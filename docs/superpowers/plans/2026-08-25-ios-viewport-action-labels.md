# iOS Viewport Action Labels Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add always-visible localized labels inside the five iOS viewport action buttons without changing their 56pt hit geometry or circular layout.

**Architecture:** Extend the existing pure iOS viewport layout policy with a local per-button label layout, then make `IosViewportActionsLogic` apply it to YAML-defined label children. Update the deterministic touch-skin generator so each faction/state action sprite moves its glyph upward and reserves a quiet lower label well while the label text remains live Fluent UI.

**Tech Stack:** C#/.NET 8, OpenRA Widget YAML and Fluent, Python/Pillow deterministic atlas generation, NUnit and Python unittest.

---

### Task 1: Lock the runtime label contract with failing tests

**Files:**
- Modify: `engine/OpenRA.Test/IosViewportControlsLayoutTest.cs`
- Modify: `engine/OpenRA.Test/IosViewportActionLayoutTest.cs`
- Modify: `packaging/tests/test_ios_touch_skin_assets.py`
- Modify: `packaging/tests/test_ios_touch_runtime_bundle.py`

- [x] **Step 1: Add pure geometry tests**

Add device-matrix assertions that `IosViewportActionLabelLayout.Create(snapshot, buttonDiameter)` returns a 14pt-high label rectangle with 6pt horizontal and 6pt bottom insets, fully contained by the existing action rectangle, with the compact-phone font distinct from the iPad font.

Measure every dedicated English and Chinese label with the actual configured Noto Sans CJK fonts and include the contrast radius in width/height fit assertions.

- [x] **Step 2: Add exact YAML and Fluent mapping tests**

For each of `STOP`, `DEPLOY`, `SELECT_TYPE`, `FORCE_ATTACK`, and `RETURN_BASE`, assert an exact `Label@LABEL` child after `Image@ICON`, `IgnoreMouseOver: true`, stable high-contrast styling, and its dedicated `button-ios-viewport-action-*.label` Fluent key. Verify default values `Stop / Deploy / Type / Force / Base` and `zh-CN` values `停止 / 部署 / 同类 / 强攻 / 基地`. Assert no `ColorBlock@LABEL_PLATE` is introduced because `ColorBlockWidget` can take mouse focus.

- [x] **Step 3: Add generated-pixel tests**

Assert every faction and action-state crop contains a low-detail, nontransparent label well at the generator-declared coordinates while the glyph region remains visibly nonempty above it. Keep the existing seven-state, gutter, pressed-offset, and determinism contracts.

Add bundle-audit regressions that reject stale or missing default and `zh-CN` Fluent files and require compiled metadata for `IosViewportActionLabelLayout.Create`.

- [x] **Step 4: Run RED**

Run:

```sh
env DOTNET_ROLL_FORWARD=Major dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter 'FullyQualifiedName~IosViewportControlsLayoutTest|FullyQualifiedName~IosViewportActionLayoutTest' -v minimal
python3 -B -m unittest packaging.tests.test_ios_touch_skin_assets -v
```

Expected: failures specifically report the missing label layout API, YAML label children, and generated label well.

### Task 2: Implement the responsive localized label overlay

**Files:**
- Modify: `engine/OpenRA.Mods.Common/Widgets/IosViewportControlsLayout.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IosViewportActionsLogic.cs`
- Modify: `mods/ra2/chrome/ingame.yaml`
- Modify: `mods/ra2/mod.yaml`
- Modify: `mods/ra2/fluent/chrome.ftl`
- Modify: `mods/ra2/fluent/zh-CN/chrome.ftl`

- [x] **Step 1: Add the pure per-button content layout**

Add `IosViewportActionLabelLayout` with a `Create(IosScreenSnapshot snapshot, int buttonDiameter)` factory. Convert 14pt height and 6pt horizontal/bottom insets through `snapshot.LogicalPoints`, return local `Rectangle` bounds, and select `IosTouchLabel` for compact phones or `TinyBold` otherwise.

- [x] **Step 2: Add an iPhone-sized font**

Register `IosTouchLabel` in `mods/ra2/mod.yaml` using the existing Noto Sans CJK bold font at 20 logical pixels. Add executable fit checks for all five English and Chinese short labels across both compact-phone snapshots and the iPad snapshots.

