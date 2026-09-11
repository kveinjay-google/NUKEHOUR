# iOS TestFlight Import Startup Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the asset-free iOS TestFlight build reliably transition from retail import to a fully visible game menu on first entry and subsequent launches.

**Architecture:** Treat non-retail presentation files as an explicit build-time contract and validate it before engine startup. Move UIKit-to-SDL transition ownership into `ViewController`, so modal dismissal, overlay removal, layout completion, and engine launch occur in a deterministic order. Keep raw errors in diagnostics and restore a visible native error surface after any failure.

**Tech Stack:** .NET 8 iOS, UIKit, SDL2/OpenGL ES, MSBuild bundle resources, NUnit, Python bundle-audit tests, `xcrun devicectl`.

---

### Task 1: Lock the failing contracts

**Files:**
- Modify: `engine/OpenRA.Test/IosContentImportContractTest.cs`
- Create: `packaging/tests/test_ios_public_presentation_contract.py`

- [ ] Add tests that require the ready state to expose an explicit enter action and prohibit direct `startGame()` calls from import-result handling.
- [ ] Add source-contract tests requiring `ViewController` to dismiss presented controllers, remove the wizard, complete layout, and only then invoke the launch callback.
- [ ] Add a bundle contract test that resolves every mandatory non-retail UI/font/shader path and verifies that maps and retail extensions are not allowlisted.
- [ ] Run the focused NUnit and Python tests and confirm that the new handoff and presentation tests fail against the current code.

### Task 2: Restore the PublicClean presentation dependency closure

**Files:**
- Modify: `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Create: `packaging/ios/public_clean_manifest.json`
- Modify: `packaging/tests/fixtures/public-clean-iossimulator-arm64-baseline.json`

- [ ] Add exact PublicClean MSBuild includes for `VERSION`, engine shaders, RA2 fonts, reviewed `bits` additions, and reviewed `uibits`; do not include `mods/ra2/maps` or retail files.
- [ ] Synchronize hash/license classifications from `packaging/macos/public_clean_manifest.json` for the shared reviewed files and add the nine user-confirmed NUKE HOUR wallpapers.
- [ ] Regenerate the expected iOS public resource baseline from the explicit allowlist.
- [ ] Run the focused Python contract and public audit unit tests until they pass.

### Task 3: Make UIKit-to-SDL handoff deterministic

**Files:**
- Modify: `ios/OpenRA.iOS/ViewController.cs`
- Modify: `ios/OpenRA.iOS/IosContentImportCoordinator.cs`
- Modify: `engine/OpenRA.Test/IosContentImportContractTest.cs`

- [ ] Implement `BeginGameLaunch(Action)` in the native view controller.
- [ ] If a controller is presented, dismiss it first; then remove the wizard, hide the native status surface, force layout, and schedule the callback with `BeginInvokeOnMainThread`.
- [ ] Route the coordinator's one-shot launch through this method.
- [ ] Run the focused NUnit test and confirm the handoff ordering passes.

### Task 4: Add preflight and visible startup recovery

**Files:**
- Create: `ios/OpenRA.iOS/IosLaunchPreflight.cs`
- Modify: `ios/OpenRA.iOS/AppDelegate.cs`
- Modify: `ios/OpenRA.iOS/ViewController.cs`
- Modify: `engine/OpenRA.Test/IosContentImportContractTest.cs`

- [ ] Define the mandatory non-retail bundle paths and return all missing paths deterministically.
- [ ] Call preflight before `Game.InitializeAndRun`; throw one typed exception before audio/input/renderer initialization when the app bundle is incomplete.
- [ ] On any startup exception, make the app window key and visible, remove stale native overlays, unhide and front the error label, and force layout.
- [ ] Run focused tests and prove a missing bundle dependency selects the native failure path.

### Task 5: Build, audit, and test the physical device lifecycle

**Files:**
- Modify only if verification exposes a defect in files listed above.

- [ ] Run all focused NUnit and Python tests.
- [ ] Build the unsigned/simulator PublicClean bundle and verify presentation closure.
- [ ] Run the public bundle audit and confirm forbidden retail extensions/maps are absent.
- [ ] Increment the independent iOS build number.
- [ ] Build and sign the device PublicClean app with the configured Apple developer identity.
- [ ] Install on the connected iPad while preserving the app data container, launch, and capture the import/ready/menu states.
- [ ] Terminate and relaunch the app, capture the visible menu again, and collect startup/render logs.
- [ ] Record exact verification commands and results before declaring completion.
