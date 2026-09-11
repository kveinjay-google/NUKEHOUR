#!/usr/bin/env python3
"""Regenerate the reviewed macOS PublicClean source manifest."""

from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

if __package__:
    from . import build_public_clean
else:
    sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
    from packaging.macos import build_public_clean


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "packaging/macos/public_clean_manifest.json"
EXACT = {
    "LICENSE",
    "engine/AUTHORS",
    "engine/COPYING",
    "engine/VERSION",
    "branding/NUKE-HOUR-1024.png",
    "launch-game.sh",
    "mod.config",
}
PREFIXES = (
    "engine/mods/common/",
    "engine/mods/common-content/",
    "mods/ra2/",
    "mods/ra2-content/",
    "mods/ra2-public/",
)
PUBLIC_BITS = {
    "mods/ra2/bits/animations/nc-impact-core.png",
    "mods/ra2/bits/animations/nc-impact-debris.png",
    "mods/ra2/bits/animations/nc-impact-ring.png",
}


def selected_sources():
    result = subprocess.run(
        ["git", "ls-files", "-z"], cwd=ROOT, check=True, capture_output=True)
    tracked = [item.decode("utf-8") for item in result.stdout.split(b"\0") if item]
    selected = [relative for relative in EXACT if (ROOT / relative).is_file()]
    for relative in tracked:
        if relative in EXACT or relative in PUBLIC_BITS:
            selected.append(relative)
            continue
        if not relative.startswith(PREFIXES):
            continue
        if relative.startswith("mods/ra2/maps/") or relative.startswith("mods/ra2/bits/"):
            continue
        path = ROOT / relative
        if path.name == ".DS_Store" or "__pycache__" in path.parts:
            continue
        if path.suffix.lower() in build_public_clean.PROHIBITED_SOURCE_EXTENSIONS:
            continue
        selected.append(relative)
    # The engine checkout is intentionally gitignored by the Mod SDK, so the
    # reviewed common runtime trees must be inventoried from disk.
    for directory in (
            ROOT / "engine/glsl",
            ROOT / "engine/mods/common",
            ROOT / "engine/mods/common-content"):
        for path in directory.rglob("*"):
            if not path.is_file() or path.is_symlink() or path.name == ".DS_Store":
                continue
            relative = path.relative_to(ROOT).as_posix()
            if path.suffix.lower() in build_public_clean.PROHIBITED_SOURCE_EXTENSIONS:
                continue
            selected.append(relative)
    return sorted(set(selected))


def provenance(relative):
    if relative.startswith("engine/"):
        return "engine-source", "GPL-3.0-only"
    if relative.startswith("mods/ra2/fonts/"):
        return "redistributable-third-party", "SIL Open Font License 1.1"
    if relative == "LICENSE" or relative.startswith("mods/ra2/"):
        return "project-original", "GPL-3.0-only / NUKE HOUR project"
    return "project-original", "NUKE HOUR project"


def make_entry(relative):
    source = ROOT / relative
    source_data = source.read_bytes()
    staged_data = source_data
    transform = None
    if relative == "mods/ra2/mod.yaml":
        transform = "public-clean-mod"
        staged_data = build_public_clean.TRANSFORMS[transform](source_data)
    category, license_name = provenance(relative)
    entry = {
        "source": relative,
        "destination": relative,
        "size": len(staged_data),
        "sha256": build_public_clean.sha256_bytes(staged_data),
        "category": category,
        "license": license_name,
    }
    if transform:
        entry.update({
            "transform": transform,
            "sourceSize": len(source_data),
            "sourceSha256": build_public_clean.sha256_bytes(source_data),
        })
    return entry


def main():
    payload = {
        "schemaVersion": 1,
        "profile": "macOS PublicClean",
        "files": [make_entry(relative) for relative in selected_sources()],
    }
    OUTPUT.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {OUTPUT.relative_to(ROOT)} with {len(payload['files'])} files")


if __name__ == "__main__":
    main()
