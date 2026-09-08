#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using System.IO;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class ProductionAlertBlinkPolicyTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "engine", "OpenRA.Mods.Common")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		[TestCase(false, 0, false)]
		[TestCase(false, 400, false)]
		[TestCase(true, 0, true)]
		[TestCase(true, 399, true)]
		[TestCase(true, 400, false)]
		[TestCase(true, 799, false)]
		[TestCase(true, 800, true)]
		public void CompletedProductionAlternatesCategoryAlertIcon(
			bool hasCompletedProduction, long runTimeMilliseconds, bool expected)
		{
			Assert.That(ProductionAlertBlinkPolicy.ShowAlertFrame(
				hasCompletedProduction, runTimeMilliseconds), Is.EqualTo(expected));
		}

		[TestCase(false, false, 0, false)]
		[TestCase(true, false, 400, true)]
		[TestCase(false, true, 0, true)]
		[TestCase(false, true, 400, false)]
		[TestCase(true, true, 0, true)]
		[TestCase(true, true, 400, false)]
		public void CompletedProductionFlashesTheVisibleCategoryButton(
			bool selected, bool hasCompletedProduction, long runTimeMilliseconds, bool expected)
		{
			Assert.That(ProductionAlertBlinkPolicy.ShouldHighlightCategory(
				selected, hasCompletedProduction, runTimeMilliseconds), Is.EqualTo(expected));
		}

		[Test]
		public void ProductionCategoryIconUsesReadyBlinkPolicy()
		{
			var classicSource = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Widgets",
				"Logic", "Ingame", "ClassicProductionLogic.cs"));
			StringAssert.Contains("button.IsHighlighted = () => ProductionAlertBlinkPolicy.ShouldHighlightCategory", classicSource);
			StringAssert.Contains("Game.RunTime", classicSource);

			var tabsSource = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Widgets",
				"Logic", "Ingame", "ProductionTabsLogic.cs"));
			StringAssert.Contains("button.IsHighlighted = () => ProductionAlertBlinkPolicy.ShouldHighlightCategory", tabsSource);
			StringAssert.Contains("Game.RunTime", tabsSource);
		}
	}
}
