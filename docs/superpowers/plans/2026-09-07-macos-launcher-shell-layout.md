# macOS Launcher Shell Layout Implementation Plan

1. Add failing geometry tests for the right content surface and language slot.
2. Introduce a pure launcher-shell layout policy and use it in initial build and
   language-triggered rebuild paths.
3. Replace bottom-packed language/footer widgets with explicit non-overlapping
   bounds.
4. Run focused launcher tests, syntax checks, and macOS branding verification.
5. Increment only the macOS product/build version; multiplayer compatibility is
   unchanged because this is a launcher-only layout fix.
