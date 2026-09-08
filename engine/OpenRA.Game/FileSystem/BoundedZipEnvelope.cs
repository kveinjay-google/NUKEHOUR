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
using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using OpenRA.Primitives;

namespace OpenRA.FileSystem
{
	public sealed record ValidatedZipEntry(string CanonicalName, long DataOffset,
		long CompressedLength, long ExpandedLength, ushort CompressionMethod);

	public static class BoundedZipEnvelope
	{
		const uint LocalHeaderSignature = 0x04034b50;
		const uint CentralHeaderSignature = 0x02014b50;
		const uint EndOfCentralDirectorySignature = 0x06054b50;
		const int CentralHeaderLength = 46;
		const int EndOfCentralDirectoryLength = 22;
		const int MaximumZipCommentLength = ushort.MaxValue;
		const ushort Utf8Flag = 1 << 11;

		sealed record CentralDirectory(long Offset, long Length, int EntryCount, long ExpandedLength);

		internal sealed record ValidatedZipEntryMetadata(ValidatedZipEntry Entry, uint Crc32);

		public static ValidatedZipEnvelope Validate(Stream ownedPinnedStream, ZipEnvelopeLimits limits,
			PackageReadContext context)
		{
			if (ownedPinnedStream == null)
				throw new ArgumentNullException(nameof(ownedPinnedStream));

			RefCountedStreamLease owner = null;
			try
			{
				if (limits == null)
					throw new ArgumentNullException(nameof(limits));
				if (context == null)
					throw new ArgumentNullException(nameof(context));
				if (context.EnvelopePolicy != null && !ReferenceEquals(context.EnvelopePolicy.Zip, limits))
					throw new InvalidOperationException("The ZIP limits do not match the bound package policy.");
				if (ownedPinnedStream.Position != 0)
					throw new ArgumentException("The owned ZIP stream must be positioned at zero.", nameof(ownedPinnedStream));

				owner = RefCountedStreamLease.TakeOwnership(ownedPinnedStream);
				ownedPinnedStream = null;
				context.ThrowIfCancelledOrExpired();
				if (owner.Length <= 0 || owner.Length > limits.MaximumCompressedBytes)
					throw Limit("The ZIP compressed-size limit was exceeded.");

				var directory = Preflight(owner, limits, context);
				var childContext = context.Descend(directory.EntryCount, directory.ExpandedLength);
				var entries = Parse(owner, directory, limits, childContext);
				var result = new ValidatedZipEnvelope(entries, owner, childContext);
				owner = null;
				return result;
			}
			catch (PackageEnvelopeException)
			{
				throw;
			}
			catch (OverflowException ex)
			{
				throw new PackageEnvelopeException(PackageEnvelopeFailure.ArithmeticOverflow,
					"ZIP envelope arithmetic overflowed.", ex);
			}
			catch (Exception ex) when (ex is ZipException || ex is EndOfStreamException || ex is IOException)
			{
				throw new PackageEnvelopeException(PackageEnvelopeFailure.Malformed, "The ZIP envelope is malformed.", ex);
			}
			finally
			{
				owner?.Dispose();
				ownedPinnedStream?.Dispose();
			}
		}

