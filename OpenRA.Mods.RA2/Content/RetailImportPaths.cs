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
using System.Globalization;
using System.IO;
using System.Text;

namespace OpenRA.Mods.RA2.Content
{
	enum RetailImportV1PathKind
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

	static class RetailImportPaths
	{
		const string ProcessLockName = "import.lock";
		const string ActivePointerName = "active.ptr";
		const string PublishJournalName = "publish.journal";
		const string GenerationsDirectoryName = "generations";
		const string QuarantineDirectoryName = "quarantine";
		const string PendingDirectoryName = "pending";
		const string PendingItemsDirectoryName = "items";
		const string ContentDirectoryName = "content";
		const string CatalogName = "catalog.bin";
		const string ReceiptsName = "receipts.bin";
		const string SealName = "seal.bin";
		const string ConsumeMarkerName = "consume.marker";
		const string LegacyMigrationMarkerName = "legacy-migration.marker";
		const int GuidTokenLength = 32;
		const int GenerationEpochLength = 20;
		const int Sha256Length = 64;
		const int MapUidLength = 40;
		const int MaximumRelativePathBytes = 4096;
		const int MaximumComponentBytes = 255;
		static readonly UTF8Encoding StrictUtf8 = new(false, true);

		static readonly object IssuerKey = new();
		static readonly V1PathToken.Issuer TokenIssuer = V1PathToken.Issuer.Create(IssuerKey);

		internal sealed class V1PathToken
		{
			readonly string generationId;
			readonly long epoch;
			readonly Guid nonce;
			readonly string canonicalRelativePath;

			private V1PathToken(RetailImportV1PathKind kind, string generationId,
				long epoch, Guid nonce, string canonicalRelativePath)
			{
				Kind = kind;
				this.generationId = generationId;
				this.epoch = epoch;
				this.nonce = nonce;
				this.canonicalRelativePath = canonicalRelativePath;
			}

			internal RetailImportV1PathKind Kind { get; }

			internal bool Matches(V1PathToken other) => other != null &&
				Kind == other.Kind && epoch == other.epoch && nonce == other.nonce &&
				StringComparer.Ordinal.Equals(generationId, other.generationId) &&
				StringComparer.Ordinal.Equals(canonicalRelativePath, other.canonicalRelativePath);

			// The canonical path is intentionally not projected from a token.  Identity
			// consumers get only the fields which are already part of the token's type.
			internal bool TryGetGenerationIdentity(out string value, out long valueEpoch)
			{
				value = generationId;
				valueEpoch = epoch;
				return generationId != null && epoch >= 0;
			}

			internal bool TryGetPendingIdentity(out Guid valueNonce)
			{
				valueNonce = nonce;
				return nonce != Guid.Empty;
			}

			internal string ResolveFor(DurableAdapterResolver resolver)
			{
				if (resolver == null)
					throw new ArgumentNullException(nameof(resolver));
				return canonicalRelativePath;
			}

			internal sealed class Issuer
			{
				readonly object key;
				Issuer(object key) { this.key = key; }
				internal static Issuer Create(object key) =>
					ReferenceEquals(key, IssuerKey) ? new Issuer(key) :
					throw new InvalidOperationException("Only RetailImportPaths may mint path tokens.");
				internal V1PathToken Issue(RetailImportV1PathKind kind, string generationId,
					long epoch, Guid nonce, string canonicalRelativePath) =>
					ReferenceEquals(key, IssuerKey) ? new V1PathToken(kind, generationId, epoch, nonce, canonicalRelativePath) :
					throw new InvalidOperationException("The token issuer is invalid.");
			}
		}

		// This capability is minted only while opening the durable adapter.  Tokens
		// deliberately expose no raw/relative string projection to the assembly.
		internal sealed class DurableAdapterResolver
		{
			internal string Resolve(V1PathToken token) => token == null ?
				throw new ArgumentNullException(nameof(token)) : token.ResolveFor(this);
		}

		internal static DurableAdapterResolver CreateDurableAdapterResolver() => new();

