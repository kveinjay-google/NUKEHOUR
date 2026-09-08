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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Server;
using OpenRA.Network;
using OpenRA.Server;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosLobbyStartAvailabilityTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !File.Exists(Path.Combine(
				directory.FullName, "engine", "OpenRA.Game", "Game.cs")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null);
			return directory!.FullName;
		}

		static string MethodBody(string source, string signature)
		{
			var signatureStart = source.IndexOf(signature, StringComparison.Ordinal);
			Assert.That(signatureStart, Is.GreaterThanOrEqualTo(0), $"Missing method '{signature}'.");
			var bodyStart = source.IndexOf('{', signatureStart);
			Assert.That(bodyStart, Is.GreaterThan(signatureStart));

			var depth = 0;
			for (var i = bodyStart; i < source.Length; i++)
			{
				if (source[i] == '{')
					depth++;
				else if (source[i] == '}' && --depth == 0)
					return source.Substring(bodyStart, i - bodyStart + 1);
			}

			Assert.Fail($"Unterminated method '{signature}'.");
			return string.Empty;
		}

		sealed class RecordingConnection : IConnection
		{
			public int LocalClientId { get; set; } = 1;
			public void StartGame() { }
			public void Send(int frame, IEnumerable<Order> orders) { }
			public void SendImmediate(IEnumerable<Order> orders) { }
			public void SendSync(int frame, int syncHash, ulong defeatState) { }
			public void Receive(OrderManager orderManager) { }
			public void Dispose() { }
		}

		static void ProcessUnitOrder(OrderManager orderManager, int clientId, Order order)
		{
			var processOrder = typeof(UnitOrders).GetMethod("ProcessOrder",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(processOrder, Is.Not.Null);
			processOrder!.Invoke(null, new object[] { orderManager, null, clientId, order });
		}

		static Session.Client Human(int index, string slot = null,
			Session.ClientMapPhase phase = Session.ClientMapPhase.Ready, int progress = 100,
			Session.ClientState state = Session.ClientState.NotReady, bool admin = false)
		{
			return new Session.Client
			{
				Index = index,
				Name = $"Player {index}",
				Slot = slot,
				MapUid = "map-a",
				MapPhase = phase,
				MapProgress = progress,
				State = state,
				IsAdmin = admin
			};
		}

		static Session PlayableSession()
		{
			var session = new Session
			{
				GlobalSettings =
				{
					Map = "map-a",
					MapStatus = Session.MapStatus.Playable,
					EnableSingleplayer = true
				}
			};
			session.Slots.Add("P1", new Session.Slot { PlayerReference = "P1", Required = true });
			session.Clients.Add(Human(1, "P1", admin: true));
			return session;
		}

		static LobbyStartInputs Inputs(Session session, LobbyStartMode mode = LobbyStartMode.SafeDirect)
		{
			return new LobbyStartInputs
			{
				LobbyInfo = session,
				Mode = mode,
				RequesterIsAdmin = true,
				RequireCurrentMapReadiness = true,
				InsufficientEnabledSpawnPoints = false
			};
		}

		[Test]
		public void LocalMapAvailabilityIsAConservativeOverlay()
		{
			var allowed = LobbyStartAvailability.Evaluate(Inputs(PlayableSession()));
			var identity = allowed.ApplyLocalMapAvailability(true);
			var unavailable = allowed.ApplyLocalMapAvailability(false);

			Assert.Multiple(() =>
			{
				Assert.That(allowed.CanStart, Is.True);
				Assert.That(identity, Is.EqualTo(allowed));
				Assert.That(unavailable.CanStart, Is.False);
				Assert.That(unavailable.Reason, Is.EqualTo(Session.LobbyStartBlockReason.SelectedMapUnavailable));
			});

			var blockedSession = PlayableSession();
			blockedSession.GlobalSettings.MapStatus = Session.MapStatus.Validating;
			var blocked = LobbyStartAvailability.Evaluate(Inputs(blockedSession));
			var overlaid = blocked.ApplyLocalMapAvailability(false);
			Assert.Multiple(() =>
			{
				Assert.That(overlaid.Reason, Is.EqualTo(blocked.Reason));
				Assert.That(overlaid.ClientIndex, Is.EqualTo(blocked.ClientIndex));
				Assert.That(overlaid.ClientMapPhase, Is.EqualTo(blocked.ClientMapPhase));
				Assert.That(overlaid.Progress, Is.EqualTo(blocked.Progress));
				Assert.That(overlaid.AdditionalBlockingClientCount,
					Is.EqualTo(blocked.AdditionalBlockingClientCount));
			});
		}

		[TestCase(Session.MapStatus.Validating, Session.LobbyStartBlockReason.ServerMapValidating)]
		[TestCase(Session.MapStatus.Incompatible, Session.LobbyStartBlockReason.ServerMapIncompatible)]
		[TestCase(Session.MapStatus.Unknown, Session.LobbyStartBlockReason.ServerMapNotPlayable)]
		public void ServerMapStatusBlocksBeforeClientAndLobbyRules(
			Session.MapStatus status, Session.LobbyStartBlockReason expected)
		{
			var session = PlayableSession();
			session.GlobalSettings.MapStatus = status;
			session.Clients[0].MapPhase = Session.ClientMapPhase.Error;
			session.Clients[0].MapProgress = -1;
			session.Slots["P1"].Required = true;
			session.Clients[0].Slot = null;

			var result = LobbyStartAvailability.Evaluate(Inputs(session));

			Assert.That(result.Reason, Is.EqualTo(expected));
		}

		[Test]
		public void ClientMapBlockersUseStablePriorityAndIncludeNoSlotHumans()
		{
			var session = PlayableSession();
			session.Clients.Add(Human(2, null, Session.ClientMapPhase.Downloading, 42));
			session.Clients.Add(Human(3, "P2", Session.ClientMapPhase.InstallingOrVerifying, -1));
			session.Clients.Add(Human(4, "P3", Session.ClientMapPhase.Error, -1));
			session.Clients.Add(new Session.Client
			{
				Index = 5,
				Slot = "P4",
				Bot = "normal",
				MapPhase = Session.ClientMapPhase.Error,
				MapProgress = -1,
				State = Session.ClientState.Invalid
			});

			var result = LobbyStartAvailability.EvaluateMapSafety(session, true);

			Assert.Multiple(() =>
			{
				Assert.That(result.Reason, Is.EqualTo(Session.LobbyStartBlockReason.ClientMapError));
				Assert.That(result.ClientIndex, Is.EqualTo(4));
				Assert.That(result.ClientMapPhase, Is.EqualTo(Session.ClientMapPhase.Error));
				Assert.That(result.Progress, Is.EqualTo(-1));
				Assert.That(result.AdditionalBlockingClientCount, Is.EqualTo(2));
			});

			session.Clients.RemoveAll(client => client.Index is 3 or 4);
			result = LobbyStartAvailability.EvaluateMapSafety(session, true);
			Assert.That(result.ClientIndex, Is.EqualTo(2), "A transient no-slot human must still block safe start.");
			Assert.That(result.Progress, Is.EqualTo(42));
		}

		[Test]
		public void LobbyRulesFollowMapSafetyInStableOrder()
		{
			var session = PlayableSession();
			session.Clients[0].Slot = null;
			var result = LobbyStartAvailability.Evaluate(Inputs(session));
			Assert.That(result.Reason, Is.EqualTo(Session.LobbyStartBlockReason.RequiredSlotEmpty));

			session.Slots["P1"].Required = false;
			result = LobbyStartAvailability.Evaluate(Inputs(session));
			Assert.That(result.Reason, Is.EqualTo(Session.LobbyStartBlockReason.NoPlayers));

			session.Clients[0].Slot = "P1";
			session.GlobalSettings.EnableSingleplayer = false;
			result = LobbyStartAvailability.Evaluate(Inputs(session));
			Assert.That(result.Reason, Is.EqualTo(Session.LobbyStartBlockReason.InsufficientHumans));

			session.Clients.Add(Human(2, "P2"));
			var spawnInputs = Inputs(session);
			spawnInputs.InsufficientEnabledSpawnPoints = true;
			result = LobbyStartAvailability.Evaluate(spawnInputs);
			Assert.That(result.Reason, Is.EqualTo(Session.LobbyStartBlockReason.InsufficientSpawnPoints));
		}

		[Test]
		public void DirectAndConfirmedModesIgnoreOnlyOrdinaryGameNotReady()
		{
			var session = PlayableSession();
			Assert.That(session.Clients.Single().State, Is.EqualTo(Session.ClientState.NotReady));
			Assert.That(LobbyStartAvailability.Evaluate(Inputs(session, LobbyStartMode.SafeDirect)).CanStart, Is.True);
			Assert.That(LobbyStartAvailability.Evaluate(Inputs(session, LobbyStartMode.ForceConfirmed)).CanStart, Is.True);

			var automatic = LobbyStartAvailability.Evaluate(Inputs(session, LobbyStartMode.Automatic));
			Assert.That(automatic.Reason, Is.EqualTo(Session.LobbyStartBlockReason.ClientGameNotReady));

			session.Clients[0].State = Session.ClientState.Ready;
			Assert.That(LobbyStartAvailability.Evaluate(Inputs(session, LobbyStartMode.Automatic)).CanStart, Is.True);

			session.Clients[0].MapPhase = Session.ClientMapPhase.Downloading;
			session.Clients[0].MapProgress = 42;
			session.Clients[0].State = Session.ClientState.Invalid;
			foreach (var mode in new[] { LobbyStartMode.SafeDirect, LobbyStartMode.ForceConfirmed, LobbyStartMode.Automatic })
				Assert.That(LobbyStartAvailability.Evaluate(Inputs(session, mode)).CanStart, Is.False,
					$"{mode} must not bypass current-map readiness.");
		}

		[Test]
		public void RequesterAndReadinessRequirementRemainExplicitInputs()
		{
			var session = PlayableSession();
			var inputs = Inputs(session);
			inputs.RequesterIsAdmin = false;
			Assert.That(LobbyStartAvailability.Evaluate(inputs).Reason,
				Is.EqualTo(Session.LobbyStartBlockReason.RequesterNotAdmin));

			session.Clients[0].MapPhase = Session.ClientMapPhase.Unknown;
			session.Clients[0].MapProgress = -1;
			session.Clients[0].State = Session.ClientState.Invalid;
			inputs = Inputs(session);
			inputs.RequireCurrentMapReadiness = false;
			Assert.That(LobbyStartAvailability.Evaluate(inputs).CanStart, Is.True,
				"Local and Skirmish must keep their trusted state path without a network handshake.");
		}

		[Test]
		public void StructuredRejectionRoundTripsAndFailsClosed()
		{
			var rejection = new LobbyStartRejection(
				Session.LobbyStartBlockReason.ClientMapDownloading,
				5,
				Session.ClientMapPhase.Downloading,
				42,
				"map-a",
				7);
			var serialized = rejection.Serialize();
			var roundTrip = LobbyStartRejection.Deserialize(serialized);

			Assert.Multiple(() =>
			{
				Assert.That(serialized, Is.EqualTo(
					"Reason: ClientMapDownloading\n" +
					"ClientIndex: 5\n" +
					"ClientMapPhase: Downloading\n" +
					"Progress: 42\n" +
					"ExpectedMapUid: map-a\n" +
					"RequestId: 7\n"));
				Assert.That(roundTrip, Is.Not.Null);
				Assert.That(roundTrip.Reason, Is.EqualTo(rejection.Reason));
				Assert.That(roundTrip.ClientIndex, Is.EqualTo(5));
				Assert.That(roundTrip.ClientMapPhase, Is.EqualTo(Session.ClientMapPhase.Downloading));
				Assert.That(roundTrip.Progress, Is.EqualTo(42));
				Assert.That(roundTrip.ExpectedMapUid, Is.EqualTo("map-a"));
				Assert.That(roundTrip.RequestId, Is.EqualTo(7));
			});

			var invalid = new[]
			{
				serialized.Replace("RequestId: 7\n", string.Empty),
				serialized + "RequestId: 8\n",
				serialized + "Unexpected: value\n",
				serialized.Replace("RequestId: 7", "RequestId: 7\n\tNested: value"),
				serialized.Replace("RequestId: 7", "RequestId: 0"),
				serialized.Replace("RequestId: 7", "RequestId: -1"),
				serialized.Replace("RequestId: 7", "RequestId: 01"),
				serialized.Replace("ClientIndex: 5", "ClientIndex: +5"),
				serialized.Replace("ClientIndex: 5", "ClientIndex: 05"),
				serialized.Replace("Progress: 42", "Progress: +42"),
				serialized.Replace("Progress: 42", "Progress: 042"),
				serialized.Replace("Downloading", "downloading"),
				serialized.Replace("Progress: 42", "Progress: 100")
			};
			Assert.That(invalid.Select(LobbyStartRejection.Deserialize), Is.All.Null);

			var serverBlock = new LobbyStartRejection(
				Session.LobbyStartBlockReason.StaleMap,
				-1,
				Session.ClientMapPhase.Unknown,
				-1,
				"map-a",
				8);
			Assert.That(LobbyStartRejection.Deserialize(serverBlock.Serialize()), Is.Not.Null,
				"Canonical signed -1 fields must remain valid.");
			Assert.That(LobbyStartRejection.Deserialize(
				serverBlock.Serialize().Replace("ClientIndex: -1", "ClientIndex: -01")), Is.Null);
			Assert.That(LobbyStartRejection.Deserialize(
				serverBlock.Serialize().Replace("Progress: -1", "Progress: -01")), Is.Null);
		}

		[Test]
		public void SafeStartRequestRoundTripsCanonicalCommand()
		{
			var request = new LobbySafeStartRequest("map-a", 7);

			Assert.Multiple(() =>
			{
				Assert.That(request.ToCommand(), Is.EqualTo("startgame_safe map-a 7"));
				Assert.That(LobbySafeStartRequest.TryParse("map-a 7", out var parsed), Is.True);
				Assert.That(parsed, Is.EqualTo(request));
				Assert.That(request, Is.EqualTo(new LobbySafeStartRequest("map-a", 7)));
				Assert.That(request, Is.Not.EqualTo(new LobbySafeStartRequest("map-a", 8)));
			});
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase(" ")]
		[TestCase("map a")]
		public void SafeStartRequestConstructorRejectsInvalidMapUid(string mapUid)
		{
			Assert.Throws<ArgumentException>(() => new LobbySafeStartRequest(mapUid, 1));
		}

		[TestCase(0)]
		[TestCase(-1)]
		public void SafeStartRequestConstructorRejectsNonPositiveId(long requestId)
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => new LobbySafeStartRequest("map-a", requestId));
		}

		[TestCase("map-a 1", true)]
		[TestCase("map-a 9223372036854775807", true)]
		[TestCase("map-a 0", false)]
		[TestCase("map-a -1", false)]
		[TestCase("map-a +1", false)]
		[TestCase("map-a 01", false)]
		[TestCase("map-a 9223372036854775808", false)]
		[TestCase("map a 1", false)]
		[TestCase("map-a 1 extra", false)]
		[TestCase("map-a", false)]
		[TestCase("map-a  1", false)]
		[TestCase(" map-a 1", false)]
		[TestCase("map-a 1 ", false)]
		[TestCase("", false)]
		public void SafeStartRequestParserRequiresExactlyTwoCanonicalFields(string value, bool expected)
		{
			Assert.That(LobbySafeStartRequest.TryParse(value, out _), Is.EqualTo(expected));
		}

		[Test]
		public void AvailabilityAndRejectionConversionsPreserveWireIdentityAndBlocker()
		{
			var session = PlayableSession();
			session.Clients[0].MapPhase = Session.ClientMapPhase.Downloading;
			session.Clients[0].MapProgress = 42;
			session.Clients[0].State = Session.ClientState.Invalid;
			var availability = LobbyStartAvailability.Evaluate(Inputs(session));
			var request = new LobbySafeStartRequest("map-a", 11);
			var rejection = availability.ToRejection(request);
			var converted = rejection.ToAvailability();

			Assert.Multiple(() =>
			{
				Assert.That(rejection.ExpectedMapUid, Is.EqualTo(request.ExpectedMapUid));
				Assert.That(rejection.RequestId, Is.EqualTo(request.RequestId));
				Assert.That(converted.Reason, Is.EqualTo(availability.Reason));
				Assert.That(converted.ClientIndex, Is.EqualTo(availability.ClientIndex));
				Assert.That(converted.ClientMapPhase, Is.EqualTo(availability.ClientMapPhase));
				Assert.That(converted.Progress, Is.EqualTo(availability.Progress));
				Assert.That(converted.AdditionalBlockingClientCount, Is.Zero);
			});
			Assert.Throws<InvalidOperationException>(() => LobbyStartAvailability.Allowed.ToRejection(request));
		}

		static LobbyStartRejection DownloadRejection(long requestId, string mapUid = "map-a", int clientIndex = 5)
		{
			return new LobbyStartRejection(
				Session.LobbyStartBlockReason.ClientMapDownloading,
				clientIndex,
				Session.ClientMapPhase.Downloading,
				42,
				mapUid,
				requestId);
		}

		[Test]
		public void SafeStartStateAcceptsOnlyTheExactPendingRequest()
		{
			var state = new LobbySafeStartRequestState(0);
			var request = state.Begin("map-a");
			var accepted = DownloadRejection(request.RequestId);

			Assert.Multiple(() =>
			{
				Assert.That(request.RequestId, Is.EqualTo(1));
				Assert.That(state.IsPending, Is.True);
				Assert.That(state.PendingRequestId, Is.EqualTo(1));
				Assert.That(state.TryAcceptRejection(accepted, "map-a"), Is.True);
				Assert.That(state.IsPending, Is.False);
				Assert.That(state.PendingRequestId, Is.Zero);
				Assert.That(state.Rejection, Is.SameAs(accepted));
				Assert.That(state.TryAcceptRejection(accepted, "map-a"), Is.False,
					"An unsolicited duplicate must not mutate cached state.");
			});
		}

		[Test]
		public void SafeStartStateIgnoresMismatchedAndPostTimeoutRejections()
		{
			var state = new LobbySafeStartRequestState(0);
			var request = state.Begin("map-a");

			Assert.Multiple(() =>
			{
				Assert.That(state.TryAcceptRejection(null, "map-a"), Is.False);
				Assert.That(state.TryAcceptRejection(DownloadRejection(request.RequestId + 1), "map-a"), Is.False);
				Assert.That(state.TryAcceptRejection(DownloadRejection(request.RequestId, "map-b"), "map-a"), Is.False);
				Assert.That(state.TryAcceptRejection(DownloadRejection(request.RequestId), "map-b"), Is.False);
				Assert.That(state.IsPending, Is.True);
				Assert.That(state.PendingRequestId, Is.EqualTo(request.RequestId));
				Assert.That(state.Rejection, Is.Null);
			});

			Assert.That(state.TryTimeout(request.RequestId), Is.True);
			Assert.That(state.TryAcceptRejection(DownloadRejection(request.RequestId), "map-a"), Is.False);
			Assert.That(state.Rejection, Is.Null);
		}

		[Test]
		public void DelayedRejectionAndWrongTimeoutCannotClearNewerRequest()
		{
			var state = new LobbySafeStartRequestState(0);
			var first = state.Begin("map-a");
			Assert.That(state.TryTimeout(first.RequestId), Is.True);
			var second = state.Begin("map-a");

			Assert.Multiple(() =>
			{
				Assert.That(second.RequestId, Is.EqualTo(first.RequestId + 1));
				Assert.That(state.TryAcceptRejection(DownloadRejection(first.RequestId), "map-a"), Is.False);
				Assert.That(state.TryTimeout(first.RequestId), Is.False);
				Assert.That(state.IsPending, Is.True);
				Assert.That(state.PendingRequestId, Is.EqualTo(second.RequestId));
			});
		}

		[Test]
		public void ReadinessAndSameMapSyncPreservePendingThenMatchingRejectionWins()
		{
			var state = new LobbySafeStartRequestState(0);
			var request = state.Begin("map-a");

			Assert.Multiple(() =>
			{
				Assert.That(state.ObserveReadinessChange(5), Is.False);
				Assert.That(state.ObserveLobbySync("map-a"), Is.False);
				Assert.That(state.IsPending, Is.True);
				Assert.That(state.TryAcceptRejection(DownloadRejection(request.RequestId), "map-a"), Is.True);
			});
		}

		[Test]
		public void ReadinessAndLobbySyncClearOnlyApplicableCachedRejections()
		{
			var state = new LobbySafeStartRequestState(0);
			var first = state.Begin("map-a");
			Assert.That(state.TryAcceptRejection(DownloadRejection(first.RequestId), "map-a"), Is.True);
			Assert.That(state.ObserveReadinessChange(4), Is.False);
			Assert.That(state.Rejection, Is.Not.Null);
			Assert.That(state.ObserveReadinessChange(5), Is.True);
			Assert.That(state.Rejection, Is.Null);

			var second = state.Begin("map-a");
			Assert.That(state.TryAcceptRejection(DownloadRejection(second.RequestId), "map-a"), Is.True);
			Assert.That(state.ObserveLobbySync("map-a"), Is.True);
			Assert.That(state.Rejection, Is.Null);
		}

		[Test]
		public void NewBeginMapChangeAndResetHaveFailClosedLifecycle()
		{
			var state = new LobbySafeStartRequestState(0);
			var first = state.Begin("map-a");
			Assert.That(state.TryAcceptRejection(DownloadRejection(first.RequestId), "map-a"), Is.True);
			var second = state.Begin("map-a");
			Assert.That(state.Rejection, Is.Null, "A new request must clear the cached rejection.");

			Assert.That(state.ObserveLobbySync("map-b"), Is.True);
			Assert.Multiple(() =>
			{
				Assert.That(state.IsPending, Is.False);
				Assert.That(state.PendingRequestId, Is.Zero);
				Assert.That(state.TryAcceptRejection(DownloadRejection(second.RequestId), "map-b"), Is.False);
				Assert.That(state.Reset(), Is.False, "Reset of empty state reports no mutation.");
			});

			var third = state.Begin("map-b");
			Assert.That(third.RequestId, Is.EqualTo(second.RequestId + 1));
			Assert.That(state.Reset(), Is.True);
			var fourth = state.Begin("map-b");
			Assert.That(fourth.RequestId, Is.EqualTo(third.RequestId + 1),
				"Reset must not permit request ID reuse.");
		}

		[Test]
		public void SafeStartStateValidatesSeedAndFailsClosedAtMaximumId()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => new LobbySafeStartRequestState(-1));
			var state = new LobbySafeStartRequestState(long.MaxValue);

			Assert.Throws<InvalidOperationException>(() => state.Begin("map-a"));
			Assert.Multiple(() =>
			{
				Assert.That(state.IsPending, Is.False);
				Assert.That(state.PendingRequestId, Is.Zero);
				Assert.That(state.Rejection, Is.Null);
			});
		}

		[Test]
		public void StructuredSafeStartRejectionIsAcceptedOnlyFromServer()
		{
			using var orderManager = new OrderManager(new RecordingConnection(), () => 0, _ => null);
			var rejection = new LobbyStartRejection(
				Session.LobbyStartBlockReason.ClientMapDownloading,
				5,
				Session.ClientMapPhase.Downloading,
				42,
				"map-a",
				7);
			var received = new List<LobbyStartRejection>();
			orderManager.SafeStartRejected += received.Add;

			var order = Order.FromTargetString("SafeStartRejected", rejection.Serialize(), true);
			ProcessUnitOrder(orderManager, 5, order);
			ProcessUnitOrder(orderManager, 0, Order.FromTargetString("SafeStartRejected", "Reason: nope\n", true));
			Assert.That(received, Is.Empty);

			ProcessUnitOrder(orderManager, 0, order);
			Assert.Multiple(() =>
			{
				Assert.That(received, Has.Count.EqualTo(1));
				Assert.That(received[0].Reason,
					Is.EqualTo(Session.LobbyStartBlockReason.ClientMapDownloading));
				Assert.That(received[0].Progress, Is.EqualTo(42));
			});
		}

		[Test]
		public void LobbyCommandsRegistersReadinessAndBothStartCommands()
		{
			var commands = new LobbyCommands();
			var handlers = typeof(LobbyCommands).GetField("commandHandlers",
				BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(commands) as IDictionary;

			Assert.That(handlers, Is.Not.Null);
			Assert.Multiple(() =>
			{
				Assert.That(handlers!.Contains("map_status"), Is.True);
				Assert.That(handlers.Contains("startgame"), Is.True);
				Assert.That(handlers.Contains("startgame_safe"), Is.True);
			});
		}

		[TestCase(ServerType.Local, true)]
		[TestCase(ServerType.Skirmish, true)]
		[TestCase(ServerType.Multiplayer, false)]
		[TestCase(ServerType.Dedicated, false)]
		public void OnlyTrustedLocalServersMayReplaceTheWholeLobbySnapshot(
			ServerType serverType, bool expected)
		{
			Assert.That(LobbyCommands.CanSyncLobby(serverType), Is.EqualTo(expected));
		}

		[Test]
		public void EveryMultiplayerStartEntryUsesTheSharedSafetyPolicy()
		{
			var root = RepositoryRoot();
			var lobbyCommands = File.ReadAllText(Path.Combine(root, "engine",
				"OpenRA.Mods.Common", "ServerTraits", "LobbyCommands.cs"));
			var server = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Game", "Server", "Server.cs"));

			var automatic = MethodBody(lobbyCommands, "static void CheckAutoStart(");
			var legacy = MethodBody(lobbyCommands, "static bool StartGame(");
			var safe = MethodBody(lobbyCommands, "static bool StartGameSafe(");
			var central = MethodBody(server, "public void StartGame()");

			Assert.Multiple(() =>
			{
				StringAssert.Contains("EvaluateStartAvailability(server, LobbyStartMode.Automatic", automatic);
				StringAssert.Contains("EvaluateStartAvailability(", legacy);
				StringAssert.Contains("LobbyStartMode.ForceConfirmed", legacy);
				StringAssert.Contains("EvaluateStartAvailability(server, LobbyStartMode.SafeDirect", safe);
				StringAssert.Contains("LobbySafeStartRequest.TryParse", safe);
				StringAssert.Contains("request.ExpectedMapUid", safe);
				StringAssert.Contains("request.RequestId", safe);
				StringAssert.Contains("availability.ToRejection(request)", safe);
				StringAssert.DoesNotContain("TryParseSafeStartMapUid", safe);
				StringAssert.Contains("if (IsMultiplayer &&", central,
					"Only trusted Local/Skirmish servers may bypass the network map handshake.");
				StringAssert.Contains("LobbyStartAvailability.EvaluateMapSafety(", central);
				Assert.That(central.IndexOf("LobbyStartAvailability.EvaluateMapSafety(", StringComparison.Ordinal),
					Is.LessThan(central.IndexOf("DropClient(c)", StringComparison.Ordinal)),
					"The fail-closed map guard must run before the legacy Invalid-client drop loop.");
			});
		}
	}
}
