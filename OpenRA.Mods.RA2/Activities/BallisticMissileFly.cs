#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenRA.Activities;
using OpenRA.Mods.RA2.Traits;
using OpenRA.Traits;

[assembly: InternalsVisibleTo("OpenRA.Test")]

namespace OpenRA.Mods.RA2.Activities
{
	enum TerminalDivePhase { Prepare, Launch, Cruise, Acquire, Hit, Done }

	readonly struct TerminalDiveState
	{
		public readonly TerminalDivePhase Phase;
		public readonly WPos Position;
		public readonly WAngle Pitch;
		public readonly WAngle Yaw;
		public readonly int Speed;
		public readonly int PrepareTicksElapsed;

		public TerminalDiveState(
			TerminalDivePhase phase,
			WPos position,
			WAngle pitch,
			WAngle yaw,
			int speed,
			int prepareTicksElapsed)
		{
			Phase = phase;
			Position = position;
			Pitch = pitch;
			Yaw = yaw;
			Speed = speed;
			PrepareTicksElapsed = prepareTicksElapsed;
		}
	}

	static class BallisticMissileTerminalDivePolicy
	{
		internal static WAngle EffectiveTerminalTurnSpeed(BallisticMissileInfo info)
		{
			return info.TerminalTurnSpeed != WAngle.Zero ? info.TerminalTurnSpeed : info.TurnSpeed;
		}

		internal static TerminalDiveState CreateInitialState(BallisticMissileInfo info, WPos position, WAngle yaw)
		{
			var initialSpeed = info.LaunchAcceleration == WDist.Zero ? info.Speed.Length : 0;
			return new TerminalDiveState(TerminalDivePhase.Prepare, position, info.CreateAngle, yaw, initialSpeed, 0);
		}

		internal static TerminalDiveState Tick(
			BallisticMissileInfo info,
			WPos initialPosition,
			WPos targetPosition,
			TerminalDiveState state)
		{
			switch (state.Phase)
			{
				case TerminalDivePhase.Prepare:
					return Prepare(info, state);
				case TerminalDivePhase.Launch:
					return Launch(info, initialPosition, targetPosition, state);
				case TerminalDivePhase.Cruise:
					return Cruise(info, targetPosition, state);
				case TerminalDivePhase.Acquire:
					return Acquire(info, targetPosition, state);
				case TerminalDivePhase.Hit:
					return Hit(info, targetPosition, state);
				default:
					return state;
			}
		}

		static TerminalDiveState Prepare(BallisticMissileInfo info, TerminalDiveState state)
		{
			var elapsed = Math.Min(state.PrepareTicksElapsed + 1, info.PrepareTick);
			var pitch = WAngle.Lerp(info.CreateAngle, info.LaunchAngle, elapsed, info.PrepareTick);
			var phase = elapsed == info.PrepareTick ? TerminalDivePhase.Launch : TerminalDivePhase.Prepare;
			return new TerminalDiveState(phase, state.Position, pitch, state.Yaw, state.Speed, elapsed);
		}

		static TerminalDiveState Launch(
			BallisticMissileInfo info,
			WPos initialPosition,
			WPos targetPosition,
			TerminalDiveState state)
		{
			if ((targetPosition - state.Position).HorizontalLength <= info.BeginHitRange.Length)
				return WithPhase(state, TerminalDivePhase.Acquire);

			var position = state.Position + ForwardStep(state.Speed, state.Pitch, state.Yaw);
			var speed = AddCapped(state.Speed, info.LaunchAcceleration.Length, info.Speed.Length);
			var phase = position.Z - initialPosition.Z >= info.BeginCruiseAltitude.Length
				? TerminalDivePhase.Cruise
				: TerminalDivePhase.Launch;
			return new TerminalDiveState(phase, position, state.Pitch, state.Yaw, speed, state.PrepareTicksElapsed);
		}

		static TerminalDiveState Cruise(BallisticMissileInfo info, WPos targetPosition, TerminalDiveState state)
		{
			if ((targetPosition - state.Position).HorizontalLength <= info.BeginHitRange.Length)
				return WithPhase(state, TerminalDivePhase.Acquire);

			var pitch = TurnTowards(state.Pitch, WAngle.Zero, info.TurnSpeed);
			var yaw = TurnTowards(state.Yaw, (targetPosition - state.Position).Yaw, info.TurnSpeed);
			var position = state.Position + ForwardStep(state.Speed, pitch, yaw);
			return new TerminalDiveState(
				TerminalDivePhase.Cruise, position, pitch, yaw, state.Speed, state.PrepareTicksElapsed);
		}

