# iOS Diagnostic Flight Recorder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a persistent structured flight recorder that identifies the deepest active engine operation during an iOS freeze and automatically transfers completed reports to the development Mac.

**Architecture:** A bounded engine-level recorder stores structured scope and heartbeat events and exposes consistent snapshots to the existing independent watchdog. The watchdog packages snapshots without calling the game thread; iOS MetricKit adds delayed system stack evidence; a Mac bridge pulls pending packages over the paired-device channel.

**Tech Stack:** C#/.NET 8 iOS, NUnit, Apple MetricKit, JSON/JSONL, `xcrun devicectl`, zsh/LaunchAgent.

---

### Task 1: Structured ring buffer and nested scopes

**Files:**
- Create: `engine/OpenRA.Game/Support/DiagnosticTrace.cs`
- Create: `engine/OpenRA.Game/Support/DiagnosticTraceBuffer.cs`
- Test: `engine/OpenRA.Test/DiagnosticTraceBufferTest.cs`

- [ ] Write tests proving bounded rotation, global ordering, nested parent spans, unmatched active scopes and heartbeat snapshots.
- [ ] Run `DOTNET_ROLL_FORWARD=Major dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~DiagnosticTraceBufferTest` and verify the tests fail because the types do not exist.
- [ ] Implement fixed-capacity `DiagnosticTraceBuffer`, `DiagnosticTraceEvent`, `DiagnosticTraceScope`, subsystem/phase enums, per-thread active scope tracking and immutable snapshots.
- [ ] Run the focused tests and verify all pass.
- [ ] Commit the recorder and tests.

### Task 2: Persistent sessions and diagnostic packages

**Files:**
- Create: `engine/OpenRA.Game/Support/DiagnosticSessionStore.cs`
- Create: `engine/OpenRA.Game/Support/DiagnosticPackageBuilder.cs`
- Test: `engine/OpenRA.Test/DiagnosticSessionStoreTest.cs`
- Modify: `engine/OpenRA.Game/Game.cs`

- [ ] Write tests for unclean-session recovery, atomic metadata replacement, report rotation and safe filenames.
- [ ] Run the focused tests and verify failure.
- [ ] Implement an active-session marker, periodic crash-safe trace checkpoints, pending `.oradiag` directories, `summary.json`, `timeline.txt`, log tails and a ten-report quota.
- [ ] Start the session after support paths/log channels initialize; mark it clean only during orderly shutdown.
- [ ] Run focused tests and commit.

### Task 3: Multi-subsystem watchdog

**Files:**
- Modify: `engine/OpenRA.Game/Support/HangDiagnostics.cs`
- Modify: `engine/OpenRA.Game/Support/HangDiagnosticsState.cs`
- Modify: `engine/OpenRA.Test/HangDiagnosticsStateTest.cs`

- [ ] Add failing tests for independent subsystem heartbeats, suspected/confirmed thresholds, progress-aware long operations and duplicate-report suppression.
- [ ] Replace the single global stage with flight-recorder snapshots while retaining the existing text report as a compatibility summary.
- [ ] On confirmed freeze, checkpoint the trace and build a pending package from the watchdog thread.
- [ ] Verify tests and commit.

### Task 4: StartGame and world-load probes

**Files:**
- Modify: `engine/OpenRA.Game/Game.cs`
- Modify: `engine/OpenRA.Game/World.cs`
- Modify: `engine/OpenRA.Game/Rules/ActorInfo.cs` or the actual trait world-load dispatch site discovered with `rg "IWorldLoaded" engine/OpenRA.Game engine/OpenRA.Mods.Common`.

- [ ] Add scopes for old-world disposal, BeforeGameStart, World construction, renderer construction, GC, LoadComplete, palette refresh and PostLoadComplete.
- [ ] Wrap every `IWorldLoaded` and `IPostWorldLoaded` dispatch with a type-ID scope and progress count.
- [ ] Preserve behavior by using `using` scopes only; validate the engine test suite and YAML checks.
- [ ] Commit.

### Task 5: Network, server, replay and audio probes

**Files:**
- Modify: `engine/OpenRA.Game/Network/OrderManager.cs`
- Modify: `engine/OpenRA.Game/Network/Connection.cs`
- Modify: `engine/OpenRA.Game/Network/ReplayRecorder.cs`
- Modify: `engine/OpenRA.Game/Server/Server.cs`
- Modify: `ios/OpenRA.Platforms.iOS/Sdl2SoundEngine.cs`
- Modify: `engine/OpenRA.Game/Sound/PcmAudioMixer.cs`

