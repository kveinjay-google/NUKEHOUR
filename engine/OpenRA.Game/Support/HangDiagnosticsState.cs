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

namespace OpenRA
{
	public sealed class HangDiagnosticsSnapshot
	{
		public string Stage { get; init; }
		public string StallReason { get; init; }
		public long StaleMilliseconds { get; init; }
		public long HeartbeatStaleMilliseconds { get; init; }
		public long LogicStaleMilliseconds { get; init; }
		public int LogicFrame { get; init; }
		public int RenderFrame { get; init; }
		public int NetworkFrame { get; init; }
		public string ConnectionState { get; init; }
		public string TickStatus { get; init; }
		public int MissingOrderCount { get; init; }
		public int FirstMissingClient { get; init; }
		public string[] Breadcrumbs { get; init; }
	}

	public sealed class HangDiagnosticsState
	{
		readonly object sync = new();
		readonly Breadcrumb[] breadcrumbs;
		readonly int capacity;
		int breadcrumbStart;
		int breadcrumbCount;

		string stage = "not-started";
		string connectionState = "unknown";
		string tickStatus = "inactive";
		int missingOrderCount;
		int firstMissingClient = -1;
		long lastHeartbeatMilliseconds;
		long lastLogicProgressMilliseconds;
		long lastReportMilliseconds = long.MinValue;
		int logicFrame;
		int renderFrame;
		int networkFrame;
		bool logicProgressExpected;
		bool paused;

		public HangDiagnosticsState(int capacity)
		{
			if (capacity < 1)
				throw new ArgumentOutOfRangeException(nameof(capacity));

			this.capacity = capacity;
			breadcrumbs = new Breadcrumb[capacity];
		}

		public void Record(string newStage, long nowMilliseconds, int newLogicFrame,
			int newRenderFrame, int newNetworkFrame, string newConnectionState,
			bool newLogicProgressExpected = false, string newTickStatus = "inactive",
			int newMissingOrderCount = 0, int newFirstMissingClient = -1)
		{
			lock (sync)
			{
				if (!newLogicProgressExpected || !logicProgressExpected || newLogicFrame != logicFrame)
					lastLogicProgressMilliseconds = nowMilliseconds;

				stage = newStage;
				lastHeartbeatMilliseconds = nowMilliseconds;
				logicFrame = newLogicFrame;
				renderFrame = newRenderFrame;
				networkFrame = newNetworkFrame;
				connectionState = newConnectionState;
				tickStatus = newTickStatus;
				missingOrderCount = newMissingOrderCount;
				firstMissingClient = newFirstMissingClient;
				logicProgressExpected = newLogicProgressExpected;

				var index = (breadcrumbStart + breadcrumbCount) % capacity;
				breadcrumbs[index] = new Breadcrumb(nowMilliseconds, newStage);
				if (breadcrumbCount < capacity)
					breadcrumbCount++;
				else
					breadcrumbStart = (breadcrumbStart + 1) % capacity;
			}
		}

		public void Pause()
		{
			lock (sync)
				paused = true;
		}

		public void Resume(long nowMilliseconds)
		{
			lock (sync)
			{
				paused = false;
				lastHeartbeatMilliseconds = nowMilliseconds;
				lastLogicProgressMilliseconds = nowMilliseconds;
			}
		}

		public bool TryCreateReport(long nowMilliseconds, long staleThresholdMilliseconds,
			long repeatIntervalMilliseconds, out HangDiagnosticsSnapshot snapshot)
		{
			lock (sync)
			{
				var heartbeatStale = nowMilliseconds - lastHeartbeatMilliseconds;
				var logicStale = nowMilliseconds - lastLogicProgressMilliseconds;
				var heartbeatHung = heartbeatStale >= staleThresholdMilliseconds;
				var logicHung = logicProgressExpected && logicStale >= staleThresholdMilliseconds;
				if (paused || (!heartbeatHung && !logicHung) ||
					(lastReportMilliseconds != long.MinValue && nowMilliseconds - lastReportMilliseconds < repeatIntervalMilliseconds))
				{
					snapshot = null;
					return false;
				}

				lastReportMilliseconds = nowMilliseconds;
				var reason = heartbeatHung ? "main-heartbeat" : "logic-progress";
				var stale = heartbeatHung ? heartbeatStale : logicStale;
				snapshot = CreateSnapshot(stale, reason, heartbeatStale, logicStale);
				return true;
			}
		}

		public HangDiagnosticsSnapshot Snapshot(long nowMilliseconds)
		{
			lock (sync)
				return CreateSnapshot(
					nowMilliseconds - lastHeartbeatMilliseconds,
					"snapshot",
					nowMilliseconds - lastHeartbeatMilliseconds,
					nowMilliseconds - lastLogicProgressMilliseconds);
		}

		HangDiagnosticsSnapshot CreateSnapshot(long staleMilliseconds, string stallReason,
			long heartbeatStaleMilliseconds, long logicStaleMilliseconds)
		{
			return new HangDiagnosticsSnapshot
			{
				Stage = stage,
				StallReason = stallReason,
				StaleMilliseconds = staleMilliseconds,
				HeartbeatStaleMilliseconds = heartbeatStaleMilliseconds,
				LogicStaleMilliseconds = logicStaleMilliseconds,
				LogicFrame = logicFrame,
				RenderFrame = renderFrame,
				NetworkFrame = networkFrame,
				ConnectionState = connectionState,
				TickStatus = tickStatus,
				MissingOrderCount = missingOrderCount,
				FirstMissingClient = firstMissingClient,
				Breadcrumbs = SnapshotBreadcrumbs()
			};
		}

		string[] SnapshotBreadcrumbs()
		{
			var result = new string[breadcrumbCount];
			for (var i = 0; i < breadcrumbCount; i++)
			{
				var breadcrumb = breadcrumbs[(breadcrumbStart + i) % capacity];
				result[i] = $"{breadcrumb.TimeMilliseconds} {breadcrumb.Stage}";
			}

			return result;
		}

		readonly struct Breadcrumb
		{
			public readonly long TimeMilliseconds;
			public readonly string Stage;

			public Breadcrumb(long timeMilliseconds, string stage)
			{
				TimeMilliseconds = timeMilliseconds;
				Stage = stage;
			}
		}
	}
}
