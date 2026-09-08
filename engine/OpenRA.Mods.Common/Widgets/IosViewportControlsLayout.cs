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
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public sealed class IosViewportActionLabelLayout
	{
		public Rectangle Bounds { get; }
		public string Font { get; }

		IosViewportActionLabelLayout(Rectangle bounds, string font)
		{
			Bounds = bounds;
			Font = font;
		}

		public static IosViewportActionLabelLayout Create(
			IosScreenSnapshot snapshot, int buttonDiameter)
		{
			var horizontalInset = snapshot.LogicalPoints(6);
			var height = snapshot.LogicalPoints(14);
			var bottomInset = snapshot.LogicalPoints(6);
			return new IosViewportActionLabelLayout(
				new Rectangle(
					horizontalInset,
					buttonDiameter - bottomInset - height,
					buttonDiameter - 2 * horizontalInset,
					height),
				snapshot.IsCompactPhone ? "IosTouchLabel" : "TinyBold");
		}

		public void ApplyTo(LabelWidget label)
		{
			label.Bounds = new WidgetBounds(
				Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
			label.Font = Font;
		}
	}

	public sealed class IosViewportControlsLayout
	{
		// Keep the approved clockwise action order on one circular ring.
		static readonly double[] ActionAngles = { -90, -45, 0 };

		public const int DefaultJoystickPoints = 128;

		public int NormalizedJoystickPoints { get; }
		public Rectangle JoystickBounds { get; }
		public IReadOnlyList<Rectangle> ActionBounds { get; }
		public Rectangle ActionsBounds { get; }
		public int ThumbDiameter { get; }
		public int MovementRadius { get; }
		public int RingRadius { get; }
		public int ButtonDiameter { get; }
		public int Margin { get; }

		IosViewportControlsLayout(
			int normalizedJoystickPoints,
			Rectangle joystickBounds,
			Rectangle[] actionBounds,
			Rectangle actionsBounds,
			int thumbDiameter,
			int movementRadius,
			int ringRadius,
			int buttonDiameter,
			int margin)
		{
			NormalizedJoystickPoints = normalizedJoystickPoints;
			JoystickBounds = joystickBounds;
			ActionBounds = Array.AsReadOnly((Rectangle[])actionBounds.Clone());
			ActionsBounds = actionsBounds;
			ThumbDiameter = thumbDiameter;
			MovementRadius = movementRadius;
			RingRadius = ringRadius;
			ButtonDiameter = buttonDiameter;
			Margin = margin;
		}

		public static int NormalizeJoystickPoints(int requested) =>
			requested is 112 or 128 or 144 ? requested : DefaultJoystickPoints;

		public static bool ShouldRefreshForJoystickSize(
			bool isIos, bool layoutInitialized, int previousNormalized, int storedRequested) =>
			isIos && (!layoutInitialized || previousNormalized != NormalizeJoystickPoints(storedRequested));

		public static IosViewportControlsLayout Create(
			IosScreenSnapshot snapshot, int requestedJoystickPoints, Rectangle bottomObstacle)
		{
			var normalized = NormalizeJoystickPoints(requestedJoystickPoints);
			var joystickDiameter = snapshot.LogicalPoints(normalized);
			var thumbDiameter = snapshot.LogicalPoints(normalized * 0.42);
			var movementRadius = snapshot.LogicalPoints(normalized * 0.27);
			var buttonDiameter = snapshot.LogicalPoints(56);
			var margin = snapshot.LogicalPoints(12);
			var obstacleGap = snapshot.LogicalPoints(8);

			var ringRadius = snapshot.LogicalPoints(normalized / 2.0 + 48);
			var offsets = new int2[ActionAngles.Length];
			for (var i = 0; i < ActionAngles.Length; i++)
				offsets[i] = RingOffset(
					ringRadius, ActionAngles[i], joystickDiameter, buttonDiameter, obstacleGap);

			var joystickBeforeCenter = joystickDiameter / 2;
			var joystickAfterCenter = joystickDiameter - joystickBeforeCenter;
			var buttonBeforeCenter = buttonDiameter / 2;

			var safe = snapshot.SafeBounds;
			var centerX = safe.Left + margin + joystickBeforeCenter;
			var centerY = safe.Bottom - margin - joystickAfterCenter;

			var joystickBounds = new Rectangle(
				centerX - joystickBeforeCenter,
				centerY - joystickBeforeCenter,
				joystickDiameter,
				joystickDiameter);
			var actions = new Rectangle[offsets.Length];
			for (var i = 0; i < offsets.Length; i++)
				actions[i] = new Rectangle(
					centerX + offsets[i].X - buttonBeforeCenter,
					centerY + offsets[i].Y - buttonBeforeCenter,
					buttonDiameter,
					buttonDiameter);

			var expandedObstacle = Expand(bottomObstacle, obstacleGap);
			var shift = RequiredUpwardShift(joystickBounds, expandedObstacle);
			foreach (var action in actions)
				shift = Math.Max(shift, RequiredUpwardShift(action, expandedObstacle));

			var insetSafe = Inset(safe, margin);
			var maximumShift = joystickBounds.Top - insetSafe.Top;
			foreach (var action in actions)
				maximumShift = Math.Min(maximumShift, action.Top - insetSafe.Top);

			if (shift > maximumShift)
				throw new InvalidOperationException(
					"The iOS viewport controls cannot clear the bottom obstacle within the inset Safe Area.");

			if (shift > 0)
			{
				joystickBounds = TranslateUp(joystickBounds, shift);
				for (var i = 0; i < actions.Length; i++)
					actions[i] = TranslateUp(actions[i], shift);
			}

			if (!insetSafe.Contains(joystickBounds) || joystickBounds.IntersectsWith(expandedObstacle))
				throw new InvalidOperationException(
					"The iOS viewport joystick does not fit within the inset Safe Area.");

			foreach (var action in actions)
			{
				if (!insetSafe.Contains(action) || action.IntersectsWith(expandedObstacle))
					throw new InvalidOperationException(
						"An iOS viewport action does not fit within the inset Safe Area.");
			}

			var actionsBounds = actions[0];
			for (var i = 1; i < actions.Length; i++)
				actionsBounds = Rectangle.Union(actionsBounds, actions[i]);

			return new IosViewportControlsLayout(
				normalized, joystickBounds, actions, actionsBounds,
				thumbDiameter, movementRadius, ringRadius, buttonDiameter, margin);
		}

		static Rectangle Expand(Rectangle bounds, int amount) => bounds.IsEmpty
			? Rectangle.Empty
			: Rectangle.FromLTRB(
				bounds.Left - amount, bounds.Top - amount,
				bounds.Right + amount, bounds.Bottom + amount);

		static Rectangle Inset(Rectangle bounds, int amount) => Rectangle.FromLTRB(
			bounds.Left + amount, bounds.Top + amount,
			bounds.Right - amount, bounds.Bottom - amount);

		static int RequiredUpwardShift(Rectangle entity, Rectangle obstacle) =>
			!obstacle.IsEmpty && entity.IntersectsWith(obstacle) ? entity.Bottom - obstacle.Top : 0;

		static Rectangle TranslateUp(Rectangle bounds, int amount) =>
			new(bounds.X, bounds.Y - amount, bounds.Width, bounds.Height);

		static int2 RingOffset(
			int radius, double angleDegrees, int joystickDiameter, int buttonDiameter, int minimumGap)
		{
			var radians = angleDegrees * Math.PI / 180;
			var idealX = radius * Math.Cos(radians);
			var idealY = radius * Math.Sin(radians);
			var roundedX = (int)Math.Round(idealX, MidpointRounding.AwayFromZero);
			var roundedY = (int)Math.Round(idealY, MidpointRounding.AwayFromZero);
			var best = int2.Zero;
			var bestRadiusError = double.MaxValue;
			var bestAngleError = double.MaxValue;
			var found = false;

			// Integer rounding can pull a diagonal square slightly inside the approved 8pt gap.
			// Search the immediate pixel neighborhood for the closest point that preserves both
			// the nominal ring radius tolerance and the real circle-to-square interaction gap.
			for (var x = roundedX - 2; x <= roundedX + 2; x++)
			{
				for (var y = roundedY - 2; y <= roundedY + 2; y++)
				{
					var candidate = new int2(x, y);
					var radiusError = Math.Abs(Length(candidate) - radius);
					var angleError = AngleError(candidate, angleDegrees);
					if (radiusError > 1.000001 || angleError > 1.000001 ||
						CircleToSquareGap(candidate, joystickDiameter, buttonDiameter) < minimumGap)
						continue;

					if (!found || radiusError < bestRadiusError - 0.000001 ||
						(Math.Abs(radiusError - bestRadiusError) <= 0.000001 && angleError < bestAngleError))
					{
						best = candidate;
						bestRadiusError = radiusError;
						bestAngleError = angleError;
						found = true;
					}
				}
			}

			if (!found)
				throw new InvalidOperationException(
					"The iOS viewport action ring cannot preserve the minimum interaction gap.");

			return best;
		}

		static double CircleToSquareGap(int2 squareCenter, int circleDiameter, int squareDiameter)
		{
			var squareBeforeCenter = squareDiameter / 2;
			var squareAfterCenter = squareDiameter - squareBeforeCenter;
			var left = squareCenter.X - squareBeforeCenter;
			var right = squareCenter.X + squareAfterCenter;
			var top = squareCenter.Y - squareBeforeCenter;
			var bottom = squareCenter.Y + squareAfterCenter;
			var nearestX = left > 0 ? left : right < 0 ? right : 0;
			var nearestY = top > 0 ? top : bottom < 0 ? bottom : 0;
			return Math.Sqrt((double)nearestX * nearestX + (double)nearestY * nearestY) -
				circleDiameter / 2.0;
		}

		static double Length(int2 value) =>
			Math.Sqrt((double)value.X * value.X + (double)value.Y * value.Y);

		static double AngleError(int2 value, double expectedDegrees)
		{
			var actual = Math.Atan2(value.Y, value.X) * 180 / Math.PI;
			var difference = Math.Abs(actual - expectedDegrees) % 360;
			return Math.Min(difference, 360 - difference);
		}
	}
}
