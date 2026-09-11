# NUKE HOUR Open Source and TestFlight Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish an audited source-only snapshot of the latest NUKE HOUR code and upload the latest public-clean iOS build to TestFlight.

**Architecture:** Treat source publication and binary distribution as separate release outputs. The public Git repository receives an allowlisted working-tree snapshot without Git history, retail game files, user imports, signing material, logs, or build products; TestFlight receives a separately built and audited PublicClean IPA signed for App Store distribution.

**Tech Stack:** Git/GitHub CLI, .NET 8 iOS workload, Xcode signing tools, Apple App Store Connect/TestFlight, Python public-bundle audit scripts.

---

### Task 1: Audit the publication boundary

**Files:**
- Inspect: `.gitignore`
- Inspect: `LICENSE`
- Inspect: `docs/legal/public-asset-sbom.json`
- Inspect: `packaging/public-content-manifest.json`
- Create: `artifacts/release-audit/open-source-file-list.txt`

- [ ] Inventory tracked and required untracked source files.
- [ ] Reject game archives, maps, user imports, signing material, credentials, build products, logs, and files larger than 50 MB.
- [ ] Record the final source allowlist and run secret-pattern and extension audits against it.

### Task 2: Publish the clean source snapshot

**Files:**
- Create: a fresh staging checkout under `/Users/kevin/Projects/NUKEHOUR-PublicRelease`
- Preserve: `LICENSE`, `README.md`, attribution, source, tests, scripts, and reconstructable engine patches

- [ ] Export the audited working-tree source snapshot without the existing repository history.
- [ ] Initialize a fresh Git repository on branch `main` and verify the complete staged file list.
- [ ] Run source tests and publication audits.
- [ ] Commit the verified snapshot and push it to `kveinjay-google/NUKEHOUR` without force-pushing.

### Task 3: Produce an App Store-distribution PublicClean IPA

**Files:**
- Inspect: `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Inspect: `ios/OpenRA.iOS/Info.plist`
- Use: `ios/scripts/build-public-clean.sh`
- Output: `artifacts/public-ios/NUKE HOUR-PublicClean.ipa`

- [ ] Verify the current marketing version/build number and exact App Store bundle identifier.
- [ ] Select a valid Apple Distribution identity and App Store provisioning profile.
- [ ] Build the PublicClean configuration for `ios-arm64`.
- [ ] Audit the `.app` and `.ipa` for forbidden retail content, user data, debug artifacts, and invalid signing.

### Task 4: Upload and verify TestFlight delivery

**Files:**
- Input: `artifacts/public-ios/NUKE HOUR-PublicClean.ipa`

- [ ] Upload the audited IPA to the existing App Store Connect app.
- [ ] Verify that App Store Connect accepts the binary and reports the uploaded build number.
- [ ] Record the GitHub URL, commit, IPA checksum, bundle ID, version/build, signing team, upload result, and any remaining Apple-side processing state.