		internal static V1PathToken ProcessLockPath() =>
			Fixed(RetailImportV1PathKind.ProcessLock, ProcessLockName);

		internal static V1PathToken ActivePointerPath() =>
			Fixed(RetailImportV1PathKind.ActivePointer, ActivePointerName);

		internal static V1PathToken PublishJournalPath() =>
			Fixed(RetailImportV1PathKind.PublishJournal, PublishJournalName);

		internal static V1PathToken GenerationsDirectoryPath() =>
			Fixed(RetailImportV1PathKind.GenerationsDirectory, GenerationsDirectoryName);

		internal static V1PathToken QuarantineDirectoryPath() =>
			Fixed(RetailImportV1PathKind.QuarantineDirectory, QuarantineDirectoryName);

		internal static V1PathToken QuarantineEntryPath(Guid nonzeroNonce) =>
			MintToken(RetailImportV1PathKind.QuarantineEntry, null, 0,
				ValidateQuarantineNonce(nonzeroNonce), QuarantineDirectoryName + "/q-" + nonzeroNonce.ToString("N"));

		internal static V1PathToken PendingDirectoryPath() =>
			Fixed(RetailImportV1PathKind.PendingDirectory, PendingDirectoryName);

		internal static V1PathToken PendingItemsDirectoryPath() =>
			Fixed(RetailImportV1PathKind.PendingItemsDirectory,
				PendingDirectoryName + "/" + PendingItemsDirectoryName);

		internal static V1PathToken GenerationDirectoryPath(string generationId, long expectedEpoch) =>
			Generation(RetailImportV1PathKind.GenerationDirectory, generationId, expectedEpoch, null);

		internal static V1PathToken GenerationContentDirectoryPath(string generationId, long expectedEpoch) =>
			Generation(RetailImportV1PathKind.GenerationContentDirectory, generationId, expectedEpoch,
				ContentDirectoryName);

		internal static V1PathToken GenerationCatalogPath(string generationId, long expectedEpoch) =>
			Generation(RetailImportV1PathKind.GenerationCatalog, generationId, expectedEpoch, CatalogName);

		internal static V1PathToken GenerationReceiptsPath(string generationId, long expectedEpoch) =>
			Generation(RetailImportV1PathKind.GenerationReceipts, generationId, expectedEpoch, ReceiptsName);

		internal static V1PathToken GenerationArtifactPath(string generationId, long expectedEpoch,
			string canonicalGenerationRelativePath)
		{
			var validatedGeneration = ValidateGenerationId(generationId, expectedEpoch, nameof(generationId));
			var relative = ValidateGenerationRelativePath(canonicalGenerationRelativePath,
				nameof(canonicalGenerationRelativePath));
			return MintToken(RetailImportV1PathKind.GenerationArtifact, validatedGeneration,
				expectedEpoch, Guid.Empty, GenerationsDirectoryName + "/" + validatedGeneration + "/" +
					ContentDirectoryName + "/" + relative);
		}

		internal static V1PathToken PendingStagingDirectoryPath(Guid nonzeroNonce) =>
			Pending(RetailImportV1PathKind.PendingStagingDirectory, nonzeroNonce, "s", null);

		internal static V1PathToken PendingStagingContentDirectoryPath(Guid nonzeroNonce) =>
			Pending(RetailImportV1PathKind.PendingStagingContentDirectory, nonzeroNonce, "s",
				ContentDirectoryName);

		internal static V1PathToken PendingStagingArtifactPath(Guid nonzeroNonce,
			string canonicalStagedRelativePath) =>
			PendingArtifact(RetailImportV1PathKind.PendingStagingArtifact, nonzeroNonce, "s",
				canonicalStagedRelativePath);

		internal static V1PathToken PendingStagingSealPath(Guid nonzeroNonce) =>
			Pending(RetailImportV1PathKind.PendingStagingSeal, nonzeroNonce, "s", SealName);

		internal static V1PathToken PendingSealedDirectoryPath(Guid nonzeroNonce) =>
			Pending(RetailImportV1PathKind.PendingSealedDirectory, nonzeroNonce, "p", null);

