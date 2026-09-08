# Build 4 Reproducibility Report

## Isolated build

The recovered tree was built only under `/Users/kevin/Projects/ra2-build4-source-release-20260908-055841` using `dotnet build`, `Release`, `net8.0-ios`, `ios-arm64`, `--no-restore`, and `CodesignDisable=true`.

The final publication projection was restored and built with `PublicClean=true`.
Result: success in 4 minutes 9.85 seconds, 0 errors, 11 warnings. Its built-in
public-bundle audit passed with 187 inventoried files and zero errors.

## Focused validation

- Build 4 publication packaging suite: 158 tests passed.
- Build 4 iOS content, menu/layout, runtime-profile, archive-graph, and retail-import tests: 257 passed, 0 failed, 0 skipped.

The managed test run used isolated, ignored test fixtures only: a local FreeType compatibility symlink under `artifacts/test-native`, an isolated `engine/mods/ra2` symlink, and a simulator-runtime restore within this worktree. It did not use or write the current-development worktree.

## Exact matches

- 1,632/1,632 non-generated PDB source documents; zero mismatch/missing.
- AOT registry matches the PDB and isolated regeneration hash.
- Rebuilt `Info.plist` matches the IPA byte-for-byte.
- Eleven changed YAML/PNG resources match their IPA entries.
- The iOS project, descriptor schema, and base native archives are exact recovered bytes.

## Binary comparison

Final managed/native binaries do not match the signed IPA:

```text
OpenRA.Game.dll rebuilt:       9462c9a4c0284bf347bd8d96ae44ef538de94d2c136a7015ee0dbc4874eafba9
OpenRA.Game.dll Build 4:       35de770a994de0fdd3113fe67fff349dc9e1013d258f767ce8e279ebb9434815
OpenRA.iOS executable rebuilt: 6ef6067b3e3bb9ffd7e614669842d6404754244fb2e792df6ad7b021c4015b7f
OpenRA.iOS executable Build 4: d2ffd8b9740e26b039a45cf0fccb845fd51e190d8c027569b06f536f6be97a32
```

All four compared project assemblies have equal identities, references,
metadata-table counts, type/method inventories, every method IL body, and
embedded resources. Only PE timestamp, MVID, and CodeView identity/path differ.
The Mach-O files have equal architecture, dependency list, load-command kinds,
build-version data, and symbol names/types; UUID, signature identity data, and
small generated AOT/link section sizes differ. These are classified as
`NONDETERMINISTIC_BUILD_METADATA`, not a source-relevant difference. See
`BUILD4_BINARY_SEMANTIC_COMPARISON.md`.

```text
SOURCE_REPRODUCIBILITY=VERIFIED
AOT_GENERATION_REPRODUCIBILITY=VERIFIED
PLIST_REPRODUCIBILITY=VERIFIED
SEMANTIC_SOURCE_MAPPING=VERIFIED
BIT_FOR_BIT_REPRODUCIBLE=NO
```
