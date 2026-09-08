using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Server;
using OpenRA.Network;
using OpenRA.Server;

namespace OpenRA.Test
{
	[TestFixture]
	[NonParallelizable]
	public sealed class IosLobbyMapReadinessTest
	{
		sealed class RecordingConnection : IConnection
		{
			public int LocalClientId { get; set; } = 1;
			public readonly List<Order> ImmediateOrders = new();

			public void StartGame() { }
			public void Send(int frame, IEnumerable<Order> orders) { }
			public void SendImmediate(IEnumerable<Order> orders) => ImmediateOrders.AddRange(orders);
			public void SendSync(int frame, int syncHash, ulong defeatState) { }
			public void Receive(OrderManager orderManager) { }
			public void Dispose() { }
		}

		static MapPreview Preview(string uid, MapStatus status)
		{
			var preview = new MapPreview(null, uid, MapGridType.Rectangular, null);
			preview.UpdateRemoteSearch(status, null, null);
			return preview;
		}

		static void AssertReport(ClientMapReadinessReport? actual, string uid,
			Session.ClientMapPhase phase, int progress)
		{
			Assert.That(actual.HasValue, Is.True);
			Assert.That(actual.Value, Is.EqualTo(new ClientMapReadinessReport(uid, phase, progress)));
		}

		static Order ReadinessOrder(ClientMapReadinessUpdate update)
		{
			return Order.FromTargetString("SyncClientMapReadiness",
				new[] { update.Serialize() }.WriteToString(), true);
		}

		static MiniYaml ReadinessUpdateYaml(string phase, int progress, string clientState)
		{
			return new MiniYaml(null, MiniYaml.FromString(
				$"ClientIndex: 5\nMapUid: map-a\nPhase: {phase}\nProgress: {progress}\nClientState: {clientState}\n",
				"client-map-readiness"));
		}

		static ClientMapReadinessUpdate ReadinessUpdate(int clientIndex, Session.ClientMapPhase phase,
			int progress, Session.ClientState clientState = Session.ClientState.Invalid)
		{
			return new ClientMapReadinessUpdate
			{
				ClientIndex = clientIndex,
				MapUid = "map-a",
				Phase = phase,
				Progress = progress,
				ClientState = clientState
			};
		}

