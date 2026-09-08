using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class DestructionAuditScriptTest
	{
		[Test]
		public void IosScriptLaunchesPollsAndCopiesCompletedReport()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "ios")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null);
			var script = File.ReadAllText(Path.Combine(directory!.FullName, "ios", "scripts", "run-destruction-audit.sh"));

			StringAssert.Contains("OPENRA_IOS_DESTRUCTION_AUDIT", script);
			StringAssert.Contains("device process launch", script);
			StringAssert.Contains("summary.json", script);
			StringAssert.Contains("device copy from", script);
			StringAssert.Contains("test -s", script);
			StringAssert.Contains("\"skipped\": 0", script);
			StringAssert.Contains("OPENRA_IOS_DESTRUCTION_MAP:-south-pacific", script);
			StringAssert.Contains("OPENRA_IOS_DESTRUCTION_BATCH_SIZE", script);
			StringAssert.Contains("OPENRA_IOS_DESTRUCTION_ACTOR_FILTER", script);
			StringAssert.Contains("batch-start", script);
			StringAssert.Contains("refinement", script);
		}
	}
}
