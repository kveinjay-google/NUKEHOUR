using Foundation;
using OpenRA.Mods.RA2.Content;
using UIKit;
using UniformTypeIdentifiers;

namespace OpenRA.iOS;

public sealed class IosRetailContentPicker
{
	PickerDelegate? pickerDelegate;

	public void Present(
		UIViewController presenter,
		Func<IReadOnlyList<string>, Task<RetailImportResult>> importSelection,
		Action<RetailImportResult> completed,
		Action cancelled,
		Action<Exception> failed)
	{
		// Use the explicit legacy Open mode for folders.  The iOS 17 runtime
		// misclassifies the UTType/asCopy initializer as an import operation for
		// public.folder and aborts the process before it can present Files.
		var picker = new UIDocumentPickerViewController(
			new[] { UTTypes.Folder.Identifier }, UIDocumentPickerMode.Open)
		{
			AllowsMultipleSelection = false,
			ModalPresentationStyle = UIModalPresentationStyle.FormSheet
		};

		var presentedDelegate = new PickerDelegate(
			importSelection, completed, cancelled, failed, ClearDelegate);
		pickerDelegate = presentedDelegate;
		picker.Delegate = pickerDelegate;
		presenter.PresentViewController(picker, true, null);
	}

	void ClearDelegate(PickerDelegate completedDelegate)
	{
		if (ReferenceEquals(pickerDelegate, completedDelegate))
			pickerDelegate = null;
	}

	sealed class PickerDelegate : UIDocumentPickerDelegate
	{
		readonly Func<IReadOnlyList<string>, Task<RetailImportResult>> importSelection;
		readonly Action<RetailImportResult> completed;
		readonly Action cancelled;
		readonly Action<Exception> failed;
		readonly Action<PickerDelegate> finished;

		public PickerDelegate(
			Func<IReadOnlyList<string>, Task<RetailImportResult>> importSelection,
			Action<RetailImportResult> completed,
			Action cancelled,
			Action<Exception> failed,
			Action<PickerDelegate> finished)
		{
			this.importSelection = importSelection;
			this.completed = completed;
			this.cancelled = cancelled;
			this.failed = failed;
			this.finished = finished;
		}

		public override async void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
		{
			var accessed = new List<NSUrl>();
			RetailImportResult? result = null;
			Exception? failure = null;
			try
			{
				foreach (var url in urls)
					if (url.StartAccessingSecurityScopedResource())
						accessed.Add(url);

				result = await importSelection(urls.Select(url => url.Path!).ToArray());
			}
			catch (Exception e)
			{
				failure = e;
			}
			finally
			{
				foreach (var url in accessed)
				{
					try
					{
						url.StopAccessingSecurityScopedResource();
					}
					catch (Exception e)
					{
						failure ??= e;
					}
				}

				FinishSafely();
			}

			// Completion can synchronously start the engine loop on the main thread.
			// Dispatch it only after all security-scoped URLs and picker references
			// have been released.
			if (failure != null)
				ReportFailure(failure);
			else if (result != null)
				CompleteSafely(result);
			else
				ReportFailure(new InvalidOperationException("The content import returned no result."));
		}

		public override void WasCancelled(UIDocumentPickerViewController controller)
		{
			FinishSafely();
			try
			{
				cancelled();
			}
			catch (Exception e)
			{
				ReportFailure(e);
			}
		}

		void CompleteSafely(RetailImportResult result)
		{
			try
			{
				completed(result);
			}
			catch (Exception e)
			{
				ReportFailure(e);
			}
		}

		void ReportFailure(Exception e)
		{
			Console.Error.WriteLine($"iOS document picker callback failed: {e.GetType().Name}");
			try
			{
				failed(e);
			}
			catch (Exception callbackError)
			{
				Console.Error.WriteLine(
					$"iOS document picker failure callback failed: {callbackError.GetType().Name}");
			}
		}

		void FinishSafely()
		{
			try
			{
				finished(this);
			}
			catch (Exception e)
			{
				Console.Error.WriteLine($"iOS document picker cleanup failed: {e.GetType().Name}");
			}
		}
	}
}
