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

using System.IO;
using NUnit.Framework;
using OpenRA.Support;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosPerformancePolicyTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "engine")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		[Test]
		public void BenchmarkPolicyCanSuppressFrameDiagnosticsWithoutDisablingWatchdog()
		{
			PerformanceBenchmarkPolicy.IsActive = false;
			Assert.That(PerformanceBenchmarkPolicy.RecordFrameDiagnostics, Is.True);
			Assert.That(PerformanceBenchmarkPolicy.RunWatchdog, Is.True);

			PerformanceBenchmarkPolicy.IsActive = true;
			Assert.That(PerformanceBenchmarkPolicy.RecordFrameDiagnostics, Is.False);
			Assert.That(PerformanceBenchmarkPolicy.RunWatchdog, Is.True);

			PerformanceBenchmarkPolicy.IsActive = false;
		}

		[Test]
		public void BenchmarkCanForceHalfResolutionWorldRendering()
		{
			try
			{
				PerformanceBenchmarkPolicy.IsActive = true;
				System.Environment.SetEnvironmentVariable("OPENRA_IOS_PERF_RENDER_SCALE", "0.5");
				Assert.That(PerformanceBenchmarkPolicy.MinimumWorldDownscaleFactor, Is.EqualTo(2));
			}
			finally
			{
				System.Environment.SetEnvironmentVariable("OPENRA_IOS_PERF_RENDER_SCALE", null);
				PerformanceBenchmarkPolicy.IsActive = false;
			}
		}

		[Test]
		public void RendererConsumesTheBenchmarkWorldDownscalePolicy()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Renderer.cs"));

			StringAssert.Contains(
				"WorldDownscaleFactor = PerformanceBenchmarkPolicy.MinimumWorldDownscaleFactor;",
				source,
				"The benchmark render-scale option must affect the world framebuffer, not only the policy value.");
		}

		[Test]
		public void BenchmarkSamplesUseResourceSamplerAllocationGcAndThermalValues()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Support", "Benchmark.cs"));

			StringAssert.Contains("resources.TotalAllocatedBytes ?? 0", source);
			StringAssert.Contains("resources.Generation0Collections ?? 0", source);
			StringAssert.Contains("resources.Generation1Collections ?? 0", source);
			StringAssert.Contains("resources.Generation2Collections ?? 0", source);
			StringAssert.Contains("resources.ThermalState", source);
			StringAssert.DoesNotContain("GC.CollectionCount(", source,
				"All resource values in one sample must come from the same guarded sampler snapshot.");
		}

		[Test]
		public void BenchmarkProfilesSimulationAndRenderingCategories()
		{
			var root = RepositoryRoot();
			var world = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Game", "World.cs"));
			var actor = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Game", "Actor.cs"));
			var traits = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Game", "TraitDictionary.cs"));
			var worldRenderer = File.ReadAllText(Path.Combine(
				root, "engine", "OpenRA.Game", "Graphics", "WorldRenderer.cs"));
			var graphics = File.ReadAllText(Path.Combine(
				root, "engine", "OpenRA.Platforms.Default", "Sdl2GraphicsContext.cs"));

			StringAssert.Contains("new PerfSample(\"tick_traits\")", world);
			StringAssert.Contains("PerformanceCategoryProfiler.Global.Measure(\"Effect\"", world);
			StringAssert.Contains("PerformanceCategoryProfiler.Global.Measure(\"Actor Activity\"", actor);
			StringAssert.Contains("profiler.Measure(text, trait.GetType())", traits);
			foreach (var category in new[]
			{
				"Renderable Prepare", "Renderable Generation", "Overlay Generation",
				"Annotation Generation", "Terrain Render", "Prepared Renderable"
			})
				StringAssert.Contains($"PerformanceCategoryProfiler.Global.Measure(\"{category}\"", worldRenderer);

			StringAssert.Contains("PerformanceCategoryProfiler.Global.Measure(\"Draw Submission\")", graphics);
			StringAssert.Contains("PerformanceCategoryProfiler.Global.Measure(\"Present\")", graphics);
			StringAssert.DoesNotContain("framebufferStatus", graphics,
				"Per-draw framebuffer polling would contaminate the benchmark being measured.");
		}

		[Test]
		public void IosReleaseSkipsSynchronousGlErrorPolling()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Platforms.Default", "OpenGL.cs"));
			var method = source.Substring(source.IndexOf("public static void CheckGLError()", System.StringComparison.Ordinal));
			method = method.Substring(0, method.IndexOf("\n\t\t}", System.StringComparison.Ordinal) + 4);

			StringAssert.Contains("#if IOS && !DEBUG", method);
			Assert.That(method.IndexOf("return;", System.StringComparison.Ordinal),
				Is.LessThan(method.IndexOf("glGetError()", System.StringComparison.Ordinal)));
		}

		[Test]
		public void IosReleaseDoesNotInstallTheSynchronousGlDebugCallback()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Platforms.Default", "OpenGL.cs"));
			var setup = source.Substring(source.IndexOf("// Setup the debug message callback handler",
				System.StringComparison.Ordinal));
			setup = setup.Substring(0, setup.IndexOf("Console.WriteLine(\"OpenGL renderer:",
				System.StringComparison.Ordinal));

			StringAssert.Contains("#if !(IOS && !DEBUG)", setup,
				"Release iOS must not install a synchronous native callback that can throw during rendering.");
		}

		[Test]
		public void HangDiagnosticsUsesBenchmarkFramePolicy()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Support", "HangDiagnostics.cs"));

			StringAssert.Contains("!PerformanceBenchmarkPolicy.RecordFrameDiagnostics", source);
			StringAssert.DoesNotContain("!PerformanceBenchmarkPolicy.RunWatchdog", source);
		}

		[Test]
		public void IosStartsHangDiagnosticsAfterSupportDirectoryIsConfigured()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "AppDelegate.cs"));

			var supportDirectory = source.IndexOf("Directory.CreateDirectory(supportPath);", System.StringComparison.Ordinal);
			var start = source.IndexOf("HangDiagnostics.Start(supportPath);", System.StringComparison.Ordinal);
			var resume = source.IndexOf("HangDiagnostics.Resume();", start, System.StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThan(supportDirectory));
			Assert.That(resume, Is.GreaterThan(start));
		}

		[Test]
		public void IosRegistersDurableUnhandledExceptionReporterBeforeBackgroundServices()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "AppDelegate.cs"));

			var supportDirectory = source.IndexOf("Directory.CreateDirectory(supportPath);",
				System.StringComparison.Ordinal);
			var register = source.IndexOf("RegisterUnhandledExceptionHandler(supportPath);",
				System.StringComparison.Ordinal);
			var runtimeInitialization = source.IndexOf("GeneratedAotObjectRegistryRegistration.RegisterAll();",
				System.StringComparison.Ordinal);
			var diagnostics = source.IndexOf("HangDiagnostics.Start(supportPath);",
				System.StringComparison.Ordinal);

			Assert.That(register, Is.GreaterThan(supportDirectory),
				"The explicit writable support directory must exist before installing the fatal callback.");
			Assert.That(register, Is.LessThan(runtimeInitialization),
				"Install the fatal callback before fallible runtime and audio initialization.");
			Assert.That(register, Is.LessThan(diagnostics),
				"Install the fatal callback before timers or other background services can fail.");
			StringAssert.Contains("UnhandledExceptionEventHandler? unhandledExceptionHandler;", source,
				"AppDomain.UnhandledException requires its dedicated delegate type on iOS/.NET 8.");
			StringAssert.Contains("if (unhandledExceptionHandler != null)", source,
				"Repeated launch callbacks must not add duplicate AppDomain subscribers.");
			StringAssert.Contains("ExceptionHandler.HandleFatalError(args.ExceptionObject, supportPath);", source,
				"Persist the original AppDomain payload synchronously to the already-known support directory.");
			StringAssert.Contains("AppDomain.CurrentDomain.UnhandledException += unhandledExceptionHandler;", source);
			StringAssert.Contains("AppDomain.CurrentDomain.UnhandledException -= unhandledExceptionHandler;", source);

			var willTerminate = source.IndexOf("public override void WillTerminate", System.StringComparison.Ordinal);
			var unregister = source.IndexOf("UnregisterUnhandledExceptionHandler();", willTerminate,
				System.StringComparison.Ordinal);
			var networkingShutdown = source.IndexOf("NearbyGameNetworking.Shutdown();", willTerminate,
				System.StringComparison.Ordinal);
			var diagnosticsStop = source.IndexOf("IosMetricKitDiagnostics.Stop();", willTerminate,
				System.StringComparison.Ordinal);
			Assert.That(unregister, Is.GreaterThan(willTerminate),
				"Detach the process-wide callback when UIKit terminates the delegate lifecycle.");
			Assert.That(unregister, Is.GreaterThan(networkingShutdown));
			Assert.That(unregister, Is.GreaterThan(diagnosticsStop),
				"Keep fatal persistence active until fallible shutdown work has completed.");
		}

		[Test]
		public void DetailedDestructionAuditTracesActorTraitEffectAndFrameEndWork()
		{
			var world = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "World.cs"));
			var traits = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "TraitDictionary.cs"));

			StringAssert.Contains("World.ActorTick.", world);
			StringAssert.Contains("World.EffectTick.", world);
			StringAssert.Contains("World.FrameEndTask.", world);
			StringAssert.Contains("World.TraitTick.", traits);
		}

		[Test]
		public void IosProvidesNativeThermalState()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "AppDelegate.cs"));

			StringAssert.Contains("RuntimeResourceSampler.ThermalStateProvider", source);
			StringAssert.Contains("NSProcessInfo.ProcessInfo.ThermalState", source);
		}

		[Test]
		public void IosCanEnableTimedBenchmarkFromLaunchEnvironment()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "AppDelegate.cs"));

			StringAssert.Contains("OPENRA_IOS_PERF_PREFIX", source);
			StringAssert.Contains("Launch.Benchmark=", source);
		}

		[Test]
		public void IosPreventsIdleLockDuringLongRunningGameplayAndAudits()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "AppDelegate.cs"));

			StringAssert.Contains("application.IdleTimerDisabled = true;", source);
		}

		[Test]
		public void IosPersistsFatalGameLoopExceptionsBeforeShowingTheFailureView()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "AppDelegate.cs"));

			var catchBlock = source.IndexOf("catch (Exception e)", System.StringComparison.Ordinal);
			var persist = source.IndexOf("ExceptionHandler.HandleFatalError(e);", catchBlock,
				System.StringComparison.Ordinal);
			var showFailure = source.IndexOf("rootViewController.ShowStartupFailure(e.ToString());", catchBlock,
				System.StringComparison.Ordinal);

			Assert.That(catchBlock, Is.GreaterThanOrEqualTo(0));
			Assert.That(persist, Is.GreaterThan(catchBlock),
				"Manual iOS launches must persist the full exception and stack trace.");
			Assert.That(showFailure, Is.GreaterThan(persist),
				"Persist the exception before replacing the game surface with the failure view.");

			var handlerSource = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Support", "ExceptionHandler.cs"));
			StringAssert.Contains("WriteFatalErrorReport(path, report);", handlerSource,
				"Fatal exception persistence must bypass the asynchronous logging queue.");
		}

		[Test]
		public void FatalExceptionReportIsDurableWhenPersistenceReturns()
		{
			var path = Path.Combine(Path.GetTempPath(), $"openra-fatal-{System.Guid.NewGuid():N}.log");
			try
			{
				var writer = typeof(ExceptionHandler).GetMethod("WriteFatalErrorReport",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
				Assert.That(writer, Is.Not.Null, "Missing synchronous fatal-report writer.");

				writer!.Invoke(null, new object[] { path, "fatal-stack-marker" });
				Assert.That(File.ReadAllText(path), Does.Contain("fatal-stack-marker"));
				using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
				Assert.That(exclusive.Length, Is.GreaterThan(0));
			}
			finally
			{
				if (File.Exists(path))
					File.Delete(path);
			}
		}

		[Test]
		public void FatalExceptionReportConstructionFallsBackWhenExceptionMetadataThrows()
		{
			var builder = typeof(ExceptionHandler).GetMethod("BuildFatalErrorReportSafely",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			Assert.That(builder, Is.Not.Null, "Missing best-effort fatal-report builder.");

			ThrowingMessageException exception;
			try
			{
				throw new ThrowingMessageException();
			}
			catch (ThrowingMessageException e)
			{
				exception = e;
			}

			var report = (string)builder!.Invoke(null, new object[] { exception })!;
			Assert.That(report, Does.Contain(typeof(ThrowingMessageException).FullName));
			Assert.That(report, Does.Contain("<message unavailable>"));
			Assert.That(report, Does.Contain(nameof(FatalExceptionReportConstructionFallsBackWhenExceptionMetadataThrows)),
				"A broken Message getter must not discard the independently available stack trace.");
		}

		[Test]
		public void FatalReportSnapshotsMutableGameStateAndIsolatesMetadataSections()
		{
			var sectionWriter = typeof(ExceptionHandler).GetMethod("AppendReportSection",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
				null, new[] { typeof(System.Text.StringBuilder), typeof(string), typeof(System.Action) }, null);
			Assert.That(sectionWriter, Is.Not.Null, "Fatal metadata sections need independent failure boundaries.");

			var report = new System.Text.StringBuilder();
			sectionWriter!.Invoke(null, new object[]
			{
				report,
				"racing-world",
				(System.Action)(() => throw new System.InvalidOperationException("world switched")),
			});
			report.AppendLine("original-exception-evidence");
			Assert.That(report.ToString(), Does.Contain("racing-world"));
			Assert.That(report.ToString(), Does.Contain("original-exception-evidence"),
				"One racing metadata read must not abort later exception evidence.");

			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Support", "ExceptionHandler.cs"));
			StringAssert.Contains("var modData = Game.ModData;", source);
			StringAssert.Contains("var manifest = modData?.Manifest;", source);
			StringAssert.Contains("var orderManager = Game.OrderManager;", source);
			StringAssert.Contains("var world = orderManager?.World;", source);
			StringAssert.Contains("var map = world?.Map;", source);
			StringAssert.DoesNotContain("Game.ModData.Manifest", source);
			StringAssert.DoesNotContain("Game.OrderManager.World", source);
		}

		[Test]
		public void FatalExceptionReporterPersistsNonExceptionPayloadToExplicitSupportDirectory()
		{
			var reporter = typeof(ExceptionHandler).GetMethod("HandleFatalError",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
				null, new[] { typeof(object), typeof(string) }, null);
			Assert.That(reporter, Is.Not.Null,
				"The AppDomain callback needs an object overload and must not depend on an initialized Platform.SupportDir.");

			var supportDirectory = Path.Combine(Path.GetTempPath(), $"openra-fatal-{System.Guid.NewGuid():N}");
			try
			{
				Directory.CreateDirectory(supportDirectory);
				reporter!.Invoke(null, new object[] { "native-non-exception-payload", supportDirectory });

				var logsDirectory = Path.Combine(supportDirectory, "Logs");
				var reports = Directory.GetFiles(logsDirectory, "exception-*.log");
				Assert.That(reports, Has.Length.EqualTo(1));
				Assert.That(File.ReadAllText(reports[0]), Does.Contain("native-non-exception-payload"));
			}
			finally
			{
				if (Directory.Exists(supportDirectory))
					Directory.Delete(supportDirectory, recursive: true);
			}
		}

		public sealed class ThrowingMessageException : System.Exception
		{
			public override string Message => throw new System.InvalidOperationException("metadata unavailable");
		}

		[Test]
		public void BenchmarkClockStartsAfterWorldLoadingCompletes()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Game.cs"));
			var afterGameStart = source.IndexOf("AfterGameStart();", System.StringComparison.Ordinal);
			var benchmarkReset = source.IndexOf("benchmark?.BeginAutomatic();", afterGameStart, System.StringComparison.Ordinal);

			Assert.That(afterGameStart, Is.GreaterThanOrEqualTo(0));
			Assert.That(benchmarkReset, Is.GreaterThan(afterGameStart));
			StringAssert.Contains("if (benchmark?.Ready == true)", source);
		}
	}
}
