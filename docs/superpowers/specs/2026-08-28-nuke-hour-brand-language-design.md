# NUKE HOUR Unified Branding and Language Design

**Date:** 2026-08-28

## Goal

Replace the user-visible product identity on every supported build with `NUKE HOUR`, use the user-provided square nuclear artwork as the common application icon, simplify the settings title to `GAME SETTINGS` / `游戏设置`, and add a main-menu language shortcut with reliable system-language matching.

The result must be consistent across iOS, PublicClean iOS, macOS, Windows, Linux, the macOS launcher, the game shell, installers, and packaged artifacts.

## Approved Product Decisions

- The visible product name is exactly `NUKE HOUR`.
- The supplied 1254×1254 RGB, opaque PNG is the sole icon source. It will not be redrawn or generatively altered.
- English settings buttons and headers display exactly `GAME SETTINGS`.
- Simplified Chinese settings buttons and headers display exactly `游戏设置`.
- The main menu exposes a three-state language selector: system automatic, Simplified Chinese, and English.
- A main-menu language change is saved first and then applied immediately by safely reloading the current mod in the same process.
- Automatic mode follows the operating system's preferred UI language on every launch.
- A manual Chinese or English choice persists and takes precedence over later system-language changes.

## Scope

### User-visible identity

The rename covers:

- iOS and PublicClean application display names, IPA names, SpringBoard icons, and startup placeholder text;
- macOS launcher and game application bundle names, Finder display names, Dock icons, launcher window text, command launcher, DMG title, and installer name;
- Windows and Linux package display names and installer/launcher names;
- the OpenRA mod title, game window title, branded load screen, main-menu subtitle, and prerelease notices;
- public-distribution legal notices that currently name `OpenRTS Mobile`;
- tests, packaging audits, and documentation that define the active product identity.

### Stable technical identity

The following identifiers remain unchanged to preserve upgrades, signing, settings, saves, and network compatibility:

- iOS and macOS bundle identifiers;
- Apple development team and provisioning configuration;
- OpenRA mod id `ra2`;
- settings, save, and support directories;
- Windows registry compatibility key;
- multiplayer protocol and service identifiers;
- internal executable names when they are not user-visible;
- internal `NUCLEAR-CRISIS-BG-*` wallpaper filenames, which contain no visible old-brand text.

Visible `.app`, `.command`, IPA, DMG, and installer filenames are not considered technical identifiers and will be renamed.

## Canonical Icon Pipeline

The exact attached 1254×1254 image is preserved as the versioned `branding/NUKE-HOUR-source-1254.png` source. A deterministic conversion produces the canonical `branding/NUKE-HOUR-1024.png` RGB master and every platform derivative:

1. Downsample the square source to 1024×1024 without cropping or adding rounded corners.
2. Normalize it to opaque sRGB.
3. Derive every iPhone and iPad AppIcon slot from that master.
4. Derive the separate PublicClean AppIcon catalog from the same master so the deny-by-default catalog boundary remains intact while the visible icon stays identical.
5. Derive the macOS iconset and `.icns` used by the launcher, the game bundle, and DMG volume.
6. Derive Windows/Linux packaging icons and `mods/ra2/icon.png` from the same master.

Generated icons must preserve the complete `NUKE HOUR` lettering, have no alpha channel where Apple forbids it, and declare the exact dimensions required by each asset catalog entry. Tests compare the two iOS catalogs and the macOS/packaging masters to prevent brand drift.

The supplied artwork is treated as a user-authorized project asset. `docs/legal/NUKE_HOUR_ICON_PROVENANCE.md` records the supplied-file origin, adoption date, original 1254×1254 dimensions, source hash, derived 1024 master hash, and the user's instruction to use it. Public release documentation continues to enforce the existing no-retail-content audit. Tests validate the immutable source hash separately from all generated dimensions and derivative hashes.

## Settings Naming

The RA2 Fluent override for `button-settings-title` becomes:

- English: `GAME SETTINGS`
- Simplified Chinese: `游戏设置`

