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
using OpenRA.Widgets;

namespace OpenRA
{
	public enum TouchPointerActionType
	{
		None,
		LeftDown,
		LeftMove,
		LeftUp,
		LeftCancel,
		RightDown,
		RightUp,
		MiddleDown,
		MiddleMove,
		MiddleUp,
		Scroll
	}

	public static class TouchPointerButtonPolicy
	{
		public static MouseButton Resolve(TouchPointerActionType type,
			bool useClassicMouseStyle, bool useAlternateScrollButton)
		{
			if (type == TouchPointerActionType.Scroll || type == TouchPointerActionType.None)
				return MouseButton.None;

			if (type == TouchPointerActionType.RightDown || type == TouchPointerActionType.RightUp)
				return MouseButton.Right;

			if (type == TouchPointerActionType.MiddleDown || type == TouchPointerActionType.MiddleMove ||
				type == TouchPointerActionType.MiddleUp)
				return useClassicMouseStyle ^ useAlternateScrollButton ? MouseButton.Right : MouseButton.Middle;

			return MouseButton.Left;
		}
	}

	public struct TouchPointerButtonState
	{
		MouseButton viewportPanButton;

		public MouseButton Resolve(TouchPointerActionType type,
			bool useClassicMouseStyle, bool useAlternateScrollButton)
		{
			var isViewportPan = type == TouchPointerActionType.MiddleDown ||
				type == TouchPointerActionType.MiddleMove || type == TouchPointerActionType.MiddleUp;
			if (!isViewportPan)
				return TouchPointerButtonPolicy.Resolve(type, useClassicMouseStyle, useAlternateScrollButton);

			if (type == TouchPointerActionType.MiddleDown || viewportPanButton == MouseButton.None)
				viewportPanButton = TouchPointerButtonPolicy.Resolve(
					type, useClassicMouseStyle, useAlternateScrollButton);

			var button = viewportPanButton;
			if (type == TouchPointerActionType.MiddleUp)
				viewportPanButton = MouseButton.None;

			return button;
		}

		public void Reset()
		{
			viewportPanButton = MouseButton.None;
		}
	}

	public struct ViewportScrollButtonState
	{
		MouseButton activeButton;

		public MouseButton CurrentOr(MouseButton configuredButton)
		{
			return activeButton == MouseButton.None ? configuredButton : activeButton;
		}

		public void Begin(MouseButton button)
		{
			activeButton = button;
		}

		public void End()
		{
			activeButton = MouseButton.None;
		}
	}

	public static class TouchPressFeedback
	{
		static Widget target;

		public static bool Active { get; private set; }
		public static int2 Position { get; private set; }

		public static void Begin(int2 position, Widget hitTarget)
		{
			Position = position;
			target = hitTarget;
			Active = true;
		}

		public static void Move(int2 position)
		{
			if (Active)
				Position = position;
		}

		public static void End()
		{
			Active = false;
			target = null;
		}

		public static bool IsActiveFor(Widget widget)
		{
			if (!Active || widget == null || !widget.RenderBounds.Contains(Position))
				return false;

			for (var candidate = target; candidate != null; candidate = candidate.Parent)
				if (candidate == widget)
					return true;

			return false;
		}
	}

	public readonly struct TouchPointerAction
	{
		public readonly TouchPointerActionType Type;
		public readonly int2 Position;
		public readonly int2 Delta;

		public TouchPointerAction(TouchPointerActionType type, int2 position, int2 delta)
		{
			Type = type;
			Position = position;
			Delta = delta;
		}
	}

	public readonly struct TouchGestureResult
	{
		public static readonly TouchGestureResult Empty = new TouchGestureResult(default, default, default, 0);

		public readonly TouchPointerAction First;
		public readonly TouchPointerAction Second;
		public readonly TouchPointerAction Third;
		public readonly int Count;

		TouchGestureResult(TouchPointerAction first, TouchPointerAction second, TouchPointerAction third, int count)
		{
			First = first;
			Second = second;
			Third = third;
			Count = count;
		}

		public static TouchGestureResult One(TouchPointerAction action)
		{
			return new TouchGestureResult(action, default, default, 1);
		}

		public static TouchGestureResult Two(TouchPointerAction first, TouchPointerAction second)
		{
			return new TouchGestureResult(first, second, default, 2);
		}

		public static TouchGestureResult Three(TouchPointerAction first, TouchPointerAction second, TouchPointerAction third)
		{
			return new TouchGestureResult(first, second, third, 3);
		}
	}

