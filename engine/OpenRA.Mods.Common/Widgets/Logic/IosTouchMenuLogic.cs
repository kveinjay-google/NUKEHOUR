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
using System.Linq;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class IosTouchMenuLogic : ChromeLogic
	{
		readonly Widget widget;
		Size lastResolution;
		Size lastNativePointSize;
		Rectangle lastSafeBounds;
		WidgetBounds lastWidgetBounds;
		Widget lastOptionsFirstRow;
		int lastOptionsRowCount = -1;
		Widget lastServerFirstRow;
		int lastServerRowCount = -1;
		bool layoutInitialized;

		[ObjectCreator.UseCtor]
		public IosTouchMenuLogic(Widget widget)
		{
			this.widget = widget;
			ApplyLayout();
		}

		public override void Tick()
		{
			if (!Platform.IsIOS)
				return;

			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			if (!layoutInitialized || resolution != lastResolution ||
				snapshot.NativePointSize != lastNativePointSize || snapshot.SafeBounds != lastSafeBounds ||
				!SameBounds(widget.Bounds, lastWidgetBounds))
			{
				ApplyLayout();
				return;
			}

			var options = widget.GetOrNull("LOBBY_OPTIONS");
			if (options != null && OptionsRowsChanged(options))
				LayoutLobbyOptionsRows(options, IosMenuLayoutPolicy.Create(true, snapshot));

			var serverList = widget.GetOrNull<ScrollPanelWidget>("SERVER_LIST");
			if (serverList != null && ServerRowsChanged(serverList))
				ApplyServerBrowserLayout(widget, IosMenuLayoutPolicy.Create(true, snapshot));
		}

		void ApplyLayout()
		{
			if (!Platform.IsIOS)
				return;

			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			ApplyIosFonts(widget);
			AdaptTree(widget, policy);

			var options = widget.GetOrNull("LOBBY_OPTIONS");
			if (options != null)
				LayoutLobbyOptionsRows(options, policy);

			if (IsServerCreationPanel(widget))
				ApplyServerCreationLayout(widget, policy);
			else if (IsServerBrowserPanel(widget))
				ApplyServerBrowserLayout(widget, policy);
			else if (widget.Id == "DIRECTCONNECT_PANEL")
				ApplyDirectConnectLayout(widget, policy);

			lastResolution = resolution;
			lastNativePointSize = snapshot.NativePointSize;
			lastSafeBounds = snapshot.SafeBounds;
			lastWidgetBounds = widget.Bounds;
			layoutInitialized = true;
		}

		static void AdaptTree(Widget root, IosMenuLayoutPolicy policy)
		{
			if (root is ButtonWidget or TextFieldWidget or SliderWidget)
				root.Bounds = policy.EnsureTouchTarget(root.Bounds);

			if (root is SliderWidget slider)
				ConfigureStepSlider(slider);

			if (root is ScrollPanelWidget scrollPanel)
			{
				scrollPanel.EnableContentDragging = true;
				scrollPanel.ContentDragThreshold = policy.ContentDragThreshold;
				if (policy.IsPhone)
				{
					scrollPanel.ScrollBar = ScrollBar.Hidden;
					scrollPanel.ScrollbarWidth = 0;
				}
				else if (scrollPanel.ScrollBar != ScrollBar.Hidden)
				{
					scrollPanel.ScrollbarWidth = Math.Max(scrollPanel.ScrollbarWidth, policy.MinimumTarget);
					scrollPanel.MinimumThumbSize = Math.Max(scrollPanel.MinimumThumbSize, policy.MinimumTarget);
				}
			}

			foreach (var child in root.Children.ToArray())
				AdaptTree(child, policy);
		}

		public static void ApplyIosFonts(Widget root)
		{
			if (root is LabelWidget label)
				label.Font = IosMenuLayoutPolicy.TouchFont(
					label.Font, label.Id is "TITLE" or "SERVER_NAME", false);
			else if (root is ButtonWidget button)
				button.Font = IosMenuLayoutPolicy.TouchFont(button.Font, false, true);
			else if (root is TextFieldWidget textField)
				textField.Font = IosMenuLayoutPolicy.TouchFont(textField.Font, false, false);
			else if (root is HotkeyEntryWidget hotkeyEntry)
				hotkeyEntry.Font = IosMenuLayoutPolicy.TouchFont(hotkeyEntry.Font, false, false);
			else if (root is SliderWidget slider)
				slider.TouchFont = IosMenuLayoutPolicy.TouchFont(slider.TouchFont, false, true);

			foreach (var child in root.Children)
				ApplyIosFonts(child);
		}

		void LayoutLobbyOptionsRows(Widget options, IosMenuLayoutPolicy policy)
		{
			if (options.Parent is not ScrollPanelWidget panel)
				return;

			var title = widget.GetOrNull<LabelWidget>("TITLE");
			var panelY = 0;
			if (title != null)
			{
				title.Bounds = new WidgetBounds(0, 0, widget.Bounds.Width, policy.MinimumTarget);
				panelY = title.Bounds.Bottom + policy.Gap;
			}

			panel.Bounds = new WidgetBounds(0, panelY, widget.Bounds.Width,
				Math.Max(0, widget.Bounds.Height - panelY));
			panel.EnableContentDragging = true;
			panel.ContentDragThreshold = policy.ContentDragThreshold;
			var scrollbarWidth = panel.ScrollBar == ScrollBar.Hidden ? 0 : panel.ScrollbarWidth;
			var contentWidth = Math.Max(policy.MinimumTarget,
				panel.Bounds.Width - scrollbarWidth - 2 * policy.Gap);
			options.Bounds = new WidgetBounds(policy.Gap, policy.Gap, contentWidth, 0);

			var y = 0;
			foreach (var row in options.Children)
			{
				var dropdown = row.Children.Any(child => child is DropDownButtonWidget);
				row.Bounds = new WidgetBounds(0, y, contentWidth, policy.OptionRowHeight(dropdown));
				var controls = row.Children.Where(child => child is CheckboxWidget or DropDownButtonWidget).ToArray();
				for (var i = 0; i < controls.Length; i++)
				{
					controls[i].Bounds = policy.OptionControlBounds(i, contentWidth, dropdown);
					var label = row.GetOrNull<LabelWidget>(controls[i].Id + "_DESC");
					if (label != null)
						label.Bounds = policy.OptionLabelBounds(i, contentWidth);
				}

				y = row.Bounds.Bottom + policy.Gap;
			}

			options.Bounds.Height = Math.Max(0, y - (options.Children.Count > 0 ? policy.Gap : 0));
			panel.ContentHeight = options.Bounds.Bottom + policy.Gap;
			lastOptionsFirstRow = options.Children.FirstOrDefault();
			lastOptionsRowCount = options.Children.Count;
		}

		static void ApplyServerCreationLayout(Widget panel, IosMenuLayoutPolicy policy)
		{
			var layout = IosServerCreationLayout.Create(policy);
			panel.Bounds = policy.ContentBounds;

			SetBounds(panel, "TITLE", layout.Header);
			LayoutLabeledField(panel, "SERVER_NAME_LABEL", "SERVER_NAME", layout.ServerNameRow, policy);
			LayoutPasswordRow(panel, layout.PasswordRow, policy);
			LayoutPortRow(panel, layout.PortRow, policy);
			LayoutNotices(panel, layout, policy);
			SetBounds(panel, "MAP_PREVIEW_ROOT", layout.MapPreview);
			SetBounds(panel, "MAP_BUTTON", layout.ChangeMap);
			SetBounds(panel, "CREATE_BUTTON", layout.CreateButton);
			SetBounds(panel, "BACK_BUTTON", layout.Back);

			var mapRoot = panel.GetOrNull("MAP_PREVIEW_ROOT");
			if (mapRoot != null)
				IosMapPreviewLayout.Apply(mapRoot, policy);
		}

		static void LayoutLabeledField(
			Widget panel, string labelId, string fieldId, WidgetBounds row, IosMenuLayoutPolicy policy)
		{
			if (policy.IsPhone)
			{
				var labelHeight = Math.Min(policy.MinimumReadableTextHeight, row.Height);
				var fieldY = Math.Min(row.Bottom, row.Y + labelHeight + policy.Gap);
				var fieldHeight = Math.Max(0, row.Bottom - fieldY);
				SetBounds(panel, labelId, new WidgetBounds(row.X, row.Y, row.Width, labelHeight));
				SetBounds(panel, fieldId, new WidgetBounds(row.X, fieldY, row.Width, fieldHeight));
				return;
			}

			var labelWidth = Math.Max(policy.MinimumTarget, row.Width / 4);
			var label = panel.GetOrNull(labelId);
			if (label != null)
				label.Bounds = new WidgetBounds(row.X, row.Y, labelWidth, row.Height);

			var fieldX = Math.Min(row.Right, row.X + labelWidth + policy.Gap);
			var field = panel.GetOrNull(fieldId);
			if (field != null)
				field.Bounds = new WidgetBounds(fieldX, row.Y, Math.Max(0, row.Right - fieldX), row.Height);
		}

		static void LayoutPasswordRow(Widget panel, WidgetBounds row, IosMenuLayoutPolicy policy)
		{
			if (policy.IsPhone)
			{
				var labelHeight = Math.Min(policy.MinimumReadableTextHeight, row.Height);
				var fieldY = Math.Min(row.Bottom, row.Y + labelHeight + policy.Gap);
				var fieldHeight = Math.Max(0, row.Bottom - fieldY);
				var phoneSuffixWidth = Math.Min(row.Width / 3, Math.Max(0, row.Width - policy.MinimumTarget - policy.Gap));
				var phoneFieldWidth = Math.Max(0, row.Width - phoneSuffixWidth - policy.Gap);
				SetBounds(panel, "PASSWORD_LABEL", new WidgetBounds(row.X, row.Y, row.Width, labelHeight));
				SetBounds(panel, "PASSWORD", new WidgetBounds(row.X, fieldY, phoneFieldWidth, fieldHeight));
				SetBounds(panel, "AFTER_PASSWORD_LABEL", new WidgetBounds(
					row.X + phoneFieldWidth + policy.Gap, fieldY, phoneSuffixWidth, fieldHeight));
				return;
			}

			var labelWidth = Math.Max(policy.MinimumTarget, row.Width / 4);
			SetBounds(panel, "PASSWORD_LABEL", new WidgetBounds(row.X, row.Y, labelWidth, row.Height));
			var fieldX = Math.Min(row.Right, row.X + labelWidth + policy.Gap);
			var available = Math.Max(0, row.Right - fieldX);
			var suffixWidth = Math.Min(available / 3, Math.Max(policy.MinimumTarget, available / 4));
			var fieldWidth = Math.Max(0, available - suffixWidth - policy.Gap);
			SetBounds(panel, "PASSWORD", new WidgetBounds(fieldX, row.Y, fieldWidth, row.Height));
			SetBounds(panel, "AFTER_PASSWORD_LABEL", new WidgetBounds(
				Math.Min(row.Right, fieldX + fieldWidth + policy.Gap), row.Y, suffixWidth, row.Height));
		}

		static void LayoutPortRow(Widget panel, WidgetBounds row, IosMenuLayoutPolicy policy)
		{
			if (policy.IsPhone)
			{
				var labelHeight = Math.Min(policy.MinimumReadableTextHeight, row.Height);
				var fieldY = Math.Min(row.Bottom, row.Y + labelHeight + policy.Gap);
				var fieldHeight = Math.Max(0, row.Bottom - fieldY);
				var phoneFieldWidth = Math.Min(policy.MinimumTarget, row.Width);
				var phoneAdvertiseX = Math.Min(row.Right, row.X + phoneFieldWidth + policy.Gap);
				SetBounds(panel, "LISTEN_PORT_LABEL", new WidgetBounds(row.X, row.Y, row.Width, labelHeight));
				SetBounds(panel, "LISTEN_PORT", new WidgetBounds(row.X, fieldY, phoneFieldWidth, fieldHeight));
				SetBounds(panel, "ADVERTISE_CHECKBOX", new WidgetBounds(
					phoneAdvertiseX, fieldY, Math.Max(0, row.Right - phoneAdvertiseX), fieldHeight));
				return;
			}

			var labelWidth = policy.IsPhone
				? Math.Min(policy.MinimumTarget, Math.Max(0, row.Width / 5))
				: Math.Max(policy.MinimumTarget, row.Width / 4);
			SetBounds(panel, "LISTEN_PORT_LABEL", new WidgetBounds(row.X, row.Y, labelWidth, row.Height));
			var fieldX = Math.Min(row.Right, row.X + labelWidth + policy.Gap);
			var fieldWidth = Math.Min(policy.IsPhone ? policy.MinimumTarget : policy.MinimumTarget * 2,
				Math.Max(0, row.Right - fieldX));
			SetBounds(panel, "LISTEN_PORT", new WidgetBounds(fieldX, row.Y, fieldWidth, row.Height));
			var advertiseX = Math.Min(row.Right, fieldX + fieldWidth + policy.Gap);
			SetBounds(panel, "ADVERTISE_CHECKBOX", new WidgetBounds(
				advertiseX, row.Y, Math.Max(0, row.Right - advertiseX), row.Height));
		}

		static void LayoutNotices(Widget panel, IosServerCreationLayout layout, IosMenuLayoutPolicy policy)
		{
			var header = layout.NoticesHeader;
			var content = layout.NoticesBody;
			var a = panel.GetOrNull<LabelWidget>("NOTICES_HEADER_A");
			var b = panel.GetOrNull<LabelWidget>("NOTICES_HEADER_B");
			var c = panel.GetOrNull<LabelWidget>("NOTICES_HEADER_C");
			LayoutNoticeHeader(a, b, c, header);

			foreach (var id in new[] { "NOTICES_LAN", "NOTICES_NO_UPNP", "NOTICES_UPNP" })
			{
				var notice = panel.GetOrNull(id);
				if (notice == null)
					continue;

				notice.Bounds = content;
				var lines = notice.Children.OfType<LabelWidget>().ToArray();
				if (lines.Length == 0)
					continue;

				var lineHeight = Math.Max(1, content.Height / lines.Length);
				var y = 0;
				for (var i = 0; i < lines.Length; i++)
				{
					var indent = lines[i].Bounds.X > 0 ? policy.Gap / 2 : 0;
					var height = i == lines.Length - 1 ? Math.Max(0, content.Height - y) : lineHeight;
					lines[i].Bounds = new WidgetBounds(indent, y,
						Math.Max(0, content.Width - indent), height);
					lines[i].WordWrap = true;
					y += lineHeight;
				}
			}
		}

		public static void RefreshServerNoticeHeader(Widget panel)
		{
			if (!Platform.IsIOS)
				return;

			var snapshot = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
			var layout = IosServerCreationLayout.Create(IosMenuLayoutPolicy.Create(true, snapshot));
			LayoutNoticeHeader(
				panel.GetOrNull<LabelWidget>("NOTICES_HEADER_A"),
				panel.GetOrNull<LabelWidget>("NOTICES_HEADER_B"),
				panel.GetOrNull<LabelWidget>("NOTICES_HEADER_C"),
				layout.NoticesHeader);
		}

		static void LayoutNoticeHeader(
			LabelWidget a, LabelWidget b, LabelWidget c, WidgetBounds header)
		{
			var labels = new[] { a, b, c };
			var widths = labels.Select(label =>
				!EvaluateDynamicVisibility(label) ? 0 :
				Game.Renderer.Fonts[label.Font].Measure(label.GetText() ?? "").X).ToArray();
			var segments = IosServerCreationLayout.HeaderSegments(header.Width, widths);
			for (var i = 0; i < labels.Length; i++)
				if (labels[i] != null)
					labels[i].Bounds = new WidgetBounds(
						header.X + segments[i].X, header.Y, segments[i].Width, header.Height);
		}

		static bool IsServerCreationPanel(Widget panel)
		{
			return panel.Id == "MULTIPLAYER_CREATESERVER_PANEL" ||
				(panel.GetOrNull("CREATE_BUTTON") != null && panel.GetOrNull("MAP_PREVIEW_ROOT") != null &&
					panel.GetOrNull("SERVER_NAME") != null);
		}

		static bool IsServerBrowserPanel(Widget panel)
		{
			return panel.Id is "MULTIPLAYER_PANEL" or "LOBBY_SERVERS_BIN" ||
				(panel.GetOrNull("SERVER_LIST") != null && panel.GetOrNull("SELECTED_SERVER") != null);
		}

		void ApplyServerBrowserLayout(Widget panel, IosMenuLayoutPolicy policy)
		{
			var standalone = panel.Id == "MULTIPLAYER_PANEL";
			if (standalone)
				panel.Bounds = policy.ContentBounds;

			var width = Math.Max(1, panel.Bounds.Width);
			var height = Math.Max(1, panel.Bounds.Height);
			var layout = IosMultiplayerBrowserLayout.Create(width, height, policy);
			if (standalone)
				SetBounds(panel, "TITLE", layout.Header);

			var table = standalone ? layout.Table : new WidgetBounds(
				0, 0, layout.Table.Width, Math.Max(0, height - policy.MinimumTarget - policy.Gap));
			var details = standalone ? layout.Details : new WidgetBounds(
				table.Right + policy.Gap, 0, layout.Details.Width, height);
			var headerHeight = Math.Min(policy.MinimumReadableTextHeight, table.Height);
			var listY = Math.Min(table.Height, headerHeight + policy.Gap);
			var listBounds = new WidgetBounds(table.X, table.Y + listY,
				table.Width, Math.Max(0, table.Height - listY));
			var noticeBounds = new WidgetBounds(
				listBounds.X, listBounds.Y, listBounds.Width,
				Math.Min(policy.MinimumReadableTextHeight, listBounds.Height));
			var notice = panel.GetOrNull("NOTICE_CONTAINER");
			if (notice != null)
			{
				notice.Bounds = noticeBounds;
				foreach (var label in notice.Children.OfType<LabelWidget>())
				{
					label.Bounds = new WidgetBounds(
						policy.Gap, 0, Math.Max(1, noticeBounds.Width - 2 * policy.Gap), noticeBounds.Height);
					label.WordWrap = false;
					label.VAlign = TextVAlign.Middle;
					IosResponsiveText.Configure(label, false);
				}
			}

			var effectiveListBounds = IosMultiplayerBrowserLayout.ServerListWithNotice(
				listBounds, noticeBounds.Height, EvaluateDynamicVisibility(notice));

			var labels = panel.GetOrNull("LABEL_CONTAINER");
			if (labels != null)
			{
				labels.Bounds = new WidgetBounds(table.X, table.Y, table.Width, headerHeight);
				IosMultiplayerBrowserLayout.ApplyServerRow(labels,
					IosMultiplayerBrowserLayout.ServerColumnBounds(table.Width, policy.MinimumTarget),
					headerHeight, policy);
			}

			var list = panel.GetOrNull<ScrollPanelWidget>("SERVER_LIST");
			if (list != null)
			{
				list.Bounds = effectiveListBounds;
				list.EnableContentDragging = true;
				list.ContentDragThreshold = policy.ContentDragThreshold;
				if (policy.IsPhone)
				{
					list.ScrollBar = ScrollBar.Hidden;
					list.ScrollbarWidth = 0;
				}
				else
				{
					list.ScrollBar = ScrollBar.Right;
					list.ScrollbarWidth = policy.MinimumTarget;
					list.MinimumThumbSize = policy.MinimumTarget;
				}

				var contentWidth = Math.Max(0, list.Bounds.Width - list.ScrollbarWidth);
				var columns = IosMultiplayerBrowserLayout.ServerColumnBounds(contentWidth, policy.MinimumTarget);
				foreach (var row in list.Children)
				{
					row.Bounds = new WidgetBounds(0, row.Bounds.Y, contentWidth, policy.MinimumTarget);
					IosMultiplayerBrowserLayout.ApplyServerRow(
						row, columns, policy.MinimumTarget, policy);
				}

				list.Layout.AdjustChildren();
				lastServerFirstRow = list.Children.FirstOrDefault();
				lastServerRowCount = list.Children.Count;
			}

			SetBounds(panel, "PROGRESS_LABEL", effectiveListBounds);
			var selected = panel.GetOrNull("SELECTED_SERVER");
			if (selected != null)
			{
				selected.Bounds = details;
				var dynamicClients = selected.GetOrNull("MULTIPLAYER_CLIENT_LIST");
				IosMultiplayerDetailsLayout.Apply(
					selected, policy, EvaluateDynamicVisibility(dynamicClients));
			}

			if (standalone)
			{
				SetBounds(panel, "FILTERS_DROPDOWNBUTTON", Offset(layout.Filters, layout.Footer.Y));
				SetBounds(panel, "RELOAD_BUTTON", Offset(layout.Reload, layout.Footer.Y));
				SetBounds(panel, "PLAYER_COUNT", Offset(layout.PlayerCount, layout.Footer.Y));
				SetBounds(panel, "DIRECTCONNECT_BUTTON", Offset(layout.DirectConnect, layout.Footer.Y));
				SetBounds(panel, "CREATE_BUTTON", Offset(layout.CreateButton, layout.Footer.Y));
				SetBounds(panel, "BACK_BUTTON", Offset(layout.Back, layout.Footer.Y));
			}
			else
			{
				var footerY = Math.Max(0, height - policy.MinimumTarget);
				var filtersWidth = Math.Max(policy.MinimumTarget, (table.Width - policy.Gap) * 3 / 4);
				SetBounds(panel, "FILTERS_DROPDOWNBUTTON",
					new WidgetBounds(0, footerY, filtersWidth, policy.MinimumTarget));
				SetBounds(panel, "RELOAD_BUTTON", new WidgetBounds(
					filtersWidth + policy.Gap, footerY,
					Math.Max(0, table.Width - filtersWidth - policy.Gap), policy.MinimumTarget));
			}
		}

		static void ApplyDirectConnectLayout(Widget panel, IosMenuLayoutPolicy policy)
		{
			panel.Bounds = policy.ContentBounds;
			var width = panel.Bounds.Width;
			var height = panel.Bounds.Height;
			SetBounds(panel, "DIRECTCONNECT_LABEL_TITLE", new WidgetBounds(0, 0, width, policy.HeaderHeight));
			var rowY = Math.Min(height, policy.HeaderHeight + policy.Gap);
			var rowHeight = Math.Min(policy.MinimumReadableTextHeight + policy.Gap + policy.MinimumTarget,
				Math.Max(0, height - rowY - policy.FooterHeight - 2 * policy.Gap));
			var addressWidth = Math.Max(0, width * 3 / 4 - policy.Gap);
			var portX = Math.Min(width, addressWidth + policy.Gap);
			var labelHeight = Math.Min(policy.MinimumReadableTextHeight, rowHeight);
			var fieldY = Math.Min(rowY + rowHeight, rowY + labelHeight + policy.Gap);
			var fieldHeight = Math.Max(0, rowY + rowHeight - fieldY);
			SetBounds(panel, "ADDRESS_LABEL", new WidgetBounds(0, rowY, addressWidth, labelHeight));
			SetBounds(panel, "IP", new WidgetBounds(0, fieldY, addressWidth, fieldHeight));
			SetBounds(panel, "PORT_LABEL", new WidgetBounds(portX, rowY, Math.Max(0, width - portX), labelHeight));
			SetBounds(panel, "PORT", new WidgetBounds(portX, fieldY, Math.Max(0, width - portX), fieldHeight));
			var footerY = Math.Max(rowY + rowHeight + policy.Gap, height - policy.FooterHeight);
			var buttonWidth = Math.Max(0, (width - policy.Gap) / 2);
			SetBounds(panel, "JOIN_BUTTON", new WidgetBounds(0, footerY, buttonWidth, Math.Max(0, height - footerY)));
			SetBounds(panel, "BACK_BUTTON", new WidgetBounds(
				buttonWidth + policy.Gap, footerY, Math.Max(0, width - buttonWidth - policy.Gap),
				Math.Max(0, height - footerY)));
		}

		static WidgetBounds Offset(WidgetBounds bounds, int y)
		{
			return new WidgetBounds(bounds.X, bounds.Y + y, bounds.Width, bounds.Height);
		}

		bool OptionsRowsChanged(Widget options)
		{
			return options.Children.Count != lastOptionsRowCount ||
				options.Children.FirstOrDefault() != lastOptionsFirstRow;
		}

		bool ServerRowsChanged(ScrollPanelWidget list)
		{
			return list.Children.Count != lastServerRowCount || list.Children.FirstOrDefault() != lastServerFirstRow;
		}

		static void SetBounds(Widget root, string id, WidgetBounds bounds)
		{
			var child = root.GetOrNull(id);
			if (child != null)
				child.Bounds = bounds;
		}

		static bool SameBounds(WidgetBounds a, WidgetBounds b)
		{
			return a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
		}

		public static bool EvaluateDynamicVisibility(Widget widget)
		{
			return widget != null && widget.IsVisible();
		}

		static void ConfigureStepSlider(SliderWidget slider)
		{
			var step = slider.Ticks > 1
				? (slider.MaximumValue - slider.MinimumValue) / (slider.Ticks - 1)
				: (slider.MaximumValue - slider.MinimumValue) / 10f;

			slider.TouchStep = step;
			slider.UseTouchStepControls = true;
		}
	}
}
