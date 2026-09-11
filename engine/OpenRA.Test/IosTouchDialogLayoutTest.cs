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
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosTouchDialogLayoutTest
	{
		static readonly object[] Devices =
		{
			new object[] { 1558, 720, 844, 390, 47d, 0d, 47d, 21d, true },
			new object[] { 1560, 720, 932, 430, 59d, 0d, 59d, 21d, true },
			new object[] { 1180, 820, 1180, 820, 0d, 0d, 0d, 20d, false },
			new object[] { 1366, 1024, 1366, 1024, 0d, 0d, 0d, 20d, false }
		};

		[TestCaseSource(nameof(Devices))]
		public void ConfirmationDialogsStayInsideTheSafeAreaAndUseWideTouchLayouts(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom, bool isPhone)
		{
			var snapshot = Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);
			var layout = IosTouchDialogLayout.CreateConfirmation(snapshot, 4, 3, false);

			Assert.That(layout.IsPhone, Is.EqualTo(isPhone));
			Assert.That(snapshot.SafeBounds.Contains(layout.Panel), Is.True);
			Assert.That(layout.Panel.Width / (double)snapshot.SafeBounds.Width,
				Is.GreaterThanOrEqualTo(isPhone ? 0.9 : 0.65));
			Assert.That(layout.LocalPanel.Contains(layout.Title), Is.True);
			Assert.That(layout.LocalPanel.Contains(layout.Body), Is.True);
			Assert.That(layout.LocalPanel.Contains(layout.Footer), Is.True);
			Assert.That(layout.Title.Bottom, Is.LessThanOrEqualTo(layout.Body.Top));
			Assert.That(layout.Body.Bottom, Is.LessThanOrEqualTo(layout.Footer.Top));
		}

		[TestCaseSource(nameof(Devices))]
		public void ConfirmationButtonsAndTextInputAreAtLeastFortyEightPhysicalPoints(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom, bool _)
		{
			var snapshot = Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);
			var buttons = IosTouchDialogLayout.CreateConfirmation(snapshot, 2, 3, false);
			var text = IosTouchDialogLayout.CreateConfirmation(snapshot, 1, 2, true);

			for (var i = 0; i < 3; i++)
				AssertPhysicalTarget(buttons.ButtonBounds(i), snapshot.LogicalPerPoint);

			AssertPhysicalTarget(text.TextInput, snapshot.LogicalPerPoint);
			Assert.That(text.Body.Bottom, Is.LessThanOrEqualTo(text.Footer.Top));
			Assert.That(text.PromptText.Bottom, Is.LessThanOrEqualTo(text.TextInput.Top));
		}

		[Test]
		public void ThreeConfirmationButtonsTileTheFooterWithoutOverlap()
		{
			var layout = IosTouchDialogLayout.CreateConfirmation(
				Snapshot(1558, 720, 844, 390, 47, 0, 47, 21), 3, 3, false);
			var first = layout.ButtonBounds(0);
			var second = layout.ButtonBounds(1);
			var third = layout.ButtonBounds(2);

			Assert.That(first.Right, Is.LessThanOrEqualTo(second.Left));
			Assert.That(second.Right, Is.LessThanOrEqualTo(third.Left));
			Assert.That(layout.Footer.Contains(first), Is.True);
			Assert.That(layout.Footer.Contains(second), Is.True);
			Assert.That(layout.Footer.Contains(third), Is.True);
		}

		[Test]
		public void DynamicPromptLineLayoutIsDeterministicAndNeverOverlapsTheFooter()
		{
			var snapshot = Snapshot(1558, 720, 844, 390, 47, 0, 47, 21);
			var first = IosTouchDialogLayout.CreateConfirmation(snapshot, 7, 2, false);
			var second = IosTouchDialogLayout.CreateConfirmation(snapshot, 7, 2, false);

			for (var i = 0; i < 7; i++)
			{
				Assert.That(first.PromptLineBounds(i).Bottom, Is.LessThanOrEqualTo(first.Footer.Top));
				Assert.That(second.PromptLineBounds(i), Is.EqualTo(first.PromptLineBounds(i)));
				if (i > 0)
					Assert.That(first.PromptLineBounds(i - 1).Bottom,
						Is.LessThanOrEqualTo(first.PromptLineBounds(i).Top));
			}
		}

		[TestCaseSource(nameof(Devices))]
		public void ColorChooserUsesAContainedLargeNonOverlappingTouchLayout(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom, bool isPhone)
		{
			var snapshot = Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);
			var layout = IosTouchDialogLayout.CreateColorPicker(snapshot, 8, 2, 1);

			Assert.That(snapshot.SafeBounds.Contains(layout.Panel), Is.True);
			Assert.That(layout.Panel.Width / (double)snapshot.SafeBounds.Width,
				Is.GreaterThanOrEqualTo(isPhone ? 0.9 : 0.7));
			Assert.That(layout.LocalPanel.Contains(layout.Main), Is.True);
			Assert.That(layout.LocalPanel.Contains(layout.Sidebar), Is.True);
			Assert.That(layout.LocalPanel.Contains(layout.TabBar), Is.True);
			Assert.That(layout.Main.Right, Is.LessThanOrEqualTo(layout.Sidebar.Left));
			Assert.That(layout.Main.Bottom, Is.LessThanOrEqualTo(layout.TabBar.Top));
			Assert.That(layout.Sidebar.Bottom, Is.LessThanOrEqualTo(layout.TabBar.Top));
			Assert.That(layout.HueBackground.Bottom, Is.LessThanOrEqualTo(layout.MixerBackground.Top));
		}

		[TestCaseSource(nameof(Devices))]
		public void ColorControlsTabsButtonsAndEverySwatchMeetPhysicalTouchMinimum(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom, bool _)
		{
			var snapshot = Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);
			var layout = IosTouchDialogLayout.CreateColorPicker(snapshot, 8, 2, 1);

			AssertPhysicalTarget(layout.HueSlider, snapshot.LogicalPerPoint);
			AssertPhysicalTarget(layout.Mixer, snapshot.LogicalPerPoint);
			AssertPhysicalTarget(layout.MixerTabButton, snapshot.LogicalPerPoint);
			AssertPhysicalTarget(layout.PaletteTabButton, snapshot.LogicalPerPoint);
			AssertPhysicalTarget(layout.RandomButton, snapshot.LogicalPerPoint);
			AssertPhysicalTarget(layout.StoreButton, snapshot.LogicalPerPoint);
			for (var row = 0; row < 2; row++)
				for (var column = 0; column < 8; column++)
					AssertPhysicalTarget(layout.PresetSwatchBounds(column, row), snapshot.LogicalPerPoint);

			for (var column = 0; column < 8; column++)
				AssertPhysicalTarget(layout.CustomSwatchBounds(column, 0), snapshot.LogicalPerPoint);
		}

		[Test]
		public void DialogYamlUsesDedicatedIosLayoutLogicWithoutChangingDesktopGeometry()
		{
			var root = RepositoryRoot();
			var confirmation = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "chrome", "confirmation-dialogs.yaml"));
			var colors = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "chrome", "color-picker.yaml"));
			var confirmationLogic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "IosTouchDialogLogic.cs"));
			var colorLogic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "IosTouchColorPickerLogic.cs"));

			Assert.That(confirmation, Does.Contain("Logic: IosTouchDialogLogic"));
			Assert.That(confirmation, Does.Contain("Width: 600"));
			Assert.That(confirmation, Does.Contain("Height: 110"));
			Assert.That(colors, Does.Contain("Logic: ColorPickerLogic, IosTouchColorPickerLogic"));
			Assert.That(colors, Does.Contain("Width: 326"));
			Assert.That(colors, Does.Contain("Height: 154"));
			Assert.That(confirmationLogic, Does.Contain("if (!Platform.IsIOS)"));
			Assert.That(colorLogic, Does.Contain("if (!Platform.IsIOS)"));
		}

		[Test]
		public void DynamicConfirmationContentRequestsASecondIosRelayout()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Widgets", "ConfirmationDialogs.cs"));

			Assert.That(source, Does.Contain("IosTouchDialogLogic.Relayout(prompt)"));
		}

		[Test]
		public void ColorPickerReflowsDynamicSwatchesAndKeepsContinuousColorDragging()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "IosTouchColorPickerLogic.cs"));

			Assert.That(source, Does.Contain("LayoutSwatches(presetArea"));
			Assert.That(source, Does.Contain("LayoutSwatches(customArea"));
			Assert.That(source, Does.Contain("hueSlider.UseTouchStepControls = false"));
		}

		[Test]
		public void ClassicColorGridContainsExactlyNineTouchSwatchesWithoutCustomMixer()
		{
			var yaml = MiniYaml.FromString(File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "mods", "common", "chrome", "color-picker.yaml")), "color-picker");
			var grid = yaml.Single(node => node.Key == "Background@IOS_SKIRMISH_COLOR_GRID");
			var children = grid.Value.NodeWithKey("Children").Value.Nodes;
			var swatches = children.Where(node =>
				node.Key.StartsWith("ColorBlock@SWATCH_", StringComparison.Ordinal)).ToArray();

			Assert.That(swatches, Has.Length.EqualTo(9));
			Assert.That(grid.Value.ToString(), Does.Not.Contain("ColorMixer@MIXER"));
			for (var index = 0; index < swatches.Length; index++)
			{
				Assert.That(swatches[index].Key, Is.EqualTo($"ColorBlock@SWATCH_{index}"));
				var selected = swatches[index].Value.NodeWithKey("Children").Value.Nodes.Single(node =>
					node.Key == "Image@SELECTED");
				Assert.That(selected, Is.Not.Null);
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

		static void AssertPhysicalTarget(Rectangle bounds, double logicalPerPoint)
		{
			Assert.That(bounds.Width / logicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(bounds.Height / logicalPerPoint, Is.GreaterThanOrEqualTo(48));
		}

		static string RepositoryRoot()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (root != null && !Directory.Exists(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets")))
				root = Directory.GetParent(root)?.FullName;

			Assert.That(root, Is.Not.Null, "Could not locate repository root.");
			return root!;
		}
	}
}
