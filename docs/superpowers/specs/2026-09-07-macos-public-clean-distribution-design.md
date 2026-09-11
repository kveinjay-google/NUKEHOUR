# macOS PublicClean Distribution Design

## Goal

Ship a user-facing Apple Silicon installer for **NUKE HOUR** that contains the
redistributable OpenRA runtime and NUKE HOUR-owned launcher/branding, but no
Electronic Arts retail game data, bundled skirmish maps, or resources whose
redistribution status is unknown.  A user supplies legally owned Red Alert 2 or
Yuri's Revenge data locally after installation.

This is a new packaging profile.  It does not delete or rewrite the existing
developer checkout, private local app bundles, or imported user content.

## Product boundary

The public artifact is named `NUKE-HOUR-PublicClean-<version>-arm64.dmg`.  The
installed application is `NUKE HOUR.app`, and every visible product string uses
`NUKE HOUR <version>` without `RA2` in the launcher identity.

The public package may contain:

- the GPL OpenRA engine runtime and repository source/configuration needed to
  run the mod;
- NUKE HOUR launcher code, localization, icon, and project-owned UI artwork;
- explicitly reviewed third-party runtime dependencies and their notices;
- configuration and import metadata that do not contain retail game bytes.

It must not contain:

- `.mix`, `.shp`, `.vxl`, `.hva`, `.aud`, `.bag`, `.idx`, `.bik`, `.vqa`, or
  `.wsa` retail data;
- bundled `.map`, `.mpr`, `.yrm`, or `.oramap` gameplay maps;
- the checkout's `mods/ra2/maps` or `mods/ra2/bits` trees;
- files classified as `unknown-or-derivative`, editor caches, logs, saves,
  replays, imported content, or build leftovers.

The allowlist is authoritative.  A file that has not been explicitly selected
by the PublicClean staging code cannot enter the app, even if its extension is
otherwise harmless.

## Architecture

### Separate PublicClean profile

`packaging/macos/build_public_clean.py` owns the clean build.  It stages into a
fresh temporary directory and constructs the package from an explicit source
allowlist.  It never starts by copying the developer app or the whole `mods`
tree and then deleting prohibited files.  The existing macOS SDK packager stays
available for private development builds.

The build has four phases:

1. validate the canonical macOS version and required local toolchain;
2. build a self-contained arm64 launcher and stage the reviewed runtime files;
3. run source-tree and staged-app audits, then sign the nested executable and
   outer app when a Developer ID identity is available;
4. create the DMG, mount it read-only, and audit the mounted application again.

Every phase fails closed.  The final command exits successfully only after the
DMG audit passes.

### Self-contained launcher and runtime

The Python/Tk launcher is frozen with PyInstaller so users do not need
Homebrew, Python, Tk, or Pillow.  Frozen mode resolves repository data beneath
`NUKE HOUR.app/Contents/Resources/runtime`; source-checkout mode keeps using the
repository root.  The application embeds the arm64 .NET/OpenRA runtime needed
to start the game, so the public package does not depend on a system `dotnet`
installation.

The existing NUKE HOUR icon remains the application icon and a clickable link
to the NUKE HOUR website.  The version remains in the launcher's lower-right
corner.

### Local-only retail import

The launcher's content page supports two entry points:

- choose an installed game folder or mounted disc and scan it recursively;
- choose one or more individual retail data or map files.

Imports are offline.  No file is uploaded and the launcher performs no content
download.  Accepted files use a conservative filename/extension allowlist.
Executables, scripts, Mach-O/PE binaries, symlinks, aliases that escape the
selected root, special files, and duplicate destination names with different
bytes are rejected.

Accepted files are copied into a staging directory under
`~/Library/Application Support/OpenRA/Content/ra2`, hashed with SHA-256, and
published by an atomic directory swap only after validation succeeds.  A failed
or cancelled import leaves the previously working content set untouched.  The
import receipt records source-independent names, sizes, hashes, and timestamp;
it never stores or transmits the user's original path.

Minimum readiness requires `ra2.mix` and `language.mix`.  `theme.mix`, Yuri's
Revenge packs, audio pairs, and maps are reported separately as optional
capabilities.  The Start button remains disabled and the content page is opened
automatically until the minimum set is present.

### Runtime resource policy

Text-based mod rules, YAML, Fluent localization, shaders, fonts, and other
reviewed OpenRA runtime data are staged explicitly.  Project-owned NUKE HOUR UI
and brand assets are enumerated in a macOS-specific public manifest with hashes
and provenance categories.  Retail-format files, built-in maps, and the
unreviewed `mods/ra2/bits` tree are never staged.

The first deliverable must prove that the clean runtime reaches the import gate
without retail data and launches with a valid locally imported minimum set.  If
an asset required after import is not demonstrably redistributable, the build
must fail rather than silently include it.

## Audit and evidence

`packaging/macos/public_clean_manifest.json` records every non-generated file
that may enter the public runtime, including SHA-256, size, category, license,
and source path.  Generated PyInstaller and .NET files are controlled by
separate directory/publisher rules and included in the generated SBOM.

`packaging/macos/public_clean_audit.py` checks:

- all staged mod/resource files are allowlisted and match their hash;
- no prohibited retail extension or built-in map exists anywhere in the app;
- no unsafe or external symlink exists;
- no imported Content, saves, replays, logs, caches, or developer-only paths
  exist;
- app metadata, bundle identity, product strings, executable architecture, and
  canonical version are correct;
- the import gate and bundled runtime paths exist;
- the mounted DMG contains exactly the app, Applications alias, and expected
  Finder metadata.

The build writes a JSON audit report, a readable Markdown report, an SBOM, and
a SHA-256 file beside the DMG under `artifacts/public-macos/`.

## Signing and notarization

When a usable Developer ID Application identity exists in the keychain, the
builder signs inside-out with hardened runtime and verifies with
`codesign --verify --deep --strict`.  If notarization credentials are present,
it submits, staples, and validates the DMG.  Missing notarization credentials
do not weaken the content audit; they are reported as an external release gate
in build metadata instead of being misrepresented as a notarized artifact.

## Error handling and recovery

- A missing build dependency stops before staging an artifact.
- A source or final audit failure deletes only the build's dedicated temporary
  staging directory and leaves the last successful artifact untouched.
- Import errors identify rejected filenames and reasons without revealing full
  source paths in receipts or logs.
- An interrupted import keeps `current` intact and removes a stale staging
  directory on the next import attempt.
- Launch failures keep the launcher open and direct the user back to content
  status instead of exiting silently.

## Verification

Automated tests cover import discovery, rejection, atomic publication,
readiness, frozen path resolution, launcher gating, manifest validation, retail
extension detection, symlink escape detection, brand/version metadata, and DMG
inventory.  The release check then performs a real arm64 build, audits the
staged app, signs and verifies it when possible, creates and mounts the DMG,
audits the mounted app, and records its checksum.

Success means the command returns zero and produces the DMG plus passing audit
evidence.  The developer checkout may still contain private assets; only the
explicit PublicClean artifact is asserted clean.
