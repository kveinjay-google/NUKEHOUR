// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available to you under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.RA2.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class V3TerminalDiveActivityTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2"))))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
		}

		static MiniYamlNode ActorRule(string relativePath, string actorName)
		{
			var path = Path.Combine(RepositoryRoot(), relativePath);
			return MiniYaml.FromFile(path).Single(node => node.Key == actorName);
		}

		static MiniYamlNode TraitRule(MiniYamlNode actor, string traitName)
		{
			return actor.Value.Nodes.Single(node => node.Key == traitName);
		}

		static BallisticMissileInfo MissileInfo(string relativePath, string actorName)
		{
			var info = new BallisticMissileInfo();
			FieldLoader.Load(info, TraitRule(ActorRule(relativePath, actorName), "BallisticMissile").Value);
			return info;
		}

		static IReadOnlyDictionary<string, string> TraitFields(MiniYamlNode actor, string traitName)
		{
			return TraitRule(actor, traitName).Value.Nodes.ToDictionary(node => node.Key, node => node.Value.Value);
		}

		[Test]
		public void V3RocketResolvesConfirmedTerminalDiveParameters()
		{
			// The actor owns this trait directly. Loading it through FieldLoader combines the
			// structured rule values with BallisticMissileInfo's effective field defaults.
			var info = MissileInfo("mods/ra2/rules/soviet-vehicles.yaml", "v3rocket");

			Assert.Multiple(() =>
			{
				Assert.That(info.CreateAngle, Is.EqualTo(new WAngle(64)));
				Assert.That(info.PrepareTick, Is.EqualTo(25));
				Assert.That(info.LaunchAngle, Is.EqualTo(new WAngle(160)));
				Assert.That(info.Speed, Is.EqualTo(new WDist(250)));
				Assert.That(info.BeginCruiseAltitude, Is.EqualTo(WDist.FromCells(3)));
				Assert.That(info.BeginHitRange, Is.EqualTo(WDist.FromCells(4)));
				Assert.That(info.TurnSpeed, Is.EqualTo(new WAngle(25)));
				Assert.That(info.TerminalDive, Is.True);
				Assert.That(info.TerminalTurnSpeed, Is.EqualTo(new WAngle(48)));
				Assert.That(info.HitAcceleration, Is.EqualTo(new WDist(20)));
				Assert.That(info.MaxHitSpeed, Is.EqualTo(new WDist(350)));
				Assert.That(info.LazyCurve, Is.False);
			});
		}

		[TestCase("mods/ra2/rules/yuri-naval.yaml", "bmisl")]
		[TestCase("mods/ra2/rules/soviet-naval.yaml", "dmisl")]
		public void NavalMissilesRetainTheirLegacyBallisticConfiguration(string relativePath, string actorName)
		{
			var info = MissileInfo(relativePath, actorName);

			Assert.Multiple(() =>
			{
				Assert.That(info.TerminalDive, Is.False);
				Assert.That(info.TerminalTurnSpeed, Is.EqualTo(WAngle.Zero));
				Assert.That(info.MaxHitSpeed, Is.EqualTo(WDist.Zero));
				Assert.That(info.LazyCurve, Is.False);
				Assert.That(info.WithoutCruise, Is.False);
				Assert.That(info.LaunchAngle, Is.EqualTo(new WAngle(192)));
				Assert.That(info.Speed, Is.EqualTo(new WDist(250)));
				Assert.That(info.LaunchAcceleration, Is.EqualTo(new WDist(16)));
				Assert.That(info.HitAcceleration, Is.EqualTo(new WDist(30)));
			});
		}

		[Test]
		public void V3LandingStateSelectsNormalEliteAndAirborneWeaponsExplicitly()
		{
			var actor = ActorRule("mods/ra2/rules/soviet-vehicles.yaml", "v3rocket");
			var missile = TraitFields(actor, "BallisticMissile");
			var normal = TraitFields(actor, "ExplodesForMaster");
			var elite = TraitFields(actor, "ExplodesForMaster@Elite");
			var airborne = TraitFields(actor, "ExplodesForMaster@Airborne");

			Assert.Multiple(() =>
			{
				Assert.That(missile["AirborneCondition"], Is.EqualTo("airborne"));
				Assert.That(normal["Weapon"], Is.EqualTo("V3Weapon"));
				Assert.That(normal["RequiresCondition"], Is.EqualTo("!airborne && !rank-elite"));
				Assert.That(elite["Weapon"], Is.EqualTo("V3WeaponE"));
				Assert.That(elite["RequiresCondition"], Is.EqualTo("!airborne && rank-elite"));
				Assert.That(airborne["Weapon"], Is.EqualTo("UnitExplode"));
				Assert.That(airborne["RequiresCondition"], Is.EqualTo("airborne"));
			});
		}

		[Test]
		public void TerminalDiveActivitySourceContractFreezesTargetAndQueuesKillAfterExactLanding()
		{
			// There is no stable World fixture for this RA2 actor in the current test project.
			// This narrowly guards the activity integration order; policy motion is executed in
			// V3TerminalDivePolicyTest and runtime World behavior remains a smoke-test boundary.
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "Activities", "BallisticMissileFly.cs"));
			var constructorStart = source.IndexOf("public BallisticMissileFly(", StringComparison.Ordinal);
			var constructorEnd = source.IndexOf("protected override void OnFirstRun", constructorStart, StringComparison.Ordinal);
			var constructor = source.Substring(constructorStart, constructorEnd - constructorStart);
			var terminalStart = source.IndexOf("bool TickTerminalDive(Actor self)", StringComparison.Ordinal);
			var terminalEnd = source.IndexOf("public override bool Tick(Actor self)", terminalStart, StringComparison.Ordinal);
			var terminal = source.Substring(terminalStart, terminalEnd - terminalStart);

			var policyTick = terminal.IndexOf(
				"BallisticMissileTerminalDivePolicy.Tick(bmInfo, initPos, targetPos, terminalDiveState)",
				StringComparison.Ordinal);
			var doneGuard = terminal.IndexOf(
				"if (terminalDiveState.Phase != TerminalDivePhase.Done)", StringComparison.Ordinal);
			var exactLanding = terminal.IndexOf("bm.SetPosition(self, targetPos);", doneGuard, StringComparison.Ordinal);
			var queuedKill = terminal.IndexOf(
				"Queue(new CallFunc(() => self.Kill(self, bm.Info.DamageTypes)));", exactLanding, StringComparison.Ordinal);

			Assert.Multiple(() =>
			{
				StringAssert.Contains("targetPos = t.CenterPosition;", constructor,
					"The activity must freeze the target coordinate when it is constructed.");
				Assert.That(policyTick, Is.GreaterThanOrEqualTo(0));
				Assert.That(doneGuard, Is.GreaterThan(policyTick));
				Assert.That(exactLanding, Is.GreaterThan(doneGuard));
				Assert.That(queuedKill, Is.GreaterThan(exactLanding),
					"SetPosition must clear airborne before ExplodesForMaster chooses the death weapon.");
				StringAssert.Contains("yield return Target.FromPos(targetPos);", source);
			});
		}
	}
}
