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
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using BeaconLib;

namespace OpenRA.Mods.Common.Network
{
	/// <summary>
	/// BeaconLib-compatible LAN probe with modern UDP broadcast handling.
	/// BeaconLib 1.0.2 does not enable SO_BROADCAST, which prevents discovery
	/// packets from leaving .NET 8 iOS devices.
	/// </summary>
	public sealed class LanGameProbe : IDisposable
	{
		const int DiscoveryPort = 35891;
		static readonly TimeSpan BeaconTimeout = TimeSpan.FromSeconds(5);

		readonly string beaconType;
		readonly UdpClient udp = new();
		readonly Thread thread;
		readonly EventWaitHandle waitHandle = new(false, EventResetMode.AutoReset);
		readonly object locationsLock = new();
		readonly IPEndPoint[] broadcastTargets;
		readonly IPEndPoint[] fallbackTargets;
		List<BeaconLocation> currentLocations = new();
		volatile bool running;
		bool started;
		int broadcastCount;
		DateTime lastFailureLogged = DateTime.MinValue;

		public bool BroadcastEnabled => udp.EnableBroadcast;

		public event Action<IEnumerable<BeaconLocation>> BeaconsUpdated = _ => { };

		public LanGameProbe(string beaconType)
		{
			this.beaconType = beaconType;
			udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			udp.EnableBroadcast = true;
			udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
			BuildDiscoveryTargets(out broadcastTargets, out fallbackTargets);
			thread = new Thread(BackgroundLoop)
			{
				IsBackground = true,
				Name = "OpenRA LAN discovery"
			};
		}

		public void Start()
		{
			if (started)
				return;

			started = true;
			running = true;
			BeginReceive();
			thread.Start();
			Log.Write("client", "LAN discovery started with targets: " +
				string.Join(", ", broadcastTargets.Select(target => target.Address)) + ".");
		}

		void BeginReceive()
		{
			try
			{
				udp.BeginReceive(ResponseReceived, null);
			}
			catch (ObjectDisposedException) { }
			catch (Exception ex)
			{
				Log.Write("client", $"LAN discovery receive failed: {ex}");
			}
		}

		void ResponseReceived(IAsyncResult result)
		{
			try
			{
				var remote = new IPEndPoint(IPAddress.Any, 0);
				var packet = udp.EndReceive(result, ref remote);
				if (TryDecodeResponse(beaconType, packet, remote.Address, out var location))
					UpdateLocation(location);
			}
			catch (ObjectDisposedException) { }
			catch (Exception ex)
			{
				Log.Write("client", $"LAN discovery response failed: {ex}");
			}
			finally
			{
				if (running)
					BeginReceive();
			}
		}

		void BackgroundLoop()
		{
			while (running)
			{
				var packet = EncodeString(beaconType);
				var sent = 0;
				Exception lastError = null;
				foreach (var target in broadcastTargets)
				{
					try
					{
						udp.Send(packet, packet.Length, target);
						sent++;
					}
					catch (Exception ex)
					{
						lastError = ex;
					}
				}

				// Some access points suppress client-to-client broadcasts. A low-frequency
				// /24 unicast sweep keeps discovery working without touching the game port.
				if (Platform.IsIOS && ++broadcastCount % 5 == 0)
				{
					foreach (var target in fallbackTargets)
					{
						try
						{
							udp.Send(packet, packet.Length, target);
						}
						catch { }
					}
				}

				if (sent == 0 && DateTime.UtcNow - lastFailureLogged > TimeSpan.FromSeconds(30))
				{
					lastFailureLogged = DateTime.UtcNow;
					Log.Write("client", $"LAN discovery could not route any broadcast target: {lastError?.Message}");
				}

				waitHandle.WaitOne(2000);
				PruneLocations();
			}
		}

		void UpdateLocation(BeaconLocation location)
		{
			List<BeaconLocation> snapshot;
			bool changed;
			lock (locationsLock)
			{
				var previous = currentLocations.FirstOrDefault(candidate => candidate.Address.Equals(location.Address));
				changed = previous == null || previous.Data != location.Data;
				currentLocations.RemoveAll(candidate => candidate.Address.Equals(location.Address));
				currentLocations.Add(location);
				snapshot = currentLocations.OrderBy(candidate => candidate.Data)
					.ThenBy(candidate => candidate.Address.ToString()).ToList();
				currentLocations = snapshot;
			}

			if (changed)
			{
				Log.Write("client", $"LAN game discovered at {location.Address}.");
				BeaconsUpdated(snapshot);
			}
		}

