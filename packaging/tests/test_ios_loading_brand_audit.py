import plistlib
import struct
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ElementTree
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "packaging" / "ios_loading_brand_audit.py"
PROJECT = ROOT / "ios" / "OpenRA.iOS" / "OpenRA.iOS.csproj"
EXPECTED_IMAGE = "\tImage: ra2|uibits/NUCLEAR-CRISIS-BG-06.png"
EXPECTED_INGAME_LAYOUT = (
    "ChromeLayout:\n"
    "\tra2|chrome/ingame-menu.yaml\n"
    "\tra2|chrome/ingame-info.yaml\n"
    "\tra2|chrome/gamesave-loading.yaml\n"
)


def png_with_dimensions(width, height):
    return (
        b"\x89PNG\r\n\x1a\n"
        + struct.pack(">I", 13)
        + b"IHDR"
        + struct.pack(">II", width, height)
        + b"\x08\x06\x00\x00\x00"
        + b"\x00\x00\x00\x00"
    )


class IosLoadingBrandAuditTest(unittest.TestCase):
    def setUp(self):
        self.assertTrue(SCRIPT.is_file(), f"Missing bundle audit tool: {SCRIPT}")
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary_directory.cleanup)
        root = Path(self.temporary_directory.name)
        self.app = root / "OpenRA.iOS.app"
        self.source_mod = root / "source" / "mods" / "ra2"

        (self.app / "mods" / "ra2" / "uibits").mkdir(parents=True)
        (self.app / "mods" / "ra2" / "chrome").mkdir(parents=True)
        (self.app / "mods" / "ra2" / "fluent" / "zh-CN").mkdir(parents=True)
        (self.source_mod / "uibits").mkdir(parents=True)
        (self.source_mod / "chrome").mkdir(parents=True)

        chrome = "menu-bg-9:\n\tImage: uibits/NUCLEAR-CRISIS-BG-06.png\n"
        (self.source_mod / "chrome.yaml").write_text(chrome, encoding="utf-8")
        (self.app / "mods" / "ra2" / "chrome.yaml").write_text(
            chrome, encoding="utf-8"
        )

        manifest = (
            f"{EXPECTED_INGAME_LAYOUT}"
            f"LoadScreen: BrandSplashLoadScreen\n{EXPECTED_IMAGE}\n",
        )
        manifest = "".join(manifest)
        (self.app / "mods" / "ra2" / "mod.yaml").write_text(
            manifest, encoding="utf-8"
        )
        (self.source_mod / "mod.yaml").write_text(manifest, encoding="utf-8")
        menu = "Container@INGAME_MENU:\n\tChildren:\n\t\tContainer@PANEL_ROOT:\n"
        info = "Container@GAME_INFO_PANEL:\n\tChildren:\n\t\tLabel@TITLE:\n"
        gamesave = (
            "Container@GAMESAVE_LOADING_SCREEN:\n"
            "\tChildren:\n"
            "\t\tLogicKeyListener@CANCEL_HANDLER:\n"
            "\t\tProgressBar@PROGRESS:\n"
        )
        (self.source_mod / "chrome" / "ingame-menu.yaml").write_text(
            menu, encoding="utf-8"
        )
        (self.source_mod / "chrome" / "ingame-info.yaml").write_text(
            info, encoding="utf-8"
        )
        (self.source_mod / "chrome" / "gamesave-loading.yaml").write_text(
            gamesave, encoding="utf-8"
        )
        (self.app / "mods" / "ra2" / "chrome" / "ingame-menu.yaml").write_text(
            menu, encoding="utf-8"
        )
        (self.app / "mods" / "ra2" / "chrome" / "ingame-info.yaml").write_text(
            info, encoding="utf-8"
        )
        (
            self.app / "mods" / "ra2" / "chrome" / "gamesave-loading.yaml"
        ).write_text(gamesave, encoding="utf-8")
        (self.app / "mods" / "ra2" / "fluent" / "mod.ftl").write_text(
            "mod-title = NUKE HOUR\n", encoding="utf-8"
        )
        (
            self.app / "mods" / "ra2" / "fluent" / "zh-CN" / "mod.ftl"
        ).write_text("mod-title = NUKE HOUR\n", encoding="utf-8")
        with (self.app / "Info.plist").open("wb") as stream:
            plistlib.dump(
                {
                    "CFBundleDisplayName": "NUKE HOUR",
                    "CFBundleName": "NUKE HOUR",
                },
                stream,
            )

        wallpaper = png_with_dimensions(2048, 1024)
        (self.source_mod / "uibits" / "NUCLEAR-CRISIS-BG-06.png").write_bytes(
            wallpaper
        )
        (
            self.app
            / "mods"
            / "ra2"
            / "uibits"
            / "NUCLEAR-CRISIS-BG-06.png"
        ).write_bytes(wallpaper)

    def run_tool(self, command):
        return subprocess.run(
            [
                "python3",
                str(SCRIPT),
                command,
                "--app-bundle",
                str(self.app),
                *(
                    ["--source-mod-root", str(self.source_mod)]
                    if command == "audit"
                    else []
                ),
            ],
            check=False,
            capture_output=True,
            text=True,
        )

    def test_prepare_removes_only_the_retired_ra2_brand_artwork(self):
        retired_names = (
            "loadscreen.png",
            "menulogo.png",
            "menulogo-sm.png",
            "menutitle.png",
            "menutitle-sm.png",
        )
        for name in retired_names:
            (self.app / "mods" / "ra2" / "uibits" / name).write_bytes(b"old")
        other_mod = self.app / "mods" / "ra" / "uibits" / "loadscreen.png"
        other_mod.parent.mkdir(parents=True)
        other_mod.write_bytes(b"valid-other-mod-artwork")

        result = self.run_tool("prepare")

        self.assertEqual(0, result.returncode, result.stderr)
        for name in retired_names:
            self.assertFalse((self.app / "mods" / "ra2" / "uibits" / name).exists())
        self.assertEqual(b"valid-other-mod-artwork", other_mod.read_bytes())

    def test_prepare_rejects_an_internal_directory_symlink_escape(self):
        uibits = self.app / "mods" / "ra2" / "uibits"
        (uibits / "NUCLEAR-CRISIS-BG-06.png").unlink()
        uibits.rmdir()
        outside = Path(self.temporary_directory.name) / "outside"
        outside.mkdir()
        outside_legacy = outside / "loadscreen.png"
        outside_legacy.write_bytes(b"must-survive")
        uibits.symlink_to(outside, target_is_directory=True)

        result = self.run_tool("prepare")

        self.assertNotEqual(0, result.returncode)
        self.assertTrue(outside_legacy.is_file())
        self.assertEqual(b"must-survive", outside_legacy.read_bytes())

    def test_audit_accepts_a_current_bundle(self):
        result = self.run_tool("audit")

        self.assertEqual(0, result.returncode, result.stderr)

    def test_audit_rejects_a_legacy_manifest_reference(self):
        (self.app / "mods" / "ra2" / "mod.yaml").write_text(
            f"{EXPECTED_INGAME_LAYOUT}"
            "LoadScreen: BrandSplashLoadScreen\n"
            "\tImage: ra2|uibits/loadscreen.png\n",
            encoding="utf-8",
        )

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("manifest", result.stderr.casefold())

    def test_audit_rejects_the_legacy_common_ingame_menu(self):
        manifest = self.app / "mods" / "ra2" / "mod.yaml"
        manifest.write_text(
            manifest.read_text(encoding="utf-8")
            .replace(
                "\tra2|chrome/ingame-menu.yaml",
                "\tcommon|chrome/ingame-menu.yaml",
            )
            .replace(
                "\tra2|chrome/ingame-info.yaml",
                "\tcommon|chrome/ingame-info.yaml",
            ),
            encoding="utf-8",
        )

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("in-game menu", result.stderr.casefold())

    def test_audit_uses_active_chrome_entries_instead_of_comments_or_tab_spelling(self):
        manifest = self.app / "mods" / "ra2" / "mod.yaml"
        manifest.write_text(
            manifest.read_text(encoding="utf-8")
            .replace(
                "\tra2|chrome/ingame-menu.yaml",
                "    common|chrome/ingame-menu.yaml",
            )
            + "# \tra2|chrome/ingame-menu.yaml\n",
            encoding="utf-8",
        )

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("in-game menu", result.stderr.casefold())

    def test_audit_ignores_nested_paths_that_are_not_active_chrome_entries(self):
        manifest = self.app / "mods" / "ra2" / "mod.yaml"
        manifest.write_text(
            "ChromeLayout:\n"
            "    common|chrome/ingame-menu.yaml:\n"
            "        ra2|chrome/ingame-menu.yaml\n"
            "        ra2|chrome/ingame-info.yaml\n"
            "        ra2|chrome/gamesave-loading.yaml\n"
            f"LoadScreen: BrandSplashLoadScreen\n{EXPECTED_IMAGE}\n",
            encoding="utf-8",
        )

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("in-game menu", result.stderr.casefold())

    def test_audit_rejects_min_yaml_list_removals_and_nested_replacements(self):
        manifest_text = (
            "ChromeLayout:\n"
            "\tra2|chrome/ingame-menu.yaml\n"
            "\tra2|chrome/ingame-info.yaml\n"
            "\tra2|chrome/gamesave-loading.yaml\n"
            "\t-ra2|chrome/ingame-menu.yaml\n"
            "\tcommon|chrome/ingame-menu.yaml:\n"
            f"LoadScreen: BrandSplashLoadScreen\n{EXPECTED_IMAGE}\n"
        )
        (self.app / "mods" / "ra2" / "mod.yaml").write_text(
            manifest_text, encoding="utf-8"
        )
        (self.source_mod / "mod.yaml").write_text(manifest_text, encoding="utf-8")

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("merge syntax", result.stderr.casefold())

    def test_audit_rejects_manifest_includes_that_can_override_chrome_layout(self):
        manifest_text = (
            f"{EXPECTED_INGAME_LAYOUT}"
            "Include: override.yaml\n"
            f"LoadScreen: BrandSplashLoadScreen\n{EXPECTED_IMAGE}\n"
        )
        (self.app / "mods" / "ra2" / "mod.yaml").write_text(
            manifest_text, encoding="utf-8"
        )
        (self.source_mod / "mod.yaml").write_text(manifest_text, encoding="utf-8")

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("include", result.stderr.casefold())

    def test_audit_rejects_duplicate_chrome_layout_sections(self):
        manifest_text = (
            f"{EXPECTED_INGAME_LAYOUT}"
            "ChromeLayout:\n"
            "\t-ra2|chrome/ingame-menu.yaml\n"
            "\tcommon|chrome/ingame-menu.yaml\n"
            f"LoadScreen: BrandSplashLoadScreen\n{EXPECTED_IMAGE}\n"
        )
        (self.app / "mods" / "ra2" / "mod.yaml").write_text(
            manifest_text, encoding="utf-8"
        )
        (self.source_mod / "mod.yaml").write_text(manifest_text, encoding="utf-8")

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("exactly one chromelayout", result.stderr.casefold())

    def test_audit_rejects_a_stale_bundled_ingame_menu(self):
        (self.app / "mods" / "ra2" / "chrome" / "ingame-menu.yaml").write_text(
            "Container@INGAME_MENU:\n\tImageCollection: logos\n",
            encoding="utf-8",
        )

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("canonical source", result.stderr.casefold())

    def test_audit_rejects_the_legacy_common_gamesave_loading_screen(self):
        manifest = self.app / "mods" / "ra2" / "mod.yaml"
        manifest.write_text(
            manifest.read_text(encoding="utf-8").replace(
                "\tra2|chrome/gamesave-loading.yaml",
                "\tcommon|chrome/gamesave-loading.yaml",
            ),
            encoding="utf-8",
        )

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("save-game loading", result.stderr.casefold())

    def test_audit_rejects_a_stale_bundled_gamesave_loading_screen(self):
        (
            self.app / "mods" / "ra2" / "chrome" / "gamesave-loading.yaml"
        ).write_text(
            "Container@GAMESAVE_LOADING_SCREEN:\n\tImageCollection: logos\n",
            encoding="utf-8",
        )

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("canonical source", result.stderr.casefold())

    def test_audit_rejects_a_matching_gamesave_layout_with_the_removed_logo(self):
        legacy_layout = (
            "Container@GAMESAVE_LOADING_SCREEN:\n\tImageCollection: logos\n"
        )
        (self.source_mod / "chrome" / "gamesave-loading.yaml").write_text(
            legacy_layout, encoding="utf-8"
        )
        (
            self.app / "mods" / "ra2" / "chrome" / "gamesave-loading.yaml"
        ).write_text(legacy_layout, encoding="utf-8")

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("removed legacy logos", result.stderr.casefold())

    def test_audit_rejects_removed_logo_with_valid_compact_yaml_spacing(self):
        legacy_layout = (
            "Container@GAMESAVE_LOADING_SCREEN:\n\tImageCollection:logos\n"
        )
        (self.source_mod / "chrome" / "gamesave-loading.yaml").write_text(
            legacy_layout, encoding="utf-8"
        )
        (
            self.app / "mods" / "ra2" / "chrome" / "gamesave-loading.yaml"
        ).write_text(legacy_layout, encoding="utf-8")

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("removed legacy logos", result.stderr.casefold())

    def test_audit_rejects_removed_logo_with_unicode_yaml_whitespace(self):
        legacy_layout = (
            "Container@GAMESAVE_LOADING_SCREEN:\n\tImageCollection\u00a0:logos\n"
        )
        (self.source_mod / "chrome" / "gamesave-loading.yaml").write_text(
            legacy_layout, encoding="utf-8"
        )
        (
            self.app / "mods" / "ra2" / "chrome" / "gamesave-loading.yaml"
        ).write_text(legacy_layout, encoding="utf-8")

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("removed legacy logos", result.stderr.casefold())

    def test_audit_rejects_a_retained_legacy_file(self):
        legacy = self.app / "mods" / "ra2" / "uibits" / "loadscreen.png"
        legacy.write_bytes(b"old")

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("legacy", result.stderr.casefold())

    def test_audit_rejects_a_retained_legacy_menu_logo(self):
        legacy = self.app / "mods" / "ra2" / "uibits" / "menulogo.png"
        legacy.write_bytes(b"old")

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("legacy", result.stderr.casefold())

    def test_audit_rejects_a_matching_chrome_catalog_with_legacy_logos(self):
        legacy_chrome = "logos:\n\tImage: uibits/menulogo.png\n"
        (self.source_mod / "chrome.yaml").write_text(
            legacy_chrome, encoding="utf-8"
        )
        (self.app / "mods" / "ra2" / "chrome.yaml").write_text(
            legacy_chrome, encoding="utf-8"
        )

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("legacy logos collection", result.stderr.casefold())

    def test_audit_rejects_legacy_logos_with_sublevel_yaml_indentation(self):
        legacy_chrome = " logos:\n    Image: uibits/menulogo.png\n"
        (self.source_mod / "chrome.yaml").write_text(
            legacy_chrome, encoding="utf-8"
        )
        (self.app / "mods" / "ra2" / "chrome.yaml").write_text(
            legacy_chrome, encoding="utf-8"
        )

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("legacy logos collection", result.stderr.casefold())

    def test_audit_rejects_a_wallpaper_that_differs_from_source(self):
        (
            self.app
            / "mods"
            / "ra2"
            / "uibits"
            / "NUCLEAR-CRISIS-BG-06.png"
        ).write_bytes(b"stale-wallpaper")

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("canonical", result.stderr.casefold())

    def test_audit_rejects_a_matching_wallpaper_with_wrong_dimensions(self):
        wallpaper = png_with_dimensions(1920, 1080)
        (self.source_mod / "uibits" / "NUCLEAR-CRISIS-BG-06.png").write_bytes(
            wallpaper
        )
        (
            self.app
            / "mods"
            / "ra2"
            / "uibits"
            / "NUCLEAR-CRISIS-BG-06.png"
        ).write_bytes(wallpaper)

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("2048x1024", result.stderr)

    def test_audit_rejects_old_brand_text_in_active_metadata(self):
        (self.app / "mods" / "ra2" / "fluent" / "mod.ftl").write_text(
            "mod-title = 阿富汗危机\n", encoding="utf-8"
        )

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("old brand", result.stderr.casefold())

    def test_audit_rejects_old_nuclear_crisis_or_openrts_visible_metadata(self):
        old_names = ("NUCLEAR" + " CRISIS", "OpenRTS" + " Mobile")
        for old_name in old_names:
            with self.subTest(old_name=old_name):
                path = self.app / "mods" / "ra2" / "fluent" / "mod.ftl"
                path.write_text(f"mod-title = {old_name}\n", encoding="utf-8")

                result = self.run_tool("audit")

                self.assertNotEqual(0, result.returncode)
                self.assertIn("old brand", result.stderr.casefold())

    def test_audit_only_exempts_exact_stable_wallpaper_tokens(self):
        metadata_path = self.app / "mods" / "ra2" / "fluent" / "mod.ftl"
        retired_tokens = (
            "NUCLEAR-CRISIS-BG-00.png",
            "NUCLEAR-CRISIS-BG-10.png",
            "NUCLEAR-CRISIS-BG-99.png",
            "XNUCLEAR-CRISIS-BG-06.pngY",
        )
        for retired_token in retired_tokens:
            with self.subTest(retired_token=retired_token):
                metadata_path.write_text(
                    f"mod-title = NUKE HOUR\nnote = {retired_token}\n",
                    encoding="utf-8",
                )

                result = self.run_tool("audit")

                self.assertNotEqual(0, result.returncode)
                self.assertIn("old brand", result.stderr.casefold())

    def test_audit_rejects_wrong_bundle_visible_name(self):
        with (self.app / "Info.plist").open("wb") as stream:
            plistlib.dump(
                {"CFBundleDisplayName": "Wrong Game", "CFBundleName": "NUKE HOUR"},
                stream,
            )

        result = self.run_tool("audit")

        self.assertNotEqual(0, result.returncode)
        self.assertIn("NUKE HOUR", result.stderr)

    def test_audit_rejects_wrong_english_or_chinese_mod_title(self):
        cases = (
            self.app / "mods" / "ra2" / "fluent" / "mod.ftl",
            self.app / "mods" / "ra2" / "fluent" / "zh-CN" / "mod.ftl",
        )
        for path in cases:
            with self.subTest(path=path):
                path.write_text("mod-title = WRONG\n", encoding="utf-8")

                result = self.run_tool("audit")

                self.assertNotEqual(0, result.returncode)
                self.assertIn("mod-title", result.stderr)
                path.write_text("mod-title = NUKE HOUR\n", encoding="utf-8")


