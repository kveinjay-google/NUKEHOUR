#!/bin/sh
set -eu

host_device_id="${OPENRA_IOS_MULTIPLAYER_HOST_DEVICE_ID:-C5CA8947-3880-593B-84D6-B57DE642A8FD}"
client_device_id="${OPENRA_IOS_MULTIPLAYER_CLIENT_DEVICE_ID:-77B248CD-CB9B-583C-8E67-7FA48E6409A5}"
host_endpoint="${OPENRA_IOS_MULTIPLAYER_HOST_ENDPOINT:-}"
host_port="${OPENRA_IOS_MULTIPLAYER_SMOKE_PORT:-1234}"
discovery_mode="${OPENRA_IOS_MULTIPLAYER_SMOKE_DISCOVERY_MODE:-auto}"
bundle_id="${OPENRA_IOS_BUNDLE_ID:-com.openra.ipad.personal}"
app_bundle="${OPENRA_IOS_MULTIPLAYER_APP_BUNDLE:-$(pwd)/ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app}"
map="${OPENRA_IOS_MULTIPLAYER_SMOKE_MAP:-south-pacific}"
required_world_ticks="${OPENRA_IOS_MULTIPLAYER_SMOKE_WORLD_TICKS:-100}"
smoke_timeout_seconds="${OPENRA_IOS_MULTIPLAYER_SMOKE_TIMEOUT_SECONDS:-120}"
runner_started_utc="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

# These values are intentionally generated for every runner invocation and cannot
# be supplied by the environment. Reusing a run id or journal cannot satisfy the
# acceptance assertions for a later launch.
identity_values="$(python3 - <<'PY'
from datetime import datetime, timezone
import secrets

stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
print(f"smoke-{stamp}-{secrets.token_hex(8)}")
print(secrets.token_hex(16))
print(secrets.token_hex(16))
print(secrets.token_hex(16))
PY
)"
run_id="$(printf '%s\n' "$identity_values" | sed -n '1p')"
launch_nonce="$(printf '%s\n' "$identity_values" | sed -n '2p')"
host_nonce="$(printf '%s\n' "$identity_values" | sed -n '3p')"
client_nonce="$(printf '%s\n' "$identity_values" | sed -n '4p')"
output_root="${OPENRA_IOS_MULTIPLAYER_SMOKE_OUTPUT:-$(pwd)/docs/testing/results/multiplayer-smoke-$run_id}"

if [ "$host_device_id" = "$client_device_id" ]; then
	printf 'Host and client must be different physical devices.\n' >&2
	exit 2
fi

if [ "$discovery_mode" != "auto" ] || [ -n "$host_endpoint" ]; then
	printf 'Auto-discovery acceptance requires discovery mode auto and no HOST_ENDPOINT.\n' >&2
	exit 2
fi

expected_runtime_hash=""

mkdir -p "$output_root"
host_journal="$output_root/host.json"
client_journal="$output_root/client.json"

python3 - "$output_root/run.json" "$run_id" "$runner_started_utc" "$discovery_mode" \
	"$launch_nonce" "$host_nonce" "$client_nonce" "$expected_runtime_hash" "$app_bundle" <<'PY'
import json
import sys

(path, run_id, started, discovery_mode, launch_nonce, host_nonce,
 client_nonce, runtime_hash, app_bundle) = sys.argv[1:]
with open(path + ".incoming", "w", encoding="utf-8") as stream:
    json.dump({
        "runId": run_id,
        "runnerStartedUtc": started,
        "discoveryMode": discovery_mode,
        "launchNonce": launch_nonce,
        "hostParticipantNonce": host_nonce,
        "clientParticipantNonce": client_nonce,
        "runtimeIdentityKind": "runtime-contract-v1",
        "expectedRuntimeHash": runtime_hash,
        "appBundle": app_bundle,
    }, stream, ensure_ascii=False, indent=2)
    stream.write("\n")
PY
mv "$output_root/run.json.incoming" "$output_root/run.json"

copy_journal()
{
	device_id="$1"
	role="$2"
	destination="$3"
	temporary="$destination.incoming.$$"
	if xcrun devicectl device copy from \
		--device "$device_id" \
		--domain-type appDataContainer \
		--domain-identifier "$bundle_id" \
		--source "Documents/OpenRA/Logs/MultiplayerSmoke/$run_id/$role.json" \
		--destination "$temporary" \
		--timeout 15 >"$output_root/copy-$role.log" 2>&1; then
		mv "$temporary" "$destination"
		return 0
	fi

	if [ -e "$temporary" ]; then
		mv "$temporary" "$destination.failed-copy.$(date -u +%Y%m%dT%H%M%SZ)"
	fi
	return 1
}

