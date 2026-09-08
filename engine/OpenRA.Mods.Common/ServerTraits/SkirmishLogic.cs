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
using System.IO;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Server;
using OpenRA.Support;
using OpenRA.Traits;
using S = OpenRA.Server.Server;

namespace OpenRA.Mods.Common.Server
{
	public class SkirmishLogic : ServerTrait, IClientJoined, INotifySyncLobbyInfo
	{
		class SkirmishSlot
		{
			[FieldLoader.Serialize(FromYamlKey = true)]
			public readonly string Slot;
			public readonly Color Color;
			public readonly string Faction;
			public readonly int SpawnPoint;
			public readonly int Team;
			public readonly int Handicap;

			public SkirmishSlot() { }

			public SkirmishSlot(Session.Client c)
			{
				Slot = c.Slot;
				Color = c.Color;
				Faction = c.Faction;
				SpawnPoint = c.SpawnPoint;
				Team = c.Team;
				Handicap = c.Handicap;
			}

			public static void DeserializeToClient(MiniYaml yaml, Session.Client c)
			{
				var s = FieldLoader.Load<SkirmishSlot>(yaml);
				c.Slot = s.Slot;
				c.Color = c.PreferredColor = s.Color;
				c.Faction = s.Faction;
				c.SpawnPoint = s.SpawnPoint;
				c.Team = s.Team;
				c.Handicap = s.Handicap;
			}
		}

		sealed class StagedHumanClient
		{
			public readonly Session.Client Published;
			public readonly Session.Client Values;

			public StagedHumanClient(Session.Client published, Session.Client values)
			{
				Published = published;
				Values = values;
			}
		}

		sealed class StagedSkirmishState
		{
			public readonly MapPreview Map;
			public readonly Session.Global GlobalSettings;
			public readonly Dictionary<string, Session.Slot> Slots;
			public readonly HashSet<int> DisabledSpawnPoints;
			public readonly List<StagedHumanClient> Humans;
			public readonly List<Session.Client> Bots;
			public readonly List<Session.Client> PublishedClients;

			public StagedSkirmishState(
				MapPreview map,
				Session.Global globalSettings,
				Dictionary<string, Session.Slot> slots,
				HashSet<int> disabledSpawnPoints,
				List<StagedHumanClient> humans,
				List<Session.Client> bots,
				List<Session.Client> publishedClients)
			{
				Map = map;
				GlobalSettings = globalSettings;
				Slots = slots;
				DisabledSpawnPoints = disabledSpawnPoints;
				Humans = humans;
				Bots = bots;
				PublishedClients = publishedClients;
			}
		}

		static Session.Client CloneClient(Session.Client client)
		{
			var clone = Session.Client.Deserialize(client.Serialize().Value);
			clone.IPAddress = client.IPAddress;
			return clone;
		}

		static Session.Slot MakeSlot(PlayerReference player)
		{
			return new Session.Slot
			{
				PlayerReference = player.Name,
				AllowBots = player.AllowBots,
				LockFaction = player.LockFaction,
				LockColor = player.LockColor,
				LockTeam = player.LockTeam,
				LockHandicap = player.LockHandicap,
				LockSpawn = player.LockSpawn,
				Required = player.Required,
			};
		}

		static Color SanitizePlayerColor(
			S server,
			MapPreview map,
			IReadOnlyCollection<Session.Client> clients,
			Color requestedColor,
			int playerIndex,
			MersenneTwister random)
		{
			var colorManager = server.ModData.DefaultRules.Actors[SystemActors.World]
				.TraitInfo<IColorPickerManagerInfo>();
			var terrainColors = server.ModData.DefaultTerrainInfo[map.TileSet]
				.RestrictedPlayerColors.ToList();
			var playerColors = clients
				.Where(client => client.Index != playerIndex)
				.Select(client => client.Color)
				.Concat(map.Players.Players.Values.Select(player => player.Color))
				.ToList();

			return colorManager.MakeValid(
				requestedColor, random, terrainColors, playerColors);
		}

		static bool TryValidateClient(
			S server,
			MapPreview map,
			Dictionary<string, Session.Slot> slots,
			IReadOnlyCollection<string> selectableFactions,
			IReadOnlyCollection<Session.Client> otherClients,
			HashSet<string> occupiedSlots,
			HashSet<int> occupiedSpawnPoints,
			MersenneTwister colorRandom,
			Session.Client client,
			bool isBot)
		{
			if (client.Slot == null)
				return !isBot;

			if (!slots.TryGetValue(client.Slot, out var slot) ||
				!map.Players.Players.TryGetValue(client.Slot, out var player) ||
				(isBot && !slot.AllowBots) ||
				!occupiedSlots.Add(client.Slot))
				return false;

			client.Faction = LobbyCommands.SanitizePlayerFaction(
				server, client.Faction, selectableFactions);
			S.SyncClientToPlayerReference(client, player);

			if (!slot.LockColor)
				client.Color = client.PreferredColor = SanitizePlayerColor(
					server, map, otherClients, client.Color, client.Index, colorRandom);

			if (client.SpawnPoint < 0 || client.SpawnPoint > map.SpawnPoints.Length ||
				(client.SpawnPoint != 0 && !occupiedSpawnPoints.Add(client.SpawnPoint)))
				return false;

			return slot.LockHandicap ||
				client.Handicap >= 0 && client.Handicap <= 95 && client.Handicap % 5 == 0;
		}

