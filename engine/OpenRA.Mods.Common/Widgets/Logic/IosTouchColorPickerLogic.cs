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
using System.Collections.Generic;
using System.Linq;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class IosTouchColorPickerLogic : ChromeLogic
	{
		readonly Widget widget;
		readonly int columns;
		readonly int presetRows;
		readonly int customRows;
		Rectangle lastSafeBounds;
		Size lastResolution;

		[ObjectCreator.UseCtor]
		public IosTouchColorPickerLogic(Widget widget, Dictionary<string, MiniYaml> logicArgs)
		{
			this.widget = widget;
			columns = Value(logicArgs, "PaletteColumns", 8);
			presetRows = Value(logicArgs, "PalettePresetRows", 2);
			customRows = Value(logicArgs, "PaletteCustomRows", 1);
			ApplyLayout();
		}

		public override void Tick()
		{
			if (!Platform.IsIOS)
				return;

			var resolution = Game.Renderer.Resolution;
			var safeBounds = IosScreenMetrics.SnapshotFor(resolution).SafeBounds;
			if (resolution != lastResolution || safeBounds != lastSafeBounds)
				ApplyLayout();
		}

		void ApplyLayout()
		{
			if (!Platform.IsIOS)
				return;

			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			var layout = IosTouchDialogLayout.CreateColorPicker(snapshot, columns, presetRows, customRows);
			widget.Bounds = Bounds(layout.Panel);

			var randomButton = widget.Get<ButtonWidget>("RANDOM_BUTTON");
			randomButton.Bounds = Bounds(layout.RandomButton);
			randomButton.Font = Font("IosBold", "Bold");
			var storeButton = widget.Get<ButtonWidget>("STORE_BUTTON");
			storeButton.Bounds = Bounds(layout.StoreButton);
			storeButton.Font = Font("IosBold", "Bold");
			widget.Get<ActorPreviewWidget>("PREVIEW").Bounds = Bounds(layout.Preview);

			var mixerTabButton = widget.Get<ButtonWidget>("MIXER_TAB_BUTTON");
			mixerTabButton.Bounds = Bounds(layout.MixerTabButton);
			mixerTabButton.Font = Font("IosBold", "Bold");
			var paletteTabButton = widget.Get<ButtonWidget>("PALETTE_TAB_BUTTON");
			paletteTabButton.Bounds = Bounds(layout.PaletteTabButton);
			paletteTabButton.Font = Font("IosBold", "Bold");

			var mixerTab = widget.Get("MIXER_TAB");
			mixerTab.Bounds = Bounds(layout.MixerTab);
			var hueBackground = mixerTab.Get("HUEBG");
			hueBackground.Bounds = Bounds(layout.HueBackground);
			var hueSlider = hueBackground.Get<HueSliderWidget>("HUE_SLIDER");
			hueSlider.Bounds = Bounds(layout.HueSlider);
			hueSlider.UseTouchStepControls = false;
			var mixerBackground = mixerTab.Get("MIXERBG");
			mixerBackground.Bounds = Bounds(layout.MixerBackground);
			mixerBackground.Get<ColorMixerWidget>("MIXER").Bounds = Bounds(layout.Mixer);

			var paletteTab = widget.Get("PALETTE_TAB");
			paletteTab.Bounds = Bounds(layout.PaletteTab);
			var palettePanel = paletteTab.Get("PALETTE_TAB_PANEL");
			palettePanel.Bounds = Bounds(layout.PalettePanel);
			var presetHeader = palettePanel.Get("PRESET_HEADER");
			presetHeader.Bounds = Bounds(layout.PresetHeader);
			LayoutHeader(presetHeader);
			var presetArea = palettePanel.Get<ContainerWidget>("PRESET_AREA");
			presetArea.Bounds = Bounds(layout.PresetArea);
			LayoutSwatches(presetArea, "COLORPRESET", columns, presetRows, layout.PresetSwatchBounds);

			var customHeader = palettePanel.Get("CUSTOM_HEADER");
			customHeader.Bounds = Bounds(layout.CustomHeader);
			LayoutHeader(customHeader);
			var customArea = palettePanel.Get<ContainerWidget>("CUSTOM_AREA");
			customArea.Bounds = Bounds(layout.CustomArea);
			LayoutSwatches(customArea, "COLORCUSTOM", columns, customRows, layout.CustomSwatchBounds);

			lastResolution = resolution;
			lastSafeBounds = snapshot.SafeBounds;
		}

		static void LayoutHeader(Widget header)
		{
			var label = header.Get<LabelWidget>("LABEL");
			label.Bounds = new WidgetBounds(0, 0, header.Bounds.Width, header.Bounds.Height);
			label.Font = Font("IosBold", "TinyBold");
		}

		static void LayoutSwatches(
			ContainerWidget area, string templateId, int columns, int rows,
			Func<int, int, Rectangle> swatchBounds)
		{
			var all = area.Children.OfType<ColorBlockWidget>().Where(swatch => swatch.Id == templateId).ToArray();
			if (all.Length == 0)
				return;

			all[0].Bounds = Bounds(swatchBounds(0, 0));
			for (var index = 1; index < all.Length; index++)
			{
				var swatchIndex = index - 1;
				var row = swatchIndex / columns;
				if (row >= rows)
					break;

				all[index].Bounds = Bounds(swatchBounds(swatchIndex % columns, row));
			}
		}

		static int Value(Dictionary<string, MiniYaml> args, string key, int fallback)
		{
			if (args == null || !args.TryGetValue(key, out var yaml))
				return fallback;
			if (!int.TryParse(yaml.Value, out var value) || value <= 0)
				throw new YamlException($"Invalid value for {key}: {yaml.Value}");

			return value;
		}

		static string Font(string iosFont, string fallback) =>
			Game.Renderer.Fonts.ContainsKey(iosFont) ? iosFont : fallback;

		static WidgetBounds Bounds(Rectangle rectangle) =>
			new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
	}
}