- [x] **Step 3: Add label children**

Under each action button, add only `Label@LABEL` after `Image@ICON`; the generated atlas supplies the dark label well. Use the exact approved Fluent key, `Align: Center`, `Font: TinyBold` as startup fallback, near-white text, contrast, and `IgnoreMouseOver: true`.

Add the five dedicated default and `zh-CN` Fluent messages with the approved short values.

- [x] **Step 4: Wire live bounds and fonts**

Cache the five `LabelWidget` instances in `IosViewportActionsLogic`. During `ApplyLayout`, leave each button and hit rectangle unchanged, keep the action image at full button size, and apply the pure local label bounds/font to the text overlay.

- [x] **Step 5: Run GREEN**

Run the focused NUnit command from Task 1. Expected: all selected tests pass.

### Task 3: Reserve the visual label well in all faction action atlases

**Files:**
- Modify: `packaging/generate_ios_touch_skin_assets.py`
- Modify: `packaging/audit_ios_touch_runtime_bundle.py`
- Modify: `packaging/public-content-manifest.json`
- Regenerate: `mods/ra2/uibits/ios-touch-actions-allies.png`
- Regenerate: `mods/ra2/uibits/ios-touch-actions-allies-2x.png`
- Regenerate: `mods/ra2/uibits/ios-touch-actions-soviets.png`
- Regenerate: `mods/ra2/uibits/ios-touch-actions-soviets-2x.png`
- Regenerate: `mods/ra2/uibits/ios-touch-actions-yuri.png`
- Regenerate: `mods/ra2/uibits/ios-touch-actions-yuri-2x.png`

- [x] **Step 1: Move glyphs and draw an empty well**

Introduce explicit action-label constants. Move each semantic glyph upward by the same amount, then draw a palette-aware dark rounded label well at the bottom of every action state without drawing text into the raster asset. Account for `inset_sprite` so the final 1x/2x well aligns with the live label bounds. If the well remains fixed across states, mask it from the existing pressed-shell displacement assertion.

- [x] **Step 2: Regenerate deterministically**

Run:

```sh
python3 -B packaging/generate_ios_touch_skin_assets.py
```

- [x] **Step 3: Run GREEN and visual inspection**

Run the Python suite from Task 1 twice and compare generated SHA-256 values between runs. Inspect all three 2x action masters to confirm glyphs remain recognizable and the label well is clean in normal, disabled, pressed, active and hover states.

- [x] **Step 4: Refresh tracked public-manifest hashes**

Update the existing entries for `mods/ra2/mod.yaml`, `mods/ra2/chrome/ingame.yaml`, `mods/ra2/fluent/chrome.ftl`, and `mods/ra2/fluent/zh-CN/chrome.ftl` to their final byte size and SHA-256 while preserving their current provenance, license-review, and public flags. Do not add the six action atlases to the public manifest without a separate provenance/public allowlist decision.

### Task 4: Verify integration and regressions

**Files:**
- Modify only if a focused test exposes an in-scope defect.

- [x] **Step 1: Run focused and adjacent tests**

```sh
env DOTNET_ROLL_FORWARD=Major dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter 'FullyQualifiedName~IosViewportControlsLayoutTest|FullyQualifiedName~IosViewportActionLayoutTest|FullyQualifiedName~IosFloatingControlsToggleTest|FullyQualifiedName~IosTouchWidgetPolicyTest' -v minimal
python3 -B -m unittest packaging.tests.test_ios_touch_skin_assets packaging.tests.test_ios_touch_runtime_bundle -v
```

- [x] **Step 2: Validate data and build**

```sh
./utility.sh --check-yaml
dotnet build engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj -c Release --no-restore --nologo
git diff --check
```

- [x] **Step 3: Review**

Perform an independent spec review followed by an independent code-quality review. Fix all Critical and Important findings with new RED-to-GREEN tests, then rerun the complete verification set.

- [x] **Step 4: Preserve delivery boundaries**

Do not stage, commit, install, reset, stash, or modify the unrelated Yuri Grinder changes. Report any ignored/new files that would require explicit collection later.
