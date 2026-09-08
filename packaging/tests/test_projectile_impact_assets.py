import ast
import hashlib
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
GENERATOR = ROOT / "packaging" / "generate_projectile_impact_assets.py"
ASSET_DIR = ROOT / "mods" / "ra2" / "bits" / "animations"
SEQUENCES = ROOT / "mods" / "ra2" / "sequences" / "misc.yaml"
PROVENANCE = ROOT / "docs" / "legal" / "projectile-impact-assets-provenance.md"
TRACKED_LOADER_TEST = ROOT / "engine" / "OpenRA.Test" / "PublicContentSafetyPolicyTest.cs"

OUTPUTS = {
    "nc-impact-core.png": 16,
    "nc-impact-ring.png": 16,
    "nc-impact-debris.png": 8,
}
FRAME_SIZE = (128, 128)
FRAME_GUTTER = 6
SEQUENCE_CONTRACT = {
    "nc_core_small": ("nc-impact-core.png", 0, 0.68, "Additive"),
    "nc_core_large": ("nc-impact-core.png", 0, 1.0, "Additive"),
    "nc_core_tesla": ("nc-impact-core.png", 8, 1.0, "Additive"),
    "nc_ring_small": ("nc-impact-ring.png", 0, 0.72, "Additive"),
    "nc_ring_large": ("nc-impact-ring.png", 0, 1.0, "Additive"),
    "nc_ring_tesla": ("nc-impact-ring.png", 8, 1.0, "Additive"),
    "nc_debris_small": ("nc-impact-debris.png", 0, 0.72, "Alpha"),
    "nc_debris_large": ("nc-impact-debris.png", 0, 1.0, "Alpha"),
}


def is_power_of_two(value: int) -> bool:
    return value > 0 and value & (value - 1) == 0


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def qualified_name(node: ast.AST) -> str:
    if isinstance(node, ast.Name):
        return node.id
    if isinstance(node, ast.Attribute):
        prefix = qualified_name(node.value)
        return f"{prefix}.{node.attr}" if prefix else node.attr
    return ""


def call_signature(node: ast.AST) -> str:
    if isinstance(node, ast.Name):
        return node.id
    if isinstance(node, ast.Attribute):
        receiver = expression_signature(node.value)
        return f"{receiver}.{node.attr}" if receiver else ""
    return ""


def expression_signature(node: ast.AST) -> str:
    if isinstance(node, ast.Name):
        return node.id
    if isinstance(node, ast.Attribute):
        receiver = expression_signature(node.value)
        return f"{receiver}.{node.attr}" if receiver else ""
    if isinstance(node, ast.Call):
        callee = call_signature(node.func)
        return f"{callee}()" if callee else ""
    return ""


def safe_call_shape(signature: str, node: ast.Call) -> bool:
    if signature == "argparse.ArgumentParser":
        return (
            not node.args
            and len(node.keywords) == 1
            and node.keywords[0].arg == "description"
            and isinstance(node.keywords[0].value, ast.Name)
            and node.keywords[0].value.id == "__doc__"
        )

    if signature == "parser.add_argument":
        keywords = {keyword.arg: keyword.value for keyword in node.keywords if keyword.arg}
        return (
            len(node.args) == 1
            and isinstance(node.args[0], ast.Constant)
            and node.args[0].value == "--output-dir"
            and len(keywords) == 3
            and isinstance(keywords.get("type"), ast.Name)
            and keywords["type"].id == "Path"
            and isinstance(keywords.get("default"), ast.Name)
            and keywords["default"].id == "DEFAULT_OUTPUT_DIR"
            and isinstance(keywords.get("help"), ast.Constant)
            and isinstance(keywords["help"].value, str)
        )

    if signature == "parser.parse_args":
        return not node.args and not node.keywords

    return True


