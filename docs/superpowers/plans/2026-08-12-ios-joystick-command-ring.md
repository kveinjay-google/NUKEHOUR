# iOS Joystick Command Ring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the rectangular iOS action cluster with five illustrated command buttons arranged in a thumb-reachable ring around the viewport joystick.

**Architecture:** Keep the existing command logic and joystick widget unchanged. Add a dedicated icon sprite sheet and Chrome collection, then update only the iOS action container geometry and image bindings.

**Tech Stack:** OpenRA Chrome YAML, PNG sprite assets, NUnit, .NET iOS AOT build.

---

### Task 1: Generate and validate command art

**Files:**
- Create: `mods/ra2/uibits/ios-joystick-command-icons.png`

- [ ] Generate five isolated icons on a removable chroma-key background.
- [ ] Remove the key color, crop each icon to an equal cell, and assemble one horizontal sprite sheet.
- [ ] Verify alpha corners, equal cell sizes, and legibility at the final rendered size.

### Task 2: Add the command ring with test coverage

**Files:**
- Modify: `mods/ra2/chrome.yaml`
- Modify: `mods/ra2/chrome/ingame.yaml`
- Modify: `engine/OpenRA.Test/IosViewportActionLayoutTest.cs`

- [ ] Add failing assertions for the five dedicated image names and ring geometry.
- [ ] Run the focused test and confirm it fails for the missing artwork mappings.
- [ ] Define the icon collection and update button coordinates to the approved half-ring.
- [ ] Run the focused test and confirm it passes.

### Task 3: Package and device verification

**Files:**
- Verify: `ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app`

- [ ] Run all engine tests and require zero failures.
- [ ] Build the signed `ios-arm64` Release app and require zero errors.
- [ ] Verify the source and Bundle icon hashes match.
- [ ] Install and launch on the connected iPad, then confirm the process remains alive.
- [ ] Commit only the focused design, plan, artwork, layout, and test files.
