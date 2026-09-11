# iOS TestFlight Import Startup Recovery Design

## Problem

The historical TestFlight build 1.0 (5) can fail immediately after a successful retail-content import, then reopen into a wallpaper-only main menu where audio and touch hit-testing still work. That old build is diagnostic evidence only; the fix and replacement release are produced from the current latest source. The current iOS PublicClean allowlist also omits presentation resources that its manifests reference, including the NUKE HOUR wallpapers, RA2 UI atlases, and RA2-configured fonts. Existing device diagnostics show the resulting failure class: a required UI sprite is missing while the render path is drawing widgets.

## Scope

This change fixes the iPhone and iPad PublicClean application. It does not add retail MIX archives, retail maps, retail music, or other official game assets to the IPA. It may package project-owned artwork, GPL community UI assets, OFL fonts, and GPL engine shaders that the application itself requires before or after user import.

## Design

### Public presentation contract

The iOS project will package the same reviewed non-retail presentation files already admitted by the macOS PublicClean manifest: engine version/shader data, RA2 fonts, RA2 UI atlases, touch-control atlases, NUKE HOUR wallpapers, and project-specific non-retail sprite additions. Maps remain excluded. The shared iOS audit manifest will classify these exact files by hash and license, and a contract test will fail if a referenced required presentation file is absent from the PublicClean MSBuild allowlist.

### Native-to-game handoff

Successful import remains on a ready screen. Entering the game is a separate action. `ViewController` owns the transition: dismiss any presented picker or alert, remove the content wizard, restore the root view to a stable laid-out state, and schedule the engine launch on the next main-loop turn. This prevents SDL from creating its iOS window while UIKit is still completing a modal dismissal.

### Launch preflight and recovery

Before `Game.InitializeAndRun`, a pure preflight validates the non-retail bundle contract. Missing paths are logged as one deterministic diagnostic and startup stays in the native host instead of partially initializing audio/input/rendering. Any later startup exception returns the application window to the foreground, removes stale overlays, unhides the native error label, and records the raw exception only in diagnostics.

### Verification

Regression tests cover the explicit launch gate, handoff ordering, error recovery, and PublicClean presentation closure. The signed PublicClean app is audited to prove required non-retail files are present and forbidden retail extensions are absent. A physical-device test covers fresh install, import, explicit entry, visible menu, termination/relaunch, visible menu, and a screenshot after each visible state.

## Success criteria

- Import completion never launches SDL from inside the picker completion path.
- A fresh PublicClean install can show the import UI without retail assets.
- After valid user assets are imported, the first game entry shows the complete menu.
- A later app relaunch shows the complete menu again.
- A missing non-retail bundle dependency produces a visible native error and a diagnostic path list, not a wallpaper-only interactive screen.
- PublicClean bundle audit finds no retail MIX, map, audio, or other forbidden official game payload.
