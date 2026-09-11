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
	RetailContentStatus? Status);

public sealed class IosContentImportCoordinator
{
	readonly ViewController viewController;
	readonly string supportPath;
	readonly string contentRoot;
	readonly string mapRoot;
	readonly Action startGame;
	readonly IosNativeStrings strings;
	readonly RetailContentCatalog catalog = new();
	readonly RetailContentImporter importer = new();
	readonly IosRetailContentPicker picker = new();
	int didStartGame;

	public IosContentImportCoordinator(
		ViewController viewController,
		string supportPath,
		Action startGame,
		IosNativeStrings strings)
	{
		this.viewController = viewController;
		this.supportPath = supportPath;
		contentRoot = Path.Combine(supportPath, "Content");
		mapRoot = Path.Combine(supportPath, "maps", "ra2", "nukehour-storage-v1");
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
		var status = InspectInstalledContent();
		if (!status.CanLaunchBaseGame)
		{
			Show(IosContentImportState.NeedsBase, strings.NeedsBaseMessage, status);
			return;
		}

		try
		{
			RuntimeShellmapInstaller.EnsurePlayableMapInstalled(supportPath);
			status = InspectInstalledContent();
		}
		catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidDataException)
		{
			Console.Error.WriteLine($"iOS playable map generation failed: {e}");
			Show(IosContentImportState.Error, strings.ImportError(RetailImportError.IoFailure), status);
			return;
		}

		if (!status.CanLaunchYuriGame)
		{
			Show(IosContentImportState.NeedsYuriOptional, strings.NeedsYuriMessage, status);
			return;
		}

		Show(IosContentImportState.Ready, strings.ReadyMessage, status);
	}

	RetailContentStatus InspectInstalledContent()
	{
		var retailRoot = Path.Combine(contentRoot, "ra2");
		var retailFiles = Directory.Exists(retailRoot) ?
			Directory.EnumerateFiles(retailRoot, "*", SearchOption.AllDirectories) : Enumerable.Empty<string>();
		var mapFiles = Directory.Exists(mapRoot) ?
			Directory.EnumerateFiles(mapRoot, "*", SearchOption.AllDirectories) : Enumerable.Empty<string>();
		return catalog.Inspect(retailFiles.Concat(mapFiles));
	}

	void ChooseFiles()
	{
		picker.Present(viewController, ImportSelectionAsync, () =>
			HandleImportResult(new RetailImportResult(false, RetailImportError.Cancelled,
				strings.CancelledMessage, null, null)));
	}

	async Task ImportSelectionAsync(IReadOnlyList<string> selectedPaths)
	{
		Show(IosContentImportState.Importing, strings.ImportingMessage);
		var files = ExpandSelection(selectedPaths).ToArray();
		var result = await importer.ImportAsync(new RetailImportRequest(files, contentRoot), CancellationToken.None);
		viewController.BeginInvokeOnMainThread(() => HandleImportResult(result));
	}

	static IEnumerable<string> ExpandSelection(IEnumerable<string> paths)
	{
		foreach (var path in paths)
		{
			if (Directory.Exists(path))
			{
				foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
					yield return file;
			}
			else if (File.Exists(path))
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
			Show(IosContentImportState.Error, strings.ImportError(result.Error), result.Status);
			return;
		}

		CheckReadiness();
	}

	void Show(IosContentImportState state, string message, RetailContentStatus? status = null)
	{
		viewController.ShowContentImportWizard(
			new IosContentWizardModel(state, strings.WizardTitle(state), message, status),
			ChooseFiles,
			ShowLicense,
			CheckReadiness,
			StartGameOnce);
	}

	void ShowLicense()
	{
		var license = new IosLicenseViewController(strings)
		{
			ModalPresentationStyle = UIModalPresentationStyle.OverFullScreen,
			ModalTransitionStyle = UIModalTransitionStyle.CrossDissolve,
		};
		viewController.PresentViewController(license, true, null);
	}

	void StartGameOnce()
	{
		if (Interlocked.Exchange(ref didStartGame, 1) == 0)
			viewController.BeginGameLaunch(startGame);
	}
}
