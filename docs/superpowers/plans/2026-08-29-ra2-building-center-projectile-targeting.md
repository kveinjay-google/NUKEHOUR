# RA2 Building-Center Projectile Targeting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make RA2 vehicle shells and guided missiles aim at an actor's center so attacks on multi-cell buildings visually land in the middle instead of the near edge.

**Architecture:** Keep the engine's targetable-cell geometry unchanged and opt the two RA2 shared projectile templates into the existing `WeaponInfo.TargetActorCenter` path. Prove the behavior through a repository-level inheritance resolver, then keep the public-content manifest and legal SBOM byte metadata synchronized.

**Tech Stack:** OpenRA MiniYaml configuration, Python 3 `unittest`, OpenRA Utility YAML validation, .NET 8 build.

---

### Task 1: Lock the RA2 targeting contract with a failing regression test

**Files:**
- Create: `packaging/tests/test_ra2_building_center_targeting.py`

- [ ] **Step 1: Create the inheritance-aware regression test**

```python
import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
WEAPON_DIRECTORY = ROOT / "mods" / "ra2" / "weapons"
TOP_LEVEL = re.compile(r"^([^\s:#][^:]*):(?:\s*#.*)?$")


def weapon_fields():
    weapons = {}
    for path in sorted(WEAPON_DIRECTORY.glob("*.yaml")):
        current = None
        for raw_line in path.read_text(encoding="utf-8").splitlines():
            match = TOP_LEVEL.fullmatch(raw_line)
            if match:
                current = match.group(1)
                if current in weapons:
                    raise AssertionError(f"duplicate weapon `{current}` in {path}")
                weapons[current] = {}
                continue

            if current is None or not raw_line.startswith("\t") or raw_line.startswith("\t\t"):
                continue

            key, separator, value = raw_line[1:].partition(":")
            if separator and (key == "TargetActorCenter" or key.startswith("Inherits")):
                weapons[current][key] = value.strip()

    return weapons


def targets_actor_center(weapons, name, active=()):
    if name in active:
        raise AssertionError(f"inheritance cycle: {' -> '.join((*active, name))}")

    fields = weapons[name]
    if "TargetActorCenter" in fields:
        return fields["TargetActorCenter"].lower() == "true"

    parents = [value for key, value in fields.items() if key.startswith("Inherits")]
    return any(targets_actor_center(weapons, parent, (*active, name)) for parent in parents)


class Ra2BuildingCenterTargetingTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.weapons = weapon_fields()

    def assert_targets_center(self, *weapon_names):
        for name in weapon_names:
            with self.subTest(weapon=name):
                self.assertTrue(
                    targets_actor_center(self.weapons, name),
                    f"{name} must resolve TargetActorCenter: true",
                )

    def test_normal_and_elite_vehicle_shells_target_actor_center(self):
        self.assert_targets_center(
            "105mm", "105mmE", "120mm", "120mmE", "120mmx", "120mmxE"
        )

    def test_normal_and_elite_ifv_missiles_target_actor_center(self):
        self.assert_targets_center("HoverMissile", "HoverMissileE")


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```bash
python3 -m unittest packaging.tests.test_ra2_building_center_targeting -v
```

Expected: two failed tests reporting that `105mm` and `HoverMissile` do not resolve `TargetActorCenter: true`. A syntax/import error is not an acceptable RED result.

### Task 2: Enable center targeting at the shared RA2 templates

**Files:**
- Modify: `mods/ra2/weapons/defaults.yaml:53-76`

- [ ] **Step 1: Add the existing engine flag to both shared templates**

```yaml
^LargeBullet:
	TargetActorCenter: true
	Projectile: Bullet
```

```yaml
^Missile:
	TargetActorCenter: true
	ReloadDelay: 60
```

- [ ] **Step 2: Re-run the focused test and verify GREEN**

Run:

```bash
python3 -m unittest packaging.tests.test_ra2_building_center_targeting -v
```

Expected: `Ran 2 tests` and `OK`.

### Task 3: Synchronize the source manifest and legal SBOM

**Files:**
- Modify: `packaging/public-content-manifest.json:2021-2025`
- Modify: `docs/legal/public-asset-sbom.json:2005-2009`

- [ ] **Step 1: Confirm the final file identity**

Run:

```bash
wc -c mods/ra2/weapons/defaults.yaml
shasum -a 256 mods/ra2/weapons/defaults.yaml
```

Expected size and digest:

```text
3324 mods/ra2/weapons/defaults.yaml
a5ff5ca1394be1cb9e84ce470589c46e1714cfd1c3e1ed4bcccea2b5825defee  mods/ra2/weapons/defaults.yaml
```

- [ ] **Step 2: Replace the matching entry in both JSON documents**

Each exact `mods/ra2/weapons/defaults.yaml` object must contain:

```json
{
  "path": "mods/ra2/weapons/defaults.yaml",
  "sha256": "a5ff5ca1394be1cb9e84ce470589c46e1714cfd1c3e1ed4bcccea2b5825defee",
  "size": 3324,
  "category": "unknown-or-derivative",
  "license": "Unreviewed",
  "public": false
}
```

- [ ] **Step 3: Verify both metadata documents agree with the source**

Run:

```bash
python3 packaging/public_brand_delta_audit.py \
  --baseline-report packaging/tests/fixtures/public-clean-iossimulator-arm64-baseline.json \
  --candidate-report packaging/tests/fixtures/public-clean-iossimulator-arm64-baseline.json \
  --source-root . \
  --manifest packaging/public-content-manifest.json \
  --sbom docs/legal/public-asset-sbom.json
```

Expected: exit 0 with no source hash/size or classification mismatch.

### Task 4: Validate, review, and commit the gameplay fix

**Files:**
- Test: `packaging/tests/test_ra2_building_center_targeting.py`
- Test: `packaging/tests/test_public_brand_delta.py`
- Validate: `mods/ra2/weapons/defaults.yaml`
- Build: `OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj`

- [ ] **Step 1: Run focused Python regression tests**

```bash
python3 -m unittest \
  packaging.tests.test_ra2_building_center_targeting \
  packaging.tests.test_public_brand_delta -v
```

Expected: all selected tests pass.

- [ ] **Step 2: Load and validate the complete RA2 MiniYaml ruleset**

```bash
./utility.sh --check-yaml
```

Expected: exit 0. Existing non-fatal unused Fluent-key or bot-name warnings may remain, but there must be no YAML or rules error.

- [ ] **Step 3: Build the RA2 managed assembly**

```bash
/Users/kevin/.dotnet/dotnet build OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj \
  -c Release --no-restore -p:NuGetAudit=false
```

Expected: build succeeds with 0 errors.

- [ ] **Step 4: Check the exact change set**

```bash
git diff --check
git diff -- \
  mods/ra2/weapons/defaults.yaml \
  packaging/tests/test_ra2_building_center_targeting.py \
  packaging/public-content-manifest.json \
  docs/legal/public-asset-sbom.json
```

Expected: no whitespace errors; the gameplay diff contains exactly two center-targeting flags, one regression test, and two matching metadata updates.

- [ ] **Step 5: Commit only the implementation files**

```bash
git add -- \
  mods/ra2/weapons/defaults.yaml \
  packaging/tests/test_ra2_building_center_targeting.py \
  packaging/public-content-manifest.json \
  docs/legal/public-asset-sbom.json
git diff --cached --name-status
git commit -m "Fix RA2 projectiles targeting building edges"
```

Expected staged list: exactly the four paths above. Existing iOS import documents and unrelated untracked files remain unstaged.
