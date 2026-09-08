using NUnit.Framework;
using OpenRA.Support;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class PcmAudioMixerTest
	{
		static readonly byte[] FullScaleMonoSample = { 0xff, 0x7f };

		static float MixOneFrame(PcmAudioMixer mixer)
		{
			var output = new float[2];
			mixer.Mix(output);
			return output[0];
		}

		[Test]
		public void MixesMono16IntoStereoFloatOutput()
		{
			var mixer = new PcmAudioMixer(48000, 2);
			var source = new byte[] { 0, 0, 0xff, 0x7f };
			mixer.Play(source, 1, 16, 48000, false, 0.5f);

			var output = new float[4];
			mixer.Mix(output);

			Assert.That(output[0], Is.EqualTo(0).Within(0.001));
			Assert.That(output[1], Is.EqualTo(0).Within(0.001));
			Assert.That(output[2], Is.EqualTo(0.5).Within(0.01));
			Assert.That(output[3], Is.EqualTo(0.5).Within(0.01));
		}

		[Test]
		public void LoopingSoundContinuesAndStoppedSoundCompletes()
		{
			var mixer = new PcmAudioMixer(48000, 2);
			var looping = mixer.Play(new byte[] { 0xff, 0x7f }, 1, 16, 48000, true, 1f);
			var output = new float[8];
			mixer.Mix(output);
			Assert.That(looping.Complete, Is.False);

			looping.Stop();
			mixer.Mix(output);
			Assert.That(looping.Complete, Is.True);
		}

		[Test]
		public void MasterAndPerSoundVolumeAreApplied()
		{
			var mixer = new PcmAudioMixer(48000, 2) { Volume = 0.5f };
			mixer.Play(new byte[] { 0xff, 0x7f }, 1, 16, 48000, false, 0.5f);
			var output = new float[2];
			mixer.Mix(output);
			Assert.That(output[0], Is.EqualTo(0.25).Within(0.01));
		}

		[Test]
		public void StopReleasesPlaybackImmediately()
		{
			var mixer = new PcmAudioMixer(48000, 2);
			var playback = mixer.Play(new byte[48000 * 2 * 2], 2, 16, 48000, true, 1f);

			mixer.Stop(playback);

			Assert.That(playback.Complete, Is.True);
			Assert.That(mixer.ActivePlaybackCount, Is.Zero);
		}

		[Test]
		public void StopAllReleasesEveryPlaybackImmediately()
		{
			var mixer = new PcmAudioMixer(48000, 2);
			var first = mixer.Play(new byte[16], 2, 16, 48000, true, 1f);
			var second = mixer.Play(new byte[16], 2, 16, 48000, true, 1f);

			mixer.StopAll();

			Assert.That(first.Complete, Is.True);
			Assert.That(second.Complete, Is.True);
			Assert.That(mixer.ActivePlaybackCount, Is.Zero);
		}

		[Test]
		public void PositionalSoundUsesDesktopInverseDistanceCurve()
		{
			var mixer = new PcmAudioMixer(48000, 2);
			mixer.SetListenerPosition(WPos.Zero);
			mixer.Play(FullScaleMonoSample, 1, 16, 48000, true, 1f,
				relative: false, position: new WPos(13652, 0, 2133));

			Assert.That(MixOneFrame(mixer), Is.EqualTo(0.5f).Within(0.01));
		}

		[Test]
		public void PositionalSoundIsFullVolumeInsideReferenceDistance()
		{
			var mixer = new PcmAudioMixer(48000, 2);
			mixer.SetListenerPosition(WPos.Zero);
			mixer.Play(FullScaleMonoSample, 1, 16, 48000, true, 1f,
				relative: false, position: new WPos(6826, 0, 2133));

			Assert.That(MixOneFrame(mixer), Is.EqualTo(1f).Within(0.01));
		}

		[Test]
		public void PositionalSoundClampsAtDesktopMaximumDistance()
		{
			var mixer = new PcmAudioMixer(48000, 2);
			mixer.SetListenerPosition(WPos.Zero);
			mixer.Play(FullScaleMonoSample, 1, 16, 48000, true, 1f,
				relative: false, position: new WPos(273066, 0, 2133));

			Assert.That(MixOneFrame(mixer), Is.EqualTo(6826f / 136533f).Within(0.001));
		}

		[Test]
		public void RelativeSoundIgnoresListenerDistance()
		{
			var mixer = new PcmAudioMixer(48000, 2);
			mixer.SetListenerPosition(new WPos(273066, 0, 0));
			mixer.Play(FullScaleMonoSample, 1, 16, 48000, true, 1f,
				relative: true, position: WPos.Zero);

			Assert.That(MixOneFrame(mixer), Is.EqualTo(1f).Within(0.01));
		}

		[Test]
		public void MovingListenerUpdatesActivePositionalSound()
		{
			var mixer = new PcmAudioMixer(48000, 2);
			mixer.SetListenerPosition(WPos.Zero);
			mixer.Play(FullScaleMonoSample, 1, 16, 48000, true, 1f,
				relative: false, position: new WPos(13652, 0, 2133));

			Assert.That(MixOneFrame(mixer), Is.EqualTo(0.5f).Within(0.01));
			mixer.SetListenerPosition(new WPos(6826, 0, 0));
			Assert.That(MixOneFrame(mixer), Is.EqualTo(1f).Within(0.01));
		}

		[Test]
		public void MovingSourceUpdatesActivePositionalSound()
		{
			var mixer = new PcmAudioMixer(48000, 2);
			mixer.SetListenerPosition(WPos.Zero);
			var playback = mixer.Play(FullScaleMonoSample, 1, 16, 48000, true, 1f,
				relative: false, position: new WPos(13652, 0, 2133));

			Assert.That(MixOneFrame(mixer), Is.EqualTo(0.5f).Within(0.01));
			mixer.SetPlaybackPosition(playback, new WPos(6826, 0, 2133));
			Assert.That(MixOneFrame(mixer), Is.EqualTo(1f).Within(0.01));
		}
	}
}
