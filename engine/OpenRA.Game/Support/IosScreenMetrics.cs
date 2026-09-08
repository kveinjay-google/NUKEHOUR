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
using OpenRA.Primitives;

namespace OpenRA
{
	public readonly struct IosSafeAreaInsets
	{
		public readonly double Left;
		public readonly double Top;
		public readonly double Right;
		public readonly double Bottom;

		public IosSafeAreaInsets(double left, double top, double right, double bottom)
		{
			Left = Math.Max(0, left);
			Top = Math.Max(0, top);
			Right = Math.Max(0, right);
			Bottom = Math.Max(0, bottom);
		}
	}

	public readonly struct IosScreenSnapshot
	{
		public readonly Size EffectiveSize;
		public readonly Size NativePointSize;
		public readonly IosSafeAreaInsets SafeAreaInsets;

		public IosScreenSnapshot(Size effectiveSize, Size nativePointSize, IosSafeAreaInsets safeAreaInsets)
		{
			EffectiveSize = new Size(Math.Max(1, effectiveSize.Width), Math.Max(1, effectiveSize.Height));
			NativePointSize = new Size(Math.Max(1, nativePointSize.Width), Math.Max(1, nativePointSize.Height));
			SafeAreaInsets = safeAreaInsets;
		}

		public double LogicalPerPoint => Math.Max(
			EffectiveSize.Width / (double)NativePointSize.Width,
			EffectiveSize.Height / (double)NativePointSize.Height);

		public int LogicalPoints(double points) =>
			(int)Math.Ceiling(Math.Max(0, points) * LogicalPerPoint);

		public bool IsCompactPhone =>
			Math.Min(NativePointSize.Width, NativePointSize.Height) < 600;

		public Rectangle SafeBounds
		{
			get
			{
				var left = LogicalInset(SafeAreaInsets.Left, EffectiveSize.Width);
				var top = LogicalInset(SafeAreaInsets.Top, EffectiveSize.Height);
				var right = LogicalInset(SafeAreaInsets.Right, EffectiveSize.Width - left);
				var bottom = LogicalInset(SafeAreaInsets.Bottom, EffectiveSize.Height - top);
				return new Rectangle(left, top,
					Math.Max(1, EffectiveSize.Width - left - right),
					Math.Max(1, EffectiveSize.Height - top - bottom));
			}
		}

		int LogicalInset(double points, int maximum) =>
			Math.Min(maximum, (int)Math.Ceiling(points * LogicalPerPoint));
	}

	public static class IosScreenMetrics
	{
		static readonly object Sync = new();
		static Size nativePointSize;
		static IosSafeAreaInsets safeAreaInsets;

		public static void Publish(Size nativeSize, IosSafeAreaInsets safeArea)
		{
			lock (Sync)
			{
				nativePointSize = nativeSize;
				safeAreaInsets = safeArea;
			}
		}

		public static IosScreenSnapshot SnapshotFor(Size effectiveSize)
		{
			lock (Sync)
			{
				var nativeSize = nativePointSize.IsEmpty ? effectiveSize : nativePointSize;
				return new IosScreenSnapshot(effectiveSize, nativeSize, safeAreaInsets);
			}
		}
	}
}
