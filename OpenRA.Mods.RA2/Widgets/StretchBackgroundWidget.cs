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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets
{
	// Full-bleed background that stretches a single chrome sprite to the
	// widget bounds. BackgroundWidget's 9-slice panel path tiles/crops and
	// cannot fill ultrawide menus without seams or empty pad regions.
	public class StretchBackgroundWidget : Widget
	{
		public string Background = "";
		public bool ClickThrough = true;
		Sprite cachedSource;
		Sprite cachedCrop;
		Size cachedTargetSize;

		public StretchBackgroundWidget() { }

		public override void Draw()
		{
			var sprites = ChromeProvider.TryGetPanelImages(Background);
			var sprite = sprites != null && sprites.Length > 4 ? sprites[4] : null;
			if (sprite == null)
				return;

			if (sprite != cachedSource || cachedTargetSize != RenderBounds.Size)
			{
				var crop = CalculateAspectFillCrop(sprite.Bounds, RenderBounds.Size);
				var scale = sprite.Size.X / sprite.Bounds.Width;
				cachedSource = sprite;
				cachedTargetSize = RenderBounds.Size;
				cachedCrop = new Sprite(sprite.Sheet, crop, sprite.ZRamp, sprite.Offset,
					sprite.Channel, sprite.BlendMode, scale);
			}

			WidgetUtils.DrawSprite(cachedCrop, new float2(RenderBounds.X, RenderBounds.Y), RenderBounds.Size);
		}

		public static Rectangle CalculateAspectFillCrop(Rectangle source, Size target)
		{
			if (source.Width <= 0 || source.Height <= 0 || target.Width <= 0 || target.Height <= 0)
				return source;

			var sourceAspect = (double)source.Width / source.Height;
			var targetAspect = (double)target.Width / target.Height;
			if (sourceAspect > targetAspect)
			{
				var width = Math.Clamp((int)Math.Round(source.Height * targetAspect), 1, source.Width);
				return new Rectangle(source.X + (source.Width - width) / 2, source.Y, width, source.Height);
			}

			var height = Math.Clamp((int)Math.Round(source.Width / targetAspect), 1, source.Height);
			return new Rectangle(source.X, source.Y + (source.Height - height) / 2, source.Width, height);
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			return !ClickThrough && EventBounds.Contains(mi.Location);
		}

		protected StretchBackgroundWidget(StretchBackgroundWidget other)
			: base(other)
		{
			Background = other.Background;
			ClickThrough = other.ClickThrough;
		}

		public override Widget Clone() { return new StretchBackgroundWidget(this); }
	}
}
