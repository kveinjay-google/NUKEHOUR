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
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosContentImportContractTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "ios", "OpenRA.iOS")))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
		}

		static string IosFile(string name)
		{
			return File.ReadAllText(Path.Combine(RepositoryRoot(), "ios", "OpenRA.iOS", name));
		}

		[Test]
		public void PublicBuildChecksImportedContentBeforeStartingGame()
		{
			var project = IosFile("OpenRA.iOS.csproj");
			var appDelegate = IosFile("AppDelegate.cs");

			StringAssert.Contains("PUBLIC_CLEAN", project);
			StringAssert.Contains("#if PUBLIC_CLEAN", appDelegate);
			StringAssert.Contains("IosContentImportCoordinator", appDelegate);
			StringAssert.Contains("contentImportCoordinator.Start", appDelegate);
		}

		[Test]
		public void PickerBalancesSecurityScopedResourceAccess()
		{
			var picker = IosFile("IosRetailContentPicker.cs");

			StringAssert.Contains("UIDocumentPickerViewController", picker);
			StringAssert.Contains("StartAccessingSecurityScopedResource", picker);
			StringAssert.Contains("finally", picker);
			StringAssert.Contains("StopAccessingSecurityScopedResource", picker);
		}

		[Test]
		public void CoordinatorUsesTransactionalImporterAndTreatsCancelAsNavigation()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");

			StringAssert.Contains("RetailContentImporter", coordinator);
			StringAssert.Contains("RetailImportError.Cancelled", coordinator);
			StringAssert.Contains("NeedsBase", coordinator);
			StringAssert.Contains("NeedsYuriOptional", coordinator);
			StringAssert.Contains("StartGameOnce", coordinator);
		}

		[Test]
		public void WizardUsesOneAdaptiveScrollableImportEntryWithoutSliders()
		{
			var viewController = IosFile("ViewController.cs");

			StringAssert.Contains("选择正版游戏文件", viewController);
			StringAssert.Contains("查看许可证", viewController);
			StringAssert.Contains("重试", viewController);
			Assert.That(viewController.Split("Button(strings.ChooseFiles", StringSplitOptions.None).Length - 1,
				Is.EqualTo(1), "The wizard must expose one import entry instead of three duplicate pickers.");
			StringAssert.DoesNotContain("Button(strings.ImportBase", viewController);
			StringAssert.DoesNotContain("Button(strings.ImportYuri", viewController);
			StringAssert.Contains("UIScrollView", viewController,
				"The complete wizard must remain reachable on short landscape phones.");
			StringAssert.Contains("UIUserInterfaceIdiom.Pad", viewController,
				"The wizard must choose a dedicated iPad layout instead of scaling the phone layout.");
			StringAssert.DoesNotContain("UISlider", viewController);
			StringAssert.Contains("Landscape", viewController);
		}

		[Test]
		public void WizardLayoutUsesRuntimeFramesAfterLandscapeGeometrySettles()
		{
			var viewController = IosFile("ViewController.cs");

			StringAssert.Contains("sealed class IosContentWizardView", viewController);
			StringAssert.Contains("public override void LayoutSubviews()", viewController);
			StringAssert.Contains("rootScroll.ContentSize", viewController);
			StringAssert.Contains("contentWizard.Frame = WizardFrame()", viewController);
			StringAssert.DoesNotContain("columns.HeightAnchor.ConstraintEqualTo(scroll.FrameLayoutGuide.HeightAnchor", viewController);
		}

		[Test]
		public void LicenseUsesAnAdaptiveScrollableSurfaceInsteadOfANarrowSystemAlert()
		{
			var viewController = IosFile("ViewController.cs");
			var coordinator = IosFile("IosContentImportCoordinator.cs");

			StringAssert.Contains("sealed class IosLicenseViewController", viewController);
			StringAssert.Contains("UIUserInterfaceIdiom.Pad", viewController);
			StringAssert.Contains("licenseScroll.ContentSize", viewController);
			StringAssert.Contains("UIModalPresentationStyle.OverFullScreen", coordinator);
			StringAssert.DoesNotContain("UIAlertController.Create", coordinator);
		}

		[Test]
		public void StartupNoticeContainsApprovedBilingualDistributionWarnings()
		{
			var viewController = IosFile("ViewController.cs");

			StringAssert.Contains("EA has not endorsed and does not support this product.", viewController);
			StringAssert.Contains("free and open-source", viewController);
			StringAssert.Contains("legally purchased and owned copy", viewController);
			StringAssert.Contains("officially authorized NUKE HOUR edition", viewController);
			StringAssert.Contains("malicious code", viewController);
			StringAssert.Contains("EA 未认可且不支持本产品", viewController);
			StringAssert.Contains("免费开源", viewController);
			StringAssert.Contains("合法购买并拥有的正版游戏副本", viewController);
			StringAssert.Contains("官方授权版本", viewController);
			StringAssert.Contains("恶意代码", viewController);
		}

		[Test]
		public void StartupNoticeKeepsWebsiteAndBundleVersionVisibleOutsideScrollableCopy()
		{
			var viewController = IosFile("ViewController.cs");

			StringAssert.Contains("https://nukehour.com", viewController);
			StringAssert.Contains("UIApplication.SharedApplication.OpenUrl", viewController);
			StringAssert.Contains("NSBundle.MainBundle", viewController);
			StringAssert.Contains("new UIScrollView", viewController);
			StringAssert.Contains("noticeScroll.AddSubview(noticeBody)", viewController);
			StringAssert.Contains("noticeScroll.ContentSize", viewController);
			StringAssert.Contains("version.Frame", viewController);
			StringAssert.Contains("website.Frame", viewController);
			StringAssert.DoesNotContain("NUKE HOUR RA2", viewController);
		}

		[Test]
		public void ReadyContentRequiresAnExplicitEnterGameAction()
		{
			var viewController = IosFile("ViewController.cs");
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var readyBranchStart = coordinator.IndexOf(
				"Show(IosContentImportState.Ready, strings.ReadyMessage, status);", StringComparison.Ordinal);
			var readyBranchEnd = coordinator.IndexOf("void ChooseFiles", readyBranchStart, StringComparison.Ordinal);
			var readyBranch = coordinator.Substring(readyBranchStart, readyBranchEnd - readyBranchStart);

			StringAssert.Contains("model.State is IosContentImportState.NeedsYuriOptional or IosContentImportState.Ready", viewController);
			StringAssert.DoesNotContain("StartGameOnce", readyBranch);
		}

		[Test]
		public void GameLaunchWaitsForTheNativePresentationToFinish()
		{
			var viewController = IosFile("ViewController.cs");
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var handoffStart = viewController.IndexOf(
				"public void BeginGameLaunch(Action startGame)", StringComparison.Ordinal);
			var handoffEnd = viewController.IndexOf("static string BundleDisplayVersion", handoffStart,
				StringComparison.Ordinal);
			Assert.That(handoffStart, Is.GreaterThanOrEqualTo(0));
			Assert.That(handoffEnd, Is.GreaterThan(handoffStart));
			var handoff = viewController.Substring(handoffStart, handoffEnd - handoffStart);

			StringAssert.Contains("PresentedViewController", handoff);
			StringAssert.Contains("DismissViewController", handoff);
			StringAssert.Contains("contentWizard?.RemoveFromSuperview()", handoff);
			StringAssert.Contains("View.LayoutIfNeeded()", handoff);
			StringAssert.Contains("BeginInvokeOnMainThread(startGame)", handoff);
			Assert.That(handoff.IndexOf("contentWizard?.RemoveFromSuperview()", StringComparison.Ordinal),
				Is.LessThan(handoff.IndexOf("BeginInvokeOnMainThread(startGame)", StringComparison.Ordinal)));
			StringAssert.Contains("viewController.BeginGameLaunch(startGame)", coordinator);
		}

		[Test]
		public void StartupFailureRestoresAVisibleNativeSurface()
		{
			var viewController = IosFile("ViewController.cs");
			var appDelegate = IosFile("AppDelegate.cs");

			StringAssert.Contains("contentWizard?.RemoveFromSuperview()", viewController);
			StringAssert.Contains("status.Hidden = false", viewController);
			StringAssert.Contains("View.BringSubviewToFront(status)", viewController);
			StringAssert.Contains("Window?.MakeKeyAndVisible()", appDelegate);
		}

		[Test]
		public void StartupPreflightRunsBeforeTheEngine()
		{
			var appDelegate = IosFile("AppDelegate.cs");
			var preflight = IosFile("IosLaunchPreflight.cs");
			var check = appDelegate.IndexOf("IosLaunchPreflight.EnsureComplete", StringComparison.Ordinal);
			var run = appDelegate.IndexOf("Game.InitializeAndRun", StringComparison.Ordinal);

			Assert.That(check, Is.GreaterThanOrEqualTo(0));
			Assert.That(run, Is.GreaterThan(check));
			StringAssert.Contains("glsl/combined.vert", preflight);
			StringAssert.Contains("mods/ra2/fonts/NotoSansCJKsc-Regular.otf", preflight);
			StringAssert.Contains("mods/ra2/uibits/menubuttons.png", preflight);
			StringAssert.Contains("mods/ra2/uibits/menupanel.png", preflight);
			StringAssert.Contains("mods/ra2/uibits/NUCLEAR-CRISIS-BG-06.png", preflight);
		}

		[Test]
		public void PublicCleanStartupDoesNotRequireStrippedDeveloperModPackages()
		{
			var manifest = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "mod.yaml"));

			StringAssert.DoesNotContain("\t$ts: ts", manifest,
				"The removed asset browser must not keep TS as a mandatory runtime mod.");
			StringAssert.DoesNotContain("\tts|chrome/assetbrowser.yaml", manifest);
			StringAssert.Contains("\t\t~ra2|bits/cameos", manifest);
			StringAssert.Contains("\t\t~ra2|bits/structures", manifest);
			StringAssert.Contains("\t\t~ra2|bits/projectiles", manifest);
			StringAssert.Contains("\t~ra2|maps: System", manifest,
				"The source checkout may provide maps, but a resource-free IPA must not require them.");
		}

		[Test]
		public void PublicCleanCreatesItsShellSurfaceOutsideTheApplicationBundle()
		{
			var appDelegate = IosFile("AppDelegate.cs");
			var project = IosFile("OpenRA.iOS.csproj");

			StringAssert.Contains("RuntimeShellmapInstaller.EnsureInstalled(supportPath)", appDelegate);
			StringAssert.Contains("#if PUBLIC_CLEAN", appDelegate);
			StringAssert.DoesNotContain("../../mods/ra2/maps/", project,
				"No map payload may be restored to the public IPA to provide the native menu surface.");
			StringAssert.DoesNotContain("../../mods/ra2/bits/**/*", project,
				"Unreviewed RA2 binary art must remain user supplied at runtime.");
			StringAssert.Contains("../../mods/ra2/bits/animations/nc-impact-*.png", project,
				"The approved clean-room NUKE HOUR impact overlays must remain available.");
		}

		[Test]
		public void ReadyRetailContentCreatesAPlayableProjectOwnedMapOutsideTheBundle()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var project = IosFile("OpenRA.iOS.csproj");

			StringAssert.Contains("RuntimeShellmapInstaller.EnsurePlayableMapInstalled(supportPath)", coordinator);
			StringAssert.Contains("Path.Combine(supportPath, \"maps\", \"ra2\", \"nukehour-storage-v1\")", coordinator);
			StringAssert.DoesNotContain("../../mods/ra2/maps/", project,
				"The generated starter map must remain user data and must not enter the IPA.");
		}

		[Test]
		public void PublicCleanCursorsUseFramesFromTheImportedRetailCursorSheet()
		{
			var cursors = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "cursors.yaml"));

			StringAssert.DoesNotContain("undeploy.shp:", cursors,
				"PublicClean excludes mod-provided SHP artwork, so startup cursors must not require it.");
			StringAssert.DoesNotContain("assaultmove.shp:", cursors,
				"PublicClean excludes mod-provided SHP artwork, so startup cursors must not require it.");
			StringAssert.Contains("\t\tundeploy:", cursors);
			StringAssert.Contains("\t\tassaultmove:", cursors);
		}
	}
}
