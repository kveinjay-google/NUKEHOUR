#!/usr/bin/env python3
"""Prepare and audit the personal iOS loading-screen bundle resources."""

from __future__ import annotations

import argparse
import hashlib
import plistlib
import re
import struct
import sys
from pathlib import Path
from typing import Iterable


EXPECTED_IMAGE = "\tImage: ra2|uibits/NUCLEAR-CRISIS-BG-06.png"
EXPECTED_VISIBLE_NAME = "NUKE HOUR"
EXPECTED_INGAME_LAYOUT_PATHS = (
    "ra2|chrome/ingame-menu.yaml",
    "ra2|chrome/ingame-info.yaml",
)
LEGACY_INGAME_LAYOUT_PATHS = (
    "common|chrome/ingame-menu.yaml",
    "common|chrome/ingame-info.yaml",
)
INGAME_LAYOUT_RELATIVE_PATHS = (
    Path("chrome/ingame-menu.yaml"),
    Path("chrome/ingame-info.yaml"),
    Path("chrome/gamesave-loading.yaml"),
)
EXPECTED_GAMESAVE_LOADING_PATH = "ra2|chrome/gamesave-loading.yaml"
LEGACY_GAMESAVE_LOADING_PATH = "common|chrome/gamesave-loading.yaml"
WALLPAPER_RELATIVE_PATH = Path("uibits/NUCLEAR-CRISIS-BG-06.png")
CHROME_RELATIVE_PATH = Path("chrome.yaml")
LEGACY_RELATIVE_PATHS = tuple(
    Path("mods/ra2/uibits") / filename
    for filename in (
        "loadscreen.png",
        "menulogo.png",
        "menulogo-sm.png",
        "menutitle.png",
        "menutitle-sm.png",
    )
)
ACTIVE_METADATA_RELATIVE_PATHS = (
    Path("Info.plist"),
    Path("mods/ra2/mod.yaml"),
    Path("mods/ra2/fluent/mod.ftl"),
    Path("mods/ra2/fluent/zh-CN/mod.ftl"),
)
EXPECTED_WALLPAPER_SIZE = (2048, 1024)
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
LEGACY_LOGO_REFERENCE = re.compile(
    r"^[^\S\r\n]*ImageCollection[^\S\r\n]*:[^\S\r\n]*logos"
    r"(?:[^\S\r\n]*(?:#.*)?)$",
    re.MULTILINE,
)
LEGACY_LOGO_COLLECTION = re.compile(
    r"^[^\S\r\n]*logos[^\S\r\n]*:(?:[^\S\r\n]*(?:#.*)?)$",
    re.MULTILINE,
)
CHROME_LAYOUT_HEADER = re.compile(
    r"^ChromeLayout[^\S\r\n]*:",
    re.MULTILINE,
)
TOP_LEVEL_INCLUDE = re.compile(
    r"^Include[^\S\r\n]*:",
    re.MULTILINE,
)
OLD_VISIBLE_BRAND = re.compile(
    r"nuclear(?:[\s-]+)crisis|openrts\s+mobile|afghan(?:istan)?|阿富汗",
    re.IGNORECASE,
)
STABLE_WALLPAPER_NAME = re.compile(
    r"(?<![A-Za-z0-9_-])NUCLEAR-CRISIS-BG-0[1-9]\.png"
    r"(?![A-Za-z0-9_-])"
)
MOD_TITLE = re.compile(
    rf"^mod-title[\t ]*=[\t ]*{re.escape(EXPECTED_VISIBLE_NAME)}[\t ]*$",
    re.MULTILINE,
)


def _validate_app_bundle_path(app_bundle: Path) -> None:
    if app_bundle.suffix != ".app":
        raise ValueError(f"refusing to modify a non-app path: {app_bundle}")
    if app_bundle.is_symlink():
        raise ValueError(f"refusing to modify a symlinked app bundle: {app_bundle}")


def _bundle_member(app_bundle: Path, relative: Path | str) -> Path:
    candidate = app_bundle / relative
    root = app_bundle.resolve(strict=False)
    resolved_parent = candidate.parent.resolve(strict=False)
    try:
        resolved_parent.relative_to(root)
    except ValueError as error:
        raise ValueError(
            f"bundle member escapes through a symlink: {candidate}"
        ) from error
    if candidate.is_symlink():
        raise ValueError(f"refusing a symlinked bundle member: {candidate}")
    return candidate


def prepare_bundle(app_bundle: Path) -> bool:
    """Remove retired RA2 brand artwork from an incremental app bundle."""
    _validate_app_bundle_path(app_bundle)
    legacy_paths = tuple(
        _bundle_member(app_bundle, relative) for relative in LEGACY_RELATIVE_PATHS
    )
    for legacy in legacy_paths:
        if legacy.exists() and not legacy.is_file():
            raise ValueError(f"legacy brand path is not a regular file: {legacy}")

    removed = False
    for legacy in legacy_paths:
        if legacy.is_file():
            legacy.unlink()
            removed = True
    return removed


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _metadata_text(path: Path) -> str:
    if path.suffix == ".plist":
        with path.open("rb") as stream:
            return repr(plistlib.load(stream))
    return path.read_text(encoding="utf-8")


