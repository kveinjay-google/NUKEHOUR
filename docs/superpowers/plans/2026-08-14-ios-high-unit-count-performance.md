# iOS High-Unit-Count Performance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a reproducible iPad Release benchmark, identify the top three bottlenecks, and verify only evidence-backed fixes.

**Architecture:** Extend the existing benchmark and resource sampling paths, add benchmark-only deterministic scenario control and category profiling, then compare the current partial-interpreter build with a generated AOT registry build. Production hot paths remain allocation-free while profiling is disabled.

**Tech Stack:** C#/.NET 8 iOS, OpenRA PerfHistory, NUnit, MSBuild/Roslyn generation, CoreDevice CLI, JSON/CSV

---

### Task 1: Audit and benchmark schema

**Files:**
- Create: `docs/performance/2026-08-14-ios-high-unit-count-audit.md`
- Create: `engine/OpenRA.Test/PerformanceBenchmarkSummaryTest.cs`
- Create: `engine/OpenRA.Game/Support/PerformanceBenchmarkSummary.cs`

- [ ] Write failing tests for percentile interpolation, FPS p5/p50, duration p50/p95, allocation rate, GC deltas and top-20 ordering.
- [ ] Run `dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --filter PerformanceBenchmarkSummaryTest` and verify the missing-type failure.
- [ ] Implement immutable sample/summary types and JSON-serializable scenario metadata.
- [ ] Re-run the focused tests and require zero failures.

### Task 2: Release benchmark purity

**Files:**
- Modify: `engine/OpenRA.Platforms.Default/OpenGL.cs`
- Modify: `engine/OpenRA.Game/Support/HangDiagnostics.cs`
- Test: `engine/OpenRA.Test/IosPerformancePolicyTest.cs`

- [ ] Write source-policy tests proving iOS non-Debug `CheckGLError` returns before `glGetError`, and benchmark mode suppresses per-frame diagnostic stages without disabling watchdog heartbeat.
- [ ] Run the tests and verify they fail on current code.
- [ ] Add compile-time iOS Release GL error policy plus runtime benchmark diagnostic policy.
- [ ] Re-run focused and complete engine tests.

### Task 3: Resource and category sampling

**Files:**
- Modify: `engine/OpenRA.Game/Support/RuntimeResourceSampler.cs`
- Create: `engine/OpenRA.Game/Support/PerformanceCategoryProfiler.cs`
- Modify: `engine/OpenRA.Game/Support/Benchmark.cs`
- Modify: `ios/OpenRA.iOS/AppDelegate.cs`
- Test: `engine/OpenRA.Test/PerformanceCategoryProfilerTest.cs`

- [ ] Write failing tests for allocation/GC deltas, thermal provider failures, disabled zero-record behavior and deterministic top-20 aggregation.
- [ ] Add `GC.GetTotalAllocatedBytes`, `GC.CollectionCount(0..2)`, and an iOS thermal provider sourced from `NSProcessInfo.ProcessInfo.ThermalState`.
- [ ] Add benchmark-only scope recording with cached category/type identifiers and no formatting in hot paths.
- [ ] Extend Benchmark to preallocate 35 seconds of samples and write once after sampling.

### Task 4: Simulation and rendering instrumentation

**Files:**
- Modify: `engine/OpenRA.Game/Actor.cs`
- Modify: `engine/OpenRA.Game/TraitDictionary.cs`
- Modify: `engine/OpenRA.Game/World.cs`
- Modify: `engine/OpenRA.Game/Graphics/WorldRenderer.cs`
- Modify: `engine/OpenRA.Game/Renderer.cs`

- [ ] Instrument Actor Activity and ITick Trait calls only while profiler mode is enabled.
- [ ] Add scopes for effects/projectiles, renderable generation, VXL, sprite batching, draw submission and present.
- [ ] Verify disabled mode does not allocate by asserting allocation deltas over 10,000 no-op scopes.
- [ ] Verify actor/trait iteration and result application order are unchanged.

### Task 5: Deterministic scenario controller and Mac runner

**Files:**
- Create: `OpenRA.Mods.RA2/Traits/IosHighUnitBenchmark.cs`
- Modify: `mods/ra2/rules/world.yaml`
- Modify: `ios/OpenRA.iOS/AppDelegate.cs`
- Create: `ios/scripts/run-high-unit-benchmarks.sh`
- Test: `engine/OpenRA.Test/IosHighUnitBenchmarkConfigurationTest.cs`

- [ ] Test parsing and validation for count, state, camera, pause, AI, render scale, warmup and duration.
- [ ] Spawn units on stable passable cells and issue deterministic idle/move/combat orders at fixed ticks.
- [ ] Add camera-visible/empty and pause controls without touch input.
- [ ] Run the matrix through CoreDevice, pull each completed report, and reject incomplete or thermally inconsistent runs.

### Task 6: Generated AOT object registry

**Files:**
- Create: `build/OpenRA.AotRegistry.Generator/*`
- Modify: `engine/OpenRA.Game/ObjectCreator.cs`
- Modify: `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Replace: `ios/OpenRA.iOS/AotPreserve.cs`
- Test: `engine/OpenRA.Test/AotObjectRegistryCoverageTest.cs`

- [ ] Write a failing coverage test that loads the RA2 manifest/rules/chrome and reports every unresolved dynamically named type.
- [ ] Generate direct parameterless and `UseCtor` delegates for accessible types in OpenRA.Game, Common, Cnc and RA2 assemblies.
- [ ] Make iOS ObjectCreator use the generated registry first and keep desktop reflection unchanged.
- [ ] Remove `MtouchInterpreter` only after coverage, YAML/map tests, Release build and device launch pass.
- [ ] Run the same benchmark scenarios for interpreter and registry builds.

### Task 7: Evidence-ranked fixes and verification

**Files:**
- Create: `docs/performance/results/<build-id>/*.json`
- Modify only files belonging to the measured top three hotspots.

- [ ] Rank hotspots by p95 contribution, total exclusive time and scaling from 100 to 1000 units.
- [ ] For each hotspot, add a failing behavior/performance regression test before its isolated fix.
- [ ] Re-run the identical matrix and attach before/after JSON with thermal state and build ID.
- [ ] Run full engine tests, YAML/map checks, signed Release build, install and device smoke test.
- [ ] Commit each evidence-backed optimization separately; never include unrelated dirty-tree changes.
