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
using System.Linq;
using System.Threading;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Server;
using OpenRA.Support;
using OpenRA.Traits;
using S = OpenRA.Server.Server;

namespace OpenRA.Mods.Common.Server
{
	public class LobbyCommands : ServerTrait, IInterpretCommand, INotifyServerStart, INotifyServerEmpty, IClientJoined, OpenRA.Server.ITick
	{
		[FluentReference]
		const string CustomRules = "notification-custom-rules";

		[FluentReference]
		const string OnlyHostStartGame = "notification-admin-start-game";

		[FluentReference]
		const string NoStartUntilRequiredSlotsFull = "notification-no-start-until-required-slots-full";

		[FluentReference]
		const string NoStartWithoutPlayers = "notification-no-start-without-players";

		[FluentReference]
		const string TwoHumansRequired = "notification-two-humans-required";

		[FluentReference]
		const string InsufficientEnabledSpawnPoints = "notification-insufficient-enabled-spawn-points";

		[FluentReference]
		const string MapNotReady = "notification-map-not-ready";

		[FluentReference("command")]
		const string MalformedCommand = "notification-malformed-command";

		[FluentReference]
		const string KickNone = "notification-kick-none";

		[FluentReference]
		const string NoKickSelf = "notification-kick-self";

		[FluentReference]
		const string NoKickGameStarted = "notification-no-kick-game-started";

		[FluentReference("admin", "player")]
		const string AdminKicked = "notification-admin-kicked";

		[FluentReference("player")]
		const string Kicked = "notification-kicked";

		[FluentReference("admin", "player")]
		const string TempBan = "notification-temp-ban";

		[FluentReference]
		const string NoTransferAdmin = "notification-admin-transfer-admin";

		[FluentReference]
		const string EmptySlot = "notification-empty-slot";

		[FluentReference("player", "name")]
		const string Nick = "notification-nick-changed";

		[FluentReference]
		const string StateUnchangedReady = "notification-state-unchanged-ready";

		[FluentReference("command")]
		const string StateUnchangedGameStarted = "notification-state-unchanged-game-started";

		[FluentReference("faction")]
		const string InvalidFactionSelected = "notification-invalid-faction-selected";

		[FluentReference]
		const string RequiresHost = "notification-requires-host";

		[FluentReference]
		const string InvalidBotSlot = "notification-invalid-bot-slot";

		[FluentReference]
		const string InvalidBotType = "notification-invalid-bot-type";

		[FluentReference]
		const string HostChangeMap = "notification-admin-change-map";

		[FluentReference]
		const string UnknownMap = "notification-unknown-map";

		[FluentReference]
		const string NotAdmin = "notification-admin-change-configuration";

		[FluentReference]
		const string InvalidConfigurationCommand = "notification-invalid-configuration-command";

		[FluentReference("option")]
		const string OptionLocked = "notification-option-locked";

		[FluentReference("player", "map")]
		const string ChangedMap = "notification-changed-map";

		[FluentReference]
		const string MapBotsDisabled = "notification-map-bots-disabled";

		[FluentReference]
		const string MapChangeNoSlot = "notification-map-change-no-slot";

		[FluentReference("player", "name", "value")]
		const string ValueChanged = "notification-option-changed";

		[FluentReference]
		const string AdminOption = "notification-admin-option";

		[FluentReference("raw")]
		const string NumberTeams = "notification-error-number-teams";

		[FluentReference]
		const string AdminClearSpawn = "notification-admin-clear-spawn";

		[FluentReference]
		const string SpawnOccupied = "notification-spawn-occupied";

		[FluentReference]
		const string SpawnLocked = "notification-spawn-locked";

		[FluentReference]
		const string AdminLobbyInfo = "notification-admin-lobby-info";

		[FluentReference]
		const string InvalidLobbyInfo = "notification-invalid-lobby-info";

		[FluentReference]
		const string AdminKick = "notification-admin-kick";

		[FluentReference]
		const string SlotClosed = "notification-slot-closed";

		[FluentReference("player")]
		const string NewAdmin = "notification-new-admin";

		[FluentReference]
		const string YouWereKicked = "notification-you-were-kicked";

		[FluentReference]
		const string VoteKickDisabled = "notification-vote-kick-disabled";

		const long ReadinessLogIntervalMilliseconds = 5000;

		readonly IDictionary<string, Func<S, Connection, Session.Client, string, bool>> commandHandlers;
		readonly ClientMapReadinessThrottle mapReadinessThrottle = new(250);
		readonly Stopwatch mapReadinessClock = Stopwatch.StartNew();
		int rejectedMapReadinessReports;
		long lastReadinessLogAt;

		public LobbyCommands()
		{
			commandHandlers = new Dictionary<string, Func<S, Connection, Session.Client, string, bool>>
			{
				{ "state", State },
				{ "map_status", MapReadiness },
				{ "startgame", StartGame },
				{ "startgame_safe", StartGameSafe },
				{ "slot", Slot },
				{ "slot_close", SlotClose },
				{ "slot_open", SlotOpen },
				{ "slot_bot", SlotBot },
				{ "map", Map },
				{ "option", Option },
				{ "reset_options", ResetOptions },
				{ "assignteams", AssignTeams },
				{ "kick", Kick },
				{ "vote_kick", VoteKick },
				{ "make_admin", MakeAdmin },
				{ "name", Name },
				{ "faction", Faction },
				{ "team", Team },
				{ "handicap", Handicap },
				{ "spawn", Spawn },
				{ "clear_spawn", ClearPlayerSpawn },
				{ "color", PlayerColor },
				{ "sync_lobby", SyncLobby }
			};
		}

		static bool ValidateSlotCommand(S server, Connection conn, Session.Client client, string arg, bool requiresHost)
		{
			lock (server.LobbyInfo)
			{
				if (!server.LobbyInfo.Slots.ContainsKey(arg))
				{
					Log.Write("server", $"Invalid slot: {arg}");
					return false;
				}

				if (requiresHost && !client.IsAdmin)
				{
					server.SendFluentMessageTo(conn, RequiresHost);
					return false;
				}

				return true;
			}
		}

		public static string CommandName(string command)
		{
			if (string.IsNullOrWhiteSpace(command))
				return string.Empty;

			var separator = command.IndexOf(' ');
			return separator < 0 ? command : command[..separator];
		}

