# V3 Terminal Dive and Layered Impact Effects Implementation Plan

> **Execution mode:** Continue directly through all tasks using strict RED → GREEN tests, then independent specification and quality reviews. Preserve the existing dirty worktree. Do not stage, commit, stash, reset, delete, install to a device, or broaden the public-content allowlist.

**Goal:** Replace the V3 rocket's single lazy parabola with the confirmed launch/cruise/acquire/terminal-dive behavior, and give V3/Kirov land and water impacts reliable four-layer visual feedback without changing damage balance or bundling retail game assets.

**Architecture:** Add an opt-in deterministic terminal-dive policy behind `BallisticMissileInfo.TerminalDive`, integrate it into the existing activity without changing legacy missile paths, and configure only V3 to use it. Generate project-original impact overlays from deterministic geometry, combine them with player-imported main explosion references through separate visual warheads, and enforce exact public-package inventory boundaries.

**Technology:** C#/.NET 6, OpenRA activities/traits/MiniYaml, Python 3 + Pillow, NUnit, unittest, MSBuild iOS bundle items.

---

## Existing-work safeguards

- `mods/ra2/rules/soviet-vehicles.yaml`, `mods/ra2/weapons/explosions.yaml`, `mods/ra2/weapons/misc.yaml`, and `ios/OpenRA.iOS/OpenRA.iOS.csproj` already contain unrelated staged or unstaged edits. Patch only the exact V3/Kirov/public-resource nodes.
- Preserve the existing V3 voxel-rendering changes and nuclear haptic changes.
- `engine/**` is hidden by the repository's root ignore rule. New engine tests may not appear in ordinary `git status`; report this explicitly at handoff, but do not force-add without authorization.
- Baseline recorded on 2026-08-25: `V3RenderingRulesTest` 2/2 passed; `packaging.tests.test_public_bundle_audit` 12/12 passed.

## Task 1: Terminal-dive policy and configuration validation

**Files**

- Modify: `OpenRA.Mods.RA2/Traits/BallisticMissile.cs`
- Modify: `OpenRA.Mods.RA2/Activities/BallisticMissileFly.cs`
- Create: `engine/OpenRA.Test/V3TerminalDivePolicyTest.cs`

**RED**

1. Add tests for opt-in defaults (`TerminalDive=false`, terminal speed/cap zero), effective terminal-turn fallback, invalid terminal configuration, exact 25-tick prepare interpolation, shortest-angle acquire turns, capped hit acceleration, `min(speed, remainingDistance)`, exact final point, no overshoot, and deterministic repeatability.
2. Add parameterized 5c/10c/18c trajectory simulations using the real V3 start offset and the confirmed parameters. Assert forward-only states, <=160 flight ticks, acquire turn <=48, hit displacement <=350, exact final target, and byte-for-byte-equivalent trace on repeat.
3. Run:

   `env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~V3TerminalDivePolicyTest -v minimal`

   Confirm failures are specifically caused by the missing terminal-dive fields/policy.

**GREEN**

4. Add `TerminalDive`, `TerminalTurnSpeed`, and `MaxHitSpeed` to `BallisticMissileInfo` with legacy-safe defaults.
5. Add ruleset validation for terminal mode: `PrepareTick > 0`, effective terminal turn speed > 0, `MaxHitSpeed > 0`.
6. Implement a deterministic integer-only terminal policy with states Prepare, Launch, Cruise, Acquire, Hit, Done. Expose only the minimal internal seam needed by NUnit.
7. Integrate it behind `info.TerminalDive`. Keep the existing legacy/LazyCurve code path structurally unchanged. Before Kill, set the exact target position so the airborne condition is cleared.
8. Re-run the focused test, then the existing V3 renderer test.

**Review**

9. Independent spec review: backward compatibility, exact confirmed state ordering, no hidden float/random use, no target tracking.
10. Independent quality review: integer overflow, angle wrapping, zero-distance handling, activity lifecycle, no unintended bmisl/dmisl changes.

## Task 2: V3 actor wiring and activity-level regression coverage

**Files**

- Modify: `mods/ra2/rules/soviet-vehicles.yaml`
- Create or extend: `engine/OpenRA.Test/V3TerminalDiveActivityTest.cs`
- Modify only if required by the real test fixture: `engine/OpenRA.Test/OpenRA.Test.csproj`

**RED**

1. Add a resolved-rule test asserting only `v3rocket` enables terminal mode with CreateAngle 64, PrepareTick 25, LaunchAngle 160, Speed 250, cruise altitude 3c, hit range 4c, TurnSpeed 25, TerminalTurnSpeed 48, HitAcceleration 20, MaxHitSpeed 350, and no LazyCurve.
2. Add a lightweight real activity/world fixture covering frozen launch target, exact landing before Kill, ordinary/elite weapon selection after airborne clears, removal before timeout, and legacy bmisl/dmisl traces remaining unchanged.
3. Run the focused activity/rule filter and observe the expected failure before YAML wiring.

**GREEN**

4. Patch only the existing `BallisticMissile` block under `v3rocket`; preserve all renderer/AOT/user changes.
5. Re-run policy, activity, and renderer tests together.

**Review**

6. Independent spec review of resolved values and actor scope.
7. Independent quality review of test realism and legacy trajectory isolation.

## Task 3: Deterministic project-original impact overlays

**Files**

- Create: `packaging/generate_projectile_impact_assets.py`
- Create: `packaging/tests/test_projectile_impact_assets.py`
- Create: `mods/ra2/bits/animations/nc-impact-core.png`
- Create: `mods/ra2/bits/animations/nc-impact-ring.png`
- Create: `mods/ra2/bits/animations/nc-impact-debris.png`
- Modify: `mods/ra2/sequences/misc.yaml`
- Create: `docs/legal/projectile-impact-assets-provenance.md`

