#!/usr/bin/env python3
"""Fail-closed materialization and patch candidate tooling for the pinned engine."""

from __future__ import annotations

import argparse
import hashlib
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import zipfile


OFFICIAL_ZIP_SHA256 = "ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a"
OFFICIAL_ROOT = "OpenRA-release-20250330"
OVERLAY_FILE_BYTES = 50 << 20
OVERLAY_AGGREGATE_BYTES = 512 << 20
ALLOWLIST_BYTES = 1 << 20
KNOWN_0007_DIAGNOSTICS = (
    "Hunk #1 succeeded at 254 with fuzz 2 (offset -1 lines).\n"
    "Hunk #5 succeeded at 352 (offset -1 lines)."
)

_ALLOWLIST_TEXT = {
    "0020": """OpenRA.Game/FileSystem/PackageEnvelopePolicy.cs
OpenRA.Game/FileSystem/PackageReadContext.cs
OpenRA.Game/FileSystem/BoundedZipEnvelope.cs
OpenRA.Game/FileSystem/IPackage.cs
OpenRA.Game/FileSystem/FileSystem.cs
OpenRA.Game/FileSystem/ZipFile.cs
OpenRA.Game/FileFormats/Png.cs
OpenRA.Game/Primitives/SegmentStream.cs
OpenRA.Mods.Cnc/FileSystem/MixEnvelopeValidator.cs
OpenRA.Mods.Cnc/FileSystem/MixFile.cs
OpenRA.Mods.Cnc/FileFormats/IdxBagEnvelopeValidator.cs
OpenRA.Mods.Cnc/FileFormats/IdxReader.cs
OpenRA.Mods.Cnc/FileFormats/XccLocalDatabase.cs
OpenRA.Mods.Cnc/FileFormats/XccGlobalDatabase.cs
OpenRA.Test/ZipEnvelopeTest.cs
OpenRA.Test/MixEnvelopeTest.cs
OpenRA.Test/IdxBagEnvelopeTest.cs
OpenRA.Test/OpenRA.Game/PngTest.cs
OpenRA.Test/RetailArchiveGraphValidatorTest.cs
OpenRA.Test/RetailContentCatalogTest.cs
OpenRA.Test/RetailSourceDiscoveryTest.cs
OpenRA.Test/RetailCandidatePlanTest.cs
OpenRA.Test/IosImportEnvelopePolicyV1Test.cs
OpenRA.Test/SyntheticRetailFixtureBuilder.cs""",
    "0021": """OpenRA.Game/FileSystem/FileSystem.cs
OpenRA.Game/Game.cs
OpenRA.Game/InstalledMods.cs
OpenRA.Game/Manifest.cs
OpenRA.Game/Map/MapCache.cs
OpenRA.Game/ModData.cs
OpenRA.Game/ModDataConstructionOptions.cs
OpenRA.Game/Platform.cs
OpenRA.Game/PreGameEngineContext.cs
OpenRA.Game/RuntimeContractIdentity.cs
OpenRA.Game/Support/Arguments.cs
OpenRA.Game/TrustedManifestSelection.cs
OpenRA.Test/ArgumentsSnapshotTest.cs
OpenRA.Test/PreGameEngineContextTest.cs
OpenRA.Test/Ra2RuntimeProfileManifestTest.cs
OpenRA.Test/RetailCandidateProfileValidationTest.cs
OpenRA.Test/RetailContentImporterTest.cs
OpenRA.Test/TrustedManifestEntryTest.cs""",
    "0022": """OpenRA.Game/Manifest.cs
OpenRA.Game/Game.cs
OpenRA.Game/Network/Handshake.cs
OpenRA.Game/Network/UnitOrders.cs
OpenRA.Game/Server/Server.cs
OpenRA.Game/Network/GameServer.cs
OpenRA.Game/Network/Session.cs
OpenRA.Mods.Common/Widgets/Logic/ServerListLogic.cs
OpenRA.Test/RuntimeContractBuilderTest.cs
OpenRA.Test/RuntimeContractHandshakeTest.cs
OpenRA.Game/RuntimeContractStorage.cs
OpenRA.Game/Settings.cs
OpenRA.Game/Network/GameSave.cs
OpenRA.Game/GameInformation.cs
OpenRA.Game/Network/ReplayRecorder.cs
OpenRA.Mods.Common/Widgets/Logic/GameSaveBrowserLogic.cs
OpenRA.Mods.Common/Widgets/Logic/ReplayBrowserLogic.cs
OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs
OpenRA.Mods.Common/ServerTraits/SkirmishLogic.cs
mods/common/fluent/chrome.ftl
mods/common/fluent/zh-CN/chrome.ftl
OpenRA.Test/RuntimeContractPersistenceTest.cs""",
    "0023": """OpenRA.Game/Map/Map.cs
OpenRA.Game/Map/MapPlayers.cs
OpenRA.Game/Map/PlayerReference.cs
OpenRA.Mods.Cnc/MapImport/Gen2MapImportModels.cs
OpenRA.Mods.Cnc/MapImport/Gen2MapImporter.cs
OpenRA.Mods.Cnc/UtilityCommands/ImportGen2MapCommand.cs
OpenRA.Mods.Cnc/UtilityCommands/ImportTiberianSunMapCommand.cs
OpenRA.Test/Gen2MapImporterTest.cs
OpenRA.Test/Ra2MapConversionServiceTest.cs""",
    "0024": """OpenRA.Game/FileFormats/Png.cs
OpenRA.Game/FileSystem/ZipFile.cs
OpenRA.Game/Graphics/PlatformInterfaces.cs
OpenRA.Game/Graphics/SheetBuilder.cs
OpenRA.Game/Map/MapUid.cs
OpenRA.Game/Map/VerifiedMapSnapshot.cs
OpenRA.Game/Map/MapPreviewImageCache.cs
OpenRA.Game/Map/ActorReference.cs
OpenRA.Game/Map/MapCache.cs
OpenRA.Game/Map/MapPreview.cs
OpenRA.Game/Renderer.cs
OpenRA.Platforms.Default/OpenGL.cs
OpenRA.Platforms.Default/Sdl2GraphicsContext.cs
OpenRA.Platforms.Default/ThreadedGraphicsContext.cs
OpenRA.Mods.Common/Widgets/MapPreviewWidget.cs
OpenRA.Mods.Common/Widgets/Logic/MapChooserLogic.cs
OpenRA.Test/OpenRA.Game/PngTest.cs
OpenRA.Test/BoundedOramapEnvelopeTest.cs
OpenRA.Test/Ra2MapValidationServiceTest.cs
OpenRA.Test/MapPreviewImageCacheTest.cs
OpenRA.Test/SheetBuilderBoundsTest.cs""",
    "0025": """OpenRA.Game/Map/RemoteMapMetadata.cs
OpenRA.Game/Map/VerifiedRemoteMapMetadataClient.cs
OpenRA.Game/Map/MapCache.cs
OpenRA.Game/Map/MapPreview.cs
OpenRA.Test/VerifiedRemoteMapMetadataClientTest.cs
OpenRA.Test/MapCacheRemoteMetadataTest.cs""",
    "0026": """OpenRA.Game/Map/RuntimeMapDownload.cs
OpenRA.Game/Map/VerifiedMapSnapshot.cs
OpenRA.Game/Map/MapCache.cs
OpenRA.Game/Map/MapPreview.cs
OpenRA.Test/VerifiedRuntimeMapStoreTest.cs
OpenRA.Test/RuntimeMapStoreRecoveryTest.cs
OpenRA.Test/MapPreviewRuntimeInstallTest.cs""",
    "0027": """OpenRA.Game/Support/IosGameContentManagement.cs
OpenRA.Game/Map/VerifiedMapSnapshot.cs
OpenRA.Game/Map/MapCache.cs
OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs
OpenRA.Test/RetailImportMetadataTest.cs
OpenRA.Test/RetailImportDurabilityTest.cs
OpenRA.Test/RetailGenerationStoreTest.cs
OpenRA.Test/RetailImportRecoveryProcessTest.cs
OpenRA.Test/RetailPendingSnapshotStoreTest.cs
OpenRA.Test/RetailLegacyMigrationTest.cs
OpenRA.Test/RetailContentImporterTransactionTest.cs
OpenRA.Test/RetailContentImporterTest.cs
OpenRA.Test/RetailImportSourceSessionContractTest.cs
OpenRA.Test/IosContentImportContractTest.cs
OpenRA.Test/RetailActiveCatalogTest.cs
OpenRA.Test/VerifiedCatalogPackageTest.cs
OpenRA.Test/RetailContentWizardStateMachineTest.cs
OpenRA.Test/IosContentWizardLayoutPolicyTest.cs
OpenRA.Test/IosGameContentManagementTest.cs
OpenRA.Test/AotObjectRegistryCoverageTest.cs""",
}

