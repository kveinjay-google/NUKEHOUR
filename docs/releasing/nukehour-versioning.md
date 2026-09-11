# NUKE HOUR release versioning

Each platform owns an independent product version in
`packaging/nukehour-version.json`. Product versions are not multiplayer
protocol versions.

## Patch release

Run this in the platform repository:

```sh
python3 packaging/nukehour_version.py bump patch
python3 packaging/nukehour_version.py check
python3 -m unittest packaging.tests.test_nukehour_version -v
```

`bump patch` increments both the patch component and platform build number
exactly once, then regenerates the MSBuild properties and mod metadata. Commit
the canonical JSON and every generated projection together.

Current independent lines:

- iOS: `1.0.8`, build `8`
- macOS: `1.0.3`, build `3`
- Android: `0.0.3`, build `3`

## Multiplayer compatibility

Multiplayer compatibility uses the generated
`nukehour-core-sha256-...` identity, not the app version. Its inputs are
listed in `packaging/nukehour-compatibility-inputs.txt`.

A UI, packaging, or platform-only fix increments only that platform's product
version and does not prevent cross-platform play. A deterministic engine,
network-order, rules, or weapons change updates the compatibility identity and
requires all peers to use matching core content.

## Storage compatibility

`nukehour-storage-v1` is stable across product releases. User maps are written
to the stable directory while the legacy literal development-version directory
remains readable. Neither product-version increments nor UI-only releases move
user map data.
