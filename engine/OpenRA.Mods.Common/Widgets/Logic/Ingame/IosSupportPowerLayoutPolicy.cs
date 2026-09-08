#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Widgets.Logic.Ingame
{
	public enum IosSupportPowerLayoutMode
	{
		Desktop,
		SafeTopLeft,
		CompactBottomRight
	}

	public readonly struct IosSupportPowerObstacle : IEquatable<IosSupportPowerObstacle>
	{
		public readonly Rectangle Bounds;
		public readonly bool Visible;

		public IosSupportPowerObstacle(Rectangle bounds, bool visible)
		{
			Bounds = visible ? bounds : Rectangle.Empty;
			Visible = visible;
		}

		public bool Equals(IosSupportPowerObstacle other)
		{
			return Bounds == other.Bounds && Visible == other.Visible;
		}

		public override bool Equals(object obj)
		{
			return obj is IosSupportPowerObstacle other && Equals(other);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Bounds, Visible);
		}

		public static bool operator ==(IosSupportPowerObstacle left, IosSupportPowerObstacle right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(IosSupportPowerObstacle left, IosSupportPowerObstacle right)
		{
			return !left.Equals(right);
		}
	}

	public readonly struct IosSupportPowerObstacles : IEquatable<IosSupportPowerObstacles>
	{
		public readonly IosSupportPowerObstacle Production;
		public readonly IosSupportPowerObstacle CommandBar;
		public readonly IosSupportPowerObstacle Joystick;
		public readonly IosSupportPowerObstacle Actions;

		public IosSupportPowerObstacles(
			IosSupportPowerObstacle production,
			IosSupportPowerObstacle commandBar,
			IosSupportPowerObstacle joystick,
			IosSupportPowerObstacle actions)
		{
			Production = production;
			CommandBar = commandBar;
			Joystick = joystick;
			Actions = actions;
		}

		public IEnumerable<IosSupportPowerObstacle> Visible
		{
			get
			{
				if (Production.Visible)
					yield return Production;
				if (CommandBar.Visible)
					yield return CommandBar;
				if (Joystick.Visible)
					yield return Joystick;
				if (Actions.Visible)
					yield return Actions;
			}
		}

		public bool Equals(IosSupportPowerObstacles other)
		{
			return Production == other.Production && CommandBar == other.CommandBar &&
				Joystick == other.Joystick && Actions == other.Actions;
		}

		public override bool Equals(object obj)
		{
			return obj is IosSupportPowerObstacles other && Equals(other);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Production, CommandBar, Joystick, Actions);
		}

		public static bool operator ==(IosSupportPowerObstacles left, IosSupportPowerObstacles right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(IosSupportPowerObstacles left, IosSupportPowerObstacles right)
		{
			return !left.Equals(right);
		}
	}

	public sealed class IosSupportPowerLayout
	{
		public IosSupportPowerLayoutMode Mode { get; }
		public bool FitsAll { get; }
		public bool HasOverride => Mode != IosSupportPowerLayoutMode.Desktop && FitsAll;
		public IReadOnlyList<Rectangle> Cells { get; }
		public Rectangle PlacementBounds { get; }
		public Size CellSize { get; }
		public int Gap { get; }
		public int Margin { get; }

		public IosSupportPowerLayout(
			IosSupportPowerLayoutMode mode,
			bool fitsAll,
			IReadOnlyList<Rectangle> cells,
			Rectangle placementBounds,
			Size cellSize,
			int gap,
			int margin)
		{
			Mode = mode;
			FitsAll = fitsAll;
			Cells = cells ?? Array.Empty<Rectangle>();
			PlacementBounds = placementBounds;
			CellSize = cellSize;
			Gap = gap;
			Margin = margin;
		}
	}

	public static class IosSupportPowerLayoutPolicy
	{
		const int CompactRenderedFootprint = 64;

		readonly struct Interval
		{
			public readonly int Left;
			public readonly int Right;
			public int Width => Right - Left;

			public Interval(int left, int right)
			{
				Left = left;
				Right = right;
			}
		}

		public static IosSupportPowerLayout Create(
			bool enabled,
			IosScreenSnapshot snapshot,
			int iconCount,
			IosSupportPowerObstacles obstacles)
		{
			if (iconCount < 0)
				throw new ArgumentOutOfRangeException(nameof(iconCount));

			if (!enabled)
				return new IosSupportPowerLayout(
					IosSupportPowerLayoutMode.Desktop, true, Array.Empty<Rectangle>(),
					Rectangle.Empty, default, 0, 0);

			var compact = snapshot.IsCompactPhone;
			var mode = compact
				? IosSupportPowerLayoutMode.CompactBottomRight
				: IosSupportPowerLayoutMode.SafeTopLeft;
			var margin = snapshot.LogicalPoints(8);
			var gap = snapshot.LogicalPoints(6);
			var minimumTouch = snapshot.LogicalPoints(48);
			var compactCell = Math.Max(minimumTouch, CompactRenderedFootprint);
			var cellSize = compact
				? new Size(compactCell, compactCell)
				: new Size(Math.Max(60, minimumTouch), Math.Max(48, minimumTouch));
			var placementBounds = Inset(snapshot.SafeBounds, margin);

			if (iconCount == 0)
				return new IosSupportPowerLayout(
					mode, true, Array.Empty<Rectangle>(), placementBounds, cellSize, gap, margin);

			var expandedObstacles = obstacles.Visible
				.Select(o => Expand(o.Bounds, margin))
				.ToArray();
			IReadOnlyList<Rectangle> cells;
			if (compact)
			{
				cells = obstacles.Production.Visible
					? PlaceCompactBesideProduction(
						iconCount, placementBounds, cellSize, gap,
						Expand(obstacles.Production.Bounds, margin), expandedObstacles)
					: PlaceCompact(iconCount, placementBounds, cellSize, gap, expandedObstacles);
			}
			else
				cells = PlaceTablet(iconCount, placementBounds, cellSize, gap, expandedObstacles);

			if (cells.Count != iconCount)
				return new IosSupportPowerLayout(
					mode, false, Array.Empty<Rectangle>(), placementBounds, cellSize, gap, margin);

			return new IosSupportPowerLayout(mode, true, cells, placementBounds, cellSize, gap, margin);
		}

		static IReadOnlyList<Rectangle> PlaceCompact(
			int count,
			Rectangle placement,
			Size cellSize,
			int gap,
			IReadOnlyList<Rectangle> obstacles)
		{
			var cells = new List<Rectangle>(count);
			for (var y = placement.Bottom - cellSize.Height;
				y >= placement.Top && cells.Count < count;
				y -= cellSize.Height + gap)
			{
				var row = new Rectangle(placement.Left, y, placement.Width, cellSize.Height);
				var interval = RightmostFreeInterval(row, cellSize.Width, obstacles);
				if (interval.Width < cellSize.Width)
					continue;

				for (var x = interval.Right - cellSize.Width;
					x >= interval.Left && cells.Count < count;
					x -= cellSize.Width + gap)
					cells.Add(new Rectangle(x, y, cellSize.Width, cellSize.Height));
			}

			return cells;
		}

		static IReadOnlyList<Rectangle> PlaceCompactBesideProduction(
			int count,
			Rectangle placement,
			Size cellSize,
			int gap,
			Rectangle expandedProduction,
			IReadOnlyList<Rectangle> expandedObstacles)
		{
			var cells = new List<Rectangle>(count);
			var rightBoundary = Math.Min(placement.Right, expandedProduction.Left);
			for (var x = rightBoundary - cellSize.Width;
				x >= placement.Left && cells.Count < count;
				x -= cellSize.Width + gap)
			{
				for (var y = placement.Bottom - cellSize.Height;
					y >= placement.Top && cells.Count < count;
					y -= cellSize.Height + gap)
				{
					var candidate = new Rectangle(x, y, cellSize.Width, cellSize.Height);
					if (placement.Contains(candidate) && !IntersectsAny(candidate, expandedObstacles))
						cells.Add(candidate);
				}
			}

			return cells;
		}

		static IReadOnlyList<Rectangle> PlaceTablet(
			int count,
			Rectangle placement,
			Size cellSize,
			int gap,
			IReadOnlyList<Rectangle> obstacles)
		{
			var cells = new List<Rectangle>(count);
			for (var x = placement.Left;
				x + cellSize.Width <= placement.Right && cells.Count < count;
				x += cellSize.Width + gap)
			{
				for (var y = placement.Top;
					y + cellSize.Height <= placement.Bottom && cells.Count < count;
					y += cellSize.Height + gap)
				{
					var candidate = new Rectangle(x, y, cellSize.Width, cellSize.Height);
					if (IntersectsAny(candidate, obstacles))
						break;

					cells.Add(candidate);
				}
			}

			return cells;
		}

		static Interval RightmostFreeInterval(
			Rectangle row,
			int minimumWidth,
			IReadOnlyList<Rectangle> obstacles)
		{
			var intervals = new List<Interval> { new(row.Left, row.Right) };
			foreach (var obstacle in obstacles)
			{
				if (obstacle.Bottom <= row.Top || obstacle.Top >= row.Bottom)
					continue;

				var next = new List<Interval>(intervals.Count + 1);
				foreach (var interval in intervals)
				{
					if (obstacle.Right <= interval.Left || obstacle.Left >= interval.Right)
					{
						next.Add(interval);
						continue;
					}

					if (obstacle.Left > interval.Left)
						next.Add(new Interval(interval.Left, Math.Min(obstacle.Left, interval.Right)));
					if (obstacle.Right < interval.Right)
						next.Add(new Interval(Math.Max(obstacle.Right, interval.Left), interval.Right));
				}

				intervals = next;
			}

			var found = false;
			var result = default(Interval);
			foreach (var interval in intervals)
			{
				if (interval.Width < minimumWidth || (found && interval.Right <= result.Right))
					continue;

				found = true;
				result = interval;
			}

			return found ? result : default;
		}

		static bool IntersectsAny(Rectangle candidate, IReadOnlyList<Rectangle> obstacles)
		{
			for (var i = 0; i < obstacles.Count; i++)
				if (candidate.IntersectsWith(obstacles[i]))
					return true;

			return false;
		}

		static Rectangle Inset(Rectangle bounds, int amount)
		{
			var xInset = Math.Min(amount, bounds.Width / 2);
			var yInset = Math.Min(amount, bounds.Height / 2);
			return new Rectangle(
				bounds.X + xInset,
				bounds.Y + yInset,
				Math.Max(0, bounds.Width - 2 * xInset),
				Math.Max(0, bounds.Height - 2 * yInset));
		}

		static Rectangle Expand(Rectangle bounds, int amount)
		{
			return Rectangle.FromLTRB(
				bounds.Left - amount,
				bounds.Top - amount,
				bounds.Right + amount,
				bounds.Bottom + amount);
		}
	}

	public readonly struct IosSupportPowerLayoutSignature : IEquatable<IosSupportPowerLayoutSignature>
	{
		public readonly Size Resolution;
		public readonly IosScreenSnapshot Snapshot;
		public readonly Size EffectiveSize;
		public readonly Size NativePointSize;
		public readonly Rectangle SafeBounds;
		public readonly int IconCount;
		public readonly IosSupportPowerObstacles Obstacles;

		public IosSupportPowerLayoutSignature(
			Size resolution,
			IosScreenSnapshot snapshot,
			int iconCount,
			IosSupportPowerObstacles obstacles)
		{
			Resolution = resolution;
			Snapshot = snapshot;
			EffectiveSize = snapshot.EffectiveSize;
			NativePointSize = snapshot.NativePointSize;
			SafeBounds = snapshot.SafeBounds;
			IconCount = iconCount;
			Obstacles = obstacles;
		}

		public bool Equals(IosSupportPowerLayoutSignature other)
		{
			return Resolution == other.Resolution && EffectiveSize == other.EffectiveSize &&
				NativePointSize == other.NativePointSize && SafeBounds == other.SafeBounds &&
				IconCount == other.IconCount && Obstacles == other.Obstacles;
		}

		public override bool Equals(object obj)
		{
			return obj is IosSupportPowerLayoutSignature other && Equals(other);
		}

		public override int GetHashCode()
		{
			var hash = default(HashCode);
			hash.Add(Resolution);
			hash.Add(EffectiveSize);
			hash.Add(NativePointSize);
			hash.Add(SafeBounds);
			hash.Add(IconCount);
			hash.Add(Obstacles);
			return hash.ToHashCode();
		}

		public static bool operator ==(
			IosSupportPowerLayoutSignature left,
			IosSupportPowerLayoutSignature right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(
			IosSupportPowerLayoutSignature left,
			IosSupportPowerLayoutSignature right)
		{
			return !left.Equals(right);
		}
	}

	public sealed class IosSupportPowerLayoutTracker
	{
		bool initialized;
		IosSupportPowerLayoutSignature last;

		public bool Update(IosSupportPowerLayoutSignature current)
		{
			if (initialized && last.Equals(current))
				return false;

			last = current;
			initialized = true;
			return true;
		}

		public bool RunIfChanged(
			IosSupportPowerLayoutSignature current,
			Action<IosSupportPowerLayoutSignature> onChanged)
		{
			if (onChanged == null)
				throw new ArgumentNullException(nameof(onChanged));
			if (!Update(current))
				return false;

			onChanged(current);
			return true;
		}
	}

	public static class IosSupportPowerCellPolicy
	{
		public static bool TryGetCells(
			int iconCount,
			Func<int, IReadOnlyList<Rectangle>> provider,
			out IReadOnlyList<Rectangle> cells)
		{
			cells = provider?.Invoke(iconCount);
			if (cells != null && cells.Count == iconCount)
				return true;

			cells = null;
			return false;
		}

		public static bool Contains(IReadOnlyList<Rectangle> cells, int2 location)
		{
			if (cells == null)
				return false;

			for (var i = 0; i < cells.Count; i++)
				if (cells[i].Contains(location))
					return true;

			return false;
		}

		public static bool ShouldHandleMouseDown(IReadOnlyList<Rectangle> cells, int2 location)
		{
			return Contains(cells, location);
		}

		public static Rectangle CenterArt(Rectangle cell, Size artSize)
		{
			return new Rectangle(
				cell.X + (cell.Width - artSize.Width) / 2,
				cell.Y + (cell.Height - artSize.Height) / 2,
				artSize.Width,
				artSize.Height);
		}

		public static Rectangle PlaceTemplate(
			Rectangle cell,
			Size artSize,
			int2 layerChildOrigin,
			Rectangle templateBounds)
		{
			var art = CenterArt(cell, artSize);
			return new Rectangle(
				art.X - layerChildOrigin.X + templateBounds.X,
				art.Y - layerChildOrigin.Y + templateBounds.Y,
				templateBounds.Width,
				templateBounds.Height);
		}
	}

	public static class IosSupportPowerTooltipPolicy
	{
		const string Ellipsis = "…";

		public static int2 ClampOrigin(
			int2 cursor,
			Rectangle tooltipBounds,
			IosScreenSnapshot snapshot,
			int2 cursorOffset,
			int bottomEdgeYOffset,
			int scale)
		{
			scale = Math.Max(1, scale);
			var safe = snapshot.SafeBounds;
			var pos = cursor + scale * cursorOffset;
			if (pos.Y + tooltipBounds.Bottom > safe.Bottom)
				pos = pos.WithY(cursor.Y + scale * bottomEdgeYOffset - tooltipBounds.Height);

			var minX = safe.Left - tooltipBounds.Left;
			var maxX = Math.Max(minX, safe.Right - tooltipBounds.Right);
			var minY = safe.Top - tooltipBounds.Top;
			var maxY = Math.Max(minY, safe.Bottom - tooltipBounds.Bottom);
			return new int2(
				Math.Clamp(pos.X, minX, maxX),
				Math.Clamp(pos.Y, minY, maxY));
		}

		public static Size MaximumSize(IosScreenSnapshot snapshot)
		{
			var inset = snapshot.LogicalPoints(16);
			return new Size(
				Math.Max(1, snapshot.SafeBounds.Width - inset),
				Math.Max(1, snapshot.SafeBounds.Height - inset));
		}

		public static string FitText(
			string text,
			int maximumWidth,
			int maximumHeight,
			Func<string, Size> measure)
		{
			if (measure == null)
				throw new ArgumentNullException(nameof(measure));
			if (string.IsNullOrEmpty(text) || maximumWidth <= 0 || maximumHeight <= 0)
				return string.Empty;
			var ellipsisSize = measure(Ellipsis);
			if (ellipsisSize.Width > maximumWidth || ellipsisSize.Height > maximumHeight)
				return string.Empty;

			var wrapped = string.Join("\n", WrapTextElements(text, maximumWidth, measure)
				.Split('\n')
				.Select(line => FitSingleLine(line, maximumWidth, measure)));
			var wrappedSize = measure(wrapped);
			if (wrappedSize.Width <= maximumWidth && wrappedSize.Height <= maximumHeight)
				return wrapped;

			var lines = wrapped.Split('\n').ToList();
			while (lines.Count > 0)
			{
				lines[^1] = Ellipsize(lines[^1], maximumWidth, measure);
				var candidate = string.Join("\n", lines);
				var candidateSize = measure(candidate);
				if (candidateSize.Width <= maximumWidth && candidateSize.Height <= maximumHeight &&
					candidate.Length > 0)
					return candidate;

				lines.RemoveAt(lines.Count - 1);
			}

			return Ellipsis;
		}

		public static string FitSingleLine(
			string text,
			int maximumWidth,
			Func<string, Size> measure)
		{
			if (measure == null)
				throw new ArgumentNullException(nameof(measure));
			if (string.IsNullOrEmpty(text) || maximumWidth <= 0)
				return string.Empty;

			var singleLine = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
			if (measure(singleLine).Width <= maximumWidth)
				return singleLine;

			return Ellipsize(singleLine, maximumWidth, measure);
		}

		static string WrapTextElements(string text, int maximumWidth, Func<string, Size> measure)
		{
			var output = new List<string>();
			foreach (var paragraph in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
			{
				var paragraphStart = output.Count;
				var line = new StringBuilder();
				var enumerator = StringInfo.GetTextElementEnumerator(paragraph);
				while (enumerator.MoveNext())
				{
					var element = enumerator.GetTextElement();
					var candidate = line.ToString() + element;
					if (line.Length > 0 && measure(candidate).Width <= maximumWidth)
					{
						line.Append(element);
						continue;
					}

					if (line.Length == 0)
					{
						if (measure(element).Width <= maximumWidth)
							line.Append(element);
						else if (!string.IsNullOrWhiteSpace(element))
							output.Add(Ellipsis);

						continue;
					}

					output.Add(line.ToString().TrimEnd());
					line.Clear();
					if (!string.IsNullOrWhiteSpace(element) && measure(element).Width <= maximumWidth)
						line.Append(element);
					else if (!string.IsNullOrWhiteSpace(element))
						output.Add(Ellipsis);
				}

				if (line.Length > 0 || output.Count == paragraphStart)
					output.Add(line.ToString().TrimEnd());
			}

			return string.Join("\n", output);
		}

		static string Ellipsize(string text, int maximumWidth, Func<string, Size> measure)
		{
			if (measure(Ellipsis).Width > maximumWidth)
				return string.Empty;

			var elements = new List<string>();
			var enumerator = StringInfo.GetTextElementEnumerator(text.TrimEnd());
			while (enumerator.MoveNext())
				elements.Add(enumerator.GetTextElement());

			while (elements.Count > 0 && measure(string.Concat(elements) + Ellipsis).Width > maximumWidth)
				elements.RemoveAt(elements.Count - 1);

			return string.Concat(elements) + Ellipsis;
		}
	}
}
