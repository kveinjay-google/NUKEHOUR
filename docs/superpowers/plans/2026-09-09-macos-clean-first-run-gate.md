# macOS PublicClean First-Run Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the final macOS PublicClean DMG survive a completely empty OpenRA profile, guide the user through import, and prevent direct skirmish until a valid map exists.

**Architecture:** Keep the current launcher shell and content importer. Defer only the gameplay-dependent skirmish page until first navigation, represent an empty map catalog with an explicit disabled state, and invalidate map-dependent pages after import so they rebuild from current data. Preserve the existing launch-time content and map guards as defense in depth.

**Tech Stack:** Python 3.11, Tkinter, `unittest`, PyInstaller, OpenRA/.NET, macOS DMG and code-signing tools.

---

### Task 1: Lock the clean-profile failure with tests

**Files:**
- Modify: `packaging/tests/test_macos_public_clean_launcher.py`

- [ ] Add a test that calls `Launcher._build_pages` with mocked page builders and proves `_page_skirmish` is not called eagerly while startup-safe pages are built.
- [ ] Add a test for `skirmish_map_menu_state([])` that requires one localized placeholder value and `ready == False`.
- [ ] Add a test for a non-empty map label list that preserves values and returns `ready == True`.
- [ ] Add a test that map/content imports invalidate the deferred `skirmish` page.
- [ ] Run `python3 -m unittest packaging.tests.test_macos_public_clean_launcher -v` and verify the new tests fail because deferred construction and the empty-map model do not exist.

### Task 2: Implement deferred skirmish construction and empty-map UI

**Files:**
- Modify: `launcher.py`
- Modify: `launcher_i18n.py`

- [ ] Add `skirmish_map_menu_state(labels)` returning `(labels, True)` when non-empty and `([translated empty-map label], False)` otherwise.
- [ ] Register the skirmish page frame without invoking `_page_skirmish`; store its builder and scrollability metadata.
- [ ] Add `_ensure_page_built(key)` and call it from `show_page` before placing the selected page.
- [ ] In `_page_skirmish`, always pass an explicit first option to `tk.OptionMenu`, disable the selector when `ready` is false, show an import-map explanation, and disable both start controls.
- [ ] Keep `_skirmish_start` as the final guard for direct or stale calls.
- [ ] Add English and Simplified Chinese catalog entries for the empty-map explanation.
- [ ] Invalidate the skirmish page after successful retail/map imports so the next navigation reloads the catalog.
- [ ] Run `python3 -m unittest packaging.tests.test_macos_public_clean_launcher packaging.tests.test_launcher_language -v` and require all tests to pass.

### Task 3: Refresh the PublicClean allowlist and verify regressions

**Files:**
- Modify: `packaging/macos/public_clean_manifest.json`

- [ ] Regenerate the exact PublicClean source manifest hashes with `python3 packaging/macos/generate_public_clean_manifest.py`.
- [ ] Run `python3 -m unittest discover -s packaging/tests -p 'test_*.py' -v` and require zero failures.
- [ ] Run `DOTNET_ROLL_FORWARD=Major make RUNTIME=net6 TARGETPLATFORM=osx-arm64 CONFIGURATION=Release all` and require zero build errors.
- [ ] Run `git diff --check` and `python3 packaging/nukehour_version.py check`.

### Task 4: Build and audit a replacement DMG

**Files:**
- Generate: `artifacts/public-macos-clean-first-run-1.0.3-<commit>/NUKE-HOUR-PublicClean-1.0.3-arm64.dmg`
- Generate: audit, SBOM, checksum, build metadata, and acceptance report beside the DMG.

- [ ] Build with `python3 packaging/macos/build_public_clean.py --output <new-output-directory>`.
- [ ] Verify the checksum and run `public_clean_audit.py` against the resulting DMG.
- [ ] Mount the DMG read-only and require deep code-sign verification plus arm64 launcher/game hosts.

### Task 5: Repeat the automated manual acceptance

**Files:**
- Generate: an untracked acceptance report in the new artifact directory.

- [ ] Copy the newly built app to a unique `/Applications/NUKE HOUR Acceptance … .app` path without overwriting an existing installation.
- [ ] Atomically move the existing OpenRA support directory to a unique backup and create an empty support directory.
- [ ] Launch the installed app executable and verify the legal notice appears without an exception.
- [ ] Acknowledge the notice, verify automatic routing to content management, and select the isolated legal `ra2.mix` and `language.mix` files through the native file picker.
- [ ] Verify the launcher reports base content ready, launch the engine from the UI, and require an arm64 game process plus startup logs with no fatal/error lines.
- [ ] Terminate only the acceptance process, preserve the generated test profile as evidence, and restore the original support directory atomically.
- [ ] Mark the acceptance report PASS only if every preceding step succeeds.

### Task 6: Commit the verified fix

**Files:**
- Modify only the tested launcher, localization, tests, manifest, specification, and plan files.

- [ ] Show the complete staged file list.
- [ ] Commit with a focused message after all automated and GUI acceptance gates pass.
- [ ] Confirm no tracked changes remain and all pre-existing untracked work is preserved.
