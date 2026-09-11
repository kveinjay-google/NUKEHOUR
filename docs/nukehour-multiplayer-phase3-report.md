# NUKEHOUR MULTIPLAYER PHASE 3 REPORT

## Status

COMPLETE

All Phase 3 completion gates were exercised successfully. Android physical
validation remains a separate Phase 1 gate and did not block Phase 3. The later
repository-only instruction prevented work in the separate Android repository,
so Android Online build synchronization is recorded as P1 below.

## Architecture

Local Multiplayer: Permanent independent Local entry; LAN hosting, Direct
`IP:port`, TCP 1234, and iOS/Android/macOS host capability do not require an
account, Internet, Lobby, cloud service, or Dedicated Server.

Online Multiplayer: Online Room List and Join by Code resolve a Dedicated
Server endpoint through HTTPS, run compatibility precheck, then use the same
existing multiplayer protocol directly with that server.

Lobby: FastAPI/Pydantic/Uvicorn single-process, in-memory, short-lived official
room directory with 45-second stale expiry. It stores no account, device, local
path, imported-resource, password, or full RuntimeContract data.

Dedicated Server: The Phase 2 gameplay server adds best-effort authenticated
registration, heartbeat, re-registration, and unregister integration. Runtime
state is authoritative for compatibility, map, status, and player count.

Gameplay path: `Client -> Dedicated Server` over the existing TCP gameplay
protocol. The Lobby neither relays nor interprets gameplay.

## Lobby Service

Platform: Linux x86_64 and Linux ARM64 deployment bundles; production uses a
non-root, read-only Docker container behind Nginx.

Artifact:

- x86_64: `artifacts/phase3-a7378f1/lobby/NUKE-HOUR-Lobby-1.0.3-phase3-linux-x64.tar.gz`
  (`6e42097b34a6e86071dcd20cb29f020542bef134d861a089193a3e79110485a7`)
- ARM64: `artifacts/phase3-a7378f1/lobby/NUKE-HOUR-Lobby-1.0.3-phase3-linux-arm64.tar.gz`
  (`6a3d831bb7ce63fdd8eef1e03b0997441e0d76877f96f8ecc9189b2364a27568`)

Version: NUKE HOUR 1.0.3, Phase 3.

Commit: Artifact source `a7378f1bcee6ea4313ef9f76b57871322fc54eb7`.

## Public Deployment

Region: `cn-hangzhou`; public address is redacted as `8.135.x.x`.

HTTPS: PASS at `https://lobby.nukehour.com/`; valid HTTPS front end with HTTP
redirect, TLS 1.2/1.3, HSTS, `nosniff`, and frame denial.

Health: PASS, HTTP 200 with `status=ok`.

Ready: PASS, HTTP 200 with `ready=true`.

## Registration

Valid server: PASS, HTTP 201; duplicate registration is handled idempotently.

Invalid credential: PASS, HTTP 401.

Heartbeat: PASS, HTTP 200; status, readiness, map, and player count update from
the live Dedicated Server.

Timeout cleanup: PASS; room visible before timeout and absent after 48 seconds.

Unregister: PASS, HTTP 204 followed by HTTP 404 lookup.

## Rooms

Room list: PASS; joinable official room is listed and stable-sorted.

Room details: PASS, HTTP 200 for a live room.

Room code: PASS; six-character case-insensitive code resolves, invalid code is
not found, confusing characters are excluded, and collision retries are tested.

Status: PASS; `WAITING` and `IN_GAME` lifecycle updates verified. Final public
state is `WAITING`, ready, and official.

Player count: PASS; heartbeat update and final 0/8 state verified.

Map: PASS; reported by the live server rather than deployment configuration.

## Client

macOS: PASS for Online list/details/precheck/join and physical public session.

iOS: PASS on physical iPad Air for Join by Code and physical public session.

Android: NOT MODIFIED / NOT BUILD VERIFIED in this phase because the active
instruction restricted all work to `/Users/kevin/Projects/ra2-mac`; the Android
project is separate. No Android physical result is claimed.

## Online Real Test

macOS Room List Join: PASS; `online-room-list-matched`, two clients, tick 1800.

iOS Room List Join: The physical iPad used Room Code in the final two-client
matrix; the shared iOS Online list implementation is covered by automated tests.

