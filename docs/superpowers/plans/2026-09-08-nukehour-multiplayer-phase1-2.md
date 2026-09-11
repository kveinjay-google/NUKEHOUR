# NUKEHOUR Multiplayer Phase 1.2 Implementation Plan

## Scope

Close the currently available Apple physical-device matrix without changing the
Phase 1 handshake, orders protocol, or RuntimeContract semantics.  The test pair
is one physical iPad and the local Apple Silicon Mac on the same LAN.

## Test infrastructure

1. Make the opt-in multiplayer smoke identity cross-platform by taking its
   256-bit identity from the active `RuntimeContract` instead of hashing an
   iOS-specific application bundle.  Persist the canonical RuntimeProfile,
   RuntimeContract, core compatibility, and resource capability in each fresh
   journal.
2. Extend the opt-in smoke with a gameplay closure mode.  Before start the host
   selects heavy starting units and enables the controlled developer option.
   After spawn, each real peer selects a local combat unit and issues synchronized
   movement, cash/resource, production, completed-production/placement, and attack
   orders.  PASS requires observable effects and a sustained synchronized world
   tick target with no disconnect or out-of-sync state.
3. Add a macOS/iOS runner that launches exactly one local macOS process and one
   physical iOS app, drives both Direct `IP:1234` directions, gathers fresh
   journals and engine logs, and sanitizes persistent device identifiers from
   committed evidence.
4. Run one temporary, controlled macOS gameplay-core mismatch fixture and require
   a compatibility rejection before lobby/gameplay.  Do not alter imported retail
   resources.

## Acceptance and reporting

- Run iOS Server to macOS Client and macOS Server to iOS Client on physical
  hardware through Direct IP port 1234.
- Require both journals to prove handshake identity, lobby/map/ready/start/spawn,
  all required gameplay actions, sustained synchronization, and clean connection.
- Attempt Chinese/English presentation compatibility when independently sourced
  legal resource sets are available.
- Record macOS LAN discovery as not implemented/not verified if the desktop
  platform still has no `INearbyGameService`; do not add one in this phase.
- Phase 1 remains PARTIAL with
  `BLOCKED_ONLY_ON_ANDROID_PHYSICAL_MATRIX`.  Stop without starting Phase 2.