		static TerminalDiveState Acquire(BallisticMissileInfo info, WPos targetPosition, TerminalDiveState state)
		{
			var remaining = targetPosition - state.Position;
			if (remaining.LengthSquared == 0)
				return new TerminalDiveState(
					TerminalDivePhase.Done, targetPosition, state.Pitch, state.Yaw, state.Speed, state.PrepareTicksElapsed);

			var targetPitch = PitchOf(remaining);
			var targetYaw = remaining.Yaw;
			var turnSpeed = EffectiveTerminalTurnSpeed(info);
			var pitch = TurnTowards(state.Pitch, targetPitch, turnSpeed);
			var yaw = TurnTowards(state.Yaw, targetYaw, turnSpeed);
			var phase = pitch == targetPitch && yaw == targetYaw ? TerminalDivePhase.Hit : TerminalDivePhase.Acquire;
			return new TerminalDiveState(phase, state.Position, pitch, yaw, state.Speed, state.PrepareTicksElapsed);
		}

		static TerminalDiveState Hit(BallisticMissileInfo info, WPos targetPosition, TerminalDiveState state)
		{
			var remaining = targetPosition - state.Position;
			if (remaining.LengthSquared == 0)
				return new TerminalDiveState(
					TerminalDivePhase.Done, targetPosition, state.Pitch, state.Yaw, state.Speed, state.PrepareTicksElapsed);

			var speed = AddCapped(state.Speed, info.HitAcceleration.Length, info.MaxHitSpeed.Length);
			var remainingDistance = remaining.Length;
			var stepLength = Math.Min(speed, remainingDistance);
			var position = stepLength == remainingDistance
				? targetPosition
				: WPos.Lerp(state.Position, targetPosition, (long)stepLength, remainingDistance);
			var movement = position - state.Position;
			var pitch = PitchOf(movement);
			var yaw = movement.Yaw;
			var phase = position == targetPosition ? TerminalDivePhase.Done : TerminalDivePhase.Hit;
			return new TerminalDiveState(phase, position, pitch, yaw, speed, state.PrepareTicksElapsed);
		}

		static TerminalDiveState WithPhase(TerminalDiveState state, TerminalDivePhase phase)
		{
			return new TerminalDiveState(
				phase, state.Position, state.Pitch, state.Yaw, state.Speed, state.PrepareTicksElapsed);
		}

		static int AddCapped(int value, int increment, int maximum)
		{
			return (int)Math.Min(maximum, (long)value + increment);
		}

		static WVec ForwardStep(int speed, WAngle pitch, WAngle yaw)
		{
			return new WVec(0, -speed, 0).Rotate(new WRot(pitch, WAngle.Zero, yaw));
		}

		static WAngle PitchOf(WVec vector)
		{
			return vector.LengthSquared == 0 ? WAngle.Zero : WAngle.ArcTan(vector.Z, vector.HorizontalLength);
		}

		internal static WAngle TurnTowards(WAngle current, WAngle target, WAngle maximumTurn)
		{
			var difference = target - current;
			if (difference == WAngle.Zero || difference.Angle <= maximumTurn.Angle ||
				1024 - difference.Angle <= maximumTurn.Angle)
				return target;

			return difference.Angle < 512 ? current + maximumTurn : current - maximumTurn;
		}
	}

	public class BallisticMissileFly : Activity
	{
		enum BMFlyStatus { Prepare, Launch, NoCruiseLaunch, LazyCurve, Cruise, Hit, Unknown }

		readonly BallisticMissile bm;
		readonly BallisticMissileInfo bmInfo;
		readonly WPos initPos;
		readonly WPos targetPos;
		int ticks = 0;
		BMFlyStatus status = BMFlyStatus.Prepare;

		int speed = 0;
		readonly int dSpeed = 0;

		readonly int horizontalLength;
		readonly WAngle preparePitchIncrement;

		readonly int lazyCurveLength = 0;
		int lazyCurveTick = 0;
		TerminalDiveState terminalDiveState;

		// Read-only audit state for the independently captured legacy trajectory fixture.
		// These properties do not participate in movement or synchronization.
		internal string LegacyAuditPhase => status.ToString();
		internal string LegacyAuditState => $"ticks={ticks};speed={speed};lazy={lazyCurveTick}";

