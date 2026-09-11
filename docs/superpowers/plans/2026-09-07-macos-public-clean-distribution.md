# macOS PublicClean Distribution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and verify an Apple Silicon `NUKE HOUR` DMG that embeds its launcher/runtime, contains no retail game data or built-in maps, and imports user-owned content locally before enabling launch.

**Architecture:** Add a fail-closed Python content importer and launcher readiness gate, then add a dedicated allowlist-based macOS staging/audit pipeline. Freeze the launcher with PyInstaller, copy only reviewed OpenRA/mod inputs, embed the arm64 .NET runtime, sign when an identity is available, create a DMG, mount it, and rerun the audit.

**Tech Stack:** Python 3.11, unittest, Tk/Pillow, PyInstaller, OpenRA/.NET 8 arm64, macOS `codesign`, `hdiutil`, and `ditto`.

---

## File map

- `ra2_files.py`: source discovery, file validation, atomic retail import, receipt, and readiness status.
- `launcher.py`: frozen-resource resolution, content import actions, readiness gate, and bundled game command.
- `launcher_i18n.py`: bilingual copy for importing and launch blocking.
- `packaging/macos/public_clean_manifest.json`: explicit reviewed source allowlist and provenance.
- `packaging/macos/public_clean_audit.py`: reusable source/app/DMG policy checks and reports.
- `packaging/macos/build_public_clean.py`: clean staging, PyInstaller build, embedded runtime, signing, DMG creation, and verification orchestration.
- `packaging/macos/build-public-clean.sh`: stable release entry point.
- `packaging/tests/test_macos_public_clean_import.py`: importer and readiness tests.
- `packaging/tests/test_macos_public_clean_launcher.py`: launcher resolution and gating tests.
- `packaging/tests/test_macos_public_clean_audit.py`: deny-by-default audit tests.
- `packaging/tests/test_macos_public_clean_builder.py`: allowlist staging and metadata tests.
- `docs/legal/PUBLIC_DISTRIBUTION.md`: macOS clean-package promise and user import notes.

### Task 1: Safe local retail importer

**Files:**
- Modify: `ra2_files.py`
- Create: `packaging/tests/test_macos_public_clean_import.py`

- [ ] **Step 1: Write failing discovery and rejection tests**

Create tests that build temporary folder trees and assert that
`discover_retail_content()` returns accepted `ra2.mix`, `language.mix`, optional
packs, and supported map files while rejecting symlinks, executable extensions,
PE/Mach-O magic, special files, and conflicting duplicate basenames.

- [ ] **Step 2: Run the importer tests and verify RED**

Run:

```bash
python3 -m unittest packaging.tests.test_macos_public_clean_import -v
```

Expected: failure because `discover_retail_content` and `import_retail_content`
do not exist and because the current readiness rule requires `theme.mix`.

- [ ] **Step 3: Implement discovery, validation, and atomic publication**

Add `MINIMUM_PACKAGES = ("ra2.mix", "language.mix")`, named optional package
sets, accepted map extensions, binary magic rejection, SHA-256 streaming, and a
`RetailImportError`.  `import_retail_content(paths, destination=None)` must copy
accepted regular files to a sibling staging directory, write
`import-receipt.json`, rename the old destination to `previous`, atomically
rename staging to current, then remove only its own previous directory.

- [ ] **Step 4: Run importer tests and verify GREEN**

