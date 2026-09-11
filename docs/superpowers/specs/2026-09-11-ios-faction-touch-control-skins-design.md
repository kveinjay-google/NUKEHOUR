# iOS Faction Touch Control Skins Design

## Goal

Replace the current mismatched iOS/iPadOS touch controls with three original, faction-specific visual systems that share one stable geometry contract. The redesign covers the floating action buttons, virtual joystick, shortcut-bar button backgrounds, shortcut-bar panel background, expand/collapse affordance, and every shortcut glyph.

## Approved direction

The user selected direction C: each faction receives a distinct shell and material language instead of a simple recolor.

- Allies: aerospace instrumentation. Brushed blue titanium, pale alloy edges, cyan illumination, restrained amber status accents.
- Soviets: heavy industrial controls. Blackened steel, worn silver, crimson enamel, brass and warm amber illumination.
- Yuri: psychic biotechnology. Obsidian-violet alloy, curved layered shells, muted acid-green indicators, pale lavender highlights.

These skins are original NUKE HOUR assets. They may echo the game's broad retro-RTS mechanical mood, but must not copy official logos, faction emblems, interface art, or retail assets.

## Shared geometry contract

All factions use the same pixel coordinates and transparent margins. Visual differences may change materials and silhouette details only inside those bounds.

- Floating actions: 512 × 512 atlas, five 56 × 56 components at X = 0, 64, 128, 192, 256 and seven state rows at Y = 0, 64, 128, 192, 256, 320, 384.
- Joystick: 256 × 256 atlas containing a 144 × 144 base, a 64 × 64 idle thumb, and a 64 × 64 active thumb.
- Shortcut chrome: 512 × 128 atlas containing eight 48 × 52 button-state cells, expanded and compact nine-slice panels, and 26 × 26 collapse/expand affordances.
- Shortcut glyphs: one 2048 × 128 atlas per faction. All 33 catalog commands occupy a 32-pixel column grid and four state rows. This includes groups 1–10, ×5 production, selection/navigation commands, unit commands, stances, sell, repair, and beacon.
- Touch hit targets remain controlled by layout code and do not shrink with the artwork.

## Icon language

The glyph itself must communicate the action. No label is rendered below any shortcut icon.

- Groups 1–10: a large numeral integrated into a small command-token motif; never a second caption.
- ×5: five stacked production bars plus a compact ×5 mark inside the glyph.
- Attack-move: reticle plus forward command vector.
- Scatter: four arrows moving away from a center point.
- Queue orders: three offset command cards with a small plus.
- Stop: an octagonal stop plate or raised palm, chosen consistently within each faction.
- Cycle base: base silhouette surrounded by a circular transfer arrow.

All glyphs use one optical canvas, stroke hierarchy, light direction, and internal padding per faction. Disabled state reduces contrast without adding an opaque black square. Hover, pressed, and active states change illumination and recess depth without moving the glyph.

## Generation and production pipeline

The built-in image generation engine produces one high-resolution style master per faction. Those masters are visual sources, not directly indexed sprite sheets. A deterministic compositor converts approved motifs into exact-size transparent atlases, so generative irregularities cannot alter region coordinates or introduce mask artifacts.

Production files use versioned `v3` names until verified. Existing shipping files remain untouched until the YAML bindings and automated atlas checks pass. Source masters are retained under `design-previews/ios-touch-controls-v3/` for reproducibility and later refinement.

## Runtime integration

`TouchFactionSkin` already selects faction-specific joystick, action, and shortcut chrome collections. The redesign adds faction-suffixed shortcut glyph collections so `AddFactionSuffixLogic` selects the matching glyph atlas too. The layout code continues to center every icon in an identical slot and keeps all shortcut labels hidden.

## Verification

- Automated tests verify dimensions, alpha channels, region bounds, equal transparent padding, faction-specific bindings, label suppression, and absence of opaque corner masks.
- Engine tests verify all touch hit targets and safe-area placement remain unchanged.
- A signed iOS build is audited, installed on the iPad, and visually checked in gameplay for every faction and interaction state that can be reached locally.
