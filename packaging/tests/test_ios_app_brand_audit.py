import hashlib
import io
import json
import plistlib
import shutil
import tempfile
import unittest
from contextlib import redirect_stderr
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "packaging" / "ios_app_brand_audit.py"
IOS_ROOT = ROOT / "ios" / "OpenRA.iOS"
ASSET_CATALOG = IOS_ROOT / "Assets.xcassets"
ENGLISH_PERMISSION = (
    "Used to discover and connect to multiplayer games on the same local "
    "network, personal hotspot, or nearby devices."
)
CHINESE_PERMISSION = "用于发现并连接同一局域网、个人热点或附近设备上的多人游戏。"


def file_inventory(root: Path) -> dict[str, str]:
    return {
        path.relative_to(root).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
        for path in sorted(root.rglob("*"))
        if path.is_file() and not path.is_symlink()
    }


class IosAppBrandAuditTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if not SCRIPT.is_file():
            cls.baseline_directory = None
            cls.baseline = None
            return

        from packaging import ios_app_brand_audit

        cls.audit = ios_app_brand_audit
        cls.baseline_directory = tempfile.TemporaryDirectory()
        cls.addClassCleanup(cls.baseline_directory.cleanup)
        cls.baseline = Path(cls.baseline_directory.name) / "NUKE HOUR.app"
        cls.baseline.mkdir()
        (cls.baseline / "Assets.car").write_bytes(b"controlled compiled catalog")

        contents = json.loads(
            (ASSET_CATALOG / "AppIcon.appiconset" / "Contents.json").read_text(
                encoding="utf-8"
            )
        )
        idioms = {"iphone": "phone", "ipad": "pad", "ios-marketing": "marketing"}
        cls.asset_metadata = []
        for image in contents["images"]:
            filename = image["filename"]
            source = ASSET_CATALOG / "AppIcon.appiconset" / filename
            with Image.open(source) as icon:
                width, height = icon.size
            cls.asset_metadata.append(
                {
                    "AssetType": "Icon Image",
                    "Name": "AppIcon",
                    "Idiom": idioms[image["idiom"]],
                    "Scale": int(image["scale"].removesuffix("x")),
                    "PixelWidth": width,
                    "PixelHeight": height,
                    "RenditionName": filename,
                    "Opaque": True,
                    "ColorModel": "RGB",
                    "Colorspace": "srgb",
                    "SHA1Digest": "A" * 64,
                }
            )

        for source_name, output_name in (
            ("NUKE-HOUR-60@2x.png", "AppIcon60x60@2x.png"),
            ("NUKE-HOUR-76@2x.png", "AppIcon76x76@2x~ipad.png"),
        ):
            shutil.copy2(
                ASSET_CATALOG / "AppIcon.appiconset" / source_name,
                cls.baseline / output_name,
            )

        plist = {
            "CFBundleDisplayName": "NUKE HOUR",
            "CFBundleName": "NUKE HOUR",
            "CFBundleIdentifier": "com.openra.ipad.personal",
            "CFBundleDevelopmentRegion": "en",
            "CFBundleLocalizations": ["en", "zh-Hans"],
            "NSLocalNetworkUsageDescription": CHINESE_PERMISSION,
            "NSBonjourServices": ["_openra-ra2._tcp"],
            "CFBundleIcons": {
                "CFBundlePrimaryIcon": {
                    "CFBundleIconFiles": ["AppIcon60x60"],
                    "CFBundleIconName": "AppIcon",
                }
            },
            "CFBundleIcons~ipad": {
                "CFBundlePrimaryIcon": {
                    "CFBundleIconFiles": ["AppIcon60x60", "AppIcon76x76"],
                    "CFBundleIconName": "AppIcon",
                }
            },
        }
        with (cls.baseline / "Info.plist").open("wb") as stream:
            plistlib.dump(plist, stream, fmt=plistlib.FMT_BINARY)

        for localization in ("en.lproj", "zh-Hans.lproj"):
            target = cls.baseline / localization
            target.mkdir()
            shutil.copy2(
                IOS_ROOT / localization / "InfoPlist.strings",
                target / "InfoPlist.strings",
            )

    def setUp(self):
        if self.baseline is None:
            if self._testMethodName != "test_brand_audit_cli_exists":
                self.skipTest(f"Missing iOS app brand audit: {SCRIPT}")
            return

        self.temporary_directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary_directory.cleanup)
        self.app = Path(self.temporary_directory.name) / "NUKE HOUR.app"
        shutil.copytree(self.baseline, self.app)

    def test_brand_audit_cli_exists(self):
        self.assertTrue(SCRIPT.is_file(), f"Missing iOS app brand audit: {SCRIPT}")

    def run_tool(self, asset_error=None, source_root=ROOT):
        stderr = io.StringIO()
        asset_result = asset_error or self.asset_metadata
        side_effect = asset_result if isinstance(asset_result, Exception) else None
        return_value = None if side_effect else asset_result
        with mock.patch.object(
            self.audit,
            "_assetutil_info",
            return_value=return_value,
            side_effect=side_effect,
        ), redirect_stderr(stderr):
            returncode = self.audit.main(
                [str(self.app), "--source-root", str(source_root)]
            )
        return SimpleNamespace(returncode=returncode, stderr=stderr.getvalue())

    def read_plist(self):
        with (self.app / "Info.plist").open("rb") as stream:
            return plistlib.load(stream)

    def write_plist(self, plist):
        with (self.app / "Info.plist").open("wb") as stream:
            plistlib.dump(plist, stream, fmt=plistlib.FMT_BINARY)

    def test_accepts_current_bundle_without_mutating_it(self):
        before = file_inventory(self.app)

        result = self.run_tool()

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(before, file_inventory(self.app))

    def test_rejects_wrong_visible_name_or_stable_bundle_identifier(self):
        for key, value in (
            ("CFBundleDisplayName", "NUCLEAR CRISIS"),
            ("CFBundleName", "OpenRTS Mobile"),
            ("CFBundleIdentifier", "com.example.changed"),
        ):
            with self.subTest(key=key):
                plist = self.read_plist()
                original = plist[key]
                plist[key] = value
                self.write_plist(plist)

                result = self.run_tool()

                self.assertNotEqual(0, result.returncode)
                self.assertIn(key.casefold(), result.stderr.casefold())
                plist[key] = original
                self.write_plist(plist)

    def test_rejects_wrong_development_region_or_localization_order(self):
        for key, value in (
            ("CFBundleDevelopmentRegion", "zh-Hans"),
            ("CFBundleLocalizations", ["zh-Hans", "en"]),
        ):
            with self.subTest(key=key):
                plist = self.read_plist()
                original = plist[key]
                plist[key] = value
                self.write_plist(plist)

                result = self.run_tool()

                plist[key] = original
                self.write_plist(plist)
                self.assertNotEqual(0, result.returncode)
                self.assertIn(key.casefold(), result.stderr.casefold())

    def test_rejects_missing_or_tampered_phone_and_tablet_icon_plist(self):
        for key, replacement in (
            ("CFBundleIcons", None),
            (
                "CFBundleIcons",
                {
                    "CFBundlePrimaryIcon": {
                        "CFBundleIconFiles": ["WrongIcon60x60"],
                        "CFBundleIconName": "AppIcon",
                    }
                },
            ),
            ("CFBundleIcons~ipad", None),
            (
                "CFBundleIcons~ipad",
                {
                    "CFBundlePrimaryIcon": {
                        "CFBundleIconFiles": ["AppIcon60x60", "AppIcon76x76"],
                        "CFBundleIconName": "WrongIcon",
                    }
                },
            ),
        ):
            with self.subTest(key=key, replacement=replacement):
                plist = self.read_plist()
                original = plist[key]
                if replacement is None:
                    del plist[key]
                else:
                    plist[key] = replacement
                self.write_plist(plist)

                result = self.run_tool()

                plist[key] = original
                self.write_plist(plist)
                self.assertNotEqual(0, result.returncode)
                self.assertIn(key.casefold(), result.stderr.casefold())

    def test_rejects_missing_or_extra_localization_directory(self):
        shutil.rmtree(self.app / "zh-Hans.lproj")
        result = self.run_tool()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("zh-hans.lproj", result.stderr.casefold())

        shutil.copytree(self.baseline / "zh-Hans.lproj", self.app / "zh-Hans.lproj")
        extra = self.app / "fr.lproj"
        extra.mkdir()
        (extra / "InfoPlist.strings").write_text("unexpected", encoding="utf-8")
        result = self.run_tool()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("fr.lproj", result.stderr.casefold())

    def test_rejects_incorrect_english_or_chinese_permission_text(self):
        for localization in ("en.lproj", "zh-Hans.lproj"):
            with self.subTest(localization=localization):
                target = self.app / localization / "InfoPlist.strings"
                original = target.read_bytes()
                target.write_text(
                    '"NSLocalNetworkUsageDescription" = "wrong";\n',
                    encoding="utf-8",
                )

                result = self.run_tool()

                self.assertNotEqual(0, result.returncode)
                self.assertIn(localization.casefold(), result.stderr.casefold())
                target.write_bytes(original)

    def test_rejects_extra_file_or_subdirectory_inside_localization(self):
        for relative_path, is_directory in (
            ("en.lproj/unexpected.txt", False),
            ("zh-Hans.lproj/nested", True),
        ):
            with self.subTest(relative_path=relative_path):
                target = self.app / relative_path
                if is_directory:
                    target.mkdir()
                else:
                    target.write_text("unexpected", encoding="utf-8")

                result = self.run_tool()

                if is_directory:
                    target.rmdir()
                else:
                    target.unlink()
                self.assertNotEqual(0, result.returncode)
                self.assertIn(relative_path.casefold(), result.stderr.casefold())

    def test_rejects_stale_or_corrupt_compiled_asset_catalog(self):
        (self.app / "Assets.car").write_bytes(b"stale catalog bytes")

        result = self.run_tool(ValueError("Assets.car is corrupt"))

        self.assertNotEqual(0, result.returncode)
        self.assertIn("assets.car", result.stderr.casefold())

    def test_rejects_unexpected_compiled_icon_catalog_name(self):
        metadata = [dict(entry) for entry in self.asset_metadata]
        metadata[0]["Name"] = "LegacyAppIcon"

        result = self.run_tool(metadata)

        self.assertNotEqual(0, result.returncode)
        self.assertIn("catalog", result.stderr.casefold())

    def test_rejects_unreferenced_public_clean_source_icon(self):
        source_root = Path(self.temporary_directory.name) / "source"
        source_ios = source_root / "ios" / "OpenRA.iOS"
        shutil.copytree(IOS_ROOT / "Assets.xcassets", source_ios / "Assets.xcassets")
        for localization in ("en.lproj", "zh-Hans.lproj"):
            shutil.copytree(IOS_ROOT / localization, source_ios / localization)
        extra = (
            source_ios
            / "Assets.xcassets"
            / "PublicAppIcon.appiconset"
            / "LEGACY-UNREFERENCED.png"
        )
        Image.new("RGB", (20, 20), (255, 0, 255)).save(extra)

        result = self.run_tool(source_root=source_root)

        self.assertNotEqual(0, result.returncode)
        self.assertIn("publicclean", result.stderr.casefold())
        self.assertIn(extra.name.casefold(), result.stderr.casefold())

    def test_rejects_compiled_launcher_icon_pixels_that_do_not_match_source(self):
        Image.new("RGB", (120, 120), (255, 0, 255)).save(
            self.app / "AppIcon60x60@2x.png"
        )

        result = self.run_tool()

        self.assertNotEqual(0, result.returncode)
        self.assertIn("appicon60x60@2x.png", result.stderr.casefold())

    def test_rejects_compiled_launcher_icon_with_alpha_even_when_rgb_matches(self):
        source = ASSET_CATALOG / "AppIcon.appiconset" / "NUKE-HOUR-60@2x.png"
        with Image.open(source) as icon:
            icon.convert("RGBA").save(self.app / "AppIcon60x60@2x.png")

        result = self.run_tool()

        self.assertNotEqual(0, result.returncode)
        self.assertIn("rgb", result.stderr.casefold())

    def test_accepts_device_cgbi_icons_via_read_only_system_conversion(self):
        cgbi_header = (
            b"\x89PNG\r\n\x1a\n"
            b"\x00\x00\x00\x04CgBI"
            b"\x50\x00\x20\x06"
            b"\x00\x00\x00\x00"
        )
        for name in ("AppIcon60x60@2x.png", "AppIcon76x76@2x~ipad.png"):
            (self.app / name).write_bytes(cgbi_header)
        before = file_inventory(self.app)

        def convert(command, **kwargs):
            self.assertEqual("/usr/bin/sips", command[0])
            source_name = Path(command[4]).name
            source_icon = (
                "NUKE-HOUR-76@2x.png"
                if source_name == "AppIcon76x76@2x~ipad.png"
                else "NUKE-HOUR-60@2x.png"
            )
            shutil.copy2(
                ASSET_CATALOG / "AppIcon.appiconset" / source_icon,
                Path(command[6]),
            )
            return SimpleNamespace(returncode=0, stdout="", stderr="")

        with mock.patch.object(self.audit.subprocess, "run", side_effect=convert):
            result = self.run_tool()

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(before, file_inventory(self.app))

    def test_rejects_device_cgbi_icon_when_system_conversion_fails(self):
        (self.app / "AppIcon60x60@2x.png").write_bytes(
            b"\x89PNG\r\n\x1a\n"
            b"\x00\x00\x00\x04CgBI"
            b"\x50\x00\x20\x06"
            b"\x00\x00\x00\x00"
        )
        before = file_inventory(self.app)
        failed = SimpleNamespace(returncode=1, stdout="", stderr="conversion failed")

        with mock.patch.object(self.audit.subprocess, "run", return_value=failed):
            result = self.run_tool()

        self.assertNotEqual(0, result.returncode)
        self.assertIn("could not be converted", result.stderr.casefold())
        self.assertEqual(before, file_inventory(self.app))

    def test_rejects_symlinked_bundle_members(self):
        outside = Path(self.temporary_directory.name) / "outside.plist"
        outside.write_bytes((self.app / "Info.plist").read_bytes())
        (self.app / "Info.plist").unlink()
        (self.app / "Info.plist").symlink_to(outside)

        result = self.run_tool()

        self.assertNotEqual(0, result.returncode)
        self.assertIn("symlink", result.stderr.casefold())


if __name__ == "__main__":
    unittest.main()
