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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenRA.Mods.Common.FileSystem;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public readonly struct IosMainMenuLayout
	{
		const int DesktopPanelWidth = 320;
		const int DesktopControlWidth = 264;
		const int SingleplayerPanelHeight = 420;
		const int DesktopVerticalMarginPoints = 12;
		const int PreferredPanelX = 72;

		public WidgetBounds Panel { get; }
		public WidgetBounds[] Controls { get; }

		IosMainMenuLayout(WidgetBounds panel, WidgetBounds[] controls)
		{
			Panel = panel;
			Controls = controls;
		}

		public static IosMainMenuLayout Create(IosMenuLayoutPolicy policy, int controlCount = 6)
		{
			if (policy == null)
				throw new ArgumentNullException(nameof(policy));

			var count = Math.Max(0, controlCount);
			var content = policy.ContentBounds;
			var desiredControlsWidth = policy.IsPhone ?
				Math.Max(DesktopControlWidth, 4 * policy.MinimumTarget) : DesktopControlWidth;
			var desiredPanelWidth = policy.IsPhone ?
				Math.Max(DesktopPanelWidth, desiredControlsWidth + 2 * policy.Gap) : DesktopPanelWidth;
			var panelWidth = Math.Min(content.Width,
				Math.Max(policy.MinimumTarget, desiredPanelWidth));
			var controlsWidth = Math.Min(
				Math.Max(1, panelWidth - 2 * policy.Gap), desiredControlsWidth);
			var controlsHeight = count * policy.MinimumTarget + Math.Max(0, count - 1) * policy.Gap;
			var desiredPanelHeight = Math.Max(SingleplayerPanelHeight,
				controlsHeight + 2 * Math.Max(policy.Gap, Points(DesktopVerticalMarginPoints, policy)));
			var panelHeight = Math.Min(content.Height, desiredPanelHeight);
			var panel = new WidgetBounds(
				Math.Clamp(PreferredPanelX, content.X, content.Right - panelWidth),
				content.Y + Math.Max(0, (content.Height - panelHeight) / 2),
				panelWidth,
				panelHeight);
			var x = Math.Max(0, (panel.Width - controlsWidth) / 2);
			var y = Math.Max(0, (panel.Height - controlsHeight) / 2);
			var controls = new WidgetBounds[count];
			for (var i = 0; i < count; i++)
				controls[i] = new WidgetBounds(
					x, y + i * (policy.MinimumTarget + policy.Gap), controlsWidth, policy.MinimumTarget);

			return new IosMainMenuLayout(panel, controls);
		}

		public static bool MatchesAppliedBounds(
			WidgetBounds expectedPanel,
			WidgetBounds actualPanel,
			IReadOnlyList<WidgetBounds> expectedControls,
			IReadOnlyList<Widget> actualControls)
		{
			if (!SameBounds(expectedPanel, actualPanel) || expectedControls == null || actualControls == null ||
				expectedControls.Count != actualControls.Count)
				return false;

			for (var i = 0; i < expectedControls.Count; i++)
				if (!SameBounds(expectedControls[i], actualControls[i].Bounds))
					return false;

			return true;
		}

		static int Points(int points, IosMenuLayoutPolicy policy) =>
			Math.Max(1, (int)Math.Ceiling(points * policy.LogicalPerPoint));

		static bool SameBounds(WidgetBounds first, WidgetBounds second) =>
			first.X == second.X && first.Y == second.Y &&
			first.Width == second.Width && first.Height == second.Height;
	}

	public class MainMenuLogic : ChromeLogic
	{
		[FluentReference]
		const string LoadingNews = "label-loading-news";

		[FluentReference("message")]
		const string NewsRetrivalFailed = "label-news-retrieval-failed";

		[FluentReference("message")]
		const string NewsParsingFailed = "label-news-parsing-failed";

		[FluentReference("author", "datetime")]
		const string AuthorDateTime = "label-author-datetime";

		[FluentReference("kind")]
		const string RuntimePersistenceMismatch = "notification-runtime-persistence-mismatch";

		[FluentReference("kind")]
		const string RuntimePersistenceLegacyHidden = "notification-runtime-persistence-legacy-hidden";

		[FluentReference]
		const string RuntimePersistenceSavedGameKind = "notification-runtime-persistence-kind-saved-game";

		[FluentReference]
		const string RuntimePersistenceReplayKind = "notification-runtime-persistence-kind-replay";

		static readonly UTF8Encoding StrictUtf8 = new(false, true);

		protected enum MenuType { Main, Singleplayer, MapEditor, StartupPrompts, None }

		protected enum MenuPanel { None, Missions, Skirmish, Multiplayer, MapEditor, Replays, GameSaves }

		protected MenuType menuType = MenuType.Main;
		readonly Widget rootMenu;
		readonly ScrollPanelWidget newsPanel;
		readonly int maxNewsHeight;
		readonly Widget newsTemplate;
		readonly LabelWidget newsStatus;
		readonly ModData modData;
		readonly World world;
		readonly ContentInstallerFileSystemLoader contentInstaller;
		readonly Widget mainMenu;
		readonly Widget[] mainMenuControls;
		readonly DropDownButtonWidget languageDropdown;
		readonly bool iosMainMenuLayoutEnabled;
		Size lastIosResolution;
		Size lastIosNativePointSize;
		Rectangle lastIosSafeBounds;
		int2 lastIosParentRenderOrigin;
		WidgetBounds lastIosPanelBounds;
		WidgetBounds[] lastIosControlBounds;
		bool iosLayoutInitialized;

		// Update news once per game launch
		static bool fetchedNews;

		protected static MenuPanel lastGameState = MenuPanel.None;

		bool newsOpen;

		void SwitchMenu(MenuType type)
		{
			menuType = type;

			DiscordService.UpdateStatus(DiscordState.InMenu);

			// Update button mouseover
			Game.RunAfterTick(Ui.ResetTooltips);
		}

		[ObjectCreator.UseCtor]
		public MainMenuLogic(Widget widget, World world, ModData modData)
		{
			this.modData = modData;

			rootMenu = widget;

			// Menu buttons
			mainMenu = widget.Get("MAIN_MENU");
			mainMenu.IsVisible = () => menuType == MenuType.Main;

			var singleplayerButton = mainMenu.Get<ButtonWidget>("SINGLEPLAYER_BUTTON");
			singleplayerButton.OnClick = () => SwitchMenu(MenuType.Singleplayer);

			var multiplayerButton = mainMenu.Get<ButtonWidget>("MULTIPLAYER_BUTTON");
			multiplayerButton.OnClick = OpenMultiplayerPanel;

			var contentButton = mainMenu.GetOrNull<ButtonWidget>("CONTENT_BUTTON");
			contentInstaller = modData.FileSystemLoader as ContentInstallerFileSystemLoader;
			this.world = world;

			if (contentButton != null)
			{
				contentButton.IsVisible = () => !Platform.IsIOS;
				contentButton.Disabled = contentInstaller == null;
				contentButton.OnClick = () =>
				{
					// Switching mods changes the world state (by disposing it),
					// so we can't do this inside the input handler.
					Game.RunAfterTick(() =>
					{
						if (contentInstaller != null)
							Game.InitializeMod(contentInstaller.ContentInstallerMod, new Arguments());
					});
				};
			}

			var settingsButton = mainMenu.Get<ButtonWidget>("SETTINGS_BUTTON");
			settingsButton.OnClick = OpenSettingsPanel;

			var quitButton = mainMenu.Get<ButtonWidget>("QUIT_BUTTON");
			quitButton.OnClick = Game.Exit;

			// iOS applications are exited with the system Home gesture. Ending the engine
			// loop from this button only exposes the native launch wallpaper while leaving
			// the application process alive, so do not offer that misleading action.
			quitButton.IsVisible = () => !Platform.IsIOS;

			languageDropdown = mainMenu.GetOrNull<DropDownButtonWidget>("LANGUAGE_DROPDOWN");
			if (languageDropdown != null)
			{
				languageDropdown.IsVisible = () => !Platform.IsIOS;
				languageDropdown.GetText = () => LanguageSelectionPolicy.GetDisplayName(
					Game.Settings.Game.Language, FluentProvider.CurrentLanguage);
				languageDropdown.OnMouseDown = _ => ShowLanguageDropdown(languageDropdown);
			}

			var controls = new List<Widget> { singleplayerButton, multiplayerButton, settingsButton };
			if (!Platform.IsIOS && languageDropdown != null)
				controls.Add(languageDropdown);

			if (!Platform.IsIOS && contentButton != null)
				controls.Add(contentButton);

			if (!Platform.IsIOS)
				controls.Add(quitButton);

			mainMenuControls = controls.ToArray();
			iosMainMenuLayoutEnabled = languageDropdown != null && contentButton != null;
			ApplyIosMainMenuLayout();

			// Singleplayer menu
			var singleplayerMenu = widget.Get("SINGLEPLAYER_MENU");
			singleplayerMenu.IsVisible = () => menuType == MenuType.Singleplayer;

			var missionsButton = singleplayerMenu.Get<ButtonWidget>("MISSIONS_BUTTON");
			missionsButton.OnClick = () => OpenMissionBrowserPanel(modData.MapCache.PickLastModifiedMap(MapVisibility.MissionSelector));

			var hasCampaign = modData.Manifest.Missions.Length > 0;
			var hasMissions = modData.MapCache
				.Any(p => p.Status == MapStatus.Available && p.Visibility.HasFlag(MapVisibility.MissionSelector));

			missionsButton.Disabled = !hasCampaign && !hasMissions;

			var hasMaps = modData.MapCache.Any(p => p.Visibility.HasFlag(MapVisibility.Lobby));
			var skirmishButton = singleplayerMenu.Get<ButtonWidget>("SKIRMISH_BUTTON");
			skirmishButton.OnClick = StartSkirmishGame;
			skirmishButton.Disabled = !hasMaps;

			var loadButton = singleplayerMenu.Get<ButtonWidget>("LOAD_BUTTON");
			loadButton.IsDisabled = () => !GameSaveBrowserLogic.IsLoadPanelEnabled(modData.Manifest);
			loadButton.OnClick = OpenGameSaveBrowserPanel;

			var encyclopediaButton = singleplayerMenu.GetOrNull<ButtonWidget>("ENCYCLOPEDIA_BUTTON");
			if (encyclopediaButton != null)
				encyclopediaButton.OnClick = OpenEncyclopediaPanel;

			singleplayerMenu.Get<ButtonWidget>("BACK_BUTTON").OnClick = () => SwitchMenu(MenuType.Main);

			// Map editor menu
			var mapEditorMenu = widget.Get("MAP_EDITOR_MENU");
			mapEditorMenu.IsVisible = () => menuType == MenuType.MapEditor;

			// Loading into the map editor
			Game.BeforeGameStart += RemoveShellmapUI;

			var onSelect = new Action<string>(uid =>
			{
				if (modData.MapCache[uid].Status != MapStatus.Available)
					SwitchMenu(MenuType.Main);
				else
					LoadMapIntoEditor(modData.MapCache[uid].Uid);
			});

			var newMapButton = widget.Get<ButtonWidget>("NEW_MAP_BUTTON");
			newMapButton.OnClick = () =>
			{
				SwitchMenu(MenuType.None);
				Game.OpenWindow("NEW_MAP_BG", new WidgetArgs()
				{
					{ "onSelect", onSelect },
					{ "onExit", () => SwitchMenu(MenuType.MapEditor) }
				});
			};

			var loadMapButton = widget.Get<ButtonWidget>("LOAD_MAP_BUTTON");
			loadMapButton.OnClick = () =>
			{
				SwitchMenu(MenuType.None);
				Game.OpenWindow("MAPCHOOSER_PANEL", new WidgetArgs()
				{
					{ "initialMap", null },
					{ "remoteMapPool", null },
					{ "initialTab", MapClassification.User },
					{ "onExit", () => SwitchMenu(MenuType.MapEditor) },
					{ "onSelect", onSelect },
					{ "filter", MapVisibility.Lobby | MapVisibility.Shellmap | MapVisibility.MissionSelector },
				});
			};

			loadMapButton.Disabled = !hasMaps;

			mapEditorMenu.Get<ButtonWidget>("BACK_BUTTON").OnClick = () => SwitchMenu(MenuType.Main);

			var newsBG = widget.GetOrNull("NEWS_BG");
			if (newsBG != null)
			{
				newsBG.IsVisible = () => Game.Settings.Game.FetchNews && menuType != MenuType.None && menuType != MenuType.StartupPrompts;

				newsPanel = Ui.LoadWidget<ScrollPanelWidget>("NEWS_PANEL", null, new WidgetArgs());
				newsTemplate = newsPanel.Get("NEWS_ITEM_TEMPLATE");
				newsPanel.RemoveChild(newsTemplate);
				maxNewsHeight = newsPanel.Bounds.Height;

				newsStatus = newsPanel.Get<LabelWidget>("NEWS_STATUS");
				SetNewsStatus(FluentProvider.GetMessage(LoadingNews));
			}

			Game.OnRemoteDirectConnect += OnRemoteDirectConnect;

			// Check for updates in the background
			var webServices = modData.Manifest.Get<WebServices>();
			if (Game.Settings.Debug.CheckVersion)
				webServices.CheckModVersion();

			var updateLabel = rootMenu.GetOrNull("UPDATE_NOTICE");
			if (updateLabel != null)
				updateLabel.IsVisible = () => !newsOpen && menuType != MenuType.None &&
					menuType != MenuType.StartupPrompts &&
					webServices.ModVersionStatus == ModVersionStatus.Outdated;

			menuType = MenuType.StartupPrompts;

			void OnIntroductionComplete()
			{
				LoadAndDisplayNews(webServices, newsBG);
				SwitchMenu(MenuType.Main);
				LaunchIntoRequestedPanel();
			}

			if (IntroductionPromptLogic.ShouldShowPrompt())
			{
				Game.OpenWindow("MAINMENU_INTRODUCTION_PROMPT", new WidgetArgs
				{
					{ "onComplete", OnIntroductionComplete }
				});
			}
			else
				OnIntroductionComplete();

			Game.OnShellmapLoaded += OpenMenuBasedOnLastGame;

			DiscordService.UpdateStatus(DiscordState.InMenu);
		}

		public static bool ApplyLanguageSelection(
			GameSettings settings,
			string systemLanguageTag,
			string appliedLanguage,
			string selectedPreference,
			Action save,
			Action scheduleReload)
		{
			if (settings == null)
				throw new ArgumentNullException(nameof(settings));
			if (save == null)
				throw new ArgumentNullException(nameof(save));
			if (scheduleReload == null)
				throw new ArgumentNullException(nameof(scheduleReload));

			settings.Language = LanguageSelectionPolicy.NormalizePreference(selectedPreference);
			save();
			var requiresReload = LanguageSelectionPolicy.RequiresReload(
				appliedLanguage, settings.Language, systemLanguageTag);
			if (requiresReload)
				scheduleReload();

			return requiresReload;
		}

		void ShowLanguageDropdown(DropDownButtonWidget dropdown)
		{
			var preferences = LanguageSelectionPolicy.SupportedPreferences;

			ScrollItemWidget SetupItem(string preference, ScrollItemWidget itemTemplate)
			{
				var normalizedPreference = LanguageSelectionPolicy.NormalizePreference(preference);
				var item = ScrollItemWidget.Setup(itemTemplate,
					() => LanguageSelectionPolicy.NormalizePreference(Game.Settings.Game.Language) == normalizedPreference,
					() =>
					{
						var currentMod = modData.Manifest.Id;
						ApplyLanguageSelection(
							Game.Settings.Game,
							Game.Settings.SystemLanguageTag,
							FluentProvider.CurrentLanguage,
							normalizedPreference,
							Game.Settings.Save,
							() => Game.RunAfterTick(() => Game.InitializeMod(currentMod, new Arguments())));
					});

				item.Get<LabelWidget>("LABEL").GetText = () =>
					LanguageSelectionPolicy.GetDisplayName(normalizedPreference, FluentProvider.CurrentLanguage);
				return item;
			}

			dropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", 500, preferences, SetupItem);
		}

		void ApplyIosMainMenuLayout()
		{
			if (!Platform.IsIOS || !iosMainMenuLayoutEnabled)
				return;

			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var layout = IosMainMenuLayout.Create(policy, mainMenuControls.Length);
			languageDropdown.RemovePanel();
			var parentRenderOrigin = mainMenu.Parent.RenderOrigin;
			var relativePanel = IosTouchWidgetPolicy.RelativeToRoot(
				layout.Panel.ToRectangle(), parentRenderOrigin);
			mainMenu.Bounds = new WidgetBounds(
				relativePanel.X, relativePanel.Y, relativePanel.Width, relativePanel.Height);
			for (var i = 0; i < mainMenuControls.Length; i++)
				mainMenuControls[i].Bounds = layout.Controls[i];

			IosTouchMenuLogic.ApplyIosFonts(mainMenu);

			lastIosResolution = resolution;
			lastIosNativePointSize = snapshot.NativePointSize;
			lastIosSafeBounds = snapshot.SafeBounds;
			lastIosParentRenderOrigin = parentRenderOrigin;
			lastIosPanelBounds = mainMenu.Bounds;
			lastIosControlBounds = new WidgetBounds[mainMenuControls.Length];
			for (var i = 0; i < mainMenuControls.Length; i++)
				lastIosControlBounds[i] = mainMenuControls[i].Bounds;

			iosLayoutInitialized = true;
		}

		public override void Tick()
		{
			if (!Platform.IsIOS || !iosMainMenuLayoutEnabled)
				return;

			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			if (!iosLayoutInitialized || resolution != lastIosResolution ||
				snapshot.NativePointSize != lastIosNativePointSize || snapshot.SafeBounds != lastIosSafeBounds ||
				mainMenu.Parent.RenderOrigin != lastIosParentRenderOrigin ||
				!IosMainMenuLayout.MatchesAppliedBounds(
					lastIosPanelBounds, mainMenu.Bounds,
					lastIosControlBounds, mainMenuControls))
				ApplyIosMainMenuLayout();
		}

		void LoadAndDisplayNews(WebServices webServices, Widget newsBG)
		{
			if (newsBG != null && Game.Settings.Game.FetchNews)
			{
				var cacheFile = Path.Combine(Platform.SupportDir, webServices.GameNewsFileName);
				var currentNews = ParseNews(cacheFile);
				if (currentNews != null)
					DisplayNews(currentNews);

				var newsButton = newsBG.GetOrNull<DropDownButtonWidget>("NEWS_BUTTON");
				if (newsButton != null)
				{
					if (!fetchedNews)
					{
						Task.Run(async () =>
						{
							try
							{
								var client = HttpClientFactory.Create();

								// Send the mod and engine version to support version-filtered news (update prompts)
								var url = new HttpQueryBuilder(webServices.GameNews)
								{
									{ "version", Game.EngineVersion },
									{ "mod", modData.Manifest.Id },
									{ "modversion", modData.Manifest.Metadata.Version }
								}.ToString();

								var response = await client.GetStringAsync(url);
								await File.WriteAllTextAsync(cacheFile, response);

								Game.RunAfterTick(() => // run on the main thread
								{
									fetchedNews = true;
									var newNews = ParseNews(cacheFile);
									if (newNews == null)
										return;

									DisplayNews(newNews);

									if (currentNews == null || newNews.Any(n => !currentNews.Select(c => c.DateTime).Contains(n.DateTime)))
										OpenNewsPanel(newsButton);
								});
							}
							catch (Exception e)
							{
								Game.RunAfterTick(() => // run on the main thread
									SetNewsStatus(FluentProvider.GetMessage(NewsRetrivalFailed, "message", e.Message)));
							}
						});
					}

					newsButton.OnClick = () => OpenNewsPanel(newsButton);
				}
			}
		}

		void OpenNewsPanel(DropDownButtonWidget button)
		{
			newsOpen = true;
			button.AttachPanel(newsPanel, () => newsOpen = false);
		}

		void OnRemoteDirectConnect(ConnectionTarget endpoint)
		{
			SwitchMenu(MenuType.None);
			Ui.OpenWindow("MULTIPLAYER_PANEL", new WidgetArgs
			{
				{ "onStart", RemoveShellmapUI },
				{ "onExit", () => SwitchMenu(MenuType.Main) },
				{ "directConnectEndPoint", endpoint },
			});
		}

		static void LoadMapIntoEditor(string uid)
		{
			Game.LoadEditor(uid);

			DiscordService.UpdateStatus(DiscordState.InMapEditor);

			lastGameState = MenuPanel.MapEditor;
		}

		void SetNewsStatus(string message)
		{
			message = WidgetUtils.WrapText(message, newsStatus.Bounds.Width, Game.Renderer.Fonts[newsStatus.Font]);
			newsStatus.GetText = () => message;
		}

		sealed class NewsItem
		{
			public string Title;
			public string Author;
			public DateTime DateTime;
			public string Content;
		}

		NewsItem[] ParseNews(string path)
		{
			if (!File.Exists(path))
				return null;

			try
			{
				return MiniYaml.FromFile(path).Select(node =>
				{
					var nodesDict = node.Value.ToDictionary();
					return new NewsItem
					{
						Title = nodesDict["Title"].Value,
						Author = nodesDict["Author"].Value,
						DateTime = FieldLoader.GetValue<DateTime>("DateTime", node.Key),
						Content = nodesDict["Content"].Value
					};
				}).ToArray();
			}
			catch (Exception ex)
			{
				SetNewsStatus(FluentProvider.GetMessage(NewsParsingFailed, "message", ex.Message));
			}

			return null;
		}

		void DisplayNews(IEnumerable<NewsItem> newsItems)
		{
			newsPanel.RemoveChildren();
			SetNewsStatus("");

			foreach (var item in newsItems)
			{
				var newsItem = newsTemplate.Clone();

				var titleLabel = newsItem.Get<LabelWidget>("TITLE");
				titleLabel.GetText = () => item.Title;

				var authorDateTimeLabel = newsItem.Get<LabelWidget>("AUTHOR_DATETIME");
				var authorDateTime = FluentProvider.GetMessage(AuthorDateTime,
					"author", item.Author,
					"datetime", item.DateTime.ToLocalTime().ToString(CultureInfo.CurrentCulture));

				authorDateTimeLabel.GetText = () => authorDateTime;

				var contentLabel = newsItem.Get<LabelWidget>("CONTENT");
				var content = item.Content.Replace("\\n", "\n");
				content = WidgetUtils.WrapText(content, contentLabel.Bounds.Width, Game.Renderer.Fonts[contentLabel.Font]);
				contentLabel.GetText = () => content;
				contentLabel.Bounds.Height = Game.Renderer.Fonts[contentLabel.Font].Measure(content).Y;
				newsItem.Bounds.Height += contentLabel.Bounds.Height;

				newsPanel.AddChild(newsItem);
				newsPanel.Layout.AdjustChildren();
				newsPanel.Bounds.Height = Math.Min(newsPanel.ContentHeight, maxNewsHeight);
			}
		}

		void RemoveShellmapUI()
		{
			rootMenu.Parent.RemoveChild(rootMenu);
		}

		void StartSkirmishGame()
		{
			SwitchMenu(MenuType.None);

			var map = modData.MapCache.ChooseInitialMap(modData.MapCache.PickLastModifiedMap(MapVisibility.Lobby) ?? Game.Settings.Server.Map, Game.CosmeticRandom);
			Game.Settings.Server.Map = map;
			Game.Settings.Save();

			ConnectionLogic.Connect(Game.CreateLocalServer(map, isSkirmish: true),
				"",
				OpenSkirmishLobbyPanel,
				() => { Game.CloseServer(); SwitchMenu(MenuType.Main); });
		}

		void OpenMissionBrowserPanel(string map)
		{
			SwitchMenu(MenuType.None);
			Game.OpenWindow("MISSIONBROWSER_PANEL", new WidgetArgs
			{
				{ "onExit", () => { Game.Disconnect(); SwitchMenu(MenuType.Singleplayer); } },
				{ "onStart", () => { RemoveShellmapUI(); lastGameState = MenuPanel.Missions; } },
				{ "initialMap", map }
			});
		}

		void OpenEncyclopediaPanel()
		{
			SwitchMenu(MenuType.None);
			Game.OpenWindow("ENCYCLOPEDIA_PANEL", new WidgetArgs
			{
				{ "onExit", () => SwitchMenu(MenuType.Singleplayer) }
			});
		}

		void OpenSkirmishLobbyPanel()
		{
			SwitchMenu(MenuType.None);
			Game.OpenWindow("SERVER_LOBBY", new WidgetArgs
			{
				{ "onExit", () => { Game.Disconnect(); SwitchMenu(MenuType.Singleplayer); } },
				{ "onStart", () => { RemoveShellmapUI(); lastGameState = MenuPanel.Skirmish; } },
				{ "skirmishMode", true }
			});
		}

		void OpenMultiplayerPanel()
		{
			SwitchMenu(MenuType.None);
			Ui.OpenWindow("MULTIPLAYER_PANEL", new WidgetArgs
			{
				{ "onStart", () => { RemoveShellmapUI(); lastGameState = MenuPanel.Multiplayer; } },
				{ "onExit", () => SwitchMenu(MenuType.Main) },
				{ "directConnectEndPoint", null },
			});
		}

		void OpenReplayBrowserPanel()
		{
			SwitchMenu(MenuType.None);
			Ui.OpenWindow("REPLAYBROWSER_PANEL", new WidgetArgs
			{
				{ "onExit", () => SwitchMenu(MenuType.Main) },
				{ "onStart", () => { RemoveShellmapUI(); lastGameState = MenuPanel.Replays; } }
			});
		}

		void OpenGameSaveBrowserPanel()
		{
			SwitchMenu(MenuType.None);
			Ui.OpenWindow("GAMESAVE_BROWSER_PANEL", new WidgetArgs
			{
				{ "onExit", () => SwitchMenu(MenuType.Singleplayer) },
				{ "onStart", () => { RemoveShellmapUI(); lastGameState = MenuPanel.GameSaves; } },
				{ "isSavePanel", false },
				{ "world", null }
			});
		}

		void OpenSettingsPanel()
		{
			SwitchMenu(MenuType.None);
			Game.OpenWindow("SETTINGS_PANEL", new WidgetArgs
			{
				{ "onExit", () => SwitchMenu(MenuType.Main) }
			});
		}

		void LaunchIntoRequestedPanel()
		{
			var target = Game.Settings.Game.LaunchInto;
			if (string.IsNullOrEmpty(target))
				return;

			Game.Settings.Game.LaunchInto = "";

			// Local patch: support deep-link targets of the form "<command> <argument>"
			// so external launchers can jump straight into a specific replay or saved game.
			var separator = target.IndexOf(' ');
			var command = separator < 0 ? target.ToLowerInvariant() : target.Substring(0, separator).ToLowerInvariant();
			var argument = separator < 0 ? null : target.Substring(separator + 1);

			switch (command)
			{
				case "replay" when !string.IsNullOrEmpty(argument):
					try
					{
						Game.JoinReplay(argument);
					}
					catch (RuntimeContractCompatibilityException ex)
					{
						Log.Write("debug", ex.ToString());
						NotifyRuntimePersistenceFailure(
							ex.Reason, RuntimePersistenceReplayKind);
					}
					catch (Exception ex)
					{
						Log.Write("debug", ex.ToString());
					}

					break;
				case "loadsave" when !string.IsNullOrEmpty(argument):
					LoadSavedGame(argument);
					break;
				case "skirmish" when !string.IsNullOrEmpty(argument):
					StartSkirmishGameDeepLink(argument);
					break;
				case "connect" when !string.IsNullOrEmpty(argument):
					ConnectToServerDeepLink(argument);
					break;
				case "skirmish":
					StartSkirmishGame();
					break;
				case "multiplayer":
					OpenMultiplayerPanel();
					break;
				case "load":
					OpenGameSaveBrowserPanel();
					break;
				case "settings":
					OpenSettingsPanel();
					break;
				case "replays":
					OpenReplayBrowserPanel();
					break;
				case "missions":
					var hasCampaign = modData.Manifest.Missions.Length > 0;
					var hasMissions = modData.MapCache
						.Any(p => p.Status == MapStatus.Available && p.Visibility.HasFlag(MapVisibility.MissionSelector));
					if (hasCampaign || hasMissions)
						OpenMissionBrowserPanel(modData.MapCache.PickLastModifiedMap(MapVisibility.MissionSelector));

					break;
				case "editor":
					SwitchMenu(MenuType.MapEditor);
					break;
				case "assetbrowser":
					SwitchMenu(MenuType.None);
					Game.OpenWindow("ASSETBROWSER_PANEL", new WidgetArgs
					{
						{ "onExit", () => SwitchMenu(MenuType.Main) }
					});
					break;
				case "credits":
					Game.OpenWindow("CREDITS_PANEL", new WidgetArgs
					{
						{ "onExit", () => SwitchMenu(MenuType.Main) }
					});
					break;
				case "music":
					SwitchMenu(MenuType.None);
					Game.OpenWindow("MUSIC_PANEL", new WidgetArgs
					{
						{ "onExit", () => SwitchMenu(MenuType.Main) },
						{ "world", world }
					});
					break;
				case "content":
					if (contentInstaller != null)
						Game.RunAfterTick(() => Game.InitializeMod(contentInstaller.ContentInstallerMod, new Arguments()));

					break;
			}
		}

		// Local patch: replicate GameSaveBrowserLogic's load flow so LaunchInto
		// can deep-link into a specific saved game ("loadsave <filename>").
		void LoadSavedGame(string saveFilename)
		{
			if (saveFilename == null)
				return;

			string filename;
			try
			{
				filename = Path.GetFileName(saveFilename);
			}
			catch (ArgumentException)
			{
				return;
			}

			if (!string.Equals(saveFilename, filename, StringComparison.Ordinal) ||
				!IsCanonicalFilename(filename))
				return;

			var savePath = Path.Combine(
				RuntimeContractStorage.SavesPath(
					Platform.SupportDir, modData.Manifest.Id,
					modData.Manifest.Metadata.Version, modData.RuntimeContract),
				filename);

			if (!File.Exists(savePath))
				return;

			GameSave save;
			try
			{
				save = new GameSave(savePath);
			}
			catch (Exception)
			{
				return;
			}

			var compatibility = save.RuntimeContractCompatibilityWith(modData.RuntimeContract);
			if (compatibility != RuntimeContractCompatibilityReason.Compatible)
			{
				NotifyRuntimePersistenceFailure(
					compatibility, RuntimePersistenceSavedGameKind);
				return;
			}

			var mapUid = save.GlobalSettings?.Map;
			if (string.IsNullOrEmpty(mapUid))
				return;

			MapPreview map;
			try
			{
				map = modData.MapCache[mapUid];
			}
			catch (Exception ex)
			{
				Log.Write("debug", ex.ToString());
				return;
			}

			if (map.Status != MapStatus.Available)
				return;

			var orders = new List<Order>()
			{
				Order.FromTargetString("LoadGameSave", filename, true),
				Order.Command($"state {Session.ClientState.Ready}")
			};

			Game.CreateAndStartLocalServer(map.Uid, orders);
		}

		static bool IsCanonicalFilename(string filename)
		{
			if (string.IsNullOrEmpty(filename) || filename is "." or ".." ||
				filename.IndexOfAny(new[] { '/', '\\', '\0', ':' }) >= 0 ||
				Path.IsPathFullyQualified(filename))
				return false;

			try
			{
				if (!filename.IsNormalized(NormalizationForm.FormC) ||
					StrictUtf8.GetByteCount(filename) > 255)
					return false;
			}
			catch (ArgumentException)
			{
				return false;
			}

			return filename.All(character =>
				!char.IsControl(character) && character is not '\u2028' and not '\u2029');
		}

		static void NotifyRuntimePersistenceFailure(
			RuntimeContractCompatibilityReason reason, string kind)
		{
			var message = reason == RuntimeContractCompatibilityReason.MissingIdentity ?
				RuntimePersistenceLegacyHidden : RuntimePersistenceMismatch;
			TextNotificationsManager.AddFeedbackLine(
				message, "kind", FluentProvider.GetMessage(kind));
		}

		// Local patch: deep-link straight into a running skirmish, bypassing the lobby UI.
		// Argument format: "<mapUid> <lobby command 1>|<lobby command 2>|..."
		// Lobby commands follow LobbyCommands grammar (option/slot_bot/faction/team/spawn...).
		// A final "state Ready" is appended, which auto-starts local (singleplayer-enabled) games.
		void StartSkirmishGameDeepLink(string argument)
		{
			SwitchMenu(MenuType.None);

			var parts = argument.Split(new[] { ' ' }, 2);
			var mapUid = parts[0];

			// Resolve by UID first, then by package folder / title so external launchers
			// can reference maps without replicating the SHA1-based UID algorithm.
			// PackageName is a full path; match on the final path segment like Game.Initialize.
			var preview = modData.MapCache.FirstOrDefault(p =>
				p.Status == MapStatus.Available &&
				(p.Uid == mapUid ||
				 string.Equals(Path.GetFileName(p.PackageName), mapUid, StringComparison.OrdinalIgnoreCase) ||
				 string.Equals(Path.GetFileNameWithoutExtension(p.PackageName), mapUid, StringComparison.OrdinalIgnoreCase) ||
				 string.Equals(p.Title, mapUid, StringComparison.OrdinalIgnoreCase)));
			if (preview == null)
			{
				// Do not fall back to Settings.Server.Map — that silently starts the wrong
				// (often smaller) map and then lobby commands like slot_bot Multi2 crash.
				Log.Write("debug", $"Skirmish deep-link map not found: '{mapUid}'");
				StartSkirmishGame();
				return;
			}

			mapUid = preview.Uid;

			Game.Settings.Server.Map = mapUid;
			Game.Settings.Save();

			var setupOrders = new List<Order>();
			if (parts.Length > 1)
				foreach (var cmd in parts[1].Split('|'))
					if (!string.IsNullOrWhiteSpace(cmd))
						setupOrders.Add(Order.Command(cmd.Trim()));

			setupOrders.Add(Order.Command($"state {Session.ClientState.Ready}"));
			Game.CreateAndStartLocalServer(mapUid, setupOrders);
		}

		// Local patch: deep-link straight into a multiplayer server lobby, bypassing
		// the server browser UI. Argument format: "<host>" or "<host>:<port>".
		void ConnectToServerDeepLink(string endpoint)
		{
			SwitchMenu(MenuType.None);

			var host = endpoint;
			var port = 1234;
			var idx = endpoint.LastIndexOf(':');
			if (idx > 0 && int.TryParse(endpoint.Substring(idx + 1), out var parsed))
			{
				host = endpoint.Substring(0, idx);
				port = parsed;
			}

			void OnLobbyExit()
			{
				Game.Disconnect();
				SwitchMenu(MenuType.Main);
			}

			ConnectionLogic.Connect(new ConnectionTarget(host, port), "",
				() => Game.OpenWindow("SERVER_LOBBY", new WidgetArgs
				{
					{ "onStart", () => { RemoveShellmapUI(); lastGameState = MenuPanel.Multiplayer; } },
					{ "onExit", (Action)OnLobbyExit },
					{ "skirmishMode", false }
				}),
				() => SwitchMenu(MenuType.Main));
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				Game.OnRemoteDirectConnect -= OnRemoteDirectConnect;
				Game.BeforeGameStart -= RemoveShellmapUI;
			}

			Game.OnShellmapLoaded -= OpenMenuBasedOnLastGame;
			base.Dispose(disposing);
		}

		void OpenMenuBasedOnLastGame()
		{
			switch (lastGameState)
			{
				case MenuPanel.Missions:
					OpenMissionBrowserPanel(null);
					break;

				case MenuPanel.Replays:
					OpenReplayBrowserPanel();
					break;

				case MenuPanel.Skirmish:
					StartSkirmishGame();
					break;

				case MenuPanel.Multiplayer:
					OpenMultiplayerPanel();
					break;

				case MenuPanel.MapEditor:
					SwitchMenu(MenuType.MapEditor);
					break;

				case MenuPanel.GameSaves:
					SwitchMenu(MenuType.Singleplayer);
					break;
			}

			lastGameState = MenuPanel.None;
		}
	}
}
