# iOS/iPadOS Retail Content Import Hardening Design

Date: 2026-08-28
Status: Recommended design selected and implementation planning authorized by the user

## Goal and release order

Turn the existing iOS/iPadOS Red Alert 2 content importer from a filename-checking prototype into a safe, recoverable importer that produces content capable of starting a real game.

The pre-game import/readiness gate is unconditional for both personal and PublicClean iOS/iPadOS builds. The compile-time distribution profile controls only which engine/project resources and mod search roots are allowed; it cannot bypass retail archive, map, receipt, profile, or recovery validation. Desktop startup and its content installer behavior remain unchanged.

The release order is fixed:

1. harden and verify retail-content import;
2. close the separate PublicClean runtime-resource gaps;
3. finish the iOS About/legal/source page;
4. prepare and verify a clean public source repository;
5. publish an immutable GitHub source tag and only then publish the matching app.

The source tree may be prepared locally during the earlier stages, but no GitHub repository, remote, push, tag, or public app release is created without separate user authorization.

## Current behavior that must not ship

The current implementation is not an acceptable release gate:

- it treats the presence of `ra2.mix` and `language.mix` as a complete validation;
- tests accept text files named `.mix` as valid content;
- hashes are calculated but not checked, persisted, or used at startup;
- Base and Yuri buttons run the same full-replacement operation;
- adding Yuri, music, or maps can erase previously valid content;
- all selected directory files are flattened and unrelated files make a full directory selection fail;
- import has no byte progress, active cancellation, single-task lock, or reliable retry;
- required package mount failures are swallowed;
- imported maps are copied to a directory that the game does not read;
- a clean PublicClean bundle still lacks independent runtime resources required to start the RA2 mod.

The new design replaces these behaviors rather than adding more filename checks around them.

## Considered approaches

### A. Incremental transactional native importer with engine validators — selected

Keep the iOS document picker and native pre-game experience, but split discovery, validation, map conversion, transaction publishing, readiness, and UI state into isolated components. Reuse OpenRA MIX/audio/map parsers rather than the current filename-only catalog.

This preserves the best iOS Files/iCloud/external-drive experience, supports a full installation directory or selected files, and gives the strongest recovery and test boundaries.

### B. Replace the native flow with the desktop `ra2-content` installer mod

This reuses more existing installer actions, but the installer is not touch- or safe-area-ready, cannot currently start reliably in PublicClean, does not own iOS security-scoped URLs, and still lacks the required deep validation. It is not selected.

### C. Require users to prepare one exact archive or folder layout externally

This is smaller to implement but transfers complexity to users, prevents straightforward installation-folder import, and still requires trustworthy validation. It is not selected.

Downloading, embedding, or repackaging retail assets is not an option. The repository contains no legally approved RA2 `Original Demo Assets` package, and the freeware C&C/Red Alert packages cannot be reused as RA2 content.

## Supported sources

The first release supports both choices approved by the user:

- individual extracted retail data files selected in the iOS Files picker;
- a complete extracted Red Alert 2 or Yuri's Revenge installation folder selected in the Files picker.

Supported providers include local Files storage, iCloud Drive, Finder file sharing, and external storage exposed through the system picker. The retail-content source importer itself makes no network request and never uploads selected retail content; this does not prohibit the separately bounded community-map service described below. A Files provider may download a user-selected cloud item under the provider's own system UI and policy. Reads occur through `NSFileCoordinator` or an injectable equivalent while the security scope is held. Provider preparation/materialization is an indeterminate phase with typed unavailable/download-required failures. Unknown-length streams are copied under a running hard byte cap, repeated space checks, and post-copy size/digest verification; a source that changes during the coordinated copy is rejected.

The first release does not directly open ISO images, ZIP/RAR/7z archives, Westwood CAB installers, or The First Decade CAB/HDR media. Selecting one produces a specific localized `extract the installation first` explanation. The existing desktop First Decade manifest bug is tracked separately; it is not presented as working iOS support.

## Content groups

Content is modeled and receipted in independent groups, then batch-committed for each user selection:

| Group | Required files or result | Readiness role |
| --- | --- | --- |
| RA2 Base | `ra2.mix`, `language.mix`, plus their required nested package graph | Required |
| RA2 Music | `theme.mix` | Optional |
| Yuri | `ra2md.mix`, `langmd.mix`, plus their required nested package graph | Optional, all-or-nothing |
| Yuri Music | `thememd.mix` | Optional |
| Maps | At least one safe, converted, loadable multiplayer `.oramap` | Required for a playable launch |

Base speech and audio are validated inside `language.mix -> audio.mix -> audio.bag/audio.idx`; they are not modeled as loose root `audio.bag/audio.idx` files. The existing misleading `Audio` state is replaced by separate Base Music and Yuri Music states.

Yuri content is optional. A partial Yuri pair is reported precisely and is not mounted, but it never disables an already valid RA2 Base installation. Adding Yuri or either music group never requires selecting Base again.

Yuri music is not marked usable merely because `thememd.mix` exists. The RA2 music definitions must be wired and validated to expose the supported Yuri tracks; until then the group remains present-but-unavailable instead of falsely reporting Ready.

### Trusted runtime profiles

The first release uses two explicit, tested manifest entries inside the same trusted canonical `ra2` package rather than adding a mutable runtime overlay. This runtime profile is independent from the compile-time personal/PublicClean distribution profile:

- `mod.ios-base.yaml` registers only the original RA2 rules, factions, actors, weapons, voices, sequences, and content packages;
- `mod.ios-yuri.yaml` registers the complete RA2 + Yuri's Revenge definition set only after the complete Yuri group validates.

Both entries retain manifest/mod ID `ra2`, use the same package, metadata version, map folders, network identity, and canonical map identity. Imported and converted maps therefore retain `RequiresMod: ra2` and remain compatible when a later launch moves from Base to the complete profile. Desktop startup still reads the unchanged default entry name `mod.yaml` and sees no new selectable mod. The engine accepts an alternate manifest entry only as a trusted root-level filename inside the already selected package, rejects separators/absolute/traversal names, and keeps that choice across same-mod reinitialization. The iOS host maps the closed runtime-profile enum to the two literal entry names; receipts and user input never supply a path or filename.

