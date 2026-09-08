#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Mods.Cnc.FileSystem;
using OpenRA.Mods.Common.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	[NonParallelizable]
	public sealed class WavReaderSafetyTest
	{
		sealed class TestPackage : IReadOnlyPackage
		{
			readonly IReadOnlyDictionary<string, byte[]> files;

			public string Name => "test";
			public IEnumerable<string> Contents => files.Keys;

			public TestPackage(params (string Name, byte[] Data)[] files)
			{
				this.files = files.ToDictionary(f => f.Name, f => f.Data);
			}

			public Stream GetStream(string filename) =>
				files.TryGetValue(filename, out var data) ? new MemoryStream(data, writable: false) : null;
			public bool Contains(string filename) => files.ContainsKey(filename);
			public IReadOnlyPackage OpenPackage(string filename, OpenRA.FileSystem.FileSystem context) => null;
			public void Dispose() { }
		}

		sealed class TestSoundSource : ISoundSource
		{
			public void Dispose() { }
		}

		sealed class TestSoundEngine : ISoundEngine
		{
			public int PlayCalls { get; private set; }
			public int StreamPlayCalls { get; private set; }
			public bool Dummy => false;
			public float Volume { get; set; }

			public SoundDevice[] AvailableDevices() => Array.Empty<SoundDevice>();
			public ISoundSource AddSoundSourceFromMemory(byte[] data, int channels, int sampleBits, int sampleRate) =>
				new TestSoundSource();
			public ISound Play2D(ISoundSource source, bool loop, bool relative, WPos pos, float volume, bool attenuateVolume)
			{
				PlayCalls++;
				return null;
			}

			public ISound Play2DStream(Stream stream, int channels, int sampleBits, int sampleRate,
				bool loop, bool relative, WPos pos, float volume)
			{
				StreamPlayCalls++;
				return null;
			}

			public void PauseSound(ISound sound, bool paused) { }
			public void StopSound(ISound sound) { }
			public void SetAllSoundsPaused(bool paused) { }
			public void StopAllSounds() { }
			public void SetListenerPosition(WPos position) { }
			public void SetSoundVolume(float volume, ISound music, ISound video) { }
			public void SetSoundLooping(bool looping, ISound sound) { }
			public void SetSoundPosition(ISound sound, WPos position) { }
			public void Dispose() { }
		}

		sealed class TestPlatform : IPlatform
		{
			readonly ISoundEngine soundEngine;

			public TestPlatform(ISoundEngine soundEngine)
			{
				this.soundEngine = soundEngine;
			}

			public ISoundEngine CreateSound(string device) => soundEngine;
			public IPlatformWindow CreateWindow(Size size, WindowMode windowMode, float scaleModifier,
				int vertexBatchSize, int indexBatchSize, int videoDisplay, GLProfile profile) =>
				throw new NotSupportedException();
			public IHapticEngine CreateHaptics() => throw new NotSupportedException();
			public IFont CreateFont(byte[] data) => throw new NotSupportedException();
		}

		sealed class TestFileSystem : IReadOnlyFileSystem
		{
			readonly byte[] data;

			public TestFileSystem(byte[] data)
			{
				this.data = data;
			}

			public Stream Open(string filename) => new MemoryStream(data, writable: false);
			public bool TryOpen(string filename, out Stream stream)
			{
				stream = Open(filename);
				return true;
			}

			public bool Exists(string filename) => true;
			public bool TryGetPackageContaining(string path, out IReadOnlyPackage package, out string filename)
			{
				package = null;
				filename = null;
				return false;
			}

			public bool IsExternalFile(string filename) => false;
		}

		sealed class ThrowingSoundFormat : ISoundFormat
		{
			readonly Exception failure;

			public bool Disposed { get; private set; }
			public int Channels => 1;
			public int SampleBits => 16;
			public int SampleRate => 22050;
			public float LengthInSeconds => 1;
			public ThrowingSoundFormat(Exception failure)
			{
				this.failure = failure;
			}

			public Stream GetPCMInputStream() => throw failure;
			public void Dispose() => Disposed = true;
		}

		sealed class ThrowingSoundLoader : ISoundLoader
		{
			readonly Exception failure;

			public int Attempts { get; private set; }
			public ThrowingSoundFormat Format { get; private set; }

			public ThrowingSoundLoader(Exception failure)
			{
				this.failure = failure;
			}

			public bool TryParseSound(Stream stream, out ISoundFormat sound)
			{
				Attempts++;
				Format = new ThrowingSoundFormat(failure);
				sound = Format;
				return true;
			}
		}

		sealed class StaticSoundFormat : ISoundFormat
		{
			readonly byte[] data = new byte[] { 1, 2, 3, 4, 5, 6 };

			public bool Disposed { get; private set; }
			public int Channels { get; }
			public int SampleBits { get; }
			public int SampleRate => 22050;
			public float LengthInSeconds => 1;

			public StaticSoundFormat(int channels, int sampleBits)
			{
				Channels = channels;
				SampleBits = sampleBits;
			}

			public Stream GetPCMInputStream() => new MemoryStream(data, writable: false);
			public void Dispose() => Disposed = true;
		}

		sealed class StaticSoundLoader : ISoundLoader
		{
			readonly int channels;
			readonly int sampleBits;

			public int Attempts { get; private set; }
			public StaticSoundFormat Format { get; private set; }

			public StaticSoundLoader(int channels, int sampleBits)
			{
				this.channels = channels;
				this.sampleBits = sampleBits;
			}

			public bool TryParseSound(Stream stream, out ISoundFormat sound)
			{
				Attempts++;
				Format = new StaticSoundFormat(channels, sampleBits);
				sound = Format;
				return true;
			}
		}

		static void WriteAscii(Stream stream, string text)
		{
			var bytes = Encoding.ASCII.GetBytes(text);
			stream.Write(bytes, 0, bytes.Length);
		}

		static void WriteChunk(Stream stream, string name, byte[] data, uint? declaredSize = null)
		{
			WriteAscii(stream, name);
			stream.Write(BitConverter.GetBytes(declaredSize ?? (uint)data.Length));
			stream.Write(data, 0, data.Length);
			if ((data.Length & 1) != 0)
				stream.WriteByte(0);
		}

		static byte[] PcmFormat(short channels = 1, short sampleBits = 16, int sampleRate = 22050)
		{
			var blockAlign = checked((short)(channels * sampleBits / 8));
			var bytesPerSecond = checked(sampleRate * blockAlign);
			using var stream = new MemoryStream();
			stream.Write(BitConverter.GetBytes((short)1));
			stream.Write(BitConverter.GetBytes(channels));
			stream.Write(BitConverter.GetBytes(sampleRate));
			stream.Write(BitConverter.GetBytes(bytesPerSecond));
			stream.Write(BitConverter.GetBytes(blockAlign));
			stream.Write(BitConverter.GetBytes(sampleBits));
			return stream.ToArray();
		}

		static byte[] ImaAdpcmFormat()
		{
			using var stream = new MemoryStream();
			stream.Write(BitConverter.GetBytes((short)0x11));
			stream.Write(BitConverter.GetBytes((short)1));
			stream.Write(BitConverter.GetBytes(8000));
			stream.Write(BitConverter.GetBytes(7111));
			stream.Write(BitConverter.GetBytes((short)8));
			stream.Write(BitConverter.GetBytes((short)4));
			stream.Write(BitConverter.GetBytes((short)2));
			stream.Write(BitConverter.GetBytes((short)9));
			return stream.ToArray();
		}

		static byte[] MsAdpcmFormat()
		{
			using var stream = new MemoryStream();
			stream.Write(BitConverter.GetBytes((short)2));
			stream.Write(BitConverter.GetBytes((short)1));
			stream.Write(BitConverter.GetBytes(8000));
			stream.Write(BitConverter.GetBytes(8000));
			stream.Write(BitConverter.GetBytes((short)8));
			stream.Write(BitConverter.GetBytes((short)4));
			return stream.ToArray();
		}

		static byte[] Wave(params (string Name, byte[] Data, uint? DeclaredSize)[] chunks)
		{
			using var body = new MemoryStream();
			WriteAscii(body, "WAVE");
			foreach (var chunk in chunks)
				WriteChunk(body, chunk.Name, chunk.Data, chunk.DeclaredSize);

			using var wave = new MemoryStream();
			WriteAscii(wave, "RIFF");
			wave.Write(BitConverter.GetBytes((int)body.Length));
			body.Position = 0;
			body.CopyTo(wave);
			return wave.ToArray();
		}

		static byte[] PcmIndex(string name, int dataLength, uint sampleRate, uint flags)
		{
			using var index = new MemoryStream();
			WriteAscii(index, "GABA");
			index.Write(BitConverter.GetBytes(2));
			index.Write(BitConverter.GetBytes(1));

			var encodedName = new byte[16];
			Encoding.ASCII.GetBytes(name).CopyTo(encodedName, 0);
			index.Write(encodedName);
			index.Write(BitConverter.GetBytes(0u));
			index.Write(BitConverter.GetBytes((uint)dataLength));
			index.Write(BitConverter.GetBytes((uint)sampleRate));
			index.Write(BitConverter.GetBytes(flags));
			index.Write(BitConverter.GetBytes(0u));
			return index.ToArray();
		}

		[Test]
		public void UnknownChunksBeforeDataAreSkippedInMergedWaves()
		{
			var expected = new byte[] { 1, 2, 3, 4 };
			var wave = Wave(
				("JUNK", new byte[] { 7, 8, 9 }, null),
				("fmt ", PcmFormat(), null),
				("data", expected, null));

			using var merged = new MergedStream(
				new MemoryStream(wave[..12], writable: false),
				new MemoryStream(wave[12..], writable: false));

			Assert.That(WavReader.LoadSound(merged, out var openPcm, out _, out _, out _, out _), Is.True);
			using var pcm = openPcm();
			Assert.That(pcm.ReadAllBytes(), Is.EqualTo(expected));
		}

		[TestCase(6u, 1)]
		[TestCase(7u, 2)]
		public void AudioBagPcmHeaderUsesA32BitByteRateAndExposesTheOriginalSamples(uint flags, short expectedChannels)
		{
			var expected = new byte[] { 1, 2, 3, 4 };
			var loader = new AudioBagLoader();
			var fileSystem = new OpenRA.FileSystem.FileSystem("test",
				new Dictionary<string, Manifest>(), new IPackageLoader[] { loader });
			fileSystem.Mount(new TestPackage(("audio.idx", PcmIndex("pcm", expected.Length, 22050u, flags))));

			Assert.That(((IPackageLoader)loader).TryParsePackage(
				new MemoryStream(expected, writable: false), "audio.bag", fileSystem, out var package), Is.True);
			using (package)
			using (var wave = package.GetStream("pcm.wav"))
			{
				Assert.That(WavReader.LoadSound(wave, out var openPcm, out var channels, out _, out _, out _), Is.True);
				Assert.That(channels, Is.EqualTo(expectedChannels));
				using var pcm = openPcm();
				Assert.That(pcm.ReadAllBytes(), Is.EqualTo(expected));
			}
		}

		[Test]
		public void WaveWithoutDataChunkIsRejectedBeforeCreatingADeferredStream()
		{
			using var stream = new MemoryStream(Wave(("fmt ", PcmFormat(), null)));

			Assert.That(WavReader.LoadSound(stream, out var openPcm, out _, out _, out _, out _), Is.False);
			Assert.That(openPcm, Is.Null);
		}

		[Test]
		public void TruncatedDataChunkUsesOnlyTheBytesThatRemain()
		{
			var expected = new byte[] { 1, 2, 3, 4 };
			using var stream = new MemoryStream(Wave(
				("fmt ", PcmFormat(), null),
				("data", expected, 4096)));

			Assert.That(WavReader.LoadSound(stream, out var openPcm, out _, out _, out _, out _), Is.True);
			using var pcm = openPcm();
			Assert.That(pcm.ReadAllBytes(), Is.EqualTo(expected));
		}

		[Test]
		public void StreamingDataChunkSizeIsClampedBeforeIntRangeValidation()
		{
			var expected = new byte[] { 1, 2, 3, 4 };
			using var stream = new MemoryStream(Wave(
				("fmt ", PcmFormat(), null),
				("data", expected, uint.MaxValue)));

			Assert.That(WavReader.LoadSound(stream, out var openPcm, out _, out _, out _, out _), Is.True);
			using var pcm = openPcm();
			Assert.That(pcm.ReadAllBytes(), Is.EqualTo(expected));
		}

		[TestCase(3, 16)]
		[TestCase(1, 24)]
		public void UnsupportedPcmLayoutsAreRejectedBeforePlayback(short channels, short sampleBits)
		{
			using var stream = new MemoryStream(Wave(
				("fmt ", PcmFormat(channels, sampleBits), null),
				("data", new byte[] { 1, 2, 3, 4, 5, 6 }, null)));

			Assert.That(WavReader.LoadSound(stream, out var openPcm, out _, out _, out _, out _), Is.False);
			Assert.That(openPcm, Is.Null);
		}

		[Test]
		public void AudioBagPcmHeaderReportsSampleRateOverflowAsInvalidData()
		{
			var loader = new AudioBagLoader();
			var fileSystem = new OpenRA.FileSystem.FileSystem("test",
				new Dictionary<string, Manifest>(), new IPackageLoader[] { loader });
			fileSystem.Mount(new TestPackage(("audio.idx", PcmIndex("pcm", 4, uint.MaxValue, 6))));

			Assert.That(((IPackageLoader)loader).TryParsePackage(
				new MemoryStream(new byte[4], writable: false), "audio.bag", fileSystem, out var package), Is.True);
			using (package)
				Assert.Throws<InvalidDataException>(() => package.GetStream("pcm.wav"));
		}

		[Test]
		public void InvalidImaAdpcmStepIndexIsReportedAsInvalidData()
		{
			using var stream = new MemoryStream(Wave(
				("fmt ", ImaAdpcmFormat(), null),
				("fact", BitConverter.GetBytes(9), null),
				("data", new byte[] { 0, 0, 255, 0, 0, 0, 0, 0 }, null)));

			Assert.That(WavReader.LoadSound(stream, out var openPcm, out _, out _, out _, out _), Is.True);
			using var pcm = openPcm();
			Assert.Throws<InvalidDataException>(() => pcm.ReadAllBytes());
		}

		[Test]
		public void InvalidMsAdpcmPredictorIsReportedAsInvalidData()
		{
			using var stream = new MemoryStream(Wave(
				("fmt ", MsAdpcmFormat(), null),
				("data", new byte[] { 255, 16, 0, 0, 0, 0, 0, 0 }, null)));

			Assert.That(WavReader.LoadSound(stream, out var openPcm, out _, out _, out _, out _), Is.True);
			using var pcm = openPcm();
			Assert.Throws<InvalidDataException>(() => pcm.ReadAllBytes());
		}

		[Test]
		public void BrokenCachedSoundIsSilencedAfterOneLoadAttempt()
		{
			var previousSettings = Game.Settings;
			var engine = new TestSoundEngine();
			var loader = new ThrowingSoundLoader(new IOException("broken pcm payload"));
			try
			{
				Game.Settings = new Settings("missing-settings.yaml", new Arguments());
				Log.AddChannel("sound", null);
				using var sound = new Sound(new TestPlatform(engine), new SoundSettings());
				sound.Initialize(new ISoundLoader[] { loader }, new TestFileSystem(new byte[] { 1 }));

				Assert.That(sound.Play(SoundType.World, "broken.wav"), Is.Null);
				Assert.That(sound.Play(SoundType.World, "broken.wav"), Is.Null);
				Assert.That(loader.Attempts, Is.EqualTo(1));
				Assert.That(loader.Format.Disposed, Is.True);
				Assert.That(engine.PlayCalls, Is.Zero);
			}
			finally
			{
				Game.Settings = previousSettings;
			}
		}

		[TestCase(3, 16)]
		[TestCase(1, 24)]
		public void UnsupportedDecodedPcmLayoutsAreSilencedBeforeReachingTheBackend(int channels, int sampleBits)
		{
			var previousSettings = Game.Settings;
			var engine = new TestSoundEngine();
			var loader = new StaticSoundLoader(channels, sampleBits);
			try
			{
				Game.Settings = new Settings("missing-settings.yaml", new Arguments());
				Log.AddChannel("sound", null);
				using var sound = new Sound(new TestPlatform(engine), new SoundSettings());
				sound.Initialize(new ISoundLoader[] { loader }, new TestFileSystem(new byte[] { 1 }));

				Assert.That(sound.Play(SoundType.World, "unsupported.ogg"), Is.Null);
				Assert.That(sound.Play(SoundType.World, "unsupported.ogg"), Is.Null);
				Assert.That(loader.Attempts, Is.EqualTo(1));
				Assert.That(loader.Format.Disposed, Is.True);
				Assert.That(engine.PlayCalls, Is.Zero);
			}
			finally
			{
				Game.Settings = previousSettings;
			}
		}

		[TestCase(3, 16)]
		[TestCase(1, 24)]
		public void UnsupportedDirectPcmStreamsAreSilencedBeforeReachingTheBackend(int channels, int sampleBits)
		{
			var previousSettings = Game.Settings;
			var engine = new TestSoundEngine();
			try
			{
				Game.Settings = new Settings("missing-settings.yaml", new Arguments());
				Log.AddChannel("sound", null);
				using var sound = new Sound(new TestPlatform(engine), new SoundSettings());
				using var format = new StaticSoundFormat(channels, sampleBits);

				Assert.That(sound.Play(format), Is.Null);
				Assert.That(engine.StreamPlayCalls, Is.Zero);
			}
			finally
			{
				Game.Settings = previousSettings;
			}
		}

		[Test]
		public void EngineInvariantFailuresAreNotSilencedAsBadAudioData()
		{
			var previousSettings = Game.Settings;
			var engine = new TestSoundEngine();
			var loader = new ThrowingSoundLoader(new InvalidOperationException("engine invariant"));
			try
			{
				Game.Settings = new Settings("missing-settings.yaml", new Arguments());
				Log.AddChannel("sound", null);
				using var sound = new Sound(new TestPlatform(engine), new SoundSettings());
				sound.Initialize(new ISoundLoader[] { loader }, new TestFileSystem(new byte[] { 1 }));

				Assert.Throws<InvalidOperationException>(() => sound.Play(SoundType.World, "broken.wav"));
				Assert.That(loader.Format.Disposed, Is.True);
				Assert.That(engine.PlayCalls, Is.Zero);
			}
			finally
			{
				Game.Settings = previousSettings;
			}
		}
	}
}
