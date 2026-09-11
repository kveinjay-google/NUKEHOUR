# NUKE HOUR iOS/iPadOS 1.0 (Build 16)

Build 16 is the first current-source release after the historical Build 4
archive. It includes the accumulated iPhone/iPad layout, import, startup,
audio, touch-control, skirmish, branding, and multiplayer compatibility work
represented by this repository state.

## PublicClean distribution boundary

The TestFlight application does not contain Red Alert 2 or Yuri's Revenge
retail MIX archives, retail maps, music, voices, user imports, signing keys, or
provisioning credentials. It contains the NUKE HOUR application, open-source
engine components, project-owned UI/branding artwork, approved fonts and
redistributable presentation dependencies.

The iOS bundle is audited against
`packaging/ios/public_clean_manifest.json` before signing and again after IPA
packaging. RA2 binary support art under `mods/ra2/bits` is deny-by-default; the
only files admitted from that directory are the three project-original
`nc-impact-*.png` overlays.

## User-supplied game data

Players import legally owned game files after installation:

- `ra2.mix` and `language.mix` enable Red Alert 2 content;
- `ra2md.mix` and `langmd.mix` enable Yuri's Revenge content.

Imported files remain in the application's writable support directory and are
not part of the application bundle or this source repository.
