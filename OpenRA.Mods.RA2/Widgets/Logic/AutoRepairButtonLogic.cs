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
using OpenRA.Mods.RA2.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets.Logic
{
	public class AutoRepairButtonLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public AutoRepairButtonLogic(ButtonWidget widget, World world)
		{
			AutoRepairManager Manager() =>
				world.LocalPlayer?.PlayerActor.TraitOrDefault<AutoRepairManager>();

			widget.OnClick = () =>
			{
				if (world.LocalPlayer == null || world.IsReplay)
					return;

				world.IssueOrder(new Order("AutoRepair", world.LocalPlayer.PlayerActor, false));
			};

			widget.IsHighlighted = () => Manager()?.Enabled == true;

			var icon = widget.Get<ImageWidget>("ICON");
			icon.GetImageName = () => Manager()?.Enabled == true ? "repair-active" : "repair";
		}
	}
}
