# Build 4 Publication Final Gate

```text
SOURCE_MAPPING:
VERIFIED

ORIGINAL_RA2_ASSETS_IN_PUBLIC_TREE:
0

UNKNOWN_MATERIAL_ASSETS:
0

FREETYPE_PROVENANCE:
VERIFIED

SECRET_SCAN:
PASS

AOT_REPRODUCIBILITY:
PASS

CLEAN_BUILD:
PASS

SEMANTIC_BINARY_MAPPING:
VERIFIED

BIT_FOR_BIT_REPRODUCIBLE:
NO

PUBLICATION_READY:
YES
```

## Publication candidate

The publication candidate is the fresh deny-by-default export at
`artifacts/build4-public-source-20260908-final3`. It is derived from the private
recovery worktree; the recovery worktree itself is not publication-safe.

The exporter excludes every source-ledger row classified
`ORIGINAL_RA2_ASSET` or `UNKNOWN`, plus Git metadata, `bin`, `obj`,
`artifacts`, `DerivedData`, app bundles, IPA/signing files, and transient
generator output. The 18 original VXL/HVA files and all 29 unresolved possible
retail derivatives remain preserved in private evidence and are absent from
the public candidate by both path and SHA-256.

## Gate evidence

- Source mapping: 1,632/1,632 non-generated PDB documents match; the recovered
  handwritten iOS host files and generated registry match their Build 4 PDB
  checksums.
- FreeType: official tag `VER-2-13-3`, commit
  `42608f77f20749dd6ddc9e0536788eaad70ea4b5`, archive SHA-256
  `bc5c898e4756d373e0d991bab053036c5eb2aa7c0d5c67e8662ddc6da40c4103`;
  historical logs show an unmodified source build.
- Material inventory: every material file matches an allowed source-ledger row
  except `engine/packaging/artwork/cnc_scalable.svg` and `ra_scalable.svg`;
  both exceptions byte-match the corresponding files in OpenRA
  `release-20250330`. No unknown or denied material is present.
- AOT: isolated regeneration produced 1,218 factories from 296 authenticated
  descriptors and exactly matched SHA-256
  `61f59a25dfc88373457fb0c16f596735d2938cb7c9a8992d3d78bad9d538af5d`.
- Clean build: publication projection `closure3` restored and built Release
  `net8.0-ios` / `ios-arm64` with `PublicClean=true`, zero errors. Its bundle
  audit passed with 187 inventoried files and zero errors. A checksum comparison
  found no source/build-input differences between `closure3` and the final
  projection; only the subsequently completed publication documentation differs.
- Plist: rebuilt and Build 4 `Info.plist` are byte-identical at SHA-256
  `b0ab0991980079e9232814e4060a8b74b7554fb06d09dfbabfd6004da09faaba`.
- Tests: 257 focused managed tests and 158 publication packaging/audit tests
  passed with zero failures.
- Binary mapping: project assemblies have equal IL, metadata, references,
  type/method inventories, and embedded resources. Mach-O architecture,
  dependencies, symbol inventories, load-command kinds, and build version
  match. Differences are compiler/AOT/link identifiers, UUID, section layout,
  and signing metadata, classified `NONDETERMINISTIC_BUILD_METADATA`.
- Formatting: `git diff --check` passes.

The broad repository Python discovery suite is not used as this archival
publication gate. It reports 21 failures outside the publication suite: nine
require the historical Pillow 11.3.0 rendering environment or compare
version-sensitive generated art, eleven enforce a superseded pre-provenance
brand/SBOM classification fixture, and one asserts a temp-path display string.
No failure identifies a source compilation, public-bundle, asset-exclusion,
secret, AOT, plist, or semantic-mapping defect.

## Publication limits

- Publish source only; do not publish or relink the IPA.
- Do not publish the private recovery/evidence worktree.
- Do not include original RA2 game data or the 29 unresolved possible
  derivatives.
- Users must import lawful RA2/Yuri's Revenge data at runtime.
- Explicit authorization was received on September 8, 2026 to publish this
  source projection as a new parentless commit, branch, and annotated tag.

The public branch and tag must be created from a parentless commit made only
from this scanned projection. No prior local branch history may be reused.