The main-menu settings button and settings-panel title intentionally share this key, so neither location repeats `NUKE HOUR`. Other main-menu branding may continue to show `NUKE HOUR` as the product identity.

## Language Preference Model

### Stored preference

`Game.Language` becomes a preference token rather than an already-resolved locale:

- `System` — follow the current operating-system UI language;
- `zh-CN` — persistent manual Simplified Chinese;
- `en` — persistent manual English.

The default is `System`. Existing `en` and `zh-CN` settings remain valid and are interpreted as manual choices. A missing legacy setting migrates naturally to `System`.

This model fixes the existing ambiguity where a manual choice equal to the current system language is deleted as a default and later begins following the system unintentionally. Because `System` is the only default value, both manual values are always serialized.

### Effective language

A pure language-selection policy resolves the stored preference against an injected system-language tag:

- any `zh`, `zh-Hans`, `zh-CN`, or equivalent Chinese tag resolves to `zh-CN`;
- `en` and all unsupported or failed detections resolve to `en`;
- a manual `zh-CN` or `en` choice ignores the system tag;
- malformed saved preferences are normalized to the displayable `System` token while settings are loaded, with English as the final detection fallback.

Fluent initialization consumes the effective language, while UI selectors display and edit the stored preference.

## Platform System-Language Sources

- **iOS:** use the first entry from `NSLocale.PreferredLanguages` as the system-language input before game settings and Fluent initialization. Declare English and Simplified Chinese localizations so iOS per-app language selection is represented correctly.
- **macOS launcher:** read `AppleLanguages` first because it represents UI preference order; use `AppleLocale`, environment locale, and Python locale only as fallbacks.
- **Windows/Linux/macOS direct engine launch:** use `.NET CultureInfo.CurrentUICulture` when no explicit system-language input is provided.
- **Unsupported language:** use English.

The macOS launcher must stop hard-coding `zh-CN` and stop passing a resolved system language as if it were a manual `Game.Language` choice. It passes the raw preference and a separate system-language input, or omits the preference when the engine can load it from settings.

iOS also has native UI that appears before OpenRA settings and Fluent exist. Before creating that UI, a native-language resolver safely reads the persisted `Game.Language` token directly from the stable settings file and resolves it with `NSLocale.PreferredLanguages` through the same `System | zh-CN | en` policy. Missing or malformed settings behave as `System`. This localizes the content-import flow, license and legal prompts, progress/status messages, startup placeholder, and error/action buttons consistently with the user's saved game preference for both personal and PublicClean builds.

Localized `InfoPlist.strings` files provide English and Simplified Chinese `NSLocalNetworkUsageDescription` text. This operating-system permission prompt intentionally follows the iOS system/per-app language, because iOS renders it before application code and cannot use the game's internal manual override. No application-owned startup path may assume Chinese before the engine initializes.

## Main-Menu Quick Selector

The RA2 main menu adds a full-size language dropdown immediately below `GAME SETTINGS`. It uses the same width, typography, focus behavior, and touch-target policy as the existing menu buttons. The Content and Quit actions are moved down within the existing panel grid so all six controls remain contained, evenly spaced, and non-overlapping on every supported iPhone, iPad, and desktop layout.

The options are localized as:

- `Auto (System)` / `自动（跟随系统）`
- `简体中文`
- `English`

The button displays the current preference, not merely the current effective locale, so users can see whether the game is following the system.

Selection flow:

1. Capture the current effective language.
2. Update the stored preference.
3. Save settings.
4. Resolve the new effective language.
5. If the effective language changed, schedule an in-process reload of the current mod after the input tick.
6. If only the preference provenance changed, update the selector without reloading.

The selector exists only on the shell/main menu, so it never interrupts an active match, replay, editor session, or multiplayer lobby.

The existing Display settings language dropdown gains the same `System`, `zh-CN`, and `en` choices and shares the resolution policy. Its existing settings save/restart workflow remains valid; the main-menu shortcut is the immediate path.

