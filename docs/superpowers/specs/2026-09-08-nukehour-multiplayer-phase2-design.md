# NUKE HOUR Multiplayer Phase 2 Design

## Scope

Phase 2 adds an internet-reachable dedicated-server deployment to NUKE HOUR
without changing or replacing Local Multiplayer.  Local LAN hosting, discovery
where implemented, Direct `IP:port`, default TCP port `1234`, and player-hosted
iOS, Android, and macOS servers remain permanent first-class product features.

Online Multiplayer in this phase is only:

```text
NUKE HOUR client -> public IPv4:port -> one dedicated game-server process
```

It does not add accounts, a room directory, matchmaking, room codes, friends,
ranking, orchestration, or a second gameplay protocol.

## Selected architecture

Reuse OpenRA's existing `OpenRA.Server` executable, `Server`, handshake,
RuntimeProfile, RuntimeContract, map/session validation, order relay, replay,
sync-report, and configuration machinery.  Add a small NUKE HOUR delivery layer
around it rather than creating a parallel server implementation.

The shared boundary remains:

```text
Shared: handshake + compatibility + map/session + orders + simulation
Local:  player-hosted OpenRA Server
Online: headless OpenRA.Server in an independently deployed process
```

The server artifact is one process for one room.  The process owns the game and
does not belong to any client.  A normal client disconnect must not be treated as
loss of a host device.  After the room's game has ended and all players have left,
the process shuts down cleanly instead of silently opening another room.  Startup
or runtime crashes remain non-zero failures with preserved logs.

## Protocol and identity

The Phase 1 protocol is frozen unless a real dedicated-server test exposes a
defect:

- transport handshake protocol `7`;
- handshake schema `1`;
- orders protocol `23`;
- gameplay compatibility `nukehour-core-sha256-*`;
- resource capability `ra2-presentation-capability-v2`;
- existing engine, mod, map, and RuntimeContract rejection codes.

The server uses the same RuntimeContract as compatible clients.  Its diagnostic
RuntimeProfile explicitly identifies a dedicated server and never pretends to be
iOS, Android, or macOS.  RuntimeProfile remains diagnostic and does not create a
second compatibility path.

## Headless resource boundary

Retail Red Alert 2/Yuri archives, executables, music, videos, speech, and other
commercial assets are client presentation inputs and must never enter the server
artifact or container image.  The server bundle contains only GPL/open-source
engine code, NUKE HOUR gameplay definitions, selected redistributable maps,
configuration, scripts, and license material.

The current client manifest requires imported presentation packages while
constructing its resource capability.  For a verified headless process, the
engine may satisfy that presentation-presence portion of the shared contract
without mounting or copying the package bytes.  This exception is restricted to
the dedicated-server entry point; clients continue to require their imported
resources.  Before enabling the exception, automated and runtime audits must
prove that map loading and deterministic simulation do not read retail files.  A
real simulation dependency becomes `DEDICATED_SERVER_RESOURCE_BLOCKER`; it is not
worked around by uploading commercial content.

## Build and runtime artifact

Add a formal Linux x86_64 server build, with Linux ARM64 supported when the same
pipeline can produce it economically.  The build uses `dotnet publish`, stages an
explicit allowlist of runtime files and mod data, audits every output path and
archive signature, and emits a versioned archive plus SHA-256 manifest.  It must
be runnable on bare Linux.

The runtime accepts existing OpenRA server settings for name, port, map,
password, authentication, replay, sync reports, and online advertising.  The
delivery wrapper adds only operational concerns that OpenRA does not expose:
listen address selection, a bounded idle timeout, one-room exit behavior, and a
machine-readable status file.  The status file distinguishes process liveness
from readiness and records only non-secret operational state.

A multi-stage Docker build may consume the same server bundle.  The final image
runs as a non-root user, exposes only the selected TCP game port, does not use
privileged/host networking, and contains neither the repository nor retail
content.

## Lifecycle and observability

The server maps its existing state into these operational states:

```text
STARTING -> WAITING_FOR_PLAYERS -> IN_LOBBY -> IN_GAME
         -> GAME_FINISHED -> STOPPING -> STOPPED
```

Logs identify product/build, protocol versions, runtime profile/contract,
listen endpoint, map, joins, compatibility rejection codes, game start/finish,
disconnect, desync, and shutdown reason.  Public logs do not expose secrets,
complete imported paths, or unnecessary full client addresses.

## Validation

Automated DS1-DS10 coverage proves headless startup, listening, valid admission,
incompatible rejection, map load, game start/order relay, disconnect stability,
clean shutdown, and unchanged Local Multiplayer behavior.  The artifact audit
must report zero commercial resources.

Acceptance then progresses in order:

1. local dedicated process with macOS and a physical iOS client;
2. temporary Linux VPS with public IPv4 and only the game port exposed;
3. macOS and iOS in one public-server game for at least 1800 world ticks;
4. compatibility-negative cases and host-independence behavior;
5. repeat both Phase 1.2 Local Direct `IP:1234` directions.

Phase 1's Android physical gate remains pending and does not block the Apple
server-foundation gate.  Phase 2 is COMPLETE only with real Linux, public
internet, two-client gameplay, local regression, artifact, audit, and document
evidence.  Otherwise the report is PARTIAL or BLOCKED with the exact unverified
gates.  Phase 3 work must not begin.