		static void ProcessUnitOrder(OrderManager orderManager, int clientId, Order order)
		{
			var processOrder = typeof(UnitOrders).GetMethod("ProcessOrder",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(processOrder, Is.Not.Null);
			processOrder!.Invoke(null, new object[] { orderManager, null, clientId, order });
		}

		[Test]
		public void SessionClientMapReadinessRoundTripsAndLegacyDefaultsAreSafe()
		{
			var client = new Session.Client
			{
				Index = 7,
				Name = "Current",
				MapUid = "map-a",
				MapPhase = Session.ClientMapPhase.Downloading,
				MapProgress = 42
			};

			var roundTrip = Session.Client.Deserialize(client.Serialize().Value);
			Assert.That(roundTrip.MapUid, Is.EqualTo("map-a"));
			Assert.That(roundTrip.MapPhase, Is.EqualTo(Session.ClientMapPhase.Downloading));
			Assert.That(roundTrip.MapProgress, Is.EqualTo(42));
			Assert.That(roundTrip.IsMapReadyFor("map-a"), Is.False);

			var legacyYaml = new MiniYaml(null, MiniYaml.FromString("Index: 8\nName: Legacy\n", "legacy-client"));
			var legacy = Session.Client.Deserialize(legacyYaml);
			Assert.That(legacy.MapUid, Is.Null);
			Assert.That(legacy.MapPhase, Is.EqualTo(Session.ClientMapPhase.Unknown));
			Assert.That(legacy.MapProgress, Is.EqualTo(-1));
			Assert.That(legacy.IsMapReadyFor("map-a"), Is.False);
		}

		[Test]
		public void PhaseProgressInvariantAcceptsExactlyTheLegalCombinations()
		{
			foreach (var phase in Enum.GetValues<Session.ClientMapPhase>())
			foreach (var progress in Enumerable.Range(-2, 104))
			{
				var expected = phase switch
				{
					Session.ClientMapPhase.Downloading => progress == -1 || progress is >= 0 and <= 99,
					Session.ClientMapPhase.Ready => progress == 100,
					_ => progress == -1
				};

				Assert.That(ClientMapReadinessState.IsValidPhaseProgress(phase, progress), Is.EqualTo(expected),
					$"Unexpected validity for {phase}/{progress}");
			}

			Assert.That(ClientMapReadinessState.IsValidPhaseProgress(
				(Session.ClientMapPhase)int.MaxValue, -1), Is.False);
		}

		[Test]
		public void ClientMapPhaseWireNamesAndOrderAreStable()
		{
			Assert.That(Enum.GetNames<Session.ClientMapPhase>(), Is.EqualTo(new[]
			{
				"Unknown",
				"Searching",
				"WaitingForDownload",
				"Downloading",
				"InstallingOrVerifying",
				"Ready",
				"Unavailable",
				"Error"
			}));
		}

		[Test]
		public void ReportParsingUsesOneRawUidTokenAndTheCanonicalCommand()
		{
			var report = new ClientMapReadinessReport("map-a", Session.ClientMapPhase.Downloading, 42);
			Assert.That(report.IsValidFor("map-a"), Is.True);
			Assert.That(report.IsValidFor("MAP-A"), Is.False);
			Assert.That(report.ToCommand(), Is.EqualTo("map_status map-a Downloading 42"));

			Assert.That(ClientMapReadinessReport.TryParse(report.ToCommand(), out var parsed), Is.True);
			Assert.That(parsed, Is.EqualTo(report));
			Assert.That(ClientMapReadinessReport.TryParse("map_status map a Ready 100", out _), Is.False);
			Assert.That(ClientMapReadinessReport.TryParse("map_status map-a Ready -1", out _), Is.False);
			Assert.That(ClientMapReadinessReport.TryParse("map_status map-a ready 100", out _), Is.False);
			Assert.That(ClientMapReadinessReport.TryParse("map_status map-a 3 42", out _), Is.False);
			Assert.That(ClientMapReadinessReport.TryParse("map_status map-a Ready 100 trailing", out _), Is.False);
		}

		[Test]
		public void PublisherDeduplicatesAndCapsDownloadingProgressAtFourHertz()
		{
			var now = 0L;
			var publisher = new ClientMapReadinessPublisher(() => now);

			AssertReport(publisher.Update("map-a", MapStatus.Searching, 0), "map-a",
				Session.ClientMapPhase.Searching, -1);
			Assert.That(publisher.Update("map-a", MapStatus.Searching, 0), Is.Null);

			AssertReport(publisher.Update("map-a", MapStatus.Downloading, 42), "map-a",
				Session.ClientMapPhase.Downloading, 42);
			Assert.That(publisher.Update("map-a", MapStatus.Downloading, 42), Is.Null);

			now = 249;
			Assert.That(publisher.Update("map-a", MapStatus.Downloading, 43), Is.Null);
			now = 250;
			AssertReport(publisher.Update("map-a", MapStatus.Downloading, 43), "map-a",
				Session.ClientMapPhase.Downloading, 43);

			AssertReport(publisher.Update("map-b", MapStatus.Downloading, 43), "map-b",
				Session.ClientMapPhase.Downloading, 43);
		}

		[Test]
		public void PublisherMapsAllPreviewStatesAndPublishesPhaseChangesImmediately()
		{
			var now = 0L;
			var publisher = new ClientMapReadinessPublisher(() => now);

			AssertReport(publisher.Update("map-a", MapStatus.Downloading, -1), "map-a",
				Session.ClientMapPhase.Downloading, -1);
			Assert.That(publisher.Update("map-a", MapStatus.Downloading, 0), Is.Null,
				"Zero callback data represents the same unknown-length download state.");
			now = 1;
			AssertReport(publisher.Update("map-a", MapStatus.DownloadAvailable, 0), "map-a",
				Session.ClientMapPhase.WaitingForDownload, -1);
			AssertReport(publisher.Update("map-a", MapStatus.Downloading, 100), "map-a",
				Session.ClientMapPhase.InstallingOrVerifying, -1);
			AssertReport(publisher.Update("map-a", MapStatus.Available, 100), "map-a",
				Session.ClientMapPhase.Ready, 100);
			AssertReport(publisher.Update("map-a", MapStatus.Unavailable, 100), "map-a",
				Session.ClientMapPhase.Unavailable, -1);
			AssertReport(publisher.Update("map-a", MapStatus.DownloadError, 100), "map-a",
				Session.ClientMapPhase.Error, -1);
			AssertReport(publisher.Update("map-a", MapStatus.Searching, 100), "map-a",
				Session.ClientMapPhase.Searching, -1);
		}

		[Test]
		public void StateApplyPreservesGameReadyOnlyForRepeatedReadyReports()
		{
			var client = new Session.Client { Index = 4, State = Session.ClientState.Invalid };
			ClientMapReadinessState.Reset(client, "map-a");
			Assert.That(client.MapUid, Is.EqualTo("map-a"));
			Assert.That(client.MapPhase, Is.EqualTo(Session.ClientMapPhase.Unknown));
			Assert.That(client.MapProgress, Is.EqualTo(-1));
			Assert.That(client.State, Is.EqualTo(Session.ClientState.Invalid));

			var ready = new ClientMapReadinessReport("map-a", Session.ClientMapPhase.Ready, 100);
			Assert.That(ClientMapReadinessState.Apply(client, "map-a", ready),
				Is.EqualTo(ClientMapReadinessApplyResult.BecameReady));
			Assert.That(client.State, Is.EqualTo(Session.ClientState.NotReady));

			client.State = Session.ClientState.Ready;
			Assert.That(ClientMapReadinessState.Apply(client, "map-a", ready),
				Is.EqualTo(ClientMapReadinessApplyResult.Unchanged));
			Assert.That(client.State, Is.EqualTo(Session.ClientState.Ready));

			var downloading = new ClientMapReadinessReport("map-a", Session.ClientMapPhase.Downloading, 42);
			Assert.That(ClientMapReadinessState.Apply(client, "map-a", downloading),
				Is.EqualTo(ClientMapReadinessApplyResult.BecameUnready));
			Assert.That(client.State, Is.EqualTo(Session.ClientState.Invalid));

			var before = client.Serialize().Value.Nodes.WriteToString();
			var wrongMap = new ClientMapReadinessReport("map-b", Session.ClientMapPhase.Ready, 100);
			Assert.That(ClientMapReadinessState.Apply(client, "map-a", wrongMap),
				Is.EqualTo(ClientMapReadinessApplyResult.Rejected));
			Assert.That(client.Serialize().Value.Nodes.WriteToString(), Is.EqualTo(before));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase(" ")]
		[TestCase("map a")]
		public void ResetRejectsInvalidMapUidBeforeMutation(string invalidMapUid)
		{
			var client = new Session.Client
			{
				Index = 4,
				MapUid = "map-a",
				MapPhase = Session.ClientMapPhase.Ready,
				MapProgress = 100,
				State = Session.ClientState.Ready
			};
			var snapshot = client.Serialize().Value.Nodes.WriteToString();

			Assert.Throws<ArgumentException>(() => ClientMapReadinessState.Reset(client, invalidMapUid));
			Assert.That(client.Serialize().Value.Nodes.WriteToString(), Is.EqualTo(snapshot));
		}

		[Test]
		public void ReadinessUpdateRoundTripsAndFailsClosedBeforeMutation()
		{
			var session = new Session { GlobalSettings = { Map = "map-a" } };
			var client = new Session.Client { Index = 5, State = Session.ClientState.Invalid };
			session.Clients.Add(client);

			var update = new ClientMapReadinessUpdate
			{
				ClientIndex = 5,
				MapUid = "map-a",
				Phase = Session.ClientMapPhase.Ready,
				Progress = 100,
				ClientState = Session.ClientState.NotReady
			};

			var serialized = update.Serialize();
			var roundTrip = ClientMapReadinessUpdate.Deserialize(serialized.Value);
			Assert.That(roundTrip, Is.Not.Null);
			Assert.That(roundTrip.ApplyTo(session), Is.True);
			Assert.That(client.IsMapReadyFor("map-a"), Is.True);
			Assert.That(client.State, Is.EqualTo(Session.ClientState.NotReady));

			var validSnapshot = client.Serialize().Value.Nodes.WriteToString();
			void AssertMalformedDoesNotMutate(string yaml, string name)
			{
				Assert.That(ClientMapReadinessUpdate.Deserialize(
					new MiniYaml(null, MiniYaml.FromString(yaml, name))), Is.Null);
				Assert.That(client.Serialize().Value.Nodes.WriteToString(), Is.EqualTo(validSnapshot));
			}

			var malformed = ClientMapReadinessUpdate.Deserialize(
				new MiniYaml(null, MiniYaml.FromString("ClientIndex: nope\n", "malformed-readiness")));
			Assert.That(malformed, Is.Null);
			Assert.That(client.Serialize().Value.Nodes.WriteToString(), Is.EqualTo(validSnapshot));
			var missingState = ClientMapReadinessUpdate.Deserialize(new MiniYaml(null, MiniYaml.FromString(
				"ClientIndex: 5\nMapUid: map-a\nPhase: Ready\nProgress: 100\n", "missing-readiness-state")));
			Assert.That(missingState, Is.Null, "Every authoritative readiness delta field is required.");
			var duplicateIndex = ClientMapReadinessUpdate.Deserialize(new MiniYaml(null, MiniYaml.FromString(
				"ClientIndex: 5\nClientIndex: 6\nMapUid: map-a\nPhase: Ready\nProgress: 100\nClientState: NotReady\n",
				"duplicate-readiness-index")));
			Assert.That(duplicateIndex, Is.Null);
			var unknownField = ClientMapReadinessUpdate.Deserialize(new MiniYaml(null, MiniYaml.FromString(
				"ClientIndex: 5\nMapUid: map-a\nPhase: Ready\nProgress: 100\nClientState: NotReady\nUnexpected: nope\n",
				"unknown-readiness-field")));
			Assert.That(unknownField, Is.Null);
			Assert.That(client.Serialize().Value.Nodes.WriteToString(), Is.EqualTo(validSnapshot));
			AssertMalformedDoesNotMutate(
				"ClientIndex: 5\nMapUid: map-a\nPhase: Bogus\nProgress: -1\nClientState: Invalid\n",
				"malformed-readiness-phase");
			AssertMalformedDoesNotMutate(
				"ClientIndex: 5\nMapUid: map-a\nPhase: Downloading\nProgress: 42\nClientState: Bogus\n",
				"malformed-readiness-client-state");
			AssertMalformedDoesNotMutate(
				"ClientIndex: 5\nMapUid: map-a\nPhase: Downloading\nProgress: 100\nClientState: Invalid\n",
				"illegal-readiness-progress");

			update.ClientIndex = 99;
			Assert.That(update.ApplyTo(session), Is.False);
			update.ClientIndex = 5;
			update.MapUid = "map-b";
			Assert.That(update.ApplyTo(session), Is.False);
			Assert.That(client.Serialize().Value.Nodes.WriteToString(), Is.EqualTo(validSnapshot));
		}

		[TestCase(Session.ClientMapPhase.Downloading, 42, Session.ClientState.Ready)]
		[TestCase(Session.ClientMapPhase.Downloading, 42, Session.ClientState.NotReady)]
		[TestCase(Session.ClientMapPhase.Ready, 100, Session.ClientState.Invalid)]
		[TestCase(Session.ClientMapPhase.Ready, 100, Session.ClientState.Disconnected)]
		public void ReadinessUpdateRejectsIncoherentPhaseAndClientStateBeforeMutation(
			Session.ClientMapPhase phase, int progress, Session.ClientState clientState)
		{
			var session = new Session { GlobalSettings = { Map = "map-a" } };
			var client = new Session.Client
			{
				Index = 5,
				MapUid = "map-a",
				MapPhase = Session.ClientMapPhase.Ready,
				MapProgress = 100,
				State = Session.ClientState.Ready
			};
			session.Clients.Add(client);
			var snapshot = client.Serialize().Value.Nodes.WriteToString();

			var deserialized = ClientMapReadinessUpdate.Deserialize(
				ReadinessUpdateYaml(phase.ToString(), progress, clientState.ToString()));
			var applied = deserialized?.ApplyTo(session) ?? false;

			Assert.Multiple(() =>
			{
				Assert.That(deserialized, Is.Null,
					$"{phase}/{progress}/{clientState} must be rejected as an authoritative readiness delta.");
				Assert.That(applied, Is.False);
				Assert.That(client.Serialize().Value.Nodes.WriteToString(), Is.EqualTo(snapshot));
			});

			var directUpdate = new ClientMapReadinessUpdate
			{
				ClientIndex = 5,
				MapUid = "map-a",
				Phase = phase,
				Progress = progress,
				ClientState = clientState
			};

			Assert.That(directUpdate.ApplyTo(session), Is.False);
			Assert.That(client.Serialize().Value.Nodes.WriteToString(), Is.EqualTo(snapshot));
		}

		[TestCase("ready", "NotReady")]
		[TestCase("5", "NotReady")]
		[TestCase("Ready", "notready")]
		[TestCase("Ready", "0")]
		public void ReadinessUpdateRejectsNonCanonicalRawEnumTokensBeforeMutation(
			string phaseToken, string clientStateToken)
		{
			var session = new Session { GlobalSettings = { Map = "map-a" } };
			var client = new Session.Client
			{
				Index = 5,
				MapUid = "map-a",
				MapPhase = Session.ClientMapPhase.Ready,
				MapProgress = 100,
				State = Session.ClientState.Ready
			};
			session.Clients.Add(client);
			var snapshot = client.Serialize().Value.Nodes.WriteToString();

			var deserialized = ClientMapReadinessUpdate.Deserialize(
				ReadinessUpdateYaml(phaseToken, 100, clientStateToken));
			var applied = deserialized?.ApplyTo(session) ?? false;

			Assert.Multiple(() =>
			{
				Assert.That(deserialized, Is.Null,
					$"Phase={phaseToken} ClientState={clientStateToken} must use canonical enum tokens.");
				Assert.That(applied, Is.False);
				Assert.That(client.Serialize().Value.Nodes.WriteToString(), Is.EqualTo(snapshot));
			});
		}

		[Test]
		public void AuthoritativeReadinessDeltaAppliesInPlaceAndFiresOnlyTheNarrowEvent()
		{
			var connection = new RecordingConnection();
			using var orderManager = new OrderManager(connection, () => 0, _ => null);
			orderManager.LobbyInfo.GlobalSettings.Map = "map-a";
			var client = new Session.Client
			{
				Index = 5,
				MapUid = "map-a",
				MapPhase = Session.ClientMapPhase.Downloading,
				MapProgress = 41,
				State = Session.ClientState.Invalid
			};
			orderManager.LobbyInfo.Clients.Add(client);

			var narrowCount = 0;
			var changedClientIndex = -1;
			var fullLobbyCount = 0;
			orderManager.ClientMapReadinessChanged += clientIndex =>
			{
				narrowCount++;
				changedClientIndex = clientIndex;
			};
			void OnLobbyInfoChanged() => fullLobbyCount++;
			Game.LobbyInfoChanged += OnLobbyInfoChanged;
			try
			{
				ProcessUnitOrder(orderManager, 0, ReadinessOrder(new ClientMapReadinessUpdate
				{
					ClientIndex = 5,
					MapUid = "map-a",
					Phase = Session.ClientMapPhase.Downloading,
					Progress = 42,
					ClientState = Session.ClientState.Invalid
				}));
			}
			finally
			{
				Game.LobbyInfoChanged -= OnLobbyInfoChanged;
			}

			Assert.That(client.MapProgress, Is.EqualTo(42));
			Assert.That(narrowCount, Is.EqualTo(1));
			Assert.That(changedClientIndex, Is.EqualTo(5));
			Assert.That(fullLobbyCount, Is.Zero,
				"A percentage-only delta must not trigger the full lobby refresh path.");
		}

		[Test]
		public void RejectedReadinessDeltasDoNotMutateOrFireLobbyEvents()
		{
			var connection = new RecordingConnection();
			using var orderManager = new OrderManager(connection, () => 0, _ => null);
			orderManager.LobbyInfo.GlobalSettings.Map = "map-a";
			var client = new Session.Client
			{
				Index = 5,
				MapUid = "map-a",
				MapPhase = Session.ClientMapPhase.Downloading,
				MapProgress = 41,
				State = Session.ClientState.Invalid
			};
			orderManager.LobbyInfo.Clients.Add(client);
			var snapshot = client.Serialize().Value.Nodes.WriteToString();
			var narrowCount = 0;
			var fullLobbyCount = 0;
			orderManager.ClientMapReadinessChanged += _ => narrowCount++;
			void OnLobbyInfoChanged() => fullLobbyCount++;
			Game.LobbyInfoChanged += OnLobbyInfoChanged;
			try
			{
				var valid = new ClientMapReadinessUpdate
				{
					ClientIndex = 5,
					MapUid = "map-a",
					Phase = Session.ClientMapPhase.Downloading,
					Progress = 42,
					ClientState = Session.ClientState.Invalid
				};
				ProcessUnitOrder(orderManager, 5, ReadinessOrder(valid));
				ProcessUnitOrder(orderManager, 0, Order.FromTargetString(
					"SyncClientMapReadiness", "ClientMapReadiness:\n\tClientIndex: nope\n", true));
				valid.MapUid = "map-b";
				ProcessUnitOrder(orderManager, 0, ReadinessOrder(valid));
				valid.MapUid = "map-a";
				valid.ClientIndex = 99;
				ProcessUnitOrder(orderManager, 0, ReadinessOrder(valid));
			}
			finally
			{
				Game.LobbyInfoChanged -= OnLobbyInfoChanged;
			}

			Assert.That(client.Serialize().Value.Nodes.WriteToString(), Is.EqualTo(snapshot));
			Assert.That(narrowCount, Is.Zero);
			Assert.That(fullLobbyCount, Is.Zero);
		}

		[Test]
		public void OrderManagerAlwaysResolvesAndPublishesOnlyTheCurrentMap()
		{
			var now = 0L;
			var connection = new RecordingConnection();
			var mapA = Preview("map-a", MapStatus.Searching);
			var mapB = Preview("map-b", MapStatus.Searching);
			var resolvedUids = new List<string>();
			var previews = new Dictionary<string, MapPreview> { ["map-a"] = mapA, ["map-b"] = mapB };
			using var orderManager = new OrderManager(connection, () => now, uid =>
			{
				resolvedUids.Add(uid);
				return previews.TryGetValue(uid, out var preview) ? preview : null;
			});

			orderManager.LobbyInfo.Clients.Add(new Session.Client { Index = 1 });
			orderManager.LobbyInfo.GlobalSettings.Map = "map-a";
			orderManager.TickImmediate();
			Assert.That(connection.ImmediateOrders.Select(o => o.TargetString),
				Is.EqualTo(new[] { "map_status map-a Searching -1" }));

			orderManager.LobbyInfo.GlobalSettings.Map = "map-b";
			orderManager.TickImmediate();
			Assert.That(connection.ImmediateOrders.Select(o => o.TargetString), Is.EqualTo(new[]
			{
				"map_status map-a Searching -1",
				"map_status map-b Searching -1"
			}));

			mapA.UpdateRemoteSearch(MapStatus.DownloadError, null, null);
			now = 1000;
			orderManager.TickImmediate();
			Assert.That(connection.ImmediateOrders, Has.Count.EqualTo(2),
				"An update from the old preview must not publish after the lobby switches maps.");
			Assert.That(resolvedUids, Is.EqualTo(new[] { "map-a", "map-b", "map-b" }));
		}

		[Test]
		public void OrderManagerPublishesUnknownForMissingCurrentPreview()
		{
			var connection = new RecordingConnection();
			using var orderManager = new OrderManager(connection, () => 0, _ => null);
			orderManager.LobbyInfo.Clients.Add(new Session.Client { Index = 1 });
			orderManager.LobbyInfo.GlobalSettings.Map = "missing-map";

			orderManager.TickImmediate();

			Assert.That(connection.ImmediateOrders.Single().TargetString,
				Is.EqualTo("map_status missing-map Unknown -1"));
		}

		[Test]
		public void OrderManagerDoesNotPublishWithoutALocalClient()
		{
			var connection = new RecordingConnection();
			using var orderManager = new OrderManager(connection, () => 0,
				uid => Preview(uid, MapStatus.Searching));
			orderManager.LobbyInfo.GlobalSettings.Map = "map-a";

			orderManager.TickImmediate();

			Assert.That(connection.ImmediateOrders, Is.Empty);
		}

		[Test]
		public void OrderManagerDoesNotPublishAfterGameStart()
		{
			var connection = new RecordingConnection();
			using var orderManager = new OrderManager(connection, () => 0,
				uid => Preview(uid, MapStatus.Searching));
			orderManager.LobbyInfo.Clients.Add(new Session.Client { Index = connection.LocalClientId });
			orderManager.LobbyInfo.GlobalSettings.Map = "map-a";
			orderManager.StartGame();

			orderManager.TickImmediate();

			Assert.That(connection.ImmediateOrders, Is.Empty);
		}

		[Test]
		public void ServerReadinessThrottlePublishesOnlyTheLatestSnapshotAtFourHertz()
		{
			var throttle = new ClientMapReadinessThrottle(250);
			var first = ReadinessUpdate(5, Session.ClientMapPhase.Downloading, 41);
			throttle.Submit(first, 0);
			first.Progress = 99;

			var initial = throttle.Drain(0).Single();
			Assert.That(initial.Progress, Is.EqualTo(41), "Submit must retain an immutable snapshot.");

			throttle.Submit(ReadinessUpdate(5, Session.ClientMapPhase.Downloading, 42), 100);
			throttle.Submit(ReadinessUpdate(5, Session.ClientMapPhase.Downloading, 43), 200);
			Assert.That(throttle.Drain(249), Is.Empty);
			Assert.That(throttle.Drain(250).Single().Progress, Is.EqualTo(43));

			throttle.Submit(ReadinessUpdate(5, Session.ClientMapPhase.Ready, 100,
				Session.ClientState.NotReady), 251);
			Assert.That(throttle.Drain(499), Is.Empty, "Phase changes are also capped at four hertz.");
			var ready = throttle.Drain(500).Single();
			Assert.That(ready.Phase, Is.EqualTo(Session.ClientMapPhase.Ready));
			Assert.That(ready.Progress, Is.EqualTo(100));
			Assert.That(ready.ClientState, Is.EqualTo(Session.ClientState.NotReady));

			throttle.Submit(ReadinessUpdate(5, Session.ClientMapPhase.Ready, 100,
				Session.ClientState.NotReady), 600);
			Assert.That(throttle.Drain(750), Is.Empty, "An already broadcast snapshot must be deduplicated.");
		}

		[Test]
		public void ServerReadinessThrottleRemovesDisconnectedClientsAndResetsOnMapChange()
		{
			var throttle = new ClientMapReadinessThrottle(250);
			throttle.Submit(ReadinessUpdate(1, Session.ClientMapPhase.Downloading, 10), 0);
			throttle.Submit(ReadinessUpdate(2, Session.ClientMapPhase.Downloading, 20), 0);
			throttle.Remove(1);

			Assert.That(throttle.Drain(0).Select(update => update.ClientIndex), Is.EqualTo(new[] { 2 }));

			throttle.Submit(ReadinessUpdate(2, Session.ClientMapPhase.Downloading, 21), 100);
			throttle.Reset();
			Assert.That(throttle.Drain(1000), Is.Empty);

			throttle.Submit(ReadinessUpdate(1, Session.ClientMapPhase.Downloading, 10), 2000);
			throttle.Submit(ReadinessUpdate(2, Session.ClientMapPhase.Downloading, 20), 2000);
			throttle.Retain(new[] { 2 });
			Assert.That(throttle.Drain(2000).Select(update => update.ClientIndex), Is.EqualTo(new[] { 2 }));
		}

		[Test]
		public void ServerMapReadinessParsingRejectsStaleMalformedAndIllegalReports()
		{
			Assert.That(LobbyCommands.TryParseMapReadiness(
				"map-a Downloading 42", "map-a", out var accepted), Is.True);
			Assert.That(accepted, Is.EqualTo(new ClientMapReadinessReport(
				"map-a", Session.ClientMapPhase.Downloading, 42)));

			Assert.That(LobbyCommands.TryParseMapReadiness("map-old Ready 100", "map-a", out _), Is.False);
			Assert.That(LobbyCommands.TryParseMapReadiness("map-a Ready -1", "map-a", out _), Is.False);
			Assert.That(LobbyCommands.TryParseMapReadiness("map-a Error 100", "map-a", out _), Is.False);
			Assert.That(LobbyCommands.TryParseMapReadiness("map-a Downloading 100", "map-a", out _), Is.False);
			Assert.That(LobbyCommands.TryParseMapReadiness("5 map-a Ready 100", "map-a", out _), Is.False,
				"A client must never be able to target another client index.");
			Assert.That(LobbyCommands.TryParseMapReadiness("map-a Ready 100 trailing", "map-a", out _), Is.False);
		}

		[Test]
		public void ServerAppliesAuthoritativeReadinessBeforeThrottleDrain()
		{
			var client = new Session.Client
			{
				Index = 5,
				MapUid = "map-a",
				MapPhase = Session.ClientMapPhase.Ready,
				MapProgress = 100,
				State = Session.ClientState.Ready
			};

			var result = LobbyCommands.ApplyMapReadiness(
				client, "map-a", "map-a Downloading 42", out var downloading);
			Assert.Multiple(() =>
			{
				Assert.That(result, Is.EqualTo(ClientMapReadinessApplyResult.BecameUnready));
				Assert.That(client.State, Is.EqualTo(Session.ClientState.Invalid),
					"Authoritative state must change before the broadcast throttle drains.");
				Assert.That(client.MapPhase, Is.EqualTo(Session.ClientMapPhase.Downloading));
				Assert.That(client.MapProgress, Is.EqualTo(42));
				Assert.That(downloading.ClientIndex, Is.EqualTo(5));
				Assert.That(downloading.ClientState, Is.EqualTo(Session.ClientState.Invalid));
			});

			result = LobbyCommands.ApplyMapReadiness(client, "map-a", "map-a Ready 100", out var ready);
			Assert.Multiple(() =>
			{
				Assert.That(result, Is.EqualTo(ClientMapReadinessApplyResult.BecameReady));
				Assert.That(client.State, Is.EqualTo(Session.ClientState.NotReady));
				Assert.That(client.IsMapReadyFor("map-a"), Is.True);
				Assert.That(ready.ClientState, Is.EqualTo(Session.ClientState.NotReady));
			});

			result = LobbyCommands.ApplyMapReadiness(client, "map-a", "map-a Ready 100", out var duplicate);
			Assert.That(result, Is.EqualTo(ClientMapReadinessApplyResult.Unchanged));
			Assert.That(duplicate, Is.Null);
		}

		[TestCase(true, false, Session.ClientState.NotReady, false)]
		[TestCase(true, false, Session.ClientState.Ready, false)]
		[TestCase(true, true, Session.ClientState.NotReady, true)]
		[TestCase(true, true, Session.ClientState.Ready, true)]
		[TestCase(false, false, Session.ClientState.NotReady, true)]
		[TestCase(false, false, Session.ClientState.Ready, true)]
		[TestCase(false, true, Session.ClientState.Invalid, false)]
		[TestCase(false, true, Session.ClientState.Disconnected, false)]
		public void StateTransitionAdmissionDependsOnServerTypeAndMapReadiness(
			bool requireMapReadiness, bool mapReady, Session.ClientState requestedState, bool expected)
		{
			var client = new Session.Client
			{
				Index = 5,
				MapUid = "map-a",
				MapPhase = mapReady ? Session.ClientMapPhase.Ready : Session.ClientMapPhase.Downloading,
				MapProgress = mapReady ? 100 : 42,
				State = mapReady ? Session.ClientState.NotReady : Session.ClientState.Invalid
			};

			Assert.That(LobbyCommands.IsClientStateTransitionAllowed(
				requireMapReadiness, client, "map-a", requestedState), Is.EqualTo(expected));
		}

		[Test]
		public void MapLifecycleResetsOnlyTheNewJoinerOrAChangedMapUid()
		{
			var existing = new Session.Client
			{
				Index = 1,
				MapUid = "map-a",
				MapPhase = Session.ClientMapPhase.Ready,
				MapProgress = 100,
				State = Session.ClientState.Ready
			};
			var newcomer = new Session.Client { Index = 2 };

			LobbyCommands.InitializeClientMapReadiness(newcomer, "map-a");
			Assert.Multiple(() =>
			{
				Assert.That(existing.IsMapReadyFor("map-a"), Is.True,
					"Joining must not reset an existing client's deduplicated Ready report.");
				Assert.That(newcomer.MapUid, Is.EqualTo("map-a"));
				Assert.That(newcomer.MapPhase, Is.EqualTo(Session.ClientMapPhase.Unknown));
				Assert.That(newcomer.State, Is.EqualTo(Session.ClientState.Invalid));
			});

			Assert.That(LobbyCommands.ResetClientMapReadinessForMapChange(
				new[] { existing, newcomer }, "map-a", "map-a"), Is.False);
			Assert.That(existing.IsMapReadyFor("map-a"), Is.True,
				"Same-map slot, team, faction, and admin mutations must preserve readiness.");

			Assert.That(LobbyCommands.ResetClientMapReadinessForMapChange(
				new[] { existing, newcomer }, "map-a", "map-b"), Is.True);
			foreach (var client in new[] { existing, newcomer })
			{
				Assert.That(client.MapUid, Is.EqualTo("map-b"));
				Assert.That(client.MapPhase, Is.EqualTo(Session.ClientMapPhase.Unknown));
				Assert.That(client.MapProgress, Is.EqualTo(-1));
				Assert.That(client.State, Is.EqualTo(Session.ClientState.Invalid));
			}
		}

		[Test]
		public void ChangedMapIdentifiesOnlyHiddenHumanClientsWithoutSlots()
		{
			var hiddenHuman = new Session.Client { Index = 1 };
			var slottedHuman = new Session.Client { Index = 2, Slot = "PlayerReference@1" };
			var hiddenBot = new Session.Client { Index = 3, Bot = "normal" };

			Assert.That(LobbyCommands.DisplacedHumanClientIndexes(
				new[] { hiddenHuman, slottedHuman, hiddenBot }, false), Is.Empty);
			Assert.That(LobbyCommands.DisplacedHumanClientIndexes(
				new[] { hiddenHuman, slottedHuman, hiddenBot }, true), Is.EqualTo(new[] { 1 }));
		}

		[TestCase("state Ready", true)]
		[TestCase("startgame", true)]
		[TestCase("startgame_safe map-a 1", true)]
		[TestCase("map_status map-a Ready 100", true)]
		[TestCase("stateevil Ready", false)]
		[TestCase("map_status_extra map-a Ready 100", false)]
		[TestCase("slot PlayerReference@1", false)]
		public void ReadyClientCommandAdmissionUsesExactCommandNames(string command, bool expected)
		{
			Assert.That(LobbyCommands.IsAllowedWhileReady(
				LobbyCommands.CommandName(command)), Is.EqualTo(expected));
		}

		[Test]
		public void BeginningDownloadAttemptSynchronouslyClearsRetryProgress()
		{
			var preview = Preview("map-a", MapStatus.DownloadError);
			typeof(MapPreview).GetField("<DownloadBytes>k__BackingField",
				BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(preview, 1234L);
			typeof(MapPreview).GetField("<DownloadPercentage>k__BackingField",
				BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(preview, 87);

			var beginAttempt = typeof(MapPreview).GetMethod("BeginDownloadAttempt",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Assert.That(beginAttempt, Is.Not.Null, "MapPreview must expose one download-attempt reset seam.");
			beginAttempt!.Invoke(preview, null);

			Assert.That(preview.DownloadBytes, Is.Zero);
			Assert.That(preview.DownloadPercentage, Is.Zero);
			Assert.That(preview.Status, Is.EqualTo(MapStatus.Downloading));
		}

		[Test]
		public void OrdersProtocolIncludesCorrelatedSafeStartRejections()
		{
			Assert.That(ProtocolVersion.Orders, Is.EqualTo(23));
		}
	}
}