Run the unittest command from Step 2.  Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add ra2_files.py packaging/tests/test_macos_public_clean_import.py
git commit -m "feat: add safe macOS retail content importer"
```

### Task 2: Frozen paths and launch readiness gate

**Files:**
- Modify: `launcher.py`
- Modify: `launcher_i18n.py`
- Create: `packaging/tests/test_macos_public_clean_launcher.py`

- [ ] **Step 1: Write failing path and gate tests**

Test `runtime_root()` with normal and frozen process inputs; test
`launch_readiness()` for missing and complete minimum content; test that the
game command chooses the embedded arm64 apphost/runtime before system `dotnet`.

- [ ] **Step 2: Run launcher tests and verify RED**

```bash
python3 -m unittest packaging.tests.test_macos_public_clean_launcher -v
```

Expected: failure because the new helpers do not exist.

- [ ] **Step 3: Implement the gate and import controls**

Resolve frozen resources from `sys._MEIPASS/runtime`, preserve source checkout
resolution, add folder and multiple-file pickers to Content Management, show
optional capability status, and route incomplete Start actions to the content
page with an explanatory message.  Keep the launcher open when the child fails
to start.

- [ ] **Step 4: Run launcher and existing layout/language tests**

```bash
python3 -m unittest \
  packaging.tests.test_macos_public_clean_launcher \
  packaging.tests.test_macos_launcher_layout \
  packaging.tests.test_launcher_language -v
```

Expected: all tests pass and visible branding remains `NUKE HOUR <version>`.

- [ ] **Step 5: Commit**

```bash
git add launcher.py launcher_i18n.py packaging/tests/test_macos_public_clean_launcher.py
git commit -m "feat: gate macOS launch on imported retail content"
```

### Task 3: Deny-by-default macOS audit

**Files:**
- Create: `packaging/macos/public_clean_audit.py`
- Create: `packaging/tests/test_macos_public_clean_audit.py`

- [ ] **Step 1: Write failing policy tests**

Use temporary app trees to prove that the audit accepts reviewed text/UI files
and generated runtime files, but reports retail extensions, bundled map
extensions, unsafe symlinks, developer directories, mismatched hashes, and
unlisted mod resources.  Include a test that rejects `RA2` in visible plist
names while permitting the internal mod id.

- [ ] **Step 2: Run the audit tests and verify RED**

```bash
python3 -m unittest packaging.tests.test_macos_public_clean_audit -v
```

Expected: import failure because the macOS audit module does not exist.

- [ ] **Step 3: Implement the audit API and CLI**

Implement `load_manifest`, `audit_runtime`, `audit_app`, `audit_dmg_inventory`,
JSON/Markdown report writers, and a nonzero CLI exit on any error.  Apply retail
and map extension checks globally; apply exact manifest hash checks to staged
`Resources/runtime`; permit only generated PyInstaller/.NET files in declared
generated prefixes; and validate bundle version, arm64 executable, and product
strings.

- [ ] **Step 4: Run audit tests and verify GREEN**

Run the unittest command from Step 2.  Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add packaging/macos/public_clean_audit.py packaging/tests/test_macos_public_clean_audit.py
git commit -m "feat: audit macOS public clean bundles"
```

### Task 4: Explicit source manifest and clean staging

**Files:**
- Create: `packaging/macos/public_clean_manifest.json`
- Create: `packaging/macos/build_public_clean.py`
- Create: `packaging/tests/test_macos_public_clean_builder.py`

- [ ] **Step 1: Write failing staging tests**

Assert that `public_source_paths()` includes required engine/mod text and
project-owned launcher/UI files, excludes `mods/ra2/maps`, `mods/ra2/bits`,
retail extensions, `.DS_Store`, and caches, and produces a deterministic tree
whose entries all match the manifest hashes.

- [ ] **Step 2: Run builder tests and verify RED**

```bash
python3 -m unittest packaging.tests.test_macos_public_clean_builder -v
```

Expected: failure because the builder and manifest do not exist.

- [ ] **Step 3: Implement allowlist staging**

Populate the manifest from reviewed tracked files only.  Implement functions to
verify the manifest against the checkout, copy regular files without following
symlinks, synthesize an arm64/version-correct plist, write license inventory,
and fail if a prohibited file is requested.  Never copy the whole repository or
whole `mods/ra2` tree.

- [ ] **Step 4: Run builder and audit tests**

