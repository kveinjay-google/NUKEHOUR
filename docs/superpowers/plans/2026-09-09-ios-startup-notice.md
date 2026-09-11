# iOS Startup Notice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the approved bilingual legal/safety notice, official website link, explicit launch action, and bottom-right bundle version to the native iOS content-import startup screen.

**Architecture:** Extend the existing immutable `IosNativeStrings` tables and native UIKit wizard instead of adding a separate startup modal. Keep the official URL fixed in code, derive display version from `NSBundle`, and retain the existing coordinator/importer boundary while replacing automatic ready-state launch with explicit user entry.

**Tech Stack:** C# 12, .NET 8 for iOS, UIKit Auto Layout, NUnit source-contract tests

---

### Task 1: Lock the startup-screen contract with failing tests

**Files:**
- Modify: `engine/OpenRA.Test/IosContentImportContractTest.cs`

- [x] **Step 1: Add focused contract tests**

Add tests that require the fixed `https://nukehour.com` URL, `UIApplication.SharedApplication.OpenUrl`, bilingual core notice copy, `NSBundle.MainBundle`, bottom/right version constraints, a notice `UIScrollView`, and explicit `Enter Game` support for both ready states. Add a coordinator assertion that the ready branch does not call `StartGameOnce` immediately after `Show`.

- [x] **Step 2: Run the focused test fixture and verify RED**

Run:

```bash
DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~IosContentImportContractTest -v minimal
```

Expected: the new notice/link/version/explicit-entry assertions fail because the current screen does not implement them.

### Task 2: Implement bilingual content and explicit startup entry

**Files:**
- Modify: `ios/OpenRA.iOS/ViewController.cs`
- Modify: `ios/OpenRA.iOS/IosContentImportCoordinator.cs`
- Test: `engine/OpenRA.Test/IosContentImportContractTest.cs`

- [x] **Step 1: Extend the immutable localization table**

Add English and Simplified Chinese strings for `Important Notice`, independent status, the exact EA non-endorsement sentence, lawful import, free/open-source anti-resale guidance, official-download safety guidance, trademark attribution, official website action, and website-open failure accessibility text.

- [x] **Step 2: Require explicit entry in ready states**

Remove the immediate `StartGameOnce()` call from the fully-ready readiness branch. Render `Enter Game` when state is either `NeedsYuriOptional` or `Ready`.

- [x] **Step 3: Run the focused tests and keep the remaining UI contract RED**

Run the Task 1 command. Expected: copy and coordinator behavior pass; link/layout/version assertions still fail.

### Task 3: Build the responsive notice UI

**Files:**
- Modify: `ios/OpenRA.iOS/ViewController.cs`
- Test: `engine/OpenRA.Test/IosContentImportContractTest.cs`

- [x] **Step 1: Add fixed website and bundle-version helpers**

Define a fixed HTTPS `NSUrl`, open it through the system application URL handler, and format `NUKE HOUR <short version> (Build <bundle build>)` with stable fallbacks.

- [x] **Step 2: Replace the single-column panel with responsive two-column content**

Build a safe-area panel with a left content/action stack and right notice card. Put the long notice body in a `UIScrollView`; keep the website action and version label outside the scroll view. Constrain the version label to the panel trailing and bottom anchors and retain 48-point-or-larger controls.

- [x] **Step 3: Preserve compact-width readability**

Use multiline labels, Dynamic Type, content layout guides, compression/hugging priorities, and safe-area constraints. Keep the panel fully inside the available landscape height rather than relying on intrinsic content height.

- [x] **Step 4: Run focused tests and verify GREEN**

Run the Task 1 command. Expected: all `IosContentImportContractTest` tests pass.

### Task 4: Regression and build verification

**Files:**
- Modify only if verification exposes an in-scope defect.

- [x] **Step 1: Run branding and importer regressions**

```bash
DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter 'FullyQualifiedName~IosContentImportContractTest|FullyQualifiedName~PublicBrandingPolicyTest|FullyQualifiedName~RetailContentImporterTest' -v minimal
```

Expected: zero failures.

- [x] **Step 2: Run the complete managed suite**

```bash
DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -v minimal
```

Expected: zero failures.

- [x] **Step 3: Build the iOS ARM64 Release application**

```bash
MSBUILDDISABLENODEREUSE=1 DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj -c Release -f net8.0-ios -p:RuntimeIdentifier=ios-arm64 -m:1 --no-restore
```

Expected: exit code 0 with no compile errors.

- [x] **Step 4: Inspect the final diff and commit**

Run `git diff --check`, review only the planned files, display the complete staged file list, and commit the implementation without touching unrelated tracked or untracked work.
