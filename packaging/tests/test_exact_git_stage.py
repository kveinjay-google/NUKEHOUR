from __future__ import annotations

import io
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import unittest

from packaging import exact_git_stage


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "packaging" / "exact_git_stage.py"
MANIFEST = ROOT / "packaging" / "ios-retail-task-paths.json"
PLAN = ROOT / "docs" / "superpowers" / "plans" / "2026-08-29-ios-retail-content-import-hardening.md"
AOT_REGISTRY = "ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs"
MAX_FILE_BYTES = 50 << 20
TASK4_IMPLEMENTATION_PATHS = (
	"OpenRA.Mods.RA2/Content/IosRuntimeManagedImageSource.cs",
	"OpenRA.Mods.RA2/Content/IosRuntimeResourcePreflight.cs",
	"OpenRA.Mods.RA2/Content/IosRuntimeResourceRoot.cs",
	"OpenRA.Mods.RA2/Content/Ra2RuntimeContract.cs",
	"OpenRA.Mods.RA2/Content/Ra2RuntimeContractBuilder.cs",
	"OpenRA.Mods.RA2/Content/RuntimeBuildIdentity.cs",
	"OpenRA.Mods.RA2/Content/RuntimeBuildIdentityReader.cs",
	"engine/OpenRA.Game/Manifest.cs",
	"engine/OpenRA.Game/Network/GameServer.cs",
	"engine/OpenRA.Game/Network/Handshake.cs",
	"engine/OpenRA.Game/Network/Session.cs",
	"engine/OpenRA.Game/Network/UnitOrders.cs",
	"engine/OpenRA.Game/Server/Server.cs",
	"engine/OpenRA.Mods.Common/Widgets/Logic/ServerListLogic.cs",
	"engine/OpenRA.Test/RuntimeContractBuilderTest.cs",
	"engine/OpenRA.Test/RuntimeContractHandshakeTest.cs",
	"ios/OpenRA.iOS/IosRuntimeBuildIdentityProvider.cs",
)


def byte_sorted(values):
	return sorted(values, key=lambda value: value.encode("utf-8"))


def plan_task_section(plan_text, task):
	match = re.search(
		rf"^### Task {task}:.*?(?=^### Task {task + 1}:|\Z)",
		plan_text,
		flags=re.MULTILINE | re.DOTALL,
	)
	if match is None:
		raise AssertionError(f"Task {task} section is missing")
	return match.group(0)


def labeled_path_blocks(section):
	blocks = {}
	current = None
	saw_path = False
	label_pattern = re.compile(r"^\*\*(.+):\*\*$")
	path_pattern = re.compile(
		r"^- (Create|Modify(?: again)?|Rewrite|Verify or deterministically modify): `([^`]+)`$"
	)

	for line in section.splitlines():
		label = label_pattern.match(line)
		if label:
			current = label.group(1)
			blocks[current] = []
			saw_path = False
			continue

		path = path_pattern.match(line)
		if current is not None and path:
			blocks[current].append((path.group(1), path.group(2)))
			saw_path = True
			continue

		if current is not None and saw_path and line.strip():
			current = None

	return blocks


def paths_from(blocks, label):
	if label not in blocks:
		raise AssertionError(f"missing plan file block: {label}")
	return [path for _, path in blocks[label]]


