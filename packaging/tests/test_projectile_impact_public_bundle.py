import hashlib
import json
import tempfile
import unittest
import xml.etree.ElementTree as ElementTree
from fnmatch import fnmatchcase
from pathlib import Path

from packaging.public_bundle_audit import audit_tree


ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "ios" / "OpenRA.iOS" / "OpenRA.iOS.csproj"
MANIFEST = ROOT / "packaging" / "public-content-manifest.json"
IMPACT_ASSET_DIRECTORY = ROOT / "mods" / "ra2" / "bits" / "animations"
PUBLIC_CLEAN_CONDITION = "'$(PublicClean)' == 'true'"
IMPACT_BUNDLE_INCLUDE = "../../mods/ra2/bits/animations/nc-impact-*.png"
IMPACT_BUNDLE_LINK = "mods/ra2/bits/animations/%(Filename)%(Extension)"

IMPACT_ASSETS = (
    "mods/ra2/bits/animations/nc-impact-core.png",
    "mods/ra2/bits/animations/nc-impact-debris.png",
    "mods/ra2/bits/animations/nc-impact-ring.png",
)
CHANGED_MIXED_SOURCE_YAML = (
    "mods/ra2/rules/soviet-vehicles.yaml",
    "mods/ra2/sequences/misc.yaml",
    "mods/ra2/weapons/explosions.yaml",
    "mods/ra2/weapons/misc.yaml",
)
FORBIDDEN_RETAIL_EXTENSIONS = (
    ".aud",
    ".bag",
    ".bik",
    ".hva",
    ".idx",
    ".mix",
    ".shp",
    ".vqa",
    ".vxl",
    ".wsa",
)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class ProjectileImpactPublicBundleTest(unittest.TestCase):
    maxDiff = None

    @classmethod
    def setUpClass(cls):
        payload = json.loads(MANIFEST.read_text(encoding="utf-8"))
        cls.manifest_files = payload["files"]
        cls.manifest_entries = {entry["path"]: entry for entry in payload["files"]}

    def selected_manifest(self, paths):
        return {
            "schemaVersion": 1,
            "files": [self.manifest_entries[path] for path in paths],
        }

    def copy_source_files(self, destination: Path, paths):
        for relative in paths:
            source = ROOT / relative
            target = destination / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(source.read_bytes())

    def test_public_clean_msbuild_item_uses_only_the_exact_impact_glob(self):
        tree = ElementTree.parse(PROJECT)
        public_groups = [
            group for group in tree.getroot().findall("ItemGroup")
            if group.get("Condition") == PUBLIC_CLEAN_CONDITION
        ]
        self.assertEqual(1, len(public_groups))

        bits_items = []
        for item in public_groups[0].findall("BundleResource"):
            include = item.get("Include", "")
            normalized = include.replace("\\", "/")
            if "/mods/ra2/bits/" in normalized:
                bits_items.append((normalized, item.findtext("Link")))

        self.assertEqual([(IMPACT_BUNDLE_INCLUDE, IMPACT_BUNDLE_LINK)], bits_items)

    def test_generated_png_inventory_is_exact_and_source_bytes_pass_audit(self):
        all_manifest_paths = tuple(entry["path"] for entry in self.manifest_files)
        self.assertEqual(len(all_manifest_paths), len(set(all_manifest_paths)))
        self.assertEqual(tuple(sorted(all_manifest_paths)), all_manifest_paths)

        source_matches = tuple(
            path.relative_to(ROOT).as_posix()
            for path in sorted(IMPACT_ASSET_DIRECTORY.glob("nc-impact-*.png"))
        )
        manifest_matches = tuple(
            sorted(
                path for path in self.manifest_entries
                if fnmatchcase(path, "mods/ra2/bits/animations/nc-impact-*.png")
            )
        )
        self.assertEqual(IMPACT_ASSETS, source_matches)
        self.assertEqual(IMPACT_ASSETS, manifest_matches)

        for relative in IMPACT_ASSETS:
            path = ROOT / relative
            entry = self.manifest_entries.get(relative)
            with self.subTest(path=relative):
                self.assertTrue(path.is_file())
                self.assertIsNotNone(entry)
                self.assertEqual(path.stat().st_size, entry["size"])
                self.assertEqual(sha256(path), entry["sha256"])
                self.assertEqual("project-original", entry["category"])
                self.assertEqual("Project original", entry["license"])
                self.assertIs(True, entry["public"])

        with tempfile.TemporaryDirectory() as directory:
            bundle = Path(directory)
            self.copy_source_files(bundle, IMPACT_ASSETS)

            result = audit_tree(bundle, self.selected_manifest(IMPACT_ASSETS))

            self.assertTrue(result.passed, result.errors)
            self.assertEqual(IMPACT_ASSETS, tuple(file.path for file in result.files))
            for audited in result.files:
                self.assertEqual((ROOT / audited.path).read_bytes(), (bundle / audited.path).read_bytes())

    def test_unlisted_bits_image_is_rejected_without_broadening_the_allowlist(self):
        with tempfile.TemporaryDirectory() as directory:
            bundle = Path(directory)
            self.copy_source_files(bundle, IMPACT_ASSETS)
            unexpected = bundle / "mods/ra2/bits/animations/nc-impact-extra.png"
            unexpected.write_bytes(b"not an approved project-original asset")

            result = audit_tree(bundle, self.selected_manifest(IMPACT_ASSETS))

            self.assertFalse(result.passed)
            self.assertIn(
                ("unclassified-resource", "mods/ra2/bits/animations/nc-impact-extra.png"),
                {(error.code, error.path) for error in result.errors},
            )

    def test_retail_extensions_remain_hard_blocked_inside_the_impact_directory(self):
        for suffix in FORBIDDEN_RETAIL_EXTENSIONS:
            with self.subTest(suffix=suffix), tempfile.TemporaryDirectory() as directory:
                bundle = Path(directory)
                relative = f"mods/ra2/bits/animations/imported-retail{suffix}"
                retail_path = bundle / relative
                retail_path.parent.mkdir(parents=True, exist_ok=True)
                retail_path.write_bytes(b"retail")
                manifest = {
                    "schemaVersion": 1,
                    "files": [{
                        "path": relative,
                        "sha256": sha256(retail_path),
                        "size": retail_path.stat().st_size,
                        "category": "project-original",
                        "license": "Project original",
                        "public": True,
                    }],
                }

                result = audit_tree(bundle, manifest)

                self.assertIn("retail-extension", {error.code for error in result.errors})

    def test_changed_mixed_source_yaml_stays_private_and_blocks_public_clean(self):
        for relative in CHANGED_MIXED_SOURCE_YAML:
            path = ROOT / relative
            entry = self.manifest_entries.get(relative)
            with self.subTest(path=relative):
                self.assertIsNotNone(entry)
                self.assertEqual(path.stat().st_size, entry["size"])
                self.assertEqual(sha256(path), entry["sha256"])
                self.assertEqual("unknown-or-derivative", entry["category"])
                self.assertEqual("Unreviewed", entry["license"])
                self.assertIs(False, entry["public"])

        selected_paths = IMPACT_ASSETS + CHANGED_MIXED_SOURCE_YAML
        with tempfile.TemporaryDirectory() as directory:
            bundle = Path(directory)
            self.copy_source_files(bundle, selected_paths)

            result = audit_tree(bundle, self.selected_manifest(selected_paths))

            self.assertFalse(result.passed)
            self.assertEqual(
                {("not-public", path) for path in CHANGED_MIXED_SOURCE_YAML},
                {(error.code, error.path) for error in result.errors},
            )
            self.assertEqual(IMPACT_ASSETS, tuple(file.path for file in result.files))


if __name__ == "__main__":
    unittest.main()