	public sealed class TouchGestureAdapter
	{
		enum TwoFingerMode
		{
			None,
			Pending,
			Pan,
			Pinch
		}

		const int DragThreshold = 8;
		const int PinchThreshold = 12;
		const ulong LongPressDelay = 450;
		const ulong TwoFingerArbitrationDelay = 32;
		const double GestureDominanceRatio = 1.6;

		bool active;
		bool dragging;
		bool longPressReady;
		bool twoFinger;
		bool twoFingerTapEligible;
		bool twoFingerLongPressReady;
		TwoFingerMode twoFingerMode;
		bool firstFingerMoved;
		bool secondFingerMoved;
		bool arbitrationWindowStarted;
		bool pinchFirstChanged;
		bool pinchSecondChanged;
		bool pinchPairWindowStarted;
		bool threeFinger;
		int2 origin;
		int2 lastPosition;
		int2 secondPosition;
		int2 firstTwoFingerOrigin;
		int2 secondTwoFingerOrigin;
		int2 settledPinchFirstPosition;
		int2 settledPinchSecondPosition;
		int2 thirdPosition;
		int2 lastCentroid;
		double lastZoomDistance;
		ulong startTime;
		ulong twoFingerStartTime;
		ulong arbitrationWindowStart;
		ulong pinchPairWindowStart;

		public static int2 MapPosition(float x, float y, Size size)
		{
			return new int2(
				Math.Clamp((int)(x * size.Width), 0, size.Width - 1),
				Math.Clamp((int)(y * size.Height), 0, size.Height - 1));
		}

		public TouchGestureResult Begin(int2 position)
		{
			return Begin(position, 0);
		}

		public TouchGestureResult Begin(int2 position, ulong timestamp)
		{
			active = true;
			twoFinger = false;
			twoFingerTapEligible = false;
			twoFingerLongPressReady = false;
			ResetTwoFingerMode();
			threeFinger = false;
			dragging = false;
			longPressReady = false;
			origin = lastPosition = position;
			startTime = timestamp;
			return TouchGestureResult.Empty;
		}

		public TouchGestureResult Move(int2 position)
		{
			if (!active)
				return TouchGestureResult.Empty;

			var delta = position - lastPosition;
			if (dragging)
			{
				lastPosition = position;
				return TouchGestureResult.One(Action(TouchPointerActionType.LeftMove, position, delta));
			}

			var offset = position - origin;
			if (offset.X * offset.X + offset.Y * offset.Y <= DragThreshold * DragThreshold)
			{
				lastPosition = position;
				return TouchGestureResult.Empty;
			}

			longPressReady = false;
			dragging = true;
			lastPosition = position;
			return TouchGestureResult.Two(
				Action(TouchPointerActionType.LeftDown, origin, int2.Zero),
				Action(TouchPointerActionType.LeftMove, position, offset));
		}

		public TouchGestureResult BeginSecond(int2 position)
		{
			return BeginSecond(position, startTime);
		}

		public TouchGestureResult BeginSecond(int2 position, ulong timestamp)
		{
			if (!active)
				return TouchGestureResult.Empty;

			var release = default(TouchPointerAction);
			var hasRelease = false;
			if (dragging)
			{
				release = Action(TouchPointerActionType.LeftCancel, lastPosition, int2.Zero);
				hasRelease = true;
			}

			active = false;
			dragging = false;
			longPressReady = false;
			twoFinger = true;
			twoFingerTapEligible = true;
			twoFingerLongPressReady = false;
			ResetTwoFingerMode();
			twoFingerMode = TwoFingerMode.Pending;
			secondPosition = position;
			firstTwoFingerOrigin = lastPosition;
			secondTwoFingerOrigin = secondPosition;
			lastCentroid = Centroid(lastPosition, secondPosition);
			lastZoomDistance = Distance(lastPosition, secondPosition);
			twoFingerStartTime = timestamp;
			return hasRelease ? TouchGestureResult.One(release) : TouchGestureResult.Empty;
		}

		public TouchGestureResult MovePrimary(int2 position)
		{
			return MovePrimary(position, twoFingerStartTime);
		}

		public TouchGestureResult MovePrimary(int2 position, ulong timestamp)
		{
			if (!twoFinger)
				return TouchGestureResult.Empty;

			var changed = position != lastPosition;
			lastPosition = position;
			return TwoFingerMove(timestamp, changed, false);
		}

		public TouchGestureResult MoveSecond(int2 position)
		{
			return MoveSecond(position, twoFingerStartTime);
		}

