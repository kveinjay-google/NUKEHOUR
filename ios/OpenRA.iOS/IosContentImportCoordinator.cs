using OpenRA.Mods.RA2.Content;
using UIKit;

namespace OpenRA.iOS;

public enum IosContentImportState
{
	Checking,
	NeedsBase,
	Importing,
	NeedsYuriOptional,
	Ready,
	Error,
}

public sealed record IosContentWizardModel(
	IosContentImportState State,
	string Title,
	string Message,
	RetailContentStatus? Status,
	bool Ra2Imported,
	bool LanguageImported,
	bool Ra2MdImported,
	bool LangMdImported,
	bool Ra2MusicImported,
	bool YuriMusicImported);

public sealed class IosContentImportCoordinator
{
	readonly ViewController viewController;
	readonly string enginePath;
	readonly string contentRoot;
	readonly Action startGame;
	readonly IosNativeStrings strings;
	readonly RetailContentCatalog catalog = new();
	readonly RetailContentImporter importer = new RetailContentImporter(requireStructuralValidation: false);
	readonly IosRetailContentPicker picker = new();
	int didStartGame;
	int importInProgress;
	int mapPreparationInProgress;

	public IosContentImportCoordinator(
		ViewController viewController,
		string enginePath,
		string supportPath,
		Action startGame,
		IosNativeStrings strings)
	{
		this.viewController = viewController;
		this.enginePath = enginePath;
		contentRoot = Path.Combine(supportPath, "Content");
		this.startGame = startGame;
		this.strings = strings ?? throw new ArgumentNullException(nameof(strings));
	}

	public void Start()
	{
		Show(IosContentImportState.Checking, strings.CheckingMessage);
		CheckReadiness();
	}

	void CheckReadiness()
	{
		var runtimeReady = RetailContentImporter.PrepareCurrentRuntimeLayout(contentRoot);
		var importedRoot = Path.Combine(contentRoot, "ra2");
		var status = catalog.InspectRequiredFilesOnly(importedRoot);
		if (!status.CanLaunchBaseGame)
		{
			Show(IosContentImportState.NeedsBase, strings.NeedsBaseMessage, status);
			return;
		}

		if (!runtimeReady)
		{
			Show(IosContentImportState.NeedsBase,
				"请选择完整的《红色警戒 2》游戏目录以导入运行资源。", status);
			return;
		}

		PrepareMapsAndStart();
	}

	void PrepareMapsAndStart()
	{
		if (Interlocked.CompareExchange(ref mapPreparationInProgress, 1, 0) != 0)
			return;

		Show(IosContentImportState.Checking, "正在从已导入的游戏文件准备地图…");
		_ = Task.Run(() => IosEmbeddedMapBootstrap.EnsurePlayableMaps(
			enginePath, Path.GetDirectoryName(contentRoot)!, CancellationToken.None))
			.ContinueWith(task => viewController.BeginInvokeOnMainThread(() =>
			{
				Interlocked.Exchange(ref mapPreparationInProgress, 0);
				if (task.IsCompletedSuccessfully && task.Result.Ready)
				{
					StartGameOnce();
					return;
				}

				var message = task.IsCompletedSuccessfully ? task.Result.Message :
					"地图准备过程中发生错误，请重新导入游戏文件。";
				if (task.Exception != null)
					Console.Error.WriteLine($"iOS embedded map preparation failed: {task.Exception}");
				Show(IosContentImportState.Error, message);
			}));
	}

	bool HasLaunchableContent()
	{
		var runtimeReady = RetailContentImporter.PrepareCurrentRuntimeLayout(contentRoot);
		var importedRoot = Path.Combine(contentRoot, "ra2");
		return catalog.InspectRequiredFilesOnly(importedRoot).CanLaunchBaseGame &&
			runtimeReady;
	}

	static bool IsImported(string directory, string name)
	{
		var path = Path.Combine(directory, name);
		return File.Exists(path) && new FileInfo(path).Length > 0;
	}

	void ChooseBase() => ChooseFiles();

