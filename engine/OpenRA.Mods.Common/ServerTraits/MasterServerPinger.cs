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
using BeaconLib;
using OpenRA.Network;
using OpenRA.Server;
using S = OpenRA.Server.Server;

namespace OpenRA.Mods.Common.Server
{
	public class MasterServerPinger : ServerTrait, ITick, INotifyServerStart, INotifyServerShutdown, INotifySyncLobbyInfo, IStartGame, IEndGame
	{
		// Refresh local discovery advertisements before nearby-discovery TTLs expire.
		const int DiscoveryRefreshInterval = 60 * 3 * 1000;

		// 1 second (in milliseconds) minimum delay between pings
		const int RateLimitInterval = 1000;

		static readonly Beacon LanGameBeacon;

		long lastPing = 0;
		long lastChanged = 0;

		static MasterServerPinger()
		{
			try
			{
				LanGameBeacon = new Beacon("OpenRALANGame", (ushort)new Random(DateTime.Now.Millisecond).Next(2048, 60000));
			}
			catch (Exception ex)
			{
				Log.Write("server", "BeaconLib.Beacon: " + ex.Message);
			}
		}

		public void Tick(S server)
		{
			// Force a local refresh before discovery advertisements time out.
			if (Game.RunTime - lastChanged > DiscoveryRefreshInterval)
				lastChanged = Game.RunTime;

			// Local discovery must never wait for the internet master server.
			// Publish once at startup, then whenever lobby state changes or the local TTL expires.
			if ((lastChanged > lastPing && Game.RunTime - lastPing > RateLimitInterval) || lastPing == 0)
			{
				var gs = new GameServer(server);
				var nearbyData = gs.ToPOSTData(true);

				if (LanGameBeacon != null)
					LanGameBeacon.BeaconData = nearbyData;

				NearbyGameNetworking.UpdateAdvertising(nearbyData);

				lastPing = Game.RunTime;
			}
		}

		void INotifyServerStart.ServerStarted(S server)
		{
			if (!server.IsMultiplayer)
				return;

			if (LanGameBeacon != null)
				LanGameBeacon.Start();

			NearbyGameNetworking.StartAdvertising(
				new GameServer(server).ToPOSTData(true), server.Settings.ListenPort);
		}

		void INotifyServerShutdown.ServerShutdown(S server)
		{
			LanGameBeacon?.Stop();
			NearbyGameNetworking.StopAdvertising();
		}

		public void LobbyInfoSynced(S server)
		{
			lastChanged = Game.RunTime;
		}

		public void GameStarted(S server)
		{
			lastChanged = Game.RunTime;
		}

		public void GameEnded(S server)
		{
			LanGameBeacon?.Stop();
			NearbyGameNetworking.StopAdvertising();

			lastChanged = Game.RunTime;
		}
	}
}
