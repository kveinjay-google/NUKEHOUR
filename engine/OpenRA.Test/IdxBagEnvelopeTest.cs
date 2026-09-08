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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OpenRA.FileSystem;
using OpenRA.Mods.Cnc.FileFormats;
using OpenRA.Mods.Cnc.FileSystem;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IdxBagEnvelopeTest
	{
		static IdxBagEnvelopeLimits Limits(long idx = 4096, int records = 32, long bag = 4096, long span = 2048) =>
			new(idx, records, 16, bag, span);

		static PackageReadContext Context() => new(default, new NeverDeadline(), 8, 128, 1 << 20);

		[TestCase(6U, 0U, 1)]
		[TestCase(7U, 0U, 2)]
		[TestCase(8U, 5U, 1)]
		[TestCase(9U, 9U, 2)]
		public void ClosedLegalFlagMatrixAcceptsPcmAndIma(uint flags, uint chunkSize, int channels)
		{
			var length = flags < 8 ? (uint)(2 * channels * 4) : checked(chunkSize * 2U);
			var pair = ValidatePair(BuildIdx(Record("Sound_1", 0, length, 22_050, flags, chunkSize)),
				new byte[length]);
			using var envelope = pair.Envelope;

			Assert.That(envelope.Entries.Single().CanonicalName, Is.EqualTo("sound_1.wav"));
			Assert.That(envelope.Entries.Single().Channels, Is.EqualTo(channels));
			Assert.That(envelope.Entries.Single().Encoding,
				Is.EqualTo(flags < 8 ? IdxBagEncoding.Pcm16 : IdxBagEncoding.ImaAdpcm));
		}

		[Test]
		public void EveryOtherLowFlagAndHighBitFormIsRejected()
		{
			foreach (var flags in Enumerable.Range(0, 16).Select(value => (uint)value)
				.Where(value => value != 6 && value != 7 && value != 8 && value != 9)
				.Concat(new[] { 0x80000006U, 0x80000008U }))
				AssertRejected(Record("sound", 0, 16, 22_050, flags, flags >= 8 ? 9U : 0U), 16,
					flags.ToString(CultureInfo.InvariantCulture));
		}

		[Test]
		public void NamesAreExactAsciiNulPaddedAndUniqueAfterFolding()
		{
			AssertRejected(RecordWithRawName(Enumerable.Repeat((byte)'a', 16).ToArray(), 0, 4, 1, 6, 0), 4, "no nul");
			AssertRejected(RecordWithRawName(new byte[] { (byte)'a', 0, (byte)'b' }.Concat(new byte[13]).ToArray(),
				0, 4, 1, 6, 0), 4, "nonzero padding");
			AssertRejected(RecordWithRawName(new byte[] { 0xC3, 0 }.Concat(new byte[14]).ToArray(),
				0, 4, 1, 6, 0), 4, "non ascii");
			AssertRejected(new[]
			{
				Record("Sound", 0, 4, 1, 6, 0),
				Record("sound", 4, 4, 1, 6, 0),
			}, 8, "folded duplicate");
		}

		[Test]
		public void IndexLengthMagicVersionAndCountAreExact()
		{
			var valid = BuildIdx(Record("sound", 0, 4, 1, 6, 0));
			AssertRejected(valid[..^1], 4, "truncated");
			AssertRejected(valid.Concat(new byte[] { 0 }).ToArray(), 4, "trailing");

			var magic = valid.ToArray();
			magic[0] = (byte)'X';
			AssertRejected(magic, 4, "magic");
			var version = valid.ToArray();
			version[4] = 3;
			AssertRejected(version, 4, "version");
			var zero = valid.ToArray();
			Array.Clear(zero, 8, 4);
			AssertRejected(zero, 4, "zero count");
		}

		[Test]
		public void MaximumRecordCountIsAcceptedAndInvalidCountsFailBeforeTableMaterialization()
		{
			var policy = RetailArchiveGraphValidator.IosV1Policy;
			var records = Enumerable.Range(0, policy.Audio.MaximumSoundRecords)
				.Select(index => Record($"s{index:x}", 0, 4, 1, 6, 0)).ToArray();
			var idx = BuildIdx(records);
			Assert.That(idx.LongLength, Is.EqualTo(checked(12L + records.LongLength * 36L)));
			var context = new PackageReadContext(default, new NeverDeadline(), policy.Mix.MaximumDepth,
				policy.Mix.MaximumEntriesAggregate, policy.Mix.MaximumAddressedBytes, policy);
			using (var mix = MixEnvelopeValidator.Validate(new MemoryStream(BuildAudioMix(idx, new byte[4])),
				policy.Mix, PackageHashType.CRC32, new AudioResolver(), context))
			using (var envelope = IdxBagEnvelopeValidator.Validate(
				mix.OpenEntrySource(PackageEntry.HashFilename("audio.idx", PackageHashType.CRC32), context),
				mix.OpenEntrySource(PackageEntry.HashFilename("audio.bag", PackageHashType.CRC32), context),
				policy.Audio, context))
				Assert.That(envelope.Entries, Has.Length.EqualTo(records.Length));

			foreach (var count in new[] { -1, policy.Audio.MaximumSoundRecords + 1 })
			{
				var invalid = BuildIdxHeader(count);
				var invalidContext = new PackageReadContext(default, new NeverDeadline(), policy.Mix.MaximumDepth,
					policy.Mix.MaximumEntriesAggregate, policy.Mix.MaximumAddressedBytes, policy);
				using var mix = MixEnvelopeValidator.Validate(
					new MemoryStream(BuildAudioMix(invalid, new byte[4])), policy.Mix,
					PackageHashType.CRC32, new AudioResolver(), invalidContext);
				var failure = Assert.Throws<PackageEnvelopeException>(() => IdxBagEnvelopeValidator.Validate(
					mix.OpenEntrySource(PackageEntry.HashFilename("audio.idx", PackageHashType.CRC32), invalidContext),
					mix.OpenEntrySource(PackageEntry.HashFilename("audio.bag", PackageHashType.CRC32), invalidContext),
					policy.Audio, invalidContext));
				Assert.That(failure.Failure, Is.EqualTo(count < 0 ? PackageEnvelopeFailure.Malformed :
					PackageEnvelopeFailure.LimitExceeded));
			}
		}

		[Test]
		public void RateAlignmentChunkAndSpanArithmeticAreClosed()
		{
			AssertRejected(Record("sound", 0, 4, 0, 6, 0), 4, "zero rate");
			AssertRejected(Record("sound", 0, 3, 1, 6, 0), 3, "pcm alignment");
			AssertRejected(Record("sound", 0, 4, uint.MaxValue, 7, 0), 4, "pcm byte rate overflow");
			AssertRejected(Record("sound", 0, 18, 1, 8, 4), 18, "ima below minimum");
			AssertRejected(Record("sound", 0, 18, 1, 8, ushort.MaxValue + 1U), 18, "ima block overflow");
			AssertRejected(Record("sound", uint.MaxValue, 4, 1, 6, 0), 16, "offset overflow");
			AssertRejected(Record("sound", 8, 9, 1, 6, 0), 16, "out of bag");
			AssertRejected(Record("sound", 0, 17, 1, 8, 5), 17, "ima span limit",
				Limits(span: 16));
		}

		[Test]
		public void MaximumImaChunkAndDerivedPcmAndImaRatesUseExactBoundaries()
		{
			const uint maximumChunk = ushort.MaxValue;
			const uint stereoImaSamples = ((maximumChunk - 8U) * 2U / 2U) + 1U;
			var maximumImaRate = checked((uint)((((ulong)uint.MaxValue + 1UL) * stereoImaSamples - 1UL) /
				maximumChunk));
			var imaLimits = Limits(bag: maximumChunk, span: maximumChunk);
			var mixLimits = MixLimits(maximumChunk, maximumChunk + 48L);
			using (var accepted = ValidatePair(
				BuildIdx(Record("ima", 0, maximumChunk, maximumImaRate, 9, maximumChunk)),
				new byte[maximumChunk], imaLimits, mixLimits).Envelope)
			{
				var entry = accepted.Entries.Single();
				Assert.That(entry.ChunkSize, Is.EqualTo(maximumChunk));
				Assert.That(entry.SampleRate, Is.EqualTo(maximumImaRate));
				Assert.That(entry.SamplesPerBlock, Is.EqualTo((ushort)stereoImaSamples));
			}
			AssertRejected(Record("ima", 0, maximumChunk, maximumImaRate + 1, 9, maximumChunk),
				checked((int)maximumChunk), "ima rate above maximum", imaLimits, mixLimits);

			var maximumStereoPcmRate = uint.MaxValue / 4U;
			using (var accepted = ValidatePair(BuildIdx(
				Record("pcm", 0, 4, maximumStereoPcmRate, 7, 0)), new byte[4]).Envelope)
				Assert.That(accepted.Entries.Single().AverageBytesPerSecond,
					Is.EqualTo(maximumStereoPcmRate * 4U));
			AssertRejected(Record("pcm", 0, 4, maximumStereoPcmRate + 1, 7, 0), 4,
				"pcm rate above maximum");
		}

		[TestCase(8U, 5U, 4U)]
		[TestCase(9U, 9U, 8U)]
		public void ImaPartialBlocksRequireACompleteChannelHeader(
			uint flags, uint chunkSize, uint headerBytes)
		{
			AssertRejected(Record("sound", 0, headerBytes - 1, 1, flags, chunkSize),
				checked((int)headerBytes - 1), "partial IMA header");

			foreach (var length in new[] { headerBytes, headerBytes + 1 })
			{
				var pair = ValidatePair(BuildIdx(Record("sound", 0, length, 1, flags, chunkSize)),
					new byte[length]);
				using var envelope = pair.Envelope;
				using var package = envelope.OpenPackage("audio.bag", pair.Context);
				using var wave = package.GetStream("sound.wav");
				var bytes = wave.ReadAllBytes();
				var fact = FindAscii(bytes, "fact");
				Assert.That(BitConverter.ToUInt32(bytes, fact + 8), Is.EqualTo(
					length == headerBytes ? 1U : flags == 8 ? 3U : 2U));
			}
		}

		[Test]
		public void SecondPassCannotMaterializeChangedUnderchargedIdxRecords()
		{
			var firstIdx = BuildIdx(Record("sound", 0, 4, 1, 6, 0));
			var secondIdx = BuildIdx(Record("sound", 0, 8, 1, 6, 0));
			var first = BuildAudioMix(firstIdx, new byte[8]);
			var second = BuildAudioMix(secondIdx, new byte[8]);
			var stream = new SwitchingReadStream(first, second, 34 + 12);
			var context = Context();
			using var mix = MixEnvelopeValidator.Validate(stream, MixLimits(),
				PackageHashType.CRC32, new AudioResolver(), context);
			var before = context.RemainingAddressedBytes;
			ValidatedIdxBagIndex escaped = null;
			PackageEnvelopeException failure = null;
			try
			{
				failure = Assert.Throws<PackageEnvelopeException>(() => escaped = IdxBagEnvelopeValidator.Validate(
					mix.OpenEntrySource(PackageEntry.HashFilename("audio.idx", PackageHashType.CRC32), context),
					mix.OpenEntrySource(PackageEntry.HashFilename("audio.bag", PackageHashType.CRC32), context),
					Limits(), context));
			}
			finally
			{
				escaped?.Dispose();
			}

			Assert.That(failure.Failure, Is.EqualTo(PackageEnvelopeFailure.Malformed));
			Assert.That(context.RemainingAddressedBytes, Is.EqualTo(before - 4));
		}

		[Test]
		public void CompleteSparseLanguageAudioChainPinsExact512MiBMixAnd384MiBBag()
		{
			var policy = RetailArchiveGraphValidator.IosV1Policy;
			var bagLength = policy.Audio.MaximumBagBytes;
			var audioLength = policy.Mix.MaximumSegmentBytes;
			var payload = new byte[] { 1, 2, 3, 4 };
			var stream = BuildSparseLanguageAudioChain(bagLength, audioLength, payload);
			var context = new PackageReadContext(default, new NeverDeadline(), policy.Mix.MaximumDepth,
				policy.Mix.MaximumEntriesAggregate, policy.Mix.MaximumAddressedBytes, policy);
			var fileSystem = new OpenRA.FileSystem.FileSystem("sparse-audio-chain",
				new Dictionary<string, Manifest>(), new IPackageLoader[]
				{
					new MixLoader(PackageHashType.CRC32, RetailFilenameCatalog.V1),
				});

			Assert.That(fileSystem.TryParsePackage(stream, "language.mix", context, out var language), Is.True);
			Assert.That(((IValidatedMixPackage)language).Entries.Single(entry => entry.CanonicalName == "audio.mix").Length,
				Is.EqualTo(audioLength));
			var audio = language.OpenPackage("audio.mix", fileSystem);
			Assert.That(audio, Is.InstanceOf<IValidatedMixPackage>());
			language.Dispose();
			var validatedAudio = (IValidatedMixPackage)audio;
			Assert.That(validatedAudio.Entries.Single(entry => entry.CanonicalName == "audio.bag").Length,
				Is.EqualTo(bagLength));
			var idxSource = validatedAudio.OpenEntrySource(
				PackageEntry.HashFilename("audio.idx", PackageHashType.CRC32));
			var bagSource = validatedAudio.OpenEntrySource(
				PackageEntry.HashFilename("audio.bag", PackageHashType.CRC32));
			var envelope = IdxBagEnvelopeValidator.Validate(idxSource, bagSource, policy.Audio, context);
			Assert.Throws<ObjectDisposedException>(() => idxSource.OpenRead());
			Assert.Throws<ObjectDisposedException>(() => bagSource.OpenRead());
			audio.Dispose();
			var package = envelope.OpenPackage("audio.bag", context);
			var wave = package.GetStream("sound.wav");
			envelope.Dispose();
			package.Dispose();
			var bytes = ReadWithoutDisposing(wave);
			Assert.That(bytes[^4..], Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
			Assert.That(stream.TotalBytesRead, Is.LessThan(4096));
			Assert.That(stream.DisposeCalls, Is.EqualTo(0));
			wave.Dispose();
			Assert.That(stream.DisposeCalls, Is.EqualTo(1));

			var above = BuildSparseLanguageAudioChain(bagLength + 1, audioLength, payload);
			var aboveContext = new PackageReadContext(default, new NeverDeadline(), policy.Mix.MaximumDepth,
				policy.Mix.MaximumEntriesAggregate, policy.Mix.MaximumAddressedBytes, policy);
			Assert.That(fileSystem.TryParsePackage(above, "language.mix", aboveContext,
				out var aboveLanguage), Is.True);
			using (aboveLanguage)
			using (var aboveAudio = (IValidatedMixPackage)aboveLanguage.OpenPackage("audio.mix", fileSystem))
			{
				var failure = Assert.Throws<PackageEnvelopeException>(() => IdxBagEnvelopeValidator.Validate(
					aboveAudio.OpenEntrySource(PackageEntry.HashFilename("audio.idx", PackageHashType.CRC32)),
					aboveAudio.OpenEntrySource(PackageEntry.HashFilename("audio.bag", PackageHashType.CRC32)),
					policy.Audio, aboveContext));
				Assert.That(failure.Failure, Is.EqualTo(PackageEnvelopeFailure.LimitExceeded));
			}
			Assert.That(above.TotalBytesRead, Is.LessThan(4096));
			Assert.That(above.DisposeCalls, Is.EqualTo(1));

			var oversizedAudio = BuildSparseLanguageAudioChain(bagLength, audioLength + 1, payload);
			var oversizedAudioContext = new PackageReadContext(default, new NeverDeadline(), policy.Mix.MaximumDepth,
				policy.Mix.MaximumEntriesAggregate, policy.Mix.MaximumAddressedBytes, policy);
			IReadOnlyPackage escaped = null;
			var audioFailure = Assert.Throws<PackageEnvelopeException>(() => fileSystem.TryParsePackage(
				oversizedAudio, "language.mix", oversizedAudioContext, out escaped));
			Assert.That(audioFailure.Failure, Is.EqualTo(PackageEnvelopeFailure.LimitExceeded));
			Assert.That(escaped, Is.Null);
			Assert.That(oversizedAudio.Position, Is.EqualTo(0));
			Assert.That(oversizedAudio.TotalBytesRead, Is.LessThan(4096));
			Assert.That(oversizedAudio.DisposeCalls, Is.EqualTo(0));
			oversizedAudio.Dispose();
			Assert.That(oversizedAudio.DisposeCalls, Is.EqualTo(1));
		}

		[TestCase("envelope", "package", "wave")]
		[TestCase("envelope", "wave", "package")]
		[TestCase("package", "envelope", "wave")]
		[TestCase("package", "wave", "envelope")]
		[TestCase("wave", "envelope", "package")]
		[TestCase("wave", "package", "envelope")]
		public void EveryEnvelopePackageAndWaveDisposalOrderClosesBackingExactlyOnce(
			string first, string second, string third)
		{
			var context = Context();
			var backing = new TrackingStream(BuildAudioMix(BuildIdx(Record("sound", 0, 4, 1, 6, 0)),
				new byte[4]));
			var mix = MixEnvelopeValidator.Validate(backing, MixLimits(),
				PackageHashType.CRC32, new AudioResolver(), context);
			var envelope = IdxBagEnvelopeValidator.Validate(
				mix.OpenEntrySource(PackageEntry.HashFilename("audio.idx", PackageHashType.CRC32), context),
				mix.OpenEntrySource(PackageEntry.HashFilename("audio.bag", PackageHashType.CRC32), context),
				Limits(), context);
			mix.Dispose();
			var package = envelope.OpenPackage("audio.bag", context);
			var wave = package.GetStream("sound.wav");
			Assert.That(wave.ReadByte(), Is.EqualTo('R'));
			var disposals = new Dictionary<string, Action>(StringComparer.Ordinal)
			{
				{ "envelope", envelope.Dispose },
				{ "package", package.Dispose },
				{ "wave", wave.Dispose },
			};

			disposals[first]();
			Assert.That(backing.DisposeCalls, Is.EqualTo(0));
			disposals[second]();
			Assert.That(backing.DisposeCalls, Is.EqualTo(0));
			disposals[third]();
			Assert.That(backing.DisposeCalls, Is.EqualTo(1));
			disposals[first]();
			disposals[second]();
			disposals[third]();
			Assert.That(backing.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void OwnershipTransfersAtCallEntryAndFinalChildClosesTheBagOnce()
		{
			var context = Context();
			var backing = new TrackingStream(BuildAudioMix(BuildIdx(Record("sound", 0, 4, 1, 6, 0)),
				new byte[4]));
			var mix = MixEnvelopeValidator.Validate(backing, MixLimits(),
				PackageHashType.CRC32, new AudioResolver(), context);
			var envelope = IdxBagEnvelopeValidator.Validate(
				mix.OpenEntrySource(PackageEntry.HashFilename("audio.idx", PackageHashType.CRC32), context),
				mix.OpenEntrySource(PackageEntry.HashFilename("audio.bag", PackageHashType.CRC32), context),
				Limits(), context);
			mix.Dispose();
			var package = envelope.OpenPackage("audio.bag", context);
			var wave = package.GetStream("sound.wav");

			envelope.Dispose();
			package.Dispose();
			Assert.That(backing.DisposeCalls, Is.EqualTo(0));
			var signature = new byte[4];
			Assert.That(wave.Read(signature, 0, signature.Length), Is.EqualTo(signature.Length));
			Assert.That(Encoding.ASCII.GetString(signature), Is.EqualTo("RIFF"));
			wave.Dispose();
			Assert.That(backing.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void PairIdentityMismatchBetweenTwoValidatedMixIndexesIsRejected()
		{
			var context = Context();
			using var first = MixEnvelopeValidator.Validate(new MemoryStream(BuildAudioMix(
				BuildIdx(Record("sound", 0, 4, 1, 6, 0)), new byte[4])), MixLimits(),
				PackageHashType.CRC32, new AudioResolver(), context);
			using var second = MixEnvelopeValidator.Validate(new MemoryStream(BuildAudioMix(
				BuildIdx(Record("sound", 0, 4, 1, 6, 0)), new byte[4])), MixLimits(),
				PackageHashType.CRC32, new AudioResolver(), context);

			Assert.Throws<PackageEnvelopeException>(() => IdxBagEnvelopeValidator.Validate(
				first.OpenEntrySource(PackageEntry.HashFilename("audio.idx", PackageHashType.CRC32), context),
				second.OpenEntrySource(PackageEntry.HashFilename("audio.bag", PackageHashType.CRC32), context),
				Limits(), context));
		}

		[Test]
		public void DirectBoundedBagLoaderRejectsNullOrPolicylessContextBeforeReading()
		{
			var loader = (IBoundedPackageLoader)new AudioBagLoader();
			var nullContext = new TrackingStream(new byte[] { 1 });
			Assert.Throws<ArgumentNullException>(() => loader.TryParsePackage(
				nullContext, "audio.bag", null, null, out _));
			Assert.That(nullContext.ReadCalls, Is.EqualTo(0));

			var policyless = new TrackingStream(new byte[] { 1 });
			Assert.Throws<InvalidOperationException>(() => loader.TryParsePackage(
				policyless, "audio.bag", null, Context(), out _));
			Assert.That(policyless.ReadCalls, Is.EqualTo(0));
		}

		[Test]
		public void IdxBagByteRecordBagAndSpanLimitsCoverBelowExactAndAbove()
		{
			var idx = BuildIdx(Record("sound", 0, 4, 1, 6, 0));
			Assert.That(idx.Length, Is.EqualTo(48));
			AssertRejected(idx, 4, "idx below", Limits(idx: 47));
			ValidatePair(idx, new byte[4], Limits(idx: 48)).Envelope.Dispose();

			AssertRejected(idx, 4, "bag below", Limits(bag: 3));
			ValidatePair(idx, new byte[4], Limits(bag: 4)).Envelope.Dispose();
			AssertRejected(idx, 4, "span below", Limits(span: 3));
			ValidatePair(idx, new byte[4], Limits(span: 4)).Envelope.Dispose();

			var two = BuildIdx(Record("one", 0, 4, 1, 6, 0), Record("two", 4, 4, 1, 6, 0));
			AssertRejected(two, 8, "record below", Limits(records: 1));
			ValidatePair(two, new byte[8], Limits(records: 2)).Envelope.Dispose();
		}

		[Test]
		public void ExactPolicyPairNamesSameSourceAndReplacementLedgerAreClosed()
		{
			var audioLimits = Limits();
			var mixLimits = MixLimits();
			var policy = Policy(mixLimits, audioLimits);
			var context = new PackageReadContext(default, new NeverDeadline(), 8, 64, 8192, policy);
			using var mix = MixEnvelopeValidator.Validate(new MemoryStream(BuildAudioMix(
				BuildIdx(Record("sound", 0, 4, 1, 6, 0)), new byte[4])), mixLimits,
				PackageHashType.CRC32, new AudioResolver(), context);
			var idxHash = PackageEntry.HashFilename("audio.idx", PackageHashType.CRC32);
			var bagHash = PackageEntry.HashFilename("audio.bag", PackageHashType.CRC32);

			Assert.Throws<InvalidOperationException>(() => IdxBagEnvelopeValidator.Validate(
				mix.OpenEntrySource(idxHash, context), mix.OpenEntrySource(bagHash, context),
				Limits(), context));
			var same = mix.OpenEntrySource(idxHash, context);
			Assert.Throws<PackageEnvelopeException>(() => IdxBagEnvelopeValidator.Validate(
				same, same, audioLimits, context));

			using var envelope = IdxBagEnvelopeValidator.Validate(
				mix.OpenEntrySource(idxHash, context), mix.OpenEntrySource(bagHash, context),
				audioLimits, context);
			Assert.Throws<ArgumentException>(() => envelope.OpenPackage("other.bag", context));
			Assert.Throws<InvalidOperationException>(() => envelope.OpenPackage("audio.bag", Context()));
			using var package = envelope.OpenPackage("audio.bag", context);
			Assert.That(package.Contains("sound.wav"), Is.True);
		}

		[Test]
		public void ImaWaveFactUsesValidatedBlockArithmetic()
		{
			var pair = ValidatePair(BuildIdx(Record("sound", 0, 15, 22_050, 8, 5)), new byte[15]);
			using var envelope = pair.Envelope;
			using var package = envelope.OpenPackage("audio.bag", pair.Context);
			using var wave = package.GetStream("sound.wav");
			var bytes = wave.ReadAllBytes();
			Assert.That(Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("RIFF"));
			var fact = FindAscii(bytes, "fact");
			Assert.That(fact, Is.GreaterThanOrEqualTo(0));
			Assert.That(BitConverter.ToUInt32(bytes, fact + 8), Is.EqualTo(9U));
		}

		static IdxRecord Record(string name, uint offset, uint length, uint rate, uint flags, uint chunk) =>
			new(NameBytes(name), offset, length, rate, flags, chunk);

		static IdxRecord RecordWithRawName(byte[] name, uint offset, uint length, uint rate, uint flags, uint chunk) =>
			new(name, offset, length, rate, flags, chunk);

		static byte[] NameBytes(string name)
		{
			var bytes = new byte[16];
			Encoding.ASCII.GetBytes(name).CopyTo(bytes, 0);
			return bytes;
		}

		static byte[] BuildIdx(params IdxRecord[] records)
		{
			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
			{
				writer.Write(Encoding.ASCII.GetBytes("GABA"));
				writer.Write(2);
				writer.Write(records.Length);
				foreach (var record in records)
				{
					writer.Write(record.Name);
					writer.Write(record.Offset);
					writer.Write(record.Length);
					writer.Write(record.SampleRate);
					writer.Write(record.Flags);
					writer.Write(record.ChunkSize);
				}
			}

			return stream.ToArray();
		}

		static byte[] BuildIdxHeader(int count)
		{
			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
			writer.Write(Encoding.ASCII.GetBytes("GABA"));
			writer.Write(2);
			writer.Write(count);
			return stream.ToArray();
		}

		static void AssertRejected(IdxRecord record, int bagLength, string label,
			IdxBagEnvelopeLimits limits = null, MixEnvelopeLimits mixLimits = null) =>
			AssertRejected(new[] { record }, bagLength, label, limits, mixLimits);

		static void AssertRejected(IdxRecord[] records, int bagLength, string label,
			IdxBagEnvelopeLimits limits = null, MixEnvelopeLimits mixLimits = null) =>
			AssertRejected(BuildIdx(records), bagLength, label, limits, mixLimits);

		static void AssertRejected(byte[] idx, int bagLength, string label,
			IdxBagEnvelopeLimits limits = null, MixEnvelopeLimits mixLimits = null)
		{
			Assert.Throws<PackageEnvelopeException>(() =>
			{
				var pair = ValidatePair(idx, new byte[bagLength], limits, mixLimits);
				pair.Envelope.Dispose();
			}, label);
		}

		static (ValidatedIdxBagIndex Envelope, PackageReadContext Context) ValidatePair(byte[] idx, byte[] bag,
			IdxBagEnvelopeLimits limits = null, MixEnvelopeLimits mixLimits = null)
		{
			var context = Context();
			using var mix = MixEnvelopeValidator.Validate(new MemoryStream(BuildAudioMix(idx, bag)),
				mixLimits ?? MixLimits(), PackageHashType.CRC32, new AudioResolver(), context);
			var envelope = IdxBagEnvelopeValidator.Validate(
				mix.OpenEntrySource(PackageEntry.HashFilename("audio.idx", PackageHashType.CRC32), context),
				mix.OpenEntrySource(PackageEntry.HashFilename("audio.bag", PackageHashType.CRC32), context),
				limits ?? Limits(), context);
			return (envelope, context);
		}

		static MixEnvelopeLimits MixLimits(long segment = 4096, long addressed = 8192) =>
			new(8, 8, 64, 4096, 4096, 32, segment, addressed);

		static PackageEnvelopePolicy Policy(MixEnvelopeLimits mix, IdxBagEnvelopeLimits audio) => new(
			new ZipEnvelopeLimits(256, 8, 8, 4096, 4096, 8192, 100),
			new PngReadLimits(64, 4096, 16384, 32, 4096, 100),
			new YamlEnvelopeLimits(4096, 128, 8, 512, 256), mix, audio);

		static int FindAscii(byte[] bytes, string value)
		{
			var needle = Encoding.ASCII.GetBytes(value);
			for (var i = 0; i <= bytes.Length - needle.Length; i++)
				if (bytes.AsSpan(i, needle.Length).SequenceEqual(needle))
					return i;
			return -1;
		}

		static byte[] ReadWithoutDisposing(Stream stream)
		{
			using var output = new MemoryStream();
			stream.CopyTo(output);
			return output.ToArray();
		}

		static byte[] BuildAudioMix(byte[] idx, byte[] bag)
		{
			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
			writer.Write((ushort)0);
			writer.Write((ushort)0);
			writer.Write((ushort)2);
			writer.Write(checked((uint)(idx.Length + bag.Length)));
			writer.Write(PackageEntry.HashFilename("audio.idx", PackageHashType.CRC32));
			writer.Write(0U);
			writer.Write((uint)idx.Length);
			writer.Write(PackageEntry.HashFilename("audio.bag", PackageHashType.CRC32));
			writer.Write((uint)idx.Length);
			writer.Write((uint)bag.Length);
			writer.Write(idx);
			writer.Write(bag);
			return stream.ToArray();
		}

		static byte[] BuildMixHeader(uint dataSize, params (uint Hash, uint Offset, uint Length)[] entries)
		{
			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
			writer.Write((ushort)0);
			writer.Write((ushort)0);
			writer.Write(checked((ushort)entries.Length));
			writer.Write(dataSize);
			foreach (var entry in entries)
			{
				writer.Write(entry.Hash);
				writer.Write(entry.Offset);
				writer.Write(entry.Length);
			}
			return stream.ToArray();
		}

		static SparseReadStream BuildSparseLanguageAudioChain(long bagLength, long audioLength, byte[] payload)
		{
			var bagLength32 = checked((uint)bagLength);
			var idx = BuildIdx(Record("sound", checked(bagLength32 - (uint)payload.Length),
				(uint)payload.Length, 22_050, 6, 0));
			const long AudioHeaderLength = 10 + 3 * 12;
			var audioDataLength64 = checked(audioLength - AudioHeaderLength);
			var fillerLength64 = checked(audioDataLength64 - idx.LongLength - bagLength);
			if (fillerLength64 <= 0)
				throw new ArgumentOutOfRangeException(nameof(audioLength));

			var audioDataLength = checked((uint)audioDataLength64);
			var fillerOffset = checked((uint)(idx.LongLength + bagLength));
			var fillerLength = checked((uint)fillerLength64);
			var audioHeader = BuildMixHeader(audioDataLength,
				(PackageEntry.HashFilename("audio.idx", PackageHashType.CRC32), 0, (uint)idx.Length),
				(PackageEntry.HashFilename("audio.bag", PackageHashType.CRC32), (uint)idx.Length, bagLength32),
				(PackageEntry.HashFilename("sparse filler.bin", PackageHashType.CRC32), fillerOffset, fillerLength));
			if (checked(audioHeader.LongLength + audioDataLength64) != audioLength)
				throw new InvalidOperationException("The sparse audio MIX geometry is inconsistent.");

			var languageHeader = BuildMixHeader(checked((uint)audioLength),
				(PackageEntry.HashFilename("audio.mix", PackageHashType.CRC32), 0, checked((uint)audioLength)));
			var audioOffset = languageHeader.LongLength;
			var idxOffset = checked(audioOffset + audioHeader.LongLength);
			var bagOffset = checked(idxOffset + idx.LongLength);
			return new SparseReadStream(checked(languageHeader.LongLength + audioLength),
				(languageHeader, 0), (audioHeader, audioOffset), (idx, idxOffset),
				(payload, checked(bagOffset + bagLength - payload.LongLength)));
		}

		sealed record IdxRecord(byte[] Name, uint Offset, uint Length, uint SampleRate, uint Flags, uint ChunkSize);

		sealed class NeverDeadline : IPackageReadDeadline
		{
			public void ThrowIfExpired() { }
		}

		sealed class AudioResolver : IMixFilenameResolver
		{
			public bool TryResolve(uint hash, PackageHashType hashType, out string canonicalName)
			{
				foreach (var name in new[] { "audio.idx", "audio.bag" })
					if (PackageEntry.HashFilename(name, hashType) == hash)
					{
						canonicalName = name;
						return true;
					}
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
					disposed = true;
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
