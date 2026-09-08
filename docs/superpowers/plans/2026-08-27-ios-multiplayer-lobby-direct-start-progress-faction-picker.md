# iOS Multiplayer Lobby Direct Start, Progress, and Faction Picker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let an iOS host start a multiplayer game with one tap once every connected human has the current map, show truthful per-player map progress and blockers until then, and make faction selection materially easier to touch while preserving the desktop UI and ordinary NotReady confirmation flow.

**Architecture:** Add a protocol-v22 current-map readiness handshake and throttled delta sync owned by the network lobby lifecycle, then enforce one pure map-safety policy at every Multiplayer/Dedicated start path while preserving desktop ordinary-NotReady confirmation semantics. Render the policy through the existing iOS footer and Ready column, and parameterize the existing grouped dropdown with a faction-only iOS style instead of introducing a new screen.

**Tech Stack:** C#/.NET 8, OpenRA Orders/MiniYaml protocol, OpenRA widgets and Fluent, NUnit, Python 3 unittest, iOS Safe Area metrics, deterministic engine patch replay, MSBuild ios-arm64 signing, CoreDevice `devicectl`.

---

## File Structure

- Modify `engine/OpenRA.Game/Network/Session.cs` — client map phase/progress and blocker enums.
- Create `engine/OpenRA.Game/Network/ClientMapReadiness.cs` — report parsing, legal phase/progress combinations, atomic state transitions, and delta DTO.
- Create `engine/OpenRA.Game/Network/ClientMapReadinessPublisher.cs` — pure phase mapping, throttling, UID reset, and command emission policy.
- Modify `engine/OpenRA.Game/Network/OrderManager.cs` — own/tick the readiness publisher and expose safe-start rejection events.
- Modify `engine/OpenRA.Game/Network/UnitOrders.cs` — receive readiness deltas/start rejections.
- Modify `engine/OpenRA.Game/Map/MapPreview.cs` — clear stale byte/percentage state before every install or retry.
- Modify `engine/OpenRA.Game/Server/ProtocolVersion.cs` — bump Orders protocol 21 to 22.
- Modify `engine/OpenRA.Game/Server/Server.cs` — broadcast one-client readiness deltas and initialize/reset snapshots.
- Create `engine/OpenRA.Game/Network/LobbyStartAvailability.cs` — shared pure start gate, stable blocker ordering, and structured rejection DTO.
- Create `engine/OpenRA.Mods.Common/ServerTraits/ClientMapReadinessThrottle.cs` — server-side latest-wins rate limiting and log sampling.
- Modify `engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs` — validate `map_status`, atomic readiness transitions, safe start, auto-start protection, and command whitelist.
- Modify `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/MapPreviewLogic.cs` — stop using unqualified `state NotReady` as the map-success handshake.
- Create `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyStartStatus.cs` — localized compact blocker formatting with measured fallback.
- Create `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyMapStatusPresentation.cs` — pure iOS/current-UID/bot visibility and compact Ready-cell presentation.
- Modify `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs` — iOS direct-start wiring, dynamic Start state/text, pending rejection handling, live player-row status, and larger main faction geometry.
- Modify `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyUtils.cs` — map-readiness widget binding and faction-item post-layout text/flag binding.
- Modify `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyLayout.cs` — ten-target player-row minimum and explicit 2/1/3/1/1/1/1 target column minima.
- Modify `engine/OpenRA.Mods.Common/Widgets/Logic/IosTouchWidgetPolicy.cs` — faction popup width, height, header, flag, and content geometry.
- Modify `engine/OpenRA.Mods.Common/Widgets/DropDownButtonWidget.cs` — optional faction-only iOS grouped-dropdown adaptation.
- Modify `engine/mods/common/chrome/lobby-players.yaml` — default-hidden iOS readiness text/icon widgets.
- Modify `engine/mods/common/fluent/chrome.ftl` — short English blocker strings.
- Modify `engine/mods/common/fluent/common.ftl` — English map-readiness notifications.
- Modify `engine/mods/common/fluent/zh-CN/chrome.ftl` — short Simplified Chinese blocker strings.
- Modify `engine/mods/common/fluent/zh-CN/common.ftl` — Simplified Chinese map-readiness notifications.
- Create `engine/OpenRA.Test/IosLobbyMapReadinessTest.cs` — phase invariants, serialization, publisher throttling, retry reset, and delta parsing.
- Create `engine/OpenRA.Test/IosLobbyStartAvailabilityTest.cs` — iOS/desktop gates, entry-point protection, and trusted local-flow regressions.
- Modify `engine/OpenRA.Test/IosLobbyResponsiveLayoutTest.cs` — footer text, live readiness widgets, ten-target columns, and main faction bounds.
- Modify `engine/OpenRA.Test/IosTouchWidgetPolicyTest.cs` — faction popup geometry, visible choices, Safe Area, flag, and desktop isolation.
- Modify `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs` — source/YAML inventory and spectator-entry invariants.
- Modify `engine/OpenRA.Test/IosMultiplayerPolicyTest.cs` — blocked-start then one-tap multiplayer smoke contract.
- Modify `engine/OpenRA.Game/Support/IosMultiplayerSmoke.cs` — blocked-start and one-tap-start smoke seams.
- Modify `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs` — register the new public readiness DTO for NativeAOT construction.
- Modify `packaging/audit_ios_touch_runtime_bundle.py` and `packaging/tests/test_ios_touch_runtime_bundle.py` — bundle source/YAML/Fluent/protocol contracts.
- Inspect `packaging/public-content-manifest.json` — refresh only changed existing entries; do not add `mods/common` or managed-code entries when none are in scope.
- Create `engine-patches/0016-ios-lobby-direct-start-progress-faction-picker.patch` — replay every engine path above from post-0015.
- Modify `engine-patches/README.md` — document 0016 scope and replay boundary.

## Task 1: Define Current-Map Readiness and a Network-Owned Publisher

**Files:**
- Modify: `engine/OpenRA.Game/Network/Session.cs`
- Create: `engine/OpenRA.Game/Network/ClientMapReadiness.cs`
- Create: `engine/OpenRA.Game/Network/ClientMapReadinessPublisher.cs`
- Modify: `engine/OpenRA.Game/Network/OrderManager.cs`
- Modify: `engine/OpenRA.Game/Network/UnitOrders.cs`
- Modify: `engine/OpenRA.Game/Map/MapPreview.cs`
- Modify: `engine/OpenRA.Game/Server/ProtocolVersion.cs`
- Create: `engine/OpenRA.Test/IosLobbyMapReadinessTest.cs`
- Modify: `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs`

