# Engine patches

The `engine/` directory is a pristine, gitignored checkout of
`OpenRA/OpenRA @ release-20250330` (see `mod.config`). Every local engine
modification MUST be captured here as a patch, otherwise it is lost the next
time `fetch-engine.sh` re-downloads the engine.

Patches apply in filename order with `patch -p1` from the engine root.
`fetch-engine.sh` applies them automatically after every fresh download via
`apply-engine-patches.sh`.

## Series

| Patch | Files | Purpose |
| --- | --- | --- |
| `0001-launchinto-ordermanager-race.patch` | `OpenRA.Game/Game.cs` | Fix null-World race when `LoadComplete` synchronously replaces `Game.OrderManager` during `Game.LaunchInto` (skirmish deep-link from the main menu). |
| `0002-launchinto-deeplinks.patch` | `OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs` | Launcher deep-links: `LaunchInto "<command> <arg>"` supports `replay`, `loadsave`, `skirmish <map> <lobby-cmds>`, `connect <host>:<port>`, plus panel shortcuts (`settings`, `replays`, `missions`, `editor`, `music`, `content`, ...). Hoists `world`/`contentInstaller` fields and extracts `OpenSettingsPanel`. |
| `0003-engine-i18n-language-selection.patch` | `OpenRA.Game/FluentProvider.cs`, `OpenRA.Game/Settings.cs`, `OpenRA.Mods.Common/Widgets/Logic/Settings/DisplaySettingsLogic.cs`, `mods/common/chrome/settings-display.yaml`, `mods/common/fluent/chrome.ftl` | User-selectable UI language: `Game.Settings.Game.Language` (default `en`), loaded as `fluent/<lang>/` overrides of each `FluentMessages` entry with English fallback, plus a language dropdown in Display settings (restart required). |
| `0004-engine-common-zh-cn-translations.patch` | `mods/common/fluent/zh-CN/*.ftl` (new) | Simplified Chinese translations for engine-common strings (chrome/common/hotkeys/rules). |
| `0005-power-counter-respect-textcolor.patch` | `OpenRA.Mods.Common/Widgets/Logic/Ingame/IngamePowerCounterLogic.cs` | Preserve each mod's configured normal power-counter color while retaining the critical red state. |
| `0006-settings-tab-fluent-labels.patch` | `OpenRA.Mods.Common/Widgets/Logic/Settings/SettingsLogic.cs` | Resolve settings tab labels and reset-dialog panel names through Fluent. |
| `0007-language-restart-and-system-default.patch` | `OpenRA.Game/Settings.cs`, `OpenRA.Mods.Common/Widgets/Logic/Settings/DisplaySettingsLogic.cs` | Select Chinese from the system UI culture, expose language changes in restart-required state, and reset language with other game settings. |
| `0008-integrated-ios-runtime.patch` | Engine runtime, common chrome/localization, and regression tests | Replays the completed iOS touch, safe-area, multiplayer, diagnostics/performance, audio/haptics, gameplay, V3/Kirov audit, and responsive HUD integration captured by this repository. |
| `0009-ios-two-finger-gesture-arbitration.patch` | `OpenRA.Game/Input/TouchGestureAdapter.cs`, `OpenRA.Platforms.Default/Sdl2Input.cs`, `OpenRA.Test/TouchGestureAdapterTest.cs` | Adds immediate, mode-locked two-finger pan/pinch arbitration while preserving short two-finger right-click and three-finger pan compatibility. |
| `0010-audio-bag-wav-safety.patch` | `OpenRA.Game/Sound/Sound.cs`, `OpenRA.Mods.Common/FileFormats/WavReader.cs`, `OpenRA.Test/WavReaderSafetyTest.cs` | Rejects malformed WAV streams before deferred reads, tolerates unknown/truncated chunks, and isolates corrupt sound effects so one asset cannot terminate the game loop. |
| `0011-order-feedback-effects.patch` | `OpenRA.Mods.Common/Traits/World/OrderEffects.cs`, `OpenRA.Test/OrderFeedbackEffectsTest.cs` | Adds optional order-aware one-shot sprite feedback while preserving the existing target flash fallback for unconfigured and unmapped orders. |
| `0012-ios-lobby-responsive-layout.patch` | iOS lobby/map-preview/multiplayer layout policies, runtime wiring, and regression tests | Reflows nested map states, localized status/action text, lobby surfaces, server details, client rows, and footer controls across supported iPhone and iPad landscape sizes. |
| `0013-post-0012-overlay-sync.patch` | Four iOS viewport source/test files plus `V3RenderingRulesTest.cs` and `YuriProductionCameoTest.cs` | Synchronizes already-committed engine overlay drift after 0012 without adding the A1 quarter-ring behavior. |
| `0014-ios-three-action-quarter-ring.patch` | iOS viewport geometry/actions/joystick sources and three regression suites | Implements the approved A1 three-action quarter ring, circular initial joystick hit area, and touch command-bar/base-cycle coverage. |
| `0015-ios-lobby-scroll-layout-polish.patch` | Five iOS lobby/map-preview/scroll policies and two regression suites | Replays the disjoint lobby, map-preview, chat, and scroll-arrow layout polish that remains outside A1; the root mod `chrome.yaml` stays in the outer repository. |
| `0016-ios-lobby-direct-start-progress-faction-picker.patch` | Multiplayer readiness protocol, iOS lobby UI, common chrome/localization, and regression tests | Starts iOS-hosted games with one tap only after every connected human has the current map ready, shows truthful download/verification blockers and progress, and enlarges the iOS faction selector without changing the desktop lobby UI. |
| `0017-ios-safe-start-rejection-correlation.patch` | 12 engine networking, server, lobby, and regression-test paths | Advances to Orders23 with correlated safe-start requests and rejections, prevents a stale response from clearing a newer pending request, and shows the authoritative blocker or progress immediately. It changes no Fluent/YAML, desktop behavior, or safe-start safety semantics. |
| `0018-nuke-hour-brand-language.patch` | 10 engine language, menu/settings, and regression-test paths | Adds the persistent `System` / `zh-CN` / `en` language policy, quick main-menu selector, simplified game-settings title, and NUKE HOUR branding assertions. Root mod, iOS, launcher, artwork, and packaging changes remain outside this engine patch. |
| `0019-ios-support-powers-beside-production.patch` | `OpenRA.Mods.Common/Widgets/Logic/Ingame/IosSupportPowerLayoutPolicy.cs`, `OpenRA.Mods.Common/Widgets/Logic/Ingame/SupportPowerBinLogic.cs`, `OpenRA.Test/IosSupportPowerLayoutPolicyTest.cs` | Places compact iPhone support powers immediately beside the full visible production sidebar, fills columns bottom-to-top and leftward, and adds layout and visible-widget-tree regression coverage. |
| `0020-macos-nuke-hour-runtime-branding.patch` | `OpenRA.Game/Game.cs`, `OpenRA.Game/Settings.cs`, `OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs`, `OpenRA.Test/IosMenuLayoutPolicyTest.cs`, `OpenRA.Test/NukeHourBrandingTest.cs` | Aligns the default player identity and local skirmish title with NUKE HOUR, adds the macOS-only About flow, and covers desktop/iOS menu isolation with regression tests. |
| `0021-independent-product-and-compatibility-versions.patch` | Engine manifest, networking, diagnostics, version-label logic, and regression tests | Separates the user-visible product release from stable storage and deterministic multiplayer compatibility while preserving the existing handshake wire field. |
| `0022-macos-content-source-delivery.patch` | 9 installer/content-source engine and regression-test paths | Captures the already-committed bounded macOS retail-content discovery overlay so a fresh engine checkout reproduces the shipping content importer exactly. |
| `0023-nukehour-multiplayer-protocol.patch` | 11 shared handshake, compatibility, lobby, localization, and regression-test paths | Adds the symmetric NUKE HOUR handshake schema, canonical runtime profile/contract, stable rejection reasons, and real TCP-framing interoperability tests. |
| `0024-runtime-contract-resource-compatibility.patch` | 3 RuntimeContract and regression-test paths | Replaces whole-retail-archive identity with required presentation-capability validation, advances the capability id, and adds RC1-RC9 false-rejection and safety coverage. |
| `0025-phase12-cross-platform-physical-smoke.patch` | `OpenRA.Game/Support/IosMultiplayerSmoke.cs`, `OpenRA.Game/Sync.cs`, `OpenRA.Test/IosMultiplayerPolicyTest.cs` | Makes the opt-in physical smoke use the active cross-platform RuntimeContract, requires observable gameplay and sustained-sync evidence, and unifies desktop/iOS boolean sync hashing after physical-device desync diagnosis. |
| `0026-nukehour-dedicated-server-foundation.patch` | 10 engine runtime, server, physical-smoke, and regression-test paths | Adds explicit dedicated-server identity, a presentation-only headless RuntimeContract path, numeric listen-address/dynamic-port settings, bounded capacity and idle handling, one-process/one-room lifecycle, atomic health status, and signal-safe clean exit without changing the shared Phase 1 protocol. |
| `0027-post-phase2-engine-sync.patch` | 4 iOS sidebar/startup-notice engine and regression-test paths | Captures the already-committed full-height iOS production-sidebar correction and startup distribution-notice test so a fresh checkout matches the current product baseline before Phase 3. |
| `0028-online-lobby-room-directory.patch` | 21 shared directory, dedicated-server registration, multiplayer-browser, physical-smoke, responsive layout, localization, and regression-test paths | Adds authenticated official-room registration, short-lived metadata discovery, Local/Online client modes, room-list and room-code lookup, HTTPS-only directory access, preflight compatibility checks, hidden Online endpoints, and direct gameplay handoff without changing the shared game protocol. |
| `0029-ios-ingame-sidebar-quickbar.patch` | 5 iOS production-sidebar layout/runtime and command-bar regression-test paths | Sizes the final production-sidebar fill row to the exact remaining height so it cannot overlap the bottom cap, and records the larger, label-aware, frameless touch-command icon layout used by the outer RA2 widget implementation. |
| `0031-macos-content-manager-return.patch` | `OpenRA.Game/Game.cs`, content availability/manager logic, and `OpenRA.Test/Ra2ContentInstallerManifestTest.cs` | Gives launcher-owned clean-build content management an explicit cancel contract: Back exits the game child and restores the still-running macOS launcher, while successful content validation clears the one-shot policy so later in-game content management still returns to the game menu. |
| `0032-ios-sidebar-scroll-button-slot.patch` | iOS production-sidebar layout/runtime and `IosProductionBatchToggleTest.cs` | Anchors both production scroll buttons to the authored slot at the top of the full-height sidebar bottom cap, applying the nested production-type offset exactly once so the artwork and hit targets cannot fall below the cap. |
| `0033-ios-hide-main-menu-quit.patch` | `MainMenuLogic.cs`, `IosMainMenuQuitPolicyTest.cs`, and `NukeHourBrandingTest.cs` | Removes the desktop-only quit action from iPhone/iPad and keeps the five remaining iOS main-menu controls in the adaptive touch layout without changing desktop behavior. |
| `0034-ios-commandbar-sidebar-menu-layout.patch` | iOS command-slot, production-sidebar, main-menu layout/runtime, and regression-test paths | Gives every touch command a common slot geometry and icon source, reapplies complete production scroll-button bounds after runtime relayout, and expands the five-button iOS main menu across the original six-row footprint. |
| `0035-ios-faction-touch-control-skins.patch` | `TouchFactionSkin.cs`, `IosViewportActionsLogic.cs`, and `IosViewportActionLayoutTest.cs` | Adds faction-routed command-glyph collections, hides redundant captions under the self-explanatory floating action icons, and locks the complete 33-glyph V3 touch-art contract into the engine replay series. |
| `0036-ios-compact-main-menu.patch` | `MainMenuLogic.cs` and three main-menu regression-test files | Removes the redundant iOS content-manager entry, shortens the system-language label, and evenly redistributes the four remaining touch targets across the established main-menu frame. |
| `0037-ios-restore-skirmish-and-settings.patch` | iOS lobby/color-picker/settings source, common chrome, and regression tests | Permanently restores the skirmish-only five-column layout, removes handicap/ready, limits skirmish and preferred colours to the nine classic swatches, and keeps the four retired Advanced modules out of rebuilt engines. |
| `0038-ios-main-menu-single-tap-language.patch` | `MainMenuLogic.cs` and `IosMenuLayoutPolicyTest.cs` | Replaces the start-menu language dropdown with a one-tap System/Chinese/English cycle and reserves balanced 20-point vertical breathing room while retaining safe touch targets. |

