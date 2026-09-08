# iOS Responsive Menu System Design

**Date:** 2026-08-13  
**Status:** Approved visual direction; awaiting written-spec review  
**Product:** 红色警戒：阿富汗危机  
**Platforms:** iPhone and iPad, landscape only

## Objective

Replace the desktop-oriented RA2 menu surfaces on iOS with a complete touch-first menu system. The redesign must preserve the existing game features while making every player-facing action comfortable and unambiguous on both iPhone and iPad landscape screens.

Desktop and macOS layouts remain unchanged. The new geometry, navigation, and touch affordances apply only when the runtime platform is iOS.

## Approved Direction

Use the **responsive paged console** design:

- iPad uses two-column layouts where width permits, keeping lists or configuration on the left and previews or explanatory context on the right.
- iPhone uses compact landscape columns or tab-separated pages when the full iPad composition would make controls too small.
- Complex desktop tables become grouped cards and selection sheets.
- Primary and back actions remain fixed at the bottom of every full-screen page.
- Tabs remain fixed at the top of multi-section pages.
- No iOS menu relies on a narrow scrollbar, slider thumb, tiny drop-down arrow, hover state, or mouse tooltip.

The app remains landscape-only. The supported reference shapes are iPad 4:3 landscape and modern iPhone wide landscape, with all controls constrained to the system safe area.

## Visual Language

The iOS menus must look native to the game rather than like generic UIKit forms.

- Dark brushed-metal and armor-panel surfaces
- Rivets, clipped corners, restrained inset highlights, and tactical framing
- Red action highlights for destructive or primary combat actions
- Gold for labels and tactical information
- Green for ready, healthy, or successful states
- Faction colors for player identity and faction-specific state
- High-contrast text over quiet, low-saturation panel backgrounds
- Existing RA2 menu artwork and faction motifs reused where resolution and legibility permit

The treatment must remain readable; texture may not reduce text contrast or obscure control states.

## Touch and Layout Rules

1. Every interactive hit region is at least 44 points in both dimensions. Critical primary actions should be larger.
2. The whole visible card or field is tappable. A user never needs to hit a small arrow or checkbox glyph.
3. Pressed, selected, disabled, ready, unavailable, and destructive states are visually distinct without hover.
4. Page titles and back navigation remain visible.
5. Primary confirmation remains visible at the bottom and is separated from destructive actions.
6. Long content uses page navigation or explicit previous/next controls. Narrow draggable scrollbars are not interaction requirements.
7. Numeric values use `minus / current value / plus` step controls.
8. Enumerated values use large cards, segmented choices, or a selection sheet.
9. Boolean settings use full-width toggle rows with text state, not a small checkbox target.
10. Text entry opens the system keyboard, keeps the edited field visible, and provides explicit Done/Cancel controls.
11. Layout calculations respect left/right notches, rounded corners, the Home indicator, and the live safe-area inset.
12. iPhone may hide secondary descriptions to preserve target size, but it may not hide a feature or state.

## Shared Page Architecture

Every complex menu follows the same hierarchy:

1. Header: back affordance, page title, and optional compact status
2. Tabs: fixed section navigation when the screen contains multiple functional groups
3. Content: responsive one- or two-column composition
4. Footer: fixed secondary and primary actions
5. Selection sheet: modal or in-page large-card picker for values formerly represented by drop-downs

The shared component set includes armored buttons, tab buttons, list cards, player cards, selection cards, step controls, toggle rows, text-entry rows, map previews, state badges, alert dialogs, and fixed action bars.

## Menu Coverage

### Main and single-player menus

- Continue game
- Single-player menu
- Multiplayer menu
- Settings
- Content/help pages that remain part of the shipping iOS product
- Campaign and mission selection
- Skirmish creation
- Save loading
- Replay browsing and playback

The iOS product continues to exclude the map editor and asset browser, as previously approved.

### Campaign, mission, save, and replay browsers

