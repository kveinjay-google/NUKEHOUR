using Foundation;
using UIKit;
using UniformTypeIdentifiers;

namespace OpenRA.iOS;

public sealed class IosRetailContentPicker
{
	PickerDelegate? pickerDelegate;

	public void Present(
		UIViewController presenter,
		Func<IReadOnlyList<string>, Task> importSelection,
		Action cancelled)
	{
		var picker = new UIDocumentPickerViewController(
			new[] { UTTypes.Data, UTTypes.Content, UTTypes.Folder }, true)
		{
			AllowsMultipleSelection = true,
			ModalPresentationStyle = UIModalPresentationStyle.FormSheet
		};

		pickerDelegate = new PickerDelegate(importSelection, cancelled, () => pickerDelegate = null);
		picker.Delegate = pickerDelegate;
		presenter.PresentViewController(picker, true, null);
	}

	sealed class PickerDelegate : UIDocumentPickerDelegate
	{
		readonly Func<IReadOnlyList<string>, Task> importSelection;
		readonly Action cancelled;
		readonly Action finished;

		public PickerDelegate(
			Func<IReadOnlyList<string>, Task> importSelection,
			Action cancelled,
			Action finished)
		{
			this.importSelection = importSelection;
			this.cancelled = cancelled;
			this.finished = finished;
		}

		public override async void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
		{
			var accessed = new List<NSUrl>();
			try
			{
				foreach (var url in urls)
					if (url.StartAccessingSecurityScopedResource())
						accessed.Add(url);

				await importSelection(urls.Select(url => url.Path!).ToArray());
			}
			finally
			{
				foreach (var url in accessed)
					url.StopAccessingSecurityScopedResource();

				finished();
			}
		}

		public override void WasCancelled(UIDocumentPickerViewController controller)
		{
			try
			{
				cancelled();
			}
			finally
			{
				finished();
			}
		}
	}
}
