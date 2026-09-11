# macOS Content Source Discovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add explicit manual directory selection and bounded automatic discovery to the in-game NUKE HOUR macOS content manager.

**Architecture:** A focused `MacOSDirectorySourceResolver` owns case-insensitive source validation and breadth-first discovery. The existing installer panel selects manual or automatic mode and then reuses the current package-selection, copy, rollback, and completion flow.

**Tech Stack:** C#/.NET 8, OpenRA Chrome YAML, Fluent localization, NUnit, Python PublicClean packaging tests.

---

## File structure

- `engine/OpenRA.Mods.Common/Installer/SourceResolvers/MacOSDirectorySourceResolver.cs`: bounded discovery and the native macOS directory picker.
- `engine/OpenRA.Mods.Common/ModContent.cs`: declarative required-filename field.
- `engine/OpenRA.Mods.Common/Widgets/Logic/Installation/InstallFromSourceLogic.cs`: manual/automatic routing.
- `engine/OpenRA.Mods.Common/Widgets/Logic/Installation/ModContentLogic.cs`: separate visible actions.
- `engine/mods/common-content/content.yaml`: manual directory button.
- `mods/ra2-content/installer/macos-directory.yaml`: normal RA2 directory package actions.
- `mods/ra2-content/mod.yaml`: source registration.
- `mods/ra2-content/fluent/{chrome.ftl,zh-CN/chrome.ftl}`: localized copy.
- `engine/OpenRA.Test/MacOSDirectorySourceResolverTest.cs`: resolver tests.
- `engine/OpenRA.Test/Ra2ContentInstallerManifestTest.cs`: UI and source contracts.
- `packaging/tests/test_macos_public_clean_builder.py`: release inclusion contract.

### Task 1: Directory discovery core

**Files:**
- Create: `engine/OpenRA.Test/MacOSDirectorySourceResolverTest.cs`
- Create: `engine/OpenRA.Mods.Common/Installer/SourceResolvers/MacOSDirectorySourceResolver.cs`
- Modify: `engine/OpenRA.Mods.Common/ModContent.cs`

- [ ] **Step 1: Write failing resolver tests**

Cover a source with `RequiredFiles: ra2.mix, language.mix` and temporary directory trees. Assert root and nested case-insensitive matches, no result beyond maximum depth, no traversal through symlink directories, and stable de-duplication of candidate roots.

- [ ] **Step 2: Run the focused tests and verify RED**

```bash
dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter MacOSDirectorySourceResolverTest --logger 'console;verbosity=minimal'
```

Expected: compilation fails because `MacOSDirectorySourceResolver` and `ModSource.RequiredFiles` do not exist.

- [ ] **Step 3: Implement the resolver**

Add `public readonly string[] RequiredFiles = Array.Empty<string>();` to `ModContent.ModSource`. Implement `FindSourcePath(ModSource, IEnumerable<string>, int maximumDepth = 6, int maximumDirectories = 20000)` using breadth-first traversal, case-insensitive filename resolution, canonical paths, a visited set, reparse-point rejection, and exception handling for inaccessible directories. `FindSourcePath(ModSource)` supplies bounded macOS roots; `GetAvailability()` is `DigitalInstall` only on macOS. `ChooseDirectory(string prompt)` invokes `/usr/bin/osascript`, passes the prompt as argv, and returns null on cancel or failure.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the Task 1 command. Expected: all resolver tests pass.

- [ ] **Step 5: Commit**

```bash
git add engine/OpenRA.Mods.Common/ModContent.cs engine/OpenRA.Mods.Common/Installer/SourceResolvers/MacOSDirectorySourceResolver.cs engine/OpenRA.Test/MacOSDirectorySourceResolverTest.cs
git commit -m 'feat: discover macOS retail content directories'
```

### Task 2: Register the RA2 macOS directory source

**Files:**
- Modify: `engine/OpenRA.Test/Ra2ContentInstallerManifestTest.cs`
- Create: `mods/ra2-content/installer/macos-directory.yaml`
- Modify: `mods/ra2-content/mod.yaml`

- [ ] **Step 1: Write a failing manifest test**

Assert that `mod.yaml` registers `ra2content|installer/macos-directory.yaml`; the source uses `Type: MacOSDirectory`, requires `ra2.mix` and `language.mix`, and defines base, music, Yuri, and Yuri music actions without URLs.

- [ ] **Step 2: Run the manifest test and verify RED**

```bash
dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter Ra2ContentInstallerManifestTest --logger 'console;verbosity=minimal'
```

Expected: the new source assertions fail.

- [ ] **Step 3: Add the declarative source**

Create `macos-directory.yaml` with `RequiredFiles: ra2.mix, language.mix` and the package-specific copy destinations used by `origin.yaml`. Register it first in `ModContent.Sources`.

- [ ] **Step 4: Run the manifest test and verify GREEN**

Run the Task 2 command. Expected: all manifest tests pass.

- [ ] **Step 5: Commit**

