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
using System.Collections.Immutable;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using OpenRA.Primitives;

namespace OpenRA.Mods.RA2.Content
{
	static class RetailImportMetadataCodec
	{
		internal const int SchemaVersion = 1;
		internal const long MaximumPayloadLength = 128L << 20;
		internal const int HeaderLength = 17;
		internal const int ChecksumLength = 32;
		const int MaximumEnvelopeLength = checked((int)MaximumPayloadLength + HeaderLength + ChecksumLength);
		static readonly byte[] Magic = { (byte)'N', (byte)'H', (byte)'R', (byte)'I' };
		static readonly UTF8Encoding StrictUtf8 = new(false, true);

		internal static byte[] EncodeEnvelope(
			RetailImportDocumentKind kind, ReadOnlySpan<byte> payload)
		{
			if (!Enum.IsDefined(typeof(RetailImportDocumentKind), kind))
				throw new ArgumentOutOfRangeException(nameof(kind));
			if (payload.Length > MaximumPayloadLength)
				throw new ArgumentOutOfRangeException(nameof(payload));

			var encoded = new byte[checked(HeaderLength + payload.Length + ChecksumLength)];
			Magic.CopyTo(encoded, 0);
			encoded[4] = (byte)kind;
			WriteInt32(encoded, 5, SchemaVersion);
			WriteInt64(encoded, 9, payload.Length);
			payload.CopyTo(encoded.AsSpan(HeaderLength, payload.Length));
			SHA256.HashData(encoded.AsSpan(0, HeaderLength + payload.Length))
				.CopyTo(encoded, HeaderLength + payload.Length);
			return encoded;
		}

		internal static RetailOperationResult<ReadOnlyMemory<byte>> DecodeEnvelope(
			byte[] encoded, RetailImportDocumentKind expectedKind)
		{
			if (!Enum.IsDefined(typeof(RetailImportDocumentKind), expectedKind))
				throw new ArgumentOutOfRangeException(nameof(expectedKind));
			if (encoded == null || encoded.Length < HeaderLength)
				return EnvelopeFailure(RetailImportDiagnosticCode.LengthMismatch);
			if (!encoded.AsSpan(0, Magic.Length).SequenceEqual(Magic))
				return EnvelopeFailure(RetailImportDiagnosticCode.MalformedEnvelope);

			var schema = ReadInt32(encoded, 5);
			if (schema > SchemaVersion)
				return RetailOperationResult<ReadOnlyMemory<byte>>.Failed(
					Failure(RetailImportErrorCode.UpdateRequired, RetailImportDiagnosticCode.None));
			if (schema != SchemaVersion)
				return EnvelopeFailure(RetailImportDiagnosticCode.MalformedEnvelope);

			var rawKind = encoded[4];
			if (!Enum.IsDefined(typeof(RetailImportDocumentKind), rawKind) || rawKind != (byte)expectedKind)
				return EnvelopeFailure(RetailImportDiagnosticCode.MalformedEnvelope);

			var payloadLength = ReadInt64(encoded, 9);
			if (payloadLength < 0)
				return EnvelopeFailure(RetailImportDiagnosticCode.ArithmeticOverflow);
			if (payloadLength > MaximumPayloadLength || payloadLength > int.MaxValue)
				return EnvelopeFailure(RetailImportDiagnosticCode.InsufficientCapacity);

			long expectedLength;
			try
			{
				expectedLength = checked(HeaderLength + payloadLength + ChecksumLength);
			}
			catch (OverflowException)
			{
				return EnvelopeFailure(RetailImportDiagnosticCode.ArithmeticOverflow);
			}

			if (expectedLength != encoded.LongLength || expectedLength > MaximumEnvelopeLength)
				return EnvelopeFailure(RetailImportDiagnosticCode.LengthMismatch);

			var payloadCount = checked((int)payloadLength);
			var checksumOffset = HeaderLength + payloadCount;
			var expectedChecksum = SHA256.HashData(encoded.AsSpan(0, checksumOffset));
			if (!CryptographicOperations.FixedTimeEquals(
				expectedChecksum, encoded.AsSpan(checksumOffset, ChecksumLength)))
				return EnvelopeFailure(RetailImportDiagnosticCode.HashMismatch);

			return RetailOperationResult<ReadOnlyMemory<byte>>.Success(
				new ReadOnlyMemory<byte>(encoded, HeaderLength, payloadCount));
		}

