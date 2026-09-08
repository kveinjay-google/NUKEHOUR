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
	public sealed class IosLobbyLayout
	{
		public int Gap { get; }
		public WidgetBounds Header { get; }
		public WidgetBounds Tabs { get; }
		public WidgetBounds Main { get; }
		public WidgetBounds Servers { get; }
		public WidgetBounds Players { get; }
		public WidgetBounds PlayerHeader { get; }
		public WidgetBounds PlayerList { get; }
		public WidgetBounds Map { get; }
		public WidgetBounds ChangeMap { get; }
		public WidgetBounds Position { get; }
		public WidgetBounds Chat { get; }
		public WidgetBounds ChatDisplay { get; }
		public WidgetBounds ChatInput { get; }
		public WidgetBounds Footer { get; }
		public WidgetBounds Start { get; }
		public WidgetBounds Disconnect { get; }
		public WidgetBounds PlayerRow { get; }
		public WidgetBounds PlayerName { get; }
		public WidgetBounds PlayerColor { get; }
		public WidgetBounds PlayerFaction { get; }
		public WidgetBounds PlayerTeam { get; }
		public WidgetBounds PlayerHandicap { get; }
		public WidgetBounds PlayerSpawn { get; }
		public WidgetBounds PlayerReady { get; }

		IosLobbyLayout(int width, int height, IosMenuLayoutPolicy policy, bool skirmishMode)
		{
			width = Math.Max(1, width);
			height = Math.Max(1, height);
			Gap = policy.Gap;

			var headerHeight = Math.Min(height, policy.HeaderHeight);
			Header = new WidgetBounds(0, 0, width, headerHeight);
			var tabsY = Math.Min(height, Header.Bottom + Gap);
			var tabsHeight = Math.Min(policy.MinimumTarget, Math.Max(0, height - tabsY));
			Tabs = new WidgetBounds(0, tabsY, width, tabsHeight);

			var footerHeight = Math.Min(policy.FooterHeight, height);
			var footerY = Math.Max(Tabs.Bottom + Gap, height - footerHeight);
			Footer = new WidgetBounds(0, footerY, width, Math.Max(0, height - footerY));

			var mainY = Math.Min(Footer.Y, Tabs.Bottom + Gap);
			var mainBottom = Math.Max(mainY, Footer.Y - Gap);
			var mainHeight = mainBottom - mainY;
			Main = new WidgetBounds(0, mainY, width, mainHeight);
			Servers = Main;
			var minimumChatHeight = policy.MinimumTarget + Gap + policy.MinimumReadableTextHeight;
			var preferredUtilityHeight = policy.IsPhone
				? Math.Max(minimumChatHeight, mainHeight / 3)
				: 3 * policy.MinimumTarget;
			var utilityHeight = Math.Min(mainHeight,
				Math.Max(minimumChatHeight, preferredUtilityHeight));
			var upperHeight = Math.Max(0, mainHeight - utilityHeight - (utilityHeight > 0 ? Gap : 0));

			var reservedScrollbar = policy.IsPhone ? 0 : policy.MinimumTarget;
			var minimumPlayerWidth = (skirmishMode ? 8 : 10) * policy.MinimumTarget;
			var playersWidth = skirmishMode ?
				2 * Math.Max(0, width - Gap) / 3 :
				Math.Max(0, width - Gap - Math.Min(
					Math.Max(policy.MinimumTarget, width - Gap - minimumPlayerWidth - Gap),
					Math.Max(2 * policy.MinimumTarget, width / 4)));
			var mapWidth = Math.Max(0, width - Gap - playersWidth);
			Players = new WidgetBounds(0, mainY, playersWidth, upperHeight);
			var playerHeaderHeight = Math.Min(
				policy.IsPhone ? policy.MinimumReadableTextHeight : policy.MinimumTarget, Players.Height);
			PlayerHeader = new WidgetBounds(0, 0, Players.Width, playerHeaderHeight);
			var playerListY = Math.Min(Players.Height, PlayerHeader.Bottom + Gap);
			PlayerList = new WidgetBounds(0, playerListY, Players.Width,
				Math.Max(0, Players.Height - playerListY));

			var utilityY = upperHeight > 0 ? Players.Bottom + Gap : mainY;
			var utilityWidth = playersWidth;
			var positionWidth = Math.Min(utilityWidth, Math.Max(2 * policy.MinimumTarget, width / 5));
			Chat = new WidgetBounds(0, utilityY, utilityWidth, utilityHeight);
			var chatInputHeight = Math.Min(policy.MinimumTarget, Chat.Height);
			var chatInputGap = policy.IsPhone ? Gap : 0;
			var chatDisplayHeight = Math.Max(0, Chat.Height - chatInputHeight - chatInputGap);
			var chatInputY = chatDisplayHeight + chatInputGap;
			ChatDisplay = new WidgetBounds(0, 0, Chat.Width, chatDisplayHeight);
			Position = new WidgetBounds(0, utilityY + chatInputY, positionWidth, chatInputHeight);
			var chatInputX = Math.Min(Chat.Width, Position.Right + Gap);
			ChatInput = new WidgetBounds(
				chatInputX, chatInputY, Math.Max(0, Chat.Width - chatInputX), chatInputHeight);

			if (policy.IsPhone)
			{
				Map = new WidgetBounds(Players.Right + Gap, Main.Y, mapWidth, Main.Height);
				var footerAvailable = Math.Max(0, Footer.Width - 2 * Gap);
				var firstRight = footerAvailable / 3;
				var secondRight = 2 * footerAvailable / 3;
				Start = new WidgetBounds(Footer.X, Footer.Y, firstRight, Footer.Height);
				ChangeMap = new WidgetBounds(Start.Right + Gap, Footer.Y,
					Math.Max(0, secondRight - firstRight), Footer.Height);
				Disconnect = new WidgetBounds(ChangeMap.Right + Gap, Footer.Y,
					Math.Max(0, Footer.Right - ChangeMap.Right - Gap), Footer.Height);
			}
			else
			{
				var changeHeight = Math.Min(policy.MinimumTarget, Main.Height);
				var mapHeight = Math.Max(0, Main.Height - changeHeight - (changeHeight > 0 ? Gap : 0));
				Map = new WidgetBounds(Players.Right + Gap, mainY, mapWidth, mapHeight);
				ChangeMap = new WidgetBounds(
					Map.X, Map.Bottom + (changeHeight > 0 ? Gap : 0), mapWidth, changeHeight);
				var footerButtonWidth = Math.Max(0, (Footer.Width - Gap) / 2);
				Start = new WidgetBounds(Footer.X, Footer.Y, footerButtonWidth, Footer.Height);
				Disconnect = new WidgetBounds(Start.Right + Gap, Footer.Y,
					Math.Max(0, Footer.Right - Start.Right - Gap), Footer.Height);
			}

			var playerRowWidth = Math.Max(0, Players.Width - reservedScrollbar);
			PlayerRow = new WidgetBounds(0, 0, playerRowWidth, Math.Min(policy.MinimumTarget, Players.Height));
			var columns = PlayerColumns(playerRowWidth, policy.MinimumTarget, skirmishMode);
			PlayerName = columns[0];
			PlayerColor = columns[1];
			PlayerFaction = columns[2];
			PlayerTeam = columns[3];
			PlayerHandicap = skirmishMode ? new WidgetBounds(PlayerTeam.Right, 0, 0, policy.MinimumTarget) : columns[4];
			PlayerSpawn = skirmishMode ? columns[4] : columns[5];
			PlayerReady = skirmishMode ? new WidgetBounds(PlayerSpawn.Right, 0, 0, policy.MinimumTarget) : columns[6];
		}

		public static IosLobbyLayout Create(int width, int height, bool compact)
		{
			return new IosLobbyLayout(width, height, IosMenuLayoutPolicy.Create(true, width, height), false);
		}

		public static IosLobbyLayout Create(int width, int height, IosMenuLayoutPolicy policy)
		{
			return new IosLobbyLayout(width, height, policy, false);
		}

		public static IosLobbyLayout Create(int width, int height, IosMenuLayoutPolicy policy, bool skirmishMode)
		{
			return new IosLobbyLayout(width, height, policy, skirmishMode);
		}

		static WidgetBounds[] PlayerColumns(int width, int minimumTarget, bool skirmishMode)
		{
			// Skirmish headings need room for 名称 / 颜色 / 阵营 / 队伍 / 出生点.
			// Names commonly contain four or more characters; spawn only needs its three-character title and a letter.
			var minimumUnits = skirmishMode ? new[] { 4, 2, 2, 2, 2 } : new[] { 2, 1, 3, 1, 1, 1, 1 };
			var weights = skirmishMode ? new[] { 4, 2, 2, 2, 2 } : new[] { 3, 1, 2, 1, 1, 1, 1 };
			var totalMinimumUnits = minimumUnits.Sum();
			var required = totalMinimumUnits * minimumTarget;
			var extra = Math.Max(0, width - required);
			var totalWeight = weights.Sum();
			var columns = new WidgetBounds[weights.Length];
			var x = 0;
			var distributedMinimumUnits = 0;
			var distributedWeight = 0;
			for (var i = 0; i < weights.Length; i++)
			{
				distributedMinimumUnits += minimumUnits[i];
				distributedWeight += weights[i];
				var right = i == weights.Length - 1 ? width : Math.Min(width,
					required > width ? (int)((long)distributedMinimumUnits * width / totalMinimumUnits) :
					distributedMinimumUnits * minimumTarget +
					(int)((long)extra * distributedWeight / totalWeight));
				columns[i] = new WidgetBounds(x, 0, Math.Max(0, right - x), minimumTarget);
				x = right;
			}

			return columns;
		}
	}
}
