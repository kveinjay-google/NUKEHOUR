#!/usr/bin/env python3
"""Generate and verify every NUKE HOUR application icon deterministically."""

import argparse
import hashlib
import io
import json
import subprocess
import sys
import tempfile
from pathlib import Path

import PIL
from PIL import Image, PngImagePlugin


REQUIRED_PILLOW_VERSION = "11.3.0"
SOURCE_RELATIVE_PATH = Path("branding/NUKE-HOUR-source-1254.png")
SOURCE_SHA256 = "af3a739efcd5eefd02c0a622b60dcb6a88cd52b224813f53df8284b60a19e99b"
MASTER_RELATIVE_PATH = Path("branding/NUKE-HOUR-1024.png")

CATALOG_DIRECTORIES = (
    Path("ios/OpenRA.iOS/Assets.xcassets/AppIcon.appiconset"),
    Path("ios/OpenRA.iOS/Assets.xcassets/PublicAppIcon.appiconset"),
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
CATALOG_SLOTS = (
    ("NUKE-HOUR-20@2x.png", "iphone", "2x", "20x20"),
    ("NUKE-HOUR-20@3x.png", "iphone", "3x", "20x20"),
    ("NUKE-HOUR-29@2x.png", "iphone", "2x", "29x29"),
    ("NUKE-HOUR-29@3x.png", "iphone", "3x", "29x29"),
    ("NUKE-HOUR-40@2x.png", "iphone", "2x", "40x40"),
    ("NUKE-HOUR-40@3x.png", "iphone", "3x", "40x40"),
    ("NUKE-HOUR-60@2x.png", "iphone", "2x", "60x60"),
    ("NUKE-HOUR-60@3x.png", "iphone", "3x", "60x60"),
    ("NUKE-HOUR-20.png", "ipad", "1x", "20x20"),
    ("NUKE-HOUR-20@2x.png", "ipad", "2x", "20x20"),
    ("NUKE-HOUR-29.png", "ipad", "1x", "29x29"),
    ("NUKE-HOUR-29@2x.png", "ipad", "2x", "29x29"),
    ("NUKE-HOUR-40.png", "ipad", "1x", "40x40"),
    ("NUKE-HOUR-40@2x.png", "ipad", "2x", "40x40"),
    ("NUKE-HOUR-76.png", "ipad", "1x", "76x76"),
    ("NUKE-HOUR-76@2x.png", "ipad", "2x", "76x76"),
    ("NUKE-HOUR-83.5@2x.png", "ipad", "2x", "83.5x83.5"),
    ("NUKE-HOUR-1024.png", "ios-marketing", "1x", "1024x1024"),
)
PACKAGING_SIZES = (16, 24, 32, 48, 64, 128, 256, 512, 1024)
ICNS_OUTPUTS = (
    Path("launcher_assets/icon.icns"),
    Path("NUKE HOUR.app/Contents/Resources/NUKE-HOUR.icns"),
    Path("NUKE HOUR GAME.app/Contents/Resources/NUKE-HOUR.icns"),
)
ICONSET_IMAGES = {
    "icon_16x16.png": 16,
    "icon_16x16@2x.png": 32,
    "icon_32x32.png": 32,
    "icon_32x32@2x.png": 64,
    "icon_128x128.png": 128,
    "icon_128x128@2x.png": 256,
    "icon_256x256.png": 256,
    "icon_256x256@2x.png": 512,
    "icon_512x512.png": 512,
    "icon_512x512@2x.png": 1024,
}


class IconPipelineError(RuntimeError):
    pass


def output_paths():
    outputs = {
        MASTER_RELATIVE_PATH,
        Path("mods/ra2/icon.png"),
        *ICNS_OUTPUTS,
    }
    for catalog in CATALOG_DIRECTORIES:
        outputs.add(catalog / "Contents.json")
        outputs.update(catalog / filename for filename in CATALOG_IMAGES)
    outputs.update(
        Path(f"packaging/artwork/icon_{size}x{size}.png")
        for size in PACKAGING_SIZES
    )
    return tuple(sorted(outputs, key=lambda path: path.as_posix()))


def legacy_paths():
    suffixes = [name.removeprefix("NUKE-HOUR-") for name in CATALOG_IMAGES]
    legacy = {
        *(CATALOG_DIRECTORIES[0] / f"NUCLEAR-CRISIS-{suffix}" for suffix in suffixes),
        *(CATALOG_DIRECTORIES[1] / f"AppIcon-{suffix}" for suffix in suffixes),
    }
    return tuple(sorted(legacy, key=lambda path: path.as_posix()))


def mutation_paths():
    return tuple(
        sorted(set(output_paths()) | set(legacy_paths()), key=lambda path: path.as_posix())
    )


def validate_output_allowlist():
    catalog_allowed = {
        catalog / "Contents.json" for catalog in CATALOG_DIRECTORIES
    } | {
        catalog / filename
        for catalog in CATALOG_DIRECTORIES
        for filename in CATALOG_IMAGES
    }
    packaging_allowed = {
        Path(f"packaging/artwork/icon_{size}x{size}.png")
        for size in PACKAGING_SIZES
    }
    exact_allowed = {
        MASTER_RELATIVE_PATH,
        Path("mods/ra2/icon.png"),
        *ICNS_OUTPUTS,
    }
    approved = catalog_allowed | packaging_allowed | exact_allowed
    unexpected = set(output_paths()) - approved
    if unexpected:
        raise IconPipelineError(
            "generator output escaped its approved allowlist: "
            + ", ".join(sorted(path.as_posix() for path in unexpected))
        )


def validate_managed_path_safety(root):
    requested_root = Path(root)
    if requested_root.is_symlink():
        raise IconPipelineError(f"repository root must not be a symlink: {requested_root}")

    root = requested_root.resolve()
    if root.exists() and not root.is_dir():
        raise IconPipelineError(f"repository root is not a directory: {root}")

    for relative in mutation_paths():
        if relative.is_absolute() or ".." in relative.parts:
            raise IconPipelineError(f"managed path escapes the repository root: {relative}")

        current = root
        for index, part in enumerate(relative.parts):
            current /= part
            if current.is_symlink():
                raise IconPipelineError(
                    f"managed path contains a symlink: {current.relative_to(root)}"
                )
            if current.exists():
                is_destination = index == len(relative.parts) - 1
                if (not is_destination and not current.is_dir()) or (
                    is_destination and not current.is_file()
                ):
                    raise IconPipelineError(
                        f"managed path has an incompatible file type: {current.relative_to(root)}"
                    )


def require_pillow_version():
    if PIL.__version__ != REQUIRED_PILLOW_VERSION:
        raise IconPipelineError(
            f"Pillow {REQUIRED_PILLOW_VERSION} is required; found {PIL.__version__}"
        )


def validate_source(source):
    source = Path(source)
    if not source.is_file():
        raise IconPipelineError(f"source is missing: {source}")
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    if digest != SOURCE_SHA256:
        raise IconPipelineError(
            f"source SHA-256 differs: expected {SOURCE_SHA256}, found {digest}"
        )

    try:
        with Image.open(source) as image:
            image.load()
            if image.format != "PNG" or image.size != (1254, 1254) or image.mode != "RGB":
                raise IconPipelineError(
                    "source contract differs: expected 1254x1254 opaque RGB PNG, "
                    f"found {image.format} {image.size[0]}x{image.size[1]} {image.mode}"
                )
    except IconPipelineError:
        raise
    except Exception as error:
        raise IconPipelineError(f"source is not a readable PNG: {source}: {error}") from error


def png_bytes(image):
    image.info.clear()
    png_info = PngImagePlugin.PngInfo()
    png_info.add(b"sRGB", b"\0")
    output = io.BytesIO()
    image.save(
        output,
        format="PNG",
        optimize=False,
        compress_level=9,
        pnginfo=png_info,
    )
    return output.getvalue()


def derived_pngs(master):
    images = {}
    for size in sorted(
        set(CATALOG_IMAGES.values()) | set(PACKAGING_SIZES) | set(ICONSET_IMAGES.values())
    ):
        resized = master.resize((size, size), Image.Resampling.LANCZOS)
        resized.info.clear()
        images[size] = png_bytes(resized)
    return images


def write_bytes(root, relative, data):
    destination = root / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(data)


def catalog_contents():
    document = {
        "images": [
            {
                "filename": filename,
                "idiom": idiom,
                "scale": scale,
                "size": size,
            }
            for filename, idiom, scale, size in CATALOG_SLOTS
        ],
        "info": {"author": "openrts", "version": 1},
    }
    return (json.dumps(document, indent=2, ensure_ascii=False) + "\n").encode("utf-8")


def build_icns(derived):
    with tempfile.TemporaryDirectory(prefix="nuke-hour-iconset-") as temporary:
        temporary = Path(temporary)
        iconset = temporary / "NUKE-HOUR.iconset"
        iconset.mkdir()
        for filename, size in ICONSET_IMAGES.items():
            (iconset / filename).write_bytes(derived[size])

        destination = temporary / "NUKE-HOUR.icns"
        try:
            result = subprocess.run(
                [
                    "/usr/bin/iconutil",
                    "--convert",
                    "icns",
                    "--output",
                    str(destination),
                    str(iconset),
                ],
                check=False,
                capture_output=True,
                text=True,
            )
        except OSError as error:
            raise IconPipelineError(f"could not run iconutil: {error}") from error
        if result.returncode:
            detail = result.stderr.strip() or result.stdout.strip() or "unknown error"
            raise IconPipelineError(f"iconutil failed: {detail}")
        if not destination.is_file():
            raise IconPipelineError("iconutil succeeded without creating an ICNS file")
        return destination.read_bytes()


def generate(root, source):
    validate_managed_path_safety(root)
    root = Path(root).resolve()
    validate_source(source)

    with Image.open(source) as source_image:
        source_image.load()
        normalized = source_image.convert("RGB")
        master = normalized.resize((1024, 1024), Image.Resampling.LANCZOS)
        master.info.clear()

    master_bytes = png_bytes(master)
    derived = derived_pngs(master)
    icns = build_icns(derived)

    write_bytes(root, MASTER_RELATIVE_PATH, master_bytes)

    contents = catalog_contents()
    for catalog in CATALOG_DIRECTORIES:
        for legacy in legacy_paths():
            if legacy.parent == catalog:
                legacy_path = root / legacy
                if legacy_path.is_file():
                    legacy_path.unlink()
        write_bytes(root, catalog / "Contents.json", contents)
        for filename, size in CATALOG_IMAGES.items():
            write_bytes(root, catalog / filename, derived[size])

    for size in PACKAGING_SIZES:
        write_bytes(
            root,
            Path(f"packaging/artwork/icon_{size}x{size}.png"),
            derived[size],
        )
    write_bytes(root, Path("mods/ra2/icon.png"), derived[32])

    for relative in ICNS_OUTPUTS:
        write_bytes(root, relative, icns)


def managed_existing_paths(root):
    root = Path(root)
    existing = set()
    for catalog in CATALOG_DIRECTORIES:
        directory = root / catalog
        if directory.is_dir():
            for path in directory.iterdir():
                if path.is_symlink():
                    raise IconPipelineError(
                        f"managed catalog contains a symlink: {path.relative_to(root)}"
                    )
                if path.is_file():
                    existing.add(path.relative_to(root))

    artwork = root / "packaging" / "artwork"
    if artwork.is_dir():
        for path in artwork.glob("icon_*.png"):
            if path.is_symlink():
                raise IconPipelineError(
                    f"managed artwork contains a symlink: {path.relative_to(root)}"
                )
            if path.is_file():
                existing.add(path.relative_to(root))

    for relative in (MASTER_RELATIVE_PATH, Path("mods/ra2/icon.png"), *ICNS_OUTPUTS):
        if (root / relative).is_file():
            existing.add(relative)
    return existing


def check(root):
    validate_managed_path_safety(root)
    root = Path(root).resolve()
    source = root / SOURCE_RELATIVE_PATH
    validate_source(source)
    with tempfile.TemporaryDirectory(prefix="nuke-hour-icon-check-") as temporary:
        candidate_root = Path(temporary)
        generate(candidate_root, source)

        expected = set(output_paths())
        actual = managed_existing_paths(root)
        missing = sorted(expected - actual, key=lambda path: path.as_posix())
        unexpected = sorted(actual - expected, key=lambda path: path.as_posix())
        differs = sorted(
            (
                relative
                for relative in expected & actual
                if (root / relative).read_bytes()
                != (candidate_root / relative).read_bytes()
            ),
            key=lambda path: path.as_posix(),
        )

    if missing or unexpected or differs:
        lines = ["NUKE HOUR icon check failed:"]
        lines.extend(f"missing: {path.as_posix()}" for path in missing)
        lines.extend(f"unexpected: {path.as_posix()}" for path in unexpected)
        lines.extend(f"differs: {path.as_posix()}" for path in differs)
        raise IconPipelineError("\n".join(lines))


def parse_arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", required=True, type=Path)
    action = parser.add_mutually_exclusive_group(required=True)
    action.add_argument("--source", type=Path)
    action.add_argument("--check", action="store_true")
    action.add_argument("--list-outputs", action="store_true")
    action.add_argument("--list-mutations", action="store_true")
    return parser.parse_args()


def main():
    arguments = parse_arguments()
    try:
        require_pillow_version()
        validate_output_allowlist()
        if arguments.list_outputs:
            print("\n".join(path.as_posix() for path in output_paths()))
        elif arguments.list_mutations:
            print("\n".join(path.as_posix() for path in mutation_paths()))
        elif arguments.check:
            check(arguments.root)
        else:
            generate(arguments.root, arguments.source)
    except IconPipelineError as error:
        print(error, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
