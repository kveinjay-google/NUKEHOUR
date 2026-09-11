# macOS NUKE HOUR Support Directory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move macOS NUKE HOUR user data to a branded support directory while preserving existing user data through a safe one-time migration.

**Architecture:** `ra2_files.py` owns support-root constants and a copy-only migration primitive. `launcher.py` invokes migration before settings are loaded and passes the branded root to every game/server process. Focused Python tests prove migration safety and command propagation.

**Tech Stack:** Python 3, `pathlib`/`os`/`shutil`/`hashlib`, Tkinter launcher, `unittest`.

---

### Task 1: Lock the migration contract with failing tests

**Files:**
- Modify: `packaging/tests/test_macos_public_clean_import.py`
- Modify: `packaging/tests/test_macos_public_clean_launcher.py`

- [x] Add a test that constructs separate legacy/current roots and asserts that only the allowlisted NUKE HOUR paths are copied, source files remain, symlinks are skipped, and copied hashes match.
- [x] Add a second test that runs migration twice and asserts existing destination conflicts are never overwritten.
- [x] Add launcher assertions for the branded settings/log paths and `Engine.SupportDir` in both client and local-server commands.
- [x] Run `python3 -m unittest packaging.tests.test_macos_public_clean_import packaging.tests.test_macos_public_clean_launcher` and confirm the new tests fail because the migration and branded path policy do not exist.

### Task 2: Implement the branded support root and migration

**Files:**
- Modify: `ra2_files.py`
- Modify: `launcher.py`

- [x] Define `SUPPORT` as `~/Library/Application Support/Nuke Hour` and `LEGACY_SUPPORT` as the old OpenRA root.
- [x] Implement an idempotent copy-only migration with a strict relative-path allowlist, no symlink traversal, exclusive destination creation, and size/SHA-256 verification.
- [x] Derive launcher settings/log paths from `ra2_files.SUPPORT` and invoke migration before constructing `Launcher`.
- [x] Add `Engine.SupportDir=<new root>` to client launch arguments and retain it in the local-server arguments.
- [x] Re-run the focused tests and require all tests to pass.

### Task 3: Verify regression boundaries

**Files:**
- Verify: `ra2_files.py`
- Verify: `launcher.py`
- Verify: `packaging/tests/test_macos_public_clean_import.py`
- Verify: `packaging/tests/test_macos_public_clean_launcher.py`

- [x] Run the macOS launcher/import suites plus `python3 -m py_compile launcher.py ra2_files.py`.
- [x] Run `git diff --check` on the implementation/test files.
- [x] Confirm the old OpenRA root appears only as `LEGACY_SUPPORT` compatibility input and that no deletion targets it.
- [x] Record that packaging remains separate because the concurrent dirty resource manifest prevents a PublicClean rebuild.