def generator_policy_violations(source: str):
    tree = ast.parse(source)
    allowed_plain_imports = {"argparse", "math", "random"}
    allowed_from_imports = {
        "pathlib": {"Path"},
        "PIL": {"Image", "ImageDraw", "ImageFilter", "PngImagePlugin"},
    }
    forbidden_names = {
        "__import__",
        "compile",
        "eval",
        "exec",
        "getattr",
        "globals",
        "input",
        "locals",
        "open",
        "setattr",
        "vars",
    }
    forbidden_attributes = {
        "__dict__",
        "call",
        "check_call",
        "check_output",
        "copy",
        "copy2",
        "copyfile",
        "extract",
        "extractall",
        "frombuffer",
        "frombytes",
        "fromfile",
        "grab",
        "imdecode",
        "imread",
        "load",
        "multiline_text",
        "open",
        "popen",
        "post",
        "read",
        "read_bytes",
        "read_text",
        "request",
        "run",
        "system",
        "text",
        "textbbox",
        "textlength",
        "unpack_archive",
        "urlopen",
        "urlretrieve",
    }
    allowed_calls = {
        "Image.alpha_composite",
        "Image.new",
        "ImageDraw.Draw",
        "ImageFilter.GaussianBlur",
        "Path",
        "Path().resolve",
        "PngImagePlugin.PngInfo",
        "argparse.ArgumentParser",
        "composite_disc",
        "core_frame",
        "debris_frame",
        "draw.ellipse",
        "draw.line",
        "draw.rectangle",
        "enumerate",
        "finish_frame",
        "frame.resize",
        "generate",
        "glow.filter",
        "glow_draw.ellipse",
        "layer.filter",
        "len",
        "main",
        "make_sheet",
        "make_sheet().save",
        "math.cos",
        "math.sin",
        "max",
        "metadata.add_text",
        "min",
        "output_dir.mkdir",
        "parser.add_argument",
        "parser.parse_args",
        "particle_parameters",
        "random.Random",
        "range",
        "rgba",
        "ring_draw.ellipse",
        "ring_frame",
        "rng.randrange",
        "rng.uniform",
        "round",
        "save_sheet",
        "scaled",
        "sheet.alpha_composite",
        "smoke.filter",
        "spark_draw.line",
        "str",
        "transparent_frame",
    }
    allowed_attribute_loads = {
        "Image.Resampling",
        "Image.Resampling.LANCZOS",
        "Image.alpha_composite",
        "Image.new",
        "ImageDraw.Draw",
        "ImageFilter.GaussianBlur",
        "Path().resolve",
        "Path().resolve().parents",
        "PngImagePlugin.PngInfo",
        "argparse.ArgumentParser",
        "args.output_dir",
        "draw.ellipse",
        "draw.line",
        "draw.rectangle",
        "frame.resize",
        "glow.filter",
        "glow_draw.ellipse",
        "layer.filter",
        "make_sheet().save",
        "math.cos",
        "math.pi",
        "math.sin",
        "math.tau",
        "metadata.add_text",
        "output_dir.mkdir",
        "parser.add_argument",
        "parser.parse_args",
        "random.Random",
        "ring_draw.ellipse",
        "rng.randrange",
        "rng.uniform",
        "sheet.alpha_composite",
        "smoke.filter",
        "spark_draw.line",
    }
    protected_callable_names = {
        signature for signature in allowed_calls if "." not in signature and "()" not in signature
    }

    violations = []
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            for alias in node.names:
                if alias.name not in allowed_plain_imports:
                    violations.append((node.lineno, f"import:{alias.name}"))
        elif isinstance(node, ast.ImportFrom) and node.module:
            allowed_names = allowed_from_imports.get(node.module, set())
            for alias in node.names:
                if node.level != 0 or alias.name not in allowed_names:
                    violations.append((node.lineno, f"import:{node.module}.{alias.name}"))
        elif isinstance(node, ast.Name) and isinstance(node.ctx, ast.Load):
            if node.id in forbidden_names:
                violations.append((node.lineno, f"name:{node.id}"))
        elif isinstance(node, ast.Attribute) and isinstance(node.ctx, ast.Load):
            signature = expression_signature(node)
            if (node.attr.startswith("__") or node.attr in forbidden_attributes or
                    signature not in allowed_attribute_loads):
                violations.append((node.lineno, f"attribute:{signature or qualified_name(node)}"))
        elif isinstance(node, ast.Attribute):
            violations.append((node.lineno, f"attribute-write:{expression_signature(node)}"))
        elif isinstance(node, ast.Name) and isinstance(node.ctx, ast.Store):
            if node.id in protected_callable_names:
                violations.append((node.lineno, f"rebind:{node.id}"))
        elif isinstance(node, ast.Call):
            signature = call_signature(node.func)
            if signature not in allowed_calls or not safe_call_shape(signature, node):
                violations.append((node.lineno, f"call:{signature or ast.dump(node.func)}"))

    return violations


