#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	[Desc("Links a slave worker to the Slave Miner that created it, including across mobile/deployed transformations.")]
	public sealed class SlaveMinerWorkerInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new SlaveMinerWorker(init); }
	}

	public sealed class SlaveMinerWorker : ITick, ISync
	{
		Actor parent;

		[Sync]
		int recoveryTicks = 25;

		public SlaveMinerWorker(ActorInitializer init)
		{
			var parentInit = init.GetOrDefault<ParentActorInit>()?.Value;
			if (parentInit != null)
				init.World.AddFrameEndTask(_ => parent = parentInit.Actor(init.World).Value);
		}

		public Actor CurrentMiner
		{
			get
			{
				var current = parent;
				while (current?.ReplacedByActor != null)
					current = current.ReplacedByActor;

				// Collapse the chain so repeated field migrations remain constant-time.
				parent = current;
				return current;
			}
		}

		void ITick.Tick(Actor self)
		{
			if (--recoveryTicks > 0)
				return;

			recoveryTicks = 25;
			if (!self.IsIdle || self.IsDead || !self.IsInWorld)
				return;

			var miner = CurrentMiner;
			if (miner == null || miner.IsDead || !miner.IsInWorld || miner.TraitOrDefault<AutoDeployedSlaveMiner>() == null)
				return;

			self.QueueActivity(new FindAndDeliverResources(self));
		}
	}

	public static class SlaveMinerDeploymentPolicy
	{
		const int PreferredResourceDistanceSquared = 9;
		const int InitialScanWindow = 10;

		public static CPos? ClosestCandidateAlongPath(
			IReadOnlyList<CPos> reversedResourcePath,
			IReadOnlySet<CPos> candidates)
		{
			foreach (var cell in reversedResourcePath)
				if (candidates.Contains(cell))
					return cell;

			return null;
		}

		public static bool ShouldDeployAtCurrentCell(
			bool canDeploy,
			bool isValidForReachedResource,
			int distanceSquared)
		{
			return canDeploy && isValidForReachedResource &&
				distanceSquared >= 0 && distanceSquared <= PreferredResourceDistanceSquared;
		}

		public static bool ShouldDeployAfterMove(bool atDestination, bool canDeploy, bool hasResource)
		{
			return atDestination && canDeploy && hasResource;
		}

		public static int InitialScanDelay(int searchInterval, uint actorId)
		{
			var window = Math.Max(1, Math.Min(InitialScanWindow, searchInterval));
			return 1 + (int)(actorId % (uint)window);
		}

		public static CPos? ResolveCandidate(
			IReadOnlyList<CPos> reversedResourcePath,
			IReadOnlySet<CPos> targetResourceCandidates,
			Func<IReadOnlyList<CPos>> findTargetResourceCandidatePath,
			Func<IReadOnlyList<CPos>> findGlobalCandidatePath,
			CPos? currentCellFallback)
		{
			var pathCandidate = ClosestCandidateAlongPath(reversedResourcePath, targetResourceCandidates);
			if (pathCandidate.HasValue)
				return pathCandidate;

			if (targetResourceCandidates.Count > 0)
			{
				var targetResourceCandidatePath = findTargetResourceCandidatePath();
				if (targetResourceCandidatePath.Count > 0)
					return targetResourceCandidatePath[0];
			}

			var globalCandidatePath = findGlobalCandidatePath();
			return globalCandidatePath.Count > 0 ? globalCandidatePath[0] : currentCellFallback;
		}
	}

	[Desc("Automatically moves a mobile Slave Miner to a reachable resource field and deploys it.")]
	public sealed class AutoSlaveMinerInfo : TraitInfo, Requires<MobileInfo>, Requires<TransformsInfo>
	{
		[Desc("Resource types that can trigger automatic deployment.")]
		public readonly HashSet<string> Resources = new() { "Ore", "Gems" };

		[Desc("Maximum distance in cells between the deployed miner and a resource cell.")]
		public readonly int WorkRadius = 8;

		[Desc("Maximum distance in cells searched from the mobile miner. Zero searches the complete map.")]
		public readonly int SearchRadius = 0;

		[Desc("Ticks between resource-field searches while idle.")]
		public readonly int SearchInterval = 125;

		public override object Create(ActorInitializer init) { return new AutoSlaveMiner(init, this); }
	}

	public sealed class AutoSlaveMiner : ITick, INotifyCreated, ISync
	{
		readonly AutoSlaveMinerInfo info;
		readonly ActorInfo deployedActorInfo;
		readonly BuildingInfo deployedBuildingInfo;
		Mobile mobile;
		Transforms transforms;
		IResourceLayer resourceLayer;

		[Sync]
		int scanTicks;

		public AutoSlaveMiner(ActorInitializer init, AutoSlaveMinerInfo info)
		{
			this.info = info;
			var transformsInfo = init.Self.Info.TraitInfo<TransformsInfo>();
			deployedActorInfo = init.World.Map.Rules.Actors[transformsInfo.IntoActor];
			deployedBuildingInfo = deployedActorInfo.TraitInfo<BuildingInfo>();
		}

		void INotifyCreated.Created(Actor self)
		{
			mobile = self.Trait<Mobile>();
			transforms = self.Trait<Transforms>();
			resourceLayer = self.World.WorldActor.Trait<IResourceLayer>();
			scanTicks = SlaveMinerDeploymentPolicy.InitialScanDelay(info.SearchInterval, self.ActorID);
		}

		void ITick.Tick(Actor self)
		{
			if (self.IsDead || !self.IsInWorld || !self.IsIdle)
				return;

			if (--scanTicks > 0)
				return;

			scanTicks = info.SearchInterval;

			var destination = FindDeploymentCell(self);
			if (!destination.HasValue)
				return;

			if (destination.Value == self.Location)
			{
				if (transforms.CanDeploy())
					self.QueueActivity(transforms.GetTransformActivity());

				return;
			}

			self.QueueActivity(mobile.MoveTo(destination.Value, 0));
			self.QueueActivity(new CallFunc(() =>
			{
				var atDestination = self.Location == destination.Value;
				var canDeploy = atDestination && transforms.CanDeploy();
				var hasResource = canDeploy && HasResourceNear(
					self.World, resourceLayer, destination.Value, info.Resources, info.WorkRadius);
				if (SlaveMinerDeploymentPolicy.ShouldDeployAfterMove(atDestination, canDeploy, hasResource))
					self.QueueActivity(transforms.GetTransformActivity());
			}));
		}

		CPos? FindDeploymentCell(Actor self)
		{
			var map = self.World.Map;
			var limitSearchRadius = info.SearchRadius > 0;
			var searchRadiusSquared = info.SearchRadius * info.SearchRadius;
			var resourceCells = new HashSet<CPos>();

			foreach (var resourceCell in map.AllCells)
			{
				var resource = resourceLayer.GetResource(resourceCell).Type;
				if (resource == null || !info.Resources.Contains(resource))
					continue;

				if (limitSearchRadius && (resourceCell - self.Location).LengthSquared > searchRadiusSquared)
					continue;

				resourceCells.Add(resourceCell);
			}

			if (resourceCells.Count == 0)
				return null;

			var candidateValidity = new Dictionary<CPos, bool>();
			var resourcePath = mobile.PathFinder.FindPathToTargetCellByPredicate(
				self,
				new[] { self.Location },
				resourceCells.Contains,
				BlockedByActor.Immovable,
				ignoreActor: self);

			var targetResourceCandidates = new HashSet<CPos>();
			CPos? currentCellFallback = null;
			if (resourcePath.Count > 0)
			{
				var targetResource = resourcePath[0];
				targetResourceCandidates = FindDeploymentCandidates(self, new[] { targetResource },
					candidateValidity, limitSearchRadius, searchRadiusSquared);
				var currentCellIsValid = targetResourceCandidates.Contains(self.Location);
				if (SlaveMinerDeploymentPolicy.ShouldDeployAtCurrentCell(
					transforms.CanDeploy(), currentCellIsValid, (self.Location - targetResource).LengthSquared))
					return self.Location;

				if (currentCellIsValid)
					currentCellFallback = self.Location;

				targetResourceCandidates.Remove(self.Location);
			}

			return SlaveMinerDeploymentPolicy.ResolveCandidate(
				resourcePath,
				targetResourceCandidates,
				() => mobile.PathFinder.FindPathToTargetCellByPredicate(
					self,
					new[] { self.Location },
					targetResourceCandidates.Contains,
					BlockedByActor.Stationary,
					ignoreActor: self),
				() => FindGlobalDeploymentPath(self, resourceCells, candidateValidity,
					limitSearchRadius, searchRadiusSquared),
				currentCellFallback);
		}

		IReadOnlyList<CPos> FindGlobalDeploymentPath(
			Actor self,
			IEnumerable<CPos> resourceCells,
			Dictionary<CPos, bool> candidateValidity,
			bool limitSearchRadius,
			int searchRadiusSquared)
		{
			// This path is only evaluated when the closest resource is blocked or has no
			// reachable 2x2 footprint. Validation results from the first field are reused.
			var allDeploymentCandidates = FindDeploymentCandidates(self, resourceCells,
				candidateValidity, limitSearchRadius, searchRadiusSquared);
			allDeploymentCandidates.Remove(self.Location);
			if (allDeploymentCandidates.Count == 0)
				return Array.Empty<CPos>();

			return mobile.PathFinder.FindPathToTargetCellByPredicate(
				self,
				new[] { self.Location },
				allDeploymentCandidates.Contains,
				BlockedByActor.Stationary,
				ignoreActor: self);
		}

		HashSet<CPos> FindDeploymentCandidates(
			Actor self,
			IEnumerable<CPos> resourceCells,
			Dictionary<CPos, bool> candidateValidity,
			bool limitSearchRadius,
			int searchRadiusSquared)
		{
			var candidates = new HashSet<CPos>();
			foreach (var resourceCell in resourceCells)
			{
				foreach (var candidate in self.World.Map.FindTilesInCircle(resourceCell, info.WorkRadius))
				{
					if (limitSearchRadius && (candidate - self.Location).LengthSquared > searchRadiusSquared)
						continue;

					if (!candidateValidity.TryGetValue(candidate, out var isValid))
					{
						isValid = mobile.CanEnterCell(candidate, self, BlockedByActor.Stationary) &&
							self.World.CanPlaceBuilding(
								candidate, deployedActorInfo, deployedBuildingInfo, self) &&
							(candidate != self.Location || transforms.CanDeploy());
						candidateValidity.Add(candidate, isValid);
					}

					if (isValid)
						candidates.Add(candidate);
				}
			}

			return candidates;
		}

		internal static bool HasResourceNear(
			World world,
			IResourceLayer resourceLayer,
			CPos center,
			IReadOnlySet<string> resources,
			int radius)
		{
			foreach (var cell in world.Map.FindTilesInCircle(center, radius))
			{
				var type = resourceLayer.GetResource(cell).Type;
				if (type != null && resources.Contains(type))
					return true;
			}

			return false;
		}
	}

	[Desc("Keeps a deployed Slave Miner working, and undeploys it after its field is exhausted.")]
	public sealed class AutoDeployedSlaveMinerInfo : TraitInfo, Requires<TransformsInfo>
	{
		[Desc("Resource types gathered by this miner's workers.")]
		public readonly HashSet<string> Resources = new() { "Ore", "Gems" };

		[Desc("Resource search radius used by deployed workers.")]
		public readonly int WorkRadius = 12;

		[Desc("Ticks between exhaustion checks.")]
		public readonly int ScanInterval = 25;

		[Desc("How long a field must remain empty before the miner migrates.")]
		public readonly int DepletedGracePeriod = 250;

		[Desc("Maximum time to wait for blocked workers to deliver before migrating anyway.")]
		public readonly int MaximumDeliveryWait = 750;

		public override object Create(ActorInitializer init) { return new AutoDeployedSlaveMiner(this); }
	}

	public sealed class AutoDeployedSlaveMiner : ITick, INotifyCreated, ISync
	{
		readonly AutoDeployedSlaveMinerInfo info;
		Transforms transforms;
		IResourceLayer resourceLayer;

		[Sync]
		int scanTicks;

		[Sync]
		int depletedTicks;

		public AutoDeployedSlaveMiner(AutoDeployedSlaveMinerInfo info)
		{
			this.info = info;
			scanTicks = info.ScanInterval;
		}

		void INotifyCreated.Created(Actor self)
		{
			transforms = self.Trait<Transforms>();
			resourceLayer = self.World.WorldActor.Trait<IResourceLayer>();
		}

		void ITick.Tick(Actor self)
		{
			if (self.IsDead || !self.IsInWorld || !self.IsIdle)
				return;

			if (--scanTicks > 0)
				return;

			scanTicks = info.ScanInterval;

			if (AutoSlaveMiner.HasResourceNear(self.World, resourceLayer, self.Location, info.Resources, info.WorkRadius))
			{
				depletedTicks = 0;
				return;
			}

			depletedTicks += info.ScanInterval;
			if (depletedTicks < info.DepletedGracePeriod)
				return;

			if (!WorkersHaveFinishedDelivering(self) && depletedTicks < info.MaximumDeliveryWait)
				return;

			depletedTicks = 0;
			if (transforms.CanDeploy())
				self.QueueActivity(transforms.GetTransformActivity());
		}

		static bool WorkersHaveFinishedDelivering(Actor self)
		{
			foreach (var worker in self.World.ActorsWithTrait<SlaveMinerWorker>())
			{
				if (worker.Actor.IsDead || !worker.Actor.IsInWorld || worker.Actor.Owner != self.Owner)
					continue;

				if (worker.Trait.CurrentMiner != self)
					continue;

				if (worker.Actor.TraitsImplementing<IStoresResources>().Any(store => store.ContentsSum > 0))
					return false;
			}

			return true;
		}
	}
}
