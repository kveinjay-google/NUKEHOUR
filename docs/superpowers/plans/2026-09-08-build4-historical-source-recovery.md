# Build 4 Historical Source Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reconstruct, verify, and document the exact source and build inputs corresponding to NukeHour iOS Build 4 without importing later development or exposing proprietary assets or secrets.

**Architecture:** Treat commit `6c17968ad62338ff0df1d1922af7d3a582f0a0db` as the immutable base, the signed Build 4 IPA and its PDBs as the primary binary evidence, historical task logs as the dirty-tree event record, and `/Users/kevin/Projects/ra2-build4-source-release` as read-only preserved recovery evidence. Materialize only evidence-supported files in this isolated worktree, record a per-file confidence ledger, and keep publication blocked until source, AOT, asset, secret, license, and reproducibility gates all pass.

**Tech Stack:** Git worktrees, .NET 8 iOS/Mono Full AOT, MSBuild, Portable PDB document checksums and embedded source, IL decompilation, SHA-256 manifests, SDL2/UIKit/OpenGL ES native integration, Python audit tooling.

---

### Task 1: Freeze the recovery evidence ledger

**Files:**
- Create: `docs/archive/BUILD4_SOURCE_MAPPING.md`
- Create: `docs/archive/evidence/build4-recovered-files.sha256`
- Read only: `/Users/kevin/Projects/ra2-build4-source-release`
- Read only: `/Users/kevin/Projects/ra2-mac`
- Read only: `/Users/kevin/Desktop/NUKE-HOUR-Build-4-signed.ipa`
- Read only: `/private/tmp/build4-pdb-listings/`
- Read only: `/private/tmp/build4-embedded/`

- [ ] **Step 1: Verify the write root before creating evidence files**

Run:

```bash
pwd
git rev-parse --show-toplevel
```

Expected: both lines are `/Users/kevin/Projects/ra2-build4-source-release-20260908-055841`.

- [ ] **Step 2: Record immutable identifiers and evidence hashes**

Record the base SHA, IPA SHA-256, bundle version, PDB hashes, preserved recovery worktree identity, and historical session-log paths in `docs/archive/BUILD4_SOURCE_MAPPING.md`.

- [ ] **Step 3: Hash every content-changing file in the preserved recovery worktree**

Generate `docs/archive/evidence/build4-recovered-files.sha256` from the exact union of `git diff --name-only`, `git diff --cached --name-only`, and `git ls-files --others --exclude-standard` in the preserved worktree. For staged deletions, record the base blob hash and the status `DELETED_IN_CANDIDATE` instead of reading a missing file.

- [ ] **Step 4: Verify the ledger is complete**

Expected: 48 modified tracked files, 18 staged deletions, 16 untracked files, 82 unique content changes.

### Task 2: Close the iOS host-source recovery gap

**Files:**
- Modify: `ios/OpenRA.iOS/AppDelegate.cs`
- Modify: `ios/OpenRA.iOS/ViewController.cs`
- Modify: `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs`
- Create: `docs/archive/BUILD4_IOS_HOST_RECOVERY.md`

- [ ] **Step 1: Verify the three unmatched PDB document checksums**

Expected Build 4 SHA-256 values:

```text
7a6751c2709dd0f88a5219731cbe3464558da7fec11aec3955c5e74bd4410ea1  ios/OpenRA.iOS/AppDelegate.cs
95482ba59b9ca03471d66c4dfd7ddb02b728e2e571c055824e533fb6ce2868db  ios/OpenRA.iOS/ViewController.cs
61f59a25dfc88373457fb0c16f596735d2938cb7c9a8992d3d78bad9d538af5d  ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs
```

Note in the report that only the first two files are handwritten; the registry is generated source.

- [ ] **Step 2: Reconstruct provenance from historical task events**

Extract every pre-build write event for the two handwritten files from the September 5 task transcript. Record timestamp, operation, predecessor hash, successor hash, and whether the event occurred before the 09:28 local build start. Exclude all later events.

- [ ] **Step 3: Cross-check the candidate source against compiled IL**

Decompile the Build 4 `OpenRA.iOS.dll` inside an isolated recovery-only tools/output directory. Compare the type/member set and method bodies for `AppDelegate` and `ViewController` against the preserved candidate. Record compiler-normalized differences separately from semantic mismatches.

- [ ] **Step 4: Materialize only exact-hash matches**

After the required root check, copy the preserved candidate files into this worktree only if their SHA-256 values match the PDB document checksums above. Any mismatch keeps the file `UNVERIFIED` and blocks materialization.

