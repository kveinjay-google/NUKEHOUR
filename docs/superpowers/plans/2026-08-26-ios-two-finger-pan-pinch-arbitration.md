# iOS Two-Finger Pan and Pinch Arbitration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the delayed two-finger pan with immediate, stable pan/pinch classification while preserving two-finger right-click and three-finger compatibility.

**Architecture:** Keep SDL finger identity and dispatch in `Sdl2Input`, and keep all gesture decisions in the platform-neutral `TouchGestureAdapter`. Add an explicit pending/pan/pinch mode, classify cumulative centroid and separation evidence after a 32 ms one-finger arbitration window, and lock the selected mode until release. Capture the engine delta as a new patch that applies after the integrated iOS runtime patch.

**Tech Stack:** C#/.NET 8, SDL2 finger events, NUnit, OpenRA mouse/viewport input, POSIX patch series, .NET iOS arm64 signing.

---

### Task 1: Lock the new gesture contract with failing adapter tests

**Files:**
- Modify: `engine/OpenRA.Test/TouchGestureAdapterTest.cs`

- [ ] **Step 1: Replace the delayed-pan expectation with immediate pan tests**

Add tests that begin at `(100,100)` and `(200,100)`, move the first finger to
`(120,100)` at 1010 ms and the second to `(220,100)` at 1020 ms, and assert:

```csharp
Assert.That(firstMove.Count, Is.Zero);
Assert.That(secondMove.Count, Is.EqualTo(2));
Assert.That(secondMove.First.Type, Is.EqualTo(TouchPointerActionType.MiddleDown));
Assert.That(secondMove.Second.Type, Is.EqualTo(TouchPointerActionType.MiddleMove));
Assert.That(secondMove.Second.Delta, Is.EqualTo(new int2(20, 0)));
Assert.That(secondMove.First.Type, Is.Not.EqualTo(TouchPointerActionType.Scroll));
Assert.That(secondMove.Second.Type, Is.Not.EqualTo(TouchPointerActionType.Scroll));
```

Do not call `Poll(1450)`. Keep the existing short-tap and stationary-timeout
tests unchanged.

- [ ] **Step 2: Add anchored and opposing pinch tests**

For anchored pinch, move only the second finger from `(200,100)` to `(220,100)`
at 1010 ms, assert no immediate action, then call `Poll(1042)` and require one
positive `Scroll` at centroid `(160,100)`. For symmetric pinch, move the first
finger to `(90,100)` and the second to `(210,100)` and require a single positive
`Scroll` without any `MiddleDown`.

- [ ] **Step 3: Add lock, rotation, and cadence tests**

Add deterministic cases proving:

```text
PanLocked + later separation change -> no Scroll
PinchLocked + later equal translation -> no MiddleDown/MiddleMove
90-degree rotation at a stable centroid and separation -> no action
30 Hz, 60 Hz, and 120 Hz sequential same-direction samples -> PanLocked
End/Cancel after pan -> exactly one MiddleUp
```

The cadence test uses millisecond timestamps and keeps the second matching
finger event inside the 32 ms window for every sampling rate.

- [ ] **Step 4: Run the focused test and confirm RED**

Run:

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore \
  --filter FullyQualifiedName~TouchGestureAdapterTest -v minimal
```

Expected: the immediate same-direction test fails because no pan is emitted;
the anchored test fails because current code scrolls immediately; lock tests
fail because the current implementation has no pinch mode.

### Task 2: Implement explicit pending, pan, and pinch modes

**Files:**
- Modify: `engine/OpenRA.Game/Input/TouchGestureAdapter.cs`
- Test: `engine/OpenRA.Test/TouchGestureAdapterTest.cs`

- [ ] **Step 1: Add the mode and arbitration state**

Add these private members to `TouchGestureAdapter`:

```csharp
enum TwoFingerMode
{
	None,
	Pending,
	Pan,
	Pinch
}

const ulong TwoFingerArbitrationDelay = 32;
const double GestureDominanceRatio = 1.6;