		public TouchGestureResult MoveSecond(int2 position, ulong timestamp)
		{
			if (!twoFinger)
				return TouchGestureResult.Empty;

			var changed = position != secondPosition;
			secondPosition = position;
			return TwoFingerMove(timestamp, false, changed);
		}

		public TouchGestureResult BeginThird(int2 position)
		{
			if (!twoFinger)
				return TouchGestureResult.Empty;

			var wasPanning = twoFingerMode == TwoFingerMode.Pan;
			thirdPosition = position;
			threeFinger = true;
			twoFinger = false;
			twoFingerTapEligible = false;
			twoFingerLongPressReady = false;
			ResetTwoFingerMode();
			var oldCentroid = lastCentroid;
			lastCentroid = Centroid(lastPosition, secondPosition, thirdPosition);
			var down = Action(TouchPointerActionType.MiddleDown, lastCentroid, int2.Zero);
			return wasPanning ? TouchGestureResult.Two(
				Action(TouchPointerActionType.MiddleUp, oldCentroid, int2.Zero), down) :
				TouchGestureResult.One(down);
		}

		public TouchGestureResult MoveThird(int2 position)
		{
			if (!threeFinger)
				return TouchGestureResult.Empty;

			thirdPosition = position;
			return ThreeFingerMove();
		}

		public TouchGestureResult MoveThreeFingerPrimary(int2 position)
		{
			if (!threeFinger)
				return TouchGestureResult.Empty;

			lastPosition = position;
			return ThreeFingerMove();
		}

		public TouchGestureResult MoveThreeFingerSecond(int2 position)
		{
			if (!threeFinger)
				return TouchGestureResult.Empty;

			secondPosition = position;
			return ThreeFingerMove();
		}

		TouchGestureResult ThreeFingerMove()
		{
			var centroid = Centroid(lastPosition, secondPosition, thirdPosition);
			var result = TouchGestureResult.One(Action(TouchPointerActionType.MiddleMove, centroid, centroid - lastCentroid));
			lastCentroid = centroid;
			return result;
		}

		public TouchGestureResult EndThreeFinger()
		{
			if (!threeFinger)
				return TouchGestureResult.Empty;

			threeFinger = false;
			active = false;
			return TouchGestureResult.One(Action(TouchPointerActionType.MiddleUp, lastCentroid, int2.Zero));
		}

		TouchGestureResult TwoFingerMove(ulong timestamp,
			bool firstPositionChanged = false, bool secondPositionChanged = false)
		{
			var centroid = Centroid(lastPosition, secondPosition);
			var firstDelta = lastPosition - firstTwoFingerOrigin;
			var secondDelta = secondPosition - secondTwoFingerOrigin;
			var firstDistanceSquared = LengthSquared(firstDelta);
			var secondDistanceSquared = LengthSquared(secondDelta);
			const int thresholdSquared = DragThreshold * DragThreshold;
			if (firstDistanceSquared > thresholdSquared)
				firstFingerMoved = true;
			if (secondDistanceSquared > thresholdSquared)
				secondFingerMoved = true;
			if (firstFingerMoved || secondFingerMoved)
				twoFingerTapEligible = false;

			if (!arbitrationWindowStarted && firstFingerMoved != secondFingerMoved)
			{
				arbitrationWindowStarted = true;
				arbitrationWindowStart = timestamp;
			}

			if (twoFingerMode == TwoFingerMode.Pan)
			{
				var delta = centroid - lastCentroid;
				if (delta == int2.Zero)
					return TouchGestureResult.Empty;

				lastCentroid = centroid;
				return TouchGestureResult.One(Action(TouchPointerActionType.MiddleMove, centroid, delta));
			}

			if (twoFingerMode == TwoFingerMode.Pinch)
				return LockedPinchMove(
					centroid, timestamp, firstPositionChanged, secondPositionChanged);

			if (twoFingerMode != TwoFingerMode.Pending)
				return TouchGestureResult.Empty;

			var initialCentroid = Centroid(firstTwoFingerOrigin, secondTwoFingerOrigin);
			var panEvidence = Distance(centroid, initialCentroid);
			var pinchEvidence = Math.Abs(Distance(lastPosition, secondPosition) -
				Distance(firstTwoFingerOrigin, secondTwoFingerOrigin));
			if (firstFingerMoved && secondFingerMoved &&
				SameDirection(firstDelta, secondDelta, firstDistanceSquared, secondDistanceSquared) &&
				panEvidence >= DragThreshold && panEvidence >= GestureDominanceRatio * pinchEvidence)
			{
				twoFingerMode = TwoFingerMode.Pan;
				var down = Action(TouchPointerActionType.MiddleDown, lastCentroid, int2.Zero);
				var pan = Action(TouchPointerActionType.MiddleMove, centroid, centroid - lastCentroid);
				lastCentroid = centroid;
				return TouchGestureResult.Two(down, pan);
			}

			if (pinchEvidence < PinchThreshold || pinchEvidence < GestureDominanceRatio * panEvidence)
				return TouchGestureResult.Empty;

			if (firstFingerMoved != secondFingerMoved &&
				(!arbitrationWindowStarted ||
					!HasElapsed(timestamp, arbitrationWindowStart, TwoFingerArbitrationDelay)))
				return TouchGestureResult.Empty;

			twoFingerTapEligible = false;
			twoFingerMode = TwoFingerMode.Pinch;
			var pinch = PinchScroll(centroid);
			SettlePinchPair();
			return pinch;
		}

