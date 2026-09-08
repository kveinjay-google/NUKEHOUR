# iOS Three-Action Quarter Ring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the iOS joystick to the Safe-Area lower-left corner, expose only Deploy/Same Type/Force Attack on a three-button quarter ring, and keep Stop/Base permanently accessible in compact and expanded touch command bars.

**Architecture:** Keep joystick/action geometry as a pure point-aware layout policy that receives a real command-bar rectangle. Keep command availability, required touch slots, preference migration, and base selection behavior in the existing RA2 command-bar layer. Preserve the five-entry faction atlas as a compatibility asset contract while changing the active runtime widget contract to three entries.

**Tech Stack:** C#/.NET 8, OpenRA widgets and MiniYaml, NUnit, Python 3 unittest, iOS Safe Area metrics, deterministic engine patch series, MSBuild ios-arm64 signing, CoreDevice `devicectl`.

---

## File Structure

- Modify `engine/OpenRA.Mods.Common/Widgets/IosViewportControlsLayout.cs` — pure three-action geometry and per-entity command-bar avoidance.
- Modify `engine/OpenRA.Mods.Common/Widgets/VirtualViewportJoystickWidget.cs` — circular initial hit testing matching the visible joystick base.
- Modify `engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IosViewportActionsLogic.cs` — three live widgets and real obstacle rectangle wiring.
- Modify `engine/OpenRA.Test/IosViewportControlsLayoutTest.cs` — device/size/obstacle geometry matrix.
- Modify `engine/OpenRA.Test/IosViewportActionLayoutTest.cs` — exact three-action runtime/YAML contract.
- Modify `mods/ra2/chrome/ingame.yaml` — remove the floating Stop and Return Base controls.
- Modify `OpenRA.Mods.RA2/Widgets/CommandBarPreferences.cs` — touch-required slots and preference version 4.
- Modify `OpenRA.Mods.RA2/Widgets/CustomCommandBarWidget.cs` — compact order, edit protection, physical sizing, labels, Safe-Area placement.
- Modify `OpenRA.Mods.RA2/Widgets/Logic/SelectionCommandBarLogic.cs` — primary-first stateless base cycling.
- Modify `engine/OpenRA.Test/IosProductionBatchToggleTest.cs` — availability, migration, required-slot, sizing and desktop isolation tests.
- Modify `packaging/audit_ios_touch_runtime_bundle.py` — separate five-entry atlas and three-entry active widget contracts.
- Modify `packaging/tests/test_ios_touch_runtime_bundle.py` — tamper tests for both contracts.
- Modify `packaging/tests/test_ios_touch_skin_assets.py` — keep all five atlas glyphs while expecting only three live widgets.
- Modify `packaging/public-content-manifest.json` — refresh only changed tracked content entries.
- Create `engine-patches/0013-post-0012-overlay-sync.patch` — replay already-committed engine overlay drift.
- Create `engine-patches/0014-ios-three-action-quarter-ring.patch` — replay A1 engine source/test changes.
- Create `engine-patches/0015-ios-lobby-scroll-layout-polish.patch` — replay the disjoint lobby/map-preview/scroll engine complement.
- Modify `engine-patches/README.md` — document 0013 through 0015.

## Task 1: Make the Three-Action Geometry Executable

**Files:**
- Modify: `engine/OpenRA.Test/IosViewportControlsLayoutTest.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/IosViewportControlsLayout.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/VirtualViewportJoystickWidget.cs`

- [ ] **Step 1: Replace the five-action geometry expectations with a real device matrix**

Add snapshots with the exact renderer/native pairs:

