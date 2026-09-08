# iOS Faction Touch Controls Runtime Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Turn the approved Allied, Soviet, and Yuri touch-control preview into production iOS runtime skins, responsive joystick/action geometry, a three-size touch setting, and Safe-Area-aware support-power placement without changing desktop behavior or command semantics.

**Architecture:** Keep rendering assets, geometry, settings, and support-power placement as separate testable units. Generate deterministic POT atlases from original project-owned design tokens, resolve a touch-only faction suffix without changing the global Yuri fallback, and apply all physical dimensions through IosScreenSnapshot point-to-logical conversion. Existing button IDs, shortcut glyphs, order logic, and desktop YAML remain the single source of behavior.

**Tech Stack:** C#/.NET 8, OpenRA Chrome/MiniYaml, Python 3 + Pillow asset generation, NUnit, iOS Safe Area metrics, MSBuild ios-arm64 bundle audit.

**Repository constraint:** The checkout contains extensive user-owned staged and unstaged work. Do not reset, stash, replace whole files, stage, commit, or push. Use surgical apply_patch edits and report every ignored engine file that must eventually be force-added.

---

## File Structure

- Create packaging/generate_ios_touch_skin_assets.py — deterministic original POT atlas generator.
- Create packaging/tests/test_ios_touch_skin_assets.py — dimensions, pixel-state, region, source/bundle contract.
- Create mods/ra2/uibits/ios-touch-actions-{allies,soviets,yuri}.png and -2x variants.
- Create mods/ra2/uibits/ios-touch-joystick-{allies,soviets,yuri}.png and -2x variants.
- Create mods/ra2/uibits/ios-touch-quickbar-{allies,soviets,yuri}.png and -2x variants.
- Modify mods/ra2/chrome.yaml — append only new touch collections and regions.
- Modify mods/ra2/metrics.yaml — append touch-only faction suffixes; preserve FactionSuffix-yuri: soviets.
- Create engine/OpenRA.Mods.Common/Widgets/TouchFactionSkin.cs — pure faction resolver.
- Create engine/OpenRA.Mods.Common/Widgets/IosViewportControlsLayout.cs — pure responsive geometry.
- Modify engine/OpenRA.Game/Support/IosScreenMetrics.cs — shared point conversion and compact-phone flag.
- Modify engine/OpenRA.Game/Input/VirtualViewportJoystickState.cs — resizable radius with cancellation.
- Modify engine/OpenRA.Mods.Common/Widgets/VirtualViewportJoystickWidget.cs — live layout and scaled sprites.
- Modify engine/OpenRA.Mods.Common/Widgets/ImageWidget.cs — opt-in StretchToFit rendering.
- Modify engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IosViewportActionsLogic.cs — skin selection and layout signature.
- Modify mods/ra2/chrome/ingame.yaml — remove fixed action placement, opt into runtime scaling.
- Modify OpenRA.Mods.RA2/Widgets/CustomCommandBarWidget.cs — iOS-only faction shell/button/toggle skin.
- Modify engine/OpenRA.Game/Settings.cs — persist IosVirtualJoystickSize.
- Modify engine/OpenRA.Mods.Common/Widgets/Logic/Settings/InputSettingsLogic.cs — bind 112/128/144 dropdown.
- Modify engine/OpenRA.Mods.Common/Widgets/Logic/Settings/IosSettingsVisibilityPolicy.cs — iOS-only setting row.
- Modify engine/mods/common/chrome/settings-input.yaml — add the large touch-size selector.
- Modify engine/mods/common/fluent/common.ftl and engine/mods/common/fluent/zh-CN/common.ftl — labels.
- Create engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IosSupportPowerLayoutPolicy.cs — pure phone/iPad/desktop layout.
- Modify engine/OpenRA.Mods.Common/Widgets/SupportPowersWidget.cs — cell-provider rendering and exact hit testing.
- Modify engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/SupportPowerBinLogic.cs — signature-driven dynamic layout.
- Modify engine/OpenRA.Mods.Common/Widgets/TooltipContainerWidget.cs — iOS Safe Area clamping.
- Modify engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/SupportPowerTooltipLogic.cs — safe width/reflow.
- Create engine/OpenRA.Test/IosViewportControlsLayoutTest.cs.
- Create engine/OpenRA.Test/IosSupportPowerLayoutPolicyTest.cs.
- Modify engine/OpenRA.Test/IosViewportActionLayoutTest.cs — remove obsolete fixed-coordinate contract.
- Create packaging/audit_ios_touch_runtime_bundle.py — final app-bundle resource and manifest gate.

