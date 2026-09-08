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

using System;
using NUnit.Framework;
using OpenRA.Support;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class PerformanceCategoryProfilerTest
	{
		sealed class SlowTrait { }
		sealed class FastTrait { }

		[Test]
		public void DisabledProfilerDoesNotReadClockOrAllocate()
		{
			var clockReads = 0;
			var profiler = new PerformanceCategoryProfiler(() =>
			{
				clockReads++;
				return 1;
			}, 1000);

			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var i = 0; i < 10000; i++)
				profiler.Measure("Trait", typeof(SlowTrait)).Dispose();
			var after = GC.GetAllocatedBytesForCurrentThread();

			Assert.That(clockReads, Is.Zero);
			Assert.That(after - before, Is.Zero);
		}

		[Test]
		public void SnapshotRanksByTotalTimeAndPreservesCounts()
		{
			long now = 0;
			var profiler = new PerformanceCategoryProfiler(() => now, 1000000) { Enabled = true };

			using (profiler.Measure("Trait", typeof(FastTrait)))
				now += 1000;
			using (profiler.Measure("Trait", typeof(SlowTrait)))
				now += 4000;
			using (profiler.Measure("Trait", typeof(SlowTrait)))
				now += 3000;

			var entries = profiler.SnapshotTop(20);

			Assert.That(entries, Has.Count.EqualTo(2));
			Assert.That(entries[0].Detail, Is.EqualTo(nameof(SlowTrait)));
			Assert.That(entries[0].CallCount, Is.EqualTo(2));
			Assert.That(entries[0].TotalMilliseconds, Is.EqualTo(7d).Within(0.001));
			Assert.That(entries[0].MaxMilliseconds, Is.EqualTo(4d).Within(0.001));
			Assert.That(entries[1].Detail, Is.EqualTo(nameof(FastTrait)));
		}

		[Test]
		public void SnapshotHonorsLimit()
		{
			long now = 0;
			var profiler = new PerformanceCategoryProfiler(() => now, 1000000) { Enabled = true };

			using (profiler.Measure("A"))
				now += 1000;
			using (profiler.Measure("B"))
				now += 2000;
			using (profiler.Measure("C"))
				now += 3000;

			Assert.That(profiler.SnapshotTop(2), Has.Count.EqualTo(2));
		}
	}
}
