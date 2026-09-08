#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Linq;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class IosMultiplayerBrowserLayout
	{
		public WidgetBounds Window { get; }
		public WidgetBounds Header { get; }
		public WidgetBounds Table { get; }
		public WidgetBounds TableHeader { get; }
		public WidgetBounds ServerList { get; }
		public WidgetBounds Details { get; }
		public WidgetBounds Footer { get; }
		public WidgetBounds Filters { get; }
		public WidgetBounds Reload { get; }
		public WidgetBounds PlayerCount { get; }
		public WidgetBounds DirectConnect { get; }
		public WidgetBounds CreateButton { get; }
		public WidgetBounds Back { get; }
		public WidgetBounds[] ServerColumns { get; }
		public WidgetBounds ServerTitle { get; }
		public WidgetBounds PasswordIcon { get; }
		public WidgetBounds AuthenticationIcon { get; }

		IosMultiplayerBrowserLayout(int width, int height, IosMenuLayoutPolicy policy)
		{
			width = Math.Max(1, width);
			height = Math.Max(1, height);
			Window = new WidgetBounds(0, 0, width, height);
			Header = new WidgetBounds(0, 0, width, Math.Min(height, policy.HeaderHeight));
			var footerY = Math.Max(Header.Bottom + policy.Gap, height - policy.FooterHeight);
			Footer = new WidgetBounds(0, footerY, width, Math.Max(0, height - footerY));

			var bodyY = Math.Min(Footer.Y, Header.Bottom + policy.Gap);
			var bodyHeight = Math.Max(0, Footer.Y - policy.Gap - bodyY);
			var detailsWidth = Math.Min(width / 3, Math.Max(2 * policy.MinimumTarget, width / 4));
			var tableWidth = Math.Max(0, width - detailsWidth - policy.Gap);
			Table = new WidgetBounds(0, bodyY, tableWidth, bodyHeight);
			Details = new WidgetBounds(Table.Right + policy.Gap, bodyY, detailsWidth, bodyHeight);
			var tableHeaderHeight = Math.Min(policy.MinimumReadableTextHeight, Table.Height);
			TableHeader = new WidgetBounds(0, 0, Table.Width, tableHeaderHeight);
			var listY = Math.Min(Table.Height, TableHeader.Bottom + policy.Gap);
			ServerList = new WidgetBounds(0, listY, Table.Width, Math.Max(0, Table.Height - listY));
			ServerColumns = ServerColumnBounds(ServerList.Width, policy.MinimumTarget);
			var titleParts = ServerTitleAndIconBounds(ServerColumns[0], policy);
			ServerTitle = titleParts[0];
			AuthenticationIcon = titleParts[1];
			PasswordIcon = titleParts[2];

			var footerCells = WeightedCells(Footer.Width, Footer.Height, policy.Gap, 2, 1, 2, 2, 2, 2);
			Filters = footerCells[0];
			Reload = footerCells[1];
			PlayerCount = footerCells[2];
			DirectConnect = footerCells[3];
			CreateButton = footerCells[4];
			Back = footerCells[5];
		}

		public static IosMultiplayerBrowserLayout Create(IosMenuLayoutPolicy policy)
		{
			return new IosMultiplayerBrowserLayout(
				policy.ContentBounds.Width, policy.ContentBounds.Height, policy);
		}

		public static IosMultiplayerBrowserLayout Create(int width, int height, IosMenuLayoutPolicy policy)
		{
			return new IosMultiplayerBrowserLayout(width, height, policy);
		}

		static WidgetBounds[] WeightedCells(int width, int height, int gap, params int[] weights)
		{
			var cells = new WidgetBounds[weights.Length];
			var available = Math.Max(0, width - Math.Max(0, weights.Length - 1) * gap);
			var totalWeight = Math.Max(1, weights.Sum());
			var x = 0;
			var cumulative = 0;
			for (var i = 0; i < weights.Length; i++)
			{
				cumulative += weights[i];
				var right = i == weights.Length - 1 ? width :
					(int)((long)available * cumulative / totalWeight) + i * gap;
				cells[i] = new WidgetBounds(x, 0, Math.Max(0, right - x), height);
				x = right + gap;
			}

			return cells;
		}

		public static WidgetBounds[] ServerTitleAndIconBounds(
			WidgetBounds titleColumn, IosMenuLayoutPolicy policy)
		{
			var maximumIconSize = Math.Max(1,
				(titleColumn.Width - 2 * policy.Gap - 1) / 2);
			var iconSize = Math.Min(policy.MinimumReadableTextHeight,
				Math.Min(titleColumn.Height, maximumIconSize));
			var iconY = titleColumn.Y + Math.Max(0, (titleColumn.Height - iconSize) / 2);
			var password = new WidgetBounds(
				Math.Max(titleColumn.X, titleColumn.Right - iconSize), iconY, iconSize, iconSize);
			var authentication = new WidgetBounds(
				Math.Max(titleColumn.X, password.X - policy.Gap - iconSize), iconY, iconSize, iconSize);
			var titleWidth = Math.Max(1, authentication.X - policy.Gap - titleColumn.X);
			var title = new WidgetBounds(
				titleColumn.X, titleColumn.Y, titleWidth, titleColumn.Height);
			return new[] { title, authentication, password };
		}

		public static WidgetBounds ServerListWithNotice(
			WidgetBounds serverList, int noticeHeight, bool visible)
		{
			if (!visible)
				return serverList;

			var reserved = Math.Min(serverList.Height, Math.Max(0, noticeHeight));
			return new WidgetBounds(
				serverList.X, serverList.Y + reserved, serverList.Width, serverList.Height - reserved);
		}

		public static void ApplyServerRow(
			Widget row, WidgetBounds[] columns, int height, IosMenuLayoutPolicy policy)
		{
			if (row == null || columns == null || columns.Length < 4)
				return;

			var group = row.Children.FirstOrDefault(candidate => candidate.Id == "LABEL");
			if (group != null)
			{
				group.Bounds = new WidgetBounds(
					columns[0].X, 0, columns[^1].Right - columns[0].X, height);
				if (group is LabelWidget groupLabel)
				{
					groupLabel.Font = IosMenuLayoutPolicy.TouchFont(groupLabel.Font, false, true);
					groupLabel.WordWrap = false;
					groupLabel.VAlign = TextVAlign.Middle;
					IosResponsiveText.Configure(groupLabel, false);
				}
			}

			foreach (var (id, index) in new[]
			{
				("NAME", 0), ("TITLE", 0), ("PLAYERS", 1), ("LOCATION", 2), ("STATUS", 3)
			})
			{
				var child = row.Children.FirstOrDefault(candidate => candidate.Id == id);
				if (child == null)
					continue;

				child.Bounds = new WidgetBounds(columns[index].X, 0, columns[index].Width, height);
				if (child is LabelWidget label)
				{
					label.WordWrap = false;
					label.VAlign = TextVAlign.Middle;
					IosResponsiveText.Configure(label, false);
				}
			}

			var titleParts = ServerTitleAndIconBounds(
				new WidgetBounds(columns[0].X, 0, columns[0].Width, height), policy);
			SetDirectBounds(row, "TITLE", titleParts[0]);
			SetDirectBounds(row, "REQUIRES_AUTHENTICATION", titleParts[1]);
			SetDirectBounds(row, "PASSWORD_PROTECTED", titleParts[2]);
		}

		public static WidgetBounds[] ServerColumnBounds(int width, int minimumTarget)
		{
			var weights = new[] { 5, 2, 2, 2 };
			var minimum = Math.Min(minimumTarget, width / weights.Length);
			var extra = Math.Max(0, width - minimum * weights.Length);
			var columns = new WidgetBounds[weights.Length];
			var x = 0;
			var cumulativeWeight = 0;
			for (var i = 0; i < weights.Length; i++)
			{
				cumulativeWeight += weights[i];
				var right = i == weights.Length - 1 ? width :
					(i + 1) * minimum + extra * cumulativeWeight / 11;
				columns[i] = new WidgetBounds(x, 0, Math.Max(0, right - x), minimumTarget);
				x = right;
			}

			return columns;
		}

		static void SetDirectBounds(Widget row, string id, WidgetBounds bounds)
		{
			var child = row.Children.FirstOrDefault(candidate => candidate.Id == id);
			if (child != null)
				child.Bounds = bounds;
		}
	}
}
