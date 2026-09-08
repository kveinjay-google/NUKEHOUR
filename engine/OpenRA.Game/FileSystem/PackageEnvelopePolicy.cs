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
using System.IO;

namespace OpenRA.FileSystem
{
	public enum PackageEnvelopeFailure
	{
		Malformed,
		LimitExceeded,
		AggregateLimitExceeded,
		ArithmeticOverflow,
	}

	public sealed class PackageEnvelopeException : IOException
	{
		public PackageEnvelopeFailure Failure { get; }

		public PackageEnvelopeException(PackageEnvelopeFailure failure, string message)
			: this(failure, message, null)
		{
		}

		public PackageEnvelopeException(PackageEnvelopeFailure failure, string message, Exception innerException)
			: base(message, innerException)
		{
			if (!Enum.IsDefined(typeof(PackageEnvelopeFailure), failure))
				throw new ArgumentOutOfRangeException(nameof(failure));

			Failure = failure;
		}
	}

	public sealed record PackageEnvelopePolicy
	{
		public ZipEnvelopeLimits Zip { get; }
		public PngReadLimits Png { get; }
		public YamlEnvelopeLimits Yaml { get; }
		public MixEnvelopeLimits Mix { get; }
		public IdxBagEnvelopeLimits Audio { get; }

		public PackageEnvelopePolicy(ZipEnvelopeLimits zip, PngReadLimits png, YamlEnvelopeLimits yaml,
			MixEnvelopeLimits mix, IdxBagEnvelopeLimits audio)
		{
			Zip = zip ?? throw new ArgumentNullException(nameof(zip));
			Png = png ?? throw new ArgumentNullException(nameof(png));
			Yaml = yaml ?? throw new ArgumentNullException(nameof(yaml));
			Mix = mix ?? throw new ArgumentNullException(nameof(mix));
			Audio = audio ?? throw new ArgumentNullException(nameof(audio));
		}
	}

	public sealed record ZipEnvelopeLimits
	{
		public int MaximumPathUtf8Bytes { get; }
		public int MaximumPathDepth { get; }
		public int MaximumEntries { get; }
		public long MaximumCompressedBytes { get; }
		public long MaximumExpandedEntryBytes { get; }
		public long MaximumExpandedAggregateBytes { get; }
		public int MaximumExpansionRatio { get; }

		public ZipEnvelopeLimits(int maximumPathUtf8Bytes, int maximumPathDepth, int maximumEntries,
			long maximumCompressedBytes, long maximumExpandedEntryBytes, long maximumExpandedAggregateBytes,
			int maximumExpansionRatio)
		{
			MaximumPathUtf8Bytes = Positive(maximumPathUtf8Bytes, nameof(maximumPathUtf8Bytes));
			if (maximumPathDepth < 0)
				throw new ArgumentOutOfRangeException(nameof(maximumPathDepth));
			MaximumPathDepth = maximumPathDepth;
			MaximumEntries = Positive(maximumEntries, nameof(maximumEntries));
			MaximumCompressedBytes = Positive(maximumCompressedBytes, nameof(maximumCompressedBytes));
			MaximumExpandedEntryBytes = Positive(maximumExpandedEntryBytes, nameof(maximumExpandedEntryBytes));
			MaximumExpandedAggregateBytes = Positive(maximumExpandedAggregateBytes, nameof(maximumExpandedAggregateBytes));
			MaximumExpansionRatio = Positive(maximumExpansionRatio, nameof(maximumExpansionRatio));
		}

		static int Positive(int value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
		static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
	}

	public sealed record PngReadLimits
	{
		public int MaximumEdge { get; }
		public long MaximumPixels { get; }
		public long MaximumDecodedBytes { get; }
		public int MaximumChunks { get; }
		public long MaximumIdatBytes { get; }
		public int MaximumInflateRatio { get; }

		public static PngReadLimits TrustedEngineDefault { get; } = new(
			4_096, 16_777_216, 64L << 20, 4_096, 32L << 20, 100);

		// System map previews are packaged with the application and therefore not
		// attacker-controlled. Flat shellmap images legitimately compress beyond
		// the user-content limit while remaining bounded by the other dimensions.
		public static PngReadLimits TrustedSystemMapDefault { get; } = new(
			4_096, 16_777_216, 64L << 20, 4_096, 32L << 20, 512);

		// Chrome sheets are application-supplied UI. Flat panels and transparent
		// controls routinely compress beyond the ordinary sprite limit, while the
		// dimensional, decoded-byte, chunk, and IDAT bounds remain in force.
		public static PngReadLimits TrustedChromeDefault { get; } = new(
			4_096, 16_777_216, 64L << 20, 4_096, 32L << 20, 512);

