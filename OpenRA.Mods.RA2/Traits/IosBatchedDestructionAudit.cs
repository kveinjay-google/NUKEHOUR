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
	sealed class IosBatchedDestructionAuditSession : IDisposable
	{
		sealed class BatchEntry
		{
			public DestructionAuditCase Case { get; }
			public Actor Actor { get; set; }
			public DestructionAuditPhase Result { get; set; }
			public string Detail { get; set; } = string.Empty;

			public BatchEntry(DestructionAuditCase auditCase)
			{
				Case = auditCase;
			}
		}

		readonly IosDestructionAuditConfiguration config;
		readonly Queue<DestructionAuditBatch> pendingBatches;
		readonly int initialBatchCount;
		readonly int totalCases;
		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly HashSet<uint> baselineActors;
		readonly CPos[] spawnCandidates;
		readonly HashSet<string> finalizedCases = new(StringComparer.Ordinal);
		readonly string outputDirectory;
		readonly FileStream progressStream;
		readonly StreamWriter progressWriter;
		readonly FileStream resultsStream;
		readonly StreamWriter resultsWriter;
		DestructionAuditBatch currentBatch;
		List<BatchEntry> currentEntries;
		DestructionAuditStateMachine state;
		int batchesCompleted;
		int passed;
		int skipped;
		int failed;
		bool completed;

		IosBatchedDestructionAuditSession(World world, WorldRenderer worldRenderer,
			IosDestructionAuditConfiguration config, IEnumerable<DestructionAuditActor> actors)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			this.config = config;
			var batches = config.BuildBatches(actors).ToArray();
			pendingBatches = new Queue<DestructionAuditBatch>(batches);
			initialBatchCount = batches.Length;
			totalCases = batches.Sum(b => b.Cases.Count);
			baselineActors = world.Actors.Select(a => a.ActorID).ToHashSet();
			spawnCandidates = world.Map.AllCells.OrderBy(c => c.Y).ThenBy(c => c.X).ToArray();
			outputDirectory = Path.Combine(Platform.SupportDir, "Logs", "DestructionAudit", config.RunId);
			Directory.CreateDirectory(outputDirectory);

			progressStream = OpenAppend(Path.Combine(outputDirectory, "progress.log"));
			progressWriter = new StreamWriter(progressStream) { AutoFlush = true };
			resultsStream = OpenAppend(Path.Combine(outputDirectory, "results.csv"));
			resultsWriter = new StreamWriter(resultsStream) { AutoFlush = true };
			if (resultsStream.Length == 0)
				WriteDurable(resultsWriter, resultsStream,
					"case,actor,death_type,iteration,batch,category,attempt,result,detail");

			var categoryCounts = batches
				.SelectMany(b => b.Cases)
				.GroupBy(c => c.Category)
				.Select(g => $"{g.Key}={g.Count()}");
			RecordProgress("audit-start",
				$"cases={totalCases} batches={initialBatchCount} {string.Join(' ', categoryCounts)}");
			WriteSummary(false);
		}

		public static IosBatchedDestructionAuditSession TryCreate(World world, WorldRenderer worldRenderer,
			IosDestructionAuditConfiguration config)
		{
			if (!config.Enabled || !IosDestructionAuditConfiguration.ShouldStartInWorld(world.Type))
				return null;

			var actors = world.Map.Rules.Actors
				.Where(kv => config.ShouldIncludeActor(kv.Key) &&
					IosDestructionAuditConfiguration.IsAuditable(kv.Key, kv.Value))
				.Select(kv => new DestructionAuditActor(
					kv.Key, IosDestructionAuditConfiguration.Classify(kv.Value)));
			return new IosBatchedDestructionAuditSession(world, worldRenderer, config, actors);
		}

		public void Tick()
		{
			if (completed)
				return;

			if (currentBatch == null)
			{
				if (pendingBatches.Count == 0)
				{
					Complete();
					return;
				}

				BeginBatch(pendingBatches.Dequeue());
				return;
			}

			if (state.Phase == DestructionAuditPhase.SettleBeforeKill ||
				state.Phase == DestructionAuditPhase.SettleEffects)
				state.Tick();

			if (state.Phase == DestructionAuditPhase.Kill)
				KillBatch();
			else if (state.Phase == DestructionAuditPhase.Cleanup)
				CleanupBatch();

			if (state.IsTerminal)
				FinishBatch();
		}

		void BeginBatch(DestructionAuditBatch batch)
		{
			currentBatch = batch;
			currentEntries = batch.Cases.Select(c => new BatchEntry(c)).ToList();
			state = new DestructionAuditStateMachine(
				config.PreKillSettleTicks, config.SettleTicks, config.TimeoutTicks);
			var members = string.Join(',', batch.Cases.Select(c => c.ActorName));
			RecordProgress("batch-start",
				$"{batch.Id} category={batch.Category} death={batch.DeathType} count={batch.Cases.Count} members={members}");

			for (var i = 0; i < currentEntries.Count; i++)
				Spawn(currentEntries[i], batchesCompleted * 20 + i);

			var spawned = currentEntries.Where(e => e.Actor != null).Select(e => e.Actor).ToArray();
			if (spawned.Length > 0)
				worldRenderer.Viewport.Center(spawned);
			state.Spawned();
			RecordProgress("batch-spawned", $"{batch.Id} spawned={spawned.Length}");
		}

		void Spawn(BatchEntry entry, int caseOrdinal)
		{
			try
			{
				var actorInfo = world.Map.Rules.Actors[entry.Case.ActorName];
				var owner = ResolveOwner(actorInfo);
				if (owner == null)
				{
					entry.Result = DestructionAuditPhase.Skipped;
					entry.Detail = "no compatible owner";
					return;
				}

				if (!TryFindSpawnCell(actorInfo, caseOrdinal, out var cell, out var subCell))
				{
					entry.Result = DestructionAuditPhase.Skipped;
					entry.Detail = "no compatible terrain";
					return;
				}

				using var spawnTrace = DiagnosticTrace.Scope(DiagnosticSubsystem.Simulation,
					$"DestructionAudit.BatchSpawn.{entry.Case.ActorName}");
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

				entry.Actor = world.CreateActor(entry.Case.ActorName, initializers);
				RecordProgress("member-spawned",
					$"{currentBatch.Id} case={entry.Case.Id} actor={entry.Actor.ActorID} cell={cell}");
			}
			catch (Exception e)
			{
				entry.Result = DestructionAuditPhase.Failed;
				entry.Detail = $"spawn exception: {e.GetType().Name}: {e.Message}";
			}
		}

		void KillBatch()
		{
			RecordProgress("batch-kill-enter", currentBatch.Id);
			foreach (var entry in currentEntries.Where(e => e.Result == default))
			{
				if (entry.Actor == null || entry.Actor.Disposed || entry.Actor.WillDispose)
				{
					entry.Result = DestructionAuditPhase.Failed;
					entry.Detail = "actor disappeared before kill";
					continue;
				}

				try
				{
					using var killTrace = DiagnosticTrace.Scope(DiagnosticSubsystem.Simulation,
						$"DestructionAudit.BatchKill.{entry.Case.ActorName}.{entry.Case.DeathType}");
					killTrace.Progress(entry.Actor.ActorID, entry.Case.Iteration);
					entry.Actor.Kill(world.WorldActor, new BitSet<DamageType>(entry.Case.DeathType));
					RecordProgress("member-kill-returned",
						$"{currentBatch.Id} case={entry.Case.Id} actor={entry.Actor.ActorID}");
				}
				catch (Exception e)
				{
					entry.Result = DestructionAuditPhase.Failed;
					entry.Detail = $"kill exception: {e.GetType().Name}: {e.Message}";
				}
			}

			state.KillReturned();
			RecordProgress("batch-kill-returned", currentBatch.Id);
		}

		void CleanupBatch()
		{
			RecordProgress("batch-cleanup-enter", currentBatch.Id);
			foreach (var actor in world.Actors.Where(a => !baselineActors.Contains(a.ActorID)).ToArray())
				if (!actor.Disposed && !actor.WillDispose)
					actor.Dispose();

			foreach (var entry in currentEntries.Where(e => e.Result == default))
				entry.Result = DestructionAuditPhase.Passed;
			state.CleanedUp();
			RecordProgress("batch-cleanup-returned", currentBatch.Id);
		}

		void FinishBatch()
		{
			var refinement = currentBatch.Id.StartsWith("refine-", StringComparison.Ordinal);
			foreach (var entry in currentEntries)
			{
				var result = entry.Result == default ? DestructionAuditPhase.Failed : entry.Result;
				WriteDurable(resultsWriter, resultsStream,
					$"{Csv(entry.Case.Id)},{Csv(entry.Case.ActorName)},{Csv(entry.Case.DeathType)}," +
					$"{entry.Case.Iteration},{Csv(currentBatch.Id)},{entry.Case.Category}," +
					$"{(refinement ? 2 : 1)},{result.ToString().ToUpperInvariant()},{Csv(entry.Detail)}");

				if (result == DestructionAuditPhase.Failed && !refinement && currentBatch.Cases.Count > 1)
				{
					pendingBatches.Enqueue(new DestructionAuditBatch(
						$"refine-{entry.Case.Id}", entry.Case.Category,
						entry.Case.DeathType, new[] { entry.Case }));
					RecordProgress("refinement-queued", entry.Case.Id);
					continue;
				}

				FinalizeCase(entry.Case.Id, result);
			}

			batchesCompleted++;
			RecordProgress("batch-finished",
				$"{currentBatch.Id} passed={currentEntries.Count(e => e.Result == DestructionAuditPhase.Passed)} " +
				$"skipped={currentEntries.Count(e => e.Result == DestructionAuditPhase.Skipped)} " +
				$"failed={currentEntries.Count(e => e.Result == DestructionAuditPhase.Failed)}");
			currentBatch = null;
			currentEntries = null;
			state = null;
			WriteSummary(false);
		}

		void FinalizeCase(string id, DestructionAuditPhase result)
		{
			if (!finalizedCases.Add(id))
				return;

			if (result == DestructionAuditPhase.Passed)
				passed++;
			else if (result == DestructionAuditPhase.Skipped)
				skipped++;
			else
				failed++;
		}

		void Complete()
		{
			completed = true;
			RecordProgress("audit-complete",
				$"passed={passed} skipped={skipped} failed={failed} batches={batchesCompleted}");
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

		bool TryFindSpawnCell(ActorInfo actorInfo, int caseOrdinal, out CPos cell, out SubCell subCell)
		{
			var building = actorInfo.TraitInfoOrDefault<BuildingInfo>();
			var positionable = actorInfo.TraitInfoOrDefault<IPositionableInfo>();
			var aircraft = actorInfo.TraitInfoOrDefault<AircraftInfo>();
			var start = IosDestructionAuditConfiguration.SpawnCandidateStartIndex(
				caseOrdinal, spawnCandidates.Length);
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
				$"DestructionAudit.{phase}", finalizedCases.Count, totalCases);
			WriteDurable(progressWriter, progressStream,
				$"{DateTime.UtcNow:O}\t{phase}\t{detail}");
		}

		void WriteSummary(bool complete)
		{
			var current = currentBatch?.Id ?? string.Empty;
			var json = "{\n" +
				$"  \"complete\": {complete.ToString().ToLowerInvariant()},\n" +
				$"  \"total\": {totalCases},\n" +
				$"  \"completed\": {finalizedCases.Count},\n" +
				$"  \"passed\": {passed},\n" +
				$"  \"skipped\": {skipped},\n" +
				$"  \"failed\": {failed},\n" +
				$"  \"initialBatches\": {initialBatchCount},\n" +
				$"  \"batchesCompleted\": {batchesCompleted},\n" +
				$"  \"pendingBatches\": {pendingBatches.Count},\n" +
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
