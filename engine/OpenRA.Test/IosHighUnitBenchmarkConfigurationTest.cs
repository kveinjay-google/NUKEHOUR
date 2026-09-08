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

using System.Collections.Generic;
using NUnit.Framework;
using OpenRA.Mods.RA2.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosHighUnitBenchmarkConfigurationTest
	{
		[Test]
		public void MissingCountDisablesScenario()
		{
			var config = IosHighUnitBenchmarkConfiguration.Parse(_ => null);
			Assert.That(config.Enabled, Is.False);
		}

		[Test]
		public void ParsesSupportedScenarioWithoutLocaleDependencies()
		{
			var values = new Dictionary<string, string>
			{
				["OPENRA_IOS_PERF_COUNT"] = "600",
				["OPENRA_IOS_PERF_STATE"] = "combat",
				["OPENRA_IOS_PERF_CAMERA"] = "empty",
				["OPENRA_IOS_PERF_PAUSED"] = "true",
				["OPENRA_IOS_PERF_UNIT"] = "e1"
			};

			var config = IosHighUnitBenchmarkConfiguration.Parse(
				name => values.TryGetValue(name, out var value) ? value : null);

			Assert.That(config.Enabled, Is.True);
			Assert.That(config.Count, Is.EqualTo(600));
			Assert.That(config.State, Is.EqualTo(IosHighUnitBenchmarkState.Combat));
			Assert.That(config.Camera, Is.EqualTo(IosHighUnitBenchmarkCamera.Empty));
			Assert.That(config.Paused, Is.True);
			Assert.That(config.UnitType, Is.EqualTo("e1"));
		}

		[TestCase("99")]
		[TestCase("200")]
		[TestCase("1001")]
		[TestCase("invalid")]
		public void RejectsUnsupportedCounts(string count)
		{
			var config = IosHighUnitBenchmarkConfiguration.Parse(
				name => name == "OPENRA_IOS_PERF_COUNT" ? count : null);

			Assert.That(config.Enabled, Is.False);
		}
	}
}
