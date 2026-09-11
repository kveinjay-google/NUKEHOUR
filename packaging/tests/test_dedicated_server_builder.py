import importlib.util
import json
import time
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "packaging" / "server" / "build_server.py"


def load_module():
    spec = importlib.util.spec_from_file_location("build_nukehour_server", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


class DedicatedServerBuilderPresenceTest(unittest.TestCase):
    def test_builder_is_checked_in(self):
        self.assertTrue(SCRIPT.is_file(), f"Missing server builder: {SCRIPT}")


@unittest.skipUnless(SCRIPT.is_file(), "server builder not implemented yet")
class DedicatedServerBuilderTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.builder = load_module()

    def test_server_data_policy_keeps_simulation_and_omits_presentation(self):
        keep = (
            "mods/ra2/mod.yaml",
            "mods/ra2/rules/world.yaml",
            "mods/ra2/fluent/rules.ftl",
            "mods/ra2/maps/defcon-6/map.yaml",
            "mods/ra2/maps/defcon-6/map.bin",
            "engine/mods/common/scripts/utils.lua",
            "engine/mods/common/hotkeys/game.yaml",
        )
        omit = (
            "mods/ra2/maps/defcon-6/map.png",
            "mods/ra2/uibits/chrome.png",
            "mods/ra2/bits/unit.shp",
            "mods/ra2/fonts/font.otf",
            "mods/ra2/audio/theme.aud",
            "mods/ra2/content/ra2.mix",
            "engine/mods/common/chrome/lobby.yaml",
            "mods/ra2/chrome.yaml",
            "mods/ra2/metrics.yaml",
            "mods/ra2/cursors.yaml",
        )

        for path in keep:
            self.assertTrue(self.builder.is_server_data_file(Path(path)), path)
        for path in omit:
            self.assertFalse(self.builder.is_server_data_file(Path(path)), path)

    def test_safe_relative_path_rejects_traversal_and_absolute_paths(self):
        self.assertEqual(
            Path("mods/ra2/mod.yaml"),
            self.builder.validate_relative_path("mods/ra2/mod.yaml"),
        )
        for path in ("../secret", "/etc/passwd", "mods/../secret", "", "a\\b"):
            with self.subTest(path=path):
                with self.assertRaises(self.builder.BuildError):
                    self.builder.validate_relative_path(path)

    def test_copy_file_rejects_symlink_sources(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source"
            target = root / "target"
            source.mkdir()
            (source / "real.yaml").write_text("Rules:\n", encoding="utf-8")
            (source / "link.yaml").symlink_to(source / "real.yaml")

            with self.assertRaises(self.builder.BuildError):
                self.builder.copy_regular_file(source / "link.yaml", target / "link.yaml")

    def test_supported_rids_are_bounded(self):
        self.assertEqual("linux-x64", self.builder.validate_rid("linux-x64"))
        self.assertEqual("linux-arm64", self.builder.validate_rid("linux-arm64"))
        for rid in ("osx-arm64", "linux-musl-x64", "../../linux-x64"):
            with self.assertRaises(self.builder.BuildError):
                self.builder.validate_rid(rid)

    def test_archive_bytes_are_deterministic(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            bundle = root / "NUKE-HOUR-Server-test-linux-x64"
            bundle.mkdir()
            (bundle / "VERSION").write_text("test\n", encoding="utf-8")
            first = root / "first.tar.gz"
            second = root / "second.tar.gz"

            self.builder.deterministic_archive(bundle, first)
            time.sleep(1.1)
            self.builder.deterministic_archive(bundle, second)

            self.assertEqual(first.read_bytes(), second.read_bytes())

    def test_missing_reflection_loaded_mod_assembly_fails_build(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            output = root / "bin"
            output.mkdir()
            (root / "OpenRA.Mods.Common.dll").write_bytes(b"common")

            with self.assertRaises(self.builder.BuildError):
                self.builder.copy_required_mod_assemblies(
                    root,
                    output,
                    ["OpenRA.Mods.Common.dll", "OpenRA.Mods.Cnc.dll"],
                )

    def test_required_mod_dependency_manifests_are_copied(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            output = root / "bin"
            output.mkdir()
            dependency = root / "OpenRA.Mods.Common.deps.json"
            dependency.write_text('{"runtimeTarget":{}}\n', encoding="utf-8")

            self.builder.copy_required_mod_dependency_manifests(
                root, output, [dependency.name]
            )

            self.assertEqual(dependency.read_bytes(), (output / dependency.name).read_bytes())

    def test_managed_publish_output_requires_an_independent_allowlist(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            publish = root / "publish"
            output = root / "bin"
            publish.mkdir()
            output.mkdir()
            (publish / "OpenRA.Server").write_bytes(b"server")
            (publish / "OpenRA.Server.dll").write_bytes(b"managed")
            delivery = {
                "publishedFileAllowlist": ["OpenRA.Server", "OpenRA.Server.dll"],
                "requiredPublishedFiles": ["OpenRA.Server", "OpenRA.Server.dll"],
            }

            self.builder.copy_allowlisted_publish_files(publish, output, delivery)
            self.assertEqual(
                {"OpenRA.Server", "OpenRA.Server.dll"},
                {path.name for path in output.iterdir()},
            )

            (publish / "unexpected-retail.bin").write_bytes(b"retail")
            with self.assertRaisesRegex(
                self.builder.BuildError, "not explicitly allowlisted"
            ):
                self.builder.copy_allowlisted_publish_files(publish, output, delivery)

    def test_delivery_manifest_allowlist_covers_the_current_publish_contract(self):
        delivery = json.loads(
            (ROOT / "server" / "server-files.json").read_text(encoding="utf-8")
        )
        allowlist = delivery["publishedFileAllowlist"]
        for name in delivery["requiredPublishedFiles"]:
            self.assertTrue(
                any(self.builder.fnmatch.fnmatchcase(name, pattern) for pattern in allowlist),
                name,
            )


if __name__ == "__main__":
    unittest.main()
