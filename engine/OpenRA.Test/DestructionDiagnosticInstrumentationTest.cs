using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class DestructionDiagnosticInstrumentationTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !File.Exists(Path.Combine(
				directory.FullName, "engine", "OpenRA.Game", "Game.cs")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null);
			return directory!.FullName;
		}

		[Test]
		public void DeathChainRecordsTraitWeaponAnimationAndSpawnBoundaries()
		{
			var root = RepositoryRoot();
			var health = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Traits", "Health.cs"));
			var explosion = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Traits", "FireWarheadOnDeath.cs"));
			var spawn = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Traits", "SpawnActorOnDeath.cs"));
			var animation = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Traits", "Render", "WithDeathAnimation.cs"));
			var world = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Game", "World.cs"));

			StringAssert.Contains("Death.NotifyKilled", health);
			StringAssert.Contains("notify.GetType().Name", health);
			StringAssert.Contains("Death.ExplosionWeapon", explosion);
			StringAssert.Contains("weaponName", explosion);
			StringAssert.Contains("Death.SpawnActor", spawn);
			StringAssert.Contains("Info.Actor", spawn);
			StringAssert.Contains("Death.Animation", animation);
			StringAssert.Contains("sequence", animation);
			StringAssert.Contains("World.FrameEndTask", world);
		}

		[Test]
		public void SpawnedSurvivorsNudgeFromAnActorWithAValidPosition()
		{
			var root = RepositoryRoot();
			var survivors = File.ReadAllText(Path.Combine(root,
				"OpenRA.Mods.RA2", "Traits", "SpawnSurvivors.cs"));

			StringAssert.Contains("new Nudge(unit)", survivors);
			StringAssert.DoesNotContain("new Nudge(w.WorldActor)", survivors);
		}
	}
}
