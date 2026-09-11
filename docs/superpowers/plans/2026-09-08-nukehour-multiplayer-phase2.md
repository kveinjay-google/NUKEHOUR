# NUKE HOUR Multiplayer Phase 2 Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan.

**Goal:** Ship and validate a minimal Linux NUKE HOUR dedicated-server artifact that reuses the Phase 1 protocol, contains no commercial game resources, works with macOS and iOS over the public internet, and does not regress Local Multiplayer.

**Architecture:** Extend the existing OpenRA dedicated entry point only for server identity, one-room lifecycle, readiness/status reporting, headless presentation-resource handling, and listen-address control. Build an audited, allowlisted Linux artifact and optional non-root container around the same executable. Keep handshake, RuntimeContract, map/session compatibility, orders, and gameplay simulation shared with Local Multiplayer.

**Tech Stack:** C#/.NET 6 OpenRA engine, Python 3 packaging/audit tests, POSIX shell, Docker multi-stage build, NUnit, unittest.

---

### Task 1: Freeze baseline and server delivery contract

**Files:**
- Create: `packaging/tests/test_dedicated_server_delivery.py`
- Create: `server/server-files.json`
- Create: `server/server-config.env.example`

- [ ] Record both repository starting commits, branches, and tracked cleanliness.
- [ ] Add failing tests for required server configuration, explicit artifact allowlists, Linux x64/ARM64 target support, TCP `1234` default plus dynamic ports, and forbidden retail-resource patterns.
- [ ] Run the focused unittest and verify each new assertion fails for the missing delivery implementation.
- [ ] Add the minimal declarative manifest/config needed to make the delivery-contract tests pass.
- [ ] Re-run the focused test and the existing 395-test Python baseline.

### Task 2: Add headless server identity and presentation-resource boundary

**Files:**
- Create: `engine-patches/0026-nukehour-dedicated-server-foundation.patch`
- Modify generated workspace: `engine/OpenRA.Game/ModData.cs`
- Modify generated workspace: `engine/OpenRA.Game/Network/NukeHourNetworkCompatibility.cs`
- Modify generated workspace: `engine/OpenRA.Server/Program.cs`
- Create in patch: `engine/OpenRA.Test/NukeHourDedicatedServerTest.cs`

- [ ] Write NUnit tests that require a dedicated-server RuntimeProfile and a headless-only presentation capability provider that produces the same RuntimeContract as a valid client.
- [ ] Run the tests and confirm they fail because dedicated identity/headless capability APIs do not exist.
- [ ] Implement the smallest explicit server-only APIs; leave all client `ModData` construction and compatibility validation unchanged.
- [ ] Add a test proving missing client presentation resources still fail closed.
- [ ] Run the focused protocol/server tests and regenerate the checked-in `0026` patch from the pristine engine baseline.
- [ ] Verify the complete engine patch chain reproduces the generated workspace exactly for the allowlisted files.

### Task 3: Implement one-room lifecycle, health, and clean crash behavior

**Files:**
- Modify: `engine-patches/0026-nukehour-dedicated-server-foundation.patch`
- Modify generated workspace: `engine/OpenRA.Server/Program.cs`
- Modify generated workspace: `engine/OpenRA.Game/Settings.cs`
- Modify generated workspace: `engine/OpenRA.Game/Server/Server.cs`
- Modify in patch: `engine/OpenRA.Test/NukeHourDedicatedServerTest.cs`

- [ ] Add failing tests for lifecycle mapping, readiness distinction, validated-client observation, idle timeout validation, listen-address parsing, one-room exit, normal-client disconnect survival, and non-zero fatal errors.
- [ ] Implement operational states mapped onto existing `ServerState`; do not create a gameplay state machine.
- [ ] Write the status file atomically with product/build/protocol/map/port/readiness/state and no secrets or client IPs.
- [ ] Replace the implicit IPv4/IPv6-any binding with validated configurable addresses while retaining compatible defaults.
- [ ] Exit cleanly after the completed room is empty; never silently restart an ended room.
- [ ] Preserve unhandled exception logging and non-zero process termination.
- [ ] Re-run dedicated, protocol, direct-TCP, and Local Multiplayer regression tests.

### Task 4: Build and audit the Linux server artifact

**Files:**
- Create: `packaging/server/build_server.py`
- Create: `packaging/server/audit_server_bundle.py`
- Create: `packaging/server/run-server.sh`
- Create: `packaging/server/healthcheck.sh`
- Create: `packaging/tests/test_dedicated_server_builder.py`
- Create: `packaging/tests/test_dedicated_server_audit.py`
- Modify: `Makefile`

- [ ] Add failing builder tests for deterministic staging, required DLL/config/map/license selection, forbidden UI/art/client/platform files, and path traversal/symlink defenses.
- [ ] Add failing audit tests that reject retail filenames, MIX/EXE/audio/video signatures, secrets, world-writable executables, undeclared files, and mismatched hashes.
- [ ] Implement the allowlist builder with separate `linux-x64` and `linux-arm64` runtime identifiers.
- [ ] Implement a bare-Linux launcher with validated environment/config values and no restart loop.
- [ ] Emit archive, inventory, version/protocol metadata, and SHA-256.
- [ ] Add `server-build`, `server-audit`, and focused test Make targets.
- [ ] Build Linux x86_64; build ARM64 if the runtime pack is available without architecture-specific source changes.
- [ ] Audit the extracted bundle and final archive; commercial resources included must equal `NONE`.

### Task 5: Add container delivery without making Docker a protocol dependency

