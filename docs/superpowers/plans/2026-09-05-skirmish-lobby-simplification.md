# Skirmish Lobby Simplification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make iOS skirmish player setup use a compact, readable layout without handicap, ready, or custom color controls.

**Architecture:** Extend the responsive lobby layout with a skirmish-only column profile. Keep network lobbies unchanged. Route skirmish color clicks to a small dropdown populated only from `IColorPickerManagerInfo.PresetColors`; retain the current manager for validation and order dispatch.

**Tech Stack:** C#, NUnit, OpenRA Chrome YAML, .NET iOS AOT build.

---

### Task 1: Specify the skirmish layout contract

**Files:**
- Modify: `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyLayout.cs`

- [ ] **Step 1: Write the failing test**

Add `SkirmishLobbyAllocatesNoHandicapOrReadyColumn` that constructs normal and skirmish layouts at 1560×720 and asserts `skirmish.PlayerHandicap.Width == 0`, `skirmish.PlayerReady.Width == 0`, contiguous remaining columns, a wider name column than color, and unchanged non-skirmish columns.

- [ ] **Step 2: Run the test to verify it fails**

Run: `/Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~IosMenuLayoutPolicyTest.SkirmishLobbyAllocatesNoHandicapOrReadyColumn`

Expected: FAIL because `IosLobbyLayout.Create` has no skirmish layout parameter.

- [ ] **Step 3: Implement the minimal layout variant**

Add a `skirmishMode` parameter to the iOS lobby layout creation path. In the skirmish profile, distribute width only among name, color, faction, team, and spawn; set handicap and ready bounds to zero. Preserve the existing seven-column profile for multiplayer.

- [ ] **Step 4: Run the focused layout test**

Run the command from Step 2.

Expected: PASS.

### Task 2: Remove skirmish-only controls and add classic color dropdown

**Files:**
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyUtils.cs`
- Modify: `engine/mods/common/chrome/lobby-players.yaml`
- Modify: `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs`

- [ ] **Step 1: Write failing source-contract tests**

Add tests that require the skirmish branch to hide `HANDICAP`, `HANDICAP_DROPDOWN`, `STATUS_CHECKBOX`, `STATUS_IMAGE`, `IOS_MAP_STATUS_TEXT`, and `IOS_MAP_STATUS_ICON`; require editable skirmish color setup to call a classic preset dropdown rather than `ShowColorDropDown`; and require the new menu item template to show a color block and label.

- [ ] **Step 2: Run focused tests to verify failure**

Run: `/Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~IosMenuLayoutPolicyTest`

Expected: FAIL on the new assertions.

- [ ] **Step 3: Implement the minimal skirmish branch**

Pass `skirmishMode` into `LayoutIosPlayerRows` and its header/row helpers. Hide the removed labels and widgets only in skirmish mode; skip handicap and ready setup there. Add `ShowClassicColorDropDown`, which builds selectable menu rows from the existing preset colors, renders their color block and readable label, and issues the existing `color {client} {value}` order. Leave `SetupEditableColorWidget` unchanged for multiplayer and settings.

- [ ] **Step 4: Run focused tests**

Run the command from Step 2.

Expected: PASS.

### Task 3: Build and verify on device

**Files:**
- Modify: `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs` (generated)

- [ ] **Step 1: Run relevant tests and whitespace validation**

Run: `/Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~IosMenuLayoutPolicyTest`

Run: `git diff --check`

Expected: focused tests pass and no whitespace errors.

- [ ] **Step 2: Build signed iOS app**

Run: `/Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj --configuration Release --framework net8.0-ios --runtime ios-arm64 --no-restore`

Expected: signed `OpenRA.iOS.app` output and regenerated AOT registry.

- [ ] **Step 3: Install and inspect skirmish mode**

Install the signed app on the registered iPad, launch it, and use the app screenshot request facility to verify the player header has no handicap or ready column and that color selection presents only the preset list.