- [ ] **Step 1: Write RED tests for the serialized state contract**

Add `IosLobbyMapReadinessTest` cases that construct and round-trip `Session.Client` through `Serialize`/`Deserialize` and require:

```csharp
var client = new Session.Client
{
	Index = 7,
	MapUid = "map-a",
	MapPhase = Session.ClientMapPhase.Downloading,
	MapProgress = 42
};

var restored = Session.Client.Deserialize(client.Serialize().Value);
Assert.That(restored.MapUid, Is.EqualTo("map-a"));
Assert.That(restored.MapPhase, Is.EqualTo(Session.ClientMapPhase.Downloading));
Assert.That(restored.MapProgress, Is.EqualTo(42));
```

Also deserialize a legacy client node without the new fields and require `MapUid == null`, `MapPhase == Unknown`, and `MapProgress == -1`. Assert legal combinations exactly: `Downloading/-1`, `Downloading/0..99`, `Ready/100`, and every other phase only with `-1`.

- [ ] **Step 2: Write RED publisher tests**

Test a pure API with a fake millisecond clock:

```csharp
var publisher = new ClientMapReadinessPublisher(() => now);
Assert.That(publisher.Update("map-a", MapStatus.Searching, 0),
	Is.EqualTo(new ClientMapReadinessReport("map-a", Session.ClientMapPhase.Searching, -1)));
Assert.That(publisher.Update("map-a", MapStatus.Downloading, 42).Value.Progress, Is.EqualTo(42));
Assert.That(publisher.Update("map-a", MapStatus.Downloading, 42), Is.Null, "same integer percent must be deduplicated");
now += 249;
Assert.That(publisher.Update("map-a", MapStatus.Downloading, 43), Is.Null, "downloading deltas are capped at 4Hz");
now += 1;
Assert.That(publisher.Update("map-a", MapStatus.Downloading, 43).Value.Progress, Is.EqualTo(43));
Assert.That(publisher.Update("map-b", MapStatus.Searching, 0).Value.MapUid, Is.EqualTo("map-b"));
```

Cover unknown Content-Length (`Downloading/-1`), `Downloading/100 -> InstallingOrVerifying/-1`, `Available -> Ready/100`, immediate phase changes, and UID cache reset. Add an OrderManager wiring test that always resolves the preview from `LobbyInfo.GlobalSettings.Map`, proving an old-map callback cannot be published after switching to `map-b`. Set a preview to stale `DownloadBytes/DownloadPercentage`, invoke the new download-attempt reset seam, and require both fields to become zero before retry progress is observed.

- [ ] **Step 3: Run focused tests and capture RED**

Run:

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~IosLobbyMapReadinessTest' -v minimal
```

Expected: compilation fails because `ClientMapPhase`, the new client fields, and `ClientMapReadinessPublisher` do not exist.

- [ ] **Step 4: Add the protocol-v22 data types and invariants**

In `Session.cs`, add:

```csharp
public enum ClientMapPhase
{
	Unknown,
	Searching,
	WaitingForDownload,
	Downloading,
	InstallingOrVerifying,
	Ready,
	Unavailable,
	Error
}

```

Add to `Session.Client`:

```csharp
public string MapUid;
public ClientMapPhase MapPhase = ClientMapPhase.Unknown;
public int MapProgress = -1;
public bool IsMapReadyFor(string uid) =>
	MapUid == uid && MapPhase == ClientMapPhase.Ready && MapProgress == 100;
```

In `ClientMapReadiness.cs`, define `ClientMapReadinessReport` with `MapUid`, `Phase`, `Progress`, value equality, `TryParse(string, out report)`, `IsValidFor(currentUid)`, and `ToCommand()` returning exactly `map_status <raw-current-uid> <phase> <progress>`. Put the single legal-combination method at `ClientMapReadinessState.IsValidPhaseProgress(phase, progress)`. Add `ClientMapReadinessState.Reset(client, uid)` and `Apply(client, currentUid, report)` so map UID, phase, progress, and `ClientState` change atomically. Add a flat `ClientMapReadinessUpdate` DTO containing `ClientIndex`, UID, phase, progress, and server-authoritative `ClientState`, with MiniYaml `Serialize`/`Deserialize` and `ApplyTo` methods. Validate the raw UID as one nonempty whitespace-free token; do not escape or decode it with a second wire format.

Set `ProtocolVersion.Orders = 22` and extend the version comment with “22: current-map readiness and iOS safe-start orders.”

Run the repository's existing AOT registry generator after adding the public parameterless `ClientMapReadinessUpdate` DTO. Accept only the deterministic inventory and `RegisterBasic` additions for that exact type; no unrelated generated-registry drift is in scope.

- [ ] **Step 5: Implement the pure publisher and own it in `OrderManager`**

Implement `ClientMapReadinessPublisher` so `Update` returns a nullable report and never issues orders itself. Map exactly: `Searching -> Searching/-1`, `DownloadAvailable -> WaitingForDownload/-1`, `Downloading` with unknown/zero callback data to `Downloading/-1`, `Downloading/1..99` to the matching percent, `Downloading/100 -> InstallingOrVerifying/-1`, `Available -> Ready/100`, `Unavailable -> Unavailable/-1`, and `DownloadError -> Error/-1`. Reset its cache whenever UID changes. In `MapPreview.Install`, synchronously set `DownloadBytes = 0` and `DownloadPercentage = 0` before marking a new attempt Downloading or starting the worker; retry must not expose the previous attempt’s value for one frame.

Add one publisher field to `OrderManager`, tick it before `SendImmediateOrders()` while `!GameStarted`, `LocalClient != null`, and `LobbyInfo.GlobalSettings.Map` is nonempty, then issue only non-null reports:

```csharp
var uid = LobbyInfo.GlobalSettings.Map;
var preview = Game.ModData.MapCache[uid];
var report = mapReadinessPublisher.Update(uid, preview.Status, preview.DownloadPercentage);
if (report != null)
	IssueOrder(Order.Command(report.Value.ToCommand()));
```

Guard missing map-cache entries as `Unknown/-1` and do not bind publishing to `MapPreviewLogic`, the Servers tab, or any Widget lifecycle.

- [ ] **Step 6: Verify GREEN and the protocol constant**

Run the Step 3 command, then:

```sh
rg -n 'Orders = 22|map_status' \
  engine/OpenRA.Game/Server/ProtocolVersion.cs \
  engine/OpenRA.Game/Network/{Session.cs,OrderManager.cs,UnitOrders.cs}