- Campaign/faction category tabs
- Mission cards with title, progress, lock state, difficulty, briefing, preview, and score where available
- Save cards with timestamp, map, game state, and preview where available
- Replay cards with map, duration, players, version/status, filtering, details, and play action
- Explicit previous/next page controls for long collections

### Multiplayer browser and connection flow

- Server list and server detail
- Refresh
- Player count and status
- Compatibility/version status
- Filters
- Direct connect address and port
- Create server name, password, listen port, advertising state, map choice, and network notices
- Join, create, cancel, and back actions

### Skirmish and multiplayer lobby

The lobby must preserve every existing player and match operation. No field is removed to gain touch space.

The top-level sections are:

- Players
- Map
- Rules/options
- Communications/music for skirmish, and communications/server state where relevant to multiplayer

Each player or slot exposes:

- Player name and profile/latency state where applicable
- Slot operations: join, open, close, add AI, remove/kick when permitted, and spectator handling
- Color selection with large swatches and a visible explanation when a color conflicts or is unavailable
- Faction selection, including random and all factions/subfactions supplied by the mod
- Team selection, including no team and every supported team number
- Handicap selection using a step or large-choice control
- Spawn selection, including random and every available map spawn
- Ready/not-ready state
- Spectator state
- AI type, name, and difficulty where applicable

Lobby-level operations include:

- Add or configure player/AI slots
- Toggle and join spectators
- Slot management/reset options
- Position management
- Map preview and map replacement
- Spawn markers synchronized between selection UI and the map preview
- Complete match rules/options
- Music selection and playback controls
- Chat mode, chat history, and text entry
- Multiplayer server tab/state where applicable
- Start game, disconnect/back, ready, and spectate actions

On iPad, the player configuration and map preview can coexist. On iPhone, the same data is distributed across tabs or a compact map/player split, while player details open in a full safe-area selection sheet.

### Settings center

All existing settings categories and options remain accessible. The redesign applies the existing values and persistence behavior; it does not invent a second settings store.

- Display/video
- Sound and music
- Touch/control behavior
- Gameplay
- Advanced options that are valid for the iOS product

All sliders are removed from the iOS presentation. Continuous or percentage settings use step controls with suitable increments and a readable numeric value. Tabs and explicit page buttons replace scroll-dependent navigation. Reset, cancel, apply, and return behaviors remain available.

### In-game menus and information

- Resume
- Save and load
- Settings
- Mission objectives
- Diplomacy
- Statistics
- Surrender or exit, with confirmation
- Victory/defeat and post-game reports
- Diagnostic center

These surfaces appear above the paused or dimmed battlefield so the player retains context. Destructive exit actions are separated from Resume and require confirmation.

### Diagnostic center and common dialogs

- Runtime status
- Performance information
- Recent errors and logs
- Screenshot capture
- Combined diagnostic bundle generation/export
- Confirmations and warnings
- Error recovery dialogs
- Text input dialogs
- Large-card color, faction, team, spawn, difficulty, and other enum pickers

Dialogs remain within the landscape safe area and provide large explicit actions. Errors should explain whether the user can retry, continue with reduced functionality, return, or export diagnostics.

## Responsive Behavior

### iPad landscape

- Prefer two columns for list/detail, configuration/preview, or navigation/content.
- Preserve descriptive text and previews when space allows.
- Avoid excessive empty borders around a desktop-sized fixed panel; the menu should use the available safe area.

### iPhone landscape

- Preserve 44-point targets before preserving simultaneous information density.
- Use tabs or page switching when a row would otherwise become too dense.
- Keep primary actions reachable in a fixed bottom bar.
- Use full-width or half-width selection sheets for detailed player and setting choices.
- Reduce secondary explanatory copy, not controls, labels, current values, or feature access.

### Size-class boundary

The layout policy should derive from live safe-area content size rather than a hard-coded device model. Tests will cover representative iPad and wide-iPhone landscape sizes plus a constrained safe-area case.

## Architecture

