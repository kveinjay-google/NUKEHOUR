# Private Registered-Device Early IPA Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the earliest safely installable Private Development Test IPA for registered iPhone/iPad devices.

**Architecture:** Keep the existing descriptor-bound generation store as the only active-state authority. Add a narrow Base-only Files-to-staging-to-generation vertical slice, then package the already signed app without rewriting its payload and verify the packaged copy before device use.

**Tech Stack:** .NET 8 iOS, C#, Apple codesign/provisioning, `ditto`, Python package checks, NUnit.

---

### Task 1: Freeze the durable core and its focused evidence

**Files:**
- Modify: `packaging/ios-retail-task7-deferred-engine-tests.sha256`
- Test: `engine/OpenRA.Test/RetailGenerationStoreTest.cs`
- Test: `engine/OpenRA.Test/RetailImportDurabilityTest.cs`
- Test: `engine/OpenRA.Test/RetailImportRecoveryProcessTest.cs`

- [ ] Run the existing focused Task 7 tests and real crash harness.
- [ ] Update only the recorded SHA-256 rows for deferred tests that changed during the durable-core repair.
- [ ] Verify that an active pointer is untouched after any rejected pre-journal validation.

### Task 2: Base-only local staging slice

**Files:**
- Modify: `OpenRA.Mods.RA2/Content/RetailContentImporter.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportCoordinatorCore.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportSpacePlanner.cs`
- Test: `engine/OpenRA.Test/RetailContentImporterTransactionTest.cs`

- [ ] Add a failing test that a single sealed Base directory failure leaves the active pointer unchanged.
- [ ] Implement only bounded staging, free-space reserve, hash/length seal, and transfer to `RetailGenerationStore.PrepareAsync`.
- [ ] Reject Yuri, music, multiple directory selections, and all remote sources before staging.
- [ ] Run the transaction test plus durable-core focused tests.

### Task 3: Base map conversion and read-only startup snapshot

**Files:**
- Modify: `OpenRA.Mods.RA2/Content/Maps/Ra2MapConversionService.cs`
- Modify: `OpenRA.Mods.RA2/Content/Maps/Ra2MapImportModels.cs`
- Modify: `ios/OpenRA.iOS/IosContentImportCoordinator.cs`
- Test: `engine/OpenRA.Test/Ra2MapConversionServiceTest.cs`

- [ ] Add a failing test for one Base local map that exceeds its envelope limit before parser allocation.
- [ ] Implement Base `.map` conversion under the existing deadline/envelope policy and retain a verified read-only snapshot.
- [ ] Require at least one valid two-player Base map before Ready; keep preview placeholder-only.
- [ ] Run conversion and startup snapshot tests.

### Task 4: Files onboarding and startup gate

**Files:**
- Modify: `ios/OpenRA.iOS/IosRetailContentPicker.cs`
- Modify: `ios/OpenRA.iOS/IosContentImportCoordinator.cs`
- Modify: `ios/OpenRA.iOS/ViewController.cs`
- Test: `engine/OpenRA.Test/RetailContentImporterTransactionTest.cs`

- [ ] Add a failing test for denied/cancelled security-scoped access that leaves active content unchanged.
- [ ] Bind `StartAccessingSecurityScopedResource`, `NSFileCoordinator`, and paired release to the single-directory import request.
- [ ] Show only import, retry, and Base-ready states; do not surface deferred workflows.
- [ ] Run the focused iOS-independent tests and an iOS simulator smoke build.

### Task 5: Signed IPA package and exact-payload verification

**Files:**
- Create: `ios/scripts/build-private-registered-device-ipa.sh`
- Create: `packaging/private_ipa_audit.py`
- Test: `packaging/tests/test_private_ipa_audit.py`

- [ ] Add failing archive tests for traversal, duplicate payload member, symlink, more than one app, and post-sign payload mutation.
- [ ] Build `ios-arm64` Release with the existing Apple Development identity and a Development provisioning profile that contains the target device.
- [ ] Package only `Payload/OpenRA.iOS.app` using `ditto`; reopen the IPA, verify its member topology and run `codesign --verify --deep --strict` against the extracted app.
- [ ] Write SHA-256 of the final IPA and install only the extracted app from that same IPA.

### Task 6: Registered-device acceptance

**Files:**
- Create: `artifacts/private-ios/README.md` (generated acceptance notes, never credentials)

- [ ] Verify `security find-identity` reports the chosen Apple Development certificate and that the embedded Development profile includes the test device UDID.
- [ ] Install the exact audited IPA on a registered device.
- [ ] Launch, import the Base directory through Files, cold restart, and start one two-player Base game.
- [ ] If any acceptance step fails, fix the narrow failed vertical-slice boundary and rebuild/re-audit a new IPA; never patch a signed app.
