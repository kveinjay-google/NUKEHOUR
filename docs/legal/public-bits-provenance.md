# Public RA2 Support-Art Provenance

The iOS PublicClean bundle requires a small `mods/ra2/bits` support-art tree for
menus, cursors, effects, Yuri structures, and production cameos. Omitting this
tree causes missing or degraded gameplay presentation even after a player
imports the retail MIX archives.

The 52 shipped files are deny-by-default and individually locked by path, size,
and SHA-256 in `public-bits-provenance.json` and the iOS public manifest:

- 20 files are byte-identical to the GPL-3.0-only OpenRA Red Alert 2 source at
  revision `61e24e3c1d7b586aa55a86096d29e1559aa9b994`.
- 27 additional files are byte-identical to the GPL-3.0-only Romanov's
  Vengeance source at revision
  `231059963663dcb07d07b56f607b41d173795da9`. Its `bits/AUTHORS.txt` credits
  the community artists, including MadHQ and Speeder for the Yuri service-depot
  and radar-dome art used here.
- 5 files are NUKE HOUR project originals: three reproducible projectile-impact
  overlays and two 240×192 Chinese Yuri production cameos generated during the
  project's 2026-08-22 artwork task.

As an independent cross-check, same-named entries were extracted from the
maintainer's lawfully owned RA2/YR retail MIX archives. Seven names overlapped;
all seven had different SHA-256 values. No reviewed bundled support-art file was
byte-identical to the corresponding retail file.

This audit does not authorize retail maps, audio, MIX archives, videos, models,
or other game data. Those remain excluded from the IPA and must be supplied by
the user through the in-app importer.

Regenerate the locked records from the two pinned source checkouts with:

```sh
python3 packaging/generate_public_bits_provenance.py \
  --openra-repo /path/to/OpenRA-ra2 \
  --rv-repo /path/to/Romanovs-Vengeance
```
