using NUnit.Framework;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class VirtualViewportJoystickStateTest
	{
		[Test]
		public void DeadZoneProducesNoMovement()
		{
			var state = new VirtualViewportJoystickState(50, 0.15f);
			state.Begin(new int2(100, 100));
			state.Move(new int2(106, 100));
			Assert.That(state.Speed, Is.Zero);
		}

		[Test]
		public void DisplacementIsClampedAndNormalized()
		{
			var state = new VirtualViewportJoystickState(50, 0.15f);
			state.Begin(new int2(100, 100));
			state.Move(new int2(200, 100));
			Assert.That(state.ThumbOffset, Is.EqualTo(new int2(50, 0)));
			Assert.That(state.Direction.X, Is.EqualTo(1).Within(0.001));
			Assert.That(state.Direction.Y, Is.Zero.Within(0.001));
			Assert.That(state.Speed, Is.EqualTo(1).Within(0.001));
		}

		[Test]
		public void ReleaseRecentersAndStops()
		{
			var state = new VirtualViewportJoystickState(50, 0.15f);
			state.Begin(new int2(100, 100));
			state.Move(new int2(140, 100));
			state.End();
			Assert.That(state.Active, Is.False);
			Assert.That(state.ThumbOffset, Is.EqualTo(int2.Zero));
			Assert.That(state.Speed, Is.Zero);
		}
	}
}
