#!/usr/bin/env python3

import argparse
import fnmatch
import gzip
import hashlib
import json
import os
import re
import shutil
import stat
import subprocess
import sys
import tarfile
import tempfile
from pathlib import Path, PurePosixPath

SCRIPT_DIRECTORY = Path(__file__).resolve().parent
if str(SCRIPT_DIRECTORY) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIRECTORY))

from audit_server_bundle import INVENTORY_NAME, audit_bundle, create_inventory


SUPPORTED_RIDS = ("linux-x64", "linux-arm64")
SERVER_DATA_SUFFIXES = {".yaml", ".ftl", ".lua"}
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


def validate_rid(rid):
    if rid not in SUPPORTED_RIDS:
        raise BuildError(f"unsupported server RID: {rid!r}")
    return rid


def validate_version(version):
    if not SAFE_VERSION.fullmatch(version or ""):
        raise BuildError(f"unsafe artifact version: {version!r}")
    return version


def is_server_data_file(relative):
    path = Path(relative)
    if "chrome" in path.parts or path.name in {"chrome.yaml", "metrics.yaml", "cursors.yaml"}:
        return False
    if path.suffix.lower() in SERVER_DATA_SUFFIXES:
        return True
    if path.name in {"AUTHORS", "COPYING", "LICENSE", "VERSION"}:
        return True
    return path.suffix.lower() == ".bin" and "maps" in path.parts