```bash
git add engine/OpenRA.Test/Ra2ContentInstallerManifestTest.cs mods/ra2-content/installer/macos-directory.yaml mods/ra2-content/mod.yaml
git commit -m 'feat: define macOS RA2 directory source'
```

### Task 3: Expose manual and automatic actions

**Files:**
- Modify: `engine/OpenRA.Test/Ra2ContentInstallerManifestTest.cs`
- Modify: `engine/mods/common-content/content.yaml`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Installation/ModContentLogic.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Installation/InstallFromSourceLogic.cs`
- Modify: `mods/ra2-content/fluent/chrome.ftl`
- Modify: `mods/ra2-content/fluent/zh-CN/chrome.ftl`

- [ ] **Step 1: Write failing UI contract tests**

Assert the layout contains `CHOOSE_SOURCE_BUTTON` and `CHECK_SOURCE_BUTTON`; logic supplies `chooseDirectory` for both paths; and Fluent includes English and Chinese strings for choosing a directory, automatic search, the picker prompt, and invalid-directory feedback.

- [ ] **Step 2: Run the UI tests and verify RED**

Run the Task 2 command. Expected: the manual button and localization keys are missing.

- [ ] **Step 3: Implement the UI extension**

On macOS, show `选择安装目录…`, `自动搜索`, and `返回` without changing the panel background or package list. Manual mode calls `ChooseDirectory`, resolves the selected tree against `MacOSDirectory` sources, and enters the existing package-selection flow. Automatic mode runs the existing source loop. Cancellation closes only the source panel; invalid selections show a localized retry message.

- [ ] **Step 4: Build and run focused tests**

```bash
dotnet build engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj -c Release --no-restore --nologo
dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter 'MacOSDirectorySourceResolverTest|Ra2ContentInstallerManifestTest' --logger 'console;verbosity=minimal'
```

Expected: build succeeds and all focused tests pass.

- [ ] **Step 5: Commit**

```bash
git add engine/mods/common-content/content.yaml engine/OpenRA.Mods.Common/Widgets/Logic/Installation/ModContentLogic.cs engine/OpenRA.Mods.Common/Widgets/Logic/Installation/InstallFromSourceLogic.cs engine/OpenRA.Test/Ra2ContentInstallerManifestTest.cs mods/ra2-content/fluent/chrome.ftl mods/ra2-content/fluent/zh-CN/chrome.ftl
git commit -m 'feat: choose or search macOS content sources'
```

### Task 4: PublicClean release inclusion

**Files:**
- Modify: `packaging/tests/test_macos_public_clean_builder.py`
- Modify: `packaging/macos/public_clean_manifest.json` (generated)

- [ ] **Step 1: Write a failing PublicClean contract test**

Require `mods/ra2-content/installer/macos-directory.yaml` in the reviewed manifest while retaining the prohibited retail-extension assertions.

- [ ] **Step 2: Run the packaging test and verify RED**

```bash
python3 -m unittest packaging.tests.test_macos_public_clean_builder -v
```

Expected: the source is absent from the current manifest.

- [ ] **Step 3: Regenerate manifest and rebuild runtime assemblies**

```bash
python3 packaging/macos/generate_public_clean_manifest.py
dotnet build OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore --nologo
```

- [ ] **Step 4: Run packaging tests and build signed DMG**

```bash
python3 -m unittest packaging.tests.test_macos_public_clean_builder -v
packaging/macos/build-public-clean.sh
```

Expected: tests pass and the builder produces `NUKE-HOUR-PublicClean-1.0.3-arm64.dmg`.

- [ ] **Step 5: Commit**

```bash
git add packaging/tests/test_macos_public_clean_builder.py packaging/macos/public_clean_manifest.json
git commit -m 'build: ship macOS content discovery in PublicClean'
```

### Task 5: Final verification

**Files:**
- Verify: `artifacts/public-macos/NUKE-HOUR-PublicClean-1.0.3-arm64.dmg`

- [ ] **Step 1: Run the complete packaging suite**

```bash
python3 -m unittest discover -s packaging/tests -p 'test_*.py' -q
```

Expected: zero failures.

- [ ] **Step 2: Audit and verify the artifact**

```bash
python3 packaging/macos/public_clean_audit.py --dmg artifacts/public-macos/NUKE-HOUR-PublicClean-1.0.3-arm64.dmg --manifest packaging/macos/public_clean_manifest.json --version 1.0.3
shasum -a 256 -c artifacts/public-macos/NUKE-HOUR-PublicClean-1.0.3-arm64.dmg.sha256
```

Expected: audit PASS and checksum OK.

- [ ] **Step 3: Runtime smoke test**

Mount the DMG, launch with an empty temporary support directory and Simplified Chinese, verify the footer shows the two new actions, then exercise automatic detection against a controlled directory containing case-varied `RA2.MIX` and `Language.MIX`. Confirm arm64 binaries and `codesign --verify --deep --strict`.

- [ ] **Step 4: Record final state**

Report the DMG path, SHA-256, test count, audit result, runtime result, commit hashes, and unchanged notarization status.
