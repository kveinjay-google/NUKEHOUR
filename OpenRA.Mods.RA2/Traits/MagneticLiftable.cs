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

using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	[Desc("This actor can be lifted and dragged by a MagneticBeam weapon.")]
	public class MagneticLiftableInfo : TraitInfo, Requires<MobileInfo>, Requires<ExternalConditionInfo>
	{
		[GrantedConditionReference]
		[Desc("External condition granted while this actor is lifted.")]
		public readonly string Condition = "magne-lift";

		[Desc("Height above the magnetron while being dragged.")]
		public readonly WDist LiftHeight = new(2048);

		[Desc("Horizontal pull speed per tick.")]
		public readonly WDist PullSpeed = new(256);

		[Desc("Stop approaching the magnetron inside this range.")]
		public readonly WDist MinRange = new(2048);

		[Desc("Damage types applied when the drop location is impassable.")]
		public readonly BitSet<DamageType> CrashDamageTypes = new("ExplosionDeath");

		public override object Create(ActorInitializer init) { return new MagneticLiftable(init, this); }
	}

	public class MagneticLiftable : ITick, INotifyKilled, INotifyRemovedFromWorld
	{
		readonly MagneticLiftableInfo info;
		readonly Mobile mobile;
		readonly ExternalCondition external;

		Actor magnetron;
		int token = Actor.InvalidConditionToken;

		public bool IsLifted => token != Actor.InvalidConditionToken;

		public MagneticLiftable(ActorInitializer init, MagneticLiftableInfo info)
		{
			this.info = info;
			mobile = init.Self.Trait<Mobile>();
			external = init.Self.TraitsImplementing<ExternalCondition>()
				.First(e => e.Info.Condition == info.Condition);
		}

		public void Capture(Actor self, Actor source)
		{
			if (source == null || source.IsDead || !source.IsInWorld)
				return;

			if (magnetron == source && IsLifted)
				return;

			Release(self, land: false);
			magnetron = source;
			token = external.GrantCondition(self, source);
			self.CancelActivity();
		}

		public void PullToward(Actor self, Actor source)
		{
			if (!IsLifted || source == null || source.IsDead)
				return;

			var origin = self.CenterPosition;
			var desired = source.CenterPosition + new WVec(0, 0, info.LiftHeight.Length);
			var delta = desired - origin;
			var minRange = info.MinRange.Length;
			var horizontal = new WVec(delta.X, delta.Y, 0);
			if (horizontal.Length > minRange)
			{
				var step = info.PullSpeed.Length;
				if (horizontal.Length > step)
					horizontal = horizontal * step / horizontal.Length;

				origin += horizontal;
			}

			origin = new WPos(origin.X, origin.Y, desired.Z);
			var cell = self.World.Map.CellContaining(origin);
			if (cell != self.Location)
			{
				mobile.SetLocation(cell, mobile.FromSubCell, cell, mobile.ToSubCell);
				self.World.UpdateMaps(self, mobile);
			}

			mobile.SetCenterPosition(self, origin);
		}

		public void Release(Actor self, bool land = true)
		{
			if (token != Actor.InvalidConditionToken)
			{
				external.TryRevokeCondition(self, magnetron, token);
				token = Actor.InvalidConditionToken;
			}

			magnetron = null;
			if (!land || self.IsDead || !self.IsInWorld)
				return;

			Land(self);
		}

		void Land(Actor self)
		{
			var cell = self.World.Map.CellContaining(self.CenterPosition);
			mobile.SetPosition(self, cell);

			if (!mobile.CanExistInCell(cell))
			{
				self.Kill(self, info.CrashDamageTypes);
				return;
			}

			if (self.World.ActorMap.GetActorsAt(cell).Any(a => a != self && a.Info.HasTraitInfo<BuildingInfo>()))
				self.Kill(self, info.CrashDamageTypes);
		}

		void ITick.Tick(Actor self)
		{
			if (!IsLifted)
				return;

			if (magnetron == null || magnetron.IsDead || !magnetron.IsInWorld)
				Release(self);
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			Release(self, land: false);
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			Release(self, land: false);
		}
	}
}
