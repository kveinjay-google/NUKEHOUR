# iOS Safe-Start Rejection Correlation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correlate every iOS safe-start rejection with its originating request and immediately display the authoritative blocker without allowing delayed responses to clear newer pending requests.

**Architecture:** Introduce strict network request and client-state types in `OpenRA.Game`, echo the request ID through the existing server rejection path, and let `LobbyLogic` delegate pending/rejection lifecycle to the pure state machine. Upgrade the Orders protocol to 23, update the two-device smoke and structural bundle audit, and deliver the result as a disjoint `0017` patch based on post-0016.

**Tech Stack:** C#/.NET 8 runtime with net6-compatible OpenRA assemblies, NUnit, MiniYaml wire serialization, Python `unittest` structural bundle audit, BSD `patch`/`diff`, .NET iOS arm64 signing toolchain.

---

## File Map

**Engine patch candidates:**

- Create `engine/OpenRA.Game/Network/LobbySafeStartRequest.cs`: strict request value object and canonical command parser/formatter.
- Create `engine/OpenRA.Game/Network/LobbySafeStartRequestState.cs`: pure pending/rejection correlation state machine.
- Modify `engine/OpenRA.Game/Network/LobbyStartAvailability.cs`: add rejection `RequestId`, strict six-field round-trip, and conversion back to availability.
- Modify `engine/OpenRA.Game/Server/ProtocolVersion.cs`: Orders 22 → 23.
- Modify `engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs`: parse request object and echo ID in every rejection.
- Modify `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs`: use the state machine and authoritative rejection result.
- Modify `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyStartStatus.cs`: map an accepted non-admin rejection to the existing localized host-only message.
- Modify `engine/OpenRA.Game/Support/IosMultiplayerSmoke.cs`: use distinct correlated request IDs.
- Modify `engine/OpenRA.Test/IosLobbyStartAvailabilityTest.cs`: protocol, state-machine, server-source, and lifecycle tests.
- Modify `engine/OpenRA.Test/IosLobbyResponsiveLayoutTest.cs`: final iOS LobbyLogic wiring contracts.
- Modify `engine/OpenRA.Test/IosLobbyMapReadinessTest.cs`: exact Ready-state command admission string.
- Modify `engine/OpenRA.Test/IosMultiplayerPolicyTest.cs`: smoke request/rejection correlation contracts.

**Outer repository delivery:**

- Modify `packaging/audit_ios_touch_runtime_bundle.py`.
- Modify `packaging/tests/test_ios_touch_runtime_bundle.py`.
- Create `engine-patches/0017-ios-safe-start-rejection-correlation.patch`.
- Modify `engine-patches/README.md`.
- Keep `packaging/public-content-manifest.json` unchanged unless inspection finds an already-existing entry for an actually changed file.

## Task 1: Strict Safe-Start Request and Rejection Wire Types

**Files:**

- Create: `engine/OpenRA.Game/Network/LobbySafeStartRequest.cs`
- Modify: `engine/OpenRA.Game/Network/LobbyStartAvailability.cs`
- Modify: `engine/OpenRA.Game/Server/ProtocolVersion.cs`
- Test: `engine/OpenRA.Test/IosLobbyStartAvailabilityTest.cs`

- [ ] **Step 1: Write failing request/rejection protocol tests**

Add NUnit cases that require this public API:

```csharp
var request = new LobbySafeStartRequest("map-a", 7);
Assert.That(request.ToCommand(), Is.EqualTo("startgame_safe map-a 7"));
Assert.That(LobbySafeStartRequest.TryParse("map-a 7", out var parsed), Is.True);
Assert.That(parsed, Is.EqualTo(request));

Assert.That(LobbySafeStartRequest.TryParse("map-a 0", out _), Is.False);
Assert.That(LobbySafeStartRequest.TryParse("map-a -1", out _), Is.False);
Assert.That(LobbySafeStartRequest.TryParse("map-a +1", out _), Is.False);
Assert.That(LobbySafeStartRequest.TryParse("map-a 01", out _), Is.False);
Assert.That(LobbySafeStartRequest.TryParse("map a 1", out _), Is.False);
Assert.That(LobbySafeStartRequest.TryParse("map-a 1 extra", out _), Is.False);
```

