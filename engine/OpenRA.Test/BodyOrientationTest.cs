// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available to you under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class BodyOrientationTest
	{
		[Test]
		public void ZeroFacingsPreservesContinuousFacing()
		{
			var facing = new WAngle(317);
			var orientation = new BodyOrientationInfo();

			Assert.That(orientation.QuantizeFacing(facing, 0), Is.EqualTo(facing));
		}
	}
}
