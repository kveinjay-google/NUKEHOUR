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

using System;
using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosMainMenuQuitPolicyTest
	{
		[Test]
		public void IosHidesQuitAndKeepsTheAdaptiveLayoutEnabled()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "OpenRA.Mods.Common")))
				directory = directory.Parent;

			var root = directory?.FullName ?? throw new InvalidOperationException("Could not locate the engine root.");
			var mainMenu = File.ReadAllText(Path.Combine(
				root, "OpenRA.Mods.Common", "Widgets", "Logic", "MainMenuLogic.cs"));

			StringAssert.Contains("quitButton.Visible = !Platform.IsIOS", mainMenu);
			StringAssert.Contains("if (quitButton.Visible)", mainMenu);
			StringAssert.Contains("mainMenuControls.Length == 4", mainMenu);
		}
	}
}
