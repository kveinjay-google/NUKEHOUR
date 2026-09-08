#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using System;
using OpenRA.Graphics;
using OpenRA.Network;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public static class IosLobbyStartStatus
	{
		[FluentReference]
		const string Validating = "button-ios-lobby-start-validating";
		[FluentReference]
		const string MapUnavailable = "button-ios-lobby-start-map-unavailable";
		[FluentReference("player")]
		const string ClientFailed = "button-ios-lobby-start-client-failed";
		[FluentReference("count")]
		const string ClientsFailed = "button-ios-lobby-start-clients-failed";
		[FluentReference("player")]
		const string ClientVerifying = "button-ios-lobby-start-client-verifying";
		[FluentReference("count")]
		const string ClientsVerifying = "button-ios-lobby-start-clients-verifying";
		[FluentReference("player", "progress")]
		const string ClientDownloading = "button-ios-lobby-start-client-downloading";
		[FluentReference("count", "progress")]
		const string ClientsDownloading = "button-ios-lobby-start-clients-downloading";
		[FluentReference("player")]
		const string ClientWaiting = "button-ios-lobby-start-client-waiting";
		[FluentReference("count")]
		const string ClientsWaiting = "button-ios-lobby-start-clients-waiting";
		[FluentReference("status", "count")]
		const string ClientAndOthers = "button-ios-lobby-start-client-and-others";
		[FluentReference("count")]
		const string ClientsNotReady = "button-ios-lobby-start-clients-not-ready";
		[FluentReference]
		const string RequiredSlot = "button-ios-lobby-start-required-slot";
		[FluentReference]
		const string NoPlayers = "button-ios-lobby-start-no-players";
		[FluentReference]
		const string TwoPlayers = "button-ios-lobby-start-two-players";
		[FluentReference]
		const string Spawns = "button-ios-lobby-start-spawns";
		[FluentReference]
		const string Waiting = "button-ios-lobby-start-waiting";
		[FluentReference]
		const string RequiresHost = "notification-requires-host";

		public static string Format(LobbyStartAvailability availability, int width, SpriteFont font,
			Func<int, string> clientName, Func<string, object[], string> localize)
		{
			if (font == null)
				throw new ArgumentNullException(nameof(font));
			if (clientName == null)
				throw new ArgumentNullException(nameof(clientName));
			if (localize == null)
				throw new ArgumentNullException(nameof(localize));

			return availability.Reason switch
			{
				Session.LobbyStartBlockReason.None => string.Empty,
				Session.LobbyStartBlockReason.ServerMapValidating =>
					localize(Validating, Array.Empty<object>()),
				Session.LobbyStartBlockReason.ServerMapIncompatible or
				Session.LobbyStartBlockReason.ServerMapNotPlayable or
				Session.LobbyStartBlockReason.SelectedMapUnavailable =>
					localize(MapUnavailable, Array.Empty<object>()),
				Session.LobbyStartBlockReason.ClientMapError or
				Session.LobbyStartBlockReason.ClientMapUnavailable => ClientStatus(
					availability, width, font, clientName, localize,
					ClientFailed, ClientsFailed),
				Session.LobbyStartBlockReason.ClientMapInstallingOrVerifying => ClientStatus(
					availability, width, font, clientName, localize,
					ClientVerifying, ClientsVerifying),
				Session.LobbyStartBlockReason.ClientMapDownloading when availability.Progress >= 0 => ClientStatus(
					availability, width, font, clientName, localize,
					ClientDownloading, ClientsDownloading,
					"progress", availability.Progress),
				Session.LobbyStartBlockReason.ClientMapDownloading or
				Session.LobbyStartBlockReason.ClientMapSearching or
				Session.LobbyStartBlockReason.ClientMapWaitingForDownload or
				Session.LobbyStartBlockReason.ClientMapUnknown or
				Session.LobbyStartBlockReason.ClientGameNotReady => ClientStatus(
					availability, width, font, clientName, localize,
					ClientWaiting, ClientsWaiting),
				Session.LobbyStartBlockReason.RequiredSlotEmpty =>
					localize(RequiredSlot, Array.Empty<object>()),
				Session.LobbyStartBlockReason.NoPlayers =>
					localize(NoPlayers, Array.Empty<object>()),
				Session.LobbyStartBlockReason.InsufficientHumans =>
					localize(TwoPlayers, Array.Empty<object>()),
				Session.LobbyStartBlockReason.InsufficientSpawnPoints =>
					localize(Spawns, Array.Empty<object>()),
				Session.LobbyStartBlockReason.RequesterNotAdmin =>
					localize(RequiresHost, Array.Empty<object>()),
				_ => localize(Waiting, Array.Empty<object>())
			};
		}

		static string ClientStatus(LobbyStartAvailability availability, int width, SpriteFont font,
			Func<int, string> clientName, Func<string, object[], string> localize,
			string namedKey, string aggregateKey, params object[] extraArguments)
		{
			var name = availability.ClientIndex >= 0 ? clientName(availability.ClientIndex) : string.Empty;
			var namedArguments = new object[extraArguments.Length + 2];
			namedArguments[0] = "player";
			namedArguments[1] = name;
			Array.Copy(extraArguments, 0, namedArguments, 2, extraArguments.Length);
			var named = localize(namedKey, namedArguments);
			var count = availability.AdditionalBlockingClientCount + 1;
			if (count == 1)
			{
				if (font.Measure(named).X <= Math.Max(0, width))
					return named;

				var aggregateArguments = new object[extraArguments.Length + 2];
				aggregateArguments[0] = "count";
				aggregateArguments[1] = count;
				Array.Copy(extraArguments, 0, aggregateArguments, 2, extraArguments.Length);
				return localize(aggregateKey, aggregateArguments);
			}

			var namedAndOthers = localize(ClientAndOthers, new object[]
			{
				"status", named,
				"count", availability.AdditionalBlockingClientCount
			});
			if (font.Measure(namedAndOthers).X <= Math.Max(0, width))
				return namedAndOthers;

			return localize(ClientsNotReady, new object[] { "count", count });
		}
	}
}
