#!/usr/bin/env python3
"""Verify and publish the provenance inventory for bundled RA2 support art."""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
OPENRA_REVISION = "61e24e3c1d7b586aa55a86096d29e1559aa9b994"
RV_REVISION = "231059963663dcb07d07b56f607b41d173795da9"
OPENRA_URL = "https://github.com/OpenRA/ra2"
RV_URL = "https://github.com/MustaphaTR/Romanovs-Vengeance"
ORIGINAL_REFERENCES = {
    "mods/ra2/bits/animations/nc-impact-core.png":
        "packaging/generate_projectile_impact_assets.py",
    "mods/ra2/bits/animations/nc-impact-debris.png":
        "packaging/generate_projectile_impact_assets.py",
    "mods/ra2/bits/animations/nc-impact-ring.png":
        "packaging/generate_projectile_impact_assets.py",
    "mods/ra2/bits/cameos/ydomicon-cn.png":
        "NUKE HOUR image-generation task 2026-08-22 (心灵感应器)",
    "mods/ra2/bits/cameos/yfixicon-cn.png":
        "NUKE HOUR image-generation task 2026-08-22 (服务站)",
}
INVENTORIES = (
    ROOT / "packaging" / "public-content-manifest.json",
    ROOT / "docs" / "legal" / "public-asset-sbom.json",
    ROOT / "packaging" / "ios" / "public_clean_manifest.json",
)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def git_bytes(repository: Path, revision: str, relative: str) -> bytes | None:
    result = subprocess.run(
        ["git", "-C", str(repository), "show", f"{revision}:{relative}"],
        check=False,
        capture_output=True,
    )
    return result.stdout if result.returncode == 0 else None


def classify(relative: str, data: bytes, openra: Path, rv: Path) -> dict[str, object]:
    if relative in ORIGINAL_REFERENCES:
        return {
            "origin": "nuke-hour-original",
            "sourceReference": ORIGINAL_REFERENCES[relative],
            "category": "project-original",
            "license": "GPL-3.0-only / NUKE HOUR project",
        }

    openra_data = git_bytes(openra, OPENRA_REVISION, relative)
    if openra_data is not None and openra_data == data:
        return {
            "origin": "openra-ra2",
            "sourceReference": f"{OPENRA_URL}/blob/{OPENRA_REVISION}/{relative}",
            "category": "redistributable-third-party",
            "license": "GPL-3.0-only / OpenRA RA2 project",
        }

    rv_relative = relative.replace("mods/ra2/", "mods/rv/", 1)
    rv_data = git_bytes(rv, RV_REVISION, rv_relative)
    if rv_data is not None and rv_data == data:
        return {
            "origin": "romanovs-vengeance",
            "sourceReference": f"{RV_URL}/blob/{RV_REVISION}/{rv_relative}",
            "category": "redistributable-third-party",
            "license": "GPL-3.0-only / Romanov's Vengeance",
        }

    raise RuntimeError(f"No approved byte-identical source for {relative}")


def update_inventory(path: Path, records: list[dict[str, object]]) -> None:
    payload = json.loads(path.read_text(encoding="utf-8"))
    entries = [entry for entry in payload["files"]
               if not entry["path"].startswith("mods/ra2/bits/")]
    for record in records:
        entries.append({
            "path": record["path"],
            "sha256": record["sha256"],
            "size": record["size"],
            "category": record["category"],
            "license": record["license"],
            "public": True,
        })
    payload["files"] = sorted(entries, key=lambda entry: entry["path"])
    if "trees" in payload:
        payload["trees"] = []
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--openra-repo", type=Path, default=ROOT)
    parser.add_argument("--rv-repo", type=Path, required=True)
    args = parser.parse_args()

    records = []
    for path in sorted((ROOT / "mods" / "ra2" / "bits").rglob("*")):
        if not path.is_file():
            continue
        relative = path.relative_to(ROOT).as_posix()
        data = path.read_bytes()
        record = {
            "path": relative,
            "sha256": sha256(data),
            "size": len(data),
            **classify(relative, data, args.openra_repo, args.rv_repo),
        }
        records.append(record)

    counts = {origin: sum(record["origin"] == origin for record in records) for origin in (
        "openra-ra2", "romanovs-vengeance", "nuke-hour-original")}
    expected = {"openra-ra2": 20, "romanovs-vengeance": 27, "nuke-hour-original": 5}
    if counts != expected or len(records) != 52:
        raise RuntimeError(f"Reviewed inventory changed: expected {expected}, got {counts}")

    provenance = {
        "schemaVersion": 1,
        "containsRetailGameFiles": False,
        "sourceRevisions": {
            "openra-ra2": {"url": OPENRA_URL, "revision": OPENRA_REVISION},
            "romanovs-vengeance": {"url": RV_URL, "revision": RV_REVISION},
        },
        "retailComparison": {
            "method": "Compared same-named entries extracted from user-owned RA2/YR MIX archives by SHA-256",
            "matchedNames": 7,
            "byteIdenticalFiles": 0,
            "performed": "2026-09-11",
        },
        "files": records,
    }
    output = ROOT / "docs" / "legal" / "public-bits-provenance.json"
    output.write_text(json.dumps(provenance, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    for inventory in INVENTORIES:
        update_inventory(inventory, records)
    print(f"Verified {len(records)} files: {counts}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
