# iOS Retail Content Import Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Personal and PublicClean iOS/iPadOS builds import a legally owned extracted Red Alert 2 installation safely, preserve a verified RA2 Base installation across failures, and permit entry only when the active profile can launch a real Base-compatible skirmish map.

**Architecture:** A bounded discovery/validation pipeline produces an immutable candidate catalog; crash-safe generation transactions atomically publish only complete catalogs under Application Support. Fixed Base/Yuri manifest entries and a deterministic runtime-contract digest isolate network, save, replay, and settings compatibility. A read-only active-catalog lease is the only retail mount, while separately bounded remote metadata and runtime-map snapshot services preserve community-map downloads without making the retail mount writable. UIKit supplies coordinated Files access and a pure state-derived wizard; engine changes ship as strict sequential patches `0019` through `0026`.

**Tech Stack:** C# / .NET 8, OpenRA MiniYaml/MIX/Map APIs, UIKit and Foundation, NUnit, Python 3.11 `unittest`, Apple filesystem primitives, shell packaging, strict `diff -ruN` engine-patch replay.

---

## Authoritative design and release boundary

- Authoritative design: `docs/superpowers/specs/2026-08-28-ios-retail-content-import-hardening-design.md`.
- About/source compliance is deliberately deferred to `docs/superpowers/specs/2026-08-28-ios-about-source-compliance-design.md` until importer and PublicClean runtime closure both pass.
- First release accepts individual extracted retail files or an extracted install folder. It does not open ISO/CAB/HDR/ZIP/RAR/7z media and never downloads retail/Demo assets.
- RA2 Base plus one verified Base-compatible two-spawn map is required. Yuri, RA2 music, Yuri music, and additional maps are isolated optional groups.
- Both Personal and PublicClean iOS builds run the same pre-game importer/recovery/readiness gate. Desktop `mod.yaml` and desktop importer behavior stay unchanged.
- Runtime community-map download is a distinct bounded service. It is not a retail source and never makes the retail catalog writable.
- The current PublicClean 580-error report is historical failure evidence, not a passing release baseline. This plan adds importer-specific audits but does not weaken or relabel the separate zero-error PublicClean runtime gate.
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
retail/runtime map filename: {canonicalLowercaseMapUid}.oramap
```

## Git, worktree, and staging protocol

- The reviewed planning snapshot is based on branch `engine-20250330` at `68f5e92`. Before Task 0, make one documentation-only handoff commit containing exactly this plan and `docs/superpowers/specs/2026-08-28-ios-retail-content-import-hardening-design.md`; record that commit as `PLAN_COMMIT`. No implementation work starts directly from the uncommitted planning checkout.
- Use a dedicated `codex/ios-retail-import-hardening` worktree. Never implement this plan in a directory containing the user's unrelated untracked artifacts.
- Preserve all retail content, private fixture paths/hashes, credentials, `artifacts/`, `docs/testing/results/`, `__pycache__`, `_CodeSignature`, `obj`, and `bin` outside commits.
- Each task's **Files** list is its exact staging allowlist. Expand it to a byte-sorted `/private/tmp/ios-retail-task-N.paths`, stage only those paths, compare with `git diff --cached --name-only --no-renames`, then run `git diff --cached --check`.
- New files under ignored `engine/` require `git add -f -- <exact-file>`; never force-add a directory and never use unrestricted `git add -A`.
- Reject staged secrets, retail extensions/data, symlinks, generated build outputs, and files over 50 MB before every commit.
- Run the listed RED command before production edits and record its actual failing assertion/compile symbol. A characterization that is already GREEN must be identified as such and cannot be presented as RED.
- Run the listed GREEN command plus `git diff --check` before every development commit.

## Engine patch series

Each engine phase is delivered immediately, before later tasks touch the same engine files:

1. `0019-ios-retail-archive-envelopes.patch`
2. `0020-ios-retail-runtime-profiles.patch`
3. `0021-ios-retail-runtime-contract.patch`
4. `0022-ra2-map-conversion-service.patch`
5. `0023-verified-oramap-map-cache.patch`
6. `0024-verified-remote-map-metadata.patch`
7. `0025-verified-runtime-map-store.patch`
8. `0026-ios-retail-active-catalog-lifecycle.patch`

For every patch:

- extract the pinned official `OpenRA-release-20250330` ZIP with SHA-256 `ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a` into a fresh temporary tree;
- replay exactly `0001` through the immediate predecessor with `/usr/bin/patch --verbose -p1 --batch -V none`;
- allow only the README-pinned two `0007` diagnostics and reject every other offset/fuzz, nonzero exit, `.rej`, or `.orig`;
- build the candidate from a post-predecessor `a/` tree and exact workspace `b/` allowlist using `diff -ruN`;
- run `packaging/verify_engine_patch_delivery.py` with the exact predecessor range and allowlist;
- require strict `git apply --check`, Apple patch `-F 0` with no diagnostics, byte equality, duplicate forward refusal with unchanged full-tree manifest, and reverse dry-run success;
- commit only the new patch plus the exact README/verifier-test updates after the feature commit.

Before any patch task, export `OPENRA_OFFICIAL_ZIP` as the absolute path to the pinned ZIP and use this fail-closed shell preamble; no command in a task may rely on an unassigned `OFFICIAL_ZIP`, `ALLOWLIST`, or verification directory:

```bash
: "${OPENRA_OFFICIAL_ZIP:?set the absolute path to the pinned official ZIP}"
test "$(shasum -a 256 "$OPENRA_OFFICIAL_ZIP" | awk '{print $1}')" = \
  ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a
PATCH_STAGE="$(mktemp -d /private/tmp/nuke-hour-engine-patch.XXXXXX)"
OFFICIAL_ZIP="$OPENRA_OFFICIAL_ZIP"
ALLOWLIST="$PATCH_STAGE/allowlist.txt"
VERIFY_DIR="$PATCH_STAGE/verification"
test ! -e "$VERIFY_DIR"
```

Each task writes its literal byte-sorted exact allowlist to `ALLOWLIST`, then invokes Task 1's checked helper (available before `0019`):

```bash
python3 packaging/engine_workspace.py candidate \
  --repository "$PWD" --official-zip "$OFFICIAL_ZIP" \
  --patch-end "$PREDECESSOR_END" --workspace engine \
  --allowlist "$ALLOWLIST" --output "$CANDIDATE"
python3 packaging/verify_engine_patch_delivery.py \
  --repository "$PWD" --official-zip "$OFFICIAL_ZIP" \
  --expected-zip-sha256 ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a \
  --predecessor-start 1 --predecessor-end "$PREDECESSOR_END" \
  --allowlist-file "$ALLOWLIST" --candidate "$CANDIDATE" \
  --workspace engine --output-dir "$VERIFY_DIR"
```

The task assigns literal `PREDECESSOR_END` and `CANDIDATE`, and writes the full allowlist from its displayed list—never a glob or inferred Git diff. The helper performs the fresh `a`/`b` replay/overlay and exact `diff -ruN` exit handling; the independent verifier repeats extraction/replay and all strict gates. Preserve the unique stage directory and `evidence.json` until that patch's feature/delivery commits and final hash report are complete.

---

### Task 0: Create the isolated implementation worktree and baseline evidence

**Files:**

- Modify and commit before worktree creation: `docs/superpowers/specs/2026-08-28-ios-retail-content-import-hardening-design.md`
- Create and commit before worktree creation: `docs/superpowers/plans/2026-08-29-ios-retail-content-import-hardening.md`
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
git add -- \
  docs/superpowers/specs/2026-08-28-ios-retail-content-import-hardening-design.md \
  docs/superpowers/plans/2026-08-29-ios-retail-content-import-hardening.md
git diff --cached --name-only --no-renames
git diff --cached --check
git commit -m "Plan iOS retail content hardening"
PLAN_COMMIT="$(git rev-parse HEAD)"
test -n "$PLAN_COMMIT"
```

The staged list must contain exactly the authoritative importer design and this implementation plan. If either is still untracked/modified after the commit, or any unrelated path is staged, stop before creating a worktree.

- [ ] **Step 2: Create a clean worktree**

Use the worktree skill's preferred location and branch `codex/ios-retail-import-hardening`. Confirm:

```bash
git branch --show-current
git status --short
git log -3 --oneline
```

Expected: the implementation worktree is rooted at `PLAN_COMMIT`, has no tracked or untracked changes, and contains the authoritative importer design plus this reviewed plan.

Because a Git worktree contains only the patched tracked subset under `engine/`, hydrate the rest from a verified official replay before any restore/build. Do not copy the ignored engine tree from the original checkout:

```bash
PROJECT_REPOSITORY=/Users/kevin/Projects/ra2-mac
WORKTREE_ROOT="$PWD"
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
BOOTSTRAP_STAGE="$(mktemp -d /private/tmp/nuke-hour-engine-bootstrap.XXXXXX)"
BOOTSTRAP_ALLOWLIST="$BOOTSTRAP_STAGE/0018.allowlist"
printf '%s\n' \
  OpenRA.Game/FluentProvider.cs \
  OpenRA.Game/LanguageSelectionPolicy.cs \
  OpenRA.Game/Settings.cs \
  OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs \
  OpenRA.Mods.Common/Widgets/Logic/Settings/DisplaySettingsLogic.cs \
  OpenRA.Test/IosMenuLayoutPolicyTest.cs \
  OpenRA.Test/IosSettingsLayoutTest.cs \
  OpenRA.Test/LanguageSelectionPolicyTest.cs \
  OpenRA.Test/PublicBrandingPolicyTest.cs \
  OpenRA.Test/SettingsTest.cs > "$BOOTSTRAP_ALLOWLIST"
python3 "$PROJECT_REPOSITORY/packaging/verify_engine_patch_delivery.py" \
  --repository "$PROJECT_REPOSITORY" \
  --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --expected-zip-sha256 ef593816d1a15443c82e865dfd1efa48e9c82e249245b208ce80e6a2900d9d0a \
  --predecessor-start 1 --predecessor-end 17 \
  --allowlist-file "$BOOTSTRAP_ALLOWLIST" \
  --candidate "$PROJECT_REPOSITORY/engine-patches/0018-nuke-hour-brand-language.patch" \
  --workspace "$PROJECT_REPOSITORY/engine" \
  --output-dir "$BOOTSTRAP_STAGE/verification"
set -- "$BOOTSTRAP_STAGE/verification/fresh-official-extraction"/*
test "$#" -eq 1
test -d "$1"
ditto "$1" "$WORKTREE_ROOT/engine"
test -z "$(git status --short)"
```

The source of hydrated bytes is only the verifier's fresh replay. The final clean-status assertion proves its tracked patched files equal this worktree; ignored official baseline files are now available for restore/build without becoming Git candidates.

- [ ] **Step 3: Record baseline tests**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet restore \
  engine/OpenRA.Test/OpenRA.Test.csproj -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet restore \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 -v minimal

python3 -m unittest discover -s packaging/tests -p 'test_*.py'
./utility.sh --check-yaml
```

Record counts and existing warnings under `/private/tmp/ios-retail-import-baseline/`; do not commit logs. Any pre-existing failure that overlaps importer/map/profile code is resolved before Task 1; unrelated failures are recorded but never relabeled as passing.

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

public readonly record struct RetailImportProgress(
	RetailImportPhase Phase,
	RetailContentGroup? Group,
	int CompletedItems,
	int? TotalItems,
	long CompletedBytes,
	long? TotalBytes,
	string? SafeBasename)
{
	public int? Percentage => TotalBytes is > 0
		? (int)Math.Min(100m, (decimal)CompletedBytes * 100m / TotalBytes.Value)
		: TotalItems is > 0
			? (int)Math.Min(100m, (decimal)CompletedItems * 100m / TotalItems.Value)
			: null;
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
	string? SafeBasename,
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

public readonly record struct RetailSourceId(
	int RootIndex,
	string CanonicalRelativePath);

public readonly record struct RetailStagingTarget
{
	public string CanonicalRelativePath { get; }
	public RetailStagingTarget(string canonicalRelativePath);
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
		RetailSourceEntry? entry);
	public RetailSourceReadKind Kind { get; }
	public RetailSourceEntry? Entry { get; }
	public static RetailSourceRead Item(RetailSourceEntry entry);
	public static RetailSourceRead Complete();
}

public sealed class RetailOperationResult<T>
{
	private RetailOperationResult(
		T value,
		RetailImportFailure? failure,
		bool succeeded);
	public bool Succeeded { get; }
	public T Value { get; }
	public RetailImportFailure? Failure { get; }
	public static RetailOperationResult<T> Success(T value);
	public static RetailOperationResult<T> Failed(RetailImportFailure failure);
}

public sealed class RetailOperationResult
{
	private RetailOperationResult(
		RetailImportFailure? failure,
		bool succeeded);
	public bool Succeeded { get; }
	public RetailImportFailure? Failure { get; }
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

public readonly record struct RetailValidationDeadline(
	long StartTimestamp, TimeSpan Budget)
{
	public static RetailValidationDeadline Start(
		IRetailMonotonicClock clock, TimeSpan budget);
	public TimeSpan Remaining(IRetailMonotonicClock clock);
	public void ThrowIfExpired(IRetailMonotonicClock clock);
}
```

`Percentage` is derived only from a known positive byte denominator or, when bytes are not meaningful, a known positive item denominator. Provider preparation, discovery, structure/conversion/profile/mount/runtime checks, committing, and recovery remain indeterminate. `RetailOperationResult<T>` has exactly one state: successful with a non-null value, or failed with one immutable `RetailImportFailure`; its constructor is private and its factories reject a null value/failure. Provider, coordinated-read, and storage seams map platform exceptions into this closed result before returning; only cooperative cancellation may propagate `OperationCanceledException`, and raw `NSError`, `IOException`, exception text, paths, or provider strings never cross the coordinator/UI/log boundary. Every record constructor defensively copies and ordinal-sorts collection inputs into `ImmutableArray`/`ImmutableDictionary`, rejects null/duplicate/noncanonical entries, and is proven unchanged after the caller mutates its original collection. `RetailStagingTarget` accepts one canonical relative path beneath the session-bound staging root and rejects absolute, empty, `.`, `..`, alternate-separator, NUL, or symlink-component targets. Validation deadlines use only the injected monotonic clock, checked timestamp arithmetic, and never wall time. Persisted seven-day pending expiry uses the separate UTC provider and a canonical UTC instant; a future seal timestamp, wall-clock rollback, invalid offset, or ambiguous encoding fails closed without touching the active generation. Introduce these types alongside the characterized legacy desktop request/result for now; iOS code must use only the new session/typed-failure path. Task 14 removes the legacy raw-path `RetailImportRequest`, free-form `Message`, and untyped `Hashes` fields atomically with the final test migration, so every final source access flows through `IRetailImportSourceSession` without breaking the intervening full suites.

`RetailSourceDiscovery` creates the only `RetailDiscoveryBudget` from the exact V1 depth/entry/apparent-byte limits, injected monotonic clock, and already-started ten-minute deadline, then passes it into `OpenEnumerationAsync`. Enumeration is pull-based and yields exactly one entry or `Complete`; it may not materialize a provider directory into an array. The production cursor calls `TryEnterDirectory` *before* asking a provider to enumerate that directory, calls `TryAcceptEntry` before allocating/yielding each item's metadata, and calls `Checkpoint` inside provider/materialization loops. Once a limit/deadline failure occurs the cursor becomes inert, closes provider handles, and returns that same typed failure; it never fetches item `limit + 1`, descends below the accepted depth, or continues after timeout. The neutral discovery loop independently checks ordering/canonicalization and refuses `Complete` if budget/accounting invariants disagree. `RetailSourceRead` factories enforce exactly one valid state. Fakes count underlying provider calls and prove immediate stop at depth, entries, apparent bytes, cancellation, and deadline boundaries—including a provider that could otherwise produce millions of children—so rejection never happens only after an unbounded array was built.

- [ ] **Step 2: Run RED**

```bash
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
engine_workspace.py allowlist --patch-id 0019|0020|0021|0022|0023|0024|0025|0026 \
  --output FILE
```

The helper validates the pinned ZIP SHA, numeric exact predecessor range, safe extraction, exact 0007 diagnostic tuple, no other fuzz/offset/reject/orig, regular non-symlink paths, and byte-sorted unique allowlists. `allowlist` owns the executable exact path sets mirrored by this plan, writes atomically, and tests lock every path/count plus `LC_ALL=C` byte order. `materialize` first captures only allowlisted tracked candidate bytes, proves every other tracked engine path equals the strict replay, fills the full engine tree from that replay, then restores and verifies the allowlisted overlay; it never copies ignored engine bytes from another checkout. `candidate` creates fresh `a` and `b`, overlays only allowlisted workspace bytes into `b`, runs `diff -ruN` with exact exit `1`, validates exact paired headers, and atomically writes the patch. Tests cover traversal, symlink, missing numeric predecessor, outside drift, untracked/ignored pollution, duplicate allowlist entries, creation/deletion, wrong ZIP, wrong 0007 diagnostics, overlay byte identity, allowlist drift, and no partial output.

- [ ] **Step 5: Run GREEN and commit**

Repeat the focused command, then:

```bash
python3 -m unittest packaging.tests.test_engine_workspace -v
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore -m:1
git diff --check
```

Stage only the fourteen listed paths and commit:

```bash
git commit -m "Model bounded iOS retail imports"
```

The new/modified `engine/OpenRA.Test` paths join the `0019` allowlist in Task 2; do not generate a partial patch yet.

---

### Task 2: Harden ZIP, PNG, MIX, IDX, and BAG boundaries and deliver `0019`

**Outer files:**

- Create: `OpenRA.Mods.RA2/Content/RetailArchiveGraphValidator.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailFilenameCatalog.cs`
- Modify: `OpenRA.Mods.RA2/Content/RetailContentCatalog.cs`
- Modify: `OpenRA.Mods.RA2/FileSystem/BagFile.cs`

**Exact `0019` engine allowlist:**

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

- Create: `engine-patches/0019-ios-retail-archive-envelopes.patch`
- Modify: `engine-patches/README.md`
- Modify: `packaging/tests/test_verify_engine_patch_delivery.py`

- [ ] **Step 1: Write RED allocation/structure tests**

Require engine APIs that take immutable limits instead of referencing the RA2 assembly:

```csharp
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
	int MaximumInflateRatio);

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
	public CancellationToken CancellationToken { get; }
	public IPackageReadDeadline Deadline { get; }
	public int Depth { get; }
	public int RemainingEntries { get; }
	public long RemainingAddressedBytes { get; }
	public void ThrowIfCancelledOrExpired();
	public PackageReadContext Descend(int childEntries, long childAddressedBytes);
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
		PackageReadContext context);
}

public sealed class ValidatedMixIndex : IDisposable
{
	public ImmutableArray<ValidatedMixEntry> Entries { get; }
	public Stream OpenEntry(uint hash, PackageReadContext context);
	public void Dispose();
}

public sealed record ValidatedMixEntry(
	uint Hash,
	string? CanonicalName,
	long DataOffset,
	long Length);

public interface IPackageReadDeadline
{
	void ThrowIfExpired();
}
```

`ValidatedZipEntry` and `ValidatedMixEntry` are immutable checked-offset/checked-length records with canonical normalized names or hashes; neither exposes a raw backing path. Passing `ownedPinnedStream` transfers ownership at call entry: failure disposes it, while success transfers it to the returned envelope/index. `OpenPackage` and `OpenEntry` use the already parsed table and the same owned stream identity; they never reopen a path or accept a replacement stream. Each returned package/entry stream acquires an independent reference; disposing the envelope/index prevents new opens but does not invalidate an already-open child, and the final reference closes the backing stream exactly once. `PackageReadContext.Descend` uses checked subtraction, rejects negative charges and limit overruns before allocating, and returns a child context sharing the same cancellation/deadline. The bounded loader follows the existing ownership rule: success transfers the stream to the package; failure restores its initial position and retains caller ownership. RED tests cover envelope-first disposal, child-first disposal, concurrent child reads, failure/cancellation, and zero leaked streams.

RED coverage must include:

- ZIP unknown/negative/inconsistent sizes, duplicate normalized paths, traversal, directory/symlink/special entries, entry and aggregate expansion, and ratio limits;
- PNG invalid signature/IHDR, 4,096 edge, pixel/stride multiplication overflow, chunk/IDAT limits, secondary inflate ratio, truncation, and decoded-byte limit before allocation;
- MIX truncated/encrypted headers, index arithmetic, nesting/entry/addressed-byte budgets, duplicate hash/name, ordinary 512 MiB segment, the complete sparse `language.mix -> audio.mix -> audio.bag` chain, 384 MiB BAG stream, and every `+1` case;
- IDX negative/huge count, checked `12 + count * 36`, invalid/duplicate 16-byte ASCII names, invalid flags/rate/chunk sizes, and `offset + length <= bag.Length`;
- runtime loaders surface typed corruption instead of swallowing `OverflowException`, `EndOfStreamException`, or guarded allocation failures.

- [ ] **Step 2: Run RED**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~ZipEnvelopeTest|FullyQualifiedName~PngTest|FullyQualifiedName~MixEnvelopeTest|FullyQualifiedName~IdxBagEnvelopeTest|FullyQualifiedName~RetailArchiveGraphValidatorTest' \
  -v minimal
```

Expected: missing envelope types plus existing filename-only loader behavior accepting at least one malformed fixture.

- [ ] **Step 3: Implement preflight-before-loader contracts**

`BoundedZipEnvelope` parses the central directory with checked 64-bit arithmetic before `ZipFileLoader` sees the stream. `Png` receives `PngReadLimits` and rejects impossible allocation before creating `Data`. Preserve the existing constructor through a trusted-default overload for bundled engine assets; every untrusted importer/remote-map path must pass explicit V1 limits.

`PackageReadContext` carries one cancellation token, an injected `IPackageReadDeadline`, and depth/entry/addressed-byte counters through every nested loader. The RA2 adapter maps the earlier of the ten-minute archive deadline and enclosing 90-minute validation deadline; every header/index/name/entry loop calls both cancellation and deadline checks. Add an opt-in `IBoundedPackageLoader : IPackageLoader` interface rather than changing the existing `IPackageLoader` method implemented by unrelated desktop loaders. An untrusted context accepts only `IBoundedPackageLoader`; the trusted desktop overload preserves every existing loader. `FileSystem.TryParsePackage` passes the same context into nested MIX/BAG/ZIP parsing. `SegmentStream` gains checked long offset/count support. A validated ZIP envelope binds its parsed index to the same pinned stream; `Open` must not reparse through the legacy loader or permit a stream swap. Fake-deadline tests prove timeout before allocation and no partially validated handle escapes.

`MixEnvelopeValidator` exposes only validated index entries. Ordinary `MixFile.GetContent` rejects a declared segment above 512 MiB before integer conversion. Local/global XCC database reads receive the same name/count/byte budgets. `IdxBagEnvelopeValidator` and `AudioBagLoader` alone may open the structurally identified `audio.bag` through a checked long-length bounded segment stream up to 384 MiB; the loader no longer catch-all suppresses corrupt IDX/BAG. It never calls the ordinary accessor for the BAG leaf.

`RetailArchiveGraphValidator` walks only validated handles, enforces the fixed nesting/aggregate/time budgets, proves the Base/Yuri nested package graph, and returns typed per-group results. `RetailFilenameCatalog` is versioned, ordinal, digestible, and contains only audited RA2/YR names needed for the supported graphs and map discovery; never brute-force MIX hashes.

The graph result also exposes bounded map handles:

```csharp
public sealed class ValidatedEmbeddedMapSource : IDisposable
{
	public string SafeName { get; }
	public long Length { get; }
	public string SourceSha256 { get; }
	public Stream OpenRead();
}

public sealed record RetailArchiveGraphResult(
	ImmutableDictionary<RetailContentGroup, RetailGroupStatus> Groups,
	ImmutableArray<ValidatedEmbeddedMapSource> EmbeddedMaps);
