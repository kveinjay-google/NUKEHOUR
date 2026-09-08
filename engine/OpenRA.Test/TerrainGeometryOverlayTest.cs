#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class TerrainGeometryOverlayTest
	{
		[TestCase(true, true, false)]
		[TestCase(false, true, false)]
		[TestCase(true, false, true)]
		[TestCase(false, false, false)]
		public void StartupPolicyOnlyForcesOverlayOffOnIos(bool current, bool isIos, bool expected)
		{
			Assert.That(TerrainGeometryOverlay.StartupEnabled(current, isIos), Is.EqualTo(expected));
		}

		[TestCase(true, true, false)]
		[TestCase(false, true, false)]
		[TestCase(true, false, true)]
		[TestCase(false, false, false)]
		public void RenderPolicyPermanentlySuppressesOverlayOnIos(bool enabled, bool isIos, bool expected)
		{
			Assert.That(TerrainGeometryOverlay.ShouldRender(enabled, isIos), Is.EqualTo(expected));
		}
	}
}
