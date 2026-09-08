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
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public static class IosTouchDialogLayout
	{
		const int PhoneHeightBreakpoint = 600;

		public static IosConfirmationDialogLayout CreateConfirmation(
			IosScreenSnapshot snapshot, int promptLineCount, int buttonCount, bool hasTextInput)
		{
			return new IosConfirmationDialogLayout(
				snapshot,
				Math.Max(1, promptLineCount),
				Math.Clamp(buttonCount, 1, 3),
				hasTextInput,
				IsPhone(snapshot));
		}

		public static IosColorPickerLayout CreateColorPicker(
			IosScreenSnapshot snapshot, int columns, int presetRows, int customRows)
		{
			return new IosColorPickerLayout(
				snapshot,
				Math.Max(1, columns),
				Math.Max(1, presetRows),
				Math.Max(1, customRows),
				IsPhone(snapshot));
		}

		static bool IsPhone(IosScreenSnapshot snapshot) =>
			Math.Min(snapshot.NativePointSize.Width, snapshot.NativePointSize.Height) < PhoneHeightBreakpoint;

		internal static int Point(IosScreenSnapshot snapshot, int value) =>
			Math.Max(1, (int)Math.Ceiling(value * snapshot.LogicalPerPoint));

		internal static Rectangle Inset(Rectangle bounds, int inset)
		{
			var horizontal = Math.Min(inset, Math.Max(0, (bounds.Width - 1) / 2));
			var vertical = Math.Min(inset, Math.Max(0, (bounds.Height - 1) / 2));
			return new Rectangle(
				bounds.X + horizontal,
				bounds.Y + vertical,
				Math.Max(1, bounds.Width - 2 * horizontal),
				Math.Max(1, bounds.Height - 2 * vertical));
		}

		internal static Rectangle Center(Rectangle bounds, int width, int height)
		{
			width = Math.Clamp(width, 1, bounds.Width);
			height = Math.Clamp(height, 1, bounds.Height);
			return new Rectangle(
				bounds.X + (bounds.Width - width) / 2,
				bounds.Y + (bounds.Height - height) / 2,
				width,
				height);
		}
	}

	public sealed class IosConfirmationDialogLayout
	{
		readonly int promptLineCount;
		readonly int buttonCount;
		readonly int gap;

		public bool IsPhone { get; }
		public int MinimumTarget { get; }
		public Rectangle Panel { get; }
		public Rectangle LocalPanel { get; }
		public Rectangle Title { get; }
		public Rectangle Body { get; }
		public Rectangle PromptText { get; }
		public Rectangle TextInput { get; }
		public Rectangle Footer { get; }

		internal IosConfirmationDialogLayout(
			IosScreenSnapshot snapshot, int promptLineCount, int buttonCount, bool hasTextInput, bool isPhone)
		{
			this.promptLineCount = promptLineCount;
			this.buttonCount = buttonCount;
			IsPhone = isPhone;
			MinimumTarget = IosTouchDialogLayout.Point(snapshot, 48);
			gap = IosTouchDialogLayout.Point(snapshot, isPhone ? 8 : 12);
			var safeMargin = IosTouchDialogLayout.Point(snapshot, isPhone ? 8 : 18);
			var maximumPanel = IosTouchDialogLayout.Inset(snapshot.SafeBounds, safeMargin);
			var panelWidth = isPhone
				? maximumPanel.Width
				: Math.Min(maximumPanel.Width, IosTouchDialogLayout.Point(snapshot, 900));
			var contentInset = IosTouchDialogLayout.Point(snapshot, isPhone ? 12 : 20);
			var titleHeight = IosTouchDialogLayout.Point(snapshot, isPhone ? 42 : 52);
			var bodyDesiredHeight = hasTextInput
				? IosTouchDialogLayout.Point(snapshot, 42) + gap + MinimumTarget
				: Math.Max(IosTouchDialogLayout.Point(snapshot, 64),
					promptLineCount * IosTouchDialogLayout.Point(snapshot, 32));
			var desiredHeight = 2 * contentInset + titleHeight + 2 * gap +
				bodyDesiredHeight + MinimumTarget;
			var minimumPanelHeight = (int)Math.Ceiling(maximumPanel.Height * (isPhone ? 0.7 : 0.5));
			var panelHeight = Math.Min(maximumPanel.Height, Math.Max(desiredHeight, minimumPanelHeight));

			Panel = IosTouchDialogLayout.Center(maximumPanel, panelWidth, panelHeight);
			LocalPanel = new Rectangle(0, 0, Panel.Width, Panel.Height);
			var inner = IosTouchDialogLayout.Inset(LocalPanel, contentInset);
			Title = new Rectangle(inner.X, inner.Y, inner.Width, Math.Min(titleHeight, inner.Height));
			Footer = new Rectangle(
				inner.X,
				Math.Max(Title.Bottom, inner.Bottom - MinimumTarget),
				inner.Width,
				Math.Min(MinimumTarget, inner.Height));
			Body = Rectangle.FromLTRB(
				inner.Left,
				Math.Min(Footer.Top, Title.Bottom + gap),
				inner.Right,
				Math.Max(Title.Bottom + gap, Footer.Top - gap));

			if (hasTextInput)
			{
				var inputHeight = Math.Min(MinimumTarget, Body.Height);
				TextInput = new Rectangle(
					Body.X,
					Math.Max(Body.Y, Body.Bottom - inputHeight),
					Body.Width,
					inputHeight);
				PromptText = Rectangle.FromLTRB(
					Body.Left,
					Body.Top,
					Body.Right,
					Math.Max(Body.Top, TextInput.Top - gap));
			}
			else
			{
				PromptText = Body;
				TextInput = default;
			}
		}

		public Rectangle ButtonBounds(int index)
		{
			if (index < 0 || index >= buttonCount)
				throw new ArgumentOutOfRangeException(nameof(index));

			var availableWidth = Math.Max(buttonCount, Footer.Width - (buttonCount - 1) * gap);
			var baseWidth = availableWidth / buttonCount;
			var remainder = availableWidth % buttonCount;
			var x = Footer.X + index * (baseWidth + gap) + Math.Min(index, remainder);
			return new Rectangle(x, Footer.Y, baseWidth + (index < remainder ? 1 : 0), Footer.Height);
		}

		public Rectangle PromptLineBounds(int index)
		{
			if (index < 0 || index >= promptLineCount)
				throw new ArgumentOutOfRangeException(nameof(index));

			var baseHeight = PromptText.Height / promptLineCount;
			var remainder = PromptText.Height % promptLineCount;
			var y = PromptText.Y + index * baseHeight + Math.Min(index, remainder);
			return new Rectangle(
				PromptText.X,
				y,
				PromptText.Width,
				baseHeight + (index < remainder ? 1 : 0));
		}
	}

	public sealed class IosColorPickerLayout
	{
		readonly int columns;
		readonly int presetRows;
		readonly int customRows;
		readonly int swatchSize;

		public bool IsPhone { get; }
		public int MinimumTarget { get; }
		public Rectangle Panel { get; }
		public Rectangle LocalPanel { get; }
		public Rectangle Main { get; }
		public Rectangle Sidebar { get; }
		public Rectangle TabBar { get; }
		public Rectangle MixerTabButton { get; }
		public Rectangle PaletteTabButton { get; }
		public Rectangle MixerTab { get; }
		public Rectangle HueBackground { get; }
		public Rectangle HueSlider { get; }
		public Rectangle MixerBackground { get; }
		public Rectangle Mixer { get; }
		public Rectangle PaletteTab { get; }
		public Rectangle PalettePanel { get; }
		public Rectangle PresetHeader { get; }
		public Rectangle PresetArea { get; }
		public Rectangle CustomHeader { get; }
		public Rectangle CustomArea { get; }
		public Rectangle Preview { get; }
		public Rectangle RandomButton { get; }
		public Rectangle StoreButton { get; }

		internal IosColorPickerLayout(
			IosScreenSnapshot snapshot, int columns, int presetRows, int customRows, bool isPhone)
		{
			this.columns = columns;
			this.presetRows = presetRows;
			this.customRows = customRows;
			IsPhone = isPhone;
			MinimumTarget = IosTouchDialogLayout.Point(snapshot, 48);
			var safeMargin = IosTouchDialogLayout.Point(snapshot, isPhone ? 6 : 18);
			var maximumPanel = IosTouchDialogLayout.Inset(snapshot.SafeBounds, safeMargin);
			var panelWidth = isPhone
				? maximumPanel.Width
				: Math.Min(maximumPanel.Width, IosTouchDialogLayout.Point(snapshot, 1050));
			var panelHeight = isPhone
				? maximumPanel.Height
				: Math.Min(maximumPanel.Height, IosTouchDialogLayout.Point(snapshot, 780));
			Panel = IosTouchDialogLayout.Center(maximumPanel, panelWidth, panelHeight);
			LocalPanel = new Rectangle(0, 0, Panel.Width, Panel.Height);

			var contentInset = IosTouchDialogLayout.Point(snapshot, isPhone ? 8 : 16);
			var gap = IosTouchDialogLayout.Point(snapshot, isPhone ? 8 : 12);
			var border = IosTouchDialogLayout.Point(snapshot, 2);
			var inner = IosTouchDialogLayout.Inset(LocalPanel, contentInset);
			TabBar = new Rectangle(inner.X, inner.Bottom - MinimumTarget, inner.Width, MinimumTarget);
			var content = Rectangle.FromLTRB(inner.Left, inner.Top, inner.Right, TabBar.Top - gap);
			var minimumSidebarWidth = IosTouchDialogLayout.Point(snapshot, isPhone ? 150 : 220);
			var sidebarWidth = Math.Max(minimumSidebarWidth, content.Width / 4);
			var maximumSidebarWidth = Math.Max(MinimumTarget,
				content.Width - gap - columns * MinimumTarget);
			sidebarWidth = Math.Min(sidebarWidth, maximumSidebarWidth);
			Main = new Rectangle(content.X, content.Y,
				Math.Max(1, content.Width - sidebarWidth - gap), content.Height);
			Sidebar = new Rectangle(Main.Right + gap, content.Y, sidebarWidth, content.Height);

			var tabAvailableWidth = TabBar.Width - gap;
			var firstTabWidth = tabAvailableWidth / 2;
			MixerTabButton = new Rectangle(TabBar.X, TabBar.Y, firstTabWidth, TabBar.Height);
			PaletteTabButton = Rectangle.FromLTRB(
				MixerTabButton.Right + gap, TabBar.Top, TabBar.Right, TabBar.Bottom);

			StoreButton = new Rectangle(
				Sidebar.X, Sidebar.Bottom - MinimumTarget, Sidebar.Width, MinimumTarget);
			RandomButton = new Rectangle(
				Sidebar.X,
				Math.Max(Sidebar.Y, StoreButton.Y - gap - MinimumTarget),
				Sidebar.Width,
				MinimumTarget);
			Preview = Rectangle.FromLTRB(
				Sidebar.Left,
				Sidebar.Top,
				Sidebar.Right,
				Math.Max(Sidebar.Top, RandomButton.Top - gap));

			MixerTab = Main;
			var hueBackgroundHeight = MinimumTarget + 2 * border;
			HueBackground = new Rectangle(0, 0, Main.Width, hueBackgroundHeight);
			HueSlider = new Rectangle(border, border,
				Math.Max(1, HueBackground.Width - 2 * border), MinimumTarget);
			MixerBackground = new Rectangle(
				0,
				HueBackground.Bottom + gap,
				Main.Width,
				Math.Max(1, Main.Height - HueBackground.Height - gap));
			Mixer = IosTouchDialogLayout.Inset(
				new Rectangle(0, 0, MixerBackground.Width, MixerBackground.Height), border);

			PaletteTab = Main;
			PalettePanel = new Rectangle(0, 0, Main.Width, Main.Height);
			var headerHeight = IosTouchDialogLayout.Point(snapshot, isPhone ? 26 : 30);
			var paletteGap = IosTouchDialogLayout.Point(snapshot, isPhone ? 4 : 6);
			var totalRows = presetRows + customRows;
			var availableSwatchHeight = Math.Max(1,
				PalettePanel.Height - 2 * headerHeight - 3 * paletteGap);
			swatchSize = Math.Max(1, Math.Min(
				PalettePanel.Width / columns,
				availableSwatchHeight / totalRows));
			var areaWidth = swatchSize * columns;
			var areaX = (PalettePanel.Width - areaWidth) / 2;

			PresetHeader = new Rectangle(0, 0, PalettePanel.Width, headerHeight);
			PresetArea = new Rectangle(
				areaX,
				PresetHeader.Bottom + paletteGap,
				areaWidth,
				swatchSize * presetRows);
			CustomHeader = new Rectangle(
				0,
				PresetArea.Bottom + paletteGap,
				PalettePanel.Width,
				headerHeight);
			CustomArea = new Rectangle(
				areaX,
				CustomHeader.Bottom + paletteGap,
				areaWidth,
				swatchSize * customRows);
		}

		public Rectangle PresetSwatchBounds(int column, int row)
		{
			ValidateSwatch(column, row, presetRows);
			return new Rectangle(column * swatchSize, row * swatchSize, swatchSize, swatchSize);
		}

		public Rectangle CustomSwatchBounds(int column, int row)
		{
			ValidateSwatch(column, row, customRows);
			return new Rectangle(column * swatchSize, row * swatchSize, swatchSize, swatchSize);
		}

		void ValidateSwatch(int column, int row, int rows)
		{
			if (column < 0 || column >= columns)
				throw new ArgumentOutOfRangeException(nameof(column));
			if (row < 0 || row >= rows)
				throw new ArgumentOutOfRangeException(nameof(row));
		}
	}
}
