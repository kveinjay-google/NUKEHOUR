using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.RA2.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class DestructionAuditConfigurationTest
	{
		[Test]
		public void AuditIsDisabledUnlessExplicitlyEnabled()
		{
			var config = IosDestructionAuditConfiguration.Parse(_ => null);

			Assert.That(config.Enabled, Is.False);
		}

		[Test]
		public void ParsesBoundedIterationsAndSettlingTicks()
		{
			var values = new Dictionary<string, string>
			{
				["OPENRA_IOS_DESTRUCTION_AUDIT"] = "true",
				["OPENRA_IOS_DESTRUCTION_ITERATIONS"] = "3",
				["OPENRA_IOS_DESTRUCTION_SETTLE_TICKS"] = "40",
				["OPENRA_IOS_DESTRUCTION_BATCH_SIZE"] = "12",
				["OPENRA_IOS_DESTRUCTION_ACTOR_FILTER"] = "E1, gapowr"
			};

			var config = IosDestructionAuditConfiguration.Parse(
				name => values.TryGetValue(name, out var value) ? value : null);

			Assert.That(config.Enabled, Is.True);
			Assert.That(config.Iterations, Is.EqualTo(3));
			Assert.That(config.PreKillSettleTicks, Is.EqualTo(1));
			Assert.That(config.SettleTicks, Is.EqualTo(40));
			Assert.That(config.BatchSize, Is.EqualTo(12));
			Assert.That(config.ShouldIncludeActor("e1"), Is.True);
			Assert.That(config.ShouldIncludeActor("gapowr"), Is.True);
			Assert.That(config.ShouldIncludeActor("zep"), Is.False);
		}

		[Test]
		public void CasesAreStableAndCoverNormalAndExplosiveDeath()
		{
			var config = IosDestructionAuditConfiguration.CreateForTests(iterations: 2);

			var cases = config.BuildCases(new[] { "Zep", "e1", "E1", "gapowr" }).ToArray();

			Assert.That(cases.Select(c => c.ActorName).Distinct(),
				Is.EqualTo(new[] { "e1", "gapowr", "zep" }));
			Assert.That(cases.Where(c => c.ActorName == "e1").Select(c => c.DeathType),
				Is.EqualTo(new[] { "BulletDeath", "ExplosionDeath", "BulletDeath", "ExplosionDeath" }));
			Assert.That(cases.Select(c => c.Id).Distinct().Count(), Is.EqualTo(cases.Length));
		}

		[TestCase(WorldType.Regular, true)]
		[TestCase(WorldType.Shellmap, false)]
		[TestCase(WorldType.Editor, false)]
		public void AuditStartsOnlyInThePlayableWorld(WorldType worldType, bool expected)
		{
			Assert.That(IosDestructionAuditConfiguration.ShouldStartInWorld(worldType), Is.EqualTo(expected));
		}

		[TestCase("amcv", true)]
		[TestCase("gapowr", true)]
		[TestCase("amcv.colorpicker", false)]
		[TestCase("caairp.rubble", false)]
		[TestCase("jumpjet.husk", false)]
		public void AuditExcludesPreviewAndDerivedHelperActors(string actorName, bool expected)
		{
			Assert.That(IosDestructionAuditConfiguration.IsGameplayActorName(actorName), Is.EqualTo(expected));
		}

		[TestCase(true, false, true)]
		[TestCase(true, true, false)]
		[TestCase(false, false, false)]
		public void AirborneAircraftCanUseAnInMapFallbackCell(
			bool hasAircraft, bool hasBuilding, bool expected)
		{
			Assert.That(IosDestructionAuditConfiguration.CanUseAirborneFallbackCell(hasAircraft, hasBuilding),
				Is.EqualTo(expected));
		}

		[Test]
		public void AuditSpawnCellsMustAvoidEveryExistingActor()
		{
			Assert.That(IosDestructionAuditConfiguration.SpawnOccupancyCheck,
				Is.EqualTo(BlockedByActor.All));
		}

		[TestCase(0, 10000, 0)]
		[TestCase(1, 10000, 997)]
		[TestCase(2, 10000, 1994)]
		[TestCase(2, 1000, 994)]
		public void ConsecutiveCasesStartTheirTerrainSearchInSeparatedRegions(
			int caseIndex, int candidateCount, int expected)
		{
			Assert.That(IosDestructionAuditConfiguration.SpawnCandidateStartIndex(caseIndex, candidateCount),
				Is.EqualTo(expected));
		}

		[Test]
		public void IsometricBuildingsAreIncludedInTheAudit()
		{
			var building = new ActorInfo("test-building",
				new HealthInfo(), new BuildingInfo(), new IsometricSelectableInfo());

			Assert.That(IosDestructionAuditConfiguration.IsAuditable("test-building", building), Is.True);
		}

		[Test]
		public void BatchesContainAtMostTwentyDifferentActorsAndDoNotMixCategories()
		{
			var config = IosDestructionAuditConfiguration.CreateForTests(iterations: 1);
			var actors = Enumerable.Range(0, 41)
				.Select(i => new DestructionAuditActor($"unit-{i:D2}", DestructionAuditCategory.MobileUnit))
				.Append(new DestructionAuditActor("power-plant", DestructionAuditCategory.Building));

			var batches = config.BuildBatches(actors).ToArray();

			Assert.That(batches, Has.All.Matches<DestructionAuditBatch>(b => b.Cases.Count <= 20));
			Assert.That(batches, Has.All.Matches<DestructionAuditBatch>(b =>
				b.Cases.Select(c => c.ActorName).Distinct().Count() == b.Cases.Count));
			Assert.That(batches, Has.All.Matches<DestructionAuditBatch>(b =>
				b.Cases.All(c => c.Category == b.Category && c.DeathType == b.DeathType)));
			Assert.That(batches.Count(b => b.Category == DestructionAuditCategory.MobileUnit), Is.EqualTo(6));
			Assert.That(batches.Count(b => b.Category == DestructionAuditCategory.Building), Is.EqualTo(2));
		}

		[TestCase(false, false, false, DestructionAuditCategory.MobileUnit)]
		[TestCase(true, false, false, DestructionAuditCategory.Building)]
		[TestCase(true, true, false, DestructionAuditCategory.Defense)]
		[TestCase(true, true, true, DestructionAuditCategory.NavalBuilding)]
		public void AuditCategoriesSeparateBuildingsDefensesAndNavalStructures(
			bool building, bool attack, bool water, DestructionAuditCategory expected)
		{
			Assert.That(IosDestructionAuditConfiguration.Classify(building, attack, water), Is.EqualTo(expected));
		}
	}
}