Change `StructuredRejectionRoundTripsAndFailsClosed` to construct:

```csharp
var rejection = new LobbyStartRejection(
    Session.LobbyStartBlockReason.ClientMapDownloading,
    5,
    Session.ClientMapPhase.Downloading,
    42,
    "map-a",
    7);
```

Require `roundTrip.RequestId == 7`; reject a missing field, duplicate `RequestId`, unknown field, nested child node, `RequestId: 0`, `RequestId: -1`, and `RequestId: 01`. Reject non-canonical `ClientIndex` and `Progress` spellings such as `+5`, `05`, and `-01` while preserving canonical `-1`. Require `availability.ToRejection(request).RequestId == request.RequestId` and `rejection.ToAvailability()` to preserve reason/client/phase/progress with zero additional blockers.

Add a source assertion that `ProtocolVersion.Orders == 23`.

- [ ] **Step 2: Run the RED suite**

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~IosLobbyStartAvailabilityTest' -v minimal
```

Expected: compile failures for missing `LobbySafeStartRequest`, missing `RequestId`, the old constructor, and protocol 22.

- [ ] **Step 3: Implement the minimal strict request type**

Implement `LobbySafeStartRequest` as a readonly equatable struct with `ExpectedMapUid`, `RequestId`, constructor validation, `ToCommand()`, and `TryParse()`.

`TryParse` must split on literal space into exactly two non-empty fields, reject whitespace in the UID, and parse the ID with `NumberStyles.None` and invariant culture. Require `id > 0` and canonical `id.ToString(CultureInfo.InvariantCulture) == input`.

- [ ] **Step 4: Add RequestId to the rejection payload**

Change the constructor to:

```csharp
public LobbyStartRejection(
    Session.LobbyStartBlockReason reason,
    int clientIndex,
    Session.ClientMapPhase clientMapPhase,
    int progress,
    string expectedMapUid,
    long requestId)
```

Require `requestId > 0`, serialize exactly six unique leaf fields, reject nested nodes, parse the ID canonically with `NumberStyles.None`, and parse signed `ClientIndex`/`Progress` using invariant culture plus exact round-trip comparison. Add:

```csharp
public LobbyStartAvailability ToAvailability();
public LobbyStartRejection ToRejection(LobbySafeStartRequest request);
```

Update Orders protocol to 23.

- [ ] **Step 5: Run GREEN and diff checks**

Repeat the Task 1 command. Expected: all selected tests pass with nonzero count. Then run `git diff --check`.

## Task 2: Pure Pending/Rejection Correlation State Machine

**Files:**

- Create: `engine/OpenRA.Game/Network/LobbySafeStartRequestState.cs`
- Test: `engine/OpenRA.Test/IosLobbyStartAvailabilityTest.cs`

- [ ] **Step 1: Write failing state-machine tests**

Require this API:

```csharp
var state = new LobbySafeStartRequestState(0);
var first = state.Begin("map-a");
Assert.That(first.RequestId, Is.EqualTo(1));
Assert.That(state.IsPending, Is.True);

var accepted = new LobbyStartRejection(
    Session.LobbyStartBlockReason.ClientMapDownloading,
    5,
    Session.ClientMapPhase.Downloading,
    42,
    "map-a",
    first.RequestId);
Assert.That(state.TryAcceptRejection(accepted, "map-a"), Is.True);
Assert.That(state.IsPending, Is.False);
Assert.That(state.Rejection, Is.SameAs(accepted));
```

Add separate tests proving:

- wrong request ID, wrong expected map, current-map mismatch, unsolicited response, and post-timeout response return false without mutation;
- after request 1 times out and request 2 begins, delayed rejection 1 cannot clear request 2;
- `TryTimeout(oldId)` cannot clear a newer request, while `TryTimeout(currentId)` can;
- `Begin` clears a cached rejection and allocates the next ID;
- a same-map `ObserveReadinessChange` preserves pending; after rejection, `ObserveReadinessChange(unrelated)` preserves another client's cached rejection and `ObserveReadinessChange(blockingClient)` clears the matching rejection;
- `ObserveLobbySync(currentUid)` preserves same-map pending but clears a cached rejection; `ObserveLobbySync(differentUid)` clears both pending and rejection;
- `Reset()` clears pending and rejection without reusing the last issued ID;
- allocation after `long.MaxValue` throws `InvalidOperationException` instead of reusing an ID.

- [ ] **Step 2: Run RED**

Run the Task 1 focused command. Expected: compile failure because `LobbySafeStartRequestState` does not exist.

- [ ] **Step 3: Implement the minimal state machine**

Implement a sealed class with constructor `LobbySafeStartRequestState(long lastIssuedRequestId)` and properties `IsPending`, `PendingRequestId`, and `Rejection`. Keep the pending map private. Every mutating method returns whether it changed state so callers invalidate cached UI only when necessary.

Use this exact acceptance condition:

```csharp
if (!IsPending || rejection == null ||
    rejection.RequestId != PendingRequestId ||
    rejection.ExpectedMapUid != pendingMapUid ||
    rejection.ExpectedMapUid != currentMapUid)
    return false;
