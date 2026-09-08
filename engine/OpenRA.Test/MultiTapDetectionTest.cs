#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using NUnit.Framework;
using OpenRA.Platforms.Default;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	[NonParallelizable]
	public sealed class MultiTapDetectionTest
	{
		[Test]
		public void ConsecutiveMousePressesAtTheSamePositionFormAMultiTap()
		{
			const byte button = 250;
			var position = new int2(120, 96);

			Assert.That(MultiTapDetection.DetectFromMouse(button, position), Is.EqualTo(1));
			Assert.That(MultiTapDetection.DetectFromMouse(button, position), Is.EqualTo(2));
		}

		[Test]
		public void CancelledMousePressDoesNotContributeToTheNextTap()
		{
			const byte button = 251;
			var position = new int2(120, 96);

			Assert.That(MultiTapDetection.DetectFromMouse(button, position), Is.EqualTo(1));
			MultiTapDetection.CancelFromMouse(button);
			Assert.That(MultiTapDetection.DetectFromMouse(button, position), Is.EqualTo(1));
		}
	}
}
