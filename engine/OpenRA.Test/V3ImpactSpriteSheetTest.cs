// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available to you under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Mods.Common.SpriteLoaders;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class V3ImpactSpriteSheetTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2"))))
				directory = directory.Parent;

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