```

Expected: all `IosLobbyMapReadinessTest` cases pass and the protocol/publisher wiring appears once in the intended locations.

## Task 2: Accept, Validate, and Delta-Sync Map Readiness

**Files:**
- Modify: `engine/OpenRA.Game/Server/Server.cs`
- Modify: `engine/OpenRA.Game/Network/UnitOrders.cs`
- Create: `engine/OpenRA.Mods.Common/ServerTraits/ClientMapReadinessThrottle.cs`
- Modify: `engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/MapPreviewLogic.cs`
- Modify: `engine/OpenRA.Test/IosLobbyMapReadinessTest.cs`

- [ ] **Step 1: Add RED command/parser/state-transition tests**

Add tests around an internal pure parser/helper extracted from `LobbyCommands`:

```csharp
Assert.That(ParseMapStatus("map-a Downloading 42", "map-a").Report.Progress, Is.EqualTo(42));
Assert.That(ParseMapStatus("map-old Ready 100", "map-a").Accepted, Is.False);
Assert.That(ParseMapStatus("map-a Ready -1", "map-a").Accepted, Is.False);
Assert.That(ParseMapStatus("map-a Error 100", "map-a").Accepted, Is.False);
Assert.That(ParseMapStatus("map-a Downloading 100", "map-a").Accepted, Is.False);
```

Require a non-Ready report to set `Client.State = Invalid`; require `Ready/100` for the current UID to atomically set `MapUid`, `MapPhase`, `MapProgress`, and convert only `Invalid -> NotReady`. In one parameterized command-admission test, require ordinary `state Ready` and `state NotReady` to be rejected while the client is map-invalid for Multiplayer/Dedicated, but accepted with the existing semantics for Local/Skirmish.

- [ ] **Step 2: Add RED delta-sync tests**

Feed a `SyncClientMapReadiness` server order through `UnitOrders.ProcessOrder` and assert that only the matching client changes and the narrow `OrderManager.ClientMapReadinessChanged(clientIndex)` event fires. Assert `Game.LobbyInfoChanged` does not fire for a percentage-only delta, so a 4Hz update cannot rebuild the whole lobby. Cover unknown client index, `-1`, `42`, `100`, and malformed payloads without crashes.

- [ ] **Step 3: Run focused tests and capture RED**

Run the Task 1 command. Expected: parser, atomic transition, and delta order assertions fail.

- [ ] **Step 4: Implement strict `map_status` handling**

Register `map_status` in `LobbyCommands`. Parse exactly three fields, update only the sending `client`, validate current UID and `ClientMapReadinessState.IsValidPhaseProgress`, and reject stale/malformed reports. While the lobby lock is held, apply every accepted report to the authoritative `Session.Client` immediately; a Ready client reporting any non-Ready phase must become Invalid before any start command in the same or next tick can pass. Then submit the resulting immutable `ClientMapReadinessUpdate` snapshot to a `ClientMapReadinessThrottle` instance owned by the existing `ITick` server trait. `Submit(update, now)` retains only the latest broadcast snapshot; `Drain(now)` only emits deltas, at most one per client every 250ms, and never mutates authoritative state; `Reset()` runs on map UID change. Phase changes replace pending broadcast values but do not bypass the server limit, so malicious phase oscillation cannot amplify broadcasts. Emit at most one aggregate invalid/flood log every five seconds.

Use `ClientMapReadinessState.Apply(client, currentUid, report)` synchronously before `Submit`. It must reject UID/combination mismatches before mutation, return `Unchanged` for duplicates, convert only `Invalid -> NotReady` on `Ready/100`, preserve an already game-Ready client, and force every current-UID non-Ready phase back to `Invalid`. Add a test that starts from game Ready, reports `Downloading/42`, does not drain the throttle, and already sees legacy start, safe start, auto-start, and the central guard all blocked.

Only a current-map UID change resets every existing client: set each client’s `MapUid` to the new UID, `MapPhase = Unknown`, `MapProgress = -1`, and `State = Invalid`. On initial join, initialize only the new client with those fields for the current lobby map; on disconnect, remove only that client; on same-UID slot/team/faction/admin or other participant mutations, preserve every existing client’s map readiness. This is required because a same-UID `Ready/100` publisher report is deduplicated and would not automatically refill a gratuitously reset server state. Existing admission already rejects a new no-slot client when `AllowSpectators == false`; after map-slot reassignment, collect any displaced non-bot clients whose `Slot` became null, send a specific server error explaining that the selected map has fewer slots, and drop those connections after enumeration. Add tests proving no hidden no-slot client remains, a join initializes only the newcomer, a disconnect removes only the departing client, and same-map lobby mutations retain the remaining clients’ readiness.

- [ ] **Step 5: Add and receive a lightweight delta order**

Add `Server.SyncClientMapReadiness(Session.Client client)` that broadcasts one `ClientMapReadinessUpdate` containing index, UID, phase, progress, and authoritative `ClientState` under `SyncClientMapReadiness`. Add the matching `UnitOrders` case, mutate the existing `Session.Client` in place, validate the combination again, and fire `OrderManager.ClientMapReadinessChanged(clientIndex)`. Do not call `Game.SyncLobbyInfo()` for a delta; full snapshot/member/slot changes keep using the existing event.

Add to `OrderManager`:

```csharp
public event Action<int> ClientMapReadinessChanged = _ => { };
internal void NotifyClientMapReadinessChanged(int clientIndex) =>
	ClientMapReadinessChanged(clientIndex);
```

Do not call `SyncLobbyClients()` for each percentage. Full `Session.Client.Serialize()` remains the new-join snapshot path.

- [ ] **Step 6: Remove unqualified map-success unlocks**

Remove the `state NotReady` orders that are issued specifically because `MapPreview` became Available from `LobbyLogic`/`MapPreviewLogic`; the network-owned publisher’s current-UID `Ready/100` report is now the only Invalid-to-NotReady map-success transition. Preserve ordinary Ready checkbox toggles and trusted local `Game.LoadMap` behavior.

- [ ] **Step 7: Verify GREEN and no full-list progress broadcast**

Run the Task 1 focused command, then:

```sh
rg -n 'SyncClientMapReadiness|SyncLobbyClients' \
  engine/OpenRA.Game/Server/Server.cs \
  engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs \
  engine/OpenRA.Game/Network/UnitOrders.cs
