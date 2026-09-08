#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets.Logic
{
	public class ProductionBatchToggleLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public ProductionBatchToggleLogic(ButtonWidget widget)
		{
			var enabled = false;

			widget.IsHighlighted = () => enabled;
			widget.OnClick = () =>
			{
				// Player widgets are initialized before the complete ingame tree is
				// attached to Ui.Root, so sibling widgets cannot be resolved here in
				// the constructor. Resolve the palette only after the user clicks.
				var palette = Ui.Root.GetOrNull<ProductionPaletteWidget>("PRODUCTION_PALETTE");
				if (palette == null)
					return;

				enabled = !enabled;
				palette.BuildCountMultiplier = enabled ? 5 : 1;
			};
		}
	}
}
