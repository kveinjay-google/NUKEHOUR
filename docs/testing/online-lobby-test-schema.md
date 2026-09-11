# Online Lobby Phase 3 Test Schema

## Evidence rules

Each real session must use a unique run ID and two independent participant
nonces. Evidence is accepted only when both journals name the same run, verify
the remote identity and acknowledgement, carry identical RuntimeProfile,
RuntimeContract, runtime hash, mod compatibility, resource capability and map,
and report exactly two human clients. Raw registration credentials, room tokens,
full VPS addresses, local device IPs, and participant nonces are not copied into
the committed summary.

## Public service matrix

| Check | Expected |
| --- | --- |
| HTTPS health/readiness | 200, minimal JSON, TLS and security headers |
| Registration | 201 with private session token |
| Invalid global credential | 401, no credential echo |
| Malformed metadata | 422 |
| Heartbeat | 200 and updated public status/player count |
| Room list/details/code | 200 and identical room identity |
| Unregister | 204, then room detail 404 |
| Missed heartbeat | absent after more than 45 seconds |
| Oversized body | 413 |
| Fixed-window rate limit | independent public/server 429 behavior |
| Production docs/CORS | docs 404, no CORS header |

## Online physical session

1. Mac resolves the exact server ID from `GET /v1/rooms` (`onlinelist`).
2. iPad resolves the current six-character code (`onlinecode`).
3. Both use the resolved `ConnectionTarget`; no IP is manually entered.
4. Both complete handshake, RuntimeProfile, RuntimeContract, lobby, map
   readiness blocker/restoration, Ready, Start, Spawn, and Gameplay.
5. Gameplay observes select, movement, resource update, production, completed
   production, and an attack activity for at least 1800 synchronized world ticks.
6. Both report no desync, order rejection, serialization failure, or unexpected
   disconnect.

For deterministic physical testing, the smoke first issues an ordinary actor
`Attack`. If map distance, visibility, stance, or target conditions prevent an
attack activity for 30 ticks, it issues a nearby terrain `ForceAttack`. The
journal records `attackTargetKind`; success still requires observing an actual
activity and never treats order submission alone as success.

## Lobby failure isolation

After both clients are in gameplay, stop only the Lobby container. Confirm HTTPS
becomes unavailable, Dedicated remains active, and both client journals continue
to advance without desync or disconnect. A game already in progress may close
its listen socket to new clients; this is not a gameplay disconnect. Restart the
Lobby and confirm readiness plus automatic Dedicated re-registration.

## Local regression

Run both directions without any Lobby variables:

- iPad Local Host -> Mac Direct IP client.
- Mac Local Host -> iPad Direct IP client.

Both must use TCP 1234, complete the same gameplay assertions, and make no
Online HTTP request. Local remains a standalone supported product capability.

## Compatibility negatives

Unit/integration fixtures change one gameplay-core compatibility field at a time
and require precheck rejection. Direct TCP fixtures prove the server-side
handshake/RuntimeContract remains authoritative. Never mutate a user's retail
resources for negative testing.
