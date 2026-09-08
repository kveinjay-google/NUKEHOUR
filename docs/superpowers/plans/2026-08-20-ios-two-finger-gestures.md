# iOS Two-Finger Gestures Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add two-finger tap cancellation and long-press same-direction viewport dragging on iOS while preserving pinch zoom and three-finger dragging.

**Architecture:** Keep gesture recognition inside `TouchGestureAdapter`, which already converts raw finger positions into synthetic mouse actions. `Sdl2Input` remains responsible only for finger identity, timestamps, and dispatching the adapter result into the existing mouse input pipeline.

**Tech Stack:** C#/.NET, SDL2 finger events, NUnit.

---

### Task 1: Define two-finger gesture behavior with failing adapter tests

**Files:**
- Modify: `engine/OpenRA.Test/TouchGestureAdapterTest.cs`

- [ ] Add tests asserting that a short stationary two-finger gesture emits right-button down/up at the centroid and emits no middle-button event.
- [ ] Add tests asserting that movement before 450 ms invalidates the tap without panning, while same-direction movement after 450 ms emits middle down/move/up.
- [ ] Add tests asserting that opposing motion still emits scroll, stationary long holds do not right-click, cancellation releases active panning, and a third finger takes over cleanly.
- [ ] Run `DOTNET_ROLL_FORWARD=Major dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter TouchGestureAdapterTest` and verify the new assertions fail because two fingers currently emit `MiddleDown` immediately.

### Task 2: Implement delayed two-finger arbitration

**Files:**
- Modify: `engine/OpenRA.Game/Input/TouchGestureAdapter.cs`
- Modify: `engine/OpenRA.Platforms.Default/Sdl2Input.cs`

- [ ] Change `BeginSecond` to accept the current SDL timestamp, record both origins, and emit no middle-button event unless it must release an existing one-finger drag.
- [ ] Track tap eligibility, the long-press threshold, active two-finger panning, and centroid deltas.
- [ ] Start panning only when both displacement vectors exceed 8 pixels and their cosine similarity is at least 0.7; continue panning with incremental centroid movement.
- [ ] Keep pinch zoom based on separation changes of at least 12 pixels and keep three-finger panning immediate.
- [ ] Make release and cancellation emit right-click, middle-up, or nothing according to the active state, never both.
- [ ] Pass `SDL_GetTicks64()` from `Sdl2Input` when the second finger arrives.
- [ ] Re-run the filtered adapter tests and verify all pass.

### Task 3: Validate and deploy

**Files:**
- No additional source files.

- [ ] Run the full `TouchGestureAdapterTest` fixture and `./utility.sh --check-yaml`.
- [ ] Build `ios/OpenRA.iOS/OpenRA.iOS.csproj` in signed Release `ios-arm64` mode.
- [ ] Install the resulting `.app` on the connected iPad and launch an automatic skirmish.
- [ ] Confirm the app remains responsive and record any limitation that requires physical multi-touch verification.
