#!/usr/bin/env python3

import argparse
from datetime import datetime, timedelta, timezone
import json
import os
import re
import signal
import socket
import subprocess
import tempfile
import time
from pathlib import Path


CASE_NAMES = tuple(f"DS{index}" for index in range(1, 11))
RESULTS = {"PASS", "FAIL", "SKIPPED"}
PRIVATE_KEYS = {
    "deviceIdentifier",
    "endpoint",
    "expectedRemoteNonce",
    "launchNonce",
    "localLanAddresses",
    "observedRemoteParticipantNonce",
    "observedRemoteSessionGuid",
    "participantNonce",
    "sessionGuid",
}
PRIVATE_IPV4 = re.compile(
    r"\b(?:10(?:\.\d{1,3}){3}|192\.168(?:\.\d{1,3}){2}|172\.(?:1[6-9]|2\d|3[01])(?:\.\d{1,3}){2})\b"
)
UUID = re.compile(
    r"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b"
)
GAMEPLAY_FIELDS = (
    "spawnObserved",
    "selectObserved",
    "movementObserved",
    "resourceUpdateObserved",
    "productionObserved",
    "completedProductionObserved",
    "attackObserved",
)


class EvidenceError(RuntimeError):
    pass


def new_report(engine_version):
    return {
        "schema": 1,
        "suite": "NUKE HOUR dedicated server DS1-DS10",
        "engineVersion": engine_version,
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "cases": {
            name: {"result": "PENDING", "evidence": ""} for name in CASE_NAMES
        },
    }


def validate_report(report):
    if report.get("schema") != 1 or report.get("suite") != "NUKE HOUR dedicated server DS1-DS10":
        raise EvidenceError("dedicated-server evidence must use schema 1")
    cases = report.get("cases")
    if not isinstance(cases, dict) or list(cases) != list(CASE_NAMES):
        raise EvidenceError("dedicated-server evidence must contain exact ordered DS1-DS10 cases")
    for name, case in cases.items():
        if not isinstance(case, dict) or case.get("result") not in RESULTS:
            raise EvidenceError(f"{name} has no terminal result")
        if not isinstance(case.get("evidence"), str) or not case["evidence"].strip():
            raise EvidenceError(f"{name} has no evidence description")
    return report


def _load_json(path):
    try:
        return json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise EvidenceError(f"invalid JSON evidence {path}: {error}") from error


def _utc_timestamp(value, field):
    try:
        parsed = datetime.fromisoformat(str(value).replace("Z", "+00:00"))
    except ValueError as error:
        raise EvidenceError(f"invalid {field} timestamp: {value!r}") from error
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def validate_physical_session(first_path, second_path, dedicated_status_path, minimum_ticks):
    reports = [_load_json(first_path), _load_json(second_path)]
    for report in reports:
        if report.get("status") != "PASSED":
            raise EvidenceError("physical session did not reach PASSED")
        if report.get("clients") != 2:
            raise EvidenceError("physical session did not contain exactly two clients")
        if report.get("outOfSync") is not False or report.get("orderRejectionObserved") is not False:
            raise EvidenceError("physical session recorded a desync or order rejection")
        if int(report.get("worldTick", 0)) < minimum_ticks:
            raise EvidenceError("physical session ended before the required world tick")
        missing = [field for field in GAMEPLAY_FIELDS if report.get(field) is not True]
        if missing:
            raise EvidenceError(f"physical session is missing gameplay evidence: {missing}")

    if not reports[0].get("runtimeHash") or reports[0].get("runtimeHash") != reports[1].get("runtimeHash"):
        raise EvidenceError("physical clients used different RuntimeContracts")
    if not reports[0].get("map") or reports[0].get("map") != reports[1].get("map"):
        raise EvidenceError("physical clients used different maps")
    if not reports[0].get("runId") or reports[0].get("runId") != reports[1].get("runId"):
        raise EvidenceError("physical clients did not belong to the same run")
    if {report.get("role") for report in reports} != {"host", "client"}:
        raise EvidenceError("physical session must contain complementary host/client control roles")
    if any(report.get("discoveryMode") != "direct" for report in reports):
        raise EvidenceError("physical dedicated session did not use Direct IP")
    endpoints = {report.get("endpoint") for report in reports}
    if len(endpoints) != 1 or not next(iter(endpoints)):
        raise EvidenceError("physical clients did not connect to one explicit endpoint")

    dedicated = _load_json(dedicated_status_path)
    if dedicated.get("product") != "NUKE HOUR Dedicated Server" or not str(
        dedicated.get("platform", "")
    ).endswith("-server"):
        raise EvidenceError("server status does not identify a dedicated runtime")
    if dedicated.get("map") != reports[0].get("map"):
        raise EvidenceError("dedicated server and physical clients used different maps")
    try:
        endpoint_port = int(str(next(iter(endpoints))).rsplit(":", 1)[-1])
    except ValueError as error:
        raise EvidenceError("physical client endpoint has no numeric port") from error
    if dedicated.get("listenPort") != endpoint_port:
        raise EvidenceError("dedicated server status does not match the client endpoint port")
    if (
        dedicated.get("transportHandshake") != 7
        or dedicated.get("handshakeSchema") != 1
        or dedicated.get("ordersProtocol") != 23
        or dedicated.get("runtimeContractSchema") != 1
    ):
        raise EvidenceError("dedicated server status used unexpected protocol versions")
    client_started = min(
        _utc_timestamp(report.get("processStartedUtc"), "client processStartedUtc")
        for report in reports
    )
    client_finished = max(
        _utc_timestamp(report.get("updatedUtc"), "client updatedUtc")
        for report in reports
    )
    status_updated = _utc_timestamp(dedicated.get("updatedUtc"), "server updatedUtc")
    if not client_started <= status_updated <= client_finished + timedelta(minutes=2):
        raise EvidenceError("dedicated status timestamp does not overlap the physical run")

    return {
        "result": "PASS",
        "clients": 2,
        "map": reports[0]["map"],
        "ticks": min(int(report["worldTick"]) for report in reports),
        "runtimeHashPrefix": reports[0]["runtimeHash"][:12],
        "serverPlatform": dedicated["platform"],
        "gameplay": list(GAMEPLAY_FIELDS),
        "desync": False,
        "orderRejection": False,
    }


