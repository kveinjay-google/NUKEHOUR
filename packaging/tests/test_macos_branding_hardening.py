import importlib.util
import unittest
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
GENERATOR_PATH = ROOT / "packaging" / "macos" / "generate_brand_artwork.py"
WALLPAPER = ROOT / "mods" / "ra2" / "uibits" / "NUCLEAR-CRISIS-BG-06.png"


def load_generator():
	spec = importlib.util.spec_from_file_location("generate_brand_artwork", GENERATOR_PATH)
	if spec is None or spec.loader is None:
		raise RuntimeError(f"unable to load {GENERATOR_PATH}")

	module = importlib.util.module_from_spec(spec)
	spec.loader.exec_module(module)
	return module


class MacOSBrandingHardeningTest(unittest.TestCase):
	def test_dedicated_server_gives_the_canonical_mod_last_precedence(self):
		source = (ROOT / "launch-dedicated.sh").read_text(encoding="utf-8")

		self.assertIn('MOD_SEARCH_PATHS="./mods,${TEMPLATE_ROOT}/mods"', source)
		self.assertNotIn('MOD_SEARCH_PATHS="${TEMPLATE_ROOT}/mods,./mods"', source)

	def test_obsolete_afghanistan_launcher_emblem_is_removed(self):
		self.assertFalse(
			(ROOT / "launcher_assets" / "emblem.png").exists(),
			"the unused Afghanistan Crisis emblem must not remain in the product tree",
		)

	def test_dmg_backgrounds_are_reproducible_nuke_hour_artwork(self):
		generator = load_generator()
		self.assertEqual("NUKE HOUR", getattr(generator, "BRAND_TITLE", None))
		cases = (
			((600, 450), ROOT / "packaging" / "artwork" / "macos-background.png"),
			((1200, 900), ROOT / "packaging" / "artwork" / "macos-background-2x.png"),
		)

		with Image.open(WALLPAPER) as source:
			for size, installed_path in cases:
				with self.subTest(size=size):
					expected = generator.render_dmg_background(source, size)
					with Image.open(installed_path) as installed:
						self.assertEqual(size, installed.size)
						self.assertEqual("RGBA", installed.convert("RGBA").mode)
						self.assertEqual(
							expected.convert("RGBA").tobytes(),
							installed.convert("RGBA").tobytes(),
						)

	def test_macos_packager_cleans_and_audits_the_staging_bundle(self):
		source = (ROOT / "packaging" / "macos" / "buildpackage.sh").read_text(
			encoding="utf-8"
		)
		clean = 'rm -rf "${BUILTDIR}"'
		first_staging_write = 'mkdir -p "${LAUNCHER_RESOURCES_DIR}"'

		self.assertIn(clean, source)
		self.assertLess(source.index(clean), source.index(first_staging_write))
		self.assertIn(
			'if [ -e "${LAUNCHER_RESOURCES_DIR}/mods/${MOD_ID}/uibits/loadscreen.png" ]; then',
			source,
		)
		self.assertIn("Legacy loading artwork leaked into the NUKE HOUR macOS bundle", source)


if __name__ == "__main__":
	unittest.main()
