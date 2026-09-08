# Build 4 iOS Build Inputs

## Historical invocation

Build 4 started at approximately 2026-09-05 09:28 local time and completed at approximately 09:33. The recorded command used `dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj`, `Release`, `net8.0-ios`, `ios-arm64`, `--no-restore`, and historical Apple Development signing properties. Signing values are deliberately omitted: they are not source inputs.

## Project and platform configuration

| Property | Build 4 value/evidence |
|---|---|
| Target framework / RID | `net8.0-ios` / `ios-arm64` |
| Configuration | `Release` |
| Minimum iOS | `15.0` |
| Device families | `1,2` |
| Bundle identifier | `com.openra.ipad.personal` |
| Display version / build | `1.0` / `4` |
| Runtime | Mono (`UseMonoRuntime=true`) |
| Interpreter | disabled/empty (`MtouchInterpreter` empty) |
| Linker | `MtouchLink=None` |
| Assembly IL stripping | disabled |
| Effective trim mode | `copy` |
| AOT | device build executes `_AOTCompile`; the IPA has 192 `.aotdata.arm64` files |
| Public-clean switch | `false` |

The recovered project SHA-256 is `bd2690a34b6479da8bdceac0aa173fd350badcb022aae217bd17acd0918e9fc8`. The source plist SHA-256 is `fd6623ad8b472ec3f979a5991a01770a58a2cbf61fd24b661f2a4e31b16efa59`. The rebuilt app plist and IPA plist are byte-identical at `b0ab0991980079e9232814e4060a8b74b7554fb06d09dfbabfd6004da09faaba`.

## Native inputs

All device native archives match the base commit:

```text
4bf80f034c4ae1d71363dc15685af84d4e1051ef3abb9a8dde37a455cc07226b  libOpenRAMetricKit.a
0b8d2ddad447e9b759549997d78de81086cc7ed69d358bbdab5062b7417e276a  libSDL2-platform-stubs.a
d0bbe37bde8496902552f2fe27d25f2194bf3bde7ac4f06ccde357d027e08d8b  libSDL2.a
eec62b4c7f0ad0feff18cf869ab443c4cbb2d5af68b37bab7d6052563fc350da  libfreetype.a
```

The project force-loads them. SDL links AVFoundation, AudioToolbox, CoreAudio, CoreBluetooth, CoreGraphics, CoreHaptics, CoreMotion, Foundation, GameController, Metal, OpenGLES, QuartzCore, and UIKit. The SDL archive identifies itself as `SDL-release-2.30.10-0-g9c821dc`. The checked-in UIKit scene patch SHA-256 is `a9fc207e46f2d00da0a740a41576eace12b47a45700e5633b2d69a3408fb0a21`. OpenGL ES is selected through SDL's GL context and the engine's embedded profile.

FreeType provenance is closed. Historical build logs record an unmodified
clone of official tag `VER-2-13-3`, peeled commit
`42608f77f20749dd6ddc9e0536788eaad70ea4b5`, configured for iOS arm64 with
optional BZip2, HarfBuzz, PNG, and Zlib integrations disabled. The official
tag archive measured SHA-256 is
`bc5c898e4756d373e0d991bab053036c5eb2aa7c0d5c67e8662ddc6da40c4103`.
See `BUILD4_FREETYPE_PROVENANCE.md`.

## Managed, resource, and toolchain graph

The app references `OpenRA.Game`, `OpenRA.Platforms.iOS`, and `OpenRA.Mods.RA2`; the latter references `OpenRA.Mods.Common` and `OpenRA.Mods.Cnc`. `mod.config` pins OpenRA `release-20250330`. The platform project reuses SDL graphics/input and removes desktop dylibs before publish.

The 296-row descriptor table contains 165 `Contract` and 131 `PreflightOnly` resources. It drives bundle inclusion and AOT identity. The recovered build passed the loading-brand and iOS touch-runtime audits.

Observed reproduction toolchain: .NET SDK 8.0.423, MSBuild 17.11.48, Microsoft.iOS workload 18.0.8319, and Xcode build 17F113. Build 4 records `DTSDKName=iphoneos26.5` and `DTXcodeBuild=17F113`.

Project configuration, descriptors, native archive bytes, SDL/UIKit
integration, OpenGL ES integration, and exact FreeType source/build provenance
are recovered.