		public PngReadLimits(int maximumEdge, long maximumPixels, long maximumDecodedBytes,
			int maximumChunks, long maximumIdatBytes, int maximumInflateRatio)
		{
			MaximumEdge = Positive(maximumEdge, nameof(maximumEdge));
			MaximumPixels = Positive(maximumPixels, nameof(maximumPixels));
			MaximumDecodedBytes = Positive(maximumDecodedBytes, nameof(maximumDecodedBytes));
			MaximumChunks = Positive(maximumChunks, nameof(maximumChunks));
			MaximumIdatBytes = Positive(maximumIdatBytes, nameof(maximumIdatBytes));
			MaximumInflateRatio = Positive(maximumInflateRatio, nameof(maximumInflateRatio));
		}

		static int Positive(int value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
		static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
	}

	public sealed record YamlEnvelopeLimits
	{
		public long MaximumBytes { get; }
		public int MaximumNodes { get; }
		public int MaximumDepth { get; }
		public int MaximumLineBytes { get; }
		public int MaximumScalarBytes { get; }

		public YamlEnvelopeLimits(long maximumBytes, int maximumNodes, int maximumDepth,
			int maximumLineBytes, int maximumScalarBytes)
		{
			MaximumBytes = Positive(maximumBytes, nameof(maximumBytes));
			MaximumNodes = Positive(maximumNodes, nameof(maximumNodes));
			MaximumDepth = Positive(maximumDepth, nameof(maximumDepth));
			MaximumLineBytes = Positive(maximumLineBytes, nameof(maximumLineBytes));
			MaximumScalarBytes = Positive(maximumScalarBytes, nameof(maximumScalarBytes));
		}

		static int Positive(int value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
		static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
	}

	public sealed record MixEnvelopeLimits
	{
		public int MaximumDepth { get; }
		public int MaximumEntriesPerArchive { get; }
		public int MaximumEntriesAggregate { get; }
		public long MaximumIndexBytes { get; }
		public long MaximumLocalDatabaseBytes { get; }
		public int MaximumLocalDatabaseNames { get; }
		public long MaximumSegmentBytes { get; }
		public long MaximumAddressedBytes { get; }

		public MixEnvelopeLimits(int maximumDepth, int maximumEntriesPerArchive, int maximumEntriesAggregate,
			long maximumIndexBytes, long maximumLocalDatabaseBytes, int maximumLocalDatabaseNames,
			long maximumSegmentBytes, long maximumAddressedBytes)
		{
			MaximumDepth = Positive(maximumDepth, nameof(maximumDepth));
			MaximumEntriesPerArchive = Positive(maximumEntriesPerArchive, nameof(maximumEntriesPerArchive));
			MaximumEntriesAggregate = Positive(maximumEntriesAggregate, nameof(maximumEntriesAggregate));
			MaximumIndexBytes = Positive(maximumIndexBytes, nameof(maximumIndexBytes));
			MaximumLocalDatabaseBytes = Positive(maximumLocalDatabaseBytes, nameof(maximumLocalDatabaseBytes));
			MaximumLocalDatabaseNames = Positive(maximumLocalDatabaseNames, nameof(maximumLocalDatabaseNames));
			MaximumSegmentBytes = Positive(maximumSegmentBytes, nameof(maximumSegmentBytes));
			MaximumAddressedBytes = Positive(maximumAddressedBytes, nameof(maximumAddressedBytes));
		}

		static int Positive(int value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
		static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
	}

	public sealed record IdxBagEnvelopeLimits
	{
		public long MaximumIdxBytes { get; }
		public int MaximumSoundRecords { get; }
		public int ExactNameBytes { get; }
		public long MaximumBagBytes { get; }
		public long MaximumSoundSpanBytes { get; }

		public IdxBagEnvelopeLimits(long maximumIdxBytes, int maximumSoundRecords, int exactNameBytes,
			long maximumBagBytes, long maximumSoundSpanBytes)
		{
			MaximumIdxBytes = Positive(maximumIdxBytes, nameof(maximumIdxBytes));
			MaximumSoundRecords = Positive(maximumSoundRecords, nameof(maximumSoundRecords));
			if (exactNameBytes != 16)
				throw new ArgumentOutOfRangeException(nameof(exactNameBytes), "IDX name fields are exactly 16 bytes.");
			ExactNameBytes = exactNameBytes;
			MaximumBagBytes = Positive(maximumBagBytes, nameof(maximumBagBytes));
			MaximumSoundSpanBytes = Positive(maximumSoundSpanBytes, nameof(maximumSoundSpanBytes));
		}

		static int Positive(int value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
		static long Positive(long value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
	}
}