def copy_regular_file(source, destination, mode=None):
    source = Path(source)
    destination = Path(destination)
    if source.is_symlink() or not source.is_file():
        raise BuildError(f"source must be a regular non-symlink file: {source}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, destination)
    os.chmod(destination, mode if mode is not None else 0o644)


def run_checked(command, cwd):
    result = subprocess.run(
        [str(item) for item in command],
        cwd=cwd,
        check=False,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        env={**os.environ, "DOTNET_ROLL_FORWARD": "LatestMajor"},
    )
    if result.returncode:
        raise BuildError(
            f"command failed ({result.returncode}): {' '.join(map(str, command))}\n{result.stdout}"
        )
    return result.stdout


def copy_filtered_tree(source_root, destination_root, logical_prefix):
    source_root = Path(source_root)
    if not source_root.is_dir():
        raise BuildError(f"missing server data root: {source_root}")
    copied = []
    for source in sorted(source_root.rglob("*")):
        if source.is_dir():
            continue
        relative = source.relative_to(source_root)
        logical = Path(logical_prefix) / relative
        if is_server_data_file(logical):
            destination = Path(destination_root) / relative
            copy_regular_file(source, destination)
            copied.append(logical.as_posix())
    return copied


def copy_required_mod_assemblies(source_directory, destination_directory, names):
    source_directory = Path(source_directory)
    destination_directory = Path(destination_directory)
    sources = []
    for name in names:
        relative = validate_relative_path(name)
        if len(relative.parts) != 1 or relative.suffix.lower() != ".dll":
            raise BuildError(f"invalid required mod assembly name: {name!r}")
        source = source_directory / relative
        if source.is_symlink() or not source.is_file():
            raise BuildError(f"missing required mod assembly: {source}")
        sources.append((source, destination_directory / relative))

    for source, destination in sources:
        copy_regular_file(source, destination)


def copy_required_mod_dependency_manifests(source_directory, destination_directory, names):
    source_directory = Path(source_directory)
    destination_directory = Path(destination_directory)
    sources = []
    for name in names:
        relative = validate_relative_path(name)
        if len(relative.parts) != 1 or not relative.name.endswith(".deps.json"):
            raise BuildError(f"invalid mod dependency manifest name: {name!r}")
        source = source_directory / relative
        if source.is_symlink() or not source.is_file():
            raise BuildError(f"missing mod dependency manifest: {source}")
        sources.append((source, destination_directory / relative))

    for source, destination in sources:
        copy_regular_file(source, destination)


def publish_managed_server(repository, publish_directory, rid):
    publish_directory = Path(publish_directory)
    publish_directory.mkdir(parents=True, exist_ok=True)
    common = (
        "-c",
        "Release",
        "-r",
        rid,
        "--self-contained",
        "true",
        "--nologo",
        "-p:PublishSingleFile=false",
        "-p:PublishTrimmed=false",
        "-p:NuGetAudit=false",
        f"-p:TargetPlatform={rid}",
        f"-p:PublishDir={publish_directory}",
    )
    run_checked(
        ("dotnet", "publish", "OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj", *common),
        repository,
    )
    run_checked(
        ("dotnet", "publish", "engine/OpenRA.Server/OpenRA.Server.csproj", *common),
        repository,
    )


def write_json(path, document):
    Path(path).write_text(
        json.dumps(document, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )
    os.chmod(path, 0o644)


def git_source_metadata(repository):
    commit = run_checked(("git", "rev-parse", "HEAD"), repository).strip()
    tracked_status = run_checked(
        ("git", "status", "--porcelain", "--untracked-files=no"), repository
    ).strip()
    if tracked_status:
        raise BuildError(
            "refusing to publish from a dirty tracked tree; commit the exact server source first"
        )
    return {"commit": commit, "trackedTreeClean": True}


def copy_allowlisted_publish_files(publish_directory, destination_directory, delivery):
    publish_directory = Path(publish_directory)
    allowlist = delivery.get("publishedFileAllowlist")
    required = set(delivery.get("requiredPublishedFiles", ()))
    if not isinstance(allowlist, list) or not allowlist or not all(
        isinstance(pattern, str)
        and pattern
        and "/" not in pattern
        and "\\" not in pattern
        for pattern in allowlist
    ):
        raise BuildError("publishedFileAllowlist must contain safe basename patterns")

    copied = set()
    for source in sorted(publish_directory.iterdir()):
        if source.is_dir():
            raise BuildError(f"unexpected directory in managed publish output: {source.name}")
        if source.is_symlink() or not source.is_file():
            raise BuildError(f"unexpected publish output type: {source.name}")
        if not any(fnmatch.fnmatchcase(source.name, pattern) for pattern in allowlist):
            raise BuildError(f"published file is not explicitly allowlisted: {source.name}")
        destination = Path(destination_directory) / source.name
        executable = source.name in {"OpenRA.Server", "createdump"} or os.access(source, os.X_OK)
        copy_regular_file(source, destination, 0o755 if executable else 0o644)
        copied.add(source.name)

    missing = sorted(required - copied)
    if missing:
        raise BuildError(f"managed publish output is missing required files: {missing}")


def stage_bundle(repository, staging, publish_directory, rid, version):
    repository = Path(repository).resolve()
    staging = Path(staging)
    publish_directory = Path(publish_directory)
    (staging / "bin").mkdir(parents=True)

    delivery = json.loads(
        (repository / "server" / "server-files.json").read_text(encoding="utf-8")
    )
    copy_allowlisted_publish_files(publish_directory, staging / "bin", delivery)
    copy_required_mod_assemblies(
        repository / "engine" / "bin",
        staging / "bin",
        delivery["requiredModAssemblies"],
    )
    copy_required_mod_dependency_manifests(
        repository / "engine" / "bin",
        staging / "bin",
        delivery["requiredModDependencyManifests"],
    )

    copy_filtered_tree(repository / "mods" / "ra2", staging / "mods" / "ra2", "mods/ra2")
    copy_filtered_tree(
        repository / "engine" / "mods" / "common",
        staging / "mods" / "common",
        "mods/common",
    )
    copy_regular_file(
        repository / "engine" / "mods" / "ts" / "mod.yaml",
        staging / "mods" / "ts" / "mod.yaml",
    )
    copy_regular_file(repository / "engine" / "COPYING", staging / "licenses" / "OpenRA-GPL-3.0.txt")
    copy_regular_file(repository / "engine" / "AUTHORS", staging / "licenses" / "OpenRA-AUTHORS.txt")
    copy_regular_file(repository / "LICENSE", staging / "licenses" / "NUKE-HOUR-GPL-3.0.txt")
    copy_regular_file(repository / "engine" / "VERSION", staging / "VERSION")
    copy_regular_file(repository / "server" / "server-config.env.example", staging / "server-config.env.example")
    copy_regular_file(repository / "packaging" / "server" / "run-server.sh", staging / "run-server.sh", 0o755)
    copy_regular_file(repository / "packaging" / "server" / "healthcheck.sh", staging / "healthcheck.sh", 0o755)

    metadata = {
        "schema": 1,
        "product": delivery["product"],
        "version": version,
        **git_source_metadata(repository),
        "rid": rid,
        "protocol": delivery["protocol"],
        "commercialResourcesIncluded": "NONE",
    }
    write_json(staging / "server-build.json", metadata)
    inventory = create_inventory(staging, exclude={INVENTORY_NAME})
    write_json(staging / INVENTORY_NAME, {"schema": 1, "files": inventory})
    audit_bundle(staging)
    return metadata


def deterministic_archive(bundle, archive_path):
    bundle = Path(bundle)
    archive_path = Path(archive_path)
    with archive_path.open("wb") as raw:
        with gzip.GzipFile(filename="", mode="wb", fileobj=raw, mtime=0) as compressed:
            with tarfile.open(fileobj=compressed, mode="w", format=tarfile.PAX_FORMAT) as archive:
                for source in sorted(bundle.rglob("*")):
                    if source.is_dir():
                        continue
                    relative = Path(bundle.name) / source.relative_to(bundle)
                    info = archive.gettarinfo(str(source), arcname=relative.as_posix())
                    info.uid = 0
                    info.gid = 0
                    info.uname = "root"
                    info.gname = "root"
                    info.mtime = 0
                    with source.open("rb") as stream:
                        archive.addfile(info, stream)


def sha256_file(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def build(repository, output_directory, rid, version):
    repository = Path(repository).resolve()
    output_directory = Path(output_directory).resolve()
    validate_rid(rid)
    validate_version(version)
    if not (repository / ".git").exists():
        raise BuildError(f"repository is not a Git checkout: {repository}")
    output_directory.mkdir(parents=True, exist_ok=True)

    artifact_name = f"NUKE-HOUR-Server-{version}-{rid}"
    final_bundle = output_directory / artifact_name
    final_archive = output_directory / f"{artifact_name}.tar.gz"
    final_sha = output_directory / f"{artifact_name}.tar.gz.sha256"
    for target in (final_bundle, final_archive, final_sha):
        if target.exists():
            raise BuildError(f"refusing to overwrite existing artifact: {target}")

    with tempfile.TemporaryDirectory(prefix="nukehour-server-build-") as temporary:
        temporary_root = Path(temporary)
        publish = temporary_root / "publish"
        staging = temporary_root / artifact_name
        publish_managed_server(repository, publish, rid)
        metadata = stage_bundle(repository, staging, publish, rid, version)
        shutil.copytree(staging, final_bundle)
        deterministic_archive(staging, final_archive)

    digest = sha256_file(final_archive)
    final_sha.write_text(f"{digest}  {final_archive.name}\n", encoding="utf-8")
    return {
        **metadata,
        "bundle": str(final_bundle),
        "archive": str(final_archive),
        "sha256": digest,
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description="Build an audited NUKE HOUR dedicated server")
    parser.add_argument("--repository", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--rid", choices=SUPPORTED_RIDS, default="linux-x64")
    parser.add_argument("--version", required=True)
    args = parser.parse_args(argv)

    try:
        result = build(args.repository, args.output_dir, args.rid, args.version)
        sys.stdout.write(json.dumps(result, indent=2, sort_keys=True) + "\n")
        return 0
    except BuildError as error:
        sys.stderr.write(f"server build failed: {error}\n")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
