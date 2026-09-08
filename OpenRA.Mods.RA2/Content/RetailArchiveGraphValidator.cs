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
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using OpenRA.FileSystem;
using OpenRA.Mods.Cnc.FileFormats;
using OpenRA.Mods.Cnc.FileSystem;

namespace OpenRA.Mods.RA2.Content
{
	static class RetailOwnershipCleanup
	{
		public static void Dispose(IDisposable value)
		{
			try
			{
				value?.Dispose();
			}
			catch
			{
				// Ownership cleanup is total and never replaces the primary result.
			}
		}

		public static void DisposeAll<T>(IEnumerable<T> values) where T : IDisposable
		{
			foreach (var value in values)
				Dispose(value);
		}
	}

	internal sealed record RetailArchiveInputDescriptor
	{
		static readonly ImmutableDictionary<string, RetailContentGroup> Roots =
			new Dictionary<string, RetailContentGroup>(StringComparer.Ordinal)
			{
				{ "ra2.mix", RetailContentGroup.Base },
				{ "language.mix", RetailContentGroup.Base },
				{ "theme.mix", RetailContentGroup.Ra2Music },
				{ "ra2md.mix", RetailContentGroup.Yuri },
				{ "langmd.mix", RetailContentGroup.Yuri },
				{ "thememd.mix", RetailContentGroup.YuriMusic },
			}.ToImmutableDictionary(StringComparer.Ordinal);

		public string CanonicalLogicalName { get; }
		public RetailContentGroup Group { get; }
		public long Length { get; }
		public string Sha256 { get; }

		internal RetailArchiveInputDescriptor(
			string canonicalLogicalName,
			RetailContentGroup group,
			long length,
			string sha256)
		{
			if (!Roots.TryGetValue(canonicalLogicalName ?? string.Empty, out var expectedGroup) ||
				expectedGroup != group)
				throw new ArgumentException("The archive root name and content group do not match.", nameof(canonicalLogicalName));
			if (length < 0 || length > IosImportEnvelopePolicyV1.SourceBytes)
				throw new ArgumentOutOfRangeException(nameof(length));
			if (sha256 == null || sha256.Length != 64 ||
				sha256.Any(character => !((character >= '0' && character <= '9') ||
					(character >= 'a' && character <= 'f'))))
				throw new ArgumentException("The source digest must be canonical lowercase SHA-256.", nameof(sha256));

			CanonicalLogicalName = canonicalLogicalName;
			Group = group;
			Length = length;
			Sha256 = sha256;
		}
	}

	internal sealed class RetailOwnedArchiveInput : IDisposable
	{
		Stream stream;
		public RetailArchiveInputDescriptor Descriptor { get; }

		RetailOwnedArchiveInput(RetailArchiveInputDescriptor descriptor, Stream ownedPinnedStream)
		{
			Descriptor = descriptor;
			stream = ownedPinnedStream;
		}

		internal static RetailOwnedArchiveInput TakeOwnership(
			RetailArchiveInputDescriptor descriptor,
			Stream ownedPinnedStream)
		{
			if (ownedPinnedStream == null)
				throw new ArgumentNullException(nameof(ownedPinnedStream));

			try
			{
				if (descriptor == null)
					throw new ArgumentNullException(nameof(descriptor));
				if (!ownedPinnedStream.CanRead || !ownedPinnedStream.CanSeek)
					throw new ArgumentException("The owned archive stream must be readable and seekable.", nameof(ownedPinnedStream));
				if (ownedPinnedStream.Position != 0 || ownedPinnedStream.Length != descriptor.Length)
					throw new ArgumentException("The owned archive stream does not match its descriptor.", nameof(ownedPinnedStream));
				return new RetailOwnedArchiveInput(descriptor, ownedPinnedStream);
			}
			catch
			{
				RetailOwnershipCleanup.Dispose(ownedPinnedStream);
				throw;
			}
		}

		internal Stream DetachOwnedPinnedStream()
		{
			var detached = Interlocked.Exchange(ref stream, null);
			return detached ?? throw new InvalidOperationException("The archive input ownership was already detached.");
		}

		public void Dispose()
		{
			RetailOwnershipCleanup.Dispose(Interlocked.Exchange(ref stream, null));
			GC.SuppressFinalize(this);
		}
	}

	internal sealed class RetailArchiveInputBatch : IDisposable
	{
		readonly object sync = new();
		ImmutableArray<RetailOwnedArchiveInput> inputs;
		bool detached;

		internal ImmutableArray<RetailOwnedArchiveInput> Inputs
		{
			get
			{
				lock (sync)
					return inputs;
			}
		}

		RetailArchiveInputBatch(ImmutableArray<RetailOwnedArchiveInput> ownedInputs)
		{
			inputs = ownedInputs;
		}

		internal static RetailArchiveInputBatch TakeOwnership(
			ImmutableArray<RetailOwnedArchiveInput> ownedInputs)
		{
			IEnumerable<RetailOwnedArchiveInput> toDispose = ownedInputs.IsDefault ?
				Array.Empty<RetailOwnedArchiveInput>() : ownedInputs;
			try
			{
				if (ownedInputs.IsDefault)
					throw new ArgumentException("A default archive input array is invalid.", nameof(ownedInputs));

				var identities = new HashSet<RetailOwnedArchiveInput>(ReferenceComparer.Instance);
				var names = new HashSet<string>(StringComparer.Ordinal);
				foreach (var input in ownedInputs)
				{
					if (input == null)
						throw new ArgumentException("The archive input array contains null.", nameof(ownedInputs));
					if (!identities.Add(input))
						throw new ArgumentException("The same owned archive input was transferred twice.", nameof(ownedInputs));
					if (!names.Add(input.Descriptor.CanonicalLogicalName))
						throw new ArgumentException("The archive input batch contains a duplicate root name.", nameof(ownedInputs));
				}

				var ordered = ownedInputs.OrderBy(input => input.Descriptor.Group)
					.ThenBy(input => input.Descriptor.CanonicalLogicalName, StringComparer.Ordinal)
					.ToImmutableArray();
				return new RetailArchiveInputBatch(ordered);
			}
			catch
			{
				RetailOwnershipCleanup.DisposeAll(
					toDispose.Where(input => input != null).Distinct(ReferenceComparer.Instance));
				throw;
			}
		}

