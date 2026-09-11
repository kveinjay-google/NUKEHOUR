import hashlib
import importlib.util
import json
import os
import subprocess
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "packaging" / "verify_engine_patch_delivery.py"
README = ROOT / "engine-patches" / "README.md"
CHECKED_IN_CANDIDATE = (
    ROOT / "engine-patches" / "0024-runtime-contract-resource-compatibility.patch"
)
OFFICIAL_ROOT = "OpenRA-release-20250330"
EXACT_ALLOWLIST = (
    "OpenRA.Game/Network/NukeHourNetworkCompatibility.cs",
    "OpenRA.Test/NukeHourDirectTcpHandshakeTest.cs",
    "OpenRA.Test/NukeHourNetworkProtocolTest.cs",
)
EXACT_0007_DIAGNOSTICS = (
    "Hunk #1 succeeded at 254 with fuzz 2 (offset -1 lines).",
    "Hunk #5 succeeded at 352 (offset -1 lines).",
)


def load_verifier():
    spec = importlib.util.spec_from_file_location(
        "verify_engine_patch_delivery", SCRIPT
    )
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def unified_patch(path, old, new):
    old_lines = old.splitlines()
    new_lines = new.splitlines()
    if old_lines:
        hunk = (
            f"@@ -1,{len(old_lines)} +1,{len(new_lines)} @@\n"
            + "".join(f"-{line}\n" for line in old_lines)
            + "".join(f"+{line}\n" for line in new_lines)
        )
        old_timestamp = "2026-01-01 00:00:00 +0000"
    else:
        hunk = (
            f"@@ -0,0 +1,{len(new_lines)} @@\n"
            + "".join(f"+{line}\n" for line in new_lines)
        )
        old_timestamp = "1970-01-01 00:00:00 +0000"

    return (
        f"diff -ruN a/{path} b/{path}\n"
        f"--- a/{path}\t{old_timestamp}\n"
        f"+++ b/{path}\t2026-01-01 00:00:01 +0000\n"
        f"{hunk}"
    )


def prefixed_hidden_patch(prefix):
    return "".join(
        prefix + line
        for line in (
            "--- c/hidden.txt\t2026-01-01 00:00:00 +0000\n",
            "+++ d/hidden.txt\t2026-01-01 00:00:01 +0000\n",
            "@@ -1,1 +1,1 @@\n",
            "-hidden-old\n",
            "+hidden-new\n",
        )
    )


def prefixed_traversal_patch(prefix):
    return "".join(
        prefix + line
        for line in (
            "--- a/../victim.txt\t1970-01-01 00:00:00 +0000\n",
            "+++ b/../victim.txt\t2026-01-01 00:00:01 +0000\n",
            "@@ -0,0 +1,1 @@\n",
            "+escaped\n",
        )
    )


def context_traversal_patch(prefix):
    return "".join(
        prefix + line
        for line in (
            "*** a/../victim.txt\t1970-01-01 00:00:00 +0000\n",
            "--- b/safe.txt\t2026-01-01 00:00:01 +0000\n",
            "***************\n",
            "*** 0 ****\n",
            "--- 1 ----\n",
            "+ escaped\n",
        )
    )


def normal_diff_traversal_patch(prefix, separator=" "):
    return "".join(
        prefix + line
        for line in (
            f"Index:{separator}a/../victim.txt\n",
            "===================================================================\n",
            "0a1\n",
            "> escaped\n",
        )
    )