		void PruneLocations()
		{
			List<BeaconLocation> snapshot = null;
			lock (locationsLock)
			{
				var cutoff = DateTime.Now - BeaconTimeout;
				var active = currentLocations.Where(location => location.LastAdvertised >= cutoff).ToList();
				if (active.Count != currentLocations.Count)
				{
					currentLocations = active;
					snapshot = active;
				}
			}

			if (snapshot != null)
				BeaconsUpdated(snapshot);
		}

		public static bool TryDecodeResponse(
			string beaconType, byte[] packet, IPAddress address, out BeaconLocation location)
		{
			location = null;
			var typePrefix = EncodeString(beaconType);
			if (packet == null || packet.Length < typePrefix.Length + 4 ||
				!packet.Take(typePrefix.Length).SequenceEqual(typePrefix))
				return false;

			var offset = typePrefix.Length;
			var port = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packet, offset));
			offset += 2;
			var dataLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packet, offset));
			offset += 2;
			if (dataLength < 0 || packet.Length < offset + dataLength)
				return false;

			var data = Encoding.UTF8.GetString(packet, offset, dataLength);
			location = new BeaconLocation(new IPEndPoint(address, port), data, DateTime.Now);
			return true;
		}

		public static byte[] EncodeResponse(string beaconType, ushort port, string data)
		{
			var prefix = EncodeString(beaconType);
			var encodedData = EncodeString(data);
			var encodedPort = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)port));
			return prefix.Concat(encodedPort).Concat(encodedData).ToArray();
		}

		static byte[] EncodeString(string value)
		{
			var bytes = Encoding.UTF8.GetBytes(value);
			var length = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)bytes.Length));
			return length.Concat(bytes).ToArray();
		}

		static void BuildDiscoveryTargets(
			out IPEndPoint[] broadcasts, out IPEndPoint[] fallback)
		{
			var broadcastAddresses = new HashSet<IPAddress>();
			var fallbackAddresses = new HashSet<IPAddress>();
			try
			{
				foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
				{
					if (network.OperationalStatus != OperationalStatus.Up ||
						network.NetworkInterfaceType == NetworkInterfaceType.Loopback)
						continue;

					foreach (var unicast in network.GetIPProperties().UnicastAddresses)
					{
						var address = unicast.Address;
						if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
							continue;

						if (unicast.IPv4Mask != null)
							broadcastAddresses.Add(GetDirectedBroadcast(address, unicast.IPv4Mask));

						if (IsPrivate(address))
						{
							var bytes = address.GetAddressBytes();
							for (var host = 1; host < 255; host++)
							{
								if (host == bytes[3])
									continue;

								fallbackAddresses.Add(new IPAddress(new[] { bytes[0], bytes[1], bytes[2], (byte)host }));
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Write("client", $"LAN discovery interface enumeration failed: {ex.Message}");
			}

			// Keep the legacy limited broadcast as a final compatibility target.
			broadcastAddresses.Add(IPAddress.Broadcast);
			broadcasts = broadcastAddresses.Select(address => new IPEndPoint(address, DiscoveryPort)).ToArray();
			fallback = fallbackAddresses.Select(address => new IPEndPoint(address, DiscoveryPort)).ToArray();
		}

		static bool IsPrivate(IPAddress address)
		{
			var bytes = address.GetAddressBytes();
			return bytes[0] == 10 ||
				(bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
				(bytes[0] == 192 && bytes[1] == 168);
		}

		public static IPAddress GetDirectedBroadcast(IPAddress address, IPAddress mask)
		{
			var addressBytes = address.GetAddressBytes();
			var maskBytes = mask.GetAddressBytes();
			if (addressBytes.Length != 4 || maskBytes.Length != 4)
				throw new ArgumentException("LAN discovery requires IPv4 addresses and masks.");

			var result = new byte[4];
			for (var i = 0; i < result.Length; i++)
				result[i] = (byte)(addressBytes[i] | ~maskBytes[i]);

			return new IPAddress(result);
		}

		public void Dispose()
		{
			running = false;
			waitHandle.Set();
			if (started && thread.IsAlive)
				thread.Join();

			udp.Dispose();
			waitHandle.Dispose();
		}
	}
}
