# Cross-Platform Startup Notice Design

Date: 2026-09-09
Status: Approved approach; written-spec review pending

## Goal

Present the same NUKE HOUR distribution, independence, lawful-import, free/open-source, anti-resale, and download-safety notice at startup on iOS/iPadOS, macOS, and Android while preserving each platform's native content-import workflow.

## Repositories and Scope

- iOS/iPadOS and macOS are implemented in `/Users/kevin/Projects/ra2-mac`.
- Android is implemented in `/Users/kevin/Projects/RA2Android`.
- Build 4 recovery, emergency-backup, release-clone, and worktree directories are outside scope and must not be inspected or modified.
- Multiplayer protocol, RuntimeContract, gameplay, retail-content validation rules, and imported user data remain unchanged.

Each repository keeps its own Git history. Existing tracked and untracked work is preserved, and only explicit notice-related files are staged.

## Shared Product Contract

Every platform shows the same core meaning in English and Simplified Chinese:

1. NUKE HOUR is an independent, completely free, open-source community project.
2. `EA has not endorsed and does not support this product.`
3. NUKE HOUR is not affiliated with, sponsored by, authorized by, or officially partnered with Electronic Arts Inc., Apple Inc., or Google LLC.
4. The distributed application contains no third-party retail game files. Users must manually import required files from a legally purchased and owned copy.
5. `Command & Conquer: Red Alert 2` and `Yuri's Revenge` are plain-text functional references used only to describe compatibility and identify files for import.
6. A claim that a separately sold copy is an officially authorized NUKE HOUR edition is false. A purchaser should report the seller and request a refund.
7. Users should download only from `https://nukehour.com` to reduce tampering and malicious-code risk.
8. A compact attribution identifies the respective EA, Apple, and Google word marks.

No third-party logo, artwork, trade dress, or official-looking typography is added. Third-party names do not become part of the NUKE HOUR product name.

The official website URL is a fixed HTTPS constant in platform code, not localization or user input. A failed system-browser handoff leaves the notice visible and reports a localized non-fatal error.

## Shared Presentation Contract

- The long notice body scrolls independently on compact screens.
- The official website action, primary continue/import actions, and version remain visible outside the scrolling body.
- The version is anchored at the bottom-right and uses platform package metadata rather than engine or multiplayer compatibility versions.
- Visible format: `NUKE HOUR <version> (Build <build>)`. It must never include `RA2`.
- A platform cannot automatically enter the game merely because retail content is already present. The user explicitly chooses `Enter Game` or `Continue` after the notice is presented.
- The notice is presented on every fresh application/launcher process start. No account, network service, or persisted acknowledgement is required.

## iOS/iPadOS

The implementation committed in `a9ccb45` is the reference behavior:

- the existing native content-import gate is a safe-area two-column panel;
- content status and import actions remain on the left;
- the notice card, scrolling body, and website action remain on the right;
- `CFBundleShortVersionString` and `CFBundleVersion` provide the bottom-right version;
- both base-ready and fully-ready states require the explicit `Enter Game` action.

No additional iOS behavior is required unless cross-platform verification finds that the shared contract is not met.

## macOS

The Tk launcher gains a dedicated startup notice page rather than a non-customizable message box.

- The notice page is the first visible page for every launcher process.
- A scrollable notice card contains the shared bilingual copy.
- The existing fixed `https://nukehour.com` browser action is reused.
- `Continue` routes to Content Management when required retail content is missing, otherwise to the existing Home page.
- The page does not import, move, or inspect files. Existing Content Management remains the only retail-import surface.
- The global launcher version footer remains bottom-right and is upgraded to the platform package version/build contract without changing multiplayer compatibility.
- Navigation cannot silently bypass the initial notice before `Continue`; after acknowledgement within the current process, normal launcher navigation works unchanged.

The launcher remains fully usable offline. Opening the website is optional and never blocks local play or import.

## Android

The current `AlertDialog`-only startup gate is replaced with a full-screen native Android notice/import view built by `MainActivity`.

- A vertical root layout contains a fixed heading, a `ScrollView` for the notice, fixed website/version controls, and fixed action controls.
- Device/system language selects English or Simplified Chinese notice strings without expanding the rest of the game's localization scope.
- `Official Website` launches an `Intent.ActionView` intent for the fixed HTTPS URL.
- Package `VersionName` and `LongVersionCode` provide the bottom-right version.
- When retail content is ready, `Enter Game` is enabled but the engine does not auto-start.
- When content is missing, `Choose Game Folder` remains available and `Enter Game` remains unavailable.
- Successful import returns to the same notice screen with refreshed readiness and an enabled `Enter Game`; it does not bypass the notice by starting the engine automatically.
- Importing uses the existing document-tree picker and importer. Its validation, storage, and clean-package rules remain unchanged.

The implementation must not add WebView content, analytics, account requirements, cloud dependencies, or network calls beyond the user-initiated external-browser action.

## Error and Lifecycle Handling

- Rotation and activity recreation rebuild the notice from current retail readiness without starting the engine.
- Repeated button taps cannot start the engine twice.
- Cancelling file selection returns to the notice/import screen.
- Browser-launch failure, missing version metadata, and incomplete content are recoverable and do not crash.
- Version fallbacks are independently `0.0.0` and `0`, producing `NUKE HOUR 0.0.0 (Build 0)`.
- User filesystem paths and raw exception text remain diagnostic-only and are not added to the new notice text.

## Testing and Acceptance

### iOS/iPadOS

- Existing `IosContentImportContractTest` remains green.
- Complete managed tests pass.
- iOS ARM64 Release builds with zero compile errors and passes existing brand/runtime bundle audits.

### macOS

- Python tests prove the notice is the initial page, Continue routes according to content readiness, the fixed URL is used, core bilingual copy is present, the notice body scrolls, and the bottom-right version has no `RA2`.
- Existing launcher, localization, content-discovery, and PublicClean packaging tests pass.
- The macOS launcher package builds far enough to inspect its Info.plist and bundled notice/version behavior.

### Android

- NUnit source-contract tests prove bilingual core copy, fixed HTTPS intent, full-screen scroll structure, package-derived bottom-right version, readiness-gated Enter Game, no ready-state auto-start, and no import-success auto-start.
- Complete managed tests pass.
- Android ARM64 Release builds with zero compile errors and its APK metadata matches the Android version manifest.

## Completion Boundary

Completion requires committed implementation in both repositories plus fresh verification evidence for all three platforms. No merge, push, branch deletion, cleanup, device installation, or Build 4 work begins without a separate explicit instruction.
