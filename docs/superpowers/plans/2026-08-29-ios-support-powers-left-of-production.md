# iPhone Support Powers Beside Production Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep every player support-power button (paradrop, nuclear missile, Iron Curtain, Weather Control, Chronosphere, Genetic Mutator, Psychic Dominator, and future entries in the same palette) fully visible and tappable immediately outside the left edge of the iPhone production sidebar.

**Architecture:** Preserve the existing shared `SupportPowersWidget` cell-provider path. On compact iPhones, treat the visible production sidebar as a complete obstacle and use its expanded left edge as a fixed right boundary; fill each column bottom-to-top and add overflow columns only to the left while avoiding the command bar, joystick, actions, and Safe Area. iPad, desktop, ordering, hotkeys, tooltip, charging, and targeting semantics remain unchanged.

**Tech Stack:** C# / OpenRA widget policies, NUnit, Python `unittest`, YAML validation, .NET 8 iOS simulator build, strict official-engine patch replay.

---

## Scope and file boundary

Engine feature allowlist:

- `engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IosSupportPowerLayoutPolicy.cs`
- `engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/SupportPowerBinLogic.cs`
- `engine/OpenRA.Test/IosSupportPowerLayoutPolicyTest.cs`

Patch-delivery allowlist:

- `engine-patches/0019-ios-support-powers-beside-production.patch`
- `engine-patches/README.md`
- `packaging/tests/test_verify_engine_patch_delivery.py`

Do not modify RA2 support-power rules or `mods/ra2/chrome/ingame-player.yaml`; all player support powers already share the same palette and must move together.

## Task 1: Establish the baseline and add failing geometry tests

**Files:**

- Modify: `engine/OpenRA.Test/IosSupportPowerLayoutPolicyTest.cs`

- [ ] Record the clean feature allowlist and confirm no pre-existing changes overlap it:

```sh
git status --short -- \
	engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IosSupportPowerLayoutPolicy.cs \
	engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/SupportPowerBinLogic.cs \
	engine/OpenRA.Test/IosSupportPowerLayoutPolicyTest.cs
```

- [ ] Run the existing baseline suite in single-process mode:

```sh
/Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj \
	--no-restore -p:NuGetAudit=false -m:1 \
	--filter "FullyQualifiedName~IosSupportPowerLayoutPolicyTest"
```

Expected: all existing tests pass.

- [ ] Add `CompactPhoneAnchorsEverySupportPowerOutsideTheProductionSidebarLeftEdge`. Use the real failing scaled snapshot (`EffectiveSize=1558×720`, `NativePointSize=844×390`), six icons, and the currently reported lower production rectangle `Rectangle(1324, 275, 234, 250)`. Build a separate full-sidebar assertion rectangle from `X=1324` through the safe-area bottom. Assert:

  - all six cells exist;
  - `Cells.Max(cell => cell.Right) == production.Left - layout.Margin`;
  - no cell intersects the margin-expanded full-sidebar assertion rectangle;
  - all cells remain inside `PlacementBounds` and avoid other visible obstacles.

- [ ] Add a compact matrix over `844×390`, `932×430`, and effective `1558×720` / native `844×390`, with counts `0, 1, 2, 3, 6, 7, 12`. Model the production sidebar as the rightmost `234` logical pixels and assert every non-empty layout is anchored outside its left edge.

- [ ] Add `CompactPhoneFillsEachSidebarColumnBottomToTopThenExpandsOnlyLeft` with the `844×390` snapshot, a full-height `234`-pixel sidebar obstacle, and twelve icons. Assert the first column shares one X coordinate with descending Y values, the next column has a smaller X, and no later column moves right again. The current row-major implementation must fail this test.

- [ ] Preserve and explicitly assert the fallback behavior: when production is hidden or missing, `CompactPhoneStartsAtLowerRightAndFlowsRightToLeftThenUp` remains byte-for-byte deterministic. Existing iPad and desktop tests remain unchanged.

- [ ] Run the new anchor test and confirm it fails against the current implementation because later icons return to the far-right sidebar area:

```sh
/Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj \
	--no-restore -p:NuGetAudit=false -m:1 \
	--filter "FullyQualifiedName~CompactPhoneAnchorsEverySupportPowerOutsideTheProductionSidebarLeftEdge"
```

Expected RED: the maximum cell right edge exceeds `production.Left - margin`; on the scaled fixture the current implementation places cells at the far-right because their row is below the declared lower obstacle.

