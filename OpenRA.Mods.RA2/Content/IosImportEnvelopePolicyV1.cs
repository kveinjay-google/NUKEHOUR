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

namespace OpenRA.Mods.RA2.Content
{
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
		public const int MapPngPixels = 16_777_216;
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
}
