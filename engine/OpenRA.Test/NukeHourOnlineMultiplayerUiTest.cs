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

using System.IO;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NukeHourOnlineMultiplayerUiTest
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
		public void LocalAndOnlineModesHavePermanentSeparateResponsibilities()
		{
			Assert.Multiple(() =>
			{
				Assert.That(MultiplayerServerModePolicy.UsesNearbyDiscovery(MultiplayerServerMode.Local), Is.True);
				Assert.That(MultiplayerServerModePolicy.UsesOnlineDirectory(MultiplayerServerMode.Local), Is.False);
				Assert.That(MultiplayerServerModePolicy.ShowsLocalHosting(MultiplayerServerMode.Local), Is.True);
				Assert.That(MultiplayerServerModePolicy.ShowsDirectIp(MultiplayerServerMode.Local), Is.True);
				Assert.That(MultiplayerServerModePolicy.UsesNearbyDiscovery(MultiplayerServerMode.Online), Is.False);
				Assert.That(MultiplayerServerModePolicy.UsesOnlineDirectory(MultiplayerServerMode.Online), Is.True);
				Assert.That(MultiplayerServerModePolicy.ShowsLocalHosting(MultiplayerServerMode.Online), Is.False);
				Assert.That(MultiplayerServerModePolicy.ShowsDirectIp(MultiplayerServerMode.Online), Is.False);
			});
		}

		[TestCase(OnlineRoomCompatibilityReason.HandshakeSchemaMismatch, "label-online-room-error-handshake")]
		[TestCase(OnlineRoomCompatibilityReason.OrdersVersionMismatch, "label-online-room-error-orders")]
		[TestCase(OnlineRoomCompatibilityReason.EngineMismatch, "label-online-room-error-engine")]
		[TestCase(OnlineRoomCompatibilityReason.ModMismatch, "label-online-room-error-mod")]
		[TestCase(OnlineRoomCompatibilityReason.ModVersionMismatch, "label-online-room-error-version")]
		[TestCase(OnlineRoomCompatibilityReason.RuntimeCapabilityMismatch, "label-online-room-error-resources")]
		public void EveryCompatibilityFailureHasAStableLocalizedReason(
			OnlineRoomCompatibilityReason reason, string expected)
		{
			Assert.That(OnlineRoomCompatibility.MessageKey(reason), Is.EqualTo(expected));
		}

		[Test]
		public void MultiplayerChromeExposesModesAndCodeWithoutAnOnlineIpSurface()
		{
			var root = RepositoryRoot();
			var yaml = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "chrome", "multiplayer-browser.yaml"));
			var logic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "ServerListLogic.cs"));

			foreach (var id in new[]
			{
				"LOCAL_MODE_BUTTON", "ONLINE_MODE_BUTTON", "ROOM_CODE_INPUT", "ROOM_CODE_BUTTON",
				"DIRECTCONNECT_BUTTON", "CREATE_BUTTON"
			})
				Assert.That(yaml, Does.Contain("@" + id + ":"), id);

			Assert.That(logic, Does.Contain("!currentServer.IsOnlineRoom"));
			Assert.That(logic, Does.Not.Contain("services.ServerList"),
				"Local Multiplayer must never depend on the legacy public master server.");
		}

		[Test]
		public void NukeHourManifestUsesTheFormalHttpsLobbyEndpoint()
		{
			var root = RepositoryRoot();
			var manifest = File.ReadAllText(Path.Combine(root, "mods", "ra2", "mod.yaml"));
			Assert.That(manifest, Does.Contain("WebServices:\n\tOnlineLobby: https://lobby.nukehour.com/"));
		}
	}
}
