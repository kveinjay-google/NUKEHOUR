# NUKEHOUR MULTIPLAYER PHASE 1.2 REPORT

## Status

COMPLETE

The available Apple physical-device matrix is complete.  The wider Phase 1
status remains PARTIAL with `BLOCKED_ONLY_ON_ANDROID_PHYSICAL_MATRIX`.

## Physical Devices

iOS: Physical iPhone 17 Pro Max, iOS 26.6.1, arm64

macOS: Physical Apple Silicon Mac (Apple M5 Pro), macOS 26.5.1, arm64

Both devices ran on the same private Wi-Fi/LAN.  Persistent device identifiers
and private LAN addresses are intentionally excluded from committed evidence.

## Matrix

| Server | Client | Connect | Handshake | RuntimeProfile | RuntimeContract | Lobby | Map | Ready | Start | Spawn | Gameplay | Result |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| iOS | macOS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| macOS | iOS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |

Both directions used Direct IP on TCP port 1234.  Each peer observed select,
movement, resource update, production, completed production, and attack.  The
iOS-host direction reached 1822/1800 world ticks; the macOS-host direction
reached 1800/1800 world ticks.  Both ended with no desync, unexpected
disconnect, serialization error, compatibility false rejection, or observed
order rejection.

RuntimeContract for all four final journals:

`v1:ra2-presentation-capability-v2:deaddf88ff5342d7ce519fea70f827447e6d78bb1b2ef740c4543e5ba2288c9b`

## Localization Compatibility

Chinese ↔ English: NOT VERIFIED

Result: The available legal resource imports had different outer
`language.mix` hashes but identical `ra2.csf` size, header, and localization
code.  They therefore do not constitute an independently confirmed Chinese ↔
English pair.  The final matrix did prove that presentation-package variance
with the same `ra2-presentation-capability-v2` capability is not falsely
rejected.

## Negative Compatibility

Result: PASS

Controlled fixtures exercised a gameplay-core mismatch through the production
TCP admission framing and were rejected with RuntimeContract mismatch before
lobby/gameplay.  No user retail resources were modified.

## Direct IP

Result: PASS in both directions using private LAN `IP:1234`; no Master Server,
account, cloud service, or Dedicated Server was used.

## LAN Discovery

Result: NOT IMPLEMENTED / NOT VERIFIED on macOS.  The macOS runtime does not
install an `INearbyGameService`; Phase 1.2 did not expand discovery scope.
Direct IP remains the verified formal Local Multiplayer connection method.

## iOS Release Metadata

The application bundle now reports `CFBundleShortVersionString=1.0.7` and
`CFBundleVersion=7`, and diagnostic platform remains `ios/arm64`.  The shared
mobile version manifest still contributes the Android product display token to
the diagnostic build field; correcting that requires per-platform manifest
projection and is recorded as Release P1 rather than expanded into Phase 1.2.

## Regressions

New failures: None in the final matrix.

Physical testing found and fixed three issues before the final PASS:

- the runner selected a VPN address ahead of the same-subnet Wi-Fi address;
- Direct IP incorrectly required a LAN-discovery advertisement Session GUID;
- desktop DynamicMethod boolean sync hashing produced `0/1`, while iOS used the
  canonical `0x555/0xAAA`, causing an immediate gameplay desync.

One incremental iOS AOT artifact crashed in Mono startup.  A clean Release
rebuild removed the corrupted incremental state; the clean signed arm64 bundle
then completed both physical tests.

## Phase 1 Remaining Gate

Android physical validation: PENDING

Pending matrix:

- iOS ↔ Android
- macOS ↔ Android
- iOS + Android + macOS in one session

Phase 1 must not be marked COMPLETE until these tests pass.  Local Multiplayer
remains a permanent supported mode with iOS, Android, and macOS Local Host,
LAN, Direct IP:Port, default TCP 1234, and no account/cloud/Dedicated Server
dependency.  Phase 2 was not started.

## Git

Starting Commit (macOS): `f0c9bfe0d3b0bb6750633a2c006f2b5ec0306c58`

Starting Commit (mobile): `ae9aab6b25032261ef5a5ed623b4050e80d147b6`

Final commits are recorded in the final handoff because a commit cannot embed
its own final object ID.

## Evidence

Sanitized machine-readable evidence is stored in
`docs/testing/results/nukehour-multiplayer-phase1.2-20260908/evidence.json`.
