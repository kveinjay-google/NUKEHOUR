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
using System.Linq;
using System.Threading;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Warheads
{
	public sealed class ImpactEffectAuditSpriteRegistration
	{
		public long CorrelationId { get; }
		public string Image { get; }
		public string Sequence { get; }
		public WPos Position { get; }
		public int ScheduledTick { get; }
		public int AddedTick { get; private set; } = -1;
		public SpriteEffect Effect { get; private set; }

		internal ImpactEffectAuditSpriteRegistration(long correlationId, string image,
			string sequence, WPos position, int scheduledTick)
		{
			CorrelationId = correlationId;
			Image = image;
			Sequence = sequence;
			Position = position;
			ScheduledTick = scheduledTick;
		}

		internal void MarkAdded(SpriteEffect effect, int tick)
		{
			Effect = effect;
			AddedTick = tick;
		}
	}

	public sealed class ImpactEffectAuditSoundRegistration
	{
		public long CorrelationId { get; }
		public string Asset { get; }
		public WPos Position { get; }
		public int ScheduledTick { get; }
		public ISound Playback { get; }
		public bool DummyEngine { get; }

		internal ImpactEffectAuditSoundRegistration(long correlationId, string asset,
			WPos position, int scheduledTick, ISound playback, bool dummyEngine)
		{
			CorrelationId = correlationId;
			Asset = asset;
			Position = position;
			ScheduledTick = scheduledTick;
			Playback = playback;
			DummyEngine = dummyEngine;
		}
	}

	public sealed class ImpactEffectAuditRegistrySnapshot
	{
		public IReadOnlyList<ImpactEffectAuditSpriteRegistration> Sprites { get; }
		public IReadOnlyList<ImpactEffectAuditSoundRegistration> Sounds { get; }

		internal ImpactEffectAuditRegistrySnapshot(
			IReadOnlyList<ImpactEffectAuditSpriteRegistration> sprites,
			IReadOnlyList<ImpactEffectAuditSoundRegistration> sounds)
		{
			Sprites = sprites;
			Sounds = sounds;
		}
	}

	public static class ImpactEffectAuditRegistry
	{
		static readonly bool Enabled =
			bool.TryParse(Environment.GetEnvironmentVariable("OPENRA_IMPACT_EFFECT_AUDIT"), out var enabled) && enabled;
		static readonly object Sync = new();
		static readonly List<ImpactEffectAuditSpriteRegistration> Sprites = new();
		static readonly List<ImpactEffectAuditSoundRegistration> Sounds = new();
		static long nextCorrelationId;

		public static bool IsEnabled => Enabled;
		public static long LatestCorrelationId => Enabled ? Interlocked.Read(ref nextCorrelationId) : 0;

		public static long ScheduleSprite(string image, string sequence, WPos position, int tick)
		{
			if (!Enabled)
				return 0;

			var correlationId = Interlocked.Increment(ref nextCorrelationId);
			lock (Sync)
				Sprites.Add(new ImpactEffectAuditSpriteRegistration(
					correlationId, image, sequence, position, tick));

			DiagnosticTrace.Instant(DiagnosticSubsystem.Render,
				$"ImpactEffectAudit.SpriteScheduled|{correlationId}|{image}|{sequence}",
				tick, PackHorizontalPosition(position));
			return correlationId;
		}

		public static void MarkSpriteAdded(long correlationId, SpriteEffect effect, int tick)
		{
			if (!Enabled || correlationId <= 0 || effect == null)
				return;

			ImpactEffectAuditSpriteRegistration registration;
			lock (Sync)
			{
				registration = Sprites.Find(item => item.CorrelationId == correlationId);
				registration?.MarkAdded(effect, tick);
			}

			if (registration != null)
				DiagnosticTrace.Instant(DiagnosticSubsystem.Render,
					$"ImpactEffectAudit.SpriteAdded|{correlationId}|{registration.Image}|{registration.Sequence}",
					tick, PackHorizontalPosition(registration.Position));
		}

		public static long RegisterSound(string asset, WPos position, int tick,
			ISound playback, bool dummyEngine)
		{
			if (!Enabled)
				return 0;

			var correlationId = Interlocked.Increment(ref nextCorrelationId);
			lock (Sync)
				Sounds.Add(new ImpactEffectAuditSoundRegistration(
					correlationId, asset, position, tick, playback, dummyEngine));

			DiagnosticTrace.Instant(DiagnosticSubsystem.Audio,
				$"ImpactEffectAudit.Sound|{correlationId}|{asset}|" +
				$"{(playback != null ? "played" : "not-played")}|{(dummyEngine ? "dummy" : "device")}",
				tick, PackHorizontalPosition(position));
			return correlationId;
		}

		public static ImpactEffectAuditRegistrySnapshot Snapshot(long afterCorrelationId, WPos position)
		{
			if (!Enabled)
				return new ImpactEffectAuditRegistrySnapshot(
					Array.Empty<ImpactEffectAuditSpriteRegistration>(),
					Array.Empty<ImpactEffectAuditSoundRegistration>());

			lock (Sync)
				return new ImpactEffectAuditRegistrySnapshot(
					Sprites.Where(item => item.CorrelationId > afterCorrelationId && item.Position == position).ToArray(),
					Sounds.Where(item => item.CorrelationId > afterCorrelationId && item.Position == position).ToArray());
		}

		static long PackHorizontalPosition(WPos pos) =>
			unchecked((long)(((ulong)(uint)pos.X << 32) | (uint)pos.Y));
	}

	[Desc("Spawn a sprite with sound.")]
	public class CreateEffectWarhead : Warhead
	{
		[SequenceReference(nameof(Image), allowNullImage: true)]
		[Desc("List of explosion sequences that can be used.")]
		public readonly string[] Explosions = Array.Empty<string>();

		[Desc("Image containing explosion effect sequence.")]
		public readonly string Image = "explosion";

		[PaletteReference(nameof(UsePlayerPalette))]
		[Desc("Palette to use for explosion effect.")]
		public readonly string ExplosionPalette = "effect";

		[Desc("Remap explosion effect to player color, if art supports it.")]
		public readonly bool UsePlayerPalette = false;

		[Desc("Display explosion effect at ground level, regardless of explosion altitude.")]
		public readonly bool ForceDisplayAtGroundLevel = false;

		[Desc("List of sounds that can be played on impact.")]
		public readonly string[] ImpactSounds = Array.Empty<string>();

		[Desc("Chance of impact sound to play.")]
		public readonly int ImpactSoundChance = 100;

		[Desc("Whether to consider actors in determining whether the explosion should happen. If false, only terrain will be considered.")]
		public readonly bool ImpactActors = true;

		[Desc("The maximum inaccuracy of the effect spawn position relative to actual impact position.")]
		public readonly WDist Inaccuracy = WDist.Zero;

		static readonly BitSet<TargetableType> TargetTypeAir = new("Air");

		/// <summary>Checks if there are any actors at impact position and if the warhead is valid against any of them.</summary>
		ImpactActorType ActorTypeAtImpact(World world, WPos pos, Actor firedBy)
		{
			var anyInvalidActor = false;

			// Check whether the impact position overlaps with an actor's hitshape
			foreach (var victim in world.FindActorsOnCircle(pos, WDist.Zero))
			{
				if (!AffectsParent && victim == firedBy)
					continue;

				var activeShapes = victim.TraitsImplementing<HitShape>().Where(t => !t.IsTraitDisabled);
				if (!activeShapes.Any(s => s.DistanceFromEdge(victim, pos).Length <= 0))
					continue;

				if (IsValidAgainst(victim, firedBy))
					return ImpactActorType.Valid;

				anyInvalidActor = true;
			}

			return anyInvalidActor ? ImpactActorType.Invalid : ImpactActorType.None;
		}

		// ActorTypeAtImpact already checks AffectsParent beforehand, to avoid parent HitShape look-ups
		// (and to prevent returning ImpactActorType.Invalid on AffectsParent=false)
		public override bool IsValidAgainst(Actor victim, Actor firedBy)
		{
			var relationship = firedBy.Owner.RelationshipWith(victim.Owner);
			if (!ValidRelationships.HasRelationship(relationship))
				return false;

			// A target type is valid if it is in the valid targets list, and not in the invalid targets list.
			if (!IsValidTarget(victim.GetEnabledTargetTypes()))
				return false;

			return true;
		}

		public override void DoImpact(in Target target, WarheadArgs args)
		{
			if (target.Type == TargetType.Invalid)
				return;

			var firedBy = args.SourceActor;
			var pos = target.CenterPosition;
			var world = firedBy.World;
			var actorAtImpact = ImpactActors ? ActorTypeAtImpact(world, pos, firedBy) : ImpactActorType.None;

			// Ignore the impact if there are only invalid actors within range
			if (actorAtImpact == ImpactActorType.Invalid)
				return;

			// Ignore the impact if there are no valid actors and no valid terrain
			// (impacts are allowed on valid actors sitting on invalid terrain!)
			if (actorAtImpact == ImpactActorType.None && !IsValidAgainstTerrain(world, pos))
				return;

			var explosion = Explosions.RandomOrDefault(world.LocalRandom);
			if (Image != null && explosion != null)
			{
				if (Inaccuracy.Length > 0)
					pos += WVec.FromPDF(world.SharedRandom, 2) * Inaccuracy.Length / 1024;

				if (ForceDisplayAtGroundLevel)
				{
					var dat = world.Map.DistanceAboveTerrain(pos);
					pos -= new WVec(0, 0, dat.Length);
				}

				var palette = ExplosionPalette;
				if (UsePlayerPalette)
					palette += firedBy.Owner.InternalName;

				if (ImpactEffectAuditRegistry.IsEnabled)
				{
					var correlationId = ImpactEffectAuditRegistry.ScheduleSprite(
						Image, explosion, pos, Game.LocalTick);
					world.AddFrameEndTask(w =>
					{
						var effect = new SpriteEffect(pos, w, Image, explosion, palette);
						w.Add(effect);
						ImpactEffectAuditRegistry.MarkSpriteAdded(correlationId, effect, Game.LocalTick);
					});
				}
				else
					world.AddFrameEndTask(w => w.Add(new SpriteEffect(pos, w, Image, explosion, palette)));
			}

			var impactSound = ImpactSounds.RandomOrDefault(world.LocalRandom);
			if (impactSound != null && world.LocalRandom.Next(0, 100) < ImpactSoundChance)
			{
				var playback = Game.Sound.Play(SoundType.World, impactSound, pos);
				if (ImpactEffectAuditRegistry.IsEnabled)
					ImpactEffectAuditRegistry.RegisterSound(
						impactSound, pos, Game.LocalTick, playback, Game.Sound.DummyEngine);
			}
		}

		/// <summary>Checks if the warhead is valid against the terrain at impact position.</summary>
		bool IsValidAgainstTerrain(World world, WPos pos)
		{
			var cell = world.Map.CellContaining(pos);
			if (!world.Map.Contains(cell))
				return false;

			var dat = world.Map.DistanceAboveTerrain(pos);
			return IsValidTarget(dat > AirThreshold ? TargetTypeAir : world.Map.GetTerrainInfo(cell).TargetTypes);
		}
	}
}