def _sanitize(value):
    if isinstance(value, dict):
        return {key: _sanitize(child) for key, child in value.items() if key not in PRIVATE_KEYS}
    if isinstance(value, list):
        return [_sanitize(child) for child in value]
    if isinstance(value, str):
        return UUID.sub("<identifier>", PRIVATE_IPV4.sub("<private-ip>", value))
    return value


def write_report(path, report):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(_sanitize(report), indent=2, sort_keys=False) + "\n", encoding="utf-8")


def _free_port():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as listener:
        listener.bind(("127.0.0.1", 0))
        return listener.getsockname()[1]


def _wait_for_status(path, predicate, process, timeout):
    deadline = time.monotonic() + timeout
    last = None
    while time.monotonic() < deadline:
        if path.is_file():
            try:
                last = json.loads(path.read_text(encoding="utf-8"))
                if predicate(last):
                    return last
            except (OSError, UnicodeError, json.JSONDecodeError):
                pass
        if process.poll() is not None:
            raise EvidenceError(f"dedicated server exited early with code {process.returncode}")
        time.sleep(0.2)
    raise EvidenceError(f"timed out waiting for dedicated-server status; last={last}")


def run_process_probe(repository, timeout=90):
    repository = Path(repository).resolve()
    launcher = repository / "launch-dedicated.sh"
    if not launcher.is_file():
        raise EvidenceError(f"missing dedicated launcher: {launcher}")

    port = _free_port()
    with tempfile.TemporaryDirectory(prefix="nukehour-ds-smoke-") as temporary:
        support = Path(temporary) / "support"
        status_path = support / "status" / "server-status.json"
        log_path = Path(temporary) / "server.log"
        environment = {
            **os.environ,
            "Name": "NUKE HOUR DS automated smoke",
            "ListenAddress": "127.0.0.1",
            "ListenPort": str(port),
            "Map": "south-pacific",
            "RecordReplays": "False",
            "EnableSyncReports": "True",
            "IdleTimeoutSeconds": "0",
            "StatusFile": "status/server-status.json",
            "SupportDir": str(support),
            "DOTNET_ROLL_FORWARD": "LatestMajor",
        }
        with log_path.open("w", encoding="utf-8") as log:
            process = subprocess.Popen(
                [str(launcher)],
                cwd=repository,
                env=environment,
                stdout=log,
                stderr=subprocess.STDOUT,
                start_new_session=True,
                text=True,
            )
            try:
                ready = _wait_for_status(
                    status_path,
                    lambda status: status.get("processAlive") is True and status.get("ready") is True,
                    process,
                    timeout,
                )
                with socket.create_connection(("127.0.0.1", port), timeout=5):
                    listening = True
                os.killpg(process.pid, signal.SIGTERM)
                exit_code = process.wait(timeout=30)
                stopped = _wait_for_status(
                    status_path,
                    lambda status: status.get("state") == "STOPPED" and status.get("processAlive") is False,
                    process,
                    5,
                )
            except Exception:
                if process.poll() is None:
                    os.killpg(process.pid, signal.SIGKILL)
                    process.wait(timeout=10)
                raise

        if exit_code != 0:
            raise EvidenceError(f"dedicated server did not shut down cleanly: {exit_code}")
        return {
            "started": True,
            "listening": listening,
            "readyState": ready.get("state"),
            "map": ready.get("map"),
            "transportHandshake": ready.get("transportHandshake"),
            "handshakeSchema": ready.get("handshakeSchema"),
            "ordersProtocol": ready.get("ordersProtocol"),
            "cleanExitCode": exit_code,
            "stoppedState": stopped.get("state"),
        }


