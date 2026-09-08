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
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class IosSettingsLayout
	{
		const int ReferenceWidth = 1180;
		const int ReferenceHeight = 720;
		const int PhoneHeightBreakpoint = 600;
		const int TabCount = 5;

		public bool Enabled { get; }
		public bool IsPhone { get; }
		public bool TabsAreHorizontal => Enabled && IsPhone;
		public double LogicalPerPoint { get; }
		public double ViewportScale => LogicalPerPoint;
		public Rectangle SafeBounds { get; }
		public Rectangle Window { get; }
		public Rectangle Header { get; }
		public Rectangle Tabs { get; }
		public Rectangle Content { get; }
		public Rectangle Footer { get; }
		public Rectangle Reset { get; }
		public Rectangle Back { get; }
		public Rectangle HotkeyHeader { get; }
		public Rectangle HotkeyList { get; }
		public Rectangle HotkeyFooter { get; }
		public int MinimumTarget { get; }
		public int RowHeight => MinimumTarget;
		public int SectionHeaderHeight { get; }
		public int HotkeyActionHeight => MinimumTarget;
		public int HotkeyColumns => IsPhone ? 1 : 2;
		public int ScrollbarWidth => MinimumTarget;
		public bool HotkeyScrollbarVisible => !IsPhone;
		public int HotkeyScrollbarWidth => HotkeyScrollbarVisible ? ScrollbarWidth : 0;
		public int ContentInset { get; }
		public int SidebarWidth => IsPhone ? 0 : Tabs.Width;
		public int TabHeight { get; }
		public int TabGap { get; }
		public int ActionHeight => Reset.Height;
		public int WindowX => Window.X;
		public int WindowY => Window.Y;
		public int WindowWidth => Window.Width;
		public int WindowHeight => Window.Height;

		IosSettingsLayout(bool enabled, IosScreenSnapshot snapshot)
		{
			Enabled = enabled;
			LogicalPerPoint = enabled ? snapshot.LogicalPerPoint : 1;
			SafeBounds = enabled ? snapshot.SafeBounds : new Rectangle(0, 0, snapshot.EffectiveSize.Width, snapshot.EffectiveSize.Height);
			IsPhone = enabled && Math.Min(snapshot.NativePointSize.Width, snapshot.NativePointSize.Height) < PhoneHeightBreakpoint;
			MinimumTarget = Point(48);
			SectionHeaderHeight = Point(32);
			ContentInset = Point(IsPhone ? 4 : 12);
			TabGap = Point(IsPhone ? 4 : 8);
			TabHeight = Point(IsPhone ? 48 : 52);

			if (!enabled)
			{
				Window = SafeBounds;
				return;
			}

			var windowMargin = Point(6);
			Window = Inset(SafeBounds, windowMargin);
			var inner = Inset(Window, ContentInset);
			var regionGap = Point(IsPhone ? 4 : 8);
			var headerHeight = Point(IsPhone ? 32 : 52);
			var footerHeight = Point(IsPhone ? 48 : 56);
			Header = new Rectangle(inner.X, inner.Y, inner.Width, headerHeight);
			Footer = new Rectangle(inner.X, inner.Bottom - footerHeight, inner.Width, footerHeight);

			if (IsPhone)
			{
				Tabs = new Rectangle(inner.X, Header.Bottom + regionGap, inner.Width, TabHeight);
				var contentTop = Tabs.Bottom + regionGap;
				Content = Rectangle.FromLTRB(inner.Left, contentTop, inner.Right, Footer.Top - regionGap);
				var actionWidth = (Footer.Width - TabGap) / 2;
				Reset = new Rectangle(Footer.X, Footer.Y, actionWidth, Footer.Height);
				Back = Rectangle.FromLTRB(Reset.Right + TabGap, Footer.Top, Footer.Right, Footer.Bottom);
			}
			else
			{
				var navigationTop = Header.Bottom + regionGap;
				var sidebarWidth = Math.Min(Point(260), Math.Max(MinimumTarget, inner.Width / 3));
				var tabsHeight = TabCount * TabHeight + (TabCount - 1) * TabGap;
				Tabs = new Rectangle(inner.X, navigationTop, sidebarWidth, tabsHeight);
				Content = Rectangle.FromLTRB(Tabs.Right + regionGap, navigationTop, inner.Right, Footer.Top - regionGap);
				Reset = new Rectangle(Footer.X, Footer.Y, sidebarWidth, Footer.Height);
				var backWidth = Math.Min(Point(220), Math.Max(MinimumTarget, Footer.Width / 3));
				Back = new Rectangle(Footer.Right - backWidth, Footer.Y, backWidth, Footer.Height);
			}

			var hotkeyGap = Point(IsPhone ? 4 : 8);
			var hotkeyFooterHeight = Point(72);
			HotkeyHeader = new Rectangle(Content.X, Content.Y, Content.Width, MinimumTarget);
			HotkeyFooter = new Rectangle(Content.X, Content.Bottom - hotkeyFooterHeight, Content.Width, hotkeyFooterHeight);
			HotkeyList = Rectangle.FromLTRB(
				Content.Left,
				HotkeyHeader.Bottom + hotkeyGap,
				Content.Right,
				HotkeyFooter.Top - hotkeyGap);
		}

		public static IosSettingsLayout ForPlatform(bool isIos)
		{
			var size = new Size(ReferenceWidth, ReferenceHeight);
			return new IosSettingsLayout(isIos, new IosScreenSnapshot(size, size, default));
		}

		public static IosSettingsLayout ForViewport(bool isIos, int viewportWidth, int viewportHeight)
		{
			var size = new Size(viewportWidth, viewportHeight);
			return new IosSettingsLayout(isIos, new IosScreenSnapshot(size, size, default));
		}

		public static IosSettingsLayout ForScreen(
			bool isIos, Size effectiveSize, Size nativePointSize, IosSafeAreaInsets safeAreaInsets)
		{
			return new IosSettingsLayout(isIos, new IosScreenSnapshot(effectiveSize, nativePointSize, safeAreaInsets));
		}

		public static IosSettingsLayout ForSnapshot(bool isIos, IosScreenSnapshot snapshot)
		{
			return new IosSettingsLayout(isIos, snapshot);
		}

		public int Scale(int value)
		{
			return Enabled ? Point(value) : value;
		}

		public int ScaleY(int value)
		{
			return Enabled ? Point(value) : value;
		}

		public Rectangle TabBounds(int index)
		{
			if (index < 0 || index >= TabCount)
				throw new ArgumentOutOfRangeException(nameof(index));

			if (!IsPhone)
				return new Rectangle(Tabs.X, Tabs.Y + index * (TabHeight + TabGap), Tabs.Width, TabHeight);

			var availableWidth = Tabs.Width - (TabCount - 1) * TabGap;
			var baseWidth = availableWidth / TabCount;
			var remainder = availableWidth % TabCount;
			var x = Tabs.X + index * (baseWidth + TabGap) + Math.Min(index, remainder);
			var width = baseWidth + (index < remainder ? 1 : 0);
			return new Rectangle(x, Tabs.Y, width, Tabs.Height);
		}

		public double RowVerticalScale(int originalHeight, int minimumTouchHeight, bool isSectionHeader)
		{
			var sourceHeight = Math.Max(1, originalHeight);
			var targetHeight = isSectionHeader ? SectionHeaderHeight : RowHeight;
			var scale = targetHeight / (double)sourceHeight;
			if (minimumTouchHeight > 0)
				scale = Math.Max(scale, MinimumTarget / (double)minimumTouchHeight);

			return Math.Max(1, scale);
		}

		public double ScrollContentHorizontalScale(
			int originalScrollWidth, int originalScrollbarWidth, int newScrollWidth)
		{
			var originalContentWidth = Math.Max(1, originalScrollWidth - originalScrollbarWidth);
			var newContentWidth = Math.Max(1, newScrollWidth - ScrollbarWidth);
			return newContentWidth / (double)originalContentWidth;
		}

		public Rectangle TransformBoundsFromOriginal(
			Rectangle originalBounds, double horizontalScale, double verticalScale,
			bool scalePosition, bool isTouchTarget)
		{
			var width = ScaleCoordinate(originalBounds.Width, horizontalScale);
			var height = ScaleCoordinate(originalBounds.Height, verticalScale);
			if (isTouchTarget)
			{
				width = Math.Max(MinimumTarget, width);
				height = Math.Max(MinimumTarget, height);
			}

			return new Rectangle(
				ScaleCoordinate(originalBounds.X, horizontalScale),
				scalePosition ? ScaleCoordinate(originalBounds.Y, verticalScale) : originalBounds.Y,
				width, height);
		}

		public int HotkeyItemWidth(int contentWidth)
		{
			var gap = Scale(6);
			var availableWidth = Math.Max(MinimumTarget, contentWidth - HotkeyScrollbarWidth - gap);
			return Math.Max(MinimumTarget, (availableWidth - (HotkeyColumns - 1) * gap) / HotkeyColumns);
		}

		int Point(int value) => Math.Max(1, (int)Math.Ceiling(value * LogicalPerPoint));

		static int ScaleCoordinate(int value, double scale) =>
			(int)Math.Round(value * scale, MidpointRounding.AwayFromZero);

		static Rectangle Inset(Rectangle bounds, int inset)
		{
			var horizontal = Math.Min(inset, Math.Max(0, (bounds.Width - 1) / 2));
			var vertical = Math.Min(inset, Math.Max(0, (bounds.Height - 1) / 2));
			return new Rectangle(bounds.X + horizontal, bounds.Y + vertical,
				Math.Max(1, bounds.Width - 2 * horizontal), Math.Max(1, bounds.Height - 2 * vertical));
		}
	}
}
