using System;
using OpenRA.Primitives;

namespace OpenRA
{
	public sealed class VirtualViewportJoystickState
	{
		readonly float deadZone;
		int2 center;

		public int Radius { get; private set; }
		public bool Active { get; private set; }
		public int2 ThumbOffset { get; private set; }
		public (float X, float Y) Direction { get; private set; }
		public float Speed { get; private set; }

		public VirtualViewportJoystickState(int radius, float deadZone)
		{
			Radius = Math.Max(1, radius);
			this.deadZone = Math.Clamp(deadZone, 0, 0.99f);
		}

		public void SetRadius(int radius)
		{
			var normalized = Math.Max(1, radius);
			if (normalized == Radius)
				return;

			End();
			Radius = normalized;
		}

		public void Begin(int2 position)
		{
			center = position;
			Active = true;
			Update(position);
		}

		public void Move(int2 position)
		{
			if (Active)
				Update(position);
		}

		void Update(int2 position)
		{
			var delta = position - center;
			var length = (float)Math.Sqrt((double)delta.X * delta.X + (double)delta.Y * delta.Y);
			if (length <= 0)
			{
				ThumbOffset = int2.Zero;
				Direction = (0, 0);
				Speed = 0;
				return;
			}

			var clamped = Math.Min(length, Radius);
			var nx = delta.X / length;
			var ny = delta.Y / length;
			ThumbOffset = new int2((int)Math.Round(nx * clamped), (int)Math.Round(ny * clamped));
			Direction = (nx, ny);
			var normalized = clamped / Radius;
			Speed = normalized <= deadZone ? 0 : (normalized - deadZone) / (1 - deadZone);
		}

		public void End()
		{
			Active = false;
			ThumbOffset = int2.Zero;
			Direction = (0, 0);
			Speed = 0;
		}
	}
}
