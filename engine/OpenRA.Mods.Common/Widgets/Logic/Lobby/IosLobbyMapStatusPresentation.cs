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
using OpenRA.Network;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public readonly struct IosLobbyMapStatusPresentation
	{
		public bool ShowMapStatus { get; }
		public string Text { get; }
		public string Icon { get; }
		public bool ShowReady => !ShowMapStatus;

		IosLobbyMapStatusPresentation(bool showMapStatus, string text, string icon)
		{
			ShowMapStatus = showMapStatus;
			Text = text;
			Icon = icon;
		}

		public static IosLobbyMapStatusPresentation For(
			Session.Client client, string currentMapUid, bool isIos)
		{
			if (!isIos || client == null || client.Bot != null ||
				!string.Equals(client.MapUid, currentMapUid, StringComparison.Ordinal) ||
				client.MapPhase == Session.ClientMapPhase.Ready)
				return new IosLobbyMapStatusPresentation(false, string.Empty, string.Empty);

			return client.MapPhase switch
			{
				Session.ClientMapPhase.Error or Session.ClientMapPhase.Unavailable =>
					new IosLobbyMapStatusPresentation(true, string.Empty, "!"),
				Session.ClientMapPhase.Downloading when client.MapProgress >= 0 =>
					new IosLobbyMapStatusPresentation(true, $"{client.MapProgress}%", string.Empty),
				_ => new IosLobbyMapStatusPresentation(true, "…", string.Empty)
			};
		}
	}
}