collect_evidence()
{
	copy_journal "$host_device_id" host "$host_journal" || true
	copy_journal "$client_device_id" client "$client_journal" || true
	for role in host client; do
		eval "device_id=\${${role}_device_id}"
		xcrun devicectl device copy from \
			--device "$device_id" \
			--domain-type appDataContainer \
			--domain-identifier "$bundle_id" \
			--source "Documents/OpenRA/Logs/MultiplayerSmoke/$run_id" \
			--destination "$output_root/$role-report" \
			--timeout 30 >"$output_root/copy-$role-report.log" 2>&1 || true
	done
}

snapshot_openra_processes()
{
	device_id="$1"
	destination="$2"
	log="$3"
	incoming="$destination.incoming"
	if ! xcrun devicectl device info processes \
		--device "$device_id" \
		--filter "executable.path CONTAINS 'OpenRA'" \
		--json-output "$incoming" \
		--timeout 15 >"$log" 2>&1; then
		return 1
	fi

	mv "$incoming" "$destination"
}

capture_process_baseline()
{
	device_id="$1"
	role="$2"
	baseline_json="$output_root/baseline-$role.json"
	if ! snapshot_openra_processes "$device_id" "$baseline_json" \
		"$output_root/baseline-$role.log"; then
		printf 'Unable to capture the %s process baseline; refusing to launch a test that cannot be cleaned up.\n' \
			"$role" >&2
		return 1
	fi
}

terminate_launched_process()
{
	device_id="$1"
	launch_json="$2"
	role="$3"
	terminate_log="$output_root/terminate-$role.log"
	baseline_json="$output_root/baseline-$role.json"
	current_json="$output_root/current-$role.json"
	if ! snapshot_openra_processes "$device_id" "$current_json" \
		"$output_root/current-$role.log"; then
		printf 'Cleanup FAILED: unable to query current %s processes.\n' "$role" >"$terminate_log"
		return 1
	fi

	pid="$(python3 - "$launch_json" "$baseline_json" "$current_json" <<'PY'
import json
import sys

def find_pid(value):
    if isinstance(value, dict):
        for key, child in value.items():
            if key.lower() in {"processidentifier", "pid"} and isinstance(child, int):
                return child
        for child in value.values():
            found = find_pid(child)
            if found is not None:
                return found
    elif isinstance(value, list):
        for child in value:
            found = find_pid(child)
            if found is not None:
                return found
    return None

def read(path):
    try:
        with open(path, encoding="utf-8") as stream:
            return json.load(stream)
    except (FileNotFoundError, json.JSONDecodeError, OSError):
        return None

launch = read(sys.argv[1])
launch_pid = find_pid(launch)
if launch_pid is not None:
    print(launch_pid)
    raise SystemExit(0)

baseline = read(sys.argv[2])
current = read(sys.argv[3])
if baseline is None or current is None:
    raise SystemExit("missing or damaged process baseline/current snapshot")

def pids(document):
    return {
        process.get("processIdentifier")
        for process in document.get("result", {}).get("runningProcesses", [])
        if isinstance(process.get("processIdentifier"), int)
    }

new_pids = sorted(pids(current) - pids(baseline))
if len(new_pids) > 1:
    raise SystemExit("multiple new OpenRA processes make cleanup ambiguous")
print(new_pids[0] if new_pids else "")
PY
)" || {
		printf 'Cleanup FAILED: unable to resolve the launched %s PID safely.\n' "$role" >"$terminate_log"
		return 1
	}

	if [ -z "$pid" ]; then
		printf 'Cleanup verified: no new %s OpenRA process was launched.\n' "$role" >"$terminate_log"
		return 0
	fi

	verification_json="$output_root/verify-$role.json"
	attempt=1
	while [ "$attempt" -le 5 ]; do
		printf 'Cleanup attempt %s: kill %s PID %s.\n' "$attempt" "$role" "$pid" >>"$terminate_log"
		if ! xcrun devicectl device process terminate --device "$device_id" --pid "$pid" \
			--kill --timeout 15 >>"$terminate_log" 2>&1; then
			printf 'Cleanup retry: kill command failed; verifying exact PID before retry.\n' \
				>>"$terminate_log"
		fi

		if xcrun devicectl device info processes \
			--device "$device_id" \
			--filter "processIdentifier == $pid" \
			--json-output "$verification_json.incoming" \
			--timeout 15 >"$output_root/verify-$role.log" 2>&1; then
			mv "$verification_json.incoming" "$verification_json"
			running="$(python3 - "$verification_json" "$pid" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as stream:
    document = json.load(stream)

