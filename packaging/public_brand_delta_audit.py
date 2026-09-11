#!/usr/bin/env python3
"""Validate the scoped NUKE HOUR PublicClean branding resource delta."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from collections import Counter
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Iterable


FIXED_BASELINE_SHA256 = (
    "db6a7e5b3ee9237994b0e5dcbc9cf8129ccf1e02acb798a1b4f3aaee576c8221"
)
FIXED_BASELINE_SIZE = 72_226
FIXED_ERROR_COUNTS = {
    "unclassified-resource": 488,
    "not-public": 92,
}
REQUIRED_BASELINE_RUNTIME_PATHS = (
    "OpenRA.iOS",
    "OpenRA.iOS.aotdata.arm64",
    "OpenRA.iOS.dll",
    "OpenRA.Game.dll",
    "OpenRA.Mods.Common.dll",
    "OpenRA.Mods.RA2.dll",
    "System.Private.CoreLib.dll",
    "libmonosgen-2.0.dylib",
    "runtimeconfig.bin",
)
EXPECTED_BASELINE_PUBLIC_PATHS = {
    "mods/ra2/bits/animations/nc-impact-core.png",
    "mods/ra2/bits/animations/nc-impact-debris.png",
    "mods/ra2/bits/animations/nc-impact-ring.png",
}
FORBIDDEN_BASELINE_PATH_FRAGMENTS = (
    ".DS_Store",
    "mods/cnc/",
    "mods/ts/",
    "VERSION",
    "glsl/",
)
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")


@dataclass(frozen=True)
class SourceMapping:
    source_path: Path
    bundle_path: str
    category: str
    license: str
    public: bool


PRIVATE_DECISION = ("unknown-or-derivative", "Unreviewed", False)
PUBLIC_DECISION = ("project-original", "Project original", True)

SOURCE_MAPPINGS = (
    SourceMapping(
        Path("mods/ra2/mod.yaml"),
        "mods/ra2/mod.yaml",
        *PRIVATE_DECISION,
    ),
    SourceMapping(
        Path("mods/ra2/chrome/mainmenu.yaml"),
        "mods/ra2/chrome/mainmenu.yaml",
        *PRIVATE_DECISION,
    ),
    SourceMapping(
        Path("mods/ra2/fluent/chrome.ftl"),
        "mods/ra2/fluent/chrome.ftl",
        *PRIVATE_DECISION,
    ),
    SourceMapping(
        Path("mods/ra2/fluent/mod.ftl"),
        "mods/ra2/fluent/mod.ftl",
        *PRIVATE_DECISION,
    ),
    SourceMapping(
        Path("mods/ra2/fluent/zh-CN/chrome.ftl"),
        "mods/ra2/fluent/zh-CN/chrome.ftl",
        *PRIVATE_DECISION,
    ),
    SourceMapping(
        Path("mods/ra2/fluent/zh-CN/mod.ftl"),
        "mods/ra2/fluent/zh-CN/mod.ftl",
        *PRIVATE_DECISION,
    ),
    SourceMapping(
        Path("ios/OpenRA.iOS/en.lproj/InfoPlist.strings"),
        "en.lproj/InfoPlist.strings",
        *PUBLIC_DECISION,
    ),
    SourceMapping(
        Path("ios/OpenRA.iOS/zh-Hans.lproj/InfoPlist.strings"),
        "zh-Hans.lproj/InfoPlist.strings",
        *PUBLIC_DECISION,
    ),
)

NON_BUNDLE_ICON_PATH = Path("mods/ra2/icon.png")
NON_BUNDLE_ICON_DECISION = PRIVATE_DECISION


def _sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _safe_report_path(value: str) -> bool:
    path = PurePosixPath(value)
    return bool(value) and not path.is_absolute() and ".." not in path.parts


def validate_report(report: Any, label: str) -> tuple[str, ...]:
    errors: list[str] = []
    if not isinstance(report, dict):
        return (f"{label} report must be a JSON object",)

    passed = report.get("passed")
    report_errors = report.get("errors")
    files = report.get("files")
    if not isinstance(passed, bool):
        errors.append(f"{label} report passed must be a boolean")
    if not isinstance(report_errors, list):
        errors.append(f"{label} report errors must be a list")
    if not isinstance(files, list):
        errors.append(f"{label} report files must be a list")
    if errors:
        return tuple(errors)

    if passed != (len(report_errors) == 0):
        errors.append(f"{label} report passed flag disagrees with its errors")

    pairs: set[tuple[str, str]] = set()
    for index, error in enumerate(report_errors):
        if not isinstance(error, dict):
            errors.append(f"{label} error {index} must be an object")
            continue
        code = error.get("code")
        path = error.get("path")
        detail = error.get("detail", "")
        if not isinstance(code, str) or not code:
            errors.append(f"{label} error {index} has an invalid code")
        if not isinstance(path, str) or not _safe_report_path(path):
            errors.append(f"{label} error {index} has an invalid path")
        if not isinstance(detail, str):
            errors.append(f"{label} error {index} has an invalid detail")
        if isinstance(code, str) and code and isinstance(path, str) and path:
            pair = (code, path)
            if pair in pairs:
                errors.append(f"{label} report contains duplicate error pair {pair!r}")
            pairs.add(pair)

    file_paths: set[str] = set()
    for index, file in enumerate(files):
        if not isinstance(file, dict):
            errors.append(f"{label} file {index} must be an object")
            continue
        path = file.get("path")
        size = file.get("size")
        sha256 = file.get("sha256")
        category = file.get("category")
        license_name = file.get("license")
        if not isinstance(path, str) or not _safe_report_path(path):
            errors.append(f"{label} file {index} has an invalid path")
        elif path in file_paths:
            errors.append(f"{label} report contains duplicate file path {path!r}")
        else:
            file_paths.add(path)
        if (
            not isinstance(size, int)
            or isinstance(size, bool)
            or size < 0
        ):
            errors.append(f"{label} file {index} has an invalid size")
        if not isinstance(sha256, str) or not SHA256_PATTERN.fullmatch(sha256):
            errors.append(f"{label} file {index} has an invalid SHA-256")
        if not isinstance(category, str) or not category:
            errors.append(f"{label} file {index} has an invalid category")
        if not isinstance(license_name, str) or not license_name:
            errors.append(f"{label} file {index} has an invalid license")

    return tuple(errors)


def _error_pairs(report: dict[str, Any]) -> set[tuple[str, str]]:
    return {(error["code"], error["path"]) for error in report["errors"]}


def validate_baseline_bundle_shape(report: Any) -> tuple[str, ...]:
    errors = list(validate_report(report, "fixed baseline"))
    if errors:
        return tuple(errors)

    pairs = _error_pairs(report)
    for path in REQUIRED_BASELINE_RUNTIME_PATHS:
        if ("unclassified-resource", path) not in pairs:
            errors.append(
                "fixed baseline is missing the complete app runtime resource "
                f"{path!r}"
            )

    public_paths = {file["path"] for file in report["files"]}
    if public_paths != EXPECTED_BASELINE_PUBLIC_PATHS:
        errors.append(
            "fixed baseline public file set mismatch: expected "
            f"{sorted(EXPECTED_BASELINE_PUBLIC_PATHS)!r}, got {sorted(public_paths)!r}"
        )
    return tuple(errors)


def compare_reports(
    baseline: Any,
    candidate: Any,
) -> tuple[str, ...]:
    errors = list(validate_report(baseline, "baseline"))
    errors.extend(validate_report(candidate, "candidate"))
    if errors:
        return tuple(errors)

    baseline_pairs = _error_pairs(baseline)
    candidate_pairs = _error_pairs(candidate)
    for pair in sorted(candidate_pairs - baseline_pairs):
        errors.append(f"candidate added error pair {pair!r}")
    for pair in sorted(baseline_pairs - candidate_pairs):
        errors.append(f"candidate removed error pair {pair!r}")
    return tuple(errors)


def _read_json(path: Path, label: str) -> tuple[Any | None, tuple[str, ...]]:
    try:
        data = path.read_bytes()
    except OSError as error:
        return None, (f"unable to read {label} {path}: {error}",)
    try:
        return json.loads(data), ()
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        return None, (f"unable to parse {label} {path}: {error}",)


def validate_fixed_baseline(path: Path) -> tuple[str, ...]:
    errors: list[str] = []
    try:
        data = path.read_bytes()
    except OSError as error:
        return (f"unable to read fixed baseline {path}: {error}",)

    actual_sha256 = _sha256_bytes(data)
    if actual_sha256 != FIXED_BASELINE_SHA256:
        errors.append(
            "fixed baseline SHA-256 mismatch: "
            f"expected {FIXED_BASELINE_SHA256}, got {actual_sha256}"
        )
    if len(data) != FIXED_BASELINE_SIZE:
        errors.append(
            f"fixed baseline size mismatch: expected {FIXED_BASELINE_SIZE}, got {len(data)}"
        )

    try:
        report = json.loads(data)
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        errors.append(f"unable to parse fixed baseline: {error}")
        return tuple(errors)

    errors.extend(validate_baseline_bundle_shape(report))
    if errors:
        return tuple(errors)

    counts = Counter(error["code"] for error in report["errors"])
    if dict(counts) != FIXED_ERROR_COUNTS:
        errors.append(
            f"fixed baseline error composition mismatch: expected {FIXED_ERROR_COUNTS}, "
            f"got {dict(counts)}"
        )
    for error in report["errors"]:
        path_value = error["path"]
        if any(fragment in path_value for fragment in FORBIDDEN_BASELINE_PATH_FRAGMENTS):
            errors.append(f"fixed baseline contains forbidden personal path {path_value!r}")
        if error["code"] == "retail-extension":
            errors.append(f"fixed baseline contains retail extension {path_value!r}")
    return tuple(errors)


def _load_entries(path: Path, label: str) -> tuple[dict[str, dict[str, Any]], tuple[str, ...]]:
    payload, errors = _read_json(path, label)
    if errors:
        return {}, errors
    if not isinstance(payload, dict) or payload.get("schemaVersion") != 1:
        return {}, (f"{label} must be a schemaVersion 1 object",)
    files = payload.get("files")
    if not isinstance(files, list):
        return {}, (f"{label} files must be a list",)

    entries: dict[str, dict[str, Any]] = {}
    entry_errors: list[str] = []
    for index, entry in enumerate(files):
        if not isinstance(entry, dict):
            entry_errors.append(f"{label} entry {index} must be an object")
            continue
        path_value = entry.get("path")
        if not isinstance(path_value, str) or not _safe_report_path(path_value):
            entry_errors.append(f"{label} entry {index} has an invalid path")
            continue
        if path_value in entries:
            entry_errors.append(f"{label} contains duplicate path {path_value!r}")
            continue
        entries[path_value] = entry
    return entries, tuple(entry_errors)


def _validate_one_entry(
    source_root: Path,
    mapping: SourceMapping,
    manifest_entries: dict[str, dict[str, Any]],
    sbom_entries: dict[str, dict[str, Any]],
) -> tuple[str, ...]:
    errors: list[str] = []
    source = source_root / mapping.source_path
    if source.is_symlink() or not source.is_file():
        return (f"mapped source must be a regular non-symlink file: {mapping.source_path}",)

    data = source.read_bytes()
    actual = {
        "sha256": _sha256_bytes(data),
        "size": len(data),
        "category": mapping.category,
        "license": mapping.license,
        "public": mapping.public,
    }
    manifest_entry = manifest_entries.get(mapping.bundle_path)
    sbom_entry = sbom_entries.get(mapping.source_path.as_posix())
    if manifest_entry is None:
        errors.append(f"manifest mapping is missing: {mapping.bundle_path}")
    if sbom_entry is None:
        errors.append(f"SBOM mapping is missing: {mapping.source_path.as_posix()}")

    for label, entry in (("manifest", manifest_entry), ("SBOM", sbom_entry)):
        if entry is None:
            continue
        for field, expected in actual.items():
            if entry.get(field) != expected:
                kind = (
                    "source hash/size drift"
                    if field in {"sha256", "size"}
                    else "classification mismatch"
                )
                errors.append(
                    f"{kind} for {mapping.source_path.as_posix()} in {label}: "
                    f"{field} expected {expected!r}, got {entry.get(field)!r}"
                )
    return tuple(errors)


def validate_source_mappings(
    source_root: Path,
    manifest_path: Path,
    sbom_path: Path,
) -> tuple[str, ...]:
    manifest_entries, manifest_errors = _load_entries(manifest_path, "manifest")
    sbom_entries, sbom_errors = _load_entries(sbom_path, "SBOM")
    errors = [*manifest_errors, *sbom_errors]
    if errors:
        return tuple(errors)

    for mapping in SOURCE_MAPPINGS:
        errors.extend(
            _validate_one_entry(
                source_root,
                mapping,
                manifest_entries,
                sbom_entries,
            )
        )

    icon_mapping = SourceMapping(
        NON_BUNDLE_ICON_PATH,
        NON_BUNDLE_ICON_PATH.as_posix(),
        *NON_BUNDLE_ICON_DECISION,
    )
    errors.extend(
        _validate_one_entry(
            source_root,
            icon_mapping,
            manifest_entries,
            sbom_entries,
        )
    )
    return tuple(errors)


def main(argv: Iterable[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline-report", type=Path, required=True)
    parser.add_argument("--candidate-report", type=Path, required=True)
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--sbom", type=Path, required=True)
    arguments = parser.parse_args(argv)

    errors = list(validate_fixed_baseline(arguments.baseline_report))
    baseline, baseline_read_errors = _read_json(arguments.baseline_report, "baseline report")
    candidate, candidate_read_errors = _read_json(arguments.candidate_report, "candidate report")
    errors.extend(baseline_read_errors)
    errors.extend(candidate_read_errors)
    errors.extend(
        validate_source_mappings(
            arguments.source_root,
            arguments.manifest,
            arguments.sbom,
        )
    )
    if not baseline_read_errors and not candidate_read_errors:
        errors.extend(compare_reports(baseline, candidate))

    for error in errors:
        print(error, file=sys.stderr)
    if errors:
        return 2

    print("PublicClean NUKE HOUR brand delta audit passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
