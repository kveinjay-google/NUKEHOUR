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
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RuntimeShellmapInstallerTest
	{
		string root;

		[SetUp]
		public void SetUp()
		{
			root = Path.Combine(Path.GetTempPath(), "openra-runtime-shellmap-" + Guid.NewGuid().ToString("N"));
		}

		[TearDown]
		public void TearDown()
		{
			if (Directory.Exists(root))
				Directory.Delete(root, true);
		}

		[Test]
		public void CreatesAValidCodeGeneratedShellmapInUserStorage()
		{
			var directory = RuntimeShellmapInstaller.EnsureInstalled(root);
			var yaml = File.ReadAllText(Path.Combine(directory, "map.yaml"));
			var binary = File.ReadAllBytes(Path.Combine(directory, "map.bin"));

			StringAssert.Contains("MapFormat: 11", yaml);
			StringAssert.Contains("Visibility: Shellmap", yaml);
			StringAssert.Contains("Tileset: TEMPERATE", yaml);
			Assert.That(binary.Length, Is.EqualTo(113897));
			Assert.That(binary[0], Is.EqualTo(2));
			Assert.That(BitConverter.ToUInt16(binary, 1), Is.EqualTo(130));
			Assert.That(BitConverter.ToUInt16(binary, 3), Is.EqualTo(146));
			Assert.That(BitConverter.ToUInt32(binary, 5), Is.EqualTo(17));
			Assert.That(BitConverter.ToUInt32(binary, 9), Is.EqualTo(56957));
			Assert.That(BitConverter.ToUInt32(binary, 13), Is.EqualTo(75937));
		}

		[Test]
		public void RepairsAnInterruptedOrInvalidPreviousGeneration()
		{
			var directory = RuntimeShellmapInstaller.InstallationPath(root);
			Directory.CreateDirectory(directory);
			File.WriteAllText(Path.Combine(directory, "map.yaml"), "partial");

			RuntimeShellmapInstaller.EnsureInstalled(root);

			StringAssert.Contains("Visibility: Shellmap", File.ReadAllText(Path.Combine(directory, "map.yaml")));
			Assert.That(new FileInfo(Path.Combine(directory, "map.bin")).Length, Is.EqualTo(113897));
		}

		[Test]
		public void CreatesAProjectOwnedPlayableSkirmishMapInUserStorage()
		{
			var package = RuntimeShellmapInstaller.EnsurePlayableMapInstalled(root);

			Assert.That(Path.GetExtension(package), Is.EqualTo(".oramap"));
			using var archive = ZipFile.OpenRead(package);
			var yamlEntry = archive.GetEntry("map.yaml");
			var binaryEntry = archive.GetEntry("map.bin");
			Assert.That(yamlEntry, Is.Not.Null);
			Assert.That(binaryEntry, Is.Not.Null);
			using var yamlReader = new StreamReader(yamlEntry.Open());
			var yaml = yamlReader.ReadToEnd();
			StringAssert.Contains("Title: NUKE HOUR Starter Battlefield", yaml);
			StringAssert.Contains("Visibility: Lobby", yaml);
			StringAssert.Contains("PlayerReference@Multi0", yaml);
			Assert.That(yaml.Split("Actor@mpspawn", StringSplitOptions.None).Length - 1, Is.GreaterThanOrEqualTo(2));
			Assert.That(binaryEntry.Length, Is.EqualTo(113897));
		}
	}
}
