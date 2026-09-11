using Foundation;
using AVFoundation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OpenRA;
using OpenRA.Mods.Cnc.FileSystem;
using OpenRA.Mods.Common.FileSystem;
using OpenRA.Mods.RA2.Content;
using OpenRA.Network;
using OpenRA.Support;
using UIKit;

namespace OpenRA.iOS;

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }
#if PUBLIC_CLEAN
	IosContentImportCoordinator? contentImportCoordinator;
#endif
	UnhandledExceptionEventHandler? unhandledExceptionHandler;

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
		// RTS sessions and device audits can run for a long time without touch input.
		// Keep the display active so iOS does not suspend the simulation mid-session.
		application.IdleTimerDisabled = true;
		Platform.OverrideHostPlatform(PlatformType.iOS);
		var supportPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OpenRA");
		Directory.CreateDirectory(supportPath);
		var nativeLanguage = IosNativeLanguage.Resolve(Path.Combine(supportPath, "settings.yaml"));
		var nativeStrings = IosNativeStrings.ForLanguage(nativeLanguage.EffectiveLanguage);
		RegisterUnhandledExceptionHandler(supportPath);
		RuntimeResourceSampler.ThermalStateProvider =
			() => NSProcessInfo.ProcessInfo.ThermalState.ToString();
		GeneratedAotObjectRegistryRegistration.RegisterAll();
		var audioSession = AVAudioSession.SharedInstance();
		audioSession.SetCategory(AVAudioSessionCategory.Playback, AVAudioSessionCategoryOptions.MixWithOthers);
		audioSession.SetActive(true);

        var rootViewController = new ViewController(nativeStrings);
        Window = new UIWindow(UIScreen.MainScreen.Bounds)
        {
            RootViewController = rootViewController
        };
        Window.MakeKeyAndVisible();

        var bundlePath = NSBundle.MainBundle.ResourcePath;
        if (string.IsNullOrEmpty(bundlePath))
            throw new InvalidOperationException("OpenRA iOS bundle resource path is unavailable.");

		HangDiagnostics.Start(supportPath);
		HangDiagnostics.Resume();
		IosMetricKitDiagnostics.Start(supportPath);
		NearbyGameNetworking.InstallService(new OpenRA.Platforms.Default.IosNearbyGameService());

        Game.PlatformFactoryOverride = _ => new OpenRA.Platforms.Default.IosPlatform();
        ObjectCreator.LoaderFactoryOverride = format => format switch
        {
            "Mix" => new MixLoader(),
            "AudioBag" => new AudioBagLoader(),
            "ContentInstallerFileSystem" => new IosContentFileSystemLoader(),
            _ => null
        };
		// Return from UIApplicationDelegate first so UIKit can finish the initial
		// scene and create the drawable. The callback still runs on the main thread,
		// which SDL's UIKit backend requires.
		void StartGame()
		{
			try
			{
				#if PUBLIC_CLEAN
				RuntimeShellmapInstaller.EnsureInstalled(supportPath);
				#endif
				IosLaunchPreflight.EnsureComplete(bundlePath);
				var sceneSize = rootViewController.View!.Bounds.Size;
				var width = Math.Max(1, (int)Math.Round(sceneSize.Width));
				var height = Math.Max(1, (int)Math.Round(sceneSize.Height));
				var display = IosDisplayLayout.ForScene(width, height);
				var launchArguments = new List<string>
				{
					$"Engine.EngineDir={bundlePath}",
					$"Engine.SupportDir={supportPath}",
					$"Engine.SystemLanguage={nativeLanguage.SystemLanguageTag}",
					$"Engine.LaunchPath={NSBundle.MainBundle.ExecutablePath}",
					"Game.Mod=ra2",
					"Game.UseClassicMouseStyle=true",
					"Graphics.Mode=Fullscreen",
					$"Graphics.FullscreenSize={display.Width},{display.Height}",
					$"Graphics.UIScale={display.UiScale.ToString("0.####", CultureInfo.InvariantCulture)}"
				};
#if PUBLIC_CLEAN
				launchArguments.Add($"Engine.ModSearchPaths={bundlePath}/mods");
				launchArguments.Add("Engine.PublicClean=true");
				launchArguments.Add("Game.AllowDownloading=false");
#else
				launchArguments.Add($"Engine.ModSearchPaths={bundlePath}/mods,{supportPath}/mods");
#endif
				var skirmishTarget = Environment.GetEnvironmentVariable("OPENRA_IOS_AUTOTEST_SKIRMISH");
				var skirmishArgument = IosDevelopmentLaunch.BuildSkirmishArgument(skirmishTarget);
				if (skirmishArgument != null)
				{
					launchArguments.Add(skirmishArgument);
					Console.WriteLine($"OpenRA iOS automatic skirmish target: {skirmishTarget}");
				}

				var benchmarkPrefix = Environment.GetEnvironmentVariable("OPENRA_IOS_PERF_PREFIX");
				if (!string.IsNullOrWhiteSpace(benchmarkPrefix))
					launchArguments.Add($"Launch.Benchmark={benchmarkPrefix}");

				Console.WriteLine($"OpenRA iOS scene: {display.Width}x{display.Height} points; " +
					$"adaptive UI: {display.EffectiveWidth}x{display.EffectiveHeight} at {display.UiScale:0.###}x");
				AotPreserve.Touch();
				Game.InitializeAndRun(launchArguments.ToArray());
			}
			catch (Exception e)
			{
				Console.WriteLine($"OpenRA iOS startup failed: {e}");
				ExceptionHandler.HandleFatalError(e);
				IosSmokeTestJournal.RecordFailure(e);
				Window?.MakeKeyAndVisible();
				rootViewController.ShowStartupFailure(e.ToString());
			}
		}

		rootViewController.StartInLandscape(() =>
		{
#if PUBLIC_CLEAN
			contentImportCoordinator = new IosContentImportCoordinator(
				rootViewController, supportPath, StartGame, nativeStrings);
			contentImportCoordinator.Start();
#else
			StartGame();
#endif
		});

        return true;
    }

	void RegisterUnhandledExceptionHandler(string supportPath)
	{
		if (unhandledExceptionHandler != null)
			return;

		unhandledExceptionHandler = (_, args) =>
			ExceptionHandler.HandleFatalError(args.ExceptionObject, supportPath);
		AppDomain.CurrentDomain.UnhandledException += unhandledExceptionHandler;
	}

	void UnregisterUnhandledExceptionHandler()
	{
		if (unhandledExceptionHandler == null)
			return;

		AppDomain.CurrentDomain.UnhandledException -= unhandledExceptionHandler;
		unhandledExceptionHandler = null;
	}

	public override void DidEnterBackground(UIApplication application)
	{
		HangDiagnostics.Pause();
		NearbyGameNetworking.Suspend();
	}

	public override void OnActivated(UIApplication application)
	{
		HangDiagnostics.Resume();
		NearbyGameNetworking.Resume();
	}

	public override void WillTerminate(UIApplication application)
	{
		NearbyGameNetworking.Shutdown();
		IosMetricKitDiagnostics.Stop();
		UnregisterUnhandledExceptionHandler();
	}
}