## Regenerating after editing engine sources

A target patch must be diffed against the official release **after** replaying
every patch that sorts before it. In other words, `a/` comes from the
post-predecessor `BASE`, not directly from the pristine archive.

```sh
export LC_ALL=C LANG=C
REPO=$(git rev-parse --show-toplevel)
ARCHIVE=/path/to/openra-release-20250330.zip
EXPECTED_ZIP_SHA256=ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a
TARGET=0024-runtime-contract-resource-compatibility.patch
BASE_DIR=$(mktemp -d /private/tmp/openra-engine-base.XXXXXX)
STAGE=$(mktemp -d /private/tmp/openra-engine-patch-stage.XXXXXX)
ALLOWLIST="$STAGE/allowlist.txt"
CANDIDATE="$STAGE/$TARGET"

[ "$(shasum -a 256 "$ARCHIVE" | awk '{print $1}')" = "$EXPECTED_ZIP_SHA256" ] || exit 1
ditto -x -k "$ARCHIVE" "$BASE_DIR"
BASE="$BASE_DIR/OpenRA-release-20250330"

# Replay the exact numeric predecessor range 0001 through 0023. Do not glob
# every patch after 0024 exists, and require exactly one patch per prefix.
# 0007 has two pinned historical diagnostics against the post-0006 baseline;
# every other predecessor must apply without offset or fuzz.
KNOWN_0007_DIAGNOSTICS='Hunk #1 succeeded at 254 with fuzz 2 (offset -1 lines).
Hunk #5 succeeded at 352 (offset -1 lines).'
mkdir -p "$STAGE/predecessor-logs"
: > "$STAGE/predecessors-combined.log"
for prefix in 0001 0002 0003 0004 0005 0006 0007 0008 0009 \
	0010 0011 0012 0013 0014 0015 0016 0017 0018 0019 \
	0020 0021 0022 0023; do
	set -- "$REPO"/engine-patches/"$prefix"-*.patch
	[ "$#" -eq 1 ] && [ -f "$1" ] || exit 1
	p=$1
	PATCH_LOG="$STAGE/predecessor-logs/$(basename "$p").log"
	if ! /usr/bin/patch --verbose -p1 --batch -V none -d "$BASE" \
		-i "$p" > "$PATCH_LOG" 2>&1; then
		cat "$PATCH_LOG"
		exit 1
	fi
	{
		printf '===== %s =====\n' "$(basename "$p")"
		cat "$PATCH_LOG"
	} >> "$STAGE/predecessors-combined.log"
	DIAGNOSTICS=$(grep -Ei 'Hunk .*(offset|fuzz)' "$PATCH_LOG" || true)
	if [ "$(basename "$p")" = '0007-language-restart-and-system-default.patch' ]; then
		[ "$DIAGNOSTICS" = "$KNOWN_0007_DIAGNOSTICS" ] || {
			cat "$PATCH_LOG"
			exit 1
		}
	elif [ -n "$DIAGNOSTICS" ]; then
		cat "$PATCH_LOG"
		exit 1
	fi
done
if find "$BASE" -type f \( -name '*.rej' -o -name '*.orig' \) | grep -q .; then
	exit 1
fi

FILES='
OpenRA.Game/Network/NukeHourNetworkCompatibility.cs
OpenRA.Test/NukeHourDirectTcpHandshakeTest.cs
OpenRA.Test/NukeHourNetworkProtocolTest.cs
'
printf '%s\n' $FILES > "$ALLOWLIST"

mkdir -p "$STAGE/a" "$STAGE/b"
for f in $FILES; do
	mkdir -p "$STAGE/a/$(dirname "$f")" "$STAGE/b/$(dirname "$f")"
	[ ! -f "$BASE/$f" ] || cp "$BASE/$f" "$STAGE/a/$f"
	[ ! -f "$REPO/engine/$f" ] || cp "$REPO/engine/$f" "$STAGE/b/$f"
done

diff_rc=0
(cd "$STAGE" && diff -ruN a b) > "$CANDIDATE" || diff_rc=$?
[ "$diff_rc" -eq 1 ] || exit 1

# The verifier creates a second fresh extraction, replays exactly 0001-0023,
# validates three unique paired headers, and publishes evidence atomically only
# after all strict application, byte-identity, duplicate, and reverse gates pass.
python3 "$REPO/packaging/verify_engine_patch_delivery.py" \
	--repository "$REPO" \
	--official-zip "$ARCHIVE" \
	--expected-zip-sha256 "$EXPECTED_ZIP_SHA256" \
	--predecessor-start 1 \
	--predecessor-end 23 \
	--allowlist-file "$ALLOWLIST" \
	--candidate "$CANDIDATE" \
	--workspace "$REPO/engine" \
	--output-dir "$STAGE/verification"
```

