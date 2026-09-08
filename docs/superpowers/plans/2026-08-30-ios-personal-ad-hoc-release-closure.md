# iOS Personal Ad Hoc Release Closure Implementation Plan

> **Required execution order:** Start only after Tasks 0–17 in `2026-08-29-ios-retail-content-import-hardening.md` finish honestly. A blocked Task 16 legal-retail device acceptance remains blocked here.

**Goal:** Produce a legally reviewed, independently installable Personal **Ad Hoc** IPA for registered iPhone/iPad devices from one clean reproducible commit. This is not PublicClean, App Store, TestFlight, or store publication. The approved About/source design is narrowed here to PersonalDistribution; its PublicClean distribution gate remains deferred and unsatisfied.

**Architecture:** A fresh official OpenRA replay through strict patch `0028` feeds a mutually exclusive `PersonalDistribution` Release build. The Apple Distribution-signed `.app` passes legal/source, importer, bundle, signature, profile, and entitlement gates before the repository's single safe IPA module packages it. That module safely extracts the final IPA; the original and extracted app must have identical complete path/type/mode/size/SHA-256 manifests, followed by a second `codesign` verification. CoreDevice installs that exact IPA-derived app, launches it, and reruns legal-retail import plus a real Base skirmish. PublicClean remains an unsigned diagnostic: its historical 580 errors are never called passing, and only zero new `(code,path)` delta is required.

**Tech stack:** C#/.NET 8 iOS AOT, MSBuild, NUnit, Python 3 `unittest`, POSIX shell, Apple `codesign`/`security`, CoreDevice `xcrun devicectl`, deterministic OpenRA patch replay.

**Selected package route:** Use the signed `.app` emitted by the .NET iOS Release build and create an IPA directly with the safe repository packager. Xcode Archive/Export is not technically required for this registered-device goal because post-package complete-manifest equality, second signature verification, profile/entitlement checks, and real-device installation prove the payload. If any gate fails, stop; never fall back to `ditto`, `zip`, `unzip`, `extractall`, post-sign mutation, or an unreviewed Archive/Export route. Plan and review Archive/Export separately if it becomes necessary.

**Legal boundary:** These are technical provenance/disclosure gates, not legal advice or a legal conclusion. The five documents, dependency inventory, source scope, and distribution terms require final user/qualified review before the signed run.

---

## Exact artifact flow and independent gates

| Stage | Artifact/state | Mandatory gate |
| --- | --- | --- |
| Source | clean committed tree after Task 17 | no retail data, credentials, evidence, outputs, or unrelated large files |
| Engine | fresh official replay `0001`…`0028` | strict replay; only the already-pinned `0007` diagnostic; workspace byte equality |
| Configuration | `PersonalDistribution=true`, `PublicClean=false`, Release, `ios-arm64` | fail-fast mutual exclusion |
| Legal/source | exactly `AUTHORS`, `COPYING`, `THIRD_PARTY_LICENSES`, `NUKEHOUR_MODIFICATIONS`, `SOURCE_INFORMATION` | exact names/bytes, closed dependency provenance, immutable public source URL |
| Signed app | Apple Distribution-signed Personal `.app` | Ad Hoc profile, registered device membership, authorized entitlements, `get-task-allow` disabled |
| IPA | safe direct package of that exact app | central parser/packager only |
| Extracted app | safely extracted `Payload/*.app` | full manifest equality and second strict `codesign` verification |
| Device | install IPA-derived app, then launch | CoreDevice succeeds; identifiers stay out of public evidence |
| Runtime | legal retail import and two-player Base skirmish | Task 16 device gate rerun against this exact payload |
| Delivery | IPA, detached SHA-256, redacted evidence | every Personal gate true; private outputs remain outside Git |

The IPA is the deliverable. Its extracted app is only the verification/install intermediary. `devicectl device install app` accepts `.app`, so install the app safely extracted from the final IPA; complete equality plus the second signature check proves it is the IPA payload.

PublicClean has a distinct branch:

```text
clean source -> PublicClean=true + PersonalDistribution=false
             -> unsigned simulator diagnostic
             -> historical 580-error report preserved; new (code,path) delta = 0
             -> no PublicClean IPA and no PublicClean release claim
```

The existing 580 errors do not block Personal when the delta is zero. A nonzero delta does block this work because it is a regression; the historical errors themselves are never converted into `PASS` and never replace Personal's independent gates.

## External inputs and honest blocked states

Code, tests, the safe parser, About UI, inventory tooling, unsigned simulator builds, and an unsigned audited candidate do not need signing material. The final run alone requires these names/inputs:

- `IOS_CODESIGN_KEY`: eligible Apple Distribution identity with its private key;
- `IOS_AD_HOC_PROVISION_UUID`: matching valid Ad Hoc profile;
- `IOS_DEVICE_UDID`: unlocked/trusted/Developer Mode registered target;
- `IOS_SOURCE_URL`: immutable public GitHub commit/tag containing corresponding source/build material, created only as a separately and directly authorized external source-publication prerequisite;
- `IOS_LEGAL_REVIEW_CONFIRMED`: exact literal `true` only after the final human or qualified legal/notice review;
- `IOS_LEGAL_RETAIL_FIXTURE_CONFIRMED`: exact literal `true` only when a legally owned extracted installation is available through Files for the final acceptance;
- `IOS_COLD_RESET_AUTHORIZED`: exact literal `true` only after the operator separately authorizes the Task 16 cold device/container reset;
- `OPENRA_OFFICIAL_ZIP`: the already-pinned official OpenRA archive;
- a legally owned extracted retail installation reachable through Files;
- explicit authorization for any required cold device/container reset.

Never print, commit, or place in redacted evidence any signing value, certificate subject, team/profile/device identifier, private retail path/name/hash, or account name. Scripts may read the named variables in memory and keep private diagnostics only in the unique `/private/tmp` run directory.

If a final input is unavailable, implementation may be called an **unsigned/audited candidate** only. Emit a redacted `blocked` record; never call it signed, Ad Hoc, installable, independently publishable, releasable, or complete. This plan verifies a supplied public source URL but does not create a GitHub repository, add a remote, push, tag, upload, or publish. Publishing the exact final corresponding-source commit is an external prerequisite requiring separate direct user authorization; implementation approval is not that authorization. Until an authorized publication exists and verifies, emit `SourcePublicationUnavailable` and stop. That narrowly authorized source publication would not authorize an app/IPA release, retail redistribution, PublicClean, or any other GitHub action.

## Git, staging, and evidence protocol

- Begin every task clean. Stop if unrelated changes appear.
- Each **Files** list is the exact staging boundary. Store the byte-sorted expected list under `/private/tmp`, stage literal paths only, display `git diff --cached --name-only --no-renames`, compare lists, and run `git diff --cached --check` before committing.
- Never stage retail data, fixtures/paths/hashes, signing/profile material, `_CodeSignature`, decoded profiles, evidence, CoreDevice output, `bin`, `obj`, caches, artifacts, symlinks, or files over 50 MB.
- Run RED before production changes and record the exact failure. An already-green characterization is not RED.
- Keep real build/sign/install evidence in `/private/tmp/nuke-hour-personal-adhoc-*`; the closed redacted JSON also remains untracked.
- This plan deliberately contains no invented `0028` count. Task 18 freezes the reviewed byte-sorted list after Tasks 0–17; later tasks accept only that output.

---

### Task 18: Freeze the post-Task-17 boundary and reserve `0028`

**Files:**

- Create: `docs/testing/IOS_PERSONAL_AD_HOC_RELEASE_BOUNDARY.md`
- Modify: `packaging/engine_workspace.py`
- Modify: `packaging/tests/test_engine_workspace.py`
- Inspect only: `docs/superpowers/specs/2026-08-28-ios-about-source-compliance-design.md`
- Inspect only: `docs/superpowers/plans/2026-08-29-ios-retail-content-import-hardening.md`
- Inspect only: `packaging/ios-personal-official-engine-assets.json`
- Inspect only: `engine-patches/0020-*.patch` through `engine-patches/0027-*.patch`
- Inspect only: candidate engine/iOS/About/legal/packaging paths found below
- Create outside Git: `/private/tmp/nuke-hour-personal-adhoc-task18.*`

