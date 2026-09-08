#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Graphics;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets
{
	/// <summary>
	/// Hold right-mouse and drag to pan the map when nothing is selected.
	/// When units are selected, right-click keeps its normal order behavior.
	/// Skips itself if the player already configured right-button as the scroll button.
	/// </summary>
	public class EmptySelectionMapScrollWidget : Widget
	{
		readonly World world;
		readonly WorldRenderer worldRenderer;

		int2? scrollStart;
		bool isScrolling;

		[ObjectCreator.UseCtor]
		public EmptySelectionMapScrollWidget(World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;

			// Sit above ViewportController for right-drag pan, but do not steal hover —
			// otherwise world unit/building name tooltips never activate.
			IgnoreMouseOver = true;
		}

		static bool RightIsConfiguredScrollButton()
		{
			var gs = Game.Settings.Game;
			return gs.UseClassicMouseStyle ^ gs.UseAlternateScrollButton;
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			// ViewportController already owns right-button scrolling in this configuration.
			if (RightIsConfiguredScrollButton())
				return false;

			var tracking = isScrolling || scrollStart.HasValue;
			if (!tracking && !mi.Button.HasFlag(MouseButton.Right))
				return false;

			var deadzone = Game.Settings.Game.MouseScrollDeadzone;

			if (mi.Event == MouseInputEvent.Down && mi.Button.HasFlag(MouseButton.Right))
			{
				if (world.Selection.Actors.Any())
					return false;

				if (!TakeMouseFocus(mi))
					return false;

				scrollStart = mi.Location;
				return false;
			}

			if (mi.Event == MouseInputEvent.Move &&
				(isScrolling || (scrollStart.HasValue && (scrollStart.Value - mi.Location).Length > deadzone)))
			{
				if (!scrollStart.HasValue && !isScrolling)
					return false;

				isScrolling = true;
				worldRenderer.Viewport.Scroll(Viewport.LastMousePos - mi.Location, false);
				return true;
			}

			if (mi.Event == MouseInputEvent.Up && tracking)
			{
				var wasScrolling = isScrolling;
				isScrolling = false;
				scrollStart = null;
				YieldMouseFocus(mi);
				return wasScrolling;
			}

			return isScrolling;
		}

		public override bool YieldMouseFocus(MouseInput mi)
		{
			scrollStart = null;
			isScrolling = false;
			return base.YieldMouseFocus(mi);
		}

		public override string GetCursor(int2 pos)
		{
			return isScrolling ? "joystick-all" : null;
		}
	}
}