```csharp
static readonly (Size Renderer, Size Native, IosSafeAreaInsets Insets)[] Devices =
{
	(new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(47, 0, 47, 21)),
	(new Size(1560, 720), new Size(932, 430), new IosSafeAreaInsets(59, 0, 59, 21)),
	// Rotation/safe-area directionality: swap unequal left/right insets.
	(new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(55, 0, 39, 21)),
	(new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(39, 0, 55, 21)),
	(new Size(1133, 744), new Size(1133, 744), new IosSafeAreaInsets(0, 0, 0, 20)),
	(new Size(1180, 820), new Size(1180, 820), new IosSafeAreaInsets(0, 0, 0, 20)),
	(new Size(1366, 1024), new Size(1366, 1024), new IosSafeAreaInsets(0, 0, 0, 20)),
};
```

For every device, joystick size 112/128/144, and command-bar state hidden/compact/expanded, assert three action bounds, angles `-90/-45/0`, ring radius `LogicalPoints(size / 2.0 + 48)`, exact 12pt left/bottom anchor when the obstacle is empty, containment inside `SafeBounds` inset by 12pt, and at least 8pt separation measured from each action square to the joystick circle. Add an executable circular hit-test contract for `VirtualViewportJoystickWidget`: points inside the rendered circle are accepted, square-corner points are rejected before focus, and active drag remains captured. When a real intersecting obstacle requires an upward shift, clamp the shift so every entity remains inside that inset Safe Area; fail the test if the supported matrix cannot fit rather than silently shrinking below the approved hit sizes.

- [ ] **Step 2: Add a failing per-entity obstacle test**

Use an obstacle rectangle found from the generated layout that intersects the overall joystick/action Union only through empty quarter-ring space and assert zero shift. First prove the candidate really exercises the regression: the overall Union intersects it while every individual joystick/action rectangle does not. Then use rectangles that intersect the joystick or one action and assert the exact minimal upward shift:

```csharp
var entities = new[] { withoutObstacle.JoystickBounds }.Concat(withoutObstacle.ActionBounds);
var overall = entities.Aggregate(Rectangle.Union);
var emptySpaceObstacle = FindQuarterRingGap(overall, entities, snapshot.LogicalPoints(8));
Assert.That(overall.IntersectsWith(emptySpaceObstacle), Is.True);
Assert.That(entities.All(e => !e.IntersectsWith(emptySpaceObstacle)), Is.True);
var unchanged = IosViewportControlsLayout.Create(snapshot, 128, emptySpaceObstacle);
Assert.That(unchanged.JoystickBounds, Is.EqualTo(withoutObstacle.JoystickBounds));

var expandedBar = Expand(actionAtThreeOClock, snapshot.LogicalPoints(8));
var shifted = IosViewportControlsLayout.Create(snapshot, 128, expandedBar);
Assert.That(shifted.JoystickBounds.Y,
	Is.EqualTo(withoutObstacle.JoystickBounds.Y - expectedMinimalShift));
```

- [ ] **Step 3: Run the focused test and capture RED**

Run:

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~IosViewportControlsLayoutTest' -v minimal
```

Expected: compilation or assertions fail because `Create` still accepts `bottomObstacleTop`, returns five actions, and uses the old angles/radius.

- [ ] **Step 4: Implement the minimum pure geometry**

Replace the angle and radius policy:

```csharp
static readonly double[] ActionAngles = { -90, -45, 0 };

var ringRadius = snapshot.LogicalPoints(normalized / 2.0 + 48);
```

Change the signature to:

```csharp
public static IosViewportControlsLayout Create(
	IosScreenSnapshot snapshot, int requestedJoystickPoints, Rectangle bottomObstacle)
```

Anchor the joystick before obstacle handling:

```csharp
var centerX = safe.Left + margin + joystickBeforeCenter;
var centerY = safe.Bottom - margin - joystickAfterCenter;
```

Construct the joystick and three action rectangles, expand a nonempty obstacle by `LogicalPoints(8)`, inspect the joystick and each action separately, and compute:

```csharp
var shift = entities
	.Where(entity => entity.IntersectsWith(expandedObstacle))
	.Select(entity => entity.Bottom - expandedObstacle.Top)
	.DefaultIfEmpty(0)
	.Max();
