import hashlib
import json
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "packaging" / "public_brand_delta_audit.py"
FIXTURE = (
    ROOT
    / "packaging"
    / "tests"
    / "fixtures"
    / "public-clean-iossimulator-arm64-baseline.json"
)
BASELINE_SHA256 = "db6a7e5b3ee9237994b0e5dcbc9cf8129ccf1e02acb798a1b4f3aaee576c8221"
BASELINE_SIZE = 72_226


class PublicBrandDeltaTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if not SCRIPT.is_file():
            raise AssertionError(f"Missing public brand delta audit: {SCRIPT}")

        from packaging import public_brand_delta_audit

        cls.audit = public_brand_delta_audit

    def test_fixed_baseline_fixture_is_the_preserved_same_rid_report(self):
        data = FIXTURE.read_bytes()
        report = json.loads(data)

        self.assertEqual(BASELINE_SIZE, len(data))
        self.assertEqual(BASELINE_SHA256, hashlib.sha256(data).hexdigest())
        self.assertFalse(report["passed"])
        self.assertEqual(580, len(report["errors"]))
        self.assertEqual(
            {"unclassified-resource": 488, "not-public": 92},
            {
                code: sum(error["code"] == code for error in report["errors"])
                for code in {error["code"] for error in report["errors"]}
            },
        )
        pairs = {(error["code"], error["path"]) for error in report["errors"]}
        self.assertEqual(580, len(pairs))
        for required in (
            "OpenRA.iOS",
            "OpenRA.iOS.aotdata.arm64",
            "OpenRA.iOS.dll",
            "OpenRA.Game.dll",
            "OpenRA.Mods.Common.dll",
            "OpenRA.Mods.RA2.dll",
            "System.Private.CoreLib.dll",
            "libmonosgen-2.0.dylib",
            "runtimeconfig.bin",
        ):
            self.assertIn(("unclassified-resource", required), pairs)
        self.assertEqual(
            {
                "mods/ra2/bits/animations/nc-impact-core.png",
                "mods/ra2/bits/animations/nc-impact-debris.png",
                "mods/ra2/bits/animations/nc-impact-ring.png",
            },
            {file["path"] for file in report["files"]},
        )
        self.assertFalse(any(error["code"] == "retail-extension" for error in report["errors"]))
        for forbidden in (".DS_Store", "mods/cnc/", "mods/ts/", "VERSION", "glsl/"):
            self.assertFalse(
                any(forbidden in error["path"] for error in report["errors"]),
                forbidden,
            )

        self.assertEqual((), self.audit.validate_fixed_baseline(FIXTURE))

    def test_incomplete_bundle_cannot_become_the_fixed_baseline(self):
        for missing in (
            "OpenRA.iOS",
            "OpenRA.iOS.aotdata.arm64",
            "OpenRA.iOS.dll",
            "OpenRA.Game.dll",
            "OpenRA.Mods.Common.dll",
            "OpenRA.Mods.RA2.dll",
            "System.Private.CoreLib.dll",
            "libmonosgen-2.0.dylib",
            "runtimeconfig.bin",
        ):
            with self.subTest(missing=missing):
                report = json.loads(FIXTURE.read_bytes())
                report["errors"] = [
                    error for error in report["errors"] if error["path"] != missing
                ]

                errors = self.audit.validate_baseline_bundle_shape(report)

                self.assertTrue(any(missing in error for error in errors))

    def test_real_changed_source_mappings_are_exact(self):
        errors = self.audit.validate_source_mappings(
            ROOT,
            ROOT / "packaging" / "public-content-manifest.json",
            ROOT / "docs" / "legal" / "public-asset-sbom.json",
        )

        self.assertEqual((), errors)

    def test_equal_error_pairs_pass_even_when_details_and_public_files_differ(self):
        baseline = self.report(("not-public", "mods/ra2/mod.yaml", "old"))
        candidate = self.report(("not-public", "mods/ra2/mod.yaml", "new"))
        candidate["files"] = [{
            "path": "en.lproj/InfoPlist.strings",
            "size": 1,
            "sha256": "0" * 64,
            "category": "project-original",
            "license": "Project original",
        }]

        self.assertEqual((), self.audit.compare_reports(baseline, candidate))

    def test_added_or_removed_error_pair_is_rejected(self):
        baseline = self.report(
            ("not-public", "mods/ra2/mod.yaml", "private"),
            ("unclassified-resource", "OpenRA.dll", ""),
        )
        added = self.report(
            ("not-public", "mods/ra2/mod.yaml", "private"),
            ("unclassified-resource", "OpenRA.dll", ""),
            ("hash-mismatch", "en.lproj/InfoPlist.strings", "changed"),
        )
        removed = self.report(("not-public", "mods/ra2/mod.yaml", "private"))

        self.assertTrue(any("added" in error for error in self.audit.compare_reports(baseline, added)))
        self.assertTrue(any("removed" in error for error in self.audit.compare_reports(baseline, removed)))

    def test_duplicate_or_malformed_report_is_rejected(self):
        duplicate = self.report(
            ("not-public", "mods/ra2/mod.yaml", "private"),
            ("not-public", "mods/ra2/mod.yaml", "private"),
        )
        malformed_cases = (
            None,
            {},
            {"passed": False, "errors": "bad", "files": []},
            {"passed": False, "errors": [{"code": 1, "path": "x"}], "files": []},
            {"passed": True, "errors": [{"code": "x", "path": "y"}], "files": []},
        )

        self.assertTrue(self.audit.validate_report(duplicate, "candidate"))
        for report in malformed_cases:
            with self.subTest(report=report):
                self.assertTrue(self.audit.validate_report(report, "candidate"))

    def test_fixed_baseline_hash_mismatch_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "baseline.json"
            path.write_bytes(FIXTURE.read_bytes() + b"\n")

            errors = self.audit.validate_fixed_baseline(path)

        self.assertTrue(any("SHA-256" in error for error in errors))

    def test_source_hash_drift_and_classification_drift_are_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.copy_mapping_fixture(root)
            manifest = root / "packaging" / "public-content-manifest.json"
            sbom = root / "docs" / "legal" / "public-asset-sbom.json"

            target = root / "mods" / "ra2" / "fluent" / "mod.ftl"
            target.write_text("mod-title = CHANGED\n", encoding="utf-8")
            errors = self.audit.validate_source_mappings(root, manifest, sbom)
            self.assertTrue(any("source hash/size drift" in error for error in errors))

            target.write_bytes((ROOT / "mods" / "ra2" / "fluent" / "mod.ftl").read_bytes())
            payload = json.loads(sbom.read_text(encoding="utf-8"))
            entry = next(item for item in payload["files"] if item["path"] == "mods/ra2/mod.yaml")
            entry["public"] = True
            sbom.write_text(json.dumps(payload), encoding="utf-8")
            errors = self.audit.validate_source_mappings(root, manifest, sbom)
            self.assertTrue(any("classification mismatch" in error for error in errors))

    def test_cli_accepts_the_fixed_fixture_as_both_reports(self):
        result = subprocess.run(
            [
                "python3",
                str(SCRIPT),
                "--baseline-report",
                str(FIXTURE),
                "--candidate-report",
                str(FIXTURE),
                "--source-root",
                str(ROOT),
                "--manifest",
                str(ROOT / "packaging" / "public-content-manifest.json"),
                "--sbom",
                str(ROOT / "docs" / "legal" / "public-asset-sbom.json"),
            ],
            check=False,
            capture_output=True,
            text=True,
        )

        self.assertEqual(0, result.returncode, result.stderr)

    @staticmethod
    def report(*errors):
        return {
            "passed": not errors,
            "errors": [
                {"code": code, "path": path, "detail": detail}
                for code, path, detail in errors
            ],
            "files": [],
        }

    @staticmethod
    def copy_mapping_fixture(root: Path):
        source_paths = [mapping.source_path for mapping in PublicBrandDeltaTest.audit.SOURCE_MAPPINGS]
        source_paths.append(PublicBrandDeltaTest.audit.NON_BUNDLE_ICON_PATH)
        for relative in source_paths:
            destination = root / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / relative, destination)

        for relative in (
            Path("packaging/public-content-manifest.json"),
            Path("docs/legal/public-asset-sbom.json"),
        ):
            destination = root / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / relative, destination)


if __name__ == "__main__":
    unittest.main()
