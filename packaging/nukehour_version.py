#!/usr/bin/env python3
"""Canonical product-version and multiplayer-compatibility projections."""

from __future__ import annotations

import argparse
import dataclasses
import hashlib
import json
import os
import re
import sys
import tempfile
from pathlib import Path


class VersionError(RuntimeError):
    pass


@dataclasses.dataclass(frozen=True)
class VersionManifest:
    schema: int
    platform: str
    version: str
    build: int
    channel: str


FIELDS = {"schema", "platform", "version", "build", "channel"}
PLATFORM_NAMES = {"ios": "iOS", "macos": "macOS", "android": "Android"}
SEMVER = re.compile(r"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$")
STORAGE_VERSION = "nukehour-storage-v1"
COMPATIBILITY_PREFIX = "nukehour-core-sha256-"


def repository_root() -> Path:
    return Path(__file__).resolve().parents[1]


def load_manifest(root: Path) -> VersionManifest:
    path = Path(root) / "packaging/nukehour-version.json"
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise VersionError(f"Cannot read {path}: {error}") from error
    if not isinstance(data, dict) or set(data) != FIELDS:
        raise VersionError(f"{path} must contain exactly {sorted(FIELDS)}")
    if data["schema"] != 1:
        raise VersionError("Unsupported version manifest schema")
    if data["platform"] not in PLATFORM_NAMES:
        raise VersionError("Unsupported platform")
    if not isinstance(data["version"], str) or not SEMVER.fullmatch(data["version"]):
        raise VersionError("Version must be three-part SemVer")
    if isinstance(data["build"], bool) or not isinstance(data["build"], int) or data["build"] < 1:
        raise VersionError("Build must be a positive integer")
    if data["channel"] != "stable":
        raise VersionError("Only the stable channel is distributable")
    return VersionManifest(**data)


def display_version(manifest: VersionManifest) -> str:
    return f"NUKE HOUR {PLATFORM_NAMES[manifest.platform]} {manifest.version} (Build {manifest.build})"


def _compatibility_files(root: Path) -> list[Path]:
    root = Path(root).resolve()
    patterns_path = root / "packaging/nukehour-compatibility-inputs.txt"
    try:
        patterns = [line.strip() for line in patterns_path.read_text(encoding="utf-8").splitlines()
                    if line.strip() and not line.lstrip().startswith("#")]
    except OSError as error:
        raise VersionError(f"Cannot read {patterns_path}: {error}") from error
    if not patterns:
        raise VersionError("Compatibility input list is empty")
    included = [pattern for pattern in patterns if not pattern.startswith("!")]
    excluded = [pattern[1:] for pattern in patterns if pattern.startswith("!")]
    if any(not pattern for pattern in excluded):
        raise VersionError("Compatibility exclusion pattern is empty")
    matched: dict[str, Path] = {}
    for pattern in included:
        hits = [path for path in root.glob(pattern) if path.is_file()]
        if not hits:
            raise VersionError(f"Compatibility pattern matched no files: {pattern}")
        for path in hits:
            if path.is_symlink():
                raise VersionError(f"Compatibility input must not be a symlink: {path}")
            resolved = path.resolve()
            try:
                relative = resolved.relative_to(root).as_posix()
            except ValueError as error:
                raise VersionError(f"Compatibility input escapes repository: {path}") from error
            if relative in matched:
                raise VersionError(f"Compatibility input matched more than once: {relative}")
            matched[relative] = resolved
    for pattern in excluded:
        for path in root.glob(pattern):
            if path.is_file():
                try:
                    relative = path.resolve().relative_to(root).as_posix()
                except ValueError as error:
                    raise VersionError(f"Compatibility exclusion escapes repository: {path}") from error
                matched.pop(relative, None)
    if not matched:
        raise VersionError("Compatibility input set is empty after exclusions")
    return [matched[key] for key in sorted(matched)]


def compatibility_digest(root: Path) -> str:
    root = Path(root).resolve()
    digest = hashlib.sha256()
    for path in _compatibility_files(root):
        relative = path.relative_to(root).as_posix().encode("utf-8")
        content = path.read_bytes()
        digest.update(len(relative).to_bytes(8, "big"))
        digest.update(relative)
        digest.update(len(content).to_bytes(8, "big"))
        digest.update(content)
    return digest.hexdigest()