class IosLoadingBrandBuildPolicyTest(unittest.TestCase):
    def test_personal_build_prepares_and_audits_the_app_bundle(self):
        project = PROJECT.read_text(encoding="utf-8")
        root = ElementTree.fromstring(project)
        prepare = root.find("./Target[@Name='PreparePersonalIosLoadingBrandBundle']")
        audit = root.find("./Target[@Name='AuditPersonalIosLoadingBrandBundle']")

        self.assertIsNotNone(prepare)
        self.assertIsNotNone(audit)
        self.assertEqual("_CreateAppBundle", prepare.get("BeforeTargets"))
        self.assertEqual("_GenerateBundleName", prepare.get("DependsOnTargets"))
        self.assertEqual("'$(PublicClean)' != 'true'", prepare.get("Condition"))
        self.assertEqual("_CreateAppBundle", audit.get("AfterTargets"))
        self.assertEqual("'$(PublicClean)' != 'true'", audit.get("Condition"))
        prepare_command = prepare.find("./Exec").get("Command")
        audit_command = audit.find("./Exec").get("Command")
        self.assertIn("ios_loading_brand_audit.py", prepare_command)
        self.assertIn(" prepare --app-bundle ", prepare_command)
        self.assertIn("ios_loading_brand_audit.py", audit_command)
        self.assertIn(" audit --app-bundle ", audit_command)
        self.assertIn("--source-mod-root", audit_command)


if __name__ == "__main__":
    unittest.main()
