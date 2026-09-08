# iPad Command Input and Debug Overlay Fix Design

## Goal

Make the iPad controls behave like a touch-native classic RTS interface:

- A short tap selects a unit when no compatible unit command should be issued.
- After a unit is selected, a short tap on terrain issues its normal action, such as move.
- A short tap on a selected deployable unit issues its self-targeted action, such as MCV deployment.
- Long press remains the secondary/cancel gesture.
- Terrain geometry debug lines are not visible during normal iPad gameplay.
- Touch input recovers after focus, visibility, or incomplete multi-touch
  transitions instead of leaving the game on an unresponsive final frame.
- A lightweight iOS-only diagnostic trail distinguishes input-state recovery
  from application lifecycle suspension if the symptom recurs.

## Root Cause

The touch adapter currently emits a left mouse click for every short tap. The iPad
settings use OpenRA's default modern mouse mode, where the action button is the
right mouse button. This allows selection but prevents short taps from issuing
movement or self-targeted deployment orders.

The diamond-shaped lines in the captured screen are produced by
`TerrainGeometryOverlay`. This developer visualization must be disabled for normal
iPad gameplay, regardless of accidental activation in the current session.

After the reported unresponsive state, the OpenRA process was no longer present.
The local client log contained `Arg_EndOfStreamException`, but iPadOS produced no
matching crash or jetsam report. The current touch state machine can retain ignored
or pressed-pointer state when a finger-up transition is lost. Lifecycle transitions
must therefore reset the complete synthetic pointer state, and diagnostics must
record when this recovery occurs.

## Design

On iOS, force OpenRA's existing classic mouse interaction mode after settings are
loaded. This reuses the engine's established selection-versus-order resolution:
left click selects when appropriate, but issues an action when the current
selection can act on the tapped target. The touch adapter can therefore continue
to emit a left click for a short tap without adding game-state knowledge to the
platform input layer.

When an iOS world is loaded, initialize `TerrainGeometryOverlay.Enabled` to false.
The debug menu may still explicitly enable it later in the same session, preserving
developer diagnostics while guaranteeing a clean normal startup.

Centralize touch cancellation in `Sdl2Input`. On iOS focus loss, window hiding,
minimization, or application background transitions, release any synthetic left,
right, or middle button, cancel the gesture adapter, and clear primary, secondary,
and ignored-finger bookkeeping. On focus/show/restore, accept the next finger as a
fresh gesture. The reset must be idempotent so duplicate lifecycle events are safe.

Write concise iOS-only diagnostic messages when the reset path repairs active or
ignored touch state. Do not log every motion event. Existing OpenRA logs then retain
enough evidence to diagnose a recurrence without creating frame-time or storage
pressure.

No desktop mouse behavior or non-iOS debug behavior changes.

## Testing

- Add a focused test proving the iOS input-policy decision selects classic mouse
  semantics while non-iOS behavior preserves the configured value.
- Add a focused test proving the iOS terrain-overlay startup policy disables the
  overlay while non-iOS startup leaves it unchanged.
- Add state-machine tests proving cancellation releases an active drag or two-finger
  gesture, clears ignored-finger state, and permits a new tap immediately.
- Add a test proving repeated lifecycle resets do not emit stuck-button state.
- Run the relevant OpenRA test project.
- Build and sign the `ios-arm64` Release app.
- Install on the paired iPad and verify:
  1. tap MCV to select;
  2. tap terrain to move;
  3. tap the selected MCV to deploy;
  4. start a new match with no terrain geometry grid;
  5. perform a two-finger gesture, background and restore the app, then tap again;
  6. application remains responsive and running without fatal errors.

## Scope

The change is limited to iOS input defaults, iOS debug-overlay initialization,
touch lifecycle recovery, focused diagnostics, their regression tests, and the
resulting signed iPad build. Existing unrelated working-tree changes are preserved
and excluded from the design commit.
