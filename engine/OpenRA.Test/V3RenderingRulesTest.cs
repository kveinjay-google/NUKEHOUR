// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available to you under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class V3RenderingRulesTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2"))))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
		}

		static (string Launcher, string Rocket) V3Rules()
		{
			var rules = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "soviet-vehicles.yaml"));
			var launcherStart = rules.IndexOf("v3:\n", StringComparison.Ordinal);
			var rocketStart = rules.IndexOf("\nv3rocket:\n", launcherStart, StringComparison.Ordinal);
			Assert.That(launcherStart, Is.GreaterThanOrEqualTo(0));
			Assert.That(rocketStart, Is.GreaterThan(launcherStart));

			return (rules.Substring(launcherStart, rocketStart - launcherStart), rules[rocketStart..]);
		}

		[Test]
		public void LauncherUsesRedAlert2VoxelNormals()
		{
			var (launcher, _) = V3Rules();

			StringAssert.Contains("RenderVoxels:", launcher);
			StringAssert.Contains("\n\t\tImage: v3\n", launcher);
			StringAssert.DoesNotContain("NormalsPalette: ts-normals", launcher);
		}

		[Test]
		public void SpawnedRocketSubmitsItsVoxelBodyForRendering()
		{
			var (_, rocket) = V3Rules();

			StringAssert.Contains("RenderVoxels:", rocket);
			StringAssert.Contains("\n\t\tImage: v3rocket\n", rocket);
			StringAssert.Contains("\n\tWithVoxelBody:\n", rocket);
			StringAssert.DoesNotContain("NormalsPalette: ts-normals", rocket);
		}

		[TestCase(160, 651)]
		[TestCase(922, 649)]
		public void SpawnedRocketVoxelRotationPreservesFlightPitchAndYaw(int pitch, int yaw)
		{
			var actor = MiniYaml.FromFile(Path.Combine(
				RepositoryRoot(), "mods", "ra2", "rules", "soviet-vehicles.yaml"))
				.Single(node => node.Key == "v3rocket");
			var trait = actor.Value.Nodes.Single(node => node.Key == "BodyOrientation");
			var info = new BodyOrientationInfo();
			FieldLoader.Load(info, trait.Value);
			var flightOrientation = new WRot(WAngle.Zero, new WAngle(pitch), new WAngle(yaw));
			var renderedOrientation = info.QuantizeOrientation(flightOrientation, info.QuantizedFacings);

			Assert.That(renderedOrientation, Is.EqualTo(flightOrientation));
		}
	}
}
