# macOS NUKE HOUR Runtime Brand Alignment Design

## Goal

Bring the macOS product branch into behavioral parity with the current Android
NUKE HOUR identity without changing gameplay, saved-data locations, bundle
identifiers, or user-selected names.

## Reference

The implementation reference is the Android project at
`/Users/kevin/Projects/RA2Android`, specifically its 2026-09-06 NUKE HOUR
branding work. The canonical Android application icon is byte-identical to this
repository's `branding/NUKE-HOUR-1024.png`, so no artwork regeneration or visual
redesign is required.

## Product Decisions

- The visible brand is always written exactly as `NUKE HOUR`.
- New players use `NUKE HOUR` as their default player name.
- The historical generated defaults `Commander` and `指挥官` migrate to
  `NUKE HOUR`; every other saved name remains unchanged.
- A local skirmish lobby uses the translated active-mod title, which is already
  `NUKE HOUR`, instead of the generic `Skirmish Game` literal.
- The macOS game start screen adds an About button immediately before Quit.
- The About panel contains the `NUKE HOUR` title, a concise macOS-specific
  description, the runtime version, `https://nukehour.com`, and a Back button.
- English and Simplified Chinese interfaces receive equivalent About copy;
  `NUKE HOUR` and the URL are not translated.
- The macOS launcher uses `NUKE HOUR` for its generated player-name default and
  recognizes the two historical generated defaults without altering custom
  names.

## Platform Boundary

The About button is enabled only when the OpenRA platform is macOS. Its YAML
definition remains in the RA2 mod alongside the rest of the product chrome, but
iOS, Windows, and Linux do not gain a new visible menu entry from this task.

The player-default and local-skirmish changes are intentionally shared engine
policy. This matches the Android implementation and prevents the same product
from presenting a different default identity when launched without the macOS
launcher.

## Architecture

- `Settings` owns exact legacy-default migration and preserves custom names.
- local-server creation reads the active mod's translated title rather than
  embedding a second product string.
- `MainMenuLogic` treats the About button as optional, exposes it on macOS, and
  owns the open/close callback so no iOS AOT registration is added for a
  macOS-only window.
- the desktop menu contract gains a seventh aligned control while the iOS
  runtime control list remains at its existing six controls.
- RA2 chrome and Fluent files own the branded panel layout and copy.
- the Python launcher mirrors the same default/migration policy before it builds
  engine launch arguments.
- engine-source and engine-test changes are captured in a new ordered engine
  patch so a future
  `fetch-engine.sh` run cannot erase them.

## Compatibility

- Keep the two existing macOS bundle identifiers, the internal game host name,
  `~/Library/Application Support/OpenRA`, mod id `ra2`, saves, maps, imported
  retail data, and network protocol unchanged.
- Do not rename or regenerate the existing app bundles, `.command` launcher,
  icons, wallpapers, DMG naming, or installer naming because they already use
  the approved NUKE HOUR identity.
- Treat only the exact historical generated names as migratable; names that a
  player deliberately chose are data and must be preserved.

## Verification

- NUnit covers the new default, both legacy migrations, custom-name retention,
  the skirmish-title source policy, and the macOS-only About wiring/copy.
- Python tests cover launcher defaults, both migrations, custom-name retention,
  and launch-argument behavior.
- Existing macOS branding, language, icon, cross-platform-branding, and menu
  layout suites remain green.
- Build the RA2 mod and engine in Release configuration.
- Generate and strictly replay the new engine patch after patches 0001-0019,
  proving byte identity with the checked-in engine sources.
- Launch the macOS game through its app bundle for a smoke check when the local
  runtime and retail content are available.
