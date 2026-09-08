#!/usr/bin/env python3
"""Fail-closed, read-only audit for the branded iOS application bundle."""

from __future__ import annotations

import argparse
import json
import plistlib
import re
import subprocess
import sys
import tempfile
from decimal import Decimal, InvalidOperation
from pathlib import Path
from typing import Iterable, Sequence

from PIL import Image, ImageChops


EXPECTED_NAME = "NUKE HOUR"
EXPECTED_BUNDLE_IDENTIFIER = "com.openra.ipad.personal"
EXPECTED_DEVELOPMENT_REGION = "en"
EXPECTED_LOCALIZATIONS = ["en", "zh-Hans"]
EXPECTED_LOCALIZATION_DIRECTORIES = {"en.lproj", "zh-Hans.lproj"}
EXPECTED_PERMISSION_BYTES = {
    "en.lproj": (
        b'"NSLocalNetworkUsageDescription" = "Used to discover and connect to '
        b'multiplayer games on the same local network, personal hotspot, or nearby devices.";\n'
    ),
    "zh-Hans.lproj": (
        '"NSLocalNetworkUsageDescription" = '
        '"用于发现并连接同一局域网、个人热点或附近设备上的多人游戏。";\n'
    ).encode("utf-8"),
}
EXPECTED_FALLBACK_PERMISSION = "用于发现并连接同一局域网、个人热点或附近设备上的多人游戏。"
EXPECTED_BONJOUR_SERVICES = ["_openra-ra2._tcp"]
EXPECTED_ICON_NAME = "AppIcon"
EXPECTED_PHONE_ICONS = {
    "CFBundlePrimaryIcon": {
        "CFBundleIconFiles": ["AppIcon60x60"],
        "CFBundleIconName": EXPECTED_ICON_NAME,
    }
}
EXPECTED_TABLET_ICONS = {
    "CFBundlePrimaryIcon": {
        "CFBundleIconFiles": ["AppIcon60x60", "AppIcon76x76"],
        "CFBundleIconName": EXPECTED_ICON_NAME,
    }
}
ICON_CATALOGS = ("AppIcon.appiconset", "PublicAppIcon.appiconset")
IDIOMS = {"iphone": "phone", "ipad": "pad", "ios-marketing": "marketing"}
HEX_DIGEST = re.compile(r"^[0-9A-Fa-f]{64}$")
LEGACY_VISIBLE_NAMES = ("NUCLEAR CRISIS", "OpenRTS Mobile", "OpenRA iPad")
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"


def _require_regular_file(path: Path, label: str) -> None:
    if path.is_symlink():
        raise ValueError(f"{label} cannot be a symlink: {path}")
    if not path.is_file():
        raise ValueError(f"{label} is missing or is not a regular file: {path}")


def _validate_bundle_root(app_bundle: Path) -> None:
    if app_bundle.suffix != ".app":
        raise ValueError(f"iOS application bundle must use the .app suffix: {app_bundle}")
    if app_bundle.is_symlink():
        raise ValueError(f"iOS application bundle cannot be a symlink: {app_bundle}")
    if not app_bundle.is_dir():
        raise ValueError(f"iOS application bundle is missing: {app_bundle}")

    for member in app_bundle.rglob("*"):
        if member.is_symlink():
            raise ValueError(f"iOS application bundle member cannot be a symlink: {member}")


def _load_plist(path: Path) -> dict[str, object]:
    _require_regular_file(path, "Info.plist")
    with path.open("rb") as stream:
        value = plistlib.load(stream)
    if not isinstance(value, dict):
        raise ValueError(f"Info.plist root must be a dictionary: {path}")
    return value


def _require_plist_value(plist: dict[str, object], key: str, expected: object) -> None:
    actual = plist.get(key)
    if actual != expected:
        raise ValueError(f"{key} must be {expected!r}, not {actual!r}")


def _audit_plist(app_bundle: Path) -> None:
    plist = _load_plist(app_bundle / "Info.plist")
    _require_plist_value(plist, "CFBundleDisplayName", EXPECTED_NAME)
    _require_plist_value(plist, "CFBundleName", EXPECTED_NAME)
    _require_plist_value(plist, "CFBundleIdentifier", EXPECTED_BUNDLE_IDENTIFIER)
    _require_plist_value(plist, "CFBundleDevelopmentRegion", EXPECTED_DEVELOPMENT_REGION)
    _require_plist_value(plist, "CFBundleLocalizations", EXPECTED_LOCALIZATIONS)
    _require_plist_value(
        plist, "NSLocalNetworkUsageDescription", EXPECTED_FALLBACK_PERMISSION
    )
    _require_plist_value(plist, "NSBonjourServices", EXPECTED_BONJOUR_SERVICES)

    _require_plist_value(plist, "CFBundleIcons", EXPECTED_PHONE_ICONS)
    _require_plist_value(plist, "CFBundleIcons~ipad", EXPECTED_TABLET_ICONS)

    serialized = repr(plist)
    for stale_name in LEGACY_VISIBLE_NAMES:
        if stale_name in serialized:
            raise ValueError(f"Info.plist contains a stale visible name: {stale_name}")


