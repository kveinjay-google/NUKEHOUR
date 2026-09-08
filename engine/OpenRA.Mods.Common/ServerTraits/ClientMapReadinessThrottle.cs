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
using System.Linq;
using OpenRA.Network;

namespace OpenRA.Mods.Common.Server
{
	public sealed class ClientMapReadinessThrottle
	{
		sealed class ClientState
		{
			public ClientMapReadinessUpdate LastBroadcast;
			public ClientMapReadinessUpdate Pending;
			public long LastBroadcastAt;
		}

		readonly long minimumIntervalMilliseconds;
		readonly Dictionary<int, ClientState> clients = new();

		public ClientMapReadinessThrottle(long minimumIntervalMilliseconds)
		{
			if (minimumIntervalMilliseconds <= 0)
				throw new ArgumentOutOfRangeException(nameof(minimumIntervalMilliseconds));

			this.minimumIntervalMilliseconds = minimumIntervalMilliseconds;
		}

		static ClientMapReadinessUpdate Copy(ClientMapReadinessUpdate update)
		{
			return new ClientMapReadinessUpdate
			{
				ClientIndex = update.ClientIndex,
				MapUid = update.MapUid,
				Phase = update.Phase,
				Progress = update.Progress,
				ClientState = update.ClientState
			};
		}

		static bool Equal(ClientMapReadinessUpdate a, ClientMapReadinessUpdate b)
		{
			return a != null && b != null && a.ClientIndex == b.ClientIndex &&
				string.Equals(a.MapUid, b.MapUid, StringComparison.Ordinal) &&
				a.Phase == b.Phase && a.Progress == b.Progress && a.ClientState == b.ClientState;
		}

		public void Submit(ClientMapReadinessUpdate update, long now)
		{
			if (update == null)
				throw new ArgumentNullException(nameof(update));
			if (update.ClientIndex < 0)
				throw new ArgumentOutOfRangeException(nameof(update));

			if (!clients.TryGetValue(update.ClientIndex, out var state))
				clients.Add(update.ClientIndex, state = new ClientState());

			var snapshot = Copy(update);
			if (Equal(state.Pending, snapshot))
				return;

			if (Equal(state.LastBroadcast, snapshot))
			{
				state.Pending = null;
				return;
			}

			state.Pending = snapshot;
		}

		public IReadOnlyList<ClientMapReadinessUpdate> Drain(long now)
		{
			var updates = new List<ClientMapReadinessUpdate>();
			foreach (var pair in clients.OrderBy(pair => pair.Key))
			{
				var state = pair.Value;
				if (state.Pending == null || (state.LastBroadcast != null &&
					now - state.LastBroadcastAt < minimumIntervalMilliseconds))
					continue;

				state.LastBroadcast = Copy(state.Pending);
				state.Pending = null;
				state.LastBroadcastAt = now;
				updates.Add(Copy(state.LastBroadcast));
			}

			return updates;
		}

		public void Remove(int clientIndex)
		{
			clients.Remove(clientIndex);
		}

		public void Retain(IEnumerable<int> clientIndexes)
		{
			if (clientIndexes == null)
				throw new ArgumentNullException(nameof(clientIndexes));

			var retained = new HashSet<int>(clientIndexes);
			foreach (var clientIndex in clients.Keys.Where(index => !retained.Contains(index)).ToArray())
				clients.Remove(clientIndex);
		}

		public void Reset()
		{
			clients.Clear();
		}
	}
}
