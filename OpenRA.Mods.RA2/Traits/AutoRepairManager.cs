#region Copyright & License Information
/*
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
	[Desc("Player trait: when enabled, automatically starts repairing damaged owned buildings.")]
	public class AutoRepairManagerInfo : TraitInfo
	{
		[Desc("Ticks between scans for damaged buildings.")]
		public readonly int ScanInterval = 10;

		public override object Create(ActorInitializer init) { return new AutoRepairManager(this); }
	}

	public class AutoRepairManager : ITick, IResolveOrder, ISync
	{
		readonly AutoRepairManagerInfo info;
		int ticks;

		[Sync]
		public bool Enabled { get; private set; }

		public AutoRepairManager(AutoRepairManagerInfo info)
		{
			this.info = info;
			ticks = info.ScanInterval;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != "AutoRepair")
				return;

			Enabled = !Enabled;
			ticks = 0;
		}

		void ITick.Tick(Actor self)
		{
			if (!Enabled || self.Owner.WinState != WinState.Undefined)
				return;

			if (--ticks > 0)
				return;

			ticks = info.ScanInterval;

			foreach (var actor in self.World.ActorsWithTrait<RepairableBuilding>())
			{
				var building = actor.Actor;
				var repairable = actor.Trait;

				if (building.Owner != self.Owner || building.IsDead || !building.IsInWorld)
					continue;

				if (repairable.IsTraitDisabled)
					continue;

				if (building.GetDamageState() == DamageState.Undamaged)
					continue;

				if (repairable.Repairers.Contains(self.Owner))
					continue;

				// Start one building per scan to avoid speech spam.
				repairable.RepairBuilding(building, self.Owner);
				break;
			}
		}
	}
}
