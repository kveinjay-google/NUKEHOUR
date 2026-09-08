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

using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosFloatingControlsToggleTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		[Test]
		public void TopButtonTogglesAllIosFloatingControls()
		{
			var root = RepositoryRoot();
			var playerLayout = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame-player.yaml"));
			var ingameLayout = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame.yaml"));
			var preferences = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "IosFloatingControlsPreferences.cs"));
			var joystick = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "VirtualViewportJoystickWidget.cs"));
			var viewportActions = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Ingame", "IosViewportActionsLogic.cs"));
			var commandBar = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Widgets", "CustomCommandBarWidget.cs"));
			var toggle = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Widgets", "Logic", "FloatingControlsToggleButtonLogic.cs"));

			var fps = playerLayout.IndexOf("Button@FPS_BUTTON:", System.StringComparison.Ordinal);
			var controls = playerLayout.IndexOf("Button@FLOATING_CONTROLS_BUTTON:", System.StringComparison.Ordinal);
			var options = playerLayout.IndexOf("MenuButton@OPTIONS_BUTTON:", System.StringComparison.Ordinal);
			Assert.That(fps, Is.GreaterThanOrEqualTo(0));
			Assert.That(controls, Is.GreaterThan(fps));
			Assert.That(options, Is.GreaterThan(controls));
			var controlsBlock = playerLayout.Substring(controls, options - controls);
			StringAssert.Contains("Background: debug-button", controlsBlock,
				"The touch-control toggle should use the native middle top-button artwork.");
			StringAssert.DoesNotContain("Label@LABEL:", controlsBlock,
				"Text overlays obscure the native faction-specific icon and its interaction states.");
			StringAssert.DoesNotContain("Text: HUD", controlsBlock);
			StringAssert.Contains("MenuButton@DEBUG_BUTTON:", playerLayout,
				"The iOS-only control toggle must not remove the desktop developer menu.");

			StringAssert.Contains("static bool visible = true;", preferences);
			StringAssert.Contains("File.WriteAllText(FilePath, visible.ToString())", preferences);
			StringAssert.Contains("IosFloatingControlsPreferences.Visible", joystick);
			StringAssert.Contains("IosFloatingControlsPreferences.Visible", viewportActions);
			StringAssert.Contains("IosFloatingControlsPreferences.Visible", commandBar);
			StringAssert.Contains("IosFloatingControlsPreferences.Visible = !IosFloatingControlsPreferences.Visible", toggle);
			StringAssert.Contains("VirtualViewportJoystick@IOS_VIEWPORT_JOYSTICK:", ingameLayout);
			StringAssert.Contains("Container@IOS_VIEWPORT_ACTIONS:", ingameLayout);
		}
	}
}
