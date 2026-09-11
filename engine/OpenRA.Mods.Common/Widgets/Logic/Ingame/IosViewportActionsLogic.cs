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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic.Ingame
{
	public interface IIosViewportControlsSkinTarget
	{
		void ApplyViewportSkin(string skin);
	}

	public static class IosViewportSkinLifecycle
	{
		public static bool Refresh(
			bool isIos, string factionInternalName, bool spectating, IIosViewportControlsSkinTarget target)
		{
			if (target == null)
				throw new ArgumentNullException(nameof(target));

			if (!isIos)
			{
				TouchFactionSkin.ResetActive();
				return false;
			}

			var skin = TouchFactionSkin.ActivateForPlayer(factionInternalName, spectating);
			target.ApplyViewportSkin(skin);
			return true;
		}
	}

	public static class IosViewportSkinApplicator
	{
		public static void Apply(
			string skin,
			int actionCount,
			ITouchFactionSkinTarget joystick,
			Action<int> applyButtonChrome,
			Action<int, string> applyImageCollection,
			Action<int> bindButtonIcon)
		{
			if (actionCount < 0)
				throw new ArgumentOutOfRangeException(nameof(actionCount));
			if (joystick == null)
				throw new ArgumentNullException(nameof(joystick));
			if (applyButtonChrome == null)
				throw new ArgumentNullException(nameof(applyButtonChrome));
			if (applyImageCollection == null)
				throw new ArgumentNullException(nameof(applyImageCollection));
			if (bindButtonIcon == null)
				throw new ArgumentNullException(nameof(bindButtonIcon));

			joystick.ApplySkin(skin);
			var imageCollection = TouchFactionSkin.ActionCollection(skin);
			for (var i = 0; i < actionCount; i++)
			{
				applyButtonChrome(i);
				applyImageCollection(i, imageCollection);
				bindButtonIcon(i);
			}
		}
	}

	public sealed class IosViewportActionsLogic : ChromeLogic, IIosViewportControlsSkinTarget
	{
		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly Widget widget;
		readonly VirtualViewportJoystickWidget joystick;
		readonly ITouchFactionSkinTarget joystickSkinTarget;
		readonly ButtonWidget[] actionButtons;
		readonly ImageWidget[] actionIcons;
		readonly LabelWidget[] actionLabels;
		Size lastResolution;
		Size lastNativePointSize;
		Rectangle lastSafeBounds;
		int lastRequestedJoystickPoints;
		Rectangle lastBottomObstacleBounds;
		string lastSkin;
		bool layoutInitialized;

		[ObjectCreator.UseCtor]
		public IosViewportActionsLogic(Widget widget, World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			this.widget = widget;
			joystick = widget.Parent.Get<VirtualViewportJoystickWidget>("IOS_VIEWPORT_JOYSTICK");
			joystickSkinTarget = joystick;
			widget.IsVisible = () => Platform.IsIOS && IosFloatingControlsPreferences.Visible;
			actionButtons = new[]
			{
				widget.Get<ButtonWidget>("DEPLOY"),
				widget.Get<ButtonWidget>("SELECT_TYPE"),
				widget.Get<ButtonWidget>("FORCE_ATTACK")
			};
			actionIcons = actionButtons.Select(button => button.Get<ImageWidget>("ICON")).ToArray();
			actionLabels = actionButtons.Select(button => button.Get<LabelWidget>("LABEL")).ToArray();

			var selectType = widget.Get<ButtonWidget>("SELECT_TYPE");
			WidgetUtils.BindButtonIcon(selectType);
			selectType.IsDisabled = () => world.IsGameOver || world.Selection.Actors.Count == 0;
			selectType.OnClick = SelectUnitsByType;

			var isIos = RefreshCurrentSkin();
			if (isIos)
				ApplyLayout(Game.Settings.Game.IosVirtualJoystickSize);
		}

		public override void Tick()
		{
			var isIos = RefreshCurrentSkin();
			if (!isIos)
				return;

			var storedJoystickPoints = Game.Settings.Game.IosVirtualJoystickSize;
			var refreshForJoystickSize = IosViewportControlsLayout.ShouldRefreshForJoystickSize(
				isIos, layoutInitialized, lastRequestedJoystickPoints, storedJoystickPoints);

			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			var requestedJoystickPoints = IosViewportControlsLayout.NormalizeJoystickPoints(
				storedJoystickPoints);
			var bottomObstacleBounds = BottomObstacleBounds();
			if (refreshForJoystickSize || resolution != lastResolution ||
				snapshot.NativePointSize != lastNativePointSize || snapshot.SafeBounds != lastSafeBounds ||
				bottomObstacleBounds != lastBottomObstacleBounds)
				ApplyLayout(requestedJoystickPoints);
		}

		bool RefreshCurrentSkin()
		{
			var localPlayer = world.LocalPlayer;
			var spectating = localPlayer == null || localPlayer.Spectating;
			return IosViewportSkinLifecycle.Refresh(
				Platform.IsIOS,
				spectating ? null : localPlayer.Faction.InternalName,
				spectating,
				this);
		}

		public void ApplyViewportSkin(string skin)
		{
			if (skin == lastSkin)
				return;

			IosViewportSkinApplicator.Apply(
				skin,
				actionButtons.Length,
				joystickSkinTarget,
				index =>
				{
					var button = actionButtons[index];
					button.Background = string.Empty;
					button.VisualHeight = 0;
				},
				(index, imageCollection) => actionIcons[index].ImageCollection = imageCollection,
				index =>
				{
					var button = actionButtons[index];
					WidgetUtils.BindButtonIcon(button);
				});

			lastSkin = skin;
		}

		static Rectangle BottomObstacleBounds()
		{
			var commandBar = Ui.Root.GetOrNull("COMMAND_BAR_BACKGROUND");
			return commandBar != null && commandBar.IsVisible()
				? commandBar.RenderBounds
				: Rectangle.Empty;
		}

		void ApplyLayout(int requestedJoystickPoints)
		{
			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			requestedJoystickPoints = IosViewportControlsLayout.NormalizeJoystickPoints(requestedJoystickPoints);
			var bottomObstacleBounds = BottomObstacleBounds();
			var layout = IosViewportControlsLayout.Create(snapshot, requestedJoystickPoints, bottomObstacleBounds);
			var labelLayout = IosViewportActionLabelLayout.Create(snapshot, layout.ButtonDiameter);
			joystick.ApplyLayout(layout.JoystickBounds, layout.ThumbDiameter, layout.MovementRadius);

			var parentOrigin = widget.Parent == null ? int2.Zero : widget.Parent.ChildOrigin;
			widget.Bounds = new WidgetBounds(
				layout.ActionsBounds.X - parentOrigin.X,
				layout.ActionsBounds.Y - parentOrigin.Y,
				layout.ActionsBounds.Width,
				layout.ActionsBounds.Height);
			for (var i = 0; i < actionButtons.Length; i++)
			{
				var bounds = layout.ActionBounds[i];
				actionButtons[i].Bounds = new WidgetBounds(
					bounds.X - layout.ActionsBounds.X,
					bounds.Y - layout.ActionsBounds.Y,
					bounds.Width,
					bounds.Height);
				actionIcons[i].Bounds = new WidgetBounds(0, 0, bounds.Width, bounds.Height);
				labelLayout.ApplyTo(actionLabels[i]);
				actionLabels[i].IsVisible = () => false;
			}

			lastResolution = resolution;
			lastNativePointSize = snapshot.NativePointSize;
			lastSafeBounds = snapshot.SafeBounds;
			lastRequestedJoystickPoints = requestedJoystickPoints;
			lastBottomObstacleBounds = bottomObstacleBounds;
			layoutInitialized = true;
		}

		void SelectUnitsByType()
		{
			if (world.IsGameOver || world.Selection.Actors.Count == 0)
				return;

			var eligiblePlayers = SelectionUtils.GetPlayersToIncludeInSelection(world);
			var selectedClasses = world.Selection.Actors
				.Where(a => !a.IsDead && eligiblePlayers.Contains(a.Owner))
				.Select(a => a.Trait<ISelectable>().Class)
				.ToHashSet();

			if (selectedClasses.Count == 0)
				return;

			var actors = SelectionUtils.SelectActorsOnScreen(world, worldRenderer, selectedClasses, eligiblePlayers).ToList();
			if (actors.Count <= world.Selection.Actors.Count)
				actors = SelectionUtils.SelectActorsInWorld(world, selectedClasses, eligiblePlayers).ToList();

			world.Selection.Combine(world, actors, true, false);
		}
	}
}
