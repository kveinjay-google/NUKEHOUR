#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Mods.Common.Effects;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public enum ActorFlashType { Overlay, Tint }

	[TraitLocation(SystemActors.World)]
	[Desc("Renders an effect at the order target locations.")]
	public class OrderEffectsInfo : TraitInfo
	{
		[Desc("The image to use for order-specific feedback. Disabled if null.")]
		public readonly string OrderFeedbackImage = null;

		[SequenceReference(nameof(OrderFeedbackImage), allowNullImage: true,
			dictionaryReference: LintDictionaryReference.Values)]
		[Desc("Order strings and their corresponding feedback sequences.")]
		public readonly Dictionary<string, string> OrderFeedbackSequences = new();

		[PaletteReference]
		[Desc("The palette to use for order-specific feedback. Disabled if null.")]
		public readonly string OrderFeedbackPalette = null;

		[Desc("The image to use.")]
		[FieldLoader.Require]
		public readonly string TerrainFlashImage = null;

		[Desc("The sequence to use.")]
		[FieldLoader.Require]
		public readonly string TerrainFlashSequence = null;

		[Desc("The palette to use.")]
		public readonly string TerrainFlashPalette = null;

		[Desc("The type of effect to apply to targeted (frozen) actors. Accepts values Overlay and Tint.")]
		public readonly ActorFlashType ActorFlashType = ActorFlashType.Overlay;

		[Desc("The overlay color to display when ActorFlashType is Overlay.")]
		public readonly Color ActorFlashOverlayColor = Color.White;

		[Desc("The overlay transparency to display when ActorFlashType is Overlay.")]
		public readonly float ActorFlashOverlayAlpha = 0.5f;

		[Desc("The tint to apply when ActorFlashType is Tint.")]
		public readonly float3 ActorFlashTint = new(1.4f, 1.4f, 1.4f);

		[Desc("Number of times to flash (frozen) actors.")]
		public readonly int ActorFlashCount = 2;

		[Desc("Number of ticks between (frozen) actor flashes.")]
		public readonly int ActorFlashInterval = 2;

		public override object Create(ActorInitializer init)
		{
			return new OrderEffects(this);
		}

		public string GetOrderFeedbackSequence(string orderString, TargetType targetType)
		{
			if (targetType != TargetType.Actor && targetType != TargetType.FrozenActor && targetType != TargetType.Terrain)
				return null;

			if (string.IsNullOrEmpty(OrderFeedbackImage) || string.IsNullOrEmpty(OrderFeedbackPalette))
				return null;

			return OrderFeedbackSequences.TryGetValue(orderString, out var sequence) && !string.IsNullOrEmpty(sequence) ?
				sequence : null;
		}
	}

	public class OrderEffects : INotifyOrderIssued
	{
		readonly OrderEffectsInfo info;
		readonly Action<World, WPos, string, string, string> scheduleOrderFeedback;

		public OrderEffects(OrderEffectsInfo info)
			: this(info, ScheduleSpriteAnnotation)
		{
		}

		public OrderEffects(OrderEffectsInfo info,
			Action<World, WPos, string, string, string> scheduleOrderFeedback)
		{
			this.info = info;
			this.scheduleOrderFeedback = scheduleOrderFeedback;
		}

		static void ScheduleSpriteAnnotation(
			World world, WPos position, string image, string sequence, string palette)
		{
			world.AddFrameEndTask(w => w.Add(new SpriteAnnotation(position, w, image, sequence, palette)));
		}

		bool INotifyOrderIssued.OrderIssued(World world, string orderString, Target target)
		{
			var sequence = info.GetOrderFeedbackSequence(orderString, target.Type);
			if (sequence != null)
			{
				var targetPosition = target.CenterPosition;
				scheduleOrderFeedback(
					world, targetPosition, info.OrderFeedbackImage, sequence, info.OrderFeedbackPalette);
				return true;
			}

			switch (target.Type)
			{
				case TargetType.Actor:
				{
					if (info.ActorFlashType == ActorFlashType.Overlay)
						world.AddFrameEndTask(w => w.Add(new FlashTarget(
							target.Actor, info.ActorFlashOverlayColor, info.ActorFlashOverlayAlpha,
							info.ActorFlashCount, info.ActorFlashInterval)));
					else
						world.AddFrameEndTask(w => w.Add(new FlashTarget(
							target.Actor, info.ActorFlashTint,
							info.ActorFlashCount, info.ActorFlashInterval)));

					return true;
				}

				case TargetType.FrozenActor:
				{
					if (info.ActorFlashType == ActorFlashType.Overlay)
						target.FrozenActor.Flash(info.ActorFlashOverlayColor, info.ActorFlashOverlayAlpha);
					else
						target.FrozenActor.Flash(info.ActorFlashTint);

					return true;
				}

				case TargetType.Terrain:
				{
					world.AddFrameEndTask(w => w.Add(new SpriteAnnotation(
						target.CenterPosition, world, info.TerrainFlashImage, info.TerrainFlashSequence, info.TerrainFlashPalette)));
					return true;
				}

				default:
					return false;
			}
		}
	}
}