ALLOWLISTS = {
    patch_id: tuple(sorted(set(text.splitlines()), key=os.fsencode))
    for patch_id, text in _ALLOWLIST_TEXT.items()
}


class WorkspaceError(Exception):
    pass


def _absolute_lexical(path: Path) -> Path:
    path = Path(path)
    if ".." in path.parts:
        raise WorkspaceError("system path must not contain traversal")
    absolute = os.path.abspath(os.fspath(path))
    # macOS exposes these fixed system roots as compatibility symlinks. Normalize
    # them lexically so validation can reject every caller-controlled symlink
    # component without resolving the supplied path.
    if sys.platform == "darwin":
        for alias in ("/var", "/tmp", "/etc"):
            if absolute == alias or absolute.startswith(alias + os.sep):
                absolute = "/private" + absolute
                break

    return Path(absolute)


def _validate_lexical_path(path: Path, kind: str, may_be_absent: bool = False) -> Path:
    path = _absolute_lexical(path)
    current = Path(path.anchor)
    parts = path.parts[1:]
    for index, component in enumerate(parts):
        current /= component
        if not os.path.lexists(current):
            if may_be_absent:
                continue
            raise WorkspaceError(f"{kind} does not exist")
        mode = os.lstat(current).st_mode
        if stat.S_ISLNK(mode):
            raise WorkspaceError(f"{kind} contains a symlink component")
        if index < len(parts) - 1 and not stat.S_ISDIR(mode):
            raise WorkspaceError(f"{kind} has a non-directory parent component")

    if not os.path.lexists(path):
        if may_be_absent:
            return path
        raise WorkspaceError(f"{kind} does not exist")
    mode = os.lstat(path).st_mode
    if kind in ("repository", "workspace"):
        if not stat.S_ISDIR(mode):
            raise WorkspaceError(f"{kind} must be a regular directory")
    elif not stat.S_ISREG(mode):
        raise WorkspaceError(f"{kind} must be a regular file")
    return path