```

Translate every entity upward by `shift`; do not test `Rectangle.Union(entities)` for collision. Validate the translated entities against the 12pt-inset Safe Area and reject/clamp any impossible shift before returning a layout.

- [ ] **Step 5: Run the focused geometry suite and verify GREEN**

Run the Step 3 command. Expected: all `IosViewportControlsLayoutTest` cases pass with a nonzero test count.

## Task 2: Wire Only Three Floating Actions

**Files:**
- Modify: `engine/OpenRA.Test/IosViewportActionLayoutTest.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IosViewportActionsLogic.cs`
- Modify: `mods/ra2/chrome/ingame.yaml`

- [ ] **Step 1: Write the failing three-widget source and runtime contract**

Change the tests to require exactly these YAML buttons and no floating STOP/RETURN_BASE:

```csharp
var active = new[] { "DEPLOY", "SELECT_TYPE", "FORCE_ATTACK" };
foreach (var id in active)
	StringAssert.Contains($"Button@{id}:", yaml);
StringAssert.DoesNotContain("Button@STOP:", viewportActionsBlock);
StringAssert.DoesNotContain("Button@RETURN_BASE:", viewportActionsBlock);
```

Reflect the `actionButtons`/`actionLabels` arrays and verify their runtime length is three. Require the logic to cache a `Rectangle lastBottomObstacleBounds` and to call the new rectangle-based `Create` overload.

- [ ] **Step 2: Run focused tests and capture RED**

Run:

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~IosViewportActionLayoutTest|FullyQualifiedName~IosViewportControlsLayoutTest' \
  -v minimal
```

Expected: old five-widget/YAML assertions fail.

- [ ] **Step 3: Remove the two floating YAML controls**

Delete the complete `Button@STOP` and `Button@RETURN_BASE` blocks from `Container@IOS_VIEWPORT_ACTIONS`. Preserve the Deploy, Select Type and Force Attack blocks without changing their icon or label keys.

- [ ] **Step 4: Rewire the runtime action array and obstacle signature**

Use:

```csharp
actionButtons = new[]
{
	widget.Get<ButtonWidget>("DEPLOY"),
	widget.Get<ButtonWidget>("SELECT_TYPE"),
	widget.Get<ButtonWidget>("FORCE_ATTACK")
};
```

Delete `HasOwnedBuilding`, `ReturnToBase`, and the RETURN_BASE constructor binding. Replace `BottomObstacleTop` with:

```csharp
static Rectangle BottomObstacleBounds()
{
	var commandBar = Ui.Root.GetOrNull("COMMAND_BAR_BACKGROUND");
	return commandBar != null && commandBar.IsVisible()
		? commandBar.RenderBounds
		: Rectangle.Empty;
}
```

Cache the full rectangle in the refresh signature and pass it into `IosViewportControlsLayout.Create`.

- [ ] **Step 5: Verify the C# and YAML gates**

Run the Step 2 command, then:

```sh
./utility.sh --check-yaml
```

Expected: focused tests pass; YAML check exits 0 with only known baseline warnings.

## Task 3: Make Stop and Base Required Touch Quickbar Commands

**Files:**
- Modify: `engine/OpenRA.Test/IosProductionBatchToggleTest.cs`
- Modify: `OpenRA.Mods.RA2/Widgets/CommandBarPreferences.cs`
- Modify: `OpenRA.Mods.RA2/Widgets/CustomCommandBarWidget.cs`
- Modify: `OpenRA.Mods.RA2/Widgets/Logic/SelectionCommandBarLogic.cs`

- [ ] **Step 1: Add failing availability and migration tests**

Require touch availability to contain STOP/CYCLE_BASE and still exclude the three floating duplicates. Cover versions 1–4, duplicate inputs, a new install default list, and desktop isolation:

