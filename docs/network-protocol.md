# NukeHour Network Compatibility

## Product Architecture

NUKE HOUR exposes two independent multiplayer entry points: **Local Multiplayer** and **Online Multiplayer**. Local Multiplayer is a permanent, first-class mode: it requires no NUKE HOUR cloud service, account, public lobby, or dedicated server; iOS, Android, and macOS may each host directly on the same LAN/Wi-Fi; LAN discovery and direct `IP:port` remain supported, with TCP port `1234` as the default.

Future Online Multiplayer may add a NUKE HOUR lobby and dedicated game-server deployment, but it must reuse the same handshake, RuntimeProfile, RuntimeContract, orders protocol, mod/map/session compatibility, and deterministic gameplay simulation. It must not create a LAN-incompatible gameplay protocol or remove, hide, weaken, or force-replace Local Multiplayer.

## Handshake Protocol

NUKE HOUR uses OpenRA transport handshake version 7 and handshake schema version 1. The transport version remains 7 so an older peer can receive an explicit incompatibility response instead of being disconnected before YAML negotiation.

Both `HandshakeRequest` and `HandshakeResponse` require the same compatibility fields:

| Field | Meaning |
| --- | --- |
| `HandshakeSchema` | Required NUKE HOUR handshake schema; currently `1`. |
| `OrdersProtocol` | OpenRA order serialization version; currently `23`. |
| `EngineCompatibility` | Platform-neutral OpenRA engine baseline. |
| `Mod` | Active internal mod id. |
| `Version` | Generated gameplay compatibility value, not the product display version. |
| `RuntimeProfile` | Canonical diagnostic runtime description. |
| `RuntimeContract` | Canonical gameplay/resource compatibility identity. |

Authentication fields remain optional and retain their existing behavior. Direct connections remain `IP:port` over TCP and default to port `1234`.

## Orders Protocol

Orders protocol 23 is unchanged. It includes correlated safe-start requests/rejections and the current map-readiness flow. Any change to the serialized order format must increment `ProtocolVersion.Orders`.

## RuntimeProfile

The runtime profile schema is `v1` with a fixed field order:

```text
v1;platform=...;architecture=...;engine=...;build=...;mod=...;runtime=...;resources=...
```

Values are canonically percent-encoded. The fields describe platform, process architecture, engine version, NUKE HOUR product/build label, mod, runtime type, and imported-resource mode.

The profile is diagnostic. Platform, architecture, and display-build differences do not by themselves make two peers gameplay-incompatible. The profile never contains a device serial, account, IP identity, machine fingerprint, local path, timestamp, or temporary name.

## RuntimeContract

The runtime contract schema is `v1`:

```text
v1:<resource-capability>:<lowercase-sha256>
```

The digest uses explicit length framing and covers:

- contract schema;
- engine compatibility;
- orders protocol;
- mod id;
- generated core/gameplay compatibility;
- normalized imported-resource capability;
- deterministic imported-resource fingerprint.

For RA2 the capability is `ra2-presentation-capability-v2`. The imported retail MIX archives provide presentation resources, not deterministic simulation definitions. The resource fingerprint therefore covers the canonical sorted logical names and verified presence of required `content|ra2.mix` and `content|language.mix`. Missing required content prevents the runtime contract from being created. Archive bytes, local paths, language variants, import order, and optional `content|ra2md.mix` / `content|langmd.mix` presence or bytes do not change gameplay compatibility. The project-owned rules, weapons, order serialization, and sync inputs remain covered by `nukehour-core-sha256`, and the selected map remains independently covered by its map UID.

This is an intentional separation between resource identity and gameplay compatibility. A retail archive may have a different identity because of localized strings, speech, video, fonts, or artwork without becoming multiplayer-incompatible. If imported content later contributes to deterministic simulation, its normalized semantic inputs must be added to an explicit gameplay manifest and the resource capability must advance again; whole-archive hashing is not a valid substitute.

The required-capability digest only evaluates the configured required archive names and their availability; it does not hash the imported directory or any archive bytes. Optional music archives, screenshots, saves, logs, UI caches, local settings, absolute paths, import timestamps, device ids, and temporary names are excluded.

The generated core compatibility value is built from the engine baseline, order/sync implementation, and RA2 rules/weapons. A gameplay-critical change therefore changes the runtime contract. Equivalent content moved to another directory, reimported, or installed on another device retains the same contract.

Maps are checked separately using the OpenRA map UID and client/server readiness state.

## Compatibility Rules

Both server and client validate the other peer before admission in this order:

1. handshake schema;
2. orders protocol;
3. engine compatibility;
4. runtime profile shape and its self-consistency with engine/mod fields;
5. mod id, gameplay compatibility, and runtime contract;
6. map/session UID and readiness before starting the game.

The server performs compatibility checks before password/authentication and before creating or admitting a lobby client. The client validates the protocol envelope before considering an external-mod switch, then performs the full compatibility check before sending its response. A map UID mismatch or unavailable current map blocks start before simulation.

## Versioning Rules

Within a supported `HandshakeSchema`, unknown fields are treated as optional metadata and ignored. They cannot override a known field.

Increment `HandshakeSchema` when any of these changes:

- a field is removed;
- a field changes meaning;
- the YAML serialization contract changes;
- a required field is added;
- the runtime-contract algorithm changes incompatibly.

Increment the transport handshake version only when the binary handshake framing changes. Increment the orders protocol when gameplay network-order serialization changes. Optional diagnostic metadata can be added without a schema bump only after confirming that older parsers safely ignore it.

No platform may introduce a required handshake field behind `#if IOS`, `#if ANDROID`, `#if MACOS`, or another platform-only fork.

## Rejection Reasons

| Code | Meaning | User-facing result |
| --- | --- | --- |
| `PROTOCOL_MISMATCH` | Schema or orders protocol is unsupported, or the required profile is malformed. | Server requires a compatible/newer multiplayer protocol. |
| `RUNTIME_CONTRACT_MISSING` | A schema-compatible peer omitted the contract. | Server requires a newer NUKE HOUR multiplayer protocol. |
| `RUNTIME_CONTRACT_MISMATCH` | Contract syntax, resource capability, or digest differs. | Imported resources are incompatible with this session. |
| `MOD_MISMATCH` | Mod id or generated gameplay compatibility differs. | Selected game/mod configuration differs. |
| `MAP_MISMATCH` | Map UID or current map readiness differs. | Selected map/session cannot start. |
| `ENGINE_MISMATCH` | Engine baseline differs. | NUKE HOUR version is incompatible with the server. |

Debug logs may include the schema, orders version, platform, architecture, resource capability, result code, and only the first 12 hexadecimal characters of the contract. Logs must never include authentication tokens, full resource contents, device identifiers, or local content paths.

## Breaking Change Policy

Protocol changes must be made in the shared OpenRA implementation and reproduced byte-for-byte in the macOS and mobile repositories. The cross-repository parity check covers the handshake, compatibility implementation, protocol constants, and this document. A schema change is not complete until round-trip, missing/invalid-field, version, resource stability, and cross-platform profile tests pass.

Physical LAN or direct-IP rows are reported as passing only after the connection, lobby map synchronization, ready/start flow, and gameplay smoke orders run on the named devices. An unavailable device is recorded as `NOT DEVICE VERIFIED`.
