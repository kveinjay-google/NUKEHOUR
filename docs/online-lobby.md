# NUKE HOUR Online Lobby

## Purpose and boundary

The Online Lobby is a short-lived directory for official NUKE HOUR Dedicated
Server rooms. It publishes discovery metadata over HTTPS, then hands the client
to the existing TCP game server. It never proxies gameplay, parses orders, runs
simulation, stores accounts, or creates servers.

```text
Client -> HTTPS Lobby -> public room endpoint
Client ----------------> Dedicated Server -> shared multiplayer protocol
```

Local Multiplayer is a permanent, independent product path. LAN hosting,
Direct `IP:port`, TCP `1234`, and iOS/Android/macOS local hosts do not require
the Lobby, an account, the Internet, or a Dedicated Server. Online and Local
reuse the same handshake, RuntimeProfile, RuntimeContract, orders protocol,
map/session compatibility, and gameplay simulation.

## Components

- `lobby/`: Python 3.11, FastAPI, Pydantic and Uvicorn Lobby service.
- `OnlineLobbyRegistration`: Dedicated Server registration, heartbeat,
  re-registration, and best-effort unregister client.
- `OnlineRoomDirectoryClient`: strict client parser, room/code lookup, response
  limit, and compatibility precheck.
- Multiplayer browser: explicit Local and Online tabs. Local never polls HTTP;
  Online polls the configured directory and still connects directly to the
  selected game server.

One Lobby process owns one thread-safe in-memory directory. The Phase 3
deployment intentionally uses one worker. State is disposable: after a Lobby
restart, live servers re-register automatically. A room disappears 45 seconds
after its last heartbeat.

## Dedicated Server configuration

Non-secret settings are documented in `server/server-config.env.example`:

- `OnlineLobbyUrl`: HTTPS Lobby base URL; blank disables Online registration.
- `OnlineLobbyServerId`: stable deployment identifier, 1–64 visible characters.
- `OnlineLobbyPublicEndpoint`: numeric public IPv4 or IPv6 address.
- `OnlineLobbyPublicPort`: public TCP game port.
- `OnlineLobbyRegion`: lowercase deployment region token.
- `OnlineLobbyHeartbeatSeconds`: bounded heartbeat interval; default 15 seconds.

The registration credential is supplied only as
`NUKEHOUR_LOBBY_REGISTRATION_CREDENTIAL` in the server process environment. It
must not be placed in settings YAML, command arguments, logs, artifacts, or Git.
The server derives status, map, players, capacity, password flag, protocol
versions, mod compatibility, engine compatibility, and runtime capability from
the live runtime. Lobby failures are logged as sanitized categories and do not
stop or alter an active match.

## Client behavior

The client base URL is `WebServices.OnlineLobby`. Production RA2 configuration
uses `https://lobby.nukehour.com/`; no VPS address or server credential is
embedded. Room endpoint and port are intentionally hidden in the room UI.

Online selection first compares handshake schema, orders version, engine
compatibility, mod id/version, and runtime capability. This is an early user
experience check only. The Dedicated Server handshake and RuntimeContract
admission remain authoritative after TCP connection and reject stale or forged
directory metadata.

## Production deployment

The audited container runs as UID 10002 with a read-only root filesystem,
`no-new-privileges`, all Linux capabilities dropped, and only a temporary `/tmp`.
Uvicorn binds to loopback behind Nginx. Nginx exposes HTTPS, redirects HTTP,
allows TLS 1.2/1.3, sets HSTS, and proxies only to the loopback service. The
registration environment file is mode 0600. The game TCP port is separate from
the Lobby HTTP port.

Required operational checks are:

1. Verify the artifact SHA-256 before deployment.
2. Inject a random registration token from a secret store or protected host
   file; never copy it into deployment documentation.
3. Check `GET /health` and `GET /ready` through public HTTPS.
4. Confirm one `WAITING` official room and the expected public game port.
5. Confirm the registration credential and per-room token are absent from
   process arguments, logs, images, archives, and public responses.
6. Exercise registration, heartbeat, timeout, unregister, invalid-auth and
   malformed-metadata fixtures without using player resources.

## Observability and recovery

Lobby logs include only operation type, official server ID, room ID, and count.
They exclude credentials and client identity. Dedicated logs record sanitized
Lobby failure categories. Health means the HTTP process is alive; readiness
means it can serve the in-memory directory. A Lobby restart loses directory
state by design and is recovered by server re-registration. An already connected
game continues when the Lobby is unavailable.
