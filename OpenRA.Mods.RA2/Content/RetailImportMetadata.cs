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
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace OpenRA.Mods.RA2.Content
{
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

	enum RetailImportDocumentKind : byte
	{
		ActiveGenerationPointer = 1,
		PublishJournal = 2,
		GenerationCatalog = 3,
		GenerationReceipt = 4,
		PendingSeal = 5,
	}

	public sealed class ActiveGenerationPointer
	{
		public int SchemaVersion { get; }
		public string GenerationId { get; }
		public long Epoch { get; }
		public string CommitId { get; }
		public string CatalogSha256 { get; }
		public string ReceiptsSha256 { get; }

		internal ActiveGenerationPointer(
			int schemaVersion,
			string generationId,
			long epoch,
			string commitId,
			string catalogSha256,
			string receiptsSha256)
		{
			RetailMetadataValidation.ValidateSchemaVersion(schemaVersion, nameof(schemaVersion));
			if (epoch < 0)
				throw new ArgumentOutOfRangeException(nameof(epoch));

			SchemaVersion = schemaVersion;
			GenerationId = RetailImportPaths.ValidateGenerationId(generationId, epoch, nameof(generationId));
			Epoch = epoch;
			CommitId = RetailImportPaths.ValidateCommitId(commitId, nameof(commitId));
			CatalogSha256 = RetailImportPaths.ValidateSha256(catalogSha256, nameof(catalogSha256));
			ReceiptsSha256 = RetailImportPaths.ValidateSha256(receiptsSha256, nameof(receiptsSha256));
		}
	}

	public sealed class RetailPublishJournal
	{
		public int SchemaVersion { get; }
		public RetailPublishState State { get; }
		public long Epoch { get; }
		public bool HasPreviousGeneration { get; }
		public string PreviousGenerationId { get; }
		public string ProposedGenerationId { get; }
		public string CommitId { get; }
		public string SourcePendingId { get; }
		public ImmutableArray<RetailContentGroup> AffectedGroups { get; }
		public ImmutableArray<string> AffectedRelativePaths { get; }

		internal RetailPublishJournal(
			int schemaVersion,
			RetailPublishState state,
			long epoch,
			bool hasPreviousGeneration,
			string previousGenerationId,
			string proposedGenerationId,
			string commitId,
			string sourcePendingId,
			ImmutableArray<RetailContentGroup> affectedGroups,
			ImmutableArray<string> affectedRelativePaths)
		{
			RetailMetadataValidation.ValidateSchemaVersion(schemaVersion, nameof(schemaVersion));
			if (!Enum.IsDefined(typeof(RetailPublishState), state))
				throw new ArgumentOutOfRangeException(nameof(state));
			if (epoch < 0)
				throw new ArgumentOutOfRangeException(nameof(epoch));
			if (!hasPreviousGeneration && (epoch != 0 || previousGenerationId != null))
				throw new ArgumentException(
					"Only an epoch-zero first installation may omit the previous generation.",
					nameof(hasPreviousGeneration));
			if (hasPreviousGeneration && previousGenerationId == null)
				throw new ArgumentException(
					"A non-first publication must bind the previous generation.",
					nameof(previousGenerationId));
			var canonicalPreviousGenerationId = hasPreviousGeneration
				? RetailMetadataValidation.ValidateGenerationIdWithoutKnownEpoch(
					previousGenerationId, nameof(previousGenerationId))
				: null;
			if (hasPreviousGeneration &&
				(!long.TryParse(canonicalPreviousGenerationId.AsSpan(2, 20),
					System.Globalization.NumberStyles.None,
					System.Globalization.CultureInfo.InvariantCulture, out var previousEpoch) ||
				 previousEpoch >= epoch))
				throw new ArgumentException(
					"The previous generation epoch must precede the proposed epoch.",
					nameof(previousGenerationId));

			SchemaVersion = schemaVersion;
			State = state;
			Epoch = epoch;
			HasPreviousGeneration = hasPreviousGeneration;
			PreviousGenerationId = canonicalPreviousGenerationId;
			ProposedGenerationId = RetailImportPaths.ValidateGenerationId(
				proposedGenerationId, epoch, nameof(proposedGenerationId));
			CommitId = RetailImportPaths.ValidateCommitId(commitId, nameof(commitId));
			SourcePendingId = RetailImportPaths.ValidateOptionalPendingId(
				sourcePendingId, nameof(sourcePendingId));
			AffectedGroups = RetailMetadataValidation.CopyOrderedGroups(
				affectedGroups, nameof(affectedGroups), requireNonempty: true, requireAll: false);
			AffectedRelativePaths = RetailMetadataValidation.CopyOrderedPaths(
				affectedRelativePaths, nameof(affectedRelativePaths),
				RetailImportPaths.ValidateGenerationRelativePath, requireNonempty: true);
			RetailMetadataValidation.ValidateAffectedPathProjection(
				AffectedGroups, AffectedRelativePaths, nameof(affectedRelativePaths));
		}
	}

	public sealed class RetailPendingArtifact
	{
		public int Ordinal { get; }
		public string SafeBasename { get; }
		public string CanonicalStagedRelativePath { get; }
		public RetailContentGroup Group { get; }
		public long Length { get; }
		public string Sha256 { get; }

		internal RetailPendingArtifact(
			int ordinal,
			string safeBasename,
			string canonicalStagedRelativePath,
			RetailContentGroup group,
			long length,
			string sha256)
		{
			if (ordinal < 0 || ordinal >= IosImportEnvelopePolicyV1.CandidateFiles)
				throw new ArgumentOutOfRangeException(nameof(ordinal));
			if (!Enum.IsDefined(typeof(RetailContentGroup), group))
				throw new ArgumentOutOfRangeException(nameof(group));
			RetailSafeBasename.Validate(safeBasename, nameof(safeBasename));
			if (!RetailCandidatePlan.TryClassify(safeBasename, out var canonicalBasename, out var classifiedGroup) ||
				!StringComparer.Ordinal.Equals(canonicalBasename, safeBasename) || classifiedGroup != group)
				throw new ArgumentException(
					"The pending artifact basename does not match its closed content group.",
					nameof(safeBasename));
			if (length < 0 || length > IosImportEnvelopePolicyV1.SourceBytes)
				throw new ArgumentOutOfRangeException(nameof(length));

			Ordinal = ordinal;
			SafeBasename = safeBasename;
			CanonicalStagedRelativePath = RetailImportPaths.ValidateStagedRelativePath(
				canonicalStagedRelativePath, nameof(canonicalStagedRelativePath));
			Group = group;
			Length = length;
			Sha256 = RetailImportPaths.ValidateSha256(sha256, nameof(sha256));
		}
	}

	public sealed class RetailPendingSeal
	{
		public int SchemaVersion { get; }
		public string PendingId { get; }
		public DateTimeOffset SealedAtUtc { get; }
		public DateTimeOffset ExpiresAtUtc { get; }
		public ImmutableArray<RetailPendingArtifact> Artifacts { get; }
		public long TotalBytes { get; }

		internal RetailPendingSeal(
			int schemaVersion,
			string pendingId,
			DateTimeOffset sealedAtUtc,
			ImmutableArray<RetailPendingArtifact> artifacts,
			long totalBytes)
		{
			RetailMetadataValidation.ValidateSchemaVersion(schemaVersion, nameof(schemaVersion));
			if (sealedAtUtc.Offset != TimeSpan.Zero)
				throw new ArgumentException("The pending seal time must have a zero UTC offset.", nameof(sealedAtUtc));
			if (artifacts.IsDefault || artifacts.Length is < 1 or > IosImportEnvelopePolicyV1.CandidateFiles)
				throw new ArgumentException("The pending artifact collection is outside V1 bounds.", nameof(artifacts));

			DateTimeOffset expiresAtUtc;
			try
			{
				expiresAtUtc = sealedAtUtc.Add(IosImportEnvelopePolicyV1.PendingRetention);
			}
			catch (ArgumentOutOfRangeException exception)
			{
				throw new ArgumentException(
					"The pending expiry would overflow.", nameof(sealedAtUtc), exception);
			}

			var copy = ImmutableArray.CreateBuilder<RetailPendingArtifact>(artifacts.Length);
			var paths = new HashSet<string>(StringComparer.Ordinal);
			var nonMapNames = new HashSet<string>(StringComparer.Ordinal);
			var mapHashes = new HashSet<string>(StringComparer.Ordinal);
			long computedTotal = 0;
			for (var index = 0; index < artifacts.Length; index++)
			{
				var artifact = artifacts[index] ?? throw new ArgumentException(
					"The pending artifact collection contains null.", nameof(artifacts));
				if (artifact.Ordinal != index)
					throw new ArgumentException(
						"Pending artifact ordinals must be contiguous and zero based.", nameof(artifacts));
				if (!paths.Add(artifact.CanonicalStagedRelativePath))
					throw new ArgumentException("Pending artifact paths must be unique.", nameof(artifacts));
				if (artifact.Group == RetailContentGroup.Maps)
				{
					if (!mapHashes.Add(artifact.Sha256))
						throw new ArgumentException(
							"Pending map digests must be unique.", nameof(artifacts));
				}
				else if (!nonMapNames.Add(artifact.SafeBasename))
					throw new ArgumentException(
						"Pending archive basenames must be unique.", nameof(artifacts));

				computedTotal = checked(computedTotal + artifact.Length);
				copy.Add(artifact);
			}

			if (computedTotal != totalBytes || totalBytes < 0 || totalBytes > IosImportEnvelopePolicyV1.PendingBytes)
				throw new ArgumentException(
					"Pending byte accounting must exactly match the bounded artifact set.", nameof(totalBytes));

			SchemaVersion = schemaVersion;
			PendingId = RetailImportPaths.ValidatePendingId(pendingId, nameof(pendingId));
			SealedAtUtc = sealedAtUtc;
			ExpiresAtUtc = expiresAtUtc;
			Artifacts = copy.MoveToImmutable();
			TotalBytes = totalBytes;
		}
	}

	public sealed class MapResidentUsage
	{
		public long MetadataBytes { get; }
		public long StringBytes { get; }
		public int StringEntries { get; }

		internal MapResidentUsage(long metadataBytes, long stringBytes, int stringEntries)
		{
			if (metadataBytes < 0 || metadataBytes > IosImportEnvelopePolicyV1.ResidentMapMetadataBytes)
				throw new ArgumentOutOfRangeException(nameof(metadataBytes));
			if (stringBytes < 0 || stringBytes > IosImportEnvelopePolicyV1.ResidentStringBytes)
				throw new ArgumentOutOfRangeException(nameof(stringBytes));
			if (stringEntries < 0 || stringEntries > IosImportEnvelopePolicyV1.ResidentStringEntries)
				throw new ArgumentOutOfRangeException(nameof(stringEntries));

			MetadataBytes = metadataBytes;
			StringBytes = stringBytes;
			StringEntries = stringEntries;
		}
	}

	static class RetailMapResidentUsageMeterV1
	{
		internal const int SchemaVersion = 1;
		static readonly UTF8Encoding StrictUtf8 = new(false, true);

		internal static MapResidentUsage Measure(ValidatedMapMetadata metadata, int mapFormat)
		{
			var encoded = EncodeCanonical(metadata, mapFormat);
			var strings = new HashSet<string>(StringComparer.Ordinal);
			long stringBytes = 0;
			var stringEntries = 0;

			void Add(string value)
			{
				if (value == null || !strings.Add(value))
					return;

				stringBytes = checked(stringBytes + StrictUtf8.GetByteCount(value));
				stringEntries = checked(stringEntries + 1);
			}

			ProjectStrings(metadata, Add);
			return new MapResidentUsage(encoded.LongLength, stringBytes, stringEntries);
		}

		internal static byte[] EncodeCanonical(ValidatedMapMetadata metadata, int mapFormat)
		{
			if (metadata == null)
				throw new ArgumentNullException(nameof(metadata));
			if (mapFormat <= 0)
				throw new ArgumentOutOfRangeException(nameof(mapFormat));

			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream, StrictUtf8, leaveOpen: true);
			writer.Write(new byte[] { (byte)'N', (byte)'H', (byte)'R', (byte)'M', SchemaVersion });
			WriteInt32(writer, mapFormat);
			WriteRequiredString(writer, metadata.Title);
			WriteRequiredString(writer, metadata.Author);
			WriteStringArray(writer, metadata.Categories);
			WriteRequiredString(writer, metadata.Tileset);
			WriteInt32(writer, (int)metadata.GridType);
			WriteInt32(writer, metadata.MapSize.X);
			WriteInt32(writer, metadata.MapSize.Y);
			WriteInt32(writer, metadata.Bounds.X);
			WriteInt32(writer, metadata.Bounds.Y);
			WriteInt32(writer, metadata.Bounds.Width);
			WriteInt32(writer, metadata.Bounds.Height);
			WriteUInt32(writer, checked((uint)metadata.Players.Length));
			foreach (var player in metadata.Players)
				WritePlayer(writer, player);
			WriteUInt32(writer, checked((uint)metadata.SpawnPoints.Length));
			foreach (var spawn in metadata.SpawnPoints)
				WriteCell(writer, spawn);
			writer.Flush();
			return stream.ToArray();
		}

		static void ProjectStrings(ValidatedMapMetadata metadata, Action<string> add)
		{
			add(metadata.Title);
			add(metadata.Author);
			foreach (var category in metadata.Categories)
				add(category);
			add(metadata.Tileset);
			foreach (var player in metadata.Players)
			{
				add(player.Name);
				add(player.Palette);
				add(player.Bot);
				add(player.StartingUnitsClass);
				add(player.Faction);
				foreach (var ally in player.Allies)
					add(ally);
				foreach (var enemy in player.Enemies)
					add(enemy);
			}
		}

		static void WritePlayer(BinaryWriter writer, ValidatedMapPlayerMetadata player)
		{
			if (player == null)
				throw new ArgumentException("The validated player projection contains null.");

			WriteRequiredString(writer, player.Name);
			WriteOptionalString(writer, player.Palette);
			WriteOptionalString(writer, player.Bot);
			WriteOptionalString(writer, player.StartingUnitsClass);
			WriteBoolean(writer, player.AllowBots);
			WriteBoolean(writer, player.Playable);
			WriteBoolean(writer, player.Required);
			WriteBoolean(writer, player.OwnsWorld);
			WriteBoolean(writer, player.Spectating);
			WriteBoolean(writer, player.NonCombatant);
			WriteBoolean(writer, player.LockFaction);
			WriteOptionalString(writer, player.Faction);
			WriteBoolean(writer, player.LockColor);
			writer.Write(player.Color.R);
			writer.Write(player.Color.G);
			writer.Write(player.Color.B);
			writer.Write(player.Color.A);
			WriteCell(writer, player.HomeLocation);
			WriteBoolean(writer, player.LockSpawn);
			WriteInt32(writer, player.Spawn);
			WriteBoolean(writer, player.LockTeam);
			WriteInt32(writer, player.Team);
			WriteBoolean(writer, player.LockHandicap);
			WriteInt32(writer, player.Handicap);
			WriteStringArray(writer, player.Allies);
			WriteStringArray(writer, player.Enemies);
		}

		static void WriteCell(BinaryWriter writer, CPos value)
		{
			WriteInt32(writer, value.X);
			WriteInt32(writer, value.Y);
			writer.Write(value.Layer);
		}

		static void WriteStringArray(BinaryWriter writer, ImmutableArray<string> values)
		{
			if (values.IsDefault)
				throw new ArgumentException("The string collection is uninitialized.", nameof(values));
			WriteUInt32(writer, checked((uint)values.Length));
			foreach (var value in values)
				WriteRequiredString(writer, value);
		}

		static void WriteRequiredString(BinaryWriter writer, string value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));
			WriteOptionalString(writer, value);
		}

		static void WriteOptionalString(BinaryWriter writer, string value)
		{
			if (value == null)
			{
				writer.Write((byte)0);
				return;
			}

			var bytes = StrictUtf8.GetBytes(value);
			if (bytes.Length == 0)
				throw new ArgumentException("Present map strings cannot use an empty sentinel.", nameof(value));
			writer.Write((byte)1);
			WriteUInt32(writer, checked((uint)bytes.Length));
			writer.Write(bytes);
		}

		static void WriteBoolean(BinaryWriter writer, bool value) => writer.Write(value ? (byte)1 : (byte)0);

		static void WriteInt32(BinaryWriter writer, int value) => WriteUInt32(writer, unchecked((uint)value));

		static void WriteUInt32(BinaryWriter writer, uint value)
		{
			writer.Write((byte)value);
			writer.Write((byte)(value >> 8));
			writer.Write((byte)(value >> 16));
			writer.Write((byte)(value >> 24));
		}
	}

	public sealed class RetailMapReceipt
	{
		public string LogicalName { get; }
		public string SourceSha256 { get; }
		public string PackageSha256 { get; }
		public long CompressedBytes { get; }
		public string MapUid { get; }
		public Ra2MapCompatibleProfiles CompatibleProfiles { get; }
		public ValidatedMapMetadata Metadata { get; }
		public int EnvelopeVersion { get; }
		public int ConverterVersion { get; }
		public int MapFormat { get; }
		public long RetainedMetadataBytes { get; }
		public long RetainedStringBytes { get; }
		public int RetainedStringEntries { get; }

		internal RetailMapReceipt(
			string logicalName,
			string sourceSha256,
			string packageSha256,
			long compressedBytes,
			string mapUid,
			Ra2MapCompatibleProfiles compatibleProfiles,
			ValidatedMapMetadata metadata,
			int envelopeVersion,
			int converterVersion,
			int mapFormat,
			long retainedMetadataBytes,
			long retainedStringBytes,
			int retainedStringEntries)
		{
			mapUid = RetailImportPaths.ValidateMapUid(mapUid, nameof(mapUid));
			if (!StringComparer.Ordinal.Equals(logicalName, mapUid))
				throw new ArgumentException("A map logical name must equal its canonical UID.", nameof(logicalName));
			if (compressedBytes < 0 || compressedBytes > IosImportEnvelopePolicyV1.OramapCompressedBytes)
				throw new ArgumentOutOfRangeException(nameof(compressedBytes));
			if (compatibleProfiles == Ra2MapCompatibleProfiles.None ||
				(compatibleProfiles & ~(Ra2MapCompatibleProfiles.Base | Ra2MapCompatibleProfiles.Yuri)) != 0)
				throw new ArgumentOutOfRangeException(nameof(compatibleProfiles));
			if (metadata == null)
				throw new ArgumentNullException(nameof(metadata));
			if (envelopeVersion <= 0)
				throw new ArgumentOutOfRangeException(nameof(envelopeVersion));
			if (converterVersion < 0)
				throw new ArgumentOutOfRangeException(nameof(converterVersion));
			if (mapFormat <= 0)
				throw new ArgumentOutOfRangeException(nameof(mapFormat));

			var measured = RetailMapResidentUsageMeterV1.Measure(metadata, mapFormat);
			if (retainedMetadataBytes != measured.MetadataBytes ||
				retainedStringBytes != measured.StringBytes ||
				retainedStringEntries != measured.StringEntries)
				throw new ArgumentException("The retained map usage does not match the canonical projection.");

			LogicalName = logicalName;
			SourceSha256 = RetailImportPaths.ValidateSha256(sourceSha256, nameof(sourceSha256));
			PackageSha256 = RetailImportPaths.ValidateSha256(packageSha256, nameof(packageSha256));
			CompressedBytes = compressedBytes;
			MapUid = mapUid;
			CompatibleProfiles = compatibleProfiles;
			Metadata = metadata;
			EnvelopeVersion = envelopeVersion;
			ConverterVersion = converterVersion;
			MapFormat = mapFormat;
			RetainedMetadataBytes = retainedMetadataBytes;
			RetainedStringBytes = retainedStringBytes;
			RetainedStringEntries = retainedStringEntries;
		}
	}

	public sealed class RetailReceiptEntry
	{
		public string CanonicalRelativePath { get; }
		public long Length { get; }
		public string Sha256 { get; }
		public string RecognizedEditionCode { get; }

		internal RetailReceiptEntry(
			string canonicalRelativePath,
			long length,
			string sha256,
			string recognizedEditionCode)
		{
			if (length < 0 || length > IosImportEnvelopePolicyV1.SourceBytes)
				throw new ArgumentOutOfRangeException(nameof(length));
			if (recognizedEditionCode != null)
				throw new ArgumentException(
					"Schema V1 has no trusted recognized-edition code set.", nameof(recognizedEditionCode));

			CanonicalRelativePath = RetailImportPaths.ValidateGenerationRelativePath(
				canonicalRelativePath, nameof(canonicalRelativePath));
			Length = length;
			Sha256 = RetailImportPaths.ValidateSha256(sha256, nameof(sha256));
			RecognizedEditionCode = null;
		}
	}

	public sealed class RetailGroupReceipt
	{
		public RetailContentGroup Group { get; }
		public RetailGroupValidation State { get; }
		public RetailEditionRecognition Recognition { get; }
		public RetailImportFailure Failure { get; }
		public ImmutableArray<RetailReceiptEntry> Entries { get; }
		public int ValidatorVersion { get; }

		internal RetailGroupReceipt(
			RetailContentGroup group,
			RetailGroupValidation state,
			RetailEditionRecognition recognition,
			RetailImportFailure failure,
			ImmutableArray<RetailReceiptEntry> entries,
			int validatorVersion)
		{
			if (!Enum.IsDefined(typeof(RetailContentGroup), group))
				throw new ArgumentOutOfRangeException(nameof(group));
			if (!Enum.IsDefined(typeof(RetailGroupValidation), state))
				throw new ArgumentOutOfRangeException(nameof(state));
			if (!Enum.IsDefined(typeof(RetailEditionRecognition), recognition) ||
				recognition == RetailEditionRecognition.Recognized)
				throw new ArgumentOutOfRangeException(nameof(recognition),
					"Schema V1 does not admit recognized retail editions.");
			if (validatorVersion <= 0)
				throw new ArgumentOutOfRangeException(nameof(validatorVersion));
			RetailMetadataValidation.ValidateGroupState(
				group, state, recognition, nameof(state));
			if (state == RetailGroupValidation.Invalid)
				RetailMetadataValidation.ValidateGroupFailure(group, failure, nameof(failure));
			else if (failure != null)
				throw new ArgumentException("Only an invalid group may carry a failure.", nameof(failure));

			var copy = RetailMetadataValidation.CopyReceiptEntries(entries, nameof(entries));
			foreach (var entry in copy)
				RetailMetadataValidation.ValidateReceiptEntryPath(
					entry.CanonicalRelativePath, group, nameof(entries));
			if (state == RetailGroupValidation.Ready && copy.Length == 0)
				throw new ArgumentException("A ready group must carry at least one receipt entry.", nameof(entries));
			if (state != RetailGroupValidation.Ready && copy.Length != 0)
				throw new ArgumentException("A non-ready group cannot carry active receipt entries.", nameof(entries));

			Group = group;
			State = state;
			Recognition = recognition;
			Failure = failure;
			Entries = copy;
			ValidatorVersion = validatorVersion;
		}
	}

	public sealed class RetailCandidateArtifact
	{
		public string LogicalName { get; }
		public string CanonicalStagedRelativePath { get; }
		public RetailContentGroup Group { get; }
		public long Length { get; }
		public string Sha256 { get; }

		internal RetailCandidateArtifact(
			string logicalName,
			string canonicalStagedRelativePath,
			RetailContentGroup group,
			long length,
			string sha256)
		{
			RetailMetadataValidation.ValidateArtifactLogicalName(logicalName, group, nameof(logicalName));
			if (length < 0 || length > IosImportEnvelopePolicyV1.SourceBytes)
				throw new ArgumentOutOfRangeException(nameof(length));

			LogicalName = logicalName;
			CanonicalStagedRelativePath = RetailImportPaths.ValidateStagedRelativePath(
				canonicalStagedRelativePath, nameof(canonicalStagedRelativePath));
			Group = group;
			Length = length;
			Sha256 = RetailImportPaths.ValidateSha256(sha256, nameof(sha256));
		}
	}

	sealed class RetailCandidateSourceRootCapability : IDisposable
	{
		IosRuntimeRootDirectoryOwner root;

		internal RetailCandidateSourceRootCapability(string rootPath)
		{
			if (string.IsNullOrEmpty(rootPath) || !Path.IsPathFullyQualified(rootPath))
				throw new ArgumentException("The candidate root must be an absolute app-owned path.", nameof(rootPath));

			if (!TryOpenRootWithoutFollowingComponents(rootPath, out root))
				throw new ArgumentException(
					"The candidate root could not be opened without following links.", nameof(rootPath));
		}

		internal IosRuntimeRootDirectoryOwner TakeRoot() =>
			Interlocked.Exchange(ref root, null) ?? throw new InvalidOperationException(
				"The candidate root capability has already been consumed.");

		public void Dispose() => Interlocked.Exchange(ref root, null)?.Release();

		static bool TryOpenRootWithoutFollowingComponents(
			string rootPath,
			out IosRuntimeRootDirectoryOwner openedRoot)
		{
			openedRoot = null;
			var descriptor = -1;
			try
			{
				if (!IosRuntimeUnixNative.TryGetCurrentLayout(out var layout))
					return false;

				var canonicalRoot = Path.GetFullPath(rootPath);
				var components = canonicalRoot.Split(
					new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
				if (components.Length == 0)
					return false;

				descriptor = IosRuntimeUnixNative.Open(
					Path.DirectorySeparatorChar.ToString(), layout.DirectoryFlags, 0);
				if (descriptor < 0 ||
					!IosRuntimeUnixNative.TryReadIdentity(descriptor, layout, out var identity) ||
					!identity.IsDirectory)
					return false;

				foreach (var component in components)
				{
					var next = IosRuntimeUnixNative.OpenAt(
						descriptor, component, layout.DirectoryFlags, 0);
					if (next < 0 ||
						!IosRuntimeUnixNative.TryReadIdentity(next, layout, out identity) ||
						!identity.IsDirectory)
					{
						if (next >= 0)
							IosRuntimeUnixNative.Close(next);
						return false;
					}

					IosRuntimeUnixNative.Close(descriptor);
					descriptor = next;
				}

				openedRoot = new IosRuntimeRootDirectoryOwner(descriptor, layout, identity);
				descriptor = -1;
				return true;
			}
			catch (Exception exception) when (IosRuntimeResourceRoot.IsBoundaryException(exception))
			{
				return false;
			}
			finally
			{
				if (descriptor >= 0)
					IosRuntimeUnixNative.Close(descriptor);
			}
		}
	}

	public sealed class RetailCandidateArtifactSource : IDisposable
	{
		IosRuntimeRootDirectoryOwner root;

		internal string SourcePendingId { get; }

		internal RetailCandidateArtifactSource(
			string sourcePendingId,
			RetailCandidateSourceRootCapability rootCapability)
		{
			SourcePendingId = RetailImportPaths.ValidateOptionalPendingId(
				sourcePendingId, nameof(sourcePendingId));
			if (rootCapability == null)
				throw new ArgumentNullException(nameof(rootCapability));

			root = rootCapability.TakeRoot();
		}

		public Stream OpenVerifiedRead(RetailCandidateArtifact artifact)
		{
			var currentRoot = root;
			if (currentRoot == null)
				throw new ObjectDisposedException(nameof(RetailCandidateArtifactSource));
			if (artifact == null)
				throw new ArgumentNullException(nameof(artifact));

			if (!IosRuntimeResourcePath.TrySplit(
					artifact.CanonicalStagedRelativePath, out var components) ||
				!currentRoot.TryRetain())
				throw new IOException("The candidate artifact could not be opened safely.");
			var acquired = IosRuntimeResourceRoot.AcquireRegularFileRetained(
				currentRoot, artifact.CanonicalStagedRelativePath, components);
			if (!acquired.Succeeded)
				throw new IOException("The candidate artifact could not be opened safely.");

			using var lease = acquired.Value;
			if (lease.Length != artifact.Length)
				throw new InvalidDataException("The candidate artifact length changed.");

			Stream stream = null;
			try
			{
				stream = lease.OpenVerifiedRead();
				using var hash = SHA256.Create();
				var actual = Convert.ToHexString(hash.ComputeHash(stream)).ToLowerInvariant();
				if (!StringComparer.Ordinal.Equals(actual, artifact.Sha256))
					throw new InvalidDataException("The candidate artifact digest changed.");
				lease.VerifyUnchanged();
				stream.Position = 0;
				return stream;
			}
			catch
			{
				stream?.Dispose();
				throw;
			}
		}

		public void Dispose() => Interlocked.Exchange(ref root, null)?.Release();
	}

	public sealed class RetailCatalogArtifact
	{
		public string LogicalName { get; }
		public string CanonicalGenerationRelativePath { get; }
		public RetailContentGroup Group { get; }
		public long Length { get; }
		public string Sha256 { get; }

		internal RetailCatalogArtifact(
			string logicalName,
			string canonicalGenerationRelativePath,
			RetailContentGroup group,
			long length,
			string sha256)
		{
			RetailMetadataValidation.ValidateArtifactLogicalName(logicalName, group, nameof(logicalName));
			if (length < 0 || length > IosImportEnvelopePolicyV1.SourceBytes)
				throw new ArgumentOutOfRangeException(nameof(length));
			canonicalGenerationRelativePath = RetailImportPaths.ValidateGenerationRelativePath(
				canonicalGenerationRelativePath, group, logicalName,
				nameof(canonicalGenerationRelativePath));

			LogicalName = logicalName;
			CanonicalGenerationRelativePath = canonicalGenerationRelativePath;
			Group = group;
			Length = length;
			Sha256 = RetailImportPaths.ValidateSha256(sha256, nameof(sha256));
		}
	}

	public sealed class RetailCandidateCatalog
	{
		public Ra2RuntimeProfileId Profile { get; }
		public Ra2RuntimeContract Contract { get; }
		public ImmutableArray<RetailCandidateArtifact> Artifacts { get; }
		public ImmutableArray<RetailGroupReceipt> Groups { get; }
		public ImmutableArray<RetailMapReceipt> Maps { get; }
		public MapResidentUsage MapResidentUsage { get; }

		internal RetailCandidateCatalog(
			Ra2RuntimeProfileId profile,
			Ra2RuntimeContract contract,
			ImmutableArray<RetailCandidateArtifact> artifacts,
			ImmutableArray<RetailGroupReceipt> groups,
			ImmutableArray<RetailMapReceipt> maps,
			MapResidentUsage mapResidentUsage)
		{
			RetailMetadataValidation.ValidateProfileAndContract(profile, contract);
			Profile = profile;
			Contract = contract;
			Artifacts = RetailMetadataValidation.CopyCandidateArtifacts(artifacts, nameof(artifacts));
			Groups = RetailMetadataValidation.CopyAllGroups(groups, nameof(groups));
			Maps = RetailMetadataValidation.CopyMaps(maps, nameof(maps));
			MapResidentUsage = RetailMetadataValidation.ValidateAggregateUsage(
				Maps, mapResidentUsage, nameof(mapResidentUsage));
			RetailMetadataValidation.ValidateContractMapFormats(Contract, Maps, nameof(maps));
			RetailMetadataValidation.ValidatePersistedProfileProjection(
				Profile, Groups, Maps, nameof(groups));
			RetailMetadataValidation.ValidateMapGroupEntries(Groups, Maps, nameof(groups));
			RetailMetadataValidation.ValidateArtifactReceiptSet(Artifacts, Groups, nameof(artifacts));
		}
	}

	public interface IRetailCatalogArtifactSource : IDisposable
	{
		Stream OpenVerifiedRead(RetailCatalogArtifact artifact);
	}

	public sealed class RetailGenerationCatalog
	{
		public Ra2RuntimeProfileId Profile { get; }
		public Ra2RuntimeContract Contract { get; }
		public ImmutableArray<RetailCatalogArtifact> Artifacts { get; }
		public ImmutableArray<RetailGroupReceipt> Groups { get; }
		public ImmutableArray<RetailMapReceipt> Maps { get; }
		public MapResidentUsage MapResidentUsage { get; }

		internal RetailGenerationCatalog(
			Ra2RuntimeProfileId profile,
			Ra2RuntimeContract contract,
			ImmutableArray<RetailCatalogArtifact> artifacts,
			ImmutableArray<RetailGroupReceipt> groups,
			ImmutableArray<RetailMapReceipt> maps,
			MapResidentUsage mapResidentUsage)
		{
			RetailMetadataValidation.ValidateProfileAndContract(profile, contract);
			Profile = profile;
			Contract = contract;
			Artifacts = RetailMetadataValidation.CopyCatalogArtifacts(artifacts, nameof(artifacts));
			Groups = RetailMetadataValidation.CopyAllGroups(groups, nameof(groups));
			Maps = RetailMetadataValidation.CopyMaps(maps, nameof(maps));
			MapResidentUsage = RetailMetadataValidation.ValidateAggregateUsage(
				Maps, mapResidentUsage, nameof(mapResidentUsage));
			RetailMetadataValidation.ValidateContractMapFormats(Contract, Maps, nameof(maps));
			RetailMetadataValidation.ValidatePersistedProfileProjection(
				Profile, Groups, Maps, nameof(groups));
			RetailMetadataValidation.ValidateMapGroupEntries(Groups, Maps, nameof(groups));
			RetailMetadataValidation.ValidateArtifactReceiptSet(Artifacts, Groups, nameof(artifacts));
		}
	}

	public sealed class RetailCatalogRepairPlan
	{
		public ImmutableArray<RetailContentGroup> RemoveGroups { get; }
		public ImmutableArray<string> RemoveMapLogicalNames { get; }
		public RetailImportFailure Reason { get; }

		RetailCatalogRepairPlan(
			ImmutableArray<RetailContentGroup> removeGroups,
			ImmutableArray<string> removeMapLogicalNames,
			RetailImportFailure reason)
		{
			RemoveGroups = RetailMetadataValidation.CopyOrderedGroups(
				removeGroups, nameof(removeGroups), requireNonempty: false, requireAll: false);
			if (RemoveGroups.Any(group => group is RetailContentGroup.Base or RetailContentGroup.Maps))
				throw new ArgumentException(
					"Only optional archive groups may be removed as whole groups.", nameof(removeGroups));
			RemoveMapLogicalNames = RetailMetadataValidation.CopyOrderedMapUids(
				removeMapLogicalNames, nameof(removeMapLogicalNames));
			Reason = reason ?? throw new ArgumentNullException(nameof(reason));
			if (RemoveGroups.IsEmpty && RemoveMapLogicalNames.IsEmpty)
				throw new ArgumentException("A repair plan must remove at least one invalid item.");
		}

		internal static RetailCatalogRepairPlan Create(
			ImmutableArray<RetailContentGroup> removeGroups,
			ImmutableArray<string> removeMapLogicalNames,
			RetailImportFailure reason) =>
			new(removeGroups, removeMapLogicalNames, reason);
	}

	public sealed class RetailReadinessResult
	{
		public ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> Groups { get; }
		public ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure> GroupFailures { get; }
		public int BaseCompatibleMapCount { get; }
		public int YuriCompatibleMapCount { get; }
		public int QuarantinedMapCount { get; }
		public RetailImportFailure Failure { get; }
		public bool BaseReady => Failure == null && BaseCompatibleMapCount > 0 &&
			Groups[RetailContentGroup.Base].Validation == RetailGroupValidation.Ready &&
			Groups[RetailContentGroup.Maps].Validation == RetailGroupValidation.Ready;
		public bool YuriReady => BaseReady && YuriCompatibleMapCount > 0 &&
			Groups[RetailContentGroup.Yuri].Validation == RetailGroupValidation.Ready;

		RetailReadinessResult(
			ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> groups,
			ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure> groupFailures,
			int baseCompatibleMapCount,
			int yuriCompatibleMapCount,
			int quarantinedMapCount,
			RetailImportFailure failure)
		{
			if (baseCompatibleMapCount < 0 || baseCompatibleMapCount > IosImportEnvelopePolicyV1.ImportedMaps)
				throw new ArgumentOutOfRangeException(nameof(baseCompatibleMapCount));
			if (yuriCompatibleMapCount < 0 || yuriCompatibleMapCount > IosImportEnvelopePolicyV1.ImportedMaps)
				throw new ArgumentOutOfRangeException(nameof(yuriCompatibleMapCount));
			if (quarantinedMapCount < 0 || quarantinedMapCount > IosImportEnvelopePolicyV1.ImportedMaps)
				throw new ArgumentOutOfRangeException(nameof(quarantinedMapCount));

			Groups = RetailMetadataValidation.CopyReadinessGroups(groups, nameof(groups));
			GroupFailures = RetailMetadataValidation.CopyGroupFailures(
				Groups, groupFailures, nameof(groupFailures));
			BaseCompatibleMapCount = baseCompatibleMapCount;
			YuriCompatibleMapCount = yuriCompatibleMapCount;
			QuarantinedMapCount = quarantinedMapCount;
			var hasPlayableBaseProjection = baseCompatibleMapCount > 0 &&
				Groups[RetailContentGroup.Base].Validation == RetailGroupValidation.Ready &&
				Groups[RetailContentGroup.Maps].Validation == RetailGroupValidation.Ready;
			if (failure == null != hasPlayableBaseProjection)
				throw new ArgumentException(
					"A readiness failure must be present exactly when the Base projection is unusable.",
					nameof(failure));
			Failure = failure;
		}

		internal static RetailReadinessResult Create(
			ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> groups,
			ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure> groupFailures,
			int baseCompatibleMapCount,
			int yuriCompatibleMapCount,
			int quarantinedMapCount,
			RetailImportFailure failure) =>
			new(groups, groupFailures, baseCompatibleMapCount, yuriCompatibleMapCount,
				quarantinedMapCount, failure);
	}

	public sealed class RetailCandidateValidation
	{
		public RetailCandidateCatalog Catalog { get; }
		public RetailReadinessResult Readiness { get; }

		RetailCandidateValidation(RetailCandidateCatalog catalog, RetailReadinessResult readiness)
		{
			Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
			Readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
			RetailMetadataValidation.ValidateReadinessMatchesCatalog(
				catalog.Profile, catalog.Groups, catalog.Maps, readiness);
		}

		internal static RetailCandidateValidation Create(
			RetailCandidateCatalog catalog, RetailReadinessResult readiness) => new(catalog, readiness);
	}

	public sealed class RetailRepairValidation
	{
		public RetailGenerationCatalog Catalog { get; }
		public RetailReadinessResult Readiness { get; }

		RetailRepairValidation(RetailGenerationCatalog catalog, RetailReadinessResult readiness)
		{
			Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
			Readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
			RetailMetadataValidation.ValidateReadinessMatchesCatalog(
				catalog.Profile, catalog.Groups, catalog.Maps, readiness);
		}

		internal static RetailRepairValidation Create(
			RetailGenerationCatalog catalog, RetailReadinessResult readiness) => new(catalog, readiness);
	}

	public sealed class RetailGenerationReceipt
	{
		public long Epoch { get; }
		public string CommitId { get; }
		public string SourcePendingId { get; }
		public Ra2RuntimeProfileId Profile { get; }
		public Ra2RuntimeContract Contract { get; }
		public string FilenameCatalogDigest { get; }
		public int ArchiveValidatorVersion { get; }
		public int MapValidatorVersion { get; }
		public ImmutableArray<RetailGroupReceipt> Groups { get; }
		public ImmutableArray<RetailMapReceipt> Maps { get; }
		public MapResidentUsage MapResidentUsage { get; }

		internal RetailGenerationReceipt(
			long epoch,
			string commitId,
			string sourcePendingId,
			Ra2RuntimeProfileId profile,
			Ra2RuntimeContract contract,
			string filenameCatalogDigest,
			int archiveValidatorVersion,
			int mapValidatorVersion,
			ImmutableArray<RetailGroupReceipt> groups,
			ImmutableArray<RetailMapReceipt> maps,
			MapResidentUsage mapResidentUsage)
		{
			if (epoch < 0)
				throw new ArgumentOutOfRangeException(nameof(epoch));
			if (archiveValidatorVersion <= 0)
				throw new ArgumentOutOfRangeException(nameof(archiveValidatorVersion));
			if (mapValidatorVersion <= 0)
				throw new ArgumentOutOfRangeException(nameof(mapValidatorVersion));
			RetailMetadataValidation.ValidateProfileAndContract(profile, contract);

			Epoch = epoch;
			CommitId = RetailImportPaths.ValidateCommitId(commitId, nameof(commitId));
			SourcePendingId = RetailImportPaths.ValidateOptionalPendingId(
				sourcePendingId, nameof(sourcePendingId));
			Profile = profile;
			Contract = contract;
			FilenameCatalogDigest = RetailImportPaths.ValidateSha256(
				filenameCatalogDigest, nameof(filenameCatalogDigest));
			if (!StringComparer.Ordinal.Equals(FilenameCatalogDigest, contract.FilenameCatalogDigest))
				throw new ArgumentException(
					"The receipt filename catalog does not match its runtime contract.",
					nameof(filenameCatalogDigest));
			ArchiveValidatorVersion = archiveValidatorVersion;
			MapValidatorVersion = mapValidatorVersion;
			Groups = RetailMetadataValidation.CopyAllGroups(groups, nameof(groups));
			Maps = RetailMetadataValidation.CopyMaps(maps, nameof(maps));
			MapResidentUsage = RetailMetadataValidation.ValidateAggregateUsage(
				Maps, mapResidentUsage, nameof(mapResidentUsage));
			RetailMetadataValidation.ValidateContractMapFormats(Contract, Maps, nameof(maps));
			RetailMetadataValidation.ValidatePersistedProfileProjection(
				Profile, Groups, Maps, nameof(groups));
			RetailMetadataValidation.ValidateMapGroupEntries(Groups, Maps, nameof(groups));
			RetailMetadataValidation.ValidateReceiptValidatorVersions(
				Groups, ArchiveValidatorVersion, MapValidatorVersion, nameof(groups));
		}
	}

	public sealed class RetailGenerationImportResult
	{
		public bool Published { get; }
		public bool PendingSealed { get; }
		public string PendingId { get; }
		public RetailReadinessResult ActiveReadiness { get; }
		public RetailImportFailure Failure { get; }

		RetailGenerationImportResult(
			bool published,
			bool pendingSealed,
			string pendingId,
			RetailReadinessResult activeReadiness,
			RetailImportFailure failure)
		{
			if (activeReadiness != null && !activeReadiness.BaseReady)
				throw new ArgumentException(
					"An active readiness authority must describe a usable authenticated generation.",
					nameof(activeReadiness));
			if (published)
			{
				if (pendingSealed || pendingId != null || activeReadiness == null || failure != null)
					throw new ArgumentException("The published import-result tuple is invalid.");
			}
			else if (pendingSealed)
			{
				RetailImportPaths.ValidatePendingId(pendingId, nameof(pendingId));
			}
			else if (pendingId != null || activeReadiness != null || failure == null)
				throw new ArgumentException("The failed import-result tuple is invalid.");

			Published = published;
			PendingSealed = pendingSealed;
			PendingId = pendingId;
			ActiveReadiness = activeReadiness;
			Failure = failure;
		}

		internal static RetailGenerationImportResult PublishedSuccess(RetailReadinessResult activeReadiness) =>
			new(true, false, null, activeReadiness, null);

		internal static RetailGenerationImportResult SealedForNextLaunch(
			string pendingId, RetailReadinessResult activeReadiness = null) =>
			new(false, true, pendingId, activeReadiness, null);

		internal static RetailGenerationImportResult RetainedPendingFailure(
			string pendingId,
			RetailReadinessResult activeReadiness,
			RetailImportFailure failure) =>
			new(false, true, pendingId, activeReadiness,
				failure ?? throw new ArgumentNullException(nameof(failure)));

		internal static RetailGenerationImportResult Failed(RetailImportFailure failure) =>
			new(false, false, null, null, failure ?? throw new ArgumentNullException(nameof(failure)));
	}

	static class RetailMetadataValidation
	{
		internal delegate string PathValidator(string value, string parameterName);

		internal static void ValidateSchemaVersion(int schemaVersion, string parameterName)
		{
			if (schemaVersion != RetailImportMetadataCodec.SchemaVersion)
				throw new ArgumentOutOfRangeException(parameterName);
		}

		internal static string ValidateGenerationIdWithoutKnownEpoch(string value, string parameterName)
		{
			if (value == null || value.Length != 55 || !value.StartsWith("g-", StringComparison.Ordinal) ||
				value[22] != '-' || !long.TryParse(value.AsSpan(2, 20),
					System.Globalization.NumberStyles.None,
					System.Globalization.CultureInfo.InvariantCulture, out var epoch))
				throw new ArgumentException("The generation ID is not canonical.", parameterName);

			return RetailImportPaths.ValidateGenerationId(value, epoch, parameterName);
		}

		internal static ImmutableArray<RetailContentGroup> CopyOrderedGroups(
			ImmutableArray<RetailContentGroup> values,
			string parameterName,
			bool requireNonempty,
			bool requireAll)
		{
			if (values.IsDefault || (requireNonempty && values.IsEmpty))
				throw new ArgumentException("The group collection is uninitialized or empty.", parameterName);
			if (requireAll && values.Length != Enum.GetValues<RetailContentGroup>().Length)
				throw new ArgumentException("The group collection must contain exactly five groups.", parameterName);

			var copy = ImmutableArray.CreateBuilder<RetailContentGroup>(values.Length);
			RetailContentGroup? previous = null;
			foreach (var value in values)
			{
				if (!Enum.IsDefined(typeof(RetailContentGroup), value) ||
					(previous is RetailContentGroup previousValue && previousValue >= value))
					throw new ArgumentException("Groups must be enum-ordered and unique.", parameterName);
				copy.Add(value);
				previous = value;
			}

			if (requireAll && !copy.SequenceEqual(Enum.GetValues<RetailContentGroup>()))
				throw new ArgumentException("The group collection must contain the closed five-key set.", parameterName);
			return copy.MoveToImmutable();
		}

		internal static ImmutableArray<string> CopyOrderedPaths(
			ImmutableArray<string> values,
			string parameterName,
			PathValidator validator,
			bool requireNonempty)
		{
			if (values.IsDefault || (requireNonempty && values.IsEmpty))
				throw new ArgumentException("The path collection is uninitialized or empty.", parameterName);
			var copy = ImmutableArray.CreateBuilder<string>(values.Length);
			string previous = null;
			foreach (var value in values)
			{
				var canonical = validator(value, parameterName);
				if (previous != null && StringComparer.Ordinal.Compare(previous, canonical) >= 0)
					throw new ArgumentException("Paths must be ordinal-ordered and unique.", parameterName);
				copy.Add(canonical);
				previous = canonical;
			}

			return copy.MoveToImmutable();
		}

		internal static ImmutableArray<string> CopyOrderedMapUids(
			ImmutableArray<string> values, string parameterName)
		{
			if (values.IsDefault)
				throw new ArgumentException("The map UID collection is uninitialized.", parameterName);
			var copy = ImmutableArray.CreateBuilder<string>(values.Length);
			string previous = null;
			foreach (var value in values)
			{
				var uid = RetailImportPaths.ValidateMapUid(value, parameterName);
				if (previous != null && StringComparer.Ordinal.Compare(previous, uid) >= 0)
					throw new ArgumentException("Map UIDs must be ordinal-ordered and unique.", parameterName);
				copy.Add(uid);
				previous = uid;
			}

			return copy.MoveToImmutable();
		}

		internal static ImmutableArray<RetailReceiptEntry> CopyReceiptEntries(
			ImmutableArray<RetailReceiptEntry> entries, string parameterName)
		{
			if (entries.IsDefault)
				throw new ArgumentException("The receipt entry collection is uninitialized.", parameterName);
			var copy = ImmutableArray.CreateBuilder<RetailReceiptEntry>(entries.Length);
			string previous = null;
			foreach (var entry in entries)
			{
				if (entry == null || (previous != null && StringComparer.Ordinal.Compare(
					previous, entry.CanonicalRelativePath) >= 0))
					throw new ArgumentException("Receipt entries must be ordinal-ordered and unique.", parameterName);
				copy.Add(entry);
				previous = entry.CanonicalRelativePath;
			}

			return copy.MoveToImmutable();
		}

		internal static void ValidateGroupFailure(
			RetailContentGroup group, RetailImportFailure failure, string parameterName)
		{
			if (failure == null || failure.Group != group)
				throw new ArgumentException("An invalid group must carry its exact group failure.", parameterName);

			var allowed = failure.Code switch
			{
				RetailImportErrorCode.CorruptArchive =>
					(failure.Diagnostic is RetailImportDiagnosticCode.MalformedEnvelope or
						RetailImportDiagnosticCode.ArithmeticOverflow) &&
					(group switch
					{
						RetailContentGroup.Ra2Music => failure.SafeBasename == "theme.mix",
						RetailContentGroup.Yuri => failure.SafeBasename is "langmd.mix" or "ra2md.mix",
						RetailContentGroup.YuriMusic => failure.SafeBasename == "thememd.mix",
						_ => false,
					}),
				RetailImportErrorCode.PartialYuri => group == RetailContentGroup.Yuri &&
					failure.Diagnostic == RetailImportDiagnosticCode.MissingRequiredArchive &&
					failure.SafeBasename is "langmd.mix" or "ra2md.mix",
				RetailImportErrorCode.InvalidAudio => group == RetailContentGroup.Yuri &&
					failure.SafeBasename == "langmd.mix" &&
					failure.Diagnostic is RetailImportDiagnosticCode.MissingRequiredArchive or
						RetailImportDiagnosticCode.MalformedEnvelope or
						RetailImportDiagnosticCode.ArithmeticOverflow,
				_ => false,
			};
			if (!allowed)
				throw new ArgumentException(
					"The group failure is outside the stable persisted failure matrix.", parameterName);
		}

		internal static void ValidateGroupState(
			RetailContentGroup group,
			RetailGroupValidation state,
			RetailEditionRecognition recognition,
			string parameterName)
		{
			var allowed = group switch
			{
				RetailContentGroup.Base => state switch
				{
					RetailGroupValidation.Missing =>
						recognition == RetailEditionRecognition.NotApplicable,
					RetailGroupValidation.Ready =>
						recognition == RetailEditionRecognition.CompatibleUnrecognized,
					_ => false,
				},
				RetailContentGroup.Ra2Music or RetailContentGroup.YuriMusic =>
					(state is RetailGroupValidation.Missing or RetailGroupValidation.Invalid or
						RetailGroupValidation.Unavailable) &&
					recognition == RetailEditionRecognition.NotApplicable,
				RetailContentGroup.Yuri => state switch
				{
					RetailGroupValidation.Missing or RetailGroupValidation.Invalid =>
						recognition == RetailEditionRecognition.NotApplicable,
					RetailGroupValidation.Unavailable or RetailGroupValidation.Ready =>
						recognition == RetailEditionRecognition.CompatibleUnrecognized,
					_ => false,
				},
				RetailContentGroup.Maps => state switch
				{
					RetailGroupValidation.Missing or RetailGroupValidation.Unavailable =>
						recognition == RetailEditionRecognition.NotApplicable,
					RetailGroupValidation.Ready =>
						recognition == RetailEditionRecognition.CompatibleUnrecognized,
					_ => false,
				},
				_ => false,
			};

			if (!allowed)
				throw new ArgumentException(
					"The group state is outside the closed persisted status matrix.", parameterName);
		}

		internal static void ValidateArtifactLogicalName(
			string logicalName, RetailContentGroup group, string parameterName)
		{
			if (!Enum.IsDefined(typeof(RetailContentGroup), group))
				throw new ArgumentOutOfRangeException(nameof(group));
			if (group == RetailContentGroup.Maps)
			{
				RetailImportPaths.ValidateMapUid(logicalName, parameterName);
				return;
			}

			RetailSafeBasename.Validate(logicalName, parameterName);
			if (!RetailCandidatePlan.TryClassify(logicalName, out var canonical, out var classifiedGroup) ||
				!StringComparer.Ordinal.Equals(canonical, logicalName) || classifiedGroup != group)
				throw new ArgumentException("The artifact logical name does not match its group.", parameterName);
		}

		internal static string GenerationRelativePath(string logicalName, RetailContentGroup group)
		{
			ValidateArtifactLogicalName(logicalName, group, nameof(logicalName));
			return group switch
			{
				RetailContentGroup.Base => "base/" + logicalName,
				RetailContentGroup.Ra2Music => "ra2-music/" + logicalName,
				RetailContentGroup.Yuri => "yuri/" + logicalName,
				RetailContentGroup.YuriMusic => "yuri-music/" + logicalName,
				RetailContentGroup.Maps => RetailImportPaths.MapRelativePath(logicalName),
				_ => throw new ArgumentOutOfRangeException(nameof(group)),
			};
		}

		internal static void ValidateAffectedPathProjection(
			ImmutableArray<RetailContentGroup> affectedGroups,
			ImmutableArray<string> affectedRelativePaths,
			string parameterName)
		{
			foreach (var path in affectedRelativePaths)
			{
				var prefix = path[..path.IndexOf('/')];
				var group = prefix switch
				{
					"base" => RetailContentGroup.Base,
					"ra2-music" => RetailContentGroup.Ra2Music,
					"yuri" => RetailContentGroup.Yuri,
					"yuri-music" => RetailContentGroup.YuriMusic,
					"maps" => RetailContentGroup.Maps,
					_ => throw new ArgumentException(
						"The affected path is outside the closed generation namespaces.", parameterName),
				};

				if (!affectedGroups.Contains(group))
					throw new ArgumentException(
						"Every affected path namespace must be included in the affected groups.", parameterName);
			}
		}

		internal static void ValidateReceiptEntryPath(
			string canonicalRelativePath,
			RetailContentGroup group,
			string parameterName)
		{
			var separator = canonicalRelativePath.IndexOf('/');
			if (separator <= 0 || canonicalRelativePath.IndexOf('/', separator + 1) >= 0)
				throw new ArgumentException(
					"A receipt entry must use one closed generation namespace.", parameterName);

			var logicalName = canonicalRelativePath[(separator + 1)..];
			if (group == RetailContentGroup.Maps)
			{
				const string Suffix = ".oramap";
				if (!logicalName.EndsWith(Suffix, StringComparison.Ordinal))
					throw new ArgumentException("A map receipt path must use its UID.", parameterName);
				logicalName = logicalName[..^Suffix.Length];
			}

			RetailImportPaths.ValidateGenerationRelativePath(
				canonicalRelativePath, group, logicalName, parameterName);
		}

		internal static void ValidateProfileAndContract(
			Ra2RuntimeProfileId profile, Ra2RuntimeContract contract)
		{
			if (!Enum.IsDefined(typeof(Ra2RuntimeProfileId), profile))
				throw new ArgumentOutOfRangeException(nameof(profile));
			if (contract == null || contract.ProfileId != profile)
				throw new ArgumentException("The runtime contract does not match the catalog profile.", nameof(contract));
		}

		internal static ImmutableArray<RetailGroupReceipt> CopyAllGroups(
			ImmutableArray<RetailGroupReceipt> groups, string parameterName)
		{
			if (groups.IsDefault || groups.Length != Enum.GetValues<RetailContentGroup>().Length)
				throw new ArgumentException("The receipt must contain exactly five groups.", parameterName);
			var copy = ImmutableArray.CreateBuilder<RetailGroupReceipt>(groups.Length);
			for (var index = 0; index < groups.Length; index++)
			{
				var group = groups[index];
				if (group == null || (int)group.Group != index)
					throw new ArgumentException("Group receipts must contain the closed enum-ordered set.", parameterName);
				copy.Add(group);
			}

			return copy.MoveToImmutable();
		}

		internal static ImmutableArray<RetailMapReceipt> CopyMaps(
			ImmutableArray<RetailMapReceipt> maps, string parameterName)
		{
			if (maps.IsDefault || maps.Length > IosImportEnvelopePolicyV1.ImportedMaps)
				throw new ArgumentException("The map receipt collection is outside V1 bounds.", parameterName);
			var copy = ImmutableArray.CreateBuilder<RetailMapReceipt>(maps.Length);
			string previous = null;
			foreach (var map in maps)
			{
				if (map == null || (previous != null &&
					StringComparer.Ordinal.Compare(previous, map.MapUid) >= 0))
					throw new ArgumentException("Map receipts must be UID-ordered and unique.", parameterName);
				copy.Add(map);
				previous = map.MapUid;
			}

			return copy.MoveToImmutable();
		}

		internal static ImmutableArray<RetailCandidateArtifact> CopyCandidateArtifacts(
			ImmutableArray<RetailCandidateArtifact> artifacts, string parameterName)
		{
			if (artifacts.IsDefault || artifacts.Length > IosImportEnvelopePolicyV1.CandidateFiles)
				throw new ArgumentException("The candidate artifact collection is outside V1 bounds.", parameterName);
			var copy = ImmutableArray.CreateBuilder<RetailCandidateArtifact>(artifacts.Length);
			ValidateArtifacts(artifacts, parameterName,
				artifact => artifact?.Group,
				artifact => artifact?.LogicalName,
				artifact => artifact?.CanonicalStagedRelativePath,
				artifact => copy.Add(artifact));
			return copy.MoveToImmutable();
		}

		internal static ImmutableArray<RetailCatalogArtifact> CopyCatalogArtifacts(
			ImmutableArray<RetailCatalogArtifact> artifacts, string parameterName)
		{
			if (artifacts.IsDefault || artifacts.Length > IosImportEnvelopePolicyV1.CandidateFiles)
				throw new ArgumentException("The catalog artifact collection is outside V1 bounds.", parameterName);
			var copy = ImmutableArray.CreateBuilder<RetailCatalogArtifact>(artifacts.Length);
			ValidateArtifacts(artifacts, parameterName,
				artifact => artifact?.Group,
				artifact => artifact?.LogicalName,
				artifact => artifact?.CanonicalGenerationRelativePath,
				artifact => copy.Add(artifact));
			return copy.MoveToImmutable();
		}

		static void ValidateArtifacts<T>(
			ImmutableArray<T> artifacts,
			string parameterName,
			Func<T, RetailContentGroup?> group,
			Func<T, string> logicalName,
			Func<T, string> path,
			Action<T> add) where T : class
		{
			RetailContentGroup? previousGroup = null;
			string previousName = null;
			var paths = new HashSet<string>(StringComparer.Ordinal);
			foreach (var artifact in artifacts)
			{
				var currentGroup = group(artifact);
				var logical = logicalName(artifact);
				var relativePath = path(artifact);
				if (artifact == null || !currentGroup.HasValue ||
					(previousGroup.HasValue && (previousGroup.Value > currentGroup.Value ||
						(previousGroup.Value == currentGroup.Value &&
							StringComparer.Ordinal.Compare(previousName, logical) >= 0))) ||
					!paths.Add(relativePath))
					throw new ArgumentException(
						"Artifacts must be group/name ordered with unique names and paths.", parameterName);
				add(artifact);
				previousGroup = currentGroup;
				previousName = logical;
			}
		}

		internal static MapResidentUsage ValidateAggregateUsage(
			ImmutableArray<RetailMapReceipt> maps,
			MapResidentUsage usage,
			string parameterName)
		{
			if (usage == null)
				throw new ArgumentNullException(parameterName);
			long metadata = 0;
			long strings = 0;
			var entries = 0;
			foreach (var map in maps)
			{
				metadata = checked(metadata + map.RetainedMetadataBytes);
				strings = checked(strings + map.RetainedStringBytes);
				entries = checked(entries + map.RetainedStringEntries);
			}

			if (usage.MetadataBytes != metadata || usage.StringBytes != strings || usage.StringEntries != entries)
				throw new ArgumentException("Aggregate map usage does not match its map receipts.", parameterName);
			return usage;
		}

		internal static void ValidateArtifactReceiptSet<T>(
			ImmutableArray<T> artifacts,
			ImmutableArray<RetailGroupReceipt> groups,
			string parameterName) where T : class
		{
			var artifactIndex = 0;
			foreach (var group in groups)
			{
				foreach (var entry in group.Entries)
				{
					if (artifactIndex >= artifacts.Length)
						throw new ArgumentException(
							"Artifacts and group receipt entries must be one-to-one.", parameterName);

					var artifact = artifacts[artifactIndex++];
					RetailContentGroup artifactGroup;
					string logicalName;
					long length;
					string sha256;
					switch (artifact)
					{
						case RetailCandidateArtifact candidate:
							artifactGroup = candidate.Group;
							logicalName = candidate.LogicalName;
							length = candidate.Length;
							sha256 = candidate.Sha256;
							break;
						case RetailCatalogArtifact catalog:
							artifactGroup = catalog.Group;
							logicalName = catalog.LogicalName;
							length = catalog.Length;
							sha256 = catalog.Sha256;
							break;
						default:
							throw new ArgumentException("The artifact type is not admitted.", parameterName);
					}

					var expectedPath = GenerationRelativePath(logicalName, artifactGroup);
					if (artifactGroup != group.Group ||
						!StringComparer.Ordinal.Equals(expectedPath, entry.CanonicalRelativePath) ||
						length != entry.Length || !StringComparer.Ordinal.Equals(sha256, entry.Sha256))
						throw new ArgumentException(
							"An artifact does not authenticate its group receipt entry.", parameterName);
				}
			}

			if (artifactIndex != artifacts.Length)
				throw new ArgumentException(
					"Artifacts and group receipt entries must be one-to-one.", parameterName);
		}

		internal static void ValidateContractMapFormats(
			Ra2RuntimeContract contract,
			ImmutableArray<RetailMapReceipt> maps,
			string parameterName)
		{
			if (maps.Any(map => map.MapFormat != contract.MapFormatVersion))
				throw new ArgumentException(
					"Every map format must match the authenticated runtime contract.", parameterName);
		}

		internal static void ValidatePersistedProfileProjection(
			Ra2RuntimeProfileId profile,
			ImmutableArray<RetailGroupReceipt> groups,
			ImmutableArray<RetailMapReceipt> maps,
			string parameterName)
		{
			var @base = groups[(int)RetailContentGroup.Base];
			var ra2Music = groups[(int)RetailContentGroup.Ra2Music];
			var yuri = groups[(int)RetailContentGroup.Yuri];
			var yuriMusic = groups[(int)RetailContentGroup.YuriMusic];
			var mapGroup = groups[(int)RetailContentGroup.Maps];
			if (!IsRequiredReady(@base) || !IsRequiredReady(mapGroup) ||
				!IsOptionalMusic(ra2Music) || !IsOptionalMusic(yuriMusic) ||
				!IsAllowedYuriProjection(yuri, profile))
				throw new ArgumentException(
					"The persisted groups do not match the authenticated runtime profile.", parameterName);

			var hasBaseMap = maps.Any(map =>
				map.CompatibleProfiles.HasFlag(Ra2MapCompatibleProfiles.Base));
			var hasYuriMap = maps.Any(map =>
				map.CompatibleProfiles.HasFlag(Ra2MapCompatibleProfiles.Yuri));
			if (!hasBaseMap || (profile == Ra2RuntimeProfileId.Yuri && !hasYuriMap) ||
				(profile == Ra2RuntimeProfileId.Base && hasYuriMap &&
				yuri.State == RetailGroupValidation.Unavailable))
				throw new ArgumentException(
					"The persisted map evidence does not match the authenticated runtime profile.",
					parameterName);
		}

		static bool IsRequiredReady(RetailGroupReceipt group)
		{
			if (group.State != RetailGroupValidation.Ready ||
				group.Recognition != RetailEditionRecognition.CompatibleUnrecognized ||
				group.Failure != null)
				return false;

			return group.Group switch
			{
				RetailContentGroup.Base => group.Entries.Select(entry => entry.CanonicalRelativePath)
					.SequenceEqual(new[] { "base/language.mix", "base/ra2.mix" }),
				RetailContentGroup.Yuri => group.Entries.Select(entry => entry.CanonicalRelativePath)
					.SequenceEqual(new[] { "yuri/langmd.mix", "yuri/ra2md.mix" }),
				RetailContentGroup.Maps => !group.Entries.IsEmpty,
				_ => false,
			};
		}

		static bool IsOptionalMusic(RetailGroupReceipt group) =>
			(group.State is RetailGroupValidation.Missing or RetailGroupValidation.Invalid or
				RetailGroupValidation.Unavailable) &&
			group.Recognition == RetailEditionRecognition.NotApplicable;

		static bool IsAllowedYuriProjection(
			RetailGroupReceipt group,
			Ra2RuntimeProfileId profile)
		{
			if (profile == Ra2RuntimeProfileId.Yuri)
				return IsRequiredReady(group);

			return group.State switch
			{
				RetailGroupValidation.Missing or RetailGroupValidation.Invalid =>
					group.Recognition == RetailEditionRecognition.NotApplicable,
				RetailGroupValidation.Unavailable =>
					group.Recognition == RetailEditionRecognition.CompatibleUnrecognized,
				_ => false,
			};
		}

		internal static void ValidateMapGroupEntries(
			ImmutableArray<RetailGroupReceipt> groups,
			ImmutableArray<RetailMapReceipt> maps,
			string parameterName)
		{
			var entries = groups[(int)RetailContentGroup.Maps].Entries;
			if (entries.Length != maps.Length)
				throw new ArgumentException(
					"The map group entries and map receipts must be one-to-one.", parameterName);
			for (var index = 0; index < maps.Length; index++)
				if (!StringComparer.Ordinal.Equals(
						entries[index].CanonicalRelativePath, RetailImportPaths.MapRelativePath(maps[index].MapUid)) ||
					entries[index].Length != maps[index].CompressedBytes ||
					!StringComparer.Ordinal.Equals(entries[index].Sha256, maps[index].PackageSha256))
					throw new ArgumentException(
						"The map group entry does not authenticate its map receipt.", parameterName);
		}

		internal static void ValidateReceiptValidatorVersions(
			ImmutableArray<RetailGroupReceipt> groups,
			int archiveValidatorVersion,
			int mapValidatorVersion,
			string parameterName)
		{
			foreach (var group in groups)
			{
				var expected = group.Group == RetailContentGroup.Maps
					? mapValidatorVersion : archiveValidatorVersion;
				if (group.ValidatorVersion != expected)
					throw new ArgumentException(
						"The group receipt validator version is inconsistent.", parameterName);
			}
		}

		internal static RetailOperationResult ValidateJournalReceipt(
			RetailPublishJournal journal,
			RetailGenerationReceipt receipt)
		{
			if (journal == null)
				throw new ArgumentNullException(nameof(journal));
			if (receipt == null)
				throw new ArgumentNullException(nameof(receipt));

			return journal.Epoch == receipt.Epoch &&
				StringComparer.Ordinal.Equals(journal.CommitId, receipt.CommitId) &&
				StringComparer.Ordinal.Equals(journal.SourcePendingId, receipt.SourcePendingId)
				? RetailOperationResult.Success()
				: ReceiptMismatch(RetailImportDiagnosticCode.JournalInvalid);
		}

		internal static RetailOperationResult ValidatePointerReceipt(
			ActiveGenerationPointer pointer,
			RetailGenerationReceipt receipt)
		{
			if (pointer == null)
				throw new ArgumentNullException(nameof(pointer));
			if (receipt == null)
				throw new ArgumentNullException(nameof(receipt));

			return pointer.Epoch == receipt.Epoch &&
				StringComparer.Ordinal.Equals(pointer.CommitId, receipt.CommitId)
				? RetailOperationResult.Success()
				: ReceiptMismatch(RetailImportDiagnosticCode.PointerMismatch);
		}

		internal static RetailOperationResult ValidateCatalogReceipt(
			RetailGenerationCatalog catalog,
			RetailGenerationReceipt receipt)
		{
			if (catalog == null)
				throw new ArgumentNullException(nameof(catalog));
			if (receipt == null)
				throw new ArgumentNullException(nameof(receipt));

			return catalog.Profile == receipt.Profile &&
				ContractsEqual(catalog.Contract, receipt.Contract) &&
				StringComparer.Ordinal.Equals(
					receipt.FilenameCatalogDigest, catalog.Contract.FilenameCatalogDigest) &&
				GroupsEqual(catalog.Groups, receipt.Groups) &&
				MapsEqual(catalog.Maps, receipt.Maps) &&
				UsageEqual(catalog.MapResidentUsage, receipt.MapResidentUsage)
				? RetailOperationResult.Success()
				: ReceiptMismatch(RetailImportDiagnosticCode.ProfileMetadataMismatch);
		}

		static bool ContractsEqual(Ra2RuntimeContract left, Ra2RuntimeContract right) =>
			left.ProfileId == right.ProfileId &&
			StringComparer.Ordinal.Equals(left.ContractDigest, right.ContractDigest) &&
			StringComparer.Ordinal.Equals(left.FilenameCatalogDigest, right.FilenameCatalogDigest) &&
			left.MapFormatVersion == right.MapFormatVersion;

		static bool GroupsEqual(
			ImmutableArray<RetailGroupReceipt> left,
			ImmutableArray<RetailGroupReceipt> right)
		{
			if (left.Length != right.Length)
				return false;
			for (var index = 0; index < left.Length; index++)
			{
				var x = left[index];
				var y = right[index];
				if (x.Group != y.Group || x.State != y.State || x.Recognition != y.Recognition ||
					x.ValidatorVersion != y.ValidatorVersion || !FailuresEqual(x.Failure, y.Failure) ||
					x.Entries.Length != y.Entries.Length)
					return false;
				for (var entryIndex = 0; entryIndex < x.Entries.Length; entryIndex++)
				{
					var a = x.Entries[entryIndex];
					var b = y.Entries[entryIndex];
					if (!StringComparer.Ordinal.Equals(a.CanonicalRelativePath, b.CanonicalRelativePath) ||
						a.Length != b.Length || !StringComparer.Ordinal.Equals(a.Sha256, b.Sha256) ||
						!StringComparer.Ordinal.Equals(a.RecognizedEditionCode, b.RecognizedEditionCode))
						return false;
				}
			}

			return true;
		}

		static bool MapsEqual(
			ImmutableArray<RetailMapReceipt> left,
			ImmutableArray<RetailMapReceipt> right)
		{
			if (left.Length != right.Length)
				return false;
			for (var index = 0; index < left.Length; index++)
			{
				var x = left[index];
				var y = right[index];
				if (!StringComparer.Ordinal.Equals(x.LogicalName, y.LogicalName) ||
					!StringComparer.Ordinal.Equals(x.SourceSha256, y.SourceSha256) ||
					!StringComparer.Ordinal.Equals(x.PackageSha256, y.PackageSha256) ||
					x.CompressedBytes != y.CompressedBytes ||
					!StringComparer.Ordinal.Equals(x.MapUid, y.MapUid) ||
					x.CompatibleProfiles != y.CompatibleProfiles ||
					x.EnvelopeVersion != y.EnvelopeVersion || x.ConverterVersion != y.ConverterVersion ||
					x.MapFormat != y.MapFormat || x.RetainedMetadataBytes != y.RetainedMetadataBytes ||
					x.RetainedStringBytes != y.RetainedStringBytes ||
					x.RetainedStringEntries != y.RetainedStringEntries ||
					!RetailMapResidentUsageMeterV1.EncodeCanonical(x.Metadata, x.MapFormat).AsSpan()
						.SequenceEqual(RetailMapResidentUsageMeterV1.EncodeCanonical(y.Metadata, y.MapFormat)))
					return false;
			}

			return true;
		}

		static bool FailuresEqual(RetailImportFailure left, RetailImportFailure right) =>
			left == null ? right == null : right != null && left.Code == right.Code &&
				left.Group == right.Group && StringComparer.Ordinal.Equals(left.SafeBasename, right.SafeBasename) &&
				left.Diagnostic == right.Diagnostic;

		static bool UsageEqual(MapResidentUsage left, MapResidentUsage right) =>
			left.MetadataBytes == right.MetadataBytes && left.StringBytes == right.StringBytes &&
			left.StringEntries == right.StringEntries;

		static RetailOperationResult ReceiptMismatch(RetailImportDiagnosticCode diagnostic) =>
			RetailOperationResult.Failed(new RetailImportFailure(
				RetailImportErrorCode.ReceiptMismatch, null, null, diagnostic));

		internal static ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> CopyReadinessGroups(
			ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> groups,
			string parameterName)
		{
			if (groups == null || groups.Count != Enum.GetValues<RetailContentGroup>().Length ||
				!Equals(groups.KeyComparer, Comparer<RetailContentGroup>.Default) ||
				!groups.Keys.SequenceEqual(Enum.GetValues<RetailContentGroup>()))
				throw new ArgumentException("Readiness must contain the closed five-key group set.", parameterName);
			var copy = ImmutableSortedDictionary.CreateBuilder<RetailContentGroup, RetailGroupStatus>();
			foreach (var group in Enum.GetValues<RetailContentGroup>())
			{
				var status = groups[group] ?? throw new ArgumentException(
					"Readiness contains a null group status.", parameterName);
				ValidateGroupState(group, status.Validation, status.Recognition, parameterName);
				if (status.Validation != RetailGroupValidation.Invalid &&
					status.Diagnostic != RetailImportDiagnosticCode.None)
					throw new ArgumentException(
						"Only an invalid group may carry a diagnostic.", parameterName);
				copy.Add(group, status);
			}

			return copy.ToImmutable();
		}

		internal static ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure> CopyGroupFailures(
			ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> groups,
			ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure> failures,
			string parameterName)
		{
			if (failures == null)
				throw new ArgumentNullException(parameterName);
			var expected = groups.Where(pair => pair.Value.Validation == RetailGroupValidation.Invalid)
				.Select(pair => pair.Key).ToArray();
			if (!Equals(failures.KeyComparer, Comparer<RetailContentGroup>.Default) ||
				!failures.Keys.SequenceEqual(expected))
				throw new ArgumentException("Invalid group statuses and failures must be one-to-one.", parameterName);

			var copy = ImmutableSortedDictionary.CreateBuilder<RetailContentGroup, RetailImportFailure>();
			foreach (var group in expected)
			{
				var failure = failures[group];
				ValidateGroupFailure(group, failure, parameterName);
				if (groups[group].Diagnostic != failure.Diagnostic)
					throw new ArgumentException("The group failure diagnostic does not match its status.", parameterName);
				copy.Add(group, failure);
			}

			return copy.ToImmutable();
		}

		internal static void ValidateReadinessMatchesCatalog(
			Ra2RuntimeProfileId profile,
			ImmutableArray<RetailGroupReceipt> groups,
			ImmutableArray<RetailMapReceipt> maps,
			RetailReadinessResult readiness)
		{
			var baseCount = maps.Count(map => map.CompatibleProfiles.HasFlag(Ra2MapCompatibleProfiles.Base));
			var yuriCount = maps.Count(map => map.CompatibleProfiles.HasFlag(Ra2MapCompatibleProfiles.Yuri));
			if (readiness.BaseCompatibleMapCount != baseCount || readiness.YuriCompatibleMapCount != yuriCount ||
				maps.Length + readiness.QuarantinedMapCount > IosImportEnvelopePolicyV1.ImportedMaps ||
				readiness.Failure != null || !readiness.BaseReady ||
				(profile == Ra2RuntimeProfileId.Base && readiness.YuriReady) ||
				(profile == Ra2RuntimeProfileId.Yuri && !readiness.YuriReady))
				throw new ArgumentException("Readiness map counts do not match the catalog.", nameof(readiness));
			foreach (var group in groups)
			{
				var status = readiness.Groups[group.Group];
				if (status.Validation != group.State || status.Recognition != group.Recognition ||
					status.Diagnostic != (group.Failure?.Diagnostic ?? RetailImportDiagnosticCode.None) ||
					!readiness.GroupFailures.TryGetValue(group.Group, out var failure) != (group.Failure == null) ||
					!FailuresEqual(group.Failure, failure))
					throw new ArgumentException("Readiness groups do not match their receipts.", nameof(readiness));
			}
		}
	}
}
