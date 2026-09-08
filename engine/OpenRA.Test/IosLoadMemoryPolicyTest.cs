using System;
using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosLoadMemoryPolicyTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !File.Exists(Path.Combine(
				directory.FullName, "engine", "OpenRA.Game", "Game.cs")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null);
			return directory!.FullName;
		}

		static string GameMethod(string startMarker, string endMarker)
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Game", "Game.cs"));
			var start = source.IndexOf(startMarker, StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing method marker: {startMarker}");
			var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
			Assert.That(end, Is.GreaterThan(start), $"Missing method end marker: {endMarker}");
			return source[start..end];
		}

		static int CountOccurrences(string source, string value)
		{
			var count = 0;
			var index = 0;
			while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
			{
				count++;
				index += value.Length;
			}

			return count;
		}

		[Test]
		public void IOSDoesNotForceBlockingCollectionsDuringWorldLoading()
		{
			Assert.That(LoadMemoryPolicy.ShouldForceCollection(isIOS: true), Is.False);
		}

		[Test]
		public void DesktopKeepsExistingLoadCollectionPolicy()
		{
			Assert.That(LoadMemoryPolicy.ShouldForceCollection(isIOS: false), Is.True);
		}

		[Test]
		public void FatalErrorReportingDoesNotForceAnIOSCollection()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Support", "ExceptionHandler.cs"));
			StringAssert.Contains("LoadMemoryPolicy.ShouldForceCollection(OperatingSystem.IsIOS())", source,
				"The fatal-report path must not reintroduce the iOS audio/GC deadlock while handling OOM.");
		}

		[Test]
		public void StartGameRoutesEveryForcedCollectionThroughTheIOSLoadPolicy()
		{
			var startGame = GameMethod("internal static void StartGame(", "public static void RestartGame()");
			var collectionCount = CountOccurrences(startGame, "GC.Collect();");
			var policyGuardCount = CountOccurrences(startGame,
				"if (LoadMemoryPolicy.ShouldForceCollection(OperatingSystem.IsIOS()))");

			Assert.That(collectionCount, Is.EqualTo(3), "The known loading path has three intentional collection points.");
			Assert.That(policyGuardCount, Is.EqualTo(collectionCount),
				"Every forced loading collection must be suppressed on iOS through LoadMemoryPolicy.");
		}

		[Test]
		public void InitializeAndRunRoutesItsStartupCollectionThroughTheIOSLoadPolicy()
		{
			var initializeAndRun = GameMethod("public static RunStatus InitializeAndRun(", "static void Initialize(");
			var collectionCount = CountOccurrences(initializeAndRun, "GC.Collect();");
			var policyGuardCount = CountOccurrences(initializeAndRun,
				"if (LoadMemoryPolicy.ShouldForceCollection(OperatingSystem.IsIOS()))");

			Assert.That(collectionCount, Is.EqualTo(1), "The startup path has one intentional collection point.");
			Assert.That(policyGuardCount, Is.EqualTo(collectionCount),
				"The startup collection runs after sound initialization and must also be suppressed on iOS.");
		}

		[Test]
		public void StartGamePersistsEachMajorIOSLoadingBoundary()
		{
			var startGame = GameMethod("internal static void StartGame(", "public static void RestartGame()");
			var stages = new[]
			{
				"world-created",
				"renderer-created",
				"load-complete",
				"order-manager-started",
				"palette-refreshed",
				"post-load-complete",
				"after-game-start",
			};

			foreach (var stage in stages)
				StringAssert.Contains($"IosLoadStageJournal.Record(\"{stage}\"", startGame,
					$"Missing persistent iOS loading boundary: {stage}");
		}

		[Test]
		public void LoadingSmokeRequiresSustainedPresentedGameplayAndLiveConnection()
		{
			var logicTick = GameMethod("static void InnerLogicTick(", "static void LogicTick()");
			var renderTick = GameMethod("static void RenderTick()", "static void Loop()");
			var presentExit = renderTick.IndexOf("HangDiagnostics.Record(\"render.present.exit\");",
				StringComparison.Ordinal);
			var ready = renderTick.IndexOf("IosSmokeTestJournal.RecordReady(", StringComparison.Ordinal);

			StringAssert.DoesNotContain("IosSmokeTestJournal.RecordReady(", logicTick,
				"Completing one logic tick must not hide a failure in the first rendered frame.");
			Assert.That(presentExit, Is.GreaterThanOrEqualTo(0));
			Assert.That(ready, Is.GreaterThan(presentExit),
				"The smoke test may pass only after the first frame has reached the display.");
			StringAssert.Contains("presentedWorld.WorldTick", renderTick);
			StringAssert.Contains("RenderFrame, RunTime", renderTick,
				"Readiness must observe both world and render progress over monotonic time.");

			var support = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Support", "LoadMemoryPolicy.cs"));
			StringAssert.Contains("readiness.Observe(elapsedMilliseconds, worldTick, renderFrame, connected)", support);
			StringAssert.Contains("criterion=duration-and-progress", support);
			StringAssert.Contains("readyRecorder.TryRecord(() => IosLoadStageJournal.Record", support);
			StringAssert.DoesNotContain("Interlocked.Exchange(ref readyRecorded", support);
			StringAssert.Contains("if (!ReferenceEquals(trackedWorld, world))", support,
				"A replacement world must start a fresh stability window.");
			StringAssert.Contains("world.OrderManager.Connection is not NetworkConnection connection", support);
			StringAssert.Contains("connection.ConnectionState == ConnectionState.Connected", support,
				"A disconnected local server must never be reported as a playable game.");
		}

		[Test]
		public void LoadingSmokeHasASustainedGameplayReadinessTracker()
		{
			var tracker = typeof(IosSmokeTestJournal).Assembly.GetType("OpenRA.IosSmokeReadinessTracker");

			Assert.That(tracker, Is.Not.Null,
				"A first presented frame is not enough evidence that gameplay remains responsive.");
			Assert.That(tracker!.GetMethod("Observe"), Is.Not.Null);
		}

		[Test]
		public void LoadingStageJournalReportsWhetherTheBoundaryReachedDisk()
		{
			var record = typeof(IosLoadStageJournal).GetMethod("Record");

			Assert.That(record, Is.Not.Null);
			Assert.That(record!.ReturnType, Is.EqualTo(typeof(bool)),
				"Smoke readiness must not be committed after a swallowed write failure.");
		}

		[Test]
		public void LoadingSmokeHasARetryableReadyCommitter()
		{
			var recorder = typeof(IosSmokeTestJournal).Assembly.GetType("OpenRA.IosSmokeReadyRecorder");

			Assert.That(recorder, Is.Not.Null);
			Assert.That(recorder!.GetMethod("TryRecord"), Is.Not.Null);
		}

		[Test]
		public void LoadingSmokeRetriesAReadyWriteThenCommitsExactlyOnce()
		{
			var recorder = new IosSmokeReadyRecorder();
			var attempts = 0;

			Assert.That(recorder.TryRecord(() =>
			{
				attempts++;
				return false;
			}), Is.False);
			Assert.That(recorder.TryRecord(() =>
			{
				attempts++;
				return true;
			}), Is.True);
			Assert.That(recorder.TryRecord(() =>
			{
				attempts++;
				return true;
			}), Is.False);
			Assert.That(attempts, Is.EqualTo(2));
		}

		[Test]
		public void LoadingSmokeRequiresTenSecondsAndOneHundredWorldTicksAndPresentedFrames()
		{
			var tracker = new IosSmokeReadinessTracker();

			Assert.That(tracker.Observe(0, 40, 1000, connected: true), Is.False);
			Assert.That(tracker.Observe(1000, 139, 1099, connected: true), Is.False);
			Assert.That(tracker.Observe(2000, 140, 1100, connected: true), Is.False,
				"One hundred fast ticks must not pass before the ten-second stability window.");
			Assert.That(tracker.Observe(10_000, 290, 1600, connected: true), Is.True);
		}

		[Test]
		public void LoadingSmokeTenSecondWindowRequiresTheWorldToStillBeAdvancing()
		{
			var tracker = new IosSmokeReadinessTracker();

			Assert.That(tracker.Observe(1000, 50, 10, connected: true), Is.False);
			Assert.That(tracker.Observe(10_999, 98, 609, connected: true), Is.False);
			Assert.That(tracker.Observe(11_000, 99, 610, connected: true), Is.False,
				"Elapsed time cannot substitute for one hundred world ticks.");

			tracker = new IosSmokeReadinessTracker();
			Assert.That(tracker.Observe(1000, 50, 10, connected: true), Is.False);
			Assert.That(tracker.Observe(9000, 60, 500, connected: true), Is.False);
			Assert.That(tracker.Observe(11_000, 60, 610, connected: true), Is.False,
				"A rendering UI must not hide a stalled world simulation.");
		}

		[Test]
		public void LoadingSmokeRestartsItsStabilityWindowAfterDisconnect()
		{
			var tracker = new IosSmokeReadinessTracker();

			Assert.That(tracker.Observe(0, 0, 0, connected: true), Is.False);
			Assert.That(tracker.Observe(5000, 50, 50, connected: false), Is.False);
			Assert.That(tracker.Observe(10_000, 100, 100, connected: true), Is.False,
				"Readiness must describe one uninterrupted connected interval.");
			Assert.That(tracker.Observe(11_000, 200, 200, connected: true), Is.False);
			Assert.That(tracker.Observe(20_000, 300, 300, connected: true), Is.True);
		}

		[Test]
		public void LoadingSmokeRunnerPrioritizesFailureAndAllowsForTheStabilityWindow()
		{
			var script = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "scripts", "run-loading-smoke.sh"));
			var failureCheck = script.IndexOf(
				"if [ \"$start_seen\" = true ] && rg -F -q \"$failure_needle\"", StringComparison.Ordinal);
			var readyCheck = script.IndexOf(
				"if [ \"$start_seen\" = true ] && rg -F -q \"$ready_needle\"", StringComparison.Ordinal);

			StringAssert.Contains("OPENRA_IOS_SMOKE_TIMEOUT_SECONDS:-120", script);
			StringAssert.Contains("criterion=duration-and-progress", script,
				"The runner must reject first-frame ready records from older app bundles.");
			Assert.That(failureCheck, Is.GreaterThanOrEqualTo(0));
			Assert.That(readyCheck, Is.GreaterThan(failureCheck),
				"A probe containing both records must fail instead of masking the exception as success.");
			StringAssert.Contains("sustained responsive gameplay", script);
			StringAssert.DoesNotContain("first presented gameplay frame", script);
		}

		[Test]
		public void LoadingSmokeRunnerUsesAPerLaunchNonceAndFixedStringBoundaries()
		{
			var script = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "scripts", "run-loading-smoke.sh"));

			StringAssert.Contains("launch_nonce=\"$(uuidgen", script);
			StringAssert.Contains("-$launch_nonce}", script,
				"The default display run id must also be collision resistant.");
			StringAssert.Contains("OPENRA_IOS_SMOKE_LAUNCH_NONCE", script);
			StringAssert.Contains("start_needle=", script);
			StringAssert.Contains("ready_needle=", script);
			StringAssert.Contains("failure_needle=", script);
			StringAssert.Contains("rg -F -q", script,
				"User-supplied run ids must never be interpreted as regular expressions.");
			StringAssert.DoesNotContain("rg -q \"smoke", script);
		}

		[Test]
		public void LoadingSmokeWritesAPerLaunchStartBoundaryAfterInitialization()
		{
			var initializeAndRun = GameMethod("public static RunStatus InitializeAndRun(", "static void Initialize(");
			var initialize = initializeAndRun.IndexOf("Initialize(new Arguments(args));", StringComparison.Ordinal);
			var start = initializeAndRun.IndexOf("IosSmokeTestJournal.RecordStart();", StringComparison.Ordinal);
			var support = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Support", "LoadMemoryPolicy.cs"));

			Assert.That(initialize, Is.GreaterThanOrEqualTo(0));
			Assert.That(start, Is.GreaterThan(initialize));
			StringAssert.Contains("OPENRA_IOS_SMOKE_LAUNCH_NONCE", support);
			StringAssert.Contains("\"smoke-start\"", support);
			StringAssert.Contains("launch={LaunchNonce}", support);
		}

		[Test]
		public void DevelopmentSkirmishTargetBecomesLaunchIntoArgument()
		{
			Assert.That(IosDevelopmentLaunch.BuildSkirmishArgument("map-uid option gamespeed fast"),
				Is.EqualTo("Game.LaunchInto=skirmish map-uid option gamespeed fast"));
			Assert.That(IosDevelopmentLaunch.BuildSkirmishArgument("  "), Is.Null);
		}

		[TestCase(1, -1, true)]
		[TestCase(299, 1, false)]
		[TestCase(300, 1, true)]
		[TestCase(600, 300, true)]
		public void DevelopmentHeartbeatMarksFirstAndPeriodicFrames(int frame, int lastFrame, bool expected)
		{
			Assert.That(IosDevelopmentLaunch.ShouldLogHeartbeat("map-uid", frame, lastFrame), Is.EqualTo(expected));
			Assert.That(IosDevelopmentLaunch.ShouldLogHeartbeat(null, frame, lastFrame), Is.False);
		}

		[Test]
		public void DevelopmentSmokeFailureIsSingleLineAndCorrelatedToItsRun()
		{
			var detail = IosSmokeTestJournal.FormatFailure("run-42", new InvalidOperationException("broken\nwidget"));

			Assert.That(detail, Does.StartWith("run=run-42 type=InvalidOperationException message="));
			Assert.That(detail, Does.Contain("broken widget"));
			Assert.That(detail, Does.Not.Contain("\n"));
		}
	}
}
