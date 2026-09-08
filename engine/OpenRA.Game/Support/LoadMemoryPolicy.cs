namespace OpenRA
{
	using System;
	using System.IO;
	using System.Text;
	using OpenRA.Network;

	public static class LoadMemoryPolicy
	{
		// A forced stop-the-world collection can deadlock the iOS main thread while
		// SDL's native audio thread is executing a managed callback. Desktop keeps
		// the established loading-screen collection behavior.
		public static bool ShouldForceCollection(bool isIOS) => !isIOS;
	}

	public static class IosDevelopmentLaunch
	{
		public static string BuildSkirmishArgument(string target)
		{
			if (string.IsNullOrWhiteSpace(target))
				return null;

			return $"Game.LaunchInto=skirmish {target.Trim()}";
		}

		public static bool ShouldLogHeartbeat(string target, int frame, int lastLoggedFrame)
		{
			if (string.IsNullOrWhiteSpace(target) || frame <= 0 || frame == lastLoggedFrame)
				return false;

			return lastLoggedFrame < 0 || frame % 300 == 0;
		}
	}

	public static class IosLoadStageJournal
	{
		public static bool Record(string stage, string detail = null)
		{
			if (!OperatingSystem.IsIOS() || string.IsNullOrEmpty(Platform.SupportDir))
				return false;

			try
			{
				var directory = Path.Combine(Platform.SupportDir, "Logs");
				Directory.CreateDirectory(directory);
				var path = Path.Combine(directory, "ios-load-stage.log");
				using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read,
					4096, FileOptions.WriteThrough);
				using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
				writer.WriteLine($"{DateTime.UtcNow:O} {stage}{(detail == null ? string.Empty : " " + detail)}");
				stream.Flush(true);
				return true;
			}
			catch
			{
				// Diagnostics must never prevent the game from loading.
				return false;
			}
		}
	}

	public sealed class IosSmokeReadinessTracker
	{
		public const long RequiredDurationMilliseconds = 10_000;
		public const int RequiredWorldTicks = 100;
		public const int RequiredRenderFrames = 100;

		bool initialized;
		long initialElapsedMilliseconds;
		int initialWorldTick;
		int initialRenderFrame;
		int lastWorldTick;
		int lastRenderFrame;

		public bool Observe(long elapsedMilliseconds, int worldTick, int renderFrame, bool connected)
		{
			if (!connected)
			{
				initialized = false;
				return false;
			}

			if (!initialized)
			{
				initialized = true;
				initialElapsedMilliseconds = elapsedMilliseconds;
				initialWorldTick = worldTick;
				initialRenderFrame = renderFrame;
				lastWorldTick = worldTick;
				lastRenderFrame = renderFrame;
				return false;
			}

			var worldAdvanced = worldTick > lastWorldTick;
			var renderAdvanced = renderFrame > lastRenderFrame;
			lastWorldTick = worldTick;
			lastRenderFrame = renderFrame;

			var reachedProgressTarget = worldTick - initialWorldTick >= RequiredWorldTicks &&
				renderFrame - initialRenderFrame >= RequiredRenderFrames;
			var reachedDurationTarget = elapsedMilliseconds - initialElapsedMilliseconds >= RequiredDurationMilliseconds &&
				worldAdvanced && renderAdvanced;
			return reachedProgressTarget && reachedDurationTarget;
		}
	}

	public sealed class IosSmokeReadyRecorder
	{
		int state;

		public bool TryRecord(Func<bool> write)
		{
			if (write == null)
				throw new ArgumentNullException(nameof(write));

			if (System.Threading.Interlocked.CompareExchange(ref state, 1, 0) != 0)
				return false;

			var completed = false;
			try
			{
				completed = write();
				return completed;
			}
			finally
			{
				System.Threading.Volatile.Write(ref state, completed ? 2 : 0);
			}
		}
	}

	public static class IosSmokeTestJournal
	{
		static readonly string RunId = Environment.GetEnvironmentVariable("OPENRA_IOS_SMOKE_RUN_ID");
		static readonly string LaunchNonce =
			Environment.GetEnvironmentVariable("OPENRA_IOS_SMOKE_LAUNCH_NONCE");
		static readonly IosSmokeReadyRecorder readyRecorder = new();
		static IosSmokeReadinessTracker readiness = new();
		static World trackedWorld;

		public static void RecordStart()
		{
			if (string.IsNullOrWhiteSpace(RunId) || string.IsNullOrWhiteSpace(LaunchNonce))
				return;

			IosLoadStageJournal.Record("smoke-start", $"run={RunId} launch={LaunchNonce}");
		}

		public static void RecordReady(
			World world, int frame, int worldTick, int renderFrame, long elapsedMilliseconds)
		{
			if (string.IsNullOrWhiteSpace(RunId) || world == null || world.Type != WorldType.Regular || frame <= 0)
				return;

			if (!ReferenceEquals(trackedWorld, world))
			{
				trackedWorld = world;
				readiness = new IosSmokeReadinessTracker();
			}

			var connected = world.OrderManager.Connection is not NetworkConnection connection ||
				connection.ConnectionState == ConnectionState.Connected;
			if (!readiness.Observe(elapsedMilliseconds, worldTick, renderFrame, connected))
				return;

			readyRecorder.TryRecord(() => IosLoadStageJournal.Record(
				"smoke-ready", $"run={RunId} launch={LaunchNonce} criterion=duration-and-progress " +
					$"map={world.Map.Uid} frame={frame} " +
					$"worldTick={worldTick} renderFrame={renderFrame} elapsedMs={elapsedMilliseconds} " +
					"connection=Connected"));
		}

		public static string FormatFailure(string runId, Exception error, string launchNonce = null)
		{
			var message = (error?.Message ?? "unknown error").Replace('\r', ' ').Replace('\n', ' ');
			var launch = string.IsNullOrWhiteSpace(launchNonce) ? string.Empty : $" launch={launchNonce}";
			return $"run={runId}{launch} type={error?.GetType().Name ?? "UnknownException"} message={message}";
		}

		public static void RecordFailure(Exception error)
		{
			if (string.IsNullOrWhiteSpace(RunId))
				return;

			IosLoadStageJournal.Record("smoke-failure", FormatFailure(RunId, error, LaunchNonce));
		}
	}
}
