# macOS PublicClean First-Run Gate Design

## Goal

Make a newly installed macOS PublicClean build start reliably with no imported retail content and no maps, guide the user through legal local import, and enable gameplay-dependent controls only when their inputs exist.

## Root cause

The launcher eagerly builds every page before it selects the startup notice or content route. The skirmish page therefore constructs a Tk `OptionMenu` while the map list is empty. Tk requires an initial value argument, so a clean profile crashes before the notice or importer can appear.

## Architecture

The macOS launcher will use the same preflight principle as iOS: startup-safe UI is shown before gameplay-dependent UI is constructed. Page frames remain registered for navigation, but page contents are built lazily on first display. The legal notice is displayed first; after acknowledgement, the existing content readiness policy routes an incomplete profile to content management.

Lazy construction is not the only protection. The skirmish page must model an empty map catalog as a valid state. It will render a disabled selector with a localized “no available maps” value, disable every start-game entry point, and explain that the user must import a supported map. The launch handler retains its existing final guard.

## State model

- `base_content_ready`: `ra2.mix` and `language.mix` are present. This permits launching the engine menu and participating in workflows that can supply a map.
- `local_skirmish_ready`: base content is ready and at least one valid local map exists. This permits the launcher's direct skirmish action.
- Missing optional music, expansion, or audio packages never blocks base-game startup.

The content page continues to show capabilities independently. A successful resource or map import refreshes the content view and invalidates the lazily built maps/skirmish pages so their next display reads the new catalog without requiring a launcher restart.

## Failure handling

Empty or malformed map catalogs must never raise during UI construction. The selector and start buttons remain disabled, and direct calls to the launch handler show the existing “no available map” error. Import failures remain atomic and preserve existing content.

## Verification

Automated tests cover lazy construction, the empty-map presentation model, disabled start actions, and invalidation after import. The final acceptance uses the built DMG copied into `/Applications`, an isolated fresh OpenRA support directory, GUI-driven legal content import, map import when a legal test map is available, and launch-process/log verification. Existing user data is atomically restored after the run.

The release gate fails unless the final DMG starts on a clean profile without an exception and reaches the content-import screen.
