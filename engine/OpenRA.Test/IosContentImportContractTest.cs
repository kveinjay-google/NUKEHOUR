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
		public void PrivateDeviceBuildAlsoChecksImportedContentBeforeStartingGame()
		{
			var appDelegate = IosFile("AppDelegate.cs");

			StringAssert.DoesNotContain("#if PUBLIC_CLEAN\n\t\t\tcontentImportCoordinator", appDelegate);
			StringAssert.Contains("contentImportCoordinator = new IosContentImportCoordinator(", appDelegate);
			StringAssert.Contains("contentImportCoordinator.Start();", appDelegate);
		}

		[Test]
		public void PickerBalancesSecurityScopedResourceAccess()
		{
			var picker = IosFile("IosRetailContentPicker.cs");

			StringAssert.Contains("UIDocumentPickerViewController", picker);
			StringAssert.Contains("UTTypes.Folder.Identifier", picker);
			StringAssert.Contains("UIDocumentPickerMode.Open", picker);
			StringAssert.Contains("AllowsMultipleSelection = false", picker);
			StringAssert.Contains("StartAccessingSecurityScopedResource", picker);
			StringAssert.Contains("finally", picker);
			StringAssert.Contains("StopAccessingSecurityScopedResource", picker);
		}

		[Test]
		public void PickerContainsAsyncDelegateFailuresAndOnlyClearsTheCompletedDelegate()
		{
			var picker = IosFile("IosRetailContentPicker.cs");

			StringAssert.Contains("catch (Exception e)", picker);
			StringAssert.Contains("failed(e);", picker);
			StringAssert.Contains("ClearDelegate", picker);
			StringAssert.Contains("ReferenceEquals(pickerDelegate, completedDelegate)", picker);
			StringAssert.DoesNotContain("() => pickerDelegate = null", picker);
		}

		[Test]
		public void PickerReleasesSecurityScopeBeforeReportingImportCompletionOrCancellation()
		{
			var picker = IosFile("IosRetailContentPicker.cs");
			var didPick = picker[picker.IndexOf(
				"public override async void DidPickDocument", StringComparison.Ordinal)..];
			var cancelled = picker[picker.IndexOf(
				"public override void WasCancelled", StringComparison.Ordinal)..];

			StringAssert.Contains("Task<RetailImportResult>", picker);
			Assert.That(didPick.IndexOf("StopAccessingSecurityScopedResource", StringComparison.Ordinal),
				Is.LessThan(didPick.IndexOf("CompleteSafely", StringComparison.Ordinal)));
			Assert.That(didPick.IndexOf("FinishSafely();", StringComparison.Ordinal),
				Is.LessThan(didPick.IndexOf("CompleteSafely", StringComparison.Ordinal)));
			Assert.That(cancelled.IndexOf("FinishSafely();", StringComparison.Ordinal),
				Is.LessThan(cancelled.IndexOf("cancelled();", StringComparison.Ordinal)));
		}

		[Test]
		public void CoordinatorUsesTransactionalImporterAndTreatsCancelAsNavigation()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");

			StringAssert.Contains("RetailContentImporter", coordinator);
			StringAssert.Contains("new RetailContentImporter(requireStructuralValidation: false)", coordinator);
			StringAssert.Contains("catalog.InspectRequiredFilesOnly", coordinator);
			StringAssert.Contains("selectedPaths.Count != 1", coordinator);
			StringAssert.Contains("Directory.Exists(selectedPath)", coordinator);
			StringAssert.Contains("RetailImportError.Cancelled", coordinator);
			StringAssert.Contains("NeedsBase", coordinator);
			StringAssert.DoesNotContain("if (!status.CanLaunchYuriGame)", coordinator);
			StringAssert.Contains("StartGameOnce", coordinator);
		}

		[Test]
		public void CoordinatorRequiresTheCurrentImportedRuntimeLayout()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");

			StringAssert.Contains("RetailContentImporter.PrepareCurrentRuntimeLayout(contentRoot)", coordinator);
			StringAssert.Contains("sourceRoot: selectedPath", coordinator);
		}

		[Test]
		public void RuntimeScansConvertedMapsFromTheImportedContentRoot()
		{
			var root = RepositoryRoot();
			var manifest = File.ReadAllText(Path.Combine(root, "mods", "ra2", "mod.yaml"));

			StringAssert.Contains("^SupportDir|Content/ra2/maps: User", manifest);
		}

		[Test]
		public void CoordinatorPreparesAndValidatesEmbeddedMapsBeforeGameStartup()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var bootstrap = IosFile("IosEmbeddedMapBootstrap.cs");

			StringAssert.Contains("PrepareMapsAndStart();", coordinator);
			StringAssert.Contains("IosEmbeddedMapBootstrap.EnsurePlayableMaps", coordinator);
			StringAssert.Contains("RetailCompatibleMapSourceScanner", bootstrap);
			StringAssert.Contains("Ra2MapConversionService", bootstrap);
			StringAssert.Contains("players < 2", bootstrap);
			StringAssert.Contains("StartGameOnce();", coordinator);
		}

		[Test]
		public void CoordinatorRecoversAnInterruptedPublishBeforeCheckingReadiness()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var checkReadinessStart = coordinator.IndexOf("void CheckReadiness()", StringComparison.Ordinal);
			var hasLaunchableStart = coordinator.IndexOf("bool HasLaunchableContent()", StringComparison.Ordinal);
			var checkReadiness = coordinator[checkReadinessStart..hasLaunchableStart];

			Assert.That(checkReadiness.IndexOf(
				"RetailContentImporter.PrepareCurrentRuntimeLayout(contentRoot)", StringComparison.Ordinal),
				Is.LessThan(checkReadiness.IndexOf("catalog.InspectRequiredFilesOnly", StringComparison.Ordinal)));
		}

		[Test]
		public void BaseRuntimeReadinessStartsImmediatelyWithoutRequiringOptionalMusic()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var checkReadinessStart = coordinator.IndexOf("void CheckReadiness()", StringComparison.Ordinal);
			var hasLaunchableStart = coordinator.IndexOf("bool HasLaunchableContent()", StringComparison.Ordinal);
			var checkReadiness = coordinator[checkReadinessStart..hasLaunchableStart];

			StringAssert.Contains("StartGameOnce();", checkReadiness);
			StringAssert.DoesNotContain("theme.mix", checkReadiness);
			StringAssert.DoesNotContain("NeedsYuriOptional", checkReadiness);
		}

		[Test]
		public void CoordinatorSerializesThePickerAndImportLifecycle()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var viewController = IosFile("ViewController.cs");

			StringAssert.Contains("Interlocked.CompareExchange(ref importInProgress, 1, 0)", coordinator);
			StringAssert.Contains("Interlocked.Exchange(ref importInProgress, 0)", coordinator);
			StringAssert.Contains("model.State != IosContentImportState.Importing", viewController);
		}

		[Test]
		public void CoordinatorDoesNotStartTheGameWhileAnImportAttemptIsActive()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var startGameMethod = coordinator[coordinator.IndexOf(
				"void StartGameOnce()", StringComparison.Ordinal)..];

			StringAssert.Contains("Volatile.Read(ref importInProgress) != 0", startGameMethod);
		}

		[Test]
		public void CoordinatorRunsEnumerationAndImportOutsideTheUIKitThread()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");

			StringAssert.Contains("Task.Run(() => ImportSelectionInBackgroundAsync", coordinator);
			StringAssert.Contains("if (!Directory.Exists(selectedPath))", coordinator);
			StringAssert.Contains("ExpandSelection(new[] { selectedPath }).ToArray()", coordinator);
			StringAssert.Contains("await importer.ImportAsync", coordinator);
			StringAssert.Contains("viewController.BeginInvokeOnMainThread", coordinator);
		}

		[Test]
		public void FailedIncrementalImportFallsBackToUsableExistingContent()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");

			StringAssert.Contains("HasLaunchableContent()", coordinator);
			StringAssert.Contains("if (HasLaunchableContent())", coordinator);
			StringAssert.Contains("CheckReadiness();", coordinator);
		}

		[Test]
		public void WizardTracksEveryRetailRootIndependently()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var viewController = IosFile("ViewController.cs");

			foreach (var flag in new[]
			{
				"Ra2Imported", "LanguageImported", "Ra2MdImported", "LangMdImported"
			})
			{
				StringAssert.Contains(flag, coordinator);
				StringAssert.Contains($"model.{flag}", viewController);
			}

			StringAssert.Contains("IsImported(importedRoot, \"ra2.mix\")", coordinator);
			StringAssert.Contains("IsImported(importedRoot, \"language.mix\")", coordinator);
			StringAssert.Contains("IsImported(importedRoot, \"ra2md.mix\")", coordinator);
			StringAssert.Contains("IsImported(importedRoot, \"langmd.mix\")", coordinator);
			StringAssert.Contains("RequiredFileStatus(\"ra2.mix\", model.Ra2Imported)", viewController);
			StringAssert.Contains("RequiredFileStatus(\"language.mix\", model.LanguageImported)", viewController);
			StringAssert.Contains("RequiredFileStatus(\"ra2md.mix（YR）\", model.Ra2MdImported)", viewController);
			StringAssert.Contains("RequiredFileStatus(\"langmd.mix（YR）\", model.LangMdImported)", viewController);

			StringAssert.DoesNotContain("var baseReady = model.Status?.Base", viewController);
			StringAssert.DoesNotContain("var yuriReady = model.Status?.Yuri", viewController);
		}

		[Test]
		public void WizardUsesLargeLandscapeButtonsWithoutSliders()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var viewController = IosFile("ViewController.cs");

			StringAssert.Contains("NUCLEAR-CRISIS-BG-02.png", viewController);
			StringAssert.Contains("UIImage.FromFile", viewController);
			StringAssert.Contains("UIViewContentMode.ScaleAspectFill", viewController);
			StringAssert.Contains("选择正版游戏文件", viewController);
			StringAssert.Contains("ra2.mix", viewController);
			StringAssert.Contains("language.mix", viewController);
			StringAssert.Contains("●", viewController);
			StringAssert.Contains("ra2md.mix", viewController);
			StringAssert.Contains("langmd.mix", viewController);
			StringAssert.Contains("theme.mix", viewController);
			StringAssert.Contains("thememd.mix", viewController);
			StringAssert.Contains("尤里的复仇（可选）", viewController);
			StringAssert.Contains("buttons.AddArrangedSubview(Button(strings.ChooseFiles, chooseBase));", viewController);
			StringAssert.Contains("strings.ContinueGame", viewController);
			StringAssert.Contains("\"theme.mix\"", coordinator);
			StringAssert.Contains("\"thememd.mix\"", coordinator);
			StringAssert.Contains("RetailContentImporter.IsSupportedPickerFile", coordinator);
			StringAssert.Contains("IosContentImportState.NeedsYuriOptional", coordinator);
			StringAssert.DoesNotContain("strings.ContentStatus(model.Status)", viewController);
			StringAssert.DoesNotContain("strings.ImportBase", viewController);
			StringAssert.DoesNotContain("strings.ImportYuri", viewController);
			StringAssert.DoesNotContain("strings.ViewLicense", viewController);
			StringAssert.DoesNotContain("strings.Retry", viewController);
			StringAssert.DoesNotContain("UISlider", viewController);
			StringAssert.Contains("new UIScrollView", viewController);
			StringAssert.Contains("ContentLayoutGuide", viewController);
			StringAssert.Contains("FrameLayoutGuide", viewController);
			StringAssert.Contains("TopAnchor.ConstraintGreaterThanOrEqualTo", viewController);
			StringAssert.Contains("BottomAnchor.ConstraintLessThanOrEqualTo", viewController);
			StringAssert.Contains("Landscape", viewController);
		}

		[Test]
		public void ReadyContentDismissesTheImportOverlayBeforeLaunching()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var viewController = IosFile("ViewController.cs");

			StringAssert.Contains("DismissContentImportWizard", coordinator);
			StringAssert.Contains("contentWizard?.RemoveFromSuperview();", viewController);
		}

		[Test]
		public void StartupFailureRemovesTheWizardAndRestoresTheStatusAboveTheBackdrop()
		{
			var viewController = IosFile("ViewController.cs");
			var failureMethod = viewController[viewController.IndexOf(
				"public void ShowStartupFailure", StringComparison.Ordinal)..];

			StringAssert.Contains("contentWizard?.RemoveFromSuperview();", failureMethod);
			StringAssert.Contains("contentWizard = null;", failureMethod);
			StringAssert.Contains("status.Hidden = false;", failureMethod);
			StringAssert.Contains("View.BringSubviewToFront(status);", failureMethod);
		}




	}
}
