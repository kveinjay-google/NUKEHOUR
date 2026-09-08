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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OpenRA.Mods.RA2.Content;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class PublicBrandingPolicyTest
	{
		static string Root()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "ios", "OpenRA.iOS")))
				directory = directory.Parent;
			return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
		}

		[Test]
		public void ChangedRa2BrandResourcesMatchBothPrivateInventoryEntriesExactly()
		{
			var root = Root();
			using var manifest = JsonDocument.Parse(File.ReadAllText(
				Path.Combine(root, "packaging", "public-content-manifest.json")));
			using var sbom = JsonDocument.Parse(File.ReadAllText(
				Path.Combine(root, "docs", "legal", "public-asset-sbom.json")));
			foreach (var path in new[]
			{
				"mods/ra2/chrome/mainmenu.yaml",
				"mods/ra2/fluent/chrome.ftl",
				"mods/ra2/fluent/mod.ftl",
				"mods/ra2/fluent/zh-CN/chrome.ftl",
				"mods/ra2/fluent/zh-CN/mod.ftl",
				"mods/ra2/icon.png",
				"mods/ra2/mod.yaml"
			})
			{
				var source = File.ReadAllBytes(Path.Combine(root, path));
				var expectedHash = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
				var manifestEntry = InventoryEntry(manifest, path);
				var sbomEntry = InventoryEntry(sbom, path);

				foreach (var entry in new[] { manifestEntry, sbomEntry })
				{
					Assert.That(entry.GetProperty("sha256").GetString(), Is.EqualTo(expectedHash), path);
					Assert.That(entry.GetProperty("size").GetInt64(), Is.EqualTo(source.LongLength), path);
					Assert.That(entry.GetProperty("category").GetString(), Is.EqualTo("unknown-or-derivative"), path);
					Assert.That(entry.GetProperty("license").GetString(), Is.EqualTo("Unreviewed"), path);
					Assert.That(entry.GetProperty("public").GetBoolean(), Is.False, path);
				}

				Assert.That(sbomEntry.GetRawText(), Is.EqualTo(manifestEntry.GetRawText()), path);
			}
		}

		[Test]
		public void IosBuildModesUseUnifiedNukeHourIdentityAndStableIdentifiers()
		{
			var iosDirectory = Path.Combine(Root(), "ios", "OpenRA.iOS");
			var project = File.ReadAllText(Path.Combine(iosDirectory, "OpenRA.iOS.csproj"));
			Assert.That(Regex.Matches(project,
				@"<ApplicationTitle>NUKE HOUR</ApplicationTitle>").Count, Is.EqualTo(2));
			StringAssert.Contains("<IpaPackageName>NUKE HOUR.ipa</IpaPackageName>", project);
			StringAssert.Contains("<IpaPackageName>NUKE HOUR-PublicClean.ipa</IpaPackageName>", project);
			Assert.That(Regex.Matches(project,
				@"<ApplicationId>com\.openra\.ipad\.personal</ApplicationId>").Count, Is.EqualTo(1));
			StringAssert.DoesNotContain("NUCLEAR CRISIS", project);
			StringAssert.DoesNotContain("OpenRTS Mobile", project);

			var plist = File.ReadAllText(Path.Combine(iosDirectory, "Info.plist"));
			Assert.That(Regex.Matches(plist, @"<string>NUKE HOUR</string>").Count, Is.EqualTo(2));
			StringAssert.DoesNotContain("NUCLEAR CRISIS", plist);
			StringAssert.DoesNotContain("OpenRA iPad", plist);

			var viewController = File.ReadAllText(Path.Combine(iosDirectory, "ViewController.cs"));
			StringAssert.DoesNotContain("NUCLEAR CRISIS", viewController);
			StringAssert.DoesNotContain("OpenRA iPad", viewController);

			var publicBuild = File.ReadAllText(Path.Combine(Root(), "ios", "scripts", "build-public-clean.sh"));
			StringAssert.Contains("IPA=\"$ARTIFACT_DIR/NUKE HOUR-PublicClean.ipa\"", publicBuild);
			StringAssert.Contains("$ARTIFACT_DIR/NUKE HOUR-PublicClean.ipa.sha256", publicBuild);
			StringAssert.DoesNotContain("OpenRA-PublicClean.ipa", publicBuild);
		}

		[Test]
		public void PublicBuildUsesUnifiedNameAndIndependentIconCatalog()
		{
			var project = File.ReadAllText(Path.Combine(Root(), "ios", "OpenRA.iOS", "OpenRA.iOS.csproj"));
			Assert.That(Regex.Matches(project,
				@"<ApplicationTitle>NUKE HOUR</ApplicationTitle>").Count, Is.EqualTo(2));
			StringAssert.Contains("PublicAppIcon.appiconset", project);
			Assert.That(File.Exists(Path.Combine(Root(), "ios", "OpenRA.iOS", "Assets.xcassets",
				"PublicAppIcon.appiconset", "NUKE-HOUR-1024.png")), Is.True);
		}

		[Test]
		public void IosDeclaresExactEnglishAndSimplifiedChinesePermissionLocalizations()
		{
			var root = Root();
			var iosDirectory = Path.Combine(root, "ios", "OpenRA.iOS");
			var project = File.ReadAllText(Path.Combine(iosDirectory, "OpenRA.iOS.csproj"));
			StringAssert.Contains(
				"<BundleResource Include=\"en.lproj/InfoPlist.strings\" LogicalName=\"en.lproj/InfoPlist.strings\" Optimize=\"false\" />",
				project);
			StringAssert.Contains(
				"<BundleResource Include=\"zh-Hans.lproj/InfoPlist.strings\" LogicalName=\"zh-Hans.lproj/InfoPlist.strings\" Optimize=\"false\" />",
				project);
			Assert.That(Regex.Matches(project, @"InfoPlist\.strings").Count, Is.EqualTo(4),
				"The two exact resources each appear once as Include and once as LogicalName; no glob is allowed.");

			var plist = File.ReadAllText(Path.Combine(iosDirectory, "Info.plist"));
			StringAssert.Contains("<key>CFBundleDevelopmentRegion</key>\n  <string>en</string>", plist);
			StringAssert.Contains(
				"<key>CFBundleLocalizations</key>\n  <array>\n    <string>en</string>\n    <string>zh-Hans</string>\n  </array>",
				plist);
			StringAssert.Contains("用于发现并连接同一局域网、个人热点或附近设备上的多人游戏。", plist);

			var englishPath = Path.Combine(iosDirectory, "en.lproj", "InfoPlist.strings");
			var chinesePath = Path.Combine(iosDirectory, "zh-Hans.lproj", "InfoPlist.strings");
			Assert.That(File.ReadAllText(englishPath), Is.EqualTo(
				"\"NSLocalNetworkUsageDescription\" = \"Used to discover and connect to multiplayer games on the same local network, personal hotspot, or nearby devices.\";\n"));
			Assert.That(File.ReadAllText(chinesePath), Is.EqualTo(
				"\"NSLocalNetworkUsageDescription\" = \"用于发现并连接同一局域网、个人热点或附近设备上的多人游戏。\";\n"));

			using var manifest = JsonDocument.Parse(File.ReadAllText(
				Path.Combine(root, "packaging", "public-content-manifest.json")));
			using var sbom = JsonDocument.Parse(File.ReadAllText(
				Path.Combine(root, "docs", "legal", "public-asset-sbom.json")));
			foreach (var entry in new[]
			{
				(Source: "ios/OpenRA.iOS/en.lproj/InfoPlist.strings", Bundle: "en.lproj/InfoPlist.strings"),
				(Source: "ios/OpenRA.iOS/zh-Hans.lproj/InfoPlist.strings", Bundle: "zh-Hans.lproj/InfoPlist.strings"),
			})
			{
				var source = File.ReadAllBytes(Path.Combine(root, entry.Source));
				var expectedHash = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
				AssertPublicOriginalInventoryEntry(InventoryEntry(manifest, entry.Bundle), expectedHash,
					source.LongLength, entry.Bundle);
				AssertPublicOriginalInventoryEntry(InventoryEntry(sbom, entry.Source), expectedHash,
					source.LongLength, entry.Source);
			}
		}

		[Test]
		public void IosResolvesNativeLanguageBeforeConstructingAnyPreFluentUi()
		{
			var iosDirectory = Path.Combine(Root(), "ios", "OpenRA.iOS");
			var resolverPath = Path.Combine(iosDirectory, "IosNativeLanguage.cs");
			Assert.That(File.Exists(resolverPath), Is.True);
			var resolver = File.ReadAllText(resolverPath);
			StringAssert.Contains("MiniYaml.FromFile", resolver);
			StringAssert.Contains("NodeWithKeyOrDefault(\"Game\")", resolver);
			StringAssert.Contains("NodeWithKeyOrDefault(\"Language\")", resolver);
			StringAssert.Contains("LanguageSelectionPolicy.NormalizePreference", resolver);
			StringAssert.Contains("LanguageSelectionPolicy.Resolve", resolver);
			StringAssert.Contains("NSLocale.PreferredLanguages", resolver);
			StringAssert.Contains("preferredLanguages[0]", resolver);
			StringAssert.Contains("LanguageSelectionPolicy.SystemPreference", resolver);
			StringAssert.Contains("LanguageSelectionPolicy.English", resolver);
			StringAssert.DoesNotContain("new Settings(", resolver);

			var appDelegate = File.ReadAllText(Path.Combine(iosDirectory, "AppDelegate.cs"));
			var resolve = appDelegate.IndexOf("var nativeLanguage = IosNativeLanguage.Resolve(", StringComparison.Ordinal);
			var table = appDelegate.IndexOf("var nativeStrings = IosNativeStrings.ForLanguage(", StringComparison.Ordinal);
			var controller = appDelegate.IndexOf("new ViewController(nativeStrings)", StringComparison.Ordinal);
			Assert.That(resolve, Is.GreaterThanOrEqualTo(0));
			Assert.That(table, Is.GreaterThan(resolve));
			Assert.That(controller, Is.GreaterThan(table));
			StringAssert.Contains("Path.Combine(supportPath, \"settings.yaml\")", appDelegate);
			StringAssert.Contains("Engine.SystemLanguage={nativeLanguage.SystemLanguageTag}", appDelegate);
			StringAssert.DoesNotContain("Game.Language=", appDelegate);
			Assert.That(Regex.IsMatch(appDelegate,
				@"new IosContentImportCoordinator\([^;]*nativeStrings[^;]*\)", RegexOptions.Singleline), Is.True);
		}

		[Test]
		public void IosNativeLanguageFailsSafeForEverySupportedMalformedSettingsShape()
		{
			var duplicate = new MiniYaml(string.Empty, MiniYaml.FromString(
				"Game:\n\tLanguage: English\n\tLanguage: zh-CN\n", "duplicate-settings.yaml"));
			var game = duplicate.NodeWithKeyOrDefault("Game");
			Assert.Throws<InvalidDataException>(() => game.Value.NodeWithKeyOrDefault("Language"));
			Assert.Throws<YamlException>(() => MiniYaml.FromString(
				"Game:\n\t\tLanguage: English\n", "bad-indent-settings.yaml"));

			var resolver = File.ReadAllText(Path.Combine(
				Root(), "ios", "OpenRA.iOS", "IosNativeLanguage.cs"));
			StringAssert.Contains("e is InvalidDataException", resolver);
			StringAssert.Contains("e is IOException", resolver);
			StringAssert.Contains("e is UnauthorizedAccessException", resolver);
			StringAssert.Contains("e is YamlException", resolver);
			var readPreference = resolver.Substring(
				resolver.IndexOf("static string ReadPreference", StringComparison.Ordinal),
				resolver.IndexOf("static string PreferredSystemLanguage", StringComparison.Ordinal) -
				resolver.IndexOf("static string ReadPreference", StringComparison.Ordinal));
			StringAssert.DoesNotContain("catch (Exception e)\n", readPreference,
				"Only known non-fatal parsing and I/O failures may fall back to System.");
			var preferredSystemLanguage = resolver.Substring(
				resolver.IndexOf("static string PreferredSystemLanguage", StringComparison.Ordinal));
			StringAssert.Contains("preferredLanguages is { Length: > 0 }", preferredSystemLanguage);
			StringAssert.Contains("catch (Exception e)", preferredSystemLanguage,
				"Foundation locale detection failures must not prevent application startup.");
		}

		[Test]
		public void IosNativeCopyUsesOneInjectedTableAndHidesRawImporterDetails()
		{
			var iosDirectory = Path.Combine(Root(), "ios", "OpenRA.iOS");
			var viewController = File.ReadAllText(Path.Combine(iosDirectory, "ViewController.cs"));
			var coordinator = File.ReadAllText(Path.Combine(iosDirectory, "IosContentImportCoordinator.cs"));

			StringAssert.Contains("public sealed class IosNativeStrings", viewController);
			StringAssert.Contains("选择正版游戏文件", viewController);
			StringAssert.Contains("导入基础游戏", viewController);
			StringAssert.Contains("导入尤里的复仇", viewController);
			StringAssert.Contains("查看许可证", viewController);
			StringAssert.Contains("重试", viewController);
			StringAssert.Contains("Choose Game Files", viewController);
			StringAssert.Contains("Import Base Game", viewController);
			StringAssert.Contains("Import Yuri's Revenge", viewController);
			foreach (var error in Enum.GetNames<RetailImportError>())
				StringAssert.Contains($"RetailImportError.{error}", viewController, error);
			StringAssert.Contains("TitleLabel.Lines = 2", viewController);
			StringAssert.Contains("UILineBreakMode.WordWrap", viewController);
			StringAssert.Contains("StartupFailureMessage", viewController);
			StringAssert.Contains("status.Text = $\"{strings.StartupFailureTitle}\\n\\n{strings.StartupFailureMessage}\"", viewController);
			StringAssert.DoesNotContain("{message}", viewController,
				"Raw startup exceptions can contain paths and stack frames and are diagnostic-only.");

			StringAssert.Contains("readonly IosNativeStrings strings", coordinator);
			StringAssert.Contains("strings.ImportError(result.Error)", coordinator);
			StringAssert.Contains("Console.Error.WriteLine", coordinator);
			StringAssert.Contains("result.Message", coordinator);
			Assert.That(Regex.IsMatch(coordinator, @"Show\([^;]*result\.Message", RegexOptions.Singleline), Is.False,
				"Raw importer messages can contain paths and are diagnostic-only.");
			Assert.That(Regex.IsMatch(coordinator, @"[\u3400-\u9fff]"), Is.False,
				"All user-visible Chinese copy belongs to the selected immutable table in ViewController.cs.");
			StringAssert.DoesNotContain("OpenRTS Mobile", coordinator);
		}

		[Test]
		public void IosApplicationBrandAuditRunsBeforeExistingBundleAuditsAndCodesign()
		{
			var project = File.ReadAllText(Path.Combine(Root(), "ios", "OpenRA.iOS", "OpenRA.iOS.csproj"));
			Assert.That(Regex.IsMatch(project,
				@"<Target Name=""AuditIosApplicationBrand""[^>]*AfterTargets=""_CreateAppBundle""[^>]*BeforeTargets=""_CodesignAppBundle""",
				RegexOptions.Singleline), Is.True);
			Assert.That(Regex.IsMatch(project,
				@"<Target Name=""NormalizeIosAssetCatalogLogicalNames""[^>]*AfterTargets=""_CompileImageAssets""[^>]*BeforeTargets=""_ComputeBundleResourceOutputPaths""[^>]*Condition=""'\$\(UseArtifactsOutput\)' == 'true'""",
				RegexOptions.Singleline), Is.True,
				"External ArtifactsPath builds must normalize actool outputs before resource destinations are computed.");
			StringAssert.Contains("$([System.IO.Path]::GetFullPath('$(DeviceSpecificIntermediateOutputPath)actool/bundle'))/", project);
			StringAssert.Contains("%(_BundleResourceWithLogicalName.FullPath)", project);
			StringAssert.Contains("@(_IosActoolBundleResource->Count()) != 3", project);
			foreach (var output in new[] { "Assets.car", "AppIcon60x60@2x.png", "AppIcon76x76@2x~ipad.png" })
			{
				StringAssert.Contains(output, project);
				StringAssert.Contains($"ACTool output is missing or duplicated: {output}", project);
			}
			StringAssert.Contains("<LogicalName>%(_BundleResourceWithLogicalName.Filename)%(_BundleResourceWithLogicalName.Extension)</LogicalName>",
				project);
			StringAssert.Contains(
				"python3 &quot;$(MSBuildProjectDirectory)/../../packaging/ios_app_brand_audit.py&quot; &quot;$(AppBundleDir)&quot; --source-root &quot;$(MSBuildProjectDirectory)/../..&quot;",
				project);
			Assert.That(Regex.IsMatch(project,
				@"<Target Name=""AuditPersonalIosLoadingBrandBundle""[^>]*DependsOnTargets=""AuditIosApplicationBrand""",
				RegexOptions.Singleline), Is.True);
			Assert.That(Regex.IsMatch(project,
				@"<Target Name=""AuditPublicBundle""[^>]*DependsOnTargets=""AuditIosApplicationBrand""",
				RegexOptions.Singleline), Is.True);
		}

		[Test]
		public void NukeHourIconCoversEveryPhoneTabletAndStoreSlot()
		{
			var iconDirectory = Path.Combine(Root(), "ios", "OpenRA.iOS", "Assets.xcassets", "AppIcon.appiconset");
			using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(iconDirectory, "Contents.json")));
			var images = document.RootElement.GetProperty("images").EnumerateArray().ToArray();
			var expected = new Dictionary<(string Idiom, string Size, string Scale), int>
			{
				{ ("iphone", "20x20", "2x"), 40 },
				{ ("iphone", "20x20", "3x"), 60 },
				{ ("iphone", "29x29", "2x"), 58 },
				{ ("iphone", "29x29", "3x"), 87 },
				{ ("iphone", "40x40", "2x"), 80 },
				{ ("iphone", "40x40", "3x"), 120 },
				{ ("iphone", "60x60", "2x"), 120 },
				{ ("iphone", "60x60", "3x"), 180 },
				{ ("ipad", "20x20", "1x"), 20 },
				{ ("ipad", "20x20", "2x"), 40 },
				{ ("ipad", "29x29", "1x"), 29 },
				{ ("ipad", "29x29", "2x"), 58 },
				{ ("ipad", "40x40", "1x"), 40 },
				{ ("ipad", "40x40", "2x"), 80 },
				{ ("ipad", "76x76", "1x"), 76 },
				{ ("ipad", "76x76", "2x"), 152 },
				{ ("ipad", "83.5x83.5", "2x"), 167 },
				{ ("ios-marketing", "1024x1024", "1x"), 1024 },
			};

			var actualSlots = images.Select(image => (
				image.GetProperty("idiom").GetString()!,
				image.GetProperty("size").GetString()!,
				image.GetProperty("scale").GetString()!)).ToArray();
			Assert.That(actualSlots, Is.EquivalentTo(expected.Keys));

			foreach (var image in images)
			{
				var key = (
					image.GetProperty("idiom").GetString()!,
					image.GetProperty("size").GetString()!,
					image.GetProperty("scale").GetString()!);
				Assert.That(expected.TryGetValue(key, out var pixels), Is.True, $"Unexpected AppIcon slot {key}.");

				var filename = image.GetProperty("filename").GetString()!;
				StringAssert.StartsWith("NUKE-HOUR-", filename);
				var path = Path.Combine(iconDirectory, filename);
				Assert.That(File.Exists(path), Is.True, $"Missing AppIcon image {filename}.");
				Assert.That(ReadPngDimensions(path), Is.EqualTo((pixels, pixels)), filename);
				Assert.That(PngHasAlpha(path), Is.False, $"AppIcon {filename} must not contain an alpha channel.");
			}
		}

		[Test]
		public void IosBuildDeclaresDenyByDefaultIconCatalogIsolation()
		{
			var iosDirectory = Path.Combine(Root(), "ios", "OpenRA.iOS");
			var plist = File.ReadAllText(Path.Combine(iosDirectory, "Info.plist"));
			StringAssert.Contains("<key>XSAppIconAssets</key>", plist);
			StringAssert.Contains("<string>Assets.xcassets/AppIcon.appiconset</string>", plist);

			var project = File.ReadAllText(Path.Combine(iosDirectory, "OpenRA.iOS.csproj"));
			StringAssert.Contains("<ImageAsset Remove=\"Assets.xcassets/**/*\" />", project);
			StringAssert.Contains("<ImageAsset Include=\"Assets.xcassets/Contents.json\" />", project);
			StringAssert.Contains(
				"<ImageAsset Include=\"Assets.xcassets/LaunchBackground.colorset/Contents.json\" />",
				project);
			StringAssert.Contains(
				"<ImageAsset Include=\"Assets.xcassets/AppIcon.appiconset/*.png\" Condition=\"'$(PublicClean)' != 'true'\" />",
				project);
			StringAssert.Contains(
				"<ImageAsset Include=\"Assets.xcassets/PublicAppIcon.appiconset/*.png\" Condition=\"'$(PublicClean)' == 'true'\">",
				project);
		}

		[Test]
		public void PersonalAndPublicIconCatalogsRemainSeparateButByteIdentical()
		{
			var assetDirectory = Path.Combine(Root(), "ios", "OpenRA.iOS", "Assets.xcassets");
			var personalDirectory = Path.Combine(assetDirectory, "AppIcon.appiconset");
			var publicDirectory = Path.Combine(assetDirectory, "PublicAppIcon.appiconset");
			Assert.That(File.ReadAllBytes(Path.Combine(personalDirectory, "Contents.json")),
				Is.EqualTo(File.ReadAllBytes(Path.Combine(publicDirectory, "Contents.json"))));

			var filenames = Directory.GetFiles(personalDirectory, "*.png")
				.Select(Path.GetFileName).OrderBy(filename => filename).ToArray();
			Assert.That(filenames, Has.Length.EqualTo(15));
			Assert.That(filenames, Has.All.StartsWith("NUKE-HOUR-"));
			Assert.That(Directory.GetFiles(publicDirectory, "*.png")
				.Select(Path.GetFileName).OrderBy(filename => filename), Is.EqualTo(filenames));
			foreach (var filename in filenames)
				Assert.That(File.ReadAllBytes(Path.Combine(personalDirectory, filename!)),
					Is.EqualTo(File.ReadAllBytes(Path.Combine(publicDirectory, filename!))), filename);

			Assert.That(Directory.GetFiles(personalDirectory, "NUCLEAR-CRISIS-*.png"), Is.Empty);
			Assert.That(Directory.GetFiles(publicDirectory, "AppIcon-*.png"), Is.Empty);
		}

		[Test]
		public void Ra2SettingsAndMenuUseNukeHourBrandingWithoutRepeatingTheProductNameInSettings()
		{
			var fluentDirectory = Path.Combine(Root(), "mods", "ra2", "fluent");
			var englishChrome = File.ReadAllText(Path.Combine(fluentDirectory, "chrome.ftl"));
			var chineseChrome = File.ReadAllText(Path.Combine(fluentDirectory, "zh-CN", "chrome.ftl"));
			var englishMod = File.ReadAllText(Path.Combine(fluentDirectory, "mod.ftl"));
			var chineseMod = File.ReadAllText(Path.Combine(fluentDirectory, "zh-CN", "mod.ftl"));

			Assert.That(Regex.Matches(englishChrome,
				@"(?m)^button-settings-title = GAME SETTINGS\r?$").Count, Is.EqualTo(1));
			Assert.That(Regex.Matches(chineseChrome,
				@"(?m)^button-settings-title = 游戏设置\r?$").Count, Is.EqualTo(1));
			StringAssert.DoesNotContain("NUKE HOUR SETTINGS", englishChrome);
			StringAssert.DoesNotContain("NUKE HOUR 设置", chineseChrome);
			Assert.That(Regex.Matches(englishMod, @"(?m)^mod-title = NUKE HOUR\r?$").Count, Is.EqualTo(1));
			Assert.That(Regex.Matches(englishMod, @"(?m)^mod-windowtitle = NUKE HOUR\r?$").Count, Is.EqualTo(1));
			Assert.That(Regex.Matches(chineseMod, @"(?m)^mod-title = NUKE HOUR\r?$").Count, Is.EqualTo(1));
			Assert.That(Regex.Matches(chineseMod, @"(?m)^mod-windowtitle = NUKE HOUR\r?$").Count, Is.EqualTo(1));
			StringAssert.Contains("label-menu-subtitle = NUKE HOUR", englishChrome);
			StringAssert.Contains("label-menu-subtitle = NUKE HOUR", chineseChrome);
			StringAssert.Contains("label-mainmenu-prerelease-notification-prompt-title = NUKE HOUR developer preview",
				englishChrome);
			StringAssert.Contains("label-mainmenu-prerelease-notification-prompt-text-a = This pre-alpha build of NUKE HOUR",
				englishChrome);
			StringAssert.Contains("label-mainmenu-prerelease-notification-prompt-title = NUKE HOUR 开发者预览版", chineseChrome);
			StringAssert.Contains("label-mainmenu-prerelease-notification-prompt-text-a = 这是 NUKE HOUR 的 pre-alpha 预览构建，",
				chineseChrome);
			StringAssert.DoesNotContain("OpenRA", englishChrome);
			StringAssert.DoesNotContain("OpenRA", chineseChrome);
		}

		[Test]
		public void ActiveRootModLoadScreenAndLegalCopyUseNukeHour()
		{
			var root = Root();
			var readme = File.ReadAllText(Path.Combine(root, "README.md"));
			Assert.That(readme.Split('\n')[0].TrimEnd('\r'), Is.EqualTo("# NUKE HOUR"));

			var modManifest = File.ReadAllText(Path.Combine(root, "mods", "ra2", "mod.yaml"));
			Assert.That(Regex.Matches(modManifest, @"(?m)^\tModTabTitle: NUKE HOUR\r?$").Count, Is.EqualTo(1));

			var loadScreen = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "LoadScreens",
				"BrandSplashLoadScreen.cs"));
			StringAssert.Contains("const string BrandTitle = \"NUKE HOUR\";", loadScreen);

			var publicLegal = File.ReadAllText(Path.Combine(root, "mods", "ra2-public", "fluent", "legal.ftl"));
			var publicLegalChinese = File.ReadAllText(Path.Combine(root, "mods", "ra2-public", "fluent", "zh-CN",
				"legal.ftl"));
			StringAssert.Contains("public-legal-title = NUKE HOUR — Legal and content notice", publicLegal);
			StringAssert.Contains("public-legal-disclaimer = NUKE HOUR is an independent open-source project.", publicLegal);
			StringAssert.Contains("public-legal-title = NUKE HOUR — 许可证与资源说明", publicLegalChinese);
			StringAssert.Contains("public-legal-disclaimer = NUKE HOUR 是独立的开源项目", publicLegalChinese);

			var publicDistribution = File.ReadAllText(Path.Combine(root, "docs", "legal", "PUBLIC_DISTRIBUTION.md"));
			var thirdPartyContent = File.ReadAllText(Path.Combine(root, "docs", "legal", "THIRD_PARTY_CONTENT.md"));
			StringAssert.Contains("named **NUKE HOUR**", publicDistribution);
			StringAssert.Contains("NUKE HOUR is an independent open-source project.", publicDistribution);
			StringAssert.Contains("NUKE HOUR is an open-source engine shell.", thirdPartyContent);
		}

		[Test]
		public void TaskFourProductionSourcesContainNoStaleVisibleProductName()
		{
			var root = Root();
			foreach (var path in new[]
			{
				"README.md",
				"mod.config",
				"mods/ra2/mod.yaml",
				"mods/ra2/fluent/chrome.ftl",
				"mods/ra2/fluent/mod.ftl",
				"mods/ra2/fluent/zh-CN/chrome.ftl",
				"mods/ra2/fluent/zh-CN/mod.ftl",
				"OpenRA.Mods.RA2/LoadScreens/BrandSplashLoadScreen.cs",
				"mods/ra2-public/fluent/legal.ftl",
				"mods/ra2-public/fluent/zh-CN/legal.ftl",
				"docs/legal/PUBLIC_DISTRIBUTION.md",
				"docs/legal/THIRD_PARTY_CONTENT.md"
			})
			{
				var source = File.ReadAllText(Path.Combine(root, path));
				var withoutInternalWallpaperNames = Regex.Replace(source,
					@"NUCLEAR-CRISIS-BG-\d{2}\.png", "", RegexOptions.IgnoreCase);
				Assert.That(Regex.IsMatch(withoutInternalWallpaperNames,
					"NUCLEAR(?:-| )?CRISIS|OpenRTS Mobile", RegexOptions.IgnoreCase), Is.False, path);
			}
		}

		[Test]
		public void MainMenuIncludesEveryNuclearCrisisWallpaper()
		{
			var ra2Directory = Path.Combine(Root(), "mods", "ra2");
			var mainMenu = File.ReadAllText(Path.Combine(ra2Directory, "chrome", "mainmenu.yaml"));
			var chrome = File.ReadAllText(Path.Combine(ra2Directory, "chrome.yaml"));
			var expected = Enumerable.Range(1, 9).Select(index => $"NUCLEAR-CRISIS-BG-{index:00}.png").ToArray();

			foreach (var filename in expected)
			{
				StringAssert.Contains(filename, chrome);
				var path = Path.Combine(ra2Directory, "uibits", filename);
				Assert.That(File.Exists(path), Is.True, $"Missing menu wallpaper {filename}.");
				var dimensions = ReadPngDimensions(path);
				Assert.That(dimensions.Width, Is.GreaterThan(dimensions.Height), $"{filename} must be landscape.");
			}

			foreach (var index in Enumerable.Range(5, 9))
				StringAssert.Contains($"menu-bg-{index}", mainMenu);
		}

		[Test]
		public void IngameMenuUsesRa2LayoutWithoutTheRemovedLegacyLogo()
		{
			var ra2Directory = Path.Combine(Root(), "mods", "ra2");
			var manifest = File.ReadAllText(Path.Combine(ra2Directory, "mod.yaml"));
			var menu = File.ReadAllText(Path.Combine(ra2Directory, "chrome", "ingame-menu.yaml"));
			var info = File.ReadAllText(Path.Combine(ra2Directory, "chrome", "ingame-info.yaml"));

			StringAssert.Contains("\tra2|chrome/ingame-menu.yaml", manifest);
			StringAssert.Contains("\tra2|chrome/ingame-info.yaml", manifest);
			StringAssert.DoesNotContain("\tcommon|chrome/ingame-menu.yaml", manifest);
			StringAssert.DoesNotContain("\tcommon|chrome/ingame-info.yaml", manifest);

			StringAssert.Contains("StretchBackground@MENU_BG", menu);
			StringAssert.Contains("Container@PANEL_ROOT", menu);
			StringAssert.Contains("Container@GAME_INFO_PANEL", info);
			StringAssert.DoesNotContain("ImageCollection: logos", menu,
				"The legacy logos collection was intentionally removed and cannot be drawn by the in-game menu.");
		}

		[Test]
		public void GameSaveLoadingScreenUsesRa2LayoutWithoutTheRemovedLegacyLogo()
		{
			var ra2Directory = Path.Combine(Root(), "mods", "ra2");
			var manifest = File.ReadAllText(Path.Combine(ra2Directory, "mod.yaml"));

			StringAssert.Contains("\tra2|chrome/gamesave-loading.yaml", manifest);
			StringAssert.DoesNotContain("\tcommon|chrome/gamesave-loading.yaml", manifest);

			var loading = File.ReadAllText(Path.Combine(ra2Directory, "chrome", "gamesave-loading.yaml"));
			StringAssert.Contains("Container@GAMESAVE_LOADING_SCREEN", loading);
			StringAssert.Contains("ProgressBar@PROGRESS", loading);
			StringAssert.Contains("LogicKeyListener@CANCEL_HANDLER", loading);
			StringAssert.DoesNotContain("ImageCollection: logos", loading,
				"The save-game loading screen cannot draw the removed legacy logos collection.");
		}

		[Test]
		public void DirectlyUploadedNuclearCrisisWallpapersUsePowerOfTwoSheets()
		{
			var wallpaperDirectory = Path.Combine(Root(), "mods", "ra2", "uibits");
			foreach (var index in Enumerable.Range(1, 9))
			{
				var filename = $"NUCLEAR-CRISIS-BG-{index:00}.png";
				var dimensions = ReadPngDimensions(Path.Combine(wallpaperDirectory, filename));
				Assert.That(IsPowerOfTwo(dimensions.Width), Is.True,
					$"{filename} width {dimensions.Width} cannot be uploaded as an OpenRA texture.");
				Assert.That(IsPowerOfTwo(dimensions.Height), Is.True,
					$"{filename} height {dimensions.Height} cannot be uploaded as an OpenRA texture.");
			}
		}

		[Test]
		public void MainMenuAndLoadScreenWallpaperConfigReferencesFullSafeSheets()
		{
			var ra2Directory = Path.Combine(Root(), "mods", "ra2");
			var mainMenu = File.ReadAllText(Path.Combine(ra2Directory, "chrome", "mainmenu.yaml"));
			var chrome = File.ReadAllText(Path.Combine(ra2Directory, "chrome.yaml"));
			var modYaml = File.ReadAllText(Path.Combine(ra2Directory, "mod.yaml"));

			foreach (var index in Enumerable.Range(1, 9))
			{
				var filename = $"NUCLEAR-CRISIS-BG-{index:00}.png";
				var collection = $"menu-bg-{index + 4}";
				StringAssert.Contains(collection, mainMenu, $"Main menu must reference {collection}.");

				var block = Regex.Match(chrome,
					$@"(?m)^{Regex.Escape(collection)}:\r?\n[ \t]+Image: {Regex.Escape(filename)}\r?\n[ \t]+PanelRegion: 0, 0, 0, 0, (?<width>\d+), (?<height>\d+), 0, 0$");
				Assert.That(block.Success, Is.True, $"Missing full-sheet chrome region for {filename}.");

				var dimensions = ReadPngDimensions(Path.Combine(ra2Directory, "uibits", filename));
				var region = (Width: int.Parse(block.Groups["width"].Value, CultureInfo.InvariantCulture),
					Height: int.Parse(block.Groups["height"].Value, CultureInfo.InvariantCulture));
				Assert.That(region, Is.EqualTo(dimensions), $"{collection} must crop only inside {filename}.");
				Assert.That(region, Is.EqualTo((2048, 1024)), $"{filename} must use the safe 2048x1024 wallpaper sheet.");
			}

			var loadImage = Regex.Match(modYaml,
				@"(?m)^[ \t]+Image: ra2\|uibits\/(?<filename>NUCLEAR-CRISIS-BG-\d{2}\.png)$");
			Assert.That(loadImage.Success, Is.True, "LoadScreen must reference a NUCLEAR CRISIS wallpaper asset.");
			var loadPath = Path.Combine(ra2Directory, "uibits", loadImage.Groups["filename"].Value);
			Assert.That(File.Exists(loadPath), Is.True, "LoadScreen wallpaper asset is missing.");
			Assert.That(ReadPngDimensions(loadPath), Is.EqualTo((2048, 1024)),
				"LoadScreen must upload the same safe power-of-two wallpaper format.");
		}

		[TestCase(1916, 821, 844, 390)]
		[TestCase(1916, 821, 1180, 820)]
		[TestCase(1672, 941, 1366, 1024)]
		[TestCase(1916, 821, 2360, 1640)]
		[TestCase(1916, 821, 2732, 2048)]
		[TestCase(1916, 821, 2778, 1284)]
		public void MenuWallpaperUsesCenteredAspectFill(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
		{
			var source = new Rectangle(0, 0, sourceWidth, sourceHeight);
			var crop = StretchBackgroundWidget.CalculateAspectFillCrop(source, new Size(targetWidth, targetHeight));

			Assert.That(source.Contains(crop), Is.True);
			Assert.That(Math.Abs((double)crop.Width / crop.Height - (double)targetWidth / targetHeight), Is.LessThan(0.002));
			Assert.That(Math.Abs((crop.Left + crop.Right) - sourceWidth), Is.LessThanOrEqualTo(1));
			Assert.That(Math.Abs((crop.Top + crop.Bottom) - sourceHeight), Is.LessThanOrEqualTo(1));
		}

		[Test]
		public void LegalNoticeContainsLicenseSourceAndExactDisclaimer()
		{
			var legal = File.ReadAllText(Path.Combine(Root(), "docs", "legal", "PUBLIC_DISTRIBUTION.md"));
			StringAssert.Contains("GNU General Public License version 3", legal);
			StringAssert.Contains("ios/scripts/build-public-clean.sh", legal);
			StringAssert.Contains("not affiliated with, endorsed by, or sponsored by Electronic Arts", legal);
			StringAssert.Contains("No Electronic Arts game assets are included", legal);
			StringAssert.Contains("legally owned copy", legal);
		}

		static (int Width, int Height) ReadPngDimensions(string path)
		{
			var header = File.ReadAllBytes(path);
			Assert.That(header.Length, Is.GreaterThanOrEqualTo(26), path);
			Assert.That(header.Take(8), Is.EqualTo(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), path);
			return (ReadBigEndianInt32(header, 16), ReadBigEndianInt32(header, 20));
		}

		static JsonElement InventoryEntry(JsonDocument document, string path)
		{
			var entries = document.RootElement.GetProperty("files").EnumerateArray()
				.Where(entry => entry.GetProperty("path").GetString() == path).ToArray();
			Assert.That(entries, Has.Length.EqualTo(1), path);
			return entries[0];
		}

		static void AssertPublicOriginalInventoryEntry(
			JsonElement entry, string expectedHash, long expectedSize, string path)
		{
			Assert.That(entry.GetProperty("sha256").GetString(), Is.EqualTo(expectedHash), path);
			Assert.That(entry.GetProperty("size").GetInt64(), Is.EqualTo(expectedSize), path);
			Assert.That(entry.GetProperty("category").GetString(), Is.EqualTo("project-original"), path);
			Assert.That(entry.GetProperty("license").GetString(), Is.EqualTo("Project original"), path);
			Assert.That(entry.GetProperty("public").GetBoolean(), Is.True, path);
		}

		static bool PngHasAlpha(string path)
		{
			var data = File.ReadAllBytes(path);
			var colorType = data[25];
			if (colorType == 4 || colorType == 6)
				return true;

			for (var offset = 8; offset + 12 <= data.Length;)
			{
				var length = ReadBigEndianInt32(data, offset);
				if (length < 0 || offset + 12 + length > data.Length)
					throw new InvalidDataException($"Invalid PNG chunk in {path}.");

				if (data[offset + 4] == (byte)'t' && data[offset + 5] == (byte)'R' &&
					data[offset + 6] == (byte)'N' && data[offset + 7] == (byte)'S')
					return true;

				offset += 12 + length;
			}

			return false;
		}

		static int ReadBigEndianInt32(byte[] data, int offset)
		{
			return data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3];
		}

		static bool IsPowerOfTwo(int value)
		{
			return value > 0 && (value & (value - 1)) == 0;
		}
	}
}
