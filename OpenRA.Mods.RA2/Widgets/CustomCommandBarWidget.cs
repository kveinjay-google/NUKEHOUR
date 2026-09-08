#region Copyright & License Information
/*
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

namespace OpenRA.Mods.RA2.Widgets
{
	public class CustomCommandBarWidget : BackgroundWidget
	{
		const int Pad = 6;
		const int Gap = 2;
		const int ExpandedButtonHeight = 52;
		const int DesktopCompactButtonHeight = 28;
		const int DesktopCompactButtonWidth = 34;
		const int TouchCompactButtonSize = 44;
		const int EditDeadzone = 8;
		const int RowGap = 6;
		const int EditButtonWidth = 72;
		const int DesktopCollapseButtonWidth = 34;

		readonly Dictionary<string, ButtonWidget> buttons = new();
		readonly Dictionary<string, Action> originalClicks = new();
		readonly Dictionary<string, Action<MouseInput>> originalMouseDowns = new();
		readonly Dictionary<string, LabelWidget> labels = new();
		readonly Dictionary<string, ImageWidget> icons = new();

		List<string> visibleOrder = new();
		bool editMode;
		bool expanded;
		bool setupDone;
		string lastTouchSkin;
		bool lastTouchExpanded;
		bool touchLayoutInitialized;
		Size lastTouchResolution;
		Size lastTouchNativePointSize;
		Rectangle lastTouchSafeBounds;

		string dragId;
		int2 dragStart;
		bool dragMoved;

		ButtonWidget editButton;
		ButtonWidget collapseButton;
		ImageWidget collapseIcon;
		LabelWidget editHint;

		public CustomCommandBarWidget()
		{
			IsVisible = () => !Platform.IsIOS || IosFloatingControlsPreferences.Visible;
		}

		void EnsureSetup()
		{
			if (setupDone)
				return;

			var slots = GetOrNull("COMMAND_SLOTS");
			if (slots == null)
				return;

			foreach (var def in CommandBarCatalog.All)
			{
				var button = slots.GetOrNull<ButtonWidget>(def.Id);
				if (button == null)
					continue;

				buttons[def.Id] = button;
				labels[def.Id] = button.GetOrNull<LabelWidget>("LABEL");
				icons[def.Id] = button.GetOrNull<ImageWidget>("ICON");
			}

			if (buttons.Count == 0)
				return;

			editButton = GetOrNull<ButtonWidget>("EDIT_TOGGLE");
			collapseButton = GetOrNull<ButtonWidget>("COLLAPSE_TOGGLE");
			editHint = GetOrNull<LabelWidget>("EDIT_HINT");

			if (editButton != null)
			{
				editButton.OnClick = ToggleEditMode;
				editButton.IsHighlighted = () => editMode;
			}

			if (collapseButton != null)
			{
				collapseIcon = collapseButton.GetOrNull<ImageWidget>("ICON");
				collapseButton.OnClick = ToggleExpanded;
				collapseButton.IsHighlighted = () => !expanded;
				collapseButton.GetText = () => "";
				if (collapseIcon != null)
					collapseIcon.GetImageName = () => expanded ? "collapse" : "expand";
				collapseButton.GetTooltipText = () => expanded
					? FluentProvider.GetMessage("button-command-bar-collapse.tooltip")
					: FluentProvider.GetMessage("button-command-bar-expand.tooltip");
			}

			var prefs = CommandBarPreferences.Load();
			visibleOrder = CommandBarLayoutPolicy.NormalizeVisibleOrder(
				prefs.VisibleOrder, UseTouchLayout, prefs.Version).Where(id => buttons.ContainsKey(id)).ToList();
			if (visibleOrder.Count == 0)
				visibleOrder = CommandBarLayoutPolicy.NormalizeVisibleOrder(
					CommandBarCatalog.DefaultVisibleIds(), UseTouchLayout, CommandBarLayoutPolicy.CurrentVersion)
					.Where(id => buttons.ContainsKey(id)).ToList();

			expanded = prefs.Expanded;

			setupDone = true;
			ApplyLayout(false);
		}

		void ToggleExpanded()
		{
			if (editMode)
				ExitEditMode(true);

			expanded = !expanded;
			CommandBarPreferences.Save(visibleOrder, expanded);
			ApplyLayout(false);
		}

		void ToggleEditMode()
		{
			if (!expanded)
			{
				expanded = true;
				CommandBarPreferences.Save(visibleOrder, expanded);
			}

			if (editMode)
				ExitEditMode(true);
			else
				EnterEditMode();
		}

		void EnterEditMode()
		{
			editMode = true;
			foreach (var kv in buttons)
			{
				originalClicks[kv.Key] = kv.Value.OnClick;
				originalMouseDowns[kv.Key] = kv.Value.OnMouseDown;

				var id = kv.Key;
				kv.Value.OnMouseDown = _ =>
				{
					dragId = id;
					dragStart = Viewport.LastMousePos;
					dragMoved = false;
				};
				kv.Value.OnClick = () =>
				{
					if (!dragMoved)
						ToggleSlot(id);
				};
			}

			ApplyLayout(true);
		}

		void ExitEditMode(bool save)
		{
			editMode = false;
			dragId = null;
			dragMoved = false;

			foreach (var kv in buttons)
			{
				if (originalClicks.TryGetValue(kv.Key, out var click))
					kv.Value.OnClick = click;
				if (originalMouseDowns.TryGetValue(kv.Key, out var down))
					kv.Value.OnMouseDown = down;
			}

			originalClicks.Clear();
			originalMouseDowns.Clear();

			if (save)
				CommandBarPreferences.Save(visibleOrder, expanded);

			ApplyLayout(false);
		}

		void ToggleSlot(string id)
		{
			if (visibleOrder.Contains(id))
			{
				if (!CommandBarLayoutPolicy.CanHide(id, UseTouchLayout))
					return;

				if (visibleOrder.Count <= 1)
					return;
				visibleOrder.Remove(id);
			}
			else
				visibleOrder.Add(id);

			ApplyLayout(true);
		}

		IEnumerable<string> HiddenIds()
		{
			return CommandBarLayoutPolicy.AvailableIds(CommandBarCatalog.All.Select(s => s.Id), UseTouchLayout)
				.Where(id => buttons.ContainsKey(id) && !visibleOrder.Contains(id));
		}

		static readonly string[] DesktopCompactIds =
		{
			"ATTACK_MOVE", "FORCE_MOVE", "FORCE_ATTACK", "GUARD",
			"DEPLOY", "SCATTER", "STOP", "QUEUE_ORDERS",
			"STANCE_ATTACKANYTHING", "STANCE_DEFEND", "STANCE_RETURNFIRE", "STANCE_HOLDFIRE"
		};

		static readonly string[] TouchCompactIds =
		{
			"GROUP_01", "GROUP_02", "GROUP_03", "GROUP_04", "GROUP_05", "PRODUCTION_X5",
			"ATTACK_MOVE", "SCATTER", "QUEUE_ORDERS", "STOP", "CYCLE_BASE"
		};

		static bool UseTouchLayout => OperatingSystem.IsIOS();
		static int CompactButtonHeight => UseTouchLayout ? TouchCompactButtonSize : DesktopCompactButtonHeight;
		static int CompactButtonWidth => UseTouchLayout ? TouchCompactButtonSize : DesktopCompactButtonWidth;
		static int CollapseButtonWidth => UseTouchLayout ? TouchCompactButtonSize : DesktopCollapseButtonWidth;

		public static string PanelBackgroundFor(bool isIos, bool expanded, string skin) =>
			isIos ? TouchFactionSkin.QuickbarPanelCollection(skin, expanded) :
			expanded ? "commandbar-panel" : "commandbar-panel-compact";

		public static string ButtonBackgroundFor(bool isIos, string desktopBackground, string skin) =>
			isIos ? TouchFactionSkin.QuickbarButtonCollection(skin) : desktopBackground;

		public static string ToggleCollectionFor(bool isIos, string desktopCollection, string skin) =>
			isIos ? TouchFactionSkin.QuickbarToggleCollection(skin) : desktopCollection;

		public static bool ShouldRefreshTouchChrome(
			bool isIos, string previousSkin, bool previousExpanded, string skin, bool expanded) =>
			isIos && (previousSkin != skin || previousExpanded != expanded);

		public static bool ShouldRefreshTouchLayout(
			bool isIos,
			bool initialized,
			Size previousResolution,
			Size previousNativePointSize,
			Rectangle previousSafeBounds,
			IosScreenSnapshot snapshot) =>
			isIos && (!initialized ||
				previousResolution != snapshot.EffectiveSize ||
				previousNativePointSize != snapshot.NativePointSize ||
				previousSafeBounds != snapshot.SafeBounds);

		public static void ApplyTouchChromeToTargets<T>(
			string skin,
			bool expanded,
			IEnumerable<T> buttons,
			Action<string> applyPanelBackground,
			Action<T, string> applyButtonBackground,
			Action<string> applyToggleCollection)
		{
			if (buttons == null)
				throw new ArgumentNullException(nameof(buttons));
			if (applyPanelBackground == null)
				throw new ArgumentNullException(nameof(applyPanelBackground));
			if (applyButtonBackground == null)
				throw new ArgumentNullException(nameof(applyButtonBackground));
			if (applyToggleCollection == null)
				throw new ArgumentNullException(nameof(applyToggleCollection));

			applyPanelBackground(TouchFactionSkin.QuickbarPanelCollection(skin, expanded));
			var buttonBackground = TouchFactionSkin.QuickbarButtonCollection(skin);
			foreach (var button in buttons)
				applyButtonBackground(button, buttonBackground);
			applyToggleCollection(TouchFactionSkin.QuickbarToggleCollection(skin));
		}

		public static IEnumerable<T> TouchChromeTargets<T>(
			IEnumerable<T> slotButtons, T editButton, T collapseButton)
			where T : class
		{
			if (slotButtons == null)
				throw new ArgumentNullException(nameof(slotButtons));

			var targets = new List<T>(slotButtons);
			if (editButton != null)
				targets.Add(editButton);
			if (collapseButton != null)
				targets.Add(collapseButton);

			return targets;
		}

		int ButtonHeight => UseTouchLayout
			? TouchCommandBarSlotPolicy.Create(
				IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution), "STOP", expanded).ButtonSize.Height
			: expanded ? ExpandedButtonHeight : CompactButtonHeight;

		int SlotWidth(string id)
		{
			return UseTouchLayout
				? TouchCommandBarSlotPolicy.Create(
					IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution), id, expanded).ButtonSize.Width
				: expanded ? CommandBarCatalog.Get(id).Width : CompactButtonWidth;
		}

		IEnumerable<string> ActiveIds()
		{
			if (expanded)
				return CommandBarLayoutPolicy.AvailableIds(visibleOrder, UseTouchLayout);

			// Compact strip mirrors the classic unit-command bar.
			var compactIds = UseTouchLayout ? TouchCompactIds : DesktopCompactIds;
			var compact = compactIds.Where(id => buttons.ContainsKey(id) && visibleOrder.Contains(id)).ToList();
			if (compact.Count == 0)
				compact = compactIds.Where(id => buttons.ContainsKey(id)).ToList();

			return compact;
		}

		public override void Tick()
		{
			base.Tick();
			EnsureSetup();
			if (setupDone)
				RefreshTouchChromeIfNeeded();

			if (setupDone && UseTouchLayout)
			{
				var snapshot = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
				if (ShouldRefreshTouchLayout(
					true,
					touchLayoutInitialized,
					lastTouchResolution,
					lastTouchNativePointSize,
					lastTouchSafeBounds,
					snapshot))
					ApplyTouchLayout(snapshot, editMode);
			}

			if (!setupDone || !editMode || dragId == null)
				return;

			if (!buttons.TryGetValue(dragId, out var button) || !button.HasMouseFocus)
			{
				if (dragMoved)
					CommandBarPreferences.Save(visibleOrder, expanded);
				dragId = null;
				dragMoved = false;
				ApplyLayout(editMode);
				return;
			}

			var mouse = Viewport.LastMousePos;
			if (!dragMoved && (mouse - dragStart).Length <= EditDeadzone)
				return;

			dragMoved = true;
			var localX = mouse.X - RenderOrigin.X;
			var onActiveRow = mouse.Y < RenderOrigin.Y + Pad + ButtonHeight + RowGap / 2;

			if (onActiveRow)
			{
				visibleOrder.Remove(dragId);
				var insertAt = InsertIndexFromLocalX(localX);
				visibleOrder.Insert(insertAt, dragId);
			}
			else
			{
				if (!CommandBarLayoutPolicy.CanHide(dragId, UseTouchLayout))
				{
					ApplyLayout(true);
					return;
				}

				visibleOrder.Remove(dragId);
			}

			ApplyLayout(true, dragId);
			button.Bounds.X = Math.Max(Pad, mouse.X - RenderOrigin.X - button.Bounds.Width / 2);
			button.Bounds.Y = onActiveRow ? Pad : Pad + ButtonHeight + RowGap;
		}

		int InsertIndexFromLocalX(int localX)
		{
			var x = Pad;
			for (var i = 0; i < visibleOrder.Count; i++)
			{
				if (!buttons.ContainsKey(visibleOrder[i]))
					continue;

				var width = SlotWidth(visibleOrder[i]);
				if (localX < x + width / 2)
					return i;
				x += width + Gap;
			}

			return visibleOrder.Count;
		}

		IEnumerable<string> ActiveTouchIds()
		{
			if (expanded)
				return CommandBarLayoutPolicy.AvailableIds(visibleOrder, true);

			var compact = TouchCompactIds
				.Where(id => buttons.ContainsKey(id) && visibleOrder.Contains(id)).ToList();
			if (compact.Count == 0)
				compact = TouchCompactIds.Where(id => buttons.ContainsKey(id)).ToList();

			return compact;
		}

		IEnumerable<string> HiddenTouchIds()
		{
			return CommandBarLayoutPolicy.AvailableIds(CommandBarCatalog.All.Select(slot => slot.Id), true)
				.Where(id => buttons.ContainsKey(id) && !visibleOrder.Contains(id));
		}

		void ApplyTouchLayout(IosScreenSnapshot snapshot, bool editing, string skipId = null)
		{
			foreach (var button in buttons.Values)
				button.IsVisible = () => false;

			var rowHeight = TouchCommandBarSlotPolicy.Create(snapshot, "STOP", expanded).ButtonSize.Height;
			var x = Pad;
			foreach (var id in ActiveTouchIds())
			{
				if (!buttons.TryGetValue(id, out var button))
					continue;

				var slot = TouchCommandBarSlotPolicy.Create(snapshot, id, expanded);
				button.IsVisible = () => true;
				if (id != skipId)
				{
					button.Bounds.X = x;
					button.Bounds.Y = Pad;
				}

				button.Bounds.Width = slot.ButtonSize.Width;
				button.Bounds.Height = rowHeight;
				LayoutTouchSlotChrome(id, slot);
				x += slot.ButtonSize.Width + Gap;
			}

			var collapseWidth = snapshot.LogicalPoints(44);
			var editWidth = snapshot.LogicalPoints(72);
			var controlsWidth = collapseWidth + Gap + (expanded ? editWidth + Gap : 0);
			var activeWidth = x + Pad + controlsWidth;

			var trayX = Pad;
			if (editing && expanded)
			{
				foreach (var id in HiddenTouchIds())
				{
					if (!buttons.TryGetValue(id, out var button))
						continue;

					var slot = TouchCommandBarSlotPolicy.Create(snapshot, id, true);
					button.IsVisible = () => true;
					if (id != skipId)
					{
						button.Bounds.X = trayX;
						button.Bounds.Y = Pad + rowHeight + RowGap;
					}

					button.Bounds.Width = slot.ButtonSize.Width;
					button.Bounds.Height = rowHeight;
					LayoutTouchSlotChrome(id, slot);
					trayX += slot.ButtonSize.Width + Gap;
				}
			}

			var trayWidth = editing && expanded ? trayX + Pad : 0;
			var totalWidth = Math.Max(Math.Max(activeWidth, trayWidth), 360);
			var totalHeight = editing && expanded
				? Pad + rowHeight + RowGap + rowHeight + Pad + 16
				: Pad + rowHeight + Pad;
			var placed = TouchCommandBarSlotPolicy.PlaceBar(snapshot, new Size(totalWidth, totalHeight));
			Bounds.X = placed.X;
			Bounds.Y = placed.Y;
			Bounds.Width = placed.Width;
			Bounds.Height = placed.Height;

			if (collapseButton != null)
			{
				collapseButton.Bounds.Width = collapseWidth;
				collapseButton.Bounds.Height = rowHeight;
				collapseButton.Bounds.X = placed.Width - Pad - collapseWidth;
				collapseButton.Bounds.Y = Pad;
				collapseButton.IsVisible = () => true;
				if (collapseIcon != null)
				{
					var iconSize = snapshot.LogicalPoints(26);
					collapseIcon.Bounds.X = (collapseWidth - iconSize) / 2;
					collapseIcon.Bounds.Y = (rowHeight - iconSize) / 2;
					collapseIcon.Bounds.Width = iconSize;
					collapseIcon.Bounds.Height = iconSize;
					collapseIcon.StretchToFit = true;
				}
			}

			if (editButton != null)
			{
				editButton.Bounds.Width = editWidth;
				editButton.Bounds.Height = rowHeight;
				editButton.Bounds.X = placed.Width - Pad - collapseWidth - Gap - editWidth;
				editButton.Bounds.Y = Pad;
				editButton.IsVisible = () => expanded;
			}

			if (editHint != null)
			{
				editHint.IsVisible = () => editing && expanded;
				editHint.Bounds.X = Pad;
				editHint.Bounds.Y = placed.Height - 18;
				editHint.Bounds.Width = Math.Max(0, placed.Width - 2 * Pad);
				editHint.Bounds.Height = 16;
			}

			var slots = GetOrNull("COMMAND_SLOTS");
			if (slots != null)
			{
				slots.Bounds.X = 0;
				slots.Bounds.Y = 0;
				slots.Bounds.Width = placed.Width;
				slots.Bounds.Height = placed.Height;
			}

			touchLayoutInitialized = true;
			lastTouchResolution = snapshot.EffectiveSize;
			lastTouchNativePointSize = snapshot.NativePointSize;
			lastTouchSafeBounds = snapshot.SafeBounds;
		}

		void ApplyLayout(bool editing, string skipId = null)
		{
			if (UseTouchLayout)
			{
				RefreshTouchChromeIfNeeded();
				ApplyTouchLayout(IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution), editing, skipId);
				return;
			}

			Background = PanelBackgroundFor(false, expanded, null);

			foreach (var kv in buttons)
				kv.Value.IsVisible = () => false;

			var x = Pad;
			foreach (var id in ActiveIds())
			{
				if (!buttons.TryGetValue(id, out var button))
					continue;

				var width = SlotWidth(id);
				button.IsVisible = () => true;
				if (id != skipId)
				{
					button.Bounds.X = x;
					button.Bounds.Y = Pad;
				}

				button.Bounds.Width = width;
				button.Bounds.Height = ButtonHeight;
				LayoutSlotChrome(id, width);
				x += width + Gap;
			}

			var controlsWidth = CollapseButtonWidth + Gap + (expanded ? EditButtonWidth + Gap : 0);
			var activeWidth = x + Pad + controlsWidth;

			var trayX = Pad;
			if (editing && expanded)
			{
				foreach (var id in HiddenIds())
				{
					if (!buttons.TryGetValue(id, out var button))
						continue;

					var width = SlotWidth(id);
					button.IsVisible = () => true;
					if (id != skipId)
					{
						button.Bounds.X = trayX;
						button.Bounds.Y = Pad + ButtonHeight + RowGap;
					}

					button.Bounds.Width = width;
					button.Bounds.Height = ButtonHeight;
					LayoutSlotChrome(id, width);
					trayX += width + Gap;
				}
			}

			var trayWidth = editing && expanded ? trayX + Pad : 0;
			var minWidth = expanded ? 360 : 220;
			var totalWidth = Math.Max(Math.Max(activeWidth, trayWidth), minWidth);
			var totalHeight = editing && expanded
				? Pad + ButtonHeight + RowGap + ButtonHeight + Pad + 16
				: Pad + ButtonHeight + Pad;

			Bounds.Width = totalWidth;
			Bounds.Height = totalHeight;
			Bounds.X = (Game.Renderer.Resolution.Width - totalWidth) / 2;
			Bounds.Y = Game.Renderer.Resolution.Height - totalHeight - 12;

			if (collapseButton != null)
			{
				collapseButton.Bounds.Width = CollapseButtonWidth;
				collapseButton.Bounds.Height = UseTouchLayout ? TouchCompactButtonSize : Math.Min(34, ButtonHeight);
				collapseButton.Bounds.X = totalWidth - Pad - CollapseButtonWidth;
				collapseButton.Bounds.Y = Pad + Math.Max(0, (ButtonHeight - collapseButton.Bounds.Height) / 2);
				collapseButton.IsVisible = () => true;
				if (collapseIcon != null)
				{
					collapseIcon.Bounds.X = (collapseButton.Bounds.Width - 26) / 2;
					collapseIcon.Bounds.Y = (collapseButton.Bounds.Height - 26) / 2;
				}
			}

			if (editButton != null)
			{
				editButton.Bounds.Width = EditButtonWidth;
				editButton.Bounds.Height = 34;
				editButton.Bounds.X = totalWidth - Pad - CollapseButtonWidth - Gap - EditButtonWidth;
				editButton.Bounds.Y = Pad + Math.Max(0, (ButtonHeight - 34) / 2);
				editButton.IsVisible = () => expanded;
			}

			if (editHint != null)
			{
				editHint.IsVisible = () => editing && expanded;
				editHint.Bounds.X = Pad;
				editHint.Bounds.Y = totalHeight - 18;
				editHint.Bounds.Width = Math.Max(0, totalWidth - 2 * Pad);
				editHint.Bounds.Height = 16;
			}

			var slots = GetOrNull("COMMAND_SLOTS");
			if (slots != null)
			{
				slots.Bounds.X = 0;
				slots.Bounds.Y = 0;
				slots.Bounds.Width = totalWidth;
				slots.Bounds.Height = totalHeight;
			}
		}

		void RefreshTouchChromeIfNeeded()
		{
			RefreshTouchChrome(UseTouchLayout);
		}

		void RefreshTouchChrome(bool isIos)
		{
			if (!isIos)
				return;

			var skin = TouchFactionSkin.Active;
			if (ShouldRefreshTouchChrome(true, lastTouchSkin, lastTouchExpanded, skin, expanded))
				ApplyTouchChrome();
		}

		IEnumerable<ButtonWidget> TouchChromeButtons()
		{
			return TouchChromeTargets(buttons.Values, editButton, collapseButton);
		}

		void ApplyTouchChrome()
		{
			var skin = TouchFactionSkin.Active;
			ApplyTouchChromeToTargets(
				skin,
				expanded,
				TouchChromeButtons(),
				background => Background = background,
				(button, background) => button.Background = background,
				collection =>
				{
					if (collapseIcon != null)
						collapseIcon.ImageCollection = collection;
				});

			lastTouchSkin = skin;
			lastTouchExpanded = expanded;
		}

		void LayoutTouchSlotChrome(string id, TouchCommandBarSlotLayout slot)
		{
			if (labels.TryGetValue(id, out var label) && label != null)
			{
				var showLabel = slot.ShowLabel;
				label.IsVisible = () => showLabel;
				label.Bounds.X = slot.LabelBounds.X;
				label.Bounds.Y = slot.LabelBounds.Y;
				label.Bounds.Width = slot.LabelBounds.Width;
				label.Bounds.Height = slot.LabelBounds.Height;
				label.Font = slot.Font;
			}

			if (icons.TryGetValue(id, out var icon) && icon != null)
			{
				icon.Bounds.X = slot.IconBounds.X;
				icon.Bounds.Y = slot.IconBounds.Y;
				icon.Bounds.Width = slot.IconBounds.Width;
				icon.Bounds.Height = slot.IconBounds.Height;
				icon.StretchToFit = CommandBarLayoutPolicy.IsRequiredTouchId(id, true);
			}
		}

		void LayoutSlotChrome(string id, int width)
		{
			if (labels.TryGetValue(id, out var label) && label != null)
			{
				var showLabel = expanded;
				label.IsVisible = () => showLabel;
				label.Bounds.Y = 36;
				label.Bounds.Width = width;
				label.Bounds.Height = 14;
			}

			if (icons.TryGetValue(id, out var icon) && icon != null)
			{
				icon.Bounds.X = Math.Max(0, (width - 26) / 2);
				icon.Bounds.Y = expanded ? 6 : Math.Max(1, (CompactButtonHeight - 26) / 2);
			}
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			EnsureSetup();

			if (editMode && expanded && mi.Button == MouseButton.Right && mi.Event == MouseInputEvent.Up)
			{
				var id = HitTestSlotId(mi.Location);
				if (id != null)
				{
					ToggleSlot(id);
					return true;
				}
			}

			return base.HandleMouseInput(mi);
		}

		string HitTestSlotId(int2 screenPos)
		{
			foreach (var kv in buttons)
			{
				if (!kv.Value.IsVisible())
					continue;
				if (kv.Value.RenderBounds.Contains(screenPos))
					return kv.Key;
			}

			return null;
		}
	}
}
