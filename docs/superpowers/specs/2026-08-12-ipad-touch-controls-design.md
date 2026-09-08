# OpenRA iPad Touch Controls Design

## Goal

Keep OpenRA's game logic and most existing menus, while adding an iPad-specific
gesture adapter and a small touch-oriented in-game command HUD. Mouse and
keyboard behavior on macOS must remain unchanged.

## Input architecture

SDL normalized finger events feed a platform-independent gesture state machine.
The state machine emits ordinary OpenRA mouse inputs, so widgets, selection,
order generators, and replay behavior continue to use existing engine paths.

The first finger owns pointer interaction. A short stationary tap emits a left
click. Movement beyond the drag threshold emits left-button drag events for
selection boxes and sliders. A stationary long press emits a right click. Two
fingers suppress click generation and emit viewport pan/zoom gestures in a later
increment.

Coordinates are always calculated in OpenRA's effective logical resolution,
not Retina drawable pixels. Gesture thresholds are measured in logical points
and remain stable across iPad models.

## Delivery increments

1. Extract and test coordinate conversion and single-finger gesture decisions.
2. Validate tap and drag against the existing full-screen menu.
3. Add long-press right-click behavior and cancellation rules.
4. Add two-finger pan and pinch zoom.
5. Add a compact in-game command HUD only for mouse/keyboard actions that are
   awkward on touch: attack-move, stop, deploy, repair, and control groups.

## Interaction rules

- Tap: left click at release position.
- Drag: left down after crossing an 8-point threshold, move while held, then up.
- Long press: right click after 450 ms if movement stays within 8 points.
- Second finger: cancels pending tap/long press before two-finger processing.
- System gestures and touches outside the SDL scene produce no OpenRA command.

## Testing

Pure coordinate and gesture state behavior is covered by NUnit tests in
`OpenRA.Test`. Simulator acceptance tests verify full-screen menu navigation,
dragging a menu slider, and later a skirmish selection/order flow. Both simulator
and unsigned `ios-arm64` builds must link before each phase commit.

