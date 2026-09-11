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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using OpenRA.Network;

namespace OpenRA.Server
{
	sealed class Program
	{
		static void Main(string[] args)
		{
			try
			{
				Run(args);
			}
			catch
			{
				// Flush logs before rethrowing, i.e. allowing the exception to go unhandled.
				// try-finally won't work - an unhandled exception kills our process without running the finally block!
				Log.Dispose();
				throw;
			}
			finally
			{
				Log.Dispose();
			}
		}

		static void Run(string[] args)
		{
			var arguments = new Arguments(args);

			var engineDirArg = arguments.GetValue("Engine.EngineDir", null);
			if (!string.IsNullOrEmpty(engineDirArg))
				Platform.OverrideEngineDir(engineDirArg);

			var supportDirArg = arguments.GetValue("Engine.SupportDir", null);
			if (!string.IsNullOrEmpty(supportDirArg))
				Platform.OverrideSupportDir(supportDirArg);

			Game.InitializeEngineVersion();

			Log.AddChannel("debug", "dedicated-debug.log", true);
			Log.AddChannel("perf", "dedicated-perf.log", true);
			Log.AddChannel("server", "dedicated-server.log", true);
			Log.AddChannel("nat", "dedicated-nat.log", true);
			Log.AddChannel("geoip", "dedicated-geoip.log", true);

			// Special case handling of Game.Mod argument: if it matches a real filesystem path
			// then we use this to override the mod search path, and replace it with the mod id
			var modID = arguments.GetValue("Game.Mod", null);
			var explicitModPaths = Array.Empty<string>();
			if (modID != null && (File.Exists(modID) || Directory.Exists(modID)))
			{
				explicitModPaths = new[] { modID };
				modID = Path.GetFileNameWithoutExtension(modID);
			}

			if (modID == null)
				throw new InvalidOperationException("Game.Mod argument missing or mod could not be found.");

			// HACK: The engine code assumes that Game.Settings is set.
			// This isn't nearly as bad as ModData, but is still not very nice.
			Game.InitializeSettings(arguments);
			var settings = Game.Settings.Server;

			Nat.Initialize();

			var envModSearchPaths = Environment.GetEnvironmentVariable("MOD_SEARCH_PATHS");
			var modSearchPaths = !string.IsNullOrWhiteSpace(envModSearchPaths) ?
				FieldLoader.GetValue<string[]>("MOD_SEARCH_PATHS", envModSearchPaths) :
				new[] { Path.Combine(Platform.EngineDir, "mods") };

			var mods = new InstalledMods(modSearchPaths, explicitModPaths);

			if (settings.MaxPlayers < 1 || settings.MaxPlayers > 64)
				throw new ArgumentOutOfRangeException(nameof(settings.MaxPlayers), "Server.MaxPlayers must be between 1 and 64.");
			if (settings.IdleTimeoutSeconds < 0)
				throw new ArgumentOutOfRangeException(nameof(settings.IdleTimeoutSeconds), "Server.IdleTimeoutSeconds cannot be negative.");
			var onlineLobbyRegistrationCredential = Environment.GetEnvironmentVariable(
				OnlineLobbyRegistrationPolicy.CredentialEnvironmentVariable);
			OnlineLobbyRegistrationPolicy.Validate(settings, onlineLobbyRegistrationCredential);

			var endpoints = DedicatedServerRuntimePolicy.ResolveEndpoints(settings.ListenAddress, settings.ListenPort).ToList();
			var stopRequested = 0;
			Console.CancelKeyPress += (_, eventArgs) =>
			{
				eventArgs.Cancel = true;
				Interlocked.Exchange(ref stopRequested, 1);
			};
			using var terminateRegistration = PosixSignalRegistration.Create(
				PosixSignal.SIGTERM,
				context =>
				{
					context.Cancel = true;
					Interlocked.Exchange(ref stopRequested, 1);
				});

			WriteLineWithTimeStamp($"Starting NUKE HOUR dedicated server for mod: {modID}");
			// HACK: The engine code still assumes that Game.ModData is set.
			using var modData = Game.ModData = new ModData(mods[modID], mods, dedicatedServer: true);
			modData.MapCache.LoadPreviewImages = false;
			modData.MapCache.LoadMaps();
			WriteLineWithTimeStamp($"Runtime profile: {modData.RuntimeProfile.Serialize()}");
			WriteLineWithTimeStamp($"Runtime contract: {modData.RuntimeContract.Serialize()}");
			WriteLineWithTimeStamp($"Protocols: transport={ProtocolVersion.Handshake}; schema={ProtocolVersion.HandshakeSchema}; orders={ProtocolVersion.Orders}");

			WriteStatus(settings, modData, DedicatedServerOperationalState.Starting, 0, settings.Map);
			var server = new Server(endpoints, settings, modData, ServerType.Dedicated);
			WriteLineWithTimeStamp($"Listening on {settings.ListenAddress}:{settings.ListenPort}; map={server.LobbyInfo.GlobalSettings.Map}");
			using var lobbyRegistration = CreateOnlineLobbyRegistration(
				settings, onlineLobbyRegistrationCredential);

			GC.Collect();
			var gameStarted = false;
			var emptySince = Stopwatch.GetTimestamp();
			var lastState = DedicatedServerOperationalState.Starting;
			var nextLobbyUpdate = 0L;
			while (server.State != ServerState.ShuttingDown)
			{
				Thread.Sleep(250);
				var validatedClients = server.ValidatedConnectionCount;
				if (validatedClients > 0)
					emptySince = Stopwatch.GetTimestamp();
				if (server.State == ServerState.GameStarted)
					gameStarted = true;

				var state = DedicatedServerRuntimePolicy.ResolveState(server.State, gameStarted, validatedClients);
				if (state != lastState)
				{
					WriteLineWithTimeStamp($"Lifecycle: {state}");
					lastState = state;
				}

				WriteStatus(settings, modData, state, validatedClients, server.LobbyInfo.GlobalSettings.Map);
				var now = Stopwatch.GetTimestamp();
				if (lobbyRegistration != null && now >= nextLobbyUpdate)
				{
					var snapshot = CreateOnlineLobbySnapshot(
						settings, modData, state, server.LobbyInfo.GlobalSettings.Map, validatedClients);
					if (!lobbyRegistration.TryUpdateAsync(snapshot, CancellationToken.None).GetAwaiter().GetResult())
						WriteLineWithTimeStamp($"Online Lobby update unavailable: {lobbyRegistration.LastErrorCode}.");
					nextLobbyUpdate = now + settings.OnlineLobbyHeartbeatSeconds * Stopwatch.Frequency;
				}
				var terminationRequested = Volatile.Read(ref stopRequested) != 0;
				if (terminationRequested || DedicatedServerRuntimePolicy.ShouldExit(
					server.State, gameStarted, validatedClients,
					DedicatedServerRuntimePolicy.ElapsedSince(
						emptySince, Stopwatch.GetTimestamp(), Stopwatch.Frequency),
					settings.IdleTimeoutSeconds))
				{
					var reason = terminationRequested ? "termination requested" : gameStarted
						? "completed room is empty" : "empty lobby idle timeout";
					WriteLineWithTimeStamp($"Stopping dedicated server: {reason}.");
					WriteStatus(settings, modData,
						gameStarted ? DedicatedServerOperationalState.GameFinished : DedicatedServerOperationalState.Stopping,
						validatedClients, server.LobbyInfo.GlobalSettings.Map);
					server.Shutdown();
				}
			}

			if (!server.WaitForShutdown(TimeSpan.FromSeconds(30)))
				throw new TimeoutException("Dedicated server cleanup did not complete within 30 seconds.");

			if (lobbyRegistration != null)
				try
				{
					using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
					lobbyRegistration.UnregisterAsync(timeout.Token).GetAwaiter().GetResult();
				}
				catch
				{
					WriteLineWithTimeStamp("Online Lobby unregister was unavailable; the room will expire automatically.");
				}

			WriteStatus(settings, modData, DedicatedServerOperationalState.Stopped, 0, server.LobbyInfo.GlobalSettings.Map);
			WriteLineWithTimeStamp("Dedicated server stopped cleanly.");
		}

