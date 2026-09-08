#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets.Logic
{
	public sealed class FloatingControlsToggleButtonLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public FloatingControlsToggleButtonLogic(ButtonWidget widget)
		{
			widget.IsVisible = () => Platform.IsIOS;
			widget.IsHighlighted = () => IosFloatingControlsPreferences.Visible;
			widget.OnClick = () =>
				IosFloatingControlsPreferences.Visible = !IosFloatingControlsPreferences.Visible;
		}
	}
}