## Safe Immediate Reload

Changing the Fluent bundle alone is insufficient because widgets and logic objects cache localized strings. The safe application path is:

- save the preference first;
- call `Game.InitializeMod` for the current mod through `Game.RunAfterTick`;
- allow the existing mod initialization path to clear UI, dispose the shell world and old Fluent bundle, recreate fonts and widgets, and load a fresh main menu.

This is an in-process mod reload, not a process restart. It works on iOS, where spawning a replacement process is not available, and avoids leaving a partially translated widget tree.

All language baselines must be scoped to a settings or mod session. In particular, the Display settings comparison baseline may not remain in a `static readonly` field across an in-process mod reload. A newly constructed settings panel captures the currently applied preference and effective language so reopening Settings after a main-menu language reload does not produce a false restart prompt.

If reloading cannot be scheduled, the saved preference remains valid and is applied on the next launch. System-language detection failures never prevent startup and fall back to English.

## macOS Launcher Behavior

The launcher uses the same preference semantics:

- first run defaults to `System`;
- its interface resolves from `AppleLanguages` and can render Chinese or English;
- its language control includes Auto, Simplified Chinese, and English;
- changing the launcher language rebuilds its Tk interface immediately;
- launching the game preserves the selected preference instead of forcing Chinese.

Existing launcher translations are connected to the live widget construction path. Remaining user-visible Chinese literals receive English entries so an English launcher page contains no mixed-language controls.

## PublicClean

PublicClean uses the `NUKE HOUR` display name and icon while retaining its isolated AppIcon catalog and deny-by-default resource audit. Legal text continues to state that the project is independent, ships no Electronic Arts content, and requires users to import legally owned data; only the product name changes.

No retail game asset becomes part of the public bundle. The public icon is generated from the user-provided canonical artwork, not from retail content.

The repository's pre-existing PublicClean application inventory is not yet a
releasable bundle: its current audits report many managed/runtime and RA2
resources that are unclassified or explicitly `public:false`. This branding
change must not reclassify those unrelated resources, weaken the deny-by-default
audit, or claim that a signed PublicClean IPA is releasable. Acceptance for this
feature covers the PublicClean source metadata, isolated catalog bytes, native
localization, and a scoped delta audit proving that every new or changed resource
has an accurate classification and leaves the same-RID audit error-path set
exactly unchanged. The existing
full-bundle provenance review remains a separately blocked release gate.

The fixed same-RID report must come from a complete clean application bundle,
not merely from a directory that passed the resource-brand audit. Its error set
must include the native executable, application and engine assemblies, core
runtime assemblies, Mono runtime library, and runtime configuration. Builds use
a fresh sibling checkout and normal relative `bin`/`obj` output. The fixed
pre-feature report uses repository commit `275a28f` with the verified
post-`0017` engine; candidates use the current source with the verified
post-`0018` engine. Absolute `UseArtifactsOutput` paths are forbidden for this
gate because the pinned .NET iOS SDK miscomputes publish destinations and can
silently produce an application directory without its executable or managed
runtime.

## Compatibility and Migration

- Existing users keep their settings, saves, imported data, and multiplayer compatibility.
- Existing `Language: en` and `Language: zh-CN` entries remain manual overrides.
- Users without a saved language begin in `System` mode.
- Renamed macOS application bundles do not change their bundle identifiers, so signing and preference continuity remain intact.
- Installer and launcher scripts recognize the new visible bundle names. Compatibility cleanup may recognize old bundle names, but it must not silently delete user data.
- No network protocol bump is required.

## Testing and Auditing

### Test-first language coverage

