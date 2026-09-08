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
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class Ra2StartingUnitsPolicyTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "engine", "OpenRA.Game"))))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		[Test]
		public void OnlyOriginalAlliedSovietAndYuriFactionsRemain()
		{
			var root = RepositoryRoot();
			var world = File.ReadAllText(Path.Combine(root, "mods", "ra2", "rules", "world.yaml"));
			var launcherSources = new[]
			{
				"ai_profiles.py", "launcher.py", "launcher_i18n.py"
			}.Select(path => Path.Combine(root, path));
			var runtimeSources = new[] { Path.Combine(root, "mods", "ra2"), Path.Combine(root, "engine", "mods", "ra2") }
				.SelectMany(directory => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
				.Where(path => path.EndsWith(".yaml", StringComparison.Ordinal) || path.EndsWith(".ftl", StringComparison.Ordinal));

			foreach (var path in launcherSources.Concat(runtimeSources))
			{
				var source = File.ReadAllText(path);
				foreach (var removed in new[] { "china", "yhtnk", "psicorps", "psinepal", "psisouth", "psitrans", "psimoon", "random-yuri" })
					Assert.That(source.IndexOf(removed, StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1),
						$"{path} still contains removed faction token '{removed}'.");
			}

			StringAssert.Contains("RandomFactionMembers: random-allies, random-soviets, yuri", world);
			StringAssert.Contains("RandomFactionMembers: cuba, libya, iraq, russia", world);
			StringAssert.Contains("Faction@yuri:\n\t\tName: meta-baseworld.faction-yuri-name\n\t\tInternalName: yuri", world);
			Assert.That(world.Split("Factions: yuri").Length - 1, Is.EqualTo(4),
				"Yuri must support none, light, medium, and heavy starting units.");
		}

		[Test]
		public void IosBundleIncludesCompleteRuntimeAndCanonicalRa2Overlay()
		{
			var project = File.ReadAllText(Path.Combine(RepositoryRoot(), "ios", "OpenRA.iOS", "OpenRA.iOS.csproj"));
			StringAssert.Contains(
				"BundleResource Include=\"../../engine/mods/**/*\" Exclude=\"../../engine/mods/ra2/**/*\"", project,
				"The complete runtime copy contains required UI and gameplay fixes.");
			StringAssert.Contains("BundleResource Include=\"../../mods/ra2/**/*\"", project,
				"The canonical RA2 overlay must be applied after the complete runtime copy.");
			StringAssert.DoesNotContain("BundleResource Remove=\"../../engine/mods/ra2/**/*\"", project,
				"MSBuild Remove does not reliably filter files that were materialized from a recursive glob.");
		}

		[Test]
		public void YuriUsesAnAvailableSidebarChromeSet()
		{
			var root = RepositoryRoot();
			foreach (var path in new[]
			{
				Path.Combine(root, "mods", "ra2", "metrics.yaml"),
				Path.Combine(root, "engine", "mods", "ra2", "metrics.yaml")
			})
				StringAssert.Contains("FactionSuffix-yuri: soviets", File.ReadAllText(path));
		}

		[Test]
		public void YuriVehiclesUseRedAlert2VoxelNormals()
		{
			var root = RepositoryRoot();
			foreach (var path in new[]
			{
				Path.Combine(root, "mods", "ra2", "rules", "yuri-vehicles.yaml"),
				Path.Combine(root, "engine", "mods", "ra2", "rules", "yuri-vehicles.yaml")
			})
				StringAssert.DoesNotContain("NormalsPalette: ts-normals", File.ReadAllText(path),
					$"{path} must use the default Red Alert 2 voxel normals palette.");
		}

		[Test]
		public void IosJoystickIconsProvideHighlightedState()
		{
			var root = RepositoryRoot();
			foreach (var path in new[]
			{
				Path.Combine(root, "mods", "ra2", "chrome.yaml"),
				Path.Combine(root, "engine", "mods", "ra2", "chrome.yaml")
			})
			{
				var chrome = File.ReadAllText(path);
				StringAssert.Contains("ios-joystick-command-icons-highlighted:\n\tInherits: ios-joystick-command-icons", chrome,
					$"{path} must define the highlighted collection used by stateful touch buttons.");
			}
		}
	}
}
