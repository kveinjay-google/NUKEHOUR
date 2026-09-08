#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace OpenRA.Support
{
	sealed class Benchmark
	{
		readonly string prefix;
		readonly Dictionary<string, List<BenchmarkPoint>> samples = new();
		readonly List<PerformanceBenchmarkSample> performanceSamples = new(800);
		readonly Queue<(int Frame, long Milliseconds)> frameTimings = new();
		readonly RuntimeResourceSampler resourceSampler = new();
		readonly Stopwatch clock = Stopwatch.StartNew();
		readonly PerformanceBenchmarkWindow window;
		readonly PerformanceBenchmarkGate gate;
		bool samplingStarted;
		bool completed;
		public bool Ready => gate.Ready;

		public Benchmark(string prefix)
		{
			this.prefix = prefix;
			window = new PerformanceBenchmarkWindow(
				ReadSeconds("OPENRA_BENCHMARK_WARMUP_SECONDS", 5) * 1000L,
				ReadSeconds("OPENRA_BENCHMARK_DURATION_SECONDS", 30) * 1000L);
			gate = new PerformanceBenchmarkGate(
				!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENRA_IOS_PERF_COUNT")));
			PerformanceBenchmarkPolicy.IsActive = true;
			PerformanceCategoryProfiler.Global.Enabled = true;
		}

		public void Tick(int localTick)
		{
			if (!gate.Ready)
				return;

			var elapsed = clock.ElapsedMilliseconds;
			var phase = window.PhaseAt(elapsed);
			if (phase == PerformanceBenchmarkPhase.Warmup)
				return;

			if (!samplingStarted)
			{
				samplingStarted = true;
				PerfHistory.Reset();
				PerformanceCategoryProfiler.Global.Reset();
			}

			if (phase == PerformanceBenchmarkPhase.Complete)
			{
				Write();
				Game.Exit();
				return;
			}

			foreach (var item in PerfHistory.Items)
				samples.GetOrAdd(item.Key).Add(new BenchmarkPoint(localTick, item.Value.LastValue));

			var resources = resourceSampler.Sample();
			var world = Game.OrderManager?.World;
			var fps = FramesPerSecond(elapsed);
			performanceSamples.Add(new PerformanceBenchmarkSample(
				elapsed,
				fps,
				Value("render"),
				Value("world_tick"),
				Value("tick_actors"),
				resources.TotalAllocatedBytes ?? 0,
				resources.Generation0Collections ?? 0,
				resources.Generation1Collections ?? 0,
				resources.Generation2Collections ?? 0,
				Value("tick_traits"),
				Value("render_prepare"),
				Value("render_flip"),
				resources.CpuPercent,
				resources.ResidentMemoryBytes,
				resources.ManagedMemoryBytes,
				resources.ThermalState,
				world?.Actors.Count() ?? 0,
				world?.Effects.Count() ?? 0,
				Value("batches")));
		}

		double FramesPerSecond(long elapsed)
		{
			frameTimings.Enqueue((Game.RenderFrame, elapsed));
			while (frameTimings.Count > 2 && elapsed - frameTimings.Peek().Milliseconds > 1000)
				frameTimings.Dequeue();

			if (frameTimings.Count < 2)
				return 0;

			var oldest = frameTimings.Peek();
			var duration = elapsed - oldest.Milliseconds;
			return duration <= 0 ? 0 : (Game.RenderFrame - oldest.Frame) * 1000d / duration;
		}

		static double Value(string name) =>
			PerfHistory.Items.TryGetValue(name, out var item) ? item.LastValue : 0;

		sealed class BenchmarkPoint
		{
			public int Tick { get; }
			public double Value { get; }

			public BenchmarkPoint(int tick, double value)
			{
				Tick = tick;
				Value = value;
			}
		}

		public void Write()
		{
			if (completed)
				return;

			completed = true;
			PerformanceCategoryProfiler.Global.Enabled = false;
			PerformanceBenchmarkPolicy.IsActive = false;

			var directory = Path.Combine(Platform.SupportDir, "Logs", "Performance", SafeFilename(prefix));
			Directory.CreateDirectory(directory);
			foreach (var sample in samples)
			{
				var name = sample.Key;
				using var writer = new StreamWriter(Path.Combine(directory, SafeFilename(name) + ".csv"),
					false, new UTF8Encoding(false));
				writer.WriteLine("tick,time [ms]");
				foreach (var point in sample.Value)
					writer.WriteLine($"{point.Tick},{point.Value.ToString(CultureInfo.InvariantCulture)}");
			}

			var report = new
			{
				SchemaVersion = 1,
				CreatedUtc = DateTime.UtcNow,
				Prefix = prefix,
				Build = Game.ModData?.Manifest.Metadata.Version,
				Scenario = ReadScenario(),
				Summary = PerformanceBenchmarkSummary.Create(performanceSamples),
				TopCategories = PerformanceCategoryProfiler.Global.SnapshotTop(20),
				Samples = performanceSamples
			};
			File.WriteAllText(Path.Combine(directory, "summary.json"),
				JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
				new UTF8Encoding(false));
		}

		public void Reset()
		{
			samples.Clear();
			performanceSamples.Clear();
			frameTimings.Clear();
			PerformanceCategoryProfiler.Global.Reset();
			clock.Restart();
			samplingStarted = false;
			completed = false;
		}

		public void BeginAutomatic()
		{
			gate.BeginAutomatic();
			if (gate.Ready)
				Reset();
		}

		public void Begin()
		{
			gate.Begin();
			Reset();
		}

		static int ReadSeconds(string name, int fallback)
		{
			return int.TryParse(Environment.GetEnvironmentVariable(name), out var value) ?
				Math.Clamp(value, 1, 3600) : fallback;
		}

		static Dictionary<string, string> ReadScenario()
		{
			var result = new Dictionary<string, string>();
			foreach (var name in new[]
			{
				"OPENRA_IOS_PERF_COUNT", "OPENRA_IOS_PERF_STATE", "OPENRA_IOS_PERF_CAMERA",
				"OPENRA_IOS_PERF_PAUSED", "OPENRA_IOS_PERF_AI", "OPENRA_IOS_PERF_RENDER_SCALE",
				"OPENRA_IOS_PERF_RUNTIME"
			})
				result[name] = Environment.GetEnvironmentVariable(name) ?? "";

			return result;
		}

		static string SafeFilename(string value)
		{
			var invalid = Path.GetInvalidFileNameChars();
			return new string((value ?? "benchmark").Select(c => invalid.Contains(c) ? '_' : c).ToArray());
		}
	}
}
