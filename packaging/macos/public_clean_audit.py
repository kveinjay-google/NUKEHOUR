#!/usr/bin/env python3
"""Fail-closed audit for NUKE HOUR macOS PublicClean artifacts."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import plistlib
import subprocess
import sys
import tempfile
from dataclasses import asdict, dataclass
from pathlib import Path, PurePosixPath


RETAIL_EXTENSIONS = frozenset({
    ".aud", ".bag", ".bik", ".hva", ".idx", ".mix", ".shp", ".vqa",
    ".vxl", ".wsa",
})
MAP_EXTENSIONS = frozenset({".map", ".mpr", ".oramap", ".yrm"})
GENERATED_PREFIXES = ("engine/bin/", "dotnet/", "NUKE HOUR GAME.app/")
GENERATED_FILES = frozenset({"engine/global mix database.dat"})
FORBIDDEN_COMPONENTS = frozenset({
    ".git", "__pycache__", "Content", "Logs", "Replays", "Saves", "maps",
})
PUBLIC_CATEGORIES = frozenset({
    "engine-source", "project-original", "redistributable-third-party",
})
EXPECTED_DMG_ENTRIES = frozenset({
    "NUKE HOUR.app", "Applications", ".background", ".VolumeIcon.icns", ".fseventsd",
})


@dataclass(frozen=True)
class AuditError:
    code: str
    path: str
    detail: str = ""


@dataclass(frozen=True)
class AuditedFile:
    path: str
    size: int
    sha256: str
    category: str
    license: str


@dataclass(frozen=True)
class AuditResult:
    errors: tuple[AuditError, ...] = ()
    files: tuple[AuditedFile, ...] = ()

    @property
    def passed(self):
        return not self.errors


def sha256_file(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_manifest(path):
    with Path(path).open(encoding="utf-8") as stream:
        manifest = json.load(stream)
    _manifest_entries(manifest)
    return manifest


def _manifest_entries(manifest):
    if manifest.get("schemaVersion") != 1:
        raise ValueError("PublicClean manifest must use schemaVersion 1")
    entries = {}
    for entry in manifest.get("files", []):
        destination = PurePosixPath(entry["destination"]).as_posix()
        if destination.startswith("../") or destination.startswith("/"):
            raise ValueError(f"manifest destination escapes runtime: {destination}")
        if destination in entries:
            raise ValueError(f"duplicate manifest destination: {destination}")
        if entry.get("category") not in PUBLIC_CATEGORIES:
            raise ValueError(f"non-public manifest category: {destination}")
        entries[destination] = entry
    return entries


def _is_generated(relative):
    return relative in GENERATED_FILES or any(
        relative.startswith(prefix) for prefix in GENERATED_PREFIXES)


def audit_runtime(root, manifest):
    root = Path(root)
    resolved_root = root.resolve()
    entries = _manifest_entries(manifest)
    errors = []
    files = []
    seen = set()

    if not root.is_dir():
        return AuditResult((AuditError("runtime-missing", str(root)),), ())

    for path in sorted(root.rglob("*"), key=lambda item: item.as_posix()):
        relative = path.relative_to(root).as_posix()
        parts = PurePosixPath(relative).parts
        if path.is_symlink():
            try:
                path.resolve(strict=True).relative_to(resolved_root)
            except (FileNotFoundError, ValueError):
                errors.append(AuditError("unsafe-symlink", relative))
                continue
        if path.is_dir():
            continue
        if not path.is_file():
            errors.append(AuditError("special-file", relative))
            continue

        suffix = path.suffix.lower()
        if suffix in RETAIL_EXTENSIONS:
            errors.append(AuditError("retail-resource", relative, suffix))
            continue
        if suffix in MAP_EXTENSIONS:
            errors.append(AuditError("bundled-map", relative, suffix))
            continue
        if any(component in FORBIDDEN_COMPONENTS for component in parts):
            errors.append(AuditError("developer-resource", relative))
            continue

        entry = entries.get(relative)
        if entry is None:
            if not _is_generated(relative):
                errors.append(AuditError("unlisted-resource", relative))
            continue

        seen.add(relative)
        actual_size = path.stat().st_size
        if actual_size != entry.get("size"):
            errors.append(AuditError(
                "size-mismatch", relative,
                f"expected {entry.get('size')!r}, got {actual_size}"))
            continue
        actual_hash = sha256_file(path)
        if actual_hash != str(entry.get("sha256", "")).lower():
            errors.append(AuditError("hash-mismatch", relative))
            continue
        files.append(AuditedFile(
            relative, actual_size, actual_hash, entry["category"], entry.get("license", "")))

    for relative in sorted(set(entries) - seen):
        errors.append(AuditError("manifest-file-missing", relative))

    return AuditResult(tuple(errors), tuple(files))


def _merge(*results):
    return AuditResult(
        tuple(error for result in results for error in result.errors),
        tuple(file for result in results for file in result.files),
    )


def audit_prohibited_tree(root):
    root = Path(root)
    resolved_root = root.resolve()
    errors = []
    for path in sorted(root.rglob("*"), key=lambda item: item.as_posix()):
        relative = path.relative_to(root).as_posix()
        if path.is_symlink():
            try:
                path.resolve(strict=True).relative_to(resolved_root)
            except (FileNotFoundError, ValueError):
                errors.append(AuditError("unsafe-symlink", relative))
            continue
        if not path.is_file():
            continue
        suffix = path.suffix.lower()
        if suffix in RETAIL_EXTENSIONS:
            errors.append(AuditError("retail-resource", relative, suffix))
        elif suffix in MAP_EXTENSIONS:
            errors.append(AuditError("bundled-map", relative, suffix))
        elif any(component in FORBIDDEN_COMPONENTS for component in path.relative_to(root).parts):
            errors.append(AuditError("developer-resource", relative))
    return AuditResult(tuple(errors), ())


def audit_app(app, manifest, version, require_executable=True):
    app = Path(app)
    errors = []
    plist_path = app / "Contents" / "Info.plist"
    try:
        with plist_path.open("rb") as stream:
            plist = plistlib.load(stream)
    except (OSError, plistlib.InvalidFileException) as exc:
        return AuditResult((AuditError("invalid-plist", str(plist_path), str(exc)),), ())

    for key in ("CFBundleDisplayName", "CFBundleName"):
        value = str(plist.get(key, ""))
        if "RA2" in value.upper():
            errors.append(AuditError("visible-ra2-brand", key, value))
        if value != "NUKE HOUR":
            errors.append(AuditError("unexpected-brand", key, value))
    if str(plist.get("CFBundleShortVersionString", "")) != str(version):
        errors.append(AuditError(
            "version-mismatch", "CFBundleShortVersionString",
            str(plist.get("CFBundleShortVersionString", ""))))

    executable_name = plist.get("CFBundleExecutable", "NUKE HOUR")
    executable = app / "Contents" / "MacOS" / executable_name
    if require_executable:
        if not executable.is_file() or not os.access(executable, os.X_OK):
            errors.append(AuditError("executable-missing", str(executable)))
        else:
            inspected = subprocess.run(
                ["file", str(executable)], capture_output=True, text=True, check=False)
            if inspected.returncode or "arm64" not in inspected.stdout:
                errors.append(AuditError("wrong-architecture", str(executable), inspected.stdout.strip()))

    runtime_result = audit_runtime(app / "Contents" / "Resources" / "runtime", manifest)
    return _merge(AuditResult(tuple(errors), ()), audit_prohibited_tree(app), runtime_result)


def audit_dmg_inventory(entries):
    names = set(entries)
    errors = [
        AuditError("unexpected-dmg-entry", name)
        for name in sorted(names - EXPECTED_DMG_ENTRIES)
    ]
    errors.extend(
        AuditError("missing-dmg-entry", name)
        for name in sorted({"NUKE HOUR.app", "Applications"} - names)
    )
    return AuditResult(tuple(errors), ())


def write_reports(result, json_path, markdown_path):
    json_path = Path(json_path)
    markdown_path = Path(markdown_path)
    json_path.parent.mkdir(parents=True, exist_ok=True)
    markdown_path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "passed": result.passed,
        "errors": [asdict(error) for error in result.errors],
        "files": [asdict(file) for file in result.files],
    }
    json_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    lines = [
        "# macOS PublicClean Audit", "",
        f"Result: **{'PASS' if result.passed else 'FAIL'}**", "",
        f"Audited manifest files: {len(result.files)}", f"Errors: {len(result.errors)}", "",
    ]
    if result.errors:
        lines.extend(["## Errors", ""])
        lines.extend(
            f"- `{error.code}` `{error.path}` {error.detail}" for error in result.errors)
        lines.append("")
    markdown_path.write_text("\n".join(lines), encoding="utf-8")


def _audit_dmg(dmg, manifest, version):
    with tempfile.TemporaryDirectory(prefix="nukehour-public-dmg-") as temporary:
        mountpoint = Path(temporary) / "mounted"
        mountpoint.mkdir()
        attached = False
        try:
            subprocess.run([
                "hdiutil", "attach", "-readonly", "-nobrowse", "-mountpoint",
                str(mountpoint), str(dmg),
            ], check=True, capture_output=True, text=True)
            attached = True
            inventory = audit_dmg_inventory(path.name for path in mountpoint.iterdir())
            app_result = audit_app(mountpoint / "NUKE HOUR.app", manifest, version)
            return _merge(inventory, app_result)
        finally:
            if attached:
                subprocess.run(["hdiutil", "detach", str(mountpoint)], check=False,
                               capture_output=True, text=True)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    target = parser.add_mutually_exclusive_group(required=True)
    target.add_argument("--runtime", type=Path)
    target.add_argument("--app", type=Path)
    target.add_argument("--dmg", type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--version", default="1.0.3")
    parser.add_argument("--json-report", type=Path)
    parser.add_argument("--markdown-report", type=Path)
    arguments = parser.parse_args(argv)
    manifest = load_manifest(arguments.manifest)
    if arguments.runtime:
        result = audit_runtime(arguments.runtime, manifest)
    elif arguments.app:
        result = audit_app(arguments.app, manifest, arguments.version)
    else:
        result = _audit_dmg(arguments.dmg, manifest, arguments.version)
    if arguments.json_report and arguments.markdown_report:
        write_reports(result, arguments.json_report, arguments.markdown_report)
    if result.passed:
        print("macOS PublicClean audit PASS")
        return 0
    for error in result.errors:
        print(f"{error.code}: {error.path} {error.detail}", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
