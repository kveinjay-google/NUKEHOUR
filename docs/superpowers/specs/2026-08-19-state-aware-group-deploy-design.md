# State-Aware Group Deploy Design

## Goal

Make the Deploy command predictable when several actors of the same type are selected. A mixed group must converge on the deployed state instead of blindly toggling each actor, while a fully deployed group must undeploy together.

## Required Behavior

Actors are evaluated in groups that share the same actor type.

- If a group contains at least one stably undeployed actor, issue Deploy only to the stably undeployed actors. Already deployed actors remain deployed.
- If every stable actor in the group is deployed, issue Undeploy to the deployed actors.
- Actors currently deploying or undeploying do not receive another toggle order.
- Each actor-type group is evaluated independently when a selection contains multiple types.
- Single-actor behavior remains unchanged.

Example: selecting three standing GIs and three deployed GIs, then pressing Deploy, deploys only the three standing GIs. Pressing Deploy again after all six have finished deploying undeploys all six.

## Scope

The state-aware behavior is opt-in for traits that represent a reversible, stable deployed/undeployed state. `GrantConditionOnDeploy` is the first implementation and covers the GI and other units that use the same two-state deployment model.

Other implementations of `IIssueDeployOrder` keep their current behavior. This includes one-shot or semantically different actions such as MCV transformation, cargo unloading, mine placement, aircraft return/rearm, portable chronoshift, and charged abilities.

## Architecture

Add a small read-only interface beside `IIssueDeployOrder` that exposes a batch-deploy state:

- Undeployed
- Deploying
- Deployed
- Undeploying

`GrantConditionOnDeploy` implements this interface by exposing its existing synchronized `DeployState`; it does not add a second source of truth.

`CommandBarLogic` continues to collect all selected `IIssueDeployOrder` traits. When Deploy is pressed it:

1. Separates state-aware entries from ordinary deploy actions.
2. Groups state-aware entries by actor type and deploy trait identity.
3. Ignores entries in transitional states.
4. For each group, chooses Deploy when any stable member is undeployed; otherwise chooses Undeploy.
5. Issues orders only to stable members whose current state differs from the chosen target.
6. Issues all ordinary deploy actions using the existing path.

Grouping by actor type and trait identity prevents multiple deploy traits on one actor or unrelated actor types from influencing each other.

## Input and Order Semantics

The behavior applies equally to the keyboard Deploy command and the iOS virtual Deploy button because both already use `CommandBarLogic.PerformDeployOrderOnSelection`.

Queued orders retain the existing queue flag. Group state is evaluated when the command is issued. The underlying activity still validates the actor state when it executes, preserving deterministic order processing and replay compatibility.

## Edge Cases

- A group containing only transitional actors produces no order.
- A disabled or paused deployment trait is excluded by its existing `CanIssueDeployOrder` check.
- Invalid deployment terrain remains protected by the trait's existing validation.
- Actors that die, change owner, or leave the world between selection and order execution remain protected by the existing order pipeline.
- A heterogeneous selection evaluates each state-aware actor type independently and leaves non-state-aware deploy actions unchanged.

## Tests

Automated tests will verify:

1. Three undeployed plus three deployed actors issue orders only to the three undeployed actors.
2. A fully deployed group issues orders to the whole group.
3. Different actor types are evaluated independently.
4. Transitional actors receive no repeated order.
5. Ordinary `IIssueDeployOrder` implementations still receive their existing deploy action.
6. Single-actor deploy and undeploy behavior is unchanged.

The command-selection policy should be extracted into a deterministic helper so these cases can be tested without constructing the full UI.

## Non-Goals

- Changing deployment animations, sounds, terrain restrictions, or unit rules.
- Changing AI-issued deploy orders.
- Converting one-shot deploy actions into stateful actions.
- Adding a new button or changing the command bar layout.
