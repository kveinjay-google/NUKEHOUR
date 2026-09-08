#!/usr/bin/env python3
"""Fail-closed exact Git staging for the iOS retail-import task boundaries."""

from __future__ import annotations

import argparse
from dataclasses import dataclass
import json
import os
from pathlib import Path, PurePosixPath
import stat
import subprocess
import sys
from types import MappingProxyType
import unicodedata


SCHEMA_VERSION = 1
MAX_MANIFEST_BYTES = 1 << 20
MAX_STAGED_FILE_BYTES = 50 << 20
VALID_PHASES = frozenset(("implementation", "delivery"))

_ROOT_KEYS = frozenset(("schemaVersion", "boundaries"))
_BOUNDARY_KEYS = frozenset(
	(
		"task",
		"phase",
		"requiredPaths",
		"optionalDirtyPaths",
		"allowedUnstagedPaths",
		"minimumStagedCount",
		"maximumStagedCount",
	)
)
_EXCLUDED_SEGMENTS = frozenset(
	(
		".git",
		".hg",
		".svn",
		"__pycache__",
		".pytest_cache",
		".mypy_cache",
		".ruff_cache",
		"node_modules",
		"bin",
		"obj",
		"private-evidence",
		"private_evidence",
		".private-evidence",
	)
)
_EXCLUDED_BASENAMES = frozenset(
	(
		"credentials",
		"credentials.json",
		"cookies",
		"cookies.json",
		"id_ed25519",
		"id_rsa",
		"secret",
		"secrets",
		"token",
		"tokens",
	)
)
_EXCLUDED_SUFFIXES = (
	".cer",
	".ipa",
	".key",
	".log",
	".mobileprovision",
	".p12",
	".pem",
	".pfx",
	".pyc",
	".pyo",
	".trx",
	".xcarchive",
	".xcresult",
)
_GIT_MUTATING_SUBCOMMANDS = frozenset(
	(
		"add",
		"am",
		"apply",
		"checkout",
		"clean",
		"commit",
		"merge",
		"mv",
		"rebase",
		"reset",
		"restore",
		"rm",
		"stash",
		"switch",
		"update-index",
	)
)


class ExactGitStageError(Exception):
	"""A closed staging-boundary validation failure."""


@dataclass(frozen=True)
class StageBoundary:
	task: int
	phase: str
	required_paths: tuple[str, ...]
	optional_dirty_paths: tuple[str, ...]
	allowed_unstaged_paths: tuple[str, ...]
	minimum_staged_count: int
	maximum_staged_count: int


@dataclass(frozen=True)
class StageManifest:
	boundaries: object


def _byte_sorted(values):
	return tuple(sorted(values, key=lambda value: value.encode("utf-8")))


def _strict_json_object(pairs):
	result = {}
	for key, value in pairs:
		if key in result:
			raise ExactGitStageError(f"duplicate JSON key: {key}")
		result[key] = value
	return result


def _regular_non_symlink_file(path: Path, kind: str):
	try:
		mode = os.lstat(path).st_mode
	except FileNotFoundError as error:
		raise ExactGitStageError(f"{kind} does not exist") from error
	except OSError as error:
		raise ExactGitStageError(f"cannot inspect {kind}") from error
	if not stat.S_ISREG(mode) or stat.S_ISLNK(mode):
		raise ExactGitStageError(f"{kind} must be a regular non-symlink file")


def _validate_manifest_path(path):
	path = Path(path)
	_regular_non_symlink_file(path, "manifest")
	try:
		size = path.stat().st_size
	except OSError as error:
		raise ExactGitStageError("cannot inspect manifest") from error
	if size > MAX_MANIFEST_BYTES:
		raise ExactGitStageError("manifest exceeds the 1 MiB limit")
	return path


