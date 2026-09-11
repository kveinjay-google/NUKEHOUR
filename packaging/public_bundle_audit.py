#!/usr/bin/env python3
"""Deny-by-default resource audit for public iOS application bundles."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import zipfile
from dataclasses import asdict, dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Iterable


RETAIL_EXTENSIONS = {
    ".aud", ".bag", ".bik", ".hva", ".idx", ".mix", ".shp", ".vqa", ".vxl", ".wsa",
}
REVIEWABLE_PUBLIC_RENDER_EXTENSIONS = {".shp"}
PUBLIC_CATEGORIES = {
    "engine-source", "project-original", "redistributable-third-party",
}
GENERATED_BUNDLE_PATHS = {
    "AppIcon60x60@2x.png",
    "AppIcon76x76@2x~ipad.png",
    "Assets.car",
    "Info.plist",
    "PkgInfo",
    "embedded.mobileprovision",
    "OpenRA.iOS",
    "aot-instances.aotdata.arm64",
    "icudt.dat",
    "runtimeconfig.bin",
    "archived-expanded-entitlements.xcent",
}


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
    errors: tuple[AuditError, ...]
    files: tuple[AuditedFile, ...]

    @property
    def passed(self) -> bool:
        return not self.errors


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _manifest_entries(manifest: dict[str, Any]) -> dict[str, dict[str, Any]]:
    if manifest.get("schemaVersion") != 1:
        raise ValueError("public content manifest must use schemaVersion 1")

    entries: dict[str, dict[str, Any]] = {}
    for entry in manifest.get("files", []):
        path = PurePosixPath(entry["path"]).as_posix()
        if path.startswith("../") or path.startswith("/"):
            raise ValueError(f"manifest path escapes audit root: {path}")
        if path in entries:
            raise ValueError(f"duplicate manifest path: {path}")
        entries[path] = entry
    return entries


def _manifest_trees(manifest: dict[str, Any]) -> dict[str, dict[str, Any]]:
    trees: dict[str, dict[str, Any]] = {}
    for entry in manifest.get("trees", []):
        path = PurePosixPath(entry["path"]).as_posix().strip("/")
        if not path or path == "." or path.startswith("../"):
            raise ValueError(f"manifest tree path escapes or covers the audit root: {path}")
        if path in trees:
            raise ValueError(f"duplicate manifest tree path: {path}")
        trees[path] = entry
    return trees


def tree_inventory(root: Path | str) -> dict[str, Any]:
    """Return a stable digest for every regular file below a reviewed resource tree."""
    tree_root = Path(root)
    digest = hashlib.sha256()
    file_count = 0
    total_size = 0
    if tree_root.is_dir():
        for path in sorted(tree_root.rglob("*"), key=lambda candidate: candidate.as_posix()):
            if not path.is_file() or path.is_symlink():
                continue
            relative = path.relative_to(tree_root).as_posix()
            size = path.stat().st_size
            file_hash = sha256_file(path)
            digest.update(relative.encode("utf-8"))
            digest.update(b"\0")
            digest.update(str(size).encode("ascii"))
            digest.update(b"\0")
            digest.update(file_hash.encode("ascii"))
            digest.update(b"\n")
            file_count += 1
            total_size += size

    return {
        "sha256": digest.hexdigest(),
        "size": total_size,
        "fileCount": file_count,
    }


def _is_generated_bundle_path(relative: str) -> bool:
    if relative in GENERATED_BUNDLE_PATHS or relative.startswith("_CodeSignature/"):
        return True

    # .NET for iOS emits its managed assemblies, AOT metadata, native runtime
    # libraries, and assembly configuration files at the application root.
    # Restrict this exemption to top-level files so lookalikes under mod/resource
    # trees remain deny-by-default.
    if "/" in relative:
        return False

    return relative.endswith((".dll", ".dll.config", ".aotdata.arm64", ".dylib"))


def inventory_tree(root: Path | str, prefix: str = "") -> list[dict[str, Any]]:
    """Create a conservative manifest fragment without granting public rights."""
    inventory_root = Path(root)
    normalized_prefix = PurePosixPath(prefix).as_posix().strip(".")
    entries: list[dict[str, Any]] = []
    for path in sorted(inventory_root.rglob("*"), key=lambda candidate: candidate.as_posix()):
        if not path.is_file() or path.is_symlink():
            continue
        relative = path.relative_to(inventory_root).as_posix()
        manifest_path = f"{normalized_prefix}/{relative}" if normalized_prefix else relative
        entries.append({
            "path": manifest_path,
            "sha256": sha256_file(path),
            "size": path.stat().st_size,
            "category": "unknown-or-derivative",
            "license": "Unreviewed",
            "public": False,
        })
    return entries


def audit_tree(root: Path | str, manifest: dict[str, Any]) -> AuditResult:
    audit_root = Path(root).resolve()
    entries = _manifest_entries(manifest)
    trees = _manifest_trees(manifest)
    errors: list[AuditError] = []
    files: list[AuditedFile] = []

    valid_trees: dict[str, dict[str, Any]] = {}
    for relative, entry in trees.items():
        category = entry.get("category", "")
        if not entry.get("public", False) or category not in PUBLIC_CATEGORIES:
            errors.append(AuditError("not-public", relative, category))
            continue

        tree_root = audit_root / relative
        if not tree_root.is_dir():
            errors.append(AuditError("manifest-tree-missing", relative))
            continue

        actual = tree_inventory(tree_root)
        expected = {
            "sha256": entry.get("sha256", "").lower(),
            "size": entry.get("size"),
            "fileCount": entry.get("fileCount"),
        }
        if actual != expected:
            errors.append(AuditError(
                "tree-inventory-mismatch", relative,
                f"expected {expected!r}, got {actual!r}"))
            continue

        valid_trees[relative] = entry

    for path in sorted(Path(root).rglob("*"), key=lambda candidate: candidate.as_posix()):
        if path.is_dir() and not path.is_symlink():
            continue

        relative = path.relative_to(root).as_posix()
        if path.is_symlink():
            try:
                path.resolve(strict=True).relative_to(audit_root)
            except (FileNotFoundError, ValueError):
                errors.append(AuditError("unsafe-symlink", relative, "target escapes audit root or is missing"))
                continue

        if not path.is_file():
            continue

        tree_entry = next((entry for tree, entry in valid_trees.items()
            if relative.startswith(f"{tree}/")), None)
        direct_entry = entries.get(relative)
        entry = direct_entry or tree_entry
        suffix = path.suffix.lower()
        reviewed_direct_asset = (
            direct_entry is not None
            and direct_entry.get("public", False)
            and direct_entry.get("category", "") == "redistributable-third-party"
        )
        reviewed_render_asset = (
            suffix in REVIEWABLE_PUBLIC_RENDER_EXTENSIONS
            and (tree_entry is not None or reviewed_direct_asset)
        )
        if suffix in RETAIL_EXTENSIONS and not reviewed_render_asset:
            errors.append(AuditError("retail-extension", relative, suffix))
            continue

        if entry is None:
            if not _is_generated_bundle_path(relative):
                errors.append(AuditError("unclassified-resource", relative))
            continue

        if direct_entry is None and tree_entry is not None:
            files.append(AuditedFile(
                path=relative,
                size=path.stat().st_size,
                sha256=sha256_file(path),
                category=tree_entry.get("category", ""),
                license=tree_entry.get("license", ""),
            ))
            continue

        category = entry.get("category", "")
        if not entry.get("public", False) or category not in PUBLIC_CATEGORIES:
            errors.append(AuditError("not-public", relative, category))
            continue

        actual_size = path.stat().st_size
        expected_size = entry.get("size")
        if (
            not isinstance(expected_size, int)
            or isinstance(expected_size, bool)
            or expected_size < 0
            or actual_size != expected_size
        ):
            errors.append(AuditError(
                "size-mismatch",
                relative,
                f"expected {expected_size!r}, got {actual_size}",
            ))
            continue

        actual_hash = sha256_file(path)
        expected_hash = entry.get("sha256", "").lower()
        if actual_hash != expected_hash:
            errors.append(AuditError("hash-mismatch", relative, f"expected {expected_hash}, got {actual_hash}"))
            continue

        files.append(AuditedFile(
            path=relative,
            size=actual_size,
            sha256=actual_hash,
            category=category,
            license=entry.get("license", ""),
        ))

    required_entries = {path for path, entry in entries.items() if entry.get("public", False)}
    missing = sorted(required_entries - {file.path for file in files} - {error.path for error in errors})
    errors.extend(AuditError("manifest-file-missing", path) for path in missing)
    return AuditResult(tuple(errors), tuple(files))


def _write_json_report(path: Path, result: AuditResult) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "passed": result.passed,
        "errors": [asdict(error) for error in result.errors],
        "files": [asdict(file) for file in result.files],
    }
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _write_markdown_report(path: Path, result: AuditResult) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        "# Public Bundle Audit",
        "",
        f"Result: **{'PASS' if result.passed else 'FAIL'}**",
        "",
        f"Audited files: {len(result.files)}",
        f"Errors: {len(result.errors)}",
        "",
    ]
    if result.errors:
        lines.extend(["## Errors", ""])
        lines.extend(f"- `{error.code}` `{error.path}` {error.detail}" for error in result.errors)
        lines.append("")
    lines.extend(["## Public files", "", "| Path | Category | SHA-256 |", "|---|---|---|"])
    lines.extend(f"| `{file.path}` | {file.category} | `{file.sha256}` |" for file in result.files)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def _audit_ipa(ipa: Path, manifest: dict[str, Any]) -> AuditResult:
    import tempfile
    with tempfile.TemporaryDirectory(prefix="openra-public-ipa-") as temporary:
        with zipfile.ZipFile(ipa) as archive:
            archive.extractall(temporary)
        apps = list(Path(temporary).glob("Payload/*.app"))
        if len(apps) != 1:
            return AuditResult((AuditError("invalid-ipa", ipa.name, "expected exactly one .app"),), ())
        return audit_tree(apps[0], manifest)


def main(argv: Iterable[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, help="App or source tree to audit")
    parser.add_argument("--ipa", type=Path, help="IPA to extract and audit")
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--inventory-out", type=Path,
                        help="Write a deny-by-default inventory for --root and exit")
    parser.add_argument("--inventory-prefix", default="")
    parser.add_argument("--json-report", type=Path)
    parser.add_argument("--markdown-report", type=Path)
    arguments = parser.parse_args(argv)
    if (arguments.root is None) == (arguments.ipa is None):
        parser.error("provide exactly one of --root or --ipa")

    if arguments.inventory_out:
        if arguments.root is None:
            parser.error("--inventory-out requires --root")
        payload = {"schemaVersion": 1, "files": inventory_tree(arguments.root, arguments.inventory_prefix)}
        arguments.inventory_out.parent.mkdir(parents=True, exist_ok=True)
        arguments.inventory_out.write_text(
            json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        return 0

    if arguments.manifest is None:
        parser.error("--manifest is required unless --inventory-out is used")

    manifest = json.loads(arguments.manifest.read_text(encoding="utf-8"))
    result = audit_tree(arguments.root, manifest) if arguments.root else _audit_ipa(arguments.ipa, manifest)
    if arguments.json_report:
        _write_json_report(arguments.json_report, result)
    if arguments.markdown_report:
        _write_markdown_report(arguments.markdown_report, result)

    for error in result.errors:
        print(f"{error.code}: {error.path}: {error.detail}", file=sys.stderr)
    return 0 if result.passed else 2


if __name__ == "__main__":
    raise SystemExit(main())
