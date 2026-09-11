#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenRA.Mods.Common.Installer;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class InstallFromSourceLogic : ChromeLogic
	{
		[FluentReference]
		const string DetectingSources = "label-detecting-sources";

		[FluentReference]
		const string CheckingSources = "label-checking-sources";

		[FluentReference("title")]
		const string SearchingSourceFor = "label-searching-source-for";

		[FluentReference]
		const string ContentPackageInstallation = "label-content-package-installation";

		[FluentReference]
		const string GameSources = "label-game-sources";

		[FluentReference]
		const string DigitalInstalls = "label-digital-installs";

		[FluentReference]
		const string GameContentNotFound = "label-game-content-not-found";

		[FluentReference]
		const string AlternativeContentSources = "label-alternative-content-sources";

		[FluentReference]
		const string SelectInstallDirectory = "label-select-install-directory";

		[FluentReference]
		const string SelectedDirectoryInvalid = "label-selected-directory-invalid";

		[FluentReference]
		const string InstallingContent = "label-installing-content";

		[FluentReference("filename")]
		public const string CopyingFilename = "label-copying-filename";

		[FluentReference("filename", "progress")]
		public const string CopyingFilenameProgress = "label-copying-filename-progress";

		[FluentReference]
		const string InstallationFailed = "label-installation-failed";

		[FluentReference]
		const string CheckInstallLog = "label-check-install-log";

		[FluentReference("filename")]
		public const string Extracting = "label-extracting-filename";

		[FluentReference("filename", "progress")]
		public const string ExtractingProgress = "label-extracting-filename-progress";

		[FluentReference]
		public const string Continue = "button-continue";

		[FluentReference]
		const string Cancel = "button-cancel";

		[FluentReference]
		const string Retry = "button-retry";

		[FluentReference]
		const string Back = "button-back";

		// Hide percentage indicators for files smaller than 25 MB
		public const int ShowPercentageThreshold = 26214400;

		enum Mode { Progress, Message, List }

		readonly ModData modData;
		readonly ModContent content;
		readonly Dictionary<string, ModContent.ModSource> sources;

		readonly Widget panel;
		readonly LabelWidget titleLabel;
		readonly ButtonWidget primaryButton;
		readonly ButtonWidget secondaryButton;

		// Progress panel
		readonly Widget progressContainer;
		readonly ProgressBarWidget progressBar;
		readonly LabelWidget progressLabel;

		// Message panel
		readonly Widget messageContainer;
		readonly LabelWidget messageLabel;

		// List Panel
		readonly Widget listContainer;
		readonly ScrollPanelWidget listPanel;
		readonly Widget listHeaderTemplate;
		readonly LabelWidget labelListTemplate;
		readonly ContainerWidget checkboxListTemplate;
		readonly LabelWidget listLabel;

		ModContent.ModPackage[] availablePackages;
		IDictionary<string, bool> selectedPackages;
		int activeOperation;

		Mode visible = Mode.Progress;

		[ObjectCreator.UseCtor]
		public InstallFromSourceLogic(
			Widget widget, ModData modData, ModContent content, Dictionary<string, ModContent.ModSource> sources,
			bool chooseDirectory)
		{
			this.modData = modData;
			this.content = content;
			this.sources = sources;

			Log.AddChannel("install", "install.log");

			panel = widget.Get("SOURCE_INSTALL_PANEL");

			titleLabel = panel.Get<LabelWidget>("TITLE");

			primaryButton = panel.Get<ButtonWidget>("PRIMARY_BUTTON");
			secondaryButton = panel.Get<ButtonWidget>("SECONDARY_BUTTON");

			// Progress view
			progressContainer = panel.Get("PROGRESS");
			progressContainer.IsVisible = () => visible == Mode.Progress;
			progressBar = panel.Get<ProgressBarWidget>("PROGRESS_BAR");
			progressLabel = panel.Get<LabelWidget>("PROGRESS_MESSAGE");
			progressLabel.IsVisible = () => visible == Mode.Progress;

			// Message view
			messageContainer = panel.Get("MESSAGE");
			messageContainer.IsVisible = () => visible == Mode.Message;
			messageLabel = messageContainer.Get<LabelWidget>("MESSAGE_MESSAGE");

			// List view
			listContainer = panel.Get("LIST");
			listContainer.IsVisible = () => visible == Mode.List;

			listPanel = listContainer.Get<ScrollPanelWidget>("LIST_PANEL");
			listHeaderTemplate = listPanel.Get("LIST_HEADER_TEMPLATE");
			labelListTemplate = listPanel.Get<LabelWidget>("LABEL_LIST_TEMPLATE");
			checkboxListTemplate = listPanel.Get<ContainerWidget>("CHECKBOX_LIST_TEMPLATE");
			listPanel.RemoveChildren();

			listLabel = listContainer.Get<LabelWidget>("LIST_MESSAGE");

			if (chooseDirectory)
				ChooseContentSource();
			else
				DetectContentSources();
		}

		void ChooseContentSource()
		{
			var operation = Interlocked.Increment(ref activeOperation);
			var prompt = FluentProvider.GetMessage(SelectInstallDirectory);
			ShowProgressbar(prompt, () => prompt);
			ShowBackOnly();

			new Task(() =>
			{
				var selectedDirectory = MacOSDirectorySourceResolver.ChooseDirectory(prompt);
				if (!IsCurrentOperation(operation))
					return;

				if (selectedDirectory == null)
				{
					Game.RunAfterTick(() =>
					{
						if (IsCurrentOperation(operation))
							CancelOperationAndClose();
					});
					return;
				}

				foreach (var kv in sources.Where(source => source.Value.Type.Value == "MacOSDirectory"))
				{
					var path = MacOSDirectorySourceResolver.FindSourcePath(
						kv.Value, new[] { selectedDirectory });
					if (!IsCurrentOperation(operation))
						return;

					if (path != null && PrepareSource(kv.Key, kv.Value, path, operation))
						return;
				}

				Game.RunAfterTick(() =>
				{
					if (!IsCurrentOperation(operation))
						return;

					ShowMessage(prompt, FluentProvider.GetMessage(SelectedDirectoryInvalid));
					ShowBackRetry(ChooseContentSource);
				});
			}).Start();
		}

		void DetectContentSources()
		{
			var operation = Interlocked.Increment(ref activeOperation);
			var message = FluentProvider.GetMessage(DetectingSources);
			ShowProgressbar(FluentProvider.GetMessage(CheckingSources), () => message);
			ShowBackOnly();

			new Task(() =>
			{
				foreach (var kv in sources)
				{
					if (!IsCurrentOperation(operation))
						return;

					message = FluentProvider.GetMessage(SearchingSourceFor, "title", kv.Value.Title);

					var sourceResolver = modData.ObjectCreator.CreateObject<ISourceResolver>($"{kv.Value.Type.Value}SourceResolver");

					var path = sourceResolver.FindSourcePath(kv.Value);
					if (!IsCurrentOperation(operation))
						return;

					if (path != null && PrepareSource(kv.Key, kv.Value, path, operation))
						return;
				}

				var missingSources = content.Packages.Values
					.Where(p => !p.IsInstalled())
					.SelectMany(p => p.Sources)
					.Select(d => sources[d]);

				var gameSources = new HashSet<string>();
				var digitalInstalls = new HashSet<string>();

				foreach (var source in missingSources)
				{
					var sourceResolver = modData.ObjectCreator.CreateObject<ISourceResolver>($"{source.Type.Value}SourceResolver");

					var availability = sourceResolver.GetAvailability();

					if (availability == Availability.GameSource)
						gameSources.Add(source.Title);
					else if (availability == Availability.DigitalInstall)
						digitalInstalls.Add(source.Title);
				}

				var options = new Dictionary<string, IEnumerable<string>>();

				if (gameSources.Count != 0)
					options.Add(FluentProvider.GetMessage(GameSources), gameSources);

				if (digitalInstalls.Count != 0)
					options.Add(FluentProvider.GetMessage(DigitalInstalls), digitalInstalls);

				Game.RunAfterTick(() =>
				{
					if (!IsCurrentOperation(operation))
						return;

					ShowList(FluentProvider.GetMessage(GameContentNotFound), FluentProvider.GetMessage(AlternativeContentSources), options);
					ShowBackRetry(DetectContentSources);
				});
			}).Start();
		}

		bool PrepareSource(string key, ModContent.ModSource source, string path, int operation)
		{
			if (!IsCurrentOperation(operation))
				return false;

			Log.Write("install", $"Using installer `{key}: {source.Title}` of type `{source.Type.Value}`:");

			var packages = content.Packages.Values
				.Where(package => package.Sources.Contains(key) && !package.IsInstalled())
				.ToArray();

			// Ignore the source when all of its content is already installed.
			if (packages.Length == 0)
				return false;

			var selected = packages.ToDictionary(package => package.Identifier, package => package.Required);
			Game.RunAfterTick(() =>
			{
				if (!IsCurrentOperation(operation))
					return;

				availablePackages = packages;
				selectedPackages = selected;
				ShowList(source, FluentProvider.GetMessage(ContentPackageInstallation));
				ShowContinueCancel(() => InstallFromSource(path, source));
			});
			return true;
		}

		bool IsCurrentOperation(int operation)
		{
			return Volatile.Read(ref activeOperation) == operation;
		}

		void CancelOperationAndClose()
		{
			Interlocked.Increment(ref activeOperation);
			Ui.CloseWindow();
		}

		void InstallFromSource(string path, ModContent.ModSource modSource)
		{
			var message = "";
			ShowProgressbar(FluentProvider.GetMessage(InstallingContent), () => message);
			ShowDisabledCancel();

			new Task(() =>
			{
				var extracted = new List<string>();

				try
				{
					void RunSourceActions(MiniYamlNode contentPackageYaml)
					{
						var sourceActionListYaml = contentPackageYaml.Value.NodeWithKeyOrDefault("Actions");
						if (sourceActionListYaml == null)
							return;

						foreach (var sourceActionNode in sourceActionListYaml.Value.Nodes)
						{
							var key = sourceActionNode.Key;
							var split = key.IndexOf('@');
							if (split != -1)
								key = key[..split];
							var sourceAction = modData.ObjectCreator.CreateObject<ISourceAction>($"{key}SourceAction");
							sourceAction.RunActionOnSource(sourceActionNode.Value, path, modData, extracted, m => message = m);
						}
					}

					var beforeInstall = modSource.Install.FirstOrDefault(x => x.Key == "BeforeInstall");
					if (beforeInstall != null)
						RunSourceActions(beforeInstall);

					foreach (var packageInstallationNode in modSource.Install.Where(
						x => x.Key == "ContentPackage" || x.Key.StartsWith("ContentPackage@", StringComparison.Ordinal)))
					{
						var packageName = packageInstallationNode.Value.NodeWithKeyOrDefault("Name")?.Value.Value;
						if (!string.IsNullOrEmpty(packageName) && selectedPackages.TryGetValue(packageName, out var required) && required)
							RunSourceActions(packageInstallationNode);
					}

					var afterInstall = modSource.Install.FirstOrDefault(x => x.Key == "AfterInstall");
					if (afterInstall != null)
						RunSourceActions(afterInstall);

					Game.RunAfterTick(Ui.CloseWindow);
				}
				catch (Exception e)
				{
					Log.Write("install", e.ToString());

					foreach (var f in extracted)
					{
						Log.Write("install", "Deleting " + f);
						File.Delete(f);
					}

					Game.RunAfterTick(() =>
					{
						ShowMessage(FluentProvider.GetMessage(InstallationFailed), FluentProvider.GetMessage(CheckInstallLog));
						ShowBackRetry(() => InstallFromSource(path, modSource));
					});
				}
			}).Start();
		}

		void ShowMessage(string title, string message)
		{
			visible = Mode.Message;
			titleLabel.GetText = () => title;
			messageLabel.GetText = () => message;

			primaryButton.Bounds.Y += messageContainer.Bounds.Height - panel.Bounds.Height;
			secondaryButton.Bounds.Y += messageContainer.Bounds.Height - panel.Bounds.Height;
			panel.Bounds.Y -= (messageContainer.Bounds.Height - panel.Bounds.Height) / 2;
			panel.Bounds.Height = messageContainer.Bounds.Height;
		}

		void ShowProgressbar(string title, Func<string> getMessage)
		{
			visible = Mode.Progress;
			titleLabel.GetText = () => title;
			progressBar.IsIndeterminate = () => true;

			var font = Game.Renderer.Fonts[progressLabel.Font];
			var status = new CachedTransform<string, string>(s => WidgetUtils.TruncateText(s, progressLabel.Bounds.Width, font));
			progressLabel.GetText = () => status.Update(getMessage());

			primaryButton.Bounds.Y += progressContainer.Bounds.Height - panel.Bounds.Height;
			secondaryButton.Bounds.Y += progressContainer.Bounds.Height - panel.Bounds.Height;
			panel.Bounds.Y -= (progressContainer.Bounds.Height - panel.Bounds.Height) / 2;
			panel.Bounds.Height = progressContainer.Bounds.Height;
		}

		void ShowList(ModContent.ModSource source, string message)
		{
			visible = Mode.List;
			var titleText = source.Title;
			titleLabel.GetText = () => titleText;
			listLabel.GetText = () => message;

			listPanel.RemoveChildren();
			foreach (var package in availablePackages)
			{
				var containerWidget = (ContainerWidget)checkboxListTemplate.Clone();
				var checkboxWidget = containerWidget.Get<CheckboxWidget>("PACKAGE_CHECKBOX");
				var title = FluentProvider.GetMessage(package.Title);
				checkboxWidget.GetText = () => title;
				checkboxWidget.IsDisabled = () => package.Required;
				checkboxWidget.IsChecked = () => selectedPackages[package.Identifier];
				checkboxWidget.OnClick = () => selectedPackages[package.Identifier] = !selectedPackages[package.Identifier];

				var contentPackageNode = source.Install.FirstOrDefault(x =>
					x.Value.NodeWithKeyOrDefault("Name")?.Value.Value == package.Identifier);

				var tooltipText = contentPackageNode?.Value.NodeWithKeyOrDefault(nameof(ModContent.ModSource.TooltipText))?.Value.Value;
				var tooltipIcon = containerWidget.Get<ImageWidget>("PACKAGE_INFO");
				tooltipIcon.IsVisible = () => !string.IsNullOrWhiteSpace(tooltipText);
				tooltipIcon.GetTooltipText = () => tooltipText;

				listPanel.AddChild(containerWidget);
			}

			primaryButton.Bounds.Y += listContainer.Bounds.Height - panel.Bounds.Height;
			secondaryButton.Bounds.Y += listContainer.Bounds.Height - panel.Bounds.Height;
			panel.Bounds.Y -= (listContainer.Bounds.Height - panel.Bounds.Height) / 2;
			panel.Bounds.Height = listContainer.Bounds.Height;
		}

		void ShowList(string title, string message, Dictionary<string, IEnumerable<string>> groups)
		{
			visible = Mode.List;
			titleLabel.GetText = () => title;
			listLabel.GetText = () => message;

			listPanel.RemoveChildren();

			foreach (var kv in groups)
			{
				if (kv.Value.Any())
				{
					var groupTitle = kv.Key;
					var headerWidget = listHeaderTemplate.Clone();
					var headerTitleWidget = headerWidget.Get<LabelWidget>("LABEL");
					headerTitleWidget.GetText = () => groupTitle;
					listPanel.AddChild(headerWidget);
				}

				foreach (var i in kv.Value)
				{
					var item = i;
					var labelWidget = (LabelWidget)labelListTemplate.Clone();
					labelWidget.GetText = () => item;
					listPanel.AddChild(labelWidget);
				}
			}

			primaryButton.Bounds.Y += listContainer.Bounds.Height - panel.Bounds.Height;
			secondaryButton.Bounds.Y += listContainer.Bounds.Height - panel.Bounds.Height;
			panel.Bounds.Y -= (listContainer.Bounds.Height - panel.Bounds.Height) / 2;
			panel.Bounds.Height = listContainer.Bounds.Height;
		}

		void ShowContinueCancel(Action continueAction)
		{
			primaryButton.OnClick = continueAction;
			var primaryButtonText = FluentProvider.GetMessage(Continue);
			primaryButton.GetText = () => primaryButtonText;
			primaryButton.Visible = true;

			secondaryButton.OnClick = CancelOperationAndClose;
			var secondaryButtonText = FluentProvider.GetMessage(Cancel);
			secondaryButton.GetText = () => secondaryButtonText;
			secondaryButton.Visible = true;
			secondaryButton.Disabled = false;
			Game.RunAfterTick(Ui.ResetTooltips);
		}

		void ShowBackRetry(Action retryAction)
		{
			primaryButton.OnClick = retryAction;
			var primaryButtonText = FluentProvider.GetMessage(Retry);
			primaryButton.GetText = () => primaryButtonText;
			primaryButton.Visible = true;

			secondaryButton.OnClick = CancelOperationAndClose;
			var secondaryButtonText = FluentProvider.GetMessage(Back);
			secondaryButton.GetText = () => secondaryButtonText;
			secondaryButton.Visible = true;
			secondaryButton.Disabled = false;
			Game.RunAfterTick(Ui.ResetTooltips);
		}

		void ShowBackOnly()
		{
			primaryButton.Visible = false;
			secondaryButton.OnClick = CancelOperationAndClose;
			var secondaryButtonText = FluentProvider.GetMessage(Back);
			secondaryButton.GetText = () => secondaryButtonText;
			secondaryButton.Visible = true;
			secondaryButton.Disabled = false;
			Game.RunAfterTick(Ui.ResetTooltips);
		}

		void ShowDisabledCancel()
		{
			primaryButton.Visible = false;
			secondaryButton.Disabled = true;
			Game.RunAfterTick(Ui.ResetTooltips);
		}
	}
}
