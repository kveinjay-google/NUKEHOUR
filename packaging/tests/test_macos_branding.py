import hashlib
import plistlib
import re
import subprocess
import unittest
from pathlib import Path

from PIL import Image

import launcher


ROOT = Path(__file__).resolve().parents[2]
LAUNCHER_APP = ROOT / "NUKE HOUR.app"
GAME_APP = ROOT / "NUKE HOUR GAME.app"
OLD_NUCLEAR_LAUNCHER_APP = ROOT / "NUCLEAR CRISIS.app"
OLD_NUCLEAR_GAME_APP = ROOT / "NUCLEAR CRISIS GAME.app"
OLD_LAUNCHER_APP = ROOT / "Red Alert 2：阿富汗危机.app"
OLD_GAME_APP = ROOT / "Red Alert 2：阿富汗危机 - 游戏.app"
LAUNCH_COMMAND = ROOT / "NUKE HOUR.command"
OLD_NUCLEAR_LAUNCH_COMMAND = ROOT / "NUCLEAR CRISIS.command"
OLD_LAUNCH_COMMAND = ROOT / "Red Alert 2：阿富汗危机.command"
CANONICAL_ICON = (
    ROOT
    / "branding"
    / "NUKE-HOUR-1024.png"
)

OLD_VISIBLE_BRAND = re.compile(
    r"阿富汗|afghan(?:istan)?",
    re.IGNORECASE,
)

EXPECTED_WALLPAPER_NAMES = [
    f"NUCLEAR-CRISIS-BG-{index:02d}.png" for index in range(1, 10)
]
EXPECTED_WALLPAPERS = [
    ROOT / "mods" / "ra2" / "uibits" / name
    for name in EXPECTED_WALLPAPER_NAMES
]