def _existing_directory(path: Path, kind: str) -> Path:
    return _validate_lexical_path(path, kind)


def _existing_file(path: Path, kind: str) -> Path:
    return _validate_lexical_path(path, kind)


def _output_file(path: Path) -> Path:
    return _validate_lexical_path(path, "output", may_be_absent=True)


def _output_directory(path: Path) -> Path:
    return _validate_lexical_path(path, "workspace", may_be_absent=True)


def _canonical_relative(value: str) -> str:
    if not value or "\x00" in value or "\\" in value or value.startswith("/"):
        raise WorkspaceError("noncanonical relative path")
    path = PurePosixPath(value)
    if any(part in ("", ".", "..") for part in value.split("/")) or path.is_absolute():
        raise WorkspaceError("noncanonical relative path")
    normalized = path.as_posix()
    if normalized != value or value.startswith("engine/"):
        raise WorkspaceError("allowlists must contain normalized engine-relative paths")
    return value


def read_allowlist(path: Path) -> tuple[str, ...]:
    path = _existing_file(path, "allowlist")
    raw = _regular_bytes(path, maximum_bytes=ALLOWLIST_BYTES)
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise WorkspaceError("allowlist is not UTF-8") from exc
    if "\r" in text or (text and not text.endswith("\n")):
        raise WorkspaceError("allowlist must use canonical LF records")
    lines = text.splitlines()
    for line in lines:
        _canonical_relative(line)
    expected = sorted(set(lines), key=os.fsencode)
    if lines != expected:
        raise WorkspaceError("allowlist must be byte-sorted and unique")
    return tuple(lines)


