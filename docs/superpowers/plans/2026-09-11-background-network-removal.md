# NUKE HOUR Background Network Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Preserve the dirty shared worktree and commit only files owned by each task.

**Goal:** Remove community news, update checks, anonymous system information, and OpenRA public-service networking without changing NUKE HOUR Local or Online Multiplayer.

**Architecture:** Delete the retired product settings and UI, collapse shared web-service configuration to the NUKE HOUR lobby seam, and make LAN discovery independent of all HTTP master-server behavior. Remove OpenRA map/profile and Itch background requests, then enforce the boundary with source/config and bundle audits.

**Tech Stack:** C#/.NET, Python 3/Tkinter, YAML chrome definitions, `unittest`, NUnit, macOS packaging scripts.

---

### Task 1: Remove launcher background-service settings and add release guards

**Files:**
- Modify: `launcher.py`
- Modify: `launcher_i18n.py`
- Modify: `packaging/tests/test_macos_public_clean_launcher.py`
- Modify: `packaging/tests/test_launcher_language.py`

- [ ] Add failing tests that reject `FetchNews`, `CheckVersion`, `SendSystemInformation`, their UI labels, Tk variables, and emitted engine arguments.
- [ ] Add a test proving the launcher still exposes user-initiated website and multiplayer paths.
- [ ] Delete the “在线服务” settings card and retired localized strings.
- [ ] Remove retired defaults, Tk variables and engine command-line arguments.
- [ ] Strip the three retired keys from launcher-managed settings during the existing atomic save path without changing unrelated values.
- [ ] Run the focused Python tests and commit only this task’s files.

### Task 2: Remove news, version-check and anonymous-system-information engine code

**Files:**
- Modify: `engine/OpenRA.Game/Settings.cs`
- Modify: `engine/OpenRA.Mods.Common/WebServices.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/Settings/AdvancedSettingsLogic.cs`
- Delete: `engine/OpenRA.Mods.Common/Widgets/Logic/SystemInfoPromptLogic.cs`
- Modify: `engine/mods/common/chrome/settings-advanced.yaml`
- Modify: shipped prompt/menu chrome and localization files found by the focused source scan
- Modify: `engine/OpenRA.Test/NukeHourBrandingTest.cs`
- Regenerate: `ios/OpenRA.iOS/AotObjectRegistry.Generated.cs` if the generator includes the deleted logic

- [ ] Add failing engine/source-contract tests for the removed settings, classes, endpoints and menu paths.
- [ ] Delete version status and `CheckModVersion` from `WebServices`, preserving only the NUKE HOUR lobby field.
- [ ] Delete main-menu news download/cache/UI and update-notice logic.
- [ ] Delete anonymous-system-information settings, prompt logic and chrome references.
- [ ] Regenerate, never hand-maintain, the iOS AOT registry when required.
- [ ] Build affected projects, run focused NUnit tests and commit only owned files.

### Task 3: Remove OpenRA master-server advertisement while preserving local discovery

**Files:**
- Modify: `engine/OpenRA.Game/Settings.cs`
- Modify: `engine/OpenRA.Mods.Common/ServerTraits/MasterServerPinger.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/ServerCreationLogic.cs`
- Modify: `engine/mods/common/chrome/multiplayer-createserver.yaml`
- Modify: shipped mod-specific create-server chrome located by source scan
- Modify: `engine/OpenRA.Game/Support/IosMultiplayerSmoke.cs`
- Modify: `packaging/server/run-server.sh`
- Modify: `packaging/tests/test_dedicated_server_delivery.py`
- Modify: focused LAN/online-lobby engine tests

- [ ] Add failing tests proving OpenRA HTTP advertisement is absent and LAN Beacon/Nearby advertising remains.
- [ ] Remove `AdvertiseOnline` as a user/configurable OpenRA-master setting and remove its UI.
- [ ] Simplify `MasterServerPinger` to local Beacon/Nearby state updates only; remove HTTP, queues, error messages and shutdown POST.
- [ ] Update server launch scripts and fixtures without touching NUKE HOUR `OnlineLobbyRegistration`.
- [ ] Run Local Multiplayer and dedicated-server focused tests and commit only owned files.

### Task 4: Remove OpenRA map/profile services and legacy Itch background lookup

**Files:**
- Modify: `engine/OpenRA.Game/Settings.cs`
- Modify: `engine/OpenRA.Game/PlayerDatabase.cs`
- Modify: `engine/OpenRA.Mods.Common/ItchIntegration.cs`
- Modify: remote-map call sites in `engine/OpenRA.Mods.Common/Widgets/Logic` and `engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs`
- Modify: player-profile/authentication call sites in `engine/OpenRA.Mods.Common` and `engine/OpenRA.Server`
- Modify: `engine/OpenRA.Test/NukeHourBrandingTest.cs` or a dedicated runtime-network-boundary test

- [ ] Add failing tests rejecting OpenRA map/profile endpoints and the Itch API endpoint.
- [ ] Remove `QueryMapRepository` and all automatic remote-map detail queries; keep local/imported map selection.
- [ ] Remove player badge/profile HTTP loads and OpenRA profile authentication fallback.
- [ ] Make authentication-required configurations without a NUKE HOUR provider fail closed with a clear diagnostic.
- [ ] Replace Itch player-name lookup with a local empty-name callback and remove its HTTP/JSON dependencies when unused.
- [ ] Run focused map, lobby, server-auth and introduction tests and commit only owned files.

### Task 5: Add a whole-product no-background-network audit and build

**Files:**
- Add: `packaging/audit_background_network.py`
- Add: `packaging/tests/test_background_network_audit.py`
- Modify: `packaging/public_bundle_audit.py`
- Modify: `packaging/tests/test_public_bundle_audit.py`
- Add: next ordered patch in `engine-patches/`
- Modify: `engine-patches/README.md`

- [ ] Add a failing audit that scans shipped source/config and built bundles for OpenRA public domains and retired feature tokens.
- [ ] Add positive assertions for `https://lobby.nukehour.com/`, LAN/Nearby discovery, Direct IP and TCP 1234.
- [ ] Integrate the audit into public-clean bundle verification.
- [ ] Capture engine changes as a new ordered patch so `fetch-engine.sh` reproduces the result without rewriting older patches.
- [ ] Run Python launcher, public-clean, bundle and dedicated-server suites.
- [ ] Run affected .NET builds/tests and the macOS public-clean build.
- [ ] Scan the produced `.app`/DMG payload; record exact verification output.
- [ ] Commit only the audit, patch and intentionally changed packaging files.

### Task 6: Final regression and scope review

- [ ] Review the complete diff against `docs/superpowers/specs/2026-09-11-background-network-removal-design.md`.
- [ ] Confirm no iOS work from the other active session was overwritten.
- [ ] Confirm no files outside `/Users/kevin/Projects/ra2-mac` were inspected or changed.
- [ ] Run `git diff --check`, focused full test suites and final banned-domain scans.
- [ ] Report preserved NUKE HOUR multiplayer endpoints separately from removed background services.
- [ ] Stop without pushing, merging or modifying the Android repository.