		static CentralDirectory Preflight(RefCountedStreamLease owner, ZipEnvelopeLimits limits,
			PackageReadContext context)
		{
			if (owner.Length < EndOfCentralDirectoryLength)
				throw Malformed("The ZIP end record is missing.");

			var tailLength = (int)Math.Min(owner.Length,
				EndOfCentralDirectoryLength + (long)MaximumZipCommentLength);
			byte[] tail;
			using (var tailStream = owner.OpenSegment(owner.Length - tailLength, tailLength))
				tail = ReadExactBytes(tailStream, tailLength);

			var endIndex = -1;
			for (var i = tail.Length - EndOfCentralDirectoryLength; i >= 0; i--)
			{
				context.ThrowIfCancelledOrExpired();
				if (ReadUInt32(tail, i) != EndOfCentralDirectorySignature)
					continue;
				var commentLength = ReadUInt16(tail, i + 20);
				if (i + EndOfCentralDirectoryLength + commentLength == tail.Length)
				{
					endIndex = i;
					break;
				}
			}

			if (endIndex < 0)
				throw Malformed("The ZIP end record is malformed.");
			if (ReadUInt16(tail, endIndex + 4) != 0 || ReadUInt16(tail, endIndex + 6) != 0)
				throw Malformed("Multi-disk ZIP archives are not accepted.");

			var entriesOnDisk = ReadUInt16(tail, endIndex + 8);
			var entryCount = ReadUInt16(tail, endIndex + 10);
			if (entryCount == 0 || entriesOnDisk != entryCount)
				throw Malformed("The ZIP central-directory entry count is invalid.");
			if (entryCount > limits.MaximumEntries)
				throw Limit("The ZIP entry-count limit was exceeded.");

			var directoryLength = (long)ReadUInt32(tail, endIndex + 12);
			var directoryOffset = (long)ReadUInt32(tail, endIndex + 16);
			var endOffset = checked(owner.Length - tail.Length + endIndex);
			if (directoryLength < checked(entryCount * (long)CentralHeaderLength) ||
				checked(directoryOffset + directoryLength) != endOffset)
				throw Malformed("The ZIP central-directory range is inconsistent.");

			long expandedTotal = 0;
			using var directory = owner.OpenSegment(directoryOffset, directoryLength);
			using var reader = new BinaryReader(directory, Encoding.UTF8, true);
			for (var i = 0; i < entryCount; i++)
			{
				context.ThrowIfCancelledOrExpired();
				if (directory.Length - directory.Position < CentralHeaderLength ||
					reader.ReadUInt32() != CentralHeaderSignature)
					throw Malformed("The ZIP central-directory header is malformed.");

				reader.ReadUInt16(); // version made by
				var versionNeeded = reader.ReadUInt16();
				var flags = reader.ReadUInt16();
				var method = reader.ReadUInt16();
				directory.Position += 8; // timestamp and CRC
				var compressedLength = (long)reader.ReadUInt32();
				var expandedLength = (long)reader.ReadUInt32();
				var nameLength = reader.ReadUInt16();
				var extraLength = reader.ReadUInt16();
				var commentLength = reader.ReadUInt16();
				var diskNumber = reader.ReadUInt16();
				reader.ReadUInt16(); // internal attributes
				var externalAttributes = reader.ReadUInt32();
				var localOffset = reader.ReadUInt32();

				ValidateCentralScalars(versionNeeded, flags, method, compressedLength, expandedLength,
					nameLength, diskNumber, externalAttributes, localOffset, limits);
				var variableLength = checked((long)nameLength + extraLength + commentLength);
				if (variableLength > directory.Length - directory.Position)
					throw Malformed("The ZIP central-directory entry is truncated.");
				directory.Position += variableLength;

				expandedTotal = checked(expandedTotal + expandedLength);
				if (expandedTotal > limits.MaximumExpandedAggregateBytes)
					throw Limit("The ZIP aggregate expanded-size limit was exceeded.");
			}

			if (directory.Position != directory.Length)
				throw Malformed("The ZIP central-directory length is inconsistent.");
			return new CentralDirectory(directoryOffset, directoryLength, entryCount, expandedTotal);
		}