```

Expected: focused tests pass; progress uses the single-client delta, while full-list sync remains only for ordinary lobby mutations/snapshots.

## Task 3: Share One Map-Safety Gate Across Every Network Multiplayer Entry

**Files:**
- Create: `engine/OpenRA.Game/Network/LobbyStartAvailability.cs`
- Modify: `engine/OpenRA.Game/Network/Session.cs`
- Modify: `engine/OpenRA.Game/Network/OrderManager.cs`
- Modify: `engine/OpenRA.Game/Network/UnitOrders.cs`
- Modify: `engine/OpenRA.Game/Server/Server.cs`
- Modify: `engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs`
- Modify: `engine/OpenRA.Game/Support/IosMultiplayerSmoke.cs`
- Create: `engine/OpenRA.Test/IosLobbyStartAvailabilityTest.cs`

- [ ] **Step 1: Write RED pure-policy tests**

Define the expected API in tests:

```csharp
var result = LobbyStartAvailability.Evaluate(new LobbyStartInputs
{
	LobbyInfo = session,
	Mode = LobbyStartMode.SafeDirect,
	RequesterIsAdmin = true,
	RequireCurrentMapReadiness = true,
	InsufficientEnabledSpawnPoints = false
});

result = result.ApplyLocalMapAvailability(localMapAvailable: true);

Assert.That(result.CanStart, Is.True);
Assert.That(result.Reason, Is.EqualTo(Session.LobbyStartBlockReason.None));
```

Add three explicit overlay RED cases: `allowed.ApplyLocalMapAvailability(true)` is an identity; `allowed.ApplyLocalMapAvailability(false)` becomes `SelectedMapUnavailable`; and `alreadyBlocked.ApplyLocalMapAvailability(false)` preserves the existing higher-priority reason, client index, phase, progress, and additional-blocker count.

Use enums `SafeDirect`, `ForceConfirmed`, and `Automatic`. Require stable blocker priority: server `Validating` → `Incompatible`/non-Playable → client Error/Unavailable → Installing/Verifying → Downloading → Searching/Waiting/Unknown → required slot → no players → two-human rule → spawns. Map readiness iterates `LobbyInfo.NonBotClients`, including a transient no-slot client; bots are ignored. Require `SafeDirect` and `ForceConfirmed` to ignore ordinary game NotReady, while `Automatic` still requires every participating human and the admin to be game Ready.

Cover an Invalid slotted human, an Invalid no-slot human, bot ignored, all-ready, a 42% blocker, multiple blockers/additional count, and a hostile host attempting legacy `startgame` while a client is map-invalid.

- [ ] **Step 2: Write RED command-admission and trusted-flow tests**

Require exact command-name parsing and the Ready-state whitelist:

```csharp
Assert.That(LobbyCommands.IsAllowedWhileReady("state"), Is.True);
Assert.That(LobbyCommands.IsAllowedWhileReady("map_status"), Is.True);
Assert.That(LobbyCommands.IsAllowedWhileReady("startgame_safe"), Is.True);
Assert.That(LobbyCommands.IsAllowedWhileReady("stateevil"), Is.False);
```

Add source/executable contracts proving:

- `startgame_safe <expectedMapUid>` rejects stale UID with a structured payload.
- `CheckAutoStart`, `startgame_safe`, and legacy `startgame` all consult the map gate in every Multiplayer/Dedicated lobby.
- desktop keeps the existing Force Start UI and may still ignore ordinary game NotReady after confirmation, but cannot force-start a map-invalid client.
- Local/Skirmish and trusted same-map Restart do not require a network readiness handshake.
- A Multiplayer current-map UID change resets every client and requires a new handshake; joining initializes only the newcomer, while disconnect and same-UID participant/slot changes preserve all remaining clients’ accepted readiness.

- [ ] **Step 3: Run the focused start tests and capture RED**

Run:

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~IosLobbyStartAvailabilityTest' -v minimal
```

Expected: compilation fails because the policy, modes, blocker enum, rejection DTO, and safe command are absent.

- [ ] **Step 4: Implement the pure policy and rejection DTO**

Put the dependency-neutral policy in `OpenRA.Game/Network` so both `Server` and `OpenRA.Mods.Common` can call it without a reverse assembly dependency:

```csharp
public enum LobbyStartMode { SafeDirect, ForceConfirmed, Automatic }

public readonly struct LobbyStartInputs
{
	public Session LobbyInfo { get; init; }
	public LobbyStartMode Mode { get; init; }
	public bool RequesterIsAdmin { get; init; }
	public bool RequireCurrentMapReadiness { get; init; }
	public bool InsufficientEnabledSpawnPoints { get; init; }
}
```

Add exact `Session.LobbyStartBlockReason` values: `None`, `RequesterNotAdmin`, `StaleMap`, `ServerMapValidating`, `ServerMapIncompatible`, `ServerMapNotPlayable`, `SelectedMapUnavailable`, `ClientMapUnknown`, `ClientMapSearching`, `ClientMapWaitingForDownload`, `ClientMapDownloading`, `ClientMapInstallingOrVerifying`, `ClientMapUnavailable`, `ClientMapError`, `RequiredSlotEmpty`, `NoPlayers`, `InsufficientHumans`, `InsufficientSpawnPoints`, and `ClientGameNotReady`. In `LobbyStartAvailability.cs`, add a flat `LobbyStartRejection` DTO with `Reason`, `ClientIndex = -1`, `ClientMapPhase`, `Progress = -1`, and `ExpectedMapUid`, plus `string Serialize()` and `static LobbyStartRejection Deserialize(string data)`. Return the first stable blocker plus `AdditionalBlockingClientCount`; never localize inside the policy.

Expose the dependency-neutral map-only subset explicitly so the central `OpenRA.Game.Server` guard does not need Mod.Common slot/spawn inputs:

```csharp
public static LobbyStartAvailability EvaluateMapSafety(
	Session lobbyInfo,
	bool requireCurrentMapReadiness);
```

`Evaluate(LobbyStartInputs)` must call `EvaluateMapSafety(...)` first, then apply requester, slot, player-count, ordinary game-Ready, and spawn gates. The map-only method owns the stable server-map/client-phase blocker ordering and the complete `NonBotClients` iteration; callers must not duplicate it. It reads only `Session.GlobalSettings.MapStatus` and accepted current-UID client state. Add `LobbyStartAvailability ApplyLocalMapAvailability(bool localMapAvailable)` as a conservative client-UI overlay that can return `SelectedMapUnavailable`; no server caller or central guard passes a remote/local `MapPreview` boolean into the shared policy.