		public BallisticMissileFly(Actor self, Target t, BallisticMissile bm)
		{
			this.bm = bm;
			bmInfo = bm.Info;
			initPos = self.CenterPosition;
			targetPos = t.CenterPosition;

			horizontalLength = (initPos - targetPos).HorizontalLength;

			if (bmInfo.LaunchAcceleration == WDist.Zero)
			{
				speed = bmInfo.Speed.Length;
				dSpeed = 0;
			}
			else
			{
				speed = 0;
				dSpeed = bmInfo.LaunchAcceleration.Length;
			}

			if (bmInfo.LazyCurve)
			{
				lazyCurveLength = Math.Max((targetPos - initPos).Length / this.bm.Info.Speed.Length, 1);
			}

			preparePitchIncrement = new WAngle((bmInfo.LaunchAngle - bmInfo.CreateAngle).Angle / bmInfo.PrepareTick);

			if (bmInfo.WithoutCruise)
			{
				preparePitchIncrement = new WAngle((new WAngle(256) - bmInfo.CreateAngle).Angle / bmInfo.PrepareTick);
			}

			if (bmInfo.TerminalDive)
				terminalDiveState = BallisticMissileTerminalDivePolicy.CreateInitialState(bmInfo, initPos, bm.Facing);
		}

		protected override void OnFirstRun(Actor self)
		{
			bm.Pitch = bmInfo.CreateAngle;
		}

		void MoveForward(Actor self)
		{
			var move = new WVec(0, -speed, 0).Rotate(new WRot(bm.Pitch, WAngle.Zero, bm.Facing));
			bm.SetPosition(self, bm.CenterPosition + move);
			if (!self.IsInWorld)
				status = BMFlyStatus.Unknown;
		}

		void PrepareStatusHandle(Actor self)
		{
			if (ticks < bmInfo.PrepareTick)
				bm.Pitch += preparePitchIncrement;
			else
			{
				if (bm.Info.AudibleThroughFog || (!self.World.ShroudObscures(bm.CenterPosition) && !self.World.FogObscures(bm.CenterPosition)))
					Game.Sound.Play(SoundType.World, bm.Info.LaunchSounds, self.World, bm.CenterPosition, null, bm.Info.SoundVolume);
				if (bmInfo.WithoutCruise)
				{
					status = BMFlyStatus.NoCruiseLaunch;
					return;
				}

				if (bmInfo.LazyCurve)
				{
					status = BMFlyStatus.LazyCurve;
					return;
				}

				status = BMFlyStatus.Launch;
			}
		}

		void LaunchStatusHandle(Actor self)
		{
			MoveForward(self);
			speed = speed + dSpeed > bmInfo.Speed.Length ? bmInfo.Speed.Length : speed + dSpeed;
			if (bm.CenterPosition.Z - initPos.Z > bmInfo.BeginCruiseAltitude.Length)
			{
				status = BMFlyStatus.Cruise;
			}
		}

		void CruiseStatusHandle(Actor self)
		{
			MoveForward(self);
			if (bm.Pitch != WAngle.Zero)
			{
				if ((bm.Pitch.Angle < bm.TurnSpeed.Angle) || (1024 - bm.Pitch.Angle < bm.TurnSpeed.Angle))
				{
					bm.Pitch = WAngle.Zero;
				}
				else
				{
					bm.Pitch -= bm.TurnSpeed;
				}
			}

			var targetYaw = (targetPos - bm.CenterPosition).Yaw;
			var yawDiff = targetYaw - bm.Facing;
			if (yawDiff != WAngle.Zero)
			{
				if ((yawDiff.Angle < bm.TurnSpeed.Angle) || (1024 - yawDiff.Angle < bm.TurnSpeed.Angle))
				{
					bm.Facing = targetYaw;
				}
				else
				{
					if (yawDiff.Angle < 512)
						bm.Facing += bm.TurnSpeed;
					else
						bm.Facing -= bm.TurnSpeed;
				}
			}

			if ((targetPos - bm.CenterPosition).HorizontalLength < bmInfo.BeginHitRange.Length)
			{
				status = BMFlyStatus.Hit;
			}
		}

		// release-20250330 WVec has no Pitch property (added in newer upstream); compute it locally.
		static WAngle PitchOf(WVec v)
		{
			return v.LengthSquared == 0 ? WAngle.Zero : WAngle.ArcTan(v.Z, v.HorizontalLength);
		}

