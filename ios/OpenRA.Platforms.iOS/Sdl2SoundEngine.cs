using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OpenRA.Support;
using SDL2;

namespace OpenRA.Platforms.Default
{
	sealed class Sdl2SoundSource : ISoundSource
	{
		public readonly byte[] Data;
		public readonly int Channels;
		public readonly int SampleBits;
		public readonly int SampleRate;

		public Sdl2SoundSource(byte[] data, int channels, int sampleBits, int sampleRate)
		{
			Data = data;
			Channels = channels;
			SampleBits = sampleBits;
			SampleRate = sampleRate;
		}

		public void Dispose() { }
	}

	sealed class Sdl2Sound : ISound
	{
		readonly PcmAudioMixer mixer;
		public readonly PcmPlayback Playback;
		public float Volume { get => Playback.Volume; set => Playback.Volume = value; }
		public float SeekPosition => Playback.SeekPosition;
		public bool Complete => Playback.Complete;
		public Sdl2Sound(PcmAudioMixer mixer, PcmPlayback playback)
		{
			this.mixer = mixer;
			Playback = playback;
		}

		public void SetPosition(WPos pos) { mixer.SetPlaybackPosition(Playback, pos); }
	}

	sealed class Sdl2SoundEngine : ISoundEngine
	{
		readonly PcmAudioMixer mixer;
		readonly List<Sdl2Sound> sounds = new();
		readonly uint device;
		readonly Thread mixerThread;
		readonly CancellationTokenSource cancel = new();
		readonly uint targetQueuedBytes;
		long audioCallbackCount;

		public bool Dummy => false;
		public float Volume { get => mixer.Volume; set => mixer.Volume = value; }

		public Sdl2SoundEngine()
		{
			if ((SDL.SDL_WasInit(SDL.SDL_INIT_AUDIO) & SDL.SDL_INIT_AUDIO) == 0 &&
				SDL.SDL_InitSubSystem(SDL.SDL_INIT_AUDIO) != 0)
				throw new InvalidOperationException("SDL audio subsystem failed: " + SDL.SDL_GetError());

			var desired = new SDL.SDL_AudioSpec
			{
				freq = 48000,
				format = SDL.AUDIO_F32SYS,
				channels = 2,
				samples = 1024,
				callback = null
			};

			device = SDL.SDL_OpenAudioDevice(IntPtr.Zero, 0, ref desired, out var obtained, 0);
			if (device == 0)
				throw new InvalidOperationException("SDL audio device failed: " + SDL.SDL_GetError());

			var frames = obtained.samples > 0 ? obtained.samples : (ushort)1024;
			var channels = obtained.channels > 0 ? obtained.channels : (byte)2;
			mixer = new PcmAudioMixer(obtained.freq, channels);
			targetQueuedBytes = (uint)(frames * channels * sizeof(float) * 3);

			mixerThread = new Thread(MixLoop)
			{
				IsBackground = true,
				Name = "OpenRA iOS Audio"
			};
			mixerThread.Start();
			SDL.SDL_PauseAudioDevice(device, 0);
			Log.Write("sound", $"SDL iOS queued audio started: {obtained.freq} Hz, {channels} channels, float32");
		}

		unsafe void MixLoop()
		{
			var samples = (int)(targetQueuedBytes / (3 * sizeof(float)));
			if (samples < 256)
				samples = 1024;

			var buffer = new float[samples];
			while (!cancel.IsCancellationRequested)
			{
				try
				{
					if (SDL.SDL_GetQueuedAudioSize(device) >= targetQueuedBytes)
					{
						Thread.Sleep(5);
						continue;
					}

					mixer.Mix(buffer);
					System.Threading.Interlocked.Increment(ref audioCallbackCount);
					fixed (float* p = buffer)
						SDL.SDL_QueueAudio(device, (IntPtr)p, (uint)(buffer.Length * sizeof(float)));
				}
				catch (Exception e)
				{
					Log.Write("sound", $"SDL iOS queued audio mix failed: {e}");
					Thread.Sleep(20);
				}
			}
		}

		public SoundDevice[] AvailableDevices() => new[] { new SoundDevice(null, "iPad Audio Output") };
		public ISoundSource AddSoundSourceFromMemory(byte[] data, int channels, int sampleBits, int sampleRate) =>
			new Sdl2SoundSource(data, channels, sampleBits, sampleRate);

		public ISound Play2D(ISoundSource source, bool loop, bool relative, WPos pos, float volume, bool attenuateVolume)
		{
			if (source is not Sdl2SoundSource pcm)
				return null;

			return Add(pcm.Data, pcm.Channels, pcm.SampleBits, pcm.SampleRate, loop, relative, pos, volume);
		}

		public ISound Play2DStream(Stream stream, int channels, int sampleBits, int sampleRate, bool loop, bool relative, WPos pos, float volume) =>
			Add(stream.ReadAllBytes(), channels, sampleBits, sampleRate, loop, relative, pos, volume);

		Sdl2Sound Add(byte[] data, int channels, int sampleBits, int sampleRate, bool loop, bool relative, WPos pos, float volume)
		{
			DiagnosticTrace.Instant(DiagnosticSubsystem.Audio, "Audio.Play", data.Length, mixer.ActivePlaybackCount);
			var playback = mixer.Play(data, channels, sampleBits, sampleRate, loop, volume, relative, pos);
			var sound = new Sdl2Sound(mixer, playback);
			sounds.RemoveAll(s => s.Complete);
			sounds.Add(sound);
			return sound;
		}

		public void PauseSound(ISound sound, bool paused) { if (sound is Sdl2Sound s) s.Playback.Paused = paused; }
		public void SetAllSoundsPaused(bool paused) { foreach (var s in sounds) s.Playback.Paused = paused; }
		public void StopSound(ISound sound)
		{
			if (sound is not Sdl2Sound s)
				return;

			mixer.Stop(s.Playback);
			sounds.Remove(s);
		}

		public void StopAllSounds()
		{
			DiagnosticTrace.Instant(DiagnosticSubsystem.Audio, "Audio.StopAll", mixer.ActivePlaybackCount, audioCallbackCount);
			mixer.StopAll();
			sounds.Clear();
		}
		public void SetListenerPosition(WPos position) { mixer.SetListenerPosition(position); }
		public void SetSoundVolume(float volume, ISound music, ISound video)
		{
			foreach (var sound in sounds.Where(s => s != music && s != video))
				sound.Volume = volume;
		}
		public void SetSoundLooping(bool looping, ISound sound) { if (sound is Sdl2Sound s) s.Playback.Loop = looping; }
		public void SetSoundPosition(ISound sound, WPos position)
		{
			if (sound is Sdl2Sound s)
				s.SetPosition(position);
		}

		public void Dispose()
		{
			cancel.Cancel();
			StopAllSounds();
			mixerThread.Join(500);
			SDL.SDL_CloseAudioDevice(device);
			cancel.Dispose();
		}
	}
}
