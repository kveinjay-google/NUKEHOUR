# Build 4 Asset Provenance

## Gate status

```text
RECOVERY_TREE_UNKNOWN=29
PUBLIC_TREE_UNKNOWN=0
ORIGINAL_RA2_ASSETS_IN_PUBLIC_TREE=0
ASSET_PROVENANCE=PASS_FOR_PUBLIC_PROJECTION
```

The exhaustive ledger is `docs/archive/evidence/build4-assets.sha256`. It
inventories 2,229 source-tree assets/resources and 186 Build 4 bundle rows. The
267 formerly unknown source assets have a per-file decision in
`docs/archive/evidence/build4-asset-provenance.tsv`.

| Scope | Class | Count |
|---|---|---:|
| Source tree | `OPENRA_UPSTREAM` | 1,822 |
| Source tree | `OPENRA_RA2_UPSTREAM` | 151 |
| Source tree | `PROJECT_CREATED` | 195 |
| Source tree | `THIRD_PARTY_OPEN_SOURCE` | 6 |
| Source tree | `BUILD_GENERATED` | 8 |
| Source tree | `ORIGINAL_RA2_ASSET` | 18 |
| Source tree | `UNKNOWN` | 29 |
| Build 4 IPA | `UNKNOWN` | 29 |

`USER_PROVIDED_AT_RUNTIME` describes retail data imported into the app data container. No imported retail data belongs in the source-release tree.

## Resolution method

Of the original 267 unknown rows, 146 were authenticated as OpenRA/ra2 by an
exact blob reachable from `upstream/master`, an OpenRA/ra2-authored historical
commit, or an explicitly recorded upstream PR cherry-pick. Ninety are local
project deltas authenticated by the Build 4 dirty-tree evidence or local commit
history. The two Noto Sans CJK SC 2.004 files carry the SIL Open Font License
1.1 in their font metadata and alongside the files.

Twenty-nine assets added by local integration commit `05e614e` remain
`UNKNOWN`: eleven Yuri-themed cameos and eighteen Yuri structure SHPs. The
commit supplies no source or license record. Exact extraction from the user's
lawfully installed `cameomd.mix` proved that three same-named cameos use the
same 2,912-byte SHP container size but different bytes, which is consistent
with modification of retail art and insufficient for redistribution. All 29
remain `public_tree=no`; they are not publication blockers because the public
projection excludes them and the public build uses runtime-imported game data.

The 18 `artsrc/vxl-reference/*.vxl`/`*.hva` files remain
`ORIGINAL_RA2_ASSET` in the private recovery tree, but are absent from the
public projection. See `BUILD4_ORIGINAL_RA2_ASSETS.md`.

SDL 2.30.10 and FreeType 2.13.3 are `THIRD_PARTY_OPEN_SOURCE`; exact FreeType
provenance is documented in `BUILD4_FREETYPE_PROVENANCE.md`. Root `.app`
bundles and AOT outputs are `BUILD_GENERATED` and are never copied to the public
projection. No evidence was removed or replaced.