```bash
python3 -m unittest \
  packaging.tests.test_macos_public_clean_builder \
  packaging.tests.test_macos_public_clean_audit -v
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add packaging/macos/public_clean_manifest.json \
  packaging/macos/build_public_clean.py \
  packaging/tests/test_macos_public_clean_builder.py
git commit -m "feat: stage allowlisted macOS public runtime"
```

### Task 5: Self-contained app and DMG build

**Files:**
- Modify: `packaging/macos/build_public_clean.py`
- Create: `packaging/macos/build-public-clean.sh`
- Modify: `docs/legal/PUBLIC_DISTRIBUTION.md`

- [ ] **Step 1: Add failing command-construction tests**

Extend the builder tests to assert a PyInstaller arm64 one-directory app using
`launcher_assets/icon.icns`, an embedded OpenRA/.NET arm64 runtime, inside-out
codesigning order, deterministic output names, mounted-DMG audit, SHA-256, SBOM,
and notarization metadata that says `not-submitted` when credentials are absent.

- [ ] **Step 2: Run the focused test and verify RED**

Run the builder unittest.  Expected: failures for the missing build orchestration
and shell entry point.

- [ ] **Step 3: Implement the release command**

Freeze the launcher, move its executable into the final bundle, stage runtime
inputs, embed the local arm64 .NET runtime/hostfxr, build the OpenRA arm64 apphost,
audit before and after signing, create a compressed DMG with an Applications
symlink, mount read-only, audit again, unmount in `finally`, and write reports,
SBOM, checksum, and build metadata.  The shell script calls the Python builder
with the canonical version and `artifacts/public-macos` output.

- [ ] **Step 4: Document the public boundary**

Add a macOS section explaining that the DMG contains no retail content or maps,
imports happen locally, `ra2.mix` and `language.mix` are required, and users are
responsible for supplying legally owned data.

- [ ] **Step 5: Run unit and repository regression tests**

```bash
python3 -m unittest discover -s packaging/tests -p 'test_*.py' -v
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add packaging/macos/build_public_clean.py \
  packaging/macos/build-public-clean.sh docs/legal/PUBLIC_DISTRIBUTION.md \
  packaging/tests/test_macos_public_clean_builder.py
git commit -m "build: add macOS public clean release pipeline"
```

### Task 6: Real build and release evidence

**Files:**
- Generated: `artifacts/public-macos/NUKE-HOUR-PublicClean-1.0.3-arm64.dmg`
- Generated: `artifacts/public-macos/NUKE-HOUR-PublicClean-1.0.3-arm64.dmg.sha256`
- Generated: `artifacts/public-macos/public-clean-audit.json`
- Generated: `artifacts/public-macos/public-clean-audit.md`
- Generated: `artifacts/public-macos/public-clean-sbom.json`
- Generated: `artifacts/public-macos/build-metadata.json`

- [ ] **Step 1: Run the real release build**

```bash
packaging/macos/build-public-clean.sh
```

Expected: PyInstaller and OpenRA finish, the app is audited, signing verification
passes when the keychain identity is usable, the DMG is mounted and audited, and
the command exits zero.

- [ ] **Step 2: Verify the artifact independently**

```bash
shasum -a 256 -c artifacts/public-macos/NUKE-HOUR-PublicClean-1.0.3-arm64.dmg.sha256
python3 packaging/macos/public_clean_audit.py \
  --dmg artifacts/public-macos/NUKE-HOUR-PublicClean-1.0.3-arm64.dmg \
  --manifest packaging/macos/public_clean_manifest.json
```

Expected: checksum `OK` and audit `PASS` with zero prohibited resources.

- [ ] **Step 3: Verify signing and architecture**

Mount the DMG to a build-owned temporary mountpoint, run
`codesign --verify --deep --strict --verbose=2` on `NUKE HOUR.app`, confirm the
main executable is arm64 with `file`, then detach it.  Expected: signature valid
and arm64 reported.

- [ ] **Step 4: Record final repository state**

Run `git status --short`, `git log -8 --oneline`, and preserve unrelated user
files.  Generated artifacts remain untracked unless the user separately asks to
commit release binaries.
