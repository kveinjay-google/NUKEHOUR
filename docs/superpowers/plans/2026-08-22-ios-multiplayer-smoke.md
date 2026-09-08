# iOS Multiplayer End-to-End Smoke Test Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make iOS local-network access explicit and prove that two physical iOS devices can join the same RA2 match, start it, and remain synchronized.

**Architecture:** Add the required iOS local-network privacy declaration. Add an environment-gated smoke-test coordinator in `OpenRA.Game` that reuses the production TCP server, lobby orders, map loading, and world tick paths; `BlankLoadScreen` invokes it only when the smoke role is explicitly set. A shell runner launches one device as host and the other as client, then retrieves durable JSON journals from both devices.

**Tech Stack:** C#/.NET 8 iOS AOT, OpenRA TCP server and order manager, NUnit source/behavior policy tests, `xcrun devicectl`, POSIX shell.

---

### Task 1: Declare iOS local-network access

**Files:**
- Modify: `ios/OpenRA.iOS/Info.plist`
- Test: `engine/OpenRA.Test/IosMultiplayerPolicyTest.cs`

- [ ] Write a failing NUnit test that loads `Info.plist` and requires a non-empty `NSLocalNetworkUsageDescription` explaining LAN game discovery, hosting, and joining.
- [ ] Run `dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~IosMultiplayerPolicyTest` and confirm failure.
- [ ] Add the Chinese privacy string `用于在同一局域网内发现、创建和加入多人游戏。`.
- [ ] Re-run the focused test and confirm it passes.

### Task 2: Add a production-path multiplayer smoke coordinator

**Files:**
- Create: `engine/OpenRA.Game/Support/IosMultiplayerSmoke.cs`
- Modify: `engine/OpenRA.Mods.Common/LoadScreens/BlankLoadScreen.cs`
- Test: `engine/OpenRA.Test/IosMultiplayerPolicyTest.cs`

- [ ] Write failing tests for disabled-by-default parsing, host/client parameter validation, JSON journal state transitions, and the load-screen opt-in call.
- [ ] Run the focused tests and confirm failure because the coordinator does not exist.
- [ ] Implement `IosMultiplayerSmoke.TryStart()` using `OPENRA_IOS_MULTIPLAYER_SMOKE_ROLE`, `..._ENDPOINT`, `..._MAP`, `..._PORT`, and `..._RUN_ID`.
- [ ] Host with `Game.CreateServer`, join through `Game.JoinServer`, mark both clients ready, issue `startgame` only as admin after two clients are present, and write `PASSED` only after at least 100 synchronized world ticks.
- [ ] Client joins the supplied endpoint, marks itself ready, and applies the same world-tick success gate.
- [ ] Persist atomic JSON journals under `Documents/OpenRA/Logs/MultiplayerSmoke/<run-id>/<role>.json`, including connection state, client count, lobby frame, world frame, failure detail, and timestamps.
- [ ] Re-run the focused tests and the existing loading/network-related tests.

### Task 3: Automate two-device deployment and evidence collection

**Files:**
- Create: `ios/scripts/run-multiplayer-smoke.sh`
- Test: `engine/OpenRA.Test/IosMultiplayerPolicyTest.cs`

- [ ] Write a failing source-policy test requiring distinct host/client device IDs, explicit environment variables, bounded timeout, journal retrieval, and assertions for `PASSED` plus at least 100 world ticks on both devices.
- [ ] Implement the runner with iPad host default `C5CA8947-3880-593B-84D6-B57DE642A8FD` and iPhone client default `77B248CD-CB9B-583C-8E67-7FA48E6409A5`.
- [ ] Make endpoint discovery explicit through an override; never silently claim success when the LAN address is unknown.
- [ ] Re-run focused tests and `sh -n ios/scripts/run-multiplayer-smoke.sh`.

### Task 4: Release build and physical-device validation

**Files:**
- Evidence: `docs/testing/results/multiplayer-smoke-<run-id>/`

- [ ] Build `Release` for `ios-arm64` without a debugger and verify the app signature.
- [ ] Install the identical signed build on iPad and iPhone.
- [ ] Start the host, start the client, permit Local Network access if iOS prompts, and run for at least 100 world ticks.
- [ ] Retrieve both journals and server/client log tails; require the same map, two clients, `PASSED`, and no disconnect, exception, or desync report.
- [ ] Relaunch the normal build on the iPad after the smoke test.
