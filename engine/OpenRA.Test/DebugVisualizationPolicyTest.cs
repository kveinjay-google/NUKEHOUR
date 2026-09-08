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
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class DebugVisualizationPolicyTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2"))))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
		}

		[TestCase(true, true, false)]
		[TestCase(false, true, false)]
		[TestCase(true, false, true)]
		[TestCase(false, false, false)]
		public void IosNeverShowsDeveloperVisualizations(bool requested, bool isIos, bool expected)
		{
			Assert.That(DebugVisualizationPolicy.IsEnabled(requested, isIos), Is.EqualTo(expected));
		}

		[Test]
		public void Ra2SelectionDecorationsHideSelectionBoxesButKeepBarsAvailable()
		{
			var root = RepositoryRoot();
			var implementation = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Traits", "Render", "SelectionDecorationsBase.cs"));
			StringAssert.Contains("public readonly bool ShowSelectionBox = true;", implementation);
			StringAssert.Contains("if (selected && Info.ShowSelectionBox)", implementation);
			StringAssert.Contains("return RenderSelectionBox(self, worldRenderer, color);", implementation,
				"Explicit target annotations must remain available for support powers and command targeting.");

			foreach (var relativePath in new[]
			{
				"mods/ra2/rules/defaults.yaml",
				"mods/ra2/rules/allied-naval.yaml",
				"mods/ra2/rules/soviet-naval.yaml",
				"engine/mods/ra2/rules/defaults.yaml",
				"engine/mods/ra2/rules/allied-naval.yaml",
				"engine/mods/ra2/rules/soviet-naval.yaml",
			})
			{
				var rules = File.ReadAllText(Path.Combine(root, relativePath));
				var decorations = Regex.Matches(rules,
					@"(?m)^\t(?:Isometric)?SelectionDecorations:\r?\n(?<properties>(?:\t\t.*\r?\n)*)");
				Assert.That(decorations.Count, Is.GreaterThan(0), $"No selection decorations found in {relativePath}.");

				foreach (Match decoration in decorations)
					StringAssert.Contains("\t\tShowSelectionBox: false", decoration.Groups["properties"].Value,
						$"Every RA2 selection decoration in {relativePath} must suppress the white selection box.");
			}
		}
	}
}
