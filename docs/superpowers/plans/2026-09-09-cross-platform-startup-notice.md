# Cross-Platform Startup Notice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Synchronize the approved NUKE HOUR startup notice, official website action, explicit game entry, and bottom-right package version across iOS/iPadOS, macOS, and Android.

**Architecture:** Keep each platform's native launcher UI and existing retail importer. iOS remains the reference implementation; macOS adds a guarded Tk startup page in `/Users/kevin/Projects/ra2-mac`, and Android adds a native full-screen notice/import view in `/Users/kevin/Projects/RA2Android`. Tests enforce the same copy and behavioral contract without sharing runtime UI code across repositories.

**Tech Stack:** Python 3/Tkinter/unittest on macOS; C# 12/.NET 8/Android Views/NUnit on Android; C#/.NET 8/UIKit/NUnit on iOS

---

### Task 1: Lock the macOS startup contract with failing tests

**Files:**
- Modify: `/Users/kevin/Projects/ra2-mac/packaging/tests/test_macos_launcher_layout.py`
- Modify: `/Users/kevin/Projects/ra2-mac/packaging/tests/test_macos_public_clean_launcher.py`

- [x] **Step 1: Add notice copy, route, and layout tests**

Add assertions requiring `startup_page()` to return `notice`, `initial_page()` to preserve the post-notice home/content route, `_accept_startup_notice()` to unlock navigation and route correctly, and `_page_notice()` to include a scrollable body plus fixed website/continue actions. Assert the exact EA sentence, lawful-import/free/open-source/anti-resale/safe-download phrases, fixed `https://nukehour.com`, and `NUKE HOUR 1.0.3 (Build 3)` formatting without `RA2`.

- [x] **Step 2: Verify RED**

Run:

```bash
python3 -m unittest packaging.tests.test_macos_launcher_layout packaging.tests.test_macos_public_clean_launcher
```

Expected: new tests fail because `startup_page`, `_page_notice`, and package build-version formatting do not exist.

### Task 2: Implement the macOS startup notice

**Files:**
- Modify: `/Users/kevin/Projects/ra2-mac/launcher.py`
- Modify: `/Users/kevin/Projects/ra2-mac/launcher_i18n.py`
- Test: `/Users/kevin/Projects/ra2-mac/packaging/tests/test_macos_launcher_layout.py`
- Test: `/Users/kevin/Projects/ra2-mac/packaging/tests/test_macos_public_clean_launcher.py`

- [x] **Step 1: Add immutable notice constants and version formatting**

Define the fixed shared Chinese source copy in `launcher.py`, add exact English translations to `launcher_i18n.py`, and change `launcher_version_text(version, build)` to return `NUKE HOUR {version} (Build {build})`. Add a pure `launcher_product_version(root)` helper that reads `packaging/nukehour-version.json` in source layout or `Contents/Info.plist` when frozen, and uses stable `0.0.0`/`0` fallbacks.

- [x] **Step 2: Add guarded startup routing**

Initialize `startup_notice_acknowledged = False`, record the existing `initial_page(requested, content_ready)` result as the post-notice target, and initially show `startup_page()`. Until `_accept_startup_notice()` sets acknowledgement true, `show_page()` redirects every non-notice page request back to `notice`.

- [x] **Step 3: Add the dedicated notice page**

Register `notice` in `_build_pages()` as a non-sidebar page. `_page_notice()` renders a fixed heading, a disabled `tk.Text` with its own scrollbar, an official-site action bound to `_open_brand_website`, a fixed Continue action bound to `_accept_startup_notice`, and no import logic. The existing global version footer remains outside this scroll region and uses the product version/build helper.

- [x] **Step 4: Verify GREEN and macOS regressions**

Run:

```bash
python3 -m unittest discover -s packaging/tests -p 'test_macos_*.py'
```

Expected: all macOS launcher, localization, branding, discovery, and PublicClean tests pass.

- [x] **Step 5: Commit the macOS implementation**

Stage only the two production files, the two test files, and this plan's checkbox update. Show the full staged file list, run `git diff --cached --check`, and commit with `feat: synchronize macOS startup notice`.

### Task 3: Lock the Android startup contract with failing tests

**Files:**
- Modify: `/Users/kevin/Projects/RA2Android/engine/OpenRA.Test/NukeHourBrandingTest.cs`

- [x] **Step 1: Add Android notice source-contract tests**