### Task 1: Create the Production Atlas Contract and Original POT Assets

**Files:**
- Create: packaging/tests/test_ios_touch_skin_assets.py
- Create: packaging/generate_ios_touch_skin_assets.py
- Create: eighteen PNG files under mods/ra2/uibits/

- [ ] **Step 1: Write the failing atlas contract**

Create a unittest that defines the exact manifest:

    FACTIONS = ("allies", "soviets", "yuri")
    SPECS = {
        "actions": ((512, 512), (1024, 1024)),
        "joystick": ((256, 256), (512, 512)),
        "quickbar": ((512, 128), (1024, 256)),
    }

For every faction/type/scale assert PNG, RGBA, sRGB-compatible decode, exact dimensions, POT edges, nonempty alpha, transparent unused gutters, and SHA difference between factions. Assert actions contain five 56x56 columns at x 0/64/128/192/256 and seven rows at y 0/64/128/192/256/320/384. Compare normalized pixel crops and require normal, pressed, active, and disabled crops to differ. Assert the 2x image dimensions and every defined region are exactly doubled.

- [ ] **Step 2: Run the focused test and observe RED**

Run:

    python3 -m unittest packaging.tests.test_ios_touch_skin_assets -v

Expected: FAIL because the generator and eighteen runtime PNG files do not exist.

- [ ] **Step 3: Implement the deterministic generator**

Use Pillow at 2x as the source of truth, then LANCZOS-downsample the 1x files. Encode the approved design tokens:

    ALLIES = Skin(shell="#13283a", metal="#8ba4b5", accent="#8fe7ff",
                  active="#5be3ff", geometry="rounded")
    SOVIETS = Skin(shell="#241a19", metal="#716b64", accent="#f0a43c",
                  active="#d93428", geometry="octagonal")
    YURI = Skin(shell="#160f1d", metal="#756780", accent="#cf56dc",
                active="#9dcc62", geometry="crystalline")

Draw five semantic glyphs in the fixed order STOP, DEPLOY, SELECT_TYPE, FORCE_ATTACK, RETURN_BASE. Draw seven action rows: normal, disabled, hover, pressed, active, active-hover, active-pressed. Pressed must combine a 3px visual inset and darker inner shadow; active must use the faction accent and must not be a recolored pressed state.

Draw joystick atlases with regions base 0,0,144,144; thumb 160,0,64,64; thumb-active 160,72,64,64. Draw quickbar atlases with eight 48x52 button state cells along y=0, expanded and compact 9-slice panels at y=64, and 26x26 collapse/expand glyphs at 259,67 and 291,67. Keep all unused pixels transparent.

- [ ] **Step 4: Generate assets and verify GREEN**

Run:

    python3 packaging/generate_ios_touch_skin_assets.py
    python3 -m unittest packaging.tests.test_ios_touch_skin_assets -v

Expected: all atlas tests PASS.

- [ ] **Step 5: Visually inspect the nine 2x masters**

Use view_image on all -2x files. Reject any unreadable glyph, accidental text/logo, mismatched state, clipped edge, or faction style contamination. Regenerate from the script rather than editing binary files manually.

### Task 2: Add Touch-Only Faction Resolution and Chrome Collections

**Files:**
- Create: engine/OpenRA.Mods.Common/Widgets/TouchFactionSkin.cs
- Modify: mods/ra2/metrics.yaml
- Modify: mods/ra2/chrome.yaml
- Modify: packaging/tests/test_ios_touch_skin_assets.py

- [ ] **Step 1: Add failing resolver and Chrome source tests**

Add NUnit cases for:

    TouchFactionSkin.Resolve("america") == "allies"
    TouchFactionSkin.Resolve("russia") == "soviets"
    TouchFactionSkin.Resolve("yuri") == "yuri"
    TouchFactionSkin.Resolve("unknown") == "allies"

Add source assertions that metrics still contain FactionSuffix-yuri: soviets and now contain TouchFactionSuffix-yuri: yuri. Add Python assertions that every Chrome Image/Image2x exists and every 1x/2x region stays inside its atlas.

