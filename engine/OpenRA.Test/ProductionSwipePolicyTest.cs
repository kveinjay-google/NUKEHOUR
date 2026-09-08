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
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class ProductionSwipePolicyTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "engine", "OpenRA.Mods.Common")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		[TestCase(-60, 4, ProductionSwipeDirection.Next)]
		[TestCase(60, -4, ProductionSwipeDirection.Previous)]
		[TestCase(-35, 0, ProductionSwipeDirection.None)]
		[TestCase(-80, 70, ProductionSwipeDirection.None)]
		[TestCase(8, 90, ProductionSwipeDirection.None)]
		public void OnlyIntentionalHorizontalSwipesChangeProductionType(
			int deltaX, int deltaY, ProductionSwipeDirection expected)
		{
			Assert.That(ProductionSwipePolicy.Resolve(new int2(deltaX, deltaY), 36), Is.EqualTo(expected));
		}

		[TestCase(5, -6, true)]
		[TestCase(13, 0, false)]
		[TestCase(0, 13, false)]
		public void TapSlopPreventsDragsFromBuildingUnits(int deltaX, int deltaY, bool expected)
		{
			Assert.That(ProductionSwipePolicy.IsTap(new int2(deltaX, deltaY), 12), Is.EqualTo(expected));
		}

		[TestCase(4, -60, ProductionSwipeAxis.Vertical)]
		[TestCase(-8, 70, ProductionSwipeAxis.Vertical)]
		[TestCase(70, 8, ProductionSwipeAxis.Horizontal)]
		[TestCase(14, 14, ProductionSwipeAxis.None)]
		public void TouchDragLocksToItsDominantAxis(int deltaX, int deltaY, ProductionSwipeAxis expected)
		{
			Assert.That(ProductionSwipePolicy.ResolveAxis(new int2(deltaX, deltaY), 12), Is.EqualTo(expected));
		}

		[TestCase(-23, 24, 0)]
		[TestCase(-24, 24, 1)]
		[TestCase(-72, 24, 3)]
		[TestCase(48, 24, -2)]
		public void VerticalDragConvertsDistanceIntoNaturalRowScrolling(int deltaY, int pixelsPerRow, int expected)
		{
			Assert.That(ProductionSwipePolicy.ResolveVerticalRows(deltaY, pixelsPerRow), Is.EqualTo(expected));
		}

		[TestCase(0, ProductionSwipeDirection.Next, 2)]
		[TestCase(2, ProductionSwipeDirection.Previous, 0)]
		[TestCase(2, ProductionSwipeDirection.Next, -1)]
		[TestCase(0, ProductionSwipeDirection.Previous, -1)]
		public void AdjacentPageSkipsUnavailableTypesWithoutWrapping(
			int current, ProductionSwipeDirection direction, int expected)
		{
			Assert.That(ProductionSwipePolicy.FindAdjacentIndex(
				current, new[] { true, false, true }, direction), Is.EqualTo(expected));
		}

		[Test]
		public void ClassicRa2ProductionSidebarConnectsSwipeToCategoryButtons()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Widgets",
				"Logic", "Ingame", "ClassicProductionLogic.cs"));
			StringAssert.Contains("palette.OnSwipeProductionType = SelectAdjacentProductionGroup", source);
			StringAssert.Contains("productionTypeButtons[adjacent].OnClick()", source);

			var palette = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Widgets",
				"ProductionPaletteWidget.cs"));
			StringAssert.Contains("ProductionSwipeAxis.Vertical", palette);
			StringAssert.Contains("ApplyTouchRowOffset", palette);
		}
	}
}
