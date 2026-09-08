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
using System.Globalization;
using System.Linq;

namespace OpenRA.Network
{
	public enum LobbyStartMode
	{
		SafeDirect,
		ForceConfirmed,
		Automatic
	}

	public struct LobbyStartInputs
	{
		public Session LobbyInfo { get; set; }
		public LobbyStartMode Mode { get; set; }
		public bool RequesterIsAdmin { get; set; }
		public bool RequireCurrentMapReadiness { get; set; }
		public bool InsufficientEnabledSpawnPoints { get; set; }
	}

	public readonly struct LobbyStartAvailability : IEquatable<LobbyStartAvailability>
	{
		public static readonly LobbyStartAvailability Allowed = new(
			Session.LobbyStartBlockReason.None, -1, Session.ClientMapPhase.Unknown, -1, 0);

		public bool CanStart => Reason == Session.LobbyStartBlockReason.None;
		public Session.LobbyStartBlockReason Reason { get; }
		public int ClientIndex { get; }
		public Session.ClientMapPhase ClientMapPhase { get; }
		public int Progress { get; }
		public int AdditionalBlockingClientCount { get; }

		internal LobbyStartAvailability(Session.LobbyStartBlockReason reason, int clientIndex,
			Session.ClientMapPhase clientMapPhase, int progress, int additionalBlockingClientCount)
		{
			Reason = reason;
			ClientIndex = clientIndex;
			ClientMapPhase = clientMapPhase;
			Progress = progress;
			AdditionalBlockingClientCount = additionalBlockingClientCount;
		}

		static LobbyStartAvailability Blocked(Session.LobbyStartBlockReason reason)
		{
			return new LobbyStartAvailability(reason, -1, Session.ClientMapPhase.Unknown, -1, 0);
		}

		static LobbyStartAvailability Blocked(Session.LobbyStartBlockReason reason,
			Session.Client client, int additionalBlockingClientCount)
		{
			return new LobbyStartAvailability(reason, client.Index, client.MapPhase,
				client.MapProgress, additionalBlockingClientCount);
		}

		static (int Priority, Session.LobbyStartBlockReason Reason) MapBlocker(Session.Client client, string mapUid)
		{
			if (client.IsMapReadyFor(mapUid) && client.State != Session.ClientState.Invalid)
				return (-1, Session.LobbyStartBlockReason.None);

			return client.MapPhase switch
			{
				Session.ClientMapPhase.Error => (0, Session.LobbyStartBlockReason.ClientMapError),
				Session.ClientMapPhase.Unavailable => (1, Session.LobbyStartBlockReason.ClientMapUnavailable),
				Session.ClientMapPhase.InstallingOrVerifying =>
					(2, Session.LobbyStartBlockReason.ClientMapInstallingOrVerifying),
				Session.ClientMapPhase.Downloading => (3, Session.LobbyStartBlockReason.ClientMapDownloading),
				Session.ClientMapPhase.Searching => (4, Session.LobbyStartBlockReason.ClientMapSearching),
				Session.ClientMapPhase.WaitingForDownload =>
					(5, Session.LobbyStartBlockReason.ClientMapWaitingForDownload),
				_ => (6, Session.LobbyStartBlockReason.ClientMapUnknown)
			};
		}

		public static LobbyStartAvailability EvaluateMapSafety(
			Session lobbyInfo, bool requireCurrentMapReadiness)
		{
			if (lobbyInfo?.GlobalSettings == null)
				return Blocked(Session.LobbyStartBlockReason.ServerMapNotPlayable);

			var mapStatus = lobbyInfo.GlobalSettings.MapStatus;
			if ((mapStatus & Session.MapStatus.Validating) != 0)
				return Blocked(Session.LobbyStartBlockReason.ServerMapValidating);
			if ((mapStatus & Session.MapStatus.Incompatible) != 0)
				return Blocked(Session.LobbyStartBlockReason.ServerMapIncompatible);
			if ((mapStatus & Session.MapStatus.Playable) == 0)
				return Blocked(Session.LobbyStartBlockReason.ServerMapNotPlayable);

			if (!requireCurrentMapReadiness)
				return Allowed;

			var currentMapUid = lobbyInfo.GlobalSettings.Map;
			var blockers = lobbyInfo.NonBotClients
				.Select(client => (Client: client, Blocker: MapBlocker(client, currentMapUid)))
				.Where(entry => entry.Blocker.Priority >= 0)
				.OrderBy(entry => entry.Blocker.Priority)
				.ThenBy(entry => entry.Client.Index)
				.ToArray();
			if (blockers.Length == 0)
				return Allowed;

			return Blocked(blockers[0].Blocker.Reason, blockers[0].Client, blockers.Length - 1);
		}

		public static LobbyStartAvailability Evaluate(LobbyStartInputs inputs)
		{
			var lobbyInfo = inputs.LobbyInfo;
			var mapSafety = EvaluateMapSafety(lobbyInfo, inputs.RequireCurrentMapReadiness);
			if (!mapSafety.CanStart)
				return mapSafety;

			if (!inputs.RequesterIsAdmin)
				return Blocked(Session.LobbyStartBlockReason.RequesterNotAdmin);

			if (lobbyInfo.Slots.Any(slot => slot.Value.Required && lobbyInfo.ClientInSlot(slot.Key) == null))
				return Blocked(Session.LobbyStartBlockReason.RequiredSlotEmpty);

			if (lobbyInfo.Slots.All(slot => lobbyInfo.ClientInSlot(slot.Key) == null))
				return Blocked(Session.LobbyStartBlockReason.NoPlayers);

			if (!lobbyInfo.GlobalSettings.EnableSingleplayer && lobbyInfo.NonBotPlayers.Count() < 2)
				return Blocked(Session.LobbyStartBlockReason.InsufficientHumans);

			if (inputs.Mode == LobbyStartMode.Automatic)
			{
				var requiredReadyClients = lobbyInfo.NonBotPlayers
					.Concat(lobbyInfo.NonBotClients.Where(client => client.IsAdmin))
					.GroupBy(client => client.Index)
					.Select(group => group.First())
					.Where(client => client.State != Session.ClientState.Ready)
					.OrderBy(client => client.Index)
					.ToArray();
				if (requiredReadyClients.Length != 0)
					return Blocked(Session.LobbyStartBlockReason.ClientGameNotReady,
						requiredReadyClients[0], requiredReadyClients.Length - 1);
			}

			if (inputs.InsufficientEnabledSpawnPoints)
				return Blocked(Session.LobbyStartBlockReason.InsufficientSpawnPoints);

			return Allowed;
		}

		public LobbyStartAvailability ApplyLocalMapAvailability(bool localMapAvailable)
		{
			return CanStart && !localMapAvailable ?
				Blocked(Session.LobbyStartBlockReason.SelectedMapUnavailable) : this;
		}

		public LobbyStartRejection ToRejection(LobbySafeStartRequest request)
		{
			if (CanStart)
				throw new InvalidOperationException("An allowed start cannot be serialized as a rejection.");

			return new LobbyStartRejection(Reason, ClientIndex, ClientMapPhase, Progress,
				request.ExpectedMapUid, request.RequestId);
		}

		public bool Equals(LobbyStartAvailability other)
		{
			return Reason == other.Reason && ClientIndex == other.ClientIndex &&
				ClientMapPhase == other.ClientMapPhase && Progress == other.Progress &&
				AdditionalBlockingClientCount == other.AdditionalBlockingClientCount;
		}

		public override bool Equals(object obj)
		{
			return obj is LobbyStartAvailability other && Equals(other);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Reason, ClientIndex, ClientMapPhase, Progress,
				AdditionalBlockingClientCount);
		}

