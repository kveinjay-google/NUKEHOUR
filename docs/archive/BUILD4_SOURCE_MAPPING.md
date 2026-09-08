# NukeHour iOS Build 4 Source Mapping

## Gate status

```text
BUILD4_SOURCE_MAPPING=VERIFIED
```

The binary, Git base, PDB set, and dirty-tree source snapshot are identified.
The handwritten iOS host files have been independently cross-checked, all
1,632 non-generated PDB documents match, and the isolated build succeeds.
Asset and FreeType provenance are closed for the deny-by-default public
projection, and semantic binary comparison found no source-relevant mismatch.

## Released binary

```text
Path: /Users/kevin/Desktop/NUKE-HOUR-Build-4-signed.ipa
SHA-256: 87a79a978833f6f743afcf4ea05acd75a06d9c693fbbfc2d81b9578a02dc2b7b
Size: 156343517 bytes
Filesystem modified time: 2026-09-05T09:43:19-0700
CFBundleIdentifier: com.openra.ipad.personal
CFBundleShortVersionString: 1.0
CFBundleVersion: 4
MinimumOSVersion: 15.0
DTSDKName: iphoneos26.5
DTXcode: 2660
DTXcodeBuild: 17F113
```

Historical task/tool logs place the build start at approximately 2026-09-05 09:28 local time, successful build/device installation at approximately 09:33, and IPA packaging at approximately 09:42–09:43.

## Git source base

```text
Base commit: 6c17968ad62338ff0df1d1922af7d3a582f0a0db
Commit time: 2026-09-03T23:19:21-07:00
Subject: Harden retail generation authority plan
Historical build state: base commit plus uncommitted working-tree changes
```

The base commit alone is not the corresponding source for Build 4.

## Managed assemblies in the released IPA

```text
585217144d4aaaff1d911d77787a831ce929547e75c01f1abccbbd334e4dbc39  OpenRA.iOS.dll
35de770a994de0fdd3113fe67fff349dc9e1013d258f767ce8e279ebb9434815  OpenRA.Game.dll
3df65c3b2b32b387025148a516d93008dc1ded575ab408ae1584e97e55da3ce0  OpenRA.Mods.Common.dll
45f0cca79bcf75deb0e3d2ae0edc8eee24d860b5ff32f601ed61ecc60dbd33e0  OpenRA.Mods.Cnc.dll
1592d6fdbfa5da812aeafed9a6a8ffeb695b056365160105cc59665d0d7c16ac  OpenRA.Mods.RA2.dll
2e0649da24ddfeccf7e7407f7c4d8117d4e35bc2587431e123333925da8d7022  OpenRA.Platforms.Default.dll
```

## Preserved matching PDB evidence

The matching PDB set is preserved below the read-only historical iOS worktree at:

`/Users/kevin/.config/superpowers/worktrees/ra2-mac/ios-retail-import-hardening/artifacts/build-testflight-5-personal/Release/net8.0-ios/ios-arm64`

```text
d19bd6cb6c7d375d58c5759075bb4ad3b56500479962e1d77412dd3f115f6a28  OpenRA.iOS.pdb
29ed8c796fd9c9280a4f5045eac078610ee371e52e0f0a98aed80b3a2df7638d  OpenRA.Game.pdb
2ce4b18067efd8360e019b050100f57b29557d4283c81a7b6c7dcfd5f89e8939  OpenRA.Mods.Common.pdb
61e6509b2e70f8410b5901094491efb9a219e240e82c7570b37191de9f9d2869  OpenRA.Mods.Cnc.pdb
a3181676cc4fa30a11f96155300ee4e1afbd13683cd979d7fc037f4b72ba7797  OpenRA.Mods.RA2.pdb
d004c17456183e65101d10a5465f5479b423ad6d720552e99f3ab9deb244383f  OpenRA.Platforms.Default.pdb
```

