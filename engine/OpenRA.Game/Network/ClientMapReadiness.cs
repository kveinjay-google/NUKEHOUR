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
using System.Globalization;
using System.Linq;

namespace OpenRA.Network
{
	public readonly struct ClientMapReadinessReport : IEquatable<ClientMapReadinessReport>
	{
		public readonly string MapUid;
		public readonly Session.ClientMapPhase Phase;
		public readonly int Progress;

		public ClientMapReadinessReport(string mapUid, Session.ClientMapPhase phase, int progress)
		{
			MapUid = mapUid;
			Phase = phase;
			Progress = progress;
		}

		internal static bool IsValidMapUid(string uid)
		{
			return !string.IsNullOrEmpty(uid) && !uid.Any(char.IsWhiteSpace);
		}

		public bool IsValidFor(string currentUid)
		{
			return IsValidMapUid(MapUid) &&
				string.Equals(MapUid, currentUid, StringComparison.Ordinal) &&
				ClientMapReadinessState.IsValidPhaseProgress(Phase, Progress);
		}

		public static bool TryParse(string command, out ClientMapReadinessReport report)
		{
			report = default;
			if (string.IsNullOrWhiteSpace(command))
				return false;

			var tokens = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length != 4 || tokens[0] != "map_status" || !IsValidMapUid(tokens[1]) ||
				!Enum.TryParse(tokens[2], false, out Session.ClientMapPhase phase) ||
				!Enum.IsDefined(typeof(Session.ClientMapPhase), phase) ||
				tokens[2] != phase.ToString() ||
				!int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var progress) ||
				!ClientMapReadinessState.IsValidPhaseProgress(phase, progress))
				return false;

