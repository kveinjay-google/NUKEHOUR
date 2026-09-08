using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RepairControlsPolicyTest
	{
		static string RepositoryRoot()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (root != null && (!Directory.Exists(Path.Combine(root, ".git")) ||
				!Directory.Exists(Path.Combine(root, "mods", "ra2"))))
				root = Directory.GetParent(root)?.FullName;

			Assert.That(root, Is.Not.Null, "Unable to locate the repository root.");
			return root!;
		}

		[Test]
		public void PlayerActorRegistersAutomaticRepairManager()
		{
			var yaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "player.yaml"));
			StringAssert.Contains("\tAutoRepairManager:", yaml);
		}

		[Test]
		public void InvalidManualRepairClickCancelsAndSelectsImmediately()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common",
				"Orders", "RepairOrderGenerator.cs"));
			StringAssert.Contains("world.CancelInputMode();", source);
			StringAssert.Contains("SelectionUtils.SelectActorsInBoxWithDeadzone", source);
			StringAssert.Contains("world.Selection.Combine", source);
		}

		[Test]
		public void ManualRepairModeUsesWrenchCursors()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common",
				"Orders", "RepairOrderGenerator.cs"));
			StringAssert.Contains("\"goldwrench\"", source);
			StringAssert.Contains("\"goldwrench-blocked\"", source);
		}

		[Test]
		public void InvalidSellClickCancelsAndSelectsImmediately()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common",
				"Orders", "GlobalButtonOrderGenerator.cs"));
			var sellGenerator = source[source.IndexOf("public class SellOrderGenerator", System.StringComparison.Ordinal)..];

			StringAssert.Contains("var orders = OrderInner(world, mi).ToArray();", sellGenerator);
			StringAssert.Contains("world.CancelInputMode();", sellGenerator);
			StringAssert.Contains("SelectionUtils.SelectActorsInBoxWithDeadzone", sellGenerator);
			StringAssert.Contains("world.Selection.Combine", sellGenerator);
		}
	}
}
