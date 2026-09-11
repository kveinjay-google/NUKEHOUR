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
			StringAssert.DoesNotContain("http://", fluent.ToLowerInvariant());
			StringAssert.DoesNotContain("https://", fluent.ToLowerInvariant());
		}

		[Test]
		public void BothIosBundleModesIncludeTheOfflineInstaller()
		{
			var project = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "OpenRA.iOS.csproj"));
			Assert.That(project.Split("../../mods/ra2-content/**/*").Length - 1, Is.EqualTo(2));
		}

		[Test]
		public void MacOSDirectorySourceSupportsBaseAndOptionalPackagesOffline()
		{
			var contentRoot = Path.Combine(RepositoryRoot(), "mods", "ra2-content");
			var mod = File.ReadAllText(Path.Combine(contentRoot, "mod.yaml"));
			var sourcePath = Path.Combine(contentRoot, "installer", "macos-directory.yaml");

			Assert.That(File.Exists(sourcePath), Is.True);
			var source = File.ReadAllText(sourcePath);
			StringAssert.Contains("ra2content|installer/macos-directory.yaml", mod);
			StringAssert.Contains("Type: MacOSDirectory", source);
			StringAssert.Contains("RequiredFiles: ra2.mix, language.mix", source);
			StringAssert.Contains("ContentPackage@base:", source);
			StringAssert.Contains("ContentPackage@music:", source);
			StringAssert.Contains("ContentPackage@yuri:", source);
			StringAssert.Contains("ContentPackage@yuri-music:", source);
			Assert.That(mod.Split("Sources: macos-directory").Length - 1, Is.EqualTo(4));
			StringAssert.DoesNotContain("http://", source.ToLowerInvariant());
			StringAssert.DoesNotContain("https://", source.ToLowerInvariant());
		}

		[Test]
		public void MacOSContentManagerOffersDirectoryPickerAndAutomaticSearch()
		{
			var root = RepositoryRoot();
			var layout = File.ReadAllText(Path.Combine(root, "engine", "mods", "common-content", "content.yaml"));
			var contentLogic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic",
				"Installation", "ModContentLogic.cs"));
			var sourceLogic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic",
				"Installation", "InstallFromSourceLogic.cs"));
			var english = File.ReadAllText(Path.Combine(root, "engine", "mods", "common-content", "fluent", "chrome.ftl"));
			var chinese = File.ReadAllText(Path.Combine(root, "mods", "ra2-content", "fluent", "zh-CN", "chrome.ftl"));

			StringAssert.Contains("Button@CHOOSE_SOURCE_BUTTON:", layout);
			StringAssert.Contains("Button@CHECK_SOURCE_BUTTON:", layout);
			StringAssert.Contains("chooseDirectory", contentLogic);
			StringAssert.Contains("ChooseContentSource", sourceLogic);
			StringAssert.Contains("ShowBackOnly", sourceLogic);
			StringAssert.Contains("activeOperation", sourceLogic);
			StringAssert.Contains("button-content-panel-choose-source = Choose Installation Directory…", english);
			StringAssert.Contains("button-content-panel-auto-search = Automatic Search", english);
			StringAssert.Contains("label-select-install-directory", english);
			StringAssert.Contains("label-selected-directory-invalid", english);
			StringAssert.Contains("button-content-panel-choose-source = 选择安装目录…", chinese);
			StringAssert.Contains("button-content-panel-auto-search = 自动搜索", chinese);
			StringAssert.Contains("label-select-install-directory", chinese);
			StringAssert.Contains("label-selected-directory-invalid", chinese);
		}

		[Test]
		public void LauncherOwnedContentManagerReturnsToLauncherInsteadOfReloadingTheMod()
		{
			var root = RepositoryRoot();
			var game = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Game", "Game.cs"));
			var loader = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "FileSystem",
				"ContentInstallerFileSystemLoader.cs"));
			var logic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic",
				"Installation", "ModContentLogic.cs"));

			StringAssert.Contains("ContentManagerReturnToLauncher", game);
			StringAssert.Contains("ClearContentManagerReturnToLauncher", loader);
			StringAssert.Contains("ReturnFromContentManager", logic);
		}
	}
}
