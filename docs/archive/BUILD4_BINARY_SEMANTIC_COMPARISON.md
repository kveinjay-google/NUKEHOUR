# Build 4 Binary Semantic Comparison

## Result

```text
MANAGED_BINARY_DIFFERENCE=NONDETERMINISTIC_BUILD_METADATA
NATIVE_BINARY_DIFFERENCE=NONDETERMINISTIC_BUILD_METADATA
SEMANTIC_SOURCE_MAPPING=VERIFIED
BIT_FOR_BIT_REPRODUCIBLE=NO
```

Whole-file hashes differ, but the differences do not identify a source-code
change. Detailed command output is preserved in
`evidence/build4-managed-semantic-comparison.txt` and
`evidence/build4-macho-semantic-comparison.txt`.

## Managed assemblies

`OpenRA.Game.dll`, `OpenRA.Mods.Common.dll`, `OpenRA.Mods.RA2.dll`, and
`OpenRA.iOS.dll` were compared with a metadata-reader tool built in this
isolated worktree. For all four assemblies, the assembly identity, metadata
table counts, assembly references, type inventory, method inventory, every
method IL body, and every embedded resource are equal.

The differing fields are the PE timestamp, module version identifier, and
CodeView identifier/path. The Build 4 PDB paths name the historical build
worktree; the rebuilt PDB paths name the isolated recovery worktree. These
compiler identifiers and paths are nondeterministic build metadata.

## Native Mach-O executable

Both executables are 64-bit arm64 Mach-O files. They have the same linked
library list, the same 1,417 exported symbols, the same 2,376 total symbol
names/types, the same load-command kinds, and the same `LC_BUILD_VERSION`
values: iOS platform, minimum iOS 15.0, SDK 26.5, and linker tool version
1267.0.

The observed differences are:

- `LC_UUID` differs.
- Code-directory hash and signing time differ; signature structure, size,
  identifier, and team are otherwise the same.
- Three generated sections differ slightly in size: `__TEXT,__text`,
  `__TEXT,__const`, and `__DATA,__bss`.

The managed inputs that feed Mono AOT are semantically identical but carry
different MVID/CodeView identities, while the native archives, generated AOT
factory registry, architecture, linker graph, symbol inventories, and Apple
toolchain/build-version record match. The small AOT/link section-layout
changes are therefore downstream build-identity and ordering effects, not
evidence of a source-relevant difference.

This establishes semantic source mapping, not byte-for-byte reproducibility.
The signed Build 4 IPA and its signing material remain outside the public
source tree.
