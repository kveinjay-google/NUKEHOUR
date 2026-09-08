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
using System.Security.Cryptography;
using System.Text;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.RA2.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	public sealed class V3RuntimeAuditCase
	{
		public string Id { get; }
		public string MissileActor { get; }
		public int RangeCells { get; }
		public bool Elite { get; }
		public bool UseLauncher { get; }
		public string ExpectedWeapon { get; }

		public V3RuntimeAuditCase(string id, string missileActor, int rangeCells,
			bool elite, bool useLauncher, string expectedWeapon)
		{
			Id = id;
			MissileActor = missileActor;
			RangeCells = rangeCells;
			Elite = elite;
			UseLauncher = useLauncher;
			ExpectedWeapon = expectedWeapon;
		}
	}

	public sealed class V3RuntimeAuditConfiguration
	{
		const int DefaultTimeoutTicks = 900;
		const int MinimumTimeoutTicks = 100;
		const int MaximumTimeoutTicks = 2500;

		public bool Enabled { get; private init; }
		public int TimeoutTicks { get; private init; }
		public string RunId { get; private init; }
		public string LegacyFixturePath { get; private init; }

		public static V3RuntimeAuditConfiguration Parse(Func<string, string> read)
		{
			if (!bool.TryParse(read("OPENRA_V3_RUNTIME_AUDIT"), out var enabled) || !enabled)
				return new V3RuntimeAuditConfiguration
				{
					Enabled = false,
					TimeoutTicks = DefaultTimeoutTicks,
					RunId = string.Empty,
					LegacyFixturePath = string.Empty
				};

			var timeout = int.TryParse(read("OPENRA_V3_RUNTIME_AUDIT_TIMEOUT_TICKS"),
				NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTimeout)
				? parsedTimeout.Clamp(MinimumTimeoutTicks, MaximumTimeoutTicks)
				: DefaultTimeoutTicks;
			var runId = SafeRunId(read("OPENRA_V3_RUNTIME_AUDIT_RUN_ID"));
			if (runId.Length == 0)
				runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
			var fixturePath = read("OPENRA_V3_RUNTIME_AUDIT_LEGACY_FIXTURE");
			if (string.IsNullOrWhiteSpace(fixturePath) || !Path.IsPathRooted(fixturePath))
				throw new InvalidOperationException(
					"OPENRA_V3_RUNTIME_AUDIT_LEGACY_FIXTURE must be an absolute path.");

			fixturePath = Path.GetFullPath(fixturePath);
			if (!File.Exists(fixturePath))
				throw new FileNotFoundException("The independent legacy trajectory fixture is missing.", fixturePath);

			return new V3RuntimeAuditConfiguration
			{
				Enabled = true,
				TimeoutTicks = timeout,
				RunId = runId,
				LegacyFixturePath = fixturePath
			};
		}

		public static V3RuntimeAuditConfiguration CreateForTests() =>
			new() { Enabled = true, TimeoutTicks = DefaultTimeoutTicks, RunId = "test" };

		public IEnumerable<V3RuntimeAuditCase> BuildCases()
		{
			foreach (var range in new[] { 5, 10, 18 })
			{
				yield return new V3RuntimeAuditCase(
					$"v3-normal-{range:D2}c", "v3rocket", range, false, true, "V3Weapon");
				yield return new V3RuntimeAuditCase(
					$"v3-elite-{range:D2}c", "v3rocket", range, true, true, "V3WeaponE");
			}

			yield return new V3RuntimeAuditCase(
				"bmisl-legacy-10c", "bmisl", 10, false, false, "CruiseWeapon");
			yield return new V3RuntimeAuditCase(
				"dmisl-legacy-10c", "dmisl", 10, false, false, "DredWeapon");
		}

		public static bool ShouldStartInWorld(WorldType worldType) => worldType == WorldType.Regular;

		static string SafeRunId(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return string.Empty;

			return new string(value.Trim()
				.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
				.Take(80)
				.ToArray());
		}
	}

	public enum V3RuntimeAuditPhase
	{
		Prepare,
		WaitForLaunch,
		InFlight,
		WaitForRemoval,
		Passed,
		Failed
	}

	public sealed class V3RuntimeAuditStateMachine
	{
		readonly int timeoutTicks;

		public V3RuntimeAuditPhase Phase { get; private set; } = V3RuntimeAuditPhase.Prepare;
		public string Detail { get; private set; } = string.Empty;
		public int Ticks { get; private set; }
		public bool IsTerminal => Phase is V3RuntimeAuditPhase.Passed or V3RuntimeAuditPhase.Failed;

		public V3RuntimeAuditStateMachine(int timeoutTicks)
		{
			this.timeoutTicks = timeoutTicks;
		}

		public void ScenarioReady() => Transition(V3RuntimeAuditPhase.WaitForLaunch);
		public void MissileLaunched() => Transition(V3RuntimeAuditPhase.InFlight);
		public void Landed() => Transition(V3RuntimeAuditPhase.WaitForRemoval);
		public void Removed() => Transition(V3RuntimeAuditPhase.Passed);

		public void Fail(string detail)
		{
			Detail = detail;
			Transition(V3RuntimeAuditPhase.Failed);
		}

		public void Tick()
		{
			if (IsTerminal)
				return;

			if (++Ticks > timeoutTicks)
				Fail($"Timed out in {Phase} after {Ticks} ticks");
		}

		void Transition(V3RuntimeAuditPhase phase)
		{
			Phase = phase;
		}
	}

	public readonly struct V3RuntimeAuditTraceSample : IEquatable<V3RuntimeAuditTraceSample>
	{
		public readonly WPos Position;
		public readonly WAngle Pitch;
		public readonly WAngle Yaw;
		public readonly string Phase;
		public readonly string State;

		public V3RuntimeAuditTraceSample(
			WPos position, WAngle pitch, WAngle yaw, string phase = "", string state = "")
		{
			Position = position;
			Pitch = pitch;
			Yaw = yaw;
			Phase = phase ?? string.Empty;
			State = state ?? string.Empty;
		}

		public bool Equals(V3RuntimeAuditTraceSample other) =>
			Position == other.Position && Pitch == other.Pitch && Yaw == other.Yaw &&
			string.Equals(Phase, other.Phase, StringComparison.Ordinal) &&
			string.Equals(State, other.State, StringComparison.Ordinal);

		public override bool Equals(object obj) => obj is V3RuntimeAuditTraceSample other && Equals(other);
		public override int GetHashCode() => HashCode.Combine(Position, Pitch, Yaw, Phase, State);
		public static bool operator ==(V3RuntimeAuditTraceSample left, V3RuntimeAuditTraceSample right) => left.Equals(right);
		public static bool operator !=(V3RuntimeAuditTraceSample left, V3RuntimeAuditTraceSample right) => !left.Equals(right);
	}

	public static class V3RuntimeAuditEvidence
	{
		public static bool HasExplosionWeaponTrace(DiagnosticTraceSnapshot snapshot,
			uint actorId, string weaponName, long afterSequence)
		{
			if (snapshot == null || !snapshot.Available || snapshot.Events == null)
				return false;

			var expectedName = "Death.ExplosionWeapon.Master." + weaponName;
			return snapshot.Events.Any(e => e.Sequence > afterSequence &&
				e.Phase == DiagnosticTracePhase.Progress && e.Name == expectedName && e.Arg0 == actorId);
		}

		public static bool SameTrajectory(IEnumerable<V3RuntimeAuditTraceSample> first,
			IEnumerable<V3RuntimeAuditTraceSample> second) => first.SequenceEqual(second);

		public static IReadOnlyList<V3RuntimeAuditTraceSample> LoadLegacyFixture(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				throw new ArgumentException("A legacy trajectory fixture path is required.", nameof(path));

			using var reader = new StreamReader(path, Encoding.UTF8, true);
			var header = reader.ReadLine();
			if (header != "actor,sample,tick,x,y,z,pitch,yaw,phase,state")
				throw new InvalidDataException("Unexpected legacy trajectory fixture header.");

			var samples = new List<V3RuntimeAuditTraceSample>();
			string line;
			while ((line = reader.ReadLine()) != null)
			{
				if (line.Length == 0)
					continue;

				var fields = ParseCsvLine(line);
				if (fields.Count != 10 || fields[0] != "dmisl")
					throw new InvalidDataException($"Invalid legacy trajectory fixture row {samples.Count + 2}.");

				var sampleIndex = ParseInvariantInt(fields[1], "sample", samples.Count + 2);
				ParseInvariantInt(fields[2], "tick", samples.Count + 2);
				if (sampleIndex != samples.Count)
					throw new InvalidDataException(
						$"Legacy trajectory sample sequence broke at row {samples.Count + 2}.");

				samples.Add(new V3RuntimeAuditTraceSample(
					new WPos(
						ParseInvariantInt(fields[3], "x", samples.Count + 2),
						ParseInvariantInt(fields[4], "y", samples.Count + 2),
						ParseInvariantInt(fields[5], "z", samples.Count + 2)),
					new WAngle(ParseInvariantInt(fields[6], "pitch", samples.Count + 2)),
					new WAngle(ParseInvariantInt(fields[7], "yaw", samples.Count + 2)),
					fields[8],
					fields[9]));
			}

			if (samples.Count == 0)
				throw new InvalidDataException("The legacy trajectory fixture is empty.");

			return samples;
		}

		public static bool SameLegacyMotionConfiguration(BallisticMissileInfo first, BallisticMissileInfo second)
		{
			return first != null && second != null &&
				first.CreateAngle == second.CreateAngle &&
				first.PrepareTick == second.PrepareTick &&
				first.BeginCruiseAltitude == second.BeginCruiseAltitude &&
				first.TurnSpeed == second.TurnSpeed &&
				first.BeginHitRange == second.BeginHitRange &&
				first.ExplosionRange == second.ExplosionRange &&
				first.LaunchAcceleration == second.LaunchAcceleration &&
				first.HitAcceleration == second.HitAcceleration &&
				first.TerminalDive == second.TerminalDive &&
				first.TerminalTurnSpeed == second.TerminalTurnSpeed &&
				first.MaxHitSpeed == second.MaxHitSpeed &&
				first.LazyCurve == second.LazyCurve &&
				first.WithoutCruise == second.WithoutCruise &&
				first.Speed == second.Speed &&
				first.LaunchAngle == second.LaunchAngle &&
				first.MinAirborneAltitude == second.MinAirborneAltitude;
		}

		public static string NormalizedTrajectoryDigest(IEnumerable<V3RuntimeAuditTraceSample> samples)
		{
			var trace = samples.ToArray();
			using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			if (trace.Length > 0)
			{
				var origin = trace[0].Position;
				foreach (var sample in trace)
				{
					var offset = sample.Position - origin;
					var canonical = FormattableString.Invariant(
						$"{offset.X},{offset.Y},{offset.Z},{sample.Pitch.Angle},{sample.Yaw.Angle},{sample.Phase},{sample.State}\n");
					var bytes = Encoding.UTF8.GetBytes(canonical);
					hash.AppendData(bytes, 0, bytes.Length);
				}
			}

			return BitConverter.ToString(hash.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
		}

		public static bool MatchesLegacyGolden(
			string actorName,
			IEnumerable<V3RuntimeAuditTraceSample> samples,
			IEnumerable<V3RuntimeAuditTraceSample> golden,
			out string expectedDigest,
			out string actualDigest)
		{
			var trace = samples.ToArray();
			var expected = golden.ToArray();
			expectedDigest = NormalizedTrajectoryDigest(expected);
			actualDigest = NormalizedTrajectoryDigest(trace);
			if (actorName is not ("bmisl" or "dmisl"))
				return false;

			return SameTrajectory(expected, trace) &&
				string.Equals(expectedDigest, actualDigest, StringComparison.Ordinal);
		}

		static int ParseInvariantInt(string value, string field, int row)
		{
			if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
				return parsed;

			throw new InvalidDataException($"Invalid {field} at legacy trajectory fixture row {row}.");
		}

		static IReadOnlyList<string> ParseCsvLine(string line)
		{
			var fields = new List<string>();
			var value = new StringBuilder();
			var quoted = false;
			for (var i = 0; i < line.Length; i++)
			{
				var character = line[i];
				if (character == '"')
				{
					if (quoted && i + 1 < line.Length && line[i + 1] == '"')
					{
						value.Append('"');
						i++;
					}
					else
						quoted = !quoted;
				}
				else if (character == ',' && !quoted)
				{
					fields.Add(value.ToString());
					value.Clear();
				}
				else
					value.Append(character);
			}

			if (quoted)
				throw new InvalidDataException("Unterminated quoted field in legacy trajectory fixture.");

			fields.Add(value.ToString());
			return fields;
		}
	}

	sealed class V3RuntimeAuditSession : IDisposable
	{
		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly V3RuntimeAuditConfiguration config;
		readonly V3RuntimeAuditCase[] cases;
		readonly string outputDirectory;
		readonly FileStream progressStream;
		readonly StreamWriter progressWriter;
		readonly FileStream resultsStream;
		readonly StreamWriter resultsWriter;
		readonly FileStream trajectoryStream;
		readonly StreamWriter trajectoryWriter;
		readonly Dictionary<string, V3RuntimeAuditTraceSample[]> legacyTraces = new(StringComparer.Ordinal);
		readonly IReadOnlyList<V3RuntimeAuditTraceSample> legacyGolden;

		V3RuntimeAuditCase currentCase;
		V3RuntimeAuditStateMachine state;
		Actor launcher;
		Actor targetActor;
		Actor missile;
		BallisticMissile ballisticMissile;
		WPos expectedTarget;
		WPos movedTarget;
		long traceSequenceStart;
		List<V3RuntimeAuditTraceSample> trajectory;
		string pendingFailure;
		string expectedTrajectoryDigest;
		string actualTrajectoryDigest;
		bool scenarioReady;
		bool targetMoved;
		bool frozenTargetObserved;
		bool landedAtExactTarget;
		bool weaponGateObserved;
		bool completed;
		int currentIndex;
		int passed;
		int failed;

		V3RuntimeAuditSession(World world, WorldRenderer worldRenderer,
			V3RuntimeAuditConfiguration config)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			this.config = config;
			cases = config.BuildCases().ToArray();
			legacyGolden = V3RuntimeAuditEvidence.LoadLegacyFixture(config.LegacyFixturePath);
			var boomerInfo = world.Map.Rules.Actors["bmisl"].TraitInfo<BallisticMissileInfo>();
			var dreadnoughtInfo = world.Map.Rules.Actors["dmisl"].TraitInfo<BallisticMissileInfo>();
			if (!V3RuntimeAuditEvidence.SameLegacyMotionConfiguration(boomerInfo, dreadnoughtInfo))
				throw new InvalidOperationException(
					"bmisl may reuse the 8db6419 dmisl fixture only while their resolved motion fields match.");

			outputDirectory = Path.Combine(Platform.SupportDir, "Logs", "V3RuntimeAudit", config.RunId);
			Directory.CreateDirectory(outputDirectory);

			progressStream = OpenAppend(Path.Combine(outputDirectory, "progress.log"));
			progressWriter = new StreamWriter(progressStream) { AutoFlush = true };
			resultsStream = OpenAppend(Path.Combine(outputDirectory, "results.csv"));
			resultsWriter = new StreamWriter(resultsStream) { AutoFlush = true };
			trajectoryStream = OpenAppend(Path.Combine(outputDirectory, "trajectory.csv"));
			trajectoryWriter = new StreamWriter(trajectoryStream) { AutoFlush = true };
			if (resultsStream.Length == 0)
				WriteDurable(resultsWriter, resultsStream,
					"case,missile,range_cells,elite,result,ticks,frozen_target,target_moved,exact_landing,weapon_gate," +
					"detail,expected_trajectory_digest,actual_trajectory_digest");
			if (trajectoryStream.Length == 0)
				WriteDurable(trajectoryWriter, trajectoryStream, "case,tick,x,y,z,pitch,yaw,phase,state");

			RecordProgress("audit-start", $"cases={cases.Length}");
			WriteSummary(false, string.Empty);
		}

		public static V3RuntimeAuditSession TryCreate(World world, WorldRenderer worldRenderer,
			V3RuntimeAuditConfiguration config)
		{
			if (!config.Enabled || !V3RuntimeAuditConfiguration.ShouldStartInWorld(world.Type))
				return null;

			return new V3RuntimeAuditSession(world, worldRenderer, config);
		}

		public void Tick()
		{
			if (completed)
				return;

			if (currentCase == null)
			{
				if (currentIndex >= cases.Length)
				{
					Complete();
					return;
				}

				BeginCase(cases[currentIndex]);
				return;
			}

			state.Tick();
			if (pendingFailure != null)
			{
				state.Fail(pendingFailure);
				pendingFailure = null;
			}

			if (state.Phase == V3RuntimeAuditPhase.Failed)
			{
				FinishCase();
				return;
			}

			switch (state.Phase)
			{
				case V3RuntimeAuditPhase.Prepare:
					if (scenarioReady)
						state.ScenarioReady();
					break;
				case V3RuntimeAuditPhase.WaitForLaunch:
					ObserveLaunch();
					break;
				case V3RuntimeAuditPhase.InFlight:
					ObserveFlight();
					break;
				case V3RuntimeAuditPhase.WaitForRemoval:
					ObserveRemoval();
					break;
				case V3RuntimeAuditPhase.Passed:
					FinishCase();
					break;
			}
		}

		void BeginCase(V3RuntimeAuditCase auditCase)
		{
			currentCase = auditCase;
			state = new V3RuntimeAuditStateMachine(config.TimeoutTicks);
			trajectory = new List<V3RuntimeAuditTraceSample>();
			traceSequenceStart = LatestTraceSequence();
			scenarioReady = false;
			targetMoved = !auditCase.UseLauncher;
			frozenTargetObserved = false;
			landedAtExactTarget = false;
			weaponGateObserved = false;
			pendingFailure = null;
			expectedTrajectoryDigest = string.Empty;
			actualTrajectoryDigest = string.Empty;
			RecordProgress("case-start", auditCase.Id);

			world.AddFrameEndTask(_ =>
			{
				try
				{
					if (auditCase.UseLauncher)
						CreateV3Scenario(auditCase);
					else
						CreateLegacyScenario(auditCase);

					scenarioReady = true;
				}
				catch (Exception e)
				{
					pendingFailure = $"scenario exception: {e.GetType().Name}: {e.Message}";
				}
			});
		}

		void CreateV3Scenario(V3RuntimeAuditCase auditCase)
		{
			var localPlayer = world.LocalPlayer ?? world.Players.FirstOrDefault(p => p.Playable && !p.NonCombatant);
			var enemyPlayer = world.Players.FirstOrDefault(p => localPlayer != null &&
				localPlayer.RelationshipWith(p) == PlayerRelationship.Enemy);
			if (localPlayer == null || enemyPlayer == null)
				throw new InvalidOperationException("A local player and an enemy player are required.");

			if (!TryFindScenarioCells(auditCase.RangeCells, out var launchCell, out var targetCell, out var moveCell))
				throw new InvalidOperationException($"No passable {auditCase.RangeCells}c V3 audit lane exists.");

			targetActor = world.CreateActor("e1", Initializers("e1", enemyPlayer, targetCell,
				WAngle.Zero));
			launcher = world.CreateActor("v3", Initializers("v3", localPlayer, launchCell,
				(world.Map.CenterOfCell(targetCell) - world.Map.CenterOfCell(launchCell)).Yaw));
			if (auditCase.Elite)
			{
				var experience = launcher.Trait<GainsExperience>();
				experience.GiveLevels(experience.MaxLevel, true);
			}

			var spawner = launcher.Trait<MissileSpawnerMaster>();
			missile = spawner.SlaveEntries.Single().Actor;
			expectedTarget = targetActor.CenterPosition;
			movedTarget = world.Map.CenterOfCell(moveCell);
			worldRenderer.Viewport.Center(new[] { launcher, targetActor });
			world.IssueOrder(new Order("Attack", launcher, Target.FromActor(targetActor), false));
			RecordProgress("attack-issued",
				$"{auditCase.Id} launcher={launcher.ActorID} missile={missile.ActorID} target={targetActor.ActorID}");
		}

		void CreateLegacyScenario(V3RuntimeAuditCase auditCase)
		{
			var owner = world.LocalPlayer ?? world.Players.FirstOrDefault(p => p.Playable && !p.NonCombatant);
			if (owner == null)
				throw new InvalidOperationException("A playable owner is required.");

			if (!TryFindScenarioCells(auditCase.RangeCells, out var launchCell, out var targetCell, out _))
				throw new InvalidOperationException($"No passable {auditCase.RangeCells}c legacy audit lane exists.");

			var launcherPosition = world.Map.CenterOfCell(launchCell);
			expectedTarget = world.Map.CenterOfCell(targetCell);
			var startPosition = launcherPosition + new WVec(0, 640, 300);
			missile = world.CreateActor(false, auditCase.MissileActor, new TypeDictionary
			{
				new OwnerInit(owner),
				new CenterPositionInit(startPosition),
				new FacingInit((expectedTarget - startPosition).Yaw),
				new FactionInit(owner.Faction.InternalName)
			});
			ballisticMissile = missile.Trait<BallisticMissile>();
			ballisticMissile.Target = Target.FromPos(expectedTarget);
			world.Add(missile);
			worldRenderer.Viewport.Center(new[] { missile });
			RecordProgress("legacy-added", $"{auditCase.Id} missile={missile.ActorID}");
		}

		void ObserveLaunch()
		{
			if (missile == null || missile.Disposed || missile.WillDispose)
			{
				state.Fail("missile disappeared before entering the World");
				return;
			}

			if (!missile.IsInWorld)
			{
				if (currentCase.UseLauncher && state.Ticks % 100 == 0)
					RecordProgress("wait-launch",
						$"{currentCase.Id} launcher={Format(launcher.CenterPosition)} " +
						$"target={Format(targetActor.CenterPosition)} " +
						$"distance={(targetActor.CenterPosition - launcher.CenterPosition).Length} " +
						$"activity={launcher.CurrentActivity?.GetType().Name ?? "none"} " +
						$"fog={world.FogObscures(targetActor.CenterPosition)} " +
						$"shroud={world.ShroudObscures(targetActor.CenterPosition)}");

				return;
			}

			ballisticMissile ??= missile.Trait<BallisticMissile>();
			var frozen = ballisticMissile.Target.CenterPosition;
			if (frozen != expectedTarget)
			{
				state.Fail($"launch target was not frozen at {expectedTarget}; observed {frozen}");
				return;
			}

			frozenTargetObserved = true;
			if (currentCase.UseLauncher)
			{
				targetActor.Trait<IPositionable>().SetCenterPosition(targetActor, movedTarget);
				targetMoved = targetActor.CenterPosition == movedTarget && movedTarget != expectedTarget;
				if (!targetMoved)
				{
					state.Fail("the target actor did not move after launch");
					return;
				}
			}
			else
				RecordTrajectorySample();

			state.MissileLaunched();
			RecordProgress("missile-launched",
				$"{currentCase.Id} missile={missile.ActorID} frozen={Format(expectedTarget)} moved={Format(movedTarget)}");
		}

		void ObserveFlight()
		{
			if (missile == null || missile.Disposed || !missile.IsInWorld)
			{
				state.Fail("missile was removed before exact landing was observed");
				return;
			}

			if (ballisticMissile.Target.CenterPosition != expectedTarget)
			{
				state.Fail("BallisticMissile.Target changed after launch");
				return;
			}

			var activityTargets = missile.CurrentActivity?.GetTargets(missile).ToArray() ?? Array.Empty<Target>();
			if (activityTargets.Length > 0 && activityTargets.Any(t => t.CenterPosition != expectedTarget))
			{
				state.Fail("BallisticMissileFly no longer exposes the frozen target");
				return;
			}

			RecordTrajectorySample();

			if (missile.CenterPosition != expectedTarget)
				return;

			landedAtExactTarget = true;
			var enabledWeapons = missile.TraitsImplementing<ExplodesForMaster>()
				.Where(t => !t.IsTraitDisabled)
				.Select(t => t.Info.Weapon)
				.ToArray();
			if (enabledWeapons.Length != 1 || enabledWeapons[0] != currentCase.ExpectedWeapon)
			{
				state.Fail($"landing weapon gate was [{string.Join(',', enabledWeapons)}], " +
					$"expected {currentCase.ExpectedWeapon}");
				return;
			}

			weaponGateObserved = true;
			state.Landed();
			RecordProgress("exact-landing",
				$"{currentCase.Id} missile={missile.ActorID} weapon={enabledWeapons[0]}");
		}

		void ObserveRemoval()
		{
			if (missile != null && missile.IsInWorld && !missile.Disposed)
				return;

			var snapshot = DiagnosticTrace.Snapshot();
			if (!V3RuntimeAuditEvidence.HasExplosionWeaponTrace(
				snapshot, missile.ActorID, currentCase.ExpectedWeapon, traceSequenceStart))
			{
				state.Fail($"missing actor-specific explosion trace for {currentCase.ExpectedWeapon}");
				return;
			}

			if (currentCase.MissileActor is "bmisl" or "dmisl" &&
				!V3RuntimeAuditEvidence.MatchesLegacyGolden(currentCase.MissileActor, trajectory, legacyGolden,
					out expectedTrajectoryDigest, out actualTrajectoryDigest))
			{
				state.Fail($"legacy trajectory golden mismatch: expected={expectedTrajectoryDigest}, " +
					$"actual={actualTrajectoryDigest}, samples={trajectory.Count}");
				FinishCase();
				return;
			}

			state.Removed();
			FinishCase();
		}

		void RecordTrajectorySample()
		{
			var activity = missile.CurrentActivity as BallisticMissileFly;
			var phase = activity?.LegacyAuditPhase ?? missile.CurrentActivity?.GetType().Name ?? "none";
			var activityState = activity?.LegacyAuditState ?? "none";
			var sample = new V3RuntimeAuditTraceSample(
				missile.CenterPosition, ballisticMissile.Pitch, ballisticMissile.Facing, phase, activityState);
			trajectory.Add(sample);
			WriteDurable(trajectoryWriter, trajectoryStream,
				$"{Csv(currentCase.Id)},{state.Ticks},{sample.Position.X},{sample.Position.Y},{sample.Position.Z}," +
				$"{sample.Pitch.Angle},{sample.Yaw.Angle},{Csv(sample.Phase)},{Csv(sample.State)}");
		}

		void FinishCase()
		{
			if (state.Phase == V3RuntimeAuditPhase.Passed)
			{
				passed++;
				if (currentCase.MissileActor is "bmisl" or "dmisl")
					legacyTraces[currentCase.MissileActor] = trajectory.ToArray();
			}
			else
				failed++;

			var result = state.Phase.ToString().ToUpperInvariant();
			WriteDurable(resultsWriter, resultsStream,
				$"{Csv(currentCase.Id)},{Csv(currentCase.MissileActor)},{currentCase.RangeCells}," +
				$"{currentCase.Elite.ToString().ToLowerInvariant()},{result},{state.Ticks}," +
				$"{frozenTargetObserved.ToString().ToLowerInvariant()}," +
				$"{targetMoved.ToString().ToLowerInvariant()}," +
				$"{landedAtExactTarget.ToString().ToLowerInvariant()}," +
				$"{weaponGateObserved.ToString().ToLowerInvariant()},{Csv(state.Detail)}," +
				$"{Csv(expectedTrajectoryDigest)},{Csv(actualTrajectoryDigest)}");
			RecordProgress("case-finished", $"{currentCase.Id} result={result} detail={state.Detail}");

			DisposeActor(launcher);
			DisposeActor(targetActor);
			DisposeActor(missile);
			currentIndex++;
			WriteSummary(false, currentCase.Id);
			currentCase = null;
			state = null;
			launcher = null;
			targetActor = null;
			missile = null;
			ballisticMissile = null;
		}

		void Complete()
		{
			var hasBoomerTrace = legacyTraces.TryGetValue("bmisl", out var boomer);
			var hasDreadnoughtTrace = legacyTraces.TryGetValue("dmisl", out var dreadnought);
			var legacyComparable = hasBoomerTrace && hasDreadnoughtTrace;
			var legacyEqual = legacyComparable && V3RuntimeAuditEvidence.SameTrajectory(boomer, dreadnought);
			if (!legacyEqual)
				failed++;
			else
				passed++;

			WriteDurable(resultsWriter, resultsStream,
				$"legacy-trace-comparison,legacy,10,false,{(legacyEqual ? "PASSED" : "FAILED")},0," +
				$"true,true,true,true,{Csv(legacyComparable ? "bmisl/dmisl trace equality" : "missing legacy trace")},,");
			completed = true;
			RecordProgress("audit-complete", $"passed={passed} failed={failed}");
			WriteSummary(true, string.Empty);
			Dispose();
			Game.Exit();
		}

		bool TryFindScenarioCells(int rangeCells, out CPos launchCell, out CPos targetCell, out CPos moveCell)
		{
			var launchInfo = world.Map.Rules.Actors["v3"].TraitInfo<IPositionableInfo>();
			var targetInfo = world.Map.Rules.Actors["e1"].TraitInfo<IPositionableInfo>();
			var directions = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
			foreach (var origin in world.Map.AllCells.OrderBy(c => c.Y).ThenBy(c => c.X))
			{
				if (!CanEnter(launchInfo, origin))
					continue;

				foreach (var (dx, dy) in directions)
				{
					var target = new CPos(origin.X + dx * rangeCells, origin.Y + dy * rangeCells);
					var moved = new CPos(target.X - dy * 2, target.Y + dx * 2);
					if (!world.Map.Contains(target) || !world.Map.Contains(moved) ||
						!CanEnter(targetInfo, target) || !CanEnter(targetInfo, moved))
						continue;

					launchCell = origin;
					targetCell = target;
					moveCell = moved;
					return true;
				}
			}

			launchCell = default;
			targetCell = default;
			moveCell = default;
			return false;
		}

		bool CanEnter(IPositionableInfo positionable, CPos cell)
		{
			var subCell = positionable.SharesCell ? world.ActorMap.FreeSubCell(cell) : SubCell.FullCell;
			return subCell != SubCell.Invalid && positionable.CanEnterCell(
				world, null, cell, subCell, check: BlockedByActor.All);
		}

		TypeDictionary Initializers(string actorName, Player owner, CPos cell, WAngle facing)
		{
			var positionable = world.Map.Rules.Actors[actorName].TraitInfo<IPositionableInfo>();
			var subCell = positionable.SharesCell ? world.ActorMap.FreeSubCell(cell) : SubCell.FullCell;
			return new TypeDictionary
			{
				new OwnerInit(owner),
				new LocationInit(cell),
				new SubCellInit(subCell),
				new FacingInit(facing),
				new FactionInit(owner.Faction.InternalName),
				new SpawnedByMapInit()
			};
		}

		static long LatestTraceSequence()
		{
			var snapshot = DiagnosticTrace.Snapshot();
			return snapshot.Available && snapshot.Events.Length > 0 ? snapshot.Events[^1].Sequence : 0;
		}

		void RecordProgress(string phase, string detail)
		{
			DiagnosticTrace.Instant(DiagnosticSubsystem.Simulation,
				"V3RuntimeAudit." + phase, currentIndex, cases.Length);
			WriteDurable(progressWriter, progressStream,
				$"{DateTime.UtcNow:O}\t{phase}\t{detail}");
		}

		void WriteSummary(bool complete, string lastCase)
		{
			var current = currentCase?.Id ?? string.Empty;
			var json = "{\n" +
				$"  \"complete\": {complete.ToString().ToLowerInvariant()},\n" +
				$"  \"total\": {cases.Length + 1},\n" +
				$"  \"completed\": {currentIndex + (complete ? 1 : 0)},\n" +
				$"  \"passed\": {passed},\n" +
				$"  \"failed\": {failed},\n" +
				$"  \"current\": \"{Json(current)}\",\n" +
				$"  \"last_case\": \"{Json(lastCase)}\"\n" +
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

		static void DisposeActor(Actor actor)
		{
			if (actor != null && !actor.Disposed && !actor.WillDispose)
				actor.Dispose();
		}

		static FileStream OpenAppend(string path) =>
			new(path, FileMode.Append, FileAccess.Write, FileShare.Read);

		static void WriteDurable(StreamWriter writer, FileStream stream, string line)
		{
			writer.WriteLine(line);
			writer.Flush();
			stream.Flush(true);
		}

		static string Format(WPos position) => $"{position.X}:{position.Y}:{position.Z}";
		static string Csv(string value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
		static string Json(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

		public void Dispose()
		{
			progressWriter.Dispose();
			resultsWriter.Dispose();
			trajectoryWriter.Dispose();
		}
	}
}