		internal ImmutableArray<RetailOwnedArchiveInput> DetachOwnedInputs()
		{
			lock (sync)
			{
				if (detached)
					throw new InvalidOperationException("The archive input batch ownership was already consumed.");
				detached = true;
				var result = inputs;
				inputs = ImmutableArray<RetailOwnedArchiveInput>.Empty;
				return result;
			}
		}

		public void Dispose()
		{
			ImmutableArray<RetailOwnedArchiveInput> close;
			lock (sync)
			{
				close = inputs;
				inputs = ImmutableArray<RetailOwnedArchiveInput>.Empty;
				detached = true;
			}

			RetailOwnershipCleanup.DisposeAll(close);

			GC.SuppressFinalize(this);
		}

		sealed class ReferenceComparer : IEqualityComparer<RetailOwnedArchiveInput>
		{
			public static ReferenceComparer Instance { get; } = new();
			public bool Equals(RetailOwnedArchiveInput x, RetailOwnedArchiveInput y) => ReferenceEquals(x, y);
			public int GetHashCode(RetailOwnedArchiveInput obj) => RuntimeHelpers.GetHashCode(obj);
		}
	}

	public sealed class ValidatedEmbeddedMapSource : IRa2MapSourceHandle
	{
		ValidatedMixEntrySource source;
		public string SafeName { get; }
		public long Length { get; }
		public string SourceSha256 { get; }

		internal ValidatedEmbeddedMapSource(
			string safeName,
			long length,
			string sourceSha256,
			ValidatedMixEntrySource ownedSource)
		{
			if (ownedSource == null)
				throw new ArgumentNullException(nameof(ownedSource));
			try
			{
				RetailSafeBasename.Validate(safeName, nameof(safeName));
				if (length < 0 || length != ownedSource.Length)
					throw new ArgumentOutOfRangeException(nameof(length));
				if (sourceSha256 == null || sourceSha256.Length != 64 ||
					sourceSha256.Any(character => !((character >= '0' && character <= '9') ||
						(character >= 'a' && character <= 'f'))))
					throw new ArgumentException("The map source digest is invalid.", nameof(sourceSha256));

				SafeName = safeName;
				Length = length;
				SourceSha256 = sourceSha256;
				source = ownedSource;
			}
			catch
			{
				RetailOwnershipCleanup.Dispose(ownedSource);
				throw;
			}
		}

		public Stream OpenRead()
		{
			var current = Volatile.Read(ref source) ??
				throw new ObjectDisposedException(nameof(ValidatedEmbeddedMapSource));
			return current.OpenRead();
		}

		public Stream OpenVerifiedRead() => OpenRead();

		public void Dispose()
		{
			RetailOwnershipCleanup.Dispose(Interlocked.Exchange(ref source, null));
			GC.SuppressFinalize(this);
		}
	}

	public sealed class RetailArchiveGraphResult : IDisposable
	{
		ImmutableArray<ValidatedEmbeddedMapSource> ownedEmbeddedMaps;
		public ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> Groups { get; }
		public ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure> GroupFailures { get; }
		public ImmutableArray<ValidatedEmbeddedMapSource> EmbeddedMaps => ownedEmbeddedMaps;

		internal RetailArchiveGraphResult(
			ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> groups,
			ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure> groupFailures,
			ImmutableArray<ValidatedEmbeddedMapSource> ownedEmbeddedMaps)
		{
			try
			{
				var expectedGroups = Enum.GetValues<RetailContentGroup>();
				if (groups == null || groups.Count != expectedGroups.Length ||
					!groups.Keys.SequenceEqual(expectedGroups.OrderBy(group => group)))
					throw new ArgumentException("The graph result must contain exactly the five content groups.", nameof(groups));
				if (groups.Any(pair => pair.Value == null || !IsAllowedStatus(pair.Key, pair.Value) ||
					pair.Value.Recognition != RetailEditionRecognition.NotApplicable ||
					(pair.Value.Validation != RetailGroupValidation.Invalid &&
						pair.Value.Diagnostic != RetailImportDiagnosticCode.None)))
					throw new ArgumentException("Task 2 group states must use the closed structural status matrix.", nameof(groups));
				if (groupFailures == null)
					throw new ArgumentNullException(nameof(groupFailures));
				var invalid = groups.Where(pair => pair.Value.Validation == RetailGroupValidation.Invalid)
					.Select(pair => pair.Key).ToArray();
				if (!groupFailures.Keys.SequenceEqual(invalid) || groupFailures.Any(pair =>
					pair.Value == null || pair.Value.Group != pair.Key ||
					groups[pair.Key].Diagnostic != pair.Value.Diagnostic || !IsAllowedFailure(pair.Key, pair.Value)))
					throw new ArgumentException("Invalid groups and group failures must have an exact one-to-one mapping.",
						nameof(groupFailures));
				if (ownedEmbeddedMaps.IsDefault || ownedEmbeddedMaps.Any(source => source == null))
					throw new ArgumentException("The embedded map array is invalid.", nameof(ownedEmbeddedMaps));
				var identities = new HashSet<ValidatedEmbeddedMapSource>(MapSourceReferenceComparer.Instance);
				foreach (var source in ownedEmbeddedMaps)
					if (!identities.Add(source))
						throw new ArgumentException(
							"The same embedded map handle was transferred more than once.", nameof(ownedEmbeddedMaps));

				var ordered = ownedEmbeddedMaps.OrderBy(source => source.SafeName, StringComparer.Ordinal)
					.ThenBy(source => source.SourceSha256, StringComparer.Ordinal).ToArray();
				var retained = ImmutableArray.CreateBuilder<ValidatedEmbeddedMapSource>();
				var byDigest = new Dictionary<string, ValidatedEmbeddedMapSource>(StringComparer.Ordinal);
				foreach (var source in ordered)
				{
					if (byDigest.TryGetValue(source.SourceSha256, out var existing))
					{
						if (existing.Length != source.Length)
							throw new InvalidDataException("Equal embedded-map digests have inconsistent lengths.");
						source.Dispose();
						continue;
					}

					byDigest.Add(source.SourceSha256, source);
					retained.Add(source);
				}

				Groups = groups;
				GroupFailures = groupFailures;
				this.ownedEmbeddedMaps = retained.ToImmutable();
			}
			catch
			{
				if (!ownedEmbeddedMaps.IsDefault)
					RetailOwnershipCleanup.DisposeAll(ownedEmbeddedMaps.Where(source => source != null)
						.Distinct(MapSourceReferenceComparer.Instance));
				throw;
			}
		}

