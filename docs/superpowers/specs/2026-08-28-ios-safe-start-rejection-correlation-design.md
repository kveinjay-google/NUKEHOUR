# iOS Safe-Start Rejection Correlation Design

**Date:** 2026-08-28
**Status:** Approved for implementation
**Scope:** Correlate iOS safe-start requests and server rejections without changing desktop lobby behavior.

## Context

The protocol-v22 iOS lobby sends `startgame_safe <map-uid>` and temporarily disables the Start button while waiting. A structured `SafeStartRejected` response currently clears that pending state without proving that the response belongs to the latest request, and the UI discards the response reason before the throttled readiness delta necessarily arrives.

The central multiplayer map-safety gate still prevents an unsafe game start, so this is not a server-authority bypass. It is a user-visible race: a delayed rejection from an earlier request can clear a newer pending request, and the button can briefly show stale text or permit another tap instead of immediately showing the server's blocker.

## Goals

- Correlate every safe-start rejection with exactly one client request.
- Ignore delayed or stale rejections without changing current UI state.
- Show the accepted server rejection reason immediately.
- Keep the rejection visible until authoritative lobby/readiness state changes, the selected map changes, or a newer request replaces it.
- Preserve the central server gate, all map-readiness invariants, desktop Force Start behavior, and Local/Skirmish compatibility.
- Deliver the fix as a new `0017` engine patch and commit without rewriting `0016`.

## Non-goals

- The request identifier is not an authentication token and does not replace admin or map-safety checks.
- No new lobby panel, animation, translation, YAML widget, or desktop behavior is introduced.
- The readiness throttle and progress protocol are not redesigned.

## Considered Approaches

### 1. Correlated request identifier plus authoritative rejection cache — selected

Add a monotonically increasing positive request identifier to the command and rejection payload. The server echoes it unchanged. A small client-side state machine accepts only the rejection matching its current pending map and request identifier, caches the accepted reason for display, and ignores old responses.

This directly fixes both the stale-response and truthful-display problems and is deterministic under retries, timeout, reordering, and map changes.

### 2. Correlate only by map UID

This rejects responses from an old map but cannot distinguish two retries for the same map. A delayed first response can still clear the second request, so it is insufficient.

### 3. Extend the disabled timeout

This reduces the chance of observing the race but does not establish response ownership or display the server reason. It masks rather than fixes the defect.

## Wire Contract

The client command becomes:

```text
startgame_safe <map-uid> <request-id>
```

- `<map-uid>` remains a non-empty raw UID with no whitespace.
- `<request-id>` is canonical invariant decimal in the range `1..Int64.MaxValue`.
- Extra tokens, signs, leading zeroes, overflow, zero, negative values, and non-decimal values are rejected.

`SafeStartRejected` adds an exact `RequestId` field to the existing fields:

```text
Reason
ClientIndex
ClientMapPhase
Progress
ExpectedMapUid
RequestId
```

The payload is a flat six-leaf schema: missing, duplicate, unknown, or nested fields are rejected. Numeric fields use their canonical invariant decimal form, so signs on non-negative values, leading zeroes, and other alternate spellings do not create a second wire representation.

The server always echoes both the parsed request identifier and the request's map UID for non-admin, stale-map, and availability rejections. In particular, a stale-map rejection must not replace `ExpectedMapUid` with the server's newer current UID: the field identifies the request being rejected. The identifier does not influence authorization or the safety decision.

Because both the command and response schema change, `ProtocolVersion.Orders` increases from 22 to 23 so mixed binaries fail during compatibility negotiation instead of silently timing out.

## Client State Machine

A focused `LobbySafeStartRequestState` owns:

- the last issued identifier;
- the current pending request and map UID;
- the last accepted structured rejection.

`Begin(mapUid)` clears any prior rejection, allocates the next positive identifier, and marks that request pending. Identifiers never wrap: reaching `Int64.MaxValue` fails closed instead of risking reuse. A 5-second timeout can clear only the exact identifier it scheduled.

