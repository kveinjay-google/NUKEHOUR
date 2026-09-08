#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace OpenRA.Mods.RA2.Content
{
	public sealed record RetailPlannedCandidate
	{
		public RetailSourceId SourceId { get; }
		public string SafeBasename { get; }
		public RetailSourceKind Kind { get; }
		public RetailContentGroup Group { get; }
		public long? Length { get; }

		internal RetailPlannedCandidate(RetailSourceId sourceId, string safeBasename, RetailSourceKind kind,
			RetailContentGroup group, long? length)
		{
			if (sourceId.CanonicalRelativePath == null)
				throw new ArgumentException("A default source ID is not valid.", nameof(sourceId));
			RetailSafeBasename.Validate(safeBasename, nameof(safeBasename));
			if (length < 0)
				throw new ArgumentOutOfRangeException(nameof(length));
			if (!Enum.IsDefined(typeof(RetailSourceKind), kind))
				throw new ArgumentOutOfRangeException(nameof(kind));
			if (!Enum.IsDefined(typeof(RetailContentGroup), group))
				throw new ArgumentOutOfRangeException(nameof(group));
			ValidateClassification(sourceId, safeBasename, group);

			SourceId = sourceId;
			SafeBasename = safeBasename;
			Kind = kind;
			Group = group;
			Length = length;
		}

		internal void Validate() => ValidateClassification(SourceId, SafeBasename, Group);

		static void ValidateClassification(RetailSourceId sourceId, string safeBasename, RetailContentGroup group)
		{
			var sourceBasename = RetailCanonicalPath.Basename(sourceId.CanonicalRelativePath);
			if (!RetailCandidatePlan.TryClassify(sourceBasename, out var canonicalBasename, out var classifiedGroup) ||
				!StringComparer.Ordinal.Equals(canonicalBasename, safeBasename) || classifiedGroup != group)
				throw new ArgumentException("Candidate metadata must match its stable source classification.", nameof(safeBasename));
		}
	}

	public sealed record RetailCandidatePlan
	{
		static readonly IReadOnlyDictionary<string, RetailContentGroup> ArchiveGroups =
			new Dictionary<string, RetailContentGroup>(StringComparer.OrdinalIgnoreCase)
			{
				{ "ra2.mix", RetailContentGroup.Base },
				{ "language.mix", RetailContentGroup.Base },
				{ "theme.mix", RetailContentGroup.Ra2Music },
				{ "ra2md.mix", RetailContentGroup.Yuri },
				{ "langmd.mix", RetailContentGroup.Yuri },
				{ "thememd.mix", RetailContentGroup.YuriMusic },
			};

		static readonly HashSet<string> MapExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".map", ".mpr", ".yrm", ".oramap",
		};

		public ImmutableArray<RetailPlannedCandidate> Candidates { get; }
		public long KnownSourceBytes { get; }
		public bool HasUnknownLengthSource { get; }

		internal RetailCandidatePlan(ImmutableArray<RetailPlannedCandidate> candidates, long knownSourceBytes, bool hasUnknownLengthSource)
		{
			if (candidates.IsDefault)
				throw new ArgumentException("Candidates cannot be a default immutable array.", nameof(candidates));
			if (candidates.IsEmpty)
				throw new ArgumentException("A candidate plan cannot be empty.", nameof(candidates));
			if (knownSourceBytes < 0)
				throw new ArgumentOutOfRangeException(nameof(knownSourceBytes));
			if (candidates.Length > IosImportEnvelopePolicyV1.CandidateFiles)
				throw new ArgumentException("Candidate count exceeds the V1 limit.", nameof(candidates));

			var copy = candidates.ToArray();
			if (copy.Any(candidate => candidate == null))
				throw new ArgumentException("Candidates cannot contain null.", nameof(candidates));
			Array.Sort(copy, (left, right) => left.SourceId.CompareTo(right.SourceId));
			for (var i = 1; i < copy.Length; i++)
				if (copy[i - 1].SourceId == copy[i].SourceId)
					throw new ArgumentException("Candidates must have unique stable source IDs.", nameof(candidates));

			long computedBytes = 0;
			var computedUnknown = false;
			foreach (var candidate in copy)
			{
				candidate.Validate();
				if (candidate.Length.HasValue)
				{
					if (candidate.Length.Value > IosImportEnvelopePolicyV1.SourceBytes)
						throw new ArgumentException("A candidate exceeds the V1 per-source limit.", nameof(candidates));
					computedBytes = checked(computedBytes + candidate.Length.Value);
				}
				else
					computedUnknown = true;
			}

			if (computedBytes > IosImportEnvelopePolicyV1.CandidateBytes)
				throw new ArgumentException("Candidate bytes exceed the V1 aggregate limit.", nameof(candidates));
			if (computedBytes != knownSourceBytes || computedUnknown != hasUnknownLengthSource)
				throw new ArgumentException("Candidate accounting must exactly match the immutable entries.", nameof(candidates));

			Candidates = ImmutableArray.Create(copy);
			KnownSourceBytes = knownSourceBytes;
			HasUnknownLengthSource = hasUnknownLengthSource;
		}

		public bool Equals(RetailCandidatePlan other) => other != null &&
			KnownSourceBytes == other.KnownSourceBytes && HasUnknownLengthSource == other.HasUnknownLengthSource &&
			Candidates.SequenceEqual(other.Candidates);

		public override int GetHashCode()
		{
			HashCode hash = default;
			hash.Add(KnownSourceBytes);
			hash.Add(HasUnknownLengthSource);
			foreach (var candidate in Candidates)
				hash.Add(candidate);
			return hash.ToHashCode();
		}

		public static RetailOperationResult<RetailCandidatePlan> Create(ImmutableArray<RetailSourceEntry> entries)
		{
			if (entries.IsDefault)
				throw new ArgumentException("Entries cannot be a default immutable array.", nameof(entries));

			var unique = new Dictionary<RetailSourceId, RetailSourceEntry>();
			foreach (var entry in entries)
			{
				if (entry == null)
					throw new ArgumentException("Entries cannot contain null.", nameof(entries));
				if (unique.TryGetValue(entry.Id, out var existing))
				{
					if (!SameMetadata(existing, entry))
						return Failed(RetailImportErrorCode.DuplicateConflict, null, entry.Basename, RetailImportDiagnosticCode.None);

					continue;
				}

				unique.Add(entry.Id, entry);
			}

			var candidates = new List<RetailPlannedCandidate>();
			long knownBytes = 0;
			var hasUnknown = false;
			foreach (var entry in unique.Values.OrderBy(item => item.Id))
			{
				if (entry.IsSymbolicLink)
					return Failed(RetailImportErrorCode.SourcePermissionDenied, null, entry.Basename, RetailImportDiagnosticCode.SecurityScopeDenied);

				if (!TryClassify(entry.Basename, out var canonicalBasename, out var group))
				{
					if (entry.Kind == RetailSourceKind.DirectoryDiscoveredFile)
						continue;

					var extension = Extension(entry.Basename);
					var code = StringComparer.OrdinalIgnoreCase.Equals(extension, ".exe") ?
						RetailImportErrorCode.ExecutableSelection : RetailImportErrorCode.ExtractInstallationFirst;
					return Failed(code, null, entry.Basename, RetailImportDiagnosticCode.None);
				}

				if (candidates.Count >= IosImportEnvelopePolicyV1.CandidateFiles)
					return Failed(RetailImportErrorCode.SourceLimitExceeded, group, canonicalBasename, RetailImportDiagnosticCode.None);
				if (entry.Length > IosImportEnvelopePolicyV1.SourceBytes)
					return Failed(RetailImportErrorCode.SourceLimitExceeded, group, canonicalBasename, RetailImportDiagnosticCode.None);

				if (entry.Length.HasValue)
				{
					try
					{
						knownBytes = checked(knownBytes + entry.Length.Value);
					}
					catch (OverflowException)
					{
						return Failed(RetailImportErrorCode.SourceLimitExceeded, group, canonicalBasename, RetailImportDiagnosticCode.ArithmeticOverflow);
					}

					if (knownBytes > IosImportEnvelopePolicyV1.CandidateBytes)
						return Failed(RetailImportErrorCode.SourceLimitExceeded, group, canonicalBasename, RetailImportDiagnosticCode.None);
				}
				else
					hasUnknown = true;

				candidates.Add(new RetailPlannedCandidate(entry.Id, canonicalBasename, entry.Kind, group, entry.Length));
			}

			if (candidates.Count == 0)
				return Failed(RetailImportErrorCode.EmptySelection, null, null, RetailImportDiagnosticCode.None);

			return RetailOperationResult<RetailCandidatePlan>.Success(
				new RetailCandidatePlan(ImmutableArray.CreateRange(candidates), knownBytes, hasUnknown));
		}

		static bool SameMetadata(RetailSourceEntry left, RetailSourceEntry right) =>
			StringComparer.Ordinal.Equals(left.Basename, right.Basename) && left.Kind == right.Kind &&
			left.Length == right.Length && left.IsSymbolicLink == right.IsSymbolicLink;

		internal static bool TryClassify(string basename, out string canonicalBasename, out RetailContentGroup group)
		{
			if (ArchiveGroups.TryGetValue(basename, out group))
			{
				canonicalBasename = ArchiveGroups.Keys.Single(name => StringComparer.OrdinalIgnoreCase.Equals(name, basename));
				return true;
			}

			if (MapExtensions.Contains(Extension(basename)))
			{
				canonicalBasename = basename;
				group = RetailContentGroup.Maps;
				return true;
			}

			canonicalBasename = null;
			group = default;
			return false;
		}

		static string Extension(string basename)
		{
			var index = basename.LastIndexOf('.');
			return index < 0 ? string.Empty : basename.Substring(index);
		}

		static RetailOperationResult<RetailCandidatePlan> Failed(RetailImportErrorCode code,
			RetailContentGroup? group, string basename, RetailImportDiagnosticCode diagnostic) =>
			RetailOperationResult<RetailCandidatePlan>.Failed(new RetailImportFailure(code, group, basename, diagnostic));
	}

	public sealed record StagedRetailSource
	{
		public RetailSourceId SourceId { get; }
		public string SafeBasename { get; }
		public string CanonicalStagedRelativePath { get; }
		public long Length { get; }
		public string Sha256 { get; }

		public StagedRetailSource(RetailSourceId sourceId, string safeBasename, string canonicalStagedRelativePath,
			long length, string sha256)
		{
			if (sourceId.CanonicalRelativePath == null)
				throw new ArgumentException("A default source ID is not valid.", nameof(sourceId));
			RetailSafeBasename.Validate(safeBasename, nameof(safeBasename));
			_ = new RetailStagingTarget(canonicalStagedRelativePath);
			if (length < 0)
				throw new ArgumentOutOfRangeException(nameof(length));
			if (sha256 == null || sha256.Length != 64 || sha256.Any(character => !((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))))
				throw new ArgumentException("SHA-256 must be canonical lowercase hexadecimal.", nameof(sha256));

			SourceId = sourceId;
			SafeBasename = safeBasename;
			CanonicalStagedRelativePath = canonicalStagedRelativePath;
			Length = length;
			Sha256 = sha256;
		}
	}

	public sealed record RetailStagedPlan
	{
		public RetailCandidatePlan CandidatePlan { get; }
		public ImmutableArray<StagedRetailSource> Sources { get; }
		public long TotalBytes { get; }

		RetailStagedPlan(RetailCandidatePlan candidatePlan, ImmutableArray<StagedRetailSource> sources, long totalBytes)
		{
			CandidatePlan = candidatePlan ?? throw new ArgumentNullException(nameof(candidatePlan));
			if (sources.IsDefault)
				throw new ArgumentException("Sources cannot be a default immutable array.", nameof(sources));
			if (totalBytes < 0)
				throw new ArgumentOutOfRangeException(nameof(totalBytes));

			var copy = sources.ToArray();
			if (copy.Any(source => source == null))
				throw new ArgumentException("Sources cannot contain null.", nameof(sources));
			Array.Sort(copy, (left, right) => left.SourceId.CompareTo(right.SourceId));
			long computedTotal = 0;
			foreach (var source in copy)
				computedTotal = checked(computedTotal + source.Length);
			if (computedTotal != totalBytes)
				throw new ArgumentException("Total bytes must exactly match the immutable sources.", nameof(totalBytes));

			Sources = ImmutableArray.Create(copy);
			TotalBytes = totalBytes;
		}

		public bool Equals(RetailStagedPlan other) => other != null && CandidatePlan == other.CandidatePlan &&
			TotalBytes == other.TotalBytes && Sources.SequenceEqual(other.Sources);

		public override int GetHashCode()
		{
			HashCode hash = default;
			hash.Add(CandidatePlan);
			hash.Add(TotalBytes);
			foreach (var source in Sources)
				hash.Add(source);
			return hash.ToHashCode();
		}

		public static RetailOperationResult<RetailStagedPlan> Seal(RetailCandidatePlan plan, ImmutableArray<StagedRetailSource> sources)
		{
			if (plan == null)
				throw new ArgumentNullException(nameof(plan));
			if (sources.IsDefault)
				throw new ArgumentException("Sources cannot be a default immutable array.", nameof(sources));

			var candidates = plan.Candidates.ToDictionary(candidate => candidate.SourceId);
			var supplied = new Dictionary<RetailSourceId, StagedRetailSource>();
			var stagedPaths = new HashSet<string>(StringComparer.Ordinal);
			foreach (var source in sources)
			{
				if (source == null)
					throw new ArgumentException("Sources cannot contain null.", nameof(sources));
				if (!candidates.TryGetValue(source.SourceId, out var candidate) || supplied.ContainsKey(source.SourceId))
					return Failed(RetailImportErrorCode.SourceChanged, null, source.SafeBasename, RetailImportDiagnosticCode.None);
				if (!stagedPaths.Add(source.CanonicalStagedRelativePath))
					return Failed(RetailImportErrorCode.DuplicateConflict, candidate.Group, source.SafeBasename, RetailImportDiagnosticCode.None);
				if (!StringComparer.Ordinal.Equals(candidate.SafeBasename, source.SafeBasename))
					return Failed(RetailImportErrorCode.SourceChanged, candidate.Group, candidate.SafeBasename, RetailImportDiagnosticCode.None);
				if (candidate.Length.HasValue && candidate.Length.Value != source.Length)
					return Failed(RetailImportErrorCode.SourceChanged, candidate.Group, candidate.SafeBasename, RetailImportDiagnosticCode.LengthMismatch);
				if (source.Length > IosImportEnvelopePolicyV1.SourceBytes)
					return Failed(RetailImportErrorCode.SourceLimitExceeded, candidate.Group, candidate.SafeBasename, RetailImportDiagnosticCode.None);

				supplied.Add(source.SourceId, source);
			}

			if (supplied.Count != candidates.Count)
				return Failed(RetailImportErrorCode.SourceChanged, null, null, RetailImportDiagnosticCode.None);

			var retained = new List<StagedRetailSource>();
			var archives = new Dictionary<string, StagedRetailSource>(StringComparer.OrdinalIgnoreCase);
			var mapHashes = new HashSet<string>(StringComparer.Ordinal);
			foreach (var candidate in plan.Candidates)
			{
				var source = supplied[candidate.SourceId];
				if (candidate.Group == RetailContentGroup.Maps)
				{
					if (mapHashes.Add(source.Sha256))
						retained.Add(source);
				}
				else if (archives.TryGetValue(candidate.SafeBasename, out var existing))
				{
					if (!StringComparer.Ordinal.Equals(existing.Sha256, source.Sha256))
						return Failed(RetailImportErrorCode.DuplicateConflict, candidate.Group, candidate.SafeBasename, RetailImportDiagnosticCode.HashMismatch);
				}
				else
				{
					archives.Add(candidate.SafeBasename, source);
					retained.Add(source);
				}
			}

			long totalBytes = 0;
			try
			{
				foreach (var source in retained)
					totalBytes = checked(totalBytes + source.Length);
			}
			catch (OverflowException)
			{
				return Failed(RetailImportErrorCode.SourceLimitExceeded, null, null, RetailImportDiagnosticCode.ArithmeticOverflow);
			}

			if (totalBytes > IosImportEnvelopePolicyV1.CandidateBytes)
				return Failed(RetailImportErrorCode.SourceLimitExceeded, null, null, RetailImportDiagnosticCode.None);

			return RetailOperationResult<RetailStagedPlan>.Success(
				new RetailStagedPlan(plan, ImmutableArray.CreateRange(retained), totalBytes));
		}

		static RetailOperationResult<RetailStagedPlan> Failed(RetailImportErrorCode code,
			RetailContentGroup? group, string basename, RetailImportDiagnosticCode diagnostic) =>
			RetailOperationResult<RetailStagedPlan>.Failed(new RetailImportFailure(code, group, basename, diagnostic));
	}
}
