using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class DiagnosticTraceBufferTest
	{
		delegate bool ActiveScopeQuery(string scopeName, out bool active);

		static ActiveScopeQuery CreateActiveScopeQuery(DiagnosticTraceBuffer trace)
		{
			var method = typeof(DiagnosticTraceBuffer).GetMethod(
				"TryIsScopeActive",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
				null,
				new[] { typeof(string), typeof(bool).MakeByRefType() },
				null);
			Assert.That(method, Is.Not.Null,
				"The watchdog needs a non-allocating active-scope query instead of a full Snapshot().");
			return (ActiveScopeQuery)method!.CreateDelegate(typeof(ActiveScopeQuery), trace);
		}

		[Test]
		public void SnapshotKeepsNewestEventsInGlobalOrder()
		{
			var trace = new DiagnosticTraceBuffer(3, () => 100);
			trace.Instant(DiagnosticSubsystem.Logic, "one");
			trace.Instant(DiagnosticSubsystem.Network, "two");
			trace.Instant(DiagnosticSubsystem.Audio, "three");
			trace.Instant(DiagnosticSubsystem.Render, "four");

			Assert.That(trace.Snapshot().Events.Select(e => e.Name),
				Is.EqualTo(new[] { "two", "three", "four" }));
		}

		[Test]
		public void NestedScopesExposeParentChainUntilDisposed()
		{
			var trace = new DiagnosticTraceBuffer(16, () => 100);
			using (trace.Scope(DiagnosticSubsystem.Loading, "StartGame"))
			{
				using (trace.Scope(DiagnosticSubsystem.Loading, "LoadComplete"))
				{
					var active = trace.Snapshot().ActiveScopes.Single();
					Assert.That(active.Path, Is.EqualTo("StartGame > LoadComplete"));
				}

				Assert.That(trace.Snapshot().ActiveScopes.Single().Path, Is.EqualTo("StartGame"));
			}

			Assert.That(trace.Snapshot().ActiveScopes, Is.Empty);
		}

		[Test]
		public void ActiveScopeQueryFindsNamedScopesWithoutAllocatingSnapshots()
		{
			var trace = new DiagnosticTraceBuffer(16, () => 100);
			var query = CreateActiveScopeQuery(trace);

			using (trace.Scope(DiagnosticSubsystem.Loading, "Game.StartGame"))
			using (trace.Scope(DiagnosticSubsystem.Loading, "World.LoadComplete"))
			{
				Assert.That(query("Game.StartGame", out var gameStartActive), Is.True);
				Assert.That(gameStartActive, Is.True);
				Assert.That(query("World.LoadComplete", out var loadCompleteActive), Is.True);
				Assert.That(loadCompleteActive, Is.True);

				// Warm the delegate/JIT before measuring the steady-state watchdog query.
				for (var i = 0; i < 32; i++)
					query("Game.StartGame", out _);

				var allocatedBefore = System.GC.GetAllocatedBytesForCurrentThread();
				for (var i = 0; i < 10000; i++)
					query("Game.StartGame", out _);
				var allocated = System.GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

				Assert.That(allocated, Is.Zero,
					"The once-per-second watchdog query must not copy the diagnostic event buffer.");
			}

			Assert.That(query("Game.StartGame", out var activeAfterDispose), Is.True);
			Assert.That(activeAfterDispose, Is.False);
		}

		[Test]
		public void ActiveScopeQueryFailsOpenWithoutWaitingForABusyBuffer()
		{
			using var clockEntered = new ManualResetEventSlim();
			using var releaseClock = new ManualResetEventSlim();
			using var queryCompleted = new ManualResetEventSlim();
			var blockClock = 1;
			var trace = new DiagnosticTraceBuffer(8, () =>
			{
				if (Interlocked.Exchange(ref blockClock, 0) == 1)
				{
					clockEntered.Set();
					releaseClock.Wait();
				}

				return 100;
			});
			var query = CreateActiveScopeQuery(trace);

			var lockHolder = new Thread(() => trace.Instant(DiagnosticSubsystem.Network, "holder"));
			lockHolder.Start();
			Assert.That(clockEntered.Wait(1000), Is.True);

			var queryAvailable = true;
			var watchdog = new Thread(() =>
			{
				queryAvailable = query("Game.StartGame", out _);
				queryCompleted.Set();
			});
			watchdog.Start();

			var completedWithoutWaiting = queryCompleted.Wait(250);
			releaseClock.Set();
			Assert.That(lockHolder.Join(1000), Is.True);
			Assert.That(watchdog.Join(1000), Is.True);
			Assert.That(completedWithoutWaiting, Is.True,
				"The watchdog scope query must fail open instead of blocking the game thread.");
			Assert.That(queryAvailable, Is.False,
				"An unavailable query must be distinguishable so the watchdog can use the safer loading threshold.");
		}

		[Test]
		public void ProgressRefreshesSubsystemHeartbeat()
		{
			long now = 10;
			var trace = new DiagnosticTraceBuffer(8, () => now);
			using var scope = trace.Scope(DiagnosticSubsystem.Loading, "Traits");
			now = 55;
			scope.Progress(4, 9);

			var heartbeat = trace.Snapshot().Heartbeats[DiagnosticSubsystem.Loading];
			Assert.That(heartbeat.TimestampMilliseconds, Is.EqualTo(55));
			Assert.That(heartbeat.Arg0, Is.EqualTo(4));
			Assert.That(heartbeat.Arg1, Is.EqualTo(9));
		}

		[Test]
		public void TraceWritesNeverWaitForAReaderOrWriterHoldingTheBuffer()
		{
			using var clockEntered = new ManualResetEventSlim();
			using var releaseClock = new ManualResetEventSlim();
			using var writeCompleted = new ManualResetEventSlim();
			var blockClock = 1;
			var trace = new DiagnosticTraceBuffer(8, () =>
			{
				if (Interlocked.Exchange(ref blockClock, 0) == 1)
				{
					clockEntered.Set();
					releaseClock.Wait();
				}

				return 100;
			});

			var lockHolder = new Thread(() => trace.Instant(DiagnosticSubsystem.Network, "holder"));
			lockHolder.Start();
			Assert.That(clockEntered.Wait(1000), Is.True, "The test thread did not acquire the trace buffer lock.");

			var gameThread = new Thread(() =>
			{
				using (trace.Scope(DiagnosticSubsystem.Loading, "game-thread")) { }
				writeCompleted.Set();
			});
			gameThread.Start();

			var completedWithoutWaiting = writeCompleted.Wait(250);
			releaseClock.Set();
			Assert.That(lockHolder.Join(1000), Is.True);
			Assert.That(gameThread.Join(1000), Is.True);
			Assert.That(completedWithoutWaiting, Is.True,
				"Diagnostic tracing must fail open instead of blocking the game thread.");
		}

		[Test]
		public void WatchdogSnapshotNeverWaitsForABusyBuffer()
		{
			using var clockEntered = new ManualResetEventSlim();
			using var releaseClock = new ManualResetEventSlim();
			using var snapshotCompleted = new ManualResetEventSlim();
			var blockClock = 1;
			var trace = new DiagnosticTraceBuffer(8, () =>
			{
				if (Interlocked.Exchange(ref blockClock, 0) == 1)
				{
					clockEntered.Set();
					releaseClock.Wait();
				}

				return 100;
			});

			var lockHolder = new Thread(() => trace.Instant(DiagnosticSubsystem.Network, "holder"));
			lockHolder.Start();
			Assert.That(clockEntered.Wait(1000), Is.True);

			DiagnosticTraceSnapshot snapshot = null;
			var watchdog = new Thread(() =>
			{
				snapshot = trace.Snapshot();
				snapshotCompleted.Set();
			});
			watchdog.Start();

			var completedWithoutWaiting = snapshotCompleted.Wait(250);
			releaseClock.Set();
			Assert.That(lockHolder.Join(1000), Is.True);
			Assert.That(watchdog.Join(1000), Is.True);
			Assert.That(completedWithoutWaiting, Is.True,
				"The watchdog must still write a report when the trace buffer is busy.");
			Assert.That(snapshot, Is.Not.Null);
			Assert.That(snapshot.Available, Is.False);
		}

		[Test]
		public void DeferredScopeEndIsReconciledAfterContention()
		{
			using var clockEntered = new ManualResetEventSlim();
			using var releaseClock = new ManualResetEventSlim();
			var clockCalls = 0;
			var trace = new DiagnosticTraceBuffer(8, () =>
			{
				if (Interlocked.Increment(ref clockCalls) == 2)
				{
					clockEntered.Set();
					releaseClock.Wait();
				}

				return 100;
			});

			var scope = trace.Scope(DiagnosticSubsystem.Loading, "load");
			var lockHolder = new Thread(() => trace.Instant(DiagnosticSubsystem.Network, "holder"));
			lockHolder.Start();
			Assert.That(clockEntered.Wait(1000), Is.True);

			scope.Dispose();
			releaseClock.Set();
			Assert.That(lockHolder.Join(1000), Is.True);

			Assert.That(trace.Snapshot().ActiveScopes, Is.Empty,
				"A fail-open scope end must not leave a permanent false hang scope.");
		}
	}
}
