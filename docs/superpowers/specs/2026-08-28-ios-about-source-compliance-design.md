# iOS/iPadOS About and Source Compliance Design

Date: 2026-08-28
Status: Design approved; implementation deferred until retail-content import and PublicClean runtime closure are complete

## Goal

Add an iOS/iPadOS-only About page to the in-game settings screen. The page identifies NUKE HOUR as an independent project based on modified OpenRA software, preserves the complete OpenRA author and GPL documents, exposes third-party license information, and provides an auditable route to the complete corresponding source.

The desktop settings UI and its existing five pages remain unchanged.

## User-visible content

The English page uses the following copy:

```text
ABOUT NUKE HOUR

NukeHour is an independent RTS engine project.

Engine Technology
NukeHour is based on modified OpenRA software.

OpenRA
Copyright © OpenRA Developers and Contributors
GNU General Public License v3 or later

This is a modified version of OpenRA release-20250330.
This version contains modifications for iOS/iPadOS
made by the NukeHour project.
Modification date: 2026-08-28.
```

The four actions are:

- `View OpenRA Contributors`
- `View GPL License`
- `View Source Code`
- `Third-Party Licenses`

The product title is written as `ABOUT NUKE HOUR` to match the visible application name. The project attribution in prose remains `NukeHour project`.

The Chinese locale provides translated explanatory text and action labels. It must still show the exact OpenRA copyright line, GPL identification, modified-version statement, OpenRA base release, and fixed modification date. Translation must not replace or shorten the bundled legal documents.

## Settings integration

The About page is a sixth settings tab on iOS/iPadOS only.

- The RA2 settings chrome declares an `ABOUT_PANEL` that is hidden by default.
- The About panel logic registers it only when `Platform.IsIOS` is true.
- Desktop builds therefore register the existing five tabs and never expose the About panel.
- iOS/iPadOS registers six tabs.
- The settings reset action is hidden or disabled while About is active. Back remains available.
- The About panel is read-only and never marks settings dirty or requests a restart.

The iOS settings layout must no longer assume exactly five tabs. Its tab count comes from the panels actually registered for the current platform. Layout calculation remains deterministic and rejects invalid indexes.

All supported landscape iPhone and iPad safe-area snapshots must satisfy:

- every tab and action button is at least 48 native points high and wide where applicable;
- all six tabs are contained, ordered, and non-overlapping;
- the page content remains inside the safe content rectangle;
- the four actions use a responsive two-column grid when space permits and a single-column stack when required;
- long text scrolls instead of being clipped or forcing controls outside the page;
- rotation, safe-area changes, UI scale changes, and reapplication preserve the active About page and produce the same geometry.

## About documents

The app bundle contains the following fixed, read-only documents:

| Bundle path | Source and contract |
| --- | --- |
| `AUTHORS` | Byte-for-byte copy of OpenRA `engine/AUTHORS` |
| `COPYING` | Byte-for-byte copy of OpenRA `engine/COPYING` |
| `THIRD_PARTY_LICENSES` | Versioned notices and license texts for dependencies shipped in the iOS app |
| `NUKEHOUR_MODIFICATIONS` | NUKE HOUR modification statement and date |
| `SOURCE_INFORMATION` | Source scope, OpenRA baseline, revision, build instructions, and published source location |

The existing `mods/ra2/AUTHORS` remains present and unchanged; it is not a substitute for OpenRA `AUTHORS`.

The document viewer is a local, plain-text, read-only scroll panel. It uses a closed document identifier enum mapped in code to the five fixed bundle paths. YAML, Fluent strings, URLs, and user input cannot supply a filesystem path. The viewer does not render HTML or execute links embedded in document text.

`View OpenRA Contributors` opens the complete `AUTHORS`. `View GPL License` opens the complete `COPYING`. `Third-Party Licenses` opens the complete generated notice document. The overview page must never embed shortened copies in place of these originals.

