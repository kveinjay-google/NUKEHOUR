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
using System.Globalization;
using System.IO;
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Mods.Common.SpriteLoaders;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class PublicContentSafetyPolicyTest
	{
		string root;

		[SetUp]
		public void SetUp()
		{
			root = Path.Combine(Path.GetTempPath(), "openra-public-safety-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(root);
		}

		[TearDown]
		public void TearDown() => Directory.Delete(root, true);

		string Write(string name, byte[] data = null)
		{
			var path = Path.Combine(root, name);
			File.WriteAllBytes(path, data ?? new byte[] { 1, 2, 3, 4 });
			return path;
		}

		[Test]
		public void RetailDataAndOrdinaryMapsAreAccepted()
		{
			Assert.That(PublicContentSafetyPolicy.ValidateImportFile(Write("ra2.mix")), Is.EqualTo(ContentSafetyViolation.None));
			Assert.That(PublicContentSafetyPolicy.ValidateImportFile(Write("custom.oramap")), Is.EqualTo(ContentSafetyViolation.None));
			Assert.That(PublicContentSafetyPolicy.ValidateMapPackageEntries(new[]
			{
				"map.yaml", "map.bin", "preview.png", "tiles/custom.shp",
			}), Is.EqualTo(ContentSafetyViolation.None));
		}

		[TestCase("payload.dll")]
		[TestCase("payload.dylib")]
		[TestCase("Payload.framework/binary")]
		[TestCase("Payload.app/binary")]
		public void ExecutableAndBundlePathsAreRejected(string name)
		{
			Assert.That(PublicContentSafetyPolicy.ValidateMapPackageEntries(new[] { name }),
				Is.EqualTo(ContentSafetyViolation.ExecutableContent));
		}

		[Test]
		public void MachOIsRejectedRegardlessOfExtension()
		{
			var path = Write("renamed.mix", new byte[] { 0xCF, 0xFA, 0xED, 0xFE, 1, 2, 3, 4 });
			Assert.That(PublicContentSafetyPolicy.ValidateImportFile(path),
				Is.EqualTo(ContentSafetyViolation.ExecutableContent));
		}

		[TestCase("mods/foreign/mod.yaml")]
		[TestCase("assemblies/plugin.dll")]
		[TestCase("scripts/mission.lua")]
		[TestCase("rules/rules.yaml")]
		public void ModAndOverrideHooksAreRejectedFromMapPackages(string entry)
		{
			Assert.That(PublicContentSafetyPolicy.ValidateMapPackageEntries(new[] { entry }),
				Is.EqualTo(ContentSafetyViolation.MapOverride));
		}
	}

	[TestFixture]
	public sealed class ProjectOriginalImpactSpriteSheetTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null)
			{
				var gitPath = Path.Combine(directory.FullName, ".git");
				if ((Directory.Exists(gitPath) || File.Exists(gitPath)) &&
					Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2")))
					break;

				directory = directory.Parent;
			}

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
		}

		[TestCase("nc-impact-core.png", 16)]
		[TestCase("nc-impact-ring.png", 16)]
		[TestCase("nc-impact-debris.png", 8)]
		public void RealPngSheetLoaderSlicesGeneratedFrames(string filename, int expectedFrames)
		{
			var path = Path.Combine(RepositoryRoot(), "mods", "ra2", "bits", "animations", filename);
			using var stream = File.OpenRead(path);
			var loader = new PngSheetLoader();

			Assert.That(loader.TryParseSprite(stream, filename, out var frames, out var metadata), Is.True);
			Assert.That(frames, Has.Length.EqualTo(expectedFrames));
			Assert.That(metadata.Get<PngSheetMetadata>().Metadata["FrameSize"], Is.EqualTo("128,128"));
			Assert.That(metadata.Get<PngSheetMetadata>().Metadata["FrameAmount"],
				Is.EqualTo(expectedFrames.ToString(CultureInfo.InvariantCulture)));
			Assert.That(frames, Has.All.Matches<ISpriteFrame>(frame =>
				frame.Type == SpriteFrameType.Rgba32 &&
				frame.Size.Width == 128 && frame.Size.Height == 128 &&
				frame.FrameSize.Width == 128 && frame.FrameSize.Height == 128));
		}
	}
}
