# iPad Touch Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add resolution-independent, testable iPad gestures while preserving OpenRA's existing mouse input and menus.

**Architecture:** A pure gesture state class converts normalized SDL finger data into semantic pointer actions. `Sdl2Input` owns this class only on iOS and translates its actions into existing `MouseInput` events. Later tasks extend the same boundary for long press and two-finger viewport gestures.

**Tech Stack:** C#/.NET 8, SDL2-CS, NUnit, .NET iOS simulator and arm64 builds.

---

### Task 1: Pure touch coordinate mapping

**Files:**
- Create: `engine/OpenRA.Platforms.Default/TouchGestureAdapter.cs`
- Create: `engine/OpenRA.Test/TouchGestureAdapterTest.cs`
- Modify: `engine/OpenRA.Platforms.Default/Sdl2Input.cs`

- [ ] Write failing NUnit cases for normalized corners, center, and clamping at a 1024x768 logical size.
- [ ] Run `dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --filter TouchGestureAdapterTest` and verify the adapter is missing.
- [ ] Implement `TouchGestureAdapter.MapPosition(float x, float y, Size size)` with logical-size multiplication and edge clamping.
- [ ] Replace local coordinate conversion in `Sdl2Input` with the adapter.
- [ ] Run the focused test and iOS simulator build; expect zero errors.
- [ ] Commit only the adapter, tests, and `Sdl2Input` changes.

### Task 2: Tap and drag state machine

**Files:**
- Modify: `engine/OpenRA.Platforms.Default/TouchGestureAdapter.cs`
- Modify: `engine/OpenRA.Test/TouchGestureAdapterTest.cs`
- Modify: `engine/OpenRA.Platforms.Default/Sdl2Input.cs`

- [ ] Add failing tests proving a stationary finger produces one click, movement beyond eight points produces down/move/up drag, and minor jitter remains a tap.
- [ ] Run focused tests and verify the new cases fail.
- [ ] Implement first-finger tracking and semantic `TouchPointerAction` values.
- [ ] Translate actions into existing `MouseInput` events without modifying non-iOS mouse handling.
- [ ] Verify focused tests, simulator build, and one menu button plus one slider drag in Simulator.
- [ ] Commit the tested tap/drag state machine.

### Task 3: Long-press right click

**Files:**
- Modify: `engine/OpenRA.Platforms.Default/TouchGestureAdapter.cs`
- Modify: `engine/OpenRA.Test/TouchGestureAdapterTest.cs`
- Modify: `engine/OpenRA.Platforms.Default/Sdl2Input.cs`

- [ ] Add failing tests for a 450 ms stationary long press and cancellation after movement or second finger.
- [ ] Implement time-aware long-press polling and right-button down/up actions.
- [ ] Verify tests, both iOS builds, and a simulator context/order interaction.
- [ ] Commit long-press support.

### Task 4: Two-finger viewport gestures

**Files:**
- Modify: `engine/OpenRA.Platforms.Default/TouchGestureAdapter.cs`
- Modify: `engine/OpenRA.Test/TouchGestureAdapterTest.cs`
- Modify: `engine/OpenRA.Platforms.Default/Sdl2Input.cs`

- [ ] Add failing tests for two-finger cancellation, centroid pan, and pinch direction.
- [ ] Emit middle-button pan and wheel-compatible zoom events through existing OpenRA input paths.
- [ ] Verify tests and simulator pan/zoom behavior in a skirmish.
- [ ] Commit two-finger gestures.

### Task 5: Touch HUD assessment and implementation

**Files:**
- Modify: `mods/ra2/chrome/ingame-player.yaml`
- Modify: `OpenRA.Mods.RA2/Widgets/Logic/SelectionCommandBarLogic.cs`
- Test: `engine/OpenRA.Test/` or RA2 lint/build checks selected from the existing project

- [ ] Record which commands remain inaccessible after gesture support.
- [ ] Add only the required large touch buttons to the existing command bar.
- [ ] Verify RA2 mod loading, simulator taps, and a skirmish order flow.
- [ ] Commit the touch HUD independently.

