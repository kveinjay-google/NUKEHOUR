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
using NUnit.Framework;
using OpenRA.FileSystem;
using OpenRA.Mods.Cnc.FileFormats;
using OpenRA.Mods.Cnc.FileSystem;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class MixEnvelopeTest
	{
		static MixEnvelopeLimits Limits(int entries = 8, long segment = 1024, long addressed = 4096,
			long index = 4096) => new(8, entries, 32, index, 4096, 32, segment, addressed);

		static PackageReadContext Context(int entries = 32, long bytes = 8192) =>
			new(default, new NeverDeadline(), 8, entries, bytes);

		static ValidatedMixIndex ValidateUnresolved(Stream stream, MixEnvelopeLimits limits,
			PackageReadContext context) => MixEnvelopeValidator.Validate(
				stream, limits, PackageHashType.CRC32, NoNameResolver.Instance, context);

		[Test]
		public void ValidatedSourcesAndEntryStreamsHaveIndependentPositionsAndLifetimes()
		{
			var backing = new TrackingStream(BuildMix((1U, new byte[] { 1, 2, 3, 4 })));
			var context = Context();
			var index = ValidateUnresolved(backing, Limits(), context);
			var source = index.OpenEntrySource(1, context);
			var first = source.OpenRead();
			var second = source.OpenRead();

			Assert.That(first.ReadByte(), Is.EqualTo(1));
			Assert.That(first.ReadByte(), Is.EqualTo(2));
			Assert.That(second.ReadByte(), Is.EqualTo(1));
			index.Dispose();
			source.Dispose();
			Assert.That(first.ReadByte(), Is.EqualTo(3));
			Assert.That(backing.DisposeCalls, Is.EqualTo(0));

			first.Dispose();
			second.Dispose();
			Assert.That(backing.DisposeCalls, Is.EqualTo(1));
			Assert.Throws<ObjectDisposedException>(() => source.OpenRead());
		}

		[Test]
		public void ReplacementLedgerIsRejectedWithoutInvalidatingTheIndex()
		{
			using var index = ValidateUnresolved(
				new MemoryStream(BuildMix((1U, new byte[] { 1 }))), Limits(), Context());

			Assert.Throws<InvalidOperationException>(() => index.OpenEntry(1, Context()));
		}

		[Test]
		public void TruncatedEncryptedDuplicateOverlappingAndOutOfRangeIndexesAreRejected()
		{
			var valid = BuildMix((1U, new byte[] { 1, 2 }));
			AssertRejected(valid[..^1]);

			var encrypted = valid.ToArray();
			encrypted[2] = 2;
			AssertRejected(encrypted);

			AssertRejected(BuildRaw(2, 2,
				new[] { (1U, 0U, 1U), (1U, 1U, 1U) }, new byte[] { 1, 2 }));
			AssertRejected(BuildRaw(2, 3,
				new[] { (1U, 0U, 2U), (2U, 1U, 2U) }, new byte[] { 1, 2, 3 }));
			AssertRejected(BuildRaw(1, 1,
				new[] { (1U, 1U, 1U) }, new byte[] { 1 }));
		}

		[Test]
		public void EmptyCountIndexBytesAndDeclaredLengthUseClosedFailureKinds()
		{
			var empty = Assert.Throws<PackageEnvelopeException>(() => ValidateUnresolved(
				new MemoryStream(BuildRaw(0, 0, Array.Empty<(uint, uint, uint)>(), Array.Empty<byte>())),
				Limits(), Context()));
			Assert.That(empty.Failure, Is.EqualTo(PackageEnvelopeFailure.Malformed));

			var bytes = BuildMix((1U, new byte[] { 1 }));
			ValidateUnresolved(new MemoryStream(bytes), Limits(index: 22), Context()).Dispose();
			var index = Assert.Throws<PackageEnvelopeException>(() => ValidateUnresolved(
				new MemoryStream(bytes), Limits(index: 21), Context()));
			Assert.That(index.Failure, Is.EqualTo(PackageEnvelopeFailure.LimitExceeded));

			var declared = (byte[])bytes.Clone();
			declared[6]++;
			var malformed = Assert.Throws<PackageEnvelopeException>(() => ValidateUnresolved(
				new MemoryStream(declared), Limits(), Context()));
			Assert.That(malformed.Failure, Is.EqualTo(PackageEnvelopeFailure.Malformed));
		}

		[Test]
		public void PerArchiveSegmentAndAggregateBudgetsCoverBelowExactAndAbove()
		{
			var bytes = BuildMix((1U, new byte[] { 1, 2, 3, 4 }));
			Assert.Throws<PackageEnvelopeException>(() => ValidateUnresolved(
				new MemoryStream(bytes), Limits(segment: 3), Context()));
			ValidateUnresolved(new MemoryStream(bytes), Limits(segment: 4), Context()).Dispose();

			Assert.Throws<PackageEnvelopeException>(() => ValidateUnresolved(
				new MemoryStream(bytes), Limits(addressed: 3), Context()));
			ValidateUnresolved(new MemoryStream(bytes), Limits(addressed: 4), Context()).Dispose();

			var twoEntries = BuildMix((1U, new byte[] { 1 }), (2U, new byte[] { 2 }));
			Assert.Throws<PackageEnvelopeException>(() => ValidateUnresolved(
				new MemoryStream(twoEntries), Limits(entries: 1), Context()));
		}

		[Test]
		public void Ordinary512MiBSegmentUsesOneLongPinnedRangeAndRejectsPlusOne()
		{
			var exactLength = IosImportEnvelopePolicyV1.MixSegmentBytes;
			var exact = BuildSparseMix(exactLength, 0x5A);
			var exactLimits = Limits(segment: exactLength, addressed: exactLength);
			var context = Context(bytes: exactLength);
			var index = ValidateUnresolved(exact, exactLimits, context);
			var source = index.OpenEntrySource(1, context);
			var child = source.OpenRead();

			Assert.That(index.Entries.Single().Length, Is.EqualTo(exactLength));
			index.Dispose();
			source.Dispose();
			child.Position = exactLength - 1;
			Assert.That(child.ReadByte(), Is.EqualTo(0x5A));
			Assert.That(exact.TotalBytesRead, Is.LessThan(1024));
			Assert.That(exact.DisposeCalls, Is.EqualTo(0));
			child.Dispose();
			Assert.That(exact.DisposeCalls, Is.EqualTo(1));

			var above = BuildSparseMix(exactLength + 1, 0x5B);
			var failure = Assert.Throws<PackageEnvelopeException>(() => ValidateUnresolved(above,
				Limits(segment: exactLength, addressed: exactLength + 1), Context(bytes: exactLength + 1)));
			Assert.That(failure.Failure, Is.EqualTo(PackageEnvelopeFailure.LimitExceeded));
			Assert.That(above.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void LegacyMixFileGetContentAccepts512MiBAndRejectsPlusOneBeforeCopy()
		{
			const string Name = "legacy-large.bin";
			var exactLength = IosImportEnvelopePolicyV1.MixSegmentBytes;
			var exactPath = BuildSparseLegacyMixFile(Name, exactLength, 0x6A);
			try
			{
				using var package = new MixLoader.MixFile(File.OpenRead(exactPath), "legacy.mix", new[] { Name });
				using var content = package.GetStream(Name);
				Assert.That(content.Length, Is.EqualTo(exactLength));
				content.Position = exactLength - 1;
				Assert.That(content.ReadByte(), Is.EqualTo(0x6A));
			}
			finally
			{
				File.Delete(exactPath);
			}

			var abovePath = BuildSparseLegacyMixFile(Name, exactLength + 1, 0x6B);
			try
			{
				using var package = new MixLoader.MixFile(File.OpenRead(abovePath), "legacy.mix", new[] { Name });
				Assert.Throws<InvalidDataException>(() => package.GetStream(Name));
			}
			finally
			{
				File.Delete(abovePath);
			}
		}

		[Test]
		public void SecondPassCannotMaterializeAChangedUnderchargedMixIndex()
		{
			var first = BuildRaw(1, 4, new[] { (1U, 0U, 1U) }, new byte[4]);
			var second = BuildRaw(1, 4, new[] { (1U, 0U, 4U) }, new byte[4]);
			var stream = new SwitchingReadStream(first, second, 10);
			var context = Context(bytes: 8);
			ValidatedMixIndex escaped = null;
			PackageEnvelopeException failure = null;
			try
			{
				failure = Assert.Throws<PackageEnvelopeException>(() => escaped =
					ValidateUnresolved(stream, Limits(segment: 4, addressed: 4), context));
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
		public void CancellationAndDeadlineAreCheckedBeforeIndexAllocation()
		{
			using var cancelled = new System.Threading.CancellationTokenSource();
			cancelled.Cancel();
			var cancelledContext = new PackageReadContext(cancelled.Token, new NeverDeadline(), 8, 8, 1024);
			Assert.Throws<OperationCanceledException>(() => ValidateUnresolved(
				new MemoryStream(BuildMix((1U, new byte[] { 1 }))), Limits(), cancelledContext));

			var expired = new PackageReadContext(default, new ExpiredDeadline(), 8, 8, 1024);
			Assert.Throws<TimeoutException>(() => ValidateUnresolved(
				new MemoryStream(BuildMix((1U, new byte[] { 1 }))), Limits(), expired));
		}

		[Test]
		public void LegacyLocalDatabaseRejectsAnInvalidCountBeforeAllocation()
		{
			using var stream = new MemoryStream(new byte[52]);
			stream.Position = 48;
			using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
				writer.Write(-1);
			stream.Position = 0;

			Assert.Throws<InvalidDataException>(() => new XccLocalDatabase(stream));
		}

		[Test]
		public void ValidatedLocalDatabasePreflightsMinimumNamesAndChecksItsContext()
		{
			using var impossible = new MemoryStream(new byte[54]);
			impossible.Position = 48;
			using (var writer = new BinaryWriter(impossible, System.Text.Encoding.ASCII, true))
				writer.Write(2);
			impossible.Position = 0;
			Assert.Throws<InvalidDataException>(() => XccLocalDatabase.ReadValidated(impossible, 54, 8));

			using var cancelled = new System.Threading.CancellationTokenSource();
			cancelled.Cancel();
			var context = new PackageReadContext(cancelled.Token, new NeverDeadline(), 8, 8, 1024);
			using var valid = new MemoryStream(new byte[54]);
			valid.Position = 48;
			using (var writer = new BinaryWriter(valid, System.Text.Encoding.ASCII, true))
			{
				writer.Write(1);
				writer.Write((byte)'a');
				writer.Write((byte)0);
			}
			valid.Position = 0;
			Assert.Throws<OperationCanceledException>(() =>
				XccLocalDatabase.ReadValidated(valid, 54, 8, context));
		}

		[Test]
		public void SharedLedgerChargesAreAtomicVisibleAndDistinguishDepthFromAggregate()
		{
			var context = new PackageReadContext(default, new NeverDeadline(), 2, 3, 5);
			var first = context.Descend(2, 3);
			Assert.That((context.RemainingEntries, context.RemainingAddressedBytes), Is.EqualTo((1, 2L)));
			var aggregate = Assert.Throws<PackageEnvelopeException>(() => context.Descend(2, 1));
			Assert.That(aggregate.Failure, Is.EqualTo(PackageEnvelopeFailure.AggregateLimitExceeded));
			Assert.That((first.RemainingEntries, first.RemainingAddressedBytes), Is.EqualTo((1, 2L)));
			first.Descend(1, 2);
			Assert.That((context.RemainingEntries, context.RemainingAddressedBytes), Is.EqualTo((0, 0L)));

			var shallow = new PackageReadContext(default, new NeverDeadline(), 0, 1, 1);
			var depth = Assert.Throws<PackageEnvelopeException>(() => shallow.Descend(1, 1));
			Assert.That(depth.Failure, Is.EqualTo(PackageEnvelopeFailure.LimitExceeded));
			Assert.That((shallow.RemainingEntries, shallow.RemainingAddressedBytes), Is.EqualTo((1, 1L)));
		}

		[Test]
		public void PolicyBoundValidationRequiresTheExactMixLimitObjectBeforeReading()
		{
			var limits = Limits();
			var policy = Policy(limits);
			var context = new PackageReadContext(default, new NeverDeadline(), 8, 32, 8192, policy);
			using (MixEnvelopeValidator.Validate(new MemoryStream(BuildMix((1U, new byte[] { 1 }))),
				limits, PackageHashType.CRC32, NoNameResolver.Instance, context)) { }

			var stream = new TrackingStream(BuildMix((1U, new byte[] { 1 })));
			Assert.Throws<InvalidOperationException>(() => MixEnvelopeValidator.Validate(stream,
				Limits(), PackageHashType.CRC32, NoNameResolver.Instance, context));
			Assert.That(stream.ReadCalls, Is.EqualTo(0));
			Assert.That(stream.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void PublicValidatorRequiresExplicitHashTypeAndNonNullResolverBeforeReading()
		{
			var methods = typeof(MixEnvelopeValidator).GetMethods()
				.Where(method => method.Name == nameof(MixEnvelopeValidator.Validate))
				.ToArray();
			Assert.That(methods, Has.Length.EqualTo(1));
			Assert.That(methods[0].GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[]
			{
				typeof(Stream), typeof(MixEnvelopeLimits), typeof(PackageHashType),
				typeof(IMixFilenameResolver), typeof(PackageReadContext),
			}));

			var stream = new TrackingStream(BuildMix((1U, new byte[] { 1 })));
			Assert.Throws<ArgumentNullException>(() => MixEnvelopeValidator.Validate(
				stream, Limits(), PackageHashType.CRC32, null, Context()));
			Assert.That(stream.ReadCalls, Is.EqualTo(0));
			Assert.That(stream.DisposeCalls, Is.EqualTo(1));
		}

		[TestCase("UPPER.MIX")]
		[TestCase("folder/name.mix")]
		[TestCase("wrong.mix")]
		public void ResolverOutputIsRevalidatedAsCanonicalAndHashExact(string maliciousName)
		{
			var hash = PackageEntry.HashFilename("cache.mix", PackageHashType.CRC32);
			Assert.Throws<PackageEnvelopeException>(() => MixEnvelopeValidator.Validate(
				new MemoryStream(BuildMix((hash, new byte[] { 1 }))), Limits(), PackageHashType.CRC32,
				new FixedResolver(maliciousName), Context()));
		}

		[Test]
		public void ConfiguredBoundedPackageRetainsChildContextAndRejectsReplacementFileSystem()
		{
			var nested = BuildMix((1U, new byte[] { 1 }));
			var cacheHash = PackageEntry.HashFilename("cache.mix", PackageHashType.CRC32);
			var outer = BuildMix((cacheHash, nested));
			var resolver = new ExactResolver("cache.mix");
			var fileSystem = CreateFileSystem(resolver);
			var limits = Limits();
			var policy = Policy(limits);
			var exact = new PackageReadContext(default, new NeverDeadline(), 2, 32, 8192, policy);
			Assert.That(fileSystem.TryParsePackage(new MemoryStream(outer), "ra2.mix", exact, out var parsed), Is.True);
			using (parsed)
			{
				Assert.That(parsed, Is.InstanceOf<IValidatedMixPackage>());
				using var child = parsed.OpenPackage("cache.mix", fileSystem);
				Assert.That(child, Is.InstanceOf<IValidatedMixPackage>());
				Assert.Throws<InvalidOperationException>(() => parsed.OpenPackage("cache.mix", CreateFileSystem(resolver)));
			}

			var tooShallow = new PackageReadContext(default, new NeverDeadline(), 1, 32, 8192, policy);
			Assert.That(fileSystem.TryParsePackage(new MemoryStream(outer), "ra2.mix", tooShallow, out parsed), Is.True);
			using (parsed)
			{
				var failure = Assert.Throws<PackageEnvelopeException>(() => parsed.OpenPackage("cache.mix", fileSystem));
				Assert.That(failure.Failure, Is.EqualTo(PackageEnvelopeFailure.LimitExceeded));
			}
		}

		[Test]
		public void BoundedFileSystemRejectsPolicylessContextBeforeTouchingTheStream()
		{
			var stream = new TrackingStream(BuildMix((1U, new byte[] { 1 })));
			var fileSystem = CreateFileSystem(new ExactResolver("cache.mix"));
			Assert.Throws<InvalidOperationException>(() => fileSystem.TryParsePackage(stream, "ra2.mix",
				Context(), out _));
			Assert.That(stream.ReadCalls, Is.EqualTo(0));
			Assert.That(stream.DisposeCalls, Is.EqualTo(0));
		}

		[Test]
		public void DirectBoundedLoaderChecksContextPolicyAndConfigurationBeforeFilenameOrSource()
		{
			var fileSystem = CreateFileSystem(new ExactResolver("cache.mix"));
			var legacy = (IBoundedPackageLoader)new MixLoader();
			var bytes = BuildMix((1U, new byte[] { 1 }));

			var nullContext = new TrackingStream(bytes);
			Assert.Throws<ArgumentNullException>(() => legacy.TryParsePackage(
				nullContext, "not-a-mix", fileSystem, null, out _));
			Assert.That(nullContext.ReadCalls, Is.EqualTo(0));

			var policyless = new TrackingStream(bytes);
			Assert.Throws<InvalidOperationException>(() => legacy.TryParsePackage(
				policyless, "not-a-mix", fileSystem, Context(), out _));
			Assert.That(policyless.ReadCalls, Is.EqualTo(0));

			var limits = Limits();
			var configuredContext = new PackageReadContext(
				default, new NeverDeadline(), 8, 32, 8192, Policy(limits));
			var unconfigured = new TrackingStream(bytes);
			Assert.Throws<InvalidOperationException>(() => legacy.TryParsePackage(
				unconfigured, "not-a-mix", fileSystem, configuredContext, out _));
			Assert.That(unconfigured.ReadCalls, Is.EqualTo(0));
		}

		static void AssertRejected(byte[] bytes)
		{
			var stream = new TrackingStream(bytes);
			Assert.Throws<PackageEnvelopeException>(() => ValidateUnresolved(stream, Limits(), Context()));
			Assert.That(stream.DisposeCalls, Is.EqualTo(1));
		}

		static byte[] BuildMix(params (uint Hash, byte[] Data)[] entries)
		{
			var offset = 0U;
			var index = entries.Select(entry =>
			{
				var item = (entry.Hash, Offset: offset, Length: (uint)entry.Data.Length);
				offset += item.Length;
				return item;
			}).ToArray();
			return BuildRaw((ushort)entries.Length, offset, index, entries.SelectMany(entry => entry.Data).ToArray());
		}

		static byte[] BuildRaw(ushort count, uint dataSize,
			(uint Hash, uint Offset, uint Length)[] entries, byte[] data)
		{
			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
			{
				writer.Write((ushort)0);
				writer.Write((ushort)0);
				writer.Write(count);
				writer.Write(dataSize);
				foreach (var entry in entries)
				{
					writer.Write(entry.Hash);
					writer.Write(entry.Offset);
					writer.Write(entry.Length);
				}

				writer.Write(data);
			}

			return stream.ToArray();
		}

		static SparseReadStream BuildSparseMix(long segmentLength, byte finalByte)
		{
			var header = BuildRaw(1, checked((uint)segmentLength),
				new[] { (1U, 0U, checked((uint)segmentLength)) }, Array.Empty<byte>());
			return new SparseReadStream(checked(header.LongLength + segmentLength),
				(header, 0), (new[] { finalByte }, header.LongLength + segmentLength - 1));
		}

		static string BuildSparseLegacyMixFile(string name, long segmentLength, byte finalByte)
		{
			var header = BuildRaw(1, checked((uint)segmentLength), new[]
			{
				(PackageEntry.HashFilename(name, PackageHashType.CRC32), 0U, checked((uint)segmentLength)),
			}, Array.Empty<byte>());
			var path = Path.Combine(Path.GetTempPath(), $"openra-legacy-mix-{Guid.NewGuid():N}.mix");
			using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
			stream.Write(header);
			stream.SetLength(checked(header.LongLength + segmentLength));
			stream.Position = stream.Length - 1;
			stream.WriteByte(finalByte);
			return path;
		}

		static PackageEnvelopePolicy Policy(MixEnvelopeLimits mix) => new(
			new ZipEnvelopeLimits(256, 8, 8, 4096, 4096, 8192, 100),
			new PngReadLimits(64, 4096, 16384, 32, 4096, 100),
			new YamlEnvelopeLimits(4096, 128, 8, 512, 256), mix,
			new IdxBagEnvelopeLimits(4096, 32, 16, 4096, 2048));

		static OpenRA.FileSystem.FileSystem CreateFileSystem(IMixFilenameResolver resolver) =>
			new("mix-envelope-test", ImmutableDictionary<string, Manifest>.Empty,
				new IPackageLoader[] { new MixLoader(PackageHashType.CRC32, resolver) });

		sealed class NeverDeadline : IPackageReadDeadline
		{
			public void ThrowIfExpired() { }
		}

		sealed class ExpiredDeadline : IPackageReadDeadline
		{
			public void ThrowIfExpired() => throw new TimeoutException();
		}

		sealed class FixedResolver : IMixFilenameResolver
		{
			readonly string name;
			public FixedResolver(string name) => this.name = name;
			public bool TryResolve(uint hash, PackageHashType hashType, out string canonicalName)
			{
				canonicalName = name;
				return true;
			}
		}

		sealed class ExactResolver : IMixFilenameResolver
		{
			readonly string name;
			public ExactResolver(string name) => this.name = name;
			public bool TryResolve(uint hash, PackageHashType hashType, out string canonicalName)
			{
				if (PackageEntry.HashFilename(name, hashType) == hash)
				{
					canonicalName = name;
					return true;
				}

				canonicalName = null;
				return false;
			}
		}

		sealed class NoNameResolver : IMixFilenameResolver
		{
			public static readonly NoNameResolver Instance = new();

			NoNameResolver() { }

			public bool TryResolve(uint hash, PackageHashType hashType, out string canonicalName)
			{
				canonicalName = null;
				return false;
			}
		}

		sealed class TrackingStream : MemoryStream
		{
			public int DisposeCalls { get; private set; }
			public int ReadCalls { get; private set; }
			public TrackingStream(byte[] bytes) : base(bytes, writable: false) { }
			public override int Read(byte[] buffer, int offset, int count)
			{
				ReadCalls++;
				return base.Read(buffer, offset, count);
			}

			public override int Read(Span<byte> buffer)
			{
				ReadCalls++;
				return base.Read(buffer);
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

		sealed class SparseReadStream : Stream
		{
			readonly long length;
			readonly (byte[] Data, long Offset)[] chunks;
			long position;
			bool disposed;

			public long TotalBytesRead { get; private set; }
			public int DisposeCalls { get; private set; }

			public SparseReadStream(long length, params (byte[] Data, long Offset)[] chunks)
			{
				this.length = length;
				this.chunks = chunks;
			}

			public override bool CanRead => !disposed;
			public override bool CanSeek => !disposed;
			public override bool CanWrite => false;
			public override long Length => length;
			public override long Position
			{
				get => position;
				set => position = value >= 0 && value <= length ? value :
					throw new ArgumentOutOfRangeException(nameof(value));
			}

			public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
			public override int Read(Span<byte> buffer)
			{
				if (disposed)
					throw new ObjectDisposedException(nameof(SparseReadStream));
				var count = (int)Math.Min(buffer.Length, length - position);
				var destination = buffer[..count];
				destination.Clear();
				foreach (var chunk in chunks)
				{
					var start = Math.Max(position, chunk.Offset);
					var end = Math.Min(position + count, chunk.Offset + chunk.Data.LongLength);
					if (start < end)
						chunk.Data.AsSpan((int)(start - chunk.Offset), (int)(end - start))
							.CopyTo(destination[(int)(start - position)..]);
				}

				position += count;
				TotalBytesRead += count;
				return count;
			}

			public override void Flush() { }
			public override long Seek(long offset, SeekOrigin origin)
			{
				Position = origin switch
				{
					SeekOrigin.Begin => offset,
					SeekOrigin.Current => checked(position + offset),
					SeekOrigin.End => checked(length + offset),
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
