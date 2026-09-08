# Skirmish Polish and Settings Pruning Design

## Scope

The iPad build will remove four non-player-facing advanced settings from the visible Advanced panel: performance graph, community-news fetching, version checking, and developer/debug controls. Their widget bindings will become optional so the shared settings logic remains valid without those controls.

The iOS skirmish colour selector will use a dedicated 3 by 3 popup of the existing nine classic preset colours. Each item is a large touch-safe colour swatch; the selected swatch has a visible outline/checkmark and no hexadecimal colour text. Multiplayer keeps its existing colour picker.

Skirmish player controls will use compact, touch-safe widths: name, colour, faction, team, spawn. The removed whitespace becomes map-preview width. The preview retains every map status, map-choice, spawn-selection, and action-state behavior.

## Design decisions

- Keep the existing preset-colour source of truth; no user-defined colours and no new colour persistence.
- Use an iOS-only widget template for the swatch grid, so desktop and multiplayer dropdowns do not regress.
- Drive player-panel versus map-preview allocation from `IosLobbyLayout` in skirmish mode. Multiplayer geometry stays unchanged.
- Hide/remove settings controls at the layout level and make `AdvancedSettingsLogic` tolerate absent optional widgets. This avoids a visible module and avoids runtime lookup faults.

## Validation

- Unit tests prove skirmish geometry gives the map more width than multiplayer and keeps all five active columns contiguous and touch-safe.
- Source-contract tests prove the new popup uses the classic preset source and contains no colour-code label path.
- Build-time iOS bundle audit accepts the compact skirmish layout while retaining the full multiplayer touch-target contract.
- A Release arm64 build is code-signature verified, then installed and launched on the registered iPad.