		sealed class MapSourceReferenceComparer : IEqualityComparer<ValidatedEmbeddedMapSource>
		{
			public static MapSourceReferenceComparer Instance { get; } = new();

			MapSourceReferenceComparer() { }

			public bool Equals(ValidatedEmbeddedMapSource x, ValidatedEmbeddedMapSource y) => ReferenceEquals(x, y);
			public int GetHashCode(ValidatedEmbeddedMapSource obj) => RuntimeHelpers.GetHashCode(obj);
		}

		static bool IsAllowedStatus(RetailContentGroup group, RetailGroupStatus status)
		{
			return group switch
			{
				RetailContentGroup.Base or RetailContentGroup.Yuri =>
					status.Validation is RetailGroupValidation.Missing or RetailGroupValidation.Invalid or
						RetailGroupValidation.Ready,
				RetailContentGroup.Ra2Music or RetailContentGroup.YuriMusic =>
					status.Validation is RetailGroupValidation.Missing or RetailGroupValidation.Invalid or
						RetailGroupValidation.Unavailable,
				RetailContentGroup.Maps =>
					status.Validation is RetailGroupValidation.Missing or RetailGroupValidation.Unavailable,
				_ => false,
			};
		}

		static bool IsAllowedFailure(RetailContentGroup group, RetailImportFailure failure)
		{
			if (group == RetailContentGroup.Maps)
				return false;
			if (failure.Code == RetailImportErrorCode.CorruptArchive)
				return (failure.Diagnostic == RetailImportDiagnosticCode.MalformedEnvelope ||
					failure.Diagnostic == RetailImportDiagnosticCode.ArithmeticOverflow) &&
					(group switch
					{
						RetailContentGroup.Base => failure.SafeBasename is "ra2.mix" or "language.mix",
						RetailContentGroup.Ra2Music => failure.SafeBasename == "theme.mix",
						RetailContentGroup.Yuri => failure.SafeBasename is "ra2md.mix" or "langmd.mix",
						RetailContentGroup.YuriMusic => failure.SafeBasename == "thememd.mix",
						_ => false,
					});
			if (group == RetailContentGroup.Base && failure.Code == RetailImportErrorCode.IncompleteBase)
				return failure.Diagnostic == RetailImportDiagnosticCode.MissingRequiredArchive &&
					failure.SafeBasename is "ra2.mix" or "language.mix";
			if (group == RetailContentGroup.Base && failure.Code == RetailImportErrorCode.MissingNestedBasePackage)
				return failure.Diagnostic == RetailImportDiagnosticCode.MissingRequiredArchive &&
					failure.SafeBasename == "language.mix";
			if (group == RetailContentGroup.Yuri && failure.Code == RetailImportErrorCode.PartialYuri)
				return failure.Diagnostic == RetailImportDiagnosticCode.MissingRequiredArchive &&
					failure.SafeBasename is "ra2md.mix" or "langmd.mix";
			if ((group == RetailContentGroup.Base || group == RetailContentGroup.Yuri) &&
				failure.Code == RetailImportErrorCode.InvalidAudio)
				return failure.SafeBasename == (group == RetailContentGroup.Base ? "language.mix" : "langmd.mix") &&
					failure.Diagnostic is RetailImportDiagnosticCode.MissingRequiredArchive or
						RetailImportDiagnosticCode.MalformedEnvelope or RetailImportDiagnosticCode.ArithmeticOverflow;
			return false;
		}

		public void Dispose()
		{
			var close = ImmutableInterlocked.InterlockedExchange(
				ref ownedEmbeddedMaps, ImmutableArray<ValidatedEmbeddedMapSource>.Empty);
			if (!close.IsDefault)
				RetailOwnershipCleanup.DisposeAll(close);
			GC.SuppressFinalize(this);
		}
	}