		internal static byte[] Encode(ActiveGenerationPointer value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));
			using var writer = new PayloadWriter();
			writer.WriteInt32(value.SchemaVersion);
			writer.WriteRequiredString(value.GenerationId);
			writer.WriteInt64(value.Epoch);
			writer.WriteRequiredString(value.CommitId);
			writer.WriteRequiredString(value.CatalogSha256);
			writer.WriteRequiredString(value.ReceiptsSha256);
			return EncodeEnvelope(RetailImportDocumentKind.ActiveGenerationPointer, writer.ToArray());
		}

		internal static byte[] Encode(RetailPublishJournal value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));
			using var writer = new PayloadWriter();
			writer.WriteInt32(value.SchemaVersion);
			writer.WriteInt32((int)value.State);
			writer.WriteInt64(value.Epoch);
			writer.WriteBoolean(value.HasPreviousGeneration);
			writer.WriteOptionalString(value.PreviousGenerationId);
			writer.WriteRequiredString(value.ProposedGenerationId);
			writer.WriteRequiredString(value.CommitId);
			writer.WriteOptionalString(value.SourcePendingId);
			writer.WriteCount(value.AffectedGroups.Length);
			foreach (var group in value.AffectedGroups)
				writer.WriteInt32((int)group);
			writer.WriteCount(value.AffectedRelativePaths.Length);
			foreach (var path in value.AffectedRelativePaths)
				writer.WriteRequiredString(path);
			return EncodeEnvelope(RetailImportDocumentKind.PublishJournal, writer.ToArray());
		}

		internal static byte[] Encode(RetailGenerationCatalog value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));
			using var writer = new PayloadWriter();
			writer.WriteInt32((int)value.Profile);
			WriteContract(writer, value.Contract);
			writer.WriteCount(value.Artifacts.Length);
			foreach (var artifact in value.Artifacts)
				WriteCatalogArtifact(writer, artifact);
			WriteGroups(writer, value.Groups);
			WriteMaps(writer, value.Maps);
			WriteUsage(writer, value.MapResidentUsage);
			return EncodeEnvelope(RetailImportDocumentKind.GenerationCatalog, writer.ToArray());
		}

		internal static byte[] Encode(RetailGenerationReceipt value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));
			using var writer = new PayloadWriter();
			writer.WriteInt64(value.Epoch);
			writer.WriteRequiredString(value.CommitId);
			writer.WriteOptionalString(value.SourcePendingId);
			writer.WriteInt32((int)value.Profile);
			WriteContract(writer, value.Contract);
			writer.WriteRequiredString(value.FilenameCatalogDigest);
			writer.WriteInt32(value.ArchiveValidatorVersion);
			writer.WriteInt32(value.MapValidatorVersion);
			WriteGroups(writer, value.Groups);
			WriteMaps(writer, value.Maps);
			WriteUsage(writer, value.MapResidentUsage);
			return EncodeEnvelope(RetailImportDocumentKind.GenerationReceipt, writer.ToArray());
		}

		internal static byte[] Encode(RetailPendingSeal value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));
			using var writer = new PayloadWriter();
			writer.WriteInt32(value.SchemaVersion);
			writer.WriteRequiredString(value.PendingId);
			writer.WriteInt64(value.SealedAtUtc.Ticks);
			writer.WriteCount(value.Artifacts.Length);
			foreach (var artifact in value.Artifacts)
			{
				writer.WriteInt32(artifact.Ordinal);
				writer.WriteRequiredString(artifact.SafeBasename);
				writer.WriteRequiredString(artifact.CanonicalStagedRelativePath);
				writer.WriteInt32((int)artifact.Group);
				writer.WriteInt64(artifact.Length);
				writer.WriteRequiredString(artifact.Sha256);
			}

			writer.WriteInt64(value.TotalBytes);
			return EncodeEnvelope(RetailImportDocumentKind.PendingSeal, writer.ToArray());
		}

		internal static RetailOperationResult<ActiveGenerationPointer> DecodeActiveGenerationPointer(
			byte[] encoded) => DecodeDocument(encoded,
				RetailImportDocumentKind.ActiveGenerationPointer,
				reader => new ActiveGenerationPointer(
					reader.ReadInt32(), reader.ReadRequiredString(), reader.ReadInt64(),
					reader.ReadRequiredString(), reader.ReadRequiredString(), reader.ReadRequiredString()),
				RetailImportErrorCode.ReceiptMismatch);

		internal static RetailOperationResult<RetailPublishJournal> DecodeRetailPublishJournal(
			byte[] encoded) => DecodeDocument(encoded,
				RetailImportDocumentKind.PublishJournal, ReadJournal,
				RetailImportErrorCode.CorruptJournal);

		internal static RetailOperationResult<RetailGenerationCatalog> DecodeRetailGenerationCatalog(
			byte[] encoded) => DecodeDocument(encoded,
				RetailImportDocumentKind.GenerationCatalog, ReadGenerationCatalog,
				RetailImportErrorCode.ReceiptMismatch);

		internal static RetailOperationResult<RetailGenerationReceipt> DecodeRetailGenerationReceipt(
			byte[] encoded) => DecodeDocument(encoded,
				RetailImportDocumentKind.GenerationReceipt, ReadGenerationReceipt,
				RetailImportErrorCode.ReceiptMismatch);

		internal static RetailOperationResult<RetailPendingSeal> DecodeRetailPendingSeal(
			byte[] encoded) => DecodeDocument(encoded,
				RetailImportDocumentKind.PendingSeal, ReadPendingSeal,
				RetailImportErrorCode.ReceiptMismatch);

		static RetailPublishJournal ReadJournal(PayloadReader reader)
		{
			var schema = reader.ReadInt32();
			var state = (RetailPublishState)reader.ReadInt32();
			var epoch = reader.ReadInt64();
			var hasPrevious = reader.ReadBoolean();
			var previous = reader.ReadOptionalString();
			var proposed = reader.ReadRequiredString();
			var commit = reader.ReadRequiredString();
			var pending = reader.ReadOptionalString();
			var groups = ImmutableArray.CreateBuilder<RetailContentGroup>(reader.ReadCount(5));
			while (groups.Count < groups.Capacity)
				groups.Add((RetailContentGroup)reader.ReadInt32());
			var paths = ImmutableArray.CreateBuilder<string>(
				reader.ReadCount(IosImportEnvelopePolicyV1.CandidateFiles));
			while (paths.Count < paths.Capacity)
				paths.Add(reader.ReadRequiredString());

			return new RetailPublishJournal(schema, state, epoch, hasPrevious, previous,
				proposed, commit, pending, groups.MoveToImmutable(), paths.MoveToImmutable());
		}

		static RetailPendingSeal ReadPendingSeal(PayloadReader reader)
		{
			var schema = reader.ReadInt32();
			var pending = reader.ReadRequiredString();
			var sealedAt = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
			var artifacts = ImmutableArray.CreateBuilder<RetailPendingArtifact>(
				reader.ReadCount(IosImportEnvelopePolicyV1.CandidateFiles));
			while (artifacts.Count < artifacts.Capacity)
				artifacts.Add(new RetailPendingArtifact(
					reader.ReadInt32(), reader.ReadRequiredString(), reader.ReadRequiredString(),
					(RetailContentGroup)reader.ReadInt32(), reader.ReadInt64(), reader.ReadRequiredString()));

			return new RetailPendingSeal(schema, pending, sealedAt,
				artifacts.MoveToImmutable(), reader.ReadInt64());
		}

		static RetailGenerationCatalog ReadGenerationCatalog(PayloadReader reader)
		{
			var profile = (Ra2RuntimeProfileId)reader.ReadInt32();
			var contract = ReadContract(reader);
			var artifacts = ImmutableArray.CreateBuilder<RetailCatalogArtifact>(
				reader.ReadCount(IosImportEnvelopePolicyV1.CandidateFiles));
			while (artifacts.Count < artifacts.Capacity)
				artifacts.Add(ReadCatalogArtifact(reader));

			return new RetailGenerationCatalog(profile, contract, artifacts.MoveToImmutable(),
				ReadGroups(reader), ReadMaps(reader), ReadUsage(reader));
		}

		static RetailGenerationReceipt ReadGenerationReceipt(PayloadReader reader)
		{
			var epoch = reader.ReadInt64();
			var commit = reader.ReadRequiredString();
			var pending = reader.ReadOptionalString();
			var profile = (Ra2RuntimeProfileId)reader.ReadInt32();
			var contract = ReadContract(reader);
			var catalogDigest = reader.ReadRequiredString();
			var archiveVersion = reader.ReadInt32();
			var mapVersion = reader.ReadInt32();
			return new RetailGenerationReceipt(epoch, commit, pending, profile, contract,
				catalogDigest, archiveVersion, mapVersion, ReadGroups(reader), ReadMaps(reader),
				ReadUsage(reader));
		}

		static void WriteContract(PayloadWriter writer, Ra2RuntimeContract value)
		{
			writer.WriteInt32((int)value.ProfileId);
			writer.WriteRequiredString(value.ContractDigest);
			writer.WriteRequiredString(value.FilenameCatalogDigest);
			writer.WriteInt32(value.MapFormatVersion);
		}

		static Ra2RuntimeContract ReadContract(PayloadReader reader) => new(
			(Ra2RuntimeProfileId)reader.ReadInt32(), reader.ReadRequiredString(),
			reader.ReadRequiredString(), reader.ReadInt32());

		static void WriteCatalogArtifact(PayloadWriter writer, RetailCatalogArtifact value)
		{
			writer.WriteRequiredString(value.LogicalName);
			writer.WriteRequiredString(value.CanonicalGenerationRelativePath);
			writer.WriteInt32((int)value.Group);
			writer.WriteInt64(value.Length);
			writer.WriteRequiredString(value.Sha256);
		}

		static RetailCatalogArtifact ReadCatalogArtifact(PayloadReader reader) => new(
			reader.ReadRequiredString(), reader.ReadRequiredString(),
			(RetailContentGroup)reader.ReadInt32(), reader.ReadInt64(), reader.ReadRequiredString());

		static void WriteGroups(PayloadWriter writer, ImmutableArray<RetailGroupReceipt> values)
		{
			writer.WriteCount(values.Length);
			foreach (var value in values)
			{
				writer.WriteInt32((int)value.Group);
				writer.WriteInt32((int)value.State);
				writer.WriteInt32((int)value.Recognition);
				WriteFailure(writer, value.Failure);
				writer.WriteCount(value.Entries.Length);
				foreach (var entry in value.Entries)
				{
					writer.WriteRequiredString(entry.CanonicalRelativePath);
					writer.WriteInt64(entry.Length);
					writer.WriteRequiredString(entry.Sha256);
					writer.WriteOptionalString(entry.RecognizedEditionCode);
				}

				writer.WriteInt32(value.ValidatorVersion);
			}
		}

		static ImmutableArray<RetailGroupReceipt> ReadGroups(PayloadReader reader)
		{
			var groups = ImmutableArray.CreateBuilder<RetailGroupReceipt>(reader.ReadCount(5));
			while (groups.Count < groups.Capacity)
			{
				var group = (RetailContentGroup)reader.ReadInt32();
				var state = (RetailGroupValidation)reader.ReadInt32();
				var recognition = (RetailEditionRecognition)reader.ReadInt32();
				var failure = ReadFailure(reader);
				var entries = ImmutableArray.CreateBuilder<RetailReceiptEntry>(
					reader.ReadCount(IosImportEnvelopePolicyV1.CandidateFiles));
				while (entries.Count < entries.Capacity)
					entries.Add(new RetailReceiptEntry(
						reader.ReadRequiredString(), reader.ReadInt64(),
						reader.ReadRequiredString(), reader.ReadOptionalString()));
				groups.Add(new RetailGroupReceipt(group, state, recognition, failure,
					entries.MoveToImmutable(), reader.ReadInt32()));
			}

			return groups.MoveToImmutable();
		}

		static void WriteFailure(PayloadWriter writer, RetailImportFailure value)
		{
			writer.WriteBoolean(value != null);
			if (value == null)
				return;
			writer.WriteInt32((int)value.Code);
			writer.WriteBoolean(value.Group.HasValue);
			if (value.Group.HasValue)
				writer.WriteInt32((int)value.Group.Value);
			writer.WriteOptionalString(value.SafeBasename);
			writer.WriteInt32((int)value.Diagnostic);
		}

		static RetailImportFailure ReadFailure(PayloadReader reader)
		{
			if (!reader.ReadBoolean())
				return null;
			var code = (RetailImportErrorCode)reader.ReadInt32();
			var group = reader.ReadBoolean() ? (RetailContentGroup?)reader.ReadInt32() : null;
			return new RetailImportFailure(
				code, group, reader.ReadOptionalString(), (RetailImportDiagnosticCode)reader.ReadInt32());
		}

		static void WriteUsage(PayloadWriter writer, MapResidentUsage value)
		{
			writer.WriteInt64(value.MetadataBytes);
			writer.WriteInt64(value.StringBytes);
			writer.WriteInt32(value.StringEntries);
		}

		static MapResidentUsage ReadUsage(PayloadReader reader) =>
			new(reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt32());

		static void WriteMaps(PayloadWriter writer, ImmutableArray<RetailMapReceipt> values)
		{
			writer.WriteCount(values.Length);
			foreach (var value in values)
			{
				writer.WriteRequiredString(value.LogicalName);
				writer.WriteRequiredString(value.SourceSha256);
				writer.WriteRequiredString(value.PackageSha256);
				writer.WriteInt64(value.CompressedBytes);
				writer.WriteRequiredString(value.MapUid);
				writer.WriteInt32((int)value.CompatibleProfiles);
				writer.WriteBytes(RetailMapResidentUsageMeterV1.EncodeCanonical(
					value.Metadata, value.MapFormat));
				writer.WriteInt32(value.EnvelopeVersion);
				writer.WriteInt32(value.ConverterVersion);
				writer.WriteInt32(value.MapFormat);
				writer.WriteInt64(value.RetainedMetadataBytes);
				writer.WriteInt64(value.RetainedStringBytes);
				writer.WriteInt32(value.RetainedStringEntries);
			}
		}

		static ImmutableArray<RetailMapReceipt> ReadMaps(PayloadReader reader)
		{
			var maps = ImmutableArray.CreateBuilder<RetailMapReceipt>(
				reader.ReadCount(IosImportEnvelopePolicyV1.ImportedMaps));
			while (maps.Count < maps.Capacity)
			{
				var logicalName = reader.ReadRequiredString();
				var sourceSha256 = reader.ReadRequiredString();
				var packageSha256 = reader.ReadRequiredString();
				var compressedBytes = reader.ReadInt64();
				var mapUid = reader.ReadRequiredString();
				var compatibleProfiles = (Ra2MapCompatibleProfiles)reader.ReadInt32();
				var projection = reader.ReadBytes(checked((int)IosImportEnvelopePolicyV1.ResidentMapMetadataBytes));
				var metadata = ReadMapMetadata(projection, out var projectedMapFormat);
				var envelopeVersion = reader.ReadInt32();
				var converterVersion = reader.ReadInt32();
				var mapFormat = reader.ReadInt32();
				if (mapFormat != projectedMapFormat)
					throw new InvalidDataException("The map format disagrees with its authenticated projection.");

				maps.Add(new RetailMapReceipt(
					logicalName, sourceSha256, packageSha256, compressedBytes, mapUid,
					compatibleProfiles, metadata, envelopeVersion, converterVersion, mapFormat,
					reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt32()));
			}

			return maps.MoveToImmutable();
		}

		static ValidatedMapMetadata ReadMapMetadata(byte[] encoded, out int mapFormat)
		{
			var reader = new MapMetadataReader(encoded);
			reader.RequireMagicAndVersion();
			mapFormat = reader.ReadInt32();
			var title = reader.ReadRequiredString();
			var author = reader.ReadRequiredString();
			var categories = reader.ReadStrings(IosImportEnvelopePolicyV1.RemoteCategories);
			var tileset = reader.ReadRequiredString();
			var gridType = (MapGridType)reader.ReadInt32();
			var mapSize = new int2(reader.ReadInt32(), reader.ReadInt32());
			var bounds = new Rectangle(
				reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
			var players = ImmutableArray.CreateBuilder<ValidatedMapPlayerMetadata>(
				reader.ReadCount(IosImportEnvelopePolicyV1.MapPlayerDefinitions));
			while (players.Count < players.Capacity)
				players.Add(ReadPlayer(reader));
			var spawns = ImmutableArray.CreateBuilder<CPos>(
				reader.ReadCount(IosImportEnvelopePolicyV1.MapSpawns));
			while (spawns.Count < spawns.Capacity)
				spawns.Add(reader.ReadCell());
			reader.RequireEnd();

			var metadata = ValidatedMapMetadata.CreateValidated(
				title, author, categories, tileset, gridType, mapSize, bounds,
				players.MoveToImmutable(), spawns.MoveToImmutable());
			var canonical = RetailMapResidentUsageMeterV1.EncodeCanonical(metadata, mapFormat);
			if (!canonical.AsSpan().SequenceEqual(encoded))
				throw new InvalidDataException("The map metadata projection is not canonical.");

			return metadata;
		}

		static ValidatedMapPlayerMetadata ReadPlayer(MapMetadataReader reader)
		{
			var name = reader.ReadRequiredString();
			var palette = reader.ReadOptionalString();
			var bot = reader.ReadOptionalString();
			var startingUnitsClass = reader.ReadOptionalString();
			var allowBots = reader.ReadBoolean();
			var playable = reader.ReadBoolean();
			var required = reader.ReadBoolean();
			var ownsWorld = reader.ReadBoolean();
			var spectating = reader.ReadBoolean();
			var nonCombatant = reader.ReadBoolean();
			var lockFaction = reader.ReadBoolean();
			var faction = reader.ReadOptionalString();
			var lockColor = reader.ReadBoolean();
			var red = reader.ReadByte();
			var green = reader.ReadByte();
			var blue = reader.ReadByte();
			var alpha = reader.ReadByte();
			var homeLocation = reader.ReadCell();
			var lockSpawn = reader.ReadBoolean();
			var spawn = reader.ReadInt32();
			var lockTeam = reader.ReadBoolean();
			var team = reader.ReadInt32();
			var lockHandicap = reader.ReadBoolean();
			var handicap = reader.ReadInt32();
			var allies = reader.ReadStrings(IosImportEnvelopePolicyV1.MapPlayerDefinitions);
			var enemies = reader.ReadStrings(IosImportEnvelopePolicyV1.MapPlayerDefinitions);

			return ValidatedMapPlayerMetadata.CreateValidated(
				name, palette, bot, startingUnitsClass,
				allowBots, playable, required, ownsWorld, spectating, nonCombatant,
				lockFaction, faction, lockColor, Color.FromArgb(alpha, red, green, blue),
				homeLocation, lockSpawn, spawn, lockTeam, team, lockHandicap, handicap,
				allies, enemies);
		}

		static RetailOperationResult<T> DecodeDocument<T>(
			byte[] encoded,
			RetailImportDocumentKind kind,
			Func<PayloadReader, T> read,
			RetailImportErrorCode corruptCode) where T : class
		{
			var envelope = DecodeEnvelope(encoded, kind);
			if (!envelope.Succeeded)
			{
				if (envelope.Failure.Code == RetailImportErrorCode.UpdateRequired)
					return RetailOperationResult<T>.Failed(envelope.Failure);

				return RetailOperationResult<T>.Failed(
					Failure(corruptCode, envelope.Failure.Diagnostic));
			}

			try
			{
				var reader = new PayloadReader(envelope.Value);
				var value = read(reader);
				reader.RequireEnd();
				return RetailOperationResult<T>.Success(value);
			}
			catch (Exception exception) when (exception is ArgumentException or ArithmeticException or
				DecoderFallbackException or EndOfStreamException or InvalidDataException)
			{
				return RetailOperationResult<T>.Failed(
					Failure(corruptCode, RetailImportDiagnosticCode.MalformedEnvelope));
			}
		}

		static RetailOperationResult<ReadOnlyMemory<byte>> EnvelopeFailure(
			RetailImportDiagnosticCode diagnostic) =>
			RetailOperationResult<ReadOnlyMemory<byte>>.Failed(
				Failure(RetailImportErrorCode.ReceiptMismatch, diagnostic));

		static RetailImportFailure Failure(
			RetailImportErrorCode code, RetailImportDiagnosticCode diagnostic) =>
			new(code, null, null, diagnostic);

		static void WriteInt32(byte[] destination, int offset, int value)
		{
			var bits = unchecked((uint)value);
			destination[offset] = (byte)bits;
			destination[offset + 1] = (byte)(bits >> 8);
			destination[offset + 2] = (byte)(bits >> 16);
			destination[offset + 3] = (byte)(bits >> 24);
		}

		static void WriteInt64(byte[] destination, int offset, long value)
		{
			var bits = unchecked((ulong)value);
			for (var index = 0; index < 8; index++)
				destination[offset + index] = (byte)(bits >> (8 * index));
		}

		static int ReadInt32(byte[] source, int offset) => unchecked(
			source[offset] |
			source[offset + 1] << 8 |
			source[offset + 2] << 16 |
			source[offset + 3] << 24);

		static long ReadInt64(byte[] source, int offset)
		{
			ulong value = 0;
			for (var index = 0; index < 8; index++)
				value |= (ulong)source[offset + index] << (8 * index);
			return unchecked((long)value);
		}

		sealed class PayloadWriter : IDisposable
		{
			readonly MemoryStream stream = new();

			internal void WriteBoolean(bool value) => stream.WriteByte(value ? (byte)1 : (byte)0);

			internal void WriteCount(int count)
			{
				if (count < 0)
					throw new ArgumentOutOfRangeException(nameof(count));
				WriteInt32(count);
			}

			internal void WriteInt32(int value)
			{
				var bytes = new byte[4];
				RetailImportMetadataCodec.WriteInt32(bytes, 0, value);
				stream.Write(bytes, 0, bytes.Length);
			}

			internal void WriteInt64(long value)
			{
				var bytes = new byte[8];
				RetailImportMetadataCodec.WriteInt64(bytes, 0, value);
				stream.Write(bytes, 0, bytes.Length);
			}

			internal void WriteBytes(byte[] value)
			{
				if (value == null)
					throw new ArgumentNullException(nameof(value));
				WriteCount(value.Length);
				stream.Write(value, 0, value.Length);
			}

			internal void WriteOptionalString(string value)
			{
				if (value == null)
				{
					stream.WriteByte(0);
					return;
				}

				var bytes = StrictUtf8.GetBytes(value);
				if (bytes.Length == 0)
					throw new ArgumentException("Present metadata strings cannot be empty.", nameof(value));
				stream.WriteByte(1);
				WriteInt32(bytes.Length);
				stream.Write(bytes, 0, bytes.Length);
			}

			internal void WriteRequiredString(string value)
			{
				if (value == null)
					throw new ArgumentNullException(nameof(value));
				WriteOptionalString(value);
			}

			internal byte[] ToArray() => stream.ToArray();
			public void Dispose() => stream.Dispose();
		}

		sealed class PayloadReader
		{
			readonly ReadOnlyMemory<byte> value;
			int offset;

			internal PayloadReader(ReadOnlyMemory<byte> value)
			{
				this.value = value;
			}

			internal int ReadInt32()
			{
				Require(4);
				var span = value.Span;
				var result = unchecked(span[offset] |
					span[offset + 1] << 8 |
					span[offset + 2] << 16 |
					span[offset + 3] << 24);
				offset += 4;
				return result;
			}

			internal bool ReadBoolean()
			{
				Require(1);
				return value.Span[offset++] switch
				{
					0 => false,
					1 => true,
					_ => throw new InvalidDataException("A boolean must be encoded as zero or one."),
				};
			}

			internal int ReadCount(int maximum)
			{
				var count = ReadInt32();
				if (count < 0 || count > maximum)
					throw new InvalidDataException("A collection count is outside its closed bound.");
				return count;
			}

			internal long ReadInt64()
			{
				Require(8);
				var span = value.Span;
				ulong result = 0;
				for (var index = 0; index < 8; index++)
					result |= (ulong)span[offset + index] << (8 * index);
				offset += 8;
				return unchecked((long)result);
			}

			internal string ReadOptionalString()
			{
				Require(1);
				var presence = value.Span[offset++];
				if (presence == 0)
					return null;
				if (presence != 1)
					throw new InvalidDataException("A string presence marker is not canonical.");

				var length = ReadInt32();
				if (length <= 0 || length > MaximumPayloadLength)
					throw new InvalidDataException("A string byte length is outside V1 bounds.");
				Require(length);
				var result = StrictUtf8.GetString(value.Span.Slice(offset, length));
				offset += length;
				return result;
			}

			internal string ReadRequiredString() => ReadOptionalString() ??
				throw new InvalidDataException("A required metadata string is absent.");

			internal byte[] ReadBytes(int maximum)
			{
				var length = ReadCount(maximum);
				Require(length);
				var result = value.Slice(offset, length).ToArray();
				offset += length;
				return result;
			}

			internal void RequireEnd()
			{
				if (offset != value.Length)
					throw new InvalidDataException("The metadata payload has trailing bytes.");
			}

			void Require(int count)
			{
				if (count < 0 || count > value.Length - offset)
					throw new EndOfStreamException("The metadata payload is truncated.");
			}
		}

		sealed class MapMetadataReader
		{
			readonly byte[] value;
			int offset;

			internal MapMetadataReader(byte[] value)
			{
				this.value = value ?? throw new ArgumentNullException(nameof(value));
			}

			internal void RequireMagicAndVersion()
			{
				Require(5);
				if (value[offset++] != (byte)'N' || value[offset++] != (byte)'H' ||
					value[offset++] != (byte)'R' || value[offset++] != (byte)'M' ||
					value[offset++] != RetailMapResidentUsageMeterV1.SchemaVersion)
					throw new InvalidDataException("The map metadata projection header is invalid.");
			}

			internal byte ReadByte()
			{
				Require(1);
				return value[offset++];
			}

			internal bool ReadBoolean() => ReadByte() switch
			{
				0 => false,
				1 => true,
				_ => throw new InvalidDataException("A map boolean must be zero or one."),
			};

			internal int ReadInt32()
			{
				Require(4);
				var result = RetailImportMetadataCodec.ReadInt32(value, offset);
				offset += 4;
				return result;
			}

			internal int ReadCount(int maximum)
			{
				var count = ReadInt32();
				if (count < 0 || count > maximum)
					throw new InvalidDataException("A map collection count is outside its closed bound.");
				return count;
			}

			internal string ReadOptionalString()
			{
				var presence = ReadByte();
				if (presence == 0)
					return null;
				if (presence != 1)
					throw new InvalidDataException("A map string presence marker is not canonical.");

				var length = ReadCount(IosImportEnvelopePolicyV1.MapYamlScalarBytes);
				if (length == 0)
					throw new InvalidDataException("A present map string cannot be empty.");
				Require(length);
				var result = StrictUtf8.GetString(value, offset, length);
				offset += length;
				return result;
			}

			internal string ReadRequiredString() => ReadOptionalString() ??
				throw new InvalidDataException("A required map string is absent.");

			internal ImmutableArray<string> ReadStrings(int maximum)
			{
				var result = ImmutableArray.CreateBuilder<string>(ReadCount(maximum));
				while (result.Count < result.Capacity)
					result.Add(ReadRequiredString());
				return result.MoveToImmutable();
			}

			internal CPos ReadCell()
			{
				var x = ReadInt32();
				var y = ReadInt32();
				var layer = ReadByte();
				var result = new CPos(x, y, layer);
				if (result.X != x || result.Y != y || result.Layer != layer)
					throw new InvalidDataException("A map cell cannot be represented canonically.");
				return result;
			}

			internal void RequireEnd()
			{
				if (offset != value.Length)
					throw new InvalidDataException("The map metadata projection has trailing bytes.");
			}

			void Require(int count)
			{
				if (count < 0 || count > value.Length - offset)
					throw new EndOfStreamException("The map metadata projection is truncated.");
			}
		}
	}
}