		TouchGestureResult LockedPinchMove(int2 centroid, ulong timestamp,
			bool firstPositionChanged, bool secondPositionChanged)
		{
			if (firstPositionChanged)
				pinchFirstChanged = true;
			if (secondPositionChanged)
				pinchSecondChanged = true;
			if (!pinchFirstChanged && !pinchSecondChanged)
				return TouchGestureResult.Empty;

			if (!pinchPairWindowStarted)
			{
				pinchPairWindowStarted = true;
				pinchPairWindowStart = timestamp;
			}

			if (!(pinchFirstChanged && pinchSecondChanged) &&
				!HasElapsed(timestamp, pinchPairWindowStart, TwoFingerArbitrationDelay))
				return TouchGestureResult.Empty;

			var distance = Distance(lastPosition, secondPosition);
			var settledDistance = Distance(settledPinchFirstPosition, settledPinchSecondPosition);
			var translation = Distance(centroid,
				Centroid(settledPinchFirstPosition, settledPinchSecondPosition));
			var separationChange = Math.Abs(distance - settledDistance);
			var commonTranslation = pinchFirstChanged && pinchSecondChanged &&
				separationChange < PinchThreshold && translation > 0 &&
				translation >= GestureDominanceRatio * separationChange;
			var accumulatedSeparationChange = Math.Abs(distance - lastZoomDistance);
			var result = TouchGestureResult.Empty;
			if (!commonTranslation || accumulatedSeparationChange >= PinchThreshold)
				result = PinchScroll(centroid);

			SettlePinchPair();
			return result;
		}

		TouchGestureResult PinchScroll(int2 centroid)
		{
			var distance = Distance(lastPosition, secondPosition);
			var zoomDelta = distance - lastZoomDistance;
			if (Math.Abs(zoomDelta) < PinchThreshold)
				return TouchGestureResult.Empty;

			lastZoomDistance = distance;
			return TouchGestureResult.One(Action(TouchPointerActionType.Scroll, centroid,
				new int2(0, Math.Sign(zoomDelta))));
		}

		void SettlePinchPair()
		{
			settledPinchFirstPosition = lastPosition;
			settledPinchSecondPosition = secondPosition;
			pinchFirstChanged = false;
			pinchSecondChanged = false;
			pinchPairWindowStarted = false;
			pinchPairWindowStart = 0;
		}

		public TouchGestureResult EndTwoFinger()
		{
			if (!twoFinger)
				return TouchGestureResult.Empty;

			twoFinger = false;
			active = false;
			var wasPanning = twoFingerMode == TwoFingerMode.Pan;
			ResetTwoFingerMode();
			if (wasPanning)
			{
				return TouchGestureResult.One(Action(TouchPointerActionType.MiddleUp, lastCentroid, int2.Zero));
			}

			if (twoFingerTapEligible && !twoFingerLongPressReady)
			{
				twoFingerTapEligible = false;
				var centroid = Centroid(lastPosition, secondPosition);
				return TouchGestureResult.Two(
					Action(TouchPointerActionType.RightDown, centroid, int2.Zero),
					Action(TouchPointerActionType.RightUp, centroid, int2.Zero));
			}

			twoFingerTapEligible = false;
			twoFingerLongPressReady = false;
			return TouchGestureResult.Empty;
		}

