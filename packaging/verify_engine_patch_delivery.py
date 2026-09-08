#!/usr/bin/env python3

import argparse
import hashlib
import json
import os
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import zipfile
from dataclasses import asdict, dataclass, replace
from pathlib import Path, PurePosixPath


KNOWN_0007_DIAGNOSTICS = (
    "Hunk #1 succeeded at 254 with fuzz 2 (offset -1 lines).",
    "Hunk #5 succeeded at 352 (offset -1 lines).",
)
KNOWN_0007_PATCH = "0007-language-restart-and-system-default.patch"
DIAGNOSTIC_PATTERN = re.compile(rb"Hunk .*(?:offset|fuzz)", re.IGNORECASE)
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
HUNK_HEADER_PATTERN = re.compile(
    rb"^@@ -([0-9]+)(?:,([0-9]+))? \+([0-9]+)(?:,([0-9]+))? @@(?: .*)?$"
)
NO_NEWLINE_MARKER = b"\\ No newline at end of file"


class VerificationError(RuntimeError):
    pass


@dataclass(frozen=True)
class CommandResult:
    command: tuple[str, ...]
    returncode: int
    output: bytes


@dataclass(frozen=True)
class ManifestEntry:
    path: str
    size: int
    sha256: str


@dataclass(frozen=True)
class VerificationConfig:
    repository: Path
    official_zip: Path
    expected_zip_sha256: str
    predecessor_start: int
    predecessor_end: int
    allowlist_file: Path
    candidate: Path
    workspace: Path
    output_dir: Path
    patch_tool: str
    git_tool: str


class SubprocessRunner:
    def run(self, command, *, cwd, stdin_path, phase):
        del phase
        environment = os.environ.copy()
        environment["LC_ALL"] = "C"
        environment["LANG"] = "C"
        stdin = None
        try:
            if stdin_path is not None:
                stdin = Path(stdin_path).open("rb")
            result = subprocess.run(
                list(command),
                cwd=cwd,
                stdin=stdin,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                check=False,
                env=environment,
            )
        finally:
            if stdin is not None:
                stdin.close()

        return CommandResult(tuple(map(str, command)), result.returncode, result.stdout)


