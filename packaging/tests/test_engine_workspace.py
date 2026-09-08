import contextlib
import hashlib
import io
import os
from pathlib import Path
import stat
import subprocess
import tempfile
import unittest
from unittest import mock
import zipfile

from packaging import engine_workspace as ew


EXPECTED_ALLOWLISTS = {
    "0020": """OpenRA.Game/FileFormats/Png.cs
OpenRA.Game/FileSystem/BoundedZipEnvelope.cs
OpenRA.Game/FileSystem/FileSystem.cs
OpenRA.Game/FileSystem/IPackage.cs
OpenRA.Game/FileSystem/PackageEnvelopePolicy.cs
OpenRA.Game/FileSystem/PackageReadContext.cs
OpenRA.Game/FileSystem/ZipFile.cs
OpenRA.Game/Primitives/SegmentStream.cs
OpenRA.Mods.Cnc/FileFormats/IdxBagEnvelopeValidator.cs
OpenRA.Mods.Cnc/FileFormats/IdxReader.cs
OpenRA.Mods.Cnc/FileFormats/XccGlobalDatabase.cs
OpenRA.Mods.Cnc/FileFormats/XccLocalDatabase.cs
OpenRA.Mods.Cnc/FileSystem/MixEnvelopeValidator.cs
OpenRA.Mods.Cnc/FileSystem/MixFile.cs
OpenRA.Test/IdxBagEnvelopeTest.cs
OpenRA.Test/IosImportEnvelopePolicyV1Test.cs
OpenRA.Test/MixEnvelopeTest.cs
OpenRA.Test/OpenRA.Game/PngTest.cs
OpenRA.Test/RetailArchiveGraphValidatorTest.cs
OpenRA.Test/RetailCandidatePlanTest.cs
OpenRA.Test/RetailContentCatalogTest.cs
OpenRA.Test/RetailSourceDiscoveryTest.cs
OpenRA.Test/SyntheticRetailFixtureBuilder.cs
OpenRA.Test/ZipEnvelopeTest.cs
""",
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
OpenRA.Test/TrustedManifestEntryTest.cs
""",
    "0022": """OpenRA.Game/Game.cs
OpenRA.Game/GameInformation.cs
OpenRA.Game/Manifest.cs
OpenRA.Game/Network/GameSave.cs
OpenRA.Game/Network/GameServer.cs
OpenRA.Game/Network/Handshake.cs
OpenRA.Game/Network/ReplayRecorder.cs
OpenRA.Game/Network/Session.cs
OpenRA.Game/Network/UnitOrders.cs
OpenRA.Game/RuntimeContractStorage.cs
OpenRA.Game/Server/Server.cs
OpenRA.Game/Settings.cs
OpenRA.Mods.Common/ServerTraits/SkirmishLogic.cs
OpenRA.Mods.Common/Widgets/Logic/GameSaveBrowserLogic.cs
OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs
OpenRA.Mods.Common/Widgets/Logic/ReplayBrowserLogic.cs
OpenRA.Mods.Common/Widgets/Logic/ServerListLogic.cs
OpenRA.Test/RuntimeContractBuilderTest.cs
OpenRA.Test/RuntimeContractHandshakeTest.cs
OpenRA.Test/RuntimeContractPersistenceTest.cs
mods/common/fluent/chrome.ftl
mods/common/fluent/zh-CN/chrome.ftl
""",
    "0023": """OpenRA.Game/Map/Map.cs
OpenRA.Game/Map/MapPlayers.cs
OpenRA.Game/Map/PlayerReference.cs
OpenRA.Mods.Cnc/MapImport/Gen2MapImportModels.cs
OpenRA.Mods.Cnc/MapImport/Gen2MapImporter.cs
OpenRA.Mods.Cnc/UtilityCommands/ImportGen2MapCommand.cs
OpenRA.Mods.Cnc/UtilityCommands/ImportTiberianSunMapCommand.cs
OpenRA.Test/Gen2MapImporterTest.cs
OpenRA.Test/Ra2MapConversionServiceTest.cs
""",
    "0024": """OpenRA.Game/FileFormats/Png.cs
OpenRA.Game/FileSystem/ZipFile.cs
OpenRA.Game/Graphics/PlatformInterfaces.cs
OpenRA.Game/Graphics/SheetBuilder.cs
OpenRA.Game/Map/ActorReference.cs
OpenRA.Game/Map/MapCache.cs
OpenRA.Game/Map/MapPreview.cs
OpenRA.Game/Map/MapPreviewImageCache.cs
OpenRA.Game/Map/MapUid.cs
OpenRA.Game/Map/VerifiedMapSnapshot.cs
OpenRA.Game/Renderer.cs
OpenRA.Mods.Common/Widgets/Logic/MapChooserLogic.cs
OpenRA.Mods.Common/Widgets/MapPreviewWidget.cs
OpenRA.Platforms.Default/OpenGL.cs
OpenRA.Platforms.Default/Sdl2GraphicsContext.cs
OpenRA.Platforms.Default/ThreadedGraphicsContext.cs
OpenRA.Test/BoundedOramapEnvelopeTest.cs
OpenRA.Test/MapPreviewImageCacheTest.cs
OpenRA.Test/OpenRA.Game/PngTest.cs
OpenRA.Test/Ra2MapValidationServiceTest.cs
OpenRA.Test/SheetBuilderBoundsTest.cs
""",
    "0025": """OpenRA.Game/Map/MapCache.cs
OpenRA.Game/Map/MapPreview.cs
OpenRA.Game/Map/RemoteMapMetadata.cs
OpenRA.Game/Map/VerifiedRemoteMapMetadataClient.cs
OpenRA.Test/MapCacheRemoteMetadataTest.cs
OpenRA.Test/VerifiedRemoteMapMetadataClientTest.cs
""",
    "0026": """OpenRA.Game/Map/MapCache.cs
OpenRA.Game/Map/MapPreview.cs
OpenRA.Game/Map/RuntimeMapDownload.cs
OpenRA.Game/Map/VerifiedMapSnapshot.cs
OpenRA.Test/MapPreviewRuntimeInstallTest.cs
OpenRA.Test/RuntimeMapStoreRecoveryTest.cs
OpenRA.Test/VerifiedRuntimeMapStoreTest.cs
""",
    "0027": """OpenRA.Game/Map/MapCache.cs
OpenRA.Game/Map/VerifiedMapSnapshot.cs
OpenRA.Game/Support/IosGameContentManagement.cs
OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs
OpenRA.Test/AotObjectRegistryCoverageTest.cs
OpenRA.Test/IosContentImportContractTest.cs
OpenRA.Test/IosContentWizardLayoutPolicyTest.cs
OpenRA.Test/IosGameContentManagementTest.cs
OpenRA.Test/RetailActiveCatalogTest.cs
OpenRA.Test/RetailContentImporterTest.cs
OpenRA.Test/RetailContentImporterTransactionTest.cs
OpenRA.Test/RetailContentWizardStateMachineTest.cs
OpenRA.Test/RetailGenerationStoreTest.cs
OpenRA.Test/RetailImportDurabilityTest.cs
OpenRA.Test/RetailImportMetadataTest.cs
OpenRA.Test/RetailImportRecoveryProcessTest.cs
OpenRA.Test/RetailImportSourceSessionContractTest.cs
OpenRA.Test/RetailLegacyMigrationTest.cs
OpenRA.Test/RetailPendingSnapshotStoreTest.cs
OpenRA.Test/VerifiedCatalogPackageTest.cs
""",
}


class EngineWorkspaceTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def make_zip(self, entries, root="OpenRA-release-20250330"):
        path = self.root / "official.zip"
        with zipfile.ZipFile(path, "w") as archive:
            for name, data, attrs in entries:
                info = zipfile.ZipInfo(f"{root}/{name}" if root else name)
                info.external_attr = attrs
                archive.writestr(info, data)
        return path

    def invoke(self, argv):
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            rc = ew.main(argv)
        return rc, stderr.getvalue()

    def test_allowlist_mappings_counts_and_byte_order_are_exact(self):
        self.assertEqual(set(ew.ALLOWLISTS), set(EXPECTED_ALLOWLISTS))
        for patch_id, expected in EXPECTED_ALLOWLISTS.items():
            output = self.root / f"{patch_id}.txt"
            rc, error = self.invoke(["allowlist", "--patch-id", patch_id, "--output", str(output)])
            self.assertEqual((rc, error), (0, ""))
            self.assertEqual(output.read_text(), expected)
            lines = expected.splitlines()
            self.assertEqual(lines, sorted(set(lines), key=os.fsencode))
            self.assertEqual(len(lines), int(patch_id == "0020") * 24 or int(patch_id == "0021") * 18 or int(patch_id == "0022") * 22 or int(patch_id == "0023") * 9 or int(patch_id == "0024") * 21 or int(patch_id == "0025") * 6 or int(patch_id == "0026") * 7 or 20)

    def test_allowlist_write_is_atomic_on_invalid_id(self):
        output = self.root / "allowlist"
        output.write_text("old")
        rc, _ = self.invoke(["allowlist", "--patch-id", "0019", "--output", str(output)])
        self.assertNotEqual(rc, 0)
        self.assertEqual(output.read_text(), "old")

    def test_allowlist_reader_rejects_duplicate_unsorted_traversal_and_engine_prefix(self):
        for text in ("a\na\n", "b\na\n", "../a\n", "/a\n", "engine/a\n", "a//b\n", "a\\b\n"):
            path = self.root / "allowlist"
            path.write_text(text)
            with self.subTest(text=text), self.assertRaises(ew.WorkspaceError):
                ew.read_allowlist(path)

    def test_safe_zip_rejects_traversal_absolute_duplicate_symlink_and_special(self):
        regular = stat.S_IFREG << 16
        cases = [
            [("../escape", b"x", regular)],
            [("/absolute", b"x", regular)],
            [("a", b"1", regular), ("a", b"2", regular)],
            [("link", b"target", stat.S_IFLNK << 16)],
            [("device", b"", stat.S_IFCHR << 16)],
        ]
        for index, entries in enumerate(cases):
            archive = self.make_zip(entries)
            with self.subTest(index=index), self.assertRaises(ew.WorkspaceError):
                ew.safe_extract_official_zip(archive, self.root / f"out-{index}")

    def test_safe_zip_requires_one_expected_root_and_preserves_regular_bytes(self):
        regular = stat.S_IFREG << 16
        archive = self.make_zip([("a.txt", b"base", regular)])
        output = self.root / "out"
        extracted = ew.safe_extract_official_zip(archive, output)
        self.assertEqual(extracted.name, "OpenRA-release-20250330")
        self.assertEqual((extracted / "a.txt").read_bytes(), b"base")

        wrong = self.make_zip([("a", b"x", regular)], root="Wrong-root")
        with self.assertRaises(ew.WorkspaceError):
            ew.safe_extract_official_zip(wrong, self.root / "wrong")

    def test_safe_zip_preserves_only_safe_executable_modes_and_rejects_set_id(self):
        archive = self.make_zip([
            ("readonly/", b"", (stat.S_IFDIR | 0o555) << 16),
            ("readonly/tool.sh", b"#!/bin/sh\n", (stat.S_IFREG | 0o755) << 16),
            ("data.txt", b"data", (stat.S_IFREG | 0o666) << 16),
            ("default.txt", b"default", 0),
        ])
        extracted = ew.safe_extract_official_zip(archive, self.root / "modes")
        self.assertEqual(stat.S_IMODE((extracted / "readonly" / "tool.sh").stat().st_mode), 0o755)
        self.assertEqual(stat.S_IMODE((extracted / "data.txt").stat().st_mode), 0o644)
        self.assertEqual(stat.S_IMODE((extracted / "default.txt").stat().st_mode), 0o644)
        self.assertEqual(stat.S_IMODE((extracted / "readonly").stat().st_mode), 0o755)

        for bits in (stat.S_ISUID, stat.S_ISGID, stat.S_ISVTX):
            rejected = self.make_zip([("unsafe", b"x", (stat.S_IFREG | 0o644 | bits) << 16)])
            with self.subTest(bits=bits), self.assertRaises(ew.WorkspaceError):
                ew.safe_extract_official_zip(rejected, self.root / f"unsafe-{bits}")

    def test_pinned_zip_hash_and_extraction_use_one_no_follow_descriptor(self):
        original = self.make_zip([("original.txt", b"original", (stat.S_IFREG | 0o644) << 16)])
        original_digest = hashlib.sha256(original.read_bytes()).hexdigest()
        replacement = self.root / "replacement.zip"
        with zipfile.ZipFile(replacement, "w") as archive:
            info = zipfile.ZipInfo(f"{ew.OFFICIAL_ROOT}/replacement.txt")
            info.external_attr = (stat.S_IFREG | 0o644) << 16
            archive.writestr(info, b"replacement")
        repository = self.init_fixture_repository()
        real_sha256 = hashlib.sha256
        replaced = False

        class ReplacingDigest:
            def __init__(self):
                self.inner = real_sha256()

            def update(self, data):
                self.inner.update(data)

            def hexdigest(self):
                nonlocal replaced
                if not replaced:
                    os.replace(replacement, original)
                    replaced = True
                return self.inner.hexdigest()

        destination = self.root / "race"
        with mock.patch.object(ew, "OFFICIAL_ZIP_SHA256", original_digest), \
                mock.patch.object(ew.hashlib, "sha256", side_effect=ReplacingDigest):
            extracted = ew._replay(repository, original, 0, destination)

        self.assertTrue(replaced)
        self.assertEqual((extracted / "original.txt").read_bytes(), b"original")
        self.assertFalse((extracted / "replacement.txt").exists())

    def test_pinned_zip_extraction_uses_private_hashed_snapshot_when_inode_changes(self):
        original = self.make_zip([("original.txt", b"ORIGINAL", (stat.S_IFREG | 0o644) << 16)])
        replacement = self.root / "replacement-inode.zip"
        with zipfile.ZipFile(replacement, "w") as archive:
            info = zipfile.ZipInfo(f"{ew.OFFICIAL_ROOT}/replaced.txt")
            info.external_attr = (stat.S_IFREG | 0o644) << 16
            archive.writestr(info, b"REPLACED")
        original_bytes = original.read_bytes()
        replacement_bytes = replacement.read_bytes()
        self.assertEqual(len(replacement_bytes), len(original_bytes))
        digest = hashlib.sha256(original_bytes).hexdigest()
        repository = self.init_fixture_repository()
        original_stat = original.stat()
        real_sha256 = hashlib.sha256
        replaced = False

        class ReplacingDigest:
            def __init__(self):
                self.inner = real_sha256()

            def update(self, data):
                self.inner.update(data)

            def hexdigest(self):
                nonlocal replaced
                if not replaced:
                    with original.open("r+b") as stream:
                        stream.write(replacement_bytes)
                        stream.flush()
                        os.fsync(stream.fileno())
                    os.utime(original, ns=(original_stat.st_atime_ns, original_stat.st_mtime_ns))
                    replaced = True
                return self.inner.hexdigest()

        with mock.patch.object(ew, "OFFICIAL_ZIP_SHA256", digest), \
                mock.patch.object(ew.hashlib, "sha256", side_effect=ReplacingDigest):
            extracted = ew._replay(repository, original, 0, self.root / "inode-race")

        self.assertTrue(replaced)
        self.assertEqual((extracted / "original.txt").read_bytes(), b"ORIGINAL")
        self.assertFalse((extracted / "replaced.txt").exists())

    def test_wrong_official_zip_hash_fails_without_partial_workspace(self):
        archive = self.make_zip([("a", b"x", stat.S_IFREG << 16)])
        workspace = self.root / "workspace"
        rc, _ = self.invoke(["materialize", "--repository", str(self.root), "--official-zip", str(archive), "--patch-end", "0", "--workspace", str(workspace)])
        self.assertNotEqual(rc, 0)
        self.assertFalse(workspace.exists())

    def test_predecessor_range_rejects_missing_and_duplicate_numeric_prefix(self):
        patches = self.root / "engine-patches"
        patches.mkdir()
        (patches / "0001-a.patch").write_text("")
        with self.assertRaises(ew.WorkspaceError):
            ew.predecessor_patches(self.root, 2)
        (patches / "0002-a.patch").write_text("")
        (patches / "0002-b.patch").write_text("")
        with self.assertRaises(ew.WorkspaceError):
            ew.predecessor_patches(self.root, 2)

    def test_patch_diagnostics_allow_only_exact_historical_0007_tuple(self):
        expected = "Hunk #1 succeeded at 254 with fuzz 2 (offset -1 lines).\nHunk #5 succeeded at 352 (offset -1 lines)."
        ew.validate_patch_diagnostics("0007-language-restart-and-system-default.patch", expected)
        for name, text in (("0007-language-restart-and-system-default.patch", expected + "\nHunk #6 succeeded at 1 (offset 1 line)."), ("0008-x.patch", "Hunk #1 succeeded at 2 (offset 1 line).")):
            with self.subTest(name=name), self.assertRaises(ew.WorkspaceError):
                ew.validate_patch_diagnostics(name, text)

    def init_fixture_repository(self):
        repository = self.root / "repo"
        (repository / "engine-patches").mkdir(parents=True)
        (repository / "engine").mkdir()
        subprocess.run(["git", "init", "-q", str(repository)], check=True)
        subprocess.run(["git", "-C", str(repository), "config", "user.email", "test@example.invalid"], check=True)
        subprocess.run(["git", "-C", str(repository), "config", "user.name", "Test"], check=True)
        return repository

    def candidate_fixture(self):
        regular = stat.S_IFREG << 16
        archive = self.make_zip([("edit.txt", b"old\n", regular)])
        digest = hashlib.sha256(archive.read_bytes()).hexdigest()
        repository = self.init_fixture_repository()
        workspace = repository / "engine"
        (workspace / "edit.txt").write_bytes(b"new\n")
        subprocess.run(["git", "-C", str(repository), "add", "-f", "engine/edit.txt"], check=True)
        subprocess.run(["git", "-C", str(repository), "commit", "-qm", "fixture"], check=True)
        allowlist = self.root / "allowlist"
        allowlist.write_text("edit.txt\n")
        return repository, archive, digest, workspace, allowlist

    @staticmethod
    def tree_manifest(root):
        return {
            path.relative_to(root).as_posix(): path.read_bytes()
            for path in root.rglob("*")
            if path.is_file() and not path.is_symlink()
        }

    def assert_no_publish_remnants(self, parent):
        remnants = [
            path.name for path in parent.iterdir()
            if ".materialized-" in path.name or ".previous-" in path.name
        ]
        self.assertEqual(remnants, [])

    def test_materialize_replays_complete_tree_and_preserves_only_allowlisted_overlay_bytes(self):
        regular = stat.S_IFREG << 16
        archive = self.make_zip([("keep.txt", b"base-keep", regular), ("edit.txt", b"base-edit", regular)])
        digest = hashlib.sha256(archive.read_bytes()).hexdigest()
        repository = self.init_fixture_repository()
        for name in ("keep.txt", "edit.txt"):
            (repository / "engine" / name).write_bytes((b"base-keep" if name == "keep.txt" else b"overlay\x00bytes"))
        subprocess.run(["git", "-C", str(repository), "add", "-f", "engine/keep.txt", "engine/edit.txt"], check=True)
        subprocess.run(["git", "-C", str(repository), "commit", "-qm", "fixture"], check=True)
        allowlist = self.root / "allowlist"
        allowlist.write_text("edit.txt\n")
        workspace = repository / "engine"
        with mock.patch.object(ew, "OFFICIAL_ZIP_SHA256", digest):
            rc, error = self.invoke(["materialize", "--repository", str(repository), "--official-zip", str(archive), "--patch-end", "0", "--workspace", str(workspace), "--overlay-allowlist", str(allowlist)])
        self.assertEqual((rc, error), (0, ""))
        self.assertEqual((workspace / "keep.txt").read_bytes(), b"base-keep")
        self.assertEqual((workspace / "edit.txt").read_bytes(), b"overlay\x00bytes")

    def test_materialize_rejects_tracked_outside_allowlist_drift_without_mutating_workspace(self):
        regular = stat.S_IFREG << 16
        archive = self.make_zip([("keep.txt", b"base", regular), ("edit.txt", b"base", regular)])
        digest = hashlib.sha256(archive.read_bytes()).hexdigest()
        repository = self.init_fixture_repository()
        (repository / "engine" / "keep.txt").write_bytes(b"drift")
        (repository / "engine" / "edit.txt").write_bytes(b"overlay")
        subprocess.run(["git", "-C", str(repository), "add", "-f", "engine/keep.txt", "engine/edit.txt"], check=True)
        subprocess.run(["git", "-C", str(repository), "commit", "-qm", "fixture"], check=True)
        allowlist = self.root / "allowlist"; allowlist.write_text("edit.txt\n")
        original = self.tree_manifest(repository / "engine")
        with mock.patch.object(ew, "OFFICIAL_ZIP_SHA256", digest):
            rc, error = self.invoke(["materialize", "--repository", str(repository), "--official-zip", str(archive), "--patch-end", "0", "--workspace", str(repository / "engine"), "--overlay-allowlist", str(allowlist)])
        self.assertNotEqual(rc, 0)
        self.assertIn("tracked engine drift outside overlay allowlist", error)
        self.assertEqual(self.tree_manifest(repository / "engine"), original)

    def test_materialize_rejects_untracked_pollution_without_mutating_workspace(self):
        regular = stat.S_IFREG << 16
        archive = self.make_zip([("keep.txt", b"base", regular), ("edit.txt", b"base", regular)])
        digest = hashlib.sha256(archive.read_bytes()).hexdigest()
        repository = self.init_fixture_repository()
        (repository / "engine" / "keep.txt").write_bytes(b"base")
        (repository / "engine" / "edit.txt").write_bytes(b"overlay")
        ignored = repository / "engine" / "ignored.bin"
        with ignored.open("wb") as stream:
            stream.seek((50 << 20) + 1)
            stream.write(b"pollution")
        subprocess.run(["git", "-C", str(repository), "add", "-f", "engine/keep.txt", "engine/edit.txt"], check=True)
        subprocess.run(["git", "-C", str(repository), "commit", "-qm", "fixture"], check=True)
        allowlist = self.root / "allowlist"; allowlist.write_text("edit.txt\n")
        original = (
            (repository / "engine" / "keep.txt").read_bytes(),
            (repository / "engine" / "edit.txt").read_bytes(),
            ignored.stat().st_size,
        )
        with mock.patch.object(ew, "OFFICIAL_ZIP_SHA256", digest):
            rc, error = self.invoke(["materialize", "--repository", str(repository), "--official-zip", str(archive), "--patch-end", "0", "--workspace", str(repository / "engine"), "--overlay-allowlist", str(allowlist)])
        self.assertNotEqual(rc, 0)
        self.assertIn("untracked or ignored workspace pollution", error)
        self.assertEqual((
            (repository / "engine" / "keep.txt").read_bytes(),
            (repository / "engine" / "edit.txt").read_bytes(),
            ignored.stat().st_size,
        ), original)

    def test_pollution_enumeration_uses_metadata_without_reading_sparse_contents(self):
        workspace = self.root / "metadata-only"
        workspace.mkdir()
        sparse = workspace / "ignored.bin"
        with sparse.open("wb") as stream:
            stream.seek((50 << 20) + 1)
            stream.write(b"x")

        with mock.patch.object(ew, "_regular_bytes", side_effect=AssertionError("content read")):
            self.assertEqual(ew._all_relative_files(workspace), ("ignored.bin",))

    def test_regular_bytes_rejects_symlink_swap_and_enforces_file_bound(self):
        source = self.root / "source"
        victim = self.root / "victim"
        source.write_bytes(b"source")
        victim.write_bytes(b"victim")
        real_open = os.open
        swapped = False

        def swap_before_open(path, flags, *args, **kwargs):
            nonlocal swapped
            if Path(path).name == source.name and not swapped:
                source.unlink()
                source.symlink_to(victim)
                swapped = True
            return real_open(path, flags, *args, **kwargs)

        with mock.patch.object(ew.os, "open", side_effect=swap_before_open):
            with self.assertRaises(ew.WorkspaceError):
                ew._regular_bytes(source)
        self.assertTrue(swapped)

        bounded = self.root / "bounded"
        exact = self.root / "exact"
        bounded.write_bytes(b"12345")
        exact.write_bytes(b"1234")
        with mock.patch.object(ew, "OVERLAY_FILE_BYTES", 4, create=True):
            self.assertEqual(ew._regular_bytes(exact), b"1234")
            with self.assertRaises(ew.WorkspaceError):
                ew._regular_bytes(bounded)

    def test_regular_descriptor_walk_pins_parent_before_final_open(self):
        parent = self.root / "stable-parent"
        victim = self.root / "victim-parent"
        moved = self.root / "moved-parent"
        parent.mkdir()
        victim.mkdir()
        source = parent / "data.bin"
        source.write_bytes(b"ORIGINAL")
        (victim / "data.bin").write_bytes(b"SECRET")
        real_open = os.open
        swapped = False
        before = len(os.listdir("/dev/fd"))

        def swap_parent_before_final(path, flags, *args, **kwargs):
            nonlocal swapped
            if Path(path).name == "data.bin" and not swapped:
                parent.rename(moved)
                parent.symlink_to(victim, target_is_directory=True)
                swapped = True
            return real_open(path, flags, *args, **kwargs)

        descriptor = None
        with mock.patch.object(ew.os, "open", side_effect=swap_parent_before_final):
            descriptor, _ = ew._open_regular_descriptor(source, "workspace path")
            try:
                self.assertEqual(os.read(descriptor, 64), b"ORIGINAL")
            finally:
                os.close(descriptor)

        self.assertTrue(swapped)
        self.assertEqual(len(os.listdir("/dev/fd")), before)

    def test_workspace_root_descriptor_pins_enumeration_and_overlay_identity(self):
        workspace = self.root / "workspace-root"
        moved = self.root / "workspace-root-moved"
        victim = self.root / "workspace-root-victim"
        workspace.mkdir()
        victim.mkdir()
        (workspace / "edit.txt").write_bytes(b"OVERLAY")
        (victim / "edit.txt").write_bytes(b"SECRET")
        before = len(os.listdir("/dev/fd"))
        descriptor = ew._open_directory_descriptor(workspace, "workspace")
        try:
            self.assertEqual(ew._all_relative_files_at(descriptor), ("edit.txt",))
            workspace.rename(moved)
            victim.rename(workspace)
            self.assertEqual(ew._regular_bytes_at(descriptor, "edit.txt"), b"OVERLAY")
        finally:
            os.close(descriptor)

        self.assertEqual(len(os.listdir("/dev/fd")), before)

    def test_descriptor_walk_never_recloses_an_intermediate_after_close_error(self):
        directory = self.root / "close-once"
        (directory / "parent").mkdir(parents=True)
        (directory / "parent" / "data").write_bytes(b"data")
        root_descriptor = ew._open_directory_descriptor(directory, "workspace")
        real_dup = os.dup
        real_open = os.open
        real_close = os.close
        duplicated = None
        opened = []
        closed = []

        def record_dup(descriptor):
            nonlocal duplicated
            duplicated = real_dup(descriptor)
            return duplicated

        def record_open(*args, **kwargs):
            descriptor = real_open(*args, **kwargs)
            opened.append(descriptor)
            return descriptor

        def fail_first_close(descriptor):
            closed.append(descriptor)
            if descriptor == duplicated and closed.count(descriptor) == 1:
                raise OSError("injected close failure")
            return real_close(descriptor)

        try:
            with mock.patch.object(ew.os, "dup", side_effect=record_dup), \
                    mock.patch.object(ew.os, "open", side_effect=record_open), \
                    mock.patch.object(ew.os, "close", side_effect=fail_first_close):
                with self.assertRaises(OSError):
                    ew._open_relative_descriptor(root_descriptor, "parent/data")
            self.assertEqual(closed.count(duplicated), 1)
        finally:
            for descriptor in opened:
                try:
                    real_close(descriptor)
                except OSError:
                    pass
            if duplicated is not None:
                try:
                    real_close(duplicated)
                except OSError:
                    pass
            real_close(root_descriptor)

    def test_materialize_overlay_aggregate_accepts_exact_and_rejects_limit_plus_one(self):
        regular = (stat.S_IFREG | 0o644) << 16
        archive = self.make_zip([("a.txt", b"aa", regular), ("b.txt", b"bbb", regular)])
        digest = hashlib.sha256(archive.read_bytes()).hexdigest()
        repository = self.init_fixture_repository()
        workspace = repository / "engine"
        (workspace / "a.txt").write_bytes(b"AA")
        (workspace / "b.txt").write_bytes(b"BBB")
        subprocess.run(["git", "-C", str(repository), "add", "-f", "engine/a.txt", "engine/b.txt"], check=True)
        subprocess.run(["git", "-C", str(repository), "commit", "-qm", "fixture"], check=True)
        allowlist = self.root / "aggregate.allowlist"
        allowlist.write_text("a.txt\nb.txt\n")

        with mock.patch.object(ew, "OFFICIAL_ZIP_SHA256", digest), \
                mock.patch.object(ew, "OVERLAY_FILE_BYTES", 5, create=True), \
                mock.patch.object(ew, "OVERLAY_AGGREGATE_BYTES", 5, create=True):
            rc, error = self.invoke(["materialize", "--repository", str(repository),
                "--official-zip", str(archive), "--patch-end", "0", "--workspace", str(workspace),
                "--overlay-allowlist", str(allowlist)])
            self.assertEqual((rc, error), (0, ""))

            (workspace / "b.txt").write_bytes(b"BBBB")
            original = self.tree_manifest(workspace)
            rc, error = self.invoke(["materialize", "--repository", str(repository),
                "--official-zip", str(archive), "--patch-end", "0", "--workspace", str(workspace),
                "--overlay-allowlist", str(allowlist)])
            self.assertNotEqual(rc, 0)
            self.assertIn("aggregate", error)
            self.assertEqual(self.tree_manifest(workspace), original)

    def test_candidate_supports_create_modify_delete_and_exact_paired_headers(self):
        regular = stat.S_IFREG << 16
        archive = self.make_zip([("modify.txt", b"old\n", regular), ("delete.txt", b"gone\n", regular)])
        digest = hashlib.sha256(archive.read_bytes()).hexdigest()
        repository = self.init_fixture_repository()
        workspace = repository / "engine"
        (workspace / "modify.txt").write_bytes(b"new\n")
        (workspace / "create.txt").write_bytes(b"made\n")
        subprocess.run(["git", "-C", str(repository), "add", "-f", "engine/modify.txt", "engine/create.txt"], check=True)
        subprocess.run(["git", "-C", str(repository), "commit", "-qm", "fixture"], check=True)
        allowlist = self.root / "allowlist"; allowlist.write_text("create.txt\ndelete.txt\nmodify.txt\n")
        output = self.root / "candidate.patch"
        with mock.patch.object(ew, "OFFICIAL_ZIP_SHA256", digest):
            rc, error = self.invoke(["candidate", "--repository", str(repository), "--official-zip", str(archive), "--patch-end", "0", "--workspace", str(workspace), "--allowlist", str(allowlist), "--output", str(output)])
        self.assertEqual((rc, error), (0, ""))
        lines = output.read_text().splitlines()
        diff_lines = [line for line in lines if line.startswith("diff -ruN ")]
        old_headers = [line.split("\t", 1)[0] for line in lines if line.startswith("--- ")]
        new_headers = [line.split("\t", 1)[0] for line in lines if line.startswith("+++ ")]
        self.assertEqual(diff_lines, [
            "diff -ruN a/create.txt b/create.txt",
            "diff -ruN a/delete.txt b/delete.txt",
            "diff -ruN a/modify.txt b/modify.txt",
        ])
        self.assertEqual(old_headers, ["--- a/create.txt", "--- a/delete.txt", "--- a/modify.txt"])
        self.assertEqual(new_headers, ["+++ b/create.txt", "+++ b/delete.txt", "+++ b/modify.txt"])

    def test_candidate_patch_parser_requires_exact_delimited_unique_header_records(self):
        valid = (
            b"diff -ruN a/foo b/foo\n"
            b"--- a/foo\t2026-01-01 00:00:00\n"
            b"+++ b/foo\t2026-01-01 00:00:01\n"
            b"@@ -1 +1 @@\n-old\n+new\n"
        )
        ew._validate_candidate_patch(valid, ("foo",))
        invalid = (
            valid.replace(b"--- a/foo\t", b"--- a/foobar\t"),
            valid.replace(b"--- a/foo\t", b"--- a/foo "),
            b"--- a/foo\tstray\n" + valid,
            valid + b"+++ b/foo\tduplicate\n",
            valid.replace(b"+++ b/foo\t2026-01-01 00:00:01\n", b""),
            valid.replace(b"diff -ruN a/foo b/foo", b"diff -ruN a/foo b/./foo"),
        )
        for candidate in invalid:
            with self.subTest(candidate=candidate), self.assertRaises(ew.WorkspaceError):
                ew._validate_candidate_patch(candidate, ("foo",))

    def test_candidate_rejects_live_and_broken_symlink_arguments_without_following(self):
        repository, archive, digest, workspace, allowlist = self.candidate_fixture()
        victim = self.root / "victim.patch"
        victim.write_text("victim")
        live = {
            "repository": (self.root / "repository-link", repository),
            "official-zip": (self.root / "archive-link", archive),
            "workspace": (self.root / "workspace-link", workspace),
            "allowlist": (self.root / "allowlist-link", allowlist),
            "output": (self.root / "output-link", victim),
        }
        for _, (link, target) in live.items():
            link.symlink_to(target, target_is_directory=target.is_dir())

        base = {
            "repository": repository,
            "official-zip": archive,
            "workspace": workspace,
            "allowlist": allowlist,
            "output": self.root / "candidate.patch",
        }
        with mock.patch.object(ew, "OFFICIAL_ZIP_SHA256", digest):
            for argument, (link, _) in live.items():
                values = dict(base)
                values[argument] = link
                rc, _ = self.invoke([
                    "candidate", "--repository", str(values["repository"]),
                    "--official-zip", str(values["official-zip"]), "--patch-end", "0",
                    "--workspace", str(values["workspace"]), "--allowlist", str(values["allowlist"]),
                    "--output", str(values["output"]),
                ])
                self.assertNotEqual(rc, 0, argument)

            broken = self.root / "broken-workspace"
            broken.symlink_to(self.root / "missing-target", target_is_directory=True)
            rc, _ = self.invoke([
                "candidate", "--repository", str(repository), "--official-zip", str(archive),
                "--patch-end", "0", "--workspace", str(broken), "--allowlist", str(allowlist),
                "--output", str(self.root / "broken.patch"),
            ])
            self.assertNotEqual(rc, 0)
        self.assertEqual(victim.read_text(), "victim")

    def test_materialize_rejects_symlink_arguments_and_broken_output_parent(self):
        repository, archive, digest, workspace, allowlist = self.candidate_fixture()
        repository_link = self.root / "materialize-repository-link"
        repository_link.symlink_to(repository, target_is_directory=True)
        workspace_link = self.root / "materialize-workspace-link"
        workspace_link.symlink_to(workspace, target_is_directory=True)
        archive_link = self.root / "materialize-archive-link"
        archive_link.symlink_to(archive)
        allowlist_link = self.root / "materialize-allowlist-link"
        allowlist_link.symlink_to(allowlist)
        cases = [
            (repository_link, archive, workspace, allowlist),
            (repository, archive_link, workspace, allowlist),
            (repository, archive, workspace_link, allowlist),
            (repository, archive, workspace, allowlist_link),
        ]
        with mock.patch.object(ew, "OFFICIAL_ZIP_SHA256", digest):
            for case in cases:
                rc, _ = self.invoke([
                    "materialize", "--repository", str(case[0]), "--official-zip", str(case[1]),
                    "--patch-end", "0", "--workspace", str(case[2]),
                    "--overlay-allowlist", str(case[3]),
                ])
                self.assertNotEqual(rc, 0)

            broken_parent = self.root / "broken-parent"
            broken_parent.symlink_to(self.root / "missing-parent", target_is_directory=True)
            rc, _ = self.invoke([
                "materialize", "--repository", str(repository), "--official-zip", str(archive),
                "--patch-end", "0", "--workspace", str(broken_parent / "engine"),
            ])
            self.assertNotEqual(rc, 0)

    def test_replace_workspace_publish_failure_restores_old_tree_and_removes_remnants(self):
        parent = self.root / "publish"
        workspace = parent / "engine"
        staged = parent / ".engine.materialized-123"
        workspace.mkdir(parents=True)
        staged.mkdir()
        (workspace / "old").write_bytes(b"old")
        (staged / "new").write_bytes(b"new")
        old_manifest = self.tree_manifest(workspace)
        real_replace = os.replace

        def fail_publish(source, destination):
            if Path(source) == staged and Path(destination) == workspace:
                raise OSError("injected publish failure")
            return real_replace(source, destination)

        with mock.patch.object(ew.os, "replace", side_effect=fail_publish):
            with self.assertRaises(OSError):
                ew._replace_workspace(staged, workspace)

        self.assertEqual(self.tree_manifest(workspace), old_manifest)
        self.assert_no_publish_remnants(parent)

    def test_replace_workspace_initial_backup_failure_preserves_old_tree_and_removes_staged(self):
        parent = self.root / "backup-failure"
        workspace = parent / "engine"
        staged = parent / ".engine.materialized-123"
        workspace.mkdir(parents=True)
        staged.mkdir()
        (workspace / "old").write_bytes(b"old")
        (staged / "new").write_bytes(b"new")
        old_manifest = self.tree_manifest(workspace)
        real_replace = os.replace

        def fail_backup(source, destination):
            if Path(source) == workspace:
                raise OSError("injected backup failure")
            return real_replace(source, destination)

        with mock.patch.object(ew.os, "replace", side_effect=fail_backup):
            with self.assertRaises(OSError):
                ew._replace_workspace(staged, workspace)

        self.assertEqual(self.tree_manifest(workspace), old_manifest)
        self.assert_no_publish_remnants(parent)

    def test_replace_workspace_partial_backup_cleanup_keeps_complete_published_tree(self):
        parent = self.root / "cleanup"
        workspace = parent / "engine"
        staged = parent / ".engine.materialized-123"
        workspace.mkdir(parents=True)
        staged.mkdir()
        (workspace / "old-a").write_bytes(b"old-a")
        (workspace / "old-b").write_bytes(b"old-b")
        (staged / "new-a").write_bytes(b"new-a")
        (staged / "new-b").write_bytes(b"new-b")
        new_manifest = self.tree_manifest(staged)
        backup = parent / f".engine.previous-{os.getpid()}"
        real_rmtree = ew.shutil.rmtree
        calls = 0

        def fail_first_cleanup(path, *args, **kwargs):
            nonlocal calls
            calls += 1
            if calls == 1:
                (backup / "old-a").unlink()
                raise OSError("injected cleanup failure")
            return real_rmtree(path, *args, **kwargs)

        with mock.patch.object(ew.shutil, "rmtree", side_effect=fail_first_cleanup):
            with self.assertRaises(ew.WorkspaceError):
                ew._replace_workspace(staged, workspace)

        self.assertEqual(self.tree_manifest(workspace), new_manifest)
        self.assertEqual(self.tree_manifest(backup), {"old-b": b"old-b"})

    def test_replace_workspace_cleanup_failure_never_vacates_published_tree(self):
        parent = self.root / "vacate-failure"
        workspace = parent / "engine"
        staged = parent / ".engine.materialized-123"
        backup = parent / f".engine.previous-{os.getpid()}"
        workspace.mkdir(parents=True)
        staged.mkdir()
        (workspace / "old").write_bytes(b"old")
        (staged / "new").write_bytes(b"new")
        old_manifest = self.tree_manifest(workspace)
        new_manifest = self.tree_manifest(staged)
        real_replace = os.replace
        real_rmtree = ew.shutil.rmtree
        vacate_attempted = False

        def fail_vacate(source, destination):
            nonlocal vacate_attempted
            if Path(source) == workspace and Path(destination) == staged:
                vacate_attempted = True
                raise OSError("injected vacate failure")
            return real_replace(source, destination)

        def fail_backup_cleanup(path, *args, **kwargs):
            if Path(path) == backup:
                raise OSError("injected backup cleanup failure")
            return real_rmtree(path, *args, **kwargs)

        with mock.patch.object(ew.os, "replace", side_effect=fail_vacate), \
                mock.patch.object(ew.shutil, "rmtree", side_effect=fail_backup_cleanup):
            with self.assertRaises(ew.WorkspaceError):
                ew._replace_workspace(staged, workspace)

        self.assertEqual(self.tree_manifest(workspace), new_manifest)
        self.assertEqual(self.tree_manifest(backup), old_manifest)
        self.assertFalse(vacate_attempted)

    def test_replace_workspace_cleanup_failure_never_attempts_backup_restore(self):
        parent = self.root / "restore-failure"
        workspace = parent / "engine"
        staged = parent / ".engine.materialized-123"
        backup = parent / f".engine.previous-{os.getpid()}"
        workspace.mkdir(parents=True)
        staged.mkdir()
        (workspace / "old").write_bytes(b"old")
        (staged / "new").write_bytes(b"new")
        old_manifest = self.tree_manifest(workspace)
        new_manifest = self.tree_manifest(staged)
        real_replace = os.replace
        real_rmtree = ew.shutil.rmtree
        restore_attempted = False

        def fail_restore(source, destination):
            nonlocal restore_attempted
            if Path(source) == backup and Path(destination) == workspace:
                restore_attempted = True
                raise OSError("injected restore failure")
            return real_replace(source, destination)

        def fail_backup_cleanup(path, *args, **kwargs):
            if Path(path) == backup:
                raise OSError("injected backup cleanup failure")
            return real_rmtree(path, *args, **kwargs)

        with mock.patch.object(ew.os, "replace", side_effect=fail_restore), \
                mock.patch.object(ew.shutil, "rmtree", side_effect=fail_backup_cleanup):
            with self.assertRaises(ew.WorkspaceError):
                ew._replace_workspace(staged, workspace)

        self.assertEqual(self.tree_manifest(workspace), new_manifest)
        self.assertEqual(self.tree_manifest(backup), old_manifest)
        self.assertFalse(staged.exists())
        self.assertFalse(restore_attempted)

    def test_replace_workspace_publish_failure_and_cleanup_failure_restores_old_and_keeps_new(self):
        parent = self.root / "publish-cleanup-failure"
        workspace = parent / "engine"
        staged = parent / ".engine.materialized-123"
        backup = parent / f".engine.previous-{os.getpid()}"
        workspace.mkdir(parents=True)
        staged.mkdir()
        (workspace / "old").write_bytes(b"old")
        (staged / "new").write_bytes(b"new")
        old_manifest = self.tree_manifest(workspace)
        new_manifest = self.tree_manifest(staged)
        real_replace = os.replace
        real_rmtree = ew.shutil.rmtree

        def fail_publish(source, destination):
            if Path(source) == staged and Path(destination) == workspace:
                raise OSError("injected publish failure")
            return real_replace(source, destination)

        def fail_staged_cleanup(path, *args, **kwargs):
            if Path(path) == staged:
                raise OSError("injected staged cleanup failure")
            return real_rmtree(path, *args, **kwargs)

        with mock.patch.object(ew.os, "replace", side_effect=fail_publish), \
                mock.patch.object(ew.shutil, "rmtree", side_effect=fail_staged_cleanup):
            with self.assertRaises(ew.WorkspaceError):
                ew._replace_workspace(staged, workspace)

        self.assertEqual(self.tree_manifest(workspace), old_manifest)
        self.assertFalse(backup.exists())
        self.assertEqual(self.tree_manifest(staged), new_manifest)

    def test_replace_workspace_backup_cleanup_failure_keeps_new_and_recoverable_old(self):
        parent = self.root / "double-cleanup-failure"
        workspace = parent / "engine"
        staged = parent / ".engine.materialized-123"
        backup = parent / f".engine.previous-{os.getpid()}"
        workspace.mkdir(parents=True)
        staged.mkdir()
        (workspace / "old").write_bytes(b"old")
        (staged / "new").write_bytes(b"new")
        old_manifest = self.tree_manifest(workspace)
        new_manifest = self.tree_manifest(staged)
        real_rmtree = ew.shutil.rmtree

        def fail_backup_and_staged_cleanup(path, *args, **kwargs):
            if Path(path) in (backup, staged):
                raise OSError("injected cleanup failure")
            return real_rmtree(path, *args, **kwargs)

        with mock.patch.object(ew.shutil, "rmtree", side_effect=fail_backup_and_staged_cleanup):
            with self.assertRaises(ew.WorkspaceError):
                ew._replace_workspace(staged, workspace)

        self.assertEqual(self.tree_manifest(workspace), new_manifest)
        self.assertEqual(self.tree_manifest(backup), old_manifest)
        self.assertFalse(staged.exists())

    def test_candidate_failure_does_not_replace_existing_output(self):
        output = self.root / "candidate.patch"; output.write_text("old")
        archive = self.make_zip([("a", b"x", stat.S_IFREG << 16)])
        allowlist = self.root / "allowlist"; allowlist.write_text("missing\n")
        rc, _ = self.invoke(["candidate", "--repository", str(self.root), "--official-zip", str(archive), "--patch-end", "0", "--workspace", str(self.root), "--allowlist", str(allowlist), "--output", str(output)])
        self.assertNotEqual(rc, 0)
        self.assertEqual(output.read_text(), "old")


if __name__ == "__main__":
    unittest.main()
