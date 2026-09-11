import json
import unittest
import xml.etree.ElementTree as ElementTree
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "ios" / "OpenRA.iOS" / "OpenRA.iOS.csproj"
MANIFEST = ROOT / "packaging" / "ios" / "public_clean_manifest.json"
PUBLIC_CONDITION = "'$(PublicClean)' == 'true'"

MANDATORY_PRESENTATION_PATHS = (
    "mods/ra2/fonts/NotoSansCJKsc-Regular.otf",
    "mods/ra2/fonts/NotoSansCJKsc-Bold.otf",
    "mods/ra2/uibits/menubuttons.png",
    "mods/ra2/uibits/menupanel.png",
    "mods/ra2/uibits/chrome.png",
    "mods/ra2/uibits/ios-commandbar-icons-v2.png",
    "mods/ra2/uibits/NUCLEAR-CRISIS-BG-06.png",
)


class IosPublicPresentationContractTest(unittest.TestCase):
    def setUp(self):
        self.project = ElementTree.parse(PROJECT).getroot()
        self.manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        self.entries = {entry["path"]: entry for entry in self.manifest["files"]}

    def public_bundle_resources(self):
        groups = [group for group in self.project.findall("ItemGroup")
                  if group.get("Condition") == PUBLIC_CONDITION]
        self.assertEqual(1, len(groups))
        return [item.get("Include", "").replace("\\", "/")
                for item in groups[0].findall("BundleResource")]

    def test_public_build_includes_engine_and_presentation_dependency_roots(self):
        includes = self.public_bundle_resources()

        self.assertIn("../../engine/VERSION", includes)
        self.assertIn("../../engine/glsl/**/*", includes)
        self.assertIn("../../mods/ra2/fonts/**/*", includes)
        self.assertIn("../../mods/ra2/uibits/**/*", includes)

    def test_public_build_bundles_the_complete_hash_locked_reviewed_bits_tree(self):
        includes = self.public_bundle_resources()

        self.assertIn("../../mods/ra2/bits/**/*", includes)
        self.assertNotIn("../../mods/ra2/bits/animations/nc-impact-*.png", includes)
        self.assertEqual([], self.manifest.get("trees", []))

        approved_bits = {
            path.relative_to(ROOT).as_posix()
            for path in (ROOT / "mods" / "ra2" / "bits").rglob("*")
            if path.is_file()
        }
        manifest_bits = {
            path for path, entry in self.entries.items()
            if path.startswith("mods/ra2/bits/") and entry.get("public")
        }
        self.assertEqual(approved_bits, manifest_bits)
        self.assertEqual(52, len(manifest_bits))
        self.assertEqual(
            {"project-original", "redistributable-third-party"},
            {self.entries[path]["category"] for path in manifest_bits},
        )

    def test_public_build_forces_a_clean_rebuild(self):
        script = (ROOT / "ios" / "scripts" / "build-public-clean.sh").read_text(
            encoding="utf-8")

        self.assertIn("-t:Rebuild", script)

    def test_public_build_strips_unused_ios_sdk_privacy_bindings(self):
        project = PROJECT.read_text(encoding="utf-8")
        info_plist = (ROOT / "ios" / "OpenRA.iOS" / "Info.plist").read_text(
            encoding="utf-8")

        self.assertIn("<MtouchLink>SdkOnly</MtouchLink>", project)
        self.assertNotIn("<MtouchLink>None</MtouchLink>", project)
        frameworks = " ".join(
            line for line in project.splitlines() if "<Frameworks>" in line)
        self.assertIn("CoreBluetooth", frameworks)
        self.assertIn("CoreMotion", frameworks)
        self.assertIn("<key>NSBluetoothAlwaysUsageDescription</key>", info_plist)
        for unused_key in (
            "NSLocationAlwaysAndWhenInUseUsageDescription",
            "NSLocationWhenInUseUsageDescription",
            "NSSiriUsageDescription",
            "NSContactsUsageDescription",
            "NSMicrophoneUsageDescription",
            "NSAppleMusicUsageDescription",
            "NSCalendarsUsageDescription",
        ):
            self.assertNotIn(f"<key>{unused_key}</key>", info_plist)

    def test_public_ipa_packaging_does_not_emit_macos_metadata_entries(self):
        script = (ROOT / "ios" / "scripts" / "build-public-clean.sh").read_text(
            encoding="utf-8")

        self.assertIn("COPYFILE_DISABLE=1", script)
        self.assertIn("/usr/bin/zip -qry", script)
        self.assertNotIn("--sequesterRsrc", script)

    def test_mandatory_presentation_files_are_public_and_hash_locked(self):
        for path in MANDATORY_PRESENTATION_PATHS:
            with self.subTest(path=path):
                entry = self.entries.get(path)
                self.assertIsNotNone(entry)
                self.assertTrue(entry["public"])
                self.assertIn(entry["category"], {
                    "engine-source", "project-original", "redistributable-third-party"})
                source = ROOT / path
                self.assertTrue(source.is_file())
                self.assertEqual(source.stat().st_size, entry["size"])
                self.assertRegex(entry["sha256"], r"^[0-9a-f]{64}$")

    def test_public_project_does_not_bundle_retail_or_maps(self):
        includes = self.public_bundle_resources()

        self.assertFalse(any("mods/ra2/maps" in path for path in includes))
        self.assertFalse(any(Path(path).suffix.lower() in {
            ".mix", ".map", ".mpr", ".oramap", ".yrm", ".bag", ".idx"
        } for path in includes))

    def test_recursive_content_include_excludes_finder_metadata(self):
        groups = [group for group in self.project.findall("ItemGroup")
                  if group.get("Condition") == PUBLIC_CONDITION]
        content = next(item for item in groups[0].findall("BundleResource")
                       if item.get("Include") == "../../mods/ra2-content/**/*")

        self.assertEqual(
            "../../mods/ra2-content/.DS_Store;../../mods/ra2-content/**/.DS_Store",
            content.get("Exclude"),
        )

    def test_candidate_build_is_newer_than_rejected_build_17(self):
        project_build = int(self.project.findtext("PropertyGroup/ApplicationVersion"))
        plist = ElementTree.parse(ROOT / "ios" / "OpenRA.iOS" / "Info.plist").getroot()
        values = list(plist.find("dict"))
        plist_build = next(
            values[index + 1].text
            for index, element in enumerate(values)
            if element.tag == "key" and element.text == "CFBundleVersion")

        self.assertGreater(project_build, 17)
        self.assertEqual(str(project_build), plist_build)


if __name__ == "__main__":
    unittest.main()
