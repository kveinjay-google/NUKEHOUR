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
	public sealed class TeslaChargeRulesTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2"))))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
		}

		[TestCase("mods/ra2/rules/soviet-infantry.yaml")]
		[TestCase("engine/mods/ra2/rules/soviet-infantry.yaml")]
		public void TeslaTrooperAutomaticallyTargetsAlliedTeslaBoostActors(string relativePath)
		{
			var rules = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
			var actorStart = rules.IndexOf("shk:\n", StringComparison.Ordinal);
			var actorEnd = rules.IndexOf("\nterror:\n", actorStart, StringComparison.Ordinal);
			var teslaTrooper = rules.Substring(actorStart, actorEnd - actorStart);

			StringAssert.Contains("AutoTargetPriority@TESLACHARGE:", teslaTrooper);
			StringAssert.Contains("ValidTargets: TeslaBoost", teslaTrooper);
			StringAssert.Contains("ValidRelationships: Ally", teslaTrooper);
			StringAssert.Contains("Priority: 2", teslaTrooper);
		}
	}
}
