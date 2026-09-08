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
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.LoadScreens;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Mods.RA2.LoadScreens
{
	// Shared desktop/iOS full-bleed cinematic splash.
	public sealed class BrandSplashLoadScreen : SheetLoadScreen
	{
		[FluentReference]
		const string Loading = "loadscreen-loading";
		const string BrandTitle = "NUKE HOUR";

		static readonly Color BrandTitleColor = Color.FromArgb(246, 214, 121);
		static readonly Color BrandTitleShadow = Color.FromArgb(192, 0, 0, 0);

		Sprite splash;
		Sprite croppedSplash;
		Sheet lastSheet;
		int lastDensity;
		Size lastResolution;
		Rectangle bounds;
		string[] messages = Array.Empty<string>();

		public override void Init(ModData modData, Dictionary<string, string> info)
		{
			base.Init(modData, info);
			messages = FluentProvider.GetMessage(Loading).Split(',').Select(x => x.Trim()).ToArray();
		}

		public override void DisplayInner(Renderer r, Sheet s, int density)
		{
			if (s != lastSheet || density != lastDensity)
			{
				lastSheet = s;
				lastDensity = density;
				splash = s == null ? null : new Sprite(s,
					new Rectangle(0, 0, s.Size.Width, s.Size.Height),
					TextureChannel.RGBA, 1f / density);
				croppedSplash = null;
			}

			if (r.Resolution != lastResolution)
			{
				lastResolution = r.Resolution;
				bounds = new Rectangle(0, 0, lastResolution.Width, lastResolution.Height);
				croppedSplash = null;
			}

			if (splash != null && croppedSplash == null)
			{
				var crop = StretchBackgroundWidget.CalculateAspectFillCrop(splash.Bounds, bounds.Size);
				var scale = splash.Size.X / splash.Bounds.Width;
				croppedSplash = new Sprite(splash.Sheet, crop, splash.ZRamp, splash.Offset,
					splash.Channel, splash.BlendMode, scale);
			}

			if (croppedSplash != null)
				WidgetUtils.DrawSprite(croppedSplash, new float2(bounds.X, bounds.Y), bounds.Size);

			if (r.Fonts != null)
			{
				var titleFont = r.Fonts["Title"];
				var titleSize = titleFont.Measure(BrandTitle);
				var titlePosition = new float2(
					(r.Resolution.Width - titleSize.X) / 2,
					Math.Max(24, r.Resolution.Height / 12));
				titleFont.DrawTextWithShadow(BrandTitle, titlePosition,
					BrandTitleColor, BrandTitleShadow, 2);

				if (messages.Length > 0)
				{
					var text = messages.Random(Game.CosmeticRandom);
					var textSize = r.Fonts["Bold"].Measure(text);
					r.Fonts["Bold"].DrawTextWithShadow(text,
						new float2(r.Resolution.Width - textSize.X - 20, r.Resolution.Height - textSize.Y - 20),
						Color.White, Color.Black, 1);
				}
			}
		}
	}
}
