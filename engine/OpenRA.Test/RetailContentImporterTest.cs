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
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenRA.FileSystem;
using OpenRA.Mods.Cnc.FileSystem;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailContentImporterTest
	{
		string root;
		string source;
		string content;
		SyntheticRetailFixtureBuilder fixture;
		string baseRa2;
		string baseLanguage;

		[SetUp]
		public void SetUp()
		{
			root = Path.Combine(Path.GetTempPath(), "openra-retail-import-" + Guid.NewGuid().ToString("N"));
			source = Path.Combine(root, "source");
			content = Path.Combine(root, "Content");
			Directory.CreateDirectory(source);
			fixture = new SyntheticRetailFixtureBuilder();
			(baseRa2, baseLanguage) = fixture.WriteValidBaseArchives();
		}

		[TearDown]
		public void TearDown()
		{
			try
			{
				if (Directory.Exists(root))
					Directory.Delete(root, true);
			}
			finally
			{
				fixture?.Dispose();
			}
		}

		string WriteSource(string name, string value = "data")
		{
			var path = Path.Combine(source, name);
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path, value);
			return path;
		}

		string WriteOramap(string relativePath, byte mapByte)
		{
			return WriteOramap(relativePath, new[] { mapByte });
		}

		string WriteOramap(string relativePath, byte[] mapBytes)
		{
			var path = Path.Combine(source, relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			using (var package = ZipFileLoader.Create(path))
			{
				package.Update("map.yaml", Encoding.UTF8.GetBytes("MapFormat: 11\n"));
				package.Update("map.bin", mapBytes);
			}

			return path;
		}

		string WriteBaseRa2WithCorruptRuntimePackage(string corruptPackage)
		{
			var packageNames = new[]
			{
				"cache.mix", "conquer.mix", "generic.mix", "isogen.mix", "isosnow.mix",
				"isotemp.mix", "isourb.mix", "load.mix", "local.mix", "neutral.mix", "sidec01.mix",
				"sidec02.mix", "sno.mix", "snow.mix", "tem.mix", "temperat.mix", "urb.mix", "urban.mix",
			};
			var packages = packageNames.Select(name => (Name: name, Data:
				string.Equals(name, corruptPackage, StringComparison.Ordinal) ? new byte[] { 1 } :
				fixture.BuildNamedMix(PackageHashType.CRC32,
					("synthetic.bin", Encoding.ASCII.GetBytes(SyntheticRetailFixtureBuilder.Sentinel + ":" + name)))))
				.ToArray();
			return fixture.WriteNamedMix("corrupt-runtime/ra2.mix", PackageHashType.CRC32, packages);
		}

		RetailImportRequest BaseRequest(params string[] extras)
		{
			var files = new[] { baseRa2, baseLanguage }.Concat(extras);
			return new RetailImportRequest(files, content, long.MaxValue);
		}

		[Test]
		public async Task CompleteBaseContentIsPublishedWithHashes()
		{
			var result = await new RetailContentImporter().ImportAsync(BaseRequest(), CancellationToken.None);

			Assert.That(result.Published, Is.True);
			Assert.That(result.Error, Is.EqualTo(RetailImportError.None));
			Assert.That(File.Exists(Path.Combine(content, "ra2", "ra2.mix")), Is.True);
			using var sha = SHA256.Create();
			using var input = File.OpenRead(baseRa2);
			var expected = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
			Assert.That(result.Hashes["ra2.mix"], Is.EqualTo(expected));
		}

		[Test]
		public async Task PublishedContentIncludesTheCurrentRuntimeLayoutMarker()
		{
			var result = await new RetailContentImporter(requireStructuralValidation: false)
				.ImportAsync(BaseRequest(), CancellationToken.None);

			Assert.That(result.Published, Is.True);
			Assert.That(File.Exists(Path.Combine(content, "ra2", ".ios-runtime-layout-v1")), Is.True);
		}

		[Test]
		public async Task LanguageArchiveSuppliesTheCameoRuntimePackage()
		{
			var result = await new RetailContentImporter(requireStructuralValidation: false)
				.ImportAsync(BaseRequest(), CancellationToken.None);

			Assert.That(result.Published, Is.True);
			Assert.That(new FileInfo(Path.Combine(content, "ra2", "cameo.mix")).Length, Is.GreaterThan(0));
		}

		[Test]
		public async Task LegacyRuntimeMarkerAcceptsCameoThatRemainsNestedInLanguageArchive()
		{
			var result = await new RetailContentImporter(requireStructuralValidation: false)
				.ImportAsync(BaseRequest(), CancellationToken.None);
			var importedRoot = Path.Combine(content, "ra2");
			File.Delete(Path.Combine(importedRoot, "cameo.mix"));

			Assert.That(result.Published, Is.True);
			Assert.That(RetailContentImporter.HasCurrentRuntimeLayout(importedRoot), Is.True);
		}

		[TestCase("language.mix")]
		[TestCase("audio.mix")]
		public async Task RuntimeMarkerRejectsNonemptyArchiveCorruptedAfterPublication(string archiveName)
		{
			var result = await new RetailContentImporter(requireStructuralValidation: false)
				.ImportAsync(BaseRequest(), CancellationToken.None);
			var importedRoot = Path.Combine(content, "ra2");
			File.WriteAllBytes(Path.Combine(importedRoot, archiveName), new byte[] { 1 });

			Assert.That(result.Published, Is.True);
			Assert.That(RetailContentImporter.HasCurrentRuntimeLayout(importedRoot), Is.False);
		}

		[TestCase("payload.dylib")]
		[TestCase("payload.exe")]
		[TestCase("payload.dll")]
		public async Task ExecutableExtensionsAreRejected(string name)
		{
			var result = await new RetailContentImporter().ImportAsync(
				BaseRequest(WriteSource(name)), CancellationToken.None);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.ExecutableContent));
			Assert.That(result.Published, Is.False);
		}

		[Test]
		public async Task ExecutableMagicIsRejectedEvenWithMixExtension()
		{
			var executable = Path.Combine(source, "payload.mix");
			File.WriteAllBytes(executable, new byte[] { 0xCF, 0xFA, 0xED, 0xFE, 1, 2, 3, 4 });

			var result = await new RetailContentImporter().ImportAsync(
				BaseRequest(executable), CancellationToken.None);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.ExecutableContent));
		}

		[Test]
		public async Task UnsupportedExtensionsAreRejected()
		{
			var result = await new RetailContentImporter().ImportAsync(
				BaseRequest(WriteSource("notes.txt")), CancellationToken.None);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.UnsupportedFile));
		}

		[Test]
		public async Task CancellationDoesNotPublishContent()
		{
			using var cancellation = new CancellationTokenSource();
			cancellation.Cancel();

			var result = await new RetailContentImporter().ImportAsync(BaseRequest(), cancellation.Token);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.Cancelled));
			Assert.That(Directory.Exists(Path.Combine(content, "ra2")), Is.False);
		}

		[Test]
		public async Task InsufficientSpaceIsRejectedBeforeCopy()
		{
			var result = await new RetailContentImporter().ImportAsync(
				new RetailImportRequest(BaseRequest().SourceFiles, content, 1), CancellationToken.None);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.InsufficientSpace));
			Assert.That(Directory.Exists(Path.Combine(content, ".staging")), Is.False);
		}

		[Test]
		public async Task DuplicateNamesAreRejectedCaseInsensitively()
		{
			var duplicateDirectory = Path.Combine(source, "duplicate");
			Directory.CreateDirectory(duplicateDirectory);
			var duplicate = Path.Combine(duplicateDirectory, "RA2.MIX");
			File.WriteAllText(duplicate, "duplicate");

			var result = await new RetailContentImporter().ImportAsync(
				BaseRequest(duplicate), CancellationToken.None);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.DuplicateFile));
		}

		[Test]
		public async Task StaleStagingIsCleanedBeforeSuccessfulImport()
		{
			var stale = Path.Combine(content, ".staging", "interrupted");
			Directory.CreateDirectory(stale);
			File.WriteAllText(Path.Combine(stale, "partial.mix"), "partial");

			var result = await new RetailContentImporter().ImportAsync(BaseRequest(), CancellationToken.None);

			Assert.That(result.Published, Is.True);
			Assert.That(Directory.EnumerateFileSystemEntries(Path.Combine(content, ".staging")), Is.Empty);
		}

		[Test]
		public async Task StaleStagingIsCleanedBeforeSpacePreflightRejectsImport()
		{
			var stale = Path.Combine(content, ".staging", "interrupted");
			Directory.CreateDirectory(stale);
			var partial = Path.Combine(stale, "partial.mix");
			File.WriteAllText(partial, "partial");

			var result = await new RetailContentImporter().ImportAsync(
				new RetailImportRequest(BaseRequest().SourceFiles, content, 1), CancellationToken.None);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.InsufficientSpace));
			Assert.That(File.Exists(partial), Is.False);
		}

		[Test]
		public async Task FailedImportPreservesPreviousReadyDirectory()
		{
			var importer = new RetailContentImporter();
			Assert.That((await importer.ImportAsync(BaseRequest(), CancellationToken.None)).Published, Is.True);
			File.WriteAllText(Path.Combine(content, "ra2", "marker.dat"), "previous");

			var failed = await importer.ImportAsync(
				new RetailImportRequest(new[] { WriteSource("replacement.mix") }, content, long.MaxValue),
				CancellationToken.None);

			Assert.That(failed.Error, Is.EqualTo(RetailImportError.IncompleteBaseContent));
			Assert.That(File.ReadAllText(Path.Combine(content, "ra2", "marker.dat")), Is.EqualTo("previous"));
		}

		[Test]
		public async Task ImportRecoversPreviousDirectoryLeftByInterruptedPublish()
		{
			var theme = WriteSource("theme.mix", "owned-ra2-music");
			var importer = new RetailContentImporter(requireStructuralValidation: false);
			Assert.That((await importer.ImportAsync(BaseRequest(theme), CancellationToken.None)).Published, Is.True);
			var current = Path.Combine(content, "ra2");
			var previous = Path.Combine(content, ".previous");
			Directory.Move(current, previous);

			var result = await importer.ImportAsync(BaseRequest(), CancellationToken.None);

			Assert.That(result.Published, Is.True);
			Assert.That(File.ReadAllText(Path.Combine(current, "theme.mix")), Is.EqualTo("owned-ra2-music"));
			Assert.That(Directory.Exists(previous), Is.False);
		}

		[Test]
		public async Task StartupPreparationRestoresPreviousWithoutStartingAnotherImport()
		{
			var importer = new RetailContentImporter(requireStructuralValidation: false);
			Assert.That((await importer.ImportAsync(BaseRequest(), CancellationToken.None)).Published, Is.True);
			var current = Path.Combine(content, "ra2");
			var previous = Path.Combine(content, ".previous");
			Directory.Move(current, previous);

			Assert.That(RetailContentImporter.PrepareCurrentRuntimeLayout(content), Is.True);
			Assert.That(Directory.Exists(current), Is.True);
			Assert.That(Directory.Exists(previous), Is.False);
			Assert.That(RetailContentImporter.HasCurrentRuntimeLayout(current), Is.True);
			Assert.That(RetailContentImporter.PrepareCurrentRuntimeLayout(content), Is.True,
				"Startup recovery must remain safe and idempotent after the first check.");
		}

		[Test]
		public async Task BaseOnlyReimportPreservesPreviouslyImportedOptionalMusic()
		{
			var theme = WriteSource("theme.mix", "owned-ra2-music");
			var themeMd = WriteSource("thememd.mix", "owned-yuri-music");
			var importer = new RetailContentImporter(requireStructuralValidation: false);
			Assert.That((await importer.ImportAsync(BaseRequest(theme, themeMd), CancellationToken.None)).Published, Is.True);

			var result = await importer.ImportAsync(BaseRequest(), CancellationToken.None);

			Assert.That(result.Published, Is.True);
			Assert.That(File.ReadAllText(Path.Combine(content, "ra2", "theme.mix")),
				Is.EqualTo("owned-ra2-music"));
			Assert.That(File.ReadAllText(Path.Combine(content, "ra2", "thememd.mix")),
				Is.EqualTo("owned-yuri-music"));
		}

		[Test]
		public async Task OptionalMusicCanBeAddedWithoutSelectingBaseAgain()
		{
			var importer = new RetailContentImporter(requireStructuralValidation: false);
			Assert.That((await importer.ImportAsync(BaseRequest(), CancellationToken.None)).Published, Is.True);
			var theme = WriteSource("theme.mix", "owned-ra2-music");

			var result = await importer.ImportAsync(
				new RetailImportRequest(new[] { theme }, content, long.MaxValue), CancellationToken.None);

			Assert.That(result.Published, Is.True);
			Assert.That(File.Exists(Path.Combine(content, "ra2", "ra2.mix")), Is.True);
			Assert.That(File.ReadAllText(Path.Combine(content, "ra2", "theme.mix")),
				Is.EqualTo("owned-ra2-music"));
		}

		[Test]
		public async Task OptionalMusicImportPreservesExistingUserContentTree()
		{
			var importer = new RetailContentImporter(requireStructuralValidation: false);
			Assert.That((await importer.ImportAsync(BaseRequest(), CancellationToken.None)).Published, Is.True);
			var userMap = Path.Combine(content, "ra2", "maps", "nested", "owned-map.oramap");
			Directory.CreateDirectory(Path.GetDirectoryName(userMap));
			File.WriteAllText(userMap, "user-owned-map");
			var theme = WriteSource("theme.mix", "owned-ra2-music");

			var result = await importer.ImportAsync(
				new RetailImportRequest(new[] { theme }, content, long.MaxValue), CancellationToken.None);

			Assert.That(result.Published, Is.True);
			Assert.That(File.ReadAllText(userMap), Is.EqualTo("user-owned-map"));
		}

		[Test]
		public void PickerCandidateFilterAcceptsRetailRootsAndOfficialMapPackages()
		{
			var method = typeof(RetailContentImporter).GetMethod("IsSupportedPickerFile");
			Assert.That(method, Is.Not.Null, "The folder picker must share the importer's explicit candidate filter.");

			bool Accepted(string path) => (bool)method.Invoke(null, new object[] { path });
			Assert.Multiple(() =>
			{
				Assert.That(Accepted("disc/RA2.MIX"), Is.True);
				Assert.That(Accepted("disc/THEME.MIX"), Is.True);
				Assert.That(Accepted("disc/MAPS01.MIX"), Is.True);
				Assert.That(Accepted("disc/maps02.mix"), Is.True);
				Assert.That(Accepted("disc/MAPSMD03.MIX"), Is.True);
				Assert.That(Accepted("disc/MULTI.MIX"), Is.True);
				Assert.That(Accepted("disc/MULTIMD.MIX"), Is.True);
				Assert.That(Accepted("disc/maps/owned.oramap"), Is.False);
				Assert.That(Accepted("disc/maps/owned.map"), Is.False);
				Assert.That(Accepted("disc/maps/owned.mpr"), Is.False);
				Assert.That(Accepted("disc/maps/owned.yrm"), Is.False);
				Assert.That(Accepted("disc/gamemd.exe"), Is.False);
				Assert.That(Accepted("disc/ra2.ini"), Is.False);
			});
		}

		[Test]
		public async Task MissingEmbeddedAudioDoesNotPublishRuntimeMarker()
		{
			var languageWithoutAudio = fixture.WriteMinimalMix("missing-audio/language.mix");
			var result = await new RetailContentImporter(requireStructuralValidation: false).ImportAsync(
				new RetailImportRequest(new[] { baseRa2, languageWithoutAudio }, content, long.MaxValue),
				CancellationToken.None);

			Assert.That(result.Published, Is.False);
			Assert.That(result.Error, Is.EqualTo(RetailImportError.IncompleteBaseContent));
			Assert.That(File.Exists(Path.Combine(content, "ra2", RetailContentImporter.IosRuntimeLayoutMarker)), Is.False);
		}

		[Test]
		public async Task MissingEmbeddedBaseRuntimePackageDoesNotPublishRuntimeMarker()
		{
			var incompleteRa2 = fixture.WriteNamedMix("missing-runtime/ra2.mix", PackageHashType.CRC32,
				("cache.mix", fixture.BuildNamedMix(PackageHashType.CRC32,
					("synthetic.bin", new byte[] { 1 }))));
			var result = await new RetailContentImporter(requireStructuralValidation: false).ImportAsync(
				new RetailImportRequest(new[] { incompleteRa2, baseLanguage }, content, long.MaxValue),
				CancellationToken.None);

			Assert.That(result.Published, Is.False);
			Assert.That(result.Error, Is.EqualTo(RetailImportError.IncompleteBaseContent));
			Assert.That(File.Exists(Path.Combine(content, "ra2", RetailContentImporter.IosRuntimeLayoutMarker)), Is.False);
		}

		[Test]
		public async Task CorruptEmbeddedRuntimePackageDoesNotReplaceLaunchableCurrentContent()
		{
			var theme = WriteSource("theme.mix", "owned-ra2-music");
			var importer = new RetailContentImporter(requireStructuralValidation: false);
			Assert.That((await importer.ImportAsync(BaseRequest(theme), CancellationToken.None)).Published, Is.True);
			var current = Path.Combine(content, "ra2");
			var originalRa2 = File.ReadAllBytes(Path.Combine(current, "ra2.mix"));
			Assert.That(RetailContentImporter.HasCurrentRuntimeLayout(current), Is.True);

			var corruptRa2 = WriteBaseRa2WithCorruptRuntimePackage("cache.mix");
			var result = await importer.ImportAsync(
				new RetailImportRequest(new[] { corruptRa2, baseLanguage }, content, long.MaxValue),
				CancellationToken.None);

			Assert.That(result.Published, Is.False);
			Assert.That(result.Error, Is.EqualTo(RetailImportError.IncompleteBaseContent));
			Assert.That(File.ReadAllBytes(Path.Combine(current, "ra2.mix")), Is.EqualTo(originalRa2));
			Assert.That(File.ReadAllText(Path.Combine(current, "theme.mix")), Is.EqualTo("owned-ra2-music"));
			Assert.That(File.Exists(Path.Combine(current, RetailContentImporter.IosRuntimeLayoutMarker)), Is.True);
			Assert.That(RetailContentImporter.HasCurrentRuntimeLayout(current), Is.True);
		}

		[Test]
		public async Task MalformedMixReturnsAnImportFailureInsteadOfThrowing()
		{
			var corruptRa2 = fixture.WriteMix("corrupt/ra2.mix", 0, 0,
				new[]
				{
					(Hash: PackageEntry.HashFilename("cache.mix", PackageHashType.CRC32),
						Offset: uint.MaxValue, Length: 1U),
				}, Array.Empty<byte>());

			var result = await new RetailContentImporter(requireStructuralValidation: false).ImportAsync(
				new RetailImportRequest(new[] { corruptRa2, baseLanguage }, content, long.MaxValue),
				CancellationToken.None);

			Assert.That(result.Published, Is.False);
			Assert.That(result.Error, Is.EqualTo(RetailImportError.IncompleteBaseContent));
			Assert.That(Directory.Exists(Path.Combine(content, "ra2")), Is.False);
		}

		[Test]
		public async Task CleanupFailureDoesNotReplaceMalformedArchiveResult()
		{
			var corruptRa2 = fixture.WriteMix("cleanup-failure/ra2.mix", 0, 0,
				new[]
				{
					(Hash: PackageEntry.HashFilename("cache.mix", PackageHashType.CRC32),
						Offset: uint.MaxValue, Length: 1U),
				}, Array.Empty<byte>());
			var importer = new RetailContentImporter(new RetailContentCatalog(), false,
				_ => throw new IOException("cleanup failed"));

			var result = await importer.ImportAsync(
				new RetailImportRequest(new[] { corruptRa2, baseLanguage }, content, long.MaxValue),
				CancellationToken.None);

			Assert.That(result.Published, Is.False);
			Assert.That(result.Error, Is.EqualTo(RetailImportError.IncompleteBaseContent));
			Assert.That(result.Message, Does.Not.Contain("cleanup failed"));
		}

		[Test]
		public async Task SemanticallyIncompleteOramapIsRejectedBeforePublication()
		{
			var map = WriteOramap("picked-installation/maps/owned.oramap", 1);

			var result = await new RetailContentImporter(requireStructuralValidation: false)
				.ImportAsync(BaseRequest(map), CancellationToken.None);

			Assert.That(result.Published, Is.False);
			Assert.That(result.Error, Is.EqualTo(RetailImportError.UnsupportedFile));
			Assert.That(Directory.Exists(Path.Combine(content, "ra2")), Is.False);
		}

		[Test]
		public async Task OramapExpansionBombIsRejectedWithoutKeepingTheLegacyUnboundedParser()
		{
			var expandedMapBytes = new byte[1 << 20];
			var bomb = WriteOramap("picked-installation/maps/bomb.oramap", expandedMapBytes);
			Assert.That(new FileInfo(bomb).Length * IosImportEnvelopePolicyV1.OramapExpansionRatio,
				Is.LessThan(expandedMapBytes.LongLength), "The test input must exceed the production expansion-ratio limit.");

			var result = await new RetailContentImporter(requireStructuralValidation: false)
				.ImportAsync(BaseRequest(bomb), CancellationToken.None);

			Assert.That(result.Published, Is.False);
			Assert.That(result.Error, Is.EqualTo(RetailImportError.UnsupportedFile));
			Assert.That(typeof(RetailContentImporter).GetMethod("TryGetLoadableOramapUid",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static), Is.Null,
				"Rejected maps must not retain the legacy whole-file/unbounded decompression path.");
		}

		[Test]
		public async Task MalformedOramapIsRejectedInsteadOfReportedAsImported()
		{
			var importer = new RetailContentImporter(requireStructuralValidation: false);
			Assert.That((await importer.ImportAsync(BaseRequest(), CancellationToken.None)).Published, Is.True);
			var malformed = WriteSource("maps/malformed.oramap", "not a zip map");

			var result = await importer.ImportAsync(
				new RetailImportRequest(new[] { malformed }, content, long.MaxValue, source),
				CancellationToken.None);

			Assert.That(result.Published, Is.False);
			Assert.That(result.Error, Is.EqualTo(RetailImportError.UnsupportedFile));
			Assert.That(Directory.GetFiles(Path.Combine(content, "ra2"), "malformed.oramap",
				SearchOption.AllDirectories), Is.Empty);
		}

		[Test]
		public async Task OptionalImportPreservesExpandedUserMapDirectory()
		{
			var importer = new RetailContentImporter(requireStructuralValidation: false);
			Assert.That((await importer.ImportAsync(BaseRequest(), CancellationToken.None)).Published, Is.True);
			var expanded = Path.Combine(content, "ra2", "maps", "owned-expanded");
			Directory.CreateDirectory(expanded);
			File.WriteAllText(Path.Combine(expanded, "map.yaml"), "MapFormat: 11\n");
			File.WriteAllBytes(Path.Combine(expanded, "map.bin"), new byte[] { 1, 2, 3 });
			File.WriteAllBytes(Path.Combine(expanded, "map.png"), new byte[] { 4, 5, 6 });

			var result = await importer.ImportAsync(
				new RetailImportRequest(new[] { WriteSource("theme.mix", "owned-music") },
					content, long.MaxValue), CancellationToken.None);

			Assert.That(result.Published, Is.True);
			Assert.That(File.ReadAllText(Path.Combine(expanded, "map.yaml")), Is.EqualTo("MapFormat: 11\n"));
			Assert.That(File.ReadAllBytes(Path.Combine(expanded, "map.bin")), Is.EqualTo(new byte[] { 1, 2, 3 }));
			Assert.That(File.ReadAllBytes(Path.Combine(expanded, "map.png")), Is.EqualTo(new byte[] { 4, 5, 6 }));
		}

		[Test]
		public async Task SpacePreflightIncludesEmbeddedRuntimePackages()
		{
			var rootArchiveBytes = new FileInfo(baseRa2).Length + new FileInfo(baseLanguage).Length;
			var result = await new RetailContentImporter(requireStructuralValidation: false).ImportAsync(
				new RetailImportRequest(new[] { baseRa2, baseLanguage }, content, rootArchiveBytes),
				CancellationToken.None);

			Assert.That(result.Published, Is.False);
			Assert.That(result.Error, Is.EqualTo(RetailImportError.InsufficientSpace));
		}

		[Test]
		public async Task SpacePreflightIncludesRuntimePackageRegeneratedFromRetainedRoot()
		{
			var importer = new RetailContentImporter(requireStructuralValidation: false);
			Assert.That((await importer.ImportAsync(BaseRequest(), CancellationToken.None)).Published, Is.True);
			var current = Path.Combine(content, "ra2");
			File.Delete(Path.Combine(current, "audio.mix"));
			var theme = WriteSource("theme.mix", "owned-ra2-music");
			var stagedSourceBytes = new FileInfo(Path.Combine(current, "ra2.mix")).Length +
				new FileInfo(Path.Combine(current, "language.mix")).Length + new FileInfo(theme).Length;

			var result = await importer.ImportAsync(
				new RetailImportRequest(new[] { theme }, content, stagedSourceBytes), CancellationToken.None);

			Assert.That(result.Published, Is.False);
			Assert.That(result.Error, Is.EqualTo(RetailImportError.InsufficientSpace));
		}
	}
}