Each resolved iOS manifest exposes a deterministic runtime-contract ID. Its canonical, versioned byte stream uses fixed UTF-8 encoding and ordinal path order over the selected entry/fragments, every referenced synchronization- or loading-relevant YAML/Fluent/resource contract, filename catalog, and map format. The managed-image identity set is every app-local managed `.dll` in the finished bundle, without a framework-name exclusion. It therefore includes all OpenRA/NukeHour project assemblies—at minimum `OpenRA.Game.dll`, `OpenRA.Mods.Cnc.dll`, `OpenRA.Mods.Common.dll`, `OpenRA.Mods.RA2.dll`, `OpenRA.Platforms.Default.dll`, and `OpenRA.iOS.dll`—plus every shipped framework and third-party managed dependency. For each image the contract records canonical bundle filename, final SHA-256 and PE module MVID, and it also records the generated AOT-registry/build identity; assembly name/version alone is insufficient. A finished-bundle audit fails on any missing, extra, duplicate, non-managed, or unparseable `.dll` relative to that exact inventory. Changing code or rules behind an unchanged list therefore changes the contract, while a deterministic rebuild from identical inputs produces the same ID. Profile ID plus the full contract digest is written into the active receipt, network/server session handshake, new replay metadata, new save metadata, and profile-scoped skirmish/settings storage. Peers with different digests fail before lobby admission. Saves/replays are listed and loaded only under the exact matching profile/digest; an iOS profiled build hides legacy files with no digest and reports why instead of guessing. The default desktop `mod.yaml` preserves existing legacy persistence behavior. Base-to-Yuri and Yuri-to-Base tests prove maps remain physically shared while incompatible saves, replays, skirmish presets, and peers remain isolated.

The three manifest entries (`mod.yaml`, `mod.ios-base.yaml`, and `mod.ios-yuri.yaml`) directly include source-controlled fragments for common metadata, Base gameplay, Yuri gameplay, desktop filesystem, and the two iOS filesystem contracts. Included fragments do not recursively include other files. `mod.yaml` preserves current desktop composition; the two iOS entries share common/Base fragments and differ only by explicit Yuri additions and mounted retail packages. The Base definition is curated by resource-dependency audit, not name filtering: it retains the original RA2 Yuri unit and voice while excluding Yuri's Revenge-only factions, units, sequences, and assets. The audited `rules/ai.yaml` contains only Base-era definitions (including the original RA2 `yuri` unit) and remains one shared common file; mixed world, defaults, proxy actors, vehicle/voxel sequences, and voice definitions are split into positive Base and Yuri fragments. A source-of-truth drift test resolves all three entries and requires every difference to be classified as a deliberate profile difference, while also failing if a future Yuri's Revenge-only actor is added to the common AI file without review.

The current canonical manifest unconditionally parses Yuri's Revenge definitions, so simply omitting optional MIX mounts is not a valid Base profile. A complete validated Yuri group makes the Yuri context eligible but does not select it by itself. Before commit, the fully validated staged import plan tests each final map under every available trusted context, then chooses Yuri only when the complete Yuri group remains valid and at least one final map is Yuri-compatible; otherwise it retains any valid Yuri bytes as inactive optional content, selects Base, and requires at least one Base-compatible map. Only after the whole batch commits does the receipt become authoritative for the next launch. `AppDelegate` accepts only the fixed profile IDs and derives the manifest entry from that verified active receipt; no map, setting, unrestricted command-line value, or user-supplied data can override it.

Both profiles receive independent startup, map-load, sequence-load, lobby-faction, and skirmish tests. The receipt records the profile ID, a digest of the exact manifest contract, canonical mod/map format versions, and the filename-catalog digest in addition to validator versions and file hashes.

### Pre-game engine context

Map conversion and profile validation occur before `Game.InitializeAndRun` through a reusable pre-game context. A shared engine startup helper configures EngineDir and SupportDir once, permits a later request only when the normalized paths are byte-for-byte identical, initializes the settings/key context needed by `ModData`, discovers the fixed bundled mods, and returns an owned no-render `ModData` scope. Normal startup reuses the same directory configuration instead of calling one-shot override APIs again. Different paths, partial initialization, or use after disposal fail closed.

The pre-game scope is single-threaded and disposed before normal game startup. Legacy types that still require `Game.ModData` may use one save/set/restore adapter only inside that scope and always under `try/finally`. This adapter is forbidden after `Game.InitializeAndRun` begins. The target map conversion API uses explicit dependencies so the adapter can be removed rather than expanded.

Candidate validation never points SupportDir at staging and never changes the active catalog. The importer builds a read-only candidate package that maps each canonical logical filename to either the preserved active file or the validated staged replacement. During candidate `ModData` construction, a scoped loader factory mounts only that closed package at the manifest's `content` mount point; all other format loaders are delegated unchanged. The mapping rejects duplicate/unknown names and paths outside the active or current transaction roots. The scoped loader and candidate package are disposed before commit and the normal loader is restored in `finally`.

Committed retail content and maps live in private, immutable, transaction-generation directories under Application Support, not in the user-visible Documents support tree. The atomic catalog pointer selects one generation; generation contents are never edited in place. After cold hashing and contract validation, the startup gate constructs an active-catalog view from verified open file identities/handles and exposes only the exact receipted archive and map names. The normal iOS filesystem loader mounts this view instead of the raw generation or `Content/ra2` directory. The two iOS manifest entries point their user map folder to the catalog's explicit read-only map package, so `MapCache` cannot scan raw support folders. Unreceipted files, stale epochs, quarantined optional groups, symlinks, hand-copied maps, and files replaced after verification are invisible. The validated view remains alive for the lifetime of `ModData` and is disposed only after unmount. Personal and PublicClean builds use the same rule; desktop file mounting remains unchanged.

Existing flat iOS content under the legacy Documents `Content/ra2` and user-map locations is never trusted or mounted directly. On first upgraded launch it is presented as a legacy migration candidate, copied through the same bounded staging/hash/structure/map validation pipeline, and activated only by a successful generation transaction. The previous files remain untouched until the new generation commits; unrelated or invalid legacy files are reported and left unmounted. Migration failure preserves both the legacy bytes and any previously valid generation.

## Definition of Ready

`Ready to Play` is stronger than `files copied`.

RA2 Base is ready only when all of the following are true:

1. both required top-level MIX files are present in the active content store;
2. they parse through the real OpenRA MIX loader;
3. every mandatory nested package declared by the active RA2 manifest can be resolved and mounted;
4. the BAG/IDX pair inside the nested audio packages validates;
5. a cold-start SHA-256 pass over every Base archive and every candidate published map matches its group receipt, while invalid optional groups/maps are isolated rather than exposed;
6. at least one map passes conversion, safety checks, supported-format loading, rules loading, and spawn validation under the Base profile;
7. the selected Base runtime profile loads without referencing Yuri-only content;
8. the current distribution profile's runtime-resource preflight reports that the engine-owned/project-owned side of the mod is present and mountable.

At least one launchable Base-compatible map must support a real skirmish with two or more player spawns. Mission-only maps, Yuri-only maps, and maps that parse but cannot create a playable Base lobby do not satisfy the Base gate.