- [ ] Split TickImmediate scopes into send-immediate and receive.
- [ ] Record packet type/size, receive/send queue depth, synchronous stream writes, immediate order processing, replay file operations and server event dispatch.
- [ ] Update audio heartbeat atomically in the callback and record decode/play/stop plus active playback count outside the callback.
- [ ] Ensure the callback performs no allocation, formatting, lock-taking diagnostic write or file I/O.
- [ ] Run audio, network and server tests and commit.

### Task 6: Simulation, render and input cursors

**Files:**
- Modify: `engine/OpenRA.Game/World.cs`
- Modify: `engine/OpenRA.Game/Game.cs`
- Modify: `engine/OpenRA.Platforms.Default/Sdl2PlatformWindow.cs`
- Modify: `ios/OpenRA.Platforms.iOS/IosTouchInput.cs` or the actual iOS touch bridge found with `rg "Touch|Finger" ios/OpenRA.Platforms.iOS`.

- [ ] Record atomic current Actor/Trait/effect/frame-end cursor and slow-item events.
- [ ] Add render-provider, annotations, UI, input-pump and Present scopes.
- [ ] Record anonymized touch ID, transformed coordinates, gesture transition and consumed result without chat content.
- [ ] Run input/layout tests, verify trace CPU/memory budget with a benchmark and commit.

### Task 7: MetricKit evidence

**Files:**
- Create: `ios/OpenRA.iOS/IosMetricKitDiagnostics.cs`
- Modify: `ios/OpenRA.iOS/AppDelegate.cs`
- Modify: `ios/OpenRA.iOS/OpenRA.iOS.csproj`

- [ ] Register an `IMXMetricManagerSubscriber` at launch and unregister on termination.
- [ ] Persist `MXDiagnosticPayload.JsonRepresentation`, including hang call-stack trees and crash diagnostics, into the pending diagnostics directory.
- [ ] Import past payloads on the next launch and correlate them with bundle version/session time.
- [ ] Build signed `ios-arm64` Release and commit.

### Task 8: Diagnostic-center controls

**Files:**
- Create: `engine/OpenRA.Mods.Common/Widgets/Logic/IosDiagnosticsCenterLogic.cs`
- Create: `mods/ra2/chrome/ios-diagnostics-center.yaml`
- Modify: `mods/ra2/chrome/mainmenu.yaml`
- Modify: `mods/ra2/chrome/ingame.yaml`
- Modify: `mods/ra2/mod.yaml`

- [ ] Add touch-sized status, Mark Problem, Save Snapshot, Share and report-list controls using existing iOS menu materials.
- [ ] Ensure package creation runs on the diagnostic worker and the UI only queues commands.
- [ ] Add layout tests for iPhone/iPad landscape safe areas and commit.

### Task 9: Mac automatic receiver

**Files:**
- Create: `tools/openra-diagnostic-bridge`
- Create: `tools/com.openra.diagnostic-bridge.plist.template`
- Create: `tools/install-openra-diagnostic-bridge.sh`
- Create: `tests/test_diagnostic_bridge.py`
- Modify: `.gitignore`

- [ ] Implement single-run and `--watch` modes that identify the paired device, pull `Documents/OpenRA/Diagnostics/Pending`, validate manifests, move reports into `.diagnostics/inbox/<id>` and leave a device-side imported marker.
- [ ] Generate `summary.md` and `index.json`; never delete the source package until local verification succeeds.
- [ ] Test command construction and idempotent imports with a fake `devicectl` executable.
- [ ] Install the removable per-user LaunchAgent and verify one automatic pull from the connected iPad.
- [ ] Commit.

### Task 10: Fault injection, full verification and deployment

**Files:**
- Create: `engine/OpenRA.Test/DiagnosticFaultClassificationTest.cs`
- Create: `docs/codebase-research/ios-diagnostic-flight-recorder.md`

- [ ] Inject simulated world-load, socket-write, audio-lock, present and frame-end stalls into the pure recorder/watchdog tests and verify each report identifies one deepest scope and its parent chain.
- [ ] Run focused tests, full engine tests, YAML validation, `git diff --check`, bridge tests and signed iOS Release build.
- [ ] Verify codesign, install the app on iPad `C5CA8947-3880-593B-84D6-B57DE642A8FD`, launch it, pull the startup report and confirm the bridge inbox.
- [ ] Record measured overhead and known limitations in the research document.
- [ ] Commit only diagnostics-related files and report verification evidence.
