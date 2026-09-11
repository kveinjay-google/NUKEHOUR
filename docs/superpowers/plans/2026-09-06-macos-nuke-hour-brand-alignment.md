# macOS NUKE HOUR Runtime Brand Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the macOS branch the same default-player, skirmish-title, and About-page NUKE HOUR identity as the current Android build, including launcher parity and reproducible engine-patch delivery.

**Architecture:** Keep name migration in settings, derive the local lobby title from mod metadata, keep branded copy/layout in the RA2 mod, and make the optional About entry macOS-only in common menu wiring. Mirror the generated-name policy in the Python launcher and capture all engine changes in ordered patch 0020.

**Tech Stack:** C#, OpenRA widget YAML, Fluent, Python/Tk, NUnit, unittest, POSIX patch tooling, .NET 8.

---

### Task 1: Specify player identity and launcher migration

**Files:**
- Create: `engine/OpenRA.Test/NukeHourBrandingTest.cs`
- Modify: `packaging/tests/test_launcher_language.py`
- Modify: `engine/OpenRA.Game/Settings.cs`
- Modify: `launcher.py`

- [ ] **Step 1: Write failing C# identity tests**

Add assertions equivalent to:

```csharp
Assert.That(new PlayerSettings().Name, Is.EqualTo("NUKE HOUR"));
Assert.That(Settings.MigrateLegacyPlayerName("Commander"), Is.EqualTo("NUKE HOUR"));
Assert.That(Settings.MigrateLegacyPlayerName("指挥官"), Is.EqualTo("NUKE HOUR"));
Assert.That(Settings.MigrateLegacyPlayerName("Field Marshal"), Is.EqualTo("Field Marshal"));
```

- [ ] **Step 2: Write failing Python launcher tests**

Require `default_player_name` to return `NUKE HOUR` in both languages,
`migrate_legacy_player_name` to migrate the empty/English/Chinese generated
defaults, and custom names to remain unchanged. Require `load_settings` and
`build_args` to use the migrated brand value.

- [ ] **Step 3: Run both focused suites and verify RED**

Run:

```sh
DOTNET_ROLL_FORWARD=Major dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~NukeHourBrandingTest
python3 -m unittest packaging.tests.test_launcher_language.LauncherLanguagePolicyTest
```

Expected: failures because the C# helper does not exist and the launcher still
generates `Commander` / `指挥官`.

- [ ] **Step 4: Implement the minimum settings policy**

Set `PlayerSettings.Name` to `NUKE HOUR`, add an exact pure migration helper for
`Commander` and `指挥官`, and invoke it after settings and command-line values are
loaded.

- [ ] **Step 5: Implement the matching launcher policy**

Return `BRAND_NAME` from `default_player_name`, add the same exact migration
helper, normalize loaded settings in memory, and keep the default-tracking path
stable across launcher language changes.

- [ ] **Step 6: Re-run focused suites and verify GREEN**

Run the two Step 3 commands. Expected: all focused identity/launcher tests pass.

### Task 2: Brand local skirmish and the macOS About page

**Files:**
- Modify: `engine/OpenRA.Test/NukeHourBrandingTest.cs`
- Modify: `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs`
- Modify: `engine/OpenRA.Game/Game.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs`
- Modify: `mods/ra2/chrome/mainmenu.yaml`
- Modify: `mods/ra2/fluent/mod.ftl`
- Modify: `mods/ra2/fluent/zh-CN/mod.ftl`

- [ ] **Step 1: Write failing skirmish and About tests**

Require local-server creation to contain:

```csharp
Name = ModData.Manifest.Metadata.TitleTranslated
```

Require the RA2 menu to define `ABOUT_BUTTON` before `QUIT_BUTTON` and an
`ABOUT_PANEL` with title, description, version, website, and Back controls.
Require menu logic to expose the button only for `PlatformType.OSX`, open the
panel, bind Back to close/return, and exclude the button from iOS controls.
Require both Fluent bundles to contain `NUKE HOUR`, `https://nukehour.com`, and
platform-appropriate English/Chinese copy.

Update the existing desktop-grid contract to require seven aligned controls,
including About before Quit. Keep the separate iOS layout contract at six
physical targets because the new control is macOS-only.

- [ ] **Step 2: Run the focused NUnit suite and verify RED**

```sh
DOTNET_ROLL_FORWARD=Major dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore --filter FullyQualifiedName~NukeHourBrandingTest
```

Expected: failures for the generic skirmish literal and missing About surface.

- [ ] **Step 3: Implement the skirmish title**