Room Code: PASS on physical iPad; `online-room-code-matched`.

Same Room: PASS; Mac and iPad resolved the same runtime/map/session without
manual IP entry.

Gameplay: PASS through tick 1800 on both participants, including selection,
movement, production, completed production, attack activity, and resource
update; no desync, rejection, serialization error, or unexpected disconnect.

Evidence: `artifacts/phase3-online-ipad-final-20260909T164732Z`.

## Lobby Failure Isolation

Result: PASS. The public Lobby was stopped after both clients entered gameplay.
Lobby health failed as expected, while both clients continued beyond tick 1800
without desync or disconnect. The service then restarted and the Dedicated
Server re-registered automatically.

## Compatibility

Precheck: PASS for handshake schema, orders version, engine compatibility, mod,
and runtime capability before TCP.

Server authority: PASS; normal handshake and RuntimeContract admission still run
after TCP and remain authoritative.

Mismatch rejection: PASS using controlled fixtures; an incompatible room is
rejected before TCP and a false-positive Lobby result is rejected by the server.
No user-owned retail resources were modified.

## Local Regression

iOS -> macOS: PASS; physical iPad Local Host to Mac Direct IP client, tick 1800.

macOS -> iOS: PASS; Mac Local Host to physical iPad Direct IP client, tick 1800.

Direct IP: PASS on LAN TCP 1234 in both directions, with no Online Lobby
dependency.

Evidence: `artifacts/phase3-local-regression-ipad-final2-20260909T164259Z`.

## Security

Credential: PASS. Registration credential is environment-only, mode-protected
on the server, excluded from client settings, artifacts, Git, process arguments,
logs, and public responses. A credential exposed during testing was revoked and
rotated before final acceptance.

HTTPS: PASS; public Lobby rejects production plaintext configuration.

Rate limit: PASS; fixed-window limits cover public lookup and authenticated
server mutation routes.

Input validation: PASS; bounded payload, strict lengths/characters/endpoints,
control-character rejection, and typed JSON validation are exercised.

Secret scan: PASS. Strict bundle audits reject credential-bearing paths and
embedded registration values. Repository and release examples contain only
empty/placeholding values; no deployable secret is tracked or packaged.

## Assets

Commercial assets: NONE in both Lobby bundles (10 files each) and both Dedicated
Server bundles (371 files each). Client distribution retains the user-import
model.

Dedicated artifacts:

- x86_64 SHA-256: `99ef508edce4a043139635b8b8703cee8bdc777d412f6fad0ae44a62f184c0cc`
- ARM64 SHA-256: `4e2ced2cf514de0da5ac5030cceccd954b12254a7c9a20907011099e80ac1937`

## Tests

Passed: 1,670 automated tests (1,601 NUnit, 59 Lobby/Dedicated Python, and 10
macOS clean-package tests); macOS Release build; strict patch replay; four bundle
audits; public API lifecycle; two Online physical journals; four Local physical
journals.

Failed: 0 in the final runs.

Skipped: 0 automated tests. Android build/device validation is deferred rather
than represented as a skipped or simulated test.

New regressions: NONE. An incremental iOS AOT launch failure was eliminated by a
full Release rebuild; deterministic attack-fixture fallback was added only to
the smoke harness. The final iPad build and all physical journals passed.

Known warnings: the upstream .NET 6 target is out of support, and
`NuGet.CommandLine 6.12.1` reports low-severity advisory `GHSA-g4vj-cjjj-v7hg`.

## Phase 1 Android Gate

Status: PENDING. Required future matrix remains iOS <-> Android, macOS <->
Android, and iOS + Android + macOS in one physical session.

## Git

Repository: `/Users/kevin/Projects/ra2-mac`

Branch: `codex/multiplayer-phase2`

Starting Commit: `457baeb282ec71068c1e1c07da2868d959cd3433`

Final Commit: the commit containing this report (repository HEAD in the final
handoff); verified implementation baseline `1dbea9ce6f5969269ff359c504b336cf52f31305`.

## Files Changed

