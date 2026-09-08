# macOS NUCLEAR CRISIS Branding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the existing NUCLEAR CRISIS name, icon, and wallpaper set to the private macOS launcher and game-host bundles.

**Architecture:** Keep the engine/mod data paths stable while replacing only Finder-visible bundle metadata and launcher presentation. The launcher reads the canonical wallpaper files directly from `mods/ra2/uibits`, while one generated `.icns` is copied byte-for-byte to every macOS icon consumer.

**Tech Stack:** Python 3 `unittest`, Tk/Pillow launcher, macOS app bundles, `plistlib`, `sips`, `iconutil`, `codesign`.

---

### Task 1: Add the macOS branding contract

**Files:**
- Create: `packaging/tests/test_macos_branding.py`

- [ ] **Step 1: Write the failing test**

Create a `unittest.TestCase` that expects `NUCLEAR CRISIS.app`, `NUCLEAR CRISIS GAME.app`, uppercase plist display names, `NuclearCrisis` as the game host, nine canonical wallpaper paths, and identical `.icns` bytes in all three destinations.

- [ ] **Step 2: Run test to verify it fails**

Run: `python3 -m unittest packaging.tests.test_macos_branding -v`

Expected: FAIL because the old Afghanistan-named bundles and launcher constants still exist.

### Task 2: Rebrand launcher behavior and app metadata

**Files:**
- Modify: `launcher.py`
- Modify: `launcher_i18n.py`
- Rename: `Red Alert 2：阿富汗危机.app` to `NUCLEAR CRISIS.app`
- Rename: `Red Alert 2：阿富汗危机 - 游戏.app` to `NUCLEAR CRISIS GAME.app`
- Modify: `NUCLEAR CRISIS.app/Contents/Info.plist`
- Modify: `NUCLEAR CRISIS GAME.app/Contents/Info.plist`
- Rename: `NUCLEAR CRISIS GAME.app/Contents/MacOS/AfghanistanCrisis` to `NuclearCrisis`

- [ ] **Step 1: Implement the minimal branding constants and wallpaper selector**

Define `BRAND_NAME`, the new app/host paths, and a sorted canonical wallpaper list. Use an injectable selector for deterministic tests; in production choose randomly and use Pillow `ImageOps.fit` to preserve aspect ratio.

- [ ] **Step 2: Replace visible launcher branding**

Set the window title, sidebar title, footer labels, running-process detection, and activation script to the NUCLEAR CRISIS identity while retaining the existing technical support directory.

- [ ] **Step 3: Update app bundle metadata and names**

Rename both bundles and the host executable, then set the corresponding `CFBundleDisplayName`, `CFBundleName`, `CFBundleExecutable`, and `CFBundleIconFile` values.

### Task 3: Build and install the shared macOS icon

**Files:**
- Modify: `launcher_assets/icon.icns`
- Modify: `NUCLEAR CRISIS.app/Contents/Resources/icon.icns`
- Modify: `NUCLEAR CRISIS GAME.app/Contents/Resources/NUCLEAR-CRISIS.icns`

- [ ] **Step 1: Generate the iconset**

Use `sips` to create 16, 32, 128, 256, 512, and 1024 pixel representations from `ios/OpenRA.iOS/Assets.xcassets/AppIcon.appiconset/NUCLEAR-CRISIS-1024.png`, then use `iconutil` to build `launcher_assets/icon.icns`.

- [ ] **Step 2: Copy the exact icon to both app bundles**

Install identical bytes in the launcher and game-host resource directories and update the game plist icon filename.

- [ ] **Step 3: Re-sign modified bundles**

Run ad-hoc signing from the inner game bundle to the launcher bundle using `codesign --force --deep --sign -`.

### Task 4: Verify and launch

**Files:**
- Test: `packaging/tests/test_macos_branding.py`

- [ ] **Step 1: Run the branding tests**

Run: `python3 -m unittest packaging.tests.test_macos_branding -v`

Expected: all tests PASS.

- [ ] **Step 2: Validate syntax, plists, icons, and signatures**

Run `python3 -m py_compile launcher.py launcher_i18n.py`, `plutil -lint` for both plists, `sips -g pixelWidth -g pixelHeight` for all icons, and `codesign --verify --deep --strict` for both bundles.

- [ ] **Step 3: Launch the macOS app**

Run `open 'NUCLEAR CRISIS.app'`, then confirm the launcher process is alive and no immediate launch error was written.