- [ ] **Step 5: Assign confidence**

Use these rules in `docs/archive/BUILD4_IOS_HOST_RECOVERY.md`:

```text
VERIFIED: candidate bytes equal the Build 4 PDB document checksum.
HIGH_CONFIDENCE: task chronology and IL agree, but no exact source checksum match exists.
PARTIAL: only a subset of methods or lines is supported.
UNVERIFIED: evidence conflicts or cannot establish the built bytes.
```

Expected current result: both handwritten files and the generated registry qualify as `VERIFIED` if the preserved candidate hashes remain unchanged.

### Task 3: Replay the evidence-supported dirty-tree snapshot

**Files:**
- Modify/Create: only paths listed in `docs/archive/evidence/build4-recovered-files.sha256`
- Create: `docs/archive/BUILD4_DIRTY_TREE_REPLAY.md`

- [ ] **Step 1: Classify each of the 82 preserved content changes**

For each path record one authority: exact PDB checksum, PDB embedded source, exact historical Git blob, pre-build task-log write event, exact IPA resource hash, build-log input, or publication-only hygiene. Do not classify a later/current version as historical evidence.

- [ ] **Step 2: Exclude unsupported and later changes**

Reject current multiplayer, later UI, performance, renderer, server, and unrelated platform work. Reject any file whose only evidence post-dates the 09:28 local Build 4 build start.

- [ ] **Step 3: Copy supported source and resources after a root check**

Copy only the accepted files from the preserved recovery worktree or exact embedded-source extraction into this worktree. Do not use `/Users/kevin/Projects/ra2-mac` as a copy source for dirty-tree content.

- [ ] **Step 4: Verify reconstructed compiled-source coverage**

For every repo-owned Portable PDB document outside generated `obj/`, compare the reconstructed source SHA-256 to its PDB checksum. Expected: zero mismatched or missing compiled-source documents.

- [ ] **Step 5: Record noncompiled inputs separately**

For project files, plist files, YAML, images, scripts, native patches, and resource manifests, record the build-log/task-log/IPA evidence and confidence independently because PDB source checksums cannot authenticate them.

### Task 4: Verify the complete iOS and native build input graph

**Files:**
- Inspect/modify if evidence supports: `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Inspect: repository-wide `*.props` and `*.targets`
- Inspect: `ios/OpenRA.iOS/Info.plist`
- Inspect: `ios/OpenRA.iOS/native/`
- Inspect: `ios/SDL2-CS/`
- Inspect: `ios/OpenRA.Platforms.iOS/`
- Inspect: `mod.config`
- Inspect: `mods/ra2/mod.yaml`
- Create: `docs/archive/BUILD4_BUILD_INPUTS.md`

- [ ] **Step 1: Extract exact evaluated MSBuild properties**

Record target framework, runtime identifier, AOT mode, interpreter setting, linker/trimmer mode, globalization settings, native library references, signing placeholders, resource items, and generator target ordering from historical project/build evidence.

- [ ] **Step 2: Verify native library provenance and hashes**

Record SDL2 and FreeType versions, library/archive hashes, source tags, patch series, UIKit/scene integration, and OpenGL ES integration. Mark any opaque or unmatched native binary `UNKNOWN` and keep publication blocked.

- [ ] **Step 3: Verify resource and mod inputs**

Compare bundle resource manifests, runtime profiles, mod manifests, embedded descriptors, and shipped non-user resources against the IPA file list and hashes.

- [ ] **Step 4: Document signing separation**

Confirm historical signing material is not needed for source reproducibility and is absent from the release tree. Use only ad-hoc/no-distribution signing for local verification.

### Task 5: Reproduce the AOT factory generation path

**Files:**
- Inspect: `build/OpenRA.AotRegistry.Generator/Program.cs`
- Inspect: `build/OpenRA.AotRegistry.Generator/OpenRA.AotRegistry.Generator.csproj`
- Inspect: `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Inspect: `packaging/ios-runtime-contract-descriptor-shape-v1.json`
- Regenerate: `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs`
- Create: `docs/archive/BUILD4_AOT_REPRODUCIBILITY.md`

- [ ] **Step 1: Record implementation and graph integration**

Document generator source path, MSBuild target name, `BeforeTargets`/`AfterTargets` placement, assembly inputs, authenticated descriptor inputs, output path, and whether the generated file is tracked.

- [ ] **Step 2: Regenerate only inside this worktree**