The PDB document checksums authenticate recovered source bytes. `OpenRA.Game` and `OpenRA.Mods.Common` also carry embedded source used to recover exact files including `Game.cs` and `MainMenuLogic.cs`.

## Three unmatched iOS PDB documents

The prior investigation found three Build 4 iOS PDB document hashes that did not match the later preserved development worktree:

```text
7a6751c2709dd0f88a5219731cbe3464558da7fec11aec3955c5e74bd4410ea1  ios/OpenRA.iOS/AppDelegate.cs
95482ba59b9ca03471d66c4dfd7ddb02b728e2e571c055824e533fb6ce2868db  ios/OpenRA.iOS/ViewController.cs
61f59a25dfc88373457fb0c16f596735d2938cb7c9a8992d3d78bad9d538af5d  ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs
```

The preserved recovery candidate now matches all three hashes byte-for-byte. `AppDelegate.cs` and `ViewController.cs` are handwritten. `GeneratedAotObjectRegistry.g.cs` is generated source, not a third handwritten host file. Isolated target execution and the full isolated Release build each regenerated 1,218 direct factories and retained the expected `61f59a...` hash.

## Evidence locations

- Preserved dirty-tree recovery candidate, read only: `/Users/kevin/Projects/ra2-build4-source-release`
- Isolated recovery/write worktree: `/Users/kevin/Projects/ra2-build4-source-release-20260908-055841`
- Recovered-file ledger: `docs/archive/evidence/build4-recovered-files.sha256`
- Recovered-file ledger SHA-256: `09c31e12da5ea70bd73ccc7ad3436320e175fd6d3c238500e9e339f5c76c1100`
- Prior recovery task log: `/Users/kevin/.codex/sessions/2026/09/08/rollout-2026-09-08T04-49-12-01a080d9-ce91-7923-8a1a-c0ad373a9b8b.jsonl`
- Prior recovery task log SHA-256: `97e16ffe1da9533d4f682b8cf750502bc170ab15ab738c5d3ffbafe99a84570d`
- PDB document listings: `/private/tmp/build4-pdb-listings/`
- Extracted embedded source: `/private/tmp/build4-embedded/`

## Current confidence

| Item | Confidence | Basis |
|---|---|---|
| Build 4 IPA identity | VERIFIED | Bundle version 4 and exact IPA hash |
| Git base | VERIFIED | Historical build/task logs identify `6c17968...` |
| Dirty-tree requirement | VERIFIED | Build occurred with uncommitted changes; PDB hashes differ from base |
| `Game.cs` | VERIFIED | Embedded PDB source and document hash |
| `MainMenuLogic.cs` | VERIFIED | Embedded PDB source and document hash |
| `AppDelegate.cs` candidate bytes | VERIFIED | Candidate SHA-256 equals Build 4 PDB document checksum; task chronology and compiled IL independently corroborate it |
| `ViewController.cs` candidate bytes | VERIFIED | Candidate SHA-256 equals Build 4 PDB document checksum; task chronology and compiled IL independently corroborate it |
| Generated AOT registry candidate | VERIFIED | Candidate SHA-256 equals PDB checksum and prior isolated regeneration |
| Complete compiled-source snapshot | VERIFIED | 1,632 non-generated PDB documents match; zero mismatched or missing |
| Complete noncompiled build-input snapshot | HIGH_CONFIDENCE | project/plist chronology, exact IPA resources, and pre-build dirty status documented |
| Asset provenance | VERIFIED FOR PUBLIC PROJECTION | 18 original and 29 unresolved possible-derivative files remain only in private evidence; none is copied to the public tree |
| Secret/signing-material audit | VERIFIED | no private key/profile/token material found in source |
| AOT factory reproducibility | VERIFIED | isolated regeneration produced 1,218 factories and exact Build 4 hash |
| Reproducible release build | VERIFIED SEMANTICALLY | build passes; plist/AOT/source and managed semantics match; nondeterministic build metadata prevents whole-file identity |