		static ImmutableArray<ValidatedZipEntryMetadata> Parse(RefCountedStreamLease owner,
			CentralDirectory central, ZipEnvelopeLimits limits, PackageReadContext context)
		{
			var result = ImmutableArray.CreateBuilder<ValidatedZipEntryMetadata>(central.EntryCount);
			var names = new HashSet<string>(StringComparer.Ordinal);
			var ranges = new List<(long Start, long End)>(central.EntryCount);
			long materializedExpandedLength = 0;
			using var directory = owner.OpenSegment(central.Offset, central.Length);
			using var reader = new BinaryReader(directory, Encoding.UTF8, true);
			for (var i = 0; i < central.EntryCount; i++)
			{
				context.ThrowIfCancelledOrExpired();
				if (directory.Length - directory.Position < CentralHeaderLength ||
					reader.ReadUInt32() != CentralHeaderSignature)
					throw Malformed("The ZIP central-directory header changed during validation.");
				reader.ReadUInt16(); // version made by
				var versionNeeded = reader.ReadUInt16();
				var flags = reader.ReadUInt16();
				var method = reader.ReadUInt16();
				reader.ReadUInt16(); // timestamp
				reader.ReadUInt16(); // date
				var crc32 = reader.ReadUInt32();
				var compressedLength = (long)reader.ReadUInt32();
				var expandedLength = (long)reader.ReadUInt32();
				var nameLength = reader.ReadUInt16();
				var extraLength = reader.ReadUInt16();
				var commentLength = reader.ReadUInt16();
				var diskNumber = reader.ReadUInt16();
				reader.ReadUInt16(); // internal attributes
				var externalAttributes = reader.ReadUInt32();
				var localOffsetValue = reader.ReadUInt32();

				ValidateCentralScalars(versionNeeded, flags, method, compressedLength, expandedLength,
					nameLength, diskNumber, externalAttributes, localOffsetValue, limits);
				var variableLength = checked((long)nameLength + extraLength + commentLength);
				if (variableLength > directory.Length - directory.Position)
					throw Malformed("The ZIP central-directory entry changed to a truncated range.");
				var nameBytes = ReadExactBytes(directory, nameLength);
				directory.Position = checked(directory.Position + extraLength + commentLength);
				materializedExpandedLength = checked(materializedExpandedLength + expandedLength);
				if (materializedExpandedLength > limits.MaximumExpandedAggregateBytes)
					throw Limit("The changed ZIP directory exceeds its aggregate expanded-size limit.");

				var name = Canonicalize(DecodeName(nameBytes), limits);
				if (!names.Add(name))
					throw Malformed("The ZIP contains a duplicate canonical path.");

				var localOffset = (long)localOffsetValue;
				var dataOffset = ReadDataOffset(owner, central.Offset, localOffset, versionNeeded, flags, method,
					crc32, compressedLength, expandedLength, nameBytes);
				var endOffset = checked(dataOffset + compressedLength);
				ranges.Add((localOffset, endOffset));
				var entry = new ValidatedZipEntry(name, dataOffset, compressedLength, expandedLength, method);
				result.Add(new ValidatedZipEntryMetadata(entry, crc32));
			}

			if (directory.Position != directory.Length)
				throw Malformed("The ZIP central-directory length changed during validation.");
			if (materializedExpandedLength != central.ExpandedLength)
				throw Malformed("The ZIP expanded aggregate changed after its budget was charged.");

			ranges.Sort((left, right) => left.Start.CompareTo(right.Start));
			for (var i = 1; i < ranges.Count; i++)
				if (ranges[i].Start < ranges[i - 1].End)
					throw Malformed("The ZIP local entry ranges overlap.");

			return result.MoveToImmutable();
		}

		static void ValidateCentralScalars(ushort versionNeeded, ushort flags, ushort method,
			long compressedLength, long expandedLength, ushort nameLength, ushort diskNumber,
			uint externalAttributes, uint localOffset, ZipEnvelopeLimits limits)
		{
			if (versionNeeded >= 45 || compressedLength == uint.MaxValue || expandedLength == uint.MaxValue ||
				localOffset == uint.MaxValue)
				throw Malformed("ZIP64 entries are not accepted.");
			if ((flags & ~Utf8Flag) != 0)
				throw Malformed("Encrypted, descriptor, and noncanonical ZIP flags are not accepted.");
			if (method != (ushort)CompressionMethod.Stored && method != (ushort)CompressionMethod.Deflated)
				throw Malformed("The ZIP compression method is unsupported.");
			if (diskNumber != 0 || nameLength == 0)
				throw Malformed("The ZIP central-directory entry is invalid.");
			if (nameLength > limits.MaximumPathUtf8Bytes)
				throw Limit("The ZIP path-length limit was exceeded.");
			if ((externalAttributes & 0x10) != 0 || IsUnixSpecial(unchecked((int)externalAttributes)))
				throw Malformed("ZIP directories, links, and special entries are not accepted.");
			if (expandedLength > limits.MaximumExpandedEntryBytes)
				throw Limit("The ZIP expanded-entry limit was exceeded.");
			if (method == (ushort)CompressionMethod.Stored && compressedLength != expandedLength)
				throw Malformed("A stored ZIP entry has contradictory sizes.");
			if (expandedLength > 0 && (compressedLength == 0 ||
				expandedLength > checked(compressedLength * limits.MaximumExpansionRatio)))
				throw Limit("The ZIP expansion-ratio limit was exceeded.");
		}

