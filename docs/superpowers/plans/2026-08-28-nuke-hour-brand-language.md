# NUKE HOUR Unified Branding and Language Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the visible product identity `NUKE HOUR` and the supplied icon on every supported platform, simplify Settings to `GAME SETTINGS` / `游戏设置`, and add a persistent `System | zh-CN | en` language selector that safely refreshes the main menu in-process.

**Architecture:** The engine stores a language preference token separately from the effective locale and resolves it through one pure policy. Main-menu and Display settings share that policy, while iOS and macOS inject their preferred system UI language without converting it into a manual override. A deterministic Pillow-based generator turns the versioned 1254×1254 source into every PNG and ICNS derivative; packaging audits lock names, hashes, localizations, and the exact ten-file `0018` engine patch boundary.

**Tech Stack:** C# / .NET 8, OpenRA MiniYaml and Fluent, UIKit / .NET iOS, Python 3.11 with Pillow 11.3.0, Tkinter, Apple asset catalogs and `iconutil`, NUnit, Python `unittest`, shell packaging scripts.

---

## Fixed inputs and invariants

- Source artwork: `/var/folders/7n/f7n6bv290dj9lbz5yr2wk4pw0000gn/T/codex-clipboard-7e1dbe1f-07b3-4295-84f4-182c0e0297c8.png`
- Source SHA-256: `af3a739efcd5eefd02c0a622b60dcb6a88cd52b224813f53df8284b60a19e99b`
- Source contract: 1254×1254 PNG, RGB, opaque, no crop, no generative redraw.
- Visible product name: `NUKE HOUR`.
- Settings title: English `GAME SETTINGS`; Simplified Chinese `游戏设置`.
- Stored language tokens in UI order: `System`, `zh-CN`, `en`.
- Stable identifiers that must not change: bundle identifiers, Apple team/provisioning, mod id `ra2`, settings/save/support paths, Windows registry key `OpenRARA2Mod`, network protocol, internal game host `NuclearCrisis`, and `NUCLEAR-CRISIS-BG-*` wallpaper filenames.
- Preserve every unrelated dirty or untracked file. Never stage `artifacts/`, `docs/testing/results/`, `__pycache__`, `_CodeSignature`, retail content, `obj`, or `bin`.
- The full PublicClean app audit is already blocked by unrelated inventory. Do not reclassify unrelated resources or bypass the audit. This feature must prove its changed-resource delta is fully classified and does not change the same-RID error set; a distributable signed PublicClean IPA remains a separately blocked release gate.
- The canonical pre-feature diagnostic uses repository commit `275a28f`, the verified official-ZIP-plus-`0001`…`0017` engine, `Release`, `PublicClean=true`, and `iossimulator-arm64` in a fresh sibling checkout on a normal, non-aliased filesystem path. Use the project's normal relative `bin`/`obj` output. Preserve the complete-app report (SHA-256 `db6a7e5b3ee9237994b0e5dcbc9cf8129ccf1e02acb798a1b4f3aaee576c8221`; 580 errors: 488 `unclassified-resource`, 92 `not-public`; exactly three public `nc-impact-*.png` files; zero retail extensions, `.DS_Store`, or personal `mods/cnc`/`mods/ts`/`VERSION`/`glsl` paths) and version those exact 72,226 bytes as the Task 8 fixture. Candidates use the current source plus the verified post-`0018` engine. Candidate and baseline must use the same RID/configuration/toolchain and have exactly equal `(code,path)` sets. The audit additionally requires complete-app sentinels such as `OpenRA.iOS`, its AOT data, the application/engine assemblies, `System.Private.CoreLib.dll`, `libmonosgen-2.0.dylib`, and `runtimeconfig.bin`. Never use either the contaminated incremental report (`498289df…`, 3234 errors) or the superseded absolute-`ArtifactsPath` report (`0ecd86dc…`, 183 errors), which omitted the executable and runtime because of a pinned .NET iOS SDK publish-path defect.

## Exact staging protocol for every task

Each task's **Files** list is its staging allowlist. Before every commit, write
those exact expanded paths to `/private/tmp/nuke-hour-task-N.paths`, stage with:

```bash
git add --pathspec-from-file=/private/tmp/nuke-hour-task-N.paths
git diff --cached --name-only --no-renames > /private/tmp/nuke-hour-task-N.actual
cmp /private/tmp/nuke-hour-task-N.paths /private/tmp/nuke-hour-task-N.actual
git diff --cached --check
```

Keep both lists byte-sorted. `--no-renames` deliberately exposes both the deleted
old path and added new path so rename comparisons remain exact. Rename tasks list
each tracked old/new file literally and use `git add -A
--pathspec-from-file=...`; never use an application directory as a recursive
pathspec. Binary/generator tasks also reject staged
files over 50 MB and any `_CodeSignature`, `obj`, `bin`, retail, cache, or artifact
path. Never use `git add -A` without the restricted pathspec file.

## Engine patch `0018` — exact ten paths

1. Create `engine/OpenRA.Game/LanguageSelectionPolicy.cs`.
2. Modify `engine/OpenRA.Game/Settings.cs`.
3. Modify `engine/OpenRA.Game/FluentProvider.cs`.
4. Modify `engine/OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs`.
5. Modify `engine/OpenRA.Mods.Common/Widgets/Logic/Settings/DisplaySettingsLogic.cs`.
6. Create `engine/OpenRA.Test/LanguageSelectionPolicyTest.cs`.
7. Modify `engine/OpenRA.Test/SettingsTest.cs`.
8. Modify `engine/OpenRA.Test/PublicBrandingPolicyTest.cs`.
9. Modify `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs`.
10. Modify `engine/OpenRA.Test/IosSettingsLayoutTest.cs`.

No other `engine/` path may enter `0018`.

---

### Task 1: Add the pure engine language-selection policy

**Files:**
- Create: `engine/OpenRA.Test/LanguageSelectionPolicyTest.cs`
- Create: `engine/OpenRA.Game/LanguageSelectionPolicy.cs`

- [ ] **Step 1: Write the failing policy tests**

Create parameterized NUnit coverage for this public contract:

```csharp
public static class LanguageSelectionPolicy
{
	public const string SystemPreference = "System";
	public const string SimplifiedChinese = "zh-CN";
	public const string English = "en";
	public static IReadOnlyList<string> SupportedPreferences { get; }
	public static string NormalizePreference(string preference);
	public static string Resolve(string preference, string systemLanguageTag);
	public static string GetDisplayName(string preference, string uiLanguage);
	public static bool RequiresReload(
		string appliedLanguage,
		string preference,
		string systemLanguageTag);
}
```

