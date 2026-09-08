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
using System.Linq;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class IosMultiplayerDetailsLayout
	{
		public WidgetBounds Window { get; }
		public WidgetBounds MapBackground { get; }
		public WidgetBounds MapPreview { get; }
		public WidgetBounds TextColumn { get; }
		public WidgetBounds[] DetailRows { get; }
		public WidgetBounds ClientList { get; }
		public WidgetBounds Join { get; }

		IosMultiplayerDetailsLayout(
			int width, int height, IosMenuLayoutPolicy policy, bool hasClients, bool hasJoin)
		{
			width = Math.Max(1, width);
			height = Math.Max(1, height);
			Window = new WidgetBounds(0, 0, width, height);
			var rowHeight = Math.Min(policy.MinimumReadableTextHeight, height);
			var joinHeight = hasJoin ? Math.Min(policy.MinimumTarget, height) : 0;
			Join = new WidgetBounds(0, Math.Max(0, height - joinHeight), width, joinHeight);
			DetailRows = new WidgetBounds[5];

			if (policy.IsPhone)
			{
				var minimumTextWidth = 2 * policy.MinimumTarget;
				var availableMapWidth = Math.Max(1, width - policy.Gap - minimumTextWidth);
				var mapSize = Math.Min(Join.Y, Math.Max(policy.MinimumTarget, availableMapWidth));
				mapSize = Math.Min(mapSize, Math.Max(1, width - policy.Gap - 1));
				MapBackground = new WidgetBounds(0, 0, mapSize, mapSize);
				MapPreview = Inset(MapBackground);
				var textX = Math.Min(width, MapBackground.Right + policy.Gap);
				var textWidth = Math.Max(1, width - textX);
				TextColumn = new WidgetBounds(textX, 0, textWidth,
					Math.Min(height, 5 * rowHeight));
				for (var i = 0; i < DetailRows.Length; i++)
					DetailRows[i] = hasClients && i == DetailRows.Length - 1
						? new WidgetBounds(textX, i * rowHeight, textWidth, 0)
						: new WidgetBounds(textX, i * rowHeight, textWidth, rowHeight);
			}
			else
			{
				var reservedBelowMap = policy.Gap + 5 * rowHeight + policy.Gap + rowHeight;
				var mapSize = Math.Min(width,
					Math.Max(policy.MinimumTarget, Join.Y - reservedBelowMap));
				mapSize = Math.Min(mapSize, Math.Max(1, Join.Y - policy.Gap - rowHeight));
				MapBackground = new WidgetBounds(0, 0, mapSize, mapSize);
				MapPreview = Inset(MapBackground);
				var textY = Math.Min(Join.Y, MapBackground.Bottom + policy.Gap);
				TextColumn = new WidgetBounds(0, textY, width,
					Math.Min(5 * rowHeight, Math.Max(1, Join.Y - textY)));
				for (var i = 0; i < DetailRows.Length; i++)
					DetailRows[i] = hasClients && i == DetailRows.Length - 1
						? new WidgetBounds(0, textY + i * rowHeight, width, 0)
						: new WidgetBounds(0, textY + i * rowHeight, width, rowHeight);
			}

			var visibleRowCount = hasClients ? 4 : 5;
			var rowsBottom = DetailRows[visibleRowCount - 1].Bottom;
			var listY = Math.Min(Join.Y,
				Math.Max(MapBackground.Bottom, rowsBottom) + policy.Gap);
			ClientList = new WidgetBounds(0, listY, width, Math.Max(1, Join.Y - listY));
		}

		public static IosMultiplayerDetailsLayout Create(
			int width, int height, IosMenuLayoutPolicy policy, bool hasClients)
		{
			return new IosMultiplayerDetailsLayout(width, height, policy, hasClients, true);
		}

		public static IosMultiplayerDetailsLayout Create(
			int width, int height, IosMenuLayoutPolicy policy, bool hasClients, bool hasJoin)
		{
			return new IosMultiplayerDetailsLayout(width, height, policy, hasClients, hasJoin);
		}

		public static void Apply(Widget selected, IosMenuLayoutPolicy policy, bool hasClients)
		{
			if (selected == null || !policy.Enabled ||
				selected.Bounds.Width <= 0 || selected.Bounds.Height <= 0)
				return;

			var join = Direct(selected, "JOIN_BUTTON");
			var clientContainer = Direct(selected, "CLIENT_LIST_CONTAINER");
			var clientListWidget = clientContainer?.Children.FirstOrDefault();
			var effectiveHasClients = hasClients && clientContainer != null && clientListWidget != null;
			var layout = Create(
				selected.Bounds.Width, selected.Bounds.Height, policy, effectiveHasClients, join != null);
			var background = Direct(selected, "MAP_BG");
			if (background != null)
			{
				background.Bounds = layout.MapBackground;
				var preview = Direct(background, "SELECTED_MAP_PREVIEW");
				if (preview != null)
					preview.Bounds = layout.MapPreview;
			}

			var ids = new[]
			{
				"SELECTED_MAP", "SELECTED_IP", "SELECTED_STATUS", "SELECTED_MOD_VERSION", "SELECTED_PLAYERS"
			};
			for (var i = 0; i < ids.Length; i++)
			{
				var label = Direct(selected, ids[i]);
				if (label == null)
					continue;

				label.Bounds = layout.DetailRows[i];
				if (label is LabelWidget text)
				{
					text.WordWrap = false;
					text.VAlign = TextVAlign.Middle;
					IosResponsiveText.Configure(text, false);
				}
			}

			if (clientContainer != null)
			{
				clientContainer.Bounds = layout.ClientList;
				if (clientListWidget != null)
					clientListWidget.Bounds = new WidgetBounds(0, 0,
						clientContainer.Bounds.Width, clientContainer.Bounds.Height);

				if (clientListWidget is ScrollPanelWidget clientList)
				{
					var contentWidth = Math.Max(1,
						clientList.Bounds.Width - clientList.ScrollbarWidth);
					foreach (var row in clientList.Children)
					{
						row.Bounds = new WidgetBounds(0, row.Bounds.Y, contentWidth, row.Bounds.Height);
						ApplyClientRow(row, policy, row.Id == "HEADER");
					}

					clientList.Layout?.AdjustChildren();
				}
			}

			if (join != null)
				join.Bounds = layout.Join;
		}

		public static void ApplyClientRow(Widget row, IosMenuLayoutPolicy policy, bool isHeader)
		{
			if (row == null || !policy.Enabled)
				return;

			var height = isHeader ? policy.MinimumReadableTextHeight : policy.MinimumTarget;
			row.Bounds = new WidgetBounds(0, row.Bounds.Y, Math.Max(1, row.Bounds.Width), height);
			var local = new WidgetBounds(0, 0, row.Bounds.Width, height);
			var label = Direct(row, "LABEL");
			if (isHeader)
				ApplyLabel(label, local, true);
			else
			{
				var flag = Direct(row, "FLAG");
				var flagWidth = Math.Min(height, Math.Max(1, row.Bounds.Width / 5));
				if (flag != null)
					flag.Bounds = new WidgetBounds(0, 0, flagWidth, height);

				var nameX = Math.Min(row.Bounds.Width, flagWidth + policy.Gap);
				ApplyLabel(label, new WidgetBounds(
					nameX, 0, Math.Max(1, row.Bounds.Width - nameX), height), false);
				ApplyLabel(Direct(row, "NOFLAG_LABEL"), local, false);
			}
		}

		static void ApplyLabel(Widget widget, WidgetBounds bounds, bool bold)
		{
			if (widget == null)
				return;

			widget.Bounds = bounds;
			if (widget is LabelWidget label)
			{
				label.Font = IosMenuLayoutPolicy.TouchFont(label.Font, false, bold);
				label.WordWrap = false;
				label.VAlign = TextVAlign.Middle;
				IosResponsiveText.Configure(label, false);
			}
		}

		static Widget Direct(Widget parent, string id)
		{
			return parent?.Children.FirstOrDefault(child => child.Id == id);
		}

		static WidgetBounds Inset(WidgetBounds bounds)
		{
			return new WidgetBounds(bounds.X + 1, bounds.Y + 1,
				Math.Max(1, bounds.Width - 2), Math.Max(1, bounds.Height - 2));
		}
	}
}
