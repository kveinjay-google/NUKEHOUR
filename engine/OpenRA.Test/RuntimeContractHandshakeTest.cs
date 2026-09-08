#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RuntimeContractHandshakeTest
	{
		const string DigestA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		const string DigestB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

		static readonly RuntimeContractIdentity BaseA = new("ra2-base", DigestA);
		static readonly RuntimeContractIdentity BaseB = new("ra2-base", DigestB);
		static readonly RuntimeContractIdentity YuriA = new("ra2-yuri", DigestA);

		[Test]
		public void LegacyClientAndServerRemainCompatible()
		{
			var request = RoundTrip(new HandshakeRequest { Mod = "ra2", Version = "1" });
			var response = RoundTrip(new HandshakeResponse
			{
				Mod = "ra2",
				Version = "1",
				Client = new Session.Client(),
			});

			Assert.Multiple(() =>
			{
				Assert.That(request.RuntimeContractCompatibilityWith(RuntimeContractIdentity.Legacy),
					Is.EqualTo(RuntimeContractCompatibilityReason.Compatible));
				Assert.That(response.RuntimeContractCompatibilityWith(RuntimeContractIdentity.Legacy),
					Is.EqualTo(RuntimeContractCompatibilityReason.Compatible));
			});
		}

		[Test]
		public void ExactProfileAndDigestRoundTripAndAdmitInBothDirections()
		{
			var request = RoundTrip(new HandshakeRequest
			{
				Mod = "ra2",
				Version = "1",
				RuntimeProfile = BaseA.ProfileId,
				RuntimeContract = BaseA.ContractDigest,
			});
			var response = RoundTrip(new HandshakeResponse
			{
				Mod = "ra2",
				Version = "1",
				RuntimeProfile = BaseA.ProfileId,
				RuntimeContract = BaseA.ContractDigest,
				Client = new Session.Client(),
			});

			Assert.Multiple(() =>
			{
				Assert.That(request.RuntimeProfile, Is.EqualTo(BaseA.ProfileId));
				Assert.That(request.RuntimeContract, Is.EqualTo(BaseA.ContractDigest));
				Assert.That(response.RuntimeProfile, Is.EqualTo(BaseA.ProfileId));
				Assert.That(response.RuntimeContract, Is.EqualTo(BaseA.ContractDigest));
				Assert.That(request.RuntimeContractCompatibilityWith(BaseA),
					Is.EqualTo(RuntimeContractCompatibilityReason.Compatible),
					"The client must admit an exact server identity.");
				Assert.That(response.RuntimeContractCompatibilityWith(BaseA),
					Is.EqualTo(RuntimeContractCompatibilityReason.Compatible),
					"The server must admit an exact client identity.");
			});
		}

		[TestCase(null, DigestA)]
		[TestCase("ra2-base", null)]
		[TestCase("", DigestA)]
		[TestCase("ra2-base", "")]
		[TestCase("RA2-BASE", DigestA)]
		[TestCase("ra2-base", "abcd")]
		public void OneSidedOrMalformedIdentityIsRejectedBeforeAdmission(
			string profile, string contract)
		{
			var request = new HandshakeRequest
			{
				RuntimeProfile = profile,
				RuntimeContract = contract,
			};
			var response = new HandshakeResponse
			{
				RuntimeProfile = profile,
				RuntimeContract = contract,
				Client = new Session.Client(),
			};

			Assert.Multiple(() =>
			{
				Assert.That(request.RuntimeContractCompatibilityWith(BaseA),
					Is.EqualTo(RuntimeContractCompatibilityReason.MalformedIdentity));
				Assert.That(response.RuntimeContractCompatibilityWith(BaseA),
					Is.EqualTo(RuntimeContractCompatibilityReason.MalformedIdentity));
			});
		}

		[Test]
		public void OneLegacyIdentityIsRejectedInBothDirections()
		{
			var profiledRequest = new HandshakeRequest
			{
				RuntimeProfile = BaseA.ProfileId,
				RuntimeContract = BaseA.ContractDigest,
			};
			var legacyRequest = new HandshakeRequest();
			var profiledResponse = new HandshakeResponse
			{
				RuntimeProfile = BaseA.ProfileId,
				RuntimeContract = BaseA.ContractDigest,
			};
			var legacyResponse = new HandshakeResponse();

			Assert.Multiple(() =>
			{
				Assert.That(profiledRequest.RuntimeContractCompatibilityWith(RuntimeContractIdentity.Legacy),
					Is.EqualTo(RuntimeContractCompatibilityReason.MissingIdentity));
				Assert.That(legacyRequest.RuntimeContractCompatibilityWith(BaseA),
					Is.EqualTo(RuntimeContractCompatibilityReason.MissingIdentity));
				Assert.That(profiledResponse.RuntimeContractCompatibilityWith(RuntimeContractIdentity.Legacy),
					Is.EqualTo(RuntimeContractCompatibilityReason.MissingIdentity));
				Assert.That(legacyResponse.RuntimeContractCompatibilityWith(BaseA),
					Is.EqualTo(RuntimeContractCompatibilityReason.MissingIdentity));
			});
		}

		[Test]
		public void ProfileAndDigestMismatchesHaveDistinctClosedReasons()
		{
			var profileMismatch = new HandshakeRequest
			{
				RuntimeProfile = YuriA.ProfileId,
				RuntimeContract = YuriA.ContractDigest,
			};
			var digestMismatch = new HandshakeResponse
			{
				RuntimeProfile = BaseB.ProfileId,
				RuntimeContract = BaseB.ContractDigest,
			};

			Assert.Multiple(() =>
			{
				Assert.That(profileMismatch.RuntimeContractCompatibilityWith(BaseA),
					Is.EqualTo(RuntimeContractCompatibilityReason.ProfileMismatch));
				Assert.That(digestMismatch.RuntimeContractCompatibilityWith(BaseA),
					Is.EqualTo(RuntimeContractCompatibilityReason.ContractMismatch));
			});
		}

		[Test]
		public void DuplicateRuntimeIdentityFieldsAreRejectedDuringDeserialization()
		{
			var duplicateRequest = "Handshake:\n" +
				"\tRuntimeProfile: ra2-base\n" +
				"\tRuntimeProfile: ra2-yuri\n" +
				$"\tRuntimeContract: {DigestA}\n";
			var duplicateResponse = "Handshake:\n" +
				"\tRuntimeProfile: ra2-base\n" +
				$"\tRuntimeContract: {DigestA}\n" +
				$"\tRuntimeContract: {DigestB}\n" +
				"Client:\n";

			Assert.Multiple(() =>
			{
				Assert.Throws<InvalidDataException>(() =>
					HandshakeRequest.Deserialize(duplicateRequest, "duplicate request"));
				Assert.Throws<InvalidDataException>(() =>
					HandshakeResponse.Deserialize(duplicateResponse, "duplicate response"));
			});
		}

		[Test]
		public void UnknownRuntimeFieldCannotOverrideTheKnownIdentity()
		{
			var request = HandshakeRequest.Deserialize("Handshake:\n" +
				"\tRuntimeProfile: ra2-base\n" +
				$"\tRuntimeContract: {DigestB}\n" +
				$"\tRuntimeContractOverride: {DigestA}\n", "unknown request field");
			var response = HandshakeResponse.Deserialize("Handshake:\n" +
				"\tRuntimeProfile: ra2-base\n" +
				$"\tRuntimeContract: {DigestB}\n" +
				$"\tRuntimeContractOverride: {DigestA}\n" +
				"Client:\n", "unknown response field");

			Assert.Multiple(() =>
			{
				Assert.That(request.RuntimeContractCompatibilityWith(BaseA),
					Is.EqualTo(RuntimeContractCompatibilityReason.ContractMismatch));
				Assert.That(response.RuntimeContractCompatibilityWith(BaseA),
					Is.EqualTo(RuntimeContractCompatibilityReason.ContractMismatch));
			});
		}

		[Test]
		public void SessionGlobalRoundTripsOptionalRuntimeIdentity()
		{
			var global = new Session.Global
			{
				ServerName = "Contract server",
				RuntimeProfile = BaseA.ProfileId,
				RuntimeContract = BaseA.ContractDigest,
			};

			var roundTrip = Session.Global.Deserialize(global.Serialize().Value);

			Assert.Multiple(() =>
			{
				Assert.That(roundTrip.RuntimeProfile, Is.EqualTo(BaseA.ProfileId));
				Assert.That(roundTrip.RuntimeContract, Is.EqualTo(BaseA.ContractDigest));
			});
		}

		[Test]
		public void LegacySessionAndAdvertisementOmitOptionalRuntimeIdentity()
		{
			var globalFields = new Session.Global { ServerName = "Legacy server" }
				.Serialize().Value.Nodes.Select(node => node.Key).ToArray();
			var server = UninitializedGameServer();
			SetField(server, nameof(GameServer.Mod), "ra2");
			SetField(server, nameof(GameServer.Version), "1");
			SetField(server, nameof(GameServer.Clients), Array.Empty<GameClient>());
			var advertisementFields = MiniYaml.FromString(
				server.ToPOSTData(lanGame: false), "legacy advertisement").Single().Value.Nodes
				.Select(node => node.Key).ToArray();

			Assert.Multiple(() =>
			{
				Assert.That(globalFields, Does.Not.Contain(nameof(Session.Global.RuntimeProfile)));
				Assert.That(globalFields, Does.Not.Contain(nameof(Session.Global.RuntimeContract)));
				Assert.That(advertisementFields, Does.Not.Contain(nameof(GameServer.RuntimeProfile)));
				Assert.That(advertisementFields, Does.Not.Contain(nameof(GameServer.RuntimeContract)));
			});
		}

		[Test]
		public void GameServerAdvertisementRoundTripsRuntimeIdentity()
		{
			var server = UninitializedGameServer();
			SetField(server, nameof(GameServer.Mod), "ra2");
			SetField(server, nameof(GameServer.Version), "1");
			SetField(server, nameof(GameServer.RuntimeProfile), BaseA.ProfileId);
			SetField(server, nameof(GameServer.RuntimeContract), BaseA.ContractDigest);
			SetField(server, nameof(GameServer.Clients), Array.Empty<GameClient>());

			var yaml = MiniYaml.FromString(server.ToPOSTData(lanGame: false), "advertisement").Single().Value;
			var fields = yaml.Nodes.ToDictionary(node => node.Key, node => node.Value.Value,
				StringComparer.Ordinal);

			Assert.Multiple(() =>
			{
				Assert.That(fields[nameof(GameServer.RuntimeProfile)], Is.EqualTo(BaseA.ProfileId));
				Assert.That(fields[nameof(GameServer.RuntimeContract)], Is.EqualTo(BaseA.ContractDigest));
			});
		}

		[Test]
		public void BrowserUsesTypedCompatibilityReasonAndPreservesLegacyOverload()
		{
			Assert.Multiple(() =>
			{
				Assert.That(GameServer.AdvertisedCompatibility(
					"ra2", "1", BaseA.ProfileId, BaseA.ContractDigest,
					"ra2", "1", BaseA, externalRegistrationAvailable: false),
					Is.EqualTo(GameServerCompatibilityReason.Compatible));
				Assert.That(GameServer.AdvertisedCompatibility(
					"ra2", "1", YuriA.ProfileId, YuriA.ContractDigest,
					"ra2", "1", BaseA, externalRegistrationAvailable: false),
					Is.EqualTo(GameServerCompatibilityReason.RuntimeProfileMismatch));
				Assert.That(GameServer.AdvertisedCompatibility(
					"ra2", "1", null, null,
					"ra2", "1", BaseA, externalRegistrationAvailable: false),
					Is.EqualTo(GameServerCompatibilityReason.RuntimeIdentityMissing));
				Assert.That(GameServer.AdvertisedCompatibility(
					"ra2", "1", BaseA.ProfileId, null,
					"ra2", "1", BaseA, externalRegistrationAvailable: false),
					Is.EqualTo(GameServerCompatibilityReason.RuntimeIdentityMalformed));
				Assert.That(GameServer.AdvertisedCompatibility(
					"ra2", "1", BaseB.ProfileId, BaseB.ContractDigest,
					"ra2", "1", BaseA, externalRegistrationAvailable: false),
					Is.EqualTo(GameServerCompatibilityReason.RuntimeContractMismatch));
				Assert.That(GameServer.AdvertisedCompatibility(
					"ra2", "2", null, null,
					"ra2", "1", BaseA, externalRegistrationAvailable: false),
					Is.EqualTo(GameServerCompatibilityReason.ModOrVersionMismatch));
				Assert.That(GameServer.AdvertisedCompatibility(
					"ra2", "2", null, null,
					"ra2", "1", BaseA, externalRegistrationAvailable: true),
					Is.EqualTo(GameServerCompatibilityReason.Compatible));
				Assert.That(ServerListLogic.IsCompatibilityUnavailable(
					GameServerCompatibilityReason.RuntimeProfileMismatch), Is.True);
				Assert.That(ServerListLogic.IsCompatibilityUnavailable(
					GameServerCompatibilityReason.Compatible), Is.False);
				Assert.That(GameServer.IsAdvertisedModCompatible(
					"ra2", "1", "ra2", "1", externalRegistrationAvailable: false), Is.True,
					"The existing five-argument compatibility overload must remain source-compatible.");
			});
		}

		[Test]
		public void ClientAndServerRejectRuntimeMismatchBeforeCreatingOrAdmittingAClient()
		{
			var root = RepositoryRoot();
			var server = File.ReadAllText(Path.Combine(
				root, "engine", "OpenRA.Game", "Server", "Server.cs"));
			var validateClient = MethodBody(server, "void ValidateClient(");
			var serverRejection = validateClient.IndexOf(
				"handshake.RuntimeContractCompatibilityWith", StringComparison.Ordinal);
			var clientCreation = validateClient.IndexOf(
				"var client = new Session.Client", StringComparison.Ordinal);
			var lobbyAdmission = validateClient.IndexOf(
				"LobbyInfo.Clients.Add(client)", StringComparison.Ordinal);

			var unitOrders = File.ReadAllText(Path.Combine(
				root, "engine", "OpenRA.Game", "Network", "UnitOrders.cs"));
			var handshakeStart = unitOrders.IndexOf("case \"HandshakeRequest\":", StringComparison.Ordinal);
			var handshakeEnd = unitOrders.IndexOf("case \"ServerError\":", handshakeStart, StringComparison.Ordinal);
			var handshakeCase = unitOrders.Substring(handshakeStart, handshakeEnd - handshakeStart);
			var clientRejection = handshakeCase.IndexOf(
				"request.RuntimeContractCompatibilityWith", StringComparison.Ordinal);
			var settingsSave = handshakeCase.IndexOf("Game.Settings.Save()", StringComparison.Ordinal);
			var responseCreation = handshakeCase.IndexOf("new HandshakeResponse", StringComparison.Ordinal);
			var responseSend = handshakeCase.IndexOf(
				"orderManager.IssueOrder(new Order(\"HandshakeResponse\"", StringComparison.Ordinal);

			Assert.Multiple(() =>
			{
				Assert.That(serverRejection, Is.GreaterThanOrEqualTo(0));
				Assert.That(clientCreation, Is.GreaterThan(serverRejection));
				Assert.That(lobbyAdmission, Is.GreaterThan(clientCreation));
				Assert.That(clientRejection, Is.GreaterThanOrEqualTo(0));
				Assert.That(settingsSave, Is.GreaterThan(clientRejection));
				Assert.That(responseCreation, Is.GreaterThan(clientRejection));
				Assert.That(responseSend, Is.GreaterThan(responseCreation));
			});
		}

		static HandshakeRequest RoundTrip(HandshakeRequest request) =>
			HandshakeRequest.Deserialize(request.Serialize(), "request");

		static HandshakeResponse RoundTrip(HandshakeResponse response) =>
			HandshakeResponse.Deserialize(response.Serialize(), "response");

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
			var start = source.IndexOf(signature, StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing method {signature}.");
			var openBrace = source.IndexOf('{', start);
			Assert.That(openBrace, Is.GreaterThan(start), $"Missing body for {signature}.");

			var depth = 0;
			for (var i = openBrace; i < source.Length; i++)
			{
				if (source[i] == '{')
					depth++;
				else if (source[i] == '}' && --depth == 0)
					return source.Substring(openBrace, i - openBrace + 1);
			}

			Assert.Fail($"Unterminated body for {signature}.");
			return null;
		}

#pragma warning disable SYSLIB0050
		static GameServer UninitializedGameServer() =>
			(GameServer)FormatterServices.GetUninitializedObject(typeof(GameServer));
#pragma warning restore SYSLIB0050

		static void SetField(GameServer server, string name, object value)
		{
			var field = typeof(GameServer).GetField(name,
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Assert.That(field, Is.Not.Null, $"Missing GameServer field {name}.");
			field.SetValue(server, value);
		}
	}
}