Cover `null`, empty, whitespace, `System`, case variants, unknown tokens, `zh`, `zh-Hans`, `zh-Hant`, `zh_CN`, `en-US`, unsupported tags, manual-over-system precedence, exact `Auto (System)` / `自动（跟随系统）`, and same-effective-language no-reload.

- [ ] **Step 2: Run RED**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter FullyQualifiedName~LanguageSelectionPolicyTest -v minimal
```

Expected: compile failure because `LanguageSelectionPolicy` does not exist.

- [ ] **Step 3: Implement the minimal pure policy**

Use an immutable preference array in the required order. Normalize tokens case-insensitively to the three canonical values. Normalize `_` to `-`; any system tag whose first language subtag is `zh` resolves to `zh-CN`, and all other/invalid tags resolve to `en`. The class must not read environment, settings, Fluent, or widgets.

- [ ] **Step 4: Run GREEN and the core build**

Run the focused test, then:

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Game/OpenRA.Game.csproj -c Release --no-restore -m:1
```

Expected: test and build exit 0.

- [ ] **Step 5: Commit Task 1**

```bash
git add -f -- engine/OpenRA.Game/LanguageSelectionPolicy.cs engine/OpenRA.Test/LanguageSelectionPolicyTest.cs
git commit -m "Add language preference policy"
```

---

### Task 2: Separate stored preference from effective Fluent language

**Files:**
- Modify: `engine/OpenRA.Test/SettingsTest.cs`
- Modify: `engine/OpenRA.Game/Settings.cs`
- Modify: `engine/OpenRA.Game/FluentProvider.cs`

- [ ] **Step 1: Add failing Settings persistence tests**

Use a unique temporary settings path per test and cover:

```text
missing file                      -> Game.Language == System
Language: broken                 -> normalized to System
Language: en / zh-CN             -> preserved as manual values
Engine.SystemLanguage=zh-Hans    -> Settings.SystemLanguageTag == zh-Hans
manual en with English system    -> Language: en remains serialized
manual zh-CN with Chinese system -> Language: zh-CN remains serialized
System                           -> omitted as default
SystemLanguageTag                -> never serialized
```

Add a source-contract assertion that `FluentProvider.Initialize` resolves the stored preference, uses one resolved value for culture and override paths, and updates `CurrentLanguage` only when the new bundle is installed.

- [ ] **Step 2: Run RED**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~SettingsTest|FullyQualifiedName~LanguageSelectionPolicyTest' \
  -v minimal
```

Expected: the old dynamic default and missing system/current-language properties fail.

- [ ] **Step 3: Implement Settings semantics**

Replace the field and add a non-section startup property:

```csharp
[Desc("Language preference for in-game text: System, zh-CN, or en.")]
public string Language = LanguageSelectionPolicy.SystemPreference;

public string SystemLanguageTag { get; }
```

Read `Engine.SystemLanguage`; when absent, safely read `CultureInfo.CurrentUICulture.Name`. After file and command-line loading, canonicalize `Game.Language`. Do not add `SystemLanguageTag` to `Sections`.

- [ ] **Step 4: Resolve Fluent once per initialization**

Add:

```csharp
public static string CurrentLanguage { get; private set; } =
	LanguageSelectionPolicy.English;
```

Compute the effective language from the stored preference and `SystemLanguageTag`; use it for culture and localized override paths, construct the bundle, then assign `CurrentLanguage`.

- [ ] **Step 5: Run GREEN and Release builds**

Run the focused fixtures, then build `OpenRA.Game` and `OpenRA.Mods.Common` Release with `--no-restore -m:1`.

- [ ] **Step 6: Commit Task 2**

```bash
git add -- engine/OpenRA.Game/Settings.cs engine/OpenRA.Game/FluentProvider.cs engine/OpenRA.Test/SettingsTest.cs
git commit -m "Persist system-aware language preferences"
```

---

### Task 3: Add the aligned main-menu selector and fix the Display baseline

**Files:**
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Settings/DisplaySettingsLogic.cs`
- Modify: `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs`
- Modify: `engine/OpenRA.Test/IosSettingsLayoutTest.cs`
- Modify: `engine/OpenRA.Test/PublicBrandingPolicyTest.cs`
- Modify: `mods/ra2/chrome/mainmenu.yaml`
- Modify: `mods/ra2/fluent/chrome.ftl`
- Modify: `mods/ra2/fluent/zh-CN/chrome.ftl`
- Modify: `packaging/public-content-manifest.json`
- Modify: `docs/legal/public-asset-sbom.json`

- [ ] **Step 1: Write failing menu behavior and geometry tests**

Require this test seam:

```csharp
public static bool ApplyLanguageSelection(
	GameSettings settings,
	string systemLanguageTag,
	string appliedLanguage,
	string selectedPreference,
	Action save,
	Action scheduleReload);
```

Prove the new preference is visible inside `save`, callback order is exactly `save` then `scheduleReload`, different effective locales reload, and `System(zh) ↔ zh-CN` only saves. Require `GetOrNull<DropDownButtonWidget>` so mods without the RA2 control still load.

Parse the YAML and assert six controls have equal `X=28`, `Width=264`, `Height=46`; Y values exactly `36,92,148,204,260,316`; equal 10-pixel gaps; and the last control remains within the 420-pixel panel.

- [ ] **Step 2: Write failing Display-session tests**

Require removal of `static readonly string OriginalLanguage`, per-instance capture of stored preference and `FluentProvider.CurrentLanguage`, and one `LanguageRequiresRestart` method used by both restart-description visibility and panel-leave result. Prove a newly built panel after main-menu reload has no stale warning.

- [ ] **Step 3: Run RED**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~IosMenuLayoutPolicyTest|FullyQualifiedName~IosSettingsLayoutTest|FullyQualifiedName~PublicBrandingPolicyTest' \
  -v minimal
