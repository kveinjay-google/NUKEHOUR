#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Network;
using OpenRA.Platforms.Default;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	[NonParallelizable]
	public sealed class IosLobbyResponsiveLayoutTest
	{
		static LobbyStartAvailability StartAvailability(
			Session.LobbyStartBlockReason reason,
			int clientIndex = -1,
			Session.ClientMapPhase phase = Session.ClientMapPhase.Unknown,
			int progress = -1,
			int additionalBlockingClients = 0)
		{
			var constructor = typeof(LobbyStartAvailability).GetConstructor(
				BindingFlags.Instance | BindingFlags.NonPublic,
				null,
				new[]
				{
					typeof(Session.LobbyStartBlockReason), typeof(int),
					typeof(Session.ClientMapPhase), typeof(int), typeof(int)
				},
				null);
			Assert.That(constructor, Is.Not.Null);
			return (LobbyStartAvailability)constructor!.Invoke(new object[]
			{
				reason, clientIndex, phase, progress, additionalBlockingClients
			});
		}

		static string LocalizeStart(string locale, string key, object[] arguments)
		{
			var file = key == "notification-requires-host" ? "common.ftl" : "chrome.ftl";
			var value = FluentValue(locale, file, key);
			for (var i = 0; i < arguments.Length; i += 2)
				value = value.Replace("{ $" + arguments[i] + " }",
					Convert.ToString(arguments[i + 1], System.Globalization.CultureInfo.InvariantCulture),
					StringComparison.Ordinal);

			return value;
		}

		static IEnumerable<TestCaseData> SupportedDevices()
		{
			yield return Device("iPhone-932", 1560, 720, 932, 430, 59, 0, 59, 21);
			yield return Device("iPhone-844", 1558, 720, 844, 390, 47, 0, 47, 21);
			yield return Device("iPad-mini-1133", 1133, 744, 1133, 744, 0, 0, 0, 20);
			yield return Device("iPad-mini-1024", 1024, 768, 1024, 768, 0, 0, 0, 20);
			yield return Device("iPad-Air", 1180, 820, 1180, 820, 0, 0, 0, 20);
			yield return Device("iPad-Pro", 1366, 1024, 1366, 1024, 0, 0, 0, 20);
		}

		static IEnumerable<TestCaseData> SupportedTablets()
		{
			yield return Device("iPad-mini-1133", 1133, 744, 1133, 744, 0, 0, 0, 20);
			yield return Device("iPad-mini-1024", 1024, 768, 1024, 768, 0, 0, 0, 20);
			yield return Device("iPad-Air", 1180, 820, 1180, 820, 0, 0, 0, 20);
			yield return Device("iPad-Pro", 1366, 1024, 1366, 1024, 0, 0, 0, 20);
		}

		static IEnumerable<TestCaseData> SupportedPhones()
		{
			yield return Device("iPhone-932", 1560, 720, 932, 430, 59, 0, 59, 21);
			yield return Device("iPhone-844", 1558, 720, 844, 390, 47, 0, 47, 21);
		}

		static IEnumerable<TestCaseData> PhoneLocales()
		{
			foreach (var locale in new[] { "en-US", "zh-CN" })
			{
				yield return PhoneLocale(
					"iPhone-932", locale, 1560, 720, 932, 430, 59, 0, 59, 21);
				yield return PhoneLocale(
					"iPhone-844", locale, 1558, 720, 844, 390, 47, 0, 47, 21);
			}
		}

		[TestCaseSource(nameof(PhoneLocales))]
		public void IosStartBlockersUseCompleteMeasuredLocalizedText(
			IosScreenSnapshot snapshot, string locale)
		{
			using var fonts = new ActualIosFontRenderer();
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var layout = IosLobbyLayout.Create(policy.ContentBounds.Width, policy.ContentBounds.Height, policy);
			var font = Game.Renderer.Fonts["IosBold"];
			var width = layout.Start.Width - 10;
			var names = new Dictionary<int, string> { { 1, "Kevin" }, { 2, new string('W', 80) } };
			string Format(LobbyStartAvailability availability) => IosLobbyStartStatus.Format(
				availability, width, font, index => names[index],
				(key, args) => LocalizeStart(locale, key, args));

			string[] expected;
			if (locale == "zh-CN")
				expected = new[]
				{
					"地图校验中…", "地图不可用", "Kevin 下载失败", "Kevin 校验中…",
					"Kevin 下载 42%", "等待 Kevin 下载", "Kevin 下载 42% · 另 1 人",
					"2 人地图未就绪",
					"请填满必需位置", "请加入玩家", "至少需要 2 人", "出生点不足",
					"只有主机才能执行此操作。"
				};
			else
				expected = new[]
				{
					"Validating map…", "Map unavailable", "Kevin failed", "Kevin verifying…",
					"Kevin 42%", "Waiting for Kevin", "Kevin 42% · +1",
					"2 maps not ready",
					"Fill required slots", "Add a player", "Need 2 players", "Enable more spawns",
					"Only the host can do that."
				};
			var actual = new[]
			{
				Format(StartAvailability(Session.LobbyStartBlockReason.ServerMapValidating)),
				Format(StartAvailability(Session.LobbyStartBlockReason.SelectedMapUnavailable)),
				Format(StartAvailability(Session.LobbyStartBlockReason.ClientMapError,
					1, Session.ClientMapPhase.Error)),
				Format(StartAvailability(Session.LobbyStartBlockReason.ClientMapInstallingOrVerifying,
					1, Session.ClientMapPhase.InstallingOrVerifying)),
				Format(StartAvailability(Session.LobbyStartBlockReason.ClientMapDownloading,
					1, Session.ClientMapPhase.Downloading, 42)),
				Format(StartAvailability(Session.LobbyStartBlockReason.ClientMapUnknown, 1)),
				Format(StartAvailability(Session.LobbyStartBlockReason.ClientMapDownloading,
					1, Session.ClientMapPhase.Downloading, 42, 1)),
				Format(StartAvailability(Session.LobbyStartBlockReason.ClientMapDownloading,
					2, Session.ClientMapPhase.Downloading, 42, 1)),
				Format(StartAvailability(Session.LobbyStartBlockReason.RequiredSlotEmpty)),
				Format(StartAvailability(Session.LobbyStartBlockReason.NoPlayers)),
				Format(StartAvailability(Session.LobbyStartBlockReason.InsufficientHumans)),
				Format(StartAvailability(Session.LobbyStartBlockReason.InsufficientSpawnPoints)),
				Format(StartAvailability(Session.LobbyStartBlockReason.RequesterNotAdmin))
			};

			Assert.That(actual, Is.EqualTo(expected));
			foreach (var value in actual)
			{
				Assert.That(value, Does.Not.Contain("..."));
				Assert.That(font.Measure(value).X, Is.LessThanOrEqualTo(width),
					$"{locale} '{value}' must fit the narrowest iPhone Start button.");
			}
		}

		[Test]
		public void IosMapStatusPresentationIsCurrentUidHumanOnlyAndTruthful()
		{
			var client = new Session.Client { MapUid = "map-a", Bot = null };
			var cases = new[]
			{
				(Session.ClientMapPhase.Unknown, -1, "…", ""),
				(Session.ClientMapPhase.Searching, -1, "…", ""),
				(Session.ClientMapPhase.WaitingForDownload, -1, "…", ""),
				(Session.ClientMapPhase.Downloading, -1, "…", ""),
				(Session.ClientMapPhase.Downloading, 42, "42%", ""),
				(Session.ClientMapPhase.InstallingOrVerifying, -1, "…", ""),
				(Session.ClientMapPhase.Unavailable, -1, "", "!"),
				(Session.ClientMapPhase.Error, -1, "", "!")
			};

			foreach (var (phase, progress, text, icon) in cases)
			{
				client.MapPhase = phase;
				client.MapProgress = progress;
				var presentation = IosLobbyMapStatusPresentation.For(client, "map-a", true);
				Assert.Multiple(() =>
				{
					Assert.That(presentation.ShowMapStatus, Is.True, phase.ToString());
					Assert.That(presentation.Text, Is.EqualTo(text), phase.ToString());
					Assert.That(presentation.Icon, Is.EqualTo(icon), phase.ToString());
					Assert.That(presentation.ShowReady, Is.False, phase.ToString());
				});
			}

			client.MapPhase = Session.ClientMapPhase.Ready;
			client.MapProgress = 100;
			Assert.That(IosLobbyMapStatusPresentation.For(client, "map-a", true).ShowReady, Is.True);
			Assert.That(IosLobbyMapStatusPresentation.For(client, "map-b", true).ShowReady, Is.True);
			Assert.That(IosLobbyMapStatusPresentation.For(client, "map-a", false).ShowReady, Is.True);
			client.Bot = "normal";
			Assert.That(IosLobbyMapStatusPresentation.For(client, "map-a", true).ShowReady, Is.True);
		}

		[Test]
		public void LobbyPlayerTemplatesDeclareHiddenIosMapStatusWidgets()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "mods", "common",
				"chrome", "lobby-players.yaml"));

			Assert.Multiple(() =>
			{
				Assert.That(source.Split("Label@IOS_MAP_STATUS_TEXT:").Length - 1, Is.EqualTo(2));
				Assert.That(source.Split("Label@IOS_MAP_STATUS_ICON:").Length - 1, Is.EqualTo(2));
				Assert.That(source.Split("Font: IosBold").Length - 1, Is.GreaterThanOrEqualTo(4));
			});
		}

		[TestCaseSource(nameof(SupportedDevices))]
		public void IosMapStatusWidgetsReuseTheReadyCellWithoutChangingRowHeight(IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var layout = IosLobbyLayout.Create(policy.ContentBounds.Width, policy.ContentBounds.Height, policy);
			var ready = BareWidget<CheckboxWidget>("STATUS_CHECKBOX");
			var readyImage = BareWidget<ImageWidget>("STATUS_IMAGE");
			var statusText = Label("IOS_MAP_STATUS_TEXT");
			var statusIcon = Label("IOS_MAP_STATUS_ICON");
			var row = Container("TEMPLATE_EDITABLE_PLAYER", ready, readyImage, statusText, statusIcon);

			ApplyIosPlayerRowLayout(row, layout, policy);
			ApplyIosPlayerRowLayout(row, layout, policy);

			Assert.Multiple(() =>
			{
				Assert.That(row.Bounds.Height, Is.EqualTo(layout.PlayerRow.Height));
				Assert.That(ready.Bounds, Is.EqualTo(layout.PlayerReady));
				Assert.That(readyImage.Bounds, Is.EqualTo(layout.PlayerReady));
				Assert.That(statusText.Bounds, Is.EqualTo(layout.PlayerReady));
				Assert.That(statusIcon.Bounds, Is.EqualTo(layout.PlayerReady));
				AssertPhysicalTarget(statusText.Bounds, policy);
				AssertPhysicalTarget(statusIcon.Bounds, policy);
			});
		}

		[TestCaseSource(nameof(SupportedDevices))]
		public void IosFactionButtonUsesRealThreeTargetBoundsAndAspectFitFlag(IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var layout = IosLobbyLayout.Create(policy.ContentBounds.Width, policy.ContentBounds.Height, policy);
			var flag = BareWidget<ImageWidget>("FACTIONFLAG");
			var name = Label("FACTIONNAME");
			var faction = BareWidget<DropDownButtonWidget>("FACTION");
			faction.AddChild(flag);
			faction.AddChild(name);
			var row = Container("TEMPLATE_EDITABLE_PLAYER", faction);

			ApplyIosPlayerRowLayout(row, layout, policy);
			ApplyIosPlayerRowLayout(row, layout, policy);

			Assert.Multiple(() =>
			{
				Assert.That(faction.Bounds, Is.EqualTo(layout.PlayerFaction));
				Assert.That(faction.Bounds.Width / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(144));
				Assert.That(faction.Bounds.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
				Assert.That(flag.StretchToFit, Is.True);
				Assert.That(flag.Bounds.Width / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(40));
				Assert.That(flag.Bounds.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(20));
				Assert.That(flag.Bounds.Width, Is.EqualTo(2 * flag.Bounds.Height).Within(1));
				Assert.That(name.Bounds.X, Is.GreaterThanOrEqualTo(flag.Bounds.Right + layout.Gap));
				Assert.That(name.Bounds.Right, Is.LessThanOrEqualTo(faction.UsableWidth));
			});
		}

		[Test]
		public void LiveMapStatusBindingsAreMutuallyExclusiveAndDesktopSafe()
		{
			var client = new Session.Client
			{
				MapUid = "map-a",
				MapPhase = Session.ClientMapPhase.Downloading,
				MapProgress = 42
			};
			var ready = BareWidget<CheckboxWidget>("STATUS_CHECKBOX");
			ready.IsVisible = () => true;
			var statusText = Label("IOS_MAP_STATUS_TEXT");
			var statusIcon = Label("IOS_MAP_STATUS_ICON");
			var row = Container("TEMPLATE_EDITABLE_PLAYER", ready, statusText, statusIcon);

			LobbyUtils.SetupIosMapStatusWidgets(row, client, "map-a", true, "STATUS_CHECKBOX");
			Assert.Multiple(() =>
			{
				Assert.That(statusText.IsVisible(), Is.True);
				Assert.That(statusText.GetText(), Is.EqualTo("42%"));
				Assert.That(statusIcon.IsVisible(), Is.False);
				Assert.That(ready.IsVisible(), Is.False);
			});

			client.MapPhase = Session.ClientMapPhase.Error;
			client.MapProgress = -1;
			Assert.Multiple(() =>
			{
				Assert.That(statusText.IsVisible(), Is.False);
				Assert.That(statusIcon.IsVisible(), Is.True);
				Assert.That(statusIcon.GetText(), Is.EqualTo("!"));
			});

			client.MapPhase = Session.ClientMapPhase.Ready;
			client.MapProgress = 100;
			Assert.Multiple(() =>
			{
				Assert.That(statusText.IsVisible(), Is.False);
				Assert.That(statusIcon.IsVisible(), Is.False);
				Assert.That(ready.IsVisible(), Is.True);
			});

			client.MapPhase = Session.ClientMapPhase.Downloading;
			client.MapProgress = 42;
			var desktopReady = BareWidget<CheckboxWidget>("STATUS_CHECKBOX");
			desktopReady.IsVisible = () => true;
			var desktopText = Label("IOS_MAP_STATUS_TEXT");
			var desktopIcon = Label("IOS_MAP_STATUS_ICON");
			var desktopRow = Container("TEMPLATE_EDITABLE_PLAYER", desktopReady, desktopText, desktopIcon);
			LobbyUtils.SetupIosMapStatusWidgets(desktopRow, client, "map-a", false, "STATUS_CHECKBOX");
			Assert.Multiple(() =>
			{
				Assert.That(desktopText.IsVisible(), Is.False);
				Assert.That(desktopIcon.IsVisible(), Is.False);
				Assert.That(desktopReady.IsVisible(), Is.True);
			});
		}

		[Test]
		public void IosStartUsesCorrelatedStateWhileDesktopKeepsForceConfirmation()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "Lobby", "LobbyLogic.cs"));

			Assert.Multiple(() =>
			{
				StringAssert.Contains("startGameButton.IsDisabled = () => Platform.IsIOS", source);
				StringAssert.Contains("new LobbySafeStartRequestState(0)", source);
				StringAssert.Contains("request = safeStartState.Begin(currentMapUid)", source);
				StringAssert.Contains("Order.Command(request.ToCommand())", source);
				StringAssert.Contains("Game.RunAfterDelay(5000", source);
				StringAssert.Contains("safeStartState.TryTimeout(request.RequestId)", source);
				StringAssert.Contains("safeStartState.TryAcceptRejection(rejection,", source);
				StringAssert.Contains("safeStartState.Rejection.ToAvailability()", source);
				StringAssert.Contains("safeStartState.ObserveReadinessChange(clientIndex)", source);
				StringAssert.Contains("safeStartState.ObserveLobbySync(", source);
				StringAssert.Contains("safeStartState.Reset()", source);
				StringAssert.DoesNotContain("safeStartPending", source);
				StringAssert.DoesNotContain("safeStartRequestIdentity", source);
				StringAssert.Contains("panel = PanelType.ForceStart;", source,
					"Desktop must retain the upstream force-start confirmation.");
				StringAssert.Contains("Game.LobbyInfoChanged += StartStatusLobbyInfoChanged;", source);
				StringAssert.Contains(
					"orderManager.ClientMapReadinessChanged += ClientMapReadinessChanged;", source);
				StringAssert.Contains("orderManager.SafeStartRejected += SafeStartRejected;", source);
				StringAssert.Contains(
					"orderManager.ClientMapReadinessChanged -= ClientMapReadinessChanged;", source);
				StringAssert.Contains("orderManager.SafeStartRejected -= SafeStartRejected;", source);
				StringAssert.Contains("Game.LobbyInfoChanged -= StartStatusLobbyInfoChanged;", source);
			});
		}

		[TestCaseSource(nameof(SupportedDevices))]
		public void PlayableMapUsesAFullWidthPreviewBandAndSeparatedMetadata(IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var lobby = IosLobbyLayout.Create(policy.ContentBounds.Width, policy.ContentBounds.Height, policy);
			var createServer = IosServerCreationLayout.Create(policy);
			foreach (var card in new[] { Local(lobby.Map), Local(createServer.MapPreview) })
			{
				var large = IosMapPreviewLayout.Create(card.Width, card.Height, policy, false);
				var playableBand = 3 * policy.MinimumReadableTextHeight + 3 * policy.Gap;
				var expectedMapHeight = Math.Max(policy.MinimumTarget, card.Height - playableBand);

				Assert.That(large.MapSurface.X, Is.Zero);
				Assert.That(large.MapSurface.Width, Is.EqualTo(card.Width),
					"Playable maps should use the whole preview column instead of a small left-aligned square.");
				Assert.That(large.MapSurface.Height, Is.EqualTo(expectedMapHeight),
					"Playable map layout must reserve only title/type/author and their necessary gaps.");
				Assert.That(large.MapSurface.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(72),
					"The preview must be materially larger than a minimum-size touch icon.");
				Assert.That(large.MapPreview, Is.EqualTo(Inset(large.MapSurface, 1)));
				AssertVisibleRoles(card,
					large.MapSurface, large.Title, large.PrimaryText, large.SecondaryText);
			}
		}

		[TestCaseSource(nameof(SupportedTablets))]
		public void TabletLobbyKeepsTheMapColumnBesideTheUtilityBand(IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var layout = IosLobbyLayout.Create(policy.ContentBounds.Width, policy.ContentBounds.Height, policy);

			Assert.Multiple(() =>
			{
				Assert.That(layout.Chat.X, Is.EqualTo(layout.Players.X));
				Assert.That(layout.Chat.Right, Is.EqualTo(layout.Players.Right),
					"Chat and position controls belong to the player column, not underneath the map.");
				Assert.That(layout.Map.Y, Is.EqualTo(layout.Main.Y));
				Assert.That(layout.Map.Bottom + layout.Gap, Is.EqualTo(layout.ChangeMap.Y));
				Assert.That(layout.ChangeMap.Bottom, Is.EqualTo(layout.Main.Bottom),
					"The map column should use the full main height beside the utility band.");
			});
		}

		[TestCaseSource(nameof(SupportedDevices))]
		public void PositionManagerIsOneReadableRowAlignedWithChatInput(IosScreenSnapshot snapshot)
		{
			using var fonts = new ActualIosFontRenderer();
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var layout = IosLobbyLayout.Create(policy.ContentBounds.Width, policy.ContentBounds.Height, policy);
			var text = FluentValue("zh-CN", "chrome.ftl", "dropdownbutton-server-lobby-slots");
			var usableWidth = layout.Position.Width - layout.Position.Height;

			Assert.Multiple(() =>
			{
				Assert.That(layout.Position.Height, Is.EqualTo(policy.MinimumTarget));
				Assert.That(layout.Position.Y, Is.EqualTo(layout.Chat.Y + layout.ChatInput.Y));
				Assert.That(layout.Position.Bottom, Is.EqualTo(layout.Chat.Bottom));
				Assert.That(layout.ChatDisplay.X, Is.Zero);
				Assert.That(layout.ChatDisplay.Width, Is.EqualTo(layout.Chat.Width),
					"Chat history should use the full player-column width above the input row.");
				Assert.That(layout.Position.Right + layout.Gap, Is.LessThanOrEqualTo(layout.ChatInput.X));
				Assert.That(Game.Renderer.Fonts["IosBold"].Measure(text).X, Is.LessThanOrEqualTo(usableWidth),
					"The dropdown marker reservation must leave enough width for 位置管理.");
			});
		}

		[TestCaseSource(nameof(SupportedDevices))]
		public void LiveChatWidgetsConsumeTheOffsetInputRow(IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var layout = IosLobbyLayout.Create(policy.ContentBounds.Width, policy.ContentBounds.Height, policy);
			var chat = new ContainerWidget();
			var mode = new ContainerWidget { Id = "CHAT_MODE", Bounds = new WidgetBounds(7, 9, 11, 13) };
			var text = new ContainerWidget { Id = "CHAT_TEXTFIELD", Bounds = new WidgetBounds(17, 19, 23, 29) };
			chat.AddChild(mode);
			chat.AddChild(text);

			ApplyIosChatLayout(chat, layout, policy);

			Assert.Multiple(() =>
			{
				Assert.That(mode.Bounds.X, Is.EqualTo(layout.ChatInput.X));
				Assert.That(mode.Bounds.Y, Is.EqualTo(layout.ChatInput.Y));
				Assert.That(mode.Bounds.Height, Is.EqualTo(layout.ChatInput.Height));
				Assert.That(text.Bounds.X, Is.EqualTo(mode.Bounds.Right + policy.Gap));
				Assert.That(text.Bounds.Right, Is.EqualTo(layout.ChatInput.Right),
					"The live text field must consume the remainder of the offset input row.");
				Assert.That(text.Bounds.Y, Is.EqualTo(layout.ChatInput.Y));
				Assert.That(text.Bounds.Height, Is.EqualTo(layout.ChatInput.Height));
			});
		}

		[TestCaseSource(nameof(SupportedTablets))]
		public void TabletLobbyScrollButtonsStayFortyEightPointSquaresAndMeetThePlayerRows(
			IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var layout = IosLobbyLayout.Create(policy.ContentBounds.Width, policy.ContentBounds.Height, policy);
			var chatScroll = IosTouchWidgetPolicy.ScrollGeometry(
				layout.ChatDisplay.Width, layout.ChatDisplay.Height, policy.MinimumTarget,
				3 * layout.ChatDisplay.Height, policy.MinimumTarget);

			Assert.Multiple(() =>
			{
				Assert.That(layout.ChatDisplay.Height, Is.GreaterThanOrEqualTo(2 * policy.MinimumTarget));
				Assert.That(chatScroll.ButtonSize, Is.EqualTo(policy.MinimumTarget));
				Assert.That(layout.PlayerRow.Right, Is.EqualTo(layout.Players.Width - policy.MinimumTarget),
					"The player header/rows must meet the scrollbar instead of reserving a second empty gap.");
			});
		}

		[TestCaseSource(nameof(SupportedPhones))]
		public void PhoneSingleActionsAndDualStateUseFullWidthStateSpecificSlots(
			IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var card = Local(IosServerCreationLayout.Create(policy).MapPreview);
			var layout = IosMapPreviewLayout.Create(card.Width, card.Height, policy, true);
			Assert.That(layout.SingleAction.X, Is.EqualTo(layout.TextColumn.X));
			Assert.That(layout.SingleAction.Width, Is.EqualTo(layout.TextColumn.Width));
			AssertPhysicalTarget(layout.SingleAction, policy);
			Assert.That(layout.DualPrimaryAction.X, Is.EqualTo(layout.TextColumn.X));
			Assert.That(layout.DualPrimaryAction.Width, Is.EqualTo(layout.TextColumn.Width));
			Assert.That(layout.DualSecondaryAction.X, Is.EqualTo(layout.TextColumn.X));
			Assert.That(layout.DualSecondaryAction.Width, Is.EqualTo(layout.TextColumn.Width));
			Assert.That(layout.DualSecondaryAction.Bottom,
				Is.LessThanOrEqualTo(layout.DualPrimaryAction.Y));
			AssertPhysicalTarget(layout.DualPrimaryAction, policy);
			AssertPhysicalTarget(layout.DualSecondaryAction, policy);

			var host = BuildMapPreviewTree(card);
			var preview = Direct(host, "MAP_PREVIEW");
			IosMapPreviewLayout.Apply(host, policy);
			Assert.That(Direct(Direct(preview, "MAP_DOWNLOAD_AVAILABLE"), "MAP_INSTALL").Bounds,
				Is.EqualTo(layout.SingleAction));
			Assert.That(Direct(preview, "MAP_UPDATE").Bounds, Is.EqualTo(layout.SingleAction));
			Assert.That(Direct(preview, "MAP_RETRY").Bounds, Is.EqualTo(layout.SingleAction));

			IosMapPreviewLayout.ApplyActionState(host, policy, true);
			var update = Direct(preview, "MAP_UPDATE").Bounds;
			var install = Direct(Direct(preview, "MAP_UPDATE_DOWNLOAD_AVAILABLE"), "MAP_INSTALL").Bounds;
			Assert.That(update, Is.EqualTo(layout.DualPrimaryAction));
			Assert.That(install, Is.EqualTo(layout.DualSecondaryAction));
			Assert.That(update.ToRectangle().IntersectsWith(install.ToRectangle()), Is.False);
			AssertPhysicalTarget(update, policy);
			AssertPhysicalTarget(install, policy);

			IosMapPreviewLayout.ApplyActionState(host, policy, false);
			Assert.That(Direct(preview, "MAP_UPDATE").Bounds, Is.EqualTo(layout.SingleAction));
		}

		[TestCaseSource(nameof(PhoneLocales))]
		public void PhoneLocalizedActionsAndLongStatusesRoundTripWithoutElision(
			IosScreenSnapshot snapshot, string locale)
		{
			using var fonts = new ActualIosFontRenderer();
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var card = Local(IosServerCreationLayout.Create(policy).MapPreview);
			var layout = IosMapPreviewLayout.Create(card.Width, card.Height, policy, true);
			var host = BuildMapPreviewTree(card);
			var preview = Direct(host, "MAP_PREVIEW");

			var installText = FluentValue(locale, "chrome.ftl", "button-map-download-available-install");
			var dualInstallText = FluentValue(
				locale, "chrome.ftl", "button-map-update-download-available-install");
			var updateText = FluentValue(locale, "chrome.ftl", "button-map-preview-update");
			var retryText = FluentValue(locale, "common.ftl", "button-retry-install");
			var downloadInstall = (ButtonWidget)Direct(
				Direct(preview, "MAP_DOWNLOAD_AVAILABLE"), "MAP_INSTALL");
			var dualInstall = (ButtonWidget)Direct(
				Direct(preview, "MAP_UPDATE_DOWNLOAD_AVAILABLE"), "MAP_INSTALL");
			var update = (ButtonWidget)Direct(preview, "MAP_UPDATE");
			var retry = (ButtonWidget)Direct(preview, "MAP_RETRY");
			SetButtonText(downloadInstall, installText);
			SetButtonText(dualInstall, dualInstallText);
			SetButtonText(update, updateText);
			SetButtonText(retry, retryText);

			var unavailable = Direct(preview, "MAP_UNAVAILABLE");
			var updateAvailable = Direct(preview, "MAP_UPDATE_AVAILABLE");
			SetLabelText((LabelWidget)Direct(unavailable, "a"),
				FluentValue(locale, "chrome.ftl", "label-map-unavailable-a"));
			SetLabelText((LabelWidget)Direct(unavailable, "b"),
				FluentValue(locale, "chrome.ftl", "label-map-unavailable-b"));
			SetLabelText((LabelWidget)Direct(updateAvailable, "a"),
				FluentValue(locale, "chrome.ftl", "label-map-update-available-a"));
			SetLabelText((LabelWidget)Direct(updateAvailable, "b"),
				FluentValue(locale, "chrome.ftl", "label-map-update-available-b"));

			IosMapPreviewLayout.Apply(host, policy);
			Assert.Multiple(() =>
			{
				Assert.That(layout.StatusText.Width, Is.EqualTo(layout.TextColumn.Width));
				Assert.That(layout.SecondaryStatusText.Width, Is.EqualTo(layout.TextColumn.Width),
					"Both long-status lines need the full phone text column.");
				Assert.That(layout.SecondaryStatusText.ToRectangle()
					.IntersectsWith(layout.SingleAction.ToRectangle()), Is.False);
				foreach (var (button, expected) in new[]
				{
					(downloadInstall, installText), (update, updateText), (retry, retryText)
				})
				{
					Assert.That(button.Bounds, Is.EqualTo(layout.SingleAction));
					Assert.That(button.GetText(), Is.EqualTo(expected),
						$"{locale} single-action text must remain complete and unambiguous.");
				}

				foreach (var label in new[] { unavailable, updateAvailable }
					.SelectMany(section => section.Children.OfType<LabelWidget>()))
					AssertTwoLineRoundTrip(label, locale);
			});

			IosMapPreviewLayout.ApplyActionState(host, policy, true);
			Assert.Multiple(() =>
			{
				Assert.That(update.Bounds, Is.EqualTo(layout.DualPrimaryAction));
				Assert.That(dualInstall.Bounds, Is.EqualTo(layout.DualSecondaryAction));
				Assert.That(update.GetText(), Is.EqualTo(updateText),
					$"{locale} dual Update text must remain complete.");
				Assert.That(dualInstall.GetText(), Is.EqualTo(dualInstallText),
					$"{locale} dual Install text must remain complete.");
				AssertPositiveContained(layout.TextColumn, update.Bounds);
				AssertPositiveContained(layout.TextColumn, dualInstall.Bounds);
				Assert.That(update.Bounds.ToRectangle().IntersectsWith(dualInstall.Bounds.ToRectangle()), Is.False);
				Assert.That(update.Bounds.ToRectangle().IntersectsWith(layout.Title.ToRectangle()), Is.False);
				Assert.That(dualInstall.Bounds.ToRectangle().IntersectsWith(layout.Title.ToRectangle()), Is.False);
				Assert.That(update.Bounds.ToRectangle().IntersectsWith(layout.MapSurface.ToRectangle()), Is.False);
				Assert.That(dualInstall.Bounds.ToRectangle().IntersectsWith(layout.MapSurface.ToRectangle()), Is.False);
				AssertCompleteButtonText(update, updateText, locale);
				AssertCompleteButtonText(dualInstall, dualInstallText, locale);
			});
		}

		[TestCaseSource(nameof(PhoneLocales))]
		public void PhoneLocalizedDynamicMapStatusesAndRetrySearchRemainComplete(
			IosScreenSnapshot snapshot, string locale)
		{
			using var fonts = new ActualIosFontRenderer();
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var card = Local(IosServerCreationLayout.Create(policy).MapPreview);
			var layout = IosMapPreviewLayout.Create(card.Width, card.Height, policy, true);
			var host = BuildMapPreviewTree(card);
			var preview = Direct(host, "MAP_PREVIEW");
			var searching = (LabelWidget)Direct(preview, "MAP_SEARCHING");
			var error = (LabelWidget)Direct(preview, "MAP_ERROR");
			var validating = (LabelWidget)Direct(Direct(preview, "MAP_VALIDATING"), "MAP_STATUS_VALIDATING");
			var downloading = (LabelWidget)Direct(Direct(preview, "MAP_DOWNLOADING"), "MAP_STATUS_DOWNLOADING");
			var retry = (ButtonWidget)Direct(preview, "MAP_RETRY");

			SetLabelText(searching, FluentValue(locale, "chrome.ftl", "label-map-preview-searching"));
			SetLabelText(error, FluentValue(locale, "chrome.ftl", "label-map-preview-error"));
			SetLabelText(validating, FluentValue(locale, "chrome.ftl", "label-map-validating-status"));
			SetButtonText(retry, FluentValue(locale, "common.ftl", "button-retry-search"));
			IosMapPreviewLayout.Apply(host, policy);

			Assert.Multiple(() =>
			{
				foreach (var label in new[] { searching, error, validating })
				{
					Assert.That(label.Bounds, Is.EqualTo(layout.StatusText));
					AssertTwoLineRoundTrip(label, locale);
				}

				Assert.That(retry.Bounds, Is.EqualTo(layout.SingleAction));
				AssertCompleteButtonText(retry,
					FluentValue(locale, "common.ftl", "button-retry-search"), locale);
			});

			foreach (var text in new[]
			{
				FluentValue(locale, "common.ftl", "label-connecting"),
				FormatFluent(locale, "common.ftl", "label-downloading-map",
					("size", "9007199254740991")),
				FormatFluent(locale, "common.ftl", "label-downloading-map-progress",
					("size", "9007199254740991"), ("progress", "100"))
			})
			{
				SetLabelText(downloading, text);
				IosMapPreviewLayout.Apply(host, policy);
				Assert.That(downloading.Bounds, Is.EqualTo(layout.StatusText));
				AssertTwoLineRoundTrip(downloading, locale);
			}
		}

		[TestCaseSource(nameof(SupportedDevices))]
		public void PureMapLayoutsContainEveryVisibleRole(IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var lobby = IosLobbyLayout.Create(policy.ContentBounds.Width, policy.ContentBounds.Height, policy);
			var createServer = IosServerCreationLayout.Create(policy);
			foreach (var card in new[] { Local(lobby.Map), Local(createServer.MapPreview) })
			{
				var large = IosMapPreviewLayout.Create(card.Width, card.Height, policy, false);
				var small = IosMapPreviewLayout.Create(card.Width, card.Height, policy, true);

				AssertMapFrame(card, large, policy, false);
				AssertMapFrame(card, small, policy, true);
				AssertVisibleRoles(card, large.MapSurface, large.Title, large.PrimaryText, large.SecondaryText);
				AssertVisibleRoles(card, small.MapSurface, small.Title, small.StatusText, small.Progress);
				AssertVisibleRoles(card, small.MapSurface, small.Title,
					small.PrimaryText, small.SecondaryText, small.SingleAction);
				AssertVisibleRoles(card, small.MapSurface, small.Title,
					small.DualPrimaryAction, small.DualSecondaryAction);
				AssertVisibleRoles(card, small.MapSurface, small.Title, small.StatusText, small.SingleAction);
				AssertPhysicalTarget(small.SingleAction, policy);
				AssertPhysicalTarget(small.DualPrimaryAction, policy);
				AssertPhysicalTarget(small.DualSecondaryAction, policy);
			}
		}

		[TestCaseSource(nameof(SupportedDevices))]
		public void MapWidgetTreeLayoutsEveryStateAndIsIdempotent(IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var card = Local(IosServerCreationLayout.Create(policy).MapPreview);
			var host = BuildMapPreviewTree(card);
			var preview = Direct(host, "MAP_PREVIEW");
			var large = IosMapPreviewLayout.Create(card.Width, card.Height, policy, false);
			var small = IosMapPreviewLayout.Create(card.Width, card.Height, policy, true);

			IosMapPreviewLayout.Apply(host, policy);
			var firstGeometry = Descendants(host).Select(widget => widget.Bounds).ToArray();
			IosMapPreviewLayout.Apply(host, policy);
			Assert.That(Descendants(host).Select(widget => widget.Bounds), Is.EqualTo(firstGeometry),
				"Responsive map application must be deterministic when reapplied after resize/state work.");

			Assert.That(preview.Bounds, Is.EqualTo(card));
			foreach (var id in new[]
			{
				"MAP_LARGE", "MAP_SMALL", "MAP_AVAILABLE", "MAP_INCOMPATIBLE", "MAP_VALIDATING",
				"MAP_DOWNLOAD_AVAILABLE", "MAP_UPDATE_DOWNLOAD_AVAILABLE", "MAP_UNAVAILABLE",
				"MAP_DOWNLOADING", "MAP_UPDATE_AVAILABLE"
			})
				Assert.That(Direct(preview, id).Bounds, Is.EqualTo(card), $"{id} must cover the full card.");

			AssertPreviewSection(Direct(preview, "MAP_LARGE"), large);
			AssertPreviewSection(Direct(preview, "MAP_SMALL"), small);

			var available = Direct(preview, "MAP_AVAILABLE");
			Assert.That(Direct(available, "MAP_TYPE").Bounds, Is.EqualTo(large.PrimaryText));
			Assert.That(Direct(available, "MAP_AUTHOR").Bounds, Is.EqualTo(large.SecondaryText));
			var incompatible = Direct(preview, "MAP_INCOMPATIBLE");
			Assert.That(Direct(incompatible, "MAP_STATUS_A").Bounds, Is.EqualTo(large.PrimaryText));
			Assert.That(Direct(incompatible, "MAP_STATUS_B").Bounds, Is.EqualTo(large.SecondaryText));

			var validating = Direct(preview, "MAP_VALIDATING");
			Assert.That(Direct(validating, "MAP_STATUS_VALIDATING").Bounds, Is.EqualTo(small.StatusText));
			Assert.That(Direct(validating, "MAP_VALIDATING_BAR").Bounds, Is.EqualTo(small.Progress));
			var downloading = Direct(preview, "MAP_DOWNLOADING");
			Assert.That(Direct(downloading, "MAP_STATUS_DOWNLOADING").Bounds, Is.EqualTo(small.StatusText));
			Assert.That(Direct(downloading, "MAP_PROGRESSBAR").Bounds, Is.EqualTo(small.Progress));

			var download = Direct(preview, "MAP_DOWNLOAD_AVAILABLE");
			Assert.That(Direct(download, "MAP_TYPE").Bounds, Is.EqualTo(small.PrimaryText));
			Assert.That(Direct(download, "MAP_AUTHOR").Bounds, Is.EqualTo(small.SecondaryText));
			Assert.That(Direct(download, "MAP_INSTALL").Bounds, Is.EqualTo(small.SingleAction));
			IosMapPreviewLayout.ApplyActionState(host, policy, true);
			var updateDownload = Direct(preview, "MAP_UPDATE_DOWNLOAD_AVAILABLE");
			var install = Direct(updateDownload, "MAP_INSTALL").Bounds;
			var update = Direct(preview, "MAP_UPDATE").Bounds;
			Assert.That(install, Is.EqualTo(small.DualSecondaryAction));
			Assert.That(update, Is.EqualTo(small.DualPrimaryAction));
			Assert.That(install, Is.Not.EqualTo(update),
				"UpdateDownloadAvailable shows MAP_INSTALL and MAP_UPDATE together, so they need distinct slots.");
			Assert.That(install.ToRectangle().IntersectsWith(update.ToRectangle()), Is.False);

			Assert.That(Direct(preview, "MAP_SEARCHING").Bounds, Is.EqualTo(small.StatusText));
			Assert.That(Direct(preview, "MAP_ERROR").Bounds, Is.EqualTo(small.StatusText));
			IosMapPreviewLayout.ApplyActionState(host, policy, false);
			Assert.That(Direct(preview, "MAP_RETRY").Bounds, Is.EqualTo(small.SingleAction));
			var unavailable = Direct(preview, "MAP_UNAVAILABLE");
			AssertLongStatusPair(unavailable, Direct(preview, "MAP_RETRY"), small, policy);
			var updateAvailable = Direct(preview, "MAP_UPDATE_AVAILABLE");
			AssertLongStatusPair(updateAvailable, Direct(preview, "MAP_UPDATE"), small, policy);

			var wrappedStatusIds = new HashSet<string>
			{
				"MAP_SEARCHING", "MAP_ERROR", "MAP_STATUS_VALIDATING", "MAP_STATUS_DOWNLOADING"
			};
			foreach (var label in Descendants(host).OfType<LabelWidget>())
			{
				var isWrappedStatus = wrappedStatusIds.Contains(label.Id) ||
					(label.Id is "a" or "b" &&
						label.Parent?.Id is "MAP_UNAVAILABLE" or "MAP_UPDATE_AVAILABLE");
				Assert.That(label.WordWrap, Is.EqualTo(isWrappedStatus),
					isWrappedStatus ? $"{label.Id} must use its measured two-line slot."
						: $"{label.Id} must remain a single-line, elided label.");
				Assert.That(label.VAlign, Is.EqualTo(TextVAlign.Middle));
				Assert.That(label.Bounds.Width, Is.GreaterThan(0));
				Assert.That(label.Bounds.Height, Is.GreaterThan(0));
				if (isWrappedStatus)
					Assert.That(label.Bounds.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			}

			foreach (var button in Descendants(host).OfType<ButtonWidget>())
				AssertPhysicalTarget(button.Bounds, policy);
		}

		[TestCase(0, 220)]
		[TestCase(172, 0)]
		[TestCase(-7, 220)]
		[TestCase(172, -9)]
		public void MapPreviewApplyNoOpsForNonPositiveHostDimensions(int width, int height)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				new IosScreenSnapshot(new Size(1560, 720), new Size(932, 430),
					new IosSafeAreaInsets(59, 0, 59, 21)));
			var host = BuildMapPreviewTree(new WidgetBounds(0, 0, width, height));
			var before = Descendants(host).Select(widget => widget.Bounds).ToArray();

			IosMapPreviewLayout.Apply(host, policy);

			Assert.That(Descendants(host).Select(widget => widget.Bounds), Is.EqualTo(before),
				"An unmeasured host must remain untouched instead of creating clamped 1x1 child geometry.");
			Assert.That(Descendants(host).Any(widget =>
				widget.Bounds.Width == 1 || widget.Bounds.Height == 1), Is.False);
		}

		[TestCase(0, 220)]
		[TestCase(172, 0)]
		[TestCase(-7, 220)]
		[TestCase(172, -9)]
		public void MapPreviewActionStateNoOpsForNonPositiveHostDimensions(int width, int height)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				new IosScreenSnapshot(new Size(1560, 720), new Size(932, 430),
					new IosSafeAreaInsets(59, 0, 59, 21)));
			var host = BuildMapPreviewTree(new WidgetBounds(0, 0, width, height));
			var before = Descendants(host).Select(widget => widget.Bounds).ToArray();

			IosMapPreviewLayout.ApplyActionState(host, policy, true);

			Assert.That(Descendants(host).Select(widget => widget.Bounds), Is.EqualTo(before),
				"An unmeasured map host must not create clamped action geometry.");
			Assert.That(Descendants(host).Any(widget =>
				widget.Bounds.Width == 1 || widget.Bounds.Height == 1), Is.False);
		}

		[TestCase(0, 432)]
		[TestCase(327, 0)]
		[TestCase(-7, 432)]
		[TestCase(327, -9)]
		public void MultiplayerDetailsApplyNoOpsForNonPositiveHostDimensions(int width, int height)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				new IosScreenSnapshot(new Size(1560, 720), new Size(932, 430),
					new IosSafeAreaInsets(59, 0, 59, 21)));
			var selected = BuildSelectedServerTree(width, height);
			var before = Descendants(selected).Select(widget => widget.Bounds).ToArray();

			IosMultiplayerDetailsLayout.Apply(selected, policy, true);

			Assert.That(Descendants(selected).Select(widget => widget.Bounds), Is.EqualTo(before),
				"An unmeasured details host must not create clamped summary or client geometry.");
			Assert.That(Descendants(selected).Any(widget =>
				widget.Bounds.Width == 1 || widget.Bounds.Height == 1), Is.False);
		}

		[Test]
		public void ResponsiveApplicationsKeepChildCoordinatesLocalToNonZeroParents()
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				new IosScreenSnapshot(new Size(1560, 720), new Size(932, 430),
					new IosSafeAreaInsets(59, 0, 59, 21)));
			var mapHost = BuildMapPreviewTree(new WidgetBounds(73, 41, 327, 249));
			IosMapPreviewLayout.Apply(mapHost, policy);
			var mapCard = new WidgetBounds(0, 0, mapHost.Bounds.Width, mapHost.Bounds.Height);
			Assert.That(Direct(mapHost, "MAP_PREVIEW").Bounds, Is.EqualTo(mapCard));
			AssertPreviewSection(Direct(Direct(mapHost, "MAP_PREVIEW"), "MAP_SMALL"),
				IosMapPreviewLayout.Create(mapCard.Width, mapCard.Height, policy, true));

			var selected = BuildSelectedServerTree(327, 432);
			selected.Bounds = new WidgetBounds(91, 53, 327, 432);
			IosMultiplayerDetailsLayout.Apply(selected, policy, true);
			AssertSelectedTree(selected,
				IosMultiplayerDetailsLayout.Create(327, 432, policy, true));
		}

		[Test]
		public void DisabledPoliciesLeaveExistingDesktopGeometryUntouched()
		{
			var policy = IosMenuLayoutPolicy.Create(false, 900, 600);
			var map = BuildMapPreviewTree(new WidgetBounds(27, 31, 172, 220));
			var mapGeometry = Descendants(map).Select(widget => widget.Bounds).ToArray();
			IosMapPreviewLayout.Apply(map, policy);
			Assert.That(Descendants(map).Select(widget => widget.Bounds), Is.EqualTo(mapGeometry));

			var selected = BuildSelectedServerTree(174, 280);
			var selectedGeometry = Descendants(selected).Select(widget => widget.Bounds).ToArray();
			IosMultiplayerDetailsLayout.Apply(selected, policy, true);
			Assert.That(Descendants(selected).Select(widget => widget.Bounds), Is.EqualTo(selectedGeometry));

			var row = Container("TEMPLATE", Container("FLAG"), Label("LABEL"), Label("NOFLAG_LABEL"));
			var rowGeometry = Descendants(row).Select(widget => widget.Bounds).ToArray();
			IosMultiplayerDetailsLayout.ApplyClientRow(row, policy, false);
			Assert.That(Descendants(row).Select(widget => widget.Bounds), Is.EqualTo(rowGeometry));
		}

		[TestCaseSource(nameof(SupportedDevices))]
		public void LobbyAllocatesUsableNormalAndDedicatedServerSurfaces(IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var layout = IosLobbyLayout.Create(policy.ContentBounds.Width, policy.ContentBounds.Height, policy);
			var window = new WidgetBounds(0, 0, policy.ContentBounds.Width, policy.ContentBounds.Height);

			AssertPositiveContained(window, layout.Main);
			AssertPositiveContained(window, layout.Servers);
			Assert.That(layout.Servers, Is.EqualTo(layout.Main),
				"The Servers tab needs the complete main surface, not the short player-list region.");
			AssertPositiveContained(layout.Main, layout.Map);
			AssertPhysicalTarget(layout.Map, policy);
			AssertPositiveContained(Local(layout.Main), RelativeTo(layout.Players, layout.Main));
			Assert.That(RelativeTo(layout.Servers, layout.Main), Is.EqualTo(Local(layout.Main)));

			if (policy.IsPhone)
			{
				Assert.That(layout.Map.Y, Is.EqualTo(layout.Main.Y));
				Assert.That(layout.Map.Bottom, Is.EqualTo(layout.Main.Bottom));
				Assert.That(layout.Players.Right, Is.LessThanOrEqualTo(layout.Map.X));
				Assert.That(layout.Chat.Right, Is.LessThanOrEqualTo(layout.Map.X));
				AssertPositiveContained(layout.Footer, layout.Start);
				AssertPositiveContained(layout.Footer, layout.ChangeMap);
				AssertPositiveContained(layout.Footer, layout.Disconnect);
				Assert.That(layout.Start.Right, Is.LessThanOrEqualTo(layout.ChangeMap.X));
				Assert.That(layout.ChangeMap.Right, Is.LessThanOrEqualTo(layout.Disconnect.X));
				AssertPhysicalTarget(layout.Start, policy);
				AssertPhysicalTarget(layout.ChangeMap, policy);
				AssertPhysicalTarget(layout.Disconnect, policy);
			}
			else
			{
				Assert.That(layout.Map.Bottom, Is.LessThanOrEqualTo(layout.ChangeMap.Y));
				Assert.That(layout.ChangeMap.X, Is.EqualTo(layout.Map.X));
				Assert.That(layout.ChangeMap.Width, Is.EqualTo(layout.Map.Width));
			}
		}

		[TestCaseSource(nameof(SupportedDevices))]
		public void MultiplayerDetailsAreStateAwareContainedAndKeepJoinFixed(IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var browser = IosMultiplayerBrowserLayout.Create(policy);
			var summary = IosMultiplayerDetailsLayout.Create(browser.Details.Width, browser.Details.Height, policy, false);
			var clients = IosMultiplayerDetailsLayout.Create(browser.Details.Width, browser.Details.Height, policy, true);

			AssertDetailsLayout(summary, policy, false);
			AssertDetailsLayout(clients, policy, true);
			Assert.That(summary.Join, Is.EqualTo(clients.Join),
				"Summary -> Clients must not move Join back to the desktop Y=255 coordinate.");
			Assert.That(clients.DetailRows[4].Height, Is.Zero,
				"The hidden SELECTED_PLAYERS summary row must be reclaimed while real clients are visible.");
			Assert.That(clients.ClientList.Height, Is.GreaterThan(summary.ClientList.Height));
			Assert.That((clients.ClientList.Height - summary.ClientList.Height) / policy.LogicalPerPoint,
				Is.GreaterThanOrEqualTo(24));
			Assert.That(clients.ClientList.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(72),
				"Client details must fit a 24pt header and at least one complete 48pt player row.");
		}

		[TestCaseSource(nameof(SupportedDevices))]
		public void SelectedServerTreeSurvivesSummaryClientSummaryTransitions(IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var browser = IosMultiplayerBrowserLayout.Create(policy);
			var selected = BuildSelectedServerTree(browser.Details.Width, browser.Details.Height);
			var clientContainer = Direct(selected, "CLIENT_LIST_CONTAINER");
			var clientList = Direct(clientContainer, "MULTIPLAYER_CLIENT_LIST");
			var join = Direct(selected, "JOIN_BUTTON");

			IosMultiplayerDetailsLayout.Apply(selected, policy, false);
			var summaryGeometry = Descendants(selected).Select(widget => widget.Bounds).ToArray();
			var summaryJoin = join.Bounds;
			var summary = IosMultiplayerDetailsLayout.Create(selected.Bounds.Width, selected.Bounds.Height, policy, false);
			AssertSelectedTree(selected, summary);

			IosMultiplayerDetailsLayout.Apply(selected, policy, true);
			var clients = IosMultiplayerDetailsLayout.Create(selected.Bounds.Width, selected.Bounds.Height, policy, true);
			AssertSelectedTree(selected, clients);
			Assert.That(join.Bounds, Is.EqualTo(summaryJoin));
			Assert.That(Direct(selected, "SELECTED_PLAYERS").Bounds.Height, Is.Zero);
			Assert.That(clientList.Bounds, Is.EqualTo(new WidgetBounds(
				0, 0, clientContainer.Bounds.Width, clientContainer.Bounds.Height)));

			IosMultiplayerDetailsLayout.Apply(selected, policy, false);
			Assert.That(Descendants(selected).Select(widget => widget.Bounds), Is.EqualTo(summaryGeometry),
				"Summary -> Clients -> Summary must exactly restore the responsive summary geometry.");
			Assert.That(join.Bounds, Is.EqualTo(summaryJoin));
		}

		[TestCaseSource(nameof(SupportedDevices))]
		public void EmbeddedServerDetailsTolerateMissingClientListAndJoin(IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var browser = IosMultiplayerBrowserLayout.Create(policy);
			var selected = BuildEmbeddedSelectedServerTree(browser.Details.Width, browser.Details.Height);
			var beforeBottom = Direct(selected, "SELECTED_PLAYERS").Bounds.Bottom;

			Assert.DoesNotThrow(() => IosMultiplayerDetailsLayout.Apply(selected, policy, false));
			var expected = IosMultiplayerDetailsLayout.Create(
				selected.Bounds.Width, selected.Bounds.Height, policy, false, false);
			Assert.That(expected.Join.Height, Is.Zero);
			Assert.That(expected.ClientList.Bottom, Is.EqualTo(expected.Window.Bottom),
				"A missing Join action must not leave a reserved footer band.");
			Assert.That(Direct(selected, "MAP_BG").Bounds, Is.EqualTo(expected.MapBackground));
			Assert.That(Direct(selected, "SELECTED_PLAYERS").Bounds, Is.EqualTo(expected.DetailRows[4]));
			Assert.That(Direct(selected, "SELECTED_PLAYERS").Bounds.Bottom, Is.GreaterThan(beforeBottom));
		}

		[TestCaseSource(nameof(SupportedDevices))]
		public void EmbeddedRequestedClientsWithoutListKeepsPlayerSummary(IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var browser = IosMultiplayerBrowserLayout.Create(policy);
			var selected = BuildEmbeddedSelectedServerTree(browser.Details.Width, browser.Details.Height);

			IosMultiplayerDetailsLayout.Apply(selected, policy, true);

			var expected = IosMultiplayerDetailsLayout.Create(
				selected.Bounds.Width, selected.Bounds.Height, policy, false, false);
			Assert.That(Direct(selected, "SELECTED_PLAYERS").Bounds, Is.EqualTo(expected.DetailRows[4]));
			Assert.That(Direct(selected, "SELECTED_PLAYERS").Bounds.Height, Is.GreaterThan(0),
				"A server payload cannot reclaim the player summary when this embedded tree has no client-list surface.");
		}

		[Test]
		public void DynamicClientVisibilityControlsSummaryReclamation()
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				new IosScreenSnapshot(new Size(1560, 720), new Size(932, 430),
					new IosSafeAreaInsets(59, 0, 59, 21)));
			var browser = IosMultiplayerBrowserLayout.Create(policy);
			var selected = BuildSelectedServerTree(browser.Details.Width, browser.Details.Height);
			var dynamicList = Direct(Direct(selected, "CLIENT_LIST_CONTAINER"), "MULTIPLAYER_CLIENT_LIST");
			dynamicList.Visible = true;
			var dynamicallyVisible = false;
			dynamicList.IsVisible = () => dynamicallyVisible;

			IosMultiplayerDetailsLayout.Apply(
				selected, policy, EvaluateDynamicVisibility(dynamicList));
			Assert.That(Direct(selected, "SELECTED_PLAYERS").Bounds.Height, Is.GreaterThan(0));

			dynamicallyVisible = true;
			IosMultiplayerDetailsLayout.Apply(
				selected, policy, EvaluateDynamicVisibility(dynamicList));
			Assert.That(Direct(selected, "SELECTED_PLAYERS").Bounds.Height, Is.Zero);
		}

		[TestCaseSource(nameof(SupportedDevices))]
		public void ExistingClientRowsReflowOnEveryPhoneAndTabletRelayout(IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var browser = IosMultiplayerBrowserLayout.Create(policy);
			var selected = BuildScrollableSelectedServerTree(
				browser.Details.Width, browser.Details.Height, policy);
			var container = Direct(selected, "CLIENT_LIST_CONTAINER");
			var list = (ScrollPanelWidget)Direct(container, "MULTIPLAYER_CLIENT_LIST");

			IosMultiplayerDetailsLayout.Apply(selected, policy, true);
			AssertClientRowsReflowed(list, container, policy);

			selected.Bounds = new WidgetBounds(
				selected.Bounds.X, selected.Bounds.Y,
				Math.Max(policy.MinimumTarget * 2, selected.Bounds.Width - policy.MinimumTarget),
				selected.Bounds.Height);
			foreach (var row in list.Children)
				row.Bounds = new WidgetBounds(7, 9, 11, 13);

			IosMultiplayerDetailsLayout.Apply(selected, policy, true);
			AssertClientRowsReflowed(list, container, policy);
		}

		[TestCaseSource(nameof(SupportedDevices))]
		public void DynamicClientHeaderAndRowsUseCurrentResponsiveGeometry(IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var details = IosMultiplayerDetailsLayout.Create(
				IosMultiplayerBrowserLayout.Create(policy).Details.Width,
				IosMultiplayerBrowserLayout.Create(policy).Details.Height,
				policy, true);
			var headerLabel = Label("LABEL");
			headerLabel.Font = "TinyBold";
			var header = Container("HEADER", headerLabel);
			header.Bounds = new WidgetBounds(0, 0, details.ClientList.Width, 13);
			var playerLabel = Label("LABEL");
			playerLabel.Font = "Tiny";
			var noFlagLabel = Label("NOFLAG_LABEL");
			noFlagLabel.Font = "Tiny";
			var row = Container("TEMPLATE", Container("FLAG"), playerLabel, noFlagLabel);
			row.Bounds = new WidgetBounds(0, 0, details.ClientList.Width, 25);

			IosMultiplayerDetailsLayout.ApplyClientRow(header, policy, true);
			IosMultiplayerDetailsLayout.ApplyClientRow(row, policy, false);

			Assert.That(header.Bounds.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(24));
			Assert.That(row.Bounds.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			AssertPositiveContained(Local(header.Bounds), Direct(header, "LABEL").Bounds);
			var flag = Direct(row, "FLAG").Bounds;
			var name = Direct(row, "LABEL").Bounds;
			var noFlagName = Direct(row, "NOFLAG_LABEL").Bounds;
			AssertPositiveContained(Local(row.Bounds), flag);
			AssertPositiveContained(Local(row.Bounds), name);
			AssertPositiveContained(Local(row.Bounds), noFlagName);
			Assert.That(flag.Right, Is.LessThanOrEqualTo(name.X));
			Assert.That(headerLabel.Font, Is.EqualTo("IosBold"));
			Assert.That(playerLabel.Font, Is.EqualTo("IosRegular"));
			Assert.That(noFlagLabel.Font, Is.EqualTo("IosRegular"));
		}

		[TestCaseSource(nameof(SupportedDevices))]
		public void MultiplayerFooterAndServerRowsReserveEveryControl(IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var layout = IosMultiplayerBrowserLayout.Create(policy);
			var footer = Local(layout.Footer);
			var cells = new[]
			{
				layout.Filters, layout.Reload, layout.PlayerCount,
				layout.DirectConnect, layout.CreateButton, layout.Back
			};

			foreach (var cell in cells)
				AssertPositiveContained(footer, cell);
			for (var i = 1; i < cells.Length; i++)
				Assert.That(cells[i - 1].Right, Is.LessThanOrEqualTo(cells[i].X));
			foreach (var action in new[]
			{
				layout.Filters, layout.Reload, layout.DirectConnect, layout.CreateButton, layout.Back
			})
				AssertPhysicalTarget(action, policy);

			var titleColumn = layout.ServerColumns[0];
			AssertPositiveContained(titleColumn, layout.ServerTitle);
			AssertPositiveContained(titleColumn, layout.PasswordIcon);
			AssertPositiveContained(titleColumn, layout.AuthenticationIcon);
			Assert.That(layout.ServerTitle.Right, Is.LessThanOrEqualTo(layout.AuthenticationIcon.X));
			Assert.That(layout.AuthenticationIcon.Right, Is.LessThanOrEqualTo(layout.PasswordIcon.X),
				"A server can require both authentication and a password, so both icon slots must remain distinct.");

			var groupLabel = Label("LABEL");
			groupLabel.Font = "TinyBold";
			var groupHeader = Container("HEADER_TEMPLATE", groupLabel);
			groupHeader.Bounds = new WidgetBounds(0, 0, layout.ServerList.Width, 13);
			ApplyServerRow(groupHeader, layout.ServerColumns, policy.MinimumReadableTextHeight, policy);
			Assert.That(groupLabel.Bounds, Is.EqualTo(new WidgetBounds(
				layout.ServerColumns[0].X, 0,
				layout.ServerColumns[^1].Right - layout.ServerColumns[0].X,
				policy.MinimumReadableTextHeight)));
			Assert.That(groupLabel.Font, Is.EqualTo("IosBold"));
			Assert.That(groupLabel.VAlign, Is.EqualTo(TextVAlign.Middle));
			Assert.That(groupLabel.WordWrap, Is.False);
		}

		[TestCaseSource(nameof(SupportedDevices))]
		public void VisibleVersionNoticeReservesListAndProgressSurface(IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var list = IosMultiplayerBrowserLayout.Create(policy).ServerList;
			var noticeHeight = Math.Min(policy.MinimumReadableTextHeight, list.Height);
			var notice = new WidgetBounds(list.X, list.Y, list.Width, noticeHeight);
			var noticeWidget = Container("NOTICE_CONTAINER");
			noticeWidget.Visible = true;
			var dynamicallyVisible = false;
			noticeWidget.IsVisible = () => dynamicallyVisible;

			var hidden = IosMultiplayerBrowserLayout.ServerListWithNotice(
				list, noticeHeight, EvaluateDynamicVisibility(noticeWidget));
			dynamicallyVisible = true;
			var visible = IosMultiplayerBrowserLayout.ServerListWithNotice(
				list, noticeHeight, EvaluateDynamicVisibility(noticeWidget));

			Assert.That(hidden, Is.EqualTo(list));
			AssertPositiveContained(list, visible);
			Assert.That(visible.Y, Is.EqualTo(notice.Bottom));
			Assert.That(visible.Bottom, Is.EqualTo(list.Bottom));
			Assert.That(notice.ToRectangle().IntersectsWith(visible.ToRectangle()), Is.False,
				"The visible version notice must reserve space above both the server list and progress label.");
		}

		[Test]
		public void DesktopNoticeWatcherLeavesProgressGeometryUntouched()
		{
			var list = new WidgetBounds(7, 11, 300, 120);
			var progress = new WidgetBounds(17, 23, 280, 96);
			var desktopShown = ServerListLogic.ReflowNoticeSurfaces(
				list, progress, 48, true, false);
			var desktopHidden = ServerListLogic.ReflowNoticeSurfaces(
				desktopShown.ServerList, desktopShown.Progress, 48, false, false);
			var iosShown = ServerListLogic.ReflowNoticeSurfaces(
				list, progress, 48, true, true);
			var iosHidden = ServerListLogic.ReflowNoticeSurfaces(
				iosShown.ServerList, iosShown.Progress, 48, false, true);

			Assert.Multiple(() =>
			{
				Assert.That(desktopShown.ServerList, Is.EqualTo(new WidgetBounds(7, 59, 300, 72)));
				Assert.That(desktopShown.Progress, Is.EqualTo(progress),
					"The version-notice watcher must not alter desktop progress geometry.");
				Assert.That(desktopHidden.ServerList, Is.EqualTo(list));
				Assert.That(desktopHidden.Progress, Is.EqualTo(progress));
				Assert.That(iosShown.ServerList, Is.EqualTo(new WidgetBounds(7, 59, 300, 72)));
				Assert.That(iosShown.Progress, Is.EqualTo(new WidgetBounds(17, 71, 280, 48)));
				Assert.That(iosHidden.ServerList, Is.EqualTo(list));
				Assert.That(iosHidden.Progress, Is.EqualTo(progress));
			});
		}

		[Test]
		public void StandaloneSelectionReflowHandlesNoSelectionAndClientSelection()
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				new IosScreenSnapshot(new Size(1560, 720), new Size(932, 430),
					new IosSafeAreaInsets(59, 0, 59, 21)));
			var details = IosMultiplayerBrowserLayout.Create(policy).Details;
			var selected = BuildSelectedServerTree(details.Width, details.Height);

			var noSelectionHasClients = ServerListLogic.ReflowSelection(
				selected, policy, false, 0, true, true);
			var summary = IosMultiplayerDetailsLayout.Create(
				selected.Bounds.Width, selected.Bounds.Height, policy, false);
			Assert.That(noSelectionHasClients, Is.False);
			Assert.That(Direct(selected, "SELECTED_PLAYERS").Bounds,
				Is.EqualTo(summary.DetailRows[4]));
			Assert.That(Direct(selected, "SELECTED_PLAYERS").Bounds.Height, Is.GreaterThan(0));

			var selectionHasClients = ServerListLogic.ReflowSelection(
				selected, policy, true, 2, true, true);
			var clients = IosMultiplayerDetailsLayout.Create(
				selected.Bounds.Width, selected.Bounds.Height, policy, true);
			Assert.That(selectionHasClients, Is.True);
			Assert.That(Direct(selected, "SELECTED_PLAYERS").Bounds,
				Is.EqualTo(clients.DetailRows[4]));
			Assert.That(Direct(selected, "SELECTED_PLAYERS").Bounds.Height, Is.Zero);
		}

		[Test]
		public void RuntimeWiringUsesSharedLayoutsAndResponsiveStateTransitions()
		{
			var root = RepositoryRoot();
			var logic = Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic");
			var lobby = File.ReadAllText(Path.Combine(logic, "Lobby", "LobbyLogic.cs"));
			var touch = File.ReadAllText(Path.Combine(logic, "IosTouchMenuLogic.cs"));
			var mapPreview = File.ReadAllText(Path.Combine(logic, "Lobby", "MapPreviewLogic.cs"));
			var servers = File.ReadAllText(Path.Combine(logic, "ServerListLogic.cs"));

			Assert.That(lobby, Does.Contain("IosMapPreviewLayout.Apply"));
			Assert.That(touch, Does.Contain("IosMapPreviewLayout.Apply"));
			Assert.That(mapPreview, Does.Contain("IosMapPreviewLayout.ApplyActionState"),
				"The shared MAP_UPDATE button must switch between single and dual slots with its runtime state.");
			Assert.That(mapPreview, Does.Contain(
				"var titleCache = ResponsiveTextCache.WithTooltip(titleLabel);"));
			Assert.That(mapPreview, Does.Contain("var typeCache = new ResponsiveTextCache();"));
			Assert.That(mapPreview, Does.Contain("var truncateCache = new ResponsiveTextCache();"));
			Assert.That(mapPreview, Does.Contain(
				"titleCache.Update(getMap().Map.Title, titleLabel.Bounds.Width,"));
			Assert.That(mapPreview, Does.Contain(
				"typeCache.Update(getMap().Map.Categories.FirstOrDefault() ?? \"\", typeLabel.Bounds.Width,"));
			Assert.That(mapPreview, Does.Contain(
				"truncateCache.Update(authorCache.Update(getMap().Map.Author), authorLabel.Bounds.Width,"));
			Assert.That(lobby, Does.Not.Contain("ResizeMapPreview"));
			Assert.That(touch, Does.Not.Contain("LayoutMapPreview"));
			Assert.That(touch, Does.Contain("ServerListWithNotice"),
				"Resize reapplication must preserve the currently visible version-notice reservation.");
			Assert.That(touch, Does.Match(@"EvaluateDynamicVisibility\(notice\)"));
			Assert.That(touch, Does.Match(@"EvaluateDynamicVisibility\(dynamicClients\)"));
			Assert.That(lobby, Does.Contain("layout.Main"));
			Assert.That(lobby, Does.Contain("layout.Servers"));
			Assert.That(lobby, Does.Contain("surface.Y - layout.Main.Y"),
				"TOP_PANELS_ROOT children must use coordinates local to Main.");
			Assert.That(lobby, Does.Match(@"chatRoot\.IsVisible\s*=\s*\(\)\s*=>\s*panel\s*!=\s*PanelType\.Servers"));
			Assert.That(servers, Does.Contain("IosMultiplayerDetailsLayout.Apply"),
				"Server selection must reapply responsive summary/client geometry.");
			Assert.That(servers, Does.Contain("IosMultiplayerDetailsLayout.ApplyClientRow"),
				"Dynamic header/player clones must be adapted after cloning.");
			Assert.That(servers, Does.Contain(
				"var title = ResponsiveTextCache.WithTooltip(mapTitle);"));
			Assert.That(servers, Does.Contain("var version = new ResponsiveTextCache();"));
			Assert.That(servers, Does.Contain(
				"title.Update(currentMap.Title, mapTitle.Bounds.Width,"));
			Assert.That(servers, Does.Contain(
				"version.Update(currentServer.ModLabel, modVersion.Bounds.Width,"));
			Assert.That(servers, Does.Contain(
				"var playerName = new ResponsiveTextCache();"));
			Assert.That(servers, Does.Contain(
				"var titleText = ResponsiveTextCache.WithTooltip(title);"));
			Assert.That(servers, Does.Contain(
				"var locationText = new ResponsiveTextCache();"));
			Assert.That(servers, Does.Contain("var hasClients = ReflowSelection("),
				"Selection changes must run through the executable responsive reflow seam.");
			Assert.That(servers, Does.Contain("server != null, server?.Clients.Length ?? 0,"));
			Assert.That(servers, Does.Contain("clientContainer != null, clientList != null"),
				"Embedded server payloads must not request client geometry without an actual loaded client list.");
			Assert.That(servers, Does.Match(
				@"if\s*\(!Platform\.IsIOS(?:\s*&&[^)]*)?\)[\s\S]{0,240}joinButton\.Bounds\.Y\s*="),
				"Desktop may retain its original Join adjustment, but iOS must not restore Y=255.");
			Assert.That(servers, Does.Match(
				@"if\s*\(show\s*!=\s*showNotices\)[\s\S]{0,180}noticeContainer\.Bounds\.Height"),
				"Responsive notice height must be read when visibility changes, not cached from desktop YAML.");
			Assert.That(servers, Does.Contain("var noticeLayout = ReflowNoticeSurfaces("));
			Assert.That(servers, Does.Contain("containerHeight, show, Platform.IsIOS"),
				"Only iOS may reflow progress below a newly shown or hidden version notice.");
			Assert.That(servers, Does.Contain("serverList.Bounds = noticeLayout.ServerList;"));
			Assert.That(servers, Does.Contain("progressText.Bounds = noticeLayout.Progress;"));

			var embeddedChrome = File.ReadAllText(Path.Combine(
				root, "engine", "mods", "common", "chrome", "lobby-servers.yaml"));
			Assert.That(embeddedChrome, Does.Not.Contain("CLIENT_LIST_CONTAINER"));
			Assert.That(embeddedChrome, Does.Not.Contain("JOIN_BUTTON"));
		}

		[Test]
		public void ProductionResponsiveTextCacheRemeasuresUnchangedContentAfterWidthAndFontChanges()
		{
			using var fonts = new ActualIosFontRenderer();
			const string TestText = "Responsive multiplayer map status";
			var cache = new ResponsiveTextCache();
			var touchLabel = Game.Renderer.Fonts["IosTouchLabel"];
			var bold = Game.Renderer.Fonts["IosBold"];
			var wideWidth = touchLabel.Measure(TestText).X;
			var narrowWidth = Math.Max(1, wideWidth / 2);

			var narrow = cache.Update(TestText, narrowWidth, touchLabel);
			var wide = cache.Update(TestText, wideWidth, touchLabel);
			var fontChanged = cache.Update(TestText, wideWidth, bold);

			Assert.Multiple(() =>
			{
				Assert.That(narrow,
					Is.EqualTo(WidgetUtils.TruncateText(TestText, narrowWidth, touchLabel)));
				Assert.That(narrow, Is.Not.EqualTo(TestText));
				Assert.That(wide, Is.EqualTo(TestText),
					"Widening the same cache instance must restore unchanged content.");
				Assert.That(fontChanged,
					Is.EqualTo(WidgetUtils.TruncateText(TestText, wideWidth, bold)));
				Assert.That(fontChanged, Is.Not.EqualTo(wide),
					"Changing only the font must force the same cache instance to remeasure.");
			});
		}

		static void AssertMapFrame(
			WidgetBounds card, IosMapPreviewLayout layout, IosMenuLayoutPolicy policy, bool small)
		{
			AssertPositiveContained(card, layout.MapSurface);
			AssertPositiveContained(layout.MapSurface, layout.MapPreview);
			Assert.That(layout.MapPreview, Is.EqualTo(Inset(layout.MapSurface, 1)));
			AssertPositiveContained(card, layout.TextColumn);
			AssertPositiveContained(card, layout.Title);
			AssertPositiveContained(card, layout.PrimaryText);
			AssertPositiveContained(card, layout.SecondaryText);
			var textRoles = new List<WidgetBounds>
			{
				layout.Title, layout.PrimaryText, layout.SecondaryText
			};
			if (small)
			{
				AssertPositiveContained(card, layout.StatusText);
				AssertPositiveContained(card, layout.SecondaryStatusText);
				AssertPositiveContained(card, layout.Progress);
				AssertPositiveContained(card, layout.SingleAction);
				AssertPositiveContained(card, layout.DualPrimaryAction);
				AssertPositiveContained(card, layout.DualSecondaryAction);
				textRoles.AddRange(new[]
				{
					layout.StatusText, layout.Progress, layout.SingleAction,
					layout.DualPrimaryAction, layout.DualSecondaryAction
				});
			}

			foreach (var textRole in textRoles)
				AssertPositiveContained(layout.TextColumn, textRole);
			if (small)
			{
				Assert.That(layout.SingleAction.X, Is.EqualTo(layout.TextColumn.X));
				Assert.That(layout.SingleAction.Width, Is.EqualTo(layout.TextColumn.Width));
				Assert.That(layout.DualPrimaryAction.X, Is.EqualTo(layout.TextColumn.X));
				Assert.That(layout.DualPrimaryAction.Width, Is.EqualTo(layout.TextColumn.Width));
				Assert.That(layout.DualSecondaryAction.X, Is.EqualTo(layout.TextColumn.X));
				Assert.That(layout.DualSecondaryAction.Width, Is.EqualTo(layout.TextColumn.Width));
				Assert.That(layout.DualSecondaryAction.Bottom,
					Is.LessThanOrEqualTo(layout.DualPrimaryAction.Y));
				Assert.That(layout.DualPrimaryAction.ToRectangle()
					.IntersectsWith(layout.DualSecondaryAction.ToRectangle()), Is.False);
				Assert.That(layout.Title.ToRectangle()
					.IntersectsWith(layout.DualSecondaryAction.ToRectangle()), Is.False);
			}

			if (small)
			{
				Assert.That(layout.Progress.ToRectangle().IntersectsWith(layout.SingleAction.ToRectangle()), Is.False,
					"Progress must never occupy the single-action slot.");
			}

			if (policy.IsPhone && small)
				Assert.That(layout.MapSurface.Right, Is.LessThanOrEqualTo(layout.TextColumn.X));
			else
				Assert.That(layout.MapSurface.Bottom, Is.LessThanOrEqualTo(layout.TextColumn.Y));
			AssertPhysicalTarget(layout.MapSurface, policy);
			Assert.That(layout.Title.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(24));
			Assert.That(layout.PrimaryText.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(24));
			Assert.That(layout.SecondaryText.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(24));
			Assert.That(layout.StatusText.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
		}

		static void AssertPreviewSection(Widget section, IosMapPreviewLayout expected)
		{
			var background = Direct(section, "MAP_BG");
			Assert.That(background.Bounds, Is.EqualTo(expected.MapSurface));
			Assert.That(Direct(background, "MAP_PREVIEW").Bounds, Is.EqualTo(expected.MapPreview));
			Assert.That(Direct(section, "MAP_TITLE").Bounds, Is.EqualTo(expected.Title));
		}

		static void AssertLongStatusPair(
			Widget section, Widget action, IosMapPreviewLayout layout, IosMenuLayoutPolicy policy)
		{
			var first = (LabelWidget)Direct(section, "a");
			var second = (LabelWidget)Direct(section, "b");
			foreach (var label in new[] { first, second })
			{
				AssertPositiveContained(layout.TextColumn, label.Bounds);
				Assert.That(label.WordWrap, Is.True);
				Assert.That(label.VAlign, Is.EqualTo(TextVAlign.Middle));
				Assert.That(label.Bounds.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			}

			Assert.That(first.Bounds.Bottom, Is.LessThanOrEqualTo(second.Bounds.Y));
			Assert.That(second.Bounds.ToRectangle().IntersectsWith(action.Bounds.ToRectangle()), Is.False);
			Assert.That(action.Bounds, Is.EqualTo(layout.SingleAction));
			AssertPositiveContained(layout.TextColumn, action.Bounds);
		}

		static void AssertDetailsLayout(
			IosMultiplayerDetailsLayout layout, IosMenuLayoutPolicy policy, bool hasClients)
		{
			Assert.That(layout.DetailRows, Has.Length.EqualTo(5));
			AssertPositiveContained(layout.Window, layout.MapBackground);
			AssertPositiveContained(layout.MapBackground, layout.MapPreview);
			Assert.That(layout.MapPreview, Is.EqualTo(Inset(layout.MapBackground, 1)));
			AssertPhysicalTarget(layout.MapBackground, policy);
			AssertPositiveContained(layout.Window, layout.TextColumn);
			for (var i = 0; i < (hasClients ? 4 : 5); i++)
			{
				AssertPositiveContained(layout.Window, layout.DetailRows[i]);
				AssertPositiveContained(layout.TextColumn, layout.DetailRows[i]);
				Assert.That(layout.DetailRows[i].Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(24));
				if (i > 0)
					Assert.That(layout.DetailRows[i - 1].Bottom, Is.LessThanOrEqualTo(layout.DetailRows[i].Y));
			}

			AssertPositiveContained(layout.Window, layout.ClientList);
			AssertPositiveContained(layout.Window, layout.Join);
			AssertPhysicalTarget(layout.Join, policy);
			Assert.That(layout.Join.Bottom, Is.EqualTo(layout.Window.Bottom));
			var visible = new List<WidgetBounds> { layout.MapBackground, layout.ClientList, layout.Join };
			visible.AddRange(layout.DetailRows.Take(hasClients ? 4 : 5));
			AssertVisibleRoles(layout.Window, visible.ToArray());
			if (policy.IsPhone)
			{
				Assert.That(layout.MapBackground.Right, Is.LessThanOrEqualTo(layout.TextColumn.X));
				Assert.That(layout.TextColumn.Width, Is.GreaterThanOrEqualTo(2 * policy.MinimumTarget));
			}
			else
				Assert.That(layout.MapBackground.Bottom, Is.LessThanOrEqualTo(layout.TextColumn.Y));
		}

		static void AssertSelectedTree(Widget selected, IosMultiplayerDetailsLayout expected)
		{
			var background = Direct(selected, "MAP_BG");
			Assert.That(background.Bounds, Is.EqualTo(expected.MapBackground));
			Assert.That(Direct(background, "SELECTED_MAP_PREVIEW").Bounds, Is.EqualTo(expected.MapPreview));
			var ids = new[]
			{
				"SELECTED_MAP", "SELECTED_IP", "SELECTED_STATUS", "SELECTED_MOD_VERSION", "SELECTED_PLAYERS"
			};
			for (var i = 0; i < ids.Length; i++)
				Assert.That(Direct(selected, ids[i]).Bounds, Is.EqualTo(expected.DetailRows[i]));
			Assert.That(Direct(selected, "CLIENT_LIST_CONTAINER").Bounds, Is.EqualTo(expected.ClientList));
			Assert.That(Direct(selected, "JOIN_BUTTON").Bounds, Is.EqualTo(expected.Join));
		}

		static void AssertClientRowsReflowed(
			ScrollPanelWidget list, Widget clientContainer,
			IosMenuLayoutPolicy policy)
		{
			Assert.That(list.Bounds, Is.EqualTo(new WidgetBounds(
				0, 0, clientContainer.Bounds.Width, clientContainer.Bounds.Height)));
			var expectedWidth = Math.Max(1, list.Bounds.Width - list.ScrollbarWidth);
			if (!policy.IsPhone)
				Assert.That(list.ScrollbarWidth / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));

			for (var i = 0; i < list.Children.Count; i++)
			{
				var row = list.Children[i];
				Assert.That(row.Bounds.X, Is.Zero);
				Assert.That(row.Bounds.Width, Is.EqualTo(expectedWidth));
				Assert.That(row.Bounds.Height / policy.LogicalPerPoint,
					Is.GreaterThanOrEqualTo(i == 0 ? 24 : 48));
				if (i > 0)
					Assert.That(list.Children[i - 1].Bounds.Bottom, Is.LessThanOrEqualTo(row.Bounds.Y));
			}

			var headerLabel = (LabelWidget)Direct(list.Children[0], "LABEL");
			Assert.That(headerLabel.Font, Is.EqualTo("IosBold"));
			Assert.That(headerLabel.VAlign, Is.EqualTo(TextVAlign.Middle));
			foreach (var row in list.Children.Skip(1))
			{
				Assert.That(((LabelWidget)Direct(row, "LABEL")).Font, Is.EqualTo("IosRegular"));
				Assert.That(((LabelWidget)Direct(row, "NOFLAG_LABEL")).Font, Is.EqualTo("IosRegular"));
			}
		}

		static Widget BuildMapPreviewTree(WidgetBounds card)
		{
			var preview = Container("MAP_PREVIEW",
				PreviewSection("MAP_LARGE"),
				PreviewSection("MAP_SMALL"),
				Container("MAP_AVAILABLE", Label("MAP_TYPE"), Label("MAP_AUTHOR")),
				Container("MAP_INCOMPATIBLE", Label("MAP_STATUS_A"), Label("MAP_STATUS_B")),
				Container("MAP_VALIDATING", Label("MAP_STATUS_VALIDATING"), Progress("MAP_VALIDATING_BAR")),
				Container("MAP_DOWNLOAD_AVAILABLE", Label("MAP_TYPE"), Label("MAP_AUTHOR"), Button("MAP_INSTALL")),
				Button("MAP_UPDATE"),
				Container("MAP_UPDATE_DOWNLOAD_AVAILABLE", Button("MAP_INSTALL")),
				Label("MAP_SEARCHING"),
				Container("MAP_UNAVAILABLE", Label("a"), Label("b")),
				Label("MAP_ERROR"),
				Container("MAP_DOWNLOADING", Label("MAP_STATUS_DOWNLOADING"), Progress("MAP_PROGRESSBAR")),
				Button("MAP_RETRY"),
				Container("MAP_UPDATE_AVAILABLE", Label("a"), Label("b")));
			preview.Bounds = new WidgetBounds(17, 19, 172, 220);
			var host = Container("MAP_PREVIEW_ROOT", preview);
			host.Bounds = card;
			return host;
		}

		static Widget PreviewSection(string id)
		{
			return Container(id,
				Container("MAP_BG", Container("MAP_PREVIEW")),
				Label("MAP_TITLE"));
		}

		static Widget BuildSelectedServerTree(int width, int height)
		{
			var list = Container("MULTIPLAYER_CLIENT_LIST",
				Container("HEADER", Label("LABEL")),
				Container("TEMPLATE", Container("FLAG"), Label("LABEL"), Label("NOFLAG_LABEL")));
			var selected = Container("SELECTED_SERVER",
				Container("MAP_BG", Container("SELECTED_MAP_PREVIEW")),
				Label("SELECTED_MAP"), Label("SELECTED_IP"), Label("SELECTED_STATUS"),
				Label("SELECTED_MOD_VERSION"), Label("SELECTED_PLAYERS"),
				Container("CLIENT_LIST_CONTAINER", list), Button("JOIN_BUTTON"));
			selected.Bounds = new WidgetBounds(0, 0, width, height);
			return selected;
		}

		static Widget BuildScrollableSelectedServerTree(
			int width, int height, IosMenuLayoutPolicy policy)
		{
			var headerLabel = Label("LABEL");
			headerLabel.Font = "TinyBold";
			var header = Container("HEADER", headerLabel);
			var firstLabel = Label("LABEL");
			firstLabel.Font = "Tiny";
			var firstNoFlag = Label("NOFLAG_LABEL");
			firstNoFlag.Font = "Tiny";
			var first = Container("TEMPLATE", Container("FLAG"), firstLabel, firstNoFlag);
			var secondLabel = Label("LABEL");
			secondLabel.Font = "Tiny";
			var secondNoFlag = Label("NOFLAG_LABEL");
			secondNoFlag.Font = "Tiny";
			var second = Container("TEMPLATE", Container("FLAG"), secondLabel, secondNoFlag);
			var list = BareWidget<ScrollPanelWidget>("MULTIPLAYER_CLIENT_LIST");
			list.Layout = new ListLayout(list);
			list.TopBottomSpacing = 0;
			list.ItemSpacing = 0;
			list.ScrollBar = policy.IsPhone ? ScrollBar.Hidden : ScrollBar.Right;
			list.ScrollbarWidth = policy.IsPhone ? 0 : policy.MinimumTarget;
			list.Bounds = new WidgetBounds(0, 0, 17, 19);
			list.AddChild(header);
			list.AddChild(first);
			list.AddChild(second);

			var selected = Container("SELECTED_SERVER",
				Container("MAP_BG", Container("SELECTED_MAP_PREVIEW")),
				Label("SELECTED_MAP"), Label("SELECTED_IP"), Label("SELECTED_STATUS"),
				Label("SELECTED_MOD_VERSION"), Label("SELECTED_PLAYERS"),
				Container("CLIENT_LIST_CONTAINER", list), Button("JOIN_BUTTON"));
			selected.Bounds = new WidgetBounds(0, 0, width, height);
			return selected;
		}

		static Widget BuildEmbeddedSelectedServerTree(int width, int height)
		{
			var selected = Container("SELECTED_SERVER",
				Container("MAP_BG", Container("SELECTED_MAP_PREVIEW")),
				Label("SELECTED_MAP"), Label("SELECTED_IP"), Label("SELECTED_STATUS"),
				Label("SELECTED_MOD_VERSION"), Label("SELECTED_PLAYERS"));
			selected.Bounds = new WidgetBounds(47, 29, width, height);
			return selected;
		}

		static ContainerWidget Container(string id, params Widget[] children)
		{
			var container = new ContainerWidget
			{
				Id = id,
				Bounds = new WidgetBounds(11, 13, 17, 19)
			};
			foreach (var child in children)
				container.AddChild(child);
			return container;
		}

		static LabelWidget Label(string id)
		{
			var label = BareWidget<LabelWidget>(id);
			label.Bounds = new WidgetBounds(11, 13, 17, 19);
			label.Font = "IosRegular";
			label.WordWrap = true;
			label.VAlign = TextVAlign.Top;
			label.GetText = () => "A deliberately long translated label that requires elision";
			return label;
		}

		static ButtonWidget Button(string id)
		{
			var button = BareWidget<ButtonWidget>(id);
			button.Bounds = new WidgetBounds(11, 13, 17, 19);
			button.Font = "IosBold";
			button.LeftMargin = 5;
			button.RightMargin = 5;
			button.GetText = () => "Action";
			return button;
		}

		static void SetButtonText(ButtonWidget button, string text)
		{
			button.Font = "IosBold";
			button.LeftMargin = 5;
			button.RightMargin = 5;
			button.GetText = () => text;
		}

		static void SetLabelText(LabelWidget label, string text)
		{
			label.Text = text;
			label.Font = "Tiny";
			label.GetText = () => text;
		}

		static void AssertTwoLineRoundTrip(LabelWidget label, string locale)
		{
			var output = label.GetText();
			Assert.That(label.Font, Is.EqualTo("IosTouchLabel"),
				$"{locale} {label.Parent?.Id}/{label.Id} must use the compact iOS status font.");
			if (!label.Text.Contains("...", StringComparison.Ordinal))
				Assert.That(output, Does.Not.Contain("..."),
					$"{locale} {label.Parent?.Id}/{label.Id} must not be silently elided.");
			Assert.That(NormalizeDisplayText(output), Is.EqualTo(NormalizeDisplayText(label.Text)),
				$"{locale} {label.Parent?.Id}/{label.Id} must survive wrapping without content loss.");
			var lines = output.Split('\n');
			Assert.That(lines, Has.Length.LessThanOrEqualTo(2));
			var font = Game.Renderer.Fonts[label.Font];
			foreach (var line in lines)
				Assert.That(font.Measure(line).X, Is.LessThanOrEqualTo(label.Bounds.Width));
		}

		static void AssertCompleteButtonText(ButtonWidget button, string expected, string locale)
		{
			var output = button.GetText();
			Assert.That(output, Is.EqualTo(expected));
			Assert.That(output, Does.Not.Contain("..."),
				$"{locale} {button.Parent?.Id}/{button.Id} must not be silently elided.");
			var availableWidth = button.Bounds.Width - button.LeftMargin - button.RightMargin;
			Assert.That(Game.Renderer.Fonts[button.Font].Measure(output).X,
				Is.LessThanOrEqualTo(availableWidth));
		}

		static string NormalizeDisplayText(string text)
		{
			return string.Concat((text ?? "").Where(c => !char.IsWhiteSpace(c)));
		}

		static string FluentValue(string locale, string file, string key)
		{
			var relative = locale == "en-US" ? file : Path.Combine(locale, file);
			var path = Path.Combine(RepositoryRoot(),
				"engine", "mods", "common", "fluent", relative);
			var prefix = key + " = ";
			var line = File.ReadLines(path)
				.Single(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal));
			return line[prefix.Length..];
		}

		static string FormatFluent(
			string locale, string file, string key,
			params (string Name, string Value)[] arguments)
		{
			var value = FluentValue(locale, file, key);
			foreach (var argument in arguments)
				value = value.Replace("{ $" + argument.Name + " }", argument.Value, StringComparison.Ordinal);
			return value;
		}

		static ProgressBarWidget Progress(string id)
		{
			return new ProgressBarWidget
			{
				Id = id,
				Bounds = new WidgetBounds(11, 13, 17, 19)
			};
		}

		static T BareWidget<T>(string id) where T : Widget
		{
#pragma warning disable SYSLIB0050
			var widget = (T)FormatterServices.GetUninitializedObject(typeof(T));
#pragma warning restore SYSLIB0050
			typeof(Widget).GetField(nameof(Widget.Children))!.SetValue(widget, new List<Widget>());
			widget.Id = id;
			return widget;
		}

		static IEnumerable<Widget> Descendants(Widget root)
		{
			yield return root;
			foreach (var child in root.Children)
				foreach (var descendant in Descendants(child))
					yield return descendant;
		}

		static Widget Direct(Widget parent, string id)
		{
			return parent.Children.Single(child => child.Id == id);
		}

		static WidgetBounds Local(WidgetBounds bounds)
		{
			return new WidgetBounds(0, 0, bounds.Width, bounds.Height);
		}

		static WidgetBounds RelativeTo(WidgetBounds bounds, WidgetBounds parent)
		{
			return new WidgetBounds(
				bounds.X - parent.X, bounds.Y - parent.Y, bounds.Width, bounds.Height);
		}

		static WidgetBounds Inset(WidgetBounds bounds, int inset)
		{
			return new WidgetBounds(bounds.X + inset, bounds.Y + inset,
				bounds.Width - 2 * inset, bounds.Height - 2 * inset);
		}

		static void AssertVisibleRoles(WidgetBounds outer, params WidgetBounds[] roles)
		{
			for (var i = 0; i < roles.Length; i++)
			{
				AssertPositiveContained(outer, roles[i]);
				for (var j = 0; j < i; j++)
					Assert.That(roles[i].ToRectangle().IntersectsWith(roles[j].ToRectangle()), Is.False,
						$"Visible roles {roles[j].ToRectangle()} and {roles[i].ToRectangle()} overlap.");
			}
		}

		static void AssertPositiveContained(WidgetBounds outer, WidgetBounds inner)
		{
			Assert.That(inner.Width, Is.GreaterThan(0));
			Assert.That(inner.Height, Is.GreaterThan(0));
			Assert.That(outer.ToRectangle().Contains(inner.ToRectangle()), Is.True,
				$"Expected {inner.ToRectangle()} inside {outer.ToRectangle()}.");
		}

		static void AssertPhysicalTarget(WidgetBounds bounds, IosMenuLayoutPolicy policy)
		{
			Assert.That(bounds.Width / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(bounds.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
		}

		static bool EvaluateDynamicVisibility(Widget widget)
		{
			var method = typeof(IosTouchMenuLogic).GetMethod(
				"EvaluateDynamicVisibility", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null,
				"iOS relayout needs one evaluable dynamic-visibility seam shared by notice and client-list decisions.");
			return (bool)method!.Invoke(null, new object[] { widget })!;
		}

		static void ApplyServerRow(
			Widget row, WidgetBounds[] columns, int height, IosMenuLayoutPolicy policy)
		{
			var method = typeof(IosMultiplayerBrowserLayout).GetMethod(
				"ApplyServerRow", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null,
				"Server data rows and group headers need one executable responsive row applicator.");
			method!.Invoke(null, new object[] { row, columns, height, policy });
		}

		static void ApplyIosChatLayout(
			Widget chat, IosLobbyLayout layout, IosMenuLayoutPolicy policy)
		{
			var method = typeof(LobbyLogic).GetMethod(
				"LayoutIosChat", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null,
				"The live lobby chat tree must share the responsive offset-input layout.");
			method!.Invoke(null, new object[] { chat, layout, policy });
		}

		static void ApplyIosPlayerRowLayout(
			Widget row, IosLobbyLayout layout, IosMenuLayoutPolicy policy, bool isSkirmish = false)
		{
			var method = typeof(LobbyLogic).GetMethod(
				"LayoutIosPlayerRow", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null,
				"The live lobby row must share the tested iOS status-cell layout.");
			method!.Invoke(null, new object[] { row, layout, policy, isSkirmish });
		}

		static TestCaseData Device(
			string name, int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			return new TestCaseData(new IosScreenSnapshot(
				new Size(effectiveWidth, effectiveHeight),
				new Size(nativeWidth, nativeHeight),
				new IosSafeAreaInsets(safeLeft, safeTop, safeRight, safeBottom))).SetName(name + "-{m}");
		}

		static TestCaseData PhoneLocale(
			string name, string locale,
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var snapshot = new IosScreenSnapshot(
				new Size(effectiveWidth, effectiveHeight),
				new Size(nativeWidth, nativeHeight),
				new IosSafeAreaInsets(safeLeft, safeTop, safeRight, safeBottom));
			return new TestCaseData(snapshot, locale).SetName(name + "-" + locale + "-{m}");
		}

		sealed class ActualIosFontRenderer : IDisposable
		{
			readonly Renderer previousRenderer;
			readonly SheetBuilder sheetBuilder;
			readonly SpriteFont touchLabel;
			readonly SpriteFont regular;
			readonly SpriteFont bold;

			public ActualIosFontRenderer()
			{
				previousRenderer = Game.Renderer;
				Log.AddChannel("perf", null);
				var fontData = File.ReadAllBytes(Path.Combine(
					RepositoryRoot(), "mods", "ra2", "fonts", "NotoSansCJKsc-Bold.otf"));
				var platform = new DefaultPlatform();
				sheetBuilder = new SheetBuilder(SheetType.BGRA, 2048);
				touchLabel = new SpriteFont(platform, "IosTouchLabel", fontData, 20, 16, 1f, sheetBuilder);
				regular = new SpriteFont(platform, "IosRegular", fontData, 24, 19, 1f, sheetBuilder);
				bold = new SpriteFont(platform, "IosBold", fontData, 26, 20, 1f, sheetBuilder);
#pragma warning disable SYSLIB0050
				var renderer = (Renderer)FormatterServices.GetUninitializedObject(typeof(Renderer));
#pragma warning restore SYSLIB0050
				renderer.Fonts = new Dictionary<string, SpriteFont>
				{
					{ "IosTouchLabel", touchLabel },
					{ "IosRegular", regular },
					{ "IosBold", bold }
				};
				Game.Renderer = renderer;
			}

			public void Dispose()
			{
				Game.Renderer = previousRenderer;
				touchLabel.Dispose();
				regular.Dispose();
				bold.Dispose();
				sheetBuilder.Dispose();
			}
		}

		static string RepositoryRoot()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (root != null &&
				!Directory.Exists(Path.Combine(root, ".git")) &&
				!File.Exists(Path.Combine(root, ".git")))
				root = Directory.GetParent(root)?.FullName;

			Assert.That(root, Is.Not.Null, "Could not locate repository root.");
			return root!;
		}
	}
}