	public sealed class RetailArchiveGraphValidator
	{
		static readonly ImmutableHashSet<string> BasePackages = ImmutableHashSet.Create(StringComparer.Ordinal,
			"cache.mix", "cameo.mix", "conquer.mix", "generic.mix", "isogen.mix", "isosnow.mix",
			"isotemp.mix", "isourb.mix", "load.mix", "local.mix", "neutral.mix", "sidec01.mix",
			"sidec02.mix", "sno.mix", "snow.mix", "tem.mix", "temperat.mix", "urb.mix", "urban.mix");
		static readonly ImmutableHashSet<string> LangMdPackages = ImmutableHashSet.Create(StringComparer.Ordinal,
			"audiomd.mix", "cameomd.mix");
		static readonly ImmutableHashSet<string> Ra2MdPackages = ImmutableHashSet.Create(StringComparer.Ordinal,
			"cachemd.mix", "conqmd.mix", "des.mix", "desert.mix", "genermd.mix", "isodesmd.mix",
			"isodes.mix", "isogenmd.mix", "isolunmd.mix", "isolun.mix", "isosnomd.mix", "isotemmd.mix",
			"isoubnmd.mix", "isoubn.mix", "isourbmd.mix", "loadmd.mix", "localmd.mix", "lun.mix",
			"lunar.mix", "ntrlmd.mix", "snowmd.mix", "ubn.mix", "urbann.mix");
		static readonly ImmutableHashSet<string> MapSuffixes = ImmutableHashSet.Create(StringComparer.Ordinal,
			".map", ".mpr", ".yrm", ".oramap");

		readonly PackageEnvelopePolicy policy;
		readonly IRetailMonotonicClock clock;
		readonly RetailFilenameCatalog filenames;
		readonly OpenRA.FileSystem.FileSystem fileSystem;

		internal static PackageEnvelopePolicy IosV1Policy { get; } = new(
			new ZipEnvelopeLimits(IosImportEnvelopePolicyV1.PackagePathUtf8Bytes,
				IosImportEnvelopePolicyV1.PackagePathDepth, IosImportEnvelopePolicyV1.OramapEntries,
				IosImportEnvelopePolicyV1.OramapCompressedBytes, IosImportEnvelopePolicyV1.OramapExpandedEntryBytes,
				IosImportEnvelopePolicyV1.OramapExpandedAggregateBytes, IosImportEnvelopePolicyV1.OramapExpansionRatio),
			new PngReadLimits(IosImportEnvelopePolicyV1.MapPngEdge, IosImportEnvelopePolicyV1.MapPngPixels,
				IosImportEnvelopePolicyV1.MapPngDecodedBytes, IosImportEnvelopePolicyV1.MapPngChunks,
				IosImportEnvelopePolicyV1.MapPngIdatBytes, IosImportEnvelopePolicyV1.MapPngInflateRatio),
			new YamlEnvelopeLimits(IosImportEnvelopePolicyV1.MapYamlBytes, IosImportEnvelopePolicyV1.MapYamlNodes,
				IosImportEnvelopePolicyV1.MapYamlDepth, IosImportEnvelopePolicyV1.MapYamlLineBytes,
				IosImportEnvelopePolicyV1.MapYamlScalarBytes),
			new MixEnvelopeLimits(IosImportEnvelopePolicyV1.MixDepth,
				IosImportEnvelopePolicyV1.MixEntriesPerArchive, IosImportEnvelopePolicyV1.MixEntriesAggregate,
				IosImportEnvelopePolicyV1.MixIndexBytes, IosImportEnvelopePolicyV1.MixLocalDatabaseBytes,
				IosImportEnvelopePolicyV1.MixLocalDatabaseNames, IosImportEnvelopePolicyV1.MixSegmentBytes,
				IosImportEnvelopePolicyV1.MixAddressedBytes),
			new IdxBagEnvelopeLimits(IosImportEnvelopePolicyV1.IdxBytes,
				IosImportEnvelopePolicyV1.IdxSoundRecords, IosImportEnvelopePolicyV1.IdxNameBytes,
				IosImportEnvelopePolicyV1.BagBytes, IosImportEnvelopePolicyV1.BagSoundSpanBytes));

		public RetailArchiveGraphValidator(
			PackageEnvelopePolicy policy,
			IRetailMonotonicClock clock,
			RetailFilenameCatalog filenames)
		{
			this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
			this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
			this.filenames = filenames ?? throw new ArgumentNullException(nameof(filenames));
			fileSystem = new OpenRA.FileSystem.FileSystem("ra2-retail-archive-validator",
				ImmutableDictionary<string, Manifest>.Empty,
				new IPackageLoader[] { new MixLoader(PackageHashType.CRC32, filenames) });
		}

