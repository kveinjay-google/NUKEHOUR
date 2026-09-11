# NUKE HOUR Multiplayer Phase 3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a secure public Online Lobby and room directory that discovers existing NUKE HOUR Dedicated Servers while permanently preserving Local Multiplayer and the shared gameplay protocol.

**Architecture:** A standalone FastAPI service owns an in-memory short-lived official-room directory. Dedicated servers register and heartbeat actual runtime/session metadata; clients browse or resolve room codes over HTTPS, precheck compatibility, then connect directly to the existing game server. Local LAN discovery and Direct IP never call the Online service.

**Tech Stack:** Python 3.11, FastAPI 0.136, Pydantic 2.13, Uvicorn 0.45, C#/.NET OpenRA engine, NUnit, Python unittest, Docker, POSIX shell.

---

### Task 1: Freeze the Phase 3 contract and service boundaries

**Files:**
- Create: `docs/superpowers/specs/2026-09-09-nukehour-multiplayer-phase3-design.md`
- Create: `docs/superpowers/plans/2026-09-09-nukehour-multiplayer-phase3.md`

- [x] **Step 1: Record the starting repository and branch**

Run `git branch --show-current && git rev-parse HEAD && git status --short` and record `codex/multiplayer-phase2` plus starting commit `457baeb282ec71068c1e1c07da2868d959cd3433` while preserving every existing untracked path.

- [x] **Step 2: Write and self-review the approved design**

Capture the metadata-only Lobby boundary, FastAPI deployment, server-derived compatibility metadata, direct gameplay connection, strict Local/Online isolation, security controls, tests, and completion gate. Verify with `rg -n 'TBD|TODO|implement later' docs/superpowers/specs/2026-09-09-nukehour-multiplayer-phase3-design.md`; expected output is empty.

- [x] **Step 3: Commit the design separately**

Stage only the design file, display `git diff --cached --name-status`, and commit `docs: design multiplayer phase 3`.

### Task 2: Build the room-directory domain with TDD

**Files:**
- Create: `lobby/nukehour_lobby/__init__.py`
- Create: `lobby/nukehour_lobby/models.py`
- Create: `lobby/nukehour_lobby/directory.py`
- Create: `packaging/tests/test_online_lobby_directory.py`

- [ ] **Step 1: Write failing domain tests**

Tests must instantiate the real directory with an injected clock and code generator and assert: valid registration returns UUID/code/token; codes use `ABCDEFGHJKMNPQRSTUVWXYZ23456789`; collision retries; duplicate `serverId` replaces only that server session; heartbeat changes status/map/players; invalid registration token is rejected; unregister removes visibility; stale rooms disappear; restart can recover through re-registration; sorting puts joinable waiting rooms first.

- [ ] **Step 2: Verify RED**

Run `python3 -m unittest packaging.tests.test_online_lobby_directory -v`; expected result is import or missing-API failures caused by the absent service.

- [ ] **Step 3: Implement the minimal domain**

Define strict request/public response models and a locked `RoomDirectory` whose public responses never contain registration credentials, internal topology, full RuntimeContract digests, passwords, or client identity. Use `uuid.uuid4()`, `secrets.choice()`, and `secrets.token_urlsafe(32)`; compare tokens with `secrets.compare_digest`.

- [ ] **Step 4: Verify GREEN and commit**

Run the focused test until all cases pass, run `git diff --check`, display the staged list, and commit `feat(lobby): add secure room directory domain`.

### Task 3: Expose the versioned HTTPS-facing API securely

**Files:**
- Create: `lobby/nukehour_lobby/rate_limit.py`
- Create: `lobby/nukehour_lobby/app.py`
- Create: `lobby/requirements.txt`
- Create: `packaging/tests/test_online_lobby_api.py`

- [ ] **Step 1: Write failing API tests**

Use FastAPI `TestClient` against the real application factory. Cover `/health`, `/ready`, room list/details/code lookup, Bearer registration auth, heartbeat token auth, unregister, 401/404/409/422/429 mappings, case-insensitive codes, a bounded JSON body, forbidden extra fields, control characters, malformed public endpoints, secret-safe responses/logs, security headers, disabled production docs/CORS, and independent public/server route rate limits.

