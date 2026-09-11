# macOS NUKE HOUR Support Directory Design

## Goal

Store macOS NUKE HOUR user data under `~/Library/Application Support/Nuke Hour`
without forcing existing users to re-import content or risking data owned by other
OpenRA installations.

## Scope

This change applies to the macOS launcher and the game/server processes it starts.
iOS and Android sandbox paths are unchanged.

## Path policy

- New support root: `~/Library/Application Support/Nuke Hour`
- Legacy support root: `~/Library/Application Support/OpenRA`
- The launcher settings file, imported retail content, maps, saves, replays, logs,
  screenshots, editor data, command-bar preferences, skirmish settings, and engine
  `SupportDir` all resolve from the new root.
- No original retail source path is stored.

## One-time compatibility migration

Before the launcher reads settings, it performs an idempotent, copy-only migration.
It copies only NUKE HOUR-owned paths from the legacy root:

- `settings.yaml`
- `ra2-commandbar.yaml`
- `skirmish.ra2.yaml`
- `Content/ra2`
- `maps/ra2`
- `Saves/ra2`
- `Replays/ra2`
- `Screenshots/ra2`
- `Editor/ra2`

The migration never deletes from the legacy root, never follows symbolic links,
never overwrites a file already present in the new root, and verifies every copied
file by size and SHA-256. A path conflict keeps the new file and records it in the
result instead of failing or overwriting user data.

## Failure behavior

Migration failures do not modify existing destination files. Any partially created
new file is removed. The launcher reports the failure and stops before starting the game so it
cannot silently open an empty profile or ask for a needless re-import.

## Verification

Automated tests cover the branded root, allowlisted copy behavior, legacy retention,
conflict preservation, symlink rejection, idempotency, hash equality, derived
settings/log paths, and propagation of `Engine.SupportDir` to game and local-server
commands.
