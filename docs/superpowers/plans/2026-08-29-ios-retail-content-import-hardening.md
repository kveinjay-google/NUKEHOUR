# iOS Retail Content Import Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the registered-device Personal iOS/iPadOS build import a legally owned extracted Red Alert 2 installation safely, preserve a verified RA2 Base installation across failures, and permit entry only when the active profile can launch a real Base-compatible skirmish map; keep PublicClean as a no-profile-resource, fail-closed diagnostic target.

**Architecture:** A bounded discovery/validation pipeline produces an immutable candidate catalog; crash-safe generation transactions atomically publish only complete catalogs under Application Support. Fixed Base/Yuri manifest entries and a deterministic runtime-contract digest isolate network, save, replay, and settings compatibility. A read-only active-catalog lease is the only retail mount, while separately bounded remote metadata and runtime-map snapshot services preserve community-map downloads without making the retail mount writable. UIKit supplies coordinated Files access and a pure state-derived wizard; engine changes ship as strict sequential patches `0020` through `0027`.

**Tech Stack:** C# / .NET 8, OpenRA MiniYaml/MIX/Map APIs, UIKit and Foundation, NUnit, Python 3.11 `unittest`, Apple filesystem primitives, shell packaging, strict `diff -ruN` engine-patch replay.

---

## Authoritative design and release boundary

- Authoritative design: `docs/superpowers/specs/2026-08-28-ios-retail-content-import-hardening-design.md`.
- Authoritative runtime-resource shape: `packaging/ios-runtime-contract-descriptor-shape-v1.json`, schema `1`, exactly `296` byte-sorted descriptors, file SHA-256 `6c0d12b5a985bcf87fc255a03a958dbc09be00de9238b239354c8d34be1bd8f6`, and framed-shape SHA-256 `4fe820475ee4d6ef843320f406b5db3fd414fd106c953b4a7b4a05020fb3614d`. This checked-in golden is a reviewed design input, not generated implementation output.
- About/source compliance for the registered-device PersonalDistribution milestone is deferred to `docs/superpowers/specs/2026-08-28-ios-about-source-compliance-design.md` until importer device acceptance passes. The authoritative design's 2026-08-30 scope clarification makes Personal the only current release/runtime target; PublicClean distribution and its full importer/readiness runtime are separately deferred until reviewed runtime resources are restored and its zero-error closure passes.
- First release accepts individual extracted retail files or an extracted install folder. It does not open ISO/CAB/HDR/ZIP/RAR/7z media and never downloads retail/Demo assets.
- RA2 Base plus one verified Base-compatible two-spawn map is required. Yuri, RA2 music, Yuri music, and additional maps are isolated optional groups.
- Personal runs the complete pre-game importer/recovery/readiness gate. PublicClean contains none of Task 3's 17 profile resources, performs only configuration-aware source/bundle diagnostics, and is expected to fail closed as `RuntimeResourcesMissing` before importer, active-catalog, runtime-service, or game construction; this plan does not launch it to claim that typed runtime observation. A future separately authorized PublicClean closure must restore reviewed resources and then run the same complete gate. Desktop `mod.yaml` and desktop importer behavior stay unchanged.
- Runtime community-map download is a distinct bounded service. It is not a retail source and never makes the retail catalog writable.
- The current PublicClean 580-error report is historical failure evidence, not a passing release baseline. This plan freezes that delta, proves the 17 profile resources are absent, and adds importer-specific source/bundle diagnostics without launching PublicClean, constructing the importer/game, updating the baseline, or relabeling any diagnostic as passing.
- No GitHub remote, repository, tag, push, or public app release is authorized by this plan.

## Fixed V1 constants and identities

Implement one `IosImportEnvelopePolicyV1` source of truth; tests and audits read these exact public constants rather than duplicating numbers:

```csharp
public static class IosImportEnvelopePolicyV1
{
	public const int PickerRoots = 64;
	public const int DiscoveryDepth = 24;
	public const int DiscoveryEntries = 100_000;
	public const long DiscoveryBytes = 32L << 30;
	public const int CandidateFiles = 4_096;
	public const long CandidateBytes = 8L << 30;
	public const long SourceBytes = 2L << 30;
	public const int MixDepth = 8;
	public const int MixEntriesPerArchive = 131_072;
	public const int MixEntriesAggregate = 262_144;
	public const long MixIndexBytes = 16L << 20;
	public const long MixLocalDatabaseBytes = 8L << 20;
	public const int MixLocalDatabaseNames = 262_144;
	public const long MixSegmentBytes = 512L << 20;
	public const long AudioBagBytes = 384L << 20;
	public const long MixAddressedBytes = 8L << 30;
	public const long IdxBytes = 4L << 20;
	public const int IdxSoundRecords = 65_536;
	public const int IdxNameBytes = 16;
	public const long BagBytes = 384L << 20;
	public const long BagSoundSpanBytes = 256L << 20;
	public const long TraditionalMapSourceBytes = 32L << 20;
	public const int TraditionalMapWidth = 512;
	public const int TraditionalMapHeight = 512;
	public const int TraditionalMapCells = 262_144;
	public const int MapActors = 8_192;
	public const int MapPlayerDefinitions = 63;
	public const int MapSpawns = 8;
	public const int ImportedMaps = 256;
	public const int RuntimeMaps = 128;
	public const long OramapCompressedBytes = 64L << 20;
	public const int OramapEntries = 8;
	public const long OramapExpandedEntryBytes = 64L << 20;
	public const long OramapExpandedAggregateBytes = 96L << 20;
	public const int OramapExpansionRatio = 100;
	public const long MapYamlBytes = 8L << 20;
	public const int MapYamlNodes = 131_072;
	public const int MapYamlDepth = 64;
	public const int MapYamlLineBytes = 256 << 10;
	public const int MapYamlScalarBytes = 64 << 10;
	public const int MapPngEdge = 4_096;
	public const long MapPngPixels = 16_777_216;
	public const long MapPngDecodedBytes = 64L << 20;
	public const int MapPngChunks = 4_096;
	public const long MapPngIdatBytes = 32L << 20;
	public const int MapPngInflateRatio = 100;
	public const long ImportedMapStorageBytes = 2L << 30;
	public const int PreviewThumbnailEdge = 1_024;
	public const int PreviewAtlasPage = 2_048;
	public const int PreviewAtlasMargin = 1;
	public const long PreviewCpuBudget = 64L << 20;
	public const long PreviewGpuBudget = 128L << 20;
	public const long ResidentMapMetadataBytes = 64L << 20;
	public const long ResidentStringBytes = 32L << 20;
	public const int ResidentStringEntries = 262_144;
	public const long PerMapStringBytes = 8L << 20;
	public const long RuntimeGenerationBytes = 1L << 30;
	public const long RuntimeWithRollbackBytes = 2L << 30;
	public const int RuntimeDownloadQueue = 4;
	public const long RuntimeDownloadStagingBytes = 256L << 20;
	public const long DiskSafetyMarginBytes = 512L << 20;
	public const long RemoteResponseBytes = 4L << 20;
	public const int RemoteRequestedUids = 256;
	public const int RemoteRecords = 256;
	public const long RemoteRecordBytes = 3L << 20;
	public const int RemoteOrdinaryLineBytes = 64 << 10;
	public const int RemoteBinaryLineBytes = 2_304 << 10;
	public const int RemoteNodes = 16_384;
	public const int RemoteDepth = 32;
	public const int RemoteFieldsPerRecord = 64;
	public const int RemoteScalarBytes = 16 << 10;
	public const int RemoteEncodedBinaryBytes = 2 << 20;
	public const int RemoteDecodedBinaryBytes = 1_572_864;
	public const int RemoteMinimapEdge = 1_024;
	public const int RemoteMinimapPixels = 1_048_576;
	public const int RemoteMinimapDecodedBytes = 4 << 20;
	public const int RemotePlayers = 64;
	public const int RemoteSpawns = 8;
	public const int RemoteCategories = 64;
	public const int RemoteRetainedRecords = 512;
	public const long RemoteRetainedBytes = 16L << 20;
	public const long RemoteRetainedStringBytes = 4L << 20;
	public const int RemoteRetainedStringEntries = 32_768;
	public const int PackagePathUtf8Bytes = 64;
	public const int PackagePathDepth = 1;
	public const long PendingBytes = 8L << 30;
	public static readonly TimeSpan PendingRetention = TimeSpan.FromDays(7);
	public static readonly TimeSpan ProviderPreparationTimeout = TimeSpan.FromMinutes(30);
	public static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromMinutes(10);
	public static readonly TimeSpan ArchiveTimeout = TimeSpan.FromMinutes(10);
	public static readonly TimeSpan MapTimeout = TimeSpan.FromMinutes(5);
	public static readonly TimeSpan RemoteMetadataTimeout = TimeSpan.FromSeconds(30);
	public static readonly TimeSpan WholeValidationTimeout = TimeSpan.FromMinutes(90);
}
```

Every constant above is the exact committed-design V1 value. Every row receives `limit - 1`, exact limit, `limit + 1`, negative/overflow where meaningful, and aggregate-pressure coverage; changing one requires a validator-version change and review.

Fixed profile and storage identities:

```text
manifest entries: mod.ios-base.yaml | mod.ios-yuri.yaml
canonical mod id: ra2
active mount: content
private root: Library/Application Support/NUKEHOUR/ImportedContent
generation id: g-{epoch:D20}-{guid:N}
commit id: c-{guid:N}
pending id: p-{guid:N}
retail/runtime map filename: {canonicalLowercaseMapUid}.oramap
```

Every `{guid:N}` token above is exactly 32 lowercase hexadecimal characters produced from one non-empty GUID; uppercase, braces, hyphens inside the token, and the all-zero GUID are noncanonical. `epoch` is a nonnegative signed 64-bit value rendered as exactly 20 decimal digits, and the generation ID's embedded epoch must equal the separately authenticated epoch field. These three grammars are permanent V1 storage identities, not display strings or caller-selected names.

## Git, worktree, and staging protocol

- The reviewed planning snapshot is based on branch `engine-20250330` at `68f5e92`. Before Task 0, make one design-handoff commit containing exactly this plan, `docs/superpowers/specs/2026-08-28-ios-retail-content-import-hardening-design.md`, the amended `docs/superpowers/plans/2026-08-30-ios-personal-ad-hoc-release-closure.md`, and `packaging/ios-runtime-contract-descriptor-shape-v1.json`; record that commit as `PLAN_COMMIT`. That commit must also contain the already-reviewed `docs/superpowers/specs/2026-08-28-ios-about-source-compliance-design.md` inherited unchanged from the planning branch. No implementation work starts directly from the uncommitted planning checkout.
- Use a dedicated `codex/ios-retail-import-hardening` worktree. Never implement this plan in a directory containing the user's unrelated untracked artifacts.
- Preserve all retail content, private fixture paths/hashes, credentials, `artifacts/`, `docs/testing/results/`, `__pycache__`, `_CodeSignature`, `obj`, and `bin` outside commits.
- Each task's **Files** list is its exact staging allowlist. Expand it to a byte-sorted `/private/tmp/ios-retail-task-N.paths`, stage only those paths, compare with `git diff --cached --name-only --no-renames`, then run `git diff --cached --check`.
- New files under ignored `engine/` require `git add -f -- <exact-file>`; never force-add a directory and never use unrestricted `git add -A`.
- Reject staged secrets, retail extensions/data, symlinks, generated build outputs, and files over 50 MB before every commit.
- Run the listed RED command before production edits and record its actual failing assertion/compile symbol. A characterization that is already GREEN must be identified as such and cannot be presented as RED.
- Run the listed GREEN command plus `git diff --check` before every development commit.
- Execute every fenced `bash` block in a fresh `/bin/bash` process. Each block is independently self-contained, begins with `set -euo pipefail`, and may not inherit variables, arrays, options, or working-directory changes from another block or the user's default `zsh` session.
- A freshly materialized official replay deliberately retains `engine/VERSION` as `{DEV_VERSION}` for byte-exact patch verification. YAML gates must not run `make version`, edit that file, or call the top-level SDK `utility.sh`, whose installation guard requires `release-20250330`. Each gate instead builds `engine/OpenRA.Utility/OpenRA.Utility.csproj` and invokes `engine/bin/OpenRA.Utility.dll` from inside `engine/` with the explicit repository-mod search path, exactly as shown below.
- Full packaging discovery and every focused group containing the image/branding generators use the Task 0 pinned `/private/tmp/nuke-hour-task0-python311/bin/python` (Python 3.11 plus Pillow 11.3.0), never whichever unrelated Homebrew `python3` happens to lead `PATH`. Focused non-image Python modules may use the documented system `python3`. The two `iconutil` cases must run with ordinary macOS process privileges; a Codex sandbox rejection is rerun with approval and is not recorded as a product failure.

## Engine patch series

`0019-ios-support-powers-beside-production.patch` is the already-delivered support-power baseline; importer delivery begins at `0020`.

Each engine phase is delivered immediately, before later tasks touch the same engine files:

1. `0020-ios-retail-archive-envelopes.patch`
2. `0021-ios-retail-runtime-profiles.patch`
3. `0022-ios-retail-runtime-contract.patch`
4. `0023-ra2-map-conversion-service.patch`
5. `0024-verified-oramap-map-cache.patch`
6. `0025-verified-remote-map-metadata.patch`
7. `0026-verified-runtime-map-store.patch`
8. `0027-ios-retail-active-catalog-lifecycle.patch`

For every patch:

- extract the pinned official `OpenRA-release-20250330` ZIP with SHA-256 `ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a` into a fresh temporary tree;
- replay exactly `0001` through the immediate predecessor with `/usr/bin/patch --verbose -p1 --batch -V none`;
- allow only the README-pinned two `0007` diagnostics and reject every other offset/fuzz, nonzero exit, `.rej`, or `.orig`;
- build the candidate from a post-predecessor `a/` tree and exact workspace `b/` allowlist using `diff -ruN`;
- run `packaging/verify_engine_patch_delivery.py` with the exact predecessor range and allowlist;
- require strict `git apply --check`, Apple patch `-F 0` with no diagnostics, byte equality, duplicate forward refusal with unchanged full-tree manifest, and reverse dry-run success;
- commit only the new patch plus the exact README/verifier-test updates after the feature commit.

Every individual patch task below contains one self-contained `/bin/bash` block that validates `OPENRA_OFFICIAL_ZIP`, creates a unique stage directory, assigns literal `PREDECESSOR_END`, `CANDIDATE`, `ALLOWLIST`, and `VERIFY_DIR`, generates the full reviewed allowlist, then runs both `engine_workspace.py candidate` and `verify_engine_patch_delivery.py`. No task inherits a variable from a generic preamble or another fence. The helper performs the fresh `a`/`b` replay/overlay and exact `diff -ruN` exit handling; the independent verifier repeats extraction/replay and all strict gates. Preserve the unique stage directory and `evidence.json` until that patch's feature/delivery commits and final hash report are complete.

---

### Task 0: Create or resume the isolated implementation worktree and baseline evidence

**Files:**

- Modify and commit before worktree creation: `docs/superpowers/specs/2026-08-28-ios-retail-content-import-hardening-design.md`
- Create and commit before worktree creation: `docs/superpowers/plans/2026-08-29-ios-retail-content-import-hardening.md`
- Create and commit before worktree creation: `packaging/ios-runtime-contract-descriptor-shape-v1.json`
- Modify and commit before worktree creation: `docs/superpowers/plans/2026-08-30-ios-personal-ad-hoc-release-closure.md`
- Verify in `PLAN_COMMIT` and restore only at the completed-Task-2 handoff: `docs/superpowers/specs/2026-08-28-ios-about-source-compliance-design.md`
- Inspect only: `.git`, `engine-patches/README.md`, `packaging/verify_engine_patch_delivery.py`
- Create outside Git: `/private/tmp/ios-retail-import-baseline/`

- [ ] **Step 1: Read required execution skills**

Read completely before changing code:

```text
/Users/kevin/.codex/skills/using-git-worktrees/SKILL.md
/Users/kevin/.codex/skills/subagent-driven-development/SKILL.md
/Users/kevin/.codex/skills/test-driven-development/SKILL.md
/Users/kevin/.codex/skills/verification-before-completion/SKILL.md
```

Before creating the implementation branch, verify the documentation handoff commit is exact and the source checkout has no other staged path:

```bash
set -euo pipefail
TASK0_STAGE="$(mktemp -d /private/tmp/nuke-hour-plan-stage.XXXXXX)"
TASK0_EXPECTED="$TASK0_STAGE/expected"
TASK0_ACTUAL="$TASK0_STAGE/actual"
printf '%s\n' \
  docs/superpowers/plans/2026-08-29-ios-retail-content-import-hardening.md \
  docs/superpowers/plans/2026-08-30-ios-personal-ad-hoc-release-closure.md \
  docs/superpowers/specs/2026-08-28-ios-retail-content-import-hardening-design.md \
  packaging/ios-runtime-contract-descriptor-shape-v1.json | \
  LC_ALL=C sort > "$TASK0_EXPECTED"
test -z "$(git diff --cached --name-only --no-renames)"
git add -- \
  docs/superpowers/specs/2026-08-28-ios-retail-content-import-hardening-design.md \
  docs/superpowers/plans/2026-08-29-ios-retail-content-import-hardening.md \
  docs/superpowers/plans/2026-08-30-ios-personal-ad-hoc-release-closure.md \
  packaging/ios-runtime-contract-descriptor-shape-v1.json
git diff --cached --name-only --no-renames | LC_ALL=C sort > "$TASK0_ACTUAL"
cmp -s "$TASK0_EXPECTED" "$TASK0_ACTUAL"
test "$(wc -l < "$TASK0_ACTUAL" | tr -d ' ')" -eq 4
cat "$TASK0_ACTUAL"
git diff --cached --check
git commit -m "Plan iOS retail content hardening"
PLAN_COMMIT="$(git rev-parse HEAD)"
test -n "$PLAN_COMMIT"
```

The staged list must contain exactly the authoritative importer design, this implementation plan, the amended Personal Ad Hoc closure plan, and the authenticated runtime-descriptor golden. The resulting `PLAN_COMMIT` must also contain the already committed Personal-only About/source design at its reviewed bytes. If any required asset is absent, any of the four newly committed paths is still untracked/modified, or any unrelated path is staged, stop before creating or resuming a worktree.

- [ ] **Step 2: Create a clean worktree for a fresh execution only**

Use the worktree skill's preferred location and branch `codex/ios-retail-import-hardening`. Confirm:

```bash
set -euo pipefail
git branch --show-current
git status --short
git log -3 --oneline
```

Expected: the implementation worktree is rooted at `PLAN_COMMIT`, has no tracked or untracked changes, and contains the authoritative importer design, this reviewed plan, and the authenticated descriptor golden.

Because a Git worktree contains only the patched tracked subset under `engine/`, hydrate the rest from a verified official replay before any restore/build. Do not copy the ignored engine tree from the original checkout:

```bash
set -euo pipefail
PROJECT_REPOSITORY=/Users/kevin/Projects/ra2-mac
WORKTREE_ROOT="$PWD"
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
BOOTSTRAP_STAGE="$(mktemp -d /private/tmp/nuke-hour-engine-bootstrap.XXXXXX)"
BOOTSTRAP_ALLOWLIST="$BOOTSTRAP_STAGE/0019.allowlist"
printf '%s\n' \
  OpenRA.Mods.Common/Widgets/Logic/Ingame/IosSupportPowerLayoutPolicy.cs \
  OpenRA.Mods.Common/Widgets/Logic/Ingame/SupportPowerBinLogic.cs \
  OpenRA.Test/IosSupportPowerLayoutPolicyTest.cs > "$BOOTSTRAP_ALLOWLIST"
python3 "$PROJECT_REPOSITORY/packaging/verify_engine_patch_delivery.py" \
  --repository "$PROJECT_REPOSITORY" \
  --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --expected-zip-sha256 ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a \
  --predecessor-start 1 --predecessor-end 18 \
  --allowlist-file "$BOOTSTRAP_ALLOWLIST" \
  --candidate "$PROJECT_REPOSITORY/engine-patches/0019-ios-support-powers-beside-production.patch" \
  --workspace "$PROJECT_REPOSITORY/engine" \
  --output-dir "$BOOTSTRAP_STAGE/verification"
set -- "$BOOTSTRAP_STAGE/verification/fresh-official-extraction"/*
test "$#" -eq 1
test -d "$1"
ditto "$1" "$WORKTREE_ROOT/engine"
test -z "$(git status --short)"
```

The source of hydrated bytes is only the verifier's fresh replay. The final clean-status assertion proves its tracked patched files equal this worktree; ignored official baseline files are now available for restore/build without becoming Git candidates.

- [ ] **Step 2A: Resume this execution from the completed Task 2 delivery**

This is the path used by the current execution. Tasks 1 and 2 already finished on `codex/ios-retail-import-hardening` at immutable commit `6977690ee4aa59e56267b3c9c4ad6a615cf9724c`. Do **not** recreate that worktree, reset it to the planning branch, or merge the planning branch: the two branches diverged after `3ae8af9a3233486d749b248c5ff351e5afebaf83`, and merging the whole planning branch would delete or replace completed Task 1/2 implementation. After the final planning commit is clean, copy only the five authoritative planning assets from `PLAN_COMMIT` into the existing implementation branch and make one single-parent handoff commit whose parent is exactly the Task 2 delivery:

```bash
set -euo pipefail

PLAN_WORKTREE=/private/tmp/ra2-mac-ios-retail-plan-amendment
IMPLEMENTATION_WORKTREE=/private/tmp/ra2-mac-ios-retail-import-hardening
IMPLEMENTATION_PARENT=6977690ee4aa59e56267b3c9c4ad6a615cf9724c
HANDOFF_STAGE="$(mktemp -d /private/tmp/nuke-hour-plan-handoff.XXXXXX)"
ASSET_LIST="$HANDOFF_STAGE/assets"
ACTUAL_LIST="$HANDOFF_STAGE/actual"

printf '%s\n' \
  docs/superpowers/plans/2026-08-29-ios-retail-content-import-hardening.md \
  docs/superpowers/plans/2026-08-30-ios-personal-ad-hoc-release-closure.md \
  docs/superpowers/specs/2026-08-28-ios-about-source-compliance-design.md \
  docs/superpowers/specs/2026-08-28-ios-retail-content-import-hardening-design.md \
  packaging/ios-runtime-contract-descriptor-shape-v1.json > "$ASSET_LIST"

test "$(git -C "$PLAN_WORKTREE" branch --show-current)" = \
  codex/ios-retail-plan-amendment
test -z "$(git -C "$PLAN_WORKTREE" status --porcelain=v1 --untracked-files=all)"
PLAN_COMMIT="$(git -C "$PLAN_WORKTREE" rev-parse HEAD)"
test "$(git -C "$PLAN_WORKTREE" rev-parse "$PLAN_COMMIT^{commit}")" = \
  "$PLAN_COMMIT"

while IFS= read -r asset_path
do
  git -C "$PLAN_WORKTREE" cat-file -e "$PLAN_COMMIT:$asset_path"
done < "$ASSET_LIST"

cd "$IMPLEMENTATION_WORKTREE"
test "$(git branch --show-current)" = codex/ios-retail-import-hardening
test "$(git rev-parse HEAD)" = "$IMPLEMENTATION_PARENT"

# Generated caches are not handoff assets. Move only these two known
# directories to one recoverable quarantine before asserting a clean tree.
GENERATED_QUARANTINE="$(mktemp -d /private/tmp/nuke-hour-generated.XXXXXX)"
if test -d packaging/__pycache__
then
  mv packaging/__pycache__ "$GENERATED_QUARANTINE/packaging.__pycache__"
fi
if test -d packaging/tests/__pycache__
then
  mv packaging/tests/__pycache__ \
    "$GENERATED_QUARANTINE/packaging.tests.__pycache__"
fi
test -z "$(git status --porcelain=v1 --untracked-files=all)"

git restore --source="$PLAN_COMMIT" --staged --worktree -- \
  docs/superpowers/plans/2026-08-29-ios-retail-content-import-hardening.md \
  docs/superpowers/plans/2026-08-30-ios-personal-ad-hoc-release-closure.md \
  docs/superpowers/specs/2026-08-28-ios-about-source-compliance-design.md \
  docs/superpowers/specs/2026-08-28-ios-retail-content-import-hardening-design.md \
  packaging/ios-runtime-contract-descriptor-shape-v1.json

git diff --cached --name-only --no-renames | LC_ALL=C sort > "$ACTUAL_LIST"
cmp -s "$ASSET_LIST" "$ACTUAL_LIST"
test -z "$(git diff --name-only --no-renames)"
test -z "$(git ls-files --others --exclude-standard)"
git diff --cached --check

while IFS= read -r asset_path
do
  test "$(git hash-object "$asset_path")" = \
    "$(git rev-parse "$PLAN_COMMIT:$asset_path")"
  PLAN_SHA="$(git show "$PLAN_COMMIT:$asset_path" | shasum -a 256 | awk '{print $1}')"
  WORKTREE_SHA="$(shasum -a 256 "$asset_path" | awk '{print $1}')"
  test "$WORKTREE_SHA" = "$PLAN_SHA"
  printf '%s  %s\n' "$WORKTREE_SHA" "$asset_path"
done < "$ASSET_LIST"

git commit -m "Refresh iOS retail import execution handoff"
HANDOFF_COMMIT="$(git rev-parse HEAD)"
test "$(git show -s --format=%P "$HANDOFF_COMMIT")" = \
  "$IMPLEMENTATION_PARENT"
git diff-tree --no-commit-id --name-only -r "$HANDOFF_COMMIT" |
  LC_ALL=C sort > "$ACTUAL_LIST"
cmp -s "$ASSET_LIST" "$ACTUAL_LIST"

while IFS= read -r asset_path
do
  test "$(git rev-parse "$HANDOFF_COMMIT:$asset_path")" = \
    "$(git rev-parse "$PLAN_COMMIT:$asset_path")"
done < "$ASSET_LIST"

test -z "$(git status --porcelain=v1 --untracked-files=all)"

# A clean Git index authenticates only tracked patch paths. Rebuild the whole
# ignored engine tree from the pinned official ZIP plus checked-in 0001..0020
# before Task 3 so no stale Task-2-era ignored byte can enter a build.
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
HANDOFF_REPLAY="$(mktemp -d /private/tmp/nuke-hour-handoff-replay.XXXXXX)"
test "$(shasum -a 256 "$OPENRA_OFFICIAL_ZIP" | awk '{print $1}')" = \
  ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a
python3 packaging/engine_workspace.py materialize \
  --repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 20 --workspace "$PWD/engine"
python3 packaging/engine_workspace.py allowlist \
  --patch-id 0020 --output "$HANDOFF_REPLAY/0020.allowlist"
python3 packaging/verify_engine_patch_delivery.py \
  --repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --expected-zip-sha256 ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a \
  --predecessor-start 1 --predecessor-end 19 \
  --allowlist-file "$HANDOFF_REPLAY/0020.allowlist" \
  --candidate engine-patches/0020-ios-retail-archive-envelopes.patch \
  --workspace "$PWD/engine" --output-dir "$HANDOFF_REPLAY/verification"
test -f "$HANDOFF_REPLAY/verification/evidence.json"
test -z "$(git status --porcelain=v1 --untracked-files=all)"
```

The resume commit changes exactly those five planning assets and no Task 1/2 code, patch, test, or delivery file. Generated caches are moved, never deleted or staged; record `GENERATED_QUARANTINE` with the handoff evidence. Record `HANDOFF_COMMIT`, `HANDOFF_REPLAY`, the official ZIP digest, and `verification/evidence.json`; Step 2B starts from that clean commit and freshly materialized complete engine tree. Task 3 starts only after Step 2B and the Step 3 baseline pass. The fresh-worktree path above and this resume path are mutually exclusive.

- [ ] **Step 2B: Apply the reviewed replay-baseline command correction for this execution**

The first baseline run after the five-asset handoff proved three execution facts: a byte-exact official replay correctly retains `engine/VERSION={DEV_VERSION}`; the top-level SDK `utility.sh` therefore cannot be its YAML gate; and a linked Git worktree plus the official x86_64 desktop native payload cannot directly execute the arm64 repository/source-layout tests. The independently reviewed correction changes only this plan and the Personal closure plan: all eight YAML gates invoke the replay Utility directly, and Task 0 establishes one recoverable current-execution test fixture plus a pinned Python environment before Task 3. Before Task 3 code or RED edits, transfer that correction in one exact follow-up whose parent is the five-asset handoff `dd5a5d9b686dd3cd567c6832ff0b7f6882a9977a`:

```bash
set -euo pipefail
PLAN_WORKTREE=/private/tmp/ra2-mac-ios-retail-plan-amendment
IMPLEMENTATION_WORKTREE=/private/tmp/ra2-mac-ios-retail-import-hardening
CORRECTION_PARENT=dd5a5d9b686dd3cd567c6832ff0b7f6882a9977a
CORRECTION_STAGE="$(mktemp -d /private/tmp/nuke-hour-plan-correction.XXXXXX)"
CORRECTION_EXPECTED="$CORRECTION_STAGE/expected.paths"
CORRECTION_ACTUAL="$CORRECTION_STAGE/actual.paths"
printf '%s\n' \
  docs/superpowers/plans/2026-08-29-ios-retail-content-import-hardening.md \
  docs/superpowers/plans/2026-08-30-ios-personal-ad-hoc-release-closure.md \
  > "$CORRECTION_EXPECTED"

test -z "$(git -C "$PLAN_WORKTREE" \
  status --porcelain=v1 --untracked-files=all)"
PLAN_CORRECTION_COMMIT="$(git -C "$PLAN_WORKTREE" rev-parse HEAD)"
cd "$IMPLEMENTATION_WORKTREE"
test "$(git rev-parse HEAD)" = "$CORRECTION_PARENT"
test -z "$(git status --porcelain=v1 --untracked-files=all)"
git restore --source="$PLAN_CORRECTION_COMMIT" --staged --worktree -- \
  docs/superpowers/plans/2026-08-29-ios-retail-content-import-hardening.md \
  docs/superpowers/plans/2026-08-30-ios-personal-ad-hoc-release-closure.md
git diff --cached --name-only --no-renames | LC_ALL=C sort \
  > "$CORRECTION_ACTUAL"
cmp -s "$CORRECTION_EXPECTED" "$CORRECTION_ACTUAL"
test -z "$(git diff --name-only --no-renames)"
test -z "$(git ls-files --others --exclude-standard)"
git diff --cached --check
git commit -m "Correct replay baseline validation commands"
TASK0_CORRECTION_COMMIT="$(git rev-parse HEAD)"
test "$(git show -s --format=%P "$TASK0_CORRECTION_COMMIT")" = \
  "$CORRECTION_PARENT"
git diff-tree --no-commit-id --name-only -r "$TASK0_CORRECTION_COMMIT" \
  | LC_ALL=C sort > "$CORRECTION_ACTUAL"
cmp -s "$CORRECTION_EXPECTED" "$CORRECTION_ACTUAL"
while IFS= read -r asset_path
do
  test "$(git rev-parse "$TASK0_CORRECTION_COMMIT:$asset_path")" = \
    "$(git rev-parse "$PLAN_CORRECTION_COMMIT:$asset_path")"
done < "$CORRECTION_EXPECTED"
test -z "$(git status --porcelain=v1 --untracked-files=all)"
```

Record `PLAN_CORRECTION_COMMIT` and `TASK0_CORRECTION_COMMIT`. This plan-only correction sequence is the only authorized post-handoff planning correction for the current execution; its exact boundary contains no engine, mod, packaging implementation, golden, specification, Task 1/2, or generated file. If executing the baseline itself proves a host-tool command-syntax defect before any Task 3 edit, correct it immediately in a follow-up restricted to these same plan paths, record both follow-up commits, and rerun the affected command from its first side-effect-free check. Task 3 starts only after the correction sequence and the complete baseline gate below.

Task 3 execution later produced behavior evidence for three authoritative-plan defects: one byte-identical RA2 model was incorrectly required as an implementation diff, the public cross-assembly pre-game ownership move was omitted from the locked API, and an acquire-by-name runtime root was incorrectly described as enumerating unrelated bundle extras. One additional correction is therefore explicitly authorized before Task 3 core GREEN. Its commit stages exactly this plan and `docs/superpowers/specs/2026-08-28-ios-retail-content-import-hardening-design.md`, and no implementation, packaging manifest/test, golden, Task 1/2, generated, or delivery file. Record `TASK3_PLAN_CORRECTION_COMMIT`, verify that exact two-path cached boundary and `git diff --cached --check`, and then retain the Task 3 JSON/test count correction as part of the later 54-path implementation commit. This narrow correction supersedes the earlier “only authorized” sentence only for these three evidenced contract fixes; it authorizes no other planning drift.

A subsequent code-quality review and three retained, compiling behavior REDs demonstrated one further omission in the Task 3 cleanup contract: `FileSystem.UnmountAll` and `Manifest.Dispose` can stop at the first throwing child, while `ModData` currently swallows every normal-disposal cleanup fault. One further evidence-driven correction is therefore explicitly authorized before production changes. Its commit again stages exactly this plan and `docs/superpowers/specs/2026-08-28-ios-retail-content-import-hardening-design.md`, and no implementation, packaging manifest/test, golden, Task 1/2, generated, or delivery file. Record `TASK3_TOTAL_CLEANUP_PLAN_CORRECTION_COMMIT`, verify that exact two-path cached boundary and `git diff --cached --check`, and retain the corresponding Task 3 manifest/test count correction in the later 54-path implementation commit. This correction authorizes only detach-first total package cleanup, total/idempotent manifest-module cleanup, and constructor-primary versus normal-disposal exception precedence in the existing rollback phases. It explicitly does not authorize any change to Game drain, lifecycle-fence sequencing, `DrainLifecycleDispatchUntil`, or its untimed-wake contract, and it authorizes no other planning drift.

- [ ] **Step 3: Record baseline tests**

```bash
set -euo pipefail
mkdir -p /private/tmp/ios-retail-import-baseline
TASK0_EVIDENCE="$(mktemp -d /private/tmp/ios-retail-import-baseline/run.XXXXXX)"

# Pin the interpreter used by every packaging test in this execution.
TASK0_PYTHON_ENV=/private/tmp/nuke-hour-task0-python311
TASK0_PYTHON="$TASK0_PYTHON_ENV/bin/python"
if test ! -x "$TASK0_PYTHON"
then
  /Users/kevin/.local/bin/python3.11 -m venv "$TASK0_PYTHON_ENV"
  PIP_DISABLE_PIP_VERSION_CHECK=1 PIP_NO_INPUT=1 \
    "$TASK0_PYTHON" -m pip install --only-binary=:all: --no-deps \
    Pillow==11.3.0 > "$TASK0_EVIDENCE/python-bootstrap.log" 2>&1
fi
test "$(
  "$TASK0_PYTHON" -c \
    'import sys, PIL; print(f"{sys.version_info.major}.{sys.version_info.minor}|{PIL.__version__}")'
)" = "3.11|11.3.0"

env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet restore \
  engine/OpenRA.Test/OpenRA.Test.csproj -m:1 \
  > "$TASK0_EVIDENCE/managed-restore.log" 2>&1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet restore \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -m:1 \
  >> "$TASK0_EVIDENCE/managed-restore.log" 2>&1

# Normalize only the three proven linked-worktree/host test prerequisites.
# Every displaced byte remains recoverable outside Git and every resulting
# path is ignored/untracked; the engine source replay stays byte-exact.
test -f .git
test ! -e engine/mods/ra2
TASK0_FIXTURE="$(mktemp -d /private/tmp/nuke-hour-task0-fixture.XXXXXX)"
TASK0_GITDIR="$(sed -n 's/^gitdir: //p' .git)"
test -n "$TASK0_GITDIR"
test -d "$TASK0_GITDIR"
mv .git "$TASK0_FIXTURE/git-pointer"
ln -s "$TASK0_GITDIR" .git
ln -s "$PWD/mods/ra2" engine/mods/ra2
test -d .git
test -d engine/mods/ra2

TASK0_FREETYPE=/opt/homebrew/opt/freetype/lib/libfreetype.6.dylib
test -f "$TASK0_FREETYPE"
test ! -L "$TASK0_FREETYPE"
xcrun lipo "$TASK0_FREETYPE" -verify_arch arm64
test -f engine/bin/freetype6.dylib
mv engine/bin/freetype6.dylib "$TASK0_FIXTURE/freetype6.x86_64.dylib"
ditto "$TASK0_FREETYPE" engine/bin/freetype6.dylib
xcrun lipo engine/bin/freetype6.dylib -verify_arch arm64
printf '%s\n' "$TASK0_FIXTURE" > /private/tmp/nuke-hour-task0-fixture.path
test -z "$(git status --porcelain=v1 --untracked-files=all)"

MANAGED_TRX="$TASK0_EVIDENCE/managed.trx"
MANAGED_LOG="$TASK0_EVIDENCE/managed.log"
set +e
env DOTNET_CLI_UI_LANGUAGE=en DOTNET_ROLL_FORWARD=Major \
  /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 -v minimal \
  --results-directory "$TASK0_EVIDENCE" \
  --logger 'trx;LogFileName=managed.trx' \
  > "$MANAGED_LOG" 2>&1
MANAGED_RC=$?
set -e
"$TASK0_PYTHON" - "$MANAGED_TRX" "$MANAGED_RC" <<'PY'
import hashlib
import sys
import xml.etree.ElementTree as ET

trx, rc_text = sys.argv[1:]
if int(rc_text) != 1:
    raise SystemExit(f"managed exit code changed: {rc_text}")

ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
root = ET.parse(trx).getroot()
summary = root.find("./t:ResultSummary", ns)
counters = root.find("./t:ResultSummary/t:Counters", ns)
expected_counts = {
    "total": "1662", "executed": "1662", "passed": "1659",
    "failed": "3", "error": "0", "timeout": "0", "aborted": "0",
    "inconclusive": "0", "passedButRunAborted": "0",
    "notRunnable": "0", "notExecuted": "0", "disconnected": "0",
    "warning": "0", "completed": "0", "inProgress": "0",
    "pending": "0",
}
if summary is None or counters is None or summary.get("outcome") != "Failed":
    raise SystemExit("managed result summary changed")
if any(counters.get(key) != value for key, value in expected_counts.items()):
    raise SystemExit(f"managed counters changed: {counters.attrib}")

failed = root.findall(".//t:UnitTestResult[@outcome='Failed']", ns)
expected_names = {
    "CompleteBaseContentIsPublishedWithHashes",
    "FailedImportPreservesPreviousReadyDirectory",
    "StaleStagingIsCleanedBeforeSuccessfulImport",
}
actual_names = {result.get("testName", "") for result in failed}
if actual_names != expected_names:
    raise SystemExit(f"managed failed-test set changed: {sorted(actual_names)}")
fingerprint = hashlib.sha256(
    "".join(name + "\n" for name in sorted(actual_names)).encode()
).hexdigest()
if fingerprint != "761074bf84b9bb5a29736ecdad7c39909f1b090d7abab8890fa6cf1c2d115063":
    raise SystemExit(f"managed failure fingerprint changed: {fingerprint}")
for result in failed:
    node = result.find("./t:Output/t:ErrorInfo/t:Message", ns)
    message = "" if node is None else "".join(node.itertext())
    if "Expected: True" not in message or "But was:  False" not in message:
        raise SystemExit(f"managed failure cause changed: {result.get('testName')}")
print("managed baseline: 1659 pass; exact three stale synthetic-MIX tests retained as RED")
PY

PYTHONDONTWRITEBYTECODE=1 "$TASK0_PYTHON" -m unittest discover \
  -s packaging/tests -p 'test_*.py' -v \
  > "$TASK0_EVIDENCE/python.log" 2>&1
rg -q '^Ran 380 tests in [0-9.]+s$' "$TASK0_EVIDENCE/python.log"
rg -q '^OK \(skipped=4\)$' "$TASK0_EVIDENCE/python.log"
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Utility/OpenRA.Utility.csproj -m:1 \
  /nr:false -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal \
  > "$TASK0_EVIDENCE/yaml-build.log" 2>&1
YAML_REPLAY_ROOT="$PWD"
(
  cd engine
  MOD_SEARCH_PATHS="./mods,$YAML_REPLAY_ROOT/mods" ENGINE_DIR=".." \
    env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet \
    bin/OpenRA.Utility.dll ra2 --check-yaml
) > "$TASK0_EVIDENCE/yaml.log" 2>&1
test -z "$(git status --porcelain=v1 --untracked-files=all)"
printf 'Task 0 evidence: %s\n' "$TASK0_EVIDENCE"
```

The exact three managed failures are retained RED evidence, not accepted passes: the legacy tests still write four-byte text named `.mix`, which Task 2 correctly rejects. Task 3's first test-only action replaces only those inputs with the existing legal synthetic MIX graph and then requires all 1,662 managed tests to pass. Packaging is already 380/380 (four explicit artifact skips) under the pinned interpreter. Keep all logs and displaced fixture bytes under `/private/tmp`; never stage them.

---

### Task 1: Add immutable import models, V1 limits, discovery, and synthetic fixtures

**Files:**

- Create: `packaging/engine_workspace.py`
- Create: `packaging/tests/test_engine_workspace.py`
- Modify: `OpenRA.Mods.RA2/Content/RetailImportModels.cs`
- Modify: `OpenRA.Mods.RA2/Content/RetailContentCatalog.cs`
- Create: `OpenRA.Mods.RA2/Content/IosImportEnvelopePolicyV1.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailSourceDiscovery.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailCandidatePlan.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportProgress.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportSource.cs`
- Create: `engine/OpenRA.Test/SyntheticRetailFixtureBuilder.cs`
- Modify: `engine/OpenRA.Test/RetailContentCatalogTest.cs`
- Create: `engine/OpenRA.Test/RetailSourceDiscoveryTest.cs`
- Create: `engine/OpenRA.Test/RetailCandidatePlanTest.cs`
- Create: `engine/OpenRA.Test/IosImportEnvelopePolicyV1Test.cs`

- [ ] **Step 1: Replace filename-only success tests with RED structural contracts**

Delete the existing assertion that text files named `ra2.mix` and `language.mix` are Ready. Add tests for closed content groups, deterministic discovery order, unsupported container diagnostics, symlink exclusion, duplicate/conflict handling, total planning, and every V1 constant.

Require these core contracts:

```csharp
public enum RetailContentGroup
{
	Base,
	Ra2Music,
	Yuri,
	YuriMusic,
	Maps,
}

public enum RetailGroupValidation
{
	Missing,
	Invalid,
	Unavailable,
	Ready,
}

public enum RetailEditionRecognition
{
	NotApplicable,
	Recognized,
	CompatibleUnrecognized,
}

public enum RetailImportPhase
{
	PreparingSources,
	Discovering,
	Planning,
	Copying,
	VerifyingActiveHashes,
	ValidatingEnvelopes,
	ValidatingArchives,
	ValidatingAudio,
	ConvertingMaps,
	LoadingMaps,
	DownloadingMap,
	ValidatingDownloadedMap,
	SelectingProfile,
	ValidatingMounts,
	CheckingRuntimeResources,
	Committing,
	Recovering,
}

public readonly struct RetailImportProgress : IEquatable<RetailImportProgress>
{
	public RetailImportPhase Phase { get; }
	public RetailContentGroup? Group { get; }
	public int CompletedItems { get; }
	public int? TotalItems { get; }
	public long CompletedBytes { get; }
	public long? TotalBytes { get; }
	public string SafeBasename { get; }
	public RetailImportProgress(
		RetailImportPhase phase,
		RetailContentGroup? group,
		int completedItems,
		int? totalItems,
		long completedBytes,
		long? totalBytes,
		string safeBasename);
	public int? Percentage => TotalBytes is > 0
		? (int)Math.Min(100m, (decimal)CompletedBytes * 100m / TotalBytes.Value)
		: TotalItems is > 0
			? (int)Math.Min(100m, (decimal)CompletedItems * 100m / TotalItems.Value)
			: null;
	public bool Equals(RetailImportProgress other);
	public override bool Equals(object obj);
	public override int GetHashCode();
	public static bool operator ==(
		RetailImportProgress left, RetailImportProgress right);
	public static bool operator !=(
		RetailImportProgress left, RetailImportProgress right);
}

public enum RetailImportErrorCode
{
	Cancelled,
	EmptySelection,
	ExtractInstallationFirst,
	ExecutableSelection,
	ProviderUnavailable,
	ProviderMaterializationRequired,
	SourcePermissionDenied,
	SourceAccessExpired,
	SourceChanged,
	SourceLimitExceeded,
	ValidationTimedOut,
	DuplicateConflict,
	InsufficientSpace,
	CrossVolume,
	CorruptArchive,
	MissingNestedBasePackage,
	InvalidAudio,
	IncompleteBase,
	PartialYuri,
	NoPlayableMap,
	MapConversionFailed,
	UnsafeMap,
	DownloadedMapInvalid,
	DownloadedMapUidMismatch,
	ProfileUnavailable,
	ProfileContextFailure,
	RequiredMountFailure,
	RuntimeResourcesMissing,
	ReceiptMismatch,
	ColdHashMismatch,
	CorruptJournal,
	RecoveryFailed,
	PublishFailed,
	RollbackFailed,
	PendingExpired,
	UpdateRequired,
	ResetRequired,
	LegacyMigrationFailed,
	StoragePolicyFailure,
	SanitizedIoFailure,
}

public enum RetailImportDiagnosticCode
{
	None,
	ProviderNeedsDownload,
	ProviderOffline,
	SecurityScopeDenied,
	DeadlineExceeded,
	LengthMismatch,
	HashMismatch,
	MalformedEnvelope,
	CorruptMapSource,
	UnsupportedMapFeature,
	InvalidSpawnLayout,
	ConversionOutputFailure,
	ArithmeticOverflow,
	MissingRequiredArchive,
	InvalidMapPackage,
	ProfileMetadataMismatch,
	PointerMismatch,
	JournalInvalid,
	GenerationInvalid,
	InsufficientCapacity,
}

public sealed record RetailImportFailure(
	RetailImportErrorCode Code,
	RetailContentGroup? Group,
	string SafeBasename,
	RetailImportDiagnosticCode Diagnostic);

public sealed record RetailGroupStatus(
	RetailGroupValidation Validation,
	RetailEditionRecognition Recognition,
	RetailImportDiagnosticCode Diagnostic);

public enum RetailSourceKind
{
	DirectFile,
	DirectoryDiscoveredFile,
}

public sealed record RetailCandidatePlan(
	ImmutableArray<RetailPlannedCandidate> Candidates,
	long KnownSourceBytes,
	bool HasUnknownLengthSource)
{
	public static RetailOperationResult<RetailCandidatePlan> Create(
		ImmutableArray<RetailSourceEntry> entries);
}

public sealed record RetailPlannedCandidate(
	RetailSourceId SourceId,
	string SafeBasename,
	RetailSourceKind Kind,
	RetailContentGroup Group,
	long? Length);

public sealed record StagedRetailSource(
	RetailSourceId SourceId,
	string SafeBasename,
	string CanonicalStagedRelativePath,
	long Length,
	string Sha256);

public sealed record RetailStagedPlan(
	RetailCandidatePlan CandidatePlan,
	ImmutableArray<StagedRetailSource> Sources,
	long TotalBytes)
{
	public static RetailOperationResult<RetailStagedPlan> Seal(
		RetailCandidatePlan plan,
		ImmutableArray<StagedRetailSource> sources);
}

public readonly struct RetailSourceId : IEquatable<RetailSourceId>
{
	public int RootIndex { get; }
	public string CanonicalRelativePath { get; }
	public RetailSourceId(int rootIndex, string canonicalRelativePath);
	public bool Equals(RetailSourceId other);
	public override bool Equals(object obj);
	public override int GetHashCode();
	public static bool operator ==(RetailSourceId left, RetailSourceId right);
	public static bool operator !=(RetailSourceId left, RetailSourceId right);
}

public readonly struct RetailStagingTarget : IEquatable<RetailStagingTarget>
{
	public string CanonicalRelativePath { get; }
	public RetailStagingTarget(string canonicalRelativePath);
	public bool Equals(RetailStagingTarget other);
	public override bool Equals(object obj);
	public override int GetHashCode();
	public static bool operator ==(
		RetailStagingTarget left, RetailStagingTarget right);
	public static bool operator !=(
		RetailStagingTarget left, RetailStagingTarget right);
}

public sealed record RetailSourceEntry(
	RetailSourceId Id,
	string Basename,
	RetailSourceKind Kind,
	long? Length,
	bool IsSymbolicLink);

public enum RetailSourceReadKind { Entry, Complete }

public sealed record RetailSourceRead
{
	private RetailSourceRead(
		RetailSourceReadKind kind,
		RetailSourceEntry entry);
	public RetailSourceReadKind Kind { get; }
	public RetailSourceEntry Entry { get; }
	public static RetailSourceRead Item(RetailSourceEntry entry);
	public static RetailSourceRead Complete();
}

public sealed class RetailOperationResult<T>
{
	private RetailOperationResult(
		T value,
		RetailImportFailure failure,
		bool succeeded);
	public bool Succeeded { get; }
	public T Value { get; }
	public RetailImportFailure Failure { get; }
	public static RetailOperationResult<T> Success(T value);
	public static RetailOperationResult<T> Failed(RetailImportFailure failure);
}

public sealed class RetailOperationResult
{
	private RetailOperationResult(
		RetailImportFailure failure,
		bool succeeded);
	public bool Succeeded { get; }
	public RetailImportFailure Failure { get; }
	public static RetailOperationResult Success();
	public static RetailOperationResult Failed(RetailImportFailure failure);
}

public sealed class RetailDiscoveryBudget
{
	internal RetailDiscoveryBudget(
		int maximumDepth,
		int maximumEntries,
		long maximumKnownBytes,
		IRetailMonotonicClock clock,
		RetailValidationDeadline deadline);
	public int AcceptedEntries { get; }
	public long AcceptedKnownBytes { get; }
	public RetailOperationResult TryEnterDirectory(int depth);
	public RetailOperationResult TryAcceptEntry(int depth, long? apparentBytes);
	public RetailOperationResult Checkpoint();
}

public interface IRetailSourceEnumeration : IAsyncDisposable
{
	ValueTask<RetailOperationResult<RetailSourceRead>> ReadNextAsync(
		CancellationToken cancellationToken);
}

public interface IRetailImportSourceSession : IAsyncDisposable
{
	Task<RetailOperationResult<IRetailSourceEnumeration>> OpenEnumerationAsync(
		RetailDiscoveryBudget budget,
		CancellationToken cancellationToken);
	Task<RetailOperationResult<StagedRetailSource>> CopyToAsync(
		RetailSourceId id,
		RetailStagingTarget destination,
		IProgress<RetailImportProgress> progress,
		CancellationToken cancellationToken);
}

public static class RetailSourceDiscovery
{
	public static Task<RetailOperationResult<RetailCandidatePlan>> DiscoverAsync(
		IRetailImportSourceSession sources,
		IRetailMonotonicClock clock,
		RetailValidationDeadline deadline,
		CancellationToken cancellationToken);
}

public interface IRetailMonotonicClock
{
	long GetTimestamp();
	TimeSpan GetElapsedTime(long startTimestamp, long endTimestamp);
}

public interface IRetailPersistentTimeProvider
{
	DateTimeOffset GetUtcNow();
}

public readonly struct RetailValidationDeadline :
	IEquatable<RetailValidationDeadline>
{
	readonly IRetailMonotonicClock owningClock;
	public long StartTimestamp { get; }
	public TimeSpan Budget { get; }
	private RetailValidationDeadline(
		IRetailMonotonicClock owningClock,
		long startTimestamp,
		TimeSpan budget);
	public static RetailValidationDeadline Start(
		IRetailMonotonicClock clock, TimeSpan budget);
	public bool MatchesClock(IRetailMonotonicClock clock);
	public TimeSpan Remaining(IRetailMonotonicClock clock);
	public void ThrowIfExpired(IRetailMonotonicClock clock);
	public bool Equals(RetailValidationDeadline other);
	public override bool Equals(object obj);
	public override int GetHashCode();
	public static bool operator ==(
		RetailValidationDeadline left, RetailValidationDeadline right);
	public static bool operator !=(
		RetailValidationDeadline left, RetailValidationDeadline right);
}
```

`Percentage` is derived only from a known positive byte denominator or, when bytes are not meaningful, a known positive item denominator. Provider preparation, discovery, structure/conversion/profile/mount/runtime checks, committing, and recovery remain indeterminate. `RetailOperationResult<T>` has exactly one state: successful with a non-null value, or failed with one immutable `RetailImportFailure`; its constructor is private and its factories reject a null value/failure. Provider, coordinated-read, and storage seams map platform exceptions into this closed result before returning; only cooperative cancellation may propagate `OperationCanceledException`, and raw `NSError`, `IOException`, exception text, paths, or provider strings never cross the coordinator/UI/log boundary. Every record constructor defensively copies and ordinal-sorts collection inputs into `ImmutableArray`/`ImmutableDictionary`, rejects null/duplicate/noncanonical entries, and is proven unchanged after the caller mutates its original collection. `RetailStagingTarget` accepts one canonical relative path beneath the session-bound staging root and rejects absolute, empty, `.`, `..`, alternate-separator, NUL, or symlink-component targets. Validation deadlines use only the injected monotonic clock, checked timestamp arithmetic, and never wall time. Persisted seven-day pending expiry uses the separate UTC provider and a canonical UTC instant; a future seal timestamp, wall-clock rollback, invalid offset, or ambiguous encoding fails closed without touching the active generation. Introduce these types alongside the characterized legacy desktop request/result for now; iOS code must use only the new session/typed-failure path. Task 14 removes the legacy raw-path `RetailImportRequest`, free-form `Message`, and untyped `Hashes` fields atomically with the final test migration, so every final source access flows through `IRetailImportSourceSession` without breaking the intervening full suites.

The project remains C# 9 with nullable annotations disabled. `RetailImportProgress`, `RetailSourceId`, `RetailStagingTarget`, and `RetailValidationDeadline` are handwritten `readonly struct : IEquatable<T>` values with the shown explicit/private constructors, get-only properties, complete ordinal/null-safe equality and hash, and `==`/`!=`; no positional value record, synthesized `with`, or C# 10 syntax is permitted. Their default values remain equality/hash-safe but are rejected at discovery/progress/staging/deadline operation boundaries. `RetailValidationDeadline.Start` is its only nondefault factory: it captures the non-null clock by reference and checked start timestamp/budget; `MatchesClock` uses `ReferenceEquals`, equality/hash include that clock identity plus timestamp/budget, and `Remaining`/`ThrowIfExpired` reject any different clock before reading it. Every typed operation that accepts both values checks `MatchesClock` at entry and maps a mismatch to its closed invalid-context failure; therefore a caller cannot reuse the timestamp against a fresh or wall-clock adapter. Plain reference properties intentionally encode closed nullable runtime states without `?`: progress/failure `SafeBasename` is null only when no concrete source item is implicated; `RetailSourceRead.Entry` is non-null exactly for `Kind=Entry` and null exactly for `Complete`; a successful operation result has its value and null `Failure`, while a failed result has default/null value and one non-null `Failure`. Private constructors/factories validate those exact tuples, and tests exercise every forbidden null/non-null combination without enabling nullable annotations.

`RetailSourceDiscovery` creates the only `RetailDiscoveryBudget` from the exact V1 depth/entry/apparent-byte limits, injected monotonic clock, and already-started ten-minute deadline, then passes it into `OpenEnumerationAsync`. Enumeration is pull-based and yields exactly one entry or `Complete`; it may not materialize a provider directory into an array. The production cursor calls `TryEnterDirectory` *before* asking a provider to enumerate that directory, calls `TryAcceptEntry` before allocating/yielding each item's metadata, and calls `Checkpoint` inside provider/materialization loops. Once a limit/deadline failure occurs the cursor becomes inert, closes provider handles, and returns that same typed failure; it never fetches item `limit + 1`, descends below the accepted depth, or continues after timeout. The neutral discovery loop independently checks ordering/canonicalization and refuses `Complete` if budget/accounting invariants disagree. `RetailSourceRead` factories enforce exactly one valid state. Fakes count underlying provider calls and prove immediate stop at depth, entries, apparent bytes, cancellation, and deadline boundaries—including a provider that could otherwise produce millions of children—so rejection never happens only after an unbounded array was built.

- [ ] **Step 2: Run RED**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailContentCatalogTest|FullyQualifiedName~RetailSourceDiscoveryTest|FullyQualifiedName~RetailCandidatePlanTest|FullyQualifiedName~IosImportEnvelopePolicyV1Test' \
  -v minimal
python3 -m unittest packaging.tests.test_engine_workspace -v
```

Expected: compile failures for the new policy/discovery/plan types, failures proving text masquerading as MIX is no longer accepted, and a missing helper/subcommand failure from the new Python contract.

- [ ] **Step 3: Implement bounded discovery and planning**

`RetailSourceDiscovery.DiscoverAsync` accepts only the session, clock, and 10-minute deadline and returns `RetailOperationResult<RetailCandidatePlan>`; it uses the session's typed enumeration result and never catches-and-relabels cancellation. It produces canonical `(rootIndex, relativePath, basename, kind, length?)` records, uses ordinal ordering, does not follow symlinks, and stops at the exact depth/entry/apparent-byte or deadline limits. Recognize only the six retail archive basenames and supported map extensions. `RetailSourceKind` preserves whether a file was directly selected or discovered under a selected directory: a directly selected executable/unsupported container returns the corresponding typed error, while the same unrelated file discovered inside a directory is ignored without copying; supported files follow the same validation path in both modes. Lock this complete behavior and failure-mapping matrix in tests. Return typed `ExtractInstallationFirst`, `ExecutableSelection`, provider/materialization/permission/timeout, and source-changed results; ignore unrelated directory entries without copying. `RetailCandidatePlan.Create` and `RetailStagedPlan.Seal` use the exact operation-result factories above, so no exception text becomes a data model.

`RetailCandidatePlan.Create` classifies Base/Yuri/music/maps, deduplicates only repeated entries with the exact same stable `RetailSourceId`, rejects conflicting metadata for one source identity, uses checked 64-bit sums, and never claims content equality before bytes are copied. `RetailStagedPlan.Seal` runs only after every selected source has been copied and SHA-256 calculated. For the six singleton retail archive names it deduplicates equal canonical basename+hash and rejects the same basename with a different hash. Map inputs are content candidates: identical hashes deduplicate, while the same basename with different bytes remains distinct until bounded conversion/validation derives canonical map UIDs; only the same validated UID with conflicting bytes is a typed duplicate conflict. Tests cover two unrelated `map01.map` files, exact duplicate maps, and same-UID conflicts. Task 6 defines the later `RetailCandidateCatalog`/`RetailReadinessResult` only after profile/runtime-contract and receipt types exist.

- [ ] **Step 4: Build only synthetic fixtures**

`SyntheticRetailFixtureBuilder` writes minimal project-generated binary MIX/IDX/BAG/map structures through explicit byte writers. It must contain a sentinel string such as `SYNTHETIC-NO-EA-DATA`, generate no real retail filename hashes beyond the public format contract, and never read a local installation.

`packaging/engine_workspace.py` provides two fail-closed subcommands used by every later clean clone/patch task:

```text
engine_workspace.py materialize --repository ROOT --official-zip ZIP \
  --patch-end N --workspace ENGINE_DIR [--overlay-allowlist FILE]
engine_workspace.py candidate --repository ROOT --official-zip ZIP \
  --patch-end N --workspace ENGINE_DIR --allowlist FILE --output PATCH
engine_workspace.py allowlist --patch-id 0020|0021|0022|0023|0024|0025|0026|0027 \
  --output FILE
```

The helper validates the pinned ZIP SHA, numeric exact predecessor range, safe extraction, exact 0007 diagnostic tuple, no other fuzz/offset/reject/orig, regular non-symlink paths, and byte-sorted unique allowlists. `allowlist` owns the executable exact path sets mirrored by this plan, writes atomically, and tests lock every path/count plus `LC_ALL=C` byte order. `materialize` first captures only allowlisted tracked candidate bytes, proves every other tracked engine path equals the strict replay, fills the full engine tree from that replay, then restores and verifies the allowlisted overlay; it never copies ignored engine bytes from another checkout. `candidate` creates fresh `a` and `b`, overlays only allowlisted workspace bytes into `b`, runs `diff -ruN` with exact exit `1`, validates exact paired headers, and atomically writes the patch. Tests cover traversal, symlink, missing numeric predecessor, outside drift, untracked/ignored pollution, duplicate allowlist entries, creation/deletion, wrong ZIP, wrong 0007 diagnostics, overlay byte identity, allowlist drift, and no partial output.

- [ ] **Step 5: Run GREEN and commit**

Repeat the focused command, then:

```bash
set -euo pipefail
python3 -m unittest packaging.tests.test_engine_workspace -v
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore -m:1
git diff --check
```

Stage only the fourteen listed paths and commit:

```bash
set -euo pipefail
git commit -m "Model bounded iOS retail imports"
```

The new/modified `engine/OpenRA.Test` paths join the `0020` allowlist in Task 2; do not generate a partial patch yet.

---

### Task 2: Harden ZIP, PNG, MIX, IDX, and BAG boundaries and deliver `0020`

**Outer files:**

- Create: `OpenRA.Mods.RA2/Content/RetailArchiveGraphValidator.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailFilenameCatalog.cs`
- Modify: `OpenRA.Mods.RA2/Content/RetailContentCatalog.cs`
- Modify: `OpenRA.Mods.RA2/FileSystem/BagFile.cs`

**Exact `0020` engine allowlist:**

- Create: `engine/OpenRA.Game/FileSystem/PackageEnvelopePolicy.cs`
- Create: `engine/OpenRA.Game/FileSystem/PackageReadContext.cs`
- Create: `engine/OpenRA.Game/FileSystem/BoundedZipEnvelope.cs`
- Modify: `engine/OpenRA.Game/FileSystem/IPackage.cs`
- Modify: `engine/OpenRA.Game/FileSystem/FileSystem.cs`
- Modify: `engine/OpenRA.Game/FileSystem/ZipFile.cs`
- Modify: `engine/OpenRA.Game/FileFormats/Png.cs`
- Modify: `engine/OpenRA.Game/Primitives/SegmentStream.cs`
- Create: `engine/OpenRA.Mods.Cnc/FileSystem/MixEnvelopeValidator.cs`
- Modify: `engine/OpenRA.Mods.Cnc/FileSystem/MixFile.cs`
- Create: `engine/OpenRA.Mods.Cnc/FileFormats/IdxBagEnvelopeValidator.cs`
- Modify: `engine/OpenRA.Mods.Cnc/FileFormats/IdxReader.cs`
- Modify: `engine/OpenRA.Mods.Cnc/FileFormats/XccLocalDatabase.cs`
- Modify: `engine/OpenRA.Mods.Cnc/FileFormats/XccGlobalDatabase.cs`
- Create: `engine/OpenRA.Test/ZipEnvelopeTest.cs`
- Create: `engine/OpenRA.Test/MixEnvelopeTest.cs`
- Create: `engine/OpenRA.Test/IdxBagEnvelopeTest.cs`
- Modify: `engine/OpenRA.Test/OpenRA.Game/PngTest.cs`
- Create: `engine/OpenRA.Test/RetailArchiveGraphValidatorTest.cs`
- Modify again: `engine/OpenRA.Test/RetailContentCatalogTest.cs`
- Modify again: `engine/OpenRA.Test/RetailSourceDiscoveryTest.cs`
- Modify again: `engine/OpenRA.Test/RetailCandidatePlanTest.cs`
- Modify again: `engine/OpenRA.Test/IosImportEnvelopePolicyV1Test.cs`
- Modify again: `engine/OpenRA.Test/SyntheticRetailFixtureBuilder.cs`

**Delivery files:**

- Create: `engine-patches/0020-ios-retail-archive-envelopes.patch`
- Modify: `engine-patches/README.md`
- Modify: `packaging/tests/test_verify_engine_patch_delivery.py`

**Pinned read-only design input (inspect, never modify or stage in Task 3):**

- `packaging/ios-runtime-contract-descriptor-shape-v1.json`

- [ ] **Step 1: Write RED allocation/structure tests**

Require engine APIs that take immutable limits instead of referencing the RA2 assembly:

```csharp
public enum PackageEnvelopeFailure
{
	Malformed,
	LimitExceeded,
	AggregateLimitExceeded,
	ArithmeticOverflow,
}

public sealed class PackageEnvelopeException : IOException
{
	public PackageEnvelopeException(
		PackageEnvelopeFailure failure,
		string message);
	public PackageEnvelopeException(
		PackageEnvelopeFailure failure,
		string message,
		Exception innerException);
	public PackageEnvelopeFailure Failure { get; }
}

public sealed record PackageEnvelopePolicy(
	ZipEnvelopeLimits Zip,
	PngReadLimits Png,
	YamlEnvelopeLimits Yaml,
	MixEnvelopeLimits Mix,
	IdxBagEnvelopeLimits Audio);

public sealed record ZipEnvelopeLimits(
	int MaximumPathUtf8Bytes,
	int MaximumPathDepth,
	int MaximumEntries,
	long MaximumCompressedBytes,
	long MaximumExpandedEntryBytes,
	long MaximumExpandedAggregateBytes,
	int MaximumExpansionRatio);

public sealed record PngReadLimits(
	int MaximumEdge,
	long MaximumPixels,
	long MaximumDecodedBytes,
	int MaximumChunks,
	long MaximumIdatBytes,
	int MaximumInflateRatio)
{
	public static PngReadLimits TrustedEngineDefault { get; } = new(
		4_096,
		16_777_216,
		64L << 20,
		4_096,
		32L << 20,
		100);
}

public sealed record YamlEnvelopeLimits(
	long MaximumBytes,
	int MaximumNodes,
	int MaximumDepth,
	int MaximumLineBytes,
	int MaximumScalarBytes);

public sealed record MixEnvelopeLimits(
	int MaximumDepth,
	int MaximumEntriesPerArchive,
	int MaximumEntriesAggregate,
	long MaximumIndexBytes,
	long MaximumLocalDatabaseBytes,
	int MaximumLocalDatabaseNames,
	long MaximumSegmentBytes,
	long MaximumAddressedBytes);

public sealed record IdxBagEnvelopeLimits(
	long MaximumIdxBytes,
	int MaximumSoundRecords,
	int ExactNameBytes,
	long MaximumBagBytes,
	long MaximumSoundSpanBytes);

public sealed class PackageReadContext
{
	public PackageReadContext(
		CancellationToken cancellationToken,
		IPackageReadDeadline deadline,
		int maximumDepth,
		int maximumEntries,
		long maximumAddressedBytes);
	public PackageReadContext(
		CancellationToken cancellationToken,
		IPackageReadDeadline deadline,
		int maximumDepth,
		int maximumEntries,
		long maximumAddressedBytes,
		PackageEnvelopePolicy envelopePolicy);
	public CancellationToken CancellationToken { get; }
	public IPackageReadDeadline Deadline { get; }
	public PackageEnvelopePolicy EnvelopePolicy { get; }
	public int Depth { get; }
	public int RemainingEntries { get; }
	public long RemainingAddressedBytes { get; }
	public void ThrowIfCancelledOrExpired();
	public PackageReadContext Descend(int childEntries, long childAddressedBytes);
	public bool SharesLedger(PackageReadContext other);
}

public interface IBoundedPackageLoader : IPackageLoader
{
	bool TryParsePackage(
		Stream stream,
		string filename,
		FileSystem context,
		PackageReadContext readContext,
		out IReadOnlyPackage package);
}

public static class BoundedZipEnvelope
{
	public static ValidatedZipEnvelope Validate(
		Stream ownedPinnedStream,
		ZipEnvelopeLimits limits,
		PackageReadContext context);
}

public sealed class ValidatedZipEnvelope : IDisposable
{
	public ImmutableArray<ValidatedZipEntry> Entries { get; }
	public IReadOnlyPackage OpenPackage(PackageReadContext context);
	public void Dispose();
}

public sealed record ValidatedZipEntry(
	string CanonicalName,
	long DataOffset,
	long CompressedLength,
	long ExpandedLength,
	ushort CompressionMethod);

public static class MixEnvelopeValidator
{
	public static ValidatedMixIndex Validate(
		Stream ownedPinnedStream,
		MixEnvelopeLimits limits,
		PackageHashType hashType,
		IMixFilenameResolver filenameResolver,
		PackageReadContext context);
}

public interface IMixFilenameResolver
{
	bool TryResolve(
		uint hash,
		PackageHashType hashType,
		out string canonicalName);
}

public class MixLoader : IBoundedPackageLoader
{
	public MixLoader();
	public MixLoader(
		PackageHashType boundedHashType,
		IMixFilenameResolver boundedFilenameResolver);
}

public interface IValidatedMixPackage : IReadOnlyPackage
{
	ImmutableArray<ValidatedMixEntry> Entries { get; }
	ValidatedMixEntrySource OpenEntrySource(uint hash);
}

public sealed class ValidatedMixIndex : IDisposable
{
	public ImmutableArray<ValidatedMixEntry> Entries { get; }
	public Stream OpenEntry(uint hash, PackageReadContext context);
	public ValidatedMixEntrySource OpenEntrySource(
		uint hash,
		PackageReadContext context);
	public void Dispose();
}

public sealed class ValidatedMixEntrySource : IDisposable
{
	public long Length { get; }
	public Stream OpenRead();
	public void Dispose();
}

public sealed record ValidatedMixEntry(
	uint Hash,
	string CanonicalName,
	long DataOffset,
	long Length);

public static class IdxBagEnvelopeValidator
{
	public static ValidatedIdxBagIndex Validate(
		ValidatedMixEntrySource ownedIdx,
		ValidatedMixEntrySource ownedBag,
		IdxBagEnvelopeLimits limits,
		PackageReadContext context);
}

public enum IdxBagEncoding
{
	Pcm16,
	ImaAdpcm,
}

public sealed record ValidatedIdxBagEntry(
	string CanonicalName,
	long Offset,
	long Length,
	uint SampleRate,
	uint Flags,
	uint ChunkSize,
	int Channels,
	IdxBagEncoding Encoding,
	ushort SamplesPerBlock,
	uint AverageBytesPerSecond);

public sealed class ValidatedIdxBagIndex : IDisposable
{
	public ImmutableArray<ValidatedIdxBagEntry> Entries { get; }
	public IReadOnlyPackage OpenPackage(
		string canonicalName,
		PackageReadContext context);
	public void Dispose();
}

public interface IPackageReadDeadline
{
	void ThrowIfExpired();
}
```

`ValidatedZipEntry` and `ValidatedMixEntry` are immutable checked-offset/checked-length records with canonical normalized names or hashes; neither exposes a raw backing path. Passing `ownedPinnedStream` transfers ownership at call entry: failure disposes it, while success transfers it to the returned envelope/index. `OpenPackage`, `OpenEntry`, and `OpenEntrySource` use the already parsed table and the same owned stream identity; they never reopen a path, copy the complete segment, or accept a replacement stream/context ledger. `ValidatedMixEntrySource` has no public constructor and contains only an immutable checked range plus one reference to the index's ref-counted pinned stream owner. Each returned package/source/entry stream acquires an independent reference and logical position; disposing the envelope/index/source prevents new opens but does not invalidate an already-open child, and the final reference closes the backing stream exactly once. `PackageReadContext.Descend` uses checked subtraction, rejects negative charges and limit overruns before allocating, and returns a child with immutable increased depth that shares the exact same thread-safe monotonic entry/addressed-byte ledger, cancellation token, deadline, and `EnvelopePolicy` reference. Exceeding maximum nesting depth is a local structural `LimitExceeded`, while exhausting the shared remaining entry or addressed-byte ledger is the distinct fatal `AggregateLimitExceeded`; the graph maps only the latter to top-level `SourceLimitExceeded`, never by exception text. `SharesLedger` is the only public identity query and reveals no counters or authority. Sibling charges are visible through both parents and children, are atomic, are never refunded, and an `OpenPackage`/`OpenEntry` call rejects a context from another ledger. When `EnvelopePolicy` is non-null, ZIP/MIX/IDX-BAG validators require `ReferenceEquals` between the supplied limits and the matching policy member before reading any byte; a value-equal replacement is still substitution and fails. The original no-policy constructor exists only for direct validators that receive explicit limits and for trusted compatibility tests; `FileSystem`'s untrusted bounded overload and every nested `IBoundedPackageLoader` reject a null policy before parsing. The bounded loader follows the existing ownership rule: success transfers the stream to the package; failure restores its initial position and retains caller ownership. RED tests cover envelope-first disposal, child-first disposal, concurrent child reads with independent positions, local depth `LimitExceeded` versus shared entries/bytes `AggregateLimitExceeded`, failure/cancellation, attempted context/policy/value-equal-limits substitution before reads, exact final close count, and zero leaked streams.

RED coverage must include:

- ZIP unknown/negative/inconsistent sizes, duplicate normalized paths, traversal, directory/symlink/special entries, entry and aggregate expansion, and ratio limits;
- PNG invalid signature/IHDR, 4,096 edge, pixel/stride multiplication overflow, chunk/IDAT limits, secondary inflate ratio, truncation, and decoded-byte limit before allocation;
- MIX truncated/encrypted headers, index arithmetic, nesting/entry/addressed-byte budgets, duplicate hash/name, ordinary 512 MiB segment, the complete sparse `language.mix -> audio.mix -> audio.bag` chain, 384 MiB BAG stream, and every `+1` case;
- IDX negative/huge count, checked `12 + count * 36`, invalid/duplicate 16-byte ASCII names, every legal and illegal flag/rate/chunk combination frozen below, and `offset + length <= bag.Length`;
- runtime loaders surface typed corruption instead of swallowing `OverflowException`, `EndOfStreamException`, or guarded allocation failures.

- [ ] **Step 2: Run RED**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~ZipEnvelopeTest|FullyQualifiedName~PngTest|FullyQualifiedName~MixEnvelopeTest|FullyQualifiedName~IdxBagEnvelopeTest|FullyQualifiedName~RetailArchiveGraphValidatorTest' \
  -v minimal
```

Expected: missing envelope types plus a behavior failure proving that the current shallow loader/catalog path accepts at least one structurally malformed synthetic MIX fixture. Task 1 already rejects filename-only `ra2.mix`/`language.mix`, so filename-only rejection is characterization evidence, not Task 2 RED evidence. Add one compilable malformed-MIX/PNG behavior RED before adding missing-symbol contract tests; otherwise a compile failure must not be presented as proof that the existing unsafe behavior was exercised. The existing `PngConstructor_CompressionMethodNotSupported_ThrowsInvalidDataException` method must receive `[Test]` before it is counted as RED/GREEN evidence.

- [ ] **Step 3: Implement preflight-before-loader contracts**

`BoundedZipEnvelope` parses the central directory with checked 64-bit arithmetic before `ZipFileLoader` sees the stream. `PackageReadContext` carries one cancellation token, an injected `IPackageReadDeadline`, one immutable non-null `PackageEnvelopePolicy` for untrusted package graphs, and a single shared depth/entry/addressed-byte ledger through every nested loader. The RA2 adapter checks the earlier of a ten-minute per-root archive deadline and the already-started enclosing 90-minute validation deadline; neither deadline, policy, nor aggregate ledger is refreshed for a child. Every header/index/name/entry/hash loop calls both cancellation and deadline checks. Add an opt-in `IBoundedPackageLoader : IPackageLoader` interface rather than changing the existing `IPackageLoader` method implemented by unrelated desktop loaders. An untrusted context accepts only `IBoundedPackageLoader`; the trusted desktop overload preserves every existing loader. `FileSystem.TryParsePackage` requires the non-null policy, passes the same context identity into nested MIX/BAG/ZIP parsing, and each bounded package retains that context rather than extending `IReadOnlyPackage` with a replacement-context method. `SegmentStream` gains checked long offset/count support backed by one ref-counted pinned stream owner and synchronized absolute reads. A validated ZIP envelope binds its parsed index to that same pinned identity; `Open` must not reparse through the legacy loader or permit a stream swap. Fake-deadline tests prove timeout before allocation and no partially validated handle escapes.

`IMixFilenameResolver` is a narrow Cnc-owned, path-free mapping seam. The RA2 filename catalog implements it; the MIX validator supplies the explicit existing `PackageHashType`, validates every returned name as canonical, recomputes its hash under that same type, and rejects resolver substitution, conflicting names, or duplicate resolved identities. A false result leaves an unknown hash opaque. The resolver conveys no stream, path, package, or root authority. RA2/YR graph parsing always configures `MixLoader(PackageHashType.CRC32, exactCatalogResolver)`; its parameterless constructor is trusted legacy-only. The bounded loader rejects absent configuration, captures that exact hash type/resolver plus the creating `FileSystem`, and returns an `IReadOnlyPackage` that also implements the Cnc-owned `IValidatedMixPackage` capability. That capability exposes only immutable validated entries and `OpenEntrySource(hash)` over the exact retained index and `PackageReadContext`; it accepts no stream, path, resolver, `FileSystem`, or replacement context. The RA2 graph requires this capability before consuming archive-local audio/maps, and a missing capability is typed corruption rather than permission to call raw `GetStream` or revalidate the MIX. The returned package otherwise rejects a replacement `FileSystem` before opening a nested entry. Thus every nested MIX stays on the same resolver/configuration without putting a Cnc type into Game's `PackageReadContext`. Local-database overlay remains solely in the RA2 graph: it inspects that exact capability's immutable hash set, enforces the four map suffixes and static-name/hash collision rules, constructs one exact hash-to-canonical-name mapping used only for those already-retained entries, and never reparses or substitutes the package; generic Cnc code never treats arbitrary local-database names as authorized. RED tests require every configured bounded `MixLoader` result to implement the capability and require a non-capable package at that boundary to map to typed corruption before any raw-entry open.

`ValidatedMixEntry.CanonicalName` is a plain reference under disabled nullable annotations: it is null exactly for an opaque hash the resolver did not recognize and non-null only after canonical name/hash validation. Its constructor and consumers enforce that closed state and never treat null as an empty-name sentinel; tests cover anonymous and resolved entries explicitly.

Archive-local `audio.idx`/`audio.bag` pairing is performed by the RA2 graph over two `ValidatedMixEntrySource` capabilities from the same validated MIX index and ledger rather than by teaching the generic one-stream loader to search a filesystem. `IdxBagEnvelopeValidator.Validate` takes ownership of both sources at call entry, rejects the same object twice, wrong canonical leaf names, different parent-index tokens, different ledgers, substituted Audio limits, or a raw/replacement stream before parsing, and closes both exactly once on every failure. After parsing, the IDX source is released; success transfers the BAG pin, immutable checked entries, and exact ledger into `ValidatedIdxBagIndex`. `OpenPackage("audio.bag", sameContext)` is the only public consumption path used by `AudioBagLoader.OpenValidated`; it rejects another name, ledger, or policy before opening. Disposing the index prevents new packages, but an already-open package or WAV stream owns an independent BAG reference and remains readable; final release closes BAG exactly once. No raw-stream IDX/BAG validation overload exists.

#### PNG delivery boundary

`Png(Stream, PngReadLimits)` is the one shared parsing implementation and rejects impossible allocation before creating `Data`. The finite `PngReadLimits.TrustedEngineDefault` shown above is exactly 4,096 edge, 16,777,216 pixels, 64 MiB decoded bytes, 4,096 chunks, 32 MiB aggregate IDAT, and 100:1 secondary inflate ratio. The characterized legacy `Png(Stream)` constructor remains for bundled/desktop callers but delegates only to that finite default. Signature, IHDR, dimensions, checked pixel/stride/decoded-size arithmetic, chunk count, aggregate IDAT, compressed-to-decoded ratio, truncation, and allocation checks live in the shared explicit implementation. Existing byte-array/output constructors use the same checked dimension/allocation helpers where applicable; no constructor retains unchecked `width * height * stride`.

The RA2 Task 2 adapter constructs an explicit `PngReadLimits` from all six `IosImportEnvelopePolicyV1` PNG constants and `IosImportEnvelopePolicyV1Test` proves field-for-field equality. Every untrusted importer call introduced by `0020` or later passes an explicit policy. `MapPreview.cs` is deliberately not in the `0020` allowlist: its two existing stream/Base64 preview calls temporarily inherit the finite trusted default, so they are bounded but do not yet claim the narrower remote/cache policy. Task 10/`0024`, whose allowlist already contains `MapPreview.cs`, changes both to the injected `MapPreviewCachePolicy.SourcePng`; Task 11's remote metadata client passes the exact 1,024-edge/1,048,576-pixel/4 MiB remote minimap policy. No test or documentation claims that every remote call uses an explicit remote policy before `0024`.

`PngTest` covers both constructors at limit `-1`, exact, and `+1` for every applicable field; huge-IHDR checked multiplication; chunk/IDAT/ratio bombs; contradictory/truncated chunks; rejection before allocation; and behavior equality between the explicit V1 policy and the legacy finite default where their values match. Task 10 adds policy-identity tests for both `MapPreview` call sites and proves that an iOS/remote policy is never silently replaced by `TrustedEngineDefault`.

#### YAML delivery boundary

Task 2/`0020` delivers only the immutable `YamlEnvelopeLimits` member of `PackageEnvelopePolicy`. It validates positive/nondefault constructor values and maps exactly 8 MiB, 131,072 nodes, depth 64, line 256 KiB, and scalar 64 KiB from the public V1 constants. Task 2 adds no YAML byte scanner, indentation parser, node counter, `MiniYaml` wrapper, or test claiming hostile YAML is already rejected, and the Task 2 graph passes no untrusted YAML to `MiniYaml`. Task 10 implements the first YAML scanner/validator in `Ra2OramapEnvelope.cs`: it accepts explicit `YamlEnvelopeLimits`, scans raw bytes before `MiniYaml`, shares the already-started map/whole deadlines, and owns YAML byte/node/depth/line/scalar `-1`/exact/`+1`, overflow, truncation, timeout, cancellation, and no-allocation-before-preflight tests. `0020` therefore claims only “YAML policy model delivered”; YAML envelope enforcement begins in `0024`.

#### MIX and IDX/BAG contracts

`MixEnvelopeValidator` exposes only validated index entries and sealed `ValidatedMixEntrySource` capabilities. Ordinary `MixFile.GetContent` rejects a declared segment above 512 MiB before integer conversion. Local/global XCC database reads receive the same name/count/byte budgets. The importer never consults the bundled global MIX database. `IdxBagEnvelopeValidator` and `AudioBagLoader` alone may open the structurally identified `audio.bag` through a checked long-length bounded source up to 384 MiB; the loader no longer catch-all suppresses corrupt IDX/BAG and never calls the ordinary accessor for the BAG leaf.

The bounded importer accepts only an archive-local pair named exactly `audio.idx` and `audio.bag` from the same already validated `audio.mix` or `audiomd.mix`. It never asks the global `FileSystem` to find the sibling and never accepts loose selected BAG/IDX files. IDX is exactly ASCII magic `GABA`, little-endian version `2`, a count from 1 through 65,536, and actual length exactly equal to checked `12 + count * 36` and at most 4 MiB. Each record contains one exact 16-byte name field followed by five little-endian `uint` fields. A name has 1–15 characters before its first NUL, every remaining byte is NUL, characters are only ASCII `[A-Za-z0-9_-]`, canonicalization is ASCII lowercase, and exactly one `.wav` suffix is appended. Names colliding after ASCII folding are invalid.

The legal flag/field matrix is closed:

| Flags | Encoding | Channels | `ChunkSize` |
| --- | --- | ---: | --- |
| `6` | signed PCM 16-bit little-endian | 1 | exactly `0` |
| `7` | signed PCM 16-bit little-endian | 2 | exactly `0` |
| `8` | IMA ADPCM 4-bit | 1 | validated nonzero block size |
| `9` | IMA ADPCM 4-bit | 2 | validated nonzero block size |

Every other value, unknown bit, or simultaneous PCM/IMA encoding is invalid. This preserves the characterized legal PCM flags 6/7 instead of accidentally narrowing them to 2/3. For every record, `SampleRate > 0`, `Length > 0`, `Length <= 256 MiB`, actual BAG length is at most 384 MiB, and checked `Offset + Length <= bag.Length`. PCM length is divisible by `2 * channels`, and checked `2 * channels * SampleRate` fits the unsigned WAV byte-rate field. IMA `ChunkSize` is at least `4 * channels + 1` and at most `ushort.MaxValue`; checked samples per block is `((ChunkSize - 4 * channels) * 2 / channels) + 1`, is positive, and every emitted WAV block-align, samples-per-block, and average-byte-rate value fits its exact unsigned field. No `uint` is converted to `int` or `short` before all relevant checks pass. Unknown flags, invalid rate/chunk arithmetic, duplicate names, truncated IDX, pair-identity mismatch, and out-of-BAG spans surface as typed audio corruption; arithmetic overflow retains its closed diagnostic.

`IdxBagEnvelopeTest` covers all four legal flags; flags 0 through 15 outside the matrix plus high-bit forms; sample-rate zero, maximum, and derived overflow; PCM alignment; IMA block-size minimum, `ushort.MaxValue`, and `+1`; exact/truncated/nonzero-padded/invalid-ASCII names; duplicate folded names; count/index-length arithmetic; BAG/span limit `-1`, exact, and `+1`; offset-plus-length overflow; same-index/name/ledger/policy identity; IDX release after parse; result/package/WAV disposal in every order; and an already-open WAV surviving outer disposal until its final independent pin closes. The unchanged existing `WavReaderSafetyTest` must also pass, including flags 6/7 and its sample-rate-overflow behavior.

#### Retail filename catalog V1 and fixed graph

`RetailFilenameCatalog.V1` is a project-owned immutable static catalog. It never reads `mods/ra2/mod.yaml`, bundled `global mix database.dat`, current culture, a directory, or retail bytes at runtime:

```csharp
public sealed class RetailFilenameCatalog
{
	public static RetailFilenameCatalog V1 { get; }
	public int Version { get; }
	public string DigestSha256 { get; }

	internal bool TryResolve(
		uint hash,
		PackageHashType hashType,
		out string canonicalName);
}
```

Version is exactly `1`. The exact lowercase-ASCII unique-name set has 54 entries, arithmetically pinned as 6 roots + 20 Base nested packages + 25 Yuri nested packages + 3 structural names:

- roots: `ra2.mix`, `language.mix`, `theme.mix`, `ra2md.mix`, `langmd.mix`, `thememd.mix`;
- Base nested packages: `audio.mix`, `cache.mix`, `cameo.mix`, `conquer.mix`, `generic.mix`, `isogen.mix`, `isosnow.mix`, `isotemp.mix`, `isourb.mix`, `load.mix`, `local.mix`, `neutral.mix`, `sidec01.mix`, `sidec02.mix`, `sno.mix`, `snow.mix`, `tem.mix`, `temperat.mix`, `urb.mix`, `urban.mix`;
- Yuri nested packages: `audiomd.mix`, `cameomd.mix`, `cachemd.mix`, `conqmd.mix`, `des.mix`, `desert.mix`, `genermd.mix`, `isodesmd.mix`, `isodes.mix`, `isogenmd.mix`, `isolunmd.mix`, `isolun.mix`, `isosnomd.mix`, `isotemmd.mix`, `isoubnmd.mix`, `isoubn.mix`, `isourbmd.mix`, `loadmd.mix`, `localmd.mix`, `lun.mix`, `lunar.mix`, `ntrlmd.mix`, `snowmd.mix`, `ubn.mix`, `urbann.mix`;
- structural names: `audio.idx`, `audio.bag`, `local mix database.dat`.

The policy additionally has four suffix rows—`.map`, `.mpr`, `.yrm`, `.oramap`—that are not guessed literal MIX filenames. Arbitrary embedded map hashes resolve only through a structurally valid archive-local `local mix database.dat`. Unknown hashes remain opaque content entries: they are not traversed, opened as packages, emitted as maps, or brute-forced, but their mere presence does not invalidate an otherwise structural MIX.

The graph roles are fixed. Base requires singleton roots `ra2.mix` and `language.mix`; its required audio chain is exactly `language.mix -> audio.mix -> { audio.idx, audio.bag }`. RA2 music is independent singleton `theme.mix`. Yuri is all-or-nothing at its roots `ra2md.mix` and `langmd.mix`; Yuri music is independent singleton `thememd.mix`. If present, `audiomd.mix` and `cameomd.mix` are children of `langmd.mix`; the remaining 23 Yuri nested packages listed above are children of `ra2md.mix`. An `audiomd.mix` audio pair is archive-local and validated by the same rules; it can never substitute a Base pair. The 19 non-audio Base nested packages may occur only below a validated Base root. `audio.idx`/`audio.bag` occur only below their identified audio MIX. `local mix database.dat` may occur below any validated MIX but may add only canonical names with one of the four map suffixes; it cannot redefine a static name, map one hash to two names, introduce separators/traversal, or authorize another executable/package type.

The digest policy has exactly 60 rows and only these ASCII tokens: six `root` rows whose parent-role is `base`, `ra2-music`, `yuri`, or `yuri-music` according to the root grouping above; one `package/language.mix/audio.mix` row; 19 `package/base-root/<name>` rows for the other Base packages; two `package/langmd.mix/<name>` rows for `audiomd.mix` and `cameomd.mix`; 23 `package/ra2md.mix/<name>` rows for the remaining Yuri packages; four `leaf/<audio.mix-or-audiomd.mix>/<audio.idx-or-audio.bag>` rows; one `resolver/any-mix/local mix database.dat` row; and four `map-suffix/local-db/<suffix>` rows. No alias token or additional row is permitted.

Catalog digest serialization is UTF-8 without BOM: `"ra2-retail-filename-catalog\0"`, decimal version, and `"\n"`, followed by those 60 rows byte-sorted by `(kind, parent-role, canonical-name)` and encoded as `kind + "\t" + parent-role + "\t" + canonical-name + "\n"`. For V1 the exact canonical `DigestSha256` is `1dec73ccb18c0518a48b8e0c3a96ac914508338bae2335bad432bea12d04c440`. Tests carry an independent full literal row list, recompute that pinned digest, assert the complete ordered sequence and public digest, and fail on name/case/order/duplicate/parent-role/classic-hash/CRC32 collision drift. A reflection count or digest computed from the implementation's own collection is insufficient.

#### Owned archive-graph boundary

`RetailArchiveGraphValidator` has no public raw-path, root, stream-factory, delegate, or caller-implemented source interface. Task 2 defines one internal sealed ownership boundary in `RetailArchiveGraphValidator.cs`; Task 2 does not reference Task 7/8 types, and Task 10 adapts to it from inside the same RA2 assembly:

```csharp
internal sealed record RetailArchiveInputDescriptor
{
	public string CanonicalLogicalName { get; }
	public RetailContentGroup Group { get; }
	public long Length { get; }
	public string Sha256 { get; }

	internal RetailArchiveInputDescriptor(
		string canonicalLogicalName,
		RetailContentGroup group,
		long length,
		string sha256);
}

internal sealed class RetailOwnedArchiveInput : IDisposable
{
	public RetailArchiveInputDescriptor Descriptor { get; }

	internal static RetailOwnedArchiveInput TakeOwnership(
		RetailArchiveInputDescriptor descriptor,
		Stream ownedPinnedStream);
	internal Stream DetachOwnedPinnedStream();
	public void Dispose();
}

internal sealed class RetailArchiveInputBatch : IDisposable
{
	internal ImmutableArray<RetailOwnedArchiveInput> Inputs { get; }

	internal static RetailArchiveInputBatch TakeOwnership(
		ImmutableArray<RetailOwnedArchiveInput> ownedInputs);
	public void Dispose();
}

public sealed class ValidatedEmbeddedMapSource : IDisposable
{
	public string SafeName { get; }
	public long Length { get; }
	public string SourceSha256 { get; }

	internal ValidatedEmbeddedMapSource(
		string safeName,
		long length,
		string sourceSha256,
		ValidatedMixEntrySource ownedSource);
	public Stream OpenRead();
	public void Dispose();
}

public sealed class RetailArchiveGraphResult : IDisposable
{
	public ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> Groups { get; }
	public ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure> GroupFailures { get; }
	public ImmutableArray<ValidatedEmbeddedMapSource> EmbeddedMaps { get; }

	internal RetailArchiveGraphResult(
		ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> groups,
		ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure> groupFailures,
		ImmutableArray<ValidatedEmbeddedMapSource> ownedEmbeddedMaps);
	public void Dispose();
}

public sealed class RetailArchiveGraphValidator
{
	public RetailArchiveGraphValidator(
		PackageEnvelopePolicy policy,
		IRetailMonotonicClock clock,
		RetailFilenameCatalog filenames);

	internal RetailOperationResult<RetailArchiveGraphResult> ValidateOwned(
		RetailArchiveInputBatch ownedInputs,
		RetailValidationDeadline alreadyStartedWholeValidationDeadline,
		CancellationToken cancellationToken);
}
```

`RetailArchiveInputDescriptor` accepts only the six exact lowercase root names and their matching group, a nonnegative length at most `SourceBytes`, and canonical lowercase 64-hex SHA-256. It contains no staged path, provider value, root, or display text. `TakeOwnership` transfers ownership at call entry and requires one readable, seekable exact stream positioned at zero whose actual length equals the descriptor. Invalid metadata, length inspection/mismatch, duplicate transfer, and batch-construction failure close every transferred stream exactly once. `DetachOwnedPinnedStream` is internal, one-shot, and leaves its input inert. The batch defensively copies and enum/name ordinal-sorts, rejects null/default entries, the same object twice, duplicate canonical root names, and group/name mismatch, and owns every accepted input. An empty batch is valid for map-only/preserved-active composition.

`ValidateOwned` consumes the batch at call entry; the caller never accesses or disposes it again. The already-started 90-minute deadline is rejected if default and is never replaced. Immediately before each root begins, the validator starts one ten-minute deadline; its internal `IPackageReadDeadline` adapter checks both deadlines, and the same adapter/token/aggregate ledger flows through every descendant. A matching cooperative `OperationCanceledException` may propagate only when the supplied token is cancelled. Timeout maps to `ValidationTimedOut/DeadlineExceeded`; checked arithmetic retains `ArithmeticOverflow`; recognized malformed archive/audio maps to `CorruptArchive`/`InvalidAudio`; every other I/O failure becomes `SanitizedIoFailure`. No exception text, raw filename beyond the safe root basename, path, or provider string crosses the typed boundary.

`RetailArchiveGraphResult` is deliberately an owning sealed class, not a record. Its internal constructor requires exactly five group keys under the closed enum-order comparer. `GroupFailures` is an immutable enum-sorted subset with exactly one `RetailImportFailure` for every and only `Validation=Invalid` group; each failure's `Group`, fixed safe root basename, diagnostic, and code must match the closed table below. Missing, unavailable, and structurally ready groups have no failure entry. This preserves the closed error code while allowing an optional sibling to remain represented. At Task 2 every structural group status uses `Recognition=NotApplicable`: exact filename and envelope structure are not edition fingerprint evidence, so `Recognized` and `CompatibleUnrecognized` are assigned only by a later manifest/profile validator. Embedded handles are ordinal-sorted by `SafeName` then `SourceSha256`; equal digest and length deduplicate deterministically, equal digest with different length fails closed, and equal basename with different digests remains distinct without a premature map-UID conflict. Each embedded handle owns a `ValidatedMixEntrySource`; the graph validator hashes that same pinned entry under cancellation/deadline checks. Disposing a handle/result prevents new opens but does not invalidate already-open independently pinned child streams. Disposal is idempotent. Failure, timeout, or cancellation releases every root/index/source/partial output and returns no graph capability.

The single per-group failure is selected by the first matching row below; within a row use byte-ordinal fixed root order `ra2.mix`, `language.mix`, `ra2md.mix`, `langmd.mix`. No nested/raw name is exposed:

| Invalid condition | `RetailImportErrorCode` | Diagnostic | Safe basename |
| --- | --- | --- | --- |
| exactly one Base root is present | `IncompleteBase` | `MissingRequiredArchive` | the missing fixed Base counterpart |
| exactly one Yuri root is present | `PartialYuri` | `MissingRequiredArchive` | the missing fixed Yuri counterpart |
| root MIX/ZIP/local-database structure is malformed or overflows | `CorruptArchive` | `MalformedEnvelope` or `ArithmeticOverflow` | offending fixed root |
| mandatory nested Base `audio.mix` is absent | `MissingNestedBasePackage` | `MissingRequiredArchive` | `language.mix` |
| required `audio.idx`/`audio.bag` pair member is absent | `InvalidAudio` | `MissingRequiredArchive` | `language.mix` or `langmd.mix` for that group |
| IDX/BAG metadata, flags, span, or audio arithmetic is invalid | `InvalidAudio` | `MalformedEnvelope` or `ArithmeticOverflow` | `language.mix` or `langmd.mix` for that group |

Neither root present is `Missing`, not `Invalid`, and creates no failure. Top-level root-pair completeness is decided before reading either present root; after a complete pair, root envelopes are checked before the `language.mix -> audio.mix` requirement, then the archive-local audio pair and audio structure. Multiple faults never create multiple entries for one group and never depend on discovery order.

Task 2's complete five-group structural matrix is fixed and every row uses `Recognition=NotApplicable`:

| Group | Absent | Structurally valid at Task 2 | Structural failure |
| --- | --- | --- | --- |
| Base | both roots absent => `Missing` | complete `ra2.mix` + `language.mix` chain => `Ready` | partial pair or any required chain fault => `Invalid` |
| RA2 Music | `theme.mix` absent => `Missing` | valid `theme.mix` retained but track/mount wiring not yet checked => `Unavailable` | invalid `theme.mix` => `Invalid` |
| Yuri | both roots absent => `Missing` | complete valid `ra2md.mix` + `langmd.mix` pair => `Ready` | partial pair or required structure fault => `Invalid` |
| Yuri Music | `thememd.mix` absent => `Missing` | valid `thememd.mix` retained, but V1 has no registered Yuri soundtrack definitions => `Unavailable` | invalid `thememd.mix` => `Invalid` |
| Maps | no embedded handles => `Missing` | one or more structurally bounded handles retained, semantic/package validation deferred to Task 10 => `Unavailable` | Task 2 never labels a map semantically Invalid; a containing archive fault belongs to its archive group |

Task 3/10 may promote or downgrade these authenticated structural results only after their explicit track/profile/map checks; Task 2 never claims final game readiness.

Group-local corruption produces an `Invalid` group status plus its exact `GroupFailures` entry and releases that group's handles so optional siblings may remain represented; a fatal aggregate/deadline/source-identity failure returns a failed operation result instead. Structural map discovery alone never declares Maps playable/Ready—Task 10 owns semantic map readiness—and no loose map is required for archive structural success. Task 10's `RetailCandidateValidator` builds the batch by calling the same sealed `RetailCandidateArtifactSource.OpenVerifiedRead` once per archive artifact and immediately transferring each returned stream; failure on the nth item disposes all earlier inputs. This creates only the later Task 10 -> Task 2 dependency. Task 9 adapts a borrowed, still-live `ValidatedEmbeddedMapSource` to `IGen2MapSource` without wrapping or replacing archive identity.

`RetailArchiveGraphValidatorTest` covers map-bearing nested packages, the independent full catalog literal/digest, valid/invalid local DB, static/local hash collision and duplicate, unknown-hash opacity, fixed parent roles, required Base audio chain, optional Yuri audio, recursion/entry/addressed-byte/time/cancellation limits, one-shot detach, batch rollback at every element, duplicate object/name rejection, every row and multi-fault priority in the Invalid-group/failure table, every row of the complete five-group structural matrix, exact failure/status bijection, Task-2-only `Recognition=NotApplicable`, graph-result-first and child-first disposal, two concurrent child reads with independent positions, use-after-dispose rejection, exact final close count, no partially validated handle, and no loose-map requirement. API-shape tests reject any public archive validator constructor/overload containing a filesystem path, root, `Func<>`, delegate, arbitrary caller-implemented source interface, or replacement stream. External `.map`/`.mpr`/`.yrm` candidates and embedded handles enter Task 9 through the same size/digest/cancellation interface.

- [ ] **Step 4: Run GREEN and builds**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~ZipEnvelopeTest|FullyQualifiedName~PngTest|FullyQualifiedName~MixEnvelopeTest|FullyQualifiedName~IdxBagEnvelopeTest|FullyQualifiedName~RetailArchiveGraphValidatorTest|FullyQualifiedName~RetailContentCatalogTest|FullyQualifiedName~RetailSourceDiscoveryTest|FullyQualifiedName~RetailCandidatePlanTest|FullyQualifiedName~IosImportEnvelopePolicyV1Test|FullyQualifiedName~WavReaderSafetyTest' \
  -v minimal

env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Game/OpenRA.Game.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Mods.Cnc/OpenRA.Mods.Cnc.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore -m:1
git diff --check
```

- [ ] **Step 5: Commit the implementation paths**

Task 2 adds envelope-integration cases or fixture capabilities to each of the five paths marked **Modify again**, so every one of the displayed 24 engine paths has a real Task 2 diff. Stage exactly the four outer paths plus those 24 engine paths: the implementation commit contains 28 paths, and the 24-path engine subset is also the unchanged `0020` patch allowlist. Show the complete staged list and commit:

```bash
set -euo pipefail
git commit -m "Harden retail archive envelopes"
```

- [ ] **Step 6: Generate and verify `0020` immediately**

Generate from official+`0001`…`0019` with the byte-sorted exact allowlist and assign every common-block variable literally:

```bash
set -euo pipefail
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
test "$(shasum -a 256 "$OPENRA_OFFICIAL_ZIP" | awk '{print $1}')" = \
  ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a
PATCH_STAGE="$(mktemp -d /private/tmp/nuke-hour-engine-patch-0020.XXXXXX)"
ALLOWLIST="$PATCH_STAGE/0020.allowlist"
VERIFY_DIR="$PATCH_STAGE/verification"
PREDECESSOR_END=19
CANDIDATE=engine-patches/0020-ios-retail-archive-envelopes.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0020 --output "$ALLOWLIST"
LC_ALL=C sort -c -u "$ALLOWLIST"
test "$(wc -l < "$ALLOWLIST" | tr -d ' ')" -eq 24
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
test -f "$VERIFY_DIR/evidence.json"
```

Require exact header equality, no outside-engine/root-mod paths, byte equality for every tracked engine path, duplicate refusal/tree unchanged, reverse success, and no registry drift. Update README and verifier tests to `0020`, run their focused Python tests, then make a separate delivery commit containing exactly the three displayed delivery files. The Task 2 boundary is therefore 28 implementation paths plus 3 delivery paths, never one combined commit:

```bash
set -euo pipefail
git commit -m "Deliver retail archive envelope engine patch"
```

---

### Task 3: Split trusted Base/Yuri manifests, add pre-game context, and deliver `0021`

**Outer files:**

- Modify again: `OpenRA.Mods.RA2/Content/RetailImportSource.cs`
- Modify again: `OpenRA.Mods.RA2/Content/RetailArchiveGraphValidator.cs`
- Create: `OpenRA.Mods.RA2/Content/Ra2RuntimeProfile.cs`
- Create: `OpenRA.Mods.RA2/Content/Ra2RetailMountContract.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailProfileValidator.cs`
- Create: `OpenRA.Mods.RA2/Content/IosRuntimeResourcePreflight.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/Ra2MapImportModels.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/Ra2MapImportContext.cs`
- Create: `OpenRA.Mods.RA2/Content/IosRuntimeResourceRoot.cs`
- Create: `packaging/exact_git_stage.py`
- Create: `packaging/ios-retail-task-paths.json`
- Create: `packaging/tests/test_exact_git_stage.py`
- Modify: `build/OpenRA.AotRegistry.Generator/Program.cs`
- Modify: `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Modify: `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs`
- Create: `mods/ra2/mod.ios-base.yaml`
- Create: `mods/ra2/mod.ios-yuri.yaml`
- Create: `mods/ra2/manifest/common.yaml`
- Create: `mods/ra2/manifest/gameplay-base.yaml`
- Create: `mods/ra2/manifest/gameplay-yuri.yaml`
- Create: `mods/ra2/manifest/filesystem-desktop.yaml`
- Create: `mods/ra2/manifest/filesystem-ios-base.yaml`
- Create: `mods/ra2/manifest/filesystem-ios-yuri.yaml`
- Create: `mods/ra2/rules/world-base.yaml`
- Create: `mods/ra2/rules/defaults-base.yaml`
- Create: `mods/ra2/rules/proxy-actors-base.yaml`
- Create: `mods/ra2/rules/allied-infantry-base.yaml`
- Create: `mods/ra2/rules/soviet-infantry-base.yaml`
- Create: `mods/ra2/rules/soviet-vehicles-base.yaml`
- Create: `mods/ra2/sequences/vehicles-base.yaml`
- Create: `mods/ra2/sequences/voxels-base.yaml`
- Create: `mods/ra2/audio/voices-base.yaml`
- Modify: `packaging/public-content-manifest.json`
- Modify: `docs/legal/public-asset-sbom.json`
- Modify: `packaging/public_brand_delta_audit.py`
- Modify: `packaging/tests/test_public_brand_delta.py`

**Exact `0021` engine allowlist:**

- Modify again: `engine/OpenRA.Game/FileSystem/FileSystem.cs`
- Modify: `engine/OpenRA.Game/Game.cs`
- Modify: `engine/OpenRA.Game/InstalledMods.cs`
- Modify: `engine/OpenRA.Game/Manifest.cs`
- Modify: `engine/OpenRA.Game/Map/MapCache.cs`
- Modify: `engine/OpenRA.Game/ModData.cs`
- Create: `engine/OpenRA.Game/ModDataConstructionOptions.cs`
- Modify: `engine/OpenRA.Game/Platform.cs`
- Create: `engine/OpenRA.Game/PreGameEngineContext.cs`
- Create: `engine/OpenRA.Game/RuntimeContractIdentity.cs`
- Modify: `engine/OpenRA.Game/Support/Arguments.cs`
- Create: `engine/OpenRA.Game/TrustedManifestSelection.cs`
- Create: `engine/OpenRA.Test/ArgumentsSnapshotTest.cs`
- Create: `engine/OpenRA.Test/PreGameEngineContextTest.cs`
- Create: `engine/OpenRA.Test/Ra2RuntimeProfileManifestTest.cs`
- Create: `engine/OpenRA.Test/RetailCandidateProfileValidationTest.cs`
- Modify: `engine/OpenRA.Test/RetailContentImporterTest.cs`
- Create: `engine/OpenRA.Test/TrustedManifestEntryTest.cs`

**Delivery files:**

- Create: `engine-patches/0021-ios-retail-runtime-profiles.patch`
- Modify: `engine-patches/README.md`
- Modify: `packaging/engine_workspace.py`
- Modify: `packaging/tests/test_engine_workspace.py`
- Modify: `packaging/tests/test_verify_engine_patch_delivery.py`

Task 3 has exactly **36 outer implementation paths**, **18 engine allowlist paths**, and **5 delivery paths**. The implementation commit therefore stages exactly **54 paths**; the separate delivery commit stages exactly the five delivery paths above. The checked-in golden is already part of `PLAN_COMMIT` and is read-only here, so it is not one of the 54 implementation paths. `packaging/tests/test_verify_engine_patch_delivery.py` is not generic today: Task 3 updates its hard-coded candidate, predecessor, README row, exact 18-header set, and replay assertions from `0020` to `0021`.

`packaging/ios-retail-task-paths.json` is the checked, versioned machine-readable mirror of every Task 3–16 implementation and delivery boundary in this plan. Schema `1` records, for each `(task, phase)`, byte-sorted unique `requiredPaths`, `optionalDirtyPaths`, and `allowedUnstagedPaths`, plus exact minimum/maximum staged counts. `packaging/tests/test_exact_git_stage.py` parses this plan's displayed **Files** blocks and requires exact path/phase equality with that manifest, including Task 6–8 deferred tests, Task 14's later force-add, and the two optional generated-AOT cases; no hand-maintained manifest drift is accepted. `packaging/exact_git_stage.py stage --repository ROOT --manifest MANIFEST --task N --phase implementation|delivery` validates canonical repository-relative paths, duplicate/case aliases, regular non-symlink type, the 50 MiB ceiling, secret/cache/bin/obj/private-evidence exclusions, and the complete dirty/index state before mutation. It rejects every cached or dirty path not explicitly permitted, stages the exact selected set with per-path `git add -f --`, writes and displays byte-sorted expected/actual lists, compares them and their count, runs cached diff checks, and exits nonzero before commit on any mismatch. Required paths must have a real diff; optional paths are staged only when dirty; allowed-unstaged paths are never staged. It never deletes, restores, resets, commits, accepts a directory/glob, or infers a path from Git status. Every Task 3–16 commit fence below invokes this helper immediately before its one `git commit`, so an earlier or unrelated cached path cannot ride into a task commit.

- [ ] **Step 1: Write RED trusted-entry and pre-game tests**

First close the exact baseline RED without changing production: modify **only** `engine/OpenRA.Test/RetailContentImporterTest.cs` so its three successful-Base paths use `SyntheticRetailFixtureBuilder.WriteValidBaseArchives` instead of four-byte text masquerading as MIX. Hash the actual synthetic archive bytes in the success assertion and preserve every failure/rollback invariant. Run the complete fixture and require all of its tests to pass; no archive parser, catalog, importer, model, or limit may be relaxed for this migration.

Next start the Task 3 packaging RED boundary: modify **only** `packaging/tests/test_public_brand_delta.py`, run it before touching the audit implementation, inventories, profile YAML, or project file, and retain the RED output. The RED assertions require the exact 11 PublicClean removes, eight unchanged `SOURCE_MAPPINGS`, 17 new `PROFILE_SPLIT_SOURCE_MAPPINGS`, 280/277 inventory counts, the unchanged five/two namespace difference, and historical delta 580 with zero added `(code,path)` rows. Only after that test demonstrably fails may the author add `packaging/tests/test_exact_git_stage.py`; before creating either `exact_git_stage.py` or `ios-retail-task-paths.json`, run that test and retain its missing-module/manifest RED. Its final suite must cover every rejection and exact-stage invariant described below. Only after both Python REDs are retained may the remaining C# tests and implementation paths change. Before the project file, generator, generated source, or preflight implementation changes, `Ra2RuntimeProfileManifestTest` reads the checked-in runtime-descriptor golden, verifies its file SHA-256, schema, count, canonical framing, and complete byte-sorted table, then asserts the missing evaluated project/generated inventory path. After implementation it requires project items, generated `RuntimeContractDescriptorInventory`, and preflight input to equal that golden shape exactly. The RED authority is the pre-existing golden, never data derived from the implementation under test.

The first C# RED also characterizes the already-delivered Task 1/2 clock gap before changing production: `RetailValidationDeadline` currently lacks its owning-clock identity, and a deadline started by clock A is accepted by discovery and archive validation when clock B is supplied. Before adding any of the other missing Task 3 API tests, create `RetailCandidateProfileValidationTest.cs` with only a `RetailValidationClockIdentityTest` fixture and run `dotnet test` filtered to that exact fixture; both behavior tests must compile against the delivered Task 2 API and fail for the wrong result/provider-read assertions, not for a missing Task 3 symbol. The fixture proves both mismatched-clock calls reach no provider/archive read and return exactly `ProfileContextFailure/null/null/ProfileMetadataMismatch`. Only after retaining that RED may the rest of the Task 3 tests be added. Task 3 then updates the two listed RA2 sources: `RetailImportSource.cs` adds the final `MatchesClock` value semantics already locked by Task 1's API, makes `RetailDiscoveryBudget` terminal with that exact failure before any provider call, and `RetailArchiveGraphValidator.ValidateOwned` performs the same check before reading any detached input. The validator still takes/disposes its owned batch exactly once on this failure. Every new Task 3/4 operation below applies the same mapping at entry. No wall-clock adapter, exception-message classification, or timestamp-only comparison is permitted.

Lock these APIs:

```csharp
public readonly struct TrustedManifestSelection : IEquatable<TrustedManifestSelection>
{
	public string ModId { get; }
	public string EntryName { get; }
	public TrustedManifestSelection(string modId, string entryName);
	public static TrustedManifestSelection Default(string modId);
	public bool Equals(TrustedManifestSelection other);
	public override bool Equals(object obj);
	public override int GetHashCode();
	public static bool operator ==(
		TrustedManifestSelection left, TrustedManifestSelection right);
	public static bool operator !=(
		TrustedManifestSelection left, TrustedManifestSelection right);
}

public sealed class EngineBootstrapConfiguration
{
	readonly Arguments arguments;
	public string EngineDir { get; }
	public string SupportDir { get; }
	public ImmutableArray<string> ModSearchPaths { get; }
	public ImmutableArray<string> ExplicitModPaths { get; }
	public Arguments Arguments => arguments.Clone();
	public EngineBootstrapConfiguration(
		string engineDir,
		string supportDir,
		ImmutableArray<string> modSearchPaths,
		ImmutableArray<string> explicitModPaths,
		Arguments arguments);
}

internal interface IModDataServiceProvider : IDisposable
{
	bool TryGet<T>(out T service) where T : class;
	void Dispose();
}

public interface IModDataBoundService
{
	IDisposable Bind(ModData modData);
}

public interface IModDataActivationToken : IDisposable
{
	Task BeginDrainAsync();
}

public interface IModDataActivatedService : IModDataBoundService
{
	IModDataActivationToken Activate();
}

public readonly struct ModDataServiceRegistration :
	IEquatable<ModDataServiceRegistration>
{
	public Type ServiceType { get; }
	public object Service { get; }
	private ModDataServiceRegistration(Type serviceType, object service);
	public static ModDataServiceRegistration Owned<T>(T service)
		where T : class;
	public bool Equals(ModDataServiceRegistration other);
	public override bool Equals(object obj);
	public override int GetHashCode();
	public static bool operator ==(
		ModDataServiceRegistration left, ModDataServiceRegistration right);
	public static bool operator !=(
		ModDataServiceRegistration left, ModDataServiceRegistration right);
}

public sealed class ModDataServiceCollection : IDisposable
{
	public bool TryAddOwnedBatch(
		ImmutableArray<ModDataServiceRegistration> registrations);
	public ModDataConstructionOptions Seal(
		FileSystemLoaderDecorator decorateFileSystemLoader,
		ModDataSettingsSnapshot settings,
		FluentInitializationMode fluentMode,
		ModDataRenderingMode renderingMode);
	public void Dispose();
}

public delegate IFileSystemLoader FileSystemLoaderDecorator(
	IFileSystemLoader configuredLoader);

public enum FluentInitializationMode
{
	Isolated,
	PublishGlobal,
}

public enum ModDataRenderingMode
{
	NoRender,
	Game,
}

public sealed class ModDataSettingsSnapshot
{
	readonly Dictionary<string, Hotkey> hotkeys;
	public string Language { get; }
	public string SystemLanguageTag { get; }
	internal static ModDataSettingsSnapshot Capture(Settings settings);
	internal Dictionary<string, Hotkey> CreateIsolatedHotkeys();
	internal Dictionary<string, Hotkey> TakePublishGlobalHotkeys();
}

public sealed class ModDataConstructionOptions : IDisposable
{
	internal static ModDataConstructionOptions CreateTrustedDesktopDefault(
		ModDataSettingsSnapshot settings);
	internal OwnedModDataConstructionState TakeOwnership();
	public void Dispose();
}

public sealed class PreGameModDataScope : IDisposable
{
	public ModData ModData { get; }
	public PreGameModDataScope TakeOwnership();
	public void Dispose();
}

internal sealed class EngineStartupState : IDisposable
{
	public EngineBootstrapConfiguration Configuration { get; }
	public TrustedManifestSelection Selection { get; }
	public RuntimeContractIdentity RuntimeContract { get; }
	internal OwnedEngineStartupState TakeOwnership();
	public void Dispose();
}

internal interface IGameLifecycleDispatchLane
{
	bool IsCurrentThread { get; }
	void Post(Action action);
	int PumpAvailable(int maxActions);
}

public static class Game
{
	internal const int LifecycleDispatchBatch = 64;
	public static RunStatus InitializeAndRun(string[] args);
	internal static RunStatus InitializeAndRun(
		EngineStartupState ownedStartupState);
	internal static void PumpLifecycleDispatch(
		IGameLifecycleDispatchLane lane);
	internal static void DrainLifecycleDispatchUntil(
		IGameLifecycleDispatchLane lane,
		Task activationFence);
}

public sealed class PreGameEngineContext : IDisposable
{
	public ModDataSettingsSnapshot Settings { get; }
	public static PreGameEngineContext Open(
		EngineBootstrapConfiguration configuration);
	public PreGameModDataScope OpenModData(
		TrustedManifestSelection selection,
		ModDataConstructionOptions options);
	internal EngineStartupState SealForGame(
		TrustedManifestSelection selection,
		RuntimeContractIdentity runtimeContract,
		ModDataConstructionOptions options);
}

public readonly struct RuntimeContractIdentity : IEquatable<RuntimeContractIdentity>
{
	public static RuntimeContractIdentity Legacy { get; }
	public string ProfileId { get; }
	public string ContractDigest { get; }
	public bool IsLegacy { get; }
	public RuntimeContractIdentity(string profileId, string contractDigest);
	public bool Equals(RuntimeContractIdentity other);
	public override bool Equals(object obj);
	public override int GetHashCode();
	public static bool operator ==(
		RuntimeContractIdentity left, RuntimeContractIdentity right);
	public static bool operator !=(
		RuntimeContractIdentity left, RuntimeContractIdentity right);
}

public sealed class Manifest
{
	public ImmutableArray<string> RuntimeContractCultures { get; }
	public T Get<T>() where T : IGlobalModData;
	public T Get<T>(ObjectCreator explicitObjectCreator)
		where T : IGlobalModData;
}

public sealed class ModData : IDisposable
{
	public TrustedManifestSelection Selection { get; }
	public RuntimeContractIdentity RuntimeContract { get; }
	public bool TryGetService<T>(out T service) where T : class;
	internal IGameLifecycleDispatchLane LifecycleDispatchLane { get; }
	internal void DrainActivationsAndUnbind(Action<Task> drainFence);
}

public sealed class MapCache : IDisposable
{
	internal Task BeginPreviewDrainAsync();
	internal void DisposePreviewResourcesOnGameThread();
}

public sealed class Arguments
{
	// Existing parsing and lookup APIs remain unchanged.
	public Arguments Clone();
}
```

Do not change the repository-wide language version: the engine remains C# 9 with nullable annotations disabled. Implement all three shown value types as handwritten `readonly struct`s, not `record struct`s, and use plain reference types in every new Task 3 engine/RA2 signature so Release builds emit no CS8632 warnings. Documented optional values may still be null at runtime, but private constructors validate every legal null/non-null state. `TrustedManifestSelection` and `RuntimeContractIdentity` constructors validate before assignment; equality compares their complete logical state with `StringComparer.Ordinal`; hashes use null-safe ordinal hashes in a checked-independent `unchecked` `* 397 ^` sequence; and the operators delegate to `Equals`. Default structs must compare/hash without throwing but are rejected at every operational boundary. `ModDataServiceRegistration` validates non-null type/service before assignment, compares `ServiceType` by reference and `Service` by `ReferenceEquals`, and uses null-safe `RuntimeHelpers.GetHashCode` only after a valid-state check. `RuntimeContractIdentity.Legacy` is the only accepted legacy state; it has `IsLegacy == true`, empty profile/digest fields, and a private legacy constructor. A public identity rejects any legacy-shaped/default state, a profile outside `[a-z0-9-]{1,32}`, or a non-lowercase-hex 64-character digest.

Add this exact defensive-copy API in `engine/OpenRA.Game/Support/Arguments.cs` without exposing its mutable backing dictionary or adding an enumerable public view:

```csharp
public Arguments Clone()
{
	var clone = new Arguments();
	foreach (var pair in args)
		clone.args.Add(pair.Key, pair.Value);

	return clone;
}
```

`args` is the existing parsed-options dictionary.  Its keys and values are already immutable strings, so copying every pair is a complete snapshot.  `EngineBootstrapConfiguration` validates/copies both immutable arrays into fresh `ImmutableArray.CreateRange` instances and stores `arguments.Clone()` in its private field; its public `Arguments` getter returns `arguments.Clone()` on every access, while the internal startup path takes one additional clone before `Settings` or any initialization can observe it.  Thus a caller cannot retain the configuration's parser state, and mutations made by `ReplaceValue` on the caller argument object or a returned snapshot cannot affect another startup.  `ArgumentsSnapshotTest` covers zero/one/many options, duplicate-key parser semantics, clone independence in both directions, and the exact configuration/startup defensive-copy boundary; `PreGameEngineContextTest` proves same-mod reconstruction sees the original snapshot and value-equality/hash/operator behavior for all three handwritten structs.  Keep `Arguments.cs` and this new test in the `0021` allowlist.

`TrustedManifestSelection` accepts only one root filename and rejects separators, absolute/traversal/NUL/empty/dot names; the RA2 layer exposes only the two fixed enum-to-literal mappings. `Manifest` gains an `entryName = "mod.yaml"` constructor and immutable `EntryName` plus insertion-ordered `IncludedFiles`. A composed entry uses repeated scalar nodes—not one child list—such as `Include: manifest/common.yaml`. Every Include value must be one canonical package-relative forward-slash path: reject absolute paths, package prefixes such as `ra2|`, empty/`.`/`..` segments, `./`, backslashes, NUL, duplicate ordinal names, and case aliases. Resolution calls the already selected package directly, processes repeated Include nodes in source order, and rejects Include inside every fragment. `RuntimeContractCultures` is a reserved manifest list rather than an `IGlobalModData` module; entries are canonical nonempty BCP-47 tags, ordinal/case-alias unique, and defensively copied. The desktop manifest omits it and therefore has an empty array; both iOS roots inherit exactly one value, `zh-CN`, from `common.yaml`. `ModData`, same-mod reconstruction, and normal startup preserve the selected entry, exact ordered resolved-fragment list, and culture list.

`RuntimeContractIdentity` accepts either the singleton `Legacy` value or profile `[a-z0-9-]{1,32}` plus a 64-lowercase-hex digest; partially initialized/default non-Legacy states fail. `TryAddOwnedBatch` first validates the complete immutable batch (non-null instances, unique service keys, no key already present, a live unsealed destination, and no duplicate object identity) and then atomically registers every item in the caller's exact immutable-array insertion order, or returns false without mutating either side. No type-name or hash sorting is permitted. Binding uses that same global submission order; rejected-batch rollback, failed-bind rollback, unbind, and final service disposal use its exact reverse. The unsealed collection releases earlier accepted batches in reverse global insertion order. `Seal` is the only constructor for `ModDataConstructionOptions`: it validates a non-null decorator, defensive settings snapshot, two-value Fluent mode, and two-value rendering mode, then atomically transfers the private provider plus ownership into a one-shot shell. No public constructor, record copy, `with`, provider implementation, or property exposes/forges that state. A failed seal leaves the collection sole owner; a successful seal leaves it inert. `TakeOwnership` is internal and atomic; disposal through a retained alias after consumption is a no-op. Duplicate types, nulls, mutation after seal, reuse after failed/consumed/disposed ownership, alias disposal, and double registration fail closed. Tests submit deliberately reverse-alphabetic service types across multiple batches and prove insertion-order bind plus exact reverse rollback/unbind/disposal.

`ModData.Selection` and `ModData.RuntimeContract` are immutable read-only instance identity. The constructor validates and assigns both plus the rendering mode as its first acquired phase, before manifest parsing, ObjectCreator, filesystem, loaders, rules, cache, or any service can observe the instance. The profiled constructor requires the exact startup selection/identity; the desktop/legacy path stores `TrustedManifestSelection.Default(canonicalModId)` and `RuntimeContractIdentity.Legacy`, never default structs. Same-mod reconstruction receives the same pair from the owned startup payload. Default, mismatched, partially legacy-shaped identity, or unknown rendering mode fails before any later phase acquires a resource.

Task 3 also closes the fresh-process no-render boundary in the same `0021` patch. Baseline `ModData` constructs `MapCache`, whose eager `SheetBuilder(SheetType.BGRA)` constructor reads `Game.Settings.Graphics.SheetSize`; that is invalid before `Game.InitializeAndRun`, where `Game.Settings` is intentionally still null. `ModDataRenderingMode.NoRender` therefore constructs a complete non-null logical `MapCache` without allocating `SheetBuilder`, texture, atlas, preview CPU/GPU storage, or starting a preview worker, and without reading `Game.Settings`, `Game.Renderer`, or another global. Metadata/map lookup needed by pre-game validation remains available; a preview/render request is rejected with the fixed no-render result and cannot lazily allocate graphics. `ModDataRenderingMode.Game` retains characterized desktop/game behavior and requires the explicit settings snapshot rather than consulting `Game.Settings` during construction. `OpenModData` accepts only `(Isolated, NoRender)` and `SealForGame` only `(PublishGlobal, Game)`, rejecting every cross-pair before ownership transfer. A fresh-process test sets `Game.Settings == null`, opens and disposes both Base and Yuri pre-game scopes, requires non-null `MapCache` plus zero render allocation/thread/global access, and proves normal Game construction still creates the characterized renderer-backed cache.

At the end of successful `ModData` construction—after manifest selection, runtime identity, filesystem, object creation, and `MapCache` are final but before escape—`ModData` invokes each `IModDataBoundService.Bind(this)` in preserved insertion order and owns the non-null returned bind token. A null token is a construction failure. `ModData.TryGetService<T>(out T service)` is the sole public lookup: it returns the exact registered instance only while the provider/ModData binding is live, otherwise returns false and sets the plain reference out value to null; there is no throwing/static getter. Bind may capture/validate the instance and attach an inert view, but cannot switch a dispatcher backend, subscribe an external source, start background work, expose a queue, or synchronously invoke a callback. After every bind token is owned, `ModData` verifies every registration key through `TryGetService` against its original `ReferenceEquals` identity. Only after all identities match does it invoke `Activate()` once, in insertion order, on each registered instance implementing `IModDataActivatedService`, and owns each non-null `IModDataActivationToken`. Activation may make already-bound work externally available but may neither change service identity nor acquire a different ModData; a null token is failure, and an `Activate` implementation that throws before returning its token must complete all fallible checks before exposure and synchronously undo its own partial work. `BeginDrainAsync` is idempotent: at call entry it synchronously rejects new work for that activation epoch and returns the same non-null fence, which completes only after that epoch can produce no worker, validation context, observer callback, or dispatcher invocation. Its `Dispose` is a nonblocking final teardown permitted only after that fence completes; activation tokens may not call `Task.Wait`, `.Result`, `GetAwaiter().GetResult()`, `Thread.Join`, or another synchronous worker join.

Any bind, identity, or activation failure prevents the instance from escaping. Game creates one fresh lifecycle lane for every game-owned `ModData` construction and injects it before service binding; pre-game scopes receive a rejecting inert lane. The lane owns a thread-safe FIFO plus one late-ticket-or-fence signal. During normal gameplay, `Game.LogicTick` calls `PumpLifecycleDispatch` once at a fixed point before advancing world/network state; it executes at most `LifecycleDispatchBatch == 64` admitted actions in FIFO order, never executes a legacy delayed action, and leaves excess work signaled for the next tick. This is the sole normal-operation consumer, so a minimap upload, runtime-map commit, dispatcher `InvokeAsync`, or memory-pressure callback completes without waiting for shutdown while an unbounded producer cannot starve simulation. Tests post 1, 64, and 65 tickets, require FIFO/exact batching across two ticks, prove real `CacheMinimap` upload and dispatcher invocation finish before shutdown begins, and prove an unrelated `RunAfterTick` action is not executed by this pump.

Shutdown/rollback first stops gameplay producers, then processes every acquired activation token in exact reverse activation order: call `BeginDrainAsync`, use the separate game-owned drain-until-fence pump until the fence completes, dispose the now-quiescent token, and continue even if one drain/final dispose faults. Task 3 modifies `MapCache.cs` in this same patch and delivers the compile-time lifecycle seam consumed by `Game`/`ModData`: the real legacy/system-map `CacheMinimap`, `LoadAsyncInternal`, and `Dispose` paths stop posting to `Game.RunAfterTick`, stop using post-dispose ThreadPool cleanup or a synchronous `Join`, and instead admit their worker/upload/`ReleaseBuffer` tickets only to the owning ModData lifecycle lane. `BeginPreviewDrainAsync` atomically rejects new preview work, signals the worker to exit, and returns one idempotent fence covering every admitted ticket; after Game pumps that fence, `DisposePreviewResourcesOnGameThread` releases the sheet/atlas directly and exactly once. The no-render cache returns an already-complete fence and has no GPU resource to release. After store drain and while the dispatcher/lane are live, Game drains this separate MapCache fence; only then may dispatcher drain, reverse bind-token disposal, registry detach, package unmount, lane close, or legacy delayed-action replacement occur. Constructor rollback, same-mod reconstruction, and final exit use this identical sequence. It then disposes bind tokens in exact reverse insertion order. `DrainLifecycleDispatchUntil` asserts the captured game thread, executes only that lane's admitted actions, never calls `PerformDelayedActions`, `LogicTick`, or a world/network tick, and waits on that signal when empty. The dispatcher therefore remains active while the runtime store and preview pipeline drain, and an operation already past its non-cancellable durability boundary can execute its reserved game-thread commit exactly once before store detachment. `Game`, `ModData`, and same-mod reconstruction use this one drain path; no alternate public dispose path may synchronously join a worker, queue ThreadPool cleanup, or post cleanup to the legacy queue. The provider remains owned across sequential same-mod reconstruction by the startup payload and disposes service instances once, in reverse insertion order, only after final activation/preview drain and unbind. Source-contract tests over the three exact MapCache methods reject `Game.RunAfterTick`, `ThreadPool.QueueUserWorkItem`, `Thread.Join`, `.Wait()`, `.Result`, and `GetAwaiter().GetResult()`. Behavior tests cover zero/one/multiple activated services, activation-after-all-identities, null/throw/failure at every activation ordinal, no activation after an earlier failure, self-rollback before a thrown activation, store → preview → dispatcher drain followed by reverse bind rollback, a fence that faults only after reaching quiescence, and a game-thread shutdown racing both service and preview workers already waiting on the dedicated lifecycle lane without deadlock.

`ModData` construction is an explicit phase machine: immutable selection/runtime identity, manifest clone, ObjectCreator/loaders, filesystem mount, custom data/isolated-or-global Fluent, load screen, WidgetLoader/MapCache, media/terrain/sequence loaders, HotkeyManager, lazy rules state, then service binds. Each acquired field/lease is recorded before advancing. Any exception rolls back only completed phases in strict reverse order, including unmount, binding tokens, map cache, ObjectCreator, manifest, and load screen; the partially constructed object never escapes. `Dispose` shares the same guarded rollback core, is safe after every partial phase, and is idempotent. Tests inject at identity assignment and every later constructor/bind phase, prove the two identity getters are available to every bind but never mutable/default, cover same-mod reconstruction, and assert no callback, mount, descriptor, token, provider, or settings ownership survives or is disposed twice.

Cleanup within those existing phases is total and has one locked exception-precedence rule. `FileSystem.UnmountAll` first snapshots every currently mounted package it owns (never an installed-mod package), then detaches the complete mounted-package, explicit-mount, mod-package, and file-index state before invoking any package callback. It attempts each owned package exactly once in the preserved snapshot order, retains the first cleanup fault, continues through every later package, and rethrows that first fault only after the root is empty; an immediate repeated or reentrant `UnmountAll` observes the detached empty root and invokes nothing. `Manifest.Dispose` uses one atomic disposal gate, snapshots its disposable modules in their existing insertion order, attempts every module exactly once even when the first or a middle module throws, then reports the first fault; every repeated or reentrant call is inert. The shared `ModData` rollback core records rather than throws cleanup faults while it completes all existing reverse phases. A constructor catch ignores that cleanup result and rethrows its original primary exception with its identity and stack intact, whereas normal `ModData.Dispose` completes total cleanup and then reports the first cleanup fault in the existing phase/order; because its ownership gate closes before cleanup, every later call is inert even when the first call threw. Deterministic tests use multiple packages and modules with hostile first and middle entries, prove exact-once cleanup plus empty/repeat-inert state, require constructor-primary priority, and require normal disposal to return the same first cleanup exception. This cleanup correction changes no activation-fence sequencing, lifecycle-lane behavior, Game drain, or `DrainLifecycleDispatchUntil` contract.

`SealForGame` requires fresh final construction options explicitly; it cannot infer or reuse them from a previously opened scope. The profiled overload, `EngineStartupState`, and `Game.InitializeAndRun(EngineStartupState)` are internal friend-only composition seams, not public startup APIs. `SealForGame` is the sole constructor for the state, which has no public constructor/copy/with API. The state shell carries defensive-copied configuration/arguments, exact selection, runtime identity, sealed provider, settings snapshot, Fluent mode, and loader decorator. `Game.InitializeAndRun(EngineStartupState)` atomically calls the internal `TakeOwnership` at entry; from that point a retained alias can only observe an inert consumed shell and its `Dispose` cannot dispose the transferred payload. The owned payload survives sequential same-mod reconstruction and is disposed once at final game shutdown. The existing public `InitializeAndRun(string[])` remains the desktop/legacy entry and internally creates a fresh equivalent `Legacy` state with empty services, the trusted loader, a snapshot of the settings it just loaded, and `PublishGlobal`. Public `RuntimeContractIdentity` and `TrustedManifestSelection` remain validated handshake/data scalars only: code outside Game plus the audited iOS TCB cannot seal them into a profiled startup state. Task 14's final IL gate permits exactly one TCB call edge to profiled `SealForGame`, from the coordinator after the exact Ready/binding comparison and pair-authorized service creation; moving or adding that call fails. Tests compile a differently named ordinary consumer and require attempts to name the state, call profiled Seal, or call the state overload to fail, while public scalar construction alone grants no startup authority. Later map/cache/import tasks inject policy/client/store/catalog through this internal seam and add no static global setters; `IModDataBoundService` is the only instance binding path and never stores a static/current `ModData`.

`Game.InitializeAndRun(EngineStartupState)` has one outer `try/finally` that begins immediately after ownership transfer and encloses every startup phase, `Run`, every `Loop` exit, and every same-mod reconstruction. Its shutdown ledger is total and ordered: first stop/unload/clear every constructed world, order manager, server, network/session callback, and other gameplay-runtime producer; then for `Game.ModData` drain the runtime-store activation, drain the ModData-owned MapCache preview worker/GPU resources, drain the dispatcher activation, dispose those quiescent tokens, dispose bind tokens in reverse registration order, and clear/dispose the object; then dispose the owned startup/service provider; then release `Chrome`, `Sound`, `Haptics`, `Renderer`, and every other platform/UI engine resource actually acquired by the existing startup path. On same-mod reconstruction, `InitializeMod` first stops producers but must not replace `delayedActions`, unmount `ModFiles`, clear `Game.ModData`, or dispose a ModData-owned package before those drains. It pumps store → MapCache preview → dispatcher through the old lifecycle lane, unbinds store → registry → dispatcher, disposes/unmounts the old ModData, closes the old lane, and only then replaces the legacy `delayedActions` queue and constructs the new lane/ModData. Thus an `InitializeMod` call already executing inside legacy `RunAfterTick` never reenters that `ActionQueue`. The shutdown pump is internal to `Game`, asserts the captured game thread, executes only the old lane's admitted FIFO until each exact fence completes, waits on its late-ticket-or-fence signal rather than joining a task, and is unavailable during normal gameplay. Each ledger stage is wrapped in its own nested `try/finally`, so a fault from one drain/stop/dispose cannot skip any later stage, and each field is atomically marked released before its potentially throwing cleanup. A real-API regression invokes same-mod `InitializeMod` from inside `Game.RunAfterTick`, queues an unrelated legacy action behind it, races a pointer-replaced service worker, and holds a preview decode/upload on the old lane after drainage begins. The pump must execute each admitted commit/release exactly once, must dispose the old sheet/atlas on the game thread, must not execute or reenter the unrelated legacy action, must wake for every late-post/fence signal, and must not replace the old queue or unmount the old ModData until both activation fences, the preview fence, and all three unbinds complete. Tests also reject posting an old-lane ticket into the reconstructed ModData and require every acquired resource/callback/GPU object to be stopped or released exactly once.

`Manifest.Get<T>(ObjectCreator)` performs lazy default-module creation through the supplied creator and never reads `Game.ModData`; all new pre-game, ModData-construction, candidate, and runtime-validation call sites use it. The parameterless `Get<T>()` remains only as a characterized desktop compatibility adapter and delegates with the current game's creator. Tests install a throwing global sentinel and prove the explicit overload, including missing/default modules such as `DefaultPlayer`, succeeds through the supplied creator while the legacy overload retains its old behavior.

`OpenModData` and `SealForGame` each consume one fresh options shell. The context exposes only its defensive settings snapshot; that sealed class privately owns its mutable `Dictionary<string, Hotkey>` and never exposes it publicly. `Isolated` creates a new independent working dictionary for each pre-game ModData and never assigns `Game.Settings.Keys`. `PublishGlobal` is accepted only by `SealForGame`; when Game consumes the startup payload it atomically installs the snapshot's one transferred dictionary as `Game.Settings.Keys` before constructing `HotkeyManager`, and ModData receives that exact same dictionary reference. Same-mod reconstruction reuses it, so hotkey edits are what `Settings.Save()` serializes and a fresh `Settings` reload reads back. Failure before installation leaves global keys unchanged; failure afterward follows the outer startup rollback contract without letting a pre-game copy overwrite globals. Tests edit a hotkey through the constructed manager, save, reload, and require equality; they also mutate isolated keys and prove `Game.Settings.Keys`, saved bytes, and later PublishGlobal state remain unchanged. `OpenModData` rejects PublishGlobal or `Game` rendering before transfer; `SealForGame` rejects Isolated or `NoRender` before transfer. The decorator is invoked once per ModData and captures no raw staging root.

`Platform.cs` replaces the two independent one-shot directory mutations on this path with one internal atomic `ConfigureDirectories(engineDir, supportDir)`: normalize/validate both before assignment, accept a later byte-identical normalized pair, and reject a different or partial pair without changing either value. `PreGameEngineContext.Open` calls that pair operation once, then constructs local `Settings`, `InstalledMods`, and the settings snapshot from the defensive argument copy; normal startup reuses the same accepted pair. It never assigns `Game.Settings` or `Game.ModData`. `ModData` reads hotkeys only from the snapshot. `FluentInitializationMode.Isolated` suppresses the constructor's global `FluentProvider.Initialize` call and is mandatory for every pre-game scope; pre-game validation uses explicit `Manifest.Get<T>(ObjectCreator)` and never requests translated global metadata or calls `InitializeLoaders`. Normal desktop/game construction alone uses `PublishGlobal`. Tests capture Platform directories plus object identity and representative values for `Game.Settings`, `Game.ModData`, `FluentProvider.CurrentLanguage`, and one known Fluent message before successful construction, failed construction at every phase, failed service bind, and disposal. Platform may change only on the first successful atomic pair; all four game/Fluent values remain byte/object-identical throughout pre-game. Tests also prove identical normalized pairs are idempotent, different/partial pairs leave Platform unchanged, and no pre-game legacy current-ModData adapter exists.

- [ ] **Step 2: Run RED**

```bash
set -euo pipefail
TASK3_RED="$(mktemp -d /private/tmp/nuke-hour-task3-red.XXXXXX)"
TASK3_PYTHON=/private/tmp/nuke-hour-task0-python311/bin/python
test -x "$TASK3_PYTHON"

# The Task 0 three-test RED is closed by fixture correction only.
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailContentImporterTest' -v minimal \
  > "$TASK3_RED/importer-fixture-green.log" 2>&1

set +e
"$TASK3_PYTHON" -m unittest packaging.tests.test_public_brand_delta -v \
  > "$TASK3_RED/public-brand.log" 2>&1
PUBLIC_RED_RC=$?
set -e
test "$PUBLIC_RED_RC" -ne 0
rg -n 'FAILED|AssertionError' "$TASK3_RED/public-brand.log"

set +e
"$TASK3_PYTHON" -m unittest packaging.tests.test_exact_git_stage -v \
  > "$TASK3_RED/exact-stage.log" 2>&1
EXACT_STAGE_RED_RC=$?
set -e
test "$EXACT_STAGE_RED_RC" -ne 0
rg -n 'ImportError|ModuleNotFoundError|No module named|ios-retail-task-paths' \
  "$TASK3_RED/exact-stage.log"

set +e
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailValidationClockIdentityTest' \
  -v minimal > "$TASK3_RED/clock.log" 2>&1
CLOCK_RED_RC=$?
set -e
test "$CLOCK_RED_RC" -ne 0
rg -F 'RetailValidationClockIdentityTest' "$TASK3_RED/clock.log"
rg -n 'Failed|FAILED' "$TASK3_RED/clock.log"
! rg -n 'error CS[0-9]+' "$TASK3_RED/clock.log"

set +e
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~TrustedManifestEntryTest|FullyQualifiedName~PreGameEngineContextTest|FullyQualifiedName~Ra2RuntimeProfileManifestTest|FullyQualifiedName~RetailCandidateProfileValidationTest|FullyQualifiedName~ArgumentsSnapshotTest' \
  -v minimal > "$TASK3_RED/missing-api.log" 2>&1
API_RED_RC=$?
set -e
test "$API_RED_RC" -ne 0
rg -n 'error CS(0246|1061)|does not exist in the current context' \
  "$TASK3_RED/missing-api.log"
```

Expected: the public-brand Python test first fails on the absent 11-remove/17-mapping/580-delta contract; the exact-stage suite then fails before its module/manifest implementation exists; the clock-only C# run compiles and fails its two delivered-behavior assertions; the later combined C# run fails on missing trusted-entry/pre-game APIs. Do not accept either Python test first introduced after its production changes or hide the clock RED behind those compile failures.

- [ ] **Step 3: Implement fixed profile selection and candidate validation**

```csharp
public enum Ra2RuntimeProfileId
{
	Base = 1,
	Yuri = 2,
}

[Flags]
public enum Ra2MapCompatibleProfiles
{
	None = 0,
	Base = 1,
	Yuri = 2,
}

public sealed class Ra2RetailMountRequirement
{
	public RetailContentGroup Group { get; }
	public string SafeBasename { get; }
	public string MountName { get; }
	public bool Required { get; }
	internal Ra2RetailMountRequirement(
		RetailContentGroup group, string safeBasename,
		string mountName, bool required);
}

public sealed class Ra2RetailMountContract
{
	public Ra2RuntimeProfileId Profile { get; }
	public ImmutableArray<Ra2RetailMountRequirement> Mounts { get; }
	public static RetailOperationResult<Ra2RetailMountContract> Create(
		Ra2RuntimeProfileId profile,
		RetailArchiveGraphResult archives);
}

public sealed class RetailMapProfileEvidence
{
	public string CanonicalMapUid { get; }
	public Ra2MapCompatibleProfiles CompatibleProfiles { get; }
	internal static RetailMapProfileEvidence FromValidatedMap(
		string canonicalMapUid,
		Ra2MapCompatibleProfiles compatibleProfiles);
}

public sealed class RetailProfileValidation
{
	public Ra2RuntimeProfileId SelectedProfile { get; }
	public Ra2RetailMountContract MountContract { get; }
	public ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> Groups { get; }
	public ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure> GroupFailures { get; }
	public ImmutableArray<RetailMapProfileEvidence> Maps { get; }
	internal RetailProfileValidation(
		Ra2RuntimeProfileId selectedProfile,
		Ra2RetailMountContract mountContract,
		ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> groups,
		ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure> groupFailures,
		ImmutableArray<RetailMapProfileEvidence> maps);
}

public sealed class RetailProfileValidator
{
	public RetailProfileValidator(IRetailMonotonicClock clock);
	public RetailOperationResult<RetailProfileValidation> Validate(
		RetailArchiveGraphResult archives,
		ImmutableArray<RetailMapProfileEvidence> maps,
		RetailValidationDeadline alreadyStartedWholeValidationDeadline,
		CancellationToken cancellationToken);
}

public sealed class ValidatedMapPlayerMetadata
{
	public string Name { get; }
	public string Palette { get; }
	public string Bot { get; }
	public string StartingUnitsClass { get; }
	public bool AllowBots { get; }
	public bool Playable { get; }
	public bool Required { get; }
	public bool OwnsWorld { get; }
	public bool Spectating { get; }
	public bool NonCombatant { get; }
	public bool LockFaction { get; }
	public string Faction { get; }
	public bool LockColor { get; }
	public Color Color { get; }
	public CPos HomeLocation { get; }
	public bool LockSpawn { get; }
	public int Spawn { get; }
	public bool LockTeam { get; }
	public int Team { get; }
	public bool LockHandicap { get; }
	public int Handicap { get; }
	public ImmutableArray<string> Allies { get; }
	public ImmutableArray<string> Enemies { get; }
	internal static ValidatedMapPlayerMetadata CreateValidated(
		string name, string palette, string bot, string startingUnitsClass,
		bool allowBots, bool playable, bool required, bool ownsWorld,
		bool spectating, bool nonCombatant, bool lockFaction, string faction,
		bool lockColor, Color color, CPos homeLocation, bool lockSpawn,
		int spawn, bool lockTeam, int team, bool lockHandicap, int handicap,
		ImmutableArray<string> allies, ImmutableArray<string> enemies);
}

public sealed class ValidatedMapMetadata
{
	public string Title { get; }
	public string Author { get; }
	public ImmutableArray<string> Categories { get; }
	public string Tileset { get; }
	public MapGridType GridType { get; }
	public int2 MapSize { get; }
	public Rectangle Bounds { get; }
	public ImmutableArray<ValidatedMapPlayerMetadata> Players { get; }
	public ImmutableArray<CPos> SpawnPoints { get; }
	public int PlayablePlayers { get; }
	internal static ValidatedMapMetadata CreateValidated(
		string title, string author, ImmutableArray<string> categories,
		string tileset, MapGridType gridType, int2 mapSize, Rectangle bounds,
		ImmutableArray<ValidatedMapPlayerMetadata> players,
		ImmutableArray<CPos> spawnPoints);
}

public interface IRa2MapSourceHandle : IDisposable
{
	string SafeName { get; }
	long Length { get; }
	string SourceSha256 { get; }
	Stream OpenVerifiedRead();
}

public static class Ra2RuntimeProfiles
{
	public static TrustedManifestSelection ManifestSelection(
		this Ra2RuntimeProfileId profile) => profile switch
	{
		Ra2RuntimeProfileId.Base => new("ra2", "mod.ios-base.yaml"),
		Ra2RuntimeProfileId.Yuri => new("ra2", "mod.ios-yuri.yaml"),
		_ => throw new ArgumentOutOfRangeException(nameof(profile)),
	};
}

public sealed class Ra2MapImportContext : IDisposable
{
	private Ra2MapImportContext(
		PreGameModDataScope ownedScope,
		Ra2RuntimeProfileId profile,
		Func<RetailStagingTarget, IReadWritePackage> outputFactory);
	public static RetailOperationResult<Ra2MapImportContext> Create(
		PreGameModDataScope ownedScope,
		Ra2RuntimeProfileId profile,
		Func<RetailStagingTarget, IReadWritePackage> outputFactory);
	public Ra2RuntimeProfileId Profile { get; }
	public ModData ModData { get; }
	public Func<RetailStagingTarget, IReadWritePackage> CreateOutputPackage { get; }
	public void Dispose();
}

public sealed class Ra2MapValidationContext : IDisposable
{
	private Ra2MapValidationContext(
		PreGameModDataScope ownedScope,
		Ra2RuntimeProfileId profile);
	private Ra2MapValidationContext(
		ModData borrowedModData,
		IDisposable ownedBindingPin,
		Ra2RuntimeProfileId profile);
	public static RetailOperationResult<Ra2MapValidationContext> Create(
		PreGameModDataScope ownedScope,
		Ra2RuntimeProfileId profile);
	internal static RetailOperationResult<Ra2MapValidationContext> CreateBound(
		ModData borrowedModData,
		IDisposable ownedBindingPin,
		Ra2RuntimeProfileId profile);
	public Ra2RuntimeProfileId Profile { get; }
	public ModData ModData { get; }
	public void Dispose();
}

internal sealed class IosRuntimeResourcePreflightResult
{
	private IosRuntimeResourcePreflightResult(
		bool staticResourcesPresent, RetailImportFailure failure);
	public bool StaticResourcesPresent { get; }
	public RetailImportFailure Failure { get; }
	internal static IosRuntimeResourcePreflightResult Present();
	internal static IosRuntimeResourcePreflightResult Missing(
		RetailImportFailure failure);
}

internal interface IIosRuntimeResourceLease : IDisposable
{
	string CanonicalRelativePath { get; }
	long Length { get; }
	Stream OpenVerifiedRead();
	void VerifyUnchanged();
}

internal interface IIosRuntimeResourceRoot : IDisposable
{
	RetailOperationResult<IIosRuntimeResourceLease> AcquireRegularFile(
		string canonicalRelativePath);
}

internal sealed class IosRuntimeResourceRoot : IIosRuntimeResourceRoot
{
	internal static RetailOperationResult<IosRuntimeResourceRoot> OpenBundleRoot(
		string trustedBundleResourceRoot);
	public RetailOperationResult<IIosRuntimeResourceLease> AcquireRegularFile(
		string canonicalRelativePath);
	public void Dispose();
}

public enum RuntimeContractResourcePurpose
{
	Contract = 1,
	PreflightOnly = 2,
}

public sealed class RuntimeContractResourceDescriptor
{
	public string LogicalName { get; }
	public string CanonicalBundleRelativePath { get; }
	public long Length { get; }
	public string Sha256 { get; }
	public ImmutableArray<Ra2RuntimeProfileId> Profiles { get; }
	public RuntimeContractResourcePurpose Purpose { get; }
	internal RuntimeContractResourceDescriptor(
		string logicalName,
		string canonicalBundleRelativePath,
		long length,
		string sha256,
		ImmutableArray<Ra2RuntimeProfileId> profiles,
		RuntimeContractResourcePurpose purpose);
}

public sealed class RuntimeContractDescriptorInventory
{
	public int SchemaVersion { get; }
	public int DescriptorCount { get; }
	public string ShapeDigest { get; }
	public ImmutableArray<RuntimeContractResourceDescriptor> Descriptors { get; }
	internal RuntimeContractDescriptorInventory(
		int schemaVersion,
		int descriptorCount,
		string shapeDigest,
		ImmutableArray<RuntimeContractResourceDescriptor> descriptors);
}

internal interface IIosRuntimeResourcePreflight
{
	IosRuntimeResourcePreflightResult Check(
		Manifest resolvedManifest,
		IIosRuntimeResourceRoot distributionRoot,
		Ra2RuntimeProfileId profile,
		RetailValidationDeadline alreadyStartedDeadline,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken);
}

internal sealed class IosRuntimeResourcePreflight : IIosRuntimeResourcePreflight
{
	internal IosRuntimeResourcePreflight(
		RuntimeContractDescriptorInventory inventory);
	public IosRuntimeResourcePreflightResult Check(
		Manifest resolvedManifest,
		IIosRuntimeResourceRoot distributionRoot,
		Ra2RuntimeProfileId profile,
		RetailValidationDeadline alreadyStartedDeadline,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken);
}
```

`ModDataConstructionOptions.FileSystemLoaderDecorator` receives the normally configured loader and wraps only the candidate `content` mount. It exposes canonical preserved-active or staged files and is disposed/restored in `finally`; it never points SupportDir at staging.

`PreGameModDataScope.TakeOwnership()` is the sole public cross-assembly ownership-transfer seam. It allocates a fresh empty destination before atomically exchanging the exact `ModData` reference from the live source to `null`. Against one source shell, exactly one concurrent take can win; the winner returns the only new owner without disposing the payload, while every retained source alias becomes inert (`ModData == null`, `Dispose()` is a no-op, and a later take throws `InvalidOperationException`). A take racing source disposal has one linearized outcome: if disposal wins no owner escapes; if the take wins, later source disposal cannot affect the returned owner. The final owner disposes the payload exactly once.

The RA2-layer `Ra2MapImportContext.Create` validates every argument before transfer, then consumes the engine-neutral `PreGameModDataScope` and wraps it with the fixed profile/output factory; on any failed result it disposes the passed scope. `Ra2MapValidationContext.Create` is the no-output sibling. The engine assembly never references an RA2 type. Each successful pre-game context exclusively owns its no-render scope, is sequential, and on disposal releases only that scope and drops the import variant's factory reference; it never reads or writes `Game.ModData`. Every pre-game context is destroyed before normal startup. Tests prove exact ownership on every validation/factory failure, atomic single-winner transfer including transfer-versus-source-disposal races, source-alias inertness, use-after-transfer rejection, and zero `Game.ModData` reads/writes.

For post-start runtime downloads, `CreateBound` creates `Ra2MapValidationContext` from the explicitly bound loaded `ModData` plus an owned ref-counted binding pin; this variant borrows the loaded instance and releases only its pin on disposal. It validates all inputs before taking the pin, disposes the passed pin on a failed result, and is callable only by the instance-scoped Task 12 factory after `IModDataBoundService.Bind`, never from a static/current adapter. No RA2 context reads or replaces `Game.ModData`.

`Ra2RetailMountContract.Create` has one success shape per closed profile. Base contains the required ordered pairs `(Base, ra2.mix, ra2.mix)` and `(Base, language.mix, language.mix)`; Yuri contains those two followed by `(Yuri, ra2md.mix, ra2md.mix)` and `(Yuri, langmd.mix, langmd.mix)`. The input is Task 2's structural graph, so `Recognition=NotApplicable` is required and accepted—it is not an "unrecognized edition" failure. The first enum/root-ordered missing or Invalid required row returns `RequiredMountFailure` with that group, fixed basename, and the graph's closed diagnostic; no path/stream/package or partial contract escapes. The private constructor defensively copies, rejects default/duplicate/case-alias/out-of-order rows, and public callers cannot instantiate requirements or a contract. Neither music file is emitted by Task 3: a structurally valid `theme.mix` and `thememd.mix` both remain authenticated but `Unavailable` until a later explicitly scoped two-stage track-definition-plus-mounted-track proof exists.

`RetailProfileValidator.Validate` is the only public profile-selection API. It borrows the owned graph, accepts only internally constructed map evidence, and never disposes/reopens a graph/path/package/stream. Its constructor captures the sole `IRetailMonotonicClock`; `Validate` rejects a default deadline, checks the supplied cancellation token and that same already-started whole deadline before selection, at every one of five group rows, every required mount, and every map item, and never starts/refills a budget. Matching cancellation may propagate; expiry becomes `ValidationTimedOut/DeadlineExceeded`. Success returns only the sealed/private validation class, a private mount contract, copied canonical map evidence, an exact five-key enum-sorted status dictionary, and an immutable enum-sorted `GroupFailures`. That failure map contains exactly one unchanged sanitized failure for every and only optional group whose final status is `Invalid`; required-group invalidity still fails the whole operation, while Missing/Unavailable/Ready groups have no entry. Each key must equal `failure.Group`, and code/diagnostic/safe basename must equal the Task 2 source failure after any closed profile-level remap. The constructor rejects missing, extra, reordered, duplicated, or mismatched entries. Public records, init setters, `with` copies, public success factories, arbitrary evidence implementations, and subclassing cannot forge a success/evidence object.

Task 3 is the first compatibility stage, not the edition catalog. A structurally Ready required Base/Yuri group enters as `NotApplicable` and is promoted to `Ready/CompatibleUnrecognized` only when its closed profile contract succeeds; Task 3 never emits `Recognized`. A later explicit trusted edition-fingerprint catalog may promote that value without changing compatibility. Required mounts accept both `CompatibleUnrecognized` and later `Recognized`, and reject only `NotApplicable` after profile validation. A complete Yuri pair with no Yuri-compatible evidence becomes `Unavailable/CompatibleUnrecognized` and Base is selected; adding the first internally validated Yuri-compatible receipt later selects Yuri without reopening archives. Partial/corrupt Yuri stays quarantined with its exact `GroupFailures[Yuri]` entry while Base is considered independently; the same rule applies independently to both optional music groups. No Base-compatible map returns `ProfileUnavailable/Maps/null/InvalidSpawnLayout`; a required mount failure uses the exact shape above; an inconsistent sealed evidence/graph selection returns `ProfileContextFailure/null/null/InvalidMapPackage`. These and timeout/cancellation are the only outcomes. Tests cover the complete cross-product, exact failure-map tuples/order, NotApplicable-to-CompatibleUnrecognized promotion, impossibility of Task-3 Recognized, and reflection/API-shape rejection of forgeable evidence/results.

`ValidatedMapMetadata` and `ValidatedMapPlayerMetadata` are sealed private-construction evidence, not records. The internal factories validate all values before retaining defensive copies; Task 10 is the only production caller. Players are sorted by canonical ordinal name; spawn order is preserved; allies/enemies are copied and ordinal-sorted. Optional source strings remain runtime null under the no-nullable-annotation project instead of acquiring sentinels. Every non-null string is NFC, has no NUL/control/newline, and has UTF-8 length `1..MapYamlScalarBytes`. Categories are capped at `RemoteCategories` (64); players, each allies set, and each enemies set are capped at `MapPlayerDefinitions` (63); spawns are capped at `MapSpawns` (8); the combined projection is capped at `ResidentStringEntries` and `PerMapStringBytes`. All collections reject default arrays, null elements, ordinal duplicates/case aliases, and dangling player references before copying.

`MapSize` is the engine's exact `int2 Map.MapSize` projection. Both axes must be positive and no greater than `TraditionalMapWidth`/`TraditionalMapHeight`, and checked size area is no greater than `TraditionalMapCells`. Bounds use checked `long` right/bottom arithmetic, have positive dimensions, start nonnegative, and their right/bottom must lie inside MapSize; checked Bounds area is also capped at `TraditionalMapCells`. Only `Rectangular` and `RectangularIsometric` are accepted. Every spawn and every nondefault home first requires layer zero; for isometric maps reject `X < Y`, then convert with `CPos.ToMPos(GridType)` and test the resulting `MPos` against Bounds. `HomeLocation == CPos.Zero` is the stock "unspecified" sentinel and is accepted without a Bounds test; every nonzero home is grid-validated. A checked-in stock RA2 map characterization proves both MapSize and this sentinel case. Tests cover MapSize and Bounds axes/area/right/bottom at limit ±1, checked overflow, every collection cap ±1, aggregate strings/bytes, isometric X<Y/layer, converted-boundary cells, and mutable caller collections. Codecs/active views later preserve this evidence and never reopen `map.yaml` to reconstruct it.

`IosRuntimeResourceRoot` in the testable RA2 assembly is the real macOS/iOS production adapter. `OpenBundleRoot` validates the trusted platform-provided bundle-resource root once, opens and owns a directory descriptor, and retains no public path. `AcquireRegularFile` accepts only a canonical relative path, walks every parent with descriptor-relative `openat(O_DIRECTORY|O_NOFOLLOW)`, opens the leaf with `O_RDONLY|O_NOFOLLOW`, then `fstat`s the opened descriptor and requires a regular file. It never performs check-then-path-open, follows a symlink, accepts a device/FIFO/socket/directory, or falls back to `File.Open`. The lease binds canonical name, every retained parent identity, leaf device/inode, length, and the pinned descriptor; `OpenVerifiedRead` is one-shot and duplicates/transfers only that descriptor after an identity/length recheck. `VerifyUnchanged` walks again from the still-retained root descriptor without following links and requires the current canonical leaf to resolve to the same retained parent chain and leaf device/inode/type/length, so unlink-and-same-name replacement is detected even though reads remain pinned to the original descriptor. An open child stream remains valid after root/lease disposal. Root, lease, and stream each own exactly one descriptor reference; success, nth-parent failure, hash failure, use-after-dispose, alias/double disposal, and all TOCTOU swaps close every reference once.

`RuntimeContractResourceDescriptor` and `RuntimeContractDescriptorInventory` are delivered in Task 3 because preflight must consume the exact same generated data later used by the contract builder. Their internal constructors defensively copy, reject default/null arrays, invalid paths/profiles/purpose/length/hash, duplicate/case-aliased/out-of-order rows, and any schema/count/shape combination other than the authenticated golden projection; they recompute the canonical framed shape from descriptor shape fields. Generated iOS source exposes exactly one `internal static RuntimeContractDescriptorInventory RuntimeContractDescriptors` property on `GeneratedAotObjectRegistryRegistration`, constructed from literal rows and no source paths. Only that generated property and the existing `OpenRA.iOS`/`OpenRA.Test` friend paths may invoke the constructors; reflection tests require zero public constructors, setters, init accessors, record copy, or `with` path. Task 14's iOS composition reads only that property; no runtime JSON/parser/public factory/alternate array exists.

`IosRuntimeResourcePreflight` enforces a static-resource distribution/mount contract, not gameplay. Its constructor requires and defensively retains the Task 3 generated inventory; no static/ambient inventory lookup exists. `Check` accepts only the explicit root capability plus the caller's same already-started validation deadline, identical clock instance, and token. It rejects a default deadline/clock mismatch, checks cancellation and expiry before every descriptor acquisition, before and after every read chunk/hash update, before reauthenticating the selected fixed projection, and before every final `VerifyUnchanged`, and never starts or refills a budget. A matching cancelled token returns the typed `Cancelled/null/null/None` failure and expiry returns typed `ValidationTimedOut/null/null/DeadlineExceeded`; neither escapes as a raw exception from this preflight boundary. The authenticated generated inventory defines exactly two fixed global-ordinal applicable projections: Base has 277 expected descriptors and Yuri has 284. `Check` validates the selected manifest/profile, selects exactly that fixed projection, acquires each expected `CanonicalBundleRelativePath` once through `AcquireRegularFile`, hashes the pinned stream, reauthenticates the same immutable projection, and immediately before success calls `VerifyUnchanged` on every retained expected-name lease. It releases all leases in one reverse-order `finally`; a failure at any point still retains earlier leases until that common cleanup. The root is acquire-by-name and exposes no inventory enumeration, so preflight neither discovers nor rejects unrelated extra files and makes no no-extra claim; exact source/evaluated-project and final `.app`/IPA bundle audits own that proof. Task 4 reuses these exact Task 3 types/data and refactors this same implementation into the expected-to-verified contract signature without introducing or removing rows; the Task 3 signature is an isolated infrastructure/behavior baseline, not a production startup call. Missing/escaped/symlink/nonregular/changed/hash-mismatched expected resources map to `RuntimeResourcesMissing/null/null/InvalidMapPackage`, never raw exceptions/paths. Only `IosRuntimeResourcePreflight` can call the internal `Present`/`Missing` factories, so invalid success/failure combinations and forged evidence are unrepresentable. Success may say only “runtime resources present,” never “gameplay verified.” Mac-hosted tests execute the real adapter for exact Base-277/Yuri-284 acquisition and verification sequences, parent/leaf symlinks, expected-name remove/rename/replace races (including replacing the first-read name after its read but before final expected-name verification), an unrelated extra that is never acquired, directory/device/FIFO rejection, one-byte tamper, timeout/cancellation at every checkpoint, reverse disposal order, and a complete Personal fixture; iOS simulator Release build proves the same class AOT-compiles. Task 8 never calls static preflight; Task 14 owns the sole final Task 4 instance and a Ready retail receipt cannot bypass it.

`RetailProfileValidator` makes Yuri eligible only after the complete pair passes the closed contract; Task 10 supplies sealed semantic map evidence for final selection. Yuri is selected only when at least one final receipt includes Yuri, while Base always requires at least one Base-compatible map. Candidate profile, required mounts, rule/sequence load, and selected-profile map count all pass before generation preparation. Task 3 deliberately keeps both music groups `Unavailable`: it defines no `Ready` transition, optional music mount, track-evidence DTO, or success factory. A later task may change that only by adding and testing a two-stage sealed proof that definitions register and every advertised track resolves from the pinned mount. Tests lock this honest state for absent, valid, invalid, and preserved music files.

- [ ] **Step 4: Split manifests without changing desktop semantics**

Keep `mods/ra2/mod.yaml` and all nine mixed desktop sources byte-for-byte unchanged: `rules/{world,defaults,proxy-actors,allied-infantry,soviet-infantry,soviet-vehicles}.yaml`, `sequences/{vehicles,voxels}.yaml`, and `audio/voices.yaml`. The desktop entry remains flat and continues to reference only those original files; it never references a new Base/Yuri overlay and therefore retains exact existing semantics. Do not teach direct-text audits or platform packagers to resolve Include, and do not move desktop metadata/version/loadscreen/branding/filesystem sections. Existing iOS loading/touch/skin/artifact, macOS/cross-platform branding, packaging script, installer-manifest, V3-impact, menu/settings/viewport, and public-branding tests run unchanged as characterization gates.

The two iOS roots use repeated package-relative scalar Includes exactly as written below; there is no child list and no `ra2|` prefix:

```yaml
# mod.ios-base.yaml
Include: manifest/common.yaml
Include: manifest/gameplay-base.yaml
Include: manifest/filesystem-ios-base.yaml

# mod.ios-yuri.yaml
Include: manifest/common.yaml
Include: manifest/gameplay-yuri.yaml
Include: manifest/filesystem-ios-yuri.yaml
```

All six fragments contain no Include. `filesystem-desktop.yaml` is an exact enumerated semantic fixture only and is referenced by neither desktop nor iOS entry. `common.yaml` contains exactly these non-gameplay/non-filesystem desktop nodes in their original order, with each scalar and subtree MiniYaml-equivalent to `mod.yaml`: `Metadata`, `ModCredits`, `Cursors`, `Chrome`, `Assemblies`, `ChromeLayout`, `AllowUnusedFluentMessagesInExternalPackages`, `LoadScreen`, `ServerTraits`, `ChromeMetrics`, `Fonts`, `DefaultOrderGenerator`, `SupportsMapsFrom`, `SoundFormats`, `SpriteFormats`, `VideoFormats`, `TerrainFormat`, `SpriteSequenceFormat`, `AssetBrowser`, and `GameSpeeds`; immediately after them it adds the one iOS-only reserved node whose exact MiniYaml is:

```yaml
RuntimeContractCultures:
	zh-CN:
```

That node is contract metadata and is excluded from the gameplay semantic-equivalence digest. The classifier rejects an omitted, duplicated, reordered, or newly unclassified desktop top-level node and rejects any other iOS-only node/culture. `gameplay-base.yaml` mirrors each desktop gameplay list in order, substitutes the nine `*-base.yaml` files for the nine mixed paths, and omits exactly the four `rules/yuri-{infantry,structures,vehicles,naval}.yaml`, two `sequences/yuri-{infantry,structures}.yaml`, and `weapons/yuri.yaml` whole files. `gameplay-yuri.yaml` is a complete standalone gameplay manifest—not an overlay—and its Rules, Sequences, ModelSequences, Weapons, Voices, Notifications, TileSets, MapGrid, Music, FluentMessages, and Hotkeys arrays are element-for-element in the exact desktop order, directly referencing all nine unchanged mixed files and all seven Yuri whole files. Yuri never loads a `*-base.yaml`; Base never loads an original mixed file. The unchanged `rules/ai.yaml` stays in both complete lists because its `yuri` token is the original RA2 unit; a drift test fails on an expansion-only AI dependency.

Each `*-base.yaml` is one controlled complete copy of its unchanged mixed source minus only the closed Base-removal table below. There are no Yuri overlays and no attempt to reconstruct source ordering with MiniYaml merge. Yuri directly loads the nine originals, so its effective node and trait order is byte/element identical to desktop.

`world-base.yaml` changes `Faction@random.RandomFactionMembers` to exactly `random-allies, random-soviets` and removes `Faction@yuri` plus `StartingUnits@pcv`, `@lightyuri`, `@mediumyuri`, and `@heavyyuri`. `proxy-actors-base.yaml` removes only `yuri.tech.infiltrated`. `defaults-base.yaml` removes complete `^Berserkable`/`^MagneticLiftable`; `^Infantry.Inherits@CHAOS`, its `MutationDeath`, and `SpawnActorOnDeath@brute`; the seven CHAOS removals plus `-SpawnActorOnDeath@brute` from `^Animal`; CHAOS/MAGNE inherits from `^Vehicle`/`^Ship`; and only magnetic target conditions. Base Vehicle locks `Targetable.RequiresCondition: !parachute` and `Repairable.RepairActors: gadept, nadept, caoutp`; Base Ship has no Targetable condition and locks `Repairable.RepairActors: gayard, nayard`; Base Animal retains `-SpawnActorOnDeath@flames`. Thus `yadept` and `yayard` are removed from Base. The seven CHAOS removals are exactly `-FirepowerMultiplier@CHAOS`, `-Berserkable@CHAOS`, `-RejectsOrders@CHAOS`, `-WithColoredOverlay@CHAOS`, `-TimedConditionBar@CHAOS`, `-Targetable@CHAOS`, `-ExternalCondition@CHAOS`.

`allied-infantry-base.yaml` removes from `dog` those seven CHAOS nodes plus `-SpawnActorOnDeath@brute`; `soviet-infantry-base.yaml` and `soviet-vehicles-base.yaml` remove exactly the seven CHAOS nodes from original RA2 `yuri` and `dron`. `vehicles-base.yaml` removes exact actor keys `ltnk, ytnk, pcv, smin, caos, tele, mind, disk, yhvr, bsub`; `voxels-base.yaml` removes `ltnk, ytnk, caos, tele, mind, disk, yhvr, bsub, bsubmisl, pcv, smin, yaggun`; `ltnk` remains Yuri-only. `voices-base.yaml` ends after complete `V3LauncherVoice`, removing the ten-node tail `GGIVoice, BattleFortressVoice, RobotTankVoice, SiegeChopperVoice, LasherTankVoice, GatlingTankVoice, SlaveMinerVoice, SlaveVoice, InitiateVoice, BruteVoice`, while retaining earlier original `YuriVoice`.

The Base drift audit treats this as a closed no-Yuri's-Revenge node/token table: no `Faction@yuri`, Yuri starting-unit key, `yuri.tech.infiltrated`, `CHAOS`, `MAGNE`, `MutationDeath`, brute spawn/removal, `yadept`, `yayard`, removed sequence/voxel key, or ten removed voice key may occur in the nine Base copies. The only reviewed name exceptions are the original RA2 `yuri` actor and its earlier `YuriVoice`; a raw global substring ban is forbidden. The Yuri test requires the nine original path lists and effective gameplay digest to equal desktop exactly, including original trait order and the full values `gadept, nadept, yadept, caoutp` / `gayard, nayard, yayard`.

Before creating any Base copy, the RED-test author resolves the untouched desktop entry with the real MiniYaml loader and records inside `Ra2RuntimeProfileManifestTest.cs` the SHA-256/size of `mod.yaml` plus all nine originals, one versioned ordered effective-gameplay digest, and one ordered full-manifest semantic fixture partitioned into common/gameplay/filesystem nodes. No generated/post-copy fixture is accepted. GREEN rechecks every desktop byte, proves each Base copy equals its source minus exactly the closed table, resolves both repeated-Include entries, and requires the resolved Yuri entry—not merely its gameplay lists—to equal the complete desktop manifest on every common/gameplay node and every filesystem node except the two explicitly classified iOS substitutions: the active-catalog content mount and the exact MapFolders pair below. It requires Base to contain only the reviewed subtraction, rejects every `*-yuri.yaml` profile path, and proves neither profile references the other's filesystem or Base-copy set.

All filesystem fragments retain the existing `PackageFormats`, `ContentInstallerFileSystem`, SystemPackages order/aliases, canonical mod ID `ra2`, active mount `content`, ContentInstallerMod, and map identity. `filesystem-desktop.yaml` alone enumerates the unchanged desktop `MapFolders`, including `~^SupportDir|maps/ra2/{DEV_VERSION}: User`. Both `filesystem-ios-base.yaml` and `filesystem-ios-yuri.yaml` instead contain exactly this ordered mapping, using normal MiniYaml mapping syntax (tab indentation in the file):

```yaml
MapFolders:
	ra2|maps: System
	content|maps: User
```

No iOS profile contains or resolves `~^SupportDir|maps/ra2/{DEV_VERSION}` or any other raw SupportDir map folder. `content|maps` names the future active catalog's read-only verified maps child and is the stable `User` location identity. Task 3's manifest tests stop at the exact resolved two-row mapping and prove no raw/writable User row exists. Task 3 does modify `MapCache` for the no-render construction and lifecycle-lane/drain seam, but it deliberately adds no verified-provider, placeholder-routing, snapshot, or publication semantics. Task 10's RED/GREEN contract below implements and behavior-tests those later non-Legacy placeholder semantics: no `Contents` enumeration, `LoadMapInternal`, `.oramap` reopen, second UID calculation, or MiniYaml parse, and the bound `IVerifiedMapSnapshotProvider` becomes the sole retail-entry source. Legacy desktop `MapFolders`, including any package-backed folder, retain their characterized enumeration behavior. The exact ContentPackages equation is `desktop = Yuri = YR_BLOCK ++ SHARED_BLOCK` and `Base = SHARED_BLOCK`; list order, aliases, parent-package qualifiers, and optional markers are semantic and compared as ordered arrays, never sets.

```text
YR_BLOCK (29, exact order)
~langmd.mix: langmd
~ra2md.mix: ra2md
~thememd.mix
~langmd|audiomd.mix: audiomd
~langmd|cameomd.mix: cameomd
~ra2md|cachemd.mix
~ra2md|conqmd.mix: conqmd
~ra2md|des.mix
~ra2md|desert.mix
~ra2md|genermd.mix
~ra2md|isodesmd.mix
~ra2md|isodes.mix
~ra2md|isogenmd.mix
~ra2md|isolunmd.mix
~ra2md|isolun.mix
~ra2md|isosnomd.mix
~ra2md|isotemmd.mix
~ra2md|isoubnmd.mix
~ra2md|isoubn.mix
~ra2md|isourbmd.mix
~ra2md|loadmd.mix
~ra2md|localmd.mix: localmd
~ra2md|lun.mix
~ra2md|lunar.mix
~ra2md|ntrlmd.mix
~ra2md|snowmd.mix
~ra2md|ubn.mix
~ra2md|urbann.mix
~audiomd|audio.bag

SHARED_BLOCK (30, exact order)
ra2.mix
language.mix
audio.mix
cache.mix
cameo.mix
conquer.mix: conquer
generic.mix
isogen.mix
isosnow.mix
isotemp.mix
isourb.mix
load.mix
local.mix
neutral.mix
sidec01.mix
sidec02.mix
sno.mix
snow.mix
tem.mix
temperat.mix
urb.mix
urban.mix
audio.bag
~theme.mix
ra2|bits
ra2|bits/cameos
ra2|bits/structures
ra2|bits/animations
ra2|bits/projectiles
ra2|uibits
```

YR remains before RA2 so later RA2 packages win shared names exactly as desktop does. Optional syntax does not weaken the profile validator: the closed mount contract independently requires the selected Base/Yuri root pairs. `theme.mix` and `thememd.mix` remain optional and `Unavailable` as specified above. Task 3 tests compare complete SystemPackages and ContentPackages nodes, prove the resolved Yuri manifest is desktop-equivalent under only the two classified iOS filesystem substitutions, prove both iOS MapFolders nodes equal the exact two-row mapping above (never desktop MapFolders), and prove Base ContentPackages is exactly desktop with only YR_BLOCK removed. Task 3's `MapCache` allowlist scope is limited to no-render construction and the lifecycle lane/drain seam. The tracking-package zero-enumeration/open/UID/MiniYaml and verified-provider one-publication-per-UID behavior fixture is mandatory in Task 10, where `VerifiedMapSnapshot` and the provider/placeholder semantics first enter the allowlist; it is not a Task 3 GREEN claim.

The only RA2 inventory additions are the following **17 byte-sorted new profile sources**; the unchanged `mod.yaml` and nine desktop mixed files retain their existing hash/size/classification entries:

```text
mods/ra2/audio/voices-base.yaml
mods/ra2/manifest/common.yaml
mods/ra2/manifest/filesystem-desktop.yaml
mods/ra2/manifest/filesystem-ios-base.yaml
mods/ra2/manifest/filesystem-ios-yuri.yaml
mods/ra2/manifest/gameplay-base.yaml
mods/ra2/manifest/gameplay-yuri.yaml
mods/ra2/mod.ios-base.yaml
mods/ra2/mod.ios-yuri.yaml
mods/ra2/rules/allied-infantry-base.yaml
mods/ra2/rules/defaults-base.yaml
mods/ra2/rules/proxy-actors-base.yaml
mods/ra2/rules/soviet-infantry-base.yaml
mods/ra2/rules/soviet-vehicles-base.yaml
mods/ra2/rules/world-base.yaml
mods/ra2/sequences/vehicles-base.yaml
mods/ra2/sequences/voxels-base.yaml
```

Add all 17 to both `packaging/public-content-manifest.json` and `docs/legal/public-asset-sbom.json` with honest source-derived SHA-256/size, established YAML category/license, and `public: false`; do not claim provenance or make them public. Both inventories remain byte-sorted and unique. Counts move from 263/260 to exactly **280 manifest paths / 277 SBOM paths**. Their namespace sets remain intentionally non-isomorphic with exactly the existing five manifest-only and two SBOM-only mappings—never global equality.

Preserve all **eight** existing `SOURCE_MAPPINGS` rows, including the existing `mods/ra2/mod.yaml`, branding, and plist mappings. Add a separate byte-sorted `PROFILE_SPLIT_SOURCE_MAPPINGS` containing exactly the 17 new paths above—no original mixed path and no duplicate mod row. Assert that each table is internally unique and that their combined source keys and combined manifest/SBOM bundle keys are pairwise duplicate-free. Per row, require a regular non-symlink source, both inventory entries, exact path/hash/size/category/license/public agreement, and only the declared namespace translation. Tests mutate one byte, classification, source key, bundle key, missing manifest row, and missing SBOM row for every mapping class, plus ordering/duplicate/global-difference/count failures.

The PublicClean MSBuild globs would otherwise pick up exactly **11** new files: both `mod.ios-*.yaml`, `audio/voices-base.yaml`, the six `rules/*-base.yaml`, and two `sequences/*-base.yaml`; the six `manifest/*.yaml` files are not matched. In the existing PublicClean ItemGroup, after current Includes, add 11 literal `BundleResource Remove="../../<path>"` rows for exactly that set—no wildcard—and test evaluated items for zero of those logical names. Personal keeps all 17. `test_public_brand_delta.py` requires the historical missing/non-public delta to remain exactly **580 before and after** with no new `(code,path)` pair; never update the baseline or relabel the diagnostic as pass.

The checked-in `packaging/ios-runtime-contract-descriptor-shape-v1.json` is the sole authoritative runtime-resource **shape**, independently of the PublicClean removal group. Its exact immutable baseline is schema `1`, descriptor count `296`, file SHA-256 `6c0d12b5a985bcf87fc255a03a958dbc09be00de9238b239354c8d34be1bd8f6`, and shape SHA-256 `4fe820475ee4d6ef843320f406b5db3fd414fd106c953b4a7b4a05020fb3614d`. It contains `165` `Contract` rows (`134` both-profile, `12` Base-only, `19` Yuri-only) and `131` both-profile `PreflightOnly` rows; the applicable projections are therefore exactly `277` Base and `284` Yuri descriptors. Each row owns literal `sourcePath`, `logicalName`, `bundleRelativePath`, enum-ordered `profiles`, and `purpose`. Source path is build provenance and is excluded from shape identity; length and content hash are generated later and are not shape fields.

The shape digest is over one exact framed stream: `LP_UTF8("OpenRA.RuntimeContractDescriptorShape")`, `U32LE(schemaVersion)`, and `U32LE(descriptorCount)`, followed by records in ascending `logicalName` UTF-8 byte order. Each record is `U32LE(payloadByteLength)` followed by `U32LE(zeroBasedOrdinal)`, `LP_UTF8(logicalName)`, `LP_UTF8(bundleRelativePath)`, `U32LE(profileCount)`, each profile as `LP_UTF8` in `Base,Yuri` enum order, and `LP_UTF8(purpose)`; `LP_UTF8` is `U32LE(strictUtf8ByteLength)` plus those bytes. The digest is lowercase SHA-256 of the complete stream. The golden and tests reject any wildcard/default/duplicate/case alias/out-of-order/unknown-profile/unknown-purpose/source-rule mismatch. A name/path/profile/purpose/membership/count mutation requires an explicit reviewed schema-baseline change; a content-only change does not.

The baseline deliberately governs every regular file in the mounted RA2 `bits`, `uibits`, and `fonts` directories as whole-package `PreflightOnly` closures, including backup or not-currently-static-reachable files; all renderer GLSL files; root `AUTHORS`, `VERSION`, and the global MIX database; `mods/ra2/AUTHORS`, `mods/ra2/icon.png`, legacy desktop/profile manifests, the three blank-shellmap files, and `mods/ts/mod.yaml`; and the complete selected runtime Contract closure including all nine Chinese Fluent overrides independent of the current language. Runtime preflight does not enumerate the root; the golden plus source/evaluated-project and final `.app`/IPA audits maintain these closed sets, while preflight authenticates only the selected fixed expected-name projection. It deliberately excludes the unselected `ra2-content`/`common-content` installer closure: preflight may run only after the native importer is Ready and required retail mounts resolve, so that loader fallback is unreachable. Task 5/14 Fluent-byte edits must regenerate descriptor length/hash while preserving this shape.

The Task 3 `OpenRA.iOS.csproj` edit declares a byte-sorted, non-wildcard `RuntimeContractDescriptor` projection for every golden row in both configuration audits, with literal source include and exact metadata; it is not a second authority. Only the Personal build transforms those items into bundle resources and installs every applicable descriptor target exactly once under the golden's `bundleRelativePath`, including an explicit bundle-root `AUTHORS`; existing globs must be narrowed/deduplicated as needed. Every one of the 296 descriptor-derived `BundleResource` items carries literal `Optimize="false"` and `RuntimeContractDerived="true"`, so the iOS SDK copies the authenticated source bytes rather than running its PNG/property-list/string optimizer. The exact 63 `.png` rows are all `Both/PreflightOnly`, but the no-transform rule applies to all 296 rows. `Optimize` and `RuntimeContractDerived` are transport/audit metadata only: neither enters the golden, the shape framing, the five-field generator input, nor generated runtime descriptors. Evaluated-item tests require all 296 Personal targets exact-once with lowercase `false`, reject missing/empty/true metadata, and require zero descriptor-derived targets in PublicClean. PublicClean retains its exact 11 removes for files caught by older globs and still excludes all 17 profile resources. The golden JSON itself is never a `BundleResource` in either configuration. The project writes evaluated descriptor metadata to one deterministic intermediate input for `GenerateAotObjectRegistry`—never a checked-in sidecar. That build-local file is strict UTF-8 without BOM, one byte-sorted row per line, exactly five TAB-separated fields `(source Identity, LogicalName, BundleRelativePath, Profiles, Purpose)`, with LF or CRLF accepted only as record separators; every field must be nonempty and contain no TAB/CR/LF/NUL, and a blank/extra/malformed/duplicate row fails. The target invokes the generator with this file, the golden's fixed path, and expected file SHA. The Task 3 generator authenticates/parses the golden, requires exact evaluated-item equality, opens every source as a regular non-symlink file, computes current length/SHA-256, and emits the complete immutable `RuntimeContractDescriptorInventory` alongside the existing AOT registrations in `GeneratedAotObjectRegistry.g.cs`. It emits no absolute/source path. It also emits one lowercase `AotRegistryIdentity` using the same strict-NFC `LP_UTF8` and `U32LE` primitives later locked by Task 4: `LP_UTF8("OpenRA.AotRegistry")`, `U32LE(1)`, record count, then records sorted by exact `Type.FullName`; each record is type full name, registration-kind tag (`Basic=1`, `UseCtor=2`), parameter count, and each constructor parameter in metadata order as parameter name plus the generator's canonical `global::` type expression. Duplicate/case-aliased type names, duplicate registrations, null names, unsupported type shapes, or unstable/locale-derived ordering fail generation. The identity hashes this framed byte stream—not generated C# text, assembly enumeration order, or platform-native representations—and the generated source contains the literal digest plus the exact canonical record projection used to recompute it. A checked literal vector independently verifies the bytes. The RED fixture verifies the golden's own file hash/framing and then asserts exact project-item, configuration-specific bundle-target/transport metadata, generated-array, preflight equality, selected root/includes, every file-bearing manifest array, the one culture declaration, and the AOT identity/record framing. Task 4 consumes the same Task 3 inventory types and golden/project projection, adds the single production `Manifest.RuntimeContractFiles` derivation and contract/build-identity consumers, and introduces no other shape source. `PreflightOnly` rows never enter that derived manifest list, while static preflight checks both purposes. Task 15 independently hashes every finished `.app` and safely extracted IPA target against the generated length/SHA; a valid PNG with identical pixels but different encoding must fail, so final byte equality—not metadata alone—is the release proof.

This is a deliberately **Personal-only runtime milestone**. Personal must bundle all 17 profile resources and pass import, profile selection, static preflight, readiness, real startup, and playable-map gates. PublicClean deliberately contains none of the 17, so its configuration-aware startup contract must stop at the first fixed-profile resource check with typed `RuntimeResourcesMissing`; it does not construct or enter the importer, active catalog, runtime services, or `Game.InitializeAndRun`. PublicClean tests in this plan are source/bundle expected-failure diagnostics only, not evidence that the typed failure was observed by launching the app. They preserve the 580 historical report without calling it pass. Task 14/15 and Completion use this same boundary.

- [ ] **Step 5: Run GREEN, YAML, builds, and commit**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~TrustedManifestEntryTest|FullyQualifiedName~PreGameEngineContextTest|FullyQualifiedName~Ra2RuntimeProfileManifestTest|FullyQualifiedName~RetailCandidateProfileValidationTest|FullyQualifiedName~ArgumentsSnapshotTest|FullyQualifiedName~IosMenuLayoutPolicyTest|FullyQualifiedName~IosSettingsLayoutTest|FullyQualifiedName~IosViewportActionLayoutTest|FullyQualifiedName~Ra2ContentInstallerManifestTest|FullyQualifiedName~V3KirovImpactRulesTest|FullyQualifiedName~PublicBrandingPolicyTest' \
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
/private/tmp/nuke-hour-task0-python311/bin/python -m unittest packaging.tests.test_public_brand_delta -v
/private/tmp/nuke-hour-task0-python311/bin/python -m unittest packaging.tests.test_exact_git_stage -v
/private/tmp/nuke-hour-task0-python311/bin/python -m unittest packaging.tests.test_engine_workspace -v
/private/tmp/nuke-hour-task0-python311/bin/python -m unittest packaging.tests.test_verify_engine_patch_delivery -v
/private/tmp/nuke-hour-task0-python311/bin/python -m unittest packaging.tests.test_ios_loading_brand_audit packaging.tests.test_ios_touch_runtime_bundle packaging.tests.test_ios_touch_skin_assets packaging.tests.test_ios_loading_brand_artifact packaging.tests.test_macos_branding packaging.tests.test_macos_branding_hardening packaging.tests.test_cross_platform_branding -v
rg -n 'set_mod_version .*mod\.yaml|MOD_VERSION=.*mod\.yaml' packaging/macos/buildpackage.sh packaging/linux/buildpackage.sh packaging/windows/buildpackage.sh
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Game/OpenRA.Game.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore -m:1
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet restore ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -p:RuntimeIdentifier=iossimulator-arm64 -m:1
TASK3_AOT_CHECK="$(mktemp -d /private/tmp/nuke-hour-task3-aot.XXXXXX)"
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet msbuild ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -t:GenerateAotObjectRegistry \
  -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:GeneratedAotRegistryFile="$TASK3_AOT_CHECK/one.cs"
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet msbuild ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -t:GenerateAotObjectRegistry \
  -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:GeneratedAotRegistryFile="$TASK3_AOT_CHECK/two.cs"
cmp "$TASK3_AOT_CHECK/one.cs" "$TASK3_AOT_CHECK/two.cs"
cmp "$TASK3_AOT_CHECK/one.cs" ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -c Release -f net8.0-ios -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:CodesignDisable=true -p:TargetPlatform=ios --no-restore -m:1
git diff --check
```

Stage exactly the 36 listed outer paths and 18 listed engine paths (54 paths total) through the checked manifest/helper, verify the displayed byte-sorted list and exact count, and commit:

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 3 --phase implementation
git commit -m "Add verified RA2 runtime profiles"
```

- [ ] **Step 6: Generate and verify `0021` immediately**

Update `packaging/engine_workspace.py` and `packaging/tests/test_engine_workspace.py` before generation so `ALLOWLISTS["0021"]` and the expected generated LF file are the same byte-sorted 18 paths displayed above, including `OpenRA.Game/FileSystem/FileSystem.cs`, `OpenRA.Game/Map/MapCache.cs`, `OpenRA.Game/Support/Arguments.cs`, `OpenRA.Test/ArgumentsSnapshotTest.cs`, and the corrected `OpenRA.Test/RetailContentImporterTest.cs`. Its count assertion must be 18. Generate from official+`0001`…`0020` with exactly those 18 engine paths:

```bash
set -euo pipefail
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
PATCH_STAGE="$(mktemp -d /private/tmp/nuke-hour-engine-patch-0021.XXXXXX)"
ALLOWLIST="$PATCH_STAGE/0021.allowlist"
VERIFY_DIR="$PATCH_STAGE/verification"
PREDECESSOR_END=20
CANDIDATE=engine-patches/0021-ios-retail-runtime-profiles.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0021 --output "$ALLOWLIST"
LC_ALL=C sort -c -u "$ALLOWLIST"
test "$(wc -l < "$ALLOWLIST" | tr -d ' ')" -eq 18
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
test -f "$VERIFY_DIR/evidence.json"
```

Update `test_verify_engine_patch_delivery.py` from its hard-coded `0020` candidate/predecessor/README assertions to exact `0021`/predecessor 20 and the same byte-sorted 18 headers. Run all three delivery test modules; require candidate headers and verifier evidence `allowlist_count == 18`, exact immediate-predecessor replay, duplicate-forward refusal with unchanged tree, reverse success, and no outside-allowlist path. After strict replay succeeds, update the README row and commit only the five delivery paths:

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 3 --phase delivery
git commit -m "Deliver RA2 runtime profile engine patch"
```

---

### Task 4: Build the deterministic runtime contract and network compatibility

For all Task 4–17 authority claims, the reviewed Personal `OpenRA.iOS.dll` is the trusted composition TCB defined by the authoritative design. The existing weak-name `InternalsVisibleTo("OpenRA.iOS")` grants the whole assembly access; it is not per-file or per-type CLR isolation. “Internal,” “unforgeable,” “sole production caller,” and “ordinary consumer cannot” mean no public path exists outside Game/RA2 plus this audited TCB. Within the TCB, exact producer/caller uniqueness is enforced by evaluated `Compile` items, final-IL call-edge allowlists, mutation tests, managed-image SHA-256/MVID/AOT identity, same-build app/IPA equality, and the Apple signature for release evidence. Simulator builds are compile/behavior evidence only. Tests pin the Game/RA2 production friend list to exactly `OpenRA.iOS`, separately allow test friends, and prove a differently named ordinary consumer assembly cannot compile against the internal authority surface. No later task may describe this friend grant as a narrower CLR boundary.

Task 4's initial `VerifiedRa2RuntimeContract`/`Check` API is an isolated expected-versus-actual resource baseline because Task 7's store-minted Ready type does not exist yet. No production startup, pending mutation, runtime service, or engine identity may consume that provisional wrapper. Task 14 explicitly modifies both owning RA2 files, adds the final Ready plus validation-identity parameters and private binding field, reruns the complete behavior/reflection matrix, and is the first task permitted to use a successful wrapper in the shipped startup graph.

An evidence-driven delivery-boundary correction is authorized before the Task 4 implementation commit. A post-Task-3/current-Task-4 byte and diff audit proves that `OpenRA.Mods.RA2/Content/Ra2RuntimeProfile.cs`, `OpenRA.Mods.RA2/Content/RetailProfileValidator.cs`, `OpenRA.Mods.RA2/Content/RetailImportModels.cs`, `build/OpenRA.AotRegistry.Generator/Program.cs`, `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs`, `ios/OpenRA.iOS/OpenRA.iOS.csproj`, `engine/OpenRA.Game/Game.cs`, and `engine/OpenRA.Game/ModData.cs` have no genuine Task 4 delta. They are therefore removed from this task's staging boundary; manufacturing a no-op or unrelated edit merely to satisfy the obsolete 25-path count is forbidden. This correction authorizes only the exact plan/manifest/`0022` allowlist/test alignment below and does not change runtime behavior, ownership, verification, or later-task requirements. `Game.cs` remains a real Task 5 path; `ModData.cs` contributes no `0022` delta.

**Outer files:**

- Create: `OpenRA.Mods.RA2/Content/Ra2RuntimeContract.cs`
- Create: `OpenRA.Mods.RA2/Content/RuntimeBuildIdentity.cs`
- Create: `OpenRA.Mods.RA2/Content/RuntimeBuildIdentityReader.cs`
- Create: `OpenRA.Mods.RA2/Content/Ra2RuntimeContractBuilder.cs`
- Modify: `OpenRA.Mods.RA2/Content/IosRuntimeResourcePreflight.cs`
- Modify: `OpenRA.Mods.RA2/Content/IosRuntimeResourceRoot.cs`
- Create: `OpenRA.Mods.RA2/Content/IosRuntimeManagedImageSource.cs`
- Create: `ios/OpenRA.iOS/IosRuntimeBuildIdentityProvider.cs`

**Engine files for `0022` begun in this task:**

- Modify: `engine/OpenRA.Game/Manifest.cs`
- Modify: `engine/OpenRA.Game/Network/GameServer.cs`
- Modify: `engine/OpenRA.Game/Network/Handshake.cs`
- Modify: `engine/OpenRA.Game/Network/Session.cs`
- Modify: `engine/OpenRA.Game/Network/UnitOrders.cs`
- Modify: `engine/OpenRA.Game/Server/Server.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/ServerListLogic.cs`
- Create: `engine/OpenRA.Test/RuntimeContractBuilderTest.cs`
- Create: `engine/OpenRA.Test/RuntimeContractHandshakeTest.cs`

Task 4 has exactly **8 outer implementation paths** and **9 engine paths begun for `0022`**, so its implementation commit stages exactly **17 paths**. There is no delivery commit in this task; Task 5 completes and delivers `0022`.

- [ ] **Step 1: Write RED canonicalization and handshake tests**

```csharp
public sealed class ManagedImageIdentity
{
	public string BundleFilename { get; }
	public string Sha256 { get; }
	public Guid ModuleMvid { get; }
	internal ManagedImageIdentity(
		string bundleFilename, string sha256, Guid moduleMvid);
}

public sealed class RuntimeBuildIdentity
{
	public ImmutableArray<ManagedImageIdentity> ManagedImages { get; }
	public string AotRegistryIdentity { get; }
	internal RuntimeBuildIdentity(
		ImmutableArray<ManagedImageIdentity> managedImages,
		string aotRegistryIdentity);
}

internal interface IRuntimeBuildIdentityProvider
{
	RetailOperationResult<RuntimeBuildIdentity> Capture(
		RetailValidationDeadline alreadyStartedDeadline,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken);
}

internal interface IRuntimeManagedImageSource : IDisposable
{
	ImmutableArray<string> EnumerateAppLocalManagedImages();
	IRuntimeManagedImageLease AcquirePinned(string canonicalBundleFilename);
}

internal interface IRuntimeManagedImageLease : IDisposable
{
	string CanonicalBundleFilename { get; }
	long Length { get; }
	Guid ModuleMvid { get; }
	Guid? LoadedModuleMvid { get; }
	Stream OpenRead();
	void VerifyUnchanged();
}

internal sealed class RuntimeBuildIdentityReader
{
	internal RuntimeBuildIdentityReader(
		IRuntimeManagedImageSource images,
		string canonicalAotRegistryIdentity);
	internal RetailOperationResult<RuntimeBuildIdentity> Capture(
		RetailValidationDeadline alreadyStartedDeadline,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken);
}

internal sealed class IosRuntimeBuildIdentityProvider :
	IRuntimeBuildIdentityProvider, IDisposable
{
	internal IosRuntimeBuildIdentityProvider(
		IosRuntimeManagedImageSource ownedBundleImages,
		string canonicalAotRegistryIdentity);
	public RetailOperationResult<RuntimeBuildIdentity> Capture(
		RetailValidationDeadline alreadyStartedDeadline,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken);
	public void Dispose();
}

internal sealed class IosRuntimeManagedImageSource :
	IRuntimeManagedImageSource
{
	internal IosRuntimeManagedImageSource(
		IosRuntimeResourceRoot sharedRoot);
	public ImmutableArray<string> EnumerateAppLocalManagedImages();
	public IRuntimeManagedImageLease AcquirePinned(
		string canonicalBundleFilename);
	public void Dispose();
}

internal sealed class IosRuntimeResourceRoot : IIosRuntimeResourceRoot
{
	internal static RetailOperationResult<IosRuntimeResourceRoot> OpenBundleRoot(
		string trustedBundleResourceRoot);
	public RetailOperationResult<IIosRuntimeResourceLease> AcquireRegularFile(
		string canonicalRelativePath);
	internal RetailOperationResult<IosRuntimeManagedImageSource> CreateManagedImageSource();
	public void Dispose();
}

// Added in Task 4. Task 3 exposes only RuntimeContractCultures.
public sealed class Manifest
{
	public ImmutableArray<string> RuntimeContractFiles { get; }
}

internal sealed class RuntimeContractInputs
{
	internal RuntimeContractDescriptorInventory Inventory { get; }
	internal RuntimeBuildIdentity BuildIdentity { get; }
	internal RuntimeContractInputs(
		RuntimeContractDescriptorInventory inventory,
		RuntimeBuildIdentity buildIdentity);
}

public sealed class Ra2RuntimeContractExpectation
{
	public Ra2RuntimeContract Contract { get; }
	internal RuntimeContractInputs Inputs { get; }
	internal TrustedManifestSelection Selection { get; }
	internal ImmutableArray<string> IncludedFiles { get; }
	internal ImmutableArray<string> Cultures { get; }
	internal ImmutableArray<string> RuntimeContractFiles { get; }
	internal Ra2RuntimeContractExpectation(
		RuntimeContractInputs inputs,
		Ra2RuntimeContract contract,
		TrustedManifestSelection selection,
		ImmutableArray<string> includedFiles,
		ImmutableArray<string> cultures,
		ImmutableArray<string> runtimeContractFiles);
}

public sealed class VerifiedRa2RuntimeContract
{
	public Ra2RuntimeContract Contract { get; }
	internal TrustedManifestSelection Selection { get; }
	internal ImmutableArray<string> MapCompatibility { get; }
	internal VerifiedRa2RuntimeContract(
		Ra2RuntimeContractExpectation expectation,
		ImmutableArray<string> mapCompatibility);
}

internal sealed class IosRuntimeResourcePreflight
{
	internal IosRuntimeResourcePreflight(RuntimeContractInputs inputs);
	internal RetailOperationResult<VerifiedRa2RuntimeContract> Check(
		Ra2RuntimeContractExpectation expectation,
		Manifest resolvedManifest,
		IIosRuntimeResourceRoot distributionRoot,
		RetailValidationDeadline alreadyStartedDeadline,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken);
}

public sealed class Ra2RuntimeContract
{
	public Ra2RuntimeProfileId ProfileId { get; }
	public string ContractDigest { get; }
	public string FilenameCatalogDigest { get; }
	public int MapFormatVersion { get; }
	internal Ra2RuntimeContract(
		Ra2RuntimeProfileId profileId,
		string contractDigest,
		string filenameCatalogDigest,
		int mapFormatVersion);
}

internal sealed class Ra2RuntimeContractBuilder
{
	internal Ra2RuntimeContractBuilder(RuntimeContractInputs inputs);
	internal RetailOperationResult<Ra2RuntimeContractExpectation> BuildExpected(
		Ra2RuntimeProfileId profile,
		TrustedManifestSelection selection,
		Manifest resolvedManifest,
		string filenameCatalogDigest,
		int mapFormatVersion,
		int archiveValidatorVersion,
		int mapValidatorVersion,
		RetailValidationDeadline alreadyStartedDeadline,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken);
}
```

The Personal iOS composition opens exactly one internal `IosRuntimeResourceRoot` from the platform-supplied bundle root and never calls `OpenBundleRoot` again. Typed `CreateManagedImageSource` returns the internal production `IosRuntimeManagedImageSource` with one retained reference to the same descriptor-root capability, or fails without transferring a reference when the root is disposed/invalid. The `IosRuntimeBuildIdentityProvider` constructor takes ownership of that child at call entry: it validates the other argument without releasing ownership back, disposes the child on constructor-path failure, and makes repeated provider disposal harmless. `RuntimeBuildIdentityReader` only borrows its internal `IRuntimeManagedImageSource`; the provider is the one owner that disposes the child. It is also the only production factory for `ManagedImageIdentity` and `RuntimeBuildIdentity`. `RuntimeContractInputs` binds that build identity to the exact generated inventory by object identity; the internal `Ra2RuntimeContractBuilder` is the only factory for an expectation and `IosRuntimeResourcePreflight` is the only factory for a verified contract. All result types have internal validating constructors, defensive immutable storage, and no record/init/with/public-success path. `IRuntimeBuildIdentityProvider`, both managed-image seams, the reader, both iOS adapters, `RuntimeContractInputs`, the builder, and preflight are internal; only the existing `OpenRA.iOS` and `OpenRA.Test` friend assemblies can compose controlled fixtures, so an ordinary consumer cannot inject a fake image/resource source or mint a successful identity/contract.

Before the first `Capture`, the coordinator starts one validation deadline and passes that exact value, the same `IRetailMonotonicClock` object, and the same lifecycle token through build-identity capture and the final static preflight; none may construct, extend, replace, or restart a budget. Pure expected-contract builds before recovery completion use that token; after the single pre-`CompleteAsync` cancellation checkpoint, every expected-contract rebuild and the single mandatory repair publication use `CancellationToken.None` while retaining the exact same deadline and clock. The composition calls typed `Capture` once and stops on failure, then creates one `RuntimeContractInputs` from the exact generated inventory instance and captured build identity. `BuildExpected` has no root/source parameter and performs no resource acquisition, stream open, hash, or `VerifyUnchanged`; it validates the manifest projection and computes the expected digest solely from authenticated generated descriptor length/hash values, the captured managed-image identity, and the fixed framing. Candidate validation and repair may persist only `expectation.Contract`; they cannot call static preflight or convert it to an engine identity. After the final retail archive/map open is Ready, and only then, `IosRuntimeResourcePreflight.Check` uses the same inputs/root/deadline/clock and the original lifecycle token to pin and hash the applicable `Contract` and `PreflightOnly` files, proves their actual bytes equal the generated values, validates and defensive-copies the final resolved manifest's exact `TrustedManifestSelection` and canonical ordinal/case-unique nonempty `MapCompatibility`, and returns a `VerifiedRa2RuntimeContract` wrapping that same `expectation.Contract` instance plus those two final projections. The wrapper has no standalone identity conversion. Only Task 13's internal `IosRuntimeMapServices.Create(verifiedContract, finalReady)` may pair it with the exact store-minted Ready, reauthenticate all receipt/profile/selection/contract fields, and derive the scalar engine identity plus an opaque internal runtime authorization owned by the returned service set. Pending consumption still requires both original capabilities directly. Root, child source, resource lease, image lease, and stream share one private ref-counted root-directory-descriptor owner. Disposing the outer root atomically blocks new acquisitions but cannot invalidate an already retained child/lease/stream; the final reference closes the root descriptor exactly once, and every failure/alias/double-dispose branch is tested. Current PublicClean stops at its earlier missing-profile-resource source/bundle boundary and never starts this deadline, opens this root, or constructs a source, provider, preflight, inputs, builder, expectation, or verified contract.

The three Task 4 operational boundaries use one exact failure table. A matching cancelled token is `Cancelled/null/null/None`; deadline expiry is `ValidationTimedOut/null/null/DeadlineExceeded`; a default deadline, different clock identity, default/unknown profile, selection/profile mismatch, malformed version/catalog input, invalid inventory projection, or expectation/preflight provenance mismatch is `ProfileContextFailure/null/null/ProfileMetadataMismatch`; and any missing/extra/duplicate/case-aliased/nonregular/unparseable/changed managed image, invalid or mismatched MVID/AOT identity, descriptor/resource absence, length/hash/type mismatch, second-enumeration drift, final identity change, or safe I/O failure under the fixed bundle root is `RuntimeResourcesMissing/null/null/InvalidMapPackage`. Framing/count/length arithmetic overflow is `RuntimeResourcesMissing/null/null/ArithmeticOverflow`. Constructor argument programmer errors that occur before ownership transfer may throw only the documented argument exception; after `Capture`, `BuildExpected`, or `Check` begins, no raw I/O, PE parser, crypto, cancellation, overflow, or disposal exception escapes. Synthetic/fake-source success is a public-API-shape impossibility outside the trusted TCB; inside that whole friend assembly it is forbidden by the evaluated-source/final-IL release invariant and tamper tests, not falsely attributed to per-type CLR isolation or represented as a runtime failure-table row. Tests cover every reachable table row independently and assert that no exception message or path changes the mapping.

`BuildExpected` resolves only the fixed `TrustedManifestSelection` against the supplied resolved manifest. Task 4's `Manifest.cs` change derives immutable ordinal `RuntimeContractFiles` without opening a package or consulting generated data. A manifest with an empty `RuntimeContractCultures`—including every legacy/default desktop entry and every other existing mod—gets `ImmutableArray.Empty` and performs no new alias/file-list validation; only an explicitly profiled entry enters the derivation below, and the builder rejects an empty culture/file projection. For a profiled entry, start with the selected root and ordered `IncludedFiles` under `mods/{manifest.Id}/`; add every path from exactly the file-bearing `Rules`, `Sequences`, `ModelSequences`, `Cursors`, `Chrome`, `ChromeLayout`, `Weapons`, `Voices`, `Notifications`, `Music`, `FluentMessages`, `TileSets`, `ChromeMetrics`, `Missions`, and `Hotkeys` arrays; and for every Fluent row add the sibling `<directory>/<culture>/<filename>` for every `RuntimeContractCultures` entry. A package-qualified row must use one exact alias declared by `FileSystem.SystemPackages`; `$id` canonicalizes only to `mods/id`, `^EngineDir|mods/id` canonicalizes only to `mods/id`, and unqualified rows belong only to the current selected mod package. Other roots, optional markers, content/retail aliases, missing/duplicate/case-aliased aliases, path escape, noncanonical culture, duplicate/case-aliased output, or an unknown file-bearing manifest section fail before the property is exposed. The final list is UTF-8 ordinal sorted and defensively copied. This exact algorithm yields 146 Base and 153 Yuri rows and must equal the applicable `Purpose=Contract` projection of the generated inventory; `PreflightOnly` rows are forbidden from that manifest list but remain mandatory inventory members.

`BuildExpected` validates the complete applicable `Contract` plus `PreflightOnly` projection from the immutable generated inventory, but has no root/source and performs no file acquisition, stream read, hash, second enumeration, or `VerifyUnchanged`. `LogicalName` is the canonical manifest/contract identity; the distinct `CanonicalBundleRelativePath` is the only later descriptor-relative open target, and both are validated for canonicality, uniqueness, exact generated mapping, and exact profile/purpose projection. It writes versioned length-prefixed UTF-8/binary records in ordinal logical-name order over inventory schema version, descriptor count and shape digest, every applicable descriptor's purpose/profile/path/name/generated length/content digest, selected entry/direct fragments, filename-catalog digest, map format, both validator versions, final managed DLL SHA-256/MVID, and AOT registry identity. Thus the expectation binds the full inventory shape and both purpose classes, while manifest equality remains limited to `Contract`. The builder API has no resource root/source, source-tree root, caller string-path open, ambient bundle, `NSBundle` callback, alternate build-identity overload, or budget-start overload. Enumeration order cannot change the digest; any one authenticated input byte/identity change must change it.

Only after the required retail archive/map validation has returned a provisional Ready lease does `IosRuntimeResourcePreflight.Check` verify the expected static bytes. It first requires `ReferenceEquals(expectation.Inputs, itsInputs)` and exact immutable equality of the expectation's selection, ordered fragments, cultures, runtime-file projection, and resolved manifest with the supplied final manifest. It derives the fixed selected authenticated descriptor projection twice—Base 277 or Yuri 284—from the immutable inventory and manifest. After the first exact projection check it acquires every expected descriptor once through `AcquireRegularFile(CanonicalBundleRelativePath)`, retains all leases, and hashes/reads only streams duplicated from those pinned descriptors. It checks the shared token/deadline before every descriptor, before and after every read chunk, before the second immutable projection equality check, and before each final expected-name `VerifyUnchanged`. Only after the two ordinal projections match, each actual length/hash equals the generated descriptor, every retained expected identity remains unchanged, and the selected manifest's canonical nonempty `MapCompatibility` has been defensive-copied does it return `VerifiedRa2RuntimeContract` around the exact same contract instance, fixed selection, and compatibility projection. It releases all leases in reverse order from one `finally`; failures retain already acquired leases until that common cleanup. Duplicate/extra/missing here means authenticated descriptor rows, not unrelated files under the root; the acquire-by-name runtime boundary makes no unrelated-extra claim, and exact source/evaluated-project plus final `.app`/IPA audits own it. Default/wrong-profile selection, descriptor/path/profile/purpose/hash/length mismatch, duplicate/extra/missing authenticated descriptor, empty/duplicate/case-aliased compatibility, empty image/AOT identity, timeout/cancellation, mutable caller input, selected-projection metadata drift, or final expected-path replacement returns a closed typed failure. Pair-authorized Task 13 construction has the sole closed literal mapping into the generic engine scalar: `Base -> RuntimeContractIdentity("ra2-base", ContractDigest)` and `Yuri -> RuntimeContractIdentity("ra2-yuri", ContractDigest)`. Neither unverified nor verified contract exposes a conversion method. Default/unknown profiles cannot convert, and no enum name, localized string, manifest filename, or caller-supplied profile text enters persistence or the handshake. Tests pin both exact strings, reject their case/alias variants, and prove a wrapper without its exact Ready cannot obtain an engine identity or runtime authorization.

The contract digest has one exact platform-independent schema. Its stream starts with `LP_UTF8("OpenRA.RA2RuntimeContract")` and `U32LE(1)`. `LP_UTF8` is `U32LE(strict-NFC-UTF8-byte-length)` followed by those bytes; every count/tag/version is `U32LE`, and every nonnegative file length is `U64LE`. It then writes, in order: the fixed profile tag (`Base=1`, `Yuri=2`); selection mod ID and entry name; the source-ordered `IncludedFiles`; the ordinal `RuntimeContractCultures`; inventory schema, total descriptor count, and raw 32-byte decoded shape digest; applicable descriptor count; then every applicable descriptor in the generated global ordinal order as global ordinal, purpose tag, profile-count/profile tags, logical name, bundle-relative path, length, and raw 32-byte content digest. Next come raw 32-byte filename-catalog digest, map-format version, archive-validator version, and map-validator version. Finally it writes the ordinal managed-image count and, per image, canonical bundle filename, raw 32-byte image digest, and the 16 bytes obtained by decoding `ModuleMvid.ToString("N")` in display order; the last field is the raw 32-byte AOT-registry digest. Hex parsing accepts only lowercase fixed-length input before decoding. The resulting `ContractDigest` is lowercase SHA-256 of the complete stream. No .NET `BinaryWriter` string prefix, platform `Guid.ToByteArray`, locale formatting, enum name, dictionary iteration order, native endianness, or trailing byte is permitted. Tests carry one checked literal framing vector, perturb every field/ordinal/count/tag, compare an independent byte writer, and prove Base/Yuri and rebuild determinism.

`OpenRA.iOS.csproj` retains Task 3's one byte-sorted literal `RuntimeContractDescriptor` projection for every row in `packaging/ios-runtime-contract-descriptor-shape-v1.json`, including exact source include, canonical logical name, canonical bundle-relative target, enum-sorted profile applicability, and `Purpose=Contract|PreflightOnly`; no wildcard, inferred membership, or second hand-maintained table is allowed. Its Personal transform retains literal `Optimize="false"` and `RuntimeContractDerived="true"` on every one of the 296 derived bundle items, including exactly 63 PNGs, while PublicClean has no descriptor-derived target. The generator consumes that golden and the evaluated five-field descriptor items 1:1—transport metadata is audited separately and never enters generation—verifies the golden file hash, schema `1`, count `296`, canonical framing, and shape digest, then requires exact row equality before reading content. It ordinally validates/deduplicates the shape and fails if logical name, bundle path, profiles, purpose, membership, source mapping, or count changed without an explicit reviewed golden/schema-baseline bump. A content-byte-only length/hash change does not require a schema bump. It hashes each current regular non-symlink source and emits schema version `1`, literal descriptor count, shape digest, and immutable `(logical, bundle-relative, length, sha256, profiles, purpose)` descriptors into `GeneratedAotObjectRegistry.g.cs`; it never emits source/absolute paths. Task 4 retains Task 3's descriptor/inventory types and exact generated hashes, then adds build-identity, expectation, preflight, verified-contract, and handshake consumers without changing inventory membership. The two inventory types have internal validating constructors, no public construction/copy/mutation surface, and defensively copy every descriptor/profile array; generator and runtime reject missing/nonregular/symlink/duplicate/case-alias/default-profile/default-purpose items and emit descriptors in ordinal logical-name order. One `RuntimeContractInputs` object binds the exact generated inventory instance to captured build identity; the pure builder and actual-byte preflight both require that same identity. `Manifest.RuntimeContractFiles` is checked only against its `Contract` projection, while final post-retail-Ready preflight checks both purposes once through the shared root. Because generation reads current source bytes, Task 5's two common Fluent edits regenerate and commit their descriptor length/hash, while Task 14's two RA2 Fluent edits deterministically change those rows and leave the resulting generated source as the one known unstaged output for Task 15. All four content edits leave shape digest and registration-only AOT identity unchanged; Task 15 regenerates twice, verifies exact bytes/source inputs against the same golden, audits all Personal transport metadata, and stages the final output.

The AOT generator separately emits the exact version-1 framed registration identity defined in Task 3; no second reader or hashing recipe exists. Task 4's reader accepts only that canonical lowercase digest, and generator/AOT coverage tests recompute it from the emitted canonical record projection before any runtime contract can use it. “App-local managed image” has one exact, non-recursive meaning: a regular no-follow file that is an immediate child of the finished `.app` root, whose one-segment NFC leaf name has no separator, control character, dot alias, or case alias and ends with the ordinal lowercase suffix `.dll`. Every such root file is included; there is no `System.*`, `Microsoft.*`, facade, framework, or third-party exclusion. A `.dll` anywhere below a nested directory, or represented by a directory, symlink, or other non-regular entry, is forbidden and the finished-app/IPA audit fails rather than silently defining a second image namespace. The set must contain the six project images `OpenRA.Game.dll`, `OpenRA.Mods.Cnc.dll`, `OpenRA.Mods.Common.dll`, `OpenRA.Mods.RA2.dll`, `OpenRA.Platforms.Default.dll` (the iOS platform project's actual assembly name), and `OpenRA.iOS.dll`, plus every shipped framework and third-party managed image. `RuntimeBuildIdentityReader` consumes only this closed ordinal leaf-name inventory. Its constructor validates the injected image source and canonical lowercase AOT identity without touching files. `Capture` checks the shared token/deadline, enumerates and ordinally canonicalizes the closed inventory, then for each name checks again, acquires one lease bound to a single no-follow descriptor/file identity, and retains that lease. SHA-256 and PE MVID are both parsed from reads of that same pinned descriptor, with checks before and after every read chunk/MVID operation, and an already-loaded MVID is compared when present. After every image is read, `Capture` checks again, enumerates the app-local inventory a second time, and requires exact ordinal set equality with the first. Only then does it check before and call `VerifyUnchanged` on every still-retained lease and release all leases in reverse order from one common `finally`; a failure never releases an earlier lease prematurely. This catches add/remove/rename and replacing the first-read filename with a same-name inode before final verification. No path is reopened for hashing or MVID. `IosRuntimeManagedImageSource` enumerates only immediate root children and implements leases through the retained `IosRuntimeResourceRoot` descriptor, while `IosRuntimeBuildIdentityProvider` owns that child at constructor call entry and delegates the exact deadline/clock/token to the reader; neither adapter uses `NSBundle.PathForResource`, `File.Open`, recursive directory discovery, or a second root. A missing/extra/duplicate/non-managed/unparseable root `.dll`, path escape, MVID mismatch, empty identity, inventory-set change, final identity change, timeout, or matching cancellation returns a closed failure before startup sealing. `RuntimeContractBuilderTest` behavior-tests the real root and managed-image adapter on macOS with a controlled finished-bundle fixture containing the six project images plus at least one framework image and one third-party parser image. It covers the AOT literal vector/recomputation, canonical root-leaf acceptance, uppercase-suffix/case-alias/nested-`.dll` rejection, parent/leaf symlinks, rename/replace between enumeration/acquisition/hash/MVID/second-enumeration/final-verification, add/remove between the two enumerations, replace-after-first-file-read, timeout/cancellation at each checkpoint, root-before-child and child-before-root disposal, every nth acquisition/read/hash failure, stream-outlives-owner, reverse lease release, and exact descriptor/ref-count closure; the iOS build proves the same production adapters compile/AOT, and Task 15 compares the real finished-app managed inventory against this exact immediate-root rule while independently rejecting every nested `.dll` in the full bundle walk.

Both legacy peers remain compatible; exactly one legacy/missing identity, profile mismatch, or digest mismatch is rejected before lobby admission in both directions.

- [ ] **Step 2: Run RED**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~RuntimeContractBuilderTest|FullyQualifiedName~RuntimeContractHandshakeTest' \
  -v minimal
```

Expected: missing identity/builder types and a profile/digest mismatch accepted by the current Mod/Version-only handshake.

- [ ] **Step 3: Implement contract propagation**

`EngineStartupState` and `ModData` continue carrying Task 3's identity; the old desktop overload supplies `Legacy`. Manifest exposes deterministic `RuntimeContractFiles`. Handshake request, response, session global data, and server advertisement add optional `RuntimeProfile`/`RuntimeContract`; client and server both validate before creating/admitting the lobby client. Server browser marks a mismatch unavailable with a typed reason. Task 14 constructs the internal `IosRuntimeBuildIdentityProvider`, builds an expectation, obtains `VerifiedRa2RuntimeContract` only after final retail Ready plus static preflight, then calls the pair-authorized Task 13 service factory and passes the returned set's internal derived engine identity plus the final sealed `ModDataConstructionOptions` into `SealForGame`; no unverified, synthetic, wrapper-only, Ready-only, or source-tree identity is allowed in a production launch.

- [ ] **Step 4: Run GREEN and commit the Task 4 paths**

Run the focused tests and Release builds for Game, Mods.Common, and RA2. The focused test creates the neutral controlled bundle fixture and executes the resource-descriptor, managed-file/MVID, shared-deadline, shared-root, TOCTOU, and disposal matrix. Invoke the iOS project's `GenerateAotObjectRegistry` target twice into temporary outputs—not the generator's old DLL-only CLI—so both runs authenticate the exact checked-in golden and consume the exact MSBuild `RuntimeContractDescriptor` projection; require byte identity, schema version 1, descriptor count 296, the pinned shape digest, the expected resource descriptors, and the expected AOT identity. A fixture copy with one resource byte changed must change that descriptor's length/hash, the generated source bytes, and the derived runtime-contract digest while leaving shape digest and registration-only AOT identity unchanged; independently mutate golden/file hash/framing/name/path/profile/purpose/source mapping/membership/count and require each to fail without an explicit reviewed golden/schema-baseline bump. Build a fresh unsigned Personal `iossimulator-arm64` app with default relative `bin/obj` to compile/AOT the production shared-root adapters, require the checked-in generated registry equals the target output, and defer finished-app metadata/source auditing to Task 15's explicitly tested audit. This Task 4 build is not a PublicClean runtime claim. Commit only this task's outer and engine paths:

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~RuntimeContractBuilderTest|FullyQualifiedName~RuntimeContractHandshakeTest' \
  -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Game/OpenRA.Game.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore -m:1
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet restore ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -p:RuntimeIdentifier=iossimulator-arm64 -m:1
AOT_CHECK_DIR="$(mktemp -d /private/tmp/nuke-hour-aot-identity.XXXXXX)"
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet msbuild ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -t:GenerateAotObjectRegistry \
  -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:GeneratedAotRegistryFile="$AOT_CHECK_DIR/one.cs"
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet msbuild ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -t:GenerateAotObjectRegistry \
  -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:GeneratedAotRegistryFile="$AOT_CHECK_DIR/two.cs"
cmp "$AOT_CHECK_DIR/one.cs" "$AOT_CHECK_DIR/two.cs"
cmp "$AOT_CHECK_DIR/one.cs" ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -c Release -f net8.0-ios -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:CodesignDisable=true -p:TargetPlatform=ios --no-restore -m:1
git diff --check
```

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 4 --phase implementation
git commit -m "Define RA2 runtime contract identity"
```

Do not generate `0022` yet; Task 5 completes persistence in the same patch.

---

### Task 5: Scope saves, replays, settings, and skirmish state; deliver `0022`

**Outer file refreshed after the Task 5 content-byte changes:**

- Modify again: `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs`

**Remaining exact `0022` engine paths:**

- Create: `engine/OpenRA.Game/RuntimeContractStorage.cs`
- Modify: `engine/OpenRA.Game/Settings.cs`
- Modify: `engine/OpenRA.Game/Network/GameSave.cs`
- Modify: `engine/OpenRA.Game/GameInformation.cs`
- Modify: `engine/OpenRA.Game/Network/ReplayRecorder.cs`
- Modify again: `engine/OpenRA.Game/Game.cs`
- Modify again: `engine/OpenRA.Game/Server/Server.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/GameSaveBrowserLogic.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/ReplayBrowserLogic.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs`
- Modify: `engine/OpenRA.Mods.Common/ServerTraits/SkirmishLogic.cs`
- Modify: `engine/mods/common/fluent/chrome.ftl`
- Modify: `engine/mods/common/fluent/zh-CN/chrome.ftl`
- Create: `engine/OpenRA.Test/RuntimeContractPersistenceTest.cs`

The complete `0022` allowlist is the union of Task 4's 9 engine paths and these 14 paths, with duplicates removed: `Server.cs` is intentionally modified in both tasks, while `Game.cs` remains Task 5-only, so the final union has exactly 22 unique paths. `ModData.cs` has no Task 4 or Task 5 delta and is not part of `0022`. Task 3's separate 18-path `0021` allowlist (including `FileSystem.cs`, `MapCache.cs`, `Arguments.cs`, and `RetailContentImporterTest.cs`) is only a replay predecessor and contributes no path to the `0022` candidate union; the Step 5/`0022` count remains exactly 22.

The Task 5 implementation commit has exactly **one outer path plus 14 engine paths = 15 paths**. The outer generated source is not part of the 22-path engine patch candidate.

**Delivery files:**

- Create: `engine-patches/0022-ios-retail-runtime-contract.patch`
- Modify: `engine-patches/README.md`
- Modify: `packaging/tests/test_verify_engine_patch_delivery.py`

- [ ] **Step 1: Write RED storage and metadata tests**

```csharp
public static class RuntimeContractStorage
{
	public static string SavesPath(
		string supportDir, string modId, string modVersion,
		RuntimeContractIdentity identity);
	public static string ReplaysPath(
		string supportDir, string modId, string modVersion,
		RuntimeContractIdentity identity);
	public static string ServerSettingsPath(
		string supportDir, string modId,
		RuntimeContractIdentity identity);
	public static string SkirmishPath(
		string supportDir, string modId,
		RuntimeContractIdentity identity);
}
```

Profiled paths are exactly:

```text
Profiles/{modId}/{profile}/{digest}/Saves/{modVersion}
Profiles/{modId}/{profile}/{digest}/Replays/{modVersion}
Profiles/{modId}/{profile}/{digest}/settings.server.yaml
Profiles/{modId}/{profile}/{digest}/skirmish.yaml
```

Every token is validated by the canonical identity/mod/version policy before path construction; alternate separators, absolute/traversal tokens, empty/default identities, and case aliases fail. `RuntimeContractPersistenceTest` locks all four exact platform-normalized strings. Legacy returns every existing desktop path unchanged. New saves and replays embed profile/digest; load validates even when a mismatched file is manually copied into the current directory. iOS profiled mode hides legacy metadata with a typed reason. Maps remain physically shared.

- [ ] **Step 2: Run RED**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~RuntimeContractPersistenceTest' -v minimal
```

Expected: Base/Yuri still share save/replay/server/skirmish state and metadata lacks runtime identity.

- [ ] **Step 3: Implement centralized storage and validation**

Profile only Server settings; keep language/input/player/game/audio/graphics in the shared global file. Route save/replay enumeration and actual load, server save/load, `loadsave` deep links, skirmish state, and browser visibility through the centralized storage API. `Game.JoinReplay` and the actual game-save load entry validate embedded identity even when callers bypass a browser or manually copy a file. Add precise English/Chinese mismatch/legacy-hidden copy. Test Base→Yuri, Yuri→Base, same-profile/different-digest, copied mismatch through browser/direct replay/direct save/deep-link/server entry points, and unchanged desktop Legacy behavior.

- [ ] **Step 4: Run GREEN, full focused builds, and commit**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~RuntimeContractBuilderTest|FullyQualifiedName~RuntimeContractHandshakeTest|FullyQualifiedName~RuntimeContractPersistenceTest' \
  -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Game/OpenRA.Game.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj -c Release --no-restore -m:1
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet restore ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -p:RuntimeIdentifier=iossimulator-arm64 -m:1
TASK5_AOT_CHECK="$(mktemp -d /private/tmp/nuke-hour-task5-aot.XXXXXX)"
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet msbuild ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -t:GenerateAotObjectRegistry \
  -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:GeneratedAotRegistryFile="$TASK5_AOT_CHECK/one.cs"
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet msbuild ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -t:GenerateAotObjectRegistry \
  -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:GeneratedAotRegistryFile="$TASK5_AOT_CHECK/two.cs"
cmp "$TASK5_AOT_CHECK/one.cs" "$TASK5_AOT_CHECK/two.cs"
cmp "$TASK5_AOT_CHECK/one.cs" ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs
git diff --check
```

Commit the one listed outer path plus the 14 listed engine paths (15 paths total):

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 5 --phase implementation
git commit -m "Scope runtime persistence by content contract"
```

- [ ] **Step 5: Generate and verify `0022` immediately**

Generate from official+`0001`…`0021` using the exact 22-path union:

```bash
set -euo pipefail
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
PATCH_STAGE="$(mktemp -d /private/tmp/nuke-hour-engine-patch-0022.XXXXXX)"
ALLOWLIST="$PATCH_STAGE/0022.allowlist"
VERIFY_DIR="$PATCH_STAGE/verification"
PREDECESSOR_END=21
CANDIDATE=engine-patches/0022-ios-retail-runtime-contract.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0022 --output "$ALLOWLIST"
LC_ALL=C sort -c -u "$ALLOWLIST"
test "$(wc -l < "$ALLOWLIST" | tr -d ' ')" -eq 22
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
test -f "$VERIFY_DIR/evidence.json"
```

The self-contained block above runs candidate generation and the independent verifier; update README/verifier tests only after that fresh replay passes. Commit the three delivery paths:

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 5 --phase delivery
git commit -m "Deliver runtime contract engine patch"
```

---

### Task 6: Add canonical generation metadata and root-constrained durable writes

**Production files committed in this task:**

- Create: `OpenRA.Mods.RA2/Content/RetailImportPaths.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportMetadata.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportMetadataCodec.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportDurableFileSystem.cs`
- Create: `packaging/ios-retail-task6-deferred-engine-tests.sha256`

**Deferred ignored engine tests (created and run now, first force-added in Task 14's `0027` feature commit):**

- Create: `engine/OpenRA.Test/RetailImportMetadataTest.cs`
- Create: `engine/OpenRA.Test/RetailImportDurabilityTest.cs`

- [ ] **Step 1: Write RED deterministic-codec and path tests**

Use one AOT-safe binary envelope, not reflection JSON:

```text
4 bytes magic "NHRI"
1 byte document kind
4 bytes schema version, little endian
8 bytes payload length, little endian
payload with checked strict-UTF8 length prefixes
32 bytes SHA-256 over header + payload
```

V1 freezes schema version `1`, a maximum payload length of `128L << 20` bytes, and these exact one-byte document kinds:

```csharp
internal enum RetailImportDocumentKind : byte
{
	ActiveGenerationPointer = 1,
	PublishJournal = 2,
	GenerationCatalog = 3,
	GenerationReceipt = 4,
	PendingSeal = 5,
}
```

The 128 MiB limit applies to the payload alone; the fixed 17-byte header and 32-byte checksum are outside it. The decoder rejects a declared payload above that cap, any value that cannot fit checked framework buffer/count APIs, or a total-length mismatch before allocating the payload. Schema `0`, unknown kinds under schema `1`, and malformed older/current documents are corruption; a schema value greater than `1` maps to `UpdateRequired` without parsing its kind or payload. Lock records for active pointer, publish journal, generation catalog, generation receipt with nested group/map receipts, and pending seal. Tests reject checksum/truncation/trailing bytes, invalid UTF-8, unknown kind/version, overflow, noncanonical generation/commit/pending/hash IDs, absolute/traversal/alternate-separator paths, and any encoded external/account path. `RetailImportMetadataTest` owns the RED matrix for all five kind values, schema `0|1|2`, kind `0|1..5|6`, payload `128 MiB - 1|exact|+1`, every nominal DTO invariant, pending-seal mutation, edition rejection, and the independent receipt-domain meter vectors. `RetailImportDurabilityTest` owns root/path/ID resolution plus atomic-write, storage-policy, fsync, replacement, cancellation, and outside-sentinel failpoint coverage. No case may be moved out of these two ignored Task 6 files to make the focused RED command compile.

```csharp
// Schema field-layout shorthand only; these are not public positional-record declarations.
public enum RetailPublishState
{
	Prepared = 1,
	Publishing = 2,
	Committed = 3,
}

public enum RetailRecoveryDisposition
{
	NoJournal,
	Missing,
	KeptPrevious,
	ConfirmedProposed,
	RestoredPrevious,
	ReadyWithCorruptJournalWarning,
	RepairRequired,
	ResetRequired,
	UpdateRequired,
	RecoveryFailed,
}

public sealed record ActiveGenerationPointer(
	int SchemaVersion,
	string GenerationId,
	long Epoch,
	string CommitId,
	string CatalogSha256,
	string ReceiptsSha256);

public sealed record RetailPublishJournal(
	int SchemaVersion,
	RetailPublishState State,
	long Epoch,
	bool HasPreviousGeneration,
	string PreviousGenerationId,
	string ProposedGenerationId,
	string CommitId,
	string SourcePendingId,
	ImmutableArray<RetailContentGroup> AffectedGroups,
	ImmutableArray<string> AffectedRelativePaths);

public sealed record RetailPendingArtifact(
	int Ordinal,
	string SafeBasename,
	string CanonicalStagedRelativePath,
	RetailContentGroup Group,
	long Length,
	string Sha256);

public sealed record RetailPendingSeal(
	int SchemaVersion,
	string PendingId,
	DateTimeOffset SealedAtUtc,
	ImmutableArray<RetailPendingArtifact> Artifacts,
	long TotalBytes);

public sealed record RetailMapReceipt(
	string LogicalName,
	string SourceSha256,
	string PackageSha256,
	long CompressedBytes,
	string MapUid,
	Ra2MapCompatibleProfiles CompatibleProfiles,
	ValidatedMapMetadata Metadata,
	int EnvelopeVersion,
	int ConverterVersion,
	int MapFormat,
	long RetainedMetadataBytes,
	long RetainedStringBytes,
	int RetainedStringEntries);

public sealed record MapResidentUsage(
	long MetadataBytes,
	long StringBytes,
	int StringEntries);

internal static class RetailMapResidentUsageMeterV1
{
	internal const int SchemaVersion = 1;
	internal static MapResidentUsage Measure(
		ValidatedMapMetadata metadata,
		int mapFormat);
}

public sealed record RetailReceiptEntry(
	string CanonicalRelativePath,
	long Length,
	string Sha256,
	string RecognizedEditionCode);

public sealed record RetailGroupReceipt(
	RetailContentGroup Group,
	RetailGroupValidation State,
	RetailEditionRecognition Recognition,
	RetailImportFailure Failure,
	ImmutableArray<RetailReceiptEntry> Entries,
	int ValidatorVersion);

public sealed record RetailCandidateCatalog(
	Ra2RuntimeProfileId Profile,
	Ra2RuntimeContract Contract,
	ImmutableArray<RetailCandidateArtifact> Artifacts,
	ImmutableArray<RetailGroupReceipt> Groups,
	ImmutableArray<RetailMapReceipt> Maps,
	MapResidentUsage MapResidentUsage);

public sealed record RetailCandidateArtifact(
	string LogicalName,
	string CanonicalStagedRelativePath,
	RetailContentGroup Group,
	long Length,
	string Sha256);

internal sealed class RetailCandidateSourceRootCapability { }

public sealed class RetailCandidateArtifactSource : IDisposable
{
	internal string SourcePendingId { get; }
	internal RetailCandidateArtifactSource(
		string sourcePendingId,
		RetailCandidateSourceRootCapability rootCapability);
	public Stream OpenVerifiedRead(RetailCandidateArtifact artifact);
	public void Dispose();
}

public sealed record RetailCatalogArtifact(
	string LogicalName,
	string CanonicalGenerationRelativePath,
	RetailContentGroup Group,
	long Length,
	string Sha256);

public sealed record RetailGenerationCatalog(
	Ra2RuntimeProfileId Profile,
	Ra2RuntimeContract Contract,
	ImmutableArray<RetailCatalogArtifact> Artifacts,
	ImmutableArray<RetailGroupReceipt> Groups,
	ImmutableArray<RetailMapReceipt> Maps,
	MapResidentUsage MapResidentUsage);

public interface IRetailCatalogArtifactSource : IDisposable
{
	Stream OpenVerifiedRead(RetailCatalogArtifact artifact);
}

public sealed record RetailCatalogRepairPlan(
	ImmutableArray<RetailContentGroup> RemoveGroups,
	ImmutableArray<string> RemoveMapLogicalNames,
	RetailImportFailure Reason);

public sealed record RetailReadinessResult(
	ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> Groups,
	ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure> GroupFailures,
	int BaseCompatibleMapCount,
	int YuriCompatibleMapCount,
	int QuarantinedMapCount,
	RetailImportFailure Failure);

public sealed record RetailCandidateValidation(
	RetailCandidateCatalog Catalog,
	RetailReadinessResult Readiness);

public sealed record RetailRepairValidation(
	RetailGenerationCatalog Catalog,
	RetailReadinessResult Readiness);

public sealed record RetailGenerationReceipt(
	long Epoch,
	string CommitId,
	string SourcePendingId,
	Ra2RuntimeProfileId Profile,
	Ra2RuntimeContract Contract,
	string FilenameCatalogDigest,
	int ArchiveValidatorVersion,
	int MapValidatorVersion,
	ImmutableArray<RetailGroupReceipt> Groups,
	ImmutableArray<RetailMapReceipt> Maps,
	MapResidentUsage MapResidentUsage);

public sealed record RetailGenerationImportResult(
	bool Published,
	bool PendingSealed,
	string PendingId,
	RetailReadinessResult ActiveReadiness,
	RetailImportFailure Failure);
```

The positional `record(...)` notation in the field-layout block above is schema shorthand, not a public construction contract. Production implements `ActiveGenerationPointer`, `RetailPublishJournal`, `RetailPendingArtifact`, `RetailPendingSeal`, `RetailMapReceipt`, `MapResidentUsage`, `RetailReceiptEntry`, `RetailGroupReceipt`, `RetailCandidateCatalog`, `RetailCandidateArtifact`, `RetailCatalogArtifact`, `RetailGenerationCatalog`, `RetailCatalogRepairPlan`, `RetailReadinessResult`, `RetailCandidateValidation`, `RetailRepairValidation`, `RetailGenerationReceipt`, and `RetailGenerationImportResult` as sealed nominal get-only types. Persistent/value DTOs use internal validating constructors callable only by their codec/service and friend tests; result/authority types use private constructors plus closed internal factories. None has a public instance/copy constructor, setter/init accessor, record `with` path, mutable collection, or subclass seam. The same rule is normative for later authority/evidence DTOs explicitly called out in Tasks 8–12 and 14. Existing test assemblies use `InternalsVisibleTo`; ordinary consumers cannot mint a receipt, Ready/readiness result, validation result, repair plan, or successful import. API-shape tests enumerate this exact list and reject any public constructor/init/with surface before codec round-trip and illegal cross-product tests run. `RetailGenerationImportResult` is the permanent name of the typed generation/pending authority through Tasks 6–14 and afterward; it never aliases, subclasses, or replaces the characterized legacy `RetailImportResult`. Task 14 deletes that legacy raw-path result with its old importer surface, leaving `RetailGenerationImportResult` unchanged.

`RetailPendingSeal` is the sole V1 pending-seal document, and its inner `SchemaVersion` must equal the authenticated outer-envelope schema `1`. `PendingId` uses the fixed `p-{guid:N}` grammar. `SealedAtUtc` must have offset zero and is encoded as its checked signed 64-bit UTC tick count; expiry is not serialized and is derived exactly once as `SealedAtUtc + IosImportEnvelopePolicyV1.PendingRetention`, with overflow rejected. `Artifacts` has 1–4,096 contiguous zero-based ordinals and is encoded in ordinal order. Every row contains only a safe basename, one transaction-root-relative canonical staged path accepted by `RetailStagingTarget`, the exact closed group classified from that basename, length in `0..IosImportEnvelopePolicyV1.SourceBytes`, and lowercase SHA-256. Paths and ordinal aliases are unique; non-map canonical basenames and map SHA-256 values are independently unique exactly as in the retained output of `RetailStagedPlan.Seal`; `TotalBytes` is the checked exact row sum at most 8 GiB. No `RetailSourceId`, picker/provider path, account identifier, security-scoped URL, `RetailCandidatePlan`, or caller root is serializable. Task 8 may reconstruct an internal sanitized `RetailStagedPlan` only by assigning root index zero and deterministic synthetic relative paths `sealed/{ordinal:D4}/{SafeBasename}` to these authenticated rows; those IDs are never persisted or treated as source identity.

`SourcePendingId` is null for a direct pre-game import and is the exact canonical pending ID for a resumed sealed import. The generation receipt and every state of its publish journal bind the same optional value; a mismatch fails authentication. This linkage is the durable proof used to finish pending consumption after a crash, so recovery never infers an ID from a directory name. `RetailGroupReceipt.Failure` is non-null exactly when `State=Invalid`; its group/code/diagnostic/fixed safe basename must equal the corresponding Task 2/3 `GroupFailures` entry, while Missing/Unavailable/Ready rows require null. Reconstructing readiness derives its exact enum-sorted failure dictionary solely from these authenticated per-group fields and rejects any missing, extra, duplicate, mismatched, or invalid-state failure before Ready. Codec tests cover direct/pending linkage, canonical/malformed pending IDs, exact pending-seal UTC/expiry and sanitized-artifact round trips, every optional group failure round trip, omission/substitution/state mismatch, and independent seal/journal/receipt tampering.

All optional reference fields above are plain reference types because nullable annotations are disabled. `PreviousGenerationId` is non-null exactly when `HasPreviousGeneration`; `SourcePendingId` is non-null exactly for a pending-backed transaction. V1 has no reviewed retail fingerprint catalog, so its trusted edition-code closed set is empty: `RetailEditionRecognition.Recognized` and every non-null `RecognizedEditionCode` are invalid V1 metadata, while structurally accepted retail data uses `CompatibleUnrecognized` with a null code. A future nonempty edition-code set requires a separately reviewed fingerprint catalog, schema/validator version change, and new tests; it cannot be introduced by accepting a new string under schema `1`. Readiness `Failure` is null exactly when its five-key projection is usable. `RetailGenerationImportResult` factories close its tuple: published success has `Published=true`, `PendingSealed=false`, null `PendingId`, non-null `ActiveReadiness`, and null `Failure`; sealed-for-next-launch success has `Published=false`, `PendingSealed=true`, a canonical non-null `PendingId`, optional preserved non-null readiness only when an authenticated active generation survives, and null `Failure`; retained-pending failure has the same false/true/canonical-ID shape and one non-null `Failure`; a failure without a sealed pending has both pending fields false/null and one non-null `Failure`. Private construction rejects every other combination, empty sentinels, and null required values. Successful discard returns the non-generic `RetailOperationResult`, not an ambiguous import-result tuple.

Every collection record defensively copies to an immutable ordinal representation, rejects null/duplicate/out-of-order data where canonical order is required, and cannot change when the caller later mutates its source list/dictionary. Candidate artifacts contain only canonical logical names and transaction-root-relative staged paths plus verified length/hash; no picker path or unrestricted root is representable. `RetailCandidateArtifactSource` is the sealed read capability bound to one transaction snapshot: the same internal constructor receives both the root capability and optional pending ID, and the object itself performs every `OpenVerifiedRead`. A direct/transient source carries null; a pending source carries its exact authenticated pending ID. There is no public constructor, interface, inheritance, delegate, detachable provenance object, or caller-supplied open callback, so an external wrapper cannot pair one source's identity with another source's bytes. `RetailReadinessResult.Groups` is an `ImmutableSortedDictionary` with a closed exact five-key enum-order comparer (Base, RA2 Music, Yuri, Yuri Music, Maps); no missing, extra, or reordered group is encodable. Its `GroupFailures` is the exact immutable enum-sorted projection of Task 3's map: one entry for every and only `Validation=Invalid` optional group, key/group/code/diagnostic/safe-basename identical, and no entry for Missing/Unavailable/Ready. Candidate validation, repair, catalog materialization, receipt codecs, cold reopen, and UI detail projection copy and authenticate both dictionaries together; tampering either side or dropping a failure fails before Ready. `BaseCompatibleMapCount` and `YuriCompatibleMapCount` are checked projections of the exact receipt flags; Base readiness requires the former to be positive, and a Yuri active profile requires the latter to be positive. `BaseReady` and `YuriReady` are derived helpers, never independently serialized. Under V1 `RecognizedEditionCode` is always null and `Recognition=Recognized` is rejected; codec tests cover `CompatibleUnrecognized` round trips plus independent non-null-code and `Recognized` tamper. `RetailMapReceipt` is a lossless projection of every trust-relevant `ValidatedMapArtifact` field, including bounded immutable metadata; its 40-lowercase-hex UID is validated by the codec and Task 10 converts it to the strong `MapUid` without reparsing YAML. For a map, `LogicalName` must equal that same lowercase UID string exactly and its generation path is exactly `maps/{uid}.oramap`; neither value is ever derived from source basename, YAML Title, author, localized text, or `SafeName`. `RetailCatalogRepairPlan.RemoveMapLogicalNames` therefore contains only canonical UID strings. Equal UID plus equal package digest/metadata deduplicates; equal UID with any differing package digest, length, or canonical metadata is `DuplicateConflict` and no candidate is chosen. Codec/materialization tests cover Unicode/confusable titles, same basename/different UID, different basename/same UID, UID case changes, path suffix changes, and UID collision tamper. `MapResidentUsage` must equal the checked sum of those exact per-map counters, stays within the global 64 MiB metadata and 32 MiB/262,144 string budgets, and is authenticated identically in candidate/catalog/receipt; mismatched, negative, overflowing, or caller-mutated usage fails. Candidate artifacts refer only to sealed staging. `PrepareAsync` creates a distinct `RetailGenerationCatalog` whose artifacts refer only to canonical generation-relative paths, and no staged DTO is serialized into or accepted by an active catalog. The generation receipt binds transaction epoch/commit, profile/full RA2 contract, filename-catalog digest, validator versions, group receipts, map receipts, and exact resident usage. The journal's affected groups and canonical relative paths are closed, sorted, duplicate-free, and authenticated; corrupt journal bytes are never used to choose a filesystem path. Codec tests mutate constructor inputs and tamper every serialized field independently. `HasPreviousGeneration == false` is valid only for epoch zero/first install and requires a null previous ID. Otherwise the previous ID is mandatory and canonical. The codec never represents “no previous” as an empty path-like string.

Task 6 owns `RetailMapResidentUsageMeterV1` in `RetailImportMetadata.cs`; receipt codecs may not depend on Task 10's later engine patch. Its canonical V1 stream begins with ASCII `NHRM` and one byte `1`, then writes `mapFormat`, Title, Author, Categories, Tileset, GridType, MapSize X/Y, Bounds X/Y/Width/Height, Players, and SpawnPoints in exactly that order. Signed scalars and enums are four-byte little-endian; booleans are one canonical byte `0|1`; `Color` is four bytes R/G/B/A; every `CPos` is X and Y as signed little-endian integers plus one Layer byte. Arrays use a four-byte unsigned little-endian count. A string is a one-byte presence marker followed, when present, by a four-byte unsigned little-endian byte count and strict UTF-8 bytes; required strings must be present, while Task 3's optional player strings may use only the absent form, never an empty sentinel. Each player writes every `ValidatedMapPlayerMetadata` property in its declared property order, including ordered Allies and Enemies. `MetadataBytes` is the checked exact stream length. `StringBytes` and `StringEntries` are respectively the checked UTF-8 payload-byte sum and count of ordinal-unique present strings projected in that same traversal order, matching the per-map interner. The Task 6 codec reconstructs through Task 3's validating factories, reruns this meter with the authenticated `MapFormat`, and requires all three receipt counters to match before admission. Literal byte vectors, duplicate/multibyte strings, every field mutation, counter `-1`/exact/`+1`, and arithmetic overflow are Task 6 RED cases.

- [ ] **Step 2: Run RED**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailImportMetadataTest|FullyQualifiedName~RetailImportDurabilityTest' \
  -v minimal
```

Expected: compile failure for the missing path/codec/durable-write types.

- [ ] **Step 3: Implement durable writes through injected platform policy**

```csharp
public interface IRetailStoragePolicy
{
	RetailOperationResult ApplyAndVerify(string path, bool recursive);
}

public interface IRetailDirectoryDurability
{
	RetailOperationResult Sync(string directory);
}
```

`WriteAtomic` returns `RetailOperationResult`, resolves beneath the fixed root, writes a random sibling `CreateNew` temp, `Flush(true)`, applies/verifies storage policy, atomically replaces, reapplies/verifies, and syncs the parent directory. Directory transitions use the same result contract and policy. Raw platform exceptions are caught inside the adapter and mapped to `StoragePolicyFailure` or `SanitizedIoFailure`; cancellation remains distinct. No method accepts an arbitrary delete root.

- [ ] **Step 4: Run GREEN and commit**

Run the focused command and RA2 Release build; require `git diff --check`. Keep both ignored engine tests present and passing but unstaged so they do not become outside-allowlist drift for `0023`...`0026`. Record their exact reviewed bytes in the tracked Task 6 SHA manifest and commit only the four production paths plus that manifest:

```bash
set -euo pipefail
test -f engine/OpenRA.Test/RetailImportMetadataTest.cs
test -f engine/OpenRA.Test/RetailImportDurabilityTest.cs
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailImportMetadataTest|FullyQualifiedName~RetailImportDurabilityTest' \
  -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj \
  -c Release --no-restore -m:1
TASK6_DEFERRED_SHA256=packaging/ios-retail-task6-deferred-engine-tests.sha256
shasum -a 256 \
  engine/OpenRA.Test/RetailImportDurabilityTest.cs \
  engine/OpenRA.Test/RetailImportMetadataTest.cs | \
  LC_ALL=C sort -k2 > "$TASK6_DEFERRED_SHA256"
test "$(wc -l < "$TASK6_DEFERRED_SHA256" | tr -d ' ')" = 2
git diff --check
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 6 --phase implementation
git commit -m "Add durable retail generation metadata"
```

The two engine tests are deferred members of the exact `0027` allowlist. Record their SHA-256 values and reject any later unreviewed edit before Task 14 force-adds them.

---

Task 6 implementation commit `6b822c4` proved that its frozen metadata/codec layer intentionally exposes only canonical storage identities plus durable `EnsureDirectory`/bounded `WriteAtomic`; Task 7 cannot safely implement immutable generation reads, streamed materialization, recovery enumeration, same-volume moves, quarantine/garbage collection, free-space admission, or generation pins without extending the two Task 6 storage helpers. One evidence-driven Task 7 authority correction is therefore authorized before Task 7 RED. Its correction commit stages exactly this plan, `docs/superpowers/specs/2026-08-28-ios-retail-content-import-hardening-design.md`, `packaging/ios-retail-task-paths.json`, and the one-line Task 7 staged-count mirror in `packaging/tests/test_exact_git_stage.py`, with no production, build, engine-test, deferred-hash, generated, delivery, or Task 6 source/test change. The later Task 7 implementation may modify only the two newly listed Task 6 helpers in addition to its existing paths; Task 6's metadata/codec sources, two ignored tests, and recorded SHA manifest remain byte-frozen.

### Task 7: Publish whole immutable generations and recover every crash boundary

**Production/build files committed in this task:**

- Create: `OpenRA.Mods.RA2/Content/RetailGenerationStore.cs`
- Modify: `OpenRA.Mods.RA2/Content/RetailImportDurableFileSystem.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportFaultInjection.cs`
- Modify: `OpenRA.Mods.RA2/Content/RetailImportPaths.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportProcessLock.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportRecovery.cs`
- Create: `build/OpenRA.RetailImport.CrashHarness/OpenRA.RetailImport.CrashHarness.csproj`
- Create: `build/OpenRA.RetailImport.CrashHarness/Program.cs`
- Create: `packaging/ios-retail-task7-deferred-engine-tests.sha256`

**Deferred ignored engine tests (created and run now, first force-added in Task 14):**

- Create: `engine/OpenRA.Test/RetailGenerationStoreTest.cs`
- Create: `engine/OpenRA.Test/RetailImportRecoveryProcessTest.cs`

- [ ] **Step 1: Write RED generation/state-machine tests**

```csharp
internal enum RetailImportV1PathKind
{
	ProcessLock,
	ActivePointer,
	PublishJournal,
	GenerationsDirectory,
	QuarantineDirectory,
	PendingDirectory,
	PendingItemsDirectory,
	GenerationDirectory,
	GenerationContentDirectory,
	GenerationCatalog,
	GenerationReceipts,
	GenerationArtifact,
	PendingStagingDirectory,
	PendingStagingContentDirectory,
	PendingStagingArtifact,
	PendingSealedDirectory,
	PendingSealedContentDirectory,
	PendingSealedArtifact,
	PendingStagingSeal,
	PendingSeal,
	PendingTombstone,
	PendingConsumeMarker,
	PendingLegacyMigrationMarker,
	QuarantineEntry,
}

internal static class RetailImportPaths
{
	internal const string ProcessLockLiteral = "import.lock";
	internal const string ActivePointerLiteral = "active.ptr";
	internal const string PublishJournalLiteral = "publish.journal";
	internal const string GenerationsLiteral = "generations";
	internal const string QuarantineLiteral = "quarantine";
	internal const string PendingLiteral = "pending";
	internal const string PendingItemsLiteral = "items";
	internal const string GenerationContentLiteral = "content";
	internal const string GenerationCatalogLiteral = "catalog.bin";
	internal const string GenerationReceiptsLiteral = "receipts.bin";
	internal const string PendingContentLiteral = "content";
	internal const string PendingSealLiteral = "seal.bin";
	internal const string PendingConsumeMarkerLiteral = "consume.marker";
	internal const string PendingLegacyMigrationMarkerLiteral = "legacy-migration.marker";

	internal sealed class V1PathToken
	{
		private V1PathToken(
			RetailImportV1PathKind kind,
			string exactGenerationId,
			long exactEpoch,
			Guid exactNonce,
			string exactCanonicalRelativePath);
		internal RetailImportV1PathKind Kind { get; }
	}

	internal static V1PathToken ProcessLockPath();
	internal static V1PathToken ActivePointerPath();
	internal static V1PathToken PublishJournalPath();
	internal static V1PathToken GenerationsDirectoryPath();
	internal static V1PathToken QuarantineDirectoryPath();
	internal static V1PathToken PendingDirectoryPath();
	internal static V1PathToken PendingItemsDirectoryPath();
	internal static V1PathToken GenerationDirectoryPath(
		string generationId, long expectedEpoch);
	internal static V1PathToken GenerationContentDirectoryPath(
		string generationId, long expectedEpoch);
	internal static V1PathToken GenerationCatalogPath(
		string generationId, long expectedEpoch);
	internal static V1PathToken GenerationReceiptsPath(
		string generationId, long expectedEpoch);
	internal static V1PathToken GenerationArtifactPath(
		string generationId,
		long expectedEpoch,
		string canonicalGenerationRelativePath);
	internal static V1PathToken PendingStagingDirectoryPath(Guid nonzeroNonce);
	internal static V1PathToken PendingStagingContentDirectoryPath(Guid nonzeroNonce);
	internal static V1PathToken PendingStagingArtifactPath(
		Guid nonzeroNonce, string canonicalStagedRelativePath);
	internal static V1PathToken PendingStagingSealPath(Guid nonzeroNonce);
	internal static V1PathToken PendingSealedDirectoryPath(Guid nonzeroNonce);
	internal static V1PathToken PendingSealedContentDirectoryPath(Guid nonzeroNonce);
	internal static V1PathToken PendingSealedArtifactPath(
		Guid nonzeroNonce, string canonicalStagedRelativePath);
	internal static V1PathToken PendingSealPath(Guid nonzeroNonce);
	internal static V1PathToken PendingTombstonePath(Guid nonzeroNonce);
	internal static V1PathToken PendingConsumeMarkerPath();
	internal static V1PathToken PendingLegacyMigrationMarkerPath();
}

internal enum RetailImportDirectoryEntryKind
{
	RegularFile,
	Directory,
}

internal enum RetailImportInspectionDisposition
{
	Missing,
	RegularFile,
	Directory,
	Invalid,
	Failed,
}

internal sealed class RetailImportInspectionResult : IDisposable
{
	private RetailImportInspectionResult(/* exact closed tuple */);
	internal RetailImportInspectionDisposition Disposition { get; }
	internal RetailImportFailure Failure { get; }
	internal bool HasEntry { get; }
	internal RetailImportDirectoryEntry TakeEntry();
	public void Dispose();
}

internal sealed class RetailImportDirectoryEntry : IDisposable
{
	private RetailImportDirectoryEntry(/* pinned parent/name/type/inode/root identity */);
	internal RetailImportDirectoryEntryKind Kind { get; }
	internal bool Matches(RetailImportPaths.V1PathToken expected);
	internal bool TryGetGenerationIdentity(out string generationId, out long epoch);
	internal bool TryGetPendingItemIdentity(
		out RetailImportV1PathKind itemKind, out Guid nonce);
	public void Dispose();
}

internal sealed class RetailQuarantinedEntry : IDisposable
{
	internal RetailQuarantinedEntry(
		RetailImportDurableFileSystem.QuarantinedEntryMintToken privateMintToken);
	internal RetailOperationResult<RetailImportDurableFileSystem.QuarantinedEntryPurgeSession>
		TryConsumeFor(
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity);
	public void Dispose();
}

internal sealed class RetailPendingCleanupTombstone : IDisposable
{
	internal RetailPendingCleanupTombstone(
		RetailImportDurableFileSystem.PendingCleanupTombstoneMintToken privateMintToken);
	internal RetailOperationResult<RetailImportDurableFileSystem.PendingCleanupPurgeSession>
		TryConsumeFor(
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity);
	public void Dispose();
}

internal sealed class RetailPendingConsumptionTombstone : IDisposable
{
	internal RetailPendingConsumptionTombstone(
		RetailImportDurableFileSystem.PendingConsumptionTombstoneMintToken privateMintToken);
	internal RetailOperationResult<RetailImportDurableFileSystem.PendingConsumptionPurgeSession>
		TryConsumeFor(
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity);
	public void Dispose();
}

internal sealed class RetailCompletePendingStaging : IDisposable
{
	internal RetailCompletePendingStaging(
		RetailImportDurableFileSystem.CompletePendingStagingMintToken privateMintToken);
	internal RetailOperationResult<RetailImportDurableFileSystem.PendingPublishSession>
		TryConsumeFor(
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity);
	public void Dispose();
}

internal enum RetailRecoveredQuarantineKind
{
	FixedDocument,
	Generation,
}

internal enum RetailQuarantineReason
{
	CorruptFixedDocumentRecovery,
	NonactiveGenerationRecovery,
	GarbageCollection,
}

internal sealed class RetailQuarantineAuthorization : IDisposable
{
	internal RetailQuarantineAuthorization(
		RetailGenerationStore.QuarantineMintToken privateMintToken);
	internal RetailOperationResult<RetailGenerationStore.QuarantineSession>
		TryConsumeFor(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity,
			RetailImportDirectoryEntry expectedSource,
			RetailRecoveredQuarantineKind expectedKind,
			RetailQuarantineReason expectedReason);
	public void Dispose();
}

internal sealed class RetailQuarantineRecoveryAuthorization : IDisposable
{
	internal RetailQuarantineRecoveryAuthorization(
		RetailGenerationStore.QuarantineRecoveryMintToken privateMintToken);
	internal RetailOperationResult<RetailGenerationStore.QuarantineRecoverySession>
		TryConsumeFor(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity,
			RetailImportDirectoryEntry expectedQuarantineEntry,
			RetailRecoveredQuarantineKind expectedKind);
	public void Dispose();
}

internal enum RetailPendingCleanupReason
{
	IncompleteStaging,
	MalformedSealed,
	ExpiredSealed,
	OrphanedSealed,
	ConfirmedDiscard,
	RecoveredCleanupTombstone,
}

internal sealed class RetailPendingCleanupAuthorization : IDisposable
{
	internal RetailPendingCleanupAuthorization(
		RetailGenerationStore.PendingCleanupMintToken privateMintToken);
	internal RetailOperationResult<RetailGenerationStore.PendingCleanupRenameSession>
		TryConsumeForRename(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity,
			RetailImportDirectoryEntry expectedSource,
			RetailPendingCleanupReason expectedReason);
	internal RetailOperationResult<RetailGenerationStore.PendingCleanupRecoverySession>
		TryConsumeForRecovery(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity,
			RetailImportDirectoryEntry expectedTombstone);
	public void Dispose();
}

internal sealed class RetailAuthenticatedPendingMarker : IDisposable
{
	internal RetailAuthenticatedPendingMarker(
		RetailImportDurableFileSystem.AuthenticatedPendingMarkerMintToken privateMintToken);
	internal RetailOperationResult<RetailImportDurableFileSystem.PendingMarkerClearSession>
		TryConsumeFor(
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity);
	public void Dispose();
}

internal sealed class RetailLegacyMarkerClearAuthorization : IDisposable
{
	internal RetailLegacyMarkerClearAuthorization(
		RetailGenerationStore.LegacyMarkerClearMintToken privateMintToken);
	internal RetailOperationResult<RetailGenerationStore.PendingMarkerAuthenticationSession>
		TryConsumeFor(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity,
			RetailPendingMarkerCodec.RetailDecodedPendingMarkerFile ownedDecodedMarker);
	public void Dispose();
}

internal enum RetailPendingConsumptionTransitionKind
{
	PrepareAndRenameSealedToTombstone,
	RecoverConsumptionTombstone,
	CommitAfterMissingConsumptionTombstone,
	AuthenticatePreparedMarkerForClear,
	AuthenticateConsumedMarkerForClear,
}

// Task 7 owns the private authorization/session shape. Task 8 adds only the
// structural binding factory; Task 14 adds the sole final pair-authority mint.
internal sealed partial class RetailPendingConsumptionAuthorization : IDisposable
{
	private RetailPendingConsumptionAuthorization(
		RetailGenerationStore.PendingConsumptionFinalMintSession privateMintSession);

	internal sealed partial class StructuralBinding : IDisposable
	{
		private StructuralBinding(
			RetailGenerationStore.PendingConsumptionStructuralToken privateToken);
		private RetailOperationResult<RetailGenerationStore.PendingConsumptionFinalMintSession>
			TryConsumeForFinalPair(RetailReadyGenerationCapability expectedReady);
		public void Dispose();
	}

	internal RetailOperationResult<RetailGenerationStore.PendingConsumptionSession>
		TryConsumeForRename(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity,
			RetailReadyGenerationCapability expectedReady,
			RetailImportDirectoryEntry expectedSealedPending);
	internal RetailOperationResult<RetailGenerationStore.PendingTombstoneRecoverySession>
		TryConsumeForTombstoneRecovery(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity,
			RetailReadyGenerationCapability expectedReady,
			RetailImportDirectoryEntry expectedTombstone,
			RetailPendingMarkerCodec.RetailDecodedPendingMarkerFile ownedDecodedMarker);
	internal RetailOperationResult<RetailGenerationStore.PendingConsumptionCommitRecoverySession>
		TryConsumeForCommitRecovery(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity,
			RetailReadyGenerationCapability expectedReady,
			RetailPendingMarkerCodec.RetailDecodedPendingMarkerFile ownedDecodedMarker);
	internal RetailOperationResult<RetailGenerationStore.PendingMarkerAuthenticationSession>
		TryConsumeForMarkerClear(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity,
			RetailReadyGenerationCapability expectedReady,
			RetailPendingMarkerCodec.RetailDecodedPendingMarkerFile ownedDecodedMarker);
	public void Dispose();
}

internal sealed class RetailConfirmedResetAuthorization : IDisposable
{
	internal RetailConfirmedResetAuthorization(
		RetailGenerationStore.ConfirmedResetMintToken privateMintToken);
	internal RetailOperationResult<RetailGenerationStore.ConfirmedResetSession>
		TryConsumeFor(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease);
	public void Dispose();
}

internal sealed class RetailQuarantinedRoot : IDisposable
{
	internal RetailQuarantinedRoot(
		RetailImportDurableFileSystem.QuarantinedRootMintToken privateMintToken);
	internal RetailOperationResult<RetailImportDurableFileSystem.QuarantinedRootPurgeSession>
		TryConsumeFor(RetailImportDurableFileSystem expectedRoot);
	public void Dispose();
}

internal sealed class RetailImportPinnedFile : IDisposable
{
	internal long Length { get; }
	internal Stream OpenRead();
	internal RetailOperationResult VerifyUnchanged();
	public void Dispose();
}

// Task 7 defines only this independently compilable type and private state.
// Task 8 adds the canonical DecodePinned implementation in the same partial type.
internal sealed partial class RetailPendingMarkerCodec
{
	private RetailPendingMarkerCodec();

	internal sealed class RetailDecodedPendingMarkerFile : IDisposable
	{
		private RetailDecodedPendingMarkerFile(
			RetailImportPaths.V1PathToken exactMarker,
			RetailImportPinnedFile ownedPinnedFile,
			int exactSchemaVersion,
			byte exactDocumentKind,
			byte exactState,
			string exactPendingId,
			string exactGenerationId,
			long exactEpoch,
			string exactCommitId,
			byte[] exactCanonicalDocumentSha256);
		internal RetailOperationResult<RetailGenerationStore.PendingMarkerAuthenticationSession>
			TryConsumeForMarker(
				RetailGenerationStore.PendingMarkerPairAuthenticationToken
					privateExpectedToken);
		internal RetailOperationResult<RetailGenerationStore.PendingMarkerAuthenticationSession>
			TryConsumeForLegacyMarker(
				RetailGenerationStore.LegacyMarkerClearMintToken
					privateExpectedToken);
		internal RetailOperationResult<RetailGenerationStore.PendingTombstoneRecoverySession>
			TryConsumeForTombstone(
				RetailGenerationStore.PendingTombstonePairAuthenticationToken
					privateExpectedToken);
		internal RetailOperationResult<RetailGenerationStore.PendingConsumptionCommitRecoverySession>
			TryConsumeForCommitRecovery(
				RetailGenerationStore.PendingConsumptionCommitPairAuthenticationToken
					privateExpectedToken);
		public void Dispose();
	}
}

internal sealed class RetailImportGenerationPin : IDisposable
{
	internal RetailOperationResult<RetailImportGenerationPin> Retain();
	internal RetailOperationResult VerifyUnchanged();
	public void Dispose();
}

internal enum RetailOuterResetTransactionState : byte
{
	Prepared = 1,
	Renamed = 2,
}

internal sealed class RetailImportDurableFileSystem : IDisposable
{
	internal const string OuterResetLockLiteral =
		".retail-import-reset.lock";
	internal const string OuterResetTransactionLiteral =
		".retail-import-reset.transaction";
	internal const string OuterResetTransactionMagic = "NHRT";
	internal const byte OuterResetTransactionVersion = 1;
	internal const int OuterResetTransactionV1Length = 86;

	internal sealed class OuterResetLockLease : IDisposable
	{
		private OuterResetLockLease(
			/* pinned outer-parent identity + fixed no-follow regular-file descriptor +
			 * nonzero acquisition epoch + true non-blocking OS lock */);
		public void Dispose();
	}

	internal sealed class QuarantinedEntryMintToken
	{
		private QuarantinedEntryMintToken(
			/* exact root/name/inode + live lease epoch + unconsumed purge state */);
		internal RetailOperationResult<QuarantinedEntryPurgeSession> TryConsumeFor(
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity);
	}

	internal sealed class QuarantinedEntryPurgeSession : IDisposable
	{
		private QuarantinedEntryPurgeSession(
			QuarantinedEntryMintToken uniquelyConsumedPrivateMintToken);
		public void Dispose();
	}

	internal sealed class PendingCleanupTombstoneMintToken
	{
		private PendingCleanupTombstoneMintToken(
			/* exact root/t-{nonce}/inode + cleanup family + lease epoch/op +
			 * complete authenticated reference-set digest + unconsumed state */);
		internal RetailOperationResult<PendingCleanupPurgeSession> TryConsumeFor(
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity);
	}

	internal sealed class PendingCleanupPurgeSession : IDisposable
	{
		private PendingCleanupPurgeSession(
			PendingCleanupTombstoneMintToken uniquelyConsumedPrivateMintToken);
		public void Dispose();
	}

	internal sealed class PendingConsumptionTombstoneMintToken
	{
		private PendingConsumptionTombstoneMintToken(
			/* exact root/t-{nonce}/inode + pair-bound Ready transaction +
			 * lease epoch/op + exact pending dispatcher + unconsumed state */);
		internal RetailOperationResult<PendingConsumptionPurgeSession> TryConsumeFor(
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity);
	}

	internal sealed class PendingConsumptionPurgeSession : IDisposable
	{
		private PendingConsumptionPurgeSession(
			PendingConsumptionTombstoneMintToken uniquelyConsumedPrivateMintToken);
		public void Dispose();
	}

	internal sealed class CompletePendingStagingMintToken
	{
		private CompletePendingStagingMintToken(
			/* exact root/s-{nonce}/inode + canonical seal/artifact set + lease epoch/op +
			 * same-nonce p destination + exact Task 7 pending dispatcher */);
		internal RetailOperationResult<PendingPublishSession> TryConsumeFor(
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity);
	}

	internal sealed class PendingPublishSession : IDisposable
	{
		private PendingPublishSession(
			CompletePendingStagingMintToken uniquelyConsumedPrivateMintToken);
		public void Dispose();
	}

	internal sealed class AuthenticatedPendingMarkerMintToken
	{
		private AuthenticatedPendingMarkerMintToken(
			/* exact root/fixed marker/inode + lease epoch/op + pair-bound consumption or
			 * authenticated legacy transaction +
			 * consumed decoded-pinned owner + unconsumed clear state */);
		internal RetailOperationResult<PendingMarkerClearSession> TryConsumeFor(
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity);
	}

	internal sealed class PendingMarkerClearSession : IDisposable
	{
		private PendingMarkerClearSession(
			AuthenticatedPendingMarkerMintToken uniquelyConsumedPrivateMintToken);
		public void Dispose();
	}

	internal sealed class QuarantinedRootMintToken
	{
		private QuarantinedRootMintToken(
			/* pinned parent/sibling/root inode + uniquely owned OuterResetLockLease +
			 * exact RetailImportFaultInjection transferred from confirmed authorization */);
		internal RetailOperationResult<QuarantinedRootPurgeSession> TryConsumeFor(
			RetailImportDurableFileSystem expectedRoot);
	}

	internal sealed class QuarantinedRootPurgeSession : IDisposable
	{
		private QuarantinedRootPurgeSession(
			QuarantinedRootMintToken uniquelyConsumedPrivateMintToken);
		public void Dispose();
	}

	// Task 7 additions to the Task 6 adapter. Its sole private nine-parameter
	// constructor and Open signature stay unchanged; Open's implementation first
	// acquires the private outer-reset lease and converges before trusting a root.
	internal RetailImportInspectionResult Inspect(
		RetailImportProcessLockLease liveLease,
		RetailImportPaths.V1PathToken path);
	internal RetailOperationResult<ImmutableArray<RetailImportDirectoryEntry>> Enumerate(
		RetailImportProcessLockLease liveLease,
		RetailImportPaths.V1PathToken fixedDirectory);
	internal RetailOperationResult<RetailImportPinnedFile> OpenPinnedRead(
		RetailImportProcessLockLease liveLease,
		RetailImportPaths.V1PathToken file);
	internal RetailOperationResult EnsureDirectory(
		RetailImportProcessLockLease liveLease,
		RetailImportPaths.V1PathToken directory,
		CancellationToken cancellationToken);
	internal RetailOperationResult WriteAtomic(
		RetailImportProcessLockLease liveLease,
		RetailImportPaths.V1PathToken document,
		byte[] payload,
		CancellationToken cancellationToken);
	internal Task<RetailOperationResult<RetailImportDirectoryEntry>> MaterializeCreateNewAsync(
		RetailImportProcessLockLease liveLease,
		RetailImportPaths.V1PathToken destination,
		RetailPendingFaultInjection pendingFaultInjection,
		Stream ownedVerifiedSource,
		long expectedLength,
		string expectedSha256,
		RetailValidationDeadline alreadyStartedDeadline,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken,
		IProgress<RetailImportProgress> progress);
	internal RetailOperationResult MoveAtomic(
		PendingPublishSession ownedSession,
		CancellationToken cancellationToken);
	internal RetailOperationResult<RetailCompletePendingStaging> FinalizePendingStaging(
		RetailImportProcessLockLease liveLease,
		RetailImportDirectoryEntry authenticatedStaging,
		RetailPendingSeal canonicalSeal,
		RetailPendingFaultInjection pendingFaultInjection,
		CancellationToken cancellationToken);
	internal RetailOperationResult<RetailQuarantinedEntry> Quarantine(
		RetailGenerationStore.QuarantineSession ownedSession,
		CancellationToken cancellationToken);
	internal RetailOperationResult Purge(
		QuarantinedEntryPurgeSession ownedSession,
		CancellationToken cancellationToken);
	internal RetailOperationResult<RetailQuarantinedEntry>
		AuthenticateQuarantinedEntryForRecovery(
			RetailGenerationStore.QuarantineRecoverySession ownedSession);
	internal RetailOperationResult<RetailPendingCleanupTombstone>
		TombstonePendingForCleanupAtomic(
			RetailGenerationStore.PendingCleanupRenameSession ownedSession,
			CancellationToken cancellationToken);
	internal RetailOperationResult<RetailPendingCleanupTombstone>
		AuthenticatePendingCleanupTombstoneForRecovery(
			RetailGenerationStore.PendingCleanupRecoverySession ownedSession);
	internal RetailOperationResult PurgePendingCleanupTombstone(
		PendingCleanupPurgeSession ownedSession,
		CancellationToken cancellationToken);
	internal RetailOperationResult<RetailPendingConsumptionTombstone>
		TombstonePendingForConsumptionAtomic(
			RetailGenerationStore.PendingConsumptionSession ownedSession,
		CancellationToken cancellationToken);
	internal RetailOperationResult PurgePendingConsumptionTombstoneAndCommit(
		PendingConsumptionPurgeSession ownedSession,
		CancellationToken cancellationToken);
	internal RetailOperationResult CommitAfterMissingConsumptionTombstone(
		RetailGenerationStore.PendingConsumptionCommitRecoverySession ownedSession,
		CancellationToken cancellationToken);
	internal RetailOperationResult<RetailPendingConsumptionTombstone>
		AuthenticatePendingTombstoneForRecovery(
			RetailGenerationStore.PendingTombstoneRecoverySession ownedSession);
	internal RetailOperationResult<RetailAuthenticatedPendingMarker> AuthenticatePendingMarker(
		RetailGenerationStore.PendingMarkerAuthenticationSession ownedSession);
	internal RetailOperationResult ClearAuthenticatedMarker(
		PendingMarkerClearSession ownedSession,
		CancellationToken cancellationToken);
	internal RetailOperationResult<long> GetAvailableFreeSpace(
		RetailImportProcessLockLease liveLease);
	internal RetailOperationResult<RetailImportGenerationPin> PinGeneration(
		RetailImportProcessLockLease liveLease,
		string generationId,
		long expectedEpoch);
	internal RetailOperationResult<RetailQuarantinedRoot> QuarantineRootForConfirmedReset(
		RetailGenerationStore.ConfirmedResetSession ownedSession,
		CancellationToken cancellationToken);
	internal RetailOperationResult PurgeQuarantinedRoot(
		QuarantinedRootPurgeSession ownedSession,
		CancellationToken cancellationToken);
	private RetailOperationResult ConvergeOuterResetTransaction(
		OuterResetLockLease ownedOuterResetLock);
	internal bool IsSameRoot(RetailImportProcessLock processLock);
	public void Dispose();
}

internal enum RetailImportProcessLockAcquireDisposition
{
	Acquired,
	Busy,
	Failed,
}

internal sealed class RetailImportProcessLockAcquireResult : IDisposable
{
	private RetailImportProcessLockAcquireResult(/* exact closed tuple */);
	internal RetailImportProcessLockAcquireDisposition Disposition { get; }
	internal RetailImportFailure Failure { get; }
	internal bool HasLease { get; }
	internal RetailImportProcessLockLease TakeLease();
	public void Dispose();
}

internal sealed class RetailImportProcessLock : IDisposable
{
	internal sealed class AcquisitionToken
	{
		private AcquisitionToken(
			/* exact root identity + nonzero acquisition epoch + freshly minted operation identity */);
	}

	private RetailImportProcessLock(/* pinned import.lock descriptor + root identity */);
	internal static RetailOperationResult<RetailImportProcessLock> Open(
		RetailImportDurableFileSystem storage);
	internal RetailImportProcessLockAcquireResult TryAcquire();
	internal bool IsBoundTo(RetailImportDurableFileSystem storage);
	public void Dispose();
}

public sealed class RetailImportProcessLockLease : IDisposable
{
	internal RetailImportProcessLockLease(
		RetailImportProcessLock.AcquisitionToken privateAcquisitionToken);
	internal bool IsLiveAndOwnedBy(
		RetailImportProcessLock expectedLock,
		RetailImportDurableFileSystem expectedRoot);
	internal RetailImportOperationIdentity BorrowOperationIdentity();
	internal bool OwnsOperationIdentity(
		RetailImportOperationIdentity expectedIdentity);
	public void Dispose();
}

internal sealed class RetailImportFaultInjection
{
	internal static RetailImportFaultInjection Disabled { get; }
	internal RetailImportFaultInjection(Action<RetailImportFailpoint> callback);
	internal void Hit(RetailImportFailpoint failpoint);
}

// Task 7 owns this enum, dispatcher, and every Hit site. Task 8 may construct
// one dispatcher and pass it through the already-frozen Task 7 APIs, but may
// neither define a second dispatcher nor synthesize an after-return boundary.
public enum RetailPendingFailpoint
{
	StagingArtifactFlushed,
	StagingSealFlushed,
	CompleteStagingAuthenticated,
	StagingRenamedToSealed,
	StagingParentDirectoryFlushed,
	ReadyCapabilityCreated,
	PreparedMarkerFlushed,
	PendingRenamedToTombstone,
	PendingTombstoneParentDirectoryFlushed,
	TombstoneDeleted,
	ConsumedMarkerFlushed,
	ConsumptionMarkerAuthenticated,
	ConsumptionMarkerCleared,
	CleanupAuthorizationAuthenticated,
	CleanupPendingRenamedToTombstone,
	CleanupParentDirectoryFlushed,
	CleanupTombstoneRecoveryAuthenticated,
	CleanupTombstoneDeleted,
}

internal sealed class RetailPendingFaultInjection
{
	internal static RetailPendingFaultInjection Disabled { get; }
	internal RetailPendingFaultInjection(Action<RetailPendingFailpoint> callback);
	internal void Hit(RetailPendingFailpoint failpoint);
}

public sealed class RetailPublishSpaceAdmission : IDisposable
{
	private RetailPublishSpaceAdmission(
		RetailGenerationStore.SpaceAdmissionMintToken privateMintToken);
	internal static RetailPublishSpaceAdmission FromStoreMintToken(
		RetailGenerationStore.SpaceAdmissionMintToken privateMintToken);
	internal RetailOperationResult<RetailGenerationStore.PreparedSpaceAdmission> TryConsumeFor(
		RetailGenerationStore expectedStore,
		RetailImportDurableFileSystem expectedRoot,
		RetailImportProcessLockLease expectedLease,
		RetailImportOperationIdentity expectedOperationIdentity,
		RetailValidatedCandidateInput expectedValidatedInput);
	internal RetailOperationResult<RetailGenerationStore.PreparedSpaceAdmission> TryConsumeFor(
		RetailGenerationStore expectedStore,
		RetailImportDurableFileSystem expectedRoot,
		RetailImportProcessLockLease expectedLease,
		RetailImportOperationIdentity expectedOperationIdentity,
		RetailValidatedRepairInput expectedValidatedRepairInput);
	public void Dispose();
}

public enum RetailPublishDisposition
{
	Published,
	RolledBack,
	Failed,
}

public enum RetailGenerationValidationDisposition
{
	Ready,
	RepairRequired,
	Invalid,
	Inconclusive,
}

public sealed class RetailGenerationValidationResult
{
	private RetailGenerationValidationResult(
		RetailGenerationValidationDisposition disposition,
		RetailReadinessResult readiness,
		RetailCatalogRepairPlan repair,
		RetailImportFailure failure,
		RetailActiveGenerationSnapshot borrowedCandidateSnapshot,
		RetailGenerationValidationIdentity identity);
	public RetailGenerationValidationDisposition Disposition { get; }
	public RetailReadinessResult Readiness { get; }
	public RetailCatalogRepairPlan Repair { get; }
	public RetailImportFailure Failure { get; }
	internal RetailGenerationValidationIdentity Identity { get; }
	internal bool MatchesCandidate(
		RetailActiveGenerationSnapshot candidate);
	internal static RetailGenerationValidationResult Ready(
		RetailActiveGenerationSnapshot snapshot,
		RetailReadinessResult readiness);
	internal static RetailGenerationValidationResult RepairRequired(
		RetailActiveGenerationSnapshot snapshot,
		RetailReadinessResult readiness,
		RetailCatalogRepairPlan repair);
	internal static RetailGenerationValidationResult Invalid(
		RetailActiveGenerationSnapshot snapshot,
		RetailReadinessResult optionalReadiness,
		RetailImportFailure failure);
	internal static RetailGenerationValidationResult Inconclusive(
		RetailActiveGenerationSnapshot snapshot,
		RetailImportFailure failure);
}

public sealed class RetailReadyGenerationCapability
{
	internal ActiveGenerationPointer Pointer { get; }
	internal RetailGenerationReceipt Receipt { get; }
	internal RetailGenerationValidationIdentity ValidationIdentity { get; }
	internal RetailReadyGenerationCapability(
		ActiveGenerationPointer pointer,
		RetailGenerationReceipt receipt,
		RetailReadyRootCapability rootCapability,
		RetailGenerationValidationIdentity validationIdentity);
}

internal sealed class RetailReadyRootCapability { }

public sealed class RetailValidatedCandidateInput : IDisposable
{
	private RetailValidatedCandidateInput(
		RetailCandidateValidation validation,
		RetailCandidateArtifactSource exactBorrowedSource,
		string exactSourcePendingId,
		RetailActiveGenerationSnapshot exactBorrowedActive,
		RetailGenerationValidationIdentity exactActiveIdentity,
		RetailImportOperationIdentity operationIdentity);
	internal RetailCandidateValidation Validation { get; }
	internal RetailCandidateArtifactSource ExactBorrowedSource { get; }
	internal string ExactSourcePendingId { get; }
	internal RetailActiveGenerationSnapshot ExactBorrowedActive { get; }
	internal RetailImportOperationIdentity OperationIdentity { get; }
	internal static RetailOperationResult<RetailValidatedCandidateInput> CreateFromValidator(
		RetailCandidateValidation validation,
		RetailCandidateArtifactSource exactBorrowedSource,
		RetailActiveGenerationSnapshot exactBorrowedActive,
		RetailImportOperationIdentity operationIdentity);
	internal bool MatchesExactSourceAndActive();
	public void Dispose();
}

public sealed class RetailValidatedRepairInput : IDisposable
{
	private RetailValidatedRepairInput(
		RetailActiveGenerationSnapshot exactBorrowedActive,
		RetailGenerationValidationIdentity exactActiveIdentity,
		RetailCatalogRepairPlan exactRepairPlan,
		RetailRepairValidation validation,
		RetailImportOperationIdentity operationIdentity);
	internal RetailActiveGenerationSnapshot ExactBorrowedActive { get; }
	internal RetailCatalogRepairPlan ExactRepairPlan { get; }
	internal RetailRepairValidation Validation { get; }
	internal RetailImportOperationIdentity OperationIdentity { get; }
	internal static RetailOperationResult<RetailValidatedRepairInput> CreateFromValidator(
		RetailActiveGenerationSnapshot exactBorrowedActive,
		RetailCatalogRepairPlan exactRepairPlan,
		RetailRepairValidation validation,
		RetailImportOperationIdentity operationIdentity);
	internal bool MatchesExactActiveAndPlan();
	public void Dispose();
}

public sealed class RetailImportOperationIdentity
{
	internal RetailImportOperationIdentity(
		RetailImportProcessLock.AcquisitionToken privateAcquisitionToken);
}

public sealed class PreparedRetailGeneration : IDisposable
{
	// The private state owns exact StoreIdentity + RootIdentity + OperationIdentity together
	// with generation/readiness/source, lease/deadline/clock/admission/root capability.
	internal PreparedRetailGeneration(
		RetailGenerationStore.PreparedMintToken privateMintToken);
	internal RetailOperationResult<RetailGenerationStore.PreparedPublicationSession> TryConsumeFor(
		RetailGenerationStore expectedStore,
		RetailImportDurableFileSystem expectedRoot);
	// This method only forwards to the uniquely held PreparedMintToken.TryConsumeFor.
	public void Dispose();
}

public sealed class RetailPublishResult : IDisposable
{
	private RetailPublishResult(/* exact closed tuple */);
	public RetailPublishDisposition Disposition { get; }
	public RetailReadyGenerationCapability Ready { get; }
	public RetailReadinessResult Readiness { get; }
	public RetailImportFailure Failure { get; }
	public bool HasActiveSnapshot { get; }
	public RetailActiveGenerationSnapshot TakeActiveSnapshot();
	public void Dispose();
	// Internal exact-state factories only.
}

public enum RetailActiveOpenDisposition
{
	Missing,
	Ready,
	RepairRequired,
	Invalid,
	Inconclusive,
}

public sealed class RetailActiveOpenResult : IDisposable
{
	private RetailActiveOpenResult(/* exact closed tuple */);
	public RetailActiveOpenDisposition Disposition { get; }
	public RetailReadyGenerationCapability Ready { get; }
	public RetailReadinessResult Readiness { get; }
	public RetailCatalogRepairPlan Repair { get; }
	public RetailImportFailure Failure { get; }
	public bool HasActiveSnapshot { get; }
	public RetailActiveGenerationSnapshot TakeActiveSnapshot();
	public void Dispose();
	// Internal exact-state factories only.
}

internal sealed class RetailGenerationValidationIdentity { }

public abstract class RetailRecoveryValidationLease : IAsyncDisposable
{
	internal RetailRecoveryValidationLease();
	internal abstract RetailGenerationValidationIdentity ValidationIdentity { get; }
	public abstract ValueTask DisposeAsync();
}

public enum RetailRecoveryCandidateDisposition
{
	Ready,
	RepairRequired,
	Invalid,
	Inconclusive,
}

public sealed class RetailRecoveryCandidateValidation<TLease> : IAsyncDisposable
	where TLease : RetailRecoveryValidationLease
{
	readonly RetailActiveGenerationSnapshot borrowedCandidateSnapshot;
	TLease ownedValidationLease;

	private RetailRecoveryCandidateValidation(
		RetailRecoveryCandidateDisposition disposition,
		RetailReadinessResult readiness,
		RetailCatalogRepairPlan repair,
		RetailImportFailure failure,
		TLease ownedValidationLease,
		RetailActiveGenerationSnapshot borrowedCandidateSnapshot,
		RetailGenerationValidationIdentity identity);
	public RetailRecoveryCandidateDisposition Disposition { get; }
	public RetailReadinessResult Readiness { get; }
	public RetailCatalogRepairPlan Repair { get; }
	public RetailImportFailure Failure { get; }
	internal bool HasValidationLease { get; }
	internal RetailGenerationValidationIdentity Identity { get; }
	internal TLease TakeValidationLease();
	internal bool MatchesCandidate(
		RetailActiveGenerationSnapshot candidate);
	public ValueTask DisposeAsync();
	internal static ValueTask<RetailRecoveryCandidateValidation<TLease>> ReadyAsync(
		RetailActiveGenerationSnapshot snapshot,
		RetailReadinessResult readiness,
		TLease ownedValidationLease);
	internal static RetailRecoveryCandidateValidation<TLease> RepairRequired(
		RetailActiveGenerationSnapshot snapshot,
		RetailReadinessResult readiness,
		RetailCatalogRepairPlan repair);
	internal static RetailRecoveryCandidateValidation<TLease> Invalid(
		RetailActiveGenerationSnapshot snapshot,
		RetailImportFailure failure);
	internal static RetailRecoveryCandidateValidation<TLease> Inconclusive(
		RetailActiveGenerationSnapshot snapshot,
		RetailImportFailure failure);
}

public sealed class RetailRecoverySession : IDisposable
{
	internal RetailRecoverySession(/* store-owned pinned recovery state */);
	public Task<RetailRecoveryResult<TLease>> CompleteAsync<TLease>(
		RetailValidationDeadline alreadyStartedDeadline,
		IRetailMonotonicClock clock,
		Func<
			RetailActiveGenerationSnapshot,
			RetailValidationDeadline,
			IRetailMonotonicClock,
			CancellationToken,
			Task<RetailRecoveryCandidateValidation<TLease>>> validator)
		where TLease : RetailRecoveryValidationLease;
	public void Dispose();
}

public sealed class RetailRecoveryResult<TLease> : IAsyncDisposable
	where TLease : RetailRecoveryValidationLease
{
	private RetailRecoveryResult(/* exact closed tuple */);
	public RetailRecoveryDisposition Disposition { get; }
	public RetailReadyGenerationCapability Ready { get; }
	public RetailReadinessResult Readiness { get; }
	public RetailCatalogRepairPlan Repair { get; }
	public RetailImportFailure Failure { get; }
	public bool HasValidationLease { get; }
	public bool HasActiveSnapshot { get; }
	public RetailActiveGenerationSnapshot TakeActiveSnapshot();
	public TLease TakeValidationLease();
	public ValueTask DisposeAsync();
}

internal sealed class RetailPreparedRootCapability { }
internal sealed class RetailGenerationRootCapability { }

public sealed class RetailActiveGenerationSnapshot :
	IRetailCatalogArtifactSource, IDisposable
{
	public ActiveGenerationPointer Pointer { get; }
	public RetailGenerationReceipt Receipt { get; }
	public RetailGenerationCatalog Catalog { get; }
	internal RetailGenerationValidationIdentity ValidationIdentity { get; }
	public Stream OpenVerifiedRead(RetailCatalogArtifact artifact);
	internal RetailActiveGenerationSnapshot(
		ActiveGenerationPointer pointer,
		RetailGenerationReceipt receipt,
		RetailGenerationCatalog catalog,
		RetailGenerationRootCapability rootCapability,
		RetailGenerationValidationIdentity validationIdentity);
	public void Dispose();
}

public enum RetailRepairPublishDisposition
{
	Published,
	RepairRequired,
	Invalid,
	Inconclusive,
	Failed,
}

public sealed class RetailRepairPublishResult<TLease> : IAsyncDisposable
	where TLease : RetailRecoveryValidationLease
{
	private RetailRepairPublishResult(/* exact closed tuple */);
	public RetailRepairPublishDisposition Disposition { get; }
	public RetailReadyGenerationCapability Ready { get; }
	public RetailReadinessResult Readiness { get; }
	public RetailCatalogRepairPlan Repair { get; }
	public RetailImportFailure Failure { get; }
	public bool HasValidationLease { get; }
	public bool HasActiveSnapshot { get; }
	public RetailActiveGenerationSnapshot TakeActiveSnapshot();
	public TLease TakeValidationLease();
	public ValueTask DisposeAsync();
}

public sealed class RetailGenerationStore : IDisposable
{
	internal sealed class StoreIdentity
	{
		private StoreIdentity();
	}

	internal sealed class RootIdentity
	{
		private RootIdentity();
	}

	internal sealed class PreparedMintToken
	{
		private PreparedMintToken(
			/* exact StoreIdentity + RootIdentity + OperationIdentity and all owners */);
		internal RetailOperationResult<PreparedPublicationSession> TryConsumeFor(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot);
	}

	internal sealed class PreparedPublicationSession : IDisposable
	{
		private PreparedPublicationSession(
			PreparedMintToken uniquelyConsumedPrivateMintToken);
		internal RetailOperationResult RecheckSpaceForPointerSwap();
		public void Dispose();
	}

	internal sealed class SpaceAdmissionMintToken
	{
		private SpaceAdmissionMintToken(
			/* exact StoreIdentity + RootIdentity + live lease acquisition epoch +
			 * OperationIdentity + exact validated-wrapper reference/identity +
			 * measured retained-on-disk census + peak incremental plan + metered writes */);
		internal RetailOperationResult<PreparedSpaceAdmission> TryConsumeFor(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity,
			RetailValidatedCandidateInput expectedValidatedInput);
		internal RetailOperationResult<PreparedSpaceAdmission> TryConsumeFor(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity,
			RetailValidatedRepairInput expectedValidatedRepairInput);
	}

	internal sealed class PreparedSpaceAdmission : IDisposable
	{
		private PreparedSpaceAdmission(
			SpaceAdmissionMintToken uniquelyConsumedPrivateMintToken);
		internal RetailOperationResult RecheckForPointerSwap();
		public void Dispose();
	}

	internal sealed class ConfirmedResetMintToken
	{
		private ConfirmedResetMintToken(
			/* exact StoreIdentity + RootIdentity + parent/root inode + live lease epoch +
			 * ReferenceEquals Store-owned RetailImportFaultInjection */);
		internal RetailOperationResult<ConfirmedResetSession> TryConsumeFor(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease);
	}

	internal sealed class ConfirmedResetSession : IDisposable
	{
		private ConfirmedResetSession(
			ConfirmedResetMintToken uniquelyConsumedPrivateMintToken);
		public void Dispose();
	}

	internal sealed class QuarantineMintToken
	{
		private QuarantineMintToken(
			/* exact StoreIdentity + RootIdentity + lease epoch + OperationIdentity +
			 * source entry reference/name/inode/kind + closed reason + recovery/GC
			 * eligibility + active/pointer/journal/pin/complete-reference-set digest */);
		internal RetailOperationResult<QuarantineSession> TryConsumeFor(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity,
			RetailImportDirectoryEntry expectedSource,
			RetailRecoveredQuarantineKind expectedKind,
			RetailQuarantineReason expectedReason);
	}

	internal sealed class QuarantineSession : IDisposable
	{
		private QuarantineSession(
			QuarantineMintToken uniquelyConsumedPrivateMintToken);
		public void Dispose();
	}

	internal sealed class QuarantineRecoveryMintToken
	{
		private QuarantineRecoveryMintToken(
			/* exact StoreIdentity + RootIdentity + lease epoch + OperationIdentity +
			 * enumerated q entry reference/name/inode + fixed recovery kind + authenticated
			 * active/pointer/journal/pin/complete-reference-set digest */);
		internal RetailOperationResult<QuarantineRecoverySession> TryConsumeFor(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity,
			RetailImportDirectoryEntry expectedQuarantineEntry,
			RetailRecoveredQuarantineKind expectedKind);
	}

	internal sealed class QuarantineRecoverySession : IDisposable
	{
		private QuarantineRecoverySession(
			QuarantineRecoveryMintToken uniquelyConsumedPrivateMintToken);
		public void Dispose();
	}

	internal sealed class PendingCleanupMintToken
	{
		private PendingCleanupMintToken(
			/* exact StoreIdentity + RootIdentity + lease epoch + OperationIdentity +
			 * s|p|t entry reference/name/inode/nonce + closed cleanup reason + full
			 * active/pointer/journal/receipt/seal/pin/reference-set digest + no
			 * matching authenticated consumption marker */);
		internal RetailOperationResult<PendingCleanupRenameSession> TryConsumeForRename(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity,
			RetailImportDirectoryEntry expectedSource,
			RetailPendingCleanupReason expectedReason);
		internal RetailOperationResult<PendingCleanupRecoverySession> TryConsumeForRecovery(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity,
			RetailImportDirectoryEntry expectedTombstone);
	}

	internal sealed class PendingCleanupRenameSession : IDisposable
	{
		private PendingCleanupRenameSession(
			PendingCleanupMintToken uniquelyConsumedPrivateMintToken);
		public void Dispose();
	}

	internal sealed class PendingCleanupRecoverySession : IDisposable
	{
		private PendingCleanupRecoverySession(
			PendingCleanupMintToken uniquelyConsumedPrivateMintToken);
		public void Dispose();
	}

	internal sealed class LegacyMarkerClearMintToken
	{
		private LegacyMarkerClearMintToken(
			/* exact StoreIdentity + RootIdentity + lease epoch + OperationIdentity +
			 * fixed legacy marker entry/inode + authenticated completed-migration state +
			 * complete reference set + expected canonical marker tuple */);
		internal RetailOperationResult<PendingMarkerAuthenticationSession>
			TryConsumeFor(
				RetailGenerationStore expectedStore,
				RetailImportDurableFileSystem expectedRoot,
				RetailImportProcessLockLease expectedLease,
				RetailImportOperationIdentity expectedOperationIdentity,
				RetailPendingMarkerCodec.RetailDecodedPendingMarkerFile ownedDecodedMarker);
	}

	internal sealed class PendingConsumptionStructuralToken
	{
		private PendingConsumptionStructuralToken(
			/* exact StoreIdentity + RootIdentity + lease epoch + OperationIdentity +
			 * exact Ready reference/ValidationIdentity + current pointer/receipt/profile/
			 * contract/selection projection + p|t|fixed-marker entry/inode/nonce + closed
			 * transition + complete reference set + exact pending dispatcher */);
		internal RetailOperationResult<PendingConsumptionFinalMintSession>
			TryConsumeForFinalPair(RetailReadyGenerationCapability expectedReady);
	}

	internal sealed class PendingConsumptionFinalMintSession
	{
		private PendingConsumptionFinalMintSession(
			PendingConsumptionStructuralToken uniquelyConsumedStructuralToken);
		internal RetailOperationResult<PendingConsumptionSession> TryConsumeForRename(
			RetailGenerationStore expectedStore,
			RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease,
			RetailImportOperationIdentity expectedOperationIdentity,
			RetailReadyGenerationCapability expectedReady,
			RetailImportDirectoryEntry expectedSealedPending);
		internal RetailOperationResult<PendingTombstoneRecoverySession>
			TryConsumeForTombstoneRecovery(
				RetailGenerationStore expectedStore,
				RetailImportDurableFileSystem expectedRoot,
				RetailImportProcessLockLease expectedLease,
				RetailImportOperationIdentity expectedOperationIdentity,
				RetailReadyGenerationCapability expectedReady,
				RetailImportDirectoryEntry expectedTombstone,
				RetailPendingMarkerCodec.RetailDecodedPendingMarkerFile ownedDecodedMarker);
		internal RetailOperationResult<PendingConsumptionCommitRecoverySession>
			TryConsumeForCommitRecovery(
				RetailGenerationStore expectedStore,
				RetailImportDurableFileSystem expectedRoot,
				RetailImportProcessLockLease expectedLease,
				RetailImportOperationIdentity expectedOperationIdentity,
				RetailReadyGenerationCapability expectedReady,
				RetailPendingMarkerCodec.RetailDecodedPendingMarkerFile ownedDecodedMarker);
		internal RetailOperationResult<PendingMarkerAuthenticationSession>
			TryConsumeForMarkerClear(
				RetailGenerationStore expectedStore,
				RetailImportDurableFileSystem expectedRoot,
				RetailImportProcessLockLease expectedLease,
				RetailImportOperationIdentity expectedOperationIdentity,
				RetailReadyGenerationCapability expectedReady,
				RetailPendingMarkerCodec.RetailDecodedPendingMarkerFile ownedDecodedMarker);
	}

	internal sealed class PendingMarkerPairAuthenticationToken
	{
		private PendingMarkerPairAuthenticationToken(
			/* consumed pair authority + exact marker token/inode/schema/digest and complete
			 * expected document-kind/state/pending/generation/epoch/commit tuple */);
		internal RetailOperationResult<PendingMarkerAuthenticationSession>
			AuthenticateAndMint(/* complete decoded semantic tuple + owned pinned file */);
	}

	internal sealed class PendingTombstonePairAuthenticationToken
	{
		private PendingTombstonePairAuthenticationToken(
			/* consumed pair authority + exact t inode/nonce + exact marker token/inode/
			 * schema/digest + Ready/receipt transaction and complete expected tuple */);
		internal RetailOperationResult<PendingTombstoneRecoverySession>
			AuthenticateAndMint(/* complete decoded semantic tuple + owned pinned file */);
	}

	internal sealed class PendingConsumptionCommitPairAuthenticationToken
	{
		private PendingConsumptionCommitPairAuthenticationToken(
			/* consumed pair authority + exact missing p/t census + exact Prepared-marker
			 * token/inode/schema/digest + Ready/receipt transaction and expected tuple */);
		internal RetailOperationResult<PendingConsumptionCommitRecoverySession>
			AuthenticateAndMint(/* complete decoded semantic tuple + owned pinned file */);
	}

	internal sealed class PendingConsumptionSession : IDisposable
	{
		private PendingConsumptionSession(
			PendingConsumptionFinalMintSession uniquelyConsumedPrivateMintSession);
		public void Dispose();
	}

	internal sealed class PendingTombstoneRecoverySession : IDisposable
	{
		private PendingTombstoneRecoverySession(
			PendingTombstonePairAuthenticationToken uniquelyConsumedPrivateToken);
		public void Dispose();
	}

	internal sealed class PendingConsumptionCommitRecoverySession : IDisposable
	{
		private PendingConsumptionCommitRecoverySession(
			PendingConsumptionCommitPairAuthenticationToken uniquelyConsumedPrivateToken);
		public void Dispose();
	}

	internal sealed class PendingMarkerAuthenticationSession : IDisposable
	{
		private PendingMarkerAuthenticationSession(
			PendingMarkerPairAuthenticationToken uniquelyConsumedPrivateToken);
		private PendingMarkerAuthenticationSession(
			LegacyMarkerClearMintToken uniquelyConsumedPrivateToken);
		public void Dispose();
	}

	internal RetailGenerationStore(
		RetailImportDurableFileSystem ownedStorage,
		RetailImportProcessLock ownedProcessLock,
		IRetailMonotonicClock clock,
		RetailImportFaultInjection faultInjection,
		RetailPendingFaultInjection pendingFaultInjection);

	internal RetailOperationResult<RetailPublishSpaceAdmission> CreateSpaceAdmission(
		RetailValidatedCandidateInput validatedInput,
		RetailImportProcessLockLease liveOperationLock);

	internal RetailOperationResult<RetailPublishSpaceAdmission> CreateSpaceAdmission(
		RetailValidatedRepairInput validatedRepairInput,
		RetailImportProcessLockLease liveOperationLock);

	// Pure checked arithmetic only: no root query, authority mint, census getter,
	// caller-capacity production seam, or state mutation. Both real probes call it.
	private static RetailOperationResult EvaluateAvailableBytes(
		long actualAvailableBytes,
		long exactPeakIncrementalBytes);

	public Task<RetailOperationResult<PreparedRetailGeneration>> PrepareAsync(
		RetailValidatedCandidateInput validatedInput,
		RetailImportProcessLockLease ownedOperationLock,
		RetailValidationDeadline alreadyStartedDeadline,
		IRetailMonotonicClock clock,
		RetailPublishSpaceAdmission spaceAdmission,
		CancellationToken cancellationToken,
		IProgress<RetailImportProgress> progress);

	public Task<RetailOperationResult<PreparedRetailGeneration>> PrepareRepairAsync(
		RetailValidatedRepairInput validatedRepairInput,
		RetailImportProcessLockLease ownedOperationLock,
		RetailValidationDeadline alreadyStartedDeadline,
		IRetailMonotonicClock clock,
		RetailPublishSpaceAdmission spaceAdmission,
		CancellationToken cancellationToken,
		IProgress<RetailImportProgress> progress);

	public Task<RetailPublishResult> PublishAsync(
		PreparedRetailGeneration prepared,
		Func<
			RetailActiveGenerationSnapshot,
			RetailValidationDeadline,
			IRetailMonotonicClock,
			CancellationToken,
			Task<RetailGenerationValidationResult>> postSwitchValidator);

	internal Task<RetailRepairPublishResult<TLease>> PublishRepairAsync<TLease>(
		PreparedRetailGeneration preparedRepair,
		Func<
			RetailActiveGenerationSnapshot,
			RetailValidationDeadline,
			IRetailMonotonicClock,
			CancellationToken,
			Task<RetailRecoveryCandidateValidation<TLease>>>
				postSwitchValidator)
		where TLease : RetailRecoveryValidationLease;

	public Task<RetailOperationResult<RetailRecoverySession>>
		BeginRecoveryAsync();

	internal RetailOperationResult<RetailConfirmedResetAuthorization>
		CreateConfirmedResetAuthorization(
			RetailImportProcessLockLease liveLease);

	internal RetailOperationResult<RetailQuarantineAuthorization>
		CreateQuarantineAuthorization(
			RetailImportProcessLockLease liveLease,
			RetailImportDirectoryEntry authenticatedSource,
			RetailRecoveredQuarantineKind expectedKind,
			RetailQuarantineReason reason);

	internal RetailOperationResult<RetailQuarantineRecoveryAuthorization>
		CreateQuarantineRecoveryAuthorization(
			RetailImportProcessLockLease liveLease,
			RetailImportDirectoryEntry enumeratedQuarantineEntry,
			RetailRecoveredQuarantineKind expectedKind);

	internal RetailOperationResult<RetailPendingCleanupAuthorization>
		CreatePendingCleanupAuthorization(
			RetailImportProcessLockLease liveLease,
			RetailImportDirectoryEntry enumeratedPendingEntry,
			RetailPendingCleanupReason reason);

	internal RetailOperationResult<RetailLegacyMarkerClearAuthorization>
		CreateLegacyMarkerClearAuthorization(
			RetailImportProcessLockLease liveLease,
			RetailImportDirectoryEntry exactLegacyMarkerEntry);

	// Task 8's partial structural binder is the only caller. This does not mint
	// consumption authority and accepts no VerifiedRa2RuntimeContract.
	internal RetailOperationResult<PendingConsumptionStructuralToken>
		CreatePendingConsumptionStructuralToken(
			RetailImportProcessLockLease liveLease,
			RetailReadyGenerationCapability exactReady,
			RetailImportDirectoryEntry exactEntry,
			RetailPendingConsumptionTransitionKind transitionKind);

	public Task<RetailActiveOpenResult> OpenActiveAsync(
		RetailImportProcessLockLease operationLock,
		Func<
			RetailActiveGenerationSnapshot,
			RetailValidationDeadline,
			IRetailMonotonicClock,
			IProgress<RetailImportProgress>,
			CancellationToken,
			Task<RetailGenerationValidationResult>> validator,
		RetailValidationDeadline alreadyStartedDeadline,
		IRetailMonotonicClock clock,
		IProgress<RetailImportProgress> progress,
		CancellationToken cancellationToken);

	public void Dispose();
}
```

Tests prove a true non-blocking operating-system lock on the no-follow fixed `import.lock` regular file, including contention from a second store instance and a second process before provider/staging/cleanup I/O; same-volume streamed materialization; no hard links/in-place edits; one generation per selection; profile/mount/map/contract/readiness revalidation before `Prepared`; pointer-only `Publishing`; complete rollback after post-switch failure; non-cancellable publish/recovery; retained previous generation; and lock/pin-protected GC. Darwin holds an `O_CREAT | O_RDWR | O_NOFOLLOW | O_CLOEXEC`, owner-only descriptor under `flock(LOCK_EX | LOCK_NB)` for the complete lease; the managed host fallback uses an equivalent non-sharing OS file handle, never an in-memory semaphore or queued wait. `TryAcquire` has exactly three private-factory tuples: `Acquired => one owned lease/null Failure`, `Busy => no lease/null Failure`, and `Failed => no lease/non-null sanitized Failure`; `TakeLease` succeeds exactly once only for Acquired and result disposal releases an untaken lease. Only the successful native/nonsharing-handle acquisition path can construct the private `AcquisitionToken`; that token binds the adapter's exact root identity plus one nonzero monotonically increasing acquisition epoch, privately mints one fresh opaque `RetailImportOperationIdentity`, and can mint exactly one lease that owns that same identity. The lease exposes only a borrowed reference and `OwnsOperationIdentity`; there is no operation-identity constructor without the unforgeable acquisition token, lease factory, public/internal token getter, boolean/out acquisition seam, or caller-created Busy value. The store constructor takes ownership of both storage and lock only after `ReferenceEquals`-stable root identities prove `ownedProcessLock.IsBoundTo(ownedStorage)`; a mismatch throws before any store state or I/O exists.

Every ordinary Task 7-added fixed-root operation requires that same live, undisposed inner process lease and rejects a different lock, root, acquisition epoch, disposed/aliased lease, or store before inspecting or mutating an inode. The sole exception is outer-root `Open`/reset convergence: it is authorized only by the adapter-private fixed outer-parent lease described below; confirmed reset initially requires both the exact inner lease and that outer lease, while a returned/reconstructed `RetailQuarantinedRoot` owns the outer lease and can finish after the quarantined inner lock file is gone. Task 6's legacy string `EnsureDirectory`/`WriteAtomic` remain only to preserve its frozen tests; the Task 7–14 production call-edge matrix forbids them. Typed inspection has exactly `Missing => no Entry/null Failure`, `RegularFile|Directory => one matching owned Entry/null Failure`, and `Invalid|Failed => no Entry/non-null Failure`; the result owns the entry until one take or disposal. An entry exposes only its closed kind plus authenticated generation `(id, epoch)` or pending `(s|p|t, nonce)` identity when that exact enumerated kind has one. Neither entry nor `V1PathToken` exposes a canonical/absolute path, filename, parent, root, descriptor, inode, or general component getter.

The operation matrix is closed. `Inspect` accepts only one exact token. `Enumerate` accepts only `GenerationsDirectory`, `QuarantineDirectory`, or `PendingItemsDirectory`, returns deterministic one-level authenticated children, and fails the whole enumeration on any unsafe/unrecognized entry. `OpenPinnedRead` accepts only fixed documents, generation catalog/receipts/artifacts, pending seal/artifacts, or pending markers. Token-based `EnsureDirectory` accepts only the named generation and pending directory kinds. Generic token-based `WriteAtomic` accepts active pointer, publish journal, generation catalog/receipts, or the legacy-migration marker; it is not a production edge for the consumption marker. `MaterializeCreateNewAsync` accepts only generation artifacts or `s-*` pending artifacts and, for the latter, invokes the exact Task 7-owned pending dispatcher only after the durable artifact boundary.

`FinalizePendingStaging` is one closed adapter operation: it re-enumerates and pins the exact same-nonce `s-*`, checks every canonical seal row/length/hash against its complete artifact set, canonically encodes and durably writes `s-{nonce}/seal.bin`, invokes `StagingSealFlushed`, reopens/decodes/verifies it unchanged, privately mints `RetailCompletePendingStaging`, and invokes `CompleteStagingAuthenticated`. That capability's atomic `TryConsumeFor` is the only bridge to a private `PendingPublishSession`; `MoveAtomic` accepts only that session, performs the same-nonce `s -> p` rename, invokes `StagingRenamedToSealed`, syncs the parent, and invokes `StagingParentDirectoryFlushed`. There is no overload taking a bare lease, entry, destination token, or completed capability.

Every live move into `quarantine/q-*` first passes through `RetailGenerationStore.CreateQuarantineAuthorization`. Store re-enumerates the complete active-pointer, journal, generation, pending-receipt, pin, and recovery/GC reference set and mints an authorization bound by reference to the exact Store/root/live lease/acquisition epoch/operation identity/source entry/name/inode/kind/closed reason and that eligibility snapshot. Its `TryConsumeFor` atomically invalidates the authorization and creates the private `QuarantineSession`; `Quarantine` accepts only that session, rechecks the entire binding, lets only the adapter choose the unused `q-{guid:N}` destination, renames and syncs both parents, and returns `RetailQuarantinedEntry`. An ordinary authenticated entry is inspection evidence only and has no direct Quarantine call. The returned cap must itself atomically `TryConsumeFor` the same root/lease/operation to create `QuarantinedEntryPurgeSession`, and `Purge` accepts only that session. After a crash, Store may mint `RetailQuarantineRecoveryAuthorization` only for a fixed-enumerated q entry after the same complete reference-set proof; its atomic `TryConsumeFor` produces `QuarantineRecoverySession`, the sole input to adapter remint. Recovery/GC authorization, live quarantine authorization, and the returned purge cap are three distinct one-shot states and expose no token getter.

Pending cleanup is not pending consumption. Store may mint `RetailPendingCleanupAuthorization` only for an exact enumerated incomplete `s-*`, malformed/invalid `p-*`, valid expired/orphan `p-*`, or crash-left cleanup `t-*` after proving from the complete active/pointer/journal/receipt/seal/pin/reference set that no live transaction, Ready receipt, consumption marker, or other owner references that nonce. For s/p, its `TryConsumeForRename` produces `PendingCleanupRenameSession`; the adapter alone performs same-nonce `s|p -> t`, syncs the parent, and returns the cleanup-only tombstone cap. A crash-left cleanup t is reauthorized from a fresh enumeration and a freshly recomputed complete reference set through `TryConsumeForRecovery`; it is never inferred merely from a `t-*` name. `RetailPendingCleanupTombstone.TryConsumeFor` is the sole bridge to `PendingCleanupPurgeSession`. Cleanup auth/session/cap cannot be passed to any consumption method, while consumption auth/session/cap cannot clean s, malformed p, or an unmarked t.

Consumption has a stricter pair-bound chain. Task 7 declares the private partial `RetailPendingConsumptionAuthorization` shape and private sessions but contains no production authorization construction. Task 8 adds only `StructuralBinding`: under the exact lease it reauthenticates current pointer, Ready receipt, profile/contract/selection projection, p/t/marker inode and complete reference set and consumes Store's `PendingConsumptionStructuralToken`; that binding is not mutation authority. Task 14 is the sole production site that first executes `ReferenceEquals(verifiedContract.Binding, ready.ValidationIdentity)`, then verifies the structural binding holds that same Ready reference, consumes it once, and invokes the private authorization constructor. Task 7 and Task 8 each have zero final-authorization `newobj`; Task 14 has exactly one. A fresh final authorization is required separately for `p -> t`, crash-left consumption-t remint, and each marker authentication/clear attempt.

The `p -> t` authorization's `TryConsumeForRename` creates `PendingConsumptionSession`; only `TombstonePendingForConsumptionAtomic` accepts it, durably writes the exact pair-bound Prepared marker, invokes its Task 7 failpoint, renames the exact p inode to the same-nonce t, invokes `PendingRenamedToTombstone`, syncs the parent, invokes `PendingTombstoneParentDirectoryFlushed`, and returns `RetailPendingConsumptionTombstone`. That cap atomically creates `PendingConsumptionPurgeSession`; only `PurgePendingConsumptionTombstoneAndCommit` may delete the t inode and durably write the pair-bound Consumed marker. A crash-left t requires a separately opened canonical marker and a fresh pair authorization. `TryConsumeForTombstoneRecovery` atomically consumes both authorization and decoded-pinned proof, calls `VerifyUnchanged`, compares the complete marker tuple with the same Ready/receipt/pointer/entry, and yields the sole private remint session. If Prepared is durable but both same-nonce p and t are absent because the crash followed t unlink, a different fresh authorization plus decoded proof and exact missing-entry census creates `PendingConsumptionCommitRecoverySession`; only `CommitAfterMissingConsumptionTombstone` may durably advance that marker to Consumed. Marker clear then reopens/canonically decodes the marker and uses yet another fresh pair authorization to create `PendingMarkerAuthenticationSession`; the adapter returns `RetailAuthenticatedPendingMarker`, whose own `TryConsumeFor` creates `PendingMarkerClearSession`, the only input to `ClearAuthenticatedMarker`. No final mutation method accepts `(lease, entry)`, `(lease, decoded marker)`, a DTO, a bare capability, or a path.

Task 7 defines the nested `RetailPendingMarkerCodec.RetailDecodedPendingMarkerFile` with a private constructor and private one-shot state containing its token, owned pin, schema/digest, and canonical document-kind/state/pending-ID/generation-ID/epoch/commit tuple; Task 8's canonical `DecodePinned` in the same partial type is the sole production decoded-proof `newobj` site and atomically binds all those fields only after complete codec validation. The proof exposes no semantic getter or delegate. A plain `RetailImportPinnedFile`, decoded DTO, unbound/reflection-constructed value, ordinary directory entry, caller path, replayed/drifted proof, wrong Ready/verified pair, or second use has no mutation bridge. Every capability/authorization/token atomically flips once while constructing its private session; failed binding consumes neither an unrelated owner nor an inode, and all aliases become inert after success and dispose idempotently. Free-space and generation-pin acquisition require the same inner lease. Process-lock, active-generation, journal-referenced, pinned, cross-root/volume, wrong-kind/nonce, self/descendant, caller-named pair, decode bypass, transaction mismatch, reference-set drift, or identity drift is rejected before mutation.

Every path component is resolved from a pinned parent only after raw directory enumeration proves one exact ordinal UTF-8 spelling and no distinct `OrdinalIgnoreCase` alias; this applies before and after create/rename to the outer importer leaf/reset lock/reset marker, inner lock, every fixed literal, and every token component. Case aliases invalidate the whole operation, including on case-folding volumes. Every controlled regular lock, marker, metadata document, and materialized artifact must have link count exactly one at open, reauthentication, rename, and before/after policy application; a preset hard link is rejected without changing its outside inode. Task 7 owns the required native identity/link-count wrappers in its filesystem/process-lock files rather than extending Task 6's frozen native identity or unrelated runtime native helper. Recursive purge uses no-follow `lstat`/`fstatat` classification at every child: only a verified directory is opened recursively, while every non-directory—including symlink and special entry—is unlinked as that directory entry without following or reading its target, so hostile leftovers cannot stall reset and outside sentinels remain byte-identical. Darwin/Linux/Windows link-count and case-fold tests run where supported, with an explicit capability gate or synthetic native adapter where the host volume cannot create the condition.

Task 7 reserves Task 8's entire path authority now. The sixth immediate root literal is exactly `pending`; partial artifacts are `pending/items/s-{guid:N}/content/{CanonicalStagedRelativePath}` and the only finalization document is `pending/items/s-{guid:N}/seal.bin`. An incomplete `s-*` may lack that seal and is never authoritative. Only after the closed finalizer authenticates the complete `s-*` does one atomic same-nonce rename expose `pending/items/p-{guid:N}/content/{CanonicalStagedRelativePath}` together with its already-durable `pending/items/p-{guid:N}/seal.bin`; no observable `p-*` may lack a seal. The remaining reserved paths are `pending/items/t-{guid:N}` and fixed `pending/consume.marker` and `pending/legacy-migration.marker`. Every GUID is nonzero lowercase `N` format, and the `RetailPendingSeal.PendingId` must be `p-` plus that same nonce. `V1PathToken`'s private payload is exactly `(kind, exactGenerationId, exactEpoch, exactNonce, exactCanonicalRelativePath)`; `PendingStagingSeal` and `PendingSeal` are distinct root-domain kinds even though both leaf literals are `seal.bin`, each factory enforces the closed per-kind null/zero/nonzero matrix, and `Kind` is the only exposed component/root-domain tag. The t spelling is shared only as a crash-stable directory grammar; its cleanup and consumption authorities, sessions, remint proofs, and purge caps are nominally distinct and never interchangeable. No Task 8 code may add a path factory or call raw `System.IO`: it must use the reserved tokens, Task 7-owned pending dispatcher, finalization session, authorized cleanup `s|p -> t`, pair-authorized consumption `p -> t`, decoded-marker proof, recovery sessions, and one-shot purge sessions above.

The snapshot constructor and generation pin are internal to the store; the public API exposes no generation-directory/root/path/descriptor/refcount getter. Its catalog is canonical, already root-contained/authenticated, and usable only with its matching pointer/receipt. Its verified artifact source accepts only catalog artifacts from that exact snapshot. A public-API/reflection test proves ordinary consumers cannot recover or enumerate the raw generation root.

`ValidateAsync` returns one `RetailValidatedCandidateInput`, not a separable validation/source/active tuple. The coordinator borrows the exact operation identity owned by its already-acquired lease and passes that same reference explicitly to the validator. The wrapper's private factory captures it together with the exact `RetailCandidateValidation`, exact borrowed `RetailCandidateArtifactSource` reference, that source's exact nullable `SourcePendingId`, and exact nullable preserved-active snapshot reference/validation identity; construction and `PrepareAsync` both require `ReferenceEquals` source/active/operation continuity plus unchanged pending ID and active identity. `PrepareAsync` accepts only that wrapper and therefore cannot pair validation from source A, lease operation A, or active A with bytes, pending linkage, lease, or preserved active state from B. It constructs the complete proposed generation as the union of verified preserved-active artifacts and staged replacements. A replacement group/map shadows only the matching logical item; every preserved byte is copied via the wrapper-bound active snapshot; every staged byte is streamed via the exact bound sealed source; and length/hash/deadline/clock are rechecked while materializing without allocating the whole artifact. Optional-only Yuri/music/map imports therefore retain Base and all unaffected valid maps without raw-path reopening. It writes the distinct generation-relative catalog and never persists staged names. `ValidateRepairAsync` likewise borrows the same lease-owned identity and returns one `RetailValidatedRepairInput` that binds it with the exact borrowed active snapshot reference/validation identity, exact repair-plan reference, and exact `RetailRepairValidation`. `PrepareRepairAsync` accepts only that wrapper, requires `ReferenceEquals(wrapper.OperationIdentity, ownedOperationLock.BorrowOperationIdentity())`, rechecks every active/plan/validation relationship, and copies only through the bound active snapshot.

Both Prepare methods take ownership of the exact OS-lock lease and one-shot admission at call entry in the fixed order `validated input, lease, deadline, clock, admission, cancellation, progress`. Before I/O they require `ReferenceEquals(wrapper.OperationIdentity, lease.BorrowOperationIdentity())` and consume the admission only through the overload whose final argument is that exact same candidate or repair wrapper reference. The admission was privately bound to that wrapper when Store recomputed its census; Prepare never supplies components, binds, or retargets it. Equal measurements from another wrapper, another validator result over the same bytes, or the other candidate/repair overload fail before I/O. Failure or cancellation releases the input, lease, and untaken admission after removing only unpublished temporary state. Success consumes the input and transfers the exact lease, original 90-minute deadline value, exact clock reference, consumed prepared admission, prepared-root capability, and private `StoreIdentity + RootIdentity + OperationIdentity` into the sole `PreparedMintToken`; `PreparedRetailGeneration` can be constructed only from that unexposed token. It has no component-authority getter, general Take, copy/clone/with path, or constructor that accepts generation/readiness/source/lease/deadline/clock/admission pieces. Its `TryConsumeFor` is only a forwarding method over its uniquely held `PreparedMintToken`; that token atomically validates the exact store/root, flips its state once, and alone constructs `PreparedPublicationSession` from the consumed private state. `PublishAsync` and `PublishRepairAsync` accept only that prepared object and their first executable action is this transition, before journal inspection, callback, free-space query, or mutation. Cross-store, cross-root, equal-looking store/root, wrong operation, retained alias, double publish, disposed prepared, or attempted repackage fails without consuming a different owner or changing bytes. Success makes every alias inert while keeping all private owners inside the guarded publication path; Publish accepts no replacement lock lease, deadline, clock, space admission, catalog, readiness, source, or root.

Every copy/hash/space/storage-policy/durability failure returns a failed `RetailOperationResult<PreparedRetailGeneration>` and writes no `Prepared` state; only cooperative cancellation before successful preparation throws `OperationCanceledException`. `OpenActiveAsync` authenticates pointer/catalog/receipts, requires a live lease from the constructor's exact OS-lock object, delegates the single content-byte validation pass described below, and returns a typed open result with an authenticated snapshot, repair plan, or closed failure; callers cannot synthesize one from a path or reopen a raw directory. Every open/publish/recovery result privately owns any non-null active snapshot until `TakeActiveSnapshot` succeeds exactly once or result disposal releases it; no direct `Active` getter exists. `Ready` is a separate non-owning opaque proof that the same active pointer/receipt completed post-switch reopen plus full Ready validation; only the store can construct it. It is non-null exactly when the internally owned active snapshot is Ready, and is null for repair, refresh-needed, no-active, reset, update, rollback failure, or hard-failure outcomes. Tests cover repair/refresh success, failure, cancellation, impossible result cross-products, take-before/after-dispose, and exactly one snapshot/lock/root disposal with no lease/descriptor leak on every branch.

All optional result references are plain C# 9 reference types with private construction and these exact runtime tuples. Ordinary generation validation is the same explicit four-state model as candidate recovery: `Ready => Readiness`; `RepairRequired => Readiness+Repair`; `Invalid => Failure` plus an optional complete Readiness only after stable complete validation proved bad content; and `Inconclusive => Failure` for timeout, cancellation, I/O/dispatcher failure, final identity drift, callback fault, illegal tuple, cleanup failure, or another operational inability to decide. Every factory also borrows the exact supplied snapshot reference and its exact `ValidationIdentity`; `MatchesCandidate` requires both `ReferenceEquals` checks. Publish is `Published => Active+Ready+Readiness and null Failure`; `RolledBack => non-null Failure` plus either an authenticated `Active+Readiness` previous pair (and `Ready` iff that pair is Ready) or all three null when no previous generation exists; `Failed => null Active/Ready/Readiness and non-null Failure`. Generic recovery `NoJournal` has null failure/repair and either no active tuple or an authenticated `Active+Ready+Readiness+owned validation lease`; `Missing` has all Active/Ready/Readiness/Repair/Failure/lease fields null and means either no pointer on a clean first launch or completed table-authorized retirement with an exact sealed pending retry—no journal is required to remain; `KeptPrevious`, `ConfirmedProposed`, and `RestoredPrevious` require that Ready tuple and null repair/failure; `ReadyWithCorruptJournalWarning` requires that tuple plus a non-null warning failure; `RepairRequired` requires authenticated `Active+Readiness+Repair`, null Ready/lease/failure; `ResetRequired`/`UpdateRequired` require null Active/Ready/Readiness/Repair/lease and non-null failure; `RecoveryFailed` requires null Active/Ready/Readiness/Repair/lease and one non-null retryable/sanitized failure and proves zero pointer, journal, inner/outer quarantine, GC, or pending-consumption mutation. Active-open has one explicit disposition and tuple: `Ready => Active+Ready+Readiness`; `RepairRequired => Active+Readiness+Repair`; `Missing => all authority/readiness/repair/failure null`; `Invalid => non-null Failure` plus either an authenticated owned `Active+Readiness` pair or both null, always with null Ready/Repair; and `Inconclusive => Failure only` with null Active/Ready/Readiness/Repair. The optional Invalid pair preserves stable five-key readiness and `GroupFailures` for Task 14's repair/reset UI while never granting Ready; result disposal or `TakeActiveSnapshot` owns/releases it normally. Inconclusive always disposes its provisional snapshot before returning. Repair publication is `Published => Active+Ready+Readiness+owned validation lease` with null repair/failure; `RepairRequired => Readiness+Repair` only; `Invalid` and `Inconclusive` each own only their typed failure; `Failed` owns only a storage/convergence failure. Every non-Published repair result has null Active/Ready/lease. `TakeValidationLease` is legal exactly once only for a Ready/Published tuple. If an attempt ever exposed `pending/items/p-{nonce}`, its wrapper and every prepared/journal/receipt state carry non-null same-nonce `SourcePendingId`; null is reserved for a genuinely direct attempt that never created `s-*` or `p-*`. Factories reject every other cross-product, null required value, empty sentinel, wrong snapshot/identity, or validation lease without Ready; tests enumerate all rows.

The store mints one fresh, non-persistent `RetailGenerationValidationIdentity` for every authenticated active snapshot created by recovery, direct open, ordinary post-switch validation, or repair publication and embeds it in that exact `RetailActiveGenerationSnapshot`. The identity is unique to that validation attempt, is never serialized, and is not derived from a generation, epoch, commit, path, receipt, or object hash. All four candidate factories privately retain only the exact supplied snapshot reference as a borrowed, non-owning reference plus its `ValidationIdentity`; they copy no path, pointer, receipt, root, or other ownership, and the recovery session remains the snapshot's sole owner. Only `Ready` owns a validation lease. `MatchesCandidate` is true only when both `ReferenceEquals(borrowedCandidateSnapshot, candidate)` and `ReferenceEquals(Identity, candidate.ValidationIdentity)` hold. `ReadyAsync` additionally requires `ReferenceEquals(snapshot.ValidationIdentity, ownedValidationLease.ValidationIdentity)` before ownership transfer; on mismatch or any later factory failure it asynchronously releases the supplied lease. The candidate result exposes no lease getter: `TakeValidationLease` is internal, legal exactly once for Ready, atomically clears ownership, and later takes fail; `DisposeAsync` awaits an untaken lease and is idempotent. `CompleteAsync` wraps every callback result in `await using`, requires `MatchesCandidate` for the exact snapshot passed to the callback, takes the selected lease exactly once, and awaits every unselected/illegal result's cleanup before releasing any snapshot or crossing the mutation boundary. A callback-retained result alias is therefore inert after completion and cannot retain or dispose the transferred lease. Every `RetailReadyGenerationCapability` receives the same identity as the snapshot whose validation minted it. Tests create two snapshots for the same authenticated pointer/receipt and reject cross-paired leases, another snapshot with equal persisted fields, wrong-identity Ready, callback substitution, duplicate identity, factory failure, retained aliases, double take, disposal before/after take, throwing cleanup, and any loss of `ReferenceEquals` continuity through candidate validation, selected result, and Ready.

`PublishRepairAsync` accepts only a `PreparedRetailGeneration` carrying the one-shot private state minted by that exact `PrepareRepairAsync`; ordinary `PublishAsync` obeys the identical ownership rule. Neither method accepts substitute lease/deadline/clock/admission arguments. After tentative pointer replacement it invokes the four-state callback exactly once for its exact post-switch snapshot, passing only the deadline/clock retained inside the consumed prepared state plus `CancellationToken.None`. Ready durably completes publication, mints Ready with the same validation identity, and transfers the callback's exact lease without reopening. RepairRequired, Invalid, or Inconclusive remain distinct, asynchronously release every provisional owner, durably follow the existing rollback table, and stop the current startup attempt; they are never relabeled `RecoveryFailed`, because repair publication may already have performed rollback writes. Storage or rollback-convergence failure returns `Failed` and exposes no authority. A process kill enters ordinary recovery on the next launch. Tests prove exactly one open/hash/final-verify for the repaired generation, exact prepared/lease/deadline/clock/admission and validation-identity transfer, no fresh recovery/open after Published, all four post-switch states, rollback, process kill, deadline expiry, callback throw, wrong identity, and async cleanup at every boundary.

`PrepareAsync` has no independently supplied source or pending-ID parameter. It reads both only through the exact one-shot `RetailValidatedCandidateInput`, rechecks its bound source reference and captured nullable pending ID before any mutation, and carries that ID unchanged through `PreparedRetailGeneration`, all publish-journal states, and the generation receipt. If any byte of this attempt became visible under `pending/items/p-{nonce}`, the canonical seal, artifact source, validator wrapper, prepared state, journal, and receipt must all carry the non-null `SourcePendingId = p-{same nonce}`; null is legal only for a genuinely direct source path that never creates either `s-*` or `p-*` at any point. Current Task 8 import/resume staging always uses `s -> p`, so both paths are non-null and same-nonce. `ResumePendingAsync` can acquire only the sealed source whose internal ID is already bound to its authenticated `PendingId`. `PrepareRepairAsync` reads active/plan/validation only from `RetailValidatedRepairInput`, preserves an authenticated active receipt's non-null source ID until consumption completes, and can never introduce or replace one. Tests reject wrapper, subclass, delegate, source-identity, null-after-staging, wrong-nonce pending ID, active-snapshot, validation-identity, plan, operation-identity, alias, and cross-validator substitution; repair/refresh preserve the link, and every publish/recovery result preserves the authenticated linkage.

`BeginRecoveryAsync` is the only normal-cold-start recovery entry. It non-blockingly acquires the same descriptor-backed operating-system lock, authenticates and pins the exact pointer, journal, receipt, catalog, and every table-authorized candidate/root identity, but reads no archive/map content, calls no validator, publishes no snapshot, and performs no pointer/quarantine/GC mutation. The returned one-shot session retains that lock and all candidate pins. Disposing an uncompleted session is zero-mutation and releases every pin/lock. `CompleteAsync` accepts the one already-started 90-minute deadline and exact constructor clock, rejects a default/wrong-budget/wrong-clock pair before callback or I/O, and passes those exact values plus `CancellationToken.None` to every lazily invoked validator at most once for each pinned candidate actually needed by the closed table. The callback's internal validation identity must be `ReferenceEquals`-bound to the exact candidate passed in. Any invoked necessary candidate that returns `Inconclusive`, or a wrong snapshot, null/default/contradictory tuple, duplicate lease, callback exception, identity drift, or awaited lease-cleanup failure returns `RecoveryFailed` before the recovery table or reauthentication can mutate anything. `RepairRequired` and `Inconclusive` are never collapsed into `Invalid`; only an explicit stable `Invalid` result may fill an invalid cell in the table. `CompleteAsync` awaits every unselected lease release in reverse validation order before reauthentication and before the non-cancellable mutation boundary; it never uses a synchronous wait or discarded cleanup task. Immediately before that boundary the session reauthenticates its pinned pointer/journal/root identities without discovering or reopening a new path and performs the final deadline check. Expiry before crossing returns zero-mutation `RecoveryFailed/Inconclusive`. After the first durable mutation, the same deadline/clock remain diagnostic context but neither expiry nor cancellation may interrupt mandatory rollback, journal/pointer convergence, parent sync, or capability cleanup; completion must reach a durable table-authorized state before reporting timeout/storage failure. Before returning, completion atomically transfers only the selected result-owned snapshot/lease, releases every other candidate pin and the process lock, and leaves the session inert; static preflight and UI work never retain the recovery lock. `CompleteAsync` is one-shot and session disposal is idempotent.

For a Ready selected candidate, `RetailRecoveryResult<TLease>` owns the selected Active snapshot and the validator's exact asynchronous lease; all unselected/failed-candidate leases and snapshots are released in reverse order. `TakeValidationLease` is legal only for a Ready tuple, succeeds once, and atomically transfers that lease; `DisposeAsync` before take releases lease then Active, while disposal after take no longer owns the lease. Its closed dispositions retain the existing table meanings, but every Ready/kept/confirmed/restored state includes the exact selected lease and every reset/update/hard-failure state includes none. The crash harness and tests cover begin/abort zero hash/mutation, previous+proposed selection, callback throw/timeout/wrong identity, each failpoint, lease transfer, double complete/take/dispose, thread-pool-continuation cleanup, and exact awaited reverse cleanup.

`OpenActiveAsync` remains the explicit non-recovery open path and authenticates pointer/catalog/receipt structure before invoking exactly one supplied active-catalog validator. On normal cold startup Task 14 instead passes `RetailActiveCatalog.OpenAsync` through the recovery session's `CompleteAsync`; that validator—not the store—owns each candidate's single content-byte cold hash, and the selected lease is transferred all the way to the coordinator without a second `OpenActiveAsync`, reopen, or hash. Immediately before the first `CompleteAsync`, the coordinator checks its lifecycle token once. From that point every candidate `BuildExpected`/open and every mandatory repair/prepare/publish/recovery retry uses `CancellationToken.None`; the same already-started monotonic deadline and identical clock still bound the entire loop. A timeout or operational failure maps to `Inconclusive -> RecoveryFailed`, never hard invalid/reset/fallback. Only the final static-resource preflight observes the lifecycle token again after the process lock is released. Direct `OpenActiveAsync` reports through the exact progress instance and may honor matching cancellation before publishing a lease. Tests prove callback/progress/deadline/clock identity, pre-completion cancellation with zero callback/mutation, cancellation immediately after the boundary without propagation into recovery, per-candidate exactly-once validation, timeout/inconclusive cleanup, and no duplicate hash pass.

`PrepareAsync` also enforces the full proposed-catalog aggregate before materialization and immediately before publication: at most 256 imported maps, 2 GiB compressed map storage, 64 MiB retained map metadata, 32 MiB/262144 retained untrusted strings, and 8 MiB strings per map. Add 256 individually valid worst-case maps, `limit + 1`, unique-string flood, and checked-sum overflow tests; aggregate rejection leaves the active pointer and previous Ready generation unchanged.

`RetailGenerationStore.CreateSpaceAdmission` accepts only the exact validated candidate/repair wrapper followed by its live lease; no caller, importer, or planner supplies a census or capacity. Under that lease the Store re-enumerates the wrapper-bound source, active/rollback generation, other retained pending data, conversion plan, and metadata plan and computes two checked groups. `observedRetainedOnDisk` is the exact already-allocated sealed source plus other retained pending and rollback bytes at this post-staging checkpoint; it is recorded for drift/under-report detection but is not added again to available-space demand. `peakIncrementalFromCheckpoint` is only the maximum additional proposed payload, converted-map output, and metadata/atomic-temporary bytes that may coexist from this checkpoint forward. Initial admission requires `available >= checked(peakIncrementalFromCheckpoint + exactly IosImportEnvelopePolicyV1.DiskSafetyMarginBytes)`, never `retained + incremental + margin`. The token binds the exact wrapper reference/identity, Store/Root/acquisition epoch/operation, measured retained identities/lengths, incremental plan, initial free-space sample, and checked requirement. During materialization Store meters every actual create/write/temporary allocation against that plan and fails before pointer work on any component under-report or identity/length drift. Immediately after `PublishingJournalFlushed`, parameterless `RecheckSpaceForPointerSwap` queries the same root and compares current free space only with the still-unwritten peak incremental pointer/journal/metadata temporary requirement plus the 512 MiB margin; bytes already written since admission and all retained bytes are not charged twice. Unknown/negative measurement, overflow, metered overrun, I/O failure, or one byte below either checkpoint fails before any pointer temporary/write/rename; exact and `+1` succeed. Both real pinned-root probes pass only their private measured values to the same private static `EvaluateAvailableBytes(actualAvailableBytes, exactPeakIncrementalBytes)`, which performs the checked addition with the fixed policy margin and returns only a non-authorizing success/failure. Reflection tests may invoke this pure arithmetic helper for deterministic negative/overflow and `-1`/exact/`+1` boundaries, but it cannot query a root, mint/consume admission, expose a census, or accept caller capacity through any production API; source/final-IL gates require both production checks to call it and forbid any other authority-producing call edge. The admission has no scalar getter, general/public/scalar factory, copy/clone/with, or retarget path, and equal numeric measurements from a different wrapper cannot consume it. It is never journaled or reconstructed: recovery confirms proposed only when the authenticated pointer already equals its generation/epoch/commit; otherwise the closed lost-admission rows below retire nonactive state and require fresh validation/admission rather than replaying this token.

- [ ] **Step 2: Add injected exception and real process-kill RED coverage**

Use this closed failpoint enum and no string-selected hook:

```csharp
public enum RetailImportFailpoint
{
	GenerationDirectoryCreated,
	PreservedFilesCopied,
	ReplacementFilesMoved,
	ReceiptsFlushed,
	CatalogFlushed,
	PreparedJournalFlushed,
	PublishingJournalFlushed,
	ActivePointerReplaced,
	CommittedJournalFlushed,
	PostCommitValidationStarted,
	QuarantineEntryRenamed,
	QuarantineRecoveryAuthenticated,
	QuarantinePurgeStarted,
	LostAdmissionRetirementCommitted,
	GarbageCollectionStarted,
	ResetPreparedMarkerFlushed,
	ResetRootRenameFlushed,
	ResetRenamedMarkerFlushed,
	ResetQuarantinePurgeStarted,
	ResetInnerLockUnlinked,
	ResetTransactionMarkerCleared,
}
```

Every failpoint is synchronous and fires exactly once only after the named durable step. `GenerationDirectoryCreated` follows directory creation, storage-policy verification, and parent sync; copied/moved follows all bytes, hashes, policy, and generation sync; each `*Flushed` follows atomic replacement and parent sync; `PublishingJournalFlushed` precedes the final free-space recheck; `ActivePointerReplaced` follows that recheck plus pointer replacement/policy/parent sync; and `PostCommitValidationStarted` occurs immediately before the sole callback. Every q-family path uses Store's authorization/session chain: `QuarantineEntryRenamed` follows adapter-chosen q rename plus both parent syncs, `QuarantineRecoveryAuthenticated` follows fresh complete-reference-set reauthorization and cap remint, and `QuarantinePurgeStarted` fires only after that cap creates its purge session. `LostAdmissionRetirementCommitted` follows durable retirement of the nonactive proposed generation and journal while any pointed Ready/RepairRequired authority and exact seal have been preserved. `GarbageCollectionStarted` is the GC-specific pre-purge observation after the same authorized q rename. Restart must authenticate/remint rather than purge an ordinary enumerated entry.

The separately closed `RetailPendingFailpoint` family is already owned by Task 7's `RetailImportFaultInjection.cs`; `RetailImportDurableFileSystem.cs` owns all Hit sites and Task 8 only constructs/injects the dispatcher. In particular, `StagingSealFlushed` and `CompleteStagingAuthenticated` occur inside `FinalizePendingStaging`; `StagingRenamedToSealed` and `StagingParentDirectoryFlushed` occur on the two sides of the parent sync inside `MoveAtomic`; and `PendingRenamedToTombstone` and `PendingTombstoneParentDirectoryFlushed` occur on the two sides of the parent sync inside pair-authorized consumption. Cleanup rename/sync/remint/purge and consumption marker/remint/clear have their own enum values and remain nominally separate. A Task 8 call to `Hit` after an adapter method returns is forbidden by source/IL tests because it cannot prove an internal durability boundary.

Reset failpoints respectively follow durable Prepared-marker replacement, root-to-sibling rename plus outer-parent fsync, durable Renamed-marker replacement, reconstruction of the exact one-shot root capability immediately before recursive purge, unlink of the quarantined root's inner `import.lock` plus sibling-directory sync, and outer transaction-marker unlink plus parent sync. Publish/q/GC failpoints follow the publish-recovery table and converge only to its exact Ready, RepairRequired, proposed, Missing-with-sealed-retry, reset, or warning result; pending cleanup/consumption follows its independently typed family; outer-reset failpoints follow the outer table to a fresh Missing root with the recorded old root purged exactly once. Every outside sentinel remains byte-identical.

Every adapter `Open`, confirmed reset, and reset convergence begins by pinning the fixed outer parent and acquiring a true non-blocking OS lease on its owner-only no-follow regular file `.retail-import-reset.lock`. Darwin uses `O_CREAT | O_RDWR | O_NOFOLLOW | O_CLOEXEC`, regular-file identity verification, and `flock(LOCK_EX | LOCK_NB)`; the managed fallback uses one equivalent non-sharing handle. First creation is parent-synced. The fixed lock is never renamed into the importer root, never recursively purged, and never removed by reset. Its lease is held continuously from before the first marker/root/sibling read through Prepared write, rename, arbitrary partial purge (including unlink of the inner `import.lock`), Renamed/marker convergence, marker clear, idempotent fresh-root creation, creation and verification of the fresh root's new inner `import.lock`, and final parent sync. Store's confirmed-reset mint binds its exact constructor-owned `RetailImportFaultInjection` by reference into the one-shot authorization. `RetailConfirmedResetAuthorization.TryConsumeFor` atomically checks the exact Store/root/live inner lease and creates the private `ConfirmedResetSession`; `QuarantineRootForConfirmedReset` accepts only that session and transfers its retained injector plus the adapter-owned outer lease into `RetailQuarantinedRoot`. That cap must in turn atomically create `QuarantinedRootPurgeSession`, the only input to live purge. Thus every named live reset durable boundary, including `ResetInnerLockUnlinked`, invokes the exact retained injector without a field, constructor argument, static, `AsyncLocal`, getter, or substitute on Task 6's adapter. Ordinary crash-time `Open` convergence reacquires only the outer lease and completes idempotently without reinjecting a second crash. A live reset returns a cap that owns this exact outer lease and injector; crash recovery reacquires the lease before reconstructing the non-injecting convergence session. A second process returns Busy/Failed and mutates nothing. The complete marker/root/sibling tuple is re-read under the outer lease before every mutation; create-if-absent fresh-root races can only join the same no-follow-verified Missing root. No inner-root or sibling `import.lock` serializes reset, no stale pre-lock snapshot authorizes mutation, and loss of an inner lock during partial purge cannot expose a concurrency gap.

The crash harness is not an RA2 friend and has no direct RA2-internal call. Its project has one direct `ProjectReference` to `../../engine/OpenRA.Test/OpenRA.Test.csproj`; that project's existing RA2 reference supplies the separately verified transitive dependency. Deferred `RetailImportRecoveryProcessTest.cs` exposes public test-only `RetailImportCrashHarnessDriver.Crash(string fixedSyntheticRoot, string exactFailpointName)` and `.Recover(string fixedSyntheticRoot)`. The driver, running as the existing `OpenRA.Test` friend, parses the enum with `ignoreCase:false`, requires `Enum.GetName(value) == exactFailpointName`, constructs the real internal storage/OS-lock/store/injector with synthetic validated metadata, calls `Environment.FailFast` at the selected checkpoint, and performs recovery in a distinct invocation. `Program` accepts exactly `crash <exact-root> <exact-enum-name>` or `recover <exact-root>`, requires the canonical no-symlink root to equal `RETAIL_IMPORT_CRASH_ROOT`, rejects extra arguments and retail bytes/paths, verifies the three-assembly contract below, and invokes only that public driver.

Each enum value has one frozen scenario rather than a result guessed from its name. `GenerationDirectoryCreated` through `PostCommitValidationStarted` start from previous Ready plus one proposed publication; the q/lost-admission family starts from its explicit Ready, pointed RepairRequired, seal-backed Missing, or no-seal Reset row; `GarbageCollectionStarted` starts from new active Ready plus one old eligible unpinned generation; and the six reset values start from one Ready root plus confirmed reset. The selected callback ignores earlier `Hit` values and writes its owner-only reached proof only when `actual == selected`; that proof contains scenario nonce, exact enum, and PID and is flushed before optional hold or `Environment.FailFast`.

Contention tests use no sleep. With `RETAIL_IMPORT_CRASH_HOLD_AT_FAILPOINT=1`, the selected callback remains inside the exact live session/lease until a nonce-checked owner-only release file appears. The parent waits for the exact reached proof, starts a second `recover` process with only `RETAIL_IMPORT_EXPECT_BUSY=1`, and requires Busy/Failed plus byte-identical marker/root/journal/sentinel snapshots before releasing the holder. `PublishingJournalFlushed` proves the inner lock; `ResetInnerLockUnlinked` proves the fixed outer lock still excludes a contender after the sibling inner lock is gone. Parent stdout/stderr drains asynchronously; every child has a bounded liveness watchdog that kills the process tree on timeout. After the held crash, two new PIDs run recovery and prove the row's first convergence plus stable second-launch state. Task 7's frozen behavioral tests reject unknown verbs and wrong/extra arity but do not assert that future Task 8 `pending-*` verbs remain forever invalid; the Task 7-time source/IL gate proves the then-current verb set.

The RED matrix is exact and every row must first fail for a named missing Task 7 contract, not for restore, fixture, syntax, or unrelated baseline failure:

| Deferred test | RED contract | Required assertions before GREEN |
| --- | --- | --- |
| `RetailGenerationStoreTest` | V1 token and durable capability surface | exact concrete token payload and generation + staging-seal/reserved pending literals/factories; closed finalize `s+seal -> complete cap -> private session -> p`; Store-minted live q authorization binds exact lease/root/op/entry inode/kind/reason and full active/journal/pin/reference set; q recovery uses a second authorization/session; incomplete s and malformed/expired/orphan p use cleanup-only auth/session/cap with crash-left t remint; consumption p/t and marker clear use nominally separate pair auth; every q/reset/staging/tombstone/marker/root/auth cap has an atomic `TryConsumeFor -> private session`; direct lease+entry, lease+decoded proof, pinned-file/DTO/ordinary-entry mutation does not compile; exact-case/no-alias, nlink=1, no-follow purge, outside-sentinel, and no raw path/delete/root API |
| `RetailGenerationStoreTest` | staged proof and final pair mint shape | Task7-compilable partial codec owns the private full-semantic decoded-pinned proof; Task 7 source/IL has zero decoded-proof mint and Task 8 exactly one canonical codec mint; Task7 owns the private partial pending-consumption authority with zero final mint, Task8 adds only structural binding with zero final mint, and Task14 has the unique authorization `newobj` strictly after `ReferenceEquals(verified.Binding, ready.ValidationIdentity)`; forgery/replay/drift/cross-pair/double-use reject before mutation |
| `RetailGenerationStoreTest` | descriptor-backed process lock | exact Acquired/Busy/Failed tuples; private-token root/epoch lease privately owns one operation identity; same-process/cross-process contention; wrong-root/store/epoch/operation and every lease-free Task 7 FS operation reject before I/O |
| `RetailGenerationStoreTest` | typed preparation/publication authority | exact validated candidate/repair wrappers borrow the lease-owned identity; `ReferenceEquals` wrapper↔lease; closed PreparedMintToken→Session transition; private Store+Root+Operation binding; private Session/PreparedSpaceAdmission constructors are referenced only by their corresponding token-consume bodies; Publish-first TryConsume; cross-store/root, null-token/default/reflection construction, repack, alias, double-use, source/pending/active/plan/operation substitution reject before mutation |
| `RetailGenerationStoreTest` | validation state machines | ordinary validation has exactly four dispositions and ActiveOpen exactly five, including Invalid's optional authenticated Active+Readiness pair and Inconclusive's required absence; enumerate every illegal cross-product |
| `RetailGenerationStoreTest` | whole-operation deadline | the same 90-minute value and `ReferenceEquals` clock traverse prepare, publish, callback, rollback, recovery, repair, and chunked materialization |
| `RetailGenerationStoreTest` | nominal space admission | exact candidate/repair wrapper + store/root/op/lease-epoch bound mint after Store recomputation; no caller census/scalar factory/getter/repack; retained-on-disk measurements are not double charged; only peak incremental + exact 512 MiB is required; metered Store writes catch every component under-report; equal numbers from another wrapper reject; private pure arithmetic helper covers negative/overflow and `-1`/exact/`+1`, and final IL proves both real pinned-root probes call it; lost admission never replays authority |
| `RetailGenerationStoreTest` | injected durability boundaries | every `RetailImportFailpoint` and Task7-owned `RetailPendingFailpoint` value has exactly one Task7 Hit site after its named durable step; FS, not Task8 after-return code, hits seal flush/auth, s->p rename/parent sync, p->t rename/parent sync, cleanup and t-remint boundaries; q rename/restart remints only after closed recovery authentication; confirmed authorization transfers Store's exact injector through the root session/cap chain without changing the Task 6 adapter constructor; all six live outer-reset boundaries include inner-lock unlink, while restart convergence does not reinject |
| `RetailGenerationStoreTest` | outer-reset transaction | exact outer-parent `.retail-import-reset.lock`, fixed sibling marker literal/86-byte NHRT V1 codec, and exhaustive marker/root/destination/partial-purge table; one outer OS lease spans every Open/reset step through fresh inner-lock creation; simultaneous contenders and kill after inner-lock unlink prove no sibling-inner-lock dependency; future/torn/checksum/identity/type/cross-volume fail closed; exactly-once old-root purge and outside sentinel |
| `RetailGenerationStoreTest` | exhaustive recovery table | every journal/pointer/candidate/pending-seal Cartesian input selects exactly one row; lost admission retires only nonactive proposed+journal state, returns pointed RepairRequired when present, preserves pointed Ready plus nonpointed repairable retry, maps no pointed authority to exact-seal Missing or no-seal Reset, and cannot loop after a kill plus two fresh starts; only Inconclusive is zero-mutation |
| `RetailImportRecoveryProcessTest` | real-process crash/restart | public synthetic driver reaches every failpoint family; publish/q/GC/lost-admission cases follow the exact table, reset cases reach fresh Missing, q leftovers safely reauthorize/remint/purge, cleanup and consumption t never cross-authorize, crash child exits nonzero, two fresh recovery children converge, and outside sentinel remains identical |
| `RetailImportRecoveryProcessTest` | non-friend harness provenance | strict CLI/root/enum, exact sibling regular-file paths, exact assembly identities, and independent SHA-256 binding for harness, OpenRA.Test, and RA2 DLLs |

- [ ] **Step 3: Run RED**

```bash
set -euo pipefail
TASK7_RED="$(mktemp -d /private/tmp/nuke-hour-task7-red.XXXXXX)"

set +e
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailGenerationStoreTest|FullyQualifiedName~RetailImportRecoveryProcessTest' \
  -v minimal > "$TASK7_RED/store.log" 2>&1
STORE_RED_RC=$?
set -e
test "$STORE_RED_RC" -ne 0
rg -n 'error CS(0117|0246|1061|1501)|does not exist in the current context' \
	"$TASK7_RED/store.log"

set +e
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  build/OpenRA.RetailImport.CrashHarness/OpenRA.RetailImport.CrashHarness.csproj \
  -c Debug -m:1 > "$TASK7_RED/harness.log" 2>&1
HARNESS_RED_RC=$?
set -e
test "$HARNESS_RED_RC" -ne 0
rg -n 'error CS(0117|0246|1061|1501)|does not exist in the current context' \
	"$TASK7_RED/harness.log"

TASK7_DEFERRED_SHA256=packaging/ios-retail-task7-deferred-engine-tests.sha256
test ! -e "$TASK7_DEFERRED_SHA256"
shasum -a 256 \
  engine/OpenRA.Test/RetailGenerationStoreTest.cs \
  engine/OpenRA.Test/RetailImportRecoveryProcessTest.cs | \
  LC_ALL=C sort -k2 > "$TASK7_DEFERRED_SHA256"
test "$(wc -l < "$TASK7_DEFERRED_SHA256" | tr -d ' ')" = 2
shasum -a 256 -c "$TASK7_DEFERRED_SHA256"
```

Expected: both invocations fail for the named missing Task 7 store/recovery contracts. Only after both genuine failures are retained does this step create and immediately verify the two-line Task 7 deferred-test SHA manifest. Those test bytes are frozen from this point; GREEN may verify but never regenerate the manifest. Task 8 tests do not exist yet and are not invoked here; the real child-process/non-skip proof belongs to Task 7 GREEN and is repeated in Task 17.

- [ ] **Step 4: Implement exact recovery decisions**

In this table, “valid” means the candidate callback returned the exact `Ready` tuple with its owned validation lease; “invalid” means the callback returned explicit `Invalid` after stable, complete validation proved content corruption. `RepairRequired` and `Inconclusive` never count as invalid, and a repairable result never authorizes a pointer switch. Outside lost-admission retirement, retaining an already pointed repairable generation returns its exact `RepairRequired` tuple without mutation. Inside lost-admission retirement, however, the stale nonactive proposed generation and journal must be retired before Task 14 can open a new repair publication transaction: the exact pointed generation and pointer are preserved, and completion returns that same authenticated `RepairRequired` snapshot/readiness/plan. A repairable nonpointed proposed generation is never activated; when the pointed generation is Ready, it and any exact pending seal remain available while only proposed/journal state is retired. Only an invoked necessary candidate's `Inconclusive`, callback exception, illegal tuple, or identity mismatch short-circuits to zero-mutation `RecoveryFailed`. Thus no non-Ready generation becomes active, pointed repair authority is not destroyed, and a transient failure cannot authorize retirement/reset/fallback.

Because the in-memory admission cannot survive a process death, the following priority subtable is evaluated before the main table whenever a `Publishing` or `Committed` journal names a complete nonactive proposed generation but the authenticated pointer does not target it. “Exact seal” means a canonical, complete `p-{nonce}` whose same non-null ID is bound by the journal/proposed receipt. Under the same live lock, Store first proves from the complete reference set that proposed is nonactive, mints the exact `RetailQuarantineAuthorization`, and durably retires proposed plus journal without touching the seal. A pointed Ready or RepairRequired generation is authority and is never quarantined by this branch. A stable Invalid pointer, its unreferenced pointed generation, or a structurally absent pointer supplies no pointed authority: with an exact seal the transaction removes/quarantines that invalid pointer state and returns existing `Missing`; without a seal it returns existing `ResetRequired`. Crash-left q entries require fresh Store reauthorization and session remint. `Inconclusive` never enters the table. This durable retirement is the convergence boundary and prevents a later repair publish from colliding with the dead journal or the same lost-admission decision from recurring.

| Pointer at lost-admission recovery | Pointed candidate | Nonactive proposed candidate | Exact same-nonce seal | Exact result after safe retirement |
| --- | --- | --- | --- | --- |
| previous | Ready | Ready or RepairRequired | either | `KeptPrevious`; retire proposed+journal, retain the exact Ready tuple, and leave the seal retryable when present |
| previous | RepairRequired | Ready or RepairRequired | either | `RepairRequired`; retire proposed/journal, preserve the pointer and exact authenticated Active+Readiness+Repair tuple, and leave the seal retryable when present |
| previous | Invalid/absent | Ready or RepairRequired | present | retire proposed/journal, quarantine the fixed invalid pointer and its unreferenced pointed generation, then existing `Missing`; Task 8 reopens/revalidates the exact seal before a fresh admission |
| previous | Invalid/absent | Ready or RepairRequired | absent | retire proposed/journal, then existing `ResetRequired`; UI is `ChooseAgain`, never activate proposed |
| absent | none | Ready or RepairRequired | present | retire proposed/journal, then existing `Missing`; first install returns to the exact sealed retry through Task 8 pending open |
| absent | none | Ready or RepairRequired | absent | retire proposed/journal, then existing `ResetRequired`; first install must Choose Again |
| other generation | independently Ready | Ready or RepairRequired | either | `ReadyWithCorruptJournalWarning`; retire proposed/journal, preserve that exact pointer/Ready tuple and seal, and never infer authority from journal data |
| other generation | RepairRequired | Ready or RepairRequired | either | `RepairRequired`; retire proposed/journal, preserve that exact pointer and repair tuple plus seal, and allow Task 14's separate repair publication |
| other generation | Invalid/absent | Ready or RepairRequired | present | retire proposed/journal, quarantine the fixed invalid pointer and its unreferenced pointed generation, then existing `Missing`; expose the seal only through Task 8 pending open |
| other generation | Invalid/absent | Ready or RepairRequired | absent | retire proposed/journal, then existing `ResetRequired`; UI is Choose Again |
| any pointed form | Inconclusive | any | either | `RecoveryFailed` with byte-identical pointer, journal, proposed, seal, and q state |

A forced kill at any authorization, q rename/sync, q remint/purge, or journal-retirement failpoint is followed by two fresh recovery launches in RED. The first successful launch must finish the same row: it removes only nonactive proposed/journal state, additionally removes invalid pointed state only for the seal-backed Missing/no-seal Reset rows, and never returns before crash-left q state is authenticated. The second launch must observe no stale journal: `NoJournal` plus the same Ready or RepairRequired pointer, an absent pointer plus the exact seal, or the clean reset state. It must perform no repeat retirement, cannot reuse the dead validation/admission, and must leave every preserved pointer, seal, marker, and outside sentinel byte-identical.

| Journal | Authenticated pointer target/epoch | Candidate validation outcomes | Exact result |
| --- | --- | --- | --- |
| absent | no pointer, first launch | none | `NoJournal`; importer remains Missing |
| absent | valid active | active valid | `NoJournal`; keep pointer and return its Ready tuple |
| absent | invalid pointer | any | `ResetRequired` |
| `Prepared` | previous | previous valid | `KeptPrevious`; proposed remains nonactive |
| `Prepared` | previous | previous invalid, proposed valid or invalid | `ResetRequired`; Prepared never authorizes a switch |
| `Prepared` | proposed/other | previous valid | `RestoredPrevious`; never confirm an unauthorized swap |
| `Prepared` | proposed/other | previous invalid | `ResetRequired` |
| `Publishing` | previous | proposed valid | use the priority lost-admission subtable; never advance the pointer |
| `Publishing` | previous/other | pointed Ready or RepairRequired, proposed RepairRequired | use the priority lost-admission subtable; retire only proposed+journal, preserve pointed authority and seal |
| `Publishing` | previous | previous invalid, proposed invalid | `ResetRequired` |
| `Publishing` | proposed at journal epoch | proposed valid | `ConfirmedProposed` |
| `Publishing` | proposed | proposed invalid, previous valid | `RestoredPrevious` |
| `Publishing` | proposed | proposed invalid, previous invalid/absent | `ResetRequired` |
| `Committed` | proposed at journal epoch | proposed valid | `ConfirmedProposed` |
| `Committed` | proposed at journal epoch | proposed invalid, previous valid | `RestoredPrevious` |
| `Committed` | proposed at journal epoch | proposed invalid, no valid previous | `ResetRequired` |
| `Committed` | previous | proposed valid | use the priority lost-admission subtable; never replay the consumed admission |
| `Committed` | previous/other | pointed Ready or RepairRequired, proposed RepairRequired | use the priority lost-admission subtable; never activate repairable proposed state |
| `Committed` | previous | proposed invalid, previous valid | `ReadyWithCorruptJournalWarning`; keep previous and quarantine inconsistent journal |
| `Committed` | previous | proposed invalid, previous invalid | `ResetRequired` |
| first install `Prepared` | pointer absent | proposed valid | `Missing`; proposed is nonactive and may be resumed/discarded |
| first install `Publishing` | pointer absent | proposed valid | use the priority lost-admission subtable |
| first install `Publishing` | proposed at journal epoch | proposed valid | `ConfirmedProposed` |
| first install `Committed` | proposed at journal epoch | proposed valid | `ConfirmedProposed` |
| first install `Committed` | pointer absent | proposed valid | use the priority lost-admission subtable |
| non-first-install `Publishing`/`Committed` | pointer absent/other | proposed valid | use the priority lost-admission subtable, including independently Ready other-pointer preservation |
| first install, any state | pointer/proposed invalid or no complete proposed | `ResetRequired` |
| any known state | pointer epoch/commit does not match journal and no table row proves one whole generation | any | `ResetRequired` |
| any known state/pointer combination not matched above | any | any | `ResetRequired`; recovery has no fallthrough/default guess |
| corrupt fixed journal | active valid | active valid | `ReadyWithCorruptJournalWarning`; quarantine fixed journal and return Ready |
| corrupt fixed journal | active invalid | none | `ResetRequired` |
| future version | any | any | `UpdateRequired` |

Outer reset has its own exhaustive transaction table, evaluated by the adapter's existing `Open` only while it owns the fixed outer-parent `.retail-import-reset.lock` lease and before it creates or trusts the importer root:

| Fixed outer marker | Fixed importer root | Marker-derived sibling | Exact convergence |
| --- | --- | --- | --- |
| absent | exact root exists | absent | normal Open |
| absent | root absent | absent | create/open a fresh Missing root |
| `Prepared` | exact recorded root exists | absent | atomically rename to the marker-derived sibling, fsync parent, durably write `Renamed`, purge, clear marker, then create/open fresh Missing root |
| `Prepared` | root absent | exact recorded sibling exists | rename already happened; durably write `Renamed`, purge, clear marker, then create/open fresh Missing root |
| `Prepared` | root absent | sibling absent | `RecoveryFailed`; Prepared cannot prove that rename and purge both completed, so mutate nothing |
| `Renamed` | root absent | exact recorded sibling exists, complete or partially purged (including missing inner `import.lock`) | reconstruct the one-shot root capability with the retained outer lease, finish purge, clear marker, create/open fresh Missing root, create/verify its new inner lock, then release outer lease |
| `Renamed` | root absent | sibling absent | purge already finished; durably clear marker, then create/open fresh Missing root |
| `Renamed` | recorded root exists | sibling absent | `RecoveryFailed`; a stale or recreated root is never treated as the quarantined inode |
| absent | any | an unjournaled matching reset sibling exists | fail closed and preserve it; never guess its nonce or identity |
| `Prepared` or `Renamed` | both root and sibling exist, or either identity/type/volume differs | any | fail closed with zero rename/purge/marker mutation |
| torn/checksum-invalid/illegal-state marker | any | any | `RecoveryFailed`; use no marker field as a path and mutate nothing |
| version greater than 1 | any | any | `UpdateRequired`; mutate nothing |

This outer table is also a multi-process linearization contract. Every row begins under the same fixed outer-parent lock, re-evaluates marker/root/sibling identities before mutation, and retains that lease through marker clear and creation/verification of the fresh root plus its new inner lock. Neither the recorded root's nor sibling's `import.lock` participates in reset serialization. In particular, a crash at `ResetInnerLockUnlinked` leaves a partial exact sibling and Renamed marker; the next lock owner reconstructs the capability from marker plus sibling inode and finishes. A contender performs zero mutation. No stale observation authorizes purge, marker clear, nonce choice, or replacement of a concurrently created root.

Never parse paths from corrupt bytes. An exhaustive Cartesian test over journal state/version, pointer target/epoch/commit, previous presence/validity, proposed presence/validity, first-install encoding, and presence/absence of the original one-shot space admission proves every input matches exactly one publish-recovery row; ambiguity or fallthrough is a test failure. `RetailPublishSpaceAdmission` is deliberately non-persistent: recovery may confirm a proposed generation only when the authenticated pointer already targets that exact generation/epoch/commit, never by moving a previous/absent/other pointer after a crash discarded the admission. Reset uses a separately confirmed one-shot authorization bound to the adapter's pinned outer parent, fixed importer leaf, and root inode. The adapter alone chooses one nonzero lowercase-`N` nonce and its immediate sibling `.retail-import-reset-q-{nonce}`. Before rename it atomically writes and parent-fsyncs the fixed sibling marker `.retail-import-reset.transaction`. Its V1 bytes are exactly 86 bytes: ASCII `NHRT`, byte version `1`, byte state `Prepared=1|Renamed=2`, 32 lowercase-ASCII nonce bytes, unsigned little-endian 64-bit root device and inode, then SHA-256 of the preceding 54 bytes. The destination is derived only from the authenticated nonce; no stored or caller path exists. The adapter rejects an existing/symlink/nonregular destination and every identity/cross-volume/self/descendant relation, follows the table through rename/parent sync/`Renamed` flush/purge/marker clear, invalidates the old store for every operation except the retained capability's purge/disposal, and permits a new store only after `Open` convergence. The fixed checksummed marker—not a lost in-memory nonce or lease—is the sole crash authority for recreating `RetailQuarantinedRoot`; invalid/future markers never authorize deletion. Optional-group corruption is repaired by preparing and publishing a new generation that omits only the invalid optional receipts; it never deletes or edits the active generation in place. A successful publish or repair retains the previous generation until a later cold-start full hash/contract/readiness validation succeeds and every old catalog/map/package lease is released; only then may lock-protected GC remove it.

The table is evaluated only after all candidates needed for its selected row have produced conclusive `Ready`, `RepairRequired`, or `Invalid` results. `RecoveryFailed` is a session-level early result, not another table guess: it leaves the pointer, journal, generations, quarantine, garbage-collection state, and pending-consumption state byte-identical, releases every snapshot/lease/publisher in reverse order, releases the process lock, and makes the session inert. RED tests independently cover timeout during every hash chunk, dispatcher rejection, callback throw, final handle-identity drift, wrong candidate identity, illegal tuple, and cancellation immediately before versus immediately after completion entry; none may reach an `invalid` cell or mutate recovery state.

- [ ] **Step 5: Run GREEN and commit**

Run the complete block, including the real process-kill harness, focused tests, RA2 Release build, deferred-test hash check, and diff check. Keep the two ignored engine tests present, byte-recorded, and unstaged. Commit only the eight production/build paths plus the tracked Task 7 SHA manifest:

```bash
set -euo pipefail
CRASH_HARNESS_PROJECT=build/OpenRA.RetailImport.CrashHarness/OpenRA.RetailImport.CrashHarness.csproj
TASK7_EVIDENCE="$(mktemp -d /private/tmp/nuke-hour-retail-task7.XXXXXX)"
TASK7_CRASH_ROOT="$TASK7_EVIDENCE/crash-root"
TASK7_TRX="$TASK7_EVIDENCE/task7.trx"
test ! -e "$TASK7_CRASH_ROOT"

env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet restore \
  "$CRASH_HARNESS_PROJECT" -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  "$CRASH_HARNESS_PROJECT" -c Debug --no-restore -m:1
CRASH_HARNESS_DLL="$(
  env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet msbuild \
    "$CRASH_HARNESS_PROJECT" \
    -getProperty:TargetPath -p:Configuration=Debug
)"
test -n "$CRASH_HARNESS_DLL"
test -f "$CRASH_HARNESS_DLL"
test ! -L "$CRASH_HARNESS_DLL"
case "$CRASH_HARNESS_DLL" in
  "$PWD"/*) ;;
  *) exit 1 ;;
esac
CRASH_HARNESS_SHA256="$(
  shasum -a 256 "$CRASH_HARNESS_DLL" | awk '{print $1}'
)"
test -n "$CRASH_HARNESS_SHA256"
CRASH_HARNESS_DIR="$(dirname "$CRASH_HARNESS_DLL")"
OPENRA_TEST_DLL="$CRASH_HARNESS_DIR/OpenRA.Test.dll"
RA2_DLL="$CRASH_HARNESS_DIR/OpenRA.Mods.RA2.dll"
test -f "$OPENRA_TEST_DLL"
test ! -L "$OPENRA_TEST_DLL"
test -f "$RA2_DLL"
test ! -L "$RA2_DLL"
OPENRA_TEST_SHA256="$(shasum -a 256 "$OPENRA_TEST_DLL" | awk '{print $1}')"
RA2_SHA256="$(shasum -a 256 "$RA2_DLL" | awk '{print $1}')"
test -n "$OPENRA_TEST_SHA256"
test -n "$RA2_SHA256"

env \
  DOTNET_ROLL_FORWARD=Major \
  RETAIL_IMPORT_PROCESS_KILL_REQUIRED=1 \
  RETAIL_IMPORT_CRASH_HARNESS_DLL="$CRASH_HARNESS_DLL" \
  RETAIL_IMPORT_CRASH_HARNESS_SHA256="$CRASH_HARNESS_SHA256" \
  RETAIL_IMPORT_OPENRA_TEST_DLL="$OPENRA_TEST_DLL" \
  RETAIL_IMPORT_OPENRA_TEST_SHA256="$OPENRA_TEST_SHA256" \
  RETAIL_IMPORT_RA2_DLL="$RA2_DLL" \
  RETAIL_IMPORT_RA2_SHA256="$RA2_SHA256" \
  RETAIL_IMPORT_CRASH_ROOT="$TASK7_CRASH_ROOT" \
	/Users/kevin/.dotnet/dotnet test \
	  engine/OpenRA.Test/OpenRA.Test.csproj --no-build --no-restore -m:1 \
    --filter 'FullyQualifiedName~RetailGenerationStoreTest|FullyQualifiedName~RetailImportRecoveryProcessTest' \
    --results-directory "$TASK7_EVIDENCE" \
    --logger 'trx;LogFileName=task7.trx' \
    -v minimal
test -f "$TASK7_TRX"
rg -F \
  'testName="EveryFailpointCrashesAndRecoversInARealChildProcess"' \
  "$TASK7_TRX"
! rg -n 'outcome="(NotExecuted|Skipped|Inconclusive)"' "$TASK7_TRX"

env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj \
  -c Release --no-restore -m:1
TASK6_DEFERRED_SHA256=packaging/ios-retail-task6-deferred-engine-tests.sha256
test -f "$TASK6_DEFERRED_SHA256"
shasum -a 256 -c "$TASK6_DEFERRED_SHA256"
TASK7_DEFERRED_SHA256=packaging/ios-retail-task7-deferred-engine-tests.sha256
test -f "$TASK7_DEFERRED_SHA256"
test "$(wc -l < "$TASK7_DEFERRED_SHA256" | tr -d ' ')" = 2
shasum -a 256 -c "$TASK7_DEFERRED_SHA256"
git diff --check
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 7 --phase implementation
git commit -m "Publish retail content with crash-safe generations"
```

`RetailImportRecoveryProcessTest` must enumerate all values from `Enum.GetValues<RetailImportFailpoint>()`; for every value it starts one real selected child that reaches the failpoint and exits nonzero through `Environment.FailFast`, then two distinct `recover` children. Generation publication, q, GC, and lost-admission cases must produce the exact row-selected Ready, pointed RepairRequired, proposed, independently Ready warning, Missing-with-sealed-retry, or Reset result; q leftovers authenticate and remint before one purge, and the second launch proves no repeat retirement. Outer-reset cases are asserted separately and converge to a fresh Missing root with the recorded old root purged exactly once, including a kill after the quarantined inner lock is unlinked. Every family keeps its outside sentinel byte-identical; no assertion describes reset as previous/proposed convergence. `RETAIL_IMPORT_PROCESS_KILL_REQUIRED=1` makes a missing harness, ignored/not-executed test, unobserved crash, missing enum value, or synthetic-root violation fail rather than skip. Before any scenario, the parent requires exact canonical immediate-sibling regular-file supply paths for `OpenRA.RetailImport.CrashHarness.dll`, `OpenRA.Test.dll`, and `OpenRA.Mods.RA2.dll`, rejects symlinks and wrong basenames/assembly names, and independently recomputes all three lowercase SHA-256 values supplied through `RETAIL_IMPORT_CRASH_HARNESS_{DLL,SHA256}`, `RETAIL_IMPORT_OPENRA_TEST_{DLL,SHA256}`, and `RETAIL_IMPORT_RA2_{DLL,SHA256}`. The child repeats those file/SHA checks and additionally requires the loaded harness `Assembly.Location`, `RetailImportCrashHarnessDriver` assembly location, and a fixed public RA2 nominal type's assembly location to respectively equal the three supplied paths. A missing, relocated, duplicated, stale, or byte-different dependency fails before synthetic-root creation. The Task 7 build accepts only `crash|recover`, fixed test-owned synthetic root, and exact enum spelling for crash; it rejects extra arguments and retail bytes/paths. Frozen behavioral tests do not assert future pending verbs remain invalid, because Task 8 later extends Program without modifying Task 7 tests. The two engine tests join `0027`.

---

### Task 8: Add sealed pending imports, space planning, legacy migration, and orchestration

**Production files committed in this task:**

- Create: `OpenRA.Mods.RA2/Content/RetailPendingSnapshotStore.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailLegacyMigration.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportSpacePlanner.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportCoordinatorCore.cs`
- Modify: `OpenRA.Mods.RA2/Content/RetailContentImporter.cs`
- Modify again: `build/OpenRA.RetailImport.CrashHarness/Program.cs`
- Create: `packaging/ios-retail-task8-deferred-engine-tests.sha256`

**Deferred ignored engine tests (created and run now, first force-added in Task 14):**

- Create: `engine/OpenRA.Test/RetailPendingSnapshotStoreTest.cs`
- Create: `engine/OpenRA.Test/RetailLegacyMigrationTest.cs`
- Create: `engine/OpenRA.Test/RetailContentImporterTransactionTest.cs`

- [ ] **Step 1: Write RED pending/space/migration tests**

Require a complete snapshot authenticated by Task 6's exact `RetailPendingSeal` artifact/hash document, 8 GiB cap, seven-day derived expiry, and no continued security-scope dependency. Persist only the canonical UTC seal time through `IRetailPersistentTimeProvider`; monotonic timestamps and expiry are never serialized. Every artifact is first materialized and reverified under same-nonce `s-*/content`. The closed Task 7 finalizer then writes/fsyncs the canonical seal inside that `s-*`, reopens/decodes/verifies the entire seal-to-artifact set, and returns the sole complete-staging capability; its atomic session bridge alone may rename `s -> p` and fsync the parent. Thus a kill before seal flush leaves only nonauthoritative `s-*`, while a kill at/after rename can expose only a complete `p-*` with its seal already present. Every visible `p-{nonce}` binds non-null `SourcePendingId=p-{same nonce}` through source/wrapper/journal/receipt. Tests recreate the store/provider to simulate process and device restart and kill before/after every Task 7-owned artifact/seal/auth/rename/parent-sync failpoint; no test can observe unsealed `p-*`.

They also cover exact seven days, one tick below/above, future timestamps, and clock rollback. Suspicious time fails closed and preserves the active pointer. Partial `s-*` is never retryable. Startup resolves publish recovery before pending/orphan cleanup. Cleanup holds the single import lock but cannot pass an ordinary entry to a deletion API: Store must mint a fresh `RetailPendingCleanupAuthorization` for the exact inode/nonce/reason after recomputing the complete pointer/journal/receipt/seal/pin/reference set. Incomplete s and malformed, invalid, expired, or orphan unreferenced p move only through the cleanup session to same-nonce t, then purge only through the cleanup tombstone session. A crash after rename requires fresh full-reference-set reauthorization of that t. It cannot be reclassified as a consumption tombstone, and a valid journal/receipt/marker-referenced item is never authorized. A second coordinator cannot clean another live transaction, and outside sentinels remain byte-identical. Pending validation failure never changes the active pointer; if active remains Ready, result capabilities include Enter/Retry/Details/Discard.

Space planning outside the Store is advisory only and returns no admission census. The importer passes the exact validated candidate or repair wrapper and live lease directly to the matching `CreateSpaceAdmission` overload. Store remeasures the wrapper-bound sealed source, other pending data, rollback generation, proposed payload/conversion plan, and metadata plan. Already allocated retained-on-disk bytes are recorded for drift detection but never added again to free-space demand; initial free space is compared only with peak incremental bytes from this post-staging checkpoint plus the exact 512 MiB margin. Store meters its own subsequent writes and the pointer-time recheck compares current free space only with still-unwritten incremental peak plus that margin. Tests cover each retained and incremental component under-reported by one, equal numeric estimates from another wrapper, unknown/negative lengths/capacity, checked overflow, actual Store writes lowering free space, and independent initial/recheck `-1`/exact/`+1` boundaries without changing active state on failure.

Legacy candidates are only the fixed old `Documents/OpenRA/Content/ra2` and resolved versioned old map directory. They are copied through the same finalized `s+seal -> private session -> p` pipeline, never mounted/moved/deleted directly, and successful migration records a durable marker. Task 8 extends Task 7's already-compiling `RetailPendingMarkerCodec` partial type with the sole canonical `DecodePinned`; only this same type can invoke the private constructor of its nested `RetailDecodedPendingMarkerFile`. The existing `RetailPendingConsumptionMarker` remains a six-field plain codec DTO and grants no mutation authority. Task 7 has zero decoded-proof construction sites and Task 8 has exactly one; runtime rejects forged/reflection/null, replaced-file drift, wrong token/root/lease/schema/digest, replay, alias, second consumption, and use after disposal.

For consumption, neither the decoded proof nor a bare lease reaches FS. Task 8 can create only a nonauthorizing `RetailPendingConsumptionAuthorization.StructuralBinding` after Store has reauthenticated the exact Ready/current pointer/receipt/entry/reference tuple. Task 14 later supplies the only final pair mint. A restart with consumption t opens/decodes one marker, but only a fresh final pair authorization may atomically consume that proof into the private tombstone-recovery session, remint the consumption-only purge cap, purge once, then reopen/redecode the still-fixed marker and use a second fresh pair authorization to produce the private marker-authentication session. The returned authenticated-marker cap must itself create the one-shot clear session. Direct `OpenPinnedRead -> FS`, DTO -> FS, decoded proof -> FS, ordinary entry -> t purge, cleanup cap -> consumption, and Ready without the matching verified pair do not compile. Legacy-marker clear uses the separately Store-minted `RetailLegacyMarkerClearAuthorization`, bound to the fixed marker inode, completed-migration state and complete reference set; it atomically combines with the decoded proof into the same private marker-authentication session and then uses the cap/session clear rule. It is not a loophole into consumption authority.

- [ ] **Step 2: Run RED and freeze the deferred tests**

```bash
set -euo pipefail
TASK8_RED="$(mktemp -d /private/tmp/nuke-hour-task8-red.XXXXXX)"
set +e
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailPendingSnapshotStoreTest|FullyQualifiedName~RetailLegacyMigrationTest|FullyQualifiedName~RetailContentImporterTransactionTest' \
  -v minimal > "$TASK8_RED/task8.log" 2>&1
TASK8_RED_RC=$?
set -e
test "$TASK8_RED_RC" -ne 0
rg -n 'error CS(0117|0246|1061|1501)|does not exist in the current context' \
	"$TASK8_RED/task8.log"

TASK8_DEFERRED_SHA256=packaging/ios-retail-task8-deferred-engine-tests.sha256
test ! -e "$TASK8_DEFERRED_SHA256"
shasum -a 256 \
  engine/OpenRA.Test/RetailPendingSnapshotStoreTest.cs \
  engine/OpenRA.Test/RetailLegacyMigrationTest.cs \
  engine/OpenRA.Test/RetailContentImporterTransactionTest.cs | \
  LC_ALL=C sort -k2 > "$TASK8_DEFERRED_SHA256"
test "$(wc -l < "$TASK8_DEFERRED_SHA256" | tr -d ' ')" = 3
shasum -a 256 -c "$TASK8_DEFERRED_SHA256"
```

Expected: the invocation fails only for named missing pending/space/migration contracts and the old importer `.staging/current/.previous` behavior changing a partial set. Only after that genuine RED is retained does this step create and immediately verify the three-line Task 8 deferred-test SHA manifest. Those bytes are frozen from this point; GREEN may verify but never regenerate the manifest.

- [ ] **Step 3: Replace the old importer orchestration**

```csharp
public enum RetailImportMode
{
	ActivateBeforeGame,
	SealForNextLaunch,
}

public sealed class RetailPendingSnapshot : IAsyncDisposable
{
	public string PendingId { get; }
	public DateTimeOffset SealedAtUtc { get; }
	public DateTimeOffset ExpiresAtUtc { get; }
	public ImmutableArray<RetailPendingArtifact> Artifacts { get; }
	internal RetailPendingSnapshot(
		RetailPendingSeal authenticatedSeal,
		RetailPendingRootCapability rootCapability);
	internal RetailStagedPlan CreateSanitizedStagedPlan();
	public ValueTask DisposeAsync();
}

internal sealed class RetailPendingRootCapability { }

public enum RetailPendingConsumptionState
{
	Prepared,
	Consumed,
}

public sealed class RetailPendingConsumptionMarker
{
	internal RetailPendingConsumptionMarker(
		int schemaVersion,
		RetailPendingConsumptionState state,
		string pendingId,
		string generationId,
		long epoch,
		string commitId);
	public int SchemaVersion { get; }
	public RetailPendingConsumptionState State { get; }
	public string PendingId { get; }
	public string GenerationId { get; }
	public long Epoch { get; }
	public string CommitId { get; }
}

// Task 8 adds this implementation to Task 7's already compiled partial type.
internal sealed partial class RetailPendingMarkerCodec
{
	internal enum MarkerDocumentKind
	{
		Consumption,
		LegacyMigration,
	}

	internal sealed class DecodeResult : IDisposable
	{
		private DecodeResult(
			MarkerDocumentKind kind,
			RetailPendingConsumptionMarker optionalConsumptionDto,
			RetailDecodedPendingMarkerFile ownedDecodedFile);
		internal MarkerDocumentKind Kind { get; }
		internal RetailPendingConsumptionMarker ConsumptionDto { get; }
		internal RetailDecodedPendingMarkerFile TakeDecodedFile();
		public void Dispose();
	}

	internal static RetailOperationResult<DecodeResult> DecodePinned(
		RetailImportPaths.V1PathToken exactMarker,
		RetailImportPinnedFile ownedPinnedFile);
}

// RetailPendingFailpoint and RetailPendingFaultInjection are already owned by
// Task 7's FaultInjection/FS files. Task 8 constructs one dispatcher only.
// It adds structural binding but has no final-authorization constructor call.
internal sealed partial class RetailPendingConsumptionAuthorization
{
	internal static RetailOperationResult<StructuralBinding> BindStructure(
		RetailGenerationStore store,
		RetailImportProcessLockLease liveLease,
		RetailReadyGenerationCapability exactReady,
		RetailImportDirectoryEntry exactEntry,
		RetailPendingConsumptionTransitionKind transitionKind);
}

// Defined by the SHA-frozen deferred OpenRA.Test sources. The non-friend
// harness invokes only this public test-only driver.
public static class RetailPendingCrashHarnessDriver
{
	public static int Crash(
		string fixedSyntheticRoot,
		string exactFailpointName);
	public static int Recover(string fixedSyntheticRoot);
}

public sealed class RetailPendingSnapshotStore
{
	public Task<RetailOperationResult> ReconcileConsumedAsync(
		RetailReadyGenerationCapability activeReady,
		VerifiedRa2RuntimeContract verifiedContract);
	public Task<RetailOperationResult> ConsumePublishedAsync(
		RetailReadyGenerationCapability publishedReady,
		VerifiedRa2RuntimeContract verifiedContract);
}

public interface IRetailCandidateValidator
{
	Task<RetailOperationResult<RetailValidatedCandidateInput>> ValidateAsync(
		RetailStagedPlan staged,
		RetailCandidateArtifactSource artifactSource,
		RetailActiveGenerationSnapshot preservedActive,
		RetailImportOperationIdentity borrowedOperationIdentity,
		RetailValidationDeadline deadline,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken,
		IProgress<RetailImportProgress> progress);

	Task<RetailOperationResult<RetailValidatedRepairInput>> ValidateRepairAsync(
		RetailActiveGenerationSnapshot active,
		RetailCatalogRepairPlan repair,
		RetailImportOperationIdentity borrowedOperationIdentity,
		RetailValidationDeadline deadline,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken,
		IProgress<RetailImportProgress> progress);
}

public enum RetailPendingOpenDisposition
{
	Missing,
	Ready,
	Expired,
	Invalid,
	UpdateRequired,
}

public sealed class RetailPendingOpenResult : IAsyncDisposable
{
	RetailPendingSnapshot ownedSnapshot;

	private RetailPendingOpenResult(
		RetailPendingOpenDisposition disposition,
		RetailPendingSnapshot ownedSnapshot,
		RetailImportFailure failure);
	public RetailPendingOpenDisposition Disposition { get; }
	public RetailImportFailure Failure { get; }
	public bool HasSnapshot { get; }
	internal static RetailPendingOpenResult Missing();
	internal static ValueTask<RetailPendingOpenResult> ReadyAsync(
		RetailPendingSnapshot ownedSnapshot);
	internal static RetailPendingOpenResult Failed(
		RetailPendingOpenDisposition disposition,
		RetailImportFailure failure);
	public RetailPendingSnapshot TakeSnapshot();
	public ValueTask DisposeAsync();
}

public Task<RetailGenerationImportResult> ImportAsync(
	IRetailImportSourceSession sources,
	RetailImportMode mode,
	CancellationToken cancellationToken,
	IProgress<RetailImportProgress> progress);

public Task<RetailPendingOpenResult> OpenPendingAsync(
	CancellationToken cancellationToken);

public Task<RetailGenerationImportResult> ResumePendingAsync(
	string pendingId,
	CancellationToken cancellationToken,
	IProgress<RetailImportProgress> progress);

public Task<RetailOperationResult> DiscardPendingAsync(
	string pendingId,
	CancellationToken cancellationToken);
```

`RetailContentImporter` receives the validator seam, generation store, the exact `RetailImportProcessLock` owned by that store, pending store, advisory storage planner, injected monotonic clock, and persistent UTC provider. Each import/resume/repair operation consumes the exact typed `TryAcquire` result before provider access, staging, orphan cleanup, or pending mutation: Busy returns the closed busy outcome without queued waiting or such I/O; Failed returns its sanitized failure; only Acquired yields a one-shot lease. The importer immediately borrows that lease's opaque `RetailImportOperationIdentity`; it never constructs, copies, or substitutes one. It owns the lease until `PrepareAsync`/`PrepareRepairAsync` takes it at call entry, and disposes it on every branch that stops before preparation. It copies and rehashes every artifact through Task 7's reserved same-nonce `pending/items/s-*/content/*` tokens, asks the closed finalizer to canonically write/fsync/reopen/verify `s-*/seal.bin`, and then consumes the resulting complete-staging capability for the sole atomic `s -> p` rename and parent sync. Only that complete `p-*` produces one transaction-bound `RetailCandidateArtifactSource`, whose non-null `SourcePendingId` is the same `p-{nonce}` carried through validation, Prepare, journal, and receipt. The importer owns that sealed capability across validation and `PrepareAsync`, passes the exact same instance to the validator, and disposes it after publish/failure/cancellation. No pending document or snapshot retains the original `RetailCandidatePlan`, `RetailSourceId`, provider path, or security-scoped URL. On resume, `RetailPendingSnapshot.CreateSanitizedStagedPlan` derives root-index-zero IDs with relative paths `sealed/{ordinal:D4}/{SafeBasename}` from the authenticated ordinal artifact rows, feeds those exact rows through `RetailStagedPlan.Seal`, and treats the synthetic IDs only as an in-memory compatibility projection. No component reconstructs a source capability from a root, relative-path DTO, interface wrapper, or open delegate.

`ActivateBeforeGame` reads the injected clock once to create one exact 90-minute whole-operation `RetailValidationDeadline`, and passes that value and the identical `IRetailMonotonicClock` reference through validator, advisory planning, Prepare, Publish, post-switch validation, rollback/convergence, repair, and every chunked read/hash/materialization call. It invokes `ValidateAsync(staged, sealedSource, preservedActive, borrowedOperationIdentity, deadline, clock, cancellation, progress)` in that order and receives one exact `RetailValidatedCandidateInput` inseparably binding source, optional active, pending ID, and the lease-owned identity. The importer then invokes `generationStore.CreateSpaceAdmission(validatedInput, lease)`; the Store remeasures that exact wrapper's retained-on-disk and peak-incremental plan, probes its own pinned root, and privately mints the already-bound admission. It transfers only the wrapper followed by that same OS-lock lease, deadline, clock, and admission into `PrepareAsync`; it never receives or projects a separable `.Validation`, `.Catalog`, `.Readiness`, source, pending ID, active snapshot, census, admission scalar, root, or capacity. A successful Prepare returns the sole prepared owner. The importer calls `PublishAsync(prepared, callback)` with no lease/deadline/clock/admission substitutes; the store's first action consumes Prepared for its exact store/root and then passes its retained deadline and identical clock reference plus `CancellationToken.None` to the callback. Repair follows the same chain: `ValidateRepairAsync(active, plan, borrowedOperationIdentity, deadline, clock, cancellation, progress)` returns one `RetailValidatedRepairInput`, `CreateSpaceAdmission(validatedRepairInput, lease)` remeasures and binds that exact wrapper, and `PrepareRepairAsync` accepts only that wrapper followed by lease, deadline, clock, admission, cancellation, and progress. Tests use `ReferenceEquals` sentinels and exact deadline values at every edge, assert source/pending/active/plan/operation/validator/Prepare/Publish/admission substitution and cross-store/root Prepared use are rejected before mutation, and prove every wrapper, lease, admission, and prepared owner is disposed exactly once.

The full validator composes optional-only replacements with verified preserved Base/maps instead of forcing reselection or trusting raw active paths. It runs the injected archive/profile/map validation, and orchestration consumes only successful typed validation and prepare results before publishing the complete candidate; any typed failure is copied into `RetailGenerationImportResult` without raw exception text. `ValidateRepairAsync` applies a closed exclusion plan to that authenticated active snapshot and returns a typed, fully revalidated generation catalog/readiness with a recomputed profile and contract; the store never invents those semantics. `SealForNextLaunch` stops after bounded discovery, coordinated copy, envelope validation, and durable pending seal; it never converts maps, opens pre-game `ModData`, publishes, or changes active profile. `OpenPendingAsync` takes the import lock, authenticates the fixed seal, derives expiry, and returns a typed disposition plus an opaque snapshot without reopening a provider. `ResumePendingAsync` verifies the requested ID against that snapshot, acquires its sealed source capability whose inseparable internal identity already carries the same ID, rehashes it, and runs full validation/activation without an external source session. `DiscardPendingAsync` removes only that authenticated pending generation after the same lock/recovery ordering and leaves active bytes/pointer unchanged. Torn/expired/mismatched IDs return typed failures. Tests prove source lifetime/identity across validate→prepare, dispose on every branch, rejection of wrapper/subclass/delegate substitution, use-after-dispose rejection, absence of original source identities after restart, deterministic sanitized-plan reconstruction, optional Yuri/music/map-only replacement preserving Base, pending resume after restart, expiry, retry, discard, prior Ready survival, Yuri-to-Base repair, and single-map repair. Task 10/14 wire the real validator composition; Task 8 tests orchestration with a deterministic bounded fake and may not contain a temporary trust callback. The characterized legacy desktop overload and its distinct `RetailImportResult` remain until Task 14 atomically removes them with `RetailContentImporterTest` migration.

Disabled nullable annotations do not change ownership semantics: `activeReady` is runtime null only when startup has no authenticated Ready capability, and `preservedActive` is null only on first import/no-active input. `RetailPendingOpenResult` has a private/factory-validated closed tuple: `Missing => no owned snapshot/null Failure`; `Ready => one owned snapshot/null Failure`; and `Expired|Invalid|UpdateRequired => no owned snapshot/non-null Failure`. It exposes no snapshot getter. `ReadyAsync` takes ownership at call entry; if tuple validation or construction fails after that point it awaits `ownedSnapshot.DisposeAsync()` before propagating. `TakeSnapshot` is legal exactly once and only for Ready, atomically clears result ownership, and transfers the snapshot to the caller. `DisposeAsync` awaits an untaken snapshot, is idempotent, and is a no-op for Missing/failed states or after a successful take. `OpenPendingAsync` callers use `await using` around the result and explicitly take before retaining. No empty pending snapshot, failure-with-snapshot, missing-with-failure, repeated take, blocking cleanup, or discarded async cleanup is constructible; tests cover all five dispositions and every ownership branch.

Pending consumption is a second, closed crash-consistent state machine and is never an eager directory delete. A resumed publish first commits the generation whose authenticated receipt/journal contains the exact `SourcePendingId`, but `RetailContentImporter` never invokes either consumption method after publish and has no production call edge to them. Publish success remains `Published` while the sealed pending bytes stay retryable and the receipt durably binds their ID. Task 8 implements canonical marker codecs, complete structural reauthentication, and the session-only adapter state machine, but it cannot mint `RetailPendingConsumptionAuthorization`: its partial contribution may construct only `StructuralBinding`, and every final-authority constructor/call-edge gate is zero in Task 8. Until Task 14 supplies the final pair edge, `ConsumePublishedAsync`/`ReconcileConsumedAsync` stop after structural validation with a typed non-mutating authorization-unavailable result; staging, retry, discard-through-cleanup, and prior Ready behavior remain complete.

Task 8's authorized core accepts only a final `RetailPendingConsumptionAuthorization`, never a Ready, verified wrapper, string, ordinary snapshot, entry, or decoded DTO. The p transition consumes that authorization into a private session that writes/fsyncs the exact Prepared marker, atomically renames p to same-root t, and fsyncs the parent inside Task 7 FS. Its returned consumption-tombstone cap must produce a private purge/commit session before t deletion and durable `Consumed`. Crash-left t recovery, Prepared-with-both-p-and-t-missing commit recovery, and either marker clear each require a newly minted final authorization plus separately decoded proof; marker clear then consumes the returned authenticated-marker cap into another private session. No operation reuses the p-transition authority. Legacy migration uses its separate structural authorization and the same decoded-proof-to-cap-to-session discipline. Once final pair-authorized cleanup crosses its boundary, importer success remains `Published` even if cleanup must finish after restart; `OpenPendingAsync` must not re-expose a pending ID already bound to the authenticated active receipt.

Final startup ordering is exact: acquire the import lock; converge generation publish recovery, including Store-authorized remint/purge of fixed-enumerated q and full-reference-set cleanup-t recovery; authenticate/open and fully validate the resulting active generation; finish the one final static-resource preflight; in Task 14 require `ReferenceEquals(verifiedContract.Binding, ready.ValidationIdentity)`; mint one final authorization from a same-Ready Task 8 structural binding; and only then enter `ReconcileConsumedAsync` or `ConsumePublishedAsync`. If the receipt binds a `SourcePendingId`, the exact pair is sufficient to create or resume its consumption marker even when the process died before the first marker write. A marker may roll forward only when pending ID, generation ID, epoch, commit ID, profile, contract, selection, Ready reference, validation identity, pointer/receipt inode, and complete reference set all match. With no final pair authority or no exact current match, the sealed pending remains retryable and no marker byte or t name authorizes deletion. Recovery is idempotent across missing original/tombstone/marker states, rejects future/torn/checksum-invalid markers without using their values as paths, and returns `OpenPendingAsync=Missing` after committed consumption.

The child-crash matrix is phase-correct. Task 8's selected fixture runs every preauthorization/staging/cleanup failpoint and proves Task 7 owns each Hit site; a second fixture, already SHA-frozen in the same source but deliberately named outside Task 8's filter, discovers the final mint by reflection and is first executed in Task 14. That final fixture enumerates every pair-required marker/p->t/t-remint/clear value and every q/lost-admission boundary, requires a real non-skipped FailFast child plus two fresh recovery PIDs, and rejects any structural-only or cross-pair mutation. Every committed, Ready, statically verified case converges to one Ready generation plus Missing pending; every uncommitted, non-Ready, preflight-failed, mismatched-wrapper, unauthenticated, cleanup/consumption-crossed, or malformed-leftover case preserves the exact retryable seal/entry and previous Ready generation.

- [ ] **Step 4: Run GREEN and commit**

Run focused tests, rebuild the crash harness, run every pending child-process kill case without skips, run the RA2 Release build, and require `git diff --check`. Task 8's `Program.cs` preserves Task 7's exact `crash`/`recover` verbs unchanged and adds only `pending-crash <exact-root> <exact-enum-name>` and `pending-recover <exact-root>`; each pending verb invokes the public SHA-frozen `RetailPendingCrashHarnessDriver`, uses a separate synthetic-root namespace, and never accepts retail bytes or an arbitrary retail path. Keep all three ignored engine tests present, byte-recorded, and unstaged. Commit only the six listed production/build paths plus the tracked Task 8 SHA manifest:

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet restore \
  build/OpenRA.RetailImport.CrashHarness/OpenRA.RetailImport.CrashHarness.csproj -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  build/OpenRA.RetailImport.CrashHarness/OpenRA.RetailImport.CrashHarness.csproj \
  -c Debug --no-restore -m:1
CRASH_HARNESS_DLL="$(
  env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet msbuild \
    build/OpenRA.RetailImport.CrashHarness/OpenRA.RetailImport.CrashHarness.csproj \
    -getProperty:TargetPath -p:Configuration=Debug
)"
CRASH_HARNESS_DIR="$(dirname "$CRASH_HARNESS_DLL")"
OPENRA_TEST_DLL="$CRASH_HARNESS_DIR/OpenRA.Test.dll"
RA2_DLL="$CRASH_HARNESS_DIR/OpenRA.Mods.RA2.dll"
test -f "$CRASH_HARNESS_DLL"
test ! -L "$CRASH_HARNESS_DLL"
test -f "$OPENRA_TEST_DLL"
test ! -L "$OPENRA_TEST_DLL"
test -f "$RA2_DLL"
test ! -L "$RA2_DLL"
CRASH_HARNESS_SHA256="$(shasum -a 256 "$CRASH_HARNESS_DLL" | awk '{print $1}')"
OPENRA_TEST_SHA256="$(shasum -a 256 "$OPENRA_TEST_DLL" | awk '{print $1}')"
RA2_SHA256="$(shasum -a 256 "$RA2_DLL" | awk '{print $1}')"
TASK8_EVIDENCE="$(mktemp -d /private/tmp/nuke-hour-retail-task8.XXXXXX)"
TASK8_TRX="$TASK8_EVIDENCE/task8.trx"
env DOTNET_ROLL_FORWARD=Major \
  RETAIL_IMPORT_PROCESS_KILL_REQUIRED=1 \
  RETAIL_IMPORT_PENDING_PROCESS_KILL_REQUIRED=1 \
  RETAIL_IMPORT_CRASH_HARNESS_DLL="$CRASH_HARNESS_DLL" \
  RETAIL_IMPORT_CRASH_HARNESS_SHA256="$CRASH_HARNESS_SHA256" \
  RETAIL_IMPORT_OPENRA_TEST_DLL="$OPENRA_TEST_DLL" \
  RETAIL_IMPORT_OPENRA_TEST_SHA256="$OPENRA_TEST_SHA256" \
  RETAIL_IMPORT_RA2_DLL="$RA2_DLL" \
  RETAIL_IMPORT_RA2_SHA256="$RA2_SHA256" \
  RETAIL_IMPORT_CRASH_ROOT="$TASK8_EVIDENCE/crash-root" \
  /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj \
  -c Debug --no-build --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailPendingSnapshotStoreTest|FullyQualifiedName~RetailLegacyMigrationTest|FullyQualifiedName~RetailContentImporterTransactionTest|FullyQualifiedName~RetailImportRecoveryProcessTest' \
  --results-directory "$TASK8_EVIDENCE" \
  --logger 'trx;LogFileName=task8.trx' \
  -v minimal
test -f "$TASK8_TRX"
rg -F \
	'testName="EveryPreauthorizationPendingFailpointCrashesAndRecoversInARealChildProcess"' \
  "$TASK8_TRX"
! rg -n 'outcome="(NotExecuted|Skipped|Inconclusive)"' "$TASK8_TRX"
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore -m:1
TASK6_DEFERRED_SHA256=packaging/ios-retail-task6-deferred-engine-tests.sha256
TASK7_DEFERRED_SHA256=packaging/ios-retail-task7-deferred-engine-tests.sha256
test -f "$TASK6_DEFERRED_SHA256"
test -f "$TASK7_DEFERRED_SHA256"
shasum -a 256 -c "$TASK6_DEFERRED_SHA256"
shasum -a 256 -c "$TASK7_DEFERRED_SHA256"
TASK8_DEFERRED_SHA256=packaging/ios-retail-task8-deferred-engine-tests.sha256
test -f "$TASK8_DEFERRED_SHA256"
test "$(wc -l < "$TASK8_DEFERRED_SHA256" | tr -d ' ')" = 3
shasum -a 256 -c "$TASK8_DEFERRED_SHA256"
git diff --check
```

`RetailPendingSnapshotStoreTest.EveryPreauthorizationPendingFailpointCrashesAndRecoversInARealChildProcess` requires one non-skipped FailFast plus two-launch convergence case for the exact Task 8-reachable subset: staging artifact/seal/auth/rename/parent-sync, Ready-cap creation, and cleanup authorization/rename/parent-sync/remint/purge. It also proves every selected Hit originates in Task 7 FS/FaultInjection source. The same SHA-frozen file declares `RetailPendingFinalAuthorizationProcessTest.EveryPairRequiredPendingFailpointCrashesAndReconcilesInARealChildProcess`; Task 8 does not select it, and Task 14 must run it after the unique final pair mint exists. That fixture enumerates the complement and fails on any missing/duplicate enum member, so the two sets are disjoint and their union is exactly `Enum.GetValues<RetailPendingFailpoint>()`. `RETAIL_IMPORT_PROCESS_KILL_REQUIRED=1` and `RETAIL_IMPORT_PENDING_PROCESS_KILL_REQUIRED=1` turn a missing harness, ignored/not-executed case, unobserved exit, structural-only authority, or missing enum value into failure. Both use only the `RETAIL_IMPORT_*` contract and require the same canonical sibling-path, assembly-identity, and independent three-DLL SHA checks as Task 7 before creating a synthetic root.

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 8 --phase implementation
git commit -m "Add retryable pending retail imports"
```

The three engine tests join `0027`; because none is tracked before Task 14, strict `0023`...`0026` workspaces remain byte-equal outside their own allowlists.

---

### Task 9: Refactor RA2 map conversion into a bounded service and deliver `0023`

**Outer files:**

- Modify: `OpenRA.Mods.RA2/Content/Maps/Ra2MapImportModels.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/Ra2MapConversionService.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/Ra2Gen2Dialect.cs`
- Modify: `OpenRA.Mods.RA2/UtilityCommands/ImportRA2MapCommand.cs`

**Exact `0023` engine allowlist:**

- Modify: `engine/OpenRA.Game/Map/Map.cs`
- Modify: `engine/OpenRA.Game/Map/MapPlayers.cs`
- Modify: `engine/OpenRA.Game/Map/PlayerReference.cs`
- Create: `engine/OpenRA.Mods.Cnc/MapImport/Gen2MapImportModels.cs`
- Create: `engine/OpenRA.Mods.Cnc/MapImport/Gen2MapImporter.cs`
- Modify: `engine/OpenRA.Mods.Cnc/UtilityCommands/ImportGen2MapCommand.cs`
- Modify: `engine/OpenRA.Mods.Cnc/UtilityCommands/ImportTiberianSunMapCommand.cs`
- Create: `engine/OpenRA.Test/Gen2MapImporterTest.cs`
- Create: `engine/OpenRA.Test/Ra2MapConversionServiceTest.cs`

**Delivery files:**

- Create: `engine-patches/0023-ra2-map-conversion-service.patch`
- Modify: `engine-patches/README.md`
- Modify: `packaging/tests/test_verify_engine_patch_delivery.py`

- [ ] **Step 1: Write RED converter tests**

Lock the engine-facing model before refactoring either CLI:

```csharp
public enum Gen2MapImportError
{
	None,
	SourceTooLarge,
	SourceLengthMismatch,
	SourceHashMismatch,
	NotMultiplayer,
	InvalidDimensions,
	InvalidCompressedBlock,
	UnsupportedTheater,
	UnsupportedTile,
	UnsupportedActor,
	UnsupportedOverlay,
	InvalidSpawn,
	TooManyActors,
	TimedOut,
	ArithmeticOverflow,
}

public sealed record Gen2MapImportDiagnostic(
	Gen2MapImportError Error,
	int RecordIndex,
	int X,
	int Y);

public sealed class Gen2MapImportResult : IDisposable
{
	private Gen2MapImportResult(
		bool succeeded,
		Map map,
		Gen2MapImportError error,
		ImmutableArray<Gen2MapImportDiagnostic> diagnostics);
	public bool Succeeded { get; }
	public Gen2MapImportError Error { get; }
	public ImmutableArray<Gen2MapImportDiagnostic> Diagnostics { get; }
	internal static Gen2MapImportResult Success(
		Map map,
		ImmutableArray<Gen2MapImportDiagnostic> diagnostics);
	internal static Gen2MapImportResult Failed(
		Gen2MapImportError error,
		ImmutableArray<Gen2MapImportDiagnostic> diagnostics);
	public Map TakeMap();
	public void Dispose();
}

public sealed record Gen2MapImportLimits(
	long MaximumSourceBytes,
	int MaximumWidth,
	int MaximumHeight,
	int MaximumCells,
	int MaximumActors,
	int MaximumSpawns);

public sealed record Gen2MapCordon(
	int Left,
	int Top,
	int Right,
	int Bottom,
	int UninteractableHorizontal,
	int UninteractableTop);

public interface IGen2MapSource
{
	long Length { get; }
	string Sha256 { get; }
	Stream OpenVerifiedRead();
}

public enum Gen2ActorSection
{
	Structures,
	Units,
	Infantry,
	Terrain,
}

public enum Gen2OverlayResolutionKind
{
	Ignore,
	Actor,
	Resource,
	Terrain,
}

public sealed class Gen2OverlayResolution
{
	private Gen2OverlayResolution(
		Gen2OverlayResolutionKind kind,
		string actorType,
		Size shape,
		DamageState damageState,
		byte resourceType,
		byte resourceDensity);
	public Gen2OverlayResolutionKind Kind { get; }
	public string ActorType { get; }
	public Size Shape { get; }
	public DamageState DamageState { get; }
	public byte ResourceType { get; }
	public byte ResourceDensity { get; }
	public static bool TryCreate(
		Gen2OverlayResolutionKind kind,
		string actorType,
		Size shape,
		DamageState damageState,
		byte resourceType,
		byte resourceDensity,
		out Gen2OverlayResolution resolution);
}

public interface IGen2MapDialect
{
	Gen2MapCordon Cordon { get; }
	bool TryResolveTheater(string legacyTheater, out string terrainId);
	bool TryResolveTile(
		string terrainId,
		ushort legacyTile,
		byte legacySubTile,
		out TerrainTile tile);
	bool TryResolveActor(
		Gen2ActorSection section,
		string legacyActor,
		out string actorType);
	bool TryResolveOverlay(
		byte overlayType,
		byte overlayData,
		out Gen2OverlayResolution resolution);
	bool TryResolveDeployableActor(string actorType, out string deployedActorType);
	bool IsLampActor(string actorType);
	bool IsCreepActor(string actorType);
}

public sealed class Gen2MapImportRequest
{
	private Gen2MapImportRequest(
		IGen2MapSource source,
		ModData modData,
		IGen2MapDialect dialect,
		Gen2MapImportLimits limits,
		string fallbackTitle,
		string author,
		string requiresMod,
		Color explicitDefaultPlayerColor);
	public static Gen2MapImportRequest Create(
		IGen2MapSource source,
		ModData modData,
		IGen2MapDialect dialect,
		Gen2MapImportLimits limits,
		string fallbackTitle,
		string author,
		string requiresMod,
		Color explicitDefaultPlayerColor);
	public IGen2MapSource Source { get; }
	public ModData ModData { get; }
	public IGen2MapDialect Dialect { get; }
	public Gen2MapImportLimits Limits { get; }
	public string FallbackTitle { get; }
	public string Author { get; }
	public string RequiresMod { get; }
	public Color ExplicitDefaultPlayerColor { get; }
}

public sealed class Gen2MapImporter
{
	public Gen2MapImportResult Import(
		Gen2MapImportRequest request,
		IGen2ImportDeadline deadline,
		CancellationToken cancellationToken);
}

public interface IGen2ImportDeadline
{
	void ThrowIfExpired();
}

public class PlayerReference
{
	public PlayerReference();
	public PlayerReference(Color explicitDefaultColor);
	public PlayerReference(MiniYaml yaml);
	public PlayerReference(MiniYaml yaml, Color explicitDefaultColor);
}

public class MapPlayers
{
	public MapPlayers(
		IEnumerable<MiniYamlNode> playerDefinitions,
		Color explicitDefaultColor);
}
```

RED cases cover the modern cordon `(1,16,1,4)` with top uninteractable `6`; malformed `Size`/`LocalSize`; dimensions above `512 x 512`; cells above `262144`; actors `8193`; spawns `9`; duplicate/out-of-bounds spawns; non-multiplayer maps; unknown tile/actor/overlay; truncated LZO/LCW blocks; timeout, cancellation; and checked coordinate arithmetic. Unknown data must return a typed diagnostic instead of substituting tile zero or silently omitting an actor. `Gen2MapImportResult` enforces exactly one state: success privately owns a non-null map and `Error=None`; failure owns no map and one non-None closed error. `TakeMap` succeeds exactly once only on success; `Dispose` releases an untaken map and is idempotent. Diagnostics are defensive-copied, bounded, contain only the closed error plus integer record/coordinate fields, and never accept text or a path.

The plain `Map` and `ActorType` references intentionally allow runtime null without nullable annotations; the map exists only inside a successful result until one-shot transfer. `Gen2OverlayResolution` is an engine-owned bounded projection consumed across the Game/RA2 assembly boundary, not an authority capability: it has no public/copy constructor, record `with`, setter/init, or subclass path, and its sole public `TryCreate` completes the entire kind/null/default/shape/resource matrix before returning success. `ActorType` is non-null exactly for `Kind=Actor`; Ignore/Resource/Terrain require null and all fields not used by the selected kind must equal their one canonical default. Failure returns false with a null out value and no partial object. The optional Yuri context is null exactly when no completely validated Yuri pair exists. `Ra2MapImportContexts.Create` validates and borrows the caller-owned Base and optional Yuri contexts; it never transfers or disposes them on either success or failure. The enclosing validator remains their sole owner and releases them in its existing reverse-order `finally`. Tests enumerate these null/kind/borrow combinations, prove failure does not dispose caller contexts, and prove the RA2 dialect constructs resolutions only through the public validating factory.

`Gen2MapImportRequest.Create` is a programmer-contract boundary: it rejects null/default dependencies, noncanonical SHA-256/length, a limit set larger than the caller's fixed policy, empty/noncanonical mod/title/author fields, a dialect with invalid cordon arithmetic, and mutable/default mapping results before parsing. It contains no path, output package, global-state accessor, or arbitrary callback. The caller retains the source, ModData, and dialect; `Import` calls `OpenVerifiedRead` exactly once, takes ownership of and disposes only that returned stream, bounded-copies at most `MaximumSourceBytes` into an owned immutable buffer while recomputing length/SHA-256, and parses only that buffer. It never disposes the source/request dependencies. A recomputed byte count different from `IGen2MapSource.Length` returns exactly `SourceLengthMismatch`; an equal-length stream whose recomputed digest differs returns exactly `SourceHashMismatch`; neither is collapsed into `SourceTooLarge`, an unsupported-map diagnostic, or a raw exception. A successful result transfers sole ownership of the returned `Map` only through one `TakeMap`; every failure disposes partial maps/buffers and returns a closed diagnostic, and disposing an untaken success closes its map. Tests cover nonseekable streams, one-open ownership, post-open source mutation, wrong length, same-length wrong hash, take/dispose ordering, dispose on every result/timeout/cancel branch, caller mutation of dialect source collections, and absence of any raw-path/open delegate API.

`IGen2MapDialect` is the complete engine-neutral mapping surface. `Ra2Gen2Dialect` and the characterized Tiberian Sun adapter defensively copy their closed theater/tile/actor/overlay/deployable/lamp/creep tables at construction and implement only these resolvers; the importer never downcasts to a CLI or reads a static dictionary. An unknown resolver input is a typed unsupported diagnostic. Tests cover every resolver branch, duplicate/case-colliding table keys, returned string/shape bounds, and byte-equivalent trusted TS output.

The RA2-facing result is immutable and contains no original source path:

```csharp
public sealed record Ra2MapCandidate(
	IRa2MapSourceHandle Source);

public sealed class Ra2MapImportContexts
{
	private Ra2MapImportContexts(
		Ra2MapImportContext baseContext,
		Ra2MapImportContext yuriContext);
	public static RetailOperationResult<Ra2MapImportContexts> Create(
		Ra2MapImportContext baseContext,
		Ra2MapImportContext yuriContext);
	public RetailOperationResult<Ra2MapImportContext> SelectFor(
		Ra2MapCandidate candidate);
}

public sealed class ConvertedMapArtifact
{
	public string CanonicalStagedRelativePath { get; }
	public string SourceSha256 { get; }
	public string PackageSha256 { get; }
	public long CompressedBytes { get; }
	private ConvertedMapArtifact(
		string canonicalStagedRelativePath,
		string sourceSha256,
		string packageSha256,
		long compressedBytes);
	internal static ConvertedMapArtifact Create(
		string canonicalStagedRelativePath,
		string sourceSha256,
		string packageSha256,
		long compressedBytes);
}

public sealed class Ra2MapConversionService
{
	public Ra2MapConversionService(IRetailMonotonicClock clock);

	public Task<RetailOperationResult<ConvertedMapArtifact>> ConvertAsync(
		Ra2MapCandidate candidate,
		Ra2MapImportContexts contexts,
		RetailStagingTarget destination,
		RetailValidationDeadline deadline,
		IProgress<RetailImportProgress> progress,
		CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Run RED**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~Gen2MapImporterTest|FullyQualifiedName~Ra2MapConversionServiceTest' \
  -v minimal
```

Expected: missing service/model symbols and at least one legacy fallback assertion failure.

- [ ] **Step 3: Extract the pure converter and keep both CLIs thin**

Move parsing, checked decompression, theater/tile/actor/overlay mapping, cordon computation, player/spawn creation, and bounds checks into `Gen2MapImporter`. `ImportGen2MapCommand` and `ImportTiberianSunMapCommand` supply only their dialect and save the returned `Map`; characterization tests require byte-equivalent trusted TS output.

`Ra2Gen2Dialect` supplies RA2 mappings. Direct staged files and Task 2 embedded handles adapt to both Task 3's sealed `IRa2MapSourceHandle` and the borrowed engine `IGen2MapSource`; conversion never accepts a raw path or arbitrary reopen delegate and rechecks length/hash while reading. `RetailCandidateValidator` first calls `PreGameEngineContext.OpenModData`, immediately passes each owned scope to `Ra2MapImportContext.Create`, and may unwrap only a successful `RetailOperationResult<Ra2MapImportContext>`; a failed factory result has already disposed the scope and returns one sanitized failure, so no naked exception/context or second disposal is possible. `Ra2MapImportContexts.Create` then requires one successful Base context and at most one successful verified-Yuri context and borrows both. `SelectFor` uses only the sanitized suffix after ASCII folding: `.map`/`.mpr` require Base, `.yrm` requires Yuri, and any other/confusable/trailing-dot/compound suffix fails before output creation. A `.yrm` without a complete pair is quarantined; no Base-then-Yuri fallback or deadline refresh occurs. Hidden Yuri content mislabeled Base fails closed. One Yuri conversion failure quarantines only that candidate and not a valid Base sibling. Task 9 tests inject both Task 3 factory failures and prove exact scope ownership plus no conversion/output call afterward.

`Ra2MapConversionService` receives the same injected `IRetailMonotonicClock` that created the deadline, adapts `RetailValidationDeadline` to the engine-neutral `IGen2ImportDeadline`, writes only `CreateNew` files below the caller's sealed transaction map directory, sets `RequiresMod = "ra2"` and `Visibility = MapVisibility.Lobby`, recomputes the staged package hash/size, and returns only `RetailOperationResult<ConvertedMapArtifact>`. It maps every closed `Gen2MapImportError` to a fixed `RetailImportErrorCode`/`RetailImportDiagnosticCode`; output create/save/compress/hash/flush failures map to sanitized storage/conversion codes and never expose an exception or path. Except for cooperative `OperationCanceledException`, no parser, compression, I/O, or hashing exception escapes this boundary. `Gen2MapImporter` checks the same deadline inside every decompression/tile/actor loop, and the service continues checks through package serialization, compression, fsync, and SHA-256 loops; a fake clock/probe proves timeout from inside every phase, removes only the uncommitted CreateNew target, and leaves outside/previous bytes unchanged. The caller creates one five-minute per-map deadline and Task 10 post-validation reuses that same start/budget pair; conversion cannot consume five minutes and then receive a fresh budget. It has no `Ready`, profile-compatibility, receipt, or publication API. Task 10 is the sole component that may transform this result into a `ValidatedMapArtifact`, so Task 9 compiles and turns GREEN without a forward dependency or a temporarily trusted callback.

Task 9 also removes the converter's remaining global player/grid dependencies. Both `Map` constructors resolve `MapGrid` through `modData.Manifest.Get<MapGrid>(modData.ObjectCreator)`. `PlayerReference` gains the exact explicit-default-color constructors above and `MapPlayers` the exact explicit-default-color overload; `Gen2MapImporter` passes `request.ExplicitDefaultPlayerColor` when generating player definitions. Existing desktop constructors remain characterized adapters. With `Game.ModData` replaced by a throwing sentinel, conversion, both `Map` constructors, and player generation still succeed through explicit dependencies; source audits forbid parameterless `Manifest.Get<MapGrid>()` or legacy `MapPlayers` calls in the new path.

The RA2 CLI calls the same service and no longer assigns `Game.ModData` or writes to an implicit working-directory target. Its explicit context supplies every dependency; no conversion or validation call may select the legacy global-state adapter.

- [ ] **Step 4: Run GREEN, full engine tests, and commit**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~Gen2MapImporterTest|FullyQualifiedName~Ra2MapConversionServiceTest' \
  -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore -m:1
git diff --check
```

Commit only the thirteen production/test paths:

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 9 --phase implementation
git commit -m "Refactor RA2 map conversion for validated imports"
```

- [ ] **Step 5: Generate and verify `0023` immediately**

Build the patch from official+`0001`...`0022` with the exact nine engine paths:

```bash
set -euo pipefail
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
PATCH_STAGE="$(mktemp -d /private/tmp/nuke-hour-engine-patch-0023.XXXXXX)"
ALLOWLIST="$PATCH_STAGE/0023.allowlist"
VERIFY_DIR="$PATCH_STAGE/verification"
PREDECESSOR_END=22
CANDIDATE=engine-patches/0023-ra2-map-conversion-service.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0023 --output "$ALLOWLIST"
LC_ALL=C sort -c -u "$ALLOWLIST"
test "$(wc -l < "$ALLOWLIST" | tr -d ' ')" -eq 9
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
test -f "$VERIFY_DIR/evidence.json"
```

The self-contained block above runs candidate generation and the independent verifier; update README/verifier tests only after a fresh strict replay passes, then commit the three delivery paths:

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 9 --phase delivery
git commit -m "Deliver RA2 map conversion engine patch"
```

---

### Task 10: Validate bounded `.oramap` packages and lazy previews; deliver `0024`

**Outer files:**

- Create: `OpenRA.Mods.RA2/Content/Maps/Ra2OramapEnvelope.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/Ra2MapValidationService.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/BoundedMapStringInterner.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailCandidateValidator.cs`

**Exact `0024` engine allowlist:**

- Modify: `engine/OpenRA.Game/FileFormats/Png.cs`
- Modify: `engine/OpenRA.Game/FileSystem/ZipFile.cs`
- Modify: `engine/OpenRA.Game/Graphics/PlatformInterfaces.cs`
- Modify: `engine/OpenRA.Game/Graphics/SheetBuilder.cs`
- Create: `engine/OpenRA.Game/Map/MapUid.cs`
- Create: `engine/OpenRA.Game/Map/VerifiedMapSnapshot.cs`
- Create: `engine/OpenRA.Game/Map/MapPreviewImageCache.cs`
- Modify: `engine/OpenRA.Game/Map/ActorReference.cs`
- Modify: `engine/OpenRA.Game/Map/MapCache.cs`
- Modify: `engine/OpenRA.Game/Map/MapPreview.cs`
- Modify: `engine/OpenRA.Game/Renderer.cs`
- Modify: `engine/OpenRA.Platforms.Default/OpenGL.cs`
- Modify: `engine/OpenRA.Platforms.Default/Sdl2GraphicsContext.cs`
- Modify: `engine/OpenRA.Platforms.Default/ThreadedGraphicsContext.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/MapPreviewWidget.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/MapChooserLogic.cs`
- Modify: `engine/OpenRA.Test/OpenRA.Game/PngTest.cs`
- Create: `engine/OpenRA.Test/BoundedOramapEnvelopeTest.cs`
- Create: `engine/OpenRA.Test/Ra2MapValidationServiceTest.cs`
- Create: `engine/OpenRA.Test/MapPreviewImageCacheTest.cs`
- Create: `engine/OpenRA.Test/SheetBuilderBoundsTest.cs`

**Delivery files:**

- Create: `engine-patches/0024-verified-oramap-map-cache.patch`
- Modify: `engine-patches/README.md`
- Modify: `packaging/tests/test_verify_engine_patch_delivery.py`

- [ ] **Step 1: Write RED envelope and semantic-validation tests**

Use one snapshot contract for imported and downloaded maps:

```csharp
public interface IVerifiedMapPackageSource : IDisposable
{
	string CanonicalRelativePath { get; }
	long Length { get; }
	string PackageSha256 { get; }
	Stream OpenVerifiedRead();
}

public sealed record VerifiedMapPackageInput(
	IVerifiedMapPackageSource Package,
	string SourceSha256,
	int ConverterVersion);

public sealed class ValidatedMapArtifact
{
	public string LogicalName { get; }
	public string CanonicalArtifactRelativePath { get; }
	public string SourceSha256 { get; }
	public string PackageSha256 { get; }
	public long CompressedBytes { get; }
	public MapUid Uid { get; }
	public Ra2MapCompatibleProfiles CompatibleProfiles { get; }
	public ValidatedMapMetadata Metadata { get; }
	public int EnvelopeVersion { get; }
	public int ConverterVersion { get; }
	public int MapFormat { get; }
	public long RetainedMetadataBytes { get; }
	public long RetainedStringBytes { get; }
	public int RetainedStringEntries { get; }
	private ValidatedMapArtifact(
		string logicalName,
		string canonicalArtifactRelativePath,
		string sourceSha256,
		string packageSha256,
		long compressedBytes,
		MapUid uid,
		Ra2MapCompatibleProfiles compatibleProfiles,
		ValidatedMapMetadata metadata,
		int envelopeVersion,
		int converterVersion,
		int mapFormat,
		long retainedMetadataBytes,
		long retainedStringBytes,
		int retainedStringEntries);
	internal static ValidatedMapArtifact Create(
		string logicalName,
		string canonicalArtifactRelativePath,
		string sourceSha256,
		string packageSha256,
		long compressedBytes,
		MapUid uid,
		Ra2MapCompatibleProfiles compatibleProfiles,
		ValidatedMapMetadata metadata,
		int envelopeVersion,
		int converterVersion,
		int mapFormat,
		long retainedMetadataBytes,
		long retainedStringBytes,
		int retainedStringEntries);
}

// Constructor invariant: LogicalName == Uid.Value (canonical lowercase hex).
// CanonicalArtifactRelativePath identifies only the sealed source-domain object;
// publication remaps it to maps/{Uid.Value}.oramap.

public enum Ra2MapValidationPurpose
{
	RetailCatalog,
	RuntimeCurrentProfile,
}

public readonly struct Ra2MapProfileContext :
	IEquatable<Ra2MapProfileContext>
{
	public Ra2RuntimeProfileId Profile { get; }
	public Ra2MapValidationContext Context { get; }
	public Ra2MapProfileContext(
		Ra2RuntimeProfileId profile,
		Ra2MapValidationContext context);
	public bool Equals(Ra2MapProfileContext other);
	public override bool Equals(object obj);
	public override int GetHashCode();
	public static bool operator ==(
		Ra2MapProfileContext left, Ra2MapProfileContext right);
	public static bool operator !=(
		Ra2MapProfileContext left, Ra2MapProfileContext right);
}

public sealed class Ra2MapValidationContexts
{
	private Ra2MapValidationContexts(
		Ra2MapValidationPurpose purpose,
		ImmutableArray<Ra2MapProfileContext> profiles);
	public Ra2MapValidationPurpose Purpose { get; }
	public ImmutableArray<Ra2MapProfileContext> Profiles { get; }
	public static RetailOperationResult<Ra2MapValidationContexts> CreateRetail(
		Ra2MapValidationContext baseContext,
		Ra2MapValidationContext yuriContext);
	public static RetailOperationResult<Ra2MapValidationContexts> CreateRuntime(
		Ra2RuntimeProfileId currentProfile,
		Ra2MapValidationContext currentContext);
}

internal sealed class Ra2ConstructedMap : IDisposable
{
	private Ra2ConstructedMap(Map map, IReadOnlyPackage package);
	internal Map Map { get; }
	internal static Ra2ConstructedMap Create(
		Map map,
		IReadOnlyPackage ownedPackage);
	public void Dispose();
}

internal interface IRa2MapConstructionFactory
{
	Ra2ConstructedMap Open(
		ModData modData,
		IReadOnlyPackage ownedPackage);
}

public sealed class Ra2MapValidationService
{
	public Ra2MapValidationService();
	internal Ra2MapValidationService(
		IRa2MapConstructionFactory construction);
	public Task<RetailOperationResult<ValidatedMapArtifact>> ValidatePackageAsync(
		VerifiedMapPackageInput input,
		Ra2MapValidationContexts contexts,
		MapValidationDeadlineSet deadlines,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken);
}

public readonly struct MapValidationDeadlineSet :
	IEquatable<MapValidationDeadlineSet>
{
	public RetailValidationDeadline Operation { get; }
	public RetailValidationDeadline Whole { get; }
	public MapValidationDeadlineSet(
		RetailValidationDeadline operation,
		RetailValidationDeadline whole);
	public static MapValidationDeadlineSet ForRetail(
		RetailValidationDeadline map,
		RetailValidationDeadline whole);
	public static MapValidationDeadlineSet ForRuntime(
		RetailValidationDeadline sharedFiveMinuteDeadline);
	public void ThrowIfExpired(IRetailMonotonicClock clock);
	public bool Equals(MapValidationDeadlineSet other);
	public override bool Equals(object obj);
	public override int GetHashCode();
	public static bool operator ==(
		MapValidationDeadlineSet left, MapValidationDeadlineSet right);
	public static bool operator !=(
		MapValidationDeadlineSet left, MapValidationDeadlineSet right);
}

public readonly struct MapUid : IEquatable<MapUid>, IComparable<MapUid>
{
	public const int HexLength = 40;
	public string Value { get; }
	public static bool TryParse(string value, out MapUid uid);
	public int CompareTo(MapUid other);
	public bool Equals(MapUid other);
	public override bool Equals(object obj);
	public override int GetHashCode();
	public override string ToString();
	public static bool operator ==(MapUid left, MapUid right);
	public static bool operator !=(MapUid left, MapUid right);
}

public interface IVerifiedMapSnapshotProvider
{
	event Action SnapshotChanged;
	VerifiedMapSnapshotLease Acquire();
}

public interface IGameThreadAffinity
{
	bool IsGameThread { get; }
}

internal interface IVerifiedMapEntrySource : IDisposable
{
	IReadOnlyPackage OpenPackage();
	IMapPreviewImageSource AcquirePreviewSource();
}

public sealed class VerifiedMapPlayerMetadata
{
	private VerifiedMapPlayerMetadata(
		string name,
		string palette,
		string bot,
		string startingUnitsClass,
		bool allowBots,
		bool playable,
		bool required,
		bool ownsWorld,
		bool spectating,
		bool nonCombatant,
		bool lockFaction,
		string faction,
		bool lockColor,
		Color color,
		CPos homeLocation,
		bool lockSpawn,
		int spawn,
		bool lockTeam,
		int team,
		bool lockHandicap,
		int handicap,
		ImmutableArray<string> allies,
		ImmutableArray<string> enemies);
	public string Name { get; }
	public string Palette { get; }
	public string Bot { get; }
	public string StartingUnitsClass { get; }
	public bool AllowBots { get; }
	public bool Playable { get; }
	public bool Required { get; }
	public bool OwnsWorld { get; }
	public bool Spectating { get; }
	public bool NonCombatant { get; }
	public bool LockFaction { get; }
	public string Faction { get; }
	public bool LockColor { get; }
	public Color Color { get; }
	public CPos HomeLocation { get; }
	public bool LockSpawn { get; }
	public int Spawn { get; }
	public bool LockTeam { get; }
	public int Team { get; }
	public bool LockHandicap { get; }
	public int Handicap { get; }
	public ImmutableArray<string> Allies { get; }
	public ImmutableArray<string> Enemies { get; }
	public static bool TryCreate(
		string name,
		string palette,
		string bot,
		string startingUnitsClass,
		bool allowBots,
		bool playable,
		bool required,
		bool ownsWorld,
		bool spectating,
		bool nonCombatant,
		bool lockFaction,
		string faction,
		bool lockColor,
		Color color,
		CPos homeLocation,
		bool lockSpawn,
		int spawn,
		bool lockTeam,
		int team,
		bool lockHandicap,
		int handicap,
		ImmutableArray<string> allies,
		ImmutableArray<string> enemies,
		out VerifiedMapPlayerMetadata metadata);
}

public sealed class VerifiedMapPreviewMetadata
{
	private VerifiedMapPreviewMetadata(
		string title,
		string author,
		ImmutableArray<string> categories,
		string tileset,
		MapGridType gridType,
		int mapWidth,
		int mapHeight,
		int boundsX,
		int boundsY,
		int boundsWidth,
		int boundsHeight,
		ImmutableArray<VerifiedMapPlayerMetadata> players,
		ImmutableArray<CPos> spawnPoints,
		int mapFormat);
	public string Title { get; }
	public string Author { get; }
	public ImmutableArray<string> Categories { get; }
	public string Tileset { get; }
	public MapGridType GridType { get; }
	public int MapWidth { get; }
	public int MapHeight { get; }
	public int BoundsX { get; }
	public int BoundsY { get; }
	public int BoundsWidth { get; }
	public int BoundsHeight { get; }
	public ImmutableArray<VerifiedMapPlayerMetadata> Players { get; }
	public ImmutableArray<CPos> SpawnPoints { get; }
	public int MapFormat { get; }
	public int PlayablePlayers => Players.Count(player => player.Playable);
	public static bool TryCreate(
		string title,
		string author,
		ImmutableArray<string> categories,
		string tileset,
		MapGridType gridType,
		int mapWidth,
		int mapHeight,
		int boundsX,
		int boundsY,
		int boundsWidth,
		int boundsHeight,
		ImmutableArray<VerifiedMapPlayerMetadata> players,
		ImmutableArray<CPos> spawnPoints,
		int mapFormat,
		out VerifiedMapPreviewMetadata metadata);
}

public readonly struct VerifiedMapResidentUsage :
	IEquatable<VerifiedMapResidentUsage>
{
	public long MetadataBytes { get; }
	public long StringBytes { get; }
	public int StringEntries { get; }
	public VerifiedMapResidentUsage(
		long metadataBytes, long stringBytes, int stringEntries);
	public bool Equals(VerifiedMapResidentUsage other);
	public override bool Equals(object obj);
	public override int GetHashCode();
	public static bool operator ==(
		VerifiedMapResidentUsage left, VerifiedMapResidentUsage right);
	public static bool operator !=(
		VerifiedMapResidentUsage left, VerifiedMapResidentUsage right);
}

public static class VerifiedMapResidentUsageMeter
{
	public const int SchemaVersion = 1;
	public static VerifiedMapResidentUsage Measure(
		VerifiedMapPreviewMetadata metadata);
}

public readonly struct VerifiedMapResidentBudgetLimits :
	IEquatable<VerifiedMapResidentBudgetLimits>
{
	public long MetadataBytes { get; }
	public long StringBytes { get; }
	public int StringEntries { get; }
	public VerifiedMapResidentBudgetLimits(
		long metadataBytes, long stringBytes, int stringEntries);
	public bool Equals(VerifiedMapResidentBudgetLimits other);
	public override bool Equals(object obj);
	public override int GetHashCode();
	public static bool operator ==(
		VerifiedMapResidentBudgetLimits left,
		VerifiedMapResidentBudgetLimits right);
	public static bool operator !=(
		VerifiedMapResidentBudgetLimits left,
		VerifiedMapResidentBudgetLimits right);
}

public enum VerifiedMapResidentBudgetFailure
{
	None,
	DuplicateUid,
	DuplicateLogicalName,
	MetadataLimit,
	StringBytesLimit,
	StringEntriesLimit,
	ArithmeticOverflow,
}

public sealed class VerifiedMapResidentBudgetResult
{
	private VerifiedMapResidentBudgetResult(
		bool succeeded,
		VerifiedMapResidentUsage usage,
		VerifiedMapResidentBudgetFailure failure);
	public bool Succeeded { get; }
	public VerifiedMapResidentUsage Usage { get; }
	public VerifiedMapResidentBudgetFailure Failure { get; }
	internal static VerifiedMapResidentBudgetResult Success(
		VerifiedMapResidentUsage usage);
	internal static VerifiedMapResidentBudgetResult Failed(
		VerifiedMapResidentBudgetFailure failure);
}

public static class VerifiedMapResidentBudget
{
	public static VerifiedMapResidentBudgetResult ValidateUnion(
		ImmutableArray<VerifiedMapEntry> visibleEntries,
		VerifiedMapResidentBudgetLimits limits);
}

public interface IMapPreviewImageSource : IDisposable
{
	long EncodedBytes { get; }
	MapPreviewImageLease Decode(
		MapPreviewCachePolicy policy,
		CancellationToken cancellationToken);
}

public sealed class MapPreviewImageLease : IDisposable
{
	private MapPreviewImageLease(
		int width,
		int height,
		IMemoryOwner<byte> ownedBgraPixels,
		int byteLength);
	public int Width { get; }
	public int Height { get; }
	public long AccountedBytes { get; }
	public static MapPreviewImageLease Create(
		int width,
		int height,
		IMemoryOwner<byte> ownedBgraPixels,
		int byteLength);
	public ReadOnlySpan<byte> GetBgraPixels();
	public void Dispose();
}

public sealed class VerifiedMapSnapshotLease : IDisposable
{
	public ImmutableArray<VerifiedMapEntry> Entries { get; }
	internal VerifiedMapSnapshotLease(
		ImmutableArray<VerifiedMapEntry> entries,
		VerifiedMapSnapshotRootPin rootPin);
	public void Dispose();
}

internal sealed class VerifiedMapSnapshotRootPin : IDisposable
{
	public VerifiedMapSnapshotRootPin Acquire();
	public void Dispose();
}

public sealed class VerifiedMapEntry
{
	private VerifiedMapEntry(
		MapUid uid,
		string logicalName,
		bool canDelete,
		VerifiedMapPreviewMetadata metadata,
		VerifiedMapResidentUsage residentUsage,
		IVerifiedMapEntrySource ownedSource);
	public MapUid Uid { get; }
	public string LogicalName { get; }
	public bool CanDelete { get; }
	public VerifiedMapPreviewMetadata Metadata { get; }
	public VerifiedMapResidentUsage ResidentUsage { get; }
	public long RetainedMetadataBytes => ResidentUsage.MetadataBytes;
	public long RetainedStringBytes => ResidentUsage.StringBytes;
	public int RetainedStringEntries => ResidentUsage.StringEntries;
	public IReadOnlyPackage OpenPackage();
	public IMapPreviewImageSource AcquirePreviewSource();
	internal static VerifiedMapEntry Create(
		MapUid uid,
		string logicalName,
		bool canDelete,
		VerifiedMapPreviewMetadata metadata,
		IVerifiedMapEntrySource ownedSource);
}

internal sealed class VerifiedMapSnapshotPublisher :
	IVerifiedMapSnapshotProvider, IDisposable
{
	internal VerifiedMapSnapshotPublisher(IGameThreadAffinity affinity);
	public event Action SnapshotChanged;
	public VerifiedMapSnapshotLease Acquire();
	internal void Publish(ImmutableArray<VerifiedMapEntry> ownedEntries);
	internal void Clear();
	public void Dispose();
}

public readonly struct MapPreviewBudgetSnapshot :
	IEquatable<MapPreviewBudgetSnapshot>
{
	public long CpuBytes { get; }
	public long PinnedCpuBytes { get; }
	public long GpuBytes { get; }
	public long PinnedGpuBytes { get; }
	public MapPreviewBudgetSnapshot(
		long cpuBytes,
		long pinnedCpuBytes,
		long gpuBytes,
		long pinnedGpuBytes);
	public bool Equals(MapPreviewBudgetSnapshot other);
	public override bool Equals(object obj);
	public override int GetHashCode();
	public static bool operator ==(
		MapPreviewBudgetSnapshot left, MapPreviewBudgetSnapshot right);
	public static bool operator !=(
		MapPreviewBudgetSnapshot left, MapPreviewBudgetSnapshot right);
}

public interface IMapPreviewBudgetProvider
{
	long CpuLimit { get; }
	long GpuLimit { get; }
	MapPreviewBudgetSnapshot Capture();
}

internal sealed class MapPreviewBudgetReservation : IDisposable
{
	internal MapPreviewBudgetReservation(
		MapPreviewBudgetLedger owner,
		long bytes,
		bool isCpu,
		bool pinned);
	internal long Bytes { get; }
	internal bool IsCpu { get; }
	internal bool IsPinned { get; }
	public void Dispose();
}

public sealed class MapPreviewBudgetLedger : IMapPreviewBudgetProvider
{
	public MapPreviewBudgetLedger(long cpuLimit, long gpuLimit);
	public long CpuLimit { get; }
	public long GpuLimit { get; }
	public MapPreviewBudgetSnapshot Capture();
	internal bool TryAcquireCpu(
		long bytes,
		bool pinned,
		out MapPreviewBudgetReservation reservation);
	internal bool TryAcquireGpu(
		long bytes,
		bool pinned,
		out MapPreviewBudgetReservation reservation);
	internal void SetPinned(
		MapPreviewBudgetReservation reservation,
		bool pinned);
}

public sealed record MapPreviewCachePolicy(
	long CpuBytes,
	long GpuBytes,
	PngReadLimits SourcePng,
	int ThumbnailEdge,
	int PreferredAtlasPage,
	int AtlasMargin,
	MapPreviewBudgetLedger BudgetLedger);

public sealed class RetailCandidateValidator : IRetailCandidateValidator, IDisposable
{
	internal RetailCandidateValidator(
		RetailArchiveGraphValidator archives,
		RetailProfileValidator profiles,
		Ra2RuntimeContractBuilder contracts,
		Ra2MapConversionService conversion,
		Ra2MapValidationService maps,
		PreGameEngineContext borrowedPreGameContext,
		IRetailMonotonicClock clock);
	public Task<RetailOperationResult<RetailValidatedCandidateInput>> ValidateAsync(
		RetailStagedPlan staged,
		RetailCandidateArtifactSource artifactSource,
		RetailActiveGenerationSnapshot preservedActive,
		RetailImportOperationIdentity borrowedOperationIdentity,
		RetailValidationDeadline deadline,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken,
		IProgress<RetailImportProgress> progress);
	public Task<RetailOperationResult<RetailValidatedRepairInput>> ValidateRepairAsync(
		RetailActiveGenerationSnapshot active,
		RetailCatalogRepairPlan repair,
		RetailImportOperationIdentity borrowedOperationIdentity,
		RetailValidationDeadline deadline,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken,
		IProgress<RetailImportProgress> progress);
	public void Dispose();
}
```

All five Task 10 value contracts above are handwritten C# 9 `readonly struct : IEquatable<T>` implementations with explicit constructors/get-only properties, null-safe complete equality/hash, and operators; operational factories reject their default values. `MapUid.Equals(object)` uses the plain C# 9 signature and `obj is MapUid other`. Optional player strings are plain references: Palette/Bot/StartingUnitsClass are null only when the source node is absent, and Faction is non-null exactly when `LockFaction`; the Yuri context and preserved-active snapshot are null only for Base-only/no-active inputs. Constructors defensively validate these states and tests cover default/null/caller-mutation cases without nullable annotations.

Require exact root entries `map.yaml`, `map.bin`, optional `map.png`; reject directories, symlinks, duplicate normalized names, alternate separators, traversal, extra entries, unknown sizes, encryption, and unsupported methods. Map `IosImportEnvelopePolicyV1.OramapCompressedBytes` directly to `ZipEnvelopeLimits.MaximumCompressedBytes`: the actual bounded package length and the separately checked sum of central-directory compressed entry sizes must each be at most 64 MiB, and every entry/central-directory offset and range must be consistent with that actual length, independently of the broader retail source-file cap. Also reject expansion above 64 MiB per entry/96 MiB aggregate and ratio above 100:1. `BoundedOramapEnvelopeTest` covers whole-package and central-directory compressed totals at `64 MiB - 1`, exact, and `+ 1`, including two individually valid 40 MiB stored entries whose aggregate compressed size must fail, plus every expanded/ratio boundary.

Before `MiniYaml`, enforce 8 MiB, 131072 nodes, depth 64, 256 KiB line, 64 KiB scalar and reject nonempty `Rules`, `Weapons`, `Voices`, `Sequences`, `ModelSequences`, `Music`, `Notifications`, or `FluentMessages`. Before `Png`, enforce signature/IHDR/chunk rules and the V1 decoded budgets.

Before constructing any `CellLayer`, `MapPlayers`, or `Map`, parse the already bounded `map.yaml` projection and require `MapFormat == Map.CurrentMapFormat`; the current value is part of the runtime contract, and neither a legacy lower value nor an unknown future higher value is accepted by this importer even though the general desktop loader may support migration. Map `IosImportEnvelopePolicyV1.TraditionalMapWidth`, `TraditionalMapHeight`, and `TraditionalMapCells` to every final direct, converted, retail, and runtime map: width and height must each be positive and at most 512, and their checked product must be at most 262,144. Require a nonempty `Bounds` rectangle wholly contained in that size, and reject every raw player-home, actor-location, and spawn coordinate outside the checked map/bounds contract before materializing engine objects.

Preflight `map.bin` against the same dimensions before calling either `Map` constructor. Accept only binary formats 1 and 2 with their exact 5-byte/17-byte headers; compute every tile (3 bytes/cell), resource (2 bytes/cell), and applicable height (1 byte/cell) span in checked 64-bit arithmetic; require the format/grid-mandated nonzero sections; require every offset to start after its header and every `offset + span` to be within the actual bounded entry length; and reject overlapping nonzero spans, truncated headers/payloads, dimension disagreement, unknown formats, and any conversion to `int`/`uint` that would overflow. Format 1's implicit offsets are computed with the same checked rules, while format 2's explicit offsets never authorize a seek until all spans have passed as one immutable layout. `Ra2OramapEnvelope` returns that validated layout capability and `Ra2MapValidationService` consumes the same pinned package handle, so validation cannot preflight one `map.bin` and construct `Map` from a reopened/replaced file. RED cases cover YAML map format `current - 1`, exact current, `current + 1`, negative and integer extremes; width/height `-1`, `0`, `511`, `512`, and `513`; checked cell-product overflow; bounds/coordinate edges; binary format `0`/`1`/`2`/`3`/`255`; binary dimension mismatch; truncated 4/5/16/17-byte headers; offset-before-header; `offset + span` overflow/out-of-range; overlapping tile/height/resource spans; and exact final-byte spans. A recording package proves every rejected case exits before `Map`, `CellLayer`, `MapPlayers`, or actor initialization is invoked.

Still before construction, count the raw `PlayerDefinitions` nodes and require at most the exact V1 value 63 (`MapPlayers.MaximumPlayerCount` must equal that pinned constant), unique canonical player names, exactly one profile-known world owner, only the closed profile-known system-owner set for nonplayable system entries, and no dangling actor owner. After construction assert the same exact current map format, `RequiresMod == "ra2"`, exact `Visibility == MapVisibility.Lobby`, no invalid custom rules, zero replaced terrain tiles, actors at most 8192, valid owners, and 2-8 playable player definitions paired with the same count of unique in-bounds spawns. Validate independently under Base and Yuri contexts and record the exact compatibility set; at least Base must succeed for Base readiness. Every direct and converted map follows this identical method. RED cases cover 62/63/64 definitions, duplicate/case-colliding names, multiple/missing world owners, nonplayable-owner floods, playable/spawn count mismatch, and dangling actor owners before allocation/publication.

Use the exact explicit-default overloads delivered by Task 9:

```csharp
public class PlayerReference
{
	public PlayerReference();
	public PlayerReference(Color explicitDefaultColor);
	public PlayerReference(MiniYaml yaml);
	public PlayerReference(MiniYaml yaml, Color explicitDefaultColor);
}

public class MapPlayers
{
	public MapPlayers(
		IEnumerable<MiniYamlNode> playerDefinitions,
		Color explicitDefaultColor);
}
```

`PlayerReference.Color` has no field initializer. Task 9's two characterized legacy constructors explicitly obtain the current manifest default and delegate; its two explicit-default overloads assign the supplied color before loading fields and never evaluate `Game.ModData`. Task 10 obtains the default through `context.ModData.Manifest.Get<DefaultPlayer>(context.ModData.ObjectCreator)` and uses it for every player parse/projection. The verified snapshot router constructs each projected reference through the explicit overload. Remote display metadata constructs no `PlayerReference` at all. Source-contract and behavior tests forbid the validator/verified/runtime call graph from selecting a legacy constructor, and a sentinel `Game.ModData` that throws proves direct, converted, runtime validation and verified MapCache routing remain successful without global access, including the absent-DefaultPlayer-module case.

Actor initializer parsing receives the same treatment:

```csharp
public class ActorReference
{
	public ActorReference(
		string type,
		Dictionary<string, MiniYaml> inits,
		ObjectCreator explicitObjectCreator);
}
```

The explicit overload captures only the supplied `ObjectCreator`, and lazy `InitDict` materialization resolves every `*Init` type through it. Existing constructors remain characterized desktop adapters that delegate with `Game.ModData.ObjectCreator`; the validator uses only the explicit overload from its pinned `Ra2MapValidationContext`. Verified MapCache routing uses already validated spawn/player metadata and constructs no `ActorReference`. Tests force lazy materialization for every actor/spawn/owner path while a throwing global sentinel is installed, cover unknown/duplicate initializers, and source-audit the full direct/converted/runtime validation call graph for the explicit overload.

Both engine `Map` constructors already resolve the grid explicitly through Task 9's `modData.Manifest.Get<MapGrid>(modData.ObjectCreator)` change. Every Task 10 `MapCache` lookup does the same; none calls the parameterless legacy `Manifest.Get<MapGrid>()` adapter. Tests exercise both the new-map/importer constructor and package-loading constructor under a throwing `Game.ModData` sentinel, then source-audit `Map.cs` and `MapCache.cs` for the explicit overload. This keeps the general desktop adapter characterized while making the complete direct/converted/runtime validation and verified-cache path independent of global state.

Map mutability is an explicit capability, not inferred from `MapClassification.User`. Retail verified snapshots set `CanDelete=false`; the runtime-download store owns deletion through its service; trusted desktop writable folders retain `CanDelete=true`. `MapPreview` propagates this capability and `MapChooserLogic` hides/disables Delete and Delete All unless every affected entry is deletable. The existing iOS feature policy continues hiding map-editor/save entry points. Tests cover mixed read-only/writable selections, delete-all filtering, direct `MapPreview.Delete` refusal without invalidating a read-only map, desktop legacy behavior, and no misleading iOS control.

`Ra2MapValidationService.ValidatePackageAsync` accepts only `VerifiedMapPackageInput`, a closed `Ra2MapValidationContexts`, `MapValidationDeadlineSet`, clock, and cancellation token; performs this complete pipeline; and is the only producer of `ValidatedMapArtifact`. After computing the strong `MapUid`, it sets `LogicalName` to `Uid.Value` exactly; caller/title/source names cannot override it. `CreateRetail` requires a borrowed Base context and accepts at most one borrowed Yuri context, canonicalizes them in Base/Yuri order, and rejects duplicates, null/default contexts, or any other profile. `CreateRuntime` requires exactly the currently active Base or Yuri profile/context and can therefore claim only that one compatibility bit. The service borrows every context for the invocation and never disposes it. `RetailCandidateValidator` opens the required pre-game scopes, calls `CreateRetail`, and disposes those contexts in reverse order in `finally`; the runtime store wraps the single context returned by its bound factory with `CreateRuntime` and likewise disposes it in `finally`.

The service opens and authenticates the untrusted package source once, bounds and snapshots the exact ZIP/YAML/BIN/PNG bytes once, and never reopens a picker, staging path, or generation filename. It may construct profile-specific `Map` instances sequentially from independent read views over that same immutable bounded snapshot, but it may not repeat envelope admission or observe different bytes between profiles. Each profile receives a fresh owned package read view. Production `IRa2MapConstructionFactory.Open` transfers that package into `Ra2ConstructedMap`; if `Map` construction throws it disposes the package immediately, and on success the wrapper always disposes `Map` (including `Sequences`) before the package. The service holds no constructed map after projecting immutable metadata. Its internal injected factory exists only for deterministic ownership tests and has no raw-path/source-opening capability. First-profile success plus second-profile failure, metadata mismatch, PNG/profile failure, timeout, cancellation, and unexpected exception tests each prove exact reverse-order disposal of every Map/package view; only the final immutable artifact and its separately specified source pin may survive.

Contexts are processed in canonical Base-then-Yuri order. The first successful profile supplies the artifact's single complete metadata projection; each later successful profile sets its bit only when its title/author/categories/tileset/grid/map-format/map-size/bounds/player/default-color/spawn projection is field-for-field equal after canonicalization. A mismatch clears only that later bit and produces the closed `ProfileMetadataMismatch` diagnostic for readiness/UI; it never lets one profile's metadata masquerade as another's. Thus Base+Yuri flags always imply one identical persistent projection, while a Base-failed/Yuri-success case legitimately stores Yuri metadata with only the Yuri bit. A profile-specific typed semantic failure likewise clears only that bit. Retail returns a typed invalid-map result only when neither profile succeeds, while candidate readiness separately requires at least one Base-compatible map. After all maps are known, `RetailCandidateValidator` computes both checked compatible-map counts and performs the final profile decision: Yuri requires a complete verified Yuri pair *and* `YuriCompatibleMapCount > 0`; otherwise it retains a structurally valid pair as inactive/Unavailable, selects Base, recomputes the Base contract/readiness, and exposes only Base-compatible maps. Runtime requires its sole current profile to succeed and never infers untested cross-profile compatibility. Tests cover Base-only, Base+Yuri, Yuri-only retail artifacts; complete Yuri archives with only a Base-only map; the same candidate with a Yuri-compatible map; Base/Yuri contexts whose default player, grid, map format, map size, bounds, or player projection differs; Base-current and Yuri-current runtime artifacts; one-profile failure isolation; source-open/envelope-admission exactly once; and canonical context order/ownership without double disposal.

A retail adapter uses the same transaction's sealed `RetailCandidateArtifactSource` to expose Task 9's converted artifact or a direct staged `.oramap`; a runtime adapter binds Task 12's random same-volume staging inode to its runtime transaction. Both expose only a canonical domain-relative descriptor and pinned verified stream—never a root or raw path—and recheck length/package digest through the same handle. `SourceSha256` and `ConverterVersion` are immutable provenance supplied alongside the package; direct/runtime inputs use package digest and converter version zero. The output's `CanonicalArtifactRelativePath` remains relative to the owning domain and is remapped explicitly into a retail candidate catalog or runtime generation; it is never accepted across domains. Retail uses `ForRetail` with one five-minute map deadline plus the enclosing 90-minute deadline. Runtime download uses `ForRuntime` with the same already-started five-minute deadline in both fields, so validation cannot refresh its budget. Every ZIP/YAML/PNG/profile loop calls `ThrowIfExpired`. One bad or timed-out map is quarantined without discarding valid sibling maps; the entire candidate fails only when no Base-compatible map remains or the whole-validation deadline expires. The artifact reports exact retained metadata/string counters before publication so Task 7 aggregate budgets and receipts never estimate or reparse them. Tests cover cross-domain descriptor rejection, disposed/changed pinned sources, context ownership, and proof that no validation overload accepts a string path or caller-defined retail source.

The validation contexts and `VerifiedMapPackageInput.Package` source in the API above are caller-owned and only borrowed for one invocation. Success, typed failure, cancellation, or exception never disposes them; the caller disposes every context and the domain adapter exactly once after the complete per-map operation, while the underlying sealed retail/runtime artifact source follows its longer transaction ownership. This is distinct from the per-profile package read views transferred into `Ra2ConstructedMap`, which the service always closes. Ownership tests reject service-side disposal of caller capabilities, caller leaks, and any surviving constructed Map/read view.

- [ ] **Step 2: Write RED lazy-cache and atlas-boundary tests**

Require metadata-only initial enumeration, one per-map bounded string interner, a 64 MiB CPU byte-accounted LRU, 128 MiB GPU-page budget, maximum source decode edge 4096, proportional thumbnail at most 1024, 2048 atlas page with margin 1, and a placeholder that does not make the map unplayable.

Add `IGraphicsContext.MaximumTextureSize`. `SheetBuilder` must use checked arithmetic and reject any allocation where `thumbnail edge + 2 * margin > min(2048, device maximum)`. Test edge `-1`, exact, `+1`, 4096-to-1024 scaling, device page below 2048, pinned eviction, rotation, memory warning, release/reupload, and no global `MapCache.StringPool` pollution.

Task 10 preserves and extends Task 3's already delivered MapCache lifecycle seam; it does not introduce a later API that Task 3's `Game`/`ModData` callers could not compile against. The new lazy CPU/GPU cache, atlas pages, verified-map preview sources, and memory-warning work all register with the existing `BeginPreviewDrainAsync` fence and are disposed by the existing game-thread `DisposePreviewResourcesOnGameThread` phase. The Task 3 legacy/system-map worker regression remains mandatory, and Task 10 adds the new verified-preview paths to the same source audit and same-mod/final-exit behavior matrix. No Task 10 path may reintroduce `Game.RunAfterTick`, `ThreadPool.QueueUserWorkItem`, `Thread.Join`, `.Wait()`, `.Result`, or `GetAwaiter().GetResult()`.

Task 6's `RetailMapResidentUsageMeterV1` is the sole receipt-domain meter and already authenticates every retail receipt before Task 10 exists. Task 10's engine-owned `VerifiedMapResidentUsageMeter` is the sole engine-entry meter; it independently implements the byte-identical `NHRM` schema `1` framing frozen in Task 6 over the exact engine projection, including `MapWidth` and `MapHeight`, and accepts no caller-provided counters. Neither implementation calls, references, reflects over, or serializes through the other assembly. `VerifiedMapEntry.Create` requires `logicalName == uid.Value`, defensively copies metadata, calls only the engine meter, stores the result, and only then takes ownership of the entry source. Task 10's RA2 validator first calls the Task 6 meter on `ValidatedMapMetadata` plus `MapFormat`, then projects every field into `VerifiedMapPreviewMetadata`, calls the engine meter, and requires exact equality of all three counters before `ValidatedMapArtifact.Create`; the artifact receives those measured values, never caller claims. Retail receipt codecs continue to reconstruct through Task 3 factories and call the Task 6 meter; runtime receipts reconstruct the engine projection and call the engine meter; cold recovery repeats the applicable domain comparison before creating an entry. Both test suites share literal expected byte-count vectors but use independent object builders and independent expected-byte writers, mutate every scalar/string/array/player/spawn field, use multibyte UTF-8 and duplicate interner values, exercise each counter `-1`/exact/`+1` and overflow, and include an RA2 cross-domain equality matrix. Tests prove no cross-assembly provider can make a large metadata object claim zero usage, omit MapSize, or substitute a display/source name.

Task 10 adds an engine-owned pure `VerifiedMapResidentBudget.ValidateUnion` helper over one or more immutable entry arrays. It checked-sums the engine-measured usage across the exact visible union, rejects duplicate UIDs/logical names, never counts one entry twice, and enforces the single V1 64 MiB / 32 MiB / 262,144 global caps without knowing a future runtime-store type. Task 10's one-provider `MapCache` route and synthetic two-provider tests use this helper. For a non-Legacy profiled manifest's exact `content|maps: User` row, construction records the location/classification but deliberately skips the legacy package enumeration path; no snapshot provider is required to exist yet because Task 3 service binding happens only after `MapCache` construction. Task 12 modifies `MapCache` again to compose the concrete retail/runtime providers through the same helper: a bad optional runtime candidate snapshot is withheld (the last verified runtime snapshot or no runtime maps remains visible) without hiding a valid retail snapshot, while a retail overflow closes readiness before publication. Tests cover retail-only exact cap, runtime-only exact cap, combined `limit - 1`/exact/`+ 1`, duplicate UID, checked overflow, profile filtering before the sum, and proof that rejection does not allocate/decode a preview or mutate the prior composed view.

- [ ] **Step 3: Run RED**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~BoundedOramapEnvelopeTest|FullyQualifiedName~Ra2MapValidationServiceTest|FullyQualifiedName~MapPreviewImageCacheTest|FullyQualifiedName~SheetBuilderBoundsTest|FullyQualifiedName~PngTest' \
  -v minimal
```

Expected: malformed packages/PNG/atlas `page + 1` are accepted or fail after allocation, and the new verified snapshot types are missing.

- [ ] **Step 4: Implement one verified-map path**

`Ra2OramapEnvelope` wraps Task 2's `BoundedZipEnvelope`; `Ra2MapValidationService` performs the pre- and post-validation and returns a complete `ValidatedMapArtifact`. It never calls `Map.Save` on a direct package and never publishes a partially compatible artifact.

`MapCache.UpdateFromVerifiedMap` consumes immutable bounded preview metadata without reparsing untrusted YAML. `MapCache` recognizes the exact non-Legacy profiled `content|maps: User` mapping during its normal MapFolders loop, registers only its location metadata, and never asks the mounted package for `Contents` or an entry stream; any other profiled raw/writable User mapping is a construction failure. The later provider Bind performs no subscription. It acquires exactly one `VerifiedMapSnapshotLease` from the immutable retail provider, attaches that lease's canonical entries once to the profiled placeholder through `UpdateFromVerifiedMap`, and owns the lease in its bind token. Unbind first removes that exact attached view and then disposes the lease; it neither triggers a fallback folder scan nor observes `SnapshotChanged`. A later same-mod reconstruction performs a fresh one-shot Acquire/attach against the same immutable provider. The provider retains the event required by `IVerifiedMapSnapshotProvider`, but the final retail registry never mutates or raises it after construction and MapCache does not subscribe. `VerifiedMapPreviewMetadata` defensively copies and preserves ordered categories and spawn coordinates, copies player definitions in their canonical name order, validates every bounded string/count/dimension/player reference, and contains no YAML node, source path, or mutable collection. Both metadata types are engine-owned bounded value projections needed by the RA2 validation assembly: each has a private constructor and exactly one public validating `TryCreate`, with no public/copy constructor, record `with`, init/setter, subclass, or mutable-array path. The factory validates all scalar, enum, player-relation, count, grid/map-size/bounds, and collection constraints and defensive-copies every collection before success; failure returns a null out value. Creating one of these values grants no receipt, validated-artifact, snapshot, root, lease, or Ready authority; only `Ra2MapValidationService` can pass them through the internal `ValidatedMapArtifact` success factory. The preview constructs a fresh `MapPlayers` solely through the engine-owned `VerifiedMapPlayerMetadata` projection, copying every lobby-facing `PlayerReference` field (including `AllowBots`, required/lock fields, faction/color/team/handicap/spawn, system-player flags, allies, and enemies); `SpawnPoints` is populated from the separately validated ordered array. Chooser, lobby, initial-map selection, and save setup therefore retain the exact validated semantics without a second parse. Tests cover every public-factory invalid cross-product, caller mutation after success, RA2 cross-assembly construction through only `TryCreate`, AllowBots false, every lock bit, system and playable players, map-size/bounds boundaries, ordered categories and spawn coordinates, allies/enemies, tracking-package zero enumeration/open before and after bind, zero provider subscriptions/callbacks, one Acquire and attach per Bind, detach-before-lease-dispose, fresh Acquire on same-mod rebind, one entry per UID, and prove no `MiniYaml`/package read occurs during snapshot routing. A Legacy desktop MapCache characterization still enumerates and loads its configured package/directory exactly as before.

The metadata constructor resolves an engine-owned `MapPreviewCachePolicy` through Task 3's sealed `ModData` service provider; the iOS bootstrap maps the complete `PngReadLimits` (edge, pixels, decoded bytes, chunks, IDAT bytes, and inflate ratio) plus every cache/atlas field from `IosImportEnvelopePolicyV1`, while absence selects characterized desktop trusted defaults. The policy owns one instance-scoped, thread-safe `MapPreviewBudgetLedger`; its constructor requires positive fixed limits equal to the policy's CPU/GPU limits. The cache accounts every CPU/GPU allocation and pin through owner-bound reservations from that ledger with checked arithmetic before allocation/upload, releases the exact token in `finally`/eviction/disposal, and cannot mutate counters directly. Zero/negative/over-limit acquisition, a foreign or disposed reservation, invalid CPU/GPU kind, double release, and pin transitions that would break `pinned <= total` fail without changing counters. `Capture` returns one atomic nonnegative snapshot with pinned values no greater than totals and totals no greater than the fixed `CpuLimit`/`GpuLimit`. Task 12 receives only its read-only provider view and checks the same live ledger at both quota gates; no iOS static or estimated preview value is accepted.

Each `AcquirePreviewSource` returns an independently owned pinned reference. `IMapPreviewImageSource.Decode` must receive that complete policy on every initial decode and later re-decode after release/memory warning; it returns a byte-accounted BGRA lease whose memory becomes inaccessible after disposal. Cross-assembly decoders create that lease only through `MapPreviewImageLease.Create`; it accepts an owned `IMemoryOwner<byte>`, requires checked `width * height * 4 == byteLength <= owned memory length`, disposes the owner on rejected construction, and transfers sole ownership on success. Preview decode occurs only when a widget requests a lease. The cache downsamples before atlas upload, releases the CPU lease after upload, evicts only unpinned pages, and rebuilds from a newly acquired verified immutable source. `TrimUnpinnedPreviews()` evicts unpinned CPU/GPU state only; Task 13's `IosRuntimeMapServices` forwards UIKit memory warnings to this exact method on the game thread. RED coverage tampers chunks/IDAT/ratio specifically on a post-warning re-decode and proves source/image lease order, invalid factory input disposal, cancellation, exact ledger accounting at `limit - 1`/exact/`+ 1`, concurrent pin/evict snapshots, defensive copies, no post-dispose memory access, and no generation-pin leak.

`RetailCandidateValidator` is the sole production implementation of Task 8's interface. Its internal constructor requires `RetailArchiveGraphValidator`, `RetailProfileValidator`, the internal `Ra2RuntimeContractBuilder`, `Ra2MapConversionService`, `Ra2MapValidationService`, a borrowed pre-game context, and the monotonic clock; it does not accept or call `IIosRuntimeResourcePreflight`. `ValidateAsync`/`ValidateRepairAsync` accept the borrowed operation identity immediately before the deadline, retain that exact reference only inside the returned one-shot wrapper, and pass their exact already-started deadline, constructor's same clock object, and same cancellation token through profile validation, conversion/map validation, and every pure `BuildExpected`; no nested component may start or refresh a budget. The coordinator remains the sole owner of `PreGameEngineContext`; the validator may open and dispose sequential per-validation `PreGameModDataScope` instances but neither stores the context beyond its own lifetime nor disposes it. Its `Dispose` cancels/cleans only owned scopes and helpers. It combines staged replacements with the authenticated preserved-active source, first validates required archives/audio/profile/mounts so the installer fallback is unreachable, then converts and validates every map, computes the complete expected contract/readiness, and returns only the bound `RetailValidatedCandidateInput` or `RetailValidatedRepairInput` operation result; it never returns separable validation/source/active/plan/operation authority. Candidate/catalog/receipt persistence stores the expectation's plain `Ra2RuntimeContract`, never a verified wrapper, operation identity, or engine identity. Its repair method applies exclusions to the authenticated active source and recomputes the profile/expectation/readiness under the same unrefreshed deadline. Static-resource verification is exclusively the Task 14 final post-retail-Ready gate and is never repeated inside candidate/repair validation. `Ra2MapValidationServiceTest` includes full-composition Base, optional-Yuri, optional-corruption, preserved-active, exact borrowed-operation `ReferenceEquals`, wrong-operation wrapper rejection at Prepare, timeout/cancellation between every profile/map/expected-contract checkpoint, retry with the still-live borrowed context, and repair cases; Task 14 constructs this exact concrete type and cannot inject an unimplemented callback. Separate Task 14 tests cover the sole static-preflight failure path.

`VerifiedMapSnapshotLease` owns an internal root pin and a defensively copied immutable entry array. Disposing the lease prevents new package/preview acquisition but already-open entry/package/preview references retain independent pins until their own disposal. Tests lock lease-first/entry-first/package-first disposal, concurrent preview references, final-pin GC eligibility, and no leaks on failure/cancellation.

Cross-assembly publication exposes no engine trust-producing factory. `IVerifiedMapEntrySource`, `VerifiedMapEntry.Create`, `VerifiedMapSnapshotPublisher`, and every publisher constructor/mutation are internal to OpenRA.Game. The existing OpenRA.Game friend boundary permits only `OpenRA.iOS` and tests to invoke them; no broad Game→RA2 friend is added. Task 14's iOS retail bridge and Task 13's iOS runtime bridge each implement a narrow RA2-internal interface: they accept only one exact RA2-owned, already-validated publication batch, adapt each still-pinned source into the internal Game source, create entries, and construct/publish the internal publisher on the game thread. RA2 can submit or receive only the opaque bridge-owned provider/lease; it never sees an entry factory, publisher, root pin, arbitrary source interface, or mutation method. Task 12's runtime bridge additionally is the only caller of internal Game download/recovery result factories and the only caller allowed to take/commit a prepared admission. `VerifiedMapSnapshotPublisher` takes ownership of the canonical immutable entry array, constructs/swaps the internal ref-counted root, and raises `SnapshotChanged`; publish, clear, or disposal off the game thread fails. The root swap is the notification commit point: it snapshots the multicast invocation list, calls every subscriber exactly once in subscription order, catches each observer exception independently, emits only a fixed engine diagnostic, and continues. Observer code can never make an already completed root swap report failure or prevent later observers from running. Old root/entry sources are disposed only after every old lease/package/preview reference releases. Reflection/source-contract tests prove ordinary mod code cannot implement the source, construct an entry/publisher, publish/clear a root, invoke an internal result factory, or substitute either bridge; controlled iOS/Test bridge fixtures cover caller mutation, double publication ownership, construction-thread confusion, cross-thread publication, source leaks, observer exceptions/reentrancy/unsubscribe, and exact root/pin cleanup.

- [ ] **Step 5: Run GREEN, full tests, builds, and commit**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~BoundedOramapEnvelopeTest|FullyQualifiedName~Ra2MapValidationServiceTest|FullyQualifiedName~MapPreviewImageCacheTest|FullyQualifiedName~SheetBuilderBoundsTest|FullyQualifiedName~PngTest' \
  -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Game/OpenRA.Game.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj -c Release --no-restore -m:1
git diff --check
```

Require every command to exit zero. Commit only the 25 listed outer/engine paths:

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 10 --phase implementation
git commit -m "Harden ORAMAP validation and preview caching"
```

- [ ] **Step 6: Generate and verify `0024` immediately**

Build from official+`0001`...`0023` with the exact 21-path engine allowlist:

```bash
set -euo pipefail
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
PATCH_STAGE="$(mktemp -d /private/tmp/nuke-hour-engine-patch-0024.XXXXXX)"
ALLOWLIST="$PATCH_STAGE/0024.allowlist"
VERIFY_DIR="$PATCH_STAGE/verification"
PREDECESSOR_END=23
CANDIDATE=engine-patches/0024-verified-oramap-map-cache.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0024 --output "$ALLOWLIST"
LC_ALL=C sort -c -u "$ALLOWLIST"
test "$(wc -l < "$ALLOWLIST" | tr -d ' ')" -eq 21
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
test -f "$VERIFY_DIR/evidence.json"
```

The self-contained block above runs candidate generation and the independent verifier. Commit the three delivery paths only after fresh replay:

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 10 --phase delivery
git commit -m "Deliver verified ORAMAP engine patch"
```

---

### Task 11: Bound remote map metadata before parsing and deliver `0025`

**Exact `0025` engine allowlist:**

- Create: `engine/OpenRA.Game/Map/RemoteMapMetadata.cs`
- Create: `engine/OpenRA.Game/Map/VerifiedRemoteMapMetadataClient.cs`
- Modify: `engine/OpenRA.Game/Map/MapCache.cs`
- Modify: `engine/OpenRA.Game/Map/MapPreview.cs`
- Create: `engine/OpenRA.Test/VerifiedRemoteMapMetadataClientTest.cs`
- Create: `engine/OpenRA.Test/MapCacheRemoteMetadataTest.cs`

**Delivery files:**

- Create: `engine-patches/0025-verified-remote-map-metadata.patch`
- Modify: `engine-patches/README.md`
- Modify: `packaging/tests/test_verify_engine_patch_delivery.py`

- [ ] **Step 1: Write RED byte-scanner and publication tests**

```csharp
public interface IRemoteMapMetadataTransport
{
	Task<RemoteMapMetadataResponse> OpenAsync(
		Uri uri,
		CancellationToken cancellationToken);
}

public sealed class RemoteMapMetadataResponse : IAsyncDisposable
{
	private RemoteMapMetadataResponse(Stream ownedBody);
	public Stream Body { get; }
	public static RemoteMapMetadataResponse Create(Stream ownedBody);
	public ValueTask DisposeAsync();
}

public interface IRemoteMapMetadataClock
{
	long GetTimestamp();
	TimeSpan GetElapsedTime(long startTimestamp, long endTimestamp);
}

public interface IGameThreadDispatcher : IGameThreadAffinity
{
	Task InvokeAsync(Action action, CancellationToken cancellationToken);
}

public enum RemoteMapMetadataFailureCode
{
	None,
	Cancelled,
	TransportUnavailable,
	TimedOut,
	ResponseLimitExceeded,
	MalformedRecord,
	UnexpectedUid,
	DuplicateUid,
	FieldLimitExceeded,
	InvalidMinimap,
	CacheLimitExceeded,
}

public sealed class VerifiedRemoteMapPreviewMetadata
{
	private VerifiedRemoteMapPreviewMetadata(/* exact bounded fields */);
	public string Title { get; }
	public string Author { get; }
	public ImmutableArray<string> Categories { get; }
	public int PlayablePlayers { get; }
	public ImmutableArray<CPos> SpawnPoints { get; }
	public int BoundsX { get; }
	public int BoundsY { get; }
	public int BoundsWidth { get; }
	public int BoundsHeight { get; }
	public MapGridType GridType { get; }
	public string Tileset { get; }
	public int MapFormat { get; }
	public bool Downloading { get; }
	public string GameMod { get; }
	internal static VerifiedRemoteMapPreviewMetadata Create(
		/* exact validated bounded fields */);
}

public sealed class RemoteMapMetadataRecord
{
	internal RemoteMapMetadataRecord(
		MapUid uid,
		VerifiedRemoteMapPreviewMetadata metadata,
		ImmutableArray<byte> validatedMinimapPng);
	public MapUid Uid { get; }
	public VerifiedRemoteMapPreviewMetadata Metadata { get; }
	public ImmutableArray<byte> ValidatedMinimapPng { get; }
}

public sealed class RemoteMapMetadataBatch
{
	public ImmutableSortedDictionary<MapUid, RemoteMapMetadataRecord> Records { get; }
	public ImmutableSortedDictionary<MapUid, RemoteMapMetadataFailureCode> Failures { get; }
	public RemoteMapMetadataFailureCode BatchFailure { get; }
	private RemoteMapMetadataBatch(
		ImmutableSortedDictionary<MapUid, RemoteMapMetadataRecord> records,
		ImmutableSortedDictionary<MapUid, RemoteMapMetadataFailureCode> failures,
		RemoteMapMetadataFailureCode batchFailure);
	internal static RemoteMapMetadataBatch Complete(
		ImmutableSortedDictionary<MapUid, RemoteMapMetadataRecord> records,
		ImmutableSortedDictionary<MapUid, RemoteMapMetadataFailureCode> failures);
	internal static RemoteMapMetadataBatch Failed(
		RemoteMapMetadataFailureCode failure);
}

public sealed record RemoteMapMetadataLimits(
	long MaximumResponseBytes,
	int MaximumRequestedUids,
	int MaximumRecords,
	long MaximumRecordBytes,
	int MaximumOrdinaryLineBytes,
	int MaximumRecognizedBinaryLineBytes,
	int MaximumNodes,
	int MaximumDepth,
	int MaximumFieldsPerRecord,
	int MaximumScalarBytes,
	int MaximumEncodedBinaryBytes,
	int MaximumDecodedBinaryBytes,
	int MaximumMinimapEdge,
	long MaximumMinimapPixels,
	int MaximumMinimapDecodedBytes,
	int MaximumPlayers,
	int MaximumSpawns,
	int MaximumCategories,
	TimeSpan Timeout);

public sealed record RemoteMapPreviewCachePolicy(
	int MaximumRecords,
	long MaximumRetainedBytes,
	long MaximumStringBytes,
	int MaximumStringEntries);

public interface IRemoteMapMetadataClient
{
	RemoteMapPreviewCachePolicy CachePolicy { get; }
	Task<RemoteMapMetadataBatch> QueryAsync(
		Uri repository,
		ImmutableArray<MapUid> requestedUids,
		CancellationToken cancellationToken);
}

public sealed class VerifiedRemoteMapMetadataClient : IRemoteMapMetadataClient
{
	public VerifiedRemoteMapMetadataClient(
		IRemoteMapMetadataTransport transport,
		IRemoteMapMetadataClock clock,
		PngReadLimits remoteMinimapPng,
		RemoteMapMetadataLimits limits,
		RemoteMapPreviewCachePolicy cachePolicy,
		ImmutableArray<string> mapCompatibility);
	public RemoteMapPreviewCachePolicy CachePolicy { get; }
	public Task<RemoteMapMetadataBatch> QueryAsync(
		Uri repository,
		ImmutableArray<MapUid> requestedUids,
		CancellationToken cancellationToken);
}
```

`RemoteMapMetadataLimits` is an immutable engine type in `RemoteMapMetadata.cs` with exact fields for 4 MiB response, 256 requested UIDs, 256 records, 3 MiB record, 64 KiB ordinary line, 2.25 MiB recognized-binary line, 16384 nodes, depth 32, 64 fields, 16 KiB scalar, 2 MiB encoded field, 1.5 MiB decoded field, 1024/1048576/4 MiB minimap, 64 players, 8 spawns, 64 categories, and a 30-second deadline. The separate immutable `RemoteMapPreviewCachePolicy` carries the **global retained-cache ceiling of 512 records / 16 MiB metadata-plus-encoded-preview bytes / 4 MiB untrusted UTF-8 string bytes / 32,768 ordinal-unique string entries**. The iOS bootstrap maps Task 1's V1 constants into both records; engine code never references the RA2 assembly. `IRemoteMapMetadataClient.CachePolicy` exposes the exact defensive-copied policy to `MapCache`, so it never downcasts the concrete client, reads iOS constants, or invents a static limit. Construction rejects zero/negative/overflowing fields and Task 13 source-contract tests require every V1 value to map exactly once. It separately constructs `remoteMinimapPng` as edge 1024, pixels 1,048,576, decoded pixels 4 MiB, chunks 4,096, IDAT at most the 1.5 MiB decoded-binary cap, and inflate ratio 100. The client rejects a mismatched or looser PNG policy—including Task 10's 4096-edge/64 MiB map policy—so the two trust domains cannot be confused. `RemoteMapMetadataBatch` defensively copies into ordinal `MapUid`-sorted immutable dictionaries, rejects null/duplicate/overlapping record and failure keys, and allows `BatchFailure != None` only with no records; a per-record failure leaves valid sibling records intact. `ValidatedMinimapPng` is empty when absent or an owned immutable copy no larger than the **1.5 MiB Base64-decoded binary limit** that has already passed the exact remote PNG policy; the 2 MiB value applies only to the encoded Base64 field. No caller buffer mutation can change the batch. Constructor-invariant tests mutate every source collection/byte array and cover disjointness, ordering, batch/per-record failure combinations, closed unknown enum values, exact 1.5 MiB ±1 binary size, explicit rejection of the normal map PNG policy, and every retained-cache aggregate at `limit - 1`/exact/`+ 1` with checked overflow.

Tests enforce: 4 MiB response; one 30-second absolute monotonic deadline shared by transport streaming, byte scanning, field decoding, PNG validation, and dispatch preparation; at most 256 requested/received UID records; 3 MiB record; ordinary line 64 KiB; only exact `minimap`, `players_block`, and `rules` keys at the correct depth may use the 2.25 MiB encoded-line ceiling; duplicate/lookalike/wrong-depth binary keys fail that record. The scanner removes those fields before MiniYaml. `rules` and `players_block` are never decoded or instantiated on iOS. `minimap` alone may decode up to 2 MiB encoded/1.5 MiB decoded and then passes a 1024-edge/4 MiB PNG policy. A fake monotonic clock proves timeout before allocation/publication and sibling-record isolation. A successful transport call transfers the response/body exclusively to the client; the client immediately uses `await using` and disposes it exactly once on success, transport error, limit rejection, malformed sibling/batch, timeout, cancellation, and queued-dispatch failure. A failed `OpenAsync` transfers nothing. Tracking async streams prove no read/response handle leaks and no double disposal. `IGameThreadDispatcher` is the one engine-neutral publication seam shared with Task 12; tests prove inline-on-game-thread, queued-off-thread, cancellation-before-run, exception propagation, and exactly-once invocation.

Accept only the nonbinary fields `title`, `author`, `categories`, `players`, `bounds`, `spawnpoints`, `map_grid_type`, `downloading`, `tileset`, `mapformat`, and `game_mod`; require canonical requested UID binding and reject duplicate/unsolicited UIDs. `map_grid_type` must parse directly into the closed `MapGridType` enum; `game_mod` is a bounded canonical token checked against the current manifest's immutable `MapCompatibility` set. A record with `Downloading == false` or incompatible `GameMod` is display-only `Unavailable`, never exposes Install/Join, and cannot publish to the runtime store. Tests cover both values independently, lookalike/case variants, a compatible enabled record, and prove no field is parsed then discarded.

Parsing occurs off-thread; mutation of MapCache occurs only through the game-thread dispatcher. `MapCache` resolves `IRemoteMapMetadataClient` and `IGameThreadDispatcher` together from its sealed `ModData` services; presence of one without the other is a construction failure, and no static/current-thread fallback exists. Desktop with neither service preserves its characterized path. Task 13/14 register the concrete dispatcher before the client/store, so every verified remote result reaches MapCache through `InvokeAsync`. Tests inject both, either one alone, an off-thread completion, and disposal during a queued callback.

`MapCache` owns one instance-scoped, game-thread-only remote-preview LRU measured from the canonical immutable record projection; it never accumulates every successful query in the general UID cache. Before admitting a batch it checked-measures the complete proposed cache against all four retained-cache ceilings. Currently visible, selected, joining, or download-in-flight records hold explicit pins; only unpinned least-recently-used records may be evicted. If the proposed batch cannot fit after safe eviction, its affected records receive the closed `CacheLimitExceeded` status and the previous cache remains unchanged. Eviction drops the encoded minimap and untrusted metadata/string references, renders a bounded placeholder, and permits a later explicit/on-screen query through the same client; it never silently treats evicted display metadata as authoritative. UIKit memory warnings are forwarded on the game thread and trim every unpinned remote record in addition to Task 10 preview pixels. Tests stream many individually valid batches across each aggregate boundary, pin the first/middle/last record, exercise selection/download races and replacement of the same UID, prove exact accounting and deterministic LRU order, and verify warning/eviction releases buffers while an active pin remains readable. Remote encoded bytes and strings are not charged to the retail/runtime authoritative-map resident counters, so this separate fixed ledger is mandatory rather than a second path around those caps.

`VerifiedRemoteMapPreviewMetadata` is deliberately display-only and separate from Task 10's authoritative `VerifiedMapPreviewMetadata`: it may supply a bounded player count and ordered spawn coordinates for search UI, but it can neither create `MapPlayers`/lobby slots nor mark a map playable. It is a nominal get-only type with one internal exact factory; `RemoteMapMetadataRecord` likewise has only an internal validating constructor and defensively copies its PNG bytes. Neither has a public/copy constructor, init/with/subclass path, or mutable collection. No remote metadata can create custom rules or authoritative player definitions. After download, only Task 10's fully validated package metadata may replace the remote preview and enter the lobby/runtime snapshot.

- [ ] **Step 2: Run RED**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~VerifiedRemoteMapMetadataClientTest|FullyQualifiedName~MapCacheRemoteMetadataTest' \
  -v minimal
```

Expected: current remote YAML/Base64/Png construction happens without the new response/line/UID limits and new client symbols are missing.

- [ ] **Step 3: Implement bounded scan, immutable result, and optional injection**

The verified client returns immutable metadata/failure dictionaries and never holds MapCache. Resolve it through Task 3's sealed `ModData` service provider only when injected by the iOS bootstrap; when absent, desktop follows its existing remote path byte-for-byte. `MapCache.QueryRemoteMapDetails` dispatches a verified batch to `UpdateFromVerifiedRemoteMetadata` on the game thread. Invalid records receive typed, user-safe failure codes and do not cancel valid sibling records.

- [ ] **Step 4: Run GREEN, full tests, and commit**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~VerifiedRemoteMapMetadataClientTest|FullyQualifiedName~MapCacheRemoteMetadataTest' \
  -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Game/OpenRA.Game.csproj -c Release --no-restore -m:1
git diff --check
```

Require every command to exit zero. Commit the six engine paths:

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 11 --phase implementation
git commit -m "Bound remote map metadata parsing"
```

- [ ] **Step 5: Generate and verify `0025` immediately**

Use official+`0001`...`0024` and the exact six-path allowlist:

```bash
set -euo pipefail
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
PATCH_STAGE="$(mktemp -d /private/tmp/nuke-hour-engine-patch-0025.XXXXXX)"
ALLOWLIST="$PATCH_STAGE/0025.allowlist"
VERIFY_DIR="$PATCH_STAGE/verification"
PREDECESSOR_END=24
CANDIDATE=engine-patches/0025-verified-remote-map-metadata.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0025 --output "$ALLOWLIST"
LC_ALL=C sort -c -u "$ALLOWLIST"
test "$(wc -l < "$ALLOWLIST" | tr -d ' ')" -eq 6
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
test -f "$VERIFY_DIR/evidence.json"
```

After the self-contained candidate/verifier block passes, commit only patch/README/verifier test:

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 11 --phase delivery
git commit -m "Deliver remote map metadata engine patch"
```

---

### Task 12: Add a verified runtime-map snapshot store and deliver `0026`

**Outer files:**

- Create: `OpenRA.Mods.RA2/Content/Maps/VerifiedRuntimeMapStore.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/RuntimeMapGenerationStore.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/RuntimeMapReceipt.cs`

**Exact `0026` engine allowlist:**

- Create: `engine/OpenRA.Game/Map/RuntimeMapDownload.cs`
- Modify: `engine/OpenRA.Game/Map/VerifiedMapSnapshot.cs`
- Modify: `engine/OpenRA.Game/Map/MapCache.cs`
- Modify: `engine/OpenRA.Game/Map/MapPreview.cs`
- Create: `engine/OpenRA.Test/VerifiedRuntimeMapStoreTest.cs`
- Create: `engine/OpenRA.Test/RuntimeMapStoreRecoveryTest.cs`
- Create: `engine/OpenRA.Test/MapPreviewRuntimeInstallTest.cs`

**Delivery files:**

- Create: `engine-patches/0026-verified-runtime-map-store.patch`
- Modify: `engine-patches/README.md`
- Modify: `packaging/tests/test_verify_engine_patch_delivery.py`

- [ ] **Step 1: Write RED queue, stream, UID, snapshot, and recovery tests**

```csharp
public enum RuntimeMapDownloadPhase
{
	Queued,
	Downloading,
	Validating,
	Publishing,
	Ready,
	Cancelled,
	Failed,
}

public sealed record RuntimeMapDownloadRequest(
	Uri Repository,
	MapUid RequestedUid,
	RuntimeContractIdentity RuntimeContract);

public sealed record RuntimeMapStoreLimits(
	int MaximumMaps,
	long CurrentGenerationBytes,
	long CurrentAndRollbackBytes,
	int MaximumQueuedDownloads,
	long StagingReservationBytes,
	long ResponseBytes,
	long MaximumResidentMetadataBytes,
	long ReservedRetailMetadataBytes,
	long MaximumResidentStringBytes,
	long ReservedRetailStringBytes,
	int MaximumResidentStringEntries,
	int ReservedRetailStringEntries,
	long MaximumPerMapStringBytes,
	long PreviewCpuBytes,
	long PreviewGpuBytes,
	long DiskSafetyMarginBytes,
	TimeSpan DownloadAndValidationTimeout);

public interface IRuntimeMapDownloadTransport
{
	Task<RuntimeMapDownloadResponse> OpenAsync(Uri repository, MapUid uid,
		CancellationToken cancellationToken);
}

public sealed class RuntimeMapDownloadResponse : IAsyncDisposable
{
	private RuntimeMapDownloadResponse(
		Stream ownedBody,
		long? declaredLength,
		string contentDisposition);
	public Stream Body { get; }
	public long? DeclaredLength { get; }
	public string ContentDisposition { get; }
	public static RuntimeMapDownloadResponse Create(
		Stream ownedBody,
		long? declaredLength,
		string contentDisposition);
	public ValueTask DisposeAsync();
}

public interface IRa2RuntimeMapContextFactory :
	IDisposable, IModDataBoundService
{
	RetailOperationResult<Ra2MapValidationContext> Open(
		RuntimeContractIdentity contract,
		RetailValidationDeadline deadline,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken);
	void Dispose();
}

public sealed class Ra2RuntimeMapContextFactory :
	IRa2RuntimeMapContextFactory, IModDataBoundService
{
	internal Ra2RuntimeMapContextFactory(
		TrustedManifestSelection selection,
		Ra2RuntimeProfileId profile,
		RuntimeContractIdentity expectedContract);
	public IDisposable Bind(ModData modData);
	public RetailOperationResult<Ra2MapValidationContext> Open(
		RuntimeContractIdentity contract,
		RetailValidationDeadline deadline,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken);
	public void Dispose();
}

public sealed class RuntimeMapGenerationStore : IDisposable
{
	internal RuntimeMapGenerationStore(
		string fixedRuntimeMapRoot,
		IRetailStoragePolicy storagePolicy,
		IRetailDirectoryDurability durability);
	public void Dispose();
}

public sealed class RuntimeMapReceipt
{
	internal RuntimeMapReceipt(
		MapUid uid,
		string logicalName,
		string canonicalGenerationRelativePath,
		string sourceSha256,
		string packageSha256,
		long compressedBytes,
		Ra2MapCompatibleProfiles compatibleProfiles,
		ValidatedMapMetadata metadata,
		int envelopeVersion,
		int converterVersion,
		int mapFormat,
		int validatorVersion,
		long retainedMetadataBytes,
		long retainedStringBytes,
		int retainedStringEntries);
	public MapUid Uid { get; }
	public string LogicalName { get; }
	public string CanonicalGenerationRelativePath { get; }
	public string SourceSha256 { get; }
	public string PackageSha256 { get; }
	public long CompressedBytes { get; }
	public Ra2MapCompatibleProfiles CompatibleProfiles { get; }
	public ValidatedMapMetadata Metadata { get; }
	public int EnvelopeVersion { get; }
	public int ConverterVersion { get; }
	public int MapFormat { get; }
	public int ValidatorVersion { get; }
	public long RetainedMetadataBytes { get; }
	public long RetainedStringBytes { get; }
	public int RetainedStringEntries { get; }
}

public sealed class RuntimeMapGenerationReference
{
	internal RuntimeMapGenerationReference(
		string generationId,
		long epoch,
		string commitId,
		string receiptSha256);
	public string GenerationId { get; }
	public long Epoch { get; }
	public string CommitId { get; }
	public string ReceiptSha256 { get; }
}

public sealed class RuntimeMapGenerationReceipt
{
	internal RuntimeMapGenerationReceipt(
		int schemaVersion,
		long epoch,
		string generationId,
		string commitId,
		RuntimeMapGenerationReference rollback,
		RuntimeContractIdentity runtimeContract,
		ImmutableArray<RuntimeMapReceipt> maps,
		MapResidentUsage mapResidentUsage);
	public int SchemaVersion { get; }
	public long Epoch { get; }
	public string GenerationId { get; }
	public string CommitId { get; }
	public RuntimeMapGenerationReference Rollback { get; }
	public RuntimeContractIdentity RuntimeContract { get; }
	public ImmutableArray<RuntimeMapReceipt> Maps { get; }
	public MapResidentUsage MapResidentUsage { get; }
}

public enum RuntimeMapPublishState
{
	Prepared,
	Publishing,
	Committed,
}

public sealed class RuntimeMapPublishJournal
{
	internal RuntimeMapPublishJournal(
		int schemaVersion,
		RuntimeMapPublishState state,
		long epoch,
		RuntimeMapGenerationReference previous,
		string proposedGenerationId,
		string commitId,
		RuntimeContractIdentity runtimeContract);
	public int SchemaVersion { get; }
	public RuntimeMapPublishState State { get; }
	public long Epoch { get; }
	public RuntimeMapGenerationReference Previous { get; }
	public string ProposedGenerationId { get; }
	public string CommitId { get; }
	public RuntimeContractIdentity RuntimeContract { get; }
}

public enum RuntimeMapDownloadFailureCode
{
	None,
	Cancelled,
	TransportUnavailable,
	InvalidDeclaredLength,
	ResponseTooLarge,
	ResponseLengthMismatch,
	TimedOut,
	InvalidPackage,
	UidMismatch,
	QuotaExceeded,
	RecoveryRequired,
	PublishFailed,
}

public readonly struct RuntimeMapDownloadProgress :
	IEquatable<RuntimeMapDownloadProgress>
{
	public MapUid Uid { get; }
	public RuntimeMapDownloadPhase Phase { get; }
	public long CompletedBytes { get; }
	public long? TotalBytes { get; }
	public RuntimeMapDownloadProgress(
		MapUid uid,
		RuntimeMapDownloadPhase phase,
		long completedBytes,
		long? totalBytes);
	public int? Percentage => TotalBytes is > 0
		? (int)Math.Min(100m,
			(decimal)CompletedBytes * 100m / TotalBytes.Value)
		: null;
	public bool Equals(RuntimeMapDownloadProgress other);
	public override bool Equals(object obj);
	public override int GetHashCode();
	public static bool operator ==(
		RuntimeMapDownloadProgress left,
		RuntimeMapDownloadProgress right);
	public static bool operator !=(
		RuntimeMapDownloadProgress left,
		RuntimeMapDownloadProgress right);
}

public sealed class RuntimeMapDownloadResult
{
	private RuntimeMapDownloadResult(
		MapUid uid,
		bool published,
		RuntimeMapDownloadFailureCode failure);
	public MapUid Uid { get; }
	public bool Published { get; }
	public RuntimeMapDownloadFailureCode Failure { get; }
	internal static RuntimeMapDownloadResult Success(MapUid uid);
	internal static RuntimeMapDownloadResult Failed(
		MapUid uid,
		RuntimeMapDownloadFailureCode failure);
	internal static RuntimeMapDownloadResult PublishedRecoveryRequired(
		MapUid uid);
}

public enum RuntimeMapStoreRecoveryDisposition
{
	Missing,
	Ready,
	RestoredPrevious,
	Quarantined,
	ResetRequired,
	UpdateRequired,
}

public enum RuntimeMapStoreRecoveryFailureCode
{
	None,
	CorruptJournal,
	CorruptPointer,
	ColdHashMismatch,
	StorageUnavailable,
	InterruptedPublish,
	CombinedAdmissionFailed,
	InvalidRecoveryState,
}

public sealed class RuntimeMapStoreRecoveryResult
{
	private RuntimeMapStoreRecoveryResult(
		RuntimeMapStoreRecoveryDisposition disposition,
		RuntimeMapStoreRecoveryFailureCode failure);
	public RuntimeMapStoreRecoveryDisposition Disposition { get; }
	public RuntimeMapStoreRecoveryFailureCode Failure { get; }
	internal static RuntimeMapStoreRecoveryResult Exact(
		RuntimeMapStoreRecoveryDisposition disposition,
		RuntimeMapStoreRecoveryFailureCode failure);
}

public enum VerifiedRuntimeMapAdmissionFailure
{
	None,
	ProviderMismatch,
	ProfileMismatch,
	DuplicateUid,
	DuplicateLogicalName,
	MetadataLimit,
	StringBytesLimit,
	StringEntriesLimit,
	ArithmeticOverflow,
}

public sealed class VerifiedRuntimeMapAdmissionResult : IDisposable
{
	private VerifiedRuntimeMapAdmissionResult(
		bool succeeded,
		VerifiedRuntimeMapAdmissionLease admission,
		VerifiedRuntimeMapAdmissionFailure failure);
	public bool Succeeded { get; }
	public VerifiedRuntimeMapAdmissionFailure Failure { get; }
	internal static VerifiedRuntimeMapAdmissionResult Success(
		VerifiedRuntimeMapAdmissionLease ownedAdmission);
	internal static VerifiedRuntimeMapAdmissionResult Failed(
		VerifiedRuntimeMapAdmissionFailure failure);
	public VerifiedRuntimeMapAdmissionLease TakeAdmission();
	public void Dispose();
}

public interface IVerifiedRuntimeMapAdmissionCoordinator
{
	VerifiedRecoveredRuntimeMapBindingResult ConfirmRecovered(
		IVerifiedMapSnapshotProvider recoveredRuntimeSnapshots);
	VerifiedRuntimeMapAdmissionResult Prepare(
		ImmutableArray<VerifiedMapEntry> borrowedProposedRuntimeEntries);
}

public sealed class VerifiedRecoveredRuntimeMapBindingResult : IDisposable
{
	private VerifiedRecoveredRuntimeMapBindingResult(
		bool succeeded,
		IDisposable ownedBinding,
		VerifiedRuntimeMapAdmissionFailure failure);
	public bool Succeeded { get; }
	public VerifiedRuntimeMapAdmissionFailure Failure { get; }
	internal static VerifiedRecoveredRuntimeMapBindingResult Success(
		IDisposable ownedBinding);
	internal static VerifiedRecoveredRuntimeMapBindingResult Failed(
		VerifiedRuntimeMapAdmissionFailure failure);
	public IDisposable TakeBinding();
	public void Dispose();
}

internal interface IVerifiedRuntimeMapAdmissionState : IDisposable
{
	void Commit(
		VerifiedMapSnapshotPublisher runtimeSnapshots,
		ImmutableArray<VerifiedMapEntry> ownedRuntimeEntries);
}

public sealed class VerifiedRuntimeMapAdmissionLease : IDisposable
{
	private VerifiedRuntimeMapAdmissionLease(
		IVerifiedRuntimeMapAdmissionState ownedState);
	internal static VerifiedRuntimeMapAdmissionLease Create(
		IVerifiedRuntimeMapAdmissionState ownedState);
	internal void Commit(
		VerifiedMapSnapshotPublisher runtimeSnapshots,
		ImmutableArray<VerifiedMapEntry> ownedRuntimeEntries);
	public void Dispose();
}

public interface IRuntimeMapDownloadSubscriptionHandle : IDisposable
{
	MapUid Uid { get; }
	Task<RuntimeMapDownloadResult> Completion { get; }
	void Cancel();
}

public sealed class RuntimeMapDownloadSubscription : IDisposable
{
	private RuntimeMapDownloadSubscription(
		IRuntimeMapDownloadSubscriptionHandle ownedHandle);
	public MapUid Uid { get; }
	public Task<RuntimeMapDownloadResult> Completion { get; }
	public static RuntimeMapDownloadSubscription Create(
		IRuntimeMapDownloadSubscriptionHandle ownedHandle);
	public void Cancel();
	public void Dispose();
}

public interface IVerifiedRuntimeMapStore
{
	IVerifiedMapSnapshotProvider Snapshots { get; }
	RuntimeMapDownloadSubscription Enqueue(
		RuntimeMapDownloadRequest request,
		IProgress<RuntimeMapDownloadProgress> progress,
		CancellationToken cancellationToken);
}

public sealed class VerifiedRuntimeMapStore :
	IVerifiedRuntimeMapStore, IModDataBoundService,
	IModDataActivatedService, IDisposable
{
	internal VerifiedRuntimeMapStore(
		IRuntimeMapDownloadTransport transport,
		Ra2MapValidationService validator,
		IRa2RuntimeMapContextFactory contexts,
		IGameThreadDispatcher gameThread,
		IRuntimeMapEngineAuthorityBridge engineAuthority,
		RuntimeMapGenerationStore generations,
		IRetailMonotonicClock clock,
		IMapPreviewBudgetProvider previewBudgets,
		RuntimeMapStoreLimits limits);
	public IVerifiedMapSnapshotProvider Snapshots { get; }
	internal Task<RuntimeMapStoreRecoveryResult> RecoverAuthorizedAsync(
		RuntimeMapStoreAuthorization authorization,
		IVerifiedMapSnapshotProvider retailSnapshots,
		CancellationToken cancellationToken);
	public RuntimeMapDownloadSubscription Enqueue(
		RuntimeMapDownloadRequest request,
		IProgress<RuntimeMapDownloadProgress> progress,
		CancellationToken cancellationToken);
	public IDisposable Bind(ModData modData);
	public IModDataActivationToken Activate();
	public void Dispose();
}

internal sealed class RuntimeMapStoreAuthorization
{
	internal RuntimeContractIdentity RuntimeIdentity { get; }
	internal RuntimeMapStoreAuthorization(
		RuntimeContractIdentity runtimeIdentity,
		VerifiedRa2RuntimeContract verifiedContract,
		RetailReadyGenerationCapability finalCatalogReady);
}

internal interface IRuntimeMapEngineAuthorityBridge
{
	RuntimeMapDownloadResult DownloadSucceeded(MapUid uid);
	RuntimeMapDownloadResult DownloadFailed(
		MapUid uid,
		RuntimeMapDownloadFailureCode failure);
	RuntimeMapDownloadResult DownloadPublishedRecoveryRequired(MapUid uid);
	RuntimeMapStoreRecoveryResult RecoveryExact(
		RuntimeMapStoreRecoveryDisposition disposition,
		RuntimeMapStoreRecoveryFailureCode failure);
	Task<RetailOperationResult<IRuntimeMapPublishedSnapshot>>
		PublishAsync(
			RuntimeMapEnginePublicationBatch ownedBatch,
			IVerifiedRuntimeMapAdmissionCoordinator admission,
			IGameThreadDispatcher gameThread,
			CancellationToken cancellationToken);
}

internal interface IRuntimeMapPublishedSnapshot :
	IVerifiedMapSnapshotProvider, IAsyncDisposable
{
	void DisposeOnGameThread();
}

internal interface IRa2VerifiedMapSource : IDisposable
{
	IReadOnlyPackage OpenPackage();
	IMapPreviewImageSource AcquirePreviewSource();
}

internal sealed class RuntimeMapEnginePublicationItem : IDisposable
{
	internal RuntimeMapReceipt Receipt { get; }
	internal static RuntimeMapEnginePublicationItem Create(
		RuntimeMapReceipt authenticatedReceipt,
		IRa2VerifiedMapSource ownedSource);
	internal IRa2VerifiedMapSource TakeSource();
	public void Dispose();
}

internal sealed class RuntimeMapEnginePublicationBatch : IDisposable
{
	internal ImmutableArray<RuntimeMapEnginePublicationItem> Items { get; }
	internal static RuntimeMapEnginePublicationBatch Create(
		ImmutableArray<RuntimeMapEnginePublicationItem> ownedItems);
	public void Dispose();
}
```

`RuntimeMapGenerationStore` has no public constructor, root factory, or root/path getter. Its raw fixed-root constructor is internal and is callable in production only from Task 13's `IosRuntimeMapServices.Create` through the existing `OpenRA.iOS` friend assembly; `OpenRA.Test` uses the same internal seam for controlled fixtures. Public-API/source-contract tests require zero public string-root constructors and reject any second production call site. Wrong suffix, noncanonical root, and symlink-component fixtures still fail in the internal constructor before I/O.

`VerifiedRuntimeMapStore.Activate` returns the typed caller-owned activation token required by Task 3. It enables the queue exactly once only after every fallible validation completes. `BeginDrainAsync` synchronously rejects new enqueue/subscriber work, cancels work that has not crossed the non-cancellable commit boundary, and returns one idempotent fence that completes only when every worker, validation context, subscriber callback, and later dispatcher invocation from that activation epoch is finished. It never synchronously joins. While Game pumps the dispatcher FIFO, an operation already past the boundary is allowed to complete its pointer/admission/journal sequence exactly as the existing commit table requires. Only after the fence completes may token disposal perform nonblocking final teardown. The later bind token asserts quiescence, disposes any uncommitted admission, detaches the recovered runtime root, and releases the context-factory binding without waiting for a worker. A null token or throw-before-return is failure; the implementation completes all fallible checks before enabling work and synchronously self-rolls back any earlier inert state. Normal unload and construction rollback drain/dispose activation before bind, so runtime detaches while retail and dispatcher are still live.

The internal constructor above is the sole production composition. It requires the download transport, Task 10 validator, a contract-bound validation-context factory, the engine game-thread dispatcher, the already root-bound generation store, monotonic clock, the same instance-scoped preview-budget provider used by `MapCache`, the exact V1 limits, and one `IRuntimeMapEngineAuthorityBridge`. Task 13 implements that bridge inside its existing `ios/OpenRA.iOS/IosRuntimeMapServices.cs`, where the existing dual friend access permits calls to internal Game and RA2 APIs; no second production implementation or public bridge constructor exists. The bridge defensively consumes one exact batch, constructs every Game entry/publisher and prepares/commits admission on the game thread, and returns only an opaque provider; it also maps the closed download/recovery tuple through internal Game factories. If dispatch rejects before execution, no publisher is created. Failed or superseded provisional publications are released with awaited `DisposeAsync`; the final store-owned provider is released directly on the game thread after store drain through `DisposeOnGameThread`. A failed bridge call disposes every transferred source/result owner exactly once and publishes nothing. The store has no direct `VerifiedMapEntry.Create`, publisher, or internal result-factory call edge. The composition rejects preview limits that do not equal the provider ledger's fixed 64 MiB CPU / 128 MiB GPU caps. `Ra2RuntimeMapContextFactory` pins one fixed trusted manifest selection, profile, and expected runtime identity but no pre-game context or `ModData`. The runtime store is the registered instance-scoped service. Its `IModDataBoundService.Bind` runs only after the dispatcher and retail registry have bound. It first requires `modData.Selection == fixedSelection` and `modData.RuntimeContract == expectedContract`, then binds the factory to that exact loaded ModData, verifies the recovered store contract against the same public identity, and obtains that instance's `MapCache` as the sole `IVerifiedRuntimeMapAdmissionCoordinator`; it never resolves a static/current cache or infers identity from manifest filenames. `ConfirmRecovered(Snapshots)` is the one atomic initial-runtime attach operation: against the already attached retail provider it profile-filters both exact roots, validates duplicate and aggregate budgets, installs the combined index, proves the recovered provider/generation identity, and on success returns the caller-owned binding token that detaches only this runtime root. Failure changes no MapCache root and aborts ModData construction. `Bind` takes that binding exactly once from its owning result, retains it in composite unbind state, and leaves the queue inert. Only Task 3's later `IModDataActivatedService.Activate`, after every bind and six identity assertions succeed, enables the queue exactly once and returns its independently owned typed activation token. A default/legacy/profile/digest/selection mismatch, failed union, later bind failure, identity mismatch, null/throwing activation, or activation-token rollback never leaves the queue exposed. On normal unload or rollback, ModData first calls the store token's `BeginDrainAsync` and pumps only the dedicated lifecycle lane until all workers, contexts, subscribers, and future dispatches from that epoch are quiescent; it then disposes the token and only afterward disposes the bind token to release uncommitted admission, remove runtime while retail remains attached, and dispose the factory binding. The bind token never waits or joins and only verifies the completed drain invariant. No partially bound store is observable. Tests cover bridge substitution/factory visibility/result ownership, the exact Base/Yuri pair, same-profile wrong digest, right digest wrong selection, legacy/default values, reconstruction, null/throw at every activation ordinal, activation-before-bind misuse, activation/bind rollback, queue impossibility before activation/after drain, and the exact race where a pointer-replaced worker waits on `gameThread.InvokeAsync` while shutdown begins on that same game thread: the dedicated lane pump executes the reserved admission exactly once, the fence completes, and teardown follows the existing durable result table without deadlock. Source-contract tests reject `Join`, `.Wait()`, `.Result`, and `GetAwaiter().GetResult()` in the store and activation-token path.

Factory bind installs one ref-counted instance binding and rejects overlap, wrong identity/selection, use before bind, and new acquisition after unbind. Same-mod reconstruction must fully unbind the old store/factory before binding the new instance. Neither the factory nor its binding is separately exposed as a ModData consumer service.

`VerifiedRecoveredRuntimeMapBindingResult` has one closed cross-product: success privately owns one non-null binding and `Failure=None`; failure owns no binding and a non-`None` failure. Factories reject null/default/forged combinations. `TakeBinding` transfers the token exactly once only on success; result disposal releases an untaken token and is idempotent. `VerifiedRuntimeMapAdmissionResult` follows the same rule with `TakeAdmission`. A second attach, transfer, or stale/double detach cannot mutate either root. Tests retain aliases, dispose before/after take and activation and during later bind rollback, and prove every untaken admission/binding is released exactly once while the retail root remains attached.

`IRa2RuntimeMapContextFactory.Open` accepts the store's exact runtime identity plus the already-started five-minute deadline, the same monotonic clock, and cancellation token. It rejects a mismatched identity or missing binding before acquiring a pin, checks cancellation/deadline immediately before and after acquisition, releases a pin acquired during an elapsed/cancelled call, and returns a fresh caller-owned sequential `Ra2MapValidationContext` borrowing only that explicitly bound ModData; it never refreshes the budget and never reads or replaces `Game.ModData`. The runtime queue disposes that context in `finally` after Task 10 validation. On ModData unload the store activation's `BeginDrainAsync` synchronously rejects new context acquisition and its fence completes only after every existing context pin and possible later dispatcher action is released; the Game pump services admitted dispatcher work until that fence completes. The subsequent activation-token dispose is nonblocking, and the later binding token only verifies quiescence and detaches the factory without waiting. Final provider shutdown disposes store and factory exactly once. Tests advance the fake clock during pin acquisition, cancel before/after acquisition, reject pre-bind/post-unbind and wrong-contract opens, exercise sequential same-mod rebind, and prove context/store/factory disposal order with no `Game.ModData` access or synchronous join.

Every verified package source is likewise disposed after validation. `RuntimeMapGenerationStore` validates the injected fixed Application Support runtime root at construction, rejects symlink/noncanonical/wrong-suffix roots, owns all staging/generation/pointer operations, and exposes no root/path getter. Only Task 13 may supply that root, storage policy, and durability adapter; tests use a bounded fixture root through the same constructor. The RA2 queue implements `IRuntimeMapDownloadSubscriptionHandle` and transfers it through the engine-owned `RuntimeMapDownloadSubscription.Create`; the wrapper has no public mutable/default constructor, owns the handle, and guarantees idempotent cancel/dispose/exactly-once completion across the assembly boundary.

`RuntimeMapReceipt` is the lossless trust projection of Task 10's `ValidatedMapArtifact`: it preserves the typed UID, exact package/source hashes and size, canonical *generation-relative* package name, compatible profiles, complete bounded player/spawn/preview metadata, envelope/converter/map-format/validator versions, and retained metadata/string counters. Its `LogicalName` must equal `Uid.Value` and its generation-relative path must equal `maps/{Uid.Value}.oramap`; it never stores a title-derived name, staging path, provider URL, source path, or free-form error. Same UID/same digest deduplicates; same UID with differing bytes or metadata returns `UidMismatch`/`InvalidPackage` before preparation. `RuntimeMapGenerationReceipt` binds the ordinal-UID-sorted defensive-copied map set to one canonical generation/epoch/commit and exact runtime identity; its `MapResidentUsage` must equal the checked per-map sum. Its optional `Rollback` is null on first install or a complete immutable reference to the exact previous generation and that previous receipt's SHA-256. The publish journal carries the byte-identical `Previous` reference. The codec rejects self-reference, mismatched ID/epoch/commit/digest, a journal/receipt reference mismatch, noncanonical or future references, and any reference whose opened receipt does not hash and identify exactly; a missing already-GC'd rollback directory merely makes the optional alternate unavailable and never invalidates the independently valid active generation. Its versioned binary codec uses checked fixed-width/length-prefixed fields plus an outer SHA-256 checksum; it rejects null/default/duplicate UID or logical names, UID/logical/path disagreement, noncanonical paths/IDs/hashes, unknown enum/flag/version values, counter/aggregate overflow, trailing bytes, and any field above the V1 limits or above the runtime allowance remaining after the authenticated retail reservation. Constructor inputs cannot mutate the receipt after creation. Tests tamper every rollback-reference component, substitute another valid receipt, create a self/cycle reference, remove a GC-eligible rollback, and prove stable absent-journal recovery can authorize only the exact referenced generation.

C# 9 nullable-reference annotations remain disabled: `ContentDisposition` is runtime null only when the response header is absent; `Rollback`/`Previous` are null exactly for first-install/no-previous states and otherwise both identify the same authenticated generation; admission is non-null exactly when `Succeeded` and `Failure=None`. `RuntimeMapDownloadProgress` is a handwritten `readonly struct : IEquatable<T>` with explicit members/operators and a default rejected at queue boundaries. Private factories validate every null tuple, and response/admission ownership is released once on all failures.

Cold recovery runs before a bound validation context exists, so it never reparses map YAML or reconstructs trust from filenames. Internal `RecoverAuthorizedAsync` requires the exact `RuntimeMapStoreAuthorization` retained by the pair-created service set plus the exact final retail snapshot provider for this launch; it acquires a caller-owned retail lease and never accepts a public/caller-created runtime scalar or UID/counter summary. Recovery has two explicit phases. **Phase A—journal/byte recovery** authenticates pointer/journal/receipt linkage, opens each exact generation-relative regular file without following links, checks recorded length and a full pinned-handle SHA-256, validates the generation's own count/storage/metadata/string totals with checked arithmetic, and deterministically projects authenticated receipt metadata/counters into one RA2-owned runtime publication batch. The iOS authority bridge is the only code that may convert that batch into internal Game `VerifiedMapPreviewMetadata`/`VerifiedMapEntry` objects. The first table uses only this closed `ByteReceiptValid` predicate; it does not include retail-union compatibility and it publishes no runtime root.

**Phase B—combined admission** takes the exact candidate selected by Phase A, profile-filters it with the acquired retail lease, applies the remaining `maximum - reservedRetail` budgets, and calls `VerifiedMapResidentBudget.ValidateUnion`. If that selected runtime candidate conflicts with newly imported/repaired retail maps or exceeds a combined cap, recovery tries the other independently byte/receipt-valid generation under the same retail lease. A compatible alternate is atomically selected according to the second table. If neither candidate is union-compatible, the store journaledly moves the conflicting runtime pointer/generations to its fixed quarantine, fsyncs an empty current state, publishes an empty verified runtime snapshot, and returns `Quarantined/CombinedAdmissionFailed`; downloaded maps remain optional and the retail game may start and redownload them. It never reports `Ready` while hiding a disk-current runtime generation. Contract or schema/validator/envelope/converter/map-format drift returns the closed `UpdateRequired` disposition with no unverified snapshot. Tests round-trip every artifact field, mutate each encoded field/checksum/version/counter/path/contract, alter bytes at equal size, rename/symlink an entry, mutate caller collections, and prove recovered players/spawns/preview semantics are byte-for-byte equivalent to the originally validated artifact without invoking `Ra2MapValidationService`. Boundary tests cover retail reservation zero/exact global cap/cap+1, runtime remaining `-1`/exact/`+1`, per-map strings, same UID/logical-name collision after a retail update, compatible alternate selection, quarantine-to-empty, and recovery after the retail profile's map set changes across launches. Every branch disposes the retail/runtime leases exactly once.

Runtime publication uses the same-volume, versioned, checksummed `RuntimeMapPublishJournal`; its IDs are canonical opaque generation IDs and its codec rejects unknown states/versions, absolute or traversal-like values, epoch/contract mismatch, duplicate/torn/trailing bytes, and unauthenticated paths before any filesystem action. Every Phase-A row below requires the pointer generation/epoch/commit and referenced generation receipts to authenticate exactly; “other” includes an unrecognized target or linkage mismatch. Each result is written as the exact provisional `RuntimeMapStoreRecoveryDisposition/RuntimeMapStoreRecoveryFailureCode` pair. Phase A only computes that decision and pins every referenced byte/receipt-valid generation; it does not replace a pointer, publish a root, quarantine, discard, or GC anything. Action prose in its result column is deferred until Phase B either admits that selected candidate or chooses an explicitly authorized alternate. A provisional `Ready` or `RestoredPrevious` must pass Phase B before it is returned or any deferred action runs.

| Journal | Pointer | Independently byte/receipt-verified generations | Exact Phase-A result |
| --- | --- | --- | --- |
| absent | absent | none | `Missing/None`; keep the empty state |
| absent | active | active valid | `Ready/None`; keep active |
| absent | invalid/other | any | `ResetRequired/CorruptPointer` |
| `Prepared` | previous | previous valid | `RestoredPrevious/InterruptedPublish`; provisionally select previous; proposed is not publication-authorized |
| `Prepared` | previous | previous invalid | `ResetRequired/ColdHashMismatch`, even if proposed is valid; Prepared never authorizes a switch |
| `Prepared` | proposed/other/absent | previous valid | `RestoredPrevious/InterruptedPublish`; provisionally select previous; proposed is not publication-authorized |
| first-install `Prepared` | absent | proposed valid or invalid | `Missing/InterruptedPublish`; quarantine/discard the never-activated proposed generation |
| `Prepared` | proposed/other/absent | no valid previous and not the first-install row | `ResetRequired/InterruptedPublish` |
| `Publishing` | previous | previous valid | `RestoredPrevious/InterruptedPublish`; provisionally select previous and retain any byte-valid proposed pin for the authorization decision below |
| `Publishing` | previous | previous invalid, proposed valid | `Ready/InterruptedPublish`; atomically advance to proposed |
| `Publishing` | previous | both invalid | `ResetRequired/ColdHashMismatch` |
| `Publishing` | proposed | proposed valid | `Ready/InterruptedPublish`; confirm proposed |
| `Publishing` | proposed | proposed invalid, previous valid | `RestoredPrevious/ColdHashMismatch`; atomically restore previous |
| `Publishing` | proposed | proposed invalid, no valid previous | `ResetRequired/ColdHashMismatch` |
| `Publishing` | absent/other | previous valid | `RestoredPrevious/InterruptedPublish`; atomically restore previous |
| first-install `Publishing` | absent | proposed valid or invalid | `Missing/InterruptedPublish`; no pointer swap is inferred |
| `Publishing` | absent/other | no valid previous and not the first-install row | `ResetRequired/InterruptedPublish` |
| `Committed` | proposed | proposed valid | `Ready/None`; confirm proposed |
| `Committed` | proposed | proposed invalid, previous valid | `RestoredPrevious/ColdHashMismatch`; atomically restore previous |
| `Committed` | proposed | proposed invalid, no valid previous | `ResetRequired/ColdHashMismatch` |
| `Committed` | previous/absent/other | proposed valid | `Ready/InterruptedPublish`; atomically complete proposed publication |
| `Committed` | previous/absent/other | proposed invalid, previous valid | `RestoredPrevious/ColdHashMismatch`; keep/restore previous and quarantine proposed with a fixed warning |
| `Committed` | previous/absent/other | no valid generation | `ResetRequired/ColdHashMismatch` |
| corrupt fixed journal | independently authenticated active | active valid | `Ready/CorruptJournal`; quarantine journal and expose the fixed warning |
| corrupt fixed journal | absent/invalid active | none | `ResetRequired/CorruptJournal` |
| future journal/receipt version | any | any | `UpdateRequired/None`; do not mutate generations |
| any unmatched or ambiguous combination | any | any | `ResetRequired/InvalidRecoveryState`; there is no fallthrough guess |

Phase A also emits a non-forgeable recovery-candidate capability. Byte validity alone never authorizes an alternate:

| Authenticated Phase-A situation | Selected candidate | Authorized alternate capability |
| --- | --- | --- |
| absent journal + valid active pointer | active | only the generation named by `activeReceipt.Rollback` whose ID/epoch/commit/receipt SHA all authenticate and whose directory still exists and is byte/receipt-valid |
| `Prepared` | previous | none; proposed was never publication-authorized, even when its bytes are valid |
| non-first-install `Publishing` | the candidate selected by the first table | the other previous/proposed generation only when it is byte/receipt-valid and bound by that same authenticated Publishing journal's full `Previous` reference or proposed ID/epoch/commit |
| `Committed` | the candidate selected by the first table | the other previous/proposed generation only when it is byte/receipt-valid and bound by that same authenticated Committed journal's full `Previous` reference or proposed ID/epoch/commit |
| corrupt fixed journal + valid independent active pointer | active | none; corrupt journal bytes cannot authorize another generation |
| first-install, `Missing`, `ResetRequired`, `UpdateRequired`, or unmatched state | none | none |

The selected and optional alternate capabilities pin their generations until Phase B and final pointer/quarantine durability complete. An unauthorized but byte-valid directory may be quarantined only after the final decision is durable; it is never considered for admission and is never destroyed between phases. Phase B is a second total decision and never rewrites a Phase-A `Missing`, `ResetRequired`, or `UpdateRequired` into success:

| Exact Phase-A outcome | Selected candidate vs exact retail union | Authorized alternate capability vs union | Exact final result |
| --- | --- | --- | --- |
| `Missing/*`, `ResetRequired/*`, or `UpdateRequired/*` | not evaluated | not evaluated | return the exact Phase-A pair; publish no runtime root |
| `Ready/*` or `RestoredPrevious/*` | admitted | any | return the exact Phase-A pair and publish that selected root |
| `Ready/*` or `RestoredPrevious/*` | rejected | authorized previous generation admitted | `RestoredPrevious/CombinedAdmissionFailed`; atomically select and publish previous |
| `Ready/*` or `RestoredPrevious/*` | rejected | authorized proposed generation admitted | `Ready/CombinedAdmissionFailed`; atomically select and publish proposed |
| `Ready/*` or `RestoredPrevious/*` | rejected | absent, unauthorized, byte-invalid, or also rejected | `Quarantined/CombinedAdmissionFailed`; quarantine conflicting runtime state, fsync/publish the explicit empty root |
| any candidate-bearing Phase-A outcome | retail lease/provider cannot be authenticated | not evaluated | `ResetRequired/CombinedAdmissionFailed`; publish no runtime root and preserve all generations |
| any unmatched or ambiguous Phase-B tuple | any | any | `ResetRequired/InvalidRecoveryState`; publish no runtime root |

The Phase-B alternate can never be the same generation as the rejected selected candidate, and if both previous and proposed are independently byte-valid the authenticated state table uniquely decides whether the other generation receives an alternate capability. An exhaustive Cartesian test spans Phase A's journal presence/version/state, first-install flag, pointer absent/previous/proposed/other plus epoch/commit match, independent previous/proposed byte/receipt validity, alternate authorization, then Phase B's selected/authorized-alternate admission outcomes and retail-provider authentication. Include the exact tuples `Prepared + both byte-valid + previous union-rejected + proposed union-admitted` (proposed remains unauthorized, so final is `Quarantined/CombinedAdmissionFailed`) and `Publishing + both byte-valid + previous union-rejected + proposed union-admitted` (authorized proposed becomes `Ready/CombinedAdmissionFailed`). Inject process death/fault between Phase A and Phase B and prove both pinned generations remain intact. Every tuple must match exactly one row in each applicable phase and exactly one final pair. Recovery never consumes a path from unauthenticated bytes, never publishes before Phase B succeeds, and never deletes a pinned generation.

Same-UID requests coalesce with independent subscribers; each subscription owns only its progress callback, cancellation registration, and completion observation. `Cancel`/`Dispose` are idempotent and unregister that subscriber without blocking; the shared operation is cancelled only after the final subscriber leaves, while already-completed results remain immutable. Different UIDs are serial; queue at most 4; staged reservations total at most 256 MiB; response is capped at 64 MiB with unknown-length streaming; percentage is determinate only from a truthful positive declared length and becomes a typed failure if the body disagrees; filename derives only from canonical UID; response Content-Disposition is recorded only as discarded metadata and never influences paths/UI. A successful transport call transfers exclusive response/body ownership to the shared operation, which immediately uses `await using` and disposes it exactly once after copy or on length/cap/timeout/cancellation/last-subscriber/validation/publication failure; a failed `OpenAsync` transfers nothing. Tracking async streams cover every branch, subscriber coalescing, final-subscriber cancellation, and double-dispose rejection. The random runtime staging inode is wrapped as `IVerifiedMapPackageSource`; Task 10 performs the complete validation with `Ra2MapValidationContexts.CreateRuntime` around the returned current-profile context, and recomputed package UID must equal the request. Download, complete package validation, and pre-publication work create one five-minute deadline and pass that same instance to `MapValidationDeadlineSet.ForRuntime`; no phase may refresh it.

The store never invokes an arbitrary progress observer from a worker. It snapshots each subscriber's next monotonic phase/byte update and invokes `progress.Report` only through `IGameThreadDispatcher`; one subscriber's callback exception is caught, emits one fixed diagnostic, and disables only that subscriber's future progress without cancelling its completion or the shared operation. Progress is phase-monotonic, bytes never decrease, a determinate total never changes, and each remaining subscriber receives at most one terminal update. Subscriber completion sources use asynchronous continuations; `MapPreview.Install` routes every terminal widget/cache mutation back through the same dispatcher rather than assuming an await continuation thread. Cancel/dispose/unbind removes queued callbacks and prevents any later observer invocation. Two-subscriber tests make the first/middle callback throw or cancel, delay/reorder dispatcher work around disposal, and prove the other subscriber and shared result succeed with game-thread affinity and exactly-once terminal state.

Before entering the non-cancellable boundary, the store dispatches the full proposed runtime entry array to the bound `IVerifiedRuntimeMapAdmissionCoordinator.Prepare`. `MapCache` holds an immutable pin to the loaded retail provider for the entire ModData lifetime, profile-filters the exact retail/proposed-runtime arrays, runs duplicate UID/logical-name and `VerifiedMapResidentBudget.ValidateUnion` checks, builds—but does not expose—the combined preview index, and returns either a typed failure or one opaque admission lease. The lease borrows the exact proposed array while the store retains ownership, pins every old/retail input needed for rollback, and exclusively reserves the next runtime publication so provider callbacks or another operation cannot stale it. Admission failure maps to `QuotaExceeded`, leaves the generation/pointer/runtime provider/combined view unchanged, and occurs before any commit journal write.

`VerifiedRuntimeMapAdmissionLease.Commit` is the sole critical in-memory publication path. On the game thread it first proves the same publisher and element-for-element proposed array supplied to `Prepare`, then transfers those entries into the runtime publisher and swaps the already-built combined `MapCache` index as one non-allocating engine transaction. All misuse/stale/thread checks happen before either swap; a valid reserved commit has no recoverable failure point. Public `SnapshotChanged` callbacks run only after both roots are current and use Task 10's per-observer exception isolation, so a UI observer cannot make a committed map invisible or fault the dispatch. The lease releases old pins after the swap; disposal without commit releases the prepared state and leaves both views unchanged. Runtime recovery before ModData binding may publish only its independently authenticated provider snapshot; no `MapCache` exists yet and recovery cannot attach, admit, or expose a combined view. A profiled `MapCache` construction creates only the exact unenumerated `content|maps: User` location placeholder. Registry `Bind` later attaches the retail root, then store `Bind` calls `ConfirmRecovered` to atomically attach/admit the already recovered runtime root and create the first combined view. No critical composition depends on reacting to `SnapshotChanged` after publication.

Immediately after the second quota check, successful admission, and one final deadline/cancellation check, the shared operation enters a non-cancellable commit boundary. From that point it ignores subscriber cancellation/deadline expiry and uses `CancellationToken.None` to write/fsync the complete generation receipt, journal `Prepared`, journal `Publishing`, atomically replace/fsync the pointer, invoke the prepared admission's nonthrowing `Commit` through `gameThread.InvokeAsync`, then write/fsync `Committed` and clear/fsync the journal directory. A subscriber that detaches after the boundary stops observing progress but cannot interrupt consistency; any remaining observer completes according to the exact result below.

If dispatch is rejected or throws **before the admission action begins** after pointer replacement, no in-memory root changed: the store durably restores/fsyncs the previous pointer (or the first-install missing pointer), disposes the uncommitted admission, confirms the old runtime and combined views are still current, records/cleans the rollback journal, and returns `Published=false, Failure=PublishFailed`. If that rollback cannot be proven it returns `Published=false, Failure=RecoveryRequired`, rejects new downloads, and leaves cold recovery to converge the pointer. Once the admission action begins, its pre-reserved engine transaction cannot throw; observer exceptions are isolated and do not enter this branch.

After admission commit, the new runtime and combined views may already have issued leases, so the store never rolls them back. A `Committed` journal write/fsync or journal-clear/directory-fsync failure preserves the proposed pointer and both new views, leaves the last fsynced journal for deterministic recovery, stops the queue, and returns the only permitted mixed result: `Published=true, Failure=RecoveryRequired`. Fully successful cleanup returns `Published=true, Failure=None`; every other failed result has `Published=false`. `RuntimeMapDownloadResult` constructor invariants reject any other combination. Process death between any two durability/publication points is resolved solely by the total table. Kill/fault tests at every receipt write, journal flush, rename, directory fsync, dispatch enqueue/before-action/action, admission swap, `Committed` flush, cleanup unlink, and cleanup fsync boundary assert one specified triple: subscriber result, durable pointer/journal, and runtime/combined snapshot. Cancellation/timeout immediately before versus after the boundary, and post-publish synchronous I/O failure with a live acquired lease, are separately locked.

Reserve quota twice: before network transfer and immediately before pointer swap. Both reservations include current + rollback generations, all staging and queued reservations, catalog count/bytes, the runtime metadata/string usage plus authenticated retail reservation under the single global resident caps, and the exact 512 MiB `DiskSafetyMarginBytes` using checked arithmetic. At each gate, atomically capture the injected preview ledger and require nonnegative/pin-within-total values and totals within the exact `RuntimeMapStoreLimits.PreviewCpuBytes`/`PreviewGpuBytes`; these limits must equal the policy's 64 MiB/128 MiB caps. The check uses live accounted values rather than estimates or static iOS constants and is repeated at swap because UI decode/upload may race the download. Preview memory is not added to disk byte counts; it is a separate mandatory resident-memory gate whose ledger already rejects allocation above its cap. Eviction considers only unpinned, non-current, re-downloadable maps in an old generation; preview eviction remains `MapPreviewImageCache`'s game-thread responsibility. If no safe victim satisfies 128 maps/1 GiB current/2 GiB with rollback/256 MiB staged/global resident limits, valid live preview accounting, and the disk margin, return a localized quota code and leave the current snapshot byte-identical. Tests cover CPU/GPU `limit - 1`/exact/`+ 1`, negative/inconsistent forged providers, preview usage changing between the two checks, pinned exact-cap state, and proof that quota failure leaves both snapshot and ledger byte-for-byte unchanged.

Task 12's `MapCache` change performs the real two-provider composition promised by Task 10 and implements `IVerifiedRuntimeMapAdmissionCoordinator`. Construction acquires neither provider and performs no package enumeration; it creates only the profiled location placeholder. Registry Bind first attaches and pins the immutable retail root. `ConfirmRecovered` then accepts exactly one recovered runtime provider, profile-filters both roots, repeats the engine-owned `VerifiedMapResidentBudget.ValidateUnion` as an invariant check, and atomically installs the first combined preview index only when it matches that provider/generation; its success token owns the runtime attachment and its disposal removes runtime without disturbing retail. A second/different provider, a call before retail attach, or a union mismatch fails without mutation and aborts ModData construction before activation/queue availability. Post-activation downloads use the prepare/commit token above, so duplicate UID/logical-name and aggregate rejection occurs before the durable boundary and a published runtime generation can never be silently absent from chooser/lobby. The loaded retail snapshot is immutable; an attempted retail provider change while an admission exists is rejected and, after normal game start, imported-content mutation is already prohibited. Public provider callbacks are coalesced best-effort notifications only, never the critical union-commit mechanism. Tests interleave retail repair/startup publication with recovery/bind/download, use same UID/same logical name with different UID, change retail resident usage by `+1` across launches, cross every combined cap, inject a different provider/root into `ConfirmRecovered`, call before registry Bind, exercise binding-token disposal/staleness and activation failure, and inject throwing provider observers; they assert recovery result, download result, durable pointer, runtime provider, and combined `MapCache` index always identify the same generation or the explicit empty quarantine state.

The generation tests enforce at most 128 runtime maps, 1 GiB current generation, 2 GiB current+rollback, immutable same-volume generation directories, versioned/checksummed pointer/journal/receipt, complete SHA-256 on cold start, live lease pinning, no eviction of selected/current/rollback/live content, and deterministic crash recovery at each pointer boundary. Downloaded maps never satisfy retail Base readiness. V1 runtime snapshot entries also expose `CanDelete=false`: eviction/removal remains an internal journaled store operation, never the legacy synchronous `MapPreview.Delete` path. `MapPreviewRuntimeInstallTest` proves the iOS chooser shows no false Delete/Delete All affordance and a direct delete call cannot invalidate or remove the current snapshot; desktop writable-map behavior remains unchanged.

- [ ] **Step 2: Run RED**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~VerifiedRuntimeMapStoreTest|FullyQualifiedName~RuntimeMapStoreRecoveryTest|FullyQualifiedName~MapPreviewRuntimeInstallTest' \
  -v minimal
```

Expected: no verified store/snapshot routing, current filename handling is server-controlled, and the new tests fail before publication.

- [ ] **Step 3: Implement the sole writable map service**

Use this fixed private layout under Application Support, scoped by full runtime contract identity:

```text
RuntimeMaps/{profile}-{digest}/
  current
  journal
  generations/{generation}/maps/{uid}.oramap
  generations/{generation}/receipt
  staging/{random}.part
  quarantine/
```

The stream lands in a random `CreateNew` same-volume file. Validate response length while copying, then Task 10 envelope/semantic rules, compatibility, and recomputed UID. Publish a complete immutable generation by pointer replacement and expose it as a new snapshot only on the game thread. Old leases continue reading the old generation. Recovery never exposes partial staging.

`MapPreview.Install` resolves `IVerifiedRuntimeMapStore` through Task 3's sealed `ModData` service provider; otherwise desktop preserves its existing `IReadWritePackage` behavior. Task 12 defines the complete constructor but does not modify AppDelegate or invent a temporary root. Task 13 constructs `RuntimeMapGenerationStore` from the fixed iOS Application Support root plus storage/durability adapters, then supplies that configured store together with transport, validator, contract-bound context factory, dispatcher, clock, and limits.

- [ ] **Step 4: Run GREEN, full tests/builds, and commit**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~VerifiedRuntimeMapStoreTest|FullyQualifiedName~RuntimeMapStoreRecoveryTest|FullyQualifiedName~MapPreviewRuntimeInstallTest' \
  -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Game/OpenRA.Game.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore -m:1
git diff --check
```

Require every command to exit zero. Commit only the ten outer/engine paths:

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 12 --phase implementation
git commit -m "Add verified runtime map downloads"
```

- [ ] **Step 5: Generate and verify `0026` immediately**

Use official+`0001`...`0025` and the exact seven-path engine allowlist:

```bash
set -euo pipefail
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
PATCH_STAGE="$(mktemp -d /private/tmp/nuke-hour-engine-patch-0026.XXXXXX)"
ALLOWLIST="$PATCH_STAGE/0026.allowlist"
VERIFY_DIR="$PATCH_STAGE/verification"
PREDECESSOR_END=25
CANDIDATE=engine-patches/0026-verified-runtime-map-store.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0026 --output "$ALLOWLIST"
LC_ALL=C sort -c -u "$ALLOWLIST"
test "$(wc -l < "$ALLOWLIST" | tr -d ' ')" -eq 7
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
test -f "$VERIFY_DIR/evidence.json"
```

After the self-contained candidate/verifier block passes, commit only patch/README/verifier test:

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 12 --phase delivery
git commit -m "Deliver verified runtime map store engine patch"
```

---

### Task 13: Add iOS Application Support roots and a coordinated Files reader

**Outer files:**

- Create: `OpenRA.Mods.RA2/Content/RetailCoordinatedSourceSessionCore.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/LifecycleGameThreadDispatcherCore.cs`
- Create: `ios/OpenRA.iOS/IosRetailImportRoots.cs`
- Create: `ios/OpenRA.iOS/IosRetailStoragePolicy.cs`
- Create: `ios/OpenRA.iOS/IosRetailDirectoryDurability.cs`
- Create: `ios/OpenRA.iOS/IosCoordinatedSourceSession.cs`
- Create: `ios/OpenRA.iOS/IosRuntimeMapServices.cs`
- Modify: `ios/OpenRA.iOS/IosRetailContentPicker.cs`
- Create: `engine/OpenRA.Test/RetailImportSourceSessionContractTest.cs`
- Modify: `engine/OpenRA.Test/IosContentImportContractTest.cs`

- [ ] **Step 1: Write RED root, policy, scope, and coordination tests**

Use Task 1's single platform-neutral `IRetailImportSourceSession` contract; no external full path crosses into UI, receipt, progress, or ordinary logs. Do not introduce a parallel reader interface or rename `CopyToAsync`:

```csharp
public sealed class IosCoordinatedSourceSession : IRetailImportSourceSession
{
	public Task<RetailOperationResult<IRetailSourceEnumeration>> OpenEnumerationAsync(
		RetailDiscoveryBudget budget,
		CancellationToken cancellationToken);
	public Task<RetailOperationResult<StagedRetailSource>> CopyToAsync(
		RetailSourceId id,
		RetailStagingTarget destination,
		IProgress<RetailImportProgress> progress,
		CancellationToken cancellationToken);
	public ValueTask DisposeAsync();
}
```

Structural and seam tests require:

- at most 64 picker roots;
- every successful `StartAccessingSecurityScopedResource` receives exactly one stop;
- a failed scope start releases all already acquired roots;
- every `async void` delegate catches, sanitizes, and returns to the UIKit main thread;
- `NSFileCoordinator` owns enumeration and the entire coordinated copy, not only the initial open;
- local, iCloud-materializing, provider-unavailable, permission-denied, unknown-length, mid-copy-change, cancellation, and background-interruption outcomes are distinct;
- unknown-length streams stay indeterminate, count bytes while copying, stop at the V1 cap, and recheck free space;
- provider preparation, coordination, and materialization share one injected monotonic 30-minute deadline and a timeout never publishes or seals a partial snapshot;
- the app itself makes no retail-source network request or upload;
- no symlink/special file is copied.

`RetailCoordinatedSourceSessionCore` is platform-neutral and owns the state machine behind those assertions. It receives injectable scope, coordination/materialization, bounded-stream, staging-target, storage/free-space, monotonic-clock, and background-cancellation adapters; its fakes execute exactly-once scope release, whole-copy coordination, unknown-length cap, mid-copy mutation, timeout, cancellation, and exception paths in `OpenRA.Test`. `IosCoordinatedSourceSession` is a thin Foundation/UIKit adapter plus source-contract coverage. The net8 test suite must behavior-test the core rather than claiming UIKit behavior from source-string assertions alone; the unsigned iOS build remains the compile/AOT check for the thin adapter.

The Foundation/provider adapter maps each outcome to the closed error/diagnostic enums: materialization/download required, provider unavailable/offline, permission or security-scope denial, source access expiry/change, validation timeout, cancellation, and sanitized I/O are never collapsed into a raw string. NUnit fakes exercise every mapping and prove no localized UI/log path receives an NSError description, exception, provider URL, or full source path.

- [ ] **Step 2: Run RED**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailImportSourceSessionContractTest|FullyQualifiedName~LifecycleGameThreadDispatcherCoreTest|FullyQualifiedName~IosContentImportContractTest' \
  -v minimal
```

Expected: missing source-reader/storage types and current picker sends raw paths outside the coordinated security scope.

- [ ] **Step 3: Implement the fixed private roots and fail-closed storage policy**

```csharp
internal sealed class IosRetailImportRoots
{
	IosRetailImportRoots(
		string importRoot,
		string runtimeMapRoot,
		string legacyContentRoot,
		string legacyMapsRoot);
	public string ImportRoot { get; }
	public string RuntimeMapRoot { get; }
	public string LegacyContentRoot { get; }
	public string LegacyMapsRoot { get; }
	internal static RetailOperationResult<IosRetailImportRoots> Create(
		NSFileManager fileManager,
		string fixedGameSupportDir,
		string engineVersion);
}
```

Use the injected `NSFileManager.GetUrl` with `ApplicationSupportDirectory`; append only fixed `NUKEHOUR/ImportedContent` and `NUKEHOUR/RuntimeMaps` components. `Game.SupportDir` remains `Documents/OpenRA`. Legacy roots derive only from that already normalized fixed support directory and the manifest-resolved version. The private constructor prevents an arbitrary active root from entering the system. Foundation lookup/create failures map to `StoragePolicyFailure`/`SanitizedIoFailure` in the returned closed result. The platform-neutral `RetailImportRootPolicy` inside `RetailCoordinatedSourceSessionCore.cs` validates canonical fixed components and legacy version tokens; behavior tests cover resolver failure, arbitrary-root/traversal attempts, symlink components, and proof that no public constructor/root factory exists.

`IosRetailStoragePolicy.ApplyAndVerify` sets and then reads back `NSURLIsExcludedFromBackupKey = true` and `NSFileProtectionCompleteUntilFirstUserAuthentication` after every create/copy/move/recovery/migration operation. It rejects symlinks and returns `StoragePolicyFailure` before publication if either attribute cannot be established. `IosRetailDirectoryDurability` opens the directory with `O_DIRECTORY | O_CLOEXEC`, calls `fsync`, and closes in `finally`.

- [ ] **Step 4: Implement coordinated access without raw-path callbacks**

`IosRetailContentPicker` passes selected `NSUrl` values directly into `IosCoordinatedSourceSession`. The session constructor binds one already-created fixed transaction staging root; it owns security scopes and `NSFileCoordinator`, exposes only `RetailSourceId`/safe basename to the neutral pipeline, and resolves `RetailStagingTarget` with component-wise `openat`/no-follow containment before `CreateNew`. Tests pass absolute, traversal, alternate-separator, and symlink targets and prove an outside sentinel is byte-identical. Provider materialization may use the user's selected provider, but the app does not initiate a retail network request. Release every scope after the coordinated copy/seal boundary, including exception and cancellation paths.

Task 13 implements `IosRuntimeMapServices` only as an inert, deferred composition factory. It captures the fixed Application Support roots, storage policy, directory durability, transports, clock, exact V1 policy adapters, and one independently usable lifecycle game-thread dispatcher, but it does not construct a contract-bound store, open an active catalog, mutate AppDelegate, or register any service in this task. Task 14 may use only that exposed dispatcher while cold-opening/repairing the active retail catalog. The factory's single `Create` method accepts exactly two non-forgeable values that already exist by Task 13: the final `VerifiedRa2RuntimeContract` and the exact `RetailReadyGenerationCapability` returned beside the selected catalog lease by Task 7 recovery. It accepts no raw `Ra2RuntimeContract`, `RuntimeContractIdentity`, selection, profile, compatibility array, usage total, catalog DTO, or Task 14 concrete type. Internally it derives the engine identity, selection, profile, canonical `MapCompatibility`, and authenticated `MapResidentUsage`; calling `Create` with a missing/default/mismatched wrapper/Ready pair fails before touching runtime storage.

Lock the iOS composition/lifecycle API:

```csharp
public interface IGameThreadDispatchBackend
{
	bool IsCurrentThread { get; }
	void Post(Action action);
}

public interface IRuntimeMemoryPressureSource
{
	IDisposable Subscribe(Action callback);
}

public sealed class LifecycleGameThreadDispatcherCore :
	IGameThreadDispatcher, IModDataActivatedService, IDisposable
{
	public LifecycleGameThreadDispatcherCore(
		IGameThreadDispatchBackend preGameBackend,
		Func<ModData, IGameThreadDispatchBackend> loadedBackend,
		IRuntimeMemoryPressureSource memoryPressure);
	public bool IsGameThread { get; }
	public Task InvokeAsync(
		Action action,
		CancellationToken cancellationToken);
	public IDisposable Bind(ModData modData);
	public IModDataActivationToken Activate();
	public void Dispose();
}

internal sealed class IosMainThreadDispatchBackend :
	IGameThreadDispatchBackend
{
	public bool IsCurrentThread { get; }
	public void Post(Action action);
}

internal sealed class IosMemoryPressureSource :
	IRuntimeMemoryPressureSource
{
	public IDisposable Subscribe(Action callback);
}

internal sealed class IosRuntimeMapServiceSet : IDisposable
{
	public IGameThreadDispatcher GameThread { get; }
	public IVerifiedRuntimeMapStore RuntimeMaps { get; }
	public IRemoteMapMetadataClient RemoteMetadata { get; }
	public MapPreviewCachePolicy PreviewPolicy { get; }
	internal RuntimeContractIdentity RuntimeIdentity { get; }
	internal Task<RuntimeMapStoreRecoveryResult> RecoverRuntimeMapsAsync(
		IVerifiedMapSnapshotProvider retailSnapshots,
		CancellationToken cancellationToken);
	public bool TryTransferDispatcherTo(
		ModDataServiceCollection freshServices);
	public bool TryTransferRemainingServicesTo(
		ModDataServiceCollection freshServices);
	public void Dispose();
}

internal sealed class IosRuntimeMapServices : IDisposable
{
	public IGameThreadDispatcher GameThread { get; }
	public RetailOperationResult<IosRuntimeMapServiceSet> Create(
		VerifiedRa2RuntimeContract verifiedContract,
		RetailReadyGenerationCapability finalCatalogReady);
	public void Dispose();
}
```

`LifecycleGameThreadDispatcherCore` is platform-neutral and behavior-tested by `OpenRA.Test`. Before ModData binding it uses the injected pre-game backend; the iOS adapter captures the exact UIKit main thread and posts through `BeginInvokeOnMainThread`, while main-thread calls run inline. At end-of-ModData construction, `Bind` only captures and validates the exact loaded backend, captured game/main thread, `ModData`, `MapCache`, and that ModData's dedicated lifecycle lane; it does not switch backends, subscribe memory pressure, post a callback, or expose loaded work. After every service has bound and all six identities match, dispatcher `Activate` runs before store activation, atomically switches to the dedicated lifecycle-lane backend, subscribes the injected memory-pressure source against that exact `MapCache`, and returns its typed activation token. It never routes a lifecycle ticket through legacy `Game.RunAfterTick` or captures `delayedActions`. A throw before return removes any observer, restores the pre-activation backend state, and exposes no callback. Its `BeginDrainAsync`, invoked only after the store's earlier reverse-order drain completes, rejects new loaded invocations, removes the observer, cancels unbegun tickets, and returns one fence for every already admitted callback; Game pumps that lane's FIFO until the fence completes, then token disposal performs only nonblocking final teardown. The later bind-token disposal merely releases the loaded backend/MapCache/ModData/lane references and permits same-mod rebind; it never drains or waits. Cancellation before execution completes the task without invoking the action; exceptions propagate exactly once. Fake backends/sources cover pre-bind background publication, inert bind, activation timing, post-activation lane queueing, warning trim, store-drain dependency, activation drain races, same-mod rebind, unrelated legacy actions, late old-lane tickets, and no callback/reference after drain/unbind/dispose. `IosMainThreadDispatchBackend` and `IosMemoryPressureSource` are thin source-contract/iOS-build adapters only.

Task 14 constructs the inert factory before active-catalog work and uses `GameThread` for every pre-bind retail snapshot publication, repair, and refresh loop. Only after a final Ready lease and matching static preflight produce the exact `VerifiedRa2RuntimeContract` does it invoke `Create(verifiedContract, finalCatalogReady)` exactly once while both capabilities and the selected catalog lease remain alive. Before runtime-root I/O, `Create` requires the Ready pointer/receipt generation, epoch, and commit to be internally authenticated; requires receipt profile and all four `Ra2RuntimeContract` fields to equal `verifiedContract.Contract`; requires `verifiedContract.Selection` to equal the profile's one fixed manifest selection; derives the canonical immutable nonempty `MapCompatibility` only from the wrapper; and derives `MapResidentUsage` only from the Ready receipt after rechecking its authenticated map sum and V1 caps. A Ready from another generation, a same-profile/wrong-digest pair, a caller-substituted compatibility/usage value, or any legacy/default state is unrepresentable or rejected before I/O. `Create` is the sole producer of the closed Base/Yuri `RuntimeIdentity` mapping and one opaque `RuntimeMapStoreAuthorization` bound to the exact pair; neither input exposes a conversion. It then builds all contract-bound components under local ownership and validates their identity/limits before committing anything; any intermediate failure disposes those locals in reverse order and leaves the factory as sole dispatcher owner. A successful call atomically transfers dispatcher ownership and the authorization into the returned set, creates one engine-owned `MapPreviewBudgetLedger` with the exact V1 64 MiB CPU/128 MiB GPU caps, embeds that same ledger in `MapPreviewCachePolicy`, and passes its read-only `IMapPreviewBudgetProvider` view to `VerifiedRuntimeMapStore`; no second ledger or static counter exists. It constructs `RuntimeMapGenerationStore` with the exact fixed `IosRetailImportRoots.RuntimeMapRoot`, `IosRetailStoragePolicy`, and `IosRetailDirectoryDurability`; constructs `Ra2RuntimeMapContextFactory` with the derived fixed trusted manifest/profile and exact verified runtime identity; and constructs `VerifiedRuntimeMapStore` with the generation store, concrete runtime transport, Task 10 validator, context factory, that same dispatcher, clock, preview provider, and exact limits. Before transfer/registration, `RecoverRuntimeMapsAsync` is the only recovery entry: it passes the retained opaque authorization to the concrete store's internal `RecoverAuthorizedAsync`. The public `IVerifiedRuntimeMapStore` exposes no recovery method and cannot accept a caller-created scalar as authority. After transfer, the factory's `GameThread` getter rejects reuse and its `Dispose` is an idempotent no-op for that dispatcher. The context factory receives the actual loaded ModData later only through the store's Task 3 instance-binding lifecycle; it never creates or retains a pre-game scope and never reaches `Game.ModData`. The same result owns the verified remote metadata client with its clock/limits/derived compatibility set and `MapPreviewCachePolicy`.

Task 13 source-contract tests lock the deferred factory's complete input/output ownership, every constructor argument, and failure-before-I/O behavior; reject raw-contract, hand-built identity, selection/profile/compatibility/usage parameters, Task 14-only concrete types, a static/global fallback, or raw-path validation overload; and cross-pair the wrapper and Ready capability from different receipts. `IosRuntimeMapServiceSet.TryTransferDispatcherTo` requires a fresh unsealed collection and submits one owned registration for the dispatcher; a rejected transfer mutates neither object and the set remains its sole owner, while success detaches only that dispatcher. After Task 14 inserts the retail registry, `TryTransferRemainingServicesTo` submits one owned batch ordered runtime store, remote client, preview policy and atomically detaches only those three; it rejects use before successful dispatcher transfer, duplicate/repeated transfer, or a different collection. Thus Task 14 can construct the required global registration order without exposing raw owned service objects or weakening atomic transfer. Task 14 validates exact type/`ReferenceEquals` identity from caller-held registration snapshots before each transfer, submits the host last, then seals. Failure at any batch leaves untransferred objects with their current owner; disposing the still-unsealed collection releases every earlier transferred service exactly once, and disposing the set releases only its still-owned remainder. After sealing, the provider owns reverse-order disposal and is never queried by coordinator/startup code. The constructed ModData performs the only post-transfer identity proof through its public `TryGetService<T>` after successful binds, followed by Task 3 activation. The Task 13 iOS build proves this inert factory compiles without any active-catalog implementation because both authority types precede Task 13. All invocation and service registration are explicitly deferred to Task 14.

The same source-contract gate permits exactly one production construction edge for `RuntimeMapStoreAuthorization`: the private pair-validation path inside `IosRuntimeMapServices.Create(verifiedContract, finalCatalogReady)`. It rejects any authorization constructor/factory or conversion outside that existing `OpenRA.iOS` friend boundary, any direct coordinator-to-store recovery call, and any public recovery member on `IVerifiedRuntimeMapStore`. Only the returned `IosRuntimeMapServiceSet` may retain that exact opaque authorization, and only its internal `RecoverRuntimeMapsAsync` may pass it to the concrete store's internal `RecoverAuthorizedAsync`; no scalar identity, receipt, path, or caller-created token can substitute for it.

`IosRuntimeMapServices` maps all applicable Task 1 V1 values into engine `RemoteMapMetadataLimits`, `RemoteMapPreviewCachePolicy`, complete `PngReadLimits`/`MapPreviewCachePolicy`, and `RuntimeMapStoreLimits`, including the four exact 512-record/16 MiB/4 MiB/32,768 remote-retention fields, authenticated final retail metadata/string reservation, remaining global resident budgets, exact 64 MiB/128 MiB preview limits bound to the shared ledger, the 512 MiB safety margin, and exact deadlines. It passes the remote policy into `VerifiedRemoteMapMetadataClient` and `MapCache` observes only that client's interface property; identity/equality tests reject a missing, substituted, or hard-coded policy. It rejects any reserved value above the global cap rather than underflowing a runtime allowance, and rejects a policy/store preview-limit mismatch before any runtime root I/O. Only the activated dispatcher routes `UIApplication.DidReceiveMemoryWarningNotification` to `MapCache.TrimUnpinnedPreviews()` on the game thread; that operation also trims unpinned remote metadata/encoded previews under their separate ledger. Bind alone cannot receive a warning. Dispatcher activation drain removes the observer before its fence completes; later bind-token disposal releases references only. Tests prove no callback survives shutdown or mutates MapCache from the UIKit thread.

- [ ] **Step 5: Run GREEN, build iOS, and commit**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailImportSourceSessionContractTest|FullyQualifiedName~LifecycleGameThreadDispatcherCoreTest|FullyQualifiedName~IosContentImportContractTest' \
  -v minimal
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -c Release -f net8.0-ios -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:CodesignDisable=true -p:TargetPlatform=ios -m:1
git diff --check
```

Commit only the ten paths:

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 13 --phase implementation
git commit -m "Coordinate private iOS retail source imports"
```

The two engine tests join the exact `0027` allowlist; do not generate that patch yet. Record the checked-in AOT registry SHA before and after the iOS build. If the generator legitimately changes it for Tasks 6–13, keep that one deterministic tracked diff unstaged and untouched through Task 14; Task 15 owns its coverage review and commit. Any other generated-file drift fails this task.

---

### Task 14: Mount only the active catalog and replace the importer UI/lifecycle

**Outer core files:**

- Modify again: `OpenRA.Mods.RA2/Content/Ra2RuntimeContract.cs`
- Modify again: `OpenRA.Mods.RA2/Content/IosRuntimeResourcePreflight.cs`
- Modify: `OpenRA.Mods.RA2/Content/RetailImportModels.cs`
- Modify: `OpenRA.Mods.RA2/Content/RetailContentImporter.cs`
- Modify again: `OpenRA.Mods.RA2/Content/RetailPendingSnapshotStore.cs`
- Create: `OpenRA.Mods.RA2/Content/VerifiedContentHandle.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailActiveCatalog.cs`
- Create: `OpenRA.Mods.RA2/FileSystem/VerifiedCatalogPackage.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailContentWizardStateMachine.cs`
- Create: `OpenRA.Mods.RA2/Content/IosContentWizardLayoutPolicy.cs`

**Outer iOS/UI files:**

- Modify: `ios/OpenRA.iOS/IosContentFileSystemLoader.cs`
- Create: `ios/OpenRA.iOS/IosActiveCatalogMountRegistry.cs`
- Create: `ios/OpenRA.iOS/IosVerifiedContentHandleFactory.cs`
- Rewrite: `ios/OpenRA.iOS/IosContentImportCoordinator.cs`
- Modify again: `ios/OpenRA.iOS/IosRuntimeMapServices.cs`
- Create: `ios/OpenRA.iOS/IosContentWizardViewController.cs`
- Modify: `ios/OpenRA.iOS/AppDelegate.cs`
- Modify: `ios/OpenRA.iOS/ViewController.cs`
- Modify: `mods/ra2/fluent/chrome.ftl`
- Modify: `mods/ra2/fluent/zh-CN/chrome.ftl`

**Engine production files for `0027`:**

- Create: `engine/OpenRA.Game/Support/IosGameContentManagement.cs`
- Modify: `engine/OpenRA.Game/Map/VerifiedMapSnapshot.cs`
- Modify: `engine/OpenRA.Game/Map/MapCache.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs`

**Engine tests for `0027`:**

- Create: `engine/OpenRA.Test/RetailImportMetadataTest.cs`
- Create: `engine/OpenRA.Test/RetailImportDurabilityTest.cs`
- Create: `engine/OpenRA.Test/RetailGenerationStoreTest.cs`
- Create: `engine/OpenRA.Test/RetailImportRecoveryProcessTest.cs`
- Create: `engine/OpenRA.Test/RetailPendingSnapshotStoreTest.cs`
- Create: `engine/OpenRA.Test/RetailLegacyMigrationTest.cs`
- Create: `engine/OpenRA.Test/RetailContentImporterTransactionTest.cs`
- Modify: `engine/OpenRA.Test/RetailContentImporterTest.cs`
- Create: `engine/OpenRA.Test/RetailActiveCatalogTest.cs`
- Create: `engine/OpenRA.Test/VerifiedCatalogPackageTest.cs`
- Create: `engine/OpenRA.Test/RetailContentWizardStateMachineTest.cs`
- Create: `engine/OpenRA.Test/IosContentWizardLayoutPolicyTest.cs`
- Create: `engine/OpenRA.Test/IosGameContentManagementTest.cs`
- Modify: `engine/OpenRA.Test/IosContentImportContractTest.cs`

Task 14 first finalizes Task 4's two contract/preflight types now that Task 7's Ready identity exists:

```csharp
public sealed class VerifiedRa2RuntimeContract
{
	public Ra2RuntimeContract Contract { get; }
	internal TrustedManifestSelection Selection { get; }
	internal ImmutableArray<string> MapCompatibility { get; }
	internal RetailGenerationValidationIdentity Binding { get; }
	private VerifiedRa2RuntimeContract(
		Ra2RuntimeContractExpectation expectation,
		ImmutableArray<string> mapCompatibility,
		RetailGenerationValidationIdentity binding);
}

internal sealed class IosRuntimeResourcePreflight
{
	internal RetailOperationResult<VerifiedRa2RuntimeContract> Check(
		Ra2RuntimeContractExpectation expectation,
		RetailReadyGenerationCapability finalCatalogReady,
		RetailGenerationValidationIdentity selectedCatalogIdentity,
		Manifest resolvedManifest,
		IIosRuntimeResourceRoot distributionRoot,
		RetailValidationDeadline alreadyStartedDeadline,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken);
}

// Task 14 adds this final partial contribution to Task 8's already-created
// RetailPendingSnapshotStore.cs. This is the sole production authorization mint.
internal sealed partial class RetailPendingConsumptionAuthorization
{
	internal static RetailOperationResult<RetailPendingConsumptionAuthorization>
		CreatePairBound(
			StructuralBinding ownedStructuralBinding,
			RetailReadyGenerationCapability exactReady,
			VerifiedRa2RuntimeContract verifiedContract)
	{
		if (!ReferenceEquals(
			verifiedContract.Binding,
			exactReady.ValidationIdentity))
			return RetailOperationResult<RetailPendingConsumptionAuthorization>
				.Failed(/* typed pair mismatch; binding remains unconsumed */);

		var mintSession = ownedStructuralBinding
			.TryConsumeForFinalPair(exactReady);
		return mintSession.Succeeded
			? RetailOperationResult<RetailPendingConsumptionAuthorization>.Success(
				new RetailPendingConsumptionAuthorization(mintSession.Value))
			: RetailOperationResult<RetailPendingConsumptionAuthorization>
				.Failed(mintSession.Failure);
	}
}
```

Before opening any static resource, final `Check` requires `ReferenceEquals(selectedCatalogIdentity, finalCatalogReady.ValidationIdentity)`, authenticates the Ready pointer/receipt and its profile plus all four persisted contract fields against the exact expectation and final manifest selection, and rejects a Ready from another validation attempt even when every persisted scalar is equal. Success stores that same opaque identity as `VerifiedRa2RuntimeContract.Binding`; no getter exposes it outside the trusted boundary. `ReconcileConsumedAsync`, `ConsumePublishedAsync`, and `IosRuntimeMapServices.Create` each begin by requiring `ReferenceEquals(verifiedContract.Binding, ready.ValidationIdentity)` before marker mutation, pending rename/delete, runtime-root I/O, or engine-identity derivation. Pending methods then obtain a fresh Task 8 structural binding for the exact transition and call `CreatePairBound`; only the resulting one-shot authorization may enter the Task 7 session-only state machine. Task 7 and Task 8 production IL must each contain zero `newobj RetailPendingConsumptionAuthorization`; Task 14's method above contains exactly one, dominated by the `ReferenceEquals` branch. Cross-pair tests use two independently validated snapshots of the same generation and two generations with byte-identical contract fields, and prove every mismatched pair leaves structural binding unconsumed and all pending/runtime bytes untouched.

- [ ] **Step 1: Write RED active-catalog isolation tests**

```csharp
internal interface IVerifiedContentHandle : IDisposable
{
	string LogicalName { get; }
	long Length { get; }
	Stream OpenRead();
	void VerifyUnchanged();
}

internal interface IVerifiedContentHandleFactory
{
	IVerifiedContentHandle Open(
		RetailActiveGenerationSnapshot snapshot,
		RetailCatalogArtifact artifact);
}

internal interface IRetailMapEngineAuthorityBridge
{
	Task<RetailOperationResult<IRetailMapPublishedSnapshot>> PublishAsync(
		ImmutableArray<RetailMapEnginePublicationItem> ownedItems,
		IGameThreadDispatcher gameThread,
		CancellationToken cancellationToken);
}

internal interface IRetailMapPublishedSnapshot :
	IVerifiedMapSnapshotProvider, IAsyncDisposable
{
	void DisposeOnGameThread();
}

internal interface IRetailMountedPackageRoot : IDisposable
{
	IReadOnlyPackage AcquirePackage();
}

internal sealed class RetailMapEnginePublicationItem : IDisposable
{
	internal RetailMapReceipt Receipt { get; }
	internal static RetailMapEnginePublicationItem Create(
		RetailMapReceipt authenticatedReceipt,
		IVerifiedContentHandle ownedPackageHandle,
		IVerifiedContentHandle ownedPreviewHandle);
	internal IVerifiedContentHandle TakePackageHandle();
	internal IVerifiedContentHandle TakePreviewHandle();
	public void Dispose();
}

internal sealed record RetailCatalogQuarantine(
	RetailContentGroup Group,
	string LogicalName,
	RetailImportFailure Failure);

internal sealed class RetailActiveCatalogLease :
	IVerifiedMapSnapshotProvider, IAsyncDisposable
{
	private RetailActiveCatalogLease(
		Ra2RuntimeProfileId profile,
		Ra2RuntimeContract contract,
		MapResidentUsage mapResidentUsage,
		ImmutableArray<RetailCatalogQuarantine> quarantinedItems,
		IRetailMountedPackageRoot ownedPackages,
		IRetailMapPublishedSnapshot ownedPublishedMaps);
	public Ra2RuntimeProfileId Profile { get; }
	public Ra2RuntimeContract Contract { get; }
	public MapResidentUsage MapResidentUsage { get; }
	public ImmutableArray<RetailCatalogQuarantine> QuarantinedItems { get; }
	public IReadOnlyPackage AcquirePackage();
	public event Action SnapshotChanged;
	public VerifiedMapSnapshotLease Acquire();
	internal static ValueTask<RetailActiveCatalogLease> CreateAsync(
		Ra2RuntimeProfileId profile,
		Ra2RuntimeContract contract,
		MapResidentUsage mapResidentUsage,
		ImmutableArray<RetailCatalogQuarantine> quarantinedItems,
		IRetailMountedPackageRoot ownedPackages,
		IRetailMapPublishedSnapshot ownedPublishedMaps);
	public ValueTask DisposeAsync();
	internal void DisposeOnGameThread();
}

internal static class RetailActiveCatalog
{
	internal static Task<RetailActiveCatalogOpenResult> OpenAsync(
		RetailActiveGenerationSnapshot snapshot,
		IVerifiedContentHandleFactory handleFactory,
		IRetailMapEngineAuthorityBridge engineAuthority,
		Ra2RuntimeContractExpectation expectedCurrentContract,
		IGameThreadDispatcher gameThread,
		RetailValidationDeadline alreadyStartedDeadline,
		IRetailMonotonicClock clock,
		IProgress<RetailImportProgress> progress,
		CancellationToken cancellationToken);
}

internal enum RetailActiveCatalogOpenDisposition
{
	Ready,
	RepairRequired,
	Invalid,
	Inconclusive,
}

internal sealed class RetailActiveCatalogOpenResult : IAsyncDisposable
{
	private RetailActiveCatalogOpenResult(/* closed tuple + snapshot identity */);
	internal RetailActiveCatalogOpenDisposition Disposition { get; }
	public RetailReadinessResult Readiness { get; }
	public RetailCatalogRepairPlan Repair { get; }
	public RetailImportFailure Failure { get; }
	internal static ValueTask<RetailActiveCatalogOpenResult> ReadyAsync(
		RetailActiveGenerationSnapshot snapshot,
		RetailActiveCatalogLease ownedLease,
		RetailReadinessResult readiness);
	internal static RetailActiveCatalogOpenResult RepairRequired(
		RetailReadinessResult readiness,
		RetailCatalogRepairPlan repair);
	internal static RetailActiveCatalogOpenResult Invalid(
		RetailReadinessResult readiness,
		RetailImportFailure failure);
	internal static RetailActiveCatalogOpenResult Inconclusive(
		RetailImportFailure failure);
	public RetailActiveCatalogLease TakeLease();
	public ValueTask DisposeAsync();
}

internal sealed class RetailActiveCatalogRecoveryLease :
	RetailRecoveryValidationLease
{
	internal Ra2RuntimeContractExpectation Expectation { get; }
	internal override RetailGenerationValidationIdentity ValidationIdentity { get; }
	private RetailActiveCatalogRecoveryLease(
		Ra2RuntimeContractExpectation expectation,
		RetailActiveGenerationSnapshot validatedSnapshot,
		RetailActiveCatalogLease ownedCatalogLease);
	internal static ValueTask<RetailActiveCatalogRecoveryLease> CreateAsync(
		Ra2RuntimeContractExpectation expectation,
		RetailActiveGenerationSnapshot validatedSnapshot,
		RetailActiveCatalogLease ownedCatalogLease);
	internal RetailActiveCatalogLease TakeCatalogLease();
	public override ValueTask DisposeAsync();
}

internal static class RetailActiveCatalogRecoveryAdapter
{
	internal static Task<
		RetailRecoveryCandidateValidation<RetailActiveCatalogRecoveryLease>>
		ValidateAsync(
			RetailActiveGenerationSnapshot snapshot,
			IVerifiedContentHandleFactory handles,
			IRetailMapEngineAuthorityBridge engineAuthority,
			Ra2RuntimeContractExpectation expectation,
			IGameThreadDispatcher gameThread,
			RetailValidationDeadline deadline,
			IRetailMonotonicClock clock,
			IProgress<RetailImportProgress> progress);
}

internal sealed class IosActiveCatalogMountRegistry :
	IVerifiedMapSnapshotProvider, IModDataBoundService, IDisposable
{
	private IosActiveCatalogMountRegistry(
		RetailActiveCatalogLease borrowedCatalog);
	internal static IosActiveCatalogMountRegistry CreateBorrowing(
		RetailActiveCatalogLease borrowedCatalog);
	internal bool TryCommitOwnershipAfterSeal(
		RetailActiveCatalogLease ownedCatalog);
	public event Action SnapshotChanged;
	public VerifiedMapSnapshotLease Acquire();
	public IReadOnlyPackage AcquireMountedPackage();
	public IDisposable Bind(ModData modData);
	public void Dispose();
}
```

Across Tasks 6–14, closed authority/evidence/results use one enforced API-shape rule. In addition to the Task 6 schema list and the concrete classes shown above, it applies to `RetailGenerationValidationResult`, `RetailPublishResult`, `RetailActiveOpenResult`, `RetailRecoveryCandidateValidation<TLease>`, `RetailRecoveryResult<TLease>`, `RetailRepairPublishResult<TLease>`, `RetailPendingOpenResult`, `ConvertedMapArtifact`, `Gen2MapImportResult`, `ValidatedMapArtifact`, `VerifiedMapResidentBudgetResult`, `VerifiedRemoteMapPreviewMetadata`, `RemoteMapMetadataBatch`, `RuntimeMapDownloadResult`, `RuntimeMapStoreRecoveryResult`, `VerifiedRuntimeMapAdmissionResult`, `VerifiedRecoveredRuntimeMapBindingResult`, and `RetailActiveCatalogOpenResult`: sealed nominal get-only classes, private constructor plus internal exact-state factories, no public constructor/init/record-copy/with/subclass path, and defensive immutable ownership. Codec/service-only persistent DTOs—`RetailPendingConsumptionMarker`, `RemoteMapMetadataRecord`, `RuntimeMapReceipt`, `RuntimeMapGenerationReference`, `RuntimeMapGenerationReceipt`, and `RuntimeMapPublishJournal`—have internal validating constructors and the same no-public-mutation rule. `Gen2OverlayResolution`, `VerifiedMapPlayerMetadata`, and `VerifiedMapPreviewMetadata` are the explicit engine-owned cross-assembly bounded-projection exception: each has a private constructor plus exactly one public validating `TryCreate`, no public/copy constructor/init/record/subclass path, and produces only immutable data—not a receipt, validated artifact, root, snapshot, lease, Ready capability, remote verified metadata, or publication result. The shared reflection matrix permits only those three exact public factory methods and proves the RA2 assembly compiles through them without adding a Game-to-RA2 friend grant. `IVerifiedMapEntrySource`, `VerifiedMapEntry.Create`, `VerifiedMapSnapshotPublisher`, its constructor, and its mutation methods are internal engine authority and are separately included in the reflection/call-edge matrix. The two iOS bridges, both bridge-owned published-snapshot interfaces, `IRetailMountedPackageRoot`, and both RA2 publication item/batch families are likewise internal and have exactly the named audited production implementations/call edges. Code outside Game/RA2 and the trusted `OpenRA.iOS` composition TCB cannot implement, construct, take from, or substitute them. `InternalsVisibleTo("OpenRA.iOS")` trusts that whole weak-named assembly; it is not per-type CLR isolation. Inside that TCB, only the exact `IRetailMapEngineAuthorityBridge` and `IRuntimeMapEngineAuthorityBridge` methods in the named iOS files may reach Game's internal authority as an evaluated-source/final-IL release invariant. Tests require the production friend list to be exactly `OpenRA.iOS`, separately whitelist test friends, evaluate the complete iOS `Compile` item graph, and enumerate final `OpenRA.iOS.dll` `InterfaceImpl`/`MethodImpl` plus every `newobj`, `call`, `callvirt`, and `ldftn` edge targeting a Game/RA2 internal authority. The exact `(caller type, caller method, target assembly/type/member/signature)` list is closed; a second implementation/call edge, moving an allowed call, direct construction of a root/Ready/snapshot/authorization, or coordinator-to-store/factory bypass fails. A separately compiled ordinary consumer with a different assembly name cannot access the surface. Finished `.app` and extracted IPA audits require exactly one immediate-root regular `OpenRA.iOS.dll`, no nested/duplicate/extra image, and exact SHA-256/MVID/AOT/same-build-reference identity; signed release evidence additionally verifies the Apple code signature. Every owning result disposes a transferred capability on factory failure and exposes only explicit one-shot `TakeMap`, `TakeSnapshot`, `TakeActiveSnapshot`, `TakeAdmission`, `TakeBinding`, `TakeValidationLease`, or `TakeLease` plus idempotent sync/async disposal appropriate to the owned capability; it never retains a direct public ownership getter. Existing task tests add a shared reflection matrix over this exact list, while each codec/result test covers its legal tuples, mutation resistance, round trip, illegal enum/null cross-products, factory visibility, and exact transfer/disposal. Pure request/limits/policy/UI-input types may retain public validating constructors, but positional notation never grants a trust-producing public constructor.

`RetailActiveCatalog.OpenAsync` is the sole normal-startup cold-hash owner, and the recovery adapter invokes it at most once for each Task 7 pinned candidate. The handle/factory, engine-authority bridge, concrete lease, open result, catalog entry point, and recovery adapter are all internal to the RA2 assembly plus the existing trusted `OpenRA.iOS`/`OpenRA.Test` friend boundary. `IosVerifiedContentHandleFactory` is the sole audited production handle-factory call edge, and `IosActiveCatalogMountRegistry.cs` contains the sole audited production `IRetailMapEngineAuthorityBridge` implementation; the complete compile-graph/final-IL contract above rejects any second implementation/call edge, public constructor, or public Open seam. Open rejects a default deadline or a clock that is not the exact coordinator-owned object used to create it, never starts/refills/replaces the budget, and checks the same token/deadline at entry, before and after every descriptor/open/hash chunk, before/after structural refresh work, before the final handle-stability pass, and before dispatch/publication. Matching cancellation in a direct non-recovery call maps to the closed Cancelled result; recovery uses `CancellationToken.None`. Deadline expiry, safe I/O/dispatcher failure, or final handle drift is `Inconclusive`, maps to `RecoveryFailed`, and never becomes hard invalid. It computes the known total from authenticated receipt lengths, reports `VerifyingActiveHashes` after each bounded read chunk with monotonic completed bytes and that exact total, and publishes no percentage when the total is unavailable or inconsistent. It performs complete SHA-256 for active top-level archives and every active map before returning a lease. The caller first builds the pure `expectedCurrentContract` expectation from the candidate receipt's closed profile plus the current manifest/filename catalog/map format, current validator versions, captured finished-bundle identity, and AOT identity. Open rejects a default/wrong-profile expectation or any immutable provenance mismatch. Matching contract and validator versions may skip only nested structural parsing after those hashes match; any mismatch returns a contract-refresh repair plan and never a Ready lease until full structural revalidation publishes a new generation. The method accepts only Task 7's authenticated generation snapshot and generation-relative artifacts, never a staging artifact, root path, or catalog DTO assembled by the caller. After all hashes and structural checks, it invokes `VerifyUnchanged()` once on every retained handle in fixed artifact order and constructs only the RA2-owned canonical publication-item batch off-thread. It transfers that batch once to the exact bridge. The bridge invokes one admitted dispatcher action that adapts the internal Game sources, constructs every `VerifiedMapEntry` plus `VerifiedMapSnapshotPublisher`, performs initial `Publish`, and constructs the opaque published-snapshot lease in that same game-thread action. If dispatch is rejected before the action begins, no Game entry/publisher exists and the bridge releases the still-owned RA2 items directly. If adaptation, construction, or Publish throws after the action begins, that same action clears/disposes every constructed publisher/entry and releases untransferred items before propagating. No failure path attempts a second cleanup dispatch through an already rejecting dispatcher. Tests prove exact bridge identity/ownership plus deadline/clock identity and expiry at every checkpoint, per-candidate exactly-once hash/open/final-verify, same-length replacement and hash-to-verify drift rejection, initial publication, repair swap, clear, and disposal all run on the game thread, and that off-thread direct use fails.

The method does not consume or dispose the caller's snapshot: it acquires an independent ref-counted root pin for the returned lease, while the caller always disposes its snapshot after open/repair orchestration. `RetailActiveCatalogOpenResult` is an owning, one-shot, factory-only class. Ready owns its lease until `TakeLease()` succeeds once or the result is asynchronously disposed; repair/invalid/inconclusive states own none. `RetailActiveCatalogLease.CreateAsync`, `RetailActiveCatalogOpenResult.ReadyAsync`, and `RetailActiveCatalogRecoveryLease.CreateAsync` each take their named owned inputs at call entry. The caller clears its corresponding ownership reference before awaiting and never cleans that input afterward. Each factory completes every tuple, identity, receipt, readiness, and `ReferenceEquals` check before publishing success; on any later validation/construction failure it awaits reverse cleanup of every input it took—published maps before package root for the catalog lease, and the catalog lease for either wrapping factory. No owning factory blocks, starts fire-and-forget cleanup, or returns before cleanup completes. `OpenAsync` and the recovery adapter await these factories and map operational factory/cleanup failure to `Inconclusive`. Invalid or inconclusive outcomes release every temporary pin/handle; a successful lease owns its pin until its packages/maps/streams release their references. The recovery adapter calls Open exactly once, transfers a Ready lease into `RetailActiveCatalogRecoveryLease` together with the exact expectation and `validatedSnapshot`; that wrapper copies only `validatedSnapshot.ValidationIdentity` and maps `Ready`, `RepairRequired`, `Invalid`, and `Inconclusive` one-for-one without error-message/code guessing. Task 7 then owns and selects those wrappers; the selected wrapper is taken once, never reopened, and its expectation is accepted by final static-resource `Check` only together with its opaque validation identity and the exact final Ready. The same validation identity must equal the selected result's `RetailReadyGenerationCapability.ValidationIdentity`, and the resulting verified wrapper privately retains that exact binding. The lease itself implements the retail `IVerifiedMapSnapshotProvider`; there is no borrowed `Maps` child getter. Its private factory accepts the fully validated profile/contract/map-usage/quarantine tuple plus exactly one owned `IRetailMountedPackageRoot` and one owned published-map snapshot, rejects any mismatch with the authenticated receipt/readiness before transfer, and asynchronously disposes both on factory failure. The package root is constructed only by `RetailActiveCatalog.OpenAsync` from the retained verified archive handles; it exposes no path, raw handle, or mutation method. `AcquirePackage()` delegates to that root and returns a separately pinned caller-owned package. Async lease disposal releases the published map snapshot on the game thread before releasing the package root; `DisposeOnGameThread` invokes the published snapshot's direct game-thread release and then releases the package root synchronously. Source-contract tests reject any second audited `IRetailMountedPackageRoot` production implementation/call edge or public construction seam and exercise factory failure, package acquisition after owner close, maps/streams/packages outliving the lease, and exact-once root disposal.

Before the post-seal ownership commit, every provisional, unselected, failed, preflight-rejected, or final-but-not-yet-committed active-catalog owner is released with awaited `DisposeAsync()` while the coordinator still owns a live pre-game dispatcher. Disposal runs inline when already on the game thread; otherwise it performs exactly one guaranteed release action admitted with the original successful publication and completes only after publisher, entries, handles, and root pins are released. It is idempotent and never uses `Wait`, `Result`, `GetAwaiter().GetResult()`, a synchronous join, or fire-and-forget cleanup. The final selected catalog becomes synchronous-ledger-owned only after `TryCommitOwnershipAfterSeal` succeeds; thereafter Game's provider-disposal ledger invokes `DisposeOnGameThread` directly after activation drain/unbind and before disposing the dispatcher, so no callback is posted after dispatcher drainage. Tests reject the first publication post and require publisher-constructor count zero; throw at construction and Publish and require same-action cleanup; dispose an unselected Ready lease from a thread-pool continuation and await game-thread cleanup before lock release; inject cleanup failure before recovery mutation and require byte-identical `RecoveryFailed`; fail before/after every registration, seal, post-seal cleanup, and ownership-commit boundary; and cover double async disposal, take-then-dispose, preflight failure, pending registry disposal, final direct game-thread registry disposal, and source scans for synchronous waits or discarded cleanup tasks.

Until the post-seal ownership commit, the coordinator exclusively owns the selected catalog lease asynchronously. `IosActiveCatalogMountRegistry.CreateBorrowing` constructs one pending shell that retains only a borrowed reference to that live lease and implements/forwards both `IVerifiedMapSnapshotProvider` and `IModDataBoundService`. While pending, acquisition and Bind may use the live borrowed lease, but registry disposal only rejects new use, detaches any inert bind, and drops the borrowed reference; it never disposes the catalog. The registry's `AcquireMountedPackage()` asks the catalog for a fresh independently pinned package on every loader-decoration call; the decorated loader owns that per-ModData package, while the registry is never owned or disposed by an individual loader. Its insertion-order `Bind` requires the exact profiled `content|maps: User` location already exists as an unenumerated placeholder in that ModData's `MapCache`, acquires exactly one caller-owned immutable snapshot lease, attaches that lease's canonical entries once as the sole retail view through `UpdateFromVerifiedMap`, and returns one token that first removes that exact view and then disposes the lease. Bind installs no subscription/callback, observes no `SnapshotChanged`, and triggers no package enumeration, reopen, YAML parse, or legacy folder scan. The final retail registry never mutates or raises `SnapshotChanged` after construction. Same-mod reconstruction obtains a fresh one-shot Acquire/attach after the old bind token has detached and released its lease.

After the service set transfers the dispatcher, the coordinator registers this borrowed registry aggregate as the sole `Owned<IVerifiedMapSnapshotProvider>` registration; the set's store/client/policy transfer and host transfer follow it. Every failure before ownership commit—including runtime recovery, registration/batch transfer, `SealForGame`, recovery-result cleanup, and pre-game context cleanup—leaves the catalog with the coordinator, which awaits `DisposeAsync` while the pre-game dispatcher is live, then disposes the pending registry, collection/startup state, service set, and current dispatcher owner in reverse order. Cleanup faults are recorded but cannot skip later owners. After `SealForGame` succeeds, the coordinator still does not expose the startup state: it finishes and disposes every validation/recovery result, temporary scope, and `PreGameEngineContext` while retaining both startup state and async catalog ownership. It then calls `registry.TryCommitOwnershipAfterSeal(catalogLease)`. That method first validates without mutation that the registry is live/pending and `ReferenceEquals(ownedCatalog, borrowedCatalog)` holds. False leaves coordinator ownership unchanged; the coordinator awaits catalog cleanup through the live dispatcher and then disposes startup state. True is one atomic non-throwing pending-to-owned transition; only then does the coordinator clear its reference. Repeated, mismatched, pre-disposed, or post-commit calls fail without changing either owner. No later await or cancellation checkpoint is permitted before synchronous `Game.InitializeAndRun`.

After successful commit, registry `Dispose` is valid only on the captured game thread and invokes `ownedCatalog.DisposeOnGameThread` directly. Provider reverse disposal therefore releases registry/catalog before dispatcher disposal, with no async cleanup posted after drain. The options' filesystem-decorator closure only borrows this same aggregate, which persists across sequential same-mod reconstruction because the sealed provider owns it. Already-open packages/maps/streams retain independent pins until they close. Thus no asynchronously disposable catalog ever enters the synchronous service ledger before it has a direct game-thread disposal path. Source-contract tests require interface assignability, exact factory/commit signatures, zero provider subscriptions/callbacks, one Acquire/attach per successful Bind, detach-before-lease-dispose, and fresh Acquire on same-mod rebind. Behavior tests cover exact/monotonic byte progress, known and unavailable totals, cancellation before publication, no fabricated percentage or duplicate hash, exact placeholder attach, zero map-package enumeration/reopen/reparse, one publication per UID, reverse unbind, registry construction/batch/seal/post-seal-cleanup/ownership-commit/game-init failure, pending-registry disposal without catalog disposal, sequential same-mod loader recreation, both final loader/provider shutdown orders, concurrent repair while an old lease remains readable, no double-dispose/leak, and streams outliving aggregate disposal.

The method returns a typed readiness/lease/repair/failure result. A Base mismatch or loss of every Base-compatible map closes the gate. A Yuri receipt is Ready only when at least one surviving map has the Yuri compatibility bit; loss of the last such map returns a repair plan that retains valid Yuri archive bytes as inactive/Unavailable, chooses Base if a Base-compatible map survives, and requires the real validator to publish a newly recomputed Base contract/readiness generation. Corrupt Yuri/music/one map otherwise returns a closed repair plan while valid Base plus another selected-profile-compatible map stays usable. `RetailGenerationStore.OpenActiveAsync` preserves and returns that same authenticated caller-owned snapshot on repair/refresh outcomes. The coordinator obtains one exact new Acquired repair lease, borrows its operation identity, calls `IRetailCandidateValidator.ValidateRepairAsync(active, plan, borrowedOperationIdentity, deadline, clock, CancellationToken.None, progress)`, has the exact store mint admission from that same live lease, passes only the returned bound `RetailValidatedRepairInput` followed by lease/deadline/clock/admission/cancellation/progress to `PrepareRepairAsync`, and invokes `PublishRepairAsync` whose sole post-switch open either transfers final Ready+lease or stops. `PrepareRepairAsync` requires `ReferenceEquals(wrapper.OperationIdentity, lease.BorrowOperationIdentity())` before I/O. The coordinator disposes the old snapshot/result exactly once on every branch and never reopens the repaired generation. Open/store never invent profile semantics or edit in place. A current manifest/build/validator digest change follows the same transaction even when no content group is excluded: identical identity is a no-op, a changed identity performs full structural validation and one lease-bearing repair publication, and failure preserves the old bytes while closing the gate with a typed update/revalidation failure. Tests cover complete Yuri archives with only a Base-compatible map, Yuri-compatible-map retention, cold loss of the final Yuri-compatible map followed by Yuri-to-Base contract recomputation, single-map repair, identity no-op, one identity input change, wrong repair-operation identity, revalidation failure, and an exact one-open/hash count. Repair/refresh never mutates the currently leased generation; GC waits for later cold-start validation and every old lease.

For that Task 14 repair edge, “mint admission” means exactly `generationStore.CreateSpaceAdmission(validatedRepairInput, lease)` after the validator returns the wrapper and before `PrepareRepairAsync`; no census, capacity, active snapshot, plan, or validation component is projected or substituted.

`RetailActiveCatalogOpenResult` uses plain references with private/factory-validated tuples: Ready internally owns one non-null lease and non-null exact five-key readiness, with null `Repair`/`Failure`; repair/refresh owns no lease, non-null readiness/repair, and null failure; `Invalid` owns no lease/repair, a non-null proven-content failure, and readiness only when complete five-key validation could be formed; `Inconclusive` owns no lease/readiness/repair and one non-null operational failure. No lease property, public constructor, record primary/copy constructor, init setter, or `with` path exists; `TakeLease` and `DisposeAsync` enforce one-shot transfer/idempotent awaited cleanup. The wizard's plain `Failure` is non-null only for a state/detail combination that permits it, while nullable-value `Progress` is present only during a progress-reporting activity; derivation rejects every contradictory combination.

Cold open preserves `Readiness.GroupFailures` unchanged. `RetailActiveCatalogLease.QuarantinedItems` contains exactly one group-level row for every such failure, keyed by the same optional group and using the failure's fixed safe basename as `LogicalName`; map quarantine rows remain separately keyed by canonical map UID. No group failure is dropped, duplicated, converted to free text, or synthesized from file presence. The wizard's status-row detail lookup reads `Readiness.GroupFailures[group]` directly for `Invalid`, displays only its sanitized code/diagnostic/safe basename, and requires no entry for Missing/Unavailable/Ready. Tests compare the Task 2 → profile → readiness → receipt → cold-open quarantine → localized detail tuple one-for-one for Yuri, RA2 Music, and Yuri Music, including multiple simultaneously invalid optional groups.

The view exposes only receipted archive names and the profile-compatible verified map snapshot. Stale generations, raw directories, unreceipted files, hand-copied maps, symlinks, old epochs, and a pathname replaced after verification remain invisible. The package is ordinal, immutable, and never implements `IReadWritePackage`.

- [ ] **Step 2: Write RED wizard and lifecycle tests**

Lock a pure state/action derivation:

```csharp
public enum RetailWizardState
{
	Checking,
	Recovering,
	NeedsBase,
	PreparingSources,
	Importing,
	Committing,
	BaseReady,
	FullyReady,
	PendingFailedWithReadyActive,
	RetryableError,
	RuntimeResourcesMissing,
	UpdateRequired,
	ResetRequired,
}

public enum RetailRetryDisposition
{
	None,
	ResumeSealed,
	ChooseAgain,
	RecheckActive,
}

public enum RetailWizardActivity
{
	Idle,
	Checking,
	Recovering,
	PreparingSources,
	Importing,
	Committing,
}

public enum RetailWizardGate
{
	Normal,
	RuntimeResourcesMissing,
	UpdateRequired,
	ResetRequired,
}

public enum RetailWizardPrimaryLabel
{
	None,
	ChooseFilesOrFolder,
	Retry,
	ChooseAgain,
	CheckAgain,
}

[Flags]
public enum RetailWizardActions
{
	None = 0,
	ChooseFiles = 1,
	Cancel = 2,
	Retry = 4,
	ViewDetails = 8,
	DiscardPending = 16,
	ImportOptional = 32,
	EnterGame = 64,
	ResetImportedContent = 128,
	ViewLicenses = 256,
	ManageGameContent = 512,
}

public sealed record RetailWizardModel(
	RetailWizardState State,
	RetailRetryDisposition RetryDisposition,
	RetailWizardActions Actions,
	RetailWizardPrimaryLabel PrimaryLabel,
	RetailReadinessResult Readiness,
	RetailImportProgress? Progress,
	RetailImportFailure Failure);

public sealed record RetailWizardInput(
	RetailWizardActivity Activity,
	RetailWizardGate Gate,
	RetailPendingOpenDisposition Pending,
	RetailRetryDisposition RetryDisposition,
	RetailReadinessResult Readiness,
	RetailImportProgress? Progress,
	RetailImportFailure Failure);

public static class RetailContentWizardStateMachine
{
	public static RetailWizardModel Derive(RetailWizardInput input);
}
```

`Derive` is the sole UI-state mapping. It validates the closed activity/gate/pending/retry combination, derives readiness only from the immutable five-key result, and rejects contradictory inputs rather than choosing an arbitrary priority. Exhaustively test every valid combination and every invalid cross-product. `Checking` and `Recovering` expose none. `NeedsBase` is exactly Choose Files + View Licenses with label `ChooseFilesOrFolder`. `PreparingSources` and `Importing` are exactly Cancel; `Committing` exposes none. `BaseReady` is exactly Enter Game + Import Optional. `FullyReady` is exactly Enter Game + Manage Game Content. `PendingFailedWithReadyActive` is exactly Enter/Retry/Details/Discard with `ResumeSealed` and label `Retry`. A retryable failure with an authenticated sealed pending snapshot is Retry/Details/Discard + `ResumeSealed`/`Retry`; one without a retained source is Choose Files/Details + `ChooseAgain`/`ChooseAgain`; active recheck is Retry/Details + `RecheckActive`/`CheckAgain`. `RuntimeResourcesMissing` is exactly View Details. Both `UpdateRequired` and `ResetRequired` are View Details + Reset Imported Content so a future-version journal has a recovery escape hatch; invoking reset always requires a separate destructive confirmation before the action crosses into the coordinator. No Enter Game appears during checking, first import, conversion, commit, recovery, update/reset requirement, or static runtime-preflight failure.

The pure state machine retains `RuntimeResourcesMissing` so Personal can report an unexpected signed-resource failure and a future runtime-closed PublicClean build can reuse the model. In the current milestone PublicClean never constructs this wizard or importer. Its compile-time/configuration source contract maps the deliberately absent profile-resource set to the same closed diagnostic code before coordinator construction, exposes only the existing non-game diagnostic surface, and has no `EnterGame`, picker, import, catalog, service, or `Game.InitializeAndRun` edge. PublicClean tests exercise this mapping as a pure/source contract and exact bundle absence, not by claiming a successful launch or observed runtime UI.

The scroll view title is exactly `Original Game Content` / `原版游戏内容` and contains exactly five status rows in this order: Base, RA2 Music, Yuri, Yuri Music, Maps. Directly beneath the title, a wrapped, always reachable disclosure reads exactly: `Import files only from a legally owned copy of Command & Conquer: Red Alert 2 / Yuri's Revenge. NUKE HOUR is an independent project and is not affiliated with or endorsed by Electronic Arts.` / `请仅从您合法拥有的《命令与征服：红色警戒 2》/《尤里的复仇》副本导入文件。NUKE HOUR 是独立项目，与 Electronic Arts 无隶属关系，也未获其认可或赞助。` These strings live only in the existing `IosNativeStrings` English/Chinese table; they are not sourced from imported files or remote metadata. Content and layout tests require the complete disclosure in both locales at every accessibility size and prove it remains reachable by scrolling before any picker action.

Each status row renders only Missing, Invalid, Unavailable, or Ready from the immutable five-key readiness map. Availability and edition recognition are separate: `Validation=Ready` plus `Recognition=CompatibleUnrecognized` renders Ready with a sanitized “compatible, unrecognized edition” detail and may satisfy Enter Game, while `Validation=Unavailable` renders Unavailable. It never labels an unrecognized hash as an official known edition and never treats hash recognition as a substitute for structural validation. Tests prove an unknown-hash but structurally valid Base plus map can enter, whereas structurally invalid content cannot. The view never infers state from file presence. Tests also bind the current phase, truthful determinate/indeterminate progress, sanitized detail code, action set, title, and legal disclosure for every state. Layout tests cover every supported iPhone/iPad landscape safe area, rotation, native/effective scale, English/Chinese longest text, and accessibility sizes. Use wrapped labels, at least 48-point buttons, and no overlap/clipping.

- [ ] **Step 3: Run RED**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailActiveCatalogTest|FullyQualifiedName~VerifiedCatalogPackageTest|FullyQualifiedName~RetailContentWizardStateMachineTest|FullyQualifiedName~IosContentWizardLayoutPolicyTest|FullyQualifiedName~IosGameContentManagementTest|FullyQualifiedName~IosContentImportContractTest' \
  -v minimal
```

Expected: current flat `Content/ra2` scan/mount, catch-all mount suppression, fixed six-button wizard, and always-immediate importer cannot satisfy the contracts.

- [ ] **Step 4: Pin verified Darwin file identities and mount once**

`IosVerifiedContentHandleFactory` opens with `O_RDONLY | O_NOFOLLOW | O_CLOEXEC`, pins the parent/path plus descriptor identity, captures regular-file type, expected length, and the platform stability fields required to detect in-place mutation, and retains that descriptor for object lifetime; it does not hash. `RetailActiveCatalog.OpenAsync` is the single SHA-256 owner and hashes the pinned descriptor with explicit-offset `pread`, compares the authenticated expected digest, reports each chunk through its progress/cancellation contract, and performs no second digest pass. Immediately before Ready/publication it calls `VerifyUnchanged()` on every retained handle, which revalidates the pinned descriptor identity/type/length/stability and the canonical no-follow parent/name binding; replacement, relink, or in-place mutation is `Inconclusive`, not content-invalid. `OpenRead()` then acquires its own duplicated descriptor/reference and returns a seekable `PreadFileStream` with an independent logical `Position`; all reads use `pread`, so streams never share or mutate an open-file-description offset. Disposing the package/handle prevents new streams but does not invalidate already-open streams; the generation lease remains pinned until the final stream releases its reference, then every descriptor closes and GC may proceed. Tests prove the one hash pass is exact, hash-then-first-read starts at byte zero, two streams interleave seek/read independently and concurrently, package/handle disposal before stream disposal still reads the same inode, final-stream release enables generation GC, exception paths leak no descriptors, same-length/hash-to-final-verify tamper cannot Ready, and rename/unlink only after admission cannot change bytes. Directory traversal uses pinned parent descriptors and rejects symlink components.

`VerifiedCatalogPackage` exposes exact archive handles plus a maps child backed by the verified snapshot. `IosContentFileSystemLoader` mounts required system packages, then the `content` package obtained from the active registry, then manifest content packages. Remove the catch-all; only manifest entries explicitly marked optional may be absent. `IosActiveCatalogMountRegistry` is the instance aggregate owned by the sealed service provider and captured only by that startup state's `FileSystemLoaderDecorator`; its catalog identity is fixed once when it takes the Ready lease and there is no `Install`/replace API. Every decorator invocation/`ModData` construction calls `AcquireMountedPackage()` exactly once and receives a fresh independently pinned package owned by that configured loader. Disposing one loader never disposes the registry or prevents a later sequential same-mod reconstruction; final provider disposal closes the registry after rejecting new acquisitions, while already-open packages/streams retain their own pins. There is no static registry or mount setter.

The Personal iOS bootstrap registers one immutable retail `IVerifiedMapSnapshotProvider` and the distinct `IVerifiedRuntimeMapStore` in Task 3's collection before sealing startup. `MapCache` composes the one-shot attached retail lease with `runtimeStore.Snapshots`; duplicate service types remain forbidden. Personal retail maps satisfy initial readiness and are filtered by profile, while runtime maps remain distinct. The retail registry never changes during a loaded ModData lifetime; runtime snapshot changes are accepted only on the game thread and old leases remain alive until every preview reference releases. Current PublicClean cannot reach this bootstrap because its earlier fixed-resource check is expected to fail closed.

- [ ] **Step 5: Implement the pre-game coordinator and responsive UIKit view**

`AppDelegate` first executes a configuration-aware fixed-resource boundary before constructing the coordinator. Personal must find all 17 Task 3 profile resources and then constructs the importer, runs mandatory retail generation recovery, fully validates the recovered active catalog, obtains both the exact final Ready capability and its matching `VerifiedRa2RuntimeContract`, passes that pair into pending-consumption reconciliation, resolves any still-visible pending import, and shows the wizard only when needed. PublicClean is required to contain none of those 17 resources; its source contract therefore maps the first missing profile resource to `RuntimeResourcesMissing`, records only sanitized diagnostics, and returns before `IosContentImportCoordinator`, `RetailContentImporter`, `PreGameEngineContext`, active catalog, runtime services, wizard, or `Game.InitializeAndRun` construction. Tests fail if a PublicClean source edge reaches any Personal factory or if any of the 17 resources is evaluated into its bundle. Because the frozen historical PublicClean build remains nonzero, this plan proves that boundary by source and bundle audit and does not claim a launched-app observation.

The remainder of Steps 5–7 is the **Personal-only** runtime path. A repair/update/non-Ready active snapshot supplies no consumption capability, so it cannot delete a retryable pending seal. The coordinator exclusively owns one `PreGameEngineContext` across validation/retry and lends it to `RetailCandidateValidator`; every attempt closes its temporary scope without closing the context. Deadline scopes are explicit rather than conflated. Each user import/resume operation remains governed by Task 8: `RetailContentImporter`, using its injected sole monotonic clock, starts one 90-minute candidate deadline and passes it unchanged through that operation's validator/prepare/publish path.

At each normal cold-start readiness attempt the coordinator first calls `RetailGenerationStore.BeginRecoveryAsync`. This locks/authenticates/pins recovery metadata only and performs zero content reads, hashes, publication, or mutation. While that session is live, it starts one startup-validation deadline from the injected clock, captures that exact clock object plus lifecycle token, constructs the internal `IosRuntimeBuildIdentityProvider`, and passes this startup triple to its one pinned all-app-local managed/AOT `Capture`. It also constructs Task 13's inert runtime-services factory before recovery completion so the still-owned `GameThread` is available for every candidate publication. Immediately before the one call to `CompleteAsync<RetailActiveCatalogRecoveryLease>` it observes the lifecycle token once; cancellation there disposes the uncompleted session with zero mutation. From entry to completion, every invoked candidate callback derives a pure `Ra2RuntimeContractExpectation` from that candidate receipt's closed profile with the same captured build identity and unchanged deadline/clock but `CancellationToken.None`, then invokes `RetailActiveCatalogRecoveryAdapter.ValidateAsync`, which calls `RetailActiveCatalog.OpenAsync(..., CancellationToken.None)` exactly once. Task 7 selects according to its closed table, durably converges pointer/journal only after reauthentication, releases every unselected lease, and returns the selected wrapper without a second open or hash. A capture/builder/dispatcher failure before `CompleteAsync` disposes the session with zero recovery mutation; an invoked candidate's `Inconclusive`, callback exception, illegal tuple, or identity drift returns `RecoveryFailed` with zero pointer/journal/quarantine/GC/pending-consumption mutation and stops this attempt.

If completion returns `Missing`, the coordinator owns no Active/Ready/validation lease. It calls `OpenPendingAsync` only after the retired recovery session released its lock; an exact Ready seal produces the existing `ResumeSealed` wizard path, and the eventual user resume reacquires a fresh process lease, starts a fresh operation deadline, runs `ValidateAsync` to obtain a new exact `RetailValidatedCandidateInput`, calls `CreateSpaceAdmission(validatedInput, lease)`, and then Prepare/Publish. Missing without a Ready seal remains Choose Again. It never reuses the crashed process's validation or admission.

If completion returns `RepairRequired`, its authenticated Active/readiness/repair tuple owns no validation lease. The coordinator retains that recovery result while it runs the real repair validator and `PrepareRepairAsync`, then calls `PublishRepairAsync<RetailActiveCatalogRecoveryLease>` under the unchanged deadline, captured build identity, clock, dispatcher, and `CancellationToken.None`. Its post-switch callback performs the repaired generation's sole `BuildExpected` plus sole `RetailActiveCatalog.OpenAsync`. `Published` becomes the final Ready owner directly: the coordinator asynchronously disposes the old RepairRequired result, takes the exact returned lease, and proceeds to static preflight. It does not call `BeginRecoveryAsync`, `CompleteAsync`, `OpenActiveAsync`, or `RetailActiveCatalog.OpenAsync` again for that generation. Every repair validation, prepare, publication callback, and post-switch build/open after the pre-completion checkpoint receives `CancellationToken.None`; none observes a later lifecycle cancellation or starts/refills/replaces the budget. Any non-Published repair result stops without pending consumption or service creation. Deadline expiry is operational `Inconclusive`, never stable content-invalid. A completed/failed user import followed by a later cold-start attempt gets a new startup deadline and never reuses the import deadline.

On final Ready, the coordinator keeps the exact `RetailRecoveryResult<RetailActiveCatalogRecoveryLease>` or `RetailRepairPublishResult<RetailActiveCatalogRecoveryLease>` and its `RetailReadyGenerationCapability finalCatalogReady` alive, takes the selected lease once, requires `ReferenceEquals(recoveryLease.ValidationIdentity, finalCatalogReady.ValidationIdentity)`, obtains its exact expectation and catalog lease without reopening, and calls final static-resource `Check` exactly once with that expectation, exact Ready, same selected validation identity, inputs/root/deadline/clock/original lifecycle token, and final resolved manifest. This is the next and only lifecycle-cancellation observation after the pre-completion checkpoint. Only a successful `VerifiedRa2RuntimeContract verifiedContract` retaining that exact identity binding fixes the final catalog/profile/contract and, together with the still-live exact Ready, authorizes pending reconciliation and Task 13 pair-authorized service construction. It does not itself expose an engine identity. Preflight failure awaits provisional catalog disposal through the still-live dispatcher, leaves the linked pending seal untouched, and exposes or consumes nothing. No contract-bound runtime store or opaque runtime authorization exists during the retail recovery/repair loop.

Only after that successful static check does the coordinator call `ConsumePublishedAsync(finalCatalogReady, verifiedContract)` for a newly linked unconsumed seal or `ReconcileConsumedAsync(finalCatalogReady, verifiedContract)` for a consumption marker/crash boundary. Each method first requires `ReferenceEquals(verifiedContract.Binding, finalCatalogReady.ValidationIdentity)`, then reauthenticates the current pointer/receipt/pending/generation/epoch/commit/profile/contract/selection/entry/reference-set tuple into a fresh Task 8 `StructuralBinding`. The sole Task 14 final factory consumes that binding into one authorization only after rechecking the same Ready reference. Each destructive transition obtains its own authorization and atomically converts it to the exact private session; a p-rename authority cannot remint t or clear a marker, and a marker-clear authority cannot purge t. Any mismatch performs zero marker/rename/delete mutation and consumes no unrelated binding. `RetailPendingFinalAuthorizationProcessTest.EveryPairRequiredPendingFailpointCrashesAndReconcilesInARealChildProcess` now runs the SHA-frozen Task 8 fixture, enumerates the exact complement of Task 8's preauthorization set, and proves a FailFast plus two fresh launches for Prepared-marker, p->t rename/sync, t remint/purge/Consumed, marker authentication/clear, q, and lost-admission boundaries. Cross-pair, structural-only, cleanup-as-consumption, bare entry, and replay cases remain byte-identical.

After consumption convergence and resolution of any remaining pending decision, the coordinator creates `IosActiveCatalogMountRegistry.CreateBorrowing(catalogLease)` while retaining sole async ownership of that exact lease. It invokes Task 13's deferred `IosRuntimeMapServices.Create(verifiedContract, finalCatalogReady)` exactly once; Create begins with the same binding-identity comparison, then atomically moves the already-used dispatcher plus pair-derived runtime authorization into the contract-bound service set and produces the store/factory, remote client, preview policy, and derived `RuntimeIdentity` without opening another pre-game scope. Before registering services or calling `SealForGame`, the coordinator invokes `serviceSet.RecoverRuntimeMapsAsync(registry, CancellationToken.None)` exactly once for that cold startup. Runtime recovery receives its identity only from the set's opaque authorization, authenticates/cold-hashes only the matching contract directory, converges any journal, and admits the recovered runtime set against an acquired lease from this exact final retail provider before `MapCache` can see it; neither public store interface nor caller scalar can initiate recovery. It does not require a bound ModData and leaves downloads disabled until normal ModData construction repeats/acknowledges the union and binds the store. A typed runtime reset/update/corruption failure exposes an empty or independently verified previous runtime snapshot and a sanitized optional-service status. A cross-launch duplicate/aggregate conflict journaledly quarantines runtime maps and exposes an empty runtime snapshot rather than returning Ready with a hidden disk-current generation. None of these optional outcomes blocks otherwise Ready retail content, and downloaded maps never satisfy retail readiness.

The coordinator then creates a fresh `ModDataServiceCollection`. While it still owns every service and the catalog lease, it constructs immutable registration snapshots and validates each exact declared service type and `ReferenceEquals` identity against the six expected instances in the final submission order; this is the only pre-start identity check. It performs four ownership-safe transfers that create exactly the global order `game-thread dispatcher → borrowed retail registry → recovered runtime-map store → remote-metadata client → preview policy → game-content-management host`: the deferred set transfers only its dispatcher; the coordinator transfers the pending registry as `IVerifiedMapSnapshotProvider`; the set transfers its remaining ordered store/client/policy batch; then the coordinator transfers the host. No later sort or registration insertion is permitted. The instance-scoped catalog loader is the separate decorator but borrows the registry and owns only the fresh package returned for that ModData construction. A rejected batch changes no registration and leaves all of that batch's objects with its current owner; each successful transfer clears only the corresponding ownership references except that catalog ownership remains with the coordinator. Any failure after an earlier success disposes the unsealed collection in reverse order while pending-registry disposal does not touch the catalog; the coordinator first awaits catalog cleanup through the live dispatcher, and each original owner releases only its still-untransferred remainder. `SealForGame` receives `serviceSet.RuntimeIdentity` while the borrowed context is alive. After successful seal, the coordinator finishes and awaits disposal of every validation/recovery result, temporary scope, and pre-game context while it still owns both startup state and catalog lease. It then calls `registry.TryCommitOwnershipAfterSeal(catalogLease)`. False triggers awaited catalog cleanup followed by startup-state disposal. True transfers catalog ownership atomically to the sealed provider/registry and clears the coordinator reference; no await, cancellation check, or other fallible operation follows before synchronous `Game.InitializeAndRun`. `RuntimeContractIdentity` is a validated handshake scalar, not standalone authorization: only the pair-owned service set may expose it to Seal.

`Game.InitializeAndRun` constructs the real ModData. Task 3 binds dispatcher first, registry second, and store third: registry attaches retail to the already constructed placeholder, then store atomically attaches/admit-confirms recovered runtime. After all three non-null bind tokens are owned, `ModData` requires each of all six `TryGetService<T>` lookups to be `ReferenceEquals` to its registration snapshot. Only then it activates the dispatcher first and the store second, owning both non-null typed activation tokens. A bind/identity/null-token/activation throw prevents escape. Rollback and normal shutdown first stop gameplay producers, then reverse-drain the store activation while the dispatcher remains live, drain Task 10's real ModData-owned MapCache preview worker and every admitted upload/`ReleaseBuffer` ticket, directly dispose its sheet/atlas on the game thread, then reverse-drain the dispatcher token; only after all three fences complete does it reverse-dispose bind tokens (`store detach runtime/factory → registry detach retail → dispatcher release backend`). It does not execute or replace legacy `delayedActions` until that sequence finishes. No activation/bind/preview path may synchronously join or wait on a worker, use ThreadPool post-dispose cleanup, or post cleanup through `Game.RunAfterTick`. The separate final provider-owned service disposal remains `host → preview → remote → store → registry → dispatcher`. Validation/batch/seal failure disposes validator/scopes/context/factory/set/services/aggregate pins exactly once and never produces a startup state; after successful seal only `EngineStartupState`/Game owns the provider and registry. Game's Task 3 outer `finally` covers normal exit plus every startup/`Loop` exception and runs independent nested cleanup stages in this exact order: world/order/server/network, store activation drain → MapCache preview drain → dispatcher activation drain → reverse bind tokens plus ModData/global clear, startup/service provider, then Chrome/Sound/Haptics/Renderer and every remaining acquired engine resource.

Personal tests lock this exact sequence: fixed-resource gate → `BeginRecoveryAsync` metadata lock/pins with zero hashes/mutation → start one startup deadline/clock/token → capture current bundle identity → construct dispatcher factory → one pre-completion lifecycle-token checkpoint → `CompleteAsync`, with each candidate performing pure `BuildExpected` then one `RetailActiveCatalog.OpenAsync` under the same deadline/clock and `CancellationToken.None` → table-selected durable convergence and one selected recovery lease, or repair/refresh performing one lease-bearing `PublishRepairAsync` whose sole post-switch open transfers final Ready directly or stops → exact Ready/validation-identity match → selected expectation plus catalog lease → one static-resource `Check(expectation, Ready, selectedIdentity, ...)` with the original lifecycle token → wrapper Binding/Ready identity match → consume/reconcile using exact `(Ready, verifiedContract)` → construct borrowed registry → `Create(verifiedContract, Ready)` → one authorization-bound runtime-map recovery through the service set → validate registrations → transfer dispatcher → borrowed registry → store/client/policy → host → seal with `serviceSet.RuntimeIdentity` while catalog remains coordinator-owned → await/dispose every recovery result and pre-game scope/context → atomic `TryCommitOwnershipAfterSeal` → synchronous game start with no intervening await/cancellation → decorator acquires per-ModData package → bind dispatcher → one-shot registry snapshot attach → store → six identity assertions → activate dispatcher then store. They then exercise post-start publication, validation, warning trim, and same-mod reconstruction through the dedicated lifecycle lane. Shutdown is `store drain/dispose → MapCache preview drain/direct GPU disposal → dispatcher drain/dispose → store/registry/dispatcher unbind → provider dispose`; it never pumps or replaces the legacy delayed-action queue until old ModData teardown is complete. Tests prove an aborted recovery session performs zero hash/mutation, `Inconclusive` produces `RecoveryFailed` with zero durable mutation, no selected or repaired generation is opened twice, no wrapper-only/Ready-only/cross-generation conversion or public recovery authority exists, no pre-start provider lookup exists, no unverified/non-Ready snapshot reaches consumption, and no Create/seal occurs with a stale pair. They inject failure at every begin/capture/build/open/complete/repair-publish/async-factory/release/preflight/consume/create/runtime-recover/bridge/aggregate/batch/seal/post-seal-cleanup/ownership-commit/bind/identity/activation/preview-drain/lane/Loop/cleanup boundary and require exact-once reverse release despite throwing cleanup. Before ownership commit they require awaited catalog cleanup before dispatcher disposal and pending-registry disposal not to touch it; after commit they require direct game-thread registry cleanup exactly once before dispatcher disposal with no posted/discarded task. The shutdown race triggers reconstruction from inside legacy `RunAfterTick`, holds both a post-pointer worker and an old MapCache preview decode/upload on the old dedicated lane, queues an unrelated legacy action, proves each old upload/`ReleaseBuffer`/sheet/atlas cleanup occurs exactly once before retail detach/unmount/lane close/queue replacement, never touches the new ModData, proves the lane pump commits once without reentering that action, and proves no progress callback survives drain. Constructor/bind rollback and final exit run the same preview-drain assertions. It never recreates a string-only startup after sealing. Separate PublicClean source tests prove this Personal graph is unreachable while the 17 profile resources remain excluded; they do not present PublicClean as having executed the graph.

In the test sequence above, “derive receipt-profile expected contract/static preflight” means construct the expected contract and preflight inputs only. The actual `Check` call occurs exactly after the cold-open/repair loop returns the matching Ready lease and before pending reconciliation/consumption, registry construction, service creation, or sealing. Tests record this exact call order and fail if `Check` runs before native retail mount readiness or after any publication becomes externally usable. The remote client, preview policy, and host do not implement `IModDataBoundService`; tests distinguish three bind tokens, two activation tokens, and six-instance provider disposal, and require the store activation to drain while dispatcher/registry stay live, dispatcher activation to drain next, then store to detach before registry and dispatcher bind tokens close.

The coordinator owns one task and one cancellation source. Backgrounding cancels only cancellable discovery/copy/validation; commit/recovery continue. Foreground before normal startup reruns recovery/readiness; foreground after the game starts cannot publish, switch profile, reconstruct ModData, or recover the active catalog. Task 10's real archive/profile/conversion/map validator composition is injected here into Task 8's `IRetailCandidateValidator`; each map shares one five-minute deadline across conversion and post-validation and the candidate shares one 90-minute deadline.

`IosContentWizardViewController` derives every control from the pure model and uses the existing `IosNativeStrings` table in `ViewController.cs`; extend that single English/Chinese table with the exact title/legal disclosure above instead of creating a parallel localization type. The disclosure is visible for missing, ready, retry, update, and reset states and cannot be covered by the action bar. Progress is determinate only with a truthful denominator. UI/log errors use typed sanitized codes and safe basenames, never full paths or raw exception text.

- [ ] **Step 6: Connect iOS-only Manage Game Content with next-launch activation**

Use a lifecycle-safe engine bridge:

```csharp
public interface IIosGameContentManagementHost
{
	bool CanOpen { get; }
	bool TryOpen();
}
```

`AppDelegate` creates one lifecycle-bound host and registers it as the sixth distinct service in the final one-item owned batch, after dispatcher, retail-map provider, runtime store, remote client, and preview policy, before sealing `ModDataServiceCollection`. `MainMenuLogic` resolves `IIosGameContentManagementHost` from its current `ModData`; on iOS it wires the existing `CONTENT_BUTTON` only when `CanOpen` is true and calls that instance's `TryOpen`, while an absent service preserves the desktop `ContentInstallerFileSystemLoader` behavior. The host is same-thread, reused by same-mod reconstruction through the same sealed provider, and explicitly disposed at final game/app shutdown; after disposal `TryOpen` is false and no UIKit/coordinator reference remains. Tests reject duplicate registration and prove reconstruction, disposal, and a stale prior host cannot be observed. There is no static host or registration API.

The in-game manager is unavailable during a match. It may coordinate-copy and envelope-validate into `PendingValidation`, then shows localized `Restart to finish import`. It never converts maps, swaps the active pointer, changes profile/contract, replaces `Game.ModData`, mutates the retail mount, or triggers an in-process mod reload. Next launch performs the full validation/activation, binds the exact pending ID into the committed receipt, and completes Task 8's durable consumption protocol before any pending is offered again. Tests prove the old active generation remains byte-identical until that fresh launch, the exact pending becomes active only there, and subsequent `OpenPendingAsync` is `Missing` across every consumption crash boundary. This task removes the legacy raw-path request/result/importer overload and migrates `RetailContentImporterTest` to the final typed/session API; no production caller or public test retains the old seam.

- [ ] **Step 7: Run GREEN, full suites/builds, and commit**

```bash
set -euo pipefail
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailActiveCatalogTest|FullyQualifiedName~VerifiedCatalogPackageTest|FullyQualifiedName~RetailContentWizardStateMachineTest|FullyQualifiedName~IosContentWizardLayoutPolicyTest|FullyQualifiedName~IosGameContentManagementTest|FullyQualifiedName~IosContentImportContractTest' \
  -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 -v minimal
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
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -c Release -f net8.0-ios -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:CodesignDisable=true -p:TargetPlatform=ios -m:1
git diff --check
```

Force-add the seven previously deferred ignored engine tests only after comparing their recorded Task 6–8 hashes, then stage the remaining listed paths. Source-contract tests must prove the old `RetailImportRequest`, free-form result `Message`/`Hashes`, and raw-path importer overload no longer exist in either production file. Keep only the known deterministic `GeneratedAotObjectRegistry.g.cs` diff unstaged for Task 15 and reject any other out-of-allowlist change. Commit exactly the 38 Task 14 paths; no other ignored engine file may enter the index. The Task 14 changes to `RetailPendingSnapshotStore.cs` and `IosRuntimeMapServices.cs` are mandatory: both consumers perform the final `ReferenceEquals(verifiedContract.Binding, ready.ValidationIdentity)` at their own API entry. Pending code then uses Task 8's structural binding and Task 14's unique final mint before every session-only mutation. Source/IL gates prove Task7 final-auth `newobj = 0`, Task8 `= 0`, Task14 `= 1`; reflection tests reject private-token/default/alias/replay construction; and child tests cross-pair independently validated Ready values from the same persisted generation before any pending/runtime mutation:

```bash
set -euo pipefail
TASK6_DEFERRED_SHA256=packaging/ios-retail-task6-deferred-engine-tests.sha256
TASK7_DEFERRED_SHA256=packaging/ios-retail-task7-deferred-engine-tests.sha256
TASK8_DEFERRED_SHA256=packaging/ios-retail-task8-deferred-engine-tests.sha256
test -f "$TASK6_DEFERRED_SHA256"
test -f "$TASK7_DEFERRED_SHA256"
test -f "$TASK8_DEFERRED_SHA256"
shasum -a 256 -c "$TASK6_DEFERRED_SHA256"
shasum -a 256 -c "$TASK7_DEFERRED_SHA256"
shasum -a 256 -c "$TASK8_DEFERRED_SHA256"
test "$(cat \
  "$TASK6_DEFERRED_SHA256" \
  "$TASK7_DEFERRED_SHA256" \
  "$TASK8_DEFERRED_SHA256" | wc -l | tr -d ' ')" = 7
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 14 --phase implementation
git commit -m "Gate iOS startup on verified retail content"
```

Do not generate `0027` until Task 15 adds its AOT/source/bundle contracts.

---

### Task 15: Add AOT and bundle audits, then deliver `0027`

**Packaging/iOS files:**

- Create: `packaging/audit_ios_retail_import_bundle.py`
- Create: `packaging/tests/test_ios_retail_import_bundle.py`
- Modify: `packaging/audit_ios_touch_runtime_bundle.py`
- Modify: `packaging/tests/test_ios_touch_runtime_bundle.py`
- Modify: `packaging/tests/test_public_bundle_audit.py`
- Create: `packaging/ios-personal-official-engine-assets.json`
- Create: `ios/scripts/run-retail-import-artifact-gates.sh`
- Modify: `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Verify or deterministically modify: `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs`

**Final exact `0027` engine allowlist:**

- Create: `engine/OpenRA.Game/Support/IosGameContentManagement.cs`
- Modify: `engine/OpenRA.Game/Map/VerifiedMapSnapshot.cs`
- Modify: `engine/OpenRA.Game/Map/MapCache.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs`
- Create: `engine/OpenRA.Test/RetailImportMetadataTest.cs`
- Create: `engine/OpenRA.Test/RetailImportDurabilityTest.cs`
- Create: `engine/OpenRA.Test/RetailGenerationStoreTest.cs`
- Create: `engine/OpenRA.Test/RetailImportRecoveryProcessTest.cs`
- Create: `engine/OpenRA.Test/RetailPendingSnapshotStoreTest.cs`
- Create: `engine/OpenRA.Test/RetailLegacyMigrationTest.cs`
- Create: `engine/OpenRA.Test/RetailContentImporterTransactionTest.cs`
- Modify: `engine/OpenRA.Test/RetailContentImporterTest.cs`
- Create: `engine/OpenRA.Test/RetailImportSourceSessionContractTest.cs`
- Modify: `engine/OpenRA.Test/IosContentImportContractTest.cs`
- Create: `engine/OpenRA.Test/RetailActiveCatalogTest.cs`
- Create: `engine/OpenRA.Test/VerifiedCatalogPackageTest.cs`
- Create: `engine/OpenRA.Test/RetailContentWizardStateMachineTest.cs`
- Create: `engine/OpenRA.Test/IosContentWizardLayoutPolicyTest.cs`
- Create: `engine/OpenRA.Test/IosGameContentManagementTest.cs`
- Modify: `engine/OpenRA.Test/AotObjectRegistryCoverageTest.cs`

**Delivery files:**

- Create: `engine-patches/0027-ios-retail-active-catalog-lifecycle.patch`
- Modify: `engine-patches/README.md`
- Modify: `packaging/tests/test_verify_engine_patch_delivery.py`

- [ ] **Step 1: Write RED bundle and AOT tamper tests**

The new audit accepts:

```text
audit_ios_retail_import_bundle.py APP_OR_IPA \
  --source-root ROOT \
  --managed-output-root TARGET_DIR \
  --configuration Personal|PublicClean \
  [--reference-app EXACT_APP_FOR_IPA_MODE]
```

It also has a fail-closed generation/check mode used only for the reviewed official-engine provenance manifest:

```text
audit_ios_retail_import_bundle.py official-provenance \
	--repository ROOT --official-zip PINNED_ZIP --patch-end 26 \
	--output MANIFEST [--check] [--candidate-allowlist EXACT_CODE_ONLY_ALLOWLIST]
```

This mode uses the same safe extractor and strict predecessor replay policy as `verify_engine_patch_delivery.py`; `--check` regenerates under a fresh temporary root and compares bytes without writing the tracked manifest. The asset provenance is intentionally bound to official+`0001`...`0026`. Before the not-yet-delivered `0027` exists, `--candidate-allowlist` validates and records the byte-sorted exact allowlist digest and requires every listed path to be code/test-only and ineligible as a bundle resource. Step 6 then parses the delivered `0027` headers, requires byte-for-byte equality with that recorded allowlist, and repeats the zero-resource check. This avoids asking a pre-sign audit to read a patch that does not exist yet. That zero-resource conclusion and manifest are authoritative only through the Task 17 official+`0001`…`0027` artifact; they never authenticate a later patch. The Personal Ad Hoc closure must regenerate the complete final provenance from strict `0001`…`0027` plus the exact `0028` candidate and prove byte equality with direct `0001`…`0028` generation.

Make the audit configuration-aware. Personal requires the exact importer/profile/catalog/map-store managed assemblies and public type/method metadata, the full AppDelegate/coordinator/loader startup wiring, both profile manifests, all 17 Task 3 profile resources, no interpreter, and generator coverage for every new YAML/ObjectCreator constructible type. PublicClean instead requires those 17 resources to be exactly absent through the 11 literal project removes, while the shared code and required AOT entrypoints remain present; its source/metadata branch must map missing resources to `RuntimeResourcesMissing` and have no edge that constructs the importer/coordinator/catalog/services/game or exposes `EnterGame`. Tamper tests delete one shared type/method, remove Personal startup recovery, restore raw `Content/ra2` mounting, add one forbidden PublicClean profile resource, route PublicClean into a Personal factory, remove a V1 constant, make the Personal content package writable, or omit an AOT constructor and must fail with a precise configuration-specific error. These are source/metadata and bundle-tamper proofs, not a claim that PublicClean launched or ran the Personal gate.

The audit consumes the exact `GenerateAotObjectRegistry` output for this build and parses the versioned generated descriptor inventory directly from that generated source; no sidecar or separately handwritten manifest is accepted. It first authenticates and parses `packaging/ios-runtime-contract-descriptor-shape-v1.json`, then requires the emitted schema, literal count, shape digest, and every ordinal `(logical name, canonical bundle-relative path, profiles, purpose, length, hash)` record to equal both that golden's shape and the evaluated non-wildcard project items. The Personal evaluated project must contain exactly 296 descriptor-derived bundle targets, each exact-once with literal `Optimize=false` and `RuntimeContractDerived=true`, including exactly 63 PNGs; missing/empty/true metadata or an extra old-glob copy fails before inspecting the app. These transport fields do not enter the generated inventory. PublicClean must contain zero descriptor-derived targets and retain its exact exclusions.

For Personal, one shared audit function opens the final `.app` root without following links and, for every `Contract` and `PreflightOnly` descriptor, requires the canonical bundle target to be present exactly once as a regular file whose type, length, and SHA-256 equal the generated source record byte-for-byte; the governed output set has no missing, extra, path alias, transformed copy, or unlisted descriptor-eligible resource, and the golden JSON itself is absent. The safely extracted IPA calls this exact same function and record set, not a second ZIP-specific approximation. It separately proves `Manifest.RuntimeContractFiles` equals only the selected profile's `Contract` projection, while preflight coverage contains both purposes. PublicClean authenticates the same golden/project projection but expects the 17 excluded profile resources to be absent and stops at its configured expected-failure boundary instead of pretending those absent rows passed a finished-app runtime audit. Tests independently tamper golden file hash/framing/schema/count/shape digest, missing/extra record, logical name, final path, profile, purpose, source mapping, project transport metadata, file type, length, and content hash—including changing only a generated-source descriptor length/hash while evaluated items and final app stay unchanged—plus add/remove/alias one governed bundle output or bundle the golden itself. A PNG-specific case replaces one of the 63 targets with a valid re-encoding that renders identical pixels but has different bytes and must fail the content digest in both `.app` and extracted-IPA audits. Every tamper requires a precise failure.

For both configurations, enumerate the finished bundle's regular-file and directory relative paths and reject exact RA2 retail basenames/signatures and importer runtime objects (`publish.journal`, `active.ptr`, `receipts.bin`, generation/pending directory shapes). Do not globally reject `.oramap` or every non-retail `.mix`: Personal legitimately ships project maps and official-engine assets including many `.oramap` files plus `engine/mods/cnc/bits/snow.mix`. Provenance is split and fail-closed: outer/project content must match an exact `git ls-files` path/hash/size record, while official-engine baseline assets must match the reviewed `packaging/ios-personal-official-engine-assets.json`. That versioned manifest records the pinned official-ZIP SHA, patch end `0026`, aggregate patch-series/replay digest, and every eligible bundle path/hash/size; it is generated and byte-verified from a fresh strict official replay, never from the ignored current `engine/` tree. The final `0027` header allowlist is independently parsed and must contain zero bundle-resource-eligible paths. PublicClean additionally requires its existing public manifest classification. Tests regenerate the manifest from a synthetic/fresh replay contract, prove clean official `.oramap` and `snow.mix` entries pass, and reject ignored-tree replacement, wrong official ZIP/patch-series digest, an asset-eligible `0027` header, missing/extra path, or changed bytes. Unknown files with those extensions, any retail signature/basename, mutated allowlisted bytes, or any importer runtime shape fail. Do not substring-scan managed binaries for fixed retail-name constants because the implementation must contain them. Separately scan eligible text/plist metadata for private fixture markers and absolute developer paths, and inspect managed metadata for only the expected type/method contracts. The audit also compares the complete finished-app managed-image inventory with Task 4's exact immediate-root `.dll` rule, rejects a missing/extra/duplicate/unparseable root image, and rejects every nested path whose leaf has the ordinal `.dll` suffix even if it is a symlink or non-regular entry. It never permits a blanket extension/directory exemption. For IPA input, parse ZIP centrally with traversal/duplicate/symlink/special/size limits, require exactly one canonical `Payload/*.app`, reject extra payload roots, then call the exact same `.app` audit function—including generated-inventory path/type/length/hash/profile/purpose and managed-image namespace checks—on the extracted root. Require the explicit `--reference-app` built in the same configuration/commit and compare its complete critical managed/resource inventory with the extracted app. Missing, stale, wrong-configuration, wrong-build, or independently parsed reference apps fail; the audit never guesses a sibling output or maintains a second IPA rule set.

The Personal audit is also the executable TCB call-graph gate. From evaluated `Compile` items and the exact final root `OpenRA.iOS.dll`, it enumerates all iOS implementations of the closed Game/RA2 authority interfaces and every IL `newobj`/`call`/`callvirt`/`ldftn` edge targeting the internal authority list, normalizes full caller/target signatures, and requires byte-for-byte equality with one reviewed closed allowlist. It verifies Game and RA2 production friendship names exactly `OpenRA.iOS` (with test friendship checked separately), and a fixture project under another assembly name must fail to compile when it tries any authority access. Mutation tests add/move each bridge, factory, root, Ready/snapshot/authorization, result, and recovery call edge independently and require audit failure. Both finished `.app` and extracted IPA require exactly one immediate-root regular `OpenRA.iOS.dll`, no nested/duplicate alias, exact generated SHA/MVID/AOT record and same-build reference bytes; the signed Personal artifact additionally passes codesign verification. The audit describes these as whole-TCB release invariants and never claims `InternalsVisibleTo` is per-file CLR isolation.

PublicClean's existing audit still reports the separately pinned historical failures until runtime closure. This task must prove the importer changes add no new `(code,path)` errors, the frozen delta remains exactly 580, and all 17 profile resources are absent; it must not update the baseline, construct/launch PublicClean, infer the runtime diagnostic from a launch, or reclassify those failures as passing.

- [ ] **Step 2: Run RED**

Run each RED independently so one expected failure cannot hide the remaining evidence:

```bash
set -euo pipefail
RED_ROOT="$(mktemp -d /private/tmp/nuke-hour-task15-red.XXXXXX)"
set +e
python3 -m unittest \
  packaging.tests.test_ios_retail_import_bundle \
  packaging.tests.test_ios_touch_runtime_bundle \
  packaging.tests.test_public_bundle_audit -v \
  > "$RED_ROOT/python.log" 2>&1
PYTHON_RC=$?
set -e
test "$PYTHON_RC" -ne 0
rg -n 'FAILED|ERROR|ImportError|ModuleNotFoundError|AssertionError' \
  "$RED_ROOT/python.log"
rg -F 'audit_ios_retail_import_bundle' "$RED_ROOT/python.log"
```

```bash
set -euo pipefail
RED_ROOT="$(mktemp -d /private/tmp/nuke-hour-task15-script-red.XXXXXX)"
set +e
bash -n ios/scripts/run-retail-import-artifact-gates.sh \
  > "$RED_ROOT/script.log" 2>&1
SCRIPT_RC=$?
set -e
test "$SCRIPT_RC" -ne 0
rg -n 'No such file|cannot open|not found' "$RED_ROOT/script.log"
rg -F 'ios/scripts/run-retail-import-artifact-gates.sh' \
  "$RED_ROOT/script.log"
```

```bash
set -euo pipefail
RED_ROOT="$(mktemp -d /private/tmp/nuke-hour-task15-csharp-red.XXXXXX)"
set +e
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~AotObjectRegistryCoverageTest|FullyQualifiedName~IosContentImportContractTest' \
  -v minimal > "$RED_ROOT/csharp.log" 2>&1
CSHARP_RC=$?
set -e
test "$CSHARP_RC" -ne 0
rg -n 'error CS|Failed!|FAILED|not found|missing' "$RED_ROOT/csharp.log"
rg -n 'AotObjectRegistryCoverageTest\.cs|IosContentImportContractTest\.cs' \
  "$RED_ROOT/csharp.log"
```

Expected: the three commands independently prove the missing audit/script and absent importer AOT/source contracts. A zero exit or a log without the named failure signature is not valid RED evidence.

`ios/scripts/run-retail-import-artifact-gates.sh` is the one reusable artifact runner. It begins with `set -euo pipefail` followed immediately by `set +x`, so even an inherited or operator-supplied xtrace cannot print signing values, and accepts only `--mode staged|committed --repository ABSOLUTE_ROOT --official-zip ABSOLUTE_ZIP --output-root EMPTY_DIRECTORY` plus `--engine-allowlist ABSOLUTE_FILE` in staged mode. `staged` requires a nonempty reviewed cached diff, materializes two fresh shared clones, applies that exact binary patch to both indexes, proves path/raw-index equality with the source index, replays official+`0001..0026` plus the reviewed `0027` overlay allowlist, then executes the Personal and PublicClean flows below. `committed` requires a clean source with empty index, creates two fresh clones at exactly its HEAD, strictly materializes official+checked-in `0001..0027`, and executes the same build/audit functions without reading a staged patch, source index, or Task 15 shell variable. Both modes use default relative `bin/obj`, take signing values only from the named environment variables, keep all artifacts/logs under output-root, verify AOT/source bytes remain unchanged, and return nonzero on every failed command. A successful run writes the audited Personal artifact at exact path `OUTPUT_ROOT/NUKE HOUR.ipa` plus its closed reports and private diagnostics beneath `OUTPUT_ROOT`; it never writes `OUTPUT_ROOT/NUKE HOUR-PublicClean.ipa`, and the PublicClean expected-failure report remains explicitly historical rather than a passing artifact. Shell tests exercise both modes with synthetic runners and reject skipped functions, mode leakage, stale sibling outputs, missing `set +x`, `set -x`, secret-bearing argv/logs, recursive deletion, wrong/missing output names, a PublicClean IPA, or a success after any injected failure.

- [ ] **Step 3: Implement an unconditional pre-sign audit target**

Add `AuditIosRetailImportBundle` after `_CreateAppBundle` and before `_CodesignAppBundle`. Personal touch audit and PublicClean public-bundle audit both depend on it. Do not depend on MSBuild target declaration order. Pass exact `$(AppBundleDir)`, repository root, and `$(TargetDir)`; never scan a stale sibling build.

Generate the AOT registry using the existing target. Compare its pre/post bytes. If unchanged, assert that explicitly and do not touch the file. If the generator legitimately adds factories, verify exact generated output, AOT coverage, and source audit before staging that one deterministic diff.

- [ ] **Step 4: Run GREEN and fresh isolated builds**

```bash
set -euo pipefail
python3 -m unittest \
  packaging.tests.test_ios_retail_import_bundle \
  packaging.tests.test_ios_touch_runtime_bundle \
  packaging.tests.test_public_bundle_audit -v
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~AotObjectRegistryCoverageTest|FullyQualifiedName~IosContentImportContractTest' \
  -v minimal
```

The reusable runner owns the complete staged-candidate flow. Its locked internal checklist is to generate and authenticate the official-engine provenance manifest; require the exact Task 15 implementation index; reconstruct two fresh full-engine clones; compare each clone's staged path and raw-index manifests with the source index; build and audit the signed Personal app and IPA; run the fixed PublicClean expected-failure diagnostic without producing a PublicClean IPA; and prove the AOT source plus cached candidate patch remain byte-identical. These are runner-internal operations covered by shell tests, not a second operator command flow, and the runner may not depend on variables created by another shell invocation.

The single authoritative Task 15 execution is:

```bash
set -euo pipefail
set +x
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
: "${IOS_CODESIGN_KEY:?set the Personal Apple Development signing identity}"
: "${IOS_PROVISION_UUID:?set the Personal provisioning profile UUID}"
TASK15_CONTROL_ROOT="$(mktemp -d /private/tmp/nuke-hour-task15-control.XXXXXX)"
TASK15_ARTIFACT_ROOT="$TASK15_CONTROL_ROOT/artifacts"
TASK15_ENGINE_ALLOWLIST="$TASK15_CONTROL_ROOT/0027-engine.allowlist"
mkdir "$TASK15_ARTIFACT_ROOT"
test -z "$(find "$TASK15_ARTIFACT_ROOT" -mindepth 1 -maxdepth 1 -print -quit)"
python3 packaging/engine_workspace.py allowlist \
  --patch-id 0027 --output "$TASK15_ENGINE_ALLOWLIST"
test "$(wc -l < "$TASK15_ENGINE_ALLOWLIST" | tr -d ' ')" -eq 20
LC_ALL=C sort -c -u "$TASK15_ENGINE_ALLOWLIST"
python3 packaging/audit_ios_retail_import_bundle.py official-provenance \
  --repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 26 \
  --output packaging/ios-personal-official-engine-assets.json \
  --candidate-allowlist "$TASK15_ENGINE_ALLOWLIST"
python3 packaging/audit_ios_retail_import_bundle.py official-provenance \
  --repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 26 \
  --output packaging/ios-personal-official-engine-assets.json --check \
  --candidate-allowlist "$TASK15_ENGINE_ALLOWLIST"
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 15 --phase implementation
test -n "$(git diff --cached --name-only --no-renames)"
git diff --cached --check
bash ios/scripts/run-retail-import-artifact-gates.sh \
  --mode staged --repository "$PWD" \
  --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --engine-allowlist "$TASK15_ENGINE_ALLOWLIST" \
  --output-root "$TASK15_ARTIFACT_ROOT"
test -f "$TASK15_ARTIFACT_ROOT/NUKE HOUR.ipa"
test ! -e "$TASK15_ARTIFACT_ROOT/NUKE HOUR-PublicClean.ipa"
```

The runner uses default relative `bin/obj`, never an absolute `ArtifactsPath`, tests the exact staged candidate in two reconstructed full-engine clones, and proves both clone index manifests, the AOT source, and the cached candidate patch remain byte-identical after the builds. Personal alone executes the successful profile/importer artifact gate. PublicClean is an expected-failure pre-sign diagnostic: its report must preserve the frozen 580 delta, its partial app must contain none of the 17 profile resources, and no command launches it or presents it as Ready/playable. No second legacy command block or fabricated PublicClean IPA is permitted.

- [ ] **Step 5: Commit packaging/AOT audit paths**

Recheck that the already staged nine listed packaging/iOS paths that actually changed plus `engine/OpenRA.Test/AotObjectRegistryCoverageTest.cs` (ten paths maximum; omit only an unchanged generated registry, never the test) are byte-identical to the candidate tested in both clones. Show and compare the exact staged list, then commit without restaging unrelated files:

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 15 --phase implementation
git commit -m "Audit iOS retail importer artifacts"
```

- [ ] **Step 6: Generate and verify `0027` immediately**

Generate from official+`0001`...`0026` using the exact 20-path engine allowlist:

```bash
set -euo pipefail
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
PATCH_STAGE="$(mktemp -d /private/tmp/nuke-hour-engine-patch-0027.XXXXXX)"
ALLOWLIST="$PATCH_STAGE/0027.allowlist"
VERIFY_DIR="$PATCH_STAGE/verification"
PREDECESSOR_END=26
CANDIDATE=engine-patches/0027-ios-retail-active-catalog-lifecycle.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0027 --output "$ALLOWLIST"
LC_ALL=C sort -c -u "$ALLOWLIST"
test "$(wc -l < "$ALLOWLIST" | tr -d ' ')" -eq 20
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
test -f "$VERIFY_DIR/evidence.json"
python3 packaging/audit_ios_retail_import_bundle.py official-provenance \
  --repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 26 \
  --output packaging/ios-personal-official-engine-assets.json --check \
  --candidate-allowlist "$ALLOWLIST"
```

Require the self-contained candidate/verifier block's strict gates and fresh `0001`...`0027` replay to be byte-equal to the workspace. Parse the actual candidate headers, require them byte-for-byte equal to the provenance manifest's recorded code-only allowlist digest, and repeat the zero bundle-resource-eligible-path assertion. Update README/verifier tests and commit only the three delivery files:

```bash
set -euo pipefail
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 15 --phase delivery
git commit -m "Deliver active retail catalog engine patch"
```

---

### Task 16: Add private acceptance tooling and run the importer release gate

**Files:**

- Create: `packaging/verify_ios_retail_acceptance_evidence.py`
- Create: `packaging/tests/test_ios_retail_acceptance_evidence.py`
- Create: `ios/scripts/run-retail-import-acceptance.sh`
- Create: `docs/testing/IOS_RETAIL_IMPORT_ACCEPTANCE.md`
- Modify: `docs/testing/IOS_RELEASE_REGRESSION_GATES.md`

- [ ] **Step 1: Write RED evidence-redaction and orchestration tests**

The verifier accepts a versioned JSON object with only this public schema:

```json
{
  "schemaVersion": 1,
  "sourceCommit": "40 lowercase hex",
  "status": "passed|blocked|failed",
  "acceptanceFailureCode": null,
  "importerFailureCode": null,
  "buildConfiguration": "Personal",
  "evidenceKind": "simulator|device",
  "deviceClass": "iPhone|iPad",
  "osMajor": 15,
  "sourceSelectionsTested": ["files", "folder"],
  "legalRetailMatch": true,
  "fileImportBaseReady": true,
  "folderImportBaseReady": true,
  "baseReady": true,
  "baseCompatibleMapCount": 1,
  "yuriCompatibleMapCount": 1,
  "mapSourceKind": "none|direct|converted",
  "enteredMainMenu": true,
  "startedTwoPlayerSkirmish": true,
  "optionalYuriReady": true,
  "yuriActivatedAfterFreshLaunch": true,
  "cancelPreservedPreviousReady": true,
  "corruptCandidatePreservedPreviousReady": true,
  "duplicateConflictPreservedPreviousReady": true,
  "lowSpacePreservedPreviousReady": true,
  "backgroundPreservedPreviousReady": true,
  "forcedTerminationPreservedPreviousReady": true,
  "recoveryPreservedPreviousReady": true,
  "legacyMigrationVerified": true,
  "backupExcluded": true,
  "protectedUntilFirstUnlock": true,
  "copyAttributesVerified": true,
  "renameAttributesVerified": true,
  "generationSwitchAttributesVerified": true,
  "rollbackAttributesVerified": true,
  "recoveryAttributesVerified": true,
  "migrationAttributesVerified": true,
  "manageCreatedPendingValidation": true,
  "manageAvoidedHotReload": true,
  "manageActivatedAfterFreshLaunch": true,
  "bundleSignatureUnchanged": true,
  "importedBytesPrivateRootOnly": true,
  "appAuditPassed": true,
  "ipaAuditPassed": true
}
```

Reject extra keys, missing fields, reordered/duplicate selection tokens, and any string containing `/Users/`, `/private/`, `Documents/`, `Application Support/`, `file://`, a 64-hex digest, retail basename, cloud-account string, IP, URL, or source filename. There is no free-text notes field. `acceptanceFailureCode` is a closed enum: `LegalFixtureUnavailable`, `SigningProfileUnavailable`, `DeviceUnavailable`, `ColdResetAuthorizationUnavailable`, or `ImportedFailure`; `importerFailureCode` is either null or one exact published `RetailImportErrorCode`. `passed` requires both null. An environment-blocked record requires exactly one of the first four acceptance codes and null importer code; a product failure requires `ImportedFailure` plus a non-null importer code. No diagnostic prose is accepted. `osMajor` is numeric but exact device identifiers/UDIDs are never written into the evidence file. A simulator record must use `sourceSelectionsTested=[]`, `mapSourceKind=none`, both compatible-map counts `0`, `legalRetailMatch=false`, `status=passed` only for the simulator subset, and every device-only/legal-fixture boolean (including real import, optional Yuri, migration, protection-stage, signing, and skirmish claims) to false; the verifier owns the exact closed simulator-field list. A passing device record requires every closed checklist boolean above true, source selections exactly `["files","folder"]`, `baseCompatibleMapCount >= 1`, `yuriCompatibleMapCount >= 1` whenever either Yuri-ready boolean is true, and a real direct or converted map source.

`manageActivatedAfterFreshLaunch` is device-only evidence and is true only when the exact pending ID created by Manage Game Content remains inactive before restart, becomes the authenticated active receipt on a genuinely fresh launch, and then reconciles to `OpenPendingAsync=Missing`. Missing, false, wrong-type, or simulator-true values are independent RED cases; the verifier never infers this claim from `manageCreatedPendingValidation` or `manageAvoidedHotReload`.

Shell-source tests require `set -euo pipefail` immediately followed by `set +x`, a fresh shared clone, default relative iOS `bin/obj`, explicit Personal configuration, explicit provision/key variables for device builds, no retail path or signing/device value in arguments/logging, no later `set -x`, no recursive delete, no physical-device uninstall, and a final call to the verifier. The script must stop if the working source commit is dirty or differs from the recorded build commit.

- [ ] **Step 2: Run RED**

Run both expected failures independently and require their named signatures:

```bash
set -euo pipefail
RED_ROOT="$(mktemp -d /private/tmp/nuke-hour-task16-python-red.XXXXXX)"
set +e
python3 -m unittest packaging.tests.test_ios_retail_acceptance_evidence -v \
  > "$RED_ROOT/python.log" 2>&1
PYTHON_RC=$?
set -e
test "$PYTHON_RC" -ne 0
rg -n 'FAILED|ERROR|ImportError|ModuleNotFoundError|AssertionError' \
  "$RED_ROOT/python.log"
rg -F 'verify_ios_retail_acceptance_evidence' "$RED_ROOT/python.log"
```

```bash
set -euo pipefail
RED_ROOT="$(mktemp -d /private/tmp/nuke-hour-task16-script-red.XXXXXX)"
set +e
bash -n ios/scripts/run-retail-import-acceptance.sh \
  > "$RED_ROOT/script.log" 2>&1
SCRIPT_RC=$?
set -e
test "$SCRIPT_RC" -ne 0
rg -n 'No such file|cannot open|not found' "$RED_ROOT/script.log"
rg -F 'ios/scripts/run-retail-import-acceptance.sh' \
  "$RED_ROOT/script.log"
```

Expected: the verifier test and missing script each fail for the intended contract. A zero exit or unrelated failure signature is not valid RED evidence.

- [ ] **Step 3: Implement the safe acceptance harness**

The script performs these automated phases:

1. verify the source commit and exact `0020`...`0027` patch set;
2. create a fresh shared clone under `/private/tmp`, then run `packaging/engine_workspace.py materialize --repository <clone> --official-zip "$OPENRA_OFFICIAL_ZIP" --patch-end 27 --workspace <clone>/engine` before restore; this reconstructs the ignored official engine from the pinned ZIP plus strict `0001`...`0027` replay and never copies the caller's ignored engine tree. Before managed tests only, create a recoverable host-test fixture under the clone's private output: require the clone's real `.git` directory, add `engine/mods/ra2` as a temporary symlink to that clone's canonical root `mods/ra2`, add a temporary `freetype6.dylib` alias to the regular arm64 `/opt/homebrew/opt/freetype/lib/libfreetype.6.dylib`, and pass only that alias directory through `DYLD_LIBRARY_PATH`. A trap moves the RA2 link to private evidence and unsets the library path before packaging/YAML/app work; it never replaces an engine byte, enters Git, or enters an app/IPA. Tests source-audit the exact setup/cleanup and run one repository-root case plus one FreeType-backed layout case inside the real fresh clone. Use relative output paths and explicitly restore every tested/built project—including `build/OpenRA.RetailImport.CrashHarness`—with the pinned SDK before any `--no-restore` command; build the crash harness and require its process-kill tests to execute rather than skip;
3. run focused/full C#, packaging, YAML, AOT, app, and IPA bundle audits;
4. create an unsigned simulator app and cold-install it only on a disposable simulator;
5. verify the empty-content startup wizard, Files selection presentation, English/Chinese layout, cancel, retry, and zero retail bytes in `.app`;
6. when signing variables and a device are explicitly supplied, build/install without uninstalling or deleting an existing physical-device container;
7. collect only sanitized app log events and invoke the evidence verifier.

The legal retail selection and confirmation of the real skirmish are deliberate device-side manual steps documented in `IOS_RETAIL_IMPORT_ACCEPTANCE.md`; the script never receives or prints a host retail path. A truly cold physical-device acceptance requires a separately authorized device/container reset. Without that authorization it is recorded as not-run, not simulated by overwriting an existing install.

- [ ] **Step 4: Run tooling GREEN and commit the public harness before acceptance**

```bash
set -euo pipefail
python3 -m unittest packaging.tests.test_ios_retail_acceptance_evidence -v
bash -n ios/scripts/run-retail-import-acceptance.sh
git diff --check
python3 packaging/exact_git_stage.py stage \
  --repository "$PWD" --manifest packaging/ios-retail-task-paths.json \
  --task 16 --phase implementation
git commit -m "Add redacted iOS retail import acceptance gate"
test -z "$(git status --short)"
```

Commit only the five listed tooling/docs paths and show the exact staged list first. This commit intentionally contains no evidence. The harness's dirty-source guard and fresh shared clone now operate on the committed candidate rather than omitting uncommitted scripts. Never commit evidence JSON, screenshots, device logs, fixture paths, provisioning data, or build outputs.

- [ ] **Step 5: Run the complete synthetic/simulator gate**

```bash
set -euo pipefail
/private/tmp/nuke-hour-task0-python311/bin/python -m unittest discover \
  -s packaging/tests -p 'test_*.py' -v
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 -v minimal
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
bash ios/scripts/run-retail-import-acceptance.sh \
  --mode simulator \
  --evidence-output /private/tmp/nuke-hour-retail-import-simulator-evidence.json
```

Expected: all source/synthetic/simulator gates pass, the fresh simulator contains no retail data before user selection, and the sanitized evidence verifier passes. Simulator evidence does not claim a legal-retail real-match pass.

- [ ] **Step 6: Run the opt-in legal-retail physical-device gate**

Only when the tester has a legally owned extracted installation available through the device Files picker and has authorized the install/reset scope:

```bash
set -euo pipefail
set +x
: "${IOS_CODESIGN_KEY:?set the Personal signing identity}"
: "${IOS_PROVISION_UUID:?set the Personal provisioning profile UUID}"
: "${IOS_DEVICE_UDID:?set the explicitly authorized device identifier}"
export IOS_CODESIGN_KEY IOS_PROVISION_UUID IOS_DEVICE_UDID
bash ios/scripts/run-retail-import-acceptance.sh \
  --mode device \
  --evidence-output /private/tmp/nuke-hour-retail-import-device-evidence.json
```

On device, perform and record without exporting private files:

- folder import and direct-file import each reach Base Ready;
- at least one converted or direct Base-compatible map produces a real two-player skirmish;
- cancel, corrupt candidate, duplicate conflict, low-space simulation, backgrounding, and forced process termination preserve the previous Ready generation;
- optional Yuri import preserves Base and activates only after a fresh app launch;
- backup exclusion and file protection are independently read back after copy, same-volume rename, generation switch, rollback, recovery, and migration;
- Manage Game Content creates PendingValidation and never hot-reloads the active mod: the old active generation stays byte-identical before restart, the exact pending ID validates and activates on the next fresh launch, its authenticated seal is consumed, and a subsequent pending-open check is `Missing`;
- app bundle/code signature remain unchanged and all imported bytes stay in the private sandbox generation root.

If the legal fixture, signing profile, device, or authorized cold-reset scope is unavailable, mark this step `BLOCKED` and stop. Do not call the importer complete and do not advance to PublicClean closure/publication.

- [ ] **Step 7: Verify acceptance left the repository untouched**

```bash
set -euo pipefail
git diff --check
test -z "$(git status --short)"
```

Keep all evidence under the explicit `/private/tmp` outputs. If the device step is blocked, retain the already reviewed tooling commit but mark the milestone blocked; do not manufacture a passing record.

---

### Task 17: Final cross-patch verification and next-milestone handoff

**Files:**

- Inspect only: all task allowlists, `engine-patches/0020-*.patch` through `engine-patches/0027-*.patch`
- Create outside Git only: `/private/tmp/nuke-hour-retail-import-final/`

- [ ] **Step 1: Replay the full engine series from the official ZIP**

Run `packaging/verify_engine_patch_delivery.py` separately for every candidate `0020`...`0027` against its immediate predecessor and then replay `0001`...`0027` in one fresh official tree. Require the single fixed 0007 historical diagnostic tuple, no other fuzz/offset, no rejects/orig files, exact headers, byte identity for every tracked engine path, duplicate forward refusal without tree mutation, and reverse dry-run success.

Each candidate must be verified against the engine workspace from the commit that first delivered that candidate. Verifying an early patch against the final engine workspace is invalid because later patches intentionally modify overlapping files. This block also proves that every checked-in candidate still equals its originally delivered bytes:

```bash
set -euo pipefail
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
FINAL_SOURCE_REPOSITORY="$PWD"
test -z "$(git status --porcelain=v1 --untracked-files=all)"
test -z "$(git diff --name-only)"
test -z "$(git diff --cached --name-only)"
test "$(shasum -a 256 "$OPENRA_OFFICIAL_ZIP" | awk '{print $1}')" = \
  ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a

PATCH_IDS=(0020 0021 0022 0023 0024 0025 0026 0027)
PREDECESSOR_ENDS=(19 20 21 22 23 24 25 26)
CANDIDATES=(
  engine-patches/0020-ios-retail-archive-envelopes.patch
  engine-patches/0021-ios-retail-runtime-profiles.patch
  engine-patches/0022-ios-retail-runtime-contract.patch
  engine-patches/0023-ra2-map-conversion-service.patch
  engine-patches/0024-verified-oramap-map-cache.patch
  engine-patches/0025-verified-remote-map-metadata.patch
  engine-patches/0026-verified-runtime-map-store.patch
  engine-patches/0027-ios-retail-active-catalog-lifecycle.patch
)
HISTORY_ROOT="$(mktemp -d /private/tmp/nuke-hour-retail-history.XXXXXX)"
DELIVERY_COMMITS=()

for index in "${!PATCH_IDS[@]}"; do
  PATCH_ID="${PATCH_IDS[$index]}"
  PREDECESSOR_END="${PREDECESSOR_ENDS[$index]}"
  CANDIDATE="${CANDIDATES[$index]}"
  CANDIDATE_COMMIT_LIST="$HISTORY_ROOT/$PATCH_ID.delivery-commits"
  git log --diff-filter=A --format='%H' -- "$CANDIDATE" \
    > "$CANDIDATE_COMMIT_LIST"
  test "$(wc -l < "$CANDIDATE_COMMIT_LIST" | tr -d ' ')" -eq 1
  DELIVERY_COMMIT="$(sed -n '1p' "$CANDIDATE_COMMIT_LIST")"
  test -n "$DELIVERY_COMMIT"
  DELIVERY_COMMITS+=("$DELIVERY_COMMIT")

  PATCH_ROOT="$HISTORY_ROOT/$PATCH_ID"
  HISTORY_CLONE="$PATCH_ROOT/repository"
  ALLOWLIST="$PATCH_ROOT/$PATCH_ID.allowlist"
  VERIFY_DIR="$PATCH_ROOT/verification"
  mkdir -p "$PATCH_ROOT"
  git clone --shared --no-hardlinks \
    "$FINAL_SOURCE_REPOSITORY" "$HISTORY_CLONE"
  git -C "$HISTORY_CLONE" checkout --detach "$DELIVERY_COMMIT"
  test "$(git -C "$HISTORY_CLONE" rev-parse HEAD)" = "$DELIVERY_COMMIT"
  cmp "$FINAL_SOURCE_REPOSITORY/$CANDIDATE" \
    "$HISTORY_CLONE/$CANDIDATE"

  PATCH_END=$((10#$PATCH_ID))
  python3 "$HISTORY_CLONE/packaging/engine_workspace.py" materialize \
    --repository "$HISTORY_CLONE" --official-zip "$OPENRA_OFFICIAL_ZIP" \
    --patch-end "$PATCH_END" --workspace "$HISTORY_CLONE/engine"
  python3 "$HISTORY_CLONE/packaging/engine_workspace.py" allowlist \
    --patch-id "$PATCH_ID" --output "$ALLOWLIST"
  LC_ALL=C sort -c -u "$ALLOWLIST"
  python3 "$HISTORY_CLONE/packaging/verify_engine_patch_delivery.py" \
    --repository "$HISTORY_CLONE" --official-zip "$OPENRA_OFFICIAL_ZIP" \
    --expected-zip-sha256 ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a \
    --predecessor-start 1 --predecessor-end "$PREDECESSOR_END" \
    --allowlist-file "$ALLOWLIST" \
    --candidate "$HISTORY_CLONE/$CANDIDATE" \
    --workspace "$HISTORY_CLONE/engine" --output-dir "$VERIFY_DIR"
  test -f "$VERIFY_DIR/evidence.json"
  rg -F '"passed": true' "$VERIFY_DIR/evidence.json"
done

FINAL_0027_DELIVERY="${DELIVERY_COMMITS[7]}"
git diff --exit-code "$FINAL_0027_DELIVERY"..HEAD -- engine
FINAL_REPLAY_ROOT="$(mktemp -d /private/tmp/nuke-hour-retail-replay.XXXXXX)"
python3 packaging/engine_workspace.py materialize \
  --repository "$FINAL_SOURCE_REPOSITORY" \
  --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 27 --workspace "$FINAL_REPLAY_ROOT/engine"
test -z "$(find "$FINAL_REPLAY_ROOT/engine" \
  \( -name '*.rej' -o -name '*.orig' \) -print -quit)"
```

- [ ] **Step 2: Run all source, packaging, and build gates from a clean checkout**

```bash
set -euo pipefail
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
FINAL_SOURCE_REPOSITORY="$PWD"
test -z "$(git status --porcelain=v1 --untracked-files=all)"
test -z "$(git diff --name-only)"
test -z "$(git diff --cached --name-only)"
FINAL_SOURCE_COMMIT="$(git rev-parse HEAD)"
FINAL_CLONE="$(mktemp -d /private/tmp/nuke-hour-retail-final.XXXXXX)/repository"
git clone --shared --no-hardlinks "$FINAL_SOURCE_REPOSITORY" "$FINAL_CLONE"
test "$(git -C "$FINAL_CLONE" rev-parse HEAD)" = "$FINAL_SOURCE_COMMIT"
python3 "$FINAL_CLONE/packaging/engine_workspace.py" materialize \
  --repository "$FINAL_CLONE" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 27 --workspace "$FINAL_CLONE/engine"
cd "$FINAL_CLONE"
test -z "$(git status --short)"

# Fresh official replay intentionally has neither the project RA2 mirror nor
# an arm64 alias for the official x86_64 desktop FreeType payload. These are
# host-test-only links; remove them before packaging/YAML/build gates.
FINAL_HOST_FIXTURE="$(mktemp -d /private/tmp/nuke-hour-final-host.XXXXXX)"
test -d .git
test ! -e engine/mods/ra2
ln -s "$PWD/mods/ra2" engine/mods/ra2
FINAL_FREETYPE=/opt/homebrew/opt/freetype/lib/libfreetype.6.dylib
test -f "$FINAL_FREETYPE"
test ! -L "$FINAL_FREETYPE"
xcrun lipo "$FINAL_FREETYPE" -verify_arch arm64
ln -s "$FINAL_FREETYPE" "$FINAL_HOST_FIXTURE/freetype6.dylib"
export DYLD_LIBRARY_PATH="$FINAL_HOST_FIXTURE"

TASK6_DEFERRED_SHA256=packaging/ios-retail-task6-deferred-engine-tests.sha256
TASK7_DEFERRED_SHA256=packaging/ios-retail-task7-deferred-engine-tests.sha256
TASK8_DEFERRED_SHA256=packaging/ios-retail-task8-deferred-engine-tests.sha256
test -f "$TASK6_DEFERRED_SHA256"
test -f "$TASK7_DEFERRED_SHA256"
test -f "$TASK8_DEFERRED_SHA256"
shasum -a 256 -c "$TASK6_DEFERRED_SHA256"
shasum -a 256 -c "$TASK7_DEFERRED_SHA256"
shasum -a 256 -c "$TASK8_DEFERRED_SHA256"
test "$(cat \
  "$TASK6_DEFERRED_SHA256" \
  "$TASK7_DEFERRED_SHA256" \
  "$TASK8_DEFERRED_SHA256" | wc -l | tr -d ' ')" = 7

env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet restore \
  engine/OpenRA.Test/OpenRA.Test.csproj -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet restore \
  engine/OpenRA.Game/OpenRA.Game.csproj -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet restore \
  engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet restore \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet restore \
  build/OpenRA.RetailImport.CrashHarness/OpenRA.RetailImport.CrashHarness.csproj -m:1
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet restore ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -p:RuntimeIdentifier=iossimulator-arm64 -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  build/OpenRA.RetailImport.CrashHarness/OpenRA.RetailImport.CrashHarness.csproj \
  -c Debug --no-restore -m:1
CRASH_HARNESS_DLL="$(
  env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet msbuild \
    build/OpenRA.RetailImport.CrashHarness/OpenRA.RetailImport.CrashHarness.csproj \
    -getProperty:TargetPath -p:Configuration=Debug
)"
test -f "$CRASH_HARNESS_DLL"
test ! -L "$CRASH_HARNESS_DLL"
case "$CRASH_HARNESS_DLL" in
  "$FINAL_CLONE"/*) ;;
  *) exit 1 ;;
esac
CRASH_HARNESS_SHA256="$(
  shasum -a 256 "$CRASH_HARNESS_DLL" | awk '{print $1}'
)"
test -n "$CRASH_HARNESS_SHA256"
CRASH_HARNESS_DIR="$(dirname "$CRASH_HARNESS_DLL")"
OPENRA_TEST_DLL="$CRASH_HARNESS_DIR/OpenRA.Test.dll"
RA2_DLL="$CRASH_HARNESS_DIR/OpenRA.Mods.RA2.dll"
test -f "$OPENRA_TEST_DLL"
test ! -L "$OPENRA_TEST_DLL"
test -f "$RA2_DLL"
test ! -L "$RA2_DLL"
OPENRA_TEST_SHA256="$(shasum -a 256 "$OPENRA_TEST_DLL" | awk '{print $1}')"
RA2_SHA256="$(shasum -a 256 "$RA2_DLL" | awk '{print $1}')"
test -n "$OPENRA_TEST_SHA256"
test -n "$RA2_SHA256"

TASK7_EVIDENCE="$(mktemp -d /private/tmp/nuke-hour-retail-final-task7.XXXXXX)"
TASK7_CRASH_ROOT="$TASK7_EVIDENCE/crash-root"
env \
  DOTNET_ROLL_FORWARD=Major \
  RETAIL_IMPORT_PROCESS_KILL_REQUIRED=1 \
  RETAIL_IMPORT_CRASH_HARNESS_DLL="$CRASH_HARNESS_DLL" \
  RETAIL_IMPORT_CRASH_HARNESS_SHA256="$CRASH_HARNESS_SHA256" \
  RETAIL_IMPORT_OPENRA_TEST_DLL="$OPENRA_TEST_DLL" \
  RETAIL_IMPORT_OPENRA_TEST_SHA256="$OPENRA_TEST_SHA256" \
  RETAIL_IMPORT_RA2_DLL="$RA2_DLL" \
  RETAIL_IMPORT_RA2_SHA256="$RA2_SHA256" \
  RETAIL_IMPORT_CRASH_ROOT="$TASK7_CRASH_ROOT" \
	/Users/kevin/.dotnet/dotnet test \
	  engine/OpenRA.Test/OpenRA.Test.csproj --no-build --no-restore -m:1 \
    --filter 'FullyQualifiedName~RetailGenerationStoreTest|FullyQualifiedName~RetailImportRecoveryProcessTest' \
    --results-directory "$TASK7_EVIDENCE" \
    --logger 'trx;LogFileName=task7.trx' -v minimal
test -f "$TASK7_EVIDENCE/task7.trx"
rg -F \
  'testName="EveryFailpointCrashesAndRecoversInARealChildProcess"' \
  "$TASK7_EVIDENCE/task7.trx"
! rg -n 'outcome="(NotExecuted|Skipped|Inconclusive)"' \
  "$TASK7_EVIDENCE/task7.trx"

TASK8_EVIDENCE="$(mktemp -d /private/tmp/nuke-hour-retail-final-task8.XXXXXX)"
env \
  DOTNET_ROLL_FORWARD=Major \
  RETAIL_IMPORT_PROCESS_KILL_REQUIRED=1 \
  RETAIL_IMPORT_PENDING_PROCESS_KILL_REQUIRED=1 \
  RETAIL_IMPORT_CRASH_HARNESS_DLL="$CRASH_HARNESS_DLL" \
  RETAIL_IMPORT_CRASH_HARNESS_SHA256="$CRASH_HARNESS_SHA256" \
  RETAIL_IMPORT_OPENRA_TEST_DLL="$OPENRA_TEST_DLL" \
  RETAIL_IMPORT_OPENRA_TEST_SHA256="$OPENRA_TEST_SHA256" \
  RETAIL_IMPORT_RA2_DLL="$RA2_DLL" \
  RETAIL_IMPORT_RA2_SHA256="$RA2_SHA256" \
  RETAIL_IMPORT_CRASH_ROOT="$TASK8_EVIDENCE/crash-root" \
	/Users/kevin/.dotnet/dotnet test \
	  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-build --no-restore -m:1 \
	  --filter 'FullyQualifiedName~RetailPendingSnapshotStoreTest|FullyQualifiedName~RetailPendingFinalAuthorizationProcessTest|FullyQualifiedName~RetailLegacyMigrationTest|FullyQualifiedName~RetailContentImporterTransactionTest|FullyQualifiedName~RetailImportRecoveryProcessTest' \
    --results-directory "$TASK8_EVIDENCE" \
    --logger 'trx;LogFileName=task8.trx' -v minimal
test -f "$TASK8_EVIDENCE/task8.trx"
rg -F \
	'testName="EveryPreauthorizationPendingFailpointCrashesAndRecoversInARealChildProcess"' \
	"$TASK8_EVIDENCE/task8.trx"
rg -F \
	'testName="EveryPairRequiredPendingFailpointCrashesAndReconcilesInARealChildProcess"' \
	"$TASK8_EVIDENCE/task8.trx"
! rg -n 'outcome="(NotExecuted|Skipped|Inconclusive)"' \
  "$TASK8_EVIDENCE/task8.trx"
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 -v minimal
mv engine/mods/ra2 "$FINAL_HOST_FIXTURE/engine-ra2-link.used"
unset DYLD_LIBRARY_PATH
test ! -e engine/mods/ra2
/private/tmp/nuke-hour-task0-python311/bin/python -m unittest discover \
  -s packaging/tests -p 'test_*.py' -v
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
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Game/OpenRA.Game.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore -m:1
git diff --check
```

Run Task 15's committed-source artifact mode from this clean final clone. It must create its own fresh Personal/PublicClean clones, strictly materialize checked-in official+`0001..0027`, and may not inspect a staged diff or reuse any Task 15 variable:

```bash
set -euo pipefail
set +x
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
: "${IOS_CODESIGN_KEY:?set the Personal Apple Development signing identity}"
: "${IOS_PROVISION_UUID:?set the Personal provisioning profile UUID}"
FINAL_ARTIFACT_SOURCE="$(git rev-parse --show-toplevel)"
FINAL_ARTIFACT_SOURCE_HEAD="$(
  git -C "$FINAL_ARTIFACT_SOURCE" rev-parse HEAD
)"
test "$(shasum -a 256 "$OPENRA_OFFICIAL_ZIP" | awk '{print $1}')" = \
  ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a
test -z "$(git -C "$FINAL_ARTIFACT_SOURCE" \
  status --porcelain=v1 --untracked-files=all)"
test -z "$(git -C "$FINAL_ARTIFACT_SOURCE" diff --name-only)"
test -z "$(git -C "$FINAL_ARTIFACT_SOURCE" diff --cached --name-only)"

FINAL_ARTIFACT_CONTROL="$(
  mktemp -d /private/tmp/nuke-hour-final-artifact-gate.XXXXXX
)"
FINAL_ARTIFACT_CLONE="$FINAL_ARTIFACT_CONTROL/repository"
FINAL_ARTIFACT_ROOT="$FINAL_ARTIFACT_CONTROL/artifacts"
git clone --shared --no-hardlinks \
  "$FINAL_ARTIFACT_SOURCE" "$FINAL_ARTIFACT_CLONE"
test "$(git -C "$FINAL_ARTIFACT_CLONE" rev-parse HEAD)" = \
  "$FINAL_ARTIFACT_SOURCE_HEAD"
test -z "$(git -C "$FINAL_ARTIFACT_CLONE" \
  status --porcelain=v1 --untracked-files=all)"
mkdir "$FINAL_ARTIFACT_ROOT"
test -z "$(find "$FINAL_ARTIFACT_ROOT" \
  -mindepth 1 -maxdepth 1 -print -quit)"

bash "$FINAL_ARTIFACT_CLONE/ios/scripts/run-retail-import-artifact-gates.sh" \
  --mode committed --repository "$FINAL_ARTIFACT_CLONE" \
  --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --output-root "$FINAL_ARTIFACT_ROOT"
test -f "$FINAL_ARTIFACT_ROOT/NUKE HOUR.ipa"
test ! -e "$FINAL_ARTIFACT_ROOT/NUKE HOUR-PublicClean.ipa"
test "$(git -C "$FINAL_ARTIFACT_CLONE" rev-parse HEAD)" = \
  "$FINAL_ARTIFACT_SOURCE_HEAD"
test -z "$(git -C "$FINAL_ARTIFACT_CLONE" \
  status --porcelain=v1 --untracked-files=all)"
```

The committed runner must build/package/audit the Personal app+IPA successfully; separately build the PublicClean expected-failure pre-sign diagnostic/report and run only its source, bundle-absence, public-brand, AOT-entrypoint, no-retail, and synthetic IPA-parser/tamper audits. PublicClean must retain the nonzero build result and frozen 580 delta; do not launch it, require either profile manifest, construct its importer/game, claim `RuntimeResourcesMissing` was observed at runtime, require/fabricate a PublicClean IPA, or relabel the historical report as success.

- [ ] **Step 3: Audit the commit and file boundary**

Require:

- no staged or committed retail extension/data, private fixture, acceptance evidence, credentials, profile, `_CodeSignature`, `artifacts`, `bin`, `obj`, caches, symlink, secret-like name, or file above 50 MB;
- exact task commits and exact patch delivery commits only;
- desktop RA2 startup/importer paths characterized unchanged;
- Personal runtime evidence proves the unconditional complete pre-game gate; configuration-aware source and bundle audits prove PublicClean cannot bypass into importer/game while its 17 profile resources are absent, without claiming that PublicClean executed that gate;
- every modified engine byte is represented in exactly one of `0020`...`0027` relative to its predecessor;
- Tasks 0–17 themselves created no GitHub remote, push, tag, repository, source publication, or public release action.

- [ ] **Step 4: Record the honest completion state**

Importer implementation is complete only if Task 16's legal-retail physical-device evidence passes. Otherwise report the precise blocked gate while retaining all passing source/synthetic/simulator evidence.

After importer acceptance, execute the separate [Personal Ad Hoc release closure plan](2026-08-30-ios-personal-ad-hoc-release-closure.md) before claiming the Personal IPA goal complete. That plan narrows the approved About/source work to a mutually exclusive registered-device `PersonalDistribution` path while PublicClean remains a separate historical-failure diagnostic; it does not authorize PublicClean, App Store, TestFlight, GitHub, or publication actions. After Task 21 forms the exact final clean source commit, Task 22 only verifies an externally supplied immutable corresponding-source URL. Creating that URL requires separate direct user authorization outside both plans; absent authorization, inaccessible/moving URL, commit mismatch, or incomplete source/build material must produce `SourcePublicationUnavailable` and stop. A narrowly authorized source-only publication does not authorize an app/IPA release or PublicClean. Task 20 also supersedes Task 15's pre-`0028` official-engine asset manifest using the exact reviewed `0028` candidate; Task 15's Apple Development-signed IPA is an audited intermediate, not the final independently distributable artifact.

## Completion criteria

This plan is complete when all of the following are true:

- Base import from extracted files and an extracted folder produces an immutable, receipted active catalog with at least one real Base-compatible two-player map;
- optional Yuri/music/map failures cannot erase or block an independently valid Base generation;
- untrusted archive/map/remote metadata bytes are bounded before existing allocation-heavy parsers;
- retail and downloaded maps are exposed only by verified immutable snapshots, with bounded preview memory and crash-safe generations;
- Personal runs the complete recovery/readiness gate and desktop behavior is unchanged; PublicClean is configuration-audited to fail closed before importer/game construction while its 17 profile resources remain absent;
- in-game content management only creates a next-launch pending import;
- every engine change is strictly replayable as `0020`...`0027`; Personal importer, branding, AOT, no-retail app, and Task 15 development-signed IPA audits pass; PublicClean's expected-failure pre-sign/source/bundle and synthetic IPA-parser audits prove exact profile-resource absence and preserve the historical 580 diagnostic without a launch or IPA claim; that Personal IPA remains only an intermediate;
- private acceptance proves a real skirmish without publishing retail files, paths, hashes, or evidence artifacts;
- no claim is made that PublicClean launched, selected a profile, observed `RuntimeResourcesMissing` at runtime, became playable, produced an IPA, or that source was published; only the linked Personal Ad Hoc closure may establish the narrower registered-device Personal IPA claim.
