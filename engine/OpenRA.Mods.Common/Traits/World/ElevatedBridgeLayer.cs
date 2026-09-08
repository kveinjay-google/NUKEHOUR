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

using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.World)]
	public class ElevatedBridgeLayerInfo : TraitInfo, ILobbyCustomRulesIgnore, ICustomMovementLayerInfo
	{
		[Desc("Terrain type used by cells outside any elevated bridge footprint.")]
		public readonly string ImpassableTerrainType = "Impassable";

		public override object Create(ActorInitializer init) { return new ElevatedBridgeLayer(init.Self, this); }
	}

	public class ElevatedBridgeLayer : ICustomMovementLayer, IWorldLoaded
	{
		readonly World world;
		readonly Map map;
		readonly CellLayer<WPos> cellCenters;
		readonly CellLayer<byte> terrainIndices;
		readonly HashSet<CPos> ends = new();
		readonly byte impassableTerrainIndex;
		byte bridgeTerrainIndex;
		bool enabled;

		public ElevatedBridgeLayer(Actor self, ElevatedBridgeLayerInfo info)
		{
			world = self.World;
			map = self.World.Map;
			cellCenters = new CellLayer<WPos>(map);
			terrainIndices = new CellLayer<byte>(map);
			impassableTerrainIndex = map.Rules.TerrainInfo.GetTerrainIndex(info.ImpassableTerrainType);
			bridgeTerrainIndex = map.Rules.TerrainInfo.GetTerrainIndex("Road");
			terrainIndices.Clear(impassableTerrainIndex);
		}

		public void WorldLoaded(World world, WorldRenderer wr)
		{
			var cellHeight = world.Map.CellHeightStep.Length;
			foreach (var tti in world.WorldActor.Info.TraitInfos<ElevatedBridgePlaceholderInfo>())
			{
				enabled = true;
				bridgeTerrainIndex = map.Rules.TerrainInfo.GetTerrainIndex(tti.TerrainType);

				foreach (var c in tti.BridgeCells())
				{
					var uv = c.ToMPos(map);

					// Passability is granted by living ElevatedBridgeSegment actors.
					// Only bake cell centers (height) and on/off-ramp ends here.
					var pos = map.CenterOfCell(c);
					cellCenters[uv] = pos - new WVec(0, 0, pos.Z - cellHeight * tti.Height);
				}

				var end = tti.EndCells();
				foreach (var c in end)
				{
					// Need to explicitly set both default and elevated layers, otherwise the .Contains check will fail
					ends.Add(new CPos(c.X, c.Y, 0));
					ends.Add(new CPos(c.X, c.Y, CustomMovementLayerType.ElevatedBridge));

					// Ramp ends stay passable so units can enter/exit the elevated layer.
					// Mid-span cells remain impassable until an ElevatedBridgeSegment enables them.
					if (terrainIndices.Contains(c))
						terrainIndices[c] = bridgeTerrainIndex;
				}
			}
		}

		public void EnableBridgeCells(IEnumerable<CPos> cells)
		{
			foreach (var c in cells)
			{
				if (!terrainIndices.Contains(c))
					continue;

				terrainIndices[c] = bridgeTerrainIndex;
				RefreshLocomotors(new CPos(c.X, c.Y, CustomMovementLayerType.ElevatedBridge));
			}
		}

		public void DisableBridgeCells(IEnumerable<CPos> cells)
		{
			foreach (var c in cells)
			{
				if (!terrainIndices.Contains(c))
					continue;

				terrainIndices[c] = impassableTerrainIndex;
				RefreshLocomotors(new CPos(c.X, c.Y, CustomMovementLayerType.ElevatedBridge));
			}
		}

		void RefreshLocomotors(CPos layeredCell)
		{
			foreach (var locomotor in world.WorldActor.TraitsImplementing<Locomotor>())
				locomotor.RefreshCellCost(layeredCell);
		}

		bool ICustomMovementLayer.EnabledForLocomotor(LocomotorInfo li) { return enabled; }
		byte ICustomMovementLayer.Index => CustomMovementLayerType.ElevatedBridge;
		bool ICustomMovementLayer.InteractsWithDefaultLayer => true;
		bool ICustomMovementLayer.ReturnToGroundLayerOnIdle => false;

		WPos ICustomMovementLayer.CenterOfCell(CPos cell)
		{
			return cellCenters[cell];
		}

		short ICustomMovementLayer.EntryMovementCost(LocomotorInfo li, CPos cell)
		{
			return ends.Contains(cell) ? (short)0 : PathGraph.MovementCostForUnreachableCell;
		}

		short ICustomMovementLayer.ExitMovementCost(LocomotorInfo li, CPos cell)
		{
			return ends.Contains(cell) ? (short)0 : PathGraph.MovementCostForUnreachableCell;
		}

		byte ICustomMovementLayer.GetTerrainIndex(CPos cell)
		{
			return terrainIndices[cell];
		}
	}
}