		static bool TryStageSkirmishState(
			S server, MiniYaml nodes, Connection conn,
			out StagedSkirmishState stagedState)
		{
			stagedState = null;

			var mapNode = nodes.NodeWithKeyOrDefault("Map");
			var mapUid = mapNode?.Value.Value;
			if (string.IsNullOrEmpty(mapUid))
				return false;

			var map = server.ModData.MapCache[mapUid];
			if (map.Status != MapStatus.Available)
				return false;

			var publishedPlayer = server.GetClient(conn);
			if (publishedPlayer == null)
				return false;

			var publishedHumans = server.LobbyInfo.Clients
				.Where(client => !client.IsBot)
				.ToList();
			if (!publishedHumans.Contains(publishedPlayer) ||
				publishedHumans.Select(client => client.Index).Distinct().Count() != publishedHumans.Count)
				return false;

			var humans = publishedHumans
				.Select(client => new StagedHumanClient(client, CloneClient(client)))
				.ToList();
			var stagedPlayer = humans.Single(human => human.Published == publishedPlayer).Values;
			var botController = humans.Select(human => human.Values)
				.FirstOrDefault(client => client.IsAdmin);
			if (botController == null)
				return false;

			var globalSettings = Session.Global.Deserialize(
				server.LobbyInfo.GlobalSettings.Serialize().Value);
			globalSettings.Map = map.Uid;
			globalSettings.MapStatus = server.MapStatusCache[map];

			var slots = map.Players.Players.Values
				.Where(player => player.Playable)
				.Select(MakeSlot)
				.ToDictionary(slot => slot.PlayerReference, slot => slot);
			LobbyCommands.LoadMapSettings(server, globalSettings, map);

			var optionsNode = nodes.NodeWithKeyOrDefault("Options");
			if (optionsNode != null)
			{
				var optionDefinitions = map.PlayerActorInfo.TraitInfos<ILobbyOptions>()
					.Concat(map.WorldActorInfo.TraitInfos<ILobbyOptions>())
					.SelectMany(options => options.LobbyOptions(map))
					.ToDictionary(option => option.Id, option => option);

				foreach (var optionNode in optionsNode.Value.Nodes)
					if (optionDefinitions.TryGetValue(optionNode.Key, out var option) &&
						!option.IsLocked && option.Values.ContainsKey(optionNode.Value.Value))
					{
						if (!globalSettings.LobbyOptions.TryGetValue(option.Id, out var optionState))
							return false;

						optionState.Value = optionState.PreferredValue = optionNode.Value.Value;
					}
			}

			var mapChanged = !string.Equals(
				server.LobbyInfo.GlobalSettings.Map, map.Uid, StringComparison.Ordinal);
			if (mapChanged)
			{
				var newSlots = slots.Keys.GetEnumerator();
				foreach (var oldSlot in server.LobbyInfo.Slots.Keys)
				{
					var human = humans.FirstOrDefault(
						candidate => candidate.Published.Slot == oldSlot);
					if (human != null)
						human.Values.Slot = newSlots.MoveNext() ? newSlots.Current : null;
				}

				LobbyCommands.ResetClientMapReadinessForMapChange(
					humans.Select(human => human.Values),
					server.LobbyInfo.GlobalSettings.Map, map.Uid);
			}

			var playerNode = nodes.NodeWithKeyOrDefault("Player");
			if (playerNode != null)
				SkirmishSlot.DeserializeToClient(playerNode.Value, stagedPlayer);

			var selectableFactions = map.WorldActorInfo.TraitInfos<FactionInfo>()
				.Where(faction => faction.Selectable)
				.Select(faction => faction.InternalName)
				.ToList();
			var stagedClients = humans.Select(human => human.Values).ToList();
			var occupiedSlots = new HashSet<string>(StringComparer.Ordinal);
			var occupiedSpawnPoints = new HashSet<int>();
			var colorRandom = new MersenneTwister(globalSettings.RandomSeed);
			foreach (var human in humans)
				if (!TryValidateClient(
					server, map, slots, selectableFactions, stagedClients,
					occupiedSlots, occupiedSpawnPoints, colorRandom, human.Values, false))
					return false;

			var bots = new List<Session.Client>();
			var botTypes = map.PlayerActorInfo.TraitInfos<IBotInfo>()
				.ToDictionary(bot => bot.Type, bot => bot);
			var botsNode = nodes.NodeWithKeyOrDefault("Bots");
			if (botsNode != null)
				foreach (var botNode in botsNode.Value.Nodes)
				{
					if (!botTypes.TryGetValue(botNode.Key, out var botInfo))
						return false;

					var bot = new Session.Client
					{
						Index = -bots.Count - 1,
						Name = botInfo.Name,
						Bot = botInfo.Type,
						Slot = botNode.Value.Value,
						State = Session.ClientState.NotReady,
						BotControllerClientIndex = botController.Index
					};

					SkirmishSlot.DeserializeToClient(botNode.Value, bot);
					if (!string.Equals(bot.Slot, botNode.Value.Value, StringComparison.Ordinal))
						return false;

					if (!TryValidateClient(
						server, map, slots, selectableFactions, stagedClients,
						occupiedSlots, occupiedSpawnPoints, colorRandom, bot, true))
						return false;

					bots.Add(bot);
					stagedClients.Add(bot);
				}

			var disabledSpawnPoints = mapChanged ?
				new HashSet<int>() : new HashSet<int>(server.LobbyInfo.DisabledSpawnPoints);
			var publishedClients = humans.Select(human => human.Published)
				.Concat(bots)
				.ToList();
			stagedState = new StagedSkirmishState(
				map, globalSettings, slots, disabledSpawnPoints,
				humans, bots, publishedClients);
			return true;
		}