def _png_dimensions(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if (
        len(header) != 24
        or header[:8] != PNG_SIGNATURE
        or header[8:12] != struct.pack(">I", 13)
        or header[12:16] != b"IHDR"
    ):
        raise ValueError("missing PNG signature or IHDR header")
    return struct.unpack(">II", header[16:24])


def _manifest_list_entries(manifest_text: str, section: str) -> tuple[str, ...]:
    """Return active entries from one top-level MiniYaml list section."""
    candidates: list[tuple[int, str]] = []
    in_section = False
    section_header = f"{section}:"
    for line in manifest_text.splitlines():
        stripped = line.strip()
        if not in_section:
            if line == line.lstrip() and stripped == section_header:
                in_section = True
            continue

        if not stripped or stripped.startswith("#"):
            continue
        if line == line.lstrip():
            break

        prefix_length = len(line) - len(line.lstrip())
        indentation = len(line[:prefix_length].expandtabs(4))
        entry = line.split("#", 1)[0].strip()
        if entry:
            candidates.append((indentation, entry))

    if not candidates:
        return ()

    direct_indentation = min(indentation for indentation, _ in candidates)
    return tuple(
        entry for indentation, entry in candidates if indentation == direct_indentation
    )


def audit_bundle(app_bundle: Path, source_mod_root: Path) -> tuple[str, ...]:
    errors: list[str] = []
    try:
        _validate_app_bundle_path(app_bundle)
    except ValueError as error:
        return (str(error),)

    if not app_bundle.is_dir():
        return (f"app bundle is missing: {app_bundle}",)

    try:
        manifest = _bundle_member(app_bundle, "mods/ra2/mod.yaml")
        legacy_paths = tuple(
            _bundle_member(app_bundle, relative)
            for relative in LEGACY_RELATIVE_PATHS
        )
        bundled_wallpaper = _bundle_member(
            app_bundle, Path("mods/ra2") / WALLPAPER_RELATIVE_PATH
        )
        bundled_chrome = _bundle_member(
            app_bundle, Path("mods/ra2") / CHROME_RELATIVE_PATH
        )
        bundled_ingame_layouts = tuple(
            _bundle_member(app_bundle, Path("mods/ra2") / relative)
            for relative in INGAME_LAYOUT_RELATIVE_PATHS
        )
        metadata_paths = tuple(
            _bundle_member(app_bundle, relative)
            for relative in ACTIVE_METADATA_RELATIVE_PATHS
        )
    except ValueError as error:
        return (str(error),)

    if not manifest.is_file():
        errors.append(f"brand manifest is missing: {manifest}")
    else:
        manifest_text = manifest.read_text(encoding="utf-8")
        chrome_layouts = set(_manifest_list_entries(manifest_text, "ChromeLayout"))
        source_manifest = source_mod_root / "mod.yaml"
        if not source_manifest.is_file():
            errors.append(f"canonical source manifest is missing: {source_manifest}")
        elif _sha256(source_manifest) != _sha256(manifest):
            errors.append(
                "bundled brand manifest does not match the canonical source: "
                f"{manifest}"
            )
        if len(CHROME_LAYOUT_HEADER.findall(manifest_text)) != 1:
            errors.append("brand manifest must contain exactly one ChromeLayout section")
        if TOP_LEVEL_INCLUDE.search(manifest_text):
            errors.append(
                "brand manifest cannot use Include because it can override ChromeLayout"
            )
        if any(entry.startswith("-") or entry.endswith(":") for entry in chrome_layouts):
            errors.append(
                "brand manifest cannot use ChromeLayout merge syntax or nested values"
            )
        if EXPECTED_IMAGE not in manifest_text:
            errors.append(
                "brand manifest does not reference "
                "ra2|uibits/NUCLEAR-CRISIS-BG-06.png"
            )
        if "uibits/loadscreen.png" in manifest_text:
            errors.append("brand manifest still references legacy loadscreen.png")
        if any(path not in chrome_layouts for path in EXPECTED_INGAME_LAYOUT_PATHS):
            errors.append(
                "brand manifest does not activate the RA2 in-game menu and info layouts"
            )
        if any(path in chrome_layouts for path in LEGACY_INGAME_LAYOUT_PATHS):
            errors.append(
                "brand manifest still activates the legacy common in-game menu or info layout"
            )
        if EXPECTED_GAMESAVE_LOADING_PATH not in chrome_layouts:
            errors.append(
                "brand manifest does not activate the RA2 save-game loading layout"
            )
        if LEGACY_GAMESAVE_LOADING_PATH in chrome_layouts:
            errors.append(
                "brand manifest still activates the legacy common save-game loading layout"
            )

    for legacy in legacy_paths:
        if legacy.exists():
            errors.append(f"legacy brand artwork remains in the app bundle: {legacy}")

    source_wallpaper = source_mod_root / WALLPAPER_RELATIVE_PATH
    if not source_wallpaper.is_file():
        errors.append(f"canonical source wallpaper is missing: {source_wallpaper}")
    if not bundled_wallpaper.is_file():
        errors.append(f"bundled loading wallpaper is missing: {bundled_wallpaper}")
    if source_wallpaper.is_file() and bundled_wallpaper.is_file():
        if _sha256(source_wallpaper) != _sha256(bundled_wallpaper):
            errors.append(
                "bundled loading wallpaper does not match the canonical source: "
                f"{bundled_wallpaper}"
            )
        try:
            dimensions = _png_dimensions(bundled_wallpaper)
        except (OSError, ValueError) as error:
            errors.append(f"bundled loading wallpaper is not a valid PNG: {error}")
        else:
            if dimensions != EXPECTED_WALLPAPER_SIZE:
                errors.append(
                    "bundled loading wallpaper must be 2048x1024 POT, got "
                    f"{dimensions[0]}x{dimensions[1]}: {bundled_wallpaper}"
                )

    source_chrome = source_mod_root / CHROME_RELATIVE_PATH
    if not source_chrome.is_file():
        errors.append(f"canonical source chrome catalog is missing: {source_chrome}")
    if not bundled_chrome.is_file():
        errors.append(f"bundled chrome catalog is missing: {bundled_chrome}")
    if source_chrome.is_file() and bundled_chrome.is_file():
        if _sha256(source_chrome) != _sha256(bundled_chrome):
            errors.append(
                "bundled chrome catalog does not match the canonical source: "
                f"{bundled_chrome}"
            )
        try:
            chrome_text = bundled_chrome.read_text(encoding="utf-8")
        except (OSError, UnicodeError) as error:
            errors.append(f"unable to read bundled chrome catalog {bundled_chrome}: {error}")
        else:
            if LEGACY_LOGO_COLLECTION.search(chrome_text):
                errors.append(
                    "bundled chrome catalog still defines the removed legacy logos collection"
                )

    for relative, bundled_layout in zip(
        INGAME_LAYOUT_RELATIVE_PATHS, bundled_ingame_layouts
    ):
        source_layout = source_mod_root / relative
        if not source_layout.is_file():
            errors.append(f"canonical source in-game layout is missing: {source_layout}")
            continue
        if not bundled_layout.is_file():
            errors.append(f"bundled in-game layout is missing: {bundled_layout}")
            continue
        if _sha256(source_layout) != _sha256(bundled_layout):
            errors.append(
                "bundled in-game layout does not match the canonical source: "
                f"{bundled_layout}"
            )

    for bundled_layout in bundled_ingame_layouts:
        if not bundled_layout.is_file():
            continue
        try:
            layout_text = bundled_layout.read_text(encoding="utf-8")
        except (OSError, UnicodeError) as error:
            errors.append(f"unable to read bundled game layout {bundled_layout}: {error}")
        else:
            if LEGACY_LOGO_REFERENCE.search(layout_text):
                errors.append(
                    "bundled game layout still references the removed legacy logos collection: "
                    f"{bundled_layout}"
                )

    for path in metadata_paths:
        if not path.is_file():
            errors.append(f"active brand metadata is missing: {path}")
            continue
        try:
            text = _metadata_text(path)
        except (OSError, plistlib.InvalidFileException, UnicodeError) as error:
            errors.append(f"unable to read active brand metadata {path}: {error}")
            continue

        if path.name == "Info.plist":
            try:
                with path.open("rb") as stream:
                    info = plistlib.load(stream)
            except (OSError, plistlib.InvalidFileException) as error:
                errors.append(f"unable to validate visible app name {path}: {error}")
            else:
                for key in ("CFBundleDisplayName", "CFBundleName"):
                    if info.get(key) != EXPECTED_VISIBLE_NAME:
                        errors.append(
                            f"{key} must be {EXPECTED_VISIBLE_NAME!r}: {path}"
                        )

        if path.name == "mod.ftl":
            if len(MOD_TITLE.findall(text)) != 1:
                errors.append(
                    f"mod-title must equal {EXPECTED_VISIBLE_NAME!r} exactly once: {path}"
                )

        visible_text = STABLE_WALLPAPER_NAME.sub("", text)
        if OLD_VISIBLE_BRAND.search(visible_text):
            errors.append(f"old brand text remains in active metadata: {path}")

    return tuple(errors)


def main(argv: Iterable[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    prepare_parser = subparsers.add_parser("prepare")
    prepare_parser.add_argument("--app-bundle", type=Path, required=True)

    audit_parser = subparsers.add_parser("audit")
    audit_parser.add_argument("--app-bundle", type=Path, required=True)
    audit_parser.add_argument("--source-mod-root", type=Path, required=True)

    arguments = parser.parse_args(argv)
    if arguments.command == "prepare":
        try:
            removed = prepare_bundle(arguments.app_bundle)
        except (OSError, ValueError) as error:
            print(error, file=sys.stderr)
            return 2
        print(
            "Removed legacy RA2 loading artwork."
            if removed
            else "No legacy RA2 loading artwork found."
        )
        return 0

    errors = audit_bundle(arguments.app_bundle, arguments.source_mod_root)
    for error in errors:
        print(error, file=sys.stderr)
    if errors:
        return 2
    print("Personal iOS loading-brand audit passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