def _validate_repository_relative_path(value):
	if not isinstance(value, str) or not value:
		raise ExactGitStageError("repository path must be a nonempty string")
	try:
		encoded = value.encode("utf-8")
	except UnicodeEncodeError as error:
		raise ExactGitStageError("repository path must be valid UTF-8") from error
	if len(encoded) > 4096:
		raise ExactGitStageError("repository path is too long")
	if unicodedata.normalize("NFC", value) != value:
		raise ExactGitStageError("repository path must use canonical Unicode framing")
	if any(ord(character) < 32 or ord(character) == 127 for character in value):
		raise ExactGitStageError("repository path contains a control character")
	if "\\" in value:
		raise ExactGitStageError("repository path must use forward slashes")
	if any(character in value for character in "*?[]"):
		raise ExactGitStageError("repository path must not contain a glob")

	path = PurePosixPath(value)
	if path.is_absolute() or str(path) != value:
		raise ExactGitStageError("repository path must be canonical and relative")
	if any(part in ("", ".", "..") for part in path.parts):
		raise ExactGitStageError("repository path must be canonical and relative")
	if path.parts[0].endswith(":"):
		raise ExactGitStageError("repository path must not contain a drive prefix")

	lower_parts = tuple(part.casefold() for part in path.parts)
	basename = lower_parts[-1]
	if (
		any(part in _EXCLUDED_SEGMENTS for part in lower_parts)
		or basename in _EXCLUDED_BASENAMES
		or basename == ".env"
		or basename.startswith(".env.")
		or basename.endswith(_EXCLUDED_SUFFIXES)
	):
		raise ExactGitStageError(f"excluded path: {value}")
	return value


def _validate_path_list(value, field):
	if not isinstance(value, list):
		raise ExactGitStageError(f"{field} must be an array")
	paths = tuple(_validate_repository_relative_path(path) for path in value)
	if paths != _byte_sorted(paths):
		raise ExactGitStageError(f"{field} must be byte-sorted")
	if len(paths) != len(set(paths)):
		raise ExactGitStageError(f"{field} contains a duplicate path")
	aliases = {}
	for path in paths:
		folded = path.casefold()
		if folded in aliases:
			raise ExactGitStageError(
				f"{field} contains a duplicate/case alias: {aliases[folded]} and {path}"
			)
		aliases[folded] = path
	return paths


def load_manifest(path):
	path = _validate_manifest_path(path)
	try:
		text = path.read_text(encoding="utf-8", errors="strict")
	except (OSError, UnicodeError) as error:
		raise ExactGitStageError("manifest is not readable strict UTF-8") from error
	try:
		payload = json.loads(text, object_pairs_hook=_strict_json_object)
	except ExactGitStageError:
		raise
	except (json.JSONDecodeError, ValueError) as error:
		raise ExactGitStageError("manifest is not valid JSON") from error
	if not isinstance(payload, dict) or frozenset(payload) != _ROOT_KEYS:
		raise ExactGitStageError("manifest root shape is invalid")
	if payload["schemaVersion"] != SCHEMA_VERSION or isinstance(
		payload["schemaVersion"], bool
	):
		raise ExactGitStageError("unsupported manifest schemaVersion")
	if not isinstance(payload["boundaries"], list) or not payload["boundaries"]:
		raise ExactGitStageError("manifest boundaries must be a nonempty array")

	boundaries = {}
	for raw in payload["boundaries"]:
		if not isinstance(raw, dict) or frozenset(raw) != _BOUNDARY_KEYS:
			raise ExactGitStageError("manifest boundary shape is invalid")
		task = raw["task"]
		phase = raw["phase"]
		minimum = raw["minimumStagedCount"]
		maximum = raw["maximumStagedCount"]
		if (
			not isinstance(task, int)
			or isinstance(task, bool)
			or not 3 <= task <= 16
		):
			raise ExactGitStageError("boundary task must be an integer from 3 through 16")
		if phase not in VALID_PHASES:
			raise ExactGitStageError("boundary phase is invalid")
		if (
			not isinstance(minimum, int)
			or isinstance(minimum, bool)
			or not isinstance(maximum, int)
			or isinstance(maximum, bool)
		):
			raise ExactGitStageError("boundary staged counts must be integers")

		required = _validate_path_list(raw["requiredPaths"], "requiredPaths")
		optional = _validate_path_list(
			raw["optionalDirtyPaths"], "optionalDirtyPaths"
		)
		allowed = _validate_path_list(
			raw["allowedUnstagedPaths"], "allowedUnstagedPaths"
		)
		all_paths = required + optional + allowed
		folded = [path.casefold() for path in all_paths]
		if len(folded) != len(set(folded)):
			raise ExactGitStageError(
				"required, optional, and allowed-unstaged paths must be disjoint without case aliases"
			)
		if minimum != len(required) or maximum != len(required) + len(optional):
			raise ExactGitStageError(
				"boundary staged counts do not match required/optional path counts"
			)

		key = (task, phase)
		if key in boundaries:
			raise ExactGitStageError("duplicate task/phase boundary")
		boundaries[key] = StageBoundary(
			task,
			phase,
			required,
			optional,
			allowed,
			minimum,
			maximum,
		)

	return StageManifest(MappingProxyType(boundaries))