```

Resolve map hashes only through the versioned minimal filename catalog plus a structurally valid archive-local filename database; the importer does not consult the bundled global MIX database and never brute-forces hashes. `RetailArchiveGraphValidatorTest` must cover map-bearing nested packages, valid/invalid local DB, hash collision/duplicate, unknown hash exclusion, recursion/entry/byte/time/cancellation limits, handle lifetime, and no loose-map requirement. External `.map`/`.mpr`/`.yrm` candidates and embedded handles enter Task 9 through the same size/digest/cancellation interface.

- [ ] **Step 4: Run GREEN and builds**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~ZipEnvelopeTest|FullyQualifiedName~PngTest|FullyQualifiedName~MixEnvelopeTest|FullyQualifiedName~IdxBagEnvelopeTest|FullyQualifiedName~RetailArchiveGraphValidatorTest|FullyQualifiedName~RetailContentCatalogTest|FullyQualifiedName~RetailSourceDiscoveryTest|FullyQualifiedName~RetailCandidatePlanTest|FullyQualifiedName~IosImportEnvelopePolicyV1Test' \
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

Task 2 adds envelope-integration cases or fixture capabilities to each of the five paths marked **Modify again**, so every one of the displayed 24 engine paths has a real Task 2 diff. Stage the four outer paths and those exact 24 engine paths; the 24-path set is also the `0019` patch allowlist. Show the complete staged list and commit:

```bash
git commit -m "Harden retail archive envelopes"
```

- [ ] **Step 6: Generate and verify `0019` immediately**

Generate the byte-sorted exact allowlist and assign every common-block variable literally:

```bash
PREDECESSOR_END=18
CANDIDATE=engine-patches/0019-ios-retail-archive-envelopes.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0019 --output "$ALLOWLIST"
```

Run the exact common `candidate` + independent verifier block under **Engine patch series** with those variables.

Require exact header equality, no outside-engine/root-mod paths, byte equality for every tracked engine path, duplicate refusal/tree unchanged, reverse success, and no registry drift. Update README and verifier tests to `0019`, run their focused Python tests, then commit only the three delivery files:

```bash
git commit -m "Deliver retail archive envelope engine patch"
```

---

### Task 3: Split trusted Base/Yuri manifests, add pre-game context, and deliver `0020`

**Outer files:**

- Modify: `OpenRA.Mods.RA2/Content/RetailImportModels.cs`
- Create: `OpenRA.Mods.RA2/Content/Ra2RuntimeProfile.cs`
- Create: `OpenRA.Mods.RA2/Content/Ra2RetailMountContract.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailProfileValidator.cs`
- Create: `OpenRA.Mods.RA2/Content/IosRuntimeResourcePreflight.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/Ra2MapImportModels.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/Ra2MapImportContext.cs`
- Modify: `mods/ra2/mod.yaml`
- Create: `mods/ra2/mod.ios-base.yaml`
- Create: `mods/ra2/mod.ios-yuri.yaml`
- Create: `mods/ra2/manifest/common.yaml`
- Create: `mods/ra2/manifest/gameplay-base.yaml`
- Create: `mods/ra2/manifest/gameplay-yuri.yaml`
- Create: `mods/ra2/manifest/filesystem-desktop.yaml`
- Create: `mods/ra2/manifest/filesystem-ios-base.yaml`
- Create: `mods/ra2/manifest/filesystem-ios-yuri.yaml`
- Modify: `mods/ra2/rules/world.yaml`
- Create: `mods/ra2/rules/world-yuri.yaml`
- Modify: `mods/ra2/rules/defaults.yaml`
- Create: `mods/ra2/rules/defaults-yuri.yaml`
- Modify: `mods/ra2/rules/proxy-actors.yaml`
- Create: `mods/ra2/rules/proxy-actors-yuri.yaml`
- Modify: `mods/ra2/sequences/vehicles.yaml`
- Create: `mods/ra2/sequences/vehicles-yuri.yaml`
- Modify: `mods/ra2/sequences/voxels.yaml`
- Create: `mods/ra2/sequences/voxels-yuri.yaml`
- Modify: `mods/ra2/audio/voices.yaml`
- Create: `mods/ra2/audio/voices-yuri.yaml`
- Modify: `packaging/public-content-manifest.json`
- Modify: `docs/legal/public-asset-sbom.json`
- Modify: `packaging/tests/test_public_brand_delta.py`

**Exact `0020` engine allowlist:**

- Create: `engine/OpenRA.Game/TrustedManifestSelection.cs`
- Create: `engine/OpenRA.Game/ModDataConstructionOptions.cs`
- Create: `engine/OpenRA.Game/PreGameEngineContext.cs`
- Create: `engine/OpenRA.Game/RuntimeContractIdentity.cs`
- Modify: `engine/OpenRA.Game/Manifest.cs`
- Modify: `engine/OpenRA.Game/InstalledMods.cs`
- Modify: `engine/OpenRA.Game/ModData.cs`
- Modify: `engine/OpenRA.Game/Platform.cs`
- Modify: `engine/OpenRA.Game/Game.cs`
- Create: `engine/OpenRA.Test/TrustedManifestEntryTest.cs`
- Create: `engine/OpenRA.Test/PreGameEngineContextTest.cs`
- Create: `engine/OpenRA.Test/Ra2RuntimeProfileManifestTest.cs`
- Create: `engine/OpenRA.Test/RetailCandidateProfileValidationTest.cs`

**Delivery files:**

- Create: `engine-patches/0020-ios-retail-runtime-profiles.patch`
- Modify: `engine-patches/README.md`
- Modify: `packaging/tests/test_verify_engine_patch_delivery.py`

- [ ] **Step 1: Write RED trusted-entry and pre-game tests**

Lock these APIs:

```csharp
public readonly record struct TrustedManifestSelection
{
	public string ModId { get; }
	public string EntryName { get; }
	public TrustedManifestSelection(string modId, string entryName);
	public static TrustedManifestSelection Default(string modId);
}

public sealed record EngineBootstrapConfiguration(
	string EngineDir,
	string SupportDir,
	ImmutableArray<string> ModSearchPaths,
	ImmutableArray<string> ExplicitModPaths,
	Arguments Arguments);

public interface IModDataServiceProvider : IDisposable
{
	bool TryGet<T>(out T service) where T : class;
	void Dispose();
}

public interface IModDataBoundService
{
	IDisposable Bind(ModData modData);
}

public readonly record struct ModDataServiceRegistration
{
	public Type ServiceType { get; }
	public object Service { get; }
	public static ModDataServiceRegistration Owned<T>(T service)
		where T : class;
}

public sealed class ModDataServiceCollection : IDisposable
{
	public bool TryAddOwnedBatch(
		ImmutableArray<ModDataServiceRegistration> registrations);
	public IModDataServiceProvider Seal();
	public void Dispose();
}

public delegate IFileSystemLoader FileSystemLoaderDecorator(
	IFileSystemLoader configuredLoader);

public sealed record ModDataConstructionOptions(
	IModDataServiceProvider Services,
	FileSystemLoaderDecorator DecorateFileSystemLoader)
{
	public static ModDataConstructionOptions TrustedDesktopDefault { get; }
}

public sealed class PreGameModDataScope : IDisposable
{
	public ModData ModData { get; }
	public void Dispose();
}

public sealed class EngineStartupState : IDisposable
{
	public EngineBootstrapConfiguration Configuration { get; }
	public TrustedManifestSelection Selection { get; }
	public RuntimeContractIdentity RuntimeContract { get; }
	public ModDataConstructionOptions ConstructionOptions { get; }
	public void Dispose();
}

public static class Game
{
	public static RunStatus InitializeAndRun(string[] args);
	public static RunStatus InitializeAndRun(
		EngineStartupState ownedStartupState);
}

public sealed class PreGameEngineContext : IDisposable
{
	public static PreGameEngineContext Open(
		EngineBootstrapConfiguration configuration);
	public PreGameModDataScope OpenModData(
		TrustedManifestSelection selection,
		ModDataConstructionOptions options);
	public EngineStartupState SealForGame(
		TrustedManifestSelection selection,
		RuntimeContractIdentity runtimeContract,
		ModDataConstructionOptions options);
}

public readonly record struct RuntimeContractIdentity
{
	public static RuntimeContractIdentity Legacy { get; }
	public string ProfileId { get; }
	public string ContractDigest { get; }
	public bool IsLegacy { get; }
	public RuntimeContractIdentity(string profileId, string contractDigest);
}

public class Manifest
{
	public T Get<T>() where T : IGlobalModData;
	public T Get<T>(ObjectCreator explicitObjectCreator)
		where T : IGlobalModData;
}
```

`TrustedManifestSelection` accepts only one root filename, rejects separators, absolute/traversal/NUL/empty names, and is constructed only from the fixed profile enum. `Manifest` gains an `entryName = "mod.yaml"` constructor and immutable ordinal `EntryName`/`IncludedFiles`; included fragments may not contain another `Include`. `ModData`, same-mod reconstruction, and normal startup must preserve the selected entry.

`RuntimeContractIdentity` accepts either the singleton `Legacy` value or profile `[a-z0-9-]{1,32}` plus 64 lowercase hexadecimal digest; partially initialized/default non-Legacy states fail. `ModDataConstructionOptions.Services` accepts only a sealed `IModDataServiceProvider`; `ModData.GetService<T>()` reads it. `TryAddOwnedBatch` first validates the complete immutable batch (non-null instances, unique service keys, no key already present, and a live unsealed destination) and then either registers every item in ordinal order while atomically taking ownership or returns false without mutating either side. The unsealed collection is disposable and releases every successfully transferred disposable service in reverse order. `Seal` atomically moves registrations and ownership into the provider, leaves the collection inert, and on any construction failure leaves the collection as the sole owner; no caller-visible partially sealed provider exists. Duplicate types, nulls, mutation after `Seal`, reuse after failed/disposed ownership, and double registration of one instance fail closed. Tests inject every validation/seal failure index, prove destination and original owners are unmodified on a rejected batch, and prove abandoning a later partially composed *fresh collection* disposes its earlier successful batches exactly once.

At the end of successful `ModData` construction—after manifest selection, runtime identity, filesystem, rules/object creation, and `MapCache` are final but before the instance escapes—`ModData` invokes every instance-scoped `IModDataBoundService.Bind(this)` in ordinal registration order and owns the returned binding tokens. Bind may capture/validate the instance but may not start background work synchronously. A failed bind disposes prior tokens in reverse order and aborts construction. `ModData.Dispose` first prevents new service work, disposes all binding tokens in reverse order, and only then tears down MapCache/filesystem state. The sealed provider itself remains owned across same-mod reconstruction by `EngineStartupState`/Game, permits only sequential bindings, and disposes its registered disposable service instances once in reverse order at final game shutdown. Tests cover exact bind timing, bind failure rollback, sequential same-mod unbind/rebind, no overlapping ModData binding, reverse disposal, and no callback after provider disposal.

`SealForGame` requires the final immutable construction options explicitly; it cannot infer them from a previously opened scope. `EngineStartupState` carries the defensive-copied bootstrap configuration/arguments, exact selection, runtime identity, sealed service provider, and loader decorator through the new `Game.InitializeAndRun(EngineStartupState)` overload and same-mod reconstruction. The existing `InitializeAndRun(string[])` remains the desktop/legacy entry and internally constructs the equivalent `Legacy` state with immutable empty services and the trusted default loader. Later map/cache/import tasks inject policy/client/store/catalog through this 0020 seam and must not add static global setters; `IModDataBoundService` is the only permitted instance binding path and never stores a static/current `ModData`.

`Manifest.Get<T>(ObjectCreator)` performs lazy default-module creation through the supplied creator and never reads `Game.ModData`; all new pre-game, ModData-construction, candidate, and runtime-validation call sites use it. The parameterless `Get<T>()` remains only as a characterized desktop compatibility adapter and delegates with the current game's creator. Tests install a throwing global sentinel and prove the explicit overload, including missing/default modules such as `DefaultPlayer`, succeeds through the supplied creator while the legacy overload retains its old behavior.

`OpenModData` and `SealForGame` each take ownership only of the objects explicitly returned/passed: a `PreGameModDataScope` owns its no-render `ModData` and decorated loader until `Dispose`; sealing does not consume an open scope. The new `Game.InitializeAndRun` overload takes ownership of `EngineStartupState` at call entry, transfers its immutable options/provider/loader into the loaded `ModData` exactly once, and disposes the remaining state on both initialization failure and normal ownership transfer. The caller must not reuse/dispose it after the call; the legacy string-array overload owns its internally created state. The decorator is invoked once around the normally ObjectCreator-configured loader; it cannot replace other startup state or capture a raw staging root. Same-mod reconstruction reuses the same selection/options/runtime identity, while desktop uses `TrustedDesktopDefault`. Tests lock overload routing, argument defensive copy, call count, ownership transfer, failure disposal, and no reuse/double-dispose.

Tests must also prove identical normalized EngineDir/SupportDir configuration is idempotent; different paths and partial initialization fail without changing global state; pre-game scope creates settings/InstalledMods/no-render `ModData`; and the legacy `Game.ModData` adapter is same-thread, non-nestable, and always restores in `finally`.

- [ ] **Step 2: Run RED**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~TrustedManifestEntryTest|FullyQualifiedName~PreGameEngineContextTest|FullyQualifiedName~Ra2RuntimeProfileManifestTest|FullyQualifiedName~RetailCandidateProfileValidationTest' \
  -v minimal
```

Expected: missing trusted-entry/pre-game APIs; current one-shot directory override and monolithic manifest cannot satisfy the tests.

- [ ] **Step 3: Implement fixed profile selection and candidate validation**

```csharp
public enum Ra2RuntimeProfileId { Base, Yuri }

[Flags]
public enum Ra2MapCompatibleProfiles
{
	None = 0,
	Base = 1,
	Yuri = 2,
}

public sealed record ValidatedMapPlayerMetadata(
	string Name,
	string? Palette,
	string? Bot,
	string? StartingUnitsClass,
	bool AllowBots,
	bool Playable,
	bool Required,
	bool OwnsWorld,
	bool Spectating,
	bool NonCombatant,
	bool LockFaction,
	string? Faction,
	bool LockColor,
	Color Color,
	CPos HomeLocation,
	bool LockSpawn,
	int Spawn,
	bool LockTeam,
	int Team,
	bool LockHandicap,
	int Handicap,
	ImmutableArray<string> Allies,
	ImmutableArray<string> Enemies);

public sealed record ValidatedMapMetadata(
	string Title,
	string Author,
	ImmutableArray<string> Categories,
	string Tileset,
	MapGridType GridType,
	Rectangle Bounds,
	ImmutableArray<ValidatedMapPlayerMetadata> Players,
	ImmutableArray<CPos> SpawnPoints)
{
	public int PlayablePlayers => Players.Count(player => player.Playable);
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
	public static Ra2MapImportContext Create(
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
	public static Ra2MapValidationContext Create(
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

public sealed record IosRuntimeResourcePreflightResult(
	bool StaticResourcesPresent,
	RetailImportFailure? Failure);

public interface IIosRuntimeResourcePreflight
{
	IosRuntimeResourcePreflightResult Check(
		Manifest resolvedManifest,
		IReadOnlyPackage distributionRoot,
		Ra2RuntimeProfileId profile);
}

public sealed class IosRuntimeResourcePreflight : IIosRuntimeResourcePreflight
{
	public IosRuntimeResourcePreflightResult Check(
		Manifest resolvedManifest,
		IReadOnlyPackage distributionRoot,
		Ra2RuntimeProfileId profile);
}
```

`ModDataConstructionOptions.FileSystemLoaderDecorator` receives the normally configured loader and wraps only the candidate `content` mount. It exposes canonical preserved-active or staged files and is disposed/restored in `finally`; it never points SupportDir at staging.

The RA2-layer `Ra2MapImportContext.Create` consumes an engine-neutral `PreGameModDataScope` only on success and wraps it with the fixed RA2 profile/output factory; on failure it disposes the passed scope. `Ra2MapValidationContext.Create` is the no-output sibling used before normal startup. The engine assembly never references an RA2 type. Each resulting pre-game context owns its no-render ModData scope, is sequential, and restores any legacy adapter in `Dispose`; only the import variant owns an output factory. A returned `PreGameModDataScope` remains independently valid until its own disposal, but every such scope is destroyed before normal game startup as required by the design.

For post-start runtime downloads, `CreateBound` creates `Ra2MapValidationContext` from the explicitly bound loaded `ModData` plus an owned ref-counted binding pin; this variant borrows the loaded instance and releases only its pin on disposal. It validates all inputs before taking the pin, disposes the passed pin on a failed result, and is callable only by the instance-scoped Task 12 factory after `IModDataBoundService.Bind`, never from a static/current adapter. No RA2 context reads or replaces `Game.ModData`.

`ValidatedMapMetadata` is the only persistent semantic projection used by receipts and active map snapshots. It defensively copies players in ordinal canonical-name order, preserves spawn-point order because it is an indexed gameplay contract, and copies/sorts each allies/enemies set. `ValidatedMapPlayerMetadata` losslessly covers every `PlayerReference` field used by preview, lobby-slot construction, bot eligibility, save initialization, and world-player setup; nullable source fields remain null instead of acquiring sentinels. Constructors reject duplicate/case-colliding player names, dangling allies/enemies, noncanonical strings, mutable/default immutable arrays, and any value outside the V1 player/spawn/bounds limits. Later map validation fills this projection only after parsing the bounded package once; codecs and active views never reconstruct it by reopening `map.yaml`.

`IosRuntimeResourcePreflight` enforces a static-resource distribution/mount contract, not a gameplay smoke test. From the resolved selected manifest and a versioned closed inventory it requires each renderer shader, referenced required system package, RA2 UI/loadscreen/font resource, profile manifest fragment, and required shell/startup resource to be a regular source-controlled bundle entry with the expected digest and mountability; it never includes/imports retail bytes and never accepts a directory wildcard. The result text may say only “runtime resources present,” never “gameplay verified.” Tests cover a complete Personal fixture, the currently known PublicClean missing set, one-byte/one-file tamper, wrong profile, symlink, optional resource absence, and the absence of any claim that renderer/gameplay ran. Task 8's real candidate validator and Task 14 cold startup both invoke the same injected instance; `RuntimeResourcesMissing` is typed and cannot be bypassed by a Ready retail receipt.

`RetailProfileValidator` makes Yuri *eligible* only after the complete Yuri pair validates; final selection occurs after Task 10 has validated the resulting map set. Partial/corrupt Yuri is quarantined from the candidate view and Base is validated instead. A complete Yuri pair with zero Yuri-compatible maps remains authenticated and reusable but inactive, renders the Yuri group `Unavailable` with a closed map-compatibility reason, and selects Base; it may become active on a later transaction that adds a Yuri-compatible map without reselecting the archives. Yuri is selected only when the final set contains at least one receipt whose exact compatibility flags include Yuri. Base always requires at least one Base-compatible map. The candidate profile, every required mount, rule/sequence load, and selected-profile map count must pass before generation preparation. RA2 music becomes Ready only when `theme.mix` validates and its mounted tracks resolve. Because V1 does not yet register Yuri soundtrack definitions, a valid `thememd.mix` has `Validation=Unavailable`, never Ready; tests lock that honest state until a later feature explicitly wires and validates those tracks. Tests cover a complete Yuri pair plus a sole Base-only map (inactive Yuri/Base selection), the same catalog with a Yuri-compatible map (Yuri selection), and reusing the preserved valid Yuri pair when that map arrives later.

- [ ] **Step 4: Split manifests without changing desktop semantics**

`mod.yaml` directly includes common + Base + Yuri + desktop filesystem. The two iOS entries include common + Base and their exact filesystem; Yuri adds only the expansion fragment. No fragment contains `Include`. The unchanged `rules/ai.yaml` is intentionally common: a source audit proves its current `yuri` token is the original RA2 unit and that it contains no Yuri's Revenge-only actor/faction dependency. The profile drift test fails if a future expansion-only AI reference appears without moving it into a reviewed Yuri fragment.

Split the six mixed files listed above into positive Base and Yuri additions. Base retains the original RA2 `yuri`/`YuriVoice`; it excludes the Yuri faction and YR-only mutations, chaos/magnetic entries, proxies, sequences, voxels, and voices. A drift test resolves all three entries and proves desktop lists are byte-for-byte equivalent to the pre-split characterization and `Yuri = Base + exact classified additions`.

The iOS filesystem fragments use active mount `content`; Base requires the Base graph and makes `theme.mix` optional; Yuri requires the complete Base+Yuri graph and makes both music groups optional. Both use canonical mod ID `ra2` and the same map identity.

Refresh only exact existing manifest/SBOM entries and add new source entries with honest current classification; do not claim new provenance or make private resources public. Add an inventory assertion comparing path/hash/size/category/license/public in both JSON files.

- [ ] **Step 5: Run GREEN, YAML, builds, and commit**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~TrustedManifestEntryTest|FullyQualifiedName~PreGameEngineContextTest|FullyQualifiedName~Ra2RuntimeProfileManifestTest|FullyQualifiedName~RetailCandidateProfileValidationTest' \
  -v minimal
./utility.sh --check-yaml
python3 -m unittest packaging.tests.test_public_brand_delta -v
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Game/OpenRA.Game.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore -m:1
git diff --check
```

Stage only the listed outer and engine paths above and commit:

```bash
git commit -m "Add verified RA2 runtime profiles"
```

- [ ] **Step 6: Generate and verify `0020` immediately**

Generate from official+`0001`…`0019` with exactly the 13 engine paths:

```bash
PREDECESSOR_END=19
CANDIDATE=engine-patches/0020-ios-retail-runtime-profiles.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0020 --output "$ALLOWLIST"
```

Run the exact common block. After strict replay succeeds, update README/verifier tests and commit only the three delivery paths:

```bash
git commit -m "Deliver RA2 runtime profile engine patch"
```

---

### Task 4: Build the deterministic runtime contract and network compatibility

**Outer files:**

- Create: `OpenRA.Mods.RA2/Content/Ra2RuntimeContract.cs`
- Create: `OpenRA.Mods.RA2/Content/RuntimeBuildIdentity.cs`
- Create: `OpenRA.Mods.RA2/Content/RuntimeBuildIdentityReader.cs`
- Create: `OpenRA.Mods.RA2/Content/Ra2RuntimeContractBuilder.cs`
- Modify: `OpenRA.Mods.RA2/Content/Ra2RuntimeProfile.cs`
- Modify: `OpenRA.Mods.RA2/Content/RetailProfileValidator.cs`
- Modify: `OpenRA.Mods.RA2/Content/RetailImportModels.cs`
- Modify: `build/OpenRA.AotRegistry.Generator/Program.cs`
- Create: `ios/OpenRA.iOS/IosRuntimeBuildIdentityProvider.cs`
- Modify: `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs`

**Engine files for `0021` begun in this task:**

- Modify: `engine/OpenRA.Game/Manifest.cs`
- Modify: `engine/OpenRA.Game/ModData.cs`
- Modify: `engine/OpenRA.Game/Game.cs`
- Modify: `engine/OpenRA.Game/Network/Handshake.cs`
- Modify: `engine/OpenRA.Game/Network/UnitOrders.cs`
- Modify: `engine/OpenRA.Game/Server/Server.cs`
- Modify: `engine/OpenRA.Game/Network/GameServer.cs`
- Modify: `engine/OpenRA.Game/Network/Session.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/ServerListLogic.cs`
- Create: `engine/OpenRA.Test/RuntimeContractBuilderTest.cs`
- Create: `engine/OpenRA.Test/RuntimeContractHandshakeTest.cs`

- [ ] **Step 1: Write RED canonicalization and handshake tests**

```csharp
public sealed record ManagedImageIdentity(
	string BundleFilename,
	string Sha256,
	Guid ModuleMvid);

public sealed record RuntimeBuildIdentity(
	ImmutableArray<ManagedImageIdentity> ManagedImages,
	string AotRegistryIdentity);

public interface IRuntimeBuildIdentityProvider
{
	RetailOperationResult<RuntimeBuildIdentity> Capture();
}

public interface IRuntimeManagedImageSource
{
	ImmutableArray<string> EnumerateAppLocalManagedImages();
	IRuntimeManagedImageLease AcquirePinned(string canonicalBundleFilename);
}

public interface IRuntimeManagedImageLease : IDisposable
{
	string CanonicalBundleFilename { get; }
	long Length { get; }
	Guid ModuleMvid { get; }
	Guid? LoadedModuleMvid { get; }
	Stream OpenRead();
	void VerifyUnchanged();
}

public sealed class RuntimeBuildIdentityReader
{
	public RuntimeBuildIdentityReader(
		IRuntimeManagedImageSource images,
		string canonicalAotRegistryIdentity);
	public RetailOperationResult<RuntimeBuildIdentity> Capture();
}

public sealed class IosRuntimeBuildIdentityProvider :
	IRuntimeBuildIdentityProvider
{
	public IosRuntimeBuildIdentityProvider(
		IRuntimeManagedImageSource bundleImages,
		string canonicalAotRegistryIdentity);
	public RetailOperationResult<RuntimeBuildIdentity> Capture();
}