		internal static V1PathToken PendingSealedContentDirectoryPath(Guid nonzeroNonce) =>
			Pending(RetailImportV1PathKind.PendingSealedContentDirectory, nonzeroNonce, "p",
				ContentDirectoryName);

		internal static V1PathToken PendingSealedArtifactPath(Guid nonzeroNonce,
			string canonicalStagedRelativePath) =>
			PendingArtifact(RetailImportV1PathKind.PendingSealedArtifact, nonzeroNonce, "p",
				canonicalStagedRelativePath);

		internal static V1PathToken PendingSealPath(Guid nonzeroNonce) =>
			Pending(RetailImportV1PathKind.PendingSeal, nonzeroNonce, "p", SealName);

		internal static V1PathToken PendingTombstonePath(Guid nonzeroNonce) =>
			Pending(RetailImportV1PathKind.PendingTombstone, nonzeroNonce, "t", null);

		internal static V1PathToken PendingConsumeMarkerPath() =>
			Fixed(RetailImportV1PathKind.PendingConsumeMarker,
				PendingDirectoryName + "/" + ConsumeMarkerName);

		internal static V1PathToken PendingLegacyMigrationMarkerPath() =>
			Fixed(RetailImportV1PathKind.PendingLegacyMigrationMarker,
				PendingDirectoryName + "/" + LegacyMigrationMarkerName);

		static V1PathToken Fixed(RetailImportV1PathKind kind, string canonicalRelativePath) =>
			MintToken(kind, null, 0, Guid.Empty, canonicalRelativePath);

		static V1PathToken Generation(RetailImportV1PathKind kind, string generationId,
			long expectedEpoch, string child)
		{
			var validatedGeneration = ValidateGenerationId(generationId, expectedEpoch, nameof(generationId));
			var relative = GenerationsDirectoryName + "/" + validatedGeneration;
			if (child != null)
				relative += "/" + child;

			return MintToken(kind, validatedGeneration, expectedEpoch, Guid.Empty, relative);
		}

		static V1PathToken Pending(RetailImportV1PathKind kind, Guid nonce, string prefix, string child)
		{
			ValidateNonce(nonce, nameof(nonce));
			var relative = PendingDirectoryName + "/" + PendingItemsDirectoryName + "/" +
				prefix + "-" + nonce.ToString("N");
			if (child != null)
				relative += "/" + child;

			return MintToken(kind, null, 0, nonce, relative);
		}

		static V1PathToken MintToken(RetailImportV1PathKind kind, string generationId,
			long epoch, Guid nonce, string canonicalRelativePath) =>
			TokenIssuer.Issue(kind, generationId, epoch, nonce, canonicalRelativePath);


		static V1PathToken PendingArtifact(RetailImportV1PathKind kind, Guid nonce, string prefix,
			string canonicalStagedRelativePath)
		{
			var artifact = ValidateStagedRelativePath(canonicalStagedRelativePath,
				nameof(canonicalStagedRelativePath));
			return Pending(kind, nonce, prefix, ContentDirectoryName + "/" + artifact);
		}

		static void ValidateNonce(Guid nonce, string parameterName)
		{
			if (nonce == Guid.Empty)
				throw new ArgumentException("A nonzero pending nonce is required.", parameterName);
		}

		static Guid ValidateQuarantineNonce(Guid nonce)
		{
			ValidateNonce(nonce, nameof(nonce));
			return nonce;
		}

		internal static string NewGenerationId(long epoch) =>
			CreateGenerationId(epoch, Guid.NewGuid());

		internal static string NewCommitId() => CreateCommitId(Guid.NewGuid());

		internal static string NewPendingId() => CreatePendingId(Guid.NewGuid());

		internal static string CreateGenerationId(long epoch, Guid guid)
		{
			if (epoch < 0)
				throw new ArgumentOutOfRangeException(nameof(epoch));

			return "g-" + epoch.ToString("D20", CultureInfo.InvariantCulture) + "-" +
				ValidateGuid(guid, nameof(guid));
		}

		internal static string CreateCommitId(Guid guid) =>
			"c-" + ValidateGuid(guid, nameof(guid));

