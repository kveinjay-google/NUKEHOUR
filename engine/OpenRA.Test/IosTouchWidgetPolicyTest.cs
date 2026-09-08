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
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosTouchWidgetPolicyTest
	{
		Renderer previousRenderer;
		bool testRendererInstalled;

		[TearDown]
		public void ReleaseMouseFocus()
		{
			Ui.MouseFocusWidget = null;
			Ui.KeyboardFocusWidget = null;
			if (testRendererInstalled)
			{
				Game.Renderer = previousRenderer;
				testRendererInstalled = false;
			}
		}

		[TestCase(0, TouchStepperSegment.Decrement)]
		[TestCase(99, TouchStepperSegment.Decrement)]
		[TestCase(100, TouchStepperSegment.Value)]
		[TestCase(199, TouchStepperSegment.Value)]
		[TestCase(200, TouchStepperSegment.Increment)]
		[TestCase(299, TouchStepperSegment.Increment)]
		public void StepperHitTestingUsesThreeEqualSegments(int x, TouchStepperSegment expected)
		{
			Assert.That(IosTouchWidgetPolicy.StepperSegmentAt(x, 300), Is.EqualTo(expected));
		}

		[TestCase(0.5f, TouchStepperSegment.Decrement, 0.4f)]
		[TestCase(0.5f, TouchStepperSegment.Value, 0.5f)]
		[TestCase(0.5f, TouchStepperSegment.Increment, 0.6f)]
		[TestCase(0f, TouchStepperSegment.Decrement, 0f)]
		[TestCase(1f, TouchStepperSegment.Increment, 1f)]
		public void StepperChangesOnlyTheOuterSegmentsAndClamps(
			float value, TouchStepperSegment segment, float expected)
		{
			Assert.That(IosTouchWidgetPolicy.StepValue(value, 0f, 1f, 0.1f, segment),
				Is.EqualTo(expected).Within(0.001f));
		}

		[Test]
		public void TouchSliderWaitsForReleaseAndNeverEntersTrackDragging()
		{
			var slider = TouchSlider();

			Assert.That(slider.HandleMouseInput(Mouse(MouseInputEvent.Down, 20)), Is.True);
			Assert.That(slider.Value, Is.EqualTo(0.5f));
			Assert.That(slider.HandleMouseInput(Mouse(MouseInputEvent.Move, 280)), Is.True);
			Assert.That(slider.Value, Is.EqualTo(0.5f));
			Assert.That(slider.HandleMouseInput(Mouse(MouseInputEvent.Up, 20)), Is.True);
			Assert.That(slider.Value, Is.EqualTo(0.4f).Within(0.001f));
			Assert.That(slider.HasMouseFocus, Is.False);
		}

		[Test]
		public void TouchSliderCenterConsumesClickWithoutChangingTheValue()
		{
			var slider = TouchSlider();

			Assert.That(slider.HandleMouseInput(Mouse(MouseInputEvent.Down, 150)), Is.True);
			Assert.That(slider.HandleMouseInput(Mouse(MouseInputEvent.Up, 150)), Is.True);
			Assert.That(slider.Value, Is.EqualTo(0.5f));
			Assert.That(slider.HasMouseFocus, Is.False);
		}

		[Test]
		public void TouchSliderDragHandsOffToScrollableParentWithoutChangingValue()
		{
			var panel = TouchScrollPanel();
			var slider = TouchSlider();
			slider.Parent = panel;

			Assert.That(slider.HandleMouseInput(MouseAt(MouseInputEvent.Down, 20, 24)), Is.True);
			Assert.That(slider.HandleMouseInput(MouseAt(MouseInputEvent.Move, 20, 15)), Is.True);
			Assert.That(Ui.MouseFocusWidget, Is.SameAs(panel));
			Assert.That(slider.Value, Is.EqualTo(0.5f));

			Assert.That(panel.HandleMouseInput(MouseAt(MouseInputEvent.Up, 20, 15)), Is.True);
			Assert.That(Ui.MouseFocusWidget, Is.Null);
			Assert.That(slider.Value, Is.EqualTo(0.5f));
		}

		[Test]
		public void DisplacedTouchSliderReleaseHandsOffWithoutChangingValue()
		{
			var panel = TouchScrollPanel();
			var slider = TouchSlider();
			slider.Parent = panel;

			Assert.That(slider.HandleMouseInput(MouseAt(MouseInputEvent.Down, 20, 24)), Is.True);
			Assert.That(slider.HandleMouseInput(MouseAt(MouseInputEvent.Up, 20, 15)), Is.True);

			Assert.That(Ui.MouseFocusWidget, Is.Null);
			Assert.That(slider.Value, Is.EqualTo(0.5f));
		}

		[Test]
		public void ScrollableTextFieldDefersKeyboardFocusAndHandsDragToPanel()
		{
			InstallEmptyFontRenderer();
			var panel = TouchScrollPanel();
			var field = TouchTextField(panel);

			Assert.That(field.HandleMouseInput(MouseAt(MouseInputEvent.Down, 20, 24)), Is.True);
			Assert.That(Ui.KeyboardFocusWidget, Is.Null,
				"Keyboard focus must wait until the gesture is known to be a tap.");
			Assert.That(Ui.MouseFocusWidget, Is.SameAs(field));

			Assert.That(field.HandleMouseInput(MouseAt(MouseInputEvent.Move, 20, 15)), Is.True);
			Assert.That(Ui.MouseFocusWidget, Is.SameAs(panel));
			Assert.That(Ui.KeyboardFocusWidget, Is.Null);
			Assert.That(panel.HandleMouseInput(MouseAt(MouseInputEvent.Up, 20, 15)), Is.True);
		}

		[Test]
		public void ScrollableTextFieldTapTakesKeyboardFocusOnlyOnRelease()
		{
			InstallEmptyFontRenderer();
			var panel = TouchScrollPanel();
			var field = TouchTextField(panel);

			Assert.That(field.HandleMouseInput(MouseAt(MouseInputEvent.Down, 20, 24)), Is.True);
			Assert.That(Ui.KeyboardFocusWidget, Is.Null);
			Assert.That(field.HandleMouseInput(MouseAt(MouseInputEvent.Up, 20, 24)), Is.True);

			Assert.That(Ui.MouseFocusWidget, Is.Null);
			Assert.That(Ui.KeyboardFocusWidget, Is.SameAs(field));
		}

		[Test]
		public void DisplacedTextFieldReleaseScrollsWithoutTakingKeyboardFocus()
		{
			InstallEmptyFontRenderer();
			var panel = TouchScrollPanel();
			var field = TouchTextField(panel);

			Assert.That(field.HandleMouseInput(MouseAt(MouseInputEvent.Down, 20, 24)), Is.True);
			Assert.That(field.HandleMouseInput(MouseAt(MouseInputEvent.Up, 20, 15)), Is.True);

			Assert.That(Ui.MouseFocusWidget, Is.Null);
			Assert.That(Ui.KeyboardFocusWidget, Is.Null);
			Assert.That(panel.ChildOrigin.Y, Is.EqualTo(-9));
		}

		[Test]
		public void ScrollableHotkeyEntryDefersKeyboardFocusAndHandsDragToPanel()
		{
			var panel = TouchScrollPanel();
			var entry = TouchHotkeyEntry(panel);

			Assert.That(entry.HandleMouseInput(MouseAt(MouseInputEvent.Down, 20, 24)), Is.True);
			Assert.That(Ui.KeyboardFocusWidget, Is.Null,
				"Keyboard focus must wait until the gesture is known to be a tap.");
			Assert.That(Ui.MouseFocusWidget, Is.SameAs(entry));

			Assert.That(entry.HandleMouseInput(MouseAt(MouseInputEvent.Move, 20, 15)), Is.True);
			Assert.That(Ui.MouseFocusWidget, Is.SameAs(panel));
			Assert.That(Ui.KeyboardFocusWidget, Is.Null);
			Assert.That(panel.HandleMouseInput(MouseAt(MouseInputEvent.Up, 20, 15)), Is.True);
		}

		[Test]
		public void ScrollableHotkeyEntryTapTakesKeyboardFocusOnlyOnRelease()
		{
			var panel = TouchScrollPanel();
			var entry = TouchHotkeyEntry(panel);

			Assert.That(entry.HandleMouseInput(MouseAt(MouseInputEvent.Down, 20, 24)), Is.True);
			Assert.That(Ui.KeyboardFocusWidget, Is.Null);
			Assert.That(entry.HandleMouseInput(MouseAt(MouseInputEvent.Up, 20, 24)), Is.True);

			Assert.That(Ui.MouseFocusWidget, Is.Null);
			Assert.That(Ui.KeyboardFocusWidget, Is.SameAs(entry));
		}

		[Test]
		public void DisplacedHotkeyReleaseScrollsWithoutTakingKeyboardFocus()
		{
			var panel = TouchScrollPanel();
			var entry = TouchHotkeyEntry(panel);

			Assert.That(entry.HandleMouseInput(MouseAt(MouseInputEvent.Down, 20, 24)), Is.True);
			Assert.That(entry.HandleMouseInput(MouseAt(MouseInputEvent.Up, 20, 15)), Is.True);

			Assert.That(Ui.MouseFocusWidget, Is.Null);
			Assert.That(Ui.KeyboardFocusWidget, Is.Null);
			Assert.That(panel.ChildOrigin.Y, Is.EqualTo(-9));
		}

		[TestCase(true)]
		[TestCase(false)]
		public void ControlsKeepImmediateBehaviorWithoutAScrollCandidate(bool contentFits)
		{
			InstallEmptyFontRenderer();
			var panel = TouchScrollPanel();
			if (contentFits)
				panel.ContentHeight = panel.Bounds.Height;
			else
				panel.EnableContentDragging = false;

			var slider = TouchSlider();
			slider.Parent = panel;
			Assert.That(slider.HandleMouseInput(MouseAt(MouseInputEvent.Down, 20, 24)), Is.True);
			Assert.That(slider.HandleMouseInput(MouseAt(MouseInputEvent.Up, 20, 24)), Is.True);
			Assert.That(slider.Value, Is.EqualTo(0.4f).Within(0.001f));

			var field = TouchTextField(panel);
			Assert.That(field.HandleMouseInput(MouseAt(MouseInputEvent.Down, 20, 24)), Is.True);
			Assert.That(Ui.KeyboardFocusWidget, Is.SameAs(field));
			Assert.That(field.HandleMouseInput(MouseAt(MouseInputEvent.Up, 20, 24)), Is.True);
			Assert.That(field.YieldKeyboardFocus(), Is.True);

			var entry = TouchHotkeyEntry(panel);
			Assert.That(entry.HandleMouseInput(MouseAt(MouseInputEvent.Down, 20, 24)), Is.True);
			Assert.That(Ui.KeyboardFocusWidget, Is.SameAs(entry));
		}

		[Test]
		public void DesktopSliderKeepsTouchStepControlsDisabledByDefault()
		{
			Assert.That(new SliderWidget().UseTouchStepControls, Is.False);
		}

		[Test]
		public void StepperSegmentsTileTheWholeControlWithoutOverlap()
		{
			var bounds = new Rectangle(10, 20, 302, 48);
			var left = IosTouchWidgetPolicy.StepperSegmentBounds(bounds, TouchStepperSegment.Decrement);
			var center = IosTouchWidgetPolicy.StepperSegmentBounds(bounds, TouchStepperSegment.Value);
			var right = IosTouchWidgetPolicy.StepperSegmentBounds(bounds, TouchStepperSegment.Increment);

			Assert.That(left, Is.EqualTo(new Rectangle(10, 20, 101, 48)));
			Assert.That(center, Is.EqualTo(new Rectangle(111, 20, 101, 48)));
			Assert.That(right, Is.EqualTo(new Rectangle(212, 20, 100, 48)));
			Assert.That(left.Right, Is.EqualTo(center.Left));
			Assert.That(center.Right, Is.EqualTo(right.Left));
			Assert.That(right.Right, Is.EqualTo(bounds.Right));
		}

		[TestCase(0f, 1f, 6, 0.2f)]
		[TestCase(0f, 1f, 0, 0.1f)]
		[TestCase(100f, 0f, 11, 10f)]
		public void SettingsStepperDerivesItsStepFromTicksOrRange(
			float minimum, float maximum, int ticks, float expected)
		{
			Assert.That(IosTouchWidgetPolicy.StepForRange(minimum, maximum, ticks),
				Is.EqualTo(expected).Within(0.001f));
		}

		[Test]
		public void SliderDrawsThreeMeasuredButtonSegmentsAndSettingsOptInTheWholeTree()
		{
			var root = RepositoryRoot();
			var slider = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "SliderWidget.cs"));
			var settings = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Settings", "SettingsLogic.cs"));

			Assert.That(slider, Does.Contain("StepperSegmentBounds"));
			Assert.That(slider, Does.Contain("FormatStepperValue"));
			Assert.That(slider, Does.Contain("textSize.Y"));
			Assert.That(slider, Does.Contain("DrawBackground(\"button\""));
			Assert.That(settings, Does.Contain("ConfigureIosTouchWidgets(panel, layout)"));
			Assert.That(settings, Does.Contain("slider.UseTouchStepControls = true"));
			Assert.That(settings, Does.Contain("slider.TouchFont = \"IosBold\""));
			Assert.That(settings, Does.Contain("IosTouchWidgetPolicy.StepForRange"));
		}

		[Test]
		public void TouchSliderFontSurvivesCloning()
		{
			var slider = TouchSlider();
			slider.TouchFont = "IosBold";

			var clone = (SliderWidget)slider.Clone();

			Assert.That(clone.TouchFont, Is.EqualTo("IosBold"));
		}

		[TestCase(0.5f, 0f, 1f, "50%")]
		[TestCase(0.125f, 0f, 1f, "12.5%")]
		[TestCase(120f, 0f, 240f, "120")]
		[TestCase(1.236f, 0f, 2.5f, "1.24")]
		public void StepperValuesUseRangeAwareCompactFormatting(
			float value, float minimum, float maximum, string expected)
		{
			Assert.That(IosTouchWidgetPolicy.FormatStepperValue(value, minimum, maximum),
				Is.EqualTo(expected));
		}

		[TestCase(100, 107, 8, false)]
		[TestCase(100, 108, 8, true)]
		[TestCase(100, 91, 8, true)]
		public void ContentPanStartsOnlyAtTheConfiguredThreshold(
			int startY, int currentY, int threshold, bool expected)
		{
			Assert.That(IosTouchWidgetPolicy.ExceedsDragThreshold(startY, currentY, threshold),
				Is.EqualTo(expected));
		}

		[TestCase(50f, 300, 900, 0f)]
		[TestCase(-200f, 300, 900, -200f)]
		[TestCase(-800f, 300, 900, -600f)]
		[TestCase(-20f, 300, 200, 0f)]
		public void ContentOffsetsClampToTheScrollableRange(
			float offset, int panelHeight, int contentHeight, float expected)
		{
			Assert.That(IosTouchWidgetPolicy.ClampScrollOffset(offset, panelHeight, contentHeight),
				Is.EqualTo(expected));
		}

		[Test]
		public void ChildGestureKeepsChildFocusUntilThresholdThenScrollPanelTakesOver()
		{
			var panel = TouchScrollPanel();
			var child = new ContainerWidget();
			Ui.MouseFocusWidget = child;

			panel.BeginContentDragFromChild(MouseAt(MouseInputEvent.Down, 100, 100));

			Assert.That(panel.TryTakeContentDragFromChild(MouseAt(MouseInputEvent.Move, 100, 93)), Is.False);
			Assert.That(Ui.MouseFocusWidget, Is.SameAs(child), "A light tap must remain owned by the child button.");

			Assert.That(panel.TryTakeContentDragFromChild(MouseAt(MouseInputEvent.Move, 100, 91)), Is.True);
			Assert.That(Ui.MouseFocusWidget, Is.SameAs(panel), "The nearest enabled scroll panel must take focus after the threshold.");
			Assert.That(panel.ChildOrigin.Y, Is.EqualTo(-9), "The takeover event must scroll immediately instead of dropping one move.");

			Assert.That(panel.HandleMouseInput(MouseAt(MouseInputEvent.Up, 100, 91)), Is.True);
			Assert.That(Ui.MouseFocusWidget, Is.Null);
		}

		[Test]
		public void CancelledChildGestureCannotTakeFocusLater()
		{
			var panel = TouchScrollPanel();
			var child = new ContainerWidget();
			Ui.MouseFocusWidget = child;

			panel.BeginContentDragFromChild(MouseAt(MouseInputEvent.Down, 100, 100));
			panel.CancelContentDragFromChild();

			Assert.That(panel.TryTakeContentDragFromChild(MouseAt(MouseInputEvent.Move, 100, 70)), Is.False);
			Assert.That(Ui.MouseFocusWidget, Is.SameAs(child));
		}

		[Test]
		public void ScrollableButtonDefersMouseDownUntilATapAndDiscardsItForADrag()
		{
			var tapEvents = new List<string>();
			var tapPanel = TouchScrollPanel();
			var tapButton = TouchButton(
				tapPanel, () => tapEvents.Add("down"), () => tapEvents.Add("click"));

			Assert.That(tapButton.HandleMouseInput(MouseAt(MouseInputEvent.Down, 100, 50)), Is.True);
			Assert.That(tapEvents, Is.Empty, "A scrollable child must not run OnMouseDown before the gesture is known to be a tap.");
			Assert.That(tapButton.HandleMouseInput(MouseAt(MouseInputEvent.Up, 100, 50)), Is.True);
			Assert.That(tapEvents, Is.EqualTo(new[] { "down", "click" }));

			var dragEvents = new List<string>();
			var dragPanel = TouchScrollPanel();
			var dragButton = TouchButton(
				dragPanel, () => dragEvents.Add("down"), () => dragEvents.Add("click"));
			Assert.That(dragButton.HandleMouseInput(MouseAt(MouseInputEvent.Down, 100, 50)), Is.True);
			Assert.That(dragButton.HandleMouseInput(MouseAt(MouseInputEvent.Move, 100, 41)), Is.True);
			Assert.That(dragPanel.HandleMouseInput(MouseAt(MouseInputEvent.Up, 100, 41)), Is.True);
			Assert.That(dragEvents, Is.Empty, "A scroll takeover must discard both deferred callbacks.");

			var releaseEvents = new List<string>();
			var releasePanel = TouchScrollPanel();
			var releaseButton = TouchButton(
				releasePanel, () => releaseEvents.Add("down"), () => releaseEvents.Add("click"));
			Assert.That(releaseButton.HandleMouseInput(MouseAt(MouseInputEvent.Down, 100, 50)), Is.True);
			Assert.That(releaseButton.HandleMouseInput(MouseAt(MouseInputEvent.Up, 100, 41)), Is.True);
			Assert.That(releaseEvents, Is.Empty,
				"A displaced release without a move must discard both deferred callbacks too.");
		}

		[Test]
		public void NonScrollableOrOptOutPanelsKeepImmediateMouseDownBehavior()
		{
			var nonScrollableDowns = 0;
			var nonScrollablePanel = TouchScrollPanel();
			nonScrollablePanel.ContentHeight = nonScrollablePanel.Bounds.Height;
			var nonScrollableButton = TouchButton(nonScrollablePanel, () => nonScrollableDowns++, () => { });

			Assert.That(nonScrollableButton.HandleMouseInput(MouseAt(MouseInputEvent.Down, 100, 50)), Is.True);
			Assert.That(nonScrollableDowns, Is.EqualTo(1));
			nonScrollableButton.YieldMouseFocus(MouseAt(MouseInputEvent.Up, 100, 50));

			var optOutDowns = 0;
			var optOutPanel = TouchScrollPanel();
			optOutPanel.EnableContentDragging = false;
			var optOutButton = TouchButton(optOutPanel, () => optOutDowns++, () => { });

			Assert.That(optOutButton.HandleMouseInput(MouseAt(MouseInputEvent.Down, 100, 50)), Is.True);
			Assert.That(optOutDowns, Is.EqualTo(1), "Desktop/default opt-out behavior must remain immediate.");
			optOutButton.YieldMouseFocus(MouseAt(MouseInputEvent.Up, 100, 50));
		}

		[Test]
		public void CancelInputReleasesFocusedButtonWithoutActivatingIt()
		{
			var clicks = 0;
			var panel = TouchScrollPanel();
			panel.EnableContentDragging = false;
			var button = TouchButton(panel, () => { }, () => clicks++);

			Assert.That(button.HandleMouseInput(MouseAt(MouseInputEvent.Down, 100, 50)), Is.True);
			Assert.That(Ui.MouseFocusWidget, Is.SameAs(button));
			Assert.That(button.Depressed, Is.True);

			Assert.That(Ui.HandleInput(MouseAt(MouseInputEvent.Cancel, 100, 50)), Is.True);
			Assert.That(Ui.MouseFocusWidget, Is.Null);
			Assert.That(button.Depressed, Is.False);
			Assert.That(clicks, Is.Zero);
		}

		[Test]
		public void CancelInputClearsEveryTouchDragOwnerState()
		{
			var slider = TouchSlider();
			slider.UseTouchStepControls = false;
			Assert.That(slider.HandleMouseInput(MouseAt(MouseInputEvent.Down, 150, 24)), Is.True);
			Assert.That(PrivateBool(slider, "isMoving"), Is.True);
			Assert.That(Ui.HandleInput(MouseAt(MouseInputEvent.Cancel, 100, 50)), Is.True);
			Assert.That(PrivateBool(slider, "isMoving"), Is.False);

			#pragma warning disable SYSLIB0050
			var mixer = (ColorMixerWidget)FormatterServices.GetUninitializedObject(typeof(ColorMixerWidget));
			var world = (WorldInteractionControllerWidget)FormatterServices.GetUninitializedObject(
				typeof(WorldInteractionControllerWidget));
			#pragma warning restore SYSLIB0050

			SetPrivateBool(mixer, "isMoving", true);
			Ui.MouseFocusWidget = mixer;
			Assert.That(Ui.HandleInput(MouseAt(MouseInputEvent.Cancel, 100, 50)), Is.True);
			Assert.That(PrivateBool(mixer, "isMoving"), Is.False);

			SetPrivateBool(world, "isDragging", true);
			Ui.MouseFocusWidget = world;
			Assert.That(Ui.HandleInput(MouseAt(MouseInputEvent.Cancel, 100, 50)), Is.True);
			Assert.That(PrivateBool(world, "isDragging"), Is.False);
		}

		[Test]
		public void ButtonsForwardChildGesturesToTheNearestTouchScrollPanel()
		{
			var root = RepositoryRoot();
			var button = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "ButtonWidget.cs"));

			Assert.That(button, Does.Contain("BeginContentDragFromChild"));
			Assert.That(button, Does.Contain("TryTakeContentDragFromChild"));
			Assert.That(button, Does.Contain("CancelContentDragFromChild"));
			Assert.That(button, Does.Contain("touchPressDeferred"));
		}

		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		public void ContentDragThresholdTracksTheCurrentPhysicalScale(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var snapshot = Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);
			var threshold = IosTouchWidgetPolicy.ContentDragThreshold(snapshot.LogicalPerPoint);

			Assert.That(threshold / snapshot.LogicalPerPoint, Is.GreaterThanOrEqualTo(8));
		}

		[Test]
		public void ScrollGeometryCannotCreateNegativeOrOversizedTrackParts()
		{
			var tiny = IosTouchWidgetPolicy.ScrollGeometry(20, 16, 48, 500, 10);
			Assert.That(tiny.ButtonSize, Is.EqualTo(8));
			Assert.That(tiny.TrackHeight, Is.Zero);
			Assert.That(tiny.ThumbHeight, Is.Zero);

			var normal = IosTouchWidgetPolicy.ScrollGeometry(200, 300, 48, 900, 10);
			Assert.That(normal.ButtonSize, Is.EqualTo(48));
			Assert.That(normal.TrackHeight, Is.EqualTo(204));
			Assert.That(normal.ThumbHeight, Is.EqualTo(68));
			Assert.That(normal.ThumbHeight, Is.LessThanOrEqualTo(normal.TrackHeight));
		}

		[Test]
		public void ScrollArrowDecorationsAreCenteredInsideEveryButtonSize()
		{
			var touch = IosTouchWidgetPolicy.CenteredDecorationOrigin(
				new Rectangle(100, 200, 48, 48), new Size(16, 16), 0);
			var pressed = IosTouchWidgetPolicy.CenteredDecorationOrigin(
				new Rectangle(100, 200, 48, 48), new Size(16, 16), 1);
			var desktop = IosTouchWidgetPolicy.CenteredDecorationOrigin(
				new Rectangle(10, 20, 24, 24), new Size(16, 16), 0);

			Assert.Multiple(() =>
			{
				Assert.That(touch, Is.EqualTo(new int2(116, 216)));
				Assert.That(pressed, Is.EqualTo(new int2(117, 217)));
				Assert.That(desktop, Is.EqualTo(new int2(14, 24)),
					"Dynamic centering must preserve the existing 24px desktop geometry.");
			});
		}

		[Test]
		public void ScrollPanelContentDraggingIsOptInAndSettingsEnableItForHiddenHotkeysToo()
		{
			var root = RepositoryRoot();
			var scrollPanel = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "ScrollPanelWidget.cs"));
			var settings = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Settings", "SettingsLogic.cs"));

			Assert.That(scrollPanel, Does.Contain("public bool EnableContentDragging = false"));
			Assert.That(scrollPanel, Does.Contain("IosTouchWidgetPolicy.ExceedsDragThreshold"));
			Assert.That(scrollPanel, Does.Contain("IosTouchWidgetPolicy.ClampScrollOffset"));
			Assert.That(scrollPanel, Does.Contain("IosTouchWidgetPolicy.ScrollGeometry"));
			Assert.That(scrollPanel, Does.Contain("IosTouchWidgetPolicy.CenteredDecorationOrigin"));
			Assert.That(CountOccurrences(scrollPanel, "Math.Max(0, rb.Width - scrollbarWidth + 1)"), Is.EqualTo(2),
				"Left and right scrollbar layouts must reserve the same legal content width.");
			Assert.That(settings, Does.Contain("scrollPanel.EnableContentDragging = true"));
			Assert.That(settings, Does.Contain("IosTouchWidgetPolicy.ContentDragThreshold(layout.LogicalPerPoint)"));
			Assert.That(settings, Does.Contain("ConfigureIosTouchWidgets(child, layout)"));
		}

		[Test]
		public void ScrollTrackAndArrowButtonsUseTheSameVisibleChromeEdge()
		{
			var root = RepositoryRoot();
			var chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));

			Assert.That(chrome, Does.Match(
				@"scrollpanel-bg:\s*\n\tImage: menupanel\.png\s*\n\tPanelRegion: 2, 2, 6, 6, 240, 240, 6, 6"),
				"The track must crop the panel's 2px outer dark rim so its red edge aligns with the arrow buttons.");
		}

		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		public void DynamicDropdownItemsAreAtLeastFortyEightPhysicalPoints(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var snapshot = Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);
			var height = IosTouchWidgetPolicy.EnsureMinimumTouchHeight(25, snapshot);

			Assert.That(height / snapshot.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void DropdownContentNeverOverlapsItsTouchScrollbar(bool scrollbarOnLeft)
		{
			var content = IosTouchWidgetPolicy.DropDownContentBounds(267, 89, 2);
			var childOriginX = scrollbarOnLeft ? 89 : 0;
			var renderedContent = new Rectangle(
				childOriginX + content.X, content.Y, content.Width, content.Height);

			if (scrollbarOnLeft)
			{
				Assert.That(renderedContent.Left, Is.GreaterThanOrEqualTo(89));
				Assert.That(renderedContent.Right, Is.LessThanOrEqualTo(267));
			}
			else
			{
				Assert.That(renderedContent.Left, Is.GreaterThanOrEqualTo(0));
				Assert.That(renderedContent.Right, Is.LessThanOrEqualTo(267 - 89));
			}

			Assert.That(content.Width, Is.GreaterThan(0));
		}

		[Test]
		public void IPhoneDropdownReservesARealFortyEightPointScrollbarHitTarget()
		{
			var snapshot = Snapshot(1558, 720, 844, 390, 47, 0, 47, 21);
			var touchTarget = IosTouchWidgetPolicy.EnsureMinimumTouchHeight(0, snapshot);
			var width = IosTouchWidgetPolicy.DropDownPanelWidth(180, touchTarget, snapshot.SafeBounds.Width);
			var maximumHeight = IosTouchWidgetPolicy.MaximumPopupHeight(Math.Max(150, 2 * touchTarget), snapshot);
			var height = IosTouchWidgetPolicy.DropDownPopupHeight(touchTarget, maximumHeight, touchTarget);
			var geometry = IosTouchWidgetPolicy.ScrollGeometry(width, height, touchTarget, touchTarget, 10);

			Assert.That(width, Is.LessThanOrEqualTo(snapshot.SafeBounds.Width));
			Assert.That(geometry.ButtonSize / snapshot.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(IosTouchWidgetPolicy.DropDownContentBounds(width, geometry.ButtonSize, 2).Right,
				Is.LessThanOrEqualTo(width - geometry.ButtonSize));
		}

		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21)]
		[TestCase(1024, 768, 1024, 768, 0, 0, 0, 20)]
		public void FactionPickerUsesFiveBySixTargetViewport(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var snapshot = Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);
			var layout = IosDropDownLayout.FactionPicker;
			var target = IosTouchWidgetPolicy.EnsureMinimumTouchHeight(0, snapshot);
			var width = IosTouchWidgetPolicy.DropDownPanelWidth(
				180, target, snapshot.SafeBounds.Width, layout.MinimumWidthTargets);
			var height = IosTouchWidgetPolicy.DropDownMaximumHeight(
				154, target, snapshot.SafeBounds.Height, layout.MaximumHeightTargets);
			var content = IosTouchWidgetPolicy.DropDownContentBounds(width, target, 2);

			Assert.Multiple(() =>
			{
				Assert.That(layout.MinimumWidthTargets, Is.EqualTo(5));
				Assert.That(layout.MaximumHeightTargets, Is.EqualTo(6));
				Assert.That(layout.HeaderHeightPoints, Is.EqualTo(24));
				Assert.That(layout.ItemImagePoints, Is.EqualTo(new Size(40, 20)));
				Assert.That(layout.OmitSingletonHeaders, Is.True);
				Assert.That(width, Is.EqualTo(Math.Min(snapshot.SafeBounds.Width, Math.Max(180, 5 * target))));
				Assert.That(height, Is.EqualTo(Math.Min(snapshot.SafeBounds.Height, 6 * target)));
				Assert.That(target / snapshot.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
				Assert.That(content.Right, Is.LessThanOrEqualTo(width - target));
			});

			if (nativeWidth == 844)
				Assert.That(content.Width / snapshot.LogicalPerPoint, Is.GreaterThanOrEqualTo(190));
		}

		[Test]
		public void Ra2FactionViewportShowsFiveSelectableRowsAndDesktopKeepsEveryHeader()
		{
			var layout = IosDropDownLayout.FactionPicker;
			const int Target = 48;
			const int PanelHeight = 6 * Target;
			var headerHeight = layout.HeaderHeightPoints;
			var y = 0;
			var visibleOptions = 0;
			var groups = new[] { 3, 1, 5, 4 };

			foreach (var optionCount in groups)
			{
				if (IosTouchWidgetPolicy.ShowDropDownGroupHeader(optionCount, true, layout))
					y += headerHeight;

				for (var option = 0; option < optionCount; option++)
				{
					var bounds = new WidgetBounds(0, y, 5 * Target, Target);
					if (bounds.Bottom <= PanelHeight)
						visibleOptions++;
					y = bounds.Bottom;
				}
			}

			Assert.That(visibleOptions, Is.GreaterThanOrEqualTo(5));
			Assert.That(groups.All(count =>
				IosTouchWidgetPolicy.ShowDropDownGroupHeader(count, false, layout)), Is.True);
		}

		[Test]
		public void DesktopFactionPickerYamlBaselineRemainsUnchanged()
		{
			var root = RepositoryRoot();
			var source = File.ReadAllText(Path.Combine(
				root, "engine", "mods", "common", "chrome", "lobby-players.yaml"));
			var scrollPanel = File.ReadAllText(Path.Combine(
				root, "engine", "OpenRA.Mods.Common", "Widgets", "ScrollPanelWidget.cs"));
			var mainStart = source.IndexOf("DropDownButton@FACTION:", StringComparison.Ordinal);
			var mainEnd = source.IndexOf("DropDownButton@TEAM_DROPDOWN:", mainStart, StringComparison.Ordinal);
			var main = source[mainStart..mainEnd];
			var templateStart = source.IndexOf("ScrollPanel@FACTION_DROPDOWN_TEMPLATE:", StringComparison.Ordinal);
			var template = source[templateStart..];

			Assert.Multiple(() =>
			{
				Assert.That(main, Does.Contain("Width: 140"));
				Assert.That(main, Does.Contain("Height: 25"));
				Assert.That(main, Does.Contain("Image@FACTIONFLAG:"));
				Assert.That(main, Does.Contain("Width: 30"));
				Assert.That(main, Does.Contain("Height: 15"));
				Assert.That(main, Does.Not.Contain("StretchToFit"));
				Assert.That(template, Does.Match(@"ScrollItem@HEADER:[\s\S]*?Height: 13"));
				Assert.That(template, Does.Match(@"ScrollItem@TEMPLATE:[\s\S]*?Height: 25"));
				Assert.That(template, Does.Match(@"Image@FLAG:[\s\S]*?Width: 30[\s\S]*?Height: 15"));
				Assert.That(template, Does.Not.Contain("StretchToFit"));
				Assert.That(scrollPanel, Does.Contain("public int TopBottomSpacing = 2"));
			});
		}

		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		public void DynamicDropdownMaximumHeightCannotExceedTheSafeArea(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var snapshot = Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);

			Assert.That(IosTouchWidgetPolicy.MaximumPopupHeight(snapshot.SafeBounds.Height + 200, snapshot),
				Is.EqualTo(snapshot.SafeBounds.Height));
			Assert.That(IosTouchWidgetPolicy.MaximumPopupHeight(240, snapshot), Is.EqualTo(240));
		}

		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		public void PopupPlacementStaysInsideSafeAreaAtEveryEdge(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var snapshot = Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);
			var safe = snapshot.SafeBounds;
			var anchors = new[]
			{
				new Rectangle(safe.Left, safe.Top, 120, 48),
				new Rectangle(safe.Right - 120, safe.Top, 120, 48),
				new Rectangle(safe.Left, safe.Bottom - 48, 120, 48),
				new Rectangle(safe.Right - 120, safe.Bottom - 48, 120, 48)
			};

			foreach (var anchor in anchors)
			{
				var popup = IosTouchWidgetPolicy.PlacePopup(anchor, anchor.X, safe.Width + 100, safe.Height + 100, safe);
				Assert.That(safe.Contains(popup), Is.True, $"{anchor} produced {popup} outside {safe}");
			}
		}

		[Test]
		public void PopupPlacementPrefersBelowThenFallsBackAboveAndConvertsToPanelRootCoordinates()
		{
			var safe = new Rectangle(80, 20, 1000, 700);
			var belowAnchor = new Rectangle(200, 100, 240, 48);
			var aboveAnchor = new Rectangle(200, 650, 240, 48);
			var below = IosTouchWidgetPolicy.PlacePopup(belowAnchor, 200, 240, 200, safe);
			var above = IosTouchWidgetPolicy.PlacePopup(aboveAnchor, 200, 240, 200, safe);

			Assert.That(below.Y, Is.EqualTo(belowAnchor.Bottom));
			Assert.That(above.Bottom, Is.EqualTo(aboveAnchor.Top));

			var relative = IosTouchWidgetPolicy.RelativeToRoot(above, new int2(37, 11));
			Assert.That(relative.X + 37, Is.EqualTo(above.X));
			Assert.That(relative.Y + 11, Is.EqualTo(above.Y));
			Assert.That(relative.Size, Is.EqualTo(above.Size));
		}

		[Test]
		public void DynamicDropdownCreationAppliesTouchItemsFontsScrollingAndSafePlacementOnlyOnIos()
		{
			var root = RepositoryRoot();
			var dropdown = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "DropDownButtonWidget.cs"));
			var lobbyUtils = File.ReadAllText(Path.Combine(
				root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Lobby", "LobbyUtils.cs"));

			Assert.That(dropdown, Does.Contain("if (Platform.IsIOS)"));
			Assert.That(dropdown, Does.Contain("AdaptIosDropDownPanel"));
			Assert.That(dropdown, Does.Contain("AdaptIosDropDownItem"));
			Assert.That(dropdown, Does.Contain("item.Children.OfType<LabelWidget>()"));
			Assert.That(dropdown, Does.Contain("label.Bounds.Height = item.Bounds.Height"));
			Assert.That(dropdown, Does.Contain("header ? \"IosBold\" : \"IosRegular\""));
			Assert.That(dropdown, Does.Contain("panel.ScrollbarWidth = minimumTouchHeight"));
			Assert.That(dropdown, Does.Contain("DropDownPanelWidth"));
			Assert.That(dropdown, Does.Contain("DropDownContentBounds"));
			Assert.That(dropdown, Does.Contain("DropDownPopupHeight"));
			Assert.That(dropdown, Does.Contain("panel.EnableContentDragging = true"));
			Assert.That(dropdown, Does.Contain("IosTouchWidgetPolicy.PlacePopup"));
			Assert.That(dropdown, Does.Contain("IosTouchWidgetPolicy.RelativeToRoot"));
			Assert.That(dropdown, Does.Contain("IosTouchWidgetPolicy.MaximumPopupHeight"));
			Assert.That(dropdown, Does.Contain("IosDropDownLayout.Default"));
			Assert.That(dropdown, Does.Contain("iosLayout.OmitSingletonHeaders"));
			Assert.That(dropdown, Does.Contain("panel.TopBottomSpacing = 0"));
			Assert.That(dropdown, Does.Contain("flag.StretchToFit = true"));
			Assert.That(lobbyUtils, Does.Contain("IosDropDownLayout.FactionPicker"));
			Assert.That(lobbyUtils, Does.Contain("label.GetText = () => WidgetUtils.TruncateText("));
			Assert.That(lobbyUtils, Does.Not.Contain("var labelText = WidgetUtils.TruncateText("));
			Assert.That(dropdown, Does.Contain("panelY + oldBounds.Height > Game.Renderer.Resolution.Height"),
				"Desktop popup placement must retain its original path.");
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

		static SliderWidget TouchSlider()
		{
			return new SliderWidget
			{
				Bounds = new WidgetBounds(0, 0, 300, 48),
				MinimumValue = 0,
				MaximumValue = 1,
				Value = 0.5f,
				TouchStep = 0.1f,
				UseTouchStepControls = true
			};
		}

		static ScrollPanelWidget TouchScrollPanel()
		{
			#pragma warning disable SYSLIB0050
			var panel = (ScrollPanelWidget)FormatterServices.GetUninitializedObject(typeof(ScrollPanelWidget));
			#pragma warning restore SYSLIB0050
			typeof(Widget).GetField(nameof(Widget.Children))!.SetValue(panel, new List<Widget>());
			panel.Bounds = new WidgetBounds(0, 0, 300, 300);
			panel.ContentHeight = 900;
			panel.ScrollBar = ScrollBar.Right;
			panel.ScrollbarWidth = 90;
			panel.EnableContentDragging = true;
			panel.ContentDragThreshold = 8;
			return panel;
		}

		static ButtonWidget TouchButton(ScrollPanelWidget parent, Action onMouseDown, Action onClick)
		{
			#pragma warning disable SYSLIB0050
			var button = (ButtonWidget)FormatterServices.GetUninitializedObject(typeof(ButtonWidget));
			#pragma warning restore SYSLIB0050
			typeof(Widget).GetField(nameof(Widget.Children))!.SetValue(button, new List<Widget>());
			button.Parent = parent;
			button.Bounds = new WidgetBounds(0, 0, 200, 90);
			button.IsVisible = () => true;
			button.IsDisabled = () => false;
			button.OnMouseDown = _ => onMouseDown();
			button.OnMouseUp = _ => onClick();
			return button;
		}

		static TextFieldWidget TouchTextField(ScrollPanelWidget parent)
		{
			#pragma warning disable SYSLIB0050
			var field = (TextFieldWidget)FormatterServices.GetUninitializedObject(typeof(TextFieldWidget));
			#pragma warning restore SYSLIB0050
			typeof(Widget).GetField(nameof(Widget.Children))!.SetValue(field, new List<Widget>());
			InitializeTouchScrollHandoff(field);
			field.Parent = parent;
			field.Bounds = new WidgetBounds(0, 0, 200, 48);
			field.IsVisible = () => true;
			field.IsDisabled = () => false;
			field.OnLoseFocus = () => { };
			field.IsValid = () => true;
			field.Font = "TestEmpty";
			field.Text = "";
			return field;
		}

		static HotkeyEntryWidget TouchHotkeyEntry(ScrollPanelWidget parent)
		{
			#pragma warning disable SYSLIB0050
			var entry = (HotkeyEntryWidget)FormatterServices.GetUninitializedObject(typeof(HotkeyEntryWidget));
			#pragma warning restore SYSLIB0050
			typeof(Widget).GetField(nameof(Widget.Children))!.SetValue(entry, new List<Widget>());
			InitializeTouchScrollHandoff(entry);
			entry.Parent = parent;
			entry.Bounds = new WidgetBounds(0, 0, 200, 48);
			entry.IsVisible = () => true;
			entry.IsDisabled = () => false;
			entry.OnLoseFocus = () => { };
			entry.IsValid = () => true;
			return entry;
		}

		static void InitializeTouchScrollHandoff(Widget widget)
		{
			var field = widget.GetType().GetField("touchScrollHandoff", BindingFlags.Instance | BindingFlags.NonPublic);
			if (field == null)
				return;

			field.SetValue(widget, Activator.CreateInstance(field.FieldType, true));
		}

		static bool PrivateBool(object instance, string name)
		{
			return (bool)instance.GetType().GetField(name,
				BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
		}

		static void SetPrivateBool(object instance, string name, bool value)
		{
			instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
				.SetValue(instance, value);
		}

		void InstallEmptyFontRenderer()
		{
			previousRenderer = Game.Renderer;
			#pragma warning disable SYSLIB0050
			var renderer = (Renderer)FormatterServices.GetUninitializedObject(typeof(Renderer));
			var font = (SpriteFont)FormatterServices.GetUninitializedObject(typeof(SpriteFont));
			#pragma warning restore SYSLIB0050
			renderer.Fonts = new Dictionary<string, SpriteFont> { { "TestEmpty", font } };
			Game.Renderer = renderer;
			testRendererInstalled = true;
		}

		static MouseInput Mouse(MouseInputEvent ev, int x)
		{
			return new MouseInput(ev, MouseButton.Left, new int2(x, 24), int2.Zero, Modifiers.None, 0);
		}

		static MouseInput MouseAt(MouseInputEvent ev, int x, int y)
		{
			return new MouseInput(ev, MouseButton.Left, new int2(x, y), int2.Zero, Modifiers.None, 0);
		}

		static string RepositoryRoot()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (root != null && !Directory.Exists(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets")))
				root = Directory.GetParent(root)?.FullName;

			Assert.That(root, Is.Not.Null, "Could not locate repository root.");
			return root!;
		}

		static int CountOccurrences(string source, string value)
		{
			var count = 0;
			var offset = 0;
			while ((offset = source.IndexOf(value, offset, System.StringComparison.Ordinal)) >= 0)
			{
				count++;
				offset += value.Length;
			}

			return count;
		}
	}
}