		public static bool IsAllowedWhileReady(string commandName)
		{
			return commandName == "state" || commandName == "startgame" ||
				commandName == "startgame_safe" || commandName == "map_status";
		}

		public static bool ValidateCommand(S server, Connection conn, Session.Client client, string command)
		{
			lock (server.LobbyInfo)
			{
				// Kick command is always valid for the host
				if (command.StartsWith("kick ", StringComparison.Ordinal) || command.StartsWith("vote_kick ", StringComparison.Ordinal))
					return true;

				if (server.State == ServerState.GameStarted)
				{
					server.SendFluentMessageTo(conn, StateUnchangedGameStarted, new object[] { "command", command });
					return false;
				}
				else if (client.State == Session.ClientState.Ready && !IsAllowedWhileReady(CommandName(command)))
				{
					server.SendFluentMessageTo(conn, StateUnchangedReady);
					return false;
				}

				return true;
			}
		}

		public bool InterpretCommand(S server, Connection conn, Session.Client client, string cmd)
		{
			if (server == null || conn == null || client == null || !ValidateCommand(server, conn, client, cmd))
				return false;

			var cmdName = cmd.Split(' ').First();
			var cmdValue = cmd.Split(' ').Skip(1).JoinWith(" ");

			if (!commandHandlers.TryGetValue(cmdName, out var a))
				return false;

			return a(server, conn, client, cmdValue);
		}

		public static bool TryParseMapReadiness(
			string value, string currentMapUid, out ClientMapReadinessReport report)
		{
			return ClientMapReadinessReport.TryParse($"map_status {value}", out report) &&
				report.IsValidFor(currentMapUid);
		}

		public static ClientMapReadinessApplyResult ApplyMapReadiness(
			Session.Client client, string currentMapUid, string value, out ClientMapReadinessUpdate update)
		{
			update = null;
			if (!TryParseMapReadiness(value, currentMapUid, out var report))
				return ClientMapReadinessApplyResult.Rejected;

			var result = ClientMapReadinessState.Apply(client, currentMapUid, report);
			if (result == ClientMapReadinessApplyResult.Rejected ||
				result == ClientMapReadinessApplyResult.Unchanged)
				return result;

			update = new ClientMapReadinessUpdate
			{
				ClientIndex = client.Index,
				MapUid = client.MapUid,
				Phase = client.MapPhase,
				Progress = client.MapProgress,
				ClientState = client.State
			};

			return result;
		}

		public static bool IsClientStateTransitionAllowed(
			bool requireMapReadiness, Session.Client client, string currentMapUid, Session.ClientState requestedState)
		{
			if (client == null || (requestedState != Session.ClientState.NotReady &&
				requestedState != Session.ClientState.Ready))
				return false;

			return !requireMapReadiness || client.IsMapReadyFor(currentMapUid);
		}

		public static void InitializeClientMapReadiness(Session.Client client, string currentMapUid)
		{
			if (client == null)
				throw new ArgumentNullException(nameof(client));

			if (client.Bot == null)
				ClientMapReadinessState.Reset(client, currentMapUid);
		}

		public static bool ResetClientMapReadinessForMapChange(
			IEnumerable<Session.Client> clients, string previousMapUid, string currentMapUid)
		{
			if (clients == null)
				throw new ArgumentNullException(nameof(clients));
			if (string.Equals(previousMapUid, currentMapUid, StringComparison.Ordinal))
				return false;

			var humanClients = clients.Where(client => client.Bot == null).ToArray();
			if (!new ClientMapReadinessReport(currentMapUid, Session.ClientMapPhase.Unknown, -1)
				.IsValidFor(currentMapUid))
				throw new ArgumentException("Map UID must be a nonempty single token.", nameof(currentMapUid));

			foreach (var client in humanClients)
				ClientMapReadinessState.Reset(client, currentMapUid);

			return true;
		}

		public static IReadOnlyList<int> DisplacedHumanClientIndexes(
			IEnumerable<Session.Client> clients, bool mapUidChanged)
		{
			if (clients == null)
				throw new ArgumentNullException(nameof(clients));

			return mapUidChanged ? clients
				.Where(client => client.Bot == null && client.Slot == null)
				.Select(client => client.Index)
				.ToArray() : Array.Empty<int>();
		}

		static ClientMapReadinessUpdate ReadinessSnapshot(Session.Client client)
		{
			return new ClientMapReadinessUpdate
			{
				ClientIndex = client.Index,
				MapUid = client.MapUid,
				Phase = client.MapPhase,
				Progress = client.MapProgress,
				ClientState = client.State
			};
		}

		void SubmitReadinessSnapshot(Session.Client client)
		{
			if (client?.Bot == null && !string.IsNullOrEmpty(client.MapUid))
				mapReadinessThrottle.Submit(ReadinessSnapshot(client), mapReadinessClock.ElapsedMilliseconds);
		}

		void MarkClientsNotReadyForConfiguration(S server)
		{
			var currentMapUid = server.LobbyInfo.GlobalSettings.Map;
			foreach (var client in server.LobbyInfo.Clients)
			{
				client.State = !server.IsMultiplayer || client.Bot != null || client.IsMapReadyFor(currentMapUid) ?
					Session.ClientState.NotReady : Session.ClientState.Invalid;
				SubmitReadinessSnapshot(client);
			}
		}

		bool MapReadiness(S server, Connection conn, Session.Client client, string value)
		{
			lock (server.LobbyInfo)
			{
				var result = ApplyMapReadiness(client, server.LobbyInfo.GlobalSettings.Map, value, out var update);
				if (result == ClientMapReadinessApplyResult.Rejected)
				{
					rejectedMapReadinessReports++;
					return true;
				}

				if (update != null)
					mapReadinessThrottle.Submit(update, mapReadinessClock.ElapsedMilliseconds);

				return true;
			}
		}

		static LobbyStartAvailability EvaluateStartAvailability(
			S server, LobbyStartMode mode, bool requesterIsAdmin)
		{
			return LobbyStartAvailability.Evaluate(new LobbyStartInputs
			{
				LobbyInfo = server.LobbyInfo,
				Mode = mode,
				RequesterIsAdmin = requesterIsAdmin,
				RequireCurrentMapReadiness = server.IsMultiplayer,
				InsufficientEnabledSpawnPoints = LobbyUtils.InsufficientEnabledSpawnPoints(
					server.Map, server.LobbyInfo)
			});
		}

