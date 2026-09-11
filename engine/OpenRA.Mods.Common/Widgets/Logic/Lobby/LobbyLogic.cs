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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class LobbyLogic : ChromeLogic, INotificationHandler<TextNotification>
	{
		[FluentReference]
		const string Add = "options-slot-admin.add-bots";

		[FluentReference]
		const string Remove = "options-slot-admin.remove-bots";

		[FluentReference]
		const string ConfigureBots = "options-slot-admin.configure-bots";

		[FluentReference("count")]
		const string NumberTeams = "options-slot-admin.teams-count";

		[FluentReference]
		const string HumanVsBots = "options-slot-admin.humans-vs-bots";

		[FluentReference]
		const string FreeForAll = "options-slot-admin.free-for-all";

		[FluentReference]
		const string ConfigureTeams = "options-slot-admin.configure-teams";

		[FluentReference]
		const string Back = "button-back";

		[FluentReference]
		const string TeamChat = "button-team-chat";

		[FluentReference]
		const string GeneralChat = "button-general-chat";

		[FluentReference("seconds")]
		const string ChatAvailability = "label-chat-availability";

		[FluentReference]
		const string ChatDisabled = "label-chat-disabled";

		static readonly Action DoNothing = () => { };

		readonly ModData modData;
		readonly Action onStart;
		readonly Action onExit;
		readonly OrderManager orderManager;
		readonly WorldRenderer worldRenderer;
		readonly bool skirmishMode;
		readonly Ruleset modRules;

		enum PanelType { Players, Options, Music, Servers, Kick, ForceStart }
		PanelType panel = PanelType.Players;

		readonly Widget lobby;
		readonly Widget editablePlayerTemplate;
		readonly Widget nonEditablePlayerTemplate;
		readonly Widget emptySlotTemplate;

		readonly ScrollPanelWidget lobbyChatPanel;
		readonly Dictionary<TextNotificationPool, Widget> chatTemplates = new();
		readonly TextFieldWidget chatTextField;
		readonly CachedTransform<int, string> chatAvailableIn;
		readonly string chatDisabled;

		readonly ScrollPanelWidget players;

		readonly Dictionary<string, LobbyFaction> factions = new();

		readonly IColorPickerManagerInfo colorManager;

		readonly TabCompletionLogic tabCompletion = new();

		MapPreview map;
		Session.MapStatus mapStatus;

		bool chatEnabled;
		bool disableTeamChat;
		bool insufficientPlayerSpawns;
		bool teamChat;
		bool updateDiscordStatus = true;
		bool resetOptionsButtonEnabled;
		readonly LobbySafeStartRequestState safeStartState = new LobbySafeStartRequestState(0);
		int startStatusRevision;
		int cachedStartStatusRevision = -1;
		LobbyStartAvailability cachedStartAvailability;
		Dictionary<int, SpawnOccupant> spawnOccupants = new();

		readonly string chatLineSound;
		readonly string playerJoinedSound;
		readonly string playerLeftSound;
		readonly string lobbyOptionChangedSound;
		IosMenuLayoutPolicy iosLayoutPolicy;
		IosLobbyLayout iosLobbyLayout;
		Size iosLayoutResolution;
		Size iosNativePointSize;
		Rectangle iosSafeBounds;

		bool MapIsPlayable => (mapStatus & Session.MapStatus.Playable) == Session.MapStatus.Playable;

		void InvalidateStartStatus()
		{
			startStatusRevision++;
		}

		void ClientMapReadinessChanged(int clientIndex)
		{
			safeStartState.ObserveReadinessChange(clientIndex);
			InvalidateStartStatus();
		}

		void SafeStartRejected(LobbyStartRejection rejection)
		{
			if (safeStartState.TryAcceptRejection(rejection, orderManager.LobbyInfo.GlobalSettings.Map))
				InvalidateStartStatus();
		}

		void StartStatusLobbyInfoChanged()
		{
			safeStartState.ObserveLobbySync(orderManager.LobbyInfo.GlobalSettings.Map);
			InvalidateStartStatus();
		}

		LobbyStartAvailability CurrentStartAvailability()
		{
			if (cachedStartStatusRevision == startStatusRevision)
				return cachedStartAvailability;

			cachedStartAvailability = safeStartState.Rejection != null ?
				safeStartState.Rejection.ToAvailability() :
				LobbyStartAvailability.Evaluate(new LobbyStartInputs
				{
					LobbyInfo = orderManager.LobbyInfo,
					Mode = LobbyStartMode.SafeDirect,
					RequesterIsAdmin = Game.IsHost,
					RequireCurrentMapReadiness = !skirmishMode,
					InsufficientEnabledSpawnPoints = insufficientPlayerSpawns
				}).ApplyLocalMapAvailability(map.Status == MapStatus.Available);
			cachedStartStatusRevision = startStatusRevision;
			return cachedStartAvailability;
		}

		void BeginSafeStart()
		{
			var availability = CurrentStartAvailability();
			if (!availability.CanStart || safeStartState.IsPending)
				return;

			var currentMapUid = orderManager.LobbyInfo.GlobalSettings.Map;
			var request = safeStartState.Begin(currentMapUid);
			InvalidateStartStatus();
			Game.RunAfterDelay(5000, () =>
			{
				if (!disposed && safeStartState.TryTimeout(request.RequestId))
					InvalidateStartStatus();
			});
			orderManager.IssueOrder(Order.Command(request.ToCommand()));
		}

		// Listen for connection failures
		void ConnectionStateChanged(OrderManager om, string password, NetworkConnection connection)
		{
			if (connection.ConnectionState == ConnectionState.NotConnected)
			{
				if (safeStartState.Reset())
					InvalidateStartStatus();

				// Show connection failed dialog
				Ui.CloseWindow();

				void OnConnect()
				{
					Game.OpenWindow("SERVER_LOBBY", new WidgetArgs()
					{
						{ "onExit", onExit },
						{ "onStart", onStart },
						{ "skirmishMode", false }
					});
				}

				Action<string> onRetry = pass => ConnectionLogic.Connect(connection.Target, pass, OnConnect, onExit);

				var switchPanel = CurrentServerSettings.ServerExternalMod != null ? "CONNECTION_SWITCHMOD_PANEL" : "CONNECTIONFAILED_PANEL";
				Ui.OpenWindow(switchPanel, new WidgetArgs()
				{
					{ "orderManager", om },
					{ "connection", connection },
					{ "password", password },
					{ "onAbort", onExit },
					{ "onQuit", null },
					{ "onRetry", onRetry }
				});
			}
		}

		[ObjectCreator.UseCtor]
		internal LobbyLogic(Widget widget, ModData modData, WorldRenderer worldRenderer, OrderManager orderManager,
			Action onExit, Action onStart, bool skirmishMode, Dictionary<string, MiniYaml> logicArgs)
		{
			map = MapCache.UnknownMap;
			lobby = widget;
			this.modData = modData;
			this.orderManager = orderManager;
			this.worldRenderer = worldRenderer;
			this.onStart = onStart;
			this.onExit = onExit;
			this.skirmishMode = skirmishMode;

			// TODO: This needs to be reworked to support per-map tech levels, bots, etc.
			modRules = modData.DefaultRules;

			Game.LobbyInfoChanged += UpdateCurrentMap;
			Game.LobbyInfoChanged += UpdatePlayerList;
			Game.LobbyInfoChanged += UpdateDiscordStatus;
			Game.LobbyInfoChanged += UpdateSpawnOccupants;
			Game.LobbyInfoChanged += UpdateOptions;
			Game.LobbyInfoChanged += StartStatusLobbyInfoChanged;
			Game.BeforeGameStart += OnGameStart;
			Game.ConnectionStateChanged += ConnectionStateChanged;
			orderManager.ClientMapReadinessChanged += ClientMapReadinessChanged;
			orderManager.SafeStartRejected += SafeStartRejected;

			ChromeMetrics.TryGet("ChatLineSound", out chatLineSound);
			ChromeMetrics.TryGet("PlayerJoinedSound", out playerJoinedSound);
			ChromeMetrics.TryGet("PlayerLeftSound", out playerLeftSound);
			ChromeMetrics.TryGet("LobbyOptionChangedSound", out lobbyOptionChangedSound);

			var name = lobby.GetOrNull<LabelWidget>("SERVER_NAME");
			if (name != null)
				name.GetText = () => orderManager.LobbyInfo.GlobalSettings.ServerName;

			var mapContainer = Ui.LoadWidget("MAP_PREVIEW", lobby.Get("MAP_PREVIEW_ROOT"), new WidgetArgs
			{
				{ "orderManager", orderManager },
				{ "getMap", (Func<(MapPreview, Session.MapStatus)>)(() => (map, mapStatus)) },
				{
					"onMouseDown", (Action<MapPreviewWidget, MapPreview, MouseInput>)((preview, mapPreview, mi) =>
						LobbyUtils.SelectSpawnPoint(orderManager, preview, mapPreview, mi))
				},
				{ "getSpawnOccupants", (Func<Dictionary<int, SpawnOccupant>>)(() => spawnOccupants) },
				{ "getDisabledSpawnPoints", (Func<HashSet<int>>)(() => orderManager.LobbyInfo.DisabledSpawnPoints) },
				{ "showUnoccupiedSpawnpoints", true },
				{ "mapUpdatesEnabled", true },
				{
					"onMapUpdate", (Action<string>)(uid =>
					{
						orderManager.IssueOrder(Order.Command("map " + uid));
						Game.Settings.Server.Map = uid;
						Game.Settings.Save();
					})
				},
			});

			mapContainer.IsVisible = () => panel != PanelType.Servers;

			UpdateCurrentMap();

			var playerBin = Ui.LoadWidget("LOBBY_PLAYER_BIN", lobby.Get("TOP_PANELS_ROOT"), new WidgetArgs());
			playerBin.IsVisible = () => panel == PanelType.Players;

			players = playerBin.Get<ScrollPanelWidget>("LOBBY_PLAYERS");
			editablePlayerTemplate = players.Get("TEMPLATE_EDITABLE_PLAYER");
			nonEditablePlayerTemplate = players.Get("TEMPLATE_NONEDITABLE_PLAYER");
			emptySlotTemplate = players.Get("TEMPLATE_EMPTY");
			colorManager = modRules.Actors[SystemActors.World].TraitInfo<IColorPickerManagerInfo>();

			foreach (var f in modRules.Actors[SystemActors.World].TraitInfos<FactionInfo>())
				factions.Add(f.InternalName, new LobbyFaction { Selectable = f.Selectable, Name = f.Name, Side = f.Side, Description = f.Description });

			var gameStarting = false;
			Func<bool> configurationDisabled = () => !Game.IsHost || gameStarting ||
				panel == PanelType.Kick || panel == PanelType.ForceStart || !MapIsPlayable ||
				orderManager.LocalClient == null || orderManager.LocalClient.IsReady;

			var mapButton = lobby.GetOrNull<ButtonWidget>("CHANGEMAP_BUTTON");
			if (mapButton != null)
			{
				mapButton.IsVisible = () => panel != PanelType.Servers;
				mapButton.IsDisabled = () => gameStarting || panel == PanelType.Kick || panel == PanelType.ForceStart ||
					orderManager.LocalClient == null || orderManager.LocalClient.IsReady;
				mapButton.OnClick = () =>
				{
					var onSelect = new Action<string>(uid =>
					{
						// Don't select the same map again, and handle map becoming unavailable
						var status = modData.MapCache[uid].Status;
						if (uid == map.Uid || (status != MapStatus.Available && status != MapStatus.DownloadAvailable))
							return;

						orderManager.IssueOrder(Order.Command("map " + uid));
						Game.Settings.Server.Map = uid;
						Game.Settings.Save();
					});

					// Check for updated maps, if the user has edited a map we'll preselect it for them
					modData.MapCache.UpdateMaps();

					Ui.OpenWindow("MAPCHOOSER_PANEL", new WidgetArgs()
					{
						{ "initialMap", modData.MapCache.PickLastModifiedMap(MapVisibility.Lobby) ?? map.Uid },
						{ "remoteMapPool", orderManager.ServerMapPool },
						{ "initialTab", MapClassification.System },
						{ "onExit", modData.MapCache.UpdateMaps },
						{ "onSelect", Game.IsHost ? onSelect : null },
						{ "filter", MapVisibility.Lobby },
					});
				};
			}

			var slotsButton = lobby.GetOrNull<DropDownButtonWidget>("SLOTS_DROPDOWNBUTTON");
			if (slotsButton != null)
			{
				slotsButton.IsVisible = () => panel != PanelType.Servers && panel != PanelType.Options;
				slotsButton.IsDisabled = () => configurationDisabled() || panel != PanelType.Players ||
					(orderManager.LobbyInfo.Slots.Values.All(s => !s.AllowBots) &&
					!orderManager.LobbyInfo.Slots.Any(s => !s.Value.LockTeam && orderManager.LobbyInfo.ClientInSlot(s.Key) != null));

				slotsButton.OnMouseDown = _ =>
				{
					var botTypes = map.PlayerActorInfo.TraitInfos<IBotInfo>().Select(t => t.Type);
					var options = new Dictionary<string, IEnumerable<DropDownOption>>();

					var botController = orderManager.LobbyInfo.Clients.FirstOrDefault(c => c.IsAdmin);
					if (orderManager.LobbyInfo.Slots.Values.Any(s => s.AllowBots))
					{
						var botOptions = new List<DropDownOption>()
						{
							new()
							{
								Title = FluentProvider.GetMessage(Add),
								IsSelected = () => false,
								OnClick = () =>
								{
									foreach (var slot in orderManager.LobbyInfo.Slots)
									{
										var bot = botTypes.Random(Game.CosmeticRandom);
										var c = orderManager.LobbyInfo.ClientInSlot(slot.Key);
										if (slot.Value.AllowBots && (c == null || c.Bot != null))
											orderManager.IssueOrder(Order.Command($"slot_bot {slot.Key} {botController.Index} {bot}"));
									}
								}
							}
						};

						if (orderManager.LobbyInfo.Clients.Any(c => c.Bot != null))
						{
							botOptions.Add(new DropDownOption()
							{
								Title = FluentProvider.GetMessage(Remove),
								IsSelected = () => false,
								OnClick = () =>
								{
									foreach (var slot in orderManager.LobbyInfo.Slots)
									{
										var c = orderManager.LobbyInfo.ClientInSlot(slot.Key);
										if (c != null && c.Bot != null)
											orderManager.IssueOrder(Order.Command("slot_open " + slot.Value.PlayerReference));
									}
								}
							});
						}

						options.Add(FluentProvider.GetMessage(ConfigureBots), botOptions);
					}

					var teamCount = (orderManager.LobbyInfo.Slots.Count(s => !s.Value.LockTeam && orderManager.LobbyInfo.ClientInSlot(s.Key) != null) + 1) / 2;
					if (teamCount >= 1)
					{
						var teamOptions = Enumerable.Range(2, teamCount - 1).Reverse().Select(d => new DropDownOption
						{
							Title = FluentProvider.GetMessage(NumberTeams, "count", d),
							IsSelected = () => false,
							OnClick = () => orderManager.IssueOrder(Order.Command($"assignteams {d}"))
						}).ToList();

						if (orderManager.LobbyInfo.Slots.Any(s => s.Value.AllowBots))
						{
							teamOptions.Add(new DropDownOption
							{
								Title = FluentProvider.GetMessage(HumanVsBots),
								IsSelected = () => false,
								OnClick = () => orderManager.IssueOrder(Order.Command("assignteams 1"))
							});
						}

						teamOptions.Add(new DropDownOption
						{
							Title = FluentProvider.GetMessage(FreeForAll),
							IsSelected = () => false,
							OnClick = () => orderManager.IssueOrder(Order.Command("assignteams 0"))
						});

						options.Add(FluentProvider.GetMessage(ConfigureTeams), teamOptions);
					}

					ScrollItemWidget SetupItem(DropDownOption option, ScrollItemWidget template)
					{
						var item = ScrollItemWidget.Setup(template, option.IsSelected, option.OnClick);
						item.Get<LabelWidget>("LABEL").GetText = () => option.Title;
						return item;
					}

					slotsButton.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", 175, options, SetupItem);
				};
			}

			var resetOptionsButton = lobby.GetOrNull<ButtonWidget>("RESET_OPTIONS_BUTTON");
			if (resetOptionsButton != null)
			{
				resetOptionsButton.IsVisible = () => panel == PanelType.Options;
				resetOptionsButton.IsDisabled = () => configurationDisabled() || !resetOptionsButtonEnabled;
				resetOptionsButton.OnMouseDown = _ => orderManager.IssueOrder(Order.Command("reset_options"));
			}

			var optionsBin = Ui.LoadWidget("LOBBY_OPTIONS_BIN", lobby.Get("TOP_PANELS_ROOT"), new WidgetArgs()
			{
				{ "orderManager", orderManager },
				{ "getMap", (Func<MapPreview>)(() => map) },
				{ "configurationDisabled", configurationDisabled }
			});

			optionsBin.IsVisible = () => panel == PanelType.Options;

			var musicBin = Ui.LoadWidget("LOBBY_MUSIC_BIN", lobby.Get("TOP_PANELS_ROOT"), new WidgetArgs
			{
				{ "onExit", DoNothing },
				{ "world", worldRenderer.World }
			});
			musicBin.IsVisible = () => panel == PanelType.Music;

			ServerListLogic serverListLogic = null;
			if (!skirmishMode)
			{
				Action<GameServer> doNothingWithServer = _ => { };

				var serversBin = Ui.LoadWidget("LOBBY_SERVERS_BIN", lobby.Get("TOP_PANELS_ROOT"), new WidgetArgs
				{
					{ "onJoin", doNothingWithServer },
				});

				serverListLogic = serversBin.LogicObjects.Select(l => l as ServerListLogic).FirstOrDefault(l => l != null);
				serversBin.IsVisible = () => panel == PanelType.Servers;
			}

			var tabContainer = skirmishMode ? lobby.Get("SKIRMISH_TABS") : lobby.Get("MULTIPLAYER_TABS");
			tabContainer.IsVisible = () => true;

			var optionsTab = tabContainer.Get<ButtonWidget>("OPTIONS_TAB");
			optionsTab.IsHighlighted = () => panel == PanelType.Options;
			optionsTab.IsDisabled = OptionsTabDisabled;
			optionsTab.OnClick = () => panel = PanelType.Options;

			var playersTab = tabContainer.Get<ButtonWidget>("PLAYERS_TAB");
			playersTab.IsHighlighted = () => panel == PanelType.Players;
			playersTab.IsDisabled = () => panel == PanelType.Kick || panel == PanelType.ForceStart;
			playersTab.OnClick = () => panel = PanelType.Players;

			var musicTab = tabContainer.Get<ButtonWidget>("MUSIC_TAB");
			musicTab.IsHighlighted = () => panel == PanelType.Music;
			musicTab.IsDisabled = () => panel == PanelType.Kick || panel == PanelType.ForceStart;
			musicTab.OnClick = () => panel = PanelType.Music;

			var serversTab = tabContainer.GetOrNull<ButtonWidget>("SERVERS_TAB");
			if (serversTab != null)
			{
				serversTab.IsHighlighted = () => panel == PanelType.Servers;
				serversTab.IsDisabled = () => panel == PanelType.Kick || panel == PanelType.ForceStart;
				serversTab.OnClick = () =>
				{
					// Refresh the list when switching to the servers tab
					if (serverListLogic != null && panel != PanelType.Servers)
						serverListLogic.RefreshServerList();

					panel = PanelType.Servers;
				};
			}

			// Force start panel
			void StartGame()
			{
				// Refresh MapCache and check if the selected map is available before attempting to start the game
				if (modData.MapCache[map.Uid].Status == MapStatus.Available)
				{
					gameStarting = true;
					orderManager.IssueOrder(Order.Command("startgame"));
				}
				else
					modData.MapCache.UpdateMaps();
			}

			bool StartDisabled() => map.Status != MapStatus.Available ||
				orderManager.LobbyInfo.Slots.Any(sl => sl.Value.Required && orderManager.LobbyInfo.ClientInSlot(sl.Key) == null) ||
				orderManager.LobbyInfo.Slots.All(sl => orderManager.LobbyInfo.ClientInSlot(sl.Key) == null) ||
				(!orderManager.LobbyInfo.GlobalSettings.EnableSingleplayer && orderManager.LobbyInfo.NonBotPlayers.Count() < 2) ||
				insufficientPlayerSpawns;

			var startGameButton = lobby.GetOrNull<ButtonWidget>("START_GAME_BUTTON");
			if (startGameButton != null)
			{
				bool IosConfigurationDisabled() =>
					!Game.IsHost || gameStarting || safeStartState.IsPending ||
					panel == PanelType.ForceStart || panel == PanelType.Kick ||
					orderManager.LocalClient == null;

				startGameButton.IsDisabled = () => Platform.IsIOS
					? IosConfigurationDisabled() || !CurrentStartAvailability().CanStart
					: configurationDisabled() || StartDisabled();

				var originalStartText = startGameButton.GetText;
				startGameButton.GetText = () =>
				{
					if (!Platform.IsIOS || !Game.IsHost)
						return originalStartText();

					if (safeStartState.IsPending)
						return FluentProvider.GetMessage("button-ios-lobby-start-waiting");

					var availability = CurrentStartAvailability();
					if (availability.CanStart)
						return originalStartText();

					var width = Math.Max(0,
						startGameButton.Bounds.Width - startGameButton.LeftMargin - startGameButton.RightMargin);
					var font = Game.Renderer.Fonts[startGameButton.Font];
					return IosLobbyStartStatus.Format(availability, width, font,
						index => orderManager.LobbyInfo.ClientWithIndex(index)?.Name ?? string.Empty,
						(key, args) => FluentProvider.GetMessage(key, args));
				};

				startGameButton.OnClick = () =>
				{
					if (Platform.IsIOS)
					{
						BeginSafeStart();
						return;
					}

					// Bots and admins don't count
					if (orderManager.LobbyInfo.Clients.Any(c => c.Slot != null && !c.IsAdmin && c.Bot == null && !c.IsReady))
						panel = PanelType.ForceStart;
					else
						StartGame();
				};
			}

			var forceStartBin = Ui.LoadWidget("FORCE_START_DIALOG", lobby.Get("TOP_PANELS_ROOT"), new WidgetArgs());
			forceStartBin.IsVisible = () => panel == PanelType.ForceStart;
			forceStartBin.Get("KICK_WARNING").IsVisible = () => orderManager.LobbyInfo.Clients.Any(c => c.IsInvalid);
			var forceStartButton = forceStartBin.Get<ButtonWidget>("OK_BUTTON");
			forceStartButton.OnClick = StartGame;
			forceStartButton.IsDisabled = StartDisabled;

			forceStartBin.Get<ButtonWidget>("CANCEL_BUTTON").OnClick = () => panel = PanelType.Players;

			var disconnectButton = lobby.Get<ButtonWidget>("DISCONNECT_BUTTON");
			disconnectButton.OnClick = () =>
			{
				Ui.CloseWindow();
				onExit();
				Game.Sound.PlayNotification(modRules, null, "Sounds", playerLeftSound, null);
			};

			if (skirmishMode)
			{
				var disconnectButtonText = FluentProvider.GetMessage(Back);
				disconnectButton.GetText = () => disconnectButtonText;
			}

			if (logicArgs.TryGetValue("ChatTemplates", out var templateIds))
			{
				foreach (var item in templateIds.Nodes)
				{
					var key = FieldLoader.GetValue<TextNotificationPool>("key", item.Key);
					chatTemplates[key] = Ui.LoadWidget(item.Value.Value, null, new WidgetArgs());
				}
			}

			var chatMode = lobby.Get<ButtonWidget>("CHAT_MODE");
			var team = FluentProvider.GetMessage(TeamChat);
			var all = FluentProvider.GetMessage(GeneralChat);
			chatMode.GetText = () => teamChat ? team : all;
			chatMode.OnClick = () => teamChat ^= true;
			chatMode.IsDisabled = () => disableTeamChat || !chatEnabled;

			chatTextField = lobby.Get<TextFieldWidget>("CHAT_TEXTFIELD");
			chatTextField.IsDisabled = () => !chatEnabled;
			chatTextField.MaxLength = UnitOrders.ChatMessageMaxLength;

			chatTextField.OnEnterKey = _ =>
			{
				if (chatTextField.Text.Length == 0)
					return true;

				// Always scroll to bottom when we've typed something
				lobbyChatPanel.ScrollToBottom();

				var teamNumber = 0U;
				if (teamChat && orderManager.LocalClient != null)
					teamNumber = orderManager.LocalClient.IsObserver ? uint.MaxValue : (uint)orderManager.LocalClient.Team;

				orderManager.IssueOrder(Order.Chat(chatTextField.Text, teamNumber));
				chatTextField.Text = "";
				return true;
			};

			chatTextField.OnTabKey = e =>
			{
				if (!chatMode.Key.IsActivatedBy(e) || chatMode.IsDisabled())
				{
					chatTextField.Text = tabCompletion.Complete(chatTextField.Text);
					chatTextField.CursorPosition = chatTextField.Text.Length;
				}
				else
					chatMode.OnKeyPress(e);

				return true;
			};

			chatTextField.OnEscKey = _ => chatTextField.YieldKeyboardFocus();

			chatAvailableIn = new CachedTransform<int, string>(x => FluentProvider.GetMessage(ChatAvailability, "seconds", x));
			chatDisabled = FluentProvider.GetMessage(ChatDisabled);

			lobbyChatPanel = lobby.Get<ScrollPanelWidget>("CHAT_DISPLAY");
			lobbyChatPanel.RemoveChildren();

			var settingsButton = lobby.GetOrNull<ButtonWidget>("SETTINGS_BUTTON");
			if (settingsButton != null)
			{
				settingsButton.OnClick = () => Ui.OpenWindow("SETTINGS_PANEL", new WidgetArgs
				{
					{ "onExit", DoNothing },
					{ "worldRenderer", worldRenderer }
				});
			}

			if (logicArgs.TryGetValue("ChatLineSound", out var yaml))
				chatLineSound = yaml.Value;
			if (logicArgs.TryGetValue("PlayerJoinedSound", out yaml))
				playerJoinedSound = yaml.Value;
			if (logicArgs.TryGetValue("PlayerLeftSound", out yaml))
				playerLeftSound = yaml.Value;
			if (logicArgs.TryGetValue("LobbyOptionChangedSound", out yaml))
				lobbyOptionChangedSound = yaml.Value;

			ApplyIosLobbyLayout();
		}

		void ApplyIosLobbyLayout()
		{
			if (!Platform.IsIOS)
				return;

			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var bounds = policy.ContentBounds;
			lobby.Bounds = bounds;
			var layout = IosLobbyLayout.Create(bounds.Width, bounds.Height, policy, skirmishMode);
			iosLayoutPolicy = policy;
			iosLobbyLayout = layout;
			iosLayoutResolution = resolution;
			iosNativePointSize = snapshot.NativePointSize;
			iosSafeBounds = snapshot.SafeBounds;
			IosTouchMenuLogic.ApplyIosFonts(lobby);

			var title = lobby.GetOrNull("SERVER_NAME");
			if (title != null)
				title.Bounds = layout.Header;

			var mapRoot = lobby.GetOrNull("MAP_PREVIEW_ROOT");
			if (mapRoot != null)
				mapRoot.Bounds = layout.Map;

			var topRoot = lobby.GetOrNull("TOP_PANELS_ROOT");
			if (topRoot != null)
			{
				topRoot.Bounds = layout.Main;
				foreach (var child in topRoot.Children)
				{
					var surface = child.Id == "LOBBY_SERVERS_BIN" ? layout.Servers : layout.Players;
					child.Bounds = new WidgetBounds(
						surface.X - layout.Main.X, surface.Y - layout.Main.Y,
						surface.Width, surface.Height);
				}
			}

			LayoutTabRow(lobby.GetOrNull("SKIRMISH_TABS"), layout.Tabs, 3);
			LayoutTabRow(lobby.GetOrNull("MULTIPLAYER_TABS"), layout.Tabs, 4);

			var chatRoot = lobby.GetOrNull("LOBBYCHAT");
			if (chatRoot != null)
			{
				chatRoot.Bounds = layout.Chat;
				chatRoot.IsVisible = () => panel != PanelType.Servers;
				LayoutIosChat(chatRoot, layout, policy);
			}

			PlaceFooterButton("START_GAME_BUTTON", layout.Start.X, layout.Start.Y,
				layout.Start.Width, layout.Start.Height);
			PlaceFooterButton("DISCONNECT_BUTTON", layout.Disconnect.X, layout.Disconnect.Y,
				layout.Disconnect.Width, layout.Disconnect.Height);
			PlaceFooterButton("CHANGEMAP_BUTTON", layout.ChangeMap.X, layout.ChangeMap.Y,
				layout.ChangeMap.Width, layout.ChangeMap.Height);
			PlaceFooterButton("SLOTS_DROPDOWNBUTTON", layout.Position.X, layout.Position.Y,
				layout.Position.Width, layout.Position.Height);
			PlaceFooterButton("RESET_OPTIONS_BUTTON", layout.Position.X, layout.Position.Y,
				layout.Position.Width, layout.Position.Height);

			LayoutIosPlayerRows(layout, policy, skirmishMode);
			IosMapPreviewLayout.Apply(mapRoot, policy);
		}

		static void LayoutTabRow(Widget container, WidgetBounds bounds, int count)
		{
			if (container == null)
				return;

			container.Bounds = bounds;
			var buttons = container.Children.OfType<ButtonWidget>().ToArray();
			var cellWidth = bounds.Width / count;
			for (var i = 0; i < buttons.Length; i++)
			{
				var right = i == buttons.Length - 1 ? bounds.Width : (i + 1) * cellWidth;
				buttons[i].Bounds = new WidgetBounds(i * cellWidth, 0, right - i * cellWidth, bounds.Height);
			}
		}

		void PlaceFooterButton(string id, int x, int y, int width, int height)
		{
			var button = lobby.GetOrNull<ButtonWidget>(id);
			if (button != null)
				button.Bounds = new WidgetBounds(x, y, width, height);
		}

		void LayoutIosPlayerRows(IosLobbyLayout layout, IosMenuLayoutPolicy policy, bool isSkirmish)
		{
			var playerBin = players.Parent;
			var labelContainer = playerBin.GetOrNull("LABEL_CONTAINER");
			var headerHeight = layout.PlayerHeader.Height;
			if (labelContainer != null)
			{
				labelContainer.Bounds = new WidgetBounds(0, 0, layout.PlayerRow.Width, headerHeight);
				LayoutPlayerHeader(labelContainer, layout, isSkirmish);
			}

			players.Bounds = new WidgetBounds(
				layout.PlayerList.X, layout.PlayerList.Y, playerBin.Bounds.Width, layout.PlayerList.Height);
			players.EnableContentDragging = true;
			players.ContentDragThreshold = policy.ContentDragThreshold;
			players.ItemSpacing = policy.Gap;
			players.TopBottomSpacing = policy.Gap;
			if (policy.IsPhone)
			{
				players.ScrollBar = ScrollBar.Hidden;
				players.ScrollbarWidth = 0;
			}
			else
			{
				players.ScrollBar = ScrollBar.Right;
				players.ScrollbarWidth = policy.MinimumTarget;
				players.MinimumThumbSize = policy.MinimumTarget;
			}

			foreach (var row in new[]
			{
				editablePlayerTemplate, nonEditablePlayerTemplate, emptySlotTemplate
			})
				LayoutIosPlayerRow(row, layout, policy, isSkirmish);

			foreach (var row in players.Children)
				LayoutIosPlayerRow(row, layout, policy, isSkirmish);

			players.Layout.AdjustChildren();
		}

		static void LayoutPlayerHeader(Widget header, IosLobbyLayout layout, bool isSkirmish)
		{
			SetDirectBounds(header, "LABEL_LOBBY_NAME", layout.PlayerName);
			SetDirectBounds(header, "LABEL_LOBBY_COLOR", layout.PlayerColor);
			SetDirectBounds(header, "LABEL_LOBBY_FACTION", layout.PlayerFaction);
			SetDirectBounds(header, "LABEL_LOBBY_TEAM", layout.PlayerTeam);
			SetDirectBounds(header, "LABEL_LOBBY_HANDICAP", layout.PlayerHandicap);
			SetDirectBounds(header, "LABEL_LOBBY_SPAWN", layout.PlayerSpawn);
			SetDirectBounds(header, "LABEL_LOBBY_STATUS", layout.PlayerReady);
			foreach (var id in new[] { "LABEL_LOBBY_HANDICAP", "LABEL_LOBBY_STATUS" })
			{
				var label = DirectChild(header, id);
				if (label != null)
					label.IsVisible = () => !isSkirmish;
			}

			foreach (var label in header.Children.OfType<LabelWidget>())
			{
				label.WordWrap = true;
				label.VAlign = TextVAlign.Middle;
			}
		}

		static void LayoutIosPlayerRow(
			Widget row, IosLobbyLayout layout, IosMenuLayoutPolicy policy, bool isSkirmish)
		{
			row.Bounds.X = layout.PlayerRow.X;
			row.Bounds.Width = layout.PlayerRow.Width;
			row.Bounds.Height = layout.PlayerRow.Height;
			foreach (var id in new[] { "NAME", "SLOT_OPTIONS", "PLAYER_ACTION" })
				SetDirectBounds(row, id, layout.PlayerName);
			foreach (var id in new[] { "COLOR", "COLORBLOCK" })
				SetDirectBounds(row, id, layout.PlayerColor);
			SetDirectBounds(row, "FACTION", layout.PlayerFaction);
			foreach (var id in new[] { "TEAM", "TEAM_DROPDOWN" })
				SetDirectBounds(row, id, layout.PlayerTeam);
			foreach (var id in new[] { "HANDICAP", "HANDICAP_DROPDOWN" })
				SetDirectBounds(row, id, layout.PlayerHandicap);
			foreach (var id in new[] { "SPAWN", "SPAWN_DROPDOWN" })
				SetDirectBounds(row, id, layout.PlayerSpawn);
			foreach (var id in new[]
			{
				"STATUS_CHECKBOX", "STATUS_IMAGE", "IOS_MAP_STATUS_TEXT", "IOS_MAP_STATUS_ICON"
			})
				SetDirectBounds(row, id, layout.PlayerReady);
			foreach (var id in new[]
			{
				"HANDICAP", "HANDICAP_DROPDOWN", "STATUS_CHECKBOX", "STATUS_IMAGE",
				"IOS_MAP_STATUS_TEXT", "IOS_MAP_STATUS_ICON"
			})
			{
				var child = DirectChild(row, id);
				if (child != null)
					child.IsVisible = () => !isSkirmish;
			}

			var actionBounds = new WidgetBounds(layout.PlayerColor.X, 0,
				Math.Max(0, layout.PlayerReady.Right - layout.PlayerColor.X), layout.PlayerRow.Height);
			foreach (var id in new[] { "JOIN" })
				SetDirectBounds(row, id, actionBounds);

			var latency = DirectChild(row, "LATENCY");
			if (latency != null)
				latency.Bounds = new WidgetBounds(layout.PlayerName.X, 0,
					Math.Min(layout.PlayerName.Width, layout.PlayerRow.Height), layout.PlayerRow.Height);
			var latencyRegion = DirectChild(row, "LATENCY_REGION");
			if (latencyRegion != null)
				latencyRegion.Bounds = new WidgetBounds(layout.PlayerName.X, 0,
					Math.Min(layout.PlayerName.Width, layout.PlayerRow.Height), layout.PlayerRow.Height);

			var faction = DirectChild(row, "FACTION");
			if (faction != null)
			{
				var flag = DirectChild(faction, "FACTIONFLAG");
				var flagWidth = Math.Max(1, (int)Math.Ceiling(40 * policy.LogicalPerPoint));
				var flagHeight = Math.Max(1, (int)Math.Ceiling(20 * policy.LogicalPerPoint));
				flagWidth = Math.Min(flagWidth, faction.Bounds.Width);
				flagHeight = Math.Min(flagHeight, faction.Bounds.Height);
				if (flag != null)
				{
					flag.Bounds = new WidgetBounds(
						0, Math.Max(0, faction.Bounds.Height - flagHeight) / 2, flagWidth, flagHeight);
					if (flag is ImageWidget image)
						image.StretchToFit = true;
				}

				var name = DirectChild(faction, "FACTIONNAME");
				if (name != null)
				{
					var nameX = Math.Min(faction.Bounds.Width, flagWidth + policy.Gap);
					var usableWidth = faction is DropDownButtonWidget dropdown ?
						dropdown.UsableWidth : faction.Bounds.Width;
					name.Bounds = new WidgetBounds(nameX, 0,
						Math.Max(0, usableWidth - nameX), faction.Bounds.Height);
				}
			}
		}

		static void SetDirectBounds(Widget parent, string id, WidgetBounds bounds)
		{
			var child = DirectChild(parent, id);
			if (child != null)
				child.Bounds = bounds;
		}

		static Widget DirectChild(Widget parent, string id)
		{
			return parent.Children.FirstOrDefault(child => child.Id == id);
		}

		static void LayoutIosChat(Widget chatRoot, IosLobbyLayout layout, IosMenuLayoutPolicy policy)
		{
			var display = chatRoot.GetOrNull("CHAT_DISPLAY");
			if (display != null)
				display.Bounds = layout.ChatDisplay;
			if (display is ScrollPanelWidget scrollPanel)
			{
				scrollPanel.EnableContentDragging = true;
				scrollPanel.ContentDragThreshold = policy.ContentDragThreshold;
				if (policy.IsPhone)
				{
					scrollPanel.ScrollBar = ScrollBar.Hidden;
					scrollPanel.ScrollbarWidth = 0;
				}
				else
				{
					scrollPanel.ScrollBar = ScrollBar.Right;
					scrollPanel.ScrollbarWidth = policy.MinimumTarget;
					scrollPanel.MinimumThumbSize = policy.MinimumTarget;
				}
			}

			var modeWidth = Math.Min(layout.ChatInput.Width, 2 * policy.MinimumTarget);
			var mode = chatRoot.GetOrNull("CHAT_MODE");
			if (mode != null)
				mode.Bounds = new WidgetBounds(
					layout.ChatInput.X, layout.ChatInput.Y, modeWidth, layout.ChatInput.Height);
			var textX = Math.Min(layout.ChatInput.Right,
				layout.ChatInput.X + modeWidth + policy.Gap);
			var text = chatRoot.GetOrNull("CHAT_TEXTFIELD");
			if (text != null)
				text.Bounds = new WidgetBounds(textX, layout.ChatInput.Y,
					Math.Max(0, layout.ChatInput.Right - textX), layout.ChatInput.Height);
		}

		bool disposed;
		protected override void Dispose(bool disposing)
		{
			if (disposing && !disposed)
			{
				disposed = true;
				safeStartState.Reset();
				Game.LobbyInfoChanged -= UpdateCurrentMap;
				Game.LobbyInfoChanged -= UpdatePlayerList;
				Game.LobbyInfoChanged -= UpdateDiscordStatus;
				Game.LobbyInfoChanged -= UpdateSpawnOccupants;
				Game.LobbyInfoChanged -= StartStatusLobbyInfoChanged;
				Game.BeforeGameStart -= OnGameStart;
				Game.ConnectionStateChanged -= ConnectionStateChanged;
				orderManager.ClientMapReadinessChanged -= ClientMapReadinessChanged;
				orderManager.SafeStartRejected -= SafeStartRejected;
			}

			base.Dispose(disposing);
		}

		bool OptionsTabDisabled()
		{
			return !MapIsPlayable || panel == PanelType.Kick || panel == PanelType.ForceStart;
		}

		bool IosLobbyLayoutChanged()
		{
			if (!Platform.IsIOS || iosLobbyLayout == null)
				return Platform.IsIOS;

			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			return resolution != iosLayoutResolution || snapshot.NativePointSize != iosNativePointSize ||
				snapshot.SafeBounds != iosSafeBounds;
		}

		public override void Tick()
		{
			if (IosLobbyLayoutChanged())
				ApplyIosLobbyLayout();

			if (panel == PanelType.Options && OptionsTabDisabled())
				panel = PanelType.Players;

			var chatWasEnabled = chatEnabled;
			chatEnabled =
				worldRenderer.World.IsReplay ||
				(Game.RunTime >= TextNotificationsManager.ChatDisabledUntil && TextNotificationsManager.ChatDisabledUntil != uint.MaxValue);

			if (chatEnabled && !chatWasEnabled)
			{
				chatTextField.Text = "";
				if (Ui.KeyboardFocusWidget == null)
					chatTextField.TakeKeyboardFocus();
			}
			else if (!chatEnabled)
			{
				var remaining = 0;
				if (TextNotificationsManager.ChatDisabledUntil != uint.MaxValue)
					remaining = (int)(TextNotificationsManager.ChatDisabledUntil - Game.RunTime + 999) / 1000;

				chatTextField.Text = remaining == 0 ? chatDisabled : chatAvailableIn.Update(remaining);
			}
		}

		void INotificationHandler<TextNotification>.Handle(TextNotification notification)
		{
			var chatLine = chatTemplates[notification.Pool].Clone();
			WidgetUtils.SetupTextNotification(chatLine, notification, lobbyChatPanel.Bounds.Width - lobbyChatPanel.ScrollbarWidth, true);

			var scrolledToBottom = lobbyChatPanel.ScrolledToBottom;
			lobbyChatPanel.AddChild(chatLine);
			if (scrolledToBottom)
				lobbyChatPanel.ScrollToBottom(smooth: true);

			switch (notification.Pool)
			{
				case TextNotificationPool.Chat:
					Game.Sound.PlayNotification(modRules, null, "Sounds", chatLineSound, null);
					break;
				case TextNotificationPool.System:
					Game.Sound.PlayNotification(modRules, null, "Sounds", lobbyOptionChangedSound, null);
					break;
				case TextNotificationPool.Join:
					Game.Sound.PlayNotification(modRules, null, "Sounds", playerJoinedSound, null);
					break;
				case TextNotificationPool.Leave:
					Game.Sound.PlayNotification(modRules, null, "Sounds", playerLeftSound, null);
					break;
			}
		}

		void UpdateCurrentMap()
		{
			mapStatus = orderManager.LobbyInfo.GlobalSettings.MapStatus;
			var uid = orderManager.LobbyInfo.GlobalSettings.Map;
			if (map.Uid == uid)
				return;

			map = modData.MapCache[uid];
		}

		void UpdatePlayerList()
		{
			if (orderManager.LocalClient == null)
				return;

			// Check if we are not assigned to any team, and are no spectator
			// If we are a spectator, check if there are more and enable spectator chat
			// Otherwise check if our assigned team has more players
			if (orderManager.LocalClient.Team == 0 && !orderManager.LocalClient.IsObserver)
				disableTeamChat = true;
			else if (orderManager.LocalClient.IsObserver)
				disableTeamChat = !orderManager.LobbyInfo.Clients.Any(c => c != orderManager.LocalClient && c.IsObserver);
			else
				disableTeamChat = !orderManager.LobbyInfo.Clients.Any(c =>
					c != orderManager.LocalClient &&
					c.Bot == null &&
					c.Team == orderManager.LocalClient.Team);

			insufficientPlayerSpawns = LobbyUtils.InsufficientEnabledSpawnPoints(map, orderManager.LobbyInfo);

			if (disableTeamChat)
				teamChat = false;

			var isHost = Game.IsHost;
			var idx = 0;
			foreach (var kv in orderManager.LobbyInfo.Slots)
			{
				var key = kv.Key;
				var slot = kv.Value;
				var client = orderManager.LobbyInfo.ClientInSlot(key);
				Widget template = null;

				// get template for possible reuse
				if (idx < players.Children.Count)
					template = players.Children[idx];

				if (client == null)
				{
					// Empty slot
					if (template == null || template.Id != emptySlotTemplate.Id)
						template = emptySlotTemplate.Clone();

					if (isHost)
						LobbyUtils.SetupEditableSlotWidget(template, slot, client, orderManager, map, modData);
					else
						LobbyUtils.SetupSlotWidget(template, modData, slot, client);

					var join = template.Get<ButtonWidget>("JOIN");
					join.IsVisible = () => !slot.Closed;
					join.IsDisabled = () => orderManager.LocalClient.IsReady;
					join.OnClick = () => orderManager.IssueOrder(Order.Command("slot " + key));
				}
				else if ((client.Index == orderManager.LocalClient.Index) ||
						 (client.Bot != null && isHost))
				{
					// Editable player in slot
					if (template == null || template.Id != editablePlayerTemplate.Id)
						template = editablePlayerTemplate.Clone();

					LobbyUtils.SetupLatencyWidget(template, client, orderManager);

					if (client.Bot != null)
						LobbyUtils.SetupEditableSlotWidget(template, slot, client, orderManager, map, modData);
					else
						LobbyUtils.SetupEditableNameWidget(template, client, orderManager, worldRenderer);

					if (skirmishMode)
						LobbyUtils.SetupEditableClassicColorWidget(template, slot, client, orderManager, colorManager);
					else
						LobbyUtils.SetupEditableColorWidget(template, slot, client, orderManager, worldRenderer, colorManager);
					LobbyUtils.SetupEditableFactionWidget(template, slot, client, orderManager, factions);
					LobbyUtils.SetupEditableTeamWidget(template, slot, client, orderManager, map);
					if (!skirmishMode)
						LobbyUtils.SetupEditableHandicapWidget(template, slot, client, orderManager);
					LobbyUtils.SetupEditableSpawnWidget(template, slot, client, orderManager, map);
					if (!skirmishMode)
						LobbyUtils.SetupEditableReadyWidget(template, client, orderManager, map, MapIsPlayable,
							orderManager.LobbyInfo.GlobalSettings.Map, Platform.IsIOS);
				}
				else
				{
					// Non-editable player in slot
					if (template == null || template.Id != nonEditablePlayerTemplate.Id)
						template = nonEditablePlayerTemplate.Clone();

					LobbyUtils.SetupLatencyWidget(template, client, orderManager);
					LobbyUtils.SetupColorWidget(template, client);
					LobbyUtils.SetupFactionWidget(template, client, factions);

					if (isHost)
					{
						LobbyUtils.SetupEditableTeamWidget(template, slot, client, orderManager, map);
						if (!skirmishMode)
							LobbyUtils.SetupEditableHandicapWidget(template, slot, client, orderManager);
						LobbyUtils.SetupEditableSpawnWidget(template, slot, client, orderManager, map);
						LobbyUtils.SetupPlayerActionWidget(template, client, orderManager, worldRenderer,
							lobby, () => panel = PanelType.Kick, () => panel = PanelType.Players);
					}
					else
					{
						LobbyUtils.SetupNameWidget(template, client, orderManager, worldRenderer, map);
						LobbyUtils.SetupTeamWidget(template, client);
						if (!skirmishMode)
							LobbyUtils.SetupHandicapWidget(template, client);
						LobbyUtils.SetupSpawnWidget(template, client);
					}

					if (!skirmishMode)
						LobbyUtils.SetupReadyWidget(template, client,
							orderManager.LobbyInfo.GlobalSettings.Map, Platform.IsIOS);
				}

				template.IsVisible = () => true;

				if (idx >= players.Children.Count)
					players.AddChild(template);
				else if (players.Children[idx].Id != template.Id)
					players.ReplaceChild(players.Children[idx], template);

				idx++;
			}

			while (players.Children.Count > idx)
				players.RemoveChild(players.Children[idx]);

			if (Platform.IsIOS && iosLobbyLayout != null)
				LayoutIosPlayerRows(iosLobbyLayout, iosLayoutPolicy, skirmishMode);

			tabCompletion.Names = orderManager.LobbyInfo.Clients.Where(c => !c.IsBot).Select(c => c.Name).Distinct().ToList();
		}

		void UpdateDiscordStatus()
		{
			var numberOfPlayers = 0;
			var slots = 0;

			if (!skirmishMode)
			{
				foreach (var kv in orderManager.LobbyInfo.Slots)
				{
					if (kv.Value.Closed)
						continue;

					slots++;
					var client = orderManager.LobbyInfo.ClientInSlot(kv.Key);

					if (client != null)
						numberOfPlayers++;
				}
			}

			// Add extra slots to keep the join button active for spectators
			if (numberOfPlayers == slots && orderManager.LobbyInfo.GlobalSettings.AllowSpectators)
				slots = numberOfPlayers + 1;

			var details = map.Title + " - " + orderManager.LobbyInfo.GlobalSettings.ServerName;
			if (updateDiscordStatus)
			{
				string secret = null;
				if (orderManager.LobbyInfo.GlobalSettings.Dedicated)
				{
					var endpoint = CurrentServerSettings.Target.GetConnectEndPoints().First();
					secret = string.Concat(endpoint.Address, "|", endpoint.Port);
				}

				var state = skirmishMode ? DiscordState.InSkirmishLobby : DiscordState.InMultiplayerLobby;

				DiscordService.UpdateStatus(state, details, secret, numberOfPlayers, slots);
				updateDiscordStatus = false;
			}
			else
			{
				if (!skirmishMode)
					DiscordService.UpdatePlayers(numberOfPlayers, slots);

				DiscordService.UpdateDetails(details);
			}
		}

		void UpdateSpawnOccupants()
		{
			spawnOccupants = orderManager.LobbyInfo.Clients
				.Where(c => c.SpawnPoint != 0)
				.ToDictionary(c => c.SpawnPoint, c => new SpawnOccupant(c));
		}

		void UpdateOptions()
		{
			if (map == null || map.WorldActorInfo == null)
				return;

			var serverOptions = orderManager.LobbyInfo.GlobalSettings.LobbyOptions;
			var mapOptions = map.PlayerActorInfo.TraitInfos<ILobbyOptions>()
				.Concat(map.WorldActorInfo.TraitInfos<ILobbyOptions>())
				.SelectMany(t => t.LobbyOptions(map))
				.Where(o => o.IsVisible)
				.OrderBy(o => o.DisplayOrder)
				.ToArray();

			resetOptionsButtonEnabled = mapOptions.Any(o => o.DefaultValue != serverOptions[o.Id].Value);
		}

		void OnGameStart()
		{
			Ui.CloseWindow();

			var state = skirmishMode ? DiscordState.PlayingSkirmish : DiscordState.PlayingMultiplayer;
			var details = map.Title + " - " + orderManager.LobbyInfo.GlobalSettings.ServerName;
			DiscordService.UpdateStatus(state, details);

			onStart();
		}
	}

	public class LobbyFaction
	{
		public bool Selectable;
		public string Name;
		public string Description;
		public string Side;
	}

	sealed class DropDownOption
	{
		public string Title;
		public Func<bool> IsSelected = () => false;
		public Action OnClick;
	}
}
