#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Mods.Common.Widgets.Logic.Ingame;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	[NonParallelizable]
	public sealed class IosViewportControlsLayoutTest
	{
		static readonly FieldInfo ChromeMetricsData = typeof(ChromeMetrics).GetField(
			"data", BindingFlags.NonPublic | BindingFlags.Static)!;
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

		public enum CommandBarState
		{
			Hidden,
			Compact,
			Expanded,
		}

		Dictionary<string, string> previousMetrics;

		sealed class RecordingSkinTarget : ITouchFactionSkinTarget, IIosViewportControlsSkinTarget
		{
			public string Skin { get; private set; }
			public List<string> Skins { get; } = new();

			public void ApplySkin(string skin)
			{
				Skin = skin;
				Skins.Add(skin);
			}

			public void ApplyViewportSkin(string skin)
			{
				ApplySkin(skin);
			}
		}

		[SetUp]
		public void SaveChromeMetrics()
		{
			previousMetrics = (Dictionary<string, string>)ChromeMetricsData.GetValue(null)!;
			TouchFactionSkin.ResetActive();
		}

		[TearDown]
		public void RestoreChromeMetrics()
		{
			TouchFactionSkin.ResetActive();
			ChromeMetricsData.SetValue(null, previousMetrics);
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "engine", "OpenRA.Game"))))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		static Dictionary<string, string> ReadRa2Metrics()
		{
			var metrics = new Dictionary<string, string>();
			var path = Path.Combine(RepositoryRoot(), "mods", "ra2", "metrics.yaml");
			foreach (var line in File.ReadLines(path))
			{
				var trimmed = line.Trim();
				var separator = trimmed.IndexOf(':');
				if (separator <= 0 || separator == trimmed.Length - 1)
					continue;

				metrics[trimmed[..separator]] = trimmed[(separator + 1)..].Trim();
			}

			return metrics;
		}

		static void LoadChromeMetrics(Dictionary<string, string> metrics)
		{
			ChromeMetricsData.SetValue(null, metrics);
		}

		static T Uninitialized<T>() where T : class =>
			(T)FormatterServices.GetUninitializedObject(typeof(T));

		static ButtonWidget UninitializedButton(string id, bool withIcon = false)
		{
			var button = Uninitialized<ButtonWidget>();
			button.Id = id;
			if (withIcon)
			{
				typeof(Widget).GetField(nameof(Widget.Children))!.SetValue(button, new List<Widget>());
				button.AddChild(new ImageWidget
				{
					Id = "ICON",
					ImageCollection = "ios-joystick-command-icons",
					ImageName = "ios-" + id.ToLowerInvariant().Replace('_', '-')
				});
				var label = Uninitialized<LabelWidget>();
				label.Id = "LABEL";
				typeof(Widget).GetField(nameof(Widget.Children))!.SetValue(label, new List<Widget>());
				button.AddChild(label);
			}

			return button;
		}

		static void SetPrivateField<T>(object target, string name, T value)
		{
			target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
				.SetValue(target, value);
		}

		static void InvokePrivate(object target, string name, params object[] arguments)
		{
			target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
				.Invoke(target, arguments);
		}

		static IEnumerable<TestCaseData> ResponsiveControlCases()
		{
			for (var device = 0; device < Devices.Length; device++)
			{
				foreach (var requestedSize in new[] { 112, 128, 144 })
				{
					foreach (var state in Enum.GetValues<CommandBarState>())
						yield return new TestCaseData(device, requestedSize, state)
							.SetName($"ResponsiveControls_Device{device}_{requestedSize}pt_{state}");
				}
			}
		}

		static Rectangle Inset(Rectangle bounds, int inset) => Rectangle.FromLTRB(
			bounds.Left + inset, bounds.Top + inset,
			bounds.Right - inset, bounds.Bottom - inset);

		static Rectangle Expand(Rectangle bounds, int amount) => bounds.IsEmpty
			? Rectangle.Empty
			: Rectangle.FromLTRB(
				bounds.Left - amount, bounds.Top - amount,
				bounds.Right + amount, bounds.Bottom + amount);

		static Rectangle CommandBarObstacle(IosScreenSnapshot snapshot, CommandBarState state)
		{
			if (state == CommandBarState.Hidden)
				return Rectangle.Empty;

			var safe = snapshot.SafeBounds;
			var margin = snapshot.LogicalPoints(12);
			var slotPoints = state == CommandBarState.Compact ? 44 : 52;
			var slotCount = state == CommandBarState.Compact ? 6 : 10;
			var height = snapshot.LogicalPoints(slotPoints);
			var width = Math.Min(
				safe.Width - 2 * margin,
				snapshot.LogicalPoints(slotPoints * slotCount));
			return new Rectangle(
				safe.Left + (safe.Width - width) / 2,
				safe.Bottom - margin - height,
				width,
				height);
		}

		static double CircleToRectangleGap(Rectangle circleBounds, Rectangle rectangle)
		{
			var centerX = circleBounds.X + circleBounds.Width / 2;
			var centerY = circleBounds.Y + circleBounds.Height / 2;
			var nearestX = Math.Clamp(centerX, rectangle.Left, rectangle.Right);
			var nearestY = Math.Clamp(centerY, rectangle.Top, rectangle.Bottom);
			var dx = nearestX - centerX;
			var dy = nearestY - centerY;
			return Math.Sqrt((double)dx * dx + (double)dy * dy) -
				Math.Min(circleBounds.Width, circleBounds.Height) / 2.0;
		}

		static Rectangle TranslateUp(Rectangle bounds, int amount) =>
			new(bounds.X, bounds.Y - amount, bounds.Width, bounds.Height);

		static Rectangle FindQuarterRingGap(
			Rectangle overall, IReadOnlyList<Rectangle> entities, int clearance)
		{
			var size = Math.Max(1, clearance);
			for (var y = overall.Top + clearance; y + size <= overall.Bottom - clearance; y++)
			{
				for (var x = overall.Left + clearance; x + size <= overall.Right - clearance; x++)
				{
					var candidate = new Rectangle(x, y, size, size);
					var expanded = Expand(candidate, clearance);
					if (overall.IntersectsWith(candidate) &&
						entities.All(entity => !entity.IntersectsWith(expanded)))
						return candidate;
				}
			}

			Assert.Fail("Unable to find empty quarter-ring space with the required obstacle clearance.");
			return Rectangle.Empty;
		}

		static bool Calls(MethodInfo caller, MethodInfo callee)
		{
			var il = caller.GetMethodBody()!.GetILAsByteArray()!;
			for (var i = 0; i + sizeof(int) < il.Length; i++)
				if ((il[i] == 0x28 || il[i] == 0x6F) && BitConverter.ToInt32(il, i + 1) == callee.MetadataToken)
					return true;

			return false;
		}

		[Test]
		public void TouchFactionResolverMapsEveryRa2FactionWithoutChangingGlobalYuriFallback()
		{
			var metrics = ReadRa2Metrics();
			LoadChromeMetrics(metrics);

			Assert.Multiple(() =>
			{
				foreach (var faction in new[] { "america", "korea", "france", "germany", "england" })
					Assert.That(TouchFactionSkin.Resolve(faction), Is.EqualTo("allies"), faction);
				foreach (var faction in new[] { "libya", "cuba", "iraq", "russia" })
					Assert.That(TouchFactionSkin.Resolve(faction), Is.EqualTo("soviets"), faction);

				Assert.That(TouchFactionSkin.Resolve("yuri"), Is.EqualTo("yuri"));
				Assert.That(metrics["FactionSuffix-yuri"], Is.EqualTo("soviets"));
				Assert.That(metrics["TouchFactionSuffix-yuri"], Is.EqualTo("yuri"));
			});
		}

		[Test]
		public void TouchFactionResolverPrefersTouchMetricsThenUsesCompatibleGlobalMetrics()
		{
			LoadChromeMetrics(new Dictionary<string, string>
			{
				{ "TouchFactionSuffix-priority", "yuri" },
				{ "FactionSuffix-priority", "soviets" },
				{ "FactionSuffix-legacy", "soviets" },
			});

			Assert.Multiple(() =>
			{
				Assert.That(TouchFactionSkin.Resolve("priority"), Is.EqualTo("yuri"));
				Assert.That(TouchFactionSkin.Resolve("legacy"), Is.EqualTo("soviets"));
			});
		}

		[Test]
		public void TouchFactionResolverNormalizesUnknownEmptyAndUnsupportedValuesToAllies()
		{
			LoadChromeMetrics(new Dictionary<string, string>
			{
				{ "TouchFactionSuffix-invalid", "psicorps" },
				{ "FactionSuffix-invalid", "soviets" },
			});

			Assert.Multiple(() =>
			{
				Assert.That(TouchFactionSkin.Resolve(null), Is.EqualTo("allies"));
				Assert.That(TouchFactionSkin.Resolve(string.Empty), Is.EqualTo("allies"));
				Assert.That(TouchFactionSkin.Resolve("unknown"), Is.EqualTo("allies"));
				Assert.That(TouchFactionSkin.Resolve("invalid"), Is.EqualTo("allies"));
			});
		}

		[Test]
		public void ActiveTouchSkinDefaultsAndResetsToAlliesAcrossWorlds()
		{
			LoadChromeMetrics(ReadRa2Metrics());

			Assert.That(TouchFactionSkin.Active, Is.EqualTo("allies"));
			Assert.That(TouchFactionSkin.ActivateForPlayer("america", false), Is.EqualTo("allies"));
			Assert.That(TouchFactionSkin.ActivateForPlayer("russia", false), Is.EqualTo("soviets"));
			Assert.That(TouchFactionSkin.Active, Is.EqualTo("soviets"));
			Assert.That(TouchFactionSkin.ActivateForPlayer("yuri", true), Is.EqualTo("allies"),
				"Spectators must never leak a previously selected faction skin.");
			Assert.That(TouchFactionSkin.Active, Is.EqualTo("allies"));

			Assert.That(TouchFactionSkin.ActivateForPlayer("unsupported", false), Is.EqualTo("allies"));
			Assert.That(TouchFactionSkin.Active, Is.EqualTo("allies"));
			TouchFactionSkin.SetActive("yuri");
			TouchFactionSkin.ResetActive();
			Assert.That(TouchFactionSkin.Active, Is.EqualTo("allies"),
				"A newly constructed world must start from the neutral Allied fallback.");
		}

		[Test]
		public void ViewportSkinLifecyclePropagatesWorldChangesAndResetsOutsideIos()
		{
			LoadChromeMetrics(ReadRa2Metrics());
			var target = new RecordingSkinTarget();

			Assert.That(IosViewportSkinLifecycle.Refresh(true, "america", false, target), Is.True);
			Assert.That(IosViewportSkinLifecycle.Refresh(true, "russia", false, target), Is.True);
			Assert.That(IosViewportSkinLifecycle.Refresh(true, "yuri", true, target), Is.True);
			Assert.That(IosViewportSkinLifecycle.Refresh(true, "unknown", false, target), Is.True);
			Assert.Multiple(() =>
			{
				Assert.That(target.Skins, Is.EqualTo(new[] { "allies", "soviets", "allies", "allies" }));
				Assert.That(TouchFactionSkin.Active, Is.EqualTo("allies"));
			});

			TouchFactionSkin.SetActive("yuri");
			Assert.That(IosViewportSkinLifecycle.Refresh(false, "russia", false, target), Is.False);
			Assert.Multiple(() =>
			{
				Assert.That(TouchFactionSkin.Active, Is.EqualTo("allies"));
				Assert.That(target.Skins, Has.Count.EqualTo(4), "Desktop must not apply touch chrome.");
			});
		}

		[Test]
		public void ViewportSkinLifecycleDrivesTheThreeProductionActionsAndJoystickMappings()
		{
			LoadChromeMetrics(ReadRa2Metrics());
			var joystick = new RecordingSkinTarget();
			var buttons = new[] { "DEPLOY", "SELECT_TYPE", "FORCE_ATTACK" }
				.Select(id => UninitializedButton(id, true)).ToArray();
			var icons = buttons.Select(button => button.Get<ImageWidget>("ICON")).ToArray();
			var previousBindings = icons.Select(icon => icon.GetSprite).ToArray();
			foreach (var button in buttons)
			{
				button.Background = "legacy";
				button.VisualHeight = 9;
			}

			var logic = Uninitialized<IosViewportActionsLogic>();
			SetPrivateField(logic, "joystickSkinTarget", joystick);
			SetPrivateField(logic, "actionButtons", buttons);
			SetPrivateField(logic, "actionIcons", icons);
			Assert.That(IosViewportSkinLifecycle.Refresh(true, "russia", false, logic), Is.True);

			Assert.Multiple(() =>
			{
				Assert.That(joystick.Skin, Is.EqualTo("soviets"));
				Assert.That(buttons.Select(button => button.Background), Has.All.EqualTo(string.Empty));
				Assert.That(buttons.Select(button => button.VisualHeight), Has.All.Zero);
				Assert.That(icons.Select(icon => icon.ImageCollection),
					Has.All.EqualTo("ios-touch-actions-soviets"));
				for (var i = 0; i < icons.Length; i++)
					Assert.That(icons[i].GetSprite, Is.Not.SameAs(previousBindings[i]),
						$"Action {i} must be rebound after its faction collection changes.");
			});
		}

		[Test]
		public void ViewportActionConstructorAndTickBothResetTheSharedSkinOutsideIos()
		{
			Assert.That(Platform.IsIOS, Is.False, "This lightweight integration seam exercises desktop reset.");
			var parent = new ContainerWidget();
			var joystick = Uninitialized<VirtualViewportJoystickWidget>();
			joystick.Id = "IOS_VIEWPORT_JOYSTICK";
			parent.AddChild(joystick);

			var actions = new ContainerWidget { Id = "IOS_VIEWPORT_ACTIONS" };
			parent.AddChild(actions);
			foreach (var id in new[] { "DEPLOY", "SELECT_TYPE", "FORCE_ATTACK" })
				actions.AddChild(UninitializedButton(id, true));

			TouchFactionSkin.SetActive("soviets");
			var logic = new IosViewportActionsLogic(
				actions, Uninitialized<World>(), Uninitialized<WorldRenderer>());
			var activeButtons = (ButtonWidget[])typeof(IosViewportActionsLogic)
				.GetField("actionButtons", BindingFlags.Instance | BindingFlags.NonPublic)!
				.GetValue(logic)!;
			Assert.That(TouchFactionSkin.Active, Is.EqualTo("allies"),
				"The constructor must initialize/reset the skin context for a new world.");
			Assert.That(activeButtons.Select(button => button.Id),
				Is.EqualTo(new[] { "DEPLOY", "SELECT_TYPE", "FORCE_ATTACK" }));

			TouchFactionSkin.SetActive("yuri");
			logic.Tick();
			Assert.That(TouchFactionSkin.Active, Is.EqualTo("allies"),
				"Tick must keep desktop worlds from consuming a stale iOS skin context.");
		}

		[Test]
		public void ViewportSkinApplicatorUpdatesJoystickThenBindsEachFactionIconCollection()
		{
			var events = new List<string>();
			var joystick = new RecordingSkinTarget();
			IosViewportSkinApplicator.Apply(
				"soviets",
				5,
				joystick,
				index => events.Add("chrome:" + index),
				(index, collection) => events.Add($"collection:{index}:{collection}"),
				index => events.Add("bind:" + index));

			Assert.That(joystick.Skin, Is.EqualTo("soviets"));
			for (var i = 0; i < 5; i++)
			{
				var chrome = events.IndexOf("chrome:" + i);
				var collection = events.IndexOf($"collection:{i}:ios-touch-actions-soviets");
				var bind = events.IndexOf("bind:" + i);
				Assert.Multiple(() =>
				{
					Assert.That(chrome, Is.GreaterThanOrEqualTo(0));
					Assert.That(collection, Is.GreaterThan(chrome));
					Assert.That(bind, Is.GreaterThan(collection),
						"BindButtonIcon must capture the faction collection after it is assigned.");
				});
			}
		}

		[Test]
		public void TouchSkinCollectionNamesAreExactForEveryFaction()
		{
			foreach (var skin in new[] { "allies", "soviets", "yuri" })
			{
				Assert.Multiple(() =>
				{
					Assert.That(TouchFactionSkin.JoystickCollection(skin),
						Is.EqualTo("ios-touch-joystick-" + skin));
					Assert.That(TouchFactionSkin.ActionCollection(skin),
						Is.EqualTo("ios-touch-actions-" + skin));
					Assert.That(TouchFactionSkin.QuickbarPanelCollection(skin, true),
						Is.EqualTo("ios-touch-quickbar-panel-" + skin));
					Assert.That(TouchFactionSkin.QuickbarPanelCollection(skin, false),
						Is.EqualTo("ios-touch-quickbar-panel-compact-" + skin));
					Assert.That(TouchFactionSkin.QuickbarButtonCollection(skin),
						Is.EqualTo("ios-touch-quickbar-button-" + skin));
					Assert.That(TouchFactionSkin.QuickbarToggleCollection(skin),
						Is.EqualTo("ios-touch-quickbar-toggle-" + skin));
				});
			}

			Assert.That(TouchFactionSkin.JoystickCollection("unsupported"),
				Is.EqualTo("ios-touch-joystick-allies"));
		}

		[Test]
		public void CommandBarChromeSelectionAppliesTouchSkinsOnlyOnIos()
		{
			foreach (var skin in new[] { "allies", "soviets", "yuri" })
			{
				Assert.Multiple(() =>
				{
					Assert.That(CustomCommandBarWidget.PanelBackgroundFor(true, true, skin),
						Is.EqualTo("ios-touch-quickbar-panel-" + skin));
					Assert.That(CustomCommandBarWidget.PanelBackgroundFor(true, false, skin),
						Is.EqualTo("ios-touch-quickbar-panel-compact-" + skin));
					Assert.That(CustomCommandBarWidget.ButtonBackgroundFor(true, "commandbar-button-soviets", skin),
						Is.EqualTo("ios-touch-quickbar-button-" + skin));
					Assert.That(CustomCommandBarWidget.ToggleCollectionFor(true, "commandbar-toggle-icons", skin),
						Is.EqualTo("ios-touch-quickbar-toggle-" + skin));
				});
			}

			Assert.Multiple(() =>
			{
				Assert.That(CustomCommandBarWidget.PanelBackgroundFor(false, true, "yuri"),
					Is.EqualTo("commandbar-panel"));
				Assert.That(CustomCommandBarWidget.PanelBackgroundFor(false, false, "yuri"),
					Is.EqualTo("commandbar-panel-compact"));
				Assert.That(CustomCommandBarWidget.ButtonBackgroundFor(
					false, "commandbar-button-soviets", "yuri"), Is.EqualTo("commandbar-button-soviets"));
				Assert.That(CustomCommandBarWidget.ToggleCollectionFor(
					false, "commandbar-toggle-icons", "yuri"), Is.EqualTo("commandbar-toggle-icons"));
			});
		}

		[Test]
		public void CommandBarTouchChromeRefreshesInitiallyAndOnlyWhenSkinOrExpansionChanges()
		{
			Assert.Multiple(() =>
			{
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchChrome(
					true, null, false, "allies", false), Is.True, "The first iOS layout must paint its chrome.");
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchChrome(
					true, "allies", false, "allies", false), Is.False);
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchChrome(
					true, "allies", false, "soviets", false), Is.True);
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchChrome(
					true, "soviets", false, "soviets", true), Is.True);
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchChrome(
					false, null, false, "yuri", true), Is.False, "Desktop must not consume touch chrome state.");
			});

			var catalogTargets = CommandBarCatalog.All.Select(slot => slot.Id).ToArray();
			var targets = CustomCommandBarWidget.TouchChromeTargets(
				catalogTargets, "EDIT_TOGGLE", "COLLAPSE_TOGGLE").ToArray();
			Assert.Multiple(() =>
			{
				Assert.That(targets.Take(catalogTargets.Length), Is.EqualTo(catalogTargets));
				Assert.That(targets[^2], Is.EqualTo("EDIT_TOGGLE"));
				Assert.That(targets[^1], Is.EqualTo("COLLAPSE_TOGGLE"));
			});

			string panel = null;
			string toggle = null;
			var backgrounds = targets.ToDictionary(target => target, _ => (string)null);
			CustomCommandBarWidget.ApplyTouchChromeToTargets(
				"yuri",
				false,
				targets,
				value => panel = value,
				(target, value) => backgrounds[target] = value,
				value => toggle = value);

			Assert.Multiple(() =>
			{
				Assert.That(panel, Is.EqualTo("ios-touch-quickbar-panel-compact-yuri"));
				Assert.That(backgrounds.Values, Has.All.EqualTo("ios-touch-quickbar-button-yuri"),
					"Every catalog slot plus edit and collapse must be repainted.");
				Assert.That(toggle, Is.EqualTo("ios-touch-quickbar-toggle-yuri"));
			});
		}

		[Test]
		public void CommandBarRefreshRepaintsEveryProductionTargetAcrossSkinAndExpansionChanges()
		{
			var commandBar = new CustomCommandBarWidget();
			var buttons = (Dictionary<string, ButtonWidget>)typeof(CustomCommandBarWidget)
				.GetField("buttons", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(commandBar)!;
			foreach (var slot in CommandBarCatalog.All)
				buttons.Add(slot.Id, UninitializedButton(slot.Id));

			var edit = UninitializedButton("EDIT_TOGGLE");
			var collapse = UninitializedButton("COLLAPSE_TOGGLE");
			var collapseIcon = new ImageWidget();
			SetPrivateField(commandBar, "editButton", edit);
			SetPrivateField(commandBar, "collapseButton", collapse);
			SetPrivateField(commandBar, "collapseIcon", collapseIcon);

			TouchFactionSkin.SetActive("yuri");
			InvokePrivate(commandBar, "RefreshTouchChrome", false);
			Assert.Multiple(() =>
			{
				Assert.That(commandBar.Background, Is.EqualTo("dialog"));
				Assert.That(buttons.Values.Select(button => button.Background), Has.All.Null);
				Assert.That(TouchFactionSkin.Active, Is.EqualTo("yuri"),
					"Desktop command bars must not consume or rewrite touch skin state.");
			});

			TouchFactionSkin.SetActive("allies");
			InvokePrivate(commandBar, "RefreshTouchChrome", true);
			Assert.Multiple(() =>
			{
				Assert.That(commandBar.Background, Is.EqualTo("ios-touch-quickbar-panel-compact-allies"));
				Assert.That(buttons.Values.Select(button => button.Background),
					Has.All.EqualTo("ios-touch-quickbar-button-allies"));
				Assert.That(edit.Background, Is.EqualTo("ios-touch-quickbar-button-allies"));
				Assert.That(collapse.Background, Is.EqualTo("ios-touch-quickbar-button-allies"));
				Assert.That(collapseIcon.ImageCollection, Is.EqualTo("ios-touch-quickbar-toggle-allies"));
			});

			edit.Background = "unchanged";
			InvokePrivate(commandBar, "RefreshTouchChrome", true);
			Assert.That(edit.Background, Is.EqualTo("unchanged"),
				"An unchanged skin and expansion state must not repaint the command bar.");

			TouchFactionSkin.SetActive("soviets");
			InvokePrivate(commandBar, "RefreshTouchChrome", true);
			Assert.That(buttons.Values.Select(button => button.Background),
				Has.All.EqualTo("ios-touch-quickbar-button-soviets"));

			SetPrivateField(commandBar, "expanded", true);
			InvokePrivate(commandBar, "RefreshTouchChrome", true);
			Assert.Multiple(() =>
			{
				Assert.That(commandBar.Background, Is.EqualTo("ios-touch-quickbar-panel-soviets"));
				Assert.That(edit.Background, Is.EqualTo("ios-touch-quickbar-button-soviets"));
				Assert.That(collapse.Background, Is.EqualTo("ios-touch-quickbar-button-soviets"));
			});
		}

		[Test]
		public void CommandBarTickAndLayoutBothCallTheProductionTouchChromeRefreshEntry()
		{
			var type = typeof(CustomCommandBarWidget);
			var refresh = type.GetMethod("RefreshTouchChromeIfNeeded",
				BindingFlags.Instance | BindingFlags.NonPublic)!;
			var tick = type.GetMethod(nameof(CustomCommandBarWidget.Tick),
				BindingFlags.Instance | BindingFlags.Public)!;
			var layout = type.GetMethod("ApplyLayout", BindingFlags.Instance | BindingFlags.NonPublic)!;

			Assert.Multiple(() =>
			{
				Assert.That(Calls(tick, refresh), Is.True,
					"Tick must repaint when the active faction changes without a geometry rebuild.");
				Assert.That(Calls(layout, refresh), Is.True,
					"ApplyLayout must paint initial and expanded/compact touch chrome immediately.");
			});
		}

		[Test]
		public void ScreenSnapshotConvertsPhysicalPointsAndDetectsCompactPhones()
		{
			var phone = new IosScreenSnapshot(
				new Size(1560, 720), new Size(932, 430), new IosSafeAreaInsets(59, 0, 59, 21));
			var tablet = new IosScreenSnapshot(
				new Size(1133, 744), new Size(1133, 744), new IosSafeAreaInsets(0, 0, 0, 20));

			Assert.Multiple(() =>
			{
				Assert.That(phone.LogicalPerPoint, Is.EqualTo(720.0 / 430).Within(0.000001));
				Assert.That(phone.LogicalPoints(-1), Is.Zero);
				Assert.That(phone.LogicalPoints(0), Is.Zero);
				Assert.That(phone.LogicalPoints(56), Is.EqualTo(94));
				Assert.That(phone.SafeBounds, Is.EqualTo(new Rectangle(99, 0, 1362, 684)));
				Assert.That(phone.IsCompactPhone, Is.True);
				Assert.That(tablet.IsCompactPhone, Is.False);
			});
		}

		[TestCase(1560, 720, 932, 430, true, "IosTouchLabel")]
		[TestCase(1558, 720, 844, 390, true, "IosTouchLabel")]
		[TestCase(1133, 744, 1133, 744, false, "TinyBold")]
		[TestCase(1180, 820, 1180, 820, false, "TinyBold")]
		[TestCase(1366, 1024, 1366, 1024, false, "TinyBold")]
		public void ViewportActionLabelLayoutPreservesTheApprovedPhysicalInsets(
			int width, int height, int nativeWidth, int nativeHeight,
			bool compactPhone, string expectedFont)
		{
			var snapshot = new IosScreenSnapshot(
				new Size(width, height), new Size(nativeWidth, nativeHeight),
				new IosSafeAreaInsets(0, 0, 0, 0));
			var buttonDiameter = snapshot.LogicalPoints(56);
			var layout = IosViewportActionLabelLayout.Create(snapshot, buttonDiameter);
			var horizontalInset = snapshot.LogicalPoints(6);
			var labelHeight = snapshot.LogicalPoints(14);
			var bottomInset = snapshot.LogicalPoints(6);
			var expectedBounds = new Rectangle(
				horizontalInset,
				buttonDiameter - bottomInset - labelHeight,
				buttonDiameter - 2 * horizontalInset,
				labelHeight);

			Assert.Multiple(() =>
			{
				Assert.That(snapshot.IsCompactPhone, Is.EqualTo(compactPhone));
				Assert.That(layout.Bounds, Is.EqualTo(expectedBounds));
				Assert.That(layout.Font, Is.EqualTo(expectedFont));
				Assert.That(new Rectangle(0, 0, buttonDiameter, buttonDiameter).Contains(layout.Bounds), Is.True);
				Assert.That(layout.Bounds.Bottom, Is.EqualTo(buttonDiameter - bottomInset));
				Assert.That(layout.Bounds.Height, Is.EqualTo(labelHeight));
			});
		}

		[Test]
		public void ViewportActionLabelLayoutAppliesBoundsAndFontToTheLiveLabel()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1560, 720), new Size(932, 430),
				new IosSafeAreaInsets(0, 0, 0, 0));
			var layout = IosViewportActionLabelLayout.Create(
				snapshot, snapshot.LogicalPoints(56));
			var label = Uninitialized<LabelWidget>();

			layout.ApplyTo(label);

			Assert.Multiple(() =>
			{
				Assert.That(label.Bounds, Is.EqualTo(new WidgetBounds(
					layout.Bounds.X, layout.Bounds.Y,
					layout.Bounds.Width, layout.Bounds.Height)));
				Assert.That(label.Font, Is.EqualTo(layout.Font));
			});
		}

		[TestCase(0, 128)]
		[TestCase(111, 128)]
		[TestCase(112, 112)]
		[TestCase(128, 128)]
		[TestCase(144, 144)]
		[TestCase(145, 128)]
		[TestCase(999, 128)]
		public void JoystickPointNormalizationOnlyAcceptsSupportedSizes(int requested, int expected)
		{
			Assert.That(IosViewportControlsLayout.NormalizeJoystickPoints(requested), Is.EqualTo(expected));
		}

		[Test]
		public void VirtualJoystickSettingDefaultsTo128Points()
		{
			Assert.That(new GameSettings().IosVirtualJoystickSize, Is.EqualTo(128));
		}

		[Test]
		public void VirtualJoystickSizeOptionsAreExactlyTheThreeSupportedValues()
		{
			Assert.That(InputSettingsLogic.IosVirtualJoystickSizes(), Is.EqualTo(new[] { 112, 128, 144 }));
		}

		[TestCase(112, "options-touch-joystick-size.small")]
		[TestCase(128, "options-touch-joystick-size.medium")]
		[TestCase(144, "options-touch-joystick-size.large")]
		[TestCase(0, "options-touch-joystick-size.medium")]
		[TestCase(999, "options-touch-joystick-size.medium")]
		public void VirtualJoystickSizeDisplayNormalizesInvalidStoredValues(int stored, string expectedLabelKey)
		{
			var settings = new GameSettings { IosVirtualJoystickSize = stored };

			Assert.Multiple(() =>
			{
				Assert.That(InputSettingsLogic.SelectedIosVirtualJoystickSize(settings), Is.EqualTo(
					IosViewportControlsLayout.NormalizeJoystickPoints(stored)));
				Assert.That(InputSettingsLogic.IosVirtualJoystickSizeLabelKey(stored), Is.EqualTo(expectedLabelKey));
			});
		}

		[Test]
		public void VirtualJoystickSizeSelectionWritesImmediatelyAndResetRestoresTheDefault()
		{
			var settings = new GameSettings();

			InputSettingsLogic.SelectIosVirtualJoystickSize(settings, 144);
			Assert.That(settings.IosVirtualJoystickSize, Is.EqualTo(144));

			InputSettingsLogic.SelectIosVirtualJoystickSize(settings, 999);
			Assert.That(settings.IosVirtualJoystickSize, Is.EqualTo(128));

			InputSettingsLogic.SelectIosVirtualJoystickSize(settings, 112);
			InputSettingsLogic.ResetIosVirtualJoystickSize(settings);
			Assert.That(settings.IosVirtualJoystickSize, Is.EqualTo(new GameSettings().IosVirtualJoystickSize));
		}

		[Test]
		public void VirtualJoystickSizeBindingAllowsPanelsWithoutTheOptionalControl()
		{
			var panelWithoutTouchSizeControl = new ContainerWidget();

			Assert.DoesNotThrow(() => InputSettingsLogic.BindIosVirtualJoystickSizeDropdown(
				panelWithoutTouchSizeControl, new GameSettings()));
		}

		[Test]
		public void JoystickSizeRefreshPolicyUsesTheLiveNormalizedSettingAndPlatformGuard()
		{
			var settings = new GameSettings();

			Assert.Multiple(() =>
			{
				Assert.That(IosViewportControlsLayout.ShouldRefreshForJoystickSize(
					false, true, 128, 144), Is.False, "Desktop must never apply the iOS layout.");
				Assert.That(IosViewportControlsLayout.ShouldRefreshForJoystickSize(
					true, false, 128, settings.IosVirtualJoystickSize), Is.True, "First iOS tick must initialize layout.");
				Assert.That(IosViewportControlsLayout.ShouldRefreshForJoystickSize(
					true, true, 128, 999), Is.False, "Invalid values normalize to the cached 128pt layout.");
				Assert.That(IosViewportControlsLayout.ShouldRefreshForJoystickSize(
					true, true, 128, 144), Is.True, "Changing 128pt to 144pt must refresh next tick.");
				Assert.That(IosViewportControlsLayout.ShouldRefreshForJoystickSize(
					true, true, 144, 144), Is.False, "An applied 144pt signature must remain stable.");

				settings.IosVirtualJoystickSize = 144;
				InputSettingsLogic.ResetIosVirtualJoystickSize(settings);
				Assert.That(IosViewportControlsLayout.ShouldRefreshForJoystickSize(
					true, true, 144, settings.IosVirtualJoystickSize), Is.True,
					"Resetting 144pt to the 128pt default must refresh next tick.");
			});
		}

		[Test]
		public void ViewportActionLayoutConsumesTheLiveJoystickRefreshPolicy()
		{
			var logic = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Ingame", "IosViewportActionsLogic.cs"));

			Assert.That(logic, Does.Contain("Game.Settings.Game.IosVirtualJoystickSize"));
			Assert.That(logic, Does.Contain("IosViewportControlsLayout.ShouldRefreshForJoystickSize"));
			Assert.That(logic, Does.Not.Contain("const int RequestedJoystickPoints"));
		}

		[Test]
		public void ViewportGeometryExposesOnlyTheRectangleObstacleContract()
		{
			var createMethods = typeof(IosViewportControlsLayout)
				.GetMethods(BindingFlags.Public | BindingFlags.Static)
				.Where(method => method.Name == nameof(IosViewportControlsLayout.Create) &&
					method.GetParameters().Length == 3)
				.ToArray();

			Assert.That(createMethods.Select(method => method.GetParameters()[2].ParameterType),
				Is.EqualTo(new[] { typeof(Rectangle) }));
		}

		[TestCaseSource(nameof(ResponsiveControlCases))]
		public void ResponsiveControlsUseApprovedQuarterRingGeometry(
			int device, int requestedSize, CommandBarState state)
		{
			var (renderer, native, insets) = Devices[device];
			var snapshot = new IosScreenSnapshot(
				renderer, native, insets);
			var safe = snapshot.SafeBounds;
			var margin = snapshot.LogicalPoints(12);
			var minimumGap = snapshot.LogicalPoints(8);
			var obstacle = CommandBarObstacle(snapshot, state);
			var expandedObstacle = Expand(obstacle, minimumGap);
			var baseline = IosViewportControlsLayout.Create(snapshot, requestedSize, Rectangle.Empty);
			var baselineEntities = new[] { baseline.JoystickBounds }.Concat(baseline.ActionBounds).ToArray();
			var expectedShift = baselineEntities
				.Where(entity => entity.IntersectsWith(expandedObstacle))
				.Select(entity => entity.Bottom - expandedObstacle.Top)
				.DefaultIfEmpty(0)
				.Max();
			var layout = IosViewportControlsLayout.Create(snapshot, requestedSize, obstacle);
			var center = new int2(
				layout.JoystickBounds.X + layout.JoystickBounds.Width / 2,
				layout.JoystickBounds.Y + layout.JoystickBounds.Height / 2);
			var expectedAngles = new[] { -90.0, -45.0, 0.0 };
			var insetSafe = Inset(safe, margin);
			var entities = new[] { layout.JoystickBounds }.Concat(layout.ActionBounds).ToArray();

			Assert.Multiple(() =>
			{
				if (state != CommandBarState.Hidden)
					Assert.That(obstacle.Left,
						Is.EqualTo(safe.Left + (safe.Width - obstacle.Width) / 2),
						$"The {state} command-bar obstacle must be centered in SafeBounds.");
				Assert.That(layout.NormalizedJoystickPoints, Is.EqualTo(requestedSize));
				Assert.That(layout.ActionBounds, Has.Count.EqualTo(3));
				Assert.That(layout.ActionBounds, Is.Not.InstanceOf<Rectangle[]>());
				Assert.That(layout.JoystickBounds.Size,
					Is.EqualTo(new Size(snapshot.LogicalPoints(requestedSize), snapshot.LogicalPoints(requestedSize))));
				Assert.That(layout.ThumbDiameter, Is.EqualTo(snapshot.LogicalPoints(requestedSize * 0.42)));
				Assert.That(layout.MovementRadius, Is.EqualTo(snapshot.LogicalPoints(requestedSize * 0.27)));
				Assert.That(layout.RingRadius,
					Is.EqualTo(snapshot.LogicalPoints(requestedSize / 2.0 + 48)));
				Assert.That(layout.ButtonDiameter, Is.EqualTo(snapshot.LogicalPoints(56)));
				Assert.That(layout.Margin, Is.EqualTo(margin));
				Assert.That(layout.ActionsBounds, Is.EqualTo(layout.ActionBounds.Aggregate(Rectangle.Union)));
				Assert.That(layout.JoystickBounds, Is.EqualTo(TranslateUp(baseline.JoystickBounds, expectedShift)));
				for (var i = 0; i < layout.ActionBounds.Count; i++)
					Assert.That(layout.ActionBounds[i],
						Is.EqualTo(TranslateUp(baseline.ActionBounds[i], expectedShift)), $"action {i}");
				Assert.That(entities, Has.All.Matches<Rectangle>(insetSafe.Contains),
					$"Every hit rectangle must stay within the 12pt-inset Safe Area for {state}.");
				Assert.That(entities, Has.All.Matches<Rectangle>(entity => !entity.IntersectsWith(expandedObstacle)),
					$"Every hit rectangle must clear the {state} command bar by 8pt.");
			});

			if (state == CommandBarState.Hidden)
			{
				Assert.Multiple(() =>
				{
					Assert.That(layout.JoystickBounds.Left, Is.EqualTo(safe.Left + margin));
					Assert.That(layout.JoystickBounds.Bottom, Is.EqualTo(safe.Bottom - margin));
				});
			}

			var radii = new double[layout.ActionBounds.Count];
			for (var i = 0; i < layout.ActionBounds.Count; i++)
			{
				var action = layout.ActionBounds[i];
				var actionCenter = new int2(action.X + action.Width / 2, action.Y + action.Height / 2);
				var dx = actionCenter.X - center.X;
				var dy = actionCenter.Y - center.Y;
				radii[i] = Math.Sqrt((double)dx * dx + (double)dy * dy);

				Assert.Multiple(() =>
				{
					Assert.That(action.Size, Is.EqualTo(new Size(layout.ButtonDiameter, layout.ButtonDiameter)));
					Assert.That(Math.Atan2(dy, dx) * 180 / Math.PI,
						Is.EqualTo(expectedAngles[i]).Within(1.0), $"action {i} at {requestedSize}pt");
					Assert.That(radii[i], Is.EqualTo(layout.RingRadius).Within(1.0));
					Assert.That(CircleToRectangleGap(layout.JoystickBounds, action),
						Is.GreaterThanOrEqualTo(minimumGap),
						$"action {i} is too close to the circular joystick interaction area " +
						$"({CircleToRectangleGap(layout.JoystickBounds, action) / snapshot.LogicalPerPoint:F3}pt)");
				});

				for (var j = 0; j < i; j++)
				{
					var other = layout.ActionBounds[j];
					var otherCenter = new int2(other.X + other.Width / 2, other.Y + other.Height / 2);
					var centerDistance = Math.Sqrt(
						(double)(actionCenter.X - otherCenter.X) * (actionCenter.X - otherCenter.X) +
						(double)(actionCenter.Y - otherCenter.Y) * (actionCenter.Y - otherCenter.Y));
					Assert.That(centerDistance - layout.ButtonDiameter,
						Is.GreaterThanOrEqualTo(minimumGap), $"actions {j} and {i} are too close");
				}
			}

			Assert.That(radii.Max() - radii.Min(), Is.LessThanOrEqualTo(1.0));
		}

		[Test]
		public void AsymmetricSafeAreaSwapMovesTheAnchorWithTheLeftInset()
		{
			var leftNotch = new IosScreenSnapshot(
				new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(55, 0, 39, 21));
			var rightNotch = new IosScreenSnapshot(
				new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(39, 0, 55, 21));
			var leftLayout = IosViewportControlsLayout.Create(leftNotch, 128, Rectangle.Empty);
			var rightLayout = IosViewportControlsLayout.Create(rightNotch, 128, Rectangle.Empty);

			Assert.Multiple(() =>
			{
				Assert.That(leftLayout.JoystickBounds.Left,
					Is.EqualTo(leftNotch.SafeBounds.Left + leftNotch.LogicalPoints(12)));
				Assert.That(rightLayout.JoystickBounds.Left,
					Is.EqualTo(rightNotch.SafeBounds.Left + rightNotch.LogicalPoints(12)));
				Assert.That(leftLayout.JoystickBounds.Left, Is.GreaterThan(rightLayout.JoystickBounds.Left));
			});
		}

		[Test]
		public void EmptyQuarterRingSpaceDoesNotMoveTheControls()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(47, 0, 47, 21));
			var withoutObstacle = IosViewportControlsLayout.Create(snapshot, 128, Rectangle.Empty);
			var entities = new[] { withoutObstacle.JoystickBounds }.Concat(withoutObstacle.ActionBounds).ToArray();
			var overall = entities.Aggregate(Rectangle.Union);
			var clearance = snapshot.LogicalPoints(8);
			var emptySpaceObstacle = FindQuarterRingGap(overall, entities, clearance);

			Assert.Multiple(() =>
			{
				Assert.That(overall.IntersectsWith(emptySpaceObstacle), Is.True);
				Assert.That(entities, Has.All.Matches<Rectangle>(entity => !entity.IntersectsWith(emptySpaceObstacle)));
				Assert.That(entities, Has.All.Matches<Rectangle>(
					entity => !entity.IntersectsWith(Expand(emptySpaceObstacle, clearance))));
			});

			var unchanged = IosViewportControlsLayout.Create(snapshot, 128, emptySpaceObstacle);
			Assert.Multiple(() =>
			{
				Assert.That(unchanged.JoystickBounds, Is.EqualTo(withoutObstacle.JoystickBounds));
				Assert.That(unchanged.ActionBounds, Is.EqualTo(withoutObstacle.ActionBounds));
			});
		}

		[TestCase(false)]
		[TestCase(true)]
		public void IntersectingEntityUsesTheExactMinimalUpwardShift(bool targetAction)
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(47, 0, 47, 21));
			var withoutObstacle = IosViewportControlsLayout.Create(snapshot, 128, Rectangle.Empty);
			var entities = new[] { withoutObstacle.JoystickBounds }.Concat(withoutObstacle.ActionBounds).ToArray();
			var target = targetAction ? withoutObstacle.ActionBounds[2] : withoutObstacle.JoystickBounds;
			var clearance = snapshot.LogicalPoints(8);
			var obstacle = new Rectangle(
				target.Left + target.Width / 3,
				target.Bottom - Math.Max(1, clearance / 2),
				Math.Max(1, target.Width / 3),
				Math.Max(1, clearance));
			var expandedObstacle = Expand(obstacle, clearance);
			var expectedShift = entities
				.Where(entity => entity.IntersectsWith(expandedObstacle))
				.Max(entity => entity.Bottom - expandedObstacle.Top);

			Assert.That(target.IntersectsWith(expandedObstacle), Is.True);
			var shifted = IosViewportControlsLayout.Create(snapshot, 128, obstacle);
			var shiftedEntities = new[] { shifted.JoystickBounds }.Concat(shifted.ActionBounds).ToArray();
			Assert.Multiple(() =>
			{
				Assert.That(shifted.JoystickBounds.Y,
					Is.EqualTo(withoutObstacle.JoystickBounds.Y - expectedShift));
				for (var i = 0; i < shifted.ActionBounds.Count; i++)
					Assert.That(shifted.ActionBounds[i].Y,
						Is.EqualTo(withoutObstacle.ActionBounds[i].Y - expectedShift), $"action {i}");
				Assert.That(shiftedEntities,
					Has.All.Matches<Rectangle>(entity => !entity.IntersectsWith(expandedObstacle)));
			});
		}

		[Test]
		public void ImpossibleObstacleIsRejectedInsteadOfEscapingTheInsetSafeArea()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1133, 744), new Size(1133, 744), new IosSafeAreaInsets(0, 0, 0, 20));
			var safe = snapshot.SafeBounds;
			var obstacle = Rectangle.FromLTRB(safe.Left, safe.Top, safe.Right, safe.Bottom);

			Assert.That(
				() => IosViewportControlsLayout.Create(snapshot, 144, obstacle),
				Throws.InvalidOperationException);
		}

		[Test]
		public void JoystickInitialHitAreaMatchesTheRenderedInscribedCircle()
		{
			var joystick = Uninitialized<VirtualViewportJoystickWidget>();
			joystick.Bounds = new WidgetBounds(10, 20, 120, 80);
			var squareJoystick = Uninitialized<VirtualViewportJoystickWidget>();
			squareJoystick.Bounds = new WidgetBounds(10, 20, 80, 80);
			var childrenField = typeof(Widget).GetField(nameof(Widget.Children))!;
			childrenField.SetValue(joystick, new List<Widget>());
			childrenField.SetValue(squareJoystick, new List<Widget>());

			Assert.Multiple(() =>
			{
				Assert.That(typeof(VirtualViewportJoystickWidget)
					.GetMethod(nameof(Widget.EventBoundsContains))!.DeclaringType,
					Is.EqualTo(typeof(VirtualViewportJoystickWidget)));
				Assert.That(joystick.EventBoundsContains(new int2(70, 60)), Is.True, "center");
				Assert.That(joystick.EventBoundsContains(new int2(70, 20)), Is.True, "circle boundary");
				Assert.That(joystick.EventBoundsContains(new int2(70, 100)), Is.True, "bottom tangent");
				Assert.That(squareJoystick.EventBoundsContains(new int2(90, 60)), Is.True, "right tangent");
				Assert.That(joystick.EventBoundsContains(new int2(10, 20)), Is.False, "square corner");
				Assert.That(joystick.EventBoundsContains(new int2(111, 60)), Is.False,
					"The radius must use half of the smaller non-square dimension.");
			});
		}

		[Test]
		public void FocusedJoystickKeepsHandlingDragOutsideTheInitialCircleUntilRelease()
		{
			var joystick = Uninitialized<VirtualViewportJoystickWidget>();
			joystick.Bounds = new WidgetBounds(10, 20, 80, 80);
			joystick.IsVisible = () => true;
			typeof(Widget).GetField(nameof(Widget.Children))!.SetValue(joystick, new List<Widget>());
			var state = new VirtualViewportJoystickState(40, 0.15f);
			SetPrivateField(joystick, "state", state);

			var platformOverride = typeof(Platform).GetField(
				"hostPlatformOverride", BindingFlags.Static | BindingFlags.NonPublic)!;
			var previousPlatformOverride = platformOverride.GetValue(null);
			var loaded = typeof(IosFloatingControlsPreferences).GetField(
				"loaded", BindingFlags.Static | BindingFlags.NonPublic)!;
			var visible = typeof(IosFloatingControlsPreferences).GetField(
				"visible", BindingFlags.Static | BindingFlags.NonPublic)!;
			var previousLoaded = loaded.GetValue(null);
			var previousVisible = visible.GetValue(null);
			var previousMouseFocus = Ui.MouseFocusWidget;
			var previousMouseOver = Ui.MouseOverWidget;

			try
			{
				Platform.OverrideHostPlatform(PlatformType.iOS);
				loaded.SetValue(null, true);
				visible.SetValue(null, true);
				Ui.MouseFocusWidget = null;
				Ui.MouseOverWidget = null;

				var down = new MouseInput(MouseInputEvent.Down, MouseButton.Left,
					new int2(50, 60), int2.Zero, Modifiers.None, 0);
				var outsideMove = new MouseInput(MouseInputEvent.Move, MouseButton.Left,
					new int2(180, 180), int2.Zero, Modifiers.None, 0);
				var outsideUp = new MouseInput(MouseInputEvent.Up, MouseButton.Left,
					new int2(180, 180), int2.Zero, Modifiers.None, 0);

				Assert.That(joystick.HandleMouseInputOuter(down), Is.True);
				Assert.Multiple(() =>
				{
					Assert.That(joystick.HasMouseFocus, Is.True);
					Assert.That(state.Active, Is.True);
					Assert.That(joystick.EventBoundsContains(outsideMove.Location), Is.False);
				});

				Assert.That(joystick.HandleMouseInputOuter(outsideMove), Is.True);
				Assert.Multiple(() =>
				{
					Assert.That(joystick.HasMouseFocus, Is.True);
					Assert.That(state.Active, Is.True);
					Assert.That(state.ThumbOffset, Is.Not.EqualTo(int2.Zero));
				});

				Assert.That(joystick.HandleMouseInputOuter(outsideUp), Is.True);
				Assert.Multiple(() =>
				{
					Assert.That(joystick.HasMouseFocus, Is.False);
					Assert.That(state.Active, Is.False);
					Assert.That(state.ThumbOffset, Is.EqualTo(int2.Zero));
				});
			}
			finally
			{
				Ui.MouseFocusWidget = previousMouseFocus;
				Ui.MouseOverWidget = previousMouseOver;
				loaded.SetValue(null, previousLoaded);
				visible.SetValue(null, previousVisible);
				platformOverride.SetValue(null, previousPlatformOverride);
			}
		}

		[Test]
		public void ResizingAnActiveJoystickCancelsInputAndUsesTheNewRadius()
		{
			var state = new VirtualViewportJoystickState(50, 0.15f);
			state.Begin(new int2(100, 100));
			state.Move(new int2(200, 125));
			Assert.That(state.Active, Is.True);

			state.SetRadius(30);
			Assert.Multiple(() =>
			{
				Assert.That(state.Radius, Is.EqualTo(30));
				Assert.That(state.Active, Is.False);
				Assert.That(state.ThumbOffset, Is.EqualTo(int2.Zero));
				Assert.That(state.Direction.X, Is.Zero);
				Assert.That(state.Direction.Y, Is.Zero);
				Assert.That(state.Speed, Is.Zero);
			});

			state.Begin(new int2(100, 100));
			state.Move(new int2(200, 100));
			Assert.That(state.ThumbOffset, Is.EqualTo(new int2(30, 0)));

			state.SetRadius(0);
			Assert.That(state.Radius, Is.EqualTo(1));
			Assert.That(state.Active, Is.False);
		}

		[Test]
		public void ImageStretchingIsOptInAndCopiedByClone()
		{
			var image = new ImageWidget();
			Assert.That(image.StretchToFit, Is.False);

			image.StretchToFit = true;
			var clone = (ImageWidget)image.Clone();
			Assert.That(clone.StretchToFit, Is.True);
		}
	}
}
