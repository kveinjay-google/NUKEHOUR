# Build 4 Recovery Changes So Far

## Emergency isolation result

- Audit time: 2026-09-08, America/Los_Angeles
- Current-development checkout: `/Users/kevin/Projects/ra2-mac`
- Current-development branch: `codex/multiplayer-phase2`
- Current-development HEAD: `0e36f8f2168c02f63e083b784b7fcb5c24c3cb85`
- Emergency backup: `/Users/kevin/Projects/ra2-mac-emergency-backup-20260908-055653`
- Verified Build 4 base: `6c17968ad62338ff0df1d1922af7d3a582f0a0db`
- Pre-isolation Build 4 worktree: `/Users/kevin/Projects/ra2-build4-source-release`
- New isolated Build 4 worktree: `/Users/kevin/Projects/ra2-build4-source-release-20260908-055841`
- New isolated branch: `archive/ios-build-4-recovery`

`/Users/kevin/Projects/ra2-mac` is read-only for Build 4 recovery from this point onward.

## Finding

The Build 4 recovery task transcript records no file-content write to `/Users/kevin/Projects/ra2-mac`. All reconstructed source writes were made in `/private/tmp/build4-candidate.hmQDL0` and then copied into `/Users/kevin/Projects/ra2-build4-source-release`.

The Build 4 task did change shared Git administrative state by running `git worktree add` and creating branch `archive/ios-build-4`. This created a linked worktree and branch reference but did not alter a tracked or untracked working-tree file in `/Users/kevin/Projects/ra2-mac`.

`POSSIBLE_WORKTREE_COLLISION: NO`

No current-development data loss was detected. The initial Build 4 task status snapshot and the emergency investigation show that the current checkout remained on `codex/multiplayer-phase2` at the same HEAD. The Build 4 command transcript contains no copy, patch, delete, checkout, restore, reset, clean, stash, build, or generator command targeting the current-development working tree.

## Files written by the Build 4 recovery task

The pre-isolation Build 4 worktree has 82 content-level changes relative to `6c17968ad62338ff0df1d1922af7d3a582f0a0db`: 48 modified tracked files, 18 staged deletions, and 16 untracked files.

### Reconstructed historical application, engine, configuration, and resource files

