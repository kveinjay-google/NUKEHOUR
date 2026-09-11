# Structure Production Queue Limit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Limit every building and defense item to one queued copy and make all five-unit requests collapse to one on macOS, iOS, and Android.

**Architecture:** Configure the existing deterministic `ProductionQueueInfo.ItemLimit` on the RA2 `Building` and `Support` queues. Reuse `ProductionBatchPolicy.ResolveAcceptedCount`, which already caps a requested batch by the queue's per-item limit, so no platform UI path or network order can bypass the rule.

**Tech Stack:** OpenRA C#, NUnit, MiniYaml RA2 rules, Python version projection tool.

---

### Task 1: Add the failing rule test on all platforms

**Files:**
- Modify: `engine/OpenRA.Test/IosProductionBatchToggleTest.cs`

- [ ] Add a parameterized NUnit test that loads `mods/ra2/rules/player.yaml`, selects `Player -> ClassicProductionQueue@Building` and `Player -> ClassicProductionQueue@Support`, and asserts `ItemLimit` equals `1`.
- [ ] Run `dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --filter FullyQualifiedName~IosProductionBatchToggleTest.StructureQueuesLimitEachItemToOne` in each repository.
- [ ] Confirm all three runs fail because the `ItemLimit` node is absent.

### Task 2: Apply the deterministic queue rule

**Files:**
- Modify: `mods/ra2/rules/player.yaml`

- [ ] Add `ItemLimit: 1` to `ClassicProductionQueue@Building` and `ClassicProductionQueue@Support` only.
- [ ] Re-run the focused rule test in all three repositories and confirm it passes.
- [ ] Run the existing `ProductionQueueAcceptsFiveOrdinaryBuildingsButPreservesLimits` policy test to confirm an item limit of one admits one item initially and zero after one copy is queued.

### Task 3: Regenerate compatibility and independent product versions

**Files:**
- Modify: `packaging/nukehour-version.json`
- Modify: `packaging/nukehour-version.props`
- Modify: `mods/ra2/mod.yaml`
- Modify when present: `mods/ra2/manifest/common.yaml`

- [ ] Run `python3 packaging/nukehour_version.py bump patch` once in each repository.
- [ ] Run `python3 packaging/nukehour_version.py check` and `python3 -m unittest packaging.tests.test_nukehour_version -v` in each repository.
- [ ] Confirm macOS, iOS, and Android emit the same `nukehour-core-sha256-...` compatibility identity while retaining independent product versions.

### Task 4: Regression verification

**Files:**
- Verify only; no additional files expected.

- [ ] Run the complete `IosProductionBatchToggleTest` fixture in each repository.
- [ ] Run repository build checks sufficient to compile `OpenRA.Mods.Common` and `OpenRA.Test`.
- [ ] Inspect diffs to confirm no existing iOS dirty-worktree changes were overwritten and that non-structure queues have no new limit.
