# macOS Launcher Shell Layout Design

## Scope

Fix only the macOS launcher's outer shell. The right content surface must cover
the complete area to the right of the sidebar, and the bottom language chooser
must remain fully visible at the launcher's fixed 1000×700 content size.

## Design

- Replace the right panel's decorative 12-pixel inset with an edge-to-edge
  rectangle beginning at the sidebar boundary.
- Give the language label, selector, and footer deterministic bottom slots.
  The selector receives at least 28 pixels of height instead of relying on the
  macOS Tk theme's requested height during `pack` negotiation.
- Keep navigation, page contents, colors, fonts, and launcher behavior intact.
- Expose the shell rectangles through one pure layout function so geometry can
  be regression-tested without opening a Tk window.

## Verification

- Unit tests assert edge-to-edge right-panel coverage.
- Unit tests assert the language label and selector fit inside the sidebar,
  the selector has a usable height, and the footer cannot overlap them.
- Run the existing launcher language and macOS branding tests after the change.