## Task 2: Implement the compact iPhone sidebar-left layout

**Files:**

- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IosSupportPowerLayoutPolicy.cs`

- [ ] Add a dedicated compact helper with this responsibility:

```csharp
static IReadOnlyList<Rectangle> PlaceCompactBesideProduction(
	int count,
	Rectangle placement,
	Size cellSize,
	int gap,
	Rectangle expandedProduction,
	IReadOnlyList<Rectangle> expandedObstacles)
```

- [ ] In `Create`, use `PlaceCompactBesideProduction` only when `snapshot.IsCompactPhone` and `obstacles.Production.Visible`; otherwise retain the existing `PlaceCompact` fallback. Tablet continues through `PlaceTablet`.

- [ ] Clamp the fixed right boundary to `Math.Min(placement.Right, expandedProduction.Left)`. Iterate X from `rightBoundary - cellSize.Width` toward `placement.Left`, and inside each column iterate Y from `placement.Bottom - cellSize.Height` toward `placement.Top`. Accept a candidate only when it is contained in the placement rectangle and intersects no expanded obstacle.

- [ ] Do not reset X to the screen right edge on higher rows. Return an empty partial override when all icons cannot fit, preserving the existing all-or-nothing `FitsAll` behavior.

- [ ] Keep `IosSupportPowerLayoutMode.CompactBottomRight` for compatibility; the semantic change is constrained by the visible production obstacle, not a public enum/API migration.

- [ ] Run the focused policy suite (runtime source-wiring assertions are added only in Task 3):

```sh
/Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj \
	--no-restore -p:NuGetAudit=false -m:1 \
	--filter "FullyQualifiedName~IosSupportPowerLayoutPolicyTest"
```

Expected GREEN: all legacy and new matrix tests pass.

## Task 3: Report the production sidebar's real visible footprint

**Files:**

- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/SupportPowerBinLogic.cs`
- Modify: `engine/OpenRA.Test/IosSupportPowerLayoutPolicyTest.cs`

- [ ] Replace the production call with a required-anchor production obstacle:

```csharp
GetProductionObstacle()
```

- [ ] `GetProductionObstacle()` must resolve `SIDEBAR_PRODUCTION` as the required anchor and `SIDEBAR_BACKGROUND_TOP` as the related root. If the required production widget is missing or hidden, return `default` so compact layout uses the existing fallback. Otherwise call this testable pure helper:

```csharp
public static IosSupportPowerObstacle CombineVisibleObstacle(
	Widget requiredAnchor,
	params Widget[] relatedRoots)
```

- [ ] `CombineVisibleObstacle` must return `default` when the required anchor is null/hidden. Otherwise union the anchor `RenderBounds`, every visible descendant recursively, and every visible related root plus its visible descendants. Skip an entire hidden subtree. Use `Rectangle.Union` only after the first visible rectangle has initialized the accumulator.

- [ ] Keep command bar, joystick, and actions on the existing single-widget `GetObstacle` path.

- [ ] Add mandatory widget-tree behavior tests using `ContainerWidget`, explicit `Bounds`, and `AddChild`: visible nested descendants may extend below their parent, hidden subtrees contribute nothing, two visible roots are unioned exactly, and a missing/hidden required production anchor returns `default` even when the top root is visible. Extend `RuntimeWidgetsConsumeTheExecutablePolicies` to require both sidebar IDs and the `GetProductionObstacle()` call.

- [ ] Run the focused cross-layout regression set:

```sh
/Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj \
	--no-restore -p:NuGetAudit=false -m:1 \
	--filter "FullyQualifiedName~IosSupportPowerLayoutPolicyTest|FullyQualifiedName~IosViewportControlsLayoutTest|FullyQualifiedName~IosViewportActionLayoutTest|FullyQualifiedName~IosProductionBatchToggleTest"
```

Expected GREEN: support-power, command-bar, joystick, action, and production toggle tests pass.

## Task 4: Verify the feature and commit the engine source change

**Files:**

- Verify only the three engine feature files above.

- [ ] Run the complete managed test suite:

```sh
/Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj \
	--no-restore -p:NuGetAudit=false -m:1
```

- [ ] Validate YAML even though no YAML is intentionally changed:

```sh
./utility.sh --check-yaml
```

- [ ] Build the affected engine projects in the sandbox-safe single-process mode:

```sh
/Users/kevin/.dotnet/dotnet build engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj \
	-c Release --no-restore -p:NuGetAudit=false -m:1 /nr:false \
	-p:UseSharedCompilation=false
/Users/kevin/.dotnet/dotnet build engine/OpenRA.Test/OpenRA.Test.csproj \
	-c Release --no-restore -p:NuGetAudit=false -m:1 /nr:false \
	-p:UseSharedCompilation=false
```

- [ ] Build the iOS simulator target with the pinned local SDK and no build servers:

```sh
env DOTNET_ROOT=/Users/kevin/.dotnet \
	PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
	/Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj \
	-f net8.0-ios -p:RuntimeIdentifier=iossimulator-arm64 -c Release \
	--no-restore -p:NuGetAudit=false -m:1 /nr:false \
	-p:UseSharedCompilation=false
```

- [ ] Inspect the exact diff and whitespace:

```sh
git diff --check -- \
	engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IosSupportPowerLayoutPolicy.cs \
	engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/SupportPowerBinLogic.cs \
	engine/OpenRA.Test/IosSupportPowerLayoutPolicyTest.cs
git diff -- \
	engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IosSupportPowerLayoutPolicy.cs \
	engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/SupportPowerBinLogic.cs \
	engine/OpenRA.Test/IosSupportPowerLayoutPolicyTest.cs
```

- [ ] Stage exactly the three engine paths, show the complete staged list, and commit:

```sh
git add \
	engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IosSupportPowerLayoutPolicy.cs \
	engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/SupportPowerBinLogic.cs \
	engine/OpenRA.Test/IosSupportPowerLayoutPolicyTest.cs
git diff --cached --name-only
git commit -m "Move iPhone support powers beside production"
```

Expected staged list: exactly three paths.

## Task 5: Deliver and verify engine patch 0019

**Files:**

- Create: `engine-patches/0019-ios-support-powers-beside-production.patch`
- Modify: `engine-patches/README.md`
- Modify: `packaging/tests/test_verify_engine_patch_delivery.py`

- [ ] Update the delivery contract test to point at `0019-ios-support-powers-beside-production.patch` and the exact three engine paths. Update predecessor selection to `1..18` and the expected safe header count from `672` to `692`. Confirm the test is RED while 0019 is absent.

```sh
python3 -m unittest \
	packaging.tests.test_verify_engine_patch_delivery.CheckedInDeliveryContractTest -v
```

- [ ] Require an official `OpenRA-release-20250330` ZIP with SHA-256 `ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a`. Never generate the patch from the ignored/materialized `engine/` as its own baseline. Set `OPENRA_OFFICIAL_ZIP` to its exact path, then run this complete candidate-generation workflow:

```sh
export LC_ALL=C LANG=C
REPO=$(git rev-parse --show-toplevel)
: "${OPENRA_OFFICIAL_ZIP:?Set OPENRA_OFFICIAL_ZIP to the pinned official archive}"
EXPECTED_ZIP_SHA256=ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a
TARGET=0019-ios-support-powers-beside-production.patch
BASE_DIR=$(mktemp -d /private/tmp/openra-engine-base.XXXXXX)
STAGE=$(mktemp -d /private/tmp/openra-engine-patch-stage.XXXXXX)
ALLOWLIST="$STAGE/allowlist.txt"
CANDIDATE="$STAGE/$TARGET"

[ "$(shasum -a 256 "$OPENRA_OFFICIAL_ZIP" | awk '{print $1}')" = \
	"$EXPECTED_ZIP_SHA256" ] || exit 1
ditto -x -k "$OPENRA_OFFICIAL_ZIP" "$BASE_DIR"
BASE="$BASE_DIR/OpenRA-release-20250330"

KNOWN_0007_DIAGNOSTICS='Hunk #1 succeeded at 254 with fuzz 2 (offset -1 lines).
Hunk #5 succeeded at 352 (offset -1 lines).'
mkdir -p "$STAGE/predecessor-logs"
: > "$STAGE/predecessors-combined.log"
for prefix in 0001 0002 0003 0004 0005 0006 0007 0008 0009 \
	0010 0011 0012 0013 0014 0015 0016 0017 0018; do
	set -- "$REPO"/engine-patches/"$prefix"-*.patch
	[ "$#" -eq 1 ] && [ -f "$1" ] || exit 1
	p=$1
	PATCH_LOG="$STAGE/predecessor-logs/$(basename "$p").log"
	/usr/bin/patch --verbose -p1 --batch -V none -d "$BASE" -i "$p" \
		> "$PATCH_LOG" 2>&1 || { cat "$PATCH_LOG"; exit 1; }
	{
		printf '===== %s =====\n' "$(basename "$p")"
		cat "$PATCH_LOG"
	} >> "$STAGE/predecessors-combined.log"
	DIAGNOSTICS=$(grep -Ei 'Hunk .*(offset|fuzz)' "$PATCH_LOG" || true)
	if [ "$(basename "$p")" = '0007-language-restart-and-system-default.patch' ]; then
		[ "$DIAGNOSTICS" = "$KNOWN_0007_DIAGNOSTICS" ] || exit 1
	elif [ -n "$DIAGNOSTICS" ]; then
		cat "$PATCH_LOG"
		exit 1
	fi
done
if find "$BASE" -type f \( -name '*.rej' -o -name '*.orig' \) | grep -q .; then
	exit 1
fi

printf '%s\n' \
	OpenRA.Mods.Common/Widgets/Logic/Ingame/IosSupportPowerLayoutPolicy.cs \
	OpenRA.Mods.Common/Widgets/Logic/Ingame/SupportPowerBinLogic.cs \
	OpenRA.Test/IosSupportPowerLayoutPolicyTest.cs \
	> "$ALLOWLIST"
LC_ALL=C sort -c -u "$ALLOWLIST"

mkdir -p "$STAGE/a" "$STAGE/b"
while IFS= read -r f; do
	mkdir -p "$STAGE/a/$(dirname "$f")" "$STAGE/b/$(dirname "$f")"
	[ ! -f "$BASE/$f" ] || cp "$BASE/$f" "$STAGE/a/$f"
	[ ! -f "$REPO/engine/$f" ] || cp "$REPO/engine/$f" "$STAGE/b/$f"
done < "$ALLOWLIST"

diff_rc=0
(cd "$STAGE" && diff -ruN a b) > "$CANDIDATE" || diff_rc=$?
[ "$diff_rc" -eq 1 ] || exit 1

python3 "$REPO/packaging/verify_engine_patch_delivery.py" \
	--repository "$REPO" \
	--official-zip "$OPENRA_OFFICIAL_ZIP" \
	--expected-zip-sha256 "$EXPECTED_ZIP_SHA256" \
	--predecessor-start 1 \
	--predecessor-end 18 \
	--allowlist-file "$ALLOWLIST" \
	--candidate "$CANDIDATE" \
	--workspace "$REPO/engine" \
	--output-dir "$STAGE/verification"
```

Expected: generation replays exactly patches `0001..0018`, permits only the two pinned 0007 diagnostics, rejects every other offset/fuzz and `.rej`/`.orig`, and produces exactly three paired sections. Verification then passes forward strict apply, byte identity, duplicate rejection, reverse dry-run, full tracked-engine identity, and atomic evidence.

- [ ] Copy the verified candidate to `engine-patches/0019-ios-support-powers-beside-production.patch`. Update the README series row and regeneration example to target 0019, replay predecessors `0001..0018`, list the exact three files, and describe three paired sections.

- [ ] Run delivery and full packaging tests:

```sh
python3 -m unittest \
	packaging.tests.test_verify_engine_patch_delivery.CheckedInDeliveryContractTest -v
python3 -m unittest discover -s packaging/tests -p 'test_*.py'
git diff --check
```

- [ ] Stage exactly the three patch-delivery paths, show the complete list, and commit:

```sh
git add \
	engine-patches/0019-ios-support-powers-beside-production.patch \
	engine-patches/README.md \
	packaging/tests/test_verify_engine_patch_delivery.py
git diff --cached --name-only
git commit -m "Deliver iPhone support power layout engine patch"
```

Expected staged list: exactly three paths. Do not stage the pre-existing retail-import design/plan changes or unrelated untracked artifacts.

## Final acceptance

- [ ] Re-run the focused support-power suite and the checked-in delivery contract from the committed tree.
- [ ] Confirm `git status --short` contains no changes to the six task paths and preserves all unrelated pre-existing workspace changes.
- [ ] Confirm the final diff/commits change no RA2 support-power ordering, hotkeys, tooltip, targeting, charge, or desktop/iPad behavior.
- [ ] Report exact test/build results, patch verification evidence location, the two commit IDs, and any external blocker (for example, a missing pinned official ZIP) without claiming an unrun gate passed.