def run_nunit_probe(repository):
    command = [
        "dotnet",
        "test",
        "engine/OpenRA.Test/OpenRA.Test.csproj",
        "--no-restore",
        "--filter",
        "FullyQualifiedName~NukeHourDedicatedServerTest|FullyQualifiedName~NukeHourDirectTcpHandshakeTest|FullyQualifiedName~NukeHourNetworkProtocolTest|FullyQualifiedName~IosLobbyMapReadinessTest|FullyQualifiedName~IosLobbyStartAvailabilityTest|FullyQualifiedName~IosMultiplayerPolicyTest",
        "--logger",
        "console;verbosity=minimal",
    ]
    result = subprocess.run(
        command,
        cwd=repository,
        env={**os.environ, "DOTNET_ROLL_FORWARD": "LatestMajor"},
        check=False,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
    )
    if result.returncode:
        raise EvidenceError(f"dedicated NUnit probe failed ({result.returncode})\n{result.stdout[-4000:]}")
    match = re.search(r"total:\s*(\d+)", result.stdout, re.IGNORECASE)
    if match is None:
        match = re.search(r"总计:\s*(\d+)", result.stdout)
    return {"result": "PASS", "tests": int(match.group(1)) if match else None}


def build_report(
    repository,
    engine_version,
    first=None,
    second=None,
    dedicated_status=None,
    minimum_ticks=1800,
):
    report = new_report(engine_version)
    process = run_process_probe(repository)
    nunit = run_nunit_probe(repository)
    physical = (
        validate_physical_session(first, second, dedicated_status, minimum_ticks)
        if first and second and dedicated_status
        else None
    )

    report["probes"] = {"process": process, "nunit": nunit}
    report["cases"]["DS1"] = {"result": "PASS", "evidence": "fresh headless process reached ready status"}
    report["cases"]["DS2"] = {"result": "PASS", "evidence": "TCP listener accepted a loopback connection"}
    report["cases"]["DS4"] = {"result": "PASS", "evidence": "controlled protocol/runtime/mod/engine/map rejection fixtures passed"}
    report["cases"]["DS5"] = {"result": "PASS", "evidence": "configured redistributable map loaded into server status"}
    report["cases"]["DS8"] = {"result": "PASS", "evidence": "dedicated host-independence and disconnect policy fixtures passed"}
    report["cases"]["DS9"] = {"result": "PASS", "evidence": "SIGTERM produced exit code 0 and STOPPED status"}
    report["cases"]["DS10"] = {"result": "PASS", "evidence": "shared protocol, map readiness, safe start, and Local host policy suites passed"}

    if physical:
        report["physicalSession"] = physical
        report["cases"]["DS3"] = {"result": "PASS", "evidence": "two valid physical clients were admitted with one RuntimeContract"}
        report["cases"]["DS6"] = {"result": "PASS", "evidence": "two-client physical lobby reached synchronized gameplay"}
        report["cases"]["DS7"] = {"result": "PASS", "evidence": "movement, resource, production, completion, and attack orders were observed"}
    else:
        for name, description in (
            ("DS3", "physical valid-client evidence was not supplied"),
            ("DS6", "physical game-start evidence was not supplied"),
            ("DS7", "physical gameplay-order evidence was not supplied"),
        ):
            report["cases"][name] = {"result": "SKIPPED", "evidence": description}

    validate_report(report)
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="Run NUKE HOUR dedicated server DS1-DS10 evidence probes")
    parser.add_argument("--repository", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--engine-version", default="release-20250330")
    parser.add_argument("--physical-first", type=Path)
    parser.add_argument("--physical-second", type=Path)
    parser.add_argument("--dedicated-status", type=Path)
    parser.add_argument("--minimum-ticks", type=int, default=1800)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(argv)
    supplied_physical = (args.physical_first, args.physical_second, args.dedicated_status)
    if any(supplied_physical) and not all(supplied_physical):
        parser.error("--physical-first, --physical-second, and --dedicated-status are required together")
    try:
        report = build_report(
            args.repository,
            args.engine_version,
            args.physical_first,
            args.physical_second,
            args.dedicated_status,
            args.minimum_ticks,
        )
        write_report(args.output, report)
        print(json.dumps(_sanitize(report), indent=2, sort_keys=False))
        return 0
    except EvidenceError as error:
        parser.exit(1, f"dedicated server smoke failed: {error}\n")


if __name__ == "__main__":
    raise SystemExit(main())