`TryAcceptRejection(rejection, currentMapUid)` mutates state only when all of these match:

- a request is still pending;
- `rejection.RequestId` equals the pending identifier;
- `rejection.ExpectedMapUid` equals the pending map UID;
- that UID is still the current lobby map.

An accepted rejection clears pending and becomes the displayed availability result. A mismatched, delayed, post-timeout, or post-map-change rejection is ignored and cannot clear a newer request.

Same-map readiness and lobby synchronization do not cancel a pending request. Server commands, rejection orders, readiness deltas, and full lobby syncs for one connection share a FIFO send queue: if a sync arrives before the matching rejection, it was queued before the server evaluated that request, so the later rejection is the newer authoritative result. If the rejection arrives first, a subsequent relevant sync clears its cached presentation. A map UID change is different: it makes the pending request inapplicable and clears it immediately.

The cached rejection is cleared when:

- a `ClientMapReadinessChanged` delta arrives for the rejection's blocking client;
- a full `LobbyInfoChanged` synchronization arrives;
- a new safe-start request begins;
- the map UID changes;
- the connection closes or the lobby logic is disposed.

A readiness delta preserves pending and clears a cached client-specific rejection only when that client matches. A full lobby synchronization preserves a same-map pending request but clears any cached rejection. A full sync whose current map UID differs from the pending request clears pending as well.

After clearing, the existing local `LobbyStartAvailability.Evaluate` result drives the button again. The Start button remains disabled while pending or while a cached rejection is active.

## Server Flow

`LobbyCommands.StartGameSafe` strictly parses the request object under the existing lobby lock. It continues to check requester admin status, expected/current map equality, and shared start availability in the existing order. Every rejection is built with the request identifier and request map UID and sent only to the requesting connection. Successful requests still call the central guarded `Server.StartGame()`.

Local, Skirmish, legacy desktop `startgame`, and automatic-start semantics remain unchanged.

## Smoke and UI Integration

`LobbyLogic` issues commands through the request object's canonical formatter, delegates pending/rejection lifecycle to the state machine, and converts an accepted rejection back to `LobbyStartAvailability` for the existing localized formatter.

An accepted `RequesterNotAdmin` rejection maps explicitly to the existing localized `notification-requires-host` message instead of the generic waiting label. A `StaleMap` rejection is never accepted into the cache because its request map no longer equals the current lobby map; the synchronized current-map availability replaces it. No new Fluent key is required.

The two-device iOS multiplayer smoke uses distinct identifiers for the blocked 42% request and the final successful request. Its rejection handler requires the blocked request identifier, proving end-to-end correlation.

## Test Contract

Tests must first fail against protocol 22 and the uncorrelated implementation, then prove:

- strict request parsing and canonical formatting;
- rejection serialization round-trip including `RequestId` and fail-closed malformed cases;
- a matching rejection immediately becomes the displayed blocker;
- an old rejection cannot clear a newer pending request;
- wrong-map, wrong-ID, post-timeout, and unsolicited rejections are ignored;
- only the current timeout clears the current pending request;
- readiness and full lobby synchronization clear cached rejection state;
- server rejection construction echoes the request identifier;
- smoke uses different correlated identifiers for blocked and final start attempts;
- desktop Force Start remains unchanged;
- the runtime bundle audit rejects protocol downgrade, missing request correlation, or a UI handler that ignores the structured reason.

## Delivery

Generate `engine-patches/0017-ios-safe-start-rejection-correlation.patch` from a clean post-0016 replay using an explicit allowlist. Validate it with strict `git apply --check`, `patch -F 0`, forward/reverse dry-runs, byte-for-byte comparison, full C#/Python/YAML gates, Release assembly builds, signed ios-arm64 build, codesign verification, and bundle audits. Public-content manifest entries remain unchanged unless an already-listed file actually changes.
