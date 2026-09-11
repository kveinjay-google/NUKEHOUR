# iOS Faction Touch Control Skins Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship three original faction-specific touch-control skins for iPhone and iPad without changing touch geometry or reintroducing labels, mask artifacts, or alignment defects.

**Architecture:** Generate one high-resolution visual master for each faction, then build exact-size transparent runtime atlases through a deterministic compositor. Keep the existing `TouchFactionSkin` routing for action, joystick, and chrome assets, and add faction-suffixed shortcut-glyph collections consumed by the existing suffix logic.

**Tech Stack:** Built-in ImageGen, PNG/RGBA sprite atlases, ImageMagick validation, OpenRA Chrome YAML, C# NUnit tests, .NET iOS arm64 build and Apple code signing.

---

### Task 1: Lock the runtime asset contract with failing tests

**Files:**
- Modify: `packaging/tests/test_ios_touch_skin_assets.py`
- Modify: `engine/OpenRA.Test/IosViewportActionLayoutTest.cs`
- Modify: `engine/OpenRA.Test/IosProductionBatchToggleTest.cs`

- [ ] Add assertions for `v3` faction atlases, exact dimensions, RGBA alpha, fixed cell coordinates, faction-suffixed glyph collections, hidden shortcut labels, and transparent corners.
- [ ] Run `python3 -m unittest packaging.tests.test_ios_touch_skin_assets` and the filtered C# tests; confirm failure because the `v3` assets and collections do not exist.

### Task 2: Generate and preserve three faction style masters

**Files:**
- Create: `design-previews/ios-touch-controls-v3/allies-style-master.png`
- Create: `design-previews/ios-touch-controls-v3/soviets-style-master.png`
- Create: `design-previews/ios-touch-controls-v3/yuri-style-master.png`

- [ ] Use one built-in ImageGen call per faction with the approved material language, identical component inventory, orthographic presentation, transparent background, no captions, no logos, and no watermark.
- [ ] Inspect each result for consistent component scale, recognizable glyph semantics, and faction distinction; regenerate only the failed faction with one targeted correction.
- [ ] Copy the selected outputs from the generated-image store into the three project paths above.

### Task 3: Produce deterministic action and joystick atlases

**Files:**
- Create: `packaging/generate_ios_touch_skin_v3.py`
- Create: `mods/ra2/uibits/ios-touch-actions-allies-v3.png`
- Create: `mods/ra2/uibits/ios-touch-actions-soviets-v3.png`
- Create: `mods/ra2/uibits/ios-touch-actions-yuri-v3.png`
- Create: `mods/ra2/uibits/ios-touch-joystick-allies-v3.png`
- Create: `mods/ra2/uibits/ios-touch-joystick-soviets-v3.png`
- Create: `mods/ra2/uibits/ios-touch-joystick-yuri-v3.png`

- [ ] Implement a deterministic RGBA compositor that places each generated motif inside the existing coordinates without resampling state-to-state geometry.
- [ ] Derive disabled, hover, pressed, active, active-hover, and active-pressed variants through fixed color/depth transforms; never synthesize opaque black masks.
- [ ] Run the Python asset tests and confirm action/joystick geometry passes.

### Task 4: Produce shortcut chrome and glyph atlases

**Files:**
- Create: `mods/ra2/uibits/ios-touch-quickbar-allies-v3.png`
- Create: `mods/ra2/uibits/ios-touch-quickbar-soviets-v3.png`
- Create: `mods/ra2/uibits/ios-touch-quickbar-yuri-v3.png`
- Create: `mods/ra2/uibits/ios-commandbar-glyphs-allies-v3.png`
- Create: `mods/ra2/uibits/ios-commandbar-glyphs-soviets-v3.png`
- Create: `mods/ra2/uibits/ios-commandbar-glyphs-yuri-v3.png`

- [ ] Generate eight fixed quickbar button states, two panel slices, and collapse/expand controls with identical state geometry.
- [ ] Render all 33 catalog glyphs for each faction on a common 26 × 26 optical canvas; keep numerals and ×5 inside their glyphs.
- [ ] Verify alpha corners, equal cell dimensions, no external captions, and no black mask blocks.

### Task 5: Bind the v3 faction skins

**Files:**
- Modify: `mods/ra2/chrome.yaml`
- Modify: `mods/ra2/chrome/ingame-player.yaml`
- Modify: `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Modify: `ios/OpenRA.iOS/Info.plist`

- [ ] Point the three existing faction action, joystick, and quickbar collections at their `v3` atlases without changing any region coordinates.
- [ ] Define `ios-commandbar-glyphs-allies`, `-soviets`, and `-yuri` plus highlighted variants, and keep `AddFactionSuffixLogic` on every shortcut icon.
- [ ] Increment the independent iOS build number once.
- [ ] Run the Python and filtered C# tests; confirm all pass.

### Task 6: Capture engine delivery changes

**Files:**
- Create: `engine-patches/0035-ios-faction-touch-control-skins.patch`
- Modify: `engine-patches/README.md`

- [ ] Generate the engine patch from the post-0034 baseline so its updated visual-contract tests replay exactly.
- [ ] Apply the patch with fuzz zero in a temporary clean engine tree, compare the replayed files to the working tree, verify duplicate refusal, and verify reverse dry-run.

### Task 7: Build, audit, install, and visually verify

**Files:**
- Verify: `ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app`

- [ ] Run the full C# suite and the complete iOS touch-asset Python suite.
- [ ] Build Release `ios-arm64` with the configured Apple Development identity and provisioning profile.
- [ ] Run the iOS touch runtime bundle audit and `codesign --verify --deep --strict`.
- [ ] Install the signed app with `devicectl`, confirm the device reports the new bundle version, launch it, and inspect gameplay screenshots for all three faction skins.
