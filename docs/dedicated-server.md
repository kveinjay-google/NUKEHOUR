# NUKE HOUR Dedicated Server

The Phase 2 server is one headless process for one game room.  It reuses the
same handshake, RuntimeContract, map/session checks, orders protocol, and
simulation as permanent Local Multiplayer.  It does not provide accounts,
room codes, matchmaking, a public lobby, or orchestration.

## Build

Linux x86_64 is the primary target; Linux ARM64 is built by the same pipeline.

```sh
make server-build SERVER_RID=linux-x64 SERVER_VERSION=1.0.3-phase2
make server-build SERVER_RID=linux-arm64 SERVER_VERSION=1.0.3-phase2
```

The output directory contains an extracted bundle, deterministic `.tar.gz`,
and SHA-256 sidecar.  Existing artifact paths are never overwritten.

Audit either form:

```sh
python3 packaging/server/audit_server_bundle.py /path/to/server-bundle
python3 packaging/server/audit_server_bundle.py /path/to/server.tar.gz
```

## Configure and run on Linux

Copy `server-config.env.example` to `server-config.env` beside the bundle and
edit it.  Do not place credentials in source control.

```sh
tar -xzf NUKE-HOUR-Server-<version>-linux-x64.tar.gz
cd NUKE-HOUR-Server-<version>-linux-x64
cp server-config.env.example server-config.env
./run-server.sh
```

Supported settings are Server Name, numeric Listen Address, Listen Port, Map,
Password, Max Players, online advertising, replay recording, sync reports,
single-player allowance, authentication requirement, idle timeout, status
file, and support directory.  Invalid keys and out-of-range ports/player limits
are rejected.  `AdvertiseLAN=True` is explicitly unsupported in Phase 2.

Defaults relevant to deployment:

- listen address `0.0.0.0`;
- TCP port `1234`, with any port from 1 through 65535 allowed for additional instances;
- `AdvertiseOnline=False`;
- sync reports enabled;
- eight players maximum;
- empty pre-game lobby timeout of 900 seconds;
- one process equals one room.

Expose only the selected TCP game port.  Direct Connect clients enter
`PUBLIC_IP:PORT`.  TLS is not added to the existing gameplay transport.

## Maps

Set `Map` to a bundled map name or UID.  If empty, the existing OpenRA map
selection rules choose an available map.  The server does not implement a
second map CDN or map protocol.

## Health and lifecycle

`healthcheck.sh` reads the atomic JSON status file.  Process liveness and
readiness are separate fields.  A ready process is in `WAITING_FOR_PLAYERS` or
`IN_LOBBY`.

Lifecycle values are:

```text
STARTING -> WAITING_FOR_PLAYERS -> IN_LOBBY -> IN_GAME
         -> GAME_FINISHED -> STOPPING -> STOPPED
```

SIGINT and SIGTERM request a clean shutdown.  A completed room exits after all
players leave; one remaining ordinary client does not make the server exit.
Unhandled failures remain non-zero and are not hidden by an internal restart
loop.  Use an external supervisor only if restart policy is desired.

Logs and status live under `SupportDir`.  They include version/protocol/state,
map and compatibility diagnostics, but omit passwords, imported retail paths,
participant nonces, and unnecessary full client addresses.

## Docker

Build an audited bundle for the Docker host architecture and place it at
`server/bundle`, then:

```sh
docker build -t nukehour/server:phase2 server
docker run --read-only --tmpfs /tmp \
  --security-opt no-new-privileges --cap-drop ALL \
  -p 1234:1234/tcp -v nukehour-data:/data \
  nukehour/server:phase2
```

The multi-stage image verifies the inventory before copying only the bundle to
the Debian runtime stage.  It runs as non-root UID 10001.  Privileged mode,
host networking, and host-filesystem mounts are neither required nor expected.

## Compatibility

Current shared values remain transport handshake 7, handshake schema 1,
orders protocol 23, RuntimeContract schema 1, and resource capability
`ra2-presentation-capability-v2`.  Dedicated Server has a distinct
`linux-server` or `macos-server` diagnostic profile; this does not bypass
protocol, engine, mod, RuntimeContract, or map rejection.

## Testing

```sh
make server-test
make server-smoke
```

The DS1-DS10 schema is documented in
`docs/testing/dedicated-server-test-schema.md`.  Real-client acceptance still
requires a physical iOS/macOS session; public-internet acceptance additionally
requires an authorized Linux host with public IPv4.

## License and source

OpenRA and the server modifications remain GPL-covered and live in this same
source repository and replayable engine patch series.  Binary distribution
must retain the included licenses and provide the corresponding source under
the applicable GPL terms.
