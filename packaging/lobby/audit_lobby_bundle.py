#!/usr/bin/env python3

import argparse
import fnmatch
import hashlib
import json
import os
import stat
import sys
from pathlib import Path, PurePosixPath


INVENTORY_NAME = "lobby-inventory.json"
ROOT_FILES = {
    "lobby-build.json",
    "lobby-config.env.example",
    "requirements.txt",
    "run-lobby.sh",
}
LICENSE_FILES = {"licenses/NUKE-HOUR-GPL-3.0.txt"}
FORBIDDEN_COMMERCIAL = (
    "*.mix", "*.bag", "*.aud", "*.vqa", "*.exe",
    "ra2.mix", "language.mix", "ra2md.mix", "langmd.mix",
)
FORBIDDEN_SECRETS = (
    ".env", ".env.*", "*.key", "*.pem", "id_rsa*", "*secret*", "*credential*",
)


class AuditError(RuntimeError):
    pass


def sha256_file(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def validate_relative_path(value):
    if not isinstance(value, str) or not value or "\\" in value or "\0" in value:
        raise AuditError(f"unsafe artifact path: {value!r}")
    path = PurePosixPath(value)
    if path.is_absolute() or path.as_posix() != value or any(
        part in ("", ".", "..") for part in path.parts
    ):
        raise AuditError(f"unsafe artifact path: {value!r}")
    return value


def iter_regular_files(bundle):
    root = Path(bundle).resolve()
    if not root.is_dir():
        raise AuditError(f"Lobby bundle is not a directory: {root}")
    for current, directories, filenames in os.walk(root, followlinks=False):
        current_path = Path(current)
        for name in directories:
            if (current_path / name).is_symlink():
                raise AuditError(f"symlink directory is forbidden: {name}")
        for name in filenames:
            path = current_path / name
            relative = path.relative_to(root).as_posix()
            validate_relative_path(relative)
            if path.is_symlink() or not path.is_file():
                raise AuditError(f"non-regular file is forbidden: {relative}")
            yield relative, path


def create_inventory(bundle, exclude=()):
    excluded = set(exclude)
    return [
        {
            "path": relative,
            "size": path.stat().st_size,
            "sha256": sha256_file(path),
            "mode": f"{stat.S_IMODE(path.stat().st_mode):04o}",
        }
        for relative, path in sorted(iter_regular_files(bundle))
        if relative not in excluded
    ]


def _load_inventory(path):
    try:
        document = json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise AuditError("invalid Lobby inventory") from exc
    if document.get("schema") != 1 or not isinstance(document.get("files"), list):
        raise AuditError("Lobby inventory must use schema 1")
    seen = set()
    for item in document["files"]:
        relative = validate_relative_path(item.get("path"))
        if relative in seen:
            raise AuditError(f"duplicate inventory path: {relative}")
        seen.add(relative)
        if item.get("mode") not in {"0644", "0755"}:
            raise AuditError(f"invalid inventory mode: {relative}")
        if not isinstance(item.get("size"), int) or item["size"] < 0:
            raise AuditError(f"invalid inventory size: {relative}")
        digest = item.get("sha256", "")
        if len(digest) != 64 or any(c not in "0123456789abcdef" for c in digest):
            raise AuditError(f"invalid inventory digest: {relative}")
    return document["files"]


def _allowlisted(relative):
    path = PurePosixPath(relative)
    if relative in ROOT_FILES or relative in LICENSE_FILES:
        return True
    return (
        len(path.parts) == 3
        and path.parts[:2] == ("src", "nukehour_lobby")
        and path.suffix == ".py"
        and not path.name.startswith("test")
    )


def audit_bundle(bundle):
    root = Path(bundle).resolve()
    expected = _load_inventory(root / INVENTORY_NAME)
    actual = create_inventory(root, exclude={INVENTORY_NAME})
    if actual != expected:
        raise AuditError("Lobby bundle does not match its exact inventory")

    for relative, path in iter_regular_files(root):
        if relative == INVENTORY_NAME:
            continue
        if not _allowlisted(relative):
            raise AuditError(f"artifact path is not allowlisted: {relative}")
        basename = PurePosixPath(relative).name.lower()
        if any(fnmatch.fnmatchcase(basename, pattern) for pattern in FORBIDDEN_COMMERCIAL):
            raise AuditError(f"commercial resource pattern is forbidden: {relative}")
        if any(fnmatch.fnmatchcase(basename, pattern) for pattern in FORBIDDEN_SECRETS):
            raise AuditError(f"secret-bearing path is forbidden: {relative}")
        if stat.S_IMODE(path.stat().st_mode) & 0o002:
            raise AuditError(f"world-writable file is forbidden: {relative}")

    combined = "\n".join(
        path.read_text(encoding="utf-8", errors="ignore")
        for relative, path in iter_regular_files(root)
        if relative != INVENTORY_NAME
    )
    if "NUKEHOUR_LOBBY_REGISTRATION_TOKEN=" in combined.replace(
        "NUKEHOUR_LOBBY_REGISTRATION_TOKEN=\n", ""
    ):
        raise AuditError("a registration credential value may be embedded")

    return {
        "schema": 1,
        "result": "PASS",
        "commercialResourcesIncluded": "NONE",
        "fileCount": len(actual),
        "inventorySha256": sha256_file(root / INVENTORY_NAME),
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description="Audit a NUKE HOUR Lobby bundle")
    parser.add_argument("bundle", type=Path)
    args = parser.parse_args(argv)
    try:
        print(json.dumps(audit_bundle(args.bundle), indent=2, sort_keys=True))
        return 0
    except AuditError as exc:
        print(f"Lobby bundle audit failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