- `.gitignore`
- `Makefile`
- `docs/legal/public-asset-sbom.json`
- `docs/nukehour-multiplayer-phase3-report.md`
- `docs/online-lobby-api.md`
- `docs/online-lobby.md`
- `docs/superpowers/plans/2026-09-09-nukehour-multiplayer-phase3.md`
- `docs/superpowers/specs/2026-09-09-nukehour-multiplayer-phase3-design.md`
- `docs/testing/online-lobby-test-schema.md`
- `docs/testing/results/online-lobby-phase3-20260909/summary.md`
- `engine-patches/0027-post-phase2-engine-sync.patch`
- `engine-patches/0028-online-lobby-room-directory.patch`
- `engine-patches/README.md`
- `engine/OpenRA.Game/Network/GameServer.cs`
- `engine/OpenRA.Game/Settings.cs`
- `engine/OpenRA.Game/Support/IosMultiplayerSmoke.cs`
- `engine/OpenRA.Mods.Common/Widgets/Logic/IosMultiplayerBrowserLayout.cs`
- `engine/OpenRA.Mods.Common/Widgets/Logic/IosTouchMenuLogic.cs`
- `engine/OpenRA.Mods.Common/Widgets/Logic/MultiplayerLogic.cs`
- `engine/OpenRA.Mods.Common/Widgets/Logic/ServerListLogic.cs`
- `engine/OpenRA.Server/Program.cs`
- `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs`
- `engine/OpenRA.Test/IosMultiplayerPolicyTest.cs`
- `engine/mods/common/chrome/multiplayer-browser.yaml`
- `engine/mods/common/fluent/chrome.ftl`
- `engine/mods/common/fluent/common.ftl`
- `engine/mods/common/fluent/zh-CN/chrome.ftl`
- `engine/mods/common/fluent/zh-CN/common.ftl`
- `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs`
- `ios/scripts/run-macos-ios-multiplayer-smoke.sh`
- `lobby/.dockerignore`
- `lobby/Dockerfile`
- `lobby/__init__.py`
- `lobby/compose.test.yaml`
- `lobby/lobby-config.env.example`
- `lobby/nukehour_lobby/__init__.py`
- `lobby/nukehour_lobby/app.py`
- `lobby/nukehour_lobby/directory.py`
- `lobby/nukehour_lobby/models.py`
- `lobby/nukehour_lobby/rate_limit.py`
- `lobby/requirements.txt`
- `lobby/run-lobby.sh`
- `mods/ra2/mod.yaml`
- `packaging/lobby/__init__.py`
- `packaging/lobby/audit_lobby_bundle.py`
- `packaging/lobby/build_lobby.py`
- `packaging/macos/public_clean_manifest.json`
- `packaging/public-content-manifest.json`
- `packaging/server/run-server.sh`
- `packaging/tests/test_dedicated_server_delivery.py`
- `packaging/tests/test_online_lobby_api.py`
- `packaging/tests/test_online_lobby_delivery.py`
- `packaging/tests/test_online_lobby_directory.py`
- `server/server-config.env.example`

## Commits

- `d5f1ffa` docs: design multiplayer phase 3
- `2a20109` docs: plan multiplayer phase 3
- `7d1739c` feat(lobby): add secure room directory domain
- `eaab00d` feat(lobby): expose authenticated room API
- `5a25c19` build(lobby): add audited deployment artifact
- `0200028` chore: exclude generated Python caches
- `7643ed0` fix(lobby): harden production request handling
- `8361782` build(engine): capture post phase 2 baseline
- `0faddbc` feat(multiplayer): add online room directory
- `659a07e` fix(server): keep lobby credential out of process arguments
- `a7378f1` fix(test): make physical attack smoke deterministic
- `8e8a8e0` docs: record online lobby operations and acceptance
- `1dbea9c` fix(packaging): refresh phase 3 public inventories
- Final report commit: repository HEAD in the final handoff.

## Remaining Issues

P0: NONE.

P1: Synchronize/build the separate Android Online client when work outside this
repository is authorized; complete the Phase 1 Android physical matrix when an
ARM64 device is available; plan migration away from EOL .NET 6 and update the
advisory-affected NuGet package.

P2: Optional room latency display and `nukehour://room/<code>` deep links.

Phase 3 stops here. Account, friends, matchmaking, ranking, automatic server
provisioning, server allocation/fleet, and chat were not started.