```

- [ ] **Step 4: Implement save-before-reload**

Normalize, assign, save, resolve, and schedule only if the target locale differs from the applied locale. Bind the optional dropdown and schedule:

```csharp
var currentMod = modData.Manifest.Id;
Game.RunAfterTick(() => Game.InitializeMod(currentMod, new Arguments()));
```

- [ ] **Step 5: Implement the per-session Display baseline**

Keep static graphics baselines, but replace the language static with:

```csharp
readonly string originalLanguagePreference;
readonly string originalEffectiveLanguage;
```

Build the dropdown from `SupportedPreferences`, use `GetDisplayName`, and compare target effective locale with the captured applied locale.

- [ ] **Step 6: Update the RA2 menu and exact labels**

Use `SETTINGS_BUTTON` at Y 148, `LANGUAGE_DROPDOWN` at 204, `CONTENT_BUTTON` at 260, and `QUIT_BUTTON` at 316. Set the shared Fluent key to exact `GAME SETTINGS` and `游戏设置`.

- [ ] **Step 7: Run GREEN and YAML checks**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~IosMenuLayoutPolicyTest|FullyQualifiedName~IosSettingsLayoutTest|FullyQualifiedName~PublicBrandingPolicyTest' \
  -v minimal
./utility.sh --check-yaml
python3 -m unittest packaging.tests.test_public_bundle_audit -v
git diff --check
```

Refresh the existing exact manifest/SBOM entries for `mods/ra2/chrome/mainmenu.yaml`
and both changed `chrome.ftl` files in the same commit. Do not add a broad
resource exemption.

- [ ] **Step 8: Commit Task 3**

Stage exactly all ten expanded paths in this task's **Files** allowlist using
the exact staging protocol, show the cached list, then commit:

```bash
git commit -m "Add quick language selection to the main menu"
```

---

### Task 4: Replace active product branding and legal copy

**Files:**
- Modify: `engine/OpenRA.Test/PublicBrandingPolicyTest.cs`
- Modify: `README.md`
- Modify: `mod.config`
- Modify: `mods/ra2/mod.yaml`
- Modify: `mods/ra2/fluent/{,zh-CN/}mod.ftl`
- Modify: `mods/ra2/fluent/{,zh-CN/}chrome.ftl`
- Modify: `OpenRA.Mods.RA2/LoadScreens/BrandSplashLoadScreen.cs`
- Modify: `mods/ra2-public/fluent/{,zh-CN/}legal.ftl`
- Modify: `docs/legal/PUBLIC_DISTRIBUTION.md`
- Modify: `docs/legal/THIRD_PARTY_CONTENT.md`
- Modify: `packaging/tests/test_macos_branding.py`
- Modify: `packaging/public-content-manifest.json`
- Modify: `docs/legal/public-asset-sbom.json`

- [ ] **Step 1: Turn active engine/root branding assertions RED**

Require the RA2 `mod-title`, `mod-windowtitle`, load-screen title, README title,
`mods/ra2/mod.yaml` `ModTabTitle`, active legal product name, and exact
`GAME SETTINGS` / `游戏设置` values to use
the new identity. Explicitly allow only internal `NUCLEAR-CRISIS-BG-*` wallpaper
filenames and stable `NuclearCrisis` game-host executable. Keep the existing
iOS and installer expectations unchanged in this task; Tasks 6 and 8 turn those
platform-specific assertions RED and GREEN atomically with their implementations.
Update only `test_macos_branding` assertions affected by `mod.config` and current
visible title metadata; keep bundle/icon assertions for Tasks 5 and 7.

