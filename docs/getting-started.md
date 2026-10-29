# Get started with NUKE HOUR

## 1. Choose your installation route

Visit [the official website](https://nukehour.com) for current app distribution information. Test availability can change; a build mentioned in development notes is not a guarantee of a public TestFlight slot or App Store release. GitHub Releases currently has no downloadable IPA.

Developers can use the [source build guide](../BUILDING.md). The public source targets ARM64 iPhone and iPad devices running iOS/iPadOS 15.0 or later and uses landscape orientation. Build and signing requirements are separate from device requirements.

## 2. Import your own game data

NUKE HOUR does not include the original Red Alert 2 or Yuri’s Revenge game data. Use files from your own lawful installation, and open the app’s content manager to follow the import instructions for your installed build.

| Content | Core archives |
| :--- | :--- |
| Red Alert 2 | `ra2.mix`, `language.mix` |
| Yuri’s Revenge expansion | `ra2md.mix`, `langmd.mix`, alongside the base-game files |

These are the core archives listed in the public source release notes, not a complete list for every optional map, soundtrack or localized edition. The importer’s validation is the authority for your selected content. Keep a backup of your original installation; do not upload retail game data to GitHub issues.

## 3. Set up a match

For single-player skirmish, choose a map, faction and player color, add AI opponents, and configure teams and starting positions. Available content depends on imported files and the app build.

For nearby play, allow Local Network access when prompted and connect the devices to a suitable shared network. For internet play, use the online lobby. Multiplayer availability depends on compatible client/server versions and network conditions; similar version labels alone do not guarantee compatibility. See the [lobby guide](online-lobby.md) for the technical workflow.

## 4. Learn the touch interface

Use the virtual joystick and on-screen command buttons to navigate and issue orders. The production sidebar contains available construction and recruitment choices. Control-group buttons provide quick access to groups. Layout and available actions can differ between phone, tablet and build versions.

Choose English or Simplified Chinese in the interface language settings.

## Need help?

[Open a support issue](https://github.com/kveinjay-google/NUKEHOUR/issues/new) with your device model, OS version, app build, selected game mode and reproduction steps. Attach a screenshot when useful. Redact private information from logs and images.

EA has not endorsed and does not support this product.