public sealed record RuntimeContractResourceDescriptor(
	string LogicalName,
	long Length,
	string Sha256);

public interface IRuntimeContractResourceSource
{
	ImmutableArray<RuntimeContractResourceDescriptor> Resolve(
		TrustedManifestSelection selection,
		Manifest resolvedManifest);
	Stream OpenVerifiedRead(RuntimeContractResourceDescriptor descriptor);
}

public sealed record Ra2RuntimeContract(
	Ra2RuntimeProfileId ProfileId,
	string ContractDigest,
	string FilenameCatalogDigest,
	int MapFormatVersion);

public sealed class Ra2RuntimeContractBuilder
{
	public Ra2RuntimeContractBuilder(
		IRuntimeContractResourceSource resources,
		RuntimeBuildIdentity buildIdentity);
	public RetailOperationResult<Ra2RuntimeContract> Build(
		Ra2RuntimeProfileId profile,
		TrustedManifestSelection selection,
		Manifest resolvedManifest,
		string filenameCatalogDigest,
		int mapFormatVersion,
		int archiveValidatorVersion,
		int mapValidatorVersion);
}
```

The iOS composition first calls the typed `IRuntimeBuildIdentityProvider.Capture` once and stops on failure. It then constructs `Ra2RuntimeContractBuilder` with that immutable identity and one instance-scoped `IRuntimeContractResourceSource`; candidate validation, repair/refresh, and cold startup all reuse this exact builder/capture. `Build` resolves only the fixed `TrustedManifestSelection` against the supplied resolved manifest, requires its direct nonrecursive fragment/resource descriptor set to equal `Manifest.RuntimeContractFiles`, opens each descriptor once through the same source capability, and writes versioned length-prefixed UTF-8/binary records in ordinal path order over selected entry/direct fragments, every loading/sync-relevant file, filename-catalog digest, map format, both validator versions, final managed DLL SHA-256/MVID, and AOT registry identity. The API has no source-tree root, string path, ambient bundle, or alternate build-identity overload. Enumeration order cannot change the digest; any one input byte/identity change must change it. Default/wrong-profile selection, descriptor/path/hash/length mismatch, duplicate/extra/missing resource, empty image/AOT identity, or mutable caller input returns a closed typed failure. `Ra2RuntimeContract.ToEngineIdentity()` is the sole conversion into Task 3's generic engine type.

The AOT generator emits a deterministic lowercase SHA-256 identity over its canonical ordinal registration records into `GeneratedAotObjectRegistry.g.cs`. Platform-neutral `RuntimeBuildIdentityReader` ordinally inventories **every** app-local `.dll`; there is no `System.*`, `Microsoft.*`, facade, framework, or third-party exclusion. The set must contain the six project images `OpenRA.Game.dll`, `OpenRA.Mods.Cnc.dll`, `OpenRA.Mods.Common.dll`, `OpenRA.Mods.RA2.dll`, `OpenRA.Platforms.Default.dll` (the iOS platform project's actual assembly name), and `OpenRA.iOS.dll`, plus every shipped framework and third-party managed image. Its constructor validates the injected image source and canonical lowercase AOT identity without touching files. `Capture` enumerates and ordinally canonicalizes the closed inventory, then for each name acquires one lease bound to a single no-follow descriptor/file identity; SHA-256 and PE MVID are both parsed from reads of that same pinned descriptor, an already-loaded MVID is compared when present, and `VerifyUnchanged` checks the same identity before disposal. After all leases verify it enumerates the app-local inventory again and requires exact ordinal set equality with the first enumeration, catching an added/removed/renamed DLL during capture. No path is reopened between hashing and MVID, and every lease is disposed exactly once on success or failure. `IosRuntimeBuildIdentityProvider` is only an `NSBundle`/Darwin-pinned-file adapter that delegates its exact typed `Capture` to this reader. A missing/extra/duplicate/non-managed/unparseable `.dll`, path escape, MVID mismatch, empty identity, inventory-set change, or post-capture byte/identity change returns a closed failure before startup sealing. `RuntimeContractBuilderTest` behavior-tests a controlled finished-bundle fixture containing the six project images plus at least one framework image and one third-party parser image, and covers every inventory/tamper/TOCTOU/lease-disposal branch, including replacement of an unloaded image between hash/MVID operations and add/remove between the two enumerations; the iOS build proves the thin adapter compiles/AOTs, and Task 15 compares the real finished-app managed inventory against this exact all-app-local rule.

Both legacy peers remain compatible; exactly one legacy/missing identity, profile mismatch, or digest mismatch is rejected before lobby admission in both directions.

- [ ] **Step 2: Run RED**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~RuntimeContractBuilderTest|FullyQualifiedName~RuntimeContractHandshakeTest' \
  -v minimal
```

Expected: missing identity/builder types and a profile/digest mismatch accepted by the current Mod/Version-only handshake.

- [ ] **Step 3: Implement contract propagation**

`EngineStartupState` and `ModData` continue carrying Task 3's identity; the old desktop overload supplies `Legacy`. Manifest exposes deterministic `RuntimeContractFiles`. Handshake request, response, session global data, and server advertisement add optional `RuntimeProfile`/`RuntimeContract`; client and server both validate before creating/admitting the lobby client. Server browser marks a mismatch unavailable with a typed reason. Task 14 constructs `IosRuntimeBuildIdentityProvider`, builds the RA2 contract, and passes `contract.ToEngineIdentity()` plus the final sealed `ModDataConstructionOptions` into `SealForGame`; no synthetic or source-tree build identity is allowed in a production launch.

- [ ] **Step 4: Run GREEN and commit the Task 4 paths**

Run the focused tests and Release builds for Game, Mods.Common, and RA2. The focused test creates the neutral controlled bundle fixture and executes the managed-file/MVID/TOCTOU tamper matrix. Run the AOT registry generator twice into temporary outputs and require byte identity plus the expected generated registry identity. Build a fresh unsigned `iossimulator-arm64` app with default relative `bin/obj` to compile/AOT the thin iOS adapter, require the checked-in generated registry equals the generator output, and defer finished-app metadata/source auditing to Task 15's explicitly tested audit. Commit only this task's outer and engine paths:

```bash
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
AOT_CHECK_DIR="$(mktemp -d /private/tmp/nuke-hour-aot-identity.XXXXXX)"
/Users/kevin/.dotnet/dotnet run \
  --project build/OpenRA.AotRegistry.Generator/OpenRA.AotRegistry.Generator.csproj \
  --configuration Release --no-launch-profile -- \
  "$AOT_CHECK_DIR/one.cs" engine/bin/OpenRA.Game.dll \
  engine/bin/OpenRA.Mods.Common.dll engine/bin/OpenRA.Mods.Cnc.dll \
  engine/bin/OpenRA.Mods.RA2.dll
/Users/kevin/.dotnet/dotnet run \
  --project build/OpenRA.AotRegistry.Generator/OpenRA.AotRegistry.Generator.csproj \
  --configuration Release --no-launch-profile -- \
  "$AOT_CHECK_DIR/two.cs" engine/bin/OpenRA.Game.dll \
  engine/bin/OpenRA.Mods.Common.dll engine/bin/OpenRA.Mods.Cnc.dll \
  engine/bin/OpenRA.Mods.RA2.dll
cmp "$AOT_CHECK_DIR/one.cs" "$AOT_CHECK_DIR/two.cs"
cmp "$AOT_CHECK_DIR/one.cs" ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet restore ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -p:RuntimeIdentifier=iossimulator-arm64 -m:1
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -c Release -f net8.0-ios -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:CodesignDisable=true -p:TargetPlatform=ios --no-restore -m:1
git diff --check
```

```bash
git commit -m "Define RA2 runtime contract identity"
```

Do not generate `0021` yet; Task 5 completes persistence in the same patch.

---

### Task 5: Scope saves, replays, settings, and skirmish state; deliver `0021`

**Remaining exact `0021` engine paths:**

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

The complete `0021` allowlist is the union of Task 4's 11 engine paths and these 14 paths, with duplicates removed: `Game.cs` and `Server.cs` are intentionally modified in both tasks, so the final union has exactly 23 unique paths.

**Delivery files:**

- Create: `engine-patches/0021-ios-retail-runtime-contract.patch`
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
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~RuntimeContractPersistenceTest' -v minimal
```

Expected: Base/Yuri still share save/replay/server/skirmish state and metadata lacks runtime identity.

- [ ] **Step 3: Implement centralized storage and validation**

Profile only Server settings; keep language/input/player/game/audio/graphics in the shared global file. Route save/replay enumeration and actual load, server save/load, `loadsave` deep links, skirmish state, and browser visibility through the centralized storage API. `Game.JoinReplay` and the actual game-save load entry validate embedded identity even when callers bypass a browser or manually copy a file. Add precise English/Chinese mismatch/legacy-hidden copy. Test Base→Yuri, Yuri→Base, same-profile/different-digest, copied mismatch through browser/direct replay/direct save/deep-link/server entry points, and unchanged desktop Legacy behavior.

- [ ] **Step 4: Run GREEN, full focused builds, and commit**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~RuntimeContractBuilderTest|FullyQualifiedName~RuntimeContractHandshakeTest|FullyQualifiedName~RuntimeContractPersistenceTest' \
  -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Game/OpenRA.Game.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj -c Release --no-restore -m:1
git diff --check
```

Commit the 14 listed engine paths:

```bash
git commit -m "Scope runtime persistence by content contract"
```

- [ ] **Step 5: Generate and verify `0021` immediately**

Generate from official+`0001`…`0020` using the exact 23-path union:

```bash
PREDECESSOR_END=20
CANDIDATE=engine-patches/0021-ios-retail-runtime-contract.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0021 --output "$ALLOWLIST"
```

Run the exact common block; update README/verifier tests only after the fresh replay passes. Commit the three delivery paths:

```bash
git commit -m "Deliver runtime contract engine patch"
```

---

### Task 6: Add canonical generation metadata and root-constrained durable writes

**Production files committed in this task:**

- Create: `OpenRA.Mods.RA2/Content/RetailImportPaths.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportMetadata.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportMetadataCodec.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportDurableFileSystem.cs`

**Deferred ignored engine tests (created and run now, first force-added in Task 14's `0026` feature commit):**

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

Lock records for active pointer, publish journal, catalog, group/map receipts, and pending seal. Tests reject checksum/truncation/trailing bytes, invalid UTF-8, unknown kind/version, overflow, noncanonical generation/commit/hash IDs, absolute/traversal/alternate-separator paths, and any encoded external/account path. A newer version maps to `UpdateRequired`.

```csharp
public enum RetailPublishState
{
	Prepared = 1,
	Publishing = 2,
	Committed = 3,
}

public enum RetailRecoveryDisposition
{
	NoJournal,
	KeptPrevious,
	ConfirmedProposed,
	RestoredPrevious,
	ReadyWithCorruptJournalWarning,
	ResetRequired,
	UpdateRequired,
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
	string? PreviousGenerationId,
	string ProposedGenerationId,
	string CommitId,
	string? SourcePendingId,
	ImmutableArray<RetailContentGroup> AffectedGroups,
	ImmutableArray<string> AffectedRelativePaths);

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

public sealed record RetailReceiptEntry(
	string CanonicalRelativePath,
	long Length,
	string Sha256,
	string? RecognizedEditionCode);

public sealed record RetailGroupReceipt(
	RetailContentGroup Group,
	RetailGroupValidation State,
	RetailEditionRecognition Recognition,
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
	internal string? SourcePendingId { get; }
	internal RetailCandidateArtifactSource(
		string? sourcePendingId,
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
	int BaseCompatibleMapCount,
	int YuriCompatibleMapCount,
	int QuarantinedMapCount,
	RetailImportFailure? Failure);

public sealed record RetailCandidateValidation(
	RetailCandidateCatalog Catalog,
	RetailReadinessResult Readiness);

public sealed record RetailRepairValidation(
	RetailGenerationCatalog Catalog,
	RetailReadinessResult Readiness);

public sealed record RetailGenerationReceipt(
	long Epoch,
	string CommitId,
	string? SourcePendingId,
	Ra2RuntimeProfileId Profile,
	Ra2RuntimeContract Contract,
	string FilenameCatalogDigest,
	int ArchiveValidatorVersion,
	int MapValidatorVersion,
	ImmutableArray<RetailGroupReceipt> Groups,
	ImmutableArray<RetailMapReceipt> Maps,
	MapResidentUsage MapResidentUsage);

public sealed record RetailImportResult(
	bool Published,
	bool PendingSealed,
	string? PendingId,
	RetailReadinessResult? ActiveReadiness,
	RetailImportFailure? Failure);
```

`SourcePendingId` is null for a direct pre-game import and is the exact canonical pending ID for a resumed sealed import. The generation receipt and every state of its publish journal bind the same optional value; a mismatch fails authentication. This linkage is the durable proof used to finish pending consumption after a crash, so recovery never infers an ID from a directory name. Codec tests cover null/non-null round trips, malformed IDs, and independent journal/receipt tampering.

Every collection record defensively copies to an immutable ordinal representation, rejects null/duplicate/out-of-order data where canonical order is required, and cannot change when the caller later mutates its source list/dictionary. Candidate artifacts contain only canonical logical names and transaction-root-relative staged paths plus verified length/hash; no picker path or unrestricted root is representable. `RetailCandidateArtifactSource` is the sealed read capability bound to one transaction snapshot: the same internal constructor receives both the root capability and optional pending ID, and the object itself performs every `OpenVerifiedRead`. A direct/transient source carries null; a pending source carries its exact authenticated pending ID. There is no public constructor, interface, inheritance, delegate, detachable provenance object, or caller-supplied open callback, so an external wrapper cannot pair one source's identity with another source's bytes. `RetailReadinessResult.Groups` is an `ImmutableSortedDictionary` with a closed exact five-key enum-order comparer (Base, RA2 Music, Yuri, Yuri Music, Maps); no missing, extra, or reordered group is encodable. `BaseCompatibleMapCount` and `YuriCompatibleMapCount` are checked projections of the exact receipt flags; Base readiness requires the former to be positive, and a Yuri active profile requires the latter to be positive. `BaseReady` and `YuriReady` are derived helpers, never independently serialized. `RecognizedEditionCode` is null for a structurally compatible unrecognized edition; a non-null value must be one exact closed code from the trusted filename/edition catalog, never an empty/free-form sentinel. Codec tests cover recognized and compatible-unrecognized round trips plus unknown-code tamper. `RetailMapReceipt` is a lossless projection of every trust-relevant `ValidatedMapArtifact` field, including bounded immutable metadata; its 40-lowercase-hex UID is validated by the codec and Task 10 converts it to the strong `MapUid` without reparsing YAML. For a map, `LogicalName` must equal that same lowercase UID string exactly and its generation path is exactly `maps/{uid}.oramap`; neither value is ever derived from source basename, YAML Title, author, localized text, or `SafeName`. `RetailCatalogRepairPlan.RemoveMapLogicalNames` therefore contains only canonical UID strings. Equal UID plus equal package digest/metadata deduplicates; equal UID with any differing package digest, length, or canonical metadata is `DuplicateConflict` and no candidate is chosen. Codec/materialization tests cover Unicode/confusable titles, same basename/different UID, different basename/same UID, UID case changes, path suffix changes, and UID collision tamper. `MapResidentUsage` must equal the checked sum of those exact per-map counters, stays within the global 64 MiB metadata and 32 MiB/262,144 string budgets, and is authenticated identically in candidate/catalog/receipt; mismatched, negative, overflowing, or caller-mutated usage fails. Candidate artifacts refer only to sealed staging. `PrepareAsync` creates a distinct `RetailGenerationCatalog` whose artifacts refer only to canonical generation-relative paths, and no staged DTO is serialized into or accepted by an active catalog. The generation receipt binds transaction epoch/commit, profile/full RA2 contract, filename-catalog digest, validator versions, group receipts, map receipts, and exact resident usage. The journal's affected groups and canonical relative paths are closed, sorted, duplicate-free, and authenticated; corrupt journal bytes are never used to choose a filesystem path. Codec tests mutate constructor inputs and tamper every serialized field independently. `HasPreviousGeneration == false` is valid only for epoch zero/first install and requires a null previous ID. Otherwise the previous ID is mandatory and canonical. The codec never represents “no previous” as an empty path-like string.

- [ ] **Step 2: Run RED**

```bash
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

Run the focused command and RA2 Release build; require `git diff --check`. Keep both ignored engine tests present and passing but unstaged so they do not become outside-allowlist drift for `0022`...`0025`. Commit only the four production paths:

```bash
git commit -m "Add durable retail generation metadata"
```

The two engine tests are deferred members of the exact `0026` allowlist. Record their SHA-256 values and reject any later unreviewed edit before Task 14 force-adds them.

---

### Task 7: Publish whole immutable generations and recover every crash boundary

**Production/build files committed in this task:**

- Create: `OpenRA.Mods.RA2/Content/RetailGenerationStore.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportRecovery.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportProcessLock.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportFaultInjection.cs`
- Create: `build/OpenRA.RetailImport.CrashHarness/OpenRA.RetailImport.CrashHarness.csproj`
- Create: `build/OpenRA.RetailImport.CrashHarness/Program.cs`

**Deferred ignored engine tests (created and run now, first force-added in Task 14):**

- Create: `engine/OpenRA.Test/RetailGenerationStoreTest.cs`
- Create: `engine/OpenRA.Test/RetailImportRecoveryProcessTest.cs`

- [ ] **Step 1: Write RED generation/state-machine tests**

```csharp
public enum RetailPublishDisposition
{
	Published,
	RolledBack,
	Failed,
}

public sealed record RetailGenerationValidationResult(
	bool IsValid,
	RetailReadinessResult Readiness,
	RetailCatalogRepairPlan? Repair,
	RetailImportFailure? Failure);

public sealed class RetailReadyGenerationCapability
{
	internal ActiveGenerationPointer Pointer { get; }
	internal RetailGenerationReceipt Receipt { get; }
	internal RetailReadyGenerationCapability(
		ActiveGenerationPointer pointer,
		RetailGenerationReceipt receipt,
		RetailReadyRootCapability rootCapability);
}

internal sealed class RetailReadyRootCapability { }

public sealed class PreparedRetailGeneration
{
	public string GenerationId { get; }
	public long Epoch { get; }
	public string CommitId { get; }
	public string? SourcePendingId { get; }
	public RetailReadinessResult Readiness { get; }
	internal PreparedRetailGeneration(
		string generationId,
		long epoch,
		string commitId,
		string? sourcePendingId,
		RetailReadinessResult readiness,
		RetailPreparedRootCapability rootCapability);
}

public sealed record RetailPublishResult(
	RetailPublishDisposition Disposition,
	RetailActiveGenerationSnapshot? Active,
	RetailReadyGenerationCapability? Ready,
	RetailReadinessResult? Readiness,
	RetailImportFailure? Failure);

public sealed record RetailRecoveryResult(
	RetailRecoveryDisposition Disposition,
	RetailActiveGenerationSnapshot? Active,
	RetailReadyGenerationCapability? Ready,
	RetailReadinessResult? Readiness,
	RetailImportFailure? Failure);

public sealed record RetailActiveOpenResult(
	RetailActiveGenerationSnapshot? Active,
	RetailReadyGenerationCapability? Ready,
	RetailReadinessResult? Readiness,
	RetailCatalogRepairPlan? Repair,
	RetailImportFailure? Failure);

internal sealed class RetailPreparedRootCapability { }
internal sealed class RetailGenerationRootCapability { }

public sealed class RetailActiveGenerationSnapshot : IRetailCatalogArtifactSource
{
	public ActiveGenerationPointer Pointer { get; }
	public RetailGenerationReceipt Receipt { get; }
	public RetailGenerationCatalog Catalog { get; }
	public Stream OpenVerifiedRead(RetailCatalogArtifact artifact);
	internal RetailActiveGenerationSnapshot(
		ActiveGenerationPointer pointer,
		RetailGenerationReceipt receipt,
		RetailGenerationCatalog catalog,
		RetailGenerationRootCapability rootCapability);
	public void Dispose();
}

public sealed class RetailGenerationStore
{
	public Task<RetailOperationResult<PreparedRetailGeneration>> PrepareAsync(
		RetailCandidateCatalog candidate,
		RetailCandidateArtifactSource artifactSource,
		RetailActiveGenerationSnapshot? active,
		CancellationToken cancellationToken,
		IProgress<RetailImportProgress> progress);

	public Task<RetailOperationResult<PreparedRetailGeneration>> PrepareRepairAsync(
		RetailActiveGenerationSnapshot active,
		RetailGenerationCatalog repairedCatalog,
		RetailCatalogRepairPlan repair,
		CancellationToken cancellationToken,
		IProgress<RetailImportProgress> progress);

	public Task<RetailPublishResult> PublishAsync(
		PreparedRetailGeneration prepared,
		Func<RetailActiveGenerationSnapshot,
			Task<RetailGenerationValidationResult>> postSwitchValidator);

	public Task<RetailRecoveryResult> RecoverAsync(
		Func<RetailActiveGenerationSnapshot,
			Task<RetailGenerationValidationResult>> validator);

	public Task<RetailActiveOpenResult> OpenActiveAsync(
		Func<RetailActiveGenerationSnapshot,
			IProgress<RetailImportProgress>,
			CancellationToken,
			Task<RetailGenerationValidationResult>> validator,
		IProgress<RetailImportProgress> progress,
		CancellationToken cancellationToken);
}
```

Tests prove single-process lock, same-volume materialization, no hard links/in-place edits, one generation per selection, profile/mount/map/contract/readiness revalidation before `Prepared`, pointer-only `Publishing`, complete rollback after post-switch failure, non-cancellable publish/recovery, retained previous generation, and lock-protected GC. The snapshot constructor and root capability are internal to the store; the public API exposes no generation-directory/root/path getter. Its catalog is canonical, already root-contained/authenticated, and usable only with its matching pointer/receipt. Its verified artifact source accepts only catalog artifacts from that exact snapshot. A public-API/reflection test proves ordinary consumers cannot recover or enumerate the raw generation root. `PrepareAsync` constructs the complete proposed generation as the union of verified preserved-active artifacts and staged replacements: a replacement group/map shadows only the matching logical item, every preserved byte is copied via the active snapshot, every staged byte via the sealed staging source, and length/hash are rechecked while materializing. Optional-only Yuri/music/map imports therefore retain Base and all unaffected valid maps without raw-path reopening. It writes the distinct generation-relative catalog and never persists staged names. Before `PrepareRepairAsync`, the real validator applies the exclusion plan to the authenticated active view and produces a complete repaired generation catalog with recomputed profile, runtime contract, readiness, groups, and map receipts. `PrepareRepairAsync` accepts that catalog plus the plan, proves it is a valid subset/reclassification of verified active artifacts, and copies only through the active snapshot into a new proposed generation. Every copy/hash/space/storage-policy/durability failure returns a failed `RetailOperationResult<PreparedRetailGeneration>` and writes no `Prepared` state; only cooperative cancellation throws `OperationCanceledException`. Tests exercise every failure mapping and prove the active generation remains byte-identical. `OpenActiveAsync` authenticates pointer/catalog/receipts, delegates the single content-byte validation pass described below, and returns a typed open result with an authenticated snapshot, repair plan, or closed failure; callers cannot synthesize one from a path or reopen a raw directory. A non-null `Active` in open/publish/recovery results is caller-owned and must be disposed. `Ready` is a separate non-owning opaque proof that the same active pointer/receipt completed post-switch reopen plus full Ready validation; only the store can construct it. It is non-null exactly when the returned active snapshot is Ready, and is null for repair, refresh-needed, no-active, reset, update, rollback failure, or hard-failure outcomes. Tests cover repair/refresh success, failure, cancellation, impossible result cross-products, and exactly one snapshot disposal with no lease/descriptor leak on every branch.

`PrepareAsync` has no independently supplied pending-ID parameter. It reads the optional ID only from the exact sealed `RetailCandidateArtifactSource.SourcePendingId` internal member and carries it unchanged through `PreparedRetailGeneration`, all publish-journal states, and the generation receipt. A direct source contains null; `ResumePendingAsync` can acquire only the sealed source whose internal ID is already bound to its authenticated `PendingId`. `PrepareRepairAsync` preserves an authenticated active receipt's non-null source ID until consumption completes but can never introduce or replace one. Tests reject wrapper, subclass, delegate, source-identity, and pending-ID substitution; repair/refresh preserve the link, and every publish/recovery result preserves the authenticated linkage.

`OpenActiveAsync` authenticates pointer/catalog/receipt structure, then invokes exactly one supplied active-catalog validator. That validator—not the store—owns the content-byte cold hash and reports it through the exact `progress` instance; the store never performs a second hash or fabricates byte counts. Normal startup may cancel before a lease is published. Recovery remains non-cancellable and exposes only the indeterminate `Recovering` activity; its validator receives a non-cancellable token and no determinate percentage is shown. Tests prove callback/progress identity, exactly-once validation, cancellation cleanup, and no duplicate hash pass.

`PrepareAsync` also enforces the full proposed-catalog aggregate before materialization and immediately before publication: at most 256 imported maps, 2 GiB compressed map storage, 64 MiB retained map metadata, 32 MiB/262144 retained untrusted strings, and 8 MiB strings per map. Add 256 individually valid worst-case maps, `limit + 1`, unique-string flood, and checked-sum overflow tests; aggregate rejection leaves the active pointer and previous Ready generation unchanged.

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
	GarbageCollectionStarted,
}
```

At each failpoint, assert recovery converges to exactly the previous or proposed complete generation according to the authenticated pointer state and an outside sentinel remains byte-identical.

The crash harness accepts only a fixed synthetic test root and enum failpoint. It builds synthetic validated metadata, calls `Environment.FailFast`, then a separate `recover` invocation validates the result. It never accepts a retail path or bytes.

- [ ] **Step 3: Run RED**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet restore \
  build/OpenRA.RetailImport.CrashHarness/OpenRA.RetailImport.CrashHarness.csproj -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  build/OpenRA.RetailImport.CrashHarness/OpenRA.RetailImport.CrashHarness.csproj \
  -c Debug --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailGenerationStoreTest|FullyQualifiedName~RetailImportRecoveryProcessTest' \
  -v minimal
```

Expected: missing store/recovery/harness contracts.

- [ ] **Step 4: Implement exact recovery decisions**

| Journal | Authenticated pointer target/epoch | Independently verified generations | Result |
| --- | --- | --- | --- |
| absent | no pointer, first launch | none | `NoJournal`; importer remains Missing |
| absent | valid active | active valid | keep pointer |
| absent | invalid pointer | any | `ResetRequired` |
| `Prepared` | previous | previous valid | keep previous; proposed remains nonactive |
| `Prepared` | previous | previous invalid, proposed valid or invalid | `ResetRequired`; Prepared never authorizes a switch |
| `Prepared` | proposed/other | previous valid | restore previous; never confirm an unauthorized swap |
| `Prepared` | proposed/other | previous invalid | `ResetRequired` |
| `Publishing` | previous | previous valid | keep previous, even when proposed is complete |
| `Publishing` | previous | previous invalid, proposed valid | atomically advance to proposed |
| `Publishing` | previous | previous invalid, proposed invalid | `ResetRequired` |
| `Publishing` | proposed at journal epoch | proposed valid | confirm proposed |
| `Publishing` | proposed | proposed invalid, previous valid | restore previous |
| `Publishing` | proposed | proposed invalid, previous invalid/absent | `ResetRequired` |
| `Committed` | proposed at journal epoch | proposed valid | confirm proposed |
| `Committed` | proposed at journal epoch | proposed invalid, previous valid | restore previous |
| `Committed` | proposed at journal epoch | proposed invalid, no valid previous | `ResetRequired` |
| `Committed` | previous | proposed valid | atomically complete proposed publication |
| `Committed` | previous | proposed invalid, previous valid | keep previous and quarantine inconsistent journal with warning |
| `Committed` | previous | proposed invalid, previous invalid | `ResetRequired` |
| first install `Prepared` | pointer absent | proposed valid | remain Missing; proposed is nonactive and may be resumed/discarded |
| first install `Publishing` | pointer absent | proposed valid | remain Missing; no pointer swap is inferred |
| first install `Publishing` | proposed at journal epoch | proposed valid | confirm proposed |
| first install `Committed` | proposed at journal epoch | proposed valid | confirm proposed |
| first install, any state | pointer/proposed invalid or no complete proposed | `ResetRequired` |
| any known state | pointer epoch/commit does not match journal and no table row proves one whole generation | any | `ResetRequired` |
| any known state/pointer combination not matched above | any | any | `ResetRequired`; recovery has no fallthrough/default guess |
| corrupt fixed journal | active valid | active valid | quarantine fixed journal, warning + Ready |
| corrupt fixed journal | active invalid | none | `ResetRequired` |
| future version | any | any | `UpdateRequired` |

Never parse paths from corrupt bytes. An exhaustive Cartesian test over journal state/version, pointer target/epoch/commit, previous presence/validity, proposed presence/validity, and first-install encoding proves every input matches exactly one row; ambiguity or fallthrough is a test failure. Reset atomically quarantines only the fixed importer root after explicit confirmation. Optional-group corruption is repaired by preparing and publishing a new generation that omits only the invalid optional receipts; it never deletes or edits the active generation in place. A successful publish or repair retains the previous generation until a later cold-start full hash/contract/readiness validation succeeds and every old catalog/map/package lease is released; only then may lock-protected GC remove it.

- [ ] **Step 5: Run GREEN and commit**

Run both commands, RA2 Release build, and diff check. Keep the two ignored engine tests present, byte-recorded, and unstaged. Commit only the six production/build paths:

```bash
git commit -m "Publish retail content with crash-safe generations"
```

The two engine tests join `0026`.

---

### Task 8: Add sealed pending imports, space planning, legacy migration, and orchestration

**Production files committed in this task:**

- Create: `OpenRA.Mods.RA2/Content/RetailPendingSnapshotStore.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailLegacyMigration.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportSpacePlanner.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportCoordinatorCore.cs`
- Modify: `OpenRA.Mods.RA2/Content/RetailContentImporter.cs`
- Modify again: `build/OpenRA.RetailImport.CrashHarness/Program.cs`

**Deferred ignored engine tests (created and run now, first force-added in Task 14):**

- Create: `engine/OpenRA.Test/RetailPendingSnapshotStoreTest.cs`
- Create: `engine/OpenRA.Test/RetailLegacyMigrationTest.cs`
- Create: `engine/OpenRA.Test/RetailContentImporterTransactionTest.cs`

- [ ] **Step 1: Write RED pending/space/migration tests**

Require a complete sealed snapshot with exact plan/hash manifest, 8 GiB cap, seven-day expiry, and no continued security-scope dependency. Persist the seal time as canonical UTC through `IRetailPersistentTimeProvider`; monotonic timestamps are never serialized. Tests recreate the store/provider to simulate process and device restart, then cover exact seven days, one tick below/above, future timestamps, and clock rollback. Suspicious time fails closed and preserves the active pointer. Partial copy is never retryable. Startup resolves publish recovery before any pending/orphan cleanup. Cleanup holds the single import lock and may remove only this transaction's cancelled partial staging or an expired/orphan snapshot proven unreferenced by every authenticated journal/pointer/pending manifest; a journal-referenced directory is never deleted, a second coordinator cannot clean another live transaction, and outside sentinels remain byte-identical. Pending validation failure never changes the active pointer; if active remains Ready, result capabilities include Enter/Retry/Details/Discard.

Space planning uses checked arithmetic over staged source + proposed generation + worst-case converted maps + retained pending + current rollback + metadata + the exact 512 MiB `DiskSafetyMarginBytes`, and reruns immediately before pointer swap. Tests cover margin `-1`, exact, `+1`, unknown source length, and checked overflow without changing active state.

Legacy candidates are only the fixed old `Documents/OpenRA/Content/ra2` and resolved versioned old map directory. They are copied through the same pipeline, never mounted/moved/deleted directly, and successful migration records a durable marker.

- [ ] **Step 2: Run RED**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailPendingSnapshotStoreTest|FullyQualifiedName~RetailLegacyMigrationTest|FullyQualifiedName~RetailContentImporterTransactionTest' \
  -v minimal
```

Expected: missing pending/space/migration contracts and old importer `.staging/current/.previous` behavior changing a partial set.

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
	public RetailCandidatePlan CandidatePlan { get; }
	internal RetailPendingSnapshot(
		string pendingId,
		DateTimeOffset sealedAtUtc,
		DateTimeOffset expiresAtUtc,
		RetailCandidatePlan candidatePlan,
		RetailPendingRootCapability rootCapability);
	public ValueTask DisposeAsync();
}