def _audit_localizations(app_bundle: Path, source_root: Path) -> None:
    actual_directories = {
        child.name
        for child in app_bundle.iterdir()
        if child.name.endswith(".lproj") and child.is_dir()
    }
    if actual_directories != EXPECTED_LOCALIZATION_DIRECTORIES:
        missing = sorted(EXPECTED_LOCALIZATION_DIRECTORIES - actual_directories)
        unexpected = sorted(actual_directories - EXPECTED_LOCALIZATION_DIRECTORIES)
        details = []
        if missing:
            details.append(f"missing {', '.join(missing)}")
        if unexpected:
            details.append(f"unexpected {', '.join(unexpected)}")
        raise ValueError("localization directory set mismatch: " + "; ".join(details))

    source_base = source_root / "ios" / "OpenRA.iOS"
    for localization, expected_bytes in EXPECTED_PERMISSION_BYTES.items():
        localization_directory = app_bundle / localization
        actual_members = {member.name for member in localization_directory.iterdir()}
        if actual_members != {"InfoPlist.strings"}:
            unexpected = sorted(actual_members - {"InfoPlist.strings"})
            missing = sorted({"InfoPlist.strings"} - actual_members)
            details = [
                *(f"unexpected {localization}/{name}" for name in unexpected),
                *(f"missing {localization}/{name}" for name in missing),
            ]
            raise ValueError(
                f"{localization} member set mismatch: " + "; ".join(details)
            )
        source = source_base / localization / "InfoPlist.strings"
        bundled = localization_directory / "InfoPlist.strings"
        _require_regular_file(source, f"source {localization}/InfoPlist.strings")
        _require_regular_file(bundled, f"bundled {localization}/InfoPlist.strings")
        if source.read_bytes() != expected_bytes:
            raise ValueError(
                f"source {localization}/InfoPlist.strings does not contain the exact permission text"
            )
        if bundled.read_bytes() != expected_bytes:
            raise ValueError(
                f"bundled {localization}/InfoPlist.strings does not match the source"
            )


def _catalog_images(catalog: Path) -> tuple[dict[str, object], ...]:
    contents_path = catalog / "Contents.json"
    _require_regular_file(contents_path, f"{catalog.name}/Contents.json")
    contents = json.loads(contents_path.read_text(encoding="utf-8"))
    images = contents.get("images") if isinstance(contents, dict) else None
    if not isinstance(images, list) or not images:
        raise ValueError(f"{contents_path} must define a non-empty images array")
    if not all(isinstance(image, dict) for image in images):
        raise ValueError(f"{contents_path} contains an invalid image entry")
    return tuple(images)