TwoFingerMode twoFingerMode;
bool firstFingerMoved;
bool secondFingerMoved;
bool arbitrationWindowStarted;
ulong arbitrationWindowStart;
```

Set `twoFingerMode = TwoFingerMode.Pending` in `BeginSecond`, and reset every
field in `Begin`, `EndTwoFinger`, `BeginThird`, and `Cancel`.

- [ ] **Step 2: Pass timestamps into two-finger movement**

Keep compatibility overloads and add timestamp-aware overloads:

```csharp
public TouchGestureResult MovePrimary(int2 position)
{
	return MovePrimary(position, twoFingerStartTime);
}

public TouchGestureResult MovePrimary(int2 position, ulong timestamp)
{
	if (!twoFinger)
		return TouchGestureResult.Empty;

	lastPosition = position;
	return TwoFingerMove(timestamp);
}
```

Apply the same shape to `MoveSecond`.

- [ ] **Step 3: Implement cumulative evidence classification**

Use the initial two-finger anchors and current positions:

```csharp
var centroid = Centroid(lastPosition, secondPosition);
var panEvidence = Distance(centroid, Centroid(firstTwoFingerOrigin, secondTwoFingerOrigin));
var pinchEvidence = Math.Abs(Distance(lastPosition, secondPosition) -
	Distance(firstTwoFingerOrigin, secondTwoFingerOrigin));
```

Pan requires both fingers beyond the 8-point dead zone, the existing
`SameDirection` rule, `panEvidence >= DragThreshold`, and
`panEvidence >= GestureDominanceRatio * pinchEvidence`. Pinch requires
`pinchEvidence >= PinchThreshold` and
`pinchEvidence >= GestureDominanceRatio * panEvidence`.

When exactly one finger first crosses the dead zone, record the movement
timestamp and remain pending. Permit anchored-pinch classification only after
`timestamp - arbitrationWindowStart >= TwoFingerArbitrationDelay`. If both
fingers cross the dead zone first, classify immediately.

- [ ] **Step 4: Dispatch only the locked mode**

For pan, emit `MiddleDown` plus the accumulated centroid `MiddleMove` once,
then only non-zero `MiddleMove` events. For pinch, reuse the existing
`lastZoomDistance` accumulator and emit only `Scroll`. Do not evaluate the
other classifier after a mode locks.

`Poll(timestamp)` continues to expire right-click eligibility at 450 ms and,
while pending, asks the classifier to resolve an elapsed anchored-pinch window.

- [ ] **Step 5: Run focused tests until GREEN**

Run the Task 1 command. Expected: every `TouchGestureAdapterTest` passes with no
new analyzer error.

### Task 3: Wire SDL timestamps and protect the event pipeline

**Files:**
- Modify: `engine/OpenRA.Platforms.Default/Sdl2Input.cs`
- Modify: `engine/OpenRA.Test/TouchGestureAdapterTest.cs`

- [ ] **Step 1: Pass one timestamp per SDL finger motion event**

In the two-finger `SDL_FINGERMOTION` branch, capture the timestamp once and use
the new overloads:

```csharp
var timestamp = SDL.SDL_GetTicks64();
if (e.tfinger.fingerId == touchFingerId)
	DispatchTouch(touchGestures.MovePrimary(pos, timestamp), inputHandler, mods);
else if (e.tfinger.fingerId == secondTouchFingerId)
	DispatchTouch(touchGestures.MoveSecond(pos, timestamp), inputHandler, mods);
```

Do not change one-finger, three-finger, external-mouse, finger-up, or reset
branches.

- [ ] **Step 2: Run adjacent input regression tests**

Run:

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore \
  --filter 'FullyQualifiedName~TouchGestureAdapterTest|FullyQualifiedName~TouchMouseEventFilterTest|FullyQualifiedName~IosViewportActionLayoutTest' \
  -v minimal
```

Expected: all selected tests pass.

