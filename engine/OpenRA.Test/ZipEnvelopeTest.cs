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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip;
using NUnit.Framework;
using OpenRA.FileSystem;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class ZipEnvelopeTest
	{
		static ZipEnvelopeLimits Limits(int entries = 8, long compressed = 4096, long expandedEntry = 1024,
			long expandedAggregate = 2048, int ratio = 100, int pathBytes = 64, int depth = 1) =>
			new(pathBytes, depth, entries, compressed, expandedEntry, expandedAggregate, ratio);

		static PackageReadContext Context(int entries = 32, long bytes = 8192) =>
			new(default, new NeverDeadline(), 8, entries, bytes);

		[Test]
		public void ContextChargesOneSharedMonotonicLedger()
		{
			var root = Context(entries: 3, bytes: 10);
			var first = root.Descend(2, 6);

			Assert.That(root.RemainingEntries, Is.EqualTo(1));
			Assert.That(first.RemainingAddressedBytes, Is.EqualTo(4));
			Assert.Throws<PackageEnvelopeException>(() => first.Descend(2, 0));
			Assert.Throws<PackageEnvelopeException>(() => root.Descend(0, 5));
			Assert.Throws<ArgumentOutOfRangeException>(() => root.Descend(-1, 0));
			Assert.Throws<ArgumentOutOfRangeException>(() => root.Descend(0, -1));
		}

		[Test]
		public void ValidatedEnvelopeKeepsPinnedChildrenAliveAcrossParentDisposal()
		{
			var backing = new TrackingStream(BuildZip(("map.yaml", "abc", CompressionMethod.Stored)));
			var context = Context();
			var envelope = BoundedZipEnvelope.Validate(backing, Limits(), context);
			var package = envelope.OpenPackage(context);
			var child = package.GetStream("map.yaml");

			envelope.Dispose();
			package.Dispose();
			Assert.That(new StreamReader(child, Encoding.ASCII, false, 1024, true).ReadToEnd(), Is.EqualTo("abc"));
			Assert.That(backing.DisposeCalls, Is.EqualTo(0));

			child.Dispose();
			Assert.That(backing.DisposeCalls, Is.EqualTo(1));
			Assert.Throws<ObjectDisposedException>(() => envelope.OpenPackage(context));
		}

		[Test]
		public void EnvelopeRejectsReplacementContextAndDisposesTransferredInput()
		{
			var backing = new TrackingStream(BuildZip(("map.yaml", "abc", CompressionMethod.Stored)));
			using var envelope = BoundedZipEnvelope.Validate(backing, Limits(), Context());

			Assert.Throws<InvalidOperationException>(() => envelope.OpenPackage(Context()));
			envelope.Dispose();
			Assert.That(backing.DisposeCalls, Is.EqualTo(1));
		}

		[TestCase("../map.yaml")]
		[TestCase("/map.yaml")]
		[TestCase("folder/map.yaml")]
		[TestCase("folder\\map.yaml")]
		public void UnsafeOrOverDepthPathsFailBeforeAHandleEscapes(string path)
		{
			var bytes = path == "/map.yaml" ? BuildAbsolutePathZip() :
				BuildZip((path, "abc", CompressionMethod.Stored));
			var backing = new TrackingStream(bytes);

			Assert.Throws<PackageEnvelopeException>(() => BoundedZipEnvelope.Validate(backing, Limits(), Context()));
			Assert.That(backing.DisposeCalls, Is.EqualTo(1));
		}

		static byte[] BuildAbsolutePathZip()
		{
			var bytes = BuildZip(("xmap.yaml", "abc", CompressionMethod.Stored));
			bytes[30] = (byte)'/';
			for (var i = 30; i <= bytes.Length - 46; i++)
				if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i, 4)) == 0x02014b50)
				{
					bytes[i + 46] = (byte)'/';
					return bytes;
				}

			throw new InvalidOperationException("Synthetic ZIP central directory was not found.");
		}

		[Test]
		public void DuplicateCanonicalPathsAndEntryBudgetsAreClosed()
		{
			var duplicate = new TrackingStream(BuildZip(
				("MAP.YAML", "a", CompressionMethod.Stored),
				("map.yaml", "b", CompressionMethod.Stored)));
			Assert.Throws<PackageEnvelopeException>(() => BoundedZipEnvelope.Validate(duplicate, Limits(), Context()));
			Assert.That(duplicate.DisposeCalls, Is.EqualTo(1));

			var count = new TrackingStream(BuildZip(
				("a", "a", CompressionMethod.Stored),
				("b", "b", CompressionMethod.Stored)));
			Assert.Throws<PackageEnvelopeException>(() => BoundedZipEnvelope.Validate(count, Limits(entries: 1), Context()));
			Assert.That(count.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void PathByteAndDepthLimitsUseExactTypedBoundaries()
		{
			var nested = BuildZip(("folder/map.yaml", "a", CompressionMethod.Stored));
			BoundedZipEnvelope.Validate(new TrackingStream(nested), Limits(depth: 2), Context()).Dispose();
			var depthFailure = Assert.Throws<PackageEnvelopeException>(() => BoundedZipEnvelope.Validate(
				new TrackingStream(nested), Limits(depth: 1), Context()));
			Assert.That(depthFailure.Failure, Is.EqualTo(PackageEnvelopeFailure.LimitExceeded));

			var path = BuildZip(("map.yaml", "a", CompressionMethod.Stored));
			BoundedZipEnvelope.Validate(new TrackingStream(path), Limits(pathBytes: 8), Context()).Dispose();
			var pathFailure = Assert.Throws<PackageEnvelopeException>(() => BoundedZipEnvelope.Validate(
				new TrackingStream(path), Limits(pathBytes: 7), Context()));
			Assert.That(pathFailure.Failure, Is.EqualTo(PackageEnvelopeFailure.LimitExceeded));
		}

		[Test]
		public void ExpandedAndCompressedLimitsCoverBelowExactAndAbove()
		{
			var bytes = BuildZip(("a", "abcd", CompressionMethod.Stored));
			foreach (var limit in new long[] { bytes.Length - 1, bytes.Length })
			{
				var stream = new TrackingStream(bytes);
				if (limit < bytes.Length)
					Assert.Throws<PackageEnvelopeException>(() => BoundedZipEnvelope.Validate(stream,
						Limits(compressed: limit), Context()));
				else
					BoundedZipEnvelope.Validate(stream, Limits(compressed: limit), Context()).Dispose();
			}

			foreach (var limit in new long[] { 3, 4, 5 })
			{
				var stream = new TrackingStream(bytes);
				if (limit < 4)
					Assert.Throws<PackageEnvelopeException>(() => BoundedZipEnvelope.Validate(stream,
						Limits(expandedEntry: limit, expandedAggregate: 16), Context()));
				else
					BoundedZipEnvelope.Validate(stream,
						Limits(expandedEntry: limit, expandedAggregate: 16), Context()).Dispose();
			}
		}

		[Test]
		public void AggregateAndRatioLimitsRejectBeforeChargingSharedLedger()
		{
			var aggregate = BuildZip(
				("a", "ab", CompressionMethod.Stored),
				("b", "cd", CompressionMethod.Stored));
			var rejectedContext = Context();
			Assert.Throws<PackageEnvelopeException>(() => BoundedZipEnvelope.Validate(
				new TrackingStream(aggregate), Limits(expandedEntry: 4, expandedAggregate: 3), rejectedContext));
			Assert.That(rejectedContext.RemainingEntries, Is.EqualTo(32));
			Assert.That(rejectedContext.RemainingAddressedBytes, Is.EqualTo(8192));
			BoundedZipEnvelope.Validate(new TrackingStream(aggregate),
				Limits(expandedEntry: 4, expandedAggregate: 4), Context()).Dispose();

			var ratioBytes = BuildZip(("a", new string('a', 1000), CompressionMethod.Deflated));
			var central = FindCentral(ratioBytes);
			var compressed = BinaryPrimitives.ReadUInt32LittleEndian(ratioBytes.AsSpan(central + 20, 4));
			var expanded = BinaryPrimitives.ReadUInt32LittleEndian(ratioBytes.AsSpan(central + 24, 4));
			var exactRatio = checked((int)((expanded + compressed - 1) / compressed));
			BoundedZipEnvelope.Validate(new TrackingStream(ratioBytes),
				Limits(expandedEntry: 1024, expandedAggregate: 1024, ratio: exactRatio), Context()).Dispose();
			Assert.Throws<PackageEnvelopeException>(() => BoundedZipEnvelope.Validate(
				new TrackingStream(ratioBytes),
				Limits(expandedEntry: 1024, expandedAggregate: 1024, ratio: exactRatio - 1), Context()));
		}

		[Test]
		public void SecondPassCannotMaterializeChangedUnderchargedCentralDirectoryScalars()
		{
			var second = BuildZip(("map.yaml", "abcd", CompressionMethod.Stored));
			var first = (byte[])second.Clone();
			var central = FindCentral(first);
			var crc = new Crc32();
			crc.Update(new byte[] { (byte)'a' });
			var firstCrc = checked((uint)crc.Value);
			BinaryPrimitives.WriteUInt32LittleEndian(first.AsSpan(14, 4), firstCrc);
			BinaryPrimitives.WriteUInt32LittleEndian(first.AsSpan(18, 4), 1);
			BinaryPrimitives.WriteUInt32LittleEndian(first.AsSpan(22, 4), 1);
			BinaryPrimitives.WriteUInt32LittleEndian(first.AsSpan(central + 16, 4), firstCrc);
			BinaryPrimitives.WriteUInt32LittleEndian(first.AsSpan(central + 20, 4), 1);
			BinaryPrimitives.WriteUInt32LittleEndian(first.AsSpan(central + 24, 4), 1);
			var stream = new SwitchingReadStream(first, second, central);
			var context = Context(bytes: 8);
			ValidatedZipEnvelope escaped = null;
			PackageEnvelopeException failure = null;
			try
			{
				failure = Assert.Throws<PackageEnvelopeException>(() => escaped =
					BoundedZipEnvelope.Validate(stream, Limits(expandedEntry: 4, expandedAggregate: 4), context));
			}
			finally
			{
				escaped?.Dispose();
			}

			Assert.That(failure.Failure, Is.EqualTo(PackageEnvelopeFailure.Malformed));
			Assert.That(context.RemainingAddressedBytes, Is.EqualTo(7));
			Assert.That(stream.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void CentralPreflightRejectsEncryptedDescriptorZip64AndSpecialEntries()
		{
			foreach (var flag in new ushort[] { 1, 8 })
			{
				var bytes = BuildZip(("map.yaml", "abc", CompressionMethod.Stored));
				var central = FindCentral(bytes);
				BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(central + 8, 2), flag);
				AssertMalformedAndClosed(bytes);
			}

			var zip64 = BuildZip(("map.yaml", "abc", CompressionMethod.Stored));
			var zip64Central = FindCentral(zip64);
			BinaryPrimitives.WriteUInt16LittleEndian(zip64.AsSpan(zip64Central + 6, 2), 45);
			BinaryPrimitives.WriteUInt32LittleEndian(zip64.AsSpan(zip64Central + 20, 4), uint.MaxValue);
			AssertMalformedAndClosed(zip64);

			var special = BuildZip(("map.yaml", "abc", CompressionMethod.Stored));
			var specialCentral = FindCentral(special);
			BinaryPrimitives.WriteUInt32LittleEndian(special.AsSpan(specialCentral + 38, 4), 0xA0000000);
			AssertMalformedAndClosed(special);
		}

		[Test]
		public void LocalAndCentralVersionFlagsNamesCrcAndSizesMustMatch()
		{
			foreach (var offset in new[] { 4, 6, 14, 18, 22 })
			{
				var bytes = BuildZip(("map.yaml", "abc", CompressionMethod.Stored));
				bytes[offset] ^= 1;
				AssertMalformedAndClosed(bytes);
			}

			var name = BuildZip(("map.yaml", "abc", CompressionMethod.Stored));
			name[30] = (byte)'n';
			AssertMalformedAndClosed(name);
		}

		[Test]
		public void ContradictoryLocalHeaderFailsBeforeAHandleEscapes()
		{
			var bytes = BuildZip(("map.yaml", "abc", CompressionMethod.Stored));
			BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8, 2),
				(ushort)CompressionMethod.Deflated);
			var backing = new TrackingStream(bytes);

			var failure = Assert.Throws<PackageEnvelopeException>(() =>
				BoundedZipEnvelope.Validate(backing, Limits(), Context()));

			Assert.That(failure.Failure, Is.EqualTo(PackageEnvelopeFailure.Malformed));
			Assert.That(backing.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void CorruptEntryPayloadFailsLazilyWithTypedCrcError()
		{
			var bytes = BuildZip(("map.yaml", "abc", CompressionMethod.Stored));
			var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26, 2));
			var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28, 2));
			bytes[30 + nameLength + extraLength] ^= 0x80;
			var backing = new TrackingStream(bytes);
			var context = Context();

			using (var envelope = BoundedZipEnvelope.Validate(backing, Limits(), context))
			using (var package = envelope.OpenPackage(context))
			using (var stream = package.GetStream("map.yaml"))
			{
				var failure = Assert.Throws<PackageEnvelopeException>(() => stream.CopyTo(Stream.Null));
				Assert.That(failure.Failure, Is.EqualTo(PackageEnvelopeFailure.Malformed));
			}

			Assert.That(backing.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void CorruptDeflatedPayloadFailsLazilyWithTypedError()
		{
			var bytes = BuildZip(("map.yaml", "abc", CompressionMethod.Deflated));
			var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26, 2));
			var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28, 2));
			bytes[30 + nameLength + extraLength] ^= 0x80;
			var backing = new TrackingStream(bytes);
			var context = Context();

			using (var envelope = BoundedZipEnvelope.Validate(backing, Limits(), context))
			using (var package = envelope.OpenPackage(context))
			using (var stream = package.GetStream("map.yaml"))
			{
				var failure = Assert.Throws<PackageEnvelopeException>(() => stream.CopyTo(Stream.Null));
				Assert.That(failure.Failure, Is.EqualTo(PackageEnvelopeFailure.Malformed));
			}

			Assert.That(backing.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void DeflatedEntryRejectsTrailingBytesInsideItsDeclaredCompressedRange()
		{
			var original = BuildZip(("map.yaml", "abc", CompressionMethod.Deflated));
			var oldCentral = FindCentral(original);
			var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(26, 2));
			var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(28, 2));
			var dataOffset = 30 + nameLength + extraLength;
			var compressedLength = BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(18, 4));
			var insertion = checked(dataOffset + (int)compressedLength);
			var bytes = new byte[original.Length + 1];
			Buffer.BlockCopy(original, 0, bytes, 0, insertion);
			bytes[insertion] = 0x7F;
			Buffer.BlockCopy(original, insertion, bytes, insertion + 1, original.Length - insertion);
			var central = oldCentral + 1;
			var end = bytes.Length - 22;
			BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(18, 4), compressedLength + 1);
			BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(central + 20, 4), compressedLength + 1);
			BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(end + 16, 4), checked((uint)central));
			var context = Context();

			using var envelope = BoundedZipEnvelope.Validate(new TrackingStream(bytes), Limits(), context);
			using var package = envelope.OpenPackage(context);
			using var stream = package.GetStream("map.yaml");
			var failure = Assert.Throws<PackageEnvelopeException>(() => stream.CopyTo(Stream.Null));
			Assert.That(failure.Failure, Is.EqualTo(PackageEnvelopeFailure.Malformed));
		}

		[Test]
		public void SeekableVerifiedEntrySupportsNestedBoundedZipAndPinsFinalLifetime()
		{
			var inner = BuildZip(("map.yaml", "abc", CompressionMethod.Stored));
			var outer = BuildZipBytes(("inner.zip", inner, CompressionMethod.Deflated));
			var backing = new TrackingStream(outer);
			var limits = Limits(compressed: 8192, expandedEntry: 4096, expandedAggregate: 8192);
			var policy = Policy(limits);
			var context = new PackageReadContext(default, new NeverDeadline(), 8, 32, 16384, policy);
			var fileSystem = new OpenRA.FileSystem.FileSystem(
				"test", new Dictionary<string, Manifest>(), Array.Empty<IPackageLoader>());

			Assert.That(fileSystem.TryParsePackage(backing, "outer.zip", context, out var outerPackage), Is.True);
			using (outerPackage)
			using (var innerPackage = outerPackage.OpenPackage("inner.zip", fileSystem))
			using (var stream = innerPackage.GetStream("map.yaml"))
			{
				Assert.That(stream.CanSeek, Is.True);
				Assert.That(stream.ReadByte(), Is.EqualTo('a'));
				stream.Position = 0;
				Assert.That(new StreamReader(stream, Encoding.ASCII, false, 1024, true).ReadToEnd(), Is.EqualTo("abc"));
				Assert.That(backing.DisposeCalls, Is.EqualTo(0));
			}

			Assert.That(backing.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void InvalidLimitObjectsAreRejected()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => new ZipEnvelopeLimits(0, 1, 1, 1, 1, 1, 1));
			Assert.Throws<ArgumentOutOfRangeException>(() => new ZipEnvelopeLimits(1, -1, 1, 1, 1, 1, 1));
			Assert.Throws<ArgumentOutOfRangeException>(() => new ZipEnvelopeLimits(1, 1, 0, 1, 1, 1, 1));
			Assert.Throws<ArgumentOutOfRangeException>(() => new ZipEnvelopeLimits(1, 1, 1, 0, 1, 1, 1));
		}

		static PackageEnvelopePolicy Policy(ZipEnvelopeLimits zip) => new(
			zip,
			PngReadLimits.TrustedEngineDefault,
			new YamlEnvelopeLimits(1, 1, 1, 1, 1),
			new MixEnvelopeLimits(8, 32, 32, 4096, 4096, 32, 8192, 16384),
			new IdxBagEnvelopeLimits(4096, 32, 16, 8192, 4096));

		static void AssertMalformedAndClosed(byte[] bytes)
		{
			var backing = new TrackingStream(bytes);
			var failure = Assert.Throws<PackageEnvelopeException>(() =>
				BoundedZipEnvelope.Validate(backing, Limits(), Context()));
			Assert.That(failure.Failure, Is.EqualTo(PackageEnvelopeFailure.Malformed));
			Assert.That(backing.DisposeCalls, Is.EqualTo(1));
		}

		static int FindCentral(byte[] bytes)
		{
			for (var i = 0; i <= bytes.Length - 46; i++)
				if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i, 4)) == 0x02014b50)
					return i;

			throw new InvalidOperationException("Synthetic ZIP central directory was not found.");
		}

		static byte[] BuildZip(params (string Name, string Content, CompressionMethod Method)[] entries) =>
			BuildZipBytes(entries.Select(entry =>
				(entry.Name, Encoding.ASCII.GetBytes(entry.Content), entry.Method)).ToArray());

		static byte[] BuildZipBytes(params (string Name, byte[] Content, CompressionMethod Method)[] entries)
		{
			using var output = new MemoryStream();
			using (var zip = new ZipOutputStream(output) { IsStreamOwner = false })
			{
				foreach (var item in entries)
				{
					var data = item.Content;
					var entry = new ZipEntry(item.Name)
					{
						CompressionMethod = item.Method,
						Size = data.Length,
					};
					zip.PutNextEntry(entry);
					zip.Write(data, 0, data.Length);
					zip.CloseEntry();
				}

				zip.Finish();
			}

			return output.ToArray();
		}

		sealed class NeverDeadline : IPackageReadDeadline
		{
			public void ThrowIfExpired() { }
		}

		sealed class TrackingStream : MemoryStream
		{
			public int DisposeCalls { get; private set; }

			public TrackingStream(byte[] bytes)
				: base(bytes, writable: false)
			{
			}

			protected override void Dispose(bool disposing)
			{
				if (disposing)
					DisposeCalls++;
				base.Dispose(disposing);
			}
		}

		sealed class SwitchingReadStream : Stream
		{
			readonly byte[] first;
			readonly byte[] second;
			readonly long triggerOffset;
			long position;
			int triggerReads;
			bool switched;
			bool disposed;

			public int DisposeCalls { get; private set; }

			public SwitchingReadStream(byte[] first, byte[] second, long triggerOffset)
			{
				if (first.Length != second.Length)
					throw new ArgumentException("Switching stream snapshots must have equal lengths.");
				this.first = first;
				this.second = second;
				this.triggerOffset = triggerOffset;
			}

			public override bool CanRead => !disposed;
			public override bool CanSeek => !disposed;
			public override bool CanWrite => false;
			public override long Length => first.LongLength;
			public override long Position
			{
				get => position;
				set => position = value >= 0 && value <= Length ? value :
					throw new ArgumentOutOfRangeException(nameof(value));
			}

			public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
			public override int Read(Span<byte> buffer)
			{
				if (disposed)
					throw new ObjectDisposedException(nameof(SwitchingReadStream));
				if (position == triggerOffset && triggerReads++ != 0)
					switched = true;
				var length = (int)Math.Min(buffer.Length, Length - position);
				(switched ? second : first).AsSpan((int)position, length).CopyTo(buffer);
				position += length;
				return length;
			}

			public override void Flush() { }
			public override long Seek(long offset, SeekOrigin origin)
			{
				Position = origin switch
				{
					SeekOrigin.Begin => offset,
					SeekOrigin.Current => checked(position + offset),
					SeekOrigin.End => checked(Length + offset),
					_ => throw new ArgumentOutOfRangeException(nameof(origin)),
				};
				return position;
			}
			public override void SetLength(long value) => throw new NotSupportedException();
			public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					disposed = true;
					DisposeCalls++;
				}
				base.Dispose(disposing);
			}
		}
	}
}
