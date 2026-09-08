# iPhone Support Powers Beside Production Sidebar Design

## Goal

On compact iPhone layouts, place every player support-power icon (including paradrops, nuclear missiles, Iron Curtain, Chronosphere, Weather Control, Genetic Mutator, and Psychic Dominator) immediately outside the left edge of the production sidebar. No icon may be drawn under, or have its input intercepted by, any part of the right sidebar.

iPad and desktop layouts must remain unchanged.

## Root Cause

The compact iPhone policy currently packs support powers from the global bottom-right corner. `SupportPowerBinLogic` reports only the declared `SIDEBAR_PRODUCTION` parent rectangle as the production obstacle.

The rendered right sidebar is larger than that rectangle:

- `SIDEBAR_BACKGROUND_TOP` covers the upper-right region.
- `ClassicProductionLogic` creates four to six background rows plus a bottom cap.
- The production scroll buttons are positioned outside the declared parent height.
- The right sidebar is drawn after the support-power container, so any overlap hides the icon and intercepts its touch.

The layout tests use synthetic obstacles and do not model these real rendered bounds, so the overlap was not detected.

## Considered Approaches

### Change the YAML support-power container coordinates

Rejected. iOS provides absolute screen-space cells through `SupportPowersWidget.CellProvider`, so the YAML container position is not the active layout source.

### Raise support powers above the sidebar or only enlarge the obstacle

Rejected. Drawing icons over the production UI would preserve the touch conflict and violate the requested placement. Enlarging only the obstacle would prevent some overlaps but would not guarantee that all icons stay beside the production edge.

### Anchor compact support powers to the full sidebar edge

Selected. It directly expresses the requested invariant and keeps the existing iOS safety policies.

## Layout Contract

When all of these are true:

- iOS support-power layout is enabled;
- the screen is a compact phone; and
- the right production sidebar is visible;

the layout must:

1. Derive one authenticated production obstacle from the union of `SIDEBAR_BACKGROUND_TOP`, `SIDEBAR_PRODUCTION`, and their visible descendant render bounds.
2. Set the support-power strip's right boundary to `production.Left - margin`, clamped inside the inset safe bounds.
3. Put the first available cell in the rightmost column, starting at the bottom and moving upward.
4. Add further columns only to the left when one column is full or blocked.
5. Reject cells intersecting the expanded production sidebar, command bar, viewport joystick, or viewport action buttons.
6. Keep every cell within the safe-area placement bounds and preserve the existing minimum touch target, icon gap, centered artwork, hit testing, and click-through gaps.
7. Return no partial override if all icons cannot fit.

If the production sidebar is absent or hidden, compact phones retain the existing generic bottom-right safe packing. iPad retains its safe top-left packing. Desktop retains its original YAML layout.

## Runtime Behavior

All player support powers already share `SupportPowers@SUPPORT_PALETTE`. The change is therefore layout-only:

- support-power ordering remains `SupportPowerPaletteOrder`;
- the first six hotkeys remain bound by icon index;
- tooltips follow the moved hit rectangles;
- clicking an icon still calls the same `Power.Target()` path;
- world targeting, charge timers, and support-power rules are unchanged;
- `SUPPORT_POWER_TIMER` and observer statistics are outside this scope.

The layout signature already includes obstacle geometry and visibility, so building, destroying, hiding, or resizing sidebar UI continues to trigger one deterministic refresh.

## Test Strategy

Add RED tests before production changes using the actual RA2 sidebar geometry and representative iPhone snapshots:

- 844×390 points;
- 932×430 points;
- 1558×720 effective pixels backed by an 844×390 native point surface;
- icon counts 0, 1, 2, 3, 6, 7, and 12.

The tests must prove:

- every compact-phone icon ends at or before `production.Left - margin`;
- the first column is flush with that boundary;
- icons flow bottom-to-top, then into columns further left;
- no cell overlaps the sidebar, command bar, joystick, actions, another cell, or the unsafe area;
- hidden production uses the existing fallback;
- iPad and desktop behavior remains unchanged;
- runtime wiring includes both right-sidebar widget IDs and uses visible descendant render bounds.

Verification includes the focused NUnit tests, the full OpenRA test project, RA2 YAML validation, a Release build, and the strict engine patch-delivery checks required by this repository.

## Expected Files

- `engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IosSupportPowerLayoutPolicy.cs`
- `engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/SupportPowerBinLogic.cs`
- `engine/OpenRA.Test/IosSupportPowerLayoutPolicyTest.cs`
- the corresponding repository engine patch and verifier metadata required by the engine patch workflow

No RA2 support-power rule, weapon, icon, or production YAML coordinate changes are required.
