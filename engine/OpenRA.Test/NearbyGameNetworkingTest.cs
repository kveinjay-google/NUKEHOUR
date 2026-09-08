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
using System.Net;
using NUnit.Framework;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NearbyGameNetworkingTest
	{
		sealed class FakeNearbyGameService : INearbyGameService
		{
			public event Action GamesChanged = () => { };
			public IReadOnlyList<NearbyGameInfo> Games => Array.Empty<NearbyGameInfo>();
			public void StartBrowsing() { }
			public void StopBrowsing() { }
			public void StartAdvertising(string payload, int localServerPort) { }
			public void UpdateAdvertising(string payload) { }
			public void StopAdvertising() { }
			public bool TryCreateConnectionTarget(string serviceId, out ConnectionTarget target)
			{
				target = null;
				return false;
			}

			public void Suspend() { }
			public void Resume() { }
			public void Dispose() { }
		}

		[TearDown]
		public void TearDown()
		{
			NearbyGameNetworking.Shutdown();
		}

		[Test]
		public void PayloadCodecRoundTripsUnicodeAndLargeLobbyData()
		{
			var payload = "Game:\n\tName: 红色警戒附近联机\n\tMap: " + new string('地', 4096);
			var encoded = NearbyGamePayloadCodec.Encode(payload);

			Assert.That(encoded.Keys, Does.Contain("v"));
			Assert.That(encoded.Keys, Does.Contain("h"));
			Assert.That(NearbyGamePayloadCodec.TryDecode(encoded, out var decoded, out var error), Is.True, error);
			Assert.That(decoded, Is.EqualTo(payload));
		}

		[Test]
		public void PayloadCodecRejectsTampering()
		{
			var encoded = new Dictionary<string, string>(NearbyGamePayloadCodec.Encode("Game:\n\tName: Test"));
			encoded["h"] = "0000000000000000";

			Assert.That(NearbyGamePayloadCodec.TryDecode(encoded, out _, out var error), Is.False);
			Assert.That(error, Does.Contain("checksum"));
		}

		[TestCase("nearby://openra-123", "openra-123")]
		[TestCase("nearby://OpenRA%20Room", "OpenRA Room")]
		public void NearbyAddressesRoundTrip(string address, string expectedServiceId)
		{
			Assert.That(NearbyGameNetworking.TryGetServiceId(address, out var serviceId), Is.True);
			Assert.That(serviceId, Is.EqualTo(expectedServiceId));
			Assert.That(NearbyGameNetworking.FormatAddress(serviceId), Is.EqualTo(address));
		}

		[TestCase("192.168.1.2:1234", "192.168.1.2", 1234)]
		[TestCase("[fe80::1234]:5678", "fe80::1234", 5678)]
		public void ConnectionTargetsParseIpv4AndIpv6(string address, string expectedHost, int expectedPort)
		{
			Assert.That(ConnectionTarget.TryParseGameAddress(address, out var target), Is.True);
			Assert.That(target.ToString(), Is.EqualTo($"{expectedHost}:{expectedPort}"));
		}

		[Test]
		public void InstalledNearbyServiceReportsItsActualAssemblyBuildIdentity()
		{
			NearbyGameNetworking.InstallService(new FakeNearbyGameService());
			var module = typeof(FakeNearbyGameService).Assembly.ManifestModule;

			Assert.That(NearbyGameNetworking.ServiceBuildIdentity,
				Does.Contain(module.ModuleVersionId.ToString("N")));
			Assert.That(NearbyGameNetworking.ServiceBuildIdentity,
				Does.StartWith(module.Assembly.GetName().Name + ":"));
		}

		[Test]
		public void LanDiscoveryProtocolMatchesBeaconWireFormatAndRoundTripsServiceIds()
		{
			var query = NearbyLanDiscoveryProtocol.EncodeQuery();
			Assert.That(query, Is.EqualTo(new byte[]
			{
				0, 13,
				(byte)'O', (byte)'p', (byte)'e', (byte)'n', (byte)'R', (byte)'A',
				(byte)'L', (byte)'A', (byte)'N', (byte)'G', (byte)'a', (byte)'m', (byte)'e'
			}));

			const string payload = "Game:\n\tName: 红色警戒附近联机\n\tAddress: 0.0.0.0:1234";
			var source = IPAddress.Parse("192.168.50.97");
			var response = NearbyLanDiscoveryProtocol.EncodeResponse(23456, payload);
			Assert.That(NearbyLanDiscoveryProtocol.TryDecodeResponse(
				response, out var beaconInstancePort, out var decoded), Is.True);
			Assert.That(beaconInstancePort, Is.EqualTo(23456));
			Assert.That(decoded, Is.EqualTo(payload));
			Assert.That(NearbyLanDiscoveryProtocol.TryDecodeGameAdvertisement(
				response, source, out var endpoint, out decoded), Is.True);
			Assert.That(endpoint, Is.EqualTo(new IPEndPoint(source, 1234)),
				"The random BeaconLib instance port must never be used as the game TCP port.");

			var serviceId = NearbyLanDiscoveryProtocol.FormatServiceId(endpoint);
			Assert.That(NearbyLanDiscoveryProtocol.IsServiceId(serviceId), Is.True);
			Assert.That(NearbyLanDiscoveryProtocol.TryParseServiceId(
				serviceId, out var parsedEndpoint), Is.True);
			Assert.That(parsedEndpoint, Is.EqualTo(endpoint));
		}

		[Test]
		public void LanDiscoveryProtocolRejectsTruncatedOrTamperedPacketsAndServiceIds()
		{
			var response = NearbyLanDiscoveryProtocol.EncodeResponse(1234, "Game:\n\tName: Test");
			Array.Resize(ref response, response.Length - 1);
			Assert.That(NearbyLanDiscoveryProtocol.TryDecodeResponse(
				response, out _, out _), Is.False);

			Assert.That(NearbyLanDiscoveryProtocol.TryDecodeResponse(
				NearbyLanDiscoveryProtocol.EncodeQuery(), out _, out _), Is.False);
			Assert.That(NearbyLanDiscoveryProtocol.TryDecodeGameAdvertisement(
				NearbyLanDiscoveryProtocol.EncodeResponse(23456, "Game:\n\tAddress: 0.0.0.0:0"),
				IPAddress.Loopback, out _, out _), Is.False);
			Assert.That(NearbyLanDiscoveryProtocol.TryParseServiceId("lan.not-base64!", out _), Is.False);
			Assert.That(NearbyLanDiscoveryProtocol.TryParseServiceId("bonjour-service", out _), Is.False);
		}

		[Test]
		public void LanDiscoveryTargetsRespectSmallAndCrossBoundarySubnets()
		{
			var small = NearbyLanDiscoveryProtocol.BuildUnicastTargets(
				IPAddress.Parse("192.168.50.10"), IPAddress.Parse("255.255.255.240"));
			Assert.That(small, Has.Count.EqualTo(13));
			Assert.That(small, Does.Contain(IPAddress.Parse("192.168.50.1")));
			Assert.That(small, Does.Contain(IPAddress.Parse("192.168.50.14")));
			Assert.That(small, Does.Not.Contain(IPAddress.Parse("192.168.50.10")));

			var crossBoundary = NearbyLanDiscoveryProtocol.BuildUnicastTargets(
				IPAddress.Parse("192.168.50.10"), IPAddress.Parse("255.255.254.0"));
			Assert.That(crossBoundary, Does.Contain(IPAddress.Parse("192.168.51.10")));

			var capped = NearbyLanDiscoveryProtocol.BuildUnicastTargets(
				IPAddress.Parse("10.23.45.67"), IPAddress.Parse("255.255.0.0"));
			Assert.That(capped, Has.Count.LessThanOrEqualTo(253));
			Assert.That(capped, Has.All.Matches<IPAddress>(address =>
				address.GetAddressBytes()[0] == 10 && address.GetAddressBytes()[1] == 23 &&
				address.GetAddressBytes()[2] == 45));
		}
	}
}