def _props(manifest: VersionManifest) -> str:
    return (
        '<Project>\n'
        '  <PropertyGroup>\n'
        f'    <NukeHourPlatform>{manifest.platform}</NukeHourPlatform>\n'
        f'    <NukeHourProductVersion>{manifest.version}</NukeHourProductVersion>\n'
        f'    <NukeHourBuildNumber>{manifest.build}</NukeHourBuildNumber>\n'
        f'    <NukeHourDisplayVersion>{display_version(manifest)}</NukeHourDisplayVersion>\n'
        '  </PropertyGroup>\n'
        '</Project>\n'
    )


def _project_metadata(source: str, values: dict[str, str]) -> str:
    lines = source.splitlines()
    metadata = next((index for index, line in enumerate(lines) if line == "Metadata:"), None)
    if metadata is None:
        raise VersionError("mod.yaml has no top-level Metadata block")
    end = metadata + 1
    while end < len(lines) and (not lines[end] or lines[end][0].isspace()):
        end += 1
    block = lines[metadata + 1:end]
    for key, value in values.items():
        prefix = f"\t{key}:"
        found = next((index for index, line in enumerate(block) if line.startswith(prefix)), None)
        projected = f"\t{key}: {value}"
        if found is None:
            block.append(projected)
        else:
            block[found] = projected
    return "\n".join(lines[:metadata + 1] + block + lines[end:]) + "\n"


def _project_map_folders(source: str) -> str:
    legacy = "\t~^SupportDir|maps/ra2/{DEV_VERSION}: User"
    stable = f"\t~^SupportDir|maps/ra2/{STORAGE_VERSION}: User"
    if legacy not in source:
        return source
    source = source.replace(stable + "\n", "")
    return source.replace(legacy, stable + "\n" + legacy)


def expected_projections(root: Path) -> dict[Path, str]:
    root = Path(root)
    manifest = load_manifest(root)
    compatibility = COMPATIBILITY_PREFIX + compatibility_digest(root)
    projections = {root / "packaging/nukehour-version.props": _props(manifest)}
    mod_path = root / "mods/ra2/mod.yaml"
    projected_mod = _project_metadata(mod_path.read_text(encoding="utf-8"), {
        "Version": STORAGE_VERSION,
        "DisplayVersion": display_version(manifest),
        "Compatibility": compatibility,
    })
    projections[mod_path] = _project_map_folders(projected_mod)
    content_path = root / "mods/ra2-content/mod.yaml"
    projections[content_path] = _project_metadata(content_path.read_text(encoding="utf-8"), {
        "Version": STORAGE_VERSION,
    })
    common_path = root / "mods/ra2/manifest/common.yaml"
    if common_path.is_file():
        projections[common_path] = _project_metadata(common_path.read_text(encoding="utf-8"), {
            "Version": STORAGE_VERSION,
            "DisplayVersion": display_version(manifest),
            "Compatibility": compatibility,
        })
    return projections


def _atomic_write(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
            stream.write(content)
        os.replace(temporary_name, path)
    except BaseException:
        try:
            os.unlink(temporary_name)
        except FileNotFoundError:
            pass
        raise


def apply(root: Path) -> dict[str, str]:
    projections = expected_projections(root)
    for path, content in projections.items():
        if not path.is_file() or path.read_text(encoding="utf-8") != content:
            _atomic_write(path, content)
    return {str(path): content for path, content in projections.items()}


def check(root: Path) -> None:
    stale = []
    for path, expected in expected_projections(root).items():
        if not path.is_file() or path.read_text(encoding="utf-8") != expected:
            stale.append(str(path))
    if stale:
        raise VersionError("Stale NUKE HOUR version projections: " + ", ".join(stale))


def bump_patch(root: Path) -> VersionManifest:
    root = Path(root)
    current = load_manifest(root)
    major, minor, patch = (int(part) for part in current.version.split("."))
    updated = dataclasses.replace(current, version=f"{major}.{minor}.{patch + 1}", build=current.build + 1)
    payload = dataclasses.asdict(updated)
    _atomic_write(root / "packaging/nukehour-version.json", json.dumps(payload, indent=2) + "\n")
    apply(root)
    return updated


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=repository_root())
    subcommands = parser.add_subparsers(dest="command", required=True)
    subcommands.add_parser("show")
    subcommands.add_parser("apply")
    subcommands.add_parser("check")
    bump = subcommands.add_parser("bump")
    bump.add_argument("part", choices=("patch",))
    args = parser.parse_args(argv)
    try:
        if args.command == "show":
            manifest = load_manifest(args.root)
            print(display_version(manifest))
            print(COMPATIBILITY_PREFIX + compatibility_digest(args.root))
        elif args.command == "apply":
            apply(args.root)
        elif args.command == "check":
            check(args.root)
        else:
            bump_patch(args.root)
    except VersionError as error:
        print(f"error: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
