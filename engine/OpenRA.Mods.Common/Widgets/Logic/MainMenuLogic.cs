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
		const int DesktopVerticalMarginPoints = 20;
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
			var referenceCount = Math.Max(6, count);
			var referenceControlsHeight = referenceCount * policy.MinimumTarget +
				Math.Max(0, referenceCount - 1) * policy.Gap;
			var verticalMargin = Math.Max(policy.Gap, Points(DesktopVerticalMarginPoints, policy));
			var desiredPanelHeight = referenceControlsHeight + 2 * verticalMargin;
			var panelHeight = Math.Min(content.Height, desiredPanelHeight);
			var panel = new WidgetBounds(
				Math.Clamp(PreferredPanelX, content.X, content.Right - panelWidth),
				content.Y + Math.Max(0, (content.Height - panelHeight) / 2),
				panelWidth,
				panelHeight);

			var controls = new WidgetBounds[count];
			if (count == 0)
				return new IosMainMenuLayout(panel, controls);

			var x = Math.Max(0, (panel.Width - controlsWidth) / 2);

			// Preserve the complete six-row control span. Fewer controls divide the
			// same space between themselves, while constrained phones reduce only
			// the outer margin before ever shrinking a physical touch target.
			var minimumControlsHeight = count * policy.MinimumTarget +
				Math.Max(0, count - 1) * policy.Gap;
			var appliedMargin = Math.Min(verticalMargin,
				Math.Max(0, (panel.Height - minimumControlsHeight) / 2));
			var referenceControlsSpan = Math.Min(
				Math.Max(1, panel.Height - 2 * appliedMargin), referenceControlsHeight);
			var availableControlsHeight = Math.Max(1,
				referenceControlsSpan - Math.Max(0, count - 1) * policy.Gap);
			var controlHeight = Math.Max(1, availableControlsHeight / count);
			var remainder = Math.Max(0, availableControlsHeight - controlHeight * count);
			var controlsHeight = referenceControlsSpan;
			var y = Math.Max(0, (panel.Height - controlsHeight) / 2);
			for (var i = 0; i < count; i++)
			{
				var height = controlHeight + (i < remainder ? 1 : 0);
				controls[i] = new WidgetBounds(x, y, controlsWidth, height);
				y += height + policy.Gap;
			}

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
		protected enum MenuType { Main, Singleplayer, MapEditor, StartupPrompts, None }

		protected enum MenuPanel { None, Missions, Skirmish, Multiplayer, MapEditor, Replays, GameSaves }

		protected MenuType menuType = MenuType.Main;
		readonly Widget rootMenu;
		readonly ModData modData;
		readonly World world;
		readonly ContentInstallerFileSystemLoader contentInstaller;
		readonly Widget mainMenu;
		readonly Widget[] mainMenuControls;
		readonly ButtonWidget languageButton;
		readonly bool iosMainMenuLayoutEnabled;
		Size lastIosResolution;
		Size lastIosNativePointSize;
		Rectangle lastIosSafeBounds;
		int2 lastIosParentRenderOrigin;
		WidgetBounds lastIosPanelBounds;
		WidgetBounds[] lastIosControlBounds;
		bool iosLayoutInitialized;

		protected static MenuPanel lastGameState = MenuPanel.None;

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
				contentButton.Visible = !Platform.IsIOS;
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

			var aboutButton = mainMenu.GetOrNull<ButtonWidget>("ABOUT_BUTTON");
			var showAboutButton = aboutButton != null && Platform.CurrentPlatform == PlatformType.OSX;
			if (aboutButton != null)
			{
				aboutButton.Visible = showAboutButton;
				if (showAboutButton)
					aboutButton.OnClick = OpenAboutPanel;
			}

			var quitButton = mainMenu.Get<ButtonWidget>("QUIT_BUTTON");
			quitButton.Visible = !Platform.IsIOS;
			if (quitButton.Visible)
				quitButton.OnClick = Game.Exit;

			languageButton = mainMenu.GetOrNull<ButtonWidget>("LANGUAGE_BUTTON");
			if (languageButton != null)
			{
				languageButton.GetText = () => Platform.IsIOS ?
					GetIosLanguageDisplayName(Game.Settings.Game.Language, FluentProvider.CurrentLanguage) :
					LanguageSelectionPolicy.GetDisplayName(
						Game.Settings.Game.Language, FluentProvider.CurrentLanguage);
				languageButton.OnClick = () =>
				{
					var currentMod = modData.Manifest.Id;
					ApplyLanguageSelection(
						Game.Settings.Game,
						Game.Settings.SystemLanguageTag,
						FluentProvider.CurrentLanguage,
						NextLanguagePreference(Game.Settings.Game.Language),
						Game.Settings.Save,
						() => Game.RunAfterTick(() => Game.InitializeMod(currentMod, new Arguments())));
				};
			}

			var controls = new List<Widget> { singleplayerButton, multiplayerButton, settingsButton };
			if (languageButton != null)
				controls.Add(languageButton);

			if (contentButton != null && contentButton.Visible)
				controls.Add(contentButton);

			if (aboutButton != null)
			{
				if (showAboutButton)
					controls.Add(aboutButton);
				else if (quitButton.Visible)
					quitButton.Bounds.Y = aboutButton.Bounds.Y;
			}

			if (quitButton.Visible)
				controls.Add(quitButton);
			mainMenuControls = controls.ToArray();
			iosMainMenuLayoutEnabled = languageButton != null && contentButton != null &&
				mainMenuControls.Length == 4;
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

			Game.OnRemoteDirectConnect += OnRemoteDirectConnect;

			menuType = MenuType.StartupPrompts;

			void OnIntroductionComplete()
			{
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

		public static string GetIosLanguageDisplayName(string preference, string uiLanguage)
		{
			if (LanguageSelectionPolicy.NormalizePreference(preference) ==
				LanguageSelectionPolicy.SystemPreference)
				return LanguageSelectionPolicy.Resolve(preference, uiLanguage) ==
					LanguageSelectionPolicy.SimplifiedChinese ? "自动" : "Auto";

			return LanguageSelectionPolicy.GetDisplayName(preference, uiLanguage);
		}

		public static string NextLanguagePreference(string preference)
		{
			var preferences = LanguageSelectionPolicy.SupportedPreferences;
			var normalized = LanguageSelectionPolicy.NormalizePreference(preference);
			var index = 0;
			for (var i = 0; i < preferences.Count; i++)
				if (preferences[i] == normalized)
				{
					index = i;
					break;
				}

			return preferences[(index + 1) % preferences.Count];
		}

		void ApplyIosMainMenuLayout()
		{
			if (!Platform.IsIOS || !iosMainMenuLayoutEnabled)
				return;

			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var layout = IosMainMenuLayout.Create(policy, mainMenuControls.Length);
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

		void OpenAboutPanel()
		{
			var aboutPanel = Game.OpenWindow("ABOUT_PANEL", new WidgetArgs());
			aboutPanel.Get<ButtonWidget>("BACK_BUTTON").OnClick = () =>
			{
				Ui.CloseWindow();
				SwitchMenu(MenuType.Main);
			};

			SwitchMenu(MenuType.None);
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
					Game.JoinReplay(argument);
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
			var savePath = Path.Combine(
				Platform.SupportDir,
				"Saves",
				modData.Manifest.Id,
				modData.Manifest.Metadata.Version,
				saveFilename);

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

			var map = modData.MapCache[save.GlobalSettings.Map];
			if (map.Status != MapStatus.Available)
				return;

			var orders = new List<Order>()
			{
				Order.FromTargetString("LoadGameSave", saveFilename, true),
				Order.Command($"state {Session.ClientState.Ready}")
			};

			Game.CreateAndStartLocalServer(map.Uid, orders);
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