internal sealed class RetailPendingRootCapability { }

public enum RetailPendingConsumptionState
{
	Prepared,
	Consumed,
}

public sealed record RetailPendingConsumptionMarker(
	int SchemaVersion,
	RetailPendingConsumptionState State,
	string PendingId,
	string GenerationId,
	long Epoch,
	string CommitId);

public enum RetailPendingConsumptionFailpoint
{
	ReadyCapabilityCreated,
	PreparedMarkerFlushed,
	PendingRenamedToTombstone,
	ParentDirectoryFlushed,
	TombstoneDeleted,
	ConsumedMarkerFlushed,
	ConsumptionMarkerCleared,
}

public sealed class RetailPendingSnapshotStore
{
	public Task<RetailOperationResult> ReconcileConsumedAsync(
		RetailReadyGenerationCapability? activeReady);
	public Task<RetailOperationResult> ConsumePublishedAsync(
		RetailReadyGenerationCapability publishedReady);
}

public interface IRetailCandidateValidator
{
	Task<RetailOperationResult<RetailCandidateValidation>> ValidateAsync(
		RetailStagedPlan staged,
		RetailCandidateArtifactSource artifactSource,
		RetailActiveGenerationSnapshot? preservedActive,
		RetailValidationDeadline deadline,
		CancellationToken cancellationToken,
		IProgress<RetailImportProgress> progress);

