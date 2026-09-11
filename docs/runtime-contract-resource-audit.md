# RuntimeContract Resource Audit — Phase 1.1

## Result

`FALSE_REJECTION_CONFIRMED`

The Phase 1 whole-file resource fingerprint rejected legitimate English and Chinese retail presentation archives even though NUKE HOUR's deterministic gameplay inputs were the same. Phase 1.1 replaces whole-archive identity with a required-resource capability check and advances the capability to `ra2-presentation-capability-v2`.

## Real resource evidence

The audit inspected existing local, user-owned imports in place. No retail bytes were copied into the repository or build outputs.

| Archive | Distinct real samples | Size(s) | Whole-file SHA-256 result |
| --- | ---: | --- | --- |
| `ra2.mix` | 1 across 5 copies | 281,888,480 | identical |
| `language.mix` | 2 across 6 copies | 52,913,656 / 56,728,325 | different |
| `ra2md.mix` | 1 across 5 copies | 204,527,696 | identical |
| `langmd.mix` | 2 across 6 copies | 84,643,206 / 85,048,736 | different |

The two complete tested resource families were:

- English presentation set: common `ra2.mix` and `ra2md.mix`, 52,913,656-byte `language.mix`, 84,643,206-byte `langmd.mix`;
- Chinese presentation set: the same `ra2.mix` and `ra2md.mix`, 56,728,325-byte `language.mix`, 85,048,736-byte `langmd.mix`.

Running both complete families through the final V2 implementation produced the same resource-capability fingerprint: `8ff70be6a9603b4026e08e3f9b6580c266c3857c84b3d9637f1b6c1fe47a946e`.

OpenRA's own MIX parser resolved the differing top-level language entries. `language.mix` differences were confined to `audio.mix`, `cameo.mix`, `grfxtxt.shp`, `ra2.csf`, fonts/credits/local filename metadata, and videos. `langmd.mix` differences were confined to `audiomd.mix`, `cameomd.mix`, credits, localized video, `ra2md.csf`, and local filename metadata; common palette, keyboard, mission-packet, and other video entries remained identical where present.

This evidence is sufficient for RC3 and RC4: the archive variants differ in localization and presentation content, not in project gameplay rules or order semantics.

## Runtime loading trace

`mods/ra2/mod.yaml` mounts the imported archives and their nested MIX packages as content sources for sprites, voxels, palettes, terrain artwork, audio, text, and video. The same manifest loads deterministic actor rules and weapons only from project-owned `ra2|rules/**/*.yaml` and `ra2|weapons/**/*.yaml`. Map gameplay state is supplied by the selected map and validated independently by map UID.

`packaging/nukehour-compatibility-inputs.txt` feeds `nukehour-core-sha256` from the engine version, order serialization, synchronization primitives, RA2 rules, and RA2 weapons. No imported MIX archive or entry appears in that deterministic input set, and no `language|` or `langmd|` path appears in the rules or weapons graph.

Classification based on the traced loaders:

| Input | Classification | Compatibility mechanism |
| --- | --- | --- |
| Order schema/serialization and sync primitives | Gameplay-critical | protocol + engine/core compatibility |
| RA2 rules and weapons YAML | Gameplay-critical | `nukehour-core-sha256` |
| Selected map | Gameplay-critical | map UID/readiness |
| Required `ra2.mix` and `language.mix` availability | Startup/resource capability | required-capability fingerprint |
| Retail sprite, voxel, palette, terrain-image, cameo, font, audio, localized text, and video bytes | Presentation-only | excluded from gameplay fingerprint |
| Optional `ra2md.mix` and `langmd.mix` availability/bytes | Optional presentation capability | excluded from gameplay fingerprint |

## Previous algorithm

The Phase 1 algorithm sorted all four archive names and hashed role, presence, byte length, and full SHA-256 for each present archive. It conflated `RESOURCE_IDENTITY` with `GAMEPLAY_COMPATIBILITY`. Any localized text, audio, video, font, artwork, container layout, or optional-package difference changed the contract.

## Final algorithm

The Phase 1.1 algorithm:

1. validates all configured logical names and rejects duplicates or an oversized manifest;
2. sorts required logical package names ordinally;
3. opens each required package through the already-mounted OpenRA virtual filesystem and fails if any is missing;
4. hashes a V2 domain marker, required package count, each canonical name, and its confirmed-present marker;
5. excludes file bytes, paths, timestamps, import order, language choice, and optional presentation packages;
6. combines that digest with engine compatibility, orders protocol, mod id, `nukehour-core-sha256`, and the advanced resource capability in the unchanged RuntimeContract wire shape.

Old `ra2-required-v1` clients are rejected because their capability and digest cannot equal `ra2-presentation-capability-v2`. Actual gameplay changes are still rejected by engine/core/protocol/mod/map checks. A missing required retail capability still fails contract construction.

## Future rule

If a future feature loads deterministic simulation data from imported archives, advance the resource capability and hash an explicit normalized semantic manifest of only those loaded gameplay inputs. Do not return to whole-file hashing.