		internal static string CreatePendingId(Guid guid) =>
			"p-" + ValidateGuid(guid, nameof(guid));

		internal static string ValidateGenerationId(
			string value,
			long expectedEpoch,
			string parameterName)
		{
			if (expectedEpoch < 0)
				throw new ArgumentOutOfRangeException(nameof(expectedEpoch));

			const int ExpectedLength = 2 + GenerationEpochLength + 1 + GuidTokenLength;
			if (value == null || value.Length != ExpectedLength ||
				!value.StartsWith("g-", StringComparison.Ordinal) ||
				value[2 + GenerationEpochLength] != '-')
				throw InvalidIdentity(parameterName);

			var epochToken = value.Substring(2, GenerationEpochLength);
			if (!IsAsciiDigits(epochToken) ||
				!long.TryParse(epochToken, NumberStyles.None, CultureInfo.InvariantCulture, out var epoch) ||
				epoch != expectedEpoch)
				throw InvalidIdentity(parameterName);

			ValidateGuidToken(value[(3 + GenerationEpochLength)..], parameterName);
			return value;
		}

		internal static string ValidateCommitId(string value, string parameterName) =>
			ValidatePrefixedGuid(value, 'c', parameterName);

		internal static string ValidatePendingId(string value, string parameterName) =>
			ValidatePrefixedGuid(value, 'p', parameterName);

		internal static string ValidateOptionalPendingId(string value, string parameterName) =>
			value == null ? null : ValidatePendingId(value, parameterName);

		internal static string ValidateSha256(string value, string parameterName) =>
			ValidateLowercaseHex(value, Sha256Length, parameterName);

		internal static string ValidateMapUid(string value, string parameterName) =>
			ValidateLowercaseHex(value, MapUidLength, parameterName);

		internal static string ValidateCanonicalRelativePath(string value, string parameterName)
		{
			try
			{
				if (string.IsNullOrEmpty(value) || value[0] == '/' || value[^1] == '/' ||
					value.IndexOfAny(new[] { '\\', '\0', ':' }) >= 0 ||
					!value.IsNormalized(NormalizationForm.FormC) ||
					StrictUtf8.GetByteCount(value) > MaximumRelativePathBytes)
					throw InvalidPath(parameterName);

				foreach (var character in value)
					if (char.IsControl(character) || character == '\u2028' || character == '\u2029')
						throw InvalidPath(parameterName);

				var components = value.Split('/');
				foreach (var component in components)
					if (component.Length == 0 || component == "." || component == ".." ||
						StrictUtf8.GetByteCount(component) > MaximumComponentBytes)
						throw InvalidPath(parameterName);

				return value;
			}
			catch (EncoderFallbackException)
			{
				throw InvalidPath(parameterName);
			}
			catch (ArgumentException exception) when (exception.ParamName == null)
			{
				throw InvalidPath(parameterName);
			}
		}

		internal static string ValidateStagedRelativePath(string value, string parameterName)
		{
			var canonical = ValidateCanonicalRelativePath(value, parameterName);
			_ = new RetailStagingTarget(canonical);
			return canonical;
		}

		internal static string ValidateGenerationRelativePath(string value, string parameterName)
		{
			var canonical = ValidateCanonicalRelativePath(value, parameterName);
			if (canonical is "base/language.mix" or "base/ra2.mix" or
				"ra2-music/theme.mix" or "yuri/langmd.mix" or "yuri/ra2md.mix" or
				"yuri-music/thememd.mix")
				return canonical;

			const string MapsPrefix = "maps/";
			const string MapSuffix = ".oramap";
			if (canonical.Length == MapsPrefix.Length + MapUidLength + MapSuffix.Length &&
				canonical.StartsWith(MapsPrefix, StringComparison.Ordinal) &&
				canonical.EndsWith(MapSuffix, StringComparison.Ordinal))
			{
				var mapUid = canonical.Substring(MapsPrefix.Length, MapUidLength);
				try
				{
					ValidateMapUid(mapUid, parameterName);
					return canonical;
				}
				catch (ArgumentException)
				{
					throw InvalidPath(parameterName);
				}
			}

			throw InvalidPath(parameterName);
		}

