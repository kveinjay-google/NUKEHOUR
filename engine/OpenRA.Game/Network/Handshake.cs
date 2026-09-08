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

namespace OpenRA.Network
{
	public enum RuntimeContractCompatibilityReason
	{
		Compatible = 0,
		MissingIdentity = 1,
		MalformedIdentity = 2,
		ProfileMismatch = 3,
		ContractMismatch = 4,
	}

	static class RuntimeContractHandshake
	{
		public static RuntimeContractCompatibilityReason Compare(
			RuntimeContractIdentity local,
			string remoteProfile,
			string remoteContract)
		{
			if (!local.IsValid)
				return RuntimeContractCompatibilityReason.MalformedIdentity;

			if (remoteProfile == null && remoteContract == null)
				return local.IsLegacy
					? RuntimeContractCompatibilityReason.Compatible
					: RuntimeContractCompatibilityReason.MissingIdentity;

			if (string.IsNullOrEmpty(remoteProfile) || string.IsNullOrEmpty(remoteContract))
				return RuntimeContractCompatibilityReason.MalformedIdentity;

			RuntimeContractIdentity remote;
			try
			{
				remote = new RuntimeContractIdentity(remoteProfile, remoteContract);
			}
			catch (ArgumentException)
			{
				return RuntimeContractCompatibilityReason.MalformedIdentity;
			}

			if (local.IsLegacy)
				return RuntimeContractCompatibilityReason.MissingIdentity;
			if (!string.Equals(local.ProfileId, remote.ProfileId, StringComparison.Ordinal))
				return RuntimeContractCompatibilityReason.ProfileMismatch;
			if (!string.Equals(local.ContractDigest, remote.ContractDigest, StringComparison.Ordinal))
				return RuntimeContractCompatibilityReason.ContractMismatch;

			return RuntimeContractCompatibilityReason.Compatible;
		}

		public static void GetFields(
			RuntimeContractIdentity identity,
			out string runtimeProfile,
			out string runtimeContract)
		{
			if (!identity.IsValid)
				throw new ArgumentException("The runtime contract identity is invalid.", nameof(identity));

			runtimeProfile = identity.IsLegacy ? null : identity.ProfileId;
			runtimeContract = identity.IsLegacy ? null : identity.ContractDigest;
		}
	}

	public class HandshakeRequest
	{
		public string Mod;
		public string Version;
		public string RuntimeProfile;
		public string RuntimeContract;
		public string AuthToken;

		internal RuntimeContractCompatibilityReason RuntimeContractCompatibilityWith(
			RuntimeContractIdentity local) =>
			RuntimeContractHandshake.Compare(local, RuntimeProfile, RuntimeContract);

		public static HandshakeRequest Deserialize(string data, string name)
		{
			var handshake = new HandshakeRequest();
			FieldLoader.Load(handshake, MiniYaml.FromString(data, name).First().Value);
			return handshake;
		}

		public string Serialize()
		{
			var handshake = FieldSaver.Save(this);
			if (RuntimeProfile == null && RuntimeContract == null)
				handshake = handshake.WithNodes(handshake.Nodes.Where(node =>
					node.Key != nameof(RuntimeProfile) && node.Key != nameof(RuntimeContract)));

			var data = new List<MiniYamlNode> { new("Handshake", handshake) };
			return data.WriteToString();
		}
	}

	public class HandshakeResponse
	{
		public string Mod;
		public string Version;
		public string RuntimeProfile;
		public string RuntimeContract;
		public string Password;

		internal RuntimeContractCompatibilityReason RuntimeContractCompatibilityWith(
			RuntimeContractIdentity local) =>
			RuntimeContractHandshake.Compare(local, RuntimeProfile, RuntimeContract);

		// Default value is hardcoded to 7 so that newer servers
		// (which define OrdersProtocol > 7) can detect older clients
		public int OrdersProtocol = 7;

		// For player authentication
		public string Fingerprint;
		public string AuthSignature;

		[FieldLoader.Ignore]
		public Session.Client Client;

		public static HandshakeResponse Deserialize(string data, string name)
		{
			var handshake = new HandshakeResponse
			{
				Client = new Session.Client()
			};

			var ys = MiniYaml.FromString(data, name);
			foreach (var y in ys)
			{
				switch (y.Key)
				{
					case "Handshake":
						FieldLoader.Load(handshake, y.Value);
						break;
					case "Client":
						FieldLoader.Load(handshake.Client, y.Value);
						break;
				}
			}

			return handshake;
		}

		public string Serialize()
		{
			var includeRuntimeContract = RuntimeProfile != null || RuntimeContract != null;
			var data = new List<MiniYamlNode>
			{
				new("Handshake", null,
					new[]
					{
						"Mod", "Version", "RuntimeProfile", "RuntimeContract", "Password",
						"Fingerprint", "AuthSignature", "OrdersProtocol"
					}.Where(field => includeRuntimeContract ||
						(field != nameof(RuntimeProfile) && field != nameof(RuntimeContract)))
						.Select(p => FieldSaver.SaveField(this, p)).ToList()),
				new("Client", FieldSaver.Save(Client))
			};

			return data.WriteToString();
		}
	}
}
