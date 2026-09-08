# iOS Fullscreen Touch Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build responsive, full-safe-area, RA2-styled iOS settings and configuration surfaces whose controls remain at least 48 physical points on landscape iPhone and iPad.

**Architecture:** Keep existing settings storage and panel logic, but replace the iOS fixed-canvas scaling layer with a pure layout policy that understands effective size, native point size, and safe-area insets. Add opt-in touch presentation to shared widgets so dynamic sliders, dropdowns, and scroll panels receive the same behavior without changing desktop defaults. Apply explicit phone/tablet shell layouts and focused dialog/configuration adapters only on iOS.

**Tech Stack:** C#/.NET 8, OpenRA widget/chrome YAML, UIKit safe-area reporting, NUnit, iOS arm64 AOT.

---

## File map

- `engine/OpenRA.Game/Support/IosScreenMetrics.cs`: UIKit-independent native/effective/safe-area snapshot.
- `ios/OpenRA.iOS/ViewController.cs`: publishes UIKit safe-area insets whenever the view layout changes.
- `engine/OpenRA.Mods.Common/Widgets/Logic/Settings/IosSettingsLayout.cs`: pure phone/tablet layout and physical touch metrics.
- `engine/OpenRA.Mods.Common/Widgets/Logic/Settings/SettingsLogic.cs`: iOS shell/content layout and platform-only visibility changes.
- `engine/OpenRA.Mods.Common/Widgets/Logic/IosTouchWidgetAdapter.cs`: reusable opt-in widget-tree touch styling for static and dynamic content.
- `engine/OpenRA.Mods.Common/Widgets/SliderWidget.cs`: three-segment stepper presentation.
- `engine/OpenRA.Mods.Common/Widgets/ScrollPanelWidget.cs`: opt-in content pan and wide touch scrollbar.
- `engine/OpenRA.Mods.Common/Widgets/DropDownButtonWidget.cs`: safe-area-clamped, large-item dropdowns on iOS.
- `engine/OpenRA.Mods.Common/Widgets/Logic/IosTouchDialogLogic.cs`: explicit confirmation and color-dialog layout.
- `mods/ra2/chrome/settings.yaml`, `engine/mods/common/chrome/confirmation-dialogs.yaml`, `engine/mods/common/chrome/color-picker.yaml`: enable semantic RA2 settings chrome and dialog adapters.
- `mods/ra2/mod.yaml`: iOS-sized font aliases that reuse the existing RA2 fonts.
- `engine/OpenRA.Test/IosSettingsLayoutTest.cs`, `engine/OpenRA.Test/IosTouchWidgetPolicyTest.cs`: pure layout, interaction, source-contract and desktop-regression tests.

The repository is already heavily modified by the user and other active work. Do not stage or commit this plan automatically; preserve all unrelated changes and report only the files touched by this feature.

### Task 1: Physical screen and safe-area metrics

**Files:**
- Create: `engine/OpenRA.Game/Support/IosScreenMetrics.cs`
- Modify: `ios/OpenRA.iOS/ViewController.cs`
- Test: `engine/OpenRA.Test/IosSettingsLayoutTest.cs`

- [ ] **Step 1: Write the failing metric conversion tests**

Add tests that create a `IosScreenSnapshot` with effective `1560×720`, native `932×430`, and point insets `59,0,59,21`. Assert that `LogicalPerPoint` is approximately `1.674`, and that the logical safe bounds equal the effective viewport minus converted insets. Add an iPad case with `1366×1024` at scale `1`.

- [ ] **Step 2: Run the tests and verify RED**

Run:

```bash
DOTNET_ROLL_FORWARD=Major dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --filter FullyQualifiedName~IosSettingsLayoutTest -m:1
```

Expected: compilation fails because `IosScreenSnapshot` does not exist.

- [ ] **Step 3: Add the UIKit-independent snapshot and publisher**

Implement immutable point insets and a snapshot with:

```csharp
public readonly record struct IosSafeAreaInsets(float Left, float Top, float Right, float Bottom);
public readonly record struct IosScreenSnapshot(Size EffectiveSize, Size NativePointSize, IosSafeAreaInsets SafeArea)
{
    public double LogicalPerPoint => Math.Max(
        EffectiveSize.Width / (double)Math.Max(1, NativePointSize.Width),
        EffectiveSize.Height / (double)Math.Max(1, NativePointSize.Height));
}
```

Store the latest insets in a lock-protected static `IosScreenMetrics`; default to conservative zero insets until UIKit reports them. In `ViewDidLayoutSubviews`, publish `View.Bounds.Size` and `View.SafeAreaInsets` without referencing OpenRA widgets.

- [ ] **Step 4: Re-run the metric tests**

Expected: all new metric tests pass.

### Task 2: Replace the fixed 1180×720 policy