Replace only the literal local-server name with the active mod's translated
metadata title. Do not change multiplayer server naming.

- [ ] **Step 4: Implement the macOS-only About flow**

Add the button before Quit, make it visible and active only on macOS, open the
modal through `Game.OpenWindow`, and bind Back/Escape to close the window and
restore the main menu. Keep the existing six-control iOS layout unchanged.

- [ ] **Step 5: Add the RA2 About layout and localized copy**

Use the existing `dialog4`, BigBold/MediumBold/Regular fonts, 620×420 desktop
panel, runtime `VersionLabelLogic`, and the Android reference information hierarchy.
Use macOS-specific description text rather than claiming the desktop build is a
mobile app.

- [ ] **Step 6: Re-run the focused NUnit suite and verify GREEN**

Run the Step 2 command. Expected: every `NukeHourBrandingTest` case passes.

### Task 3: Deliver engine changes as patch 0020

**Files:**
- Create: `engine-patches/0020-macos-nuke-hour-runtime-branding.patch`
- Modify: `engine-patches/README.md`
- Modify: `packaging/tests/test_verify_engine_patch_delivery.py`

- [ ] **Step 1: Update the delivery contract test first**

Point the checked-in candidate contract to patch 0020, require the exact five
engine paths, replay predecessors 0001-0019, and update the expected validated
header count after measuring the real series.

- [ ] **Step 2: Run the delivery tests and verify RED**

```sh
python3 -m unittest packaging.tests.test_verify_engine_patch_delivery
```

Expected: failure because patch 0020 and its README entry do not yet exist.

- [ ] **Step 3: Generate patch 0020 against the post-0019 baseline**

The exact patch allowlist is:

```text
OpenRA.Game/Game.cs
OpenRA.Game/Settings.cs
OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs
OpenRA.Test/IosMenuLayoutPolicyTest.cs
OpenRA.Test/NukeHourBrandingTest.cs
```

Create canonical `diff -ruN a/... b/...` output with no preamble, alternate
prefixes, binary metadata, fuzz, or out-of-allowlist path.

- [ ] **Step 4: Document the new patch**

Add patch 0020 to the series table and update the regeneration example to use
0020 with predecessors 0001-0019 and the five-path allowlist.

- [ ] **Step 5: Strictly verify replay and workspace identity**

Download the pinned official OpenRA release archive to a temporary directory,
verify SHA-256 `ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a`,
then run `packaging/verify_engine_patch_delivery.py` with predecessor end 19.
Expected: exit 0, no new offset/fuzz diagnostics, five paired headers, byte
identity with `engine/`, duplicate-apply rejection, and reverse dry-run success.

- [ ] **Step 6: Re-run the delivery unit suite and verify GREEN**

```sh
python3 -m unittest packaging.tests.test_verify_engine_patch_delivery
```

Expected: all delivery tests pass.

### Task 4: Regression verification and macOS build

**Files:**
- Verify all changed files; do not touch unrelated untracked files.

- [ ] **Step 1: Run Python brand and launcher suites**

```sh
python3 -m unittest \
  packaging.tests.test_launcher_language \
  packaging.tests.test_macos_branding \
  packaging.tests.test_macos_branding_hardening \
  packaging.tests.test_cross_platform_branding \
  packaging.tests.test_nuke_hour_icon_pipeline
python3 packaging/generate_nuke_hour_icons.py --root "$PWD" --check
```

- [ ] **Step 2: Run engine branding and menu suites**

```sh
DOTNET_ROLL_FORWARD=Major dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore \
  --filter 'FullyQualifiedName~NukeHourBrandingTest|FullyQualifiedName~IosMenuLayoutPolicyTest'
```

- [ ] **Step 3: Build Release outputs**

```sh
DOTNET_ROLL_FORWARD=Major dotnet build engine/OpenRA.Game/OpenRA.Game.csproj -c Release --no-restore --nologo
DOTNET_ROLL_FORWARD=Major dotnet build OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore --nologo
```

- [ ] **Step 4: Run source and repository hygiene checks**

```sh
git diff --check
git status --short
```

Confirm the complete changed-file list matches this plan and every unrelated
pre-existing untracked path remains untouched.

- [ ] **Step 5: Perform a macOS smoke launch when available**

Open `NUKE HOUR GAME.app`, verify the branded load screen and main menu, open
About, close it with Back/Escape, and confirm a new skirmish displays NUKE HOUR.
If retail content or interactive launch prerequisites are unavailable, report
that limitation explicitly rather than claiming the manual check passed.