Yuri activation is Ready only when Base is Ready, both Yuri archives pass their complete nested-package checks, and at least one final map is Yuri-compatible. A valid Yuri pair without such a map is retained as authenticated inactive optional content but reported Unavailable until a later transaction supplies a compatible map; it never produces an empty Yuri map view. Music readiness never controls Base launch readiness. A cold-start failure in Yuri, either music group, or one map marks only that optional group/item Invalid/Unavailable and excludes it from the active-catalog view. If Base plus at least one Base-compatible map still validate, the gate selects Base and permits launch while a journaled repair generation quarantines or deactivates the invalid optional data; it never deletes active files in place. A Base archive failure or loss of every Base-compatible map remains blocking.

Known retail hashes identify recognized editions. A structurally complete but unknown hash may be accepted as `Compatible, unrecognized edition`; it is never labeled as an official known version. Hash recognition is not a substitute for structural validation.

## Source discovery and selection safety

A platform-neutral discovery service receives explicit selections from the iOS picker and emits a deterministic import plan.

- Direct file selections and files discovered under a selected directory are distinguished.
- Directory traversal examines regular files only, does not follow symlinks, and applies explicit depth, entry-count, and total-byte limits large enough for a normal installation but bounded against provider loops or denial of service.
- Recognized retail basenames and supported map extensions become candidates.
- Unrelated files inside a selected installation directory, including executables, DLLs, configuration files, text files, and uninstallers, are ignored and summarized; they are never copied or executed.
- Directly selecting executable content is a specific error.
- Directly selecting an unsupported archive or installer is a specific `extract first` error.
- Directory enumeration and cloud-provider access errors become recoverable results; they cannot escape an `async void` callback.
- Full user paths and cloud-account identifiers never appear in UI, telemetry, or ordinary logs.

Candidates are classified into content groups before any active content is changed. A same-name/same-hash candidate is deduplicated. Same-name/different-hash retail archives are a conflict and require a new selection; the importer never silently chooses or overwrites one. Converted maps use content digests for identity so equal maps deduplicate and different maps with the same display name can coexist safely.

## Validation pipeline

Validation proceeds through explicit phases:

1. discover and classify candidates;
2. calculate the copy plan and required temporary space;
3. copy candidates into a unique staging transaction while computing SHA-256;
4. re-read staged size and digest;
5. run bounded envelope validation before any existing MIX, IDX/BAG, or ZIP loader sees untrusted input;
6. parse top-level and nested MIX archives with hardened OpenRA loaders;
7. validate required nested package names against the candidate profile manifest contract;
8. validate BAG/IDX relationships and readable audio indexes;
9. select the candidate Base or Yuri profile and prove its restricted required mounts and runtime-contract digest;
10. discover, convert, safely copy, and load maps under that candidate profile;
11. run final candidate readiness and the static runtime-resource preflight;
12. prepare deterministic group receipts, catalog, and a complete proposed immutable generation;
13. revalidate the proposed generation's profile, required mounts, maps, contract, receipts, and readiness, then durably mark it `Prepared`;
14. atomically publish the complete affected batch by switching the generation pointer;
15. reopen and verify the selected active generation; any post-switch failure enters mandatory recovery and restores the complete previous generation rather than accepting a partially valid active view.

The validator does not depend on mutable global UI state. Its inputs are an immutable import plan, explicit manifest contract, explicit destination roots, validator version, and cancellation/progress interfaces.

Tests use project-generated synthetic MIX, BAG/IDX, and map fixtures containing no Electronic Arts data. A private opt-in integration fixture may point to a legally owned local installation, but its files, paths, hashes, and derived assets never enter Git or public artifacts.

The bounded envelope policy is versioned and checked before allocating from untrusted counts or declared sizes. It uses checked arithmetic and hard limits for source bytes, nested depth, entry counts, index bytes, individual and aggregate expanded bytes, normalized-name/hash uniqueness, compression ratio, elapsed time, and cancellation. IDX validation proves its header/count table fits the real file, bounds the sound count, validates unique legal names and fields, and proves every BAG `offset + length` is inside the BAG. ZIP validation parses the central directory before the existing loader, rejects unknown/negative/inconsistent sizes, duplicate normalized paths, traversal, unsupported entry types, and expansion bombs. The hardened runtime loaders map truncated input, overflow, out-of-memory guards, and invalid offsets to typed corruption errors rather than swallowing them.

### `IosImportEnvelopePolicyV1` limits

Version 1 freezes the following binary-size and count limits; implementations cannot substitute larger values without a validator-version change and review:

| Boundary | V1 limit |
| --- | ---: |
| picker roots | 64 |
| discovery depth / entries / apparent bytes | 24 / 100,000 / 32 GiB |
| recognized candidate files / total candidate bytes | 4,096 / 8 GiB |
| one retail source or unknown-length coordinated copy | 2 GiB |
| MIX nesting / entries per archive / aggregate entries | 8 / 131,072 / 262,144 |
| MIX index / local filename DB / local DB names | 16 MiB / 8 MiB / 262,144 |
| one regular MIX segment accessor, including `audio.mix` / one streamed BAG leaf / aggregate addressed MIX bytes | 512 MiB / 384 MiB / 8 GiB |
| IDX file / sound records / encoded name bytes | 4 MiB / 65,536 / 16 ASCII bytes |
| BAG file / one indexed sound span | 384 MiB / 256 MiB |
| traditional map source / width / height / cells | 32 MiB / 512 / 512 / 262,144 |
| map actors / player definitions / player spawns / imported maps per catalog / runtime-downloaded maps | 8,192 / 63 / 8 / 256 / 128 |
| `.oramap` compressed bytes / entries | 64 MiB / 8 |
| `.oramap` one expanded entry / aggregate expanded bytes / ratio | 64 MiB / 96 MiB / 100:1 |
| map YAML bytes / nodes / nesting depth / line bytes / scalar bytes | 8 MiB / 131,072 / 64 / 256 KiB / 64 KiB |
| map PNG width / height / pixels / decoded bytes | 4,096 / 4,096 / 16,777,216 / 64 MiB |
| map PNG chunks / aggregate IDAT bytes / secondary inflate ratio | 4,096 / 32 MiB / 100:1 |
| imported map compressed storage / resident map metadata | 2 GiB / 64 MiB |
| resident untrusted strings / string entries / per-map string bytes | 32 MiB / 262,144 / 8 MiB |
| CPU decoded-preview LRU / GPU preview atlas | 64 MiB / 128 MiB |
| GPU preview thumbnail edge / preferred atlas page / atlas margin | 1,024 / 2,048 / 1 pixel |
| remote metadata response / requested UIDs / records / one record | 4 MiB / 256 / 256 / 3 MiB |
| remote ordinary line / recognized encoded-binary line / nodes / nesting depth / fields per record | 64 KiB / 2.25 MiB / 16,384 / 32 / 64 |
| remote text scalar / encoded binary field / decoded binary field | 16 KiB / 2 MiB / 1.5 MiB |
| remote minimap width / height / pixels / decoded bytes | 1,024 / 1,024 / 1,048,576 / 4 MiB |
| remote players / spawns / categories per record | 64 / 8 / 64 |
| retained remote records / retained remote metadata+encoded-preview bytes / retained remote string bytes / string entries | 512 / 16 MiB / 4 MiB / 32,768 |
| runtime-map current generation / current plus rollback / queued downloads / queued staging bytes / free-space safety margin | 1 GiB / 2 GiB / 4 / 256 MiB / 512 MiB |
| package path UTF-8 bytes / depth | 64 / 1 |
| pending snapshot bytes / retention | 8 GiB / 7 days |
| provider preparation / discovery / one archive / one map / remote metadata / whole validation | 30 min / 10 min / 10 min / 5 min / 30 s / 90 min |

