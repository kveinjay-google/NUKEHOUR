# RA2 Mac/iOS Codebase Research Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Systematically map the complete RA2 Mac/iOS codebase into evidence-backed, maintainable research notes that make later fixes and features safer.

**Architecture:** Treat the repository as seven connected subsystems: bootstrap/platform, game loop/network, rendering, input/UI, RA2 rules/content, build/deployment, and diagnostics. Each subsystem note records entry points, ownership, state flow, platform differences, extension points, failure modes, and exact source references; a final index links cross-subsystem call chains.

**Tech Stack:** C#/.NET 8, OpenRA engine and trait system, MiniYAML/Fluent, SDL2, UIKit/.NET iOS, MSBuild, Python/macOS packaging utilities.

---

### Task 1: Freeze the research baseline and inventory

**Files:**
- Create: `docs/codebase-research/README.md`
- Create: `docs/codebase-research/source-inventory.md`

- [ ] Record the repository root, current branch, HEAD, dirty-worktree warning, and directory/file counts.
- [ ] Inventory every tracked source/config file by subsystem and classify generated, vendored, application, test, and content files.
- [ ] Record untracked project extensions separately so they are studied without being mistaken for upstream engine code.
- [ ] Verify every top-level directory is represented in the inventory with `rg --files`.
- [ ] Commit only the two inventory documents.

### Task 2: Bootstrap, platform abstraction, settings, and iOS lifecycle

**Files:**
- Create: `docs/codebase-research/bootstrap-platform-ios.md`

- [ ] Trace `AppDelegate.FinishedLaunching` through `Game.InitializeAndRun`, engine directory resolution, platform factory creation, settings construction, mod discovery, and initial world creation.
- [ ] Document `Platform.CurrentPlatform`, the iOS override timing, SDL/UIKit ownership, app foreground/background callbacks, and support-directory semantics.
- [ ] Trace command-line settings precedence over `settings.yaml` and explain which preferences should persist on iPad.
- [ ] Record the exact packaged-mod versus writable-support-mod precedence and validate it against `InstalledMods` source behavior.
- [ ] Add a startup sequence diagram and cite every node with a file and symbol.
- [ ] Verify the documented chain by targeted searches and an iOS build.
- [ ] Commit the bootstrap/platform study.

### Task 3: Main loop, network synchronization, and hang diagnostics

**Files:**
- Create: `docs/codebase-research/game-loop-network-hangs.md`

- [ ] Trace `Game.Run` and `Game.Loop` through logic ticks, order processing, local server/client networking, rendering, event pumping, and shutdown.
- [ ] Identify all blocking calls, locks, waits, socket reads, queues, and main-thread-only operations reachable during a local skirmish.
- [ ] Map the watchdog breadcrumbs to actual source boundaries and interpret the captured `logic.network-receive.enter` hang without overstating causality.
- [ ] Document log files, rotation/lifetime, device-copy commands, missing observability, and safe future instrumentation points.
- [ ] Add a frame/tick/network sequence diagram plus a table of hang signatures and discriminating evidence.
- [ ] Verify diagnostic tests and record exact results.
- [ ] Commit the game-loop/network study.

### Task 4: Rendering, terrain, shroud, overlays, and the visible grid

**Files:**
- Create: `docs/codebase-research/rendering-terrain-overlays.md`

- [ ] Trace world rendering from viewport visible cells through terrain, shroud, annotations, post-processing, UI, and final presentation.
- [ ] Enumerate every code path capable of drawing cell diamonds, ramp polygons, screen-map bounds, selection geometry, path-search geometry, and editor overlays.
- [ ] Separate `TerrainGeometryOverlay`, renderable debug geometry, terrain asset seams, shroud patterns, map-grid data, and persisted debug preferences using concrete visual fingerprints.
- [ ] Use the screenshot and render-layer invariants to eliminate incompatible renderer candidates and rank the remaining causes by evidence.
- [ ] Record iOS policy enforcement points and explain why earlier suppression attempts did not affect the observed frame.
- [ ] Define a single-variable physical-device A/B matrix that can identify the exact texture/shader source before behavior is changed.
- [ ] Record the package-hash, screenshot, and logging evidence required for a later verified grid fix.
- [ ] Commit the rendering study without claiming the grid defect is fixed.

### Task 5: Input, touch gestures, orders, and widgets

**Files:**
- Create: `docs/codebase-research/input-touch-orders-ui.md`

- [ ] Trace UIKit/SDL touch events into OpenRA mouse events, gesture translation, widget routing, world selection, order generators, and network orders.
- [ ] Document gesture ownership, pointer capture, modal interception, focus, cancellation, and recovery after backgrounding.
- [ ] Trace MCV select, move, and deploy flows from input through actor traits and activities.
- [ ] Map Chrome/MiniYAML widget composition, logic binding, hotkeys, settings pages, sidebars, and iOS sizing overrides.
- [ ] Add call-chain tables for select, move, deploy, pause/menu, and settings interactions.
- [ ] Verify touch-related unit tests and physical-device smoke tests.
- [ ] Commit the input/UI study.

### Task 6: RA2 actor/trait rules and content graph

**Files:**
- Create: `docs/codebase-research/ra2-rules-content.md`

- [ ] Map `mod.yaml` manifests into rules, sequences, weapons, audio, chrome, maps, and Fluent localization.
- [ ] Explain inheritance, trait construction, conditional traits, activities, orders, locomotors, production, prerequisites, factions, and starting units.
- [ ] Inventory custom `OpenRA.Mods.RA2` code and map each type to the MiniYAML references that instantiate it.
- [ ] Record Yuri/custom-content additions separately from upstream RA2 behavior and identify incomplete dependency chains.
- [ ] Run MiniYAML lint and record errors versus non-blocking localization warnings.
- [ ] Commit the rules/content study.

### Task 7: Build, packaging, signing, installation, and persistence

**Files:**
- Create: `docs/codebase-research/build-deploy-device-testing.md`

- [ ] Trace project references and build outputs for engine, RA2 assembly, desktop launcher, and iOS app.
- [ ] Document bundle resource copying, codesigning/provisioning, bundle identifier, device installation, process launch, log collection, and app-container data persistence.
- [ ] Explain incremental-build hazards and how to prove a changed rule/code file is present in the installed bundle.
- [ ] Define a reproducible physical-iPad smoke matrix for startup, new game, MCV orders, touch settings, overlays, background/foreground, and 15-minute stability.
- [ ] Verify read-only signing/device commands against the connected device and record expected output patterns without embedding private signing material.
- [ ] Commit the build/deployment study.

### Task 8: Synthesize the maintainer index and development gates

**Files:**
- Create: `docs/codebase-research/architecture-index.md`
- Create: `docs/codebase-research/development-checklist.md`
- Modify: `docs/codebase-research/README.md`

- [ ] Link every subsystem note and build a symbol-to-document index for common development tasks.
- [ ] Add cross-subsystem diagrams for startup, one simulation frame, one render frame, one touch order, and one iOS deployment.
- [ ] Rank architectural risks by evidence, impact, and confidence; distinguish confirmed defects from hypotheses.
- [ ] Define mandatory pre-change, test, package-content, device-install, screenshot, log, and commit checks.
- [ ] Search all research notes for unsupported claims, stale paths, placeholders, and inconsistent symbol names; correct them.
- [ ] Run the complete applicable test/lint/build suite and record results.
- [ ] Commit the final research index and checklist.
