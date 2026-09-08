# macOS NUCLEAR CRISIS Branding Design

## Goal

Make the private macOS launcher and game host use the same visible identity as the iOS build: the uppercase name `NUCLEAR CRISIS`, the nuclear-explosion application icon, and the `NUCLEAR-CRISIS-BG-*.png` wallpaper set.

## Scope

- Rename the Finder-visible launcher bundle to `NUCLEAR CRISIS.app`.
- Rename the Finder-visible game-host bundle to `NUCLEAR CRISIS GAME.app`.
- Use `NUCLEAR CRISIS` for bundle display names, the launcher window title, sidebar branding, and visible launcher footer text.
- Convert the existing iOS `NUCLEAR-CRISIS-1024.png` master into one macOS `.icns`, then install the exact same `.icns` in the launcher assets and both app bundles.
- Let the Python launcher select one of the nine canonical `mods/ra2/uibits/NUCLEAR-CRISIS-BG-*.png` wallpapers per launch and crop it to the launcher viewport without distortion.
- Keep the game menu's existing cross-platform random wallpaper list; it already loads the same nine images on macOS.

## Compatibility Boundaries

- Keep `~/Library/Application Support/OpenRA` unchanged so saves, maps, settings, logs, and imported retail resources remain available.
- Keep the existing bundle identifiers unchanged to avoid creating a second preference/permission identity.
- Rename the private game host executable to `NuclearCrisis`, update every launcher reference, and preserve the fallback `launch-game.sh` path.
- Do not change gameplay assets, iOS assets, public-clean packaging, or retail content.

## Verification

- A Python branding contract verifies the two new app bundle names, plist display names, executable/icon references, nine wallpaper inputs, identical installed `.icns` files, and absence of the old Afghanistan branding from active launcher sources.
- `plutil`, `sips`, `codesign`, and `python -m py_compile` validate the rebuilt bundles.
- Launch `NUCLEAR CRISIS.app` on macOS and confirm the process opens without an immediate launcher error.
