# Build 4 Dirty-Tree Replay

## Result

```text
BASE=6c17968ad62338ff0df1d1922af7d3a582f0a0db
COMPILED_SOURCE_DOCUMENTS_MATCHED=1632
COMPILED_SOURCE_DOCUMENTS_MISMATCHED=0
COMPILED_SOURCE_DOCUMENTS_MISSING=0
GENERATED_OBJ_DOCUMENTS_SKIPPED=15
DIRTY_TREE_REPLAY=HIGH_CONFIDENCE
```

Build 4 was produced from the base commit plus an uncommitted working tree. The base commit by itself is not corresponding source.

## Evidence hierarchy

1. The signed Build 4 IPA and matching Portable PDBs authenticate compiled source.
2. Embedded PDB source supplies exact bytes where available.
3. Portable PDB document SHA-256 values authenticate the remaining compiled files.
4. Historical task/tool events establish the pre-build chronology for project, plist, test, and audit files.
5. Exact IPA byte comparison authenticates changed bundle resources.
6. The preserved recovery candidate is used only when one of the preceding authorities corroborates it.

## Replayed source

- Thirty-three production C# paths were replayed only after matching their Build 4 PDB document hashes.
- `AppDelegate.cs` and `ViewController.cs` match the two handwritten iOS-host document hashes that differed from the later development tree.
- `GeneratedAotObjectRegistry.g.cs` matches its Build 4 PDB document hash and isolated regeneration output.
- `Game.cs` and `MainMenuLogic.cs` match embedded PDB source byte-for-byte.
- The complete six-assembly PDB check reports 1,632 matching non-generated documents, zero mismatches, and zero missing files.

The historical project PDB had three unmatched documents during the initial comparison: two handwritten files (`AppDelegate.cs`, `ViewController.cs`) and one generated file (`GeneratedAotObjectRegistry.g.cs`). It did not contain three unmatched handwritten files. Other handwritten host files, including `Program.cs`, already matched the Build 4 document checksums.

## Replayed noncompiled inputs

| Input group | Result | Authority |
|---|---|---|
| `ios/OpenRA.iOS/OpenRA.iOS.csproj` | exact recovered bytes | four pre-build task-log edits; SHA-256 `bd2690a34b6479da8bdceac0aa173fd350badcb022aae217bd17acd0918e9fc8` |
| `ios/OpenRA.iOS/Info.plist` | exact recovered bytes | logged build-number change plus exact rebuilt/IPA plist hash |
| 11 changed YAML/PNG bundle resources | exact recovered bytes | each matches the corresponding Build 4 IPA entry |
| 9 modified engine tests | high confidence | present in the 09:08 local pre-build status; not app compile inputs |
| bundle audit script and test | high confidence | direct pre-build edits and pre-build status; recovered app build passed the audit |
| retail crash harness | high confidence | present as untracked source before the build; not an iOS app project reference |
| 8 historical plans/specifications | high confidence | present in pre-build status; documentation only |
| deferred-test SHA ledger | high confidence | present before the build; evidence only |

Ephemeral `.playwright-cli`, `artifacts`, `__pycache__`, and build-output directories from the historical worktree were not imported as source.

## Explicit exclusions

- No post-Build-4 multiplayer, renderer, performance, UIKit-window, SDL-library, or current-version work was imported.
- Native SDL/library changes visible in the September 6 status post-date Build 4 and were excluded.
- The 18 `artsrc/vxl-reference` deletions in the preserved recovery candidate were publication-hygiene work, not part of the 09:08 local pre-build dirty status. The files remain preserved pending an authorized non-destructive exclusion step.
- The five files under `third_party/licenses/` are publication-only notice material, not Build 4 compile inputs.

The compiled-source snapshot is `VERIFIED`. The broader dirty-tree replay is `HIGH_CONFIDENCE` because noncompiled tests, documentation, and audit tooling do not have PDB document checksums.