All addition/multiplication and offset calculations use checked 64-bit arithmetic before conversion to framework `int` APIs. A value exactly at a limit is accepted only if all structural checks pass; `limit + 1`, negative encodings, overflow, unknown declared expansion, or timeout fails with the typed boundary. Per-item validity never overrides an aggregate budget: planning accounts for every retained active, proposed, rollback, queued, decoded, uploaded, metadata, and interned-string allocation before commit or publication. Tests cover `limit - 1`, `limit`, `limit + 1`, integer-overflow forms, and many individually valid items crossing every aggregate row. The private legal-retail/device acceptance records only that real files fall within V1, never their paths or bytes.

The 512 MiB MIX limit applies to every ordinary segment accessor returned by `MixFile.GetContent`, explicitly including the intermediate `audio.mix` in `language.mix -> audio.mix -> audio.bag`; it is an `int`-conversion/allocation-safety boundary even though the current implementation returns a `SegmentStream`, not a promise that the complete entry is resident in memory. Larger declared ordinary entries are rejected before any `uint`-to-`int` conversion. A structurally identified and validated `audio.bag` is the sole 384 MiB leaf exception: it uses a dedicated long-length bounded streaming accessor, is never parsed as a nested MIX, and the hardened audio path reads only validated IDX spans through checked bounded streams. The 384 MiB leaf ceiling leaves 128 MiB inside the 512 MiB `audio.mix` ceiling for IDX, headers, indexes, and other validated entries, while the enclosing `language.mix` remains under the 2 GiB retail-source/container ceiling. All bytes still count toward the 8 GiB addressed-MIX aggregate, transaction disk planning, and cancellation/time budgets. Tests use a sparse or virtual complete `language.mix -> audio.mix -> audio.bag` fixture to prove exact 512 MiB intermediate-container and exact 384 MiB BAG-stream boundaries plus each `limit + 1` rejection without allocating those amounts in process memory.

## Map discovery and conversion

The importer automatically discovers supported multiplayer maps in the selected directory and in map-bearing packages exposed by validated retail archives. It also accepts separately selected `.map`, `.mpr`, `.yrm`, and `.oramap` files.

The large utility-only RA2 map command is refactored into a reusable conversion service with explicit dependencies. The CLI command becomes a thin adapter over the same service. The target conversion API does not read `Game.ModData` as an implicit global; it receives a restricted map-import context containing the current manifest, terrain data, rules, and output package factory.

A bootstrap validation context initializes only the engine/project data required to convert and load maps before `Game.InitializeAndRun`. It does not enter the RA2 main menu or create a game world. EngineDir and SupportDir bootstrap is idempotent for the same resolved paths, and the normal game startup reuses rather than conflicts with that initialization. Any remaining OpenRA types that still require `Game.ModData` are isolated behind one sequential scoped adapter that saves and restores the previous value in `try/finally`; parallel map conversion is forbidden until those implicit dependencies are removed. If the context cannot be initialized, map readiness is unavailable and the startup gate remains closed.

Traditional `.map`, `.mpr`, and `.yrm` sources enter the RA2 conversion service. A direct `.oramap` is not converted: it first passes the bounded ZIP/package envelope and an exact current-format root-file allowlist (`map.yaml`, `map.bin`, and optional `map.png`), then enters the same post-conversion map validation used for generated output. Any other entry, subdirectory, symlink, duplicate normalized name, embedded script, or custom data file is rejected. The validator bounds raw YAML bytes, line/scalar sizes, indentation depth, node count, and parsed object counts before calling `MiniYaml`; it preflights PNG signature, IHDR, dimensions, checked decoded stride/size, chunk count, aggregate IDAT bytes, and secondary inflate ratio before calling `Png`. The shared `Png` implementation and MIX content accessors are hardened with the same checked allocation guards so no other call site can reintroduce unchecked `Width * Height * stride` or `uint`-to-`int` allocation.

For every traditional candidate the conversion path:

- validates source size and format before allocating large buffers;
- converts into a unique staging output;
- applies the modern Gen2 map cordon/padding rules instead of the legacy RA2 utility bounds;
- treats unknown tiles, unsupported actors, invalid compressed lengths, impossible dimensions, and duplicate spawn definitions as explicit diagnostics instead of silently replacing data;
- rejects executable content, scripts, assemblies, native bundles, path traversal, duplicate package entries, symlinks, and unsupported embedded content;
- reads the legacy `MultiplayerOnly` source flag and rejects source maps that are not multiplayer candidates;
- emits the current map format, canonical `RequiresMod: ra2`, and `MapVisibility.Lobby`;
- loads the converted map, metadata, terrain, rules, and player definitions;
- requires a supported theater; unsupported Yuri theaters do not block an already valid Base map set;
- requires at least two valid multiplayer spawn points;
- writes only into the current transaction's staged map root and publishes it through the verified catalog-backed `MapFolders` package;
- keeps an existing valid map when a replacement or conversion fails.

Before constructing an OpenRA `Map`, both converted and direct `.oramap` metadata are parsed independently and rejected if any custom `Rules`, `Weapons`, `Voices`, `Sequences`, `ModelSequences`, `Music`, `Notifications`, or `FluentMessages` data is present. After construction the validator requires exact Lobby visibility, `InvalidCustomRules == false`, zero replaced invalid terrain tiles, valid bounded actors/owners/players, unique in-bounds spawn points, canonical mod compatibility, and successful current rules/terrain loading. A shell-map-only, mission-selector-only, hidden, or loader-fallback map does not satisfy readiness. Validation decodes at most one preview under the per-map budget and releases its CPU buffer before advancing. Untrusted map strings use a per-map bounded interner and only bounded immutable display metadata crosses into the catalog; they never enter the existing unbounded global `MapCache.StringPool`. Every map is tested independently in both trusted contexts when the required assets exist, and its receipt records closed `CompatibleProfiles` flags. The Base active view exposes only Base-compatible maps; the Yuri view may expose Base-compatible and Yuri-only maps. A catalog can never become Ready unless at least one map is Base-compatible, so later Yuri damage can still degrade safely to Base.

