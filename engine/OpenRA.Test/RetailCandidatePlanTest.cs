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
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailCandidatePlanTest
	{
		static void AssertValueEqual<T>(T left, T right)
		{
			Assert.That(right, Is.EqualTo(left));
			Assert.That(right.GetHashCode(), Is.EqualTo(left.GetHashCode()));
			Assert.That(new HashSet<T> { left }.Contains(right), Is.True);
		}

		static void AssertRecord<T>()
		{
			Assert.That(typeof(T).GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null,
				typeof(T).Name + " must be a C# record class.");
		}

		static RetailSourceEntry Entry(int root, string path, RetailSourceKind kind = RetailSourceKind.DirectoryDiscoveredFile, long? length = 1)
		{
			return new RetailSourceEntry(new RetailSourceId(root, path), path.Split('/').Last(), kind, length, false);
		}

		static StagedRetailSource Staged(RetailPlannedCandidate candidate, string hash, long? length = null)
		{
			return new StagedRetailSource(candidate.SourceId, candidate.SafeBasename,
				"sources/" + candidate.SourceId.RootIndex + "/" + candidate.SourceId.CanonicalRelativePath,
				length ?? candidate.Length ?? 1, hash);
		}

		[Test]
		public void CandidatePlanningClassifiesCanonicalizesSortsAndAccounts()
		{
			var entries = ImmutableArray.Create(
				Entry(1, "maps/Zeta.MPR", length: null),
				Entry(0, "disc/LANGUAGE.MIX", length: 7),
				Entry(0, "disc/ra2.mix", length: 11),
				Entry(0, "disc/theme.mix", length: 13));

			var result = RetailCandidatePlan.Create(entries);

			Assert.That(result.Succeeded, Is.True);
			Assert.That(result.Value.Candidates.Select(c => c.SourceId.CanonicalRelativePath),
				Is.EqualTo(new[] { "disc/LANGUAGE.MIX", "disc/ra2.mix", "disc/theme.mix", "maps/Zeta.MPR" }));
			Assert.That(result.Value.Candidates.Select(c => c.SafeBasename),
				Is.EqualTo(new[] { "language.mix", "ra2.mix", "theme.mix", "Zeta.MPR" }));
			Assert.That(result.Value.Candidates.Select(c => c.Group),
				Is.EqualTo(new[] { RetailContentGroup.Base, RetailContentGroup.Base, RetailContentGroup.Ra2Music, RetailContentGroup.Maps }));
			Assert.That(result.Value.KnownSourceBytes, Is.EqualTo(31));
			Assert.That(result.Value.HasUnknownLengthSource, Is.True);
		}

		[Test]
		public void OnlyTheSixOwnedArchiveRootsReachTheArchiveGraphBoundary()
		{
			var result = RetailCandidatePlan.Create(ImmutableArray.Create(
				Entry(0, "RA2.MIX"), Entry(0, "language.mix"), Entry(0, "theme.mix"),
				Entry(0, "ra2md.mix"), Entry(0, "langmd.mix"), Entry(0, "thememd.mix"),
				Entry(0, "audio.mix"), Entry(0, "audio.idx"), Entry(0, "audio.bag")));

			Assert.That(result.Succeeded, Is.True);
			Assert.That(result.Value.Candidates.Select(candidate => candidate.SafeBasename), Is.EquivalentTo(new[]
			{
				"language.mix", "langmd.mix", "ra2.mix", "ra2md.mix", "theme.mix", "thememd.mix",
			}));
			foreach (var candidate in result.Value.Candidates)
				Assert.That(candidate.Group, Is.EqualTo(candidate.SafeBasename switch
				{
					"ra2.mix" or "language.mix" => RetailContentGroup.Base,
					"ra2md.mix" or "langmd.mix" => RetailContentGroup.Yuri,
					"theme.mix" => RetailContentGroup.Ra2Music,
					"thememd.mix" => RetailContentGroup.YuriMusic,
					_ => throw new AssertionException("Unexpected archive root."),
				}));
		}

		[Test]
		public void ExactRepeatedStableIdDedupesButConflictingMetadataFails()
		{
			var entry = Entry(0, "ra2.mix", RetailSourceKind.DirectFile, 12);
			var duplicate = RetailCandidatePlan.Create(ImmutableArray.Create(entry, entry));
			var conflict = RetailCandidatePlan.Create(ImmutableArray.Create(entry, Entry(0, "ra2.mix", RetailSourceKind.DirectFile, 13)));

			Assert.That(duplicate.Succeeded, Is.True);
			Assert.That(duplicate.Value.Candidates, Has.Length.EqualTo(1));
			Assert.That(conflict.Succeeded, Is.False);
			Assert.That(conflict.Failure.Code, Is.EqualTo(RetailImportErrorCode.DuplicateConflict));
		}

		[TestCase("setup.exe", RetailImportErrorCode.ExecutableSelection)]
		[TestCase("disc.iso", RetailImportErrorCode.ExtractInstallationFirst)]
		[TestCase("archive.zip", RetailImportErrorCode.ExtractInstallationFirst)]
		[TestCase("archive.7z", RetailImportErrorCode.ExtractInstallationFirst)]
		[TestCase("unknown.bin", RetailImportErrorCode.ExtractInstallationFirst)]
		public void UnsupportedDirectFilesReturnClosedDiagnostics(string name, RetailImportErrorCode code)
		{
			var result = RetailCandidatePlan.Create(ImmutableArray.Create(Entry(0, name, RetailSourceKind.DirectFile)));
			Assert.That(result.Succeeded, Is.False);
			Assert.That(result.Failure.Code, Is.EqualTo(code));
			Assert.That(result.Failure.SafeBasename, Is.EqualTo(name));
		}

		[Test]
		public void UnrelatedDirectoryFilesAndLooseAudioAreIgnored()
		{
			var result = RetailCandidatePlan.Create(ImmutableArray.Create(
				Entry(0, "notes.txt"), Entry(0, "audio.bag"), Entry(0, "audio.idx"), Entry(0, "map01.map")));
			Assert.That(result.Succeeded, Is.True);
			Assert.That(result.Value.Candidates.Select(c => c.SafeBasename), Is.EqualTo(new[] { "map01.map" }));
		}

		[Test]
		public void EmptyOrDefaultSelectionFailsClosed()
		{
			Assert.That(RetailCandidatePlan.Create(ImmutableArray<RetailSourceEntry>.Empty).Failure.Code,
				Is.EqualTo(RetailImportErrorCode.EmptySelection));
			Assert.Throws<ArgumentException>(() => RetailCandidatePlan.Create(default));
		}

		[Test]
		public void SourceAndAggregateLimitsStopBeforeLimitPlusOne()
		{
			var belowSource = RetailCandidatePlan.Create(ImmutableArray.Create(
				Entry(0, "below.map", length: IosImportEnvelopePolicyV1.SourceBytes - 1)));
			var exactSource = RetailCandidatePlan.Create(ImmutableArray.Create(
				Entry(0, "exact.map", length: IosImportEnvelopePolicyV1.SourceBytes)));
			var tooLarge = RetailCandidatePlan.Create(ImmutableArray.Create(Entry(0, "ra2.mix", length: IosImportEnvelopePolicyV1.SourceBytes + 1)));
			Assert.That(belowSource.Succeeded, Is.True);
			Assert.That(exactSource.Succeeded, Is.True);
			Assert.That(tooLarge.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceLimitExceeded));

			var exact = RetailCandidatePlan.Create(ImmutableArray.Create(
				Entry(0, "a.map", length: IosImportEnvelopePolicyV1.SourceBytes),
				Entry(1, "b.map", length: IosImportEnvelopePolicyV1.SourceBytes),
				Entry(2, "c.map", length: IosImportEnvelopePolicyV1.SourceBytes),
				Entry(3, "d.map", length: IosImportEnvelopePolicyV1.SourceBytes)));
			Assert.That(exact.Succeeded, Is.True);

			var aggregate = RetailCandidatePlan.Create(exact.Value.Candidates
				.Select(c => Entry(c.SourceId.RootIndex, c.SourceId.CanonicalRelativePath, c.Kind, c.Length)).Append(Entry(4, "e.map", length: 1)).ToImmutableArray());
			Assert.That(aggregate.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceLimitExceeded));
		}

		[Test]
		public void CandidateFileLimitAcceptsLimitAndRejectsLimitPlusOne()
		{
			var exactEntries = Enumerable.Range(0, IosImportEnvelopePolicyV1.CandidateFiles)
				.Select(index => Entry(index, "map" + index + ".map", length: 0)).ToImmutableArray();
			Assert.That(RetailCandidatePlan.Create(exactEntries).Succeeded, Is.True);

			var exceeded = exactEntries.Add(Entry(IosImportEnvelopePolicyV1.CandidateFiles, "overflow.map", length: 0));
			Assert.That(RetailCandidatePlan.Create(exceeded).Failure.Code,
				Is.EqualTo(RetailImportErrorCode.SourceLimitExceeded));
		}

		[Test]
		public void InvalidSourceEntriesAreRejectedBeforePlanning()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => new RetailSourceId(-1, "ra2.mix"));
			foreach (var path in new[] { "", "/ra2.mix", "../ra2.mix", "a//ra2.mix", "a/./ra2.mix", "a\\ra2.mix", "a\0ra2.mix" })
				Assert.Throws<ArgumentException>(() => new RetailSourceId(0, path), path);
			Assert.Throws<ArgumentOutOfRangeException>(() => Entry(0, "ra2.mix", length: -1));
			Assert.Throws<ArgumentException>(() => new RetailSourceEntry(new RetailSourceId(0, "folder/ra2.mix"), "language.mix", RetailSourceKind.DirectFile, 1, false));
		}

		[Test]
		public void PublicCandidateConstructionCannotForgeClassificationOrBasename()
		{
			Assert.Throws<ArgumentException>(() => new RetailPlannedCandidate(
				new RetailSourceId(0, "setup.exe"), "setup.exe", RetailSourceKind.DirectFile,
				RetailContentGroup.Base, 1));
			Assert.Throws<ArgumentException>(() => new RetailPlannedCandidate(
				new RetailSourceId(0, "ra2.mix"), "ra2.mix", RetailSourceKind.DirectFile,
				RetailContentGroup.Maps, 1));
			Assert.Throws<ArgumentException>(() => new RetailPlannedCandidate(
				new RetailSourceId(0, "folder/RA2.MIX"), "language.mix", RetailSourceKind.DirectFile,
				RetailContentGroup.Base, 1));

			var archive = new RetailPlannedCandidate(new RetailSourceId(0, "folder/RA2.MIX"), "ra2.mix",
				RetailSourceKind.DirectoryDiscoveredFile, RetailContentGroup.Base, 1);
			var map = new RetailPlannedCandidate(new RetailSourceId(0, "maps/地图.map"), "地图.map",
				RetailSourceKind.DirectoryDiscoveredFile, RetailContentGroup.Maps, 1);
			Assert.That(archive.SafeBasename, Is.EqualTo("ra2.mix"));
			Assert.That(map.SafeBasename, Is.EqualTo("地图.map"));
		}

		[Test]
		public void CandidateProvenanceCanOnlyBeCreatedByTheValidatedFactory()
		{
			Assert.That(typeof(RetailPlannedCandidate).GetConstructors(), Is.Empty);
			Assert.That(typeof(RetailCandidatePlan).GetConstructors(), Is.Empty);
			Assert.Throws<ArgumentException>(() => new RetailCandidatePlan(
				ImmutableArray<RetailPlannedCandidate>.Empty, 0, false));

			var linked = new RetailSourceEntry(new RetailSourceId(0, "ra2.mix"), "ra2.mix",
				RetailSourceKind.DirectFile, 1, true);
			var rejected = RetailCandidatePlan.Create(ImmutableArray.Create(linked));
			Assert.That(rejected.Succeeded, Is.False);
			Assert.That(rejected.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourcePermissionDenied));

			var created = RetailCandidatePlan.Create(ImmutableArray.Create(Entry(0, "ra2.mix", length: 1)));
			Assert.That(created.Succeeded, Is.True);
			var staged = RetailStagedPlan.Seal(created.Value,
				ImmutableArray.Create(Staged(created.Value.Candidates[0], new string('a', 64))));
			Assert.That(staged.Succeeded, Is.True);
		}

		[Test]
		public void SymbolicLinksAreExcluded()
		{
			var linked = new RetailSourceEntry(new RetailSourceId(0, "ra2.mix"), "ra2.mix", RetailSourceKind.DirectFile, 1, true);
			var result = RetailCandidatePlan.Create(ImmutableArray.Create(linked));
			Assert.That(result.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourcePermissionDenied));
		}

		[Test]
		public void StagingTargetAcceptsOnlyStructuralCanonicalPaths()
		{
			Assert.That(new RetailStagingTarget("sources/0/ra2.mix").CanonicalRelativePath, Is.EqualTo("sources/0/ra2.mix"));
			foreach (var path in new[] { "", ".", "..", "/absolute", "a//b", "a/./b", "a/../b", "a\\b", "a\0b" })
				Assert.Throws<ArgumentException>(() => new RetailStagingTarget(path), path);
		}

		[Test]
		public void SealRequiresExactCoverageCanonicalHashesAndCanonicalTargets()
		{
			var plan = RetailCandidatePlan.Create(ImmutableArray.Create(Entry(0, "ra2.mix", length: 5))).Value;
			var candidate = plan.Candidates[0];
			var hash = new string('a', 64);

			Assert.That(RetailStagedPlan.Seal(plan, ImmutableArray<StagedRetailSource>.Empty).Failure.Code,
				Is.EqualTo(RetailImportErrorCode.SourceChanged));
			Assert.Throws<ArgumentException>(() => Staged(candidate, hash.ToUpperInvariant()));
			Assert.Throws<ArgumentException>(() => new StagedRetailSource(candidate.SourceId, candidate.SafeBasename, "bad//path", 5, hash));
			Assert.That(RetailStagedPlan.Seal(plan, ImmutableArray.Create(Staged(candidate, hash))).Succeeded, Is.True);
		}

		[Test]
		public void StagedPlanCannotBeForgedThroughPublicConstruction()
		{
			var plan = RetailCandidatePlan.Create(ImmutableArray.Create(Entry(0, "ra2.mix", length: 5))).Value;
			Assert.That(typeof(RetailStagedPlan).GetConstructors(), Is.Empty);
			Assert.Throws<MissingMethodException>(() => Activator.CreateInstance(
				typeof(RetailStagedPlan), plan, ImmutableArray<StagedRetailSource>.Empty, 0L));
		}

		[Test]
		public void CandidateAndStagedDtosHaveRecordValueSemantics()
		{
			AssertRecord<RetailSourceEntry>();
			AssertRecord<RetailPlannedCandidate>();
			AssertRecord<RetailCandidatePlan>();
			AssertRecord<StagedRetailSource>();
			AssertRecord<RetailStagedPlan>();

			var source = Entry(0, "maps/map01.map", RetailSourceKind.DirectoryDiscoveredFile, 5);
			AssertValueEqual(source, Entry(0, "maps/map01.map", RetailSourceKind.DirectoryDiscoveredFile, 5));
			Assert.That(new[]
			{
				Entry(1, "maps/map01.map", RetailSourceKind.DirectoryDiscoveredFile, 5),
				Entry(0, "maps/map02.map", RetailSourceKind.DirectoryDiscoveredFile, 5),
				Entry(0, "maps/map01.map", RetailSourceKind.DirectFile, 5),
				Entry(0, "maps/map01.map", RetailSourceKind.DirectoryDiscoveredFile, 6),
				new RetailSourceEntry(source.Id, source.Basename, source.Kind, source.Length, true),
			}, Has.All.Not.EqualTo(source));

			var planned = new RetailPlannedCandidate(source.Id, source.Basename, source.Kind,
				RetailContentGroup.Maps, source.Length);
			AssertValueEqual(planned, new RetailPlannedCandidate(source.Id, source.Basename, source.Kind,
				RetailContentGroup.Maps, source.Length));
			Assert.That(new[]
			{
				new RetailPlannedCandidate(new RetailSourceId(1, source.Id.CanonicalRelativePath),
					source.Basename, source.Kind, RetailContentGroup.Maps, source.Length),
				new RetailPlannedCandidate(new RetailSourceId(0, "maps/map02.map"),
					"map02.map", source.Kind, RetailContentGroup.Maps, source.Length),
				new RetailPlannedCandidate(source.Id, source.Basename, RetailSourceKind.DirectFile,
					RetailContentGroup.Maps, source.Length),
				new RetailPlannedCandidate(source.Id, source.Basename, source.Kind,
					RetailContentGroup.Maps, 6),
			}, Has.All.Not.EqualTo(planned));

			var plan = RetailCandidatePlan.Create(ImmutableArray.Create(source)).Value;
			var equalPlan = RetailCandidatePlan.Create(ImmutableArray.Create(
				Entry(0, "maps/map01.map", RetailSourceKind.DirectoryDiscoveredFile, 5))).Value;
			AssertValueEqual(plan, equalPlan);

			var hash = new string('a', 64);
			var staged = Staged(plan.Candidates[0], hash);
			var equalStaged = Staged(equalPlan.Candidates[0], hash);
			AssertValueEqual(staged, equalStaged);
			Assert.That(new[]
			{
				new StagedRetailSource(new RetailSourceId(1, source.Id.CanonicalRelativePath),
					source.Basename, "sources/1/maps/map01.map", 5, hash),
				new StagedRetailSource(source.Id, "other.map", "sources/0/maps/map01.map", 5, hash),
				new StagedRetailSource(source.Id, source.Basename, "other/0/maps/map01.map", 5, hash),
				new StagedRetailSource(source.Id, source.Basename, "sources/0/maps/map01.map", 6, hash),
				new StagedRetailSource(source.Id, source.Basename, "sources/0/maps/map01.map", 5,
					new string('b', 64)),
			}, Has.All.Not.EqualTo(staged));

			var sealedPlan = RetailStagedPlan.Seal(plan, ImmutableArray.Create(staged)).Value;
			var equalSealedPlan = RetailStagedPlan.Seal(equalPlan, ImmutableArray.Create(equalStaged)).Value;
			AssertValueEqual(sealedPlan, equalSealedPlan);
		}

		[Test]
		public void SealDedupesIdenticalArchivesAndMapsButRejectsArchiveConflicts()
		{
			var hashA = new string('a', 64);
			var hashB = new string('b', 64);
			var archivePlan = RetailCandidatePlan.Create(ImmutableArray.Create(Entry(0, "ra2.mix", length: 5), Entry(1, "RA2.MIX", length: 5))).Value;
			var archiveSame = RetailStagedPlan.Seal(archivePlan, archivePlan.Candidates.Select(c => Staged(c, hashA)).ToImmutableArray());
			var archiveConflict = RetailStagedPlan.Seal(archivePlan, ImmutableArray.Create(Staged(archivePlan.Candidates[0], hashA), Staged(archivePlan.Candidates[1], hashB)));

			Assert.That(archiveSame.Succeeded, Is.True);
			Assert.That(archiveSame.Value.Sources, Has.Length.EqualTo(1));
			Assert.That(archiveConflict.Failure.Code, Is.EqualTo(RetailImportErrorCode.DuplicateConflict));

			var mapPlan = RetailCandidatePlan.Create(ImmutableArray.Create(Entry(0, "map01.map", length: 5), Entry(1, "map01.map", length: 5))).Value;
			var exactMapDuplicate = RetailStagedPlan.Seal(mapPlan, mapPlan.Candidates.Select(c => Staged(c, hashA)).ToImmutableArray());
			var distinctMaps = RetailStagedPlan.Seal(mapPlan, ImmutableArray.Create(Staged(mapPlan.Candidates[0], hashA), Staged(mapPlan.Candidates[1], hashB)));
			Assert.That(exactMapDuplicate.Value.Sources, Has.Length.EqualTo(1));
			Assert.That(distinctMaps.Succeeded, Is.True);
			Assert.That(distinctMaps.Value.Sources, Has.Length.EqualTo(2));
		}
	}
}
