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
using System.Collections.Generic;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public static class IosSettingsVisibilityPolicy
	{
		static readonly HashSet<string> DesktopOnlyContainers = new(StringComparer.Ordinal)
		{
			"VIDEO_MODE_DROPDOWN_CONTAINER",
			"WINDOW_RESOLUTION_CONTAINER",
			"DISPLAY_SELECTION_CONTAINER",
			"GL_PROFILE_DROPDOWN_CONTAINER",
			"MOUSE_CONTROL_CONTAINER",
			"ZOOM_MODIFIER_CONTAINER",
			"MOUSE_CONTROL_DESC_CLASSIC",
			"MOUSE_CONTROL_DESC_MODERN",
			"EDGESCROLL_CHECKBOX_CONTAINER",
			"ALTERNATE_SCROLL_CHECKBOX_CONTAINER",
			"LOCKMOUSE_CHECKBOX_CONTAINER",
			"MOUSE_SCROLL_TYPE_CONTAINER",
			"MOUSE_SETTINGS_SPACER"
		};

		static readonly HashSet<string> IosOnlyContainers = new(StringComparer.Ordinal)
		{
			"TOUCH_JOYSTICK_SIZE_CONTAINER"
		};

		public static bool IsDesktopOnlyContainer(string id) =>
			id != null && DesktopOnlyContainers.Contains(id);

		public static bool IsIosOnlyContainer(string id) =>
			id != null && IosOnlyContainers.Contains(id);

		public static bool ShouldShowContainer(bool isIos, string id)
		{
			if (IsDesktopOnlyContainer(id))
				return !isIos;

			if (IsIosOnlyContainer(id))
				return isIos;

			return true;
		}

		public static bool ShouldCollapseRow(bool isIos, string id) =>
			!ShouldShowContainer(isIos, id);

		public static bool IsHotkeyGroupVisible(bool isIos, IEnumerable<string> types)
		{
			if (!isIos || types == null)
				return true;

			foreach (var type in types)
				if (string.Equals(type, "Editor", StringComparison.OrdinalIgnoreCase))
					return false;

			return true;
		}

		public static bool ShouldShowAudioDeviceSelector(bool isIos, int availableDeviceCount) =>
			!isIos || availableDeviceCount > 1;

		public static Rectangle ColumnBounds(int rowWidth, int columnCount, int columnIndex, int inset, int gap)
		{
			if (columnCount <= 0)
				throw new ArgumentOutOfRangeException(nameof(columnCount));

			if (columnIndex < 0 || columnIndex >= columnCount)
				throw new ArgumentOutOfRangeException(nameof(columnIndex));

			rowWidth = Math.Max(columnCount, rowWidth);
			inset = Math.Clamp(inset, 0, Math.Max(0, (rowWidth - columnCount) / 2));
			var widthInsideInsets = rowWidth - 2 * inset;
			gap = columnCount == 1 ? 0 : Math.Clamp(
				gap, 0, Math.Max(0, (widthInsideInsets - columnCount) / (columnCount - 1)));
			var usableWidth = widthInsideInsets - (columnCount - 1) * gap;
			var baseWidth = usableWidth / columnCount;
			var remainder = usableWidth % columnCount;
			var x = inset + columnIndex * (baseWidth + gap) + Math.Min(columnIndex, remainder);
			var width = baseWidth + (columnIndex < remainder ? 1 : 0);
			return new Rectangle(x, 0, width, 1);
		}
	}
}
