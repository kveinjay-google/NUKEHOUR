# iOS Touch Viewport Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a fixed lower-left iOS viewport joystick, exclusive three-finger map dragging, and remove editor/asset-browser access from the iOS product.

**Architecture:** Extend the platform-neutral touch state machine for three fingers, then route the third SDL finger through the existing middle-button viewport path. Add a testable radial joystick state and a dedicated in-game widget that captures its own touch-compatible mouse stream and scrolls the viewport continuously. Keep product exclusions conditional at runtime/package boundaries so desktop features remain intact.

**Tech Stack:** C#/.NET, SDL2, OpenRA Widget/Chrome, MiniYAML, NUnit, .NET iOS

---

### Task 1: Three-finger pan state machine

**Files:**
- Modify: `engine/OpenRA.Game/Input/TouchGestureAdapter.cs`
- Modify: `engine/OpenRA.Test/TouchGestureAdapterTest.cs`
- Modify: `engine/OpenRA.Platforms.Default/Sdl2Input.cs`

- [ ] Add failing tests proving a third finger releases the active two-finger gesture, starts one exclusive middle-button pan, emits centroid movement, suppresses partial-release taps, and cancels idempotently.
- [ ] Run `env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --filter TouchGestureAdapterTest --no-restore` and verify the new tests fail for missing three-finger APIs.
- [ ] Implement third-position state and route a third SDL finger id through begin/move/end/reset paths.
- [ ] Re-run the focused tests and build `engine/OpenRA.Platforms.Default/OpenRA.Platforms.Default.csproj` with zero errors.

### Task 2: Fixed virtual joystick

**Files:**
- Create: `engine/OpenRA.Game/Input/VirtualViewportJoystickState.cs`
- Create: `engine/OpenRA.Test/VirtualViewportJoystickStateTest.cs`
- Create: `engine/OpenRA.Mods.Common/Widgets/VirtualViewportJoystickWidget.cs`
- Modify: `mods/ra2/chrome/ingame.yaml`
- Modify: `mods/ra2/chrome/ingame-player.yaml`

- [ ] Add failing tests for the 15% dead zone, normalized direction, radial clamp, proportional speed, wrong-pointer rejection, and release/cancel recentering.
- [ ] Run the focused tests and verify failure because the joystick state does not exist.
- [ ] Implement the state object and make focused tests pass.
- [ ] Implement a 112px iOS-only widget that takes mouse focus, draws a semi-transparent base/thumb using existing color rendering, and applies viewport scrolling each tick.
- [ ] Add it after world interaction widgets and reserve the lower-left command-bar region.
- [ ] Build the Common and RA2 assemblies with zero errors.

### Task 3: iOS editor and asset-browser exclusion

**Files:**
- Create: `engine/OpenRA.Test/IosProductFeaturePolicyTest.cs`
- Create: `engine/OpenRA.Game/Support/IosProductFeaturePolicy.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs`
- Modify: `mods/ra2/chrome/mainmenu.yaml`
- Modify: `ios/OpenRA.iOS/OpenRA.iOS.csproj`

- [ ] Add failing policy/layout tests proving iOS hides both tools while desktop retains them.
- [ ] Implement a platform policy and condition both menu buttons without changing desktop behavior.
- [ ] Exclude editor-specific Chrome and asset-browser resources from the iOS bundle without stripping shared gameplay rules.
- [ ] Run focused and full engine tests.

### Task 4: Documentation, build, and device verification

**Files:**
- Modify: `docs/codebase-research/input-touch-orders-ui.md`
- Modify: `docs/codebase-research/build-deploy-device-testing.md`

- [ ] Document the gesture transitions, joystick bounds/dead zone, and iOS exclusions.
- [ ] Run the complete engine tests, platform build, RA2 build, and signed iOS Release build.
- [ ] Install the signed app on the paired iPad and launch with the old process terminated.
- [ ] Verify launch/process state and record manual smoke-test checkpoints for joystick stop/recenter, three-finger pan, ordinary selection, and command-bar clearance.