	void ChooseFiles()
	{
		if (Interlocked.CompareExchange(ref importInProgress, 1, 0) != 0)
			return;

		var didCompleteAttempt = 0;
		void CompleteAttempt(RetailImportResult result)
		{
			if (Interlocked.Exchange(ref didCompleteAttempt, 1) != 0)
				return;

			Interlocked.Exchange(ref importInProgress, 0);
			HandleImportResult(result);
		}

		void CompleteAttemptOnMainThread(RetailImportResult result)
		{
			viewController.BeginInvokeOnMainThread(() => CompleteAttempt(result));
		}

		void FailAttempt(Exception e)
		{
			Console.Error.WriteLine($"iOS retail content picker failed: {e.GetType().Name}");
			CompleteAttemptOnMainThread(
				new RetailImportResult(false, RetailImportError.IoFailure, e.Message, null, null));
		}

		try
		{
			picker.Present(
				viewController,
				ImportSelectionAsync,
				CompleteAttemptOnMainThread,
				() => CompleteAttemptOnMainThread(new RetailImportResult(false, RetailImportError.Cancelled,
					strings.CancelledMessage, null, null)),
				FailAttempt);
		}
		catch (Exception e)
		{
			CompleteAttemptOnMainThread(new RetailImportResult(false, RetailImportError.IoFailure,
				e.Message, null, null));
		}
	}

	async Task<RetailImportResult> ImportSelectionAsync(IReadOnlyList<string> selectedPaths)
	{
		if (selectedPaths.Count != 1)
			return new RetailImportResult(false, RetailImportError.UnsupportedFile,
				"Select one extracted Red Alert 2 installation directory.", null, null);

		Show(IosContentImportState.Importing, strings.ImportingMessage);
		var selectedPath = selectedPaths[0];
		return await Task.Run(() => ImportSelectionInBackgroundAsync(selectedPath))
			.ConfigureAwait(false);
	}

	async Task<RetailImportResult> ImportSelectionInBackgroundAsync(string selectedPath)
	{
		if (!Directory.Exists(selectedPath))
			return new RetailImportResult(false, RetailImportError.UnsupportedFile,
				"Select one extracted Red Alert 2 installation directory.", null, null);

		string[] files;
		try
		{
			files = ExpandSelection(new[] { selectedPath }).ToArray();
		}
		catch (Exception e) when (e is IOException || e is UnauthorizedAccessException ||
			e is System.Security.SecurityException)
		{
			Console.Error.WriteLine($"iOS retail content enumeration failed: {e.GetType().Name}");
			return new RetailImportResult(false, RetailImportError.IoFailure,
				e.Message, null, null);
		}

		if (files.Length == 0)
			return new RetailImportResult(false, RetailImportError.UnsupportedFile,
				"The selected directory does not contain supported Red Alert 2 files.", null, null);

		try
		{
			return await importer.ImportAsync(
				new RetailImportRequest(files, contentRoot, sourceRoot: selectedPath),
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception e)
		{
			Console.Error.WriteLine($"iOS retail content import failed unexpectedly: {e.GetType().Name}");
			return new RetailImportResult(false, RetailImportError.IoFailure, e.Message, null, null);
		}
	}

	static IEnumerable<string> ExpandSelection(IEnumerable<string> paths)
	{
		foreach (var path in paths)
		{
			if (Directory.Exists(path))
			{
				var options = new EnumerationOptions
				{
					RecurseSubdirectories = true,
					IgnoreInaccessible = false,
					AttributesToSkip = FileAttributes.ReparsePoint,
				};
				foreach (var file in Directory.EnumerateFiles(path, "*", options))
					if (RetailContentImporter.IsSupportedPickerFile(file))
						yield return file;
			}
			else if (File.Exists(path) && RetailContentImporter.IsSupportedPickerFile(path))
				yield return path;
		}
	}

	void HandleImportResult(RetailImportResult result)
	{
		if (result.Error == RetailImportError.Cancelled)
		{
			CheckReadiness();
			return;
		}

		if (!result.Published)
		{
			Console.Error.WriteLine($"iOS retail content import failed ({result.Error}): {result.Message}");
			if (HasLaunchableContent())
			{
				CheckReadiness();
				return;
			}

			Show(IosContentImportState.Error, strings.ImportError(result.Error), result.Status);
			return;
		}

		CheckReadiness();
	}

	void Show(IosContentImportState state, string message, RetailContentStatus? status = null)
	{
		var importedRoot = Path.Combine(contentRoot, "ra2");
		Action? continueGame = state == IosContentImportState.NeedsYuriOptional ? StartGameOnce : null;
		viewController.ShowContentImportWizard(
			new IosContentWizardModel(state, strings.WizardTitle(state), message, status,
				IsImported(importedRoot, "ra2.mix"), IsImported(importedRoot, "language.mix"),
				IsImported(importedRoot, "ra2md.mix"), IsImported(importedRoot, "langmd.mix"),
				IsImported(importedRoot, "theme.mix"), IsImported(importedRoot, "thememd.mix")),
			ChooseBase, continueGame);
	}

	void StartGameOnce()
	{
		if (Volatile.Read(ref importInProgress) != 0)
			return;

		if (Interlocked.Exchange(ref didStartGame, 1) == 0)
		{
			viewController.DismissContentImportWizard();
			startGame();
		}
	}
}