After the root check, run the historical generator with the reconstructed Build 4 assemblies and 296 authenticated descriptor resources. Direct all intermediate and build output under this worktree.

- [ ] **Step 3: Compare output**

Expected: 1,218 direct factories and SHA-256 `61f59a25dfc88373457fb0c16f596735d2938cb7c9a8992d3d78bad9d538af5d` for `GeneratedAotObjectRegistry.g.cs`.

- [ ] **Step 4: Prove reproducibility**

Record the full command, toolchain versions, input hashes, output hash, and any deterministic-build limitations. A different output blocks publication.

### Task 6: Complete asset provenance

**Files:**
- Create: `docs/archive/BUILD4_ASSET_PROVENANCE.md`
- Create: `docs/archive/evidence/build4-assets.sha256`
- Inspect: all release-tree files and all relevant Build 4 IPA resources

- [ ] **Step 1: Inventory relevant assets**

Inventory asset extensions, archives, native libraries, images, audio, maps, palettes, fonts, shaders, and embedded resources while excluding only isolated generated output directories from the source-release inventory.

- [ ] **Step 2: Assign one provenance class per file**

Use exactly:

```text
OPENRA_UPSTREAM
OPENRA_RA2_UPSTREAM
PROJECT_CREATED
THIRD_PARTY_OPEN_SOURCE
BUILD_GENERATED
USER_PROVIDED_AT_RUNTIME
ORIGINAL_RA2_ASSET
UNKNOWN
```

- [ ] **Step 3: Preserve evidence for excluded original assets**

Do not destroy the preserved recovery worktree. If the 18 `artsrc/vxl-reference` files remain `ORIGINAL_RA2_ASSET`, exclude them only from this release worktree and record their base blob IDs, hashes, and rationale.

- [ ] **Step 4: Enforce the publication gate**

Expected public tree: zero `ORIGINAL_RA2_ASSET` files and zero important `UNKNOWN` files.

### Task 7: Complete secret and signing-material audit

**Files:**
- Create: `docs/archive/BUILD4_SECRET_AUDIT.md`

- [ ] **Step 1: Scan file names and Git-tracked content**

Scan for private-key, certificate, provisioning-profile, environment, credential, token, password, and signing-material patterns without printing secret values.

- [ ] **Step 2: Scan content with redacted findings**

For every hit record path, type, and a redacted fingerprint only. Never copy historical signing material into this worktree.

- [ ] **Step 3: Verify ignore rules**

Confirm `.gitignore` excludes private signing material, `bin/`, `obj/`, `artifacts/`, `DerivedData/`, generated temporary output, and user-imported game data without hiding required historical source.

- [ ] **Step 4: Enforce the publication gate**

Expected: no private key, token, password, private provisioning profile, signing secret, or unexplained credential-like content in the release tree.

### Task 8: Run isolated reproducibility checks and prepare archival documentation

**Files:**
- Create: `BUILDING.md`
- Create: `NOTICE.md`
- Modify: `README.md`
- Create: `docs/archive/BUILD4_REPRODUCIBILITY.md`
- Create: `docs/archive/BUILD4_PUBLICATION_GATE.md`

- [ ] **Step 1: Run restore/build/test commands only in this worktree**

Before each command that can write, verify `pwd` and `git rev-parse --show-toplevel`. Keep all `bin/`, `obj/`, AOT, and packaging output inside this worktree. Do not use historical or current-development output directories.

- [ ] **Step 2: Compare reconstructed build evidence**

Compare managed assembly identity, PDB document set/checksums, generated AOT registry, resource inventory, plist metadata, native dependency hashes, and bundle layout. Signing differences are expected and must be documented rather than hidden.

- [ ] **Step 3: Write archival documentation**

State prominently: `ARCHIVAL SOURCE RELEASE`, `HISTORICAL BUILD`, `NOT CURRENT DEVELOPMENT VERSION`, no original Red Alert 2 assets, user-provided legally obtained game data required, GPLv3 preserved, signing credentials excluded, and exact Build 4 source-mapping confidence.

- [ ] **Step 4: Evaluate the publication gate**

Set `BUILD4_SOURCE_MAPPING=VERIFIED` only if all exact-source and build-input checks pass. Otherwise set `BUILD4_SOURCE_MAPPING=UNVERIFIED` and `PUBLICATION_BLOCKED`, listing every remaining gap.

- [ ] **Step 5: Stop before remote mutation**

Do not create a remote repository, push, tag, publish, or rewrite history until the local gate is green and explicit remote authorization has been reconfirmed.
