import hashlib
import json
import plistlib
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from packaging.macos import build_public_clean as builder


ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "packaging/macos/public_clean_manifest.json"


class MacOSPublicCleanBuilderTest(unittest.TestCase):
    def test_real_manifest_has_only_tracked_reviewed_public_sources(self):
        manifest = builder.load_source_manifest(MANIFEST)
        paths = builder.public_source_paths(ROOT, manifest)
        relative = {path.relative_to(ROOT).as_posix() for path in paths}

        self.assertIn("mods/ra2/mod.yaml", relative)
        self.assertIn("mods/ra2/uibits/chrome.png", relative)
        self.assertIn("engine/mods/common/FreeSans.ttf", relative)
        self.assertIn("engine/glsl/combined.vert", relative)
        self.assertIn("mods/ra2-content/mod.yaml", relative)
        self.assertIn("mods/ra2-content/installer/macos-directory.yaml", relative)
        self.assertNotIn("mods/ra2/maps", "\n".join(sorted(relative)))
        self.assertNotIn("mods/ra2/bits/structures", "\n".join(sorted(relative)))
        self.assertFalse(any(path.name == ".DS_Store" for path in paths))
        self.assertFalse(any(path.suffix.lower() in builder.PROHIBITED_SOURCE_EXTENSIONS for path in paths))

    def test_real_manifest_hashes_match_checkout(self):
        manifest = builder.load_source_manifest(MANIFEST)

        errors = builder.verify_source_manifest(ROOT, manifest)

        self.assertEqual((), errors)

    def test_content_manager_is_branded_and_uses_chinese_capable_fonts(self):
        content_mod = (ROOT / "mods/ra2-content/mod.yaml").read_text(encoding="utf-8")
        chinese = (ROOT / "mods/ra2-content/fluent/zh-CN/chrome.ftl").read_text(
            encoding="utf-8")

        self.assertIn("\tWindowTitle: mod-windowtitle", content_mod)
        self.assertIn("\t\t$ra2: ra2", content_mod)
        self.assertIn("Font: ra2|fonts/NotoSansCJKsc-Regular.otf", content_mod)
        self.assertIn("Font: ra2|fonts/NotoSansCJKsc-Bold.otf", content_mod)
        self.assertNotIn("Font: common|FreeSans", content_mod)
        self.assertIn("mod-windowtitle = NUKE HOUR", chinese)
        self.assertIn("label-content-panel-title = 管理游戏内容", chinese)
        self.assertIn("label-package-template-installed = 已安装", chinese)
        self.assertIn("button-content-panel-check-source = 检测光盘或安装目录", chinese)

    def test_stage_copies_only_manifest_entries(self):
        with tempfile.TemporaryDirectory(prefix="nukehour-stage-test-") as temporary:
            base = Path(temporary)
            source = base / "source"
            destination = base / "runtime"
            source.mkdir()
            (source / "keep.yaml").write_bytes(b"keep")
            (source / "private.mix").write_bytes(b"retail")
            manifest = {"schemaVersion": 1, "files": [{
                "source": "keep.yaml",
                "destination": "mods/ra2/keep.yaml",
                "size": 4,
                "sha256": hashlib.sha256(b"keep").hexdigest(),
                "category": "project-original",
                "license": "test",
            }]}

            builder.stage_public_runtime(source, destination, manifest)

            self.assertEqual(b"keep", (destination / "mods/ra2/keep.yaml").read_bytes())
            self.assertFalse((destination / "private.mix").exists())

    def test_plist_and_artifact_name_use_canonical_brand_without_ra2(self):
        payload = builder.public_app_plist("1.0.3", 3)
        encoded = plistlib.dumps(payload).decode("utf-8")

        self.assertEqual("NUKE HOUR", payload["CFBundleDisplayName"])
        self.assertEqual("1.0.3", payload["CFBundleShortVersionString"])
        self.assertEqual("3", payload["CFBundleVersion"])
        self.assertNotIn("RA2", encoded)
        self.assertEqual(
            "NUKE-HOUR-PublicClean-1.0.3-arm64.dmg",
            builder.public_dmg_name("1.0.3"),
        )

    def test_sbom_contains_hashes_for_manifest_and_generated_files(self):
        with tempfile.TemporaryDirectory(prefix="nukehour-sbom-test-") as temporary:
            root = Path(temporary)
            (root / "engine/bin").mkdir(parents=True)
            (root / "engine/bin/OpenRA.dll").write_bytes(b"engine")

            sbom = builder.build_sbom(root, {"schemaVersion": 1, "files": []})

            self.assertEqual(1, sbom["schemaVersion"])
            self.assertEqual("engine/bin/OpenRA.dll", sbom["files"][0]["path"])
            self.assertEqual(hashlib.sha256(b"engine").hexdigest(), sbom["files"][0]["sha256"])

    def test_engine_binary_copy_places_mix_database_at_runtime_engine_root(self):
        with tempfile.TemporaryDirectory(prefix="nukehour-engine-copy-test-") as temporary:
            root = Path(temporary) / "source"
            runtime = Path(temporary) / "runtime"
            (root / "engine/bin").mkdir(parents=True)
            for name in ("OpenRA.dll", "OpenRA.Mods.RA2.dll", "OpenRA.Platforms.Default.dll"):
                (root / "engine/bin" / name).write_bytes(b"engine")
            (root / "engine/bin/global mix database.dat").write_bytes(b"filename-index")

            builder._copy_engine_bin(root, runtime)

            self.assertEqual(
                b"filename-index",
                (runtime / "engine/global mix database.dat").read_bytes(),
            )

    def test_public_clean_cursors_reuse_user_supplied_retail_mouse_sheet(self):
        source = (
            "Cursors:\n"
            "\tmouse.shp: mouse\n"
            "\t\tattackmove:\n"
            "\t\t\tStart: 404\n"
            "\tundeploy.shp: mouse\n"
            "\t\tundeploy:\n"
            "\t\t\tStart: 0\n"
            "\tassaultmove.shp: mouse\n"
            "\t\tassaultmove:\n"
            "\t\t\tStart: 0\n"
        ).encode()

        result = builder._public_clean_cursors(source).decode()

        self.assertNotIn("undeploy.shp", result)
        self.assertNotIn("assaultmove.shp", result)
        self.assertIn("\t\tundeploy:\n\t\t\tStart: 120", result)
        self.assertIn("\t\tassaultmove:\n\t\t\tStart: 404", result)
        self.assertIn("\t\tassaultmove-blocked:\n\t\t\tStart: 384", result)

    def test_pyinstaller_command_is_arm64_windowed_and_branded(self):
        command = builder.pyinstaller_command(
            Path("/tools/pyinstaller"), ROOT, Path("/tmp/work"))

        self.assertEqual("/tools/pyinstaller", command[0])
        self.assertIn("--windowed", command)
        self.assertIn("--onedir", command)
        self.assertEqual("arm64", command[command.index("--target-architecture") + 1])
        self.assertEqual("NUKE HOUR", command[command.index("--name") + 1])
        self.assertEqual(str(ROOT / "launcher_assets/icon.icns"),
                         command[command.index("--icon") + 1])

    def test_release_metadata_does_not_claim_notarization_without_credentials(self):
        metadata = builder.release_metadata(
            "1.0.3", 3, signing_identity="Developer ID Application: Example",
            notarized=False)

        self.assertEqual("signed", metadata["signing"])
        self.assertEqual("not-submitted", metadata["notarization"])
        self.assertNotIn("Example", json.dumps(metadata))

    def test_release_shell_entry_point_runs_the_python_builder(self):
        script = ROOT / "packaging/macos/build-public-clean.sh"

        self.assertTrue(script.is_file())
        source = script.read_text(encoding="utf-8")
        self.assertIn("build_public_clean.py", source)
        self.assertIn("artifacts/public-macos", source)

    def test_game_apphost_resolves_dotnet_root_above_host_directory(self):
        source = (ROOT / "packaging/macos/apphost_ra2.c").read_text(encoding="utf-8")

        self.assertIn("for (int i = 0; i < 4; i++)", source)


if __name__ == "__main__":
    unittest.main()
