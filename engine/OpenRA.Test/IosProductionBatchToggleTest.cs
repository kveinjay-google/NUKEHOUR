#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Mods.RA2.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosProductionBatchToggleTest
	{
		static readonly (Size Renderer, Size Native, IosSafeAreaInsets Insets)[] Devices =
		{
			(new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(47, 0, 47, 21)),
			(new Size(1560, 720), new Size(932, 430), new IosSafeAreaInsets(59, 0, 59, 21)),
			(new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(55, 0, 39, 21)),
			(new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(39, 0, 55, 21)),
			(new Size(1133, 744), new Size(1133, 744), new IosSafeAreaInsets(0, 0, 0, 20)),
			(new Size(1180, 820), new Size(1180, 820), new IosSafeAreaInsets(0, 0, 0, 20)),
			(new Size(1366, 1024), new Size(1366, 1024), new IosSafeAreaInsets(0, 0, 0, 20)),
		};

		static bool Calls(MethodInfo caller, MethodInfo callee)
		{
			var il = caller.GetMethodBody()?.GetILAsByteArray();
			if (il == null)
				return false;

			for (var i = 0; i + sizeof(int) < il.Length; i++)
				if ((il[i] == 0x28 || il[i] == 0x6F) && BitConverter.ToInt32(il, i + 1) == callee.MetadataToken)
					return true;

			return false;
		}

		static T Uninitialized<T>() where T : class =>
			(T)FormatterServices.GetUninitializedObject(typeof(T));

		static void InitializeWidget(Widget widget, string id)
		{
			typeof(Widget).GetField(nameof(Widget.Children))!.SetValue(widget, new List<Widget>());
			widget.Id = id;
			widget.IsVisible = () => true;
		}

		static ButtonWidget CreateSlot(string id, out LabelWidget label, out ImageWidget icon)
		{
			var button = Uninitialized<ButtonWidget>();
			InitializeWidget(button, id);
			label = Uninitialized<LabelWidget>();
			InitializeWidget(label, "LABEL");
			icon = new ImageWidget { Id = "ICON" };
			button.AddChild(icon);
			button.AddChild(label);
			return button;
		}

		static void SetPrivateField<T>(object target, string name, T value)
		{
			target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
		}

		static (int Width, int Height) PngSize(string path)
		{
			using var stream = File.OpenRead(path);
			using var reader = new BinaryReader(stream);
			Assert.That(reader.ReadBytes(8), Is.EqualTo(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
			Assert.That(reader.ReadBytes(4), Is.EqualTo(new byte[] { 0, 0, 0, 13 }));
			Assert.That(reader.ReadBytes(4), Is.EqualTo(new byte[] { 73, 72, 68, 82 }));

			static int ReadBigEndianInt32(BinaryReader input)
			{
				var bytes = input.ReadBytes(4);
				return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
			}

			return (ReadBigEndianInt32(reader), ReadBigEndianInt32(reader));
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		[TestCase("Building")]
		[TestCase("Support")]
		public void StructureQueuesLimitEachItemToOne(string queue)
		{
			var path = Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "player.yaml");
			var player = MiniYaml.FromString(File.ReadAllText(path), path)
				.Single(node => node.Key == "Player");
			var productionQueue = player.Value.NodeWithKey($"ClassicProductionQueue@{queue}");

			Assert.That(productionQueue.Value.NodeWithKey("ItemLimit").Value.Value, Is.EqualTo("1"));
		}

		[TestCase("Infantry")]
		[TestCase("Vehicle")]
		[TestCase("Aircraft")]
		[TestCase("Ship")]
		public void UnitQueuesKeepTheirDefaultBatchLimit(string queue)
		{
			var path = Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "player.yaml");
			var player = MiniYaml.FromString(File.ReadAllText(path), path)
				.Single(node => node.Key == "Player");
			var productionQueue = player.Value.NodeWithKey($"ClassicProductionQueue@{queue}");

			Assert.That(productionQueue.Value.NodeWithKeyOrDefault("ItemLimit"), Is.Null);
		}

		[TestCase(Modifiers.None, 1, 1)]
		[TestCase(Modifiers.None, 5, 5)]
		[TestCase(Modifiers.Shift, 1, 5)]
		[TestCase(Modifiers.Shift, 5, 5)]
		[TestCase(Modifiers.None, 0, 1)]
		public void ProductionCountUsesEitherShiftOrTouchMultiplier(Modifiers modifiers, int multiplier, int expected)
		{
			Assert.That(ProductionBatchPolicy.ResolveStartCount(modifiers, multiplier), Is.EqualTo(expected));
		}

		[TestCase(0, 0, 0, 999, 0, 0, 5)]
		[TestCase(3, 0, 5, 999, 0, 0, 2)]
		[TestCase(2, 2, 0, 3, 0, 0, 1)]
		[TestCase(0, 0, 0, 999, 1, 0, 1)]
		[TestCase(0, 1, 0, 999, 1, 0, 0)]
		public void ProductionQueueAcceptsFiveOrdinaryBuildingsButPreservesLimits(
			int queueCount, int sameItemQueued, int queueLimit, int itemLimit,
			int buildLimit, int owned, int expected)
		{
			Assert.That(ProductionBatchPolicy.ResolveAcceptedCount(
				5, queueCount, sameItemQueued, queueLimit, itemLimit, buildLimit, owned), Is.EqualTo(expected));
		}

		[Test]
		public void ProductionQueueUsesTheSharedBatchLimitPolicy()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common",
				"Traits", "Player", "ProductionQueue.cs"));

			StringAssert.Contains("ProductionBatchPolicy.ResolveAcceptedCount", source);
			StringAssert.Contains("Info.QueueLimit, Info.ItemLimit, bi.BuildLimit, ownedCount", source);
		}

		[Test]
		public void TouchCommandBarKeepsFiveGroupsRequiredCommandsAndRemovesJoystickDuplicates()
		{
			var visible = CommandBarLayoutPolicy.AvailableIds(CommandBarCatalog.AllIds, true).ToArray();

			Assert.That(visible, Does.Contain("GROUP_01"));
			Assert.That(visible, Does.Contain("GROUP_05"));
			Assert.That(visible, Does.Not.Contain("GROUP_06"));
			Assert.That(visible, Does.Not.Contain("GROUP_10"));
			Assert.That(visible, Does.Not.Contain("SELECT_BY_TYPE"));
			Assert.That(visible, Does.Contain("CYCLE_BASE"));
			Assert.That(visible, Does.Not.Contain("FORCE_ATTACK"));
			Assert.That(visible, Does.Not.Contain("DEPLOY"));
			Assert.That(visible, Does.Not.Contain("FORCE_MOVE"));
			Assert.That(visible, Does.Not.Contain("GUARD"));
			Assert.That(visible, Does.Not.Contain("STANCE_ATTACKANYTHING"));
			Assert.That(visible, Does.Not.Contain("STANCE_DEFEND"));
			Assert.That(visible, Does.Not.Contain("STANCE_RETURNFIRE"));
			Assert.That(visible, Does.Contain("STOP"));
			Assert.That(visible, Does.Contain("PRODUCTION_X5"));
		}

		[Test]
		public void DesktopCommandBarKeepsCommandsThatHaveNoVirtualJoystickDuplicate()
		{
			var visible = CommandBarLayoutPolicy.AvailableIds(CommandBarCatalog.AllIds, false).ToArray();

			Assert.That(visible, Does.Contain("GROUP_10"));
			Assert.That(visible, Does.Contain("SELECT_BY_TYPE"));
			Assert.That(visible, Does.Contain("FORCE_ATTACK"));
			Assert.That(visible, Does.Contain("DEPLOY"));
			Assert.That(visible, Does.Contain("STOP"));
			Assert.That(visible, Does.Not.Contain("PRODUCTION_X5"));
		}

		[Test]
		public void DesktopPreferencesAndCompactOrderRemainUnchangedAcrossTouchMigrationVersions()
		{
			var savedOrder = new[] { "ATTACK_MOVE", "GROUP_01" };
			foreach (var version in new[] { 1, 2, 3, 4 })
			{
				var normalized = CommandBarLayoutPolicy.NormalizeVisibleOrder(
					savedOrder, false, version).ToArray();
				Assert.Multiple(() =>
				{
					Assert.That(normalized, Is.EqualTo(savedOrder), $"v{version}");
					Assert.That(normalized, Does.Not.Contain("STOP"), $"v{version}");
					Assert.That(normalized, Does.Not.Contain("CYCLE_BASE"), $"v{version}");
					Assert.That(normalized, Does.Not.Contain("PRODUCTION_X5"), $"v{version}");
				});
			}

			var desktopCompact = (string[])typeof(CustomCommandBarWidget).GetField(
				"DesktopCompactIds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
			Assert.That(desktopCompact, Is.EqualTo(new[]
			{
				"ATTACK_MOVE", "FORCE_MOVE", "FORCE_ATTACK", "GUARD",
				"DEPLOY", "SCATTER", "STOP", "QUEUE_ORDERS",
				"STANCE_ATTACKANYTHING", "STANCE_DEFEND", "STANCE_RETURNFIRE", "STANCE_HOLDFIRE",
			}));
		}

		[Test]
		public void TouchPreferenceMigrationKeepsLegacyX5ThresholdAndAppendsRequiredCommandsOnce()
		{
			Assert.That(CommandBarLayoutPolicy.CurrentVersion, Is.EqualTo(4));
			foreach (var version in new[] { 1, 2 })
			{
				var upgraded = CommandBarLayoutPolicy.NormalizeVisibleOrder(
					new[] { "GROUP_01", "STOP", "STOP" }, true, version).ToArray();
				Assert.Multiple(() =>
				{
					Assert.That(upgraded.Count(id => id == "PRODUCTION_X5"), Is.EqualTo(1), $"v{version}");
					Assert.That(upgraded.Count(id => id == "STOP"), Is.EqualTo(1), $"v{version}");
					Assert.That(upgraded.Count(id => id == "CYCLE_BASE"), Is.EqualTo(1), $"v{version}");
				});
			}

			foreach (var version in new[] { 3, 4 })
			{
				var customized = CommandBarLayoutPolicy.NormalizeVisibleOrder(
					new[] { "GROUP_01", "STOP", "STOP" }, true, version).ToArray();
				Assert.Multiple(() =>
				{
					Assert.That(customized, Does.Not.Contain("PRODUCTION_X5"), $"v{version}");
					Assert.That(customized.Count(id => id == "STOP"), Is.EqualTo(1), $"v{version}");
					Assert.That(customized.Count(id => id == "CYCLE_BASE"), Is.EqualTo(1), $"v{version}");
				});
			}

			var newInstall = CommandBarLayoutPolicy.NormalizeVisibleOrder(
				CommandBarCatalog.DefaultVisibleIds(), true, 4).ToArray();
			Assert.Multiple(() =>
			{
				Assert.That(newInstall, Does.Contain("STOP"));
				Assert.That(newInstall, Does.Contain("CYCLE_BASE"));
			});
		}

		[Test]
		public void TouchCompactOrderEndsWithQueueStopAndCycleBase()
		{
			var compact = (string[])typeof(CustomCommandBarWidget).GetField(
				"TouchCompactIds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;

			Assert.That(compact.TakeLast(3), Is.EqualTo(new[] { "QUEUE_ORDERS", "STOP", "CYCLE_BASE" }));
		}

		[Test]
		public void RequiredTouchCommandsCannotBeHiddenByAnyEditPath()
		{
			Assert.Multiple(() =>
			{
				Assert.That(CommandBarLayoutPolicy.IsRequiredTouchId("STOP", true), Is.True);
				Assert.That(CommandBarLayoutPolicy.IsRequiredTouchId("CYCLE_BASE", true), Is.True);
				Assert.That(CommandBarLayoutPolicy.IsRequiredTouchId("STOP", false), Is.False);
				Assert.That(CommandBarLayoutPolicy.CanHide("STOP", true), Is.False);
				Assert.That(CommandBarLayoutPolicy.CanHide("CYCLE_BASE", true), Is.False);
				Assert.That(CommandBarLayoutPolicy.CanHide("STOP", false), Is.True);
				Assert.That(CommandBarLayoutPolicy.CanHide("SCATTER", true), Is.True);
			});

			var widget = typeof(CustomCommandBarWidget);
			var canHide = typeof(CommandBarLayoutPolicy).GetMethod(nameof(CommandBarLayoutPolicy.CanHide))!;
			var toggle = widget.GetMethod("ToggleSlot", BindingFlags.Instance | BindingFlags.NonPublic)!;
			var tick = widget.GetMethod(nameof(CustomCommandBarWidget.Tick), BindingFlags.Instance | BindingFlags.Public)!;
			var context = widget.GetMethod(
				nameof(CustomCommandBarWidget.HandleMouseInput), BindingFlags.Instance | BindingFlags.Public)!;
			var leftClick = widget.GetNestedTypes(BindingFlags.NonPublic)
				.SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
				.FirstOrDefault(method => method.Name.Contains("EnterEditMode", StringComparison.Ordinal) && Calls(method, toggle));
			Assert.Multiple(() =>
			{
				Assert.That(Calls(toggle, canHide), Is.True, "Click toggles must reject required IDs immediately.");
				Assert.That(Calls(tick, canHide), Is.True, "Dragging a required ID to the hidden row must snap back.");
				Assert.That(Calls(context, toggle), Is.True, "The right-click edit path must share ToggleSlot.");
				Assert.That(leftClick, Is.Not.Null, "The left-click edit path must share ToggleSlot.");
			});
		}

		[TestCase(false)]
		[TestCase(true)]
		public void RequiredTouchSlotsKeepStableChromeSizeWithoutRedundantLabels(bool expanded)
		{
			foreach (var device in Devices)
			{
				var snapshot = new IosScreenSnapshot(device.Renderer, device.Native, device.Insets);
				foreach (var id in new[] { "STOP", "CYCLE_BASE" })
				{
					var layout = TouchCommandBarSlotPolicy.Create(snapshot, id, expanded);
					var buttonBounds = new Rectangle(0, 0, layout.ButtonSize.Width, layout.ButtonSize.Height);
					Assert.Multiple(() =>
					{
						Assert.That(layout.ButtonSize.Width, Is.GreaterThanOrEqualTo(44), id);
						Assert.That(layout.ButtonSize.Height, Is.GreaterThanOrEqualTo(44), id);
						Assert.That(layout.ShowLabel, Is.False, id);
						Assert.That(layout.Font, Is.EqualTo("TinyBold"), id);
						Assert.That(layout.LabelBounds.IsEmpty, Is.True, id);
						Assert.That(buttonBounds.Contains(layout.IconBounds), Is.True, id);
						Assert.That(layout.IconBounds.Size,
							Is.EqualTo(new Size(expanded ? 34 : 32, expanded ? 34 : 32)), id);
					});
				}
			}
		}

		[Test]
		public void TouchCommandBarKeepsNineSliceChromeAtAuthoredLogicalSize()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1564, 720), new Size(956, 440), new IosSafeAreaInsets(62, 0, 62, 21));

			foreach (var expanded in new[] { false, true })
			{
				var layout = TouchCommandBarSlotPolicy.Create(snapshot, "SCATTER", expanded);
				Assert.Multiple(() =>
				{
					Assert.That(layout.ButtonSize.Width,
						Is.EqualTo(expanded ? 48 : 44), $"width expanded={expanded}");
					Assert.That(layout.ButtonSize.Height,
						Is.EqualTo(expanded ? 52 : 44), $"height expanded={expanded}");
					Assert.That(layout.IconBounds.Size,
						Is.EqualTo(new Size(expanded ? 34 : 32, expanded ? 34 : 32)),
						$"icon expanded={expanded}");
				});
			}
		}

		[Test]
		public void IosProductionSidebarUsesFullViewportAndSeparatesPaletteFromBackgroundFillRows()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1564, 720), new Size(956, 440), new IosSafeAreaInsets(62, 0, 62, 21));
			var layout = IosIngameSidebarLayoutPolicy.Create(snapshot);

			Assert.Multiple(() =>
			{
				Assert.That(layout.TopBounds, Is.EqualTo(new Rectangle(1564 - 234, 0, 234, 275)));
				Assert.That(layout.ProductionBounds.Top, Is.EqualTo(layout.TopBounds.Bottom));
				Assert.That(layout.ProductionBounds.Bottom, Is.EqualTo(snapshot.EffectiveSize.Height));
				Assert.That(layout.MaximumRows, Is.EqualTo(7), "Only complete icon rows may appear above the bottom cap.");
				Assert.That(layout.BackgroundRows, Is.EqualTo(7), "Only complete background rows may be emitted.");
				Assert.That(layout.BackgroundRemainderHeight, Is.EqualTo(29),
					"A separately sized final fill must close the fractional gap without overlapping the bottom cap.");
				Assert.That(layout.BottomCapY, Is.EqualTo(layout.ProductionBounds.Height - 66));
				Assert.That(layout.ScrollButtonsY, Is.EqualTo(layout.BottomCapY),
					"The scroll buttons must overlay the authored button slot at the top of the bottom cap.");
				Assert.That(layout.ScrollButtonLocalY(-30) - 30, Is.EqualTo(layout.BottomCapY),
					"The PRODUCTION_TYPES parent offset must be applied exactly once.");
				Assert.That(layout.ScrollButtonLocalBounds(-30, true),
					Is.EqualTo(new WidgetBounds(89, layout.BottomCapY + 30, 77, 27)));
				Assert.That(layout.ScrollButtonLocalBounds(-30, false),
					Is.EqualTo(new WidgetBounds(12, layout.BottomCapY + 30, 77, 27)));
				Assert.That(layout.BackgroundRows * 50 + layout.BackgroundRemainderHeight,
					Is.EqualTo(layout.BottomCapY));
				Assert.That(IosIngameSidebarLayoutPolicy.BackgroundRowHeights(layout),
					Is.EqualTo(new[] { 50, 50, 50, 50, 50, 50, 50, 29 }));
			});
		}

		[Test]
		public void TouchCommandBarUsesOneSlotAndGlyphCanvasForEveryCompactCommand()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1564, 720), new Size(956, 440), new IosSafeAreaInsets(62, 0, 62, 21));
			var ids = new[]
			{
				"GROUP_01", "GROUP_02", "GROUP_03", "GROUP_04", "GROUP_05", "PRODUCTION_X5",
				"ATTACK_MOVE", "SCATTER", "QUEUE_ORDERS", "STOP", "CYCLE_BASE"
			};

			foreach (var expanded in new[] { false, true })
			{
				var layouts = ids.Select(id => TouchCommandBarSlotPolicy.Create(snapshot, id, expanded)).ToArray();
				Assert.Multiple(() =>
				{
					Assert.That(layouts.Select(layout => layout.ButtonSize).Distinct().ToArray(), Has.Length.EqualTo(1),
						$"Every touch command must occupy the same outer slot when expanded={expanded}.");
					Assert.That(layouts.Select(layout => layout.IconBounds).Distinct().ToArray(), Has.Length.EqualTo(1),
						$"Every touch glyph must use the same centered canvas when expanded={expanded}.");
					Assert.That(layouts.Select(layout => layout.Font).Distinct().ToArray(), Has.Length.EqualTo(1),
						$"Command labels must use one visual style when expanded={expanded}.");
					Assert.That(layouts.All(layout => !layout.ShowLabel && layout.LabelBounds.IsEmpty), Is.True,
						$"Touch command glyphs must communicate their action without duplicated labels when expanded={expanded}.");
				});
			}
		}

		[Test]
		public void ClassicProductionRelayoutReanchorsBothScrollButtons()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Widgets",
				"Logic", "Ingame", "ClassicProductionLogic.cs"));

			StringAssert.Contains("RefreshIosSidebarLayout(widget, palette, typesContainer, scrollUp, scrollDown);", source,
				"The live layout ticker must re-anchor both arrows after a renderer or widget relayout.");
			StringAssert.Contains("PositionIosScrollButton(typesContainer, scrollUp, layout);", source);
			StringAssert.Contains("PositionIosScrollButton(typesContainer, scrollDown, layout);", source);
			StringAssert.Contains("button.Bounds = layout.ScrollButtonLocalBounds", source,
				"Relayout must restore the complete authored slot, not only its vertical coordinate.");
		}

		[TestCase(false)]
		[TestCase(true)]
		public void TouchCommandBarPlacementIsCenteredBottomInsetAndSafeContained(bool expanded)
		{
			foreach (var device in Devices)
			{
				var snapshot = new IosScreenSnapshot(device.Renderer, device.Native, device.Insets);
				var safe = snapshot.SafeBounds;
				var size = new Size(
					Math.Min(safe.Width, snapshot.LogicalPoints(640)),
					snapshot.LogicalPoints(expanded ? 120 : 56));
				var placed = TouchCommandBarSlotPolicy.PlaceBar(snapshot, size);
				Assert.Multiple(() =>
				{
					Assert.That(safe.Contains(placed), Is.True);
					Assert.That(Math.Abs(2 * placed.X + placed.Width - (2 * safe.X + safe.Width)), Is.LessThanOrEqualTo(1));
					Assert.That(safe.Bottom - placed.Bottom, Is.EqualTo(snapshot.LogicalPoints(12)));
				});
			}
		}

		[Test]
		public void TouchCommandBarRefreshSignatureIncludesRendererNativeAndAsymmetricSafeArea()
		{
			var baseline = new IosScreenSnapshot(
				new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(55, 0, 39, 21));
			var rendererChanged = new IosScreenSnapshot(
				new Size(1559, 720), new Size(844, 390), new IosSafeAreaInsets(55, 0, 39, 21));
			var nativeChanged = new IosScreenSnapshot(
				new Size(1558, 720), new Size(845, 390), new IosSafeAreaInsets(55, 0, 39, 21));
			var safeAreaSwapped = new IosScreenSnapshot(
				new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(39, 0, 55, 21));

			Assert.Multiple(() =>
			{
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchLayout(
					true, false, default, default, default, baseline), Is.True);
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchLayout(
					true, true, baseline.EffectiveSize, baseline.NativePointSize, baseline.SafeBounds, baseline), Is.False);
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchLayout(
					true, true, baseline.EffectiveSize, baseline.NativePointSize, baseline.SafeBounds, rendererChanged), Is.True);
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchLayout(
					true, true, baseline.EffectiveSize, baseline.NativePointSize, baseline.SafeBounds, nativeChanged), Is.True);
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchLayout(
					true, true, baseline.EffectiveSize, baseline.NativePointSize, baseline.SafeBounds, safeAreaSwapped), Is.True);
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchLayout(
					false, false, default, default, default, baseline), Is.False);
			});

			var tick = typeof(CustomCommandBarWidget).GetMethod(
				nameof(CustomCommandBarWidget.Tick), BindingFlags.Instance | BindingFlags.Public)!;
			var refresh = typeof(CustomCommandBarWidget).GetMethod(
				nameof(CustomCommandBarWidget.ShouldRefreshTouchLayout), BindingFlags.Static | BindingFlags.Public)!;
			Assert.That(Calls(tick, refresh), Is.True, "The live widget must reflow on the complete screen signature.");
		}

		[TestCase(0, false, false)]
		[TestCase(0, true, true)]
		[TestCase(1, false, false)]
		[TestCase(1, true, true)]
		[TestCase(2, false, false)]
		[TestCase(2, true, true)]
		[TestCase(3, false, false)]
		[TestCase(3, true, true)]
		[TestCase(4, false, false)]
		[TestCase(4, true, true)]
		[TestCase(5, false, false)]
		[TestCase(5, true, true)]
		[TestCase(6, false, false)]
		[TestCase(6, true, true)]
		public void LiveTouchLayoutUsesOnePhysicalRowAndKeepsRequiredControlsInsideSafeArea(
			int deviceIndex, bool expanded, bool editing)
		{
			var device = Devices[deviceIndex];
			var snapshot = new IosScreenSnapshot(device.Renderer, device.Native, device.Insets);
			var commandBar = new CustomCommandBarWidget();
			var slots = new ContainerWidget { Id = "COMMAND_SLOTS" };
			commandBar.AddChild(slots);
			var buttons = (Dictionary<string, ButtonWidget>)typeof(CustomCommandBarWidget)
				.GetField("buttons", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(commandBar)!;
			var labels = (Dictionary<string, LabelWidget>)typeof(CustomCommandBarWidget)
				.GetField("labels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(commandBar)!;
			var icons = (Dictionary<string, ImageWidget>)typeof(CustomCommandBarWidget)
				.GetField("icons", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(commandBar)!;

			foreach (var id in new[] { "STOP", "CYCLE_BASE", "SCATTER" })
			{
				var button = CreateSlot(id, out var label, out var icon);
				slots.AddChild(button);
				buttons.Add(id, button);
				labels.Add(id, label);
				icons.Add(id, icon);
			}

			var edit = CreateSlot("EDIT_TOGGLE", out _, out _);
			var collapse = CreateSlot("COLLAPSE_TOGGLE", out _, out var collapseIcon);
			commandBar.AddChild(edit);
			commandBar.AddChild(collapse);
			SetPrivateField(commandBar, "editButton", edit);
			SetPrivateField(commandBar, "collapseButton", collapse);
			SetPrivateField(commandBar, "collapseIcon", collapseIcon);
			var visibleOrder = new List<string> { "STOP", "CYCLE_BASE" };
			if (!expanded && !editing)
				visibleOrder.Add("SCATTER");
			SetPrivateField(commandBar, "visibleOrder", visibleOrder);
			SetPrivateField(commandBar, "expanded", expanded);

			var apply = typeof(CustomCommandBarWidget).GetMethod(
				"ApplyTouchLayout", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(apply, Is.Not.Null, "The production widget needs an executable touch-layout seam.");
			apply!.Invoke(commandBar, new object[] { snapshot, editing, null });

			var rowHeight = expanded ? 52 : 44;
			Assert.Multiple(() =>
			{
				Assert.That(snapshot.SafeBounds.Contains(commandBar.RenderBounds), Is.True);
				Assert.That(collapse.Bounds.Height, Is.EqualTo(rowHeight));
				Assert.That(snapshot.SafeBounds.Contains(collapse.RenderBounds), Is.True);
				Assert.That(edit.Bounds.Height, Is.EqualTo(rowHeight));
				Assert.That(edit.IsVisible(), Is.EqualTo(expanded));
				if (expanded)
					Assert.That(snapshot.SafeBounds.Contains(edit.RenderBounds), Is.True);
			});

			foreach (var id in new[] { "STOP", "CYCLE_BASE" })
			{
				var button = buttons[id];
				Assert.Multiple(() =>
				{
					Assert.That(button.IsVisible(), Is.True, id);
					Assert.That(button.Bounds.Height, Is.EqualTo(rowHeight), id);
					Assert.That(snapshot.SafeBounds.Contains(button.RenderBounds), Is.True, id);
					Assert.That(labels[id].IsVisible(), Is.False, id);
					Assert.That(labels[id].Font, Is.EqualTo("TinyBold"), id);
					Assert.That(button.RenderBounds.Contains(icons[id].RenderBounds), Is.True, id);
					Assert.That(icons[id].StretchToFit, Is.True, id);
				});
			}

			if (editing)
			{
				Assert.Multiple(() =>
				{
					Assert.That(buttons["SCATTER"].IsVisible(), Is.True);
					Assert.That(buttons["SCATTER"].Bounds.Height, Is.EqualTo(rowHeight));
					Assert.That(buttons["SCATTER"].Bounds.Y, Is.GreaterThan(buttons["STOP"].Bounds.Y));
					Assert.That(snapshot.SafeBounds.Contains(buttons["SCATTER"].RenderBounds), Is.True);
				});
			}

			if (!expanded && !editing)
			{
				Assert.That(buttons["SCATTER"].IsVisible(), Is.True);
				Assert.That(icons["SCATTER"].StretchToFit, Is.True);
			}
		}

		[Test]
		public void RequiredTouchSlotsKeepTheirExistingLabelsAndIconBindings()
		{
			var layout = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "mods", "ra2", "chrome", "ingame-player.yaml"));
			foreach (var (id, collection, icon, label) in new[]
			{
				("STOP", "ios-commandbar-glyphs", "stop", "button-command-bar-stop.label"),
				("CYCLE_BASE", "ios-commandbar-glyphs", "cycle-base", "button-selection-bar-cycle-base.label"),
			})
			{
				var start = layout.IndexOf($"Button@{id}:", StringComparison.Ordinal);
				var end = layout.IndexOf("\n\t\t\t\t\t\tButton@", start + 1, StringComparison.Ordinal);
				var block = layout.Substring(start, end - start);
				Assert.Multiple(() =>
				{
					Assert.That(start, Is.GreaterThanOrEqualTo(0));
					StringAssert.Contains("ImageCollection: " + collection, block);
					StringAssert.Contains("ImageName: " + icon, block);
					StringAssert.Contains("Text: " + label, block);
				});
			}
		}

		[Test]
		public void CycleBasePolicyIsStatelessPrimaryFirstThenActorIdAndWraps()
		{
			var candidates = new[] { (ActorId: 9u, IsPrimary: false), (ActorId: 7u, IsPrimary: true), (ActorId: 3u, IsPrimary: false) };
			var ordered = BaseCyclePolicy.Order(candidates, candidate => candidate.ActorId, candidate => candidate.IsPrimary);

			Assert.Multiple(() =>
			{
				Assert.That(ordered.Select(candidate => candidate.ActorId), Is.EqualTo(new[] { 7u, 3u, 9u }));
				Assert.That(BaseCyclePolicy.Next(ordered, _ => false).ActorId, Is.EqualTo(7u));
				Assert.That(BaseCyclePolicy.Next(ordered, candidate => candidate.ActorId == 7u).ActorId, Is.EqualTo(3u));
				Assert.That(BaseCyclePolicy.Next(ordered, candidate => candidate.ActorId == 9u).ActorId, Is.EqualTo(7u));
			});
		}

		[Test]
		public void CycleBaseResolveUsesOrderedCycleBeforeFallback()
		{
			var firstBase = new object();
			var secondBase = new object();
			var fallback = new object();

			var resolved = BaseCyclePolicy.Resolve(
				new[] { firstBase, secondBase }, new[] { fallback }, candidate => candidate == firstBase);

			Assert.That(resolved, Is.SameAs(secondBase));
		}

		[Test]
		public void CycleBaseResolveReturnsFirstFallbackWhenNoBases()
		{
			var firstFallback = new object();
			var secondFallback = new object();

			var resolved = BaseCyclePolicy.Resolve(
				Array.Empty<object>(), new[] { firstFallback, secondFallback }, _ => false);

			Assert.That(resolved, Is.SameAs(firstFallback));
		}

		[Test]
		public void CycleBaseResolveReturnsNullWhenNoCandidateExists()
		{
			var resolved = BaseCyclePolicy.Resolve(
				Array.Empty<object>(), Array.Empty<object>(), _ => false);

			Assert.That(resolved, Is.Null);
		}

		[Test]
		public void CycleBaseExecuteRunsSelectCenterAndClickOnceInOrder()
		{
			var candidate = new object();
			var calls = new List<string>();
			var selectCount = 0;
			var centerCount = 0;
			var clickCount = 0;

			var executed = BaseCyclePolicy.Execute(
				candidate,
				selected =>
				{
					Assert.That(selected, Is.SameAs(candidate));
					selectCount++;
					calls.Add("select");
				},
				() =>
				{
					centerCount++;
					calls.Add("center");
				},
				() =>
				{
					clickCount++;
					calls.Add("click");
				});

			Assert.Multiple(() =>
			{
				Assert.That(executed, Is.True);
				Assert.That(selectCount, Is.EqualTo(1));
				Assert.That(centerCount, Is.EqualTo(1));
				Assert.That(clickCount, Is.EqualTo(1));
				Assert.That(calls, Is.EqualTo(new[] { "select", "center", "click" }));
			});
		}

		[Test]
		public void CycleBaseExecuteHasNoSideEffectsWhenCandidateIsNull()
		{
			var selectCount = 0;
			var centerCount = 0;
			var clickCount = 0;

			var executed = BaseCyclePolicy.Execute<object>(
				null, _ => selectCount++, () => centerCount++, () => clickCount++);

			Assert.Multiple(() =>
			{
				Assert.That(executed, Is.False);
				Assert.That(selectCount, Is.Zero);
				Assert.That(centerCount, Is.Zero);
				Assert.That(clickCount, Is.Zero);
			});
		}

		[Test]
		public void CycleBaseProductionPathConsumesTheStatelessPolicyAndPrimaryTrait()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "OpenRA.Mods.RA2", "Widgets", "Logic",
				"SelectionCommandBarLogic.cs"));

			StringAssert.Contains("BaseCyclePolicy.Order(", source);
			StringAssert.Contains("var nextBase = BaseCyclePolicy.Resolve(", source);
			StringAssert.Contains("BaseCyclePolicy.Execute(nextBase,", source);
			StringAssert.Contains("a => a.ActorID", source);
			StringAssert.Contains("TraitOrDefault<PrimaryBuilding>()?.IsPrimary == true", source);
			StringAssert.DoesNotContain("lastBase", source);
		}

		[Test]
		public void BottomCommandBarExposesProductionBatchToggle()
		{
			var root = RepositoryRoot();
			var layout = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame-player.yaml"));
			var chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));
			var catalog = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Widgets", "CommandBarPreferences.cs"));
			var logic = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Widgets", "Logic", "ProductionBatchToggleLogic.cs"));

			StringAssert.Contains("Button@PRODUCTION_X5:", layout);
			StringAssert.Contains("Logic: ProductionBatchToggleLogic", layout);
			StringAssert.Contains("Text: button-command-bar-production-x5.short", layout);
			StringAssert.Contains("ImageCollection: ios-commandbar-glyphs", layout);
			StringAssert.Contains("ImageName: production-x5", layout);
			StringAssert.Contains("production-x5: 163, 3, 26, 26", chrome);
			StringAssert.Contains("new(\"PRODUCTION_X5\", 48, true)", catalog);
			StringAssert.Contains("BuildCountMultiplier = enabled ? 5 : 1", logic);
			Assert.That(File.Exists(Path.Combine(root, "mods", "ra2", "uibits", "ios-commandbar-glyphs-unified.png")), Is.True);
		}

		[Test]
		public void ProductionBatchToggleDefersPaletteLookupUntilTheWidgetTreeIsAttached()
		{
			var root = RepositoryRoot();
			var logic = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Widgets", "Logic", "ProductionBatchToggleLogic.cs"));

			StringAssert.DoesNotContain("var palette = Ui.Root.Get<ProductionPaletteWidget>", logic);
			StringAssert.Contains("Ui.Root.GetOrNull<ProductionPaletteWidget>", logic);
		}

		[Test]
		public void BottomCommandBarUsesGeneratedCollapseAndExpandIcons()
		{
			var root = RepositoryRoot();
			var layout = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame-player.yaml"));
			var chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));
			var widget = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Widgets", "CustomCommandBarWidget.cs"));
			var image = Path.Combine(root, "mods", "ra2", "uibits", "commandbar-toggle-icons.png");

			StringAssert.Contains("ImageCollection: commandbar-toggle-icons", layout);
			StringAssert.Contains("ImageName: collapse", layout);
			StringAssert.Contains("collapse: 3, 3, 26, 26", chrome);
			StringAssert.Contains("expand: 35, 3, 26, 26", chrome);
			StringAssert.Contains("expanded ? \"collapse\" : \"expand\"", widget);
			StringAssert.Contains("collapseIcon.Bounds.X", widget);
			Assert.That(File.Exists(image), Is.True);
			Assert.That(PngSize(image), Is.EqualTo((64, 32)));
		}

		[Test]
		public void ProductionBatchToggleUsesTheSameFactionChromeAsItsNeighbours()
		{
			var root = RepositoryRoot();
			var layout = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame-player.yaml"));

			StringAssert.Contains("Logic: ProductionBatchToggleLogic, AddFactionSuffixLogic", layout,
				"The X5 slot must use the same faction button chrome as its neighbours.");
		}

		[Test]
		public void BottomCommandBarUsesOneGeneratedArmoredTray()
		{
			var root = RepositoryRoot();
			var chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));
			var atlas = Path.Combine(root, "mods", "ra2", "uibits", "commandbar-bar-v2.png");

			StringAssert.Contains("commandbar-panel:\n\tImage: commandbar-bar-v2.png", chrome);
			StringAssert.Contains("commandbar-panel-compact:\n\tImage: commandbar-bar-v2.png", chrome);
			Assert.That(File.Exists(atlas), Is.True);
			Assert.That(PngSize(atlas), Is.EqualTo((256, 128)),
				"The generated tray atlas must remain power-of-two for iOS/OpenGL uploads.");
		}

		[Test]
		public void UnexploredTerrainDoesNotOpenAWorldTooltip()
		{
			Assert.That(WorldTooltipVisibilityPolicy.ShouldShow(WorldTooltipType.Unexplored), Is.False);
			Assert.That(WorldTooltipVisibilityPolicy.ShouldShow(WorldTooltipType.Actor), Is.True);
			Assert.That(WorldTooltipVisibilityPolicy.ShouldShow(WorldTooltipType.Resource), Is.True);
		}
	}
}
