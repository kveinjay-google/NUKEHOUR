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

using OpenRA.Graphics;
using OpenRA.Network;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class RegisteredProfileTooltipLogic : ChromeLogic
	{
		[FluentReference]
		const string LoadingPlayerProfileFailed = "label-loading-player-profile-failed";

		[ObjectCreator.UseCtor]
		public RegisteredProfileTooltipLogic(
			Widget widget,
			WorldRenderer worldRenderer,
			ModData modData,
			Session.Client client)
		{
			var header = widget.Get("HEADER");
			header.Get("PROFILE_HEADER").IsVisible = () => false;

			var messageHeader = header.Get("MESSAGE_HEADER");
			messageHeader.IsVisible = () => true;
			var message = messageHeader.Get<LabelWidget>("MESSAGE");
			message.GetText = () => FluentProvider.GetMessage(LoadingPlayerProfileFailed);
		}
	}
}
