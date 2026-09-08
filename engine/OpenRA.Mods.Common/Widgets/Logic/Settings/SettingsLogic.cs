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
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class SettingsLogic : ChromeLogic
	{
		[FluentReference]
		const string SettingsSaveTitle = "dialog-settings-save.title";

		[FluentReference]
		const string SettingsSavePrompt = "dialog-settings-save.prompt";

		[FluentReference]
		const string SettingsSaveCancel = "dialog-settings-save.cancel";

		[FluentReference]
		const string RestartTitle = "dialog-settings-restart.title";

		[FluentReference]
		const string RestartPrompt = "dialog-settings-restart.prompt";

		[FluentReference]
		const string RestartAccept = "dialog-settings-restart.confirm";

		[FluentReference]
		const string RestartCancel = "dialog-settings-restart.cancel";

		[FluentReference("panel")]
		const string ResetTitle = "dialog-settings-reset.title";

		[FluentReference]
		const string ResetPrompt = "dialog-settings-reset.prompt";

		[FluentReference]
		const string ResetAccept = "dialog-settings-reset.confirm";

		[FluentReference]
		const string ResetCancel = "dialog-settings-reset.cancel";

		readonly Dictionary<string, Func<bool>> leavePanelActions = new();
		readonly Dictionary<string, Action> resetPanelActions = new();

		readonly Widget panelContainer, tabContainer;
		readonly ButtonWidget tabTemplate;
		readonly int2 buttonStride;
		readonly List<ButtonWidget> buttons = new();
		readonly Dictionary<string, string> panels = new();
		readonly Dictionary<Widget, WidgetBounds> iosOriginalBounds = new();
		readonly Dictionary<ScrollPanelWidget, (int TopBottom, int Item, int Scrollbar, ScrollBar Position)> iosOriginalScrollMetrics = new();
		readonly Widget settingsWidget;
		readonly ContainerWidget panelTemplate;
		Size lastViewportSize;
		Rectangle lastIosWindow;
		Rectangle lastIosContent;
		bool lastIosWasPhone;
		string activePanel;

		bool needsRestart = false;

		static SettingsLogic() { }

		[ObjectCreator.UseCtor]
		public SettingsLogic(Widget widget, Action onExit, WorldRenderer worldRenderer, Dictionary<string, MiniYaml> logicArgs, ModData modData)
		{
			settingsWidget = widget;
			panelContainer = widget.Get("PANEL_CONTAINER");
			panelTemplate = panelContainer.Get<ContainerWidget>("PANEL_TEMPLATE");
			panelContainer.RemoveChild(panelTemplate);

			tabContainer = widget.Get("SETTINGS_TAB_CONTAINER");
			tabTemplate = tabContainer.Get<ButtonWidget>("BUTTON_TEMPLATE");
			tabContainer.RemoveChild(tabTemplate);

			if (logicArgs.TryGetValue("ButtonStride", out var buttonStrideNode))
				buttonStride = FieldLoader.GetValue<int2>("ButtonStride", buttonStrideNode.Value);

			if (logicArgs.TryGetValue("Panels", out var settingsPanels))
			{
				panels = settingsPanels.ToDictionary(kv => kv.Value);

				foreach (var panel in panels)
				{
					var container = panelTemplate.Clone() as ContainerWidget;
					container.Id = panel.Key;
					panelContainer.AddChild(container);

					Game.LoadWidget(worldRenderer.World, panel.Key, container, new WidgetArgs()
					{
						{ "registerPanel", (Action<string, string, Func<Widget, Func<bool>>, Func<Widget, Action>>)RegisterSettingsPanel },
						{ "panelID", panel.Key },
						{ "label", panel.Value }
					});
				}
			}

			if (Platform.IsIOS)
			{
				CaptureIosOriginalLayout(widget);
				iosOriginalBounds.Add(panelTemplate, panelTemplate.Bounds);
				ApplyIosLayout();
			}
			else
				foreach (var panel in panels.Keys)
					ApplyPlatformVisibility(panelContainer.Get(panel), false);

			widget.Get<ButtonWidget>("BACK_BUTTON").OnClick = () =>
			{
				needsRestart |= leavePanelActions[activePanel]();
				var current = Game.Settings;
				current.Save();

				void CloseAndExit() { Ui.CloseWindow(); onExit(); }
				if (needsRestart)
				{
					void NoRestart() => ConfirmationDialogs.ButtonPrompt(modData,
						title: SettingsSaveTitle,
						text: SettingsSavePrompt,
						onCancel: CloseAndExit,
						cancelText: SettingsSaveCancel);

					if (!Game.ExternalMods.TryGetValue(ExternalMod.MakeKey(Game.ModData.Manifest), out var external))
					{
						NoRestart();
						return;
					}

					ConfirmationDialogs.ButtonPrompt(modData,
						title: RestartTitle,
						text: RestartPrompt,
						onConfirm: () => Game.SwitchToExternalMod(external, null, NoRestart),
						confirmText: RestartAccept,
						onCancel: CloseAndExit,
						cancelText: RestartCancel);
				}
				else
					CloseAndExit();
			};

			widget.Get<ButtonWidget>("RESET_BUTTON").OnClick = () =>
			{
				void Reset()
				{
					resetPanelActions[activePanel]();
					Game.Settings.Save();
				}

				ConfirmationDialogs.ButtonPrompt(modData,
					title: ResetTitle,
					text: ResetPrompt,
					titleArguments: new object[] { "panel", FluentProvider.GetMessage(panels[activePanel]) },
					onConfirm: Reset,
					confirmText: ResetAccept,
					onCancel: () => { },
					cancelText: ResetCancel);
			};
		}

		void CaptureIosOriginalLayout(Widget widget)
		{
			iosOriginalBounds.Add(widget, widget.Bounds);
			if (widget is ScrollPanelWidget scrollPanel)
				iosOriginalScrollMetrics.Add(scrollPanel,
					(scrollPanel.TopBottomSpacing, scrollPanel.ItemSpacing, scrollPanel.ScrollbarWidth, scrollPanel.ScrollBar));

			foreach (var child in widget.Children)
				CaptureIosOriginalLayout(child);
		}

		void RestoreIosOriginalLayout()
		{
			foreach (var entry in iosOriginalBounds)
				entry.Key.Bounds = entry.Value;

			foreach (var entry in iosOriginalScrollMetrics)
			{
				entry.Key.TopBottomSpacing = entry.Value.TopBottom;
				entry.Key.ItemSpacing = entry.Value.Item;
				entry.Key.ScrollbarWidth = entry.Value.Scrollbar;
				entry.Key.ScrollBar = entry.Value.Position;
			}
		}

		void ApplyIosLayout()
		{
			RestoreIosOriginalLayout();

			var viewport = Game.Renderer.Resolution;
			lastViewportSize = viewport;
			var layout = IosSettingsLayout.ForSnapshot(true, IosScreenMetrics.SnapshotFor(viewport));
			lastIosWindow = layout.Window;
			lastIosContent = layout.Content;
			lastIosWasPhone = layout.IsPhone;
			settingsWidget.Bounds = ToWidgetBounds(layout.Window);

			var title = settingsWidget.Get<LabelWidget>("SETTINGS_LABEL_TITLE");
			title.Bounds = RelativeTo(layout.Header, layout.Window);
			title.Font = "IosTitle";

			tabContainer.Bounds = RelativeTo(layout.Tabs, layout.Window);
			tabTemplate.Font = "IosBold";
			for (var i = 0; i < buttons.Count; i++)
			{
				buttons[i].Bounds = RelativeTo(layout.TabBounds(i), layout.Tabs);
				buttons[i].Font = "IosBold";
			}

			panelContainer.Bounds = new WidgetBounds(0, 0, layout.Window.Width, layout.Window.Height);
			foreach (var panel in panels.Keys)
			{
				var panelWidget = panelContainer.Get(panel);
				panelWidget.Bounds = RelativeTo(layout.Content, layout.Window);
				ApplyIosPanelContent(panelWidget, layout);
			}

			panelTemplate.Bounds = RelativeTo(layout.Content, layout.Window);

			var reset = settingsWidget.Get<ButtonWidget>("RESET_BUTTON");
			reset.Bounds = RelativeTo(layout.Reset, layout.Window);
			reset.Font = "IosBold";

			var back = settingsWidget.Get<ButtonWidget>("BACK_BUTTON");
			back.Bounds = RelativeTo(layout.Back, layout.Window);
			back.Font = "IosBold";
		}

		void ApplyIosPanelContent(Widget panel, IosSettingsLayout layout)
		{
			ApplyIosVisibility(panel);

			var settingsScrollPanel = panel.GetOrNull<ScrollPanelWidget>("SETTINGS_SCROLLPANEL");
			if (settingsScrollPanel != null)
				ApplyIosSettingsScrollPanel(panel, settingsScrollPanel, layout);

			if (panel.Id == "HOTKEYS_PANEL")
				ApplyIosHotkeyPanel(panel, layout);

			ConfigureIosTouchWidgets(panel, layout);
		}

		static void ConfigureIosTouchWidgets(Widget widget, IosSettingsLayout layout)
		{
			if (widget is SliderWidget slider)
			{
				slider.TouchStep = IosTouchWidgetPolicy.StepForRange(
					slider.MinimumValue, slider.MaximumValue, slider.Ticks);
				slider.UseTouchStepControls = true;
				slider.TouchFont = "IosBold";
			}
			else if (widget is ScrollPanelWidget scrollPanel)
			{
				scrollPanel.EnableContentDragging = true;
				scrollPanel.ContentDragThreshold = IosTouchWidgetPolicy.ContentDragThreshold(layout.LogicalPerPoint);
			}

			foreach (var child in widget.Children)
				ConfigureIosTouchWidgets(child, layout);
		}

		static void ApplyIosVisibility(Widget panel) => ApplyPlatformVisibility(panel, true);

		static void ApplyPlatformVisibility(Widget panel, bool isIos)
		{
			foreach (var widget in DescendantsAndSelf(panel))
			{
				if (IosSettingsVisibilityPolicy.ShouldShowContainer(isIos, widget.Id))
					continue;

				widget.Visible = false;
				widget.IsVisible = () => false;
			}

			var scrollPanel = panel.GetOrNull<ScrollPanelWidget>("SETTINGS_SCROLLPANEL");
			if (scrollPanel == null)
				return;

			foreach (var row in scrollPanel.Children)
			{
				if (IosSettingsVisibilityPolicy.ShouldCollapseRow(isIos, row.Id))
				{
					row.Visible = false;
					row.IsVisible = () => false;
					continue;
				}

				var columns = row.Children.Where(ContainsTouchTarget).ToArray();
				if (columns.Length == 0 || columns.Any(column => column.IsVisible()))
					continue;

				row.Visible = false;
				row.IsVisible = () => false;
			}

			scrollPanel.Layout.AdjustChildren();
		}

		static IEnumerable<Widget> DescendantsAndSelf(Widget widget)
		{
			yield return widget;
			foreach (var child in widget.Children)
				foreach (var descendant in DescendantsAndSelf(child))
					yield return descendant;
		}

		void ApplyIosSettingsScrollPanel(Widget panel, ScrollPanelWidget scrollPanel, IosSettingsLayout layout)
		{
			var sourceScrollPanel = OriginalBounds(scrollPanel);
			var originalScrollbarWidth = iosOriginalScrollMetrics.TryGetValue(scrollPanel, out var originalMetrics) ?
				originalMetrics.Scrollbar : scrollPanel.ScrollbarWidth;
			var horizontalScale = layout.ScrollContentHorizontalScale(
				sourceScrollPanel.Width, originalScrollbarWidth, panel.Bounds.Width);
			scrollPanel.Bounds = new WidgetBounds(0, 0, panel.Bounds.Width, panel.Bounds.Height);
			scrollPanel.ScrollbarWidth = layout.ScrollbarWidth;
			scrollPanel.TopBottomSpacing = layout.Scale(4);
			scrollPanel.ItemSpacing = layout.Scale(6);

			foreach (var item in scrollPanel.Children)
			{
				var source = OriginalBounds(item);
				if (source.Height <= 0)
				{
					item.Bounds = new WidgetBounds(
						ScaleHorizontal(source.X, horizontalScale), source.Y,
						ScaleHorizontal(source.Width, horizontalScale), layout.Scale(8));
					continue;
				}

				var minimumTouchHeight = MinimumOriginalTouchHeight(item);
				var isSectionHeader = item.Id != null && item.Id.EndsWith("SECTION_HEADER", StringComparison.Ordinal);
				var verticalScale = layout.RowVerticalScale(source.Height, minimumTouchHeight, isSectionHeader);
				ScaleWidgetTreeFromOriginal(item, layout, horizontalScale, verticalScale, false);
				ReflowIosVisibleColumns(item, layout);
			}

			scrollPanel.Layout.AdjustChildren();
		}

		void ScaleWidgetTreeFromOriginal(
			Widget widget, IosSettingsLayout layout, double horizontalScale, double verticalScale, bool scalePosition)
		{
			var bounds = OriginalBounds(widget);
			var transformed = layout.TransformBoundsFromOriginal(
				bounds.ToRectangle(), horizontalScale, verticalScale, scalePosition, IsTouchTarget(widget));
			widget.Bounds = ToWidgetBounds(transformed);
			ApplyIosFont(widget);

			foreach (var child in widget.Children)
				ScaleWidgetTreeFromOriginal(child, layout, horizontalScale, verticalScale, true);
		}

		WidgetBounds OriginalBounds(Widget widget) =>
			iosOriginalBounds.TryGetValue(widget, out var bounds) ? bounds : widget.Bounds;

		int MinimumOriginalTouchHeight(Widget widget)
		{
			if (!widget.IsVisible())
				return 0;

			var minimum = IsTouchTarget(widget) ? OriginalBounds(widget).Height : 0;
			foreach (var child in widget.Children)
			{
				var childMinimum = MinimumOriginalTouchHeight(child);
				if (childMinimum > 0 && (minimum == 0 || childMinimum < minimum))
					minimum = childMinimum;
			}

			return minimum;
		}

		static void ReflowIosVisibleColumns(Widget row, IosSettingsLayout layout)
		{
			var columns = row.Children.Where(column => column.IsVisible() && ContainsTouchTarget(column)).ToArray();
			if (columns.Length == 0 || columns.Length > 2)
				return;

			var inset = layout.Scale(10);
			var gap = layout.Scale(10);
			for (var i = 0; i < columns.Length; i++)
			{
				var target = IosSettingsVisibilityPolicy.ColumnBounds(
					row.Bounds.Width, columns.Length, i, inset, gap);
				ResizeColumnHorizontally(columns[i], target.X, target.Width);
			}
		}

		static void ResizeColumnHorizontally(Widget column, int x, int width)
		{
			var sourceWidth = Math.Max(1, column.Bounds.Width);
			var scale = width / (double)sourceWidth;
			column.Bounds = new WidgetBounds(x, column.Bounds.Y, width, column.Bounds.Height);
			foreach (var child in column.Children)
				ScaleCurrentHorizontal(child, scale);
		}

		static void ScaleCurrentHorizontal(Widget widget, double scale)
		{
			widget.Bounds = new WidgetBounds(
				ScaleHorizontal(widget.Bounds.X, scale), widget.Bounds.Y,
				ScaleHorizontal(widget.Bounds.Width, scale), widget.Bounds.Height);

			foreach (var child in widget.Children)
				ScaleCurrentHorizontal(child, scale);
		}

		static bool ContainsTouchTarget(Widget widget)
		{
			if (IsTouchTarget(widget))
				return true;

			return widget.Children.Any(ContainsTouchTarget);
		}

		static bool IsTouchTarget(Widget widget) =>
			widget is ButtonWidget || widget is TextFieldWidget || widget is SliderWidget || widget is HotkeyEntryWidget;

		static void ApplyIosFont(Widget widget)
		{
			if (widget is LabelWidget label)
				label.Font = label.Font != null && label.Font.Contains("Bold", StringComparison.Ordinal) ? "IosBold" : "IosRegular";
			else if (widget is ButtonWidget button)
				button.Font = "IosBold";
			else if (widget is TextFieldWidget textField)
				textField.Font = "IosRegular";
			else if (widget is HotkeyEntryWidget hotkeyEntry)
				hotkeyEntry.Font = "IosRegular";
		}

		void ApplyIosHotkeyPanel(Widget panel, IosSettingsLayout layout)
		{
			var header = RelativeRectangle(layout.HotkeyHeader, layout.Content);
			var list = RelativeRectangle(layout.HotkeyList, layout.Content);
			var footer = RelativeRectangle(layout.HotkeyFooter, layout.Content);
			var gap = layout.Scale(4);
			var filterLabelWidth = layout.Scale(70);
			var contextLabelWidth = layout.Scale(90);
			var fieldWidth = Math.Max(layout.MinimumTarget,
				(header.Width - filterLabelWidth - contextLabelWidth - 3 * gap) / 2);

			SetBounds(panel.GetOrNull("FILTER_INPUT_LABEL"),
				new Rectangle(header.X, header.Y, filterLabelWidth, header.Height));
			SetBounds(panel.GetOrNull("FILTER_INPUT"),
				new Rectangle(header.X + filterLabelWidth + gap, header.Y, fieldWidth, header.Height));
			var contextLabelX = header.Right - contextLabelWidth - gap - fieldWidth;
			SetBounds(panel.GetOrNull("CONTEXT_DROPDOWN_LABEL"),
				new Rectangle(contextLabelX, header.Y, contextLabelWidth, header.Height));
			SetBounds(panel.GetOrNull("CONTEXT_DROPDOWN"),
				new Rectangle(header.Right - fieldWidth, header.Y, fieldWidth, header.Height));

			var hotkeyList = panel.Get<ScrollPanelWidget>("HOTKEY_LIST");
			hotkeyList.Bounds = ToWidgetBounds(list);
			var originalScrollBar = iosOriginalScrollMetrics.TryGetValue(hotkeyList, out var originalMetrics) ?
				originalMetrics.Position : hotkeyList.ScrollBar;
			hotkeyList.ScrollBar = layout.HotkeyScrollbarVisible ? originalScrollBar : ScrollBar.Hidden;
			hotkeyList.ScrollbarWidth = layout.HotkeyScrollbarWidth;
			hotkeyList.TopBottomSpacing = layout.Scale(4);
			hotkeyList.ItemSpacing = layout.Scale(6);
			LayoutIosHotkeyEmptyList(panel.GetOrNull("HOTKEY_EMPTY_LIST"), list);
			ApplyIosHotkeyListChildren(hotkeyList, layout);

			var remapBackground = panel.GetOrNull("HOTKEY_REMAP_BGND");
			SetBounds(remapBackground, footer);
			SetBounds(panel.GetOrNull("HOTKEY_REMAP_DIALOG"), new Rectangle(0, 0, footer.Width, footer.Height));
			ApplyIosHotkeyFooter(panel, footer, layout);

			foreach (var child in panel.Children)
				ApplyIosFontTree(child);
		}

		static void LayoutIosHotkeyEmptyList(Widget emptyList, Rectangle bounds)
		{
			if (emptyList == null)
				return;

			SetBounds(emptyList, bounds);
			SetBounds(emptyList.GetOrNull("HOTKEY_EMPTY_LIST_MESSAGE"),
				new Rectangle(0, 0, bounds.Width, bounds.Height));
		}

		static void ApplyIosHotkeyFooter(Widget panel, Rectangle footer, IosSettingsLayout layout)
		{
			var gap = layout.Scale(4);
			var labelWidth = Math.Max(layout.MinimumTarget, footer.Width / 5);
			var buttonsWidth = Math.Max(3 * layout.MinimumTarget + 2 * gap, footer.Width / 3);
			buttonsWidth = Math.Min(buttonsWidth, footer.Width - labelWidth - 3 * gap - layout.MinimumTarget);
			var buttonStart = footer.Width - buttonsWidth;
			var entryX = labelWidth + gap;
			var entryWidth = Math.Max(layout.MinimumTarget, buttonStart - gap - entryX);
			SetBounds(panel.GetOrNull("HOTKEY_LABEL"),
				new Rectangle(0, 0, labelWidth, layout.MinimumTarget));
			SetBounds(panel.GetOrNull("HOTKEY_ENTRY"),
				new Rectangle(entryX, 0, entryWidth, layout.MinimumTarget));
			SetBounds(panel.GetOrNull("NOTICES"),
				new Rectangle(entryX, layout.MinimumTarget, entryWidth, Math.Max(1, footer.Height - layout.MinimumTarget)));

			var buttonWidth = (buttonsWidth - 2 * gap) / 3;
			SetBounds(panel.GetOrNull("OVERRIDE_HOTKEY_BUTTON"),
				new Rectangle(buttonStart, 0, buttonWidth, layout.HotkeyActionHeight));
			SetBounds(panel.GetOrNull("CLEAR_HOTKEY_BUTTON"),
				new Rectangle(buttonStart + buttonWidth + gap, 0, buttonWidth, layout.HotkeyActionHeight));
			SetBounds(panel.GetOrNull("RESET_HOTKEY_BUTTON"),
				Rectangle.FromLTRB(buttonStart + 2 * (buttonWidth + gap), 0, footer.Width, layout.HotkeyActionHeight));
		}

		static void ApplyIosHotkeyListChildren(ScrollPanelWidget list, IosSettingsLayout layout)
		{
			var gap = layout.Scale(6);
			var columns = layout.HotkeyColumns;
			var itemWidth = layout.HotkeyItemWidth(list.Bounds.Width);
			var availableWidth = columns * itemWidth + (columns - 1) * gap;
			var y = list.TopBottomSpacing;
			var column = 0;

			foreach (var item in list.Children)
			{
				if (item.Id == "HEADER")
				{
					if (column != 0)
					{
						y += layout.MinimumTarget + gap;
						column = 0;
					}

					item.Bounds = new WidgetBounds(0, y, availableWidth, layout.SectionHeaderHeight);
					PrepareHotkeyHeader(item, availableWidth, layout.SectionHeaderHeight);
					y += layout.SectionHeaderHeight + gap;
					continue;
				}

				item.Bounds = new WidgetBounds(column * (itemWidth + gap), y, itemWidth, layout.MinimumTarget);
				PrepareHotkeyItem(item, itemWidth, layout.MinimumTarget, layout);
				column++;
				if (column == columns)
				{
					y += layout.MinimumTarget + gap;
					column = 0;
				}
			}

			if (column != 0)
				y += layout.MinimumTarget + gap;

			list.ContentHeight = y + list.TopBottomSpacing;
		}

		internal static void LayoutIosHotkeyList(ScrollPanelWidget list)
		{
			if (!Platform.IsIOS)
				return;

			var layout = IosSettingsLayout.ForSnapshot(true,
				IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution));
			ApplyIosHotkeyListChildren(list, layout);
		}

		internal static void PrepareIosHotkeyTemplates(Widget headerTemplate, Widget itemTemplate)
		{
			if (!Platform.IsIOS)
				return;

			var layout = IosSettingsLayout.ForSnapshot(true,
				IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution));
			var gap = layout.Scale(6);
			var columns = layout.HotkeyColumns;
			var itemWidth = layout.HotkeyItemWidth(layout.Content.Width);
			var availableWidth = columns * itemWidth + (columns - 1) * gap;
			headerTemplate.Bounds = new WidgetBounds(0, 0, availableWidth, layout.SectionHeaderHeight);
			itemTemplate.Bounds = new WidgetBounds(0, 0, itemWidth, layout.MinimumTarget);
			PrepareHotkeyHeader(headerTemplate, availableWidth, layout.SectionHeaderHeight);
			PrepareHotkeyItem(itemTemplate, itemWidth, layout.MinimumTarget, layout);
		}

		static void PrepareHotkeyHeader(Widget header, int width, int height)
		{
			SetBounds(header.GetOrNull("BACKGROUND"), new Rectangle(0, 0, width, height));
			SetBounds(header.GetOrNull("LABEL"), new Rectangle(0, 0, width, height));
			ApplyIosFontTree(header);
		}

		static void PrepareHotkeyItem(Widget item, int width, int height, IosSettingsLayout layout)
		{
			var gap = layout.Scale(4);
			var buttonWidth = Math.Max(layout.MinimumTarget, width * 2 / 5);
			SetBounds(item.GetOrNull("FUNCTION"),
				new Rectangle(0, 0, Math.Max(1, width - buttonWidth - gap), height));
			SetBounds(item.GetOrNull("HOTKEY"),
				new Rectangle(width - buttonWidth, 0, buttonWidth, height));
			ApplyIosFontTree(item);
		}

		static void ApplyIosFontTree(Widget widget)
		{
			if (widget == null)
				return;

			ApplyIosFont(widget);
			foreach (var child in widget.Children)
				ApplyIosFontTree(child);
		}

		static void SetBounds(Widget widget, Rectangle bounds)
		{
			if (widget != null)
				widget.Bounds = ToWidgetBounds(bounds);
		}

		static WidgetBounds RelativeTo(Rectangle bounds, Rectangle parent) =>
			new(bounds.X - parent.X, bounds.Y - parent.Y, bounds.Width, bounds.Height);

		static Rectangle RelativeRectangle(Rectangle bounds, Rectangle parent) =>
			new(bounds.X - parent.X, bounds.Y - parent.Y, bounds.Width, bounds.Height);

		static WidgetBounds ToWidgetBounds(Rectangle bounds) =>
			new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

		static int ScaleHorizontal(int value, double scale) =>
			(int)Math.Round(value * scale, MidpointRounding.AwayFromZero);

		public override void Tick()
		{
			if (!Platform.IsIOS)
				return;

			var viewport = Game.Renderer.Resolution;
			var layout = IosSettingsLayout.ForSnapshot(true, IosScreenMetrics.SnapshotFor(viewport));
			if (viewport != lastViewportSize || layout.Window != lastIosWindow ||
				layout.Content != lastIosContent || layout.IsPhone != lastIosWasPhone)
				ApplyIosLayout();
		}

		public void RegisterSettingsPanel(string panelID, string label, Func<Widget, Func<bool>> init, Func<Widget, Action> reset)
		{
			var panel = panelContainer.Get(panelID);

			activePanel ??= panelID;

			panel.IsVisible = () => activePanel == panelID;

			leavePanelActions.Add(panelID, init(panel));
			resetPanelActions.Add(panelID, reset(panel));

			AddSettingsTab(panelID, label);
		}

		ButtonWidget AddSettingsTab(string id, string label)
		{
			var tab = tabTemplate.Clone() as ButtonWidget;
			var lastButton = buttons.LastOrDefault();
			if (lastButton != null)
			{
				tab.Bounds.X = lastButton.Bounds.X + buttonStride.X;
				tab.Bounds.Y = lastButton.Bounds.Y + buttonStride.Y;
			}

			tab.Id = id;
			// Panel labels in chrome YAML are Fluent keys (e.g. button-settings-tab-display).
			var resolvedLabel = FluentProvider.GetMessage(
				Platform.IsIOS && id == "INPUT_PANEL" ? "button-settings-tab-touch" : label);
			tab.GetText = () => resolvedLabel;
			tab.IsHighlighted = () => activePanel == id;
			tab.OnClick = () =>
			{
				needsRestart |= leavePanelActions[activePanel]();
				Game.Settings.Save();
				activePanel = id;
			};

			tabContainer.AddChild(tab);
			buttons.Add(tab);

			return tab;
		}
	}
}
