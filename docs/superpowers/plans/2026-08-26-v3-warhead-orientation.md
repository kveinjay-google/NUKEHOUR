# V3 Warhead Orientation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the rendered V3 rocket point along its already-correct flight path, including the terminal dive.

**Architecture:** Keep the terminal-dive state machine unchanged. Switch the V3 rocket's `BodyOrientation` from yaw-only quantization to the engine's existing continuous voxel-orientation mode, and lock that integration contract with an executable rule test.

**Tech Stack:** C#, NUnit, OpenRA MiniYAML rules, .NET 8

---

### Task 1: Lock the V3 voxel-orientation rule

**Files:**
- Modify: `engine/OpenRA.Test/V3RenderingRulesTest.cs`
- Modify: `mods/ra2/rules/soviet-vehicles.yaml`
- Modify: `packaging/public-content-manifest.json`

- [ ] **Step 1: Write the failing test**

Add `SpawnedRocketVoxelRotationPreservesFlightPitchAndYaw`, parse the real `v3rocket` `BodyOrientation` rule through `FieldLoader`, and pass captured launch and terminal-dive orientations through the same `QuantizeOrientation` behavior used by `WithVoxelBody`.

```csharp
[TestCase(160, 651)]
[TestCase(922, 649)]
public void SpawnedRocketVoxelRotationPreservesFlightPitchAndYaw(int pitch, int yaw)
{
	var actor = MiniYaml.FromFile(Path.Combine(
		RepositoryRoot(), "mods", "ra2", "rules", "soviet-vehicles.yaml"))
		.Single(node => node.Key == "v3rocket");
	var trait = actor.Value.Nodes.Single(node => node.Key == "BodyOrientation");
	var info = new BodyOrientationInfo();
	FieldLoader.Load(info, trait.Value);
	var flightOrientation = new WRot(WAngle.Zero, new WAngle(pitch), new WAngle(yaw));
	var renderedOrientation = info.QuantizeOrientation(flightOrientation, info.QuantizedFacings);

	Assert.That(renderedOrientation, Is.EqualTo(flightOrientation));
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter 'FullyQualifiedName~V3RenderingRulesTest' -v minimal
```

Expected: the new test fails because the current rule resolves to `QuantizedFacings: 32`.

- [ ] **Step 3: Apply the minimal production fix**

Change only the `v3rocket` `BodyOrientation.QuantizedFacings` value from `32` to `0`.

```yaml
	BodyOrientation:
		QuantizedFacings: 0
```

- [ ] **Step 4: Run the focused test and verify GREEN**

Run the command from Step 2. Expected: every `V3RenderingRulesTest` test passes.

- [ ] **Step 5: Refresh the pinned private-rule metadata**

Update only the existing `mods/ra2/rules/soviet-vehicles.yaml` manifest entry with the file's new byte size and SHA-256. Preserve `unknown-or-derivative`, `Unreviewed`, and `public: false`, then run:

```sh
python3 -B -m unittest packaging.tests.test_projectile_impact_public_bundle -v
```

Expected: all five tests pass; the mixed-source rule remains blocked from PublicClean.

### Task 2: Verify trajectory and rules remain unchanged

**Files:**
- Verify: `engine/OpenRA.Test/V3TerminalDivePolicyTest.cs`
- Verify: `engine/OpenRA.Test/V3TerminalDiveActivityTest.cs`
- Verify: `engine/OpenRA.Test/DreadnoughtMissileRulesTest.cs`

- [ ] **Step 1: Run the adjacent regression tests**

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter 'FullyQualifiedName~V3RenderingRulesTest|FullyQualifiedName~V3TerminalDivePolicyTest|FullyQualifiedName~V3TerminalDiveActivityTest|FullyQualifiedName~DreadnoughtMissileRulesTest' -v minimal
```

Expected: all selected tests pass, including the movement-vector pitch assertions.

- [ ] **Step 2: Validate rules and build RA2**

```sh
./utility.sh --check-yaml
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore --nologo
git diff --check
```

Expected: YAML check and build exit zero; diff check produces no output.

- [ ] **Step 3: Request independent review**

Review the rule boundary, the rendering data flow, test authenticity, and the final diff. Fix every Critical or Important finding before delivery.

- [ ] **Step 4: Commit the verified fix**

Stage only the two documentation files, `V3RenderingRulesTest.cs`, `soviet-vehicles.yaml`, and the refreshed manifest, show the complete staged list, and commit with a narrowly scoped message.
