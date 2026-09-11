#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public interface ITouchFactionSkinTarget
	{
		void ApplySkin(string skin);
	}

	public static class TouchFactionSkin
	{
		const string Default = "allies";
		static string active = Default;

		public static string Active => active;

		public static string Resolve(string internalName)
		{
			if (string.IsNullOrEmpty(internalName))
				return Default;

			if (ChromeMetrics.TryGet("TouchFactionSuffix-" + internalName, out string touch))
				return Normalize(touch);
			if (ChromeMetrics.TryGet("FactionSuffix-" + internalName, out string fallback))
				return Normalize(fallback);

			return Default;
		}

		public static string ResolveForPlayer(string internalName, bool spectating) =>
			spectating ? Default : Resolve(internalName);

		public static string ActivateForPlayer(string internalName, bool spectating)
		{
			SetActive(ResolveForPlayer(internalName, spectating));
			return active;
		}

		public static void SetActive(string skin)
		{
			active = Normalize(skin);
		}

		public static void ResetActive()
		{
			active = Default;
		}

		public static string JoystickCollection(string skin) =>
			"ios-touch-joystick-" + Normalize(skin);

		public static string ActionCollection(string skin) =>
			"ios-touch-actions-" + Normalize(skin);

		public static string QuickbarPanelCollection(string skin, bool expanded) =>
			(expanded ? "ios-touch-quickbar-panel-" : "ios-touch-quickbar-panel-compact-") + Normalize(skin);

		public static string QuickbarButtonCollection(string skin) =>
			"ios-touch-quickbar-button-" + Normalize(skin);

		public static string QuickbarToggleCollection(string skin) =>
			"ios-touch-quickbar-toggle-" + Normalize(skin);

		public static string CommandGlyphCollection(string skin) =>
			"ios-commandbar-glyphs-" + Normalize(skin);

		static string Normalize(string suffix)
		{
			return suffix == "allies" || suffix == "soviets" || suffix == "yuri" ? suffix : Default;
		}
	}
}