```

`ObserveReadinessChange` preserves pending and clears a cached rejection only when its client index matches. `ObserveLobbySync` preserves same-map pending, clears any cached rejection, and clears pending only when the current map UID differs. This follows the server connection's FIFO order while still invalidating requests that target an obsolete map.

- [ ] **Step 4: Run GREEN**

Repeat the focused command and require all tests pass. Run `git diff --check`.

## Task 3: Echo Correlation Through the Server Command

**Files:**

- Modify: `engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs`
- Modify: `engine/OpenRA.Test/IosLobbyStartAvailabilityTest.cs`
- Modify: `engine/OpenRA.Test/IosLobbyMapReadinessTest.cs`

- [ ] **Step 1: Write RED server/source contracts**

Replace old map-only parser tests with `LobbySafeStartRequest.TryParse` tests from Task 1. Update Ready command admission to `startgame_safe map-a 1`, and update/rename the existing `OrdersProtocolIncludesCurrentMapReadiness` assertion in `IosLobbyMapReadinessTest` from Orders 22 to Orders 23.

In the existing `EveryMultiplayerStartEntryUsesTheSharedSafetyPolicy` source inspection, require the `StartGameSafe` body to contain:

```text
LobbySafeStartRequest.TryParse
request.ExpectedMapUid
availability.ToRejection(request)
request.RequestId
```

Require all explicit non-admin and stale-map `new LobbyStartRejection(...)` calls to pass `request.RequestId`.
Require them to pass `request.ExpectedMapUid`, not `currentMapUid`, so a parsed request remains correlatable even when the server has already switched maps.

- [ ] **Step 2: Run RED**

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~IosLobbyStartAvailabilityTest|FullyQualifiedName~IosLobbyMapReadinessTest' \
  -v minimal
```

Expected: source-contract and command-admission failures against the map-only handler.

- [ ] **Step 3: Implement the server echo path**

Remove `TryParseSafeStartMapUid`. Parse a `LobbySafeStartRequest request` under the existing lobby lock. Use `request.ExpectedMapUid` for stale-map comparison and pass the whole request to `availability.ToRejection(request)`. All explicit rejections must include `request.RequestId`.

All parsed-request rejection branches must echo `request.ExpectedMapUid` and `request.RequestId`. Do not change the order of admin, stale-map, shared availability, or central `Server.StartGame()` checks.

- [ ] **Step 4: Run GREEN**

Repeat the Task 3 command and require all tests pass. Run `git diff --check`.

## Task 4: Wire the State Machine into iOS Lobby and Smoke

**Files:**

- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyStartStatus.cs`
- Modify: `engine/OpenRA.Game/Support/IosMultiplayerSmoke.cs`
- Modify: `engine/OpenRA.Test/IosLobbyResponsiveLayoutTest.cs`
- Modify: `engine/OpenRA.Test/IosMultiplayerPolicyTest.cs`

- [ ] **Step 1: Write RED UI/smoke contracts**

Require `LobbyLogic` to own `new LobbySafeStartRequestState(0)`, issue `request.ToCommand()`, call `TryAcceptRejection`, and render `safeStartState.Rejection.ToAvailability()` before local evaluation. Require readiness and full lobby handlers to clear authoritative rejection state, exact timeout IDs, disconnect reset, and symmetric event teardown. Add `RequesterNotAdmin` to the measured en-US/zh-CN Start-button cases and require `IosLobbyStartStatus` to use the existing `notification-requires-host` key; prove a stale-map rejection cannot be accepted after the map changes.

Update smoke source tests to require two distinct request objects/IDs and rejection validation against the blocked request ID. Keep the existing assertion that desktop still assigns `panel = PanelType.ForceStart`.

- [ ] **Step 2: Run RED**

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~IosLobbyStartAvailabilityTest|FullyQualifiedName~IosLobbyResponsiveLayoutTest|FullyQualifiedName~IosMultiplayerPolicyTest' \
  -v minimal
```