Set `RequireCurrentMapReadiness = server.IsMultiplayer` for legacy `startgame`, automatic start, the new safe command, and the central server guard. This server-owned fact cannot be spoofed by a client. The client-side iOS evaluation separately calls `ApplyLocalMapAvailability(map.Status == Available)` after evaluating the server-owned model; a dedicated server never reads or receives a remote filesystem-availability boolean.

- [ ] **Step 5: Protect all network start paths and preserve desktop/local behavior**

Register `startgame_safe`. Parse exactly one expected UID, require admin, evaluate under the lobby lock, and call `server.StartGame()` only when allowed. On rejection:

```csharp
server.SendOrderTo(conn, "SafeStartRejected", rejection.Serialize());
```

For every Multiplayer/Dedicated lobby, call `EvaluateMapSafety(...)` from legacy `startgame`, `CheckAutoStart`, and a fail-closed guard at the top of `Server.StartGame()` while its existing `LobbyInfo` lock is held, before the start log/state mutation and before its existing `IsInvalid` drop loop. This prevents raw legacy orders or future call sites from kicking a downloading participant. Keep the original desktop confirmation UI and its ability to ignore ordinary game NotReady; only the map safety invariant becomes universal.

Refine `ValidateCommand` to compare the exact first command token and allow `state`, `map_status`, `startgame`, and `startgame_safe` while game Ready. Reject client attempts to set server-owned `Invalid`/`Disconnected`; in every `server.IsMultiplayer` lobby, accept `state Ready/NotReady` only if `client.IsMapReadyFor(currentUid)`.

The network-owned publisher may report the real local `MapPreview` state in every hosted lobby type, including Local/Skirmish; this is telemetry/state synchronization, not a fabricated sequence. For `server.IsMultiplayer == false`, keep accepting ordinary `state Ready/NotReady` without requiring `MapPhase == Ready`, and bypass `EvaluateMapSafety` in the central start guard; `RestartGame` creates a Local server and therefore follows this explicit bypass. Add executable `Game.LoadMap`, Local, Skirmish, and Restart regressions proving their direct `state Ready` path reaches `Server.StartGame` without being dropped by the existing Invalid-client loop. In Multiplayer/Dedicated, only a changed current-map UID resets all readiness; joining initializes only the new client, and disconnect/same-UID lobby changes preserve the remaining accepted states.

- [ ] **Step 6: Receive structured rejection and clear pending reliably**

Add to `OrderManager`:

```csharp
public event Action<LobbyStartRejection> SafeStartRejected = _ => { };
internal void NotifySafeStartRejected(LobbyStartRejection rejection) => SafeStartRejected(rejection);
```

Parse `SafeStartRejected` in `UnitOrders` and invoke the event. Malformed rejection payloads must be ignored with one diagnostic, not crash the lobby. `LobbyLogic` must subscribe with a named handler and remove that exact handler from its normal disposal/teardown path; add a dispose/reopen regression proving a rejection after disposal cannot mutate the old lobby and a reopened lobby observes the event exactly once.

- [ ] **Step 7: Update the multiplayer smoke handshake**

Before the smoke sends game `state Ready`, wait until the current map is locally Available, publish/observe current-UID `Ready/100`, and wait until `LocalClient.IsMapReadyFor(uid)`. Add a blocked phase where one participant remains `Downloading/42`, require the host safe-start request to be rejected without disconnect, then publish Ready and require one safe-start request to begin the game. Preserve existing timeouts and diagnostics.

- [ ] **Step 8: Verify GREEN and bypass coverage**

Run the Step 3 command plus Task 1 tests. Expected: all start/policy/protocol cases pass, including legacy-command bypass, auto-start bypass, host-already-Ready whitelist, Local/Skirmish, and Restart coverage.

## Task 4: Render Truthful iOS Blockers Without Adding Lobby Height

**Files:**
- Create: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyStartStatus.cs`
- Create: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyMapStatusPresentation.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyUtils.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyLayout.cs`
- Modify: `engine/mods/common/chrome/lobby-players.yaml`
- Modify: `engine/mods/common/fluent/chrome.ftl`
- Modify: `engine/mods/common/fluent/zh-CN/chrome.ftl`
- Modify: `engine/OpenRA.Test/IosLobbyResponsiveLayoutTest.cs`
- Modify: `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs`

- [ ] **Step 1: Write RED dynamic-Start tests**

Test `IosLobbyStartStatus.Format(availability, buttonWidth, font)` for both locales and both phone snapshots. Require these full compact values without clipping the reason or percentage:

```text
EN: Validating map… | Map unavailable | Kevin failed | Kevin verifying…
    Kevin 42% | Waiting for Kevin | 2 downloading · 42%
    Fill required slots | Add a player | Need 2 players | Enable more spawns
ZH: 地图校验中… | 地图不可用 | Kevin 下载失败 | Kevin 校验中…
    Kevin 下载 42% | 等待 Kevin 下载 | 2 人下载 · 42%
    请填满必需位置 | 请加入玩家 | 至少需要 2 人 | 出生点不足
```

Use `ActualIosFontRenderer`, the real `layout.Start` width, and the `IosBold` font. For an arbitrarily long player name, require a whole-string fallback such as `2 downloading · 42%`/`2 人下载 · 42%`; do not ellipsize only the reason or progress.

- [ ] **Step 2: Write RED live-row widget tests**

Add default-hidden widget inventory assertions for `IOS_MAP_STATUS_TEXT` and `IOS_MAP_STATUS_ICON` in both editable and noneditable templates. First test the pure seam `IosLobbyMapStatusPresentation.For(client, currentUid, isIos)` and require `ShowMapStatus`, `Text`, `Icon`, and `ShowReady` values without constructing an OrderManager. Then reflect `LayoutIosPlayerRow` against synthetic rows and require the new widgets to equal `layout.PlayerReady`, stay 48×48pt physical, and be mutually exclusive with `STATUS_CHECKBOX`/`STATUS_IMAGE`:

```csharp
Assert.That(statusText.IsVisible(), Is.True);
Assert.That(statusText.GetText(), Is.EqualTo("42%"));
Assert.That(statusIcon.IsVisible(), Is.False);
Assert.That(readyCheckbox.IsVisible(), Is.False);
```

Cover matching-current-UID `Unknown`, `Searching`, `WaitingForDownload`, `Downloading/-1`, `Downloading/42`, `InstallingOrVerifying`, `Unavailable`, `Error`, and `Ready`; also cover bot, stale UID, repeated layout application, iOS-only visibility, and desktop `Downloading/42` remaining hidden with original checkbox/checkmark behavior. Add a subscription-lifecycle test: after disposing the lobby, neither a readiness delta nor a safe-start rejection may invoke the old instance; reopening subscribes each named handler once.

