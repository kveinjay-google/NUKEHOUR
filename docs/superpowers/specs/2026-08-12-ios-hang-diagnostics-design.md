# iOS Hang Diagnostics Design

## Problem

OpenRA already writes separate client, server, debug, graphics, sound, performance, and exception logs. These logs capture exceptions and network failures, but a live process whose main thread stops does not throw and therefore leaves no precise failure record. The existing logger also flushes on a timer, so the final seconds can be absent.

## Design

Add an iOS-only `HangDiagnostics` service in the engine. The main game loop records lightweight breadcrumbs at component boundaries: loop dispatch, UI tick, network receive, order readiness, world tick, render preparation, world draw, UI draw, and buffer presentation. Each breadcrumb stores its stage, monotonic timestamp, logic frame, render frame, network frame, connection state, managed-memory usage, and a short ring buffer of recent transitions.

An independent background watchdog checks the main heartbeat once per second. If the heartbeat has not advanced for eight seconds while diagnostics are active, it writes `Logs/hang-diagnostic.log` directly with `FileStream` configured for immediate flush. It repeats only when the stuck stage changes or after thirty seconds, preventing unbounded writes. The file is capped by replacing it at the start of each app launch.

iOS lifecycle callbacks pause the watchdog before suspension and reset the heartbeat on resume so normal backgrounding cannot create false hang reports. The feature records data only; it does not kill or restart the game.

## Expected diagnostic value

The final stage identifies the blocked subsystem—for example `logic.world-tick.enter`, `render.world-draw.enter`, `render.present.enter`, or `network.receive.wait`. Recent transitions show whether the simulation, rendering, or network stopped first. This is sufficient to narrow the next investigation to a specific boundary even when no managed exception exists.

## Testing

Unit-test stale-heartbeat detection, paused behavior, report throttling, and breadcrumb ring-buffer ordering with injected timestamps. Run the full engine suite, build the signed iPad app, install, and launch it.
