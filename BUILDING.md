# Building the Build 4 Archival Source

This is the source archive corresponding to NukeHour iOS Build 4, not the
current development version. The historical build used:

- .NET SDK 8.0.423
- Microsoft.iOS workload 18.0.8319
- Xcode build 17F113
- target framework `net8.0-ios`
- runtime identifier `ios-arm64`
- `Release` configuration
- OpenRA engine release `release-20250330` (pinned by `mod.config`)

Install the matching .NET SDK, iOS workload, and Xcode command-line tools. From
the repository root, restore and make a source-only, unsigned verification
build with:

```sh
dotnet restore ios/OpenRA.iOS/OpenRA.iOS.csproj -r ios-arm64
dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj \
  --configuration Release \
  --framework net8.0-ios \
  --runtime ios-arm64 \
  --no-restore \
  -p:PublicClean=true \
  -p:CodesignDisable=true \
  -p:BuildIpa=false
```

`PublicClean=true` enables the publication-safe path. Do not add certificates,
private keys, provisioning profiles, credentials, an IPA, an app bundle, or
original game data to this repository. Build outputs must remain untracked.

The checked-in AOT generator is `build/OpenRA.AotRegistry.Generator`; its output
is integrated into `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs`. Users must
provide eligible Red Alert 2/Yuri's Revenge data from their own lawful
installation at runtime.

See [the publication gate](docs/archive/BUILD4_PUBLICATION_GATE.md),
[AOT reproducibility notes](docs/archive/BUILD4_AOT_REPRODUCIBILITY.md), and
[third-party notices](THIRD_PARTY_NOTICES.md).
