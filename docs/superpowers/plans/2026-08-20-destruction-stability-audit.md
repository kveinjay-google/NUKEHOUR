# Destruction Stability Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Automatically destroy every spawnable RA2 unit and building through normal and explosive death paths, identify the exact callback on a hang, and produce a durable per-actor report on iPad.

**Architecture:** Extend the existing iOS benchmark world trait with a separately enabled destruction-audit state machine so no new YAML-reflected type or AOT registration is required. Actor discovery is derived from resolved rules, execution is serialized one actor at a time, and crash-safe progress is written before every risky transition. Existing structured diagnostic scopes are extended only at death-chain boundaries.

**Tech Stack:** C#/.NET 8 iOS AOT, OpenRA actor/trait rules, NUnit, shell + `xcrun devicectl`.

---

### Task 1: Configuration and deterministic case generation

**Files:**
- Modify: `OpenRA.Mods.RA2/Traits/IosHighUnitBenchmark.cs`
- Test: `engine/OpenRA.Test/DestructionAuditConfigurationTest.cs`

- [ ] Add failing NUnit tests for disabled defaults, iteration parsing, stable actor ordering, and the two required death types.
- [ ] Run the focused test and confirm it fails because destruction-audit configuration/case generation does not exist.
- [ ] Add `IosDestructionAuditConfiguration`, `DestructionAuditCase`, and a deterministic case builder that accepts resolved actor metadata.
- [ ] Re-run focused tests and confirm all cases pass.

### Task 2: Serialized runtime destruction audit

**Files:**
- Modify: `OpenRA.Mods.RA2/Traits/IosHighUnitBenchmark.cs`
- Test: `engine/OpenRA.Test/DestructionAuditStateMachineTest.cs`

- [ ] Add failing tests for the state transition sequence, timeout classification, skip classification, and resume-safe case identity.
- [ ] Run tests and confirm failure for the missing state machine.
- [ ] Implement the pure state machine, then connect it to `IWorldLoaded`/`ITick`: select a valid cell, create one actor, settle, kill with the requested damage type, allow effects to finish, clean up, and advance.
- [ ] Write `progress.log`, `results.csv`, and `summary.json` under the support directory with flush-to-disk semantics at risky boundaries.
- [ ] Re-run state-machine and configuration tests.

### Task 3: Death-chain diagnostic scopes

**Files:**
- Modify: `engine/OpenRA.Mods.Common/Traits/Health.cs`
- Modify: `engine/OpenRA.Mods.Common/Traits/FireWarheadOnDeath.cs`
- Modify: `engine/OpenRA.Mods.Common/Traits/SpawnActorOnDeath.cs`
- Modify: `engine/OpenRA.Mods.Common/Traits/Render/WithDeathAnimation.cs`
- Modify: `OpenRA.Mods.RA2/Traits/ExplodesForMaster.cs`
- Test: `engine/OpenRA.Test/DestructionDiagnosticInstrumentationTest.cs`

- [ ] Add a failing policy test requiring actor/trait/weapon-aware scopes at every synchronous death boundary and frame-end creation boundary.
- [ ] Run the test and confirm the missing boundaries are reported.
- [ ] Add minimal structured scopes and progress IDs without adding per-frame disk I/O.
- [ ] Re-run the diagnostic instrumentation test and existing diagnostic buffer tests.

### Task 4: iOS launcher and collector

**Files:**
- Create: `ios/scripts/run-destruction-audit.sh`
- Test: `engine/OpenRA.Test/DestructionAuditScriptTest.cs`

- [ ] Add a failing source-policy test requiring launch environment variables, completion polling, report copying, timeout handling, and nonzero exit on incomplete audit.
- [ ] Run the test and confirm the script is absent.
- [ ] Implement the script using `xcrun devicectl`, the configured iPad identifier, a mixed-terrain skirmish map, and app-container report copying.
- [ ] Re-run the script policy test.

### Task 5: Root-cause fixes discovered by the audit

**Files:**
- Modify only files named by the first failing actor's trace.
- Add one focused regression test per confirmed root cause under `engine/OpenRA.Test/`.

- [ ] Run YAML lint and the automated audit to collect the first failing object and deepest active scope.
- [ ] State one root-cause hypothesis from the trace and add a failing regression test.
- [ ] Apply the smallest production fix and verify the focused test turns green.
- [ ] Restart the full audit from the beginning; repeat only when a new independently confirmed failure appears.

### Task 6: Release and real-device verification

**Files:**
- Update generated AOT registry only if the coverage test proves it is required.
- Write results beneath `docs/testing/results/` only after a completed run.

- [ ] Run focused NUnit tests for audit configuration, state machine, diagnostics, and launcher.
- [ ] Run `./utility.sh ra2 --check-yaml` and record all errors/warnings.
- [ ] Build `ios/OpenRA.iOS/OpenRA.iOS.csproj` with `Configuration=Release` and `RuntimeIdentifier=ios-arm64`.
- [ ] Install and launch on the connected iPad, run the all-actor audit for three iterations, and copy the report.
- [ ] Verify `failed=0`, account explicitly for every skipped actor on a compatible terrain map, and preserve any hang bundle if failure occurs.
- [ ] Commit only the scoped audit and root-cause files after showing the staged file list.
