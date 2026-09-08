using System;
using OpenRA.Primitives;

namespace OpenRA.Platforms.Default
{
	public sealed class IosPlatform : IPlatform
	{
		public IPlatformWindow CreateWindow(
			Size size, WindowMode windowMode, float scaleModifier, int vertexBatchSize, int indexBatchSize, int videoDisplay, GLProfile profile)
		{
			return new Sdl2PlatformWindow(size, windowMode, scaleModifier, vertexBatchSize, indexBatchSize, videoDisplay, profile);
		}

		public ISoundEngine CreateSound(string device)
		{
			try
			{
				return new Sdl2SoundEngine();
			}
			catch (Exception e)
			{
				Log.Write("sound", $"SDL iOS audio unavailable; continuing without audio: {e}");
				return new DummySoundEngine();
			}
		}

		public IHapticEngine CreateHaptics()
		{
			return new IosHapticEngine();
		}

		public IFont CreateFont(byte[] data)
		{
			return new FreeTypeFont(data);
		}
	}
}
