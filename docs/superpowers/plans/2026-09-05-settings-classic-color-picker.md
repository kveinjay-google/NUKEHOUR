# Settings Classic Color Picker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the settings Preferred Color control use only a nine-colour classic palette.

**Architecture:** Add a dedicated preset-grid attachment method to `ColorPickerManager`. Route only `DisplaySettingsLogic` through it and reuse the existing skirmish classic-colour grid, leaving the generic colour chooser and its other callers intact.

**Tech Stack:** C#, OpenRA MiniYaml widgets, NUnit, .NET iOS Release build.

---

### Task 1: Lock the settings-specific palette contract

**Files:**
- Modify: `engine/OpenRA.Test/IosTouchDialogLayoutTest.cs`
- Modify: `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
Assert.That(displaySettingsSource, Does.Contain("ShowClassicColorDropDown"));
Assert.That(displaySettingsSource, Does.Not.Contain("ShowColorDropDown(colorDropdown"));
Assert.That(classicGrid, Does.Contain("Background@IOS_SKIRMISH_COLOR_GRID:"));
Assert.That(classicGrid, Does.Not.Contain("ColorMixer@MIXER"));
```

- [ ] **Step 2: Run the focused tests to verify failure**

Run: `/Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~IosTouchDialogLayoutTest -v:minimal`

Expected: a settings picker contract assertion fails because the focused path does not exist.

### Task 2: Add the focused classic-colour picker

**Files:**
- Modify: `engine/OpenRA.Mods.Common/Traits/World/ColorPickerManager.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Settings/DisplaySettingsLogic.cs`
- Modify: `engine/mods/common/chrome/color-picker.yaml`

- [ ] **Step 1: Add `ShowClassicColorDropDown`**

```csharp
void ShowClassicColorDropDown(DropDownButtonWidget dropdownButton, Color initialColor, Action<Color> onExit)
{
    // Load IOS_PREFERRED_COLOR_GRID, bind PresetColors.Take(9), issue onExit, and close the panel.
}
```

- [ ] **Step 2: Route only `PLAYERCOLOR` in `DisplaySettingsLogic` through the new method**

```csharp
colorDropdown.OnMouseDown = _ => colorManager.ShowClassicColorDropDown(colorDropdown, ps.Color, color =>
{
    ps.Color = color;
    Game.Settings.Save();
});
```

- [ ] **Step 3: Bind the existing `IOS_SKIRMISH_COLOR_GRID`**

Use its existing top-level 3×3 `ColorBlock` grid with `SWATCH_0` through `SWATCH_8` and its selected-state image per swatch. Do not load the generic chooser, mixer, palette tabs, random, store, preview, or text labels.

- [ ] **Step 4: Run focused tests to verify success**

Run: `/Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~IosTouchDialogLayoutTest -v:minimal`

Expected: all selected tests pass.

### Task 3: Build and device verification

**Files:**
- Verify: `ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app`

- [ ] **Step 1: Build Release iOS application**

Run: `/Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj --configuration Release --framework net8.0-ios --runtime ios-arm64 --no-restore -v:minimal`

Expected: `Build succeeded` and iOS touch runtime bundle audit passes.

- [ ] **Step 2: Verify, install, and launch**

Run: `codesign --verify --deep --strict ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app`

Run: `xcrun devicectl device install app --device C5CA8947-3880-593B-84D6-B57DE642A8FD --timeout 600 ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app`

Run: `xcrun devicectl device process launch --device C5CA8947-3880-593B-84D6-B57DE642A8FD --terminate-existing com.openra.ipad.personal`

Expected: valid signature, successful installation, and a running application process.