		public static bool operator ==(LobbyStartAvailability left, LobbyStartAvailability right) => left.Equals(right);
		public static bool operator !=(LobbyStartAvailability left, LobbyStartAvailability right) => !left.Equals(right);
	}

	public sealed class LobbyStartRejection
	{
		public Session.LobbyStartBlockReason Reason { get; }
		public int ClientIndex { get; }
		public Session.ClientMapPhase ClientMapPhase { get; }
		public int Progress { get; }
		public string ExpectedMapUid { get; }
		public long RequestId { get; }

		public LobbyStartRejection(Session.LobbyStartBlockReason reason, int clientIndex,
			Session.ClientMapPhase clientMapPhase, int progress, string expectedMapUid, long requestId)
		{
			Reason = reason;
			ClientIndex = clientIndex;
			ClientMapPhase = clientMapPhase;
			Progress = progress;
			ExpectedMapUid = expectedMapUid;
			RequestId = requestId;
			if (!IsValid())
				throw new ArgumentException("Invalid lobby start rejection.");
		}

		bool IsValid()
		{
			if (!Enum.IsDefined(typeof(Session.LobbyStartBlockReason), Reason) ||
				Reason == Session.LobbyStartBlockReason.None || ClientIndex < -1 ||
				string.IsNullOrEmpty(ExpectedMapUid) || ExpectedMapUid.Any(char.IsWhiteSpace) ||
				RequestId <= 0 ||
				!ClientMapReadinessState.IsValidPhaseProgress(ClientMapPhase, Progress))
				return false;

			if (ClientIndex < 0)
				return ClientMapPhase == Session.ClientMapPhase.Unknown && Progress == -1;

			return Reason switch
			{
				Session.LobbyStartBlockReason.ClientMapUnknown => ClientMapPhase == Session.ClientMapPhase.Unknown,
				Session.LobbyStartBlockReason.ClientMapSearching => ClientMapPhase == Session.ClientMapPhase.Searching,
				Session.LobbyStartBlockReason.ClientMapWaitingForDownload =>
					ClientMapPhase == Session.ClientMapPhase.WaitingForDownload,
				Session.LobbyStartBlockReason.ClientMapDownloading => ClientMapPhase == Session.ClientMapPhase.Downloading,
				Session.LobbyStartBlockReason.ClientMapInstallingOrVerifying =>
					ClientMapPhase == Session.ClientMapPhase.InstallingOrVerifying,
				Session.LobbyStartBlockReason.ClientMapUnavailable => ClientMapPhase == Session.ClientMapPhase.Unavailable,
				Session.LobbyStartBlockReason.ClientMapError => ClientMapPhase == Session.ClientMapPhase.Error,
				Session.LobbyStartBlockReason.ClientGameNotReady => ClientMapPhase == Session.ClientMapPhase.Ready,
				_ => false
			};
		}

		public LobbyStartAvailability ToAvailability()
		{
			return new LobbyStartAvailability(Reason, ClientIndex, ClientMapPhase, Progress, 0);
		}

		public string Serialize()
		{
			return new List<MiniYamlNode>
			{
				new(nameof(Reason), Reason.ToString()),
				new(nameof(ClientIndex), ClientIndex.ToString(CultureInfo.InvariantCulture)),
				new(nameof(ClientMapPhase), ClientMapPhase.ToString()),
				new(nameof(Progress), Progress.ToString(CultureInfo.InvariantCulture)),
				new(nameof(ExpectedMapUid), ExpectedMapUid),
				new(nameof(RequestId), RequestId.ToString(CultureInfo.InvariantCulture))
			}.WriteToString();
		}

		public static LobbyStartRejection Deserialize(string data)
		{
			try
			{
				var nodes = MiniYaml.FromString(data, "lobby-start-rejection");
				var expected = new[]
				{
					nameof(Reason), nameof(ClientIndex), nameof(ClientMapPhase), nameof(Progress),
					nameof(ExpectedMapUid), nameof(RequestId)
				};
				if (nodes.Count != expected.Length || expected.Any(key => nodes.Count(node => node.Key == key) != 1) ||
					nodes.Any(node => !expected.Contains(node.Key) || node.Value.Nodes.Any()))
					return null;

				var values = nodes.ToDictionary(node => node.Key, node => node.Value.Value);
				if (!Enum.TryParse(values[nameof(Reason)], false, out Session.LobbyStartBlockReason reason) ||
					values[nameof(Reason)] != reason.ToString() ||
					!int.TryParse(values[nameof(ClientIndex)], NumberStyles.AllowLeadingSign,
						CultureInfo.InvariantCulture, out var clientIndex) ||
					values[nameof(ClientIndex)] != clientIndex.ToString(CultureInfo.InvariantCulture) ||
					!Enum.TryParse(values[nameof(ClientMapPhase)], false, out Session.ClientMapPhase phase) ||
					values[nameof(ClientMapPhase)] != phase.ToString() ||
					!int.TryParse(values[nameof(Progress)], NumberStyles.AllowLeadingSign,
						CultureInfo.InvariantCulture, out var progress) ||
					values[nameof(Progress)] != progress.ToString(CultureInfo.InvariantCulture) ||
					!long.TryParse(values[nameof(RequestId)], NumberStyles.None,
						CultureInfo.InvariantCulture, out var requestId) || requestId <= 0 ||
					values[nameof(RequestId)] != requestId.ToString(CultureInfo.InvariantCulture))
					return null;

				return new LobbyStartRejection(reason, clientIndex, phase, progress,
					values[nameof(ExpectedMapUid)], requestId);
			}
			catch (Exception)
			{
				return null;
			}
		}
	}
}
