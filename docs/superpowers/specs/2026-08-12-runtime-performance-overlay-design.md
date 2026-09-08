# Runtime Performance Overlay Design

## Goal

Extend the existing OpenRA performance text overlay so iOS and desktop builds show actionable runtime resource information without adding meaningful render-loop overhead or risking a platform-specific crash.

## Display

The overlay will show:

- rolling FPS;
- current game-process CPU utilization, normalized to total device capacity;
- resident process memory and managed GC heap memory;
- simulation tick and render timing;
- draw batches;
- physical output resolution, logical world viewport size/downscale, and world framebuffer size.

CPU and memory are process metrics, not whole-device metrics. This keeps the values relevant to game diagnosis and uses APIs available to the shared .NET runtime.

## Architecture

`RuntimeResourceSampler` owns process resource sampling and formatting-independent state. It samples no more often than every 500 ms, computes CPU from the delta between process CPU time and monotonic elapsed time, and caches the latest resident/managed memory values. Platform/API failures are caught and represented as unavailable data so the diagnostics feature cannot stop gameplay.

`PerfDebugLogic` keeps responsibility for the overlay text and combines the cached system sample with existing frame/tick/render counters. `Renderer` exposes its output surface size as a read-only property so the overlay can distinguish actual output resolution from the logical viewport and world framebuffer.

## Verification

Unit tests cover CPU normalization/clamping, interval caching, memory formatting, and unavailable-data behavior. The engine test project and relevant builds must pass before device installation. On iPad, enable the existing performance text toggle and verify that CPU, memory, FPS, and all three size readings update while a match remains responsive.