If a document is absent, unreadable, not a regular file, outside the expected bundle root, or fails its recorded digest, the UI shows a localized `Document unavailable` message and logs a diagnostic without crashing. The same condition is a build/release audit failure.

## Modification record

`NUKEHOUR_MODIFICATIONS` records at minimum:

- that NUKE HOUR is a modified version of OpenRA;
- the OpenRA baseline `release-20250330`;
- that the modifications target iOS/iPadOS;
- the project attribution `NukeHour project`;
- the fixed modification date `2026-08-28`;
- the exact source revision used to build the app;
- a pointer to `AUTHORS`, `COPYING`, `THIRD_PARTY_LICENSES`, and `SOURCE_INFORMATION`.

The date is source controlled. It is not derived from the current clock or rebuilt on every compilation. The document is generated deterministically from a versioned template plus explicit build inputs for the exact source revision and source URL. A development build with no public URL emits an exact `public source URL not configured for this development build` statement; it never emits a placeholder URL. Dirty or unidentified source trees are non-distributable.

## Third-party notices

`THIRD_PARTY_LICENSES` is produced from a source-controlled inventory of all third-party code and non-code resources actually shipped in the iOS bundle when their licenses require notice. At minimum, the inventory covers the locked .NET runtime components, SDL2, SDL2-CS, FreeType, shipped NuGet assemblies, native libraries included by the final link, fonts, images, shaders, and other attributed runtime resources. Build-only inputs such as the Microsoft.iOS workload and packages that do not ship in the app are recorded in a separate reproducible-toolchain inventory and are not misrepresented as redistributed app components.

For every entry the inventory records:

- component and exact version or immutable source revision;
- upstream project URL;
- license identifier;
- provenance of the license text;
- the exact license/notice text required for redistribution;
- every package, assembly, native binary, or exact bundle resource path that caused it to be included.

The generated document is deterministic. Its generator and audit reject missing versions, unknown native-library provenance, missing license text, unlisted shipped dependencies, and stale entries. In particular, a locally installed FreeType license must not be used unless it is proven to match the FreeType binary in the app. A release remains blocked until every shipped component has closed provenance.

## Source publication preparation

This task prepares a publishable local Git repository but does not create a GitHub repository, add a remote, or push without separate user authorization.

The publishable source set includes:

- the complete NUKE HOUR source used for the build;
- the OpenRA baseline identification and all engine patches, including the new About feature patch;
- iOS project files and deterministic build scripts;
- `AUTHORS`, `COPYING`, third-party notices, modification record, and source information;
- reproducible build instructions and the exact toolchain/workload versions;
- a release tag/commit convention that lets a binary identify its exact source revision.

It excludes:

- original Red Alert 2 or Yuri's Revenge retail assets and user-imported content;
- provisioning profiles, signing identities, certificates, private keys, tokens, cookies, and credentials;
- user settings, saves, maps, replays, logs, screenshots, and device data;
- caches, `bin`, `obj`, downloaded packages, private artifacts, and unrelated files larger than 50 MB.

Before any public release, the source tree passes a secret scan, large-file gate, tracked-file inventory, clean-clone build check, and an audit proving that every shipped source modification is represented in the public source set.

## Source action and GitHub release gate

During development, `View Source Code` opens the local `SOURCE_INFORMATION`. The document contains the exact current source revision and OpenRA baseline even when no network source has been configured. Its name and copy describe source information, not a time-limited GPL written offer.

For a distributable release, a fixed source URL is mandatory. It must:

- use `https`;
- point to a public GitHub repository controlled for NUKE HOUR;
- identify an immutable release tag or commit, not a moving default branch;
- contain the complete modified source and build materials corresponding to the binary.

The configured URL is a build-owned constant or generated release value, never a Fluent/YAML value or user input. The iOS host opens it on the main thread using the system browser. The opener rejects non-HTTPS URLs, user information, fragments that change the source target, localhost, IP literals, unapproved hosts, and paths that do not identify the configured repository and immutable revision.

