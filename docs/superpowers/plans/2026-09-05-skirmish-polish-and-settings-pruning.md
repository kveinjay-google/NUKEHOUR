# Skirmish Polish and Settings Pruning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a compact iPad skirmish lobby with a nine-swatch colour chooser, a larger map preview, and a player-facing Advanced settings panel without the four requested modules.

**Architecture:** `AdvancedSettingsLogic` will bind only controls present in the iOS Advanced YAML. `IosLobbyLayout` will select a compact skirmish column contract and a larger map allocation, while `LobbyUtils` renders an iOS-specific fixed nine-colour grid using preset colours. Existing multiplayer paths and settings reset behavior stay intact.

**Tech Stack:** C#, OpenRA widgets/YAML, NUnit, Python iOS runtime bundle audit, .NET iOS arm64 Release build.

---

### Task 1: Remove non-player-facing Advanced settings controls

**Files:**
- Modify: `engine/mods/common/chrome/settings-advanced.yaml`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Settings/AdvancedSettingsLogic.cs`
- Test: `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs`

- [ ] **Step 1: Write failing source-contract test**

Add a test that reads the Advanced YAML and logic source and asserts the four identifiers `PERFGRAPH_CHECKBOX`, `FETCH_NEWS_CHECKBOX`, `CHECK_VERSION_CHECKBOX`, and developer containers are absent from the panel/binding path.

- [ ] **Step 2: Run the test and observe its expected failure**

Run: `/Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~IosMenuLayoutPolicyTest.AdvancedSettingsExcludeNonPlayerFacingControls -v:minimal`

Expected: FAIL because the existing YAML contains the controls.

- [ ] **Step 3: Remove the rows and direct bindings**

Delete the community-news, performance-graph, update-check, debug-header, debug-hidden, and developer rows from the YAML. Remove their direct `BindCheckboxPref` and visibility lookups from `AdvancedSettingsLogic`; leave reset behavior and the remaining advanced items intact.

- [ ] **Step 4: Run the focused test**

Run the Task 1 command. Expected: 1 passing test.

### Task 2: Add the fixed nine-swatch skirmish colour popup

**Files:**
- Modify: `engine/mods/common/chrome/color-picker.yaml`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyUtils.cs`
- Test: `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs`

- [ ] **Step 1: Write failing source-contract test**

Assert the skirmish colour setup uses `PresetColors.Take(9)`, opens `IOS_SKIRMISH_COLOR_GRID`, and does not create a label from `color.ToString()`.

- [ ] **Step 2: Run the test and observe its expected failure**

Run: `/Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~IosMenuLayoutPolicyTest.SkirmishColorPickerUsesNineSwatchesWithoutCodes -v:minimal`

Expected: FAIL because the existing item template exposes `color.ToString()`.

- [ ] **Step 3: Implement the popup and selection state**

Add `IOS_SKIRMISH_COLOR_GRID` and a touch-safe colour-swatch template to the colour-picker YAML. In `SetupEditableClassicColorWidget`, populate exactly the first nine preset colours, set each swatch colour, issue the existing `color` order on tap, and show a selected-state indicator without text.

- [ ] **Step 4: Run the focused test**

Run the Task 2 command. Expected: 1 passing test.

### Task 3: Reallocate skirmish width to map preview

**Files:**
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyLayout.cs`
- Modify: `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs`
- Modify: `packaging/audit_ios_touch_runtime_bundle.py`

- [ ] **Step 1: Write failing layout test**

Create a skirmish layout test that asserts its map width exceeds multiplayer map width, its colour/faction/team controls are compact but at least one target wide, and its active columns remain contiguous.

- [ ] **Step 2: Run the test and observe its expected failure**

Run: `/Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~IosMenuLayoutPolicyTest.SkirmishLayoutPrioritizesMapPreviewAndCompactControls -v:minimal`

Expected: FAIL because both modes currently use the same map allocation.

- [ ] **Step 3: Implement mode-specific map and column allocation**

In skirmish mode, compute a five-control minimum width, cap players at that compact width plus a small readability reserve, and give all remaining permitted horizontal space to `Map`. Keep the existing seven-column multiplayer allocation unchanged. Update the bundle audit to verify both contracts.

- [ ] **Step 4: Run the focused test and bundle audit tests**

Run the Task 3 test, then `python3 -m unittest packaging.tests.test_ios_touch_runtime_bundle -v`. Expected: all selected tests pass.

### Task 4: Build, install, and launch

**Files:**
- Verify: `ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app`

- [ ] **Step 1: Run focused regression suite**

Run: `/Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~IosMenuLayoutPolicyTest -v:minimal`

- [ ] **Step 2: Build signed Release app**

Run: `/Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj --configuration Release --framework net8.0-ios --runtime ios-arm64 --no-restore -v:minimal`

- [ ] **Step 3: Verify identity and install**

Run `codesign --verify --deep --strict --verbose=2` on the app, install using `xcrun devicectl device install app --device C5CA8947-3880-593B-84D6-B57DE642A8FD`, then launch `com.openra.ipad.personal` with `xcrun devicectl`.