Expected: source wiring tests fail because `LobbyLogic` and smoke still format map-only commands and discard the structured reason.

- [ ] **Step 3: Implement LobbyLogic lifecycle wiring**

Replace `safeStartPending`/`safeStartRequestIdentity` ownership with `LobbySafeStartRequestState`.

- `BeginSafeStart` calls `Begin(currentMapUid)`, schedules `TryTimeout(request.RequestId)`, and issues `request.ToCommand()`.
- `SafeStartRejected` invalidates only when `TryAcceptRejection` returns true.
- `CurrentStartAvailability` returns `state.Rejection.ToAvailability()` before evaluating local state.
- `IosLobbyStartStatus.Format` maps `RequesterNotAdmin` to the existing localized `notification-requires-host` message instead of the generic waiting message. `StaleMap` remains outside the display cache because the state machine rejects it when the current map differs.
- `ClientMapReadinessChanged` calls `ObserveReadinessChange(clientIndex)` before invalidating while preserving same-map pending.
- A named full-lobby handler calls `ObserveLobbySync(currentMapUid)` and invalidates; it preserves same-map pending, clears cached rejection, and clears obsolete-map pending before recomputing. It is subscribed/unsubscribed instead of the direct invalidation delegate.
- Disconnect and dispose call `Reset()`.
- Button disabled/text delegates use `state.IsPending`; an accepted rejection therefore displays through existing `IosLobbyStartStatus.Format` with no new Fluent keys.

- [ ] **Step 4: Update the smoke**

Allocate distinct IDs for blocked and final host requests, issue canonical request commands, and require `HandleSafeStartRejected` to match the blocked request ID in addition to reason/client/progress/map UID.

- [ ] **Step 5: Run GREEN**

Repeat the Task 4 command and require all tests pass. Run the complete focused iOS lobby filter and `git diff --check`.

## Task 5: Runtime Audit and Tamper Coverage

**Files:**

- Modify: `packaging/audit_ios_touch_runtime_bundle.py`
- Modify: `packaging/tests/test_ios_touch_runtime_bundle.py`

- [ ] **Step 1: Add RED structural/tamper tests**

Update the valid fixture to protocol 23, the two-token safe-start command, six-field rejection, state-machine lifecycle, and request-ID smoke checks.

Add tamper cases that must be rejected when:

- protocol is downgraded to 22;
- `RequestId` is removed from rejection serialization;
- the server sends a rejection without `request.RequestId`;
- the stale-map rejection substitutes `currentMapUid` for `request.ExpectedMapUid`;
- the UI rejection handler clears pending without `TryAcceptRejection`;
- the UI ignores `Rejection.ToAvailability()`;
- an accepted `RequesterNotAdmin` rejection falls through to the generic waiting label;
- the smoke reuses the blocked request ID for final start.

- [ ] **Step 2: Run RED**

```sh
/usr/bin/python3 -m unittest \
  packaging.tests.test_ios_touch_runtime_bundle \
  packaging.tests.test_ios_touch_skin_assets
```

Expected: the new valid contract and tamper tests fail against the old audit.

- [ ] **Step 3: Implement minimal audit recognition**

Require exact protocol/wire/state-machine source markers and managed metadata while preserving every existing quarter-ring, atlas, lobby, scroll, DLL, AOT, and retail-content check. Do not make the audit depend on line numbers.

- [ ] **Step 4: Run GREEN and discovery**

Repeat the two-module command, then run:

```sh
/usr/bin/python3 -m unittest discover -s packaging/tests -p 'test_*.py'
```

Require zero failures and run `git diff --check`.

## Task 6: Generate 0017, Review, Build, and Commit

**Files:**

- Create: `engine-patches/0017-ios-safe-start-rejection-correlation.patch`
- Modify: `engine-patches/README.md`
- Inspect only: `packaging/public-content-manifest.json`