def _atomic_write(path: Path, data: bytes) -> None:
    path = _output_file(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    try:
        with os.fdopen(fd, "wb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    except BaseException:
        try:
            os.unlink(temporary)
        except FileNotFoundError:
            pass
        raise


def _descriptor_flags() -> int:
    if not hasattr(os, "O_NOFOLLOW") or not hasattr(os, "O_CLOEXEC") or not hasattr(os, "O_DIRECTORY"):
        raise WorkspaceError("platform lacks required no-follow descriptor support")
    return os.O_RDONLY | os.O_CLOEXEC | os.O_NOFOLLOW


def _open_relative_descriptor(root_descriptor: int, relative: str, *, directory: bool = False) -> tuple[int, os.stat_result]:
    relative = _canonical_relative(relative)
    current = os.dup(root_descriptor)
    os.set_inheritable(current, False)
    try:
        components = relative.split("/")
        for index, component in enumerate(components):
            final = index == len(components) - 1
            flags = _descriptor_flags()
            if not final or directory:
                flags |= os.O_DIRECTORY
            following = os.open(component, flags, dir_fd=current)
            previous = current
            current = following
            os.close(previous)

        identity = os.fstat(current)
        expected = stat.S_ISDIR(identity.st_mode) if directory else stat.S_ISREG(identity.st_mode)
        if not expected:
            raise WorkspaceError("descriptor-relative path has the wrong file type")
        result = current
        current = None
        return result, identity
    except BaseException:
        if current is not None:
            os.close(current)
        raise


def _open_absolute_descriptor(path: Path, kind: str, *, directory: bool) -> tuple[int, os.stat_result]:
    path = _existing_directory(path, kind) if directory else _existing_file(path, kind)
    root_flags = _descriptor_flags() | os.O_DIRECTORY
    root = os.open(path.anchor, root_flags)
    if path == Path(path.anchor):
        identity = os.fstat(root)
        return root, identity
    try:
        relative = path.relative_to(path.anchor).as_posix()
        descriptor, identity = _open_relative_descriptor(root, relative, directory=directory)
        return descriptor, identity
    except OSError as exc:
        raise WorkspaceError(f"{kind} unavailable") from exc
    finally:
        os.close(root)


def _open_regular_descriptor(path: Path, kind: str) -> tuple[int, os.stat_result]:
    return _open_absolute_descriptor(path, kind, directory=False)


def _open_directory_descriptor(path: Path, kind: str) -> int:
    descriptor, _ = _open_absolute_descriptor(path, kind, directory=True)
    return descriptor


def _stable_identity(value: os.stat_result) -> tuple[int, int, int, int]:
    return value.st_dev, value.st_ino, value.st_size, value.st_mtime_ns


def _extract_zip_stream(stream, destination: Path) -> Path:
    seen: set[str] = set()
    directory_targets: set[Path] = set()
    destination.mkdir(parents=True, exist_ok=True)
    try:
        archive = zipfile.ZipFile(stream)
    except (OSError, zipfile.BadZipFile) as exc:
        raise WorkspaceError("invalid official ZIP") from exc
    with archive:
        for info in archive.infolist():
            name = info.filename
            if not name or "\x00" in name or "\\" in name or name.startswith("/"):
                raise WorkspaceError("unsafe ZIP member")
            parts = name.rstrip("/").split("/")
            if not parts or parts[0] != OFFICIAL_ROOT or any(part in ("", ".", "..") for part in parts):
                raise WorkspaceError("ZIP must contain one expected official root")
            canonical = "/".join(parts)
            if canonical in seen:
                raise WorkspaceError("duplicate ZIP member")
            seen.add(canonical)
            mode = (info.external_attr >> 16) & 0xFFFF
            file_type = stat.S_IFMT(mode)
            is_directory = name.endswith("/") or file_type == stat.S_IFDIR
            if mode & (stat.S_ISUID | stat.S_ISGID | stat.S_ISVTX):
                raise WorkspaceError("ZIP contains unsafe permission bits")
            if file_type not in (0, stat.S_IFREG, stat.S_IFDIR) or stat.S_ISLNK(mode):
                raise WorkspaceError("ZIP contains a link or special entry")
            if (name.endswith("/") and file_type == stat.S_IFREG) or (file_type == stat.S_IFDIR and not name.endswith("/")):
                raise WorkspaceError("ZIP member type disagrees with its name")
            target = destination.joinpath(*parts)
            if is_directory:
                target.mkdir(parents=True, exist_ok=True)
                directory_targets.add(target)
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(info) as source, target.open("xb") as output:
                shutil.copyfileobj(source, output)
            target.chmod(0o755 if mode & 0o111 else 0o644)
            current = target.parent
            root_target = destination / OFFICIAL_ROOT
            while current == root_target or root_target in current.parents:
                directory_targets.add(current)
                if current == root_target:
                    break
                current = current.parent
    for target in sorted(directory_targets, key=lambda path: len(path.parts), reverse=True):
        target.chmod(0o755)
    root = destination / OFFICIAL_ROOT
    if not root.is_dir() or root.is_symlink():
        raise WorkspaceError("missing expected official root")
    roots = [entry.name for entry in destination.iterdir()]
    if roots != [OFFICIAL_ROOT]:
        raise WorkspaceError("ZIP contains multiple roots")
    return root


def safe_extract_official_zip(archive_path: Path, destination: Path) -> Path:
    descriptor, before = _open_regular_descriptor(archive_path, "official ZIP")
    try:
        with os.fdopen(descriptor, "rb") as stream:
            root = _extract_zip_stream(stream, destination)
            after = os.fstat(stream.fileno())
            if _stable_identity(before) != _stable_identity(after):
                raise WorkspaceError("official ZIP changed during extraction")
            return root
    except BaseException:
        raise


def _extract_pinned_zip(path: Path, destination: Path) -> Path:
    descriptor, before = _open_regular_descriptor(path, "official ZIP")
    digest = hashlib.sha256()
    try:
        with os.fdopen(descriptor, "rb") as source, tempfile.TemporaryFile(mode="w+b") as snapshot:
            os.fchmod(snapshot.fileno(), 0o600)
            for block in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(block)
                snapshot.write(block)
            snapshot.flush()
            if digest.hexdigest() != OFFICIAL_ZIP_SHA256:
                raise WorkspaceError("official ZIP SHA-256 mismatch")
            after = os.fstat(source.fileno())
            if _stable_identity(before) != _stable_identity(after):
                raise WorkspaceError("official ZIP changed during snapshot")
            snapshot.seek(0)
            return _extract_zip_stream(snapshot, destination)
    except OSError as exc:
        raise WorkspaceError("official ZIP unavailable") from exc


def predecessor_patches(repository: Path, patch_end: int) -> tuple[Path, ...]:
    if patch_end < 0:
        raise WorkspaceError("patch-end must be nonnegative")
    directory = repository / "engine-patches"
    patches: list[Path] = []
    for number in range(1, patch_end + 1):
        prefix = f"{number:04d}-"
        matches = sorted((path for path in directory.glob(f"{prefix}*.patch") if path.is_file() and not path.is_symlink()), key=lambda path: os.fsencode(path.name))
        if len(matches) != 1:
            raise WorkspaceError("numeric predecessor range has a gap or duplicate")
        patches.append(_existing_file(matches[0], "predecessor patch"))
    return tuple(patches)


def validate_patch_diagnostics(name: str, output: str) -> None:
    diagnostics = "\n".join(line for line in output.splitlines() if re.search(r"Hunk .*(offset|fuzz)", line, re.IGNORECASE))
    if name == "0007-language-restart-and-system-default.patch":
        if diagnostics != KNOWN_0007_DIAGNOSTICS:
            raise WorkspaceError("0007 diagnostics differ from the pinned tuple")
    elif diagnostics:
        raise WorkspaceError("patch used offset or fuzz")


def _assert_regular_tree(root: Path) -> None:
    for directory, names, files in os.walk(root, followlinks=False):
        directory_path = Path(directory)
        for name in names + files:
            path = directory_path / name
            mode = path.lstat().st_mode
            if name.endswith((".rej", ".orig")) or stat.S_ISLNK(mode) or not (stat.S_ISDIR(mode) or stat.S_ISREG(mode)):
                raise WorkspaceError("tree contains rejected output, link, or special path")


def _replay(repository: Path, official_zip: Path, patch_end: int, destination: Path) -> Path:
    extracted = _extract_pinned_zip(official_zip, destination)
    environment = dict(os.environ, LC_ALL="C", LANG="C")
    for patch_path in predecessor_patches(repository, patch_end):
        command = ["/usr/bin/patch", "--verbose", "-p1", "--batch", "-V", "none", "-d", str(extracted), "-i", str(patch_path)]
        result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, env=environment, check=False)
        if result.returncode != 0:
            raise WorkspaceError("predecessor patch failed")
        validate_patch_diagnostics(patch_path.name, result.stdout)
        _assert_regular_tree(extracted)
    return extracted


def _read_regular_descriptor(descriptor: int, before: os.stat_result, maximum_bytes: int) -> bytes:
    if before.st_size > maximum_bytes:
        raise WorkspaceError("workspace file exceeds bounded read limit")
    remaining = before.st_size
    blocks = []
    while remaining:
        block = os.read(descriptor, min(1024 * 1024, remaining))
        if not block:
            raise WorkspaceError("workspace file changed during read")
        blocks.append(block)
        remaining -= len(block)
    if os.read(descriptor, 1):
        raise WorkspaceError("workspace file changed during read")
    after = os.fstat(descriptor)
    if _stable_identity(before) != _stable_identity(after):
        raise WorkspaceError("workspace file changed during read")
    return b"".join(blocks)


def _regular_bytes(path: Path, maximum_bytes: int | None = None) -> bytes:
    descriptor = None
    try:
        descriptor, before = _open_regular_descriptor(path, "workspace path")
        return _read_regular_descriptor(descriptor, before,
            OVERLAY_FILE_BYTES if maximum_bytes is None else maximum_bytes)
    except OSError as exc:
        raise WorkspaceError("workspace path unavailable") from exc
    finally:
        if descriptor is not None:
            os.close(descriptor)


def _regular_bytes_at(root_descriptor: int, relative: str, maximum_bytes: int | None = None) -> bytes:
    descriptor = None
    try:
        descriptor, before = _open_relative_descriptor(root_descriptor, relative)
        return _read_regular_descriptor(descriptor, before,
            OVERLAY_FILE_BYTES if maximum_bytes is None else maximum_bytes)
    except OSError as exc:
        raise WorkspaceError("workspace path unavailable") from exc
    finally:
        if descriptor is not None:
            os.close(descriptor)


def _optional_regular_bytes_at(root_descriptor: int, relative: str) -> bytes | None:
    try:
        return _regular_bytes_at(root_descriptor, relative)
    except WorkspaceError as exc:
        try:
            os.stat(relative, dir_fd=root_descriptor, follow_symlinks=False)
        except FileNotFoundError:
            return None
        raise exc


def _assert_no_symlink_components(root: Path, relative: str) -> Path:
    current = root
    if os.path.lexists(current) and stat.S_ISLNK(os.lstat(current).st_mode):
        raise WorkspaceError("workspace root cannot be a symlink")
    for component in relative.split("/"):
        current = current / component
        if os.path.lexists(current) and stat.S_ISLNK(os.lstat(current).st_mode):
            raise WorkspaceError("workspace path contains a symlink component")
    return current


def _tracked_engine_paths(repository: Path) -> tuple[str, ...]:
    result = subprocess.run(["git", "-C", str(repository), "ls-files", "-z", "--", "engine"], stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    if result.returncode != 0:
        raise WorkspaceError("repository is not a readable Git worktree")
    paths = []
    for raw in result.stdout.split(b"\0"):
        if not raw:
            continue
        try:
            value = raw.decode("utf-8")
        except UnicodeDecodeError as exc:
            raise WorkspaceError("tracked engine path is not UTF-8") from exc
        if not value.startswith("engine/"):
            raise WorkspaceError("unexpected tracked path")
        paths.append(_canonical_relative(value[len("engine/"):]))
    return tuple(sorted(set(paths), key=os.fsencode))


def _all_relative_files_at(root_descriptor: int) -> tuple[str, ...]:
    output = []
    for directory, names, files, directory_descriptor in os.fwalk(
            ".", follow_symlinks=False, dir_fd=root_descriptor):
        for name in names:
            mode = os.stat(name, dir_fd=directory_descriptor, follow_symlinks=False).st_mode
            if stat.S_ISLNK(mode) or not stat.S_ISDIR(mode):
                raise WorkspaceError("workspace contains a link or special directory")
        for name in files:
            mode = os.stat(name, dir_fd=directory_descriptor, follow_symlinks=False).st_mode
            if stat.S_ISLNK(mode) or not stat.S_ISREG(mode):
                raise WorkspaceError("workspace contains a link or special file")
            prefix = "" if directory == "." else directory.removeprefix("./") + "/"
            output.append(prefix + name)
    return tuple(sorted(output, key=os.fsencode))


def _all_relative_files(root: Path) -> tuple[str, ...]:
    descriptor = _open_directory_descriptor(root, "workspace")
    try:
        return _all_relative_files_at(descriptor)
    finally:
        os.close(descriptor)


def _copy_replay_tree(replay: Path, destination: Path) -> None:
    shutil.copytree(replay, destination, symlinks=False)
    _assert_regular_tree(destination)


def _remove_regular_path(path: Path) -> None:
    if not os.path.lexists(path):
        return
    mode = os.lstat(path).st_mode
    if stat.S_ISLNK(mode):
        raise WorkspaceError("refusing to remove a symlink")
    if stat.S_ISDIR(mode):
        shutil.rmtree(path)
    elif stat.S_ISREG(mode):
        path.unlink()
    else:
        raise WorkspaceError("refusing to remove a special path")


def _replace_workspace(staged: Path, workspace: Path) -> None:
    backup = workspace.parent / f".{workspace.name}.previous-{os.getpid()}"
    had_workspace = False
    moved_to_backup = False
    workspace.parent.mkdir(parents=True, exist_ok=True)
    try:
        if os.path.lexists(backup):
            raise WorkspaceError("workspace backup target already exists")
        if os.path.lexists(workspace):
            mode = os.lstat(workspace).st_mode
            if stat.S_ISLNK(mode) or not stat.S_ISDIR(mode):
                raise WorkspaceError("workspace must be a directory")
            had_workspace = True
            os.replace(workspace, backup)
            moved_to_backup = True
        os.replace(staged, workspace)
    except BaseException as original:
        rollback_error = None
        restored = not moved_to_backup
        if moved_to_backup:
            try:
                if not os.path.lexists(backup):
                    raise WorkspaceError("workspace backup became unavailable")
                if os.path.lexists(workspace):
                    if os.path.lexists(staged):
                        raise WorkspaceError("cannot preserve two unpublished workspace trees")
                    os.replace(workspace, staged)
                os.replace(backup, workspace)
                restored = True
            except BaseException as exc:
                rollback_error = exc

        cleanup_error = None
        if restored and os.path.lexists(staged):
            try:
                _remove_regular_path(staged)
            except BaseException as exc:
                cleanup_error = exc

        if rollback_error is not None:
            raise WorkspaceError("workspace rollback failed; recoverable trees were preserved") from original
        if cleanup_error is not None:
            raise WorkspaceError("workspace restored but unpublished cleanup failed") from original
        raise

    if had_workspace:
        try:
            _remove_regular_path(backup)
        except BaseException as exc:
            raise WorkspaceError("workspace published but backup cleanup failed; remnant preserved") from exc


def _assert_directory_binding(path: Path, pinned_descriptor: int) -> None:
    current = _open_directory_descriptor(path, "workspace")
    try:
        pinned = os.fstat(pinned_descriptor)
        observed = os.fstat(current)
        if (pinned.st_dev, pinned.st_ino) != (observed.st_dev, observed.st_ino):
            raise WorkspaceError("workspace root identity changed")
    finally:
        os.close(current)


def materialize(repository: Path, official_zip: Path, patch_end: int, workspace: Path, overlay_allowlist: Path | None) -> None:
    repository = _existing_directory(repository, "repository")
    official_zip = _existing_file(official_zip, "official ZIP")
    workspace = _existing_directory(workspace, "workspace") if overlay_allowlist else _output_directory(workspace)
    if overlay_allowlist is not None:
        overlay_allowlist = _existing_file(overlay_allowlist, "allowlist")
    allowlist = read_allowlist(overlay_allowlist) if overlay_allowlist else ()
    overlay: dict[str, bytes | None] = {}
    workspace_descriptor = None
    try:
        if overlay_allowlist:
            workspace_descriptor = _open_directory_descriptor(workspace, "workspace")
        with tempfile.TemporaryDirectory(prefix="engine-workspace-") as temporary_text:
            temporary = Path(temporary_text)
            replay = _replay(repository, official_zip, patch_end, temporary / "extract")
            replay_descriptor = _open_directory_descriptor(replay, "workspace")
            try:
                if overlay_allowlist:
                    replay_names_for_pollution = set(_all_relative_files_at(replay_descriptor))
                    allowed = set(allowlist)
                    for relative in _all_relative_files_at(workspace_descriptor):
                        if relative not in replay_names_for_pollution and relative not in allowed:
                            raise WorkspaceError("untracked or ignored workspace pollution")
                    for relative in allowlist:
                        overlay[relative] = _optional_regular_bytes_at(workspace_descriptor, relative)
                    overlay_bytes = sum(len(data) for data in overlay.values() if data is not None)
                    if overlay_bytes > OVERLAY_AGGREGATE_BYTES:
                        raise WorkspaceError("workspace overlay exceeds aggregate limit")
                    for relative in _tracked_engine_paths(repository):
                        if relative in allowed:
                            continue
                        workspace_data = _optional_regular_bytes_at(workspace_descriptor, relative)
                        replay_data = _optional_regular_bytes_at(replay_descriptor, relative)
                        if workspace_data is None or replay_data is None or workspace_data != replay_data:
                            raise WorkspaceError("tracked engine drift outside overlay allowlist")
            finally:
                os.close(replay_descriptor)

            staged = temporary / "staged"
            _copy_replay_tree(replay, staged)
            for relative, data in overlay.items():
                target = staged / relative
                if data is None:
                    if target.exists():
                        target.unlink()
                else:
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(data)
                    if target.read_bytes() != data:
                        raise WorkspaceError("overlay byte identity failure")
            if workspace_descriptor is not None:
                _assert_directory_binding(workspace, workspace_descriptor)
            published = workspace.parent / f".{workspace.name}.materialized-{os.getpid()}"
            backup = workspace.parent / f".{workspace.name}.previous-{os.getpid()}"
            if os.path.lexists(published) or os.path.lexists(backup):
                raise WorkspaceError("unpublished materialization remnant already exists")
            os.replace(staged, published)
            _replace_workspace(published, workspace)
    finally:
        if workspace_descriptor is not None:
            os.close(workspace_descriptor)


def _validate_candidate_patch(data: bytes, allowlist: tuple[str, ...]) -> None:
    try:
        text = data.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise WorkspaceError("candidate patch is not UTF-8") from exc
    if "\r" in text or not text.endswith("\n"):
        raise WorkspaceError("candidate patch must use canonical LF records")
    diff_paths: list[str] = []
    lines = text.splitlines()
    consumed_headers: set[int] = set()
    for index, line in enumerate(lines):
        if not line.startswith("diff -ruN "):
            continue
        match = re.fullmatch(r"diff -ruN a/([^\t ]+) b/([^\t ]+)", line)
        if match is None:
            raise WorkspaceError("candidate has noncanonical diff header")
        left, right = match.groups()
        if left != right or left not in allowlist:
            raise WorkspaceError("candidate changed a path outside the allowlist")
        _canonical_relative(left)
        if index + 2 >= len(lines):
            raise WorkspaceError("candidate has mismatched paired headers")
        old_match = re.fullmatch(r"--- a/([^\t]+)\t.+", lines[index + 1])
        new_match = re.fullmatch(r"\+\+\+ b/([^\t]+)\t.+", lines[index + 2])
        if old_match is None or new_match is None or old_match.group(1) != left or new_match.group(1) != right:
            raise WorkspaceError("candidate has mismatched paired headers")
        consumed_headers.update((index + 1, index + 2))
        diff_paths.append(left)
    for index, line in enumerate(lines):
        if (line.startswith("diff ") and not line.startswith("diff -ruN ")) or (
            (line.startswith("--- ") or line.startswith("+++ ")) and index not in consumed_headers
        ):
            raise WorkspaceError("candidate has stray or ambiguous header records")
    if not diff_paths or len(diff_paths) != len(set(diff_paths)) or diff_paths != sorted(diff_paths, key=os.fsencode):
        raise WorkspaceError("candidate must have one byte-sorted pair per changed path")


def candidate(repository: Path, official_zip: Path, patch_end: int, workspace: Path, allowlist_path: Path, output: Path) -> None:
    repository = _existing_directory(repository, "repository")
    official_zip = _existing_file(official_zip, "official ZIP")
    workspace = _existing_directory(workspace, "workspace")
    allowlist_path = _existing_file(allowlist_path, "allowlist")
    output = _output_file(output)
    allowlist = read_allowlist(allowlist_path)
    workspace_descriptor = _open_directory_descriptor(workspace, "workspace")
    try:
        with tempfile.TemporaryDirectory(prefix="engine-candidate-") as temporary_text:
            temporary = Path(temporary_text)
            replay = _replay(repository, official_zip, patch_end, temporary / "extract")
            a = temporary / "a"
            b = temporary / "b"
            _copy_replay_tree(replay, a)
            _copy_replay_tree(replay, b)
            overlay_bytes = 0
            for relative in allowlist:
                data = _optional_regular_bytes_at(workspace_descriptor, relative)
                target = b / relative
                if data is not None:
                    overlay_bytes += len(data)
                    if overlay_bytes > OVERLAY_AGGREGATE_BYTES:
                        raise WorkspaceError("candidate overlay exceeds aggregate limit")
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(data)
                    if target.read_bytes() != data:
                        raise WorkspaceError("candidate overlay byte identity failure")
                elif target.exists():
                    target.unlink()
            _assert_directory_binding(workspace, workspace_descriptor)
            environment = dict(os.environ, LC_ALL="C", LANG="C")
            result = subprocess.run(["diff", "-ruN", "a", "b"], cwd=temporary, stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=environment, check=False)
            if result.returncode != 1 or result.stderr:
                raise WorkspaceError("candidate diff must report exactly one changed result")
            _validate_candidate_patch(result.stdout, allowlist)
            _atomic_write(output, result.stdout)
    finally:
        os.close(workspace_descriptor)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="engine_workspace.py")
    commands = parser.add_subparsers(dest="command", required=True)
    materialize_parser = commands.add_parser("materialize")
    candidate_parser = commands.add_parser("candidate")
    allowlist_parser = commands.add_parser("allowlist")
    for subparser in (materialize_parser, candidate_parser):
        subparser.add_argument("--repository", type=Path, required=True)
        subparser.add_argument("--official-zip", type=Path, required=True)
        subparser.add_argument("--patch-end", type=int, required=True)
        subparser.add_argument("--workspace", type=Path, required=True)
    materialize_parser.add_argument("--overlay-allowlist", type=Path)
    candidate_parser.add_argument("--allowlist", type=Path, required=True)
    candidate_parser.add_argument("--output", type=Path, required=True)
    allowlist_parser.add_argument("--patch-id", choices=tuple(ALLOWLISTS), required=True)
    allowlist_parser.add_argument("--output", type=Path, required=True)
    return parser


def main(argv: list[str] | None = None) -> int:
    try:
        arguments = _parser().parse_args(argv)
        if arguments.command == "allowlist":
            data = ("\n".join(ALLOWLISTS[arguments.patch_id]) + "\n").encode("utf-8")
            _atomic_write(arguments.output, data)
        elif arguments.command == "materialize":
            materialize(arguments.repository, arguments.official_zip, arguments.patch_end, arguments.workspace, arguments.overlay_allowlist)
        elif arguments.command == "candidate":
            candidate(arguments.repository, arguments.official_zip, arguments.patch_end, arguments.workspace, arguments.allowlist, arguments.output)
        return 0
    except SystemExit as exc:
        return int(exc.code)
    except (WorkspaceError, OSError, subprocess.SubprocessError, zipfile.BadZipFile) as exc:
        print(f"engine_workspace: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
