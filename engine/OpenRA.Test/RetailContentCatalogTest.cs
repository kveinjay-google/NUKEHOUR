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

using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using NUnit.Framework;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailContentCatalogTest
	{
		readonly RetailContentCatalog catalog = new();

		[Test]
		public void EmptyDirectoryReportsEveryContentGroupMissing()
		{
			var status = catalog.Inspect(System.Array.Empty<string>());

			Assert.That(status.Base, Is.EqualTo(ContentState.Missing));
			Assert.That(status.Yuri, Is.EqualTo(ContentState.Missing));
			Assert.That(status.Audio, Is.EqualTo(ContentState.Missing));
			Assert.That(status.Maps, Is.EqualTo(ContentState.Missing));
			Assert.That(status.CanLaunchBaseGame, Is.False);
			Assert.That(status.CanLaunchYuriGame, Is.False);
		}

		[Test]
		public void FilenameOnlyAndNonexistentMixSelectionsAreNotReady()
		{
			var filenameOnly = catalog.Inspect(new[] { "ra2.mix", "language.mix" });
			var nonexistent = catalog.Inspect(new[] { "/not-present/ra2.mix", "/not-present/language.mix" });

			Assert.That(filenameOnly.Base, Is.Not.EqualTo(ContentState.Ready));
			Assert.That(filenameOnly.CanLaunchBaseGame, Is.False);
			Assert.That(nonexistent.Base, Is.Not.EqualTo(ContentState.Ready));
			Assert.That(nonexistent.CanLaunchBaseGame, Is.False);
		}

		[Test]
		public void TextFilesMasqueradingAsMixArchivesAreNotReady()
		{
			var directory = Path.Combine(Path.GetTempPath(), "openra-retail-catalog-" + System.Guid.NewGuid());
			Directory.CreateDirectory(directory);
			try
			{
				File.WriteAllText(Path.Combine(directory, "ra2.mix"), "not a mix archive");
				File.WriteAllText(Path.Combine(directory, "language.mix"), "not a mix archive");

				var status = catalog.Inspect(directory);

				Assert.That(status.Base, Is.EqualTo(ContentState.Incomplete));
				Assert.That(status.CanLaunchBaseGame, Is.False);
			}
			finally
			{
				Directory.Delete(directory, true);
			}
		}

		[Test]
		public void MinimalOuterMixHeadersDoNotSatisfyTheRequiredArchiveGraph()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			fixture.WriteMinimalMix("disc/RA2.MIX");
			fixture.WriteMinimalMix("Language.mix");

			var status = catalog.Inspect(fixture.Root);

			Assert.That(status.Base, Is.EqualTo(ContentState.Incomplete));
			Assert.That(status.CanLaunchBaseGame, Is.False);
			Assert.That(status.Yuri, Is.EqualTo(ContentState.Missing));
		}

		[Test]
		public void ZeroEntryAndFakeEncryptedMixArchivesAreRejected()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var language = fixture.WriteMinimalMix("language.mix");
			var zeroEntry = fixture.WriteMix("zero/ra2.mix", 0, 0,
				System.Array.Empty<(uint Hash, uint Offset, uint Length)>(), System.Array.Empty<byte>());
			Assert.That(catalog.Inspect(new[] { zeroEntry, language }).Base, Is.EqualTo(ContentState.Incomplete));

			var encrypted = new byte[92];
			encrypted[2] = 2;
			var fakeEncrypted = fixture.WriteBytes("encrypted/ra2.mix", encrypted);
			Assert.That(catalog.Inspect(new[] { fakeEncrypted, language }).Base, Is.EqualTo(ContentState.Incomplete));
		}

		[Test]
		public void MixFlagsMustBeExplicitlyUnencryptedWithoutChecksumOrUnknownBits()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var language = fixture.WriteMinimalMix("language.mix");
			var data = new byte[] { 1 };
			var entries = new[] { (Hash: 1U, Offset: 0U, Length: 1U) };
			foreach (var flags in new ushort[] { 1, 2, 4, ushort.MaxValue })
			{
				var path = fixture.WriteMix("flags-" + flags + "/ra2.mix", flags, 1, entries, data);
				Assert.That(catalog.Inspect(new[] { path, language }).Base, Is.EqualTo(ContentState.Incomplete),
					flags.ToString(CultureInfo.InvariantCulture));
			}
		}

		[Test]
		public void MixIndexRejectsOutOfRangeZeroLengthDuplicateAndOverlappingEntries()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var language = fixture.WriteMinimalMix("language.mix");
			var cases = new[]
			{
				fixture.WriteMix("range/ra2.mix", 0, 1, new[] { (Hash: 1U, Offset: 1U, Length: 1U) }, new byte[] { 1 }),
				fixture.WriteMix("zero-length/ra2.mix", 0, 1, new[] { (Hash: 1U, Offset: 0U, Length: 0U) }, new byte[] { 1 }),
				fixture.WriteMix("duplicate/ra2.mix", 0, 2, new[]
				{
					(Hash: 1U, Offset: 0U, Length: 1U),
					(Hash: 1U, Offset: 1U, Length: 1U),
				}, new byte[] { 1, 2 }),
				fixture.WriteMix("overlap/ra2.mix", 0, 3, new[]
				{
					(Hash: 1U, Offset: 0U, Length: 2U),
					(Hash: 2U, Offset: 1U, Length: 2U),
				}, new byte[] { 1, 2, 3 }),
			};

			foreach (var path in cases)
				Assert.That(catalog.Inspect(new[] { path, language }).Base, Is.EqualTo(ContentState.Incomplete), path);
		}

		[Test]
		public void MixDeclaredLengthMustExactlyMatchStableFileLength()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var language = fixture.WriteMinimalMix("language.mix");
			var truncated = fixture.WriteMix("truncated/ra2.mix", 0, 4,
				new[] { (Hash: 1U, Offset: 0U, Length: 4U) }, new byte[] { 1, 2, 3 });
			var trailing = fixture.WriteMix("trailing/ra2.mix", 0, 1,
				new[] { (Hash: 1U, Offset: 0U, Length: 1U) }, new byte[] { 1, 2 });
			Assert.That(catalog.Inspect(new[] { truncated, language }).Base, Is.EqualTo(ContentState.Incomplete));
			Assert.That(catalog.Inspect(new[] { trailing, language }).Base, Is.EqualTo(ContentState.Incomplete));
		}

		[Test]
		public void MixSymlinkIsRejectedEvenWhenTargetIsValid()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var target = fixture.WriteMinimalMix("target.mix");
			var language = fixture.WriteMinimalMix("language.mix");
			var link = Path.Combine(fixture.Root, "ra2.mix");
			File.CreateSymbolicLink(link, target);
			Assert.That(catalog.Inspect(new[] { link, language }).Base, Is.EqualTo(ContentState.Incomplete));
		}

		[Test]
		public void MixFilesBelowASymlinkedParentDirectoryAreRejected()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			fixture.WriteMinimalMix("victim/ra2.mix");
			fixture.WriteMinimalMix("victim/language.mix");
			var linkedParent = Path.Combine(fixture.Root, "linked-parent");
			Directory.CreateSymbolicLink(linkedParent, Path.Combine(fixture.Root, "victim"));

			var status = catalog.Inspect(new[]
			{
				Path.Combine(linkedParent, "ra2.mix"),
				Path.Combine(linkedParent, "language.mix"),
			});

			Assert.That(status.Base, Is.Not.EqualTo(ContentState.Ready));
			Assert.That(status.CanLaunchBaseGame, Is.False);
		}

		[Test]
		[NonParallelizable]
		public void ParentReplacementBeforeFinalOpenUsesPinnedDirectoryAndClosesEveryDescriptor()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var source = Path.Combine(fixture.Root, "source");
			var moved = Path.Combine(fixture.Root, "source-moved");
			var victim = Path.Combine(fixture.Root, "victim");
			var (ra2, language) = fixture.WriteValidBaseArchives("source/ra2.mix", "language.mix");
			Directory.CreateDirectory(victim);
			File.WriteAllText(Path.Combine(victim, "ra2.mix"), "VICTIM-NOT-A-MIX");
			var swapped = false;
			var trackUnixDescriptors = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
				RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
			var descriptorsBefore = trackUnixDescriptors ? Directory.GetFiles("/dev/fd").Length : 0;
			var stableCatalog = new RetailContentCatalog(new RetailStableFileOpener((_, component) =>
			{
				if (swapped || component != "ra2.mix")
					return;

				Directory.Move(source, moved);
				Directory.CreateSymbolicLink(source, victim);
				swapped = true;
			}));

			var status = stableCatalog.Inspect(new[] { ra2, language });

			Assert.That(swapped, Is.True);
			Assert.That(status.Base, Is.EqualTo(ContentState.Ready));
			if (trackUnixDescriptors)
				Assert.That(Directory.GetFiles("/dev/fd").Length, Is.EqualTo(descriptorsBefore));
		}

		[Test]
		public void UnixNoFollowLayoutsAreExplicitForEverySupportedArchitecture()
		{
			Assert.That(RetailStableFileOpener.TryGetUnixLayout(OSPlatform.OSX, Architecture.Arm64,
				out var darwinArm), Is.True);
			Assert.That((darwinArm.DirectoryFlags, darwinArm.FileFlags, darwinArm.ModeOffset, darwinArm.ModeWidth),
				Is.EqualTo((0x1100100, 0x1000100, 4, 2)));
			Assert.That(RetailStableFileOpener.TryGetUnixLayout(OSPlatform.OSX, Architecture.X64,
				out var darwinX64), Is.True);
			Assert.That((darwinX64.DirectoryFlags, darwinX64.FileFlags, darwinX64.ModeOffset, darwinX64.ModeWidth),
				Is.EqualTo((0x1100100, 0x1000100, 8, 2)));

			Assert.That(RetailStableFileOpener.TryGetUnixLayout(OSPlatform.Linux, Architecture.Arm64,
				out var linuxArm), Is.True);
			Assert.That((linuxArm.DirectoryFlags, linuxArm.FileFlags, linuxArm.ModeOffset, linuxArm.ModeWidth),
				Is.EqualTo((0xB0000, 0xA0000, 16, 4)));

			Assert.That(RetailStableFileOpener.TryGetUnixLayout(OSPlatform.Linux, Architecture.X64,
				out var linuxX64), Is.True);
			Assert.That((linuxX64.DirectoryFlags, linuxX64.FileFlags, linuxX64.ModeOffset, linuxX64.ModeWidth),
				Is.EqualTo((0xB0000, 0xA0000, 24, 4)));
			Assert.That(RetailStableFileOpener.TryGetUnixLayout(OSPlatform.Linux, Architecture.X86,
				out _), Is.False);
		}

		[Test]
		public void LooseAudioCannotSubstituteForTheMandatoryArchiveLocalPair()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var ra2 = fixture.WriteNamedMix("ra2.mix", OpenRA.Mods.Cnc.FileSystem.PackageHashType.CRC32,
				("opaque.bin", new byte[] { 1 }));
			var language = fixture.WriteNamedMix("language.mix", OpenRA.Mods.Cnc.FileSystem.PackageHashType.CRC32,
				("opaque.bin", new byte[] { 1 }));
			var incomplete = catalog.Inspect(new[] { ra2, language, "AUDIO.BAG", "audio.idx" });
			var (validRa2, validLanguage) = fixture.WriteValidBaseArchives("valid/ra2.mix", "valid/language.mix");
			var ready = catalog.Inspect(new[] { validRa2, validLanguage });

			Assert.That(incomplete.Audio, Is.EqualTo(ContentState.Incomplete));
			Assert.That(incomplete.CanLaunchBaseGame, Is.False);
			Assert.That(ready.Audio, Is.EqualTo(ContentState.Ready));
			Assert.That(ready.CanLaunchBaseGame, Is.True);
		}

		[Test]
		public void YuriRequiresBothExpansionArchives()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var (ra2, language) = fixture.WriteValidBaseArchives();
			var ra2md = fixture.WriteNamedMix("ra2md.mix", OpenRA.Mods.Cnc.FileSystem.PackageHashType.CRC32,
				("opaque.bin", new byte[] { 1 }));
			var incomplete = catalog.Inspect(new[] { ra2, language, ra2md });
			var langmd = fixture.WriteNamedMix("langmd.mix", OpenRA.Mods.Cnc.FileSystem.PackageHashType.CRC32,
				("opaque.bin", new byte[] { 1 }));
			var ready = catalog.Inspect(new[] { ra2, language, ra2md, langmd });

			Assert.That(incomplete.Yuri, Is.EqualTo(ContentState.Incomplete));
			Assert.That(incomplete.CanLaunchYuriGame, Is.False);
			Assert.That(ready.Yuri, Is.EqualTo(ContentState.Ready));
			Assert.That(ready.CanLaunchYuriGame, Is.True);
		}

		[Test]
		public void ImportedLooseMapsAreReportedSeparately()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var (ra2, language) = fixture.WriteValidBaseArchives();
			var status = catalog.Inspect(new[]
			{
				ra2,
				language,
				"Maps/Custom.MPR",
			});

			Assert.That(status.Maps, Is.EqualTo(ContentState.Ready));
			Assert.That(status.CanLaunchBaseGame, Is.True);
		}
	}
}