**Files:**
- Create: `server/Dockerfile`
- Create: `server/.dockerignore`
- Create: `server/compose.test.yaml`
- Modify: `packaging/tests/test_dedicated_server_delivery.py`

- [ ] Add failing static tests for multi-stage build, non-root final user, explicit TCP port, no privileged/host mode, and final-image copy limited to the audited artifact.
- [ ] Implement the minimal image and compose test definition.
- [ ] Start an available local Docker/Colima runtime when safe, build the image, inspect its user/files, and run the health probe.
- [ ] If no container runtime can be made available, retain bare-Linux completion evidence and mark Docker runtime verification P1 rather than faking it.

### Task 6: Automated DS1-DS10 integration harness

**Files:**
- Create: `tools/run_dedicated_server_smoke.py`
- Create: `packaging/tests/test_dedicated_server_smoke.py`
- Create: `docs/testing/dedicated-server-test-schema.md`

- [ ] Add parser/fixture tests for DS1-DS10 evidence and rejection reason mapping.
- [ ] Launch a fresh server support directory and prove DS1 start, DS2 listen/readiness, DS5 configured map load, and DS9 signal-driven clean shutdown.
- [ ] Reuse the real Phase 1 handshake client fixture for DS3 valid admission and DS4 protocol/runtime/mod/engine mismatches.
- [ ] Reuse OpenRA order/session tests for DS6 start, DS7 order acceptance, and DS8 disconnect independence.
- [ ] Run DS10 protocol and Local host regression suites without changing their expected protocol values.
- [ ] Persist sanitized, machine-readable evidence under `docs/testing/results/dedicated-server-*`.

### Task 7: Documentation and resource audit

**Files:**
- Create: `docs/server-resource-audit.md`
- Create: `docs/dedicated-server.md`
- Create: `docs/nukehour-multiplayer-phase2-report.md`
- Modify: `README.md`

- [ ] Document gameplay-required versus presentation-only files and the runtime proof used to classify them.
- [ ] State `Commercial assets included: NONE`; if runtime evidence contradicts this, stop public deployment with `DEDICATED_SERVER_RESOURCE_BLOCKER`.
- [ ] Document bare-Linux build/run/config/ports/maps/logs/shutdown/health/Docker/compatibility/testing.
- [ ] Document GPL/source obligations and keep the server source in the same public source tree.
- [ ] Seed the final report with exact automated results and unresolved physical/public gates.

### Task 8: Local real-client dedicated-server validation

**Files:**
- Modify: `docs/nukehour-multiplayer-phase2-report.md`
- Create evidence: `docs/testing/results/dedicated-server-local-*/`

- [ ] Start one audited dedicated process on the development Mac with a fresh support directory and `AdvertiseOnline=False`.
- [ ] Connect a macOS client through Direct IP and validate handshake, RuntimeContract, lobby, map, Ready, Start, spawn, gameplay orders, and stable ticks.
- [ ] Install/launch the matching build on a physical iPhone/iPad and repeat against the same independent process.
- [ ] Join macOS and iOS simultaneously, execute movement/production/combat, and sustain at least 1800 world ticks without desync or unexpected disconnect.
- [ ] Disconnect one ordinary client and prove the server process does not exit because a client-host disappeared.

### Task 9: Temporary Linux public-internet validation

**Files:**
- Modify: `docs/nukehour-multiplayer-phase2-report.md`
- Create evidence: `docs/testing/results/dedicated-server-internet-*/`

- [ ] Discover an already-authorized temporary Linux VPS/VM destination; do not purchase infrastructure or expose unrelated ports.
- [ ] Deploy only the audited archive/image and open only its selected TCP game port.
- [ ] Verify Linux x86_64 runtime, public IPv4 Direct Connect, readiness, logs, and artifact SHA-256.
- [ ] Connect macOS over the public route and complete gameplay.
- [ ] Connect physical iOS over an independent internet path where available and complete gameplay.
- [ ] Run both clients in the same public game for at least 1800 ticks and record map, desync, disconnect, and result evidence.
- [ ] Remove or stop the temporary test instance when testing ends without deleting user infrastructure.

### Task 10: Compatibility negatives and Local Multiplayer regression

**Files:**
- Modify: `docs/nukehour-multiplayer-phase2-report.md`
- Create evidence: `docs/testing/results/dedicated-server-compatibility-*/`
- Create evidence: `docs/testing/results/multiplayer-phase2-local-regression-*/`

- [ ] Use controlled fixtures/builds to verify protocol, RuntimeContract missing/mismatch, mod, engine, and map rejection; never corrupt retail content.
- [ ] Re-run physical iOS Host to macOS Client on LAN Direct `IP:1234`.
- [ ] Re-run physical macOS Host to iOS Client on LAN Direct `IP:1234`.
- [ ] Confirm Local Multiplayer still does not depend on accounts, cloud, lobby, or dedicated infrastructure.
- [ ] Keep `Phase 1 Android Physical Gate: PENDING` unless a real ARM64 Android device becomes available.

### Task 11: Final verification and commits

**Files:**
- Modify: `docs/nukehour-multiplayer-phase2-report.md`

- [ ] Run the complete Python packaging suite, relevant NUnit suite, full build, server artifact audit, DS1-DS10, and patch-delivery verification.
- [ ] Recheck both repositories for unrelated user changes and leave them untouched.
- [ ] Fill every final-report field with evidence-backed PASS/FAIL/SKIPPED status and exact commit/artifact hashes.
- [ ] Show the complete staged file list before each commit; never stage untracked historical artifacts.
- [ ] Commit implementation and final evidence on `codex/multiplayer-phase2` without pushing or starting Phase 3.