pid = int(sys.argv[2])
processes = document.get("result", {}).get("runningProcesses", [])
print("true" if any(process.get("processIdentifier") == pid for process in processes) else "false")
PY
)" || running=true
			if [ "$running" = false ]; then
				printf 'Cleanup verified: %s PID %s is absent.\n' "$role" "$pid" >>"$terminate_log"
				return 0
			fi
		fi

		attempt=$((attempt + 1))
		sleep 1
	done

	printf 'Cleanup FAILED: %s PID %s is still present or could not be verified.\n' \
		"$role" "$pid" >>"$terminate_log"
	return 1
}

cleanup()
{
	status="$?"
	trap - EXIT INT TERM
	set +e
	collect_evidence
	cleanup_status=0
	terminate_launched_process "$host_device_id" "$output_root/launch-host.json" host || cleanup_status=1
	terminate_launched_process "$client_device_id" "$output_root/launch-client.json" client || cleanup_status=1
	if [ "$status" -eq 0 ] && [ "$cleanup_status" -ne 0 ]; then
		status="$cleanup_status"
	fi
	exit "$status"
}

capture_process_baseline "$host_device_id" host
capture_process_baseline "$client_device_id" client
trap cleanup EXIT INT TERM

assert_journal()
{
	journal="$1"
	role="$2"
	local_nonce="$3"
	remote_nonce="$4"
	python3 - "$journal" "$role" "$required_world_ticks" "$run_id" "$discovery_mode" \
		"$launch_nonce" "$local_nonce" "$remote_nonce" "$expected_runtime_hash" "$runner_started_utc" <<'PY'
from datetime import datetime, timezone
import json
import sys

(path, expected_role, required_ticks, expected_run_id, expected_mode,
 expected_launch, expected_local_nonce, expected_remote_nonce,
 expected_runtime_hash, runner_started) = sys.argv[1:]
required_ticks = int(required_ticks)

with open(path, encoding="utf-8") as stream:
    report = json.load(stream)

def utc(value):
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    return parsed if parsed.tzinfo else parsed.replace(tzinfo=timezone.utc)

errors = []
checks = (
    ("status", "PASSED"),
    ("role", expected_role),
    ("runId", expected_run_id),
    ("launchNonce", expected_launch),
    ("participantNonce", expected_local_nonce),
    ("expectedRemoteNonce", expected_remote_nonce),
    ("expectedRuntimeHash", expected_runtime_hash),
    ("observedRemoteParticipantNonce", expected_remote_nonce),
)
for key, expected in checks:
    if report.get(key) != expected:
        errors.append(f"{key}={report.get(key)!r}, expected={expected!r}")
if len(report.get("runtimeHash", "")) != 64:
    errors.append(f"runtimeHash={report.get('runtimeHash')!r}")
if report.get("observedRemoteRuntimeHash") != report.get("runtimeHash"):
    errors.append(f"observedRemoteRuntimeHash={report.get('observedRemoteRuntimeHash')!r}")
if int(report.get("clients", 0)) != 2:
    errors.append(f"clients={report.get('clients')!r}")
if int(report.get("worldTick", 0)) < required_ticks:
    errors.append(f"worldTick={report.get('worldTick')!r}")
if report.get("outOfSync") is not False:
    errors.append(f"outOfSync={report.get('outOfSync')!r}")
if expected_role == "client" and report.get("discoveryStage") != "nearby-room-matched":
    errors.append(f"discoveryStage={report.get('discoveryStage')!r}")
if expected_role == "client" and int(report.get("discoveryElapsedMs", -1)) < 0:
    errors.append(f"discoveryElapsedMs={report.get('discoveryElapsedMs')!r}")

try:
    started = utc(runner_started)
    if utc(report.get("processStartedUtc", "")) < started:
        errors.append("processStartedUtc predates runner start")
    if utc(report.get("updatedUtc", "")) < started:
        errors.append("updatedUtc predates runner start")
except (AttributeError, TypeError, ValueError) as error:
    errors.append(f"invalid journal timestamp: {error}")

if errors:
    raise SystemExit(f"{expected_role} journal failed: " + ", ".join(errors))
PY
}

