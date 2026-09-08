# iPad Touch Settings Menu Design

## Goal

Replace the desktop-oriented settings shell on iOS with a landscape iPad layout that is comfortable to operate by touch, while leaving desktop behavior unchanged.

## Approved direction

Use direction A: a persistent left category sidebar and a scrollable content panel. The user requested a wider sidebar, so it occupies 30% of the 1000-unit dialog (280 units inside the margins). Category buttons are 56 units high with 12-unit gaps. The bottom actions are 52 units high.

## iOS behavior

- The dialog uses almost all of the iPad landscape viewport, capped at 1180 × 720 logical units.
- The left sidebar contains large category targets. Existing categories remain wired to their current settings logic so every control continues to work.
- Content begins to the right of the wider sidebar and scrolls vertically.
- All settings content is vertically expanded by 1.45× on iOS. Rows, labels, checkboxes, dropdowns, sliders, and text fields retain their relationships but gain larger touch targets and spacing.
- Reset and Done are large, separated bottom actions. Changes continue to save when switching category and when closing, matching current behavior.
- Desktop and macOS retain the existing YAML dimensions and spacing.

## Categories

The first implementation keeps the five functional panels—Display, Sound, Input/Touch, Hotkeys, and Advanced—to avoid dropping working controls. On iOS the Input label is presented as Touch Controls. Desktop-only controls remain available only where their existing logic requires them; a later content pass may replace them with dedicated gesture preferences after those preferences exist in the engine.

## Testing

Unit-test the platform layout policy and geometry independently from the UI renderer. Then run the complete engine test suite, build the signed `ios-arm64` Release app, install it on the paired iPad, and verify successful launch.
