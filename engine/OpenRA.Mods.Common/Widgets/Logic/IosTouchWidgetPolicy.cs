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
using System.Globalization;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public readonly struct IosDropDownLayout
	{
		public static IosDropDownLayout Default { get; } = new(3, 0, 0, default, false);
		public static IosDropDownLayout FactionPicker { get; } = new(5, 6, 24, new Size(40, 20), true);

		public int MinimumWidthTargets { get; }
		public int MaximumHeightTargets { get; }
		public int HeaderHeightPoints { get; }
		public Size ItemImagePoints { get; }
		public bool OmitSingletonHeaders { get; }

		IosDropDownLayout(
			int minimumWidthTargets, int maximumHeightTargets, int headerHeightPoints,
			Size itemImagePoints, bool omitSingletonHeaders)
		{
			MinimumWidthTargets = minimumWidthTargets;
			MaximumHeightTargets = maximumHeightTargets;
			HeaderHeightPoints = headerHeightPoints;
			ItemImagePoints = itemImagePoints;
			OmitSingletonHeaders = omitSingletonHeaders;
		}
	}

	public enum TouchStepperSegment
	{
		Decrement,
		Value,
		Increment
	}

	public readonly struct TouchScrollGeometry
	{
		public readonly int ButtonSize;
		public readonly int TrackHeight;
		public readonly int ThumbHeight;

		public TouchScrollGeometry(int buttonSize, int trackHeight, int thumbHeight)
		{
			ButtonSize = buttonSize;
			TrackHeight = trackHeight;
			ThumbHeight = thumbHeight;
		}
	}

	public static class IosTouchWidgetPolicy
	{
		const int MinimumTouchTargetPoints = 48;

		public static TouchStepperSegment StepperSegmentAt(int x, int width)
		{
			if (width <= 0)
				return TouchStepperSegment.Value;

			var segment = (int)Math.Clamp((long)x * 3 / width, 0, 2);
			return (TouchStepperSegment)segment;
		}

		public static Rectangle StepperSegmentBounds(Rectangle bounds, TouchStepperSegment segment)
		{
			var index = (int)segment;
			var start = (index * bounds.Width + 2) / 3;
			var end = ((index + 1) * bounds.Width + 2) / 3;
			return new Rectangle(bounds.X + start, bounds.Y, Math.Max(0, end - start), bounds.Height);
		}

		public static float StepValue(
			float value, float minimum, float maximum, float step, TouchStepperSegment segment)
		{
			if (segment == TouchStepperSegment.Value)
				return value;

			var low = Math.Min(minimum, maximum);
			var high = Math.Max(minimum, maximum);
			var delta = Math.Abs(step) * (segment == TouchStepperSegment.Increment ? 1 : -1);
			return Math.Clamp(value + delta, low, high);
		}

		public static float StepForRange(float minimum, float maximum, int ticks)
		{
			var range = Math.Abs(maximum - minimum);
			return ticks > 1 ? range / (ticks - 1) : range / 10f;
		}

		public static string FormatStepperValue(float value, float minimum, float maximum)
		{
			if (minimum == 0f && maximum == 1f)
				return (value * 100f).ToString("0.##", CultureInfo.InvariantCulture) + "%";

			if (IsWhole(minimum) && IsWhole(maximum))
				return Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

			return value.ToString("0.##", CultureInfo.InvariantCulture);
		}

		public static bool ExceedsDragThreshold(int startY, int currentY, int threshold)
		{
			return Math.Abs((long)currentY - startY) >= Math.Max(0, threshold);
		}

		public static float ClampScrollOffset(float offset, int panelHeight, int contentHeight)
		{
			var minimum = Math.Min(0, panelHeight - contentHeight);
			return Math.Clamp(offset, minimum, 0);
		}

		public static int ContentDragThreshold(double logicalPerPoint)
		{
			return LogicalPoints(logicalPerPoint, 8);
		}

		public static TouchScrollGeometry ScrollGeometry(
			int panelWidth, int panelHeight, int requestedButtonSize, int contentHeight, int minimumThumbSize)
		{
			var width = Math.Max(0, panelWidth);
			var height = Math.Max(0, panelHeight);
			var buttonSize = Math.Min(Math.Max(0, requestedButtonSize), Math.Min(width, height / 2));
			var trackHeight = Math.Max(0, height - 2 * buttonSize);
			var thumbHeight = 0;
			if (contentHeight > height && trackHeight > 0)
			{
				var proportionalHeight = (int)((long)trackHeight * height / Math.Max(1, contentHeight));
				thumbHeight = Math.Min(trackHeight, Math.Max(Math.Max(0, minimumThumbSize), proportionalHeight));
			}

			return new TouchScrollGeometry(buttonSize, trackHeight, thumbHeight);
		}

		public static int2 CenteredDecorationOrigin(
			Rectangle bounds, Size decorationSize, int pressedDepth)
		{
			var depth = Math.Max(0, pressedDepth);
			return new int2(
				bounds.X + Math.Max(0, bounds.Width - decorationSize.Width) / 2 + depth,
				bounds.Y + Math.Max(0, bounds.Height - decorationSize.Height) / 2 + depth);
		}

		public static int EnsureMinimumTouchHeight(int height, IosScreenSnapshot snapshot)
		{
			return Math.Max(height, LogicalPoints(snapshot.LogicalPerPoint, MinimumTouchTargetPoints));
		}

		public static int MaximumPopupHeight(int requestedHeight, IosScreenSnapshot snapshot)
		{
			return Math.Min(Math.Max(0, requestedHeight), snapshot.SafeBounds.Height);
		}

		public static int DropDownPanelWidth(int requestedWidth, int minimumTouchSize, int safeWidth)
		{
			return DropDownPanelWidth(requestedWidth, minimumTouchSize, safeWidth, 3);
		}

		public static int DropDownPanelWidth(
			int requestedWidth, int minimumTouchSize, int safeWidth, int minimumWidthTargets)
		{
			var availableWidth = Math.Max(1, safeWidth);
			var minimumUsableWidth = Math.Min(availableWidth,
				Math.Max(1, minimumTouchSize) * Math.Max(1, minimumWidthTargets));
			return Math.Min(availableWidth, Math.Max(Math.Max(1, requestedWidth), minimumUsableWidth));
		}

		public static int DropDownMaximumHeight(
			int requestedHeight, int minimumTouchSize, int safeHeight, int maximumHeightTargets)
		{
			if (maximumHeightTargets <= 0)
				return Math.Min(Math.Max(0, requestedHeight), Math.Max(0, safeHeight));

			return Math.Min(Math.Max(0, safeHeight),
				Math.Max(1, minimumTouchSize) * maximumHeightTargets);
		}

		public static bool ShowDropDownGroupHeader(
			int optionCount, bool isIos, IosDropDownLayout layout)
		{
			return !isIos || !layout.OmitSingletonHeaders || optionCount != 1;
		}

		public static int DropDownPopupHeight(int contentHeight, int maximumHeight, int minimumTouchSize)
		{
			var availableHeight = Math.Max(1, maximumHeight);
			var minimumScrollableHeight = Math.Min(availableHeight, Math.Max(1, minimumTouchSize) * 2);
			return Math.Min(availableHeight, Math.Max(Math.Max(1, contentHeight), minimumScrollableHeight));
		}

		public static Rectangle DropDownContentBounds(int panelWidth, int scrollbarWidth, int inset)
		{
			// ChildOrigin already shifts children past a left-side scrollbar, so this
			// must stay in child-local coordinates and reserve the width only once.
			var width = Math.Max(0, panelWidth);
			var reservedScrollbarWidth = Math.Min(width, Math.Max(0, scrollbarWidth));
			var contentRight = width - reservedScrollbarWidth;
			var horizontalInset = Math.Min(
				Math.Max(0, inset), contentRight / 2);

			return new Rectangle(
				horizontalInset,
				0,
				Math.Max(0, contentRight - 2 * horizontalInset),
				0);
		}

		public static Rectangle PlacePopup(
			Rectangle anchor, int preferredX, int desiredWidth, int desiredHeight, Rectangle safeBounds)
		{
			var width = Math.Min(Math.Max(1, desiredWidth), Math.Max(1, safeBounds.Width));
			var height = Math.Min(Math.Max(1, desiredHeight), Math.Max(1, safeBounds.Height));
			var x = Math.Clamp(preferredX, safeBounds.Left, safeBounds.Right - width);
			var belowY = anchor.Bottom;
			var y = belowY + height <= safeBounds.Bottom ? belowY : anchor.Top - height;
			y = Math.Clamp(y, safeBounds.Top, safeBounds.Bottom - height);
			return new Rectangle(x, y, width, height);
		}

		public static Rectangle RelativeToRoot(Rectangle bounds, int2 rootRenderOrigin)
		{
			return new Rectangle(
				bounds.X - rootRenderOrigin.X,
				bounds.Y - rootRenderOrigin.Y,
				bounds.Width,
				bounds.Height);
		}

		static int LogicalPoints(double logicalPerPoint, int points)
		{
			return Math.Max(1, (int)Math.Ceiling(points * logicalPerPoint));
		}

		static bool IsWhole(float value)
		{
			return Math.Abs(value - MathF.Round(value)) < 0.0001f;
		}
	}
}
