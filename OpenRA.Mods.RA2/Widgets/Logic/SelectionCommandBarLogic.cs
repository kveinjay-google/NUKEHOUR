#region Copyright & License Information
/*
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
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Orders;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets.Logic
{
	public enum TouchControlGroupAction
	{
		UseDesktopRules,
		Create,
		Recall,
		Ignore
	}

	public static class ControlGroupButtonPolicy
	{
		public static TouchControlGroupAction Resolve(
			bool touchPlatform, Modifiers modifiers, bool groupEmpty, bool hasOwnedSelection)
		{
			if (!touchPlatform || modifiers != Modifiers.None)
				return TouchControlGroupAction.UseDesktopRules;

			if (!groupEmpty)
				return TouchControlGroupAction.Recall;

			return hasOwnedSelection ? TouchControlGroupAction.Create : TouchControlGroupAction.Ignore;
		}
	}

	public static class BaseCyclePolicy
	{
		public static IReadOnlyList<T> Order<T>(
			IEnumerable<T> candidates, Func<T, uint> actorId, Func<T, bool> isPrimary)
		{
			return candidates
				.OrderByDescending(isPrimary)
				.ThenBy(actorId)
				.ToArray();
		}

		public static T Next<T>(IReadOnlyList<T> ordered, Func<T, bool> isSelected)
		{
			if (ordered.Count == 0)
				return default;

			for (var i = 0; i < ordered.Count; i++)
				if (isSelected(ordered[i]))
					return ordered[(i + 1) % ordered.Count];

			return ordered[0];
		}

		public static T Resolve<T>(
			IReadOnlyList<T> orderedBases, IEnumerable<T> fallback, Func<T, bool> isSelected)
			where T : class
		{
			return orderedBases.Count > 0 ? Next(orderedBases, isSelected) : fallback.FirstOrDefault();
		}

		public static bool Execute<T>(T candidate, Action<T> select, Action center, Action click)
			where T : class
		{
			if (candidate == null)
				return false;

			select(candidate);
			center();
			click();
			return true;
		}
	}

	public class SelectionCommandBarLogic : ChromeLogic
	{
		[FluentReference]
		const string NothingSelected = "nothing-selected";

		[FluentReference("units")]
		const string SelectedUnitsAcrossScreen = "selected-units-across-screen";

		[FluentReference("units")]
		const string SelectedUnitsAcrossMap = "selected-units-across-map";

		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly ISelection selection;
		readonly Viewport viewport;

		readonly string clickSound = ChromeMetrics.Get<string>("ClickSound");
		readonly string clickDisabledSound = ChromeMetrics.Get<string>("ClickDisabledSound");

		[ObjectCreator.UseCtor]
		public SelectionCommandBarLogic(Widget widget, World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			selection = world.Selection;
			viewport = worldRenderer.Viewport;

			for (var i = 0; i < world.ControlGroups.Groups.Length; i++)
			{
				var group = i;
				var button = widget.GetOrNull<ButtonWidget>($"GROUP_{group + 1:D2}");
				if (button == null)
					continue;

				BindIcon(button);
				button.IsHighlighted = () => GroupIsHighlighted(group);
				button.OnClick = () => ActivateControlGroup(group, false);
				button.OnDoubleClick = () => ActivateControlGroup(group, true);
			}

			Bind(widget, "SELECT_ALL", SelectAllUnits);
			Bind(widget, "SELECT_BY_TYPE", SelectUnitsByType, () => !selection.Actors.Any(a => a.IsInWorld && !a.IsDead));
			Bind(widget, "CYCLE_BASE", CycleBase);
			Bind(widget, "TO_SELECTION", () => viewport.Center(selection.Actors), () => !selection.Actors.Any());
			Bind(widget, "TO_LAST_EVENT", JumpToLastEvent);
			Bind(widget, "CYCLE_HARVESTERS", CycleHarvesters);
			Bind(widget, "REMOVE_FROM_GROUP", RemoveFromGroup, () => !selection.Actors.Any(a => a.Owner == world.LocalPlayer));

			BindOrderGenerator<SellOrderGenerator>(widget, "SELL");
			BindOrderGenerator<RepairOrderGenerator>(widget, "REPAIR");
			BindOrderGenerator<BeaconOrderGenerator>(widget, "BEACON");
		}

		void BindOrderGenerator<T>(Widget widget, string id)
			where T : IOrderGenerator, new()
		{
			var button = widget.GetOrNull<ButtonWidget>(id);
			if (button == null)
				return;

			BindIcon(button);
			button.OnClick = () => world.ToggleInputMode<T>();
			button.IsHighlighted = () => world.OrderGenerator is T;
		}

		static void Bind(Widget widget, string id, Action action, Func<bool> disabled = null)
		{
			var button = widget.GetOrNull<ButtonWidget>(id);
			if (button == null)
				return;

			BindIcon(button);
			button.OnClick = action;
			if (disabled != null)
				button.IsDisabled = disabled;
		}

		static void BindIcon(ButtonWidget button)
		{
			if (button.GetOrNull<ImageWidget>("ICON") != null)
				WidgetUtils.BindButtonIcon(button);
		}

		bool GroupIsHighlighted(int group)
		{
			var actors = world.ControlGroups.GetActorsInControlGroup(group).ToArray();
			if (actors.Length == 0)
				return false;

			return actors.All(a => selection.Contains(a)) && selection.Actors.Count == actors.Length;
		}

		void ActivateControlGroup(int group, bool jump)
		{
			if (world.IsGameOver || world.LocalPlayer == null)
				return;

			var mods = Game.GetModifierKeys();
			var groupEmpty = !world.ControlGroups.GetActorsInControlGroup(group).Any();
			var hasOwnedSelection = selection.Actors.Any(a =>
				a.IsInWorld && !a.IsDead && a.Owner == world.LocalPlayer);
			var touchAction = ControlGroupButtonPolicy.Resolve(Platform.IsIOS, mods, groupEmpty, hasOwnedSelection);

			if (touchAction == TouchControlGroupAction.Create)
			{
				world.ControlGroups.CreateControlGroup(group);
				PlayClick();
				return;
			}

			if (touchAction == TouchControlGroupAction.Ignore)
			{
				Game.Sound.PlayNotification(world.Map.Rules, world.LocalPlayer, "Sounds", clickDisabledSound, null);
				return;
			}

			if (mods.HasModifier(Modifiers.Ctrl) && mods.HasModifier(Modifiers.Shift))
				world.ControlGroups.AddSelectionToControlGroup(group);
			else if (mods.HasModifier(Modifiers.Ctrl))
				world.ControlGroups.CreateControlGroup(group);
			else if (mods.HasModifier(Modifiers.Shift))
				world.ControlGroups.CombineSelectionWithControlGroup(group);
			else if (mods.HasModifier(Modifiers.Alt) || jump)
			{
				world.ControlGroups.SelectControlGroup(group);
				viewport.Center(world.ControlGroups.GetActorsInControlGroup(group));
			}
			else
				world.ControlGroups.SelectControlGroup(group);

			PlayClick();
		}

		void SelectAllUnits()
		{
			if (world.IsGameOver)
				return;

			var eligiblePlayers = SelectionUtils.GetPlayersToIncludeInSelection(world);
			var modifiers = Game.GetModifierKeys();

			var newSelection = SelectionUtils.SelectActorsOnScreen(world, worldRenderer, null, eligiblePlayers)
				.SubsetWithHighestSelectionPriority(modifiers).ToList();

			if (newSelection.Count > selection.Actors.Count)
				TextNotificationsManager.AddFeedbackLine(SelectedUnitsAcrossScreen, "units", newSelection.Count);
			else
			{
				newSelection = SelectionUtils.SelectActorsInWorld(world, null, eligiblePlayers)
					.SubsetWithHighestSelectionPriority(modifiers).ToList();
				TextNotificationsManager.AddFeedbackLine(SelectedUnitsAcrossMap, "units", newSelection.Count);
			}

			selection.Combine(world, newSelection, false, false);
			PlayClick();
		}

		void SelectUnitsByType()
		{
			if (world.IsGameOver)
				return;

			if (!selection.Actors.Any())
			{
				TextNotificationsManager.AddFeedbackLine(NothingSelected);
				Game.Sound.PlayNotification(world.Map.Rules, world.LocalPlayer, "Sounds", clickDisabledSound, null);
				return;
			}

			var eligiblePlayers = SelectionUtils.GetPlayersToIncludeInSelection(world);
			var ownedActors = selection.Actors
				.Where(x => !x.IsDead && eligiblePlayers.Contains(x.Owner))
				.ToList();

			if (ownedActors.Count == 0)
				return;

			var selectedClasses = ownedActors
				.Select(a => a.Trait<ISelectable>().Class)
				.ToHashSet();

			var newSelection = SelectionUtils.SelectActorsOnScreen(world, worldRenderer, selectedClasses, eligiblePlayers).ToList();

			if (newSelection.Count > selection.Actors.Count)
				TextNotificationsManager.AddFeedbackLine(SelectedUnitsAcrossScreen, "units", newSelection.Count);
			else
			{
				newSelection = SelectionUtils.SelectActorsInWorld(world, selectedClasses, eligiblePlayers).ToList();
				TextNotificationsManager.AddFeedbackLine(SelectedUnitsAcrossMap, "units", newSelection.Count);
			}

			selection.Combine(world, newSelection, true, false);
			PlayClick();
		}

		void CycleBase()
		{
			var player = world.RenderPlayer ?? world.LocalPlayer;
			if (player == null)
				return;

			var bases = BaseCyclePolicy.Order(
				world.ActorsHavingTrait<BaseBuilding>().Where(a => a.Owner == player),
				a => a.ActorID,
				a => a.TraitOrDefault<PrimaryBuilding>()?.IsPrimary == true);

			var nextBase = BaseCyclePolicy.Resolve(
				bases,
				world.ActorsHavingTrait<Building>()
					.Where(a => a.Owner == player && a.Info.HasTraitInfo<SelectableInfo>()),
				selection.Contains);
			BaseCyclePolicy.Execute(nextBase,
				building => selection.Combine(world, new Actor[] { building }, false, true),
				() => viewport.Center(selection.Actors),
				PlayClick);
		}

		void CycleHarvesters()
		{
			var player = world.RenderPlayer ?? world.LocalPlayer;
			if (player == null)
				return;

			var harvesters = world.ActorsHavingTrait<Harvester>()
				.Where(a => a.IsInWorld && a.Owner == player)
				.ToList();

			if (harvesters.Count == 0)
				return;

			var nextHarvester = harvesters.SkipWhile(b => !selection.Contains(b)).Skip(1).FirstOrDefault() ?? harvesters[0];
			selection.Combine(world, new Actor[] { nextHarvester }, false, true);
			viewport.Center(selection.Actors);
			PlayClick();
		}

		void JumpToLastEvent()
		{
			var radar = world.WorldActor.TraitOrDefault<RadarPings>();
			if (radar == null || radar.LastPingPosition == null)
				return;

			viewport.Center(radar.LastPingPosition.Value);
			PlayClick();
		}

		void RemoveFromGroup()
		{
			if (world.LocalPlayer == null)
				return;

			foreach (var a in selection.Actors.Where(a => a.Owner == world.LocalPlayer).ToList())
				world.ControlGroups.RemoveFromControlGroup(a);

			PlayClick();
		}

		void PlayClick()
		{
			Game.Sound.PlayNotification(world.Map.Rules, world.LocalPlayer, "Sounds", clickSound, null);
		}
	}
}