		void HitStatusHandle(Actor self)
		{
			MoveForward(self);
			speed += bmInfo.HitAcceleration.Length;
			var targetPitch = PitchOf(targetPos - bm.CenterPosition);
			var pitchDiff = targetPitch - bm.Pitch;
			if (pitchDiff != WAngle.Zero)
			{
				if ((pitchDiff.Angle < bm.TurnSpeed.Angle) || (1024 - pitchDiff.Angle < bm.TurnSpeed.Angle))
				{
					bm.Pitch = targetPitch;
				}
				else
				{
					if (pitchDiff.Angle < 512)
						bm.Pitch += bm.TurnSpeed;
					else
						bm.Pitch -= bm.TurnSpeed;
				}
			}

			var targetYaw = (targetPos - bm.CenterPosition).Yaw;
			var yawDiff = targetYaw - bm.Facing;
			if (yawDiff != WAngle.Zero)
			{
				if ((yawDiff.Angle < bm.TurnSpeed.Angle) || (1024 - yawDiff.Angle < bm.TurnSpeed.Angle))
				{
					bm.Facing = targetYaw;
				}
				else
				{
					if (yawDiff.Angle < 512)
						bm.Facing += bm.TurnSpeed;
					else
						bm.Facing -= bm.TurnSpeed;
				}
			}

			if ((targetPos - bm.CenterPosition).Length < bmInfo.ExplosionRange.Length)
			{
				status = BMFlyStatus.Unknown;
			}
		}

		void NoCruiseLaunchStatusHandle(Actor self)
		{
			MoveForward(self);
			speed = speed + dSpeed > bmInfo.Speed.Length ? bmInfo.Speed.Length : speed + dSpeed;
			if (bm.CenterPosition.Z - initPos.Z < horizontalLength && (bm.CenterPosition - targetPos).HorizontalLength > horizontalLength * 3 / 5)
			{
				return;
			}

			if (bm.Pitch != WAngle.Zero)
			{
				var newTurnSpeed = new WAngle(8192 * bm.TurnSpeed.Angle / horizontalLength);
				if ((bm.Pitch.Angle < newTurnSpeed.Angle) || (1024 - bm.Pitch.Angle < newTurnSpeed.Angle))
				{
					bm.Pitch = WAngle.Zero;
				}
				else
				{
					bm.Pitch -= newTurnSpeed;
				}

				return;
			}

			status = BMFlyStatus.Hit;
		}

		void LazyCurveHandle(Actor self)
		{
			var pos = WPos.LerpQuadratic(initPos, targetPos, bm.Info.LaunchAngle, lazyCurveTick, lazyCurveLength);
			bm.Pitch = PitchOf(pos - bm.CenterPosition);
			bm.SetPosition(self, pos);
			lazyCurveTick++;
			if ((targetPos - bm.CenterPosition).Length < bmInfo.ExplosionRange.Length)
			{
				status = BMFlyStatus.Unknown;
			}
		}

		bool TickTerminalDive(Actor self)
		{
			var previousPhase = terminalDiveState.Phase;
			terminalDiveState = BallisticMissileTerminalDivePolicy.Tick(bmInfo, initPos, targetPos, terminalDiveState);
			bm.Pitch = terminalDiveState.Pitch;
			bm.Facing = terminalDiveState.Yaw;
			if (bm.CenterPosition != terminalDiveState.Position)
				bm.SetPosition(self, terminalDiveState.Position);

			if (previousPhase == TerminalDivePhase.Prepare && terminalDiveState.Phase != TerminalDivePhase.Prepare &&
				(bm.Info.AudibleThroughFog || (!self.World.ShroudObscures(bm.CenterPosition) && !self.World.FogObscures(bm.CenterPosition))))
				Game.Sound.Play(SoundType.World, bm.Info.LaunchSounds, self.World, bm.CenterPosition, null, bm.Info.SoundVolume);

			if (terminalDiveState.Phase != TerminalDivePhase.Done)
				return false;

			bm.SetPosition(self, targetPos);
			Queue(new CallFunc(() => self.Kill(self, bm.Info.DamageTypes)));
			return true;
		}

		public override bool Tick(Actor self)
		{
			if (bmInfo.TerminalDive)
				return TickTerminalDive(self);

			switch (status)
			{
				case BMFlyStatus.Prepare:
					PrepareStatusHandle(self);
					break;
				case BMFlyStatus.Launch:
					LaunchStatusHandle(self);
					break;
				case BMFlyStatus.NoCruiseLaunch:
					NoCruiseLaunchStatusHandle(self);
					break;
				case BMFlyStatus.Cruise:
					CruiseStatusHandle(self);
					break;
				case BMFlyStatus.Hit:
					HitStatusHandle(self);
					break;
				case BMFlyStatus.LazyCurve:
					LazyCurveHandle(self);
					break;
				default:
					bm.SetPosition(self, targetPos);
					Queue(new CallFunc(() => self.Kill(self, bm.Info.DamageTypes)));
					return true;
			}

			ticks++;
			return false;
		}

		public override IEnumerable<Target> GetTargets(Actor self)
		{
			yield return Target.FromPos(targetPos);
		}
	}
}
