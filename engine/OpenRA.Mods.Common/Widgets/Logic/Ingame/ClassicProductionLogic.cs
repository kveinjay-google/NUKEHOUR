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
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class ClassicProductionLogic : ChromeLogic
	{
		readonly ProductionPaletteWidget palette;
		readonly World world;
		readonly ProductionTypeButtonWidget[] productionTypeButtons;

		void SetupProductionGroupButton(ProductionTypeButtonWidget button)
		{
			if (button == null)
				return;

			// Classic production queues are initialized at game start, and then never change.
			var queues = world.LocalPlayer.PlayerActor.TraitsImplementing<ProductionQueue>()
				.Where(q => (q.Info.Group ?? q.Info.Type) == button.ProductionGroup)
				.ToArray();

			void SelectTab(bool reverse)
			{
				// Prefer an enabled queue; fall back so categories with no producer yet still switch the palette.
				palette.CurrentQueue = queues.FirstOrDefault(q => q.Enabled) ?? queues.FirstOrDefault();

				// When a tab is selected, scroll to the top because the current row position may be invalid for the new tab
				palette.ScrollToTop();

				// Attempt to pick up a completed building (if there is one) so it can be placed
				palette.PickUpCompletedBuilding();
			}

			// Always keep category tabs clickable and show their icons. Empty queues simply show an empty palette.
			button.IsDisabled = () => queues.Length == 0;
			button.OnMouseUp = mi => SelectTab(mi.Modifiers.HasModifier(Modifiers.Shift));
			button.OnKeyPress = e => SelectTab(e.Modifiers.HasModifier(Modifiers.Shift));
			button.OnClick = () => SelectTab(false);
			button.IsHighlighted = () => ProductionAlertBlinkPolicy.ShouldHighlightCategory(
				queues.Contains(palette.CurrentQueue),
				queues.Any(q => q.AllQueued().Any(i => i.Done)),
				Game.RunTime);

			var chromeName = button.ProductionGroup.ToLowerInvariant();
			var icon = button.Get<ImageWidget>("ICON");
			icon.GetImageName = () =>
				ProductionAlertBlinkPolicy.ShowAlertFrame(
					queues.Any(q => q.AllQueued().Any(i => i.Done)), Game.RunTime) ?
					chromeName + "-alert" : chromeName;
		}

		[ObjectCreator.UseCtor]
		public ClassicProductionLogic(Widget widget, World world)
		{
			this.world = world;
			palette = widget.Get<ProductionPaletteWidget>("PRODUCTION_PALETTE");
			ApplyIosSidebarLayout(widget, palette);

			var background = widget.GetOrNull("PALETTE_BACKGROUND");
			var foreground = widget.GetOrNull("PALETTE_FOREGROUND");
			if (background != null || foreground != null)
			{
				Widget backgroundTemplate = null;
				Widget backgroundBottom = null;
				Widget foregroundTemplate = null;

				if (background != null)
				{
					backgroundTemplate = background.Get("ROW_TEMPLATE");
					backgroundBottom = background.GetOrNull("BOTTOM_CAP");
				}

				if (foreground != null)
					foregroundTemplate = foreground.Get("ROW_TEMPLATE");

				void UpdateBackground(int _, int icons)
				{
					var iosLayout = Platform.IsIOS
						? IosIngameSidebarLayoutPolicy.Create(
							IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution))
						: default;
					var rows = Platform.IsIOS ? iosLayout.BackgroundRows :
						Math.Min(palette.MaximumRows,
							Math.Max(palette.MinimumRows, (icons + palette.Columns - 1) / palette.Columns));
					var rowHeights = Platform.IsIOS
						? IosIngameSidebarLayoutPolicy.BackgroundRowHeights(iosLayout)
						: Enumerable.Repeat(backgroundTemplate?.Bounds.Height ?? foregroundTemplate.Bounds.Height, rows).ToArray();

					if (background != null)
					{
						background.RemoveChildren();

						var y = 0;
						foreach (var height in rowHeights)
						{
							var row = backgroundTemplate.Clone();
							row.Bounds.Y = y;
							row.Bounds.Height = height;
							if (row is ImageWidget image)
								image.StretchToFit = height != backgroundTemplate.Bounds.Height;
							background.AddChild(row);
							y += height;
						}

						if (backgroundBottom == null)
							return;

						backgroundBottom.Bounds.Y = Platform.IsIOS ? iosLayout.BottomCapY : y;
						background.AddChild(backgroundBottom);
					}

					if (foreground != null)
					{
						foreground.RemoveChildren();

						var y = 0;
						foreach (var height in rowHeights)
						{
							var row = foregroundTemplate.Clone();
							row.Bounds.Y = y;
							row.Bounds.Height = height;
							if (row is ImageWidget image)
								image.StretchToFit = height != foregroundTemplate.Bounds.Height;
							foreground.AddChild(row);
							y += height;
						}
					}
				}

				palette.OnIconCountChanged += UpdateBackground;

				// Set the initial palette state
				UpdateBackground(0, 0);
			}

			var typesContainer = widget.Get("PRODUCTION_TYPES");
			productionTypeButtons = typesContainer.Children.OfType<ProductionTypeButtonWidget>().ToArray();
			foreach (var button in productionTypeButtons)
				SetupProductionGroupButton(button);

			palette.OnSwipeProductionType = SelectAdjacentProductionGroup;

			// Hook up scroll up and down buttons on the palette before installing the
			// ticker so their authored bottom-cap slots can be refreshed after any
			// renderer or widget relayout.
			var scrollDown = widget.GetOrNull<ButtonWidget>("SCROLL_DOWN_BUTTON");
			if (scrollDown != null)
			{
				scrollDown.OnClick = palette.ScrollDown;
				scrollDown.IsVisible = () => palette.TotalIconCount > palette.MaxIconRowOffset * palette.Columns;
				scrollDown.IsDisabled = () => !palette.CanScrollDown;
			}

			var scrollUp = widget.GetOrNull<ButtonWidget>("SCROLL_UP_BUTTON");
			if (scrollUp != null)
			{
				scrollUp.OnClick = palette.ScrollUp;
				scrollUp.IsVisible = () => palette.TotalIconCount > palette.MaxIconRowOffset * palette.Columns;
				scrollUp.IsDisabled = () => !palette.CanScrollUp;
			}

			RefreshIosSidebarLayout(widget, palette, typesContainer, scrollUp, scrollDown);

			var ticker = widget.Get<LogicTickerWidget>("PRODUCTION_TICKER");
			ticker.OnTick = () =>
			{
				RefreshIosSidebarLayout(widget, palette, typesContainer, scrollUp, scrollDown);

				// Only auto-select when nothing is selected. Do not leave an intentionally empty category tab.
				if (palette.CurrentQueue == null)
				{
					foreach (var b in typesContainer.Children)
					{
						if (b is not ProductionTypeButtonWidget button || button.IsDisabled())
							continue;

						button.OnClick();
						break;
					}
				}
			};

			SetMaximumVisibleRows(palette);
		}

		static void PositionIosScrollButton(
			Widget typesContainer, ButtonWidget button, IosIngameSidebarLayout layout)
		{
			if (!Platform.IsIOS || button == null)
				return;

			// The bottom-cap artwork contains an authored slot for these buttons at
			// its top edge. Convert that production-panel coordinate into the local
			// coordinate of PRODUCTION_TYPES whenever layout changes so visuals and
			// hit targets cannot retain stale startup coordinates.
			button.Bounds = layout.ScrollButtonLocalBounds(
				typesContainer.Bounds.Y, button.Id == "SCROLL_UP_BUTTON");
		}

		static void RefreshIosSidebarLayout(
			Widget productionSidebar,
			ProductionPaletteWidget productionPalette,
			Widget typesContainer,
			ButtonWidget scrollUp,
			ButtonWidget scrollDown)
		{
			if (!Platform.IsIOS)
				return;

			var layout = IosIngameSidebarLayoutPolicy.Create(
				IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution));
			ApplyIosSidebarLayout(productionSidebar, productionPalette, layout);
			PositionIosScrollButton(typesContainer, scrollUp, layout);
			PositionIosScrollButton(typesContainer, scrollDown, layout);
		}

		static void ApplyIosSidebarLayout(Widget productionSidebar, ProductionPaletteWidget productionPalette)
		{
			if (!Platform.IsIOS)
				return;

			var layout = IosIngameSidebarLayoutPolicy.Create(
				IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution));
			ApplyIosSidebarLayout(productionSidebar, productionPalette, layout);
		}

		static void ApplyIosSidebarLayout(
			Widget productionSidebar,
			ProductionPaletteWidget productionPalette,
			IosIngameSidebarLayout layout)
		{
			productionSidebar.Bounds.X = layout.ProductionBounds.X;
			productionSidebar.Bounds.Y = layout.ProductionBounds.Y;
			productionSidebar.Bounds.Width = layout.ProductionBounds.Width;
			productionSidebar.Bounds.Height = layout.ProductionBounds.Height;
			productionPalette.MaximumRows = layout.MaximumRows;

			var top = Ui.Root.GetOrNull("SIDEBAR_BACKGROUND_TOP");
			if (top != null)
			{
				top.Bounds.X = layout.TopBounds.X;
				top.Bounds.Y = layout.TopBounds.Y;
				top.Bounds.Width = layout.TopBounds.Width;
				top.Bounds.Height = layout.TopBounds.Height;
			}
		}

		bool SelectAdjacentProductionGroup(ProductionSwipeDirection direction)
		{
			if (productionTypeButtons.Length < 2)
				return false;

			var current = Array.FindIndex(productionTypeButtons, button => button.IsHighlighted());
			if (current < 0)
				return false;

			var enabled = productionTypeButtons.Select(button => !button.IsDisabled()).ToArray();
			var adjacent = ProductionSwipePolicy.FindAdjacentIndex(current, enabled, direction);
			if (adjacent < 0)
				return false;

			productionTypeButtons[adjacent].OnClick();
			return true;
		}

		static void SetMaximumVisibleRows(ProductionPaletteWidget productionPalette)
		{
			if (Platform.IsIOS)
			{
				var container = Ui.Root.GetOrNull<ContainerWidget>("SIDEBAR_PRODUCTION");
				if (container == null)
					return;

				var layout = IosIngameSidebarLayoutPolicy.Create(
					IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution));
				productionPalette.MaximumRows = layout.MaximumRows;
				productionPalette.MaxIconRowOffset = layout.MaximumRows;
				return;
			}

			var screenHeight = Game.Renderer.Resolution.Height;

			// Get height of currently displayed icons
			var containerWidget = Ui.Root.GetOrNull<ContainerWidget>("SIDEBAR_PRODUCTION");

			if (containerWidget == null)
				return;

			var sidebarProductionHeight = containerWidget.Bounds.Y;

			// Check if icon heights exceed y resolution
			var maxItemsHeight = screenHeight - sidebarProductionHeight;

			var maxIconRowOffest = maxItemsHeight / productionPalette.IconSize.Y - 1;
			productionPalette.MaxIconRowOffset = Math.Min(maxIconRowOffest, productionPalette.MaximumRows);
		}
	}
}
