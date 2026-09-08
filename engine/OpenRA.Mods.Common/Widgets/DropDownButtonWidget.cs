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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class DropDownButtonWidget : ButtonWidget
	{
		public readonly string Decorations = "dropdown-decorations";
		public readonly string DecorationMarker = "marker";
		public readonly string Separators = "dropdown-separators";
		public readonly string SeparatorImage = "separator";
		public readonly TextAlign PanelAlign = TextAlign.Left;
		public string PanelRoot;

		Widget panel;
		MaskWidget fullscreenMask;
		Widget panelRoot;
		CachedTransform<(bool Disabled, bool Pressed, bool Hover, bool Focused, bool Highlighted), Sprite> getMarkerImage;
		CachedTransform<(bool Disabled, bool Pressed, bool Hover, bool Focused, bool Highlighted), Sprite> getSeparatorImage;

		[ObjectCreator.UseCtor]
		public DropDownButtonWidget(ModData modData)
			: base(modData) { }

		protected DropDownButtonWidget(DropDownButtonWidget widget)
			: base(widget)
		{
			PanelRoot = widget.PanelRoot;
			Decorations = widget.Decorations;
			DecorationMarker = widget.DecorationMarker;
			Separators = widget.Separators;
			SeparatorImage = widget.SeparatorImage;
		}

		public override void Draw()
		{
			base.Draw();
			var stateOffset = Depressed ? new int2(VisualHeight, VisualHeight) : new int2(0, 0);

			var rb = RenderBounds;
			var isDisabled = IsDisabled();
			var isHover = Ui.MouseOverWidget == this || Children.Any(c => c == Ui.MouseOverWidget);

			getMarkerImage ??= WidgetUtils.GetCachedStatefulImage(Decorations, DecorationMarker);

			var arrowImage = getMarkerImage.Update((isDisabled, Depressed, isHover, false, IsHighlighted()));
			WidgetUtils.DrawSprite(
				arrowImage,
				stateOffset + new float2(
					rb.Right - (int)((rb.Height + arrowImage.Size.X) / 2),
					rb.Top + (int)((rb.Height - arrowImage.Size.Y) / 2)));

			getSeparatorImage ??= WidgetUtils.GetCachedStatefulImage(Separators, SeparatorImage);

			var separatorImage = getSeparatorImage.Update((isDisabled, Depressed, isHover, false, IsHighlighted()));
			if (separatorImage != null)
				WidgetUtils.DrawSprite(
					separatorImage,
					stateOffset + new float2(-3, 0) + new float2(rb.Right - rb.Height + 4,
					rb.Top + (int)((rb.Height - separatorImage.Size.Y) / 2)));
		}

		public override Widget Clone() { return new DropDownButtonWidget(this); }

		// This is crap
		public override int UsableWidth => Bounds.Width - Bounds.Height; /* space for button */

		public override void Hidden()
		{
			base.Hidden();
			RemovePanel();
		}

		public override void Removed()
		{
			base.Removed();
			RemovePanel();
		}

		public void RemovePanel()
		{
			if (panel == null)
				return;

			panelRoot.RemoveChild(fullscreenMask);
			panelRoot.RemoveChild(panel);
			panel = fullscreenMask = null;

			Ui.ResetTooltips();
		}

		public void AttachPanel(Widget p) { AttachPanel(p, null); }
		public void AttachPanel(Widget p, Action onCancel)
		{
			if (panel != null)
				throw new InvalidOperationException("Attempted to attach a panel to an open dropdown");
			panel = p;

			// Mask to prevent any clicks from being sent to other widgets
			fullscreenMask = new MaskWidget
			{
				Bounds = new WidgetBounds(0, 0, Game.Renderer.Resolution.Width, Game.Renderer.Resolution.Height)
			};

			fullscreenMask.OnMouseDown += mi => { Game.Sound.PlayNotification(ModRules, null, "Sounds", ClickSound, null); RemovePanel(); };
			if (onCancel != null)
				fullscreenMask.OnMouseDown += _ => onCancel();

			panelRoot = PanelRoot == null ? Ui.Root : Ui.Root.Get(PanelRoot);

			panelRoot.AddChild(fullscreenMask);

			var oldBounds = panel.Bounds;
			var panelX = RenderOrigin.X - panelRoot.RenderOrigin.X;
			if (PanelAlign == TextAlign.Right)
				panelX += Bounds.Width - oldBounds.Width;
			else if (PanelAlign == TextAlign.Center)
				panelX += (Bounds.Width - oldBounds.Width) / 2;

			if (Platform.IsIOS)
			{
				var snapshot = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
				var absoluteBounds = IosTouchWidgetPolicy.PlacePopup(
					RenderBounds,
					panelX + panelRoot.RenderOrigin.X,
					oldBounds.Width,
					oldBounds.Height,
					snapshot.SafeBounds);
				var relativeBounds = IosTouchWidgetPolicy.RelativeToRoot(absoluteBounds, panelRoot.RenderOrigin);
				panel.Bounds = new WidgetBounds(
					relativeBounds.X,
					relativeBounds.Y,
					relativeBounds.Width,
					relativeBounds.Height);
			}
			else
			{
				var panelY = RenderOrigin.Y + Bounds.Height - panelRoot.RenderOrigin.Y;
				if (panelY + oldBounds.Height > Game.Renderer.Resolution.Height)
					panelY -= Bounds.Height + oldBounds.Height;

				panel.Bounds = new WidgetBounds(
					panelX,
					panelY,
					oldBounds.Width,
					oldBounds.Height);
			}

			panelRoot.AddChild(panel);

			(panel as ScrollPanelWidget)?.ScrollToSelectedItem();
		}

		public void ShowDropDown<T>(
			string panelTemplate, int maxHeight, IEnumerable<T> options, Func<T, ScrollItemWidget, ScrollItemWidget> setupItem)
		{
			var substitutions = new Dictionary<string, int>() { { "DROPDOWN_WIDTH", Bounds.Width } };
			var panel = (ScrollPanelWidget)Ui.LoadWidget(panelTemplate, null, new WidgetArgs() { { "substitutions", substitutions } });
			var snapshot = Platform.IsIOS ? IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution) : default;
			if (Platform.IsIOS)
				AdaptIosDropDownPanel(panel, snapshot);

			var itemTemplate = panel.Get<ScrollItemWidget>("TEMPLATE");
			panel.RemoveChildren();
			foreach (var option in options)
			{
				var o = option;

				var item = setupItem(o, itemTemplate);
				if (Platform.IsIOS)
					AdaptIosDropDownItem(item, panel, snapshot, false);

				var onClick = item.OnClick;
				item.OnClick = () => { onClick(); RemovePanel(); };

				panel.AddChild(item);
			}

			var effectiveMaxHeight = Platform.IsIOS ?
				IosTouchWidgetPolicy.MaximumPopupHeight(
					Math.Max(maxHeight, 2 * panel.ScrollbarWidth), snapshot) : maxHeight;
			panel.Bounds.Height = Platform.IsIOS ?
				IosTouchWidgetPolicy.DropDownPopupHeight(
					panel.ContentHeight, effectiveMaxHeight, panel.ScrollbarWidth) :
				Math.Min(effectiveMaxHeight, panel.ContentHeight);
			AttachPanel(panel);
		}

		public void ShowDropDown<T>(
			string panelTemplate, int height, Dictionary<string, IEnumerable<T>> groups, Func<T, ScrollItemWidget, ScrollItemWidget> setupItem)
		{
			ShowDropDown(panelTemplate, height, groups, setupItem, IosDropDownLayout.Default);
		}

		public void ShowDropDown<T>(
			string panelTemplate, int height, Dictionary<string, IEnumerable<T>> groups,
			Func<T, ScrollItemWidget, ScrollItemWidget> setupItem, IosDropDownLayout iosLayout)
		{
			var substitutions = new Dictionary<string, int>() { { "DROPDOWN_WIDTH", Bounds.Width } };
			var panel = (ScrollPanelWidget)Ui.LoadWidget(panelTemplate, null, new WidgetArgs() { { "substitutions", substitutions } });
			var snapshot = Platform.IsIOS ? IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution) : default;
			if (Platform.IsIOS)
				AdaptIosDropDownPanel(panel, snapshot, iosLayout);

			var headerTemplate = panel.GetOrNull<ScrollItemWidget>("HEADER");
			var itemTemplate = panel.Get<ScrollItemWidget>("TEMPLATE");
			panel.RemoveChildren();

			foreach (var kv in groups)
			{
				var group = kv.Key;
				var options = kv.Value.ToArray();
				if (group.Length > 0 && headerTemplate != null &&
					IosTouchWidgetPolicy.ShowDropDownGroupHeader(options.Length, Platform.IsIOS, iosLayout))
				{
					var header = ScrollItemWidget.Setup(headerTemplate, () => false, () => { });
					if (Platform.IsIOS)
						AdaptIosDropDownItem(header, panel, snapshot, true, iosLayout);

					header.Get<LabelWidget>("LABEL").GetText = () => group;
					panel.AddChild(header);
				}

				foreach (var option in options)
				{
					var o = option;

					var item = setupItem(o, itemTemplate);
					if (Platform.IsIOS)
						AdaptIosDropDownItem(item, panel, snapshot, false, iosLayout);

					var onClick = item.OnClick;
					item.OnClick = () => { onClick(); RemovePanel(); };

					panel.AddChild(item);
				}
			}

			var effectiveHeight = Platform.IsIOS ? IosTouchWidgetPolicy.DropDownMaximumHeight(
				Math.Max(height, 2 * panel.ScrollbarWidth), panel.ScrollbarWidth,
				snapshot.SafeBounds.Height, iosLayout.MaximumHeightTargets) : height;
			panel.Bounds.Height = Platform.IsIOS ?
				IosTouchWidgetPolicy.DropDownPopupHeight(
					panel.ContentHeight, effectiveHeight, panel.ScrollbarWidth) :
				Math.Min(effectiveHeight, panel.ContentHeight);
			AttachPanel(panel);
		}

		static void AdaptIosDropDownPanel(
			ScrollPanelWidget panel, IosScreenSnapshot snapshot,
			IosDropDownLayout iosLayout = default)
		{
			if (iosLayout.MinimumWidthTargets == 0)
				iosLayout = IosDropDownLayout.Default;

			var minimumTouchHeight = IosTouchWidgetPolicy.EnsureMinimumTouchHeight(0, snapshot);
			panel.Bounds.Width = IosTouchWidgetPolicy.DropDownPanelWidth(
				panel.Bounds.Width, minimumTouchHeight, snapshot.SafeBounds.Width,
				iosLayout.MinimumWidthTargets);
			panel.ScrollbarWidth = minimumTouchHeight;
			panel.EnableContentDragging = true;
			panel.ContentDragThreshold = IosTouchWidgetPolicy.ContentDragThreshold(snapshot.LogicalPerPoint);
			if (iosLayout.OmitSingletonHeaders)
				panel.TopBottomSpacing = 0;

			var headerTemplate = panel.GetOrNull<ScrollItemWidget>("HEADER");
			if (headerTemplate != null)
				AdaptIosDropDownItem(headerTemplate, panel, snapshot, true, iosLayout);

			var itemTemplate = panel.GetOrNull<ScrollItemWidget>("TEMPLATE");
			if (itemTemplate != null)
				AdaptIosDropDownItem(itemTemplate, panel, snapshot, false, iosLayout);
		}

		static void AdaptIosDropDownItem(
			ScrollItemWidget item, ScrollPanelWidget panel, IosScreenSnapshot snapshot, bool header,
			IosDropDownLayout iosLayout = default)
		{
			if (iosLayout.MinimumWidthTargets == 0)
				iosLayout = IosDropDownLayout.Default;

			var contentBounds = IosTouchWidgetPolicy.DropDownContentBounds(
				panel.Bounds.Width,
				panel.ScrollbarWidth,
				2);
			item.Bounds.X = contentBounds.X;
			item.Bounds.Width = contentBounds.Width;
			item.Bounds.Height = header && iosLayout.HeaderHeightPoints > 0 ?
				Math.Max(1, (int)Math.Ceiling(iosLayout.HeaderHeightPoints * snapshot.LogicalPerPoint)) :
				IosTouchWidgetPolicy.EnsureMinimumTouchHeight(item.Bounds.Height, snapshot);
			var font = header ? "IosBold" : "IosRegular";
			item.Font = font;
			var flag = !header && iosLayout.ItemImagePoints.Width > 0 ?
				item.Children.OfType<ImageWidget>().FirstOrDefault(child => child.Id == "FLAG") : null;
			var labelX = -1;
			if (flag != null)
			{
				var flagWidth = Math.Max(1,
					(int)Math.Ceiling(iosLayout.ItemImagePoints.Width * snapshot.LogicalPerPoint));
				var flagHeight = Math.Max(1,
					(int)Math.Ceiling(iosLayout.ItemImagePoints.Height * snapshot.LogicalPerPoint));
				flagWidth = Math.Min(flagWidth, item.Bounds.Width);
				flagHeight = Math.Min(flagHeight, item.Bounds.Height);
				flag.Bounds = new WidgetBounds(
					0, Math.Max(0, item.Bounds.Height - flagHeight) / 2, flagWidth, flagHeight);
				flag.StretchToFit = true;
				labelX = Math.Min(item.Bounds.Width,
					flag.Bounds.Right + IosMenuLayoutPolicy.Create(true, snapshot).Gap);
			}

			foreach (var label in item.Children.OfType<LabelWidget>())
			{
				if (labelX >= 0)
					label.Bounds.X = labelX;
				label.Bounds.Y = 0;
				label.Bounds.Height = item.Bounds.Height;
				var rightPadding = labelX >= 0 ? 0 : label.Bounds.X > 0 ? label.Bounds.X : 0;
				label.Bounds.Width = Math.Max(0, item.Bounds.Width - label.Bounds.X - rightPadding);
				label.Font = font;
			}
		}
	}

	public class MaskWidget : Widget
	{
		public event Action<MouseInput> OnMouseDown = _ => { };
		public MaskWidget() { }
		public MaskWidget(MaskWidget other)
			: base(other)
		{
			OnMouseDown = other.OnMouseDown;
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event == MouseInputEvent.Move)
				return false;

			if (mi.Event == MouseInputEvent.Down)
				OnMouseDown(mi);

			return true;
		}

		public override string GetCursor(int2 pos) { return null; }
		public override Widget Clone() { return new MaskWidget(this); }
	}
}
