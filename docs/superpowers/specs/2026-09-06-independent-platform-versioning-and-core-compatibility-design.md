# NUKE HOUR Independent Platform Versioning and Core Compatibility Design

## Objective

Replace every user-visible development placeholder with a real NUKE HOUR product version and establish a repeatable release mechanism in which iOS, macOS, and Android advance independently. Application release numbers must not decide multiplayer compatibility. Multiplayer compatibility is determined by the deterministic game core and the selected map.

## Current Problem

The three working trees still expose `{DEV_VERSION}` through the RA2 manifest and `VersionLabelLogic`. iOS pins `CFBundleShortVersionString` to `1.0` and `CFBundleVersion` to `6`. Android pins `versionName` to `0.0.1` and `versionCode` to `1`. The macOS packager accepts an arbitrary tag but has no checked product-version source. None of the three has a single platform-local source of truth or a mandatory projection check.

The engine also uses `Manifest.Metadata.Version` for both presentation and network admission. That couples harmless platform-specific UI releases to multiplayer compatibility and is therefore the wrong boundary.

## Version Model

Each platform checkout owns one canonical `packaging/nukehour-version.json` document with these required fields:

- `schema`: integer schema version, initially `1`;
- `platform`: exactly `ios`, `macos`, or `android` for that checkout;
- `version`: a three-part SemVer product version such as `1.0.7`;
- `build`: a positive, monotonically increasing integer;
- `channel`: `stable` for distributable builds.

Initial versions are:

- iOS: `1.0.7`, build `7`;
- macOS: `1.0.1`, build `1`;
- Android: `0.0.2`, build `2`.

These sequences are independent. An iOS-only repair advances only iOS. Rebuilding the same revision is deterministic and does not advance any number.

## Version Command

Each checkout contains the same `packaging/nukehour_version.py` implementation. It provides:

- `show`: print the canonical platform, version, build, and channel;
- `bump patch`: increment the patch component and build exactly once, then update generated projections;
- `apply`: regenerate projections without incrementing, for recovery and review;
- `check`: perform no writes and fail if the manifest, generated projections, package metadata, or runtime labels disagree.

`bump patch` is the explicit boundary for one completed, distributable repair. Ordinary test builds never mutate the version. Every platform packaging entry point invokes `check`, so a stale or hand-edited projection cannot produce an artifact.

## Runtime Presentation

The mod metadata gains a user-facing display version distinct from its internal compatibility data. Main-menu, in-game, and About labels render the product value in this form:

`NUKE HOUR iOS 1.0.7 (Build 7)`

The platform word changes for macOS and Android. No distributable artifact may contain or display `{DEV_VERSION}`, `DEV VERSION`, or an equivalent development marker.

The canonical version is projected into:

- iOS application display version and bundle build number;
- macOS `CFBundleShortVersionString` and numeric `CFBundleVersion`;
- Android `versionName` and monotonically increasing integer `versionCode`;
- the RA2 display-version metadata consumed by all `VersionLabelLogic` labels;
- release artifact filenames where the existing packaging convention includes a version.

## Multiplayer Compatibility

Application version is never sent or compared as the multiplayer admission key.

The manifest gains a separate compatibility identifier. Existing non-NUKE-HOUR mods default to their current `Metadata.Version`, preserving backward compatibility. NUKE HOUR builds use a generated compatibility fingerprint derived from a checked, sorted list of deterministic inputs:

- engine network message and lockstep-simulation contracts;
- RA2 simulation traits and activities;
- gameplay rules, weapons, warheads, actors, and deterministic Lua scripts;
- compatibility-affecting manifest configuration.

Presentation-only material is excluded: UI layouts, touch controls, localization, brand text, artwork, audio, icons, packaging metadata, import screens, and platform host code that does not participate in deterministic simulation.

The input-list definition is version controlled and identical across the three platform implementations. Hashing uses normalized relative paths, exact file bytes, ordinal path ordering, and SHA-256. The generated compatibility identifier has a stable prefix and the complete digest. Builds fail on a missing input, an unexpected duplicate, an empty input set, or a stale projected fingerprint.

Client/server handshake and server-list compatibility checks compare this compatibility identifier. Selected maps retain their existing UID equality check. Replays and diagnostics record both product version and compatibility identifier so defects can be traced without conflating release numbering with simulation compatibility.

Changing only an application version or excluded presentation file leaves compatibility unchanged. Changing a listed core input changes compatibility and prevents unsafe mixed-core games, even if the visible application versions happen to match.

## Data and Upgrade Behavior

Save, replay, imported-content, and user-map storage paths must not move merely because a platform product version increments. Existing literal `{DEV_VERSION}` storage locations remain readable during migration. The implementation introduces a stable NUKE HOUR storage identity and performs non-destructive discovery of the legacy location; it does not delete or overwrite user data.

A compatibility change may make an old replay non-playable, but the replay remains visible and reports the recorded compatibility identifier instead of disappearing because an application patch changed its folder.

## Build Integration

iOS and Android MSBuild projects consume generated, checked version properties. Their checked-in plist or manifest files no longer carry conflicting hard-coded values. The macOS packager reads the canonical manifest, rejects a caller tag that disagrees, and writes distinct short and build versions into the app plist.

The iOS signed build and install workflow calls `check` before compilation. Android APK/AAB packaging calls `check` before compilation. macOS app/DMG packaging calls `check` before staging. A developer can still run engine unit tests without bumping or rewriting version state.

## Tests

Automated tests are added before production changes and cover:

- parsing valid platform manifests and rejecting malformed or mismatched values;
- `bump patch` incrementing patch and build once while preserving other fields;
- `apply` being idempotent;
- `check` detecting every stale package/runtime projection;
- distributable source and finished artifacts containing no development marker;
- main-menu and About labels using the product display version;
- product-version-only changes leaving the compatibility fingerprint unchanged;
- listed core-rule changes changing the fingerprint;
- UI, localization, brand, audio, and packaging changes not changing it;
- multiplayer comparison using compatibility identifiers rather than product versions;
- map UID mismatch still rejecting a game independently;
- legacy mods falling back to manifest version;
- legacy user-data locations remaining discoverable.

Final verification builds the iOS Release app, Android Release APK, and macOS application package far enough to inspect their native package metadata and bundled runtime labels. The three platform-focused test suites must report zero failures.

## Operational Rule Going Forward

For every completed repair intended for installation or distribution:

1. run that platform's `bump patch` once;
2. review the version-manifest and generated projection diff;
3. run platform tests and `check`;
4. build and inspect the artifact;
5. commit the repair and version increment together.

Documentation-only work, experiments, failed fixes, and repeated rebuilds do not consume a version number.
