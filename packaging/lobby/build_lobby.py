#!/usr/bin/env python3

import argparse
import gzip
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tarfile
import tempfile
from pathlib import Path, PurePosixPath


SCRIPT_DIRECTORY = Path(__file__).resolve().parent
if str(SCRIPT_DIRECTORY) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIRECTORY))

from audit_lobby_bundle import INVENTORY_NAME, audit_bundle, create_inventory


SUPPORTED_RIDS = ("linux-x64", "linux-arm64")
SAFE_VERSION = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")


class BuildError(RuntimeError):
    pass


def validate_relative_path(value):
    if not isinstance(value, str) or not value or "\\" in value or "\0" in value:
        raise BuildError(f"unsafe relative path: {value!r}")
    path = PurePosixPath(value)
    if path.is_absolute() or path.as_posix() != value or any(
        part in ("", ".", "..") for part in path.parts
    ):
        raise BuildError(f"unsafe relative path: {value!r}")
    return Path(value)


def validate_rid(value):
    if value not in SUPPORTED_RIDS:
        raise BuildError(f"unsupported Lobby RID: {value!r}")
    return value


def validate_version(value):
    if not SAFE_VERSION.fullmatch(value or ""):
        raise BuildError(f"unsafe artifact version: {value!r}")
    return value


def copy_regular(source, destination, mode=0o644):
    source = Path(source)
    if source.is_symlink() or not source.is_file():
        raise BuildError(f"source must be a regular file: {source}")
    destination = Path(destination)
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, destination)
    os.chmod(destination, mode)


def write_json(path, value):
    Path(path).write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    os.chmod(path, 0o644)


def stage_bundle(repository, destination, rid, version, commit):
    repository = Path(repository).resolve()
    destination = Path(destination)
    validate_rid(rid)
    validate_version(version)
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise BuildError("commit must be a full lowercase Git SHA-1")
    destination.mkdir(parents=True, exist_ok=False)

    source_root = repository / "lobby" / "nukehour_lobby"
    for source in sorted(source_root.glob("*.py")):
        copy_regular(source, destination / "src" / "nukehour_lobby" / source.name)
    copy_regular(repository / "lobby" / "requirements.txt", destination / "requirements.txt")
    copy_regular(repository / "lobby" / "lobby-config.env.example", destination / "lobby-config.env.example")
    copy_regular(repository / "lobby" / "run-lobby.sh", destination / "run-lobby.sh", 0o755)
    copy_regular(repository / "LICENSE", destination / "licenses" / "NUKE-HOUR-GPL-3.0.txt")
    write_json(destination / "lobby-build.json", {
        "schema": 1,
        "product": "NUKE HOUR Lobby Service",
        "version": version,
        "commit": commit,
        "rid": rid,
        "platform": "linux",
        "commercialResourcesIncluded": "NONE",
    })
    write_json(destination / INVENTORY_NAME, {
        "schema": 1,
        "files": create_inventory(destination, exclude={INVENTORY_NAME}),
    })
    audit_bundle(destination)


def deterministic_archive(bundle, archive_path):
    bundle = Path(bundle)
    with Path(archive_path).open("wb") as raw:
        with gzip.GzipFile(filename="", mode="wb", fileobj=raw, mtime=0) as compressed:
            with tarfile.open(fileobj=compressed, mode="w", format=tarfile.PAX_FORMAT) as archive:
                for source in sorted(bundle.rglob("*")):
                    if source.is_dir():
                        continue
                    info = archive.gettarinfo(
                        str(source), arcname=(Path(bundle.name) / source.relative_to(bundle)).as_posix()
                    )
                    info.uid = info.gid = 0
                    info.uname = info.gname = "root"
                    info.mtime = 0
                    with source.open("rb") as stream:
                        archive.addfile(info, stream)


def sha256_file(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def repository_commit(repository):
    result = subprocess.run(
        ["git", "rev-parse", "HEAD"], cwd=repository, check=True,
        text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    )
    return result.stdout.strip()


def build(repository, output_directory, rid, version):
    repository = Path(repository).resolve()
    output_directory = Path(output_directory).resolve()
    validate_rid(rid)
    validate_version(version)
    commit = repository_commit(repository)
    output_directory.mkdir(parents=True, exist_ok=True)
    name = f"NUKE-HOUR-Lobby-{version}-{rid}"
    bundle = output_directory / name
    archive = output_directory / f"{name}.tar.gz"
    checksum = output_directory / f"{name}.tar.gz.sha256"
    for target in (bundle, archive, checksum):
        if target.exists():
            raise BuildError(f"refusing to overwrite existing artifact: {target}")
    with tempfile.TemporaryDirectory(prefix="nukehour-lobby-build-") as temporary:
        staged = Path(temporary) / name
        stage_bundle(repository, staged, rid, version, commit)
        shutil.copytree(staged, bundle)
        deterministic_archive(staged, archive)
    digest = sha256_file(archive)
    checksum.write_text(f"{digest}  {archive.name}\n", encoding="utf-8")
    return {"bundle": str(bundle), "archive": str(archive), "sha256": digest}


def main(argv=None):
    parser = argparse.ArgumentParser(description="Build an audited NUKE HOUR Lobby bundle")
    parser.add_argument("--repository", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--rid", choices=SUPPORTED_RIDS, required=True)
    parser.add_argument("--version", required=True)
    args = parser.parse_args(argv)
    try:
        print(json.dumps(build(args.repository, args.output_dir, args.rid, args.version), indent=2))
        return 0
    except (BuildError, OSError, subprocess.SubprocessError) as exc:
        print(f"Lobby build failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
