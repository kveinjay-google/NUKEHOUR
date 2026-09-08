import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from packaging.public_bundle_audit import audit_tree, inventory_tree


class PublicBundleAuditTest(unittest.TestCase):
    def setUp(self):
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        self.manifest = {"schemaVersion": 1, "files": []}

    def tearDown(self):
        self.temporary_directory.cleanup()

    def write(self, relative_path: str, data: bytes = b"data") -> Path:
        path = self.root / relative_path
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        return path

    def allow(self, relative_path: str, data: bytes, category: str = "project-original") -> None:
        self.manifest["files"].append({
            "path": relative_path,
            "sha256": hashlib.sha256(data).hexdigest(),
            "size": len(data),
            "category": category,
            "license": "Project original",
            "public": True,
        })

    def error_codes(self, result):
        return {error.code for error in result.errors}

    def test_retail_mix_is_rejected_even_when_listed(self):
        data = b"retail"
        self.write("mods/ra2/ra2.mix", data)
        self.allow("mods/ra2/ra2.mix", data, "retail-user-content")

        result = audit_tree(self.root, self.manifest)

        self.assertIn("retail-extension", self.error_codes(result))

    def test_unlisted_binary_art_is_rejected(self):
        self.write("mods/ra2/bits/unit.shp")

        result = audit_tree(self.root, self.manifest)

        self.assertIn("retail-extension", self.error_codes(result))

    def test_actool_generated_app_icon_outputs_are_allowed(self):
        self.write("Assets.car", b"compiled catalog")
        self.write("AppIcon60x60@2x.png", b"compiled phone icon")
        self.write("AppIcon76x76@2x~ipad.png", b"compiled tablet icon")

        result = audit_tree(self.root, self.manifest)

        self.assertEqual((), result.errors)

    def test_unlisted_top_level_png_is_not_treated_as_generated_app_icon(self):
        self.write("UnrelatedArtwork.png", b"private artwork")

        result = audit_tree(self.root, self.manifest)

        self.assertIn("unclassified-resource", self.error_codes(result))

    def test_generated_root_managed_and_aot_outputs_are_allowed(self):
        self.write("OpenRA.Game.dll", b"managed")
        self.write("OpenRA.Game.aotdata.arm64", b"aot")
        self.write("OpenRA.iOS", b"native executable")
        self.write("runtimeconfig.bin", b"runtime")
        self.write("icudt.dat", b"runtime globalization data")
        self.write("archived-expanded-entitlements.xcent", b"generated entitlements")

        result = audit_tree(self.root, self.manifest)

        self.assertEqual((), result.errors)

    def test_nested_unlisted_dll_is_not_treated_as_generated_output(self):
        self.write("mods/ra2/unreviewed.dll", b"not a root build output")

        result = audit_tree(self.root, self.manifest)

        self.assertIn("unclassified-resource", self.error_codes(result))

    def test_app_icon_lookalike_outside_actool_output_set_is_rejected(self):
        self.write("AppIcon83.5x83.5@3x.png", b"not an actool output")

        result = audit_tree(self.root, self.manifest)

        self.assertIn("unclassified-resource", self.error_codes(result))

    def test_declared_original_asset_with_matching_sha_passes(self):
        data = b"original"
        self.write("mods/ra2-public/icon.png", data)
        self.allow("mods/ra2-public/icon.png", data)

        result = audit_tree(self.root, self.manifest)

        self.assertEqual((), result.errors)

    def test_declared_asset_with_wrong_hash_is_rejected(self):
        self.write("mods/ra2-public/icon.png", b"changed!")
        self.allow("mods/ra2-public/icon.png", b"original")

        result = audit_tree(self.root, self.manifest)

        self.assertIn("hash-mismatch", self.error_codes(result))

    def test_declared_asset_with_wrong_size_is_rejected(self):
        data = b"original"
        self.write("en.lproj/InfoPlist.strings", data)
        self.allow("en.lproj/InfoPlist.strings", data)
        self.manifest["files"][0]["size"] += 1

        result = audit_tree(self.root, self.manifest)

        self.assertIn("size-mismatch", self.error_codes(result))

    def test_missing_declared_localization_is_rejected(self):
        self.allow("zh-Hans.lproj/InfoPlist.strings", b"localized")

        result = audit_tree(self.root, self.manifest)

        self.assertIn("manifest-file-missing", self.error_codes(result))

    def test_unlisted_localization_is_not_broadly_exempted(self):
        self.write("fr.lproj/InfoPlist.strings", b"unexpected")

        result = audit_tree(self.root, self.manifest)

        self.assertIn("unclassified-resource", self.error_codes(result))

    def test_non_public_manifest_entry_is_rejected(self):
        data = b"private"
        self.write("mods/ra2-public/private.png", data)
        self.allow("mods/ra2-public/private.png", data)
        self.manifest["files"][0]["public"] = False

        result = audit_tree(self.root, self.manifest)

        self.assertIn("not-public", self.error_codes(result))

    def test_absent_non_public_inventory_entry_is_not_required(self):
        self.manifest["files"].append({
            "path": "mods/ra2/maps/private-map.oramap",
            "sha256": hashlib.sha256(b"private").hexdigest(),
            "size": len(b"private"),
            "category": "retail-user-content",
            "license": "User supplied",
            "public": False,
        })

        result = audit_tree(self.root, self.manifest)

        self.assertNotIn("manifest-file-missing", self.error_codes(result))

    def test_symlink_that_escapes_root_is_rejected(self):
        outside = Path(self.temporary_directory.name).parent / "outside-public-audit.bin"
        outside.write_bytes(b"outside")
        try:
            link = self.root / "mods/ra2-public/link.bin"
            link.parent.mkdir(parents=True, exist_ok=True)
            link.symlink_to(outside)

            result = audit_tree(self.root, self.manifest)

            self.assertIn("unsafe-symlink", self.error_codes(result))
        finally:
            outside.unlink(missing_ok=True)

    def test_json_manifest_can_be_serialized_without_platform_paths(self):
        data = b"shader"
        self.write("glsl/combined.frag", data)
        self.allow("glsl/combined.frag", data, "engine-source")

        result = audit_tree(self.root, json.loads(json.dumps(self.manifest)))

        self.assertEqual("glsl/combined.frag", result.files[0].path)

    def test_inventory_defaults_every_file_to_unknown_and_private(self):
        self.write("bits/unit.shp", b"asset")

        entries = inventory_tree(self.root, "mods/ra2")

        self.assertEqual("mods/ra2/bits/unit.shp", entries[0]["path"])
        self.assertEqual("unknown-or-derivative", entries[0]["category"])
        self.assertFalse(entries[0]["public"])
        self.assertEqual(hashlib.sha256(b"asset").hexdigest(), entries[0]["sha256"])


if __name__ == "__main__":
    unittest.main()