- [ ] **Step 2: Observe RED**

Run:

    dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~IosViewportControlsLayoutTest -m:1
    python3 -m unittest packaging.tests.test_ios_touch_skin_assets -v

Expected: resolver type and Chrome collections are missing.

- [ ] **Step 3: Implement the pure resolver**

Implement:

    public static string Resolve(string internalName)
    {
        if (string.IsNullOrEmpty(internalName))
            return "allies";

        if (ChromeMetrics.TryGet("TouchFactionSuffix-" + internalName, out string touch))
            return Normalize(touch);
        if (ChromeMetrics.TryGet("FactionSuffix-" + internalName, out string fallback))
            return Normalize(fallback);
        return "allies";
    }

Normalize must allow only allies, soviets, yuri. Do not change AddFactionSuffixLogic.

- [ ] **Step 4: Append metrics and Chrome collections**

Append TouchFactionSuffix mappings for Allied countries, Soviet countries, and Yuri. Preserve the existing global Yuri line.

Append collections:

- ios-touch-actions-{faction} with Image and Image2x plus seven state regions.
- ios-touch-actions-{faction}-highlighted inheriting the base and remapping normal/hover/pressed to the three active rows.
- ios-touch-joystick-{faction} with base/thumb/thumb-active.
- ios-touch-quickbar-button-{faction} and seven state suffix collections.
- ios-touch-quickbar-panel-{faction}, ios-touch-quickbar-panel-compact-{faction}, ios-touch-quickbar-toggle-{faction}.

- [ ] **Step 5: Verify GREEN**

Run both focused commands from Step 2. Expected: all resolver and asset/Chrome tests PASS.

### Task 3: Implement Responsive Joystick and Five-Button Geometry

**Files:**
- Create: engine/OpenRA.Mods.Common/Widgets/IosViewportControlsLayout.cs
- Create: engine/OpenRA.Test/IosViewportControlsLayoutTest.cs
- Modify: engine/OpenRA.Game/Support/IosScreenMetrics.cs
- Modify: engine/OpenRA.Game/Input/VirtualViewportJoystickState.cs
- Modify: engine/OpenRA.Mods.Common/Widgets/ImageWidget.cs
- Modify: engine/OpenRA.Mods.Common/Widgets/VirtualViewportJoystickWidget.cs
- Modify: engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IosViewportActionsLogic.cs
- Modify: mods/ra2/chrome/ingame.yaml
- Modify: engine/OpenRA.Test/IosViewportActionLayoutTest.cs

- [ ] **Step 1: Write failing pure geometry tests**

Use device snapshots:

    (1560x720, native 932x430, safe 59/0/59/21)
    (1558x720, native 844x390, safe 47/0/47/21)
    (1133x744, native 1133x744, safe bottom 20)
    (1180x820, native 1180x820, safe bottom 20)
    (1366x1024, native 1366x1024, safe bottom 20)

For each 112/128/144 setting assert:

- all bounds are inside SafeBounds and at least 12pt above bottomObstacleTop;
- button physical diameter is 56pt and never below 48pt;
- button rectangles do not intersect each other or the joystick thumb sweep;
- the five center radii differ by at most one logical pixel;
- center angles are -150, -115, -80, -45, -10 within rounding tolerance;
- base, thumb, movement radius, and ring radius increase monotonically.

Add normalization cases 0/111/145/999 => 128 and 112/128/144 => themselves.

- [ ] **Step 2: Observe RED**

Run:

    dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~IosViewportControlsLayoutTest -m:1

Expected: missing IosViewportControlsLayout and point conversion APIs.

- [ ] **Step 3: Implement point conversion and pure layout**

IosScreenSnapshot exposes:

    public int LogicalPoints(double points) =>
        (int)Math.Ceiling(Math.Max(0, points) * LogicalPerPoint);

    public bool IsCompactPhone =>
        Math.Min(NativePointSize.Width, NativePointSize.Height) < 600;

IosViewportControlsLayout uses:

    angles = [-150, -115, -80, -45, -10]
    ringRadiusPoints = normalizedSize * 127.0 / 128.0
    thumbDiameterPoints = normalizedSize * 0.42
    movementRadiusPoints = normalizedSize * 0.27
    buttonDiameterPoints = 56
    marginPoints = 12

Return screen-space JoystickBounds, five action rectangles, their union ActionsBounds, and all scaled dimensions.

