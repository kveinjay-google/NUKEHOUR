# iPad Fullscreen Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Display OpenRA/RA2 as a full-screen landscape iPad game and verify the original OpenRA menu is usable at the actual SDL drawable size.

**Architecture:** UIKit hosts the process and SDL owns the game view. The iOS platform window requests SDL fullscreen, refreshes its dimensions from the live drawable, and the existing OpenRA renderer consumes those dimensions. The existing RA2 menu remains the first choice; UIKit menu code is a fallback only if that menu stays unusable.

**Tech Stack:** .NET 8 for iOS, UIKit, SDL2 UIKit backend, OpenGL ES 3, OpenRA.

---

### Task 1: Request a fullscreen SDL iOS game window

**Files:**
- Modify: `engine/OpenRA.Platforms.Default/Sdl2PlatformWindow.cs`
- Test: iOS Simulator launch

- [ ] Confirm `SDL_WINDOW_FULLSCREEN` is absent from the current iOS window flags with `rg -n -C 6 'WindowFlags|SDL_CreateWindow' engine/OpenRA.Platforms.Default/Sdl2PlatformWindow.cs`.
- [ ] Replace the immutable flags declaration with a mutable `windowFlags`, retaining `SDL_WINDOW_OPENGL | SDL_WINDOW_ALLOW_HIGHDPI`, then append `SDL_WINDOW_FULLSCREEN` when `OperatingSystem.IsIOS()` returns true.
- [ ] Build using the iOS Simulator target and require zero errors.
- [ ] Commit only `engine/OpenRA.Platforms.Default/Sdl2PlatformWindow.cs` with message `Request fullscreen SDL window on iOS`.

### Task 2: Synchronize live iOS SDL dimensions

**Files:**
- Modify: `engine/OpenRA.Platforms.Default/Sdl2PlatformWindow.cs`
- Test: iOS Simulator screenshot and engine log

- [ ] Make `windowSize` writable and add `RefreshIOSDrawableSize()`. It queries `SDL_GetWindowSize` and `SDL_GL_GetDrawableSize`; when all four values are positive, updates `windowSize`, `surfaceSize`, and `windowScale` within `syncObject`.
- [ ] Invoke the helper immediately after final SDL window creation, before OpenRA renderer initialization.
- [ ] Reinstall the simulator app, copy RA2 content into its Documents content folder, launch, wait 20 seconds, and capture `/tmp/openra-ios-fullscreen.png`.
- [ ] Verify the SDL view fills its iPad application region in landscape, not a floating window.
- [ ] Commit only `engine/OpenRA.Platforms.Default/Sdl2PlatformWindow.cs` with message `Sync iOS window size with SDL drawable`.

### Task 3: Verify the original menu before adding a fallback

**Files:**
- Modify only if needed: `ios/OpenRA.iOS/AppDelegate.cs`, `ios/OpenRA.iOS/ViewController.cs`
- Test: iOS Simulator UI screenshots and app logs

- [ ] After 30 seconds, capture `/tmp/openra-ios-menu-fullscreen.png` and scan the app logs for `exception`, `fatal`, or `error`.
- [ ] Activate a visible primary OpenRA setup/menu action using the Simulator; capture a follow-up screenshot to establish that the screen changed and SDL input reached OpenRA.
- [ ] If the original menu fits and accepts input, preserve it and do not create a fallback menu.
- [ ] Only if it remains unusable, add a UIKit pre-launch menu containing Continue, Single Player, Settings, and Quit. Its Continue action invokes the existing OpenRA launch path; hide it after SDL initializes. Do not duplicate OpenRA game rules, resource loading, renderer, or in-game UI.
- [ ] If a fallback is added, commit exactly `AppDelegate.cs` and `ViewController.cs` with message `Add iPad launch menu fallback`.
