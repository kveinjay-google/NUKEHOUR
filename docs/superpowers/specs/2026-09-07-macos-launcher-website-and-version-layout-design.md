# macOS Launcher Website Link and Version Layout Design

## Goal

Add a direct NUKE HOUR website affordance to the macOS launcher and make the
launcher version visible in a stable, branded position without changing the
existing launcher shell dimensions or the language-control layout.

## Approved behavior

- The existing top-left NUKE HOUR emblem remains visually unchanged.
- Clicking or pressing the emblem opens `https://nukehour.com` in the user's
  default browser.
- The emblem uses a hand cursor so its clickability is discoverable.
- The existing launcher version source remains `engine/VERSION`; no new version
  source or parsing rule is introduced.
- The visible version label is exactly `NUKE HOUR ` followed by the value read
  from `engine/VERSION`, and must not contain `RA2`.
- The version label is a persistent footer in the launcher content area,
  right-aligned at the bottom-right with the existing shell padding. It is
  visible while switching between launcher pages.

## Components and data flow

`Launcher._build_sidebar` owns the emblem widget and binds its click event to a
small launcher method that delegates to Python's standard `webbrowser` module.
The launcher does not fetch website content or block startup on the browser
operation.

The root launcher shell owns the version footer placement. The label reads the
same `_engine_version()` value already used by the home page, so packaging and
fallback behavior remain unchanged. The home-page-only version label is removed
to avoid duplicate version text.

## Error handling

If the platform browser cannot be opened, the launcher remains usable and
shows the existing localized operation-error dialog rather than exiting or
changing the current page.

## Verification

- Unit/layout tests cover the exact `NUKE HOUR ` plus engine-version footer
  text, the right-bottom anchor, and removal of the old `· RA2` suffix.
- A launcher construction test confirms the emblem has a click binding and a
  hand cursor.
- Existing packaging and launcher tests continue to run unchanged.

## Scope boundaries

No changes are made to OpenRA in-game menus, iOS/Android projects, application
icons, website content, or the launcher window size.
