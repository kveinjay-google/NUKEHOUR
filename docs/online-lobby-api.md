# NUKE HOUR Online Lobby API v1

The production API is JSON over HTTPS. All write models forbid unknown fields;
strings and collections are bounded. Public responses never contain the global
registration credential, per-room registration token, password, RuntimeContract
digest, client identity, or internal topology.

## Public routes

### `GET /health`

Returns `200 {"status":"ok"}`. It does not inspect the room directory.

### `GET /ready`

Returns `200 {"ready":true}` when the process is ready to serve requests.

### `GET /v1/rooms`

Returns `200 {"rooms":[PublicRoom...]}`. Only non-stale, ready rooms whose
status is `WAITING` or `IN_GAME` are public. Sorting prefers non-full `WAITING`
rooms, then full `WAITING`, then `IN_GAME`, followed by creation time and room
ID.

### `GET /v1/rooms/{roomId}`

Returns one public room or `404 {"detail":"room not found"}`.

### `GET /v1/rooms/code/{roomCode}`

Room codes are case-insensitive on input and normalized to six characters from
`ABCDEFGHJKMNPQRSTUVWXYZ23456789`. Invalid syntax returns 422; a valid unknown
code returns 404.

`PublicRoom` fields are:

```json
{
  "roomId": "uuid",
  "roomCode": "A7K9Q2",
  "serverId": "stable-server-id",
  "serverEndpoint": "203.0.113.10",
  "serverPort": 4567,
  "serverName": "NUKE HOUR Dedicated Server",
  "region": "region-token",
  "status": "WAITING",
  "map": "map-uid",
  "mod": "ra2",
  "modVersion": "compatibility-version",
  "players": 0,
  "maxPlayers": 8,
  "hasPassword": false,
  "ready": true,
  "engineCompatibility": "release-20250330",
  "handshakeSchemaVersion": 1,
  "ordersVersion": 23,
  "runtimeCapability": "ra2-presentation-capability-v2",
  "trust": "official",
  "createdAt": "RFC3339 timestamp",
  "lastHeartbeatAt": "RFC3339 timestamp"
}
```

The endpoint is needed for direct TCP handoff but the product UI does not
display it.

## Authenticated server routes

### `POST /v1/servers/register`

Requires `Authorization: Bearer <global-registration-credential>`. The JSON body
contains the public fields above except Lobby-owned room ID/code/trust/timestamps.
`serverEndpoint` must be a numeric IP address; `serverPort` is 1–65535;
`players <= maxPlayers <= 64`; region/mod tokens use their documented restricted
alphabets.

Success returns 201:

```json
{
  "room": { "...": "PublicRoom" },
  "registrationToken": "private per-registration token"
}
```

Registering the same `serverId` preserves its room ID/code and creation time but
rotates the private token and replaces the published session. A wrong or missing
global credential returns 401.

### `POST /v1/servers/{serverId}/heartbeat`

Requires the private `registrationToken` as Bearer. The optional body fields are
`status`, `map`, `players`, `maxPlayers`, `hasPassword`, and `ready`. It returns
the updated `PublicRoom`. Unknown server is 404; incorrect token is 401; invalid
capacity or values are 422.

### `POST /v1/servers/{serverId}/unregister`

Requires the private registration token and returns 204. The room immediately
disappears. Unknown server is 404; incorrect token is 401.

## Status and error behavior

States are `STARTING`, `WAITING`, `IN_GAME`, `FINISHED`, and `OFFLINE`.
`STARTING`, `FINISHED`, `OFFLINE`, non-ready, and stale records are hidden.
Public and server request limits are independent fixed windows keyed by request
source: defaults are 120 and 240 requests per 60 seconds. Exceeding a window
returns 429. Bodies over 16 KiB return 413. Schema violations return 422.
Unknown host headers return 400. Production `/docs` and `/openapi.json` return
404; CORS is not enabled.

Every HTTP response adds `X-Content-Type-Options: nosniff`,
`X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, a restrictive
`Permissions-Policy`, and `Cache-Control: no-store`. Public Nginx additionally
sets HSTS.
