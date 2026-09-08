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
using System.Collections.Immutable;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class ArgumentsSnapshotTest
	{
		[Test]
		public void CloneCopiesEveryParsedOptionAndIsIndependentBothWays()
		{
			var original = new Arguments("alpha=one", "ignored", "beta=two=three", "alpha=last");
			var clone = original.Clone();

			Assert.Multiple(() =>
			{
				Assert.That(clone.GetValue("alpha", null), Is.EqualTo("last"));
				Assert.That(clone.GetValue("beta", null), Is.EqualTo("two=three"));
				Assert.That(clone.Contains("ignored"), Is.False);
			});

			original.ReplaceValue("alpha", "original-change");
			clone.ReplaceValue("beta", "clone-change");
			Assert.Multiple(() =>
			{
				Assert.That(clone.GetValue("alpha", null), Is.EqualTo("last"));
				Assert.That(original.GetValue("beta", null), Is.EqualTo("two=three"));
			});
		}

		[Test]
		public void EmptyCloneIsIndependent()
		{
			var original = Arguments.Empty;
			var clone = original.Clone();
			clone.ReplaceValue("new", "value");

			Assert.Multiple(() =>
			{
				Assert.That(original.Contains("new"), Is.False);
				Assert.That(clone.GetValue("new", null), Is.EqualTo("value"));
			});
		}

		[Test]
		public void BootstrapConfigurationDefensivelyCopiesArraysAndArguments()
		{
			var search = new[] { "mods", "system" };
			var explicitMods = new[] { "ra2" };
			var arguments = new Arguments("Engine.LaunchPath=/one");
			var configuration = new EngineBootstrapConfiguration(
				"/engine",
				"/support",
				ImmutableArray.CreateRange(search),
				ImmutableArray.CreateRange(explicitMods),
				arguments);

			search[0] = "changed";
			explicitMods[0] = "changed";
			arguments.ReplaceValue("Engine.LaunchPath", "/two");
			var first = configuration.Arguments;
			first.ReplaceValue("Engine.LaunchPath", "/three");

			Assert.Multiple(() =>
			{
				Assert.That(configuration.EngineDir, Is.EqualTo("/engine"));
				Assert.That(configuration.SupportDir, Is.EqualTo("/support"));
				Assert.That(configuration.ModSearchPaths, Is.EqualTo(new[] { "mods", "system" }));
				Assert.That(configuration.ExplicitModPaths, Is.EqualTo(new[] { "ra2" }));
				Assert.That(configuration.Arguments.GetValue("Engine.LaunchPath", null), Is.EqualTo("/one"));
			});
		}

		[Test]
		public void BootstrapConfigurationRejectsInvalidState()
		{
			Assert.Throws<ArgumentNullException>(() => new EngineBootstrapConfiguration(
				null, "/support", ImmutableArray<string>.Empty,
				ImmutableArray<string>.Empty, Arguments.Empty));
			Assert.Throws<ArgumentException>(() => new EngineBootstrapConfiguration(
				"/engine", "/support", default,
				ImmutableArray<string>.Empty, Arguments.Empty));
			Assert.Throws<ArgumentException>(() => new EngineBootstrapConfiguration(
				"/engine", "/support", ImmutableArray.Create("mods", null),
				ImmutableArray<string>.Empty, Arguments.Empty));
			Assert.Throws<ArgumentNullException>(() => new EngineBootstrapConfiguration(
				"/engine", "/support", ImmutableArray<string>.Empty,
				ImmutableArray<string>.Empty, null));
		}
	}
}
