using System;
using UIKit;

namespace OpenRA.Platforms.Default
{
	sealed class IosHapticEngine : IHapticEngine
	{
		readonly UIImpactFeedbackGenerator nuclear = new(UIImpactFeedbackStyle.Heavy);
		readonly UIImpactFeedbackGenerator lightning = new(UIImpactFeedbackStyle.Medium);

		public void Play(HapticEffect effect, float intensity)
		{
			var strength = Math.Clamp(intensity, 0f, 1f);
			if (strength <= 0f)
				return;

			UIApplication.SharedApplication.BeginInvokeOnMainThread(() =>
			{
				var generator = effect == HapticEffect.NuclearExplosion ? nuclear : lightning;
				generator.Prepare();
				generator.ImpactOccurred((nfloat)strength);
			});
		}

		public void Dispose()
		{
			nuclear.Dispose();
			lightning.Dispose();
		}
	}
}