```csharp
var upgraded = CommandBarLayoutPolicy.NormalizeVisibleOrder(
	new[] { "GROUP_01", "STOP", "STOP" }, true, 3).ToArray();
Assert.That(upgraded.Count(x => x == "STOP"), Is.EqualTo(1));
Assert.That(upgraded.Count(x => x == "CYCLE_BASE"), Is.EqualTo(1));

var currentMissing = CommandBarLayoutPolicy.NormalizeVisibleOrder(
	new[] { "GROUP_01" }, true, 4).ToArray();
Assert.That(currentMissing, Does.Contain("STOP"));
Assert.That(currentMissing, Does.Contain("CYCLE_BASE"));
```

Assert `CurrentVersion == 4`, the compact exact tail is `QUEUE_ORDERS, STOP, CYCLE_BASE`, and `IsRequiredTouchId` is false on desktop.

- [ ] **Step 2: Add failing edit-protection tests**

Extract or expose a pure policy used by both `ToggleSlot` and the drag-to-hidden branch:

```csharp
Assert.That(CommandBarLayoutPolicy.CanHide("STOP", true), Is.False);
Assert.That(CommandBarLayoutPolicy.CanHide("CYCLE_BASE", true), Is.False);
Assert.That(CommandBarLayoutPolicy.CanHide("STOP", false), Is.True);
Assert.That(CommandBarLayoutPolicy.CanHide("SCATTER", true), Is.True);
```

Add executable/source wiring assertions proving all three edit paths—left-click toggle, right-click toggle/context action, and drag-to-hidden—call the same policy so the required commands remain visible immediately, not only after reload.

- [ ] **Step 3: Add failing physical-size, label, Safe-Area and live-widget tests**

For all approved phone/tablet snapshots and compact/expanded states, exercise `TouchCommandBarSlotPolicy.Create(snapshot, id, expanded)` and assert compact and expanded STOP/CYCLE_BASE hit rectangles are at least `LogicalPoints(44)` in both dimensions, use `IosTouchLabel`, keep compact labels visible with the existing Fluent keys, and bind `STOP -> commandbar-icons/stop` plus `CYCLE_BASE -> commandbar-icons/cycle-base`. Exercise `TouchCommandBarSlotPolicy.PlaceBar(snapshot, size)` and assert the returned rectangle is centered inside `snapshot.SafeBounds`, has a 12pt bottom margin when space permits, and is fully contained.

Add a production behavior seam around `CustomCommandBarWidget.ApplyLayout` and instantiate a representative widget tree. Assert the actual row, toggle, edit tray, each required Button `RenderBounds`, icon bounds, label visibility/font, and parent command-bar bounds all consume the same physical row height and remain inside SafeBounds. Do not accept a pure-policy-only test that can pass while the real widget continues using `ButtonHeight`.

Cache a complete screen signature (renderer size, native point size and Safe Area) in `CustomCommandBarWidget.Tick()`. Add a test that changes each component and proves the live widget re-applies layout, including a left/right Safe Area swap.

- [ ] **Step 4: Add failing base-cycle policy tests**

Extract a pure ordering function used by `CycleBase` and cover:

```csharp
// Primary first, then ActorID; no current candidate selects first; selected candidate advances; last wraps.
Assert.That(OrderBaseIds(new[] { (9u, false), (7u, true), (3u, false) }),
	Is.EqualTo(new[] { 7u, 3u, 9u }));
```

Add behavior tests for: no BaseBuilding traits falls back to the first selectable owned Building; no eligible building performs no selection, viewport movement or click; success changes selection once, centers the viewport once and plays one click. Keep the existing fallback and feedback.

- [ ] **Step 5: Run the quickbar suite and capture RED**

Run:

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~IosProductionBatchToggleTest|FullyQualifiedName~IosViewportControlsLayoutTest' \
  -v minimal
```

Expected: old suppression, version, compact order, edit and sizing policies fail.

- [ ] **Step 6: Implement version 4 and required touch IDs**

In `CommandBarLayoutPolicy` add:

```csharp
public const int CurrentVersion = 4;
static readonly string[] RequiredTouchIds = { "STOP", "CYCLE_BASE" };
public static bool IsRequiredTouchId(string id, bool touchLayout) =>
	touchLayout && RequiredTouchIds.Contains(id);