Normal `MapCache` enumeration is metadata-first and does not eagerly decode every preview image. The 4,096-pixel PNG edge is only a safe decode ceiling, never an atlas-upload size. Preview PNGs are decoded on demand through a byte-accounted CPU LRU, converted with checked arithmetic into an aspect-preserving thumbnail no larger than 1,024 by 1,024, uploaded through a separately byte-accounted GPU atlas, and the source CPU `Png.Data` buffer is released after conversion/upload unless the thumbnail remains within the LRU budget. The preferred atlas page is 2,048 pixels with a one-pixel margin; creation first proves the page does not exceed the device maximum texture size and every upload proves `thumbnail edge + 2 * margin <= page edge`. On a smaller supported page the thumbnail ceiling is reduced to `page edge - 2 * margin`; if allocation, conversion, or upload still cannot satisfy the checked bounds, the UI uses a fixed bundled placeholder while the validated map remains playable. `SheetBuilder` is hardened to reject any rectangle outside its page rather than relying on callers. Entries with live UI/game references are pinned; cache pressure or an iOS memory warning evicts only unpinned previews and regenerates them later from the verified immutable handle. Catalog planning rejects excess map count, compressed storage, resident metadata, or string budgets before pointer publication, so 256 individually legal worst-case maps cannot multiply into unbounded resident memory.

The converter records source and output digests in the receipt but does not log or publish the retail source path. The final logical map destination is the new generation's catalog map package, whose version/identity is derived from the canonical manifest contract; conversion never writes into the currently mounted `MapFolders` package or a hard-coded `{DEV_VERSION}` directory. Map failures are reported per map; one bad optional map does not erase other valid maps.

MIX indexes contain hashes rather than reliable filenames. Embedded-map discovery uses a versioned, audited RA2/YR filename catalog together with any valid `local mix database.dat` found in the archive. It never brute-forces hashes. Recursion depth, entry count, aggregate uncompressed bytes, compression ratio, and processing time are bounded. If the existing `global mix database.dat` is used, its pinned OpenRA source, digest, and distribution classification must be recorded; otherwise a minimal project-owned resolver replaces it.

### Verified community-map downloads

The prohibition on downloading retail/Demo assets does not remove the existing community-map download feature. When map downloading is enabled by the fixed distribution policy, iOS routes `MapPreview.Install` through a `VerifiedRuntimeMapStore` instead of asking the read-only catalog package for `IReadWritePackage`.

Remote discovery is also placed behind a `VerifiedRemoteMapMetadataClient`; hardening does not begin only after the binary download starts. The client applies the V1 response, request-count, record, line, indentation, node, field, scalar, collection, encoded-byte, decoded-byte, PNG, cancellation, and timeout limits before constructing any `MiniYaml`, Base64 buffer, `Png`, player list, rule object, or preview. A byte scanner recognizes only the exact `minimap`, `players_block`, and `rules` binary-field keys at the expected record depth. Those lines have the separate 2.25 MiB raw-line ceiling so a 2 MiB encoded value plus bounded key/indentation syntax is reachable; every other line remains under 64 KiB. The scanner removes all three fields before `MiniYaml`: `rules` and `players_block` are ignored on iOS, while `minimap` alone is decoded through the 2 MiB encoded/1.5 MiB compressed limits and then the remote PNG envelope. Lookalike keys, duplicate binary fields, invalid indentation, or a binary field at another depth reject the record. The client accepts only canonical UIDs that were present in that exact request and a closed allowlist of basic display fields. iOS never decodes or applies remote custom-rules payloads; a remote result remains non-authoritative display metadata until its downloaded package passes the full local validator. Parsing occurs off the game thread, but the bounded immutable result is committed to UI/`MapCache` state only on the game thread.

The store streams the response to a random same-volume temporary file under the V1 compressed-byte and time limits, reports real received/declared byte progress when a trustworthy denominator exists, and ignores `Content-Disposition` as a filesystem name. The only final logical name is derived from the requested canonical map UID. Before publication it applies the exact `.oramap` envelope/file allowlist, rejects custom rules/scripts, constructs the map under the current explicit profile context, requires Lobby visibility and profile compatibility, recomputes the actual UID, and requires it to equal the requested UID. It then publishes through its own immutable generation/pointer and adds the validated package to the composed runtime map view; the current `MapCache` preview is updated only after that success. Incomplete, oversized, UID-mismatched, invalid, cancelled, or interrupted downloads remain invisible and preserve the previous view.

The retail active catalog and filesystem mounts remain immutable for the entire loaded-mod lifetime. `VerifiedRuntimeMapStore` is the sole, narrow exception: it never changes the retail mount set, profile, runtime-contract ID, or `Game.ModData`. On the game thread it atomically replaces a dedicated immutable runtime-map snapshot consumed by `MapCache`. The previous snapshot, generation, package handles, and streams remain alive until every `MapPreview`/`MapCache` reference releases them; garbage collection is delayed and lock-protected. The store has its own serialized download queue, per-request cancellation token, checksummed generation receipt and pointer, cold-start full-hash validation, crash recovery, and orphan cleanup. Concurrent requests for the same UID coalesce; different UIDs are queued only within the fixed count and staging-byte budgets. Before download and again before pointer swap, it reserves the current-generation, retained-rollback, staging, catalog-count, preview-cache, and disk-safety budgets. At quota it evicts only unpinned, non-current, re-downloadable maps from an old generation; it never evicts the selected map, the last verified snapshot, or a generation with a live handle. If safe eviction cannot satisfy the budget, the request fails with a localized quota error and leaves the view unchanged. A failed recovery keeps the last verified runtime snapshot or exposes no downloaded maps, never a partially published generation.

Runtime-downloaded maps never count toward initial Base Ready, so a fresh install still requires one imported Base-compatible map. They use the same backup-exclusion/file-protection policy and are partitioned by runtime-contract ID. The existing map-readiness publisher continues to report Downloading, Validating, Ready, and failure reason/progress, and every start path remains blocked until the requested map reaches Ready. iOS editor save/delete actions that require a raw writable MapFolder are hidden unless and until they are explicitly adapted to the verified store.

## Transaction, recovery, and receipts

Only one import transaction may run in the process. A second request is rejected before it can clean or reuse another transaction's staging directory.

