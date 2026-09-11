# Structure Production Queue Limit Design

## Goal

Building and defense production must never queue more than one copy of the same item. The production multiplier, including Shift and the touch `x5` control, must have no effect on these two categories. Infantry, vehicle, aircraft, and ship queues retain their existing batch behavior on macOS, iOS, and Android.

## Design

The RA2 player rules already divide structure production into the `Building` and `Support` queues and expose `ProductionQueueInfo.ItemLimit` as the deterministic per-item admission limit. Set `ItemLimit: 1` on exactly those two queues. The existing shared order resolver applies this limit after receiving any requested batch size, so the first click admits one item and later clicks admit zero until that item leaves the queue. A request for five is likewise reduced to one.

This belongs in synchronized game rules rather than platform UI code. It therefore covers mouse, keyboard, touch, AI-issued production orders, replay/order processing, and multiplayer consistently without duplicating platform-specific conditions.

## Compatibility and versions

`mods/ra2/rules/player.yaml` is a multiplayer compatibility input. All three repositories must receive byte-identical rule changes and then regenerate the same core compatibility digest. Product versions remain independent and each receives one patch/build increment for this release.

## Verification

Tests will prove that `Building` and `Support` have `ItemLimit: 1`, other queues are unchanged, a five-item request admits one structure when absent, and a repeated request admits zero while that structure is queued. The version projection tests must pass in all three repositories and all three generated compatibility identities must match.
