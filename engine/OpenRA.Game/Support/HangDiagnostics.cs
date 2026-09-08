#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using OpenRA.Network;
using OpenRA.Support;

namespace OpenRA
{
	public static class HangDiagnostics
	{
		const int BreadcrumbCapacity = 64;
		const long StaleThresholdMilliseconds = 8000;
		const long LoadingStaleThresholdMilliseconds = 30000;
		const long RepeatIntervalMilliseconds = 30000;

		static readonly Stopwatch Clock = Stopwatch.StartNew();
		static readonly HangDiagnosticsState State = new(BreadcrumbCapacity);
		static readonly object lifecycleSync = new();

		static Thread watchdogThread;
		static CancellationTokenSource cancellation;
		static string supportDirectory;
		static string reportPath;

		public static void Start(string supportDirectoryOverride = null)
		{
			if (!Platform.IsIOS)
				return;

			lock (lifecycleSync)
			{
				if (watchdogThread != null)
					return;

				try
				{
					supportDirectory = string.IsNullOrWhiteSpace(supportDirectoryOverride) ?
						Platform.SupportDir : supportDirectoryOverride;
					var logDirectory = Path.Combine(supportDirectory, "Logs");
					Directory.CreateDirectory(logDirectory);
					reportPath = Path.Combine(logDirectory, "hang-diagnostic.log");
					if (File.Exists(reportPath) && new FileInfo(reportPath).Length > 0)
					{
						var archive = Path.Combine(logDirectory, $"hang-diagnostic-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
						File.Move(reportPath, archive);
					}

					File.WriteAllText(reportPath,
						$"OpenRA iOS hang diagnostics started {DateTime.UtcNow:u}{Environment.NewLine}", Encoding.UTF8);
				}
				catch (Exception e)
				{
					Console.WriteLine($"Failed to initialize iOS hang diagnostics: {e}");
					supportDirectory = null;
					reportPath = null;
					return;
				}

				cancellation = new CancellationTokenSource();
				State.Pause();
				watchdogThread = new Thread(Watch)
				{
					Name = "OpenRA iOS Hang Watchdog",
					IsBackground = true
				};
				watchdogThread.Start(cancellation.Token);
			}
		}

		public static void Stop()
		{
			lock (lifecycleSync)
			{
				if (watchdogThread == null)
					return;

				cancellation.Cancel();
				watchdogThread.Join(2000);
				cancellation.Dispose();
				cancellation = null;
				watchdogThread = null;
			}
		}

		public static void Pause() => State.Pause();
		public static void Resume() => State.Resume(Clock.ElapsedMilliseconds);
		public static void SaveSnapshot(string reason = "manual")
		{
			if (!Platform.IsIOS)
				return;

			ThreadPool.QueueUserWorkItem(_ => WriteReport(State.Snapshot(Clock.ElapsedMilliseconds), reason));
		}

		public static void Record(string stage)
		{
			if (!Platform.IsIOS || !PerformanceBenchmarkPolicy.RecordFrameDiagnostics)
				return;

			var orderManager = Game.OrderManager;
			var connectionState = orderManager?.Connection is NetworkConnection connection ?
				connection.ConnectionState.ToString() : orderManager?.Connection?.GetType().Name ?? "none";
			var world = orderManager?.World;
			var expectsLogicProgress = orderManager?.GameStarted == true && world != null &&
				world.Type == WorldType.Regular && !world.Paused && !world.IsLoadingGameSave &&
				(!world.IsReplay || world.ReplayTimestep != 0);
			var tickStatus = orderManager?.DiagnosticTickStatus ?? "inactive";
			State.Record(stage, Clock.ElapsedMilliseconds, orderManager?.LocalFrameNumber ?? -1,
				Game.RenderFrame, orderManager?.NetFrameNumber ?? -1, connectionState,
				expectsLogicProgress, tickStatus, orderManager?.DiagnosticMissingOrderCount ?? 0,
				orderManager?.DiagnosticFirstMissingClient ?? -1);
		}

		public static long SelectStaleThreshold(bool hasLoadingScope) =>
			hasLoadingScope ? LoadingStaleThresholdMilliseconds : StaleThresholdMilliseconds;

		static void Watch(object value)
		{
			var token = (CancellationToken)value;
			while (!token.WaitHandle.WaitOne(1000))
			{
				var hasLoadingScope = !DiagnosticTrace.TryIsScopeActive("Game.StartGame", out var scopeActive) ||
					scopeActive;
				if (!State.TryCreateReport(Clock.ElapsedMilliseconds, SelectStaleThreshold(hasLoadingScope),
					RepeatIntervalMilliseconds, out var snapshot))
					continue;

				try
				{
					WriteReport(snapshot, "watchdog-hang");
				}
				catch (Exception e)
				{
					Console.WriteLine($"Failed to write iOS hang diagnostics: {e}");
				}
			}
		}

		static void WriteReport(HangDiagnosticsSnapshot snapshot, string reason)
		{
			var trace = DiagnosticTrace.Snapshot();
			var reportId = $"hang-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}";
			if (string.IsNullOrEmpty(supportDirectory) || string.IsNullOrEmpty(reportPath))
				return;

			var pending = Path.Combine(supportDirectory, "Diagnostics", "Pending", reportId + ".oradiag");
			Directory.CreateDirectory(pending);
			var summary = new
			{
				ReportId = reportId,
				CreatedUtc = DateTime.UtcNow,
				Reason = reason,
				LegacyStage = snapshot.Stage,
				snapshot.StallReason,
				snapshot.StaleMilliseconds,
				snapshot.HeartbeatStaleMilliseconds,
				snapshot.LogicStaleMilliseconds,
				snapshot.LogicFrame,
				snapshot.RenderFrame,
				snapshot.NetworkFrame,
				snapshot.ConnectionState,
				snapshot.TickStatus,
				snapshot.MissingOrderCount,
				snapshot.FirstMissingClient,
				ManagedMemoryBytes = GC.GetTotalMemory(false),
				WorkingSetBytes = Environment.WorkingSet,
				TraceAvailable = trace.Available,
				trace.DroppedEvents,
				ActiveScopes = trace.ActiveScopes.Select(s => new { s.ThreadId, s.SpanId, s.StartedMilliseconds, s.Path }),
				Heartbeats = trace.Heartbeats.ToDictionary(h => h.Key.ToString(), h => new
				{
					h.Value.TimestampMilliseconds,
					h.Value.Name,
					h.Value.Arg0,
					h.Value.Arg1
				})
			};
			File.WriteAllText(Path.Combine(pending, "summary.json"),
				JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
			using (var timeline = new StreamWriter(Path.Combine(pending, "timeline.txt"), false, new UTF8Encoding(false)))
				foreach (var e in trace.Events)
					timeline.WriteLine($"{e.Sequence} {e.TimestampMilliseconds} T{e.ThreadId} {e.Subsystem} {e.Phase} span={e.SpanId} parent={e.ParentSpanId} {e.Name} {e.Arg0}/{e.Arg1}");

			using var stream = new FileStream(reportPath, FileMode.Append, FileAccess.Write, FileShare.Read,
				4096, FileOptions.WriteThrough);
			using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
			writer.WriteLine();
			writer.WriteLine($"HANG DETECTED {DateTime.UtcNow:u}");
			writer.WriteLine($"Stage: {snapshot.Stage}");
			writer.WriteLine($"StallReason: {snapshot.StallReason}");
			writer.WriteLine($"StaleMilliseconds: {snapshot.StaleMilliseconds}");
			writer.WriteLine($"HeartbeatStaleMilliseconds: {snapshot.HeartbeatStaleMilliseconds}");
			writer.WriteLine($"LogicStaleMilliseconds: {snapshot.LogicStaleMilliseconds}");
			writer.WriteLine($"LogicFrame: {snapshot.LogicFrame}");
			writer.WriteLine($"RenderFrame: {snapshot.RenderFrame}");
			writer.WriteLine($"NetworkFrame: {snapshot.NetworkFrame}");
			writer.WriteLine($"ConnectionState: {snapshot.ConnectionState}");
			writer.WriteLine($"TickStatus: {snapshot.TickStatus}");
			writer.WriteLine($"MissingOrderCount: {snapshot.MissingOrderCount}");
			writer.WriteLine($"FirstMissingClient: {snapshot.FirstMissingClient}");
			writer.WriteLine($"ManagedMemoryBytes: {GC.GetTotalMemory(false)}");
			writer.WriteLine($"WorkingSetBytes: {Environment.WorkingSet}");
			writer.WriteLine($"TraceAvailable: {trace.Available}");
			writer.WriteLine($"TraceDroppedEvents: {trace.DroppedEvents}");
			writer.WriteLine("Recent breadcrumbs:");
			foreach (var breadcrumb in snapshot.Breadcrumbs)
				writer.WriteLine(breadcrumb);
			writer.WriteLine("Active diagnostic scopes:");
			foreach (var scope in trace.ActiveScopes)
				writer.WriteLine($"Thread {scope.ThreadId}: {scope.Path}");

			writer.Flush();
			stream.Flush(true);
		}
	}
}
