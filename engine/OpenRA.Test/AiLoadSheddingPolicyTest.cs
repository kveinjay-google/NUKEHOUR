using System.IO;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class AiLoadSheddingPolicyTest
	{
		[TestCase(182, 0, 40, 40)]
		[TestCase(20, 25, 40, 15)]
		[TestCase(20, 40, 40, 0)]
		[TestCase(182, 0, 0, 182)]
		public void AttackForceTransferIsBounded(int available, int current, int maximum, int expected)
		{
			Assert.That(AiLoadSheddingPolicy.UnitsToTransfer(available, current, maximum), Is.EqualTo(expected));
		}

		[TestCase(119, 120, false)]
		[TestCase(120, 120, true)]
		[TestCase(182, 120, true)]
		[TestCase(182, 0, false)]
		public void ProductionStopsAtConfiguredTotalUnitLimit(int current, int maximum, bool expected)
		{
			Assert.That(AiLoadSheddingPolicy.HasReachedUnitLimit(current, maximum), Is.EqualTo(expected));
		}

		[Test]
		public void ExplosionWeaponsDoNotReferenceMissingAudioAssets()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (root != null && !Directory.Exists(Path.Combine(root, "mods", "ra2")))
				root = Directory.GetParent(root)?.FullName;

			Assert.That(root, Is.Not.Null, "Unable to locate the repository root.");
			var yaml = File.ReadAllText(Path.Combine(root!, "mods", "ra2", "weapons", "explosions.yaml"));
			StringAssert.DoesNotContain("expnew09.wav", yaml);
			StringAssert.DoesNotContain("expnew13.wav", yaml);
		}
	}
}