- [ ] **Step 2: Verify RED**

Run `python3 -m unittest packaging.tests.test_online_lobby_api -v`; expected failures must identify missing routes or controls rather than broken test setup.

- [ ] **Step 3: Implement API and safety middleware**

Create `create_app(settings, directory, clock)` with explicit response models, router-level Bearer dependency on registration, per-session heartbeat token dependency, request-size middleware, Trusted Host middleware, security-header middleware, exception sanitization, and an injected token-bucket limiter. Production sets `debug=False`, `docs_url=None`, `redoc_url=None`, and `openapi_url=None`; it neither enables CORS nor trusts arbitrary forwarded headers.

- [ ] **Step 4: Verify GREEN and commit**

Run focused domain/API tests, inspect dependency versions, scan for `debug=True|--reload|allow_origins|shell=True|os.system`, then stage the complete list and commit `feat(lobby): expose authenticated room API`.

### Task 4: Package and audit the Lobby Service

**Files:**
- Create: `lobby/run-lobby.sh`
- Create: `lobby/lobby-config.env.example`
- Create: `lobby/Dockerfile`
- Create: `lobby/.dockerignore`
- Create: `lobby/compose.test.yaml`
- Create: `packaging/lobby/build_lobby.py`
- Create: `packaging/lobby/audit_lobby_bundle.py`
- Create: `packaging/tests/test_online_lobby_delivery.py`
- Modify: `Makefile`

- [ ] **Step 1: Write failing delivery/audit tests**

Require pinned dependencies, non-root UID, read-only-compatible runtime, explicit healthcheck, no source-repository copy, no credentials/default token, no commercial game resources, Linux x86_64/ARM64 support, deterministic inventory/SHA-256, no world-writable executable, and no unsafe reload/debug/proxy-header flags.

- [ ] **Step 2: Verify RED**

Run `python3 -m unittest packaging.tests.test_online_lobby_delivery -v`; expected failures identify missing packaging files.

- [ ] **Step 3: Implement artifact and container**

Build an explicit allowlisted Lobby bundle with source, pinned requirements, license/config/run files, inventory, version metadata, archive and SHA-256. The Docker runtime installs only pinned dependencies, runs as UID 10002, accepts a secret only from environment, and exposes the API port without bundling game/server/client resources.

- [ ] **Step 4: Verify GREEN, container runtime, and commit**

Run focused tests, build both architecture declarations, build/run the native Docker image when the local runtime is available, call `/health` and `/ready`, audit the image/bundle, display staged files, and commit `build(lobby): add audited deployment artifact`.

### Task 5: Integrate Dedicated Server registration and heartbeat

**Files:**
- Create: `engine-patches/0027-nukehour-online-lobby.patch`
- Modify generated: `engine/OpenRA.Game/Settings.cs`
- Create generated: `engine/OpenRA.Game/Server/OnlineLobbyRegistration.cs`
- Modify generated: `engine/OpenRA.Server/Program.cs`
- Create generated test: `engine/OpenRA.Test/NukeHourOnlineLobbyRegistrationTest.cs`
- Modify: `engine-patches/README.md`
- Modify: `packaging/server/run-server.sh`
- Modify: `server/server-config.env.example`
- Modify: `server/server-files.json`
- Modify: `packaging/tests/test_dedicated_server_delivery.py`

- [ ] **Step 1: Write failing server-integration tests**

Require server-only Lobby URL, public endpoint/port, region, credential and heartbeat settings; disabled-by-default Local behavior; server-derived protocol/runtime/map/player/status metadata; ready-only registration; bounded heartbeat interval; re-register on 404; best-effort unregister; cancellation; and gameplay isolation from all HTTP failures. Tests must assert credentials never enter status, logs, client settings, or public metadata.

- [ ] **Step 2: Verify RED**

Run the focused NUnit and Python delivery tests and confirm missing settings/client behavior causes the failures.

- [ ] **Step 3: Implement minimal C# registration client**

