# NUKE HOUR Independent Platform Versioning and Core Compatibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give iOS, macOS, and Android independently incrementing product versions while multiplayer admission follows a deterministic core-rules compatibility fingerprint instead of the product version.

**Architecture:** Each platform checkout owns a canonical JSON version manifest and a common Python projection/check tool. Engine metadata separates display, storage, and compatibility identities; the existing wire `Version` field carries compatibility for backward wire-format stability. Platform package projects consume checked generated properties, and release entry points refuse stale projections or development placeholders.

**Tech Stack:** Python 3 standard library, NUnit/.NET 6 engine tests, .NET 8 iOS and Android MSBuild, Bash/macOS packaging, MiniYaml manifests, SHA-256.

---

## File Map

The same shared files are changed in the iOS, macOS, and Android checkouts unless a platform is explicitly named:

- `packaging/nukehour-version.json`: platform-local canonical product version.
- `packaging/nukehour-version.props`: generated MSBuild projection.
- `packaging/nukehour-compatibility-inputs.txt`: deterministic input patterns.
- `packaging/nukehour_version.py`: `show`, `bump patch`, `apply`, and `check` implementation.
- `packaging/tests/test_nukehour_version.py`: version and fingerprint unit tests.
- `engine/OpenRA.Game/Manifest.cs`: metadata fields and fallback properties.
- `engine/OpenRA.Mods.Common/Widgets/Logic/VersionLabelLogic.cs`: user-visible product version.
- `engine/OpenRA.Game/Network/UnitOrders.cs`, `engine/OpenRA.Game/Server/Server.cs`, and `engine/OpenRA.Game/Network/GameServer.cs`: compatibility-based admission and discovery.
- `engine/OpenRA.Game/GameInformation.cs` and `engine/OpenRA.Game/Support/ExceptionHandler.cs`: record product and compatibility identities.
- `engine/OpenRA.Test/NukeHourVersionCompatibilityTest.cs`: runtime metadata and network policy tests.
- `mods/ra2/mod.yaml` plus the iOS profile manifest where present: generated storage, display, and compatibility metadata.
- `mods/ra2-content/mod.yaml` and desktop map-folder metadata: remove the development placeholder and use the stable storage identity.

Platform integration files:

- iOS: `ios/OpenRA.iOS/OpenRA.iOS.csproj`, `ios/OpenRA.iOS/Info.plist`, and `ios/scripts/build-public-clean.sh`.
- macOS: `packaging/macos/buildpackage.sh` and its focused packaging tests.
- Android: `android/OpenRA.Android/OpenRA.Android.csproj` and `android/OpenRA.Android/Properties/AndroidManifest.xml`.

### Task 1: Implement the canonical version tool in the macOS checkout

- [ ] Write `packaging/tests/test_nukehour_version.py` first. Cover valid parsing, malformed SemVer/build/channel/platform rejection, `bump patch`, idempotent `apply`, stale-projection rejection, deterministic sorted hashing, included-core changes changing the digest, and excluded-UI changes preserving it.
- [ ] Run `python3 -m unittest packaging.tests.test_nukehour_version -v`; expect failure because `packaging/nukehour_version.py` does not exist.
- [ ] Add `packaging/nukehour-version.json` with macOS `1.0.1`, build `1`, stable channel, and add an explicit compatibility-input pattern list.
- [ ] Implement the standard-library-only tool. Use atomic same-directory temporary-file replacement, reject unknown JSON keys, normalize paths to `/`, reject symlinks and paths outside the checkout, sort ordinally, frame every hash entry with path and byte length, and support `--root` for isolated tests.
- [ ] Generate `packaging/nukehour-version.props` and the RA2 metadata projections with `apply`.
- [ ] Run the focused Python tests and `check`; expect all tests and the check to pass.
- [ ] Commit only the tool, manifest, inputs, generated projection, tests, and projected mod metadata.

### Task 2: Separate display, storage, and compatibility identities in the engine

- [ ] Add failing NUnit tests in `engine/OpenRA.Test/NukeHourVersionCompatibilityTest.cs` for `DisplayVersionOrVersion` and `CompatibilityOrVersion` fallback, a NUKE HOUR manifest using all three identities, and labels returning the display version.
- [ ] Add failing policy tests proving server validation, client handshake responses, fake replay handshakes, LAN/server-list compatibility, and replay metadata use compatibility rather than display version.
- [ ] Run `/Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~NukeHourVersionCompatibilityTest -v:minimal`; expect assertions to fail against the current single-version behavior.
- [ ] Extend `ModMetadata` with optional `DisplayVersion` and `Compatibility` fields and non-empty fallback properties. Keep existing mods source-compatible by falling back to `Version`.
- [ ] Change `VersionLabelLogic` to render `DisplayVersionOrVersion`.
- [ ] Keep the handshake wire field named `Version`, but populate and compare it using `CompatibilityOrVersion` in server, client, replay handshake, and server-list paths.
- [ ] Add optional `ProductVersion` to `GameInformation`; record product version and compatibility independently. Add both values to exception diagnostics.
- [ ] Run the focused NUnit tests; expect all to pass. Run existing multiplayer policy tests to catch accidental protocol regressions.
- [ ] Commit the engine behavior and tests as one independently reviewable change.

### Task 3: Integrate macOS application packaging

