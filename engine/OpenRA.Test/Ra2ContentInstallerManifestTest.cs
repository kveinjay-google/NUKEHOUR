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
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class Ra2ContentInstallerManifestTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2-content")))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
		}

		[Test]
		public void InstallerDefinesBaseAndYuriPackagesWithoutDownloads()
		{
			var root = RepositoryRoot();
			var contentRoot = Path.Combine(root, "mods", "ra2-content");
			var mod = File.ReadAllText(Path.Combine(contentRoot, "mod.yaml"));
			var installer = string.Join("\n", Directory.EnumerateFiles(
				Path.Combine(contentRoot, "installer"), "*.yaml").Select(File.ReadAllText));

			StringAssert.Contains("ContentPackage@base:", mod);
			StringAssert.Contains("ContentPackage@yuri:", mod);
			StringAssert.Contains("ra2md.mix", mod);
			StringAssert.Contains("langmd.mix", mod);
			StringAssert.DoesNotContain("http://", installer.ToLowerInvariant());
			StringAssert.DoesNotContain("https://", installer.ToLowerInvariant());
		}

		[Test]
		public void ChineseOfflineImportInstructionsAreBundled()
		{
			var path = Path.Combine(RepositoryRoot(), "mods", "ra2-content", "fluent", "zh-CN", "chrome.ftl");
			Assert.That(File.Exists(path), Is.True);
			var fluent = File.ReadAllText(path);
			StringAssert.Contains("自行导入", fluent);
			StringAssert.Contains("尤里的复仇", fluent);
			StringAssert.DoesNotContain("下载", fluent);
		}

		[Test]
		public void BothIosBundleModesIncludeTheOfflineInstaller()
		{
			var project = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "OpenRA.iOS.csproj"));
			Assert.That(project.Split("../../mods/ra2-content/**/*").Length - 1, Is.EqualTo(2));
		}
	}
}