def _git(repository, *arguments, mutating=False, check=True):
	if not arguments:
		raise ExactGitStageError("missing git subcommand")
	subcommand = arguments[0]
	if mutating:
		if (
			subcommand != "add"
			or len(arguments) != 4
			or arguments[1:3] != ("-f", "--")
		):
			raise ExactGitStageError("forbidden mutating git command")
	elif subcommand in _GIT_MUTATING_SUBCOMMANDS:
		raise ExactGitStageError("mutating git command requires the exact add guard")

	try:
		result = subprocess.run(
			("git", "-C", os.fspath(repository), *arguments),
			check=False,
			stdout=subprocess.PIPE,
			stderr=subprocess.PIPE,
		)
	except OSError as error:
		raise ExactGitStageError("unable to execute git") from error
	if check and result.returncode != 0:
		raise ExactGitStageError(
			f"git {subcommand} failed with exit code {result.returncode}"
		)
	return result


def _decode_git_path_list(raw, kind):
	if not raw:
		return ()
	try:
		parts = raw.decode("utf-8", errors="strict").split("\0")
	except UnicodeError as error:
		raise ExactGitStageError(f"git returned a non-UTF-8 {kind} path") from error
	if parts[-1] != "":
		raise ExactGitStageError(f"git returned malformed {kind} path framing")
	paths = tuple(parts[:-1])
	if len(paths) != len(set(paths)):
		raise ExactGitStageError(f"git returned a duplicate {kind} path")
	for path in paths:
		_validate_repository_relative_path(path)
	return _byte_sorted(paths)


def _git_paths(repository, *arguments, kind):
	return _decode_git_path_list(_git(repository, *arguments).stdout, kind)


def _validate_repository(repository):
	repository = Path(repository)
	if not repository.is_absolute():
		raise ExactGitStageError("repository must be absolute")
	lexical = os.path.abspath(os.fspath(repository))
	if sys.platform == "darwin":
		for alias in ("/var", "/tmp", "/etc"):
			if lexical == alias or lexical.startswith(alias + os.sep):
				lexical = "/private" + lexical
				break
	repository = Path(lexical)
	try:
		mode = os.lstat(repository).st_mode
	except OSError as error:
		raise ExactGitStageError("repository does not exist") from error
	if not stat.S_ISDIR(mode) or stat.S_ISLNK(mode):
		raise ExactGitStageError("repository must be a regular non-symlink directory")
	try:
		root = _git(repository, "rev-parse", "--show-toplevel").stdout.decode(
			"utf-8", errors="strict"
		).strip()
	except UnicodeError as error:
		raise ExactGitStageError("repository root is not valid UTF-8") from error
	if root != os.fspath(repository):
		raise ExactGitStageError("repository must be the exact Git worktree root")
	_git(repository, "rev-parse", "--verify", "HEAD")
	return repository