- [ ] **Step 4: Add resizable joystick state tests and implementation**

Write RED cases: begin/move activates state; SetRadius(newRadius) clears Active, ThumbOffset, Direction, and Speed; the next move clamps at newRadius.

Implement Radius as a private-set property and SetRadius as End followed by a positive normalized assignment.

- [ ] **Step 5: Add opt-in image scaling and widget layout**

Write RED tests/source contracts that ImageWidget.StretchToFit defaults false and is copied by Clone. When true, Draw uses RenderBounds.Size. Add VirtualViewportJoystickWidget.ApplyLayout and CancelActiveInput; update state radius before accepting new input, release focus, and scale both base and thumb sprites to the computed sizes.

- [ ] **Step 6: Connect live layout without changing commands**

IosViewportActionsLogic caches resolution, NativePointSize, SafeBounds, normalized size, and actual command-bar top. On signature change it:

1. creates IosViewportControlsLayout;
2. applies JoystickBounds and scaled dimensions;
3. sets the action container to ActionsBounds;
4. converts each screen rectangle to container-local bounds;
5. sets each icon to fill its button;
6. preserves CommandBarLogic, BindButtonIcon, existing IDs, disabled predicates, and ForceModifiersOrderGenerator state.

In ingame.yaml keep fallback dimensions, remove obsolete fixed X/Y values, set the action ImageCollection base to ios-touch-actions, and set StretchToFit: True. Apply the resolved faction collection in IosViewportActionsLogic.

- [ ] **Step 7: Replace obsolete fixed-coordinate tests and verify GREEN**

Run:

    dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter "FullyQualifiedName~IosViewportControlsLayoutTest|FullyQualifiedName~IosViewportActionLayoutTest" -m:1

Expected: all layout/state/integration tests PASS.

### Task 4: Add the Three-Size Touch Setting

**Files:**
- Modify: engine/OpenRA.Game/Settings.cs
- Modify: engine/OpenRA.Mods.Common/Widgets/Logic/Settings/InputSettingsLogic.cs
- Modify: engine/OpenRA.Mods.Common/Widgets/Logic/Settings/IosSettingsVisibilityPolicy.cs
- Modify: engine/mods/common/chrome/settings-input.yaml
- Modify: engine/mods/common/fluent/common.ftl
- Modify: engine/mods/common/fluent/zh-CN/common.ftl
- Extend: engine/OpenRA.Test/IosViewportControlsLayoutTest.cs

- [ ] **Step 1: Write failing settings tests**

Assert GameSettings default 128, dropdown options exactly 112/128/144, invalid stored values display and apply as 128, selection writes through immediately, Reset restores 128, iOS shows TOUCH_JOYSTICK_SIZE_CONTAINER, desktop hides it, and a panel without the optional control initializes safely.

- [ ] **Step 2: Observe RED**

Run the IosViewportControlsLayoutTest filter. Expected: missing field/control/policy APIs.

- [ ] **Step 3: Implement persistence and optional binding**

Add:

    public int IosVirtualJoystickSize = 128;

InputSettingsLogic uses GetOrNull<DropDownButtonWidget>("TOUCH_JOYSTICK_SIZE_DROPDOWN"). Its text and selection always pass through NormalizeJoystickPoints. Reset copies the default field. The options are localized Small 112 pt, Medium 128 pt, Large 144 pt.

Add an iOS-only full-width row immediately below the input section header. The Dropdown remains at least 48pt after the existing iOS SettingsLogic transformation. Add English and Chinese labels without OpenRA branding.

- [ ] **Step 4: Verify GREEN and settings regression**

Run:

    dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter "FullyQualifiedName~IosViewportControlsLayoutTest|FullyQualifiedName~IosSettingsLayoutTest" -m:1

Expected: all focused settings/layout tests PASS.

### Task 5: Apply Faction Skins to Joystick and Quickbar Without Changing Symbols

**Files:**
- Modify: engine/OpenRA.Mods.Common/Widgets/VirtualViewportJoystickWidget.cs
- Modify: engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IosViewportActionsLogic.cs
- Modify: OpenRA.Mods.RA2/Widgets/CustomCommandBarWidget.cs
- Extend: engine/OpenRA.Test/IosViewportControlsLayoutTest.cs
- Extend: packaging/tests/test_ios_touch_skin_assets.py