Require `MainActivity.cs` to contain the exact bilingual core phrases, `ScrollView`, the fixed `https://nukehour.com` URI, `Intent.ActionView`, package `VersionName` and `LongVersionCode`, bottom/right gravity, and readiness-controlled Enter Game. Extract the ready-on-create and successful-import source regions and assert they do not call `StartHost()`.

- [x] **Step 2: Verify RED**

Run from `/Users/kevin/Projects/RA2Android`:

```bash
DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~NukeHourBrandingTest -v minimal
```

Expected: the new Android startup-notice assertions fail against the current AlertDialog and automatic-start behavior.

### Task 4: Implement the Android full-screen notice/import view

**Files:**
- Modify: `/Users/kevin/Projects/RA2Android/android/OpenRA.Android/MainActivity.cs`
- Test: `/Users/kevin/Projects/RA2Android/engine/OpenRA.Test/NukeHourBrandingTest.cs`

- [x] **Step 1: Add the bilingual native string table**

Add a private immutable `AndroidStartupStrings` record with English and Simplified Chinese values for heading, shared notice body, website, folder selection, exit, enter game, import progress, cancellation, and safe failures. Resolve Chinese only when the current locale language begins with `zh`; otherwise use English.

- [x] **Step 2: Build the full-screen notice view**

Keep the SDL surface in the existing root `FrameLayout`, then overlay a native opaque notice panel containing a heading, body `ScrollView`, fixed action rows, and a bottom-right version label. Format the package metadata as `NUKE HOUR {VersionName ?? "0.0.0"} (Build {build})`, using `LongVersionCode` on API 28+ and `VersionCode` on API 24–27, and never include `RA2` in that label.

- [x] **Step 3: Wire website and content actions**

Open the fixed HTTPS URI with `new Intent(Intent.ActionView, AndroidUri.Parse(...))`, retaining the screen on failure. Folder selection continues to use `Intent.ActionOpenDocumentTree`. `RefreshStartupNotice()` enables Enter Game only when base content is ready; successful import calls it instead of `StartHost()`.

- [x] **Step 4: Preserve explicit one-time engine entry**

Only the Enter Game handler removes the native notice overlay and calls the existing idempotent `StartHost()`. Activity creation, readiness inspection, cancellation, and successful import never call `StartHost()` automatically.

- [x] **Step 5: Verify GREEN**

Run the focused Task 3 command. Expected: all `NukeHourBrandingTest` tests pass.

- [x] **Step 6: Commit the Android implementation**

Stage only `MainActivity.cs` and `NukeHourBrandingTest.cs`, display the complete staged list, run `git diff --cached --check`, and commit with `feat: synchronize Android startup notice`.

### Task 5: Cross-platform verification

**Files:**
- Modify only when verification exposes an in-scope defect.

- [x] **Step 1: Verify the current iOS reference**

From `/Users/kevin/Projects/ra2-mac` run:

```bash
DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~IosContentImportContractTest -v minimal
```

Expected: 7 tests pass.

- [x] **Step 2: Run the complete macOS/iOS managed suite**

```bash
DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -v minimal
```

Expected: zero failures.

- [x] **Step 3: Build iOS ARM64 Release**

```bash
MSBUILDDISABLENODEREUSE=1 DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj -c Release -f net8.0-ios -p:RuntimeIdentifier=ios-arm64 -m:1 --no-restore
```

Expected: zero compile errors and successful brand/runtime bundle audits.

- [x] **Step 4: Run the complete Android managed suite**

From `/Users/kevin/Projects/RA2Android` run:

```bash
DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -v minimal
```

Expected: zero failures.

- [x] **Step 5: Build Android ARM64 Release**

```bash
MSBUILDDISABLENODEREUSE=1 DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build android/OpenRA.Android/OpenRA.Android.csproj -c Release -p:AndroidSdkDirectory=/opt/homebrew/share/android-commandlinetools -p:JavaSdkDirectory=/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home -m:1 --no-restore
```

Expected: zero compile errors and a signed APK whose manifest versionName/versionCode equal `0.0.3`/`3`.

- [x] **Step 6: Preserve both repositories**

Run `git status --short` and `git log -3 --oneline` in both repositories. Confirm all planned tracked changes are committed and every pre-existing untracked path remains untouched. Keep both feature branches as-is; do not merge, push, delete, or clean either repository.
