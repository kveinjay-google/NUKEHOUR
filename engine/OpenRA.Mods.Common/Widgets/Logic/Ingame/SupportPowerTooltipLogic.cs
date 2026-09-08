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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets.Logic.Ingame;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class SupportPowerTooltipLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public SupportPowerTooltipLogic(Widget widget, TooltipContainerWidget tooltipContainer,
			Func<SupportPowersWidget.SupportPowerIcon> getTooltipIcon, World world)
		{
			widget.IsVisible = () => getTooltipIcon() != null && getTooltipIcon().Power.Info != null;
			var nameLabel = widget.Get<LabelWidget>("NAME");
			var hotkeyLabel = widget.Get<LabelWidget>("HOTKEY");
			var timeLabel = widget.Get<LabelWidget>("TIME");
			var descLabel = widget.Get<LabelWidget>("DESC");
			var nameFont = Game.Renderer.Fonts[nameLabel.Font];
			var hotkeyFont = Game.Renderer.Fonts[hotkeyLabel.Font];
			var timeFont = Game.Renderer.Fonts[timeLabel.Font];
			var descFont = Game.Renderer.Fonts[descLabel.Font];
			var baseHeight = widget.Bounds.Height;
			var baseDescBounds = descLabel.Bounds;
			var timeOffset = timeLabel.Bounds.X;

			SupportPowerInstance lastPower = null;
			var lastHotkey = Hotkey.Invalid;
			var lastRemainingSeconds = 0;
			var lastResolution = default(Size);
			var lastNativePointSize = default(Size);
			var lastSafeBounds = Rectangle.Empty;
			var lastIosLayout = false;

			tooltipContainer.BeforeRender = () =>
			{
				var icon = getTooltipIcon();
				if (icon == null || icon.Power == null || icon.Power.Instances.Count == 0)
					return;

				var sp = icon.Power;

				// HACK: This abuses knowledge of the internals of WidgetUtils.FormatTime
				// to efficiently work when the label is going to change, requiring a panel relayout
				var remainingSeconds = (int)Math.Ceiling(sp.RemainingTicks * world.Timestep / 1000f);

				var hotkey = icon.Hotkey?.GetValue() ?? Hotkey.Invalid;
				var resolution = Game.Renderer.Resolution;
				var iosLayout = Platform.IsIOS;
				var snapshot = iosLayout ? IosScreenMetrics.SnapshotFor(resolution) : default;
				var layoutChanged = iosLayout && (!lastIosLayout || resolution != lastResolution ||
					snapshot.NativePointSize != lastNativePointSize || snapshot.SafeBounds != lastSafeBounds);
				if (sp == lastPower && hotkey == lastHotkey && lastRemainingSeconds == remainingSeconds &&
					!layoutChanged)
					return;

				var description = sp.Description;
				Size maximumSize = default;
				var maximumContentWidth = 0;
				if (iosLayout)
				{
					maximumSize = IosSupportPowerTooltipPolicy.MaximumSize(snapshot);
					maximumContentWidth = Math.Max(1, maximumSize.Width - 2 * nameLabel.Bounds.X);
					var descriptionHeight = Math.Max(1, maximumSize.Height - baseHeight);
					description = IosSupportPowerTooltipPolicy.FitText(
						sp.Description,
						maximumContentWidth,
						descriptionHeight,
						text =>
						{
							var measured = descFont.Measure(text);
							return new Size(measured.X, measured.Y);
						});
					descLabel.Bounds.Width = maximumContentWidth;
				}
				else
					descLabel.Bounds = baseDescBounds;

				descLabel.GetText = () => description;
				var descSize = descFont.Measure(description);
				if (iosLayout)
					descLabel.Bounds.Height = Math.Min(
						Math.Max(1, maximumSize.Height - baseHeight), descSize.Y);

				var timeText = sp.TooltipTimeTextOverride();
				if (timeText == null)
				{
					var remaining = WidgetUtils.FormatTime(sp.RemainingTicks, world.Timestep);
					var total = WidgetUtils.FormatTime(sp.Info.ChargeInterval, world.Timestep);
					timeText = $"{remaining} / {total}";
				}

				timeLabel.GetText = () => timeText;
				var nameText = sp.Name;
				var hotkeyText = hotkey.IsValid() ? $"({hotkey.DisplayString()})" : string.Empty;
				if (iosLayout)
				{
					var gap = nameLabel.Bounds.X;
					var minimumNameWidth = Math.Min(
						maximumContentWidth, nameFont.Measure("…").X);
					var maximumTimeWidth = Math.Max(
						0, maximumContentWidth - minimumNameWidth - gap);
					timeText = IosSupportPowerTooltipPolicy.FitSingleLine(
						timeText, maximumTimeWidth, text =>
						{
							var measured = timeFont.Measure(text);
							return new Size(measured.X, measured.Y);
						});

					var timeWidth = timeFont.Measure(timeText).X;
					var remaining = maximumContentWidth - timeWidth -
						(timeText.Length > 0 ? gap : 0);
					var maximumHotkeyWidth = Math.Max(
						0, remaining - minimumNameWidth - gap);
					hotkeyText = IosSupportPowerTooltipPolicy.FitSingleLine(
						hotkeyText, maximumHotkeyWidth, text =>
						{
							var measured = hotkeyFont.Measure(text);
							return new Size(measured.X, measured.Y);
						});

					var hotkeyWidth = hotkeyFont.Measure(hotkeyText).X;
					remaining -= hotkeyWidth + (hotkeyText.Length > 0 ? gap : 0);
					nameText = IosSupportPowerTooltipPolicy.FitSingleLine(
						nameText, Math.Max(1, remaining), text =>
						{
							var measured = nameFont.Measure(text);
							return new Size(measured.X, measured.Y);
						});
				}

				nameLabel.GetText = () => nameText;
				var nameSize = nameFont.Measure(nameText);
				timeLabel.GetText = () => timeText;
				var timeSize = timeFont.Measure(timeText);
				hotkeyLabel.Visible = hotkey.IsValid() && hotkeyText.Length > 0;
				var hotkeySize = hotkeyLabel.Visible ? hotkeyFont.Measure(hotkeyText) : int2.Zero;
				if (hotkeyLabel.Visible)
				{
					hotkeyLabel.GetText = () => hotkeyText;
					hotkeyLabel.Bounds.X = nameSize.X + 2 * nameLabel.Bounds.X;
				}

				var topWidth = iosLayout
					? nameSize.X + hotkeySize.X + timeSize.X +
						(hotkeyLabel.Visible ? nameLabel.Bounds.X : 0) +
						(timeText.Length > 0 ? nameLabel.Bounds.X : 0)
					: nameSize.X + (hotkeyLabel.Visible ? hotkeySize.X + 2 * nameLabel.Bounds.X : 0) +
						timeSize.X + timeOffset;
				var desiredWidth = 2 * nameLabel.Bounds.X + Math.Max(topWidth, descSize.X);
				var desiredHeight = baseHeight + descSize.Y;
				if (iosLayout)
				{
					widget.Bounds.Width = Math.Min(maximumSize.Width, desiredWidth);
					widget.Bounds.Height = Math.Min(maximumSize.Height, desiredHeight);
					timeLabel.Bounds.X = Math.Max(
						nameLabel.Bounds.X, widget.Bounds.Width - nameLabel.Bounds.X - timeSize.X);
				}
				else
				{
					widget.Bounds.Width = desiredWidth;
					widget.Bounds.Height = desiredHeight;
					timeLabel.Bounds.X = widget.Bounds.Width - nameLabel.Bounds.X - timeSize.X;
				}

				lastPower = sp;
				lastHotkey = hotkey;
				lastRemainingSeconds = remainingSeconds;
				lastIosLayout = iosLayout;
				if (iosLayout)
				{
					lastResolution = resolution;
					lastNativePointSize = snapshot.NativePointSize;
					lastSafeBounds = snapshot.SafeBounds;
				}
			};

			timeLabel.GetColor = () => getTooltipIcon() != null && !getTooltipIcon().Power.Active
				? Color.Red : Color.White;
		}
	}
}
