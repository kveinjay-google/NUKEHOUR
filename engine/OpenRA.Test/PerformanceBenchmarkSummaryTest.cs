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
using OpenRA.Support;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class PerformanceBenchmarkSummaryTest
	{
		[Test]
		public void PercentileUsesLinearInterpolation()
		{
			var values = new[] { 50d, 10d, 40d, 20d, 30d };

			Assert.That(PerformanceBenchmarkSummary.Percentile(values, 0.05), Is.EqualTo(12d).Within(0.001));
			Assert.That(PerformanceBenchmarkSummary.Percentile(values, 0.50), Is.EqualTo(30d).Within(0.001));
			Assert.That(PerformanceBenchmarkSummary.Percentile(values, 0.95), Is.EqualTo(48d).Within(0.001));
		}

		[Test]
		public void SummarizeReportsLowFpsAndHighFrameTimeTails()
		{
			var samples = new List<PerformanceBenchmarkSample>
			{
				new(0, 50, 20, 4, 2, 100 * 1048576L, 1, 2, 3),
				new(1000, 40, 25, 6, 3, 102 * 1048576L, 2, 2, 3),
				new(2000, 30, 35, 8, 4, 106 * 1048576L, 3, 3, 3)
			};

			var summary = PerformanceBenchmarkSummary.Create(samples);

			Assert.That(summary.FpsP5, Is.EqualTo(31d).Within(0.001));
			Assert.That(summary.FpsP50, Is.EqualTo(40d).Within(0.001));
			Assert.That(summary.FrameTimeP50Milliseconds, Is.EqualTo(25d).Within(0.001));
			Assert.That(summary.FrameTimeP95Milliseconds, Is.EqualTo(34d).Within(0.001));
			Assert.That(summary.WorldTickP95Milliseconds, Is.EqualTo(7.8d).Within(0.001));
			Assert.That(summary.AllocationsMegabytesPerSecond, Is.EqualTo(3d).Within(0.001));
			Assert.That(summary.Generation0Collections, Is.EqualTo(2));
			Assert.That(summary.Generation1Collections, Is.EqualTo(1));
			Assert.That(summary.Generation2Collections, Is.EqualTo(0));
		}

		[Test]
		public void EmptySummaryIsFiniteAndZero()
		{
			var summary = PerformanceBenchmarkSummary.Create(System.Array.Empty<PerformanceBenchmarkSample>());

			Assert.That(summary.FpsP5, Is.Zero);
			Assert.That(summary.FrameTimeP95Milliseconds, Is.Zero);
			Assert.That(summary.AllocationsMegabytesPerSecond, Is.Zero);
			Assert.That(summary.Generation0Collections, Is.Zero);
		}

		[Test]
		public void SummarizeIncludesCpuMemoryAndThermalEvidence()
		{
			var samples = new List<PerformanceBenchmarkSample>
			{
				new(0, 60, 16, 2, 1, 0, 0, 0, 0,
					cpuPercent: 10, residentMemoryBytes: 100, managedMemoryBytes: 40, thermalState: "Nominal"),
				new(1000, 55, 18, 3, 2, 0, 0, 0, 0,
					cpuPercent: 30, residentMemoryBytes: 150, managedMemoryBytes: 60, thermalState: "Fair"),
				new(2000, 50, 20, 4, 3, 0, 0, 0, 0,
					cpuPercent: null, residentMemoryBytes: 120, managedMemoryBytes: 50, thermalState: "Nominal")
			};

			var summary = PerformanceBenchmarkSummary.Create(samples);

			Assert.That(summary.CpuP50Percent, Is.EqualTo(20).Within(0.001));
			Assert.That(summary.CpuP95Percent, Is.EqualTo(29).Within(0.001));
			Assert.That(summary.PeakResidentMemoryBytes, Is.EqualTo(150));
			Assert.That(summary.PeakManagedMemoryBytes, Is.EqualTo(60));
			Assert.That(summary.ThermalStates, Is.EqualTo(new[] { "Fair", "Nominal" }));
		}

		[Test]
		public void BenchmarkWindowSeparatesWarmupSamplingAndCompletion()
		{
			var window = new PerformanceBenchmarkWindow(5000, 30000);

			Assert.That(window.PhaseAt(4999), Is.EqualTo(PerformanceBenchmarkPhase.Warmup));
			Assert.That(window.PhaseAt(5000), Is.EqualTo(PerformanceBenchmarkPhase.Sampling));
			Assert.That(window.PhaseAt(34999), Is.EqualTo(PerformanceBenchmarkPhase.Sampling));
			Assert.That(window.PhaseAt(35000), Is.EqualTo(PerformanceBenchmarkPhase.Complete));
		}

		[Test]
		public void ExplicitBenchmarkGateIgnoresTransitionalWorldsUntilScenarioIsReady()
		{
			var automatic = new PerformanceBenchmarkGate(false);
			automatic.BeginAutomatic();
			Assert.That(automatic.Ready, Is.True);

			var explicitGate = new PerformanceBenchmarkGate(true);
			explicitGate.BeginAutomatic();
			Assert.That(explicitGate.Ready, Is.False);
			explicitGate.Begin();
			Assert.That(explicitGate.Ready, Is.True);
		}
	}
}