		static long ReadDataOffset(RefCountedStreamLease owner, long centralOffset, long localOffset,
			ushort versionNeeded, ushort flags, ushort method, uint crc32, long compressedLength, long expandedLength,
			byte[] centralName)
		{
			if (localOffset < 0 || checked(localOffset + 30) > centralOffset)
				throw Malformed("The ZIP local header is out of range.");
			using var stream = owner.OpenSegment(localOffset, centralOffset - localOffset);
			using var reader = new BinaryReader(stream, Encoding.UTF8, true);
			if (reader.ReadUInt32() != LocalHeaderSignature)
				throw Malformed("The ZIP local header signature is invalid.");
			var localVersionNeeded = reader.ReadUInt16();
			var localFlags = reader.ReadUInt16();
			var localMethod = reader.ReadUInt16();
			stream.Position += 4;
			var localCrc = reader.ReadUInt32();
			var localCompressed = (long)reader.ReadUInt32();
			var localExpanded = (long)reader.ReadUInt32();
			var nameLength = reader.ReadUInt16();
			var extraLength = reader.ReadUInt16();
			if (localVersionNeeded != versionNeeded || localFlags != flags || localMethod != method || localCrc != crc32 ||
				localCompressed != compressedLength || localExpanded != expandedLength ||
				nameLength != centralName.Length)
				throw Malformed("The ZIP local and central headers are inconsistent.");
			var localName = ReadExactBytes(stream, nameLength);
			if (!localName.SequenceEqual(centralName))
				throw Malformed("The ZIP local and central names are inconsistent.");
			var dataOffset = checked(localOffset + 30L + nameLength + extraLength);
			if (checked(dataOffset + compressedLength) > centralOffset)
				throw Malformed("The ZIP entry data is out of range.");
			return dataOffset;
		}

		static string Canonicalize(string name, ZipEnvelopeLimits limits)
		{
			if (string.IsNullOrEmpty(name) || name.Contains('\\') || name.Contains(':') ||
				name.StartsWith('/') || name.EndsWith('/') || name.Any(character => character < ' ' || character == 0x7f))
				throw Malformed("The ZIP path is not canonical.");
			var parts = name.Split('/');
			if (parts.Length > limits.MaximumPathDepth)
				throw Limit("The ZIP path-depth limit was exceeded.");
			if (parts.Any(part => part.Length == 0 || part == "." || part == ".."))
				throw Malformed("The ZIP path traversal syntax is invalid.");
			var utf8 = new UTF8Encoding(false, true);
			if (utf8.GetByteCount(name) > limits.MaximumPathUtf8Bytes)
				throw Limit("The ZIP path-length limit was exceeded.");
			return name.ToLowerInvariant();
		}

		static string DecodeName(byte[] name)
		{
			try
			{
				return new UTF8Encoding(false, true).GetString(name);
			}
			catch (DecoderFallbackException ex)
			{
				throw new PackageEnvelopeException(PackageEnvelopeFailure.Malformed,
					"The ZIP path is not valid UTF-8.", ex);
			}
		}

		static byte[] ReadExactBytes(Stream stream, int length)
		{
			var bytes = new byte[length];
			var offset = 0;
			while (offset < length)
			{
				var read = stream.Read(bytes, offset, length - offset);
				if (read == 0)
					throw new EndOfStreamException();
				offset += read;
			}

			return bytes;
		}

		static ushort ReadUInt16(byte[] bytes, int offset) =>
			(ushort)(bytes[offset] | bytes[offset + 1] << 8);

		static uint ReadUInt32(byte[] bytes, int offset) =>
			(uint)(bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24);

		static bool IsUnixSpecial(int attributes)
		{
			var mode = (attributes >> 16) & 0xF000;
			return mode != 0 && mode != 0x8000;
		}

		static PackageEnvelopeException Malformed(string message) =>
			new(PackageEnvelopeFailure.Malformed, message);

		static PackageEnvelopeException Limit(string message) =>
			new(PackageEnvelopeFailure.LimitExceeded, message);
	}

	public sealed class ValidatedZipEnvelope : IDisposable
	{
		RefCountedStreamLease owner;
		readonly PackageReadContext context;
		readonly ImmutableArray<BoundedZipEnvelope.ValidatedZipEntryMetadata> metadata;
		public ImmutableArray<ValidatedZipEntry> Entries { get; }

		internal ValidatedZipEnvelope(ImmutableArray<BoundedZipEnvelope.ValidatedZipEntryMetadata> metadata,
			RefCountedStreamLease ownedLease, PackageReadContext context)
		{
			this.metadata = metadata;
			Entries = metadata.Select(item => item.Entry).ToImmutableArray();
			owner = ownedLease;
			this.context = context;
		}

