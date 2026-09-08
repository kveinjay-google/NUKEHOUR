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
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Support
{
	public enum PerformanceBenchmarkPhase { Warmup, Sampling, Complete }

	public sealed class PerformanceBenchmarkGate
	{
		readonly bool requiresExplicitStart;

		public bool Ready { get; private set; }

		public PerformanceBenchmarkGate(bool requiresExplicitStart)
		{
			this.requiresExplicitStart = requiresExplicitStart;
		}

		public void BeginAutomatic()
		{
			if (!requiresExplicitStart)
				Ready = true;
		}

		public void Begin() => Ready = true;
	}

	public readonly struct PerformanceBenchmarkWindow
	{
		readonly long warmupMilliseconds;
		readonly long completedMilliseconds;

		public PerformanceBenchmarkWindow(long warmupMilliseconds, long samplingMilliseconds)
		{
			this.warmupMilliseconds = Math.Max(0, warmupMilliseconds);
			completedMilliseconds = this.warmupMilliseconds + Math.Max(1, samplingMilliseconds);
		}

		public PerformanceBenchmarkPhase PhaseAt(long elapsedMilliseconds)
		{
			if (elapsedMilliseconds < warmupMilliseconds)
				return PerformanceBenchmarkPhase.Warmup;

			return elapsedMilliseconds < completedMilliseconds ?
				PerformanceBenchmarkPhase.Sampling : PerformanceBenchmarkPhase.Complete;
		}
	}

	public readonly struct PerformanceBenchmarkSample
	{
		public long ElapsedMilliseconds { get; }
		public double FramesPerSecond { get; }
		public double FrameTimeMilliseconds { get; }
		public double WorldTickMilliseconds { get; }
		public double ActorTickMilliseconds { get; }
		public double TraitTickMilliseconds { get; }
		public double RenderPrepareMilliseconds { get; }
		public double PresentMilliseconds { get; }
		public long TotalAllocatedBytes { get; }
		public int Generation0Collections { get; }
		public int Generation1Collections { get; }
		public int Generation2Collections { get; }
		public double? CpuPercent { get; }
		public long? ResidentMemoryBytes { get; }
		public long? ManagedMemoryBytes { get; }
		public string ThermalState { get; }
		public int ActorCount { get; }
		public int EffectCount { get; }
		public double BatchCount { get; }

		public PerformanceBenchmarkSample(long elapsedMilliseconds, double framesPerSecond,
			double frameTimeMilliseconds, double worldTickMilliseconds, double actorTickMilliseconds,
			long totalAllocatedBytes, int generation0Collections, int generation1Collections,
			int generation2Collections, double traitTickMilliseconds = 0,
			double renderPrepareMilliseconds = 0, double presentMilliseconds = 0,
			double? cpuPercent = null, long? residentMemoryBytes = null, long? managedMemoryBytes = null,
			string thermalState = null, int actorCount = 0, int effectCount = 0, double batchCount = 0)
		{
			ElapsedMilliseconds = elapsedMilliseconds;
			FramesPerSecond = framesPerSecond;
			FrameTimeMilliseconds = frameTimeMilliseconds;
			WorldTickMilliseconds = worldTickMilliseconds;
			ActorTickMilliseconds = actorTickMilliseconds;
			TraitTickMilliseconds = traitTickMilliseconds;
			RenderPrepareMilliseconds = renderPrepareMilliseconds;
			PresentMilliseconds = presentMilliseconds;
			TotalAllocatedBytes = totalAllocatedBytes;
			Generation0Collections = generation0Collections;
			Generation1Collections = generation1Collections;
			Generation2Collections = generation2Collections;
			CpuPercent = cpuPercent;
			ResidentMemoryBytes = residentMemoryBytes;
			ManagedMemoryBytes = managedMemoryBytes;
			ThermalState = thermalState;
			ActorCount = actorCount;
			EffectCount = effectCount;
			BatchCount = batchCount;
		}
	}

	public sealed class PerformanceBenchmarkSummary
	{
		public double FpsP5 { get; private init; }
		public double FpsP50 { get; private init; }
		public double FrameTimeP50Milliseconds { get; private init; }
		public double FrameTimeP95Milliseconds { get; private init; }
		public double WorldTickP95Milliseconds { get; private init; }
		public double ActorTickP95Milliseconds { get; private init; }
		public double TraitTickP95Milliseconds { get; private init; }
		public double RenderPrepareP95Milliseconds { get; private init; }
		public double PresentP95Milliseconds { get; private init; }
		public double AllocationsMegabytesPerSecond { get; private init; }
		public int Generation0Collections { get; private init; }
		public int Generation1Collections { get; private init; }
		public int Generation2Collections { get; private init; }
		public double CpuP50Percent { get; private init; }
		public double CpuP95Percent { get; private init; }
		public long PeakResidentMemoryBytes { get; private init; }
		public long PeakManagedMemoryBytes { get; private init; }
		public IReadOnlyList<string> ThermalStates { get; private init; } = Array.Empty<string>();

		public static PerformanceBenchmarkSummary Create(IReadOnlyList<PerformanceBenchmarkSample> samples)
		{
			if (samples == null || samples.Count == 0)
				return new PerformanceBenchmarkSummary();

			var first = samples[0];
			var last = samples[samples.Count - 1];
			var elapsedSeconds = Math.Max(0, last.ElapsedMilliseconds - first.ElapsedMilliseconds) / 1000d;
			var allocatedBytes = Math.Max(0, last.TotalAllocatedBytes - first.TotalAllocatedBytes);

			return new PerformanceBenchmarkSummary
			{
				FpsP5 = Percentile(samples.Select(s => s.FramesPerSecond), 0.05),
				FpsP50 = Percentile(samples.Select(s => s.FramesPerSecond), 0.50),
				FrameTimeP50Milliseconds = Percentile(samples.Select(s => s.FrameTimeMilliseconds), 0.50),
				FrameTimeP95Milliseconds = Percentile(samples.Select(s => s.FrameTimeMilliseconds), 0.95),
				WorldTickP95Milliseconds = Percentile(samples.Select(s => s.WorldTickMilliseconds), 0.95),
				ActorTickP95Milliseconds = Percentile(samples.Select(s => s.ActorTickMilliseconds), 0.95),
				TraitTickP95Milliseconds = Percentile(samples.Select(s => s.TraitTickMilliseconds), 0.95),
				RenderPrepareP95Milliseconds = Percentile(samples.Select(s => s.RenderPrepareMilliseconds), 0.95),
				PresentP95Milliseconds = Percentile(samples.Select(s => s.PresentMilliseconds), 0.95),
				AllocationsMegabytesPerSecond = elapsedSeconds <= 0 ? 0 : allocatedBytes / 1048576d / elapsedSeconds,
				Generation0Collections = Math.Max(0, last.Generation0Collections - first.Generation0Collections),
				Generation1Collections = Math.Max(0, last.Generation1Collections - first.Generation1Collections),
				Generation2Collections = Math.Max(0, last.Generation2Collections - first.Generation2Collections),
				CpuP50Percent = Percentile(samples.Where(s => s.CpuPercent.HasValue).Select(s => s.CpuPercent.Value), 0.50),
				CpuP95Percent = Percentile(samples.Where(s => s.CpuPercent.HasValue).Select(s => s.CpuPercent.Value), 0.95),
				PeakResidentMemoryBytes = samples.Max(s => s.ResidentMemoryBytes ?? 0),
				PeakManagedMemoryBytes = samples.Max(s => s.ManagedMemoryBytes ?? 0),
				ThermalStates = samples.Select(s => s.ThermalState)
					.Where(s => !string.IsNullOrEmpty(s))
					.Distinct(StringComparer.Ordinal)
					.OrderBy(s => s, StringComparer.Ordinal)
					.ToArray()
			};
		}

		public static double Percentile(IEnumerable<double> values, double percentile)
		{
			if (values == null)
				return 0;

			var sorted = values.OrderBy(v => v).ToArray();
			if (sorted.Length == 0)
				return 0;

			var position = Math.Clamp(percentile, 0, 1) * (sorted.Length - 1);
			var lower = (int)Math.Floor(position);
			var upper = (int)Math.Ceiling(position);
			if (lower == upper)
				return sorted[lower];

			return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
		}
	}
}
