# NUKEHOUR Multiplayer Phase 1 Implementation Plan

1. Record both repository baselines and preserve unrelated untracked files.
2. Add failing protocol/profile/contract/rejection/versioning tests in the shared OpenRA test suite.
3. Implement canonical runtime profile and runtime contract identities plus the shared compatibility validator.
4. Wire the required fields into server requests, client responses, fake/replay handshakes, and bidirectional validation.
5. Add stable rejection codes, localized UI messages, bounded debug logging, and map mismatch mapping.
6. Add protocol documentation and cross-repository parity checks.
7. Deliver ordered engine patches in both repositories and verify fresh replay.
8. Run focused unit tests, full relevant test suites, serialization/localhost TCP tests, macOS build, iOS build, and Android build.
9. Execute any physically available LAN/device matrix rows; mark unavailable rows `NOT DEVICE VERIFIED`.
10. Review the final diff, commit logically in both repositories, and produce the required Phase 1 report without starting Phase 2.
