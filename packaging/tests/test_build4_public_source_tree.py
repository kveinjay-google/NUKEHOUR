import hashlib
import tempfile
import unittest
from pathlib import Path

from packaging.build4_public_source_tree import denied_assets, export_tree


class Build4PublicSourceTreeTest(unittest.TestCase):
    def setUp(self):
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        self.source = self.root / "source"
        self.source.mkdir()
        self.ledger = self.source / "ledger.tsv"

    def tearDown(self):
        self.temporary_directory.cleanup()

    def write(self, relative: str, data: bytes = b"source") -> Path:
        path = self.source / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        return path

    def write_ledger(self, rows: list[tuple[str, str, str]]) -> None:
        lines = ["# scope\tclassification\tsha256\tsize\tbundled_in_build4\tpath"]
        for classification, relative, data in rows:
            encoded = data.encode()
            lines.append(
                f"SOURCE\t{classification}\t{hashlib.sha256(encoded).hexdigest()}\t"
                f"{len(encoded)}\tno\t{relative}"
            )
        self.ledger.write_text("\n".join(lines) + "\n", encoding="utf-8")

    def test_original_and_unknown_assets_are_omitted_but_evidence_is_kept(self):
        self.write("artsrc/vxl-reference/tank.vxl", b"retail")
        self.write("mods/ra2/bits/cameos/unknown.shp", b"unknown")
        self.write("docs/archive/evidence/provenance.tsv", b"evidence")
        self.write("src/Game.cs", b"source")
        self.write_ledger([
            ("ORIGINAL_RA2_ASSET", "artsrc/vxl-reference/tank.vxl", "retail"),
            ("UNKNOWN", "mods/ra2/bits/cameos/unknown.shp", "unknown"),
        ])

        output = self.root / "public"
        export_tree(self.source, output, self.ledger)

        self.assertFalse((output / "artsrc/vxl-reference/tank.vxl").exists())
        self.assertFalse((output / "mods/ra2/bits/cameos/unknown.shp").exists())
        self.assertTrue((output / "docs/archive/evidence/provenance.tsv").is_file())
        self.assertTrue((output / "src/Game.cs").is_file())

    def test_build_and_signing_outputs_are_omitted(self):
        self.write("engine/bin/OpenRA.Game.dll", b"generated")
        self.write("ios/OpenRA.iOS/obj/project.assets.json", b"generated")
        self.write("release.ipa", b"signed")
        self.write("profile.mobileprovision", b"secret")
        self.write_ledger([])

        output = self.root / "public"
        export_tree(self.source, output, self.ledger)

        self.assertFalse((output / "engine/bin").exists())
        self.assertFalse((output / "ios/OpenRA.iOS/obj").exists())
        self.assertFalse((output / "release.ipa").exists())
        self.assertFalse((output / "profile.mobileprovision").exists())

    def test_refuses_to_overwrite_destination(self):
        self.write_ledger([])
        output = self.root / "public"
        output.mkdir()

        with self.assertRaises(FileExistsError):
            export_tree(self.source, output, self.ledger)

    def test_ledger_denies_only_original_and_unknown_source_rows(self):
        self.write_ledger([
            ("ORIGINAL_RA2_ASSET", "retail.vxl", "retail"),
            ("UNKNOWN", "unknown.png", "unknown"),
            ("PROJECT_CREATED", "project.png", "project"),
        ])

        self.assertEqual({"retail.vxl", "unknown.png"}, denied_assets(self.ledger))

    def test_public_build_authenticates_registry_without_private_descriptors(self):
        repository = Path(__file__).resolve().parents[2]
        project = (repository / "ios/OpenRA.iOS/OpenRA.iOS.csproj").read_text(encoding="utf-8")

        self.assertIn(
            '<Target Name="GenerateAotObjectRegistry" BeforeTargets="CoreCompile" '
            'Condition="\'$(PublicClean)\' != \'true\'"',
            project,
        )
        self.assertIn('Target Name="ValidatePublicAotObjectRegistry"', project)
        self.assertIn("<GetFileHash Files=\"$(GeneratedAotRegistryFile)\" Algorithm=\"SHA256\">", project)
        self.assertIn("61F59A25DFC88373457FB0C16F596735D2938CB7C9A8992D3D78BAD9D538AF5D", project)


if __name__ == "__main__":
    unittest.main()
