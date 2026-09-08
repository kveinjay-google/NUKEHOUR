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

using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	[Desc("Lifts MagneticLiftable targets while this actor keeps firing the configured armament.")]
	public class MagneticBeamInfo : ConditionalTraitInfo, Requires<AttackBaseInfo>
	{
		[Desc("Armament name that triggers the lift beam.")]
		public readonly string Armament = "secondary";

		public override object Create(ActorInitializer init) { return new MagneticBeam(init, this); }
	}

	public class MagneticBeam : ConditionalTrait<MagneticBeamInfo>, INotifyAttack, ITick, INotifyKilled, INotifyRemovedFromWorld
	{
		readonly AttackBase attack;
		Actor captured;
		MagneticLiftable liftable;

		public MagneticBeam(ActorInitializer init, MagneticBeamInfo info)
			: base(info)
		{
			attack = init.Self.Trait<AttackBase>();
		}

		void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel) { }

		void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
		{
			if (IsTraitDisabled || a.Info.Name != Info.Armament)
				return;

			if (target.Type != TargetType.Actor || target.Actor == null || target.Actor.IsDead)
				return;

			var next = target.Actor.TraitOrDefault<MagneticLiftable>();
			if (next == null)
				return;

			if (captured != null && captured != target.Actor)
				ReleaseCurrent();

			captured = target.Actor;
			liftable = next;
			liftable.Capture(captured, self);
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitDisabled)
			{
				ReleaseCurrent();
				return;
			}

			if (captured == null || liftable == null)
				return;

			if (captured.IsDead || !captured.IsInWorld || !liftable.IsLifted || !attack.IsAiming)
			{
				ReleaseCurrent();
				return;
			}

			liftable.PullToward(captured, self);
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			ReleaseCurrent();
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			ReleaseCurrent();
		}

		void ReleaseCurrent()
		{
			if (captured != null && !captured.IsDead)
				liftable?.Release(captured);

			captured = null;
			liftable = null;
		}
	}
}