def expected_plan_boundaries():
	plan_text = PLAN.read_text(encoding="utf-8")
	blocks = {
		task: labeled_path_blocks(plan_task_section(plan_text, task))
		for task in range(3, 17)
	}

	implementation_labels = {
		3: ("Outer files", "Exact `0021` engine allowlist"),
		4: ("Outer files", "Engine files for `0022` begun in this task"),
		5: (
			"Outer file refreshed after the Task 5 content-byte changes",
			"Remaining exact `0022` engine paths",
		),
		6: ("Production files committed in this task",),
		7: ("Production/build files committed in this task",),
		8: ("Production files committed in this task",),
		9: ("Outer files", "Exact `0023` engine allowlist"),
		10: ("Outer files", "Exact `0024` engine allowlist"),
		11: ("Exact `0025` engine allowlist",),
		12: ("Outer files", "Exact `0026` engine allowlist"),
		13: ("Outer files",),
		14: (
			"Outer core files",
			"Outer iOS/UI files",
			"Engine production files for `0027`",
			"Engine tests for `0027`",
		),
		16: ("Files",),
	}
	delivery_tasks = (3, 5, 9, 10, 11, 12, 15)
	boundaries = {}

	deferred = []
	deferred_by_task = {}
	for task in range(6, 9):
		label = (
			"Deferred ignored engine tests (created and run now, first force-added in Task 14's `0027` feature commit)"
			if task == 6
			else "Deferred ignored engine tests (created and run now, first force-added in Task 14)"
		)
		deferred.extend(paths_from(blocks[task], label))
		deferred_by_task[task] = tuple(byte_sorted(deferred))

	for task, labels in implementation_labels.items():
		required = []
		for label in labels:
			required.extend(paths_from(blocks[task], label))

		allowed = []
		if 6 <= task <= 13:
			allowed.extend(deferred_by_task[min(task, 8)])
		if task in (13, 14):
			allowed.append(AOT_REGISTRY)

		boundaries[(task, "implementation")] = {
			"requiredPaths": byte_sorted(required),
			"optionalDirtyPaths": [],
			"allowedUnstagedPaths": byte_sorted(allowed),
			"minimumStagedCount": len(required),
			"maximumStagedCount": len(required),
		}

	task15_packaging = blocks[15]["Packaging/iOS files"]
	task15_required = [
		path
		for action, path in task15_packaging
		if action != "Verify or deterministically modify"
	]
	task15_optional = [
		path
		for action, path in task15_packaging
		if action == "Verify or deterministically modify"
	]
	step5_path = re.search(
		r"plus `(engine/OpenRA\.Test/AotObjectRegistryCoverageTest\.cs)`",
		plan_task_section(plan_text, 15),
	)
	if step5_path is None:
		raise AssertionError("Task 15 AOT coverage commit path is missing")
	task15_required.append(step5_path.group(1))
	boundaries[(15, "implementation")] = {
		"requiredPaths": byte_sorted(task15_required),
		"optionalDirtyPaths": byte_sorted(task15_optional),
		"allowedUnstagedPaths": [],
		"minimumStagedCount": len(task15_required),
		"maximumStagedCount": len(task15_required) + len(task15_optional),
	}

	for task in delivery_tasks:
		required = paths_from(blocks[task], "Delivery files")
		allowed = deferred_by_task[8] if 9 <= task <= 12 else ()
		boundaries[(task, "delivery")] = {
			"requiredPaths": byte_sorted(required),
			"optionalDirtyPaths": [],
			"allowedUnstagedPaths": list(allowed),
			"minimumStagedCount": len(required),
			"maximumStagedCount": len(required),
		}

	invoked = {
		(int(task), phase)
		for task, phase in re.findall(
			r"--task (\d+) --phase (implementation|delivery)", plan_text
		)
		if 3 <= int(task) <= 16
	}
	if set(boundaries) != invoked:
		raise AssertionError(
			f"parsed boundaries differ from commit fences: "
			f"missing={byte_sorted(map(str, invoked - set(boundaries)))} "
			f"extra={byte_sorted(map(str, set(boundaries) - invoked))}"
		)

	return boundaries


class GitRepositoryFixture:
	def __init__(self):
		self.temp = tempfile.TemporaryDirectory()
		self.container = Path(self.temp.name)
		self.root = self.container / "repo"
		self.root.mkdir()
		self.manifest = self.container / "manifest.json"
		self.git("init", "-q")
		self.git("config", "user.email", "stage-test@example.invalid")
		self.git("config", "user.name", "Exact Stage Test")
		for path in ("required.txt", "optional.txt", "allowed.txt", "unrelated.txt"):
			self.write(path, f"baseline {path}\n")
		self.git("add", "--", ".")
		self.git("commit", "-q", "-m", "baseline")
		self.write_manifest()

	def close(self):
		self.temp.cleanup()

	def git(self, *arguments, check=True):
		return subprocess.run(
			("git", "-C", os.fspath(self.root), *arguments),
			check=check,
			stdout=subprocess.PIPE,
			stderr=subprocess.PIPE,
			text=True,
		)

	def write(self, relative_path, contents):
		path = self.root / relative_path
		path.parent.mkdir(parents=True, exist_ok=True)
		path.write_text(contents, encoding="utf-8")
		return path

	def write_manifest(
		self,
		*,
		required=("required.txt",),
		optional=("optional.txt",),
		allowed=("allowed.txt",),
		minimum=1,
		maximum=2,
		task=3,
		phase="implementation",
	):
		payload = {
			"schemaVersion": 1,
			"boundaries": [
				{
					"task": task,
					"phase": phase,
					"requiredPaths": list(required),
					"optionalDirtyPaths": list(optional),
					"allowedUnstagedPaths": list(allowed),
					"minimumStagedCount": minimum,
					"maximumStagedCount": maximum,
				}
			],
		}
		self.manifest.write_text(
			json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8"
		)

	def cached_paths(self):
		output = self.git(
			"diff", "--cached", "--name-only", "--no-renames", "-z"
		).stdout
		return [path for path in output.split("\0") if path]

	def head(self):
		return self.git("rev-parse", "HEAD").stdout.strip()


