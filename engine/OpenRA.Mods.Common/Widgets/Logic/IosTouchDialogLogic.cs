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
	public sealed class IosTouchDialogLogic : ChromeLogic
	{
		readonly Widget widget;
		Rectangle lastSafeBounds;
		Size lastResolution;
		int lastChildCount = -1;

		[ObjectCreator.UseCtor]
		public IosTouchDialogLogic(Widget widget)
		{
			this.widget = widget;
			ApplyLayout();
		}

		public override void Tick()
		{
			if (!Platform.IsIOS)
				return;

			var resolution = Game.Renderer.Resolution;
			var safeBounds = IosScreenMetrics.SnapshotFor(resolution).SafeBounds;
			if (resolution != lastResolution || safeBounds != lastSafeBounds || widget.Children.Count != lastChildCount)
				ApplyLayout();
		}

		public static void Relayout(Widget widget)
		{
			if (!Platform.IsIOS)
				return;

			var snapshot = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
			if (widget.Id == "TEXT_INPUT_PROMPT")
				LayoutTextInput(widget, snapshot);
			else
				LayoutButtonPrompt(widget, snapshot);
		}

		void ApplyLayout()
		{
			if (!Platform.IsIOS)
				return;

			Relayout(widget);
			lastResolution = Game.Renderer.Resolution;
			lastSafeBounds = IosScreenMetrics.SnapshotFor(lastResolution).SafeBounds;
			lastChildCount = widget.Children.Count;
		}

		static void LayoutButtonPrompt(Widget prompt, IosScreenSnapshot snapshot)
		{
			var promptLabels = prompt.Children
				.OfType<LabelWidget>()
				.Where(label => label.Id == "PROMPT_TEXT")
				.ToArray();
			var contentLabels = promptLabels.Length > 1 ? promptLabels.Skip(1).ToArray() : promptLabels;
			var buttons = ActiveButtons(prompt);
			var layout = IosTouchDialogLayout.CreateConfirmation(
				snapshot, Math.Max(1, contentLabels.Length), Math.Max(1, buttons.Count), false);

			prompt.Bounds = Bounds(layout.Panel);
			var title = prompt.Get<LabelWidget>("PROMPT_TITLE");
			title.Bounds = Bounds(layout.Title);
			title.Font = Font("IosTitle", "Bold");

			for (var i = 0; i < contentLabels.Length; i++)
			{
				contentLabels[i].Bounds = Bounds(layout.PromptLineBounds(i));
				contentLabels[i].Font = Font("IosRegular", "Regular");
				contentLabels[i].WordWrap = true;
			}

			for (var i = 0; i < buttons.Count; i++)
			{
				buttons[i].Bounds = Bounds(layout.ButtonBounds(i));
				buttons[i].Font = Font("IosBold", "Bold");
			}
		}

		static void LayoutTextInput(Widget prompt, IosScreenSnapshot snapshot)
		{
			var buttons = new List<ButtonWidget>
			{
				prompt.Get<ButtonWidget>("ACCEPT_BUTTON"),
				prompt.Get<ButtonWidget>("CANCEL_BUTTON")
			};
			var layout = IosTouchDialogLayout.CreateConfirmation(snapshot, 1, buttons.Count, true);

			prompt.Bounds = Bounds(layout.Panel);
			var title = prompt.Get<LabelWidget>("PROMPT_TITLE");
			title.Bounds = Bounds(layout.Title);
			title.Font = Font("IosTitle", "Bold");

			var promptText = prompt.Get<LabelWidget>("PROMPT_TEXT");
			promptText.Bounds = Bounds(layout.PromptText);
			promptText.Font = Font("IosRegular", "Regular");
			promptText.WordWrap = true;

			var input = prompt.Get<TextFieldWidget>("INPUT_TEXT");
			input.Bounds = Bounds(layout.TextInput);
			input.Font = Font("IosRegular", input.Font);

			for (var i = 0; i < buttons.Count; i++)
			{
				buttons[i].Bounds = Bounds(layout.ButtonBounds(i));
				buttons[i].Font = Font("IosBold", "Bold");
			}
		}

		static List<ButtonWidget> ActiveButtons(Widget prompt)
		{
			var ids = prompt.Id == "THREEBUTTON_PROMPT"
				? new[] { "CONFIRM_BUTTON", "OTHER_BUTTON", "CANCEL_BUTTON" }
				: new[] { "CONFIRM_BUTTON", "CANCEL_BUTTON" };
			var candidates = ids
				.Select(prompt.GetOrNull<ButtonWidget>)
				.Where(button => button != null)
				.ToList();
			var visible = candidates.Where(button => button.IsVisible()).ToList();
			return visible.Count > 0 ? visible : candidates;
		}

		static string Font(string iosFont, string fallback) =>
			Game.Renderer.Fonts.ContainsKey(iosFont) ? iosFont : fallback;

		static WidgetBounds Bounds(Rectangle rectangle) =>
			new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
	}
}
