// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available to you under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using OpenRA.Mods.RA2.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class V3RuntimeAuditTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2"))))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
		}

		static string LegacyFixturePath() => Path.Combine(
			RepositoryRoot(), "docs", "testing", "fixtures", "v3-legacy-8db6419", "trajectory.csv");

		static BallisticMissileInfo MissileInfo(string relativePath, string actorName)
		{
			var actor = MiniYaml.FromFile(Path.Combine(RepositoryRoot(), relativePath))
				.Single(node => node.Key == actorName);
			var trait = actor.Value.Nodes.Single(node => node.Key == "BallisticMissile");
			var info = new BallisticMissileInfo();
			FieldLoader.Load(info, trait.Value);
			return info;
		}

		static string FileSha256(string path)
		{
			using var stream = File.OpenRead(path);
			using var hash = SHA256.Create();
			return Convert.ToHexString(hash.ComputeHash(stream)).ToLowerInvariant();
		}

		[Test]
		public void RuntimeAuditIsDisabledUnlessExplicitlyEnabled()
		{
			var config = V3RuntimeAuditConfiguration.Parse(_ => null);

			Assert.That(config.Enabled, Is.False);
		}

		[Test]
		public void RuntimeAuditBoundsTimeoutAndSanitizesRunId()
		{
			var values = new Dictionary<string, string>
			{
				["OPENRA_V3_RUNTIME_AUDIT"] = "true",
				["OPENRA_V3_RUNTIME_AUDIT_TIMEOUT_TICKS"] = "99999",
				["OPENRA_V3_RUNTIME_AUDIT_RUN_ID"] = "../../run 42?",
				["OPENRA_V3_RUNTIME_AUDIT_LEGACY_FIXTURE"] = LegacyFixturePath()
			};
			var config = V3RuntimeAuditConfiguration.Parse(
				name => values.TryGetValue(name, out var value) ? value : null);

			Assert.Multiple(() =>
			{
				Assert.That(config.Enabled, Is.True);
				Assert.That(config.TimeoutTicks, Is.EqualTo(2500));
				Assert.That(config.RunId, Is.EqualTo("run42"));
				Assert.That(config.LegacyFixturePath, Is.EqualTo(LegacyFixturePath()));
			});
		}

		[Test]
		public void IndependentLegacyFixtureIsTraceableAndContainsEveryActivityTick()
		{
			var root = RepositoryRoot();
			var fixturePath = LegacyFixturePath();
			var provenancePath = Path.Combine(
				root, "docs", "testing", "fixtures", "v3-legacy-8db6419", "provenance.json");
			var provenance = File.ReadAllText(provenancePath);
			var samples = V3RuntimeAuditEvidence.LoadLegacyFixture(fixturePath);

			Assert.Multiple(() =>
			{
				Assert.That(FileSha256(fixturePath),
					Is.EqualTo("20023b6509d90776209a68443ccb3f143fc224de2dc8b9cba4869e9c588ddaf1"));
				StringAssert.Contains("8db64195c8f97423a25f17b998c21f86c2abef15", provenance);
				StringAssert.Contains("18a29cffd68958274f33015b4e345f7d6c095f59ae0e3f1f245e8df5b7882909", provenance);
				StringAssert.Contains("3ddfef949e070c6715888f9b47e8acf04f261727c65fd90b2b850a650772339b", provenance);
				StringAssert.Contains("Commit 8db6419 contains dmisl but no bmisl actor", provenance);
				Assert.That(samples, Has.Count.EqualTo(82));
				Assert.That(samples[0].Phase, Is.EqualTo("Prepare"));
				Assert.That(samples[0].State, Is.EqualTo("ticks=2;speed=0;lazy=0"));
				Assert.That(samples.Select(s => s.Phase), Does.Contain("Launch"));
				Assert.That(samples.Select(s => s.Phase), Does.Contain("Cruise"));
				Assert.That(samples.Select(s => s.Phase), Does.Contain("Hit"));
				Assert.That(samples[^1].Phase, Is.EqualTo("none"));
				Assert.That(samples[^1].State, Is.EqualTo("none"));
			});
		}

		[Test]
		public void BmislMayReuseDmislFixtureOnlyBehindResolvedMotionRuleEquality()
		{
			var bmisl = MissileInfo("mods/ra2/rules/yuri-naval.yaml", "bmisl");
			var dmisl = MissileInfo("mods/ra2/rules/soviet-naval.yaml", "dmisl");
			var v3 = MissileInfo("mods/ra2/rules/soviet-vehicles.yaml", "v3rocket");

			Assert.Multiple(() =>
			{
				Assert.That(V3RuntimeAuditEvidence.SameLegacyMotionConfiguration(bmisl, dmisl), Is.True);
				Assert.That(V3RuntimeAuditEvidence.SameLegacyMotionConfiguration(bmisl, v3), Is.False);
			});
		}

		[Test]
		public void RuntimeCasesCoverEveryConfirmedV3RangeAndBothLegacyMissiles()
		{
			var cases = V3RuntimeAuditConfiguration.CreateForTests().BuildCases().ToArray();
			var v3 = cases.Where(c => c.MissileActor == "v3rocket").ToArray();

			Assert.Multiple(() =>
			{
				Assert.That(v3.Length, Is.EqualTo(6));
				Assert.That(v3.Select(c => c.RangeCells), Is.EqualTo(new[] { 5, 5, 10, 10, 18, 18 }));
				Assert.That(v3.Where(c => !c.Elite).Select(c => c.ExpectedWeapon),
					Is.All.EqualTo("V3Weapon"));
				Assert.That(v3.Where(c => c.Elite).Select(c => c.ExpectedWeapon),
					Is.All.EqualTo("V3WeaponE"));
				Assert.That(v3, Has.All.Matches<V3RuntimeAuditCase>(c => c.UseLauncher));
				Assert.That(cases.Single(c => c.MissileActor == "bmisl").ExpectedWeapon,
					Is.EqualTo("CruiseWeapon"));
				Assert.That(cases.Single(c => c.MissileActor == "dmisl").ExpectedWeapon,
					Is.EqualTo("DredWeapon"));
				Assert.That(cases.Where(c => c.MissileActor is "bmisl" or "dmisl"),
					Has.All.Matches<V3RuntimeAuditCase>(c => !c.UseLauncher));
			});
		}

		[Test]
		public void RuntimeStateRequiresLaunchLandingAndRemovalBeforePassing()
		{
			var state = new V3RuntimeAuditStateMachine(timeoutTicks: 20);

			Assert.That(state.Phase, Is.EqualTo(V3RuntimeAuditPhase.Prepare));
			state.ScenarioReady();
			Assert.That(state.Phase, Is.EqualTo(V3RuntimeAuditPhase.WaitForLaunch));
			state.MissileLaunched();
			Assert.That(state.Phase, Is.EqualTo(V3RuntimeAuditPhase.InFlight));
			state.Landed();
			Assert.That(state.Phase, Is.EqualTo(V3RuntimeAuditPhase.WaitForRemoval));
			state.Removed();
			Assert.That(state.Phase, Is.EqualTo(V3RuntimeAuditPhase.Passed));
		}

		[Test]
		public void RuntimeStateTimesOutWithTheActiveBoundary()
		{
			var state = new V3RuntimeAuditStateMachine(timeoutTicks: 2);
			state.ScenarioReady();
			state.MissileLaunched();

			state.Tick();
			state.Tick();
			state.Tick();

			Assert.That(state.Phase, Is.EqualTo(V3RuntimeAuditPhase.Failed));
			StringAssert.Contains("InFlight", state.Detail);
		}

		[Test]
		public void ExplosionTraceMustMatchActorWeaponPhaseAndCaseBoundary()
		{
			var snapshot = new DiagnosticTraceSnapshot
			{
				Available = true,
				DroppedEvents = 0,
				Events = new[]
				{
					new DiagnosticTraceEvent(9, 0, 1, DiagnosticSubsystem.Simulation,
						DiagnosticTracePhase.Progress, 1, 0, "Death.ExplosionWeapon.Master.V3Weapon", 42, 0),
					new DiagnosticTraceEvent(11, 0, 1, DiagnosticSubsystem.Simulation,
						DiagnosticTracePhase.Begin, 2, 0, "Death.ExplosionWeapon.Master.V3WeaponE", 0, 0),
					new DiagnosticTraceEvent(12, 0, 1, DiagnosticSubsystem.Simulation,
						DiagnosticTracePhase.Progress, 2, 0, "Death.ExplosionWeapon.Master.V3WeaponE", 42, 0),
					new DiagnosticTraceEvent(13, 0, 1, DiagnosticSubsystem.Simulation,
						DiagnosticTracePhase.Progress, 3, 0, "Death.ExplosionWeapon.Master.V3WeaponE", 43, 0)
				},
				ActiveScopes = Array.Empty<DiagnosticActiveScope>(),
				Heartbeats = new Dictionary<DiagnosticSubsystem, DiagnosticHeartbeat>()
			};

			Assert.Multiple(() =>
			{
				Assert.That(V3RuntimeAuditEvidence.HasExplosionWeaponTrace(
					snapshot, 42, "V3WeaponE", afterSequence: 10), Is.True);
				Assert.That(V3RuntimeAuditEvidence.HasExplosionWeaponTrace(
					snapshot, 42, "V3Weapon", afterSequence: 10), Is.False);
				Assert.That(V3RuntimeAuditEvidence.HasExplosionWeaponTrace(
					snapshot, 42, "V3WeaponE", afterSequence: 12), Is.False);
				Assert.That(V3RuntimeAuditEvidence.HasExplosionWeaponTrace(
					snapshot, 44, "V3WeaponE", afterSequence: 10), Is.False);
			});
		}

		[Test]
		public void LegacyTrajectoryComparisonUsesPositionOrientationPhaseAndActivityState()
		{
			var first = new[]
			{
				new V3RuntimeAuditTraceSample(new WPos(1, 2, 3), new WAngle(4), new WAngle(5), "Launch", "ticks=1"),
				new V3RuntimeAuditTraceSample(new WPos(6, 7, 8), new WAngle(9), new WAngle(10), "Cruise", "ticks=2")
			};
			var same = new[]
			{
				new V3RuntimeAuditTraceSample(new WPos(1, 2, 3), new WAngle(4), new WAngle(5), "Launch", "ticks=1"),
				new V3RuntimeAuditTraceSample(new WPos(6, 7, 8), new WAngle(9), new WAngle(10), "Cruise", "ticks=2")
			};
			var changedState = new[]
			{
				new V3RuntimeAuditTraceSample(new WPos(1, 2, 3), new WAngle(4), new WAngle(5), "Launch", "ticks=1"),
				new V3RuntimeAuditTraceSample(new WPos(6, 7, 8), new WAngle(9), new WAngle(10), "Hit", "ticks=2")
			};

			Assert.That(V3RuntimeAuditEvidence.SameTrajectory(first, same), Is.True);
			Assert.That(V3RuntimeAuditEvidence.SameTrajectory(first, changedState), Is.False);
			Assert.That(first[0] == same[0], Is.True);
			Assert.That(first[1] != changedState[1], Is.True);
		}

		[Test]
		public void LegacyGoldenDigestRejectsTwoIdenticallyWrongTrajectories()
		{
			var identicallyWrong = new[]
			{
				new V3RuntimeAuditTraceSample(new WPos(1, 2, 3), new WAngle(4), new WAngle(5), "Launch", "ticks=1"),
				new V3RuntimeAuditTraceSample(new WPos(6, 7, 8), new WAngle(9), new WAngle(10), "Hit", "ticks=2")
			};
			var golden = V3RuntimeAuditEvidence.LoadLegacyFixture(LegacyFixturePath());

			Assert.Multiple(() =>
			{
				Assert.That(V3RuntimeAuditEvidence.MatchesLegacyGolden(
					"bmisl", identicallyWrong, golden, out _, out _), Is.False);
				Assert.That(V3RuntimeAuditEvidence.MatchesLegacyGolden(
					"dmisl", identicallyWrong, golden, out _, out _), Is.False);
				Assert.That(V3RuntimeAuditEvidence.SameTrajectory(identicallyWrong, identicallyWrong), Is.True,
					"Mutual equality alone would falsely accept two trajectories with the same regression.");
			});
		}

		[Test]
		public void LegacyGoldenRejectsPhaseRegressionWithIdenticalMotionSamples()
		{
			var golden = V3RuntimeAuditEvidence.LoadLegacyFixture(LegacyFixturePath()).ToArray();
			var changed = golden.ToArray();
			var sample = changed[50];
			changed[50] = new V3RuntimeAuditTraceSample(
				sample.Position, sample.Pitch, sample.Yaw, "Hit", sample.State);

			Assert.Multiple(() =>
			{
				Assert.That(changed.Select(s => (s.Position, s.Pitch, s.Yaw)),
					Is.EqualTo(golden.Select(s => (s.Position, s.Pitch, s.Yaw))));
				Assert.That(V3RuntimeAuditEvidence.SameTrajectory(golden, changed), Is.False);
				Assert.That(V3RuntimeAuditEvidence.MatchesLegacyGolden(
					"dmisl", changed, golden, out _, out _), Is.False);
			});
		}

		[Test]
		public void RuntimeHarnessUsesRealLauncherWorldAndDiagnosticBoundaries()
		{
			var root = RepositoryRoot();
			var audit = File.ReadAllText(Path.Combine(
				root, "OpenRA.Mods.RA2", "Traits", "V3RuntimeAudit.cs"));
			var benchmark = File.ReadAllText(Path.Combine(
				root, "OpenRA.Mods.RA2", "Traits", "IosHighUnitBenchmark.cs"));

			StringAssert.Contains("world.CreateActor(\"v3\"", audit);
			StringAssert.Contains("RelationshipWith(p) == PlayerRelationship.Enemy", audit,
				"The target owner must be hostile but non-AI creeps are valid and remain stationary.");
			StringAssert.Contains("world.IssueOrder(new Order(\"Attack\"", audit);
			StringAssert.Contains("Trait<MissileSpawnerMaster>()", audit);
			StringAssert.Contains("Trait<BallisticMissile>()", audit);
			StringAssert.Contains("SetCenterPosition", audit);
			StringAssert.Contains("TraitsImplementing<ExplodesForMaster>()", audit);
			StringAssert.Contains("DiagnosticTrace.Snapshot()", audit);
			StringAssert.Contains("MatchesLegacyGolden", audit);
			StringAssert.Contains("LegacyAuditPhase", audit);
			StringAssert.Contains("phase,state", audit);
			StringAssert.Contains("expected_trajectory_digest,actual_trajectory_digest", audit);
			StringAssert.Contains("V3RuntimeAuditConfiguration.Parse", benchmark);
			StringAssert.Contains("V3RuntimeAuditSession.TryCreate", benchmark);
			StringAssert.Contains("v3RuntimeAudit.Tick()", benchmark);
		}

		[Test]
		public void DesktopRunnerHasBoundedPidScopedLifecycleAndStrictEvidenceGate()
		{
			var scriptPath = Path.Combine(
				RepositoryRoot(), "packaging", "run_v3_runtime_audit.sh");
			Assert.That(File.Exists(scriptPath), Is.True, "The desktop runtime audit runner is missing.");
			var script = File.ReadAllText(scriptPath);

			StringAssert.Contains("game_pid=$!", script);
			StringAssert.Contains("ps -p \"$game_pid\" -o command=", script);
			StringAssert.Contains("OpenRA.dll", script);
			StringAssert.Contains("kill -TERM \"$game_pid\"", script);
			StringAssert.Contains("kill -KILL \"$game_pid\"", script);
			StringAssert.Contains("wait_for_game_exit \"$term_grace_seconds\"", script);
			StringAssert.Contains("wait_for_game_exit \"$reap_grace_seconds\"", script);
			StringAssert.Contains("OPENRA_V3_RUNTIME_AUDIT=true", script);
			StringAssert.Contains("OPENRA_V3_RUNTIME_AUDIT_LEGACY_FIXTURE", script);
			StringAssert.Contains("docs/testing/fixtures/v3-legacy-8db6419/trajectory.csv", script);
			StringAssert.Contains("docs/testing/fixtures/v3-legacy-8db6419/provenance.json", script);
			StringAssert.Contains("Game.LaunchInto=skirmish", script);
			StringAssert.Contains("slot_bot Multi1 0 test", script,
				"The audit world must contain a real hostile player for the target actor.");
			StringAssert.Contains("option fog False", script,
				"The 10c and 18c actor targets must remain visible beyond the V3's 7c sight range.");
			StringAssert.Contains("\"complete\": true", script);
			StringAssert.Contains("\"failed\": 0", script);
			StringAssert.Contains("\"passed\": 9", script);
			StringAssert.Contains("\"total\": 9", script);
			StringAssert.Contains("\"completed\": 9", script);
			StringAssert.DoesNotContain("pkill", script);
			StringAssert.DoesNotContain("killall", script);
			StringAssert.DoesNotContain("xcrun", script);
		}
	}
}