If the URL is absent or the system cannot open it, the local `SOURCE_INFORMATION` remains available and the UI displays a localized explanation. Development builds may use this fallback. Personal distribution, PublicClean distribution, IPA packaging, and release audits fail when the immutable public source URL is absent or invalid.

## Packaging and provenance

The five About documents are unconditional bundle resources for both personal and PublicClean configurations. Their logical names are exact and build processing must not transform their final bytes. `AUTHORS` and `COPYING` are copied directly. The other three are produced in an isolated intermediate directory by deterministic generators from versioned inputs, then audited before bundle assembly.

The public content manifest and legal SBOM record the bundle/source path mapping, SHA-256, size, provenance category, license classification, and public-distribution decision. No directory-wide wildcard exemption is introduced. Incremental output is cleaned or audited so stale legal files cannot survive under alternate names.

The generated iOS application registry is refreshed only if the new logic requires it. The generator coverage test and byte-level registry diff are authoritative; entries are not edited manually to silence a test.

OpenRA engine changes are delivered in the next sequential engine patch after all importer and PublicClean runtime patches that precede this deferred feature. Its baseline is the strict replay tree produced by that immediate predecessor. Historical patches remain unchanged. The About patch has an exact path allowlist, strict no-fuzz application, duplicate-forward rejection, reverse dry-run success, and byte-for-byte replay comparison.

## Error handling and security boundaries

- Document selection is enum-based and fail-closed.
- Bundle path resolution must prove the final regular file is inside the expected app resource root.
- Symlinks, path traversal, duplicate logical resources, alternate filenames, and hash mismatches are rejected.
- External URLs are fixed release metadata and validated before use.
- Document text is never interpreted as markup, commands, or URLs.
- Missing legal/source material blocks distribution rather than silently removing an action.
- User-visible errors are localized; underlying exceptions and paths go only to diagnostics.

## Verification

The implementation follows test-driven development and must include:

1. Settings behavior tests proving six iOS tabs, five desktop tabs, About-only reset suppression, correct active-panel transitions, and no restart/dirty state.
2. Device-matrix layout tests covering all supported iPhone/iPad snapshots, safe areas, rotation, UI scale, containment, non-overlap, 48-point targets, scroll behavior, and idempotent reapplication.
3. Fluent tests for exact English and Chinese content, fixed modification date, and measured button/text fit with the real iOS fonts.
4. Document-policy tests for every valid document and tamper cases covering missing files, wrong hashes, symlinks, traversal, unknown identifiers, duplicate resources, and non-regular files.
5. Source-link tests for the immutable GitHub URL contract, main-thread host handoff, missing-link fallback, and rejected schemes/hosts/revisions.
6. Dependency-inventory tests that compare the final managed/native package graph with `THIRD_PARTY_LICENSES` and fail on unresolved or stale provenance.
7. Personal and PublicClean bundle audits proving the five document bytes, manifest/SBOM mappings, absence of stale alternatives, and release failure without a configured immutable source URL.
8. AOT registry coverage, YAML/Fluent validation, focused tests, the complete C# suite, packaging discovery, Release builds, strict replay of the new sequential About patch, and final signed personal/PublicClean iOS bundle audits.

The signed-artifact checks are mandatory distribution gates. If signing credentials or an eligible device are unavailable, implementation tests may complete but release acceptance remains explicitly blocked. No completion claim is made while the corresponding-source URL, a shipped dependency's provenance, or a required signed artifact gate remains unresolved.

## Non-goals

- No desktop About settings page.
- No changes to the existing desktop Credits window.
- No embedding or downloading of Red Alert 2/Yuri's Revenge retail assets.
- No GitHub repository creation, remote addition, push, or release publication without explicit user authorization.
- No web view, arbitrary browser, or user-configurable legal/source URL.
- No rewriting, translating, shortening, or replacing the original OpenRA `AUTHORS` or `COPYING` files.
