# NUKE HOUR Multiplayer Phase 1.1 Implementation Plan

1. Preserve both repositories' existing dirty/untracked user files and record repository, branch, and starting commits.
2. Capture device availability without recording unique device identifiers in project artifacts. If no real Android ARM64 device is available, create the required operator checklist and keep final status blocked.
3. Inventory and deduplicate local real `ra2.mix`, `language.mix`, `ra2md.mix`, and `langmd.mix` samples by size and SHA-256 without copying or committing proprietary bytes.
4. Inspect archive entry names and digests with OpenRA's own MIX parser. Trace the mounted package graph and repository compatibility inputs. Write an evidence-only resource audit that distinguishes presentation identity from deterministic gameplay inputs.
5. Add RC1-RC9 tests first. Confirm RC3/RC4 fail under whole-file hashing and all existing rejection tests remain safe.
6. Change the resource fingerprint to canonical required-capability presence, exclude optional/presentation bytes, and advance the RA2 capability id. Keep the handshake wire schema unchanged.
7. Update protocol documentation, resource-audit documentation, and the Android physical-device checklist. Mirror shared source/tests/config/docs to the mobile repository.
8. Regenerate and verify each repository's engine patch delivery artifacts and any generated compatibility/version projections required by the established build workflow.
9. Run focused RC tests, direct TCP tests, parity checks, full NUnit/Python baselines, and available macOS/iOS/Android builds. Compare failures against the documented baseline and reject new failures.
10. Execute only physical-device matrix cells that can be proven in the current environment. Never substitute simulator, emulator, loopback, or inferred output for a missing real-device result.
11. Request an independent code review, resolve all Critical/Important findings, and commit logical changes separately in both repositories after showing the complete staged file list.
12. Publish the exact Phase 1.1 closure report. If Android remains unavailable, use `BLOCKED_ON_ANDROID_DEVICE_VALIDATION`; stop without beginning Dedicated Server Phase 2.