Before accepting a new patch, use a second fresh extraction, capture the full
verbose output, and apply every predecessor with the verbose replay command
shown above. Any nonzero exit, unexpected `offset`/`fuzz` diagnostic, or
`.rej`/`.orig` file fails the gate. The only historical exception is the exact
two-diagnostic `0007` result pinned above; new patches do not inherit it.
For `0024`, verify exactly three unique, paired `diff -ruN a/... b/...` sections.
The verifier consumes each section's headers and every unified hunk through its
declared old/new line counts; it rejects preambles, indentation, alternate
prefixes, binary/index/rename metadata, or any other trailing content.
Root files, iOS, launcher, artwork, docs, packaging, `mods/ra2`, application
bundles, `obj`, and `bin` paths are outside the engine delivery boundary. The
portable verifier performs the equivalent strict gates:

```sh
(cd "$BASE" && git apply --check --verbose -p1 -- "$CANDIDATE")
/usr/bin/patch --forward --verbose -p1 --batch -F 0 -V none \
	-d "$BASE" -i "$CANDIDATE"
```

`-F 0` disables fuzz but does not disable offsets, so the captured strict log
must independently contain no offset/fuzz diagnostics. All three allowlisted
results must compare byte-for-byte with `engine/`, and every path in the union
of `git ls-files -z -- engine` and the allowlist must be present and identical.
A failed forward dry-run must leave the Python byte-sorted full-tree SHA-256
manifest unchanged; a reverse dry-run must succeed and also leave it unchanged.
Any `.rej`, `.orig`, symlink, missing path, or outside-allowlist drift fails the
delivery. The verifier writes per-command logs, the full manifest, and atomic
`evidence.json` under its unique output directory.

The integrated series was last regenerated from the official
`OpenRA-release-20250330` archive with SHA-256
`ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a`.

Engine upgrades: bump `ENGINE_VERSION` in `mod.config`, delete `engine/`,
run `./fetch-engine.sh`, and rebase every patch that fails to apply.
