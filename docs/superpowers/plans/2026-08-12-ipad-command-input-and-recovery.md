# iPad Command Input and Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make short taps issue classic RTS commands on iPad, remove the accidental terrain grid, and recover cleanly from interrupted touch/lifecycle sequences.

**Architecture:** Reuse OpenRA's classic mouse policy through an iOS launch override instead of teaching the platform layer about game selection. Keep gesture recovery inside `TouchGestureAdapter` and keep SDL finger bookkeeping inside `Sdl2Input`, with one idempotent reset path used by lifecycle events and stale ignored-touch recovery. Initialize the terrain visualization to off through a small policy helper that is directly testable without an iOS runtime.

**Tech Stack:** C# 12, .NET 8 iOS, SDL2, NUnit, OpenRA traits and input pipeline.

---

### Task 1: iPad classic command semantics

**Files:**
- Modify: `ios/OpenRA.iOS/AppDelegate.cs`
- Test: `engine/OpenRA.Test/SettingsTest.cs`

- [ ] **Step 1: Add a failing launch-override parsing test**

Add an NUnit case alongside existing settings tests that constructs settings with
`Game.UseClassicMouseStyle=true` and asserts `settings.Game.UseClassicMouseStyle`
is true. This proves the exact launch argument used by the iOS host is honored.

- [ ] **Step 2: Run the focused test and verify the precondition**

Run:

```bash
env DOTNET_ROOT=/Users/kevin/.dotnet PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj \
  --filter 'FullyQualifiedName~SettingsTest'
```

Expected: the parsing test passes; if an equivalent test already exists, retain
the stronger existing coverage and record that the launch argument is covered.

- [ ] **Step 3: Add the iOS launch override**

Add this argument to the `launchArguments` array in `AppDelegate`:

```csharp
"Game.UseClassicMouseStyle=true"
```

Keep the override iOS-host-only so desktop settings are unchanged.

- [ ] **Step 4: Run the focused settings test**

Run the Step 2 command. Expected: PASS with zero failures.

### Task 2: Terrain overlay startup policy

**Files:**
- Modify: `engine/OpenRA.Mods.Common/Traits/World/TerrainGeometryOverlay.cs`
- Test: `engine/OpenRA.Test/TerrainGeometryOverlayTest.cs`

- [ ] **Step 1: Write failing policy tests**

Create tests for a pure policy method:

```csharp
[TestCase(true, true, false)]
[TestCase(false, true, false)]
[TestCase(true, false, true)]
[TestCase(false, false, false)]
public void StartupPolicyOnlyForcesOverlayOffOnIos(
    bool current, bool isIos, bool expected)
{
    Assert.That(TerrainGeometryOverlay.StartupEnabled(current, isIos), Is.EqualTo(expected));
}
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```bash
env DOTNET_ROOT=/Users/kevin/.dotnet PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj \
  --filter 'FullyQualifiedName~TerrainGeometryOverlayTest'
```

Expected: compilation fails because `StartupEnabled` does not exist.

- [ ] **Step 3: Add the minimal policy and apply it at world load**

Add:

```csharp
public static bool StartupEnabled(bool current, bool isIos)
{
    return isIos ? false : current;
}
```

At the start of `WorldLoaded`, assign:

```csharp
Enabled = StartupEnabled(Enabled, OperatingSystem.IsIOS());
```

- [ ] **Step 4: Run the focused test and verify GREEN**

Run the Step 2 command. Expected: PASS with zero failures.

### Task 3: Interrupted touch recovery

**Files:**
- Modify: `engine/OpenRA.Game/Input/TouchGestureAdapter.cs`
- Modify: `engine/OpenRA.Platforms.Default/Sdl2Input.cs`
- Test: `engine/OpenRA.Test/TouchGestureAdapterTest.cs`

- [ ] **Step 1: Add failing adapter recovery tests**

Add tests that prove:

```csharp
var adapter = new TouchGestureAdapter();
adapter.Begin(new int2(100, 100), 1000);
adapter.BeginSecond(new int2(200, 100));
var reset = adapter.Cancel();
Assert.That(reset.Count, Is.EqualTo(1));
Assert.That(reset.First.Type, Is.EqualTo(TouchPointerActionType.MiddleUp));

adapter.Begin(new int2(50, 50), 2000);
var tap = adapter.End(new int2(50, 50));
Assert.That(tap.First.Type, Is.EqualTo(TouchPointerActionType.LeftDown));
```

Add a second test that calls `Cancel()` twice and asserts the second result is empty.

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```bash
env DOTNET_ROOT=/Users/kevin/.dotnet PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj \
  --filter 'FullyQualifiedName~TouchGestureAdapterTest'
```

Expected: the two-finger cancellation assertion fails because current `Cancel()`
returns no `MiddleUp` action.

- [ ] **Step 3: Make adapter cancellation complete and idempotent**

Update `Cancel()` so it handles `twoFinger` first, clears all adapter flags, and
returns one `MiddleUp` at `lastCentroid`. Preserve the existing active-drag
`LeftUp` behavior and return empty for an already reset adapter.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run the Step 2 command. Expected: all touch adapter tests pass.

- [ ] **Step 5: Centralize SDL touch reset**

Add a `ResetTouchInput` helper in `Sdl2Input` that dispatches `touchGestures.Cancel()`,
logs one iOS debug message only when active bookkeeping existed, and clears:

```csharp
touchDown = false;
twoFingerTouch = false;
ignoringRemainingTouch = false;
touchFingerId = 0;
secondTouchFingerId = 0;
ignoredTouchFingerId = 0;
```

Call it for focus loss, hidden, and minimized lifecycle events. On an iOS
`SDL_FINGERDOWN` received while `ignoringRemainingTouch` is true, call the reset
helper and process that event as a new primary touch instead of dropping it.

- [ ] **Step 6: Run all OpenRA tests**

Run:

```bash
env DOTNET_ROOT=/Users/kevin/.dotnet PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj
```

Expected: zero failed tests.

### Task 4: Build, install, and device verification

**Files:**
- Verify: `ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app`

- [ ] **Step 1: Build and sign the physical-device Release app**

Run the repository's established `dotnet build` command with `ios-arm64`, signing
identity `Apple Development: kevinjay@vip.qq.com (MTZA59F583)`, and provisioning
profile `5ab89da9-2159-4b1a-ad62-5a5e18019d97`.

Expected: build succeeds with zero errors.

- [ ] **Step 2: Verify signing and install**

Run `codesign --verify --deep --strict` on the app, then install it with
`xcrun devicectl device install app` to device
`C5CA8947-3880-593B-84D6-B57DE642A8FD`.

Expected: designated requirement passes and installation reports bundle ID
`com.openra.ipad.personal`.

- [ ] **Step 3: Launch with console diagnostics**

Launch with `xcrun devicectl device process launch --terminate-existing --console`.
Expected: RA2 loads, no fatal exception appears, and the process remains present.

- [ ] **Step 4: Verify device state and logs**

Confirm the installed app record and process with `devicectl`, then copy the iOS
OpenRA logs. Verify no fatal/crash record and retain any touch recovery messages as
diagnostic evidence.

- [ ] **Step 5: Commit only the focused fix**

Stage the exact implementation and test files from Tasks 1-3, display the staged
file list, and commit with message `Fix iPad command input and touch recovery`.
Do not stage unrelated working-tree changes.