**Files:**
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Settings/IosSettingsLayout.cs`
- Modify: `engine/OpenRA.Test/IosSettingsLayoutTest.cs`

- [ ] **Step 1: Replace old compact tests with real device matrices**

Use these cases:

```csharp
[TestCase(1560, 720, 932, 430, true)]
[TestCase(1558, 720, 844, 390, true)]
[TestCase(1133, 744, 1133, 744, false)]
[TestCase(1180, 820, 1180, 820, false)]
[TestCase(1366, 1024, 1366, 1024, false)]
```

For every case assert: window inside safe bounds; window uses at least 90% of safe width and height; `MinimumTarget / LogicalPerPoint >= 48`; phone has top tabs and no sidebar; tablet has sidebar and vertical tabs; five tabs and footer do not overlap content.

- [ ] **Step 2: Run the matrix and verify RED against the fixed-canvas policy**

Expected: phone physical target, compact classification, and iPad screen-utilization assertions fail.

- [ ] **Step 3: Implement explicit shell regions**

Expose `Window`, `Header`, `Tabs`, `Content`, `Footer`, `Reset`, and `Back` bounds. Calculate all metrics in point units first and convert using `LogicalPerPoint`. Use native landscape height `< 600` for compact phone layout. Phone uses header → horizontal tabs → content → footer; tablet uses header plus sidebar/tabs and right content.

- [ ] **Step 4: Add anchored child-transform tests**

Test a hotkey list with top margin and bottom remap footer. The list must stretch between header and footer, while the footer remains bottom-anchored. Apply the transform twice and assert identical results.

- [ ] **Step 5: Run the layout suite GREEN**

Expected: all device matrix and transform tests pass.

### Task 3: Fullscreen RA2 settings shell and content reflow

**Files:**
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Settings/SettingsLogic.cs`
- Modify: `mods/ra2/chrome/settings.yaml`
- Modify: `mods/ra2/mod.yaml`
- Test: `engine/OpenRA.Test/IosSettingsLayoutTest.cs`

- [ ] **Step 1: Add source-contract tests**

Assert the settings shell uses `settings-panel`, still declares exactly the five panel IDs, and the RA2 manifest provides `IosRegular`, `IosBold`, and `IosTitle` font aliases. Assert desktop source coordinates remain present in YAML.

- [ ] **Step 2: Run and verify RED**

Expected: missing semantic background and iOS font aliases.

- [ ] **Step 3: Implement shell layout**

In `SettingsLogic`, create the layout from renderer effective size, renderer native size, and the current safe-area snapshot. Place title, tabs, panel container, reset, and back from explicit bounds. Set tab buttons to highlighted RA2 state and iOS fonts. Keep desktop construction unchanged.

- [ ] **Step 4: Reflow panel content without blind recursive canvas scaling**

For scroll-row pages, scale each original row to the physical target and let `ListLayout.AdjustChildren()` recompute positions. For hotkeys, explicitly position filter/context header, stretch `HOTKEY_LIST`, bottom-anchor `HOTKEY_REMAP_BGND`, and make the iPhone entry template single-column. Capture templates as well as live children so filtered rows remain adapted.

- [ ] **Step 5: Hide only iOS-invalid controls**

Hide window mode, window resolution, display selector, GL profile, mouse lock, desktop mouse-control scheme, alternate mouse scrolling, and edge-scroll rows on iOS. Preserve all fields and desktop visibility. Rename the input tab to the existing touch translation and keep external-keyboard hotkeys accessible.

- [ ] **Step 6: Run focused settings tests**

Expected: shell/source/layout contracts pass and desktop-policy identity tests remain green.

### Task 4: Slider-free stepper controls

**Files:**
- Modify: `engine/OpenRA.Mods.Common/Widgets/SliderWidget.cs`
- Create: `engine/OpenRA.Test/IosTouchWidgetPolicyTest.cs`

- [ ] **Step 1: Write RED tests for segment hit-testing and formatting**

Test three equal segments: left decrements, center leaves the value unchanged, right increments. Test `0.5` on a `0..1` range formats as `50%`, `120` on an integer range formats as `120`, and fractional non-percent values use at most two decimals.

- [ ] **Step 2: Implement the minimal stepper policy**

Add a pure `TouchStepperPolicy` with `SegmentAt`, `Step`, and `FormatValue`. In touch mode draw three `menu-button` segments, center the text vertically using font measurements, and show the formatted current value in the middle. Do not enter slider-drag mode in touch presentation.

- [ ] **Step 3: Ensure all seven settings sliders opt in**

The settings tree adapter calculates a step from ticks or range and sets `UseTouchStepControls = true`. Assert the display, audio, and input slider IDs are included in an iOS source/adapter contract test.

- [ ] **Step 4: Run the widget tests GREEN**

Expected: segment, clamp, format, and desktop default tests pass.

### Task 5: Direct content drag and touch dropdowns

**Files:**
- Modify: `engine/OpenRA.Mods.Common/Widgets/ScrollPanelWidget.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/DropDownButtonWidget.cs`
- Modify: `engine/mods/common/chrome/dropdowns.yaml`
- Test: `engine/OpenRA.Test/IosTouchWidgetPolicyTest.cs`

- [ ] **Step 1: Add RED tests for pan math and safe popup placement**

