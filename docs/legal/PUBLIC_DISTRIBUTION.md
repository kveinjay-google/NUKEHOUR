# Public Distribution

## Identity

The public engine-only application is named **NUKE HOUR**. It keeps a separate
technical bundle identity and public distribution policy from private development
builds; private builds are not suitable for public distribution.

## License and source

The engine is distributed under the GNU General Public License version 3 or any
later version. Complete corresponding source and build instructions are
available in this repository. Run `ios/scripts/build-public-clean.sh` to create
the audited public artifact.

For macOS, run `packaging/macos/build-public-clean.sh`. The resulting Apple
Silicon DMG is self-contained: users do not need to install Python, Tk, or .NET.
It contains no retail MIX/SHP/VXL/audio/video data and no built-in gameplay
maps. On first launch, the launcher asks the user to import content locally
from a legally owned installation, mounted disc, or selected data files.

The minimum macOS content set is `ra2.mix` plus `language.mix`. Music, Yuri's
Revenge data, audio packs, and player-supplied maps are optional capabilities.
Import is offline, rejects executable content, and does not upload source paths
or game files.

## Required disclaimer

NUKE HOUR is an independent open-source project. It is not affiliated with, endorsed by, or sponsored by Electronic Arts. No Electronic Arts game assets are included. Players must import content from a legally owned copy.

## Release gate

Every public application bundle, IPA, and DMG must pass the deny-by-default
resource audit. A failed or incomplete audit produces no releasable artifact.
The software is provided without warranty under the terms of the GPL.