		internal RetailOperationResult<RetailArchiveGraphResult> ValidateOwned(
			RetailArchiveInputBatch ownedInputs,
			RetailValidationDeadline alreadyStartedWholeValidationDeadline,
			CancellationToken cancellationToken)
		{
			if (ownedInputs == null)
				throw new ArgumentNullException(nameof(ownedInputs));
			if (!alreadyStartedWholeValidationDeadline.MatchesClock(clock))
			{
				ownedInputs.Dispose();
				return Failed(
					RetailImportErrorCode.ProfileContextFailure,
					null,
					null,
					RetailImportDiagnosticCode.ProfileMetadataMismatch);
			}

			var inputs = ownedInputs.DetachOwnedInputs();
			ownedInputs.Dispose();
			var roots = new Dictionary<string, OwnedRoot>(StringComparer.Ordinal);
			var maps = new List<ValidatedEmbeddedMapSource>();
			RetailContentGroup? currentGroup = null;
			string currentRoot = null;
			try
			{
				if (alreadyStartedWholeValidationDeadline == default)
					throw new ArgumentException("The whole-validation deadline must already be started.",
						nameof(alreadyStartedWholeValidationDeadline));
				foreach (var input in inputs)
					roots.Add(input.Descriptor.CanonicalLogicalName,
						new OwnedRoot(input.Descriptor, input.DetachOwnedPinnedStream()));

				var deadline = new ArchiveDeadline(clock, alreadyStartedWholeValidationDeadline);
				var context = new PackageReadContext(cancellationToken, deadline, policy.Mix.MaximumDepth,
					policy.Mix.MaximumEntriesAggregate, policy.Mix.MaximumAddressedBytes, policy);
				var groups = ImmutableSortedDictionary.CreateBuilder<RetailContentGroup, RetailGroupStatus>();
				var failures = ImmutableSortedDictionary.CreateBuilder<RetailContentGroup, RetailImportFailure>();

				ProcessBase(roots, context, deadline, maps, groups, failures, ref currentGroup, ref currentRoot);
				ProcessSingleton(RetailContentGroup.Ra2Music, "theme.mix", RetailGroupValidation.Unavailable,
					roots, context, deadline, maps, groups, failures, ref currentGroup, ref currentRoot);
				ProcessYuri(roots, context, deadline, maps, groups, failures, ref currentGroup, ref currentRoot);
				ProcessSingleton(RetailContentGroup.YuriMusic, "thememd.mix", RetailGroupValidation.Unavailable,
					roots, context, deadline, maps, groups, failures, ref currentGroup, ref currentRoot);
				groups.Add(RetailContentGroup.Maps, Status(
					maps.Count == 0 ? RetailGroupValidation.Missing : RetailGroupValidation.Unavailable));

				var result = new RetailArchiveGraphResult(groups.ToImmutable(), failures.ToImmutable(), maps.ToImmutableArray());
				maps.Clear();
				return RetailOperationResult<RetailArchiveGraphResult>.Success(result);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (TimeoutException)
			{
				return Failed(RetailImportErrorCode.ValidationTimedOut, currentGroup, currentRoot,
					RetailImportDiagnosticCode.DeadlineExceeded);
			}
			catch (FatalGraphException ex)
			{
				return RetailOperationResult<RetailArchiveGraphResult>.Failed(ex.Failure);
			}
			catch (PackageEnvelopeException ex) when (ex.Failure == PackageEnvelopeFailure.AggregateLimitExceeded)
			{
				return Failed(RetailImportErrorCode.SourceLimitExceeded, currentGroup, currentRoot,
					RetailImportDiagnosticCode.None);
			}
			catch (PackageEnvelopeException ex)
			{
				return Failed(RetailImportErrorCode.SanitizedIoFailure, currentGroup, currentRoot,
					ex.Failure == PackageEnvelopeFailure.ArithmeticOverflow ?
						RetailImportDiagnosticCode.ArithmeticOverflow : RetailImportDiagnosticCode.None);
			}
			catch (OverflowException)
			{
				return Failed(RetailImportErrorCode.SanitizedIoFailure, currentGroup, currentRoot,
					RetailImportDiagnosticCode.ArithmeticOverflow);
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
				ex is OperationCanceledException)
			{
				return Failed(RetailImportErrorCode.SanitizedIoFailure, currentGroup, currentRoot,
					RetailImportDiagnosticCode.None);
			}
			finally
			{
				RetailOwnershipCleanup.DisposeAll(inputs);
				RetailOwnershipCleanup.DisposeAll(roots.Values);
				RetailOwnershipCleanup.DisposeAll(maps);
			}
		}

		void ProcessBase(Dictionary<string, OwnedRoot> roots, PackageReadContext context, ArchiveDeadline deadline,
			List<ValidatedEmbeddedMapSource> maps,
			ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus>.Builder groups,
			ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure>.Builder failures,
			ref RetailContentGroup? currentGroup, ref string currentRoot)
		{
			const RetailContentGroup group = RetailContentGroup.Base;
			var hasLanguage = roots.ContainsKey("language.mix");
			var hasRa2 = roots.ContainsKey("ra2.mix");
			if (!hasLanguage && !hasRa2)
			{
				groups.Add(group, Status(RetailGroupValidation.Missing));
				return;
			}

			if (hasLanguage != hasRa2)
			{
				var missing = hasLanguage ? "ra2.mix" : "language.mix";
				AddInvalid(group, RetailImportErrorCode.IncompleteBase, missing,
					RetailImportDiagnosticCode.MissingRequiredArchive, groups, failures);
				return;
			}

			var firstMap = maps.Count;
			try
			{
				currentGroup = group;
				currentRoot = "ra2.mix";
				using (var ra2 = ValidateRoot(roots["ra2.mix"], context, deadline))
				{
					deadline.Select("ra2.mix");
					ProcessContainer(ra2.Package, "ra2.mix", context, group, "ra2.mix", maps);
				}

				currentRoot = "language.mix";
				using (var language = ValidateRoot(roots["language.mix"], context, deadline))
				{
					deadline.Select("language.mix");
					ProcessContainer(language.Package, "language.mix", context, group, "language.mix", maps);
				}

				groups.Add(group, Status(RetailGroupValidation.Ready));
			}
			catch (GroupGraphException ex)
			{
				DisposeMapsFrom(maps, firstMap);
				AddInvalid(group, ex.Failure.Code, ex.Failure.SafeBasename, ex.Failure.Diagnostic, groups, failures);
			}
			finally
			{
				roots["language.mix"].Dispose();
				roots["ra2.mix"].Dispose();
			}
		}

		void ProcessYuri(Dictionary<string, OwnedRoot> roots, PackageReadContext context, ArchiveDeadline deadline,
			List<ValidatedEmbeddedMapSource> maps,
			ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus>.Builder groups,
			ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure>.Builder failures,
			ref RetailContentGroup? currentGroup, ref string currentRoot)
		{
			const RetailContentGroup group = RetailContentGroup.Yuri;
			var hasLangMd = roots.ContainsKey("langmd.mix");
			var hasRa2Md = roots.ContainsKey("ra2md.mix");
			if (!hasLangMd && !hasRa2Md)
			{
				groups.Add(group, Status(RetailGroupValidation.Missing));
				return;
			}

			if (hasLangMd != hasRa2Md)
			{
				var missing = hasLangMd ? "ra2md.mix" : "langmd.mix";
				AddInvalid(group, RetailImportErrorCode.PartialYuri, missing,
					RetailImportDiagnosticCode.MissingRequiredArchive, groups, failures);
				return;
			}

			var firstMap = maps.Count;
			try
			{
				currentGroup = group;
				currentRoot = "ra2md.mix";
				using (var ra2md = ValidateRoot(roots["ra2md.mix"], context, deadline))
				{
					deadline.Select("ra2md.mix");
					ProcessContainer(ra2md.Package, "ra2md.mix", context, group, "ra2md.mix", maps);
				}

				currentRoot = "langmd.mix";
				using (var langmd = ValidateRoot(roots["langmd.mix"], context, deadline))
				{
					deadline.Select("langmd.mix");
					ProcessContainer(langmd.Package, "langmd.mix", context, group, "langmd.mix", maps);
				}

				groups.Add(group, Status(RetailGroupValidation.Ready));
			}
			catch (GroupGraphException ex)
			{
				DisposeMapsFrom(maps, firstMap);
				AddInvalid(group, ex.Failure.Code, ex.Failure.SafeBasename, ex.Failure.Diagnostic, groups, failures);
			}
			finally
			{
				roots["langmd.mix"].Dispose();
				roots["ra2md.mix"].Dispose();
			}
		}

		void ProcessSingleton(RetailContentGroup group, string rootName, RetailGroupValidation validState,
			Dictionary<string, OwnedRoot> roots, PackageReadContext context, ArchiveDeadline deadline,
			List<ValidatedEmbeddedMapSource> maps,
			ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus>.Builder groups,
			ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure>.Builder failures,
			ref RetailContentGroup? currentGroup, ref string currentRoot)
		{
			if (!roots.TryGetValue(rootName, out var root))
			{
				groups.Add(group, Status(RetailGroupValidation.Missing));
				return;
			}

			var firstMap = maps.Count;
			try
			{
				currentGroup = group;
				currentRoot = rootName;
				using var archive = ValidateRoot(root, context, deadline);
				ProcessContainer(archive.Package, rootName, context, group, rootName, maps);
				groups.Add(group, Status(validState));
			}
			catch (GroupGraphException ex)
			{
				DisposeMapsFrom(maps, firstMap);
				AddInvalid(group, ex.Failure.Code, ex.Failure.SafeBasename, ex.Failure.Diagnostic, groups, failures);
			}
			finally
			{
				root.Dispose();
			}
		}

		ValidatedRoot ValidateRoot(OwnedRoot root, PackageReadContext context, ArchiveDeadline deadline)
		{
			deadline.Begin(root.Descriptor.CanonicalLogicalName);
			var stream = root.Detach();
			try
			{
				string digest;
				try
				{
					digest = Hash(stream, root.Descriptor.Length, context);
				}
				catch (Exception ex) when (ex is EndOfStreamException || ex is InvalidDataException)
				{
					throw new FatalGraphException(new RetailImportFailure(RetailImportErrorCode.SourceChanged,
						root.Descriptor.Group, root.Descriptor.CanonicalLogicalName,
						RetailImportDiagnosticCode.LengthMismatch));
				}

				if (!StringComparer.Ordinal.Equals(digest, root.Descriptor.Sha256))
					throw new FatalGraphException(new RetailImportFailure(RetailImportErrorCode.SourceChanged,
						root.Descriptor.Group, root.Descriptor.CanonicalLogicalName, RetailImportDiagnosticCode.HashMismatch));
				stream.Position = 0;
				IReadOnlyPackage parsed = null;
				try
				{
					var recognized = fileSystem.TryParsePackage(
						stream, root.Descriptor.CanonicalLogicalName, context, out parsed);
					if (recognized)
						stream = null;
					if (!recognized ||
						parsed is not IValidatedMixPackage validated)
					{
						parsed?.Dispose();
						throw GroupFailure(RetailImportErrorCode.CorruptArchive, root.Descriptor.Group,
							root.Descriptor.CanonicalLogicalName, RetailImportDiagnosticCode.MalformedEnvelope);
					}

					return new ValidatedRoot(validated);
				}
				catch (PackageEnvelopeException ex) when (ex.Failure != PackageEnvelopeFailure.AggregateLimitExceeded)
				{
					throw GroupFailure(RetailImportErrorCode.CorruptArchive, root.Descriptor.Group,
						root.Descriptor.CanonicalLogicalName,
						ex.Failure == PackageEnvelopeFailure.ArithmeticOverflow ?
							RetailImportDiagnosticCode.ArithmeticOverflow : RetailImportDiagnosticCode.MalformedEnvelope);
				}
			}
			finally
			{
				RetailOwnershipCleanup.Dispose(stream);
			}
		}

		void ProcessContainer(IValidatedMixPackage index, string role, PackageReadContext context,
			RetailContentGroup group, string rootName, List<ValidatedEmbeddedMapSource> maps)
		{
			try
			{
				var allowed = role switch
				{
					"ra2.mix" => BasePackages,
					"language.mix" => ImmutableHashSet.Create(StringComparer.Ordinal, "audio.mix", "cameo.mix"),
					"langmd.mix" => LangMdPackages,
					"ra2md.mix" => Ra2MdPackages,
					_ => ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal),
				};

				var resolvedEntries = index.Entries.Where(entry => entry.CanonicalName != null).ToArray();
				foreach (var entry in resolvedEntries)
				{
					var name = entry.CanonicalName;
					if (name == "local mix database.dat")
						continue;
					if (name == "audio.idx" || name == "audio.bag")
					{
						if (role != "audio.mix" && role != "audiomd.mix")
							throw Corrupt(group, rootName);
						continue;
					}

					if (!name.EndsWith(".mix", StringComparison.Ordinal))
						continue;
					if (!allowed.Contains(name))
						throw Corrupt(group, rootName);
				}

				var overlay = ReadMapOverlay(index, context);
				foreach (var pair in overlay.OrderBy(pair => pair.Value, StringComparer.Ordinal))
					maps.Add(CreateMap(index, pair.Key, pair.Value, context, group, rootName));

				if (role == "language.mix" && !Contains(index, "audio.mix"))
					throw GroupFailure(RetailImportErrorCode.MissingNestedBasePackage, group, "language.mix",
						RetailImportDiagnosticCode.MissingRequiredArchive);

				var nested = resolvedEntries.Where(entry => entry.CanonicalName.EndsWith(".mix", StringComparison.Ordinal))
					.OrderBy(entry => entry.CanonicalName is "audio.mix" or "audiomd.mix" ? 1 : 0)
					.ThenBy(entry => entry.CanonicalName, StringComparer.Ordinal);
				foreach (var entry in nested)
				{
					var name = entry.CanonicalName;
					IReadOnlyPackage opened = null;
					try
					{
						opened = index.OpenPackage(name, fileSystem);
						if (opened is not IValidatedMixPackage)
						{
							opened?.Dispose();
							throw Corrupt(group, rootName);
						}
					}
					catch (PackageEnvelopeException ex) when (ex.Failure != PackageEnvelopeFailure.AggregateLimitExceeded)
					{
						throw GroupFailure(RetailImportErrorCode.CorruptArchive, group, rootName,
							ex.Failure == PackageEnvelopeFailure.ArithmeticOverflow ?
								RetailImportDiagnosticCode.ArithmeticOverflow : RetailImportDiagnosticCode.MalformedEnvelope);
					}

					using (opened)
					{
						var child = (IValidatedMixPackage)opened;
						ProcessContainer(child, name, context, group, rootName, maps);
						if (name == "audio.mix" || name == "audiomd.mix")
							ValidateAudio(child, context, group, rootName);
					}
				}
			}
			catch (InvalidDataException)
			{
				throw Corrupt(group, rootName);
			}
			catch (EndOfStreamException)
			{
				throw Corrupt(group, rootName);
			}
			catch (OverflowException)
			{
				throw GroupFailure(RetailImportErrorCode.CorruptArchive, group, rootName,
					RetailImportDiagnosticCode.ArithmeticOverflow);
			}
			catch (PackageEnvelopeException ex) when (ex.Failure != PackageEnvelopeFailure.AggregateLimitExceeded)
			{
				throw GroupFailure(RetailImportErrorCode.CorruptArchive, group, rootName,
					ex.Failure == PackageEnvelopeFailure.ArithmeticOverflow ?
						RetailImportDiagnosticCode.ArithmeticOverflow : RetailImportDiagnosticCode.MalformedEnvelope);
			}
		}

