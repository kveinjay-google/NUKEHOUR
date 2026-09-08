# iPad Touch Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the approved wide-sidebar, large-target iPad settings menu without changing desktop settings.

**Architecture:** Add a small, pure iOS settings-layout policy next to `SettingsLogic`, then apply its geometry only when `OperatingSystem.IsIOS()` is true. Keep existing panel logic and data binding intact; transform loaded widget bounds after panel construction.

**Tech Stack:** C#, OpenRA widget system, NUnit, .NET for iOS.

---

### Task 1: iPad layout policy

**Files:**
- Create: `engine/OpenRA.Mods.Common/Widgets/Logic/Settings/IosSettingsLayout.cs`
- Create: `engine/OpenRA.Test/IosSettingsLayoutTest.cs`

- [ ] Write failing tests for the 280-unit sidebar, 56-unit tabs, 52-unit actions, 1.45 content scale, and desktop no-op policy.
- [ ] Run the focused tests and confirm the missing policy produces compilation failures.
- [ ] Implement immutable layout metrics and coordinate scaling helpers.
- [ ] Run the focused tests and confirm all policy tests pass.

### Task 2: Apply the iPad shell and content layout

**Files:**
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Settings/SettingsLogic.cs`
- Modify: `mods/ra2/chrome/settings.yaml`

- [ ] Apply the policy after all settings panels are loaded, only on iOS.
- [ ] Resize and reposition the dialog, sidebar, content panel, Reset, and Done controls.
- [ ] Vertically scale each loaded panel tree so interactive controls receive larger targets and spacing.
- [ ] Preserve desktop YAML geometry and existing save/reset callbacks.

### Task 3: Verify and deploy

**Files:**
- Modify: `docs/superpowers/specs/2026-08-12-ipad-touch-settings-design.md` only if verification exposes a contradiction.

- [ ] Run focused layout tests.
- [ ] Run the complete `OpenRA.Test` suite.
- [ ] Build the signed Release `ios-arm64` application.
- [ ] Install it on the paired iPad and launch it.
- [ ] Commit only the settings implementation, tests, and design documents.
