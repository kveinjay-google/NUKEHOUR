// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available to you under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.IO;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class YuriProductionCameoTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2"))))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
		}

		static string ChildSequenceBlock(string actor, string sequence, string nextSequence)
		{
			var start = actor.IndexOf($"\n\t{sequence}:\n", StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing {sequence} sequence.");

			var end = actor.IndexOf($"\n\t{nextSequence}:\n", start, StringComparison.Ordinal);
			Assert.That(end, Is.GreaterThan(start), $"Missing sequence after {sequence}.");
			return actor.Substring(start, end - start);
		}

		static void AssertContainsLine(string block, string expected)
		{
			CollectionAssert.Contains(block.Split('\n'), expected);
		}

		[TestCase("ydomicon-cn.png", "actor-yadome =", ".name = 心灵感应器")]
		[TestCase("yfixicon-cn.png", "actor-yadept =", ".name = 服务站")]
		public void YuriUtilityBuildingsUseLocalizedPngCameos(string filename, string actorKey, string localizedName)
		{
			var root = RepositoryRoot();
			var sequences = File.ReadAllText(Path.Combine(root, "mods", "ra2", "sequences", "yuri-structures.yaml"));
			var localization = File.ReadAllText(Path.Combine(root, "mods", "ra2", "fluent", "zh-CN", "rules.ftl"));
			var cameo = Path.Combine(root, "mods", "ra2", "bits", "cameos", filename);

			StringAssert.Contains($"Filename: bits/cameos/{filename}", sequences);
			Assert.That(File.Exists(cameo), Is.True, $"Missing remastered cameo {filename}.");
			StringAssert.Contains(actorKey, localization);
			StringAssert.Contains(localizedName, localization);
		}

		[Test]
		public void HighResolutionCameosAreScaledToFitTheProductionCell()
		{
			Assert.That(ProductionCameoScalePolicy.Resolve(240, 192, 64, 48), Is.EqualTo(0.25f));
			Assert.That(ProductionCameoScalePolicy.Resolve(60, 48, 64, 48), Is.EqualTo(1f));
		}

		[Test]
		public void GrinderUsesTheFullBuildingSpriteForItsIdleBody()
		{
			var sequences = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "sequences", "yuri-structures.yaml"));
			var grinderStart = sequences.IndexOf("\nyagrnd:\n", StringComparison.Ordinal);
			var grinderEnd = sequences.IndexOf("\nyadept:\n", grinderStart, StringComparison.Ordinal);

			Assert.That(grinderStart, Is.GreaterThanOrEqualTo(0));
			Assert.That(grinderEnd, Is.GreaterThan(grinderStart));

			var grinder = sequences.Substring(grinderStart, grinderEnd - grinderStart);
			var defaults = ChildSequenceBlock(grinder, "Defaults", "idle");
			var idle = ChildSequenceBlock(grinder, "idle", "damaged-idle");
			var damagedIdle = ChildSequenceBlock(grinder, "damaged-idle", "grind");
			var grind = ChildSequenceBlock(grinder, "grind", "damaged-grind");
			var damagedGrind = ChildSequenceBlock(grinder, "damaged-grind", "make");

			AssertContainsLine(defaults, "\t\tFilename: yggrnd.shp");
			AssertContainsLine(idle, "\t\tFilename: yggrnd.shp");
			AssertContainsLine(idle, "\t\t\tSNOW: yagrnd.shp");
			AssertContainsLine(idle, "\t\tShadowStart: 3");
			StringAssert.DoesNotContain("Length:", idle);
			StringAssert.DoesNotContain("Tick:", idle);

			AssertContainsLine(damagedIdle, "\t\tFilename: yggrnd.shp");
			AssertContainsLine(damagedIdle, "\t\t\tSNOW: yagrnd.shp");
			AssertContainsLine(damagedIdle, "\t\tStart: 1");
			AssertContainsLine(damagedIdle, "\t\tShadowStart: 4");
			StringAssert.DoesNotContain("Length:", damagedIdle);
			StringAssert.DoesNotContain("Tick:", damagedIdle);

			AssertContainsLine(grind, "\t\tFilename: yggrnd_b.shp");
			AssertContainsLine(grind, "\t\t\tSNOW: yagrnd_b.shp");
			AssertContainsLine(grind, "\t\tLength: 48");
			AssertContainsLine(grind, "\t\tShadowStart: 96");

			AssertContainsLine(damagedGrind, "\t\tFilename: yggrnd_b.shp");
			AssertContainsLine(damagedGrind, "\t\t\tSNOW: yagrnd_b.shp");
			AssertContainsLine(damagedGrind, "\t\tStart: 48");
			AssertContainsLine(damagedGrind, "\t\tLength: 48");
			AssertContainsLine(damagedGrind, "\t\tShadowStart: 144");
		}

		[TestCase("mods/ra2/fluent/zh-CN/rules.ftl")]
		[TestCase("engine/mods/ra2/fluent/zh-CN/rules.ftl")]
		public void LasherTankUsesTheFamiliarChineseName(string relativePath)
		{
			var localization = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
			var actorStart = localization.IndexOf("actor-ltnk =", StringComparison.Ordinal);
			var actorEnd = localization.IndexOf("\nactor-ytnk =", actorStart, StringComparison.Ordinal);
			var lasherTank = localization.Substring(actorStart, actorEnd - actorStart);

			StringAssert.Contains(".name = 狂风坦克", lasherTank);
			StringAssert.DoesNotContain("鞭笞者轻型坦克", lasherTank);
		}
	}
}
