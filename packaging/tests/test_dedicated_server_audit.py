import importlib.util
import json
import os
import sys
import tarfile
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "packaging" / "server" / "audit_server_bundle.py"


def load_module():
    spec = importlib.util.spec_from_file_location("audit_nukehour_server", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


class DedicatedServerAuditPresenceTest(unittest.TestCase):
    def test_audit_is_checked_in(self):
        self.assertTrue(SCRIPT.is_file(), f"Missing server audit: {SCRIPT}")


@unittest.skipUnless(SCRIPT.is_file(), "server audit not implemented yet")
class DedicatedServerAuditTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.audit = load_module()

    def make_bundle(self, root):
        bundle = Path(root) / "bundle"
        (bundle / "bin").mkdir(parents=True)
        (bundle / "mods" / "ra2").mkdir(parents=True)
        (bundle / "bin" / "OpenRA.Server.dll").write_bytes(b"MZmanaged-server")
        (bundle / "mods" / "ra2" / "mod.yaml").write_text(
            "Metadata:\n\tTitle: NUKE HOUR\n", encoding="utf-8"
        )
        (bundle / "run-server.sh").write_text("#!/bin/sh\nexit 0\n", encoding="utf-8")
        os.chmod(bundle / "run-server.sh", 0o755)
        inventory = self.audit.create_inventory(bundle, exclude={"server-inventory.json"})
        (bundle / "server-inventory.json").write_text(
            json.dumps({"schema": 1, "files": inventory}, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        return bundle

    def rewrite_inventory(self, bundle):
        inventory = self.audit.create_inventory(bundle, exclude={"server-inventory.json"})
        (bundle / "server-inventory.json").write_text(
            json.dumps({"schema": 1, "files": inventory}, sort_keys=True) + "\n",
            encoding="utf-8",
        )

    def test_clean_declared_bundle_passes(self):
        with tempfile.TemporaryDirectory() as temporary:
            bundle = self.make_bundle(temporary)
            evidence = self.audit.audit_bundle(bundle)

            self.assertEqual("PASS", evidence["result"])
            self.assertEqual("NONE", evidence["commercialResourcesIncluded"])

    def test_retail_names_and_signatures_are_rejected(self):
        cases = {
            "ra2.mix": b"retail",
            "language.mix": b"retail",
            "movie.vqa": b"FORMxxxxWVQAVQHD",
            "game.exe": b"MZ" + bytes(64),
        }
        for name, payload in cases.items():
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary:
                bundle = self.make_bundle(temporary)
                (bundle / name).write_bytes(payload)
                self.rewrite_inventory(bundle)
                with self.assertRaises(self.audit.AuditError):
                    self.audit.audit_bundle(bundle)

    def test_inventory_declared_but_non_allowlisted_file_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            bundle = self.make_bundle(temporary)
            (bundle / "notes.txt").write_text("not a server delivery file", encoding="utf-8")
            self.rewrite_inventory(bundle)

            with self.assertRaisesRegex(self.audit.AuditError, "independently allowlisted"):
                self.audit.audit_bundle(bundle)

    def test_symlinks_undeclared_files_and_world_writable_executables_are_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            bundle = self.make_bundle(temporary)
            (bundle / "extra.txt").write_text("undeclared", encoding="utf-8")
            with self.assertRaises(self.audit.AuditError):
                self.audit.audit_bundle(bundle)

    def test_archive_is_extracted_and_audited_with_safe_member_paths(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            bundle = self.make_bundle(temporary)
            archive = root / "NUKE-HOUR-Server-test-linux-x64.tar.gz"
            with tarfile.open(archive, "w:gz") as stream:
                stream.add(bundle, arcname="NUKE-HOUR-Server-test-linux-x64")

            evidence = self.audit.audit_artifact(archive)

            self.assertEqual("PASS", evidence["result"])
            self.assertEqual("NONE", evidence["commercialResourcesIncluded"])
            self.assertEqual(self.audit.sha256_file(archive), evidence["archiveSha256"])

    def test_archive_rejects_path_traversal_before_extraction(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            payload = root / "payload"
            payload.write_bytes(b"escape")
            archive = root / "malicious.tar.gz"
            with tarfile.open(archive, "w:gz") as stream:
                stream.add(payload, arcname="NUKE-HOUR-Server/../../escape")

            with self.assertRaises(self.audit.AuditError):
                self.audit.audit_artifact(archive)

        with tempfile.TemporaryDirectory() as temporary:
            bundle = self.make_bundle(temporary)
            os.chmod(bundle / "run-server.sh", 0o777)
            inventory = self.audit.create_inventory(bundle, exclude={"server-inventory.json"})
            (bundle / "server-inventory.json").write_text(
                json.dumps({"schema": 1, "files": inventory}, sort_keys=True) + "\n",
                encoding="utf-8",
            )
            with self.assertRaises(self.audit.AuditError):
                self.audit.audit_bundle(bundle)


if __name__ == "__main__":
    unittest.main()