def sha256_file(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def byte_sort_key(path):
    return os.fsencode(path)


def validate_relative_path(raw_path):
    if not isinstance(raw_path, str) or not raw_path:
        raise VerificationError("unsafe empty engine-relative path")
    if "\0" in raw_path or "\\" in raw_path or "\n" in raw_path or "\r" in raw_path:
        raise VerificationError(f"unsafe engine-relative path: {raw_path!r}")

    path = PurePosixPath(raw_path)
    if (
        path.is_absolute()
        or path.as_posix() != raw_path
        or raw_path == "."
        or any(part in ("", ".", "..") for part in path.parts)
    ):
        raise VerificationError(f"unsafe engine-relative path: {raw_path!r}")

    return raw_path


def normalize_allowlist(paths):
    normalized = tuple(validate_relative_path(path) for path in paths)
    if not normalized:
        raise VerificationError("engine allowlist is empty")
    if len(set(normalized)) != len(normalized):
        raise VerificationError("engine allowlist contains duplicate paths")
    return tuple(sorted(normalized, key=byte_sort_key))


def load_allowlist(path):
    raw = Path(path).read_bytes()
    if b"\0" in raw:
        raise VerificationError("allowlist contains a NUL byte")
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError as error:
        raise VerificationError("allowlist is not valid UTF-8") from error

    lines = text.splitlines()
    if any(not line for line in lines):
        raise VerificationError("allowlist contains a blank path")
    return normalize_allowlist(lines)


def parse_diff_marker(line):
    prefix = b"diff -ruN "
    if not line.startswith(prefix):
        raise VerificationError("candidate section is not marked by exact 'diff -ruN'")
    payload = line[len(prefix):]
    separator = payload.find(b" b/")
    if separator <= 1:
        raise VerificationError("candidate diff -ruN marker has malformed paths")

    old_token = payload[:separator]
    new_token = payload[separator + 1:]
    if not old_token.startswith(b"a/") or not new_token.startswith(b"b/"):
        raise VerificationError("candidate diff -ruN marker must use a/ and b/ paths")

    try:
        old_path = os.fsdecode(old_token[2:])
        new_path = os.fsdecode(new_token[2:])
    except UnicodeError as error:
        raise VerificationError("candidate diff marker path cannot be decoded") from error

    validate_relative_path(old_path)
    validate_relative_path(new_path)
    if old_path != new_path:
        raise VerificationError(
            f"candidate diff marker paths are not paired: {old_path!r} != {new_path!r}"
        )
    return old_path


def parse_file_header(line, marker, prefix):
    if not line.startswith(marker):
        raise VerificationError("candidate file headers are not paired after diff -ruN")
    token, separator, timestamp = line[len(marker):].partition(b"\t")
    if not separator or not timestamp:
        raise VerificationError("candidate file header is missing its tab timestamp")
    if not token.startswith(prefix):
        raise VerificationError("candidate file header must use matching a/ and b/ paths")

    path = os.fsdecode(token[len(prefix):])
    validate_relative_path(path)
    return path


def parse_hunk_header(line):
    match = HUNK_HEADER_PATTERN.fullmatch(line)
    if match is None:
        raise VerificationError(f"malformed unified-diff hunk header: {line!r}")
    old_count = int(match.group(2)) if match.group(2) is not None else 1
    new_count = int(match.group(4)) if match.group(4) is not None else 1
    if old_count == 0 and new_count == 0:
        raise VerificationError("unified-diff hunk cannot have two zero counts")
    return old_count, new_count


def consume_hunk(lines, index):
    old_count, new_count = parse_hunk_header(lines[index])
    index += 1
    old_seen = 0
    new_seen = 0
    while old_seen < old_count or new_seen < new_count:
        if index >= len(lines):
            raise VerificationError("unified-diff hunk ended before its declared counts")
        line = lines[index]
        if not line:
            raise VerificationError("unified-diff hunk body line has no prefix")
        prefix = line[:1]
        if prefix == b" ":
            old_seen += 1
            new_seen += 1
        elif prefix == b"-":
            old_seen += 1
        elif prefix == b"+":
            new_seen += 1
        else:
            raise VerificationError(
                f"unified-diff hunk body has an invalid prefix: {line!r}"
            )
        if old_seen > old_count or new_seen > new_count:
            raise VerificationError("unified-diff hunk body exceeds its declared counts")
        index += 1
        if index < len(lines) and lines[index] == NO_NEWLINE_MARKER:
            index += 1
    return index


def validate_candidate_headers(candidate, allowlist):
    expected = normalize_allowlist(allowlist)
    data = Path(candidate).read_bytes()
    if not data:
        raise VerificationError("candidate patch is empty")
    if b"\0" in data:
        raise VerificationError("candidate patch contains a NUL byte")
    if b"\r" in data:
        raise VerificationError("candidate patch contains a carriage return")
    if not data.endswith(b"\n"):
        raise VerificationError("candidate patch must end with a newline")

    lines = data[:-1].split(b"\n")
    sections = []
    index = 0
    while index < len(lines):
        if not lines[index].startswith(b"diff -ruN "):
            raise VerificationError(
                "candidate has unsupported or trailing content/file headers; "
                "expected exact diff -ruN marker "
                f"at line {index + 1}: {lines[index]!r}"
            )
        if index + 2 >= len(lines):
            raise VerificationError("candidate diff -ruN section has no paired headers")

        marker_path = parse_diff_marker(lines[index])
        old_path = parse_file_header(lines[index + 1], b"--- ", b"a/")
        new_path = parse_file_header(lines[index + 2], b"+++ ", b"b/")
        if old_path != new_path or old_path != marker_path:
            raise VerificationError(
                "candidate old/new/diff marker paths are not paired: "
                f"{marker_path!r}, {old_path!r}, {new_path!r}"
            )

        sections.append(marker_path)
        index += 3
        hunk_count = 0
        while index < len(lines) and lines[index].startswith(b"@@ "):
            index = consume_hunk(lines, index)
            hunk_count += 1
        if hunk_count == 0:
            raise VerificationError(
                f"candidate diff -ruN section has no unified hunks: {marker_path}"
            )

    if not sections:
        raise VerificationError("candidate has no exact diff -ruN sections")

    duplicates = sorted(
        {path for path in sections if sections.count(path) > 1}, key=byte_sort_key
    )
    if duplicates:
        raise VerificationError(
            "candidate contains duplicate diff sections: " + ", ".join(duplicates)
        )

    actual = tuple(sorted(sections, key=byte_sort_key))
    if actual != expected:
        raise VerificationError(
            "candidate headers do not equal the exact allowlist: "
            f"expected={list(expected)!r} actual={list(actual)!r}"
        )
    return actual


def ensure_root_directory(root, label):
    path = Path(root)
    try:
        mode = path.lstat().st_mode
    except FileNotFoundError as error:
        raise VerificationError(f"{label} does not exist: {path}") from error
    if stat.S_ISLNK(mode):
        raise VerificationError(f"{label} is a symlink: {path}")
    if not stat.S_ISDIR(mode):
        raise VerificationError(f"{label} is not a directory: {path}")
    return path


def ensure_regular_path_under(root, relative, label):
    root = ensure_root_directory(root, f"{label} root")
    relative = validate_relative_path(relative)
    current = root
    parts = PurePosixPath(relative).parts
    for index, part in enumerate(parts):
        current /= part
        try:
            mode = current.lstat().st_mode
        except FileNotFoundError as error:
            raise VerificationError(f"missing {label}: {relative}") from error
        if stat.S_ISLNK(mode):
            raise VerificationError(f"{label} path contains a symlink: {relative}")
        if index < len(parts) - 1 and not stat.S_ISDIR(mode):
            raise VerificationError(f"{label} parent is not a directory: {relative}")
        if index == len(parts) - 1 and not stat.S_ISREG(mode):
            raise VerificationError(f"{label} is not a regular file: {relative}")
    return current


def validate_workspace_paths(workspace, allowlist):
    normalized = normalize_allowlist(allowlist)
    for relative in normalized:
        ensure_regular_path_under(workspace, relative, "workspace allowlist file")
    return normalized


def ensure_regular_file(path, label):
    path = Path(path)
    try:
        mode = path.lstat().st_mode
    except FileNotFoundError as error:
        raise VerificationError(f"missing {label}: {path}") from error
    if stat.S_ISLNK(mode):
        raise VerificationError(f"{label} is a symlink: {path}")
    if not stat.S_ISREG(mode):
        raise VerificationError(f"{label} is not a regular file: {path}")
    return path


def select_predecessors(repository, start, end):
    if not isinstance(start, int) or not isinstance(end, int) or start < 1 or end < start:
        raise VerificationError(f"invalid predecessor range: {start!r}..{end!r}")

    patch_directory = ensure_root_directory(
        Path(repository) / "engine-patches", "engine patch directory"
    )
    selected = []
    for number in range(start, end + 1):
        prefix = f"{number:04d}"
        matches = sorted(patch_directory.glob(f"{prefix}-*.patch"), key=lambda p: os.fsencode(p.name))
        if len(matches) != 1:
            raise VerificationError(
                f"predecessor {prefix} must have exactly one patch, found {len(matches)}"
            )
        selected.append(ensure_regular_file(matches[0], f"predecessor {prefix}"))
    return tuple(selected)


def predecessor_header_token(line, marker, marker_index):
    remainder = line[marker_index + len(marker):]
    if marker.startswith(b"Index:"):
        return remainder.split(None, 1)[0] if remainder else b""
    return remainder.split(b"\t", 1)[0]


def is_context_hunk_range(marker, token):
    if marker == b"*** ":
        return re.fullmatch(rb"[0-9]+(?:,[0-9]+)? \*{4}", token) is not None
    if marker == b"--- ":
        return re.fullmatch(rb"[0-9]+(?:,[0-9]+)? -{4}", token) is not None
    return False


def validate_predecessor_header_path(path, patch, line_number):
    components = path.split("/")
    if (
        not path
        or path.startswith("/")
        or "\\" in path
        or any(component in ("", ".", "..") for component in components)
    ):
        raise VerificationError(
            "unsafe predecessor file header "
            f"in {patch.name}:{line_number}: {path!r}"
        )


def validate_predecessor_header_paths(patch):
    patch = ensure_regular_file(patch, "predecessor patch")
    headers = []
    for line_number, line in enumerate(patch.read_bytes().splitlines(), 1):
        for marker in (b"--- ", b"+++ ", b"*** ", b"Index: ", b"Index:\t"):
            offset = 0
            while True:
                marker_index = line.find(marker, offset)
                if marker_index < 0:
                    break
                token = predecessor_header_token(line, marker, marker_index)
                if is_context_hunk_range(marker, token):
                    offset = marker_index + len(marker)
                    continue

                path = os.fsdecode(token)
                # Index-like text can legitimately occur in patched source.  It is
                # only a patch target when it has path syntax; file headers are
                # always validated, even when Apple patch accepts an indentation
                # or arbitrary prefix before them.
                looks_like_path = "/" in path or path.startswith((".", "/"))
                if not marker.startswith(b"Index:") or looks_like_path:
                    validate_predecessor_header_path(path, patch, line_number)
                if marker in (b"--- ", b"+++ "):
                    headers.append(path)
                offset = marker_index + len(marker)

    if not headers:
        raise VerificationError(
            f"predecessor has no recognizable file headers: {patch.name}"
        )
    return tuple(headers)


def extract_offset_fuzz_diagnostics(output):
    return tuple(
        os.fsdecode(line)
        for line in bytes(output).splitlines()
        if DIAGNOSTIC_PATTERN.search(line)
    )


def validate_predecessor_diagnostics(patch_name, output):
    diagnostics = extract_offset_fuzz_diagnostics(output)
    expected = KNOWN_0007_DIAGNOSTICS if patch_name == KNOWN_0007_PATCH else ()
    if diagnostics != expected:
        raise VerificationError(
            f"unexpected predecessor offset/fuzz diagnostics for {patch_name}: "
            f"expected={expected!r} actual={diagnostics!r}"
        )
    return diagnostics


def ensure_no_patch_residue(root):
    root = ensure_root_directory(root, "replay root")
    residue = []
    for current, directories, files in os.walk(root, followlinks=False):
        current_path = Path(current)
        for name in directories:
            path = current_path / name
            if stat.S_ISLNK(path.lstat().st_mode):
                raise VerificationError(f"replay tree contains a symlink: {path.relative_to(root)}")
        for name in files:
            if name.endswith((".rej", ".orig")):
                residue.append((current_path / name).relative_to(root).as_posix())
    if residue:
        residue.sort(key=byte_sort_key)
        raise VerificationError("patch residue detected: " + ", ".join(residue))


def build_tree_manifest(root):
    root = ensure_root_directory(root, "manifest root")
    files = []
    for current, directories, names in os.walk(root, topdown=True, followlinks=False):
        current_path = Path(current)
        for name in directories:
            path = current_path / name
            mode = path.lstat().st_mode
            if stat.S_ISLNK(mode):
                raise VerificationError(
                    f"manifest tree contains a symlink: {path.relative_to(root)}"
                )
            if not stat.S_ISDIR(mode):
                raise VerificationError(
                    f"manifest tree contains a non-directory: {path.relative_to(root)}"
                )
        for name in names:
            path = current_path / name
            mode = path.lstat().st_mode
            relative = path.relative_to(root).as_posix()
            if stat.S_ISLNK(mode):
                raise VerificationError(f"manifest tree contains a symlink: {relative}")
            if not stat.S_ISREG(mode):
                raise VerificationError(
                    f"manifest tree contains a non-regular file: {relative}"
                )
            files.append((relative, path))

    entries = []
    for relative, path in sorted(files, key=lambda item: byte_sort_key(item[0])):
        entries.append(
            ManifestEntry(relative, path.stat().st_size, sha256_file(path))
        )
    return tuple(entries)


def manifest_sha256(manifest):
    digest = hashlib.sha256()
    for entry in manifest:
        digest.update(os.fsencode(entry.path))
        digest.update(b"\0")
        digest.update(str(entry.size).encode("ascii"))
        digest.update(b"\0")
        digest.update(entry.sha256.encode("ascii"))
        digest.update(b"\n")
    return digest.hexdigest()


def patch_command(patch_tool, direction):
    command = [
        str(patch_tool),
        direction,
        "--dry-run",
        "-p1",
        "--batch",
        "-F",
        "0",
        "-V",
        "none",
    ]
    return tuple(command)


def verify_duplicate_refusal(replay, candidate, patch_tool, runner):
    before = build_tree_manifest(replay)
    result = runner.run(
        patch_command(patch_tool, "--forward"),
        cwd=replay,
        stdin_path=candidate,
        phase="duplicate-forward-dry-run",
    )
    after = build_tree_manifest(replay)
    if before != after:
        raise VerificationError("duplicate forward dry-run mutated replay bytes")
    ensure_no_patch_residue(replay)
    if result.returncode == 0:
        raise VerificationError("duplicate forward dry-run unexpectedly accepted the patch")
    return result, before


def verify_reverse_dry_run(replay, candidate, patch_tool, runner):
    before = build_tree_manifest(replay)
    result = runner.run(
        patch_command(patch_tool, "--reverse"),
        cwd=replay,
        stdin_path=candidate,
        phase="reverse-dry-run",
    )
    after = build_tree_manifest(replay)
    if before != after:
        raise VerificationError("reverse dry-run mutated replay bytes")
    ensure_no_patch_residue(replay)
    if result.returncode != 0:
        raise VerificationError(
            f"reverse dry-run failed with exit code {result.returncode}"
        )
    return result, before


def atomic_write_bytes(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(
        prefix=f".{path.name}.", dir=path.parent
    )
    try:
        with os.fdopen(descriptor, "wb") as stream:
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


def atomic_write_json(path, payload):
    data = (
        json.dumps(payload, indent=2, sort_keys=True, ensure_ascii=True) + "\n"
    ).encode("utf-8")
    atomic_write_bytes(path, data)


def safe_extract_zip(archive_path, destination):
    archive_path = ensure_regular_file(archive_path, "official ZIP")
    destination = Path(destination)
    destination.mkdir(parents=True, exist_ok=False)
    seen = set()
    roots = set()
    try:
        archive = zipfile.ZipFile(archive_path)
    except zipfile.BadZipFile as error:
        raise VerificationError(f"official ZIP is invalid: {archive_path}") from error

    with archive:
        for info in archive.infolist():
            raw_name = info.filename
            if not raw_name or "\\" in raw_name or "\0" in raw_name:
                raise VerificationError(f"unsafe ZIP entry: {raw_name!r}")
            trimmed = raw_name[:-1] if raw_name.endswith("/") else raw_name
            path = PurePosixPath(trimmed)
            if (
                not trimmed
                or path.is_absolute()
                or path.as_posix() != trimmed
                or any(part in ("", ".", "..") for part in path.parts)
            ):
                raise VerificationError(f"unsafe ZIP entry: {raw_name!r}")
            if trimmed in seen:
                raise VerificationError(f"duplicate ZIP entry: {trimmed}")
            seen.add(trimmed)
            roots.add(path.parts[0])

            mode = (info.external_attr >> 16) & 0o170000
            if mode == stat.S_IFLNK:
                raise VerificationError(f"ZIP entry is a symlink: {trimmed}")

            target = destination.joinpath(*path.parts)
            if info.is_dir():
                target.mkdir(parents=True, exist_ok=True)
                continue
            if mode not in (0, stat.S_IFREG):
                raise VerificationError(f"ZIP entry is not a regular file: {trimmed}")
            target.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(info, "r") as source, target.open("xb") as output:
                shutil.copyfileobj(source, output)

    if len(roots) != 1:
        raise VerificationError(
            f"official ZIP must contain exactly one top-level root, found {sorted(roots)!r}"
        )
    root = destination / next(iter(roots))
    return ensure_root_directory(root, "extracted official source root")


def command_evidence(result, log_path, output_dir):
    return {
        "command": list(result.command),
        "returncode": result.returncode,
        "log": Path(log_path).relative_to(output_dir).as_posix(),
    }


def replay_predecessors(replay, predecessors, output_dir, patch_tool, runner):
    validated_headers = {
        patch: validate_predecessor_header_paths(patch)
        for patch in predecessors
    }
    logs = output_dir / "predecessor-logs"
    logs.mkdir()
    combined_path = output_dir / "predecessors-combined.log"
    combined = bytearray()
    evidence = []
    for patch in predecessors:
        header_paths = validated_headers[patch]
        command = (
            str(patch_tool),
            "--verbose",
            "-p1",
            "--batch",
            "-V",
            "none",
        )
        result = runner.run(
            command,
            cwd=replay,
            stdin_path=patch,
            phase=f"predecessor:{patch.name}",
        )
        log_path = logs / f"{patch.name}.log"
        atomic_write_bytes(log_path, result.output)
        combined.extend(f"===== {patch.name} =====\n".encode("utf-8"))
        combined.extend(result.output)
        if not result.output.endswith(b"\n"):
            combined.extend(b"\n")
        atomic_write_bytes(combined_path, bytes(combined))

        if result.returncode != 0:
            raise VerificationError(
                f"predecessor {patch.name} failed with exit code {result.returncode}"
            )
        diagnostics = validate_predecessor_diagnostics(patch.name, result.output)
        ensure_no_patch_residue(replay)
        item = command_evidence(result, log_path, output_dir)
        item.update(
            {
                "name": patch.name,
                "sha256": sha256_file(patch),
                "header_path_count": len(header_paths),
                "diagnostics": list(diagnostics),
            }
        )
        evidence.append(item)
    return evidence, combined_path


def tracked_engine_paths(repository, git_tool, runner):
    command = (
        str(git_tool),
        "-C",
        str(repository),
        "ls-files",
        "-z",
        "--",
        "engine",
    )
    result = runner.run(
        command,
        cwd=repository,
        stdin_path=None,
        phase="git-ls-files-engine",
    )
    if result.returncode != 0:
        raise VerificationError(
            f"git ls-files engine failed with exit code {result.returncode}"
        )
    if result.output and not result.output.endswith(b"\0"):
        raise VerificationError("git ls-files -z returned a non-NUL-terminated result")

    paths = []
    for raw in result.output.split(b"\0"):
        if not raw:
            continue
        path = os.fsdecode(raw)
        if not path.startswith("engine/"):
            raise VerificationError(f"git returned a non-engine path: {path!r}")
        paths.append(validate_relative_path(path[len("engine/"):]))
    if len(paths) != len(set(paths)):
        raise VerificationError("git ls-files returned duplicate engine paths")
    return tuple(sorted(paths, key=byte_sort_key)), result


def verify_byte_identity(replay, workspace, tracked, allowlist):
    allowed = set(allowlist)
    union = tuple(sorted(set(tracked) | allowed, key=byte_sort_key))
    evidence = []
    for relative in union:
        replay_path = ensure_regular_path_under(replay, relative, "replay engine file")
        workspace_path = ensure_regular_path_under(
            workspace, relative, "workspace engine file"
        )
        replay_sha = sha256_file(replay_path)
        workspace_sha = sha256_file(workspace_path)
        if replay_sha != workspace_sha or replay_path.stat().st_size != workspace_path.stat().st_size:
            scope = "allowlisted" if relative in allowed else "outside allowlist"
            raise VerificationError(f"{scope} byte mismatch: {relative}")
        evidence.append(
            {
                "path": relative,
                "size": replay_path.stat().st_size,
                "sha256": replay_sha,
                "allowlisted": relative in allowed,
            }
        )
    return evidence


def write_tree_manifest(path, manifest):
    payload = {
        "entry_count": len(manifest),
        "sha256": manifest_sha256(manifest),
        "entries": [asdict(entry) for entry in manifest],
    }
    atomic_write_json(path, payload)
    return payload


def validate_config(config):
    repository = ensure_root_directory(config.repository, "repository").resolve(
        strict=True
    )
    workspace = ensure_root_directory(config.workspace, "workspace").resolve(
        strict=True
    )
    expected_workspace = repository / "engine"
    if workspace.resolve(strict=True) != expected_workspace.resolve(strict=True):
        raise VerificationError(
            f"workspace must be the repository engine directory: {expected_workspace}"
        )

    official_zip = ensure_regular_file(config.official_zip, "official ZIP").resolve(
        strict=True
    )
    allowlist_file = ensure_regular_file(config.allowlist_file, "allowlist").resolve(
        strict=True
    )
    candidate = ensure_regular_file(config.candidate, "candidate patch").resolve(
        strict=True
    )
    for label, path in (
        ("official ZIP", official_zip),
        ("allowlist", allowlist_file),
        ("candidate patch", candidate),
    ):
        if any(character in str(path) for character in ("\0", "\n", "\r")):
            raise VerificationError(f"{label} path contains a control character")

    expected_sha = config.expected_zip_sha256.lower()
    if not SHA256_PATTERN.fullmatch(expected_sha):
        raise VerificationError("expected ZIP SHA-256 must be 64 hexadecimal characters")
    output_input = Path(config.output_dir)
    try:
        output_input.lstat()
    except FileNotFoundError:
        pass
    else:
        raise VerificationError(f"output directory already exists: {output_input}")
    output_dir = output_input.resolve(strict=False)
    output_dir.mkdir(parents=True)

    canonical = replace(
        config,
        repository=repository,
        official_zip=official_zip,
        allowlist_file=allowlist_file,
        candidate=candidate,
        workspace=workspace,
        output_dir=output_dir,
    )
    return canonical, expected_sha


def verify_delivery(config, runner=None):
    runner = runner or SubprocessRunner()
    config, expected_zip_sha256 = validate_config(config)
    repository = config.repository
    workspace = config.workspace

    actual_zip_sha256 = sha256_file(config.official_zip)
    if actual_zip_sha256 != expected_zip_sha256:
        raise VerificationError(
            "official ZIP SHA-256 mismatch: "
            f"expected={expected_zip_sha256} actual={actual_zip_sha256}"
        )

    allowlist = load_allowlist(config.allowlist_file)
    validate_workspace_paths(workspace, allowlist)
    headers = validate_candidate_headers(config.candidate, allowlist)
    predecessors = select_predecessors(
        repository, config.predecessor_start, config.predecessor_end
    )

    extraction = config.output_dir / "fresh-official-extraction"
    replay = safe_extract_zip(config.official_zip, extraction)
    predecessor_evidence, combined_log = replay_predecessors(
        replay, predecessors, config.output_dir, config.patch_tool, runner
    )

    git_check_before = build_tree_manifest(replay)
    git_check = runner.run(
        (
            str(config.git_tool),
            "apply",
            "--check",
            "--verbose",
            "-p1",
            "--",
            str(config.candidate),
        ),
        cwd=replay,
        stdin_path=None,
        phase="git-apply-check",
    )
    git_check_log = config.output_dir / "git-apply-check.log"
    atomic_write_bytes(git_check_log, git_check.output)
    if build_tree_manifest(replay) != git_check_before:
        raise VerificationError("git apply --check mutated replay bytes")
    if git_check.returncode != 0:
        raise VerificationError(
            f"git apply --check failed with exit code {git_check.returncode}"
        )

    strict_apply = runner.run(
        (
            str(config.patch_tool),
            "--forward",
            "--verbose",
            "-p1",
            "--batch",
            "-F",
            "0",
            "-V",
            "none",
        ),
        cwd=replay,
        stdin_path=config.candidate,
        phase="strict-apply",
    )
    strict_log = config.output_dir / "strict-apply.log"
    atomic_write_bytes(strict_log, strict_apply.output)
    if strict_apply.returncode != 0:
        raise VerificationError(
            f"strict candidate apply failed with exit code {strict_apply.returncode}"
        )
    strict_diagnostics = extract_offset_fuzz_diagnostics(strict_apply.output)
    if strict_diagnostics:
        raise VerificationError(
            f"strict candidate apply used offset/fuzz: {strict_diagnostics!r}"
        )
    ensure_no_patch_residue(replay)

    tracked, git_ls_files = tracked_engine_paths(
        repository, config.git_tool, runner
    )
    identity = verify_byte_identity(replay, workspace, tracked, allowlist)

    manifest = build_tree_manifest(replay)
    manifest_path = config.output_dir / "tree-manifest.json"
    manifest_payload = write_tree_manifest(manifest_path, manifest)

    duplicate, duplicate_manifest = verify_duplicate_refusal(
        replay, config.candidate, config.patch_tool, runner
    )
    duplicate_log = config.output_dir / "duplicate-forward-dry-run.log"
    atomic_write_bytes(duplicate_log, duplicate.output)

    reverse, reverse_manifest = verify_reverse_dry_run(
        replay, config.candidate, config.patch_tool, runner
    )
    reverse_log = config.output_dir / "reverse-dry-run.log"
    atomic_write_bytes(reverse_log, reverse.output)
    if duplicate_manifest != manifest or reverse_manifest != manifest:
        raise VerificationError("dry-run manifest does not equal strict replay manifest")
    ensure_no_patch_residue(replay)

    evidence = {
        "schema_version": 1,
        "passed": True,
        "archive": {
            "path": str(config.official_zip),
            "sha256": actual_zip_sha256,
            "size": config.official_zip.stat().st_size,
            "extracted_root": replay.name,
        },
        "candidate": {
            "path": str(config.candidate),
            "sha256": sha256_file(config.candidate),
            "size": config.candidate.stat().st_size,
            "headers": list(headers),
        },
        "predecessor_range": {
            "start": config.predecessor_start,
            "end": config.predecessor_end,
        },
        "predecessors": predecessor_evidence,
        "predecessor_combined_log": combined_log.relative_to(
            config.output_dir
        ).as_posix(),
        "git_apply_check": command_evidence(
            git_check, git_check_log, config.output_dir
        ),
        "strict_apply": {
            **command_evidence(strict_apply, strict_log, config.output_dir),
            "diagnostics": list(strict_diagnostics),
        },
        "git_ls_files": {
            "command": list(git_ls_files.command),
            "returncode": git_ls_files.returncode,
            "tracked_engine_path_count": len(tracked),
        },
        "byte_identity": {
            "path_count": len(identity),
            "allowlist_count": len(allowlist),
            "outside_allowlist_count": len(identity) - len(allowlist),
            "paths": identity,
        },
        "tree_manifest": {
            "path": manifest_path.relative_to(config.output_dir).as_posix(),
            "entry_count": manifest_payload["entry_count"],
            "sha256": manifest_payload["sha256"],
        },
        "duplicate": command_evidence(
            duplicate, duplicate_log, config.output_dir
        ),
        "reverse": command_evidence(reverse, reverse_log, config.output_dir),
        "locale": {"LC_ALL": "C", "LANG": "C"},
    }
    evidence_path = config.output_dir / "evidence.json"
    atomic_write_json(evidence_path, evidence)
    return evidence_path, evidence


def default_patch_tool():
    apple_patch = Path("/usr/bin/patch")
    if apple_patch.is_file():
        return str(apple_patch)
    return shutil.which("patch") or "patch"


def build_parser():
    parser = argparse.ArgumentParser(
        description=(
            "Strictly replay a numbered engine patch delivery from an official ZIP "
            "and prove byte identity with the tracked workspace."
        )
    )
    parser.add_argument("--repository", type=Path, required=True)
    parser.add_argument("--official-zip", type=Path, required=True)
    parser.add_argument("--expected-zip-sha256", required=True)
    parser.add_argument("--predecessor-start", type=int, required=True)
    parser.add_argument("--predecessor-end", type=int, required=True)
    parser.add_argument("--allowlist-file", type=Path, required=True)
    parser.add_argument("--candidate", type=Path, required=True)
    parser.add_argument("--workspace", type=Path, required=True)
    parser.add_argument(
        "--output-dir",
        type=Path,
        required=True,
        help="New, non-existing directory for logs, manifests, replay, and evidence",
    )
    return parser


def main(argv=None):
    arguments = build_parser().parse_args(argv)
    config = VerificationConfig(
        repository=arguments.repository,
        official_zip=arguments.official_zip,
        expected_zip_sha256=arguments.expected_zip_sha256,
        predecessor_start=arguments.predecessor_start,
        predecessor_end=arguments.predecessor_end,
        allowlist_file=arguments.allowlist_file,
        candidate=arguments.candidate,
        workspace=arguments.workspace,
        output_dir=arguments.output_dir,
        patch_tool=default_patch_tool(),
        git_tool=shutil.which("git") or "git",
    )
    try:
        evidence_path, evidence = verify_delivery(config)
    except (VerificationError, OSError, zipfile.BadZipFile) as error:
        print(f"verification failed: {error}", file=sys.stderr)
        return 1

    print(
        "verified engine patch delivery: "
        f"candidate_sha256={evidence['candidate']['sha256']} "
        f"evidence={evidence_path}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
