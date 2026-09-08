#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Widgets
{
	public enum ProductionSwipeDirection
	{
		None,
		Previous,
		Next
	}

	public enum ProductionSwipeAxis
	{
		None,
		Horizontal,
		Vertical
	}

	public static class ProductionSwipePolicy
	{
		public static ProductionSwipeAxis ResolveAxis(int2 delta, int minimumDistance)
		{
			var horizontal = Math.Abs(delta.X);
			var vertical = Math.Abs(delta.Y);
			if (horizontal < minimumDistance && vertical < minimumDistance)
				return ProductionSwipeAxis.None;

			if (horizontal >= minimumDistance && horizontal >= vertical * 3 / 2)
				return ProductionSwipeAxis.Horizontal;

			if (vertical >= minimumDistance && vertical >= horizontal * 3 / 2)
				return ProductionSwipeAxis.Vertical;

			return ProductionSwipeAxis.None;
		}

		public static ProductionSwipeDirection Resolve(int2 delta, int minimumDistance)
		{
			var horizontal = Math.Abs(delta.X);
			var vertical = Math.Abs(delta.Y);
			if (horizontal < minimumDistance || horizontal < vertical * 3 / 2)
				return ProductionSwipeDirection.None;

			return delta.X < 0 ? ProductionSwipeDirection.Next : ProductionSwipeDirection.Previous;
		}

		public static int ResolveVerticalRows(int deltaY, int pixelsPerRow)
		{
			return pixelsPerRow > 0 ? -deltaY / pixelsPerRow : 0;
		}

		public static bool IsTap(int2 delta, int maximumDistance)
		{
			return Math.Abs(delta.X) <= maximumDistance && Math.Abs(delta.Y) <= maximumDistance;
		}

		public static int FindAdjacentIndex(int currentIndex, IReadOnlyList<bool> enabled,
			ProductionSwipeDirection direction)
		{
			var step = direction == ProductionSwipeDirection.Next ? 1 : -1;
			for (var index = currentIndex + step; index >= 0 && index < enabled.Count; index += step)
				if (enabled[index])
					return index;

			return -1;
		}
	}
}
