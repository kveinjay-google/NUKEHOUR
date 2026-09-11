# NUKEHOUR MULTIPLAYER PHASE 2 REPORT

## Status

PARTIAL

Phase 2 dedicated-server foundation is implemented and locally validated. The public-internet gate and Android physical gate remain open, so Phase 1/Phase 2 are not marked complete.

## Architecture

- Local Multiplayer remains a first-class LAN / Direct IP deployment with TCP 1234, no account, cloud, lobby, or dedicated-server dependency.
- Online Multiplayer is a separate future deployment that reuses the frozen handshake, RuntimeProfile, RuntimeContract, orders, mod, map, and gameplay simulation protocols.
- The dedicated runtime is headless, one process / one room, with explicit lifecycle and clean signal shutdown.

## Dedicated Server

- Platform: Linux x86_64 primary and Linux ARM64 artifact builds.
- Runtime: self-contained .NET publish; no public Master Server, lobby, account, or cloud dependency.
- Configuration: name, numeric listen address, port (default TCP 1234), map, password, max players, replay/sync options, status file, and online-advertise opt-in (default off).
- Lifecycle: STARTING, WAITING_FOR_PLAYERS, IN_LOBBY, IN_GAME, GAME_FINISHED, STOPPING, STOPPED.
- Health: status JSON distinguishes process alive from ready; container runs as non-root with read-only root filesystem.

## Resource Audit

- Commercial resources included: NONE.
- The server bundle contains managed binaries, gameplay definitions, maps, and licenses only; retail MIX/BAG/AUD/VQA/EXE resources are rejected.
- The headless RuntimeContract exemption is exact and fail-closed for `ra2-presentation-capability-v2`; any resource-set drift blocks startup with `DEDICATED_SERVER_RESOURCE_BLOCKER`.

## Local Test

- macOS dedicated server → physical iOS client: PASS.
- Same session completed handshake, RuntimeProfile, RuntimeContract, lobby, map, Ready, Start, spawn, select, movement, production, completion, attack, and resource update for 1800 ticks with no desync or order rejection.
- Direct IP was used; LAN discovery remains NOT IMPLEMENTED / NOT VERIFIED in this phase.

## Internet Test

- Authorized server region: NOT DEPLOYED (no authorized VPS was available; no purchase or external mutation was made).
- Public IPv4: NOT AVAILABLE in the current network (CGNAT).
- macOS → public Internet: NOT VERIFIED.
- iOS → public Internet: NOT VERIFIED.

## Compatibility

- Frozen protocol versions: transport handshake 7, handshake schema 1, orders 23, RuntimeContract schema 1.
- Controlled incompatible gameplay-core fixtures: PASS (connection rejected before gameplay).
- Presentation localization compatibility remains based on the shared `ra2-presentation-capability-v2` contract.

## Regression and Gates

- Python packaging/regression tests: 424 passed.
- Multiplayer/dedicated NUnit tests: 176 passed.
- DS1–DS10 smoke schema and process probes: PASS; physical DS3/DS6/DS7 evidence: PASS.
- Linux x86_64 and ARM64 archive audits: PASS; ARM64 container readiness, non-root, read-only root, listener, and clean exit: PASS.
- Android physical validation: PENDING.
- A later Local rerun was blocked only because the connected iOS devices were locked; the committed Phase 1.2 baseline remains PASS.

## Git

- Repository: `/Users/kevin/Projects/ra2-mac`
- Branch: `codex/multiplayer-phase2`
- Starting commit: `be43a42d06d3640eff70a9f831219d261e83a70f`
- Design commit: `0e36f8f2168c02f63e083b784b7fcb5c24c3cb85`
- Implementation commit: `83df711f90130765ba795a392c0629bb1edc595c`

## Files Changed

Dedicated runtime and policy: `engine/OpenRA.Game/**`, `engine/OpenRA.Server/Program.cs`, `engine/OpenRA.Test/**`.

Delivery and audit: `packaging/server/**`, `packaging/tests/test_dedicated_server_*.py`, `server/**`, `launch-dedicated.sh`, `Makefile`.

Smoke and documentation: `tools/run_dedicated_server_smoke.py`, `ios/scripts/run-macos-ios-multiplayer-smoke.sh`, `docs/dedicated-server.md`, `docs/server-resource-audit.md`, `docs/testing/dedicated-server-test-schema.md`.

## Remaining Issues

- P1: authorized Linux public host and public IPv4 are required for the Internet Multiplayer gate.
- P1: Android ARM64 physical device is required to close the Phase 1 matrix.
- P1: x86_64 container runtime still needs an x86_64 Docker/build host; the x86_64 archive itself is built and audited.
- P2: LAN discovery is not implemented / verified in this phase.

Phase 3 is intentionally not started.