		static OnlineLobbyRegistrationClient CreateOnlineLobbyRegistration(
			ServerSettings settings, string registrationCredential)
		{
			if (string.IsNullOrWhiteSpace(settings.OnlineLobbyUrl))
				return null;

			return new OnlineLobbyRegistrationClient(
				new HttpClient { Timeout = TimeSpan.FromSeconds(5) },
				OnlineLobbyRegistrationPolicy.ParseBaseUri(settings.OnlineLobbyUrl),
				registrationCredential,
				settings.OnlineLobbyServerId);
		}

		static OnlineLobbyRoomSnapshot CreateOnlineLobbySnapshot(
			ServerSettings settings,
			ModData modData,
			DedicatedServerOperationalState state,
			string map,
			int validatedClients)
		{
			var publicPort = settings.OnlineLobbyPublicPort == 0
				? settings.ListenPort : settings.OnlineLobbyPublicPort;
			return new OnlineLobbyRoomSnapshot(
				settings.OnlineLobbyPublicEndpoint,
				publicPort,
				settings.Name,
				settings.OnlineLobbyRegion,
				state,
				map,
				modData.Manifest.Id,
				modData.Manifest.Metadata.CompatibilityOrVersion,
				validatedClients,
				settings.MaxPlayers,
				!string.IsNullOrEmpty(settings.Password),
				OnlineLobbyRegistrationPolicy.ShouldPublish(state),
				Game.EngineVersion ?? "Unknown",
				ProtocolVersion.HandshakeSchema,
				ProtocolVersion.Orders,
				modData.RuntimeContract.ResourceCapability);
		}

		static void WriteStatus(
			ServerSettings settings,
			ModData modData,
			DedicatedServerOperationalState state,
			int validatedClients,
			string map)
		{
			if (string.IsNullOrWhiteSpace(settings.StatusFile))
				return;

			var statusPath = Path.IsPathRooted(settings.StatusFile)
				? settings.StatusFile : Path.Combine(Platform.SupportDir, settings.StatusFile);
			var directory = Path.GetDirectoryName(statusPath);
			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);

			var document = DedicatedServerStatusDocument.Serialize(
				state, modData.RuntimeProfile.BuildVersion,
				Game.EngineVersion ?? "Unknown", modData.Manifest.Id,
				modData.RuntimeProfile.Platform, settings.ListenAddress,
				settings.ListenPort, map, validatedClients);
			var temporaryPath = statusPath + ".tmp";
			File.WriteAllText(temporaryPath, document + Environment.NewLine, new UTF8Encoding(false));
			File.Move(temporaryPath, statusPath, true);
		}

		static void WriteLineWithTimeStamp(string line)
		{
			Console.WriteLine($"[{DateTime.Now.ToString(Game.Settings.Server.TimestampFormat, CultureInfo.CurrentCulture)}] {line}");
		}
	}
}