- [ ] **Step 2: Run RED**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter FullyQualifiedName~PublicBrandingPolicyTest -v minimal
python3 -m unittest packaging.tests.test_macos_branding -v
```

- [ ] **Step 3: Apply exact visible metadata**

Use these values and preserve the registry key:

```sh
PACKAGING_INSTALLER_NAME="NUKE-HOUR"
PACKAGING_DISPLAY_NAME="NUKE HOUR"
PACKAGING_MACOS_DISPLAY_NAME="NUKE HOUR"
PACKAGING_MACOS_INSTALLER_NAME="NUKE-HOUR"
PACKAGING_WINDOWS_LAUNCHER_NAME="NUKE-HOUR"
PACKAGING_WINDOWS_INSTALL_DIR_NAME="NUKE HOUR"
PACKAGING_WINDOWS_REGISTRY_KEY="OpenRARA2Mod"
```

Update active mod, menu, load-screen, README, and public legal names. Do not rewrite historical plans/specs.

- [ ] **Step 4: Run GREEN, public-resource audit, and the active-string scan**

Refresh the existing exact manifest/SBOM entries for `mods/ra2/mod.yaml`, both
changed `mod.ftl` files, both changed `chrome.ftl` files, and any changed public
legal files. Preserve their current honest provenance/public decisions; a new
brand string does not prove the surrounding inherited file publicly
redistributable. Run the focused C# test plus `python3 -m unittest
packaging.tests.test_public_bundle_audit packaging.tests.test_macos_branding -v`.
Update the macOS fixture's `mod.config`/visible-title assertions here; defer only
bundle/icon assertions whose resources have not changed. Limit this task's
stale-brand scan to the Task 4 file allowlist. The complete
cross-platform active-path scan runs only after Tasks 6–8 have implemented their
own platform names.

- [ ] **Step 5: Commit Task 4**

Expand the two Fluent brace entries into four literal paths, stage exactly this
task's **Files** allowlist with the common protocol, show the cached list, and
commit:

```bash
git commit -m "Rename the product to NUKE HOUR"
```

---

### Task 5: Create the deterministic icon pipeline and replace every icon

**Files:**
- Create: `branding/NUKE-HOUR-source-1254.png`
- Create: `branding/NUKE-HOUR-1024.png`
- Create: `packaging/generate_nuke_hour_icons.py`
- Create: `packaging/tests/test_nuke_hour_icon_pipeline.py`
- Create: `docs/legal/NUKE_HOUR_ICON_PROVENANCE.md`
- Modify: `engine/OpenRA.Test/PublicBrandingPolicyTest.cs`
- Modify: `packaging/tests/test_macos_branding.py`
- Modify: `launcher.py`
- Modify generated: both `ios/OpenRA.iOS/Assets.xcassets/*AppIcon.appiconset` directories
- Modify generated: `packaging/artwork/icon_*.png`, `mods/ra2/icon.png`, `launcher_assets/icon.icns`
- Modify generated: macOS launcher/game app ICNS resources
- Modify: `packaging/public-content-manifest.json`
- Modify: `docs/legal/public-asset-sbom.json`

- [ ] **Step 1: Write failing generator tests**

The CLI contract is:

```text
python3 packaging/generate_nuke_hour_icons.py --root <repo> --source <png>
python3 packaging/generate_nuke_hour_icons.py --root <repo> --check
python3 packaging/generate_nuke_hour_icons.py --root <repo> --list-outputs
python3 packaging/generate_nuke_hour_icons.py --root <repo> --list-mutations
```

Tests use a temporary root and assert immutable source hash/dimensions/mode, deterministic 1024 master, exact 18 catalog slots backed by 15 unique PNGs, RGB/no alpha, byte-identical personal/PublicClean corresponding outputs, packaging sizes `16,24,32,48,64,128,256,512,1024`, 32×32 mod icon, identical ICNS bytes, and `--check` failure on missing/extra/tampered output. In the same RED step, update `PublicBrandingPolicyTest` and `test_macos_branding` to require NUKE-HOUR catalog names, equal personal/public bytes, and the new canonical macOS icon consumers.
`--list-outputs` prints the sorted exact repository-relative derivative allowlist
and rejects any output outside the approved catalogs, artwork, mod-icon,
launcher-icon, and two macOS bundle resource paths. `--list-mutations` prints the
sorted union of desired outputs and every tracked legacy icon path that the
generator removes, including both catalogs' old `NUCLEAR-CRISIS-*`/`AppIcon-*`
PNGs. Task 5 stages the mutation list so deletions cannot be omitted.

Update `launcher.py` in this same task to load the canonical
`branding/NUKE-HOUR-1024.png`. The old catalog marketing PNG is a managed legacy
output and must not remain as a hidden compatibility alias.

- [ ] **Step 2: Run RED**

```bash
python3 -m unittest packaging.tests.test_nuke_hour_icon_pipeline -v
python3 -m unittest packaging.tests.test_macos_branding -v
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter FullyQualifiedName~PublicBrandingPolicyTest -v minimal
```

Expected: missing generator/module plus old icon/catalog assertions fail.

- [ ] **Step 3: Version the exact source**

Copy the attachment byte-for-byte with `ditto`. Verify:

```bash
shasum -a 256 branding/NUKE-HOUR-source-1254.png
```

Expected digest: `af3a739efcd5eefd02c0a622b60dcb6a88cd52b224813f53df8284b60a19e99b`.

- [ ] **Step 4: Implement deterministic generation**

Fail closed unless Pillow is exactly `11.3.0`. Strip metadata, convert to opaque RGB/sRGB, resize every output from the 1024 master with `Image.Resampling.LANCZOS`, and save with fixed PNG parameters. Build one temporary iconset, call `iconutil -c icns`, and copy identical bytes to all ICNS consumers. `--check` must regenerate in a temporary directory and compare paths/bytes without writing the worktree.

- [ ] **Step 5: Generate assets and provenance**

Both AppIcon catalogs remain physically separate but use `NUKE-HOUR-*` filenames and matching bytes. Record attachment origin, date, dimensions, source/master hashes, conversion command, Pillow version, and user-authorized redistribution; do not claim retail or project-original provenance.

- [ ] **Step 6: Run GREEN**

```bash
python3 -m unittest packaging.tests.test_nuke_hour_icon_pipeline -v
python3 -m unittest packaging.tests.test_public_bundle_audit \
  packaging.tests.test_macos_branding -v
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter FullyQualifiedName~PublicBrandingPolicyTest -v minimal
python3 packaging/generate_nuke_hour_icons.py --check
```

Refresh the existing exact manifest/SBOM entry for `mods/ra2/icon.png` in this
same commit; the icon commit must not leave the public audit broken.

- [ ] **Step 7: Commit Task 5**

Combine all nine literal non-generated paths in this task's **Files** list with
the generator's `--list-mutations` result into the exact sorted pathspec. The
mutation list includes `branding/NUKE-HOUR-1024.png` plus desired and deleted
legacy derivatives. Show the complete staged list, prove no staged file exceeds
50 MB, assert no legacy managed icon remains after generation, and commit:

```bash
git commit -m "Generate unified NUKE HOUR icons"
```

---

### Task 6: Unify iOS/PublicClean branding and pre-Fluent localization

**Files:**
- Create: `ios/OpenRA.iOS/IosNativeLanguage.cs`
- Create: `ios/OpenRA.iOS/en.lproj/InfoPlist.strings`
- Create: `ios/OpenRA.iOS/zh-Hans.lproj/InfoPlist.strings`
- Modify: `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Modify: `ios/OpenRA.iOS/Info.plist`
- Modify: `ios/OpenRA.iOS/AppDelegate.cs`
- Modify: `ios/OpenRA.iOS/ViewController.cs`
- Modify: `ios/OpenRA.iOS/IosContentImportCoordinator.cs`
- Modify: `ios/scripts/build-public-clean.sh`
- Create: `packaging/ios_app_brand_audit.py`
- Create: `packaging/tests/test_ios_app_brand_audit.py`
- Modify: `engine/OpenRA.Test/PublicBrandingPolicyTest.cs`
- Modify: `packaging/public-content-manifest.json`
- Modify: `docs/legal/public-asset-sbom.json`

- [ ] **Step 1: Write failing iOS brand/native-language tests**

Require both build modes to declare `ApplicationTitle=NUKE HOUR`; personal IPA `NUKE HOUR.ipa`; public IPA `NUKE HOUR-PublicClean.ipa`; unchanged application IDs; separate AppIcon catalogs with equal images; `CFBundleDevelopmentRegion=en`; and `CFBundleLocalizations=[en,zh-Hans]`.

Lock the brand-audit CLI as `python3 packaging/ios_app_brand_audit.py
<app-bundle> --source-root <repo>`. It must inspect the built plist and compiled
icon metadata/bytes without mutating the app.

Source contracts must prove native code reads only `Game.Language` from the stable settings file, treats missing/malformed as `System`, uses the first `NSLocale.PreferredLanguages` entry, passes the raw tag as `Engine.SystemLanguage`, and routes wizard/license/status/action/error text through one selected string table. Preserve the existing valid Chinese `NSLocalNetworkUsageDescription` fallback literal in the base plist so `IosMultiplayerPolicyTest` remains meaningful; English and Chinese `.lproj` resources control the actual OS-owned prompt.

- [ ] **Step 2: Run RED**

```bash
python3 -m unittest packaging.tests.test_ios_app_brand_audit -v
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~PublicBrandingPolicyTest|FullyQualifiedName~IosContentImportContractTest|FullyQualifiedName~IosMultiplayerPolicyTest' \
  -v minimal
```

- [ ] **Step 3: Implement native resolver and strings**

`IosNativeLanguage` only parses the stable settings token, obtains the preferred
system tag, and resolves the effective native language. Keep the complete
immutable English/Chinese native string table in `ViewController.cs`, where the
existing immutable engine contract test already verifies the Chinese action
literals; do not move those literals into the new file and do not expand the
ten-path `0018` allowlist. `AppDelegate` selects that table before constructing
`ViewController` and injects it into the PublicClean coordinator; engine launch
receives the system tag separately. Map `RetailImportError` to localized user
text and keep raw details in diagnostics.

- [ ] **Step 4: Add OS-owned permission localization**

Keep a valid plist fallback and add exact English/Chinese `NSLocalNetworkUsageDescription` values to the two `.lproj` files. These follow iOS system/per-app language, not the game's manual preference.

- [ ] **Step 5: Wire build and brand-audit order**

Include both `.lproj` resources unconditionally. Run `AuditIosApplicationBrand` after `_CreateAppBundle` and before codesign; make personal and PublicClean audits depend on it. Preserve IDs, provisioning, Bonjour services, and deny-by-default catalog selection.

- [ ] **Step 6: Run GREEN, public-resource audit, and plist checks**

```bash
python3 -m unittest packaging.tests.test_ios_app_brand_audit -v
python3 -m unittest packaging.tests.test_public_bundle_audit -v
plutil -lint ios/OpenRA.iOS/Info.plist \
  ios/OpenRA.iOS/en.lproj/InfoPlist.strings \
  ios/OpenRA.iOS/zh-Hans.lproj/InfoPlist.strings
```

Add only the two exact `.lproj/InfoPlist.strings` manifest/SBOM classifications
in this task. Do not defer their classification to Task 8 and do not add a
directory-wide `.lproj` exemption.

- [ ] **Step 7: Commit Task 6**

Stage exactly the expanded Task 6 **Files** allowlist with the common protocol;
show the cached list and prove neither `GeneratedAotObjectRegistry.g.cs` nor any
build/signature output entered it.

```bash
git commit -m "Localize and rebrand the iOS application"
```

---

### Task 7: Rebrand and localize the macOS launcher

**Files:**
- Modify: `launcher_i18n.py`
- Modify: `launcher.py`
- Modify: `packaging/generate_nuke_hour_icons.py`
- Modify: `packaging/tests/test_nuke_hour_icon_pipeline.py`
- Modify: `packaging/tests/test_macos_branding.py`
- Create: `packaging/tests/test_launcher_language.py`
- Rename: `NUCLEAR CRISIS.app` → `NUKE HOUR.app`
- Rename: `NUCLEAR CRISIS GAME.app` → `NUKE HOUR GAME.app`
- Rename: `NUCLEAR CRISIS.command` → `NUKE HOUR.command`
- Modify: both renamed app `Contents/Info.plist` files and icon resources

- [ ] **Step 1: Write failing launcher-language tests**

Test these pure semantics:

```python
normalize_preference(None) == "System"
resolve_language("System", "zh-Hans-CN") == "zh-CN"
resolve_language("en", "zh-Hans-CN") == "en"
resolve_language("zh-CN", "en-US") == "zh-CN"
```

Mock `defaults` and prove `AppleLanguages` wins over conflicting `AppleLocale`; environment/locale are fallbacks. Prove absent YAML gives `System`, manual values persist, launch arguments contain raw `Game.Language=<preference>` and `Engine.SystemLanguage=<tag>`, and no code forces Chinese. Add AST/string coverage requiring every active launcher UI literal to have an English translation.

- [ ] **Step 2: Run RED**

```bash
python3 -m unittest packaging.tests.test_launcher_language packaging.tests.test_macos_branding -v
```

- [ ] **Step 3: Connect `launcher_i18n` and the three-state control**

Use one strategy source:

```python
PREFERENCES = (("System", "System"), ("简体中文", "zh-CN"), ("English", "en"))
normalize_preference(value)
detect_system_language_tag()
resolve_language(preference, system_tag)
translate(text, effective_language)
```

Set launcher defaults to `System`. Persist a changed preference atomically, update translation state, convert enum-label variables through semantic values, destroy/rebuild widgets while preserving current values/page, and never spawn a replacement process merely to change launcher language.

- [ ] **Step 4: Translate active launcher surfaces**

Route factories, direct labels, menus, dialogs, status text, and dynamic templates through the translation helper. English mode may retain only explicit proper nouns such as `简体中文`.

- [ ] **Step 5: Rename bundles while preserving technical IDs and user files**

Before any rename, record a sorted path/SHA-256 inventory of the two existing
untracked `_CodeSignature` directories and move only those directories into a
recoverable, timestamped `artifacts/pre-nuke-hour-signatures/` archive. These are
generated signatures invalidated by the authorized bundle rename; preserve their
bytes and inventory but do not recreate the old branded `.app` directories.
Rename the tracked bundle contents and command. The archive remains untracked,
is reported to the user, and never enters the new bundle or index. Update
visible plist names, paths, command script, and icon resource references in the
renamed tracked bundles. Set both bundle plist display/name values to exact
`NUKE HOUR`; keep `CFBundleIdentifier`, `ra2launcher`, and `NuclearCrisis`
unchanged. Fresh signatures for the new bundles are produced only by the later
signing step and are never staged.

In the same change, replace the generator's two old bundle ICNS consumers with
the renamed bundle resource paths and update its managed mutation set/tests.
The generator must no longer reference or recreate either old branded app root.

- [ ] **Step 6: Run GREEN and syntax checks**

```bash
python3 -m unittest packaging.tests.test_launcher_language packaging.tests.test_macos_branding -v
python3 -m unittest packaging.tests.test_nuke_hour_icon_pipeline -v
python3 packaging/generate_nuke_hour_icons.py --check
python3 -m py_compile launcher.py launcher_i18n.py
sh -n 'NUKE HOUR.command'
plutil -lint 'NUKE HOUR.app/Contents/Info.plist' 'NUKE HOUR GAME.app/Contents/Info.plist'
```

- [ ] **Step 7: Commit Task 7**

Use `git add -A --pathspec-from-file=...` with each tracked old/new bundle file
and old/new command file listed literally, plus launcher sources, tests, and icon
resources. Do not use a bundle-directory pathspec. Show the staged rename list,
confirm `_CodeSignature` and `artifacts/` are absent, compare the archived
signature inventory, assert both old branded app roots are absent, and commit:

```bash
git commit -m "Rebrand and localize the macOS launcher"
```

---

### Task 8: Update packaging audits, manifests, and installer artwork

**Files:**
- Create: `packaging/tests/test_cross_platform_branding.py`
- Create: `packaging/public_brand_delta_audit.py`
- Create: `packaging/tests/test_public_brand_delta.py`
- Create: `packaging/tests/fixtures/public-clean-iossimulator-arm64-baseline.json`
- Modify: `packaging/public_bundle_audit.py`
- Modify: `packaging/tests/test_public_bundle_audit.py`
- Modify: `packaging/public-content-manifest.json`
- Modify: `docs/legal/public-asset-sbom.json`
- Modify: `packaging/ios_loading_brand_audit.py`
- Modify: `packaging/tests/test_ios_loading_brand_audit.py`
- Modify: `packaging/macos/buildpackage.sh`
- Modify: `packaging/macos/generate_brand_artwork.py`
- Modify generated: `packaging/artwork/macos-background.png`
- Modify generated: `packaging/artwork/macos-background-2x.png`
- Modify: `packaging/tests/test_macos_branding_hardening.py`
- Modify: `packaging/windows/buildpackage.sh`
- Modify: `packaging/windows/buildpackage.nsi`
- Modify: `ios/scripts/build-public-clean.sh`

- [ ] **Step 1: Write audit RED tests**

Add tamper cases for wrong app name, stale catalog bytes, missing/extra localization, incorrect English/Chinese permission text, old IPA/installer titles, and stale public source hashes. Assert Windows output `NUKE-HOUR-<tag>-<platform>.exe` and `NUKE-HOUR-<tag>-<platform>-winportable.zip`, launcher `NUKE-HOUR.exe`, Linux `NUKE-HOUR-<tag>-x86_64.AppImage`, and macOS `NUKE-HOUR-<tag>.dmg`, while registry key and `openra-ra2` remain stable. Lock the Windows PE ProductName/FileDescription, NSIS Start Menu folder, and desktop shortcut to the new visible identity. Do not add broad `.lproj` or PNG exemptions.

`test_public_brand_delta.py` loads the real source tree, manifest, and SBOM. For
every changed file that actually enters the PublicClean app it verifies exact
source-to-bundle mapping, hash/size parity, and an explicit
category/license/public decision. The exact mappings in scope are:

```text
mods/ra2/mod.yaml                    -> mods/ra2/mod.yaml
mods/ra2/chrome/mainmenu.yaml        -> mods/ra2/chrome/mainmenu.yaml
mods/ra2/fluent/chrome.ftl           -> mods/ra2/fluent/chrome.ftl
mods/ra2/fluent/mod.ftl              -> mods/ra2/fluent/mod.ftl
mods/ra2/fluent/zh-CN/chrome.ftl     -> mods/ra2/fluent/zh-CN/chrome.ftl
mods/ra2/fluent/zh-CN/mod.ftl        -> mods/ra2/fluent/zh-CN/mod.ftl
ios/OpenRA.iOS/en.lproj/InfoPlist.strings      -> en.lproj/InfoPlist.strings
ios/OpenRA.iOS/zh-Hans.lproj/InfoPlist.strings -> zh-Hans.lproj/InfoPlist.strings
```

The two project-authored localized plist strings use a supported public
category. Inherited RA2 YAML/Fluent files retain their honest private
classification and corresponding known `not-public` errors. The user-authorized
icon provenance is recorded, but `mods/ra2/icon.png` remains `public:false`
because it is not a PublicClean BundleResource; PublicClean consumes the separate
compiled AppIcon catalog through the existing generated-output exemption. A
separate final
audit compares its error `(code,path)` set with the preserved pre-change report
and requires exact equality unless the plan first records and explains a specific
expected removal. Never accept additions or silently accept disappearing bundle
resources, and never reinterpret the existing errors as a passing bundle.

Lock `public_brand_delta_audit.py` to accept `--baseline-report`,
`--candidate-report`, `--source-root`, `--manifest`, and `--sbom`. It validates
the actual changed-source entries and exits non-zero if the candidate adds or
removes any error pair, or if the fixed baseline hash/error composition
differs. Copy the preserved same-RID report byte-for-byte into the tracked
fixture and assert its fixed SHA/counts in the unit test.

- [ ] **Step 2: Run RED**

```bash
python3 -m unittest \
  packaging.tests.test_cross_platform_branding \
  packaging.tests.test_public_brand_delta \
  packaging.tests.test_public_bundle_audit \
  packaging.tests.test_ios_loading_brand_audit \
  packaging.tests.test_macos_branding_hardening -v
```

- [ ] **Step 3: Re-verify exact public-resource classification**

Verify the earlier tasks' exact entries for `mods/ra2/mod.yaml`,
`chrome/mainmenu.yaml`, both
`chrome.ftl`, both `mod.ftl`, `icon.png`, and both localized
`InfoPlist.strings`. Refresh only entries whose bytes change again in this task.
Mirror applicable hashes/sizes in the legal SBOM; preserve categories/licenses
and deny-by-default behavior.

Implement `public_brand_delta_audit.py` in this step. Unit tests must exercise
the real mapping validation plus synthetic baseline/candidate reports for exact
equality, added error, removed error, baseline hash mismatch, source hash drift,
and malformed reports. Run the real CLI with identical fixture baseline/candidate
as the positive GREEN case; `--help` alone is not acceptance.
Copy the complete pre-feature app report only from the clean sibling build
described in the fixed inputs; verify its SHA/count composition, exact three-file
public set, and required runtime sentinels before adding the fixture. The sibling
checkout must be at `275a28f`, contain the verified post-`0017` engine replay,
use normal relative `bin`/`obj`, have its own
`PublicAuditReportDir`, and have no personal build in those roots. Do not pass
`UseArtifactsOutput` or an absolute `ArtifactsPath`: with the pinned SDK that
combination can create a resource-only `.app` and an invalidly small report.

- [ ] **Step 4: Update installer outputs and artwork**

Use `NUKE HOUR` in DMG title/background text, Windows PE metadata, NSIS menu and
shortcut names, installer names, audit messages, and public IPA/hash filenames.
Regenerate both tracked macOS background PNGs through the approved artwork
script. Keep internal wallpaper filenames unchanged and continue byte
comparisons. Run the complete active-source old-brand scan here, with an exact
allowlist only for stable internal executable/wallpaper identifiers and
historical documents.

- [ ] **Step 5: Run packaging GREEN**

```bash
python3 -m unittest discover -s packaging/tests -p 'test_*.py'
python3 packaging/public_brand_delta_audit.py \
  --baseline-report packaging/tests/fixtures/public-clean-iossimulator-arm64-baseline.json \
  --candidate-report packaging/tests/fixtures/public-clean-iossimulator-arm64-baseline.json \
  --source-root "$PWD" --manifest packaging/public-content-manifest.json \
  --sbom docs/legal/public-asset-sbom.json
python3 packaging/generate_nuke_hour_icons.py --check
git diff --check
```

- [ ] **Step 6: Commit Task 8**

Stage exactly the expanded Task 8 **Files** allowlist with the common protocol,
including both generated DMG background PNGs and both Windows packaging files.
Show the cached list and reject all packaging build/output directories.

```bash
git commit -m "Audit NUKE HOUR distribution artifacts"
```

---

### Task 9: Generate and strictly replay engine patch `0018`

**Files:**
- Create: `engine-patches/0018-nuke-hour-brand-language.patch`
- Modify: `engine-patches/README.md`
- Create: `packaging/verify_engine_patch_delivery.py`
- Create: `packaging/tests/test_verify_engine_patch_delivery.py`

- [ ] **Step 1: Verify the official source and predecessor baseline**

First write RED tests for a portable Python verifier. Its CLI accepts repository,
official ZIP, expected ZIP SHA, predecessor range, exact allowlist, candidate,
and workspace paths. Synthetic fixtures reject wrong ZIP hash, unapproved
fuzz/offset, unexpected headers, `.rej/.orig`, outside-allowlist drift, a
duplicate apply that mutates bytes, and reverse-dry-run failure. It uses Python
byte-path sorting/hashing instead of GNU `sort -z`, which stock macOS lacks.

Use the official `release-20250330` ZIP with SHA-256:

```text
ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a
```

Extract to a fresh `mktemp -d` directory and apply `0001` through `0017` in
order with Apple `patch --verbose -p1 --batch -V none`. Capture one log per
patch and compare every `offset|fuzz` line byte-for-byte with README's exact two
historical `0007` diagnostics; reject all other diagnostics, any non-zero exit,
or any `.rej`/`.orig`. Preserve the combined predecessor log in `/private/tmp`.

- [ ] **Step 2: Generate from the ten-file allowlist**

Create `a/` from the fresh post-0017 tree and `b/` from current workspace for
only the exact ten files listed above. Set
`STAGE=$(mktemp -d /private/tmp/openra-0018-stage.XXXXXX)` and
`CANDIDATE="$STAGE/0018-nuke-hour-brand-language.patch"`; run
`(cd "$STAGE" && diff -ruN a b) > "$CANDIDATE"`, require the exact expected
`diff` exit code 1, and reject 0 or >1. Extract both old/new header paths,
normalize the `a/`/`b/` prefix, sort them, and compare them byte-for-byte with a
ten-line allowlist before touching `engine-patches/`.

- [ ] **Step 3: Prove strict application and byte identity**

On a second fresh post-0017 tree:

```bash
(cd "$REPLAY" && git apply --check --verbose -p1 "$CANDIDATE")
patch -p1 --batch -F 0 -V none -d "$REPLAY" \
  < "$CANDIDATE"
```

Run the now-GREEN checked-in verifier. It captures the strict apply log and
rejects `offset|fuzz`; compares all ten replay paths byte-for-byte with
workspace; builds the tracked-engine/allowlist union; rejects any missing or
mismatched outside path; creates a portable sorted SHA-256 manifest; proves the
failed duplicate forward dry-run leaves it unchanged; proves reverse dry-run
succeeds; and rechecks no `.rej/.orig`. It writes a JSON evidence report inside
the fresh `$STAGE`. Do not use fixed candidate names or GNU-only NUL sorting.

- [ ] **Step 4: Update README and commit**

Run `python3 -m unittest packaging.tests.test_verify_engine_patch_delivery -v`
and the real verifier invocation. Only after every candidate gate passes, add `$CANDIDATE` to the repository with
`apply_patch` as `engine-patches/0018-nuke-hour-brand-language.patch`. Document
`0018`, exact headers, post-0017 baseline, strict `-F 0`, and separation from
root/iOS/launcher/artwork changes. Stage exactly the patch, README, verifier,
and verifier test with the common exact staging gate.

```bash
git commit -m "Add NUKE HOUR engine patch"
```

---

### Task 10: Run full builds, audits, artifact checks, and final review

**Files:**
- No intended source changes.
- `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs` must remain byte-identical.

- [ ] **Step 1: Run complete suites**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 -v minimal
python3 -m unittest discover -s packaging/tests -p 'test_*.py'
./utility.sh --check-yaml
```

- [ ] **Step 2: Build engine Release outputs**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Game/OpenRA.Game.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj -c Release --no-restore -m:1
```

- [ ] **Step 3: Build and audit an unsigned iOS simulator artifact**

```bash
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -c Release -f net8.0-ios -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:CodesignDisable=true -p:TargetPlatform=ios -m:1
```

Run app-brand audit against the built app and confirm the AOT registry has no diff.

```bash
SIM_APP="$PWD/ios/OpenRA.iOS/bin/Release/net8.0-ios/iossimulator-arm64/OpenRA.iOS.app"
test -d "$SIM_APP"
python3 packaging/ios_app_brand_audit.py "$SIM_APP" --source-root "$PWD"
git diff --exit-code -- ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs
```

- [ ] **Step 4: Build and audit the signed personal iOS artifact**

Build personal `ios-arm64`, run brand/loading/touch audits and strict codesign.
Existing valid signing credentials are required for final personal-artifact
acceptance; if unavailable, report that artifact gate blocked. Run the
PublicClean source/catalog/native-localization and scoped delta audits. Do not
invoke a bypass flag or claim a distributable PublicClean IPA: its existing full
audit must continue to fail on the unchanged unrelated inventory. If that
separate inventory is independently resolved before this gate, then run
`ios/scripts/build-public-clean.sh` and its app/IPA/codesign audits normally.
Do not publish or upload artifacts.

Run an unsigned PublicClean simulator diagnostic build without suppressing its
audit target. Require the build to stop specifically at `AuditPublicBundle`,
produce a candidate JSON report, and then prove exact error-pair equality:

```bash
PUBLIC_ROOT=$(mktemp -d /Users/kevin/Projects/nuke-hour-public-candidate.XXXXXX)
git clone --shared --no-hardlinks "$PWD" "$PUBLIC_ROOT/repo"
# Populate "$PUBLIC_ROOT/repo/engine" from a fresh Task 9 verified post-0018
# replay, then byte-compare the ten 0018 paths before building.
set +e
cd "$PUBLIC_ROOT/repo"
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -c Release -f net8.0-ios -p:RuntimeIdentifier=iossimulator-arm64 -m:1 \
  -p:PublicClean=true -p:CodesignDisable=true -p:TargetPlatform=ios \
  -p:PublicAuditReportDir="$PUBLIC_ROOT/audit" \
  > /private/tmp/nuke-hour-public-build.log 2>&1
PUBLIC_BUILD_EXIT=$?
set -e
test "$PUBLIC_BUILD_EXIT" -ne 0
test -x ios/OpenRA.iOS/bin/Release/net8.0-ios/iossimulator-arm64/OpenRA.iOS.app/OpenRA.iOS
test -f ios/OpenRA.iOS/bin/Release/net8.0-ios/iossimulator-arm64/OpenRA.iOS.app/OpenRA.iOS.dll
python3 packaging/public_brand_delta_audit.py \
  --baseline-report packaging/tests/fixtures/public-clean-iossimulator-arm64-baseline.json \
  --candidate-report "$PUBLIC_ROOT/audit/public-bundle-audit.json" \
  --source-root "$PUBLIC_ROOT/repo" \
  --manifest packaging/public-content-manifest.json \
  --sbom docs/legal/public-asset-sbom.json
```

Also assert the captured build log names `AuditPublicBundle` and contains no
earlier compile/link/brand-audit failure; otherwise the expected non-zero exit is
not accepted as the known provenance blocker. The fresh isolated artifacts root
(including per-project output and intermediate directories) is mandatory; never
reuse the personal simulator output.

```bash
: "${IOS_CODESIGN_KEY:?set the existing Apple Development identity}"
: "${IOS_PROVISION_UUID:?set the existing provisioning UUID}"
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -c Release -f net8.0-ios -p:RuntimeIdentifier=ios-arm64 -m:1 \
  -p:CodesignKey="$IOS_CODESIGN_KEY" \
  -p:CodesignProvision="$IOS_PROVISION_UUID"
DEVICE_APP="$PWD/ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app"
python3 packaging/ios_app_brand_audit.py "$DEVICE_APP" --source-root "$PWD"
python3 packaging/ios_loading_brand_audit.py audit \
  --app-bundle "$DEVICE_APP" --source-mod-root "$PWD/mods/ra2"
python3 packaging/audit_ios_touch_runtime_bundle.py "$DEVICE_APP" \
  --source-root "$PWD" \
  --managed-output-root "$PWD/ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64"
codesign --verify --deep --strict --verbose=4 "$DEVICE_APP"
```

- [ ] **Step 5: Build, cold-install, and verify platform icons**

Run existing macOS packaging commands, strict codesign, and macOS branding
tests. Install fresh copies into isolated temporary/test locations and verify the
Finder and Dock icon after clearing only the test artifact's icon cache. For iOS,
request the narrow device/simulator install authority required at this gate,
first resolve and display the target UDID, existing bundle id, artifact path,
and RID. Use `iossimulator-arm64` only with a simulator and `ios-arm64` only with
a physical device; uninstall/install only a disposable test copy of the personal
build, then capture SpringBoard plus English/Chinese per-app-language evidence.
PublicClean cold-install verification remains blocked with its full audit unless
the separate provenance inventory has been resolved. If device/simulator access
or install authority is unavailable, report only the visual acceptance gate
blocked. Never replace the user's installed production app, publish, or upload
artifacts.

Always run an explicitly unsigned package/branding gate first:

```bash
MAC_UNSIGNED_OUT=$(mktemp -d /private/tmp/nuke-hour-macos-unsigned.XXXXXX)
env -u MACOS_DEVELOPER_IDENTITY \
  -u MACOS_DEVELOPER_CERTIFICATE_BASE64 \
  -u MACOS_DEVELOPER_CERTIFICATE_PASSWORD \
  packaging/macos/buildpackage.sh 20250330 "$MAC_UNSIGNED_OUT"
test -f "$MAC_UNSIGNED_OUT/NUKE-HOUR-20250330.dmg"
python3 -m unittest packaging.tests.test_macos_branding \
  packaging.tests.test_macos_branding_hardening -v
```

Then run the signed gate only through an explicit fail-closed branch:

```bash
if [ -z "${MACOS_DEVELOPER_IDENTITY:-}" ]; then
  printf '%s\n' 'BLOCKED: signed macOS gate needs MACOS_DEVELOPER_IDENTITY'
else
  set -e
  security find-identity -v -p codesigning | \
    grep -F -- "$MACOS_DEVELOPER_IDENTITY" >/dev/null
  MAC_OUT=$(mktemp -d /private/tmp/nuke-hour-macos-signed.XXXXXX)
  packaging/macos/buildpackage.sh 20250330 "$MAC_OUT"
  test -f "$MAC_OUT/NUKE-HOUR-20250330.dmg"
  MAC_MOUNT=$(mktemp -d /private/tmp/nuke-hour-mount.XXXXXX)
  MAC_ATTACHED=false
  cleanup_nuke_hour_mount() {
    if [ "$MAC_ATTACHED" = true ]; then hdiutil detach "$MAC_MOUNT"; fi
  }
  trap cleanup_nuke_hour_mount EXIT HUP INT TERM
  hdiutil attach "$MAC_OUT/NUKE-HOUR-20250330.dmg" -readonly -nobrowse \
    -mountpoint "$MAC_MOUNT"
  MAC_ATTACHED=true
  MOUNTED_APP="$MAC_MOUNT/NUKE HOUR.app"
  test -d "$MOUNTED_APP"
  codesign --verify --deep --strict --verbose=4 "$MOUNTED_APP"
  hdiutil detach "$MAC_MOUNT"
  MAC_ATTACHED=false
  trap - EXIT HUP INT TERM
fi
```

The unsigned DMG is branding evidence only, never signed acceptance. The signed
branch verifies before any cold-install copy and its trap detaches only this
resolved temporary mount point on every exit path.
For simulator verification, first run `xcrun simctl list devices available -j`
and select one explicit UDID; do not use `booted` or a wildcard when multiple
devices exist. Display `plutil -extract CFBundleIdentifier raw` from the exact
artifact and `xcrun simctl listapps <UDID>` before requesting install/uninstall
authority.

- [ ] **Step 6: Final repository gates**

```bash
python3 packaging/generate_nuke_hour_icons.py --check
git diff --check
git status --short
git diff --cached --name-only
```

Confirm fresh evidence, no target over 50 MB, no secrets, empty index after commits, unrelated untracked files unchanged, and no build/artifact/retail/signature/cache path committed.

- [ ] **Step 7: Request two-stage and holistic review**

Dispatch a spec reviewer against the approved design and plan, then a code-quality reviewer against implementation commits. Fix every Critical or Important issue and re-run relevant focused/full gates before claiming completion.