def _validate_dirty_aliases(dirty_paths, permitted_paths):
	aliases = {}
	for path in dirty_paths:
		folded = path.casefold()
		if folded in aliases and aliases[folded] != path:
			raise ExactGitStageError(
				f"dirty path case alias: {aliases[folded]} and {path}"
			)
		aliases[folded] = path
	permitted_by_case = {path.casefold(): path for path in permitted_paths}
	for path in dirty_paths:
		canonical = permitted_by_case.get(path.casefold())
		if canonical is not None and canonical != path:
			raise ExactGitStageError(
				f"dirty path case alias differs from manifest: {path} versus {canonical}"
			)


def _validate_worktree_file(repository, relative_path, kind):
	current = repository
	parts = PurePosixPath(relative_path).parts
	for index, part in enumerate(parts):
		current /= part
		try:
			mode = os.lstat(current).st_mode
		except FileNotFoundError as error:
			raise ExactGitStageError(f"{kind} does not exist: {relative_path}") from error
		except OSError as error:
			raise ExactGitStageError(f"cannot inspect {kind}: {relative_path}") from error
		if stat.S_ISLNK(mode):
			raise ExactGitStageError(f"{kind} has a symlink component: {relative_path}")
		if index < len(parts) - 1:
			if not stat.S_ISDIR(mode):
				raise ExactGitStageError(
					f"{kind} has a non-directory parent: {relative_path}"
				)
		elif not stat.S_ISREG(mode):
			raise ExactGitStageError(
				f"{kind} must be a regular non-symlink file: {relative_path}"
			)
	if current.stat().st_size > MAX_STAGED_FILE_BYTES:
		raise ExactGitStageError(f"{kind} exceeds the 50 MiB limit: {relative_path}")


def _path_has_real_diff(repository, path, dirty_paths):
	if path in dirty_paths:
		return True
	tracked_at_head = _git(
		repository, "cat-file", "-e", f"HEAD:{path}", check=False
	).returncode == 0
	if not tracked_at_head and os.path.lexists(repository / PurePosixPath(path)):
		return True
	return False


def _write_path_list(output, label, paths):
	output.write(f"{label} ({len(paths)}):\n")
	for path in paths:
		output.write(f"{path}\n")


