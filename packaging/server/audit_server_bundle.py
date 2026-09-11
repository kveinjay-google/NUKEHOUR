#!/usr/bin/env python3

import argparse
import fnmatch
import hashlib
import json
import os
import shutil
import stat
import sys
import tarfile
import tempfile
from pathlib import Path, PurePosixPath


INVENTORY_NAME = "server-inventory.json"
FORBIDDEN_NAMES = {
    "ra2.mix",
    "language.mix",
    "ra2md.mix",
    "langmd.mix",
    "gamemd.exe",
    "game.exe",
    "ra2.exe",
}
FORBIDDEN_PATTERNS = ("*.mix", "*.bag", "*.aud", "*.vqa", "*.exe")
SECRET_PATTERNS = (
    "*.key",
    "*.pem",
    "id_rsa*",
    ".env",
    ".env.*",
    "*credentials*",
    "*secret*",
)
ROOT_FILES = {
    "VERSION",
    "healthcheck.sh",
    "run-server.sh",
    "server-build.json",
    "server-config.env.example",
}
LICENSE_FILES = {
    "licenses/NUKE-HOUR-GPL-3.0.txt",
    "licenses/OpenRA-AUTHORS.txt",
    "licenses/OpenRA-GPL-3.0.txt",
}
BIN_FILE_PATTERNS = (
    "AUTHORS",
    "BeaconLib.dll",
    "DiscordRPC.dll",
    "Eluant.dll",
    "Eluant.dll.config",
    "FuzzyLogicLibrary.dll",
    "ICSharpCode.SharpZipLib.dll",
    "Linguini.Bundle.dll",
    "Linguini.Shared.dll",
    "Linguini.Syntax.dll",
    "MP3Sharp.dll",
    "Microsoft.*.dll",
    "Mono.Nat.dll",
    "NVorbis.dll",
    "Newtonsoft.Json.dll",
    "OpenRA.Game.dll",
    "OpenRA.Game.pdb",
    "OpenRA.Mods.Cnc.deps.json",
    "OpenRA.Mods.Cnc.dll",
    "OpenRA.Mods.Common.deps.json",
    "OpenRA.Mods.Common.dll",
    "OpenRA.Mods.RA2.deps.json",
    "OpenRA.Mods.RA2.dll",
    "OpenRA.Mods.RA2.pdb",
    "OpenRA.Server",
    "OpenRA.Server.deps.json",
    "OpenRA.Server.dll",
    "OpenRA.Server.pdb",
    "OpenRA.Server.runtimeconfig.json",
    "Pfim.dll",
    "System.*.dll",
    "System.dll",
    "TagLibSharp.dll",
    "WindowsBase.dll",
    "createdump",
    "libSystem.*.so",
    "libclrjit.so",
    "libcoreclr.so",
    "libcoreclrtraceptprovider.so",
    "libdbgshim.so",
    "libhostfxr.so",
    "libhostpolicy.so",
    "libmscordaccore.so",
    "libmscordbi.so",
    "lua51.so",
    "mscorlib.dll",
    "netstandard.dll",
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
        raise AuditError(f"server bundle is not a directory: {root}")

    for current, directories, filenames in os.walk(root, followlinks=False):
        current_path = Path(current)
        for name in list(directories):
            directory = current_path / name
            if directory.is_symlink():
                raise AuditError(f"symlink directory is forbidden: {directory.relative_to(root)}")
        for name in filenames:
            path = current_path / name
            relative = path.relative_to(root).as_posix()
            validate_relative_path(relative)
            if path.is_symlink():
                raise AuditError(f"symlink file is forbidden: {relative}")
            if not path.is_file():
                raise AuditError(f"special file is forbidden: {relative}")
            yield relative, path


def create_inventory(bundle, exclude=()):
    excluded = set(exclude)
    inventory = []
    for relative, path in sorted(iter_regular_files(bundle)):
        if relative in excluded:
            continue
        mode = stat.S_IMODE(path.stat().st_mode)
        inventory.append(
            {
                "path": relative,
                "size": path.stat().st_size,
                "sha256": sha256_file(path),
                "mode": f"{mode:04o}",
            }
        )
    return inventory


def load_inventory(path):
    try:
        document = json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise AuditError(f"invalid server inventory: {error}") from error
    if document.get("schema") != 1 or not isinstance(document.get("files"), list):
        raise AuditError("server inventory must use schema 1 and contain a files list")

    seen = set()
    for entry in document["files"]:
        if not isinstance(entry, dict):
            raise AuditError("server inventory entries must be objects")
        relative = validate_relative_path(entry.get("path"))
        if relative in seen:
            raise AuditError(f"duplicate inventory path: {relative}")
        seen.add(relative)
        if not isinstance(entry.get("size"), int) or entry["size"] < 0:
            raise AuditError(f"invalid inventory size: {relative}")
        digest = entry.get("sha256")
        if not isinstance(digest, str) or len(digest) != 64 or any(
            character not in "0123456789abcdef" for character in digest
        ):
            raise AuditError(f"invalid inventory digest: {relative}")
        if entry.get("mode") not in {"0644", "0755"}:
            raise AuditError(f"invalid inventory mode: {relative}")
    return document["files"]


def reject_commercial_or_secret_file(relative, path):
    basename = PurePosixPath(relative).name.lower()
    if basename in FORBIDDEN_NAMES or any(
        fnmatch.fnmatchcase(basename, pattern) for pattern in FORBIDDEN_PATTERNS
    ):
        raise AuditError(f"commercial resource pattern is forbidden: {relative}")
    if any(fnmatch.fnmatchcase(basename, pattern) for pattern in SECRET_PATTERNS):
        raise AuditError(f"secret-bearing file pattern is forbidden: {relative}")

    with path.open("rb") as stream:
        prefix = stream.read(32)
    if prefix.startswith(b"MZ") and Path(relative).suffix.lower() != ".dll":
        raise AuditError(f"Windows executable signature is forbidden: {relative}")
    if prefix.startswith(b"FORM") and b"WVQA" in prefix:
        raise AuditError(f"VQA signature is forbidden: {relative}")


def reject_non_allowlisted_path(relative):
    path = PurePosixPath(relative)
    if relative in ROOT_FILES or relative in LICENSE_FILES:
        return
    if len(path.parts) == 2 and path.parts[0] == "bin" and any(
        fnmatch.fnmatchcase(path.name, pattern) for pattern in BIN_FILE_PATTERNS
    ):
        return
    if path.parts[:2] in (("mods", "ra2"), ("mods", "common")):
        if "chrome" not in path.parts and path.name not in {
            "chrome.yaml", "metrics.yaml", "cursors.yaml"
        }:
            if path.suffix.lower() in {".yaml", ".ftl", ".lua"}:
                return
            if path.suffix.lower() == ".bin" and "maps" in path.parts:
                return
            if path.name in {"AUTHORS", "COPYING", "LICENSE", "VERSION"}:
                return
    if relative == "mods/ts/mod.yaml":
        return
    raise AuditError(f"artifact path is not independently allowlisted: {relative}")


def audit_bundle(bundle):
    root = Path(bundle).resolve()
    inventory_path = root / INVENTORY_NAME
    expected = load_inventory(inventory_path)
    actual = create_inventory(root, exclude={INVENTORY_NAME})
    if actual != expected:
        expected_paths = {entry["path"] for entry in expected}
        actual_paths = {entry["path"] for entry in actual}
        extra = sorted(actual_paths - expected_paths)
        missing = sorted(expected_paths - actual_paths)
        raise AuditError(
            f"bundle does not match inventory; extra={extra}; missing={missing}"
        )

    for relative, path in iter_regular_files(root):
        if relative == INVENTORY_NAME:
            continue
        reject_non_allowlisted_path(relative)
        reject_commercial_or_secret_file(relative, path)
        mode = stat.S_IMODE(path.stat().st_mode)
        if mode & 0o002:
            raise AuditError(f"world-writable artifact file is forbidden: {relative}")

    return {
        "schema": 1,
        "result": "PASS",
        "commercialResourcesIncluded": "NONE",
        "fileCount": len(actual),
        "inventorySha256": sha256_file(inventory_path),
    }


def audit_archive(archive_path):
    archive_path = Path(archive_path).resolve()
    if not archive_path.is_file() or not archive_path.name.endswith(".tar.gz"):
        raise AuditError(f"server archive must be a .tar.gz file: {archive_path}")

    with tempfile.TemporaryDirectory(prefix="nukehour-server-audit-") as temporary:
        extraction_root = Path(temporary)
        roots = set()
        seen = set()
        total_size = 0
        try:
            with tarfile.open(archive_path, mode="r:gz") as archive:
                members = archive.getmembers()
                if not members or len(members) > 100000:
                    raise AuditError("server archive has an invalid member count")

                for member in members:
                    name = validate_relative_path(member.name)
                    parts = PurePosixPath(name).parts
                    if not parts or not parts[0].startswith("NUKE-HOUR-Server-"):
                        raise AuditError(f"unexpected archive root: {name}")
                    roots.add(parts[0])
                    if name in seen:
                        raise AuditError(f"duplicate archive member: {name}")
                    seen.add(name)
                    if not (member.isdir() or member.isreg()):
                        raise AuditError(f"archive links and special files are forbidden: {name}")
                    total_size += member.size
                    if total_size > 2 * 1024 * 1024 * 1024:
                        raise AuditError("server archive exceeds the extraction size limit")

                if len(roots) != 1:
                    raise AuditError(f"server archive must contain exactly one root: {sorted(roots)}")

                for member in members:
                    destination = extraction_root.joinpath(*PurePosixPath(member.name).parts)
                    if member.isdir():
                        destination.mkdir(parents=True, exist_ok=True)
                        continue

                    destination.parent.mkdir(parents=True, exist_ok=True)
                    source = archive.extractfile(member)
                    if source is None:
                        raise AuditError(f"unable to read archive member: {member.name}")
                    with source, destination.open("xb") as output:
                        shutil.copyfileobj(source, output)
                    os.chmod(destination, stat.S_IMODE(member.mode))
        except (OSError, tarfile.TarError) as error:
            raise AuditError(f"invalid server archive: {error}") from error

        evidence = audit_bundle(extraction_root / next(iter(roots)))
        evidence["archiveSha256"] = sha256_file(archive_path)
        return evidence


def audit_artifact(path):
    path = Path(path)
    return audit_bundle(path) if path.is_dir() else audit_archive(path)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Audit a NUKE HOUR dedicated server bundle")
    parser.add_argument("bundle", type=Path)
    parser.add_argument("--evidence", type=Path)
    args = parser.parse_args(argv)

    try:
        evidence = audit_artifact(args.bundle)
        output = json.dumps(evidence, indent=2, sort_keys=True) + "\n"
        if args.evidence:
            args.evidence.parent.mkdir(parents=True, exist_ok=True)
            args.evidence.write_text(output, encoding="utf-8")
        sys.stdout.write(output)
        return 0
    except AuditError as error:
        sys.stderr.write(f"server bundle audit failed: {error}\n")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
