# iOS Settings Responsive Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the iOS settings window and all of its controls fit the live game viewport at every supported landscape resolution.

**Architecture:** Extend the pure `IosSettingsLayout` policy to calculate one viewport scale from a 1180×720 design canvas. Apply that policy consistently to the settings shell and loaded panel widget trees while leaving desktop behavior unchanged.

**Tech Stack:** C#, OpenRA widget system, NUnit, .NET for iOS.

---

### Task 1: Viewport layout policy

**Files:**
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Settings/IosSettingsLayout.cs`
- Test: `engine/OpenRA.Test/IosSettingsLayoutTest.cs`

- [ ] Add failing tests for standard iPad, compact iPad UI-scale, and iPhone landscape viewports.
- [ ] Verify the tests fail because the fixed policy has no viewport-aware geometry.
- [ ] Add design-canvas scaling, centered window bounds, and scaled metrics.
- [ ] Run the focused tests and verify they pass.

### Task 2: Apply one coordinate transform

**Files:**
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Settings/SettingsLogic.cs`

- [ ] Replace fixed shell calculations with the viewport policy.
- [ ] Scale panel X/Y/width/height and scroll spacing consistently.
- [ ] Preserve scroll layout and desktop behavior.
- [ ] Run the focused tests and YAML checks.

### Task 3: Device verification

**Files:**
- Build: `ios/OpenRA.iOS/OpenRA.iOS.csproj`

- [ ] Build Release for `ios-arm64`.
- [ ] Install the app on the connected iPad.
- [ ] Launch and verify that the game remains responsive.
