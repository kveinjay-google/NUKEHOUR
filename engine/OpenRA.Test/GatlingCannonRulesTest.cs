// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available to you under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class GatlingCannonRulesTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2"))))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
		}

		[TestCase("mods/ra2/rules/yuri-structures.yaml")]
		[TestCase("engine/mods/ra2/rules/yuri-structures.yaml")]
		public void GatlingCannonAlignsTheOriginalVoxelTurretWithItsBuiltBase(string relativePath)
		{
			var rules = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
			var actorStart = rules.IndexOf("yaggun:\n", StringComparison.Ordinal);
			var actorEnd = rules.IndexOf("\nyatech:\n", actorStart, StringComparison.Ordinal);
			var gatlingCannon = rules.Substring(actorStart, actorEnd - actorStart);

			StringAssert.Contains("Turreted:\n\t\tTurnSpeed: 40\n\t\tInitialFacing: 896\n\t\tOffset: 480,0,0", gatlingCannon);
			StringAssert.Contains("RenderVoxels:\n\t\tImage: yaggun\n\t\tNormalsPalette: ts-normals", gatlingCannon);
			StringAssert.DoesNotContain("Scale: 18", gatlingCannon);
			StringAssert.Contains("WithVoxelTurret:\n\t\tRequiresCondition: !build-incomplete", gatlingCannon);
		}

		[TestCase("mods/ra2/rules/allied-vehicles.yaml", "tnkd", "fv", "13")]
		[TestCase("mods/ra2/rules/soviet-vehicles.yaml", "apoc", "ttnk", "10.5")]
		public void VehicleVoxelScaleMatchesTheOriginalAssetProportions(
			string relativePath, string actorName, string nextActorName, string expectedScale)
		{
			var rules = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
			var actorStart = rules.IndexOf($"{actorName}:\n", StringComparison.Ordinal);
			var actorEnd = rules.IndexOf($"\n{nextActorName}:\n", actorStart, StringComparison.Ordinal);
			var actorRules = rules.Substring(actorStart, actorEnd - actorStart);

			StringAssert.Contains($"RenderVoxels:\n\t\tScale: {expectedScale}", actorRules);
		}
	}
}
