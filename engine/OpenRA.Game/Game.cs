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
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Server;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA
{
	public static class Game
	{
		// Hosts such as iOS can provide a statically linked platform implementation.
		// Desktop hosts leave this unset and continue to load OpenRA.Platforms.*.dll.
		public static Func<string, IPlatform> PlatformFactoryOverride { get; set; }

		[FluentReference("filename")]
		const string SavedScreenshot = "notification-saved-screenshot";
		const int RenderAuditRequestCheckInterval = 250;

		public const int TimestepJankThreshold = 250; // Don't catch up for delays larger than 250ms
		internal const int LifecycleDispatchBatch = 64;

		public static InstalledMods Mods { get; private set; }
		public static ExternalMods ExternalMods { get; private set; }

		public static ModData ModData;
		public static Settings Settings;
		public static CursorManager Cursor;
		public static bool HideCursor;

		static WorldRenderer worldRenderer;
		static string modLaunchWrapper;
		static OwnedEngineStartupState activeStartup;
		static int startupAdmission;
		static readonly ConditionalWeakTable<ReplayConnection, ReplaySourcePathHolder> ReplaySourcePaths = new();
		internal static Func<string, bool> ReplaySnapshotDeleteOverride;

		sealed class ReplaySourcePathHolder
		{
			public string SourcePath;
			public string SnapshotPath;
		}

		internal static OrderManager OrderManager;
		static Server.Server server;

		public static MersenneTwister CosmeticRandom = new(); // not synced

		public static Renderer Renderer;
		public static Sound Sound;
		public static IHapticEngine Haptics;

		public static string EngineVersion { get; private set; }
		public static LocalPlayerProfile LocalPlayerProfile;

		static bool takeScreenshot = false;
		static readonly bool RenderAuditEnabled =
			bool.TryParse(Environment.GetEnvironmentVariable("OPENRA_IOS_RENDER_AUDIT"), out var renderAuditEnabled) && renderAuditEnabled;
		static long nextRenderAuditRequestCheckAt;
		static Benchmark benchmark = null;

		public static event Action OnShellmapLoaded = () => { };

		public static OrderManager JoinServer(ConnectionTarget endpoint, string password, bool recordReplay = true)
		{
			var newConnection = new NetworkConnection(endpoint);
			if (recordReplay)
				newConnection.StartRecording(() => TimestampedFilename());

			var om = new OrderManager(newConnection);
			JoinInner(om);
			CurrentServerSettings.Password = password;
			CurrentServerSettings.Target = endpoint;

			lastConnectionState = ConnectionState.PreConnecting;
			ConnectionStateChanged(OrderManager, password, newConnection);

			return om;
		}

		public static string TimestampedFilename(bool includemilliseconds = false, string extra = "")
		{
			var format = includemilliseconds ? "yyyy-MM-ddTHHmmssfffZ" : "yyyy-MM-ddTHHmmssZ";
			return ModData.Manifest.Id + extra + "-" + DateTime.UtcNow.ToString(format, CultureInfo.InvariantCulture);
		}

		static void JoinInner(OrderManager om)
		{
			// Refresh static classes before the game starts.
			TextNotificationsManager.Clear();
			UnitOrders.Clear();

			// HACK: The shellmap World and OrderManager are owned by the main menu's WorldRenderer instead of Game.
			// This allows us to switch Game.OrderManager from the shellmap to the new network connection when joining
			// a lobby, while keeping the OrderManager that runs the shellmap intact.
			// A matching check in World.Dispose (which is called by WorldRenderer.Dispose) makes sure that we dispose
			// the shellmap's OM when a lobby game actually starts.
			if (OrderManager?.World == null || OrderManager.World.Type != WorldType.Shellmap)
			{
				ReleaseReplaySnapshot(OrderManager?.Connection as ReplayConnection);
				OrderManager?.Dispose();
			}

			OrderManager = om;
		}

		public static void JoinReplay(string replayFile)
		{
			ReplayConnection replayConnection = null;
			try
			{
				var activeRuntimeContract = ModData.RuntimeContract;
				if (activeRuntimeContract.IsLegacy)
				{
					var metadata = ReplayMetadata.Read(replayFile);
					if (metadata?.GameInfo == null)
						throw new InvalidDataException("The replay metadata is missing or invalid.");

					ThrowIfRuntimeContractIncompatible(
						activeRuntimeContract,
						metadata.GameInfo.RuntimeProfile,
						metadata.GameInfo.RuntimeContract,
						"metadata");
					replayConnection = new ReplayConnection(replayFile);
					ValidateReplayLobby(replayConnection, activeRuntimeContract);
				}
				else
				{
					var replayBytes = File.ReadAllBytes(replayFile);
					replayConnection = CreateCompatibleReplayConnection(
						replayBytes, activeRuntimeContract);
				}

				RememberReplaySourcePath(replayConnection, replayFile);

				var orderManager = new OrderManager(replayConnection);
				replayConnection = null;
				try
				{
					JoinInner(orderManager);
				}
				catch
				{
					ReleaseReplaySnapshot(orderManager.Connection as ReplayConnection);
					orderManager.Dispose();
					throw;
				}
			}
			finally
			{
				if (replayConnection != null)
				{
					ReleaseReplaySnapshot(replayConnection);
					((IDisposable)replayConnection).Dispose();
				}
			}
		}

		internal static void RememberReplaySourcePath(
			ReplayConnection replayConnection, string sourcePath)
		{
			if (replayConnection == null)
				throw new ArgumentNullException(nameof(replayConnection));
			if (sourcePath == null)
				throw new ArgumentNullException(nameof(sourcePath));

			var holder = ReplaySourcePaths.GetValue(
				replayConnection, _ => new ReplaySourcePathHolder());
			lock (holder)
				holder.SourcePath = sourcePath;
		}

		internal static string ReplaySourcePath(ReplayConnection replayConnection)
		{
			if (replayConnection == null)
				return null;

			if (!ReplaySourcePaths.TryGetValue(replayConnection, out var replaySourcePath))
				return replayConnection.Filename;

			lock (replaySourcePath)
				return replaySourcePath.SourcePath ?? replayConnection.Filename;
		}

		static void RememberReplaySnapshotPath(
			ReplayConnection replayConnection, string snapshotPath)
		{
			var holder = ReplaySourcePaths.GetValue(
				replayConnection, _ => new ReplaySourcePathHolder());
			lock (holder)
				holder.SnapshotPath = snapshotPath;
		}

		internal static void ReleaseReplaySnapshot(ReplayConnection replayConnection)
		{
			if (replayConnection == null ||
				!ReplaySourcePaths.TryGetValue(replayConnection, out var holder))
				return;

			lock (holder)
			{
				if (holder.SnapshotPath != null && TryDeleteReplaySnapshot(holder.SnapshotPath))
					holder.SnapshotPath = null;
			}
		}

		internal static ReplayConnection CreateCompatibleReplayConnection(
			byte[] replayBytes, RuntimeContractIdentity activeRuntimeContract)
		{
			if (replayBytes == null)
				throw new ArgumentNullException(nameof(replayBytes));

			var snapshotPath = Path.Combine(
				Path.GetTempPath(), $"openra-replay-{Guid.NewGuid():N}.orarep");
			var snapshotCreated = false;
			try
			{
				using (var snapshot = new FileStream(
					snapshotPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
				{
					snapshotCreated = true;
					snapshot.Write(replayBytes, 0, replayBytes.Length);
				}

				var metadata = ReplayMetadata.Read(snapshotPath);
				if (metadata?.GameInfo == null)
					throw new InvalidDataException("The replay metadata is missing or invalid.");

				ThrowIfRuntimeContractIncompatible(
					activeRuntimeContract,
					metadata.GameInfo.RuntimeProfile,
					metadata.GameInfo.RuntimeContract,
					"metadata");

				ReplayConnection replayConnection = null;
				try
				{
					replayConnection = new ReplayConnection(snapshotPath);
					if (replayConnection.LobbyInfo?.GlobalSettings == null)
						throw new InvalidDataException("The replay lobby metadata is missing or invalid.");

					ValidateReplayLobby(replayConnection, activeRuntimeContract);

					var compatibleReplayConnection = replayConnection;
					replayConnection = null;
					RememberReplaySnapshotPath(compatibleReplayConnection, snapshotPath);
					snapshotCreated = false;
					return compatibleReplayConnection;
				}
				finally
				{
					if (replayConnection != null)
						((IDisposable)replayConnection).Dispose();
				}
			}
			finally
			{
				if (snapshotCreated)
					TryDeleteReplaySnapshot(snapshotPath);
			}
		}

		static void ValidateReplayLobby(
			ReplayConnection replayConnection,
			RuntimeContractIdentity activeRuntimeContract)
		{
			if (replayConnection.LobbyInfo?.GlobalSettings == null)
				throw new InvalidDataException("The replay lobby metadata is missing or invalid.");

			ThrowIfRuntimeContractIncompatible(
				activeRuntimeContract,
				replayConnection.LobbyInfo.GlobalSettings.RuntimeProfile,
				replayConnection.LobbyInfo.GlobalSettings.RuntimeContract,
				"lobby");
		}

		static void ThrowIfRuntimeContractIncompatible(
			RuntimeContractIdentity activeRuntimeContract,
			string storedProfile, string storedContract, string source)
		{
			var compatibility = RuntimeContractStorage.Compatibility(
				activeRuntimeContract, storedProfile, storedContract);
			if (compatibility != RuntimeContractCompatibilityReason.Compatible)
				throw new RuntimeContractCompatibilityException(
					compatibility,
					$"The replay {source} runtime identity is incompatible ({compatibility}).");
		}

		static bool TryDeleteReplaySnapshot(string snapshotPath)
		{
			var deleteOverride = ReplaySnapshotDeleteOverride;
			if (deleteOverride != null)
				return deleteOverride(snapshotPath);

			try
			{
				File.Delete(snapshotPath);
				return true;
			}
			catch (Exception ex) when (
				ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
			{
				Log.Write("debug", ex.ToString());
				return false;
			}
		}

		static void JoinLocal()
		{
			JoinInner(new OrderManager(new EchoConnection()));

			// Add a spectator client for the local player
			// On the shellmap this player is controlling the map via scripted orders
			OrderManager.LobbyInfo.Clients.Add(new Session.Client
			{
				Index = OrderManager.Connection.LocalClientId,
				Name = Settings.Player.Name,
				PreferredColor = Settings.Player.Color,
				Color = Settings.Player.Color,
				Faction = "Random",
				SpawnPoint = 0,
				Team = 0,
				State = Session.ClientState.Ready
			});
		}

		// More accurate replacement for Environment.TickCount
		static readonly Stopwatch Stopwatch = Stopwatch.StartNew();
		public static long RunTime => Stopwatch.ElapsedMilliseconds;

		public static int RenderFrame = 0;
		public static int NetFrameNumber => OrderManager.NetFrameNumber;
		public static int LocalTick => OrderManager.LocalFrameNumber;

		public static event Action<ConnectionTarget> OnRemoteDirectConnect = _ => { };
		public static event Action<OrderManager, string, NetworkConnection> ConnectionStateChanged = (om, pass, conn) => { };
		static ConnectionState lastConnectionState = ConnectionState.PreConnecting;
		public static int LocalClientId => OrderManager.Connection.LocalClientId;

		public static void RemoteDirectConnect(ConnectionTarget endpoint)
		{
			OnRemoteDirectConnect(endpoint);
		}

		// Hacky workaround for orderManager visibility
		public static Widget OpenWindow(World world, string widget)
		{
			return Ui.OpenWindow(widget, new WidgetArgs() { { "world", world }, { "orderManager", OrderManager }, { "worldRenderer", worldRenderer } });
		}

		// Who came up with the great idea of making these things
		// impossible for the things that want them to access them directly?
		public static Widget OpenWindow(string widget, WidgetArgs args)
		{
			return Ui.OpenWindow(widget, new WidgetArgs(args)
			{
				{ "world", worldRenderer.World },
				{ "orderManager", OrderManager },
				{ "worldRenderer", worldRenderer },
			});
		}

		// Load a widget with world, orderManager, worldRenderer args, without adding it to the widget tree
		public static Widget LoadWidget(World world, string id, Widget parent, WidgetArgs args)
		{
			return ModData.WidgetLoader.LoadWidget(new WidgetArgs(args)
			{
				{ "world", world },
				{ "orderManager", OrderManager },
				{ "worldRenderer", worldRenderer },
			}, parent, id);
		}

		public static event Action LobbyInfoChanged = () => { };

		internal static void SyncLobbyInfo()
		{
			LobbyInfoChanged();
		}

		public static event Action BeforeGameStart = () => { };
		public static event Action AfterGameStart = () => { };
		internal static void StartGame(string mapUID, WorldType type)
		{
			HangDiagnostics.Record("loading.start-game.enter");
			using var startGameTrace = DiagnosticTrace.Scope(DiagnosticSubsystem.Loading, "Game.StartGame");

			// Dispose of the old world before creating a new one.
			using (DiagnosticTrace.Scope(DiagnosticSubsystem.Loading, "DisposeOldWorld"))
				worldRenderer?.Dispose();

			Cursor.SetCursor(null);
			using (DiagnosticTrace.Scope(DiagnosticSubsystem.Loading, "BeforeGameStart"))
				BeforeGameStart();

			using (new PerfTimer("NewWorld"))
			using (DiagnosticTrace.Scope(DiagnosticSubsystem.Loading, "World.ctor"))
				OrderManager.World = new World(mapUID, ModData, OrderManager, type);
			IosLoadStageJournal.Record("world-created", $"map={mapUID}");

			// Local patch: pin the OrderManager/World this call created.
			// LoadComplete below can synchronously replace Game.OrderManager via JoinInner
			// (e.g. Game.LaunchInto opening the skirmish lobby from MainMenuLogic),
			// so dereferencing OrderManager.World after that point can hit a null World
			// on the freshly-joined lobby OrderManager.
			var orderManager = OrderManager;
			var world = OrderManager.World;

			world.GameOver += FinishBenchmark;

			using (DiagnosticTrace.Scope(DiagnosticSubsystem.Loading, "WorldRenderer.ctor"))
				worldRenderer = new WorldRenderer(ModData, world);
			IosLoadStageJournal.Record("renderer-created", $"map={mapUID}");

			// Proactively collect memory during loading to reduce peak memory.
			// iOS relies on the runtime collector while the managed SDL audio callback is active.
			if (LoadMemoryPolicy.ShouldForceCollection(OperatingSystem.IsIOS()))
				using (DiagnosticTrace.Scope(DiagnosticSubsystem.Loading, "GC.BeforeLoadComplete"))
					GC.Collect();

			using (new PerfTimer("LoadComplete"))
			using (DiagnosticTrace.Scope(DiagnosticSubsystem.Loading, "World.LoadComplete"))
				world.LoadComplete(worldRenderer);
			HangDiagnostics.Record("loading.world-load-complete.exit");
			IosLoadStageJournal.Record("load-complete", $"map={mapUID}");

			// Proactively collect memory during loading to reduce peak memory.
			if (LoadMemoryPolicy.ShouldForceCollection(OperatingSystem.IsIOS()))
				using (DiagnosticTrace.Scope(DiagnosticSubsystem.Loading, "GC.AfterLoadComplete"))
					GC.Collect();

			if (orderManager.GameStarted)
				return;

			Ui.MouseFocusWidget = null;
			Ui.KeyboardFocusWidget = null;

			orderManager.StartGame();
			IosLoadStageJournal.Record("order-manager-started", $"map={mapUID}");
			worldRenderer.RefreshPalette();
			IosLoadStageJournal.Record("palette-refreshed", $"map={mapUID}");
			Cursor.SetCursor(ChromeMetrics.Get<string>("DefaultCursor"));

			// Now loading is completed, now is the ideal time to run a GC and compact the LOH.
			// - All the temporary garbage created during loading can be collected.
			// - Live objects are likely to live for the length of the game or longer,
			//   thus promoting them into a higher generation is not an issue.
			// - We can remove any fragmentation in the LOH caused by temporary loading garbage.
			// - A loading screen is visible, so a delay won't matter to the user.
			//   Much better to clean up now then to drop frames during gameplay for GC pauses.
			// The iOS runtime does not support explicit LOH compaction and must not
			// force a stop-the-world collection while the audio callback is active.
			if (LoadMemoryPolicy.ShouldForceCollection(OperatingSystem.IsIOS()))
			{
				GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
				GC.Collect();
			}

			// PostLoadComplete is designed for anything that should trigger at the very end of loading.
			// e.g. audio notifications that the game is starting.
			using (DiagnosticTrace.Scope(DiagnosticSubsystem.Loading, "World.PostLoadComplete"))
				world.PostLoadComplete(worldRenderer);
			IosLoadStageJournal.Record("post-load-complete", $"map={mapUID}");

			AfterGameStart();
			benchmark?.BeginAutomatic();
			IosLoadStageJournal.Record("after-game-start", $"map={mapUID}");
			HangDiagnostics.Record("loading.start-game.exit");
		}

		public static void RestartGame()
		{
			var replay = OrderManager.Connection as ReplayConnection;
			var replayName = ReplaySourcePath(replay);
			ReleaseReplaySnapshot(replay);
			var lobbyInfo = OrderManager.LobbyInfo;

			// Reseed the RNG so this isn't an exact repeat of the last game
			lobbyInfo.GlobalSettings.RandomSeed = CosmeticRandom.Next();

			// Note: the map may have been changed on disk outside the game, changing its UID.
			// Use the updated UID if we have tracked the update instead of failing.
			lobbyInfo.GlobalSettings.Map = ModData.MapCache.GetUpdatedMap(lobbyInfo.GlobalSettings.Map);
			if (lobbyInfo.GlobalSettings.Map == null)
			{
				Disconnect();
				Ui.ResetAll();
				LoadShellMap();
				return;
			}

			var orders = new[]
			{
					Order.Command($"sync_lobby {lobbyInfo.Serialize()}"),
					Order.Command("startgame")
			};

			// Disconnect from the current game
			Disconnect();
			Ui.ResetAll();

			// Restart the game with the same replay/mission
			if (replay != null)
				JoinReplay(replayName);
			else
				CreateAndStartLocalServer(lobbyInfo.GlobalSettings.Map, orders);
		}

		public static void CreateAndStartLocalServer(string mapUID, IEnumerable<Order> setupOrders)
		{
			OrderManager om = null;

			void LobbyReady()
			{
				LobbyInfoChanged -= LobbyReady;
				foreach (var o in setupOrders)
					om.IssueOrder(o);
			}

			LobbyInfoChanged += LobbyReady;

			om = JoinServer(CreateLocalServer(mapUID), "");
		}

		public static bool IsHost
		{
			get
			{
				var id = OrderManager.Connection.LocalClientId;
				var client = OrderManager.LobbyInfo.ClientWithIndex(id);
				return client != null && client.IsAdmin;
			}
		}

		static Modifiers modifiers;
		public static Modifiers GetModifierKeys() { return modifiers; }
		internal static void HandleModifierKeys(Modifiers mods) { modifiers = mods; }

		public static void InitializeSettings(Arguments args)
		{
			Settings = new Settings(Path.Combine(Platform.SupportDir, "settings.yaml"), args);
		}

		static void InitializeSettings(
			Arguments args, string modId,
			RuntimeContractIdentity runtimeContract)
		{
			var globalSettingsFile = Path.Combine(Platform.SupportDir, "settings.yaml");
			var serverSettingsFile = RuntimeContractStorage.ServerSettingsPath(
				Platform.SupportDir, modId, runtimeContract);
			Settings = new Settings(globalSettingsFile, serverSettingsFile, args);
		}

		public static RunStatus InitializeAndRun(string[] args)
		{
			if (!TryAcquireStartupAdmission())
				throw new InvalidOperationException("A game startup owner is already active.");

			OwnedEngineStartupState ownedStartup = null;
			ExceptionDispatchInfo firstFailure = null;
			var result = default(RunStatus);
			var runCompleted = false;
			try
			{
				Initialize(new Arguments(args));
				ownedStartup = Volatile.Read(ref activeStartup) ??
					throw new InvalidOperationException("Legacy startup did not publish its owned state.");
				if (!ownedStartup.RuntimeContract.IsLegacy)
					throw new InvalidOperationException("The public startup entry requires a Legacy owner.");

				IosSmokeTestJournal.RecordStart();

				// Proactively collect memory during loading to reduce peak memory.
				if (LoadMemoryPolicy.ShouldForceCollection(OperatingSystem.IsIOS()))
					GC.Collect();

				result = RunProfiled();
				runCompleted = true;
			}
			catch (Exception exception)
			{
				firstFailure = ExceptionDispatchInfo.Capture(exception);
			}
			finally
			{
				try
				{
					if (ownedStartup != null)
					{
						var cleanupFailure = CleanupProfiledStartup(ownedStartup, runCompleted);
						firstFailure ??= cleanupFailure;
					}
				}
				finally { ReleaseStartupAdmission(); }
			}

			firstFailure?.Throw();
			return result;
		}

		static void Initialize(Arguments args)
		{
			OwnedEngineStartupState legacyStartup = null;
			ExceptionDispatchInfo firstFailure = null;
			try { Initialize(args, null, out legacyStartup); }
			catch (Exception exception)
			{
				firstFailure = ExceptionDispatchInfo.Capture(exception);
			}
			finally
			{
				if (firstFailure != null && legacyStartup != null)
				{
					var cleanupFailure = CleanupProfiledStartup(legacyStartup, notifyQuit: false);
					firstFailure ??= cleanupFailure;
				}
			}

			firstFailure?.Throw();
		}

		static void Initialize(Arguments args, OwnedEngineStartupState startup)
		{
			Initialize(args, startup, out _);
		}

		static OwnedEngineStartupState CreateLegacyStartupState(
			Arguments args, string modID,
			IEnumerable<string> modSearchPaths,
			IEnumerable<string> explicitModPaths)
		{
			using var options = ModDataConstructionOptions.CreateTrustedDesktopDefault(
				ModDataSettingsSnapshot.Capture(Settings));
			OwnedModDataConstructionState construction = null;
			EngineStartupState startup = null;
			try
			{
				construction = options.TakeOwnership();
				startup = new EngineStartupState(
					new EngineBootstrapConfiguration(
						Platform.EngineDir,
						Platform.SupportDir,
						ImmutableArray.CreateRange(modSearchPaths),
						ImmutableArray.CreateRange(explicitModPaths),
						args),
					TrustedManifestSelection.Default(modID),
					RuntimeContractIdentity.Legacy,
					construction);
				construction = null;

				var ownedStartup = startup.TakeOwnership();
				startup = null;
				return ownedStartup;
			}
			finally
			{
				startup?.Dispose();
				construction?.Dispose();
			}
		}

		static void Initialize(
			Arguments args, OwnedEngineStartupState startup,
			out OwnedEngineStartupState legacyStartup)
		{
			legacyStartup = null;
			if (startup == null)
			{
				var engineDirArg = args.GetValue("Engine.EngineDir", null);
				if (!string.IsNullOrEmpty(engineDirArg))
					Platform.OverrideEngineDir(engineDirArg);

				var supportDirArg = args.GetValue("Engine.SupportDir", null);
				if (!string.IsNullOrEmpty(supportDirArg))
					Platform.OverrideSupportDir(supportDirArg);
			}
			else
				Platform.ConfigureDirectories(
					startup.Configuration.EngineDir, startup.Configuration.SupportDir);

			Console.WriteLine($"Platform is {Platform.CurrentPlatform} ({Platform.CurrentArchitecture})");

			// Load the engine version as early as possible so it can be written to exception logs
			try
			{
				EngineVersion = File.ReadAllText(Path.Combine(Platform.EngineDir, "VERSION")).Trim();
			}
			catch { }

			if (string.IsNullOrEmpty(EngineVersion))
				EngineVersion = "Unknown";

			Console.WriteLine($"Engine version is {EngineVersion}");
			Console.WriteLine($"Runtime: {Platform.RuntimeVersion}");

			// Special case handling of Game.Mod argument: if it matches a real filesystem path
			// then we use this to override the mod search path, and replace it with the mod id
			var modID = startup?.Selection.ModId ?? args.GetValue("Game.Mod", null);
			IEnumerable<string> explicitModPaths = startup != null ?
				startup.Configuration.ExplicitModPaths : Array.Empty<string>();
			if (startup == null && modID != null && (File.Exists(modID) || Directory.Exists(modID)))
			{
				explicitModPaths = new[] { modID };
				modID = Path.GetFileNameWithoutExtension(modID);
			}

			var modSearchArg = args.GetValue("Engine.ModSearchPaths", null);
			IEnumerable<string> modSearchPaths = startup != null ?
				startup.Configuration.ModSearchPaths :
				modSearchArg != null ?
					FieldLoader.GetValue<string[]>("Engine.ModsPath", modSearchArg) :
					new[] { Path.Combine(Platform.EngineDir, "mods") };

			if (startup == null || startup.RuntimeContract.IsLegacy)
				InitializeSettings(args);
			else
				InitializeSettings(args, modID, startup.RuntimeContract);
			if (startup == null)
			{
				if (string.IsNullOrEmpty(modID))
					throw new InvalidOperationException("Game.Mod argument missing.");

				var candidate = CreateLegacyStartupState(
					args, modID, modSearchPaths, explicitModPaths);
				if (Interlocked.CompareExchange(ref activeStartup, candidate, null) != null)
				{
					var collision = ExceptionDispatchInfo.Capture(
						new InvalidOperationException("A game startup owner is already active."));
					try { candidate.Dispose(); }
					catch { }
					collision.Throw();
				}

				legacyStartup = candidate;
				startup = candidate;
			}

			if (startup != null)
			{
				var construction = startup.Construction ??
					throw new ObjectDisposedException(nameof(OwnedEngineStartupState));
				if (construction.FluentMode != FluentInitializationMode.PublishGlobal ||
					construction.RenderingMode != ModDataRenderingMode.Game)
					throw new InvalidOperationException(
						"Game startup requires publish-global rendered construction state.");

				var snapshot = construction.Settings;
				if (Settings.Game.Language != snapshot.Language ||
					Settings.SystemLanguageTag != snapshot.SystemLanguageTag ||
					Settings.Graphics.SheetSize != snapshot.GraphicsSheetSize)
					throw new InvalidOperationException(
						"Game startup settings differ from the sealed pre-game snapshot.");

				Settings.Keys = snapshot.TakePublishGlobalHotkeys();
			}

			Log.AddChannel("perf", "perf.log");
			Log.AddChannel("debug", "debug.log");
			Log.AddChannel("server", "server.log", true);
			Log.AddChannel("sound", "sound.log");
			Log.AddChannel("graphics", "graphics.log");
			Log.AddChannel("geoip", "geoip.log");
			Log.AddChannel("nat", "nat.log");
			Log.AddChannel("client", "client.log");
			HangDiagnostics.Start();

			var platforms = new[] { Settings.Game.Platform, "Default", null };
			foreach (var p in platforms)
			{
				if (p == null)
					throw new InvalidOperationException("Failed to initialize platform-integration library. Check graphics.log for details.");

				Settings.Game.Platform = p;
				try
				{
					var platform = CreatePlatform(p);
					Renderer = new Renderer(platform, Settings.Graphics);
					Sound = new Sound(platform, Settings.Sound);
					Haptics = platform.CreateHaptics();

					break;
				}
				catch (Exception e)
				{
					Log.Write("graphics", $"{e}");
					Console.WriteLine("Renderer initialization failed. Check graphics.log for details.");

					try
					{
						Renderer?.Dispose();
					}
					catch (Exception disposeError)
					{
						Log.Write("graphics", $"{disposeError}");
					}

					Renderer = null;

					try
					{
						Sound?.Dispose();
					}
					catch (Exception disposeError)
					{
						Log.Write("graphics", $"{disposeError}");
					}

					Sound = null;

					try
					{
						Haptics?.Dispose();
					}
					catch (Exception disposeError)
					{
						Log.Write("graphics", $"{disposeError}");
					}

					Haptics = null;
				}
			}

			Nat.Initialize();

			Mods = new InstalledMods(modSearchPaths, explicitModPaths);
			Console.WriteLine("Internal mods:");
			foreach (var mod in Mods)
				Console.WriteLine($"\t{mod.Key} ({mod.Value.Metadata.Version})");

			modLaunchWrapper = args.GetValue("Engine.LaunchWrapper", null);

			ExternalMods = new ExternalMods();

			if (modID != null && Mods.TryGetValue(modID, out _))
			{
				var launchPath = args.GetValue("Engine.LaunchPath", null);
				var launchArgs = new List<string>();

				// Sanitize input from platform-specific launchers
				// Process.Start requires paths to not be quoted, even if they contain spaces
				if (launchPath != null && launchPath[0] == '"' && launchPath.Last() == '"')
					launchPath = launchPath[1..^1];

				// Metadata registration requires an explicit launch path
				if (launchPath != null)
					ExternalMods.Register(Mods[modID], launchPath, launchArgs, ModRegistration.User);

				ExternalMods.ClearInvalidRegistrations(ModRegistration.User);
			}

			Console.WriteLine("External mods:");
			foreach (var mod in ExternalMods)
				Console.WriteLine($"\t{mod.Key} ({mod.Value.Version})");

			InitializeMod(modID, args);
		}

		internal static RunStatus InitializeAndRun(EngineStartupState startup)
		{
			if (startup == null)
				throw new ArgumentNullException(nameof(startup));

			var ownedStartup = startup.TakeOwnership();
			ExceptionDispatchInfo firstFailure = null;
			var result = default(RunStatus);
			var runCompleted = false;
			var ownsActiveSlot = false;
			var ownsStartupAdmission = false;
			try
			{
				if (!TryAcquireStartupAdmission())
					throw new InvalidOperationException("A game startup owner is already active.");
				ownsStartupAdmission = true;
				if (Interlocked.CompareExchange(ref activeStartup, ownedStartup, null) != null)
					throw new InvalidOperationException("A game startup owner is already active.");
				ownsActiveSlot = true;
				Initialize(ownedStartup.Configuration.Arguments, ownedStartup);
				IosSmokeTestJournal.RecordStart();

				if (LoadMemoryPolicy.ShouldForceCollection(OperatingSystem.IsIOS()))
					GC.Collect();

				result = RunProfiled();
				runCompleted = true;
			}
			catch (Exception exception)
			{
				firstFailure = ExceptionDispatchInfo.Capture(exception);
			}
			finally
			{
				try
				{
					if (ownsActiveSlot)
					{
						var cleanupFailure = CleanupProfiledStartup(ownedStartup, runCompleted);
						firstFailure ??= cleanupFailure;
					}
					else
						try { ownedStartup.Dispose(); }
						catch (Exception exception)
						{
							firstFailure ??= ExceptionDispatchInfo.Capture(exception);
						}
				}
				finally
				{
					if (ownsStartupAdmission)
						ReleaseStartupAdmission();
				}
			}

			firstFailure?.Throw();
			return result;
		}

		static bool TryAcquireStartupAdmission()
		{
			if (Interlocked.CompareExchange(ref startupAdmission, 1, 0) != 0)
				return false;

			if (Volatile.Read(ref activeStartup) == null)
				return true;

			Volatile.Write(ref startupAdmission, 0);
			return false;
		}

		static void ReleaseStartupAdmission()
		{
			Volatile.Write(ref startupAdmission, 0);
		}

		public static IPlatform CreatePlatform(string platformName)
		{
			if (PlatformFactoryOverride != null)
				return PlatformFactoryOverride(platformName);

			var rendererPath = Path.Combine(Platform.BinDir, "OpenRA.Platforms." + platformName + ".dll");

#if NET5_0_OR_GREATER
			var loader = new AssemblyLoader(rendererPath);
			var platformType = loader.LoadDefaultAssembly().GetTypes().SingleOrDefault(t => typeof(IPlatform).IsAssignableFrom(t));

#else
			// NOTE: This is currently the only use of System.Reflection in this file, so would give an unused using error if we import it above
			var assembly = System.Reflection.Assembly.LoadFile(rendererPath);
			var platformType = assembly.GetTypes().SingleOrDefault(t => typeof(IPlatform).IsAssignableFrom(t));
#endif

			if (platformType == null)
				throw new InvalidOperationException("Platform dll must include exactly one IPlatform implementation.");

			return (IPlatform)platformType.GetConstructor(Type.EmptyTypes).Invoke(null);
		}

		static ExceptionDispatchInfo TeardownCurrentModEpoch()
		{
			ExceptionDispatchInfo firstFailure = null;
			void Cleanup(Action action)
			{
				try { action(); }
				catch (Exception exception)
				{
					firstFailure ??= ExceptionDispatchInfo.Capture(exception);
				}
			}

			Cleanup(Ui.ResetAll);

			var ownedCursor = Cursor;
			Cursor = null;
			if (ownedCursor != null)
				Cleanup(ownedCursor.Dispose);
			LocalPlayerProfile = null;

			var ownedWorldRenderer = worldRenderer;
			worldRenderer = null;
			if (ownedWorldRenderer != null)
				Cleanup(ownedWorldRenderer.Dispose);

			var ownedServer = server;
			server = null;
			if (ownedServer != null)
				Cleanup(ownedServer.Shutdown);

			var ownedOrderManager = OrderManager;
			OrderManager = null;
			if (ownedOrderManager != null)
			{
				Cleanup(() => ReleaseReplaySnapshot(
					ownedOrderManager.Connection as ReplayConnection));
				Cleanup(ownedOrderManager.Dispose);
			}

			var ownedModData = ModData;
			if (ownedModData != null)
			{
				var lifecycleLane = ownedModData.LifecycleDispatchLane;
				if (lifecycleLane is IGameLifecycleDispatchWaiter)
					Cleanup(() => ownedModData.DrainActivationsAndUnbind(
						fence => DrainLifecycleDispatchUntil(lifecycleLane, fence)));

				Cleanup(ownedModData.Dispose);
				if (lifecycleLane is IDisposable disposableLane)
					Cleanup(disposableLane.Dispose);
			}

			ModData = null;
			return firstFailure;
		}

		static ModData CreateModDataEpoch(string mod)
		{
			var startup = activeStartup;
			if (startup == null)
				return new ModData(Mods[mod], Mods, true);

			var selection = startup.Selection;
			if (!string.Equals(mod, selection.ModId, StringComparison.Ordinal))
				throw new InvalidOperationException(
					"Profiled startup cannot switch away from its trusted manifest selection.");

			var construction = startup.Construction ??
				throw new ObjectDisposedException(nameof(OwnedEngineStartupState));
			var lifecycleLane = new GameLifecycleDispatchLane();
			try
			{
				var manifest = Mods.OpenManifest(selection);
				return new ModData(
					manifest, Mods, selection, startup.RuntimeContract,
					construction, lifecycleLane, useLoadScreen: true,
					ownsManifestPackage: true, ownsProvider: false);
			}
			catch
			{
				try { lifecycleLane.Dispose(); }
				catch { }
				throw;
			}
		}

		static Arguments ModEpochArguments(Arguments fallback)
		{
			if (fallback == null)
				throw new ArgumentNullException(nameof(fallback));

			return activeStartup?.Configuration.Arguments ?? fallback;
		}

		public static void InitializeMod(string mod, Arguments args)
		{
			if (activeStartup != null &&
				!string.Equals(mod, activeStartup.Selection.ModId, StringComparison.Ordinal))
				throw new InvalidOperationException(
					"Profiled startup cannot switch away from its trusted manifest selection.");
			var epochArgs = ModEpochArguments(args);

			// Clear static state if we have switched mods
			LobbyInfoChanged = () => { };
			ConnectionStateChanged = (om, p, conn) => { };
			BeforeGameStart = () => { };
			OnRemoteDirectConnect = endpoint => { };

			TeardownCurrentModEpoch()?.Throw();
			delayedActions = new ActionQueue();

			if (mod == null)
				throw new InvalidOperationException("Game.Mod argument missing.");

			if (!Mods.ContainsKey(mod))
				throw new InvalidOperationException($"Unknown or invalid mod '{mod}'.");

			Console.WriteLine($"Loading mod: {mod}");

			Sound.StopVideo();

			ModData = CreateModDataEpoch(mod);

			LocalPlayerProfile = new LocalPlayerProfile(Path.Combine(Platform.SupportDir, Settings.Game.AuthProfile), ModData.Manifest.Get<PlayerDatabase>());

			if (!ModData.LoadScreen.BeforeLoad())
				return;

			ModData.InitializeLoaders(ModData.DefaultFileSystem);
			Renderer.InitializeFonts(ModData);

			using (new PerfTimer("LoadMaps"))
				ModData.MapCache.LoadMaps();

			var grid = ModData.Manifest.Contains<MapGrid>() ? ModData.Manifest.Get<MapGrid>() : null;
			Renderer.InitializeDepthBuffer(grid);

			Cursor?.Dispose();
			Cursor = new CursorManager(ModData.CursorProvider, ModData.Manifest.CursorSheetSize);

			var metadata = ModData.Manifest.Metadata;
			if (!string.IsNullOrEmpty(metadata.WindowTitleTranslated))
				Renderer.Window.SetWindowTitle(metadata.WindowTitleTranslated);

			PerfHistory.Items["render"].HasNormalTick = false;
			PerfHistory.Items["batches"].HasNormalTick = false;
			PerfHistory.Items["render_world"].HasNormalTick = false;
			PerfHistory.Items["render_widgets"].HasNormalTick = false;
			PerfHistory.Items["render_flip"].HasNormalTick = false;
			PerfHistory.Items["terrain_lighting"].HasNormalTick = false;

			JoinLocal();

			ModData.LoadScreen.StartGame(epochArgs);
		}

		public static void LoadEditor(string mapUid)
		{
			JoinLocal();
			StartGame(mapUid, WorldType.Editor);
		}

		public static void LoadShellMap()
		{
			var shellmap = ChooseShellmap();
			using (new PerfTimer("StartGame"))
			{
				StartGame(shellmap, WorldType.Shellmap);
				OnShellmapLoaded();
			}
		}

		static string ChooseShellmap()
		{
			var shellmaps = ModData.MapCache
				.Where(m => m.Status == MapStatus.Available && m.Visibility.HasFlag(MapVisibility.Shellmap))
				.Select(m => m.Uid);

			var shellmap = shellmaps.RandomOrDefault(CosmeticRandom);
			if (shellmap == null)
				throw new InvalidDataException("No valid shellmaps available");

			return shellmap;
		}

		public static void SwitchToExternalMod(ExternalMod mod, string[] launchArguments = null, Action onFailed = null)
		{
			try
			{
				var path = mod.LaunchPath;
				var args = launchArguments != null ? mod.LaunchArgs.Append(launchArguments) : mod.LaunchArgs;
				if (modLaunchWrapper != null)
				{
					path = modLaunchWrapper;
					args = new[] { mod.LaunchPath }.Concat(args);
				}

				var p = Process.Start(path, args.Select(a => "\"" + a + "\"").JoinWith(" "));
				if (p == null || p.HasExited)
					onFailed();
				else
				{
					p.Close();
					Exit();
				}
			}
			catch (Exception e)
			{
				Log.Write("debug", "Failed to switch to external mod.");
				Log.Write("debug", "Error was: " + e.Message);
				onFailed();
			}
		}

		static RunStatus state = RunStatus.Running;
		public static event Action OnQuit = () => { };

		// Note: These delayed actions should only be used by widgets or disposing objects
		// - things that depend on a particular world should be queuing them on the world actor.
		static volatile ActionQueue delayedActions = new();

		public static void RunAfterTick(Action a) { delayedActions.Add(a, RunTime); }
		public static void RunAfterDelay(int delayMilliseconds, Action a) { delayedActions.Add(a, RunTime + delayMilliseconds); }

		static void TakeScreenshotInner()
		{
			using (new PerfTimer("Renderer.SaveScreenshot"))
			{
				var mod = ModData.Manifest.Metadata;
				var directory = Path.Combine(Platform.SupportDir, "Screenshots", ModData.Manifest.Id, mod.Version);
				Directory.CreateDirectory(directory);

				var filename = TimestampedFilename(true);
				var path = Path.Combine(directory, string.Concat(filename, ".png"));
				Log.Write("debug", "Taking screenshot " + path);

				Renderer.SaveScreenshot(path);
				TextNotificationsManager.Debug(FluentProvider.GetMessage(SavedScreenshot, "filename", filename));
			}
		}

		static void CaptureRequestedRenderAuditFrame()
		{
			if (!RenderAuditEnabled || RunTime < nextRenderAuditRequestCheckAt)
				return;

			nextRenderAuditRequestCheckAt = RunTime + RenderAuditRequestCheckInterval;
			var directory = Path.Combine(Platform.SupportDir, "RenderAudit");
			var requestDirectory = Path.Combine(directory, "requests");
			if (File.Exists(requestDirectory))
			{
				try { File.Delete(requestDirectory); }
				catch (IOException) { return; }
			}

			Directory.CreateDirectory(requestDirectory);
			var requestPath = Directory.EnumerateFiles(requestDirectory, "*.request")
				.OrderBy(path => path, StringComparer.Ordinal)
				.FirstOrDefault();
			if (requestPath == null)
				return;

			try { File.Delete(requestPath); }
			catch (IOException) { return; }

			var requestId = Path.GetFileNameWithoutExtension(requestPath);
			var path = Path.Combine(directory, $"render-{requestId}.png");
			Log.Write("debug", "Capturing requested render audit screenshot " + path);
			Renderer.SaveScreenshot(path);
		}

		static void InnerLogicTick(OrderManager orderManager)
		{
			HangDiagnostics.Record("logic.inner.enter");
			var tick = RunTime;

			var world = orderManager.World;

			if (Ui.LastTickTime.ShouldAdvance(tick))
			{
				Ui.LastTickTime.AdvanceTickTime(tick);
				Sync.RunUnsynced(world, Ui.Tick);
				Cursor.Tick();
			}

			if (orderManager.LastTickTime.ShouldAdvance(tick))
			{
				if (orderManager.GameStarted && orderManager.LocalFrameNumber == 0)
					PerfHistory.Reset(); // Remove history that occurred whilst the new game was loading.

				using (var sample = new PerfSample("tick_time"))
				{
					orderManager.LastTickTime.AdvanceTickTime(tick);

					Sound.Tick();

					HangDiagnostics.Record("logic.network-receive.enter");
					Sync.RunUnsynced(world, orderManager.TickImmediate);
					HangDiagnostics.Record("logic.network-receive.exit");

					if (world == null)
					{
						if (orderManager.GameStarted)
							PerfHistory.Reset(); // Remove old history when a new game starts.
						return;
					}

					HangDiagnostics.Record("logic.order-readiness.enter");
					if (orderManager.TryTick())
					{
						HangDiagnostics.Record("logic.order-generator.enter");
						Sync.RunUnsynced(world, () => world.OrderGenerator.Tick(world));
						HangDiagnostics.Record("logic.world-tick.enter");

						using (new PerfSample("world_tick"))
							world.Tick();
						HangDiagnostics.Record("logic.world-tick.exit");

						PerfHistory.Tick();
					}

					// Wait until we have done our first world Tick before TickRendering
					if (orderManager.LocalFrameNumber > 0)
					{
						HangDiagnostics.Record("logic.world-render-state.enter");
						Sync.RunUnsynced(world, () => world.TickRender(worldRenderer));
						HangDiagnostics.Record("logic.world-render-state.exit");
					}
				}

				benchmark?.Tick(LocalTick);
			}
		}

		static void LogicTick()
		{
			HangDiagnostics.Record("logic.tick.enter");
			PumpLifecycleDispatch(ModData?.LifecycleDispatchLane);
			PerformDelayedActions();

			if (OrderManager.Connection is NetworkConnection nc && nc.ConnectionState != lastConnectionState)
			{
				lastConnectionState = nc.ConnectionState;
				ConnectionStateChanged(OrderManager, null, nc);
			}

			InnerLogicTick(OrderManager);
			if (worldRenderer != null && OrderManager.World != worldRenderer.World)
				InnerLogicTick(worldRenderer.World.OrderManager);
			HangDiagnostics.Record("logic.tick.exit");
		}

		public static void PerformDelayedActions()
		{
			delayedActions.PerformActions(RunTime);
		}

		internal static void PumpLifecycleDispatch(IGameLifecycleDispatchLane lane)
		{
			if (lane == null)
				return;
			if (!lane.IsCurrentThread)
				throw new InvalidOperationException("Lifecycle work must run on its owning game thread.");

			lane.PumpAvailable(LifecycleDispatchBatch);
		}

		internal static void DrainLifecycleDispatchUntil(
			IGameLifecycleDispatchLane lane, Task fence)
		{
			if (lane == null)
				throw new ArgumentNullException(nameof(lane));
			if (fence == null)
				throw new ArgumentNullException(nameof(fence));
			if (!lane.IsCurrentThread)
				throw new InvalidOperationException("Lifecycle work must drain on its owning game thread.");
			if (lane is not IGameLifecycleDispatchWaiter waiter)
				throw new InvalidOperationException("The lifecycle lane does not support drain waiting.");

			ExceptionDispatchInfo firstFailure = null;
			void CaptureFirst(Exception exception)
			{
				firstFailure ??= ExceptionDispatchInfo.Capture(exception);
			}

			while (true)
			{
				var processed = 0;
				try { processed = lane.PumpAvailable(LifecycleDispatchBatch); }
				catch (Exception exception) { CaptureFirst(exception); }

				bool closed;
				bool quiescent;
				try
				{
					closed = waiter.IsClosed;
					quiescent = waiter.IsQuiescent;
				}
				catch (Exception exception)
				{
					CaptureFirst(exception);
					break;
				}

				if (closed)
				{
					CaptureFirst(new ObjectDisposedException("lifecycle dispatch lane"));
					break;
				}

				if (fence.IsCompleted && quiescent)
					break;
				if (processed == 0 && quiescent)
				{
					IGameLifecycleDispatchWaitRegistration registration = null;
					try
					{
						registration = waiter.RegisterWait(fence);
						registration.Wait();
					}
					catch (Exception exception) { CaptureFirst(exception); }
					finally
					{
						if (registration != null)
							try { registration.Dispose(); }
							catch (Exception exception) { CaptureFirst(exception); }
					}
				}
			}

			if (fence.IsFaulted)
				CaptureFirst(fence.Exception.InnerExceptions[0]);
			else if (fence.IsCanceled)
				CaptureFirst(new TaskCanceledException(fence));

			firstFailure?.Throw();
		}

		public static void TakeScreenshot()
		{
			takeScreenshot = true;
		}

		static void RenderTick()
		{
			HangDiagnostics.Record("render.tick.enter");
			using (new PerfSample("render"))
			{
				++RenderFrame;

				// Prepare renderables (i.e. render voxels) before calling BeginFrame
				using (new PerfSample("render_prepare"))
				{
					HangDiagnostics.Record("render.prepare.enter");
					worldRenderer?.BeginFrame();

					// World rendering is disabled while the loading screen is displayed
					if (worldRenderer != null && !worldRenderer.World.IsLoadingGameSave)
					{
						worldRenderer.Viewport.Tick();
						worldRenderer.PrepareRenderables();
					}

					Ui.PrepareRenderables();
					worldRenderer?.EndFrame();
					HangDiagnostics.Record("render.prepare.exit");
				}

				// worldRenderer is null during the initial install/download screen
				// World rendering is disabled while the loading screen is displayed
				// Use worldRenderer.World instead of OrderManager.World to avoid a rendering mismatch while processing orders
				if (worldRenderer != null && !worldRenderer.World.IsLoadingGameSave)
				{
					Renderer.BeginWorld(worldRenderer.Viewport.Rectangle);
					Sound.SetListenerPosition(worldRenderer.Viewport.CenterPosition);
					using (new PerfSample("render_world"))
					{
						HangDiagnostics.Record("render.world-draw.enter");
						worldRenderer.Draw();
						HangDiagnostics.Record("render.world-draw.exit");
					}
				}

				using (new PerfSample("render_widgets"))
				{
					HangDiagnostics.Record("render.ui-draw.enter");
					Renderer.BeginUI();

					if (worldRenderer != null && !worldRenderer.World.IsLoadingGameSave)
						worldRenderer.DrawAnnotations();

					Ui.Draw();

					if (ModData != null && ModData.CursorProvider != null)
					{
						if (HideCursor)
							Cursor.SetCursor(null);
						else
						{
							Cursor.SetCursor(Ui.Root.GetCursorOuter(Viewport.LastMousePos) ?? "default");
							Cursor.Render(Renderer);
						}
					}

					HangDiagnostics.Record("render.ui-draw.exit");
				}

				using (new PerfSample("render_flip"))
				{
					HangDiagnostics.Record("render.present.enter");
					Renderer.EndFrame(new DefaultInputHandler(OrderManager.World));
					HangDiagnostics.Record("render.present.exit");

					if (worldRenderer != null)
					{
						var presentedWorld = worldRenderer.World;
						IosSmokeTestJournal.RecordReady(
							presentedWorld, presentedWorld.OrderManager.LocalFrameNumber,
							presentedWorld.WorldTick, RenderFrame, RunTime);
					}
				}

				if (takeScreenshot)
				{
					takeScreenshot = false;
					TakeScreenshotInner();
				}

				if (worldRenderer != null && !worldRenderer.World.IsLoadingGameSave)
					CaptureRequestedRenderAuditFrame();
			}

			PerfHistory.Items["render"].Tick();
			PerfHistory.Items["batches"].Tick();
			PerfHistory.Items["render_world"].Tick();
			PerfHistory.Items["render_widgets"].Tick();
			PerfHistory.Items["render_flip"].Tick();
			PerfHistory.Items["terrain_lighting"].Tick();
		}

		static void Loop()
		{
			// The game loop mainly does two things: logic updates and
			// drawing on the screen.
			// ---
			// We ideally want the logic to run every 'Timestep' ms and
			// rendering to be done at 'MaxFramerate', so 1000 / MaxFramerate ms.
			// Any additional free time is used in 'Sleep' so we don't
			// consume more CPU/GPU resources than necessary.
			// ---
			// In case logic or rendering takes more time than the ideal
			// and we're getting behind, we can skip rendering some frames
			// but there's a fail-safe minimum FPS to make sure the screen
			// gets updated at least that often.
			// ---
			// TODO: Separate world/UI rendering
			// It would be nice to separate the world rendering from the UI rendering
			// so that we can update the UI more often than the world. This would
			// help make the game playable (mouse/controls) even in low world
			// framerates.
			// It's not possible at the moment because the render buffer is cleared
			// before rendering and we don't keep the last rendered world buffer.

			// When the logic has fallen behind by this much, skip the pending
			// updates and start fresh.
			// For example, if we want to update logic every 10 ms but each loop
			// temporarily takes 100 ms, the 'nextLogic' timestamp will be too low
			// and the current timestamp ('now') will have moved on. Even if the
			// update time returns to normal, it will take a long time to catch up
			// (if ever).
			// This also means that the 'logicInterval' cannot be longer than this
			// value.
			const int MaxLogicTicksBehind = 250;

			// Try to maintain at least this many FPS during replays, even if it slows down logic.
			// However, if the user has enabled a framerate limit that is even lower
			// than this, then that limit will be used.
			const int MinReplayFps = 10;

			// Timestamps for when the next logic and rendering should run
			var nextLogic = RunTime;
			var nextRender = RunTime;
			var forcedNextRender = RunTime;
			var renderBeforeNextTick = false;
			HangDiagnostics.Resume();
			HangDiagnostics.Record("loop.started");

			while (state == RunStatus.Running)
			{
				HangDiagnostics.Record("loop.dispatch");
				var logicInterval = Ui.Timestep;
				var logicWorld = worldRenderer?.World;

				// ReplayTimestep = 0 means the replay is paused: we need to keep logicInterval as UI.Timestep to avoid breakage
				if (logicWorld != null && (!logicWorld.IsReplay || logicWorld.ReplayTimestep != 0))
					logicInterval = logicWorld == OrderManager.World ? OrderManager.SuggestedTimestep : logicWorld.Timestep;

				// Ideal time between screen updates
				var renderInterval = logicInterval;
				if (!Settings.Graphics.CapFramerateToGameFps)
				{
					var maxFramerate = Settings.Graphics.CapFramerate ? Settings.Graphics.MaxFramerate.Clamp(1, 1000) : 1000;
					renderInterval = 1000 / maxFramerate;
				}

				// Tick as fast as possible while restoring game saves, capping rendering at 5 FPS
				if (OrderManager.World != null && OrderManager.World.IsLoadingGameSave)
				{
					logicInterval = 1;
					renderInterval = 200;
				}

				var now = RunTime;

				// If the logic has fallen behind too much, skip it and catch up
				if (now - nextLogic > MaxLogicTicksBehind)
					nextLogic = now;

				// When's the next update (logic or render)
				var nextUpdate = Math.Min(nextLogic, nextRender);
				if (now >= nextUpdate)
				{
					var forceRender = renderBeforeNextTick || now >= forcedNextRender;

					if (now >= nextLogic && !renderBeforeNextTick)
					{
						nextLogic += logicInterval;

						LogicTick();

						// Force at least one render per tick during regular gameplay
						if (OrderManager.World != null && !OrderManager.World.IsLoadingGameSave && !OrderManager.World.IsReplay)
							renderBeforeNextTick = true;
					}

					var haveSomeTimeUntilNextLogic = now < nextLogic;
					var isTimeToRender = now >= nextRender;
					if (!Renderer.WindowIsSuspended && ((isTimeToRender && haveSomeTimeUntilNextLogic) || forceRender))
					{
						nextRender = now + renderInterval;

						// Pick the minimum allowed FPS (the lower between 'minReplayFPS'
						// and the user's max frame rate) and convert it to maximum time
						// allowed between screen updates.
						// We do this before rendering to include the time rendering takes
						// in this interval.
						var maxRenderInterval = Math.Max(1000 / MinReplayFps, renderInterval);
						forcedNextRender = now + maxRenderInterval;

						RenderTick();
						renderBeforeNextTick = false;
					}

					// Simulate a render tick if it was time to render but we skip actually rendering
					if (Renderer.WindowIsSuspended && isTimeToRender)
					{
						// Make sure that nextUpdate is set to a proper minimum interval
						nextRender = now + renderInterval;

						// Still process SDL events to allow a restore to come through
						Renderer.Window.PumpInput(new NullInputHandler());

						// Ensure that we still logic tick despite not rendering
						renderBeforeNextTick = false;
					}
				}
				else
					Thread.Sleep((int)(nextUpdate - now));
			}
		}

		static RunStatus Run()
		{
			if (Settings.Graphics.MaxFramerate < 1)
			{
				Settings.Graphics.MaxFramerate = new GraphicSettings().MaxFramerate;
				Settings.Graphics.CapFramerate = false;
			}

			try
			{
				Loop();
			}
			finally
			{
				HangDiagnostics.Stop();

				// Ensure that the active replay is properly saved
				ReleaseReplaySnapshot(OrderManager?.Connection as ReplayConnection);
				OrderManager?.Dispose();
			}

			worldRenderer?.Dispose();
			ModData.Dispose();
			ChromeProvider.Deinitialize();

			Sound.Dispose();
			Haptics.Dispose();
			Renderer.Dispose();

			OnQuit();

			return state;
		}

		static RunStatus RunProfiled()
		{
			if (Settings.Graphics.MaxFramerate < 1)
			{
				Settings.Graphics.MaxFramerate = new GraphicSettings().MaxFramerate;
				Settings.Graphics.CapFramerate = false;
			}

			Loop();
			return state;
		}

		static ExceptionDispatchInfo CleanupProfiledStartup(
			OwnedEngineStartupState startup, bool notifyQuit)
		{
			ExceptionDispatchInfo firstFailure = null;
			void Capture(ExceptionDispatchInfo failure)
			{
				firstFailure ??= failure;
			}

			void Cleanup(Action action)
			{
				try { action(); }
				catch (Exception exception)
				{
					firstFailure ??= ExceptionDispatchInfo.Capture(exception);
				}
			}

			Cleanup(HangDiagnostics.Stop);

			var ownedCursor = Cursor;
			Cursor = null;
			if (ownedCursor != null)
				Cleanup(ownedCursor.Dispose);

			Capture(TeardownCurrentModEpoch());

			var ownedMods = Mods;
			Mods = null;
			if (ownedMods != null)
				Cleanup(ownedMods.Dispose);

			Cleanup(startup.Dispose);

			Cleanup(ChromeProvider.Deinitialize);

			var ownedSound = Sound;
			Sound = null;
			if (ownedSound != null)
				Cleanup(ownedSound.Dispose);

			var ownedHaptics = Haptics;
			Haptics = null;
			if (ownedHaptics != null)
				Cleanup(ownedHaptics.Dispose);

			var ownedRenderer = Renderer;
			Renderer = null;
			if (ownedRenderer != null)
				Cleanup(ownedRenderer.Dispose);

			if (notifyQuit)
				Cleanup(OnQuit);

			Interlocked.CompareExchange(ref activeStartup, null, startup);

			return firstFailure;
		}

		public static void Exit()
		{
			state = RunStatus.Success;
		}

		public static void Disconnect()
		{
			ExceptionDispatchInfo firstFailure = null;
			void Cleanup(Action action)
			{
				try { action(); }
				catch (Exception exception)
				{
					firstFailure ??= ExceptionDispatchInfo.Capture(exception);
				}
			}

			Cleanup(() => ReleaseReplaySnapshot(
				OrderManager.Connection as ReplayConnection));
			Cleanup(() => OrderManager.World?.TraitDict.PrintReport());
			Cleanup(() => OrderManager.Dispose());
			Cleanup(CloseServer);
			Cleanup(JoinLocal);
			firstFailure?.Throw();
		}

		public static void CloseServer()
		{
			server?.Shutdown();
		}

		public static T CreateObject<T>(string name)
		{
			return ModData.ObjectCreator.CreateObject<T>(name);
		}

		public static ConnectionTarget CreateServer(ServerSettings settings)
		{
			var endpoints = new List<IPEndPoint>
			{
				new(IPAddress.IPv6Any, settings.ListenPort),
				new(IPAddress.Any, settings.ListenPort)
			};
			server = new Server.Server(endpoints, settings, ModData, ServerType.Multiplayer);

			return server.GetEndpointForLocalConnection();
		}

		public static ConnectionTarget CreateLocalServer(string map, bool isSkirmish = false)
		{
			var settings = new ServerSettings()
			{
				Name = "Skirmish Game",
				Map = map,
				AdvertiseOnline = false
			};

			// Always connect to local games using the same loopback connection
			// Exposing multiple endpoints introduces a race condition on the client's PlayerIndex (sometimes 0, sometimes 1)
			// This would break the Restart button, which relies on the PlayerIndex always being the same for local servers
			var endpoints = new List<IPEndPoint>
			{
				new(IPAddress.Loopback, 0)
			};
			server = new Server.Server(endpoints, settings, ModData, isSkirmish ? ServerType.Skirmish : ServerType.Local);

			return server.GetEndpointForLocalConnection();
		}

		public static bool IsCurrentWorld(World world)
		{
			return OrderManager != null && OrderManager.World == world && !world.Disposing;
		}

		public static bool SetClipboardText(string text)
		{
			return Renderer.Window.SetClipboardText(text);
		}

		public static void BenchmarkMode(string prefix)
		{
			benchmark = new Benchmark(prefix);
		}

		public static void BeginBenchmark()
		{
			benchmark?.Begin();
		}

		public static void LoadMap(string launchMap)
		{
			var orders = new List<Order>
			{
				Order.Command("option gamespeed default"),
				Order.Command($"state {Session.ClientState.Ready}")
			};

			var map = ModData.MapCache.SingleOrDefault(m => m.Uid == launchMap || Path.GetFileName(m.PackageName) == launchMap);
			if (map == null)
				throw new ArgumentException($"Could not find map '{launchMap}'.");

			CreateAndStartLocalServer(map.Uid, orders);
		}

		public static void FinishBenchmark()
		{
			if (benchmark?.Ready == true)
			{
				benchmark.Write();
				Exit();
			}
		}
	}

	public static class CurrentServerSettings
	{
		public static string Password;
		public static ConnectionTarget Target;
		public static ExternalMod ServerExternalMod;
	}
}
