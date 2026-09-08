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
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosSettingsLayoutTest
	{
		[Test]
		public void DisplayLanguageRestartBaselineIsScopedToTheCurrentPanelSession()
		{
			Assert.That(DisplaySettingsLogic.LanguageRequiresRestart(
				LanguageSelectionPolicy.English,
				LanguageSelectionPolicy.English,
				"en-US"), Is.False);
			Assert.That(DisplaySettingsLogic.LanguageRequiresRestart(
				LanguageSelectionPolicy.SimplifiedChinese,
				LanguageSelectionPolicy.SimplifiedChinese,
				"zh-Hans"), Is.False,
				"A newly constructed panel after the main-menu reload must not inherit a stale language warning.");
			Assert.That(DisplaySettingsLogic.LanguageRequiresRestart(
				LanguageSelectionPolicy.SimplifiedChinese,
				LanguageSelectionPolicy.English,
				"zh-Hans"), Is.True);
			Assert.That(DisplaySettingsLogic.LanguageRequiresRestart(
				LanguageSelectionPolicy.English,
				LanguageSelectionPolicy.SimplifiedChinese,
				"zh-Hans"), Is.True,
				"A saved preference still requires reload while Fluent is using the previous effective locale.");
		}

		[Test]
		public void DisplaySettingsUsesPerInstanceLanguageBaselinesAndTheSharedPreferencePolicy()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "Settings", "DisplaySettingsLogic.cs"));

			Assert.That(source, Does.Not.Contain("static readonly string OriginalLanguage"));
			Assert.That(source, Does.Contain("readonly string originalLanguagePreference;"));
			Assert.That(source, Does.Contain("readonly string originalEffectiveLanguage;"));
			Assert.That(source, Does.Contain("LanguageSelectionPolicy.SupportedPreferences"));
			Assert.That(source, Does.Contain("LanguageSelectionPolicy.GetDisplayName"));
			Assert.That(CountOccurrences(source, "LanguageRequiresRestart("), Is.EqualTo(3),
				"The single helper must be declared once and used by both restart surfaces.");
		}

		[Test]
		public void ScreenSnapshotConvertsNativeSafeAreaToEffectiveCoordinates()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1560, 720),
				new Size(932, 430),
				new IosSafeAreaInsets(59, 0, 59, 21));

			Assert.That(snapshot.LogicalPerPoint, Is.EqualTo(720d / 430d).Within(0.0001));
			Assert.That(snapshot.SafeBounds, Is.EqualTo(new Rectangle(99, 0, 1362, 684)));
		}

		[Test]
		public void IpadScreenSnapshotKeepsOneToOneSafeAreaCoordinates()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1366, 1024),
				new Size(1366, 1024),
				new IosSafeAreaInsets(0, 0, 0, 20));

			Assert.That(snapshot.LogicalPerPoint, Is.EqualTo(1));
			Assert.That(snapshot.SafeBounds, Is.EqualTo(new Rectangle(0, 0, 1366, 1004)));
		}

		[Test]
		public void IosViewControllerPublishesBoundsAndSafeAreaAfterLayout()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "ios", "OpenRA.iOS", "ViewController.cs"));

			Assert.That(source, Does.Contain("IosScreenMetrics.Publish"));
			Assert.That(source, Does.Contain("View.Bounds"));
			Assert.That(source, Does.Contain("View.SafeAreaInsets"));
		}

		[TestCase(932, 430, 932, 430)]
		[TestCase(430, 932, 932, 430)]
		public void IphoneSceneUsesLandscapeFullscreenAndFitsTheReferenceCanvas(
			int width, int height, int expectedWidth, int expectedHeight)
		{
			var display = IosDisplayLayout.ForScene(width, height);

			Assert.That(display.Width, Is.EqualTo(expectedWidth));
			Assert.That(display.Height, Is.EqualTo(expectedHeight));
			Assert.That(display.EffectiveWidth, Is.GreaterThanOrEqualTo(1024));
			Assert.That(display.EffectiveHeight, Is.GreaterThanOrEqualTo(720));
		}

		[Test]
		public void IpadKeepsNativeLogicalScale()
		{
			var display = IosDisplayLayout.ForScene(1366, 1024);

			Assert.That(display.UiScale, Is.EqualTo(1f));
			Assert.That(display.EffectiveWidth, Is.EqualTo(1366));
			Assert.That(display.EffectiveHeight, Is.EqualTo(1024));
		}

		[Test]
		public void IosBundleDeclaresAModernGeneratedLaunchScreen()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (root != null && !File.Exists(Path.Combine(root, "ios", "OpenRA.iOS", "Info.plist")))
				root = Directory.GetParent(root)?.FullName;

			Assert.That(root, Is.Not.Null, "Could not locate repository root.");
			var plist = File.ReadAllText(Path.Combine(root!, "ios", "OpenRA.iOS", "Info.plist"));
			Assert.That(plist, Does.Contain("<key>UILaunchScreen</key>"));
			Assert.That(plist, Does.Contain("<key>UILaunchScreen</key>\n  <dict>"));
		}

		[Test]
		public void DesktopPolicyDoesNotAlterCoordinates()
		{
			var layout = IosSettingsLayout.ForPlatform(false);

			Assert.That(layout.Enabled, Is.False);
			Assert.That(layout.Scale(37), Is.EqualTo(37));
			Assert.That(layout.ScaleY(100), Is.EqualTo(100));
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21, true)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21, true)]
		[TestCase(1133, 744, 1133, 744, 0, 0, 0, 20, false)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20, false)]
		[TestCase(1366, 1024, 1366, 1024, 0, 0, 0, 20, false)]
		public void DeviceMatrixUsesSafeNearFullscreenShellAndExpectedNavigation(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom, bool isPhone)
		{
			var layout = CreateLayout(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);

			Assert.That(layout.Enabled, Is.True);
			Assert.That(layout.IsPhone, Is.EqualTo(isPhone));
			Assert.That(layout.SafeBounds.Contains(layout.Window), Is.True);
			Assert.That(layout.Window.Width / (double)layout.SafeBounds.Width, Is.GreaterThanOrEqualTo(0.9));
			Assert.That(layout.Window.Height / (double)layout.SafeBounds.Height, Is.GreaterThanOrEqualTo(0.9));
			Assert.That(layout.Window.Contains(layout.Header), Is.True);
			Assert.That(layout.Window.Contains(layout.Tabs), Is.True);
			Assert.That(layout.Window.Contains(layout.Content), Is.True);
			Assert.That(layout.Window.Contains(layout.Footer), Is.True);
			Assert.That(layout.Content.Bottom, Is.LessThanOrEqualTo(layout.Footer.Top));

			if (isPhone)
			{
				Assert.That(layout.TabsAreHorizontal, Is.True);
				Assert.That(layout.Tabs.Top, Is.GreaterThanOrEqualTo(layout.Header.Bottom));
				Assert.That(layout.Content.Top, Is.GreaterThanOrEqualTo(layout.Tabs.Bottom));
			}
			else
			{
				Assert.That(layout.TabsAreHorizontal, Is.False);
				Assert.That(layout.Content.Left, Is.GreaterThanOrEqualTo(layout.Tabs.Right));
			}
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1133, 744, 1133, 744, 0, 0, 0, 20)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		[TestCase(1366, 1024, 1366, 1024, 0, 0, 0, 20)]
		public void EveryShellTouchTargetIsAtLeast48NativePoints(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var layout = CreateLayout(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);

			Assert.That(layout.MinimumTarget / layout.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			AssertPhysicalTarget(layout.Reset, layout.LogicalPerPoint);
			AssertPhysicalTarget(layout.Back, layout.LogicalPerPoint);
			for (var i = 0; i < 5; i++)
				AssertPhysicalTarget(layout.TabBounds(i), layout.LogicalPerPoint);
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1133, 744, 1133, 744, 0, 0, 0, 20)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		[TestCase(1366, 1024, 1366, 1024, 0, 0, 0, 20)]
		public void HotkeyHeaderListAndFooterHaveExplicitNonOverlappingRegions(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var layout = CreateLayout(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);

			Assert.That(layout.Content.Contains(layout.HotkeyHeader), Is.True);
			Assert.That(layout.Content.Contains(layout.HotkeyList), Is.True);
			Assert.That(layout.Content.Contains(layout.HotkeyFooter), Is.True);
			Assert.That(layout.HotkeyHeader.Bottom, Is.LessThanOrEqualTo(layout.HotkeyList.Top));
			Assert.That(layout.HotkeyList.Bottom, Is.LessThanOrEqualTo(layout.HotkeyFooter.Top));
			Assert.That(layout.HotkeyList.Height, Is.GreaterThan(0));
			Assert.That(layout.HotkeyActionHeight / layout.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
		}

		[Test]
		public void IosHotkeyEmptyMessageFillsTheResizedEmptyListContainer()
		{
			var emptyList = new ContainerWidget { Id = "HOTKEY_EMPTY_LIST" };
			var message = new ContainerWidget
			{
				Id = "HOTKEY_EMPTY_LIST_MESSAGE",
				Bounds = new WidgetBounds(4, 5, 6, 7)
			};
			emptyList.AddChild(message);

			var layoutMethod = typeof(SettingsLogic).GetMethod(
				"LayoutIosHotkeyEmptyList", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.That(layoutMethod, Is.Not.Null, "Expected a testable empty-list layout helper.");

			var listBounds = new Rectangle(11, 22, 333, 144);
			layoutMethod!.Invoke(null, new object[] { emptyList, listBounds });

			Assert.That(emptyList.Bounds.ToRectangle(), Is.EqualTo(listBounds));
			Assert.That(message.Bounds.ToRectangle(), Is.EqualTo(new Rectangle(0, 0, 333, 144)));
		}

		[Test]
		public void EquivalentScreenSnapshotsProduceEquivalentLayouts()
		{
			var first = CreateLayout(1558, 720, 844, 390, 47, 0, 47, 21);
			var second = CreateLayout(1558, 720, 844, 390, 47, 0, 47, 21);

			Assert.That(second.Window, Is.EqualTo(first.Window));
			Assert.That(second.Header, Is.EqualTo(first.Header));
			Assert.That(second.Tabs, Is.EqualTo(first.Tabs));
			Assert.That(second.Content, Is.EqualTo(first.Content));
			Assert.That(second.Footer, Is.EqualTo(first.Footer));
			Assert.That(second.HotkeyList, Is.EqualTo(first.HotkeyList));
		}

		[Test]
		public void TransformingCapturedOriginalBoundsTwiceDoesNotAccumulateScale()
		{
			var layout = CreateLayout(1558, 720, 844, 390, 47, 0, 47, 21);
			var original = new Rectangle(12, 18, 200, 20);
			const double HorizontalScale = 1.25;
			var verticalScale = layout.RowVerticalScale(original.Height, original.Height, false);

			var first = layout.TransformBoundsFromOriginal(
				original, HorizontalScale, verticalScale, true, true);
			var reapplied = layout.TransformBoundsFromOriginal(
				original, HorizontalScale, verticalScale, true, true);
			var accumulated = layout.TransformBoundsFromOriginal(
				first, HorizontalScale, verticalScale, true, true);

			Assert.That(reapplied, Is.EqualTo(first));
			Assert.That(accumulated, Is.Not.EqualTo(first),
				"This guards against transforming the already-scaled runtime bounds.");
			Assert.That(original, Is.EqualTo(new Rectangle(12, 18, 200, 20)));
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21)]
		[TestCase(1133, 744, 1133, 744, 0, 0, 0, 20)]
		public void ScrollRowsScaleFromOriginalBoundsToPhysicalTouchTargets(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var layout = CreateLayout(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);

			var scale = layout.RowVerticalScale(50, 20, false);
			Assert.That(20 * scale / layout.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(layout.RowVerticalScale(50, 20, false), Is.EqualTo(scale));
			Assert.That(13 * layout.RowVerticalScale(13, 0, true),
				Is.GreaterThanOrEqualTo(layout.SectionHeaderHeight));
		}

		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		public void TouchJoystickDropdownScalesToAtLeast48NativePoints(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var layout = CreateLayout(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);
			var verticalScale = layout.RowVerticalScale(50, 25, false);
			var row = layout.TransformBoundsFromOriginal(
				new Rectangle(0, 0, 750, 50), 1, verticalScale, false, false);
			var dropdown = layout.TransformBoundsFromOriginal(
				new Rectangle(10, 25, 730, 25), 1, verticalScale, true, true);

			Assert.That(dropdown.Height / layout.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(row.Contains(dropdown), Is.True);
		}

		[Test]
		public void ScrollRowsScaleBetweenAvailableContentWidthsOn844PointPhone()
		{
			var layout = CreateLayout(1558, 720, 844, 390, 47, 0, 47, 21);
			const int OriginalScrollWidth = 750;
			const int OriginalScrollbarWidth = 24;
			const int OriginalContentWidth = OriginalScrollWidth - OriginalScrollbarWidth;
			var newContentWidth = layout.Content.Width - layout.ScrollbarWidth;

			var scale = layout.ScrollContentHorizontalScale(
				OriginalScrollWidth, OriginalScrollbarWidth, layout.Content.Width);
			var transformed = layout.TransformBoundsFromOriginal(
				new Rectangle(0, 0, OriginalContentWidth, 50), scale, 1, false, false);

			Assert.That(scale, Is.EqualTo(newContentWidth / (double)OriginalContentWidth).Within(0.0001));
			Assert.That(transformed.Width, Is.EqualTo(newContentWidth));

			var settings = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Settings", "SettingsLogic.cs"));
			Assert.That(settings, Does.Contain("OriginalBounds(scrollPanel)"));
			Assert.That(settings, Does.Contain("layout.ScrollContentHorizontalScale("));
		}

		[Test]
		public void DynamicHotkeysUseOnePhoneColumnAndTwoIpadColumns()
		{
			var phone = CreateLayout(1558, 720, 844, 390, 47, 0, 47, 21);
			var ipad = CreateLayout(1180, 820, 1180, 820, 0, 0, 0, 20);

			Assert.That(phone.HotkeyColumns, Is.EqualTo(1));
			Assert.That(ipad.HotkeyColumns, Is.EqualTo(2));
			Assert.That(phone.HotkeyItemWidth(phone.Content.Width), Is.GreaterThanOrEqualTo(phone.MinimumTarget));
			Assert.That(ipad.HotkeyItemWidth(ipad.Content.Width), Is.GreaterThanOrEqualTo(ipad.MinimumTarget));
		}

		[Test]
		public void CompactPhoneHotkeyListNeverBuildsANegativeScrollbarTrack()
		{
			var phone = CreateLayout(1558, 720, 844, 390, 47, 0, 47, 21);
			var ipad = CreateLayout(1180, 820, 1180, 820, 0, 0, 0, 20);

			Assert.That(phone.HotkeyList.Height, Is.LessThan(2 * phone.ScrollbarWidth),
				"This compact layout would create a negative traditional scrollbar track.");
			Assert.That(phone.HotkeyScrollbarVisible, Is.False);
			Assert.That(phone.HotkeyScrollbarWidth, Is.Zero);
			Assert.That(phone.HotkeyList.Height - 2 * phone.HotkeyScrollbarWidth, Is.GreaterThanOrEqualTo(0));
			Assert.That(ipad.HotkeyScrollbarVisible, Is.True);
			Assert.That(ipad.HotkeyList.Height - 2 * ipad.HotkeyScrollbarWidth, Is.GreaterThanOrEqualTo(0));

			var settings = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Settings", "SettingsLogic.cs"));
			Assert.That(settings, Does.Contain("ScrollBar.Hidden"));
			Assert.That(settings, Does.Contain("layout.HotkeyScrollbarWidth"));
		}

		[Test]
		public void Ra2SettingsYamlUsesSemanticPanelAndKeepsDesktopGeometryAndFivePanels()
		{
			var yaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "chrome", "settings.yaml"));

			Assert.That(yaml, Does.Contain("Background: settings-panel"));
			Assert.That(yaml, Does.Contain("Width: 1000"));
			Assert.That(yaml, Does.Contain("Height: 580"));
			Assert.That(CountOccurrences(yaml, "_PANEL: button-settings-tab-"), Is.EqualTo(5));
		}

		[Test]
		public void Ra2ManifestDefinesIosFontAliasesUsingTheExistingFontFiles()
		{
			var yaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "mod.yaml"));

			Assert.That(yaml, Does.Contain("IosRegular:"));
			Assert.That(yaml, Does.Contain("IosBold:"));
			Assert.That(yaml, Does.Contain("IosTitle:"));
			Assert.That(CountOccurrences(yaml, "Font: ra2|fonts/NotoSansCJKsc-Regular.otf"), Is.GreaterThanOrEqualTo(2));
			Assert.That(CountOccurrences(yaml, "Font: ra2|fonts/NotoSansCJKsc-Bold.otf"), Is.GreaterThanOrEqualTo(3));
		}

		[Test]
		public void SettingsLogicUsesPublishedMetricsExplicitRegionsAndAdaptedDynamicHotkeys()
		{
			var root = RepositoryRoot();
			var settings = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Settings", "SettingsLogic.cs"));
			var hotkeys = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Settings", "HotkeysSettingsLogic.cs"));

			Assert.That(settings, Does.Contain("IosScreenMetrics.SnapshotFor"));
			Assert.That(settings, Does.Contain("layout.Header"));
			Assert.That(settings, Does.Contain("layout.HotkeyHeader"));
			Assert.That(settings, Does.Contain("layout.HotkeyList"));
			Assert.That(settings, Does.Contain("layout.HotkeyFooter"));
			Assert.That(settings, Does.Contain("\"IosRegular\""));
			Assert.That(settings, Does.Contain("\"IosBold\""));
			Assert.That(settings, Does.Contain("\"IosTitle\""));
			Assert.That(settings, Does.Not.Contain("1180"));
			Assert.That(settings, Does.Not.Contain("720"));
			Assert.That(settings.IndexOf("RestoreIosOriginalLayout();", StringComparison.Ordinal),
				Is.LessThan(settings.IndexOf("IosScreenMetrics.SnapshotFor", StringComparison.Ordinal)));
			Assert.That(hotkeys, Does.Contain("PrepareIosHotkeyTemplates"));
		}

		[Test]
		public void StableIosTickDoesNotRelayoutTheEntireHotkeyTree()
		{
			var root = RepositoryRoot();
			var settings = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Settings", "SettingsLogic.cs"));
			var hotkeys = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Settings", "HotkeysSettingsLogic.cs"));
			var tickStart = settings.IndexOf("public override void Tick()", StringComparison.Ordinal);
			var tickEnd = settings.IndexOf("public void RegisterSettingsPanel", tickStart, StringComparison.Ordinal);
			Assert.That(tickStart, Is.GreaterThanOrEqualTo(0));
			Assert.That(tickEnd, Is.GreaterThan(tickStart));

			var tick = settings.Substring(tickStart, tickEnd - tickStart);
			Assert.That(tick, Does.Contain("ApplyIosLayout();"),
				"Viewport or safe-area changes must still trigger a full layout.");
			Assert.That(tick, Does.Not.Contain("ApplyIosHotkeyPanel"),
				"A stable viewport must not traverse the entire hotkey tree every tick.");
			Assert.That(CountOccurrences(hotkeys, "PrepareIosHotkeyTemplates"), Is.GreaterThanOrEqualTo(2));
			Assert.That(hotkeys, Does.Contain("SettingsLogic.LayoutIosHotkeyList(hotkeyList);"),
				"A dynamic rebuild must request one list-only layout pass.");
		}

		[TestCase("VIDEO_MODE_DROPDOWN_CONTAINER")]
		[TestCase("WINDOW_RESOLUTION_CONTAINER")]
		[TestCase("DISPLAY_SELECTION_CONTAINER")]
		[TestCase("GL_PROFILE_DROPDOWN_CONTAINER")]
		[TestCase("MOUSE_CONTROL_CONTAINER")]
		[TestCase("ZOOM_MODIFIER_CONTAINER")]
		[TestCase("MOUSE_CONTROL_DESC_CLASSIC")]
		[TestCase("MOUSE_CONTROL_DESC_MODERN")]
		[TestCase("EDGESCROLL_CHECKBOX_CONTAINER")]
		[TestCase("ALTERNATE_SCROLL_CHECKBOX_CONTAINER")]
		[TestCase("LOCKMOUSE_CHECKBOX_CONTAINER")]
		[TestCase("MOUSE_SCROLL_TYPE_CONTAINER")]
		[TestCase("MOUSE_SETTINGS_SPACER")]
		public void IosVisibilityPolicyHidesDesktopOnlySettingContainers(string id)
		{
			Assert.That(IosSettingsVisibilityPolicy.IsDesktopOnlyContainer(id), Is.True);
		}

		[TestCase("SCROLLSPEED_SLIDER_CONTAINER")]
		[TestCase("ZOOMSPEED_SLIDER_CONTAINER")]
		[TestCase("UI_SCROLLSPEED_SLIDER_CONTAINER")]
		[TestCase("LANGUAGE_DROPDOWN_CONTAINER")]
		[TestCase("FRAME_LIMIT_SLIDER_CONTAINER")]
		public void IosVisibilityPolicyKeepsUsefulTouchSettings(string id)
		{
			Assert.That(IosSettingsVisibilityPolicy.IsDesktopOnlyContainer(id), Is.False);
		}

		[TestCase(true, "TOUCH_JOYSTICK_SIZE_CONTAINER", true)]
		[TestCase(false, "TOUCH_JOYSTICK_SIZE_CONTAINER", false)]
		[TestCase(true, "MOUSE_CONTROL_CONTAINER", false)]
		[TestCase(false, "MOUSE_CONTROL_CONTAINER", true)]
		[TestCase(true, "SCROLLSPEED_SLIDER_CONTAINER", true)]
		[TestCase(false, "SCROLLSPEED_SLIDER_CONTAINER", true)]
		[TestCase(true, null, true)]
		[TestCase(false, null, true)]
		public void PlatformVisibilityPolicyShowsOnlyContainersForTheActivePlatform(
			bool isIos, string id, bool expected)
		{
			Assert.That(IosSettingsVisibilityPolicy.ShouldShowContainer(isIos, id), Is.EqualTo(expected));
			Assert.That(IosSettingsVisibilityPolicy.IsIosOnlyContainer("TOUCH_JOYSTICK_SIZE_CONTAINER"), Is.True);
		}

		[TestCase(true, "TOUCH_JOYSTICK_SIZE_CONTAINER", false)]
		[TestCase(false, "TOUCH_JOYSTICK_SIZE_CONTAINER", true)]
		[TestCase(true, "MOUSE_SETTINGS_SPACER", true)]
		[TestCase(false, "MOUSE_SETTINGS_SPACER", false)]
		[TestCase(true, "SCROLLSPEED_SLIDER_CONTAINER", false)]
		[TestCase(false, "SCROLLSPEED_SLIDER_CONTAINER", false)]
		public void PlatformVisibilityPolicyIdentifiesRowsThatMustCollapse(
			bool isIos, string id, bool expected)
		{
			Assert.That(IosSettingsVisibilityPolicy.ShouldCollapseRow(isIos, id), Is.EqualTo(expected));
		}

		[TestCase(true, true, false)]
		[TestCase(false, false, true)]
		public void SettingsLogicConsumesPlatformVisibilityPolicy(
			bool isIos, bool expectedTouchVisible, bool expectedMouseVisible)
		{
			var panel = new ContainerWidget();
			var touch = new ContainerWidget { Id = "TOUCH_JOYSTICK_SIZE_CONTAINER" };
			var mouse = new ContainerWidget { Id = "MOUSE_CONTROL_CONTAINER" };
			var common = new ContainerWidget { Id = "SCROLLSPEED_SLIDER_CONTAINER" };
			panel.AddChild(touch);
			panel.AddChild(mouse);
			panel.AddChild(common);

			var applyVisibility = typeof(SettingsLogic).GetMethod(
				"ApplyPlatformVisibility", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.That(applyVisibility, Is.Not.Null, "Expected SettingsLogic to consume the pure platform policy.");
			applyVisibility!.Invoke(null, new object[] { panel, isIos });

			Assert.Multiple(() =>
			{
				Assert.That(touch.IsVisible(), Is.EqualTo(expectedTouchVisible));
				Assert.That(mouse.IsVisible(), Is.EqualTo(expectedMouseVisible));
				Assert.That(common.IsVisible(), Is.True);
			});
		}

		[Test]
		public void TouchJoystickSizeRowAndLocalizedOptionsAreWiredIntoSettings()
		{
			var root = RepositoryRoot();
			var yaml = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "chrome", "settings-input.yaml"));
			var english = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "fluent", "common.ftl"));
			var chinese = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "fluent", "zh-CN", "common.ftl"));
			var inputLogic = File.ReadAllText(Path.Combine(
				root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Settings", "InputSettingsLogic.cs"));
			var settingsLogic = File.ReadAllText(Path.Combine(
				root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Settings", "SettingsLogic.cs"));
			var header = yaml.IndexOf("Background@INPUT_SECTION_HEADER:", StringComparison.Ordinal);
			var touchRow = yaml.IndexOf("Container@TOUCH_JOYSTICK_SIZE_CONTAINER:", StringComparison.Ordinal);
			var mouseRow = touchRow < 0 ? -1 : yaml.IndexOf("Container@ROW:", touchRow, StringComparison.Ordinal);

			Assert.Multiple(() =>
			{
				Assert.That(header, Is.GreaterThanOrEqualTo(0));
				Assert.That(touchRow, Is.GreaterThan(header));
				Assert.That(mouseRow, Is.GreaterThan(touchRow), "The touch-size row must immediately precede the desktop input rows.");
				Assert.That(yaml, Does.Contain("DropDownButton@TOUCH_JOYSTICK_SIZE_DROPDOWN:"));
				Assert.That(yaml, Does.Contain("Text: label-touch-joystick-size-container"));
				Assert.That(yaml, Does.Contain("CollapseHiddenChildren: True"));
				Assert.That(english, Does.Contain("label-touch-joystick-size-container = Virtual joystick size"));
				Assert.That(english, Does.Contain(".small = Small (112 pt)"));
				Assert.That(english, Does.Contain(".medium = Medium (128 pt)"));
				Assert.That(english, Does.Contain(".large = Large (144 pt)"));
				Assert.That(chinese, Does.Contain("label-touch-joystick-size-container = 虚拟摇杆尺寸"));
				Assert.That(chinese, Does.Contain(".small = 小（112 pt）"));
				Assert.That(chinese, Does.Contain(".medium = 中（128 pt）"));
				Assert.That(chinese, Does.Contain(".large = 大（144 pt）"));
				Assert.That(inputLogic, Does.Contain("GetOrNull<DropDownButtonWidget>(\"TOUCH_JOYSTICK_SIZE_DROPDOWN\")"));
				Assert.That(settingsLogic, Does.Contain("ApplyPlatformVisibility(panelContainer.Get(panel), false)"));
				Assert.That(CountOccurrences(settingsLogic,
					"ApplyPlatformVisibility(panelContainer.Get(panel)"), Is.EqualTo(1),
					"The iOS constructor path already applies visibility while laying out each panel.");
			});
		}

		[Test]
		public void IosFiltersEditorHotkeysWithoutChangingDesktopGroups()
		{
			Assert.That(IosSettingsVisibilityPolicy.IsHotkeyGroupVisible(true, new[] { "Editor" }), Is.False);
			Assert.That(IosSettingsVisibilityPolicy.IsHotkeyGroupVisible(true, new[] { "Unit" }), Is.True);
			Assert.That(IosSettingsVisibilityPolicy.IsHotkeyGroupVisible(false, new[] { "Editor" }), Is.True);
		}

		[TestCase(true, 0, false)]
		[TestCase(true, 1, false)]
		[TestCase(true, 2, true)]
		[TestCase(false, 1, true)]
		public void SingleAudioDeviceSelectorIsHiddenOnlyOnIos(bool isIos, int count, bool expected)
		{
			Assert.That(IosSettingsVisibilityPolicy.ShouldShowAudioDeviceSelector(isIos, count), Is.EqualTo(expected));
		}

		[Test]
		public void RemainingSettingsColumnsExpandIntoTheFreedRowSpace()
		{
			var single = IosSettingsVisibilityPolicy.ColumnBounds(640, 1, 0, 16, 12);
			var left = IosSettingsVisibilityPolicy.ColumnBounds(640, 2, 0, 16, 12);
			var right = IosSettingsVisibilityPolicy.ColumnBounds(640, 2, 1, 16, 12);

			Assert.That(single, Is.EqualTo(new Rectangle(16, 0, 608, 1)));
			Assert.That(left.Left, Is.EqualTo(16));
			Assert.That(left.Right, Is.LessThanOrEqualTo(right.Left));
			Assert.That(right.Right, Is.EqualTo(624));
			Assert.That(left.Width, Is.EqualTo(right.Width));
		}

		[Test]
		public void SettingsColumnLayoutClampsInsetsForExtremelyNarrowRows()
		{
			var left = IosSettingsVisibilityPolicy.ColumnBounds(3, 2, 0, 16, 12);
			var right = IosSettingsVisibilityPolicy.ColumnBounds(3, 2, 1, 16, 12);

			Assert.That(left.Left, Is.GreaterThanOrEqualTo(0));
			Assert.That(left.Width, Is.GreaterThan(0));
			Assert.That(left.Right, Is.LessThanOrEqualTo(right.Left));
			Assert.That(right.Width, Is.GreaterThan(0));
			Assert.That(right.Right, Is.LessThanOrEqualTo(3));
		}

		[Test]
		public void SettingsLogicAppliesIosVisibilityBeforeColumnReflow()
		{
			var settings = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Settings", "SettingsLogic.cs"));

			Assert.That(settings, Does.Contain("ApplyIosVisibility"));
			Assert.That(settings, Does.Contain("ReflowIosVisibleColumns"));
			Assert.That(settings.IndexOf("ApplyIosVisibility", StringComparison.Ordinal),
				Is.LessThan(settings.IndexOf("ApplyIosSettingsScrollPanel", StringComparison.Ordinal)));
		}

		static IosSettingsLayout CreateLayout(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			return IosSettingsLayout.ForScreen(
				true,
				new Size(effectiveWidth, effectiveHeight),
				new Size(nativeWidth, nativeHeight),
				new IosSafeAreaInsets(safeLeft, safeTop, safeRight, safeBottom));
		}

		static void AssertPhysicalTarget(Rectangle bounds, double logicalPerPoint)
		{
			Assert.That(bounds.Width / logicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(bounds.Height / logicalPerPoint, Is.GreaterThanOrEqualTo(48));
		}

		static int CountOccurrences(string value, string needle)
		{
			var count = 0;
			var start = 0;
			while ((start = value.IndexOf(needle, start, StringComparison.Ordinal)) >= 0)
			{
				count++;
				start += needle.Length;
			}

			return count;
		}

		static string RepositoryRoot()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (root != null && !File.Exists(Path.Combine(root, "ios", "OpenRA.iOS", "Info.plist")))
				root = Directory.GetParent(root)?.FullName;

			Assert.That(root, Is.Not.Null, "Could not locate repository root.");
			return root!;
		}
	}
}