		public IReadOnlyPackage OpenPackage(PackageReadContext context) => OpenPackage(context, null);

		internal IReadOnlyPackage OpenPackage(PackageReadContext context, FileSystem creatingFileSystem)
		{
			var current = System.Threading.Volatile.Read(ref owner);
			if (current == null)
				throw new ObjectDisposedException(nameof(ValidatedZipEnvelope));
			if (!this.context.SharesLedger(context) ||
				!ReferenceEquals(this.context.EnvelopePolicy, context?.EnvelopePolicy))
				throw new InvalidOperationException("The ZIP package context belongs to another aggregate ledger.");
			context.ThrowIfCancelledOrExpired();
			return new ValidatedZipPackage(metadata, current.Acquire(), this.context, creatingFileSystem);
		}

		public void Dispose()
		{
			System.Threading.Interlocked.Exchange(ref owner, null)?.Dispose();
			GC.SuppressFinalize(this);
		}

		sealed class ValidatedZipPackage : IReadOnlyPackage
		{
			readonly Dictionary<string, BoundedZipEnvelope.ValidatedZipEntryMetadata> entries;
			readonly PackageReadContext context;
			readonly FileSystem creatingFileSystem;
			RefCountedStreamLease owner;
			public string Name => "validated.zip";
			public IEnumerable<string> Contents => entries.Keys;

			public ValidatedZipPackage(ImmutableArray<BoundedZipEnvelope.ValidatedZipEntryMetadata> entries,
				RefCountedStreamLease ownedLease, PackageReadContext context, FileSystem creatingFileSystem)
			{
				this.entries = entries.ToDictionary(item => item.Entry.CanonicalName, StringComparer.Ordinal);
				owner = ownedLease;
				this.context = context;
				this.creatingFileSystem = creatingFileSystem;
			}

			public Stream GetStream(string filename)
			{
				var current = System.Threading.Volatile.Read(ref owner);
				if (current == null)
					throw new ObjectDisposedException(nameof(ValidatedZipPackage));
				context.ThrowIfCancelledOrExpired();
				if (filename == null || !entries.TryGetValue(filename.ToLowerInvariant(), out var entry))
					return null;

				var compressed = current.OpenSegment(entry.Entry.DataOffset, entry.Entry.CompressedLength);
				try
				{
					return new ValidatedZipEntryStream(compressed,
						entry.Entry.CompressionMethod == (ushort)CompressionMethod.Deflated,
						entry.Entry.ExpandedLength, entry.Crc32, context);
				}
				catch
				{
					compressed.Dispose();
					throw;
				}
			}

			public bool Contains(string filename) => filename != null && entries.ContainsKey(filename.ToLowerInvariant());

			public IReadOnlyPackage OpenPackage(string filename, FileSystem fileSystem)
			{
				if (creatingFileSystem == null || !ReferenceEquals(creatingFileSystem, fileSystem))
					throw new InvalidOperationException("A bounded ZIP package rejects a replacement file system.");
				var stream = GetStream(filename);
				if (stream == null)
					return null;
				try
				{
					if (creatingFileSystem.TryParsePackage(stream, filename, context, out var package))
						return package;
					stream.Dispose();
					return null;
				}
				catch
				{
					stream.Dispose();
					throw;
				}
			}

			public void Dispose()
			{
				System.Threading.Interlocked.Exchange(ref owner, null)?.Dispose();
			}

			sealed class ValidatedZipEntryStream : Stream
			{
				Stream compressed;
				Stream decoded;
				readonly bool deflated;
				readonly long expandedLength;
				readonly uint expectedCrc32;
				readonly PackageReadContext context;
				readonly Crc32 crc32 = new();
				Inflater inflater;
				long position;
				bool verified;

				public ValidatedZipEntryStream(Stream compressed, bool deflated, long expandedLength,
					uint expectedCrc32, PackageReadContext context)
				{
					this.compressed = compressed;
					this.deflated = deflated;
					decoded = CreateDecoder();
					this.expandedLength = expandedLength;
					this.expectedCrc32 = expectedCrc32;
					this.context = context;
				}

				public override bool CanRead => decoded != null;
				public override bool CanSeek => decoded != null;
				public override bool CanWrite => false;
				public override long Length => expandedLength;
				public override long Position
				{
					get => position;
					set => Seek(value, SeekOrigin.Begin);
				}

