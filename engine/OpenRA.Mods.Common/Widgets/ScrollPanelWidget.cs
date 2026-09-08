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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public interface ILayout
	{
		void AdjustChild(Widget w);
		void AdjustChildren();
	}

	public enum ScrollPanelAlign
	{
		Bottom,
		Top
	}

	public enum ScrollBar
	{
		Left,
		Right,
		Hidden
	}

	public class ScrollPanelWidget : Widget
	{
		readonly Ruleset modRules;
		public int ScrollbarWidth = 24;
		public int BorderWidth = 1;
		public int TopBottomSpacing = 2;
		public int ItemSpacing = 0;
		public int ButtonDepth = ChromeMetrics.Get<int>("ButtonDepth");
		public string ClickSound = ChromeMetrics.Get<string>("ClickSound");
		public string ClickDisabledSound = ChromeMetrics.Get<string>("ClickDisabledSound");
		public string Background = "scrollpanel-bg";
		public string ScrollBarBackground = "scrollpanel-bg";
		public string Button = "scrollpanel-button";
		public string Decorations = "scrollpanel-decorations";
		public readonly string DecorationScrollUp = "up";
		public readonly string DecorationScrollDown = "down";
		readonly CachedTransform<(bool Disabled, bool Pressed, bool Hover, bool Focused, bool Highlighted), Sprite> getUpArrowImage;
		readonly CachedTransform<(bool Disabled, bool Pressed, bool Hover, bool Focused, bool Highlighted), Sprite> getDownArrowImage;
		public int ContentHeight;
		public ILayout Layout;
		public int MinimumThumbSize = 10;
		public ScrollPanelAlign Align = ScrollPanelAlign.Top;
		public ScrollBar ScrollBar = ScrollBar.Right;
		public bool CollapseHiddenChildren;
		public bool EnableContentDragging = false;
		public int ContentDragThreshold = 8;

		// Fraction of the remaining scroll-delta to move in 40ms
		public float SmoothScrollSpeed = 0.333f;

		protected bool upPressed;
		protected bool downPressed;
		protected bool upDisabled;
		protected bool downDisabled;
		protected bool thumbPressed;
		protected Rectangle upButtonRect;
		protected Rectangle downButtonRect;
		protected Rectangle backgroundRect;
		protected Rectangle scrollbarRect;
		protected Rectangle thumbRect;

		// The target value is the list offset we're trying to reach
		float targetListOffset;

		// The current value is the actual list offset at the moment
		float currentListOffset;
		bool contentDragCandidate;
		bool contentDragging;
		int contentDragStartY;
		float contentDragStartOffset;

		// The Game.Runtime value when UpdateSmoothScrolling was last called
		// Used for calculating the per-frame smooth-scrolling delta
		long lastSmoothScrollTime = 0;

		// Setting "smooth" to true will only update the target list offset.
		// Setting "smooth" to false will also set the current list offset,
		// i.e. it will scroll immediately.
		//
		// For example, scrolling with the mouse wheel will use smooth
		// scrolling to give a nice visual effect that makes it easier
		// for the user to follow. Dragging the scrollbar's thumb, however,
		// will scroll to the desired position immediately.
		protected void SetListOffset(float value, bool smooth)
		{
			targetListOffset = value;
			if (!smooth)
			{
				var oldListOffset = currentListOffset;
				currentListOffset = value;

				// Update mouseover
				if (oldListOffset != currentListOffset)
					Ui.ResetTooltips();
			}
		}

		[ObjectCreator.UseCtor]
		public ScrollPanelWidget(ModData modData)
		{
			modRules = modData.DefaultRules;

			Layout = new ListLayout(this);

			getUpArrowImage = WidgetUtils.GetCachedStatefulImage(Decorations, DecorationScrollUp);
			getDownArrowImage = WidgetUtils.GetCachedStatefulImage(Decorations, DecorationScrollDown);
		}

		public override void RemoveChildren()
		{
			ContentHeight = 0;
			base.RemoveChildren();
			Scroll(0);
		}

		public override void AddChild(Widget child)
		{
			// Initial setup of margins/height
			Layout.AdjustChild(child);
			base.AddChild(child);
		}

		public override void RemoveChild(Widget child)
		{
			base.RemoveChild(child);
			Layout.AdjustChildren();
			Scroll(0);
		}

		public void ReplaceChild(Widget oldChild, Widget newChild)
		{
			oldChild.Removed();
			newChild.Parent = this;
			Children[Children.IndexOf(oldChild)] = newChild;
			Layout.AdjustChildren();
			Scroll(0);
		}

		public override void DrawOuter()
		{
			if (!IsVisible())
				return;

			UpdateSmoothScrolling();

			var rb = RenderBounds;
			var geometry = IosTouchWidgetPolicy.ScrollGeometry(
				rb.Width, rb.Height, ScrollbarWidth, ContentHeight, MinimumThumbSize);
			var scrollbarWidth = geometry.ButtonSize;
			var scrollbarHeight = geometry.TrackHeight;

			// Scroll thumb is only visible if the content does not fit within the panel bounds
			var thumbHeight = geometry.ThumbHeight;
			var thumbOrigin = rb.Y + scrollbarWidth;
			if (thumbHeight > 0 && rb.Height != ContentHeight)
			{
				thumbOrigin += (int)((scrollbarHeight - thumbHeight) * currentListOffset / (rb.Height - ContentHeight));
			}

			switch (ScrollBar)
			{
				case ScrollBar.Left:
					backgroundRect = new Rectangle(rb.X + scrollbarWidth, rb.Y,
						Math.Max(0, rb.Width - scrollbarWidth + 1), Math.Max(0, rb.Height));
					upButtonRect = new Rectangle(rb.X, rb.Y, scrollbarWidth, scrollbarWidth);
					downButtonRect = new Rectangle(rb.X, rb.Bottom - scrollbarWidth, scrollbarWidth, scrollbarWidth);
					scrollbarRect = new Rectangle(rb.X, rb.Y + Math.Max(0, scrollbarWidth - 1), scrollbarWidth,
						scrollbarHeight + (scrollbarWidth > 0 ? 2 : 0));
					thumbRect = new Rectangle(rb.X, thumbOrigin, scrollbarWidth, thumbHeight);
					break;
				case ScrollBar.Right:
					backgroundRect = new Rectangle(rb.X, rb.Y, Math.Max(0, rb.Width - scrollbarWidth + 1), Math.Max(0, rb.Height));
					upButtonRect = new Rectangle(rb.Right - scrollbarWidth, rb.Y, scrollbarWidth, scrollbarWidth);
					downButtonRect = new Rectangle(rb.Right - scrollbarWidth, rb.Bottom - scrollbarWidth, scrollbarWidth, scrollbarWidth);
					scrollbarRect = new Rectangle(rb.Right - scrollbarWidth, rb.Y + Math.Max(0, scrollbarWidth - 1), scrollbarWidth,
						scrollbarHeight + (scrollbarWidth > 0 ? 2 : 0));
					thumbRect = new Rectangle(rb.Right - scrollbarWidth, thumbOrigin, scrollbarWidth, thumbHeight);
					break;
				case ScrollBar.Hidden:
					backgroundRect = new Rectangle(rb.X, rb.Y, Math.Max(0, rb.Width + 1), Math.Max(0, rb.Height));
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			WidgetUtils.DrawPanel(Background, backgroundRect);

			if (ScrollBar != ScrollBar.Hidden)
			{
				var upHover = Ui.MouseOverWidget == this && upButtonRect.Contains(Viewport.LastMousePos);
				upDisabled = thumbHeight == 0 || currentListOffset >= 0;

				var downHover = Ui.MouseOverWidget == this && downButtonRect.Contains(Viewport.LastMousePos);
				downDisabled = thumbHeight == 0 || currentListOffset <= Bounds.Height - ContentHeight;

				var thumbHover = Ui.MouseOverWidget == this && thumbRect.Contains(Viewport.LastMousePos);
				WidgetUtils.DrawPanel(ScrollBarBackground, scrollbarRect);
				ButtonWidget.DrawBackground(Button, upButtonRect, upDisabled, upPressed, upHover, false);
				ButtonWidget.DrawBackground(Button, downButtonRect, downDisabled, downPressed, downHover, false);

				if (thumbHeight > 0)
					ButtonWidget.DrawBackground(Button, thumbRect, false, HasMouseFocus && thumbHover, thumbHover, false);

				var upArrowImage = getUpArrowImage.Update((upDisabled, upPressed, upHover, false, false));
				var upOrigin = IosTouchWidgetPolicy.CenteredDecorationOrigin(
					upButtonRect, new Size((int)upArrowImage.Size.X, (int)upArrowImage.Size.Y),
					!upPressed || upDisabled ? 0 : ButtonDepth);
				WidgetUtils.DrawSprite(upArrowImage,
					new float2(upOrigin.X, upOrigin.Y));

				var downArrowImage = getDownArrowImage.Update((downDisabled, downPressed, downHover, false, false));
				var downOrigin = IosTouchWidgetPolicy.CenteredDecorationOrigin(
					downButtonRect, new Size((int)downArrowImage.Size.X, (int)downArrowImage.Size.Y),
					!downPressed || downDisabled ? 0 : ButtonDepth);
				WidgetUtils.DrawSprite(downArrowImage,
					new float2(downOrigin.X, downOrigin.Y));
			}

			var horizontalBorder = Math.Min(Math.Max(0, BorderWidth), backgroundRect.Width / 2);
			var verticalBorder = Math.Min(Math.Max(0, BorderWidth), backgroundRect.Height / 2);
			var drawBounds = backgroundRect.InflateBy(
				-horizontalBorder, -verticalBorder, -horizontalBorder, -verticalBorder);
			Game.Renderer.EnableScissor(drawBounds);

			// ChildOrigin enumerates the widget tree, so only evaluate it once
			var co = ChildOrigin;
			drawBounds = new Rectangle(drawBounds.X - co.X, drawBounds.Y - co.Y, drawBounds.Width, drawBounds.Height);

			foreach (var child in Children)
				if (child.Bounds.ToRectangle().IntersectsWith(drawBounds))
					child.DrawOuter();

			Game.Renderer.DisableScissor();
		}

		public override int2 ChildOrigin
		{
			get
			{
				var rb = RenderBounds;
				var scrollbarWidth = IosTouchWidgetPolicy.ScrollGeometry(
					rb.Width, rb.Height, ScrollbarWidth, ContentHeight, MinimumThumbSize).ButtonSize;
				return RenderOrigin + new int2(ScrollBar == ScrollBar.Left ? scrollbarWidth : 0, (int)currentListOffset);
			}
		}

		public override bool EventBoundsContains(int2 location)
		{
			return EventBounds.Contains(location);
		}

		void Scroll(int amount, bool smooth = false)
		{
			var newTarget = targetListOffset + amount * Game.Settings.Game.UIScrollSpeed;
			newTarget = Math.Min(0, Math.Max(Bounds.Height - ContentHeight, newTarget));

			SetListOffset(newTarget, smooth);
		}

		public void ScrollToBottom(bool smooth = false)
		{
			var value = Align == ScrollPanelAlign.Top ?
				Math.Min(0, Bounds.Height - ContentHeight) :
				Bounds.Height - ContentHeight;

			SetListOffset(value, smooth);
		}

		public void ScrollToTop(bool smooth = false)
		{
			var value = Align == ScrollPanelAlign.Top ? 0 :
				Math.Max(0, Bounds.Height - ContentHeight);

			SetListOffset(value, smooth);
		}

		public bool ScrolledToBottom => targetListOffset == Math.Min(0, Bounds.Height - ContentHeight) || ContentHeight <= Bounds.Height;

		void ScrollToItem(Widget item, bool smooth = false)
		{
			// Scroll the item to be visible
			float? newOffset = null;
			if (item.Bounds.Top + currentListOffset < 0)
				newOffset = ItemSpacing - item.Bounds.Top;

			if (item.Bounds.Bottom + currentListOffset > RenderBounds.Height)
				newOffset = RenderBounds.Height - item.Bounds.Bottom - ItemSpacing;

			if (newOffset.HasValue)
				SetListOffset(newOffset.Value, smooth);
		}

		public void ScrollToItem(string itemKey, bool smooth = false)
		{
			var item = Children.FirstOrDefault(c => c is ScrollItemWidget si && si.ItemKey == itemKey);

			if (item != null)
				ScrollToItem(item, smooth);
		}

		public void ScrollToSelectedItem()
		{
			var item = Children.FirstOrDefault(c => c is ScrollItemWidget si && si.IsSelected());

			if (item != null)
				ScrollToItem(item);
		}

		void UpdateSmoothScrolling()
		{
			if (lastSmoothScrollTime == 0)
			{
				lastSmoothScrollTime = Game.RunTime;
				return;
			}

			var offsetDiff = targetListOffset - currentListOffset;
			var absOffsetDiff = Math.Abs(offsetDiff);
			if (absOffsetDiff > 1f)
			{
				var dt = Game.RunTime - lastSmoothScrollTime;
				currentListOffset += offsetDiff * SmoothScrollSpeed.Clamp(0.1f, 1.0f) * dt / 40;

				Ui.ResetTooltips();
			}
			else
				SetListOffset(targetListOffset, false);

			lastSmoothScrollTime = Game.RunTime;
		}

		public override void Tick()
		{
			if (upPressed)
				Scroll(1);

			if (downPressed)
				Scroll(-1);
		}

		public override bool YieldMouseFocus(MouseInput mi)
		{
			upPressed = downPressed = thumbPressed = false;
			CancelContentDragFromChild();
			return base.YieldMouseFocus(mi);
		}

		public bool BeginContentDragFromChild(MouseInput mi)
		{
			CancelContentDragFromChild();
			if (!EnableContentDragging || ContentHeight <= RenderBounds.Height || !ContentBoundsContains(mi.Location))
				return false;

			contentDragCandidate = true;
			contentDragStartY = mi.Location.Y;
			contentDragStartOffset = currentListOffset;
			return true;
		}

		public bool TryTakeContentDragFromChild(MouseInput mi)
		{
			if (!EnableContentDragging || !contentDragCandidate)
				return false;

			contentDragging |= IosTouchWidgetPolicy.ExceedsDragThreshold(
				contentDragStartY, mi.Location.Y, ContentDragThreshold);
			if (!contentDragging)
				return false;

			if (!TakeMouseFocus(mi))
			{
				contentDragging = false;
				return false;
			}

			var newOffset = IosTouchWidgetPolicy.ClampScrollOffset(
				contentDragStartOffset + mi.Location.Y - contentDragStartY,
				RenderBounds.Height, ContentHeight);
			SetListOffset(newOffset, false);
			return true;
		}

		public void CancelContentDragFromChild()
		{
			contentDragCandidate = contentDragging = false;
		}

		int2 lastMouseLocation;

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event == MouseInputEvent.Scroll)
			{
				Scroll(mi.Delta.Y, true);
				return true;
			}

			if (mi.Button != MouseButton.Left)
				return false;

			if (EnableContentDragging && mi.Event == MouseInputEvent.Down && ContentBoundsContains(mi.Location))
			{
				BeginContentDragFromChild(mi);
				if (!contentDragCandidate)
					return false;

				if (!TakeMouseFocus(mi))
				{
					CancelContentDragFromChild();
					return false;
				}

				return true;
			}

			if (EnableContentDragging && contentDragCandidate && HasMouseFocus)
			{
				if (mi.Event == MouseInputEvent.Up)
					return YieldMouseFocus(mi);

				if (mi.Event == MouseInputEvent.Move)
				{
					TryTakeContentDragFromChild(mi);
					return true;
				}
			}

			if (mi.Event == MouseInputEvent.Down && !TakeMouseFocus(mi))
				return false;

			if (!HasMouseFocus)
				return false;

			if (HasMouseFocus && mi.Event == MouseInputEvent.Up)
				return YieldMouseFocus(mi);

			if (thumbPressed && mi.Event == MouseInputEvent.Move)
			{
				var rb = RenderBounds;
				var geometry = IosTouchWidgetPolicy.ScrollGeometry(
					rb.Width, rb.Height, ScrollbarWidth, ContentHeight, MinimumThumbSize);
				var thumbTravel = geometry.TrackHeight - geometry.ThumbHeight;
				var oldOffset = currentListOffset;
				if (thumbTravel > 0)
				{
					var newOffset = currentListOffset +
						(int)((lastMouseLocation.Y - mi.Location.Y) * (ContentHeight - rb.Height) * 1f / thumbTravel);
					newOffset = IosTouchWidgetPolicy.ClampScrollOffset(newOffset, rb.Height, ContentHeight);
					SetListOffset(newOffset, false);
				}

				if (oldOffset != currentListOffset)
					lastMouseLocation = mi.Location;
			}
			else
			{
				upPressed = upButtonRect.Contains(mi.Location);
				downPressed = downButtonRect.Contains(mi.Location);
				thumbPressed = thumbRect.Contains(mi.Location);
				if (thumbPressed)
					lastMouseLocation = mi.Location;

				if (mi.Event == MouseInputEvent.Down)
				{
					if (thumbPressed || (upPressed && !upDisabled) || (downPressed && !downDisabled))
						Game.Sound.PlayNotification(modRules, null, "Sounds", ClickSound, null);
					else if ((upPressed && upDisabled) || (downPressed && downDisabled))
						Game.Sound.PlayNotification(modRules, null, "Sounds", ClickDisabledSound, null);
				}
			}

			return upPressed || downPressed || thumbPressed;
		}

		bool ContentBoundsContains(int2 location)
		{
			var rb = RenderBounds;
			if (!rb.Contains(location))
				return false;

			if (ScrollBar == ScrollBar.Hidden)
				return true;

			var scrollbarWidth = IosTouchWidgetPolicy.ScrollGeometry(
				rb.Width, rb.Height, ScrollbarWidth, ContentHeight, MinimumThumbSize).ButtonSize;
			return ScrollBar == ScrollBar.Left ?
				location.X >= rb.Left + scrollbarWidth : location.X < rb.Right - scrollbarWidth;
		}

		IObservableCollection collection;
		Func<object, Widget> makeWidget;
		Func<Widget, object, bool> widgetItemEquals;
		bool autoScroll;

		public void Unbind()
		{
			Bind(null, null, null, false);
		}

		public void Bind(IObservableCollection c, Func<object, Widget> makeWidget, Func<Widget, object, bool> widgetItemEquals, bool autoScroll)
		{
			this.autoScroll = autoScroll;

			Game.RunAfterTick(() =>
			{
				if (collection != null)
				{
					collection.OnAdd -= BindingAdd;
					collection.OnRemove -= BindingRemove;
					collection.OnRemoveAt -= BindingRemoveAt;
					collection.OnSet -= BindingSet;
					collection.OnRefresh -= BindingRefresh;
				}

				this.makeWidget = makeWidget;
				this.widgetItemEquals = widgetItemEquals;

				RemoveChildren();
				collection = c;

				if (c != null)
				{
					foreach (var item in c.ObservedItems)
						BindingAddImpl(item);

					c.OnAdd += BindingAdd;
					c.OnRemove += BindingRemove;
					c.OnRemoveAt += BindingRemoveAt;
					c.OnSet += BindingSet;
					c.OnRefresh += BindingRefresh;
				}
			});
		}

		void BindingAdd(IObservableCollection col, object item)
		{
			Game.RunAfterTick(() =>
			{
				if (collection != col)
					return;

				BindingAddImpl(item);
			});
		}

		void BindingAddImpl(object item)
		{
			if (makeWidget == null)
				return;

			var widget = makeWidget(item);
			var scrollToBottom = autoScroll && ScrolledToBottom;

			AddChild(widget);

			if (scrollToBottom)
				ScrollToBottom();
		}

		void BindingRemove(IObservableCollection col, object item)
		{
			Game.RunAfterTick(() =>
			{
				if (collection != col)
					return;

				var widget = Children.FirstOrDefault(w => widgetItemEquals(w, item));
				if (widget != null)
					RemoveChild(widget);
			});
		}

		void BindingRemoveAt(IObservableCollection col, int index)
		{
			Game.RunAfterTick(() =>
			{
				if (collection != col)
					return;

				if (index < 0 || index >= Children.Count)
					return;

				RemoveChild(Children[index]);
			});
		}

		void BindingSet(IObservableCollection col, object oldItem, object newItem)
		{
			Game.RunAfterTick(() =>
			{
				if (collection != col)
					return;

				var newWidget = makeWidget(newItem);
				newWidget.Parent = this;

				var i = Children.FindIndex(w => widgetItemEquals(w, oldItem));
				if (i >= 0)
				{
					var oldWidget = Children[i];
					oldWidget.Removed();
					Children[i] = newWidget;
					Layout.AdjustChildren();
				}
				else
					AddChild(newWidget);
			});
		}

		void BindingRefresh(IObservableCollection col)
		{
			Game.RunAfterTick(() =>
			{
				if (collection != col)
					return;

				RemoveChildren();
				foreach (var item in collection.ObservedItems)
					BindingAddImpl(item);
			});
		}
	}
}