def stage_exact(repository, manifest_path, task, phase, output=None):
	output = output if output is not None else sys.stdout
	repository = _validate_repository(repository)
	manifest = load_manifest(manifest_path)
	boundary = manifest.boundaries.get((task, phase))
	if boundary is None:
		raise ExactGitStageError("requested task/phase boundary is not in the manifest")
	unmerged = _git_paths(
		repository,
		"diff",
		"--name-only",
		"--diff-filter=U",
		"-z",
		kind="unmerged",
	)
	if unmerged:
		raise ExactGitStageError(f"repository has an unmerged path: {unmerged[0]}")

	staged = _git_paths(
		repository,
		"diff",
		"--cached",
		"--name-only",
		"--no-renames",
		"--diff-filter=ACDMRTUXB",
		"-z",
		kind="cached",
	)
	unstaged = _git_paths(
		repository,
		"diff",
		"--name-only",
		"--no-renames",
		"--diff-filter=ACDMRTUXB",
		"-z",
		kind="dirty",
	)
	untracked = _git_paths(
		repository,
		"ls-files",
		"--others",
		"--exclude-standard",
		"-z",
		kind="untracked",
	)
	dirty = frozenset(staged) | frozenset(unstaged) | frozenset(untracked)
	permitted = (
		boundary.required_paths
		+ boundary.optional_dirty_paths
		+ boundary.allowed_unstaged_paths
	)
	_validate_dirty_aliases(dirty, permitted)

	allowed_set = frozenset(boundary.allowed_unstaged_paths)
	selected_permitted = frozenset(
		boundary.required_paths + boundary.optional_dirty_paths
	)
	for path in staged:
		if path in allowed_set:
			raise ExactGitStageError(f"allowed-unstaged path is cached: {path}")
		if path not in selected_permitted:
			raise ExactGitStageError(
				f"cached path is outside the selected boundary: {path}"
			)
	for path in _byte_sorted(frozenset(unstaged) | frozenset(untracked)):
		if path not in frozenset(permitted):
			raise ExactGitStageError(
				f"dirty path is outside the selected boundary: {path}"
			)

	for path in boundary.required_paths:
		_validate_worktree_file(repository, path, "required path")
		if not _path_has_real_diff(repository, path, dirty):
			raise ExactGitStageError(f"required path has no diff: {path}")

	selected = list(boundary.required_paths)
	for path in boundary.optional_dirty_paths:
		if _path_has_real_diff(repository, path, dirty):
			_validate_worktree_file(repository, path, "optional dirty path")
			selected.append(path)
	for path in boundary.allowed_unstaged_paths:
		if _path_has_real_diff(repository, path, dirty):
			_validate_worktree_file(repository, path, "allowed-unstaged path")

	selected = _byte_sorted(selected)
	if not (
		boundary.minimum_staged_count
		<= len(selected)
		<= boundary.maximum_staged_count
	):
		raise ExactGitStageError("selected staged path count is outside the manifest bounds")

	precheck = _git(
		repository,
		"diff",
		"--check",
		"HEAD",
		"--",
		*selected,
		check=False,
	)
	if precheck.returncode != 0:
		raise ExactGitStageError("candidate diff check failed before index mutation")
	for path in selected:
		if _git(repository, "cat-file", "-e", f"HEAD:{path}", check=False).returncode == 0:
			continue
		new_file_check = _git(
			repository,
			"diff",
			"--no-index",
			"--check",
			"/dev/null",
			os.fspath(repository / PurePosixPath(path)),
			check=False,
		)
		if new_file_check.returncode not in (0, 1):
			raise ExactGitStageError("candidate diff check failed before index mutation")

	_write_path_list(output, "expected staged paths", selected)
	for path in selected:
		_git(repository, "add", "-f", "--", path, mutating=True)

	actual = _git_paths(
		repository,
		"diff",
		"--cached",
		"--name-only",
		"--no-renames",
		"--diff-filter=ACDMRTUXB",
		"-z",
		kind="cached",
	)
	_write_path_list(output, "actual staged paths", actual)
	if actual != selected:
		raise ExactGitStageError("actual staged paths do not equal the expected exact list")
	if not (
		boundary.minimum_staged_count
		<= len(actual)
		<= boundary.maximum_staged_count
	):
		raise ExactGitStageError("actual staged path count is outside the manifest bounds")
	check = _git(repository, "diff", "--cached", "--check", check=False)
	if check.returncode != 0:
		raise ExactGitStageError("cached diff check failed")
	return selected


def _parser():
	parser = argparse.ArgumentParser(
		description="Stage one checked iOS retail-import task boundary exactly."
	)
	subparsers = parser.add_subparsers(dest="command", required=True)
	stage = subparsers.add_parser("stage")
	stage.add_argument("--repository", required=True)
	stage.add_argument("--manifest", required=True)
	stage.add_argument("--task", required=True, type=int)
	stage.add_argument("--phase", required=True, choices=tuple(sorted(VALID_PHASES)))
	return parser


def main(argv=None):
	arguments = _parser().parse_args(argv)
	try:
		if arguments.command == "stage":
			repository = Path(arguments.repository)
			manifest = Path(arguments.manifest)
			if not manifest.is_absolute():
				manifest = repository / manifest
			stage_exact(
				repository,
				manifest,
				arguments.task,
				arguments.phase,
				output=sys.stdout,
			)
			return 0
	except ExactGitStageError as error:
		print(f"exact_git_stage: {error}", file=sys.stderr)
		return 1
	return 1


if __name__ == "__main__":
	sys.exit(main())