- [ ] **Step 3: Run focused UI tests and capture RED**

Run:

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~IosLobbyResponsiveLayoutTest|FullyQualifiedName~IosMenuLayoutPolicyTest' \
  -v minimal
```

Expected: missing formatter/widget IDs and old Start wiring fail.

- [ ] **Step 4: Add short Fluent strings and a measured formatter**

Add the exact English/Chinese strings from Step 1 under `button-ios-lobby-start-*` keys. Include separate named and aggregate keys for downloading, verifying, failed, and waiting; pass `player`, `count`, and `progress` as Fluent variables. Add `notification-client-no-slot-after-map-change = The selected map has no available player slot for you.` and `notification-client-no-slot-after-map-change = 所选地图没有可用的玩家位置。` in the matching locales. Implement `IosLobbyStartStatus.Format` to try the named form, measure it, then replace the complete string with the aggregate form if it exceeds the button width.

Do not attach `IosResponsiveText.Configure` to hide a failing string-width contract. Validation has no percentage; unknown-length download has no fake 0%.

- [ ] **Step 5: Wire one-tap iOS Start and pending recovery**

In `LobbyLogic`, keep the desktop `StartDisabled`, Force Start panel, and OK/Cancel bindings intact. Add local `CurrentStartAvailability()` and an iOS configuration predicate that preserves host, local-client, `gameStarting`, kick/force modal, and pending restrictions but deliberately omits only `LocalClient.IsReady`:

```csharp
bool IosConfigurationDisabled() =>
	!Game.IsHost || gameStarting || safeStartPending ||
	panel == PanelType.ForceStart || panel == PanelType.Kick ||
	orderManager.LocalClient == null;

startButton.IsDisabled = () => Platform.IsIOS
	? IosConfigurationDisabled() || !CurrentStartAvailability().CanStart
	: configurationDisabled() || StartDisabled();
```

For iOS, do not let the host’s ordinary game Ready state disable Start. On click, re-evaluate, set `safeStartPending`/`safeStartPendingAt = Game.RunTime`, schedule a request-identity-checked five-second clear, and send one `startgame_safe <uid>`; do not set the Force Start panel. Clear pending on `SafeStartRejected`, disconnect, and timeout; accepted StartGame follows the existing game-start teardown. Restore original Start text whenever the state is allowed, the local client is not host, or the platform is not iOS.

- [ ] **Step 6: Add and bind the compact Ready-cell widgets**

In both player templates add:

```yaml
Label@IOS_MAP_STATUS_TEXT:
	Visible: false
	IgnoreMouseOver: true
	Align: Center
	VAlign: Middle
	Font: IosBold
Label@IOS_MAP_STATUS_ICON:
	Visible: false
	IgnoreMouseOver: true
	Align: Center
	VAlign: Middle
	Font: IosBold
```

Set both bounds to `layout.PlayerReady`. Implement `IosLobbyMapStatusPresentation.For`: only `isIos && client.Bot == null && client.MapUid == currentUid && client.MapPhase != Ready` may show the new family; return `42%` for known download, `…` for indeterminate/search/install, `!` for error/unavailable, and `ShowReady = true` otherwise. Pass current UID into both editable and noneditable Ready bindings in `LobbyUtils`, and let their visibility/text delegates read the in-place client mutation directly. Subscribe named handlers to `OrderManager.ClientMapReadinessChanged` only to invalidate the cached Start status and to `SafeStartRejected` to clear pending; never call `UpdatePlayerList` for a delta. Remove both exact handlers together from the normal `LobbyLogic` disposal/teardown path. Do not add a new column or increase row/footer height.

- [ ] **Step 7: Verify GREEN across locales and devices**

Run the Step 3 command. Expected: both iPhones and all tablet snapshots pass localized width, visibility, containment, idempotence, and desktop-hidden assertions.

## Task 5: Enlarge Only the iOS Faction Button and Grouped Picker

**Files:**
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyLayout.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/IosTouchWidgetPolicy.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/DropDownButtonWidget.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyUtils.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs`
- Modify: `engine/OpenRA.Test/IosLobbyResponsiveLayoutTest.cs`
- Modify: `engine/OpenRA.Test/IosTouchWidgetPolicyTest.cs`
- Modify: `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs`

- [ ] **Step 1: Write RED ten-target player-column tests**

Across every supported snapshot, require semantic minimums `{ name:2, color:1, faction:3, team:1, handicap:1, spawn:1, ready:1 } * MinimumTarget`, a total row minimum of ten targets, positive contained non-overlapping columns, and unchanged footer/main heights. Prove the iPhone 844 row has positive remaining slack.

Reflect `LayoutIosPlayerRow(row, layout, policy)` and require:

```csharp
Assert.That(faction.Bounds, Is.EqualTo(layout.PlayerFaction));
Assert.That(faction.Bounds.Width / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(144));
Assert.That(faction.Bounds.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
Assert.That(flag.StretchToFit, Is.True);
Assert.That(flag.Bounds.Width / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(40));
Assert.That(flag.Bounds.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(20));
Assert.That(flag.Bounds.Width, Is.EqualTo(2 * flag.Bounds.Height).Within(1));
Assert.That(name.Bounds.X, Is.GreaterThanOrEqualTo(flag.Bounds.Right + layout.Gap));
Assert.That(name.Bounds.Right, Is.LessThanOrEqualTo(faction.UsableWidth));
```

Use real bounds, not expanded `EventBounds`, and reapply layout to prove idempotence.

- [ ] **Step 2: Write RED faction-popup policy tests**

Define:

```csharp
public readonly struct IosDropDownLayout
{
	public static IosDropDownLayout Default { get; }
	public static IosDropDownLayout FactionPicker { get; }
	public int MinimumWidthTargets { get; }
	public int MaximumHeightTargets { get; }
	public int HeaderHeightPoints { get; }
	public Size ItemImagePoints { get; }
	public bool OmitSingletonHeaders { get; }
}
```

Test `FactionPicker` as `5, 6, 24, 40×20, true`. For iPhone/iPad Safe Areas require panel width `min(safeWidth, max(requested, 5*target))`, height `min(safeHeight, 6*target)`, a 48pt scrollbar, 48pt interactive items, 24pt noninteractive headers, iOS faction `TopBottomSpacing = 0`, and content width of approximately 190pt or more at the minimum phone scale.

Use the actual RA2 group counts `[3,1,5,4]`, build the real child sequence, and require at least five selectable children whose actual `Bounds.Bottom <= panel.Bounds.Height` in the initial 288pt viewport. Achieve this by omitting only the singleton group header and setting spacing to zero on iOS. Require every header and the original spacing of 2 on desktop.

