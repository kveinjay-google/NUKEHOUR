using System;
using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosProductFeaturePolicyTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "ios", "OpenRA.iOS"))))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
		}

		[Test]
		public void IosExcludesDesktopAuthoringTools()
		{
			Assert.That(IosProductFeaturePolicy.ShowMapEditor(true), Is.False);
			Assert.That(IosProductFeaturePolicy.ShowAssetBrowser(true), Is.False);
		}

		[Test]
		public void DesktopRetainsAuthoringTools()
		{
			Assert.That(IosProductFeaturePolicy.ShowMapEditor(false), Is.True);
			Assert.That(IosProductFeaturePolicy.ShowAssetBrowser(false), Is.True);
		}

		[Test]
		public void PublicCleanBundleIsExplicitAndFailClosed()
		{
			var repositoryRoot = RepositoryRoot();
			var project = File.ReadAllText(Path.Combine(
				repositoryRoot, "ios", "OpenRA.iOS", "OpenRA.iOS.csproj"));

			StringAssert.Contains("<PublicClean Condition=\"'$(PublicClean)' == ''\">false</PublicClean>", project);
			StringAssert.Contains("Condition=\"'$(PublicClean)' != 'true'\"", project,
				"Retail development resources must be excluded from PublicClean.");
			StringAssert.Contains("../../mods/ra2-content/**/*", project,
				"A clean install must include the code-only content installer.");
			StringAssert.Contains("Target Name=\"AuditPublicBundle\"", project);
			StringAssert.Contains("packaging/public_bundle_audit.py", project);
			StringAssert.Contains("Condition=\"'$(PublicClean)' == 'true'\"", project);

			var buildScriptPath = Path.Combine(repositoryRoot, "ios", "scripts", "build-public-clean.sh");
			Assert.That(File.Exists(buildScriptPath), Is.True,
				"Public builds must use a reproducible fail-closed entry point.");
			var buildScript = File.ReadAllText(buildScriptPath);
			StringAssert.Contains("-p:PublicClean=true", buildScript);
			StringAssert.Contains("-p:BaseOutputPath=", buildScript);
			StringAssert.Contains("--ipa", buildScript);
			StringAssert.Contains("shasum -a 256", buildScript);
		}
	}
}