			report = new ClientMapReadinessReport(tokens[1], phase, progress);
			return true;
		}

		public string ToCommand()
		{
			if (!IsValidFor(MapUid))
				throw new InvalidOperationException("Cannot serialize an invalid map readiness report.");

			return $"map_status {MapUid} {Phase} {Progress.ToString(CultureInfo.InvariantCulture)}";
		}

		public bool Equals(ClientMapReadinessReport other)
		{
			return string.Equals(MapUid, other.MapUid, StringComparison.Ordinal) &&
				Phase == other.Phase && Progress == other.Progress;
		}

		public override bool Equals(object obj)
		{
			return obj is ClientMapReadinessReport other && Equals(other);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(MapUid, Phase, Progress);
		}

		public static bool operator ==(ClientMapReadinessReport left, ClientMapReadinessReport right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(ClientMapReadinessReport left, ClientMapReadinessReport right)
		{
			return !left.Equals(right);
		}
	}

	public enum ClientMapReadinessApplyResult
	{
		Rejected,
		Unchanged,
		Changed,
		BecameReady,
		BecameUnready
	}

	public static class ClientMapReadinessState
	{
		public static bool IsValidPhaseProgress(Session.ClientMapPhase phase, int progress)
		{
			if (!Enum.IsDefined(typeof(Session.ClientMapPhase), phase))
				return false;

			return phase switch
			{
				Session.ClientMapPhase.Downloading => progress == -1 || progress is >= 0 and <= 99,
				Session.ClientMapPhase.Ready => progress == 100,
				_ => progress == -1
			};
		}

		public static void Reset(Session.Client client, string mapUid)
		{
			if (client == null)
				throw new ArgumentNullException(nameof(client));
			if (!ClientMapReadinessReport.IsValidMapUid(mapUid))
				throw new ArgumentException("Map UID must be a nonempty single token.", nameof(mapUid));

			client.MapUid = mapUid;
			client.MapPhase = Session.ClientMapPhase.Unknown;
			client.MapProgress = -1;
			client.State = Session.ClientState.Invalid;
		}

		public static ClientMapReadinessApplyResult Apply(
			Session.Client client, string currentUid, ClientMapReadinessReport report)
		{
			if (client == null || !report.IsValidFor(currentUid))
				return ClientMapReadinessApplyResult.Rejected;

			var wasReady = client.IsMapReadyFor(currentUid);
			var nextState = report.Phase == Session.ClientMapPhase.Ready ?
				(client.State == Session.ClientState.Invalid ? Session.ClientState.NotReady : client.State) :
				Session.ClientState.Invalid;
			var changed = !string.Equals(client.MapUid, report.MapUid, StringComparison.Ordinal) ||
				client.MapPhase != report.Phase || client.MapProgress != report.Progress || client.State != nextState;

			client.MapUid = report.MapUid;
			client.MapPhase = report.Phase;
			client.MapProgress = report.Progress;
			client.State = nextState;

			var isReady = client.IsMapReadyFor(currentUid);
			if (!wasReady && isReady)
				return ClientMapReadinessApplyResult.BecameReady;
			if (wasReady && !isReady)
				return ClientMapReadinessApplyResult.BecameUnready;

			return changed ? ClientMapReadinessApplyResult.Changed : ClientMapReadinessApplyResult.Unchanged;
		}
	}

	public sealed class ClientMapReadinessUpdate
	{
		[FieldLoader.Require]
		public int ClientIndex;

		[FieldLoader.Require]
		public string MapUid;

		[FieldLoader.Require]
		public Session.ClientMapPhase Phase = Session.ClientMapPhase.Unknown;

		[FieldLoader.Require]
		public int Progress = -1;

		[FieldLoader.Require]
		public Session.ClientState ClientState = Session.ClientState.Invalid;

		bool IsStructurallyValid()
		{
			return ClientIndex >= 0 && IsValidClientStateForPhase() &&
				new ClientMapReadinessReport(MapUid, Phase, Progress).IsValidFor(MapUid);
		}

		bool IsValidClientStateForPhase()
		{
			if (!Enum.IsDefined(typeof(Session.ClientState), ClientState) ||
				ClientState == Session.ClientState.Disconnected)
				return false;

			return Phase == Session.ClientMapPhase.Ready ?
				ClientState == Session.ClientState.NotReady || ClientState == Session.ClientState.Ready :
				ClientState == Session.ClientState.Invalid;
		}

		static bool IsCanonicalEnumToken<T>(string rawToken) where T : struct, Enum
		{
			return !string.IsNullOrEmpty(rawToken) &&
				Enum.TryParse(rawToken, false, out T value) &&
				Enum.IsDefined(typeof(T), value) &&
				string.Equals(rawToken, value.ToString(), StringComparison.Ordinal);
		}

		static bool HasExactlyOneOfEachField(MiniYaml data, out string phase, out string clientState)
		{
			const int AllFields = 0b1_1111;
			phase = null;
			clientState = null;
			var seen = 0;
			foreach (var node in data.Nodes)
			{
				var field = node.Key switch
				{
					nameof(ClientIndex) => 1 << 0,
					nameof(MapUid) => 1 << 1,
					nameof(Phase) => 1 << 2,
					nameof(Progress) => 1 << 3,
					nameof(ClientState) => 1 << 4,
					_ => 0
				};

				if (field == 0 || (seen & field) != 0)
					return false;

				seen |= field;
				if (field == 1 << 2)
					phase = node.Value.Value;
				else if (field == 1 << 4)
					clientState = node.Value.Value;
			}

			return seen == AllFields;
		}

		public MiniYamlNode Serialize()
		{
			return new MiniYamlNode("ClientMapReadiness", FieldSaver.Save(this));
		}

		public static ClientMapReadinessUpdate Deserialize(MiniYaml data)
		{
			try
			{
				if (data == null || !HasExactlyOneOfEachField(data, out var phase, out var clientState) ||
					!IsCanonicalEnumToken<Session.ClientMapPhase>(phase) ||
					!IsCanonicalEnumToken<Session.ClientState>(clientState))
					return null;

				var update = FieldLoader.Load<ClientMapReadinessUpdate>(data);
				return update.IsStructurallyValid() ? update : null;
			}
			catch (Exception)
			{
				// Lobby deltas are untrusted network input and must fail closed.
				return null;
			}
		}

		public static bool TryDeserialize(string data, string name, out ClientMapReadinessUpdate update)
		{
			update = null;
			try
			{
				var nodes = MiniYaml.FromString(data, name);
				if (nodes.Count != 1 || nodes[0].Key != "ClientMapReadiness")
					return false;

				update = Deserialize(nodes[0].Value);
				return update != null;
			}
			catch (Exception)
			{
				return false;
			}
		}

		public bool ApplyTo(Session session)
		{
			if (session == null || session.GlobalSettings == null || !IsStructurallyValid() ||
				!string.Equals(MapUid, session.GlobalSettings.Map, StringComparison.Ordinal))
				return false;

			var client = session.ClientWithIndex(ClientIndex);
			if (client == null)
				return false;

			client.MapUid = MapUid;
			client.MapPhase = Phase;
			client.MapProgress = Progress;
			client.State = ClientState;
			return true;
		}
	}
}
