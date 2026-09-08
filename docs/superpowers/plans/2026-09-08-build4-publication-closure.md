# Build 4 Publication Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce an evidence-preserving, publication-safe Build 4 source tree and a final gate decision without modifying the current-development worktree.

**Architecture:** Treat the reconstructed Build 4 tree as immutable historical evidence and derive a separate public-tree projection that excludes retail assets. Resolve asset provenance by exact hashes and repository history, establish FreeType provenance from preserved build inputs, and compare binaries semantically before rerunning every release gate.

**Tech Stack:** Git plumbing/history, SHA-256 manifests, Python audit scripts, .NET 8/MSBuild, Portable PDB and PE metadata tools, Mach-O inspection tools, iOS/Mono AOT.

---

### Task 1: Original RA2 asset evidence and public-tree exclusion

**Files:**
- Create: `docs/archive/BUILD4_ORIGINAL_RA2_ASSETS.md`
- Create: `docs/archive/evidence/build4-original-ra2-assets.tsv`
- Create: `packaging/build4_public_source_tree.py`
- Create: `packaging/tests/test_build4_public_source_tree.py`

- [x] Record path, SHA-256, classification basis, tracked/bundled status, compile requirement, and runtime role for all 18 files.
- [x] Add a deterministic manifest-driven exporter that copies source into a separate staging directory while omitting prohibited retail assets and all build/signing output.
- [x] Test that all prohibited hashes are absent while evidence manifests remain present.
- [x] Build and run retail-import tests against the projected tree.

### Task 2: Unknown asset provenance resolution

**Files:**
- Modify: `docs/archive/evidence/build4-assets.sha256`
- Create: `docs/archive/evidence/build4-asset-provenance.tsv`
- Modify: `docs/archive/BUILD4_ASSET_PROVENANCE.md`

- [x] Resolve exact matches against local OpenRA and OpenRA/ra2 upstream histories first.
- [x] Resolve remaining dependency, generated, and project-created files using package metadata and commit history.
- [x] Prioritize and separately report the 186 files bundled in Build 4.
- [x] Leave unsupported classifications as `UNKNOWN` and exclude every unresolved file from the public projection.

### Task 3: FreeType provenance

**Files:**
- Create: `docs/archive/BUILD4_FREETYPE_PROVENANCE.md`
- Modify: `NOTICE.md`

- [x] Inspect the native archive, build scripts, task logs, package metadata, and repository history for exact version/source evidence.
- [x] Record version, source archive/tag/commit, source URL, recoverable SHA-256, license, build relationship, and modification status.
- [x] Mark provenance `VERIFIED` only when the native input is tied to exact source/package evidence.

### Task 4: Semantic binary comparison

**Files:**
- Create: `docs/archive/BUILD4_BINARY_SEMANTIC_COMPARISON.md`
- Create: `docs/archive/evidence/build4-managed-semantic-comparison.txt`
- Create: `docs/archive/evidence/build4-macho-semantic-comparison.txt`
- Modify: `docs/archive/BUILD4_REPRODUCIBILITY.md`

- [x] Compare managed IL, metadata, resources, references, and type/method inventories.
- [x] Compare Mach-O architecture, symbols, dependencies, sections, UUID/build-version commands, signatures, and other load commands.
- [x] Classify every observed mismatch as source-relevant or nondeterministic build metadata, retaining `PARTIAL` if evidence cannot exclude a source-relevant difference.

### Task 5: Final publication gate

**Files:**
- Create: `BUILD4_PUBLICATION_FINAL_GATE.md`
- Modify: `docs/archive/BUILD4_PUBLICATION_GATE.md`

- [x] Export a fresh public-tree projection and rerun the asset and secret scans.
- [x] From that projection, run the clean Release `ios-arm64` build, AOT regeneration/hash comparison, plist comparison, managed tests, packaging tests, and `git diff --check`.
- [x] Record every requested gate field and the evidence behind the decision.
- [ ] If and only if every publication gate passes, verify branch `archive/ios-build-4`, prepare tag `ios-build-4`, and perform one last asset/secret scan; do not push or publish.
