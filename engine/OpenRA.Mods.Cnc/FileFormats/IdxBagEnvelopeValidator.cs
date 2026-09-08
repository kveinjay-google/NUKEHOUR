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
using System.Text;
using OpenRA.FileSystem;
using OpenRA.Mods.Cnc.FileSystem;
using OpenRA.Primitives;

namespace OpenRA.Mods.Cnc.FileFormats
{
	public enum IdxBagEncoding
	{
		Pcm16,
		ImaAdpcm,
	}

	public sealed record ValidatedIdxBagEntry(string CanonicalName, long Offset, long Length,
		uint SampleRate, uint Flags, uint ChunkSize, int Channels, IdxBagEncoding Encoding,
		ushort SamplesPerBlock, uint AverageBytesPerSecond);

	public static class IdxBagEnvelopeValidator
	{
		public static ValidatedIdxBagIndex Validate(ValidatedMixEntrySource ownedIdx,
			ValidatedMixEntrySource ownedBag, IdxBagEnvelopeLimits limits, PackageReadContext context)
		{
			RefCountedStreamLease bagOwner = null;
			try
			{
				if (ownedIdx == null)
					throw new ArgumentNullException(nameof(ownedIdx));
				if (ownedBag == null)
					throw new ArgumentNullException(nameof(ownedBag));
				if (limits == null)
					throw new ArgumentNullException(nameof(limits));
				if (context == null)
					throw new ArgumentNullException(nameof(context));
				if (context.EnvelopePolicy != null && !ReferenceEquals(context.EnvelopePolicy.Audio, limits))
					throw new InvalidOperationException("The IDX/BAG limits do not match the bound package policy.");
				if (!ownedIdx.SharesBacking(ownedBag) || !ownedIdx.SharesLedger(context) || !ownedBag.SharesLedger(context))
					throw Malformed("The IDX/BAG pair does not share one validated archive identity.");
				if (!ownedIdx.MatchesPolicy(context) || !ownedBag.MatchesPolicy(context))
					throw Malformed("The IDX/BAG pair does not share one validated package policy.");
				if (ownedIdx.CanonicalName != "audio.idx" || ownedBag.CanonicalName != "audio.bag")
					throw Malformed("The validated audio pair names are not exact.");
				context.ThrowIfCancelledOrExpired();
				if (ownedIdx.Length > limits.MaximumIdxBytes || ownedBag.Length > limits.MaximumBagBytes)
					throw Limit("The IDX/BAG file-size limit was exceeded.");

				ImmutableArray<ValidatedIdxBagEntry> entries;
				PackageReadContext childContext;
				using (var idx = ownedIdx.OpenRead())
					entries = Parse(idx, ownedIdx.Length, ownedBag.Length, limits,
						ownedIdx.ReadContext, out childContext);
				bagOwner = RefCountedStreamLease.TakeOwnership(ownedBag.OpenRead());
				var result = new ValidatedIdxBagIndex(entries, bagOwner, childContext);
				bagOwner = null;
				return result;
			}
			catch (PackageEnvelopeException)
			{
				throw;
			}
			catch (OverflowException ex)
			{
				throw new PackageEnvelopeException(PackageEnvelopeFailure.ArithmeticOverflow,
					"IDX/BAG envelope arithmetic overflowed.", ex);
			}
			catch (Exception ex) when (ex is IOException || ex is EndOfStreamException || ex is ArgumentException)
			{
				throw new PackageEnvelopeException(PackageEnvelopeFailure.Malformed,
					"The IDX/BAG envelope is malformed.", ex);
			}
			finally
			{
				bagOwner?.Dispose();
				ownedIdx?.Dispose();
				ownedBag?.Dispose();
			}
		}

		static ImmutableArray<ValidatedIdxBagEntry> Parse(Stream stream, long idxLength, long bagLength,
			IdxBagEnvelopeLimits limits, PackageReadContext context, out PackageReadContext childContext)
		{
			if (idxLength < 12)
				throw Malformed("The IDX header is truncated.");
			using var reader = new BinaryReader(stream, Encoding.ASCII, true);
			if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "GABA" || reader.ReadInt32() != 2)
				throw Malformed("The IDX magic or version is invalid.");
			var count = reader.ReadInt32();
			if (count <= 0)
				throw Malformed("The IDX sound-record count is invalid.");
			if (count > limits.MaximumSoundRecords)
				throw Limit("The IDX sound-record limit was exceeded.");
			var expectedLength = checked(12L + count * 36L);
			if (expectedLength > limits.MaximumIdxBytes)
				throw Limit("The IDX byte limit was exceeded.");
			if (expectedLength != idxLength)
				throw Malformed("The IDX length does not match its record count.");