Lock the complete desktop baseline in RED before implementation: parse the common YAML and/or construct the desktop widgets to require main `FACTION` `140×25`, main and item flags `30×15` with the original `StretchToFit` value, header height `13`, item height `25`, every group header present, and `TopBottomSpacing = 2`. Repeat the construction with `isIos = false` while passing `IosDropDownLayout.FactionPicker` and require every one of those bounds/visibility/spacing values to remain identical.

- [ ] **Step 3: Run the faction RED suite**

Run:

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~IosLobbyResponsiveLayoutTest|FullyQualifiedName~IosTouchWidgetPolicyTest|FullyQualifiedName~IosMenuLayoutPolicyTest' \
  -v minimal
```

Expected: old seven-target minimum, generic three-target popup, 48pt headers, small non-stretched flags, and premature faction text truncation fail.

- [ ] **Step 4: Implement explicit column minima**

In `IosLobbyLayout`, set `minimumPlayerWidth = 10 * policy.MinimumTarget`. Replace the old “one target each plus weights” calculation with minimum units `{ 2, 1, 3, 1, 1, 1, 1 }`; distribute remaining pixels with the existing `{ 3, 1, 2, 1, 1, 1, 1 }` weights and give the final column the remainder so rounding cannot create a gap.

Pass `IosMenuLayoutPolicy` into `LayoutIosPlayerRow`. Give `FACTION` the full column bounds. Within `DropDownButtonWidget.UsableWidth`, use a 40×20pt aspect-fit flag, a policy gap, and the remaining label width; reserve the marker’s full button height on the right. Set `StretchToFit = true` only in this iOS path.

- [ ] **Step 5: Add a faction-only grouped-dropdown preset**

Keep every existing `ShowDropDown` signature and make it forward to `IosDropDownLayout.Default`. Add one grouped overload with an `IosDropDownLayout` final parameter. Thread it through `AdaptIosDropDownPanel` and `AdaptIosDropDownItem`; ignore the preset on desktop.

Extend `IosTouchWidgetPolicy` with:

```csharp
public static int DropDownPanelWidth(
	int requestedWidth, int target, int safeWidth, int minimumWidthTargets);
public static int DropDownMaximumHeight(
	int requestedHeight, int target, int safeHeight, int maximumHeightTargets);
public static bool ShowDropDownGroupHeader(
	int optionCount, bool isIos, IosDropDownLayout layout);
```

The faction adapter sets `panel.TopBottomSpacing = 0`, uses a 24pt header, 48pt option, 40×20pt centered aspect-fit `FLAG`, and a `LABEL` beginning one Gap after the flag. Continue to use existing `PlacePopup` for Safe Area clamping. Leave default spacing unchanged for every other dropdown and all desktop paths.

- [ ] **Step 6: Use the preset only for factions and defer truncation**

In `LobbyUtils.ShowFactionDropDown`, pass `IosDropDownLayout.FactionPicker`. Change the label delegate so truncation runs after the adapter has set final bounds:

```csharp
var fullName = FluentProvider.GetMessage(faction.Name);
label.GetText = () => WidgetUtils.TruncateText(
	fullName, label.Bounds.Width, Game.Renderer.Fonts[label.Font]);
```

Do not modify the desktop YAML baselines: main faction `140×25`, header `13`, item `25`, and item flag `30×15` remain unchanged.

- [ ] **Step 7: Verify GREEN and desktop isolation**

Run the Step 3 command. Expected: all device geometry, five-visible-choice, four-corner popup, flag/label/scrollbar non-overlap, whole-row hit, and desktop baseline tests pass.

## Task 6: Run the Full Test, Bundle, and Manifest Gates

**Files:**
- Modify: `packaging/audit_ios_touch_runtime_bundle.py`
- Modify: `packaging/tests/test_ios_touch_runtime_bundle.py`
- Inspect: `packaging/public-content-manifest.json` (leave unchanged when no existing entry covers the changed files)

- [ ] **Step 1: Add RED bundle/source contracts**

Extend the runtime audit fixture tests to require protocol 22, both `SyncClientMapReadiness` directions, the narrow readiness-changed event, the iOS-only Start path, default-hidden status widget IDs, the two Fluent locales, ten-target columns, and `IosDropDownLayout.FactionPicker`. Add tamper cases for protocol downgrade, removed map gate, a readiness delta changed to call `Game.SyncLobbyInfo()`, missing Chinese blocker, visible desktop status widget, `Downloading/100`, and faction preset replaced by Default.

- [ ] **Step 2: Run the Python RED tests**

Run:

```sh
/usr/bin/python3 -m unittest \
  packaging.tests.test_ios_touch_runtime_bundle \
  packaging.tests.test_ios_touch_skin_assets
```

Expected: the new contract/tamper tests fail until the audit recognizes the intended sources and manifest entries.

- [ ] **Step 3: Implement the minimum audit updates**

Keep the audit structural: verify exact enum/order/widget/Fluent/preset wiring and actual bundle hashes, not incidental whitespace or method line numbers. Preserve all existing quarter-ring, atlas, lobby, scroll, managed assembly, and retail-asset checks.

- [ ] **Step 4: Run all focused C# suites**

Run:

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~IosLobbyMapReadinessTest|FullyQualifiedName~IosLobbyStartAvailabilityTest|FullyQualifiedName~IosLobbyResponsiveLayoutTest|FullyQualifiedName~IosTouchWidgetPolicyTest|FullyQualifiedName~IosMenuLayoutPolicyTest|FullyQualifiedName~IosLanServerCompatibilityTest' \
  -v minimal
```

Expected: all selected tests pass with a nonzero count and no skipped requirement.

- [ ] **Step 5: Run full C#, Python, YAML, and diff gates**

Run:

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 -v minimal
/usr/bin/python3 -m unittest discover -s packaging/tests -p 'test_*.py'
./utility.sh --check-yaml
git diff --check
```

Expected: full C# and Python suites pass; YAML exits 0 with only recorded baseline warnings; diff check prints nothing.

- [ ] **Step 6: Build the affected assemblies**

Run:

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Game/OpenRA.Game.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj -c Release --no-restore -m:1
```

Expected: both builds complete with 0 errors; record the known NU1901 warning separately if it remains.

- [ ] **Step 7: Refresh only changed public-content entries**