class SyntheticDelivery:
    def __init__(self, root):
        self.root = Path(root)
        self.repository = self.root / "repository"
        self.workspace = self.repository / "engine"
        self.patch_dir = self.repository / "engine-patches"
        self.archive = self.root / "official.zip"
        self.allowlist = self.root / "allowlist.txt"
        self.candidate = self.root / "candidate-with-arbitrary-name.patch"
        self.output = self.root / "evidence-output"

        self.workspace.mkdir(parents=True)
        self.patch_dir.mkdir()
        subprocess.run(
            ["git", "init", "-q", str(self.repository)],
            check=True,
            capture_output=True,
        )

        with zipfile.ZipFile(self.archive, "w", zipfile.ZIP_DEFLATED) as archive:
            archive.writestr(f"{OFFICIAL_ROOT}/outside.txt", "upstream\n")
            archive.writestr(f"{OFFICIAL_ROOT}/allowed.txt", "old\n")

        (self.patch_dir / "0001-predecessor.patch").write_text(
            unified_patch("outside.txt", "upstream\n", "predecessor\n"),
            encoding="utf-8",
        )
        # The exact numeric end must keep this out of predecessor replay.
        (self.patch_dir / "0018-must-not-be-a-predecessor.patch").write_text(
            "not a patch\n", encoding="utf-8"
        )

        (self.workspace / "outside.txt").write_text(
            "predecessor\n", encoding="utf-8"
        )
        (self.workspace / "allowed.txt").write_text("final\n", encoding="utf-8")
        (self.workspace / "new.txt").write_text("created\n", encoding="utf-8")
        subprocess.run(
            [
                "git",
                "-C",
                str(self.repository),
                "add",
                "engine/outside.txt",
                "engine/allowed.txt",
                "engine/new.txt",
            ],
            check=True,
            capture_output=True,
        )

        self.allowlist.write_text("allowed.txt\nnew.txt\n", encoding="utf-8")
        self.candidate.write_text(
            unified_patch("allowed.txt", "old\n", "final\n")
            + unified_patch("new.txt", "", "created\n"),
            encoding="utf-8",
        )

    @property
    def archive_sha256(self):
        return hashlib.sha256(self.archive.read_bytes()).hexdigest()

    def cli(self, *, expected_sha=None, output=None):
        return [
            sys.executable,
            str(SCRIPT),
            "--repository",
            str(self.repository),
            "--official-zip",
            str(self.archive),
            "--expected-zip-sha256",
            expected_sha or self.archive_sha256,
            "--predecessor-start",
            "1",
            "--predecessor-end",
            "1",
            "--allowlist-file",
            str(self.allowlist),
            "--candidate",
            str(self.candidate),
            "--workspace",
            str(self.workspace),
            "--output-dir",
            str(output or self.output),
        ]

    def run(self, **kwargs):
        return subprocess.run(
            self.cli(**kwargs),
            check=False,
            capture_output=True,
            text=True,
        )


class ScriptPresenceTest(unittest.TestCase):
    def test_verifier_is_checked_in(self):
        self.assertTrue(SCRIPT.is_file(), f"Missing verifier: {SCRIPT}")