def parse_sequence_block(source: str) -> dict[str, dict[str, str]]:
    lines = source.splitlines()
    start = lines.index("nc-impact:") + 1
    block = []
    for line in lines[start:]:
        if line and not line.startswith("\t"):
            break
        block.append(line)

    sequences = {}
    current = None
    for line in block:
        match = re.fullmatch(r"\t([a-z0-9_-]+):", line)
        if match:
            current = match.group(1)
            sequences[current] = {}
            continue

        match = re.fullmatch(r"\t\t([A-Za-z]+):\s*(.+)", line)
        if match and current:
            sequences[current][match.group(1)] = match.group(2)

    return sequences


class ProjectileImpactAssetTest(unittest.TestCase):
    maxDiff = None

    def run_generator(self, output_dir: Path):
        self.assertTrue(GENERATOR.is_file(), f"missing generator: {GENERATOR}")
        result = subprocess.run(
            [sys.executable, str(GENERATOR), "--output-dir", str(output_dir)],
            cwd=ROOT,
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertEqual(0, result.returncode, result.stderr or result.stdout)

    def test_generator_cli_needs_no_source_assets(self):
        self.assertTrue(GENERATOR.is_file(), f"missing generator: {GENERATOR}")
        result = subprocess.run(
            [sys.executable, str(GENERATOR), "--help"],
            cwd=ROOT,
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("--output-dir", result.stdout)
        self.assertNotRegex(result.stdout.lower(), r"--(?:input|source|reference|palette)")

    def test_generator_source_rejects_external_raster_and_text_dependencies(self):
        self.assertTrue(GENERATOR.is_file(), f"missing generator: {GENERATOR}")
        source = GENERATOR.read_text(encoding="utf-8")
        self.assertNotIn("ImageFont", source)
        self.assertEqual([], generator_policy_violations(source))

    def test_source_gate_catches_file_process_download_and_extraction_bypasses(self):
        bypasses = {
            "os-system": "import os\nos.system('curl example.invalid/asset.png')\n",
            "os-popen": "import os\nos.popen('unzip retail.zip')\n",
            "zip-extract": "import zipfile\nzipfile.ZipFile('retail.zip').extractall('.')\n",
            "tar-extract": "import tarfile\ntarfile.open('retail.tar').extractall('.')\n",
            "opencv-read": "import cv2\ncv2.imread('retail.png')\n",
            "numpy-read": "import numpy as np\nnp.fromfile('retail.bin')\n",
            "dynamic-import": "module = __import__('urllib.request')\nmodule.urlopen('https://example.invalid')\n",
            "aliased-image-read": "read_raster = Image.open\nread_raster('retail.png')\n",
            "aliased-path-read": "read_asset = Path.read_bytes\nread_asset(Path('retail.bin'))\n",
            "aliased-dynamic-process": (
                "imp = __import__\nos = imp('os')\nrunner = os.system\n"
                "runner('curl example.invalid/asset.png')\n"
            ),
            "screen-capture": "from PIL import ImageGrab\nImageGrab.grab()\n",
            "argparse-filetype-read": (
                "import argparse\nstream = argparse.FileType('rb')('retail.png')\n"
                "data = b''.join(stream)\n"
            ),
            "png-plugin-reader": (
                "from PIL import PngImagePlugin\nimport argparse\n"
                "stream = argparse.FileType('rb')('retail.png')\n"
                "image = PngImagePlugin.PngImageFile(stream)\nimage.getpixel((0, 0))\n"
            ),
            "dunder-path-read": (
                "from pathlib import Path\n"
                "reader = object.__getattribute__(Path('retail.bin'), 'read_bytes')\n"
                "data = reader()\n"
            ),
            "rebound-filetype-call": (
                "import argparse\ngenerate = argparse.FileType\nrgba = generate('rb')\n"
                "stream = rgba('retail.png')\ndata = [chunk for chunk in stream]\n"
            ),
            "rebound-png-reader-call": (
                "from PIL import PngImagePlugin\n"
                "ring_frame = PngImagePlugin.PngImageFile\n"
                "image = ring_frame('retail.png')\n"
            ),
            "argparse-response-file": (
                "import argparse\n"
                "parser = argparse.ArgumentParser(fromfile_prefix_chars='@')\n"
                "parser.add_argument('data')\n"
                "args = parser.parse_args(['@retail-palette.txt'])\n"
            ),
            "argparse-response-file-monkeypatch": (
                "import argparse\nfrom pathlib import Path\n"
                "parser = argparse.ArgumentParser(description=__doc__)\n"
                "parser.fromfile_prefix_chars = '@'\n"
                "parser.add_argument('--output-dir', type=Path, "
                "default=DEFAULT_OUTPUT_DIR, help='x')\n"
                "args = parser.parse_args()\n"
            ),
        }
        for name, source in bypasses.items():
            with self.subTest(name=name):
                self.assertTrue(generator_policy_violations(source))

    def test_real_loader_regression_lives_in_a_tracked_test_source(self):
        source = TRACKED_LOADER_TEST.read_text(encoding="utf-8")
        self.assertIn("PngSheetLoader", source)
        self.assertIn("RealPngSheetLoaderSlicesGeneratedFrames", source)
        self.assertIn("File.Exists", source)
        for filename in OUTPUTS:
            self.assertIn(filename, source)

    def test_isolated_generations_are_byte_identical_and_exactly_scoped(self):
        with tempfile.TemporaryDirectory() as first_dir, tempfile.TemporaryDirectory() as second_dir:
            first = Path(first_dir)
            second = Path(second_dir)
            self.run_generator(first)
            self.run_generator(second)

            self.assertEqual(sorted(OUTPUTS), sorted(path.name for path in first.iterdir()))
            self.assertEqual(sorted(OUTPUTS), sorted(path.name for path in second.iterdir()))
            for filename in OUTPUTS:
                with self.subTest(filename=filename):
                    self.assertEqual(first.joinpath(filename).read_bytes(), second.joinpath(filename).read_bytes())

    def test_generated_sheets_have_bounded_rgba_pot_geometry_and_text_metadata(self):
        with tempfile.TemporaryDirectory() as output_dir:
            output = Path(output_dir)
            self.run_generator(output)
            for filename, frame_amount in OUTPUTS.items():
                with self.subTest(filename=filename), Image.open(output / filename) as image:
                    self.assertEqual("RGBA", image.mode)
                    self.assertTrue(is_power_of_two(image.width))
                    self.assertTrue(is_power_of_two(image.height))
                    self.assertLessEqual(image.width, 1024)
                    self.assertLessEqual(image.height, 256)
                    expected_size = (1024, 128 if frame_amount == 8 else 256)
                    self.assertEqual(expected_size, image.size)
                    self.assertEqual(0, image.width % FRAME_SIZE[0])
                    self.assertEqual(0, image.height % FRAME_SIZE[1])
                    self.assertEqual("128,128", image.text.get("FrameSize"))
                    self.assertEqual(str(frame_amount), image.text.get("FrameAmount"))
                    self.assertIn(b"tEXtFrameSize\x00", (output / filename).read_bytes())
                    self.assertIn(b"tEXtFrameAmount\x00", (output / filename).read_bytes())

    def test_each_declared_frame_is_nonempty_and_has_a_transparent_gutter(self):
        with tempfile.TemporaryDirectory() as output_dir:
            output = Path(output_dir)
            self.run_generator(output)
            frame_width, frame_height = FRAME_SIZE
            for filename, frame_amount in OUTPUTS.items():
                with Image.open(output / filename) as image:
                    alpha = image.getchannel("A")
                    frames_per_row = image.width // frame_width
                    for index in range(frame_amount):
                        x = index % frames_per_row * frame_width
                        y = index // frames_per_row * frame_height
                        frame = alpha.crop((x, y, x + frame_width, y + frame_height))
                        interior = frame.crop(
                            (
                                FRAME_GUTTER,
                                FRAME_GUTTER,
                                frame_width - FRAME_GUTTER,
                                frame_height - FRAME_GUTTER,
                            )
                        )
                        borders = (
                            frame.crop((0, 0, frame_width, FRAME_GUTTER)),
                            frame.crop((0, frame_height - FRAME_GUTTER, frame_width, frame_height)),
                            frame.crop((0, 0, FRAME_GUTTER, frame_height)),
                            frame.crop((frame_width - FRAME_GUTTER, 0, frame_width, frame_height)),
                        )
                        with self.subTest(filename=filename, frame=index):
                            self.assertGreater(interior.getextrema()[1], 0)
                            self.assertTrue(all(border.getbbox() is None for border in borders))

    def test_sequence_contract_uses_only_generated_sheets(self):
        source = SEQUENCES.read_text(encoding="utf-8")
        self.assertIn("nc-impact:\n", source)
        sequences = parse_sequence_block(source)
        self.assertEqual(set(SEQUENCE_CONTRACT), set(sequences))

        allowed_filenames = {f"bits/animations/{name}" for name in OUTPUTS}
        for name, (filename, start, scale, blend_mode) in SEQUENCE_CONTRACT.items():
            with self.subTest(sequence=name):
                values = sequences[name]
                self.assertEqual(f"bits/animations/{filename}", values.get("Filename"))
                self.assertIn(values["Filename"], allowed_filenames)
                self.assertEqual(str(start), values.get("Start", "0"))
                self.assertEqual("8", values.get("Length"))
                self.assertEqual("55", values.get("Tick"))
                self.assertAlmostEqual(scale, float(values.get("Scale", "1")))
                self.assertEqual(blend_mode, values.get("BlendMode"))

    def test_repository_assets_match_a_fresh_generation(self):
        with tempfile.TemporaryDirectory() as output_dir:
            output = Path(output_dir)
            self.run_generator(output)
            for filename in OUTPUTS:
                installed = ASSET_DIR / filename
                with self.subTest(filename=filename):
                    self.assertTrue(installed.is_file(), f"missing generated asset: {installed}")
                    self.assertEqual(sha256(output / filename), sha256(installed))

    def test_clean_room_provenance_names_every_output_and_regeneration_command(self):
        self.assertTrue(PROVENANCE.is_file(), f"missing provenance: {PROVENANCE}")
        source = PROVENANCE.read_text(encoding="utf-8")
        lowered = source.lower()
        self.assertIn("clean-room", lowered)
        self.assertIn("project original", lowered)
        self.assertIn("python3 packaging/generate_projectile_impact_assets.py", source)
        self.assertIn("no external raster", lowered)
        for filename in OUTPUTS:
            self.assertIn(filename, source)


if __name__ == "__main__":
    unittest.main()