- `OpenRA.Mods.RA2/Content/IosRuntimeResourceRoot.cs`
- `OpenRA.Mods.RA2/Content/RetailArchiveGraphValidator.cs`
- `OpenRA.Mods.RA2/Content/RetailContentCatalog.cs`
- `OpenRA.Mods.RA2/Content/RetailContentImporter.cs`
- `OpenRA.Mods.RA2/Content/RetailImportDurableFileSystem.cs`
- `OpenRA.Mods.RA2/Content/RetailImportModels.cs`
- `OpenRA.Mods.RA2/Content/RetailImportPaths.cs`
- `OpenRA.Mods.RA2/Content/Maps/Ra2MapConversionService.cs` (untracked)
- `OpenRA.Mods.RA2/Content/Maps/RetailCompatibleMapSourceScanner.cs` (untracked)
- `OpenRA.Mods.RA2/Content/RetailGenerationStore.cs` (untracked)
- `OpenRA.Mods.RA2/Content/RetailGenerationTransaction.cs` (untracked)
- `OpenRA.Mods.RA2/Content/RetailImportDurableGenerationTypes.cs` (untracked)
- `OpenRA.Mods.RA2/Content/RetailImportFaultInjection.cs` (untracked)
- `OpenRA.Mods.RA2/Content/RetailImportProcessLock.cs` (untracked)
- `OpenRA.Mods.RA2/Content/RetailImportRecovery.cs` (untracked)
- `OpenRA.Mods.RA2/UtilityCommands/ImportRA2MapCommand.cs`
- `engine/OpenRA.Game/FileSystem/PackageEnvelopePolicy.cs`
- `engine/OpenRA.Game/Game.cs`
- `engine/OpenRA.Game/Map/MapPreview.cs`
- `engine/OpenRA.Mods.Common/TraitsInterfaces.cs`
- `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyLayout.cs`
- `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs`
- `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyUtils.cs`
- `engine/OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs`
- `engine/OpenRA.Mods.Common/Widgets/Logic/Settings/AdvancedSettingsLogic.cs`
- `engine/OpenRA.Mods.Common/Widgets/Logic/Settings/DisplaySettingsLogic.cs`
- `engine/mods/common/chrome/color-picker.yaml`
- `engine/mods/common/chrome/settings-advanced.yaml`
- `ios/OpenRA.iOS/AppDelegate.cs`
- `ios/OpenRA.iOS/Info.plist`
- `ios/OpenRA.iOS/IosContentFileSystemLoader.cs`
- `ios/OpenRA.iOS/IosContentImportCoordinator.cs`
- `ios/OpenRA.iOS/IosEmbeddedMapBootstrap.cs` (untracked)
- `ios/OpenRA.iOS/IosRetailContentPicker.cs`
- `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- `ios/OpenRA.iOS/ViewController.cs`
- `mods/ra2/audio/notifications.yaml`
- `mods/ra2/manifest/filesystem-desktop.yaml`
- `mods/ra2/manifest/filesystem-ios-base.yaml`
- `mods/ra2/manifest/filesystem-ios-yuri.yaml`
- `mods/ra2/maps/blank-shellmap/map.yaml`
- `mods/ra2/mod.yaml`
- `mods/ra2/uibits/dialog.png`
- `mods/ra2/uibits/ios-joystick-command-icons.png`
- `mods/ra2/uibits/menubuttons.png`

### Reconstructed historical tests and audit tooling

- `engine/OpenRA.Test/IosContentImportContractTest.cs`
- `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs`
- `engine/OpenRA.Test/IosTouchDialogLayoutTest.cs`
- `engine/OpenRA.Test/IosViewportActionLayoutTest.cs`
- `engine/OpenRA.Test/OpenRA.Game/PngTest.cs`
- `engine/OpenRA.Test/Ra2RuntimeProfileManifestTest.cs`
- `engine/OpenRA.Test/RetailArchiveGraphValidatorTest.cs`
- `engine/OpenRA.Test/RetailContentImporterTest.cs`
- `engine/OpenRA.Test/SyntheticRetailFixtureBuilder.cs`
- `packaging/audit_ios_touch_runtime_bundle.py`
- `packaging/tests/test_ios_touch_runtime_bundle.py`
- `build/OpenRA.RetailImport.CrashHarness/OpenRA.RetailImport.CrashHarness.csproj` (untracked)
- `build/OpenRA.RetailImport.CrashHarness/Program.cs` (untracked)

### Generated/recovered source and temporary generated outputs

- `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs` was replaced with the recovered Build 4 generated AOT registry.
- `/private/tmp/GeneratedAotObjectRegistry.candidate.g.cs` was generated for comparison.
- `/private/tmp/build4-types-exact.txt` and `/private/tmp/build4-types-candidate.txt` were generated for registry comparison.
- `/private/tmp/build4-descriptors.tsv`, `/private/tmp/build4-pdb-*`, `/private/tmp/build4-embedded/`, and `/private/tmp/build4-candidate.hmQDL0/` were created or populated as recovery/audit intermediates.
- A complete iOS build ran in `/private/tmp/build4-candidate.hmQDL0`, producing isolated `bin/` and `obj/` outputs there. Those outputs were excluded when the candidate tree was copied to the pre-isolation Build 4 worktree.

### Documentation and licensing files

No archival `README.md`, `BUILDING.md`, `NOTICE.md`, or publication report had been created before emergency isolation.

The task added these untracked third-party license documents to the pre-isolation Build 4 worktree:

- `third_party/licenses/FreeType-2.13.3-FTL.txt`
- `third_party/licenses/FreeType-2.13.3-LICENSE.txt`
- `third_party/licenses/Mono.Nat-3.0.4-LICENSE.md`
- `third_party/licenses/NVorbis-0.10.5-LICENSE.txt`
- `third_party/licenses/SDL2-2.30.10-LICENSE.txt`

This report, `BUILD4_RECOVERY_CHANGES_SO_FAR.md`, is the first file written in the new isolated recovery worktree.

### Staged deletions in the pre-isolation Build 4 worktree

The task staged deletion of 18 historical reference-asset files after classifying them as commercial-game-derived reference material. They remain recoverable from Git and were not deleted from the current-development checkout:

- `artsrc/vxl-reference/1tnk.vxl`
- `artsrc/vxl-reference/2tnk.vxl`
- `artsrc/vxl-reference/3tnk.vxl`
- `artsrc/vxl-reference/3tnktur.vxl`
- `artsrc/vxl-reference/4tnk.hva`
- `artsrc/vxl-reference/4tnk.vxl`
- `artsrc/vxl-reference/4tnkbarl.vxl`
- `artsrc/vxl-reference/4tnktur.hva`
- `artsrc/vxl-reference/4tnktur.vxl`
- `artsrc/vxl-reference/htnk.vxl`
- `artsrc/vxl-reference/htnkbarl.vxl`
- `artsrc/vxl-reference/htnktur.vxl`
- `artsrc/vxl-reference/hwtz.vxl`
- `artsrc/vxl-reference/rtnk.vxl`
- `artsrc/vxl-reference/sref.vxl`
- `artsrc/vxl-reference/sreftur.vxl`
- `artsrc/vxl-reference/ttnk.vxl`
- `artsrc/vxl-reference/ttnktur.vxl`

## Files and data only read

The task transcript permits an exact content-write inventory, but it does not contain a syscall-level inventory of every file opened by recursive commands such as `find`, `rg`, `git`, `zipinfo`, `shasum`, and `dotnet`. The explicit read-only paths and recursive scopes were:

- Git identity, status, refs, logs, reflogs, tags, remotes, tracked content, and worktree metadata under `/Users/kevin/Projects/ra2-mac`.
- `/Users/kevin/Projects/ra2-mac/README.md`, `LICENSE`, `VERSION`, `engine/VERSION`, and the Build 4 base versions of source/project files accessed through `git show`.
- Candidate IPA `/Users/kevin/Desktop/NUKE-HOUR-Build-4-signed.ipa`, including `Info.plist`, app file listing, managed assemblies, embedded PDB data, executable metadata, and hashes.
- IPA candidates below `/Users/kevin/Projects/ra2-mac`, `/Users/kevin/.config/superpowers/worktrees/ra2-mac`, `/Users/kevin/Desktop`, and simulator/Xcode locations returned by the search.
- Xcode archive `/Users/kevin/Library/Developer/Xcode/Archives/2026-09-06/OpenRA.iOS 9-06-26 5.55 AM.xcarchive`.
- Current-development sibling worktree `/Users/kevin/.config/superpowers/worktrees/ra2-mac/ios-retail-import-hardening`, including source, PDBs, build artifacts, tests, packaging tools, and license metadata.
- Historical clone directories under `/Users/kevin/Projects/nuke-hour-*`.
- Recovery candidate `/private/tmp/build4-candidate.hmQDL0`, extracted app `/private/tmp/nukehour-build4.hCwkPM`, PDB listings/embedded source under `/private/tmp/build4-pdb-*` and `/private/tmp/build4-embedded`, and temporary audit files under `/private/tmp`.
- Historical Codex session logs under `/Users/kevin/.codex/sessions` and `/Users/kevin/.codex/archived_sessions`.
- NuGet package metadata and license files under `/Users/kevin/.nuget/packages`.
- Public upstream Git metadata/content for OpenRA, OpenRA RA2, SDL 2.30.10, FreeType 2.13.3, and selected managed dependencies.

## Commands that may have changed repository or filesystem state

### Shared Git repository administrative state

- `git worktree add /Users/kevin/Projects/ra2-build4-source-release 6c17968ad62338ff0df1d1922af7d3a582f0a0db`
- `git -C /Users/kevin/Projects/ra2-build4-source-release switch -c archive/ios-build-4`

These changed branch/worktree metadata in the shared `.git` directory. They did not modify `/Users/kevin/Projects/ra2-mac` working-tree files.

### Pre-isolation Build 4 worktree writes

- `rsync -a --delete-excluded ... /private/tmp/build4-candidate.hmQDL0/ /Users/kevin/Projects/ra2-build4-source-release/`
- `cp` commands that copied historical tests, audit tools, crash-harness files, and license texts into the Build 4 worktree.
- Four patch operations against `engine/OpenRA.Test/IosContentImportContractTest.cs`.
- `git rm -- artsrc/vxl-reference/...` for the 18 staged deletions listed above.

### Temporary/external writes

- `mktemp`, `unzip`, `ditto`, `curl --output`, `git clone`, `cp`, `mv`, `mkdir`, `tee`, redirections, and generated audit lists under `/tmp` and `/private/tmp`.
- `dotnet build -c Release /tmp/build4-pdb-audit/build4-pdb-audit.csproj`; outputs remained under `/tmp/build4-pdb-audit`.
- AOT generator execution after changing directory to `/private/tmp/build4-candidate.hmQDL0/ios/OpenRA.iOS`; output remained under `/private/tmp` and the candidate tree.
- Full iOS build in `/private/tmp/build4-candidate.hmQDL0`; build output remained in that isolated temporary tree.

## Collision and build assessment

- File modified by current development overwritten by Build 4 task: **NO, according to the complete Build 4 task command transcript.**
- `POSSIBLE_WORKTREE_COLLISION`: **NO** for working-tree content.
- Shared Git administrative metadata changed: **YES**, limited to the linked worktree and `archive/ios-build-4` branch creation.
- Source-generating build run inside `/Users/kevin/Projects/ra2-mac`: **NO**.
- A `dotnet build` command was launched while the shell working directory was `/Users/kevin/Projects/ra2-mac`, but its explicit project and all outputs were under `/tmp/build4-pdb-audit`; it did not build or generate source in the current-development checkout.
- Full Build 4 iOS build: **YES**, only in `/private/tmp/build4-candidate.hmQDL0`.
- Automatic repair attempted in current-development checkout: **NO**.
- Commit, reset, clean, restore, stash, rebase, cherry-pick, or patch application performed during emergency isolation: **NO**.

## Emergency backup evidence

The backup contains:

- `git-status.txt`
- `branch.txt`
- `head.txt`
- `working-tree.patch`
- `index.patch` (empty because the index had no staged changes at capture time)
- `untracked-files.txt`
- `working-tree/` full filesystem copy excluding `.git`

Evidence SHA-256 values:

```text
0767b71d1cb3d4a2cb9a0bd8c0c18b88fdcd4819ab062627cec0f502d784b2c2  branch.txt
7babfb5c1038936ab176e08db5d1ee5f1576c27a0ef2ff4a24723692a9ad453c  head.txt
26397d67c93aefe3500b7d9ba0074460aac0f86763251780aabd4bafb7aacedb  git-status.txt
7d52728165f1cfe8c0975e30c830df3b89a97913e0b9c4128dbece8d4cc9cfed  working-tree.patch
e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855  index.patch
b0fffb1793983a101181389ca3a321de0e863588a0a3c23125442b736a8a663b  untracked-files.txt
```

At verification time, the live `git status --short` still matched `git-status.txt` byte-for-byte.

## Resume gate

- Current development may resume in `/Users/kevin/Projects/ra2-mac`.
- Build 4 recovery may resume only in `/Users/kevin/Projects/ra2-build4-source-release-20260908-055841`.
- Do not resume Build 4 work in the dirty pre-isolation worktree `/Users/kevin/Projects/ra2-build4-source-release` until its 82 content changes are deliberately reviewed and migrated.
- No historical reconstruction, migration, build, commit, tag, push, cleanup, or deletion was performed after the emergency isolation request.
