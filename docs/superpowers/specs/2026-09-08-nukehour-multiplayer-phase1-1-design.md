# NUKE HOUR Multiplayer Phase 1.1 Design

## Scope

Phase 1.1 closes two Phase 1 risks only:

1. validate the existing direct-LAN protocol on real iOS, Android ARM64, and Apple Silicon macOS devices, including the six server/client directions and one three-client game;
2. audit and correct RuntimeContract false rejection caused by whole-file hashing of user-imported Red Alert 2 archives.

Dedicated servers, accounts, matchmaking, relay/NAT traversal, chat services, cloud saves, and any replacement online protocol are outside this phase. Local Multiplayer and Online Multiplayer are independent product entry points. Local Multiplayer is a permanent first-class product mode: LAN hosting and discovery, direct `IP:port`, and default TCP port `1234` remain available without a NUKE HOUR cloud service, account, public lobby, or dedicated server, and iOS, Android, and macOS may each act as Local Host. Future online services must reuse the same handshake, RuntimeProfile, RuntimeContract, orders protocol, mod/map/session compatibility, and gameplay simulation, and must not remove, hide, weaken, or make Local Multiplayer dependent on an online service.

## Evidence and compatibility boundary

The current `RuntimeResourceFingerprint` hashes every byte of `ra2.mix`, `language.mix`, `ra2md.mix`, and `langmd.mix`. Local real-resource samples contain one identical `ra2.mix` and one identical `ra2md.mix`, but two legitimate language families whose complete-archive hashes differ. Entry inspection attributes those differences to localized strings, fonts, UI graphics, cameo art, speech/audio, credits, keyboard text, mission text, and videos.

The mounted imported packages provide sprites, voxels, palettes, terrain art, audio, text, and video. NUKE HOUR simulation rules, weapons, order serialization, and synchronization primitives are repository-owned and covered by `nukehour-core-sha256`; selected maps are checked independently by OpenRA map UID. No imported MIX entry was found in the mod's rules or weapons loading graph. Therefore the imported archives are resource identity and rendering capabilities, not gameplay compatibility identity.

If future investigation finds imported data that changes deterministic simulation, that input must be added to an explicit semantic gameplay manifest and the capability version must be advanced. Whole-archive hashes must not be reintroduced merely as a proxy.

## RuntimeContract algorithm

Keep the Phase 1 handshake and RuntimeContract wire shape unchanged. Change only the resource fingerprint semantics and advance the RA2 resource capability identifier so old whole-file contracts fail closed.

The resource compatibility digest is a canonical SHA-256 over:

- a versioned domain marker;
- the sorted logical names of required imported packages;
- confirmation that each required logical package can be opened.

Missing required packages fail contract construction. Package bytes, local paths, timestamps, import order, source media, language choice, optional presentation packages, and product build numbers are excluded. Optional Yuri archives are not a deterministic gameplay input and do not split otherwise compatible peers.

The complete admission identity remains:

- handshake schema and orders protocol;
- engine compatibility;
- mod id;
- `nukehour-core-sha256` gameplay compatibility;
- versioned resource capability plus required-resource capability digest;
- selected map UID at map validation/readiness.

This separates `RESOURCE_IDENTITY` from `GAMEPLAY_COMPATIBILITY`: differing retail archive bytes can remain compatible, while a changed core, protocol, mod, contract schema/capability, missing required package, or selected map is still rejected.

## Tests

Add named RC1-RC9 coverage:

- RC1 identical resources at different local paths produce an identical contract;
- RC2 the same resources after a fresh reimport produce an identical contract;
- RC3 confirmed presentation-only differences remain compatible;
- RC4 confirmed localization-only differences remain compatible;
- RC5 a gameplay-core change is rejected;
- RC6 a missing required gameplay asset is rejected before admission;
- RC7 a mod id mismatch is rejected;
- RC8 a mod gameplay compatibility mismatch is rejected;
- RC9 an engine compatibility mismatch is rejected.

Existing handshake, direct TCP, parity, NUnit, Python, platform build, and clean-package checks remain regression gates. The known mobile baseline may stay the same or improve, but no new failure is accepted.

## Device-validation policy

Use real physical devices only: an iOS device, an Android ARM64 device, and an Apple Silicon Mac on the same Wi-Fi, TCP port 1234. Each matrix cell covers connection, lobby, map identity, Ready, Start, gameplay orders, and result observation. A separate three-client game covers all platforms together.

No Android device is currently visible to ADB. Phase 1.1 therefore cannot claim complete physical-device closure in this environment. Finish all non-Android work, record any evidence-backed device results, create `docs/android-device-multiplayer-checklist.md`, and report `BLOCKED_ON_ANDROID_DEVICE_VALIDATION` until the real Android matrix and three-client game are executed.

## Stop condition

Phase 1.1 is COMPLETE only when RC1-RC9, the full real-device matrix, the three-client game, compatibility rejection, and regression gates pass. Otherwise report PARTIAL or BLOCKED with exact missing evidence. Stop after the Phase 1.1 closure report and do not begin Phase 2.
