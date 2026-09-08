# iOS Two-Finger Gesture Design

## Goal

Add two iOS gestures without disturbing one-finger selection, selection boxes,
the virtual joystick, three-finger camera dragging, or two-finger pinch zoom:

- A short two-finger tap produces one right-click at the fingers' centroid.
- Holding two fingers for 450 ms and then moving both in the same direction pans
  the viewport using their centroid movement.

## Gesture state

The touch adapter owns the gesture arbitration. Adding the second finger cancels
any pending one-finger tap or selection drag, records both initial positions and
the second-finger timestamp, and does not emit a mouse button immediately.

A two-finger gesture remains a tap candidate while both fingers stay within the
movement deadzone and the long-press delay has not elapsed. Releasing either
finger then emits right-button down and up at the current centroid.

After the long-press delay, camera dragging starts only when both fingers have
moved beyond the deadzone and their displacement vectors point in substantially
the same direction. The adapter emits middle-button down followed by incremental
middle-button movement using centroid deltas, then middle-button up on release.
Movement before the delay invalidates the tap but does not start camera dragging.

Opposing two-finger motion continues to emit the existing wheel-compatible pinch
zoom. Adding a third finger cancels any pending two-finger interpretation and
starts the existing three-finger centroid pan. Focus loss and interruption release
any active synthetic mouse button and discard pending taps.

## Direction rule

Both fingers must exceed the 8-pixel drag deadzone. Their displacement vectors
must have a positive dot product and cosine similarity of at least 0.7. This
accepts natural hand jitter while rejecting pinch and crossing motions.

## Tests

Adapter tests cover short tap right-click, jitter tolerance, held stationary
release, early movement rejection, same-direction long-press pan, opposite-motion
pinch, active-pan release/cancellation, and third-finger takeover. Existing
one-finger and three-finger tests remain green.
