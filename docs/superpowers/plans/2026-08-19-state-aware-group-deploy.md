# State-Aware Group Deploy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make each same-type selection group converge on deployed state unless every stable member is already deployed, in which case the group undeploys.

**Architecture:** Add an opt-in `IStatefulDeployOrder` interface that exposes the existing synchronized `DeployState`. A pure `BatchDeployOrderPolicy` determines a group's target state, while `CommandBarLogic` groups candidates by actor type and deploy trait type and preserves selection order when issuing filtered orders.

**Tech Stack:** C# 12, OpenRA traits/orders, LINQ, NUnit.

---

### Task 1: Define policy with failing tests

**Files:**
- Create: `engine/OpenRA.Test/BatchDeployOrderPolicyTest.cs`
- Modify: `engine/OpenRA.Mods.Common/TraitsInterfaces.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/CommandBarLogic.cs`

- [ ] Add tests asserting that mixed undeployed/deployed state targets `Undeployed`, all-deployed targets `Deployed`, and transition-only groups return no target.
- [ ] Add a reflection assertion that `GrantConditionOnDeploy` implements `IStatefulDeployOrder`.
- [ ] Run `DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --filter FullyQualifiedName~BatchDeployOrderPolicyTest --no-restore` and verify compilation fails because the policy and interface do not exist.

### Task 2: Implement state-aware grouping

**Files:**
- Modify: `engine/OpenRA.Mods.Common/TraitsInterfaces.cs`
- Modify: `engine/OpenRA.Mods.Common/Traits/Conditions/GrantConditionOnDeploy.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/CommandBarLogic.cs`

- [ ] Add `IStatefulDeployOrder : IIssueDeployOrder` with a read-only `DeployState` property.
- [ ] Make `GrantConditionOnDeploy` implement the interface using its existing property.
- [ ] Add `BatchDeployOrderPolicy.GetTargetState(IEnumerable<DeployState>)`: return `Undeployed` when any stable member is undeployed, `Deployed` when all stable members are deployed, and null when no stable member exists.
- [ ] In `PerformDeployOrderOnSelection`, filter by `CanIssueDeployOrder`, group stateful entries by actor info name and trait runtime type, calculate each target, then iterate original candidate order. Include ordinary deploy traits unchanged; include stateful traits only when stable and equal to their group's target state.
- [ ] Run the focused test and expect all cases to pass.

### Task 3: Verify and commit

**Files:**
- Verify all four implementation/test files above.

- [ ] Build `engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj` with `--no-restore`.
- [ ] Run the focused test and `git diff --check`.
- [ ] Commit only the interface, policy/command logic, trait, and test with message `Fix state-aware group deploy commands`.
