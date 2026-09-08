#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class PlatformHostOverrideTest
	{
		[Test]
		public void ExplicitIosHostOverridesDesktopRuntimeDetection()
		{
			Assert.That(Platform.ResolvePlatform(PlatformType.OSX, PlatformType.iOS), Is.EqualTo(PlatformType.iOS));
		}

		[Test]
		public void MissingHostOverrideKeepsDetectedPlatform()
		{
			Assert.That(Platform.ResolvePlatform(PlatformType.Linux, null), Is.EqualTo(PlatformType.Linux));
		}
	}
}