**RED**

1. Add Python tests that require a no-input deterministic generator, exactly the declared output files, RGBA/POT sheets, embedded `FrameSize`/`FrameAmount`, transparent gutters, non-empty frames, bounded dimensions/frame counts, and isolated double-generation byte identity.
2. AST/source tests must reject external raster reads, subprocess download/extraction, text/logo rendering, or dependence on retail palettes/assets.
3. Add sequence-contract tests for `nc_core_small/large/tesla`, `nc_ring_small/large/tesla`, and `nc_debris_small/large` referencing only generated images.
4. Run the new unittest and confirm missing generator/assets fail.

**GREEN**

5. Implement the Pillow generator using fixed project colors, mathematical circles, seeded/local deterministic particles, and embedded PNG metadata.
6. Generate the three sheets, add sequence definitions, and document clean-room provenance.
7. Inspect every generated sheet visually and rerun the asset suite twice, including an isolated temporary-directory generation comparison.

**Review**

8. Independent spec review of layer/state coverage and output constraints.
9. Independent quality/license review of provenance, deterministic rendering, frame geometry, and texture cost.

## Task 4: V3/Kirov layered visual warheads and Dred isolation

**Files**

- Modify: `mods/ra2/weapons/explosions.yaml`
- Modify: `mods/ra2/weapons/misc.yaml`
- Create: `engine/OpenRA.Test/V3KirovImpactRulesTest.cs`

**RED**

1. Capture the resolved pre-change balance values for V3Weapon, V3WeaponE, DredWeapon, DredWeaponE, BlimpBomb, and BlimpBombE: damage, spread, falloff, versus, damage types, rate-affecting weapon data.
2. Add rule tests requiring the seven named visual/shake warheads, one explosion sequence per CreateEffect, `ImpactActors:false`, ground-level forcing, exactly one land and water sound, water layers in normal and elite variants, and no more than four sprite layers per terrain.
3. Add explicit tests that Dred resolves to its prior single-layer visual rather than inheriting V3's new layered effects.
4. Run the focused test and observe failures against current single-layer rules.

**GREEN**

5. Extract `^V3FamilyDamage` without changing any resolved balance values.
6. Give V3/V3E the confirmed core/main/ring/plume-or-splash/shake matrix and delays.
7. Give BlimpBomb/BlimpBombE the confirmed Kirov matrix while preserving damage fields and existing unrelated haptic additions.
8. Make Dred/DredE explicitly retain their resolved pre-change single-layer effect.
9. Re-run focused rules tests and `./utility.sh --check-yaml`.

**Review**

10. Independent spec review of terrain split, layer count, timings, sequences, sound count, and balance snapshots.
11. Independent quality review of MiniYaml inheritance/removals, duplicate warheads, fog behavior, and overdraw.

## Task 5: Exact PublicClean boundary and inventory

**Files**

- Modify: `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Modify: `packaging/public-content-manifest.json`
- Modify: `packaging/tests/test_public_bundle_audit.py`
- Create or modify: `packaging/tests/test_projectile_impact_public_bundle.py`

**RED**

1. Add fixture tests requiring PublicClean to include only `mods/ra2/bits/animations/nc-impact-*.png`, rejecting any other `bits` image or retail extension.
2. Require every new generated PNG to have exact size/SHA-256 and `project-original`, `Project original`, `public:true` inventory fields.
3. Require the modified existing YAML inventory entries to update size/SHA while remaining `unknown-or-derivative`, `Unreviewed`, `public:false`.
4. Run the focused public audit tests and confirm the missing allowlist/manifest entries fail.

**GREEN**

5. Add only the exact `nc-impact-*.png` bundle item to the PublicClean MSBuild group.
6. Update inventory entries from actual files. Do not reclassify existing YAML or weaken deny-by-default rules.
7. Run focused public fixture audits. Record the full PublicClean audit's existing mixed-source YAML block as an expected legal boundary, never as a pass.

**Review**

8. Independent spec/security review for whitelist breadth, symlink/path behavior, retail-extension absence, and hash integrity.
9. Independent quality review of XML item conditions and manifest reproducibility.

## Task 6: Integrated verification and handoff

**Files**

- Modify only if a verified regression requires it; otherwise no production edits.
- Write test evidence under the existing project testing-results convention if available.

**Verification**

1. Fresh focused NUnit: terminal policy, activity/rules, renderer, V3/Kirov impact.
2. Fresh Python: projectile assets, public fixture, existing public bundle audit.
3. Regenerate assets and verify zero hash changes.
4. `./utility.sh --check-yaml`.
5. Release builds for `OpenRA.Mods.RA2` and `OpenRA.Mods.Common`.
6. Run the available desktop/simulator smoke harness without installing a device. Exercise V3 normal/elite and Kirov normal/elite on visible land/water targets when the harness supports it; otherwise report the exact unautomated visual boundary rather than claiming it passed.
7. Inspect resulting app/bundle content if produced: generated PNG byte identity, no imported MIX/SHP/AUD/VXL/VQA, correct source/YAML hashes.
8. `git diff --check`, scoped whitespace/EOF checks, and complete changed-file inventory. Preserve staged/unstaged separation; do not stage.
9. Final independent spec and code-quality review over the frozen diff. Fix every Critical/Important finding through a new RED → GREEN cycle.

**Completion report**

- State exactly which tests/builds/audits passed.
- State that no device install was performed.
- State that iPad Mini continuous-bombardment performance remains a deferred real-device check.
- State that the complete PublicClean package remains legally blocked by existing non-public mixed-source YAML until separately reviewed; do not imply this task solved that release-wide boundary.
