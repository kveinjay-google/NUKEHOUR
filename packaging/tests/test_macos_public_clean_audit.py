import hashlib
import json
import os
import plistlib
import tempfile
import unittest
from pathlib import Path

from packaging.macos import public_clean_audit as audit


class MacOSPublicCleanAuditTest(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="nukehour-audit-test-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)

    def write(self, relative, data=b"data"):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        return path

    @staticmethod
    def manifest_entry(destination, data, source="source"):
        return {
            "source": source,
            "destination": destination,
            "size": len(data),
            "sha256": hashlib.sha256(data).hexdigest(),
            "category": "project-original",
            "license": "NUKE HOUR Project",
        }

    def test_runtime_accepts_exact_manifest_and_generated_runtime_prefixes(self):
        data = b"Name: NUKE HOUR\n"
        self.write("mods/ra2/mod.yaml", data)
        self.write("engine/bin/OpenRA.dll", b"compiled engine")
        self.write("dotnet/host/fxr/8/libhostfxr.dylib", b"runtime")
        manifest = {"schemaVersion": 1, "files": [
            self.manifest_entry("mods/ra2/mod.yaml", data),
        ]}

        result = audit.audit_runtime(self.root, manifest)

        self.assertTrue(result.passed, result.errors)

    def test_runtime_accepts_generated_engine_mix_filename_index(self):
        self.write("engine/global mix database.dat", b"filename-index")

        result = audit.audit_runtime(
            self.root, {"schemaVersion": 1, "files": []})

        self.assertTrue(result.passed, result.errors)

    def test_runtime_rejects_retail_maps_unlisted_files_and_unsafe_symlinks(self):
        self.write("mods/ra2/private.mix", b"retail")
        self.write("mods/ra2/maps/builtin.oramap", b"map")
        self.write("mods/ra2/unlisted.png", b"unknown")
        outside = self.write("outside.txt", b"outside")
        link = self.root / "mods/ra2/link.yaml"
        link.parent.mkdir(parents=True, exist_ok=True)
        os.symlink(outside, link)

        result = audit.audit_runtime(self.root / "mods", {"schemaVersion": 1, "files": []})

        codes = {error.code for error in result.errors}
        self.assertIn("retail-resource", codes)
        self.assertIn("bundled-map", codes)
        self.assertIn("unlisted-resource", codes)
        self.assertIn("unsafe-symlink", codes)

    def test_runtime_rejects_hash_drift_and_missing_required_entry(self):
        self.write("mod.yaml", b"modified")
        manifest = {"schemaVersion": 1, "files": [
            self.manifest_entry("mod.yaml", b"original"),
            self.manifest_entry("missing.ftl", b"required"),
        ]}

        result = audit.audit_runtime(self.root, manifest)

        codes = [error.code for error in result.errors]
        self.assertIn("hash-mismatch", codes)
        self.assertIn("manifest-file-missing", codes)

    def test_app_metadata_rejects_ra2_visible_name_but_not_internal_identifier(self):
        app = self.root / "NUKE HOUR.app"
        contents = app / "Contents"
        contents.mkdir(parents=True)
        with (contents / "Info.plist").open("wb") as stream:
            plistlib.dump({
                "CFBundleDisplayName": "NUKE HOUR RA2",
                "CFBundleName": "NUKE HOUR",
                "CFBundleIdentifier": "com.openra.ra2mac.public",
                "CFBundleShortVersionString": "1.0.3",
            }, stream)

        result = audit.audit_app(app, {"schemaVersion": 1, "files": []}, "1.0.3",
                                 require_executable=False)

        self.assertIn("visible-ra2-brand", {error.code for error in result.errors})
        self.assertNotIn("bundle-identifier", {error.code for error in result.errors})

    def test_app_rejects_retail_resources_outside_the_runtime_directory(self):
        app = self.root / "NUKE HOUR.app"
        contents = app / "Contents"
        contents.mkdir(parents=True)
        with (contents / "Info.plist").open("wb") as stream:
            plistlib.dump({
                "CFBundleDisplayName": "NUKE HOUR",
                "CFBundleName": "NUKE HOUR",
                "CFBundleIdentifier": "com.nukehour.macos.publicclean",
                "CFBundleShortVersionString": "1.0.3",
            }, stream)
        self.write("NUKE HOUR.app/Contents/Frameworks/private.mix", b"retail")

        result = audit.audit_app(app, {"schemaVersion": 1, "files": []}, "1.0.3",
                                 require_executable=False)

        self.assertIn("retail-resource", {error.code for error in result.errors})

    def test_dmg_inventory_is_exact(self):
        good = audit.audit_dmg_inventory(["NUKE HOUR.app", "Applications", ".background",
                                          ".VolumeIcon.icns", ".fseventsd"])
        bad = audit.audit_dmg_inventory(["NUKE HOUR.app", "Applications", "private.mix"])

        self.assertTrue(good.passed)
        self.assertIn("unexpected-dmg-entry", {error.code for error in bad.errors})

    def test_report_writers_emit_pass_and_file_inventory(self):
        data = b"clean"
        self.write("clean.yaml", data)
        manifest = {"schemaVersion": 1, "files": [
            self.manifest_entry("clean.yaml", data),
        ]}
        result = audit.audit_runtime(self.root, manifest)
        json_path = self.root / "report.json"
        markdown_path = self.root / "report.md"

        audit.write_reports(result, json_path, markdown_path)

        self.assertTrue(json.loads(json_path.read_text())["passed"])
        self.assertIn("Result: **PASS**", markdown_path.read_text())


if __name__ == "__main__":
    unittest.main()
