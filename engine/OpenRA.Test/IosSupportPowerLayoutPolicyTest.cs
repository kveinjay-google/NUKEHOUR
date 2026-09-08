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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Mods.Common.Widgets.Logic.Ingame;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosSupportPowerLayoutPolicyTest
	{
		static readonly int[] IconCounts = { 0, 1, 2, 3, 6, 7, 12 };

		static IEnumerable<TestCaseData> DeviceCases()
		{
			yield return new TestCaseData(
				new IosScreenSnapshot(new Size(844, 390), new Size(844, 390), new IosSafeAreaInsets(47, 0, 34, 21)),
				true).SetName("iPhone_844x390");
			yield return new TestCaseData(
				new IosScreenSnapshot(new Size(932, 430), new Size(932, 430), new IosSafeAreaInsets(59, 0, 41, 21)),
				true).SetName("iPhone_932x430");
			yield return new TestCaseData(
				new IosScreenSnapshot(new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(47, 0, 47, 21)),
				true).SetName("iPhone_844x390_scaled_render_surface");
			yield return new TestCaseData(
				new IosScreenSnapshot(new Size(1133, 744), new Size(1133, 744), new IosSafeAreaInsets(0, 0, 0, 20)),
				false).SetName("iPadMini_1133x744");
			yield return new TestCaseData(
				new IosScreenSnapshot(new Size(1366, 1024), new Size(1366, 1024), new IosSafeAreaInsets(0, 0, 0, 20)),
				false).SetName("iPad_1366x1024");
		}

		static IEnumerable<TestCaseData> CompactSidebarCases()
		{
			foreach (var snapshot in new[]
			{
				new IosScreenSnapshot(
					new Size(844, 390), new Size(844, 390), new IosSafeAreaInsets(47, 0, 34, 21)),
				new IosScreenSnapshot(
					new Size(932, 430), new Size(932, 430), new IosSafeAreaInsets(59, 0, 41, 21)),
				new IosScreenSnapshot(
					new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(47, 0, 47, 21))
			})
				foreach (var count in IconCounts)
					yield return new TestCaseData(snapshot, count)
						.SetName($"effective{snapshot.EffectiveSize.Width}x{snapshot.EffectiveSize.Height}_count{count}");
		}

		static IosSupportPowerObstacles ObstaclesFor(IosScreenSnapshot snapshot)
		{
			var safe = snapshot.SafeBounds;
			if (snapshot.IsCompactPhone)
			{
				return new IosSupportPowerObstacles(
					new IosSupportPowerObstacle(new Rectangle(safe.Right - 125, safe.Top + 120, 125, 210), true),
					new IosSupportPowerObstacle(new Rectangle(safe.Left + 145, safe.Bottom - 70, 410, 70), true),
					new IosSupportPowerObstacle(new Rectangle(safe.Left, safe.Bottom - 175, 140, 160), true),
					new IosSupportPowerObstacle(new Rectangle(safe.Left + 100, safe.Bottom - 195, 165, 180), true));
			}

			return new IosSupportPowerObstacles(
				new IosSupportPowerObstacle(new Rectangle(safe.Right - 235, safe.Top + 160, 235, 320), true),
				new IosSupportPowerObstacle(new Rectangle(safe.Left + 300, safe.Bottom - 70, 700, 70), true),
				new IosSupportPowerObstacle(new Rectangle(safe.Left, safe.Bottom - 205, 190, 190), true),
				new IosSupportPowerObstacle(new Rectangle(safe.Left + 120, safe.Bottom - 230, 250, 210), true));
		}

		[TestCaseSource(nameof(DeviceCases))]
		public void DeviceMatrixFitsEverySupportedIconCount(IosScreenSnapshot snapshot, bool compact)
		{
			var obstacles = ObstaclesFor(snapshot);
			foreach (var count in IconCounts)
			{
				var layout = IosSupportPowerLayoutPolicy.Create(true, snapshot, count, obstacles);
				Assert.Multiple(() =>
				{
					Assert.That(layout.HasOverride, Is.True, $"count={count}");
					Assert.That(layout.FitsAll, Is.True, $"count={count}");
					Assert.That(layout.Cells.Count, Is.EqualTo(count), $"count={count}");
					Assert.That(layout.Mode, Is.EqualTo(compact
						? IosSupportPowerLayoutMode.CompactBottomRight
						: IosSupportPowerLayoutMode.SafeTopLeft));
					Assert.That(layout.CellSize.Width, Is.GreaterThanOrEqualTo(snapshot.LogicalPoints(48)));
					Assert.That(layout.CellSize.Height, Is.GreaterThanOrEqualTo(snapshot.LogicalPoints(48)));
				});

				if (!compact)
				{
					Assert.That(layout.CellSize.Width, Is.GreaterThanOrEqualTo(60));
					Assert.That(layout.CellSize.Height, Is.GreaterThanOrEqualTo(48));
				}

				AssertCellsAreSafe(layout, obstacles);
				var duplicate = IosSupportPowerLayoutPolicy.Create(true, snapshot, count, obstacles);
				CollectionAssert.AreEqual(layout.Cells, duplicate.Cells, $"count={count}");
			}
		}

		[TestCaseSource(nameof(DeviceCases))]
		public void ObstacleVisibilityCombinationsRemainSafe(IosScreenSnapshot snapshot, bool _)
		{
			var occupied = ObstaclesFor(snapshot);
			for (var visibilityMask = 0; visibilityMask < 16; visibilityMask++)
			{
				var obstacles = new IosSupportPowerObstacles(
					WithVisibility(occupied.Production, (visibilityMask & 1) != 0),
					WithVisibility(occupied.CommandBar, (visibilityMask & 2) != 0),
					WithVisibility(occupied.Joystick, (visibilityMask & 4) != 0),
					WithVisibility(occupied.Actions, (visibilityMask & 8) != 0));

				foreach (var count in IconCounts)
				{
					var layout = IosSupportPowerLayoutPolicy.Create(true, snapshot, count, obstacles);
					Assert.That(layout.HasOverride, Is.True,
						$"mask={visibilityMask}, count={count}");
					Assert.That(layout.Cells.Count, Is.EqualTo(count),
						$"mask={visibilityMask}, count={count}");
					AssertCellsAreSafe(layout, obstacles);
				}
			}
		}

		[Test]
		public void CompactPhoneAnchorsEverySupportPowerOutsideTheProductionSidebarLeftEdge()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(47, 0, 47, 21));
			var existing = ObstaclesFor(snapshot);
			var production = new IosSupportPowerObstacle(
				new Rectangle(1324, 275, 234, 250), true);
			var obstacles = new IosSupportPowerObstacles(
				production, existing.CommandBar, existing.Joystick, existing.Actions);
			var fullSidebar = new Rectangle(
				production.Bounds.Left, snapshot.SafeBounds.Top, production.Bounds.Width,
				snapshot.SafeBounds.Bottom - snapshot.SafeBounds.Top);
			var layout = IosSupportPowerLayoutPolicy.Create(true, snapshot, 6, obstacles);

			Assert.Multiple(() =>
			{
				Assert.That(layout.FitsAll, Is.True);
				Assert.That(layout.HasOverride, Is.True);
				Assert.That(layout.Cells.Count, Is.EqualTo(6));
			});
			Assert.That(layout.Cells.Max(cell => cell.Right),
				Is.EqualTo(production.Bounds.Left - layout.Margin));
			foreach (var cell in layout.Cells)
			{
				Assert.That(layout.PlacementBounds.Contains(cell), Is.True, cell.ToString());
				Assert.That(cell.IntersectsWith(Expand(fullSidebar, layout.Margin)), Is.False,
					$"cell={cell}, full sidebar={fullSidebar}");
			}

			AssertCellsAreSafe(layout, obstacles);
		}

		[TestCaseSource(nameof(CompactSidebarCases))]
		public void CompactPhoneSidebarMatrixStaysOutsideTheRightmostSidebar(IosScreenSnapshot snapshot, int count)
		{
			var sidebar = new Rectangle(
				snapshot.EffectiveSize.Width - 234, snapshot.SafeBounds.Top, 234,
				snapshot.SafeBounds.Bottom - snapshot.SafeBounds.Top);
			var occupied = ObstaclesFor(snapshot);
			var obstacles = new IosSupportPowerObstacles(
				new IosSupportPowerObstacle(sidebar, true),
				occupied.CommandBar, occupied.Joystick, occupied.Actions);
			var layout = IosSupportPowerLayoutPolicy.Create(true, snapshot, count, obstacles);

			Assert.Multiple(() =>
			{
				Assert.That(layout.FitsAll, Is.True, $"count={count}");
				Assert.That(layout.HasOverride, Is.True, $"count={count}");
				Assert.That(layout.Cells.Count, Is.EqualTo(count), $"count={count}");
			});
			if (count == 0)
				return;

			Assert.That(layout.Cells.Max(cell => cell.Right),
				Is.EqualTo(sidebar.Left - layout.Margin), $"count={count}");
			AssertCellsAreSafe(layout, obstacles);
		}

		[Test]
		public void CompactPhoneFillsEachSidebarColumnBottomToTopThenExpandsOnlyLeft()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(844, 390), new Size(844, 390), new IosSafeAreaInsets(47, 0, 34, 21));
			var sidebar = new Rectangle(snapshot.EffectiveSize.Width - 234, 0, 234, snapshot.EffectiveSize.Height);
			var obstacles = new IosSupportPowerObstacles(
				new IosSupportPowerObstacle(sidebar, true), default, default, default);
			var layout = IosSupportPowerLayoutPolicy.Create(true, snapshot, 12, obstacles);

			Assert.Multiple(() =>
			{
				Assert.That(layout.FitsAll, Is.True);
				Assert.That(layout.HasOverride, Is.True);
				Assert.That(layout.Cells.Count, Is.EqualTo(12));
			});

			var columns = new List<List<Rectangle>>();
			foreach (var cell in layout.Cells)
			{
				if (columns.Count == 0 || columns[^1][0].X != cell.X)
					columns.Add(new List<Rectangle>());
				columns[^1].Add(cell);
			}

			var firstX = columns[0][0].X;
			var availableYs = new List<int>();
			for (var y = layout.PlacementBounds.Bottom - layout.CellSize.Height;
				y >= layout.PlacementBounds.Top;
				y -= layout.CellSize.Height + layout.Gap)
			{
				var candidate = new Rectangle(firstX, y, layout.CellSize.Width, layout.CellSize.Height);
				if (layout.PlacementBounds.Contains(candidate) &&
					!candidate.IntersectsWith(Expand(sidebar, layout.Margin)))
					availableYs.Add(y);
			}

			Assert.That(availableYs, Is.Not.Empty);
			Assert.That(columns[0].Count, Is.EqualTo(availableYs.Count));
			for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
			{
				var column = columns[columnIndex];
				Assert.That(column[0].Y, Is.EqualTo(availableYs[0]));
				CollectionAssert.AreEqual(availableYs.Take(column.Count), column.Select(cell => cell.Y).ToArray());
				if (columnIndex > 0)
					Assert.That(column[0].X, Is.LessThan(columns[columnIndex - 1][0].X));
				if (columnIndex < columns.Count - 1)
					Assert.That(column.Count, Is.EqualTo(availableYs.Count));
			}

			for (var i = 1; i < layout.Cells.Count; i++)
				Assert.That(layout.Cells[i].X, Is.LessThanOrEqualTo(layout.Cells[i - 1].X));

			AssertCellsAreSafe(layout, obstacles);
		}

		[Test]
		public void CompactPhoneStartsAtLowerRightAndFlowsRightToLeftThenUp()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(844, 390), new Size(844, 390), new IosSafeAreaInsets(47, 0, 34, 21));
			var layout = IosSupportPowerLayoutPolicy.Create(true, snapshot, 12, default);

			Assert.That(layout.Cells[0].Location, Is.EqualTo(new int2(
				layout.PlacementBounds.Right - layout.CellSize.Width,
				layout.PlacementBounds.Bottom - layout.CellSize.Height)));
			for (var i = 1; i < layout.Cells.Count; i++)
			{
				if (layout.Cells[i].Y == layout.Cells[i - 1].Y)
					Assert.That(layout.Cells[i].X, Is.LessThan(layout.Cells[i - 1].X));
				else
				{
					Assert.That(layout.Cells[i].Y, Is.LessThan(layout.Cells[i - 1].Y));
					Assert.That(layout.Cells[i].X,
						Is.EqualTo(layout.PlacementBounds.Right - layout.CellSize.Width));
				}
			}

			var narrow = new IosScreenSnapshot(
				new Size(320, 300), new Size(320, 300), new IosSafeAreaInsets(0, 0, 0, 0));
			var wrapped = IosSupportPowerLayoutPolicy.Create(true, narrow, 7, default);
			var firstUpperCell = wrapped.Cells.First(c => c.Y < wrapped.Cells[0].Y);
			Assert.That(firstUpperCell.X, Is.EqualTo(wrapped.PlacementBounds.Right - wrapped.CellSize.Width));
		}

		[Test]
		public void CompactRenderedFootprintsStayInsideCellsAndNeverOverlap()
		{
			foreach (var snapshot in new[]
			{
				new IosScreenSnapshot(
					new Size(844, 390), new Size(844, 390), new IosSafeAreaInsets(47, 0, 34, 21)),
				new IosScreenSnapshot(
					new Size(932, 430), new Size(932, 430), new IosSafeAreaInsets(59, 0, 41, 21)),
				new IosScreenSnapshot(
					new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(47, 0, 47, 21))
			})
			{
				var obstacles = ObstaclesFor(snapshot);
				var layout = IosSupportPowerLayoutPolicy.Create(true, snapshot, 12, obstacles);
				Assert.That(layout.HasOverride, Is.True);
				Assert.That(layout.CellSize.Width,
					Is.GreaterThanOrEqualTo(Math.Max(snapshot.LogicalPoints(48), 64)));
				Assert.That(layout.CellSize.Height,
					Is.GreaterThanOrEqualTo(Math.Max(snapshot.LogicalPoints(48), 64)));

				var rendered = layout.Cells.Select(cell =>
				{
					var art = IosSupportPowerCellPolicy.CenterArt(cell, new Size(60, 48));
					return new Rectangle(art.X - 2, art.Y - 2, 62, 50);
				}).ToArray();

				for (var i = 0; i < rendered.Length; i++)
				{
					Assert.That(layout.Cells[i].Contains(rendered[i]), Is.True,
						$"cell={layout.Cells[i]}, rendered={rendered[i]}");
					for (var j = i + 1; j < rendered.Length; j++)
						Assert.That(rendered[i].IntersectsWith(rendered[j]), Is.False,
							$"rendered footprints {i} and {j}");
				}
			}
		}

		[Test]
		public void TabletStartsAtUpperLeftAndMovesToNextColumnAtObstacle()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1133, 744), new Size(1133, 744), new IosSafeAreaInsets(0, 0, 0, 20));
			var layout = IosSupportPowerLayoutPolicy.Create(true, snapshot, 12, default);
			Assert.That(layout.Cells.Select(c => c.X).Distinct().Count(), Is.EqualTo(1));
			for (var i = 1; i < layout.Cells.Count; i++)
				Assert.That(layout.Cells[i].Y, Is.GreaterThan(layout.Cells[i - 1].Y));

			var constrained = new IosScreenSnapshot(
				new Size(400, 300), new Size(800, 700), new IosSafeAreaInsets(0, 0, 0, 0));
			var obstacle = new IosSupportPowerObstacles(
				new IosSupportPowerObstacle(new Rectangle(0, 145, 60, 70), true), default, default, default);
			var wrapped = IosSupportPowerLayoutPolicy.Create(true, constrained, 7, obstacle);
			Assert.That(wrapped.Cells[0].Location, Is.EqualTo(wrapped.PlacementBounds.Location));
			Assert.That(wrapped.Cells.Any(c => c.X > wrapped.Cells[0].X && c.Y == wrapped.PlacementBounds.Top), Is.True);
			AssertCellsAreSafe(wrapped, obstacle);
		}

		[Test]
		public void InvisibleObstaclesDoNotAffectLayout()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(844, 390), new Size(844, 390), new IosSafeAreaInsets(47, 0, 34, 21));
			var hidden = new IosSupportPowerObstacles(
				new IosSupportPowerObstacle(snapshot.SafeBounds, false), default, default, default);

			var baseline = IosSupportPowerLayoutPolicy.Create(true, snapshot, 7, default);
			var result = IosSupportPowerLayoutPolicy.Create(true, snapshot, 7, hidden);
			CollectionAssert.AreEqual(baseline.Cells, result.Cells);
		}

		[Test]
		public void CompactPhoneUsesGenericBottomRightFallbackWhenProductionIsMissingOrHidden()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(844, 390), new Size(844, 390), new IosSafeAreaInsets(47, 0, 34, 21));
			var missing = IosSupportPowerLayoutPolicy.Create(true, snapshot, 7, default);
			var hidden = new IosSupportPowerObstacles(
				new IosSupportPowerObstacle(new Rectangle(610, 0, 234, 390), false),
				default, default, default);
			var hiddenResult = IosSupportPowerLayoutPolicy.Create(true, snapshot, 7, hidden);

			Assert.That(missing.Mode, Is.EqualTo(IosSupportPowerLayoutMode.CompactBottomRight));
			Assert.That(hiddenResult.Mode, Is.EqualTo(IosSupportPowerLayoutMode.CompactBottomRight));
			CollectionAssert.AreEqual(missing.Cells, hiddenResult.Cells);
		}

		[Test]
		public void HiddenObstacleBoundsDoNotInvalidateAStableSignature()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(844, 390), new Size(844, 390), new IosSafeAreaInsets(47, 0, 34, 21));
			var first = new IosSupportPowerObstacles(
				new IosSupportPowerObstacle(new Rectangle(1, 2, 100, 200), false), default, default, default);
			var movedWhileHidden = new IosSupportPowerObstacles(
				new IosSupportPowerObstacle(new Rectangle(300, 200, 50, 40), false), default, default, default);
			var tracker = new IosSupportPowerLayoutTracker();

			Assert.That(tracker.Update(
				new IosSupportPowerLayoutSignature(snapshot.EffectiveSize, snapshot, 6, first)), Is.True);
			Assert.That(tracker.Update(
				new IosSupportPowerLayoutSignature(snapshot.EffectiveSize, snapshot, 6, movedWhileHidden)), Is.False);
		}

		[Test]
		public void ImpossibleLayoutReturnsNoPartialOverride()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(100, 100), new Size(100, 100), new IosSafeAreaInsets(0, 0, 0, 0));
			var result = IosSupportPowerLayoutPolicy.Create(true, snapshot, 12, default);

			Assert.Multiple(() =>
			{
				Assert.That(result.FitsAll, Is.False);
				Assert.That(result.HasOverride, Is.False);
				Assert.That(result.Cells, Is.Empty);
			});
		}

		[Test]
		public void DesktopRequestsNoOverride()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1920, 1080), new Size(1920, 1080), new IosSafeAreaInsets(50, 20, 50, 20));
			var result = IosSupportPowerLayoutPolicy.Create(false, snapshot, 12, ObstaclesFor(snapshot));

			Assert.Multiple(() =>
			{
				Assert.That(result.Mode, Is.EqualTo(IosSupportPowerLayoutMode.Desktop));
				Assert.That(result.HasOverride, Is.False);
				Assert.That(result.Cells, Is.Empty);
			});
		}

		[Test]
		public void CellProviderRejectsStaleCountsAndHitTestingUsesOnlyActualCells()
		{
			var cells = new[]
			{
				new Rectangle(0, 0, 48, 48),
				new Rectangle(54, 0, 48, 48),
				new Rectangle(54, 54, 48, 48)
			};

			Assert.That(IosSupportPowerCellPolicy.TryGetCells(3, _ => cells, out var current), Is.True);
			Assert.That(current, Is.SameAs(cells));
			Assert.That(IosSupportPowerCellPolicy.TryGetCells(2, _ => cells, out var stale), Is.False);
			Assert.That(stale, Is.Null);
			Assert.That(IosSupportPowerCellPolicy.Contains(current, new int2(24, 24)), Is.True);
			Assert.That(IosSupportPowerCellPolicy.Contains(current, new int2(51, 24)), Is.False, "gap");
			Assert.That(IosSupportPowerCellPolicy.Contains(current, new int2(24, 72)), Is.False, "union hole");
			Assert.That(IosSupportPowerCellPolicy.ShouldHandleMouseDown(current, new int2(51, 24)), Is.False);

			var art = IosSupportPowerCellPolicy.CenterArt(new Rectangle(10, 20, 80, 70), new Size(60, 48));
			Assert.That(art, Is.EqualTo(new Rectangle(20, 31, 60, 48)));
		}

		[Test]
		public void TemplatePlacementPreservesForegroundSizeAndRelativeOffset()
		{
			var cell = new Rectangle(100, 80, 64, 64);
			var childOrigin = new int2(10, 20);
			var template = new Rectangle(-2, -2, 62, 50);
			var local = IosSupportPowerCellPolicy.PlaceTemplate(
				cell, new Size(60, 48), childOrigin, template);
			var rendered = new Rectangle(
				local.X + childOrigin.X, local.Y + childOrigin.Y, local.Width, local.Height);
			var art = IosSupportPowerCellPolicy.CenterArt(cell, new Size(60, 48));

			Assert.That(local.Size, Is.EqualTo(template.Size));
			Assert.That(rendered, Is.EqualTo(new Rectangle(art.X - 2, art.Y - 2, 62, 50)));
		}

		[Test]
		public void LayoutTrackerRefreshesExactlyOnceForEverySignatureChange()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(844, 390), new Size(844, 390), new IosSafeAreaInsets(47, 0, 34, 21));
			var obstacles = ObstaclesFor(snapshot);
			var tracker = new IosSupportPowerLayoutTracker();

			void AssertOnce(IosSupportPowerLayoutSignature signature)
			{
				Assert.That(tracker.Update(signature), Is.True);
				Assert.That(tracker.Update(signature), Is.False);
			}

			AssertOnce(new IosSupportPowerLayoutSignature(new Size(844, 390), snapshot, 6, obstacles));
			AssertOnce(new IosSupportPowerLayoutSignature(new Size(845, 390), snapshot, 6, obstacles));

			var resized = new IosScreenSnapshot(
				new Size(845, 390), new Size(845, 390), new IosSafeAreaInsets(47, 0, 34, 21));
			AssertOnce(new IosSupportPowerLayoutSignature(new Size(845, 390), resized, 6, obstacles));

			var rotated = new IosScreenSnapshot(
				new Size(390, 845), new Size(390, 845), new IosSafeAreaInsets(0, 47, 21, 34));
			AssertOnce(new IosSupportPowerLayoutSignature(new Size(390, 845), rotated, 6, obstacles));
			AssertOnce(new IosSupportPowerLayoutSignature(new Size(390, 845), rotated, 7, obstacles));

			var movedObstacle = new IosSupportPowerObstacles(
				new IosSupportPowerObstacle(new Rectangle(1, 2, 3, 4), true),
				obstacles.CommandBar, obstacles.Joystick, obstacles.Actions);
			AssertOnce(new IosSupportPowerLayoutSignature(new Size(390, 845), rotated, 7, movedObstacle));

			var hiddenObstacle = new IosSupportPowerObstacles(
				new IosSupportPowerObstacle(movedObstacle.Production.Bounds, false),
				movedObstacle.CommandBar, movedObstacle.Joystick, movedObstacle.Actions);
			AssertOnce(new IosSupportPowerLayoutSignature(new Size(390, 845), rotated, 7, hiddenObstacle));
		}

		[Test]
		public void LayoutTrackerRunsTheRefreshMutationOnlyForChangedSignatures()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(844, 390), new Size(844, 390), new IosSafeAreaInsets(47, 0, 34, 21));
			var first = new IosSupportPowerLayoutSignature(snapshot.EffectiveSize, snapshot, 6, default);
			var second = new IosSupportPowerLayoutSignature(snapshot.EffectiveSize, snapshot, 7, default);
			var tracker = new IosSupportPowerLayoutTracker();
			var refreshes = 0;

			Assert.That(tracker.RunIfChanged(first, _ => refreshes++), Is.True);
			Assert.That(tracker.RunIfChanged(first, _ => refreshes++), Is.False);
			Assert.That(tracker.RunIfChanged(second, _ => refreshes++), Is.True);
			Assert.That(tracker.RunIfChanged(second, _ => refreshes++), Is.False);
			Assert.That(refreshes, Is.EqualTo(2));
		}

		[TestCase(30, 20)]
		[TestCase(390, 20)]
		[TestCase(30, 280)]
		[TestCase(390, 280)]
		public void TooltipOriginClampsInsideEveryAsymmetricSafeAreaEdge(int cursorX, int cursorY)
		{
			var snapshot = new IosScreenSnapshot(
				new Size(420, 300), new Size(420, 300), new IosSafeAreaInsets(30, 20, 45, 25));
			var tooltip = new Rectangle(0, 0, 120, 80);
			var origin = IosSupportPowerTooltipPolicy.ClampOrigin(
				new int2(cursorX, cursorY), tooltip, snapshot, new int2(8, 20), -5, 1);
			var rendered = new Rectangle(origin.X + tooltip.X, origin.Y + tooltip.Y, tooltip.Width, tooltip.Height);
			Assert.That(snapshot.SafeBounds.Contains(rendered), Is.True, $"origin={origin}, rendered={rendered}");
		}

		[Test]
		public void TooltipBottomOverflowFlipsAboveCursorAndRotationChangesClamp()
		{
			var landscape = new IosScreenSnapshot(
				new Size(844, 390), new Size(844, 390), new IosSafeAreaInsets(47, 0, 34, 21));
			var portrait = new IosScreenSnapshot(
				new Size(390, 844), new Size(390, 844), new IosSafeAreaInsets(0, 47, 21, 34));
			var tooltip = new Rectangle(3, 4, 240, 120);
			var cursor = new int2(380, 370);
			var first = IosSupportPowerTooltipPolicy.ClampOrigin(cursor, tooltip, landscape, new int2(8, 20), -5, 1);
			var second = IosSupportPowerTooltipPolicy.ClampOrigin(cursor, tooltip, portrait, new int2(8, 20), -5, 1);

			Assert.That(first.Y, Is.LessThan(cursor.Y));
			Assert.That(first, Is.Not.EqualTo(second));
		}

		[Test]
		public void TooltipTextWrapsUnbrokenEnglishAndCjkWithinSafeWidthAndHeight()
		{
			var english = new string('W', 80);
			const string Chinese = "超级武器支援技能可以在地图上选择目标区域并立即发动攻击";
			foreach (var value in new[] { english, Chinese, "A👨‍👩‍👧‍👦B" + english })
			{
				var fitted = IosSupportPowerTooltipPolicy.FitText(value, 64, 36, MeasureMonospace);
				var measured = MeasureMonospace(fitted);
				Assert.Multiple(() =>
				{
					Assert.That(fitted, Is.Not.Empty);
					Assert.That(measured.Width, Is.LessThanOrEqualTo(64), value);
					Assert.That(measured.Height, Is.LessThanOrEqualTo(36), value);
					Assert.That(fitted.Contains('\n') || fitted.EndsWith("…", StringComparison.Ordinal), Is.True, value);
				});
			}

			var snapshot = new IosScreenSnapshot(
				new Size(420, 300), new Size(420, 300), new IosSafeAreaInsets(30, 20, 45, 25));
			var maximum = IosSupportPowerTooltipPolicy.MaximumSize(snapshot);
			Assert.That(maximum.Width, Is.EqualTo(snapshot.SafeBounds.Width - snapshot.LogicalPoints(16)));
			Assert.That(maximum.Height, Is.EqualTo(snapshot.SafeBounds.Height - snapshot.LogicalPoints(16)));
		}

		[Test]
		public void TooltipTextNeverReturnsAnOversizedSingleGrapheme()
		{
			const string Family = "👨‍👩‍👧‍👦";
			var fitted = IosSupportPowerTooltipPolicy.FitText(
				Family + "A", 64, 36, text => MeasureWithWideFamily(text, Family));

			Assert.Multiple(() =>
			{
				Assert.That(MeasureWithWideFamily(fitted, Family).Width, Is.LessThanOrEqualTo(64));
				Assert.That(MeasureWithWideFamily(fitted, Family).Height, Is.LessThanOrEqualTo(36));
			});
		}

		[Test]
		public void TooltipSingleLineContentCannotPaintBeyondItsAssignedWidth()
		{
			foreach (var value in new[]
			{
				new string('N', 80),
				"超长本地化支援技能名称不能越过安全区域边界",
				"(CommandOrControlPlusAnExtremelyLongLocalizedHotkey)",
				"999999999999999999 / 999999999999999999"
			})
			{
				var fitted = IosSupportPowerTooltipPolicy.FitSingleLine(value, 64, MeasureMonospace);
				Assert.Multiple(() =>
				{
					Assert.That(fitted, Does.Not.Contain("\n"));
					Assert.That(MeasureMonospace(fitted).Width, Is.LessThanOrEqualTo(64), value);
					Assert.That(fitted.EndsWith("…", StringComparison.Ordinal), Is.True, value);
				});
			}
		}

		[Test]
		public void RuntimeIntegrationKeepsReflectedConstructorSignatures()
		{
			var binCtor = typeof(SupportPowerBinLogic).GetConstructors().Single();
			CollectionAssert.AreEqual(new[] { typeof(Widget) }, binCtor.GetParameters().Select(p => p.ParameterType));

			var paletteCtor = typeof(SupportPowersWidget).GetConstructors()
				.Single(c => c.GetCustomAttribute<ObjectCreator.UseCtorAttribute>() != null);
			CollectionAssert.AreEqual(
				new[] { typeof(ModData), typeof(World), typeof(OpenRA.Graphics.WorldRenderer) },
				paletteCtor.GetParameters().Select(p => p.ParameterType));
		}

		[Test]
		public void ProductionObstacleUnionsTheVisibleProductionAndTopRoots()
		{
			var production = new ContainerWidget { Bounds = new WidgetBounds(700, 80, 100, 200) };
			var top = new ContainerWidget { Bounds = new WidgetBounds(100, 20, 180, 40) };

			var obstacle = SupportPowerBinLogic.CombineVisibleObstacle(production, top);

			Assert.That(obstacle, Is.EqualTo(
				new IosSupportPowerObstacle(new Rectangle(100, 20, 700, 260), true)));
		}

		[Test]
		public void ProductionObstacleIncludesVisibleDescendantsBeyondTheirParentBounds()
		{
			var production = new ContainerWidget { Bounds = new WidgetBounds(700, 80, 100, 200) };
			var child = new ContainerWidget { Bounds = new WidgetBounds(10, 150, 40, 50) };
			child.AddChild(new ContainerWidget { Bounds = new WidgetBounds(0, 100, 150, 50) });
			production.AddChild(child);

			var obstacle = SupportPowerBinLogic.CombineVisibleObstacle(production);

			Assert.That(obstacle, Is.EqualTo(
				new IosSupportPowerObstacle(new Rectangle(700, 80, 160, 300), true)));
		}

		[Test]
		public void ProductionObstacleExcludesAnEntireHiddenChildSubtree()
		{
			var production = new ContainerWidget { Bounds = new WidgetBounds(700, 80, 100, 200) };
			var hidden = new ContainerWidget
			{
				Bounds = new WidgetBounds(10, 150, 40, 50),
				Visible = false
			};
			hidden.AddChild(new ContainerWidget { Bounds = new WidgetBounds(0, 150, 150, 50) });
			production.AddChild(hidden);

			var obstacle = SupportPowerBinLogic.CombineVisibleObstacle(production);

			Assert.That(obstacle, Is.EqualTo(
				new IosSupportPowerObstacle(new Rectangle(700, 80, 100, 200), true)));
		}

		[Test]
		public void ProductionObstacleIgnoresAMissingRelatedRoot()
		{
			var production = new ContainerWidget { Bounds = new WidgetBounds(700, 80, 100, 200) };

			var obstacle = SupportPowerBinLogic.CombineVisibleObstacle(production, null);

			Assert.That(obstacle, Is.EqualTo(
				new IosSupportPowerObstacle(new Rectangle(700, 80, 100, 200), true)));
		}

		[Test]
		public void HiddenProductionAnchorReturnsNoObstacleEvenWhenTopRootIsVisible()
		{
			var production = new ContainerWidget
			{
				Bounds = new WidgetBounds(700, 80, 100, 200),
				Visible = false
			};
			var top = new ContainerWidget { Bounds = new WidgetBounds(100, 20, 180, 40) };

			var obstacle = SupportPowerBinLogic.CombineVisibleObstacle(production, top);

			Assert.That(obstacle, Is.EqualTo(default(IosSupportPowerObstacle)));
		}

		[Test]
		public void NullProductionAnchorReturnsNoObstacle()
		{
			var top = new ContainerWidget { Bounds = new WidgetBounds(100, 20, 180, 40) };

			var obstacle = SupportPowerBinLogic.CombineVisibleObstacle(null, top);

			Assert.That(obstacle, Is.EqualTo(default(IosSupportPowerObstacle)));
		}

		[Test]
		public void RuntimeWidgetsConsumeTheExecutablePolicies()
		{
			var provider = typeof(SupportPowersWidget).GetField("CellProvider");
			Assert.That(provider, Is.Not.Null);
			Assert.That(provider!.FieldType,
				Is.EqualTo(typeof(Func<int, IReadOnlyList<Rectangle>>)));
			Assert.That(typeof(SupportPowersWidget).GetMethod(nameof(Widget.EventBoundsContains))!.DeclaringType,
				Is.EqualTo(typeof(SupportPowersWidget)));
			Assert.That(typeof(SupportPowerBinLogic).GetMethod(nameof(ChromeLogic.Tick))!.DeclaringType,
				Is.EqualTo(typeof(SupportPowerBinLogic)));

			var root = RepositoryRoot();
			var palette = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "SupportPowersWidget.cs"));
			var bin = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Ingame", "SupportPowerBinLogic.cs"));
			var container = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "TooltipContainerWidget.cs"));
			var tooltip = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Ingame", "SupportPowerTooltipLogic.cs"));

			Assert.Multiple(() =>
			{
				StringAssert.Contains("IosSupportPowerCellPolicy.TryGetCells", palette);
				StringAssert.Contains("IosSupportPowerCellPolicy.ShouldHandleMouseDown", palette);
				StringAssert.Contains("IosScreenMetrics.SnapshotFor", bin);
				foreach (var id in new[]
				{
					"SIDEBAR_PRODUCTION", "SIDEBAR_BACKGROUND_TOP", "COMMAND_BAR_BACKGROUND",
					"IOS_VIEWPORT_JOYSTICK", "IOS_VIEWPORT_ACTIONS"
				})
					StringAssert.Contains(id, bin);
				StringAssert.Contains("GetProductionObstacle()", bin);
				StringAssert.Contains("CombineVisibleObstacleCore(production, backgroundTop)", bin);
				StringAssert.Contains("if (relatedRoots == null || relatedRoots.Length <= 1)", bin);
				StringAssert.Contains("return CombineVisibleObstacleCore(requiredAnchor, relatedRoot);", bin);
				StringAssert.Contains("new IosSupportPowerObstacles(\n\t\t\t\tGetProductionObstacle(),", bin);
				StringAssert.Contains("layoutTracker.RunIfChanged", bin);
				StringAssert.Contains("palette.RefreshIcons()", bin);
				StringAssert.Contains("IosSupportPowerCellPolicy.PlaceTemplate", bin);
				StringAssert.Contains("if (!Platform.IsIOS)", container);
				StringAssert.Contains("IosSupportPowerTooltipPolicy.ClampOrigin", container);
				StringAssert.Contains("IosSupportPowerTooltipPolicy.MaximumSize", tooltip);
				StringAssert.Contains("IosSupportPowerTooltipPolicy.FitText", tooltip);
			});
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "engine", "OpenRA.Game")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		static Size MeasureMonospace(string text)
		{
			var lines = text.Split('\n');
			var width = lines.Max(line => StringInfo.ParseCombiningCharacters(line).Length) * 8;
			return new Size(width, Math.Max(1, lines.Length) * 12);
		}

		static Size MeasureWithWideFamily(string text, string family)
		{
			var lines = text.Split('\n');
			var width = lines.Max(line =>
			{
				var elements = new List<string>();
				var enumerator = StringInfo.GetTextElementEnumerator(line);
				while (enumerator.MoveNext())
					elements.Add(enumerator.GetTextElement());

				return elements.Sum(element => element == family ? 80 : 8);
			});
			return new Size(width, Math.Max(1, lines.Length) * 12);
		}

		static IosSupportPowerObstacle WithVisibility(IosSupportPowerObstacle obstacle, bool visible)
		{
			return new IosSupportPowerObstacle(obstacle.Bounds, visible);
		}

		static void AssertCellsAreSafe(IosSupportPowerLayout layout, IosSupportPowerObstacles obstacles)
		{
			foreach (var cell in layout.Cells)
			{
				Assert.That(layout.PlacementBounds.Contains(cell), Is.True, cell.ToString());
				foreach (var obstacle in obstacles.Visible)
					Assert.That(cell.IntersectsWith(Expand(obstacle.Bounds, layout.Margin)), Is.False,
						$"cell={cell}, obstacle={obstacle.Bounds}");
			}

			for (var i = 0; i < layout.Cells.Count; i++)
				for (var j = i + 1; j < layout.Cells.Count; j++)
					Assert.That(layout.Cells[i].IntersectsWith(layout.Cells[j]), Is.False,
						$"cells {i} and {j}");
		}

		static Rectangle Expand(Rectangle rect, int amount)
		{
			return Rectangle.FromLTRB(
				rect.Left - amount, rect.Top - amount,
				rect.Right + amount, rect.Bottom + amount);
		}
	}
}