@unittest.skipUnless(SCRIPT.is_file(), "verifier not implemented yet")
class EnginePatchDeliveryUnitTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.verifier = load_verifier()

    def test_cli_exposes_every_delivery_input(self):
        result = subprocess.run(
            [sys.executable, str(SCRIPT), "--help"],
            check=False,
            capture_output=True,
            text=True,
        )

        self.assertEqual(0, result.returncode, result.stderr)
        for option in (
            "--repository",
            "--official-zip",
            "--expected-zip-sha256",
            "--predecessor-start",
            "--predecessor-end",
            "--allowlist-file",
            "--candidate",
            "--workspace",
            "--output-dir",
        ):
            self.assertIn(option, result.stdout)

    def test_exact_numeric_predecessor_range_rejects_gaps_and_duplicates(self):
        with tempfile.TemporaryDirectory() as temporary:
            repository = Path(temporary)
            patches = repository / "engine-patches"
            patches.mkdir()
            (patches / "0001-one.patch").write_bytes(b"one")
            (patches / "0003-three.patch").write_bytes(b"three")

            with self.assertRaisesRegex(self.verifier.VerificationError, "0002"):
                self.verifier.select_predecessors(repository, 1, 3)

            (patches / "0002-two-a.patch").write_bytes(b"two")
            (patches / "0002-two-b.patch").write_bytes(b"two")
            with self.assertRaisesRegex(self.verifier.VerificationError, "exactly one"):
                self.verifier.select_predecessors(repository, 1, 3)

    def test_exact_numeric_range_never_self_includes_0018(self):
        with tempfile.TemporaryDirectory() as temporary:
            repository = Path(temporary)
            patches = repository / "engine-patches"
            patches.mkdir()
            for number in range(1, 19):
                (patches / f"{number:04d}-fixture.patch").write_bytes(b"patch")

            selected = self.verifier.select_predecessors(repository, 1, 17)

        self.assertEqual(17, len(selected))
        self.assertEqual("0017-fixture.patch", selected[-1].name)
        self.assertNotIn("0018-fixture.patch", {path.name for path in selected})

    def test_header_parser_requires_exact_unique_paired_sections(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            valid = root / "valid.patch"
            valid.write_text(
                unified_patch("allowed.txt", "old\n", "new\n"),
                encoding="utf-8",
            )
            self.assertEqual(
                ("allowed.txt",),
                self.verifier.validate_candidate_headers(
                    valid, ("allowed.txt",)
                ),
            )

            duplicate = root / "duplicate.patch"
            duplicate.write_bytes(valid.read_bytes() + valid.read_bytes())
            with self.assertRaisesRegex(self.verifier.VerificationError, "duplicate"):
                self.verifier.validate_candidate_headers(
                    duplicate, ("allowed.txt",)
                )

            mismatched = root / "mismatched.patch"
            mismatched.write_text(
                valid.read_text(encoding="utf-8").replace(
                    "+++ b/allowed.txt", "+++ b/other.txt"
                ),
                encoding="utf-8",
            )
            with self.assertRaisesRegex(self.verifier.VerificationError, "paired"):
                self.verifier.validate_candidate_headers(
                    mismatched, ("allowed.txt", "other.txt")
                )

            unmarked = root / "unmarked.patch"
            unmarked.write_text(
                valid.read_text(encoding="utf-8").replace(
                    "diff -ruN a/allowed.txt b/allowed.txt\n", ""
                ),
                encoding="utf-8",
            )
            with self.assertRaisesRegex(self.verifier.VerificationError, "diff -ruN"):
                self.verifier.validate_candidate_headers(
                    unmarked, ("allowed.txt",)
                )

    def test_header_parser_rejects_unexpected_and_unsafe_paths(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            unexpected = root / "unexpected.patch"
            unexpected.write_text(
                unified_patch("allowed.txt", "old\n", "new\n")
                + unified_patch("extra.txt", "old\n", "new\n"),
                encoding="utf-8",
            )
            with self.assertRaisesRegex(self.verifier.VerificationError, "allowlist"):
                self.verifier.validate_candidate_headers(
                    unexpected, ("allowed.txt",)
                )

            traversal = root / "traversal.patch"
            traversal.write_text(
                unified_patch("../escape.txt", "old\n", "new\n"),
                encoding="utf-8",
            )
            with self.assertRaisesRegex(self.verifier.VerificationError, "unsafe"):
                self.verifier.validate_candidate_headers(
                    traversal, ("../escape.txt",)
                )

    def test_header_parser_rejects_unmarked_nonstandard_prefix_file_headers(self):
        with tempfile.TemporaryDirectory() as temporary:
            candidate = Path(temporary) / "hidden-prefix.patch"
            candidate.write_text(
                unified_patch("allowed.txt", "old\n", "new\n")
                + "--- c/hidden.txt\t2026-01-01 00:00:00 +0000\n"
                + "+++ d/hidden.txt\t2026-01-01 00:00:01 +0000\n"
                + "@@ -1,1 +1,1 @@\n"
                + "-hidden-old\n"
                + "+hidden-new\n",
                encoding="utf-8",
            )

            with self.assertRaisesRegex(
                self.verifier.VerificationError, "file headers"
            ):
                self.verifier.validate_candidate_headers(
                    candidate, ("allowed.txt",)
                )

    def test_unified_diff_grammar_consumes_hunk_counts_and_rejects_other_formats(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            valid_text = unified_patch("allowed.txt", "old\n", "new\n")
            cases = {
                "malformed hunk count": valid_text.replace(
                    "@@ -1,1 +1,1 @@", "@@ -1,2 +1,1 @@"
                ),
                "trailing garbage": valid_text + "trailing garbage\n",
                "index preamble": "index 1234567..89abcde 100644\n" + valid_text,
                "binary section": (
                    "diff -ruN a/allowed.txt b/allowed.txt\n"
                    "Binary files a/allowed.txt and b/allowed.txt differ\n"
                ),
                "rename metadata": valid_text + "rename from allowed.txt\n",
            }
            for name, contents in cases.items():
                with self.subTest(name=name):
                    candidate = root / f"{name.replace(' ', '-')}.patch"
                    candidate.write_text(contents, encoding="utf-8")
                    with self.assertRaises(self.verifier.VerificationError):
                        self.verifier.validate_candidate_headers(
                            candidate, ("allowed.txt",)
                        )

    def test_unified_diff_grammar_accepts_exact_no_newline_markers(self):
        with tempfile.TemporaryDirectory() as temporary:
            candidate = Path(temporary) / "no-newline.patch"
            candidate.write_text(
                "diff -ruN a/allowed.txt b/allowed.txt\n"
                "--- a/allowed.txt\t2026-01-01 00:00:00 +0000\n"
                "+++ b/allowed.txt\t2026-01-01 00:00:01 +0000\n"
                "@@ -1,1 +1,1 @@\n"
                "-old\n"
                "\\ No newline at end of file\n"
                "+new\n"
                "\\ No newline at end of file\n",
                encoding="utf-8",
            )

            self.assertEqual(
                ("allowed.txt",),
                self.verifier.validate_candidate_headers(
                    candidate, ("allowed.txt",)
                ),
            )

    def test_workspace_allowlist_rejects_file_and_parent_symlinks(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            real = root / "real.txt"
            real.write_bytes(b"real")
            (root / "linked.txt").symlink_to(real)
            with self.assertRaisesRegex(self.verifier.VerificationError, "symlink"):
                self.verifier.validate_workspace_paths(root, ("linked.txt",))

            real_directory = root / "real-directory"
            real_directory.mkdir()
            (real_directory / "nested.txt").write_bytes(b"nested")
            (root / "linked-directory").symlink_to(real_directory)
            with self.assertRaisesRegex(self.verifier.VerificationError, "symlink"):
                self.verifier.validate_workspace_paths(
                    root, ("linked-directory/nested.txt",)
                )

    def test_0007_diagnostics_are_a_fixed_exact_tuple(self):
        output = (
            "patching file 'OpenRA.Game/Settings.cs'\n"
            + "\n".join(EXACT_0007_DIAGNOSTICS)
            + "\n"
        ).encode()
        self.assertEqual(
            EXACT_0007_DIAGNOSTICS,
            self.verifier.validate_predecessor_diagnostics(
                "0007-language-restart-and-system-default.patch", output
            ),
        )

        for name, altered in (
            (
                "0007-language-restart-and-system-default.patch",
                output.replace(b"fuzz 2", b"fuzz 1"),
            ),
            ("0006-settings.patch", output),
            (
                "0007-language-restart-and-system-default.patch",
                output
                + b"Hunk #9 succeeded at 999 with fuzz 1 (offset 2 lines).\n",
            ),
        ):
            with self.subTest(name=name, altered=altered):
                with self.assertRaisesRegex(
                    self.verifier.VerificationError, "offset/fuzz"
                ):
                    self.verifier.validate_predecessor_diagnostics(name, altered)

    def test_reject_and_backup_files_are_fatal(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "clean.txt").write_bytes(b"clean")
            self.verifier.ensure_no_patch_residue(root)
            (root / "failed.rej").write_bytes(b"reject")
            with self.assertRaisesRegex(self.verifier.VerificationError, "failed.rej"):
                self.verifier.ensure_no_patch_residue(root)

            (root / "failed.rej").unlink()
            (root / "backup.orig").write_bytes(b"backup")
            with self.assertRaisesRegex(self.verifier.VerificationError, "backup.orig"):
                self.verifier.ensure_no_patch_residue(root)

    def test_manifest_sorts_paths_by_filesystem_bytes_and_rejects_symlinks(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for name in ("z.txt", "A.txt", "é.txt"):
                (root / name).write_text(name, encoding="utf-8")

            manifest = self.verifier.build_tree_manifest(root)
            expected = tuple(
                sorted(("z.txt", "A.txt", "é.txt"), key=os.fsencode)
            )
            self.assertEqual(expected, tuple(entry.path for entry in manifest))
            self.assertTrue(all(len(entry.sha256) == 64 for entry in manifest))

            (root / "linked.txt").symlink_to(root / "A.txt")
            with self.assertRaisesRegex(self.verifier.VerificationError, "symlink"):
                self.verifier.build_tree_manifest(root)

    def test_duplicate_refusal_detects_a_runner_that_mutates_bytes(self):
        verifier = self.verifier

        class MutatingRunner:
            def run(self, command, *, cwd, stdin_path, phase):
                self.command = tuple(command)
                self.phase = phase
                (Path(cwd) / "tracked.txt").write_bytes(b"mutated")
                return verifier.CommandResult(tuple(command), 1, b"refused")

        with tempfile.TemporaryDirectory() as temporary:
            replay = Path(temporary)
            (replay / "tracked.txt").write_bytes(b"before")
            candidate = replay / "candidate.patch"
            candidate.write_bytes(b"candidate")
            runner = MutatingRunner()

            with self.assertRaisesRegex(verifier.VerificationError, "mutated"):
                verifier.verify_duplicate_refusal(
                    replay, candidate, "/usr/bin/patch", runner
                )

        self.assertEqual("duplicate-forward-dry-run", runner.phase)
        self.assertIn("--forward", runner.command)
        self.assertIn("--dry-run", runner.command)

    def test_reverse_dry_run_failure_is_fatal(self):
        verifier = self.verifier

        class FailingRunner:
            def run(self, command, *, cwd, stdin_path, phase):
                self.command = tuple(command)
                self.phase = phase
                return verifier.CommandResult(tuple(command), 1, b"cannot reverse")

        with tempfile.TemporaryDirectory() as temporary:
            replay = Path(temporary)
            (replay / "tracked.txt").write_bytes(b"after")
            candidate = replay / "candidate.patch"
            candidate.write_bytes(b"candidate")
            runner = FailingRunner()

            with self.assertRaisesRegex(verifier.VerificationError, "reverse"):
                verifier.verify_reverse_dry_run(
                    replay, candidate, "/usr/bin/patch", runner
                )

        self.assertEqual("reverse-dry-run", runner.phase)
        self.assertIn("--reverse", runner.command)
        self.assertIn("--dry-run", runner.command)


@unittest.skipUnless(SCRIPT.is_file(), "verifier not implemented yet")
class EnginePatchDeliverySyntheticCliTest(unittest.TestCase):
    def test_end_to_end_fixture_writes_atomic_evidence_and_full_manifest(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = SyntheticDelivery(temporary)
            result = fixture.run()

            self.assertEqual(0, result.returncode, result.stderr)
            evidence_path = fixture.output / "evidence.json"
            manifest_path = fixture.output / "tree-manifest.json"
            self.assertTrue(evidence_path.is_file())
            self.assertTrue(manifest_path.is_file())
            self.assertFalse(any(fixture.output.glob(".evidence.json.*")))

            evidence = json.loads(evidence_path.read_text(encoding="utf-8"))
            self.assertTrue(evidence["passed"])
            self.assertEqual(fixture.archive_sha256, evidence["archive"]["sha256"])
            self.assertEqual(
                ["allowed.txt", "new.txt"], evidence["candidate"]["headers"]
            )
            self.assertEqual(1, len(evidence["predecessors"]))
            self.assertEqual(3, evidence["byte_identity"]["path_count"])
            self.assertEqual(3, evidence["tree_manifest"]["entry_count"])
            self.assertNotEqual(0, evidence["duplicate"]["returncode"])
            self.assertEqual(0, evidence["reverse"]["returncode"])
            self.assertTrue(
                (fixture.output / "predecessors-combined.log").is_file()
            )

    def test_wrong_zip_hash_fails_before_evidence_is_published(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = SyntheticDelivery(temporary)
            result = fixture.run(expected_sha="0" * 64)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("ZIP SHA-256", result.stderr)
            self.assertFalse((fixture.output / "evidence.json").exists())

    def test_unexpected_candidate_header_fails_before_apply(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = SyntheticDelivery(temporary)
            fixture.candidate.write_text(
                fixture.candidate.read_text(encoding="utf-8")
                + unified_patch("extra.txt", "old\n", "new\n"),
                encoding="utf-8",
            )
            result = fixture.run()

            self.assertNotEqual(0, result.returncode)
            self.assertIn("allowlist", result.stderr)
            self.assertFalse((fixture.output / "evidence.json").exists())

    def test_outside_allowlist_workspace_drift_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = SyntheticDelivery(temporary)
            (fixture.workspace / "outside.txt").write_text(
                "unpatched workspace drift\n", encoding="utf-8"
            )
            result = fixture.run()

            self.assertNotEqual(0, result.returncode)
            self.assertIn("outside allowlist", result.stderr)
            self.assertFalse((fixture.output / "evidence.json").exists())

    def test_hidden_official_tree_tamper_outside_git_union_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = SyntheticDelivery(temporary)
            with zipfile.ZipFile(fixture.archive, "a", zipfile.ZIP_DEFLATED) as archive:
                archive.writestr(f"{OFFICIAL_ROOT}/hidden.txt", "hidden-old\n")
            fixture.candidate.write_text(
                fixture.candidate.read_text(encoding="utf-8")
                + "--- c/hidden.txt\t2026-01-01 00:00:00 +0000\n"
                + "+++ d/hidden.txt\t2026-01-01 00:00:01 +0000\n"
                + "@@ -1,1 +1,1 @@\n"
                + "-hidden-old\n"
                + "+hidden-new\n",
                encoding="utf-8",
            )
            result = fixture.run()

            self.assertNotEqual(0, result.returncode)
            self.assertIn("file headers", result.stderr)
            self.assertFalse((fixture.output / "evidence.json").exists())

    def assert_prefixed_hidden_tamper_is_rejected(self, prefix):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = SyntheticDelivery(temporary)
            with zipfile.ZipFile(fixture.archive, "a", zipfile.ZIP_DEFLATED) as archive:
                archive.writestr(f"{OFFICIAL_ROOT}/hidden.txt", "hidden-old\n")
            fixture.candidate.write_text(
                fixture.candidate.read_text(encoding="utf-8")
                + prefixed_hidden_patch(prefix),
                encoding="utf-8",
            )

            result = fixture.run()

            self.assertNotEqual(0, result.returncode)
            self.assertIn("unsupported or trailing", result.stderr)
            self.assertFalse((fixture.output / "evidence.json").exists())
            self.assertFalse((fixture.output / "git-apply-check.log").exists())
            self.assertFalse(
                (fixture.output / "fresh-official-extraction").exists()
            )

    def test_whitespace_prefixed_hidden_official_tree_tamper_is_rejected(self):
        self.assert_prefixed_hidden_tamper_is_rejected(" ")

    def test_arbitrary_prefix_hidden_official_tree_tamper_is_rejected(self):
        self.assert_prefixed_hidden_tamper_is_rejected("X")

    def test_predecessor_path_traversal_is_rejected_before_apple_patch(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = SyntheticDelivery(temporary)
            predecessor = fixture.patch_dir / "0001-predecessor.patch"
            predecessor.write_text(
                predecessor.read_text(encoding="utf-8")
                + prefixed_traversal_patch("X"),
                encoding="utf-8",
            )

            result = fixture.run()

            escaped = (
                fixture.output
                / "fresh-official-extraction"
                / "victim.txt"
            )
            self.assertFalse(escaped.exists())
            self.assertNotEqual(0, result.returncode)
            self.assertIn("unsafe predecessor file header", result.stderr)
            self.assertFalse((fixture.output / "evidence.json").exists())

    def test_context_predecessor_path_traversal_is_rejected_before_apple_patch(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = SyntheticDelivery(temporary)
            predecessor = fixture.patch_dir / "0001-predecessor.patch"
            predecessor.write_text(
                predecessor.read_text(encoding="utf-8")
                + context_traversal_patch("X"),
                encoding="utf-8",
            )

            result = fixture.run()

            escaped = fixture.output / "fresh-official-extraction" / "victim.txt"
            self.assertFalse(escaped.exists())
            self.assertNotEqual(0, result.returncode)
            self.assertIn("unsafe predecessor file header", result.stderr)
            self.assertFalse((fixture.output / "evidence.json").exists())

    def test_index_normal_diff_path_traversal_is_rejected_before_apple_patch(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = SyntheticDelivery(temporary)
            predecessor = fixture.patch_dir / "0001-predecessor.patch"
            predecessor.write_text(
                predecessor.read_text(encoding="utf-8")
                + normal_diff_traversal_patch("X"),
                encoding="utf-8",
            )

            result = fixture.run()

            escaped = fixture.output / "fresh-official-extraction" / "victim.txt"
            self.assertFalse(escaped.exists())
            self.assertNotEqual(0, result.returncode)
            self.assertIn("unsafe predecessor file header", result.stderr)
            self.assertFalse((fixture.output / "evidence.json").exists())

    def test_tab_index_normal_diff_path_traversal_is_rejected_before_apple_patch(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = SyntheticDelivery(temporary)
            predecessor = fixture.patch_dir / "0001-predecessor.patch"
            predecessor.write_text(
                predecessor.read_text(encoding="utf-8")
                + normal_diff_traversal_patch("X", "\t"),
                encoding="utf-8",
            )

            result = fixture.run()

            escaped = fixture.output / "fresh-official-extraction" / "victim.txt"
            self.assertFalse(escaped.exists())
            self.assertNotEqual(0, result.returncode)
            self.assertIn("unsafe predecessor file header", result.stderr)
            self.assertFalse((fixture.output / "evidence.json").exists())

    def test_all_predecessors_are_path_validated_before_first_patch_runs(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = SyntheticDelivery(temporary)
            (fixture.patch_dir / "0002-traversal.patch").write_text(
                prefixed_traversal_patch("X"), encoding="utf-8"
            )
            arguments = fixture.cli()
            end_index = arguments.index("--predecessor-end")
            arguments[end_index + 1] = "2"

            result = subprocess.run(
                arguments,
                check=False,
                capture_output=True,
                text=True,
            )

            replay_outside = (
                fixture.output
                / "fresh-official-extraction"
                / OFFICIAL_ROOT
                / "outside.txt"
            )
            self.assertNotEqual(0, result.returncode)
            self.assertEqual("upstream\n", replay_outside.read_text(encoding="utf-8"))
            self.assertFalse(
                (fixture.output / "fresh-official-extraction" / "victim.txt").exists()
            )
            self.assertFalse((fixture.output / "evidence.json").exists())

    def test_candidate_filename_that_looks_like_a_git_option_is_checked_as_a_file(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = SyntheticDelivery(temporary)
            option_named_candidate = fixture.root / "--allow-empty"
            fixture.candidate.rename(option_named_candidate)
            arguments = fixture.cli()
            candidate_index = arguments.index("--candidate")
            arguments[candidate_index:candidate_index + 2] = [
                "--candidate=--allow-empty"
            ]

            result = subprocess.run(
                arguments,
                cwd=fixture.root,
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(0, result.returncode, result.stderr)
            evidence = json.loads(
                (fixture.output / "evidence.json").read_text(encoding="utf-8")
            )
            command = evidence["git_apply_check"]["command"]
            git_log = (
                fixture.output / evidence["git_apply_check"]["log"]
            ).read_text(encoding="utf-8")
            self.assertIn("Checking patch allowed.txt", git_log)
            self.assertIn("Checking patch new.txt", git_log)
            self.assertEqual("--", command[-2])
            self.assertEqual(str(option_named_candidate.resolve()), command[-1])
            self.assertEqual(
                str(option_named_candidate.resolve()), evidence["candidate"]["path"]
            )


@unittest.skipUnless(SCRIPT.is_file(), "verifier not implemented yet")
class CheckedInDeliveryContractTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.verifier = load_verifier()

    def test_checked_in_0024_has_the_exact_three_paired_headers(self):
        self.assertTrue(
            CHECKED_IN_CANDIDATE.is_file(),
            f"Missing checked-in candidate: {CHECKED_IN_CANDIDATE}",
        )
        self.assertEqual(
            EXACT_ALLOWLIST,
            self.verifier.validate_candidate_headers(
                CHECKED_IN_CANDIDATE, EXACT_ALLOWLIST
            ),
        )

    def test_current_predecessor_series_has_776_safe_file_headers(self):
        predecessors = self.verifier.select_predecessors(ROOT, 1, 23)
        headers = tuple(
            header
            for predecessor in predecessors
            for header in self.verifier.validate_predecessor_header_paths(
                predecessor
            )
        )

        self.assertEqual(776, len(headers))

    def test_readme_pins_0024_and_the_exact_0007_diagnostics(self):
        readme = README.read_text(encoding="utf-8")
        self.assertIn(
            "0024-runtime-contract-resource-compatibility.patch", readme
        )
        for path in EXACT_ALLOWLIST:
            self.assertIn(path, readme)

        exact_block = "\n".join(EXACT_0007_DIAGNOSTICS)
        self.assertEqual(1, readme.count(exact_block))
        self.assertIn(
            'git apply --check --verbose -p1 -- "$CANDIDATE"', readme
        )
        self.assertEqual(
            EXACT_0007_DIAGNOSTICS,
            self.verifier.KNOWN_0007_DIAGNOSTICS,
        )


if __name__ == "__main__":
    unittest.main()
