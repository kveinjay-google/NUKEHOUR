import hashlib
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
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
PROFILE_SPLIT_PATHS = (
    "mods/ra2/audio/voices-base.yaml",
    "mods/ra2/manifest/common.yaml",
    "mods/ra2/manifest/filesystem-desktop.yaml",
    "mods/ra2/manifest/filesystem-ios-base.yaml",
    "mods/ra2/manifest/filesystem-ios-yuri.yaml",
    "mods/ra2/manifest/gameplay-base.yaml",
    "mods/ra2/manifest/gameplay-yuri.yaml",
    "mods/ra2/mod.ios-base.yaml",
    "mods/ra2/mod.ios-yuri.yaml",
    "mods/ra2/rules/allied-infantry-base.yaml",
    "mods/ra2/rules/defaults-base.yaml",
    "mods/ra2/rules/proxy-actors-base.yaml",
    "mods/ra2/rules/soviet-infantry-base.yaml",
    "mods/ra2/rules/soviet-vehicles-base.yaml",
    "mods/ra2/rules/world-base.yaml",
    "mods/ra2/sequences/vehicles-base.yaml",
    "mods/ra2/sequences/voxels-base.yaml",
)
PUBLIC_CLEAN_PROFILE_REMOVES = (
    "../../mods/ra2/audio/voices-base.yaml",
    "../../mods/ra2/mod.ios-base.yaml",
    "../../mods/ra2/mod.ios-yuri.yaml",
    "../../mods/ra2/rules/allied-infantry-base.yaml",
    "../../mods/ra2/rules/defaults-base.yaml",
    "../../mods/ra2/rules/proxy-actors-base.yaml",
    "../../mods/ra2/rules/soviet-infantry-base.yaml",
    "../../mods/ra2/rules/soviet-vehicles-base.yaml",
    "../../mods/ra2/rules/world-base.yaml",
    "../../mods/ra2/sequences/vehicles-base.yaml",
    "../../mods/ra2/sequences/voxels-base.yaml",
)
MANIFEST_ONLY_PATHS = {
    "en.lproj/InfoPlist.strings",
    "mods/ra2/bits/animations/nc-impact-core.png",
    "mods/ra2/bits/animations/nc-impact-debris.png",
    "mods/ra2/bits/animations/nc-impact-ring.png",
    "zh-Hans.lproj/InfoPlist.strings",
}
SBOM_ONLY_PATHS = {
    "ios/OpenRA.iOS/en.lproj/InfoPlist.strings",
    "ios/OpenRA.iOS/zh-Hans.lproj/InfoPlist.strings",
}


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

    def test_existing_and_profile_split_source_mappings_are_exact(self):
        existing = tuple(
            (
                mapping.source_path.as_posix(),
                mapping.bundle_path,
                mapping.category,
                mapping.license,
                mapping.public,
            )
            for mapping in self.audit.SOURCE_MAPPINGS
        )
        self.assertEqual(
            (
                ("mods/ra2/mod.yaml", "mods/ra2/mod.yaml", *self.audit.PRIVATE_DECISION),
                (
                    "mods/ra2/chrome/mainmenu.yaml",
                    "mods/ra2/chrome/mainmenu.yaml",
                    *self.audit.PRIVATE_DECISION,
                ),
                (
                    "mods/ra2/fluent/chrome.ftl",
                    "mods/ra2/fluent/chrome.ftl",
                    *self.audit.PRIVATE_DECISION,
                ),
                (
                    "mods/ra2/fluent/mod.ftl",
                    "mods/ra2/fluent/mod.ftl",
                    *self.audit.PRIVATE_DECISION,
                ),
                (
                    "mods/ra2/fluent/zh-CN/chrome.ftl",
                    "mods/ra2/fluent/zh-CN/chrome.ftl",
                    *self.audit.PRIVATE_DECISION,
                ),
                (
                    "mods/ra2/fluent/zh-CN/mod.ftl",
                    "mods/ra2/fluent/zh-CN/mod.ftl",
                    *self.audit.PRIVATE_DECISION,
                ),
                (
                    "ios/OpenRA.iOS/en.lproj/InfoPlist.strings",
                    "en.lproj/InfoPlist.strings",
                    *self.audit.PUBLIC_DECISION,
                ),
                (
                    "ios/OpenRA.iOS/zh-Hans.lproj/InfoPlist.strings",
                    "zh-Hans.lproj/InfoPlist.strings",
                    *self.audit.PUBLIC_DECISION,
                ),
            ),
            existing,
        )

        profile_mappings = getattr(self.audit, "PROFILE_SPLIT_SOURCE_MAPPINGS", ())
        actual_profiles = tuple(
            (
                mapping.source_path.as_posix(),
                mapping.bundle_path,
                mapping.category,
                mapping.license,
                mapping.public,
            )
            for mapping in profile_mappings
        )
        self.assertEqual(
            tuple((path, path, *self.audit.PRIVATE_DECISION) for path in PROFILE_SPLIT_PATHS),
            actual_profiles,
        )
        self.assertEqual(
            list(PROFILE_SPLIT_PATHS),
            sorted(PROFILE_SPLIT_PATHS, key=lambda value: value.encode()),
        )

        all_sources = [mapping.source_path.as_posix() for mapping in self.audit.SOURCE_MAPPINGS]
        all_sources.extend(mapping.source_path.as_posix() for mapping in profile_mappings)
        all_bundles = [mapping.bundle_path for mapping in self.audit.SOURCE_MAPPINGS]
        all_bundles.extend(mapping.bundle_path for mapping in profile_mappings)
        self.assertEqual(len(all_sources), len(set(all_sources)))
        self.assertEqual(len(all_bundles), len(set(all_bundles)))

    def test_profile_split_inventories_keep_the_exact_namespace_difference(self):
        manifest = json.loads(
            (ROOT / "packaging" / "public-content-manifest.json").read_text(
                encoding="utf-8"
            )
        )
        sbom = json.loads(
            (ROOT / "docs" / "legal" / "public-asset-sbom.json").read_text(
                encoding="utf-8"
            )
        )
        manifest_paths = [entry["path"] for entry in manifest["files"]]
        sbom_paths = [entry["path"] for entry in sbom["files"]]

        self.assertEqual(280, len(manifest_paths))
        self.assertEqual(277, len(sbom_paths))
        self.assertEqual(manifest_paths, sorted(manifest_paths, key=lambda value: value.encode()))
        self.assertEqual(sbom_paths, sorted(sbom_paths, key=lambda value: value.encode()))
        self.assertEqual(len(manifest_paths), len(set(manifest_paths)))
        self.assertEqual(len(sbom_paths), len(set(sbom_paths)))
        self.assertEqual(set(PROFILE_SPLIT_PATHS), set(PROFILE_SPLIT_PATHS) & set(manifest_paths))
        self.assertEqual(set(PROFILE_SPLIT_PATHS), set(PROFILE_SPLIT_PATHS) & set(sbom_paths))
        self.assertEqual(MANIFEST_ONLY_PATHS, set(manifest_paths) - set(sbom_paths))
        self.assertEqual(SBOM_ONLY_PATHS, set(sbom_paths) - set(manifest_paths))

    def test_public_clean_has_exact_literal_profile_removes(self):
        project = ET.parse(ROOT / "ios" / "OpenRA.iOS" / "OpenRA.iOS.csproj")
        public_groups = [
            group
            for group in project.getroot().findall("ItemGroup")
            if group.get("Condition") == "'$(PublicClean)' == 'true'"
        ]
        self.assertEqual(1, len(public_groups))
        removes = tuple(
            item.get("Remove")
            for item in public_groups[0].findall("BundleResource")
            if item.get("Remove") is not None
        )
        self.assertEqual(PUBLIC_CLEAN_PROFILE_REMOVES, removes)
        self.assertFalse(any("*" in path or "?" in path for path in removes))
        children = list(public_groups[0])
        include_indices = [
            index
            for index, item in enumerate(children)
            if item.tag == "BundleResource" and item.get("Include") is not None
        ]
        remove_indices = [
            index
            for index, item in enumerate(children)
            if item.tag == "BundleResource" and item.get("Remove") is not None
        ]
        self.assertLess(max(include_indices), min(remove_indices))

    def test_historical_delta_remains_580_with_zero_new_pairs(self):
        baseline = json.loads(FIXTURE.read_bytes())
        candidate = json.loads(FIXTURE.read_bytes())
        baseline_pairs = {
            (error["code"], error["path"]) for error in baseline["errors"]
        }
        candidate_pairs = {
            (error["code"], error["path"]) for error in candidate["errors"]
        }

        self.assertEqual(580, len(baseline_pairs))
        self.assertEqual(set(), candidate_pairs - baseline_pairs)
        self.assertEqual(set(), baseline_pairs - candidate_pairs)
        self.assertEqual((), self.audit.compare_reports(baseline, candidate))

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

    def test_profile_source_hash_and_classification_drift_are_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.copy_mapping_fixture(root)
            manifest = root / "packaging" / "public-content-manifest.json"
            sbom = root / "docs" / "legal" / "public-asset-sbom.json"
            relative = Path("mods/ra2/rules/world-base.yaml")
            target = root / relative

            target.write_bytes(target.read_bytes() + b"# drift\n")
            errors = self.audit.validate_source_mappings(root, manifest, sbom)
            self.assertTrue(any("source hash/size drift" in error for error in errors))

            target.write_bytes((ROOT / relative).read_bytes())
            payload = json.loads(manifest.read_text(encoding="utf-8"))
            entry = next(item for item in payload["files"] if item["path"] == relative.as_posix())
            entry["public"] = True
            manifest.write_text(json.dumps(payload), encoding="utf-8")
            errors = self.audit.validate_source_mappings(root, manifest, sbom)
            self.assertTrue(any("classification mismatch" in error for error in errors))

    def test_missing_profile_inventory_rows_are_rejected(self):
        relative = "mods/ra2/manifest/common.yaml"
        for inventory_relative, expected in (
            (Path("packaging/public-content-manifest.json"), "manifest mapping is missing"),
            (Path("docs/legal/public-asset-sbom.json"), "SBOM mapping is missing"),
        ):
            with self.subTest(inventory=inventory_relative):
                with tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary)
                    self.copy_mapping_fixture(root)
                    inventory = root / inventory_relative
                    payload = json.loads(inventory.read_text(encoding="utf-8"))
                    payload["files"] = [
                        entry for entry in payload["files"] if entry["path"] != relative
                    ]
                    inventory.write_text(json.dumps(payload), encoding="utf-8")

                    errors = self.audit.validate_source_mappings(
                        root,
                        root / "packaging" / "public-content-manifest.json",
                        root / "docs" / "legal" / "public-asset-sbom.json",
                    )

                self.assertTrue(any(expected in error for error in errors), errors)

    def test_inventory_order_count_and_namespace_drift_are_rejected(self):
        mutations = (
            (
                Path("packaging/public-content-manifest.json"),
                lambda files: files.__setitem__(slice(0, 2), reversed(files[:2])),
                "manifest paths must be UTF-8 byte-sorted",
            ),
            (
                Path("packaging/public-content-manifest.json"),
                lambda files: files.pop(),
                "manifest must contain exactly 280 paths",
            ),
            (
                Path("docs/legal/public-asset-sbom.json"),
                lambda files: files.append(
                    {
                        **next(
                            entry
                            for entry in json.loads(
                                (ROOT / "packaging" / "public-content-manifest.json").read_text(
                                    encoding="utf-8"
                                )
                            )["files"]
                            if entry["path"] == "en.lproj/InfoPlist.strings"
                        )
                    }
                ),
                "namespace mismatch",
            ),
        )
        for inventory_relative, mutate, expected in mutations:
            with self.subTest(inventory=inventory_relative, expected=expected):
                with tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary)
                    self.copy_mapping_fixture(root)
                    inventory = root / inventory_relative
                    payload = json.loads(inventory.read_text(encoding="utf-8"))
                    mutate(payload["files"])
                    inventory.write_text(json.dumps(payload), encoding="utf-8")

                    errors = self.audit.validate_source_mappings(
                        root,
                        root / "packaging" / "public-content-manifest.json",
                        root / "docs" / "legal" / "public-asset-sbom.json",
                    )

                self.assertTrue(any(expected in error for error in errors), errors)

    def test_cli_accepts_the_fixed_fixture_as_both_reports(self):
        result = subprocess.run(
            [
                sys.executable,
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
        source_paths.extend(
            mapping.source_path
            for mapping in getattr(
                PublicBrandDeltaTest.audit,
                "PROFILE_SPLIT_SOURCE_MAPPINGS",
                (),
            )
        )
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
