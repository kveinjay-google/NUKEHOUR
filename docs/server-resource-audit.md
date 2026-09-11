# NUKE HOUR Dedicated Server Resource Audit

## Result

Commercial assets included: **NONE**.

The Linux bundle and archive are generated from an explicit allowlist and then
checked against a cryptographic inventory.  The archive audit rejects path
traversal, links, special files, duplicate members, undeclared files, hash or
mode changes, secret-like filenames, Windows executables, and commercial media
extensions including MIX, BAG, AUD, VQA, and EXE.

## Server requires

- the self-contained `OpenRA.Server` runtime and managed dependencies;
- `OpenRA.Game`, `OpenRA.Mods.Common`, `OpenRA.Mods.Cnc`, and
  `OpenRA.Mods.RA2` assemblies and dependency manifests;
- NUKE HOUR/OpenRA gameplay YAML, Lua, and Fluent definitions needed while
  loading the RA2 rules and selected map;
- redistributable map YAML and `map.bin` data;
- engine/mod version metadata, launch/health scripts, configuration template,
  source acknowledgements, and GPL license texts.

## Server does not require

- `ra2.mix`, `language.mix`, `ra2md.mix`, `langmd.mix`, or any other MIX file;
- original game executables;
- music, speech, sound effects, cinematics, or retail video;
- sprites, palettes, fonts, cursor images, UI chrome images, or client artwork;
- SDL/OpenGL client windows, GPU access, desktop interaction, iOS code,
  Android code, or macOS app frameworks;
- an account, NukeHour cloud service, public lobby, or OpenRA Master Server.

## Gameplay and presentation boundary

The simulation still parses some presentation *definitions* because the RA2
manifest is shared with clients, but the headless process does not open their
referenced media bytes.  Dedicated `ModData` skips the client WidgetLoader and
uses a server-only `headless-server` RuntimeProfile.  It projects only the
versioned `ra2-presentation-capability-v2` presence contract, producing the
same deterministic RuntimeContract as a legally imported client without
copying any retail package to the server.

That exemption is fail-closed: it is accepted only for the exact audited
`ra2-presentation-capability-v2` capability and its exact four presentation
package names (`ra2.mix`, `language.mix`, `ra2md.mix`, and `langmd.mix`).
Adding or renaming even one entry blocks server startup with
`DEDICATED_SERVER_RESOURCE_BLOCKER`; a future gameplay dependency therefore
cannot silently inherit the presentation exemption. Missing resources on a
client still fail.

## Runtime proof

The audited Linux ARM64 bundle ran in a non-root, read-only container with no
retail resources, reached `WAITING_FOR_PLAYERS`, accepted the game port, and
stopped cleanly.  The same headless path hosted a real Apple two-client game
through lobby, map, Ready, Start, spawn, movement, resources, production,
completed production, and attack for at least 1800 world ticks without desync.

The builder accepts only a checked-in publish-output allowlist and refuses a
dirty tracked source tree. It records the exact commit, `trackedTreeClean`, and
`commercialResourcesIncluded: NONE` in `server-build.json`; independent bundle,
archive, and container-build policies reject inventory-declared extras as well
as commercial resources.
