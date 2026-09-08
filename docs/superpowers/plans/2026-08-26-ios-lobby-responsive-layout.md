# iOS Lobby and Multiplayer Responsive Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the duplicate outer-only iOS lobby resizing with shared nested map and multiplayer-detail layouts that stay readable and contained on iPhone, iPad mini, iPad Air, and iPad Pro.

**Architecture:** Introduce pure `IosMapPreviewLayout` and `IosMultiplayerDetailsLayout` geometry objects with shared widget-tree application seams. Update the compact lobby allocation so the map remains usable and the Servers tab owns the full main surface. Extend the browser footer/server-row geometry, and make `ServerListLogic` preserve responsive Join/client-row layout on iOS. Persist every ignored engine change in patch `0012`.

**Tech Stack:** C# 12, OpenRA widget/chrome system, NUnit, MiniYaml, shell patch replay.

---

## Task 1: Lock the broken geometry with RED tests

**Files:**

- Add: `engine/OpenRA.Test/IosLobbyResponsiveLayoutTest.cs`
- Modify: `engine/OpenRA.Test/IosMenuLayoutPolicyTest.cs`

- [ ] Add a single device-case source covering both supported iPhones plus iPad mini 1024 × 768, the current 1133 × 744 compact-tablet snapshot, iPad Air 1180 × 820, and iPad Pro 1366 × 1024 with real effective/native sizes and safe-area insets.
- [ ] Add map-layout tests for playable, validating/downloading, update/install, simultaneous update-plus-install, and retry/error visible-role sets. Require positive sizes, containment, non-overlap, a map surface of at least 48 physical points, and two distinct 48-point action slots.
- [ ] Build synthetic widget trees with every ID from `lobby-mappreview.yaml`; call the production application seam; assert nested `MAP_PREVIEW` fill, direct button bounds, progress/text slots, and idempotent reapplication.
- [ ] Add compact lobby tests requiring a full-height normal map card, three footer actions, and a full-main Servers panel distinct from the player/chat region.
- [ ] Add multiplayer tests for five summary rows, state-aware reclamation of the hidden player row, a client list that fits one 24-point header plus one 48-point row, nested selected map, fixed Join placement, footer Player Count, row icons, width/font-aware retruncation, and Summary → Clients → Summary transitions.
- [ ] Fix the test repository-root helper to accept `.git` as either a directory or linked-worktree file.
- [ ] Run the focused tests before production changes and preserve the expected compile/assertion failures as RED evidence:

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore --filter 'FullyQualifiedName~OpenRA.Test.IosLobbyResponsiveLayoutTest|FullyQualifiedName~OpenRA.Test.IosMenuLayoutPolicyTest' -v minimal
```

## Task 2: Implement the shared map-preview layout

**Files:**

- Add: `engine/OpenRA.Mods.Common/Widgets/Logic/IosMapPreviewLayout.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/IosTouchMenuLogic.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/MapPreviewLogic.cs`

- [ ] Implement pure large/small map, title, primary, secondary, double-height status, progress, primary-action, and secondary-action bounds with compact horizontal and tablet vertical presentations.
- [ ] Implement one application seam that assigns every map state container and every nested label/progress/button ID without turning direct actions into full-card overlays.
- [ ] Resize each nested `MapPreview` to a one-pixel inset of its current background.
- [ ] Keep metadata single-line and width-aware truncated. Only enable wrapping for long status labels after assigning a measured two-line-height slot; preserve existing visibility callbacks.
- [ ] Update `MapPreviewLogic` title/type/author truncation keys to include current width and current font, then prove the same map is recalculated after a compact/tablet resize.
- [ ] Replace both duplicate map resize methods with the shared seam.
- [ ] Run the map-focused tests until GREEN.

## Task 3: Give compact lobby and embedded servers usable surfaces

**Files:**

- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyLayout.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs`

- [ ] Add a full `Main` surface and explicit normal-panel/server-panel bounds.
- [ ] On phones, let the map use full main height and place Start, Change Map, and Disconnect in three non-overlapping footer cells.
- [ ] Make `TOP_PANELS_ROOT` cover `Main`; assign normal tab children to the player region and `LOBBY_SERVERS_BIN` to the full main region.
- [ ] Hide chat only while the iOS Servers tab is active; rely on existing visibility rules for the map and map button.
- [ ] Preserve tablet player/map/chat allocation outside the Servers tab.
- [ ] Run compact and tablet lobby tests until GREEN.

