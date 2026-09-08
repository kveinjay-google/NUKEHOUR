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
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
	public sealed class RetailArchiveGraphValidatorTest
	{
		static readonly string[] CatalogRows =
		{
			"leaf\taudio.mix\taudio.bag", "leaf\taudio.mix\taudio.idx",
			"leaf\taudiomd.mix\taudio.bag", "leaf\taudiomd.mix\taudio.idx",
			"map-suffix\tlocal-db\t.map", "map-suffix\tlocal-db\t.mpr",
			"map-suffix\tlocal-db\t.oramap", "map-suffix\tlocal-db\t.yrm",
			"package\tbase-root\tcache.mix", "package\tbase-root\tcameo.mix",
			"package\tbase-root\tconquer.mix", "package\tbase-root\tgeneric.mix",
			"package\tbase-root\tisogen.mix", "package\tbase-root\tisosnow.mix",
			"package\tbase-root\tisotemp.mix", "package\tbase-root\tisourb.mix",
			"package\tbase-root\tload.mix", "package\tbase-root\tlocal.mix",
			"package\tbase-root\tneutral.mix", "package\tbase-root\tsidec01.mix",
			"package\tbase-root\tsidec02.mix", "package\tbase-root\tsno.mix",
			"package\tbase-root\tsnow.mix", "package\tbase-root\ttem.mix",
			"package\tbase-root\ttemperat.mix", "package\tbase-root\turb.mix",
			"package\tbase-root\turban.mix", "package\tlangmd.mix\taudiomd.mix",
			"package\tlangmd.mix\tcameomd.mix", "package\tlanguage.mix\taudio.mix",
			"package\tra2md.mix\tcachemd.mix", "package\tra2md.mix\tconqmd.mix",
			"package\tra2md.mix\tdes.mix", "package\tra2md.mix\tdesert.mix",
			"package\tra2md.mix\tgenermd.mix", "package\tra2md.mix\tisodes.mix",
			"package\tra2md.mix\tisodesmd.mix", "package\tra2md.mix\tisogenmd.mix",
			"package\tra2md.mix\tisolun.mix", "package\tra2md.mix\tisolunmd.mix",
			"package\tra2md.mix\tisosnomd.mix", "package\tra2md.mix\tisotemmd.mix",
			"package\tra2md.mix\tisoubn.mix", "package\tra2md.mix\tisoubnmd.mix",
			"package\tra2md.mix\tisourbmd.mix", "package\tra2md.mix\tloadmd.mix",
			"package\tra2md.mix\tlocalmd.mix", "package\tra2md.mix\tlun.mix",
			"package\tra2md.mix\tlunar.mix", "package\tra2md.mix\tntrlmd.mix",
			"package\tra2md.mix\tsnowmd.mix", "package\tra2md.mix\tubn.mix",
			"package\tra2md.mix\turbann.mix", "resolver\tany-mix\tlocal mix database.dat",
			"root\tbase\tlanguage.mix", "root\tbase\tra2.mix",
			"root\tra2-music\ttheme.mix", "root\tyuri\tlangmd.mix",
			"root\tyuri\tra2md.mix", "root\tyuri-music\tthememd.mix",
		};

		[Test]
		public void IndependentCatalogLiteralPinsAllRowsNamesHashesAndDigest()
		{
			Assert.That(CatalogRows, Is.EqualTo(CatalogRows.OrderBy(row => row, StringComparer.Ordinal)));
			Assert.That(CatalogRows, Has.Length.EqualTo(60));
			var bytes = Encoding.UTF8.GetBytes("ra2-retail-filename-catalog\0" + 1 + "\n" +
				string.Concat(CatalogRows.Select(row => row + "\n")));
			var digest = BitConverter.ToString(SHA256.HashData(bytes)).Replace("-", "").ToLowerInvariant();
			Assert.That(digest, Is.EqualTo("1dec73ccb18c0518a48b8e0c3a96ac914508338bae2335bad432bea12d04c440"));
			Assert.That(RetailFilenameCatalog.V1.Version, Is.EqualTo(1));
			Assert.That(RetailFilenameCatalog.V1.DigestSha256, Is.EqualTo(digest));

			var names = CatalogRows.Select(row => row.Split('\t')[2])
				.Where(name => !name.StartsWith('.')).Distinct(StringComparer.Ordinal).ToArray();
			Assert.That(names, Has.Length.EqualTo(54));
			foreach (var hashType in new[] { PackageHashType.Classic, PackageHashType.CRC32 })
			{
				var hashes = new HashSet<uint>();
				foreach (var name in names)
				{
					var hash = PackageEntry.HashFilename(name, hashType);
					Assert.That(hashes.Add(hash), Is.True, $"{hashType}:{name}");
					Assert.That(RetailFilenameCatalog.V1.TryResolve(hash, hashType, out var resolved), Is.True);
					Assert.That(resolved, Is.EqualTo(name));
				}
			}
		}

		[Test]
		public void OuterHeadersWithoutRequiredBaseAudioChainAreInvalid()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var ra2 = fixture.BuildNamedMix(PackageHashType.CRC32,
				("cache.mix", fixture.BuildNamedMix(PackageHashType.CRC32,
					("opaque.bin", Encoding.ASCII.GetBytes("x")))));
			var language = fixture.BuildNamedMix(PackageHashType.CRC32,
				("opaque.bin", Encoding.ASCII.GetBytes("x")));
			using var result = Validate(ra2, language);

			Assert.That(result.Groups[RetailContentGroup.Base].Validation, Is.EqualTo(RetailGroupValidation.Invalid));
			Assert.That(result.Groups.Values, Has.All.Property(nameof(RetailGroupStatus.Recognition))
				.EqualTo(RetailEditionRecognition.NotApplicable));
			Assert.That(result.GroupFailures.Keys, Is.EqualTo(new[] { RetailContentGroup.Base }));
			Assert.That(result.GroupFailures[RetailContentGroup.Base].Code,
				Is.EqualTo(RetailImportErrorCode.MissingNestedBasePackage));
			Assert.That(result.Groups[RetailContentGroup.Maps].Validation, Is.Not.EqualTo(RetailGroupValidation.Ready));
		}

		[Test]
		public void ValidBaseAudioAndLocalDatabaseMapProduceAnOwnedStructuralHandle()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var bag = new byte[] { 1, 2, 3, 4 };
			var idx = BuildIdx("sound", (uint)bag.Length, 6, 0);
			var audio = fixture.BuildNamedMix(PackageHashType.CRC32,
				("audio.idx", idx), ("audio.bag", bag));
			var language = fixture.BuildNamedMix(PackageHashType.CRC32, ("audio.mix", audio));
			var map = Encoding.ASCII.GetBytes("synthetic-map");
			var ra2 = fixture.BuildNamedMix(PackageHashType.CRC32,
				("local mix database.dat", SyntheticRetailFixtureBuilder.BuildLocalMixDatabase("test.map")),
				("test.map", map));
			var result = Validate(ra2, language);
			var handle = result.EmbeddedMaps.Single();
			var child = handle.OpenRead();

			Assert.That(result.Groups[RetailContentGroup.Base].Validation, Is.EqualTo(RetailGroupValidation.Ready));
			Assert.That(result.GroupFailures, Is.Empty);
			Assert.That(result.Groups[RetailContentGroup.Maps].Validation, Is.Not.EqualTo(RetailGroupValidation.Ready));
			Assert.That(handle.SafeName, Is.EqualTo("test.map"));
			Assert.That(handle.Length, Is.EqualTo(map.Length));
			result.Dispose();
			Assert.That(new StreamReader(child, Encoding.ASCII).ReadToEnd(), Is.EqualTo("synthetic-map"));
			child.Dispose();
			Assert.Throws<ObjectDisposedException>(() => handle.OpenRead());
		}

		[Test]
		public void CatalogOpensOwnedEmbeddedMapHandlesFromAnImportedDirectory()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var (_, language) = ValidBase(fixture);
			var map = Encoding.ASCII.GetBytes("catalog-map");
			var ra2 = fixture.BuildNamedMix(PackageHashType.CRC32,
				("local mix database.dat", SyntheticRetailFixtureBuilder.BuildLocalMixDatabase("catalog.map")),
				("catalog.map", map));
			fixture.WriteBytes("ra2.mix", ra2);
			fixture.WriteBytes("language.mix", language);

			var operation = new RetailContentCatalog().OpenValidatedArchiveGraph(
				fixture.Root, CancellationToken.None);

			Assert.That(operation.Succeeded, Is.True);
			using var graph = operation.Value;
			var handle = graph.EmbeddedMaps.Single();
			using var stream = handle.OpenVerifiedRead();
			Assert.That(stream.ReadAllBytes(), Is.EqualTo(map));
		}

		[Test]
		public void CompatibleScannerReadsTraditionalMapsFromOfficialMultiplayerPackage()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var map = Encoding.ASCII.GetBytes("[Basic]\nName=Test Arena\n");
			fixture.WriteNamedMix("MULTI.MIX", PackageHashType.CRC32,
				("local mix database.dat", SyntheticRetailFixtureBuilder.BuildLocalMixDatabase("arena.mpr")),
				("arena.mpr", map));

			var sources = new RetailCompatibleMapSourceScanner().Scan(
				fixture.Root, null, CancellationToken.None);
			try
			{
				var source = sources.Single();
				using var stream = source.OpenVerifiedRead();
				Assert.That(source.SafeName, Is.EqualTo("arena.mpr"));
				Assert.That(stream.ReadAllBytes(), Is.EqualTo(map));
			}
			finally
			{
				foreach (var source in sources)
					source.Dispose();
			}
		}

		[Test]
		public void NestedMapHandleSupportsChildFirstDisposalAndFinalClose()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var (_, language) = ValidBase(fixture);
			var map = Encoding.ASCII.GetBytes("nested-map");
			var nested = fixture.BuildNamedMix(PackageHashType.CRC32,
				("local mix database.dat", SyntheticRetailFixtureBuilder.BuildLocalMixDatabase("nested.map")),
				("nested.map", map));
			var ra2 = fixture.BuildNamedMix(PackageHashType.CRC32, ("cache.mix", nested));
			var ra2Stream = new TrackingStream(ra2);
			var languageStream = new TrackingStream(language);
			var batch = RetailArchiveInputBatch.TakeOwnership(ImmutableArray.Create(
				RetailOwnedArchiveInput.TakeOwnership(Descriptor("ra2.mix", RetailContentGroup.Base, ra2), ra2Stream),
				RetailOwnedArchiveInput.TakeOwnership(Descriptor("language.mix", RetailContentGroup.Base, language),
					languageStream)));
			var clock = new FakeClock();
			var result = Succeeded(new RetailArchiveGraphValidator(Policy(), clock, RetailFilenameCatalog.V1)
				.ValidateOwned(batch, RetailValidationDeadline.Start(clock,
					IosImportEnvelopePolicyV1.WholeValidationTimeout), default));
			var handle = result.EmbeddedMaps.Single();
			var child = handle.OpenRead();

			Assert.That(new StreamReader(child, Encoding.ASCII, false, 1024, leaveOpen: true).ReadToEnd(),
				Is.EqualTo("nested-map"));
			child.Dispose();
			Assert.That(ra2Stream.DisposeCalls, Is.EqualTo(0));
			result.Dispose();
			Assert.That((ra2Stream.DisposeCalls, languageStream.DisposeCalls), Is.EqualTo((1, 1)));
			Assert.Throws<ObjectDisposedException>(() => handle.OpenRead());
			result.Dispose();
			Assert.That(ra2Stream.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void OwnedBatchRejectsDuplicateObjectsAndRollsBackEveryTransferredInput()
		{
			var bytes = new SyntheticRetailFixtureBuilder().BuildNamedMix(PackageHashType.CRC32,
				("opaque.bin", new byte[] { 1 }));
			var stream = new TrackingStream(bytes);
			var descriptor = Descriptor("ra2.mix", RetailContentGroup.Base, bytes);
			var input = RetailOwnedArchiveInput.TakeOwnership(descriptor, stream);

			Assert.Throws<ArgumentException>(() => RetailArchiveInputBatch.TakeOwnership(
				ImmutableArray.Create(input, input)));
			Assert.That(stream.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void OwnedBatchRejectsDistinctInputsWithDuplicateCanonicalRootAndRollsBothBack()
		{
			var bytes = new byte[] { 1 };
			var firstStream = new TrackingStream(bytes);
			var secondStream = new TrackingStream(bytes);
			var descriptor = Descriptor("ra2.mix", RetailContentGroup.Base, bytes);
			var first = RetailOwnedArchiveInput.TakeOwnership(descriptor, firstStream);
			var second = RetailOwnedArchiveInput.TakeOwnership(descriptor, secondStream);

			Assert.Throws<ArgumentException>(() => RetailArchiveInputBatch.TakeOwnership(
				ImmutableArray.Create(first, second)));
			Assert.That((firstStream.DisposeCalls, secondStream.DisposeCalls), Is.EqualTo((1, 1)));
		}

		[Test]
		public void DescriptorAndInputBoundaryRejectsPathsWrongGroupsAndNonCanonicalHashes()
		{
			Assert.Throws<ArgumentException>(() => new RetailArchiveInputDescriptor(
				"folder/ra2.mix", RetailContentGroup.Base, 1, new string('0', 64)));
			Assert.Throws<ArgumentException>(() => new RetailArchiveInputDescriptor(
				"ra2.mix", RetailContentGroup.Yuri, 1, new string('0', 64)));
			Assert.Throws<ArgumentException>(() => new RetailArchiveInputDescriptor(
				"ra2.mix", RetailContentGroup.Base, 1, new string('A', 64)));

			var stream = new TrackingStream(new byte[] { 1 });
			stream.Position = 1;
			Assert.Throws<ArgumentException>(() => RetailOwnedArchiveInput.TakeOwnership(
				new RetailArchiveInputDescriptor("ra2.mix", RetailContentGroup.Base, 1, new string('0', 64)), stream));
			Assert.That(stream.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void PublicValidatorSurfaceCannotAcceptAPathRootFactoryDelegateOrCallerSource()
		{
			var publicMembers = typeof(RetailArchiveGraphValidator).GetMembers(BindingFlags.Public | BindingFlags.Instance);
			foreach (var parameter in publicMembers.OfType<MethodBase>().SelectMany(member => member.GetParameters()))
			{
				Assert.That(parameter.ParameterType, Is.Not.EqualTo(typeof(string)));
				Assert.That(typeof(Delegate).IsAssignableFrom(parameter.ParameterType), Is.False);
				Assert.That(parameter.ParameterType.Name, Does.Not.Contain("Source"));
			}

			Assert.That(typeof(RetailArchiveGraphResult).IsSealed, Is.True);
			Assert.That(typeof(RetailArchiveGraphResult).GetMethod("<Clone>$", BindingFlags.Instance |
				BindingFlags.Public | BindingFlags.NonPublic), Is.Null);
		}

		[Test]
		public void MissingValidatedMixCapabilityIsTypedCorruptionWithoutRawFallback()
		{
			var bytes = new byte[] { 1, 2, 3, 4 };
			var stream = new TrackingStream(bytes);
			var batch = RetailArchiveInputBatch.TakeOwnership(ImmutableArray.Create(
				RetailOwnedArchiveInput.TakeOwnership(Descriptor("theme.mix", RetailContentGroup.Ra2Music, bytes), stream)));
			var loader = new NonValidatedMixLoader();
			var replacement = new OpenRA.FileSystem.FileSystem("missing-mix-capability",
				ImmutableDictionary<string, Manifest>.Empty, new IPackageLoader[] { loader });
			var clock = new FakeClock();
			var validator = new RetailArchiveGraphValidator(Policy(), clock, RetailFilenameCatalog.V1);
			var fileSystemField = typeof(RetailArchiveGraphValidator).GetField("fileSystem",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(fileSystemField, Is.Not.Null);
			fileSystemField.SetValue(validator, replacement);

			using var result = Succeeded(validator.ValidateOwned(batch,
				RetailValidationDeadline.Start(clock, IosImportEnvelopePolicyV1.WholeValidationTimeout), default));
			AssertFailure(result, RetailContentGroup.Ra2Music, RetailImportErrorCode.CorruptArchive,
				"theme.mix", RetailImportDiagnosticCode.MalformedEnvelope);
			Assert.That(loader.BoundedCalls, Is.EqualTo(1));
			Assert.That(loader.Package.RawAccessCalls, Is.EqualTo(0));
			Assert.That(loader.Package.DisposeCalls, Is.EqualTo(1));
			Assert.That(stream.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void FiveGroupStructuralMatrixIsExactAndRecognitionIsDeferred()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var (ra2, language) = ValidBase(fixture);
			var opaque = OpaqueMix(fixture);
			using var result = Succeeded(ValidateOperation(new FakeClock(), Policy(), default,
				("ra2.mix", RetailContentGroup.Base, ra2),
				("language.mix", RetailContentGroup.Base, language),
				("theme.mix", RetailContentGroup.Ra2Music, opaque),
				("ra2md.mix", RetailContentGroup.Yuri, opaque),
				("langmd.mix", RetailContentGroup.Yuri, opaque),
				("thememd.mix", RetailContentGroup.YuriMusic, opaque)));

			Assert.That(result.Groups.Select(pair => (pair.Key, pair.Value.Validation)), Is.EqualTo(new[]
			{
				(RetailContentGroup.Base, RetailGroupValidation.Ready),
				(RetailContentGroup.Ra2Music, RetailGroupValidation.Unavailable),
				(RetailContentGroup.Yuri, RetailGroupValidation.Ready),
				(RetailContentGroup.YuriMusic, RetailGroupValidation.Unavailable),
				(RetailContentGroup.Maps, RetailGroupValidation.Missing),
			}));
			Assert.That(result.Groups.Values, Has.All.Property(nameof(RetailGroupStatus.Recognition))
				.EqualTo(RetailEditionRecognition.NotApplicable));
			Assert.That(result.GroupFailures, Is.Empty);
		}

		[Test]
		public void OptionalYuriAudioIsValidatedWhenPresentAndMayBeAbsent()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var (ra2, language) = ValidBase(fixture);
			var bag = new byte[] { 1, 2, 3, 4 };
			var validAudio = fixture.BuildNamedMix(PackageHashType.CRC32,
				("audio.idx", BuildIdx("sound", 4, 6, 0)), ("audio.bag", bag));
			var validLang = fixture.BuildNamedMix(PackageHashType.CRC32,
				("audiomd.mix", validAudio));
			using (var valid = Succeeded(ValidateOperation(new FakeClock(), Policy(), default,
				("ra2.mix", RetailContentGroup.Base, ra2),
				("language.mix", RetailContentGroup.Base, language),
				("ra2md.mix", RetailContentGroup.Yuri, OpaqueMix(fixture)),
				("langmd.mix", RetailContentGroup.Yuri, validLang))))
				Assert.That(valid.Groups[RetailContentGroup.Yuri].Validation,
					Is.EqualTo(RetailGroupValidation.Ready));

			var invalidAudio = fixture.BuildNamedMix(PackageHashType.CRC32,
				("audio.idx", BuildIdx("sound", 4, 6, 0)));
			var invalidLang = fixture.BuildNamedMix(PackageHashType.CRC32,
				("audiomd.mix", invalidAudio));
			using var invalid = Succeeded(ValidateOperation(new FakeClock(), Policy(), default,
				("ra2.mix", RetailContentGroup.Base, ra2),
				("language.mix", RetailContentGroup.Base, language),
				("ra2md.mix", RetailContentGroup.Yuri, OpaqueMix(fixture)),
				("langmd.mix", RetailContentGroup.Yuri, invalidLang)));
			AssertFailure(invalid, RetailContentGroup.Yuri, RetailImportErrorCode.InvalidAudio,
				"langmd.mix", RetailImportDiagnosticCode.MissingRequiredArchive);
		}

		[Test]
		public void EmptyBatchReportsEveryGroupMissingWithoutFailures()
		{
			using var result = Succeeded(ValidateOperation(new FakeClock(), Policy(), default));
			Assert.That(result.Groups.Values.Select(status => status.Validation),
				Is.All.EqualTo(RetailGroupValidation.Missing));
			Assert.That(result.GroupFailures, Is.Empty);
		}

		[TestCase("language.mix", RetailImportErrorCode.IncompleteBase, "ra2.mix")]
		[TestCase("ra2.mix", RetailImportErrorCode.IncompleteBase, "language.mix")]
		[TestCase("langmd.mix", RetailImportErrorCode.PartialYuri, "ra2md.mix")]
		[TestCase("ra2md.mix", RetailImportErrorCode.PartialYuri, "langmd.mix")]
		public void RootPairCompletenessHasFixedSafeCounterpart(
			string presentName, RetailImportErrorCode expectedCode, string missingName)
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var group = presentName.EndsWith("md.mix", StringComparison.Ordinal) ?
				RetailContentGroup.Yuri : RetailContentGroup.Base;
			using var result = Succeeded(ValidateOperation(new FakeClock(), Policy(), default,
				(presentName, group, OpaqueMix(fixture))));

			AssertFailure(result, group, expectedCode, missingName,
				RetailImportDiagnosticCode.MissingRequiredArchive);
		}

		[TestCase("ra2.mix", RetailContentGroup.Base)]
		[TestCase("language.mix", RetailContentGroup.Base)]
		[TestCase("theme.mix", RetailContentGroup.Ra2Music)]
		[TestCase("ra2md.mix", RetailContentGroup.Yuri)]
		[TestCase("langmd.mix", RetailContentGroup.Yuri)]
		[TestCase("thememd.mix", RetailContentGroup.YuriMusic)]
		public void MalformedRootUsesGroupLocalCorruptArchiveFailure(string name, RetailContentGroup group)
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var inputs = new List<(string Name, RetailContentGroup Group, byte[] Bytes)>();
			if (group == RetailContentGroup.Base)
			{
				var (ra2, language) = ValidBase(fixture);
				inputs.Add(("ra2.mix", group, name == "ra2.mix" ? new byte[] { 1 } : ra2));
				inputs.Add(("language.mix", group, name == "language.mix" ? new byte[] { 1 } : language));
			}
			else if (group == RetailContentGroup.Yuri)
			{
				var opaque = OpaqueMix(fixture);
				inputs.Add(("ra2md.mix", group, name == "ra2md.mix" ? new byte[] { 1 } : opaque));
				inputs.Add(("langmd.mix", group, name == "langmd.mix" ? new byte[] { 1 } : opaque));
			}
			else
				inputs.Add((name, group, new byte[] { 1 }));

			using var result = Succeeded(ValidateOperation(new FakeClock(), Policy(), default, inputs.ToArray()));
			AssertFailure(result, group, RetailImportErrorCode.CorruptArchive, name,
				RetailImportDiagnosticCode.MalformedEnvelope);
		}

		[Test]
		public void MultiFaultPriorityParsesRa2AndRa2mdBeforeTheirLanguageRoots()
		{
			using var baseResult = Succeeded(ValidateOperation(new FakeClock(), Policy(), default,
				("ra2.mix", RetailContentGroup.Base, new byte[] { 1 }),
				("language.mix", RetailContentGroup.Base, new byte[] { 2 })));
			AssertFailure(baseResult, RetailContentGroup.Base, RetailImportErrorCode.CorruptArchive,
				"ra2.mix", RetailImportDiagnosticCode.MalformedEnvelope);

			using var yuriResult = Succeeded(ValidateOperation(new FakeClock(), Policy(), default,
				("ra2md.mix", RetailContentGroup.Yuri, new byte[] { 1 }),
				("langmd.mix", RetailContentGroup.Yuri, new byte[] { 2 })));
			AssertFailure(yuriResult, RetailContentGroup.Yuri, RetailImportErrorCode.CorruptArchive,
				"ra2md.mix", RetailImportDiagnosticCode.MalformedEnvelope);
		}

		[TestCase(RetailContentGroup.Base, "ra2.mix", "language.mix")]
		[TestCase(RetailContentGroup.Yuri, "ra2md.mix", "langmd.mix")]
		public void FixedRootPrioritySpansEnvelopeAndContainerValidationPhases(
			RetailContentGroup group, string firstRoot, string secondRoot)
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var invalidDatabase = fixture.BuildNamedMix(PackageHashType.CRC32,
				("local mix database.dat", SyntheticRetailFixtureBuilder.BuildLocalMixDatabase("payload.exe")),
				("payload.exe", new byte[] { 1 }));
			using var result = Succeeded(ValidateOperation(new FakeClock(), Policy(), default,
				(firstRoot, group, invalidDatabase), (secondRoot, group, new byte[] { 1 })));

			AssertFailure(result, group, RetailImportErrorCode.CorruptArchive,
				firstRoot, RetailImportDiagnosticCode.MalformedEnvelope);
		}

		[Test]
		public void ArchiveStructureWinsOverInvalidAudioIndependentOfEntryOrder()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var ra2 = OpaqueMix(fixture);
			var invalidAudio = fixture.BuildNamedMix(PackageHashType.CRC32,
				("audio.idx", BuildIdx("sound", 4, 6, 0)));
			var disallowed = OpaqueMix(fixture);
			foreach (var entries in new[]
			{
				new[] { (Name: "audio.mix", Data: invalidAudio), (Name: "cache.mix", Data: disallowed) },
				new[] { (Name: "cache.mix", Data: disallowed), (Name: "audio.mix", Data: invalidAudio) },
			})
			{
				var language = fixture.BuildNamedMix(PackageHashType.CRC32, entries);
				using var result = Succeeded(ValidateOperation(new FakeClock(), Policy(), default,
					("ra2.mix", RetailContentGroup.Base, ra2),
					("language.mix", RetailContentGroup.Base, language)));
				AssertFailure(result, RetailContentGroup.Base, RetailImportErrorCode.CorruptArchive,
					"language.mix", RetailImportDiagnosticCode.MalformedEnvelope);
			}
		}

		[Test]
		public void RootLocalDatabaseStructureWinsOverMissingMandatoryAudio()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var invalidDatabase = SyntheticRetailFixtureBuilder.BuildLocalMixDatabase("payload.exe");
			var language = fixture.BuildNamedMix(PackageHashType.CRC32,
				("local mix database.dat", invalidDatabase), ("payload.exe", new byte[] { 1 }));
			using var result = Succeeded(ValidateOperation(new FakeClock(), Policy(), default,
				("ra2.mix", RetailContentGroup.Base, OpaqueMix(fixture)),
				("language.mix", RetailContentGroup.Base, language)));

			AssertFailure(result, RetailContentGroup.Base, RetailImportErrorCode.CorruptArchive,
				"language.mix", RetailImportDiagnosticCode.MalformedEnvelope);
		}

		[Test]
		public void MissingAndMalformedBaseAudioUseDistinctClosedFailures()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var ra2 = OpaqueMix(fixture);
			var missingAudio = fixture.BuildNamedMix(PackageHashType.CRC32,
				("opaque.bin", new byte[] { 1 }));
			using var missing = Succeeded(ValidateOperation(new FakeClock(), Policy(), default,
				("ra2.mix", RetailContentGroup.Base, ra2),
				("language.mix", RetailContentGroup.Base, missingAudio)));
			AssertFailure(missing, RetailContentGroup.Base, RetailImportErrorCode.MissingNestedBasePackage,
				"language.mix", RetailImportDiagnosticCode.MissingRequiredArchive);

			var audioWithoutBag = fixture.BuildNamedMix(PackageHashType.CRC32,
				("audio.idx", BuildIdx("sound", 4, 6, 0)));
			var invalidLanguage = fixture.BuildNamedMix(PackageHashType.CRC32,
				("audio.mix", audioWithoutBag));
			using var invalid = Succeeded(ValidateOperation(new FakeClock(), Policy(), default,
				("ra2.mix", RetailContentGroup.Base, ra2),
				("language.mix", RetailContentGroup.Base, invalidLanguage)));
			AssertFailure(invalid, RetailContentGroup.Base, RetailImportErrorCode.InvalidAudio,
				"language.mix", RetailImportDiagnosticCode.MissingRequiredArchive);
		}

		[Test]
		public void ArithmeticOverflowRemainsGroupLocalForArchivesAndAudio()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var (_, validLanguage) = ValidBase(fixture);
			var overflowRoot = BuildRawMix(1, new[] { (Hash: 1U, Offset: uint.MaxValue, Length: 2U) },
				new byte[] { 1 });
			using var archive = Succeeded(ValidateOperation(new FakeClock(), Policy(), default,
				("ra2.mix", RetailContentGroup.Base, overflowRoot),
				("language.mix", RetailContentGroup.Base, validLanguage)));
			AssertFailure(archive, RetailContentGroup.Base, RetailImportErrorCode.CorruptArchive,
				"ra2.mix", RetailImportDiagnosticCode.ArithmeticOverflow);

			var overflowingIdx = BuildIdx("sound", 2, 6, 0, uint.MaxValue);
			var audioMix = fixture.BuildNamedMix(PackageHashType.CRC32,
				("audio.idx", overflowingIdx), ("audio.bag", new byte[] { 1, 2 }));
			var language = fixture.BuildNamedMix(PackageHashType.CRC32, ("audio.mix", audioMix));
			using var audio = Succeeded(ValidateOperation(new FakeClock(), Policy(), default,
				("ra2.mix", RetailContentGroup.Base, OpaqueMix(fixture)),
				("language.mix", RetailContentGroup.Base, language)));
			AssertFailure(audio, RetailContentGroup.Base, RetailImportErrorCode.InvalidAudio,
				"language.mix", RetailImportDiagnosticCode.ArithmeticOverflow);
		}

		[Test]
		public void LocalDatabaseRejectsDuplicatesStaticNamesAndWrongSuffixButUnknownHashesRemainOpaque()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var (_, language) = ValidBase(fixture);
			foreach (var names in new[]
			{
				new[] { "test.map", "test.map" },
				new[] { "audio.mix" },
				new[] { "payload.exe" },
			})
			{
				var entries = new List<(string Name, byte[] Data)>
				{
					("local mix database.dat", SyntheticRetailFixtureBuilder.BuildLocalMixDatabase(names)),
				};
				entries.AddRange(names.Distinct(StringComparer.Ordinal).Select(name => (name, new byte[] { 1 })));
				var ra2 = fixture.BuildNamedMix(PackageHashType.CRC32, entries.ToArray());
				using var result = Succeeded(ValidateOperation(new FakeClock(), Policy(), default,
					("ra2.mix", RetailContentGroup.Base, ra2),
					("language.mix", RetailContentGroup.Base, language)));
				AssertFailure(result, RetailContentGroup.Base, RetailImportErrorCode.CorruptArchive,
					"ra2.mix", RetailImportDiagnosticCode.MalformedEnvelope);
			}

			using var opaque = Succeeded(ValidateOperation(new FakeClock(), Policy(), default,
				("ra2.mix", RetailContentGroup.Base, OpaqueMix(fixture)),
				("language.mix", RetailContentGroup.Base, language)));
			Assert.That(opaque.Groups[RetailContentGroup.Base].Validation, Is.EqualTo(RetailGroupValidation.Ready));
		}

		[Test]
		public void NestedDepthIsLocalButSharedAggregateExhaustionIsFatal()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var (ra2, language) = ValidBase(fixture);
			using var exact = Succeeded(ValidateOperation(new FakeClock(), Policy(depth: 3), default,
				("ra2.mix", RetailContentGroup.Base, ra2),
				("language.mix", RetailContentGroup.Base, language)));
			Assert.That(exact.Groups[RetailContentGroup.Base].Validation, Is.EqualTo(RetailGroupValidation.Ready));

			using var tooDeep = Succeeded(ValidateOperation(new FakeClock(), Policy(depth: 2), default,
				("ra2.mix", RetailContentGroup.Base, ra2),
				("language.mix", RetailContentGroup.Base, language)));
			AssertFailure(tooDeep, RetailContentGroup.Base, RetailImportErrorCode.InvalidAudio,
				"language.mix", RetailImportDiagnosticCode.MalformedEnvelope);

			var aggregate = ValidateOperation(new FakeClock(), Policy(aggregateEntries: 4), default,
				("ra2.mix", RetailContentGroup.Base, ra2),
				("language.mix", RetailContentGroup.Base, language));
			Assert.That(aggregate.Succeeded, Is.False);
			Assert.That(aggregate.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceLimitExceeded));
			Assert.That(aggregate.Failure.Diagnostic, Is.EqualTo(RetailImportDiagnosticCode.None));
		}

		[Test]
		public void SharedAddressedByteLedgerAcceptsExactAndRejectsOneBelowAsFatal()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var bag = new byte[] { 1, 2, 3, 4 };
			var idx = BuildIdx("sound", 4, 6, 0);
			var audio = fixture.BuildNamedMix(PackageHashType.CRC32,
				("audio.idx", idx), ("audio.bag", bag));
			var language = fixture.BuildNamedMix(PackageHashType.CRC32, ("audio.mix", audio));
			var ra2 = OpaqueMix(fixture);
			var exactBytes = checked(1L + audio.LongLength + idx.LongLength + bag.LongLength + bag.LongLength);
			using var exact = Succeeded(ValidateOperation(new FakeClock(),
				Policy(aggregateBytes: exactBytes), default,
				("ra2.mix", RetailContentGroup.Base, ra2),
				("language.mix", RetailContentGroup.Base, language)));
			Assert.That(exact.Groups[RetailContentGroup.Base].Validation,
				Is.EqualTo(RetailGroupValidation.Ready));

			var below = ValidateOperation(new FakeClock(), Policy(aggregateBytes: exactBytes - 1), default,
				("ra2.mix", RetailContentGroup.Base, ra2),
				("language.mix", RetailContentGroup.Base, language));
			Assert.That(below.Succeeded, Is.False);
			Assert.That(below.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceLimitExceeded));
			Assert.That(below.Failure.Diagnostic, Is.EqualTo(RetailImportDiagnosticCode.None));
		}

		[Test]
		public void DeadlineReturnsTypedFailureAndMatchingCancellationPropagates()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var (ra2, language) = ValidBase(fixture);
			var timedOut = ValidateOperation(new ExpiringClock(), Policy(), default,
				("ra2.mix", RetailContentGroup.Base, ra2),
				("language.mix", RetailContentGroup.Base, language));
			Assert.That(timedOut.Succeeded, Is.False);
			Assert.That(timedOut.Failure.Code, Is.EqualTo(RetailImportErrorCode.ValidationTimedOut));
			Assert.That(timedOut.Failure.Diagnostic, Is.EqualTo(RetailImportDiagnosticCode.DeadlineExceeded));

			using var cancelled = new CancellationTokenSource();
			cancelled.Cancel();
			Assert.Throws<OperationCanceledException>(() => ValidateOperation(new FakeClock(), Policy(),
				cancelled.Token, ("ra2.mix", RetailContentGroup.Base, ra2),
				("language.mix", RetailContentGroup.Base, language)));
		}

		[Test]
		public void BatchRollbackDisposesEveryDistinctTransferredElementAndDetachIsOneShot()
		{
			var bytes = new byte[] { 1 };
			var firstStream = new TrackingStream(bytes);
			var secondStream = new TrackingStream(bytes);
			var first = RetailOwnedArchiveInput.TakeOwnership(
				Descriptor("ra2.mix", RetailContentGroup.Base, bytes), firstStream);
			var second = RetailOwnedArchiveInput.TakeOwnership(
				Descriptor("language.mix", RetailContentGroup.Base, bytes), secondStream);
			Assert.Throws<ArgumentException>(() => RetailArchiveInputBatch.TakeOwnership(
				ImmutableArray.Create(first, null, second)));
			Assert.That((firstStream.DisposeCalls, secondStream.DisposeCalls), Is.EqualTo((1, 1)));

			var detachedStream = new TrackingStream(bytes);
			var detached = RetailOwnedArchiveInput.TakeOwnership(
				Descriptor("ra2.mix", RetailContentGroup.Base, bytes), detachedStream);
			Assert.That(detached.DetachOwnedPinnedStream(), Is.SameAs(detachedStream));
			Assert.Throws<InvalidOperationException>(() => detached.DetachOwnedPinnedStream());
			detached.Dispose();
			Assert.That(detachedStream.DisposeCalls, Is.EqualTo(0));
			detachedStream.Dispose();
		}

		[Test]
		public void ResultConstructorEnforcesRecognitionFailureBijectionAndClosedMapping()
		{
			var groups = Enum.GetValues<RetailContentGroup>().ToImmutableSortedDictionary(group => group,
				group => new RetailGroupStatus(group == RetailContentGroup.Base ? RetailGroupValidation.Invalid :
					RetailGroupValidation.Missing, RetailEditionRecognition.NotApplicable,
					group == RetailContentGroup.Base ? RetailImportDiagnosticCode.MissingRequiredArchive :
						RetailImportDiagnosticCode.None));
			Assert.Throws<ArgumentException>(() => new RetailArchiveGraphResult(groups,
				ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure>.Empty,
				ImmutableArray<ValidatedEmbeddedMapSource>.Empty));

			var wrong = ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure>.Empty.Add(
				RetailContentGroup.Base, new RetailImportFailure(RetailImportErrorCode.InvalidAudio,
					RetailContentGroup.Base, "ra2.mix", RetailImportDiagnosticCode.MissingRequiredArchive));
			Assert.Throws<ArgumentException>(() => new RetailArchiveGraphResult(groups, wrong,
				ImmutableArray<ValidatedEmbeddedMapSource>.Empty));

			var unavailableBase = groups.SetItem(RetailContentGroup.Base,
				new RetailGroupStatus(RetailGroupValidation.Unavailable,
					RetailEditionRecognition.NotApplicable, RetailImportDiagnosticCode.None));
			Assert.Throws<ArgumentException>(() => new RetailArchiveGraphResult(unavailableBase,
				ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure>.Empty,
				ImmutableArray<ValidatedEmbeddedMapSource>.Empty));
			var readyMaps = groups.SetItem(RetailContentGroup.Base,
				new RetailGroupStatus(RetailGroupValidation.Missing,
					RetailEditionRecognition.NotApplicable, RetailImportDiagnosticCode.None))
				.SetItem(RetailContentGroup.Maps, new RetailGroupStatus(RetailGroupValidation.Ready,
					RetailEditionRecognition.NotApplicable, RetailImportDiagnosticCode.None));
			Assert.Throws<ArgumentException>(() => new RetailArchiveGraphResult(readyMaps,
				ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure>.Empty,
				ImmutableArray<ValidatedEmbeddedMapSource>.Empty));
		}

		[Test]
		public void DuplicateEmbeddedHandleIdentityIsRejectedButDistinctEqualDigestsDedupeSafely()
		{
			var digest = BitConverter.ToString(SHA256.HashData(new byte[] { 1 }))
				.Replace("-", string.Empty).ToLowerInvariant();
			var context = new PackageReadContext(default, new NeverDeadline(), 8, 8, 32);
			var limits = new MixEnvelopeLimits(8, 8, 8, 1024, 1024, 8, 1024, 1024);

			var duplicateBacking = new TrackingStream(BuildRawMix(1,
				new[] { (Hash: 1U, Offset: 0U, Length: 1U) }, new byte[] { 1 }));
			var duplicateIndex = MixEnvelopeValidator.Validate(duplicateBacking, limits,
				PackageHashType.CRC32, RetailFilenameCatalog.V1, context);
			var duplicate = new ValidatedEmbeddedMapSource("one.map", 1, digest,
				duplicateIndex.OpenEntrySource(1, context));
			RetailArchiveGraphResult escaped = null;
			try
			{
				Assert.Throws<ArgumentException>(() => escaped = new RetailArchiveGraphResult(
					GroupsWithStructuralMaps(),
					ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure>.Empty,
					ImmutableArray.Create(duplicate, duplicate)));
			}
			finally
			{
				escaped?.Dispose();
				duplicateIndex.Dispose();
			}
			Assert.That(duplicateBacking.DisposeCalls, Is.EqualTo(1));

			var distinctBacking = new TrackingStream(BuildRawMix(2,
				new[]
				{
					(Hash: 1U, Offset: 0U, Length: 1U),
					(Hash: 2U, Offset: 1U, Length: 1U),
				}, new byte[] { 1, 1 }));
			var distinctIndex = MixEnvelopeValidator.Validate(distinctBacking, limits,
				PackageHashType.CRC32, RetailFilenameCatalog.V1, context);
			var first = new ValidatedEmbeddedMapSource("a.map", 1, digest,
				distinctIndex.OpenEntrySource(1, context));
			var second = new ValidatedEmbeddedMapSource("b.map", 1, digest,
				distinctIndex.OpenEntrySource(2, context));
			var result = new RetailArchiveGraphResult(GroupsWithStructuralMaps(),
				ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure>.Empty,
				ImmutableArray.Create(first, second));
			Assert.That(result.EmbeddedMaps, Has.Length.EqualTo(1));
			distinctIndex.Dispose();
			Assert.That(distinctBacking.DisposeCalls, Is.EqualTo(0));
			result.Dispose();
			Assert.That(distinctBacking.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void ResultIdentityGuardRequiresOneLinearReferenceSetReusedForRollback()
		{
			var testDirectory = Path.GetDirectoryName(CurrentSourceFile());
			var production = File.ReadAllText(Path.GetFullPath(Path.Combine(testDirectory,
				"..", "..", "OpenRA.Mods.RA2", "Content", "RetailArchiveGraphValidator.cs")));

			Assert.That(production, Does.Contain("HashSet<ValidatedEmbeddedMapSource>"));
			Assert.That(production, Does.Contain("MapSourceReferenceComparer.Instance"));
			Assert.That(production, Does.Contain("Distinct(MapSourceReferenceComparer.Instance)"));
			Assert.That(production, Does.Not.Contain("for (var j = i + 1;"));
		}

		[Test]
		public void NonAggregateEnvelopeFailureCannotUseTheFatalAggregateMapping()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var (_, language) = ValidBase(fixture);
			var throwing = new EnvelopeThrowingStream(1, PackageEnvelopeFailure.LimitExceeded);
			var batch = RetailArchiveInputBatch.TakeOwnership(ImmutableArray.Create(
				RetailOwnedArchiveInput.TakeOwnership(new RetailArchiveInputDescriptor(
					"ra2.mix", RetailContentGroup.Base, 1, new string('0', 64)), throwing),
				RetailOwnedArchiveInput.TakeOwnership(Descriptor("language.mix", RetailContentGroup.Base, language),
					new MemoryStream(language, writable: false))));
			var clock = new FakeClock();
			var operation = new RetailArchiveGraphValidator(Policy(), clock, RetailFilenameCatalog.V1)
				.ValidateOwned(batch, RetailValidationDeadline.Start(clock,
					IosImportEnvelopePolicyV1.WholeValidationTimeout), default);

			Assert.That(operation.Succeeded, Is.False);
			Assert.That(operation.Failure.Code, Is.EqualTo(RetailImportErrorCode.SanitizedIoFailure));
			Assert.That(operation.Failure.Diagnostic, Is.EqualTo(RetailImportDiagnosticCode.None));
			Assert.That(throwing.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void RootHashRejectsPrematureEofInsteadOfAcceptingADeclaredLongerArchive()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var (ra2, language) = ValidBase(fixture);
			var truncatedDigest = BitConverter.ToString(SHA256.HashData(ra2[..^1]))
				.Replace("-", string.Empty).ToLowerInvariant();
			var premature = new PrematureEofStream(ra2, ra2.Length - 1);
			var batch = RetailArchiveInputBatch.TakeOwnership(ImmutableArray.Create(
				RetailOwnedArchiveInput.TakeOwnership(new RetailArchiveInputDescriptor(
					"ra2.mix", RetailContentGroup.Base, ra2.LongLength, truncatedDigest), premature),
				RetailOwnedArchiveInput.TakeOwnership(Descriptor("language.mix", RetailContentGroup.Base, language),
					new MemoryStream(language, writable: false))));
			var clock = new FakeClock();
			var operation = new RetailArchiveGraphValidator(Policy(), clock, RetailFilenameCatalog.V1)
				.ValidateOwned(batch, RetailValidationDeadline.Start(clock,
					IosImportEnvelopePolicyV1.WholeValidationTimeout), default);

			Assert.That(operation.Succeeded, Is.False);
			Assert.That(operation.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceChanged));
			Assert.That(operation.Failure.Diagnostic, Is.EqualTo(RetailImportDiagnosticCode.LengthMismatch));
			Assert.That(premature.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void RootDigestMismatchIsFatalSourceChangedAndReleasesEveryRoot()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var (ra2, language) = ValidBase(fixture);
			var ra2Stream = new TrackingStream(ra2);
			var languageStream = new TrackingStream(language);
			var batch = RetailArchiveInputBatch.TakeOwnership(ImmutableArray.Create(
				RetailOwnedArchiveInput.TakeOwnership(new RetailArchiveInputDescriptor(
					"ra2.mix", RetailContentGroup.Base, ra2.LongLength, new string('0', 64)), ra2Stream),
				RetailOwnedArchiveInput.TakeOwnership(Descriptor("language.mix", RetailContentGroup.Base, language),
					languageStream)));
			var clock = new FakeClock();
			var operation = new RetailArchiveGraphValidator(Policy(), clock, RetailFilenameCatalog.V1)
				.ValidateOwned(batch, RetailValidationDeadline.Start(clock,
					IosImportEnvelopePolicyV1.WholeValidationTimeout), default);

			Assert.That(operation.Succeeded, Is.False);
			Assert.That(operation.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceChanged));
			Assert.That(operation.Failure.Diagnostic, Is.EqualTo(RetailImportDiagnosticCode.HashMismatch));
			Assert.That((ra2Stream.DisposeCalls, languageStream.DisposeCalls), Is.EqualTo((1, 1)));
		}

		[Test]
		public void EmbeddedMapHashRejectsPrematureEofAfterTheRootHashPassed()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var (_, language) = ValidBase(fixture);
			var map = Encoding.ASCII.GetBytes("truncated-map");
			var database = SyntheticRetailFixtureBuilder.BuildLocalMixDatabase("test.map");
			var ra2 = fixture.BuildNamedMix(PackageHashType.CRC32,
				("local mix database.dat", database), ("test.map", map));
			var mapOffset = checked(10L + 2 * 12L + database.LongLength);
			var premature = new PrematureEofStream(ra2,
				checked(mapOffset + map.LongLength - 1), armAfterRootRead: true);
			var batch = RetailArchiveInputBatch.TakeOwnership(ImmutableArray.Create(
				RetailOwnedArchiveInput.TakeOwnership(Descriptor("ra2.mix", RetailContentGroup.Base, ra2), premature),
				RetailOwnedArchiveInput.TakeOwnership(Descriptor("language.mix", RetailContentGroup.Base, language),
					new MemoryStream(language, writable: false))));
			var clock = new FakeClock();
			var operation = new RetailArchiveGraphValidator(Policy(), clock, RetailFilenameCatalog.V1)
				.ValidateOwned(batch, RetailValidationDeadline.Start(clock,
					IosImportEnvelopePolicyV1.WholeValidationTimeout), default);

			Assert.That(operation.Succeeded, Is.False);
			Assert.That(operation.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceChanged));
			Assert.That(operation.Failure.SafeBasename, Is.EqualTo("ra2.mix"));
			Assert.That(operation.Failure.Diagnostic, Is.EqualTo(RetailImportDiagnosticCode.LengthMismatch));
			Assert.That(premature.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void InvalidEmbeddedMapMetadataClosesTheTransferredSource()
		{
			var backing = new TrackingStream(BuildRawMix(1,
				new[] { (Hash: 1U, Offset: 0U, Length: 1U) }, new byte[] { 1 }));
			var context = new PackageReadContext(default, new NeverDeadline(), 8, 8, 32);
			using var index = MixEnvelopeValidator.Validate(backing,
				new MixEnvelopeLimits(8, 8, 8, 1024, 1024, 8, 1024, 1024),
				PackageHashType.CRC32, RetailFilenameCatalog.V1, context);
			var source = index.OpenEntrySource(1, context);

			Assert.Throws<ArgumentOutOfRangeException>(() => new ValidatedEmbeddedMapSource(
				"test.map", 2, new string('0', 64), source));
			index.Dispose();
			Assert.That(backing.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void EmbeddedMapSupportsConcurrentChildrenAndClosesBackingOnlyAfterFinalChild()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var bag = new byte[] { 1, 2, 3, 4 };
			var audio = fixture.BuildNamedMix(PackageHashType.CRC32,
				("audio.idx", BuildIdx("sound", 4, 6, 0)), ("audio.bag", bag));
			var language = fixture.BuildNamedMix(PackageHashType.CRC32, ("audio.mix", audio));
			var map = Encoding.ASCII.GetBytes("parallel-map");
			var ra2 = fixture.BuildNamedMix(PackageHashType.CRC32,
				("local mix database.dat", SyntheticRetailFixtureBuilder.BuildLocalMixDatabase("test.map")),
				("test.map", map));
			var ra2Stream = new TrackingStream(ra2);
			var languageStream = new TrackingStream(language);
			var batch = RetailArchiveInputBatch.TakeOwnership(ImmutableArray.Create(
				RetailOwnedArchiveInput.TakeOwnership(Descriptor("ra2.mix", RetailContentGroup.Base, ra2), ra2Stream),
				RetailOwnedArchiveInput.TakeOwnership(Descriptor("language.mix", RetailContentGroup.Base, language),
					languageStream)));
			var clock = new FakeClock();
			var operation = new RetailArchiveGraphValidator(Policy(), clock, RetailFilenameCatalog.V1)
				.ValidateOwned(batch, RetailValidationDeadline.Start(clock,
					IosImportEnvelopePolicyV1.WholeValidationTimeout), default);
			var result = Succeeded(operation);
			var handle = result.EmbeddedMaps.Single();
			var first = handle.OpenRead();
			var second = handle.OpenRead();
			var reads = new string[2];
			Parallel.Invoke(
				() => reads[0] = new StreamReader(first, Encoding.ASCII, false, 1024, leaveOpen: true).ReadToEnd(),
				() => reads[1] = new StreamReader(second, Encoding.ASCII, false, 1024, leaveOpen: true).ReadToEnd());
			Assert.That(reads, Is.All.EqualTo("parallel-map"));
			result.Dispose();
			Assert.That(ra2Stream.DisposeCalls, Is.EqualTo(0));
			Assert.Throws<ObjectDisposedException>(() => handle.OpenRead());
			first.Dispose();
			Assert.That(ra2Stream.DisposeCalls, Is.EqualTo(0));
			second.Dispose();
			Assert.That(ra2Stream.DisposeCalls, Is.EqualTo(1));
			Assert.That(languageStream.DisposeCalls, Is.EqualTo(1));
		}

		static RetailArchiveGraphResult Validate(byte[] ra2, byte[] language)
		{
			return Succeeded(ValidateOperation(new FakeClock(), Policy(), default,
				("ra2.mix", RetailContentGroup.Base, ra2),
				("language.mix", RetailContentGroup.Base, language)));
		}

		static string CurrentSourceFile([CallerFilePath] string path = null) => path;

		static RetailOperationResult<RetailArchiveGraphResult> ValidateOperation(IRetailMonotonicClock clock,
			PackageEnvelopePolicy policy, CancellationToken cancellationToken,
			params (string Name, RetailContentGroup Group, byte[] Bytes)[] roots)
		{
			var owned = roots.Select(root => RetailOwnedArchiveInput.TakeOwnership(
				Descriptor(root.Name, root.Group, root.Bytes), new MemoryStream(root.Bytes, writable: false)))
				.ToImmutableArray();
			var batch = RetailArchiveInputBatch.TakeOwnership(owned);
			var validator = new RetailArchiveGraphValidator(policy, clock, RetailFilenameCatalog.V1);
			return validator.ValidateOwned(batch,
				RetailValidationDeadline.Start(clock, IosImportEnvelopePolicyV1.WholeValidationTimeout), cancellationToken);
		}

		static RetailArchiveGraphResult Succeeded(RetailOperationResult<RetailArchiveGraphResult> operation)
		{
			Assert.That(operation.Succeeded, Is.True,
				operation.Succeeded ? null : $"{operation.Failure.Code}/{operation.Failure.Diagnostic}");
			return operation.Value;
		}

		static ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> GroupsWithStructuralMaps() =>
			Enum.GetValues<RetailContentGroup>().ToImmutableSortedDictionary(group => group,
				group => new RetailGroupStatus(group == RetailContentGroup.Maps ?
					RetailGroupValidation.Unavailable : RetailGroupValidation.Missing,
					RetailEditionRecognition.NotApplicable, RetailImportDiagnosticCode.None));

		static void AssertFailure(RetailArchiveGraphResult result, RetailContentGroup group,
			RetailImportErrorCode code, string safeName, RetailImportDiagnosticCode diagnostic)
		{
			Assert.That(result.Groups[group].Validation, Is.EqualTo(RetailGroupValidation.Invalid));
			Assert.That(result.Groups[group].Diagnostic, Is.EqualTo(diagnostic));
			Assert.That(result.GroupFailures[group], Is.EqualTo(
				new RetailImportFailure(code, group, safeName, diagnostic)));
		}

		static (byte[] Ra2, byte[] Language) ValidBase(SyntheticRetailFixtureBuilder fixture)
		{
			var bag = new byte[] { 1, 2, 3, 4 };
			var audio = fixture.BuildNamedMix(PackageHashType.CRC32,
				("audio.idx", BuildIdx("sound", (uint)bag.Length, 6, 0)), ("audio.bag", bag));
			return (OpaqueMix(fixture),
				fixture.BuildNamedMix(PackageHashType.CRC32, ("audio.mix", audio)));
		}

		static byte[] OpaqueMix(SyntheticRetailFixtureBuilder fixture) =>
			fixture.BuildNamedMix(PackageHashType.CRC32, ("opaque.bin", new byte[] { 1 }));

		static RetailArchiveInputDescriptor Descriptor(string name, RetailContentGroup group, byte[] bytes) =>
			new(name, group, bytes.LongLength,
				BitConverter.ToString(SHA256.HashData(bytes)).Replace("-", "").ToLowerInvariant());

		static PackageEnvelopePolicy Policy(int depth = IosImportEnvelopePolicyV1.MixDepth,
			int aggregateEntries = IosImportEnvelopePolicyV1.MixEntriesAggregate,
			long aggregateBytes = IosImportEnvelopePolicyV1.MixAddressedBytes) => new(
			new ZipEnvelopeLimits(IosImportEnvelopePolicyV1.PackagePathUtf8Bytes,
				IosImportEnvelopePolicyV1.PackagePathDepth, IosImportEnvelopePolicyV1.OramapEntries,
				IosImportEnvelopePolicyV1.OramapCompressedBytes, IosImportEnvelopePolicyV1.OramapExpandedEntryBytes,
				IosImportEnvelopePolicyV1.OramapExpandedAggregateBytes, IosImportEnvelopePolicyV1.OramapExpansionRatio),
			new PngReadLimits(IosImportEnvelopePolicyV1.MapPngEdge, IosImportEnvelopePolicyV1.MapPngPixels,
				IosImportEnvelopePolicyV1.MapPngDecodedBytes, IosImportEnvelopePolicyV1.MapPngChunks,
				IosImportEnvelopePolicyV1.MapPngIdatBytes, IosImportEnvelopePolicyV1.MapPngInflateRatio),
			new YamlEnvelopeLimits(IosImportEnvelopePolicyV1.MapYamlBytes, IosImportEnvelopePolicyV1.MapYamlNodes,
				IosImportEnvelopePolicyV1.MapYamlDepth, IosImportEnvelopePolicyV1.MapYamlLineBytes,
				IosImportEnvelopePolicyV1.MapYamlScalarBytes),
			new MixEnvelopeLimits(depth, IosImportEnvelopePolicyV1.MixEntriesPerArchive,
				aggregateEntries, IosImportEnvelopePolicyV1.MixIndexBytes,
				IosImportEnvelopePolicyV1.MixLocalDatabaseBytes, IosImportEnvelopePolicyV1.MixLocalDatabaseNames,
				IosImportEnvelopePolicyV1.MixSegmentBytes, aggregateBytes),
			new IdxBagEnvelopeLimits(IosImportEnvelopePolicyV1.IdxBytes, IosImportEnvelopePolicyV1.IdxSoundRecords,
				IosImportEnvelopePolicyV1.IdxNameBytes, IosImportEnvelopePolicyV1.BagBytes,
				IosImportEnvelopePolicyV1.BagSoundSpanBytes));

		static byte[] BuildIdx(string name, uint length, uint flags, uint chunk, uint offset = 0)
		{
			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
			{
				writer.Write(Encoding.ASCII.GetBytes("GABA"));
				writer.Write(2);
				writer.Write(1);
				var nameBytes = new byte[16];
				Encoding.ASCII.GetBytes(name).CopyTo(nameBytes, 0);
				writer.Write(nameBytes);
				writer.Write(offset);
				writer.Write(length);
				writer.Write(22_050U);
				writer.Write(flags);
				writer.Write(chunk);
			}

			return stream.ToArray();
		}

		static byte[] BuildRawMix(uint dataSize,
			(uint Hash, uint Offset, uint Length)[] entries, byte[] data)
		{
			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
			{
				writer.Write((ushort)0);
				writer.Write((ushort)0);
				writer.Write(checked((ushort)entries.Length));
				writer.Write(dataSize);
				foreach (var entry in entries)
				{
					writer.Write(entry.Hash);
					writer.Write(entry.Offset);
					writer.Write(entry.Length);
				}

				writer.Write(data);
			}

			return stream.ToArray();
		}

		sealed class FakeClock : IRetailMonotonicClock
		{
			long timestamp;
			public long GetTimestamp() => timestamp++;
			public TimeSpan GetElapsedTime(long start, long end) => TimeSpan.FromTicks(end - start);
		}

		sealed class ExpiringClock : IRetailMonotonicClock
		{
			long timestamp;
			public long GetTimestamp() => timestamp++;
			public TimeSpan GetElapsedTime(long start, long end) =>
				end == start ? TimeSpan.Zero : TimeSpan.FromDays(1);
		}

		sealed class NeverDeadline : IPackageReadDeadline
		{
			public void ThrowIfExpired() { }
		}

		sealed class NonValidatedMixLoader : IBoundedPackageLoader
		{
			public int BoundedCalls { get; private set; }
			public NonValidatedPackage Package { get; private set; }

			bool IPackageLoader.TryParsePackage(Stream stream, string filename,
				OpenRA.FileSystem.FileSystem context, out IReadOnlyPackage package) =>
				throw new AssertionException("The bounded graph must not call the trusted loader entry point.");

			bool IBoundedPackageLoader.TryParsePackage(Stream stream, string filename,
				OpenRA.FileSystem.FileSystem context, PackageReadContext readContext, out IReadOnlyPackage package)
			{
				BoundedCalls++;
				Package = new NonValidatedPackage(filename, stream);
				package = Package;
				return true;
			}
		}

		sealed class NonValidatedPackage : IReadOnlyPackage
		{
			Stream stream;
			public string Name { get; }
			public IEnumerable<string> Contents => Array.Empty<string>();
			public int RawAccessCalls { get; private set; }
			public int DisposeCalls { get; private set; }

			public NonValidatedPackage(string name, Stream ownedStream)
			{
				Name = name;
				stream = ownedStream;
			}

			public Stream GetStream(string filename)
			{
				RawAccessCalls++;
				return null;
			}

			public bool Contains(string filename) => false;

			public IReadOnlyPackage OpenPackage(string filename, OpenRA.FileSystem.FileSystem context)
			{
				RawAccessCalls++;
				return null;
			}

			public void Dispose()
			{
				var owned = Interlocked.Exchange(ref stream, null);
				if (owned != null)
				{
					DisposeCalls++;
					owned.Dispose();
				}
			}
		}

		sealed class TrackingStream : MemoryStream
		{
			public int DisposeCalls { get; private set; }
			public TrackingStream(byte[] bytes) : base(bytes, writable: false) { }

			protected override void Dispose(bool disposing)
			{
				if (disposing)
					DisposeCalls++;
				base.Dispose(disposing);
			}
		}

		sealed class EnvelopeThrowingStream : Stream
		{
			readonly long length;
			readonly PackageEnvelopeFailure failure;
			long position;
			bool disposed;

			public int DisposeCalls { get; private set; }
			public EnvelopeThrowingStream(long length, PackageEnvelopeFailure failure)
			{
				this.length = length;
				this.failure = failure;
			}
			public override bool CanRead => !disposed;
			public override bool CanSeek => !disposed;
			public override bool CanWrite => false;
			public override long Length => length;
			public override long Position
			{
				get => position;
				set => position = value >= 0 && value <= length ? value :
					throw new ArgumentOutOfRangeException(nameof(value));
			}
			public override int Read(byte[] buffer, int offset, int count) =>
				throw new PackageEnvelopeException(failure, "synthetic envelope failure");
			public override int Read(Span<byte> buffer) =>
				throw new PackageEnvelopeException(failure, "synthetic envelope failure");
			public override void Flush() { }
			public override long Seek(long offset, SeekOrigin origin)
			{
				Position = origin switch
				{
					SeekOrigin.Begin => offset,
					SeekOrigin.Current => checked(position + offset),
					SeekOrigin.End => checked(length + offset),
					_ => throw new ArgumentOutOfRangeException(nameof(origin)),
				};
				return position;
			}
			public override void SetLength(long value) => throw new NotSupportedException();
			public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					disposed = true;
					DisposeCalls++;
				}
				base.Dispose(disposing);
			}
		}

		sealed class PrematureEofStream : Stream
		{
			readonly byte[] bytes;
			readonly long readableLength;
			readonly bool armAfterRootRead;
			long position;
			bool armed;
			bool disposed;

			public int DisposeCalls { get; private set; }
			public PrematureEofStream(byte[] bytes, long readableLength, bool armAfterRootRead = false)
			{
				this.bytes = bytes;
				this.readableLength = readableLength;
				this.armAfterRootRead = armAfterRootRead;
				armed = !armAfterRootRead;
			}
			public override bool CanRead => !disposed;
			public override bool CanSeek => !disposed;
			public override bool CanWrite => false;
			public override long Length => bytes.LongLength;
			public override long Position
			{
				get => position;
				set
				{
					if (value < 0 || value > Length)
						throw new ArgumentOutOfRangeException(nameof(value));
					if (armAfterRootRead && !armed && position == Length && value == 0)
						armed = true;
					position = value;
				}
			}
			public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
			public override int Read(Span<byte> buffer)
			{
				if (disposed)
					throw new ObjectDisposedException(nameof(PrematureEofStream));
				var available = armed ? readableLength : Length;
				var count = (int)Math.Min(buffer.Length, Math.Max(0, available - position));
				bytes.AsSpan((int)position, count).CopyTo(buffer);
				position += count;
				return count;
			}
			public override void Flush() { }
			public override long Seek(long offset, SeekOrigin origin)
			{
				Position = origin switch
				{
					SeekOrigin.Begin => offset,
					SeekOrigin.Current => checked(position + offset),
					SeekOrigin.End => checked(Length + offset),
					_ => throw new ArgumentOutOfRangeException(nameof(origin)),
				};
				return position;
			}
			public override void SetLength(long value) => throw new NotSupportedException();
			public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					disposed = true;
					DisposeCalls++;
				}
				base.Dispose(disposing);
			}
		}
	}
}
