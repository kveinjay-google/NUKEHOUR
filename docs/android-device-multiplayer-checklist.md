# Android Physical-Device Multiplayer Checklist

Status: `BLOCKED_ON_ANDROID_DEVICE_VALIDATION`

Use this checklist when a real Android ARM64 device is connected. Emulator, simulator, loopback, or a desktop Android compatibility layer cannot close Phase 1.1.

## Preconditions

- Real Android ARM64 device visible to `adb devices -l` and authorized.
- Real iOS device and Apple Silicon Mac available.
- All three devices use the same ordinary Wi-Fi LAN and can reach TCP port 1234 directly.
- Current Phase 1.1 builds are installed; each client shows the expected NUKE HOUR product name.
- Each device has user-imported required retail content. Do not place retail assets in an installation package or project artifact.
- Record device model, OS version, CPU architecture, and app build only. Do not record serial numbers, UDIDs, advertising identifiers, or other unique device identifiers.
- Disable VPN, Private Relay, guest-network isolation, and mobile-data fallback for the run.

## Per-direction procedure

For every row below:

1. Start Local Multiplayer on the designated server and confirm TCP port 1234.
2. Join from the designated client by the LAN address or local discovery path under test.
3. Confirm both peers enter the same lobby and report compatible RuntimeContract state.
4. Select the same map and confirm identical map UID/readiness.
5. Set both players Ready and start the match.
6. Issue movement, attack, production, and stop/cancel orders from both peers.
7. Run long enough to observe synchronized gameplay and a result/clean disconnect with no out-of-sync report.
8. Save sanitized server/client logs and the build/runtime summaries. Remove unique device identifiers before storing evidence.

| Server | Client | Connect | Lobby | Map | Ready | Start | Gameplay | Result |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| iOS | Android |  |  |  |  |  |  |  |
| Android | iOS |  |  |  |  |  |  |  |
| macOS | Android |  |  |  |  |  |  |  |
| Android | macOS |  |  |  |  |  |  |  |
| iOS | macOS |  |  |  |  |  |  |  |
| macOS | iOS |  |  |  |  |  |  |  |

## Three-client game

- Start one server on any of the three platforms.
- Join exactly one iOS, one Android ARM64, and one Apple Silicon macOS client to the same lobby.
- Confirm all three have the same map UID and reach Ready.
- Start once, then issue gameplay orders from every client.
- Confirm synchronized world progress, no out-of-sync report, and a clean result/disconnect for all participants.

## RuntimeContract compatibility cases

- English and Chinese `language.mix`/`langmd.mix` presentation variants with the same NUKE HOUR gameplay core: must connect.
- Reimported identical resources at different local paths: must connect.
- Client with the old `ra2-required-v1` contract: must reject before lobby admission.
- Missing RuntimeContract: must reject with `RUNTIME_CONTRACT_MISSING`.
- Handshake schema or orders protocol mismatch: must reject with `PROTOCOL_MISMATCH`.
- Modified `nukehour-core-sha256` gameplay input: must reject before lobby admission.
- Different selected map UID: must block Ready/Start with `MAP_MISMATCH` or the established map-readiness reason.

## Closure evidence

Phase 1.1 may change from BLOCKED to COMPLETE only after all six rows, the three-client game, and all rejection cases have dated, sanitized evidence from real devices and the regression suites show no new failures.
