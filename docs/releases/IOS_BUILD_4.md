# iOS Build 4 Release Metadata

| Field | Value |
| --- | --- |
| Archive | NukeHour iOS Build 4 corresponding source |
| Historical build date | September 5, 2026 |
| Bundle identifier | `com.openra.ipad.personal` |
| Short version | `1.0` |
| Bundle version | `4` |
| Minimum iOS version | `15.0` |
| Target | `net8.0-ios` / `ios-arm64` / `Release` |
| OpenRA base | `release-20250330` |
| Public branch | `archive/ios-build-4` |
| Public tag | `ios-build-4` |
| Public commit | The parentless commit carrying this file |
| Historical IPA SHA-256 | `87a79a978833f6f743afcf4ea05acd75a06d9c693fbbfc2d81b9578a02dc2b7b` |

The historical IPA hash is recorded only for source-to-build identification;
the IPA itself is not distributed or linked from this archive.

The source includes the iOS host, the `OpenRA.AotRegistry.Generator` factory
generator, its build integration, the generated AOT registry, build files, and
the modified OpenRA/RA2 source associated with Build 4. The recovered generator
produced 1,218 factories from 296 authenticated descriptors. Source mapping and
semantic comparison evidence are documented under `docs/archive/`.

Excluded from this archive are original Red Alert 2/Yuri's Revenge game assets,
user-imported data, the Build 4 IPA, build products, certificates, private keys,
provisioning profiles, credentials, and all pre-publication Git ancestry.