			// Scalar preflight uses a fixed stack buffer and charges the shared ledger before table allocation.
			long addressedBytes = 0;
			for (var i = 0; i < count; i++)
			{
				context.ThrowIfCancelledOrExpired();
				ReadName(reader.BaseStream, createString: false, out _);
				var offset = reader.ReadUInt32();
				var length = reader.ReadUInt32();
				var sampleRate = reader.ReadUInt32();
				var flags = reader.ReadUInt32();
				var chunkSize = reader.ReadUInt32();
				ValidateRecord(offset, length, sampleRate, flags, chunkSize, bagLength, limits,
					out _, out _, out _, out _);
				addressedBytes = checked(addressedBytes + length);
			}

			childContext = context.Descend(count, addressedBytes);
			stream.Position = 12;
			var names = new HashSet<string>(StringComparer.Ordinal);
			var result = ImmutableArray.CreateBuilder<ValidatedIdxBagEntry>(count);
			long materializedAddressedBytes = 0;
			for (var i = 0; i < count; i++)
			{
				childContext.ThrowIfCancelledOrExpired();
				ReadName(reader.BaseStream, createString: true, out var name);
				if (!names.Add(name))
					throw Malformed("The IDX contains a duplicate folded name.");
				var offset = reader.ReadUInt32();
				var length = reader.ReadUInt32();
				var sampleRate = reader.ReadUInt32();
				var flags = reader.ReadUInt32();
				var chunkSize = reader.ReadUInt32();
				ValidateRecord(offset, length, sampleRate, flags, chunkSize, bagLength, limits,
					out var channels, out var encoding, out var samplesPerBlock,
					out var averageBytesPerSecond);
				materializedAddressedBytes = checked(materializedAddressedBytes + length);

				result.Add(new ValidatedIdxBagEntry(name + ".wav", offset, length, sampleRate, flags,
					chunkSize, channels, encoding, samplesPerBlock, averageBytesPerSecond));
			}
			if (materializedAddressedBytes != addressedBytes)
				throw Malformed("The IDX records changed after their aggregate budget was charged.");

			return result.MoveToImmutable();
		}

		static void ReadName(Stream stream, bool createString, out string name)
		{
			Span<byte> bytes = stackalloc byte[16];
			var total = 0;
			while (total < bytes.Length)
			{
				var read = stream.Read(bytes[total..]);
				if (read == 0)
					throw Malformed("The IDX name field is truncated.");
				total += read;
			}

			var nul = bytes.IndexOf((byte)0);
			if (nul <= 0 || nul > 15)
				throw Malformed("The IDX name is not exactly NUL padded.");
			for (var i = nul; i < bytes.Length; i++)
				if (bytes[i] != 0)
					throw Malformed("The IDX name is not exactly NUL padded.");
			for (var i = 0; i < nul; i++)
				if (!(bytes[i] >= 'A' && bytes[i] <= 'Z') && !(bytes[i] >= 'a' && bytes[i] <= 'z') &&
					!(bytes[i] >= '0' && bytes[i] <= '9') && bytes[i] != '_' && bytes[i] != '-')
					throw Malformed("The IDX name contains invalid ASCII.");
			name = createString ? Encoding.ASCII.GetString(bytes[..nul]).ToLowerInvariant() : null;
		}

