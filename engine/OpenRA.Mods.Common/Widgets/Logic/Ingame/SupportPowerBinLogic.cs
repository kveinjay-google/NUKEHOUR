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
using System.Collections.Generic;
using OpenRA.Mods.Common.Widgets.Logic.Ingame;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class SupportPowerBinLogic : ChromeLogic
	{
		readonly SupportPowersWidget palette;
		readonly Widget background;
		readonly Widget foreground;
		readonly Widget backgroundTemplate;
		readonly Widget foregroundTemplate;
		readonly IosSupportPowerLayoutTracker layoutTracker = new();
		readonly Func<int, IReadOnlyList<Rectangle>> cellProvider;
		readonly Action<IosSupportPowerLayoutSignature> applyLayout;
		IReadOnlyList<Rectangle> activeCells;

		[ObjectCreator.UseCtor]
		public SupportPowerBinLogic(Widget widget)
		{
			palette = widget.Get<SupportPowersWidget>("SUPPORT_PALETTE");
			background = widget.GetOrNull("PALETTE_BACKGROUND");
			foreground = widget.GetOrNull("PALETTE_FOREGROUND");
			backgroundTemplate = background?.Get("ICON_TEMPLATE");
			foregroundTemplate = foreground?.Get("ICON_TEMPLATE");
			cellProvider = GetActiveCells;
			applyLayout = ApplyLayout;

			if (Platform.IsIOS)
			{
				palette.CellProvider = cellProvider;
				UpdateLayers(0, null);
			}
			else if (background != null || foreground != null)
			{
				palette.OnIconCountChanged += UpdateLegacyLayers;

				// Set the initial palette state
				UpdateLegacyLayers(0, 0);
			}
		}

		public override void Tick()
		{
			if (!Platform.IsIOS)
				return;

			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			var obstacles = new IosSupportPowerObstacles(
				GetProductionObstacle(),
				GetObstacle("COMMAND_BAR_BACKGROUND"),
				GetObstacle("IOS_VIEWPORT_JOYSTICK"),
				GetObstacle("IOS_VIEWPORT_ACTIONS"));
			var signature = new IosSupportPowerLayoutSignature(
				resolution, snapshot, palette.IconCount, obstacles);
			layoutTracker.RunIfChanged(signature, applyLayout);
		}

		void ApplyLayout(IosSupportPowerLayoutSignature signature)
		{
			var layout = IosSupportPowerLayoutPolicy.Create(
				true, signature.Snapshot, signature.IconCount, signature.Obstacles);
			activeCells = layout.HasOverride ? layout.Cells : null;
			palette.CellProvider = activeCells == null ? null : cellProvider;

			// SupportPowersWidget ticks before this logic. Refresh once more only when
			// the layout signature changes so this frame uses the new cell provider.
			palette.RefreshIcons();
			var layerCells = activeCells != null && activeCells.Count == palette.IconCount
				? activeCells
				: null;
			UpdateLayers(palette.IconCount, layerCells);
		}

		IReadOnlyList<Rectangle> GetActiveCells(int iconCount)
		{
			return activeCells != null && activeCells.Count == iconCount ? activeCells : null;
		}

		static IosSupportPowerObstacle GetObstacle(string id)
		{
			var obstacle = Ui.Root.GetOrNull(id);
			if (obstacle == null || !obstacle.IsVisible())
				return default;

			return new IosSupportPowerObstacle(obstacle.RenderBounds, true);
		}

		static IosSupportPowerObstacle GetProductionObstacle()
		{
			var production = Ui.Root.GetOrNull("SIDEBAR_PRODUCTION");
			var backgroundTop = Ui.Root.GetOrNull("SIDEBAR_BACKGROUND_TOP");
			return CombineVisibleObstacleCore(production, backgroundTop);
		}

		public static IosSupportPowerObstacle CombineVisibleObstacle(Widget requiredAnchor, params Widget[] relatedRoots)
		{
			if (relatedRoots == null || relatedRoots.Length <= 1)
			{
				var relatedRoot = relatedRoots == null || relatedRoots.Length == 0 ? null : relatedRoots[0];
				return CombineVisibleObstacleCore(requiredAnchor, relatedRoot);
			}

			if (!TryGetVisibleAnchorBounds(requiredAnchor, out var bounds))
				return default;

			foreach (var root in relatedRoots)
				UnionVisibleSubtree(root, ref bounds);

			return new IosSupportPowerObstacle(bounds, true);
		}

		static IosSupportPowerObstacle CombineVisibleObstacleCore(Widget requiredAnchor, Widget relatedRoot)
		{
			if (!TryGetVisibleAnchorBounds(requiredAnchor, out var bounds))
				return default;

			UnionVisibleSubtree(relatedRoot, ref bounds);
			return new IosSupportPowerObstacle(bounds, true);
		}

		static bool TryGetVisibleAnchorBounds(Widget requiredAnchor, out Rectangle bounds)
		{
			bounds = default;
			if (requiredAnchor == null || !requiredAnchor.IsVisible())
				return false;

			bounds = requiredAnchor.RenderBounds;
			UnionVisibleDescendants(requiredAnchor, ref bounds);
			return true;
		}

		static void UnionVisibleDescendants(Widget widget, ref Rectangle bounds)
		{
			foreach (var child in widget.Children)
				UnionVisibleSubtree(child, ref bounds);
		}

		static void UnionVisibleSubtree(Widget widget, ref Rectangle bounds)
		{
			if (widget == null || !widget.IsVisible())
				return;

			bounds = Rectangle.Union(bounds, widget.RenderBounds);
			UnionVisibleDescendants(widget, ref bounds);
		}

		void UpdateLegacyLayers(int _, int icons)
		{
			UpdateLayers(icons, null);
		}

		void UpdateLayers(int icons, IReadOnlyList<Rectangle> cells)
		{
			UpdateLayer(background, backgroundTemplate, icons, cells);
			UpdateLayer(foreground, foregroundTemplate, icons, cells);
		}

		void UpdateLayer(
			Widget layer,
			Widget template,
			int icons,
			IReadOnlyList<Rectangle> cells)
		{
			if (layer == null)
				return;

			layer.RemoveChildren();
			var rowHeight = palette.IconSize.Y + palette.IconMargin;
			var rowWidth = palette.IconSize.X + palette.IconMargin;
			for (var i = 0; i < icons; i++)
			{
				var row = template.Clone();
				if (cells != null && cells.Count == icons)
				{
					var placement = IosSupportPowerCellPolicy.PlaceTemplate(
						cells[i], new Size(palette.IconSize.X, palette.IconSize.Y),
						layer.ChildOrigin, template.Bounds.ToRectangle());
					row.Bounds = new WidgetBounds(
						placement.X, placement.Y, placement.Width, placement.Height);
				}
				else if (palette.Horizontal)
					row.Bounds.X += i * rowWidth;
				else
					row.Bounds.Y += i * rowHeight;

				layer.AddChild(row);
			}
		}
	}
}