				public override int Read(byte[] buffer, int offset, int count)
				{
					if (buffer == null)
						throw new ArgumentNullException(nameof(buffer));
					if (offset < 0)
						throw new ArgumentOutOfRangeException(nameof(offset));
					if (count < 0)
						throw new ArgumentOutOfRangeException(nameof(count));
					if (offset > buffer.Length - count)
						throw new ArgumentException("The requested range exceeds the destination buffer.", nameof(buffer));
					var current = decoded ?? throw new ObjectDisposedException(nameof(ValidatedZipEntryStream));
					context.ThrowIfCancelledOrExpired();
					if (position == expandedLength)
					{
						VerifyEnd(current);
						return 0;
					}

					if (count == 0)
						return 0;

					var wanted = (int)Math.Min(count, expandedLength - position);
					int read;
					try
					{
						read = current.Read(buffer, offset, wanted);
					}
					catch (PackageEnvelopeException)
					{
						throw;
					}
					catch (Exception ex) when (ex is SharpZipBaseException || ex is InvalidDataException || ex is IOException)
					{
						throw new PackageEnvelopeException(PackageEnvelopeFailure.Malformed,
							"The ZIP entry payload is malformed.", ex);
					}

					if (read == 0)
						throw new PackageEnvelopeException(PackageEnvelopeFailure.Malformed,
							"The ZIP entry is truncated.");
					crc32.Update(new ArraySegment<byte>(buffer, offset, read));
					position = checked(position + read);
					if (position == expandedLength)
						VerifyEnd(current);
					return read;
				}

				void VerifyEnd(Stream current)
				{
					if (verified)
						return;

					int extra;
					try
					{
						extra = current.ReadByte();
					}
					catch (PackageEnvelopeException)
					{
						throw;
					}
					catch (Exception ex) when (ex is SharpZipBaseException || ex is InvalidDataException || ex is IOException)
					{
						throw new PackageEnvelopeException(PackageEnvelopeFailure.Malformed,
							"The ZIP entry payload is malformed.", ex);
					}

					if (extra != -1)
						throw new PackageEnvelopeException(PackageEnvelopeFailure.Malformed,
							"The ZIP entry expands beyond its declared size.");
					if (deflated && (inflater == null || !inflater.IsFinished ||
						inflater.TotalIn != compressed.Length))
						throw new PackageEnvelopeException(PackageEnvelopeFailure.Malformed,
							"The ZIP entry did not consume its exact declared compressed range.");
					if (unchecked((uint)crc32.Value) != expectedCrc32)
						throw new PackageEnvelopeException(PackageEnvelopeFailure.Malformed,
							"The ZIP entry checksum is invalid.");
					verified = true;
				}

				Stream CreateDecoder()
				{
					if (!deflated)
					{
						inflater = null;
						return compressed;
					}

					inflater = new Inflater(true);
					return new InflaterInputStream(compressed, inflater) { IsStreamOwner = false };
				}

				public override void Flush() { }

				public override long Seek(long offset, SeekOrigin origin)
				{
					var target = origin switch
					{
						SeekOrigin.Begin => offset,
						SeekOrigin.Current => checked(position + offset),
						SeekOrigin.End => checked(expandedLength + offset),
						_ => throw new ArgumentOutOfRangeException(nameof(origin)),
					};
					if (target < 0 || target > expandedLength)
						throw new IOException("Attempted to seek outside the ZIP entry.");

					var current = decoded ?? throw new ObjectDisposedException(nameof(ValidatedZipEntryStream));
					if (target == position)
						return position;
					if (!ReferenceEquals(current, compressed))
						current.Dispose();
					compressed.Position = 0;
					decoded = CreateDecoder();
					crc32.Reset();
					position = 0;
					verified = false;
					var scratch = new byte[8192];
					while (position < target)
						Read(scratch, 0, (int)Math.Min(scratch.Length, target - position));
					return position;
				}

				public override void SetLength(long value) => throw new NotSupportedException();
				public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

				protected override void Dispose(bool disposing)
				{
					if (disposing)
					{
						var closeDecoded = System.Threading.Interlocked.Exchange(ref decoded, null);
						var closeCompressed = System.Threading.Interlocked.Exchange(ref compressed, null);
						inflater = null;
						closeDecoded?.Dispose();
						if (!ReferenceEquals(closeDecoded, closeCompressed))
							closeCompressed?.Dispose();
					}

					base.Dispose(disposing);
				}
			}
		}
	}
}
