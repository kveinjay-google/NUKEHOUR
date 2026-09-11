# NUKE HOUR Multiplayer Phase 3 Design

## Scope and permanent boundaries

Phase 3 adds an Online Multiplayer room directory without changing the Phase 1/2
game protocol. Local Multiplayer remains a separate first-class path: LAN,
Direct `IP:port`, TCP `1234`, and player-hosted iOS, Android, and macOS games do
not depend on the Lobby Service, an account, or any cloud component.

The Online path is metadata discovery only:

```text
Client -> HTTPS Lobby Service -> room endpoint
Client -------------------------> Dedicated Game Server (existing TCP protocol)
```

The Lobby Service never relays, parses, stores, or simulates gameplay orders.
Phase 3 does not add accounts, matchmaking, chat, friends, ranking, server
allocation, autoscaling, or automatic server creation.

## Selected architecture

### Lobby Service

Add one small Python 3.11/FastAPI process under `lobby/`. It owns an in-memory,
thread-safe directory of short-lived official rooms. A single instance is
sufficient for Phase 3. Dedicated servers re-register when the service restarts,
so loss of in-memory state is recoverable without restarting the game process.

Public read routes expose only room fields needed for display, compatibility
precheck, and endpoint resolution. Server write routes use an exact
`Authorization: Bearer` registration credential supplied only through the
deployment environment. Registration returns a random UUID room id, an
unambiguous six-character room code, and a separate high-entropy registration
token used for heartbeat and unregister. Neither credential is logged or
returned by public routes.

The service uses strict Pydantic schemas with forbidden extra fields, bounded
strings, control-character rejection, numeric endpoint validation, a small JSON
body limit, Trusted Host validation, generic errors, security headers, and
in-process token-bucket rate limiting. CORS, cookies, HTML, file serving,
WebSockets, and public OpenAPI documentation are disabled in production. TLS is
terminated by the deployment reverse proxy; localhost HTTP remains supported for
tests and development.

### Dedicated Server registration

Extend the Phase 2 dedicated runtime with a focused Lobby registration client.
The server reads Lobby URL, public endpoint, region, and registration credential
from server-only settings/environment. It derives map, players, maximum players,
password flag, lifecycle status, handshake schema, orders version, engine
compatibility, mod compatibility, and runtime capability from the running build
and session. Deployment scripts never hand-author protocol values.

Registration begins only after the dedicated process is ready. Heartbeats run at
a bounded interval, a missing registration causes automatic re-registration, and
shutdown makes a best-effort unregister request. Lobby errors are logged without
credentials and never stop or alter gameplay.

### Client

The multiplayer browser gains explicit `Local` and `Online` modes plus permanent
Direct Connect. Local mode reads only LAN/nearby discovery and never calls the
Lobby Service. Online mode polls the configured HTTPS `/v1/rooms` API at a
reasonable interval, shows room metadata without exposing IP/port, supports
room-code lookup, and maps a selected room to the existing `ConnectionTarget`.

Before TCP connect, the client compares the public compatibility envelope:
handshake schema, orders version, engine compatibility, mod id/version, and
runtime capability. A clear version-mismatch message is shown when this fails.
The full Phase 1 handshake and RuntimeContract admission remains authoritative
after connection, including when the directory metadata was stale or incorrect.

`WebServices.OnlineLobby` is the single client base-URL setting. Development,
staging, and production builds can override it without embedding a VPS address or
any registration secret.

## Room lifecycle and API

Public routes:

- `GET /v1/rooms`
- `GET /v1/rooms/{roomId}`
- `GET /v1/rooms/code/{roomCode}`
- `GET /health`
- `GET /ready`

Authenticated server routes:

- `POST /v1/servers/register`
- `POST /v1/servers/{serverId}/heartbeat`
- `POST /v1/servers/{serverId}/unregister`

Directory states are `STARTING`, `WAITING`, `IN_GAME`, `FINISHED`, and
`OFFLINE`, mapped from the existing dedicated lifecycle. Only ready `WAITING`
rooms are joinable. Missed heartbeats transition to offline and disappear from
public listing after the configured TTL. Duplicate registration by `serverId`
updates the same server record and rotates its private registration session;
room-code collisions are retried without replacing an existing room.

## Testing and completion

TDD covers registration/authentication, validation, duplicate handling,
heartbeat updates, stale cleanup, unregister, room-code collision and lookup,
rate limiting, secret-safe logging, client parsing/precheck/error mapping, server
retry/isolation, UI mode separation, and Local/Direct IP regression.

Build artifacts include Linux x86_64 and ARM64 Lobby images/bundles and a refreshed
Dedicated Server artifact, all audited for credentials and commercial resources.
Final acceptance requires a public HTTPS deployment, real macOS and physical iOS
room-list/code joins into the same dedicated room, normal gameplay, Lobby outage
isolation, and Local Direct `IP:1234` regression. Android is build-verified only
unless a physical ARM64 Android device is available; the Phase 1 Android physical
gate remains pending.

Phase 3 is reported `COMPLETE` only if every explicit completion gate has fresh
evidence. Otherwise the report is `PARTIAL` or `BLOCKED`, with unverified gates
listed. Work stops after the Phase 3 report and does not begin provisioning,
accounts, friends, matchmaking, ranking, or other later phases.