class ExactGitStageTest(unittest.TestCase):
	def setUp(self):
		self.fixture = GitRepositoryFixture()

	def tearDown(self):
		self.fixture.close()

	def assert_rejected_without_index_mutation(self, expected_fragment, action):
		before_paths = self.fixture.cached_paths()
		before_head = self.fixture.head()
		with self.assertRaisesRegex(
			exact_git_stage.ExactGitStageError, re.escape(expected_fragment)
		):
			action()
		self.assertEqual(before_paths, self.fixture.cached_paths())
		self.assertEqual(before_head, self.fixture.head())

	def stage(self, output=None):
		return exact_git_stage.stage_exact(
			self.fixture.root,
			self.fixture.manifest,
			3,
			"implementation",
			output=output or io.StringIO(),
		)

	def test_checked_manifest_is_schema_one_local_regular_and_loadable(self):
		self.assertEqual(SCRIPT.resolve(), Path(exact_git_stage.__file__).resolve())
		self.assertTrue(SCRIPT.is_file())
		self.assertFalse(SCRIPT.is_symlink())
		self.assertTrue(os.access(SCRIPT, os.X_OK))
		self.assertTrue(MANIFEST.is_file())
		self.assertFalse(MANIFEST.is_symlink())
		payload = json.loads(MANIFEST.read_text(encoding="utf-8"))

		self.assertEqual(1, payload["schemaVersion"])
		loaded = exact_git_stage.load_manifest(MANIFEST)
		self.assertEqual(21, len(loaded.boundaries))

	def test_checked_manifest_exactly_mirrors_all_plan_commit_boundaries(self):
		expected = expected_plan_boundaries()
		loaded = exact_git_stage.load_manifest(MANIFEST)
		actual = {}
		for key, boundary in loaded.boundaries.items():
			actual[key] = {
				"requiredPaths": list(boundary.required_paths),
				"optionalDirtyPaths": list(boundary.optional_dirty_paths),
				"allowedUnstagedPaths": list(boundary.allowed_unstaged_paths),
				"minimumStagedCount": boundary.minimum_staged_count,
				"maximumStagedCount": boundary.maximum_staged_count,
			}

		self.assertEqual(expected, actual)
		self.assertEqual(
			TASK4_IMPLEMENTATION_PATHS,
			loaded.boundaries[(4, "implementation")].required_paths,
		)
		self.assertEqual(
			{
				(3, "implementation"): (54, 54),
				(3, "delivery"): (5, 5),
				(4, "implementation"): (17, 17),
				(5, "implementation"): (15, 15),
				(5, "delivery"): (3, 3),
				(6, "implementation"): (5, 5),
				(7, "implementation"): (9, 9),
				(8, "implementation"): (7, 7),
				(9, "implementation"): (13, 13),
				(9, "delivery"): (3, 3),
				(10, "implementation"): (25, 25),
				(10, "delivery"): (3, 3),
				(11, "implementation"): (6, 6),
				(11, "delivery"): (3, 3),
				(12, "implementation"): (10, 10),
				(12, "delivery"): (3, 3),
				(13, "implementation"): (10, 10),
				(14, "implementation"): (38, 38),
				(15, "implementation"): (9, 10),
				(15, "delivery"): (3, 3),
				(16, "implementation"): (5, 5),
			},
			{
				key: (
					boundary.minimum_staged_count,
					boundary.maximum_staged_count,
				)
				for key, boundary in loaded.boundaries.items()
			},
		)

	def test_stage_adds_required_and_dirty_optional_but_never_allowed_unstaged(self):
		self.fixture.write("required.txt", "required changed\n")
		self.fixture.write("optional.txt", "optional changed\n")
		self.fixture.write("allowed.txt", "allowed changed\n")
		before_head = self.fixture.head()
		output = io.StringIO()

		selected = self.stage(output)

		self.assertEqual(("optional.txt", "required.txt"), selected)
		self.assertEqual(["optional.txt", "required.txt"], self.fixture.cached_paths())
		self.assertIn("expected staged paths (2)", output.getvalue())
		self.assertIn("actual staged paths (2)", output.getvalue())
		self.assertIn(" M allowed.txt", self.fixture.git("status", "--short").stdout)
		self.assertEqual(before_head, self.fixture.head())

	def test_stage_omits_clean_optional_and_accepts_exact_minimum(self):
		self.fixture.write("required.txt", "required changed\n")
		selected = self.stage()
		self.assertEqual(("required.txt",), selected)
		self.assertEqual(["required.txt"], self.fixture.cached_paths())

	def test_stage_force_adds_an_exact_ignored_required_file(self):
		self.fixture.write(".gitignore", "ignored/\n")
		self.fixture.git("add", "--", ".gitignore")
		self.fixture.git("commit", "-q", "-m", "ignore fixture")
		self.fixture.write("ignored/required.txt", "ignored but required\n")
		self.fixture.write_manifest(
			required=("ignored/required.txt",),
			optional=(),
			allowed=(),
			maximum=1,
		)

		selected = self.stage()

		self.assertEqual(("ignored/required.txt",), selected)
		self.assertEqual(["ignored/required.txt"], self.fixture.cached_paths())

	def test_required_path_must_exist_be_regular_and_have_a_real_diff(self):
		self.assert_rejected_without_index_mutation("required path has no diff", self.stage)

		self.fixture.write_manifest(required=("missing.txt",), optional=(), allowed=(), maximum=1)
		self.assert_rejected_without_index_mutation("required path does not exist", self.stage)

	def test_unrelated_dirty_untracked_or_cached_paths_are_rejected(self):
		cases = ("tracked", "untracked", "cached")
		for case in cases:
			with self.subTest(case=case):
				fixture = GitRepositoryFixture()
				try:
					fixture.write("required.txt", "required changed\n")
					if case == "tracked":
						fixture.write("unrelated.txt", "unrelated changed\n")
					elif case == "untracked":
						fixture.write("extra.txt", "extra\n")
					else:
						fixture.write("unrelated.txt", "unrelated changed\n")
						fixture.git("add", "--", "unrelated.txt")
					before = fixture.cached_paths()
					with self.assertRaisesRegex(
						exact_git_stage.ExactGitStageError,
						"dirty path is outside the selected boundary"
						if case != "cached"
						else "cached path is outside the selected boundary",
					):
						exact_git_stage.stage_exact(
							fixture.root,
							fixture.manifest,
							3,
							"implementation",
							output=io.StringIO(),
						)
					self.assertEqual(before, fixture.cached_paths())
				finally:
					fixture.close()

	def test_allowed_unstaged_path_is_rejected_if_already_cached(self):
		self.fixture.write("required.txt", "required changed\n")
		self.fixture.write("allowed.txt", "allowed changed\n")
		self.fixture.git("add", "--", "allowed.txt")
		self.assert_rejected_without_index_mutation(
			"allowed-unstaged path is cached", self.stage
		)

	def test_case_alias_of_a_permitted_path_is_rejected_as_extra_dirty_state(self):
		with self.assertRaisesRegex(exact_git_stage.ExactGitStageError, "case alias"):
			exact_git_stage._validate_dirty_aliases(
				("REQUIRED.txt",), ("required.txt",)
			)

	def test_symlink_directory_fifo_and_symlinked_parent_are_rejected(self):
		cases = ("symlink", "directory", "fifo", "symlink-parent")
		for case in cases:
			with self.subTest(case=case):
				fixture = GitRepositoryFixture()
				try:
					relative = f"unsafe-{case}"
					path = fixture.root / relative
					if case == "symlink":
						path.symlink_to(fixture.root / "required.txt")
					elif case == "directory":
						path.mkdir()
					elif case == "fifo":
						os.mkfifo(path)
					else:
						real = fixture.container / "real-parent"
						real.mkdir()
						(real / "file.txt").write_text("unsafe\n", encoding="utf-8")
						path.symlink_to(real, target_is_directory=True)
						relative += "/file.txt"
					allowed = (
						(f"unsafe-{case}",) if case == "symlink-parent" else ()
					)
					fixture.write_manifest(
						required=(relative,), optional=(), allowed=allowed, maximum=1
					)
					with self.assertRaisesRegex(
						exact_git_stage.ExactGitStageError,
						"regular non-symlink file|symlink component",
					):
						exact_git_stage.stage_exact(
							fixture.root,
							fixture.manifest,
							3,
							"implementation",
							output=io.StringIO(),
						)
					self.assertEqual([], fixture.cached_paths())
				finally:
					fixture.close()

	def test_file_over_fifty_mib_is_rejected_before_index_mutation(self):
		large = self.fixture.root / "large.dat"
		with large.open("wb") as stream:
			stream.truncate(MAX_FILE_BYTES + 1)
		self.fixture.write_manifest(
			required=("large.dat",), optional=(), allowed=(), maximum=1
		)
		self.assert_rejected_without_index_mutation("50 MiB", self.stage)

	def test_cached_diff_check_failure_is_rejected_before_index_mutation(self):
		self.fixture.write("required.txt", "trailing whitespace  \n")
		self.assert_rejected_without_index_mutation("diff check failed", self.stage)

	def test_untracked_diff_check_failure_is_rejected_before_index_mutation(self):
		self.fixture.write("new.txt", "trailing whitespace  \n")
		self.fixture.write_manifest(
			required=("new.txt",), optional=(), allowed=(), maximum=1
		)
		self.assert_rejected_without_index_mutation("diff check failed", self.stage)

	def test_unmerged_index_is_rejected_before_any_add(self):
		main_branch = self.fixture.git("branch", "--show-current").stdout.strip()
		self.fixture.git("checkout", "-q", "-b", "conflicting-side")
		self.fixture.write("required.txt", "side\n")
		self.fixture.git("add", "--", "required.txt")
		self.fixture.git("commit", "-q", "-m", "side")
		self.fixture.git("checkout", "-q", main_branch)
		self.fixture.write("required.txt", "main\n")
		self.fixture.git("add", "--", "required.txt")
		self.fixture.git("commit", "-q", "-m", "main")
		merge = self.fixture.git("merge", "conflicting-side", check=False)
		self.assertNotEqual(0, merge.returncode)
		before = self.fixture.cached_paths()

		with self.assertRaisesRegex(
			exact_git_stage.ExactGitStageError, "unmerged path"
		):
			self.stage()

		self.assertEqual(before, self.fixture.cached_paths())

	def test_manifest_rejects_unknown_schema_shape_alias_overlap_and_unsafe_paths(self):
		valid_boundary = {
			"task": 3,
			"phase": "implementation",
			"requiredPaths": ["required.txt"],
			"optionalDirtyPaths": [],
			"allowedUnstagedPaths": [],
			"minimumStagedCount": 1,
			"maximumStagedCount": 1,
		}
		variants = {
			"schema": {"schemaVersion": 2, "boundaries": [valid_boundary]},
			"task-range": {
				"schemaVersion": 1,
				"boundaries": [{**valid_boundary, "task": 17}],
			},
			"unknown": {
				"schemaVersion": 1,
				"boundaries": [{**valid_boundary, "extra": True}],
			},
			"duplicate-boundary": {
				"schemaVersion": 1,
				"boundaries": [valid_boundary, valid_boundary],
			},
			"alias": {
				"schemaVersion": 1,
				"boundaries": [
					{
						**valid_boundary,
						"requiredPaths": ["A.txt", "a.txt"],
						"minimumStagedCount": 2,
						"maximumStagedCount": 2,
					}
				],
			},
			"overlap": {
				"schemaVersion": 1,
				"boundaries": [
					{**valid_boundary, "optionalDirtyPaths": ["required.txt"]}
				],
			},
			"glob": {
				"schemaVersion": 1,
				"boundaries": [{**valid_boundary, "requiredPaths": ["src/*.cs"]}],
			},
			"traversal": {
				"schemaVersion": 1,
				"boundaries": [{**valid_boundary, "requiredPaths": ["../secret"]}],
			},
			"absolute": {
				"schemaVersion": 1,
				"boundaries": [{**valid_boundary, "requiredPaths": ["/tmp/file"]}],
			},
			"backslash": {
				"schemaVersion": 1,
				"boundaries": [{**valid_boundary, "requiredPaths": ["src\\file.cs"]}],
			},
			"noncanonical": {
				"schemaVersion": 1,
				"boundaries": [{**valid_boundary, "requiredPaths": ["src//file.cs"]}],
			},
			"unsorted": {
				"schemaVersion": 1,
				"boundaries": [
					{
						**valid_boundary,
						"requiredPaths": ["z.txt", "a.txt"],
						"minimumStagedCount": 2,
						"maximumStagedCount": 2,
					}
				],
			},
			"bad-count": {
				"schemaVersion": 1,
				"boundaries": [{**valid_boundary, "minimumStagedCount": 0}],
			},
		}
		for name, payload in variants.items():
			with self.subTest(name=name):
				self.fixture.manifest.write_text(
					json.dumps(payload), encoding="utf-8"
				)
				with self.assertRaises(exact_git_stage.ExactGitStageError):
					exact_git_stage.load_manifest(self.fixture.manifest)

	def test_manifest_rejects_secret_cache_build_and_private_evidence_paths(self):
		paths = (
			".env",
			"src/__pycache__/module.pyc",
			"src/bin/output.dll",
			"src/obj/output.dll",
			"reports/private-evidence/device.log",
			"signing/profile.mobileprovision",
		)
		for unsafe in paths:
			with self.subTest(path=unsafe):
				self.fixture.write_manifest(
					required=(unsafe,), optional=(), allowed=(), maximum=1
				)
				with self.assertRaisesRegex(
					exact_git_stage.ExactGitStageError, "excluded path"
				):
					exact_git_stage.load_manifest(self.fixture.manifest)

	def test_manifest_file_itself_must_be_regular_and_not_a_symlink(self):
		target = self.fixture.container / "real-manifest.json"
		target.write_bytes(self.fixture.manifest.read_bytes())
		link = self.fixture.container / "manifest-link.json"
		link.symlink_to(target)
		with self.assertRaisesRegex(
			exact_git_stage.ExactGitStageError, "manifest must be a regular non-symlink file"
		):
			exact_git_stage.load_manifest(link)

	def test_cli_requires_absolute_repository_and_prints_byte_sorted_lists(self):
		self.fixture.write("required.txt", "required changed\n")
		command = (
			os.fspath(SCRIPT),
			"stage",
			"--repository",
			os.fspath(self.fixture.root),
			"--manifest",
			os.fspath(self.fixture.manifest),
			"--task",
			"3",
			"--phase",
			"implementation",
		)
		result = subprocess.run(
			command,
			check=False,
			stdout=subprocess.PIPE,
			stderr=subprocess.PIPE,
			text=True,
		)
		self.assertEqual(0, result.returncode, result.stderr)
		self.assertIn("expected staged paths (1):\nrequired.txt", result.stdout)
		self.assertIn("actual staged paths (1):\nrequired.txt", result.stdout)

		relative = subprocess.run(
			(*command[:3], "repo", *command[4:]),
			cwd=self.fixture.container,
			check=False,
			stdout=subprocess.PIPE,
			stderr=subprocess.PIPE,
			text=True,
		)
		self.assertNotEqual(0, relative.returncode)
		self.assertIn("repository must be absolute", relative.stderr)

	def test_internal_git_guard_permits_only_add_as_a_mutating_subcommand(self):
		for subcommand in ("clean", "commit", "reset", "restore", "rm"):
			with self.subTest(subcommand=subcommand):
				with self.assertRaisesRegex(
					exact_git_stage.ExactGitStageError,
					"forbidden mutating git command",
				):
					exact_git_stage._git(
						self.fixture.root,
						subcommand,
						"placeholder",
						mutating=True,
					)


if __name__ == "__main__":
	unittest.main()