- [ ] Add failing tests to `packaging/tests/test_macos_branding.py` for canonical product/build values, rejection of a mismatched CLI tag, and absence of development placeholders in staged app metadata.
- [ ] Run the focused packaging tests; expect failures against the free-form tag behavior.
- [ ] Update `packaging/macos/buildpackage.sh` to run `nukehour_version.py check`, read the canonical macOS version/build, reject a supplied tag that differs, and write product and build values separately into `Info.plist`.
- [ ] Ensure the RA2 manifest uses stable storage identity `nukehour-storage-v1`, canonical macOS display version, and the generated compatibility digest.
- [ ] Run Python packaging tests, the version check, `git diff --check`, and a macOS staging/package smoke build if signing/notarization is not required for the staging target.
- [ ] Commit the macOS integration.

### Task 4: Port the version system to the iOS checkout

- [ ] Copy the proven tool and tests, then first set the iOS manifest to `1.0.7`, build `7`, and platform `ios`; run tests before changing iOS package metadata so projection assertions fail.
- [ ] Import `packaging/nukehour-version.props` from `ios/OpenRA.iOS/OpenRA.iOS.csproj`, map it to `ApplicationDisplayVersion` and `ApplicationVersion`, and remove conflicting hard-coded plist version values.
- [ ] Add a pre-build `Exec` check that performs no writes. Add the same gate to the public-clean packaging entry point.
- [ ] Port the engine display/compatibility behavior and tests without overwriting unrelated dirty iOS changes. Regenerate the AOT registry only if the metadata implementation introduces a new constructed type; plain fields and properties require no registry entry.
- [ ] Apply the iOS display version and generated compatibility projection to both active iOS manifest profiles.
- [ ] Run Python version tests, focused NUnit tests, existing iOS multiplayer policy tests, iOS bundle audits, and `git diff --check`.
- [ ] Build a signed Release `ios-arm64` app and verify `Info.plist`, runtime labels, compatibility projection, codesign, and absence of development placeholders.
- [ ] Commit only the version-system files and precise engine/manifest hunks, preserving all unrelated worktree changes.

### Task 5: Port the version system to Android

- [ ] Copy the proven tool and tests, then first set the Android manifest to `0.0.2`, build `2`, and platform `android`; run tests before package integration so projection assertions fail.
- [ ] Import the generated props from `android/OpenRA.Android/OpenRA.Android.csproj`, map product/build to Android application version properties, and remove duplicate version values from `Properties/AndroidManifest.xml`.
- [ ] Add a no-write pre-build version check.
- [ ] Port the engine display/compatibility behavior and tests, apply the Android manifest projection, and keep the stable storage identity.
- [ ] Run Python version tests, focused NUnit tests, Android/mobile UI tests, `git diff --check`, and a Release APK build.
- [ ] Inspect the APK manifest and embedded RA2 manifest for Android `0.0.2`, build `2`, the compatibility digest, and no development placeholder.
- [ ] Commit the Android integration.

### Task 6: Preserve user data across the storage-identity migration

- [ ] Add failing tests characterizing discovery of saves, replays, and user maps under both the stable `nukehour-storage-v1` location and the legacy literal development-version location, including conflict cases where the stable file wins and the legacy file is never overwritten or deleted.
- [ ] Run those tests and confirm they fail because current browsers use only `Metadata.Version`.
- [ ] Add a small engine-owned storage-path resolver that returns the stable path first and an existing legacy path second only for NUKE HOUR. Do not copy, move, overwrite, or delete user data.
- [ ] Use it in save/replay browsers and desktop user-map discovery while leaving other mods unchanged.
- [ ] Run focused migration tests and existing save/replay/map-cache tests.
- [ ] Port the exact change to all three checkouts and commit it in each.

### Task 7: Cross-platform compatibility verification

- [ ] Run `nukehour_version.py show` and `check` in all three checkouts and capture the independent product versions.
- [ ] Run a compatibility report command in all three. The compatibility digest must be identical; if it differs, print the differing normalized inputs and correct unintended core drift instead of bypassing the check.
- [ ] Run all focused Python and NUnit suites on all three platforms.
- [ ] Verify that changing a temporary copy of only a UI file leaves the digest unchanged and changing a temporary copy of one rules file changes it.
- [ ] Scan source and built artifacts for `{DEV_VERSION}`, `DEV VERSION`, and `development build` in user-visible/runtime version locations; require zero matches.
- [ ] Confirm iOS, macOS, and Android package metadata matches each platform's own canonical version and build.

### Task 8: Release workflow and current-device delivery

- [ ] Add concise release instructions documenting `show`, one-time `bump patch`, `apply`, `check`, package build, and the rule that repeated builds never bump.
- [ ] For the current iOS repair, retain the initial `1.0.7`/build `7` established by this migration; do not bump a second time during verification.
- [ ] Rebuild and sign the iOS Release app with the configured Apple Development identity and provisioning profile.
- [ ] Install it on the connected iPad, launch it when unlocked, and inspect the main-menu/About version label.
- [ ] Record final test counts, artifact paths, package versions, compatibility digest, and per-repository commit IDs.

## Plan Self-Review

- Every design requirement maps to a task: independent versions (Tasks 1, 3–5), product/compatibility separation (Task 2), deterministic core fingerprint (Task 1), stable data paths (Task 6), build enforcement (Tasks 3–5), cross-platform proof (Task 7), and release usage (Task 8).
- The plan contains no deferred placeholders. All generated values originate from the canonical JSON manifest or computed compatibility inputs.
- The wire field remains `Version` while its value becomes `CompatibilityOrVersion`; product version is recorded separately as `ProductVersion`. These names remain consistent across all tasks.