		static void SendStartBlocker(S server, Connection conn, LobbyStartAvailability availability)
		{
			switch (availability.Reason)
			{
				case Session.LobbyStartBlockReason.RequesterNotAdmin:
					server.SendFluentMessageTo(conn, OnlyHostStartGame);
					break;
				case Session.LobbyStartBlockReason.RequiredSlotEmpty:
					server.SendFluentMessageTo(conn, NoStartUntilRequiredSlotsFull);
					break;
				case Session.LobbyStartBlockReason.NoPlayers:
					server.SendOrderTo(conn, "Message", NoStartWithoutPlayers);
					break;
				case Session.LobbyStartBlockReason.InsufficientHumans:
					server.SendFluentMessageTo(conn, TwoHumansRequired);
					break;
				case Session.LobbyStartBlockReason.InsufficientSpawnPoints:
					server.SendFluentMessageTo(conn, InsufficientEnabledSpawnPoints);
					break;
				default:
					server.SendFluentMessageTo(conn, MapNotReady);
					break;
			}
		}

		static void CheckAutoStart(S server)
		{
			lock (server.LobbyInfo)
			{
				if (!EvaluateStartAvailability(server, LobbyStartMode.Automatic, true).CanStart)
					return;

				server.StartGame();
			}
		}

		bool State(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				if (!Enum<Session.ClientState>.TryParse(s, false, out var state) ||
					state.ToString() != s || !IsClientStateTransitionAllowed(
						server.IsMultiplayer, client, server.LobbyInfo.GlobalSettings.Map, state))
				{
					server.SendFluentMessageTo(conn, MalformedCommand, new object[] { "command", "state" });

					return true;
				}

				client.State = state;
				SubmitReadinessSnapshot(client);
				Log.Write("server", $"Player @{conn.EndPoint} is {client.State}");

				server.SyncLobbyClients();
				CheckAutoStart(server);

				return true;
			}
		}