public static bool CanHide(string id, bool touchLayout) =>
	!IsRequiredTouchId(id, touchLayout);
```

Remove STOP/CYCLE_BASE from `TouchSuppressedIds`. Keep the legacy X5 migration on its independent `< 3` threshold so v3/v4 layouts that deliberately omit `PRODUCTION_X5` do not resurrect it. After filtering and deduplication, append each missing required touch ID exactly once for every touch configuration, including v4 and new installs; do not change desktop defaults. Add explicit v3/v4 no-X5-resurrection tests.

- [ ] **Step 7: Implement compact order, edit protection and touch sizing**

Append STOP/CYCLE_BASE to `TouchCompactIds`. Make the left-click and right-click/context toggle paths return immediately when `CanHide` is false, and make the drag-to-hidden branch retain required IDs.

Add this pure policy next to the command-bar layout policy and consume it from `CustomCommandBarWidget`:

```csharp
public readonly struct TouchCommandBarSlotLayout
{
	public readonly Size ButtonSize;
	public readonly Rectangle IconBounds;
	public readonly Rectangle LabelBounds;
	public readonly string Font;
	public readonly bool ShowLabel;

	public TouchCommandBarSlotLayout(
		Size buttonSize, Rectangle iconBounds, Rectangle labelBounds,
		string font, bool showLabel)
	{
		ButtonSize = buttonSize;
		IconBounds = iconBounds;
		LabelBounds = labelBounds;
		Font = font;
		ShowLabel = showLabel;
	}
}

public static class TouchCommandBarSlotPolicy
{
	public static TouchCommandBarSlotLayout Create(
		IosScreenSnapshot snapshot, string id, bool expanded)
	{
		var required = CommandBarLayoutPolicy.IsRequiredTouchId(id, true);
		var height = snapshot.LogicalPoints(expanded ? 52 : 44);
		var width = required ? snapshot.LogicalPoints(56) :
			expanded ? CommandBarCatalog.Get(id).Width : 44;
		var showLabel = expanded || required;
		var labelHeight = snapshot.LogicalPoints(14);
		var bottomInset = snapshot.LogicalPoints(2);
		var iconSize = required ? snapshot.LogicalPoints(26) : 26;
		var label = showLabel
			? new Rectangle(0, height - bottomInset - labelHeight, width, labelHeight)
			: Rectangle.Empty;
		var iconBottom = showLabel ? label.Top : height;
		var icon = new Rectangle((width - iconSize) / 2,
			Math.Max(0, (iconBottom - iconSize) / 2), iconSize, iconSize);
		return new TouchCommandBarSlotLayout(
			new Size(width, height), icon, label,
			required ? "IosTouchLabel" : "TinyBold", showLabel);
	}

	public static Rectangle PlaceBar(IosScreenSnapshot snapshot, Size size)
	{
		var safe = snapshot.SafeBounds;
		var margin = snapshot.LogicalPoints(12);
		return new Rectangle(
			safe.Left + Math.Max(0, (safe.Width - size.Width) / 2),
			Math.Max(safe.Top, safe.Bottom - margin - size.Height),
			Math.Min(size.Width, safe.Width), Math.Min(size.Height, safe.Height));
	}
}
```

`CustomCommandBarWidget` must choose one physical row height for the current touch state, then use it consistently for active buttons, edit tray, total bar height and toggle position. It must place/reflow every child after `PlaceBar`, use the returned per-slot widths/icon/label bounds, set `StretchToFit = true` only for required touch icons, and retain the existing desktop constants and rendering path. Re-run this layout when the cached complete screen signature changes.

- [ ] **Step 8: Implement stateless primary-first base cycling**

Order candidates with:

```csharp
var bases = world.ActorsHavingTrait<BaseBuilding>()
	.Where(a => a.Owner == player)
	.OrderByDescending(a => a.TraitOrDefault<PrimaryBuilding>()?.IsPrimary == true)
	.ThenBy(a => a.ActorID)
	.ToList();
