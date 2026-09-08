#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets.Logic
{
	// Picks one of several background image collections at random each time
	// the menu is shown, so successive game launches get different artwork.
	public class RandomBackgroundLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public RandomBackgroundLogic(StretchBackgroundWidget widget, Dictionary<string, MiniYaml> logicArgs)
		{
			var images = logicArgs["Images"].Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
			if (images.Length > 0)
				widget.Background = images[Game.CosmeticRandom.Next(images.Length)];
		}
	}
}
