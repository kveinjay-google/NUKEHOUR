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

using System;

namespace OpenRA
{
	/// <summary>
	/// Separates the UIKit scene size from OpenRA's effective UI canvas.
	/// SDL renders to the full Retina drawable while the UI canvas is kept
	/// large enough for desktop-authored menus to remain inside the viewport.
	/// </summary>
	public readonly struct IosDisplayLayout
	{
		const int MinimumEffectiveWidth = 1024;
		const int MinimumEffectiveHeight = 720;

		public int Width { get; }
		public int Height { get; }
		public float UiScale { get; }

		IosDisplayLayout(int width, int height, float uiScale)
		{
			Width = width;
			Height = height;
			UiScale = uiScale;
		}

		public int EffectiveWidth => (int)Math.Floor(Width / UiScale);
		public int EffectiveHeight => (int)Math.Floor(Height / UiScale);

		public static IosDisplayLayout ForScene(int width, int height)
		{
			var landscapeWidth = Math.Max(1, Math.Max(width, height));
			var landscapeHeight = Math.Max(1, Math.Min(width, height));
			var scale = Math.Min(1f, Math.Min(
				landscapeWidth / (float)MinimumEffectiveWidth,
				landscapeHeight / (float)MinimumEffectiveHeight));

			return new IosDisplayLayout(landscapeWidth, landscapeHeight, scale);
		}
	}
}
