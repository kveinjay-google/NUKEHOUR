# NUKEHOUR MULTIPLAYER PHASE 2.1 FINAL CLOSURE REPORT

## Status

COMPLETE

## Phase 2 Final Status

COMPLETE

## Public Dedicated Server

Provider: Alibaba Cloud ECS
Region: cn-shenzhen
OS: Alibaba Cloud Linux 4.0.3
Architecture: x86_64
Game Port: TCP 4567
Public Address: REDACTED (8.135.x.x)

## Artifact

Filename: `NUKE-HOUR-Server-1.0.3-phase2-final-linux-x64.tar.gz`
Version: `1.0.3 / release-20250330`
Commit: `83df711f90130765ba795a392c0629bb1edc595c`
SHA256 Local: `1d1051fa5d2473a5a737d7b34f903cab7714aebd57512190eb6938252ea6917a`
SHA256 Remote: `1d1051fa5d2473a5a737d7b34f903cab7714aebd57512190eb6938252ea6917a`
Match: PASS

## Internet Matrix

| Client | Connect | Handshake | Lobby | Map | Start | Gameplay | Result |
| ------ | ------- | --------- | ----- | --- | ----- | -------- | ------ |
| macOS Apple Silicon | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| iOS arm64 | PASS | PASS | PASS | PASS | PASS | PASS | PASS |

Direct IP `8.135.x.x:4567` was used; no Master Server or cloud lobby was involved.

## Same Session Stability

Clients: macOS + iOS
Session: `phase12-20260908T152413Z-a3e854e63596`
Ticks: common stable window 2373 (macOS 2384 / iOS 2373)
OutOfSync: false
Unexpected Disconnect: none during the stable window
Order Rejection: false
Result: PASS

The same public session issued select, movement, resource update, production, completed-production, and attack orders. A separate public gameplay journal observed the attack action successfully.

## Host Independence

PASS. After one client disconnected, the dedicated server remained alive and in-game. It transitioned to `GameFinished` and stopped cleanly only after the second client exited.

## Empty Server Lifecycle

PASS. Current configured policy is clean shutdown after a completed room becomes empty (`Restart=no`). Logs show `GameFinished` → `Stopping` → `Dedicated server stopped cleanly`; no crash, zombie process, or busy loop was observed.

## Public Negative Compatibility

PASS. A controlled malformed/mismatched HandshakeResponse fixture sent over the public Direct IP path was explicitly rejected with `PROTOCOL_MISMATCH`; it did not degrade to timeout, silent disconnect, or server crash.

## x86_64 Runtime

PASS. The public x86_64 VPS ran the formal archived artifact, reached READY, listened on TCP 4567, admitted valid Apple clients, and shut down cleanly.

## ARM64 Runtime

PASS. The formal Linux ARM64 archive audit and container runtime evidence passed: READY, listener active, non-root UID 10001, read-only root, and clean exit.

## Local Multiplayer Regression

iOS Host → macOS: PASS
macOS Host → iOS: PASS
Direct IP: PASS over LAN TCP 1234

The committed Phase 1.2 physical evidence remains valid; Online Dedicated Server work did not remove or replace Local Multiplayer.

## Commercial Asset Audit

PASS. The deployed server directory contains none of `ra2.mix`, `language.mix`, `ra2md.mix`, `langmd.mix`, commercial executables, music, cinematics, or other unauthorized proprietary assets. Commercial assets included = NONE.

## Android

Phase 1 Android Physical Matrix:
PENDING

Android → Dedicated Server Physical:
NOT DEVICE VERIFIED

## Tests

Passed: 424 packaging tests; 176 multiplayer/dedicated NUnit tests; DS1–DS10; x86_64 and ARM64 runtime/audit gates; public Apple session; host-independence; public negative; Local Multiplayer regression.
Failed: 0 required gates (one superseded 1800-tick attempt timed out before lobby start and is retained only as diagnostic evidence).
Skipped: Android physical matrix; LAN Discovery (not implemented / not verified).
New Regressions: none.

## Git

Branch: `codex/multiplayer-phase2`
Starting Commit: `e3fcf0bb7f21dbf87c263d5544139624bf4af7e2`
Final Commit: recorded by the closure commit containing this report and evidence

## Files Changed

- `docs/testing/results/dedicated-server-phase2.1/evidence.json`
- `docs/nukehour-multiplayer-phase2.1-final-report.md`
- `docs/superpowers/plans/2026-09-08-nukehour-phase2.1-final-closure.md`

## Commits

- `e3fcf0bb7f21dbf87c263d5544139624bf4af7e2` — starting Phase 2 evidence baseline
- closure commit — Phase 2.1 final evidence and report

## Remaining Issues

P0: None.
P1: Android ARM64 physical validation remains open for Phase 1; Phase 1 stays PARTIAL.
P2: LAN Discovery remains NOT IMPLEMENTED / NOT VERIFIED. No Phase 3 Online Lobby, matchmaking, account, orchestrator, or autoscaling work was started.
