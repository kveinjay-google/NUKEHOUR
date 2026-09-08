#!/usr/bin/env python3
"""Resolve the Build 4 UNKNOWN asset ledger rows from immutable evidence."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import subprocess
from pathlib import Path, PurePosixPath


LEDGER_HEADER = (
    "scope", "classification", "sha256", "size", "bundled_in_build4", "path"
)

# These files arrived together in the local integration commit without source,
# license, or generation records.  Three same-named cameos were compared with
# the user's legally installed Yuri archives: their containers are identical in
# size but their bytes differ.  They therefore remain fail-closed rather than
# being mislabeled as project-created.
UNRESOLVED_LOCAL_BINARY_PREFIXES = (
    "mods/ra2/bits/cameos/",
    "mods/ra2/bits/structures/ya",
    "mods/ra2/bits/structures/yg",
)
UNRESOLVED_LOCAL_BINARY_NAMES = {
    "diskicon.shp", "ggunicon.shp", "gtncicon.shp", "slavicon.shp",
    "ydomicon-cn.png", "ydomicon.shp", "yfixicon-cn.png", "yfixicon.shp",
    "yhvricon.shp", "yrcyicon.png", "ytnkicon.shp",
}
LOCAL_AUTHOR_EMAIL = "kevin@users.noreply.github.com"


def run_git(repo: Path, *arguments: str, check: bool = True) -> str:
    result = subprocess.run(
        ["git", "-C", str(repo), *arguments],
        check=check,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    return result.stdout.rstrip("\n")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def parse_ledger(path: Path) -> tuple[list[str], list[list[str]]]:
    comments: list[str] = []
    rows: list[list[str]] = []
    with path.open(encoding="utf-8", newline="") as stream:
        for raw in stream:
            if raw.startswith("#") or not raw.strip():
                comments.append(raw.rstrip("\n"))
                continue
            row = raw.rstrip("\n").split("\t")
            if len(row) != len(LEDGER_HEADER):
                raise ValueError(f"invalid ledger row: {raw!r}")
            rows.append(row)
    return comments, rows


def reachable_blobs(repo: Path, revision: str) -> set[str]:
    output = run_git(repo, "rev-list", "--objects", revision)
    return {line.split(" ", 1)[0] for line in output.splitlines() if line}


def origin(repo: Path, relative: str) -> tuple[str, str, str, str]:
    output = run_git(
        repo, "log", "--diff-filter=AMR", "--follow", "-1",
        "--format=%H%x09%an%x09%ae%x09%s", "--", relative,
    )
    parts = output.split("\t", 3)
    return tuple(parts) if len(parts) == 4 else ("", "", "", "")


def is_dirty(repo: Path, relative: str) -> bool:
    return subprocess.run(
        ["git", "-C", str(repo), "diff", "--quiet", "--", relative],
        check=False,
    ).returncode != 0


def unresolved_local_binary(relative: str, commit: str) -> bool:
    if commit != "05e614e2497c99185a3abc9d244cd285241916b0":
        return False
    path = PurePosixPath(relative)
    if relative.startswith("mods/ra2/bits/cameos/"):
        return path.name in UNRESOLVED_LOCAL_BINARY_NAMES
    return any(relative.startswith(prefix) for prefix in UNRESOLVED_LOCAL_BINARY_PREFIXES)


def public_bundle_sources(repo: Path) -> dict[str, str]:
    """Map source-tree paths to the exact PublicClean bundle projection."""
    mapped: dict[str, str] = {}
    for source_root, bundle_root in (
        ("engine/mods/common", "mods/common"),
        ("engine/mods/common-content", "mods/common-content"),
        ("mods/ra2-content", "mods/ra2-content"),
    ):
        root = repo / source_root
        if not root.is_dir():
            continue
        for source in root.rglob("*"):
            if source.is_file() and not source.is_symlink() and source.name != ".DS_Store":
                source_relative = source.relative_to(repo).as_posix()
                bundle_relative = (PurePosixPath(bundle_root) / source.relative_to(root).as_posix()).as_posix()
                mapped[bundle_relative] = source_relative

    ra2 = repo / "mods/ra2"
    patterns = (
        "*.yaml", "AUTHORS", "audio/**/*.yaml", "bits/animations/nc-impact-*.png",
        "chrome/**/*.yaml", "fluent/**/*.ftl", "rules/**/*.yaml",
        "sequences/**/*.yaml", "tilesets/**/*.yaml", "weapons/**/*.yaml",
    )
    removed = {
        "mods/ra2/audio/voices-base.yaml",
        "mods/ra2/mod.ios-base.yaml",
        "mods/ra2/mod.ios-yuri.yaml",
        "mods/ra2/rules/allied-infantry-base.yaml",
        "mods/ra2/rules/defaults-base.yaml",
        "mods/ra2/rules/proxy-actors-base.yaml",
        "mods/ra2/rules/soviet-infantry-base.yaml",
        "mods/ra2/rules/soviet-vehicles-base.yaml",
        "mods/ra2/rules/world-base.yaml",
        "mods/ra2/sequences/vehicles-base.yaml",
        "mods/ra2/sequences/voxels-base.yaml",
    }
    for pattern in patterns:
        for source in ra2.glob(pattern):
            if source.is_file() and not source.is_symlink() and source.name != ".DS_Store":
                relative = source.relative_to(repo).as_posix()
                if relative not in removed:
                    mapped[relative] = relative
    return mapped


def classify(
    repo: Path,
    relative: str,
    upstream_blobs: set[str],
) -> tuple[str, str, str]:
    blob = run_git(repo, "hash-object", "--", relative)
    if blob in upstream_blobs:
        return "OPENRA_RA2_UPSTREAM", "yes", f"exact Git blob {blob} reachable from upstream/master"

    if is_dirty(repo, relative):
        return (
            "PROJECT_CREATED", "yes",
            "Build 4 recovered dirty-tree bytes; local project delta authenticated by Build 4 evidence",
        )

    if relative in {
        "mods/ra2/fonts/NotoSansCJKsc-Bold.otf",
        "mods/ra2/fonts/NotoSansCJKsc-Regular.otf",
    }:
        return (
            "THIRD_PARTY_OPEN_SOURCE", "yes",
            "Noto Sans CJK SC 2.004; embedded SIL Open Font License 1.1 and mods/ra2/fonts/OFL.txt",
        )

    commit, author, email, subject = origin(repo, relative)
    if unresolved_local_binary(relative, commit):
        return (
            "UNKNOWN", "no",
            f"local commit {commit}; no source/license record; fail-closed as possible retail derivative",
        )

    if email != LOCAL_AUTHOR_EMAIL:
        return (
            "OPENRA_RA2_UPSTREAM", "yes",
            f"OpenRA/ra2 repository commit {commit} by {author}: {subject}",
        )

    if subject.startswith("Cherry-pick upstream PR #"):
        return "OPENRA_RA2_UPSTREAM", "yes", f"{commit}: {subject}"

    return "PROJECT_CREATED", "yes", f"local project commit {commit}: {subject}"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path.cwd())
    parser.add_argument("--ledger", type=Path, required=True)
    parser.add_argument("--provenance-out", type=Path, required=True)
    parser.add_argument("--public-manifest", type=Path)
    parser.add_argument("--upstream-revision", default="upstream/master")
    arguments = parser.parse_args()

    repo = arguments.repo.resolve()
    comments, rows = parse_ledger(arguments.ledger)
    blobs = reachable_blobs(repo, arguments.upstream_revision)
    managed_paths: set[str] = set()
    if arguments.provenance_out.is_file():
        with arguments.provenance_out.open(encoding="utf-8", newline="") as stream:
            managed_paths = {row["path"] for row in csv.DictReader(stream, delimiter="\t")}
    bundled_paths = {
        row[5]: row[2]
        for row in rows
        if row[0] == "BUILD4_BUNDLE"
    }

    evidence_rows: list[tuple[str, ...]] = []
    updated: list[list[str]] = []
    decisions: dict[tuple[str, str], tuple[str, str, str]] = {}
    for row in rows:
        scope, classification, sha256, size, old_bundled, relative = row
        exact_bundled = bundled_paths.get(relative) == sha256
        if scope == "SOURCE":
            row[4] = "yes" if exact_bundled else "no"
        if classification != "UNKNOWN" and relative not in managed_paths:
            updated.append(row)
            continue

        key = (relative, sha256)
        decision = decisions.get(key)
        if decision is None:
            if not (repo / relative).is_file():
                decision = ("UNKNOWN", "no", "path is not present in recovered source tree")
            else:
                if sha256_file(repo / relative) != sha256:
                    raise ValueError(f"ledger SHA-256 is stale for {relative}")
                decision = classify(repo, relative, blobs)
            decisions[key] = decision

        resolved, publish, evidence = decision
        row[1] = resolved
        updated.append(row)
        if scope == "SOURCE":
            evidence_rows.append((
                relative, sha256, size, "yes" if exact_bundled else "no",
                resolved, publish, evidence,
            ))

    with arguments.ledger.open("w", encoding="utf-8", newline="") as stream:
        stream.write("# " + "\t".join(LEDGER_HEADER) + "\n")
        for row in updated:
            stream.write("\t".join(row) + "\n")

    arguments.provenance_out.parent.mkdir(parents=True, exist_ok=True)
    with arguments.provenance_out.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.writer(stream, delimiter="\t", lineterminator="\n")
        writer.writerow((
            "path", "sha256", "size", "bundled_in_build4", "classification",
            "included_in_public_tree", "evidence",
        ))
        writer.writerows(sorted(evidence_rows))

    if arguments.public_manifest:
        category_names = {
            "OPENRA_UPSTREAM": ("engine-source", "OpenRA GPLv3+"),
            "OPENRA_RA2_UPSTREAM": ("engine-source", "OpenRA/ra2 GPLv3+"),
            "PROJECT_CREATED": ("project-original", "Project original; GPLv3+ distribution"),
            "THIRD_PARTY_OPEN_SOURCE": ("redistributable-third-party", "Upstream open-source license"),
            "UNKNOWN": ("unknown-or-derivative", "Unreviewed"),
        }
        source_classifications = {
            row[5]: row[1] for row in updated if row[0] == "SOURCE"
        }
        source_classifications["mods/ra2/AUTHORS"] = "OPENRA_RA2_UPSTREAM"
        manifest = json.loads(arguments.public_manifest.read_text(encoding="utf-8"))
        entries = {entry["path"]: entry for entry in manifest.get("files", [])}
        for entry in manifest.get("files", []):
            if entry["path"].startswith("mods/"):
                entry["public"] = False

        for bundle_relative, source_relative in public_bundle_sources(repo).items():
            classification = source_classifications.get(source_relative)
            if classification is None and source_relative.startswith("engine/mods/"):
                classification = "OPENRA_UPSTREAM"
            elif classification is None and source_relative.startswith("mods/ra2-content/"):
                classification = "OPENRA_RA2_UPSTREAM"
            if classification in {None, "UNKNOWN", "ORIGINAL_RA2_ASSET"}:
                continue
            category, license_name = category_names[classification]
            source = repo / source_relative
            entry = entries.get(bundle_relative)
            if entry is None:
                entry = {"path": bundle_relative}
                manifest["files"].append(entry)
                entries[bundle_relative] = entry
            entry["sha256"] = sha256_file(source)
            entry["size"] = source.stat().st_size
            entry["category"] = category
            entry["license"] = license_name
            entry["public"] = True
        manifest["files"].sort(key=lambda entry: entry["path"])
        arguments.public_manifest.write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