	Task<RetailOperationResult<RetailRepairValidation>> ValidateRepairAsync(
		RetailActiveGenerationSnapshot active,
		RetailCatalogRepairPlan repair,
		RetailValidationDeadline deadline,
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

public sealed record RetailPendingOpenResult(
	RetailPendingOpenDisposition Disposition,
	RetailPendingSnapshot? Snapshot,
	RetailImportFailure? Failure);

public Task<RetailImportResult> ImportAsync(
	IRetailImportSourceSession sources,
	RetailImportMode mode,
	CancellationToken cancellationToken,
	IProgress<RetailImportProgress> progress);

public Task<RetailPendingOpenResult> OpenPendingAsync(
	CancellationToken cancellationToken);

public Task<RetailImportResult> ResumePendingAsync(
	string pendingId,
	CancellationToken cancellationToken,
	IProgress<RetailImportProgress> progress);

public Task<RetailImportResult> DiscardPendingAsync(
	string pendingId,
	CancellationToken cancellationToken);
```

`RetailContentImporter` receives the validator seam, generation store, pending store, storage planner, injected monotonic clock, and persistent UTC provider. After copying, the pending/transaction store seals the staged manifest and returns one transaction-bound `RetailCandidateArtifactSource`; the importer owns that sealed capability across validation and `PrepareAsync`, passes the exact same instance to both, and disposes it after publish/failure/cancellation. No component reconstructs it from a root, relative-path DTO, interface wrapper, or open delegate. `ActivateBeforeGame` creates one 90-minute whole-validation deadline and supplies that source plus the authenticated preserved active snapshot to the validator, so an optional-only selection composes replacements with verified preserved Base/maps instead of forcing reselection or trusting raw active paths. It runs the full injected archive/profile/map validation, then consumes only successful typed validation and prepare results before publishing the complete candidate; any typed failure is copied into `RetailImportResult` without raw exception text. `ValidateRepairAsync` applies a closed exclusion plan to that authenticated active snapshot and returns a typed, fully revalidated generation catalog/readiness with a recomputed profile and contract; the store never invents those semantics. `SealForNextLaunch` stops after bounded discovery, coordinated copy, envelope validation, and durable pending seal; it never converts maps, opens pre-game `ModData`, publishes, or changes active profile. `OpenPendingAsync` takes the import lock, authenticates the fixed seal/manifest, enforces expiry, and returns a typed disposition plus an opaque snapshot without reopening a provider. `ResumePendingAsync` verifies the requested ID against that snapshot, acquires its sealed source capability whose inseparable internal identity already carries the same ID, rehashes it, and runs full validation/activation without an external source session. `DiscardPendingAsync` removes only that authenticated pending generation after the same lock/recovery ordering and leaves active bytes/pointer unchanged. Torn/expired/mismatched IDs return typed failures. Tests prove source lifetime/identity across validate→prepare, dispose on every branch, rejection of wrapper/subclass/delegate substitution, use-after-dispose rejection, optional Yuri/music/map-only replacement preserving Base, pending resume after restart, expiry, retry, discard, prior Ready survival, Yuri-to-Base repair, and single-map repair. Task 10/14 wire the real validator composition; Task 8 tests orchestration with a deterministic bounded fake and may not contain a temporary trust callback. The characterized legacy desktop overload remains until Task 14 atomically removes it with `RetailContentImporterTest` migration.

Pending consumption is a second, closed crash-consistent state machine and is never an eager directory delete. A resumed publish first commits the generation whose authenticated receipt/journal contains the exact `SourcePendingId`. `ConsumePublishedAsync` accepts no string or ordinary snapshot: it requires the opaque `RetailReadyGenerationCapability` emitted only after post-switch reopen returns full Ready for that exact generation/epoch/commit, rechecks that the current pointer still matches the capability, and obtains the pending ID only from its authenticated receipt. It then writes and fsyncs a versioned, checksummed `Prepared` marker, atomically renames the sealed pending snapshot to its fixed same-root tombstone, fsyncs the parent, deletes the tombstone, writes/fsyncs `Consumed`, and clears the marker. Once publication crosses this point, importer success remains `Published` even if cleanup must finish after restart; `OpenPendingAsync` must not re-expose a pending ID already bound to the authenticated active receipt.

Startup ordering is exact: acquire the import lock; converge generation publish recovery; authenticate/open and fully validate the resulting active generation; pass only its returned Ready capability to `ReconcileConsumedAsync`; only then enumerate, expire, or orphan-clean pending snapshots. If that capability's receipt binds a `SourcePendingId`, the receipt is sufficient to create or resume its consumption marker even when the process died before the first marker write. A marker may roll forward only when all four values—pending ID, generation ID, epoch, and commit ID—match both the capability and the current pointer. With no Ready capability or no exact current match, the sealed pending remains retryable and no marker byte or directory name authorizes deletion. Recovery is idempotent across missing original/tombstone/marker states, rejects future/torn/checksum-invalid markers without using their values as paths, and returns `OpenPendingAsync=Missing` after a committed activation. Real child-process kill tests cover before/after every receipt, pointer, post-switch Ready-capability creation, marker flush, rename, parent-fsync, tombstone delete, `Consumed` flush, and marker-clear boundary; every committed-and-Ready case converges to one Ready generation plus Missing pending, while every uncommitted or non-Ready case preserves the exact retryable seal and previous Ready generation.

- [ ] **Step 4: Run GREEN and commit**

Run focused tests, rebuild the crash harness, run every pending child-process kill case without skips, run the RA2 Release build, and require `git diff --check`. `Program.cs` accepts only the existing fixed synthetic root plus the closed pending failpoint enum and never a retail path. Keep all three ignored engine tests present, byte-recorded, and unstaged. Commit only the six listed production/build paths:

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet restore \
  build/OpenRA.RetailImport.CrashHarness/OpenRA.RetailImport.CrashHarness.csproj -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  build/OpenRA.RetailImport.CrashHarness/OpenRA.RetailImport.CrashHarness.csproj \
  -c Debug --no-restore -m:1
RETAIL_CRASH_HARNESS_DLL="$PWD/build/OpenRA.RetailImport.CrashHarness/bin/Debug/net8.0/OpenRA.RetailImport.CrashHarness.dll"
test -f "$RETAIL_CRASH_HARNESS_DLL"
env DOTNET_ROLL_FORWARD=Major \
  OPENRA_RETAIL_CRASH_HARNESS_DLL="$RETAIL_CRASH_HARNESS_DLL" \
  OPENRA_REQUIRE_PENDING_FAILPOINTS=all \
  /Users/kevin/.dotnet/dotnet test engine/OpenRA.Test/OpenRA.Test.csproj \
  -c Debug --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailPendingSnapshotStoreTest|FullyQualifiedName~RetailLegacyMigrationTest|FullyQualifiedName~RetailContentImporterTransactionTest|FullyQualifiedName~RetailImportRecoveryProcessTest' \
  -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore -m:1
git diff --check
```

`RetailPendingSnapshotStoreTest` must enumerate `Enum.GetValues<RetailPendingConsumptionFailpoint>()` and require one non-skipped child `FailFast`/restart/reconcile result for every value; `OPENRA_REQUIRE_PENDING_FAILPOINTS=all` turns any missing harness, ignored test, unobserved exit, or missing enum case into failure rather than skip.

```bash
git commit -m "Add retryable pending retail imports"
```

The three engine tests join `0026`; because none is tracked before Task 14, strict `0022`...`0025` workspaces remain byte-equal outside their own allowlists.

---

### Task 9: Refactor RA2 map conversion into a bounded service and deliver `0022`

**Outer files:**

- Modify: `OpenRA.Mods.RA2/Content/Maps/Ra2MapImportModels.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/Ra2MapConversionService.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/Ra2Gen2Dialect.cs`
- Modify: `OpenRA.Mods.RA2/UtilityCommands/ImportRA2MapCommand.cs`

**Exact `0022` engine allowlist:**

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

- Create: `engine-patches/0022-ra2-map-conversion-service.patch`
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

public sealed record Gen2MapImportResult
{
	private Gen2MapImportResult(
		bool succeeded,
		Map? map,
		Gen2MapImportError error,
		ImmutableArray<Gen2MapImportDiagnostic> diagnostics);
	public bool Succeeded { get; }
	public Map? Map { get; }
	public Gen2MapImportError Error { get; }
	public ImmutableArray<Gen2MapImportDiagnostic> Diagnostics { get; }
	public static Gen2MapImportResult Success(
		Map map,
		ImmutableArray<Gen2MapImportDiagnostic> diagnostics);
	public static Gen2MapImportResult Failed(
		Gen2MapImportError error,
		ImmutableArray<Gen2MapImportDiagnostic> diagnostics);
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

public sealed record Gen2OverlayResolution(
	Gen2OverlayResolutionKind Kind,
	string? ActorType,
	Size Shape,
	DamageState DamageState,
	byte ResourceType,
	byte ResourceDensity);

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

RED cases cover the modern cordon `(1,16,1,4)` with top uninteractable `6`; malformed `Size`/`LocalSize`; dimensions above `512 x 512`; cells above `262144`; actors `8193`; spawns `9`; duplicate/out-of-bounds spawns; non-multiplayer maps; unknown tile/actor/overlay; truncated LZO/LCW blocks; timeout, cancellation; and checked coordinate arithmetic. Unknown data must return a typed diagnostic instead of substituting tile zero or silently omitting an actor. `Gen2MapImportResult` enforces exactly one state: success has a non-null map and `Error=None`; failure has no map and one non-None closed error. Diagnostics are defensive-copied, bounded, contain only the closed error plus integer record/coordinate fields, and never accept text or a path.

`Gen2MapImportRequest.Create` is a programmer-contract boundary: it rejects null/default dependencies, noncanonical SHA-256/length, a limit set larger than the caller's fixed policy, empty/noncanonical mod/title/author fields, a dialect with invalid cordon arithmetic, and mutable/default mapping results before parsing. It contains no path, output package, global-state accessor, or arbitrary callback. The caller retains the source, ModData, and dialect; `Import` calls `OpenVerifiedRead` exactly once, takes ownership of and disposes only that returned stream, bounded-copies at most `MaximumSourceBytes` into an owned immutable buffer while recomputing length/SHA-256, and parses only that buffer. It never disposes the source/request dependencies. A recomputed byte count different from `IGen2MapSource.Length` returns exactly `SourceLengthMismatch`; an equal-length stream whose recomputed digest differs returns exactly `SourceHashMismatch`; neither is collapsed into `SourceTooLarge`, an unsupported-map diagnostic, or a raw exception. A successful result transfers sole ownership of the returned `Map` to the caller; every failure disposes partial maps/buffers and returns a closed diagnostic. Tests cover nonseekable streams, one-open ownership, post-open source mutation, wrong length, same-length wrong hash, dispose on every result/timeout/cancel branch, caller mutation of dialect source collections, and absence of any raw-path/open delegate API.

`IGen2MapDialect` is the complete engine-neutral mapping surface. `Ra2Gen2Dialect` and the characterized Tiberian Sun adapter defensively copy their closed theater/tile/actor/overlay/deployable/lamp/creep tables at construction and implement only these resolvers; the importer never downcasts to a CLI or reads a static dictionary. An unknown resolver input is a typed unsupported diagnostic. Tests cover every resolver branch, duplicate/case-colliding table keys, returned string/shape bounds, and byte-equivalent trusted TS output.

The RA2-facing result is immutable and contains no original source path:

```csharp
public sealed record Ra2MapCandidate(
	IRa2MapSourceHandle Source);

public sealed class Ra2MapImportContexts
{
	private Ra2MapImportContexts(
		Ra2MapImportContext baseContext,
		Ra2MapImportContext? yuriContext);
	public static RetailOperationResult<Ra2MapImportContexts> Create(
		Ra2MapImportContext baseContext,
		Ra2MapImportContext? yuriContext);
	public RetailOperationResult<Ra2MapImportContext> SelectFor(
		Ra2MapCandidate candidate);
}

public sealed record ConvertedMapArtifact(
	string CanonicalStagedRelativePath,
	string SourceSha256,
	string PackageSha256,
	long CompressedBytes);

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
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~Gen2MapImporterTest|FullyQualifiedName~Ra2MapConversionServiceTest' \
  -v minimal
```

Expected: missing service/model symbols and at least one legacy fallback assertion failure.

- [ ] **Step 3: Extract the pure converter and keep both CLIs thin**

Move parsing, checked decompression, theater/tile/actor/overlay mapping, cordon computation, player/spawn creation, and bounds checks into `Gen2MapImporter`. `ImportGen2MapCommand` and `ImportTiberianSunMapCommand` supply only their dialect and save the returned `Map`; characterization tests require byte-equivalent trusted TS output.

`Ra2Gen2Dialect` supplies RA2 mappings. Direct staged files and Task 2 embedded handles adapt to both Task 3's sealed `IRa2MapSourceHandle` and the borrowed engine `IGen2MapSource`; conversion never accepts a raw path or arbitrary reopen delegate and rechecks length/hash while reading. `Ra2MapImportContexts.Create` requires one Base context and at most one verified-Yuri context and borrows both without disposing them. `SelectFor` uses only the already sanitized source basename suffix after ASCII-only case folding: `.map`/`.mpr` deterministically require Base, `.yrm` deterministically requires Yuri, and any other, Unicode-confusable, trailing-dot, or compound suffix is rejected before output creation. A `.yrm` without a complete validated Yuri pair is quarantined; the service never tries Base then Yuri, never refreshes the deadline, and never creates a second output. Hidden Yuri-only content mislabeled as Base fails through the closed dialect/profile diagnostics rather than triggering a fallback. `RetailCandidateValidator` establishes archive eligibility first, opens the Base and optional Yuri import scopes, converts each map through that closed selection, then passes outputs to Task 10 for independent compatibility validation before making the final active-profile decision. One Yuri conversion failure quarantines only that candidate and does not discard a valid Base sibling.

`Ra2MapConversionService` receives the same injected `IRetailMonotonicClock` that created the deadline, adapts `RetailValidationDeadline` to the engine-neutral `IGen2ImportDeadline`, writes only `CreateNew` files below the caller's sealed transaction map directory, sets `RequiresMod = "ra2"` and `Visibility = MapVisibility.Lobby`, recomputes the staged package hash/size, and returns only `RetailOperationResult<ConvertedMapArtifact>`. It maps every closed `Gen2MapImportError` to a fixed `RetailImportErrorCode`/`RetailImportDiagnosticCode`; output create/save/compress/hash/flush failures map to sanitized storage/conversion codes and never expose an exception or path. Except for cooperative `OperationCanceledException`, no parser, compression, I/O, or hashing exception escapes this boundary. `Gen2MapImporter` checks the same deadline inside every decompression/tile/actor loop, and the service continues checks through package serialization, compression, fsync, and SHA-256 loops; a fake clock/probe proves timeout from inside every phase, removes only the uncommitted CreateNew target, and leaves outside/previous bytes unchanged. The caller creates one five-minute per-map deadline and Task 10 post-validation reuses that same start/budget pair; conversion cannot consume five minutes and then receive a fresh budget. It has no `Ready`, profile-compatibility, receipt, or publication API. Task 10 is the sole component that may transform this result into a `ValidatedMapArtifact`, so Task 9 compiles and turns GREEN without a forward dependency or a temporarily trusted callback.

Task 9 also removes the converter's remaining global player/grid dependencies. Both `Map` constructors resolve `MapGrid` through `modData.Manifest.Get<MapGrid>(modData.ObjectCreator)`. `PlayerReference` gains the exact explicit-default-color constructors above and `MapPlayers` the exact explicit-default-color overload; `Gen2MapImporter` passes `request.ExplicitDefaultPlayerColor` when generating player definitions. Existing desktop constructors remain characterized adapters. With `Game.ModData` replaced by a throwing sentinel, conversion, both `Map` constructors, and player generation still succeed through explicit dependencies; source audits forbid parameterless `Manifest.Get<MapGrid>()` or legacy `MapPlayers` calls in the new path.

The RA2 CLI calls the same service and no longer assigns `Game.ModData` or writes to an implicit working-directory target. Its explicit context supplies every dependency; no conversion or validation call may select the legacy global-state adapter.

- [ ] **Step 4: Run GREEN, full engine tests, and commit**

```bash
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
git commit -m "Refactor RA2 map conversion for validated imports"
```

- [ ] **Step 5: Generate and verify `0022` immediately**

Build the patch from official+`0001`...`0021` with the exact nine engine paths:

```bash
PREDECESSOR_END=21
CANDIDATE=engine-patches/0022-ra2-map-conversion-service.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0022 --output "$ALLOWLIST"
```

Run the exact common block, update README/verifier tests only after a fresh strict replay passes, then commit the three delivery paths:

```bash
git commit -m "Deliver RA2 map conversion engine patch"
```

---

### Task 10: Validate bounded `.oramap` packages and lazy previews; deliver `0023`

**Outer files:**

- Create: `OpenRA.Mods.RA2/Content/Maps/Ra2OramapEnvelope.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/Ra2MapValidationService.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/BoundedMapStringInterner.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailCandidateValidator.cs`

**Exact `0023` engine allowlist:**

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

- Create: `engine-patches/0023-verified-oramap-map-cache.patch`
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

public sealed record ValidatedMapArtifact(
	string LogicalName,
	string CanonicalArtifactRelativePath,
	string SourceSha256,
	string PackageSha256,
	long CompressedBytes,
	MapUid Uid,
	Ra2MapCompatibleProfiles CompatibleProfiles,
	ValidatedMapMetadata Metadata,
	int EnvelopeVersion,
	int ConverterVersion,
	int MapFormat,
	long RetainedMetadataBytes,
	long RetainedStringBytes,
	int RetainedStringEntries);

// Constructor invariant: LogicalName == Uid.Value (canonical lowercase hex).
// CanonicalArtifactRelativePath identifies only the sealed source-domain object;
// publication remaps it to maps/{Uid.Value}.oramap.

public enum Ra2MapValidationPurpose
{
	RetailCatalog,
	RuntimeCurrentProfile,
}

public readonly record struct Ra2MapProfileContext(
	Ra2RuntimeProfileId Profile,
	Ra2MapValidationContext Context);

public sealed class Ra2MapValidationContexts
{
	private Ra2MapValidationContexts(
		Ra2MapValidationPurpose purpose,
		ImmutableArray<Ra2MapProfileContext> profiles);
	public Ra2MapValidationPurpose Purpose { get; }
	public ImmutableArray<Ra2MapProfileContext> Profiles { get; }
	public static RetailOperationResult<Ra2MapValidationContexts> CreateRetail(
		Ra2MapValidationContext baseContext,
		Ra2MapValidationContext? yuriContext);
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

public readonly record struct MapValidationDeadlineSet(
	RetailValidationDeadline Operation,
	RetailValidationDeadline Whole)
{
	public static MapValidationDeadlineSet ForRetail(
		RetailValidationDeadline map,
		RetailValidationDeadline whole);
	public static MapValidationDeadlineSet ForRuntime(
		RetailValidationDeadline sharedFiveMinuteDeadline);
	public void ThrowIfExpired(IRetailMonotonicClock clock);
}

public readonly struct MapUid : IEquatable<MapUid>, IComparable<MapUid>
{
	public const int HexLength = 40;
	public string Value { get; }
	public static bool TryParse(string value, out MapUid uid);
	public int CompareTo(MapUid other);
	public bool Equals(MapUid other);
	public override bool Equals(object? obj);
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

public interface IVerifiedMapEntrySource : IDisposable
{
	IReadOnlyPackage OpenPackage();
	IMapPreviewImageSource AcquirePreviewSource();
}

public sealed record VerifiedMapPlayerMetadata(
	string Name,
	string? Palette,
	string? Bot,
	string? StartingUnitsClass,
	bool AllowBots,
	bool Playable,
	bool Required,
	bool OwnsWorld,
	bool Spectating,
	bool NonCombatant,
	bool LockFaction,
	string? Faction,
	bool LockColor,
	Color Color,
	CPos HomeLocation,
	bool LockSpawn,
	int Spawn,
	bool LockTeam,
	int Team,
	bool LockHandicap,
	int Handicap,
	ImmutableArray<string> Allies,
	ImmutableArray<string> Enemies);

public sealed record VerifiedMapPreviewMetadata(
	string Title,
	string Author,
	ImmutableArray<string> Categories,
	ImmutableArray<VerifiedMapPlayerMetadata> Players,
	ImmutableArray<CPos> SpawnPoints,
	int BoundsX,
	int BoundsY,
	int BoundsWidth,
	int BoundsHeight,
	MapGridType GridType,
	string Tileset,
	int MapFormat)
{
	public int PlayablePlayers => Players.Count(player => player.Playable);
}

public readonly record struct VerifiedMapResidentUsage(
	long MetadataBytes,
	long StringBytes,
	int StringEntries);

public static class VerifiedMapResidentUsageMeter
{
	public static VerifiedMapResidentUsage Measure(
		VerifiedMapPreviewMetadata metadata);
}

public readonly record struct VerifiedMapResidentBudgetLimits(
	long MetadataBytes,
	long StringBytes,
	int StringEntries);

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

public sealed record VerifiedMapResidentBudgetResult
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
	public static VerifiedMapEntry Create(
		MapUid uid,
		string logicalName,
		bool canDelete,
		VerifiedMapPreviewMetadata metadata,
		IVerifiedMapEntrySource ownedSource);
}

public sealed class VerifiedMapSnapshotPublisher :
	IVerifiedMapSnapshotProvider, IDisposable
{
	public VerifiedMapSnapshotPublisher(IGameThreadAffinity affinity);
	public event Action SnapshotChanged;
	public VerifiedMapSnapshotLease Acquire();
	public void Publish(ImmutableArray<VerifiedMapEntry> ownedEntries);
	public void Clear();
	public void Dispose();
}

public readonly record struct MapPreviewBudgetSnapshot(
	long CpuBytes,
	long PinnedCpuBytes,
	long GpuBytes,
	long PinnedGpuBytes);

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
	public RetailCandidateValidator(
		RetailArchiveGraphValidator archives,
		RetailProfileValidator profiles,
		Ra2RuntimeContractBuilder contracts,
		Ra2MapConversionService conversion,
		Ra2MapValidationService maps,
		IIosRuntimeResourcePreflight runtimeResources,
		PreGameEngineContext borrowedPreGameContext,
		IRetailMonotonicClock clock);
	public Task<RetailOperationResult<RetailCandidateValidation>> ValidateAsync(
		RetailStagedPlan staged,
		RetailCandidateArtifactSource artifactSource,
		RetailActiveGenerationSnapshot? preservedActive,
		RetailValidationDeadline deadline,
		CancellationToken cancellationToken,
		IProgress<RetailImportProgress> progress);
	public Task<RetailOperationResult<RetailRepairValidation>> ValidateRepairAsync(
		RetailActiveGenerationSnapshot active,
		RetailCatalogRepairPlan repair,
		RetailValidationDeadline deadline,
		CancellationToken cancellationToken,
		IProgress<RetailImportProgress> progress);
	public void Dispose();
}
```

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

Contexts are processed in canonical Base-then-Yuri order. The first successful profile supplies the artifact's single complete metadata projection; each later successful profile sets its bit only when its title/author/categories/tileset/grid/bounds/player/default-color/spawn projection is field-for-field equal after canonicalization. A mismatch clears only that later bit and produces the closed `ProfileMetadataMismatch` diagnostic for readiness/UI; it never lets one profile's metadata masquerade as another's. Thus Base+Yuri flags always imply one identical persistent projection, while a Base-failed/Yuri-success case legitimately stores Yuri metadata with only the Yuri bit. A profile-specific typed semantic failure likewise clears only that bit. Retail returns a typed invalid-map result only when neither profile succeeds, while candidate readiness separately requires at least one Base-compatible map. After all maps are known, `RetailCandidateValidator` computes both checked compatible-map counts and performs the final profile decision: Yuri requires a complete verified Yuri pair *and* `YuriCompatibleMapCount > 0`; otherwise it retains a structurally valid pair as inactive/Unavailable, selects Base, recomputes the Base contract/readiness, and exposes only Base-compatible maps. Runtime requires its sole current profile to succeed and never infers untested cross-profile compatibility. Tests cover Base-only, Base+Yuri, Yuri-only retail artifacts; complete Yuri archives with only a Base-only map; the same candidate with a Yuri-compatible map; Base/Yuri contexts whose default player, grid, bounds, or player projection differs; Base-current and Yuri-current runtime artifacts; one-profile failure isolation; source-open/envelope-admission exactly once; and canonical context order/ownership without double disposal.

A retail adapter uses the same transaction's sealed `RetailCandidateArtifactSource` to expose Task 9's converted artifact or a direct staged `.oramap`; a runtime adapter binds Task 12's random same-volume staging inode to its runtime transaction. Both expose only a canonical domain-relative descriptor and pinned verified stream—never a root or raw path—and recheck length/package digest through the same handle. `SourceSha256` and `ConverterVersion` are immutable provenance supplied alongside the package; direct/runtime inputs use package digest and converter version zero. The output's `CanonicalArtifactRelativePath` remains relative to the owning domain and is remapped explicitly into a retail candidate catalog or runtime generation; it is never accepted across domains. Retail uses `ForRetail` with one five-minute map deadline plus the enclosing 90-minute deadline. Runtime download uses `ForRuntime` with the same already-started five-minute deadline in both fields, so validation cannot refresh its budget. Every ZIP/YAML/PNG/profile loop calls `ThrowIfExpired`. One bad or timed-out map is quarantined without discarding valid sibling maps; the entire candidate fails only when no Base-compatible map remains or the whole-validation deadline expires. The artifact reports exact retained metadata/string counters before publication so Task 7 aggregate budgets and receipts never estimate or reparse them. Tests cover cross-domain descriptor rejection, disposed/changed pinned sources, context ownership, and proof that no validation overload accepts a string path or caller-defined retail source.

The validation contexts and `VerifiedMapPackageInput.Package` source in the API above are caller-owned and only borrowed for one invocation. Success, typed failure, cancellation, or exception never disposes them; the caller disposes every context and the domain adapter exactly once after the complete per-map operation, while the underlying sealed retail/runtime artifact source follows its longer transaction ownership. This is distinct from the per-profile package read views transferred into `Ra2ConstructedMap`, which the service always closes. Ownership tests reject service-side disposal of caller capabilities, caller leaks, and any surviving constructed Map/read view.

- [ ] **Step 2: Write RED lazy-cache and atlas-boundary tests**

Require metadata-only initial enumeration, one per-map bounded string interner, a 64 MiB CPU byte-accounted LRU, 128 MiB GPU-page budget, maximum source decode edge 4096, proportional thumbnail at most 1024, 2048 atlas page with margin 1, and a placeholder that does not make the map unplayable.

Add `IGraphicsContext.MaximumTextureSize`. `SheetBuilder` must use checked arithmetic and reject any allocation where `thumbnail edge + 2 * margin > min(2048, device maximum)`. Test edge `-1`, exact, `+1`, 4096-to-1024 scaling, device page below 2048, pinned eviction, rotation, memory warning, release/reupload, and no global `MapCache.StringPool` pollution.

`VerifiedMapResidentUsageMeter.Measure` is the sole trust-boundary meter. It serializes every retained metadata field in one versioned canonical fixed-width/length-prefixed form, uses checked UTF-8 byte counts, counts the ordinal-unique canonical untrusted strings retained by the per-map interner, and rejects null/default/mutable/noncanonical values or arithmetic overflow. `VerifiedMapEntry.Create` requires `logicalName == uid.Value`, does not accept caller-provided counters, defensively copies metadata, calls the meter, stores the result, and only then takes ownership of the entry source. Task 10's RA2 validator projects `ValidatedMapMetadata` into that exact engine metadata, uses the same measured result for `ValidatedMapArtifact`, and rejects any claimed counter mismatch. Retail/runtime receipt codecs reconstruct the projection, remeasure it, and require byte-for-byte counter equality before admission; cold recovery repeats the comparison before creating an entry. Tests mutate every scalar/string/array/player/spawn field, use multibyte UTF-8 and duplicate interner values, exercise each counter `-1`/exact/`+1` and overflow, and prove no cross-assembly provider can make a large metadata object claim zero usage or substitute a display/source name.

Task 10 adds an engine-owned pure `VerifiedMapResidentBudget.ValidateUnion` helper over one or more immutable entry arrays. It checked-sums the engine-measured usage across the exact visible union, rejects duplicate UIDs/logical names, never counts one entry twice, and enforces the single V1 64 MiB / 32 MiB / 262,144 global caps without knowing a future runtime-store type. Task 10's one-provider `MapCache` route and synthetic two-provider tests use this helper. Task 12 modifies `MapCache` again to compose the concrete retail/runtime providers through the same helper: a bad optional runtime candidate snapshot is withheld (the last verified runtime snapshot or no runtime maps remains visible) without hiding a valid retail snapshot, while a retail overflow closes readiness before publication. Tests cover retail-only exact cap, runtime-only exact cap, combined `limit - 1`/exact/`+ 1`, duplicate UID, checked overflow, profile filtering before the sum, and proof that rejection does not allocate/decode a preview or mutate the prior composed view.

- [ ] **Step 3: Run RED**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~BoundedOramapEnvelopeTest|FullyQualifiedName~Ra2MapValidationServiceTest|FullyQualifiedName~MapPreviewImageCacheTest|FullyQualifiedName~SheetBuilderBoundsTest|FullyQualifiedName~PngTest' \
  -v minimal
```

Expected: malformed packages/PNG/atlas `page + 1` are accepted or fail after allocation, and the new verified snapshot types are missing.

- [ ] **Step 4: Implement one verified-map path**

`Ra2OramapEnvelope` wraps Task 2's `BoundedZipEnvelope`; `Ra2MapValidationService` performs the pre- and post-validation and returns a complete `ValidatedMapArtifact`. It never calls `Map.Save` on a direct package and never publishes a partially compatible artifact.

`MapCache.UpdateFromVerifiedMap` consumes immutable bounded preview metadata without reparsing untrusted YAML. `VerifiedMapPreviewMetadata` defensively copies/sorts categories and player definitions, preserves ordered spawn coordinates, validates every bounded string/count/dimension/player reference, and contains no YAML node, source path, or mutable collection. It constructs a fresh `MapPlayers` solely through the engine-owned `VerifiedMapPlayerMetadata` projection, copying every lobby-facing `PlayerReference` field (including `AllowBots`, required/lock fields, faction/color/team/handicap/spawn, system-player flags, allies, and enemies); `SpawnPoints` is populated from the separately validated ordered array. Chooser, lobby, initial-map selection, and save setup therefore retain the exact validated semantics without a second parse. Tests cover AllowBots false, every lock bit, system and playable players, ordered spawn coordinates, allies/enemies, and prove no `MiniYaml`/package read occurs during snapshot routing.

The metadata constructor resolves an engine-owned `MapPreviewCachePolicy` through Task 3's sealed `ModData` service provider; the iOS bootstrap maps the complete `PngReadLimits` (edge, pixels, decoded bytes, chunks, IDAT bytes, and inflate ratio) plus every cache/atlas field from `IosImportEnvelopePolicyV1`, while absence selects characterized desktop trusted defaults. The policy owns one instance-scoped, thread-safe `MapPreviewBudgetLedger`; its constructor requires positive fixed limits equal to the policy's CPU/GPU limits. The cache accounts every CPU/GPU allocation and pin through owner-bound reservations from that ledger with checked arithmetic before allocation/upload, releases the exact token in `finally`/eviction/disposal, and cannot mutate counters directly. Zero/negative/over-limit acquisition, a foreign or disposed reservation, invalid CPU/GPU kind, double release, and pin transitions that would break `pinned <= total` fail without changing counters. `Capture` returns one atomic nonnegative snapshot with pinned values no greater than totals and totals no greater than the fixed `CpuLimit`/`GpuLimit`. Task 12 receives only its read-only provider view and checks the same live ledger at both quota gates; no iOS static or estimated preview value is accepted.

Each `AcquirePreviewSource` returns an independently owned pinned reference. `IMapPreviewImageSource.Decode` must receive that complete policy on every initial decode and later re-decode after release/memory warning; it returns a byte-accounted BGRA lease whose memory becomes inaccessible after disposal. Cross-assembly decoders create that lease only through `MapPreviewImageLease.Create`; it accepts an owned `IMemoryOwner<byte>`, requires checked `width * height * 4 == byteLength <= owned memory length`, disposes the owner on rejected construction, and transfers sole ownership on success. Preview decode occurs only when a widget requests a lease. The cache downsamples before atlas upload, releases the CPU lease after upload, evicts only unpinned pages, and rebuilds from a newly acquired verified immutable source. `TrimUnpinnedPreviews()` evicts unpinned CPU/GPU state only; Task 13's `IosRuntimeMapServices` forwards UIKit memory warnings to this exact method on the game thread. RED coverage tampers chunks/IDAT/ratio specifically on a post-warning re-decode and proves source/image lease order, invalid factory input disposal, cancellation, exact ledger accounting at `limit - 1`/exact/`+ 1`, concurrent pin/evict snapshots, defensive copies, no post-dispose memory access, and no generation-pin leak.

`RetailCandidateValidator` is the sole production implementation of Task 8's interface. Its constructor requires `RetailArchiveGraphValidator`, `RetailProfileValidator`, `Ra2RuntimeContractBuilder`, `Ra2MapConversionService`, `Ra2MapValidationService`, `IIosRuntimeResourcePreflight`, a borrowed pre-game context, and the monotonic clock. The coordinator remains the sole owner of `PreGameEngineContext`; the validator may open and dispose sequential per-validation `PreGameModDataScope` instances but neither stores the context beyond its own lifetime nor disposes it. Its `Dispose` cancels/cleans only owned scopes and helpers. The concrete startup composition injects `IosRuntimeResourcePreflight`; tests inject the same closed interface to prove the failure path, and no static preflight entry exists. It combines staged replacements with the authenticated preserved-active source, validates archives/audio/profile/mounts/static runtime resources, converts and validates every map, computes the complete runtime contract/readiness, and returns only typed operation results. Its repair method applies exclusions to the authenticated active source and recomputes the profile/contract/readiness. `Ra2MapValidationServiceTest` includes full-composition Base, optional-Yuri, optional-corruption, preserved-active, runtime-preflight failure, retry with the still-live borrowed context, and repair cases; Task 14 constructs this exact concrete type and cannot inject an unimplemented callback.

`VerifiedMapSnapshotLease` owns an internal root pin and a defensively copied immutable entry array. Disposing the lease prevents new package/preview acquisition but already-open entry/package/preview references retain independent pins until their own disposal. Tests lock lease-first/entry-first/package-first disposal, concurrent preview references, final-pin GC eligibility, and no leaks on failure/cancellation.

Cross-assembly publication goes only through the engine-owned factories above. RA2 retail/runtime providers implement `IVerifiedMapEntrySource` with their already-pinned generation capabilities, then transfer each source into `VerifiedMapEntry.Create`; no public constructor exposes `VerifiedMapSnapshotRootPin`. `VerifiedMapSnapshotPublisher` receives the explicit game-thread affinity service (Task 11's dispatcher implements it), takes ownership of a canonical immutable entry array, constructs/swaps the internal ref-counted root, and raises `SnapshotChanged`; `Publish`, `Clear`, or `Dispose` when `IsGameThread` is false fails. It never assumes its construction/UIKit thread is the game thread. The root swap is the notification commit point: it snapshots the multicast invocation list, calls every subscriber exactly once in subscription order, catches each observer exception independently, emits only a fixed engine diagnostic, and continues. Observer code can never make an already completed root swap report failure or prevent later observers from running. Old root/entry sources are disposed only after every old lease/package/preview reference releases. Tests compile a provider from the RA2 assembly against only these public factories and reject direct root-pin construction, caller mutation after publish, double publication ownership, construction-thread confusion, cross-thread publication, and source leaks on validation/factory failure; first/middle/last throwing observers, reentrant `Acquire`, unsubscribe-during-notification, and `Clear`/`Dispose` prove the published root, pins, and operation result remain deterministic.

- [ ] **Step 5: Run GREEN, full tests, builds, and commit**

```bash
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
git commit -m "Harden ORAMAP validation and preview caching"
```

- [ ] **Step 6: Generate and verify `0023` immediately**

Build from official+`0001`...`0022` with the exact 21-path engine allowlist:

```bash
PREDECESSOR_END=22
CANDIDATE=engine-patches/0023-verified-oramap-map-cache.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0023 --output "$ALLOWLIST"
```

Run the exact common block. Commit the three delivery paths only after fresh replay:

```bash
git commit -m "Deliver verified ORAMAP engine patch"
```

---

### Task 11: Bound remote map metadata before parsing and deliver `0024`

**Exact `0024` engine allowlist:**

- Create: `engine/OpenRA.Game/Map/RemoteMapMetadata.cs`
- Create: `engine/OpenRA.Game/Map/VerifiedRemoteMapMetadataClient.cs`
- Modify: `engine/OpenRA.Game/Map/MapCache.cs`
- Modify: `engine/OpenRA.Game/Map/MapPreview.cs`
- Create: `engine/OpenRA.Test/VerifiedRemoteMapMetadataClientTest.cs`
- Create: `engine/OpenRA.Test/MapCacheRemoteMetadataTest.cs`

**Delivery files:**

- Create: `engine-patches/0024-verified-remote-map-metadata.patch`
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

public sealed record VerifiedRemoteMapPreviewMetadata(
	string Title,
	string Author,
	ImmutableArray<string> Categories,
	int PlayablePlayers,
	ImmutableArray<CPos> SpawnPoints,
	int BoundsX,
	int BoundsY,
	int BoundsWidth,
	int BoundsHeight,
	MapGridType GridType,
	string Tileset,
	int MapFormat,
	bool Downloading,
	string GameMod);

public sealed record RemoteMapMetadataRecord(
	MapUid Uid,
	VerifiedRemoteMapPreviewMetadata Metadata,
	ImmutableArray<byte> ValidatedMinimapPng);

public sealed record RemoteMapMetadataBatch(
	ImmutableSortedDictionary<MapUid, RemoteMapMetadataRecord> Records,
	ImmutableSortedDictionary<MapUid, RemoteMapMetadataFailureCode> Failures,
	RemoteMapMetadataFailureCode BatchFailure);

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

`VerifiedRemoteMapPreviewMetadata` is deliberately display-only and separate from Task 10's authoritative `VerifiedMapPreviewMetadata`: it may supply a bounded player count and ordered spawn coordinates for search UI, but it can neither create `MapPlayers`/lobby slots nor mark a map playable. No remote metadata can create custom rules or authoritative player definitions. After download, only Task 10's fully validated package metadata may replace the remote preview and enter the lobby/runtime snapshot.

- [ ] **Step 2: Run RED**

```bash
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
git commit -m "Bound remote map metadata parsing"
```

- [ ] **Step 5: Generate and verify `0024` immediately**

Use official+`0001`...`0023` and the exact six-path allowlist:

```bash
PREDECESSOR_END=23
CANDIDATE=engine-patches/0024-verified-remote-map-metadata.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0024 --output "$ALLOWLIST"
```

Run the exact common block, then commit only patch/README/verifier test:

```bash
git commit -m "Deliver remote map metadata engine patch"
```

---

### Task 12: Add a verified runtime-map snapshot store and deliver `0025`

**Outer files:**

- Create: `OpenRA.Mods.RA2/Content/Maps/VerifiedRuntimeMapStore.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/RuntimeMapGenerationStore.cs`
- Create: `OpenRA.Mods.RA2/Content/Maps/RuntimeMapReceipt.cs`

**Exact `0025` engine allowlist:**

- Create: `engine/OpenRA.Game/Map/RuntimeMapDownload.cs`
- Modify: `engine/OpenRA.Game/Map/VerifiedMapSnapshot.cs`
- Modify: `engine/OpenRA.Game/Map/MapCache.cs`
- Modify: `engine/OpenRA.Game/Map/MapPreview.cs`
- Create: `engine/OpenRA.Test/VerifiedRuntimeMapStoreTest.cs`
- Create: `engine/OpenRA.Test/RuntimeMapStoreRecoveryTest.cs`
- Create: `engine/OpenRA.Test/MapPreviewRuntimeInstallTest.cs`

**Delivery files:**

- Create: `engine-patches/0025-verified-runtime-map-store.patch`
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
		string? contentDisposition);
	public Stream Body { get; }
	public long? DeclaredLength { get; }
	public string? ContentDisposition { get; }
	public static RuntimeMapDownloadResponse Create(
		Stream ownedBody,
		long? declaredLength,
		string? contentDisposition);
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
	public Ra2RuntimeMapContextFactory(
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
	public RuntimeMapGenerationStore(
		string fixedRuntimeMapRoot,
		IRetailStoragePolicy storagePolicy,
		IRetailDirectoryDurability durability);
	public void Dispose();
}

public sealed record RuntimeMapReceipt(
	MapUid Uid,
	string LogicalName,
	string CanonicalGenerationRelativePath,
	string SourceSha256,
	string PackageSha256,
	long CompressedBytes,
	Ra2MapCompatibleProfiles CompatibleProfiles,
	ValidatedMapMetadata Metadata,
	int EnvelopeVersion,
	int ConverterVersion,
	int MapFormat,
	int ValidatorVersion,
	long RetainedMetadataBytes,
	long RetainedStringBytes,
	int RetainedStringEntries);

public sealed record RuntimeMapGenerationReference(
	string GenerationId,
	long Epoch,
	string CommitId,
	string ReceiptSha256);

public sealed record RuntimeMapGenerationReceipt(
	int SchemaVersion,
	long Epoch,
	string GenerationId,
	string CommitId,
	RuntimeMapGenerationReference? Rollback,
	RuntimeContractIdentity RuntimeContract,
	ImmutableArray<RuntimeMapReceipt> Maps,
	MapResidentUsage MapResidentUsage);

public enum RuntimeMapPublishState
{
	Prepared,
	Publishing,
	Committed,
}

public sealed record RuntimeMapPublishJournal(
	int SchemaVersion,
	RuntimeMapPublishState State,
	long Epoch,
	RuntimeMapGenerationReference? Previous,
	string ProposedGenerationId,
	string CommitId,
	RuntimeContractIdentity RuntimeContract);

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

public readonly record struct RuntimeMapDownloadProgress(
	MapUid Uid,
	RuntimeMapDownloadPhase Phase,
	long CompletedBytes,
	long? TotalBytes)
{
	public int? Percentage => TotalBytes is > 0
		? (int)Math.Min(100m,
			(decimal)CompletedBytes * 100m / TotalBytes.Value)
		: null;
}

public sealed record RuntimeMapDownloadResult
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

public sealed record RuntimeMapStoreRecoveryResult(
	RuntimeMapStoreRecoveryDisposition Disposition,
	RuntimeMapStoreRecoveryFailureCode Failure);

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

public sealed record VerifiedRuntimeMapAdmissionResult
{
	private VerifiedRuntimeMapAdmissionResult(
		bool succeeded,
		VerifiedRuntimeMapAdmissionLease? admission,
		VerifiedRuntimeMapAdmissionFailure failure);
	public bool Succeeded { get; }
	public VerifiedRuntimeMapAdmissionLease? Admission { get; }
	public VerifiedRuntimeMapAdmissionFailure Failure { get; }
	internal static VerifiedRuntimeMapAdmissionResult Success(
		VerifiedRuntimeMapAdmissionLease ownedAdmission);
	internal static VerifiedRuntimeMapAdmissionResult Failed(
		VerifiedRuntimeMapAdmissionFailure failure);
}

public interface IVerifiedRuntimeMapAdmissionCoordinator
{
	VerifiedRuntimeMapAdmissionFailure ConfirmRecovered(
		IVerifiedMapSnapshotProvider recoveredRuntimeSnapshots);
	VerifiedRuntimeMapAdmissionResult Prepare(
		ImmutableArray<VerifiedMapEntry> borrowedProposedRuntimeEntries);
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
	public void Commit(
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
	Task<RuntimeMapStoreRecoveryResult> RecoverAsync(
		RuntimeContractIdentity contract,
		IVerifiedMapSnapshotProvider retailSnapshots,
		CancellationToken cancellationToken);
	RuntimeMapDownloadSubscription Enqueue(
		RuntimeMapDownloadRequest request,
		IProgress<RuntimeMapDownloadProgress> progress,
		CancellationToken cancellationToken);
}

public sealed class VerifiedRuntimeMapStore :
	IVerifiedRuntimeMapStore, IModDataBoundService, IDisposable
{
	public VerifiedRuntimeMapStore(
		IRuntimeMapDownloadTransport transport,
		Ra2MapValidationService validator,
		IRa2RuntimeMapContextFactory contexts,
		IGameThreadDispatcher gameThread,
		RuntimeMapGenerationStore generations,
		IRetailMonotonicClock clock,
		IMapPreviewBudgetProvider previewBudgets,
		RuntimeMapStoreLimits limits);
	public IVerifiedMapSnapshotProvider Snapshots { get; }
	public Task<RuntimeMapStoreRecoveryResult> RecoverAsync(
		RuntimeContractIdentity contract,
		IVerifiedMapSnapshotProvider retailSnapshots,
		CancellationToken cancellationToken);
	public RuntimeMapDownloadSubscription Enqueue(
		RuntimeMapDownloadRequest request,
		IProgress<RuntimeMapDownloadProgress> progress,
		CancellationToken cancellationToken);
	public IDisposable Bind(ModData modData);
	public void Dispose();
}
```

The constructor above is the sole production composition. It requires the download transport, Task 10 validator, a contract-bound validation-context factory, the engine game-thread dispatcher, the already root-bound generation store, monotonic clock, the same instance-scoped preview-budget provider used by `MapCache`, and exact V1 limits; it has no default/static dependency lookup. It rejects preview limits that do not equal the provider ledger's fixed 64 MiB CPU / 128 MiB GPU caps. `Ra2RuntimeMapContextFactory` pins one fixed trusted manifest selection, profile, and expected runtime identity but no pre-game context or `ModData`. The runtime store is the registered instance-scoped service. Its `IModDataBoundService.Bind` first binds the factory to the exact loaded ModData, verifies the loaded manifest/runtime identity against the recovered store contract, and obtains that same instance's `MapCache` as the sole `IVerifiedRuntimeMapAdmissionCoordinator`; it never resolves a static/current cache. It then calls `ConfirmRecovered(Snapshots)` and requires `None`, proving the exact cold-recovered provider root is the one already admitted into that MapCache's combined index. A mismatch aborts ModData construction before the queue is enabled rather than running with divergent views. Only after all four checks succeed does it enable the queue. The returned composite unbind token atomically rejects new subscriptions, cancels and joins the queue, disposes any uncommitted admission, releases snapshot/cache callbacks, and only then disposes the factory binding token. No partially bound store is observable.

Factory bind installs one ref-counted instance binding and rejects overlap, wrong identity/selection, use before bind, and new acquisition after unbind. Same-mod reconstruction must fully unbind the old store/factory before binding the new instance. Neither the factory nor its binding is separately exposed as a ModData consumer service.

`IRa2RuntimeMapContextFactory.Open` accepts the store's exact runtime identity plus the already-started five-minute deadline, the same monotonic clock, and cancellation token. It rejects a mismatched identity or missing binding before acquiring a pin, checks cancellation/deadline immediately before and after acquisition, releases a pin acquired during an elapsed/cancelled call, and returns a fresh caller-owned sequential `Ra2MapValidationContext` borrowing only that explicitly bound ModData; it never refreshes the budget and never reads or replaces `Game.ModData`. The runtime queue disposes that context in `finally` after Task 10 validation. On ModData unload the store stops/cancels the queue first, then the binding token waits for/releases the final context pin before unbinding; final provider shutdown disposes store and factory exactly once. Tests advance the fake clock during pin acquisition, cancel before/after acquisition, reject pre-bind/post-unbind and wrong-contract opens, exercise sequential same-mod rebind, and prove context/store/factory disposal order with no `Game.ModData` access.

Every verified package source is likewise disposed after validation. `RuntimeMapGenerationStore` validates the injected fixed Application Support runtime root at construction, rejects symlink/noncanonical/wrong-suffix roots, owns all staging/generation/pointer operations, and exposes no root/path getter. Only Task 13 may supply that root, storage policy, and durability adapter; tests use a bounded fixture root through the same constructor. The RA2 queue implements `IRuntimeMapDownloadSubscriptionHandle` and transfers it through the engine-owned `RuntimeMapDownloadSubscription.Create`; the wrapper has no public mutable/default constructor, owns the handle, and guarantees idempotent cancel/dispose/exactly-once completion across the assembly boundary.

`RuntimeMapReceipt` is the lossless trust projection of Task 10's `ValidatedMapArtifact`: it preserves the typed UID, exact package/source hashes and size, canonical *generation-relative* package name, compatible profiles, complete bounded player/spawn/preview metadata, envelope/converter/map-format/validator versions, and retained metadata/string counters. Its `LogicalName` must equal `Uid.Value` and its generation-relative path must equal `maps/{Uid.Value}.oramap`; it never stores a title-derived name, staging path, provider URL, source path, or free-form error. Same UID/same digest deduplicates; same UID with differing bytes or metadata returns `UidMismatch`/`InvalidPackage` before preparation. `RuntimeMapGenerationReceipt` binds the ordinal-UID-sorted defensive-copied map set to one canonical generation/epoch/commit and exact runtime identity; its `MapResidentUsage` must equal the checked per-map sum. Its optional `Rollback` is null on first install or a complete immutable reference to the exact previous generation and that previous receipt's SHA-256. The publish journal carries the byte-identical `Previous` reference. The codec rejects self-reference, mismatched ID/epoch/commit/digest, a journal/receipt reference mismatch, noncanonical or future references, and any reference whose opened receipt does not hash and identify exactly; a missing already-GC'd rollback directory merely makes the optional alternate unavailable and never invalidates the independently valid active generation. Its versioned binary codec uses checked fixed-width/length-prefixed fields plus an outer SHA-256 checksum; it rejects null/default/duplicate UID or logical names, UID/logical/path disagreement, noncanonical paths/IDs/hashes, unknown enum/flag/version values, counter/aggregate overflow, trailing bytes, and any field above the V1 limits or above the runtime allowance remaining after the authenticated retail reservation. Constructor inputs cannot mutate the receipt after creation. Tests tamper every rollback-reference component, substitute another valid receipt, create a self/cycle reference, remove a GC-eligible rollback, and prove stable absent-journal recovery can authorize only the exact referenced generation.

Cold recovery runs before a bound validation context exists, so it never reparses map YAML or reconstructs trust from filenames. `RecoverAsync` requires the exact final retail snapshot provider for this launch in addition to the runtime contract; it acquires a caller-owned retail lease and never accepts caller-provided UID/counter summaries. Recovery has two explicit phases. **Phase A—journal/byte recovery** authenticates pointer/journal/receipt linkage, opens each exact generation-relative regular file without following links, checks recorded length and a full pinned-handle SHA-256, validates the generation's own count/storage/metadata/string totals with checked arithmetic, and deterministically projects authenticated receipt metadata/counters into `VerifiedMapPreviewMetadata`/`VerifiedMapEntry`. The first table uses only this closed `ByteReceiptValid` predicate; it does not include retail-union compatibility and it publishes no runtime root.

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

`VerifiedRuntimeMapAdmissionLease.Commit` is the sole critical in-memory publication path. On the game thread it first proves the same publisher and element-for-element proposed array supplied to `Prepare`, then transfers those entries into the runtime publisher and swaps the already-built combined `MapCache` index as one non-allocating engine transaction. All misuse/stale/thread checks happen before either swap; a valid reserved commit has no recoverable failure point. Public `SnapshotChanged` callbacks run only after both roots are current and use Task 10's per-observer exception isolation, so a UI observer cannot make a committed map invisible or fault the dispatch. The lease releases old pins after the swap; disposal without commit releases the prepared state and leaves both views unchanged. Runtime recovery before ModData binding may publish only its independently authenticated provider snapshot; initial MapCache construction performs the same union admission before exposing a combined view. No critical composition depends on reacting to `SnapshotChanged` after publication.

Immediately after the second quota check, successful admission, and one final deadline/cancellation check, the shared operation enters a non-cancellable commit boundary. From that point it ignores subscriber cancellation/deadline expiry and uses `CancellationToken.None` to write/fsync the complete generation receipt, journal `Prepared`, journal `Publishing`, atomically replace/fsync the pointer, invoke the prepared admission's nonthrowing `Commit` through `gameThread.InvokeAsync`, then write/fsync `Committed` and clear/fsync the journal directory. A subscriber that detaches after the boundary stops observing progress but cannot interrupt consistency; any remaining observer completes according to the exact result below.

If dispatch is rejected or throws **before the admission action begins** after pointer replacement, no in-memory root changed: the store durably restores/fsyncs the previous pointer (or the first-install missing pointer), disposes the uncommitted admission, confirms the old runtime and combined views are still current, records/cleans the rollback journal, and returns `Published=false, Failure=PublishFailed`. If that rollback cannot be proven it returns `Published=false, Failure=RecoveryRequired`, rejects new downloads, and leaves cold recovery to converge the pointer. Once the admission action begins, its pre-reserved engine transaction cannot throw; observer exceptions are isolated and do not enter this branch.

After admission commit, the new runtime and combined views may already have issued leases, so the store never rolls them back. A `Committed` journal write/fsync or journal-clear/directory-fsync failure preserves the proposed pointer and both new views, leaves the last fsynced journal for deterministic recovery, stops the queue, and returns the only permitted mixed result: `Published=true, Failure=RecoveryRequired`. Fully successful cleanup returns `Published=true, Failure=None`; every other failed result has `Published=false`. `RuntimeMapDownloadResult` constructor invariants reject any other combination. Process death between any two durability/publication points is resolved solely by the total table. Kill/fault tests at every receipt write, journal flush, rename, directory fsync, dispatch enqueue/before-action/action, admission swap, `Committed` flush, cleanup unlink, and cleanup fsync boundary assert one specified triple: subscriber result, durable pointer/journal, and runtime/combined snapshot. Cancellation/timeout immediately before versus after the boundary, and post-publish synchronous I/O failure with a live acquired lease, are separately locked.

Reserve quota twice: before network transfer and immediately before pointer swap. Both reservations include current + rollback generations, all staging and queued reservations, catalog count/bytes, the runtime metadata/string usage plus authenticated retail reservation under the single global resident caps, and the exact 512 MiB `DiskSafetyMarginBytes` using checked arithmetic. At each gate, atomically capture the injected preview ledger and require nonnegative/pin-within-total values and totals within the exact `RuntimeMapStoreLimits.PreviewCpuBytes`/`PreviewGpuBytes`; these limits must equal the policy's 64 MiB/128 MiB caps. The check uses live accounted values rather than estimates or static iOS constants and is repeated at swap because UI decode/upload may race the download. Preview memory is not added to disk byte counts; it is a separate mandatory resident-memory gate whose ledger already rejects allocation above its cap. Eviction considers only unpinned, non-current, re-downloadable maps in an old generation; preview eviction remains `MapPreviewImageCache`'s game-thread responsibility. If no safe victim satisfies 128 maps/1 GiB current/2 GiB with rollback/256 MiB staged/global resident limits, valid live preview accounting, and the disk margin, return a localized quota code and leave the current snapshot byte-identical. Tests cover CPU/GPU `limit - 1`/exact/`+ 1`, negative/inconsistent forged providers, preview usage changing between the two checks, pinned exact-cap state, and proof that quota failure leaves both snapshot and ledger byte-for-byte unchanged.

Task 12's `MapCache` change performs the real two-provider composition promised by Task 10 and implements `IVerifiedRuntimeMapAdmissionCoordinator`. Initial construction acquires the retail and cold-recovered runtime leases, profile-filters both, repeats the engine-owned `VerifiedMapResidentBudget.ValidateUnion` as an invariant check, and exposes a combined preview index only when it matches the already recovery-admitted provider root. It records that exact root identity so `ConfirmRecovered` can return `None` only for the same provider/generation; any mismatch aborts ModData construction before the queue exists. Post-bind downloads use the prepare/commit token above, so duplicate UID/logical-name and aggregate rejection occurs before the durable boundary and a published runtime generation can never be silently absent from chooser/lobby. The loaded retail snapshot is immutable; an attempted retail provider change while an admission exists is rejected and, after normal game start, imported-content mutation is already prohibited. Public provider callbacks are coalesced best-effort notifications only, never the critical union-commit mechanism. Tests interleave retail repair/startup publication with recovery/bind/download, use same UID/same logical name with different UID, change retail resident usage by `+1` across launches, cross every combined cap, inject a different provider/root into `ConfirmRecovered`, admission disposal/staleness, and throwing provider observers, and assert recovery result, download result, durable pointer, runtime provider, and combined `MapCache` index always identify the same generation or the explicit empty quarantine state.

The generation tests enforce at most 128 runtime maps, 1 GiB current generation, 2 GiB current+rollback, immutable same-volume generation directories, versioned/checksummed pointer/journal/receipt, complete SHA-256 on cold start, live lease pinning, no eviction of selected/current/rollback/live content, and deterministic crash recovery at each pointer boundary. Downloaded maps never satisfy retail Base readiness. V1 runtime snapshot entries also expose `CanDelete=false`: eviction/removal remains an internal journaled store operation, never the legacy synchronous `MapPreview.Delete` path. `MapPreviewRuntimeInstallTest` proves the iOS chooser shows no false Delete/Delete All affordance and a direct delete call cannot invalidate or remove the current snapshot; desktop writable-map behavior remains unchanged.

- [ ] **Step 2: Run RED**

```bash
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
git commit -m "Add verified runtime map downloads"
```

- [ ] **Step 5: Generate and verify `0025` immediately**

Use official+`0001`...`0024` and the exact seven-path engine allowlist:

```bash
PREDECESSOR_END=24
CANDIDATE=engine-patches/0025-verified-runtime-map-store.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0025 --output "$ALLOWLIST"
```

Run the exact common block, then commit only patch/README/verifier test:

```bash
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

Task 13 implements `IosRuntimeMapServices` only as an inert, deferred composition factory. It captures the fixed Application Support roots, storage policy, directory durability, transports, clock, exact V1 policy adapters, and one independently usable lifecycle game-thread dispatcher, but it does not construct a contract-bound store, open an active catalog, mutate AppDelegate, or register any service in this task. Task 14 may use only that exposed dispatcher while cold-opening/repairing the active retail catalog. The factory's single `Create` method requires the *final* `Ra2RuntimeContract`, fixed trusted manifest selection/profile, defensive-copied immutable `MapCompatibility` set, and authenticated retail `MapResidentUsage` that exist only after Task 14 reaches a final Ready active lease. Calling `Create` with a missing/default/mismatched identity or usage that does not equal the final receipt fails before touching runtime storage.

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
	IGameThreadDispatcher, IModDataBoundService, IDisposable
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
	public bool TryTransferCoreServicesTo(
		ModDataServiceCollection freshServices);
	public void Dispose();
}

internal sealed class IosRuntimeMapServices : IDisposable
{
	public IGameThreadDispatcher GameThread { get; }
	public RetailOperationResult<IosRuntimeMapServiceSet> Create(
		Ra2RuntimeContract contract,
		TrustedManifestSelection selection,
		Ra2RuntimeProfileId profile,
		ImmutableArray<string> mapCompatibility,
		MapResidentUsage reservedRetailMapUsage);
	public void Dispose();
}
```

`LifecycleGameThreadDispatcherCore` is platform-neutral and behavior-tested by `OpenRA.Test`. Before ModData binding it uses the injected pre-game backend; the iOS adapter captures the exact UIKit main thread and posts through `BeginInvokeOnMainThread`, while main-thread calls run inline. At end-of-ModData construction its binding obtains a backend whose `Post` is the thread-safe `Game.RunAfterTick`, verifies the bound thread is the captured game/main thread, and subscribes the injected memory-pressure source against that exact `modData.MapCache`. The unbind token first rejects new work, drains/cancels queued callbacks, removes the observer, and then releases the ModData reference. Cancellation before execution completes the task without invoking the action; exceptions propagate exactly once. Fake backends/sources cover pre-bind background publication, inline publication, bind timing, post-bind queueing, warning trim, unbind races, same-mod rebind, and no callback/reference after unbind/dispose. `IosMainThreadDispatchBackend` and `IosMemoryPressureSource` are thin source-contract/iOS-build adapters only.

Task 14 constructs the inert factory before active-catalog work and uses `GameThread` for every pre-bind retail snapshot publication, repair, and refresh loop. Only after a final Ready lease fixes the exact profile/contract does it invoke `Create` exactly once. `Create` builds all contract-bound components under local ownership and validates their identity/limits before committing anything; any intermediate failure disposes those locals in reverse order and leaves the factory as sole dispatcher owner. A successful call atomically transfers dispatcher ownership from the factory into the returned set, creates one engine-owned `MapPreviewBudgetLedger` with the exact V1 64 MiB CPU/128 MiB GPU caps, embeds that same ledger in `MapPreviewCachePolicy`, and passes its read-only `IMapPreviewBudgetProvider` view to `VerifiedRuntimeMapStore`; no second ledger or static counter exists. It constructs `RuntimeMapGenerationStore` with the exact fixed `IosRetailImportRoots.RuntimeMapRoot`, `IosRetailStoragePolicy`, and `IosRetailDirectoryDurability`; constructs `Ra2RuntimeMapContextFactory` with the fixed trusted manifest/profile and exact expected runtime identity; and constructs `VerifiedRuntimeMapStore` with the generation store, concrete runtime transport, Task 10 validator, context factory, that same dispatcher, clock, preview provider, and exact limits. After transfer, the factory's `GameThread` getter rejects reuse and its `Dispose` is an idempotent no-op for that dispatcher. The context factory receives the actual loaded ModData later only through the store's Task 3 instance-binding lifecycle; it never creates or retains a pre-game scope and never reaches `Game.ModData`. The same result owns the verified remote metadata client with its clock/limits/compatibility set and `MapPreviewCachePolicy`.

Task 13 source-contract tests lock the deferred factory's complete input/output ownership, every constructor argument, and failure-before-I/O behavior, and reject construction from Task 14-only concrete types, a static/global fallback, or raw-path validation overload. `IosRuntimeMapServiceSet.TryTransferCoreServicesTo` requires a fresh unsealed collection and submits one owned batch ordered dispatcher, runtime store, remote client, preview policy. A rejected batch mutates neither object and the set remains sole owner; a successful batch atomically detaches the set and makes the fresh collection sole owner, so the set's later `Dispose` is an idempotent no-op. Task 14 submits retail provider plus host as a second owned batch, then seals. If that second batch or sealing fails, disposing the still-unsealed fresh collection releases every transferred service exactly once; after sealing, the provider owns reverse-order disposal. The sealed provider must resolve the exact same six object identities or it is immediately disposed and no startup state is produced. The Task 13 iOS build proves this inert factory compiles without any active-catalog implementation. All invocation and service registration are explicitly deferred to Task 14.

`IosRuntimeMapServices` maps all applicable Task 1 V1 values into engine `RemoteMapMetadataLimits`, `RemoteMapPreviewCachePolicy`, complete `PngReadLimits`/`MapPreviewCachePolicy`, and `RuntimeMapStoreLimits`, including the four exact 512-record/16 MiB/4 MiB/32,768 remote-retention fields, authenticated final retail metadata/string reservation, remaining global resident budgets, exact 64 MiB/128 MiB preview limits bound to the shared ledger, the 512 MiB safety margin, and exact deadlines. It passes the remote policy into `VerifiedRemoteMapMetadataClient` and `MapCache` observes only that client's interface property; identity/equality tests reject a missing, substituted, or hard-coded policy. It rejects any reserved value above the global cap rather than underflowing a runtime allowance, and rejects a policy/store preview-limit mismatch before any runtime root I/O. The bound dispatcher routes `UIApplication.DidReceiveMemoryWarningNotification` to `MapCache.TrimUnpinnedPreviews()` on the game thread; that operation also trims unpinned remote metadata/encoded previews under their separate ledger. Unbind/disposal removes the observer, and tests prove no callback survives shutdown or mutates MapCache from the UIKit thread.

- [ ] **Step 5: Run GREEN, build iOS, and commit**

```bash
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
git commit -m "Coordinate private iOS retail source imports"
```

The two engine tests join the exact `0026` allowlist; do not generate that patch yet. Record the checked-in AOT registry SHA before and after the iOS build. If the generator legitimately changes it for Tasks 6–13, keep that one deterministic tracked diff unstaged and untouched through Task 14; Task 15 owns its coverage review and commit. Any other generated-file drift fails this task.

---

### Task 14: Mount only the active catalog and replace the importer UI/lifecycle

**Outer core files:**

- Modify: `OpenRA.Mods.RA2/Content/RetailImportModels.cs`
- Modify: `OpenRA.Mods.RA2/Content/RetailContentImporter.cs`
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
- Create: `ios/OpenRA.iOS/IosContentWizardViewController.cs`
- Modify: `ios/OpenRA.iOS/AppDelegate.cs`
- Modify: `ios/OpenRA.iOS/ViewController.cs`
- Modify: `mods/ra2/fluent/chrome.ftl`
- Modify: `mods/ra2/fluent/zh-CN/chrome.ftl`

**Engine production files for `0026`:**

- Create: `engine/OpenRA.Game/Support/IosGameContentManagement.cs`
- Modify: `engine/OpenRA.Game/Map/VerifiedMapSnapshot.cs`
- Modify: `engine/OpenRA.Game/Map/MapCache.cs`
- Modify: `engine/OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs`

**Engine tests for `0026`:**

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

- [ ] **Step 1: Write RED active-catalog isolation tests**

```csharp
public interface IVerifiedContentHandle : IDisposable
{
	string LogicalName { get; }
	long Length { get; }
	Stream OpenRead();
}

public interface IVerifiedContentHandleFactory
{
	IVerifiedContentHandle Open(
		RetailActiveGenerationSnapshot snapshot,
		RetailCatalogArtifact artifact);
}

public sealed record RetailCatalogQuarantine(
	RetailContentGroup Group,
	string LogicalName,
	RetailImportFailure Failure);

public sealed class RetailActiveCatalogLease :
	IVerifiedMapSnapshotProvider, IDisposable
{
	public Ra2RuntimeProfileId Profile { get; }
	public Ra2RuntimeContract Contract { get; }
	public MapResidentUsage MapResidentUsage { get; }
	public ImmutableArray<RetailCatalogQuarantine> QuarantinedItems { get; }
	public IReadOnlyPackage AcquirePackage();
	public event Action SnapshotChanged;
	public VerifiedMapSnapshotLease Acquire();
	public void Dispose();
}

public static class RetailActiveCatalog
{
	public static Task<RetailActiveCatalogOpenResult> OpenAsync(
		RetailActiveGenerationSnapshot snapshot,
		IVerifiedContentHandleFactory handleFactory,
		Ra2RuntimeContract expectedCurrentContract,
		IGameThreadDispatcher gameThread,
		IProgress<RetailImportProgress> progress,
		CancellationToken cancellationToken);
}

public sealed record RetailActiveCatalogOpenResult(
	RetailActiveCatalogLease? Lease,
	RetailReadinessResult Readiness,
	RetailCatalogRepairPlan? Repair,
	RetailImportFailure? Failure);

internal sealed class IosActiveCatalogMountRegistry :
	IVerifiedMapSnapshotProvider, IDisposable
{
	public IosActiveCatalogMountRegistry(
		RetailActiveCatalogLease ownedCatalog);
	public event Action SnapshotChanged;
	public VerifiedMapSnapshotLease Acquire();
	public IReadOnlyPackage AcquireMountedPackage();
	public void Dispose();
}
```

`RetailActiveCatalog.OpenAsync` is the sole normal-startup cold-hash owner. It computes the known total from authenticated receipt lengths, reports `VerifyingActiveHashes` after each bounded read chunk with monotonic completed bytes and that exact total, and publishes no percentage when the total is unavailable or inconsistent. It performs complete SHA-256 for active top-level archives and every active map before returning a lease. The caller must first build `expectedCurrentContract` from the receipt's closed profile plus the current manifest/filename catalog/map format, current validator versions, captured finished-bundle identity, and AOT identity. Open rejects a default/wrong-profile contract. Matching contract and validator versions may skip only nested structural parsing after those hashes match; any mismatch returns a contract-refresh repair plan and never a Ready lease until full structural revalidation publishes a new generation. The method accepts only Task 7's authenticated generation snapshot and generation-relative artifacts, never a staging artifact, root path, or catalog DTO assembled by the caller. After all entries validate, it constructs a `VerifiedMapSnapshotPublisher` with the injected dispatcher/affinity and invokes the initial `Publish` only through `IGameThreadDispatcher.InvokeAsync`; the returned lease owns that publisher. Failure/cancellation before or during publication clears/disposes it through the same dispatcher, so no RA2 assembly constructs an internal root pin or publishes from the UIKit/hash thread. Tests prove initial publication, repair swap, clear, and disposal all run on the game thread, and that off-thread direct use fails.

The method does not consume or dispose the caller's snapshot: it acquires an independent ref-counted root pin for the returned lease, while the caller always disposes its snapshot after open/repair orchestration. Failure/cancellation releases every temporary pin/handle; a successful lease owns its pin until its packages/maps/streams release their references. The lease itself implements the retail `IVerifiedMapSnapshotProvider`; there is no borrowed `Maps` child getter. `AcquirePackage()` returns a separately pinned caller-owned package.

Before service transfer the coordinator exclusively owns the lease. It constructs one `IosActiveCatalogMountRegistry` that takes sole ownership of that lease and itself implements/forwards `IVerifiedMapSnapshotProvider`. The registry's `AcquireMountedPackage()` asks its owned catalog for a fresh independently pinned package on every loader-decoration call; the decorated loader owns that per-ModData package, while the registry is never owned or disposed by an individual loader. The second atomic service batch registers the **registry aggregate**, not the underlying lease, as `Owned<IVerifiedMapSnapshotProvider>`. Batch rejection leaves the registry (and therefore lease) with the coordinator; batch success transfers the aggregate to the sealed service provider and clears every coordinator reference. The options' filesystem-decorator closure only borrows this same aggregate, which persists across sequential same-mod reconstruction because the sealed provider owns it. Final provider disposal closes the registry/lease exactly once and rejects new loader/map acquisitions; already-open packages/maps/streams retain independent pins until they close. Thus no registry exists outside the Task 3 ownership graph and the decorator captures no unowned disposable. Tests cover exact/monotonic byte progress, known and unavailable totals, cancellation before publication, no fabricated percentage or duplicate hash, registry construction/batch/seal/game-init failure, sequential same-mod loader recreation, both final loader/provider shutdown orders, concurrent repair while an old lease remains readable, no double-dispose/leak, and streams outliving aggregate disposal.

The method returns a typed readiness/lease/repair/failure result. A Base mismatch or loss of every Base-compatible map closes the gate. A Yuri receipt is Ready only when at least one surviving map has the Yuri compatibility bit; loss of the last such map returns a repair plan that retains valid Yuri archive bytes as inactive/Unavailable, chooses Base if a Base-compatible map survives, and requires the real validator to publish a newly recomputed Base contract/readiness generation before reopening. Corrupt Yuri/music/one map otherwise returns a closed repair plan while valid Base plus another selected-profile-compatible map stays usable. `RetailGenerationStore.OpenActiveAsync` preserves and returns that same authenticated caller-owned snapshot on repair/refresh outcomes; the coordinator passes it with the plan to `IRetailCandidateValidator.ValidateRepairAsync`, then passes the returned recomputed catalog to `RetailGenerationStore.PrepareRepairAsync`, publishes the new generation, reopens it, and disposes the old snapshot exactly once on every success/failure/cancel branch. Open/store never invent profile semantics or edit in place. A current manifest/build/validator digest change follows the same revalidation-and-refresh transaction even when no content group is excluded: identical identity is a no-op, a changed identity performs full structural validation and publishes a new receipt/catalog generation, and failure preserves the old bytes while closing the gate with a typed update/revalidation failure. Tests cover complete Yuri archives with only a Base-compatible map, Yuri-compatible-map retention, cold loss of the final Yuri-compatible map followed by Yuri-to-Base contract recomputation, single-map repair, identity no-op, one identity input change, and revalidation failure. Repair/refresh never mutates the currently leased generation; GC waits for later cold-start validation and every old lease.

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
	RetailImportFailure? Failure);

public sealed record RetailWizardInput(
	RetailWizardActivity Activity,
	RetailWizardGate Gate,
	RetailPendingOpenDisposition Pending,
	RetailRetryDisposition RetryDisposition,
	RetailReadinessResult Readiness,
	RetailImportProgress? Progress,
	RetailImportFailure? Failure);

public static class RetailContentWizardStateMachine
{
	public static RetailWizardModel Derive(RetailWizardInput input);
}
```

`Derive` is the sole UI-state mapping. It validates the closed activity/gate/pending/retry combination, derives readiness only from the immutable five-key result, and rejects contradictory inputs rather than choosing an arbitrary priority. Exhaustively test every valid combination and every invalid cross-product. `Checking` and `Recovering` expose none. `NeedsBase` is exactly Choose Files + View Licenses with label `ChooseFilesOrFolder`. `PreparingSources` and `Importing` are exactly Cancel; `Committing` exposes none. `BaseReady` is exactly Enter Game + Import Optional. `FullyReady` is exactly Enter Game + Manage Game Content. `PendingFailedWithReadyActive` is exactly Enter/Retry/Details/Discard with `ResumeSealed` and label `Retry`. A retryable failure with an authenticated sealed pending snapshot is Retry/Details/Discard + `ResumeSealed`/`Retry`; one without a retained source is Choose Files/Details + `ChooseAgain`/`ChooseAgain`; active recheck is Retry/Details + `RecheckActive`/`CheckAgain`. `RuntimeResourcesMissing` is exactly View Details. Both `UpdateRequired` and `ResetRequired` are View Details + Reset Imported Content so a future-version journal has a recovery escape hatch; invoking reset always requires a separate destructive confirmation before the action crosses into the coordinator. No Enter Game appears during checking, first import, conversion, commit, recovery, update/reset requirement, or static runtime-preflight failure.

The scroll view title is exactly `Original Game Content` / `原版游戏内容` and contains exactly five status rows in this order: Base, RA2 Music, Yuri, Yuri Music, Maps. Directly beneath the title, a wrapped, always reachable disclosure reads exactly: `Import files only from a legally owned copy of Command & Conquer: Red Alert 2 / Yuri's Revenge. NUKE HOUR is an independent project and is not affiliated with or endorsed by Electronic Arts.` / `请仅从您合法拥有的《命令与征服：红色警戒 2》/《尤里的复仇》副本导入文件。NUKE HOUR 是独立项目，与 Electronic Arts 无隶属关系，也未获其认可或赞助。` These strings live only in the existing `IosNativeStrings` English/Chinese table; they are not sourced from imported files or remote metadata. Content and layout tests require the complete disclosure in both locales at every accessibility size and prove it remains reachable by scrolling before any picker action.

Each status row renders only Missing, Invalid, Unavailable, or Ready from the immutable five-key readiness map. Availability and edition recognition are separate: `Validation=Ready` plus `Recognition=CompatibleUnrecognized` renders Ready with a sanitized “compatible, unrecognized edition” detail and may satisfy Enter Game, while `Validation=Unavailable` renders Unavailable. It never labels an unrecognized hash as an official known edition and never treats hash recognition as a substitute for structural validation. Tests prove an unknown-hash but structurally valid Base plus map can enter, whereas structurally invalid content cannot. The view never infers state from file presence. Tests also bind the current phase, truthful determinate/indeterminate progress, sanitized detail code, action set, title, and legal disclosure for every state. Layout tests cover every supported iPhone/iPad landscape safe area, rotation, native/effective scale, English/Chinese longest text, and accessibility sizes. Use wrapped labels, at least 48-point buttons, and no overlap/clipping.

- [ ] **Step 3: Run RED**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailActiveCatalogTest|FullyQualifiedName~VerifiedCatalogPackageTest|FullyQualifiedName~RetailContentWizardStateMachineTest|FullyQualifiedName~IosContentWizardLayoutPolicyTest|FullyQualifiedName~IosGameContentManagementTest|FullyQualifiedName~IosContentImportContractTest' \
  -v minimal
```

Expected: current flat `Content/ra2` scan/mount, catch-all mount suppression, fixed six-button wizard, and always-immediate importer cannot satisfy the contracts.

- [ ] **Step 4: Pin verified Darwin file identities and mount once**

`IosVerifiedContentHandleFactory` opens with `O_RDONLY | O_NOFOLLOW | O_CLOEXEC`, pins the descriptor identity, verifies regular-file type and expected length, and retains that descriptor for object lifetime; it does not hash. `RetailActiveCatalog.OpenAsync` is the single SHA-256 owner and hashes the pinned descriptor with explicit-offset `pread`, compares the authenticated expected digest, reports each chunk through its progress/cancellation contract, and performs no second digest pass. `OpenRead()` then acquires its own duplicated descriptor/reference and returns a seekable `PreadFileStream` with an independent logical `Position`; all reads use `pread`, so streams never share or mutate an open-file-description offset. Disposing the package/handle prevents new streams but does not invalidate already-open streams; the generation lease remains pinned until the final stream releases its reference, then every descriptor closes and GC may proceed. Tests prove the one hash pass is exact, hash-then-first-read starts at byte zero, two streams interleave seek/read independently and concurrently, package/handle disposal before stream disposal still reads the same inode, final-stream release enables generation GC, exception paths leak no descriptors, and rename/unlink after admission cannot change bytes. Directory traversal uses pinned parent descriptors and rejects symlink components.

`VerifiedCatalogPackage` exposes exact archive handles plus a maps child backed by the verified snapshot. `IosContentFileSystemLoader` mounts required system packages, then the `content` package obtained from the active registry, then manifest content packages. Remove the catch-all; only manifest entries explicitly marked optional may be absent. `IosActiveCatalogMountRegistry` is the instance aggregate owned by the sealed service provider and captured only by that startup state's `FileSystemLoaderDecorator`; its catalog identity is fixed once when it takes the Ready lease and there is no `Install`/replace API. Every decorator invocation/`ModData` construction calls `AcquireMountedPackage()` exactly once and receives a fresh independently pinned package owned by that configured loader. Disposing one loader never disposes the registry or prevents a later sequential same-mod reconstruction; final provider disposal closes the registry after rejecting new acquisitions, while already-open packages/streams retain their own pins. There is no static registry or mount setter.

The iOS bootstrap registers one retail `IVerifiedMapSnapshotProvider` and the distinct `IVerifiedRuntimeMapStore` in Task 3's collection before sealing startup. `MapCache` composes the retail provider with `runtimeStore.Snapshots`; duplicate service types remain forbidden. Retail maps satisfy initial readiness and are filtered by profile, while runtime maps remain distinct. Provider snapshot changes are accepted only on the game thread and old leases remain alive until every preview reference releases.

- [ ] **Step 5: Implement the pre-game coordinator and responsive UIKit view**

`AppDelegate` unconditionally constructs the importer for Personal and PublicClean, runs non-cancellable retail generation recovery, fully validates the recovered active catalog, passes only its opaque Ready capability into pending-consumption reconciliation, then resolves any still-visible pending import and shows the wizard only when needed. A repair/update/non-Ready active snapshot supplies no consumption capability, so it cannot delete a retryable pending seal. The coordinator exclusively owns one import/startup `PreGameEngineContext` across validation/retry and lends it to `RetailCandidateValidator`; every attempt closes its temporary scope without closing the context. Before opening content, it constructs `IosRuntimeBuildIdentityProvider`, captures the pinned all-app-local managed/AOT build identity once, resolves the authenticated snapshot receipt's closed profile to its fixed manifest, checks static runtime resources, and builds the expected *current* `Ra2RuntimeContract`. It also constructs Task 13's inert runtime-services factory, then passes that expected contract and the factory's still-owned `GameThread` to `RetailActiveCatalog.OpenAsync`. Cold hash, repair, and receipt-refresh loops continue with that same dispatcher; after any repair/profile change the coordinator recomputes the expected contract from the same captured build identity and the new fixed profile before reopening. No contract-bound runtime store exists during those loops. Only a Ready lease whose receipted contract equals the recomputed expected contract fixes the final catalog/profile/contract, installs that state into the pre-game startup state, and publishes its verified retail snapshot before service collection sealing.

Only after the Ready lease proves its receipted contract equals the just-computed current contract does the coordinator first construct the `IosActiveCatalogMountRegistry`, transferring the lease into that still-coordinator-owned aggregate while retaining its authenticated `MapResidentUsage` value. It then invokes Task 13's deferred `IosRuntimeMapServices.Create` with that usage, atomically moving the already-used dispatcher into the contract-bound service set and producing the store/factory, remote client, and preview policy without opening any additional pre-game scope. Before registering services or calling `SealForGame`, the coordinator invokes `runtimeMapStore.RecoverAsync(contract.ToEngineIdentity(), registry, CancellationToken.None)` exactly once for that cold startup. Runtime recovery authenticates/cold-hashes only the matching contract directory, converges any journal, and admits the recovered runtime set against an acquired lease from this exact final retail provider before `MapCache` can see it; it does not require a bound ModData and leaves downloads disabled until normal ModData construction repeats/acknowledges the union and binds the store. A typed runtime reset/update/corruption failure exposes an empty or independently verified previous runtime snapshot and a sanitized optional-service status. A cross-launch duplicate/aggregate conflict journaledly quarantines runtime maps and exposes an empty runtime snapshot rather than returning Ready with a hidden disk-current generation. None of these optional outcomes blocks otherwise Ready retail content, and downloaded maps never satisfy retail readiness.

The coordinator then creates a fresh `ModDataServiceCollection`. The deferred set atomically transfers its first owned batch (game-thread dispatcher, recovered runtime-map store, remote-metadata client, preview policy); Task 14 constructs the active-catalog registry aggregate and atomically transfers the second (that registry registered/owned as `IVerifiedMapSnapshotProvider`, plus game-content-management host), preserving the six-service order, and seals the collection for `ModDataConstructionOptions`. The instance-scoped catalog loader is the separate decorator but borrows the provider-owned registry and owns only the fresh package returned for that ModData construction. A rejected second batch leaves the registry/lease with the coordinator; successful transfer clears both ownership references. Any failure after an earlier successful batch disposes the unsealed collection, which owns and releases that batch; a seal failure likewise leaves the collection as the sole owner. After successful seal, only the provider owns all six, including the registry aggregate. The coordinator verifies all six exact identities through the provider; mismatch disposes the provider and aggregate and never produces a startup state. The coordinator calls `SealForGame` while its borrowed context is still alive, then disposes every validation/pre-game scope and the coordinator context before normal startup. `Game.InitializeAndRun` constructs the real ModData; Task 3 binds the dispatcher first and runtime store/factory second before the instance escapes, while reverse unbind stops the store before dispatcher/MapCache teardown. Validation/batch/seal failure disposes validator/scopes/context/factory/set/services/aggregate pins exactly once and never produces a startup state; after successful seal only `EngineStartupState`/Game owns the provider and therefore the registry. Tests lock the order recover pointer/receipt → capture current bundle identity → derive receipt-profile expected contract/static preflight → construct dispatcher factory → retail cold hash/snapshot/repair, recomputing after profile changes, until expected==receipted Ready → reconcile the exact Ready capability and hide/consume its linked pending ID → resolve any remaining pending decision → construct registry aggregate → create contract-bound runtime services/transfer dispatcher → one non-cancellable runtime-map recovery → transfer-core-batch → transfer-registry/host-batch → seal/identity-check → all pre-game scope/context disposal → game start → decorator acquires per-ModData package → dispatcher bind → runtime-store bind, then exercise post-start publication, validation, warning trim, same-mod reconstruction, and unbind/rebind. They prove no non-Ready snapshot reaches consumption, no Ready lease or `Create` occurs with a stale/pre-repair identity, inject rejection/failure at every capture/preflight/open/consume/create/aggregate/batch/seal boundary, cover recovery failure isolation, no stale/wrong-contract snapshot, no download before bind, no recovery call after game startup, retry/final-seal, and no use-after-dispose/double-dispose. It never recreates a string-only startup after sealing. `#if PUBLIC_CLEAN` must not bypass this path.

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

`AppDelegate` creates one lifecycle-bound host and registers it as the sixth distinct service (after the retail-map provider) in the second owned batch before sealing `ModDataServiceCollection`. `MainMenuLogic` resolves `IIosGameContentManagementHost` from its current `ModData`; on iOS it wires the existing `CONTENT_BUTTON` only when `CanOpen` is true and calls that instance's `TryOpen`, while an absent service preserves the desktop `ContentInstallerFileSystemLoader` behavior. The host is same-thread, reused by same-mod reconstruction through the same sealed provider, and explicitly disposed at final game/app shutdown; after disposal `TryOpen` is false and no UIKit/coordinator reference remains. Tests reject duplicate registration and prove reconstruction, disposal, and a stale prior host cannot be observed. There is no static host or registration API.

The in-game manager is unavailable during a match. It may coordinate-copy and envelope-validate into `PendingValidation`, then shows localized `Restart to finish import`. It never converts maps, swaps the active pointer, changes profile/contract, replaces `Game.ModData`, mutates the retail mount, or triggers an in-process mod reload. Next launch performs the full validation/activation, binds the exact pending ID into the committed receipt, and completes Task 8's durable consumption protocol before any pending is offered again. Tests prove the old active generation remains byte-identical until that fresh launch, the exact pending becomes active only there, and subsequent `OpenPendingAsync` is `Missing` across every consumption crash boundary. This task removes the legacy raw-path request/result/importer overload and migrates `RetailContentImporterTest` to the final typed/session API; no production caller or public test retains the old seam.

- [ ] **Step 7: Run GREEN, full suites/builds, and commit**

```bash
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~RetailActiveCatalogTest|FullyQualifiedName~VerifiedCatalogPackageTest|FullyQualifiedName~RetailContentWizardStateMachineTest|FullyQualifiedName~IosContentWizardLayoutPolicyTest|FullyQualifiedName~IosGameContentManagementTest|FullyQualifiedName~IosContentImportContractTest' \
  -v minimal
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 -v minimal
./utility.sh --check-yaml
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -c Release -f net8.0-ios -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:CodesignDisable=true -p:TargetPlatform=ios -m:1
git diff --check
```

Force-add the seven previously deferred ignored engine tests only after comparing their recorded Task 6–8 hashes, then stage the remaining listed paths. Source-contract tests must prove the old `RetailImportRequest`, free-form result `Message`/`Hashes`, and raw-path importer overload no longer exist in either production file. Keep only the known deterministic `GeneratedAotObjectRegistry.g.cs` diff unstaged for Task 15 and reject any other out-of-allowlist change. Commit exactly the 34 Task 14 paths; no other ignored engine file may enter the index:

```bash
git commit -m "Gate iOS startup on verified retail content"
```

Do not generate `0026` until Task 15 adds its AOT/source/bundle contracts.

---

### Task 15: Add AOT and bundle audits, then deliver `0026`

**Packaging/iOS files:**

- Create: `packaging/audit_ios_retail_import_bundle.py`
- Create: `packaging/tests/test_ios_retail_import_bundle.py`
- Modify: `packaging/audit_ios_touch_runtime_bundle.py`
- Modify: `packaging/tests/test_ios_touch_runtime_bundle.py`
- Modify: `packaging/tests/test_public_bundle_audit.py`
- Create: `packaging/ios-personal-official-engine-assets.json`
- Modify: `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Verify or deterministically modify: `ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs`

**Final exact `0026` engine allowlist:**

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

- Create: `engine-patches/0026-ios-retail-active-catalog-lifecycle.patch`
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
	--repository ROOT --official-zip PINNED_ZIP --patch-end 25 \
	--output MANIFEST [--check] [--candidate-allowlist EXACT_CODE_ONLY_ALLOWLIST]
```

This mode uses the same safe extractor and strict predecessor replay policy as `verify_engine_patch_delivery.py`; `--check` regenerates under a fresh temporary root and compares bytes without writing the tracked manifest. The asset provenance is intentionally bound to official+`0001`...`0025`. Before the not-yet-delivered `0026` exists, `--candidate-allowlist` validates and records the byte-sorted exact allowlist digest and requires every listed path to be code/test-only and ineligible as a bundle resource. Step 6 then parses the delivered `0026` headers, requires byte-for-byte equality with that recorded allowlist, and repeats the zero-resource check. This avoids asking a pre-sign audit to read a patch that does not exist yet.

Require exact importer/profile/catalog/map-store managed assemblies and public type/method metadata; exact AppDelegate/coordinator/loader startup wiring; both profile manifests; no interpreter; and generator coverage for every new YAML/ObjectCreator constructible type. Tamper tests delete one type/method, remove startup recovery, restore raw `Content/ra2` mounting, bypass PublicClean gate, remove a V1 constant, make the content package writable, or omit the AOT constructor and must fail with a precise error.

For both configurations, enumerate the finished bundle's regular-file and directory relative paths and reject exact RA2 retail basenames/signatures and importer runtime objects (`publish.journal`, `active.ptr`, `receipts.bin`, generation/pending directory shapes). Do not globally reject `.oramap` or every non-retail `.mix`: Personal legitimately ships project maps and official-engine assets including many `.oramap` files plus `engine/mods/cnc/bits/snow.mix`. Provenance is split and fail-closed: outer/project content must match an exact `git ls-files` path/hash/size record, while official-engine baseline assets must match the reviewed `packaging/ios-personal-official-engine-assets.json`. That versioned manifest records the pinned official-ZIP SHA, patch end `0025`, aggregate patch-series/replay digest, and every eligible bundle path/hash/size; it is generated and byte-verified from a fresh strict official replay, never from the ignored current `engine/` tree. The final `0026` header allowlist is independently parsed and must contain zero bundle-resource-eligible paths. PublicClean additionally requires its existing public manifest classification. Tests regenerate the manifest from a synthetic/fresh replay contract, prove clean official `.oramap` and `snow.mix` entries pass, and reject ignored-tree replacement, wrong official ZIP/patch-series digest, an asset-eligible `0026` header, missing/extra path, or changed bytes. Unknown files with those extensions, any retail signature/basename, mutated allowlisted bytes, or any importer runtime shape fail. Do not substring-scan managed binaries for fixed retail-name constants because the implementation must contain them. Separately scan eligible text/plist metadata for private fixture markers and absolute developer paths, and inspect managed metadata for only the expected type/method contracts. The audit also compares the complete finished-app managed-image inventory with Task 4's all-app-local `.dll` rule and fails a missing/extra/duplicate/unparseable image. It never permits a blanket extension/directory exemption. For IPA input, parse ZIP centrally with traversal/duplicate/symlink/special/size limits, require exactly one canonical `Payload/*.app`, reject extra payload roots, audit the extracted app identically, require the explicit `--reference-app` built in the same configuration/commit, and compare its critical managed/resource manifest with the extracted app. Missing, stale, wrong-configuration, or wrong-build reference apps fail; the audit never guesses a sibling output.

PublicClean's existing audit still reports the separately pinned historical failures until runtime closure. This task must prove the importer changes add no new `(code,path)` errors and must not reclassify those failures as passing.

- [ ] **Step 2: Run RED**

```bash
python3 -m unittest \
  packaging.tests.test_ios_retail_import_bundle \
  packaging.tests.test_ios_touch_runtime_bundle \
  packaging.tests.test_public_bundle_audit -v
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~AotObjectRegistryCoverageTest|FullyQualifiedName~IosContentImportContractTest' \
  -v minimal
```

Expected: missing audit/target and absent importer AOT/source contracts.

- [ ] **Step 3: Implement an unconditional pre-sign audit target**

Add `AuditIosRetailImportBundle` after `_CreateAppBundle` and before `_CodesignAppBundle`. Personal touch audit and PublicClean public-bundle audit both depend on it. Do not depend on MSBuild target declaration order. Pass exact `$(AppBundleDir)`, repository root, and `$(TargetDir)`; never scan a stale sibling build.

Generate the AOT registry using the existing target. Compare its pre/post bytes. If unchanged, assert that explicitly and do not touch the file. If the generator legitimately adds factories, verify exact generated output, AOT coverage, and source audit before staging that one deterministic diff.

- [ ] **Step 4: Run GREEN and fresh isolated builds**

```bash
python3 -m unittest \
  packaging.tests.test_ios_retail_import_bundle \
  packaging.tests.test_ios_touch_runtime_bundle \
  packaging.tests.test_public_bundle_audit -v
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~AotObjectRegistryCoverageTest|FullyQualifiedName~IosContentImportContractTest' \
  -v minimal
```

Materialize the exact candidate into two fresh clones before building it:

```bash
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
TASK15_STAGE="$(mktemp -d /private/tmp/nuke-hour-task15.XXXXXX)"
TASK15_PATCH="$TASK15_STAGE/candidate.patch"
PERSONAL_CLONE="$TASK15_STAGE/personal"
PUBLIC_CLONE="$TASK15_STAGE/public"
TASK15_ENGINE_ALLOWLIST="$TASK15_STAGE/0026-engine.allowlist"
python3 packaging/engine_workspace.py allowlist \
  --patch-id 0026 --output "$TASK15_ENGINE_ALLOWLIST"
test "$(wc -l < "$TASK15_ENGINE_ALLOWLIST" | tr -d ' ')" -eq 20
LC_ALL=C sort -c -u "$TASK15_ENGINE_ALLOWLIST"
python3 packaging/audit_ios_retail_import_bundle.py official-provenance \
	--repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" --patch-end 25 \
	--output packaging/ios-personal-official-engine-assets.json \
	--candidate-allowlist "$TASK15_ENGINE_ALLOWLIST"
python3 packaging/audit_ios_retail_import_bundle.py official-provenance \
	--repository "$PWD" --official-zip "$OPENRA_OFFICIAL_ZIP" --patch-end 25 \
	--output packaging/ios-personal-official-engine-assets.json --check \
	--candidate-allowlist "$TASK15_ENGINE_ALLOWLIST"

git add -- \
  packaging/audit_ios_retail_import_bundle.py \
  packaging/tests/test_ios_retail_import_bundle.py \
  packaging/audit_ios_touch_runtime_bundle.py \
  packaging/tests/test_ios_touch_runtime_bundle.py \
  packaging/tests/test_public_bundle_audit.py \
  packaging/ios-personal-official-engine-assets.json \
  ios/OpenRA.iOS/OpenRA.iOS.csproj
git add -f -- engine/OpenRA.Test/AotObjectRegistryCoverageTest.cs
if ! git diff --quiet -- ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs; then
  git add -- ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs
fi
git diff --cached --check
git diff --cached --binary > "$TASK15_PATCH"
test -s "$TASK15_PATCH"

git clone --shared --no-hardlinks "$PWD" "$PERSONAL_CLONE"
git clone --shared --no-hardlinks "$PWD" "$PUBLIC_CLONE"
git -C "$PERSONAL_CLONE" apply --index "$TASK15_PATCH"
git -C "$PUBLIC_CLONE" apply --index "$TASK15_PATCH"
python3 "$PERSONAL_CLONE/packaging/engine_workspace.py" materialize \
  --repository "$PERSONAL_CLONE" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 25 --workspace "$PERSONAL_CLONE/engine" \
  --overlay-allowlist "$TASK15_ENGINE_ALLOWLIST"
python3 "$PUBLIC_CLONE/packaging/engine_workspace.py" materialize \
  --repository "$PUBLIC_CLONE" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 25 --workspace "$PUBLIC_CLONE/engine" \
  --overlay-allowlist "$TASK15_ENGINE_ALLOWLIST"
git -C "$PERSONAL_CLONE" diff --cached --check
git -C "$PUBLIC_CLONE" diff --cached --check
git diff --cached --name-only --no-renames > "$TASK15_STAGE/source.paths"
git -C "$PERSONAL_CLONE" diff --cached --name-only --no-renames > "$TASK15_STAGE/personal.paths"
git -C "$PUBLIC_CLONE" diff --cached --name-only --no-renames > "$TASK15_STAGE/public.paths"
cmp "$TASK15_STAGE/source.paths" "$TASK15_STAGE/personal.paths"
cmp "$TASK15_STAGE/source.paths" "$TASK15_STAGE/public.paths"
git diff --cached --raw --full-index --no-renames > "$TASK15_STAGE/source.index"
git -C "$PERSONAL_CLONE" diff --cached --raw --full-index --no-renames > "$TASK15_STAGE/personal.index"
git -C "$PUBLIC_CLONE" diff --cached --raw --full-index --no-renames > "$TASK15_STAGE/public.index"
cmp "$TASK15_STAGE/source.index" "$TASK15_STAGE/personal.index"
cmp "$TASK15_STAGE/source.index" "$TASK15_STAGE/public.index"
```

Run the signed Personal candidate, create its IPA only from that exact codesigned app, and audit both artifacts:

```bash
: "${IOS_CODESIGN_KEY:?set the Personal Apple Development signing identity}"
: "${IOS_PROVISION_UUID:?set the Personal provisioning profile UUID}"
PERSONAL_APP="$PERSONAL_CLONE/ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app"
PERSONAL_TARGET="$PERSONAL_CLONE/ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64"
PERSONAL_IPA="$TASK15_STAGE/NUKE HOUR.ipa"

env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet restore \
  "$PERSONAL_CLONE/ios/OpenRA.iOS/OpenRA.iOS.csproj" \
  -p:RuntimeIdentifier=ios-arm64 -m:1
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet build \
  "$PERSONAL_CLONE/ios/OpenRA.iOS/OpenRA.iOS.csproj" \
  -c Release -f net8.0-ios -p:RuntimeIdentifier=ios-arm64 \
  -p:CodesignKey="$IOS_CODESIGN_KEY" \
  -p:CodesignProvision="$IOS_PROVISION_UUID" --no-restore -m:1
test -d "$PERSONAL_APP"
codesign --verify --deep --strict --verbose=4 "$PERSONAL_APP"
PERSONAL_PACKAGE="$TASK15_STAGE/personal-package"
mkdir -p "$PERSONAL_PACKAGE/Payload"
ditto "$PERSONAL_APP" "$PERSONAL_PACKAGE/Payload/OpenRA.iOS.app"
(cd "$PERSONAL_PACKAGE" && ditto -c -k --sequesterRsrc --keepParent Payload "$PERSONAL_IPA")
test -f "$PERSONAL_IPA"
python3 "$PERSONAL_CLONE/packaging/audit_ios_retail_import_bundle.py" "$PERSONAL_APP" \
  --source-root "$PERSONAL_CLONE" --managed-output-root "$PERSONAL_TARGET" \
  --configuration Personal
python3 "$PERSONAL_CLONE/packaging/audit_ios_retail_import_bundle.py" "$PERSONAL_IPA" \
  --source-root "$PERSONAL_CLONE" --managed-output-root "$PERSONAL_TARGET" \
  --configuration Personal --reference-app "$PERSONAL_APP"
git -C "$PERSONAL_CLONE" diff --exit-code -- \
  ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs
```

Run the fixed unsigned PublicClean simulator diagnostic and require the known gate, not a fabricated IPA:

```bash
PUBLIC_APP="$PUBLIC_CLONE/ios/OpenRA.iOS/bin/Release/net8.0-ios/iossimulator-arm64/OpenRA.iOS.app"
PUBLIC_TARGET="$PUBLIC_CLONE/ios/OpenRA.iOS/bin/Release/net8.0-ios/iossimulator-arm64"
PUBLIC_REPORT="$TASK15_STAGE/public-report"
mkdir -p "$PUBLIC_REPORT"
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet restore \
  "$PUBLIC_CLONE/ios/OpenRA.iOS/OpenRA.iOS.csproj" \
  -p:RuntimeIdentifier=iossimulator-arm64 -m:1
set +e
env DOTNET_ROOT=/Users/kevin/.dotnet \
  PATH=/Users/kevin/.dotnet:/usr/bin:/bin:/usr/sbin:/sbin \
  /Users/kevin/.dotnet/dotnet build \
  "$PUBLIC_CLONE/ios/OpenRA.iOS/OpenRA.iOS.csproj" \
  -c Release -f net8.0-ios -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:PublicClean=true -p:CodesignDisable=true -p:TargetPlatform=ios \
  -p:PublicAuditReportDir="$PUBLIC_REPORT" --no-restore -m:1 \
  > "$TASK15_STAGE/public-build.log" 2>&1
PUBLIC_RC=$?
set -e
test "$PUBLIC_RC" -ne 0
test -d "$PUBLIC_APP"
test -f "$PUBLIC_REPORT/public-bundle-audit.json"
python3 "$PUBLIC_CLONE/packaging/audit_ios_retail_import_bundle.py" "$PUBLIC_APP" \
  --source-root "$PUBLIC_CLONE" --managed-output-root "$PUBLIC_TARGET" \
  --configuration PublicClean
python3 "$PUBLIC_CLONE/packaging/public_brand_delta_audit.py" \
  --baseline-report "$PUBLIC_CLONE/packaging/tests/fixtures/public-clean-iossimulator-arm64-baseline.json" \
  --candidate-report "$PUBLIC_REPORT/public-bundle-audit.json" \
  --source-root "$PUBLIC_CLONE" \
  --manifest "$PUBLIC_CLONE/packaging/public-content-manifest.json" \
  --sbom "$PUBLIC_CLONE/docs/legal/public-asset-sbom.json"
test ! -e "$TASK15_STAGE/NUKE HOUR-PublicClean.ipa"
git -C "$PUBLIC_CLONE" diff --exit-code -- \
  ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs
git show :ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs > "$TASK15_STAGE/source-aot.cs"
cmp "$TASK15_STAGE/source-aot.cs" \
  "$PERSONAL_CLONE/ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs"
cmp "$TASK15_STAGE/source-aot.cs" \
  "$PUBLIC_CLONE/ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs"
git diff --cached --binary > "$TASK15_STAGE/candidate-after-build.patch"
cmp "$TASK15_PATCH" "$TASK15_STAGE/candidate-after-build.patch"
```

The commands above are the single authoritative Task 15 build flow. They use default relative `bin/obj`, never an absolute `ArtifactsPath`, test the exact staged candidate in two reconstructed full-engine clones, and prove both clone index manifests, the AOT source, and the cached candidate patch remain byte-identical after the builds. PublicClean must stop only at the pinned historical provenance/runtime gate; no second legacy command block or fabricated PublicClean IPA is permitted.

- [ ] **Step 5: Commit packaging/AOT audit paths**

Recheck that the already staged eight listed packaging/iOS paths that actually changed plus `engine/OpenRA.Test/AotObjectRegistryCoverageTest.cs` (nine paths maximum; omit only an unchanged generated registry, never the test) are byte-identical to the candidate tested in both clones. Show and compare the exact staged list, then commit without restaging unrelated files:

```bash
git commit -m "Audit iOS retail importer artifacts"
```

- [ ] **Step 6: Generate and verify `0026` immediately**

Generate from official+`0001`...`0025` using the exact 20-path engine allowlist:

```bash
PREDECESSOR_END=25
CANDIDATE=engine-patches/0026-ios-retail-active-catalog-lifecycle.patch
python3 packaging/engine_workspace.py allowlist --patch-id 0026 --output "$ALLOWLIST"
cmp "$ALLOWLIST" "$TASK15_ENGINE_ALLOWLIST"
```

Run the exact common block; require all strict gates and a fresh `0001`...`0026` replay byte-equal to the workspace. Parse the actual candidate headers, require them byte-for-byte equal to the provenance manifest's recorded code-only allowlist digest, and repeat the zero bundle-resource-eligible-path assertion. Update README/verifier tests and commit only the three delivery files:

```bash
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

Shell-source tests require `set -euo pipefail`, a fresh shared clone, default relative iOS `bin/obj`, explicit Personal configuration, explicit provision/key variables for device builds, no retail path in arguments/logging, no `set -x`, no recursive delete, no physical-device uninstall, and a final call to the verifier. The script must stop if the working source commit is dirty or differs from the recorded build commit.

- [ ] **Step 2: Run RED**

```bash
python3 -m unittest packaging.tests.test_ios_retail_acceptance_evidence -v
bash -n ios/scripts/run-retail-import-acceptance.sh
```

Expected: missing verifier/script and test import failure.

- [ ] **Step 3: Implement the safe acceptance harness**

The script performs these automated phases:

1. verify the source commit and exact `0019`...`0026` patch set;
2. create a fresh shared clone under `/private/tmp`, then run `packaging/engine_workspace.py materialize --repository <clone> --official-zip "$OPENRA_OFFICIAL_ZIP" --patch-end 26 --workspace <clone>/engine` before restore; this reconstructs the ignored official engine from the pinned ZIP plus strict `0001`...`0026` replay and never copies the caller's ignored engine tree. Use relative output paths and explicitly restore every tested/built project—including `build/OpenRA.RetailImport.CrashHarness`—with the pinned SDK before any `--no-restore` command; build the crash harness and require its process-kill tests to execute rather than skip;
3. run focused/full C#, packaging, YAML, AOT, app, and IPA bundle audits;
4. create an unsigned simulator app and cold-install it only on a disposable simulator;
5. verify the empty-content startup wizard, Files selection presentation, English/Chinese layout, cancel, retry, and zero retail bytes in `.app`;
6. when signing variables and a device are explicitly supplied, build/install without uninstalling or deleting an existing physical-device container;
7. collect only sanitized app log events and invoke the evidence verifier.

The legal retail selection and confirmation of the real skirmish are deliberate device-side manual steps documented in `IOS_RETAIL_IMPORT_ACCEPTANCE.md`; the script never receives or prints a host retail path. A truly cold physical-device acceptance requires a separately authorized device/container reset. Without that authorization it is recorded as not-run, not simulated by overwriting an existing install.

- [ ] **Step 4: Run tooling GREEN and commit the public harness before acceptance**

```bash
python3 -m unittest packaging.tests.test_ios_retail_acceptance_evidence -v
bash -n ios/scripts/run-retail-import-acceptance.sh
git diff --check
git commit -m "Add redacted iOS retail import acceptance gate"
test -z "$(git status --short)"
```

Commit only the five listed tooling/docs paths and show the exact staged list first. This commit intentionally contains no evidence. The harness's dirty-source guard and fresh shared clone now operate on the committed candidate rather than omitting uncommitted scripts. Never commit evidence JSON, screenshots, device logs, fixture paths, provisioning data, or build outputs.

- [ ] **Step 5: Run the complete synthetic/simulator gate**

```bash
python3 -m unittest discover -s packaging/tests -p 'test_*.py' -v
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 -v minimal
./utility.sh --check-yaml
bash ios/scripts/run-retail-import-acceptance.sh \
  --mode simulator \
  --evidence-output /private/tmp/nuke-hour-retail-import-simulator-evidence.json
```

Expected: all source/synthetic/simulator gates pass, the fresh simulator contains no retail data before user selection, and the sanitized evidence verifier passes. Simulator evidence does not claim a legal-retail real-match pass.

- [ ] **Step 6: Run the opt-in legal-retail physical-device gate**

Only when the tester has a legally owned extracted installation available through the device Files picker and has authorized the install/reset scope:

```bash
env IOS_CODESIGN_KEY="$IOS_CODESIGN_KEY" \
  IOS_PROVISION_UUID="$IOS_PROVISION_UUID" \
  IOS_DEVICE_UDID="$IOS_DEVICE_UDID" \
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
git diff --check
test -z "$(git status --short)"
```

Keep all evidence under the explicit `/private/tmp` outputs. If the device step is blocked, retain the already reviewed tooling commit but mark the milestone blocked; do not manufacture a passing record.

---

### Task 17: Final cross-patch verification and next-milestone handoff

**Files:**

- Inspect only: all task allowlists, `engine-patches/0019-*.patch` through `engine-patches/0026-*.patch`
- Create outside Git only: `/private/tmp/nuke-hour-retail-import-final/`

- [ ] **Step 1: Replay the full engine series from the official ZIP**

Run `packaging/verify_engine_patch_delivery.py` separately for every candidate `0019`...`0026` against its immediate predecessor and then replay `0001`...`0026` in one fresh official tree. Require the single fixed 0007 historical diagnostic tuple, no other fuzz/offset, no rejects/orig files, exact headers, byte identity for every tracked engine path, duplicate forward refusal without tree mutation, and reverse dry-run success.

- [ ] **Step 2: Run all source, packaging, and build gates from a clean checkout**

```bash
: "${OPENRA_OFFICIAL_ZIP:?set the pinned official OpenRA ZIP}"
FINAL_SOURCE_REPOSITORY="$PWD"
FINAL_CLONE="$(mktemp -d /private/tmp/nuke-hour-retail-final.XXXXXX)/repository"
git clone --shared --no-hardlinks "$FINAL_SOURCE_REPOSITORY" "$FINAL_CLONE"
python3 "$FINAL_CLONE/packaging/engine_workspace.py" materialize \
  --repository "$FINAL_CLONE" --official-zip "$OPENRA_OFFICIAL_ZIP" \
  --patch-end 26 --workspace "$FINAL_CLONE/engine"
cd "$FINAL_CLONE"
test -z "$(git status --short)"

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
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet test \
  engine/OpenRA.Test/OpenRA.Test.csproj --no-restore -m:1 -v minimal
python3 -m unittest discover -s packaging/tests -p 'test_*.py' -v
./utility.sh --check-yaml
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Game/OpenRA.Game.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  engine/OpenRA.Mods.Common/OpenRA.Mods.Common.csproj -c Release --no-restore -m:1
env DOTNET_ROLL_FORWARD=Major /Users/kevin/.dotnet/dotnet build \
  OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore -m:1
git diff --check
```

Repeat Personal/PublicClean isolated iOS builds and the exact artifact gates from Task 15: Personal app+IPA, PublicClean pre-sign app, and synthetic PublicClean IPA parser/tamper coverage. PublicClean may stop only at the separately pinned provenance/runtime closure gate; importer, brand, AOT, and no-retail audits must pass first. Do not require or fabricate a real PublicClean IPA while that earlier build target still fails, and no test may relabel the historical PublicClean failure report as success.

- [ ] **Step 3: Audit the commit and file boundary**

Require:

- no staged or committed retail extension/data, private fixture, acceptance evidence, credentials, profile, `_CodeSignature`, `artifacts`, `bin`, `obj`, caches, symlink, secret-like name, or file above 50 MB;
- exact task commits and exact patch delivery commits only;
- desktop RA2 startup/importer paths characterized unchanged;
- Personal/PublicClean both use the unconditional pre-game gate;
- every modified engine byte is represented in exactly one of `0019`...`0026` relative to its predecessor;
- no GitHub remote, push, tag, repository creation, or public release action occurred.

- [ ] **Step 4: Record the honest completion state**

Importer implementation is complete only if Task 16's legal-retail physical-device evidence passes. Otherwise report the precise blocked gate while retaining all passing source/synthetic/simulator evidence.

After importer acceptance, create a separate design/plan for the PublicClean runtime-resource closure described by the authoritative spec. Only after that zero-error runtime gate passes may the already-approved iOS About/legal/source plan be implemented. GitHub preparation/tag/push and app publication remain separately authorized future actions.

## Completion criteria

This plan is complete when all of the following are true:

- Base import from extracted files and an extracted folder produces an immutable, receipted active catalog with at least one real Base-compatible two-player map;
- optional Yuri/music/map failures cannot erase or block an independently valid Base generation;
- untrusted archive/map/remote metadata bytes are bounded before existing allocation-heavy parsers;
- retail and downloaded maps are exposed only by verified immutable snapshots, with bounded preview memory and crash-safe generations;
- both iOS configurations run the same recovery/readiness gate and desktop behavior is unchanged;
- in-game content management only creates a next-launch pending import;
- every engine change is strictly replayable as `0019`...`0026`; all importer, branding, AOT, no-retail app, Personal IPA, and synthetic PublicClean IPA-parser audits pass; the real PublicClean IPA plus historical runtime/provenance gate remain explicitly blocked for the next milestone rather than being relabeled as passing;
- private acceptance proves a real skirmish without publishing retail files, paths, hashes, or evidence artifacts;
- no claim is made that PublicClean is playable, source is published, or the app is releasable before the separate remaining gates pass.
