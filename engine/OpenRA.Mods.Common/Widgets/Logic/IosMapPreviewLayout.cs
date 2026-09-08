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
using System.Runtime.CompilerServices;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class IosMapPreviewLayout
	{
		public WidgetBounds Window { get; }
		public WidgetBounds MapSurface { get; }
		public WidgetBounds MapPreview { get; }
		public WidgetBounds TextColumn { get; }
		public WidgetBounds Title { get; }
		public WidgetBounds PrimaryText { get; }
		public WidgetBounds SecondaryText { get; }
		public WidgetBounds StatusText { get; }
		public WidgetBounds SecondaryStatusText { get; }
		public WidgetBounds Progress { get; }
		public WidgetBounds SingleAction { get; }
		public WidgetBounds DualPrimaryAction { get; }
		public WidgetBounds DualSecondaryAction { get; }

		IosMapPreviewLayout(int width, int height, IosMenuLayoutPolicy policy, bool small)
		{
			width = Math.Max(1, width);
			height = Math.Max(1, height);
			Window = new WidgetBounds(0, 0, width, height);
			var textHeight = Math.Min(policy.MinimumReadableTextHeight, height);
			var actionHeight = Math.Min(policy.MinimumTarget, height);
			var statusHeight = Math.Min(2 * policy.MinimumReadableTextHeight, height);

			if (policy.IsPhone && small)
			{
				var minimumTextWidth = 3 * policy.MinimumTarget + policy.Gap;
				var availableMapWidth = Math.Max(1, width - policy.Gap - minimumTextWidth);
				var mapSize = Math.Min(height, Math.Max(policy.MinimumTarget, availableMapWidth));
				mapSize = Math.Min(mapSize, Math.Max(1, width - policy.Gap - 1));
				MapSurface = new WidgetBounds(0, 0, mapSize, mapSize);
				MapPreview = Inset(MapSurface);
				var textX = Math.Min(width, MapSurface.Right + policy.Gap);
				TextColumn = new WidgetBounds(textX, 0, Math.Max(1, width - textX), height);

				Title = new WidgetBounds(textX, 0, TextColumn.Width, textHeight);
				var primaryY = Math.Min(height - textHeight, Title.Bottom + policy.Gap);
				PrimaryText = new WidgetBounds(textX, Math.Max(0, primaryY), TextColumn.Width, textHeight);
				var secondaryY = Math.Min(height - textHeight, PrimaryText.Bottom + policy.Gap);
				SecondaryText = new WidgetBounds(textX, Math.Max(0, secondaryY), TextColumn.Width, textHeight);
				StatusText = new WidgetBounds(
					textX, Math.Min(height - statusHeight, Title.Bottom), TextColumn.Width, statusHeight);
				var primaryActionY = Math.Max(0, height - actionHeight);
				SingleAction = new WidgetBounds(
					textX, primaryActionY, TextColumn.Width, actionHeight);
				DualPrimaryAction = SingleAction;
				var secondaryActionY = Math.Max(0,
					primaryActionY - policy.Gap - actionHeight);
				DualSecondaryAction = new WidgetBounds(
					textX, secondaryActionY, TextColumn.Width, actionHeight);
				var progressY = Math.Max(0, primaryActionY - policy.Gap - textHeight);
				Progress = new WidgetBounds(textX, progressY, TextColumn.Width, textHeight);
				SecondaryStatusText = new WidgetBounds(
					textX, Math.Min(height - statusHeight, StatusText.Bottom),
					TextColumn.Width, statusHeight);
				return;
			}

			var lowerHeight = small
				? textHeight + statusHeight + textHeight + 2 * actionHeight + 4 * policy.Gap
				: 3 * textHeight + 3 * policy.Gap;
			var mapHeight = Math.Max(policy.MinimumTarget, height - lowerHeight);
			mapHeight = Math.Min(mapHeight, Math.Max(1, height - policy.Gap - textHeight));
			MapSurface = new WidgetBounds(0, 0, width, mapHeight);
			MapPreview = Inset(MapSurface);
			var textY = Math.Min(height - textHeight, MapSurface.Bottom + policy.Gap);
			TextColumn = new WidgetBounds(0, textY, width, Math.Max(1, height - textY));
			Title = new WidgetBounds(0, textY, width, textHeight);
			var tabletPrimaryY = Math.Min(height - textHeight, Title.Bottom + policy.Gap);
			PrimaryText = new WidgetBounds(0, Math.Max(0, tabletPrimaryY), width, textHeight);
			var tabletSecondaryY = Math.Min(height - textHeight, PrimaryText.Bottom + policy.Gap);
			SecondaryText = new WidgetBounds(0, Math.Max(0, tabletSecondaryY), width, textHeight);
			StatusText = new WidgetBounds(0, Math.Max(0, tabletPrimaryY), width, statusHeight);
			SecondaryStatusText = new WidgetBounds(
				0, Math.Min(height - statusHeight, StatusText.Bottom + policy.Gap), width, statusHeight);
			var tabletPrimaryActionY = Math.Max(0, height - actionHeight);
			SingleAction = new WidgetBounds(0, tabletPrimaryActionY, width, actionHeight);
			DualPrimaryAction = SingleAction;
			var tabletSecondaryActionY = Math.Max(0,
				tabletPrimaryActionY - policy.Gap - actionHeight);
			DualSecondaryAction = new WidgetBounds(0, tabletSecondaryActionY, width, actionHeight);
			var tabletProgressY = Math.Max(0,
				tabletSecondaryActionY - policy.Gap - textHeight);
			Progress = new WidgetBounds(0, tabletProgressY, width, textHeight);
		}

		public static IosMapPreviewLayout Create(
			int width, int height, IosMenuLayoutPolicy policy, bool small)
		{
			return new IosMapPreviewLayout(width, height, policy, small);
		}

		public static void Apply(Widget host, IosMenuLayoutPolicy policy)
		{
			if (host == null || !policy.Enabled ||
				host.Bounds.Width <= 0 || host.Bounds.Height <= 0)
				return;

			var preview = Direct(host, "MAP_PREVIEW");
			if (preview == null)
				return;

			var card = new WidgetBounds(0, 0, host.Bounds.Width, host.Bounds.Height);
			preview.Bounds = card;
			var large = Create(card.Width, card.Height, policy, false);
			var small = Create(card.Width, card.Height, policy, true);

			foreach (var id in new[]
			{
				"MAP_LARGE", "MAP_SMALL", "MAP_AVAILABLE", "MAP_INCOMPATIBLE", "MAP_VALIDATING",
				"MAP_DOWNLOAD_AVAILABLE", "MAP_UPDATE_DOWNLOAD_AVAILABLE", "MAP_UNAVAILABLE",
				"MAP_DOWNLOADING", "MAP_UPDATE_AVAILABLE"
			})
				SetBounds(preview, id, card);

			ApplyPreviewSection(Direct(preview, "MAP_LARGE"), large);
			ApplyPreviewSection(Direct(preview, "MAP_SMALL"), small);

			ApplyLabel(Direct(Direct(preview, "MAP_AVAILABLE"), "MAP_TYPE"), large.PrimaryText, false);
			ApplyLabel(Direct(Direct(preview, "MAP_AVAILABLE"), "MAP_AUTHOR"), large.SecondaryText, false);
			ApplyLabel(Direct(Direct(preview, "MAP_INCOMPATIBLE"), "MAP_STATUS_A"), large.PrimaryText, false);
			ApplyLabel(Direct(Direct(preview, "MAP_INCOMPATIBLE"), "MAP_STATUS_B"), large.SecondaryText, false);

			var validating = Direct(preview, "MAP_VALIDATING");
			ApplyLabel(Direct(validating, "MAP_STATUS_VALIDATING"), small.StatusText, true);
			SetBounds(validating, "MAP_VALIDATING_BAR", small.Progress);
			var downloading = Direct(preview, "MAP_DOWNLOADING");
			ApplyLabel(Direct(downloading, "MAP_STATUS_DOWNLOADING"), small.StatusText, true);
			SetBounds(downloading, "MAP_PROGRESSBAR", small.Progress);

			var download = Direct(preview, "MAP_DOWNLOAD_AVAILABLE");
			ApplyLabel(Direct(download, "MAP_TYPE"), small.PrimaryText, false);
			ApplyLabel(Direct(download, "MAP_AUTHOR"), small.SecondaryText, false);
			ApplyButton(Direct(download, "MAP_INSTALL"), small.SingleAction);
			ApplyButton(Direct(preview, "MAP_UPDATE"), small.SingleAction);
			ApplyButton(Direct(Direct(preview, "MAP_UPDATE_DOWNLOAD_AVAILABLE"), "MAP_INSTALL"),
				small.DualSecondaryAction);

			ApplyLabel(Direct(preview, "MAP_SEARCHING"), small.StatusText, true);
			ApplyLabel(Direct(preview, "MAP_ERROR"), small.StatusText, true);
			ApplyButton(Direct(preview, "MAP_RETRY"), small.SingleAction);

			var unavailable = Direct(preview, "MAP_UNAVAILABLE");
			ApplyLabel(Direct(unavailable, "a"), small.StatusText, true);
			ApplyLabel(Direct(unavailable, "b"), small.SecondaryStatusText, true);
			var updateAvailable = Direct(preview, "MAP_UPDATE_AVAILABLE");
			ApplyLabel(Direct(updateAvailable, "a"), small.StatusText, true);
			ApplyLabel(Direct(updateAvailable, "b"), small.SecondaryStatusText, true);
		}

		public static void ApplyActionState(
			Widget host, IosMenuLayoutPolicy policy, bool dualActions)
		{
			if (host == null || !policy.Enabled ||
				host.Bounds.Width <= 0 || host.Bounds.Height <= 0)
				return;

			var preview = Direct(host, "MAP_PREVIEW");
			if (preview == null)
				return;

			var layout = Create(host.Bounds.Width, host.Bounds.Height, policy, true);
			ApplyButton(Direct(preview, "MAP_UPDATE"),
				dualActions ? layout.DualPrimaryAction : layout.SingleAction);
			ApplyButton(Direct(Direct(preview, "MAP_UPDATE_DOWNLOAD_AVAILABLE"), "MAP_INSTALL"),
				layout.DualSecondaryAction);
		}

		static void ApplyPreviewSection(Widget section, IosMapPreviewLayout layout)
		{
			if (section == null)
				return;

			var background = Direct(section, "MAP_BG");
			if (background != null)
			{
				background.Bounds = layout.MapSurface;
				var nestedPreview = Direct(background, "MAP_PREVIEW");
				if (nestedPreview != null)
					nestedPreview.Bounds = layout.MapPreview;
			}

			ApplyLabel(Direct(section, "MAP_TITLE"), layout.Title, false);
		}

		static void ApplyLabel(Widget widget, WidgetBounds bounds, bool wordWrap)
		{
			if (widget == null)
				return;

			widget.Bounds = bounds;
			if (widget is LabelWidget label)
			{
				if (wordWrap)
					label.Font = "IosTouchLabel";

				label.WordWrap = wordWrap;
				label.VAlign = TextVAlign.Middle;
				IosResponsiveText.Configure(label, wordWrap);
			}
		}

		static void ApplyButton(Widget widget, WidgetBounds bounds)
		{
			if (widget == null)
				return;

			widget.Bounds = bounds;
			if (widget is ButtonWidget button)
				IosResponsiveText.Configure(button);
		}

		static Widget Direct(Widget parent, string id)
		{
			return parent?.Children.FirstOrDefault(child => child.Id == id);
		}

		static void SetBounds(Widget parent, string id, WidgetBounds bounds)
		{
			var child = Direct(parent, id);
			if (child != null)
				child.Bounds = bounds;
		}

		static WidgetBounds Inset(WidgetBounds bounds)
		{
			return new WidgetBounds(bounds.X + 1, bounds.Y + 1,
				Math.Max(1, bounds.Width - 2), Math.Max(1, bounds.Height - 2));
		}
	}

	public sealed class ResponsiveTextCache
	{
		readonly CachedTransform<(string Text, int Width, SpriteFont Font), string> cache;

		public ResponsiveTextCache(Action<string, string> onMeasured = null)
		{
			cache = new CachedTransform<(string Text, int Width, SpriteFont Font), string>(state =>
			{
				var text = state.Text ?? "";
				var output = WidgetUtils.TruncateText(text, Math.Max(1, state.Width), state.Font);
				onMeasured?.Invoke(text, output);
				return output;
			});
		}

		public static ResponsiveTextCache WithTooltip(LabelWithTooltipWidget label)
		{
			ArgumentNullException.ThrowIfNull(label);
			return new ResponsiveTextCache((text, output) =>
				label.GetTooltipText = text == output ? null : () => text);
		}

		public string Update(string text, int width, SpriteFont font)
		{
			return cache.Update((text ?? "", width, font));
		}
	}

	public static class IosResponsiveText
	{
		sealed class LabelState
		{
			public Func<string> Original;
			public Func<string> Wrapper;
			public bool TwoLines;
			readonly CachedTransform<(string Text, int Width, SpriteFont Font, bool TwoLines), string> cache;

			public LabelState(LabelWidget label, bool twoLines)
			{
				Original = label.GetText;
				TwoLines = twoLines;
				cache = new CachedTransform<
					(string Text, int Width, SpriteFont Font, bool TwoLines), string>(state =>
					Fit(state.Text, state.Width, state.Font, state.TwoLines));
				Wrapper = () => cache.Update((
					Original?.Invoke() ?? "", label.Bounds.Width, Game.Renderer.Fonts[label.Font], TwoLines));
			}
		}

		sealed class ButtonState
		{
			public Func<string> Original;
			public Func<string> Wrapper;
			readonly CachedTransform<(string Text, int Width, SpriteFont Font), string> cache;

			public ButtonState(ButtonWidget button)
			{
				Original = button.GetText;
				cache = new CachedTransform<(string Text, int Width, SpriteFont Font), string>(state =>
					WidgetUtils.TruncateText(state.Text, Math.Max(1, state.Width), state.Font));
				Wrapper = () => cache.Update((Original?.Invoke() ?? "",
					button.Bounds.Width - button.LeftMargin - button.RightMargin,
					Game.Renderer.Fonts[button.Font]));
			}
		}

		static readonly ConditionalWeakTable<LabelWidget, LabelState> LabelStates = new();
		static readonly ConditionalWeakTable<ButtonWidget, ButtonState> ButtonStates = new();

		public static void Configure(LabelWidget label, bool twoLines)
		{
			if (!LabelStates.TryGetValue(label, out var state))
			{
				state = new LabelState(label, twoLines);
				LabelStates.Add(label, state);
			}
			else if (label.GetText != state.Wrapper)
				state.Original = label.GetText;

			state.TwoLines = twoLines;
			label.GetText = state.Wrapper;
		}

		public static void Configure(ButtonWidget button)
		{
			if (!ButtonStates.TryGetValue(button, out var state))
			{
				state = new ButtonState(button);
				ButtonStates.Add(button, state);
			}
			else if (button.GetText != state.Wrapper)
				state.Original = button.GetText;

			button.GetText = state.Wrapper;
		}

		static string Fit(string text, int width, SpriteFont font, bool twoLines)
		{
			width = Math.Max(1, width);
			if (!twoLines)
				return WidgetUtils.TruncateText(text, width, font);

			if (font.Measure(text).X <= width)
				return text;

			var fallback = -1;
			for (var i = 1; i < text.Length; i++)
			{
				var first = text[..i].TrimEnd();
				var second = text[i..].TrimStart();
				if (first.Length == 0 || second.Length == 0 ||
					font.Measure(first).X > width || font.Measure(second).X > width)
					continue;

				fallback = i;
				if (char.IsWhiteSpace(text[i - 1]) || char.IsWhiteSpace(text[i]))
					return first + "\n" + second;
			}

			if (fallback > 0)
				return text[..fallback].TrimEnd() + "\n" + text[fallback..].TrimStart();

			return WidgetUtils.TruncateText(text, width, font);
		}
	}
}
