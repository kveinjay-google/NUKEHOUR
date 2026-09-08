#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class IosMenuLayoutPolicy
	{
		const int MinimumTouchTargetPoints = 48;

		public bool Enabled { get; }
		public bool IsPhone { get; }
		public bool Compact => IsPhone;
		public double LogicalPerPoint { get; }
		public int MinimumTarget { get; }
		public int Gap { get; }
		public int HeaderHeight { get; }
		public int FooterHeight { get; }
		public int MinimumReadableTextHeight { get; }
		public int ContentDragThreshold { get; }
		public int OptionColumnCount => IsPhone ? 1 : 3;
		public WidgetBounds ContentBounds { get; }

		IosMenuLayoutPolicy(bool enabled, IosScreenSnapshot snapshot)
		{
			Enabled = enabled;
			LogicalPerPoint = enabled ? snapshot.LogicalPerPoint : 1;
			IsPhone = enabled && Math.Min(snapshot.NativePointSize.Width, snapshot.NativePointSize.Height) < 600;
			MinimumTarget = enabled ? Point(MinimumTouchTargetPoints) : MinimumTouchTargetPoints;
			Gap = enabled ? Point(IsPhone ? 8 : 12) : 12;
			HeaderHeight = enabled ? Math.Max(MinimumTarget, Point(IsPhone ? 48 : 56)) : 56;
			FooterHeight = enabled ? Math.Max(MinimumTarget, Point(IsPhone ? 52 : 60)) : 60;
			MinimumReadableTextHeight = enabled ? Point(24) : 24;
			ContentDragThreshold = enabled ? Point(8) : 8;

			if (!enabled)
			{
				ContentBounds = new WidgetBounds(0, 0, snapshot.EffectiveSize.Width, snapshot.EffectiveSize.Height);
				return;
			}

			var safe = snapshot.SafeBounds;
			var horizontalInset = Math.Min(Point(16), safe.Width / 2);
			var verticalInset = Math.Min(Point(12), safe.Height / 2);
			ContentBounds = new WidgetBounds(
				safe.X + horizontalInset,
				safe.Y + verticalInset,
				Math.Max(0, safe.Width - 2 * horizontalInset),
				Math.Max(0, safe.Height - 2 * verticalInset));
		}

		public static IosMenuLayoutPolicy Create(bool isIos, int width, int height)
		{
			var size = new Size(Math.Max(1, width), Math.Max(1, height));
			return Create(isIos, new IosScreenSnapshot(size, size, default));
		}

		public static IosMenuLayoutPolicy Create(bool isIos, IosScreenSnapshot snapshot)
		{
			return new IosMenuLayoutPolicy(isIos, snapshot);
		}

		public int OptionRowHeight(bool dropdown)
		{
			var itemHeight = dropdown ? OptionLabelHeight + Gap + MinimumTarget : MinimumTarget;
			return OptionColumnCount == 1 ? 3 * itemHeight + 2 * Gap : itemHeight;
		}

		public WidgetBounds OptionControlBounds(int index, int width, bool dropdown)
		{
			var item = Math.Clamp(index, 0, 2);
			var itemHeight = dropdown ? OptionLabelHeight + Gap + MinimumTarget : MinimumTarget;
			if (OptionColumnCount == 1)
			{
				var y = item * (itemHeight + Gap) + (dropdown ? OptionLabelHeight + Gap : 0);
				return new WidgetBounds(0, y, Math.Max(MinimumTarget, width), MinimumTarget);
			}

			var availableWidth = Math.Max(0, width - 2 * Gap);
			var start = (int)((long)item * availableWidth / 3) + item * Gap;
			var end = (int)((long)(item + 1) * availableWidth / 3) + item * Gap;
			return new WidgetBounds(start, dropdown ? OptionLabelHeight + Gap : 0,
				Math.Max(MinimumTarget, end - start), MinimumTarget);
		}

		public WidgetBounds OptionLabelBounds(int index, int width)
		{
			var control = OptionControlBounds(index, width, true);
			return new WidgetBounds(control.X, control.Y - Gap - OptionLabelHeight,
				control.Width, OptionLabelHeight);
		}

		public WidgetBounds EnsureTouchTarget(WidgetBounds bounds)
		{
			if (!Enabled)
				return bounds;

			return new WidgetBounds(bounds.X, bounds.Y,
				Math.Max(bounds.Width, MinimumTarget), Math.Max(bounds.Height, MinimumTarget));
		}

		public static float StepValue(float value, float minimum, float maximum, float step, bool increment)
		{
			return Math.Clamp(value + (increment ? step : -step), minimum, maximum);
		}

		public static string TouchFont(string currentFont, bool title, bool action)
		{
			if (title)
				return "IosTitle";

			return action || currentFont?.Contains("Bold", StringComparison.Ordinal) == true
				? "IosBold" : "IosRegular";
		}

		int OptionLabelHeight => Point(22);

		int Point(int points)
		{
			return Math.Max(1, (int)Math.Ceiling(points * LogicalPerPoint));
		}
	}
}
