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
using OpenRA.GameRules;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class V3KirovImpactRulesTest
	{
		sealed class ImpactExpectation
		{
			public readonly string Core;
			public readonly string Main;
			public readonly string LandSound;
			public readonly string Ring;
			public readonly string Plume;
			public readonly string Splash;
			public readonly int ShakeDuration;
			public readonly int ShakeIntensity;

			public ImpactExpectation(string core, string main, string landSound, string ring,
				string plume, string splash, int shakeDuration, int shakeIntensity)
			{
				Core = core;
				Main = main;
				LandSound = landSound;
				Ring = ring;
				Plume = plume;
				Splash = splash;
				ShakeDuration = shakeDuration;
				ShakeIntensity = shakeIntensity;
			}
		}

		static readonly IReadOnlyDictionary<string, ImpactExpectation> ImpactExpectations =
			new Dictionary<string, ImpactExpectation>
			{
				{
					"V3Weapon", new("nc_core_small", "large_clsn", "gexp14a.wav",
						"nc_ring_small", "nc_debris_small", "large_watersplash", 5, 1)
				},
				{
					"V3WeaponE", new("nc_core_large", "terrorist_explosion", "gexpapoa.wav",
						"nc_ring_large", "nc_debris_large", "huge_watersplash", 8, 2)
				},
				{
					"BlimpBomb", new("nc_core_large", "verylarge_clsn", "gexp14a.wav",
						"nc_ring_large", "nc_debris_large", "huge_watersplash", 6, 2)
				},
				{
					"BlimpBombE", new("nc_core_tesla", "kirovtesla", "gexp14a.wav",
						"nc_ring_tesla", "nc_debris_large", "huge_watersplash", 8, 3)
				},
			};

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null)
			{
				var gitPath = Path.Combine(directory.FullName, ".git");
				if ((Directory.Exists(gitPath) || File.Exists(gitPath)) &&
					Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2")))
					return directory.FullName;

				directory = directory.Parent;
			}

			throw new InvalidOperationException("Could not locate the repository root.");
		}

		static IReadOnlyList<MiniYamlNode> RawWeaponFile(string relativePath)
		{
			return MiniYaml.FromFile(Path.Combine(RepositoryRoot(), relativePath));
		}

		static IReadOnlyDictionary<string, MiniYamlNode> ResolvedWeapons()
		{
			var root = RepositoryRoot();
			var manifest = MiniYaml.FromFile(Path.Combine(root, "mods", "ra2", "mod.yaml"));
			var paths = manifest.Single(node => node.Key == "Weapons").Value.Nodes
				.Select(node => node.Key)
				.Select(path => path.StartsWith("ra2|", StringComparison.Ordinal) ?
					Path.Combine(root, "mods", "ra2", path[4..]) :
					throw new InvalidOperationException($"Unexpected RA2 weapon path `{path}`."));

			var merged = MiniYaml.Merge(paths.Select(path => MiniYaml.FromFile(path)));
			return merged.ToDictionary(node => node.Key, StringComparer.Ordinal);
		}

		static MiniYamlNode Weapon(string name)
		{
			return ResolvedWeapons()[name];
		}

		static MiniYamlNode Warhead(MiniYamlNode weapon, string key)
		{
			return weapon.Value.Nodes.Single(node => node.Key == key);
		}

		static SpreadDamageWarhead DamageWarhead(string weaponName)
		{
			var node = Weapon(weaponName).Value.Nodes.Single(node => node.Key == "Warhead@1Dam");
			Assert.That(node.Value.Value, Is.EqualTo("SpreadDamage"));
			var warhead = new SpreadDamageWarhead();
			FieldLoader.Load(warhead, node.Value);
			return warhead;
		}

		static CreateEffectWarhead Effect(MiniYamlNode node)
		{
			Assert.That(node.Value.Value, Is.EqualTo("CreateEffect"));
			var effect = new CreateEffectWarhead();
			FieldLoader.Load(effect, node.Value);
			return effect;
		}

		static ShakeScreenWarhead Shake(MiniYamlNode node)
		{
			Assert.That(node.Value.Value, Is.EqualTo("ShakeScreen"));
			var shake = new ShakeScreenWarhead();
			FieldLoader.Load(shake, node.Value);
			return shake;
		}

		static T WeaponField<T>(MiniYamlNode weapon, string key, T defaultValue)
		{
			var node = weapon.Value.Nodes.SingleOrDefault(candidate => candidate.Key == key);
			return node == null ? defaultValue : FieldLoader.GetValue<T>(key, node.Value.Value);
		}

		static IReadOnlyDictionary<string, int> V3Versus()
		{
			return new Dictionary<string, int>
			{
				{ "None", 100 }, { "Flak", 100 }, { "Plate", 80 }, { "Light", 100 },
				{ "Medium", 70 }, { "Heavy", 70 }, { "Wood", 100 }, { "Steel", 100 },
				{ "Concrete", 90 }, { "Drone", 100 }, { "Rocket", 0 },
			};
		}

		static IReadOnlyDictionary<string, int> KirovVersus()
		{
			return new Dictionary<string, int>
			{
				{ "None", 100 }, { "Flak", 100 }, { "Plate", 100 }, { "Light", 70 },
				{ "Medium", 35 }, { "Heavy", 35 }, { "Wood", 85 }, { "Steel", 75 },
				{ "Concrete", 50 }, { "Drone", 100 }, { "Rocket", 100 },
			};
		}

		[TestCase("V3Weapon", 200, 768, false)]
		[TestCase("V3WeaponE", 200, 1024, false)]
		[TestCase("DredWeapon", 200, 1024, false)]
		[TestCase("DredWeaponE", 300, 1024, false)]
		[TestCase("BlimpBomb", 250, 400, true)]
		[TestCase("BlimpBombE", 250, 400, true)]
		public void DamageBalanceMatchesThePreChangeResolvedSnapshot(
			string weaponName, int damage, int spread, bool kirov)
		{
			var warhead = DamageWarhead(weaponName);
			var expectedVersus = kirov ? KirovVersus() : V3Versus();
			var expectedDamageTypes = kirov ?
				new[] { "Prone70Percent", "TriggerProne", "BulletDeath" } :
				new[] { "Prone70Percent", "TriggerProne", "ExplosionDeath" };
			var expectedTargets = kirov ?
				new[] { "Building", "Ground", "Water" } : new[] { "Ground", "Water" };

			Assert.Multiple(() =>
			{
				Assert.That(warhead.Damage, Is.EqualTo(damage));
				Assert.That(warhead.Spread, Is.EqualTo(new WDist(spread)));
				Assert.That(warhead.Falloff, Is.EqualTo(new[] { 100, 37, 14, 5, 0 }));
				Assert.That(warhead.Range, Is.Null);
				Assert.That(warhead.Versus, Is.EquivalentTo(expectedVersus));
				Assert.That(warhead.DamageTypes, Is.EquivalentTo(expectedDamageTypes));
				Assert.That(warhead.ValidTargets, Is.EquivalentTo(expectedTargets));
			});
		}

		[TestCase("V3Weapon", false)]
		[TestCase("V3WeaponE", false)]
		[TestCase("DredWeapon", false)]
		[TestCase("DredWeaponE", false)]
		[TestCase("BlimpBomb", true)]
		[TestCase("BlimpBombE", true)]
		public void RateAndProjectileFieldsMatchThePreChangeResolvedSnapshot(string weaponName, bool kirov)
		{
			var weapon = Weapon(weaponName);
			Assert.Multiple(() =>
			{
				Assert.That(WeaponField(weapon, "ReloadDelay", 1), Is.EqualTo(kirov ? 50 : 1));
				Assert.That(WeaponField(weapon, "Range", WDist.Zero), Is.EqualTo(kirov ? new WDist(256) : WDist.Zero));
				Assert.That(WeaponField(weapon, "MinRange", WDist.Zero), Is.EqualTo(kirov ? new WDist(1) : WDist.Zero));
				Assert.That(WeaponField(weapon, "Burst", 1), Is.EqualTo(1));
				Assert.That(WeaponField(weapon, "BurstDelays", new[] { 5 }), Is.EqualTo(new[] { 5 }));
				Assert.That(WeaponField<string[]>(weapon, "Report", null),
					kirov ? Is.EqualTo(new[] { "vkiratta.wav" }) : Is.Null);
				Assert.That(weapon.Value.Nodes.SingleOrDefault(node => node.Key == "Projectile")?.Value.Value,
					kirov ? Is.EqualTo("GravityBomb") : Is.Null);
			});
		}

		[Test]
		public void V3FamilyWeaponsInheritOnlyTheSharedDamageTemplate()
		{
			var raw = RawWeaponFile("mods/ra2/weapons/explosions.yaml");
			Assert.That(raw, Has.Some.Matches<MiniYamlNode>(node => node.Key == "^V3FamilyDamage"));

			foreach (var weaponName in new[] { "V3Weapon", "V3WeaponE", "DredWeapon", "DredWeaponE" })
			{
				var weapon = raw.Single(node => node.Key == weaponName);
				var inherits = weapon.Value.Nodes
					.Where(node => node.Key == "Inherits" || node.Key.StartsWith("Inherits@", StringComparison.Ordinal))
					.Select(node => node.Value.Value);
				Assert.That(inherits, Is.EqualTo(new[] { "^V3FamilyDamage" }), weaponName);
			}
		}

		[TestCase("V3Weapon")]
		[TestCase("V3WeaponE")]
		[TestCase("BlimpBomb")]
		[TestCase("BlimpBombE")]
		public void LayeredImpactsResolveTheConfirmedTerrainMatrix(string weaponName)
		{
			var weapon = Weapon(weaponName);
			var expected = ImpactExpectations[weaponName];
			var core = Effect(Warhead(weapon, "Warhead@20Core"));
			var mainLand = Effect(Warhead(weapon, "Warhead@21MainLand"));
			var mainWater = Effect(Warhead(weapon, "Warhead@22MainWater"));
			var ring = Effect(Warhead(weapon, "Warhead@23Ring"));
			var plume = Effect(Warhead(weapon, "Warhead@24PlumeLand"));
			var splash = Effect(Warhead(weapon, "Warhead@25SplashWater"));
			var shake = Shake(Warhead(weapon, "Warhead@26Shake"));

			Assert.Multiple(() =>
			{
				Assert.That(core.Image, Is.EqualTo("nc-impact"));
				Assert.That(core.Explosions, Is.EqualTo(new[] { expected.Core }));
				Assert.That(core.Delay, Is.Zero);
				Assert.That(core.ValidTargets, Is.EquivalentTo(new[] { "Ground", "Water", "Air" }));

				Assert.That(mainLand.Explosions, Is.EqualTo(new[] { expected.Main }));
				Assert.That(mainLand.ImpactSounds, Is.EqualTo(new[] { expected.LandSound }));
				Assert.That(mainLand.Delay, Is.Zero);
				Assert.That(mainLand.ValidTargets, Is.EquivalentTo(new[] { "Ground", "Air" }));

				Assert.That(mainWater.Explosions, Is.EqualTo(new[] { expected.Main }));
				Assert.That(mainWater.ImpactSounds, Is.Empty);
				Assert.That(mainWater.Delay, Is.Zero);
				Assert.That(mainWater.ValidTargets, Is.EquivalentTo(new[] { "Water" }));

				Assert.That(ring.Image, Is.EqualTo("nc-impact"));
				Assert.That(ring.Explosions, Is.EqualTo(new[] { expected.Ring }));
				Assert.That(ring.Delay, Is.EqualTo(1));
				Assert.That(ring.ValidTargets, Is.EquivalentTo(new[] { "Ground", "Water", "Air" }));

				Assert.That(plume.Image, Is.EqualTo("nc-impact"));
				Assert.That(plume.Explosions, Is.EqualTo(new[] { expected.Plume }));
				Assert.That(plume.Delay, Is.EqualTo(3));
				Assert.That(plume.ValidTargets, Is.EquivalentTo(new[] { "Ground", "Air" }));

				Assert.That(splash.Explosions, Is.EqualTo(new[] { expected.Splash }));
				Assert.That(splash.ImpactSounds, Is.EqualTo(new[] { "gexpwasa.wav" }));
				Assert.That(splash.Delay, Is.EqualTo(1));
				Assert.That(splash.ValidTargets, Is.EquivalentTo(new[] { "Water" }));

				Assert.That(shake.Duration, Is.EqualTo(expected.ShakeDuration));
				Assert.That(shake.Intensity, Is.EqualTo(expected.ShakeIntensity));
				Assert.That(shake.Multiplier, Is.EqualTo(new float2(1, 1)));
			});
		}

		[TestCase("V3Weapon")]
		[TestCase("V3WeaponE")]
		[TestCase("BlimpBomb")]
		[TestCase("BlimpBombE")]
		public void VisualEffectsAreReliableAndBoundedPerTerrain(string weaponName)
		{
			var weapon = Weapon(weaponName);
			var effects = weapon.Value.Nodes
				.Where(node => node.Value.Value == "CreateEffect")
				.Select(Effect)
				.ToArray();

			Assert.Multiple(() =>
			{
				Assert.That(effects, Has.Length.EqualTo(6));
				Assert.That(effects, Has.All.Matches<CreateEffectWarhead>(effect => effect.Explosions.Length == 1));
				Assert.That(effects, Has.All.Matches<CreateEffectWarhead>(effect => !effect.ImpactActors));
				Assert.That(effects, Has.All.Matches<CreateEffectWarhead>(effect => effect.ForceDisplayAtGroundLevel));
				Assert.That(effects.Count(effect => effect.ValidTargets.Contains("Ground")), Is.EqualTo(4));
				Assert.That(effects.Count(effect => effect.ValidTargets.Contains("Water")), Is.EqualTo(4));
				Assert.That(effects.Count(effect => effect.ValidTargets.Contains("Ground") && effect.ImpactSounds.Length > 0),
					Is.EqualTo(1));
				Assert.That(effects.Count(effect => effect.ValidTargets.Contains("Water") && effect.ImpactSounds.Length > 0),
					Is.EqualTo(1));
				Assert.That(weapon.Value.Nodes,
					Has.None.Matches<MiniYamlNode>(node => node.Value.Value == "LeaveSmudge"),
					"Legacy smudge smoke would create a fifth SpriteEffect beyond the four-layer impact budget.");
			});
		}

		[Test]
		public void DreadnoughtWeaponsRetainTheirResolvedLegacySingleLayerEffects()
		{
			var normal = Weapon("DredWeapon");
			var elite = Weapon("DredWeaponE");
			var normalEffects = normal.Value.Nodes.Where(node => node.Value.Value == "CreateEffect").ToArray();
			var eliteEffects = elite.Value.Nodes.Where(node => node.Value.Value == "CreateEffect").ToArray();

			Assert.Multiple(() =>
			{
				Assert.That(normalEffects.Select(node => node.Key),
					Is.EqualTo(new[] { "Warhead@2Eff", "Warhead@3EffWater" }));
				Assert.That(Effect(normalEffects[0]).Explosions, Is.EqualTo(new[] { "medium_clsn" }));
				Assert.That(Effect(normalEffects[0]).ImpactSounds, Is.EqualTo(new[] { "gexp14a.wav" }));
				Assert.That(Effect(normalEffects[0]).ValidTargets, Is.EquivalentTo(new[] { "Ground", "Air" }));
				Assert.That(Effect(normalEffects[1]).Explosions, Is.EqualTo(new[] { "small_watersplash" }));
				Assert.That(Effect(normalEffects[1]).ImpactSounds, Is.EqualTo(new[] { "gexpwasa.wav" }));
				Assert.That(Effect(normalEffects[1]).ValidTargets, Is.EquivalentTo(new[] { "Water" }));

				Assert.That(eliteEffects.Select(node => node.Key), Is.EqualTo(new[] { "Warhead@2Eff" }));
				Assert.That(Effect(eliteEffects[0]).Explosions, Is.EqualTo(new[] { "terrorist_explosion" }));
				Assert.That(Effect(eliteEffects[0]).ImpactSounds, Is.EqualTo(new[] { "gexpapoa.wav" }));
				Assert.That(Effect(eliteEffects[0]).ValidTargets,
					Is.EquivalentTo(new[] { "Ground", "Water", "Air" }));

				Assert.That(normalEffects.Concat(eliteEffects).SelectMany(node => Effect(node).Explosions),
					Has.None.StartsWith("nc_"));
				Assert.That(normal.Value.Nodes.Concat(elite.Value.Nodes),
					Has.None.Matches<MiniYamlNode>(node => node.Value.Value == "ShakeScreen"));
				Assert.That(normal.Value.Nodes, Has.Some.Matches<MiniYamlNode>(node => node.Value.Value == "LeaveSmudge"));
				Assert.That(elite.Value.Nodes, Has.Some.Matches<MiniYamlNode>(node => node.Value.Value == "LeaveSmudge"));
			});
		}
	}
}
