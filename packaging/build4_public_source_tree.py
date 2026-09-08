#!/usr/bin/env python3
"""Create a fail-closed public projection of the recovered Build 4 tree."""

from __future__ import annotations

import argparse
import csv
import os
import shutil
from pathlib import Path, PurePosixPath


EXCLUDED_DIRECTORY_NAMES = {
    ".git", ".vs", ".vscode", "__pycache__", "artifacts", "bin", "DerivedData", "obj",
}
EXCLUDED_SUFFIXES = {".ipa", ".mobileprovision", ".p12", ".pfx"}
EXCLUDED_APP_NAMES = {"NUKE HOUR.app", "NUKE HOUR GAME.app"}


def denied_assets(ledger: Path) -> set[str]:
    denied: set[str] = set()
    with ledger.open(encoding="utf-8", newline="") as stream:
        for raw in stream:
            if raw.startswith("#") or not raw.strip():
                continue
            scope, classification, _sha256, _size, _bundled, relative = raw.rstrip("\n").split("\t")
            if scope == "SOURCE" and classification in {"ORIGINAL_RA2_ASSET", "UNKNOWN"}:
                denied.add(PurePosixPath(relative).as_posix())
    return denied


def excluded(relative: PurePosixPath, denied: set[str]) -> bool:
    text = relative.as_posix()
    return (
        text in denied
        or any(part in EXCLUDED_DIRECTORY_NAMES for part in relative.parts)
        or any(part in EXCLUDED_APP_NAMES for part in relative.parts)
        or relative.suffix.lower() in EXCLUDED_SUFFIXES
        or relative.name == ".DS_Store"
        or relative.name == "utility.log"
    )


def export_tree(source: Path, destination: Path, ledger: Path) -> tuple[int, int]:
    source = source.resolve()
    destination = destination.resolve()
    if destination.exists():
        raise FileExistsError(f"refusing to overwrite existing destination: {destination}")
    try:
        destination.relative_to(source)
    except ValueError:
        pass
    else:
        if destination.parts[: len(source.parts) + 1] != source.parts + ("artifacts",):
            raise ValueError("destination inside source must be directly below artifacts/")

    denied = denied_assets(ledger)
    destination.mkdir(parents=True)
    copied = 0
    omitted = 0
    for current, directory_names, file_names in os.walk(source, followlinks=False):
        current_path = Path(current)
        relative_dir = current_path.relative_to(source)
        kept_directories: list[str] = []
        for name in sorted(directory_names):
            relative = PurePosixPath(relative_dir.as_posix(), name)
            path = current_path / name
            if excluded(relative, denied):
                omitted += 1
            elif path.is_symlink():
                target = os.readlink(path)
                target_path = (path.parent / target).resolve()
                try:
                    target_path.relative_to(source)
                except ValueError as error:
                    raise ValueError(f"symlink escapes source tree: {relative}") from error
                output = destination / relative
                output.parent.mkdir(parents=True, exist_ok=True)
                output.symlink_to(target)
                copied += 1
            else:
                kept_directories.append(name)
        directory_names[:] = kept_directories

        for name in sorted(file_names):
            relative = PurePosixPath(relative_dir.as_posix(), name)
            path = current_path / name
            if excluded(relative, denied):
                omitted += 1
                continue
            output = destination / relative
            output.parent.mkdir(parents=True, exist_ok=True)
            if path.is_symlink():
                target = os.readlink(path)
                target_path = (path.parent / target).resolve()
                try:
                    target_path.relative_to(source)
                except ValueError as error:
                    raise ValueError(f"symlink escapes source tree: {relative}") from error
                output.symlink_to(target)
            else:
                shutil.copy2(path, output)
            copied += 1
    return copied, omitted


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=Path.cwd())
    parser.add_argument("--destination", type=Path, required=True)
    parser.add_argument("--ledger", type=Path, required=True)
    arguments = parser.parse_args()
    copied, omitted = export_tree(arguments.source, arguments.destination, arguments.ledger)
    print(f"copied={copied}")
    print(f"omitted={omitted}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
