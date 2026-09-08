# Private Registered-Device Early IPA Design

## Goal

Produce a privately signed **Private Development Test** IPA for registered iPhone/iPad devices as early as possible, then validate the exact signed payload on a physical device. This is not a public, Ad Hoc distribution, App Store, or TestFlight release.

## Scope

The first candidate supports one local, already-extracted Base retail-content directory selected through Files. The application supplies its independently maintained compatibility layer and runtime mount definitions; the player supplies only legally owned game assets and is never asked to locate or import a separate rules or compatibility pack. It persists only a fully written, hash-verified generation under the fixed Application Support root, then mounts the selected active catalog read-only. At least one converted Base two-player map is required before launch.

Yuri, music, multiple selection, Manage Content UI, seven-day pending UX, automatic GC, legacy migration, remote maps/search/download, PublicClean, public-source/legal evidence, and exhaustive release evidence are deferred. Old plan documents remain historical references and are not modified.

## Safety contract

- New generations write below the fixed private root using no-follow descriptor operations; symlinks and special files are rejected.
- Every imported file is bounded before high allocation, copied with a length/hash check, fsynced, validated, and made active only through an atomic pointer switch. The prior active generation remains recoverable.
- Files-provider access is security-scoped and coordinated, with every scope and coordinator release paired on all paths.
- MIX/IDX/BAG/ZIP/map parsing has byte, entry, depth, expansion-ratio, path and deadline limits before allocation-heavy parsing.
- Cold start authenticates the active catalog, receipt and runtime contract before exposing any mount. The active catalog is read-only.
- Release AOT/linker device build and bundle smoke must succeed. The bundle contains neither retail input data, private input paths, nor credentials.
- IPA creation packages only `Payload/<App>.app`, rejects archive traversal/duplicates/symlinks, then reopens the same IPA payload and runs strict `codesign` verification before installation.

## Delivery path

1. Preserve the completed Task 7 durable generation core and close its consistency checks.
2. Add the minimum one-directory staging/seal path, keeping failed imports from altering active state.
3. Add Base-only local map conversion and verified read-only snapshot mounting; remote entry points remain unreachable.
4. Wire Files import and a minimal onboarding/startup gate.
5. Build a signed device Release app with the existing Apple Development identity and a device-containing Development provisioning profile, create an IPA without post-sign mutation, verify it, install it, and perform Base import plus one two-player launch using that exact IPA.

## Acceptance states

`blocked candidate` is an unsigned or unprovisioned app bundle and is never described as installable. `candidate` is a signed **Private Development Test** IPA that passed archive and codesign checks but has not completed device acceptance. `accepted` is the same signed IPA payload installed and launched on a device registered in its Development profile, with a Base import and two-player game start observed.
