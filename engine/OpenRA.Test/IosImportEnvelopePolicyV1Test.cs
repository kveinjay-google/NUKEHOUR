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
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.FileSystem;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosImportEnvelopePolicyV1Test
	{
		static void AssertValueEqual<T>(T left, T right)
		{
			Assert.That(right, Is.EqualTo(left));
			Assert.That(right.GetHashCode(), Is.EqualTo(left.GetHashCode()));
			Assert.That(new HashSet<T> { left }.Contains(right), Is.True);
		}

		static void AssertRecord<T>()
		{
			Assert.That(typeof(T).GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null,
				typeof(T).Name + " must be a C# record class.");
		}

		static void AssertNames<T>(params string[] expected) where T : Enum
		{
			Assert.That(Enum.GetNames(typeof(T)), Is.EqualTo(expected));
		}

		[Test]
		public void ClosedEnumsHaveOnlyTheSpecifiedOrderedValues()
		{
			AssertNames<RetailContentGroup>("Base", "Ra2Music", "Yuri", "YuriMusic", "Maps");
			AssertNames<RetailGroupValidation>("Missing", "Invalid", "Unavailable", "Ready");
			AssertNames<RetailEditionRecognition>("NotApplicable", "Recognized", "CompatibleUnrecognized");
			AssertNames<RetailImportPhase>("PreparingSources", "Discovering", "Planning", "Copying", "VerifyingActiveHashes", "ValidatingEnvelopes", "ValidatingArchives", "ValidatingAudio", "ConvertingMaps", "LoadingMaps", "DownloadingMap", "ValidatingDownloadedMap", "SelectingProfile", "ValidatingMounts", "CheckingRuntimeResources", "Committing", "Recovering");
			AssertNames<RetailImportErrorCode>("Cancelled", "EmptySelection", "ExtractInstallationFirst", "ExecutableSelection", "ProviderUnavailable", "ProviderMaterializationRequired", "SourcePermissionDenied", "SourceAccessExpired", "SourceChanged", "SourceLimitExceeded", "ValidationTimedOut", "DuplicateConflict", "InsufficientSpace", "CrossVolume", "CorruptArchive", "MissingNestedBasePackage", "InvalidAudio", "IncompleteBase", "PartialYuri", "NoPlayableMap", "MapConversionFailed", "UnsafeMap", "DownloadedMapInvalid", "DownloadedMapUidMismatch", "ProfileUnavailable", "ProfileContextFailure", "RequiredMountFailure", "RuntimeResourcesMissing", "ReceiptMismatch", "ColdHashMismatch", "CorruptJournal", "RecoveryFailed", "PublishFailed", "RollbackFailed", "PendingExpired", "UpdateRequired", "ResetRequired", "LegacyMigrationFailed", "StoragePolicyFailure", "SanitizedIoFailure");
			AssertNames<RetailImportDiagnosticCode>("None", "ProviderNeedsDownload", "ProviderOffline", "SecurityScopeDenied", "DeadlineExceeded", "LengthMismatch", "HashMismatch", "MalformedEnvelope", "CorruptMapSource", "UnsupportedMapFeature", "InvalidSpawnLayout", "ConversionOutputFailure", "ArithmeticOverflow", "MissingRequiredArchive", "InvalidMapPackage", "ProfileMetadataMismatch", "PointerMismatch", "JournalInvalid", "GenerationInvalid", "InsufficientCapacity");
			AssertNames<RetailSourceKind>("DirectFile", "DirectoryDiscoveredFile");
			AssertNames<RetailSourceReadKind>("Entry", "Complete");
			AssertNames<PackageEnvelopeFailure>("Malformed", "LimitExceeded",
				"AggregateLimitExceeded", "ArithmeticOverflow");
		}

		[Test]
		public void PackageEnvelopeExceptionHasTheExactTwoPublicConstructorShapes()
		{
			var constructors = typeof(PackageEnvelopeException).GetConstructors()
				.Select(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray())
				.OrderBy(parameters => parameters.Length)
				.ToArray();
			Assert.That(constructors, Has.Length.EqualTo(2));
			Assert.That(constructors[0], Is.EqualTo(new[] { typeof(PackageEnvelopeFailure), typeof(string) }));
			Assert.That(constructors[1], Is.EqualTo(new[]
			{
				typeof(PackageEnvelopeFailure), typeof(string), typeof(Exception),
			}));
		}

		[Test]
		public void V1ConstantsAreExact()
		{
			Assert.Multiple(() =>
			{
				Assert.That(IosImportEnvelopePolicyV1.PickerRoots, Is.EqualTo(64));
				Assert.That(IosImportEnvelopePolicyV1.DiscoveryDepth, Is.EqualTo(24));
				Assert.That(IosImportEnvelopePolicyV1.DiscoveryEntries, Is.EqualTo(100_000));
				Assert.That(IosImportEnvelopePolicyV1.DiscoveryBytes, Is.EqualTo(32L << 30));
				Assert.That(IosImportEnvelopePolicyV1.CandidateFiles, Is.EqualTo(4_096));
				Assert.That(IosImportEnvelopePolicyV1.CandidateBytes, Is.EqualTo(8L << 30));
				Assert.That(IosImportEnvelopePolicyV1.SourceBytes, Is.EqualTo(2L << 30));
				Assert.That(IosImportEnvelopePolicyV1.MixDepth, Is.EqualTo(8));
				Assert.That(IosImportEnvelopePolicyV1.MixEntriesPerArchive, Is.EqualTo(131_072));
				Assert.That(IosImportEnvelopePolicyV1.MixEntriesAggregate, Is.EqualTo(262_144));
				Assert.That(IosImportEnvelopePolicyV1.MixIndexBytes, Is.EqualTo(16L << 20));
				Assert.That(IosImportEnvelopePolicyV1.MixLocalDatabaseBytes, Is.EqualTo(8L << 20));
				Assert.That(IosImportEnvelopePolicyV1.MixLocalDatabaseNames, Is.EqualTo(262_144));
				Assert.That(IosImportEnvelopePolicyV1.MixSegmentBytes, Is.EqualTo(512L << 20));
				Assert.That(IosImportEnvelopePolicyV1.AudioBagBytes, Is.EqualTo(384L << 20));
				Assert.That(IosImportEnvelopePolicyV1.MixAddressedBytes, Is.EqualTo(8L << 30));
				Assert.That(IosImportEnvelopePolicyV1.IdxBytes, Is.EqualTo(4L << 20));
				Assert.That(IosImportEnvelopePolicyV1.IdxSoundRecords, Is.EqualTo(65_536));
				Assert.That(IosImportEnvelopePolicyV1.IdxNameBytes, Is.EqualTo(16));
				Assert.That(IosImportEnvelopePolicyV1.BagBytes, Is.EqualTo(384L << 20));
				Assert.That(IosImportEnvelopePolicyV1.BagSoundSpanBytes, Is.EqualTo(256L << 20));
				Assert.That(IosImportEnvelopePolicyV1.TraditionalMapSourceBytes, Is.EqualTo(32L << 20));
				Assert.That(IosImportEnvelopePolicyV1.TraditionalMapWidth, Is.EqualTo(512));
				Assert.That(IosImportEnvelopePolicyV1.TraditionalMapHeight, Is.EqualTo(512));
				Assert.That(IosImportEnvelopePolicyV1.TraditionalMapCells, Is.EqualTo(262_144));
				Assert.That(IosImportEnvelopePolicyV1.MapActors, Is.EqualTo(8_192));
				Assert.That(IosImportEnvelopePolicyV1.MapPlayerDefinitions, Is.EqualTo(63));
				Assert.That(IosImportEnvelopePolicyV1.MapSpawns, Is.EqualTo(8));
				Assert.That(IosImportEnvelopePolicyV1.ImportedMaps, Is.EqualTo(256));
				Assert.That(IosImportEnvelopePolicyV1.RuntimeMaps, Is.EqualTo(128));
				Assert.That(IosImportEnvelopePolicyV1.OramapCompressedBytes, Is.EqualTo(64L << 20));
				Assert.That(IosImportEnvelopePolicyV1.OramapEntries, Is.EqualTo(8));
				Assert.That(IosImportEnvelopePolicyV1.OramapExpandedEntryBytes, Is.EqualTo(64L << 20));
				Assert.That(IosImportEnvelopePolicyV1.OramapExpandedAggregateBytes, Is.EqualTo(96L << 20));
				Assert.That(IosImportEnvelopePolicyV1.OramapExpansionRatio, Is.EqualTo(100));
				Assert.That(IosImportEnvelopePolicyV1.MapYamlBytes, Is.EqualTo(8L << 20));
				Assert.That(IosImportEnvelopePolicyV1.MapYamlNodes, Is.EqualTo(131_072));
				Assert.That(IosImportEnvelopePolicyV1.MapYamlDepth, Is.EqualTo(64));
				Assert.That(IosImportEnvelopePolicyV1.MapYamlLineBytes, Is.EqualTo(256 << 10));
				Assert.That(IosImportEnvelopePolicyV1.MapYamlScalarBytes, Is.EqualTo(64 << 10));
				Assert.That(IosImportEnvelopePolicyV1.MapPngEdge, Is.EqualTo(4_096));
				Assert.That(IosImportEnvelopePolicyV1.MapPngPixels, Is.EqualTo(16_777_216));
				Assert.That(IosImportEnvelopePolicyV1.MapPngDecodedBytes, Is.EqualTo(64L << 20));
				Assert.That(IosImportEnvelopePolicyV1.MapPngChunks, Is.EqualTo(4_096));
				Assert.That(IosImportEnvelopePolicyV1.MapPngIdatBytes, Is.EqualTo(32L << 20));
				Assert.That(IosImportEnvelopePolicyV1.MapPngInflateRatio, Is.EqualTo(100));
				Assert.That(IosImportEnvelopePolicyV1.ImportedMapStorageBytes, Is.EqualTo(2L << 30));
				Assert.That(IosImportEnvelopePolicyV1.PreviewThumbnailEdge, Is.EqualTo(1_024));
				Assert.That(IosImportEnvelopePolicyV1.PreviewAtlasPage, Is.EqualTo(2_048));
				Assert.That(IosImportEnvelopePolicyV1.PreviewAtlasMargin, Is.EqualTo(1));
				Assert.That(IosImportEnvelopePolicyV1.PreviewCpuBudget, Is.EqualTo(64L << 20));
				Assert.That(IosImportEnvelopePolicyV1.PreviewGpuBudget, Is.EqualTo(128L << 20));
				Assert.That(IosImportEnvelopePolicyV1.ResidentMapMetadataBytes, Is.EqualTo(64L << 20));
				Assert.That(IosImportEnvelopePolicyV1.ResidentStringBytes, Is.EqualTo(32L << 20));
				Assert.That(IosImportEnvelopePolicyV1.ResidentStringEntries, Is.EqualTo(262_144));
				Assert.That(IosImportEnvelopePolicyV1.PerMapStringBytes, Is.EqualTo(8L << 20));
				Assert.That(IosImportEnvelopePolicyV1.RuntimeGenerationBytes, Is.EqualTo(1L << 30));
				Assert.That(IosImportEnvelopePolicyV1.RuntimeWithRollbackBytes, Is.EqualTo(2L << 30));
				Assert.That(IosImportEnvelopePolicyV1.RuntimeDownloadQueue, Is.EqualTo(4));
				Assert.That(IosImportEnvelopePolicyV1.RuntimeDownloadStagingBytes, Is.EqualTo(256L << 20));
				Assert.That(IosImportEnvelopePolicyV1.DiskSafetyMarginBytes, Is.EqualTo(512L << 20));
				Assert.That(IosImportEnvelopePolicyV1.RemoteResponseBytes, Is.EqualTo(4L << 20));
				Assert.That(IosImportEnvelopePolicyV1.RemoteRequestedUids, Is.EqualTo(256));
				Assert.That(IosImportEnvelopePolicyV1.RemoteRecords, Is.EqualTo(256));
				Assert.That(IosImportEnvelopePolicyV1.RemoteRecordBytes, Is.EqualTo(3L << 20));
				Assert.That(IosImportEnvelopePolicyV1.RemoteOrdinaryLineBytes, Is.EqualTo(64 << 10));
				Assert.That(IosImportEnvelopePolicyV1.RemoteBinaryLineBytes, Is.EqualTo(2_304 << 10));
				Assert.That(IosImportEnvelopePolicyV1.RemoteNodes, Is.EqualTo(16_384));
				Assert.That(IosImportEnvelopePolicyV1.RemoteDepth, Is.EqualTo(32));
				Assert.That(IosImportEnvelopePolicyV1.RemoteFieldsPerRecord, Is.EqualTo(64));
				Assert.That(IosImportEnvelopePolicyV1.RemoteScalarBytes, Is.EqualTo(16 << 10));
				Assert.That(IosImportEnvelopePolicyV1.RemoteEncodedBinaryBytes, Is.EqualTo(2 << 20));
				Assert.That(IosImportEnvelopePolicyV1.RemoteDecodedBinaryBytes, Is.EqualTo(1_572_864));
				Assert.That(IosImportEnvelopePolicyV1.RemoteMinimapEdge, Is.EqualTo(1_024));
				Assert.That(IosImportEnvelopePolicyV1.RemoteMinimapPixels, Is.EqualTo(1_048_576));
				Assert.That(IosImportEnvelopePolicyV1.RemoteMinimapDecodedBytes, Is.EqualTo(4 << 20));
				Assert.That(IosImportEnvelopePolicyV1.RemotePlayers, Is.EqualTo(64));
				Assert.That(IosImportEnvelopePolicyV1.RemoteSpawns, Is.EqualTo(8));
				Assert.That(IosImportEnvelopePolicyV1.RemoteCategories, Is.EqualTo(64));
				Assert.That(IosImportEnvelopePolicyV1.RemoteRetainedRecords, Is.EqualTo(512));
				Assert.That(IosImportEnvelopePolicyV1.RemoteRetainedBytes, Is.EqualTo(16L << 20));
				Assert.That(IosImportEnvelopePolicyV1.RemoteRetainedStringBytes, Is.EqualTo(4L << 20));
				Assert.That(IosImportEnvelopePolicyV1.RemoteRetainedStringEntries, Is.EqualTo(32_768));
				Assert.That(IosImportEnvelopePolicyV1.PackagePathUtf8Bytes, Is.EqualTo(64));
				Assert.That(IosImportEnvelopePolicyV1.PackagePathDepth, Is.EqualTo(1));
				Assert.That(IosImportEnvelopePolicyV1.PendingBytes, Is.EqualTo(8L << 30));
				Assert.That(IosImportEnvelopePolicyV1.PendingRetention, Is.EqualTo(TimeSpan.FromDays(7)));
				Assert.That(IosImportEnvelopePolicyV1.ProviderPreparationTimeout, Is.EqualTo(TimeSpan.FromMinutes(30)));
				Assert.That(IosImportEnvelopePolicyV1.DiscoveryTimeout, Is.EqualTo(TimeSpan.FromMinutes(10)));
				Assert.That(IosImportEnvelopePolicyV1.ArchiveTimeout, Is.EqualTo(TimeSpan.FromMinutes(10)));
				Assert.That(IosImportEnvelopePolicyV1.MapTimeout, Is.EqualTo(TimeSpan.FromMinutes(5)));
				Assert.That(IosImportEnvelopePolicyV1.RemoteMetadataTimeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
				Assert.That(IosImportEnvelopePolicyV1.WholeValidationTimeout, Is.EqualTo(TimeSpan.FromMinutes(90)));
			});
		}

		[Test]
		public void TaskTwoPolicyAdapterMapsEveryArchivePngAndYamlFieldExactly()
		{
			var policy = RetailArchiveGraphValidator.IosV1Policy;

			Assert.That(policy.Zip, Is.EqualTo(new ZipEnvelopeLimits(
				IosImportEnvelopePolicyV1.PackagePathUtf8Bytes, IosImportEnvelopePolicyV1.PackagePathDepth,
				IosImportEnvelopePolicyV1.OramapEntries, IosImportEnvelopePolicyV1.OramapCompressedBytes,
				IosImportEnvelopePolicyV1.OramapExpandedEntryBytes,
				IosImportEnvelopePolicyV1.OramapExpandedAggregateBytes,
				IosImportEnvelopePolicyV1.OramapExpansionRatio)));
			Assert.That(policy.Png, Is.EqualTo(new PngReadLimits(
				IosImportEnvelopePolicyV1.MapPngEdge, IosImportEnvelopePolicyV1.MapPngPixels,
				IosImportEnvelopePolicyV1.MapPngDecodedBytes, IosImportEnvelopePolicyV1.MapPngChunks,
				IosImportEnvelopePolicyV1.MapPngIdatBytes, IosImportEnvelopePolicyV1.MapPngInflateRatio)));
			Assert.That(policy.Yaml, Is.EqualTo(new YamlEnvelopeLimits(
				IosImportEnvelopePolicyV1.MapYamlBytes, IosImportEnvelopePolicyV1.MapYamlNodes,
				IosImportEnvelopePolicyV1.MapYamlDepth, IosImportEnvelopePolicyV1.MapYamlLineBytes,
				IosImportEnvelopePolicyV1.MapYamlScalarBytes)));
			Assert.That(policy.Mix, Is.EqualTo(new MixEnvelopeLimits(
				IosImportEnvelopePolicyV1.MixDepth, IosImportEnvelopePolicyV1.MixEntriesPerArchive,
				IosImportEnvelopePolicyV1.MixEntriesAggregate, IosImportEnvelopePolicyV1.MixIndexBytes,
				IosImportEnvelopePolicyV1.MixLocalDatabaseBytes, IosImportEnvelopePolicyV1.MixLocalDatabaseNames,
				IosImportEnvelopePolicyV1.MixSegmentBytes, IosImportEnvelopePolicyV1.MixAddressedBytes)));
			Assert.That(policy.Audio, Is.EqualTo(new IdxBagEnvelopeLimits(
				IosImportEnvelopePolicyV1.IdxBytes, IosImportEnvelopePolicyV1.IdxSoundRecords,
				IosImportEnvelopePolicyV1.IdxNameBytes, IosImportEnvelopePolicyV1.BagBytes,
				IosImportEnvelopePolicyV1.BagSoundSpanBytes)));
		}

		[TestCase(1, 4, 1, 2, 25)]
		[TestCase(1, 3, 0, 0, 33)]
		[TestCase(5, 4, 0, 0, 100)]
		[TestCase(0, 0, 1, 3, 33)]
		public void ProgressUsesTruncatedWholeBytesThenItemsAndCaps(long completedBytes, long? totalBytes, int completedItems, int? totalItems, int expected)
		{
			var progress = new RetailImportProgress(RetailImportPhase.Copying, null, completedItems, totalItems, completedBytes, totalBytes, null);
			Assert.That(progress.GetType().GetProperty(nameof(RetailImportProgress.Percentage)).PropertyType, Is.EqualTo(typeof(int?)));
			Assert.That(progress.Percentage, Is.EqualTo(expected));
		}

		[Test]
		public void ProgressWithoutPositiveKnownDenominatorIsIndeterminate()
		{
			var progress = new RetailImportProgress(RetailImportPhase.Discovering, null, 0, null, 0, null, null);
			Assert.That(progress.Percentage, Is.Null);
		}

		[TestCase(".")]
		[TestCase("..")]
		[TestCase("a/b")]
		[TestCase("a\\b")]
		[TestCase("a:b")]
		[TestCase("a\0b")]
		[TestCase("a\tb")]
		[TestCase("a\nb")]
		[TestCase("a\u001fb")]
		[TestCase("a\u007fb")]
		[TestCase("a\u0085b")]
		[TestCase("a\u2028b")]
		[TestCase("a\u2029b")]
		public void SafeBasenamesRejectPathAndControlSyntax(string basename)
		{
			Assert.Throws<ArgumentException>(() => new RetailImportFailure(
				RetailImportErrorCode.SourceChanged, null, basename, RetailImportDiagnosticCode.None));
			Assert.Throws<ArgumentException>(() => new RetailImportProgress(
				RetailImportPhase.Copying, null, 0, null, 0, null, basename));
		}

		[Test]
		public void EveryPublicBasenameDtoUsesTheSameValidatorAndPreservesUnicode()
		{
			var id = new RetailSourceId(0, "maps/地图.map");
			Assert.That(new RetailImportFailure(RetailImportErrorCode.SourceChanged, null, "地图.map",
				RetailImportDiagnosticCode.None).SafeBasename, Is.EqualTo("地图.map"));
			Assert.That(new RetailImportProgress(RetailImportPhase.Copying, null, 0, null, 0, null,
				"地图.map").SafeBasename, Is.EqualTo("地图.map"));
			Assert.That(new RetailSourceEntry(id, "地图.map", RetailSourceKind.DirectoryDiscoveredFile,
				1, false).Basename, Is.EqualTo("地图.map"));
			Assert.That(new RetailPlannedCandidate(id, "地图.map", RetailSourceKind.DirectoryDiscoveredFile,
				RetailContentGroup.Maps, 1).SafeBasename, Is.EqualTo("地图.map"));
			Assert.That(new StagedRetailSource(id, "地图.map", "sources/0/maps/地图.map", 1,
				new string('a', 64)).SafeBasename, Is.EqualTo("地图.map"));

			Assert.Throws<ArgumentException>(() => new RetailSourceEntry(
				new RetailSourceId(0, "bad:name.map"), "bad:name.map", RetailSourceKind.DirectFile, 1, false));
			Assert.Throws<ArgumentException>(() => new RetailPlannedCandidate(
				new RetailSourceId(0, "bad:name.map"), "bad:name.map", RetailSourceKind.DirectFile,
				RetailContentGroup.Maps, 1));
			Assert.Throws<ArgumentException>(() => new StagedRetailSource(
				new RetailSourceId(0, "bad:name.map"), "bad:name.map", "sources/0/bad:name.map", 1,
				new string('a', 64)));
		}

		[Test]
		public void ProgressIsReadonlyValueTypeWithAllMaterialFieldsInEquality()
		{
			Assert.That(typeof(RetailImportProgress).IsValueType, Is.True);
			Assert.That(typeof(RetailImportProgress).GetFields(BindingFlags.Instance | BindingFlags.Public)
				.All(field => field.IsInitOnly), Is.True);
			var value = new RetailImportProgress(RetailImportPhase.Copying, RetailContentGroup.Base, 1, 2, 3, 4, "ra2.mix");
			var equal = new RetailImportProgress(RetailImportPhase.Copying, RetailContentGroup.Base, 1, 2, 3, 4, "ra2.mix");
			AssertValueEqual(value, equal);
			Assert.That(value == equal, Is.True);
			Assert.That(value != equal, Is.False);

			var unequal = new[]
			{
				new RetailImportProgress(RetailImportPhase.Planning, RetailContentGroup.Base, 1, 2, 3, 4, "ra2.mix"),
				new RetailImportProgress(RetailImportPhase.Copying, RetailContentGroup.Maps, 1, 2, 3, 4, "ra2.mix"),
				new RetailImportProgress(RetailImportPhase.Copying, RetailContentGroup.Base, 0, 2, 3, 4, "ra2.mix"),
				new RetailImportProgress(RetailImportPhase.Copying, RetailContentGroup.Base, 1, 3, 3, 4, "ra2.mix"),
				new RetailImportProgress(RetailImportPhase.Copying, RetailContentGroup.Base, 1, 2, 2, 4, "ra2.mix"),
				new RetailImportProgress(RetailImportPhase.Copying, RetailContentGroup.Base, 1, 2, 3, 5, "ra2.mix"),
				new RetailImportProgress(RetailImportPhase.Copying, RetailContentGroup.Base, 1, 2, 3, 4, "map01.map"),
			};
			Assert.That(unequal, Has.All.Not.EqualTo(value));
		}

		[Test]
		public void FailureAndGroupStatusAreRecordValuesAcrossEveryMaterialField()
		{
			AssertRecord<RetailImportFailure>();
			AssertRecord<RetailGroupStatus>();
			var failure = new RetailImportFailure(RetailImportErrorCode.SourceChanged, RetailContentGroup.Base,
				"ra2.mix", RetailImportDiagnosticCode.LengthMismatch);
			AssertValueEqual(failure, new RetailImportFailure(RetailImportErrorCode.SourceChanged, RetailContentGroup.Base,
				"ra2.mix", RetailImportDiagnosticCode.LengthMismatch));
			Assert.That(new[]
			{
				new RetailImportFailure(RetailImportErrorCode.SourceLimitExceeded, RetailContentGroup.Base, "ra2.mix", RetailImportDiagnosticCode.LengthMismatch),
				new RetailImportFailure(RetailImportErrorCode.SourceChanged, RetailContentGroup.Maps, "ra2.mix", RetailImportDiagnosticCode.LengthMismatch),
				new RetailImportFailure(RetailImportErrorCode.SourceChanged, RetailContentGroup.Base, "language.mix", RetailImportDiagnosticCode.LengthMismatch),
				new RetailImportFailure(RetailImportErrorCode.SourceChanged, RetailContentGroup.Base, "ra2.mix", RetailImportDiagnosticCode.HashMismatch),
			}, Has.All.Not.EqualTo(failure));

			var status = new RetailGroupStatus(RetailGroupValidation.Ready, RetailEditionRecognition.Recognized,
				RetailImportDiagnosticCode.None);
			AssertValueEqual(status, new RetailGroupStatus(RetailGroupValidation.Ready, RetailEditionRecognition.Recognized,
				RetailImportDiagnosticCode.None));
			Assert.That(new[]
			{
				new RetailGroupStatus(RetailGroupValidation.Invalid, RetailEditionRecognition.Recognized, RetailImportDiagnosticCode.None),
				new RetailGroupStatus(RetailGroupValidation.Ready, RetailEditionRecognition.CompatibleUnrecognized, RetailImportDiagnosticCode.None),
				new RetailGroupStatus(RetailGroupValidation.Ready, RetailEditionRecognition.Recognized, RetailImportDiagnosticCode.HashMismatch),
			}, Has.All.Not.EqualTo(status));
		}

		[Test]
		public void OperationResultsEnforceExactlyOneState()
		{
			var failure = new RetailImportFailure(RetailImportErrorCode.EmptySelection, null, null, RetailImportDiagnosticCode.None);
			var success = RetailOperationResult<string>.Success("ok");
			var failed = RetailOperationResult<string>.Failed(failure);

			Assert.That(success.Succeeded, Is.True);
			Assert.That(success.Value, Is.EqualTo("ok"));
			Assert.Throws<InvalidOperationException>(() => _ = success.Failure);
			Assert.That(failed.Succeeded, Is.False);
			Assert.That(failed.Failure, Is.SameAs(failure));
			Assert.Throws<InvalidOperationException>(() => _ = failed.Value);
			Assert.Throws<ArgumentNullException>(() => RetailOperationResult<string>.Success(null));
			Assert.Throws<ArgumentNullException>(() => RetailOperationResult<string>.Failed(null));
		}

		[Test]
		public void DeadlineUsesOnlyMonotonicElapsedTime()
		{
			var clock = new FakeClock { Timestamp = 10 };
			var deadline = RetailValidationDeadline.Start(clock, TimeSpan.FromSeconds(5));
			clock.Timestamp = 14;
			Assert.That(deadline.Remaining(clock), Is.EqualTo(TimeSpan.FromSeconds(1)));
			clock.Timestamp = 15;
			Assert.Throws<TimeoutException>(() => deadline.ThrowIfExpired(clock));
		}

		sealed class FakeClock : IRetailMonotonicClock
		{
			public long Timestamp { get; set; }
			public long GetTimestamp() => Timestamp;
			public TimeSpan GetElapsedTime(long start, long end) => TimeSpan.FromSeconds(end - start);
		}
	}
}
