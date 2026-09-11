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
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic.Ingame;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosViewportActionLayoutTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "engine", "OpenRA.Game"))))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		static (int Width, int Height) PngSize(string path)
		{
			using var stream = File.OpenRead(path);
			using var reader = new BinaryReader(stream);
			reader.ReadBytes(16);
			var width = reader.ReadBytes(4);
			var height = reader.ReadBytes(4);
			return (
				(width[0] << 24) | (width[1] << 16) | (width[2] << 8) | width[3],
				(height[0] << 24) | (height[1] << 16) | (height[2] << 8) | height[3]);
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
		public void IngameLayoutDoesNotExposeManualDiagnosticsButton()
		{
			var root = RepositoryRoot();
			var layout = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame.yaml"));

			StringAssert.DoesNotContain("Button@SAVE_DIAGNOSTICS", layout);
			StringAssert.DoesNotContain("IosDiagnosticsCenterLogic", layout);
		}

		[Test]
		public void IosViewportControlsExposeExactlyThreeTouchActions()
		{
			var root = RepositoryRoot();
			var yaml = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame.yaml"));
			var manifest = File.ReadAllText(Path.Combine(root, "mods", "ra2", "mod.yaml"));
			StringAssert.Contains("ra2|chrome/ingame.yaml", manifest,
				"The RA2 touch layout must replace the common desktop ingame layout.");
			StringAssert.Contains("Container@IOS_VIEWPORT_ACTIONS", yaml);

			var start = yaml.IndexOf("Container@IOS_VIEWPORT_ACTIONS:", StringComparison.Ordinal);
			var end = yaml.IndexOf("\n\t\t\t\tStrategicProgress@", start, StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0));
			Assert.That(end, Is.GreaterThan(start));
			var ids = yaml.Substring(start, end - start).Split('\n')
				.Select(line => line.Trim())
				.Where(line => line.StartsWith("Button@", StringComparison.Ordinal))
				.Select(line => line["Button@".Length..line.IndexOf(':')])
				.ToArray();
			Assert.That(ids, Is.EqualTo(new[] { "DEPLOY", "SELECT_TYPE", "FORCE_ATTACK" }));
		}

		[Test]
		public void IosViewportActionsUseRuntimeGeometryAndScaledDedicatedIcons()
		{
			var root = RepositoryRoot();
			var layout = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame.yaml"));
			var chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));
			var actionLogic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Ingame", "IosViewportActionsLogic.cs"));
			var joystick = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "VirtualViewportJoystickWidget.cs"));
			var image = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "ImageWidget.cs"));

			StringAssert.Contains("ios-joystick-command-icons:", chrome);
			foreach (var (button, icon) in new[]
			{
				("DEPLOY", "ios-deploy"),
				("SELECT_TYPE", "ios-select-type"),
				("FORCE_ATTACK", "ios-force-attack"),
			})
			{
				StringAssert.Contains(icon + ":", chrome);
				StringAssert.Contains("ImageName: " + icon, layout);

				var start = layout.IndexOf("Button@" + button + ":", System.StringComparison.Ordinal);
				Assert.That(start, Is.GreaterThanOrEqualTo(0));
				var end = layout.IndexOf("\n\t\t\t\t\t\tButton@", start + 1, System.StringComparison.Ordinal);
				if (end < 0)
					end = layout.IndexOf("\n\t\t\t\tStrategicProgress@", start, System.StringComparison.Ordinal);

				var buttonBlock = layout.Substring(start, end - start);
				StringAssert.DoesNotContain("\n\t\t\t\t\t\t\tX:", buttonBlock);
				StringAssert.DoesNotContain("\n\t\t\t\t\t\t\tY:", buttonBlock);
				StringAssert.Contains("StretchToFit: true", buttonBlock);
			}

			var joystickStart = layout.IndexOf("VirtualViewportJoystick@IOS_VIEWPORT_JOYSTICK:", System.StringComparison.Ordinal);
			var actionsStart = layout.IndexOf("Container@IOS_VIEWPORT_ACTIONS:", joystickStart, System.StringComparison.Ordinal);
			var joystickBlock = layout.Substring(joystickStart, actionsStart - joystickStart);
			StringAssert.Contains("Width: 128", joystickBlock);
			StringAssert.Contains("Height: 128", joystickBlock);

			StringAssert.Contains("public override void Tick()", actionLogic);
			StringAssert.Contains("IosScreenMetrics.SnapshotFor", actionLogic);
			StringAssert.Contains("COMMAND_BAR_BACKGROUND", actionLogic);
			StringAssert.Contains("joystick.ApplyLayout", actionLogic);
			StringAssert.Contains("IsVisible = ControlsVisible;", joystick);
			StringAssert.Contains("if (!visible)\n\t\t\t\tCancelActiveInput();", joystick);
			StringAssert.Contains("CancelActiveInput", joystick);
			StringAssert.Contains("state.SetRadius", joystick);
			StringAssert.DoesNotContain("center.X - 24", joystick);
			StringAssert.Contains("StretchToFit", image);
			StringAssert.Contains("RenderBounds.Size", image);
		}

		[Test]
		public void IosViewportRuntimeUsesExactlyThreeActionsAndTheFullCommandBarRectangle()
		{
			var logicType = typeof(IosViewportActionsLogic);
			var bottomObstacle = logicType.GetMethod(
				"BottomObstacleBounds", BindingFlags.Static | BindingFlags.NonPublic);
			var applyLayout = logicType.GetMethod("ApplyLayout", BindingFlags.Instance | BindingFlags.NonPublic)!;
			var rectangleCreate = typeof(IosViewportControlsLayout).GetMethod(
				nameof(IosViewportControlsLayout.Create),
				BindingFlags.Static | BindingFlags.Public,
				null,
				new[] { typeof(IosScreenSnapshot), typeof(int), typeof(Rectangle) },
				null)!;
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Ingame",
				"IosViewportActionsLogic.cs"));

			Assert.Multiple(() =>
			{
				Assert.That(bottomObstacle, Is.Not.Null);
				Assert.That(bottomObstacle?.ReturnType, Is.EqualTo(typeof(Rectangle)));
				Assert.That(logicType.GetField("lastBottomObstacleBounds", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType,
					Is.EqualTo(typeof(Rectangle)));
				Assert.That(Calls(applyLayout, rectangleCreate), Is.True,
					"The live layout must pass the real command-bar rectangle into the pure geometry policy.");
				Assert.That(source, Does.Contain("widget.Get<ButtonWidget>(\"DEPLOY\")"));
				Assert.That(source, Does.Contain("widget.Get<ButtonWidget>(\"SELECT_TYPE\")"));
				Assert.That(source, Does.Contain("widget.Get<ButtonWidget>(\"FORCE_ATTACK\")"));
				Assert.That(source, Does.Not.Contain("widget.Get<ButtonWidget>(\"STOP\")"));
				Assert.That(source, Does.Not.Contain("widget.Get<ButtonWidget>(\"RETURN_BASE\")"));
				Assert.That(source, Does.Not.Contain("BottomObstacleTop"));
				Assert.That(source, Does.Not.Contain("lastBottomObstacleTop"));
				Assert.That(source, Does.Not.Contain("HasOwnedBuilding"));
				Assert.That(source, Does.Not.Contain("ReturnToBase"));
			});
		}

		[Test]
		public void IosViewportActionsCacheLabelsAndConsumeThePureLabelLayoutPolicy()
		{
			var logicType = typeof(IosViewportActionsLogic);
			var labels = logicType.GetField("actionLabels", BindingFlags.Instance | BindingFlags.NonPublic);
			var applyLayout = logicType.GetMethod("ApplyLayout", BindingFlags.Instance | BindingFlags.NonPublic)!;
			var createLayout = typeof(IosViewportActionLabelLayout).GetMethod(
				nameof(IosViewportActionLabelLayout.Create), BindingFlags.Static | BindingFlags.Public)!;
			var applyLabel = typeof(IosViewportActionLabelLayout).GetMethod(
				"ApplyTo", BindingFlags.Instance | BindingFlags.Public);

			Assert.Multiple(() =>
			{
				Assert.That(labels, Is.Not.Null, "The three live label widgets must be cached once at construction.");
				Assert.That(labels?.FieldType, Is.EqualTo(typeof(LabelWidget[])));
				Assert.That(Calls(applyLayout, createLayout), Is.True,
					"ApplyLayout must consume the tested pure label layout policy on every geometry refresh.");
				Assert.That(applyLabel, Is.Not.Null,
					"The label policy needs a directly behavior-tested runtime application seam.");
				Assert.That(applyLabel != null && Calls(applyLayout, applyLabel), Is.True,
					"ApplyLayout must apply the tested bounds and font behavior to every live label.");
				Assert.That(logicType.GetField("actionLabelPlates", BindingFlags.Instance | BindingFlags.NonPublic),
					Is.Null, "The raster atlas is the only label plate; ColorBlockWidget can intercept input.");
			});
		}

		[Test]
		public void IosJoystickUsesLiveFactionArtworkWithoutDroppingTheActiveThumb()
		{
			var root = RepositoryRoot();
			var chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));
			var joystick = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "VirtualViewportJoystickWidget.cs"));

			StringAssert.Contains("Image: commandbar-icons-remastered.png", chrome);
			foreach (var skin in new[] { "allies", "soviets", "yuri" })
				StringAssert.Contains("ios-touch-joystick-" + skin + ":", chrome);
			StringAssert.Contains("public void ApplySkin(string skin)", joystick);
			StringAssert.Contains("string imageCollection = \"ios-touch-joystick-allies\";", joystick,
				"The constructor-time fallback must already use the neutral Allied touch atlas.");
			StringAssert.Contains("TouchFactionSkin.JoystickCollection(skin)", joystick);
			StringAssert.Contains("ChromeProvider.GetImage(imageCollection, \"base\")", joystick);
			StringAssert.Contains("ChromeProvider.GetImage(imageCollection, \"thumb\")", joystick);
			StringAssert.Contains("ChromeProvider.GetImage(imageCollection, \"thumb-active\")", joystick);
			StringAssert.Contains("state.Active ? activeThumbSprite : thumbSprite", joystick);

			Assert.That(File.Exists(Path.Combine(root, "mods", "ra2", "uibits", "commandbar-icons-remastered.png")), Is.True);
			foreach (var skin in new[] { "allies", "soviets", "yuri" })
				Assert.That(File.Exists(Path.Combine(root, "mods", "ra2", "uibits", $"ios-touch-joystick-{skin}.png")), Is.True);
		}

		[Test]
		public void RemasteredCommandBarAtlasUsesPowerOfTwoTextureDimensions()
		{
			var root = Path.Combine(RepositoryRoot(), "mods", "ra2", "uibits");
			Assert.That(PngSize(Path.Combine(root, "commandbar-icons-remastered.png")), Is.EqualTo((1024, 128)));
			Assert.That(PngSize(Path.Combine(root, "ios-commandbar-icons-v2.png")), Is.EqualTo((256, 128)));
			Assert.That(PngSize(Path.Combine(root, "ios-viewport-joystick.png")), Is.EqualTo((256, 128)));
			Assert.That(PngSize(Path.Combine(root, "ios-joystick-command-icons.png")), Is.EqualTo((512, 512)));
		}

		[Test]
		public void IosViewportActionsExposeDistinctPressedAndPersistentHighlightedArtwork()
		{
			var root = RepositoryRoot();
			var chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));
			var logic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Ingame", "IosViewportActionsLogic.cs"));
			var commands = new[] { "ios-stop", "ios-deploy", "ios-select-type", "ios-force-attack", "ios-return-base" };

			foreach (var command in commands)
			{
				StringAssert.Contains(command + "-pressed:", chrome,
					$"{command} must draw a visibly depressed touch state.");
				StringAssert.Contains(command + "-hover:", chrome);
			}

			StringAssert.Contains("ios-joystick-command-icons-highlighted:", chrome);
			StringAssert.Contains("ios-force-attack: 168, 256, 56, 56", chrome,
				"Force attack must use a dedicated persistent active sprite.");
			StringAssert.Contains("WidgetUtils.BindButtonIcon(selectType);", logic);
			StringAssert.DoesNotContain("var returnBase", logic);
		}

		[Test]
		public void IosViewportActionsUseOneFactionAtlasShellWithoutLegacyButtonOffsets()
		{
			var root = RepositoryRoot();
			var layout = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame.yaml"));
			var logic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Ingame", "IosViewportActionsLogic.cs"));

			foreach (var button in new[] { "DEPLOY", "SELECT_TYPE", "FORCE_ATTACK" })
			{
				StringAssert.Contains($"Button@{button}:", layout);
				var start = layout.IndexOf($"Button@{button}:", System.StringComparison.Ordinal);
				var end = layout.IndexOf("\n\t\t\t\t\t\tButton@", start + 1, System.StringComparison.Ordinal);
				if (end < 0)
					end = layout.IndexOf("\n\t\t\t\tStrategicProgress@", start, System.StringComparison.Ordinal);

				StringAssert.Contains("ImageCollection: ios-touch-actions-allies", layout.Substring(start, end - start),
					$"{button} must have a valid Allied touch fallback before runtime faction binding.");
				StringAssert.Contains("ImageName: ios-" + button.ToLowerInvariant().Replace('_', '-'), layout);
			}

			StringAssert.Contains("TouchFactionSkin.ActionCollection(skin)", logic);
			StringAssert.Contains("button.Background = string.Empty;", logic);
			StringAssert.Contains("button.VisualHeight = 0;", logic);
			StringAssert.Contains("WidgetUtils.BindButtonIcon(button);", logic);
		}

		[Test]
		public void IosTouchInputPublishesImmediatePressFeedbackWithoutIssuingAnEarlyClick()
		{
			var root = RepositoryRoot();
			var input = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Platforms.Default", "Sdl2Input.cs"));
			var button = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "ButtonWidget.cs"));

			StringAssert.Contains("TouchPressFeedback.Begin(pos, Ui.MouseOverWidget);", input);
			StringAssert.Contains("TouchPressFeedback.Move(pos);", input);
			StringAssert.Contains("TouchPressFeedback.End();", input);
			StringAssert.Contains("TouchPressFeedback.IsActiveFor(this)", button,
				"Pressed feedback must remain owned by the button hit at touch-down.");
			StringAssert.DoesNotContain("DispatchTouch(touchGestures.Begin(pos", input,
				"Visual feedback must not emit a premature LeftDown that can misfire when another finger joins.");
			StringAssert.Contains("inputHandler is not NullInputHandler", input,
				"Touches that begin while the game ignores input must never replay into the next UI.");
			StringAssert.Contains("if (hadTouchState)\n\t\t\t\tinputHandler.OnMouseInput(new MouseInput(MouseInputEvent.Cancel", input,
				"Reset must release viewport focus even when a null input handler drops the synthetic pan-up.");
		}

		[Test]
		public void ForceAttackModeHighlightsUntilTheFirstWorldTargetOrCancellation()
		{
			var root = RepositoryRoot();
			var commandBar = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Ingame", "CommandBarLogic.cs"));
			var generator = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Orders", "ForceModifiersOrderGenerator.cs"));

			StringAssert.Contains("IsForceModifiersActive(Modifiers.Ctrl)", commandBar);
			StringAssert.Contains("new ForceModifiersOrderGenerator(Modifiers.Ctrl, true)", commandBar);
			StringAssert.Contains("ShouldCancelAfterOrders(Modifiers, cancelOnFirstUse, orders)", generator,
				"The active state must clear only after the target click yields a real force-attack order.");
			StringAssert.Contains("public override bool InputOverridesSelection", generator);
			StringAssert.Contains("mi.Modifiers |= Modifiers;", generator,
				"Force attack must override classic selection when the target is an actor.");
			StringAssert.Contains("if (Modifiers == Modifiers.Ctrl)\n\t\t\t\treturn true;", generator,
				"A one-shot force-attack target click must reach the generator instead of selecting the actor.");
			StringAssert.Contains("forceAttackTargeting", commandBar + File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "WorldInteractionControllerWidget.cs")),
				"Double-click and drag-selection branches must preserve or consume the active targeting mode intentionally.");
			StringAssert.Contains("forceAttackTargeting && mi.Button == Game.Settings.Game.MouseButtonPreference.Cancel &&\n\t\t\t\t\t!IsValidDragbox", File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "WorldInteractionControllerWidget.cs")),
				"Modern mouse mode must cancel force attack with a left click without breaking drag selection.");
		}

		[Test]
		public void CompactTouchCommandsUseTransparentGlyphsWithoutEmbeddedSquareFrames()
		{
			var root = RepositoryRoot();
			var chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));
			var layout = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame-player.yaml"));

			StringAssert.Contains("ios-commandbar-glyphs:", chrome);
			StringAssert.Contains("\tImage: ios-commandbar-glyphs-unified.png", chrome);
			StringAssert.Contains("ios-commandbar-glyphs-highlighted:", chrome,
				"Every stateful iOS command glyph collection must provide the highlighted collection requested by AddFactionSuffixLogic.");
			StringAssert.Contains("\tInherits: ios-commandbar-glyphs", chrome,
				"Highlighted iOS command glyphs must inherit the complete highlighted region map.");
			foreach (var icon in new[]
			{
				"group-1", "group-2", "group-3", "group-4", "group-5", "production-x5",
				"attack-move", "scatter", "queue-orders", "stop", "cycle-base"
			})
			{
				StringAssert.Contains(icon + ":", chrome);
				var iconName = "ImageName: " + icon;
				var iconAt = layout.IndexOf(iconName, System.StringComparison.Ordinal);
				Assert.That(iconAt, Is.GreaterThanOrEqualTo(0));
				var collectionAt = layout.LastIndexOf("ImageCollection:", iconAt, System.StringComparison.Ordinal);
				Assert.That(layout.Substring(collectionAt, iconAt - collectionAt), Does.Contain("ios-commandbar-glyphs"));
				Assert.That(layout.Substring(collectionAt, iconAt - collectionAt), Does.Not.Contain("ios-commandbar-icons-v2"));
			}

			Assert.That(File.Exists(Path.Combine(root, "mods", "ra2", "uibits", "ios-commandbar-glyphs-unified.png")),
				Is.True, "The deterministic unified glyph atlas must ship with the mod.");
		}

		[Test]
		public void IosTouchV3ProvidesFactionSpecificChromeAndEveryCommandGlyph()
		{
			var root = RepositoryRoot();
			var chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));
			var widget = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Widgets", "CustomCommandBarWidget.cs"));
			var actionLogic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets",
				"Logic", "Ingame", "IosViewportActionsLogic.cs"));
			var glyphs = new[]
			{
				"group-1", "group-2", "group-3", "group-4", "group-5", "production-x5",
				"group-6", "group-7", "group-8", "group-9", "group-0", "select-all", "select-by-type",
				"cycle-base", "to-selection", "to-last-event", "cycle-harvesters", "remove-from-group",
				"attack-move", "force-move", "force-attack", "guard", "deploy", "scatter", "stop",
				"queue-orders", "attack-anything", "defend", "return-fire", "hold-fire", "sell", "repair", "beacon"
			};

			foreach (var faction in new[] { "allies", "soviets", "yuri" })
			{
				StringAssert.Contains($"ios-commandbar-glyphs-{faction}:", chrome);
				StringAssert.Contains($"Image: ios-commandbar-glyphs-{faction}-v3.png", chrome);
				foreach (var glyph in glyphs)
					StringAssert.Contains($"{glyph}:", chrome);
			}

			StringAssert.Contains("TouchFactionSkin.CommandGlyphCollection(skin)", widget);
			StringAssert.Contains("actionLabels[i].IsVisible = () => false;", actionLogic,
				"The regenerated floating action icons must communicate their actions without captions below them.");
		}

		[Test]
		public void PowerMeterBarsAreParentedToTheSidebarMeter()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "OpenRA.Mods.RA2", "Widgets", "PowerMeterWidget.cs"));
			StringAssert.Contains("AddChild(newPower);", source);
			StringAssert.DoesNotContain("Children.Add(newPower);", source);
		}

		[Test]
		public void GlesSpriteTextureCoordinatesUseHighPrecisionInBothShaderStages()
		{
			var root = RepositoryRoot();
			var vertex = File.ReadAllText(Path.Combine(root, "engine", "glsl", "combined.vert"));
			var fragment = File.ReadAllText(Path.Combine(root, "engine", "glsl", "combined.frag"));

			StringAssert.Contains("out highp vec4 vTexCoord", vertex);
			StringAssert.Contains("precision highp float", fragment);
			StringAssert.Contains("in highp vec4 vTexCoord", fragment);
		}

		[Test]
		public void IosAudioInitializesSdlAndFallsBackWithoutBlockingStartup()
		{
			var root = RepositoryRoot();
			var engine = File.ReadAllText(Path.Combine(root,
				"ios", "OpenRA.Platforms.iOS", "Sdl2SoundEngine.cs"));
			var platform = File.ReadAllText(Path.Combine(root,
				"ios", "OpenRA.Platforms.iOS", "IosPlatform.cs"));

			StringAssert.Contains("SDL.SDL_InitSubSystem(SDL.SDL_INIT_AUDIO)", engine);
			StringAssert.Contains("catch (Exception e)", platform);
			StringAssert.Contains("return new DummySoundEngine()", platform);
		}

		[Test]
		public void GlesSpriteChannelKindDoesNotDependOnIntegerFragmentVaryings()
		{
			var root = RepositoryRoot();
			var vertex = File.ReadAllText(Path.Combine(root, "engine", "glsl", "combined.vert"));
			var fragment = File.ReadAllText(Path.Combine(root, "engine", "glsl", "combined.frag"));

			StringAssert.Contains("flat out vec2 vChannelKind", vertex);
			StringAssert.Contains("flat in vec2 vChannelKind", fragment);
			StringAssert.DoesNotContain("flat in uint vChannelType", fragment);
			StringAssert.Contains("bool isPaletted = vChannelKind.x > 0.5", fragment);
			StringAssert.Contains("bool isColor = vChannelKind.y > 0.5", fragment);
		}
	}
}
