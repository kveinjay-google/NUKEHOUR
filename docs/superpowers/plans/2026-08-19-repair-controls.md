# Repair Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make automatic repair functional and let an invalid manual-repair click immediately return to normal selection with a wrench cursor.

**Architecture:** Register the existing synchronized player manager in RA2 rules. In `RepairOrderGenerator`, materialize candidate repair orders; when a left click produces none, cancel the special input mode and reuse `SelectionUtils` to apply the same point-selection semantics immediately.

**Tech Stack:** C# 12, OpenRA order generators and selection utilities, RA2 MiniYaml rules, NUnit source-policy tests.

---

### Task 1: Add failing policy tests

**Files:**
- Create: `engine/OpenRA.Test/RepairControlsPolicyTest.cs`

- [ ] Assert `mods/ra2/rules/player.yaml` registers `AutoRepairManager:`.
- [ ] Assert `RepairOrderGenerator` contains invalid-left-click cancellation and point-selection fallback.
- [ ] Assert the generator returns `goldwrench` and `goldwrench-blocked` cursors.
- [ ] Run the focused test and verify failures against current files.

### Task 2: Implement repair controls

**Files:**
- Modify: `mods/ra2/rules/player.yaml`
- Modify: `engine/OpenRA.Mods.Common/Orders/RepairOrderGenerator.cs`

- [ ] Add `AutoRepairManager:` to the normal `Player` actor.
- [ ] Materialize valid repair orders on left click. If none exist, cancel input mode, select the highest-priority actor at the clicked world pixel using the current modifiers, and combine the selection with standard shift/click semantics.
- [ ] Keep repair mode active after valid repair orders and keep right-click cancellation.
- [ ] Return gold wrench cursor names for valid and blocked targets.

### Task 3: Verify and commit

- [ ] Run the focused policy test and build `OpenRA.Mods.RA2`.
- [ ] Run `git diff --check` on repair files.
- [ ] Commit only repair files with message `Fix automatic and manual repair controls`.
- [ ] Build the complete iOS Release application.
