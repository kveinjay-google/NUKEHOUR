# RA2 Building-Center Projectile Targeting Design

## Problem

RA2 buildings expose a targetable position for every occupied cell through their multi-cell `HitShape`. The default armament path chooses the targetable position nearest the muzzle. As a result, vehicle shells and guided missiles consistently converge on a building's near edge instead of the actor center used by the original game presentation.

## Decision

Enable the engine's existing `WeaponInfo.TargetActorCenter` behavior on the RA2 shared `^LargeBullet` and `^Missile` weapon templates.

This is the narrowest durable fix because:

- `105mm`, `120mm`, `120mmx`, and their elite variants inherit `^LargeBullet`.
- `HoverMissile` and `HoverMissileE` inherit `^Missile`.
- The engine already applies the flag consistently when the armament creates the passive target and when a guided missile refreshes its target.
- Multi-cell targetable positions remain available for range, pathing, hit-shape, and obstruction logic.

The setting also makes these weapons aim at the center of other actors. RA2 vehicles and infantry normally expose their center as their only relevant target point, so this does not introduce a practical gameplay difference for those targets.

## Out of Scope

- Damage, armor modifiers, reload timing, range, burst behavior, and projectile inaccuracy.
- Building hit shapes and occupied-cell targeting used by range checks.
- Infantry small arms, beams, special powers, and projectile families that do not inherit either selected template.
- Engine-wide targeting behavior for other mods.

## Verification

Add a repository regression test that resolves the relevant RA2 weapon inheritance and proves that representative normal and elite vehicle shells plus IFV missiles inherit `TargetActorCenter: true`. The test must fail before the configuration change and pass afterward.

Then run:

- the focused targeting regression;
- the full packaging Python test suite relevant to source/manifest consistency;
- the RA2 project build or rules load check available in this checkout;
- `git diff --check`.

Because `mods/ra2/weapons/defaults.yaml` is recorded in the public-content manifest and legal SBOM, update their exact size and SHA-256 entries without changing the existing `unknown-or-derivative`, `Unreviewed`, or `public: false` classification.