```

Select the first candidate when none is selected, otherwise advance and wrap; keep the existing fallback, viewport centering and `PlayClick()`.

- [ ] **Step 9: Verify GREEN and desktop isolation**

Run the Step 5 command and the adjacent command-bar/selection tests. Expected: all pass with nonzero counts and desktop assertions unchanged.

## Task 4: Split Active Widget and Atlas Bundle Contracts

**Files:**
- Modify: `packaging/tests/test_ios_touch_skin_assets.py`
- Modify: `packaging/tests/test_ios_touch_runtime_bundle.py`
- Modify: `packaging/audit_ios_touch_runtime_bundle.py`
- Modify: `packaging/public-content-manifest.json`

- [ ] **Step 1: Write failing audit tests**

Define two explicit contracts:

```python
ACTION_ATLAS_GLYPHS = {
    "STOP": "ios-stop",
    "DEPLOY": "ios-deploy",
    "SELECT_TYPE": "ios-select-type",
    "FORCE_ATTACK": "ios-force-attack",
    "RETURN_BASE": "ios-return-base",
}
ACTIVE_VIEWPORT_ACTIONS = {
    "DEPLOY": "ios-deploy",
    "SELECT_TYPE": "ios-select-type",
    "FORCE_ATTACK": "ios-force-attack",
}
```

Keep pixel/region checks over all five atlas glyphs. Make YAML/runtime semantic checks require exactly three active buttons. Add tamper mutations that re-add STOP/RETURN_BASE, remove one active action, or remove an unused atlas region; each mutation must fail for the intended reason.

- [ ] **Step 2: Run Python tests and capture RED**

Run:

```sh
python3 -B -m unittest \
  packaging.tests.test_ios_touch_skin_assets \
  packaging.tests.test_ios_touch_runtime_bundle -v
