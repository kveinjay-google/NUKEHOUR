import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).parents[1] / "nukehour_version.py"
SPEC = importlib.util.spec_from_file_location("nukehour_version", MODULE_PATH)
nukehour_version = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = nukehour_version
SPEC.loader.exec_module(nukehour_version)


class NukeHourVersionTests(unittest.TestCase):
    def make_root(self, platform="macos", version="1.0.1", build=1):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        (root / "packaging").mkdir()
        (root / "mods/ra2").mkdir(parents=True)
        (root / "mods/ra2-content").mkdir(parents=True)
        (root / "mods/ra2/rules").mkdir(parents=True)
        (root / "mods/ra2/weapons").mkdir(parents=True)
        (root / "engine/OpenRA.Game/Network").mkdir(parents=True)
        (root / "OpenRA.Mods.RA2/Traits").mkdir(parents=True)
        manifest = {
            "schema": 1,
            "platform": platform,
            "version": version,
            "build": build,
            "channel": "stable",
        }
        (root / "packaging/nukehour-version.json").write_text(
            json.dumps(manifest) + "\n", encoding="utf-8")
        (root / "packaging/nukehour-compatibility-inputs.txt").write_text(
            "engine/OpenRA.Game/Network/**/*.cs\n"
            "OpenRA.Mods.RA2/Traits/**/*.cs\n"
            "mods/ra2/rules/**/*.yaml\n"
            "mods/ra2/weapons/**/*.yaml\n",
            encoding="utf-8",
        )
        (root / "engine/OpenRA.Game/Network/Order.cs").write_text("network", encoding="utf-8")
        (root / "OpenRA.Mods.RA2/Traits/Core.cs").write_text("trait", encoding="utf-8")
        (root / "mods/ra2/rules/core.yaml").write_text("Rule: 1\n", encoding="utf-8")
        (root / "mods/ra2/weapons/core.yaml").write_text("Weapon: 1\n", encoding="utf-8")
        (root / "mods/ra2/mod.yaml").write_text(
            "Metadata:\n\tTitle: mod-title\n\tVersion: {DEV_VERSION}\n\n"
            "MapFolders:\n\tra2|maps: System\n"
            "\t~^SupportDir|maps/ra2/{DEV_VERSION}: User\n",
            encoding="utf-8",
        )
        (root / "mods/ra2-content/mod.yaml").write_text(
            "Metadata:\n\tTitle: content\n\tVersion: {DEV_VERSION}\n",
            encoding="utf-8",
        )
        return root

    def test_manifest_rejects_unknown_fields_and_invalid_values(self):
        root = self.make_root()
        path = root / "packaging/nukehour-version.json"
        for update in (
            {"extra": True},
            {"version": "1.0"},
            {"build": 0},
            {"channel": "development"},
            {"platform": "windows"},
        ):
            data = json.loads(path.read_text(encoding="utf-8"))
            data.update(update)
            path.write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaises(nukehour_version.VersionError):
                nukehour_version.load_manifest(root)
            path.write_text(json.dumps({
                "schema": 1, "platform": "macos", "version": "1.0.1",
                "build": 1, "channel": "stable"}), encoding="utf-8")

    def test_apply_is_idempotent_and_check_detects_stale_projection(self):
        root = self.make_root()
        first = nukehour_version.apply(root)
        second = nukehour_version.apply(root)
        self.assertEqual(first, second)
        nukehour_version.check(root)
        props = root / "packaging/nukehour-version.props"
        props.write_text(props.read_text(encoding="utf-8") + "stale", encoding="utf-8")
        with self.assertRaises(nukehour_version.VersionError):
            nukehour_version.check(root)

    def test_bump_patch_advances_product_and_build_once(self):
        root = self.make_root(version="2.4.9", build=18)
        updated = nukehour_version.bump_patch(root)
        self.assertEqual("2.4.10", updated.version)
        self.assertEqual(19, updated.build)
        self.assertEqual(updated, nukehour_version.load_manifest(root))
        nukehour_version.check(root)

    def test_compatibility_hash_is_deterministic_and_ignores_ui(self):
        root = self.make_root()
        (root / "mods/ra2/chrome").mkdir()
        ui = root / "mods/ra2/chrome/mainmenu.yaml"
        ui.write_text("UI: one\n", encoding="utf-8")
        before = nukehour_version.compatibility_digest(root)
        ui.write_text("UI: two\n", encoding="utf-8")
        self.assertEqual(before, nukehour_version.compatibility_digest(root))
        (root / "mods/ra2/rules/core.yaml").write_text("Rule: 2\n", encoding="utf-8")
        self.assertNotEqual(before, nukehour_version.compatibility_digest(root))

    def test_compatibility_hash_can_exclude_platform_projection_fragments(self):
        root = self.make_root()
        inputs = root / "packaging/nukehour-compatibility-inputs.txt"
        inputs.write_text(inputs.read_text(encoding="utf-8") +
                          "!mods/ra2/rules/*-base.yaml\n", encoding="utf-8")
        projection = root / "mods/ra2/rules/world-base.yaml"
        projection.write_text("Projected: one\n", encoding="utf-8")
        before = nukehour_version.compatibility_digest(root)
        projection.write_text("Projected: two\n", encoding="utf-8")
        self.assertEqual(before, nukehour_version.compatibility_digest(root))

    def test_apply_projects_display_storage_and_compatibility_versions(self):
        root = self.make_root(platform="ios", version="1.0.7", build=7)
        digest = nukehour_version.compatibility_digest(root)
        nukehour_version.apply(root)
        mod = (root / "mods/ra2/mod.yaml").read_text(encoding="utf-8")
        self.assertIn("Version: nukehour-storage-v1", mod)
        self.assertIn("DisplayVersion: NUKE HOUR iOS 1.0.7 (Build 7)", mod)
        self.assertIn(f"Compatibility: nukehour-core-sha256-{digest}", mod)
        self.assertIn("~^SupportDir|maps/ra2/nukehour-storage-v1: User", mod)
        self.assertIn("~^SupportDir|maps/ra2/{DEV_VERSION}: User", mod)
        self.assertLess(
            mod.index("~^SupportDir|maps/ra2/nukehour-storage-v1: User"),
            mod.index("~^SupportDir|maps/ra2/{DEV_VERSION}: User"))
        content = (root / "mods/ra2-content/mod.yaml").read_text(encoding="utf-8")
        self.assertIn("Version: nukehour-storage-v1", content)

    def test_platform_build_is_gated_by_the_canonical_version(self):
        root = Path(__file__).parents[2]
        platform = nukehour_version.load_manifest(root).platform
        if platform == "macos":
            source = (root / "packaging/macos/buildpackage.sh").read_text(encoding="utf-8")
            self.assertIn('nukehour_version.py" check', source)
            self.assertIn("CANONICAL_VERSION", source)
            self.assertIn("CANONICAL_BUILD", source)
            self.assertIn("CFBundleVersion", source)
        elif platform == "ios":
            source = (root / "ios/OpenRA.iOS/OpenRA.iOS.csproj").read_text(encoding="utf-8")
            self.assertIn("nukehour-version.props", source)
            self.assertIn("NukeHourProductVersion", source)
            self.assertIn("NukeHourBuildNumber", source)
            self.assertIn("NukeHourVersionCheck", source)
        else:
            source = (root / "android/OpenRA.Android/OpenRA.Android.csproj").read_text(encoding="utf-8")
            self.assertIn("nukehour-version.props", source)
            self.assertIn("NukeHourProductVersion", source)
            self.assertIn("NukeHourBuildNumber", source)
            self.assertIn("NukeHourVersionCheck", source)


if __name__ == "__main__":
    unittest.main()