def _expected_icon_contract(
    source_root: Path,
) -> tuple[set[tuple[str, int, int, int]], set[str]]:
    assets = source_root / "ios" / "OpenRA.iOS" / "Assets.xcassets"
    personal = assets / ICON_CATALOGS[0]
    public = assets / ICON_CATALOGS[1]
    personal_images = _catalog_images(personal)
    public_images = _catalog_images(public)
    if (personal / "Contents.json").read_bytes() != (public / "Contents.json").read_bytes():
        raise ValueError("personal and PublicClean AppIcon Contents.json files differ")
    if public_images != personal_images:
        raise ValueError("personal and PublicClean AppIcon slots differ")

    slots: set[tuple[str, int, int, int]] = set()
    referenced_files: set[str] = set()
    for image in personal_images:
        filename = image.get("filename")
        idiom = image.get("idiom")
        scale_text = image.get("scale")
        size_text = image.get("size")
        if not all(isinstance(value, str) for value in (filename, idiom, scale_text, size_text)):
            raise ValueError("AppIcon slot must define string filename, idiom, scale, and size")
        if not filename.startswith("NUKE-HOUR-") or Path(filename).name != filename:
            raise ValueError(f"AppIcon source filename is not canonical: {filename}")
        if idiom not in IDIOMS:
            raise ValueError(f"AppIcon source idiom is unsupported: {idiom}")
        if not scale_text.endswith("x") or not scale_text[:-1].isdigit():
            raise ValueError(f"AppIcon source scale is invalid: {scale_text}")
        try:
            points = Decimal(size_text.split("x", 1)[0])
            scale = int(scale_text[:-1])
            pixels = int(points * scale)
        except (InvalidOperation, ValueError) as error:
            raise ValueError(f"AppIcon source size is invalid: {size_text}") from error

        source = personal / filename
        public_source = public / filename
        _require_regular_file(source, f"personal AppIcon {filename}")
        _require_regular_file(public_source, f"PublicClean AppIcon {filename}")
        if source.read_bytes() != public_source.read_bytes():
            raise ValueError(f"personal and PublicClean AppIcon bytes differ: {filename}")
        with Image.open(source) as icon:
            icon.load()
            if icon.size != (pixels, pixels):
                raise ValueError(
                    f"AppIcon {filename} must be {pixels}x{pixels}, not {icon.size}"
                )
            if icon.mode != "RGB":
                raise ValueError(f"AppIcon {filename} must be opaque RGB, not {icon.mode}")

        referenced_files.add(filename)
        slots.add((IDIOMS[idiom], scale, pixels, pixels))

    for label, catalog in (("personal", personal), ("PublicClean", public)):
        png_members = tuple(catalog.glob("*.png"))
        invalid_members = [
            member.name
            for member in png_members
            if member.is_symlink() or not member.is_file()
        ]
        if invalid_members:
            raise ValueError(
                f"{label} AppIcon PNG members must be regular files: {sorted(invalid_members)!r}"
            )
        actual_files = {path.name for path in png_members}
        if actual_files != referenced_files:
            raise ValueError(
                f"{label} AppIcon file set does not exactly match Contents.json: "
                f"expected {sorted(referenced_files)!r}, found {sorted(actual_files)!r}"
            )
    return slots, referenced_files


def _assetutil_info(assets_car: Path) -> list[dict[str, object]]:
    """Return assetutil JSON. Kept injectable so unit tests never invoke Xcode."""
    result = subprocess.run(
        ["/usr/bin/assetutil", "--info", str(assets_car)],
        check=False,
        capture_output=True,
        text=True,
        timeout=30,
    )
    if result.returncode != 0:
        detail = result.stderr.strip() or result.stdout.strip() or "assetutil failed"
        raise ValueError(f"Assets.car could not be inspected: {detail}")
    try:
        value = json.loads(result.stdout)
    except json.JSONDecodeError as error:
        raise ValueError("Assets.car assetutil output is not valid JSON") from error
    if not isinstance(value, list) or not all(isinstance(entry, dict) for entry in value):
        raise ValueError("Assets.car assetutil output must be an array of dictionaries")
    return value


def _audit_asset_metadata(
    metadata: Sequence[dict[str, object]],
    expected_slots: set[tuple[str, int, int, int]],
) -> None:
    named_icon_entries = [
        entry
        for entry in metadata
        if entry.get("AssetType") in ("Icon Image", "MultiSized Image")
    ]
    if not named_icon_entries:
        raise ValueError("Assets.car contains no AppIcon metadata")
    icon_names = {entry.get("Name") for entry in named_icon_entries}
    if icon_names != {EXPECTED_ICON_NAME}:
        raise ValueError(f"Assets.car contains unexpected icon catalog names: {icon_names!r}")

    icon_images = [
        entry for entry in named_icon_entries if entry.get("AssetType") == "Icon Image"
    ]
    actual_slots: set[tuple[str, int, int, int]] = set()
    for entry in icon_images:
        rendition_name = entry.get("RenditionName")
        if not isinstance(rendition_name, str) or not rendition_name.endswith(".png"):
            raise ValueError("Assets.car AppIcon rendition is missing a PNG RenditionName")
        if entry.get("Opaque") is not True:
            raise ValueError(f"Assets.car AppIcon rendition is not opaque: {rendition_name}")
        if entry.get("ColorModel") != "RGB" or entry.get("Colorspace") != "srgb":
            raise ValueError(f"Assets.car AppIcon rendition is not RGB/sRGB: {rendition_name}")
        digest = entry.get("SHA1Digest")
        if not isinstance(digest, str) or not HEX_DIGEST.fullmatch(digest):
            raise ValueError(f"Assets.car AppIcon rendition has an invalid digest: {rendition_name}")

        values = (
            entry.get("Idiom"),
            entry.get("Scale"),
            entry.get("PixelWidth"),
            entry.get("PixelHeight"),
        )
        if (
            not isinstance(values[0], str)
            or any(not isinstance(value, int) or isinstance(value, bool) for value in values[1:])
        ):
            raise ValueError(f"Assets.car AppIcon rendition has invalid slot metadata: {rendition_name}")
        actual_slots.add((values[0], values[1], values[2], values[3]))

    missing_slots = expected_slots - actual_slots
    if missing_slots:
        raise ValueError(f"Assets.car is missing AppIcon slots: {sorted(missing_slots)!r}")


