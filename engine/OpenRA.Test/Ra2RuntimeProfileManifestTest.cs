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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using NUnit.Framework;
using OpenRA.FileSystem;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class Ra2RuntimeProfileManifestTest
	{
		const string GoldenFileSha256 = "6c0d12b5a985bcf87fc255a03a958dbc09be00de9238b239354c8d34be1bd8f6";
		const string GoldenShapeSha256 = "4fe820475ee4d6ef843320f406b5db3fd414fd106c953b4a7b4a05020fb3614d";
		const string ShapeMagic = "OpenRA.RuntimeContractDescriptorShape";
		const string AotMagic = "OpenRA.AotRegistry";
		const string ManifestSemanticMagic = "OpenRA.Ra2ManifestSemantics";
		const int GoldenSchemaVersion = 1;
		const int GoldenDescriptorCount = 296;
		const int ManifestSemanticSchemaVersion = 1;
		const string DesktopGameplaySemanticSha256 =
			"477359f04361541128803a546908843ff025213ca7be51cf7d33b3590d99762c";
		const string DesktopFullManifestSemanticSha256 =
			"edfeeaf20df66eeb048035ce8fe00bba9b9c31c1ad1aeccea05c45f74d666ec6";
		const string SharedRa2AiSha256 =
			"afcf48baec31d0ebaf3a0e139774cc4cdebca3dc1dc8ca3a3c5ef8aa5c5bd995";
		const int SharedRa2AiLength = 3834;

		static readonly UTF8Encoding StrictUtf8 = new(false, true);
		static readonly DesktopSourceFixture[] DesktopPreSplitSources =
		{
			new("mods/ra2/mod.yaml", 8796,
				"6fa6eb2beeb17488d2ef3a7d2bab5c87acfa3f03b6ed26e572f51c80583178f7"),
			new("mods/ra2/rules/world.yaml", 11326,
				"55ed4dca1c2a4c4053db5dfa8d21462d0a60e956361d9cf1839912a180a9034b"),
			new("mods/ra2/rules/defaults.yaml", 29110,
				"1b4f61f869754b333176fd37058eb02fcc64b30f4a96236fd83b4caf3d23a8c6"),
			new("mods/ra2/rules/proxy-actors.yaml", 319,
				"1aa6110f77972d8f698b33ab4fbec0a93f8f40e6d2f8d4c4b4d6b39aeb1a2b79"),
			new("mods/ra2/rules/allied-infantry.yaml", 15008,
				"84cff40c9c20ec22b6fdd11ae46cf0030e16710c7a4e8390757b1b2e3536ddae"),
			new("mods/ra2/rules/soviet-infantry.yaml", 10667,
				"e13ffb6fefdea6c51d8dcf737ffd0313dc45ce3d4b2a2a3e9fb8449c3c3c4ab3"),
			new("mods/ra2/rules/soviet-vehicles.yaml", 11843,
				"373fbdef57c090ca382a08b774c901cb905bbc53f656616554c13bb568c1de59"),
			new("mods/ra2/sequences/vehicles.yaml", 5432,
				"39e0f87a785572b3c41e9b4b57bc19a0167d9fe4908c8c2671618ad53a2cbf3c"),
			new("mods/ra2/sequences/voxels.yaml", 2390,
				"2c66ce26eff7fd3b3147ab38c5f31d0813cf75ed6a7237c9c8cc9c67c7ab0b2d"),
			new("mods/ra2/audio/voices.yaml", 13232,
				"8af108c3c861fdeca8f636fc3f8d5318f9d8ad172afb2a8c8b9ccf242f944ba3"),
		};

		static readonly string[] CommonManifestKeys =
		{
			"Metadata", "ModCredits", "Cursors", "Chrome", "Assemblies", "ChromeLayout",
			"AllowUnusedFluentMessagesInExternalPackages", "LoadScreen", "ServerTraits",
			"ChromeMetrics", "Fonts", "DefaultOrderGenerator", "SupportsMapsFrom", "SoundFormats",
			"SpriteFormats", "VideoFormats", "TerrainFormat", "SpriteSequenceFormat", "AssetBrowser",
			"GameSpeeds",
		};

		static readonly string[] GameplayManifestKeys =
		{
			"Rules", "Sequences", "ModelSequences", "Weapons", "Voices", "Notifications", "TileSets",
			"MapGrid", "Music", "FluentMessages", "Hotkeys",
		};

		static readonly string[] FileSystemManifestKeys =
		{
			"PackageFormats", "FileSystem", "MapFolders",
		};

		static readonly string[] DesktopManifestKeys =
		{
			"Metadata", "ModCredits", "PackageFormats", "FileSystem", "MapFolders", "Rules", "Sequences",
			"ModelSequences", "Cursors", "Chrome", "Assemblies", "ChromeLayout", "Weapons", "Voices",
			"Notifications", "TileSets", "MapGrid", "Music", "FluentMessages",
			"AllowUnusedFluentMessagesInExternalPackages", "Hotkeys", "LoadScreen", "ServerTraits",
			"ChromeMetrics", "Fonts", "DefaultOrderGenerator", "SupportsMapsFrom", "SoundFormats",
			"SpriteFormats", "VideoFormats", "TerrainFormat", "SpriteSequenceFormat", "AssetBrowser",
			"GameSpeeds",
		};

		static readonly string[] YuriOnlyWholeGameplayFiles =
		{
			"ra2|rules/yuri-infantry.yaml", "ra2|rules/yuri-structures.yaml",
			"ra2|rules/yuri-vehicles.yaml", "ra2|rules/yuri-naval.yaml",
			"ra2|sequences/yuri-infantry.yaml", "ra2|sequences/yuri-structures.yaml",
			"ra2|weapons/yuri.yaml",
		};

		static readonly IReadOnlyDictionary<string, string> BaseGameplayReplacements =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["ra2|rules/world.yaml"] = "ra2|rules/world-base.yaml",
				["ra2|rules/defaults.yaml"] = "ra2|rules/defaults-base.yaml",
				["ra2|rules/proxy-actors.yaml"] = "ra2|rules/proxy-actors-base.yaml",
				["ra2|rules/allied-infantry.yaml"] = "ra2|rules/allied-infantry-base.yaml",
				["ra2|rules/soviet-infantry.yaml"] = "ra2|rules/soviet-infantry-base.yaml",
				["ra2|rules/soviet-vehicles.yaml"] = "ra2|rules/soviet-vehicles-base.yaml",
				["ra2|sequences/vehicles.yaml"] = "ra2|sequences/vehicles-base.yaml",
				["ra2|sequences/voxels.yaml"] = "ra2|sequences/voxels-base.yaml",
				["ra2|audio/voices.yaml"] = "ra2|audio/voices-base.yaml",
			};

		static readonly string[] ChaosRemovals =
		{
			"-FirepowerMultiplier@CHAOS", "-Berserkable@CHAOS", "-RejectsOrders@CHAOS",
			"-WithColoredOverlay@CHAOS", "-TimedConditionBar@CHAOS", "-Targetable@CHAOS",
			"-ExternalCondition@CHAOS",
		};

		static readonly string[] YuriContentPackages =
		{
			"~langmd.mix\tlangmd", "~ra2md.mix\tra2md", "~thememd.mix\t",
			"~langmd|audiomd.mix\taudiomd", "~langmd|cameomd.mix\tcameomd", "~ra2md|cachemd.mix\t",
			"~ra2md|conqmd.mix\tconqmd", "~ra2md|des.mix\t", "~ra2md|desert.mix\t",
			"~ra2md|genermd.mix\t", "~ra2md|isodesmd.mix\t", "~ra2md|isodes.mix\t",
			"~ra2md|isogenmd.mix\t", "~ra2md|isolunmd.mix\t", "~ra2md|isolun.mix\t",
			"~ra2md|isosnomd.mix\t", "~ra2md|isotemmd.mix\t", "~ra2md|isoubnmd.mix\t",
			"~ra2md|isoubn.mix\t", "~ra2md|isourbmd.mix\t", "~ra2md|loadmd.mix\t",
			"~ra2md|localmd.mix\tlocalmd", "~ra2md|lun.mix\t", "~ra2md|lunar.mix\t",
			"~ra2md|ntrlmd.mix\t", "~ra2md|snowmd.mix\t", "~ra2md|ubn.mix\t",
			"~ra2md|urbann.mix\t", "~audiomd|audio.bag\t",
		};

		static readonly string[] SharedContentPackages =
		{
			"ra2.mix\t", "language.mix\t", "audio.mix\t", "cache.mix\t", "cameo.mix\t",
			"conquer.mix\tconquer", "generic.mix\t", "isogen.mix\t", "isosnow.mix\t",
			"isotemp.mix\t", "isourb.mix\t", "load.mix\t", "local.mix\t", "neutral.mix\t",
			"sidec01.mix\t", "sidec02.mix\t", "sno.mix\t", "snow.mix\t", "tem.mix\t",
			"temperat.mix\t", "urb.mix\t", "urban.mix\t", "audio.bag\t", "~theme.mix\t",
			"ra2|bits\t", "ra2|bits/cameos\t", "ra2|bits/structures\t", "ra2|bits/animations\t",
			"ra2|bits/projectiles\t", "ra2|uibits\t",
		};
		static readonly string[] ProfileSplitPaths =
		{
			"mods/ra2/audio/voices-base.yaml",
			"mods/ra2/manifest/common.yaml",
			"mods/ra2/manifest/filesystem-desktop.yaml",
			"mods/ra2/manifest/filesystem-ios-base.yaml",
			"mods/ra2/manifest/filesystem-ios-yuri.yaml",
			"mods/ra2/manifest/gameplay-base.yaml",
			"mods/ra2/manifest/gameplay-yuri.yaml",
			"mods/ra2/mod.ios-base.yaml",
			"mods/ra2/mod.ios-yuri.yaml",
			"mods/ra2/rules/allied-infantry-base.yaml",
			"mods/ra2/rules/defaults-base.yaml",
			"mods/ra2/rules/proxy-actors-base.yaml",
			"mods/ra2/rules/soviet-infantry-base.yaml",
			"mods/ra2/rules/soviet-vehicles-base.yaml",
			"mods/ra2/rules/world-base.yaml",
			"mods/ra2/sequences/vehicles-base.yaml",
			"mods/ra2/sequences/voxels-base.yaml",
		};

		static readonly string[] PublicCleanProfileRemoves =
		{
			"../../mods/ra2/audio/voices-base.yaml",
			"../../mods/ra2/mod.ios-base.yaml",
			"../../mods/ra2/mod.ios-yuri.yaml",
			"../../mods/ra2/rules/allied-infantry-base.yaml",
			"../../mods/ra2/rules/defaults-base.yaml",
			"../../mods/ra2/rules/proxy-actors-base.yaml",
			"../../mods/ra2/rules/soviet-infantry-base.yaml",
			"../../mods/ra2/rules/soviet-vehicles-base.yaml",
			"../../mods/ra2/rules/world-base.yaml",
			"../../mods/ra2/sequences/vehicles-base.yaml",
			"../../mods/ra2/sequences/voxels-base.yaml",
		};

		[Test]
		public void DesktopManifestAndMixedSourcesRetainTheirPreSplitBytes()
		{
			var root = RepositoryRoot();
			foreach (var fixture in DesktopPreSplitSources)
			{
				var bytes = File.ReadAllBytes(Path.Combine(root, fixture.Path));
				Assert.Multiple(() =>
				{
					Assert.That(bytes.LongLength, Is.EqualTo(fixture.Length), fixture.Path);
					Assert.That(Hex(SHA256.HashData(bytes)), Is.EqualTo(fixture.Sha256), fixture.Path);
				});
			}
		}

		[Test]
		public void RealProfileEntriesAreAnExactClosedSplitOfTheDesktopManifest()
		{
			var root = RepositoryRoot();
			var modRoot = Path.Combine(root, "mods", "ra2");
			var desktopNodes = ReadYaml(modRoot, "mod.yaml");
			var commonNodes = ReadYaml(modRoot, "manifest/common.yaml");
			var gameplayBaseNodes = ReadYaml(modRoot, "manifest/gameplay-base.yaml");
			var gameplayYuriNodes = ReadYaml(modRoot, "manifest/gameplay-yuri.yaml");
			var filesystemDesktopNodes = ReadYaml(modRoot, "manifest/filesystem-desktop.yaml");
			var filesystemBaseNodes = ReadYaml(modRoot, "manifest/filesystem-ios-base.yaml");
			var filesystemYuriNodes = ReadYaml(modRoot, "manifest/filesystem-ios-yuri.yaml");

			Assert.That(desktopNodes.Select(n => n.Key), Is.EqualTo(DesktopManifestKeys));
			Assert.That(CommonManifestKeys.Concat(GameplayManifestKeys).Concat(FileSystemManifestKeys)
				.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(DesktopManifestKeys.Length));
			Assert.That(DesktopManifestKeys.Except(CommonManifestKeys.Concat(GameplayManifestKeys)
				.Concat(FileSystemManifestKeys), StringComparer.Ordinal), Is.Empty,
				"Every desktop top-level manifest node must have exactly one reviewed partition.");

			var desktopCommon = Partition(desktopNodes, CommonManifestKeys);
			var desktopGameplay = Partition(desktopNodes, GameplayManifestKeys);
			var desktopFileSystem = Partition(desktopNodes, FileSystemManifestKeys);
			Assert.Multiple(() =>
			{
				Assert.That(Hex(SHA256.HashData(CanonicalYaml(desktopGameplay))),
					Is.EqualTo(DesktopGameplaySemanticSha256), "ordered effective-gameplay fixture");
				Assert.That(Hex(SHA256.HashData(FrameManifestSemantics(
					desktopCommon, desktopGameplay, desktopFileSystem))),
					Is.EqualTo(DesktopFullManifestSemanticSha256), "partitioned full-manifest fixture");
			});

			Assert.That(commonNodes.Select(n => n.Key),
				Is.EqualTo(CommonManifestKeys.Append("RuntimeContractCultures")));
			var cultures = SingleNode(commonNodes, "RuntimeContractCultures");
			Assert.Multiple(() =>
			{
				Assert.That(cultures.Value.Value, Is.Null.Or.Empty);
				Assert.That(cultures.Value.Nodes.Select(n => n.Key), Is.EqualTo(new[] { "zh-CN" }));
				Assert.That(cultures.Value.Nodes[0].Value.Value, Is.Null.Or.Empty);
				Assert.That(cultures.Value.Nodes[0].Value.Nodes, Is.Empty);
			});
			Assert.That(gameplayBaseNodes.Select(n => n.Key), Is.EqualTo(GameplayManifestKeys));
			Assert.That(gameplayYuriNodes.Select(n => n.Key), Is.EqualTo(GameplayManifestKeys));
			Assert.That(CanonicalYaml(commonNodes.Take(commonNodes.Count - 1)),
				Is.EqualTo(CanonicalYaml(desktopCommon)));
			Assert.That(CanonicalYaml(gameplayYuriNodes), Is.EqualTo(CanonicalYaml(desktopGameplay)));
			Assert.That(CanonicalYaml(new[] { SingleNode(gameplayBaseNodes, "MapGrid") }),
				Is.EqualTo(CanonicalYaml(new[] { SingleNode(desktopGameplay, "MapGrid") })));
			Assert.That(CanonicalYaml(filesystemDesktopNodes), Is.EqualTo(CanonicalYaml(desktopFileSystem)));

			AssertEntryIncludes(modRoot, "mod.ios-base.yaml", new[]
			{
				"manifest/common.yaml", "manifest/gameplay-base.yaml", "manifest/filesystem-ios-base.yaml",
			});
			AssertEntryIncludes(modRoot, "mod.ios-yuri.yaml", new[]
			{
				"manifest/common.yaml", "manifest/gameplay-yuri.yaml", "manifest/filesystem-ios-yuri.yaml",
			});
			foreach (var fragment in new[]
			{
				commonNodes, gameplayBaseNodes, gameplayYuriNodes, filesystemDesktopNodes,
				filesystemBaseNodes, filesystemYuriNodes,
			})
				Assert.That(fragment.Select(n => n.Key), Does.Not.Contain("Include"));

			using var desktopPackage = new Folder(modRoot);
			using var basePackage = new Folder(modRoot);
			using var yuriPackage = new Folder(modRoot);
			using var desktop = new Manifest("ra2", desktopPackage);
			using var baseProfile = new Manifest("ra2", basePackage, "mod.ios-base.yaml");
			using var yuriProfile = new Manifest("ra2", yuriPackage, "mod.ios-yuri.yaml");

			Assert.Multiple(() =>
			{
				Assert.That(desktop.IncludedFiles, Is.Empty);
				Assert.That(desktop.RuntimeContractCultures, Is.Empty);
				Assert.That(baseProfile.RuntimeContractCultures, Is.EqualTo(new[] { "zh-CN" }));
				Assert.That(yuriProfile.RuntimeContractCultures, Is.EqualTo(new[] { "zh-CN" }));
			});

			AssertManifestArraysEqual(desktop, yuriProfile);
			AssertBaseGameplayProjection(desktop, baseProfile);
			AssertCommonManifestArraysEqual(desktop, baseProfile);
			AssertCommonManifestArraysEqual(desktop, yuriProfile);

			var originalMixed = BaseGameplayReplacements.Keys.ToArray();
			var baseFiles = ManifestGameplayFiles(baseProfile).ToArray();
			var yuriFiles = ManifestGameplayFiles(yuriProfile).ToArray();
			Assert.Multiple(() =>
			{
				Assert.That(baseFiles.Intersect(originalMixed, StringComparer.Ordinal), Is.Empty,
					"Base must never load an original mixed gameplay source.");
				Assert.That(baseFiles.Intersect(YuriOnlyWholeGameplayFiles, StringComparer.Ordinal), Is.Empty,
					"Base must omit the seven whole Yuri gameplay files.");
				Assert.That(yuriFiles.Intersect(BaseGameplayReplacements.Values, StringComparer.Ordinal), Is.Empty,
					"Yuri must never load a Base copy.");
				Assert.That(yuriFiles, Does.Contain("ra2|rules/ai.yaml"));
				Assert.That(baseFiles, Does.Contain("ra2|rules/ai.yaml"));
			});
			var sharedAiBytes = File.ReadAllBytes(Path.Combine(modRoot, "rules", "ai.yaml"));
			Assert.Multiple(() =>
			{
				Assert.That(sharedAiBytes.LongLength, Is.EqualTo(SharedRa2AiLength));
				Assert.That(Hex(SHA256.HashData(sharedAiBytes)), Is.EqualTo(SharedRa2AiSha256));
			});

			AssertFileSystemProjection(desktopNodes, filesystemBaseNodes, filesystemYuriNodes);
		}

		[Test]
		public void BaseGameplayCopiesApplyOnlyTheReviewedClosedSubtraction()
		{
			var root = RepositoryRoot();
			AssertClosedSubtraction(root, "mods/ra2/rules/world.yaml", "mods/ra2/rules/world-base.yaml",
				new[]
				{
					"^BaseWorld/Faction@yuri", "World/StartingUnits@pcv",
					"World/StartingUnits@lightyuri", "World/StartingUnits@mediumyuri",
					"World/StartingUnits@heavyyuri",
				},
				new[]
				{
					new SemanticChange("^BaseWorld/Faction@random/RandomFactionMembers",
						"random-allies, random-soviets, yuri", "random-allies, random-soviets"),
				});

			AssertClosedSubtraction(root, "mods/ra2/rules/proxy-actors.yaml",
				"mods/ra2/rules/proxy-actors-base.yaml", new[] { "yuri.tech.infiltrated" },
				Array.Empty<SemanticChange>());

			var defaultsRemovals = new List<string>
			{
				"^Berserkable", "^MagneticLiftable", "^Infantry/Inherits@CHAOS",
				"^Infantry/WithDeathAnimation@effect/DeathTypes/MutationDeath",
				"^Infantry/SpawnActorOnDeath@brute", "^Animal/-SpawnActorOnDeath@brute",
				"^Vehicle/Inherits@CHAOS", "^Vehicle/Inherits@MAGNE", "^Ship/Inherits@CHAOS",
				"^Ship/Inherits@MAGNE", "^Ship/Targetable/RequiresCondition",
			};
			defaultsRemovals.AddRange(ChaosRemovals.Select(key => "^Animal/" + key));
			AssertClosedSubtraction(root, "mods/ra2/rules/defaults.yaml", "mods/ra2/rules/defaults-base.yaml",
				defaultsRemovals,
				new[]
				{
					new SemanticChange("^Vehicle/Targetable/RequiresCondition",
						"!parachute && !magne-lift", "!parachute"),
					new SemanticChange("^Vehicle/Repairable/RepairActors",
						"gadept, nadept, yadept, caoutp", "gadept, nadept, caoutp"),
					new SemanticChange("^Ship/Repairable/RepairActors",
						"gayard, nayard, yayard", "gayard, nayard"),
				});

			AssertClosedSubtraction(root, "mods/ra2/rules/allied-infantry.yaml",
				"mods/ra2/rules/allied-infantry-base.yaml",
				ChaosRemovals.Select(key => "dog/" + key).Append("dog/-SpawnActorOnDeath@brute"),
				Array.Empty<SemanticChange>());
			AssertClosedSubtraction(root, "mods/ra2/rules/soviet-infantry.yaml",
				"mods/ra2/rules/soviet-infantry-base.yaml",
				ChaosRemovals.Select(key => "yuri/" + key), Array.Empty<SemanticChange>());
			AssertClosedSubtraction(root, "mods/ra2/rules/soviet-vehicles.yaml",
				"mods/ra2/rules/soviet-vehicles-base.yaml",
				ChaosRemovals.Select(key => "dron/" + key), Array.Empty<SemanticChange>());

			AssertClosedSubtraction(root, "mods/ra2/sequences/vehicles.yaml",
				"mods/ra2/sequences/vehicles-base.yaml",
				new[] { "ltnk", "ytnk", "pcv", "smin", "caos", "tele", "mind", "disk", "yhvr", "bsub" },
				Array.Empty<SemanticChange>());
			AssertClosedSubtraction(root, "mods/ra2/sequences/voxels.yaml",
				"mods/ra2/sequences/voxels-base.yaml",
				new[]
				{
					"ltnk", "ytnk", "caos", "tele", "mind", "disk", "yhvr", "bsub", "bsubmisl",
					"pcv", "smin", "yaggun",
				}, Array.Empty<SemanticChange>());
			AssertClosedSubtraction(root, "mods/ra2/audio/voices.yaml", "mods/ra2/audio/voices-base.yaml",
				new[]
				{
					"GGIVoice", "BattleFortressVoice", "RobotTankVoice", "SiegeChopperVoice",
					"LasherTankVoice", "GatlingTankVoice", "SlaveMinerVoice", "SlaveVoice",
					"InitiateVoice", "BruteVoice",
				}, Array.Empty<SemanticChange>());

			var baseVoiceKeys = ReadYaml(Path.Combine(root, "mods", "ra2"), "audio/voices-base.yaml")
				.Select(n => n.Key).ToArray();
			Assert.Multiple(() =>
			{
				Assert.That(baseVoiceKeys.Last(), Is.EqualTo("V3LauncherVoice"));
				Assert.That(baseVoiceKeys, Does.Contain("YuriVoice"));
			});
		}

		[Test]
		public void RuntimeDescriptorGoldenIsAuthenticatedAndCanonicallyFramed()
		{
			var root = RepositoryRoot();
			var goldenPath = Path.Combine(root, "packaging", "ios-runtime-contract-descriptor-shape-v1.json");
			var goldenBytes = File.ReadAllBytes(goldenPath);
			Assert.That(Hex(SHA256.HashData(goldenBytes)), Is.EqualTo(GoldenFileSha256));

			var golden = ReadGolden(goldenBytes);
			Assert.Multiple(() =>
			{
				Assert.That(golden.SchemaVersion, Is.EqualTo(GoldenSchemaVersion));
				Assert.That(golden.DescriptorCount, Is.EqualTo(GoldenDescriptorCount));
				Assert.That(golden.ShapeDigest, Is.EqualTo(GoldenShapeSha256));
				Assert.That(golden.Descriptors, Has.Count.EqualTo(GoldenDescriptorCount));
				Assert.That(golden.Descriptors.Count(d => d.Purpose == "Contract"), Is.EqualTo(165));
				Assert.That(golden.Descriptors.Count(d => d.Purpose == "PreflightOnly"), Is.EqualTo(131));
				Assert.That(golden.Descriptors.Count(d => d.Purpose == "Contract" &&
					d.Profiles.SequenceEqual(new[] { "Base" })), Is.EqualTo(12));
				Assert.That(golden.Descriptors.Count(d => d.Purpose == "Contract" &&
					d.Profiles.SequenceEqual(new[] { "Yuri" })), Is.EqualTo(19));
				Assert.That(golden.Descriptors.Count(d => d.Purpose == "Contract" &&
					d.Profiles.SequenceEqual(new[] { "Base", "Yuri" })), Is.EqualTo(134));
				Assert.That(golden.Descriptors.Count(d => d.Purpose == "PreflightOnly" &&
					d.Profiles.SequenceEqual(new[] { "Base", "Yuri" })), Is.EqualTo(131));
				Assert.That(golden.Descriptors.Count(d => d.Profiles.Contains("Base")), Is.EqualTo(277));
				Assert.That(golden.Descriptors.Count(d => d.Profiles.Contains("Yuri")), Is.EqualTo(284));
			});

			AssertCanonicalGolden(golden);
			Assert.That(Hex(SHA256.HashData(FrameShape(golden))), Is.EqualTo(GoldenShapeSha256));

			var literal = new DescriptorShape("unused", "fixture", "fixture",
				new[] { "Base", "Yuri" }, "Contract");
			var literalFrame = FrameShape(new GoldenShape(1, 1,
				"310af2f25415e800bfa4821450a664836e5644009ac6fd61cde46cf070c1a07c",
				new[] { literal }));
			Assert.Multiple(() =>
			{
				Assert.That(Convert.ToHexString(literalFrame).ToLowerInvariant(), Is.EqualTo(
					"250000004f70656e52412e52756e74696d65436f6e747261637444657363726970746f725368617065" +
					"01000000010000003a000000000000000700000066697874757265070000006669787475726502000000" +
					"0400000042617365040000005975726908000000436f6e7472616374"));
				Assert.That(Hex(SHA256.HashData(literalFrame)), Is.EqualTo(
					"310af2f25415e800bfa4821450a664836e5644009ac6fd61cde46cf070c1a07c"));
			});
		}

		[Test]
		public void EvaluatedProjectProjectionAndBundleTransportMatchGolden()
		{
			var root = RepositoryRoot();
			var golden = ReadGolden(File.ReadAllBytes(Path.Combine(
				root, "packaging", "ios-runtime-contract-descriptor-shape-v1.json")));
			var personal = EvaluateIosProject(root, false);
			var publicClean = EvaluateIosProject(root, true);

			AssertEvaluatedDescriptors(golden, personal.RuntimeDescriptors, "Personal");
			AssertEvaluatedDescriptors(golden, publicClean.RuntimeDescriptors, "PublicClean");
			AssertPersonalBundleTransport(golden, personal.BundleResources);
			Assert.That(personal.BundleResources.Select(BundleTarget),
				Does.Not.Contain("ios-runtime-contract-descriptor-shape-v1.json"));

			var publicDerived = publicClean.BundleResources
				.Where(i => Metadata(i, "RuntimeContractDerived") == "true")
				.ToArray();
			Assert.That(publicDerived, Is.Empty,
				"PublicClean must not transform any runtime descriptor into a bundle resource.");
			Assert.That(publicClean.BundleResources.Select(BundleTarget),
				Does.Not.Contain("ios-runtime-contract-descriptor-shape-v1.json"));

			var profileTargets = golden.Descriptors.Where(d => ProfileSplitPaths.Contains(
				d.LogicalName, StringComparer.Ordinal)).Select(d => d.BundleRelativePath).ToArray();
			Assert.That(profileTargets, Has.Length.EqualTo(17));
			Assert.That(publicClean.BundleResources.Select(BundleTarget).Intersect(profileTargets,
				StringComparer.Ordinal), Is.Empty, "PublicClean must contain none of the 17 profile resources.");

			AssertExactPublicCleanRemoves(root);
		}

		[Test]
		public void GenerationTargetWritesStrictProjectionAndReproducesCheckedInSource()
		{
			var root = RepositoryRoot();
			var golden = ReadGolden(File.ReadAllBytes(Path.Combine(
				root, "packaging", "ios-runtime-contract-descriptor-shape-v1.json")));
			var temporaryDirectory = CreateTemporaryDirectory(root, "openra-runtime-descriptors-");
			try
			{
				var inputPath = Path.Combine(temporaryDirectory, "runtime-contract-descriptors-v1.tsv");
				var outputPath = Path.Combine(temporaryDirectory, "GeneratedAotObjectRegistry.g.cs");
				RunIosGenerationTarget(root, inputPath, outputPath);

				var inputBytes = File.ReadAllBytes(inputPath);
				Assert.That(inputBytes.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }),
					"The evaluated descriptor input must be UTF-8 without a BOM.");
				var input = StrictUtf8.GetString(inputBytes).Replace("\r\n", "\n");
				Assert.That(input, Does.Not.Contain("\r"),
					"A carriage return is valid only as part of a CRLF record separator.");
				if (input.EndsWith("\n", StringComparison.Ordinal))
					input = input[..^1];
				var rows = input.Split('\n');
				Assert.That(rows, Has.Length.EqualTo(GoldenDescriptorCount));
				for (var i = 0; i < golden.Descriptors.Count; i++)
				{
					var descriptor = golden.Descriptors[i];
					Assert.That(rows[i].Split('\t'), Has.Length.EqualTo(5), $"evaluated row {i}");
					Assert.That(rows[i], Is.EqualTo(string.Join("\t",
						descriptor.SourcePath,
						descriptor.LogicalName,
						descriptor.BundleRelativePath,
						string.Join(",", descriptor.Profiles),
						descriptor.Purpose)), $"evaluated row {i}");
				}

				Assert.That(File.ReadAllBytes(outputPath), Is.EqualTo(File.ReadAllBytes(Path.Combine(
					root, "ios", "OpenRA.iOS", "GeneratedAotObjectRegistry.g.cs"))));
			}
			finally
			{
				Directory.Delete(temporaryDirectory, true);
			}
		}

		[Test]
		public void GeneratedInventoryMatchesGoldenAndCurrentSourceBytes()
		{
			var root = RepositoryRoot();
			var golden = ReadGolden(File.ReadAllBytes(Path.Combine(
				root, "packaging", "ios-runtime-contract-descriptor-shape-v1.json")));
			var generatedPath = Path.Combine(root, "ios", "OpenRA.iOS", "GeneratedAotObjectRegistry.g.cs");
			var generated = File.ReadAllText(generatedPath, StrictUtf8);

			Assert.That(Regex.Matches(generated,
				@"internal static RuntimeContractDescriptorInventory RuntimeContractDescriptors\s*\{").Count,
				Is.EqualTo(1), "The generated descriptor inventory property is missing or duplicated.");
			Assert.That(ExtractIntConstant(generated, "RuntimeContractDescriptorSchemaVersion"),
				Is.EqualTo(GoldenSchemaVersion));
			Assert.That(ExtractIntConstant(generated, "RuntimeContractDescriptorCount"),
				Is.EqualTo(GoldenDescriptorCount));
			Assert.That(ExtractStringConstant(generated, "RuntimeContractDescriptorShapeDigest"),
				Is.EqualTo(GoldenShapeSha256));

			var emitted = ParseGeneratedDescriptors(generated);
			Assert.That(emitted, Has.Count.EqualTo(GoldenDescriptorCount));
			var iosProjectDirectory = Path.Combine(root, "ios", "OpenRA.iOS");
			for (var i = 0; i < golden.Descriptors.Count; i++)
			{
				var shape = golden.Descriptors[i];
				var descriptor = emitted[i];
				var sourcePath = Path.GetFullPath(Path.Combine(iosProjectDirectory, shape.SourcePath));
				var sourceBytes = File.ReadAllBytes(sourcePath);
				Assert.Multiple(() =>
				{
					Assert.That(descriptor.LogicalName, Is.EqualTo(shape.LogicalName), $"row {i}");
					Assert.That(descriptor.BundleRelativePath, Is.EqualTo(shape.BundleRelativePath), $"row {i}");
					Assert.That(descriptor.Profiles, Is.EqualTo(shape.Profiles), $"row {i}");
					Assert.That(descriptor.Purpose, Is.EqualTo(shape.Purpose), $"row {i}");
					Assert.That(descriptor.Length, Is.EqualTo(sourceBytes.LongLength), shape.SourcePath);
					Assert.That(descriptor.Sha256, Is.EqualTo(Hex(SHA256.HashData(sourceBytes))), shape.SourcePath);
				});
			}

			Assert.That(golden.Descriptors.Where(d => generated.Contains(d.SourcePath,
				StringComparison.Ordinal)).Select(d => d.SourcePath), Is.Empty,
				"Generated source must not disclose build provenance paths.");
			Assert.That(generated, Does.Not.Contain(root));
		}

		[Test]
		public void GeneratedAotIdentityUsesTheCanonicalRecordFraming()
		{
			var root = RepositoryRoot();
			var generated = File.ReadAllText(Path.Combine(
				root, "ios", "OpenRA.iOS", "GeneratedAotObjectRegistry.g.cs"), StrictUtf8);
			var expectedIdentity = ExtractStringConstant(generated, "AotRegistryIdentity");
			Assert.That(expectedIdentity, Does.Match("^[0-9a-f]{64}$"));

			var payloadMatch = Regex.Match(generated,
				"// BEGIN AOT IDENTITY RECORD PAYLOADS(?<body>.*?)// END AOT IDENTITY RECORD PAYLOADS",
				RegexOptions.Singleline | RegexOptions.CultureInvariant);
			Assert.That(payloadMatch.Success, Is.True, "The canonical generated AOT record projection is missing.");
			var payloads = Regex.Matches(payloadMatch.Groups["body"].Value, "\"(?<payload>[A-Za-z0-9+/]+={0,2})\"")
				.Select(m => Convert.FromBase64String(m.Groups["payload"].Value))
				.ToArray();
			Assert.That(payloads, Is.Not.Empty);

			var records = payloads.Select(ParseAotRecord).ToArray();
			Assert.That(records.Select(r => r.TypeName),
				Is.Ordered.Using<string>(StringComparer.Ordinal));
			Assert.That(records.Select(r => r.TypeName).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
				Is.EqualTo(records.Length));
			Assert.That(records.Select(r => r.Kind), Is.All.Matches<int>(kind => kind is 1 or 2));

			var registeredNames = ParseGeneratedStringArray(generated, "RegisteredTypeNames");
			Assert.That(registeredNames, Is.EqualTo(records.Select(r => r.TypeName)));
			var framed = FrameAot(payloads);
			Assert.That(Hex(SHA256.HashData(framed)), Is.EqualTo(expectedIdentity));

			var selectedType = typeof(OpenRA.Mods.Common.Widgets.Logic.ConnectionLogic);
			var selectedIndex = Array.FindIndex(records,
				record => record.TypeName == selectedType.FullName);
			Assert.That(selectedIndex, Is.GreaterThanOrEqualTo(0),
				$"{selectedType.FullName} must be present in the checked generated registry.");
			var constructor = selectedType.GetConstructors(BindingFlags.Instance |
				BindingFlags.Public | BindingFlags.NonPublic).Single(candidate =>
					candidate.CustomAttributes.Any(attribute => attribute.AttributeType.FullName ==
						"OpenRA.ObjectCreator+UseCtorAttribute"));
			var reflectedParameters = constructor.GetParameters().Select(parameter =>
				new AotParameter(parameter.Name!, AotTypeExpression(parameter.ParameterType))).ToArray();
			Assert.That(reflectedParameters, Has.Length.GreaterThanOrEqualTo(2));
			Assert.That(reflectedParameters.Select(parameter => parameter.Name), Is.Not.EqualTo(
				reflectedParameters.Select(parameter => parameter.Name).OrderBy(name => name,
					StringComparer.Ordinal)), "The real fixture must detect alphabetical reordering.");

			var expectedRecord = new AotRecord(selectedType.FullName!, 2, reflectedParameters);
			var selectedRecord = records[selectedIndex];
			var factory = ParseGeneratedUseCtorFactory(generated, AotTypeExpression(selectedType));
			Assert.Multiple(() =>
			{
				Assert.That(selectedRecord.Kind, Is.EqualTo(2));
				Assert.That(selectedRecord.Parameters.Select(parameter =>
					(parameter.Name, parameter.TypeExpression)), Is.EqualTo(reflectedParameters.Select(parameter =>
					(parameter.Name, parameter.TypeExpression))), "canonical record metadata order");
				Assert.That(payloads[selectedIndex], Is.EqualTo(FrameAotRecord(expectedRecord)),
					"canonical record payload");
				Assert.That(factory.Parameters.Select(parameter =>
					(parameter.Name, parameter.TypeExpression)), Is.EqualTo(reflectedParameters.Select(parameter =>
					(parameter.Name, parameter.TypeExpression))), "generated constructor argument order");
			});

			var literalPayload = FrameAotRecord(new AotRecord("Example.Type", 1,
				Array.Empty<AotParameter>()));
			var literalFrame = FrameAot(new[] { literalPayload });
			Assert.Multiple(() =>
			{
				Assert.That(Convert.ToHexString(literalFrame).ToLowerInvariant(), Is.EqualTo(
					"120000004f70656e52412e416f74526567697374727901000000010000000c0000004578616d706c652e" +
					"547970650100000000000000"));
				Assert.That(Hex(SHA256.HashData(literalFrame)), Is.EqualTo(
					"d45dd698a5f0bd2a48fe724d5fe8c7f993e5e506824d8ffa7865e1293f28b352"));
			});
		}

		[Test]
		public void GeneratorRejectsLeafAndParentSymlinkAssemblyPaths()
		{
			var root = RepositoryRoot();
			var temporaryDirectory = CreateTemporaryDirectory(root, "openra-aot-symlinks-");
			try
			{
				var generator = BuildAotGenerator(root);
				var descriptorInput = WriteDescriptorInput(root, temporaryDirectory);
				var realAssemblyDirectory = Path.Combine(root, "engine", "bin");
				AssertGeneratorAcceptsRegularInputs(root, generator, descriptorInput,
					Path.Combine(temporaryDirectory, "control.g.cs"));
				var parentLink = Path.Combine(temporaryDirectory, "assembly-parent-link");
				Directory.CreateSymbolicLink(parentLink, realAssemblyDirectory);
				var parentResult = RunAotGenerator(root, generator, descriptorInput,
					Path.Combine(temporaryDirectory, "parent.g.cs"),
					ManagedAssemblyPaths(root, Path.Combine(parentLink, "OpenRA.Game.dll")));

				var leafDirectory = Path.Combine(temporaryDirectory, "leaf");
				Directory.CreateDirectory(leafDirectory);
				var leafLink = Path.Combine(leafDirectory, "OpenRA.Game.dll");
				File.CreateSymbolicLink(leafLink, Path.Combine(realAssemblyDirectory, "OpenRA.Game.dll"));
				var leafResult = RunAotGenerator(root, generator, descriptorInput,
					Path.Combine(temporaryDirectory, "leaf.g.cs"), ManagedAssemblyPaths(root, leafLink));

				Assert.Multiple(() =>
				{
					Assert.That(parentResult.ExitCode, Is.Not.Zero,
						"A managed assembly path with a symlink parent was accepted." +
						Environment.NewLine + parentResult.Diagnostics);
					Assert.That(leafResult.ExitCode, Is.Not.Zero,
						"A managed assembly leaf symlink was accepted." +
						Environment.NewLine + leafResult.Diagnostics);
				});
			}
			finally
			{
				Directory.Delete(temporaryDirectory, true);
			}
		}

		[Test]
		public void GeneratorRejectsNonRegularAssemblyPaths()
		{
			var root = RepositoryRoot();
			var temporaryDirectory = CreateTemporaryDirectory(root, "openra-aot-nonregular-");
			try
			{
				var generator = BuildAotGenerator(root);
				var descriptorInput = WriteDescriptorInput(root, temporaryDirectory);
				AssertGeneratorAcceptsRegularInputs(root, generator, descriptorInput,
					Path.Combine(temporaryDirectory, "control.g.cs"));
				var nonRegularPath = Path.Combine(temporaryDirectory, "OpenRA.Game.dll");
				if (OperatingSystem.IsWindows())
					Directory.CreateDirectory(nonRegularPath);
				else
				{
					var mkfifo = new ProcessStartInfo
					{
						FileName = "/usr/bin/mkfifo",
						WorkingDirectory = temporaryDirectory,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						UseShellExecute = false,
					};
					mkfifo.ArgumentList.Add(nonRegularPath);
					var mkfifoResult = RunProcess(mkfifo, 5000, "creating the non-regular FIFO fixture");
					Assert.That(mkfifoResult.ExitCode, Is.Zero, mkfifoResult.Diagnostics);
				}
				var result = RunAotGenerator(root, generator, descriptorInput,
					Path.Combine(temporaryDirectory, "nonregular.g.cs"),
					ManagedAssemblyPaths(root, nonRegularPath), 5000);

				Assert.That(result.ExitCode, Is.Not.Zero,
					"A non-regular file was accepted as a managed assembly input." +
					Environment.NewLine + result.Diagnostics);
			}
			finally
			{
				Directory.Delete(temporaryDirectory, true);
			}
		}

		[Test]
		public void GeneratorRejectsManagedAssemblyPathIdentityDrift()
		{
			var root = RepositoryRoot();
			var temporaryDirectory = CreateTemporaryDirectory(root, "openra-aot-identity-drift-");
			var stopMutating = false;
			Exception mutationError = null;
			Thread mutator = null;
			try
			{
				var generator = BuildAotGenerator(root);
				var descriptorInput = WriteDescriptorInput(root, temporaryDirectory);
				AssertGeneratorAcceptsRegularInputs(root, generator, descriptorInput,
					Path.Combine(temporaryDirectory, "control.g.cs"));
				var sourceAssembly = Path.Combine(root, "engine", "bin", "OpenRA.Game.dll");
				var changingAssembly = Path.Combine(temporaryDirectory, "OpenRA.Game.dll");
				WritePaddedAssemblyCopy(sourceAssembly, changingAssembly);
				var mutationCount = 0;
				mutator = new Thread(() =>
				{
					try
					{
						while (!Volatile.Read(ref stopMutating))
						{
							var replacement = Path.Combine(temporaryDirectory,
								"replacement-" + Guid.NewGuid().ToString("N") + ".dll");
							WritePaddedAssemblyCopy(sourceAssembly, replacement);
							File.Move(replacement, changingAssembly, true);
							Interlocked.Increment(ref mutationCount);
						}
					}
					catch (Exception e)
					{
						mutationError = e;
					}
				});
				mutator.Start();
				Assert.That(SpinWait.SpinUntil(() => Volatile.Read(ref mutationCount) >= 2, 5000),
					Is.True, "The identity-drift fixture did not start mutating the assembly path.");

				var result = RunAotGenerator(root, generator, descriptorInput,
					Path.Combine(temporaryDirectory, "identity-drift.g.cs"),
					ManagedAssemblyPaths(root, changingAssembly));
				Volatile.Write(ref stopMutating, true);
				mutator.Join();

				Assert.Multiple(() =>
				{
					Assert.That(mutationError, Is.Null);
					Assert.That(mutationCount, Is.GreaterThanOrEqualTo(2));
					Assert.That(result.ExitCode, Is.Not.Zero,
						"A managed assembly path whose file identity changed was accepted." +
						Environment.NewLine + result.Diagnostics);
				});
			}
			finally
			{
				Volatile.Write(ref stopMutating, true);
				if (mutator != null && mutator.IsAlive)
					mutator.Join();
				Directory.Delete(temporaryDirectory, true);
			}
		}

		[Test]
		public void GeneratorRejectsPreclosedDependencyCandidateIdentityDrift()
		{
			var root = RepositoryRoot();
			var temporaryDirectory = CreateTemporaryDirectory(root, "openra-aot-candidate-drift-");
			var stopMutating = false;
			Exception mutationError = null;
			Thread mutator = null;
			try
			{
				var candidateDirectory = Path.Combine(temporaryDirectory, "candidates");
				Directory.CreateDirectory(candidateDirectory);
				var sourceDirectory = Path.Combine(root, "engine", "bin");
				var changingAssembly = Path.Combine(candidateDirectory, "OpenRA.Mods.Common.dll");
				foreach (var source in Directory.GetFiles(sourceDirectory, "*.dll"))
				{
					var destination = Path.Combine(candidateDirectory, Path.GetFileName(source));
					if (string.Equals(destination, changingAssembly, StringComparison.Ordinal))
						WritePaddedAssemblyCopy(source, destination);
					else
						File.Copy(source, destination);
				}

				var mutationCount = 0;
				mutator = new Thread(() =>
				{
					try
					{
						var source = Path.Combine(sourceDirectory, "OpenRA.Mods.Common.dll");
						while (!Volatile.Read(ref stopMutating))
						{
							var replacement = Path.Combine(candidateDirectory,
								"common-" + Guid.NewGuid().ToString("N") + ".replacement");
							WritePaddedAssemblyCopy(source, replacement);
							File.Move(replacement, changingAssembly, true);
							Interlocked.Increment(ref mutationCount);
						}
					}
					catch (Exception e)
					{
						mutationError = e;
					}
				});
				mutator.Start();
				Assert.That(SpinWait.SpinUntil(() => Volatile.Read(ref mutationCount) >= 2, 5000),
					Is.True, "The dependency-candidate drift fixture did not start mutating.");

				var generator = BuildAotGenerator(root);
				var descriptorInput = WriteDescriptorInput(root, temporaryDirectory);
				var result = RunAotGenerator(root, generator, descriptorInput,
					Path.Combine(temporaryDirectory, "candidate-drift.g.cs"),
					new[] { Path.Combine(candidateDirectory, "OpenRA.Game.dll") });
				Volatile.Write(ref stopMutating, true);
				mutator.Join();

				Assert.Multiple(() =>
				{
					Assert.That(mutationError, Is.Null);
					Assert.That(mutationCount, Is.GreaterThanOrEqualTo(2));
					Assert.That(result.ExitCode, Is.Not.Zero, result.Diagnostics);
					Assert.That(result.Diagnostics, Does.Contain("managed assembly candidate " + changingAssembly));
					Assert.That(result.Diagnostics, Does.Contain("changed"));
				});
			}
			finally
			{
				Volatile.Write(ref stopMutating, true);
				if (mutator != null && mutator.IsAlive)
					mutator.Join();
				Directory.Delete(temporaryDirectory, true);
			}
		}

		[Test]
		public void GeneratorRejectsSameSimpleNameWithDifferentAssemblyVersion()
		{
			var root = RepositoryRoot();
			var temporaryDirectory = CreateTemporaryDirectory(root, "openra-aot-version-identity-");
			try
			{
				var generator = BuildAotGenerator(root);
				var descriptorInput = WriteDescriptorInput(root, temporaryDirectory);
				var fixture = BuildAssemblyIdentityFixture(temporaryDirectory,
					new Version(1, 0, 0, 0), null, null,
					new Version(2, 0, 0, 0), null, null);
				var result = RunAotGenerator(root, generator, descriptorInput,
					Path.Combine(temporaryDirectory, "version-mismatch.g.cs"),
					new[] { fixture.ConsumerAssembly, fixture.CandidateAssembly });

				Assert.That(result.ExitCode, Is.Not.Zero,
					"A dependency candidate with the requested simple name but a different version was accepted." +
					Environment.NewLine + result.Diagnostics);
			}
			finally
			{
				Directory.Delete(temporaryDirectory, true);
			}
		}

		[Test]
		public void GeneratorRejectsSameSimpleNameWithDifferentAssemblyCulture()
		{
			var root = RepositoryRoot();
			var temporaryDirectory = CreateTemporaryDirectory(root, "openra-aot-culture-identity-");
			try
			{
				var generator = BuildAotGenerator(root);
				var descriptorInput = WriteDescriptorInput(root, temporaryDirectory);
				var fixture = BuildAssemblyIdentityFixture(temporaryDirectory,
					new Version(1, 0, 0, 0), "fr", null,
					new Version(1, 0, 0, 0), "de", null);
				var result = RunAotGenerator(root, generator, descriptorInput,
					Path.Combine(temporaryDirectory, "culture-mismatch.g.cs"),
					new[] { fixture.ConsumerAssembly, fixture.CandidateAssembly });

				Assert.That(result.ExitCode, Is.Not.Zero,
					"A dependency candidate with the requested simple name but a different culture was accepted." +
					Environment.NewLine + result.Diagnostics);
			}
			finally
			{
				Directory.Delete(temporaryDirectory, true);
			}
		}

		[Test]
		public void GeneratorRejectsSameSimpleNameWithDifferentPublicKeyToken()
		{
			var root = RepositoryRoot();
			var temporaryDirectory = CreateTemporaryDirectory(root, "openra-aot-token-identity-");
			try
			{
				var referencedPublicKey = typeof(TestAttribute).Assembly.GetName().GetPublicKey();
				var candidatePublicKey = typeof(JsonDocument).Assembly.GetName().GetPublicKey();
				Assert.That(referencedPublicKey, Is.Not.Null.And.Not.Empty);
				Assert.That(candidatePublicKey, Is.Not.Null.And.Not.Empty);
				Assert.That(PublicKeyToken(referencedPublicKey),
					Is.Not.EqualTo(PublicKeyToken(candidatePublicKey)));

				var generator = BuildAotGenerator(root);
				var descriptorInput = WriteDescriptorInput(root, temporaryDirectory);
				var fixture = BuildAssemblyIdentityFixture(temporaryDirectory,
					new Version(1, 0, 0, 0), null, referencedPublicKey,
					new Version(1, 0, 0, 0), null, candidatePublicKey);
				var result = RunAotGenerator(root, generator, descriptorInput,
					Path.Combine(temporaryDirectory, "token-mismatch.g.cs"),
					new[] { fixture.ConsumerAssembly, fixture.CandidateAssembly });

				Assert.That(result.ExitCode, Is.Not.Zero,
					"A dependency candidate with the requested simple name but a different token was accepted." +
					Environment.NewLine + result.Diagnostics);
			}
			finally
			{
				Directory.Delete(temporaryDirectory, true);
			}
		}

		[Test]
		public void GeneratorRejectsCaseAliasedCandidateDirectories()
		{
			var root = RepositoryRoot();
			var temporaryDirectory = CreateTemporaryDirectory(root, "openra-aot-directory-alias-");
			try
			{
				var canonicalDirectory = Path.Combine(temporaryDirectory, "Candidates");
				var aliasDirectory = Path.Combine(temporaryDirectory, "candidates");
				Directory.CreateDirectory(canonicalDirectory);
				if (!Directory.Exists(aliasDirectory))
					Directory.CreateDirectory(aliasDirectory);
				var gameAssembly = Path.Combine(canonicalDirectory, "OpenRA.Game.dll");
				var commonAssembly = Path.Combine(aliasDirectory, "OpenRA.Mods.Common.dll");
				File.Copy(Path.Combine(root, "engine", "bin", "OpenRA.Game.dll"), gameAssembly);
				File.Copy(Path.Combine(root, "engine", "bin", "OpenRA.Mods.Common.dll"), commonAssembly);

				var generator = BuildAotGenerator(root);
				var descriptorInput = WriteDescriptorInput(root, temporaryDirectory);
				var result = RunAotGenerator(root, generator, descriptorInput,
					Path.Combine(temporaryDirectory, "directory-alias.g.cs"),
					new[] { gameAssembly, commonAssembly });

				Assert.Multiple(() =>
				{
					Assert.That(result.ExitCode, Is.Not.Zero);
					Assert.That(result.Diagnostics, Does.Contain(
						"Managed assembly directories are duplicated or case-aliased."));
				});
			}
			finally
			{
				Directory.Delete(temporaryDirectory, true);
			}
		}

		[Test]
		public void GeneratorRejectsDuplicateFullAssemblyIdentityAcrossCandidateDirectories()
		{
			var root = RepositoryRoot();
			var temporaryDirectory = CreateTemporaryDirectory(root, "openra-aot-duplicate-identity-");
			try
			{
				var firstDirectory = Path.Combine(temporaryDirectory, "first");
				var secondDirectory = Path.Combine(temporaryDirectory, "second");
				Directory.CreateDirectory(firstDirectory);
				Directory.CreateDirectory(secondDirectory);
				var firstAssembly = Path.Combine(firstDirectory, "OpenRA.Game.dll");
				var secondAssembly = Path.Combine(secondDirectory, "OpenRA.Game.dll");
				var source = Path.Combine(root, "engine", "bin", "OpenRA.Game.dll");
				File.Copy(source, firstAssembly);
				File.Copy(source, secondAssembly);

				var generator = BuildAotGenerator(root);
				var descriptorInput = WriteDescriptorInput(root, temporaryDirectory);
				var result = RunAotGenerator(root, generator, descriptorInput,
					Path.Combine(temporaryDirectory, "duplicate-identity.g.cs"),
					new[] { firstAssembly, secondAssembly });

				Assert.Multiple(() =>
				{
					Assert.That(result.ExitCode, Is.Not.Zero);
					Assert.That(result.Diagnostics, Does.Contain("Duplicate managed assembly identity OpenRA.Game"));
				});
			}
			finally
			{
				Directory.Delete(temporaryDirectory, true);
			}
		}

		[Test]
		public void GeneratorRejectsConflictingFullIdentitiesBeforeReflectionWhenOneLoadsFirst()
		{
			var root = RepositoryRoot();
			var temporaryDirectory = CreateTemporaryDirectory(root, "openra-aot-coexisting-identity-");
			try
			{
				var versionOne = BuildSyntheticAssembly(temporaryDirectory, "dependency-v1",
					"IdentityDependency", new Version(1, 0, 0, 0), null, null,
					"namespace IdentityDependency { public class BaseThing { } }");
				var versionTwo = BuildSyntheticAssembly(temporaryDirectory, "dependency-v2",
					"IdentityDependency", new Version(2, 0, 0, 0), null, null,
					"namespace IdentityDependency { public class BaseThing { } }");
				var consumerOne = BuildSyntheticConsumer(temporaryDirectory, "consumer-v1",
					"IdentityConsumerV1", "ConsumerThingV1", versionOne);
				var consumerTwo = BuildSyntheticConsumer(temporaryDirectory, "consumer-v2",
					"IdentityConsumerV2", "ConsumerThingV2", versionTwo);
				File.Copy(versionOne, Path.Combine(Path.GetDirectoryName(consumerOne),
					"IdentityDependency.dll"));
				File.Copy(versionTwo, Path.Combine(Path.GetDirectoryName(consumerTwo),
					"IdentityDependency.dll"));

				var generator = BuildAotGenerator(root);
				var descriptorInput = WriteDescriptorInput(root, temporaryDirectory);
				var output = Path.Combine(temporaryDirectory, "coexisting-identity.g.cs");
				var result = RunAotGenerator(root, generator, descriptorInput, output,
					new[] { consumerTwo, consumerOne });

				Assert.Multiple(() =>
				{
					Assert.That(result.ExitCode, Is.Not.Zero,
						"Default ALC incorrectly allowed the first simple-name load to satisfy another full identity." +
						Environment.NewLine + result.Diagnostics);
					Assert.That(result.Diagnostics, Does.Contain(
						"Conflicting managed assembly identities share simple name IdentityDependency:"));
					Assert.That(File.Exists(output), Is.False,
						"The conflict must fail during candidate preclosure, before reflection emits output.");
				});
			}
			finally
			{
				Directory.Delete(temporaryDirectory, true);
			}
		}

		static List<MiniYamlNode> ReadYaml(string root, string relativePath)
		{
			var path = Path.Combine(root, relativePath);
			using var stream = File.OpenRead(path);
			return MiniYaml.FromStream(stream, path);
		}

		static List<MiniYamlNode> Partition(IEnumerable<MiniYamlNode> nodes, IEnumerable<string> keys)
		{
			var keySet = new HashSet<string>(keys, StringComparer.Ordinal);
			return nodes.Where(node => keySet.Contains(node.Key)).ToList();
		}

		static byte[] CanonicalYaml(IEnumerable<MiniYamlNode> nodes) =>
			StrictUtf8.GetBytes(nodes.WriteToString());

		static byte[] FrameManifestSemantics(IReadOnlyList<MiniYamlNode> common,
			IReadOnlyList<MiniYamlNode> gameplay, IReadOnlyList<MiniYamlNode> fileSystem)
		{
			using var output = new MemoryStream();
			WriteLengthPrefixed(output, ManifestSemanticMagic);
			WriteUInt32(output, ManifestSemanticSchemaVersion);
			foreach (var partition in new[]
			{
				("common", CanonicalYaml(common)),
				("gameplay", CanonicalYaml(gameplay)),
				("filesystem", CanonicalYaml(fileSystem)),
			})
			{
				WriteLengthPrefixed(output, partition.Item1);
				WriteByteBlock(output, partition.Item2);
			}

			return output.ToArray();
		}

		static void WriteByteBlock(Stream output, byte[] bytes)
		{
			WriteUInt32(output, checked((uint)bytes.Length));
			output.Write(bytes, 0, bytes.Length);
		}

		static void AssertEntryIncludes(string modRoot, string entryName, string[] expected)
		{
			var nodes = ReadYaml(modRoot, entryName);
			Assert.That(nodes, Has.Count.EqualTo(expected.Length));
			for (var i = 0; i < expected.Length; i++)
			{
				Assert.Multiple(() =>
				{
					Assert.That(nodes[i].Key, Is.EqualTo("Include"), $"{entryName} row {i}");
					Assert.That(nodes[i].Value.Value, Is.EqualTo(expected[i]), $"{entryName} row {i}");
					Assert.That(nodes[i].Value.Nodes, Is.Empty, $"{entryName} row {i}");
				});
			}
		}

		static void AssertCommonManifestArraysEqual(Manifest expected, Manifest actual)
		{
			Assert.Multiple(() =>
			{
				Assert.That(actual.Cursors, Is.EqualTo(expected.Cursors), "Cursors");
				Assert.That(actual.Chrome, Is.EqualTo(expected.Chrome), "Chrome");
				Assert.That(actual.Assemblies, Is.EqualTo(expected.Assemblies), "Assemblies");
				Assert.That(actual.ChromeLayout, Is.EqualTo(expected.ChromeLayout), "ChromeLayout");
				Assert.That(actual.ServerTraits, Is.EqualTo(expected.ServerTraits), "ServerTraits");
				Assert.That(actual.ChromeMetrics, Is.EqualTo(expected.ChromeMetrics), "ChromeMetrics");
				Assert.That(actual.SoundFormats, Is.EqualTo(expected.SoundFormats), "SoundFormats");
				Assert.That(actual.SpriteFormats, Is.EqualTo(expected.SpriteFormats), "SpriteFormats");
				Assert.That(actual.VideoFormats, Is.EqualTo(expected.VideoFormats), "VideoFormats");
				Assert.That(actual.MapCompatibility, Is.EqualTo(expected.MapCompatibility), "SupportsMapsFrom");
			});
		}

		static void AssertManifestArraysEqual(Manifest expected, Manifest actual)
		{
			AssertCommonManifestArraysEqual(expected, actual);
			Assert.Multiple(() =>
			{
				Assert.That(actual.Rules, Is.EqualTo(expected.Rules), "Rules");
				Assert.That(actual.Sequences, Is.EqualTo(expected.Sequences), "Sequences");
				Assert.That(actual.ModelSequences, Is.EqualTo(expected.ModelSequences), "ModelSequences");
				Assert.That(actual.Weapons, Is.EqualTo(expected.Weapons), "Weapons");
				Assert.That(actual.Voices, Is.EqualTo(expected.Voices), "Voices");
				Assert.That(actual.Notifications, Is.EqualTo(expected.Notifications), "Notifications");
				Assert.That(actual.TileSets, Is.EqualTo(expected.TileSets), "TileSets");
				Assert.That(actual.Music, Is.EqualTo(expected.Music), "Music");
				Assert.That(actual.FluentMessages, Is.EqualTo(expected.FluentMessages), "FluentMessages");
				Assert.That(actual.Hotkeys, Is.EqualTo(expected.Hotkeys), "Hotkeys");
			});
		}

		static void AssertBaseGameplayProjection(Manifest desktop, Manifest baseProfile)
		{
			Assert.Multiple(() =>
			{
				Assert.That(baseProfile.Rules, Is.EqualTo(BaseProjection(desktop.Rules)), "Base Rules");
				Assert.That(baseProfile.Sequences, Is.EqualTo(BaseProjection(desktop.Sequences)), "Base Sequences");
				Assert.That(baseProfile.ModelSequences, Is.EqualTo(BaseProjection(desktop.ModelSequences)),
					"Base ModelSequences");
				Assert.That(baseProfile.Weapons, Is.EqualTo(BaseProjection(desktop.Weapons)), "Base Weapons");
				Assert.That(baseProfile.Voices, Is.EqualTo(BaseProjection(desktop.Voices)), "Base Voices");
				Assert.That(baseProfile.Notifications, Is.EqualTo(desktop.Notifications), "Base Notifications");
				Assert.That(baseProfile.TileSets, Is.EqualTo(desktop.TileSets), "Base TileSets");
				Assert.That(baseProfile.Music, Is.EqualTo(desktop.Music), "Base Music");
				Assert.That(baseProfile.FluentMessages, Is.EqualTo(desktop.FluentMessages), "Base FluentMessages");
				Assert.That(baseProfile.Hotkeys, Is.EqualTo(desktop.Hotkeys), "Base Hotkeys");
			});
		}

		static string[] BaseProjection(IEnumerable<string> desktop) => desktop
			.Where(path => !YuriOnlyWholeGameplayFiles.Contains(path, StringComparer.Ordinal))
			.Select(path => BaseGameplayReplacements.TryGetValue(path, out var replacement) ? replacement : path)
			.ToArray();

		static IEnumerable<string> ManifestGameplayFiles(Manifest manifest) => manifest.Rules
			.Concat(manifest.Sequences)
			.Concat(manifest.ModelSequences)
			.Concat(manifest.Weapons)
			.Concat(manifest.Voices)
			.Concat(manifest.Notifications)
			.Concat(manifest.TileSets)
			.Concat(manifest.Music)
			.Concat(manifest.FluentMessages)
			.Concat(manifest.Hotkeys);

		static void AssertFileSystemProjection(IReadOnlyList<MiniYamlNode> desktopNodes,
			IReadOnlyList<MiniYamlNode> baseNodes, IReadOnlyList<MiniYamlNode> yuriNodes)
		{
			var desktopPackageFormats = SingleNode(desktopNodes, "PackageFormats");
			var desktopFileSystem = SingleNode(desktopNodes, "FileSystem");
			var desktopMapFolders = SingleNode(desktopNodes, "MapFolders");
			var basePackageFormats = SingleNode(baseNodes, "PackageFormats");
			var baseFileSystem = SingleNode(baseNodes, "FileSystem");
			var baseMapFolders = SingleNode(baseNodes, "MapFolders");
			var yuriPackageFormats = SingleNode(yuriNodes, "PackageFormats");
			var yuriFileSystem = SingleNode(yuriNodes, "FileSystem");
			var yuriMapFolders = SingleNode(yuriNodes, "MapFolders");

			Assert.Multiple(() =>
			{
				Assert.That(baseNodes.Select(n => n.Key), Is.EqualTo(FileSystemManifestKeys));
				Assert.That(yuriNodes.Select(n => n.Key), Is.EqualTo(FileSystemManifestKeys));
				Assert.That(CanonicalYaml(new[] { basePackageFormats }),
					Is.EqualTo(CanonicalYaml(new[] { desktopPackageFormats })));
				Assert.That(CanonicalYaml(new[] { yuriPackageFormats }),
					Is.EqualTo(CanonicalYaml(new[] { desktopPackageFormats })));
				Assert.That(baseFileSystem.Value.Value, Is.EqualTo("ContentInstallerFileSystem"));
				Assert.That(yuriFileSystem.Value.Value, Is.EqualTo("ContentInstallerFileSystem"));
			});

			var desktopSystem = SingleNode(desktopFileSystem.Value.Nodes, "SystemPackages");
			var baseSystem = SingleNode(baseFileSystem.Value.Nodes, "SystemPackages");
			var yuriSystem = SingleNode(yuriFileSystem.Value.Nodes, "SystemPackages");
			var desktopPackages = SingleNode(desktopFileSystem.Value.Nodes, "ContentPackages");
			var basePackages = SingleNode(baseFileSystem.Value.Nodes, "ContentPackages");
			var yuriPackages = SingleNode(yuriFileSystem.Value.Nodes, "ContentPackages");
			var desktopInstaller = SingleNode(desktopFileSystem.Value.Nodes, "ContentInstallerMod");
			var baseInstaller = SingleNode(baseFileSystem.Value.Nodes, "ContentInstallerMod");
			var yuriInstaller = SingleNode(yuriFileSystem.Value.Nodes, "ContentInstallerMod");
			var expectedDesktopSystem = new[]
			{
				"^EngineDir\t", "$ra2\tra2", "$ts\tts", "^EngineDir|mods/common\tcommon",
				"~^SupportDir|Content/ra2\tcontent",
			};
			var expectedIosSystem = new[]
			{
				"^EngineDir\t", "$ra2\tra2", "$ts\tts", "^EngineDir|mods/common\tcommon",
				"~^SupportDir|Content/ra2\tcontent",
			};
			Assert.Multiple(() =>
			{
				Assert.That(desktopFileSystem.Value.Nodes.Select(n => n.Key), Is.EqualTo(new[]
				{
					"SystemPackages", "ContentInstallerMod", "ContentPackages",
				}));
				Assert.That(baseFileSystem.Value.Nodes.Select(n => n.Key), Is.EqualTo(new[]
				{
					"SystemPackages", "ContentInstallerMod", "ContentPackages",
				}));
				Assert.That(yuriFileSystem.Value.Nodes.Select(n => n.Key), Is.EqualTo(new[]
				{
					"SystemPackages", "ContentInstallerMod", "ContentPackages",
				}));
				Assert.That(desktopInstaller.Value.Value, Is.EqualTo("ra2-content"));
				Assert.That(baseInstaller.Value.Value, Is.EqualTo("ra2-content"));
				Assert.That(yuriInstaller.Value.Value, Is.EqualTo("ra2-content"));
				Assert.That(desktopInstaller.Value.Nodes, Is.Empty);
				Assert.That(baseInstaller.Value.Nodes, Is.Empty);
				Assert.That(yuriInstaller.Value.Nodes, Is.Empty);
				Assert.That(NodeRows(desktopSystem), Is.EqualTo(expectedDesktopSystem));
				Assert.That(NodeRows(baseSystem), Is.EqualTo(expectedIosSystem));
				Assert.That(NodeRows(yuriSystem), Is.EqualTo(expectedIosSystem));
				Assert.That(NodeRows(desktopPackages), Is.EqualTo(YuriContentPackages.Concat(SharedContentPackages)));
				Assert.That(NodeRows(yuriPackages), Is.EqualTo(YuriContentPackages.Concat(SharedContentPackages)));
				Assert.That(NodeRows(basePackages), Is.EqualTo(SharedContentPackages));
				Assert.That(NodeRows(desktopMapFolders), Is.EqualTo(new[]
				{
					"ra2|maps\tSystem", "^SupportDir|Content/ra2/maps\tUser",
					"~^SupportDir|maps/ra2/{DEV_VERSION}\tUser",
				}));
				Assert.That(NodeRows(baseMapFolders), Is.EqualTo(new[]
				{
					"ra2|maps\tSystem", "^SupportDir|Content/ra2/maps\tUser",
				}));
				Assert.That(NodeRows(yuriMapFolders), Is.EqualTo(new[]
				{
					"ra2|maps\tSystem", "^SupportDir|Content/ra2/maps\tUser",
				}));
			});

			var iosFileSystemText = StrictUtf8.GetString(CanonicalYaml(baseNodes)) +
				StrictUtf8.GetString(CanonicalYaml(yuriNodes));
			StringAssert.Contains("~^SupportDir|Content/ra2", iosFileSystemText);
		}

		static MiniYamlNode SingleNode(IEnumerable<MiniYamlNode> nodes, string key) =>
			nodes.Single(node => node.Key == key);

		static string[] NodeRows(MiniYamlNode node) => node.Value.Nodes
			.Select(child => child.Key + "\t" + (child.Value.Value ?? string.Empty)).ToArray();

		static void AssertClosedSubtraction(string root, string sourcePath, string basePath,
			IEnumerable<string> removedPaths, IEnumerable<SemanticChange> changes)
		{
			var source = ReadYaml(root, sourcePath);
			var baseCopy = ReadYaml(root, basePath);
			var remainingRemovals = new HashSet<string>(removedPaths, StringComparer.Ordinal);
			var changeMap = changes.ToDictionary(change => change.Path, StringComparer.Ordinal);
			var remainingChanges = new HashSet<string>(changeMap.Keys, StringComparer.Ordinal);
			AssertClosedSubtractionNodes(source, baseCopy, string.Empty, remainingRemovals,
				changeMap, remainingChanges, basePath);
			Assert.Multiple(() =>
			{
				Assert.That(remainingRemovals, Is.Empty, $"unused removal fixture for {basePath}");
				Assert.That(remainingChanges, Is.Empty, $"unused change fixture for {basePath}");
			});
		}

		static void AssertClosedSubtractionNodes(IReadOnlyList<MiniYamlNode> source,
			IReadOnlyList<MiniYamlNode> baseCopy, string parentPath, ISet<string> remainingRemovals,
			IReadOnlyDictionary<string, SemanticChange> changes, ISet<string> remainingChanges, string label)
		{
			var baseIndex = 0;
			foreach (var sourceNode in source)
			{
				var path = string.IsNullOrEmpty(parentPath) ? sourceNode.Key : parentPath + "/" + sourceNode.Key;
				if (remainingRemovals.Remove(path))
					continue;

				Assert.That(baseIndex, Is.LessThan(baseCopy.Count), $"unexpected removal at {label}:{path}");
				var baseNode = baseCopy[baseIndex++];
				Assert.That(baseNode.Key, Is.EqualTo(sourceNode.Key), $"addition/reorder at {label}:{path}");
				if (changes.TryGetValue(path, out var change) && remainingChanges.Remove(path))
				{
					Assert.Multiple(() =>
					{
						Assert.That(sourceNode.Value.Value, Is.EqualTo(change.SourceValue), $"source fixture {path}");
						Assert.That(baseNode.Value.Value, Is.EqualTo(change.BaseValue), $"Base change {path}");
					});
				}
				else
					Assert.That(baseNode.Value.Value, Is.EqualTo(sourceNode.Value.Value), $"scalar drift at {label}:{path}");

				AssertClosedSubtractionNodes(sourceNode.Value.Nodes, baseNode.Value.Nodes, path,
					remainingRemovals, changes, remainingChanges, label);
			}

			Assert.That(baseIndex, Is.EqualTo(baseCopy.Count), $"unexpected Base addition under {label}:{parentPath}");
		}

		static void AssertCanonicalGolden(GoldenShape golden)
		{
			var logicalNames = new HashSet<string>(StringComparer.Ordinal);
			var logicalAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var bundlePaths = new HashSet<string>(StringComparer.Ordinal);
			var bundleAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for (var i = 0; i < golden.Descriptors.Count; i++)
			{
				var descriptor = golden.Descriptors[i];
				AssertCanonicalString(descriptor.SourcePath, $"sourcePath[{i}]");
				AssertCanonicalRelativePath(descriptor.LogicalName, $"logicalName[{i}]");
				AssertCanonicalRelativePath(descriptor.BundleRelativePath, $"bundleRelativePath[{i}]");
				Assert.That(descriptor.LogicalName, Is.EqualTo(descriptor.BundleRelativePath));
				Assert.That(descriptor.SourcePath, Does.StartWith("../../"));
				Assert.That(descriptor.Profiles, Is.EqualTo(descriptor.Profiles.Contains("Base") &&
					descriptor.Profiles.Contains("Yuri") ? new[] { "Base", "Yuri" } : descriptor.Profiles));
				Assert.That(descriptor.Profiles, Is.Not.Empty);
				Assert.That(descriptor.Profiles, Is.All.AnyOf("Base", "Yuri"));
				Assert.That(descriptor.Profiles.Distinct(StringComparer.Ordinal).Count(),
					Is.EqualTo(descriptor.Profiles.Length));
				Assert.That(descriptor.Purpose, Is.AnyOf("Contract", "PreflightOnly"));
				Assert.That(logicalNames.Add(descriptor.LogicalName), Is.True);
				Assert.That(logicalAliases.Add(descriptor.LogicalName), Is.True);
				Assert.That(bundlePaths.Add(descriptor.BundleRelativePath), Is.True);
				Assert.That(bundleAliases.Add(descriptor.BundleRelativePath), Is.True);
				if (i > 0)
					Assert.That(CompareUtf8(golden.Descriptors[i - 1].LogicalName, descriptor.LogicalName),
						Is.LessThan(0), $"descriptor row {i} is not in canonical UTF-8 byte order");
			}
		}

		static void AssertEvaluatedDescriptors(GoldenShape golden, IReadOnlyList<EvaluatedItem> actual,
			string configuration)
		{
			Assert.That(actual, Has.Count.EqualTo(GoldenDescriptorCount), configuration);
			for (var i = 0; i < golden.Descriptors.Count; i++)
			{
				var expected = golden.Descriptors[i];
				var item = actual[i];
				Assert.Multiple(() =>
				{
					Assert.That(item.Identity, Is.EqualTo(expected.SourcePath), $"{configuration} source row {i}");
					Assert.That(Metadata(item, "LogicalName"), Is.EqualTo(expected.LogicalName), $"{configuration} row {i}");
					Assert.That(Metadata(item, "BundleRelativePath"), Is.EqualTo(expected.BundleRelativePath), $"{configuration} row {i}");
					Assert.That(Metadata(item, "Profiles"), Is.EqualTo(string.Join(",", expected.Profiles)), $"{configuration} row {i}");
					Assert.That(Metadata(item, "Purpose"), Is.EqualTo(expected.Purpose), $"{configuration} row {i}");
				});
			}
		}

		static void AssertPersonalBundleTransport(GoldenShape golden, IReadOnlyList<EvaluatedItem> resources)
		{
			var derived = resources.Where(i => Metadata(i, "RuntimeContractDerived") == "true").ToArray();
			Assert.That(derived, Has.Length.EqualTo(GoldenDescriptorCount));
			for (var i = 0; i < golden.Descriptors.Count; i++)
			{
				var expected = golden.Descriptors[i];
				var item = derived[i];
				Assert.Multiple(() =>
				{
					Assert.That(item.Identity, Is.EqualTo(expected.SourcePath), $"bundle source row {i}");
					Assert.That(BundleTarget(item), Is.EqualTo(expected.BundleRelativePath), $"bundle target row {i}");
					Assert.That(Metadata(item, "Optimize"), Is.EqualTo("false"), $"bundle optimize row {i}");
					Assert.That(Metadata(item, "RuntimeContractDerived"), Is.EqualTo("true"), $"bundle marker row {i}");
				});
				Assert.That(resources.Count(r => BundleTarget(r) == expected.BundleRelativePath),
					Is.EqualTo(1), $"bundle target must be exact-once: {expected.BundleRelativePath}");
			}

			Assert.That(derived.Count(i => BundleTarget(i).EndsWith(".png", StringComparison.Ordinal)),
				Is.EqualTo(63));
			Assert.That(derived.Where(i => BundleTarget(i).EndsWith(".png", StringComparison.Ordinal))
				.All(i => Metadata(i, "Profiles") == "Base,Yuri" && Metadata(i, "Purpose") == "PreflightOnly"),
				Is.True);
		}

		static void AssertExactPublicCleanRemoves(string root)
		{
			var project = XDocument.Load(Path.Combine(root, "ios", "OpenRA.iOS", "OpenRA.iOS.csproj"),
				LoadOptions.PreserveWhitespace);
			var publicGroup = project.Root!.Elements("ItemGroup")
				.Single(e => (string)e.Attribute("Condition")! == "'$(PublicClean)' == 'true'");
			var children = publicGroup.Elements().ToArray();
			var lastInclude = Array.FindLastIndex(children,
				e => e.Name.LocalName == "BundleResource" && e.Attribute("Include") != null);
			var removes = children.Where(e => e.Name.LocalName == "BundleResource" &&
				e.Attribute("Remove") != null).ToArray();
			Assert.That(removes.Select(e => (string)e.Attribute("Remove")!),
				Is.EqualTo(PublicCleanProfileRemoves));
			Assert.That(removes, Is.All.Matches<XElement>(e => !((string)e.Attribute("Remove")!).Any(
				c => c is '*' or '?' or '[' or ']')));
			Assert.That(removes.Select(e => Array.IndexOf(children, e)), Is.All.GreaterThan(lastInclude));
		}

		static EvaluatedProject EvaluateIosProject(string root, bool publicClean)
		{
			var dotnet = DotnetHost();
			var start = new ProcessStartInfo
			{
				FileName = dotnet,
				WorkingDirectory = root,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			};
			start.ArgumentList.Add("msbuild");
			start.ArgumentList.Add(Path.Combine(root, "ios", "OpenRA.iOS", "OpenRA.iOS.csproj"));
			start.ArgumentList.Add("-nologo");
			start.ArgumentList.Add("-p:RuntimeIdentifier=iossimulator-arm64");
			start.ArgumentList.Add($"-p:PublicClean={publicClean.ToString().ToLowerInvariant()}");
			start.ArgumentList.Add("-getItem:RuntimeContractDescriptor,BundleResource");

			using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet msbuild.");
			var stdoutTask = process.StandardOutput.ReadToEndAsync();
			var stderrTask = process.StandardError.ReadToEndAsync();
			if (!process.WaitForExit(60000))
			{
				process.Kill(true);
				Assert.Fail("Timed out while evaluating the iOS project.");
			}

			var stdout = stdoutTask.GetAwaiter().GetResult();
			var stderr = stderrTask.GetAwaiter().GetResult();
			Assert.That(process.ExitCode, Is.Zero, stderr + Environment.NewLine + stdout);
			var jsonStart = stdout.IndexOf('{');
			Assert.That(jsonStart, Is.GreaterThanOrEqualTo(0), stdout);
			using var document = JsonDocument.Parse(stdout[jsonStart..]);
			var items = document.RootElement.GetProperty("Items");
			return new EvaluatedProject(
				ReadEvaluatedItems(items.GetProperty("RuntimeContractDescriptor")),
				ReadEvaluatedItems(items.GetProperty("BundleResource")));
		}

		static void RunIosGenerationTarget(string root, string inputPath, string outputPath)
		{
			var start = new ProcessStartInfo
			{
				FileName = DotnetHost(),
				WorkingDirectory = root,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			};
			start.ArgumentList.Add("msbuild");
			start.ArgumentList.Add(Path.Combine(root, "ios", "OpenRA.iOS", "OpenRA.iOS.csproj"));
			start.ArgumentList.Add("-nologo");
			start.ArgumentList.Add("-v:minimal");
			start.ArgumentList.Add("-p:WarningLevel=0");
			start.ArgumentList.Add("-p:BuildProjectReferences=false");
			start.ArgumentList.Add("-p:RuntimeIdentifier=iossimulator-arm64");
			start.ArgumentList.Add("-p:RuntimeContractDescriptorInputFile=" + inputPath);
			start.ArgumentList.Add("-p:GeneratedAotRegistryFile=" + outputPath);
			start.ArgumentList.Add("-t:GenerateAotObjectRegistry");

			using var process = Process.Start(start) ?? throw new InvalidOperationException(
				"Could not start the iOS AOT generation target.");
			var stdoutTask = process.StandardOutput.ReadToEndAsync();
			var stderrTask = process.StandardError.ReadToEndAsync();
			if (!process.WaitForExit(120000))
			{
				process.Kill(true);
				Assert.Fail("Timed out while executing the iOS AOT generation target.");
			}

			var stdout = stdoutTask.GetAwaiter().GetResult();
			var stderr = stderrTask.GetAwaiter().GetResult();
			Assert.That(process.ExitCode, Is.Zero, stderr + Environment.NewLine + stdout);
		}

		static string BuildAotGenerator(string root)
		{
			var project = Path.Combine(root, "build", "OpenRA.AotRegistry.Generator",
				"OpenRA.AotRegistry.Generator.csproj");
			var start = new ProcessStartInfo
			{
				FileName = DotnetHost(),
				WorkingDirectory = root,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			};
			start.ArgumentList.Add("build");
			start.ArgumentList.Add(project);
			start.ArgumentList.Add("--configuration");
			start.ArgumentList.Add("Release");
			start.ArgumentList.Add("--nologo");
			var result = RunProcess(start, 120000, "building the AOT registry generator");
			Assert.That(result.ExitCode, Is.Zero, result.Diagnostics);

			var generator = Path.Combine(root, "build", "OpenRA.AotRegistry.Generator",
				"bin", "Release", "net8.0", "OpenRA.AotRegistry.Generator.dll");
			Assert.That(File.Exists(generator), Is.True, generator);
			return generator;
		}

		static ProcessResult RunAotGenerator(string root, string generator, string descriptorInput,
			string output, IReadOnlyList<string> assemblies, int timeoutMilliseconds = 120000)
		{
			var start = new ProcessStartInfo
			{
				FileName = DotnetHost(),
				WorkingDirectory = Path.Combine(root, "ios", "OpenRA.iOS"),
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			};
			start.ArgumentList.Add(generator);
			start.ArgumentList.Add(output);
			start.ArgumentList.Add(descriptorInput);
			start.ArgumentList.Add(Path.Combine(root, "packaging",
				"ios-runtime-contract-descriptor-shape-v1.json"));
			start.ArgumentList.Add(GoldenFileSha256);
			foreach (var assembly in assemblies)
				start.ArgumentList.Add(assembly);
			return RunProcess(start, timeoutMilliseconds, "running the AOT registry generator");
		}

		static void AssertGeneratorAcceptsRegularInputs(string root, string generator,
			string descriptorInput, string output)
		{
			var gameAssembly = Path.Combine(root, "engine", "bin", "OpenRA.Game.dll");
			var result = RunAotGenerator(root, generator, descriptorInput, output,
				ManagedAssemblyPaths(root, gameAssembly));
			Assert.That(result.ExitCode, Is.Zero,
				"The valid control generator invocation failed." + Environment.NewLine + result.Diagnostics);
		}

		static ProcessResult RunProcess(ProcessStartInfo start, int timeoutMilliseconds, string operation)
		{
			using var process = Process.Start(start) ?? throw new InvalidOperationException(
				$"Could not start process while {operation}.");
			var stdoutTask = process.StandardOutput.ReadToEndAsync();
			var stderrTask = process.StandardError.ReadToEndAsync();
			if (!process.WaitForExit(timeoutMilliseconds))
			{
				process.Kill(true);
				Assert.Fail($"Timed out while {operation}.");
			}

			return new ProcessResult(process.ExitCode, stdoutTask.GetAwaiter().GetResult(),
				stderrTask.GetAwaiter().GetResult());
		}

		static string WriteDescriptorInput(string root, string temporaryDirectory)
		{
			var golden = ReadGolden(File.ReadAllBytes(Path.Combine(root, "packaging",
				"ios-runtime-contract-descriptor-shape-v1.json")));
			var path = Path.Combine(temporaryDirectory, "runtime-contract-descriptors-v1.tsv");
			File.WriteAllLines(path, golden.Descriptors.Select(descriptor => string.Join("\t",
				descriptor.SourcePath,
				descriptor.LogicalName,
				descriptor.BundleRelativePath,
				string.Join(",", descriptor.Profiles),
				descriptor.Purpose)), new UTF8Encoding(false));
			return path;
		}

		static string[] ManagedAssemblyPaths(string root, string gameAssembly) =>
			new[]
			{
				gameAssembly,
				Path.Combine(root, "engine", "bin", "OpenRA.Mods.Common.dll"),
				Path.Combine(root, "engine", "bin", "OpenRA.Mods.Cnc.dll"),
				Path.Combine(root, "engine", "bin", "OpenRA.Mods.RA2.dll"),
			};

		static string CreateTemporaryDirectory(string root, string prefix)
		{
			var path = Path.Combine(Path.GetDirectoryName(root)!, prefix + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(path);
			return path;
		}

		static void WritePaddedAssemblyCopy(string source, string destination)
		{
			File.Copy(source, destination);
			using var stream = new FileStream(destination, FileMode.Open, FileAccess.Write, FileShare.Read);
			stream.SetLength(64L * 1024 * 1024);
		}

		static string PublicKeyToken(byte[] publicKey)
		{
			var name = new AssemblyName();
			name.SetPublicKey(publicKey);
			return Convert.ToHexString(name.GetPublicKeyToken()).ToLowerInvariant();
		}

		static AssemblyIdentityFixture BuildAssemblyIdentityFixture(string temporaryDirectory,
			Version referencedVersion, string referencedCulture, byte[] referencedPublicKey,
			Version candidateVersion, string candidateCulture, byte[] candidatePublicKey)
		{
			var referencedAssembly = BuildSyntheticAssembly(temporaryDirectory, "referenced",
				"IdentityDependency", referencedVersion, referencedCulture, referencedPublicKey,
				"namespace IdentityDependency { public class BaseThing { } }");
			var consumerAssembly = BuildSyntheticConsumer(temporaryDirectory, referencedAssembly);
			var candidateAssembly = BuildSyntheticAssembly(temporaryDirectory, "candidate",
				"IdentityDependency", candidateVersion, candidateCulture, candidatePublicKey,
				"namespace IdentityDependency { public class BaseThing { } }");
			return new AssemblyIdentityFixture(consumerAssembly, candidateAssembly);
		}

		static string BuildSyntheticConsumer(string temporaryDirectory, string referencedAssembly) =>
			BuildSyntheticConsumer(temporaryDirectory, "consumer", "IdentityConsumer",
				"ConsumerThing", referencedAssembly);

		static string BuildSyntheticConsumer(string temporaryDirectory, string projectName,
			string assemblyName, string typeName, string referencedAssembly)
		{
			var projectDirectory = Path.Combine(temporaryDirectory, projectName + "-project");
			Directory.CreateDirectory(projectDirectory);
			var projectPath = Path.Combine(projectDirectory, projectName + ".csproj");
			var project = new XDocument(
				new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
					new XElement("PropertyGroup",
						new XElement("TargetFramework", "net8.0"),
						new XElement("AssemblyName", assemblyName),
						new XElement("GenerateAssemblyInfo", "false")),
					new XElement("ItemGroup",
						new XElement("Reference", new XAttribute("Include", "IdentityDependency"),
							new XElement("HintPath", referencedAssembly),
							new XElement("Private", "false")))));
			project.Save(projectPath);
			File.WriteAllText(Path.Combine(projectDirectory, "Consumer.cs"),
				$"namespace {assemblyName} {{ public sealed class {typeName} : IdentityDependency.BaseThing {{ }} }}");
			BuildSyntheticProject(projectPath);
			var outputs = Directory.GetFiles(projectDirectory, assemblyName + ".dll",
				SearchOption.AllDirectories).Where(path => path.Contains(Path.DirectorySeparatorChar +
					"bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToArray();
			Assert.That(outputs, Has.Length.EqualTo(1), string.Join(Environment.NewLine, outputs));
			return outputs[0];
		}

		static string BuildSyntheticAssembly(string temporaryDirectory, string projectName,
			string assemblyName, Version version, string culture, byte[] publicKey, string source)
		{
			var projectDirectory = Path.Combine(temporaryDirectory, projectName + "-project");
			Directory.CreateDirectory(projectDirectory);
			var propertyGroup = new XElement("PropertyGroup",
				new XElement("TargetFramework", "net8.0"),
				new XElement("AssemblyName", assemblyName),
				new XElement("GenerateAssemblyInfo", "false"));
			if (publicKey != null)
			{
				var keyPath = Path.Combine(projectDirectory, "identity-key.snk");
				File.WriteAllBytes(keyPath, publicKey);
				propertyGroup.Add(
					new XElement("SignAssembly", "true"),
					new XElement("PublicSign", "true"),
					new XElement("AssemblyOriginatorKeyFile", keyPath));
			}

			var projectPath = Path.Combine(projectDirectory, projectName + ".csproj");
			new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
				propertyGroup)).Save(projectPath);
			var attributes = "using System.Reflection;" + Environment.NewLine +
				$"[assembly: AssemblyVersion(\"{version}\")]" + Environment.NewLine +
				$"[assembly: AssemblyFileVersion(\"{version}\")]" + Environment.NewLine;
			if (!string.IsNullOrEmpty(culture))
				attributes += $"[assembly: AssemblyCulture(\"{culture}\")]" + Environment.NewLine;
			File.WriteAllText(Path.Combine(projectDirectory, "Fixture.cs"), attributes + source);
			BuildSyntheticProject(projectPath);
			var outputs = Directory.GetFiles(projectDirectory, assemblyName + ".dll",
				SearchOption.AllDirectories).Where(path => path.Contains(Path.DirectorySeparatorChar +
					"bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToArray();
			Assert.That(outputs, Has.Length.EqualTo(1), string.Join(Environment.NewLine, outputs));
			return outputs[0];
		}

		static void BuildSyntheticProject(string projectPath)
		{
			var start = new ProcessStartInfo
			{
				FileName = DotnetHost(),
				WorkingDirectory = Path.GetDirectoryName(projectPath),
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			};
			start.ArgumentList.Add("build");
			start.ArgumentList.Add(projectPath);
			start.ArgumentList.Add("--configuration");
			start.ArgumentList.Add("Release");
			start.ArgumentList.Add("--nologo");
			start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
			var result = RunProcess(start, 120000, "building a synthetic assembly identity fixture");
			Assert.That(result.ExitCode, Is.Zero, result.Diagnostics);
		}

		static IReadOnlyList<EvaluatedItem> ReadEvaluatedItems(JsonElement array)
		{
			return array.EnumerateArray().Select(item => new EvaluatedItem(
				item.GetProperty("Identity").GetString()!,
				item.EnumerateObject().Where(p => p.Name != "Identity")
					.ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty,
						StringComparer.Ordinal))).ToArray();
		}

		static string DotnetHost()
		{
			var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
			if (!string.IsNullOrEmpty(host) && File.Exists(host))
				return host;

			var root = Environment.GetEnvironmentVariable("DOTNET_ROOT");
			if (!string.IsNullOrEmpty(root))
			{
				var candidate = Path.Combine(root, "dotnet");
				if (File.Exists(candidate))
					return candidate;
			}

			const string PinnedHost = "/Users/kevin/.dotnet/dotnet";
			return File.Exists(PinnedHost) ? PinnedHost : "dotnet";
		}

		static GoldenShape ReadGolden(byte[] bytes)
		{
			using var document = JsonDocument.Parse(bytes);
			var root = document.RootElement;
			Assert.That(root.EnumerateObject().Select(p => p.Name), Is.EqualTo(new[]
			{
				"schemaVersion", "descriptorCount", "purposeCounts", "shapeDigest",
				"shapeFraming", "sourcePathRules", "descriptors"
			}));
			var descriptors = root.GetProperty("descriptors").EnumerateArray().Select(element =>
			{
				Assert.That(element.EnumerateObject().Select(p => p.Name), Is.EqualTo(new[]
				{
					"logicalName", "bundleRelativePath", "profiles", "purpose", "sourcePath"
				}));
				return new DescriptorShape(
					element.GetProperty("sourcePath").GetString()!,
					element.GetProperty("logicalName").GetString()!,
					element.GetProperty("bundleRelativePath").GetString()!,
					element.GetProperty("profiles").EnumerateArray().Select(p => p.GetString()!).ToArray(),
					element.GetProperty("purpose").GetString()!);
			}).ToArray();

			return new GoldenShape(
				root.GetProperty("schemaVersion").GetInt32(),
				root.GetProperty("descriptorCount").GetInt32(),
				root.GetProperty("shapeDigest").GetString()!,
				descriptors);
		}

		static byte[] FrameShape(GoldenShape golden)
		{
			using var output = new MemoryStream();
			WriteLengthPrefixed(output, ShapeMagic);
			WriteUInt32(output, checked((uint)golden.SchemaVersion));
			WriteUInt32(output, checked((uint)golden.DescriptorCount));
			for (var i = 0; i < golden.Descriptors.Count; i++)
			{
				using var payload = new MemoryStream();
				var descriptor = golden.Descriptors[i];
				WriteUInt32(payload, checked((uint)i));
				WriteLengthPrefixed(payload, descriptor.LogicalName);
				WriteLengthPrefixed(payload, descriptor.BundleRelativePath);
				WriteUInt32(payload, checked((uint)descriptor.Profiles.Length));
				foreach (var profile in descriptor.Profiles)
					WriteLengthPrefixed(payload, profile);
				WriteLengthPrefixed(payload, descriptor.Purpose);
				WriteUInt32(output, checked((uint)payload.Length));
				payload.WriteTo(output);
			}

			return output.ToArray();
		}

		static IReadOnlyList<GeneratedDescriptor> ParseGeneratedDescriptors(string source)
		{
			const string Pattern = @"new RuntimeContractResourceDescriptor\(\s*""(?<logical>[^""]+)"",\s*" +
				@"""(?<bundle>[^""]+)"",\s*(?<length>[0-9]+)L,\s*""(?<hash>[0-9a-f]{64})"",\s*" +
				@"ImmutableArray\.Create\((?<profiles>[^)]*)\),\s*" +
				@"RuntimeContractResourcePurpose\.(?<purpose>Contract|PreflightOnly)\)";
			return Regex.Matches(source, Pattern, RegexOptions.CultureInvariant)
				.Select(match => new GeneratedDescriptor(
					match.Groups["logical"].Value,
					match.Groups["bundle"].Value,
					long.Parse(match.Groups["length"].Value, CultureInfo.InvariantCulture),
					match.Groups["hash"].Value,
					Regex.Matches(match.Groups["profiles"].Value,
						@"Ra2RuntimeProfileId\.(Base|Yuri)").Select(p => p.Groups[1].Value).ToArray(),
					match.Groups["purpose"].Value)).ToArray();
		}

		static IReadOnlyList<string> ParseGeneratedStringArray(string source, string fieldName)
		{
			var match = Regex.Match(source,
				$@"{Regex.Escape(fieldName)}\s*=\s*\{{(?<body>.*?)\}};",
				RegexOptions.Singleline | RegexOptions.CultureInvariant);
			Assert.That(match.Success, Is.True, $"Generated array {fieldName} is missing.");
			return Regex.Matches(match.Groups["body"].Value, "\"(?<value>[^\"]+)\"")
				.Select(m => m.Groups["value"].Value).ToArray();
		}

		static GeneratedAotFactory ParseGeneratedUseCtorFactory(string source, string typeExpression)
		{
			var registrationMarker = $"GeneratedAotObjectRegistry.RegisterWithArgs(typeof({typeExpression})";
			var lines = source.Split('\n').Select(line => line.Trim()).Where(line =>
				line.Contains(registrationMarker, StringComparison.Ordinal)).ToArray();
			Assert.That(lines, Has.Length.EqualTo(1),
				$"Expected one generated UseCtor factory for {typeExpression}.");
			var constructorMarker = $"static args => new {typeExpression}(";
			var bodyStart = lines[0].IndexOf(constructorMarker, StringComparison.Ordinal);
			Assert.That(bodyStart, Is.GreaterThanOrEqualTo(0), lines[0]);
			Assert.That(lines[0], Does.EndWith("));"));
			var body = lines[0][(bodyStart + constructorMarker.Length)..^3];
			var matches = Regex.Matches(body,
				@"\((?<type>global::[^)]+)\)args\[""(?<name>[^""]+)""\]",
				RegexOptions.CultureInvariant);
			Assert.That(string.Join(", ", matches.Select(match => match.Value)), Is.EqualTo(body),
				$"Factory arguments for {typeExpression} are not in the canonical generated form.");
			return new GeneratedAotFactory(matches.Select(match => new AotParameter(
				match.Groups["name"].Value, match.Groups["type"].Value)).ToArray());
		}

		static string AotTypeExpression(Type type)
		{
			if (type.IsByRef || type.IsPointer || type.IsGenericParameter)
				throw new InvalidDataException($"Unsupported reflected AOT type shape: {type}.");
			if (type.IsArray)
				return AotTypeExpression(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
			if (type.IsGenericType)
			{
				var definition = type.GetGenericTypeDefinition();
				var fullName = definition.FullName ?? throw new InvalidDataException(
					$"Reflected generic AOT type has no full name: {type}.");
				return "global::" + fullName[..fullName.IndexOf('`')] + "<" +
					string.Join(", ", type.GetGenericArguments().Select(AotTypeExpression)) + ">";
			}

			return "global::" + (type.FullName ?? throw new InvalidDataException(
				$"Reflected AOT type has no full name: {type}.")).Replace('+', '.');
		}

		static int ExtractIntConstant(string source, string name)
		{
			var match = Regex.Match(source,
				$@"\b{Regex.Escape(name)}\s*=\s*(?<value>[0-9]+);",
				RegexOptions.CultureInvariant);
			Assert.That(match.Success, Is.True, $"Generated integer constant {name} is missing.");
			return int.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
		}

		static string ExtractStringConstant(string source, string name)
		{
			var match = Regex.Match(source,
				$@"\b{Regex.Escape(name)}\s*=\s*""(?<value>[^""]+)"";",
				RegexOptions.CultureInvariant);
			Assert.That(match.Success, Is.True, $"Generated string constant {name} is missing.");
			return match.Groups["value"].Value;
		}

		static byte[] FrameAot(IReadOnlyList<byte[]> payloads)
		{
			using var output = new MemoryStream();
			WriteLengthPrefixed(output, AotMagic);
			WriteUInt32(output, 1);
			WriteUInt32(output, checked((uint)payloads.Count));
			foreach (var payload in payloads)
				output.Write(payload, 0, payload.Length);
			return output.ToArray();
		}

		static byte[] FrameAotRecord(AotRecord record)
		{
			using var output = new MemoryStream();
			WriteLengthPrefixed(output, record.TypeName);
			WriteUInt32(output, checked((uint)record.Kind));
			WriteUInt32(output, checked((uint)record.Parameters.Length));
			foreach (var parameter in record.Parameters)
			{
				WriteLengthPrefixed(output, parameter.Name);
				WriteLengthPrefixed(output, parameter.TypeExpression);
			}

			return output.ToArray();
		}

		static AotRecord ParseAotRecord(byte[] payload)
		{
			using var input = new MemoryStream(payload, false);
			var typeName = ReadLengthPrefixed(input);
			var kind = checked((int)ReadUInt32(input));
			var count = checked((int)ReadUInt32(input));
			var parameters = new AotParameter[count];
			for (var i = 0; i < count; i++)
				parameters[i] = new AotParameter(ReadLengthPrefixed(input), ReadLengthPrefixed(input));
			Assert.That(input.Position, Is.EqualTo(input.Length), $"Trailing AOT record bytes for {typeName}.");
			AssertCanonicalString(typeName, "AOT type name");
			foreach (var parameter in parameters)
			{
				AssertCanonicalString(parameter.Name, $"AOT parameter name for {typeName}");
				AssertCanonicalString(parameter.TypeExpression, $"AOT parameter type for {typeName}");
				Assert.That(parameter.TypeExpression, Does.StartWith("global::"));
			}

			if (kind == 1)
				Assert.That(parameters, Is.Empty,
					$"Basic record {typeName} must have zero parameters.");
			return new AotRecord(typeName, kind, parameters);
		}

		static void WriteLengthPrefixed(Stream output, string value)
		{
			AssertCanonicalString(value, "framed string");
			var bytes = StrictUtf8.GetBytes(value);
			WriteUInt32(output, checked((uint)bytes.Length));
			output.Write(bytes, 0, bytes.Length);
		}

		static string ReadLengthPrefixed(Stream input)
		{
			var length = checked((int)ReadUInt32(input));
			var bytes = new byte[length];
			if (input.Read(bytes, 0, length) != length)
				throw new InvalidDataException("Truncated length-prefixed UTF-8 string.");
			return StrictUtf8.GetString(bytes);
		}

		static void WriteUInt32(Stream output, uint value)
		{
			output.WriteByte((byte)value);
			output.WriteByte((byte)(value >> 8));
			output.WriteByte((byte)(value >> 16));
			output.WriteByte((byte)(value >> 24));
		}

		static uint ReadUInt32(Stream input)
		{
			var b0 = input.ReadByte();
			var b1 = input.ReadByte();
			var b2 = input.ReadByte();
			var b3 = input.ReadByte();
			if ((b0 | b1 | b2 | b3) < 0)
				throw new InvalidDataException("Truncated unsigned 32-bit value.");
			return (uint)(b0 | b1 << 8 | b2 << 16 | b3 << 24);
		}

		static void AssertCanonicalString(string value, string label)
		{
			Assert.Multiple(() =>
			{
				Assert.That(value, Is.Not.Null.And.Not.Empty, label);
				Assert.That(value.IsNormalized(NormalizationForm.FormC), Is.True, label);
				Assert.That(value.IndexOfAny(new[] { '\0', '\t', '\r', '\n' }), Is.EqualTo(-1), label);
				Assert.DoesNotThrow(() => StrictUtf8.GetBytes(value), label);
			});
		}

		static void AssertCanonicalRelativePath(string value, string label)
		{
			AssertCanonicalString(value, label);
			Assert.Multiple(() =>
			{
				Assert.That(value, Does.Not.StartWith("/"), label);
				Assert.That(value, Does.Not.Contain("\\"), label);
				Assert.That(value.Split('/'), Is.All.Not.Empty, label);
				Assert.That(value.Split('/'), Is.All.Not.EqualTo(".").And.Not.EqualTo(".."), label);
				Assert.That(value.IndexOfAny(new[] { '*', '?', '[', ']' }), Is.EqualTo(-1), label);
			});
		}

		static int CompareUtf8(string left, string right)
		{
			var leftBytes = StrictUtf8.GetBytes(left);
			var rightBytes = StrictUtf8.GetBytes(right);
			for (var i = 0; i < Math.Min(leftBytes.Length, rightBytes.Length); i++)
				if (leftBytes[i] != rightBytes[i])
					return leftBytes[i].CompareTo(rightBytes[i]);
			return leftBytes.Length.CompareTo(rightBytes.Length);
		}

		static string BundleTarget(EvaluatedItem item)
		{
			var logicalName = Metadata(item, "LogicalName");
			if (!string.IsNullOrEmpty(logicalName))
				return logicalName;
			var link = Metadata(item, "Link");
			return string.IsNullOrEmpty(link) ? item.Identity : link;
		}

		static string Metadata(EvaluatedItem item, string name) =>
			item.Metadata.TryGetValue(name, out var value) ? value : string.Empty;

		static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !File.Exists(Path.Combine(directory.FullName,
				"packaging", "ios-runtime-contract-descriptor-shape-v1.json")))
				directory = directory.Parent;
			return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
		}

		sealed class GoldenShape
		{
			public int SchemaVersion { get; }
			public int DescriptorCount { get; }
			public string ShapeDigest { get; }
			public IReadOnlyList<DescriptorShape> Descriptors { get; }

			public GoldenShape(int schemaVersion, int descriptorCount, string shapeDigest,
				IReadOnlyList<DescriptorShape> descriptors)
			{
				SchemaVersion = schemaVersion;
				DescriptorCount = descriptorCount;
				ShapeDigest = shapeDigest;
				Descriptors = descriptors;
			}
		}

		sealed class DesktopSourceFixture
		{
			public string Path { get; }
			public long Length { get; }
			public string Sha256 { get; }

			public DesktopSourceFixture(string path, long length, string sha256)
			{
				Path = path;
				Length = length;
				Sha256 = sha256;
			}
		}

		sealed class SemanticChange
		{
			public string Path { get; }
			public string SourceValue { get; }
			public string BaseValue { get; }

			public SemanticChange(string path, string sourceValue, string baseValue)
			{
				Path = path;
				SourceValue = sourceValue;
				BaseValue = baseValue;
			}
		}

		sealed class DescriptorShape
		{
			public string SourcePath { get; }
			public string LogicalName { get; }
			public string BundleRelativePath { get; }
			public string[] Profiles { get; }
			public string Purpose { get; }

			public DescriptorShape(string sourcePath, string logicalName, string bundleRelativePath,
				string[] profiles, string purpose)
			{
				SourcePath = sourcePath;
				LogicalName = logicalName;
				BundleRelativePath = bundleRelativePath;
				Profiles = profiles;
				Purpose = purpose;
			}
		}

		sealed class EvaluatedProject
		{
			public IReadOnlyList<EvaluatedItem> RuntimeDescriptors { get; }
			public IReadOnlyList<EvaluatedItem> BundleResources { get; }

			public EvaluatedProject(IReadOnlyList<EvaluatedItem> runtimeDescriptors,
				IReadOnlyList<EvaluatedItem> bundleResources)
			{
				RuntimeDescriptors = runtimeDescriptors;
				BundleResources = bundleResources;
			}
		}

		sealed class EvaluatedItem
		{
			public string Identity { get; }
			public IReadOnlyDictionary<string, string> Metadata { get; }

			public EvaluatedItem(string identity, IReadOnlyDictionary<string, string> metadata)
			{
				Identity = identity;
				Metadata = metadata;
			}
		}

		sealed class GeneratedDescriptor
		{
			public string LogicalName { get; }
			public string BundleRelativePath { get; }
			public long Length { get; }
			public string Sha256 { get; }
			public string[] Profiles { get; }
			public string Purpose { get; }

			public GeneratedDescriptor(string logicalName, string bundleRelativePath, long length,
				string sha256, string[] profiles, string purpose)
			{
				LogicalName = logicalName;
				BundleRelativePath = bundleRelativePath;
				Length = length;
				Sha256 = sha256;
				Profiles = profiles;
				Purpose = purpose;
			}
		}

		sealed class AotRecord
		{
			public string TypeName { get; }
			public int Kind { get; }
			public AotParameter[] Parameters { get; }

			public AotRecord(string typeName, int kind, AotParameter[] parameters)
			{
				TypeName = typeName;
				Kind = kind;
				Parameters = parameters;
			}
		}

		sealed class GeneratedAotFactory
		{
			public AotParameter[] Parameters { get; }

			public GeneratedAotFactory(AotParameter[] parameters)
			{
				Parameters = parameters;
			}
		}

		sealed class AssemblyIdentityFixture
		{
			public string ConsumerAssembly { get; }
			public string CandidateAssembly { get; }

			public AssemblyIdentityFixture(string consumerAssembly, string candidateAssembly)
			{
				ConsumerAssembly = consumerAssembly;
				CandidateAssembly = candidateAssembly;
			}
		}

		sealed class ProcessResult
		{
			public int ExitCode { get; }
			public string Diagnostics { get; }

			public ProcessResult(int exitCode, string stdout, string stderr)
			{
				ExitCode = exitCode;
				Diagnostics = stderr + Environment.NewLine + stdout;
			}
		}

		sealed class AotParameter
		{
			public string Name { get; }
			public string TypeExpression { get; }

			public AotParameter(string name, string typeExpression)
			{
				Name = name;
				TypeExpression = typeExpression;
			}
		}
	}
}