		void ValidateAudio(IValidatedMixPackage index, PackageReadContext context,
			RetailContentGroup group, string rootName)
		{
			var idx = Find(index, "audio.idx");
			var bag = Find(index, "audio.bag");
			if (idx == null || bag == null)
				throw GroupFailure(RetailImportErrorCode.InvalidAudio, group,
					group == RetailContentGroup.Base ? "language.mix" : rootName,
					RetailImportDiagnosticCode.MissingRequiredArchive);

			try
			{
				using var audio = IdxBagEnvelopeValidator.Validate(
					index.OpenEntrySource(idx.Hash), index.OpenEntrySource(bag.Hash), policy.Audio, context);
			}
			catch (PackageEnvelopeException ex) when (ex.Failure != PackageEnvelopeFailure.AggregateLimitExceeded)
			{
				throw GroupFailure(RetailImportErrorCode.InvalidAudio, group,
					group == RetailContentGroup.Base ? "language.mix" : rootName,
					ex.Failure == PackageEnvelopeFailure.ArithmeticOverflow ?
						RetailImportDiagnosticCode.ArithmeticOverflow : RetailImportDiagnosticCode.MalformedEnvelope);
			}
		}

		Dictionary<uint, string> ReadMapOverlay(IValidatedMixPackage index, PackageReadContext context)
		{
			var database = Find(index, "local mix database.dat");
			var overlay = new Dictionary<uint, string>();
			if (database == null)
				return overlay;
			if (database.Length > policy.Mix.MaximumLocalDatabaseBytes)
				throw new InvalidDataException("The local MIX database exceeds its byte limit.");

			using var databaseSource = index.OpenEntrySource(database.Hash);
			using var stream = databaseSource.OpenRead();
			var names = XccLocalDatabase.ReadValidated(stream,
				policy.Mix.MaximumLocalDatabaseBytes, policy.Mix.MaximumLocalDatabaseNames, context);
			var canonicalNames = new HashSet<string>(StringComparer.Ordinal);
			foreach (var name in names)
			{
				context.ThrowIfCancelledOrExpired();
				if (!IsCanonicalMapName(name) || !canonicalNames.Add(name))
					throw new InvalidDataException("The local MIX database contains an invalid map name.");
				var hash = PackageEntry.HashFilename(name, PackageHashType.CRC32);
				if (filenames.TryResolve(hash, PackageHashType.CRC32, out _) ||
					!index.Entries.Any(entry => entry.Hash == hash) || overlay.ContainsKey(hash))
					throw new InvalidDataException("The local MIX database contains a collision or missing entry.");
				overlay.Add(hash, name);
			}

			return overlay;
		}

