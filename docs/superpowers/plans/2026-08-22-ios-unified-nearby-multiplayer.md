# iOS Unified Nearby Multiplayer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make LAN rooms immediately visible and add one iOS nearby transport that works over infrastructure Wi-Fi, Personal Hotspot, and Apple peer-to-peer Wi-Fi.

**Architecture:** Fix active-Mod compatibility and separate local discovery from the Internet query. Add a platform-neutral nearby service and compressed TXT advertisement codec, then implement Network.framework browsing, advertising, and loopback TCP relays on iOS so the existing OpenRA protocol remains unchanged.

**Tech Stack:** C#/.NET 8 iOS AOT, OpenRA TCP networking, Apple Network.framework/Bonjour, NUnit, `xcrun devicectl`.

---

### Task 1: Restore iOS LAN room compatibility

**Files:**
- Modify: `ios/OpenRA.iOS/AppDelegate.cs`
- Modify: `engine/OpenRA.Game/Network/GameServer.cs`
- Test: `engine/OpenRA.Test/IosLanServerCompatibilityTest.cs`

- [ ] Add a failing test that passes identical current/advertised Mod ID and Version with an empty external-Mod registry and expects compatibility.
- [ ] Run the focused test and confirm that the current ExternalMods-only decision returns false.
- [ ] Add `Engine.LaunchPath=<NSBundle.MainBundle.ExecutablePath>` to iOS launch arguments.
- [ ] Extract `GameServer.IsAdvertisedModCompatible(advertisedMod, advertisedVersion, currentMod, currentVersion, externalMatch)` and make an exact active-Mod match return true.
- [ ] Run the focused test and existing `LanGameProbeTest`/`IosMultiplayerPolicyTest` tests.

### Task 2: Make local discovery independent of the Internet query

**Files:**
- Create: `engine/OpenRA.Mods.Common/Network/LocalGameDiscovery.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/ServerListLogic.cs`
- Test: `engine/OpenRA.Test/LocalGameDiscoveryTest.cs`

- [ ] Add failing tests for Beacon YAML parsing, endpoint rewriting, default-filter visibility, LAN/Internet deduplication, and invalid-payload diagnostics.
- [ ] Add a test with a never-completing Internet query and assert that a LAN snapshot is emitted immediately.
- [ ] Implement a pure parser/merger that preserves Session GUID and transport metadata.
- [ ] Cache Internet results separately; Beacon updates call the local refresh path directly on the next game tick and never return merely because `activeQuery` is true.
- [ ] Log rejected local advertisements with a rate-limited reason.
- [ ] Run focused tests and the complete OpenRA.Test suite.

### Task 3: Define nearby contracts and TXT codec

**Files:**
- Create: `engine/OpenRA.Game/Network/NearbyMultiplayer.cs`
- Create: `engine/OpenRA.Game/Network/NearbyAdvertisementCodec.cs`
- Test: `engine/OpenRA.Test/NearbyAdvertisementCodecTest.cs`

- [ ] Add failing round-trip tests for UTF-8 room YAML, 220-byte maximum chunks, missing chunks, reordered chunks, bad checksum, and a 16 KiB decompression ceiling.
- [ ] Define `NearbyGameAdvertisement`, `NearbyConnectionRoute`, `INearbyMultiplayer`, and the static platform registration point.
- [ ] Implement versioned compression/chunking with deterministic SHA-256 verification and bounded decompression.
- [ ] Run focused codec tests and confirm zero allocations are required on repeated unchanged snapshots beyond result construction.

### Task 4: Implement the iOS Network.framework transport

**Files:**
- Create: `ios/OpenRA.Platforms.iOS/IosNearbyMultiplayer.cs`
- Create: `ios/OpenRA.Platforms.iOS/IosNearbyStreamRelay.cs`
- Modify: `ios/OpenRA.Platforms.iOS/IosPlatform.cs`
- Modify: `ios/OpenRA.iOS/AppDelegate.cs`
- Modify: `ios/OpenRA.iOS/Info.plist`
- Test: `engine/OpenRA.Test/IosNearbyMultiplayerPolicyTest.cs`

- [ ] Add failing policy tests requiring `_openra-ra2._tcp` in `NSBonjourServices`, `IncludePeerToPeer = true` for browser/listener/connection, bounded relay buffers, and cancellation on disposal.
- [ ] Implement `NWBrowser` snapshot conversion using complete TXT records and a private serial `DispatchQueue`.
- [ ] Implement an ephemeral `NWListener` with Bonjour advertisement and host-side loopback relay to the actual OpenRA server port.
- [ ] Implement a client-side loopback listener that binds only `IPAddress.Loopback`, connects its selected `NWEndpoint`, and stops browsing before connection.
- [ ] Implement sequential NW send completion and socket read pumps; close both halves on error, EOF, app background, or explicit dispose.
- [ ] Register the implementation before `Game.InitializeAndRun` and add the Bonjour privacy declaration.
- [ ] Build `ios-arm64` to verify all Network.framework bindings and AOT callbacks.

### Task 5: Publish, browse, merge, and join nearby rooms

**Files:**
- Modify: `engine/OpenRA.Mods.Common/ServerTraits/MasterServerPinger.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/ServerListLogic.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/MultiplayerLogic.cs`
- Modify: `engine/OpenRA.Game/Network/GameServer.cs`
- Test: `engine/OpenRA.Test/NearbyRoomIntegrationTest.cs`

- [ ] Add a failing integration test that publishes one room through LAN and nearby sources and expects one visible row preferring LAN.
- [ ] Add a failing test that a nearby-only room carries its route ID into the join action and produces a loopback `ConnectionTarget`.
- [ ] Start/update/stop advertisement with server lifecycle events.
- [ ] Feed nearby snapshots into the same local aggregator and label the source in Chinese.
- [ ] Extend `GameServer` with ignored discovery metadata, and route nearby joins through `INearbyMultiplayer.TryCreateConnectionTarget`.
- [ ] Run focused tests and the complete test suite.

### Task 6: Extend automated two-device evidence

**Files:**
- Modify: `engine/OpenRA.Game/Support/IosMultiplayerSmoke.cs`
- Modify: `ios/scripts/run-multiplayer-smoke.sh`
- Test: `engine/OpenRA.Test/IosMultiplayerPolicyTest.cs`

- [ ] Add failing tests for `OPENRA_IOS_MULTIPLAYER_SMOKE_TRANSPORT=lan|nearby`, nearby client discovery without a supplied IP, a three-second discovery budget, and journal transport/path fields.
- [ ] Let the client select the host by run ID from the nearby snapshot and then join through the production route.
- [ ] Record discovery latency, selected transport, Network.framework path description, client count, map, frames, world tick, and desync status.
- [ ] Require both host and client journals to reach `PASSED` after at least 100 synchronized ticks.

### Task 7: Build, deploy, and validate on physical devices

**Files:**
- Evidence: `docs/testing/results/nearby-multiplayer-<run-id>/`

- [ ] Build one Release `ios-arm64` app and verify its embedded profile contains both iPad UDIDs.
- [ ] Install the identical app on iPad Air and iPad mini 6.
- [ ] Run the LAN smoke test and require room discovery, two clients, matching map, 100 world ticks, and no desync.
- [ ] Run the production UI flow: Air creates a room, mini displays it within three seconds, mini joins the same Lobby.
- [ ] Run the same smoke through the nearby route and retain both journals and logs.
- [ ] Repeat on Personal Hotspot; then disconnect infrastructure Wi-Fi while leaving Wi-Fi enabled and require the logged peer-to-peer path.
