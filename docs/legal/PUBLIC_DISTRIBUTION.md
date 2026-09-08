# Public iOS Distribution

## Identity

The public engine-only application is named **NUKE HOUR**. It keeps a separate
technical bundle identity and public distribution policy from private development
builds; private builds are not suitable for public distribution.

## License and source

The engine is distributed under the GNU General Public License version 3 or any
later version. Complete corresponding source and build instructions are
available in this repository. Run `ios/scripts/build-public-clean.sh` to create
the audited public artifact.

## Required disclaimer

NUKE HOUR is an independent open-source project. It is not affiliated with, endorsed by, or sponsored by Electronic Arts. No Electronic Arts game assets are included. Players must import content from a legally owned copy.

## Release gate

Every public application bundle and IPA must pass the deny-by-default resource
audit. A failed or incomplete audit produces no releasable IPA. The software is
provided without warranty under the terms of the GPL.