		public TouchGestureResult End(int2 position)
		{
			if (!active)
				return TouchGestureResult.Empty;

			active = false;
			if (longPressReady)
			{
				longPressReady = false;
				return TouchGestureResult.Two(
					Action(TouchPointerActionType.RightDown, position, int2.Zero),
					Action(TouchPointerActionType.RightUp, position, int2.Zero));
			}

			if (!dragging)
				return TouchGestureResult.Two(
					Action(TouchPointerActionType.LeftDown, position, int2.Zero),
					Action(TouchPointerActionType.LeftUp, position, int2.Zero));

			dragging = false;
			return TouchGestureResult.One(Action(TouchPointerActionType.LeftUp, position, int2.Zero));
		}

		public TouchGestureResult Poll(ulong timestamp)
		{
			if (twoFinger)
			{
				if (!twoFingerLongPressReady && HasElapsed(timestamp, twoFingerStartTime, LongPressDelay))
				{
					twoFingerLongPressReady = true;
					twoFingerTapEligible = false;
				}

				if (twoFingerMode == TwoFingerMode.Pending)
					return TwoFingerMove(timestamp);
				if (twoFingerMode == TwoFingerMode.Pinch)
					return LockedPinchMove(
						Centroid(lastPosition, secondPosition), timestamp, false, false);

				return TouchGestureResult.Empty;
			}

			if (!active || dragging || longPressReady || !HasElapsed(timestamp, startTime, LongPressDelay))
				return TouchGestureResult.Empty;

			longPressReady = true;
			return TouchGestureResult.Empty;
		}

		public TouchGestureResult Cancel()
		{
			if (threeFinger)
			{
				threeFinger = false;
				active = false;
				twoFinger = false;
				dragging = false;
				longPressReady = false;
				ResetTwoFingerMode();
				return TouchGestureResult.One(Action(TouchPointerActionType.MiddleUp, lastCentroid, int2.Zero));
			}

			if (twoFinger)
			{
				var wasPanning = twoFingerMode == TwoFingerMode.Pan;
				twoFinger = false;
				active = false;
				dragging = false;
				longPressReady = false;
				twoFingerTapEligible = false;
				twoFingerLongPressReady = false;
				ResetTwoFingerMode();
				if (wasPanning)
				{
					return TouchGestureResult.One(Action(TouchPointerActionType.MiddleUp, lastCentroid, int2.Zero));
				}

				return TouchGestureResult.Empty;
			}

			if (!active)
				return TouchGestureResult.Empty;

			active = false;
			longPressReady = false;

			if (dragging)
			{
				dragging = false;
				return TouchGestureResult.One(Action(TouchPointerActionType.LeftCancel, lastPosition, int2.Zero));
			}

			return TouchGestureResult.Empty;
		}

		void ResetTwoFingerMode()
		{
			twoFingerMode = TwoFingerMode.None;
			firstFingerMoved = false;
			secondFingerMoved = false;
			arbitrationWindowStarted = false;
			pinchFirstChanged = false;
			pinchSecondChanged = false;
			pinchPairWindowStarted = false;
			settledPinchFirstPosition = int2.Zero;
			settledPinchSecondPosition = int2.Zero;
			arbitrationWindowStart = 0;
			pinchPairWindowStart = 0;
		}

		static TouchPointerAction Action(TouchPointerActionType type, int2 position, int2 delta)
		{
			return new TouchPointerAction(type, position, delta);
		}

		static int2 Centroid(int2 first, int2 second)
		{
			return new int2((first.X + second.X) / 2, (first.Y + second.Y) / 2);
		}

		static int2 Centroid(int2 first, int2 second, int2 third)
		{
			return new int2((first.X + second.X + third.X) / 3, (first.Y + second.Y + third.Y) / 3);
		}

		static double Distance(int2 first, int2 second)
		{
			var delta = first - second;
			return Math.Sqrt((double)delta.X * delta.X + (double)delta.Y * delta.Y);
		}

		static long LengthSquared(int2 value)
		{
			return (long)value.X * value.X + (long)value.Y * value.Y;
		}

		static bool HasElapsed(ulong timestamp, ulong start, ulong delay)
		{
			return timestamp >= start && timestamp - start >= delay;
		}

		static bool SameDirection(int2 first, int2 second, long firstLengthSquared, long secondLengthSquared)
		{
			const int thresholdSquared = DragThreshold * DragThreshold;
			if (firstLengthSquared <= thresholdSquared || secondLengthSquared <= thresholdSquared)
				return false;

			var dot = (long)first.X * second.X + (long)first.Y * second.Y;
			if (dot <= 0)
				return false;

			const double minimumCosine = 0.7;
			return (double)dot * dot >= minimumCosine * minimumCosine *
				firstLengthSquared * secondLengthSquared;
		}
	}
}
