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
using System.Globalization;
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	public sealed class IosDestructionAuditConfiguration
	{
		const int DefaultIterations = 3;
		const int DefaultPreKillSettleTicks = 1;
		const int DefaultSettleTicks = 40;
		const int DefaultTimeoutTicks = 250;
		const int DefaultBatchSize = 20;
		static readonly string[] RequiredDeathTypes = { "BulletDeath", "ExplosionDeath" };

		public bool Enabled { get; private init; }
		public int Iterations { get; private init; }
		public int PreKillSettleTicks => DefaultPreKillSettleTicks;
		public int SettleTicks { get; private init; }
		public int TimeoutTicks { get; private init; }
		public int BatchSize { get; private init; }
		public string RunId { get; private init; }
		readonly HashSet<string> actorFilter;

		IosDestructionAuditConfiguration(bool enabled, int iterations, int settleTicks, int timeoutTicks,
			int batchSize, string runId, HashSet<string> actorFilter)
		{
			Enabled = enabled;
			Iterations = iterations;
			SettleTicks = settleTicks;
			TimeoutTicks = timeoutTicks;
			BatchSize = batchSize;
			RunId = runId;
			this.actorFilter = actorFilter;
		}

		public static IosDestructionAuditConfiguration Parse(Func<string, string> read)
		{
			if (!bool.TryParse(read("OPENRA_IOS_DESTRUCTION_AUDIT"), out var enabled) || !enabled)
				return new IosDestructionAuditConfiguration(
					false, 0, 0, 0, 0, string.Empty, new HashSet<string>(StringComparer.Ordinal));

			return new IosDestructionAuditConfiguration(
				true,
				ParseBounded(read("OPENRA_IOS_DESTRUCTION_ITERATIONS"), DefaultIterations, 1, 10),
				ParseBounded(read("OPENRA_IOS_DESTRUCTION_SETTLE_TICKS"), DefaultSettleTicks, 1, 250),
				ParseBounded(read("OPENRA_IOS_DESTRUCTION_TIMEOUT_TICKS"), DefaultTimeoutTicks, 25, 2500),
				ParseBounded(read("OPENRA_IOS_DESTRUCTION_BATCH_SIZE"), DefaultBatchSize, 1, DefaultBatchSize),
				SafeRunId(read("OPENRA_IOS_DESTRUCTION_RUN_ID")),
				ParseActorFilter(read("OPENRA_IOS_DESTRUCTION_ACTOR_FILTER")));
		}

		public static IosDestructionAuditConfiguration CreateForTests(int iterations = DefaultIterations) =>
			new(true, iterations, DefaultSettleTicks, DefaultTimeoutTicks, DefaultBatchSize,
				"test", new HashSet<string>(StringComparer.Ordinal));

		public bool ShouldIncludeActor(string actorName) =>
			actorFilter.Count == 0 || actorFilter.Contains(actorName?.Trim().ToLowerInvariant() ?? string.Empty);

		public static bool ShouldStartInWorld(WorldType worldType) => worldType == WorldType.Regular;

		public static bool IsGameplayActorName(string actorName) =>
			!string.IsNullOrWhiteSpace(actorName) && !actorName.Contains('.');

		public static bool CanUseAirborneFallbackCell(bool hasAircraft, bool hasBuilding) =>
			hasAircraft && !hasBuilding;

		public static BlockedByActor SpawnOccupancyCheck => BlockedByActor.All;

		public static int SpawnCandidateStartIndex(int caseIndex, int candidateCount)
		{
			if (candidateCount <= 0)
				return 0;

			return (int)((long)Math.Max(caseIndex, 0) * 997 % candidateCount);
		}

		public static bool IsAuditable(string name, ActorInfo actorInfo) =>
			IsGameplayActorName(name) && !name.StartsWith('^') &&
			actorInfo.HasTraitInfo<IHealthInfo>() &&
			actorInfo.HasTraitInfo<ISelectableInfo>() &&
			(actorInfo.HasTraitInfo<BuildingInfo>() || actorInfo.HasTraitInfo<IPositionableInfo>());

		public static DestructionAuditCategory Classify(bool hasBuilding, bool hasAttack, bool usesWater)
		{
			if (!hasBuilding)
				return DestructionAuditCategory.MobileUnit;
			if (usesWater)
				return DestructionAuditCategory.NavalBuilding;
			return hasAttack ? DestructionAuditCategory.Defense : DestructionAuditCategory.Building;
		}

		public static DestructionAuditCategory Classify(ActorInfo actorInfo)
		{
			var building = actorInfo.TraitInfoOrDefault<BuildingInfo>();
			return Classify(
				building != null,
				actorInfo.TraitInfos<AttackBaseInfo>().Any(),
				building?.TerrainTypes.Contains("Water") == true);
		}

		public IEnumerable<DestructionAuditCase> BuildCases(IEnumerable<string> actorNames)
		{
			var names = actorNames
				.Where(n => !string.IsNullOrWhiteSpace(n))
				.Select(n => n.Trim().ToLowerInvariant())
				.Distinct(StringComparer.Ordinal)
				.OrderBy(n => n, StringComparer.Ordinal)
				.ToArray();

			for (var iteration = 1; iteration <= Iterations; iteration++)
				foreach (var name in names)
					foreach (var deathType in RequiredDeathTypes)
						yield return new DestructionAuditCase(name, deathType, iteration);
		}

		public IEnumerable<DestructionAuditBatch> BuildBatches(IEnumerable<DestructionAuditActor> actors)
		{
			var normalized = actors
				.Where(a => !string.IsNullOrWhiteSpace(a.ActorName))
				.Select(a => new DestructionAuditActor(a.ActorName.Trim().ToLowerInvariant(), a.Category))
				.GroupBy(a => a.ActorName, StringComparer.Ordinal)
				.Select(g => g.First())
				.OrderBy(a => a.Category)
				.ThenBy(a => a.ActorName, StringComparer.Ordinal)
				.ToArray();

			var batchNumber = 0;
			for (var iteration = 1; iteration <= Iterations; iteration++)
				foreach (var category in normalized.Select(a => a.Category).Distinct())
					foreach (var deathType in RequiredDeathTypes)
						foreach (var group in normalized.Where(a => a.Category == category).Chunk(BatchSize))
						{
							var batchCases = group
								.Select(a => new DestructionAuditCase(
									a.ActorName, deathType, iteration, a.Category))
								.ToArray();
							yield return new DestructionAuditBatch(
								$"batch-{++batchNumber:D3}", category, deathType, batchCases);
						}
		}

		static int ParseBounded(string value, int fallback, int minimum, int maximum) =>
			int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ?
				parsed.Clamp(minimum, maximum) : fallback;

		static HashSet<string> ParseActorFilter(string value) =>
			new((value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
				.Select(v => v.Trim().ToLowerInvariant())
				.Where(v => v.Length > 0), StringComparer.Ordinal);

		static string SafeRunId(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

			return new string(value.Trim().Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
		}
	}

	public enum DestructionAuditCategory
	{
		MobileUnit,
		Building,
		Defense,
		NavalBuilding
	}

	public sealed class DestructionAuditActor
	{
		public string ActorName { get; }
		public DestructionAuditCategory Category { get; }

		public DestructionAuditActor(string actorName, DestructionAuditCategory category)
		{
			ActorName = actorName;
			Category = category;
		}
	}

	public sealed class DestructionAuditBatch
	{
		public string Id { get; }
		public DestructionAuditCategory Category { get; }
		public string DeathType { get; }
		public IReadOnlyList<DestructionAuditCase> Cases { get; }

		public DestructionAuditBatch(string id, DestructionAuditCategory category,
			string deathType, IReadOnlyList<DestructionAuditCase> cases)
		{
			Id = id;
			Category = category;
			DeathType = deathType;
			Cases = cases;
		}
	}

	public sealed class DestructionAuditCase
	{
		public string ActorName { get; }
		public string DeathType { get; }
		public int Iteration { get; }
		public DestructionAuditCategory Category { get; }
		public string Id => $"{Iteration:D2}-{ActorName}-{DeathType}";

		public DestructionAuditCase(string actorName, string deathType, int iteration,
			DestructionAuditCategory category = DestructionAuditCategory.MobileUnit)
		{
			ActorName = actorName;
			DeathType = deathType;
			Iteration = iteration;
			Category = category;
		}
	}

	public enum DestructionAuditPhase
	{
		Prepare,
		SettleBeforeKill,
		Kill,
		SettleEffects,
		Cleanup,
		Passed,
		Skipped,
		Failed
	}

	public sealed class DestructionAuditStateMachine
	{
		readonly int preKillSettleTicks;
		readonly int effectSettleTicks;
		readonly int timeoutTicks;
		int phaseTicks;
		int totalTicks;

		public DestructionAuditPhase Phase { get; private set; } = DestructionAuditPhase.Prepare;
		public string Detail { get; private set; } = string.Empty;

		public DestructionAuditStateMachine(int settleTicks, int timeoutTicks)
			: this(settleTicks, settleTicks, timeoutTicks) { }

		public DestructionAuditStateMachine(int preKillSettleTicks, int effectSettleTicks, int timeoutTicks)
		{
			this.preKillSettleTicks = preKillSettleTicks;
			this.effectSettleTicks = effectSettleTicks;
			this.timeoutTicks = timeoutTicks;
		}

		public void Spawned() => Transition(DestructionAuditPhase.SettleBeforeKill);
		public void KillReturned() => Transition(DestructionAuditPhase.SettleEffects);
		public void CleanedUp() => Transition(DestructionAuditPhase.Passed);

		public void Skip(string detail)
		{
			Detail = detail;
			Transition(DestructionAuditPhase.Skipped, false);
		}

		public void Fail(string detail)
		{
			Detail = detail;
			Transition(DestructionAuditPhase.Failed, false);
		}

		public void Tick()
		{
			if (IsTerminal)
				return;

			totalTicks++;
			phaseTicks++;
			if (totalTicks > timeoutTicks)
			{
				var timedOutPhase = Phase;
				Fail($"Timed out in {timedOutPhase} after {totalTicks} ticks");
				return;
			}

			if (Phase == DestructionAuditPhase.SettleBeforeKill && phaseTicks >= preKillSettleTicks)
				Transition(DestructionAuditPhase.Kill);
			else if (Phase == DestructionAuditPhase.SettleEffects && phaseTicks >= effectSettleTicks)
				Transition(DestructionAuditPhase.Cleanup);
		}

		public bool IsTerminal => Phase == DestructionAuditPhase.Passed ||
			Phase == DestructionAuditPhase.Skipped || Phase == DestructionAuditPhase.Failed;

		void Transition(DestructionAuditPhase next, bool resetPhaseTicks = true)
		{
			Phase = next;
			if (resetPhaseTicks)
				phaseTicks = 0;
		}
	}

	sealed class IosDestructionAuditSession : IDisposable
	{
		readonly IosDestructionAuditConfiguration config;
		readonly List<DestructionAuditCase> cases;
		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly HashSet<uint> baselineActors;
		readonly CPos[] spawnCandidates;
		readonly string outputDirectory;
		readonly FileStream progressStream;
		readonly StreamWriter progressWriter;
		readonly FileStream resultsStream;
		readonly StreamWriter resultsWriter;
		DestructionAuditStateMachine state;
		DestructionAuditCase currentCase;
		Actor currentActor;
		int currentIndex;
		int passed;
		int skipped;
		int failed;
		bool completed;

		IosDestructionAuditSession(World world, WorldRenderer worldRenderer,
			IosDestructionAuditConfiguration config, IEnumerable<string> actorNames)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			this.config = config;
			cases = config.BuildCases(actorNames).ToList();
			baselineActors = world.Actors.Select(a => a.ActorID).ToHashSet();
			spawnCandidates = world.Map.AllCells.OrderBy(c => c.Y).ThenBy(c => c.X).ToArray();
			outputDirectory = Path.Combine(Platform.SupportDir, "Logs", "DestructionAudit", config.RunId);
			Directory.CreateDirectory(outputDirectory);

			progressStream = OpenAppend(Path.Combine(outputDirectory, "progress.log"));
			progressWriter = new StreamWriter(progressStream) { AutoFlush = true };
			resultsStream = OpenAppend(Path.Combine(outputDirectory, "results.csv"));
			resultsWriter = new StreamWriter(resultsStream) { AutoFlush = true };
			if (resultsStream.Length == 0)
				WriteDurable(resultsWriter, resultsStream, "case,actor,death_type,iteration,result,detail");

			RecordProgress("audit-start", $"cases={cases.Count}");
			WriteSummary(false);
		}

		public static IosDestructionAuditSession TryCreate(World world, WorldRenderer worldRenderer,
			IosDestructionAuditConfiguration config)
		{
			if (!config.Enabled || !IosDestructionAuditConfiguration.ShouldStartInWorld(world.Type))
				return null;

			var actorNames = world.Map.Rules.Actors
				.Where(kv => IosDestructionAuditConfiguration.IsAuditable(kv.Key, kv.Value))
				.Select(kv => kv.Key);
			return new IosDestructionAuditSession(world, worldRenderer, config, actorNames);
		}

		public void Tick()
		{
			if (completed)
				return;

			if (currentCase == null)
			{
				if (currentIndex >= cases.Count)
				{
					Complete();
					return;
				}

				BeginCase(cases[currentIndex]);
				return;
			}

			if (state.Phase == DestructionAuditPhase.SettleBeforeKill ||
				state.Phase == DestructionAuditPhase.SettleEffects)
				state.Tick();

			if (state.Phase == DestructionAuditPhase.Kill)
				KillCurrentActor();
			else if (state.Phase == DestructionAuditPhase.Cleanup)
				CleanupCurrentCase();

			if (state.IsTerminal)
				FinishCurrentCase();
		}

		void BeginCase(DestructionAuditCase auditCase)
		{
			currentCase = auditCase;
			state = new DestructionAuditStateMachine(
				config.PreKillSettleTicks, config.SettleTicks, config.TimeoutTicks);
			RecordProgress("prepare", auditCase.Id);

			try
			{
				var actorInfo = world.Map.Rules.Actors[auditCase.ActorName];
				var owner = ResolveOwner(actorInfo);
				if (owner == null)
				{
					state.Skip("no compatible owner");
					FinishCurrentCase();
					return;
				}

				if (!TryFindSpawnCell(actorInfo, out var cell, out var subCell))
				{
					state.Skip("no compatible terrain");
					FinishCurrentCase();
					return;
				}

				RecordProgress("spawn-enter", $"{auditCase.Id} cell={cell}");
				using var spawnTrace = DiagnosticTrace.Scope(DiagnosticSubsystem.Simulation,
					$"DestructionAudit.Spawn.{auditCase.ActorName}");
				var initializers = new TypeDictionary
				{
					new OwnerInit(owner),
					new LocationInit(cell),
					new SubCellInit(subCell),
					new FacingInit(actorInfo.TraitInfoOrDefault<IFacingInfo>()?.GetInitialFacing() ?? WAngle.Zero),
					new FactionInit(owner.Faction.InternalName),
					new SkipMakeAnimsInit(),
					new SpawnedByMapInit()
				};
				var aircraft = actorInfo.TraitInfoOrDefault<AircraftInfo>();
				if (aircraft != null)
					initializers.Add(new CenterPositionInit(
						world.Map.CenterOfCell(cell) + new WVec(0, 0, aircraft.CruiseAltitude.Length)));

				currentActor = world.CreateActor(auditCase.ActorName, initializers);
				worldRenderer.Viewport.Center(new[] { currentActor });
				state.Spawned();
				RecordProgress("spawn-returned", $"{auditCase.Id} actor={currentActor.ActorID}");
			}
			catch (Exception e)
			{
				state.Fail($"spawn exception: {e.GetType().Name}: {e.Message}");
				FinishCurrentCase();
			}
		}

		void KillCurrentActor()
		{
			if (currentActor == null || currentActor.Disposed)
			{
				state.Fail("actor disappeared before kill");
				return;
			}

			RecordProgress("kill-enter",
				$"{currentCase.Id} actor={currentActor.ActorID} type={currentCase.DeathType}");
			using var killTrace = DiagnosticTrace.Scope(DiagnosticSubsystem.Simulation,
				$"DestructionAudit.Kill.{currentCase.ActorName}.{currentCase.DeathType}");
			killTrace.Progress(currentActor.ActorID, currentCase.Iteration);
			try
			{
				currentActor.Kill(world.WorldActor, new BitSet<DamageType>(currentCase.DeathType));
				state.KillReturned();
				RecordProgress("kill-returned", $"{currentCase.Id} actor={currentActor.ActorID}");
			}
			catch (Exception e)
			{
				state.Fail($"kill exception: {e.GetType().Name}: {e.Message}");
			}
		}

		void CleanupCurrentCase()
		{
			RecordProgress("cleanup-enter", currentCase.Id);
			foreach (var actor in world.Actors.Where(a => !baselineActors.Contains(a.ActorID)).ToArray())
				if (!actor.Disposed && !actor.WillDispose)
					actor.Dispose();

			state.CleanedUp();
			RecordProgress("cleanup-returned", currentCase.Id);
		}

		void FinishCurrentCase()
		{
			var result = state.Phase.ToString().ToUpperInvariant();
			if (state.Phase == DestructionAuditPhase.Passed)
				passed++;
			else if (state.Phase == DestructionAuditPhase.Skipped)
				skipped++;
			else
				failed++;

			WriteDurable(resultsWriter, resultsStream,
				$"{Csv(currentCase.Id)},{Csv(currentCase.ActorName)},{Csv(currentCase.DeathType)}," +
				$"{currentCase.Iteration},{result},{Csv(state.Detail)}");
			RecordProgress("case-finished", $"{currentCase.Id} result={result} detail={state.Detail}");
			currentIndex++;
			currentCase = null;
			currentActor = null;
			state = null;
			WriteSummary(false);
		}

		void Complete()
		{
			completed = true;
			RecordProgress("audit-complete", $"passed={passed} skipped={skipped} failed={failed}");
			WriteSummary(true);
			if (failed == 0)
				Game.Exit();
		}

		Player ResolveOwner(ActorInfo actorInfo)
		{
			var required = actorInfo.TraitInfoOrDefault<RequiresSpecificOwnersInfo>();
			if (required != null)
				return world.Players.FirstOrDefault(p => required.ValidOwnerNames.Contains(p.InternalName));

			return world.LocalPlayer ?? world.Players.FirstOrDefault(p => p.Playable && !p.NonCombatant);
		}

		bool TryFindSpawnCell(ActorInfo actorInfo, out CPos cell, out SubCell subCell)
		{
			var building = actorInfo.TraitInfoOrDefault<BuildingInfo>();
			var positionable = actorInfo.TraitInfoOrDefault<IPositionableInfo>();
			var aircraft = actorInfo.TraitInfoOrDefault<AircraftInfo>();
			var start = IosDestructionAuditConfiguration.SpawnCandidateStartIndex(
				currentIndex, spawnCandidates.Length);
			for (var offset = 0; offset < spawnCandidates.Length; offset++)
			{
				var candidate = spawnCandidates[(start + offset) % spawnCandidates.Length];
				if (building != null && world.CanPlaceBuilding(candidate, actorInfo, building, null))
				{
					cell = candidate;
					subCell = SubCell.FullCell;
					return true;
				}

				if (positionable != null && positionable.CanEnterCell(
					world, null, candidate, SubCell.FullCell,
					check: IosDestructionAuditConfiguration.SpawnOccupancyCheck))
				{
					cell = candidate;
					subCell = positionable.SharesCell ? world.ActorMap.FreeSubCell(candidate) : SubCell.FullCell;
					if (subCell != SubCell.Invalid)
						return true;
				}
			}

			// Fixed-wing aircraft often have no landable terrain because they normally
			// spawn from an airfield. The audit creates them at cruise altitude, where
			// any contained map cell is a valid and deterministic test position.
			if (IosDestructionAuditConfiguration.CanUseAirborneFallbackCell(aircraft != null, building != null))
			{
				cell = spawnCandidates[start];
				subCell = SubCell.FullCell;
				return true;
			}

			cell = default;
			subCell = SubCell.Invalid;
			return false;
		}

		void RecordProgress(string phase, string detail)
		{
			DiagnosticTrace.Instant(DiagnosticSubsystem.Simulation,
				$"DestructionAudit.{phase}", currentIndex, cases.Count);
			WriteDurable(progressWriter, progressStream,
				$"{DateTime.UtcNow:O}\t{phase}\t{detail}");
		}

		void WriteSummary(bool complete)
		{
			var current = currentCase?.Id ?? string.Empty;
			var json = "{\n" +
				$"  \"complete\": {complete.ToString().ToLowerInvariant()},\n" +
				$"  \"total\": {cases.Count},\n" +
				$"  \"completed\": {currentIndex},\n" +
				$"  \"passed\": {passed},\n" +
				$"  \"skipped\": {skipped},\n" +
				$"  \"failed\": {failed},\n" +
				$"  \"current\": \"{Json(current)}\"\n" +
				"}\n";
			var path = Path.Combine(outputDirectory, "summary.json");
			var temporary = path + ".tmp";
			using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.Read))
			using (var writer = new StreamWriter(stream))
			{
				writer.Write(json);
				writer.Flush();
				stream.Flush(true);
			}

			File.Move(temporary, path, true);
		}

		static FileStream OpenAppend(string path) =>
			new(path, FileMode.Append, FileAccess.Write, FileShare.Read);

		static void WriteDurable(StreamWriter writer, FileStream stream, string line)
		{
			writer.WriteLine(line);
			writer.Flush();
			stream.Flush(true);
		}

		static string Csv(string value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
		static string Json(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

		public void Dispose()
		{
			progressWriter.Dispose();
			resultsWriter.Dispose();
		}
	}
}
