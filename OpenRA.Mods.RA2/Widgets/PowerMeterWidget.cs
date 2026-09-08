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
using System.Globalization;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets.Logic
{
	public class PowerMeterWidget : Widget
	{
		[FluentReference("usage", "capacity")]
		const string PowerUsage = "label-power-usage";

		[FluentReference]
		const string Infinite = "label-infinite-power";

		Widget sidebarProduction;
		int lastMeterCheck;
		int barHeight;
		bool bypassAnimation;
		int warningFlash;
		int lastTotalPowerDisplay;
		int barWidth = 12;

		readonly Lazy<TooltipContainerWidget> tooltipContainer;
		readonly CachedTransform<(int, string), string> tooltipTextCached;

		protected readonly World World;

		[Desc("The name of the Container Widget to tie the Y axis to")]
		[FieldLoader.Require]
		public readonly string MeterAlongside = "";

		[Desc("The name of the Container with the items to get the height from")]
		[FieldLoader.Require]
		public readonly string ParentContainer = "";

		[Desc("Height of each meter bar")]
		[FieldLoader.Require]
		public readonly int MeterHeight = 3;

		[Desc("How many units of power each bar represents")]
		[FieldLoader.Require]
		public readonly int PowerUnitsPerBar = 25;

		[Desc("How many Ticks to wait before animating the bar")]
		[FieldLoader.Require]
		public readonly int TickWait = 4;

		[Desc("Blank Image for the meter bar")]
		[FieldLoader.Require]
		public readonly string NoPowerImage = "";

		[Desc("When you have access power to use")]
		[FieldLoader.Require]
		public readonly string AvailablePowerImage = "";

		[Desc("Used power image")]
		[FieldLoader.Require]
		public readonly string UsedPowerImage = "";

		[Desc("Too much power used meter image")]
		[FieldLoader.Require]
		public readonly string OverUsedPowerImage = "";

		[Desc("Flash image for the top bar")]
		[FieldLoader.Require]
		public readonly string FlashPowerImage = "";

		[Desc("The collection of images to get the meter images from")]
		[FieldLoader.Require]
		public readonly string ImageCollection = "";

		public readonly string TooltipTemplate = "SIMPLE_TOOLTIP";
		public readonly string TooltipContainer;

		[ObjectCreator.UseCtor]
		public PowerMeterWidget(World world)
		{
			World = world;
			tooltipContainer = Exts.Lazy(() =>
				Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));

			var infinite = FluentProvider.GetMessage(Infinite);
			tooltipTextCached = new CachedTransform<(int, string), string>(args =>
				FluentProvider.GetMessage(PowerUsage,
					"usage", args.Item1.ToString(NumberFormatInfo.CurrentInfo),
					"capacity", args.Item2 ?? infinite));
		}

		public void CalculateMeterBarDimensions()
		{
			// Height of power meter in pixels
			var newBarHeight = 0;
			foreach (var child in sidebarProduction.Children)
				if (child.Id == MeterAlongside)
					newBarHeight += child.Bounds.Height;

			if (newBarHeight != barHeight)
			{
				barHeight = newBarHeight;

				// Don't animate the meter after changing sidebars
				bypassAnimation = true;
			}

			if (barHeight > 0 && (Bounds.Height != barHeight || Bounds.Width != barWidth))
				Bounds = new WidgetBounds(Bounds.X, Bounds.Y, barWidth, barHeight);
		}

		public Widget GetSidebar()
		{
			if (Parent == null)
				return null;

			if (sidebarProduction != null)
				return sidebarProduction;

			sidebarProduction = Parent.GetOrNull(ParentContainer);
			return sidebarProduction;
		}

		public void CheckBarNumber()
		{
			var meterDistance = MeterHeight;
			var numberOfBars = (int)decimal.Floor(barHeight / meterDistance);

			if (Children.Count == numberOfBars)
				return;

			Children.Clear();

			var sprite = ChromeProvider.GetImage(ImageCollection, NoPowerImage);
			if (sprite != null)
				barWidth = Math.Max(12, (int)sprite.Size.X);

			// Create a list of new bars (local coordinates relative to this widget)
			for (var i = 0; i < numberOfBars; i++)
			{
				var newPower = new ImageWidget
				{
					ImageCollection = ImageCollection,
					ImageName = NoPowerImage,
					IgnoreMouseOver = true,
					ClickThrough = true,
				};

				newPower.Bounds = new WidgetBounds(0, barHeight - (i + 1) * meterDistance, barWidth, meterDistance);
				newPower.GetImageName = () => newPower.ImageName;
				AddChild(newPower);
			}

			Bounds = new WidgetBounds(Bounds.X, Bounds.Y, barWidth, barHeight);
		}

		public void CheckFlash(PowerManager powerManager, int totalPowerDisplay)
		{
			var startWarningFlash = powerManager.PowerState != PowerState.Normal;

			if (lastTotalPowerDisplay != totalPowerDisplay)
			{
				startWarningFlash = true;
				lastTotalPowerDisplay = totalPowerDisplay;
			}

			if (startWarningFlash && warningFlash <= 0)
				warningFlash = 10;
		}

		string GetPowerTooltipText()
		{
			if (World.LocalPlayer == null)
				return null;

			var powerManager = World.LocalPlayer.PlayerActor.Trait<PowerManager>();
			var developerMode = World.LocalPlayer.PlayerActor.Trait<DeveloperMode>();
			var capacity = developerMode.UnlimitedPower
				? null
				: powerManager.PowerProvided.ToString(NumberFormatInfo.CurrentInfo);

			return tooltipTextCached.Update((powerManager.PowerDrained, capacity));
		}

		public override void MouseEntered()
		{
			if (string.IsNullOrEmpty(TooltipContainer))
				return;

			tooltipContainer.Value.SetTooltip(TooltipTemplate,
				new WidgetArgs() { { "getText", (Func<string>)GetPowerTooltipText }, { "world", World } });
		}

		public override void MouseExited()
		{
			if (string.IsNullOrEmpty(TooltipContainer))
				return;

			tooltipContainer.Value.RemoveTooltip();
		}

		public override void Tick()
		{
			if (GetSidebar() == null)
				return;

			CalculateMeterBarDimensions();
			CheckBarNumber();

			// If just changed power level or low power, flash the last bar meter
			lastMeterCheck++;
			if (lastMeterCheck < TickWait)
				return;

			lastMeterCheck = 0;

			// Number of power units represent each bar
			var stepSize = PowerUnitsPerBar;

			var powerManager = World.LocalPlayer.PlayerActor.Trait<PowerManager>();
			var totalPowerDisplay = Math.Max(powerManager.PowerProvided, powerManager.PowerDrained);

			var totalPowerStep = decimal.Floor(totalPowerDisplay / stepSize);
			var powerUsedStep = decimal.Floor(powerManager.PowerDrained / stepSize);
			var powerAvailableStep = decimal.Floor(powerManager.PowerProvided / stepSize);

			// Display a percentage if the bar is maxed out
			if (totalPowerStep > Children.Count)
			{
				var powerFraction = Children.Count / (float)totalPowerStep;
				totalPowerDisplay = (int)(totalPowerDisplay * powerFraction);
				totalPowerStep = (int)((float)totalPowerStep * powerFraction);
				powerUsedStep = (int)((float)powerUsedStep * powerFraction);
				powerAvailableStep = (int)((float)powerAvailableStep * powerFraction);
			}

			CheckFlash(powerManager, totalPowerDisplay);

			for (var i = 0; i < Children.Count; i++)
			{
				if (Children[i] is not ImageWidget image)
					continue;

				if (i > totalPowerStep || totalPowerStep == 0)
				{
					image.ImageName = NoPowerImage;
					continue;
				}

				var targetIcon = AvailablePowerImage;

				if (i < powerUsedStep)
					targetIcon = UsedPowerImage;

				if (i > powerAvailableStep)
					targetIcon = OverUsedPowerImage;

				if (i == totalPowerStep && powerManager.PowerState == PowerState.Low)
					targetIcon = OverUsedPowerImage;

				// Flash the top bar if something is wrong
				if (i == totalPowerStep)
				{
					if (warningFlash % 2 != 0)
						targetIcon = FlashPowerImage;
					if (warningFlash > 0)
						warningFlash--;
				}

				// We exit if updating a bar meter. This gives a nice animation effect
				if (image.ImageName != targetIcon)
				{
					image.ImageName = targetIcon;
					if (!bypassAnimation)
						return;
				}
			}

			bypassAnimation = false;
		}
	}
}