```

Expected: current five-active-action constants reject the new contract.

- [ ] **Step 3: Implement the split contract**

Update the production audit and fixtures to use all five entries only for atlas/file integrity and three entries for live YAML/logic wiring. Preserve all current source-to-bundle hash comparisons and managed assembly metadata gates.

- [ ] **Step 4: Refresh the manifest precisely**

Recompute only entries whose tracked content changed, especially `mods/ra2/chrome/ingame.yaml`:

```sh
shasum -a 256 mods/ra2/chrome/ingame.yaml
stat -f '%z %N' mods/ra2/chrome/ingame.yaml
```

Update only the matching `sha256` and `size`; retain category, license and `public` fields.

- [ ] **Step 5: Verify Python and content gates**

Run the Step 2 command plus:

```sh
./utility.sh --check-yaml
./utility.sh --check-missing-sprites
```

Expected: all commands exit 0 apart from documented warning-only output.

## Task 5: Restore Complete Engine Patch Reproducibility

**Files:**
- Create: `engine-patches/0013-post-0012-overlay-sync.patch`
- Create: `engine-patches/0014-ios-three-action-quarter-ring.patch`
- Create: `engine-patches/0015-ios-lobby-scroll-layout-polish.patch`
- Modify: `engine-patches/README.md`

- [ ] **Step 1: Verify the official archive**

Use the existing official ZIP only if:

```sh
shasum -a 256 /private/tmp/openra-release-20250330.5qwdqs/openra-release-20250330.zip
```

prints `ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a`.

- [ ] **Step 2: Generate 0013 from the pre-A1 committed engine overlay**

Apply 0001–0012 to a fresh extraction. Diff exactly these six already-committed drift paths:

```text
OpenRA.Mods.Common/Widgets/IosViewportControlsLayout.cs
OpenRA.Mods.Common/Widgets/Logic/Ingame/IosViewportActionsLogic.cs
OpenRA.Test/IosViewportActionLayoutTest.cs
OpenRA.Test/IosViewportControlsLayoutTest.cs
OpenRA.Test/V3RenderingRulesTest.cs
OpenRA.Test/YuriProductionCameoTest.cs
```

Confirm 0013 contains no A1 behavior by comparing its four viewport files against their blobs from pre-A1 commit `6c80d55` and the two regression tests against commit `6ff9a65`.

- [ ] **Step 3: Generate 0014 from the post-0013 replay tree to the final A1 engine files**

Allow exactly these A1 engine paths:

```text
OpenRA.Mods.Common/Widgets/IosViewportControlsLayout.cs
OpenRA.Mods.Common/Widgets/VirtualViewportJoystickWidget.cs
OpenRA.Mods.Common/Widgets/Logic/Ingame/IosViewportActionsLogic.cs
OpenRA.Test/IosViewportControlsLayoutTest.cs
OpenRA.Test/IosViewportActionLayoutTest.cs
OpenRA.Test/IosProductionBatchToggleTest.cs
```

Do not include root RA2 files, binaries, obj/bin, assets or private paths in an engine patch.

- [ ] **Step 4: Generate 0015 from the post-0014 replay tree**

Allow exactly this disjoint lobby/map-preview/scroll engine complement:

```text
OpenRA.Mods.Common/Widgets/Logic/IosMapPreviewLayout.cs
OpenRA.Mods.Common/Widgets/Logic/IosTouchWidgetPolicy.cs
OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyLayout.cs
OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs
OpenRA.Mods.Common/Widgets/ScrollPanelWidget.cs
OpenRA.Test/IosLobbyResponsiveLayoutTest.cs
OpenRA.Test/IosTouchWidgetPolicyTest.cs
```

Keep the root mod `mods/ra2/chrome.yaml` as an ordinary outer-repository change; it must not appear in an engine patch.

- [ ] **Step 5: Fresh-replay 0001–0015 and compare**

Extract the official ZIP again, apply every patch in filename order with `patch -p1 -s --batch`, reject `.rej`, and `cmp` every tracked materialized engine file against the workspace. At the post-0013, post-0014 and post-0015 layers, record tree hashes, repeat that layer's patch with `--forward --dry-run --batch`, require nonzero refusal, require the before/after tree hashes to match, and verify `-R --dry-run` succeeds.

- [ ] **Step 6: Document the three patches**

Add README rows that distinguish overlay synchronization, A1 behavior, and the disjoint non-A1 lobby/scroll complement while preserving the official archive checksum instructions.

## Task 6: Full Verification and Development Commit

**Files:** all task files above

- [ ] **Step 1: Run the complete focused and adjacent suites**

Run:

```sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~IosViewportControlsLayoutTest|FullyQualifiedName~IosViewportActionLayoutTest|FullyQualifiedName~IosProductionBatchToggleTest|FullyQualifiedName~IosFloatingControlsToggleTest|FullyQualifiedName~IosTouchWidgetPolicyTest|FullyQualifiedName~IosLobbyResponsiveLayoutTest' \
  -v minimal

/usr/bin/python3 -B -m unittest \
  packaging.tests.test_ios_touch_skin_assets \
  packaging.tests.test_ios_touch_runtime_bundle -v

