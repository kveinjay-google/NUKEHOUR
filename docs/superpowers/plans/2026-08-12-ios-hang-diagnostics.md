# iOS Hang Diagnostics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist precise subsystem breadcrumbs when the iPad process remains alive but the game loop stops advancing.

**Architecture:** A pure heartbeat state model owns detection and ring-buffer rules. An iOS-only background service samples that model and writes a direct, immediately flushed report, while game/network/render boundaries publish breadcrumbs.

**Tech Stack:** C#, OpenRA engine, NUnit, .NET for iOS.

---

### Task 1: Heartbeat state model

**Files:**
- Create: `engine/OpenRA.Game/Support/HangDiagnosticsState.cs`
- Create: `engine/OpenRA.Test/HangDiagnosticsStateTest.cs`

- [ ] Write failing tests for stale detection, pause suppression, report throttling, and ring ordering.
- [ ] Run the focused tests and verify they fail because the model is missing.
- [ ] Implement the thread-safe state model and immutable snapshots.
- [ ] Run the focused tests and verify they pass.

### Task 2: Watchdog and instrumentation

**Files:**
- Create: `engine/OpenRA.Game/Support/HangDiagnostics.cs`
- Modify: `engine/OpenRA.Game/Game.cs`
- Modify: `engine/OpenRA.Game/Network/Connection.cs`
- Modify: `ios/OpenRA.iOS/AppDelegate.cs`

- [ ] Start the watchdog after log channels and support paths are initialized.
- [ ] Add main-loop, logic, world-tick, render, present, and network breadcrumbs.
- [ ] Pause and resume from iOS application lifecycle callbacks.
- [ ] Write reports directly to `Logs/hang-diagnostic.log` with immediate flush.

### Task 3: Verify and deploy

**Files:**
- No additional production files.

- [ ] Run focused diagnostics tests and the full test suite.
- [ ] Build the signed `ios-arm64` Release app.
- [ ] Install and launch on the paired iPad.
- [ ] Commit only diagnostics code, tests, and design documents.
