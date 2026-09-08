# iOS SDL patches

`SDL_uikitwindow-ipad-scene.patch` records the local SDL UIKit change included
in both `ios-arm64/libSDL2.a` and `iossimulator-arm64/libSDL2.a`.

The patch attaches SDL's window to the active `UIWindowScene` and sizes it from
the scene coordinate space. This prevents iPadOS from creating a detached
compatibility window and keeps rendering and touch coordinates aligned in
full-screen and windowed multitasking modes.