## Task 4: Implement responsive multiplayer details and rows

**Files:**

- Add: `engine/OpenRA.Mods.Common/Widgets/Logic/IosMultiplayerDetailsLayout.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/IosMultiplayerBrowserLayout.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/IosTouchMenuLogic.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/ServerListLogic.cs`

- [ ] Implement state-aware compact side-by-side and tablet stacked selected-server detail geometry. Reserve the compact text-column minimum before sizing the map, and reclaim the hidden player-summary row when clients are present.
- [ ] Resize the nested selected map and loaded client-list scroll panel through the shared application seam.
- [ ] Add Player Count to the six-cell standalone footer and reserve password/authentication icon space inside server title rows.
- [ ] Give embedded server details the full panel height while reserving filter/reload controls under the server table.
- [ ] Prevent `ServerListLogic` from restoring desktop Join coordinates on iOS.
- [ ] At the end of every `ServerListLogic.SelectServer` selection and clear path, call the shared details application seam with the current `hasClients` state. Reapply once dynamic client rows have been built so Summary → Clients → Summary immediately reclaims and restores the player-summary slot without waiting for a resize or server-row-count change.
- [ ] Adapt dynamic client header/player clones to current iOS fonts and geometry before adding them to the list.
- [ ] Include current bounds width and font in selected-map/mod-version truncation caches and test the same server across a resize.
- [ ] Run multiplayer detail, state-transition, and dynamic-row tests until GREEN.

## Task 5: Run adjacent verification and inspect layout invariants

**Files:**

- Modify as needed only within Tasks 1–4 files.

- [ ] Run the complete responsive-menu test group:

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore --filter 'FullyQualifiedName~OpenRA.Test.IosLobbyResponsiveLayoutTest|FullyQualifiedName~OpenRA.Test.IosMenuLayoutPolicyTest|FullyQualifiedName~OpenRA.Test.IosTouchDialogLayoutTest|FullyQualifiedName~OpenRA.Test.IosSettingsLayoutTest|FullyQualifiedName~OpenRA.Test.IosMultiplayerPolicyTest' -v minimal
```

- [ ] Run `./utility.sh --check-yaml` from the repository root.
- [ ] Build Common and RA2 in Release with the exact commands below, single-node to avoid shared-output races:

```bash
env DOTNET_ROOT=/Users/kevin/.dotnet PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin /Users/kevin/.dotnet/dotnet build engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj -c Release --no-restore --nologo -m:1
env DOTNET_ROOT=/Users/kevin/.dotnet PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin /Users/kevin/.dotnet/dotnet build OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore --nologo -m:1
```
- [ ] Check `git diff --check` and verify no unrelated files changed.
- [ ] Independently review every device/state result for positive geometry, immediate-parent containment, non-overlap, physical targets, and idempotence.

## Task 6: Persist ignored engine files in patch 0012

**Files:**

- Add: `engine-patches/0012-ios-lobby-responsive-layout.patch`
- Modify: `engine-patches/README.md`
- Force-add: only the exact modified/new engine production and test files from Tasks 1–4.

- [ ] Verify the official release ZIP SHA-256 is `ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a`.
- [ ] Freshly extract the pinned official release and apply patches 0001 through 0011 in order.
- [ ] Generate 0012 only from the exact engine paths changed by this task; do not include root mod copies, build output, logs, or unrelated ignored files.
- [ ] In a second fresh extraction, apply 0001 through 0012 and compare every patched target byte-for-byte with the materialized `engine/` file.
- [ ] Confirm patch headers contain only expected paths and `git diff --check` reports no whitespace errors.

## Task 7: Independent review and delivery commit

**Files:**

- All exact task files only.

- [ ] Request an independent specification review against the approved design and device/state matrix.
- [ ] Resolve every Critical or Important finding with a fresh RED → GREEN cycle.
- [ ] Request an independent code-quality/replay review and resolve every Critical or Important finding.
- [ ] Rerun focused/adjacent tests, YAML validation, Release builds, replay comparison, and diff checks after the last source change.
- [ ] Stage only the exact implementation, tests, 0012 patch, README, specification, and plan files. Show the staged file list before committing.
- [ ] Commit locally with a focused message. Do not push, install to devices, delete artifacts, or touch unrelated untracked files.
