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

using System.Net;
using NUnit.Framework;
using OpenRA.Mods.Common.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class LanGameProbeTest
	{
		[Test]
		public void EnablesUdpBroadcastBeforeSendingDiscoveryPackets()
		{
			using var probe = new LanGameProbe("OpenRALANGame");

			Assert.That(probe.BroadcastEnabled, Is.True);
		}

		[Test]
		public void DecodesTheExistingBeaconLibWireFormat()
		{
			var packet = LanGameProbe.EncodeResponse("OpenRALANGame", 23456, "Game:\n\tName: Test");

			Assert.That(LanGameProbe.TryDecodeResponse(
				"OpenRALANGame", packet, IPAddress.Parse("192.168.50.97"), out var location), Is.True);
			Assert.That(location.Address, Is.EqualTo(new IPEndPoint(IPAddress.Parse("192.168.50.97"), 23456)));
			Assert.That(location.Data, Is.EqualTo("Game:\n\tName: Test"));
		}

		[TestCase("192.168.50.23", "255.255.255.0", "192.168.50.255")]
		[TestCase("10.42.17.9", "255.255.0.0", "10.42.255.255")]
		public void CalculatesRoutableSubnetBroadcastAddress(string address, string mask, string expected)
		{
			var broadcast = LanGameProbe.GetDirectedBroadcast(
				IPAddress.Parse(address), IPAddress.Parse(mask));

			Assert.That(broadcast, Is.EqualTo(IPAddress.Parse(expected)));
		}
	}
}
