# iOS Touch Viewport Controls Design

## Goal

Make map navigation reliable on iPad through two complementary controls: a permanent fixed joystick in the lower-left corner and three-finger direct map dragging. Both controls move only the camera and never select actors, issue orders, or activate menu widgets.

## Selected Layout

The approved layout is option A, **fixed lower-left joystick**:

- 112 logical-pixel circular touch target;
- placed inside the iOS safe area at the lower-left of the battlefield;
- semi-transparent base with a clearly visible movable thumb;
- 15% radial dead zone;
- thumb displacement controls direction and continuously scales camera speed;
- maximum thumb displacement is clamped to the joystick radius;
- releasing or cancelling the controlling finger immediately recenters the thumb and stops movement;
- the bottom command bar reserves the joystick width instead of rendering beneath it.

The joystick is visible only during an active match on iOS. It is hidden in menus, loading screens, replays without player controls, and non-iOS builds. It must remain above the battlefield interaction widgets so touches in its bounds cannot create selection boxes or orders.

## Three-Finger Map Drag

Three simultaneous fingers transition the touch adapter into an exclusive viewport-pan state. Entering this state releases any active one-finger selection drag or two-finger gesture before starting the pan. The centroid of the three fingers is the pointer position; each centroid delta becomes a middle-button-compatible standard map-pan event.

While three-finger pan is active:

- individual finger movement does not emit left, right, or scroll input;
- adding or removing a finger ends the current pan cleanly;
- remaining releases are ignored until all participating fingers are lifted, preventing accidental taps;
- focus loss, backgrounding, interruption, or cancellation emits one matching pan release and clears every tracked finger;
- movement follows the fingers directly, matching the existing standard viewport-scroll direction.

Two-finger pan/pinch remains supported for compatibility. Three-finger pan is the unambiguous camera-only gesture and takes priority over selection and commands.

## Architecture

`TouchGestureAdapter` expands from two to three tracked positions and exposes explicit begin/move/end transitions for a three-finger pan. It remains platform-neutral and unit tested. `Sdl2Input` tracks the third SDL finger id, routes its motion, and applies the existing interrupted-touch cleanup rules.

`VirtualViewportJoystickState` is a platform-neutral state object that computes normalized direction, dead-zone removal, clamped thumb position, and speed factor. `VirtualViewportJoystickWidget` owns rendering and input capture. It scrolls the active `WorldRenderer.Viewport` during ticks, using the existing viewport scroll-speed setting as the maximum-speed basis.

The RA2 in-game Chrome layout adds the joystick after world interaction and viewport widgets so reverse-order hit testing gives it priority. iOS visibility is determined by the engine platform flag; desktop Chrome appearance and input remain unchanged.

## Visual Treatment

The base uses the existing RA2 metallic UI language rather than a generic mobile-game asset: dark desaturated blue/steel, thin pale outer ring, restrained gold directional accents, and enough transparency to preserve map awareness. The thumb has stronger contrast than the base. It does not pulse continuously; only active touch adds a subtle highlight and directional indicator.

## iOS Product Exclusions

The map editor and asset browser are not part of the iOS product:

- their main-menu buttons are hidden or omitted on iOS;
- their Chrome layouts are excluded from the iOS-specific RA2 manifest/layout set;
- editor-only resources are not removed from shared RA2 rules when gameplay or map loading still references their traits;
- desktop and macOS builds retain both tools unchanged.

This is a platform packaging/UI exclusion, not deletion of shared engine editor code.

## Failure Handling

Any lost or unmatched joystick finger cancels the joystick instead of retaining movement. Gesture state transitions are idempotent. If the joystick cannot find an active world renderer, it draws nothing and consumes no input. Diagnostic breadcrumbs record gesture mode changes and joystick start/stop events without logging raw device identifiers.

## Verification

Automated tests cover joystick dead zone, radial clamp, proportional speed, release/cancel recentering, three-finger transition from an active selection drag, centroid deltas, partial release suppression, repeated cancellation, and preservation of one/two-finger behavior. Layout tests verify iOS joystick inclusion, touch-target size, command-bar clearance, and iOS editor/asset-browser exclusion while desktop manifests retain both tools.

Physical iPad testing verifies continuous eight-direction camera movement, variable speed, immediate stop, all map boundaries, no accidental actor selection/order, three-finger dragging in every direction, interruption/background recovery, coexistence with pinch zoom, and no loss of ordinary taps or drag-selection afterward.
