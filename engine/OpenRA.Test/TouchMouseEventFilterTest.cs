#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software under the GNU General Public License.
 */
#endregion

using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class TouchMouseEventFilterTest
	{
		[Test]
		public void IosIgnoresMouseEventsSynthesizedFromTouch()
		{
			Assert.That(TouchMouseEventFilter.ShouldIgnore(true, TouchMouseEventFilter.TouchMouseId), Is.True);
		}

		[Test]
		public void IosKeepsRealMouseEvents()
		{
			Assert.That(TouchMouseEventFilter.ShouldIgnore(true, 1), Is.False);
		}

		[Test]
		public void DesktopKeepsSynthesizedMouseEventsForCompatibility()
		{
			Assert.That(TouchMouseEventFilter.ShouldIgnore(false, TouchMouseEventFilter.TouchMouseId), Is.False);
		}
	}
}