Each content group is staged and validated independently, but every group affected by one user selection is published as one batch. The validator first constructs and verifies the complete resulting catalog, including preserved active groups and maps. If replacing Base, adding Yuri, or changing maps would leave that resulting catalog without a valid required profile and at least one compatible playable map, nothing from that selection is activated.

Staging, immutable generations, pointer, journal, and receipts live on the same filesystem volume. A new generation is built under an unreferenced unique directory by copying or filesystem-cloning preserved verified files (never hard-linking or editing them) and moving validated staged replacements/maps into their canonical generation paths. Publishing uses a versioned, checksummed journal with a transaction epoch, the previous generation ID, the proposed generation ID, and only closed group enums plus canonical relative filenames. Absolute paths, empty components, alternate separators, `.`/`..`, symlinks, and unknown versions/states fail closed. Journal/catalog/receipt/pointer files are written to unique temporary files, flushed to stable storage, atomically renamed, and followed by the platform metadata/directory durability operation before the next state transition.

Copied-but-not-yet-validated data uses a separate non-authoritative `PendingValidation` snapshot, sealed by an exact plan/hash manifest. It is not a publish journal state and cannot select a runtime profile. Cold startup always resolves a real publish journal first, then may validate a complete pending snapshot; only successful candidate/profile/map validation creates `Prepared`. A complete sealed snapshot no longer depends on its original security-scoped URL. Only an incomplete copy is invalidated by lost source access. A failed pending snapshot is quarantined and never changes the active pointer. If the existing active view is Ready, the UI retains `Enter Game` and adds `Retry`, `View Details`, and `Discard Pending Import`; discard removes only that sealed snapshot under the import lock and revalidates active state. Without an existing Ready view, the startup gate stays closed.

The publish state machine is `Prepared -> Publishing -> Committed`:

- the complete proposed generation, all group receipts, and its catalog are durable and fully revalidated before `Prepared`;
- `Publishing` performs only the atomic replacement of the small active-generation pointer;
- the previous immutable generation remains the rollback snapshot and is never modified;
- any synchronous failure before pointer replacement leaves the previous pointer authoritative; a failure after replacement is resolved from the pointer and journal epoch;
- startup recovery inspects unfinished journals before readiness checks;
- recovery either keeps/restores the previous pointer or confirms the complete proposed generation; it never guesses per group;
- a truncated, checksum-invalid, or path-invalid journal enters the fixed-name fail-closed recovery decision below and preserves both generations plus staging; startup may continue only if the independently authenticated active pointer and everything it references pass that decision, otherwise the gate remains closed; an unknown newer journal version always reports the update-required state;
- old generations are garbage-collected only while holding the import lock, after the committed generation, every receipt, active view, and a later cold-start post-commit validation pass; the newest valid rollback generation is retained until then.

Recovery is mandatory and cannot be cancelled by the UI. A process kill at any generation materialization, journal flush, receipt/catalog flush, pointer swap, post-commit validation, or garbage-collection boundary must converge to the complete previous or complete proposed generation on a later launch. This avoids modifying the mounted generation when adding one optional group while preserving atomicity across Base and Maps. Partial Yuri content is never committed. A failed map transaction cannot damage Base archives.

A checksum/path-invalid journal is never parsed for paths or operations. It is moved by its fixed well-known filename into an app-owned quarantine name. If the active pointer, referenced generation, catalog, and receipts independently pass complete validation, that pointer remains authoritative and startup continues with a warning. If they do not, the UI offers a confirmed `Reset Imported Content` action that atomically quarantines only the fixed importer root and returns to Missing; it never follows journal data or touches external selections. A journal with a newer unknown version instead reports `Update NUKE HOUR to finish recovery`; reset remains a separately confirmed escape hatch. Tests cover corrupt journal plus valid pointer, corrupt journal plus invalid pointer, future version, reset, and proof that outside files remain byte-identical.

Receipts are stored outside the mounted retail file namespace and contain only canonical relative names, group, size, SHA-256, recognized-edition identifier when available, profile ID, manifest-contract digest, filename-catalog digest, validator/converter/map-format versions, map UID/output digest, transaction epoch, validation result, and commit identifier. They contain no external paths or account data.

Startup uses the receipt as a cache, not blind proof. Every cold start recomputes SHA-256 for active top-level retail archives and maps even when size and timestamps match. Only matching hashes plus matching profile/manifest/catalog/validator versions may skip the expensive nested structural pass. A missing file, digest mismatch, or mount failure invalidates and structurally revalidates the affected item/group. The global gate closes only when Base is invalid, a mount required by the newly selected profile is invalid, or no Base-compatible map remains. Corrupt/unmountable Yuri, music, and individual maps are quarantined from the view and the profile is recomputed as described above. Required mount exceptions are no longer swallowed; only explicitly validated optional groups may be omitted.

## Progress, cancellation, retry, and lifecycle

The import core reports a typed immutable progress snapshot:

- phase: PreparingSources, Discovering, Planning, Copying, VerifyingActiveHashes, ValidatingEnvelopes, ValidatingArchives, ValidatingAudio, ConvertingMaps, LoadingMaps, DownloadingMap, ValidatingDownloadedMap, SelectingProfile, ValidatingMounts, CheckingRuntimeResources, Committing, or Recovering;
- current sanitized filename or content group;
- completed and total items;
- completed and total bytes when meaningful;
- integer percentage only when the denominator is known.

Provider preparation, discovery, structural validation, conversion, profile/mount checks, runtime-resource checks, and recovery use an indeterminate indicator rather than a fabricated percentage. Copying and active SHA-256 verification report real completed/total byte progress when their denominators are known. Unknown-length provider streams remain indeterminate while enforcing a hard running byte cap and repeated free-space checks.

The coordinator owns one cancellation token source for the active transaction. During cancellable discovery/copy/validation/conversion work, all import/start/retry buttons are disabled and only Cancel remains enabled. `Committing` and `Recovering` are non-cancellable: they show the exact phase, reason, and indeterminate progress, with no action button. Cancel, backgrounding, picker access loss, or application interruption never publishes partial content and never removes the previous Ready set.

Retry is semantic:

- if a complete staged snapshot remains and the failed operation is safely repeatable, retry resumes validation/conversion without re-copying;
- if staging is incomplete because source access was lost, the action is `Choose Again` and reopens the picker;
- `Check Again` revalidates active content and is not presented as retrying an import.

Retryable snapshots are sealed by a plan/hash manifest, have a size cap and expiry, and remain outside the active namespace. A complete sealed snapshot is self-contained and survives release of the source security scope; a partial snapshot is never retryable. Cancel deletes unpublished data for its own transaction unless a non-cancellable commit/recovery has begun. Startup runs journal recovery before validating any retry snapshot; orphan or expired staging is deleted only while holding the process-wide import lock and only when no journal references it.

