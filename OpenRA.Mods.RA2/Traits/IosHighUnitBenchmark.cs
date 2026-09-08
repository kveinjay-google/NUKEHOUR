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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	public enum IosHighUnitBenchmarkState
	{
		Idle,
		Move,
		Combat
	}

	public enum IosHighUnitBenchmarkCamera
	{
		Visible,
		Empty
	}

	public sealed class IosHighUnitBenchmarkConfiguration
	{
		static readonly HashSet<int> SupportedCounts = new() { 100, 300, 600, 1000 };

		public bool Enabled { get; private init; }
		public int Count { get; private init; }
		public IosHighUnitBenchmarkState State { get; private init; }
		public IosHighUnitBenchmarkCamera Camera { get; private init; }
		public bool Paused { get; private init; }
		public string UnitType { get; private init; }

		public static IosHighUnitBenchmarkConfiguration Parse(Func<string, string> read)
		{
			if (!int.TryParse(read("OPENRA_IOS_PERF_COUNT"), out var count) || !SupportedCounts.Contains(count))
				return new IosHighUnitBenchmarkConfiguration { Enabled = false };

			Enum.TryParse(read("OPENRA_IOS_PERF_STATE"), true, out IosHighUnitBenchmarkState state);
			Enum.TryParse(read("OPENRA_IOS_PERF_CAMERA"), true, out IosHighUnitBenchmarkCamera camera);
			bool.TryParse(read("OPENRA_IOS_PERF_PAUSED"), out var paused);
			var unitType = read("OPENRA_IOS_PERF_UNIT");

			return new IosHighUnitBenchmarkConfiguration
			{
				Enabled = true,
				Count = count,
				State = state,
				Camera = camera,
				Paused = paused,
				UnitType = string.IsNullOrWhiteSpace(unitType) ? "e1" : unitType.Trim().ToLowerInvariant()
			};
		}
	}

	[Desc("Creates deterministic high-unit-count performance benchmark scenarios when enabled by environment variables.")]
	public sealed class IosHighUnitBenchmarkInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new IosHighUnitBenchmark(); }
	}

	public sealed class IosHighUnitBenchmark : IWorldLoaded, ITick, INotifyActorDisposing
	{
		int readyCountdown = -1;
		World world;
		bool pauseWhenReady;
		IosBatchedDestructionAuditSession destructionAudit;
		V3RuntimeAuditSession v3RuntimeAudit;
		ImpactEffectsRuntimeAuditSession impactEffectsRuntimeAudit;

		public void WorldLoaded(World world, WorldRenderer worldRenderer)
		{
			var v3AuditConfig = V3RuntimeAuditConfiguration.Parse(Environment.GetEnvironmentVariable);
			if (v3AuditConfig.Enabled)
			{
				if (V3RuntimeAuditConfiguration.ShouldStartInWorld(world.Type))
					world.AddFrameEndTask(w =>
						v3RuntimeAudit = V3RuntimeAuditSession.TryCreate(w, worldRenderer, v3AuditConfig));

				return;
			}

			var impactAuditConfig = ImpactEffectsRuntimeAuditConfiguration.Parse(Environment.GetEnvironmentVariable);
			if (impactAuditConfig.Enabled)
			{
				if (ImpactEffectsRuntimeAuditConfiguration.ShouldStartInWorld(world.Type))
					world.AddFrameEndTask(w =>
						impactEffectsRuntimeAudit = ImpactEffectsRuntimeAuditSession.TryCreate(
							w, worldRenderer, impactAuditConfig));

				return;
			}

			var destructionConfig = IosDestructionAuditConfiguration.Parse(Environment.GetEnvironmentVariable);
			if (destructionConfig.Enabled)
			{
				if (IosDestructionAuditConfiguration.ShouldStartInWorld(world.Type))
					world.AddFrameEndTask(w =>
						destructionAudit = IosBatchedDestructionAuditSession.TryCreate(w, worldRenderer, destructionConfig));

				return;
			}

			var config = IosHighUnitBenchmarkConfiguration.Parse(Environment.GetEnvironmentVariable);
			if (!config.Enabled)
				return;

			world.AddFrameEndTask(w => CreateScenario(w, worldRenderer, config));
		}

		void ITick.Tick(Actor self)
		{
			if (v3RuntimeAudit != null)
			{
				v3RuntimeAudit.Tick();
				return;
			}

			if (impactEffectsRuntimeAudit != null)
			{
				impactEffectsRuntimeAudit.Tick();
				return;
			}

			if (destructionAudit != null)
			{
				destructionAudit.Tick();
				return;
			}

			if (readyCountdown < 0)
				return;

			if (readyCountdown-- == 0)
			{
				if (pauseWhenReady)
					world.SetLocalPauseState(true);

				Game.BeginBenchmark();
				readyCountdown = -1;
				Log.Write("perf", "Benchmark sampling window started after scenario stabilization.");
			}
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			impactEffectsRuntimeAudit?.AbortForWorldDisposal();
		}

		void CreateScenario(World world, WorldRenderer worldRenderer, IosHighUnitBenchmarkConfiguration config)
		{
			if (!world.Map.Rules.Actors.TryGetValue(config.UnitType, out var actorInfo))
			{
				Log.Write("perf", $"Benchmark unit type '{config.UnitType}' does not exist.");
				return;
			}

			var positionable = actorInfo.TraitInfo<IPositionableInfo>();
			var players = world.Players.Where(p => p.Playable && !p.NonCombatant).ToArray();
			if (players.Length == 0)
				return;

			var localPlayer = world.LocalPlayer ?? players[0];
			var enemyPlayer = players.FirstOrDefault(p => !p.IsAlliedWith(localPlayer)) ?? localPlayer;
			var validCells = world.Map.AllCells
				.Where(c => positionable.CanEnterCell(world, null, c))
				.OrderBy(c => c.Y)
				.ThenBy(c => c.X)
				.ToArray();

			var actors = new List<Actor>(config.Count);
			for (var i = 0; i < config.Count && i < validCells.Length; i++)
			{
				var cell = validCells[(i * 7919) % validCells.Length];
				if (!positionable.CanEnterCell(world, null, cell))
					continue;

				var subCell = positionable.SharesCell ? world.ActorMap.FreeSubCell(cell) : SubCell.FullCell;
				if (positionable.SharesCell && subCell == SubCell.Invalid)
					continue;

				var owner = config.State == IosHighUnitBenchmarkState.Combat && (i & 1) != 0 ? enemyPlayer : localPlayer;
				actors.Add(world.CreateActor(config.UnitType, new TypeDictionary
				{
					new OwnerInit(owner),
					new LocationInit(cell),
					new SubCellInit(subCell),
					new FacingInit(WAngle.Zero),
					new SpawnedByMapInit(),
				}));
			}

			ApplyScenarioOrders(world, actors, validCells, config.State);

			if (config.Camera == IosHighUnitBenchmarkCamera.Visible && actors.Count > 0)
				worldRenderer.Viewport.Center(actors);
			else if (config.Camera == IosHighUnitBenchmarkCamera.Empty)
				worldRenderer.Viewport.Center(world.Map.CenterOfCell(world.Map.AllCells.TopLeft));

			// Exclude creation plus the first second of shroud/path/render cache work.
			// The benchmark's own warmup begins after this stabilization countdown.
			readyCountdown = 25;
			this.world = world;
			pauseWhenReady = config.Paused;
			Log.Write("perf", $"Benchmark scenario ready: {actors.Count} {config.UnitType} actors, " +
				$"state={config.State}, camera={config.Camera}, paused={config.Paused}.");
		}

		static void ApplyScenarioOrders(World world, List<Actor> actors, CPos[] validCells, IosHighUnitBenchmarkState state)
		{
			if (state == IosHighUnitBenchmarkState.Move)
			{
				for (var i = 0; i < actors.Count; i++)
					world.IssueOrder(new Order("Move", actors[i], Target.FromCell(world, validCells[validCells.Length - 1 - i]), false));
			}
			else if (state == IosHighUnitBenchmarkState.Combat)
			{
				for (var i = 0; i + 1 < actors.Count; i += 2)
				{
					world.IssueOrder(new Order("Attack", actors[i], Target.FromActor(actors[i + 1]), false));
					world.IssueOrder(new Order("Attack", actors[i + 1], Target.FromActor(actors[i]), false));
				}
			}
		}
	}
}
