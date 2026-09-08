# Settings Classic Color Picker Design

## Goal

Replace the game settings Preferred Color editor with a fixed nine-colour classic palette.

## Scope

Only `DisplaySettingsLogic` changes its click handler. Multiplayer lobby, the introduction prompt, and the asset browser continue to use the existing full colour chooser.

## Behaviour

The settings picker presents the same first nine `PresetColors` used by skirmish. Each swatch shows the selected state. Tapping a swatch immediately updates `Game.Settings.Player.Color`, saves settings, and closes the panel. The picker has no mixer, random action, custom-colour history, or preview actor.

## Implementation

`ColorPickerManager` will expose a focused method for attaching a classic preset grid. It receives the existing dropdown, initial colour, and completion callback. The method reuses the existing `IOS_SKIRMISH_COLOR_GRID` widget and binds its nine swatches using `PresetColors.Take(9)`.

`DisplaySettingsLogic` calls that focused method. The existing `COLOR_CHOOSER` and its call sites remain unchanged.

## Verification

Add source and YAML structure tests proving the settings path invokes the focused picker, the grid contains exactly nine selectable preset swatches, and no mixer/custom controls are part of the focused grid. Run the focused tests, Release iOS build, signing verification, device installation, and device launch confirmation.
