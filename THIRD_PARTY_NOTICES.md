# Third-Party Notices

This archival source includes or references third-party software. The license
terms shipped by each upstream project control; this file is an informational
index and does not replace those terms. Package binaries and platform SDKs are
not committed to this source archive.

## Project and native components

| Component | Version/source | License | Notice location |
| --- | --- | --- | --- |
| OpenRA and modified OpenRA/RA2 source | `release-20250330` base | GNU GPL v3 | `LICENSE`, `engine/COPYING`, and upstream notices retained in `engine/` |
| SDL 2 | 2.30.10 (`release-2.30.10`) | zlib | `third_party/licenses/SDL2-2.30.10-LICENSE.txt` |
| SDL2-CS bindings | local source under `ios/SDL2-CS` | zlib | License headers retained with source |
| FreeType | 2.13.3 (`VER-2-13-3`) | FreeType Project License or GPL v2 | `third_party/licenses/FreeType-2.13.3-LICENSE.txt`, `third_party/licenses/FreeType-2.13.3-FTL.txt` |

## Direct managed dependencies

| Package | Version | Declared license |
| --- | --- | --- |
| DiscordRichPresence | 1.2.1.24 | MIT |
| Linguini.Bundle | 0.8.1 | MIT OR Apache-2.0 |
| Microsoft.Extensions.DependencyModel | 6.0.2 | MIT |
| Microsoft.Win32.Registry | 5.0.0 | MIT |
| Mono.Nat | 3.0.4 | See `third_party/licenses/Mono.Nat-3.0.4-LICENSE.md` |
| MP3Sharp | 1.0.5 | LGPL-3.0 |
| Newtonsoft.Json | 13.0.1 | MIT |
| NuGet.CommandLine | 6.12.1 | Apache-2.0 |
| NVorbis | 0.10.5 | See `third_party/licenses/NVorbis-0.10.5-LICENSE.txt` |
| OpenRA-Eluant | 1.0.22 | MIT |
| OpenRA-Freetype6 | 1.0.11 | FreeType Project License OR GPL-2.0-only |
| OpenRA-FuzzyLogicLibrary | 1.0.1 | **License review pending: package metadata does not declare a license** |
| OpenRA-OpenAL-CS | 1.0.22 | LGPL-2.1 |
| OpenRA-SDL2-CS | 1.0.42 | zlib |
| Pfim | 0.11.3 | MIT |
| rix0rrr.BeaconLib | 1.0.2 | MIT |
| SharpZipLib | 1.4.2 | MIT |
| System.Collections.Immutable | 6.0.0 | MIT |
| System.Runtime.Loader | 4.3.0 | Microsoft .NET Library license |
| System.Threading.Channels | 6.0.0 | MIT |
| TagLibSharp | 2.3.0 | LGPL-2.1-only |

Transitive packages include `Linguini.Shared` and `Linguini.Syntax` 0.8.0
(MIT OR Apache-2.0), plus Microsoft .NET runtime/reference packages under their
applicable Microsoft license terms.

## Build and test dependencies

The build/test graph references Microsoft.NET.Test.Sdk 17.10.0, NUnit 3.13.3,
NUnit.Console 3.16.3, NUnit3TestAdapter 4.5.0, StyleCop.Analyzers
1.2.0-beta.435, Roslynator.Analyzers 4.2.0, and
Roslynator.Formatting.Analyzers 4.2.0. These are downloaded during restore and
are not included as binaries in this archive. Consult each package's included
license and upstream metadata before redistribution.

Apple's Xcode and Microsoft.iOS toolchains are external prerequisites and are
not redistributed here.