class MacOSBrandingTest(unittest.TestCase):
    def assert_no_old_visible_brand(self, value, location):
        match = OLD_VISIBLE_BRAND.search(value)
        if match:
            self.fail(f"old visible brand {match.group()!r} remains in {location}")

    def rgba_1024_digest(self, path, expected_format):
        with Image.open(path) as image:
            self.assertEqual(expected_format, image.format, str(path))
            if expected_format == "ICNS":
                physical_sizes = {
                    (width * scale, height * scale)
                    for width, height, scale in image.info.get("sizes", ())
                }
                self.assertIn((1024, 1024), physical_sizes, str(path))
                image.size = (1024, 1024)
            self.assertEqual((1024, 1024), image.size, str(path))
            rgba = image.convert("RGBA")
            rgba.load()

        return hashlib.sha256(rgba.tobytes()).hexdigest()

    def sips_dimensions(self, path):
        result = subprocess.run(
            ["sips", "-g", "pixelWidth", "-g", "pixelHeight", str(path)],
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertEqual(0, result.returncode, result.stderr)
        property_pattern = re.compile(
            r"^\s*(pixelWidth|pixelHeight):\s*(\d+)\s*$",
            re.MULTILINE,
        )
        properties = property_pattern.findall(result.stdout)
        self.assertEqual(2, len(properties), result.stdout)
        return {name: int(value) for name, value in properties}

    def test_only_nuke_hour_app_bundle_names_are_installed(self):
        for app in (LAUNCHER_APP, GAME_APP):
            with self.subTest(app=app.name):
                self.assertTrue(app.is_dir(), f"missing app bundle: {app}")
                self.assertFalse(
                    (app / "Contents" / "_CodeSignature").exists(),
                    f"stale signature was copied into renamed bundle: {app}",
                )

        for app in (
            OLD_NUCLEAR_LAUNCHER_APP,
            OLD_NUCLEAR_GAME_APP,
            OLD_LAUNCHER_APP,
            OLD_GAME_APP,
        ):
            with self.subTest(app=app.name):
                self.assertFalse(app.exists(), f"old app bundle still exists: {app}")

        self.assertTrue(LAUNCH_COMMAND.is_file(), f"missing launcher: {LAUNCH_COMMAND}")
        self.assertTrue(
            LAUNCH_COMMAND.stat().st_mode & 0o111,
            f"launcher is not executable: {LAUNCH_COMMAND}",
        )
        self.assertFalse(
            OLD_LAUNCH_COMMAND.exists(),
            f"old launcher still exists: {OLD_LAUNCH_COMMAND}",
        )
        self.assertFalse(
            OLD_NUCLEAR_LAUNCH_COMMAND.exists(),
            f"old launcher still exists: {OLD_NUCLEAR_LAUNCH_COMMAND}",
        )

    def test_bundle_plists_use_the_corresponding_uppercase_brand(self):
        cases = (
            (
                LAUNCHER_APP,
                {
                    "CFBundleDisplayName": "NUKE HOUR",
                    "CFBundleName": "NUKE HOUR",
                    "CFBundleExecutable": "ra2launcher",
                    "CFBundleIconFile": "NUKE-HOUR.icns",
                    "CFBundleIdentifier": "com.openra.ra2mac.launcher",
                },
            ),
            (
                GAME_APP,
                {
                    "CFBundleDisplayName": "NUKE HOUR",
                    "CFBundleName": "NUKE HOUR",
                    "CFBundleExecutable": "NuclearCrisis",
                    "CFBundleIconFile": "NUKE-HOUR.icns",
                    "CFBundleIdentifier": "com.ra2mac.afghanistan-crisis.game",
                },
            ),
        )

        for app, expected in cases:
            with self.subTest(app=app.name):
                plist_path = app / "Contents" / "Info.plist"
                self.assertTrue(plist_path.is_file(), f"missing plist: {plist_path}")
                with plist_path.open("rb") as plist_file:
                    plist = plistlib.load(plist_file)
                for key, value in expected.items():
                    self.assertEqual(value, plist.get(key), f"{plist_path}: {key}")
                for key, value in plist.items():
                    if key == "CFBundleIdentifier" or not isinstance(value, str):
                        continue
                    self.assert_no_old_visible_brand(value, f"{plist_path}: {key}")

    def test_game_bundle_contains_the_nuclear_crisis_host(self):
        host = GAME_APP / "Contents" / "MacOS" / "NuclearCrisis"

        self.assertTrue(host.is_file(), f"missing game host: {host}")
        self.assertTrue(host.stat().st_mode & 0o111, f"game host is not executable: {host}")

    def test_game_bundle_drops_the_old_host_and_icon_files(self):
        legacy_files = (
            GAME_APP / "Contents" / "MacOS" / "AfghanistanCrisis",
            GAME_APP / "Contents" / "Resources" / "AfghanistanCrisis.icns",
            GAME_APP / "Contents" / "Resources" / "NUCLEAR-CRISIS.icns",
            LAUNCHER_APP / "Contents" / "Resources" / "icon.icns",
        )

        for path in legacy_files:
            with self.subTest(path=str(path.relative_to(ROOT))):
                self.assertFalse(path.exists(), f"legacy bundle file still exists: {path}")

    def test_shell_entry_points_use_bilingual_dependency_errors(self):
        entry_points = (
            LAUNCH_COMMAND,
            LAUNCHER_APP / "Contents" / "MacOS" / "ra2launcher",
        )
        for path in entry_points:
            with self.subTest(path=str(path.relative_to(ROOT))):
                source = path.read_text(encoding="utf-8")
                self.assertIn("Homebrew Python Tk is required", source)
                self.assertIn("需要安装 Homebrew Python Tk", source)
                self.assertIn("NUKE HOUR Launcher / NUKE HOUR 启动器", source)

    def test_launcher_exposes_the_brand_and_exact_wallpaper_set(self):
        self.assertEqual("NUKE HOUR", getattr(launcher, "BRAND_NAME", None))

        wallpaper_paths = getattr(launcher, "brand_wallpaper_paths", None)
        self.assertTrue(callable(wallpaper_paths), "brand_wallpaper_paths must be defined")
        self.assertEqual(EXPECTED_WALLPAPERS, [Path(path) for path in wallpaper_paths()])

        actual_names = sorted(
            path.name
            for path in (ROOT / "mods" / "ra2" / "uibits").glob(
                "NUCLEAR-CRISIS-BG-*.png"
            )
        )
        self.assertEqual(EXPECTED_WALLPAPER_NAMES, actual_names)
        for wallpaper in EXPECTED_WALLPAPERS:
            with self.subTest(wallpaper=wallpaper.name):
                self.assertTrue(wallpaper.is_file(), f"missing wallpaper: {wallpaper}")

    def test_launcher_uses_the_canonical_nuke_hour_master_icon(self):
        expected = ROOT / "branding" / "NUKE-HOUR-1024.png"
        self.assertEqual(expected, Path(getattr(launcher, "BRAND_ICON", "")))

        source = (ROOT / "launcher.py").read_text(encoding="utf-8")
        self.assertIn('REPO, "branding", "NUKE-HOUR-1024.png"', source)
        self.assertNotIn("NUCLEAR-CRISIS-1024.png", source)
        self.assertNotIn("AppIcon.appiconset", source)

    def test_wallpaper_choice_accepts_an_injectable_selector(self):
        choose_wallpaper = getattr(launcher, "choose_brand_wallpaper", None)
        self.assertTrue(callable(choose_wallpaper), "choose_brand_wallpaper must be defined")
        selector_input = []

        def select_last(paths):
            selector_input.extend(Path(path) for path in paths)
            return paths[-1]

        selected = Path(choose_wallpaper(select_last))

        self.assertEqual(EXPECTED_WALLPAPERS, selector_input)
        self.assertEqual(EXPECTED_WALLPAPERS[-1], selected)

    def test_wallpaper_choice_without_selector_returns_a_canonical_wallpaper(self):
        choose_wallpaper = getattr(launcher, "choose_brand_wallpaper", None)
        self.assertTrue(callable(choose_wallpaper), "choose_brand_wallpaper must be defined")

        selected = Path(choose_wallpaper())

        self.assertIn(selected, EXPECTED_WALLPAPERS)

    def test_all_macos_icons_match_the_canonical_nuke_hour_master_and_each_other(self):
        icons = (
            ROOT / "launcher_assets" / "icon.icns",
            LAUNCHER_APP / "Contents" / "Resources" / "NUKE-HOUR.icns",
            GAME_APP / "Contents" / "Resources" / "NUKE-HOUR.icns",
        )
        self.assertTrue(CANONICAL_ICON.is_file(), f"missing canonical icon: {CANONICAL_ICON}")
        self.assertTrue(icons[0].is_file(), f"missing launcher icon: {icons[0]}")
        canonical_digest = self.rgba_1024_digest(CANONICAL_ICON, "PNG")
        reference_icns_digest = hashlib.sha256(icons[0].read_bytes()).hexdigest()

        for icon in icons:
            with self.subTest(icon=str(icon.relative_to(ROOT))):
                self.assertTrue(icon.is_file(), f"missing icon: {icon}")
                self.assertEqual(
                    {"pixelWidth": 1024, "pixelHeight": 1024},
                    self.sips_dimensions(icon),
                    str(icon),
                )
                self.assertEqual(
                    canonical_digest,
                    self.rgba_1024_digest(icon, "ICNS"),
                    f"1024px frame does not match canonical iOS icon: {icon}",
                )
                self.assertEqual(
                    reference_icns_digest,
                    hashlib.sha256(icon.read_bytes()).hexdigest(),
                    f"ICNS bytes differ from launcher_assets/icon.icns: {icon}",
                )

    def test_mod_selector_icon_is_a_lanczos_reduction_of_the_canonical_icon(self):
        mod_icon = ROOT / "mods" / "ra2" / "icon.png"
        with Image.open(CANONICAL_ICON) as canonical:
            expected = canonical.convert("RGBA").resize(
                (32, 32),
                Image.Resampling.LANCZOS,
            )
            expected.load()
        with Image.open(mod_icon) as actual:
            self.assertEqual("PNG", actual.format)
            self.assertEqual((32, 32), actual.size)
            actual_rgba = actual.convert("RGBA")
            actual_rgba.load()

        self.assertEqual(expected.tobytes(), actual_rgba.tobytes())

    def test_active_launcher_sources_drop_old_visible_branding(self):
        active_files = (
            ROOT / "launcher.py",
            ROOT / "launcher_i18n.py",
        )

        for path in active_files:
            with self.subTest(path=str(path.relative_to(ROOT))):
                self.assertTrue(path.is_file(), f"missing active branding file: {path}")
                source = path.read_text(encoding="utf-8")
                self.assert_no_old_visible_brand(source, path)
                self.assertNotIn("NUCLEAR CRISIS wallpaper", source)

    def test_macos_launch_paths_give_the_canonical_mod_last_precedence(self):
        canonical_mods = ROOT / "mods"
        expected = f"./mods,{canonical_mods}"

        self.assertEqual(expected, getattr(launcher, "MAC_MOD_SEARCH_PATHS", None))

        launch_script = (ROOT / "launch-game.sh").read_text(encoding="utf-8")
        utility_script = (ROOT / "utility.sh").read_text(encoding="utf-8")
        self.assertIn(
            'MOD_SEARCH_PATHS="./mods,${TEMPLATE_ROOT}/mods"',
            launch_script,
        )
        self.assertNotIn(
            'MOD_SEARCH_PATHS="${TEMPLATE_ROOT}/mods,./mods"',
            launch_script,
        )
        self.assertIn(
            'MOD_SEARCH_PATHS="./mods,${TEMPLATE_ROOT}/mods"',
            utility_script,
        )
        self.assertNotIn(
            'MOD_SEARCH_PATHS="${TEMPLATE_ROOT}/mods,./mods"',
            utility_script,
        )

    def test_packaging_metadata_uses_the_nuke_hour_visible_identity(self):
        mod_config = (ROOT / "mod.config").read_text(encoding="utf-8")
        macos_packager = (
            ROOT / "packaging" / "macos" / "buildpackage.sh"
        ).read_text(encoding="utf-8")

        expected_values = {
            "PACKAGING_INSTALLER_NAME": "NUKE-HOUR",
            "PACKAGING_DISPLAY_NAME": "NUKE HOUR",
            "PACKAGING_MACOS_DISPLAY_NAME": "NUKE HOUR",
            "PACKAGING_MACOS_INSTALLER_NAME": "NUKE-HOUR",
            "PACKAGING_WINDOWS_LAUNCHER_NAME": "NUKE-HOUR",
            "PACKAGING_WINDOWS_INSTALL_DIR_NAME": "NUKE HOUR",
            "PACKAGING_WINDOWS_REGISTRY_KEY": "OpenRARA2Mod",
        }
        for key, expected in expected_values.items():
            with self.subTest(key=key):
                matches = re.findall(
                    rf'^{re.escape(key)}="([^"]*)"$',
                    mod_config,
                    re.MULTILINE,
                )
                self.assertEqual([expected], matches)
        self.assertIn(
            'PACKAGING_OSX_APP_NAME="${PACKAGING_MACOS_DISPLAY_NAME}.app"',
            macos_packager,
        )
        self.assertNotIn('PACKAGING_OSX_APP_NAME="OpenRA - ', macos_packager)
        self.assertIn(
            '${TEMPLATE_ROOT}/launcher_assets/icon.icns',
            macos_packager,
        )
        self.assertIn(
            '${OUTPUTDIR}/${PACKAGING_MACOS_INSTALLER_NAME}-${TAG}.dmg',
            macos_packager,
        )

    def test_shared_ra2_load_screen_uses_the_canonical_brand_wallpaper(self):
        mod_yaml = (ROOT / "mods" / "ra2" / "mod.yaml").read_text(
            encoding="utf-8"
        )
        load_screen = (
            ROOT
            / "OpenRA.Mods.RA2"
            / "LoadScreens"
            / "BrandSplashLoadScreen.cs"
        ).read_text(encoding="utf-8")

        self.assertIn("LoadScreen: BrandSplashLoadScreen", mod_yaml)
        self.assertIn(
            "\tImage: ra2|uibits/NUCLEAR-CRISIS-BG-06.png",
            mod_yaml,
        )
        self.assertNotIn("uibits/loadscreen.png", mod_yaml)
        self.assertFalse(
            (ROOT / "mods" / "ra2" / "uibits" / "loadscreen.png").exists()
        )

        self.assertIn(
            "new Rectangle(0, 0, s.Size.Width, s.Size.Height)",
            load_screen,
        )
        self.assertIn(
            "StretchBackgroundWidget.CalculateAspectFillCrop",
            load_screen,
        )
        self.assertNotIn("1920, 1080", load_screen)
        self.assertIn('const string BrandTitle = "NUKE HOUR";', load_screen)
        self.assertIn('r.Fonts["Title"]', load_screen)
        self.assertIn("DrawTextWithShadow", load_screen)
        self.assertIn("Color.FromArgb(246, 214, 121)", load_screen)
        self.assertIn('const string Loading = "loadscreen-loading";', load_screen)
        self.assertIn("messages.Random(Game.CosmeticRandom)", load_screen)
        self.assertIn(
            "r.Resolution.Width - textSize.X - 20",
            load_screen,
        )
        self.assertIn(
            "r.Resolution.Height - textSize.Y - 20",
            load_screen,
        )


if __name__ == "__main__":
    unittest.main()