- [ ] **Step 1: Prove the predecessor and inventory read-only**

```bash
set -euo pipefail
test -z "$(git status --short)"
test "$(git ls-files 'engine-patches/002[0-7]-*.patch' | wc -l | tr -d ' ')" -eq 8
test -z "$(git ls-files 'engine-patches/0028-*.patch')"
python3 -m unittest packaging.tests.test_engine_workspace -v
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
TASK18_STAGE="$(mktemp -d /private/tmp/nuke-hour-personal-adhoc-task18.XXXXXX)"
TASK18_REPLAY="$TASK18_STAGE/replay"
python3 packaging/engine_workspace.py materialize \
  --repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 27 --workspace "$TASK18_REPLAY"
rg -n 'About|SOURCE_INFORMATION|THIRD_PARTY|Open.*URL|Open.*Url|Platform.IsIOS|TabCount|RESET_BUTTON' \
  engine/OpenRA.Game engine/OpenRA.Mods.Common engine/OpenRA.Test \
  engine/mods/common mods/ra2 ios packaging docs/legal
rg -n 'extractall|ZipFile|ditto -c|-p:PublicClean|-p:PersonalDistribution|Codesign' \
  packaging ios/scripts ios/OpenRA.iOS/OpenRA.iOS.csproj
git ls-files | LC_ALL=C sort > "$TASK18_STAGE/tracked.paths"
```

Require the eight patches, unused `0028`, successful fresh replay, unchanged worktree, and the honest Task 15 provenance boundary (official+`0001`…`0026` with separately recorded zero-resource `0027`). That manifest is not final authority for `0028`; Task 20 must supersede it. If `0028` exists or Task 17 is blocked, stop and amend numbering/scope before code.

- [ ] **Step 2: Independently freeze real path sets**

Create `docs/testing/IOS_PERSONAL_AD_HOC_RELEASE_BOUNDARY.md` with:

- exact clean predecessor commit and verified replay end `0027`;
- byte-sorted `engine-allowlist` fenced block with every and only engine-relative path About/source work may create/modify/delete;
- byte-sorted `task19-outer-implementation-paths`, `task20-outer-implementation-paths`, and `task21-outer-implementation-paths` fenced blocks containing every and only the exact iOS/mods/packaging/legal/test paths staged by that task;
- `delivery-paths` containing only `engine-patches/0028-ios-about-source-compliance.patch`, `engine-patches/README.md`, and `packaging/tests/test_verify_engine_patch_delivery.py`;
- explicit no-wildcard/no-inferred-count rules and a stop/re-review rule for any added path;
- `Review: approved` only after a reviewer independent of the implementation author compares every path with the post-`0027` replay and approved About design.

If a safe exact list cannot be frozen, mark Task 18 blocked; do not start Tasks 19–22.

- [ ] **Step 3: Write RED mapping test**

Extend `test_engine_workspace.py` to parse the boundary's exact `engine-allowlist`, require canonical LF/UTF-8 engine-relative, byte-sorted, unique, nonempty records, and compare it exactly with `ALLOWLISTS["0028"]`. Preserve `0020`…`0027` byte-for-byte and remove the existing conditional count fallback rather than extending it.

```bash
set -euo pipefail
TASK18_RED="$(mktemp -d /private/tmp/nuke-hour-personal-task18-red.XXXXXX)"
set +e
python3 -m unittest \
  packaging.tests.test_engine_workspace.EngineWorkspaceTest.test_allowlist_mappings_counts_and_byte_order_are_exact \
  -v > "$TASK18_RED/engine-workspace.log" 2>&1
TASK18_RED_RC=$?
set -e
test "$TASK18_RED_RC" -ne 0
rg -n 'FAILED|AssertionError|0028' "$TASK18_RED/engine-workspace.log"
```

Expected RED: reviewed `0028` exists in the boundary but not the mapping.

- [ ] **Step 4: Add only reviewed mapping and run GREEN**

```bash
set -euo pipefail
TASK18_GREEN="$(mktemp -d /private/tmp/nuke-hour-personal-task18-green.XXXXXX)"
TASK18_ALLOWLIST="$TASK18_GREEN/0028.allowlist"
python3 packaging/engine_workspace.py allowlist \
  --patch-id 0028 --output "$TASK18_ALLOWLIST"
LC_ALL=C sort -c -u "$TASK18_ALLOWLIST"
python3 -m unittest packaging.tests.test_engine_workspace -v
git diff --check
```

The test derives the count from the reviewed records; no literal count is added to this plan or a fallback expression.

- [ ] **Step 5: Stage exactly three paths and commit**

```bash
set -euo pipefail
TASK18_COMMIT="$(mktemp -d /private/tmp/nuke-hour-personal-task18-commit.XXXXXX)"
printf '%s\n' \
  docs/testing/IOS_PERSONAL_AD_HOC_RELEASE_BOUNDARY.md \
  packaging/engine_workspace.py \
  packaging/tests/test_engine_workspace.py | LC_ALL=C sort \
  > "$TASK18_COMMIT/expected.paths"
git add -- docs/testing/IOS_PERSONAL_AD_HOC_RELEASE_BOUNDARY.md \
  packaging/engine_workspace.py packaging/tests/test_engine_workspace.py
git diff --cached --name-only --no-renames | LC_ALL=C sort \
  > "$TASK18_COMMIT/staged.paths"
cat "$TASK18_COMMIT/staged.paths"
cmp "$TASK18_COMMIT/expected.paths" "$TASK18_COMMIT/staged.paths"
git diff --cached --check
git commit -m "Freeze Personal Ad Hoc release boundary"
test -z "$(git status --short)"
```

---

### Task 19: Make distribution modes exclusive and centralize safe IPA handling

**Files:**