Use a pure policy to assert: movement below 8pt remains a tap; movement above it becomes a drag; offsets clamp to `[panelHeight-contentHeight, 0]`. Test dropdown placement near each screen edge and assert the resulting panel is inside safe bounds.

- [ ] **Step 2: Implement opt-in content pan**

Add `EnableContentDragging`, `ContentDragThreshold`, and state fields to `ScrollPanelWidget`. When enabled, dragging the panel background updates list offset one-to-one and yields cleanly on release. Keep wheel, arrow, and thumb behavior unchanged when the option is false.

- [ ] **Step 3: Adapt dynamic dropdowns at creation time**

On iOS, clone dropdown items at `MinimumTarget` height, use iOS fonts, set scrollbars to the touch target, enable content dragging, and clamp the popup to the safe bounds. Do this inside `ShowDropDown`/`AttachPanel` so runtime-created options cannot bypass adaptation.

- [ ] **Step 4: Run widget and dropdown tests GREEN**

Expected: pan policy, placement, dynamic item height, and desktop-default tests pass.

### Task 6: Confirmation, text-input, and color dialogs

**Files:**
- Create: `engine/OpenRA.Mods.Common/Widgets/Logic/IosTouchDialogLogic.cs`
- Modify: `engine/mods/common/chrome/confirmation-dialogs.yaml`
- Modify: `engine/mods/common/chrome/color-picker.yaml`
- Test: `engine/OpenRA.Test/IosTouchWidgetPolicyTest.cs`

- [ ] **Step 1: Add RED dialog-layout tests**

For phone and tablet safe bounds, assert two-button, three-button, text-input, and color layouts stay inside the safe area; buttons are at least 48pt; labels and controls do not intersect.

- [ ] **Step 2: Implement explicit dialog variants**

Use widget IDs to position title, body, fields, and 1–3 actions. Phone actions may use a full-width horizontal row when they fit or a compact vertical stack; tablet uses horizontal actions. The color picker expands preview, tabs, mixer/palette area, and color cells instead of merely increasing child heights.

- [ ] **Step 3: Replace generic expansion on these roots**

Attach `IosTouchDialogLogic` to confirmation and color roots. It must be a no-op outside iOS.

- [ ] **Step 4: Run dialog tests GREEN**

Expected: all dialog geometry and source-contract tests pass.

### Task 7: Reuse touch controls in other configuration surfaces

**Files:**
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/IosTouchMenuLogic.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/IosMenuLayoutPolicy.cs`
- Modify: `engine/mods/common/chrome/lobby-options.yaml`
- Modify: `engine/mods/common/chrome/lobby-players.yaml`
- Modify: `engine/mods/common/chrome/multiplayer-createserver.yaml`
- Test: `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs`

- [ ] **Step 1: Write physical-size and source RED tests**

Construct menu policies with both effective and native sizes and assert 48pt targets on phone/tablet. Assert the actual manifest-loaded common configuration roots invoke the iOS touch adapter.

- [ ] **Step 2: Update the generic iOS policy**

Make `IosMenuLayoutPolicy` accept logical-per-point metrics so it no longer mistakes an iPhone effective canvas for a tablet. Reuse the stepper, dropdown, font, and scroll adapters; do not blindly enlarge children without reflow.

- [ ] **Step 3: Opt in the actual configuration roots**

Attach the adapter to lobby options, lobby players, and create-server roots that `mods/ra2/mod.yaml` really loads. Retain all columns and fields; on compact phones stack configuration rows where needed and keep map/player actions adjacent to their data.

- [ ] **Step 4: Run lobby/menu tests GREEN**

Expected: configuration controls meet physical targets and existing lobby layout tests pass.

### Task 8: Verification and visual QA

**Files:**
- Update only tests or feature files when verification reveals a scoped defect.

- [ ] **Step 1: Run focused tests without stale binaries**

```bash
MSBUILDDISABLENODEREUSE=1 DOTNET_ROLL_FORWARD=Major dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --filter 'FullyQualifiedName~IosSettingsLayoutTest|FullyQualifiedName~IosTouchWidgetPolicyTest|FullyQualifiedName~IosMenuLayoutPolicyTest' -m:1
```

Expected: all selected tests pass.

- [ ] **Step 2: Build the modified desktop assemblies**

```bash
DOTNET_ROLL_FORWARD=Major dotnet build engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj -c Release -m:1
```

Expected: zero errors.

- [ ] **Step 3: Build iOS arm64 Release/AOT**

```bash
dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj -c Release -r ios-arm64 -m:1
```

Expected: zero errors through AOT, linking, and signing.

- [ ] **Step 4: Run static size/source audit**

Verify there are no visible iOS setting sliders, no settings popup outside safe bounds, and every interactive settings/configuration control computes to at least 48pt for all five device cases. Run `git diff --check` and inspect only the feature-related diff.

- [ ] **Step 5: Install and visually inspect phone and tablet builds when devices are available**

Capture all five settings pages plus dropdown, color, and confirmation dialogs on at least one iPhone and one iPad. Verify one-finger scrolling, stepper operation, keyboard entry, rotation lock, touch feedback, and that desktop behavior remains unchanged.
