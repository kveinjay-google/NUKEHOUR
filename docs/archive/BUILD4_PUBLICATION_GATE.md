# Build 4 Publication Gate

## Current decision

```text
BUILD4_SOURCE_MAPPING=VERIFIED
PUBLICATION_CANDIDATE=READY
REMOTE_PUBLICATION_AUTHORIZED=YES
```

## Passing gates

- IPA/base identity and handwritten iOS host recovery files: verified.
- Generated AOT registry: verified and reproducible.
- Compiled source: 1,632 matches, zero mismatch/missing.
- Historical iOS project and plist: recovered; rebuilt plist is byte-identical.
- Isolated Release `ios-arm64` build: 0 errors.
- Publication validation: 158 Python tests and 257 managed tests passed with zero failures.
- Source secret/signing scan: passes.
- Current development worktree remained read-only.
- Eighteen original RA2 reference files and 29 unresolved possible derivatives
  are absent from the public projection; evidence remains in the private tree.
- Exact FreeType 2.13.3 source/build provenance is verified.
- Managed and native semantic source mapping is verified; whole-file identity
  is not reproducible because compiler, AOT/link, UUID, and signing metadata differ.

## Publication boundary

Only the exported public projection is publication-safe. The private recovery
tree intentionally retains prohibited evidence and must not itself be pushed.
No IPA, signing material, or original game data may be published. Explicit
authorization to publish this projection as a new parentless commit was
received on September 8, 2026.
