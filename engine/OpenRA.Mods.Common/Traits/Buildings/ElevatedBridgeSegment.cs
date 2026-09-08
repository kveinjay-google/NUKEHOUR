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

using System;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Destroyable elevated bridge segment that enables/disables ElevatedBridgeLayer passability.")]
	public class ElevatedBridgeSegmentInfo : TraitInfo, IRulesetLoaded, Requires<IHealthInfo>
	{
		[Desc("Terrain type granted on the elevated movement layer while this segment is alive.")]
		public readonly string TerrainType = "Road";

		public readonly string Type = "ElevatedBridge";

		[Desc("Cells relative to the actor location that this segment makes passable.")]
		public readonly CVec[] FootprintOffsets = { new(-1, 0), new(0, 0), new(1, 0) };

		public readonly CVec[] NeighbourOffsets = Array.Empty<CVec>();

		[WeaponReference]
		[Desc("The name of the weapon to use when demolishing the bridge")]
		public readonly string DemolishWeapon = "Demolish";

		public WeaponInfo DemolishWeaponInfo { get; private set; }

		[Desc("Types of damage that this bridge causes to units over/in path of it while being destroyed/repaired.")]
		public readonly BitSet<DamageType> DamageTypes = default;

		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			var weaponToLower = (DemolishWeapon ?? string.Empty).ToLowerInvariant();
			if (!rules.Weapons.TryGetValue(weaponToLower, out var weapon))
				throw new YamlException($"Weapons Ruleset does not contain an entry '{weaponToLower}'");

			DemolishWeaponInfo = weapon;
		}

		public override object Create(ActorInitializer init) { return new ElevatedBridgeSegment(init.Self, this); }
	}

	public class ElevatedBridgeSegment : IBridgeSegment, INotifyAddedToWorld, INotifyRemovedFromWorld
	{
		readonly ElevatedBridgeSegmentInfo info;
		readonly Actor self;
		readonly BridgeLayer bridgeLayer;
		readonly ElevatedBridgeLayer elevatedBridgeLayer;
		readonly CPos[] cells;
		readonly IHealth health;

		public ElevatedBridgeSegment(Actor self, ElevatedBridgeSegmentInfo info)
		{
			this.info = info;
			this.self = self;
			health = self.Trait<IHealth>();
			bridgeLayer = self.World.WorldActor.Trait<BridgeLayer>();
			elevatedBridgeLayer = self.World.WorldActor.Trait<ElevatedBridgeLayer>();
			cells = info.FootprintOffsets.Select(o => self.Location + o).ToArray();
		}

		void INotifyAddedToWorld.AddedToWorld(Actor self)
		{
			bridgeLayer.Add(self, cells);
			elevatedBridgeLayer.EnableBridgeCells(cells);
			KillInvalidActorsInFootprint(self);
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			bridgeLayer.Remove(self, cells);
			elevatedBridgeLayer.DisableBridgeCells(cells);
			KillInvalidActorsInFootprint(self);
		}

		void KillInvalidActorsInFootprint(Actor self)
		{
			foreach (var c in cells)
			{
				var layered = new CPos(c.X, c.Y, CustomMovementLayerType.ElevatedBridge);
				foreach (var a in self.World.ActorMap.GetActorsAt(layered))
					if (a.Info.HasTraitInfo<IPositionableInfo>() && !a.Trait<IPositionable>().CanExistInCell(layered))
						a.Kill(self, info.DamageTypes);
			}
		}

		void IBridgeSegment.Repair(Actor repairer)
		{
			health.InflictDamage(self, repairer, new Damage(-health.MaxHP), true);
		}

		void IBridgeSegment.Demolish(Actor saboteur, BitSet<DamageType> damageTypes)
		{
			self.World.AddFrameEndTask(w =>
			{
				if (self.IsDead)
					return;

				info.DemolishWeaponInfo.Impact(Target.FromPos(self.CenterPosition), saboteur);
				self.Kill(saboteur, damageTypes);
			});
		}

		string IBridgeSegment.Type => info.Type;
		DamageState IBridgeSegment.DamageState => self.GetDamageState();
		bool IBridgeSegment.Valid => self.IsInWorld;
		CVec[] IBridgeSegment.NeighbourOffsets => info.NeighbourOffsets;
		CPos IBridgeSegment.Location => self.Location;
	}
}
