import plistlib
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
APP_BUNDLE = (
    ROOT
    / "ios"
    / "OpenRA.iOS"
    / "bin"
    / "Release"
    / "net8.0-ios"
    / "ios-arm64"
    / "OpenRA.iOS.app"
)
SOURCE_WALLPAPER = (
    ROOT / "mods" / "ra2" / "uibits" / "NUCLEAR-CRISIS-BG-06.png"
)
BUNDLED_WALLPAPER = (
    APP_BUNDLE / "mods" / "ra2" / "uibits" / "NUCLEAR-CRISIS-BG-06.png"
)
LEGACY_LOADING_SCREEN = (
    APP_BUNDLE / "mods" / "ra2" / "uibits" / "loadscreen.png"
)
ACTIVE_BRAND_METADATA = (
    APP_BUNDLE / "Info.plist",
    APP_BUNDLE / "mods" / "ra2" / "mod.yaml",
    APP_BUNDLE / "mods" / "ra2" / "fluent" / "mod.ftl",
    APP_BUNDLE / "mods" / "ra2" / "fluent" / "zh-CN" / "mod.ftl",
)


@unittest.skipUnless(
    APP_BUNDLE.is_dir(),
    f"Requires a personal ios-arm64 Release app: {APP_BUNDLE}",
)
class IosLoadingBrandArtifactTest(unittest.TestCase):
    def setUp(self):
        self.assertTrue(
            APP_BUNDLE.is_dir(),
            f"Build the personal ios-arm64 Release app first: {APP_BUNDLE}",
        )

    def test_release_manifest_references_the_nuclear_crisis_wallpaper(self):
        manifest = (APP_BUNDLE / "mods" / "ra2" / "mod.yaml").read_text(
            encoding="utf-8"
        )
        self.assertIn(
            "\tImage: ra2|uibits/NUCLEAR-CRISIS-BG-06.png",
            manifest,
        )
        self.assertNotIn("uibits/loadscreen.png", manifest)

    def test_release_bundle_does_not_retain_the_legacy_loading_screen(self):
        self.assertFalse(
            LEGACY_LOADING_SCREEN.exists(),
            f"Stale loading artwork survived incremental bundling: {LEGACY_LOADING_SCREEN}",
        )

    def test_release_wallpaper_matches_the_canonical_source(self):
        self.assertTrue(BUNDLED_WALLPAPER.is_file())
        self.assertEqual(SOURCE_WALLPAPER.read_bytes(), BUNDLED_WALLPAPER.read_bytes())

    def test_active_release_brand_metadata_has_no_afghan_crisis_name(self):
        for path in ACTIVE_BRAND_METADATA:
            with self.subTest(path=path.relative_to(APP_BUNDLE)):
                self.assertTrue(path.is_file())
                if path.suffix == ".plist":
                    with path.open("rb") as stream:
                        text = repr(plistlib.load(stream))
                else:
                    text = path.read_text(encoding="utf-8")

                lowered = text.casefold()
                self.assertNotIn("afghan", lowered)
                self.assertNotIn("阿富汗", text)


if __name__ == "__main__":
    unittest.main()
