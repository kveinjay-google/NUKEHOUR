# NUKEHOUR Multiplayer Phase 1 Design

## Scope

Unify the OpenRA handshake used by the macOS, iOS, and Android NUKE HOUR builds. This phase changes only protocol identity, compatibility validation, rejection reporting, tests, and local/direct-network verification. It does not add accounts, matchmaking, relay, NAT traversal, or hosted infrastructure.

## Audit Result

The active macOS and mobile workspaces both use the same OpenRA 20250330 handshake implementation: wire handshake 7, orders 23, `Mod`, `Version`, and client-only `OrdersProtocol`. Neither active workspace contains a production `RuntimeProfile` or `RuntimeContract` implementation. An older iOS hardening branch contains an iOS-only contract draft, but that draft hashes iOS manifests and iOS managed assemblies and therefore cannot be used as a cross-platform contract.

The existing `Compatibility` metadata is already generated from the common engine version, order/sync implementation, and RA2 rules/weapons inputs. The current macOS and Android values are identical. This is the correct platform-neutral gameplay identity to reuse.

## Protocol

- Preserve transport handshake 7 so legacy peers reach an explicit YAML rejection instead of disconnecting before an error can be returned.
- Add required `HandshakeSchema`, currently 1, to both request and response.
- Add server `OrdersProtocol` so the client validates the server before responding.
- Add required `EngineCompatibility`, `RuntimeProfile`, and `RuntimeContract` to both directions.
- Preserve `Mod`, `Version`, authentication, direct IP, and TCP port 1234 behavior.
- Unknown fields are ignored only within a supported schema and are therefore optional metadata. Any new required field or semantic/serialization change increments `HandshakeSchema`; wire framing changes increment the transport handshake version.

## Runtime Profile

`RuntimeProfile` is a canonical, bounded, path-free diagnostic description with a fixed field order:

- profile schema
- platform
- process architecture
- engine version
- NUKE HOUR product/build label
- mod id
- runtime type
- imported-resource mode

The profile includes no device identifier, account, IP address, absolute path, timestamp, or machine fingerprint. Platform and product-build differences are diagnostic and are not compared as gameplay equality.

## Runtime Contract

`RuntimeContract` is a canonical identity containing a contract schema, a resource capability id, and a lowercase SHA-256 digest. The digest frames and hashes:

- contract schema
- engine compatibility
- orders protocol
- mod id
- generated mod/gameplay compatibility value
- normalized imported-resource capability
- deterministic fingerprint of the explicitly configured imported resources

For RA2 the fingerprint covers required `content|ra2.mix` and `content|language.mix`, plus optional `content|ra2md.mix` and `content|langmd.mix`. Canonical logical names are sorted and length-framed with required/optional role, presence, byte length, and SHA-256 content digest. Missing required content prevents contract creation. Optional music, screenshots, saves, logs, UI caches, local settings, file paths, timestamps, and temporary names are excluded. Maps remain independently identified by OpenRA map UID and readiness state.

This design intentionally does not hash the whole imported directory. Reinstalling, moving, or reimporting equivalent content produces the same contract. Changing an engine/rules/weapons compatibility input changes the generated core compatibility value and therefore the contract.

## Validation and Errors

Both peers use one validator and the same order:

1. handshake schema
2. orders protocol
3. engine compatibility
4. mod id and mod compatibility
5. runtime profile shape and runtime contract
6. map/session readiness before game start

Stable internal reasons are `PROTOCOL_MISMATCH`, `ENGINE_MISMATCH`, `MOD_MISMATCH`, `RUNTIME_CONTRACT_MISSING`, `RUNTIME_CONTRACT_MISMATCH`, and `MAP_MISMATCH`. Server responses use localized, user-readable messages. Debug logs show versions, platform/resource-profile metadata, and a shortened contract prefix, never tokens, paths, or full resource contents.

## Delivery

The implementation is identical in both repositories. Each repository receives its own ordered engine patch so a fresh engine checkout reproduces the change. A parity test compares the canonical protocol source and documentation across the two workspaces when both are present.

Physical-device LAN rows are reported only when actually executed. Unavailable iOS/Android hardware remains `NOT DEVICE VERIFIED` rather than being inferred from unit or simulator tests.
