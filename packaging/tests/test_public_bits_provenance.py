import hashlib
import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
PROVENANCE = ROOT / "docs" / "legal" / "public-bits-provenance.json"
INVENTORIES = (
    ROOT / "packaging" / "public-content-manifest.json",
    ROOT / "docs" / "legal" / "public-asset-sbom.json",
    ROOT / "packaging" / "ios" / "public_clean_manifest.json",
)


class PublicBitsProvenanceTest(unittest.TestCase):
    def test_every_bundled_bits_file_has_exact_origin_and_inventory_entries(self):
        provenance = json.loads(PROVENANCE.read_text(encoding="utf-8"))
        records = {entry["path"]: entry for entry in provenance["files"]}
        source_files = {
            path.relative_to(ROOT).as_posix()
            for path in (ROOT / "mods" / "ra2" / "bits").rglob("*")
            if path.is_file()
        }

        self.assertEqual(1, provenance["schemaVersion"])
        self.assertEqual(source_files, set(records))
        self.assertEqual(52, len(records))
        self.assertEqual(
            {"openra-ra2", "romanovs-vengeance", "nuke-hour-original"},
            {entry["origin"] for entry in records.values()},
        )

        inventories = []
        for path in INVENTORIES:
            payload = json.loads(path.read_text(encoding="utf-8"))
            inventories.append({entry["path"]: entry for entry in payload["files"]})

        for relative, record in records.items():
            with self.subTest(path=relative):
                source = ROOT / relative
                self.assertEqual(source.stat().st_size, record["size"])
                self.assertEqual(hashlib.sha256(source.read_bytes()).hexdigest(), record["sha256"])
                self.assertTrue(record["sourceReference"])
                expected_category = (
                    "project-original"
                    if record["origin"] == "nuke-hour-original"
                    else "redistributable-third-party"
                )
                for inventory in inventories:
                    entry = inventory[relative]
                    self.assertTrue(entry["public"])
                    self.assertEqual(record["sha256"], entry["sha256"])
                    self.assertEqual(record["size"], entry["size"])
                    self.assertEqual(expected_category, entry["category"])
                    self.assertIn("GPL-3.0", entry["license"])

    def test_provenance_separates_retail_assets_from_shipped_support_art(self):
        provenance = json.loads(PROVENANCE.read_text(encoding="utf-8"))

        self.assertFalse(provenance["containsRetailGameFiles"])
        self.assertIn("retailComparison", provenance)
        self.assertGreater(provenance["retailComparison"]["matchedNames"], 0)
        self.assertEqual(0, provenance["retailComparison"]["byteIdenticalFiles"])


if __name__ == "__main__":
    unittest.main()