For every changed tracked bundle file, compute `shasum -a 256` and `wc -c`, update only its existing manifest entry, and run the Python audit again. Do not add obj/bin, private profiles, caches, downloaded maps, screenshots, or retail assets.

## Task 7: Generate 0016, Fresh-Replay It, Review, and Commit

**Files:**
- Create: `engine-patches/0016-ios-lobby-direct-start-progress-faction-picker.patch`
- Modify: `engine-patches/README.md`
- Modify: every implementation/test/packaging file explicitly listed above

- [ ] **Step 1: Verify the pinned official archive**

Run:

```sh
shasum -a 256 /private/tmp/openra-release-20250330.5qwdqs/openra-release-20250330.zip
```

Expected exactly:

```text
ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a
```

Do not download or substitute a different archive when this verified copy exists.

- [ ] **Step 2: Build an exact post-0015 baseline**

Create a new `mktemp -d` directory, extract the verified ZIP with `ditto -x -k`, locate the single extracted engine root, and apply `0001` through `0015` in filename order with `patch -p1 -s --batch`. Fail on any `.rej`, fuzzy/offset surprise, or nonzero patch result.

- [ ] **Step 3: Generate 0016 from an explicit engine allowlist**

Stage `a/` from the post-0015 replay and `b/` from the workspace for exactly the engine files listed in this plan, including new source/test files, common YAML, and both common Fluent files. Use `diff -ruN a b` once to create `engine-patches/0016-ios-lobby-direct-start-progress-faction-picker.patch`.

Before accepting it, inspect headers:

```sh
rg '^diff -ruN |^--- |^\+\+\+ ' engine-patches/0016-ios-lobby-direct-start-progress-faction-picker.patch
```

Expected: every header is under the approved engine allowlist; no `mods/ra2`, packaging, docs, obj/bin, app bundle, profile, cache, map, screenshot, or private path appears.

- [ ] **Step 4: Prove forward, reverse, and byte-for-byte replay**

In a second fresh extraction, apply `0001` through `0016`. Then:

- require a second `patch --forward --dry-run --batch -p1` of 0016 to fail without changing the tree;
- require `patch -R --dry-run --batch -p1` of 0016 to succeed;
- `cmp` every approved 0016 engine path against the workspace;
- run a recursive tracked-source comparison excluding only documented pristine release noise such as generated `VERSION`, bin, and obj;
- require zero `.rej` files.

- [ ] **Step 5: Document the patch**

Add a README row:

```markdown
| `0016-ios-lobby-direct-start-progress-faction-picker.patch` | Map-readiness protocol/start gate, iOS lobby status UI, faction picker, and regression suites | Adds protocol-v22 current-map progress, prevents every network multiplayer start until each connected human map is ready, removes the iOS force-start confirmation, and enlarges only the iOS faction picker. |
```

Preserve the official archive checksum and all 0001–0015 descriptions.

- [ ] **Step 6: Run ordered reviews**

Dispatch a fresh spec-compliance reviewer against the approved design, plan, diff, and focused test output. Resolve every Critical/Important issue and re-run until PASS. Then dispatch a different code-quality reviewer; resolve every Critical/Important issue and re-run until PASS. Reviewers must check desktop isolation, all start bypasses, UID/phase invariants, throttle/log amplification, mixed iOS/desktop rooms, minimum iPhone geometry, and patch replay.

- [ ] **Step 7: Re-run final verification after review fixes**

Repeat Task 6 Steps 4–6 plus fresh replay. Evidence must come from the final working tree, not pre-review output.

- [ ] **Step 8: Stage only the exact feature allowlist**

Use ordinary `git add` for tracked docs, packaging, manifest, patch, and README files. Use exact `git add -f -- <path>` for only the approved materialized engine paths. Show:

```sh
git diff --cached --name-status
git diff --cached --check
```

Confirm no pre-existing untracked artifacts, private profiles, device data, downloaded maps, screenshots, obj/bin, app bundle contents, caches, credentials, or files larger than 50 MB are staged.

- [ ] **Step 9: Commit the verified implementation**

Commit with:

```sh
git commit -m "Simplify iOS multiplayer lobby flow"
```

Expected: the commit contains implementation, tests, 0016, README, audit, and manifest updates as one replay-consistent change.

## Task 8: Build, Sign, and Cover-Install the Final iOS App

**Files:** no source changes expected

- [ ] **Step 1: Resolve current devices and preserve data evidence**

Enumerate connected physical devices at execution time with `xcrun devicectl list devices`. For each available iPhone/iPad target, record CoreDevice ID, hardware UDID, lock/developer-mode status, installed `com.openra.ipad.personal` metadata, current process, exception-log count, and `Documents/OpenRA/settings.yaml` SHA-256. Never uninstall and never delete app Documents.

- [ ] **Step 2: Resolve a valid signing identity/profile and build Release**

Select the currently valid Apple Development identity and provisioning profile that contains every target UDID. Build:

```sh
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -c Release -r ios-arm64 -m:1 \
  -p:CodesignKey='<resolved Apple Development identity>' \
  -p:CodesignProvision='<resolved profile UUID>'
```

Expected: exit 0 and `ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app`.

- [ ] **Step 3: Run signed-bundle gates**

Run `codesign --verify --deep --strict`, inspect entitlements/profile, and run loading-brand plus touch-runtime audits against the new app. Require bundle ID `com.openra.ipad.personal`, Team/Application Identifier `267KQ4A3N4`, protocol 22 sources, updated common YAML/Fluent, source/bundle equality, and zero unexpected retail/private content.

- [ ] **Step 4: Cover-install without uninstalling**

For each freshly resolved unlocked target, run:

```sh
xcrun devicectl device install app \
  --device '<resolved CoreDevice ID>' --timeout 600 \
  --json-output '/private/tmp/ra2-ios-lobby-<device>-install.json' \
  ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app
```

Require successful JSON for the expected bundle/team/profile. If a requested connected target is locked or unavailable, report that exact target as blocked; do not silently skip it or claim all-device completion.

- [ ] **Step 5: Launch, sustain, and smoke the installed app**

Launch the new bundle, wait 10–15 seconds, and require a live PID. Run the loading smoke with a unique result directory. For multiplayer, verify on compatible devices that a 42%/validating participant keeps Start disabled with the correct text, Ready removes the blocker, one host tap starts without Force Start, and the enlarged faction picker shows at least five choices with 40×20pt flags.

- [ ] **Step 6: Prove device data preservation**

Re-read `settings.yaml`, exception logs, and installed-app metadata. Require the settings hash to match preinstall evidence and no new matching exception log. Report automated evidence separately from any remaining manual visual/real-network observation.
