using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosMultiplayerPolicyTest
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

		[Test]
		public void IosDeclaresWhyItNeedsLocalNetworkAccess()
		{
			var plist = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"ios", "OpenRA.iOS", "Info.plist"));

			StringAssert.Contains("<key>NSLocalNetworkUsageDescription</key>", plist);
			StringAssert.Contains("附近设备", plist);
			StringAssert.Contains("<key>NSBonjourServices</key>", plist);
			StringAssert.Contains("_openra-ra2._tcp", plist);
		}

		[Test]
		public void IosNearbyNetworkingUsesBonjourPeerToPeerAndRawTcpRelays()
		{
			var sourcePath = Path.Combine(RepositoryRoot(), "ios", "OpenRA.Platforms.iOS",
				"IosNearbyGameService.cs");

			Assert.That(File.Exists(sourcePath), Is.True,
				"The iOS nearby-game service must be compiled from the iOS platform assembly.");
			var source = File.ReadAllText(sourcePath);
			StringAssert.Contains("IncludePeerToPeer = true", source);
			StringAssert.Contains("NearbyGamePayloadCodec.Encode", source);
			StringAssert.Contains("NearbyGamePayloadCodec.TryDecode", source);
			StringAssert.Contains("NWBrowser", source);
			StringAssert.Contains("NWListener", source);
			StringAssert.Contains("TcpListener", source);
			StringAssert.Contains("TcpClient", source);
			StringAssert.Contains("ScheduleBrowserRetryLocked", source);
			StringAssert.Contains("ScheduleAdvertiserRetryLocked", source);
			StringAssert.Contains("NearbyRelayDeadlinePolicy.RunWithResultAsync", source);
			StringAssert.Contains("SendCompleteAsync", source);
		}

		[Test]
		public void IosNearbyRelayUsesNullableDispatchDataForTcpFin()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "ios",
				"OpenRA.Platforms.iOS", "IosNearbyGameService.cs"));

			StringAssert.Contains("connection.Send((DispatchData)null,", source,
				"Network.framework represents a zero-byte stream FIN with null DispatchData.");
			StringAssert.DoesNotContain("SendAsync(Array.Empty<byte>(), 0, true)", source,
				"The byte[] slice binding rejects a zero-length FIN before it reaches Network.framework.");
		}

		[Test]
		public void IosNearbyDiscoveryObservesRegistrationWaitingAndIndividualChanges()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "ios",
				"OpenRA.Platforms.iOS", "IosNearbyGameService.cs"));

			StringAssert.Contains("BrowserWaitingState = (NWBrowserState)4", source,
				"Waiting carries local-network privacy and transient DNS failures and must not be silent.");
			StringAssert.Contains("SetAdvertisedEndpointChangedHandler", source,
				"Listener Ready does not prove that Bonjour registration completed.");
			StringAssert.Contains("IndividualChangesDelegate", source,
				"Individual old/new results are required to remove vanished services reliably.");
			StringAssert.Contains("ScheduleBrowserLivenessCheckLocked", source,
				"A Ready browser that delivers no usable result must recover without a relaunch.");
			StringAssert.Contains("ScheduleAdvertiserRegistrationCheckLocked", source,
				"A listener that never registers its Bonjour endpoint must be restarted.");
		}

		[Test]
		public void IosRegistersTheActiveModWithItsExecutableLaunchPath()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"ios", "OpenRA.iOS", "AppDelegate.cs"));

			StringAssert.Contains("NSBundle.MainBundle.ExecutablePath", source);
			StringAssert.Contains("Engine.LaunchPath=", source);
		}

		[Test]
		public void NearbyNetworkingIsWiredThroughTheProductionHostBrowserAndJoinPaths()
		{
			var root = RepositoryRoot();
			var appDelegate = File.ReadAllText(Path.Combine(root, "ios", "OpenRA.iOS", "AppDelegate.cs"));
			var pinger = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"ServerTraits", "MasterServerPinger.cs"));
			var browser = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "ServerListLogic.cs"));
			var multiplayer = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "MultiplayerLogic.cs"));

			StringAssert.Contains("NearbyGameNetworking.InstallService", appDelegate);
			StringAssert.Contains("NearbyGameNetworking.StartAdvertising", pinger);
			StringAssert.Contains("NearbyGameNetworking.UpdateAdvertising", pinger);
			StringAssert.Contains("NearbyGameNetworking.StopAdvertising", pinger);
			StringAssert.Contains("NearbyGameNetworking.StartBrowsing", browser);
			StringAssert.Contains("NearbyGameNetworking.GamesChanged", browser);
			StringAssert.Contains("ConnectionTarget.TryParseGameAddress", multiplayer);
			StringAssert.DoesNotContain("server.Address.Split(':')", multiplayer);
		}

		[Test]
		public void MultiplayerSmokeIsDisabledWithoutAnExplicitRole()
		{
			var config = IosMultiplayerSmokeConfiguration.Parse(_ => null);

			Assert.That(config.Enabled, Is.False);
		}

		[Test]
		public void MultiplayerSmokeValidatesHostAndClientInputs()
		{
			var hostValues = new Dictionary<string, string>
			{
				["OPENRA_IOS_MULTIPLAYER_SMOKE_ROLE"] = "host",
				["OPENRA_IOS_MULTIPLAYER_SMOKE_MAP"] = "south-pacific",
				["OPENRA_IOS_MULTIPLAYER_SMOKE_PORT"] = "1234",
				["OPENRA_IOS_MULTIPLAYER_SMOKE_RUN_ID"] = "lan-test",
				["OPENRA_IOS_MULTIPLAYER_SMOKE_EXPECTED_RUNTIME_HASH"] = new string('a', 64),
				["OPENRA_IOS_MULTIPLAYER_SMOKE_LAUNCH_NONCE"] = new string('1', 32),
				["OPENRA_IOS_MULTIPLAYER_SMOKE_PARTICIPANT_NONCE"] = new string('2', 32),
				["OPENRA_IOS_MULTIPLAYER_SMOKE_EXPECTED_REMOTE_NONCE"] = new string('3', 32)
			};
			var host = IosMultiplayerSmokeConfiguration.Parse(
				name => hostValues.TryGetValue(name, out var value) ? value : null);

			Assert.That(host.Enabled, Is.True);
			Assert.That(host.Role, Is.EqualTo(IosMultiplayerSmokeRole.Host));
			Assert.That(host.Port, Is.EqualTo(1234));
			Assert.That(host.Map, Is.EqualTo("south-pacific"));

			var autoClient = IosMultiplayerSmokeConfiguration.Parse(name => name switch
			{
				"OPENRA_IOS_MULTIPLAYER_SMOKE_ROLE" => "client",
				"OPENRA_IOS_MULTIPLAYER_SMOKE_RUN_ID" => "lan-test",
				"OPENRA_IOS_MULTIPLAYER_SMOKE_EXPECTED_RUNTIME_HASH" => new string('a', 64),
				"OPENRA_IOS_MULTIPLAYER_SMOKE_LAUNCH_NONCE" => new string('1', 32),
				"OPENRA_IOS_MULTIPLAYER_SMOKE_PARTICIPANT_NONCE" => new string('3', 32),
				"OPENRA_IOS_MULTIPLAYER_SMOKE_EXPECTED_REMOTE_NONCE" => new string('2', 32),
				_ => null
			});
			Assert.That(autoClient.Enabled, Is.True);
			Assert.That(autoClient.DiscoveryMode, Is.EqualTo(IosMultiplayerSmokeDiscoveryMode.Auto));

			var invalidDirectClient = IosMultiplayerSmokeConfiguration.Parse(name => name switch
			{
				"OPENRA_IOS_MULTIPLAYER_SMOKE_ROLE" => "client",
				"OPENRA_IOS_MULTIPLAYER_SMOKE_DISCOVERY_MODE" => "direct",
				"OPENRA_IOS_MULTIPLAYER_SMOKE_EXPECTED_RUNTIME_HASH" => new string('a', 64),
				"OPENRA_IOS_MULTIPLAYER_SMOKE_LAUNCH_NONCE" => new string('1', 32),
				"OPENRA_IOS_MULTIPLAYER_SMOKE_PARTICIPANT_NONCE" => new string('3', 32),
				"OPENRA_IOS_MULTIPLAYER_SMOKE_EXPECTED_REMOTE_NONCE" => new string('2', 32),
				_ => null
			});
			Assert.That(invalidDirectClient.Enabled, Is.False);
			StringAssert.Contains("endpoint", invalidDirectClient.ValidationError.ToLowerInvariant());
		}

		[Test]
		public void MultiplayerSmokeRequiresAnExpectedRuntimeBundleHash()
		{
			var config = IosMultiplayerSmokeConfiguration.Parse(name => name switch
			{
				"OPENRA_IOS_MULTIPLAYER_SMOKE_ROLE" => "host",
				"OPENRA_IOS_MULTIPLAYER_SMOKE_LAUNCH_NONCE" => new string('1', 32),
				"OPENRA_IOS_MULTIPLAYER_SMOKE_PARTICIPANT_NONCE" => new string('2', 32),
				"OPENRA_IOS_MULTIPLAYER_SMOKE_EXPECTED_REMOTE_NONCE" => new string('3', 32),
				_ => null
			});

			Assert.That(config.Enabled, Is.False);
			StringAssert.Contains("runtime", config.ValidationError.ToLowerInvariant());
		}

		[Test]
		public void MultiplayerSmokeAutoDiscoveryMatchesOnlyTheUniqueRunRoom()
		{
			var launchNonce = new string('1', 32);
			var hostNonce = new string('2', 32);
			var runtimeHash = new string('a', 64);
			var roomName = IosMultiplayerSmokeDiscovery.RoomName("run-42", launchNonce, hostNonce, runtimeHash);
			var matchingPayload = $"Game:\n\tName: {roomName}\n\tAddress: 0.0.0.0:1234\n\tId: host-session";
			var games = new[]
			{
				new NearbyGameInfo("other", "Game:\n\tName: OpenRA iOS smoke run-41"),
				new NearbyGameInfo("match", matchingPayload)
			};

			Assert.That(IosMultiplayerSmokeDiscovery.TryFindUniqueRoom(
				games, "run-42", launchNonce, hostNonce, runtimeHash, out var match, out var error), Is.True, error);
			Assert.That(match.Game.ServiceId, Is.EqualTo("match"));
			Assert.That(match.RemoteSessionGuid, Is.EqualTo("host-session"));
			Assert.That(match.RemoteParticipantNonce, Is.EqualTo(hostNonce));
			Assert.That(match.RemoteBuildToken, Is.EqualTo(runtimeHash));

			var duplicate = new[]
			{
				new NearbyGameInfo("bonjour-service", matchingPayload),
				new NearbyGameInfo(
					NearbyLanDiscoveryProtocol.FormatServiceId(
						new System.Net.IPEndPoint(System.Net.IPAddress.Parse("192.168.50.15"), 1234)),
					matchingPayload)
			};
			Assert.That(IosMultiplayerSmokeDiscovery.TryFindUniqueRoom(
				duplicate, "run-42", launchNonce, hostNonce, runtimeHash, out match, out error), Is.True, error);
			Assert.That(NearbyLanDiscoveryProtocol.IsServiceId(match.Game.ServiceId), Is.True);

			var differentSession = matchingPayload.Replace("host-session", "other-session");
			var ambiguous = new[]
			{
				new NearbyGameInfo("first", matchingPayload),
				new NearbyGameInfo("second", differentSession)
			};
			Assert.That(IosMultiplayerSmokeDiscovery.TryFindUniqueRoom(
				ambiguous, "run-42", launchNonce, hostNonce, runtimeHash, out _, out error), Is.False);
			StringAssert.Contains("multiple", error.ToLowerInvariant());
		}

		[Test]
		public void MultiplayerSmokeParticipantIdentityFramesRoundTripFullLaunchIdentity()
		{
			var launchNonce = new string('1', 32);
			var participantNonce = new string('2', 32);
			var runtimeHash = new string('a', 64);
			var sessionGuid = "01234567-89ab-cdef-0123-456789abcdef";
			var frames = IosMultiplayerSmokeParticipantIdentity.CreateFrames(
				IosMultiplayerSmokeRole.Host, launchNonce, participantNonce, runtimeHash, sessionGuid);

			Assert.That(frames, Has.All.Length.LessThanOrEqualTo(16));
			Assert.That(IosMultiplayerSmokeParticipantIdentity.TryReassemble(
				frames.Reverse(), launchNonce, out var identity, out var error), Is.True, error);
			Assert.That(identity.Role, Is.EqualTo(IosMultiplayerSmokeRole.Host));
			Assert.That(identity.LaunchNonce, Is.EqualTo(launchNonce));
			Assert.That(identity.ParticipantNonce, Is.EqualTo(participantNonce));
			Assert.That(identity.RuntimeHash, Is.EqualTo(runtimeHash));
			Assert.That(identity.SessionGuid, Is.EqualTo(sessionGuid));
		}

		[Test]
		public void MultiplayerSmokeAcknowledgementAuthenticatesTheExactReceivedIdentity()
		{
			var launchNonce = new string('1', 32);
			var host = new IosMultiplayerSmokeParticipantIdentity(
				IosMultiplayerSmokeRole.Host,
				launchNonce,
				new string('2', 32),
				new string('a', 64),
				"01234567-89ab-cdef-0123-456789abcdef");
			var differentHost = new IosMultiplayerSmokeParticipantIdentity(
				IosMultiplayerSmokeRole.Host,
				launchNonce,
				new string('3', 32),
				new string('a', 64),
				"01234567-89ab-cdef-0123-456789abcdef");
			var client = new IosMultiplayerSmokeParticipantIdentity(
				IosMultiplayerSmokeRole.Client,
				launchNonce,
				new string('4', 32),
				new string('a', 64),
				"fedcba98-7654-3210-fedc-ba9876543210");
			var differentClient = new IosMultiplayerSmokeParticipantIdentity(
				IosMultiplayerSmokeRole.Client,
				launchNonce,
				new string('5', 32),
				new string('a', 64),
				"fedcba98-7654-3210-fedc-ba9876543210");

			var acknowledgement = IosMultiplayerSmokeParticipantIdentity.CreateAcknowledgementFrame(
				client, host);

			Assert.That(acknowledgement, Has.Length.EqualTo(16));
			Assert.That(IosMultiplayerSmokeParticipantIdentity.IsAcknowledgementFrame(
				acknowledgement, client, host), Is.True);
			Assert.That(IosMultiplayerSmokeParticipantIdentity.IsAcknowledgementFrame(
				acknowledgement, client, differentHost), Is.False);
			Assert.That(IosMultiplayerSmokeParticipantIdentity.IsAcknowledgementFrame(
				acknowledgement, differentClient, host), Is.False);
		}

		[Test]
		public void MultiplayerSmokeWaitsForReciprocalAcknowledgementAndPublishesItBeforeReady()
		{
			const string acknowledgement = "AC1234567890abcd";

			Assert.That(IosMultiplayerSmokeReadiness.CanIssueReady(
				remoteIdentityVerified: true,
				remoteAcknowledgedLocalIdentity: false,
				remoteIdentityCurrentlyValid: true,
				currentLocalName: acknowledgement,
				lastIssuedLocalName: acknowledgement,
				localAcknowledgement: acknowledgement), Is.False);
			Assert.That(IosMultiplayerSmokeReadiness.CanIssueReady(
				remoteIdentityVerified: true,
				remoteAcknowledgedLocalIdentity: true,
				remoteIdentityCurrentlyValid: true,
				currentLocalName: "H0015deadbeef000",
				lastIssuedLocalName: "H0015deadbeef000",
				localAcknowledgement: acknowledgement), Is.False);
			Assert.That(IosMultiplayerSmokeReadiness.CanIssueReady(
				remoteIdentityVerified: true,
				remoteAcknowledgedLocalIdentity: true,
				remoteIdentityCurrentlyValid: true,
				currentLocalName: acknowledgement,
				lastIssuedLocalName: acknowledgement,
				localAcknowledgement: acknowledgement), Is.True);

			Assert.That(IosMultiplayerSmokeReadiness.ShouldHoldAcknowledgement(
				remoteAcknowledgedLocalIdentity: false,
				currentLocalName: acknowledgement,
				lastIssuedLocalName: acknowledgement,
				localAcknowledgement: acknowledgement), Is.False,
				"The faster peer must keep cycling identity frames until the slower peer acknowledges it.");
			Assert.That(IosMultiplayerSmokeReadiness.ShouldHoldAcknowledgement(
				remoteAcknowledgedLocalIdentity: true,
				currentLocalName: acknowledgement,
				lastIssuedLocalName: acknowledgement,
				localAcknowledgement: acknowledgement), Is.True,
				"The final acknowledgement must remain server-visible after entering Ready.");
		}

		[Test]
		public void MultiplayerSmokeRuntimeHashCoversAssembliesAndRulesBytes()
		{
			var root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "smoke-runtime-hash");
			Directory.CreateDirectory(Path.Combine(root, "mods", "ra2", "rules"));
			foreach (var file in IosMultiplayerSmokeRuntimeIdentity.RequiredBundleFiles)
			{
				var path = Path.Combine(root, file);
				Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				File.WriteAllText(path, file);
			}
			File.WriteAllText(Path.Combine(root, "Info.plist"),
				"<plist><dict><key>CFBundleShortVersionString</key><string>1.2</string>" +
				"<key>CFBundleVersion</key><string>42</string></dict></plist>");

			File.WriteAllText(Path.Combine(root, "mods", "ra2", "rules", "world.yaml"), "World: first");
			var first = IosMultiplayerSmokeRuntimeIdentity.Compute(root);
			File.WriteAllText(Path.Combine(root, "mods", "ra2", "rules", "world.yaml"), "World: changed");
			var second = IosMultiplayerSmokeRuntimeIdentity.Compute(root);

			Assert.That(first, Has.Length.EqualTo(64));
			Assert.That(second, Is.Not.EqualTo(first));
		}

		[Test]
		public void LoadScreenInvokesMultiplayerSmokeOnlyThroughExplicitOptIn()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine",
				"OpenRA.Mods.Common", "LoadScreens", "BlankLoadScreen.cs"));

			StringAssert.Contains("if (IosMultiplayerSmoke.TryStart())", source);
		}

		[Test]
		public void MultiplayerSmokeRunnerRequiresTwoDevicesAndVerifiesBothJournals()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "ios", "scripts",
				"run-multiplayer-smoke.sh"));

			StringAssert.Contains("OPENRA_IOS_MULTIPLAYER_HOST_DEVICE_ID", source);
			StringAssert.Contains("OPENRA_IOS_MULTIPLAYER_CLIENT_DEVICE_ID", source);
			StringAssert.Contains("OPENRA_IOS_MULTIPLAYER_HOST_ENDPOINT", source);
			StringAssert.Contains("OPENRA_IOS_MULTIPLAYER_SMOKE_DISCOVERY_MODE", source);
			StringAssert.Contains("OPENRA_IOS_MULTIPLAYER_SMOKE_DISCOVERY_MODE:-auto", source);
			StringAssert.Contains("Auto-discovery acceptance requires", source);
			StringAssert.Contains("host_nonce", source);
			StringAssert.Contains("client_nonce", source);
			StringAssert.Contains("OPENRA_IOS_MULTIPLAYER_SMOKE_ROLE", source);
			StringAssert.Contains("OPENRA_IOS_MULTIPLAYER_SMOKE_TIMEOUT_SECONDS", source);
			StringAssert.Contains("Documents/OpenRA/Logs/MultiplayerSmoke", source);
			StringAssert.Contains("assert_journal", source);
			StringAssert.Contains("worldTick", source);
			StringAssert.Contains("outOfSync", source);
			StringAssert.Contains("expected_runtime_hash", source);
			StringAssert.Contains("launch_nonce", source);
			StringAssert.Contains("runner_started_utc", source);
			StringAssert.Contains("trap cleanup EXIT", source);
			StringAssert.Contains("--kill", source,
				"Smoke cleanup must not leave a stale lockstep match running on either device.");
			StringAssert.Contains("verify-$role.json", source,
				"Smoke cleanup must verify that the exact launched PID disappeared.");
			StringAssert.Contains("cleanup_status", source,
				"A failed cleanup must fail an otherwise successful smoke run.");
			StringAssert.Contains("baseline-$role.json", source,
				"Cleanup must recover the launched PID safely when launch JSON is missing or damaged.");

			var cleanupStart = source.IndexOf("terminate_launched_process()", System.StringComparison.Ordinal);
			var cleanupEnd = source.IndexOf("\ncleanup()", cleanupStart, System.StringComparison.Ordinal);
			var cleanupSource = source.Substring(cleanupStart, cleanupEnd - cleanupStart);
			var retryLoop = cleanupSource.IndexOf("while [ \"$attempt\" -le 5 ]", System.StringComparison.Ordinal);
			var forceKill = cleanupSource.IndexOf("device process terminate", System.StringComparison.Ordinal);
			Assert.That(forceKill, Is.GreaterThan(retryLoop),
				"Each CoreDevice kill attempt must be retried together with exact-PID verification.");
		}

		[Test]
		public void MultiplayerSmokeAutoDiscoveryUsesTheUnifiedNearbyTransport()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine",
				"OpenRA.Game", "Support", "IosMultiplayerSmoke.cs"));

			StringAssert.Contains("NearbyGameNetworking.GamesChanged +=", source);
			StringAssert.Contains("NearbyGameNetworking.StartBrowsing()", source);
			StringAssert.Contains("NearbyGameNetworking.FormatAddress", source);
			StringAssert.Contains("ConnectionTarget.TryParseGameAddress", source);
			StringAssert.Contains("DISCOVERING", source);
			StringAssert.Contains("discoveryElapsedMs", source);
			StringAssert.Contains("runtimeHash", source);
			StringAssert.Contains("launchNonce", source);
		}

		[Test]
		public void MultiplayerSmokeCannotAutoStartOrPassWithOnlyTheHost()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine",
				"OpenRA.Game", "Support", "IosMultiplayerSmoke.cs"));

			StringAssert.Contains("settings.EnableSingleplayer = false;", source);
			StringAssert.Contains("IosMultiplayerSmokeReadiness.CanIssueReady(", source);
			StringAssert.Contains("remoteIdentityVerified, remoteAcknowledgedLocalIdentity,", source);
			StringAssert.Contains(
				"remoteIdentityVerified && remoteIdentityCurrentlyValid && humans.Length == 2", source);
		}

		[Test]
		public void MultiplayerSmokeExercisesBlockedThenOneTapSafeStart()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine",
				"OpenRA.Game", "Support", "IosMultiplayerSmoke.cs"));

			StringAssert.Contains("selectedMap.Status == MapStatus.Available", source,
				"Game Ready must wait for the selected map to be locally readable.");
			StringAssert.Contains("localClient.IsMapReadyFor(currentMapUid)", source,
				"The smoke must observe the server-accepted current-UID handshake before Ready.");
			StringAssert.Contains("Session.ClientMapPhase.Downloading, 42", source,
				"One participant must expose a deterministic 42% blocker.");
			StringAssert.Contains("orderManager.SafeStartRejected += HandleSafeStartRejected", source);
			StringAssert.Contains("Session.LobbyStartBlockReason.ClientMapDownloading", source);
			StringAssert.Contains("blockedStartRequest = new LobbySafeStartRequest(currentMapUid, 1)", source);
			StringAssert.Contains("finalStartRequest = new LobbySafeStartRequest(currentMapUid, 2)", source);
			StringAssert.Contains("rejection.RequestId != blockedStartRequest.RequestId", source);
			StringAssert.Contains("Order.Command(blockedStartRequest.ToCommand())", source);
			StringAssert.Contains("Order.Command(finalStartRequest.ToCommand())", source,
				"The blocked probe and accepted launch must use distinct canonical requests.");
			StringAssert.DoesNotContain("Order.Command(\"startgame\")", source,
				"The smoke must not bypass the new safe-start protocol with the legacy command.");
		}
	}
}
