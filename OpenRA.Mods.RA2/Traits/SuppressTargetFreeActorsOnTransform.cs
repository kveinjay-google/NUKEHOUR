#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	public sealed class SlaveMinerRedeployInit : ValueActorInit<bool>, ISingleInstanceInit, ISuppressInitExport
	{
		public SlaveMinerRedeployInit(bool value)
			: base(value) { }
	}

	[Desc("Marks the transformed mobile Slave Miner as having already deployed its workers.")]
	public sealed class MarkSlaveMinerAsDeployedOnTransformInfo : TraitInfo, Requires<TransformsInfo>
	{
		public override object Create(ActorInitializer init) { return new MarkSlaveMinerAsDeployedOnTransform(); }
	}

	public sealed class MarkSlaveMinerAsDeployedOnTransform : ITransformActorInitModifier
	{
		void ITransformActorInitModifier.ModifyTransformActorInit(Actor self, TypeDictionary init)
		{
			init.Add(new SlaveMinerRedeployInit(true));
		}
	}

	[Desc("Prevents FreeActor traits on the transformed actor from spawning after the Slave Miner's first deployment.")]
	public sealed class SuppressTargetFreeActorsOnTransformInfo : TraitInfo, Requires<TransformsInfo>
	{
		public override object Create(ActorInitializer init) { return new SuppressTargetFreeActorsOnTransform(init); }
	}

	public sealed class SuppressTargetFreeActorsOnTransform : ITransformActorInitModifier
	{
		readonly Transforms transforms;
		readonly bool previouslyDeployed;

		public SuppressTargetFreeActorsOnTransform(ActorInitializer init)
		{
			transforms = init.Self.Trait<Transforms>();
			previouslyDeployed = init.GetValue<SlaveMinerRedeployInit, bool>(false);
		}

		void ITransformActorInitModifier.ModifyTransformActorInit(Actor self, TypeDictionary init)
		{
			if (!previouslyDeployed)
				return;

			var target = self.World.Map.Rules.Actors[transforms.Info.IntoActor];
			foreach (var freeActor in target.TraitInfos<FreeActorInfo>())
				init.Add(new FreeActorInit(freeActor, false));
		}
	}
}