		static void CopyClientState(Session.Client source, Session.Client destination)
		{
			destination.PreferredColor = source.PreferredColor;
			destination.Color = source.Color;
			destination.Faction = source.Faction;
			destination.SpawnPoint = source.SpawnPoint;
			destination.State = source.State;
			destination.MapUid = source.MapUid;
			destination.MapPhase = source.MapPhase;
			destination.MapProgress = source.MapProgress;
			destination.Team = source.Team;
			destination.Handicap = source.Handicap;
			destination.Slot = source.Slot;
		}

		static void CommitSkirmishState(S server, StagedSkirmishState stagedState)
		{
			foreach (var bot in stagedState.Bots)
				bot.Index = server.ChooseFreePlayerIndex();

			foreach (var human in stagedState.Humans)
				CopyClientState(human.Values, human.Published);

			server.Map = stagedState.Map;
			server.LobbyInfo.GlobalSettings = stagedState.GlobalSettings;
			server.LobbyInfo.Slots = stagedState.Slots;
			server.LobbyInfo.DisabledSpawnPoints = stagedState.DisabledSpawnPoints;
			server.LobbyInfo.Clients = stagedState.PublishedClients;
		}

		static bool IsExpectedSkirmishRestoreFailure(Exception exception) =>
			exception is IOException ||
			exception is UnauthorizedAccessException ||
			exception is InvalidDataException ||
			exception is YamlException ||
			exception is ArgumentException ||
			exception is FormatException ||
			exception is OverflowException ||
			exception is InvalidOperationException ||
			exception is KeyNotFoundException;

		static bool TryInitializeFromFile(S server, string path, Connection conn)
		{
			StagedSkirmishState stagedState;
			try
			{
				if (!File.Exists(path))
					return false;

				var nodes = new MiniYaml("", MiniYaml.FromFile(path));
				if (!TryStageSkirmishState(server, nodes, conn, out stagedState))
					return false;
			}
			catch (Exception ex) when (IsExpectedSkirmishRestoreFailure(ex))
			{
				Log.Write("debug", ex.ToString());
				return false;
			}

			CommitSkirmishState(server, stagedState);
			return true;
		}

		void INotifySyncLobbyInfo.LobbyInfoSynced(S server)
		{
			if (server.Type != ServerType.Skirmish)
				return;

			try
			{
				var path = RuntimeContractStorage.SkirmishPath(
					Platform.SupportDir, server.ModData.Manifest.Id,
					server.ModData.RuntimeContract);
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				var playerClient = server.LobbyInfo.NonBotClients.First();
				new List<MiniYamlNode>
				{
					new("Map", server.LobbyInfo.GlobalSettings.Map),
					new("Options", new MiniYaml("", server.LobbyInfo.GlobalSettings.LobbyOptions
						.Select(kv => new MiniYamlNode(kv.Key, kv.Value.Value)))),
					new("Player", FieldSaver.Save(new SkirmishSlot(playerClient))),
					new("Bots", new MiniYaml("", server.LobbyInfo.Clients.Where(c => c.IsBot)
						.Select(b => new MiniYamlNode(b.Bot, FieldSaver.Save(new SkirmishSlot(b))))))
				}.WriteToFile(path);
			}
			catch (Exception ex)
			{
				Log.Write("debug", ex.ToString());
			}
		}

		void IClientJoined.ClientJoined(S server, Connection conn)
		{
			if (server.Type != ServerType.Skirmish)
				return;

			var skirmishFile = RuntimeContractStorage.SkirmishPath(
				Platform.SupportDir, server.ModData.Manifest.Id,
				server.ModData.RuntimeContract);
			if (TryInitializeFromFile(server, skirmishFile, conn))
				return;

			var slot = server.LobbyInfo.FirstEmptyBotSlot();
			var bot = server.Map.PlayerActorInfo.TraitInfos<IBotInfo>().Select(t => t.Type).FirstOrDefault();
			var botController = server.LobbyInfo.Clients.FirstOrDefault(c => c.IsAdmin);
			if (slot != null && bot != null && botController != null)
				server.InterpretCommand($"slot_bot {slot} {botController.Index} {bot}", conn);
		}
	}
}
