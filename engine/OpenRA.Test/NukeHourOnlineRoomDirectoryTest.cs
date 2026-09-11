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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NukeHourOnlineRoomDirectoryTest
	{
		sealed class StaticHandler : HttpMessageHandler
		{
			readonly HttpResponseMessage response;
			public Uri RequestUri { get; private set; }

			public StaticHandler(HttpStatusCode status, string body)
			{
				response = new HttpResponseMessage(status)
				{
					Content = new StringContent(body, Encoding.UTF8, "application/json"),
				};
			}

			protected override Task<HttpResponseMessage> SendAsync(
				HttpRequestMessage request, CancellationToken cancellationToken)
			{
				RequestUri = request.RequestUri;
				return Task.FromResult(response);
			}
		}

		const string RoomJson = @"{
			""roomId"":""6aaf2f79-84f5-4fc8-b4df-1f1b77f3d8e6"",
			""roomCode"":""A7K9Q2"",
			""serverId"":""server-a"",
			""serverEndpoint"":""203.0.113.10"",
			""serverPort"":4567,
			""serverName"":""Official Room"",
			""region"":""asia-east"",
			""status"":""WAITING"",
			""map"":""map-uid"",
			""mod"":""ra2"",
			""modVersion"":""nukehour-core-sha256-example"",
			""players"":2,
			""maxPlayers"":8,
			""hasPassword"":false,
			""ready"":true,
			""engineCompatibility"":""release-20250330"",
			""handshakeSchemaVersion"":1,
			""ordersVersion"":23,
			""runtimeCapability"":""ra2-presentation-capability-v2"",
			""trust"":""official"",
			""createdAt"":""2026-09-09T12:00:00Z"",
			""lastHeartbeatAt"":""2026-09-09T12:00:10Z""
		}";

		static OnlineRoomCompatibilityIdentity CompatibleIdentity() => new(
			"release-20250330", "ra2", "nukehour-core-sha256-example",
			"ra2-presentation-capability-v2", 1, 23);

		[Test]
		public async Task RoomListParsesStrictMetadataAndKeepsEndpointOutOfDisplayText()
		{
			var handler = new StaticHandler(HttpStatusCode.OK, "{\"rooms\":[" + RoomJson + "]}");
			using var client = new OnlineRoomDirectoryClient(
				new HttpClient(handler), new Uri("https://lobby.nukehour.com/"));

			var rooms = await client.GetRoomsAsync(CancellationToken.None);

			Assert.That(rooms, Has.Count.EqualTo(1));
			var room = rooms[0];
			Assert.Multiple(() =>
			{
				Assert.That(room.RoomCode, Is.EqualTo("A7K9Q2"));
				Assert.That(room.Endpoint, Is.EqualTo("203.0.113.10:4567"));
				Assert.That(room.DisplaySummary, Does.Contain("Official Room"));
				Assert.That(room.DisplaySummary, Does.Contain("asia-east"));
				Assert.That(room.DisplaySummary, Does.Not.Contain("203.0.113.10"));
				Assert.That(handler.RequestUri.AbsolutePath, Is.EqualTo("/v1/rooms"));
			});
		}

		[Test]
		public async Task RoomCodeLookupNormalizesAndResolves()
		{
			var handler = new StaticHandler(HttpStatusCode.OK, RoomJson);
			using var client = new OnlineRoomDirectoryClient(
				new HttpClient(handler), new Uri("https://lobby.nukehour.com/"));

			var room = await client.ResolveCodeAsync("a7k9q2", CancellationToken.None);
			Assert.That(room.RoomCode, Is.EqualTo("A7K9Q2"));
			Assert.That(handler.RequestUri.AbsolutePath, Is.EqualTo("/v1/rooms/code/A7K9Q2"));
			Assert.That(
				async () => await client.ResolveCodeAsync("bad!", CancellationToken.None),
				Throws.TypeOf<ArgumentException>());
		}

		[TestCase("release-other", "ra2", "nukehour-core-sha256-example", "ra2-presentation-capability-v2",
			1, 23, OnlineRoomCompatibilityReason.EngineMismatch)]
		[TestCase("release-20250330", "other", "nukehour-core-sha256-example", "ra2-presentation-capability-v2",
			1, 23, OnlineRoomCompatibilityReason.ModMismatch)]
		[TestCase("release-20250330", "ra2", "other", "ra2-presentation-capability-v2",
			1, 23, OnlineRoomCompatibilityReason.ModVersionMismatch)]
		[TestCase("release-20250330", "ra2", "nukehour-core-sha256-example", "other",
			1, 23, OnlineRoomCompatibilityReason.RuntimeCapabilityMismatch)]
		[TestCase("release-20250330", "ra2", "nukehour-core-sha256-example", "ra2-presentation-capability-v2",
			2, 23, OnlineRoomCompatibilityReason.HandshakeSchemaMismatch)]
		[TestCase("release-20250330", "ra2", "nukehour-core-sha256-example", "ra2-presentation-capability-v2",
			1, 24, OnlineRoomCompatibilityReason.OrdersVersionMismatch)]
		public void PrecheckRejectsEveryPublicCompatibilityMismatch(
			string engine, string mod, string version, string capability,
			int handshake, int orders, OnlineRoomCompatibilityReason expected)
		{
			var room = OnlineRoom.Parse(RoomJson);
			var local = new OnlineRoomCompatibilityIdentity(engine, mod, version, capability, handshake, orders);
			Assert.That(OnlineRoomCompatibility.Precheck(room, local), Is.EqualTo(expected));
		}

		[Test]
		public void MatchingPrecheckIsOnlyAnOptimizationAndEndpointRemainsDirect()
		{
			var room = OnlineRoom.Parse(RoomJson);
			Assert.Multiple(() =>
			{
				Assert.That(OnlineRoomCompatibility.Precheck(room, CompatibleIdentity()), Is.EqualTo(OnlineRoomCompatibilityReason.Compatible));
				Assert.That(room.TryGetConnectionTarget(out var target), Is.True);
				Assert.That(target.ToString(), Does.Contain("203.0.113.10"));
				Assert.That(target.ToString(), Does.Contain("4567"));
				Assert.That(room.TryGetConnectionTarget(out _), Is.True,
					"a positive precheck only resolves the existing direct TCP target");
			});
		}

		[TestCase(HttpStatusCode.ServiceUnavailable, OnlineRoomDirectoryError.ServiceUnavailable)]
		[TestCase(HttpStatusCode.NotFound, OnlineRoomDirectoryError.RoomDisappeared)]
		[TestCase(HttpStatusCode.TooManyRequests, OnlineRoomDirectoryError.RateLimited)]
		public void HttpFailuresHaveStableUserFacingCategories(HttpStatusCode status, OnlineRoomDirectoryError expected)
		{
			var handler = new StaticHandler(status, "{}");
			using var client = new OnlineRoomDirectoryClient(
				new HttpClient(handler), new Uri("https://lobby.nukehour.com/"));

			var exception = Assert.ThrowsAsync<OnlineRoomDirectoryException>(
				async () => await client.GetRoomsAsync(CancellationToken.None));
			Assert.That(exception.Error, Is.EqualTo(expected));
		}

		[Test]
		public void PublicLobbyRequiresHttpsExceptForLoopbackDevelopment()
		{
			Assert.Multiple(() =>
			{
				Assert.That(OnlineRoomDirectoryClient.ParseBaseUri("https://lobby.nukehour.com").Scheme, Is.EqualTo("https"));
				Assert.That(OnlineRoomDirectoryClient.ParseBaseUri("http://localhost:8080").IsLoopback, Is.True);
				Assert.That(() => OnlineRoomDirectoryClient.ParseBaseUri("http://203.0.113.10:8080"), Throws.ArgumentException);
			});
		}
	}
}
