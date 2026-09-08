// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available to you under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.RA2.Activities;
using OpenRA.Mods.RA2.Traits;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class V3TerminalDivePolicyTest
	{
		static BallisticMissileInfo LoadInfo(string yaml)
		{
			var info = new BallisticMissileInfo();
			FieldLoader.Load(info, new MiniYaml(null, MiniYaml.FromString(yaml, "V3TerminalDivePolicyTest")));
			return info;
		}

		static BallisticMissileInfo ConfirmedInfo()
		{
			return LoadInfo(@"CreateAngle: 64
PrepareTick: 25
LaunchAngle: 160
Speed: 250
BeginCruiseAltitude: 3c0
BeginHitRange: 4c0
TurnSpeed: 25
TerminalDive: true
TerminalTurnSpeed: 48
HitAcceleration: 20
MaxHitSpeed: 350");
		}

		static List<TerminalDiveState> Simulate(BallisticMissileInfo info, WPos start, WPos target, WAngle yaw)
		{
			var state = BallisticMissileTerminalDivePolicy.CreateInitialState(info, start, yaw);
			var trace = new List<TerminalDiveState> { state };
			for (var i = 0; i < 240 && state.Phase != TerminalDivePhase.Done; i++)
			{
				state = BallisticMissileTerminalDivePolicy.Tick(info, start, target, state);
				trace.Add(state);
			}

			return trace;
		}

		static int ShortestAngleDistance(WAngle a, WAngle b)
		{
			var difference = (a - b).Angle;
			return Math.Min(difference, 1024 - difference);
		}

		static long Dot(WVec a, WVec b)
		{
			return (long)a.X * b.X + (long)a.Y * b.Y + (long)a.Z * b.Z;
		}

		static WAngle PitchOf(WVec vector)
		{
			return vector.LengthSquared == 0 ? WAngle.Zero : WAngle.ArcTan(vector.Z, vector.HorizontalLength);
		}

		static string TraceKey(TerminalDiveState state)
		{
			return $"{state.Phase}:{state.Position}:{state.Pitch.Angle}:{state.Yaw.Angle}:{state.Speed}:{state.PrepareTicksElapsed}";
		}

		static bool IsAllowedTransition(TerminalDivePhase previous, TerminalDivePhase current)
		{
			return previous == current ||
				(previous == TerminalDivePhase.Prepare && current == TerminalDivePhase.Launch) ||
				(previous == TerminalDivePhase.Launch && current == TerminalDivePhase.Cruise) ||
				(previous == TerminalDivePhase.Launch && current == TerminalDivePhase.Acquire) ||
				(previous == TerminalDivePhase.Cruise && current == TerminalDivePhase.Acquire) ||
				(previous == TerminalDivePhase.Acquire && current == TerminalDivePhase.Hit) ||
				(previous == TerminalDivePhase.Hit && current == TerminalDivePhase.Done);
		}

		[Test]
		public void TerminalDiveFieldsDefaultToLegacySafeValues()
		{
			var info = new BallisticMissileInfo();

			Assert.Multiple(() =>
			{
				Assert.That(info.TerminalDive, Is.False);
				Assert.That(info.TerminalTurnSpeed, Is.EqualTo(WAngle.Zero));
				Assert.That(info.MaxHitSpeed, Is.EqualTo(WDist.Zero));
			});
		}

		[Test]
		public void ZeroTerminalTurnSpeedFallsBackToRegularTurnSpeed()
		{
			var info = LoadInfo(@"TerminalDive: true
TurnSpeed: 25
TerminalTurnSpeed: 0
MaxHitSpeed: 350");

			Assert.DoesNotThrow(() => ((IRulesetLoaded<ActorInfo>)info).RulesetLoaded(null, new ActorInfo("test", info)));
			Assert.That(BallisticMissileTerminalDivePolicy.EffectiveTerminalTurnSpeed(info), Is.EqualTo(new WAngle(25)));
		}

		[TestCase(0, 25, 48, 350, "PrepareTick")]
		[TestCase(25, 0, 0, 350, "turn speed")]
		[TestCase(25, 25, 48, 0, "MaxHitSpeed")]
		public void InvalidTerminalConfigurationIsRejectedDuringRulesetLoad(
			int prepareTick, int turnSpeed, int terminalTurnSpeed, int maxHitSpeed, string expectedField)
		{
			var info = LoadInfo($@"TerminalDive: true
PrepareTick: {prepareTick}
TurnSpeed: {turnSpeed}
TerminalTurnSpeed: {terminalTurnSpeed}
MaxHitSpeed: {maxHitSpeed}");

			var exception = Assert.Throws<YamlException>(() =>
				((IRulesetLoaded<ActorInfo>)info).RulesetLoaded(null, new ActorInfo("test", info)));
			StringAssert.Contains(expectedField, exception.Message);
		}

		[Test]
		public void LegacyConfigurationDoesNotRequireTerminalValues()
		{
			var info = LoadInfo(@"TerminalDive: false
PrepareTick: 0
TurnSpeed: 0
TerminalTurnSpeed: 0
MaxHitSpeed: 0");

			Assert.DoesNotThrow(() => ((IRulesetLoaded<ActorInfo>)info).RulesetLoaded(null, new ActorInfo("legacy", info)));
		}

		[Test]
		public void TwentyFifthPrepareTickReachesLaunchAngleExactly()
		{
			var info = ConfirmedInfo();
			var start = new WPos(0, 640, 300);
			var target = new WPos(0, -10240, 0);
			var state = BallisticMissileTerminalDivePolicy.CreateInitialState(info, start, WAngle.Zero);

			for (var i = 0; i < 24; i++)
				state = BallisticMissileTerminalDivePolicy.Tick(info, start, target, state);

			Assert.Multiple(() =>
			{
				Assert.That(state.Phase, Is.EqualTo(TerminalDivePhase.Prepare));
				Assert.That(state.Pitch, Is.Not.EqualTo(info.LaunchAngle));
			});

			state = BallisticMissileTerminalDivePolicy.Tick(info, start, target, state);
			Assert.Multiple(() =>
			{
				Assert.That(state.Phase, Is.EqualTo(TerminalDivePhase.Launch));
				Assert.That(state.Pitch, Is.EqualTo(info.LaunchAngle));
				Assert.That(state.PrepareTicksElapsed, Is.EqualTo(25));
			});
		}

		[Test]
		public void TurnTowardsUsesTheShortestWrappedDirection()
		{
			Assert.Multiple(() =>
			{
				Assert.That(
					BallisticMissileTerminalDivePolicy.TurnTowards(new WAngle(1000), new WAngle(100), new WAngle(48)),
					Is.EqualTo(new WAngle(24)));
				Assert.That(
					BallisticMissileTerminalDivePolicy.TurnTowards(new WAngle(10), new WAngle(1010), new WAngle(48)),
					Is.EqualTo(new WAngle(1010)));
			});
		}

		[Test]
		public void AcquireTurnsInPlaceAndLocksBeforeHit()
		{
			var info = ConfirmedInfo();
			var start = new WPos(0, 0, 3072);
			var target = new WPos(2600, -2200, 0);
			var state = new TerminalDiveState(
				TerminalDivePhase.Acquire, start, new WAngle(160), new WAngle(1000), info.Speed.Length, info.PrepareTick);
			var acquireTicks = 0;

			while (state.Phase == TerminalDivePhase.Acquire && acquireTicks < 12)
			{
				var previous = state;
				state = BallisticMissileTerminalDivePolicy.Tick(info, start, target, state);
				acquireTicks++;
				Assert.Multiple(() =>
				{
					Assert.That(state.Position, Is.EqualTo(previous.Position));
					Assert.That(ShortestAngleDistance(state.Pitch, previous.Pitch), Is.LessThanOrEqualTo(48));
					Assert.That(ShortestAngleDistance(state.Yaw, previous.Yaw), Is.LessThanOrEqualTo(48));
				});
			}

			var targetVector = target - start;
			Assert.Multiple(() =>
			{
				Assert.That(state.Phase, Is.EqualTo(TerminalDivePhase.Hit));
				Assert.That(acquireTicks, Is.LessThanOrEqualTo(11));
				Assert.That(state.Pitch, Is.EqualTo(PitchOf(targetVector)));
				Assert.That(state.Yaw, Is.EqualTo(targetVector.Yaw));
			});
		}

		[Test]
		public void HitAccelerationIsCappedAndCannotOvershoot()
		{
			var info = ConfirmedInfo();
			var start = WPos.Zero;
			var target = new WPos(0, -1000, 0);
			var state = new TerminalDiveState(
				TerminalDivePhase.Hit, start, WAngle.Zero, WAngle.Zero, 340, info.PrepareTick);

			var next = BallisticMissileTerminalDivePolicy.Tick(info, start, target, state);

			Assert.Multiple(() =>
			{
				Assert.That(next.Speed, Is.EqualTo(350));
				Assert.That((next.Position - start).Length, Is.LessThanOrEqualTo(350));
				Assert.That(next.Position, Is.EqualTo(new WPos(0, -350, 0)));
				Assert.That(next.Phase, Is.EqualTo(TerminalDivePhase.Hit));
			});

			var nearTarget = new WPos(0, -100, 0);
			next = BallisticMissileTerminalDivePolicy.Tick(info, start, nearTarget, state);
			Assert.Multiple(() =>
			{
				Assert.That(next.Position, Is.EqualTo(nearTarget));
				Assert.That((next.Position - start).Length, Is.EqualTo(100));
				Assert.That(next.Phase, Is.EqualTo(TerminalDivePhase.Done));
			});
		}

		[Test]
		public void HitInterpolationMakesTheExactStepAtLargeWorldCoordinates()
		{
			var info = LoadInfo(@"TerminalDive: true
TurnSpeed: 25
TerminalTurnSpeed: 48
HitAcceleration: 20
MaxHitSpeed: 100000000");
			var start = new WPos(1000000000, 0, 0);
			var target = new WPos(-1000000000, 0, 0);
			var state = new TerminalDiveState(
				TerminalDivePhase.Hit, start, WAngle.Zero, new WAngle(768), 99999980, 0);
			var remainingBefore = (target - start).Length;

			var next = BallisticMissileTerminalDivePolicy.Tick(info, start, target, state);
			var movement = next.Position - start;
			var remainingAfter = (target - next.Position).Length;

			Assert.Multiple(() =>
			{
				Assert.That(next.Phase, Is.EqualTo(TerminalDivePhase.Hit));
				Assert.That(next.Speed, Is.EqualTo(100000000));
				Assert.That(movement.Length, Is.EqualTo(100000000));
				Assert.That(remainingAfter, Is.LessThan(remainingBefore));
				Assert.That(remainingAfter, Is.EqualTo(1900000000));
			});
		}

		[TestCase(5)]
		[TestCase(10)]
		[TestCase(18)]
		public void ConfirmedRangesFinishDeterministicallyWithoutOvershoot(int rangeInCells)
		{
			var info = ConfirmedInfo();
			var start = new WPos(0, 640, 300);
			var target = new WPos(0, -rangeInCells * 1024, 0);
			var trace = Simulate(info, start, target, WAngle.Zero);
			var repeat = Simulate(info, start, target, WAngle.Zero);
			var axis = target - start;

			Assert.Multiple(() =>
			{
				Assert.That(trace[^1].Phase, Is.EqualTo(TerminalDivePhase.Done));
				Assert.That(trace[^1].Position, Is.EqualTo(target));
				Assert.That(trace.Count - 1 - info.PrepareTick, Is.LessThanOrEqualTo(160));
				Assert.That(trace.Zip(trace.Skip(1), (a, b) => IsAllowedTransition(a.Phase, b.Phase)), Is.All.True);
				Assert.That(trace.Select(TraceKey), Is.EqualTo(repeat.Select(TraceKey)));
			});

			foreach (var state in trace)
				Assert.That(Dot(state.Position - start, axis), Is.LessThanOrEqualTo(axis.LengthSquared));

			var acquireTicks = 0;
			for (var i = 1; i < trace.Count; i++)
			{
				var previous = trace[i - 1];
				var current = trace[i];
				if (previous.Phase == TerminalDivePhase.Acquire)
				{
					acquireTicks++;
					Assert.Multiple(() =>
					{
						Assert.That(current.Position, Is.EqualTo(previous.Position));
						Assert.That(ShortestAngleDistance(current.Pitch, previous.Pitch), Is.LessThanOrEqualTo(48));
						Assert.That(ShortestAngleDistance(current.Yaw, previous.Yaw), Is.LessThanOrEqualTo(48));
					});
				}

				if (previous.Phase != TerminalDivePhase.Hit)
					continue;

				var movement = current.Position - previous.Position;
				Assert.That(movement.Length, Is.LessThanOrEqualTo(350));
				Assert.That((target - current.Position).Length, Is.LessThan((target - previous.Position).Length));
				if (movement.LengthSquared != 0)
				{
					Assert.That(current.Pitch, Is.EqualTo(PitchOf(movement)));
					Assert.That(current.Yaw, Is.EqualTo(movement.Yaw));
				}
			}

			Assert.That(acquireTicks, Is.LessThanOrEqualTo(8));
			if (rangeInCells == 5)
				Assert.That(trace, Has.None.Matches<TerminalDiveState>(state => state.Phase == TerminalDivePhase.Cruise));
		}

		[TestCase(0, 0)]
		[TestCase(256, 1024)]
		[TestCase(512, -1024)]
		[TestCase(768, 0)]
		public void CardinalYawAndTargetAltitudeRemainDeterministic(int yaw, int targetAltitude)
		{
			var info = ConfirmedInfo();
			var start = new WPos(0, 640, 300);
			var horizontal = new WVec(0, -10240, 0).Rotate(new WRot(WAngle.Zero, WAngle.Zero, new WAngle(yaw)));
			var target = WPos.Zero + new WVec(horizontal.X, horizontal.Y, targetAltitude);
			var trace = Simulate(info, start, target, new WAngle(yaw));
			var repeat = Simulate(info, start, target, new WAngle(yaw));

			Assert.Multiple(() =>
			{
				Assert.That(trace[^1].Phase, Is.EqualTo(TerminalDivePhase.Done));
				Assert.That(trace[^1].Position, Is.EqualTo(target));
				Assert.That(trace.Select(TraceKey), Is.EqualTo(repeat.Select(TraceKey)));
			});
		}
	}
}