- normalize `zh-Hans`, `zh-Hant`, `zh_CN`, English, empty, malformed, and unsupported tags;
- prove manual preference overrides the system;
- prove `System` follows a changed system language on the next launch;
- prove a manual value equal to the current system language is still serialized;
- prove legacy `en` and `zh-CN` settings remain compatible;
- prove system detection failure falls back to English;
- prove iOS uses preferred UI languages;
- prove application-owned pre-Fluent iOS import, legal, status, startup, error, and action strings honor manual preference first and otherwise follow `NSLocale.PreferredLanguages`;
- prove the network-permission description follows iOS system/per-app localization independently of the game preference;
- prove macOS prioritizes `AppleLanguages` over `AppleLocale`;
- prove the launcher no longer forces `Game.Language=zh-CN`;
- prove main-menu selection saves before reload and avoids reload when the effective language is unchanged;
- prove opening Settings, returning to the main menu, switching language, reloading, and reopening Settings does not report a stale restart requirement;
- prove the main-menu and Display settings both expose all three options.

### Branding and icon coverage

- exact `NUKE HOUR` display names in iOS, PublicClean, macOS, Windows/Linux packaging, mod metadata, load screen, launcher, and legal copy;
- exact `GAME SETTINGS` and `游戏设置` Fluent values, with no product-name prefix;
- no active user-visible `NUCLEAR CRISIS` or `OpenRTS Mobile` strings;
- canonical icon dimensions, RGB color model, opacity, and deterministic derivatives;
- identical visual source across personal iOS, PublicClean, macOS, Windows/Linux, and mod icons;
- renamed `.app`, `.command`, IPA, DMG, and installer outputs reference valid internal paths.

### Final artifact gates

- full C# and Python test suites;
- YAML and Fluent reference checks;
- engine Release builds;
- signed iOS personal build plus source/simulator verification of the PublicClean
  title, isolated icon catalog, and native localization;
- iOS loading-brand and touch-runtime audits, the scoped PublicClean delta audit,
  and an explicit unchanged-baseline report from a complete full PublicClean app;
- macOS launcher and game bundle build, signing, and strict `codesign` verification;
- package branding tests for macOS, Windows, and Linux;
- clean-install icon verification to avoid SpringBoard, Finder, or Dock cache false positives.

## Engine Patch Delivery

Engine changes are delivered as a new `0018` patch with exactly these ten headers:

1. `OpenRA.Game/LanguageSelectionPolicy.cs`
2. `OpenRA.Game/Settings.cs`
3. `OpenRA.Game/FluentProvider.cs`
4. `OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs`
5. `OpenRA.Mods.Common/Widgets/Logic/Settings/DisplaySettingsLogic.cs`
6. `OpenRA.Test/LanguageSelectionPolicyTest.cs`
7. `OpenRA.Test/SettingsTest.cs`
8. `OpenRA.Test/PublicBrandingPolicyTest.cs`
9. `OpenRA.Test/IosMenuLayoutPolicyTest.cs`
10. `OpenRA.Test/IosSettingsLayoutTest.cs`

Historical patches are not rewritten. The patch baseline `a/` is a fresh official `release-20250330` source extraction followed by ordered application of `0001` through `0017`; it is never pristine upstream alone and never the dirty current worktree. Candidate `b/` differs from that post-`0017` baseline only at the ten allowlisted paths.

Root `mods/ra2`, iOS, launcher, packaging, artwork, legal, documentation, application bundles, `obj`, `bin`, private artifacts, and all unrelated files are forbidden from `0018`. The final replay must apply `0018` with zero fuzz or offset, match all ten current approved engine paths byte-for-byte, preserve every other tracked engine path, reject duplicate application without changing the tree, and pass reverse dry-run validation.

## Non-goals

- Renaming bundle identifiers, support directories, save paths, registry compatibility keys, the `ra2` mod id, or multiplayer services.
- Adding languages beyond Simplified Chinese and English.
- Hot-switching language during an active game, lobby, replay, or editor session.
- Rewriting the supplied icon artwork or changing the existing menu background artwork.
- Renaming internal wallpaper files solely because their filenames contain the previous brand.
- Resolving or overriding the pre-existing full PublicClean managed/resource
  provenance inventory; a signed distributable PublicClean IPA remains blocked
  until that separate legal classification work is completed.