./utility.sh --check-yaml
./utility.sh --check-missing-sprites
git diff --check
```

Record every nonzero test count and exit code. The personal iOS bundle audit in Task 7 is the authoritative packaged-content gate; do not expand this task by rewriting the unrelated legal SBOM snapshot.

- [ ] **Step 2: Build the two relevant managed projects**

Run single-node Release builds for `engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj` and `OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj`. Expected: 0 errors; record baseline NU1901 separately.

- [ ] **Step 3: Request ordered reviews**

First dispatch a fresh spec-compliance reviewer against the approved design and plan. Resolve every Critical/Important issue and re-review. Only after spec PASS dispatch a fresh code-quality reviewer; resolve and re-review before proceeding.

- [ ] **Step 4: Stage only the explicit allowlist**

Use ordinary `git add` for tracked root/packaging/docs/patch files (including root `mods/ra2/chrome.yaml`) and exact `git add -f -- <approved engine path...>` for the six A1 engine files plus the seven 0015 engine files (and only the explicitly approved 0013 drift paths if they are materially changed). Show the complete staged name-status list and confirm no untracked artifacts, private content, generated obj/bin or retail assets are included.

- [ ] **Step 5: Commit the verified implementation**

Commit with:

```sh
git commit -m "Fix iOS quarter-ring touch controls"
```

## Task 7: Signed Release Build and Connected Real-Device Installation

**Files:** no source edits expected

- [ ] **Step 1: Preserve device data evidence**

Enumerate the connected physical iPhone, iPad mini and iPad Air at execution time. For every target, confirm it is unlocked, paired, in Developer Mode and present in the selected installed profile. Record its resolved CoreDevice ID/hardware UDID, installed-app JSON, process ID, exception-log count, and `Documents/OpenRA/settings.yaml` SHA-256. Never run uninstall. Do not rely on stale hard-coded IDs or profile UUIDs if Xcode refreshed them.

- [ ] **Step 2: Build and sign the final commit**

Use the freshly enumerated CoreDevice IDs (examples below, not fixed contracts) and the freshly selected valid profile. Run:

```sh
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -c Release -r ios-arm64 -m:1 \
  -p:CodesignKey='Apple Development: kevinjay@vip.qq.com (MTZA59F583)' \
  -p:CodesignProvision=ac93a0de-2e3e-408b-8c3a-d69579bc6719
```

Expected: exit 0 and signed `ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app`.

- [ ] **Step 3: Run signed-bundle gates**

Run strict codesign, loading-brand audit and touch-runtime audit against the newly built app. Confirm `com.openra.ipad.personal`, Team `267KQ4A3N4`, profile UUID, three-device provisioning membership, and source/bundle equality.

- [ ] **Step 4: Cover-install without uninstalling**

Run:

```sh
xcrun devicectl device install app \
  --device 77B248CD-CB9B-583C-8E67-7FA48E6409A5 \
  --timeout 600 --json-output /private/tmp/ra2-a1-iphone-install.json \
  ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app

xcrun devicectl device install app \
  --device C5CA8947-3880-593B-84D6-B57DE642A8FD \
  --timeout 600 --json-output /private/tmp/ra2-a1-ipad-air-install.json \
  ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app

xcrun devicectl device install app \
  --device B30BE649-3E0F-58EA-B25E-3F6B74E6D9E8 \
  --timeout 600 --json-output /private/tmp/ra2-a1-ipad-mini-install.json \
  ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app
```

Run each command only after its lock check passes. Expected: successful install JSON for `com.openra.ipad.personal`, the correct Team/profile/signature, and a queryable installed bundle on every target. A changed private bundle URL is useful evidence when present but is not a required success contract. If any of the three requested targets remains locked or unavailable, the installation task is not complete: report that target as blocked rather than silently skipping it or claiming all-device completion.

- [ ] **Step 5: Launch and verify sustained execution**

Terminate only the existing process through launch, start the new bundle, wait 10–15 seconds, and query processes. Require launch outcome success, a PID, and the process still present after the wait.

- [ ] **Step 6: Run loading smoke and post-install data checks**

Run `ios/scripts/run-loading-smoke.sh` with the device ID and a unique output directory. Require `smoke-ready ... criterion=duration-and-progress` and no matching `smoke-failure`. Re-copy `settings.yaml` and require its hash to match the preinstall value; require no new `exception-*.log`.

- [ ] **Step 7: Report the one remaining manual visual check honestly**

Report automatic installation/runtime evidence separately from the manual on-device visual acceptance: joystick at Safe Area +12pt, three buttons at 12/1:30/3, Stop/Base visible in compact and expanded bars, readable labels, and correct Base cycling.