		static void ValidateRecord(uint offset, uint length, uint sampleRate, uint flags, uint chunkSize,
			long bagLength, IdxBagEnvelopeLimits limits, out int channels, out IdxBagEncoding encoding,
			out ushort samplesPerBlock, out uint averageBytesPerSecond)
		{
			if (sampleRate == 0 || length == 0)
				throw Malformed("The IDX sound span or rate is invalid.");
			if (length > limits.MaximumSoundSpanBytes)
				throw Limit("The IDX sound-span limit was exceeded.");
			var end = checked(offset + length);
			if (end > bagLength)
				throw Malformed("The IDX sound span exceeds the BAG.");

			channels = (flags & 1) == 0 ? 1 : 2;
			samplesPerBlock = 0;
			switch (flags)
			{
				case 6:
				case 7:
					encoding = IdxBagEncoding.Pcm16;
					if (chunkSize != 0 || length % (uint)(2 * channels) != 0)
						throw Malformed("The PCM IDX fields are invalid.");
					averageBytesPerSecond = checked((uint)(2UL * (uint)channels * sampleRate));
					break;
				case 8:
				case 9:
					encoding = IdxBagEncoding.ImaAdpcm;
					var headerBytes = 4U * (uint)channels;
					if (chunkSize < headerBytes + 1 || chunkSize > ushort.MaxValue)
						throw Malformed("The IMA block size is invalid.");
					var samples = checked(((chunkSize - headerBytes) * 2U / (uint)channels) + 1U);
					if (samples == 0 || samples > ushort.MaxValue)
						throw Malformed("The IMA samples-per-block value is invalid.");
					var remainder = length % chunkSize;
					if (remainder != 0 && remainder < headerBytes)
						throw Malformed("The IMA partial block does not contain a complete channel header.");
					samplesPerBlock = (ushort)samples;
					var average = checked((ulong)sampleRate * chunkSize / samples);
					if (average == 0 || average > uint.MaxValue)
						throw Malformed("The IMA average byte rate is invalid.");
					averageBytesPerSecond = (uint)average;
					_ = CalculateImaDecodedSamples(length, chunkSize, channels, samples);
					break;
				default:
					throw Malformed("The IDX flags are not in the closed PCM/IMA matrix.");
			}
		}

		internal static uint CalculateImaDecodedSamples(uint length, uint chunkSize, int channels, uint samplesPerBlock)
		{
			var headerBytes = 4U * (uint)channels;
			var fullBlocks = length / chunkSize;
			var remainder = length % chunkSize;
			ulong decoded = checked((ulong)fullBlocks * samplesPerBlock);
			if (remainder != 0)
			{
				if (remainder < headerBytes)
					throw Malformed("The IMA partial block does not contain a complete channel header.");
				decoded = checked(decoded + ((remainder - headerBytes) * 2U / (uint)channels) + 1U);
			}
			if (decoded > uint.MaxValue)
				throw Malformed("The IMA decoded sample count is invalid.");
			return (uint)decoded;
		}

		static PackageEnvelopeException Malformed(string message) =>
			new(PackageEnvelopeFailure.Malformed, message);