Introduce an iOS-only responsive menu layout policy and reusable touch component policy at the RA2 Chrome/widget boundary.

- Existing menu logic remains the source of behavior and state.
- Existing widget IDs used by lobby, settings, save/load, mission, and multiplayer logic are preserved wherever practical.
- iOS-specific Chrome templates provide the new hierarchy and geometry.
- Shared iOS layout code selects iPad-style or compact landscape composition from the live safe-area size.
- Desktop YAML and logic remain operational and visually unchanged.
- Selection sheets adapt existing drop-down option providers rather than duplicating faction, color, team, spawn, or rule data.
- Step controls adapt existing numeric setting bindings rather than maintaining parallel values.

If a legacy logic class assumes fixed desktop coordinates or direct child placement, add the smallest platform-aware adapter at that boundary instead of duplicating the entire logic class.

## State, Input, and Error Handling

- Opening and closing a page must preserve unsaved values according to the existing menu's cancel/apply semantics.
- Rotation is unsupported; an unexpected orientation or safe-area change triggers a layout recomputation without losing selection state.
- A modal selection sheet captures input until it closes and must not leak taps to the page behind it.
- Keyboard presentation shifts or pages content so the active text field and Done/Cancel actions remain visible.
- Disabled choices remain visible with an explanation when the original menu communicates a restriction.
- Missing optional content produces a large recoverable error state rather than an empty or tiny desktop dialog.

## Accessibility and Localization

- Chinese is the primary shipping language, but layouts allow for longer localized strings.
- Text may wrap to two lines in cards where necessary; it must not be clipped to preserve a fixed desktop width.
- Color is never the only indication of readiness, conflict, selection, or error.
- Dynamic font scaling is bounded to preserve the game layout, while the default iOS presentation remains comfortably readable.

## Verification Strategy

### Automated policy checks

- iOS-only activation; desktop layouts remain unchanged
- Minimum hit-region dimensions
- Representative iPad and iPhone safe-area layouts remain within bounds
- Fixed headers, tabs, and footers do not overlap content
- Numeric controls update the same settings values as the legacy sliders
- Selection sheets expose the same option sets as legacy drop-downs
- Modal input does not leak to widgets behind it
- Every mapped legacy menu action has an iOS-visible control

### Functional menu inventory tests

Create a source- and runtime-level inventory for all player-facing Chrome screens. Each screen records its desktop actions and corresponding iOS presentation. The lobby inventory explicitly verifies name, color, faction, team, handicap, spawn, ready, spectator, AI, position, map, options, music, chat, server, start, and disconnect operations.

### Device smoke matrix

On physical iPad and iPhone landscape devices where available:

- Launch and main navigation
- Campaign/mission selection
- Skirmish creation with every player field changed
- Multiplayer browser, direct connect, and server creation
- Save/load and replay lists
- Every settings tab and step control
- Pause, objectives, diplomacy, statistics, and exit confirmation
- Keyboard entry and dismissal
- Diagnostic screenshot/log export
- Safe-area inspection at supported resolutions
- Fifteen-minute interaction/stability pass with no frozen input

The iPad is the required physical-device gate for the first implementation. iPhone layouts also require automated geometry tests and a simulator/device visual check before completion.

## Delivery Boundaries

- This work redesigns player-facing menus; it does not redesign the in-game HUD already handled by the joystick/command-bar work.
- It does not reintroduce the map editor or asset browser on iOS.
- It does not change match rules, lobby protocol, save formats, or desktop behavior.
- Implementation may be delivered in reviewable menu-family phases, but the feature is not considered complete until the full inventory passes and the signed app is installed and smoke-tested.

## Completion Criteria

The redesign is complete when every shipping iOS menu has a landscape touch presentation, no required action depends on a sub-44-point target or slider, the lobby feature inventory has no omissions, iPad and iPhone layouts remain inside their safe areas, desktop behavior is unchanged, automated checks pass, and the signed build passes the physical-device smoke gate.