Use one `HttpClient` with explicit timeouts and `Authorization: Bearer`. Read actual `ProtocolVersion`, manifest, `RuntimeProfile`, `RuntimeContract.ResourceCapability`, dedicated state, map and validated client count. Do not transmit the RuntimeContract digest. Start after ready, heartbeat every configured 15 seconds, re-register after service state loss, and never let Lobby exceptions terminate the server loop.

- [ ] **Step 4: Update delivery and replay patch**

Add only non-secret configuration keys and the new assembly output to the server allowlist. Generate patch 0027 from a verified post-0026 pristine engine baseline, reject fuzz/offset, and verify materialized files byte-for-byte.

- [ ] **Step 5: Verify and commit**

Run focused server tests, DS1-DS10, Phase 2 artifact audit, and patch-delivery verification; display staged files and commit `feat(server): register dedicated rooms with lobby`.

### Task 6: Add Online room parsing and compatibility precheck

**Files:**
- Modify generated: `engine/OpenRA.Game/Network/GameServer.cs`
- Create generated: `engine/OpenRA.Game/Network/OnlineRoomDirectory.cs`
- Modify generated: `engine/OpenRA.Mods.Common/WebServices.cs`
- Create generated tests: `engine/OpenRA.Test/NukeHourOnlineRoomDirectoryTest.cs`
- Modify: `engine-patches/0027-nukehour-online-lobby.patch`
- Modify: `mods/ra2/mod.yaml`

- [ ] **Step 1: Write failing client-domain tests**

Cover strict JSON parsing, stable sort, room detail/code endpoints, endpoint hiding from display, exact error categories, compatibility matches/mismatches for handshake schema/orders/engine/mod/version/runtime capability, and the rule that a positive precheck does not bypass the normal server handshake.

- [ ] **Step 2: Verify RED**

Run the focused NUnit tests and confirm missing online-room APIs cause the failure.

- [ ] **Step 3: Implement parser/client/precheck**

Add a cancellable polling client using `HttpClientFactory`, bounded response reads and explicit JSON DTOs. Convert compatible selected rooms to existing `ConnectionTarget` only at join time. Configure one `OnlineLobby` base URL in `WebServices`; do not embed VPS addresses or credentials.

- [ ] **Step 4: Verify GREEN and commit**

Run focused and complete multiplayer compatibility tests, update patch 0027 and its replay audit, display staged files, and commit `feat(client): add online room compatibility precheck`.

### Task 7: Add independent Local and Online UI paths

**Files:**
- Modify generated: `engine/OpenRA.Mods.Common/Widgets/Logic/ServerListLogic.cs`
- Modify generated: `engine/OpenRA.Mods.Common/Widgets/Logic/MultiplayerLogic.cs`
- Modify generated: `engine/mods/common/chrome/multiplayer-browser.yaml`
- Modify generated: `engine/mods/common/fluent/chrome.ftl`
- Modify generated: `engine/mods/common/fluent/chrome.zh-cn.ftl`
- Modify generated tests: `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs`
- Create generated tests: `engine/OpenRA.Test/NukeHourOnlineMultiplayerUiTest.cs`
- Modify: `engine-patches/0027-nukehour-online-lobby.patch`

- [ ] **Step 1: Write failing UI policy tests**

Require explicit Local/Online tabs, permanent Direct IP, no Local HTTP request, Online polling, room name/map/player/region/status/password/compatibility display, details, code lookup, offline/empty/disappeared/full/started/mismatch/connection/password messages, hidden endpoint, disabled join for incompatible/non-joinable rooms, and iOS touch-safe bounds.

- [ ] **Step 2: Verify RED**

Run the focused UI tests and confirm absent widgets/logic cause expected failures.

- [ ] **Step 3: Implement the smallest UI change**

Default to Local. Switch modes by cancelling the previous provider and clearing selection. Local subscribes only to LAN/nearby sources; Online starts 10-second polling and room-code lookup. Direct Connect and Create Local Server remain available without service access. Joining uses the existing `ConnectionLogic.Connect` and existing lobby.

- [ ] **Step 4: Verify GREEN and commit**

