# iOS Release Regression Gates

An iOS build is not testable or deliverable merely because it reaches the main menu. It must pass the gates below on a physical device without an attached debugger.

## Mandatory loading gate

Run `ios/scripts/run-loading-smoke.sh` against the signed Release build. The script launches a fixed skirmish and succeeds only after a regular world completes loading and executes its first simulation tick. A timeout automatically retrieves the current Logs and Diagnostics folders.

## Diagnostic fail-open invariant

Diagnostics must never wait on the game thread. Trace writes and watchdog snapshots use non-blocking acquisition and may drop diagnostic events under contention. A dropped-event counter is included in each hang report. `DiagnosticTraceBufferTest` injects lock contention and blocks a change that reintroduces waiting.

## Loading cursor invariant

Every `IWorldLoaded` callback records three legacy-watchdog stages: `enter`, `callback-exit`, and `exit`. This distinguishes a stuck game callback from a stuck diagnostic scope even when structured trace collection is unavailable.

## Historical hang signatures

| Signature | Reproducer | Required regression |
| --- | --- | --- |
| `LoadWidgetAtGameStart.WorldLoaded` is the last completed perf item; no `.oradiag` is produced; process remains alive | Release build, fixed skirmish, wait for first playable tick | Trace writes and snapshots never block; loading smoke reaches `smoke-ready` |
| Load completes but the first simulation tick never occurs | Release build, fixed skirmish | Loading smoke must fail and retrieve device evidence |

Every newly confirmed hang must add one row with a deterministic reproducer and an automated pass/fail condition. A fix is incomplete until its row exists and its regression passes.

## Release sequence

1. Run targeted unit tests, including diagnostics and the subsystem changed by the patch.
2. Build the signed `Release/ios-arm64` app with per-frame debug polling disabled.
3. Install it on the physical iPad.
4. Run the mandatory loading gate for Allied, Soviet, and Yuri starting configurations as applicable.
5. Preserve the gate evidence with the build identifier; do not replace it with a manual observation.
