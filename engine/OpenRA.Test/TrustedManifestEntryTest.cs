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
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OpenRA.FileSystem;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class TrustedManifestEntryTest
	{
		const string Common =
			"Metadata:\n" +
			"\tTitle: Test\n" +
			"\tVersion: test\n" +
			"\tWebsite: https://example.invalid\n" +
			"\tWebIcon32: icon.png\n" +
			"\tWindowTitle: Test\n" +
			"LoadScreen: TestLoadScreen\n";

		const string IosCultures =
			"RuntimeContractCultures:\n" +
			"\tzh-CN:\n";

		const string Gameplay =
			"Rules:\n" +
			"\trules/base.yaml:\n";

		const string FileSystem =
			"FileSystem: TestFileSystem\n";

		[Test]
		public void TrustedSelectionIsCanonicalAndHasCompleteValueSemantics()
		{
			var first = new TrustedManifestSelection("ra2", "mod.ios-base.yaml");
			var same = new TrustedManifestSelection("ra2", "mod.ios-base.yaml");
			var other = new TrustedManifestSelection("ra2", "mod.ios-yuri.yaml");

			Assert.Multiple(() =>
			{
				Assert.That(first.ModId, Is.EqualTo("ra2"));
				Assert.That(first.EntryName, Is.EqualTo("mod.ios-base.yaml"));
				Assert.That(first, Is.EqualTo(same));
				Assert.That(first == same, Is.True);
				Assert.That(first != same, Is.False);
				Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
				Assert.That(first, Is.Not.EqualTo(other));
				Assert.That(default(TrustedManifestSelection).GetHashCode(), Is.EqualTo(0));
			});
		}

		[TestCase("")]
		[TestCase(".")]
		[TestCase("..")]
		[TestCase("../mod.yaml")]
		[TestCase("manifest/mod.yaml")]
		[TestCase("manifest\\mod.yaml")]
		[TestCase("ra2|mod.yaml")]
		[TestCase("/mod.yaml")]
		[TestCase("mod\0.yaml")]
		public void TrustedSelectionRejectsNonRootEntries(string entryName)
		{
			Assert.Throws<ArgumentException>(() => new TrustedManifestSelection("ra2", entryName));
		}

		[Test]
		public void RepeatedScalarIncludesResolveInSourceOrderAndAreDefensive()
		{
			using var package = Package(
				("mod.ios-base.yaml",
					"Include: manifest/common.yaml\n" +
					"Include: manifest/gameplay-base.yaml\n" +
					"Include: manifest/filesystem-ios-base.yaml\n"),
				("manifest/common.yaml", Common + IosCultures),
				("manifest/gameplay-base.yaml", Gameplay),
				("manifest/filesystem-ios-base.yaml", FileSystem));
			using var manifest = new Manifest("ra2", package, "mod.ios-base.yaml");

			Assert.Multiple(() =>
			{
				Assert.That(manifest.EntryName, Is.EqualTo("mod.ios-base.yaml"));
				Assert.That(manifest.IncludedFiles, Is.EqualTo(new[]
				{
					"manifest/common.yaml",
					"manifest/gameplay-base.yaml",
					"manifest/filesystem-ios-base.yaml",
				}));
				Assert.That(manifest.Rules, Is.EqualTo(new[] { "rules/base.yaml" }));
				Assert.That(manifest.RuntimeContractCultures, Is.EqualTo(new[] { "zh-CN" }));
			});

			var included = manifest.IncludedFiles.ToArray();
			included[0] = "changed";
			Assert.That(manifest.IncludedFiles[0], Is.EqualTo("manifest/common.yaml"));
		}

		[Test]
		public void DefaultEntryRemainsModYamlAndHasNoIncludesOrCultures()
		{
			using var package = Package(("mod.yaml", Common + Gameplay + FileSystem));
			using var manifest = new Manifest("ra2", package);

			Assert.Multiple(() =>
			{
				Assert.That(manifest.EntryName, Is.EqualTo("mod.yaml"));
				Assert.That(manifest.IncludedFiles, Is.Empty);
				Assert.That(manifest.RuntimeContractCultures, Is.Empty);
			});
		}

		[Test]
		public void ExplicitGetPassesTheCompleteMiniYamlToConfiguredConstructors()
		{
			using var package = Package(("mod.yaml",
				Common + Gameplay + FileSystem +
				"ExplicitCtorModule: configured\n" +
				"\tChild: retained\n"));
			using var manifest = new Manifest("ra2", package);

			var module = manifest.Get<ExplicitCtorModule>(null);

			Assert.Multiple(() =>
			{
				Assert.That(module.Value, Is.EqualTo("configured"));
				Assert.That(module.Children, Is.EqualTo(new[] { "Child" }));
			});
		}

		[TestCase("/absolute.yaml")]
		[TestCase("../escape.yaml")]
		[TestCase("manifest/../escape.yaml")]
		[TestCase("./manifest/common.yaml")]
		[TestCase("manifest//common.yaml")]
		[TestCase("manifest\\common.yaml")]
		[TestCase("ra2|manifest/common.yaml")]
		[TestCase("manifest/common.yaml\0")]
		public void IncludeRejectsNonCanonicalPathsBeforeOpening(string include)
		{
			using var package = Package(("mod.ios-base.yaml", $"Include: {include}\n"));
			Assert.Throws<YamlException>(() => new Manifest("ra2", package, "mod.ios-base.yaml"));
			Assert.That(package.Opened, Is.EqualTo(new[] { "mod.ios-base.yaml" }));
		}

		[Test]
		public void DuplicateCaseAliasAndNestedIncludesFailClosed()
		{
			using var duplicate = Package(
				("mod.ios-base.yaml",
					"Include: manifest/common.yaml\n" +
					"Include: manifest/common.yaml\n"),
				("manifest/common.yaml", Common + Gameplay + FileSystem));
			Assert.Throws<YamlException>(() => new Manifest("ra2", duplicate, "mod.ios-base.yaml"));

			using var alias = Package(
				("mod.ios-base.yaml",
					"Include: manifest/common.yaml\n" +
					"Include: manifest/Common.yaml\n"),
				("manifest/common.yaml", Common),
				("manifest/Common.yaml", Gameplay + FileSystem));
			Assert.Throws<YamlException>(() => new Manifest("ra2", alias, "mod.ios-base.yaml"));

			using var nested = Package(
				("mod.ios-base.yaml", "Include: manifest/common.yaml\n"),
				("manifest/common.yaml", "Include: manifest/nested.yaml\n"),
				("manifest/nested.yaml", Common + Gameplay + FileSystem));
			Assert.Throws<YamlException>(() => new Manifest("ra2", nested, "mod.ios-base.yaml"));
		}

		static TestPackage Package(params (string Name, string Text)[] files) =>
			new(files.Select(file => (file.Name, Encoding.UTF8.GetBytes(file.Text))).ToArray());

		public sealed class ExplicitCtorModule : IGlobalModData
		{
			public string Value { get; }
			public string[] Children { get; }

			public ExplicitCtorModule(MiniYaml yaml)
			{
				Value = yaml.Value;
				Children = yaml.Nodes.Select(node => node.Key).ToArray();
			}
		}

		sealed class TestPackage : IReadOnlyPackage
		{
			readonly IReadOnlyDictionary<string, byte[]> files;

			public string Name => "trusted-manifest-test";
			public IEnumerable<string> Contents => files.Keys;
			public List<string> Opened { get; } = new();

			public TestPackage(params (string Name, byte[] Data)[] files)
			{
				this.files = files.ToDictionary(file => file.Name, file => file.Data, StringComparer.Ordinal);
			}

			public Stream GetStream(string filename)
			{
				Opened.Add(filename);
				return files.TryGetValue(filename, out var data)
					? new MemoryStream(data, writable: false) : null;
			}

			public bool Contains(string filename) => files.ContainsKey(filename);
			public IReadOnlyPackage OpenPackage(string filename, OpenRA.FileSystem.FileSystem context) => null;
			public void Dispose() { }
		}
	}
}