Planning budgets staged source bytes, a complete proposed generation (including preserved-file copies/clones), worst-case converted map bytes, retained retry data, retention of the previous rollback generation, journal/receipt overhead, and a safety margin, then rechecks free space immediately before commit. Generation materialization and pointer publication stay on one volume; a cross-volume destination is rejected rather than silently downgraded to non-atomic copy.

Before normal game startup, foreground activation may resume the import UI but first runs non-cancellable journal recovery and then recomputes readiness. After `Game.InitializeAndRun` begins, foreground activation may only resume UIKit and an incomplete pending copy; it cannot recover/publish active content, switch profile, convert maps, reconstruct `ModData`, or re-run the startup gate. Security-scoped access is held only while enumerating and copying the selected sources and is always released exactly once.

## iOS/iPadOS user experience

The startup wizard is shown when minimum playable content is missing, invalid, or recovering, or when a quarantined/pending import requires an explicit Retry, Details, or Discard decision. When the active RA2 Base, one Base-compatible map, and the runtime-resource preflight remain Ready, that pending-decision state keeps `Enter Game` available alongside its resolution actions; it never demotes the verified active catalog. With no pending decision, a Ready active catalog enters the main menu normally. Optional Yuri/music omissions are shown without blocking Base.

The page is a safe-area-constrained `UIScrollView` containing:

- a title and concise legal-content explanation;
- separate Base, RA2 Music, Yuri, Yuri Music, and Maps status rows;
- current phase, real progress, and sanitized failure details;
- state-specific actions rather than six permanently visible buttons.

Compact landscape iPhones use a one- or two-column action grid; iPads may use wider columns. Every action is at least 48 points, supports Dynamic Type, wraps without truncation, and remains reachable at supported accessibility sizes.

Recommended actions by state are:

- Missing Base/Maps: `Choose Files or Folder`, `View Licenses`;
- Importing: `Cancel` only;
- Recoverable failure: `Retry` when valid staging exists, otherwise `Choose Again`, plus `View Details`;
- Failed pending optional import with active Ready content: `Enter Game`, `Retry`, `Discard Pending Import`, and `View Details`;
- Base Ready, optional content missing: `Enter Game`, `Import Optional Content`;
- Fully Ready: `Enter Game` and `Manage Game Content`.

Optional content remains accessible after startup through an iOS-only `Manage Game Content` action from the main menu. It reuses discovery, copying, envelope validation, and the transaction format, and it is unavailable during a match. The first release never replaces `Game.ModData`, converts retail maps, changes the active profile, or mutates retail filesystem mounts while the game process owns a loaded mod. The only runtime publication exception is the separately isolated `VerifiedRuntimeMapStore` snapshot protocol above; it cannot alter retail readiness or the profile. An in-game retail-content selection is sealed as a pending transaction and displays `Restart to finish import`; the next application launch runs recovery, profile/map validation, and activation in the pre-game gate. There is no in-process mod hot reload in this release.

English and Simplified Chinese strings follow the existing System/English/Chinese preference policy. Raw exceptions and full external paths are diagnostic-only.

## Error model

The typed error model distinguishes at least:

- selection cancelled;
- source access expired;
- provider item unavailable or not materialized;
- source changed during coordinated copy;
- empty selection;
- unsupported archive/installer;
- directly selected executable content;
- discovery limit exceeded;
- duplicate conflicting archive;
- insufficient staging space;
- missing Base files;
- corrupt or unsupported MIX;
- missing nested Base package;
- invalid BAG/IDX pair;
- partial Yuri group;
- no playable map;
- map conversion failure;
- unsafe map content;
- downloaded map exceeds limits or UID/profile validation fails;
- profile unavailable or inconsistent;
- required content mount failure;
- runtime resource preflight failure;
- receipt mismatch;
- publish/rollback/recovery failure;
- corrupt or newer-version recovery journal;
- storage backup-exclusion or file-protection failure;
- cancelled active import;
- generic sanitized I/O failure.

The UI shows the failing group and safe missing basenames where useful. It never claims success while validation or recovery is incomplete.

## PublicClean runtime-resource boundary

Import correctness and PublicClean runtime closure are separate mandatory gates.

The importer owns only player-provided retail data and converted player maps. It must not solve missing engine/project resources by copying retail data into the app bundle or by weakening the public manifest.

After importer hardening, a separate focused design closes the known clean-bundle requirements, including shaders, required common/system packages, public-safe UI/load-screen/font resources, shell-map or no-shell-map startup behavior, the global MIX filename database when required, and removal or replacement of `$ts`/TS chrome dependencies that are not part of the public RA2 runtime. It also implements and tests a real consumer for PublicClean mode, or replaces the inert `Engine.PublicClean` argument with an explicit build-selected runtime manifest; the current argument alone is not treated as an active safety boundary.

That design must not solve the problem by adding the entire TS mod or blanket-allowing current RA2 `bits/uibits`. The current bundle is missing renderer GLSL, fonts, load-screen/UI resources, a usable shell map, and the TS asset-browser dependency is reachable from the RA2 manifest. Existing `.shp` resources are rejected by the public audit before manifest classification and therefore require a provenance-approved replacement format or a separately justified audit-policy change. A minimal project-owned shell map may be used only after its source and assets pass the public manifest.

The importer preflight is a static bundle/file/mount contract only. It cannot claim that renderer, fonts, load screen, shell map, or skirmish runtime behavior has been exercised; those claims require the separate clean-bundle runtime smoke gate. The UI calls the former `Runtime resources present` rather than `App runtime verified`.

The existing PublicClean audit baseline is a known-failure provenance baseline, not proof that the app launches. Final release requires both:

- importer end-to-end Ready with legally owned external content; and
- a clean PublicClean bundle whose engine/project resources pass provenance and runtime smoke tests.

The final App and IPA release audits are new zero-error gates. The historical 580-error fixture remains diagnostic history and is never redefined as a passing baseline.

No source or app release is described as playable until both gates pass on a fresh install.

## Security and legal boundaries

