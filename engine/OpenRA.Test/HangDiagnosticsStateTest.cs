#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class HangDiagnosticsStateTest
	{
		[Test]
		public void ReportsAfterHeartbeatStopsForThreshold()
		{
			var state = new HangDiagnosticsState(4);
			state.Record("logic.world.enter", 1000, 20, 30, 10, "Connected");

			Assert.That(state.TryCreateReport(8999, 8000, 30000, out _), Is.False);
			Assert.That(state.TryCreateReport(9000, 8000, 30000, out var report), Is.True);
			Assert.That(report.Stage, Is.EqualTo("logic.world.enter"));
			Assert.That(report.StaleMilliseconds, Is.EqualTo(8000));
		}

		[Test]
		public void PausedStateNeverReportsAndResumeResetsHeartbeat()
		{
			var state = new HangDiagnosticsState(4);
			state.Record("render.present.enter", 1000, 1, 2, 3, "Connected");
			state.Pause();

			Assert.That(state.TryCreateReport(20000, 8000, 30000, out _), Is.False);

			state.Resume(20000);
			Assert.That(state.TryCreateReport(27999, 8000, 30000, out _), Is.False);
		}

		[Test]
		public void PauseRemainsStickyUntilExplicitResume()
		{
			var state = new HangDiagnosticsState(4);
			state.Record("render.present.exit", 1000, 10, 20, 5, "Connected", true, "advanced");
			state.Pause();
			state.Record("lifecycle.trailing-callback", 1100, 10, 21, 5, "Connected", true, "advanced");

			Assert.That(state.TryCreateReport(20000, 8000, 30000, out _), Is.False);
			state.Resume(20000);
			Assert.That(state.TryCreateReport(27999, 8000, 30000, out _), Is.False);
		}

		[Test]
		public void RepeatedReportsAreThrottled()
		{
			var state = new HangDiagnosticsState(4);
			state.Record("render.world.enter", 0, 1, 2, 3, "Connected");

			Assert.That(state.TryCreateReport(8000, 8000, 30000, out _), Is.True);
			Assert.That(state.TryCreateReport(37999, 8000, 30000, out _), Is.False);
			Assert.That(state.TryCreateReport(38000, 8000, 30000, out _), Is.True);
		}

		[Test]
		public void SnapshotKeepsNewestBreadcrumbsInOrder()
		{
			var state = new HangDiagnosticsState(3);
			state.Record("one", 1, 0, 0, 0, "Connected");
			state.Record("two", 2, 0, 0, 0, "Connected");
			state.Record("three", 3, 0, 0, 0, "Connected");
			state.Record("four", 4, 0, 0, 0, "Connected");

			var snapshot = state.Snapshot(5);
			Assert.That(snapshot.Breadcrumbs, Is.EqualTo(new[] { "2 two", "3 three", "4 four" }));
		}

		[Test]
		public void ReportsWhenGameplayLogicStopsButRenderingContinues()
		{
			var state = new HangDiagnosticsState(4);
			state.Record("logic.world.exit", 1000, 20, 30, 10, "Connected", true, "advanced");
			state.Record("render.present.exit", 8999, 20, 300, 10, "Connected", true, "waiting-orders");

			Assert.That(state.TryCreateReport(8999, 8000, 30000, out _), Is.False);
			state.Record("render.present.exit", 9000, 20, 301, 10, "Connected", true, "waiting-orders");
			Assert.That(state.TryCreateReport(9000, 8000, 30000, out var report), Is.True);
			Assert.That(report.StallReason, Is.EqualTo("logic-progress"));
			Assert.That(report.LogicStaleMilliseconds, Is.EqualTo(8000));
			Assert.That(report.TickStatus, Is.EqualTo("waiting-orders"));
		}

		[Test]
		public void AdvancingLogicOrLeavingGameplayResetsLogicStallDetection()
		{
			var state = new HangDiagnosticsState(4);
			state.Record("logic.world.exit", 1000, 20, 30, 10, "Connected", true, "advanced");
			state.Record("logic.world.exit", 8500, 21, 300, 11, "Connected", true, "advanced");
			Assert.That(state.TryCreateReport(9000, 8000, 30000, out _), Is.False);

			state.Record("menu", 10000, 21, 301, 11, "Connected", false, "inactive");
			state.Record("menu", 30000, 21, 302, 11, "Connected", false, "inactive");
			Assert.That(state.TryCreateReport(30000, 8000, 30000, out _), Is.False);
		}

		[Test]
		public void LoadingScopesReceiveASeparateStaleBudget()
		{
			Assert.That(HangDiagnostics.SelectStaleThreshold(hasLoadingScope: false), Is.EqualTo(8000));
			Assert.That(HangDiagnostics.SelectStaleThreshold(hasLoadingScope: true), Is.EqualTo(30000));
		}

		[Test]
		public void WatchdogQueriesLoadingScopeWithoutBuildingAFullTraceSnapshot()
		{
			var directory = new System.IO.DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !System.IO.Directory.Exists(
				System.IO.Path.Combine(directory.FullName, "engine")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			var source = System.IO.File.ReadAllText(System.IO.Path.Combine(
				directory!.FullName, "engine", "OpenRA.Game", "Support", "HangDiagnostics.cs"));
			var watchStart = source.IndexOf("static void Watch(object value)", System.StringComparison.Ordinal);
			var watchEnd = source.IndexOf("static void WriteReport", watchStart, System.StringComparison.Ordinal);
			Assert.That(watchStart, Is.GreaterThanOrEqualTo(0));
			Assert.That(watchEnd, Is.GreaterThan(watchStart));

			var watch = source.Substring(watchStart, watchEnd - watchStart);
			StringAssert.Contains("DiagnosticTrace.TryIsScopeActive", watch);
			StringAssert.DoesNotContain("DiagnosticTrace.Snapshot()", watch,
				"The one-second watchdog loop must not copy the complete trace ring buffer.");
			StringAssert.Contains("!DiagnosticTrace.TryIsScopeActive", watch,
				"Lock contention must conservatively use the longer loading threshold.");
		}
	}
}
