import hashlib
import importlib.util
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import PIL
from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
GENERATOR = ROOT / "packaging" / "generate_nuke_hour_icons.py"
SOURCE = ROOT / "branding" / "NUKE-HOUR-source-1254.png"
SOURCE_SHA256 = "af3a739efcd5eefd02c0a622b60dcb6a88cd52b224813f53df8284b60a19e99b"

GENERATOR_SPEC = importlib.util.spec_from_file_location(
    "generate_nuke_hour_icons", GENERATOR
)
ICON_GENERATOR = importlib.util.module_from_spec(GENERATOR_SPEC)
GENERATOR_SPEC.loader.exec_module(ICON_GENERATOR)

CATALOGS = (
    "ios/OpenRA.iOS/Assets.xcassets/AppIcon.appiconset",
    "ios/OpenRA.iOS/Assets.xcassets/PublicAppIcon.appiconset",
)
CATALOG_IMAGES = {
    "NUKE-HOUR-20.png": 20,
    "NUKE-HOUR-20@2x.png": 40,
    "NUKE-HOUR-20@3x.png": 60,
    "NUKE-HOUR-29.png": 29,
    "NUKE-HOUR-29@2x.png": 58,
    "NUKE-HOUR-29@3x.png": 87,
    "NUKE-HOUR-40.png": 40,
    "NUKE-HOUR-40@2x.png": 80,
    "NUKE-HOUR-40@3x.png": 120,
    "NUKE-HOUR-60@2x.png": 120,
    "NUKE-HOUR-60@3x.png": 180,
    "NUKE-HOUR-76.png": 76,
    "NUKE-HOUR-76@2x.png": 152,
    "NUKE-HOUR-83.5@2x.png": 167,
    "NUKE-HOUR-1024.png": 1024,
}
CATALOG_SLOTS = {
    ("iphone", "20x20", "2x"): ("NUKE-HOUR-20@2x.png", 40),
    ("iphone", "20x20", "3x"): ("NUKE-HOUR-20@3x.png", 60),
    ("iphone", "29x29", "2x"): ("NUKE-HOUR-29@2x.png", 58),
    ("iphone", "29x29", "3x"): ("NUKE-HOUR-29@3x.png", 87),
    ("iphone", "40x40", "2x"): ("NUKE-HOUR-40@2x.png", 80),
    ("iphone", "40x40", "3x"): ("NUKE-HOUR-40@3x.png", 120),
    ("iphone", "60x60", "2x"): ("NUKE-HOUR-60@2x.png", 120),
    ("iphone", "60x60", "3x"): ("NUKE-HOUR-60@3x.png", 180),
    ("ipad", "20x20", "1x"): ("NUKE-HOUR-20.png", 20),
    ("ipad", "20x20", "2x"): ("NUKE-HOUR-20@2x.png", 40),
    ("ipad", "29x29", "1x"): ("NUKE-HOUR-29.png", 29),
    ("ipad", "29x29", "2x"): ("NUKE-HOUR-29@2x.png", 58),
    ("ipad", "40x40", "1x"): ("NUKE-HOUR-40.png", 40),
    ("ipad", "40x40", "2x"): ("NUKE-HOUR-40@2x.png", 80),
    ("ipad", "76x76", "1x"): ("NUKE-HOUR-76.png", 76),
    ("ipad", "76x76", "2x"): ("NUKE-HOUR-76@2x.png", 152),
    ("ipad", "83.5x83.5", "2x"): ("NUKE-HOUR-83.5@2x.png", 167),
    ("ios-marketing", "1024x1024", "1x"): ("NUKE-HOUR-1024.png", 1024),
}
PACKAGING_SIZES = (16, 24, 32, 48, 64, 128, 256, 512, 1024)
ICNS_OUTPUTS = (
    "launcher_assets/icon.icns",
    "NUKE HOUR.app/Contents/Resources/NUKE-HOUR.icns",
    "NUKE HOUR GAME.app/Contents/Resources/NUKE-HOUR.icns",
)


def expected_outputs():
    outputs = {
        "branding/NUKE-HOUR-1024.png",
        "mods/ra2/icon.png",
        *ICNS_OUTPUTS,
    }
    for catalog in CATALOGS:
        outputs.add(f"{catalog}/Contents.json")
        outputs.update(f"{catalog}/{filename}" for filename in CATALOG_IMAGES)
    outputs.update(
        f"packaging/artwork/icon_{size}x{size}.png"
        for size in PACKAGING_SIZES
    )
    return sorted(outputs)


def expected_legacy_outputs():
    suffixes = [filename.removeprefix("NUKE-HOUR-") for filename in CATALOG_IMAGES]
    return sorted(
        {
            *(f"{CATALOGS[0]}/NUCLEAR-CRISIS-{suffix}" for suffix in suffixes),
            *(f"{CATALOGS[1]}/AppIcon-{suffix}" for suffix in suffixes),
        }
    )


