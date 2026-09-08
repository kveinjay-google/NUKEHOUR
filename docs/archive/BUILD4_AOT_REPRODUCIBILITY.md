# Build 4 AOT Factory Reproducibility

## Method

```text
METHOD=Build-time assembly/type reflection generator integrated through MSBuild
C_SHARP_SOURCE_GENERATOR=NO
RESULT=VERIFIED
```

## Exact implementation

- Generator: `build/OpenRA.AotRegistry.Generator/Program.cs`
- Generator project: `build/OpenRA.AotRegistry.Generator/OpenRA.AotRegistry.Generator.csproj`
- Integration: `GenerateAotObjectRegistry` in `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Ordering: `BeforeTargets="CoreCompile"`
- Dependencies: `PrepareAotObjectRegistryGeneration;ResolveProjectReferences;WriteRuntimeContractDescriptorInput`
- Inputs: `OpenRA.Game.dll`, `OpenRA.Mods.Common.dll`, `OpenRA.Mods.Cnc.dll`, `OpenRA.Mods.RA2.dll`
- Descriptor TSV: `obj/Release/net8.0-ios/ios-arm64/runtime-contract-descriptors-v1.tsv`
- Schema: `packaging/ios-runtime-contract-descriptor-shape-v1.json`
- Output: `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs`

The executable loads the assemblies, reflects eligible types/constructors, validates the descriptor schema and bytes, and emits direct constructor registrations. It does not implement Roslyn source-generator interfaces.

```text
1355bae67c78c40e15521d8cb536d6881b0027d8864a73faa46506ffc2f4a935  Program.cs
031da6a6f6d810b9f23ffe7818f3acefb7b0ff54b0eb8c6752735ad58d254904  generator csproj
6c0d12b5a985bcf87fc255a03a958dbc09be00de9238b239354c8d34be1bd8f6  descriptor schema JSON
90cca4a3fb2f831ec8631d03a9b130bafa02292b65cadadc294ec6f8ef3d1a97  evaluated descriptor TSV
```

There are 296 rows: 165 `Contract` and 131 `PreflightOnly`. Shape digest: `4fe820475ee4d6ef843320f406b5db3fd414fd106c953b4a7b4a05020fb3614d`.

The isolated target and full Release build each generated 1,218 factories and the exact Build 4/PDB hash:

```text
61f59a25dfc88373457fb0c16f596735d2938cb7c9a8992d3d78bad9d538af5d  GeneratedAotObjectRegistry.g.cs
```

The generated identity is `47e17a7805a60e65d868c5ffbf264f5382e97b06ed3586b0edcfd05d212b7a27`. The output is tracked but deterministically regenerated before `CoreCompile`; intermediates stay under isolated `obj`/`bin`.