make_environment()
{
	role="$1"
	participant_nonce="$2"
	expected_remote_nonce="$3"
	python3 - "$role" "$participant_nonce" "$expected_remote_nonce" "$map" "$host_port" \
		"$run_id" "$required_world_ticks" "$smoke_timeout_seconds" "$discovery_mode" \
		"$expected_runtime_hash" "$launch_nonce" <<'PY'
import json
import sys

(role, participant, remote, game_map, port, run_id, ticks, timeout,
 discovery_mode, runtime_hash, launch_nonce) = sys.argv[1:]
environment = {
    "OPENRA_IOS_MULTIPLAYER_SMOKE_ROLE": role,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_DISCOVERY_MODE": discovery_mode,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_MAP": game_map,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_PORT": port,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_RUN_ID": run_id,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_WORLD_TICKS": ticks,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_TIMEOUT_SECONDS": timeout,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_LAUNCH_NONCE": launch_nonce,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_PARTICIPANT_NONCE": participant,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_EXPECTED_REMOTE_NONCE": remote,
}
print(json.dumps(environment, ensure_ascii=False, separators=(",", ":")))
PY
}

host_environment="$(make_environment host "$host_nonce" "$client_nonce")"
client_environment="$(make_environment client "$client_nonce" "$host_nonce")"

xcrun devicectl device process launch \
	--device "$host_device_id" \
	--terminate-existing \
	--timeout 30 \
	--json-output "$output_root/launch-host.json" \
	--environment-variables "$host_environment" \
	"$bundle_id" >"$output_root/launch-host.log" 2>&1

sleep 2

xcrun devicectl device process launch \
	--device "$client_device_id" \
	--terminate-existing \
	--timeout 30 \
	--json-output "$output_root/launch-client.json" \
	--environment-variables "$client_environment" \
	"$bundle_id" >"$output_root/launch-client.log" 2>&1

deadline=$(($(date +%s) + smoke_timeout_seconds + 30))
passed=false
while [ "$(date +%s)" -lt "$deadline" ]; do
	if copy_journal "$host_device_id" host "$host_journal" && \
		copy_journal "$client_device_id" client "$client_journal" && \
		grep -q '"status": "PASSED"' "$host_journal" && \
		grep -q '"status": "PASSED"' "$client_journal"; then
		passed=true
		break
	fi

	if { [ -s "$host_journal" ] && grep -q '"status": "FAILED"' "$host_journal"; } || \
		{ [ -s "$client_journal" ] && grep -q '"status": "FAILED"' "$client_journal"; }; then
		break
	fi

	sleep 2
done

if [ "$passed" != true ]; then
	printf 'Multiplayer smoke did not pass; evidence: %s\n' "$output_root" >&2
	exit 1
fi

assert_journal "$host_journal" host "$host_nonce" "$client_nonce"
assert_journal "$client_journal" client "$client_nonce" "$host_nonce"

python3 - "$host_journal" "$client_journal" "$run_id" "$launch_nonce" \
	"$host_nonce" "$client_nonce" "$expected_runtime_hash" <<'PY'
import json
import sys

host_path, client_path, run_id, launch_nonce, host_nonce, client_nonce, runtime_hash = sys.argv[1:]
with open(host_path, encoding="utf-8") as stream:
    host = json.load(stream)
with open(client_path, encoding="utf-8") as stream:
    client = json.load(stream)

checks = [
    (host.get("runId"), run_id, "host run id"),
    (client.get("runId"), run_id, "client run id"),
    (host.get("launchNonce"), launch_nonce, "host launch nonce"),
    (client.get("launchNonce"), launch_nonce, "client launch nonce"),
    (host.get("participantNonce"), host_nonce, "host participant nonce"),
    (client.get("participantNonce"), client_nonce, "client participant nonce"),
    (host.get("runtimeHash"), client.get("runtimeHash"), "shared RuntimeContract hash"),
    (host.get("sessionGuid"), client.get("observedRemoteSessionGuid"), "host session guid"),
    (client.get("sessionGuid"), host.get("observedRemoteSessionGuid"), "client session guid"),
    (host_nonce, client.get("observedRemoteParticipantNonce"), "client observed host nonce"),
    (client_nonce, host.get("observedRemoteParticipantNonce"), "host observed client nonce"),
]
for observed, expected, label in checks:
    if not expected or observed != expected:
        raise SystemExit(f"{label} mismatch: observed={observed!r}, expected={expected!r}")
if host.get("map") != client.get("map"):
    raise SystemExit(f"map mismatch: host={host.get('map')!r}, client={client.get('map')!r}")
PY

printf 'Multiplayer smoke passed: %s\n' "$output_root"