class NukeHourIconPipelineTest(unittest.TestCase):
    def run_generator(self, root, *arguments, check=True):
        result = subprocess.run(
            [
                sys.executable,
                str(GENERATOR),
                "--root",
                str(root),
                *map(str, arguments),
            ],
            check=False,
            capture_output=True,
            text=True,
        )
        if check and result.returncode:
            self.fail(
                f"generator failed ({result.returncode})\n"
                f"stdout:\n{result.stdout}\nstderr:\n{result.stderr}"
            )
        return result

    def temporary_root(self):
        temporary = tempfile.TemporaryDirectory()
        root = Path(temporary.name)
        versioned_source = root / "branding" / SOURCE.name
        versioned_source.parent.mkdir(parents=True)
        shutil.copyfile(SOURCE, versioned_source)
        return temporary, root, versioned_source

    def assert_rgb_png(self, path, size):
        with Image.open(path) as image:
            self.assertEqual("PNG", image.format, str(path))
            self.assertEqual("RGB", image.mode, str(path))
            self.assertEqual((size, size), image.size, str(path))
            image.load()

    def test_unexpected_iconutil_errors_fail_closed_without_a_fallback(self):
        master = Image.new("RGB", (1024, 1024), (151, 37, 19))
        derived = ICON_GENERATOR.derived_pngs(master)
        unexpected = subprocess.CompletedProcess(
            ["iconutil"],
            1,
            stdout="",
            stderr="iconutil: permission denied\n",
        )

        with mock.patch.object(
            ICON_GENERATOR.subprocess, "run", return_value=unexpected
        ) as iconutil:
            with self.assertRaisesRegex(
                ICON_GENERATOR.IconPipelineError, "permission denied"
            ):
                ICON_GENERATOR.build_icns(derived)

        iconutil.assert_called_once()
        self.assertEqual("/usr/bin/iconutil", iconutil.call_args.args[0][0])

    def test_iconutil_failure_does_not_partially_mutate_the_root(self):
        temporary, root, source = self.temporary_root()
        self.addCleanup(temporary.cleanup)
        legacy = root / expected_legacy_outputs()[0]
        legacy.parent.mkdir(parents=True, exist_ok=True)
        legacy.write_bytes(b"legacy")
        before = {
            path.relative_to(root): hashlib.sha256(path.read_bytes()).hexdigest()
            for path in root.rglob("*")
            if path.is_file()
        }

        with mock.patch.object(
            ICON_GENERATOR,
            "build_icns",
            side_effect=ICON_GENERATOR.IconPipelineError("iconutil failed"),
        ):
            with self.assertRaisesRegex(
                ICON_GENERATOR.IconPipelineError, "iconutil failed"
            ):
                ICON_GENERATOR.generate(root, source)

        after = {
            path.relative_to(root): hashlib.sha256(path.read_bytes()).hexdigest()
            for path in root.rglob("*")
            if path.is_file()
        }
        self.assertEqual(before, after)

    def test_generation_rejects_managed_symlinks_before_writing(self):
        for link_kind in ("parent", "output"):
            with self.subTest(link_kind=link_kind):
                temporary, root, source = self.temporary_root()
                self.addCleanup(temporary.cleanup)
                external = tempfile.TemporaryDirectory()
                self.addCleanup(external.cleanup)
                external_root = Path(external.name)
                sentinel = external_root / "sentinel"
                sentinel.write_bytes(b"outside-must-not-change")

                if link_kind == "parent":
                    (root / "launcher_assets").symlink_to(
                        external_root, target_is_directory=True
                    )
                else:
                    output = root / "mods" / "ra2" / "icon.png"
                    output.parent.mkdir(parents=True)
                    os.symlink(sentinel, output)

                with mock.patch.object(
                    ICON_GENERATOR, "build_icns", return_value=b"not-reached"
                ) as iconutil:
                    with self.assertRaisesRegex(
                        ICON_GENERATOR.IconPipelineError, "symlink"
                    ):
                        ICON_GENERATOR.generate(root, source)

                iconutil.assert_not_called()
                self.assertEqual(b"outside-must-not-change", sentinel.read_bytes())
                self.assertFalse((root / "branding" / "NUKE-HOUR-1024.png").exists())

    def test_versioned_source_is_the_exact_opaque_user_attachment(self):
        self.assertTrue(SOURCE.is_file(), f"missing canonical source: {SOURCE}")
        self.assertEqual(SOURCE_SHA256, hashlib.sha256(SOURCE.read_bytes()).hexdigest())
        self.assert_rgb_png(SOURCE, 1254)

    def test_cli_lists_the_exact_sorted_output_and_mutation_allowlists(self):
        outputs = self.run_generator(ROOT, "--list-outputs").stdout.splitlines()
        self.assertEqual(expected_outputs(), outputs)
        self.assertEqual(outputs, sorted(set(outputs)))
        self.assertEqual(46, len(outputs))

        mutations = self.run_generator(ROOT, "--list-mutations").stdout.splitlines()
        self.assertEqual(
            sorted(set(expected_outputs()) | set(expected_legacy_outputs())),
            mutations,
        )
        self.assertEqual(mutations, sorted(set(mutations)))
        self.assertEqual(76, len(mutations))
        self.assertNotIn("branding/NUKE-HOUR-source-1254.png", mutations)
        for path in outputs + mutations:
            self.assertNotIn("NUCLEAR CRISIS.app/", path)
            self.assertNotIn("NUCLEAR CRISIS GAME.app/", path)

    def test_generation_is_deterministic_and_populates_every_consumer(self):
        self.assertEqual("11.3.0", PIL.__version__)
        temporary, root, source = self.temporary_root()
        self.addCleanup(temporary.cleanup)

        for legacy in expected_legacy_outputs():
            path = root / legacy
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b"legacy")

        self.run_generator(root, "--source", source)
        first_hashes = {
            path: hashlib.sha256((root / path).read_bytes()).hexdigest()
            for path in expected_outputs()
        }
        self.run_generator(root, "--source", source)
        second_hashes = {
            path: hashlib.sha256((root / path).read_bytes()).hexdigest()
            for path in expected_outputs()
        }
        self.assertEqual(first_hashes, second_hashes)
        self.assertEqual(
            SOURCE_SHA256,
            hashlib.sha256(source.read_bytes()).hexdigest(),
            "generation must never rewrite the immutable source",
        )
        for legacy in expected_legacy_outputs():
            self.assertFalse((root / legacy).exists(), legacy)

        master = root / "branding" / "NUKE-HOUR-1024.png"
        self.assert_rgb_png(master, 1024)
        with Image.open(master) as master_image:
            self.assertEqual(
                {"srgb": 0},
                master_image.info,
                "the output must declare sRGB without inheriting source metadata",
            )

        first_catalog = root / CATALOGS[0]
        second_catalog = root / CATALOGS[1]
        self.assertEqual(
            (first_catalog / "Contents.json").read_bytes(),
            (second_catalog / "Contents.json").read_bytes(),
        )
        contents = json.loads((first_catalog / "Contents.json").read_text())
        images = contents["images"]
        self.assertEqual(18, len(images))
        self.assertEqual(15, len({image["filename"] for image in images}))
        actual_slots = {
            (image["idiom"], image["size"], image["scale"]): (
                image["filename"],
                CATALOG_IMAGES[image["filename"]],
            )
            for image in images
        }
        self.assertEqual(CATALOG_SLOTS, actual_slots)
        for filename, pixels in CATALOG_IMAGES.items():
            personal = first_catalog / filename
            public = second_catalog / filename
            self.assert_rgb_png(personal, pixels)
            self.assertEqual(personal.read_bytes(), public.read_bytes(), filename)

        for size in PACKAGING_SIZES:
            path = root / "packaging" / "artwork" / f"icon_{size}x{size}.png"
            self.assert_rgb_png(path, size)
        self.assertEqual(
            master.read_bytes(),
            (root / "packaging" / "artwork" / "icon_1024x1024.png").read_bytes(),
        )

        mod_icon = root / "mods" / "ra2" / "icon.png"
        self.assert_rgb_png(mod_icon, 32)
        self.assertEqual(
            (root / "packaging" / "artwork" / "icon_32x32.png").read_bytes(),
            mod_icon.read_bytes(),
        )

        icns_bytes = [(root / path).read_bytes() for path in ICNS_OUTPUTS]
        self.assertTrue(icns_bytes[0])
        self.assertEqual([icns_bytes[0]] * len(icns_bytes), icns_bytes)
        for relative in ICNS_OUTPUTS:
            with Image.open(root / relative) as icon:
                self.assertEqual("ICNS", icon.format)
                self.assertEqual((1024, 1024), icon.size)
                icon.load()

    def test_check_fails_closed_for_tampered_missing_and_extra_outputs(self):
        temporary, root, source = self.temporary_root()
        self.addCleanup(temporary.cleanup)
        self.run_generator(root, "--source", source)
        before_check = {
            path.relative_to(root): (
                path.stat().st_mtime_ns,
                hashlib.sha256(path.read_bytes()).hexdigest(),
            )
            for path in root.rglob("*")
            if path.is_file()
        }
        self.run_generator(root, "--check")
        after_check = {
            path.relative_to(root): (
                path.stat().st_mtime_ns,
                hashlib.sha256(path.read_bytes()).hexdigest(),
            )
            for path in root.rglob("*")
            if path.is_file()
        }
        self.assertEqual(before_check, after_check, "--check must not write the root")

        tampered = root / "packaging" / "artwork" / "icon_24x24.png"
        tampered.write_bytes(tampered.read_bytes() + b"tampered")
        result = self.run_generator(root, "--check", check=False)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("differs", result.stderr)

        self.run_generator(root, "--source", source)
        missing = root / CATALOGS[0] / "NUKE-HOUR-29@3x.png"
        missing.unlink()
        result = self.run_generator(root, "--check", check=False)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("missing", result.stderr)

        self.run_generator(root, "--source", source)
        extra = root / CATALOGS[1] / "NUKE-HOUR-extra.png"
        extra.write_bytes(b"unexpected")
        result = self.run_generator(root, "--check", check=False)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("unexpected", result.stderr)


if __name__ == "__main__":
    unittest.main()
