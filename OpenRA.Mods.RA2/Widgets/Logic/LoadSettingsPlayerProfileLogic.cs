#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets.Logic
{
	// Loads the forum account / player profile panel for Settings.
	// Unlike LoadLocalPlayerProfileLogic, this always shows the full connect UI
	// (main-menu logic hides it whenever another window is open).
	public class LoadSettingsPlayerProfileLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public LoadSettingsPlayerProfileLogic(Widget widget, World world)
		{
			Func<bool> minimalProfile = () => false;

			Game.LoadWidget(world, "LOCAL_PROFILE_PANEL", widget, new WidgetArgs()
			{
				{ "minimalProfile", minimalProfile }
			});
		}
	}
}
