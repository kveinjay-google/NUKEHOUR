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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Platforms.Default;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosMenuLayoutPolicyTest
	{
		[Test]
		public void MainMenuLanguageSelectionSavesBeforeSchedulingAnEffectiveLanguageReload()
		{
			var settings = new GameSettings { Language = LanguageSelectionPolicy.SystemPreference };
			var callbacks = new List<string>();

			var reload = MainMenuLogic.ApplyLanguageSelection(
				settings,
				"zh-Hans",
				LanguageSelectionPolicy.English,
				LanguageSelectionPolicy.SimplifiedChinese,
				() =>
				{
					Assert.That(settings.Language, Is.EqualTo(LanguageSelectionPolicy.SimplifiedChinese));
					callbacks.Add("save");
				},
				() => callbacks.Add("reload"));

			Assert.That(reload, Is.True);
			Assert.That(callbacks, Is.EqualTo(new[] { "save", "reload" }));
		}

		[TestCase("zh-Hans", "System", "zh-CN")]
		[TestCase("zh-Hans", "zh-CN", "System")]
		public void MainMenuLanguageSelectionOnlySavesWhenTheEffectiveLanguageIsUnchanged(
			string systemLanguageTag, string initialPreference, string selectedPreference)
		{
			var settings = new GameSettings { Language = initialPreference };
			var callbacks = new List<string>();

			var reload = MainMenuLogic.ApplyLanguageSelection(
				settings,
				systemLanguageTag,
				LanguageSelectionPolicy.SimplifiedChinese,
				selectedPreference,
				() => callbacks.Add("save"),
				() => callbacks.Add("reload"));

			Assert.That(reload, Is.False);
			Assert.That(settings.Language, Is.EqualTo(selectedPreference));
			Assert.That(callbacks, Is.EqualTo(new[] { "save" }));
		}

		[Test]
		public void Ra2MainMenuUsesOneAlignedSevenControlDesktopGrid()
		{
			var yaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "chrome", "mainmenu.yaml"));
			var ids = new[]
			{
				"SINGLEPLAYER_BUTTON", "MULTIPLAYER_BUTTON", "SETTINGS_BUTTON",
				"LANGUAGE_BUTTON", "CONTENT_BUTTON", "ABOUT_BUTTON", "QUIT_BUTTON"
			};
			var expectedY = new[] { 36, 92, 148, 204, 260, 316, 372 };
			var bounds = ids.Select(id => MainMenuControlBounds(yaml, id)).ToArray();

			for (var i = 0; i < bounds.Length; i++)
			{
				Assert.That(bounds[i], Is.EqualTo(new WidgetBounds(28, expectedY[i], 264, 46)), ids[i]);
				var body = MainMenuControlBody(yaml, ids[i]);
				Assert.That(body, Does.Contain("VisualHeight: 0"), ids[i]);
				Assert.That(body, Does.Contain("Background: main-menu-button"), ids[i]);
				Assert.That(body, Does.Contain("Font: Bold"), ids[i]);
				if (i > 0)
					Assert.That(bounds[i].Y - bounds[i - 1].Bottom, Is.EqualTo(10), ids[i]);
			}

			Assert.That(bounds[^1].Bottom, Is.LessThanOrEqualTo(420));
			Assert.That(yaml, Does.Contain("Button@LANGUAGE_BUTTON:"));
			Assert.That(yaml, Does.Contain("Background: main-menu-button"));
			Assert.That(yaml, Does.Contain("Font: Bold"));
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1133, 744, 1133, 744, 0, 0, 0, 20)]
		[TestCase(1024, 768, 1024, 768, 0, 0, 0, 20)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		[TestCase(1366, 1024, 1366, 1024, 0, 0, 0, 20)]
		public void IosMainMenuPlacesSixPhysicalTargetsInsideTheSafeContentWithoutOverlap(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
					safeLeft, safeTop, safeRight, safeBottom));
			var layout = IosMainMenuLayout.Create(policy);

			Assert.That(policy.ContentBounds.ToRectangle().Contains(layout.Panel.ToRectangle()), Is.True);
			Assert.That(layout.Panel.X, Is.EqualTo(Math.Clamp(
				72, policy.ContentBounds.X, policy.ContentBounds.Right - layout.Panel.Width)));
			Assert.That(layout.Controls, Has.Length.EqualTo(6));
			for (var i = 0; i < layout.Controls.Length; i++)
			{
				var control = layout.Controls[i];
				AssertPhysicalTarget(control, policy);
				Assert.That(new Rectangle(0, 0, layout.Panel.Width, layout.Panel.Height)
					.Contains(control.ToRectangle()), Is.True);
				if (i > 0)
					Assert.That(layout.Controls[i - 1].Bottom, Is.LessThanOrEqualTo(control.Y));
			}
		}

		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		public void IosMainMenuRedistributesFourControlsInsideAnAirierOriginalFrame(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
					safeLeft, safeTop, safeRight, safeBottom));
			var six = IosMainMenuLayout.Create(policy, 6);
			var four = IosMainMenuLayout.Create(policy, 4);

			Assert.Multiple(() =>
			{
				Assert.That(four.Panel, Is.EqualTo(six.Panel),
					"Removing Quit and Content must not collapse the established main-menu frame.");
				Assert.That(four.Controls, Has.Length.EqualTo(4));
				Assert.That(four.Controls.Select(control => control.Height), Has.All.GreaterThan(six.Controls[0].Height),
					"The remaining buttons should grow instead of leaving an empty sixth row.");
				Assert.That(four.Controls.Max(control => control.Height) -
					four.Controls.Min(control => control.Height), Is.LessThanOrEqualTo(1));
				Assert.That(four.Controls[0].Y, Is.GreaterThanOrEqualTo(six.Controls[0].Y));
				Assert.That(four.Controls[0].Y,
					Is.GreaterThanOrEqualTo((int)Math.Ceiling(20 * policy.LogicalPerPoint)),
					"The main-menu frame needs breathing room above and below its controls.");
				Assert.That(four.Panel.Height - four.Controls[^1].Bottom,
					Is.EqualTo(four.Controls[0].Y).Within(1),
					"The expanded frame inset should remain vertically balanced.");
			});

			for (var i = 1; i < four.Controls.Length; i++)
				Assert.That(four.Controls[i].Y - four.Controls[i - 1].Bottom, Is.EqualTo(policy.Gap));
		}

		[TestCase("System", "zh-CN", "自动")]
		[TestCase("system", "en", "Auto")]
		[TestCase("zh-CN", "en", "简体中文")]
		[TestCase("en", "zh-CN", "English")]
		public void IosMainMenuUsesCompactLanguageNames(
			string preference, string uiLanguage, string expected)
		{
			Assert.That(MainMenuLogic.GetIosLanguageDisplayName(preference, uiLanguage), Is.EqualTo(expected));
		}

		[TestCase("System", "zh-CN")]
		[TestCase("system", "zh-CN")]
		[TestCase("zh-CN", "en")]
		[TestCase("en", "System")]
		[TestCase("unsupported", "zh-CN")]
		public void MainMenuLanguageButtonCyclesThroughSupportedPreferences(
			string preference, string expected)
		{
			Assert.That(MainMenuLogic.NextLanguagePreference(preference), Is.EqualTo(expected));
		}

		[Test]
		public void IosMainMenuMeasuredLanguageLabelsFitTheButtonAndGenericPopupOnEveryDevice()
		{
			using var fonts = new ActualIosMenuFonts();
			var labels = new[] { "Auto (System)", "自动（跟随系统）", "简体中文", "English" };
			var snapshots = new[]
			{
				Snapshot(1560, 720, 932, 430, 59, 0, 59, 21),
				Snapshot(1558, 720, 844, 390, 47, 0, 47, 21),
				Snapshot(1133, 744, 1133, 744, 0, 0, 0, 20),
				Snapshot(1024, 768, 1024, 768, 0, 0, 0, 20),
				Snapshot(1180, 820, 1180, 820, 0, 0, 0, 20),
				Snapshot(1366, 1024, 1366, 1024, 0, 0, 0, 20),
			};
			var dropdownTemplate = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "mods", "common", "chrome", "dropdowns.yaml"));
			Assert.That(dropdownTemplate, Does.Contain("X: 10"));
			Assert.That(dropdownTemplate, Does.Contain("Width: PARENT_WIDTH - 20"));

			foreach (var snapshot in snapshots)
			{
				var policy = IosMenuLayoutPolicy.Create(true, snapshot);
				var dropdown = IosMainMenuLayout.Create(policy).Controls[3];
				var buttonUsableWidth = dropdown.Width - dropdown.Height;
				var popupWidth = IosTouchWidgetPolicy.DropDownPanelWidth(
					dropdown.Width, policy.MinimumTarget, snapshot.SafeBounds.Width);
				var popupItem = IosTouchWidgetPolicy.DropDownContentBounds(
					popupWidth, policy.MinimumTarget, 2);
				var popupLabelWidth = Math.Max(0, popupItem.Width - 20);

				foreach (var label in labels)
				{
					Assert.That(fonts.Bold.Measure(label).X, Is.LessThanOrEqualTo(buttonUsableWidth),
						$"{snapshot.NativePointSize}: main-menu button truncates {label}.");
					Assert.That(fonts.Regular.Measure(label).X, Is.LessThanOrEqualTo(popupLabelWidth),
						$"{snapshot.NativePointSize}: generic popup truncates {label}.");
				}

				Assert.That(dropdown.Width, Is.EqualTo(policy.IsPhone ?
					Math.Max(264, 4 * policy.MinimumTarget) : 264));
			}
		}

		[Test]
		public void MainMenuLogicOwnsIosRelayoutAndKeepsTheLanguageControlOptional()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "MainMenuLogic.cs"));

			Assert.That(source, Does.Contain("GetOrNull<ButtonWidget>(\"LANGUAGE_BUTTON\")"));
			Assert.That(source, Does.Contain("NextLanguagePreference(Game.Settings.Game.Language)"));
			Assert.That(source, Does.Not.Contain("ShowLanguageDropdown"));
			Assert.That(source, Does.Contain("IosScreenMetrics.SnapshotFor"));
			Assert.That(source, Does.Contain("IosTouchMenuLogic.ApplyIosFonts(mainMenu)"));
			Assert.That(source, Does.Contain("IosTouchWidgetPolicy.RelativeToRoot"));
			Assert.That(source, Does.Contain("mainMenu.Parent.RenderOrigin"));
			Assert.That(source, Does.Contain("contentButton.Visible = !Platform.IsIOS;"));
			Assert.That(source, Does.Contain("if (contentButton != null && contentButton.Visible)"));
			Assert.That(source, Does.Not.Contain("menus.Bounds ="));
			Assert.That(source, Does.Contain("public override void Tick()"));
			var yaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "chrome", "mainmenu.yaml"));
			Assert.That(yaml, Does.Not.Contain("Logic: MainMenuLogic, IosTouchMenuLogic"));
			Assert.That(yaml, Does.Contain("Button@LANGUAGE_BUTTON:"));
			Assert.That(yaml, Does.Not.Contain("DropDownButton@LANGUAGE_DROPDOWN:"));
			Assert.That(yaml, Does.Contain("Background: main-menu-button"));

			var chrome = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "chrome.yaml"));
			foreach (var style in new[]
			{
				"main-menu-button:", "main-menu-button-hover:",
				"main-menu-button-pressed:", "main-menu-button-disabled:"
			})
				Assert.That(chrome, Does.Contain(style), style);

			var mainMenuButton = Regex.Match(chrome,
				@"(?ms)^main-menu-button:\s*\n(?<body>.*?)(?=^\S|\z)");
			Assert.That(mainMenuButton.Success, Is.True);
			Assert.That(mainMenuButton.Groups["body"].Value, Does.Not.Contain("PanelRegion:"));
			Assert.That(mainMenuButton.Groups["body"].Value, Does.Contain("background: 64, 64, 1, 1"));
			Assert.That(mainMenuButton.Groups["body"].Value, Does.Contain("border-l: 0, 64, 4, 1"));
			Assert.That(mainMenuButton.Groups["body"].Value, Does.Contain("border-r: 124, 64, 4, 1"));
		}

		[Test]
		public void IosMainMenuPanelConvertsFromScreenSpaceWithoutAssumingAZeroParentOrigin()
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(1558, 720, 844, 390, 47, 0, 47, 21));
			var panel = IosMainMenuLayout.Create(policy).Panel.ToRectangle();
			var parentOrigin = new int2(72, 200);
			var relative = IosTouchWidgetPolicy.RelativeToRoot(panel, parentOrigin);

			Assert.That(relative.X + parentOrigin.X, Is.EqualTo(panel.X));
			Assert.That(relative.Y + parentOrigin.Y, Is.EqualTo(panel.Y));
			Assert.That(relative.Size, Is.EqualTo(panel.Size));
		}

		[Test]
		public void MainMenuTickDetectsWhenExternalLayoutRecalculationResetsAppliedBounds()
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(1558, 720, 844, 390, 47, 0, 47, 21));
			var layout = IosMainMenuLayout.Create(policy);
			var actualControls = layout.Controls.Select(bounds =>
				(Widget)new ContainerWidget { Bounds = bounds }).ToArray();
			Assert.That(IosMainMenuLayout.MatchesAppliedBounds(
				layout.Panel, layout.Panel, layout.Controls, actualControls), Is.True);
			var resetPanel = layout.Panel;
			resetPanel.Y++;
			Assert.That(IosMainMenuLayout.MatchesAppliedBounds(
				layout.Panel, resetPanel, layout.Controls, actualControls), Is.False);
			actualControls[2].Bounds.X++;
			Assert.That(IosMainMenuLayout.MatchesAppliedBounds(
				layout.Panel, layout.Panel, layout.Controls, actualControls), Is.False);

			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "MainMenuLogic.cs"));

			Assert.That(source, Does.Contain("WidgetBounds lastIosPanelBounds;"));
			Assert.That(source, Does.Contain("WidgetBounds[] lastIosControlBounds;"));
			Assert.That(source, Does.Contain("IosMainMenuLayout.MatchesAppliedBounds("));
			Assert.That(source, Does.Contain("lastIosPanelBounds, mainMenu.Bounds"));
			Assert.That(source, Does.Contain("lastIosControlBounds, mainMenuControls"));
		}

		[TestCase(1180, 820, false)]
		[TestCase(844, 390, true)]
		public void IosPolicyUsesLandscapeSafeAreaAndResponsiveBreakpoint(int width, int height, bool compact)
		{
			var policy = IosMenuLayoutPolicy.Create(true, width, height);

			Assert.That(policy.Enabled, Is.True);
			Assert.That(policy.Compact, Is.EqualTo(compact));
			Assert.That(policy.MinimumTarget, Is.GreaterThanOrEqualTo(44));
			Assert.That(policy.ContentBounds.X, Is.GreaterThanOrEqualTo(16));
			Assert.That(policy.ContentBounds.Y, Is.GreaterThanOrEqualTo(12));
			Assert.That(policy.ContentBounds.Right, Is.LessThanOrEqualTo(width - 16));
			Assert.That(policy.ContentBounds.Bottom, Is.LessThanOrEqualTo(height - 12));
		}

		[Test]
		public void DesktopPolicyIsDisabledAndLeavesBoundsUnchanged()
		{
			var policy = IosMenuLayoutPolicy.Create(false, 1180, 820);
			var bounds = new WidgetBounds(10, 20, 30, 25);

			Assert.That(policy.Enabled, Is.False);
			Assert.That(policy.EnsureTouchTarget(bounds), Is.EqualTo(bounds));
		}

		[Test]
		public void IosPolicyExpandsShortControlsWithoutMovingTheirTopEdge()
		{
			var policy = IosMenuLayoutPolicy.Create(true, 1180, 820);
			var enlarged = policy.EnsureTouchTarget(new WidgetBounds(10, 20, 100, 25));

			Assert.That(enlarged, Is.EqualTo(new WidgetBounds(10, 20, 100, 48)));
		}

		[TestCase(0.5f, false, 0.4f)]
		[TestCase(0.5f, true, 0.6f)]
		[TestCase(0f, false, 0f)]
		[TestCase(1f, true, 1f)]
		public void TouchStepValuesAreClamped(float value, bool increment, float expected)
		{
			Assert.That(IosMenuLayoutPolicy.StepValue(value, 0f, 1f, 0.1f, increment),
				Is.EqualTo(expected).Within(0.001f));
		}

		[Test]
		public void WideLobbyGivesConfigurationMostOfTheUsableWidth()
		{
			var layout = IosLobbyLayout.Create(1148, 796, false);

			Assert.That(layout.Players.Width, Is.GreaterThan(layout.Map.Width * 2));
			Assert.That(layout.Players.Height, Is.GreaterThanOrEqualTo(400));
			Assert.That(layout.ChangeMap.X, Is.EqualTo(layout.Map.X));
			Assert.That(layout.ChangeMap.Width, Is.EqualTo(layout.Map.Width));
			Assert.That(layout.ChangeMap.Y, Is.EqualTo(layout.Map.Bottom + layout.Gap));
			Assert.That(layout.Chat.Height, Is.LessThan(layout.Players.Height / 2));
			Assert.That(layout.Tabs.Bottom, Is.LessThanOrEqualTo(layout.Players.Y));
		}

		[Test]
		public void SkirmishLobbyRemovesHandicapAndReadyWhileKeepingTextBalancedColumns()
		{
			var policy = IosMenuLayoutPolicy.Create(true, 1560, 720);
			var multiplayer = IosLobbyLayout.Create(1560, 720, policy, false);
			var skirmish = IosLobbyLayout.Create(1560, 720, policy, true);

			Assert.That(skirmish.PlayerHandicap.Width, Is.Zero);
			Assert.That(skirmish.PlayerReady.Width, Is.Zero);
			Assert.That(skirmish.PlayerName.Width, Is.EqualTo(2 * skirmish.PlayerSpawn.Width));
			Assert.That(skirmish.PlayerColor.Right, Is.EqualTo(skirmish.PlayerFaction.X));
			Assert.That(skirmish.PlayerFaction.Right, Is.EqualTo(skirmish.PlayerTeam.X));
			Assert.That(skirmish.PlayerTeam.Right, Is.EqualTo(skirmish.PlayerSpawn.X));
			Assert.That(skirmish.PlayerSpawn.Right, Is.EqualTo(skirmish.PlayerRow.Right));
			Assert.That(skirmish.Players.Width, Is.EqualTo(2 * skirmish.Map.Width));
			Assert.That(multiplayer.PlayerHandicap.Width, Is.GreaterThan(0));
			Assert.That(multiplayer.PlayerReady.Width, Is.GreaterThan(0));
		}

		[Test]
		public void CompactLobbyUsesFullHeightMapAndFooterMapAction()
		{
			var layout = IosLobbyLayout.Create(812, 366, true);

			Assert.That(layout.Map.Y, Is.EqualTo(layout.Main.Y));
			Assert.That(layout.Map.Bottom, Is.EqualTo(layout.Main.Bottom));
			Assert.That(layout.Start.Right, Is.LessThanOrEqualTo(layout.ChangeMap.X));
			Assert.That(layout.ChangeMap.Right, Is.LessThanOrEqualTo(layout.Disconnect.X));
			Assert.That(layout.ChangeMap.Y, Is.EqualTo(layout.Footer.Y));
			Assert.That(layout.ChangeMap.Bottom, Is.EqualTo(layout.Footer.Bottom));
			Assert.That(layout.Footer.Bottom, Is.LessThanOrEqualTo(366));
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		public void CompactLobbyReservesReadableChatHistory(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
					safeLeft, safeTop, safeRight, safeBottom));
			var layout = IosLobbyLayout.Create(policy.ContentBounds.Width, policy.ContentBounds.Height, policy);

			Assert.That(layout.ChatDisplay.Height / policy.LogicalPerPoint,
				Is.GreaterThanOrEqualTo(24), "CHAT_DISPLAY must remain readable on short landscape phones.");
			Assert.That(layout.ChatInput.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(layout.ChatDisplay.Bottom, Is.LessThanOrEqualTo(layout.ChatInput.Y));
			Assert.That(layout.PlayerList.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48),
				"The compact player list must show at least one complete touch row.");
			Assert.That(layout.Map.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(24),
				"The compact map preview must remain readable.");
		}

		[Test]
		public void ServerNoticeHeaderSegmentsRemeasureAndClampWithoutOverlap()
		{
			var segments = IosServerCreationLayout.HeaderSegments(300, 180, 160, 120);

			Assert.That(segments, Has.Length.EqualTo(3));
			Assert.That(segments[0].X, Is.Zero);
			Assert.That(segments[0].Right, Is.EqualTo(segments[1].X));
			Assert.That(segments[1].Right, Is.EqualTo(segments[2].X));
			Assert.That(segments[2].Right, Is.EqualTo(300));
			Assert.That(segments.All(segment => segment.Width > 0), Is.True);
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21, true)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21, true)]
		[TestCase(1133, 744, 1133, 744, 0, 0, 0, 20, false)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20, false)]
		[TestCase(1366, 1024, 1366, 1024, 0, 0, 0, 20, false)]
		public void SnapshotPolicyUsesSafeAreaNativeBreakpointAndPhysicalTargets(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom, bool isPhone)
		{
			var snapshot = Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);

			Assert.That(policy.Enabled, Is.True);
			Assert.That(policy.IsPhone, Is.EqualTo(isPhone));
			Assert.That(policy.Compact, Is.EqualTo(isPhone));
			Assert.That(policy.LogicalPerPoint, Is.EqualTo(snapshot.LogicalPerPoint).Within(0.0001));
			Assert.That(snapshot.SafeBounds.Contains(policy.ContentBounds.ToRectangle()), Is.True);
			Assert.That(policy.ContentBounds.Width / (double)snapshot.SafeBounds.Width, Is.GreaterThanOrEqualTo(0.9));
			Assert.That(policy.ContentBounds.Height / (double)snapshot.SafeBounds.Height, Is.GreaterThanOrEqualTo(0.9));
			Assert.That(policy.MinimumTarget / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(policy.HeaderHeight / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(policy.FooterHeight / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
		}

		[Test]
		public void CompatibilityOverloadTreatsItsDimensionsAsNativePoints()
		{
			var phone = IosMenuLayoutPolicy.Create(true, 844, 390);
			var ipad = IosMenuLayoutPolicy.Create(true, 1180, 820);

			Assert.That(phone.IsPhone, Is.True);
			Assert.That(phone.LogicalPerPoint, Is.EqualTo(1));
			Assert.That(ipad.IsPhone, Is.False);
		}

		[TestCase(1558, 720, 844, 390, true)]
		[TestCase(1180, 820, 1180, 820, false)]
		public void LobbyOptionRowsHaveLargeNonOverlappingControls(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight, bool isPhone)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight, 0, 0, 0, 20));
			var width = policy.ContentBounds.Width;
			var checkboxRow = new WidgetBounds(0, 0, width, policy.OptionRowHeight(false));
			var dropdownRow = new WidgetBounds(0, 0, width, policy.OptionRowHeight(true));

			Assert.That(policy.OptionColumnCount, Is.EqualTo(isPhone ? 1 : 3));
			var checkboxControls = Enumerable.Range(0, 3)
				.Select(i => policy.OptionControlBounds(i, width, false)).ToArray();
			var dropdownControls = Enumerable.Range(0, 3)
				.Select(i => policy.OptionControlBounds(i, width, true)).ToArray();
			for (var i = 0; i < 3; i++)
			{
				AssertContained(checkboxRow, checkboxControls[i]);
				AssertContained(dropdownRow, dropdownControls[i]);
				AssertPhysicalTarget(checkboxControls[i], policy);
				AssertPhysicalTarget(dropdownControls[i], policy);
				var label = policy.OptionLabelBounds(i, width);
				AssertContained(dropdownRow, label);
				Assert.That(label.Bottom, Is.LessThanOrEqualTo(dropdownControls[i].Y));

				for (var j = 0; j < i; j++)
				{
					Assert.That(checkboxControls[i].ToRectangle().IntersectsWith(checkboxControls[j].ToRectangle()), Is.False);
					Assert.That(dropdownControls[i].ToRectangle().IntersectsWith(dropdownControls[j].ToRectangle()), Is.False);
				}
			}
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1133, 744, 1133, 744, 0, 0, 0, 20)]
		[TestCase(1024, 768, 1024, 768, 0, 0, 0, 20)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		[TestCase(1366, 1024, 1366, 1024, 0, 0, 0, 20)]
		public void LobbyGeometryKeepsAllSurfacesAndPlayerColumnsUsable(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
					safeLeft, safeTop, safeRight, safeBottom));
			var layout = IosLobbyLayout.Create(policy.ContentBounds.Width, policy.ContentBounds.Height, policy);
			var window = new WidgetBounds(0, 0, policy.ContentBounds.Width, policy.ContentBounds.Height);

			foreach (var region in new[]
			{
				layout.Header, layout.Tabs, layout.Main, layout.Servers, layout.Players, layout.Map, layout.ChangeMap,
				layout.Position, layout.Chat, layout.Footer, layout.Start, layout.Disconnect
			})
				AssertContained(window, region);

			Assert.That(layout.Players.Right, Is.LessThanOrEqualTo(layout.Map.X));
			Assert.That(layout.Servers, Is.EqualTo(layout.Main));
			if (policy.IsPhone)
			{
				Assert.That(layout.Map.Y, Is.EqualTo(layout.Main.Y));
				Assert.That(layout.Map.Bottom, Is.EqualTo(layout.Main.Bottom));
				Assert.That(layout.Start.Right, Is.LessThanOrEqualTo(layout.ChangeMap.X));
				Assert.That(layout.ChangeMap.Right, Is.LessThanOrEqualTo(layout.Disconnect.X));
				Assert.That(layout.ChangeMap.Y, Is.EqualTo(layout.Footer.Y));
			}
			else
			{
				Assert.That(layout.ChangeMap.X, Is.EqualTo(layout.Map.X));
				Assert.That(layout.ChangeMap.Width, Is.EqualTo(layout.Map.Width));
				Assert.That(layout.ChangeMap.Y, Is.EqualTo(layout.Map.Bottom + layout.Gap));
			}

			AssertPhysicalTarget(layout.ChangeMap, policy);
			AssertPhysicalTarget(layout.Start, policy);
			AssertPhysicalTarget(layout.Disconnect, policy);

			var playerRegion = new WidgetBounds(0, 0, layout.Players.Width, layout.Players.Height);
			AssertContained(playerRegion, layout.PlayerRow);
			Assert.That(layout.PlayerRow.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			var columns = new[]
			{
				layout.PlayerName, layout.PlayerColor, layout.PlayerFaction, layout.PlayerTeam,
				layout.PlayerHandicap, layout.PlayerSpawn, layout.PlayerReady
			};
			var minimumUnits = new[] { 2, 1, 3, 1, 1, 1, 1 };
			Assert.That(layout.PlayerRow.Width, Is.GreaterThanOrEqualTo(10 * policy.MinimumTarget));
			for (var i = 0; i < columns.Length; i++)
			{
				var column = columns[i];
				AssertContained(new WidgetBounds(0, 0, layout.PlayerRow.Width, layout.PlayerRow.Height), column);
				Assert.That(column.Width, Is.GreaterThanOrEqualTo(minimumUnits[i] * policy.MinimumTarget));
				Assert.That(column.Width, Is.GreaterThan(0));
			}

			for (var i = 1; i < columns.Length; i++)
				Assert.That(columns[i - 1].Right, Is.LessThanOrEqualTo(columns[i].X));

			if (nativeWidth == 844)
				Assert.That(layout.PlayerRow.Width - 10 * policy.MinimumTarget, Is.GreaterThan(0));
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1133, 744, 1133, 744, 0, 0, 0, 20)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		[TestCase(1366, 1024, 1366, 1024, 0, 0, 0, 20)]
		public void CreateServerGeometryIsNearFullscreenAndContained(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var snapshot = Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var layout = IosServerCreationLayout.Create(policy);

			Assert.That(policy.ContentBounds.Width / (double)snapshot.SafeBounds.Width, Is.GreaterThanOrEqualTo(0.9));
			Assert.That(policy.ContentBounds.Height / (double)snapshot.SafeBounds.Height, Is.GreaterThanOrEqualTo(0.9));
			foreach (var region in new[]
			{
				layout.Header, layout.Form, layout.MapPreview, layout.ChangeMap, layout.Footer,
				layout.CreateButton, layout.Back, layout.ServerNameRow, layout.PasswordRow,
				layout.PortRow, layout.Notices
			})
				AssertContained(layout.Window, region);

			Assert.That(layout.Form.Right, Is.LessThanOrEqualTo(layout.MapPreview.X));
			Assert.That(layout.Form.Width, Is.GreaterThan(layout.MapPreview.Width));
			Assert.That(layout.ChangeMap.X, Is.EqualTo(layout.MapPreview.X));
			Assert.That(layout.ChangeMap.Width, Is.EqualTo(layout.MapPreview.Width));
			Assert.That(layout.ChangeMap.Y, Is.EqualTo(layout.MapPreview.Bottom + layout.Gap));
			if (policy.IsPhone)
			{
				Assert.That(layout.ServerNameRow.Right, Is.LessThanOrEqualTo(layout.PasswordRow.X));
				Assert.That(layout.PasswordRow.Right, Is.LessThanOrEqualTo(layout.PortRow.X));
				Assert.That(layout.ServerNameRow.Y, Is.EqualTo(layout.PasswordRow.Y));
				Assert.That(layout.PasswordRow.Y, Is.EqualTo(layout.PortRow.Y));
				Assert.That(layout.ServerNameRow.Height,
					Is.GreaterThanOrEqualTo(policy.MinimumReadableTextHeight + policy.Gap + policy.MinimumTarget),
					"Phone form columns need a separate label band above their 48pt controls.");
			}
			else
			{
				Assert.That(layout.ServerNameRow.Bottom, Is.LessThanOrEqualTo(layout.PasswordRow.Y));
				Assert.That(layout.PasswordRow.Bottom, Is.LessThanOrEqualTo(layout.PortRow.Y));
			}

			Assert.That(layout.PortRow.Bottom, Is.LessThanOrEqualTo(layout.Notices.Y));
			Assert.That(layout.NoticesBody.Height / policy.LogicalPerPoint,
				Is.GreaterThanOrEqualTo(24), "The Notices body must remain readable and reachable.");
			Assert.That(layout.NoticesBody.Bottom, Is.LessThanOrEqualTo(layout.Footer.Y));
			AssertPhysicalTarget(layout.ChangeMap, policy);
			AssertPhysicalTarget(layout.CreateButton, policy);
			AssertPhysicalTarget(layout.Back, policy);
		}

		[Test]
		public void CommonLobbyAndServerRootsOptInAndPreserveAllControls()
		{
			var root = RepositoryRoot();
			var chrome = Path.Combine(root, "engine", "mods", "common", "chrome");
			var options = File.ReadAllText(Path.Combine(chrome, "lobby-options.yaml"));
			var players = File.ReadAllText(Path.Combine(chrome, "lobby-players.yaml"));
			var createServer = File.ReadAllText(Path.Combine(chrome, "multiplayer-createserver.yaml"));
			var manifest = File.ReadAllText(Path.Combine(root, "mods", "ra2", "mod.yaml"));
			var ra2Chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));

			Assert.That(options, Does.Contain("Container@LOBBY_OPTIONS_BIN:\n\tLogic: IosTouchMenuLogic"));
			Assert.That(players, Does.Contain("Container@LOBBY_PLAYER_BIN:\n\tLogic: IosTouchMenuLogic"));
			Assert.That(createServer, Does.Contain("Logic: ServerCreationLogic, IosTouchMenuLogic"));
			Assert.That(createServer, Does.Not.Contain("Background: menu-panel"));
			Assert.That(createServer, Does.Not.Contain("Background: menu-button"));
			Assert.That(ra2Chrome, Does.Contain("dialog:\n\tInherits: menu-panel"));
			Assert.That(ra2Chrome, Does.Contain("button:\n\tInherits: menu-button"));
			foreach (var file in new[] { "lobby-options.yaml", "lobby-players.yaml", "multiplayer-createserver.yaml" })
				Assert.That(manifest, Does.Contain("common|chrome/" + file));

			foreach (var id in new[]
			{
				"NAME", "COLOR", "FACTION", "TEAM_DROPDOWN", "HANDICAP_DROPDOWN",
				"SPAWN_DROPDOWN", "STATUS_CHECKBOX"
			})
				Assert.That(players, Does.Contain("@" + id + ":"), $"Lobby player surface lost {id}.");

			foreach (var id in new[]
			{
				"SERVER_NAME", "PASSWORD", "LISTEN_PORT", "MAP_PREVIEW_ROOT",
				"MAP_BUTTON", "CREATE_BUTTON", "BACK_BUTTON"
			})
				Assert.That(createServer, Does.Contain("@" + id + ":"), $"Create Server surface lost {id}.");

			Assert.That(createServer, Does.Not.Contain("ADVERTISE_CHECKBOX"));
		}

		[Test]
		public void ProductMenusRemoveExtrasTelemetryAndSpectatorEntryPoints()
		{
			var root = RepositoryRoot();
			var mainMenu = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "mainmenu.yaml"));
			var mainMenuLogic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "MainMenuLogic.cs"));
			var advanced = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "chrome",
				"settings-advanced.yaml"));
			var prompts = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "chrome",
				"mainmenu-prompts.yaml"));
			var advancedLogic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "Settings", "AdvancedSettingsLogic.cs"));
			var players = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "chrome",
				"lobby-players.yaml"));
			var lobbyLogic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "Lobby", "LobbyLogic.cs"));
			var commands = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"ServerTraits", "LobbyCommands.cs"));
			var session = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Game", "Network", "Session.cs"));

			Assert.That(mainMenu, Does.Not.Contain("EXTRAS_BUTTON"));
			Assert.That(mainMenu, Does.Not.Contain("EXTRAS_MENU"));
			Assert.That(mainMenuLogic, Does.Not.Contain("SystemInfoPromptLogic"));
			Assert.That(advanced, Does.Not.Contain("SENDSYSINFO"));
			foreach (var removed in new[]
			{
				"PERFGRAPH_CHECKBOX", "FETCH_NEWS_CHECKBOX", "CHECK_VERSION_CHECKBOX",
				"DEBUG_SECTION_HEADER", "DEBUG_HIDDEN_CONTAINER", "BOTDEBUG_CHECKBOX_CONTAINER"
			})
				Assert.That(advanced, Does.Not.Contain(removed), removed + " must remain removed.");
			Assert.That(prompts, Does.Not.Contain("MAINMENU_SYSTEM_INFO_PROMPT"));
			Assert.That(advancedLogic, Does.Not.Contain("SendSystemInformation"));
			Assert.That(advancedLogic, Does.Not.Contain("BindCheckboxPref(panel, \"PERFGRAPH_CHECKBOX\""));
			Assert.That(advancedLogic, Does.Not.Contain("BindCheckboxPref(panel, \"FETCH_NEWS_CHECKBOX\""));
			Assert.That(advancedLogic, Does.Not.Contain("BindCheckboxPref(panel, \"CHECK_VERSION_CHECKBOX\""));
			Assert.That(advancedLogic, Does.Not.Contain("DEBUG_HIDDEN_CONTAINER"));
			Assert.That(players, Does.Not.Contain("TEMPLATE_NEW_SPECTATOR"));
			Assert.That(players, Does.Not.Contain("@SPECTATE"));
			Assert.That(players, Does.Not.Contain("TOGGLE_SPECTATORS"));
			Assert.That(lobbyLogic, Does.Not.Contain("newSpectatorTemplate"));
			Assert.That(commands, Does.Not.Contain("{ \"spectate\", Specate }"));
			Assert.That(commands, Does.Not.Contain("{ \"allow_spectators\", AllowSpectators }"));
			Assert.That(commands, Does.Not.Contain("{ \"make_spectator\", MakeSpectator }"));
			Assert.That(session, Does.Contain("public bool AllowSpectators = false;"));
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1133, 744, 1133, 744, 0, 0, 0, 20)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		public void MultiplayerBrowserUsesContainedReadableColumnsAndActions(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
					safeLeft, safeTop, safeRight, safeBottom));
			var layout = IosMultiplayerBrowserLayout.Create(policy);

			foreach (var region in new[]
			{
				layout.Header, layout.LocalMode, layout.OnlineMode, layout.Table, layout.TableHeader,
				layout.ServerList, layout.Details, layout.Footer, layout.Filters, layout.Reload,
				layout.RoomCodeInput, layout.RoomCodeButton, layout.DirectConnect, layout.CreateButton, layout.Back
			})
				AssertContained(layout.Window, region);

			Assert.That(layout.ServerList.Bottom, Is.LessThanOrEqualTo(layout.Footer.Y));
			Assert.That(layout.LocalMode.Right, Is.LessThanOrEqualTo(layout.OnlineMode.X));
			Assert.That(layout.OnlineMode.Right, Is.LessThanOrEqualTo(layout.Header.X));
			Assert.That(layout.RoomCodeInput.Right, Is.LessThanOrEqualTo(layout.Reload.X));
			Assert.That(layout.Reload.Right, Is.LessThanOrEqualTo(layout.RoomCodeButton.X));
			Assert.That(layout.Details.X, Is.GreaterThanOrEqualTo(layout.Table.Right));
			Assert.That(layout.ServerColumns.First().X, Is.Zero);
			Assert.That(layout.ServerColumns.Last().Right, Is.EqualTo(layout.ServerList.Width));
			for (var i = 1; i < layout.ServerColumns.Length; i++)
				Assert.That(layout.ServerColumns[i - 1].Right, Is.EqualTo(layout.ServerColumns[i].X));
			foreach (var column in layout.ServerColumns)
				Assert.That(column.Width / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			foreach (var action in new[]
			{
				layout.LocalMode, layout.OnlineMode, layout.Filters, layout.Reload, layout.RoomCodeInput,
				layout.RoomCodeButton, layout.DirectConnect, layout.CreateButton, layout.Back
			})
				AssertPhysicalTarget(action, policy);
		}

		[Test]
		public void MultiplayerPagesOptIntoTheResponsiveTouchLayout()
		{
			var chrome = Path.Combine(RepositoryRoot(), "engine", "mods", "common", "chrome");
			foreach (var file in new[]
			{
				"multiplayer-browser.yaml", "multiplayer-directconnect.yaml", "lobby-servers.yaml"
			})
				Assert.That(File.ReadAllText(Path.Combine(chrome, file)), Does.Contain("IosTouchMenuLogic"));
		}

		[Test]
		public void MainMenuDoesNotShipOrReferenceAfghanistanCrisisBrandArtwork()
		{
			var root = RepositoryRoot();
			foreach (var modRoot in new[]
			{
				Path.Combine(root, "mods", "ra2"),
				Path.Combine(root, "engine", "mods", "ra2")
			})
			{
				var mainMenu = File.ReadAllText(Path.Combine(modRoot, "chrome", "mainmenu.yaml"));
				var chrome = File.ReadAllText(Path.Combine(modRoot, "chrome.yaml"));

				Assert.That(mainMenu, Does.Not.Contain("Container@BRAND"));
				Assert.That(mainMenu, Does.Not.Contain("Image@LOGO"));
				Assert.That(mainMenu, Does.Not.Contain("menu-logo"));
				Assert.That(chrome, Does.Not.Contain("menulogo"));
				Assert.That(chrome, Does.Not.Contain("menutitle"));

				foreach (var file in new[]
				{
					"menulogo.png", "menulogo-sm.png", "menutitle.png", "menutitle-sm.png"
				})
					Assert.That(File.Exists(Path.Combine(modRoot, "uibits", file)), Is.False,
						$"Obsolete Afghanistan Crisis brand art remains: {modRoot}/uibits/{file}");
			}
		}

		[Test]
		public void IosSourcesUseSnapshotsAndAdaptDynamicLobbyContent()
		{
			var root = RepositoryRoot();
			var logicRoot = Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic");
			var touch = File.ReadAllText(Path.Combine(logicRoot, "IosTouchMenuLogic.cs"));
			var lobby = File.ReadAllText(Path.Combine(logicRoot, "Lobby", "LobbyLogic.cs"));

			Assert.That(touch, Does.Contain("IosScreenMetrics.SnapshotFor"));
			Assert.That(touch, Does.Contain("LayoutLobbyOptionsRows"));
			Assert.That(touch, Does.Contain("EnableContentDragging = true"));
			Assert.That(touch, Does.Contain("ApplyServerCreationLayout"));
			Assert.That(lobby, Does.Contain("IosScreenMetrics.SnapshotFor"));
			Assert.That(lobby, Does.Contain("LayoutIosPlayerRows"));
			foreach (var setup in new[]
			{
				"SetupEditableNameWidget", "SetupEditableColorWidget", "SetupEditableFactionWidget",
				"SetupEditableTeamWidget", "SetupEditableHandicapWidget", "SetupEditableSpawnWidget",
				"SetupEditableReadyWidget"
			})
				Assert.That(lobby, Does.Contain(setup), $"LobbyLogic lost {setup}.");
		}

		[Test]
		public void SkirmishLobbyUsesClassicColorsAndSkipsRemovedControls()
		{
			var root = RepositoryRoot();
			var lobby = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "Lobby", "LobbyLogic.cs"));
			var utils = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "Lobby", "LobbyUtils.cs"));
			var settings = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "Settings", "DisplaySettingsLogic.cs"));

			Assert.That(lobby, Does.Contain("SetupEditableClassicColorWidget"));
			Assert.That(lobby, Does.Contain("if (!skirmishMode)\n\t\t\t\t\t\tLobbyUtils.SetupEditableHandicapWidget"));
			Assert.That(lobby, Does.Contain("if (!skirmishMode)\n\t\t\t\t\t\tLobbyUtils.SetupEditableReadyWidget"));
			Assert.That(lobby, Does.Contain("label.IsVisible = () => !isSkirmish"));
			Assert.That(utils, Does.Contain("ShowClassicColorDropDown"));
			Assert.That(settings, Does.Contain("ShowClassicColorDropDown(colorDropdown"));
			Assert.That(settings, Does.Not.Contain("ShowColorDropDown(colorDropdown, ps.Color"));
		}

		[TestCase("Tiny", false, false, "IosRegular")]
		[TestCase("Regular", false, false, "IosRegular")]
		[TestCase("TinyBold", false, false, "IosBold")]
		[TestCase("Regular", false, true, "IosBold")]
		[TestCase("Bold", true, false, "IosTitle")]
		public void IosMenuFontsReplaceDesktopSizedFonts(
			string original, bool title, bool action, string expected)
		{
			Assert.That(IosMenuLayoutPolicy.TouchFont(original, title, action), Is.EqualTo(expected));
		}

		[Test]
		public void ShippingConfigurationRootsDeclareTouchAdaptation()
		{
			var chrome = Path.Combine(RepositoryRoot(), "engine", "mods", "common", "chrome");
			foreach (var file in new[] { "lobby-options.yaml", "lobby-players.yaml", "multiplayer-createserver.yaml" })
			{
				var text = File.ReadAllText(Path.Combine(chrome, file));
				Assert.That(text, Does.Contain("IosTouchMenuLogic"), $"{file} is missing iOS touch adaptation.");
			}
		}

		static IosScreenSnapshot Snapshot(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			return new IosScreenSnapshot(
				new Size(effectiveWidth, effectiveHeight),
				new Size(nativeWidth, nativeHeight),
				new IosSafeAreaInsets(safeLeft, safeTop, safeRight, safeBottom));
		}

		static void AssertPhysicalTarget(WidgetBounds bounds, IosMenuLayoutPolicy policy)
		{
			Assert.That(bounds.Width / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(bounds.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
		}

		static void AssertContained(WidgetBounds outer, WidgetBounds inner)
		{
			Assert.That(outer.ToRectangle().Contains(inner.ToRectangle()), Is.True,
				$"Expected {inner.ToRectangle()} inside {outer.ToRectangle()}.");
		}

		static WidgetBounds MainMenuControlBounds(string yaml, string id)
		{
			var body = MainMenuControlBody(yaml, id);
			int Value(string key)
			{
				var value = Regex.Match(body, $@"(?m)^\s+{key}: (?<value>\d+)$");
				Assert.That(value.Success, Is.True, $"Missing {key} for {id}.");
				return int.Parse(value.Groups["value"].Value, CultureInfo.InvariantCulture);
			}

			return new WidgetBounds(Value("X"), Value("Y"), Value("Width"), Value("Height"));
		}

		static string MainMenuControlBody(string yaml, string id)
		{
			var match = Regex.Match(yaml,
				$@"(?ms)^\s+(?:Button|DropDownButton)@{Regex.Escape(id)}:\s*\n(?<body>.*?)(?=^\s+(?:Button|DropDownButton|Background|Label|Container)@|\z)");
			Assert.That(match.Success, Is.True, $"Missing main-menu control {id}.");
			return match.Groups["body"].Value;
		}

		sealed class ActualIosMenuFonts : IDisposable
		{
			readonly SheetBuilder sheetBuilder;

			public SpriteFont Regular { get; }
			public SpriteFont Bold { get; }

			public ActualIosMenuFonts()
			{
				Log.AddChannel("perf", null);
				var root = RepositoryRoot();
				var platform = new DefaultPlatform();
				sheetBuilder = new SheetBuilder(SheetType.BGRA, 2048);
				Regular = new SpriteFont(platform, "IosRegular", File.ReadAllBytes(Path.Combine(
					root, "mods", "ra2", "fonts", "NotoSansCJKsc-Regular.otf")), 24, 19, 1f, sheetBuilder);
				Bold = new SpriteFont(platform, "IosBold", File.ReadAllBytes(Path.Combine(
					root, "mods", "ra2", "fonts", "NotoSansCJKsc-Bold.otf")), 26, 20, 1f, sheetBuilder);
			}

			public void Dispose()
			{
				Regular.Dispose();
				Bold.Dispose();
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
