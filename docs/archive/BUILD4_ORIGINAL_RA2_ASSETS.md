# Build 4 Original RA2 Asset Closure

## Result

```text
ORIGINAL_RA2_ASSETS_PRESERVED_IN_RECOVERY_TREE=18
ORIGINAL_RA2_ASSETS_IN_PUBLIC_TREE=0
BUILD4_IPA_MEMBERSHIP=0
COMPILE_REQUIRED=0
```

The complete per-file evidence is in
`docs/archive/evidence/build4-original-ra2-assets.tsv`. Each row records the
path, SHA-256, classification basis, Git status, Build 4 bundle membership,
compile requirement, runtime role, and public-tree decision.

## Why these files are original game data

All 18 files are Westwood VXL/HVA vehicle-model or animation data. Commit
`0c29059e931625ea1b1f1795b00b65a17e682e20` explicitly says that it preserved
"loose VXL/HVA reference assets from engine/ root" in
`artsrc/vxl-reference/`. Their names are RA2 vehicle/turret/barrel identifiers,
not build inputs or generated outputs. Two files (`hwtz.vxl` and
`ttnktur.vxl`) also exact-match copies present in the user's installed RA2 map
library. No contrary creation or redistribution evidence exists, so the
fail-safe classification is `ORIGINAL_RA2_ASSET`.

## Tracked, bundled, and runtime status

All 18 paths are tracked in the historical repository. A direct member-name
scan of `/Users/kevin/Desktop/NUKE-HOUR-Build-4-signed.ipa` found none of the
18 paths or filenames. The previous ledger's `bundled_in_build4=yes` value was
not an IPA-membership result (it also marked `.github` files as bundled); the
corrected ledger now records `no`.

No project, MSBuild, resource manifest, generator, or source file references
`artsrc/vxl-reference`. The files are not required to compile and were not
runtime data in Build 4. They are historical art-reference data only.

## Evidence preservation and public projection

The files remain untouched in this isolated recovery tree and in its Git
objects. This tree is the evidence copy and is not the public projection.
`packaging/build4_public_source_tree.py` creates a new projection and denies
every `SOURCE` row classified `ORIGINAL_RA2_ASSET` or `UNKNOWN`. It also omits
build, signing, and transient output. The exporter refuses to overwrite an
existing destination. This preserves the historical bytes while ensuring no
retail asset is copied into the publication candidate.