def _audit_compiled_icon_pixels(app_bundle: Path, source_root: Path) -> None:
    source_catalog = (
        source_root
        / "ios"
        / "OpenRA.iOS"
        / "Assets.xcassets"
        / "AppIcon.appiconset"
    )
    comparisons = (
        ("AppIcon60x60@2x.png", "NUKE-HOUR-60@2x.png"),
        ("AppIcon76x76@2x~ipad.png", "NUKE-HOUR-76@2x.png"),
    )
    for bundled_name, source_name in comparisons:
        bundled = app_bundle / bundled_name
        source = source_catalog / source_name
        _require_regular_file(bundled, f"compiled launcher icon {bundled_name}")
        _require_regular_file(source, f"source launcher icon {source_name}")
        with Image.open(source) as source_icon, _load_compiled_icon(bundled) as bundled_icon:
            source_icon.load()
            bundled_icon.load()
            if bundled_icon.mode != "RGB" or "transparency" in bundled_icon.info:
                raise ValueError(f"{bundled_name} must be opaque RGB without transparency")
            if source_icon.size != bundled_icon.size:
                raise ValueError(
                    f"{bundled_name} dimensions do not match {source_name}: "
                    f"{bundled_icon.size} != {source_icon.size}"
                )
            difference = ImageChops.difference(
                source_icon.convert("RGB"), bundled_icon.convert("RGB")
            )
            if difference.getbbox() is not None:
                raise ValueError(f"{bundled_name} pixels do not match {source_name}")


def _load_compiled_icon(path: Path) -> Image.Image:
    with path.open("rb") as stream:
        prefix = stream.read(16)
    if prefix[:8] != PNG_SIGNATURE or prefix[12:16] != b"CgBI":
        with Image.open(path) as icon:
            icon.load()
            return icon.copy()

    with tempfile.TemporaryDirectory(prefix="nuke-hour-cgbi-") as temporary:
        decoded = Path(temporary) / "decoded.png"
        try:
            result = subprocess.run(
                [
                    "/usr/bin/sips",
                    "-s",
                    "format",
                    "png",
                    str(path),
                    "--out",
                    str(decoded),
                ],
                check=False,
                capture_output=True,
                text=True,
                timeout=30,
            )
        except (OSError, subprocess.SubprocessError) as error:
            raise ValueError(f"CgBI icon {path.name} could not be converted: {error}") from error
        if result.returncode != 0:
            detail = result.stderr.strip() or result.stdout.strip() or "sips failed"
            raise ValueError(
                f"CgBI icon {path.name} could not be converted: {detail}"
            )
        _require_regular_file(decoded, f"decoded CgBI icon {path.name}")
        try:
            with Image.open(decoded) as icon:
                icon.load()
                return icon.copy()
        except OSError as error:
            raise ValueError(
                f"converted CgBI icon {path.name} is not a readable PNG: {error}"
            ) from error


def audit_app_bundle(app_bundle: Path, source_root: Path) -> tuple[str, ...]:
    """Return audit errors without mutating either the bundle or source tree."""
    try:
        _validate_bundle_root(app_bundle)
        if source_root.is_symlink() or not source_root.is_dir():
            raise ValueError(f"source root is missing or symlinked: {source_root}")
        _audit_plist(app_bundle)
        _audit_localizations(app_bundle, source_root)
        expected_slots, _ = _expected_icon_contract(source_root)
        assets_car = app_bundle / "Assets.car"
        _require_regular_file(assets_car, "Assets.car")
        if assets_car.stat().st_size == 0:
            raise ValueError(f"Assets.car is empty: {assets_car}")
        try:
            metadata = _assetutil_info(assets_car)
        except (OSError, ValueError, subprocess.SubprocessError) as error:
            raise ValueError(f"Assets.car inspection failed: {error}") from error
        _audit_asset_metadata(metadata, expected_slots)
        _audit_compiled_icon_pixels(app_bundle, source_root)
    except (OSError, ValueError, plistlib.InvalidFileException) as error:
        return (str(error),)
    return ()


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Audit the visible brand and native localization of an iOS .app"
    )
    parser.add_argument("app_bundle", type=Path)
    parser.add_argument("--source-root", required=True, type=Path)
    return parser


def main(argv: Iterable[str] | None = None) -> int:
    arguments = _parser().parse_args(argv)
    errors = audit_app_bundle(arguments.app_bundle, arguments.source_root)
    if errors:
        for error in errors:
            print(f"iOS application brand audit failed: {error}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