		internal static string ValidateGenerationRelativePath(
			string value,
			RetailContentGroup group,
			string logicalName,
			string parameterName)
		{
			var canonical = ValidateGenerationRelativePath(value, parameterName);
			var expected = group switch
			{
				RetailContentGroup.Base when logicalName is "language.mix" or "ra2.mix" =>
					"base/" + logicalName,
				RetailContentGroup.Ra2Music when logicalName == "theme.mix" =>
					"ra2-music/theme.mix",
				RetailContentGroup.Yuri when logicalName is "langmd.mix" or "ra2md.mix" =>
					"yuri/" + logicalName,
				RetailContentGroup.YuriMusic when logicalName == "thememd.mix" =>
					"yuri-music/thememd.mix",
				RetailContentGroup.Maps => MapRelativePath(logicalName),
				_ => null,
			};

			if (!StringComparer.Ordinal.Equals(canonical, expected))
				throw InvalidPath(parameterName);

			return canonical;
		}

		internal static string MapRelativePath(string mapUid) =>
			"maps/" + ValidateMapUid(mapUid, nameof(mapUid)) + ".oramap";

		internal static string ResolveUnderRoot(string fixedRoot, string canonicalRelativePath)
		{
			if (string.IsNullOrEmpty(fixedRoot) || !Path.IsPathFullyQualified(fixedRoot))
				throw new ArgumentException("A fixed storage root is required.", nameof(fixedRoot));

			var relative = ValidateCanonicalRelativePath(
				canonicalRelativePath, nameof(canonicalRelativePath));
			try
			{
				var root = Path.GetFullPath(fixedRoot);
				var candidate = root;
				foreach (var component in relative.Split('/'))
					candidate = Path.Combine(candidate, component);

				candidate = Path.GetFullPath(candidate);
				var comparison = OperatingSystem.IsWindows() ?
					StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
				var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ?
					root : root + Path.DirectorySeparatorChar;
				if (!candidate.StartsWith(rootPrefix, comparison))
					throw InvalidPath(nameof(canonicalRelativePath));

				return candidate;
			}
			catch (Exception exception) when (exception is IOException ||
				exception is NotSupportedException || exception is System.Security.SecurityException)
			{
				throw InvalidPath(nameof(canonicalRelativePath));
			}
		}

		static string ValidateGuid(Guid value, string parameterName)
		{
			if (value == Guid.Empty)
				throw InvalidIdentity(parameterName);

			return value.ToString("N", CultureInfo.InvariantCulture);
		}

		static string ValidatePrefixedGuid(string value, char prefix, string parameterName)
		{
			if (value == null || value.Length != 2 + GuidTokenLength ||
				value[0] != prefix || value[1] != '-')
				throw InvalidIdentity(parameterName);

			ValidateGuidToken(value[2..], parameterName);
			return value;
		}

		static void ValidateGuidToken(string value, string parameterName)
		{
			ValidateLowercaseHex(value, GuidTokenLength, parameterName);
			if (value == new string('0', GuidTokenLength))
				throw InvalidIdentity(parameterName);
		}

		static string ValidateLowercaseHex(string value, int length, string parameterName)
		{
			if (value == null || value.Length != length)
				throw InvalidIdentity(parameterName);

			foreach (var character in value)
				if (!IsLowercaseHex(character))
					throw InvalidIdentity(parameterName);

			return value;
		}

		static bool IsAsciiDigits(string value)
		{
			foreach (var character in value)
				if (character < '0' || character > '9')
					return false;

			return true;
		}

		static bool IsLowercaseHex(char value) =>
			(value >= '0' && value <= '9') || (value >= 'a' && value <= 'f');

		static ArgumentException InvalidIdentity(string parameterName) =>
			new("The storage identity is not canonical.", parameterName);

		static ArgumentException InvalidPath(string parameterName) =>
			new("The path is not a canonical root-relative path.", parameterName);
	}
}