- No retail game asset is committed to Git, bundled in the app, uploaded by the app, downloaded by the retail importer, or written outside the private importer/runtime-map roots. Provider materialization and the separately bounded community-map service retain the explicit exceptions defined above.
- The importer never executes selected content or loads user DLLs, native libraries, scripts, or arbitrary mod rules.
- Directory-discovered executable and irrelevant files are ignored, never copied.
- Direct executable selections, executable magic under accepted extensions, unsafe map packages, symlinks, and traversal are rejected.
- All writes resolve under fixed support/map roots and reject escapes before I/O.
- Every staging, pending, immutable generation, catalog, receipt, retained rollback generation, converted-map, and legacy-migration destination is marked `NSURLIsExcludedFromBackupKey = true` and uses `NSFileProtectionCompleteUntilFirstUserAuthentication`. Creation, coordinated copy, rename, recovery, and migration reassert and verify both resource properties; inability to establish them is a typed storage-policy failure before publication.
- Logs contain group, basename, byte count, digest when appropriate, and error code, but no cloud path or account data.
- The app states that users must import content from a legally owned copy and that NUKE HOUR is not affiliated with or endorsed by Electronic Arts.

## Verification

Implementation follows test-driven development.

### Core tests

- file/folder discovery, ignored directory files, direct unsupported selection, symlink/traversal/provider errors, deterministic limits, and duplicate rules;
- synthetic valid/corrupt/partial MIX graphs and BAG/IDX pairs;
- known, compatible-unrecognized, corrupt, missing nested package, and receipt-stale outcomes;
- independent Base/Yuri/music/map receipts and preservation rules combined with whole-selection atomic commits;
- same-hash dedupe and different-hash conflicts;
- byte progress, indeterminate phases, single-task exclusion, and semantic retry;
- cancellation at every cancellable phase and explicit proof that commit/recovery cannot be cancelled;
- injected failures and real child-process termination at every generation materialization, journal/receipt/catalog flush, pointer-swap, post-commit validation, and garbage-collection boundary, automatic recovery, whole-batch rollback/roll-forward, and preservation of previous Ready content;
- same-size/same-timestamp content tampering, cold-start digest recheck, contract/version invalidation, retry snapshot expiry, and cross-volume rejection;
- active-catalog isolation from unreceipted maps/archives, stale generations, same-name post-hash replacement, raw support-directory files, and invalid optional groups;
- transactional migration from the legacy Documents content/maps layout without ever mounting it directly;
- Base/Yuri runtime-contract persistence across lobby handshakes, saves, replays, skirmish settings, legacy files, and both profile transition directions;
- cold-start optional Yuri/music/single-map corruption degrading to a valid Base view without exposing or deleting the invalid item;
- map discovery, conversion, unsafe package rejection, duplicate maps, load/rules/spawn validation, and destination correctness;
- direct `.oramap` envelope/allowlist/custom-rule rejection, expansion bombs, invalid terrain fallback, and cross-profile map compatibility;
- MIX `uint` entry lengths around the signed allocation boundary; YAML byte/node/depth/line/scalar bombs; PNG huge-IHDR, checked-stride overflow, chunk/IDAT/inflate bombs; and all V1 boundary rows at `limit - 1`, `limit`, and `limit + 1`;
- 256 individually valid worst-case imported maps, unique YAML-string floods, lazy preview decode/CPU release, 4,096-edge source downsampling, 2,048 atlas/page-boundary `-1`/exact/`+1`, device texture limits, rotation/memory warning, pinned eviction/re-upload, CPU-LRU and GPU-atlas eviction, safe placeholder fallback, and deterministic aggregate rejection without process OOM;
- remote-map metadata response/line/node/field/collection limits, excessive indentation, unsolicited or duplicate UID, Base64 and PNG bombs, remote custom-rules suppression, timeout/cancellation, and game-thread-only publication;
- community-map download streaming limits, untrusted response filename, exact UID/profile validation, progress, cancellation, interruption, immutable snapshot/handle lifetime, queue coalescing, repeated downloads through quota, current-plus-rollback space exhaustion, pinned-map non-eviction, cold-start receipt/hash recovery, and invisible failure;
- required mount failure propagation and optional-group omission.

### UI and platform tests

- state-machine transitions and exact action visibility/disabled state;
- no Enter Game path while an import, validation, map conversion, recovery, or runtime preflight is incomplete;
- iPhone/iPad safe-area and Dynamic Type matrices, scrolling, wrapped text, 48-point controls, rotation, background cancellation, and re-entry;
- security-scoped URL lifetime balance and all exception paths returning to the main thread;
- coordinated local/iCloud reads, provider materialization failure, unknown-length streams, source mutation during copy, and no retail-import-originated network/upload path; the separately bounded community-map client is tested only through its dedicated service;
- simulator/unit seams plus physical-device checks for backup exclusion and file protection after copy, rename, recovery, migration, and generation switch;
- English/Chinese content and measured fit;
- main-menu Manage Game Content lifecycle and deferred next-launch activation;
- in-game pending-import lifecycle, no runtime `Game.ModData` replacement, restart-required messaging, and next-launch activation.
- failed pending import with and without an existing Ready view, including safe discard and continued launch of the previous catalog.

### Bundle and end-to-end gates

- PublicClean contains no retail files before or after build;
- imported content remains only under the sandbox support/map roots and does not change app code signing;
- clean-device RA2 folder import reaches a real two-player skirmish;
- incremental Yuri/music import preserves Base and enables only validated optional content;
- cancellation, low space, corruption, duplicate conflicts, backgrounding, forced process termination, and recovery preserve the previous playable set;
- full C# and packaging suites, AOT coverage, Release builds, source/bundle audits, fresh signed-device build, and fresh-install smoke;
- an opt-in private retail fixture and a physical-device test may consume legally owned files, but evidence records only redacted status and never publishes the files or their paths.

## Delivery decomposition

This feature is implemented before the About/source-publication work in the following bounded milestones:

1. discovery, content model, validator contracts, and synthetic fixtures;
2. verified Base and Base+Yuri runtime profiles;
3. incremental transaction journal, receipts, recovery, and progress/cancellation;
4. reusable map conversion/load validation and correct map destination;
5. iOS coordinator/state machine and responsive UI;
6. in-game optional-content staging and next-launch activation;
7. private-fixture and physical-device import acceptance;
8. separate PublicClean runtime-resource closure and zero-error release audit;
9. About/legal/source page, GitHub preparation, immutable source tag, then app release.

Each milestone remains reversible and keeps unrelated user files, retail content, credentials, build artifacts, and historical engine patches out of commits.

## Non-goals for this design

- No network download of retail assets and no `Original Demo Assets` package; the separately bounded existing community-map download path is not a retail-content source.
- No direct ISO, CAB/HDR, ZIP, RAR, or 7z extraction in the first release.
- No desktop importer redesign.
- No executable mod/plugin import.
- No arbitrary map scripts or embedded rule overrides in PublicClean.
- No silent conflict resolution or destructive replacement of valid content.
- No in-process profile switch, map conversion, or mod hot reload after normal game startup.
- No claim that the current PublicClean provenance baseline is a passing runtime build.
- No GitHub publication or app release until the importer and separate runtime-resource gates pass.
