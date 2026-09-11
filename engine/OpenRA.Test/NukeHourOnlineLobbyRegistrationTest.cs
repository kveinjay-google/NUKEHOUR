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
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenRA.Server;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NukeHourOnlineLobbyRegistrationTest
	{
		sealed class RecordingHandler : HttpMessageHandler
		{
			readonly Queue<HttpResponseMessage> responses;
			public readonly List<HttpRequestMessage> Requests = new();
			public readonly List<string> Bodies = new();

			public RecordingHandler(params HttpResponseMessage[] responses)
			{
				this.responses = new Queue<HttpResponseMessage>(responses);
			}

			protected override async Task<HttpResponseMessage> SendAsync(
				HttpRequestMessage request, CancellationToken cancellationToken)
			{
				Requests.Add(request);
				Bodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
				return responses.Dequeue();
			}
		}

		static OnlineLobbyRoomSnapshot Snapshot(DedicatedServerOperationalState state = DedicatedServerOperationalState.WaitingForPlayers) =>
			new(
				"203.0.113.10", 4567, "Official Room", "asia-east", state,
				"map-uid", "ra2", "nukehour-core-sha256-example", 2, 8, false, true,
				"release-20250330", ProtocolVersion.HandshakeSchema, ProtocolVersion.Orders,
				"ra2-presentation-capability-v2");

		static HttpResponseMessage Json(HttpStatusCode status, string json = "{}") => new(status)
		{
			Content = new StringContent(json, Encoding.UTF8, "application/json"),
		};

		[Test]
		public void LobbySettingsAreDisabledAndSecretFreeByDefault()
		{
			var settings = new ServerSettings();
			Assert.Multiple(() =>
			{
				Assert.That(settings.OnlineLobbyUrl, Is.Empty);
				Assert.That(settings.OnlineLobbyServerId, Is.Empty);
				Assert.That(typeof(ServerSettings).GetField("OnlineLobbyRegistrationCredential"), Is.Null,
					"Registration credentials must not be accepted as command-line settings.");
				Assert.That(OnlineLobbyRegistrationPolicy.CredentialEnvironmentVariable,
					Is.EqualTo("NUKEHOUR_LOBBY_REGISTRATION_CREDENTIAL"));
				Assert.That(settings.OnlineLobbyPublicEndpoint, Is.Empty);
				Assert.That(settings.OnlineLobbyPublicPort, Is.EqualTo(0));
				Assert.That(settings.OnlineLobbyRegion, Is.Empty);
				Assert.That(settings.OnlineLobbyHeartbeatSeconds, Is.EqualTo(15));
			});
		}

		[Test]
		public void RegistrationPolicyRequiresASecretFromTheDedicatedEnvironment()
		{
			var settings = new ServerSettings
			{
				OnlineLobbyUrl = "https://lobby.nukehour.com/",
				OnlineLobbyServerId = "server-a",
				OnlineLobbyPublicEndpoint = "203.0.113.10",
				OnlineLobbyPublicPort = 4567,
				OnlineLobbyRegion = "asia-east"
			};

			Assert.Multiple(() =>
			{
				Assert.That(() => OnlineLobbyRegistrationPolicy.Validate(settings, "short"),
					Throws.ArgumentException);
				Assert.That(() => OnlineLobbyRegistrationPolicy.Validate(settings, new string('s', 32)),
					Throws.Nothing);
			});
		}

		[Test]
		public void PublicHttpsAndLoopbackHttpAreTheOnlyAllowedLobbyUrls()
		{
			Assert.Multiple(() =>
			{
				Assert.That(OnlineLobbyRegistrationPolicy.ParseBaseUri("https://lobby.nukehour.com").Scheme, Is.EqualTo("https"));
				Assert.That(OnlineLobbyRegistrationPolicy.ParseBaseUri("http://127.0.0.1:8080").IsLoopback, Is.True);
				Assert.That(() => OnlineLobbyRegistrationPolicy.ParseBaseUri("http://203.0.113.10:8080"), Throws.ArgumentException);
				Assert.That(() => OnlineLobbyRegistrationPolicy.ParseBaseUri("file:///tmp/lobby"), Throws.ArgumentException);
			});
		}

		[TestCase(DedicatedServerOperationalState.Starting, false)]
		[TestCase(DedicatedServerOperationalState.WaitingForPlayers, true)]
		[TestCase(DedicatedServerOperationalState.InLobby, true)]
		[TestCase(DedicatedServerOperationalState.InGame, true)]
		[TestCase(DedicatedServerOperationalState.GameFinished, false)]
		[TestCase(DedicatedServerOperationalState.Stopping, false)]
		public void RegistrationPublishesOnlyLiveReadyLifecycleStates(
			DedicatedServerOperationalState state, bool expected)
		{
			Assert.That(OnlineLobbyRegistrationPolicy.ShouldPublish(state), Is.EqualTo(expected));
		}

		[Test]
		public async Task RegistrationAndHeartbeatUsePrivateBearerTokens()
		{
			var handler = new RecordingHandler(
				Json(HttpStatusCode.Created,
					"{\"room\":{\"roomId\":\"room-id\",\"roomCode\":\"A7K9Q2\"}," +
					"\"registrationToken\":\"session-token-that-is-private-and-long\"}"),
				Json(HttpStatusCode.OK, "{}"));
			using var http = new HttpClient(handler);
			using var client = new OnlineLobbyRegistrationClient(
				http, new Uri("https://lobby.nukehour.com/"), "official-registration-credential-long", "server-a");

			await client.UpdateAsync(Snapshot(), CancellationToken.None);
			await client.UpdateAsync(Snapshot(DedicatedServerOperationalState.InGame), CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(handler.Requests[0].RequestUri.AbsolutePath, Is.EqualTo("/v1/servers/register"));
				Assert.That(handler.Requests[0].Headers.Authorization.Parameter, Is.EqualTo("official-registration-credential-long"));
				Assert.That(handler.Requests[1].RequestUri.AbsolutePath, Is.EqualTo("/v1/servers/server-a/heartbeat"));
				Assert.That(handler.Requests[1].Headers.Authorization.Parameter, Is.EqualTo("session-token-that-is-private-and-long"));
				Assert.That(handler.Bodies[0], Does.Contain("\"handshakeSchemaVersion\":1"));
				Assert.That(handler.Bodies[0], Does.Contain("\"ordersVersion\":23"));
				Assert.That(handler.Bodies[0], Does.Not.Contain("runtimeContract"));
				Assert.That(handler.Bodies[1], Does.Contain("\"status\":\"IN_GAME\""));
			});
		}

		[Test]
		public async Task MissingLobbyRegistrationImmediatelyReregisters()
		{
			var handler = new RecordingHandler(
				Json(HttpStatusCode.Created,
					"{\"room\":{\"roomId\":\"room-id\",\"roomCode\":\"A7K9Q2\"}," +
					"\"registrationToken\":\"session-token-that-is-private-and-long\"}"),
				Json(HttpStatusCode.NotFound),
				Json(HttpStatusCode.Created,
					"{\"room\":{\"roomId\":\"room-id-2\",\"roomCode\":\"B8M3R4\"}," +
					"\"registrationToken\":\"replacement-session-token-private\"}"));
			using var client = new OnlineLobbyRegistrationClient(
				new HttpClient(handler), new Uri("https://lobby.nukehour.com/"),
				"official-registration-credential-long", "server-a");

			await client.UpdateAsync(Snapshot(), CancellationToken.None);
			await client.UpdateAsync(Snapshot(), CancellationToken.None);

			Assert.That(handler.Requests.ConvertAll(r => r.RequestUri.AbsolutePath), Is.EqualTo(new[]
			{
				"/v1/servers/register",
				"/v1/servers/server-a/heartbeat",
				"/v1/servers/register",
			}));
		}

		[Test]
		public void LobbyHttpFailureIsReturnedToTheMonitorAndNeverThrowsIntoGameplay()
		{
			var handler = new RecordingHandler(Json(HttpStatusCode.ServiceUnavailable));
			using var client = new OnlineLobbyRegistrationClient(
				new HttpClient(handler), new Uri("https://lobby.nukehour.com/"),
				"official-registration-credential-long", "server-a");

			Assert.That(async () => await client.TryUpdateAsync(Snapshot(), CancellationToken.None), Throws.Nothing);
			Assert.That(client.LastErrorCode, Is.EqualTo("LOBBY_HTTP_503"));
		}
	}
}