		static bool StartGame(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				var availability = EvaluateStartAvailability(
					server, LobbyStartMode.ForceConfirmed, client.IsAdmin);
				if (!availability.CanStart)
				{
					SendStartBlocker(server, conn, availability);
					return true;
				}

				server.StartGame();

				return true;
			}
		}

		static bool StartGameSafe(S server, Connection conn, Session.Client client, string value)
		{
			lock (server.LobbyInfo)
			{
				var currentMapUid = server.LobbyInfo.GlobalSettings.Map;
				if (!LobbySafeStartRequest.TryParse(value, out var request))
				{
					server.SendFluentMessageTo(conn, MalformedCommand,
						new object[] { "command", "startgame_safe" });
					return true;
				}

				if (!client.IsAdmin)
				{
					server.SendOrderTo(conn, "SafeStartRejected", new LobbyStartRejection(
						Session.LobbyStartBlockReason.RequesterNotAdmin,
						-1,
						Session.ClientMapPhase.Unknown,
						-1,
						request.ExpectedMapUid,
						request.RequestId).Serialize());
					return true;
				}

				if (!string.Equals(request.ExpectedMapUid, currentMapUid, StringComparison.Ordinal))
				{
					Log.Write("server", $"[NET] Reject start: {NetworkCompatibilityReason.MapMismatch.Code()}");
					server.SendOrderTo(conn, "SafeStartRejected", new LobbyStartRejection(
						Session.LobbyStartBlockReason.StaleMap,
						-1,
						Session.ClientMapPhase.Unknown,
						-1,
						request.ExpectedMapUid,
						request.RequestId).Serialize());
					return true;
				}

				var availability = EvaluateStartAvailability(server, LobbyStartMode.SafeDirect, true);
				if (!availability.CanStart)
				{
					server.SendOrderTo(conn, "SafeStartRejected", availability.ToRejection(request).Serialize());
					return true;
				}

				server.StartGame();
				return true;
			}
		}

		static bool Slot(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				if (!server.LobbyInfo.Slots.TryGetValue(s, out var slot))
				{
					Log.Write("server", $"Invalid slot: {s}");
					return false;
				}

				if (slot.Closed || server.LobbyInfo.ClientInSlot(s) != null)
					return false;

				// If the previous slot had a locked spawn then we must not carry that to the new slot
				var oldSlot = client.Slot != null ? server.LobbyInfo.Slots[client.Slot] : null;
				if (oldSlot != null && oldSlot.LockSpawn)
					client.SpawnPoint = 0;

				client.Slot = s;
				S.SyncClientToPlayerReference(client, server.Map.Players.Players[s]);

				if (!slot.LockColor)
					client.PreferredColor = client.Color = SanitizePlayerColor(server, client.Color, client.Index, conn);

				server.SyncLobbyClients();
				CheckAutoStart(server);

				return true;
			}
		}

		static bool SlotClose(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				if (!ValidateSlotCommand(server, conn, client, s, true))
					return false;

				// kick any player that's in the slot
				var occupant = server.LobbyInfo.ClientInSlot(s);
				if (occupant != null)
				{
					if (occupant.Bot != null)
					{
						server.LobbyInfo.Clients.Remove(occupant);
						server.SyncLobbyClients();
					}
					else
					{
						var occupantConn = server.Conns.FirstOrDefault(c => c.PlayerIndex == occupant.Index);
						if (occupantConn != null)
						{
							server.SendOrderTo(conn, "ServerError", SlotClosed);
							server.DropClient(occupantConn);
						}
					}
				}

				server.LobbyInfo.Slots[s].Closed = true;
				server.SyncLobbySlots();

				return true;
			}
		}

		static bool SlotOpen(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				if (!ValidateSlotCommand(server, conn, client, s, true))
					return false;

				var slot = server.LobbyInfo.Slots[s];
				slot.Closed = false;
				server.SyncLobbySlots();

				// Slot may have a bot in it
				var occupant = server.LobbyInfo.ClientInSlot(s);
				if (occupant != null && occupant.Bot != null)
					server.LobbyInfo.Clients.Remove(occupant);

				server.SyncLobbyClients();

				return true;
			}
		}

		static bool SlotBot(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				var parts = s.Split(' ');
				if (parts.Length < 3)
				{
					server.SendFluentMessageTo(conn, MalformedCommand, new object[] { "command", "slot_bot" });
					return true;
				}

				if (!ValidateSlotCommand(server, conn, client, parts[0], true))
					return false;

				var slot = server.LobbyInfo.Slots[parts[0]];
				var bot = server.LobbyInfo.ClientInSlot(parts[0]);
				if (!Exts.TryParseInt32Invariant(parts[1], out var controllerClientIndex))
				{
					Log.Write("server", $"Invalid bot controller client index: {parts[1]}");
					return false;
				}

				// Invalid slot
				if (bot != null && bot.Bot == null)
				{
					server.SendFluentMessageTo(conn, InvalidBotSlot);
					return true;
				}

				var botType = parts[2];
				var botInfo = server.Map.PlayerActorInfo.TraitInfos<IBotInfo>()
					.FirstOrDefault(b => b.Type == botType);

				if (botInfo == null)
				{
					server.SendFluentMessageTo(conn, InvalidBotType);
					return true;
				}

				slot.Closed = false;
				if (bot == null)
				{
					// Create a new bot
					bot = new Session.Client()
					{
						Index = server.ChooseFreePlayerIndex(),
						Name = botInfo.Name,
						Bot = botType,
						Slot = parts[0],
						Faction = "Random",
						SpawnPoint = 0,
						Team = 0,
						Handicap = 0,
						State = Session.ClientState.NotReady,
						BotControllerClientIndex = controllerClientIndex
					};

					// Pick a random color for the bot
					var colorManager = server.ModData.DefaultRules.Actors[SystemActors.World].TraitInfo<IColorPickerManagerInfo>();
					var terrainColors = server.ModData.DefaultTerrainInfo[server.Map.TileSet].RestrictedPlayerColors.ToList();
					var playerColors = server.LobbyInfo.Clients.Select(c => c.Color)
						.Concat(server.Map.Players.Players.Values.Select(p => p.Color)).ToList();

					bot.Color = bot.PreferredColor = colorManager.RandomPresetColor(server.Random, terrainColors, playerColors);

					server.LobbyInfo.Clients.Add(bot);
				}
				else
				{
					// Change the type of the existing bot
					bot.Name = botInfo.Name;
					bot.Bot = botType;
				}

				S.SyncClientToPlayerReference(bot, server.Map.Players.Players[parts[0]]);
				server.SyncLobbyClients();
				server.SyncLobbySlots();

				return true;
			}
		}

		bool Map(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				if (!client.IsAdmin)
				{
					server.SendFluentMessageTo(conn, HostChangeMap);
					return true;
				}

				if (server.MapPool != null && !server.MapPool.Contains(s))
				{
					QueryFailed();
					return true;
				}

				var lastMap = server.LobbyInfo.GlobalSettings.Map;
				void SelectMap(MapPreview map)
				{
					lock (server.LobbyInfo)
					{
						// Make sure the map hasn't changed in the meantime
						if (server.LobbyInfo.GlobalSettings.Map != lastMap)
							return;

						var mapUidChanged = !string.Equals(lastMap, map.Uid, StringComparison.Ordinal);
						server.LobbyInfo.GlobalSettings.Map = map.Uid;

						var oldSlots = server.LobbyInfo.Slots.Keys.ToArray();
						server.Map = server.ModData.MapCache[server.LobbyInfo.GlobalSettings.Map];
						server.LobbyInfo.GlobalSettings.MapStatus = server.MapStatusCache[server.Map];

						server.LobbyInfo.Slots = server.Map.Players.Players
							.Select(p => MakeSlotFromPlayerReference(p.Value))
							.Where(ss => ss != null)
							.ToDictionary(ss => ss.PlayerReference, ss => ss);

						LoadMapSettings(server, server.LobbyInfo.GlobalSettings, server.Map);

						// Reset client states
						var selectableFactions = server.Map.WorldActorInfo.TraitInfos<FactionInfo>()
							.Where(f => f.Selectable)
							.Select(f => f.InternalName)
							.ToList();

						if (ResetClientMapReadinessForMapChange(
							server.LobbyInfo.Clients, lastMap, map.Uid))
							mapReadinessThrottle.Reset();

						foreach (var c in server.LobbyInfo.Clients)
						{
							c.Faction = SanitizePlayerFaction(server, c.Faction, selectableFactions);
							if (mapUidChanged && c.Bot != null)
								c.State = Session.ClientState.Invalid;
						}

						// Reassign players into new slots based on their old slots:
						//  - Observers remain as observers
						//  - Players who now lack a slot are made observers
						//  - Bots who now lack a slot are dropped
						//  - Bots who are not defined in the map rules are dropped
						var botTypes = server.Map.PlayerActorInfo.TraitInfos<IBotInfo>().Select(t => t.Type);
						var slots = server.LobbyInfo.Slots.Keys.ToArray();
						var i = 0;
						foreach (var os in oldSlots)
						{
							var c = server.LobbyInfo.ClientInSlot(os);
							if (c == null)
								continue;

							c.SpawnPoint = 0;
							c.Slot = i < slots.Length ? slots[i++] : null;
							if (c.Slot != null)
							{
								// Remove Bot from slot if slot forbids bots
								if (c.Bot != null && (!server.Map.Players.Players[c.Slot].AllowBots || !botTypes.Contains(c.Bot)))
									server.LobbyInfo.Clients.Remove(c);
								S.SyncClientToPlayerReference(c, server.Map.Players.Players[c.Slot]);
							}
							else if (c.Bot != null)
								server.LobbyInfo.Clients.Remove(c);
							else
								c.Color = Color.White;
						}

						var displacedClientIndexes = DisplacedHumanClientIndexes(
							server.LobbyInfo.Clients, mapUidChanged).ToHashSet();
						var displacedConnections = server.Conns
							.Where(displaced => displacedClientIndexes.Contains(displaced.PlayerIndex))
							.ToArray();
						foreach (var displaced in displacedConnections)
						{
							server.SendOrderTo(displaced, "ServerError", MapChangeNoSlot);
							mapReadinessThrottle.Remove(displaced.PlayerIndex);
							server.DropClient(displaced);
						}

						// Validate if color is allowed and get an alternative if it isn't
						foreach (var c in server.LobbyInfo.Clients)
							if (c.Slot != null && !server.LobbyInfo.Slots[c.Slot].LockColor)
								c.Color = c.PreferredColor = SanitizePlayerColor(server, c.Color, c.Index, conn);

						server.LobbyInfo.DisabledSpawnPoints.Clear();

						server.SyncLobbyInfo();

						server.SendFluentMessage(ChangedMap, "player", client.Name, "map", server.Map.Title);

						if ((server.LobbyInfo.GlobalSettings.MapStatus & Session.MapStatus.UnsafeCustomRules) != 0)
							server.SendFluentMessage(CustomRules);

						if (!server.LobbyInfo.GlobalSettings.EnableSingleplayer)
							server.SendFluentMessage(TwoHumansRequired);
						else if (server.Map.Players.Players.Where(p => p.Value.Playable).All(p => !p.Value.AllowBots))
							server.SendFluentMessage(MapBotsDisabled);

						var briefing = MissionBriefingOrDefault(server);
						if (briefing != null)
							server.SendMessage(briefing);
					}
				}

				var m = server.ModData.MapCache[s];
				if (m.Status == MapStatus.Available || m.Status == MapStatus.DownloadAvailable)
					SelectMap(m);
				else
					QueryFailed();

				return true;
			}

			void QueryFailed() => server.SendFluentMessageTo(conn, UnknownMap);
		}

		bool Option(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				if (!client.IsAdmin)
				{
					server.SendFluentMessageTo(conn, NotAdmin);
					return true;
				}

				var allOptions = server.Map.PlayerActorInfo.TraitInfos<ILobbyOptions>()
					.Concat(server.Map.WorldActorInfo.TraitInfos<ILobbyOptions>())
					.SelectMany(t => t.LobbyOptions(server.Map));

				// Overwrite keys with duplicate ids
				var options = new Dictionary<string, LobbyOption>();
				foreach (var o in allOptions)
					options[o.Id] = o;

				var split = s.Split(' ');
				if (split.Length < 2 || !options.TryGetValue(split[0], out var option) ||
					!option.Values.ContainsKey(split[1]))
				{
					server.SendFluentMessageTo(conn, InvalidConfigurationCommand);
					return true;
				}

				if (option.IsLocked)
				{
					server.SendFluentMessageTo(conn, OptionLocked, new object[] { "option", option.Name });
					return true;
				}

				var oo = server.LobbyInfo.GlobalSettings.LobbyOptions[option.Id];
				if (oo.Value == split[1])
					return true;

				if (!option.Values.ContainsKey(split[1]))
				{
					server.SendFluentMessageTo(conn, InvalidConfigurationCommand);
					return true;
				}

				oo.Value = oo.PreferredValue = split[1];

				server.SyncLobbyGlobalSettings();
				server.SendFluentMessage(ValueChanged, "player", client.Name, "name", option.Name, "value", option.Label(split[1]));

				MarkClientsNotReadyForConfiguration(server);

				server.SyncLobbyClients();

				return true;
			}
		}

		bool ResetOptions(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				if (!client.IsAdmin)
				{
					server.SendFluentMessageTo(conn, NotAdmin);
					return true;
				}

				var allOptions = server.Map.PlayerActorInfo.TraitInfos<ILobbyOptions>()
					.Concat(server.Map.WorldActorInfo.TraitInfos<ILobbyOptions>())
					.SelectMany(t => t.LobbyOptions(server.Map));

				var options = new Dictionary<string, Session.LobbyOptionState>();
				foreach (var o in allOptions)
				{
					if (o.DefaultValue != server.LobbyInfo.GlobalSettings.LobbyOptions[o.Id].Value)
						server.SendFluentMessage(ValueChanged,
							"player", client.Name,
							"name", o.Name,
							"value", o.Label(o.DefaultValue));

					options[o.Id] = new Session.LobbyOptionState
					{
						IsLocked = o.IsLocked,
						Value = o.DefaultValue,
						PreferredValue = o.DefaultValue
					};
				}

				server.LobbyInfo.GlobalSettings.LobbyOptions = options;
				server.SyncLobbyGlobalSettings();

				MarkClientsNotReadyForConfiguration(server);

				server.SyncLobbyClients();

				return true;
			}
		}

		static bool AssignTeams(S server, Connection conn, Session.Client client, string raw)
		{
			lock (server.LobbyInfo)
			{
				if (!client.IsAdmin)
				{
					server.SendFluentMessageTo(conn, AdminOption);
					return true;
				}

				if (!Exts.TryParseInt32Invariant(raw, out var teamCount))
				{
					server.SendFluentMessageTo(conn, NumberTeams, new object[] { "raw", raw });
					return true;
				}

				var maxTeams = (server.LobbyInfo.Clients.Count(c => c.Slot != null) + 1) / 2;
				teamCount = teamCount.Clamp(0, maxTeams);
				var clients = server.LobbyInfo.Slots
					.Select(slot => server.LobbyInfo.ClientInSlot(slot.Key))
					.Where(c => c != null && !server.LobbyInfo.Slots[c.Slot].LockTeam)
					.ToList();

				var assigned = 0;
				var clientCount = clients.Count;
				foreach (var player in clients)
				{
					// Free for all
					if (teamCount == 0)
						player.Team = 0;

					// Humans vs Bots
					else if (teamCount == 1)
						player.Team = player.Bot == null ? 1 : 2;
					else
						player.Team = assigned++ * teamCount / clientCount + 1;
				}

				server.SyncLobbyClients();

				return true;
			}
		}

		static bool Kick(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				if (!client.IsAdmin)
				{
					server.SendFluentMessageTo(conn, AdminKick);
					return true;
				}

				var split = s.Split(' ');
				if (split.Length < 2)
				{
					server.SendFluentMessageTo(conn, MalformedCommand, new object[] { "command", "kick" });
					return true;
				}

				var kickConn = Exts.TryParseInt32Invariant(split[0], out var kickClientID)
					? server.Conns.SingleOrDefault(c => server.GetClient(c)?.Index == kickClientID) : null;

				if (kickConn == null)
				{
					server.SendFluentMessageTo(conn, KickNone);
					return true;
				}

				var kickClient = server.GetClient(kickConn);
				if (client == kickClient)
				{
					server.SendFluentMessageTo(conn, NoKickSelf);
					return true;
				}

				if (server.State == ServerState.GameStarted && !kickClient.IsObserver && !server.HasClientWonOrLost(kickClient))
				{
					server.SendFluentMessageTo(conn, NoKickGameStarted);
					return true;
				}

				Log.Write("server", $"Kicking client {kickClientID}.");
				server.SendFluentMessage(AdminKicked, "admin", client.Name, "player", kickClient.Name);
				server.SendOrderTo(kickConn, "ServerError", YouWereKicked);
				server.DropClient(kickConn);

				if (bool.TryParse(split[1], out var tempBan) && tempBan)
				{
					Log.Write("server", $"Temporarily banning client {kickClientID} ({kickClient.IPAddress}).");
					server.SendFluentMessage(TempBan, "admin", client.Name, "player", kickClient.Name);
					server.TempBans.Add(kickClient.IPAddress);
				}

				server.SyncLobbyClients();
				server.SyncLobbySlots();

				return true;
			}
		}

		static bool VoteKick(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				var split = s.Split(' ');
				if (split.Length != 2)
				{
					server.SendFluentMessageTo(conn, MalformedCommand, new object[] { "command", "vote_kick" });
					return true;
				}

				if (!server.Settings.EnableVoteKick)
				{
					server.SendFluentMessageTo(conn, VoteKickDisabled);
					return true;
				}

				var kickConn = Exts.TryParseInt32Invariant(split[0], out var kickClientID)
					? server.Conns.SingleOrDefault(c => server.GetClient(c)?.Index == kickClientID) : null;

				if (kickConn == null)
				{
					server.SendFluentMessageTo(conn, KickNone);
					return true;
				}

				var kickClient = server.GetClient(kickConn);
				if (client == kickClient)
				{
					server.SendFluentMessageTo(conn, NoKickSelf);
					return true;
				}

				if (!bool.TryParse(split[1], out var vote))
				{
					server.SendFluentMessageTo(conn, MalformedCommand, new object[] { "command", "vote_kick" });
					return true;
				}

				if (server.VoteKickTracker.VoteKick(conn, client, kickConn, kickClient, kickClientID, vote))
				{
					Log.Write("server", $"Kicking client {kickClientID}.");
					server.SendFluentMessage(Kicked, "player", kickClient.Name);
					server.SendOrderTo(kickConn, "ServerError", YouWereKicked);
					server.DropClient(kickConn);

					server.SyncLobbyClients();
					server.SyncLobbySlots();
				}

				return true;
			}
		}

		void OpenRA.Server.ITick.Tick(S server)
		{
			server.VoteKickTracker.Tick();
			var now = mapReadinessClock.ElapsedMilliseconds;
			lock (server.LobbyInfo)
			{
				mapReadinessThrottle.Retain(server.LobbyInfo.Clients
					.Where(client => client.Bot == null)
					.Select(client => client.Index));

				foreach (var pending in mapReadinessThrottle.Drain(now))
				{
					var client = server.LobbyInfo.ClientWithIndex(pending.ClientIndex);
					if (client?.Bot == null && client.MapUid == server.LobbyInfo.GlobalSettings.Map)
						server.SyncClientMapReadiness(ReadinessSnapshot(client));
				}

				if (rejectedMapReadinessReports > 0 && now - lastReadinessLogAt >= ReadinessLogIntervalMilliseconds)
				{
					Log.Write("server", $"Rejected {rejectedMapReadinessReports} invalid map readiness reports.");
					rejectedMapReadinessReports = 0;
					lastReadinessLogAt = now;
				}
			}
		}

		static bool MakeAdmin(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				if (!client.IsAdmin)
				{
					server.SendFluentMessageTo(conn, NoTransferAdmin);
					return true;
				}

				var newAdminConn = Exts.TryParseInt32Invariant(s, out var newAdminId)
					? server.Conns.SingleOrDefault(c => server.GetClient(c)?.Index == newAdminId) : null;

				if (newAdminConn == null)
				{
					server.SendFluentMessageTo(conn, EmptySlot);
					return true;
				}

				var newAdminClient = server.GetClient(newAdminConn);
				client.IsAdmin = false;
				newAdminClient.IsAdmin = true;

				var bots = server.LobbyInfo.Slots
					.Select(slot => server.LobbyInfo.ClientInSlot(slot.Key))
					.Where(c => c != null && c.Bot != null);
				foreach (var b in bots)
					b.BotControllerClientIndex = newAdminId;

				server.SendFluentMessage(NewAdmin, "player", newAdminClient.Name);
				Log.Write("server", $"{newAdminClient.Name} is now the admin.");
				server.SyncLobbyClients();

				return true;
			}
		}

		static bool Name(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				var sanitizedName = Settings.SanitizedPlayerName(s);
				if (sanitizedName == client.Name)
					return true;

				Log.Write("server", $"Player@{conn.EndPoint} is now known as {sanitizedName}.");
				server.SendFluentMessage(Nick, "player", client.Name, "name", sanitizedName);
				client.Name = sanitizedName;
				server.SyncLobbyClients();

				return true;
			}
		}

		static bool Faction(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				var parts = s.Split(' ');
				var targetClient = server.LobbyInfo.ClientWithIndex(Exts.ParseInt32Invariant(parts[0]));

				// Only the host can change other client's info
				if (targetClient.Index != client.Index && !client.IsAdmin)
					return true;

				// Map has disabled faction changes
				if (server.LobbyInfo.Slots[targetClient.Slot].LockFaction)
					return true;

				var faction = parts[1];
				var isValidFaction = server.Map.WorldActorInfo.TraitInfos<FactionInfo>()
					.Any(f => f.Selectable && f.InternalName == client.Faction);

				if (!isValidFaction)
				{
					server.SendFluentMessageTo(conn, InvalidFactionSelected, new object[] { "faction", faction });
					return true;
				}

				targetClient.Faction = faction;
				server.SyncLobbyClients();

				return true;
			}
		}

		static bool Team(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				var parts = s.Split(' ');
				var targetClient = server.LobbyInfo.ClientWithIndex(Exts.ParseInt32Invariant(parts[0]));

				// Only the host can change other client's info
				if (targetClient.Index != client.Index && !client.IsAdmin)
					return true;

				// Map has disabled team changes
				if (server.LobbyInfo.Slots[targetClient.Slot].LockTeam)
					return true;

				if (!Exts.TryParseInt32Invariant(parts[1], out var team))
				{
					Log.Write("server", $"Invalid team: {s}");
					return false;
				}

				targetClient.Team = team;
				server.SyncLobbyClients();

				return true;
			}
		}

		static bool Handicap(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				var parts = s.Split(' ');
				var targetClient = server.LobbyInfo.ClientWithIndex(Exts.ParseInt32Invariant(parts[0]));

				// Only the host can change other client's info
				if (targetClient.Index != client.Index && !client.IsAdmin)
					return true;

				// Map has disabled handicap changes
				if (server.LobbyInfo.Slots[targetClient.Slot].LockHandicap)
					return true;

				if (!Exts.TryParseInt32Invariant(parts[1], out var handicap))
				{
					Log.Write("server", $"Invalid handicap: {s}");
					return false;
				}

				// Handicaps may be set between 0 - 95% in steps of 5%
				var options = Enumerable.Range(0, 20).Select(i => 5 * i);
				if (!options.Contains(handicap))
				{
					Log.Write("server", $"Invalid handicap: {s}");
					return false;
				}

				targetClient.Handicap = handicap;
				server.SyncLobbyClients();

				return true;
			}
		}

		static bool ClearPlayerSpawn(S server, Connection conn, Session.Client client, string s)
		{
			var spawnPoint = Exts.ParseInt32Invariant(s);
			if (spawnPoint == 0)
				return true;

			var existingClient = server.LobbyInfo.Clients.FirstOrDefault(cc => cc.SpawnPoint == spawnPoint);
			if (client != existingClient && !client.IsAdmin)
			{
				server.SendFluentMessageTo(conn, AdminClearSpawn);
				return true;
			}

			// Clearing a selected spawn point removes the player
			if (existingClient != null)
			{
				// Prevent a map-defined lock spawn from being affected
				if (existingClient.Slot != null && server.LobbyInfo.Slots[existingClient.Slot].LockSpawn)
					return true;

				existingClient.SpawnPoint = 0;
				if (existingClient.State == Session.ClientState.Ready)
					existingClient.State = Session.ClientState.NotReady;

				server.SyncLobbyClients();
				return true;
			}

			// Clearing an empty spawn point prevents it from being selected
			// Clearing a disabled spawn restores it for use
			if (!server.LobbyInfo.DisabledSpawnPoints.Add(spawnPoint))
				server.LobbyInfo.DisabledSpawnPoints.Remove(spawnPoint);

			server.SyncLobbyInfo();
			return true;
		}

		static bool Spawn(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				var parts = s.Split(' ');
				var targetClient = server.LobbyInfo.ClientWithIndex(Exts.ParseInt32Invariant(parts[0]));

				// Only the host can change other client's info
				if (targetClient.Index != client.Index && !client.IsAdmin)
					return true;

				// Spectators don't need a spawnpoint
				if (targetClient.Slot == null)
					return true;

				// Map has disabled spawn changes
				if (server.LobbyInfo.Slots[targetClient.Slot].LockSpawn)
					return true;

				if (!Exts.TryParseInt32Invariant(parts[1], out var spawnPoint)
					|| spawnPoint < 0 || spawnPoint > server.Map.SpawnPoints.Length)
				{
					Log.Write("server", $"Invalid spawn point: {parts[1]}");
					return true;
				}

				if (server.LobbyInfo.Clients.Any(cc => cc != client && (cc.SpawnPoint == spawnPoint) && (cc.SpawnPoint != 0)))
				{
					server.SendFluentMessageTo(conn, SpawnOccupied);
					return true;
				}

				// Check if any other slot has locked the requested spawn
				if (spawnPoint > 0)
				{
					var spawnLockedByAnotherSlot = server.LobbyInfo.Slots.Where(ss => ss.Value.LockSpawn).Any(ss =>
					{
						var pr = PlayerReferenceForSlot(server, ss.Value);
						return pr != null && pr.Spawn == spawnPoint;
					});

					if (spawnLockedByAnotherSlot)
					{
						server.SendFluentMessageTo(conn, SpawnLocked);
						return true;
					}
				}

				targetClient.SpawnPoint = spawnPoint;
				server.SyncLobbyClients();

				return true;
			}
		}

		static bool PlayerColor(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				var parts = s.Split(' ');
				var targetClient = server.LobbyInfo.ClientWithIndex(Exts.ParseInt32Invariant(parts[0]));

				// Only the host can change other client's info
				if (targetClient.Index != client.Index && !client.IsAdmin)
					return true;

				// Spectator or map has disabled color changes
				if (targetClient.Slot == null || server.LobbyInfo.Slots[targetClient.Slot].LockColor)
					return true;

				// Validate if color is allowed and get an alternative it isn't
				var newColor = FieldLoader.GetValue<Color>("(value)", parts[1]);
				targetClient.Color = SanitizePlayerColor(server, newColor, targetClient.Index, conn);

				// Only update player's preferred color if new color is valid
				if (newColor == targetClient.Color)
					targetClient.PreferredColor = targetClient.Color;

				server.SyncLobbyClients();

				return true;
			}
		}

		public static bool CanSyncLobby(ServerType serverType)
		{
			return serverType is ServerType.Local or ServerType.Skirmish;
		}

		static bool SyncLobby(S server, Connection conn, Session.Client client, string s)
		{
			lock (server.LobbyInfo)
			{
				if (!CanSyncLobby(server.Type))
				{
					server.SendFluentMessageTo(conn, InvalidLobbyInfo);
					return true;
				}

				if (!client.IsAdmin)
				{
					server.SendFluentMessageTo(conn, AdminLobbyInfo);
					return true;
				}

				try
				{
					server.LobbyInfo = Session.Deserialize(s, nameof(SyncLobby));
					server.SyncLobbyInfo();
				}
				catch (Exception)
				{
					server.SendFluentMessageTo(conn, InvalidLobbyInfo);
				}

				return true;
			}
		}

		static void InitializeMapPool(S server)
		{
			if (server.Type != ServerType.Dedicated)
				return;

			var mapCache = server.ModData.MapCache;
			if (server.Settings.MapPool.Length > 0)
				server.MapPool = server.Settings.MapPool.ToHashSet();
			else
				server.MapPool = mapCache
					.Where(p => p.Status == MapStatus.Available && p.Visibility.HasFlag(MapVisibility.Lobby))
					.Select(p => p.Uid)
					.ToHashSet();

			var unknownMaps = server.MapPool.Where(server.MapIsUnknown).ToList();
			if (unknownMaps.Count == 0)
				return;

			var stillUnknownMaps = server.MapPool.Where(server.MapIsUnknown).ToList();
			if (stillUnknownMaps.Count != 0)
				Log.Write("server", "Failed to resolve maps: " + stillUnknownMaps.JoinWith(", "));
		}

		static string ChooseInitialMap(S server)
		{
			if (server.MapIsKnown(server.Settings.Map))
				return server.Settings.Map;

			if (server.MapPool == null)
				return server.ModData.MapCache.ChooseInitialMap(server.Settings.Map, new MersenneTwister());

			return server.MapPool
				.Where(server.MapIsKnown)
				.RandomOrDefault(new MersenneTwister());
		}

		public void ServerStarted(S server)
		{
			lock (server.LobbyInfo)
			{
				InitializeMapPool(server);

				var uid = ChooseInitialMap(server);
				if (string.IsNullOrEmpty(uid))
					throw new InvalidOperationException("Unable to resolve a valid initial map");

				server.LobbyInfo.GlobalSettings.Map = server.Settings.Map = uid;
				server.Map = server.ModData.MapCache[uid];
				server.LobbyInfo.GlobalSettings.MapStatus = server.MapStatusCache[server.Map];
				server.LobbyInfo.Slots = server.Map.Players.Players
					.Select(p => MakeSlotFromPlayerReference(p.Value))
					.Where(s => s != null)
					.ToDictionary(s => s.PlayerReference, s => s);

				LoadMapSettings(server, server.LobbyInfo.GlobalSettings, server.Map);
			}
		}

		static Session.Slot MakeSlotFromPlayerReference(PlayerReference pr)
		{
			if (!pr.Playable)
				return null;

			return new Session.Slot
			{
				PlayerReference = pr.Name,
				Closed = false,
				AllowBots = pr.AllowBots,
				LockFaction = pr.LockFaction,
				LockColor = pr.LockColor,
				LockTeam = pr.LockTeam,
				LockHandicap = pr.LockHandicap,
				LockSpawn = pr.LockSpawn,
				Required = pr.Required,
			};
		}

		public static void LoadMapSettings(S server, Session.Global gs, MapPreview map)
		{
			lock (server.LobbyInfo)
			{
				var options = map.PlayerActorInfo.TraitInfos<ILobbyOptions>()
					.Concat(map.WorldActorInfo.TraitInfos<ILobbyOptions>())
					.SelectMany(t => t.LobbyOptions(map));

				foreach (var o in options)
				{
					var value = o.DefaultValue;
					var preferredValue = o.DefaultValue;
					if (gs.LobbyOptions.TryGetValue(o.Id, out var state))
					{
						// Propagate old state on map change
						if (!o.IsLocked)
						{
							if (o.Values.Keys.Contains(state.PreferredValue))
								value = state.PreferredValue;
							else if (o.Values.Keys.Contains(state.Value))
								value = state.Value;
						}

						preferredValue = state.PreferredValue;
					}
					else
						state = new Session.LobbyOptionState();

					state.IsLocked = o.IsLocked;
					state.Value = value;
					state.PreferredValue = preferredValue;
					gs.LobbyOptions[o.Id] = state;
				}
			}
		}

		public static Color SanitizePlayerColor(S server, Color askedColor, int playerIndex, Connection connectionToEcho = null)
		{
			lock (server.LobbyInfo)
			{
				var colorManager = server.ModData.DefaultRules.Actors[SystemActors.World].TraitInfo<IColorPickerManagerInfo>();
				var askColor = askedColor;

				void OnError(string message)
				{
					if (connectionToEcho != null && message != null)
						server.SendFluentMessageTo(connectionToEcho, message);
				}

				var terrainColors = server.ModData.DefaultTerrainInfo[server.Map.TileSet].RestrictedPlayerColors.ToList();
				var playerColors = server.LobbyInfo.Clients.Where(c => c.Index != playerIndex).Select(c => c.Color)
					.Concat(server.Map.Players.Players.Values.Select(p => p.Color)).ToList();

				return colorManager.MakeValid(askColor, server.Random, terrainColors, playerColors, OnError);
			}
		}

		public static string SanitizePlayerFaction(S server, string askedFaction, IEnumerable<string> validFactions)
		{
			return !validFactions.Contains(askedFaction) ? "Random" : askedFaction;
		}

		static string MissionBriefingOrDefault(S server)
		{
			var missionData = server.Map.WorldActorInfo.TraitInfoOrDefault<MissionDataInfo>();
			if (missionData != null && !string.IsNullOrEmpty(missionData.Briefing))
				return missionData.Briefing.Replace("\\n", "\n");

			return null;
		}

		public void ClientJoined(S server, Connection conn)
		{
			lock (server.LobbyInfo)
			{
				if (server.MapPool != null)
					server.SendOrderTo(conn, "SyncMapPool", FieldSaver.FormatValue(server.MapPool));

				var client = server.GetClient(conn);
				mapReadinessThrottle.Remove(client.Index);
				InitializeClientMapReadiness(client, server.LobbyInfo.GlobalSettings.Map);

				// Validate whether color is allowed and get an alternative if it isn't
				if (client.Slot != null && !server.LobbyInfo.Slots[client.Slot].LockColor)
					client.Color = SanitizePlayerColor(server, client.Color, client.Index);

				// Report any custom map details
				// HACK: this isn't the best place for this to live, but if we move it somewhere else
				// then we need a larger hack to hook the map change event.
				var briefing = MissionBriefingOrDefault(server);
				if (briefing != null)
					server.SendOrderTo(conn, "Message", briefing);
			}
		}

		void INotifyServerEmpty.ServerEmpty(S server)
		{
			lock (server.LobbyInfo)
			{
				mapReadinessThrottle.Reset();
				rejectedMapReadinessReports = 0;

				// Expire any temporary bans
				server.TempBans.Clear();

				server.LobbyInfo.GlobalSettings.AllowSpectators = false;

				// Reset player slots
				server.LobbyInfo.Slots = server.Map.Players.Players
					.Select(p => MakeSlotFromPlayerReference(p.Value))
					.Where(ss => ss != null)
					.ToDictionary(ss => ss.PlayerReference, ss => ss);
			}
		}

		public static PlayerReference PlayerReferenceForSlot(S server, Session.Slot slot)
		{
			if (slot == null)
				return null;

			return server.Map.Players.Players[slot.PlayerReference];
		}
	}
}
