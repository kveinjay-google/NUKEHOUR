# V3 Warhead Orientation Design

## Problem

The V3 terminal-dive position follows the intended launch, cruise, acquire, and hit path, but the rendered voxel remains level while descending. This makes the rocket look as though it is being dropped vertically instead of flying nose-first toward its target.

## Root Cause

`BallisticMissileFly` updates `BallisticMissile.Orientation.Pitch` and `.Yaw` from the actual movement vector. `WithVoxelBody` then passes that orientation through `BodyOrientation.QuantizeOrientation` before rendering. The `v3rocket` rule currently sets `QuantizedFacings: 32`; any non-zero facing count quantizes yaw and intentionally replaces pitch and roll with zero.

The Yuri `bmisl` ballistic missile uses the same `WithVoxelBody` rendering path with `QuantizedFacings: 0`, which preserves the complete continuous voxel orientation. Voxel aircraft use the same continuous-orientation convention.

## Decision

Set only `v3rocket` to `BodyOrientation.QuantizedFacings: 0`.

This keeps the existing trajectory, speed, target acquisition, exact landing, damage, explosion, and model assets unchanged. It also avoids a global renderer change and preserves quantized facings for actors that require them.

## Acceptance Criteria

- The V3 rocket rule explicitly uses continuous voxel orientation.
- A regression test fails if the rule returns to 32 facings.
- The existing terminal-dive tests continue to prove that stored pitch follows the movement vector.
- V3 rules parse successfully and the RA2 project builds without errors.
