# Runtime Performance Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add CPU, memory, FPS, and unambiguous resolution data to the existing runtime performance overlay.

**Architecture:** A small process sampler caches resource readings at a 500 ms interval and degrades safely when platform APIs are unavailable. The existing overlay composes those readings with renderer and performance-history data, while a read-only renderer property exposes physical output size.

**Tech Stack:** C#/.NET, OpenRA widget system, NUnit

---

### Task 1: Resource sampler

**Files:**
- Create: `engine/OpenRA.Game/Support/RuntimeResourceSampler.cs`
- Test: `engine/OpenRA.Test/RuntimeResourceSamplerTest.cs`

- [ ] **Step 1: Write failing sampler tests**

Cover normalized CPU calculation, clamping, cached samples inside the interval, refreshed samples after the interval, byte formatting, and unavailable values.

- [ ] **Step 2: Run tests and verify failure**

Run: `dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --filter RuntimeResourceSamplerTest`

Expected: compilation failure because `RuntimeResourceSampler` does not exist.

- [ ] **Step 3: Implement the sampler**

Add a sampler that accepts injectable clock/process providers for tests, samples at most twice per second, catches provider exceptions, and returns nullable CPU/resident-memory values plus managed heap bytes.

- [ ] **Step 4: Run focused tests**

Run: `dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --filter RuntimeResourceSamplerTest`

Expected: all sampler tests pass.

### Task 2: Renderer and overlay integration

**Files:**
- Modify: `engine/OpenRA.Game/Renderer.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/PerfDebugLogic.cs`
- Modify: `engine/mods/common/chrome/ingame-perf.yaml`

- [ ] **Step 1: Expose output resolution**

Add a read-only `OutputSize` property backed by the platform window surface size.

- [ ] **Step 2: Compose expanded performance text**

Show FPS and CPU together, resident and managed memory together, then existing timing/batch data followed by output resolution, viewport/downscale, and world framebuffer size.

- [ ] **Step 3: Resize the overlay label**

Increase the text label bounds so the additional lines are readable without clipping.

- [ ] **Step 4: Build affected projects**

Run: `dotnet build engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj`

Expected: build succeeds with zero errors.

### Task 3: Full verification and research record

**Files:**
- Modify: `docs/codebase-research/rendering-terrain-overlays.md`

- [ ] **Step 1: Document the metrics path**

Record the setting, widget logic, sampler, renderer surface-size source, sampling rate, and meaning of each displayed metric.

- [ ] **Step 2: Run the engine tests**

Run: `dotnet test engine/OpenRA.Test/OpenRA.Test.csproj`

Expected: all tests pass.

- [ ] **Step 3: Build and install the iOS app**

Use the repository iOS build/install workflow and launch it on the unlocked iPad.

- [ ] **Step 4: Perform device smoke testing**

Enable performance text, enter a match, and verify CPU/memory refresh, FPS movement, correct resolution readings, and continued touch responsiveness.