		static ValidatedEmbeddedMapSource CreateMap(IValidatedMixPackage index, uint hash, string name,
			PackageReadContext context, RetailContentGroup group, string rootName)
		{
			var source = index.OpenEntrySource(hash);
			try
			{
				using var stream = source.OpenRead();
				string digest;
				try
				{
					digest = Hash(stream, source.Length, context);
				}
				catch (Exception ex) when (ex is EndOfStreamException || ex is InvalidDataException)
				{
					throw new FatalGraphException(new RetailImportFailure(RetailImportErrorCode.SourceChanged,
						group, rootName, RetailImportDiagnosticCode.LengthMismatch));
				}

				return new ValidatedEmbeddedMapSource(name, source.Length, digest, source);
			}
			catch
			{
				RetailOwnershipCleanup.Dispose(source);
				throw;
			}
		}

		static bool IsCanonicalMapName(string name)
		{
			if (string.IsNullOrEmpty(name) || name == "." || name == ".." ||
				!StringComparer.Ordinal.Equals(name, name.ToLowerInvariant()) ||
				name.Any(character => character > 0x7F || char.IsControl(character) ||
					character == '/' || character == '\\' || character == ':'))
				return false;
			return MapSuffixes.Contains(Path.GetExtension(name));
		}

		static bool Contains(IValidatedMixPackage index, string name) => Find(index, name) != null;
		static ValidatedMixEntry Find(IValidatedMixPackage index, string name) =>
			index.Entries.FirstOrDefault(entry => StringComparer.Ordinal.Equals(entry.CanonicalName, name));