- [ ] **Step 1: Write failing integration contracts**

Assert the joystick collection resolves to ios-touch-joystick-{skin}, action images to ios-touch-actions-{skin}, and iOS CustomCommandBarWidget selects ios-touch-quickbar-panel/button/toggle-{skin}. Assert non-iOS still uses commandbar-panel, commandbar-button, commandbar-toggle-icons. Parse ingame-player.yaml and assert shortcut IDs/order and the icon collections ios-commandbar-icons-v2 and production-x5-icon are unchanged.

- [ ] **Step 2: Observe RED**

Run the C# layout filter and Python asset test. Expected: integration names are not used.

- [ ] **Step 3: Implement iOS-only skin selection**

VirtualViewportJoystickWidget resolves from worldRenderer.World.LocalPlayer and caches faction collection. IosViewportActionsLogic resolves the same suffix and sets the five ImageWidget collections before BindButtonIcon state lookup.

CustomCommandBarWidget resolves through Game.OrderManager.World only after EnsureSetup can see a live local player. When Platform.IsIOS:

    Background = expanded
        ? "ios-touch-quickbar-panel-" + skin
        : "ios-touch-quickbar-panel-compact-" + skin;

Set all quickbar slot, edit, and collapse button backgrounds to ios-touch-quickbar-button-{skin}; set only the collapse icon collection to ios-touch-quickbar-toggle-{skin}. Do not change child shortcut icon ImageCollection or order. On desktop retain existing names byte-for-byte.

- [ ] **Step 4: Verify GREEN**

Run both focused suites. Expected: all skin integration and symbol-preservation tests PASS.

### Task 6: Implement Safe-Area Support-Power Layout and Exact Hit Testing

**Files:**
- Create: engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IosSupportPowerLayoutPolicy.cs
- Create: engine/OpenRA.Test/IosSupportPowerLayoutPolicyTest.cs
- Modify: engine/OpenRA.Mods.Common/Widgets/SupportPowersWidget.cs
- Modify: engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/SupportPowerBinLogic.cs
- Modify: engine/OpenRA.Mods.Common/Widgets/TooltipContainerWidget.cs
- Modify: engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/SupportPowerTooltipLogic.cs

- [ ] **Step 1: Write failing layout matrix tests**

Cover iPhone 844x390 and 932x430 with asymmetric left/right Safe Areas; iPad mini 1133x744 and iPad 1366x1024; non-iOS desktop. Test icon counts 0/1/2/6/12 and occupied rectangles for production/sidebar, expanded/collapsed/edit quickbar, joystick, and action ring.

Assert:

- compact cells are at least 48 physical points;
- every cell is inside SafeBounds inset by 8pt;
- cells do not intersect each other or occupied rectangles expanded by 8pt;
- order starts at lower right, moves right-to-left, then upward;
- iPad remains top-left vertical inside SafeBounds;
- desktop returns no override;
- identical inputs are deterministic and all phone matrices fit twelve cells.

- [ ] **Step 2: Observe RED**

Run:

    dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~IosSupportPowerLayoutPolicyTest -m:1

Expected: missing policy and layout types.

- [ ] **Step 3: Implement the pure policy**

Define Desktop, SafeTopLeft, CompactBottomRight. For compact phones use ceil(48 * LogicalPerPoint) square cells, 6pt gap, 8pt Safe Area margin, and obstacles expanded by 8pt. For each row find the rightmost continuous free interval, place cells from right to left, then move one cell+gap upward. Never shrink or silently hide a cell; return FitsAll=false when impossible.

- [ ] **Step 4: Add failing hit-test and reflow tests**

Assert cell center hits; cell gap, obstacle hole, and empty area inside the union bounding rectangle do not hit. Assert unchanged signature does not rebuild foreground children, while SafeBounds/rotation/icon count/obstacle changes rebuild exactly once.

- [ ] **Step 5: Implement widget and logic integration**

SupportPowersWidget accepts an optional cell provider. It keeps current single-axis behavior when null. With a provider, use cell rectangles as dictionary keys, center the 60x48 art inside each cell, override EventBoundsContains to check actual cells, and return false for mouse down outside all cells.