### Task 4: Capture a replayable engine patch

**Files:**
- Create: `engine-patches/0009-ios-two-finger-gesture-arbitration.patch`
- Modify: `engine-patches/README.md`
- Modify: `docs/superpowers/specs/2026-08-26-ios-two-finger-pan-pinch-arbitration-design.md`

- [ ] **Step 1: Generate the patch from the pre-feature commit**

Create a temporary `a/` and `b/` tree with `mktemp -d`. Populate `a/` using
`git show ffbbc7e:engine/<path>` and `b/` from the current three engine files,
then run `diff -ruN a b` into a temporary patch and move it to
`engine-patches/0009-ios-two-finger-gesture-arbitration.patch`.

The patch must contain exactly:

```text
OpenRA.Game/Input/TouchGestureAdapter.cs
OpenRA.Platforms.Default/Sdl2Input.cs
OpenRA.Test/TouchGestureAdapterTest.cs
```

- [ ] **Step 2: Document patch 0009**

Append one README series row describing immediate two-finger pan/pinch
arbitration and confirm the design spec names patch 0009.

- [ ] **Step 3: Verify patch replay**

Build another temporary tree from the same three files at commit `ffbbc7e`, run:

```sh
patch -d "$VERIFY_TREE" -p1 --forward --batch \
  < engine-patches/0009-ios-two-finger-gesture-arbitration.patch
```

Compare all three patched files byte-for-byte with the working engine files.
Expected: patch exits 0 and all comparisons match.

### Task 5: Review and verify the implementation

**Files:**
- Review all files from Tasks 1-4

- [ ] **Step 1: Run focused and adjacent NUnit suites**

Run both exact commands from Tasks 1 and 3 and record totals.

- [ ] **Step 2: Build affected managed projects**

Run:

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Game/OpenRA.Game.csproj -c Release --no-restore --nologo
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Platforms.Default/OpenRA.Platforms.Default.csproj \
  -c Release --no-restore --nologo
```

Expected: 0 errors. Existing repository warnings may remain documented.

- [ ] **Step 3: Run repository hygiene gates**

Run `git diff --check`, confirm no unexpected tracked files changed, and verify
the new patch has only the three intended engine paths.

- [ ] **Step 4: Request independent spec and quality reviews**

The spec review checks every approved behavior and patch reproducibility. The
quality review checks arithmetic overflow, SDL event ordering, balanced button
events, cancellation, mode lock, and test mutation resistance. Fix every
Critical or Important finding with a new RED-to-GREEN cycle.

### Task 6: Commit, sign, install, and verify on iPad Air

**Files:**
- Commit the reviewed source, tests, patch, README, plan, and spec correction

- [ ] **Step 1: Show and commit the exact staged list**

Stage only the files named in this plan, print `git diff --cached --name-status`,
then commit with message:

```text
Improve iOS two-finger pan and pinch gestures
```

- [ ] **Step 2: Build and sign iOS arm64 Release**

Use team `267KQ4A3N4`, the active Apple Development identity, and provisioning
profile `ac93a0de-2e3e-408b-8c3a-d69579bc6719`. Expected: 0 errors, loading
brand audit passes, touch-runtime bundle audit passes, and strict `codesign`
verification passes.

- [ ] **Step 3: Cover-install on the physical iPad Air**

Install the signed app with `xcrun devicectl device install app` to CoreDevice
`C5CA8947-3880-593B-84D6-B57DE642A8FD` without uninstalling the old app. Query
`com.openra.ipad.personal` afterward and confirm the new application container.

- [ ] **Step 4: Launch and record the manual acceptance checklist**

Launch the installed app and confirm it remains running long enough to reach
the game UI. Report these physical interactions as pending user checks rather
than claiming injected-device evidence:

```text
two-finger short tap -> right-click
two fingers same direction -> pan without zoom
opposing or anchored pinch -> zoom without pan
lift and retry -> mode can change on the next gesture
ordinary one-finger selection still works
```