		static string Hash(Stream stream, long expectedLength, PackageReadContext context)
		{
			using var hash = SHA256.Create();
			var buffer = new byte[64 << 10];
			long total = 0;
			while (total < expectedLength)
			{
				context.ThrowIfCancelledOrExpired();
				var read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, expectedLength - total));
				if (read == 0)
					throw new EndOfStreamException("The validated source ended before its declared length.");
				total = checked(total + read);
				hash.TransformBlock(buffer, 0, read, null, 0);
			}

			context.ThrowIfCancelledOrExpired();
			if (stream.ReadByte() != -1)
				throw new InvalidDataException("The validated source exceeds its declared length.");

			hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
			return BitConverter.ToString(hash.Hash).Replace("-", string.Empty).ToLowerInvariant();
		}

		static RetailGroupStatus Status(RetailGroupValidation validation,
			RetailImportDiagnosticCode diagnostic = RetailImportDiagnosticCode.None) =>
			new(validation, RetailEditionRecognition.NotApplicable, diagnostic);

		static void AddInvalid(RetailContentGroup group, RetailImportErrorCode code, string safeName,
			RetailImportDiagnosticCode diagnostic,
			ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus>.Builder groups,
			ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure>.Builder failures)
		{
			groups.Add(group, Status(RetailGroupValidation.Invalid, diagnostic));
			failures.Add(group, new RetailImportFailure(code, group, safeName, diagnostic));
		}

		static GroupGraphException Corrupt(RetailContentGroup group, string safeName) =>
			GroupFailure(RetailImportErrorCode.CorruptArchive, group, safeName,
				RetailImportDiagnosticCode.MalformedEnvelope);

		static GroupGraphException GroupFailure(RetailImportErrorCode code, RetailContentGroup group,
			string safeName, RetailImportDiagnosticCode diagnostic) =>
			new(new RetailImportFailure(code, group, safeName, diagnostic));

		static RetailOperationResult<RetailArchiveGraphResult> Failed(RetailImportErrorCode code,
			RetailContentGroup? group, string safeName, RetailImportDiagnosticCode diagnostic) =>
			RetailOperationResult<RetailArchiveGraphResult>.Failed(
				new RetailImportFailure(code, group, safeName, diagnostic));

		static void DisposeMapsFrom(List<ValidatedEmbeddedMapSource> maps, int first)
		{
			for (var i = maps.Count - 1; i >= first; i--)
			{
				RetailOwnershipCleanup.Dispose(maps[i]);
				maps.RemoveAt(i);
			}
		}

		sealed class ArchiveDeadline : IPackageReadDeadline
		{
			readonly IRetailMonotonicClock clock;
			readonly RetailValidationDeadline whole;
			readonly Dictionary<string, RetailValidationDeadline> roots = new(StringComparer.Ordinal);
			string current;

			public ArchiveDeadline(IRetailMonotonicClock clock, RetailValidationDeadline whole)
			{
				this.clock = clock;
				this.whole = whole;
			}

			public void Begin(string root)
			{
				if (!roots.ContainsKey(root))
					roots.Add(root, RetailValidationDeadline.Start(clock, IosImportEnvelopePolicyV1.ArchiveTimeout));
				current = root;
				ThrowIfExpired();
			}

			public void Select(string root)
			{
				if (!roots.ContainsKey(root))
					throw new InvalidOperationException("The archive deadline was not started.");
				current = root;
				ThrowIfExpired();
			}

			public void ThrowIfExpired()
			{
				whole.ThrowIfExpired(clock);
				if (current == null)
					throw new InvalidOperationException("No archive deadline is active.");
				roots[current].ThrowIfExpired(clock);
			}
		}

		sealed class OwnedRoot : IDisposable
		{
			Stream stream;
			public RetailArchiveInputDescriptor Descriptor { get; }
			public OwnedRoot(RetailArchiveInputDescriptor descriptor, Stream ownedStream)
			{
				Descriptor = descriptor;
				stream = ownedStream;
			}

			public Stream Detach() => Interlocked.Exchange(ref stream, null) ??
				throw new InvalidOperationException("The root stream ownership was already consumed.");
			public void Dispose() =>
				RetailOwnershipCleanup.Dispose(Interlocked.Exchange(ref stream, null));
		}

		sealed class ValidatedRoot : IDisposable
		{
			public IValidatedMixPackage Package { get; }
			public ValidatedRoot(IValidatedMixPackage package)
			{
				Package = package;
			}

			public void Dispose() => RetailOwnershipCleanup.Dispose(Package);
		}

		sealed class GroupGraphException : Exception
		{
			public RetailImportFailure Failure { get; }
			public GroupGraphException(RetailImportFailure failure) => Failure = failure;
		}

		sealed class FatalGraphException : Exception
		{
			public RetailImportFailure Failure { get; }
			public FatalGraphException(RetailImportFailure failure) => Failure = failure;
		}
	}
}