- [ ] **Step 1: Run final source gates**

Run the full C# suite, Python discovery, root `./utility.sh --check-yaml`, `git diff --check`, and Release builds for `OpenRA.Game` and `OpenRA.Mods.Common`. Require zero errors; record the known NU1901 warning separately.

- [ ] **Step 2: Build a clean post-0016 baseline**

Verify the official ZIP SHA-256 remains:

```text
ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a
```

Extract it into a new `mktemp -d` directory with `ditto -x -k`. Apply 0001–0016 in filename order with `patch -p1 --batch --verbose -V none`, capture the output, and reject any nonzero exit, `.rej`, or `.orig` file. Reject every offset/fuzz diagnostic except the pinned historical `0007` result: exactly `Hunk #1 succeeded at 254 with fuzz 2 (offset -1 lines).` and `Hunk #5 succeeded at 352 (offset -1 lines).`. This predecessor compatibility exception does not weaken the strict 0017 gate.

- [ ] **Step 3: Generate 0017 from the exact engine allowlist**

Stage exactly these paths under `a/` from post-0016 and `b/` from the workspace:

```text
OpenRA.Game/Network/LobbySafeStartRequest.cs
OpenRA.Game/Network/LobbySafeStartRequestState.cs
OpenRA.Game/Network/LobbyStartAvailability.cs
OpenRA.Game/Server/ProtocolVersion.cs
OpenRA.Game/Support/IosMultiplayerSmoke.cs
OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs
OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyStartStatus.cs
OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs
OpenRA.Test/IosLobbyMapReadinessTest.cs
OpenRA.Test/IosLobbyResponsiveLayoutTest.cs
OpenRA.Test/IosLobbyStartAvailabilityTest.cs
OpenRA.Test/IosMultiplayerPolicyTest.cs
```

Generate one `diff -ruN a b`, require its headers equal this allowlist exactly, and reject docs, packaging, `mods/ra2`, app bundles, profiles, device data, caches, screenshots, obj, and bin. If implementation proves another engine file is genuinely required, update this plan and review the boundary before generating the candidate; do not silently broaden the patch.

- [ ] **Step 4: Strict fresh replay proof**

On a second fresh post-0016 extraction, run `git apply --check`, apply 0017 with `patch -p1 --batch -F 0 -V none`, reject any `offset|fuzz`, compare every allowlisted path byte-for-byte, require repeat-forward dry-run failure without tree mutation, reverse dry-run success, and zero `.rej/.orig` files.

- [ ] **Step 5: Update README and dispatch ordered reviews**

Document 0017 as request/rejection correlation that upgrades Orders to 23 across exactly 12 engine paths without changing Fluent files, YAML, desktop, or safety semantics. Correct the README regeneration recipe so 0017's `a/` side comes from the official ZIP after replaying 0001–0016, never directly from the pristine ZIP; `b/` comes from the workspace allowlist. Capture verbose predecessor output, allow only the exact documented historical `0007` diagnostics, and reject every other offset/fuzz. Dispatch a spec reviewer, resolve every Critical/Important finding, then dispatch a separate code-quality reviewer and repeat focused tests after fixes.

- [ ] **Step 6: Build and verify the signed app**

Run `AotObjectRegistryCoverageTest` and require `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs` to remain byte-identical: the new state class has only a one-argument constructor and must not enter the object factory inventory. Build ios-arm64 Release without `--no-restore` using the valid Apple Development identity/profile. Require build exit 0, external `codesign --verify --deep --strict`, Team ID `267KQ4A3N4`, Bundle ID `com.openra.ipad.personal`, loading-brand audit pass, and runtime bundle audit pass.

- [ ] **Step 7: Stage and commit the exact allowlist**

Use ordinary `git add` for the design/plan, packaging audit/tests, README, and 0017 patch. Use exact `git add -f -- <path>` only for the materialized engine allowlist. Show `git diff --cached --name-status`, `git diff --cached --check`, confirm no file exceeds 50 MB and no unrelated untracked/private/device/generated artifacts are staged, then commit:

```sh
git commit -m "Correlate iOS safe-start rejections"
```

Do not push, merge, rewrite `0016`, or install onto a physical device without separately valid device availability and authorization.
