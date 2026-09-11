# Dedicated Server DS1-DS10 Evidence Schema

`tools/run_dedicated_server_smoke.py` emits schema version 1 JSON.  The report
contains exactly ten ordered cases; each case has a terminal `PASS`, `FAIL`, or
`SKIPPED` result and a non-empty evidence description.

| Case | Gate | Evidence source |
| --- | --- | --- |
| DS1 | Server starts headless | Fresh process and machine-readable status |
| DS2 | Server listens | Real TCP connection to an ephemeral loopback port |
| DS3 | Valid client admitted | Two-client physical session with one RuntimeContract |
| DS4 | Incompatible client rejected | Controlled protocol/runtime/mod/engine/map fixtures |
| DS5 | Map loads | Server status plus physical session map identity |
| DS6 | Game starts | Physical lobby reaching synchronized gameplay |
| DS7 | Orders accepted | Movement, resources, production, completion, and attack observed |
| DS8 | Disconnect does not crash server | Dedicated host-independence policy tests |
| DS9 | Clean shutdown | SIGTERM, exit code 0, and final `STOPPED` status |
| DS10 | Local Multiplayer unchanged | Shared protocol, map, safe-start, and Local host suites |

The process probe always uses a fresh temporary support directory,
`AdvertiseOnline=False`, a dynamically allocated test port, and the existing
shared server executable.  Physical evidence is optional at the command-line;
DS3, DS6, and DS7 are explicitly `SKIPPED` when it is not supplied.

Committed reports are sanitized.  Device identifiers, participant/session
nonces, private endpoints, and private LAN addresses are removed.  A report
must never turn a missing physical or public-network run into a synthetic pass.

Example:

```sh
python3 tools/run_dedicated_server_smoke.py \
  --physical-first /path/to/ios-client.json \
  --physical-second /path/to/macos-host.json \
  --dedicated-status /path/to/server-status.json \
  --output docs/testing/results/dedicated-server-phase2/evidence.json
```

The three physical inputs are an indivisible evidence set. The validator
requires one run ID, complementary client control roles, one Direct IP endpoint,
matching map/port/protocol status, and overlapping UTC timestamps.
