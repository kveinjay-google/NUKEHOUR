using System;
using System.Collections.Generic;

namespace OpenRA.Support
{
	public sealed class PcmAudioMixer
	{
		const int ListenerHeight = 2133;
		const int ReferenceDistance = 6826;
		const int MaximumDistance = 136533;
		const int RolloffFactor = 1;

		readonly object sync = new();
		readonly List<PcmPlayback> playbacks = new();
		readonly int outputRate;
		readonly int outputChannels;
		WPos listenerPosition;

		public float Volume { get; set; } = 1f;

		public int ActivePlaybackCount
		{
			get
			{
				lock (sync)
					return playbacks.Count;
			}
		}

		public PcmAudioMixer(int outputRate, int outputChannels)
		{
			this.outputRate = outputRate;
			this.outputChannels = outputChannels;
		}

		public PcmPlayback Play(byte[] data, int channels, int sampleBits, int sampleRate, bool loop, float volume)
		{
			return Play(data, channels, sampleBits, sampleRate, loop, volume, true, WPos.Zero);
		}

		public PcmPlayback Play(byte[] data, int channels, int sampleBits, int sampleRate, bool loop, float volume,
			bool relative, WPos position)
		{
			if (channels is < 1 or > 2 || sampleBits is not (8 or 16) || sampleRate <= 0)
				throw new ArgumentOutOfRangeException(nameof(channels), "Only mono/stereo 8/16-bit PCM is supported.");

			var playback = new PcmPlayback(data, channels, sampleBits, sampleRate, loop, volume, relative, position);
			lock (sync)
				playbacks.Add(playback);

			return playback;
		}

		public void SetListenerPosition(WPos position)
		{
			lock (sync)
				listenerPosition = position + new WVec(0, 0, ListenerHeight);
		}

		public void SetPlaybackPosition(PcmPlayback playback, WPos position)
		{
			if (playback == null)
				return;

			lock (sync)
				playback.Position = position;
		}

		public void Stop(PcmPlayback playback)
		{
			if (playback == null)
				return;

			lock (sync)
			{
				playback.Stopped = true;
				playback.Complete = true;
				playbacks.Remove(playback);
			}
		}

		public void StopAll()
		{
			lock (sync)
			{
				foreach (var playback in playbacks)
				{
					playback.Stopped = true;
					playback.Complete = true;
				}

				playbacks.Clear();
			}
		}

		public void Mix(float[] output)
		{
			Array.Clear(output, 0, output.Length);
			lock (sync)
			{
				for (var i = playbacks.Count - 1; i >= 0; i--)
				{
					var sound = playbacks[i];
					if (sound.Stopped)
					{
						sound.Complete = true;
						playbacks.RemoveAt(i);
						continue;
					}

					if (sound.Paused)
						continue;

					MixSound(sound, output);
					if (sound.Complete)
						playbacks.RemoveAt(i);
				}
			}
		}

		void MixSound(PcmPlayback sound, float[] output)
		{
			var outputFrames = output.Length / outputChannels;
			var sourceFrames = sound.Data.Length / (sound.Channels * sound.BytesPerSample);
			var distanceGain = DistanceGain(sound);
			for (var frame = 0; frame < outputFrames; frame++)
			{
				var sourceFrame = (int)sound.FramePosition;
				if (sourceFrame >= sourceFrames)
				{
					if (!sound.Loop)
					{
						sound.Complete = true;
						break;
					}

					sound.FramePosition %= sourceFrames;
					sourceFrame = (int)sound.FramePosition;
				}

				for (var channel = 0; channel < outputChannels; channel++)
				{
					var sourceChannel = sound.Channels == 1 ? 0 : Math.Min(channel, sound.Channels - 1);
					var sample = ReadSample(sound, sourceFrame, sourceChannel) * sound.Volume * Volume * distanceGain;
					var index = frame * outputChannels + channel;
					output[index] = Math.Clamp(output[index] + sample, -1f, 1f);
				}

				sound.FramePosition += (double)sound.SampleRate / outputRate;
			}
		}

		float DistanceGain(PcmPlayback sound)
		{
			if (sound.Relative)
				return 1f;

			var distance = Math.Clamp((sound.Position - listenerPosition).Length, ReferenceDistance, MaximumDistance);
			return ReferenceDistance /
				(float)(ReferenceDistance + RolloffFactor * (distance - ReferenceDistance));
		}

		static float ReadSample(PcmPlayback sound, int frame, int channel)
		{
			var offset = (frame * sound.Channels + channel) * sound.BytesPerSample;
			if (sound.SampleBits == 16)
				return (short)(sound.Data[offset] | sound.Data[offset + 1] << 8) / 32768f;

			return (sound.Data[offset] - 128) / 128f;
		}
	}

	public sealed class PcmPlayback
	{
		internal readonly byte[] Data;
		internal readonly int Channels;
		internal readonly int SampleBits;
		internal readonly int SampleRate;
		internal readonly int BytesPerSample;
		internal double FramePosition;
		internal bool Stopped;
		internal readonly bool Relative;
		internal WPos Position;

		public bool Loop { get; set; }
		public bool Paused { get; set; }
		public bool Complete { get; internal set; }
		public float Volume { get; set; }
		public float SeekPosition => (float)(FramePosition / SampleRate);

		internal PcmPlayback(byte[] data, int channels, int sampleBits, int sampleRate, bool loop, float volume,
			bool relative, WPos position)
		{
			Data = data;
			Channels = channels;
			SampleBits = sampleBits;
			SampleRate = sampleRate;
			BytesPerSample = sampleBits / 8;
			Loop = loop;
			Volume = volume;
			Relative = relative;
			Position = position;
		}

		public void Stop() => Stopped = true;
	}
}