Run UI/layout tests, render/launch the desktop browser, build iOS ARM64, update the AOT registry if required, replay patch 0027, display staged files, and commit `feat(ui): separate local and online multiplayer`.

### Task 8: Synchronize and build Android current version

**Files:**
- Apply equivalent engine patch/materialized changes only to current Android repository: `/Users/kevin/Projects/RA2Android`
- Modify Android AOT/generated registries only when build output proves necessary.

- [ ] **Step 1: Confirm current Android branch and preserve user work**

Record branch/status without inspecting any Build 4 recovery path. Do not clean, reset, restore, stash, or remove untracked files.

- [ ] **Step 2: Port tests before implementation**

Apply the Phase 3 client-domain/UI tests, run them RED, then synchronize only the tested engine/mod changes and run GREEN.

- [ ] **Step 3: Build Android ARM64**

Build the existing Release ARM64 target, record warnings/errors and artifact SHA-256. Mark Android Online `BUILD VERIFIED / NOT DEVICE VERIFIED` unless a real Android device becomes available.

- [ ] **Step 4: Commit Android separately**

Display the complete staged list, commit without push, and retain the Phase 1 physical-device gate as pending.

### Task 9: Document, deploy, and execute real public acceptance

**Files:**
- Create: `docs/online-lobby.md`
- Create: `docs/online-lobby-api.md`
- Create: `docs/testing/online-lobby-test-schema.md`
- Create evidence: `docs/testing/results/online-lobby-phase3-*/`
- Create: `docs/nukehour-multiplayer-phase3-report.md`

- [ ] **Step 1: Write implementation-accurate documentation**

Document architecture, exact routes/request/response/errors/auth/rate limits, registration, heartbeat, lifecycle, room codes, compatibility, privacy, security, deployment, observability, testing, and permanent Local/Online separation. Do not document routes or fields absent from code.

- [ ] **Step 2: Build and deploy audited artifacts**

Use only the already-authorized VPS credential source without displaying or committing secrets. Deploy a temporary Lobby behind HTTPS and an updated dedicated server; expose only required ports, verify local/remote SHA-256, `/health`, `/ready`, registration, heartbeat, room appearance, timeout cleanup and unregister.

- [ ] **Step 3: Execute real Apple online flows**

Using physical iOS and macOS, join through Room List and Room Code without manually entering IP, enter the same dedicated room, pass normal lobby/ready/start/gameplay smoke, and verify a stopped Lobby does not interrupt the game session.

- [ ] **Step 4: Re-run Local acceptance**

Verify physical iOS host to macOS client and macOS host to physical iOS client through LAN Direct `IP:1234`; no account, Internet, Lobby, or Dedicated Server dependency is allowed.

- [ ] **Step 5: Run negative/security/resource gates**

Verify invalid registration credential, malformed metadata, rate limits, room-code enumeration resistance, controlled precheck mismatch, server-authority mismatch rejection, secret scan, HTTPS, and commercial-assets `NONE` for Lobby and Dedicated bundles.

### Task 10: Final verification and report

**Files:**
- Modify: `docs/nukehour-multiplayer-phase3-report.md`

- [ ] **Step 1: Run fresh full verification**

Run all Lobby Python tests, packaging tests, relevant and full NUnit suites, macOS/iOS/Android Release builds, Lobby x86_64/ARM64 builds/audits, Dedicated x86_64/ARM64 builds/audits, DS1-DS10, patch replay, secret scan, real Online evidence validation, and Local regression evidence validation.

- [ ] **Step 2: Complete the prescribed report exactly**

Fill every Phase 3 report field with `PASS`, `FAIL`, `SKIPPED`, `PENDING`, artifact hashes, public deployment evidence, starting/final commits, complete changed-file lists and remaining P0/P1/P2 issues. Mark `COMPLETE` only when every required Phase 3 gate has fresh evidence.

- [ ] **Step 3: Commit and stop**

Display the complete staged list, commit the final evidence/report, preserve branches and untracked work, do not push, and do not begin server allocation, accounts, friends, matchmaking, ranking, chat, or any later phase.
