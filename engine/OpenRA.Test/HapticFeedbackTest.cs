#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class HapticFeedbackTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		[TestCase(0f, 0)]
		[TestCase(-1f, 0)]
		[TestCase(0.75f, 0.75f)]
		[TestCase(2f, 1f)]
		public void NearbyHapticStrengthTracksAudioVolume(float audioVolume, float expected)
		{
			Assert.That(HapticStrengthPolicy.Resolve(audioVolume, WPos.Zero, WPos.Zero), Is.EqualTo(expected));
		}

		[Test]
		public void DistantHapticStrengthUsesWorldAudioAttenuation()
		{
			var strength = HapticStrengthPolicy.Resolve(0.8f, WPos.Zero, new WPos(136533, 0, 0));

			Assert.That(strength, Is.EqualTo(0.04f).Within(0.002f));
		}

		[Test]
		public void NuclearAndLightningAudioImpactsDeclareHapticFeedback()
		{
			var root = RepositoryRoot();
			var explosions = File.ReadAllText(Path.Combine(root, "mods", "ra2", "weapons", "explosions.yaml"));
			var lightning = File.ReadAllText(Path.Combine(root, "mods", "ra2", "weapons", "zaps.yaml"));

			StringAssert.Contains("Warhead@22Haptic: HapticFeedback", explosions);
			StringAssert.Contains("Effect: NuclearExplosion", explosions);
			StringAssert.Contains("Warhead@4Haptic: HapticFeedback", lightning);
			StringAssert.Contains("Effect: LightningStrike", lightning);
		}

		[Test]
		public void IosPlatformProvidesNativeHapticEngine()
		{
			var root = RepositoryRoot();
			var platform = File.ReadAllText(Path.Combine(root, "ios", "OpenRA.Platforms.iOS", "IosPlatform.cs"));
			var haptics = File.ReadAllText(Path.Combine(root, "ios", "OpenRA.Platforms.iOS", "IosHapticEngine.cs"));

			StringAssert.Contains("CreateHaptics()", platform);
			StringAssert.Contains("new IosHapticEngine()", platform);
			StringAssert.Contains("ImpactOccurred", haptics);
		}
	}
}
