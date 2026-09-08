# iOS Two-Finger Pan and Pinch Arbitration Design

## Goal

Let players navigate the battlefield with two fingers without requiring the
three-finger drag:

- a short stationary two-finger tap remains a right-click;
- two fingers translating together pan the viewport immediately;
- opposing movement, or a natural anchored pinch where one finger moves while
  the other stays nearly still, zooms the viewport;
- one touch gesture is locked to either pan or pinch until a participating
  finger is lifted.

The existing one-finger selection, long-press command, virtual joystick,
external mouse, and three-finger compatibility paths must remain unchanged.

## Gesture ownership and states

`TouchGestureAdapter` remains the platform-neutral owner of gesture
arbitration. `Sdl2Input` continues to own SDL finger identity, timestamps,
coordinate conversion, dispatch, interruption cleanup, and suppression of the
remaining finger release after a multi-touch gesture ends.

The two-finger state is explicit:

```text
None -> Pending -> PanLocked
                -> PinchLocked
```

Adding the second finger cancels any active one-finger selection drag, records
both positions, their centroid, their separation, and the second-finger
timestamp, then enters `Pending` without emitting a pointer button.

`PanLocked` emits one matching middle-compatible down/move/up stream using the
two-finger centroid. `PinchLocked` emits only wheel-compatible scroll actions at
the centroid. A locked mode never changes until the gesture ends. Adding a
third finger keeps the existing clean handoff to the three-finger centroid-pan
path.

## Pending classification

The adapter compares cumulative motion from the two-finger anchors instead of
classifying an individual SDL motion event. This is required because SDL
reports the two fingers sequentially even when the user moves them together.

The existing thresholds remain the physical basis:

- drag dead zone: 8 logical points;
- pinch distance threshold: 12 logical points;
- short-tap timeout: 450 ms.

If only one finger has crossed the dead zone, the adapter waits for an
arbitration window of at most 32 ms before deciding. A matching second-finger
event inside that window can establish translation; if the other finger remains
anchored and separation change dominates, the gesture can become a pinch after
the window. The delay is short enough to feel immediate while preventing a
sequential same-direction pair from producing a false zoom step.

For current positions `p1` and `p2` and anchors `a1` and `a2`:

```text
panEvidence   = distance((p1 + p2) / 2, (a1 + a2) / 2)
pinchEvidence = abs(distance(p1, p2) - distance(a1, a2))
```

The classifier uses a 1.6 dominance ratio:

- pan requires both fingers beyond the dead zone, substantially aligned
  displacement vectors, `panEvidence >= 8`, and pan evidence at least 1.6
  times pinch evidence;
- pinch requires `pinchEvidence >= 12` and pinch evidence at least 1.6 times
  pan evidence;
- otherwise the gesture remains pending and emits nothing.

This makes symmetric opposing motion a clear pinch and same-direction motion a
clear pan. With one anchored finger, separation changes twice as fast as the
centroid moves, so a deliberate anchored pinch also qualifies. Pure rotation
changes neither centroid nor separation enough to qualify and is ignored.

Any movement beyond the 8-point dead zone permanently removes right-click
eligibility, even if the gesture remains ambiguous. Releasing both fingers
inside the dead zone before the 450 ms timeout emits exactly one right-button
down/up pair at the current centroid. A stationary hold beyond the timeout does
not produce a right-click.

## Dispatch and cleanup

Pan begins with a middle-compatible down at the original centroid followed by
the accumulated centroid delta. Later motion emits non-zero centroid deltas
only. Ending or cancelling pan emits exactly one matching up.

Pinch reuses the existing 12-point wheel-step accumulator and zoom modifier.
The viewport therefore continues to zoom around the current two-finger
centroid. Translation after pinch lock cannot start a pan, and separation
change after pan lock cannot emit scroll.

The existing cancellation path remains authoritative. Finger loss, focus loss,
window hiding, app suspension, or input suppression clears all arbitration
state and releases an active pan exactly once. The remaining physical finger
release is swallowed as it is today, preventing accidental selections or
commands.

## Implementation boundary

Production changes are limited to:

- `engine/OpenRA.Game/Input/TouchGestureAdapter.cs` for the explicit mode,
  evidence classifier, arbitration timer, and locked dispatch;
- `engine/OpenRA.Platforms.Default/Sdl2Input.cs` to pass SDL timestamps with
  two-finger motion;
- a new `engine-patches/0009-ios-two-finger-gesture-arbitration.patch` so the
  change remains isolated and a fresh engine checkout reproduces the same
  runtime behavior after the existing integrated iOS patch.

Tests are centered in `engine/OpenRA.Test/TouchGestureAdapterTest.cs`. No UIKit
gesture recognizer, game rule, Chrome layout, save format, or network protocol
change is required.

## Verification

Automated tests must prove:

- short two-finger tap still emits one right-click;
- a 450 ms stationary hold still emits no right-click;
- sequential same-direction motion starts pan without `Poll(450)` and never
  emits scroll;
- symmetric opposing motion starts pinch and never emits middle-button input;
- an anchored pinch waits through the 32 ms arbitration window and then zooms;
- pan and pinch remain locked until release;
- pure rotation and balanced ambiguous motion emit neither action;
- pan end, cancellation, focus-reset, and third-finger handoff have balanced
  down/up events;
- one-finger, virtual joystick, external mouse, and existing three-finger tests
  remain green;
- representative 30, 60, and 120 Hz event sequences reach the same mode.

Final verification includes focused NUnit tests, adjacent iOS input tests,
Release builds for the affected managed projects, the repository engine-patch
replay/consistency check, strict iOS arm64 signing, and installation plus launch
verification on the physical iPad Air. Physical right-click, pan, pinch,
interruption, and subsequent ordinary-selection gestures remain an explicit
manual acceptance checkpoint because the repository has no trusted mechanism
for injecting multi-touch events into a physical device.