		static PackageEnvelopeException Limit(string message) =>
			new(PackageEnvelopeFailure.LimitExceeded, message);
	}

	public sealed class ValidatedIdxBagIndex : IDisposable
	{
		RefCountedStreamLease bagOwner;
		readonly PackageReadContext context;
		public ImmutableArray<ValidatedIdxBagEntry> Entries { get; }

		internal ValidatedIdxBagIndex(ImmutableArray<ValidatedIdxBagEntry> entries,
			RefCountedStreamLease ownedBag, PackageReadContext context)
		{
			Entries = entries;
			bagOwner = ownedBag;
			this.context = context;
		}

		public IReadOnlyPackage OpenPackage(string name, PackageReadContext context)
		{
			var current = System.Threading.Volatile.Read(ref bagOwner);
			if (current == null)
				throw new ObjectDisposedException(nameof(ValidatedIdxBagIndex));
			if (!StringComparer.Ordinal.Equals(name, "audio.bag"))
				throw new ArgumentException("Only the validated canonical audio.bag package may be opened.", nameof(name));
			if (!this.context.SharesLedger(context) ||
				!ReferenceEquals(this.context.EnvelopePolicy, context?.EnvelopePolicy))
				throw new InvalidOperationException("The audio package context belongs to another aggregate ledger.");
			context.ThrowIfCancelledOrExpired();
			return new ValidatedBagPackage(name, Entries, current.Acquire(), this.context);
		}

		public void Dispose()
		{
			System.Threading.Interlocked.Exchange(ref bagOwner, null)?.Dispose();
			GC.SuppressFinalize(this);
		}

		sealed class ValidatedBagPackage : IReadOnlyPackage
		{
			RefCountedStreamLease bagOwner;
			readonly Dictionary<string, ValidatedIdxBagEntry> entries;
			readonly PackageReadContext context;
			public string Name { get; }
			public IEnumerable<string> Contents => entries.Keys;

			public ValidatedBagPackage(string name, ImmutableArray<ValidatedIdxBagEntry> entries,
				RefCountedStreamLease ownedBag, PackageReadContext context)
			{
				Name = name;
				this.entries = entries.ToDictionary(entry => entry.CanonicalName, StringComparer.Ordinal);
				bagOwner = ownedBag;
				this.context = context;
			}

			public Stream GetStream(string filename)
			{
				var current = System.Threading.Volatile.Read(ref bagOwner);
				if (current == null)
					throw new ObjectDisposedException(nameof(ValidatedBagPackage));
				context.ThrowIfCancelledOrExpired();
				if (filename == null || !entries.TryGetValue(filename.ToLowerInvariant(), out var entry))
					return null;
				var header = BuildWaveHeader(entry);
				var data = current.OpenSegment(entry.Offset, entry.Length);
				return new OwnedConcatenatedReadStream(header, data);
			}

			public bool Contains(string filename) => filename != null && entries.ContainsKey(filename.ToLowerInvariant());
			public IReadOnlyPackage OpenPackage(string filename, OpenRA.FileSystem.FileSystem fileSystem) => null;

			public void Dispose()
			{
				System.Threading.Interlocked.Exchange(ref bagOwner, null)?.Dispose();
			}

			static Stream BuildWaveHeader(ValidatedIdxBagEntry entry)
			{
				var stream = new MemoryStream();
				using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
				writer.Write(Encoding.ASCII.GetBytes("RIFF"));
				writer.Write(checked((uint)entry.Length + (entry.Encoding == IdxBagEncoding.Pcm16 ? 36U : 52U)));
				writer.Write(Encoding.ASCII.GetBytes("WAVE"));
				writer.Write(Encoding.ASCII.GetBytes("fmt "));
				if (entry.Encoding == IdxBagEncoding.Pcm16)
				{
					writer.Write(16);
					writer.Write((ushort)1);
					writer.Write((ushort)entry.Channels);
					writer.Write(entry.SampleRate);
					writer.Write(entry.AverageBytesPerSecond);
					writer.Write((ushort)(2 * entry.Channels));
					writer.Write((ushort)16);
				}
				else
				{
					writer.Write(20);
					writer.Write((ushort)17);
					writer.Write((ushort)entry.Channels);
					writer.Write(entry.SampleRate);
					writer.Write(entry.AverageBytesPerSecond);
					writer.Write((ushort)entry.ChunkSize);
					writer.Write((ushort)4);
					writer.Write((ushort)2);
					writer.Write(entry.SamplesPerBlock);
					writer.Write(Encoding.ASCII.GetBytes("fact"));
					writer.Write(4);
					writer.Write(IdxBagEnvelopeValidator.CalculateImaDecodedSamples(
						(uint)entry.Length, entry.ChunkSize, entry.Channels, entry.SamplesPerBlock));
				}

				writer.Write(Encoding.ASCII.GetBytes("data"));
				writer.Write((uint)entry.Length);
				stream.Position = 0;
				return stream;
			}
		}

		sealed class OwnedConcatenatedReadStream : Stream
		{
			Stream first;
			Stream second;
			public OwnedConcatenatedReadStream(Stream first, Stream second)
			{
				this.first = first;
				this.second = second;
			}

			public override bool CanRead => first != null;
			public override bool CanSeek => false;
			public override bool CanWrite => false;
			public override long Length => checked(first.Length + second.Length);
			public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
			public override int Read(byte[] buffer, int offset, int count)
			{
				if (first == null)
					throw new ObjectDisposedException(nameof(OwnedConcatenatedReadStream));
				var read = first.Read(buffer, offset, count);
				return read != 0 ? read : second.Read(buffer, offset, count);
			}
			public override void Flush() { }
			public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
			public override void SetLength(long value) => throw new NotSupportedException();
			public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					first?.Dispose();
					second?.Dispose();
					first = null;
					second = null;
				}
				base.Dispose(disposing);
			}
		}
	}
}
