# macOS Content Source Discovery Design

## Goal

Make the in-game NUKE HOUR content manager usable when no retail assets are installed. A Mac user must be able to choose any lawful Red Alert 2 installation directory, or ask the app to search common local installation locations automatically.

## Scope

This change applies to the macOS in-game content manager shown before the RA2 mod can load. It does not change the PublicClean destination, download content, upload files, or add retail assets to the installer.

## Considered approaches

1. Add search only to the Python launcher. This is low risk, but does not fix the in-game screen the user reached.
2. Exit the game and reopen the launcher's content page. This reuses existing import code, but creates a confusing two-application handoff.
3. Extend the existing OpenRA content installer. This keeps the workflow in one window and reuses its package selection and copy actions. This is the selected approach.

## User interface

The content manager footer will expose three explicit actions:

- **选择安装目录…** opens the native macOS folder picker.
- **自动搜索** scans bounded, known local roots for a directory containing the required base files.
- **返回** closes the content manager.

The existing package list, background, colors, typography, and import confirmation flow remain unchanged. English strings are supplied for English systems.

## Source model

A new `MacOSDirectory` installer source describes a normal extracted or digital RA2 directory. It requires `ra2.mix` and `language.mix`, case-insensitively. Optional music and Yuri files remain optional package choices.

The resolver provides two entry points that share validation:

- manual discovery searches the user-selected directory and a bounded number of child levels;
- automatic discovery searches only existing common roots such as `/Applications`, `/Volumes`, Steam, CrossOver, Whisky, Porting Kit, `~/Games`, and Wine prefixes.

Search is breadth-first, skips symbolic-link directories, catches inaccessible directories, enforces a maximum depth and directory count, and returns the first complete source. It does not scan the whole disk.

## Data flow

1. The user selects a folder or starts automatic search.
2. The resolver locates a directory containing both required base archives.
3. The existing installer displays packages available from that source.
4. The existing copy actions import selected files into the OpenRA support directory.
5. The existing installed-content check updates the content manager and continues into NUKE HOUR when requirements are satisfied.

No source path is persisted in the public build metadata or import receipt.

## Errors and cancellation

- Canceling the macOS picker returns to the content manager without an error.
- A selected directory without the required files shows a localized “content not found” result and allows choosing again.
- Automatic search with no match shows the existing alternatives list plus retry and manual-selection actions.
- Read failures and inaccessible directories are ignored during discovery; install failures continue to use `install.log` and the existing rollback behavior.

## Verification

- Unit tests cover case-insensitive matching, nested discovery, depth limits, symlink skipping, inaccessible candidates, and common-root generation.
- Manifest tests require the new source and the two visible actions in both English and Simplified Chinese.
- Engine and packaging tests must pass.
- The final signed PublicClean DMG must pass the deny-by-default audit.
- A runtime smoke test from a mounted DMG with an empty support directory must visually show the two new actions and successfully detect a controlled fake source directory without importing bundled retail content.
