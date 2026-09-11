import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


REPOSITORY = Path(__file__).resolve().parents[2]
SCRIPT = REPOSITORY / "tools" / "run_dedicated_server_smoke.py"


def load_module():
    spec = importlib.util.spec_from_file_location("dedicated_server_smoke", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class DedicatedServerSmokeTest(unittest.TestCase):
    def test_schema_requires_exact_ds1_through_ds10(self):
        module = load_module()
        report = module.new_report("release-20250330")
        self.assertEqual(list(report["cases"]), [f"DS{i}" for i in range(1, 11)])
        with self.assertRaises(module.EvidenceError):
            module.validate_report(report)

        for case in report["cases"].values():
            case.update(result="PASS", evidence="controlled fixture")
        module.validate_report(report)

    def test_physical_session_requires_full_synchronized_gameplay(self):
        module = load_module()
        base = {
            "status": "PASSED",
            "runId": "dedicated-physical-run",
            "role": "client",
            "discoveryMode": "direct",
            "endpoint": "192.168.1.2:12340",
            "processStartedUtc": "2026-09-08T12:00:00Z",
            "updatedUtc": "2026-09-08T12:05:00Z",
            "runtimeHash": "a" * 64,
            "map": "map-uid",
            "clients": 2,
            "worldTick": 1800,
            "outOfSync": False,
            "orderRejectionObserved": False,
            "spawnObserved": True,
            "selectObserved": True,
            "movementObserved": True,
            "resourceUpdateObserved": True,
            "productionObserved": True,
            "completedProductionObserved": True,
            "attackObserved": True,
        }
        with tempfile.TemporaryDirectory() as temporary:
            first = Path(temporary) / "first.json"
            second = Path(temporary) / "second.json"
            status = Path(temporary) / "server-status.json"
            first.write_text(json.dumps(base), encoding="utf-8")
            second.write_text(json.dumps(dict(base, role="host")), encoding="utf-8")
            status.write_text(json.dumps({
                "product": "NUKE HOUR Dedicated Server",
                "platform": "linux-server",
                "listenPort": 12340,
                "map": "map-uid",
                "transportHandshake": 7,
                "handshakeSchema": 1,
                "ordersProtocol": 23,
                "runtimeContractSchema": 1,
                "updatedUtc": "2026-09-08T12:05:30Z",
            }), encoding="utf-8")
            evidence = module.validate_physical_session(first, second, status, 1800)
            self.assertEqual(evidence["result"], "PASS")
            self.assertEqual(evidence["ticks"], 1800)

            invalid = dict(base, role="host", attackObserved=False)
            second.write_text(json.dumps(invalid), encoding="utf-8")
            with self.assertRaises(module.EvidenceError):
                module.validate_physical_session(first, second, status, 1800)

            second.write_text(json.dumps(base), encoding="utf-8")
            with self.assertRaises(module.EvidenceError):
                module.validate_physical_session(first, second, status, 1800)

    def test_report_writer_omits_private_network_and_device_fields(self):
        module = load_module()
        report = module.new_report("release-20250330")
        report["source"] = {
            "endpoint": "192.168.1.2:1234",
            "deviceIdentifier": "private-device-id",
            "result": "PASS",
        }
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "report.json"
            module.write_report(output, report)
            text = output.read_text(encoding="utf-8")
            self.assertNotIn("192.168.1.2", text)
            self.assertNotIn("private-device-id", text)
            self.assertIn('"result": "PASS"', text)


if __name__ == "__main__":
    unittest.main()