- Create: `packaging/ios_ipa_archive.py`
- Create: `packaging/tests/test_ios_ipa_archive.py`
- Create: `packaging/tests/test_ios_personal_distribution_policy.py`
- Modify: `packaging/audit_ios_retail_import_bundle.py`
- Modify: `packaging/tests/test_ios_retail_import_bundle.py`
- Modify: `packaging/public_bundle_audit.py`
- Modify: `packaging/tests/test_public_bundle_audit.py`
- Modify: `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Modify: `ios/scripts/build-public-clean.sh`
- Modify: `docs/testing/IOS_RELEASE_REGRESSION_GATES.md`

All paths must already be in Task 18's reviewed `task19-outer-implementation-paths` block. Stop for a boundary review otherwise.

- [ ] **Step 1: Write RED mode and malicious-IPA tests**

`test_ios_personal_distribution_policy.py` requires:

- `PersonalDistribution` defaults false;
- `PersonalDistribution=true` with `PublicClean=true` fails before build/package work;
- PersonalDistribution requires Release, `ios-arm64`, signing enabled, and never accepts a development-signing shortcut;
- normal Personal development/unsigned simulator builds cannot emit a release IPA or passing release report;
- PublicClean's legacy script becomes diagnostic-only, creates no IPA, and treats only a zero historical `(code,path)` delta as successful diagnostic execution without naming the 580-error build `PASS`.

`test_ios_ipa_archive.py` covers one canonical `Payload/*.app`, complete round trip, deterministic entry order, regular files/directories/modes, empty files, and bounded streamed files. Reject:

- absolute, `..`, `.`, empty-component, backslash, NUL, non-canonical, drive-like, Unicode-normalization, or case-fold-colliding paths;
- duplicate raw/normalized paths, multiple/nested apps, entries outside the sole app subtree, or ambiguous file/directory prefixes;
- symlink, hard-link-like, device, socket, FIFO, unknown/special mode, encryption, unsupported compression, bad CRC, truncation, or overlapping ZIP data;
- over 50,000 entries, one regular entry over 2 GiB, aggregate uncompressed content over 4 GiB, compression ratio over 200:1, or streamed bytes beyond declared/aggregate limits;
- a symlinked destination ancestor, nonempty target, mode/hash mismatch, or usable partial output after failure.

Source-scan production `packaging/` and `ios/scripts/`: reject `extractall`, `unpack_archive`, shell `unzip`, or an independent IPA `ZipFile` traversal outside `ios_ipa_archive.py` and its tests.

```bash
set -euo pipefail
python3 -m unittest \
  packaging.tests.test_ios_ipa_archive \
  packaging.tests.test_ios_personal_distribution_policy \
  packaging.tests.test_ios_retail_import_bundle \
  packaging.tests.test_public_bundle_audit -v
```

Expected RED: missing module/property, current unsafe `extractall`, and legacy PublicClean IPA creation.

- [ ] **Step 2: Implement the sole IPA parser/packager**

`packaging/ios_ipa_archive.py` is the only production implementation allowed to enumerate, create, or extract an IPA:

```text
create --app APP --output IPA
inspect --ipa IPA --report REPORT
extract --ipa IPA --destination EMPTY_DIRECTORY --report REPORT
manifest --app APP --output MANIFEST
compare-manifests --expected ORIGINAL --actual EXTRACTED
```

It must use `lstat`/no-follow traversal; accept only regular files/directories; write byte-sorted explicit canonical `Payload/<app>.app/...` records with Unix modes; stream data/hashes in bounded chunks; manually create each destination with exclusive/no-follow opens; revalidate ancestors; set modes, fsync, and atomically publish only after CRC/size/hash verification; and emit complete manifest records `(relativePath, kind, mode, size, sha256)` for files and `(relativePath, kind, mode)` for directories. It contains no `extractall`, including trusted paths.

Both `audit_ios_retail_import_bundle.py` and `public_bundle_audit.py` import this API and contain no ZIP walk. IPA importer audit retains mandatory explicit same-configuration/commit `--reference-app`, now comparing the entire manifest rather than a critical subset.

- [ ] **Step 3: Implement the distribution contract and retire legacy PublicClean packaging**

In the iOS project:

- `PersonalDistribution` and `PublicClean` default false and are mutually exclusive;
- PersonalDistribution requires Release, `ios-arm64`, signing enabled, immutable source URL, and Task 20's five documents;
- PublicClean cannot inherit a Personal-only wildcard;
- a signed normal Personal development build remains non-distributable;
- common pre-sign importer/app audits run, but no target calls the app Ad Hoc before Task 21's post-sign audit.

Embed no signing values. Convert `build-public-clean.sh` to an unsigned simulator diagnostic: retain the known failed report, run `public_brand_delta_audit.py`, return diagnostic success only for delta zero, assert no PublicClean IPA, and never read signing variables, sign, package, or write an app/build `PASS`.

- [ ] **Step 4: Run GREEN and the source safety gate**

```bash
set -euo pipefail
python3 -m unittest \
  packaging.tests.test_ios_ipa_archive \
  packaging.tests.test_ios_personal_distribution_policy \
  packaging.tests.test_ios_retail_import_bundle \
  packaging.tests.test_public_bundle_audit -v
bash -n ios/scripts/build-public-clean.sh
test -z "$(rg -n 'extractall|unpack_archive|(^|[[:space:]])unzip([[:space:]]|$)' \
  packaging ios/scripts --glob '*.py' --glob '*.sh' \
  | rg -v 'test_ios_ipa_archive.py|forbidden|reject')"
git diff --check
```

- [ ] **Step 5: Stage the exact ten-file boundary and commit**

Display and compare the exact staged list with Task 18, then:

```bash
set -euo pipefail
git diff --cached --check
git commit -m "Harden iOS distribution packaging"
test -z "$(git status --short)"
```

---

### Task 20: Implement About/source compliance and deliver strict engine patch `0028`

**Outer files:**

- Create: `packaging/generate_ios_about_documents.py`
- Create: `packaging/ios-third-party-distribution-inventory.json`
- Create: `packaging/ios-reproducible-toolchain-inventory.json`
- Create: `packaging/tests/test_ios_about_documents.py`
- Modify: `packaging/ios-personal-official-engine-assets.json`
- Create: `docs/legal/ios/NUKEHOUR_MODIFICATIONS.template`
- Create: `docs/legal/ios/SOURCE_INFORMATION.template`
- Modify: `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Modify: `ios/OpenRA.iOS/AppDelegate.cs`
- Create: `ios/OpenRA.iOS/IosAboutSourceHost.cs`
- Verify or deterministically modify: `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs`
- Modify: `mods/ra2/chrome/settings.yaml`
- Modify: `mods/ra2/fluent/chrome.ftl`
- Modify: `mods/ra2/fluent/zh-CN/chrome.ftl`
- Modify: `packaging/audit_ios_retail_import_bundle.py`
- Modify: `packaging/tests/test_ios_retail_import_bundle.py`
- Modify: `packaging/public-content-manifest.json`
- Modify: `docs/legal/public-asset-sbom.json`
- Modify: `docs/testing/IOS_RELEASE_REGRESSION_GATES.md`

**Engine implementation files:**

- Create/modify/delete only exact engine-relative paths in the approved `engine-allowlist` block of `docs/testing/IOS_PERSONAL_AD_HOC_RELEASE_BOUNDARY.md`.
- Candidate settings/layout/chrome/Fluent/document/platform/test paths discovered before Task 18 are not authority; the reviewed block is authoritative and no fixed count is copied here.

**Delivery files:**

- Create: `engine-patches/0028-ios-about-source-compliance.patch`
- Modify: `engine-patches/README.md`
- Modify: `packaging/tests/test_verify_engine_patch_delivery.py`

Every outer/delivery/engine path must match Task 18 before editing. In particular, the 19 outer paths above must equal the reviewed `task20-outer-implementation-paths` block; 18 must have a real diff, while `GeneratedAotObjectRegistry.g.cs` is included only when deterministic generation changes its bytes. No path from Task 19 or Task 21 is staged here. The three delivery paths remain a separate commit; the superseding official-engine manifest belongs to the implementation commit because the final app audit consumes it.

- [ ] **Step 1: Write RED About, source, inventory, and bundle tests**

Require:

- iOS registers six settings tabs from actual panels; desktop remains five; invalid indexes fail; supported landscape iPhone/iPad safe-area matrices keep 48-point targets, containment, order, rotation/UI-scale idempotence, responsive actions, and scrolling;
- About is read-only, never dirty/restart, hides/disables reset only while active, and always permits Back;
- exact English/Chinese attribution and fixed modification date;
- a closed enum maps only to root bundle names `AUTHORS`, `COPYING`, `THIRD_PARTY_LICENSES`, `NUKEHOUR_MODIFICATIONS`, `SOURCE_INFORMATION`; YAML, Fluent, URL, and user input cannot become paths;
- `AUTHORS`/`COPYING` equal fresh official replay bytes; the other three are deterministic from versioned inputs plus explicit source commit/URL;
- missing/extra/renamed/duplicate/symlink/non-regular/traversal/stale/hash-mismatched documents fail UI policy and distribution audit;
- immutable source URL is HTTPS public GitHub pinned to the exact commit/tag; credentials, localhost, IP, fragment, moving branch, or unapproved path fails; opening uses iOS main-thread host handoff, with local `SOURCE_INFORMATION` fallback only for non-distribution development;
- shipped dependency graph is exactly covered, including .NET runtime, SDL2, SDL2-CS, FreeType, shipped NuGet assemblies, final native libraries, fonts, images, shaders, and attributed resources; stale/missing version/provenance/license/path fails;
- build-only tool/workload inputs are separate and not represented as redistributed components;
- app audit requires exactly five documents and expected bytes for Personal and PublicClean; PersonalDistribution additionally requires immutable public source;
- official-engine provenance is regenerated from the pinned official ZIP plus strict `0001`…`0027` and the exact reviewed `0028` candidate; the manifest records final patch end `0028`, candidate/allowlist digests, and every final bundle-eligible engine path/hash/size, so no `0028` resource may bypass or conflict with Task 15's earlier `0026` baseline plus zero-resource `0027` proof;
- PublicClean retains historical baseline with zero new `(code,path)` errors; wording/JSON that labels the 580-error build `PASS` fails.

```bash
set -euo pipefail
TASK20_RED="$(mktemp -d /private/tmp/nuke-hour-personal-task20-red.XXXXXX)"
set +e
python3 -m unittest \
  packaging.tests.test_ios_about_documents \
  packaging.tests.test_ios_retail_import_bundle \
  packaging.tests.test_public_bundle_audit \
  packaging.tests.test_ios_personal_distribution_policy -v \
  > "$TASK20_RED/python.log" 2>&1
TASK20_PYTHON_RED_RC=$?
set -e
test "$TASK20_PYTHON_RED_RC" -ne 0
rg -n 'FAILED|ERROR|ImportError|ModuleNotFoundError|AssertionError' \
  "$TASK20_RED/python.log"

set +e
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~IosSettingsLayoutTest|FullyQualifiedName~IosAbout|FullyQualifiedName~AotObjectRegistryCoverageTest' \
  -v minimal > "$TASK20_RED/csharp.log" 2>&1
TASK20_CSHARP_RED_RC=$?
set -e
test "$TASK20_CSHARP_RED_RC" -ne 0
rg -n 'Failed!|FAILED|error CS|not found|missing' "$TASK20_RED/csharp.log"

# Existing YAML validity is a characterization prerequisite, not RED evidence.
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Utility/OpenRA.Utility.csproj -m:1 \
  /nr:false -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
YAML_REPLAY_ROOT="$PWD"
(
  cd engine
  MOD_SEARCH_PATHS="./mods,$YAML_REPLAY_ROOT/mods" ENGINE_DIR=".." \
    env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet \
    bin/OpenRA.Utility.dll ra2 --check-yaml
)
```

Expected RED: missing generator/inventories/panel/policies/five-document contract and the stale Task 15 provenance manifest that cannot authenticate a resource-bearing `0028`.

- [ ] **Step 2: Implement five-document and inventory gates**

`generate_ios_about_documents.py` provides deterministic `generate`, `audit-app`, `audit-inventory`, and `verify-public-source` commands. It copies official `AUTHORS`/`COPYING` byte-for-byte; generates the other three with LF/UTF-8/stable order; takes exact clean commit and `IOS_SOURCE_URL` explicitly; allows the approved exact fallback only in development; compares final managed/native/resource bundle graph with the closed distribution inventory; and rejects dirty/unidentified source, unknown/stale entries, missing notices, locally guessed license provenance, stale alternatives, or nonimmutable/unverifiable source URL.

Extend Task 15's sole `official-provenance` implementation rather than creating a second asset parser. Its reviewed-candidate mode accepts exact baseline `--patch-end 27`, exact `--candidate-patch-id 0028`, exact `--candidate-patch`, and matching `--candidate-allowlist`; it freshly replays official+`0001`…`0027`, strictly applies the candidate, proves its headers equal the reviewed allowlist, enumerates the final eligible assets, and emits/checks one manifest whose effective patch end is `0028` and which binds the candidate SHA-256 plus allowlist digest. Its output must be byte-identical to direct strict `--patch-end 28` generation once that candidate exists at its checked-in name. Missing/extra/changed resources, a candidate/header mismatch, ignored-workspace bytes, or a manifest still ending at `0026`/`0027` fails. Task 15's historical manifest was honest for its own pre-`0028` milestone; Task 20 deliberately supersedes it for the final Personal artifact.

The inventory records facts/notices for review, not legal approval. Release docs say “not legal advice; final review required”; original upstream documents remain unchanged.

Wire generation before bundle assembly and audit before signing. Add exactly five root `BundleResource` logical names, no wildcard, and reject incremental stale alternatives. Change `GeneratedAotObjectRegistry.g.cs` only through the generator when coverage proves a justified entry.

- [ ] **Step 3: Implement reviewed engine/About paths and run GREEN**

Implement only within Task 18's engine allowlist. Source opening uses fixed build-owned metadata and iOS main-thread host; document/UI text cannot provide an external URL. Viewer is local plain text, read-only, enum-based, non-HTML, and fail-closed.

```bash
set -euo pipefail
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
TASK20_PROVENANCE="$(mktemp -d /private/tmp/nuke-hour-personal-provenance.XXXXXX)"
TASK20_ALLOWLIST="$TASK20_PROVENANCE/0028.allowlist"
TASK20_CANDIDATE="$TASK20_PROVENANCE/0028.patch"
python3 packaging/engine_workspace.py allowlist \
  --patch-id 0028 --output "$TASK20_ALLOWLIST"
LC_ALL=C sort -c -u "$TASK20_ALLOWLIST"
python3 packaging/engine_workspace.py candidate \
  --repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 27 --workspace engine --allowlist "$TASK20_ALLOWLIST" \
  --output "$TASK20_CANDIDATE"
python3 packaging/audit_ios_retail_import_bundle.py official-provenance \
  --repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 27 --candidate-patch-id 0028 \
  --candidate-patch "$TASK20_CANDIDATE" \
  --candidate-allowlist "$TASK20_ALLOWLIST" \
  --output packaging/ios-personal-official-engine-assets.json
python3 packaging/audit_ios_retail_import_bundle.py official-provenance \
  --repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 27 --candidate-patch-id 0028 \
  --candidate-patch "$TASK20_CANDIDATE" \
  --candidate-allowlist "$TASK20_ALLOWLIST" \
  --output packaging/ios-personal-official-engine-assets.json --check
python3 -m unittest \
  packaging.tests.test_ios_about_documents \
  packaging.tests.test_ios_retail_import_bundle \
  packaging.tests.test_public_bundle_audit \
  packaging.tests.test_ios_personal_distribution_policy -v
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~IosSettingsLayoutTest|FullyQualifiedName~IosAbout|FullyQualifiedName~AotObjectRegistryCoverageTest' \
  -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Utility/OpenRA.Utility.csproj -m:1 \
  /nr:false -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
YAML_REPLAY_ROOT="$PWD"
(
  cd engine
  MOD_SEARCH_PATHS="./mods,$YAML_REPLAY_ROOT/mods" ENGINE_DIR=".." \
    env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet \
    bin/OpenRA.Utility.dll ra2 --check-yaml
)
git diff --check
```

Generate `allowlist --patch-id 0028`, parse the actual engine diff, and require exact reviewed path equality. Run full C# and packaging suites before commit.

- [ ] **Step 4: Run unsigned Personal and PublicClean diagnostics**

```bash
set -euo pipefail
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
TASK20_STAGE="$(mktemp -d /private/tmp/nuke-hour-personal-adhoc-task20.XXXXXX)"
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet restore ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -p:RuntimeIdentifier=iossimulator-arm64 -m:1
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -c Release -f net8.0-ios -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:PublicClean=false -p:PersonalDistribution=false \
  -p:CodesignDisable=true --no-restore -m:1
PUBLIC_DIAGNOSTIC_DIR="$TASK20_STAGE/public-diagnostic" \
  bash ios/scripts/build-public-clean.sh
test -f "$TASK20_STAGE/public-diagnostic/public-clean-diagnostic.json"
test -z "$(find "$TASK20_STAGE" -type f -name '*.ipa' -print -quit)"
```

Unsigned Personal is only a candidate. PublicClean diagnostic may exit zero for correct delta execution, but JSON still says historical failure, never pass.

- [ ] **Step 5: Commit About/source implementation**

Generate the exact stage list from Task 18's `task20-outer-implementation-paths` and `engine-allowlist` blocks; omit the generated AOT registry only when byte-identical, and exclude the three delivery paths until Step 6. Display/compare it and run:

```bash
set -euo pipefail
git diff --cached --check
git commit -m "Add iOS About source compliance"
test -z "$(git status --short)"
```

- [ ] **Step 6: Generate, verify, and commit `0028`**

```bash
set -euo pipefail
: "${OPENRA_OFFICIAL_ZIP:?set the absolute path to the pinned official ZIP}"
test "$(shasum -a 256 "$OPENRA_OFFICIAL_ZIP" | awk '{print $1}')" = \
  ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a
PATCH_STAGE="$(mktemp -d /private/tmp/nuke-hour-engine-patch-0028.XXXXXX)"
ALLOWLIST="$PATCH_STAGE/0028.allowlist"
VERIFY_DIR="$PATCH_STAGE/verification"
PREDECESSOR_END=27
CANDIDATE=engine-patches/0028-ios-about-source-compliance.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0028 --output "$ALLOWLIST"
LC_ALL=C sort -c -u "$ALLOWLIST"
python3 packaging/engine_workspace.py candidate \
  --repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end "$PREDECESSOR_END" --workspace engine \
  --allowlist "$ALLOWLIST" --output "$CANDIDATE"
python3 packaging/verify_engine_patch_delivery.py \
  --repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --expected-zip-sha256 ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a \
  --predecessor-start 1 --predecessor-end "$PREDECESSOR_END" \
  --allowlist-file "$ALLOWLIST" --candidate "$CANDIDATE" \
  --workspace engine --output-dir "$VERIFY_DIR"
python3 packaging/audit_ios_retail_import_bundle.py official-provenance \
  --repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 27 --candidate-patch-id 0028 \
  --candidate-patch "$CANDIDATE" \
  --candidate-allowlist "$ALLOWLIST" \
  --output packaging/ios-personal-official-engine-assets.json --check
python3 packaging/audit_ios_retail_import_bundle.py official-provenance \
  --repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 28 \
  --output packaging/ios-personal-official-engine-assets.json --check
git diff --exit-code -- packaging/ios-personal-official-engine-assets.json
```

Require exact headers, strict no-fuzz, duplicate-forward refusal without mutation, reverse dry-run, and fresh `0001`…`0028` replay byte equality. Update README/verifier tests, stage/display only the three delivery files, then:

```bash
set -euo pipefail
git diff --cached --check
git commit -m "Deliver iOS About source compliance engine patch"
test -z "$(git status --short)"
```

---

### Task 21: Add Personal Ad Hoc build, signature, equality, and redacted-evidence tooling

**Files:**

- Create: `packaging/audit_ios_personal_ad_hoc.py`
- Create: `packaging/verify_ios_personal_release_evidence.py`
- Create: `packaging/tests/test_ios_personal_ad_hoc.py`
- Create: `packaging/tests/test_ios_personal_release_evidence.py`
- Create: `ios/scripts/build-personal-ad-hoc.sh`
- Create: `ios/scripts/run-personal-ad-hoc-release.sh`
- Modify: `ios/scripts/run-retail-import-acceptance.sh`
- Modify: `packaging/tests/test_ios_retail_acceptance_evidence.py`
- Create: `docs/testing/IOS_PERSONAL_AD_HOC_RELEASE.md`
- Modify: `docs/testing/IOS_RELEASE_REGRESSION_GATES.md`

All paths must match Task 18's reviewed `task21-outer-implementation-paths` boundary.

- [ ] **Step 1: Write RED signature/profile/equality/orchestration tests**

Using synthetic app/ZIP/profile/entitlement fixtures and an injected subprocess runner, require:

- strict `codesign` on original and safely extracted IPA app;
- leaf signer certificate is in the embedded profile, current, validly chained, and Apple Distribution policy-compliant without recording its subject;
- embedded profile CMS signature and Apple trust chain verify before any plist field is trusted; its UUID equals `IOS_AD_HOC_PROVISION_UUID` without outputting either value; it is unexpired, contains registered devices, is not enterprise/all-devices, development, or TestFlight, matches signed certificate/team/app identifiers internally, and contains `IOS_DEVICE_UDID` without outputting it;
- `get-task-allow` false/absent; app entitlements are an authorized policy-aware subset; wildcard/application/keychain group matching is fail-closed;
- PersonalDistribution true/PublicClean false, valid source commit/URL/five docs, and importer/AOT/brand/no-retail/dependency audits;
- absent, unauthorized, inaccessible, moving, or commit-mismatched public source maps to `SourcePublicationUnavailable` before any signing/build/device action; no shell may treat a bare verification exit as sufficient blocked evidence;
- central IPA module only, complete manifest equality, and failure for any changed type/mode/path/hash or stale reference;
- final re-verification fails if either the IPA or detached digest changes after the first audit or device acceptance, and never reuses the first extracted tree as proof;
- development signer, missing private key, unsigned/forged/tampered CMS, wrong profile UUID, mismatched/expired profile, unregistered device, unauthorized entitlement, or post-sign mutation fails with closed error code;
- output/redacted JSON contains no signing/profile/team/device values.
- a passed record requires explicit closed legal-review confirmation; missing or non-`true` confirmation maps only to `LegalReviewUnavailable`.

Shell tests require `set -euo pipefail`, an immediate `set +x` before reading or expanding any sensitive variable, no later `set -x`, clean commit, fresh shared clone, official replay through `0028`, relative `bin/obj`, explicit PersonalDistribution/PublicClean properties, no signing value in args/log/evidence, no recursive deletion, and no physical-device uninstall. Build modes:

- `unsigned-candidate`: audited unsigned Personal app, no IPA, and closed evidence `status=blocked`, `failureCode=UnsignedCandidateOnly`; all signing/install/runtime booleans remain false;
- `ad-hoc`: final inputs mandatory; no success until post-extraction signature/equality.

`run-retail-import-acceptance.sh` gains device-only `--prebuilt-app`, `--artifact-audit`, and `--app-manifest`. Before install, after install, after launch, and after acceptance it re-manifests the exact local app with the central IPA module, compares it byte-for-byte with the supplied extracted-app manifest and artifact report, and reruns strict signature verification. It must use that audited IPA-derived app without rebuilding/substitution and retain Task 16 redaction/reset protections. Tests make a one-byte/mode/path change at each boundary and require a closed failure before further device action.

```bash
set -euo pipefail
python3 -m unittest \
  packaging.tests.test_ios_personal_ad_hoc \
  packaging.tests.test_ios_personal_release_evidence \
  packaging.tests.test_ios_retail_acceptance_evidence -v
bash -n ios/scripts/build-personal-ad-hoc.sh
bash -n ios/scripts/run-personal-ad-hoc-release.sh
bash -n ios/scripts/run-retail-import-acceptance.sh
```

Expected RED: missing tooling/schema/prebuilt-app route.

- [ ] **Step 2: Implement post-sign Ad Hoc audit**

`audit_ios_personal_ad_hoc.py` accepts original app, final IPA, safely extracted app, source/build roots, and environment-variable **names** for profile/device checks. It invokes `codesign`/`security` without echoing sensitive output, parses temporary data under `private/`, emits only closed booleans/error codes plus the canonical non-secret app bundle identifier needed for launch, and delegates every IPA operation/manifest to `ios_ipa_archive.py`. A `bundle-identifier` subcommand must re-read the extracted app, match it to the audited report, and return only that identifier; a `verify-install` subcommand must privately compare the CoreDevice install result with the same identifier. A `reverify-artifact` subcommand must stream-hash the current IPA and validate the detached digest, safely inspect/extract it again into a new empty private directory through the central module, compare its complete manifest with the accepted reference, and rerun strict signature/profile/entitlement checks immediately before final evidence is recorded.

Derive—not trust—Ad Hoc status from the real app signature, cryptographically verified embedded profile CMS, exact requested-profile match, device membership, certificate/team/app-id linkage, and entitlements. Merely decoding profile plist bytes is insufficient. Never delete installed app/device data. Decoded profiles, certificates, raw entitlements, and CoreDevice logs never enter public report.

- [ ] **Step 3: Implement direct signed-app-to-IPA build**

`build-personal-ad-hoc.sh --mode ad-hoc --output-dir DIRECTORY` performs: clean revision; fresh shared clone; official replay `0001`…`0028`; public-source verification; Release `ios-arm64`, PersonalDistribution true/PublicClean false build with `IOS_CODESIGN_KEY` and `IOS_AD_HOC_PROVISION_UUID` read only from environment; integrated pre-sign audits; first signature/profile/entitlement audit; central safe IPA creation; safe extraction; complete manifest equality; second signature/profile/entitlement audit; importer IPA audit with explicit original reference; detached SHA-256 and redacted artifact report.

Never guess siblings, use `ditto`/shell ZIP, run a second build, or silently Archive/Export. Exact outputs:

```text
NUKE HOUR-Personal-AdHoc.ipa
NUKE HOUR-Personal-AdHoc.ipa.sha256
original-app.manifest.json
extracted-app.manifest.json
personal-ad-hoc-artifact-audit.json
extracted/Payload/OpenRA.iOS.app/
private/
```

Unsigned mode stops before signing/IPA and writes only unsigned app plus candidate/blocked report.

- [ ] **Step 4: Implement the closed release evidence schema**

`verify_ios_personal_release_evidence.py` accepts/emits only this schema; missing/extra keys fail. The object below is the passed-artifact form:

```json
{
  "schemaVersion": 1,
  "sourceCommit": "40 lowercase hex",
  "status": "passed|blocked|failed",
  "failureCode": null,
  "artifactClass": "PersonalAdHoc",
  "buildConfiguration": "PersonalDistribution",
  "distributionMethod": "ad-hoc",
  "freshReplayPatchEnd": "0028",
  "personalPublicCleanMutualExclusionPassed": true,
  "publicCleanDiagnosticState": "historical-failure",
  "publicCleanErrorDelta": 0,
  "aboutDocuments": ["AUTHORS", "COPYING", "THIRD_PARTY_LICENSES", "NUKEHOUR_MODIFICATIONS", "SOURCE_INFORMATION"],
  "aboutDocumentAuditPassed": true,
  "dependencyInventoryClosed": true,
  "immutableSourceAuditPassed": true,
  "legalReviewConfirmed": true,
  "appAuditPassed": true,
  "ipaAuditPassed": true,
  "completeManifestEqual": true,
  "modesEqual": true,
  "hashesEqual": true,
  "originalCodesignVerified": true,
  "extractedCodesignVerified": true,
  "appleDistributionSignerVerified": true,
  "adHocProfileVerified": true,
  "deviceRegisteredInProfile": true,
  "entitlementsAuthorized": true,
  "getTaskAllowDisabled": true,
  "installedFromExtractedIpaApp": true,
  "launchedOnRegisteredDevice": true,
  "retailAcceptancePassed": true,
  "baseImportReady": true,
  "twoPlayerSkirmishStarted": true,
  "repositoryCleanAfterAcceptance": true
}
```

`failureCode` is null only for passed. Closed enum: `UnsignedCandidateOnly`, `OfficialArchiveUnavailable`, `SigningCertificateUnavailable`, `AdHocProfileUnavailable`, `RegisteredDeviceUnavailable`, `SourcePublicationUnavailable`, `LegalReviewUnavailable`, `LegalFixtureUnavailable`, `ColdResetAuthorizationUnavailable`, `BuildFailed`, `ArtifactAuditFailed`, `InstallFailed`, `LaunchFailed`, `RetailAcceptanceFailed`, `RepositoryDirty`.

`artifactClass`, `buildConfiguration`, and `distributionMethod` are closed correlated triples. Before a real IPA has passed both signature/profile audits they must be `UnsignedCandidate`, `PersonalCandidate`, and `none`; such a record cannot be passed and cannot set any IPA/signing/install/runtime boolean true. Only after those artifact gates pass may they become `PersonalAdHoc`, `PersonalDistribution`, and `ad-hoc`; a later install/launch/acceptance failure may retain that truthful signed-artifact triple while status is failed or blocked. An `UnsignedCandidateOnly` record must use the unsigned triple and must never contain the word Ad Hoc in its user-facing classification.

Replay and PublicClean fields are also execution-correlated: `freshReplayPatchEnd` is null until the exact replay finishes and may then be only `0028`; `publicCleanDiagnosticState` is `not-run` with `publicCleanErrorDelta=null` until the diagnostic finishes, then is `historical-failure` with the observed nonnegative integer delta. Early blocked records must not claim replay or delta evidence. Passed requires `0028`, `historical-failure`, and delta zero.

Blocked records use unavailable/authorization codes and false for unexecuted booleans. Passed requires the signed-artifact triple, all booleans including `legalReviewConfirmed`, the exact five-document array, completed replay, historical-failure state, and delta zero; it never requires the 580 errors to disappear.

Reject free text, URLs, paths, 64-hex values, IPs, accounts, certificate/profile/team/device identifiers, retail names, and extra diagnostics. `sourceCommit` is the only allowed hex revision. Detached IPA SHA and private phase reports remain separate.

The tool also provides `record-blocked --source-commit COMMIT --failure-code CLOSED_CODE --output FILE`. It accepts only `OfficialArchiveUnavailable`, `SigningCertificateUnavailable`, `AdHocProfileUnavailable`, `RegisteredDeviceUnavailable`, `SourcePublicationUnavailable`, `LegalReviewUnavailable`, `LegalFixtureUnavailable`, `ColdResetAuthorizationUnavailable`, or `RepositoryDirty`; fills the unsigned correlated triple, `freshReplayPatchEnd=null`, `publicCleanDiagnosticState=not-run`, `publicCleanErrorDelta=null`, and every unexecuted boolean with false; emits no note or external value; and immediately passes the same strict verifier. Preflight scripts must use it before stopping for a missing external input or dirty repository; a raw shell abort is not honest blocked evidence.

- [ ] **Step 5: Implement final orchestration and docs**

`run-personal-ad-hoc-release.sh` runs preflight/build, identifies the one extracted app from parser report, installs/launches it, runs retail acceptance with `--prebuilt-app`, `--artifact-audit`, and the exact extracted-app manifest, and builds/verifies release evidence. It reads `IOS_LEGAL_REVIEW_CONFIRMED`, `IOS_LEGAL_RETAIL_FIXTURE_CONFIRMED`, and `IOS_COLD_RESET_AUTHORIZED` only as closed literal `true` values; the evidence record command accepts the legal-review environment-variable name rather than free-form text. The acceptance script independently requires the latter two before any reset or legal-fixture step and still relies on the device-side manual proof. CoreDevice JSON/logs remain under `private/` and are never echoed.

`IOS_PERSONAL_AD_HOC_RELEASE.md` documents operator prompts, legal review, trust/lock/Developer Mode, reset authorization, no-uninstall policy, Files retail selection, Base Ready, and real skirmish; it says not legal advice and PublicClean/App Store/TestFlight are out of scope.

- [ ] **Step 6: Run GREEN and unsigned candidate**

```bash
set -euo pipefail
python3 -m unittest \
  packaging.tests.test_ios_personal_ad_hoc \
  packaging.tests.test_ios_personal_release_evidence \
  packaging.tests.test_ios_retail_acceptance_evidence \
  packaging.tests.test_ios_ipa_archive -v
bash -n ios/scripts/build-personal-ad-hoc.sh
bash -n ios/scripts/run-personal-ad-hoc-release.sh
bash -n ios/scripts/run-retail-import-acceptance.sh
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
TASK21_STAGE="$(mktemp -d /private/tmp/nuke-hour-personal-adhoc-task21.XXXXXX)"
OPENRA_OFFICIAL_ZIP="$OPENRA_OFFICIAL_ZIP" \
  bash ios/scripts/build-personal-ad-hoc.sh \
  --mode unsigned-candidate --output-dir "$TASK21_STAGE/unsigned"
test -d "$TASK21_STAGE/unsigned/OpenRA.iOS.app"
test ! -e "$TASK21_STAGE/unsigned/NUKE HOUR-Personal-AdHoc.ipa"
python3 packaging/verify_ios_personal_release_evidence.py \
  verify "$TASK21_STAGE/unsigned/personal-ad-hoc-release-evidence.json"
git diff --check
```

Expected: unsigned candidate verifies only as `blocked/UnsignedCandidateOnly`, is explicitly non-publishable, and emits no IPA.

- [ ] **Step 7: Commit tooling before real signing**

Stage/display exactly the ten paths and compare Task 18, then:

```bash
set -euo pipefail
git diff --cached --check
git commit -m "Add Personal Ad Hoc release gates"
test -z "$(git status --short)"
```

Do not run Ad Hoc mode, inspect local signing identities, decode a real profile, or access a device in this commit.

---

### Task 22: Replay, sign, package, install, launch, and accept the final Personal IPA

**Repository files:**

- Inspect only: all committed source, `engine-patches/0001-*.patch` through `engine-patches/0028-*.patch`, and Task 18 boundary
- Modify: none
- Create outside Git: one unique `/private/tmp/nuke-hour-personal-adhoc-final.*`

- [ ] **Step 1: Run external-input and clean-source preflight**

Run Steps 1–8 in the same non-interactive Bash session. The first commands fix fail-closed semantics and disable inherited xtrace for every later block; never invoke this flow with `bash -x` or paste individual later blocks into an unguarded shell.

```bash
set -euo pipefail
set +x
FINAL_SOURCE_COMMIT="$(git rev-parse HEAD)"
FINAL_ROOT="$(mktemp -d /private/tmp/nuke-hour-personal-adhoc-final.XXXXXX)"
FINAL_OUTPUT="$FINAL_ROOT/output"
mkdir -p "$FINAL_OUTPUT"
record_blocked() {
  python3 packaging/verify_ios_personal_release_evidence.py record-blocked \
    --source-commit "$FINAL_SOURCE_COMMIT" --failure-code "$1" \
    --output "$FINAL_OUTPUT/personal-ad-hoc-release-evidence.json"
  python3 packaging/verify_ios_personal_release_evidence.py verify \
    "$FINAL_OUTPUT/personal-ad-hoc-release-evidence.json"
  exit 2
}
test -z "$(git status --short)" || record_blocked RepositoryDirty
git diff --check || record_blocked RepositoryDirty
test -n "${OPENRA_OFFICIAL_ZIP:-}" || record_blocked OfficialArchiveUnavailable
test -n "${IOS_SOURCE_URL:-}" || record_blocked SourcePublicationUnavailable
test "${IOS_LEGAL_REVIEW_CONFIRMED:-}" = true || record_blocked LegalReviewUnavailable
test "${IOS_LEGAL_RETAIL_FIXTURE_CONFIRMED:-}" = true || record_blocked LegalFixtureUnavailable
test "${IOS_COLD_RESET_AUTHORIZED:-}" = true || record_blocked ColdResetAuthorizationUnavailable
test -n "${IOS_CODESIGN_KEY:-}" || record_blocked SigningCertificateUnavailable
test -n "${IOS_AD_HOC_PROVISION_UUID:-}" || record_blocked AdHocProfileUnavailable
test -n "${IOS_DEVICE_UDID:-}" || record_blocked RegisteredDeviceUnavailable
```

Confirm final legal review, legal retail access, and cold-reset authorization outside the script. If unavailable, emit the exact blocked code and stop—never substitute development signing or call an unsigned candidate publishable.

- [ ] **Step 2: Verify public corresponding source**

```bash
set +e
IOS_SOURCE_URL="$IOS_SOURCE_URL" \
python3 packaging/generate_ios_about_documents.py verify-public-source \
  --source-root "$PWD" --source-commit "$FINAL_SOURCE_COMMIT" \
  --source-url-env IOS_SOURCE_URL \
  --work-dir "$FINAL_ROOT/private/source-verification" \
  --redacted-report "$FINAL_ROOT/source-verification.json"
SOURCE_VERIFY_RC=$?
set -e
test "$SOURCE_VERIFY_RC" -eq 0 || \
  record_blocked SourcePublicationUnavailable
```

Require the immutable public target to contain exact corresponding modified source/build material and exclude private retail/signing/device material. This is read-only verification of the separately authorized external prerequisite. This plan performs no repository creation, push, tag, release, or publication action; if the prerequisite was not separately authorized and completed, the earlier `SourcePublicationUnavailable` branch is the only honest result.

- [ ] **Step 3: Replay all patches and final workspace**

Run `verify_engine_patch_delivery.py` separately for `0020` through `0028` against each immediate predecessor:

```bash
for PATCH_NUMBER in {20..28}; do
  PATCH_ID="$(printf '%04d' "$PATCH_NUMBER")"
  PATCH_CANDIDATES=(engine-patches/"$PATCH_ID"-*.patch)
  test "${#PATCH_CANDIDATES[@]}" -eq 1
  test -f "${PATCH_CANDIDATES[0]}"
  DELIVERY_COMMIT="$(git log --diff-filter=A --format=%H -- "${PATCH_CANDIDATES[0]}")"
  test "$(printf '%s\n' "$DELIVERY_COMMIT" | grep -Ec '^[0-9a-f]{40}$')" -eq 1
  git merge-base --is-ancestor "$DELIVERY_COMMIT" HEAD
  HISTORY_CLONE="$FINAL_ROOT/history-$PATCH_ID"
  git clone --shared --no-checkout "$PWD" "$HISTORY_CLONE"
  git -C "$HISTORY_CLONE" checkout --detach "$DELIVERY_COMMIT"
  test -z "$(git -C "$HISTORY_CLONE" status --short)"
  cmp "${PATCH_CANDIDATES[0]}" "$HISTORY_CLONE/${PATCH_CANDIDATES[0]}"
  PATCH_ALLOWLIST="$FINAL_ROOT/$PATCH_ID.allowlist"
  HEAD_ALLOWLIST="$FINAL_ROOT/$PATCH_ID.head.allowlist"
  python3 packaging/engine_workspace.py allowlist \
    --patch-id "$PATCH_ID" --output "$HEAD_ALLOWLIST"
  python3 "$HISTORY_CLONE/packaging/engine_workspace.py" allowlist \
    --patch-id "$PATCH_ID" --output "$PATCH_ALLOWLIST"
  LC_ALL=C sort -c -u "$PATCH_ALLOWLIST"
  LC_ALL=C sort -c -u "$HEAD_ALLOWLIST"
  cmp "$HEAD_ALLOWLIST" "$PATCH_ALLOWLIST"
  python3 "$HISTORY_CLONE/packaging/verify_engine_patch_delivery.py" \
    --repository "$HISTORY_CLONE" --official-zip "$OPENRA_OFFICIAL_ZIP" \
    --expected-zip-sha256 ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a \
    --predecessor-start 1 --predecessor-end "$((PATCH_NUMBER - 1))" \
    --allowlist-file "$PATCH_ALLOWLIST" \
    --candidate "$HISTORY_CLONE/${PATCH_CANDIDATES[0]}" \
    --workspace "$HISTORY_CLONE/engine" \
    --output-dir "$FINAL_ROOT/patch-verification-$PATCH_ID"
  if test "$PATCH_ID" = 0028; then
    FINAL_ENGINE_DELIVERY_COMMIT="$DELIVERY_COMMIT"
  fi
done
test -n "${FINAL_ENGINE_DELIVERY_COMMIT:-}"
git diff --exit-code "$FINAL_ENGINE_DELIVERY_COMMIT"..HEAD -- engine
```

Then materialize the complete final workspace:

```bash
FINAL_REPLAY="$FINAL_ROOT/engine-replay"
python3 packaging/engine_workspace.py materialize \
  --repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 28 --workspace "$FINAL_REPLAY"
python3 packaging/engine_workspace.py allowlist \
  --patch-id 0028 --output "$FINAL_ROOT/0028.allowlist"
LC_ALL=C sort -c -u "$FINAL_ROOT/0028.allowlist"
python3 packaging/audit_ios_retail_import_bundle.py official-provenance \
  --repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 28 \
  --output packaging/ios-personal-official-engine-assets.json --check
```

Require only pinned `0007` diagnostics, no other fuzz/offset/reject/orig files, exact headers/allowlists, duplicate-forward refusal, reverse dry-run, and complete byte equality.

- [ ] **Step 4: Run all gates and PublicClean delta diagnostic**

```bash
/private/tmp/nuke-hour-task0-python311/bin/python -m unittest discover \
  -s packaging/tests -p 'test_*.py' -v
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet restore \
  engine/OpenRA.Test/OpenRA.Test.csproj -p:NuGetAudit=false -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 /nr:false \
  -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Utility/OpenRA.Utility.csproj -m:1 \
  /nr:false -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
YAML_REPLAY_ROOT="$PWD"
(
  cd engine
  MOD_SEARCH_PATHS="./mods,$YAML_REPLAY_ROOT/mods" ENGINE_DIR=".." \
    env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet \
    bin/OpenRA.Utility.dll ra2 --check-yaml
)
PUBLIC_DIAGNOSTIC_DIR="$FINAL_ROOT/public-diagnostic" \
  bash ios/scripts/build-public-clean.sh
test -f "$FINAL_ROOT/public-diagnostic/public-clean-diagnostic.json"
test -z "$(find "$FINAL_ROOT/public-diagnostic" -type f -name '*.ipa' -print -quit)"
```

PublicClean remains a historical failure with zero new delta; the 580-error baseline is not a pass and does not block the distinct Personal artifact.

- [ ] **Step 5: Build, sign, package, extract, and audit one artifact**

```bash
OPENRA_OFFICIAL_ZIP="$OPENRA_OFFICIAL_ZIP" \
IOS_SOURCE_URL="$IOS_SOURCE_URL" \
IOS_LEGAL_REVIEW_CONFIRMED="$IOS_LEGAL_REVIEW_CONFIRMED" \
IOS_CODESIGN_KEY="$IOS_CODESIGN_KEY" \
IOS_AD_HOC_PROVISION_UUID="$IOS_AD_HOC_PROVISION_UUID" \
IOS_DEVICE_UDID="$IOS_DEVICE_UDID" \
bash ios/scripts/build-personal-ad-hoc.sh \
    --mode ad-hoc --output-dir "$FINAL_OUTPUT"
FINAL_IPA="$FINAL_OUTPUT/NUKE HOUR-Personal-AdHoc.ipa"
FINAL_EXTRACTED_APP="$FINAL_OUTPUT/extracted/Payload/OpenRA.iOS.app"
test -f "$FINAL_IPA"
test -d "$FINAL_EXTRACTED_APP"
cmp "$FINAL_OUTPUT/original-app.manifest.json" \
  "$FINAL_OUTPUT/extracted-app.manifest.json"
codesign --verify --deep --strict --verbose=4 "$FINAL_EXTRACTED_APP"
python3 packaging/audit_ios_personal_ad_hoc.py verify-report \
  "$FINAL_OUTPUT/personal-ad-hoc-artifact-audit.json"
```

Require the actual Ad Hoc signer/profile/device/entitlements, five documents/inventory/source URL, both signatures, complete type/mode/size/hash equality, and the superseding official-engine provenance bound to strict `0001`…`0027` plus this exact `0028` candidate. Never skip the independent `cmp` or second `codesign`.

- [ ] **Step 6: Install exact IPA-derived app and launch**

```bash
xcrun devicectl device install app \
  --device "$IOS_DEVICE_UDID" --timeout 600 --quiet \
  --json-output "$FINAL_OUTPUT/private/device-install.json" \
  --log-output "$FINAL_OUTPUT/private/device-install.log" \
  "$FINAL_EXTRACTED_APP"
FINAL_BUNDLE_ID="$(python3 packaging/audit_ios_personal_ad_hoc.py bundle-identifier \
  --report "$FINAL_OUTPUT/personal-ad-hoc-artifact-audit.json" \
  --app "$FINAL_EXTRACTED_APP")"
test -n "$FINAL_BUNDLE_ID"
python3 packaging/audit_ios_personal_ad_hoc.py verify-install \
  --report "$FINAL_OUTPUT/personal-ad-hoc-artifact-audit.json" \
  --install-result "$FINAL_OUTPUT/private/device-install.json"
xcrun devicectl device process launch \
  --device "$IOS_DEVICE_UDID" --timeout 120 --quiet --terminate-existing \
  --json-output "$FINAL_OUTPUT/private/device-launch.json" \
  --log-output "$FINAL_OUTPUT/private/device-launch.log" \
  "$FINAL_BUNDLE_ID"
```

CoreDevice installs `.app` bundles and launches installed apps by the audited bundle identifier; complete manifest equality and signature verification prove the installed payload came from the final IPA. Never print or commit these logs, uninstall the app, or erase its container. Reset only through the separately authorized Task 16 procedure.

- [ ] **Step 7: Run full retail import and skirmish against this payload**

```bash
IOS_DEVICE_UDID="$IOS_DEVICE_UDID" \
IOS_LEGAL_RETAIL_FIXTURE_CONFIRMED="$IOS_LEGAL_RETAIL_FIXTURE_CONFIRMED" \
IOS_COLD_RESET_AUTHORIZED="$IOS_COLD_RESET_AUTHORIZED" \
bash ios/scripts/run-retail-import-acceptance.sh \
  --mode device \
  --prebuilt-app "$FINAL_EXTRACTED_APP" \
  --artifact-audit "$FINAL_OUTPUT/personal-ad-hoc-artifact-audit.json" \
  --app-manifest "$FINAL_OUTPUT/extracted-app.manifest.json" \
  --evidence-output "$FINAL_OUTPUT/retail-import-device-evidence.json"
```

Require all Task 16 device invariants, both selection routes, Base Ready, preservation/recovery, retail bytes private-only, unchanged signature, and a real two-player Base skirmish. The guard must prove the accepted app still equals the IPA manifest and must never rebuild or substitute it.

- [ ] **Step 8: Produce and verify redacted evidence**

```bash
python3 packaging/audit_ios_personal_ad_hoc.py reverify-artifact \
  --ipa "$FINAL_IPA" --detached-sha256 "$FINAL_IPA.sha256" \
  --artifact-report "$FINAL_OUTPUT/personal-ad-hoc-artifact-audit.json" \
  --expected-manifest "$FINAL_OUTPUT/extracted-app.manifest.json" \
  --work-dir "$FINAL_OUTPUT/private/final-artifact-recheck"
IOS_LEGAL_REVIEW_CONFIRMED="$IOS_LEGAL_REVIEW_CONFIRMED" \
python3 packaging/verify_ios_personal_release_evidence.py record \
  --source-commit "$FINAL_SOURCE_COMMIT" \
  --artifact-report "$FINAL_OUTPUT/personal-ad-hoc-artifact-audit.json" \
  --public-clean-report "$FINAL_ROOT/public-diagnostic/public-clean-diagnostic.json" \
  --source-report "$FINAL_ROOT/source-verification.json" \
  --acceptance-report "$FINAL_OUTPUT/retail-import-device-evidence.json" \
  --legal-review-env IOS_LEGAL_REVIEW_CONFIRMED \
  --output "$FINAL_OUTPUT/personal-ad-hoc-release-evidence.json"
python3 packaging/verify_ios_personal_release_evidence.py verify \
  "$FINAL_OUTPUT/personal-ad-hoc-release-evidence.json"
test -f "$FINAL_IPA.sha256"
test -z "$(git status --short)"
git diff --check
```

Keep artifacts, evidence, and private diagnostics outside Git. No upload, push, tag, or publication is authorized.

---

## Completion criteria

Complete only when:

- Tasks 0–17, including legal-retail device acceptance, passed honestly;
- Task 18 froze an independently reviewed exact `0028` boundary after fresh `0027` replay;
- strict `0028` represents every engine change and fresh `0001` through `0028` replay equals the tested workspace;
- the official-engine asset manifest was regenerated from that same replay/candidate, records effective patch end `0028`, and authenticates every final bundle-eligible engine asset rather than reusing Task 15's pre-`0028` manifest;
- the final build is a mutually exclusive Release `ios-arm64` Personal Ad Hoc artifact—not development, PublicClean, App Store, or TestFlight;
- exactly five About/source documents, an immutable corresponding-source URL, the closed dependency inventory, and final human/legal review pass;
- the legacy PublicClean script no longer signs or packages, the central IPA module is the sole production IPA implementation, and no `extractall` or unsafe shell extraction remains;
- PublicClean remains the historical 580-error failure with zero new delta, is never called a pass, and does not replace the Personal gate;
- original and extracted full manifests match and both signature/profile/entitlement audits pass;
- the IPA-derived app installs and launches on the registered device, and that exact payload imports legal retail data and starts a real two-player Base skirmish;
- the IPA, detached digest, and closed redacted evidence exist outside Git and the repository is clean;
- no publishable claim exists while any signing, source, review, device, reset, fixture, install, or runtime gate is blocked.

Passing authorizes describing that artifact only as a registered-device **Personal Ad Hoc IPA**. It does not authorize PublicClean, App Store/TestFlight, GitHub publication action, retail redistribution, or a legal conclusion.