SupportPowerBinLogic caches resolution, NativePointSize, SafeBounds, icon count, and visible obstacle bounds. It recomputes only on signature change, updates the provider, calls RefreshIcons once, and rebuilds foreground clones at the resulting local cell positions. Query obstacles after construction so dynamic commandbar/sidebar bounds exist.

- [ ] **Step 6: Clamp support-power tooltips to Safe Area**

Add tests for all four corners and long English/Chinese descriptions. On iOS clamp left/top/right/bottom to SafeBounds and cap tooltip width to SafeBounds minus 16pt; remeasure on resolution/SafeBounds change. Desktop retains existing full-resolution behavior.

- [ ] **Step 7: Verify GREEN**

Run:

    dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter "FullyQualifiedName~IosSupportPowerLayoutPolicyTest|FullyQualifiedName~IosViewportControlsLayoutTest|FullyQualifiedName~IosViewportActionLayoutTest" -m:1

Expected: all support-power and touch-control tests PASS.

### Task 7: Add Final Bundle Audit and Run Full Verification

**Files:**
- Create: packaging/audit_ios_touch_runtime_bundle.py
- Extend: packaging/tests/test_ios_touch_skin_assets.py
- Modify only if generated type coverage requires it: ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs

- [ ] **Step 1: Write the failing app-bundle audit contract**

The audit accepts an app path and must reject:

- missing 1x/2x touch assets;
- source/bundle SHA mismatch;
- non-POT, wrong mode, or region overflow;
- missing touch Chrome collections or metrics;
- any active iOS touch path still referencing old ios-joystick-command-icons or ios-viewport-joystick;
- changed shortcut symbol order;
- bundled design-demos/faction-touch-controls concept boards;
- missing runtime policy types in the compiled assemblies/AOT registry when a reflected type was introduced.

- [ ] **Step 2: Observe RED against the current app**

Run:

    python3 packaging/audit_ios_touch_runtime_bundle.py ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app

Expected: FAIL because the current app predates the new runtime assets and wiring.

- [ ] **Step 3: Run complete source verification**

Run:

    python3 -m unittest packaging.tests.test_ios_touch_skin_assets -v
    dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter "FullyQualifiedName~IosViewportControlsLayoutTest|FullyQualifiedName~IosSupportPowerLayoutPolicyTest|FullyQualifiedName~IosViewportActionLayoutTest|FullyQualifiedName~IosSettingsLayoutTest" -m:1
    ./utility.sh --check-yaml
    dotnet build engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj -c Release --no-restore -m:1
    dotnet build OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore -m:1
    git diff --check

Expected: zero failures and zero build errors; only pre-existing dependency/localization warnings may remain and must be itemized.

- [ ] **Step 4: Rebuild the iOS app without installing**

Run the repository's signed/personal ios-arm64 Release build path. Do not install to a device unless the user separately requests installation. Confirm the final app is newer than all source assets.

- [ ] **Step 5: Run the final bundle audit**

Run:

    python3 packaging/audit_ios_touch_runtime_bundle.py ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app

Expected: PASS with all eighteen source/bundle SHA matches, Chrome/YAML parity, no concept boards, and no missing sprite/collection reference.

- [ ] **Step 6: Independent review and delivery inventory**

Dispatch a fresh specification reviewer, then a fresh code-quality reviewer, then a final holistic reviewer. Resolve every Critical/Important finding and rerun the exact failing gate. Report:

- all modified/new files;
- focused and aggregate test counts;
- build and bundle-audit results;
- every engine file hidden by the root .gitignore that must later be force-added;
- no staging, commit, push, installation, or destructive cleanup.

---

## Plan Self-Review

- Spec coverage: faction-specific joystick/actions/quickbar, preserved shortcut symbols/order, 112/128/144 setting, live cancellation, equal-radius action ring, compact iPhone support powers, iPad/desktop behavior, Safe Area, tooltip, POT assets, and final app audit each map to a concrete task.
- Placeholder scan: no deferred implementation markers or unspecified test steps remain.
- Type consistency: IosScreenSnapshot.LogicalPoints and IsCompactPhone are shared by both pure layouts; TouchFactionSkin is non-reflected; SupportPowerBinLogic and SupportPowersWidget constructor signatures remain unchanged; no AOT registry edit is expected unless implementation violates that boundary.
- Scope discipline: the plan does not alter commands, shortcut ordering, game rules, UIKit publishing, global FactionSuffix-yuri, or device installation.
