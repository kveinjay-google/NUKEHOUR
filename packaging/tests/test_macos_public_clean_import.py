import json
import os
import tempfile
import unittest
from pathlib import Path

import ra2_files


class MacOSPublicCleanImportTest(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="nukehour-import-test-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)

    def write(self, relative, data=b"content"):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        return path

    def test_default_support_directory_uses_the_nuke_hour_brand(self):
        self.assertEqual("Nuke Hour", Path(ra2_files.SUPPORT).name)
        self.assertEqual("OpenRA", Path(ra2_files.LEGACY_SUPPORT).name)

    def test_legacy_migration_copies_only_nuke_hour_owned_data_and_keeps_sources(self):
        legacy = self.root / "OpenRA"
        current = self.root / "Nuke Hour"
        expected = {
            "settings.yaml": b"settings",
            "ra2-commandbar.yaml": b"commandbar",
            "skirmish.ra2.yaml": b"skirmish",
            "Content/ra2/ra2.mix": b"retail",
            "maps/ra2/version/custom.oramap": b"map",
            "Saves/ra2/version/save.orasav": b"save",
            "Replays/ra2/version/replay.orarep": b"replay",
            "Screenshots/ra2/version/screen.png": b"screen",
            "Editor/ra2/version/marker.bin": b"marker",
        }
        for relative, data in expected.items():
            path = legacy / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        unrelated = legacy / "Content/cnc/private.mix"
        unrelated.parent.mkdir(parents=True)
        unrelated.write_bytes(b"other mod")
        linked = legacy / "Content/ra2/linked.mix"
        linked.symlink_to(unrelated)

        result = ra2_files.migrate_legacy_support(legacy, current)

        self.assertEqual(sorted(expected), list(result["copied"]))
        self.assertIn("Content/ra2/linked.mix", result["skipped"])
        self.assertFalse((current / "Content/cnc/private.mix").exists())
        for relative, data in expected.items():
            self.assertEqual(data, (current / relative).read_bytes())
            self.assertEqual(data, (legacy / relative).read_bytes())

    def test_legacy_migration_is_idempotent_and_never_overwrites_new_data(self):
        legacy = self.root / "OpenRA"
        current = self.root / "Nuke Hour"
        legacy_settings = legacy / "settings.yaml"
        current_settings = current / "settings.yaml"
        legacy_settings.parent.mkdir(parents=True)
        current_settings.parent.mkdir(parents=True)
        legacy_settings.write_bytes(b"legacy")
        current_settings.write_bytes(b"current")

        first = ra2_files.migrate_legacy_support(legacy, current)
        second = ra2_files.migrate_legacy_support(legacy, current)

        self.assertEqual(b"current", current_settings.read_bytes())
        self.assertEqual(("settings.yaml",), first["conflicts"])
        self.assertEqual(("settings.yaml",), second["conflicts"])
        self.assertEqual((), first["copied"])
        self.assertEqual((), second["copied"])

    def test_legacy_migration_rejects_a_symlinked_destination_root(self):
        legacy = self.root / "OpenRA"
        outside = self.root / "outside"
        current = self.root / "Nuke Hour"
        legacy.mkdir()
        outside.mkdir()
        (legacy / "settings.yaml").write_bytes(b"settings")
        current.symlink_to(outside, target_is_directory=True)

        with self.assertRaisesRegex(ValueError, "symbolic link"):
            ra2_files.migrate_legacy_support(legacy, current)

        self.assertEqual([], list(outside.iterdir()))

    def test_discovers_supported_packages_and_maps_recursively(self):
        self.write("game/RA2.MIX", b"base")
        self.write("game/Data/language.mix", b"language")
        self.write("game/Data/theme.mix", b"music")
        self.write("game/Maps/custom.mpr", b"map")
        self.write("game/RA2.EXE", b"MZ executable")
        self.write("game/readme.txt", b"not imported")

        discovery = ra2_files.discover_retail_content([self.root / "game"])

        self.assertEqual(
            ["custom.mpr", "language.mix", "ra2.mix", "theme.mix"],
            [item["name"] for item in discovery["files"]],
        )
        self.assertIn("RA2.EXE", [item["name"] for item in discovery["rejected"]])
        self.assertNotIn("readme.txt", [item["name"] for item in discovery["rejected"]])

    def test_rejects_symlinks_and_executable_magic(self):
        outside = self.write("outside.mix", b"retail")
        selected = self.root / "selected"
        selected.mkdir()
        os.symlink(outside, selected / "linked.mix")
        self.write("selected/fake.mix", b"MZ\x90\x00payload")

        discovery = ra2_files.discover_retail_content([selected])

        self.assertEqual([], discovery["files"])
        reasons = {item["name"]: item["reason"] for item in discovery["rejected"]}
        self.assertEqual("symlink", reasons["linked.mix"])
        self.assertEqual("executable-magic", reasons["fake.mix"])

    def test_conflicting_duplicate_basenames_fail_closed(self):
        first = self.write("one/ra2.mix", b"first")
        second = self.write("two/RA2.MIX", b"second")

        with self.assertRaisesRegex(ra2_files.RetailImportError, "conflicting duplicate"):
            ra2_files.discover_retail_content([first, second])

    def test_import_publishes_atomically_and_writes_path_free_receipt(self):
        destination = self.root / "Content" / "ra2"
        first = self.write("source/ra2.mix", b"base")
        second = self.write("source/language.mix", b"language")

        result = ra2_files.import_retail_content(
            [first, second], destination=destination, imported_at="2026-09-07T12:00:00Z")

        self.assertTrue(result["healthy"])
        self.assertEqual(b"base", (destination / "ra2.mix").read_bytes())
        receipt_text = (destination / "import-receipt.json").read_text(encoding="utf-8")
        receipt = json.loads(receipt_text)
        self.assertEqual("2026-09-07T12:00:00Z", receipt["importedAt"])
        self.assertEqual(["language.mix", "ra2.mix"], [item["name"] for item in receipt["files"]])
        self.assertNotIn(str(self.root), receipt_text)
        self.assertFalse(any(destination.parent.glob(".ra2.import-*")))

    def test_failed_import_preserves_existing_content(self):
        destination = self.root / "Content" / "ra2"
        destination.mkdir(parents=True)
        (destination / "ra2.mix").write_bytes(b"working")

        unsupported = self.write("source/readme.txt", b"nothing useful")
        with self.assertRaisesRegex(ra2_files.RetailImportError, "no supported"):
            ra2_files.import_retail_content([unsupported], destination=destination)

        self.assertEqual(b"working", (destination / "ra2.mix").read_bytes())
        self.assertFalse(any(destination.parent.glob(".ra2.import-*")))

    def test_minimum_readiness_does_not_require_theme_mix(self):
        destination = self.root / "Content" / "ra2"
        destination.mkdir(parents=True)
        (destination / "ra2.mix").write_bytes(b"base")
        (destination / "language.mix").write_bytes(b"language")

        status = ra2_files.content_status(destination)

        self.assertTrue(status["healthy"])
        self.assertEqual([], status["missing_core"])
        self.assertFalse(status["capabilities"]["music"])

    def test_map_files_are_published_to_the_user_map_directory(self):
        destination = self.root / "Content" / "ra2"
        maps_destination = self.root / "maps" / "ra2" / "version"
        map_file = self.write("source/custom.oramap", b"map")

        status = ra2_files.import_retail_content(
            [map_file], destination=destination, maps_destination=maps_destination)

        self.assertFalse(status["healthy"])
        self.assertEqual(b"map", (maps_destination / "custom.oramap").read_bytes())
        self.assertFalse((destination / "custom.oramap").exists())


if __name__ == "__main__":
    unittest.main()
