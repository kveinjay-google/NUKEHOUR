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
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class AdvancedSettingsLogic : ChromeLogic
	{
		static readonly bool OriginalServerDiscoverNatDevices;

		static AdvancedSettingsLogic()
		{
			var original = Game.Settings;
			OriginalServerDiscoverNatDevices = original.Server.DiscoverNatDevices;
		}

		[ObjectCreator.UseCtor]
		public AdvancedSettingsLogic(Action<string, string, Func<Widget, Func<bool>>, Func<Widget, Action>> registerPanel, string panelID, string label)
		{
			registerPanel(panelID, label, InitPanel, ResetPanel);
		}

		Func<bool> InitPanel(Widget panel)
		{
			var ds = Game.Settings.Debug;
			var ss = Game.Settings.Server;
			var scrollPanel = panel.Get<ScrollPanelWidget>("SETTINGS_SCROLLPANEL");

			// Advanced
			SettingsUtils.BindCheckboxPref(panel, "NAT_DISCOVERY", ss, "DiscoverNatDevices");
			SettingsUtils.BindCheckboxPref(panel, "PERFTEXT_CHECKBOX", ds, "PerfText");

			SettingsUtils.AdjustSettingsScrollPanelLayout(scrollPanel);

			return () => ss.DiscoverNatDevices != OriginalServerDiscoverNatDevices;
		}

		Action ResetPanel(Widget panel)
		{
			var ds = Game.Settings.Debug;
			var ss = Game.Settings.Server;
			var dds = new DebugSettings();
			var dss = new ServerSettings();

			return () =>
			{
				ss.DiscoverNatDevices = dss.DiscoverNatDevices;
				ds.PerfText = dds.PerfText;
				ds.PerfGraph = dds.PerfGraph;
				ds.SyncCheckUnsyncedCode = dds.SyncCheckUnsyncedCode;
				ds.SyncCheckBotModuleCode = dds.SyncCheckBotModuleCode;
				ds.BotDebug = dds.BotDebug;
				ds.LuaDebug = dds.LuaDebug;
				ds.CheckVersion = dds.CheckVersion;
				ds.EnableDebugCommandsInReplays = dds.EnableDebugCommandsInReplays;
				ds.EnableSimulationPerfLogging = dds.EnableSimulationPerfLogging;
			};
		}
	}
}
