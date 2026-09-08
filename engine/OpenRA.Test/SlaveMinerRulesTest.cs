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

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OpenRA.Mods.RA2.Traits;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class SlaveMinerRulesTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null &&
				(!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "OpenRA.Mods.RA2"))))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		[Test]
		public void DeployedSlaveMinerProvidesTwoPassableFiveWorkerDocks()
		{
			var rules = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "yuri-structures.yaml"));
			var miner = rules.Substring(rules.IndexOf("yarefn:", System.StringComparison.Ordinal));
			miner = miner.Substring(0, miner.IndexOf("\nyaweap:", System.StringComparison.Ordinal));

			StringAssert.Contains("Footprint: x+ x+", miner);
			StringAssert.Contains("DockHost@SLAVE_NORTH:", miner);
			StringAssert.Contains("DockHost@SLAVE_EAST:", miner);
			Assert.That(miner.Split("MaxQueueLength: 5").Length - 1, Is.EqualTo(2));
		}

		[Test]
		public void DeployedSlaveMinerUsesDirectCreditSettlement()
		{
			var rules = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "yuri-structures.yaml"));
			var miner = rules.Substring(rules.IndexOf("yarefn:", System.StringComparison.Ordinal));
			miner = miner.Substring(0, miner.IndexOf("\nyaweap:", System.StringComparison.Ordinal));

			StringAssert.Contains("Refinery:\n\t\tShowTicks: True\n\t\tTickRate: 10\n\t\tUseStorage: false", miner);
		}

		[Test]
		public void SlaveMinerOnlySuppressesWorkersAfterFirstDeployment()
		{
			var mobileRules = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "yuri-vehicles.yaml"));
			var structureRules = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "yuri-structures.yaml"));
			var trait = File.ReadAllText(Path.Combine(RepositoryRoot(), "OpenRA.Mods.RA2", "Traits", "SuppressTargetFreeActorsOnTransform.cs"));

			StringAssert.Contains("SuppressTargetFreeActorsOnTransform:", mobileRules);
			StringAssert.Contains("MarkSlaveMinerAsDeployedOnTransform:", structureRules);
			StringAssert.Contains("class SlaveMinerRedeployInit", trait);
			StringAssert.Contains("ISingleInstanceInit", trait);
			StringAssert.Contains("GetValue<SlaveMinerRedeployInit, bool>(false)", trait);
			StringAssert.Contains("if (!previouslyDeployed)", trait);
			StringAssert.Contains("new SlaveMinerRedeployInit(true)", trait);
			StringAssert.Contains("new FreeActorInit(freeActor, false)", trait);
		}

		[Test]
		public void SlaveMinerAutonomyIsWiredAcrossMobileDeployedAndWorkerActors()
		{
			var mobileRules = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "yuri-vehicles.yaml"));
			var structureRules = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "yuri-structures.yaml"));
			var infantryRules = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "yuri-infantry.yaml"));

			StringAssert.Contains("AutoSlaveMiner:", mobileRules);
			StringAssert.Contains("SearchRadius: 0", mobileRules);
			StringAssert.Contains("AutoDeployedSlaveMiner:", structureRules);
			StringAssert.Contains("SlaveMinerWorker:", infantryRules);
			StringAssert.Contains("SearchOnCreation: true", infantryRules);
			StringAssert.Contains("WorkRadius: 12", structureRules);
			StringAssert.Contains("MaximumDeliveryWait: 750", structureRules);

			var deployedMiner = structureRules.Substring(structureRules.IndexOf("yarefn:", System.StringComparison.Ordinal));
			deployedMiner = deployedMiner.Substring(0, deployedMiner.IndexOf("\nyaweap:", System.StringComparison.Ordinal));
			StringAssert.DoesNotContain("global-factundeploy", deployedMiner);
			StringAssert.DoesNotContain("RequiresCondition: factundeploy", deployedMiner);
		}

		[Test]
		public void DeploymentChoosesTheLegalCellClosestToTheReachedResourceAlongThePath()
		{
			var resource = new CPos(20, 10);
			var preferred = new CPos(18, 10);
			var outer = new CPos(13, 10);
			var source = new CPos(10, 10);
			var reversedPath = new[] { resource, preferred, outer, source };
			var candidates = new HashSet<CPos>
			{
				preferred,
				outer
			};

			var destination = SlaveMinerDeploymentPolicy.ClosestCandidateAlongPath(reversedPath, candidates);

			Assert.That(destination, Is.EqualTo(preferred));
		}

		[Test]
		public void DeploymentPathSelectionReturnsNullWhenNoLegalCandidateIsOnTheResourcePath()
		{
			var resource = new CPos(20, 10);
			var offPath = new CPos(18, 12);
			var source = new CPos(10, 10);
			var candidates = new HashSet<CPos>
			{
				offPath
			};

			var destination = SlaveMinerDeploymentPolicy.ClosestCandidateAlongPath(
				new[] { resource, new CPos(19, 10), source }, candidates);

			Assert.That(destination, Is.Null);
		}

		[Test]
		public void DeploymentOnlyTreatsTheCurrentCellAsPreferredWhenItIsWithinThreeCellsOfOre()
		{
			Assert.That(SlaveMinerDeploymentPolicy.ShouldDeployAtCurrentCell(true, true, 9), Is.True);
			Assert.That(SlaveMinerDeploymentPolicy.ShouldDeployAtCurrentCell(true, true, 10), Is.False);
			Assert.That(SlaveMinerDeploymentPolicy.ShouldDeployAtCurrentCell(true, true, 64), Is.False);
			Assert.That(SlaveMinerDeploymentPolicy.ShouldDeployAtCurrentCell(true, false, 1), Is.False,
				"A nearby but unreachable resource must not make the miner deploy in place.");
			Assert.That(SlaveMinerDeploymentPolicy.ShouldDeployAtCurrentCell(false, true, 1), Is.False);
		}

		[Test]
		public void DeploymentFallsBackToAnotherReachableResourceField()
		{
			var resource = new CPos(20, 10);
			var source = new CPos(10, 10);
			var blockedCandidate = new CPos(19, 11);
			var reachableCandidate = new CPos(30, 10);
			var selectedSearches = 0;
			var globalSearches = 0;

			var destination = SlaveMinerDeploymentPolicy.ResolveCandidate(
				new[] { resource, new CPos(19, 10), source },
				new HashSet<CPos> { blockedCandidate },
				() =>
				{
					selectedSearches++;
					return Array.Empty<CPos>();
				},
				() =>
				{
					globalSearches++;
					return new[] { reachableCandidate, source };
				},
				null);

			Assert.That(destination, Is.EqualTo(reachableCandidate));
			Assert.That(selectedSearches, Is.EqualTo(1));
			Assert.That(globalSearches, Is.EqualTo(1));
		}

		[Test]
		public void DeploymentAfterMoveRequiresDestinationFootprintAndResource()
		{
			Assert.That(SlaveMinerDeploymentPolicy.ShouldDeployAfterMove(true, true, true), Is.True);
			Assert.That(SlaveMinerDeploymentPolicy.ShouldDeployAfterMove(false, true, true), Is.False);
			Assert.That(SlaveMinerDeploymentPolicy.ShouldDeployAfterMove(true, false, true), Is.False);
			Assert.That(SlaveMinerDeploymentPolicy.ShouldDeployAfterMove(true, true, false), Is.False);
		}

		[Test]
		public void InitialSlaveMinerScansAreDeterministicallyStaggered()
		{
			var delays = new HashSet<int>();
			for (uint actorId = 0; actorId < 10; actorId++)
				delays.Add(SlaveMinerDeploymentPolicy.InitialScanDelay(125, actorId));

			Assert.That(delays, Is.EquivalentTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
		}

		[Test]
		public void MobileMinerDoesNotDeployMerelyBecauseOreIsAtTheEdgeOfItsWorkRadius()
		{
			var automation = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "Traits", "SlaveMinerAutomation.cs"));

			StringAssert.DoesNotContain(
				"HasResourceNear(self.World, resourceLayer, self.Location, info.Resources, info.WorkRadius) && transforms.CanDeploy()",
				automation);
			Assert.That(automation.Split("FindPathToTargetCellByPredicate(").Length - 1, Is.EqualTo(3));
			StringAssert.Contains("if (resourceCells.Count == 0)", automation);
			StringAssert.Contains("resourceCells.Contains", automation);
			StringAssert.DoesNotContain("ResourceDistancePenalty", automation);
			StringAssert.Contains("self.QueueActivity(transforms.GetTransformActivity())", automation);
			StringAssert.Contains("self.Location == destination.Value", automation);
			StringAssert.Contains("SlaveMinerDeploymentPolicy.ShouldDeployAfterMove", automation);
		}

		[Test]
		public void MobileMinerCachesDeploymentValidationAndFallsBackAcrossResourceFields()
		{
			var automation = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "Traits", "SlaveMinerAutomation.cs"));

			StringAssert.Contains("var candidateValidity = new Dictionary<CPos, bool>()", automation);
			StringAssert.Contains("candidateValidity.TryGetValue", automation);
			StringAssert.Contains("FindDeploymentCandidates(self, new[] { targetResource }", automation);
			StringAssert.Contains("FindDeploymentCandidates(self, resourceCells", automation);
			StringAssert.Contains("allDeploymentCandidates.Contains", automation);
			StringAssert.Contains("BlockedByActor.Immovable", automation);
			StringAssert.Contains("SlaveMinerDeploymentPolicy.ResolveCandidate", automation);
			StringAssert.DoesNotContain("Dictionary<CPos, Dictionary<CPos, int>>", automation);
		}
	}
}
