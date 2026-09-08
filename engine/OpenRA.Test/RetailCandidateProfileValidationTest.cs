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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenRA.FileSystem;
using OpenRA.Mods.Cnc.FileSystem;
using OpenRA.Mods.RA2.Content;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailCandidateProfileValidationTestClockIdentity
	{
		[Test]
		public async Task DiscoveryRejectsAnotherClocksDeadlineBeforeProviderRead()
		{
			var sourceClock = new FakeClock();
			var suppliedClock = new FakeClock();
			var deadline = RetailValidationDeadline.Start(
				sourceClock, IosImportEnvelopePolicyV1.DiscoveryTimeout);
			var session = new CountingSession();

			var result = await RetailSourceDiscovery.DiscoverAsync(
				session, suppliedClock, deadline, CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.False);
				AssertClockMismatch(result.Failure);
				Assert.That(session.OpenCalls, Is.Zero);
				Assert.That(session.Enumeration.ReadCalls, Is.Zero);
			});
		}

		[Test]
		public void ArchiveGraphRejectsAnotherClocksDeadlineBeforeReadAndClosesOwnership()
		{
			var bytes = new byte[] { 1 };
			var stream = new TrackingStream(bytes);
			var digest = BitConverter.ToString(SHA256.HashData(bytes))
				.Replace("-", string.Empty).ToLowerInvariant();
			var descriptor = new RetailArchiveInputDescriptor(
				"ra2.mix", RetailContentGroup.Base, bytes.LongLength, digest);
			var input = RetailOwnedArchiveInput.TakeOwnership(descriptor, stream);
			var batch = RetailArchiveInputBatch.TakeOwnership(ImmutableArray.Create(input));
			var sourceClock = new FakeClock();
			var suppliedClock = new FakeClock();
			var deadline = RetailValidationDeadline.Start(
				sourceClock, IosImportEnvelopePolicyV1.WholeValidationTimeout);
			var validator = new RetailArchiveGraphValidator(
				RetailArchiveGraphValidator.IosV1Policy,
				suppliedClock,
				RetailFilenameCatalog.V1);

			var result = validator.ValidateOwned(batch, deadline, CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.False);
				AssertClockMismatch(result.Failure);
				Assert.That(stream.ReadCalls, Is.Zero);
				Assert.That(stream.DisposeCalls, Is.EqualTo(1));
			});

			batch.Dispose();
			Assert.That(stream.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void ArchiveClockMismatchSuppressesCleanupFailureAndClosesEveryInput()
		{
			var bytes = new byte[] { 1 };
			var digest = BitConverter.ToString(SHA256.HashData(bytes))
				.Replace("-", string.Empty).ToLowerInvariant();
			var firstStream = new TrackingStream(bytes, throwOnDispose: true);
			var secondStream = new TrackingStream(bytes);
			var first = RetailOwnedArchiveInput.TakeOwnership(
				new RetailArchiveInputDescriptor(
					"language.mix", RetailContentGroup.Base, bytes.LongLength, digest),
				firstStream);
			var second = RetailOwnedArchiveInput.TakeOwnership(
				new RetailArchiveInputDescriptor(
					"ra2.mix", RetailContentGroup.Base, bytes.LongLength, digest),
				secondStream);
			var batch = RetailArchiveInputBatch.TakeOwnership(ImmutableArray.Create(first, second));
			var sourceClock = new FakeClock();
			var suppliedClock = new FakeClock();
			var deadline = RetailValidationDeadline.Start(
				sourceClock, IosImportEnvelopePolicyV1.WholeValidationTimeout);
			var validator = new RetailArchiveGraphValidator(
				RetailArchiveGraphValidator.IosV1Policy,
				suppliedClock,
				RetailFilenameCatalog.V1);
			RetailOperationResult<RetailArchiveGraphResult> result = null;

			try
			{
				Assert.DoesNotThrow(() =>
					result = validator.ValidateOwned(batch, deadline, CancellationToken.None));
				Assert.Multiple(() =>
				{
					Assert.That(result.Succeeded, Is.False);
					AssertClockMismatch(result.Failure);
					Assert.That(firstStream.ReadCalls, Is.Zero);
					Assert.That(secondStream.ReadCalls, Is.Zero);
					Assert.That(firstStream.DisposeCalls, Is.EqualTo(1));
					Assert.That(secondStream.DisposeCalls, Is.EqualTo(1));
				});
			}
			finally
			{
				second.Dispose();
				batch.Dispose();
			}
		}

		[TestCase(false)]
		[TestCase(true)]
		public void BatchRollbackPreservesDuplicateArgumentFailureAndClosesEveryInput(
			bool duplicateObject)
		{
			var bytes = new byte[] { 1 };
			var firstStream = new TrackingStream(bytes, throwOnDispose: true);
			var duplicateStream = new TrackingStream(bytes);
			var laterStream = new TrackingStream(bytes);
			var first = OwnedInput("ra2.mix", RetailContentGroup.Base, bytes, firstStream);
			var duplicate = duplicateObject ? first :
				OwnedInput("ra2.mix", RetailContentGroup.Base, bytes, duplicateStream);
			var later = OwnedInput("theme.mix", RetailContentGroup.Ra2Music, bytes, laterStream);

			var error = Assert.Throws<ArgumentException>(() =>
				RetailArchiveInputBatch.TakeOwnership(ImmutableArray.Create(first, duplicate, later)));

			Assert.Multiple(() =>
			{
				Assert.That(error.ParamName, Is.EqualTo("ownedInputs"));
				Assert.That(firstStream.DisposeCalls, Is.EqualTo(1));
				if (!duplicateObject)
					Assert.That(duplicateStream.DisposeCalls, Is.EqualTo(1));
				Assert.That(laterStream.DisposeCalls, Is.EqualTo(1));
			});
		}

		[Test]
		public void EarlyFatalGraphFailureSuppressesHostileCleanupAndReleasesLaterRootsAndMaps()
		{
			using var fixture = new SyntheticRetailFixtureBuilder();
			var (_, languagePath) = fixture.WriteValidBaseArchives();
			var language = File.ReadAllBytes(languagePath);
			var map = Encoding.ASCII.GetBytes("retained-map");
			var ra2 = fixture.BuildNamedMix(
				PackageHashType.CRC32,
				("local mix database.dat",
					SyntheticRetailFixtureBuilder.BuildLocalMixDatabase("test.map")),
				("test.map", map));
			var opaque = fixture.BuildNamedMix(
				PackageHashType.CRC32,
				("opaque.bin", new byte[] { 1 }));
			var ra2Stream = new TrackingStream(ra2);
			var languageStream = new TrackingStream(language);
			var themeStream = new TrackingStream(opaque, throwOnDispose: true);
			var ra2mdStream = new TrackingStream(opaque);
			var langmdStream = new TrackingStream(opaque);
			var batch = RetailArchiveInputBatch.TakeOwnership(ImmutableArray.Create(
				OwnedInput("ra2.mix", RetailContentGroup.Base, ra2, ra2Stream),
				OwnedInput("language.mix", RetailContentGroup.Base, language, languageStream,
					new string('0', 64)),
				OwnedInput("theme.mix", RetailContentGroup.Ra2Music, opaque, themeStream),
				OwnedInput("ra2md.mix", RetailContentGroup.Yuri, opaque, ra2mdStream),
				OwnedInput("langmd.mix", RetailContentGroup.Yuri, opaque, langmdStream)));
			var clock = new FakeClock();
			RetailOperationResult<RetailArchiveGraphResult> operation = null;

			Assert.DoesNotThrow(() => operation = new RetailArchiveGraphValidator(
				RetailArchiveGraphValidator.IosV1Policy,
				clock,
				RetailFilenameCatalog.V1).ValidateOwned(
					batch,
					RetailValidationDeadline.Start(
						clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
					CancellationToken.None));

			Assert.Multiple(() =>
			{
				Assert.That(operation.Succeeded, Is.False);
				Assert.That(operation.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceChanged));
				Assert.That(operation.Failure.SafeBasename, Is.EqualTo("language.mix"));
				Assert.That(operation.Failure.Diagnostic,
					Is.EqualTo(RetailImportDiagnosticCode.HashMismatch));
				Assert.That(ra2Stream.DisposeCalls, Is.EqualTo(1));
				Assert.That(languageStream.DisposeCalls, Is.EqualTo(1));
				Assert.That(themeStream.DisposeCalls, Is.EqualTo(1));
				Assert.That(ra2mdStream.DisposeCalls, Is.EqualTo(1));
				Assert.That(langmdStream.DisposeCalls, Is.EqualTo(1));
			});
		}

		static RetailOwnedArchiveInput OwnedInput(
			string name,
			RetailContentGroup group,
			byte[] bytes,
			TrackingStream stream,
			string digest = null)
		{
			digest ??= Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
			return RetailOwnedArchiveInput.TakeOwnership(
				new RetailArchiveInputDescriptor(name, group, bytes.LongLength, digest),
				stream);
		}

		static void AssertClockMismatch(RetailImportFailure failure)
		{
			Assert.That(failure.Code, Is.EqualTo(RetailImportErrorCode.ProfileContextFailure));
			Assert.That(failure.Group, Is.Null);
			Assert.That(failure.SafeBasename, Is.Null);
			Assert.That(failure.Diagnostic,
				Is.EqualTo(RetailImportDiagnosticCode.ProfileMetadataMismatch));
		}

		sealed class FakeClock : IRetailMonotonicClock
		{
			public long GetTimestamp() => 0;
			public TimeSpan GetElapsedTime(long start, long end) => TimeSpan.Zero;
		}

		sealed class CountingSession : IRetailImportSourceSession
		{
			public int OpenCalls { get; private set; }
			public CountingEnumeration Enumeration { get; } = new();

			public Task<RetailOperationResult<IRetailSourceEnumeration>> OpenEnumerationAsync(
				RetailDiscoveryBudget budget,
				CancellationToken cancellationToken)
			{
				OpenCalls++;
				return Task.FromResult(
					RetailOperationResult<IRetailSourceEnumeration>.Success(Enumeration));
			}

			public Task<RetailOperationResult<StagedRetailSource>> CopyToAsync(
				RetailSourceId id,
				RetailStagingTarget target,
				IProgress<RetailImportProgress> progress,
				CancellationToken cancellationToken) =>
				throw new AssertionException("Clock mismatch must fail before copy.");

			public ValueTask DisposeAsync() => default;
		}

		sealed class CountingEnumeration : IRetailSourceEnumeration
		{
			public int ReadCalls { get; private set; }

			public ValueTask<RetailOperationResult<RetailSourceRead>> ReadNextAsync(
				CancellationToken cancellationToken)
			{
				ReadCalls++;
				return new ValueTask<RetailOperationResult<RetailSourceRead>>(
					RetailOperationResult<RetailSourceRead>.Success(RetailSourceRead.Complete()));
			}

			public ValueTask DisposeAsync() => default;
		}

		sealed class TrackingStream : MemoryStream
		{
			readonly bool throwOnDispose;
			public int ReadCalls { get; private set; }
			public int DisposeCalls { get; private set; }

			public TrackingStream(byte[] bytes, bool throwOnDispose = false)
				: base(bytes, writable: false)
			{
				this.throwOnDispose = throwOnDispose;
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				ReadCalls++;
				return base.Read(buffer, offset, count);
			}

			public override int Read(Span<byte> buffer)
			{
				ReadCalls++;
				return base.Read(buffer);
			}

			public override int ReadByte()
			{
				ReadCalls++;
				return base.ReadByte();
			}

			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					DisposeCalls++;
					if (throwOnDispose)
						throw new IOException("Injected cleanup failure.");
				}

				base.Dispose(disposing);
			}
		}
	}

	[TestFixture]
	public sealed class RetailCandidateProfileValidationTest
	{
		[Test]
		public void RuntimeProfilesMapOnlyToTheTwoFixedManifestEntries()
		{
			Assert.Multiple(() =>
			{
				Assert.That(Ra2RuntimeProfileId.Base.ManifestSelection(),
					Is.EqualTo(new TrustedManifestSelection("ra2", "mod.ios-base.yaml")));
				Assert.That(Ra2RuntimeProfileId.Yuri.ManifestSelection(),
					Is.EqualTo(new TrustedManifestSelection("ra2", "mod.ios-yuri.yaml")));
				Assert.Throws<ArgumentOutOfRangeException>(() =>
					((Ra2RuntimeProfileId)0).ManifestSelection());
			});
		}

		[Test]
		public void MountContractsHaveExactRequiredOrderAndNeverMountMusic()
		{
			using var graph = Graph(
				(RetailContentGroup.Base, RetailGroupValidation.Ready),
				(RetailContentGroup.Yuri, RetailGroupValidation.Ready));
			var baseResult = Ra2RetailMountContract.Create(Ra2RuntimeProfileId.Base, graph);
			var yuriResult = Ra2RetailMountContract.Create(Ra2RuntimeProfileId.Yuri, graph);

			Assert.Multiple(() =>
			{
				Assert.That(baseResult.Succeeded, Is.True);
				Assert.That(baseResult.Value.Mounts, Has.Length.EqualTo(2));
				Assert.That(MountShape(baseResult.Value), Is.EqualTo(new[]
				{
					"Base:ra2.mix:ra2.mix:True",
					"Base:language.mix:language.mix:True",
				}));
				Assert.That(yuriResult.Succeeded, Is.True);
				Assert.That(MountShape(yuriResult.Value), Is.EqualTo(new[]
				{
					"Base:ra2.mix:ra2.mix:True",
					"Base:language.mix:language.mix:True",
					"Yuri:ra2md.mix:ra2md.mix:True",
					"Yuri:langmd.mix:langmd.mix:True",
				}));
			});
		}

		[Test]
		public void MissingRequiredYuriPairReturnsClosedMountFailure()
		{
			using var graph = Graph(
				(RetailContentGroup.Base, RetailGroupValidation.Ready),
				(RetailContentGroup.Yuri, RetailGroupValidation.Missing));
			var result = Ra2RetailMountContract.Create(Ra2RuntimeProfileId.Yuri, graph);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.False);
				Assert.That(result.Failure.Code, Is.EqualTo(RetailImportErrorCode.RequiredMountFailure));
				Assert.That(result.Failure.Group, Is.EqualTo(RetailContentGroup.Yuri));
				Assert.That(result.Failure.SafeBasename, Is.EqualTo("ra2md.mix"));
				Assert.That(result.Failure.Diagnostic,
					Is.EqualTo(RetailImportDiagnosticCode.MissingRequiredArchive));
			});
		}

		[Test]
		public void ProfileValidatorSelectsYuriOnlyWithAValidatedYuriCompatibleMap()
		{
			using var graph = Graph(
				(RetailContentGroup.Base, RetailGroupValidation.Ready),
				(RetailContentGroup.Yuri, RetailGroupValidation.Ready));
			var map = RetailMapProfileEvidence.FromValidatedMap(
				new string('a', 64), Ra2MapCompatibleProfiles.Base | Ra2MapCompatibleProfiles.Yuri);
			var clock = new FakeClock();
			var result = new RetailProfileValidator(clock).Validate(
				graph,
				ImmutableArray.Create(map),
				RetailValidationDeadline.Start(clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.True);
				Assert.That(result.Value.SelectedProfile, Is.EqualTo(Ra2RuntimeProfileId.Yuri));
				Assert.That(result.Value.Maps, Has.Length.EqualTo(1));
				Assert.That(result.Value.Groups[RetailContentGroup.Base].Recognition,
					Is.EqualTo(RetailEditionRecognition.CompatibleUnrecognized));
				Assert.That(result.Value.Groups[RetailContentGroup.Yuri].Recognition,
					Is.EqualTo(RetailEditionRecognition.CompatibleUnrecognized));
				Assert.That(result.Value.Groups.Values,
					Has.None.Property(nameof(RetailGroupStatus.Recognition))
					.EqualTo(RetailEditionRecognition.Recognized));
			});
		}

		[Test]
		public void CorruptOptionalYuriIsQuarantinedWithoutBlockingBase()
		{
			var failure = new RetailImportFailure(
				RetailImportErrorCode.PartialYuri,
				RetailContentGroup.Yuri,
				"ra2md.mix",
				RetailImportDiagnosticCode.MissingRequiredArchive);
			using var graph = GraphWithFailure(failure);
			var map = RetailMapProfileEvidence.FromValidatedMap(
				new string('b', 64), Ra2MapCompatibleProfiles.Base);
			var clock = new FakeClock();
			var result = new RetailProfileValidator(clock).Validate(
				graph,
				ImmutableArray.Create(map),
				RetailValidationDeadline.Start(clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.True);
				Assert.That(result.Value.SelectedProfile, Is.EqualTo(Ra2RuntimeProfileId.Base));
				Assert.That(result.Value.GroupFailures.Keys,
					Is.EqualTo(new[] { RetailContentGroup.Yuri }));
				Assert.That(result.Value.GroupFailures[RetailContentGroup.Yuri], Is.SameAs(failure));
			});
		}

		[Test]
		public void EveryOptionalInvalidGroupKeepsItsExactFailureInEnumOrder()
		{
			var ra2Music = new RetailImportFailure(
				RetailImportErrorCode.CorruptArchive,
				RetailContentGroup.Ra2Music,
				"theme.mix",
				RetailImportDiagnosticCode.MalformedEnvelope);
			var yuri = new RetailImportFailure(
				RetailImportErrorCode.PartialYuri,
				RetailContentGroup.Yuri,
				"ra2md.mix",
				RetailImportDiagnosticCode.MissingRequiredArchive);
			var yuriMusic = new RetailImportFailure(
				RetailImportErrorCode.CorruptArchive,
				RetailContentGroup.YuriMusic,
				"thememd.mix",
				RetailImportDiagnosticCode.ArithmeticOverflow);
			using var graph = GraphWithFailures(ra2Music, yuri, yuriMusic);
			var map = RetailMapProfileEvidence.FromValidatedMap(
				new string('d', 64), Ra2MapCompatibleProfiles.Base);
			var clock = new FakeClock();

			var result = new RetailProfileValidator(clock).Validate(
				graph,
				ImmutableArray.Create(map),
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.That(result.Succeeded, Is.True);
			Assert.Multiple(() =>
			{
				Assert.That(result.Value.GroupFailures.Keys, Is.EqualTo(new[]
				{
					RetailContentGroup.Ra2Music,
					RetailContentGroup.Yuri,
					RetailContentGroup.YuriMusic,
				}));
				Assert.That(result.Value.GroupFailures[RetailContentGroup.Ra2Music],
					Is.SameAs(ra2Music));
				Assert.That(result.Value.GroupFailures[RetailContentGroup.Yuri],
					Is.SameAs(yuri));
				Assert.That(result.Value.GroupFailures[RetailContentGroup.YuriMusic],
					Is.SameAs(yuriMusic));
				Assert.That(result.Value.Groups, Has.Count.EqualTo(5));
			});
		}

		[Test]
		public void ValidatorRejectsForeignClockBeforeWalkingGroupsOrMaps()
		{
			using var graph = Graph(
				(RetailContentGroup.Base, RetailGroupValidation.Ready),
				(RetailContentGroup.Yuri, RetailGroupValidation.Missing));
			var sourceClock = new FakeClock();
			var suppliedClock = new FakeClock();
			var result = new RetailProfileValidator(suppliedClock).Validate(
				graph,
				ImmutableArray<RetailMapProfileEvidence>.Empty,
				RetailValidationDeadline.Start(
					sourceClock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.False);
				Assert.That(result.Failure.Code,
					Is.EqualTo(RetailImportErrorCode.ProfileContextFailure));
				Assert.That(result.Failure.Group, Is.Null);
				Assert.That(result.Failure.SafeBasename, Is.Null);
				Assert.That(result.Failure.Diagnostic,
					Is.EqualTo(RetailImportDiagnosticCode.ProfileMetadataMismatch));
			});
		}

		[Test]
		public void ValidatorChecksDeadlineAtEveryRequiredMountBeforeMapSelection()
		{
			using var graph = Graph(
				(RetailContentGroup.Base, RetailGroupValidation.Ready),
				(RetailContentGroup.Yuri, RetailGroupValidation.Missing));
			var clock = new CheckpointClock(expireAfterElapsedReads: 6);
			var deadline = RetailValidationDeadline.Start(
				clock, IosImportEnvelopePolicyV1.WholeValidationTimeout);

			var result = new RetailProfileValidator(clock).Validate(
				graph,
				ImmutableArray<RetailMapProfileEvidence>.Empty,
				deadline,
				CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.False);
				Assert.That(result.Failure.Code,
					Is.EqualTo(RetailImportErrorCode.ValidationTimedOut));
				Assert.That(result.Failure.Group, Is.Null);
				Assert.That(result.Failure.SafeBasename, Is.Null);
				Assert.That(result.Failure.Diagnostic,
					Is.EqualTo(RetailImportDiagnosticCode.DeadlineExceeded));
			});
		}

		[Test]
		public void ValidatorChecksYuriMountDeadlineAfterCompatibleMapEvidence()
		{
			using var graph = Graph(
				(RetailContentGroup.Base, RetailGroupValidation.Ready),
				(RetailContentGroup.Yuri, RetailGroupValidation.Ready));
			var map = RetailMapProfileEvidence.FromValidatedMap(
				new string('e', 64), Ra2MapCompatibleProfiles.Base | Ra2MapCompatibleProfiles.Yuri);
			var clock = new CheckpointClock(expireAfterElapsedReads: 10);
			var deadline = RetailValidationDeadline.Start(
				clock, IosImportEnvelopePolicyV1.WholeValidationTimeout);

			var result = new RetailProfileValidator(clock).Validate(
				graph,
				ImmutableArray.Create(map),
				deadline,
				CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.False);
				Assert.That(result.Failure.Code,
					Is.EqualTo(RetailImportErrorCode.ValidationTimedOut));
				Assert.That(result.Failure.Group, Is.Null);
				Assert.That(result.Failure.SafeBasename, Is.Null);
				Assert.That(result.Failure.Diagnostic,
					Is.EqualTo(RetailImportDiagnosticCode.DeadlineExceeded));
			});
		}

		[Test]
		public void FirstRequiredMountFailureWinsOverExpiryAtALaterMount()
		{
			using var graph = Graph(
				(RetailContentGroup.Base, RetailGroupValidation.Missing),
				(RetailContentGroup.Yuri, RetailGroupValidation.Missing));
			var clock = new CheckpointClock(expireAfterElapsedReads: 7);
			var result = new RetailProfileValidator(clock).Validate(
				graph,
				ImmutableArray<RetailMapProfileEvidence>.Empty,
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.False);
				Assert.That(result.Failure.Code,
					Is.EqualTo(RetailImportErrorCode.RequiredMountFailure));
				Assert.That(result.Failure.Group, Is.EqualTo(RetailContentGroup.Base));
				Assert.That(result.Failure.SafeBasename, Is.EqualTo("ra2.mix"));
				Assert.That(result.Failure.Diagnostic,
					Is.EqualTo(RetailImportDiagnosticCode.MissingRequiredArchive));
			});
		}

		[Test]
		public void FirstRequiredMountFailureWinsOverCancellationAtALaterMount()
		{
			using var graph = Graph(
				(RetailContentGroup.Base, RetailGroupValidation.Missing),
				(RetailContentGroup.Yuri, RetailGroupValidation.Missing));
			using var cancellation = new CancellationTokenSource();
			var clock = new CancellingCheckpointClock(7, cancellation);
			var result = new RetailProfileValidator(clock).Validate(
				graph,
				ImmutableArray<RetailMapProfileEvidence>.Empty,
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				cancellation.Token);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.False);
				Assert.That(result.Failure.Code,
					Is.EqualTo(RetailImportErrorCode.RequiredMountFailure));
				Assert.That(result.Failure.Group, Is.EqualTo(RetailContentGroup.Base));
				Assert.That(result.Failure.SafeBasename, Is.EqualTo("ra2.mix"));
				Assert.That(result.Failure.Diagnostic,
					Is.EqualTo(RetailImportDiagnosticCode.MissingRequiredArchive));
			});
		}

		[Test]
		public void SecondRequiredMountFailureIsCheckedAtItsOwnOrderedRow()
		{
			var failure = new RetailImportFailure(
				RetailImportErrorCode.CorruptArchive,
				RetailContentGroup.Base,
				"language.mix",
				RetailImportDiagnosticCode.MalformedEnvelope);
			using var graph = GraphWithFailure(failure);
			var clock = new CheckpointClock(expireAfterElapsedReads: 7);
			var result = new RetailProfileValidator(clock).Validate(
				graph,
				ImmutableArray<RetailMapProfileEvidence>.Empty,
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.False);
				Assert.That(result.Failure.Code,
					Is.EqualTo(RetailImportErrorCode.ValidationTimedOut));
				Assert.That(result.Failure.Group, Is.Null);
				Assert.That(result.Failure.SafeBasename, Is.Null);
				Assert.That(result.Failure.Diagnostic,
					Is.EqualTo(RetailImportDiagnosticCode.DeadlineExceeded));
			});
		}

		[Test]
		public void CancellationBeforeSecondRequiredMountFailureWinsAtThatRow()
		{
			var failure = new RetailImportFailure(
				RetailImportErrorCode.CorruptArchive,
				RetailContentGroup.Base,
				"language.mix",
				RetailImportDiagnosticCode.MalformedEnvelope);
			using var graph = GraphWithFailure(failure);
			using var cancellation = new CancellationTokenSource();
			var clock = new CancellingCheckpointClock(7, cancellation);
			var result = new RetailProfileValidator(clock).Validate(
				graph,
				ImmutableArray<RetailMapProfileEvidence>.Empty,
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				cancellation.Token);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.False);
				Assert.That(result.Failure.Code, Is.EqualTo(RetailImportErrorCode.Cancelled));
				Assert.That(result.Failure.Group, Is.Null);
				Assert.That(result.Failure.SafeBasename, Is.Null);
				Assert.That(result.Failure.Diagnostic,
					Is.EqualTo(RetailImportDiagnosticCode.None));
			});
		}

		[Test]
		public void ValidatorEnforcesTheImportedMapLimitBeforeCanonicalOrdering()
		{
			using var graph = Graph(
				(RetailContentGroup.Base, RetailGroupValidation.Ready),
				(RetailContentGroup.Yuri, RetailGroupValidation.Missing));
			var clock = new FakeClock();
			var deadline = RetailValidationDeadline.Start(
				clock, IosImportEnvelopePolicyV1.WholeValidationTimeout);
			var validator = new RetailProfileValidator(clock);

			var atLimit = validator.Validate(
				graph,
				Maps(IosImportEnvelopePolicyV1.ImportedMaps),
				deadline,
				CancellationToken.None);
			var aboveLimit = validator.Validate(
				graph,
				Maps(IosImportEnvelopePolicyV1.ImportedMaps + 1),
				deadline,
				CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(atLimit.Succeeded, Is.True);
				Assert.That(atLimit.Value.Maps,
					Has.Length.EqualTo(IosImportEnvelopePolicyV1.ImportedMaps));
				AssertProfileContextFailure(aboveLimit);
			});
		}

		[Test]
		public void ValidatorChecksCancellationAfterCanonicalMapOrdering()
		{
			using var graph = Graph(
				(RetailContentGroup.Base, RetailGroupValidation.Ready),
				(RetailContentGroup.Yuri, RetailGroupValidation.Missing));
			using var cancellation = new CancellationTokenSource();
			var clock = new CancellingCheckpointClock(9, cancellation);
			var result = new RetailProfileValidator(clock).Validate(
				graph,
				Maps(1),
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				cancellation.Token);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.False);
				Assert.That(result.Failure.Code, Is.EqualTo(RetailImportErrorCode.Cancelled));
				Assert.That(result.Failure.Group, Is.Null);
				Assert.That(result.Failure.SafeBasename, Is.Null);
				Assert.That(result.Failure.Diagnostic,
					Is.EqualTo(RetailImportDiagnosticCode.None));
			});
		}

		[Test]
		public void ValidatorChecksExpiryAfterCanonicalMapOrdering()
		{
			using var graph = Graph(
				(RetailContentGroup.Base, RetailGroupValidation.Ready),
				(RetailContentGroup.Yuri, RetailGroupValidation.Missing));
			var clock = new CheckpointClock(expireAfterElapsedReads: 9);
			var result = new RetailProfileValidator(clock).Validate(
				graph,
				Maps(1),
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.False);
				Assert.That(result.Failure.Code,
					Is.EqualTo(RetailImportErrorCode.ValidationTimedOut));
				Assert.That(result.Failure.Group, Is.Null);
				Assert.That(result.Failure.SafeBasename, Is.Null);
				Assert.That(result.Failure.Diagnostic,
					Is.EqualTo(RetailImportDiagnosticCode.DeadlineExceeded));
			});
		}

		[Test]
		public void AuthenticatedMusicRemainsUnavailableUntilTrackProofExists()
		{
			using var graph = Graph(
				(RetailContentGroup.Base, RetailGroupValidation.Ready),
				(RetailContentGroup.Ra2Music, RetailGroupValidation.Unavailable),
				(RetailContentGroup.Yuri, RetailGroupValidation.Missing),
				(RetailContentGroup.YuriMusic, RetailGroupValidation.Unavailable));
			var map = RetailMapProfileEvidence.FromValidatedMap(
				new string('c', 64), Ra2MapCompatibleProfiles.Base);
			var clock = new FakeClock();

			var result = new RetailProfileValidator(clock).Validate(
				graph,
				ImmutableArray.Create(map),
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.That(result.Succeeded, Is.True);
			Assert.Multiple(() =>
			{
				Assert.That(result.Value.Groups[RetailContentGroup.Ra2Music].Validation,
					Is.EqualTo(RetailGroupValidation.Unavailable));
				Assert.That(result.Value.Groups[RetailContentGroup.Ra2Music].Recognition,
					Is.EqualTo(RetailEditionRecognition.NotApplicable));
				Assert.That(result.Value.Groups[RetailContentGroup.YuriMusic].Validation,
					Is.EqualTo(RetailGroupValidation.Unavailable));
				Assert.That(result.Value.Groups[RetailContentGroup.YuriMusic].Recognition,
					Is.EqualTo(RetailEditionRecognition.NotApplicable));
				Assert.That(result.Value.GroupFailures, Is.Empty);
			});
		}

		[Test]
		public void ProfileResultConstructorRejectsNonClosedSuccessfulGroupStates()
		{
			var valid = ValidProfileValidation(Ra2RuntimeProfileId.Base);
			var missingGroup = valid.Groups.Remove(RetailContentGroup.YuriMusic);
			var foreignGroup = missingGroup.Add(
				(RetailContentGroup)int.MaxValue,
				valid.Groups[RetailContentGroup.YuriMusic]);
			var recognized = valid.Groups.SetItem(
				RetailContentGroup.Base,
				new RetailGroupStatus(
					RetailGroupValidation.Ready,
					RetailEditionRecognition.Recognized,
					RetailImportDiagnosticCode.None));
			var baseMissing = valid.Groups.SetItem(
				RetailContentGroup.Base,
				Status(RetailGroupValidation.Missing));
			var mapsUnavailable = valid.Groups.SetItem(
				RetailContentGroup.Maps,
				Status(RetailGroupValidation.Unavailable));
			var musicReady = valid.Groups.SetItem(
				RetailContentGroup.Ra2Music,
				Status(RetailGroupValidation.Ready));

			Assert.Multiple(() =>
			{
				Assert.Throws<ArgumentException>(() => DirectValidation(valid, groups: missingGroup));
				Assert.Throws<ArgumentException>(() => DirectValidation(valid, groups: foreignGroup));
				Assert.Throws<ArgumentException>(() => DirectValidation(valid, groups: recognized));
				Assert.Throws<ArgumentException>(() => DirectValidation(valid, groups: baseMissing));
				Assert.Throws<ArgumentException>(() => DirectValidation(valid, groups: mapsUnavailable));
				Assert.Throws<ArgumentException>(() => DirectValidation(valid, groups: musicReady));
			});
		}

		[Test]
		public void ProfileResultConstructorRequiresMandatoryMapEvidenceAndSelectedYuriState()
		{
			var validBase = ValidProfileValidation(Ra2RuntimeProfileId.Base);
			var validYuri = ValidProfileValidation(Ra2RuntimeProfileId.Yuri);
			var yuriOnlyMap = RetailMapProfileEvidence.FromValidatedMap(
				new string('f', 64), Ra2MapCompatibleProfiles.Yuri);
			var baseOnlyMap = RetailMapProfileEvidence.FromValidatedMap(
				new string('1', 64), Ra2MapCompatibleProfiles.Base);
			var yuriUnavailable = validYuri.Groups.SetItem(
				RetailContentGroup.Yuri,
				new RetailGroupStatus(
					RetailGroupValidation.Unavailable,
					RetailEditionRecognition.CompatibleUnrecognized,
					RetailImportDiagnosticCode.None));

			Assert.Multiple(() =>
			{
				Assert.Throws<ArgumentException>(() => DirectValidation(
					validBase, maps: ImmutableArray.Create(yuriOnlyMap)));
				Assert.Throws<ArgumentException>(() => DirectValidation(
					validBase, maps: Maps(IosImportEnvelopePolicyV1.ImportedMaps + 1)));
				Assert.Throws<ArgumentException>(() => DirectValidation(
					validYuri, maps: ImmutableArray.Create(baseOnlyMap)));
				Assert.Throws<ArgumentException>(() => DirectValidation(validYuri, groups: yuriUnavailable));
				Assert.Throws<ArgumentException>(() => new RetailProfileValidation(
					Ra2RuntimeProfileId.Base,
					validYuri.MountContract,
					validBase.Groups,
					validBase.GroupFailures,
					validBase.Maps));
			});
		}

		[Test]
		public void ProfileResultConstructorRejectsForgedOptionalFailureTuple()
		{
			var validFailure = new RetailImportFailure(
				RetailImportErrorCode.PartialYuri,
				RetailContentGroup.Yuri,
				"ra2md.mix",
				RetailImportDiagnosticCode.MissingRequiredArchive);
			using var graph = GraphWithFailure(validFailure);
			var map = RetailMapProfileEvidence.FromValidatedMap(
				new string('2', 64), Ra2MapCompatibleProfiles.Base);
			var clock = new FakeClock();
			var result = new RetailProfileValidator(clock).Validate(
				graph,
				ImmutableArray.Create(map),
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.That(result.Succeeded, Is.True);
			var valid = result.Value;
			var forged = new RetailImportFailure(
				RetailImportErrorCode.CorruptArchive,
				RetailContentGroup.Yuri,
				"ra2md.mix",
				RetailImportDiagnosticCode.MissingRequiredArchive);
			var failures = valid.GroupFailures.SetItem(RetailContentGroup.Yuri, forged);

			Assert.Multiple(() =>
			{
				Assert.Throws<ArgumentException>(() => DirectValidation(
					valid,
					groupFailures: ImmutableSortedDictionary<
						RetailContentGroup, RetailImportFailure>.Empty));
				Assert.Throws<ArgumentException>(() => DirectValidation(valid, groupFailures: failures));
			});
		}

		static RetailProfileValidation DirectValidation(
			RetailProfileValidation source,
			ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> groups = null,
			ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure> groupFailures = null,
			ImmutableArray<RetailMapProfileEvidence> maps = default) =>
			new(
				source.SelectedProfile,
				source.MountContract,
				groups ?? source.Groups,
				groupFailures ?? source.GroupFailures,
				maps.IsDefault ? source.Maps : maps);

		static RetailProfileValidation ValidProfileValidation(Ra2RuntimeProfileId profile)
		{
			using var graph = Graph(
				(RetailContentGroup.Base, RetailGroupValidation.Ready),
				(RetailContentGroup.Yuri, RetailGroupValidation.Ready));
			var compatibleProfiles = profile == Ra2RuntimeProfileId.Yuri
				? Ra2MapCompatibleProfiles.Base | Ra2MapCompatibleProfiles.Yuri
				: Ra2MapCompatibleProfiles.Base;
			var map = RetailMapProfileEvidence.FromValidatedMap(
				new string('3', 64), compatibleProfiles);
			var clock = new FakeClock();
			var result = new RetailProfileValidator(clock).Validate(
				graph,
				ImmutableArray.Create(map),
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.That(result.Succeeded, Is.True);
			return result.Value;
		}

		static ImmutableArray<RetailMapProfileEvidence> Maps(int count) =>
			Enumerable.Range(0, count).Select(index =>
				RetailMapProfileEvidence.FromValidatedMap(
					index.ToString("x64", CultureInfo.InvariantCulture),
					Ra2MapCompatibleProfiles.Base)).ToImmutableArray();

		static void AssertProfileContextFailure(
			RetailOperationResult<RetailProfileValidation> result)
		{
			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.False);
				Assert.That(result.Failure.Code,
					Is.EqualTo(RetailImportErrorCode.ProfileContextFailure));
				Assert.That(result.Failure.Group, Is.Null);
				Assert.That(result.Failure.SafeBasename, Is.Null);
				Assert.That(result.Failure.Diagnostic,
					Is.EqualTo(RetailImportDiagnosticCode.InvalidMapPackage));
			});
		}

		static string[] MountShape(Ra2RetailMountContract contract)
		{
			var result = new string[contract.Mounts.Length];
			for (var i = 0; i < result.Length; i++)
			{
				var mount = contract.Mounts[i];
				result[i] = $"{mount.Group}:{mount.SafeBasename}:{mount.MountName}:{mount.Required}";
			}

			return result;
		}

		static RetailArchiveGraphResult Graph(
			params (RetailContentGroup Group, RetailGroupValidation Validation)[] overrides)
		{
			var groups = DefaultGroups();
			foreach (var (group, validation) in overrides)
				groups[group] = new RetailGroupStatus(
					validation,
					RetailEditionRecognition.NotApplicable,
					RetailImportDiagnosticCode.None);

			return new RetailArchiveGraphResult(
				groups.ToImmutable(),
				ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure>.Empty,
				ImmutableArray<ValidatedEmbeddedMapSource>.Empty);
		}

		static RetailArchiveGraphResult GraphWithFailure(RetailImportFailure failure)
			=> GraphWithFailures(failure);

		static RetailArchiveGraphResult GraphWithFailures(params RetailImportFailure[] failures)
		{
			var groups = DefaultGroups();
			groups[RetailContentGroup.Base] = new RetailGroupStatus(
				RetailGroupValidation.Ready,
				RetailEditionRecognition.NotApplicable,
				RetailImportDiagnosticCode.None);
			var groupFailures = ImmutableSortedDictionary.CreateBuilder<
				RetailContentGroup, RetailImportFailure>();
			foreach (var failure in failures)
			{
				groups[failure.Group.Value] = new RetailGroupStatus(
					RetailGroupValidation.Invalid,
					RetailEditionRecognition.NotApplicable,
					failure.Diagnostic);
				groupFailures.Add(failure.Group.Value, failure);
			}

			return new RetailArchiveGraphResult(
				groups.ToImmutable(),
				groupFailures.ToImmutable(),
				ImmutableArray<ValidatedEmbeddedMapSource>.Empty);
		}

		static ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus>.Builder DefaultGroups()
		{
			var groups = ImmutableSortedDictionary.CreateBuilder<RetailContentGroup, RetailGroupStatus>();
			groups.Add(RetailContentGroup.Base, Status(RetailGroupValidation.Missing));
			groups.Add(RetailContentGroup.Ra2Music, Status(RetailGroupValidation.Missing));
			groups.Add(RetailContentGroup.Yuri, Status(RetailGroupValidation.Missing));
			groups.Add(RetailContentGroup.YuriMusic, Status(RetailGroupValidation.Missing));
			groups.Add(RetailContentGroup.Maps, Status(RetailGroupValidation.Missing));
			return groups;
		}

		static RetailGroupStatus Status(RetailGroupValidation validation) =>
			new(validation, RetailEditionRecognition.NotApplicable, RetailImportDiagnosticCode.None);

		sealed class FakeClock : IRetailMonotonicClock
		{
			public long GetTimestamp() => 0;
			public TimeSpan GetElapsedTime(long start, long end) => TimeSpan.Zero;
		}

		sealed class CheckpointClock : IRetailMonotonicClock
		{
			readonly int expireAfterElapsedReads;
			int elapsedReads;

			public CheckpointClock(int expireAfterElapsedReads)
			{
				this.expireAfterElapsedReads = expireAfterElapsedReads;
			}

			public long GetTimestamp() => 0;

			public TimeSpan GetElapsedTime(long start, long end) =>
				++elapsedReads > expireAfterElapsedReads
					? TimeSpan.MaxValue : TimeSpan.Zero;
		}

		sealed class CancellingCheckpointClock : IRetailMonotonicClock
		{
			readonly int cancelAfterElapsedReads;
			readonly CancellationTokenSource cancellation;
			int elapsedReads;

			public CancellingCheckpointClock(
				int cancelAfterElapsedReads,
				CancellationTokenSource cancellation)
			{
				this.cancelAfterElapsedReads = cancelAfterElapsedReads;
				this.cancellation = cancellation;
			}

			public long GetTimestamp() => 0;

			public TimeSpan GetElapsedTime(long start, long end)
			{
				if (++elapsedReads == cancelAfterElapsedReads)
					cancellation.Cancel();

				return TimeSpan.Zero;
			}
		}
	}

	[TestFixture]
	public sealed class RetailCandidateProfileValidationTestIosRuntimeResourceRoot
	{
		[Test]
		public void DarwinStatLayoutAndNativeSymbolsMatchEachRuntimeAbi()
		{
			Assert.That(IosRuntimeUnixNative.TryGetLayout(
				OSPlatform.OSX, Architecture.Arm64, out var arm64), Is.True);
			Assert.That(IosRuntimeUnixNative.TryGetLayout(
				OSPlatform.OSX, Architecture.X64, out var x64), Is.True);

			Assert.Multiple(() =>
			{
				Assert.That((
					arm64.DeviceOffset, arm64.DeviceWidth,
					arm64.ModeOffset, arm64.ModeWidth,
					arm64.InodeOffset, arm64.InodeWidth,
					arm64.SizeOffset),
					Is.EqualTo((0, 4, 4, 2, 8, 8, 96)));
				Assert.That((
					x64.DeviceOffset, x64.DeviceWidth,
					x64.ModeOffset, x64.ModeWidth,
					x64.InodeOffset, x64.InodeWidth,
					x64.SizeOffset),
						Is.EqualTo((0, 4, 4, 2, 8, 8, 96)));
			});

			var native = typeof(IosRuntimeUnixNative);
			var selector = native.GetMethod(
				"UseDarwinInode64FStatSymbol",
				BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
			Assert.That(selector, Is.Not.Null);
			Assert.Multiple(() =>
			{
				Assert.That(selector.Invoke(null, new object[] { true, Architecture.X64 }), Is.True);
				Assert.That(selector.Invoke(null, new object[] { true, Architecture.Arm64 }), Is.False);
				Assert.That(selector.Invoke(null, new object[] { false, Architecture.X64 }), Is.False,
					"An iOS x64 simulator must continue to call the bare fstat symbol.");
				var entryPoints = native.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
					.Select(method => method.GetCustomAttribute<DllImportAttribute>()?.EntryPoint)
					.Where(entryPoint => entryPoint != null).ToArray();
				Assert.That(entryPoints, Does.Contain("fstat"));
				Assert.That(entryPoints, Does.Contain("fstat$INODE64"));
			});
		}

		[Test]
		public void InvalidUnicodeAndNonCanonicalPathsFailClosed()
		{
			RequireUnixAdapter();
			using var fixture = new TemporaryDirectory("openra-runtime-root-invalid-unicode");
			var opened = IosRuntimeResourceRoot.OpenBundleRoot(fixture.Path);
			Assert.That(opened.Succeeded, Is.True);
			using var root = opened.Value;
			foreach (var path in new[] { "\uD800", "\uDC00", "prefix/\uD800", "e\u0301.bin" })
			{
				RetailOperationResult<IIosRuntimeResourceLease> result = null;
				Assert.DoesNotThrow(() => result = root.AcquireRegularFile(path));
				Assert.Multiple(() =>
				{
					Assert.That(result.Succeeded, Is.False);
					Assert.That(result.Failure.Code,
						Is.EqualTo(RetailImportErrorCode.RuntimeResourcesMissing));
					Assert.That(result.Failure.Group, Is.Null);
					Assert.That(result.Failure.SafeBasename, Is.Null);
					Assert.That(result.Failure.Diagnostic,
						Is.EqualTo(RetailImportDiagnosticCode.InvalidMapPackage));
				});
			}
		}

		[Test]
		public void RejectsParentAndLeafSymlinksAndDirectoryLeaves()
		{
			RequireUnixAdapter();
			using var fixture = new TemporaryDirectory("openra-runtime-root-links");
			var realParent = Path.Combine(fixture.Path, "real");
			Directory.CreateDirectory(realParent);
			File.WriteAllBytes(Path.Combine(realParent, "payload.bin"), new byte[] { 1, 2, 3 });
			Directory.CreateDirectory(Path.Combine(realParent, "directory-leaf"));
			Directory.CreateSymbolicLink(Path.Combine(fixture.Path, "parent-link"), realParent);
			File.CreateSymbolicLink(
				Path.Combine(realParent, "leaf-link"),
				Path.Combine(realParent, "payload.bin"));

			var opened = IosRuntimeResourceRoot.OpenBundleRoot(fixture.Path);
			Assert.That(opened.Succeeded, Is.True);
			using var root = opened.Value;

			Assert.Multiple(() =>
			{
				Assert.That(root.AcquireRegularFile("parent-link/payload.bin").Succeeded, Is.False);
				Assert.That(root.AcquireRegularFile("real/leaf-link").Succeeded, Is.False);
				Assert.That(root.AcquireRegularFile("real/directory-leaf").Succeeded, Is.False);
			});
		}

		[Test]
		public void ChildStreamIsOneShotAndOutlivesRootAndLease()
		{
			RequireUnixAdapter();
			using var fixture = new TemporaryDirectory("openra-runtime-root-stream");
			var expected = Encoding.UTF8.GetBytes("pinned-runtime-resource");
			File.WriteAllBytes(Path.Combine(fixture.Path, "payload.bin"), expected);
			var opened = IosRuntimeResourceRoot.OpenBundleRoot(fixture.Path);
			Assert.That(opened.Succeeded, Is.True);
			var root = opened.Value;
			var acquired = root.AcquireRegularFile("payload.bin");
			Assert.That(acquired.Succeeded, Is.True);
			var lease = acquired.Value;
			var stream = lease.OpenVerifiedRead();

			Assert.Throws<InvalidOperationException>(() => lease.OpenVerifiedRead());
			lease.Dispose();
			lease.Dispose();
			root.Dispose();
			root.Dispose();

			using (stream)
			using (var copied = new MemoryStream())
			{
				stream.CopyTo(copied);
				Assert.That(copied.ToArray(), Is.EqualTo(expected));
			}
		}

		[Test]
		public void VerifyUnchangedRejectsLeafReplacementWhilePinnedReadUsesOriginalDescriptor()
		{
			RequireUnixAdapter();
			using var fixture = new TemporaryDirectory("openra-runtime-root-replace");
			var original = Encoding.UTF8.GetBytes("original");
			var replacement = Encoding.UTF8.GetBytes("replaced");
			var path = Path.Combine(fixture.Path, "payload.bin");
			File.WriteAllBytes(path, original);
			var opened = IosRuntimeResourceRoot.OpenBundleRoot(fixture.Path);
			Assert.That(opened.Succeeded, Is.True);
			using var root = opened.Value;
			var acquired = root.AcquireRegularFile("payload.bin");
			Assert.That(acquired.Succeeded, Is.True);
			using var lease = acquired.Value;
			using var stream = lease.OpenVerifiedRead();
			var replacementPath = Path.Combine(fixture.Path, "replacement.bin");
			File.WriteAllBytes(replacementPath, replacement);
			File.Move(replacementPath, path, overwrite: true);

			Assert.Throws<IOException>(() => lease.VerifyUnchanged());
			using var copied = new MemoryStream();
			stream.CopyTo(copied);
			Assert.That(copied.ToArray(), Is.EqualTo(original));
		}

		[Test]
		public void VerifyUnchangedRejectsParentReplacementEvenWhenLeafBytesMatch()
		{
			RequireUnixAdapter();
			using var fixture = new TemporaryDirectory("openra-runtime-root-parent-replace");
			var parent = Path.Combine(fixture.Path, "parent");
			var displaced = Path.Combine(fixture.Path, "displaced");
			var expected = Encoding.UTF8.GetBytes("same-bytes");
			Directory.CreateDirectory(parent);
			File.WriteAllBytes(Path.Combine(parent, "payload.bin"), expected);
			var opened = IosRuntimeResourceRoot.OpenBundleRoot(fixture.Path);
			Assert.That(opened.Succeeded, Is.True);
			using var root = opened.Value;
			var acquired = root.AcquireRegularFile("parent/payload.bin");
			Assert.That(acquired.Succeeded, Is.True);
			using var lease = acquired.Value;
			using var stream = lease.OpenVerifiedRead();
			Directory.Move(parent, displaced);
			Directory.CreateDirectory(parent);
			File.WriteAllBytes(Path.Combine(parent, "payload.bin"), expected);

			Assert.Throws<IOException>(() => lease.VerifyUnchanged());
			using var copied = new MemoryStream();
			stream.CopyTo(copied);
			Assert.That(copied.ToArray(), Is.EqualTo(expected));
		}

		static void RequireUnixAdapter()
		{
			if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
				Assert.Ignore("The production descriptor-relative adapter supports Darwin and Linux.");
		}
	}

	[TestFixture]
	public sealed class RetailCandidateProfileValidationTestIosRuntimeResourcePreflight
	{
		static readonly Lazy<RuntimeContractFixture> RuntimeFixture =
			new(RuntimeContractFixture.Load);

		[Test]
		public void ClockMismatchReachesNoResourceAcquisition()
		{
			var root = new RecordingRuntimeRoot(RuntimeFixture.Value.Payloads);
			var deadlineClock = new SwitchClock();
			var suppliedClock = new SwitchClock();
			var result = Check(
				root,
				suppliedClock,
				RetailValidationDeadline.Start(
					deadlineClock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.Multiple(() =>
			{
				AssertFailure(result, RetailImportErrorCode.ProfileContextFailure,
					RetailImportDiagnosticCode.ProfileMetadataMismatch);
				Assert.That(root.AcquiredPaths, Is.Empty);
				Assert.That(root.DisposedPaths, Is.Empty);
			});
		}

		[Test]
		public void CancellationDuringHashReturnsClosedFailureAndReleasesTheLease()
		{
			var fixture = RuntimeFixture.Value;
			using var cancellation = new CancellationTokenSource();
			var root = new RecordingRuntimeRoot(fixture.Payloads)
			{
				StreamFirstRead = ordinal =>
				{
					if (ordinal == 0)
						cancellation.Cancel();
				},
			};
			var clock = new SwitchClock();
			var result = Check(
				root,
				clock,
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				cancellation.Token);

			Assert.Multiple(() =>
			{
				AssertFailure(result, RetailImportErrorCode.Cancelled,
					RetailImportDiagnosticCode.None);
				Assert.That(root.AcquiredPaths, Has.Count.EqualTo(1));
				Assert.That(root.DisposedPaths, Is.EqualTo(root.AcquiredPaths));
				Assert.That(root.VerifiedPaths, Is.Empty);
			});
		}

		[Test]
		public void TimeoutAfterFinalHashStopsBeforeFinalExpectedNameVerificationAndReleasesInReverse()
		{
			var fixture = RuntimeFixture.Value;
			var clock = new SwitchClock();
			var root = new RecordingRuntimeRoot(fixture.Payloads)
			{
				StreamDisposed = ordinal =>
				{
					if (ordinal == fixture.BaseDescriptorCount - 1)
						clock.Expired = true;
				},
			};
			var result = Check(
				root,
				clock,
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.Multiple(() =>
			{
				AssertFailure(result, RetailImportErrorCode.ValidationTimedOut,
					RetailImportDiagnosticCode.DeadlineExceeded);
				Assert.That(root.AcquiredPaths, Has.Count.EqualTo(fixture.BaseDescriptorCount));
				Assert.That(root.VerifiedPaths, Is.Empty);
				Assert.That(root.DisposedPaths,
					Is.EqualTo(root.AcquiredPaths.AsEnumerable().Reverse()));
			});
		}

		[Test]
		public void HashMismatchMapsToRuntimeResourcesMissingAndReleasesTheLease()
		{
			var fixture = RuntimeFixture.Value;
			var root = new RecordingRuntimeRoot(fixture.Payloads)
			{
				PayloadTransform = (ordinal, payload) =>
				{
					var copy = payload.ToArray();
					if (ordinal == 0)
						copy[0] ^= 0xFF;

					return copy;
				},
			};
			var clock = new SwitchClock();
			var result = Check(
				root,
				clock,
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.Multiple(() =>
			{
				AssertFailure(result, RetailImportErrorCode.RuntimeResourcesMissing,
					RetailImportDiagnosticCode.InvalidMapPackage);
				Assert.That(root.AcquiredPaths, Has.Count.EqualTo(1));
				Assert.That(root.DisposedPaths, Is.EqualTo(root.AcquiredPaths));
			});
		}

		[Test]
		public void CancellationDuringFinalVerificationStillReleasesEveryLeaseInReverse()
		{
			var fixture = RuntimeFixture.Value;
			using var cancellation = new CancellationTokenSource();
			var root = new RecordingRuntimeRoot(fixture.Payloads)
			{
				LeaseVerified = ordinal =>
				{
					if (ordinal == 0)
						cancellation.Cancel();
				},
			};
			var clock = new SwitchClock();
			var result = Check(
				root,
				clock,
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				cancellation.Token);

			Assert.Multiple(() =>
			{
				AssertFailure(result, RetailImportErrorCode.Cancelled,
					RetailImportDiagnosticCode.None);
				Assert.That(root.AcquiredPaths, Has.Count.EqualTo(fixture.BaseDescriptorCount));
				Assert.That(root.VerifiedPaths, Has.Count.EqualTo(1));
				Assert.That(root.DisposedPaths,
					Is.EqualTo(root.AcquiredPaths.AsEnumerable().Reverse()));
			});
		}

		[Test]
		public void SuccessHashesThenFinallyVerifiesEveryExpectedNameLeaseAndReleasesInReverse()
		{
			var fixture = RuntimeFixture.Value;
			var root = new RecordingRuntimeRoot(fixture.Payloads);
			var clock = new SwitchClock();
			var result = Check(
				root,
				clock,
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(result.StaticResourcesPresent, Is.True);
				Assert.That(result.Failure, Is.Null);
				Assert.That(root.AcquiredPaths, Has.Count.EqualTo(fixture.BaseDescriptorCount));
				Assert.That(root.VerifiedPaths, Is.EqualTo(root.AcquiredPaths));
				Assert.That(root.DisposedPaths,
					Is.EqualTo(root.AcquiredPaths.AsEnumerable().Reverse()));
			});
		}

		[Test]
		public void TracingRootDelegatesInnerLeaseMetadataInsteadOfEchoingRequestedName()
		{
			const string wrongLeasePath = "wrong/metadata.bin";
			var fixture = RuntimeFixture.Value;
			var expectedPaths = fixture.ExpectedPaths(Ra2RuntimeProfileId.Base);
			var inner = new RecordingRuntimeRoot(fixture.Payloads)
			{
				LeasePathTransform = (ordinal, path) =>
					ordinal == 0 ? wrongLeasePath : path,
			};
			using var root = new TracingRuntimeRoot(inner);
			var clock = new SwitchClock();
			var result = Check(
				root,
				clock,
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.Multiple(() =>
			{
				AssertFailure(result, RetailImportErrorCode.RuntimeResourcesMissing,
					RetailImportDiagnosticCode.InvalidMapPackage);
				Assert.That(root.AcquiredPaths, Is.EqualTo(expectedPaths.Take(1)));
				Assert.That(root.HashedPaths, Is.Empty);
				Assert.That(root.VerifiedPaths, Is.Empty);
				Assert.That(root.StreamDisposeAttemptPaths, Is.Empty);
				Assert.That(root.DisposedPaths, Is.EqualTo(new[] { wrongLeasePath }));
				Assert.That(root.Events, Is.EqualTo(new[]
				{
					TraceEvent(RuntimeTracePhase.Acquire, expectedPaths[0]),
					TraceEvent(RuntimeTracePhase.LeaseDispose, wrongLeasePath),
				}));
				Assert.That(root.DisposeCalls, Is.Zero,
					"Check must borrow rather than dispose the supplied root.");
			});
		}

		[Test]
		public void TracingObserverCountsEveryDuplicateLeaseAndStreamDisposeAttemptBeforeFailing()
		{
			var fixture = RuntimeFixture.Value;
			var path = fixture.ExpectedPaths(Ra2RuntimeProfileId.Base)[0];
			using var root = new TracingRuntimeRoot(
				new RecordingRuntimeRoot(fixture.Payloads));
			var acquired = root.AcquireRegularFile(path);
			Assert.That(acquired.Succeeded, Is.True);
			var lease = acquired.Value;
			var stream = lease.OpenVerifiedRead();

			stream.Dispose();
			Assert.Throws<AssertionException>(() => stream.Dispose());
			lease.Dispose();
			Assert.Throws<AssertionException>(() => lease.Dispose());

			Assert.Multiple(() =>
			{
				Assert.That(root.StreamDisposeAttemptPaths, Is.EqualTo(new[] { path, path }));
				Assert.That(root.DisposedPaths, Is.EqualTo(new[] { path, path }));
				Assert.That(root.Events, Is.EqualTo(new[]
				{
					TraceEvent(RuntimeTracePhase.Acquire, path),
					TraceEvent(RuntimeTracePhase.HashOpen, path),
					TraceEvent(RuntimeTracePhase.StreamDispose, path),
					TraceEvent(RuntimeTracePhase.StreamDispose, path),
					TraceEvent(RuntimeTracePhase.LeaseDispose, path),
					TraceEvent(RuntimeTracePhase.LeaseDispose, path),
				}));
				Assert.That(root.DisposeCalls, Is.Zero);
			});
		}

		[TestCase(Ra2RuntimeProfileId.Base, 277)]
		[TestCase(Ra2RuntimeProfileId.Yuri, 284)]
		public void FixedProfileProjectionAcquiresHashesAndFinallyVerifiesEachExpectedNameExactlyOnce(
			Ra2RuntimeProfileId profile,
			int expectedCount)
		{
			RequireUnixAdapter();
			var fixture = RuntimeFixture.Value;
			var expectedPaths = fixture.ExpectedPaths(profile);
			using var directory = fixture.MaterializeRoot("openra-runtime-preflight-projection");
			var opened = IosRuntimeResourceRoot.OpenBundleRoot(directory.Path);
			Assert.That(opened.Succeeded, Is.True);
			using var root = new TracingRuntimeRoot(opened.Value);
			var clock = new SwitchClock();
			var result = Check(
				root,
				profile,
				clock,
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(result.StaticResourcesPresent, Is.True);
				Assert.That(result.Failure, Is.Null);
				Assert.That(expectedPaths, Has.Length.EqualTo(expectedCount));
				Assert.That(root.AcquiredPaths, Is.EqualTo(expectedPaths));
				Assert.That(root.HashedPaths, Is.EqualTo(expectedPaths));
				Assert.That(root.VerifiedPaths, Is.EqualTo(expectedPaths));
				Assert.That(root.StreamDisposeAttemptPaths, Is.EqualTo(expectedPaths));
				Assert.That(root.AcquiredPaths.Distinct(StringComparer.Ordinal).Count(),
					Is.EqualTo(expectedCount));
				Assert.That(root.DisposedPaths,
					Is.EqualTo(expectedPaths.AsEnumerable().Reverse()));
				Assert.That(root.Events,
					Is.EqualTo(ExpectedTrace(expectedPaths, expectedPaths.Length)));
				Assert.That(root.DisposeCalls, Is.Zero,
					"Check must borrow rather than dispose the supplied root.");
			});
		}

		[TestCase(Ra2RuntimeProfileId.Base, 277)]
		[TestCase(Ra2RuntimeProfileId.Yuri, 284)]
		public void UnrelatedRootExtraIsNeverAcquiredHashedOrVerifiedAndDoesNotChangePreflightSuccess(
			Ra2RuntimeProfileId profile,
			int expectedCount)
		{
			RequireUnixAdapter();
			const string unrelatedPath = "unrelated/extra.bin";
			var fixture = RuntimeFixture.Value;
			var expectedPaths = fixture.ExpectedPaths(profile);
			using var directory = fixture.MaterializeRoot("openra-runtime-preflight-extra");
			var unrelatedFullPath = Path.Combine(directory.Path, "unrelated", "extra.bin");
			Directory.CreateDirectory(Path.GetDirectoryName(unrelatedFullPath));
			File.WriteAllBytes(unrelatedFullPath, Encoding.UTF8.GetBytes("unrelated-extra"));
			var opened = IosRuntimeResourceRoot.OpenBundleRoot(directory.Path);
			Assert.That(opened.Succeeded, Is.True);
			using var root = new TracingRuntimeRoot(opened.Value);
			var clock = new SwitchClock();
			var result = Check(
				root,
				profile,
				clock,
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(result.StaticResourcesPresent, Is.True);
				Assert.That(result.Failure, Is.Null);
				Assert.That(expectedPaths, Has.Length.EqualTo(expectedCount));
				Assert.That(root.AcquiredPaths, Is.EqualTo(expectedPaths));
				Assert.That(root.HashedPaths, Is.EqualTo(expectedPaths));
				Assert.That(root.VerifiedPaths, Is.EqualTo(expectedPaths));
				Assert.That(root.StreamDisposeAttemptPaths, Is.EqualTo(expectedPaths));
				Assert.That(root.DisposedPaths,
					Is.EqualTo(expectedPaths.AsEnumerable().Reverse()));
				Assert.That(root.AcquiredPaths, Does.Not.Contain(unrelatedPath));
				Assert.That(root.HashedPaths, Does.Not.Contain(unrelatedPath));
				Assert.That(root.VerifiedPaths, Does.Not.Contain(unrelatedPath));
				Assert.That(root.Events,
					Is.EqualTo(ExpectedTrace(expectedPaths, expectedPaths.Length)));
				Assert.That(root.DisposeCalls, Is.Zero,
					"Check must borrow rather than dispose the supplied root.");
			});
		}

		[TestCase(ExpectedMemberMutation.Remove)]
		[TestCase(ExpectedMemberMutation.Rename)]
		[TestCase(ExpectedMemberMutation.Replace)]
		public void ExpectedMemberMutationAfterHashFailsAtFinalExpectedNameVerification(
			ExpectedMemberMutation mutation)
		{
			RequireUnixAdapter();
			var fixture = RuntimeFixture.Value;
			var expectedPaths = fixture.ExpectedPaths(Ra2RuntimeProfileId.Base);
			using var directory = fixture.MaterializeRoot("openra-runtime-preflight-mutation");
			var mutatedPath = expectedPaths[0];
			var mutatedFullPath = Path.Combine(
				directory.Path,
				mutatedPath.Replace('/', Path.DirectorySeparatorChar));
			var mutationApplied = false;
			var opened = IosRuntimeResourceRoot.OpenBundleRoot(directory.Path);
			Assert.That(opened.Succeeded, Is.True);
			using var root = new TracingRuntimeRoot(opened.Value)
			{
				StreamDisposed = (ordinal, path) =>
				{
					if (ordinal != expectedPaths.Length - 1)
						return;

					Assert.That(path, Is.EqualTo(expectedPaths[^1]));
					ApplyExpectedMemberMutation(mutatedFullPath, mutation);
					mutationApplied = true;
				},
			};
			var clock = new SwitchClock();
			IosRuntimeResourcePreflightResult result = null;

			Assert.DoesNotThrow(() => result = Check(
				root,
				Ra2RuntimeProfileId.Base,
				clock,
				RetailValidationDeadline.Start(
					clock, IosImportEnvelopePolicyV1.WholeValidationTimeout),
				CancellationToken.None));

			Assert.Multiple(() =>
			{
				Assert.That(mutationApplied, Is.True);
				AssertFailure(result, RetailImportErrorCode.RuntimeResourcesMissing,
					RetailImportDiagnosticCode.InvalidMapPackage);
				Assert.That(root.AcquiredPaths, Is.EqualTo(expectedPaths));
				Assert.That(root.HashedPaths, Is.EqualTo(expectedPaths));
				Assert.That(root.VerifiedPaths, Is.EqualTo(expectedPaths.Take(1)));
				Assert.That(root.StreamDisposeAttemptPaths, Is.EqualTo(expectedPaths));
				Assert.That(root.DisposedPaths,
					Is.EqualTo(expectedPaths.AsEnumerable().Reverse()));
				Assert.That(root.Events, Is.EqualTo(ExpectedTrace(expectedPaths, 1)));
				Assert.That(root.DisposeCalls, Is.Zero,
					"Check must borrow rather than dispose the supplied root.");
			});
		}

		static ImmutableArray<string> ExpectedTrace(
			ImmutableArray<string> expectedPaths,
			int verifiedCount)
		{
			if (verifiedCount < 0 || verifiedCount > expectedPaths.Length)
				throw new ArgumentOutOfRangeException(nameof(verifiedCount));

			var events = ImmutableArray.CreateBuilder<string>(
				expectedPaths.Length * 6 + verifiedCount);
			foreach (var path in expectedPaths)
			{
				events.Add(TraceEvent(RuntimeTracePhase.Acquire, path));
				events.Add(TraceEvent(RuntimeTracePhase.HashOpen, path));
				events.Add(TraceEvent(RuntimeTracePhase.HashRead, path));
				events.Add(TraceEvent(RuntimeTracePhase.HashEndOfStream, path));
				events.Add(TraceEvent(RuntimeTracePhase.StreamDispose, path));
			}

			foreach (var path in expectedPaths.Take(verifiedCount))
				events.Add(TraceEvent(RuntimeTracePhase.VerifyUnchanged, path));

			foreach (var path in expectedPaths.Reverse())
				events.Add(TraceEvent(RuntimeTracePhase.LeaseDispose, path));

			return events.ToImmutable();
		}

		static string TraceEvent(RuntimeTracePhase phase, string path) =>
			$"{phase}:{path}";

		static void ApplyExpectedMemberMutation(
			string expectedFullPath,
			ExpectedMemberMutation mutation)
		{
			switch (mutation)
			{
				case ExpectedMemberMutation.Remove:
					File.Delete(expectedFullPath);
					break;
				case ExpectedMemberMutation.Rename:
					File.Move(expectedFullPath, expectedFullPath + ".renamed");
					break;
				case ExpectedMemberMutation.Replace:
					var replacementPath = expectedFullPath + ".replacement";
					File.WriteAllBytes(replacementPath, File.ReadAllBytes(expectedFullPath));
					File.Move(replacementPath, expectedFullPath, overwrite: true);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(mutation));
			}
		}

		static IosRuntimeResourcePreflightResult Check(
			IIosRuntimeResourceRoot root,
			IRetailMonotonicClock clock,
			RetailValidationDeadline deadline,
			CancellationToken cancellationToken) =>
			Check(root, Ra2RuntimeProfileId.Base, clock, deadline, cancellationToken);

		static IosRuntimeResourcePreflightResult Check(
			IIosRuntimeResourceRoot root,
			Ra2RuntimeProfileId profile,
			IRetailMonotonicClock clock,
			RetailValidationDeadline deadline,
			CancellationToken cancellationToken)
		{
			var modRoot = Path.Combine(RepositoryRoot(), "mods", "ra2");
			using var package = new Folder(modRoot);
			var selection = profile.ManifestSelection();
			using var manifest = new Manifest(selection.ModId, package, selection.EntryName);
			return new IosRuntimeResourcePreflight(RuntimeFixture.Value.Inventory).Check(
				manifest,
				root,
				profile,
				deadline,
				clock,
				cancellationToken);
		}

		static void RequireUnixAdapter()
		{
			if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
				Assert.Ignore("The production descriptor-relative adapter supports Darwin and Linux.");
		}

		static void AssertFailure(
			IosRuntimeResourcePreflightResult result,
			RetailImportErrorCode code,
			RetailImportDiagnosticCode diagnostic)
		{
			Assert.That(result.StaticResourcesPresent, Is.False);
			Assert.That(result.Failure, Is.Not.Null);
			Assert.That(result.Failure.Code, Is.EqualTo(code));
			Assert.That(result.Failure.Group, Is.Null);
			Assert.That(result.Failure.SafeBasename, Is.Null);
			Assert.That(result.Failure.Diagnostic, Is.EqualTo(diagnostic));
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !File.Exists(Path.Combine(
				directory.FullName,
				"packaging",
				"ios-runtime-contract-descriptor-shape-v1.json")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory.FullName;
		}

		sealed class RuntimeContractFixture
		{
			public RuntimeContractDescriptorInventory Inventory { get; }
			public IReadOnlyDictionary<string, byte[]> Payloads { get; }
			public int BaseDescriptorCount { get; }

			public ImmutableArray<string> ExpectedPaths(Ra2RuntimeProfileId profile) =>
				Inventory.Descriptors
					.Where(descriptor => descriptor.Profiles.Contains(profile))
					.Select(descriptor => descriptor.CanonicalBundleRelativePath)
					.ToImmutableArray();

			public TemporaryDirectory MaterializeRoot(string prefix)
			{
				var directory = new TemporaryDirectory(prefix);
				try
				{
					foreach (var pair in Payloads)
					{
						var fullPath = Path.Combine(
							directory.Path,
							pair.Key.Replace('/', Path.DirectorySeparatorChar));
						Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
						File.WriteAllBytes(fullPath, pair.Value);
					}

					return directory;
				}
				catch
				{
					directory.Dispose();
					throw;
				}
			}

			RuntimeContractFixture(
				RuntimeContractDescriptorInventory inventory,
				IReadOnlyDictionary<string, byte[]> payloads,
				int baseDescriptorCount)
			{
				Inventory = inventory;
				Payloads = payloads;
				BaseDescriptorCount = baseDescriptorCount;
			}

			public static RuntimeContractFixture Load()
			{
				var path = Path.Combine(
					RepositoryRoot(),
					"packaging",
					"ios-runtime-contract-descriptor-shape-v1.json");
				using var document = JsonDocument.Parse(File.ReadAllBytes(path));
				var root = document.RootElement;
				var descriptors = ImmutableArray.CreateBuilder<RuntimeContractResourceDescriptor>();
				var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);
				foreach (var item in root.GetProperty("descriptors").EnumerateArray())
				{
					var logicalName = item.GetProperty("logicalName").GetString();
					var bundlePath = item.GetProperty("bundleRelativePath").GetString();
					var profiles = item.GetProperty("profiles").EnumerateArray()
						.Select(value => Enum.Parse<Ra2RuntimeProfileId>(value.GetString()))
						.ToImmutableArray();
					var purpose = Enum.Parse<RuntimeContractResourcePurpose>(
						item.GetProperty("purpose").GetString());
					var payload = Encoding.UTF8.GetBytes("runtime-fixture:" + logicalName);
					var digest = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
					descriptors.Add(new RuntimeContractResourceDescriptor(
						logicalName,
						bundlePath,
						payload.LongLength,
						digest,
						profiles,
						purpose));
					payloads.Add(bundlePath, payload);
				}

				var immutableDescriptors = descriptors.ToImmutable();
				var inventory = new RuntimeContractDescriptorInventory(
					root.GetProperty("schemaVersion").GetInt32(),
					root.GetProperty("descriptorCount").GetInt32(),
					root.GetProperty("shapeDigest").GetString(),
					immutableDescriptors);
				return new RuntimeContractFixture(
					inventory,
					payloads,
					immutableDescriptors.Count(descriptor =>
						descriptor.Profiles.Contains(Ra2RuntimeProfileId.Base)));
			}
		}

		sealed class SwitchClock : IRetailMonotonicClock
		{
			public bool Expired { get; set; }
			public long GetTimestamp() => 0;
			public TimeSpan GetElapsedTime(long start, long end) =>
				Expired ? TimeSpan.MaxValue : TimeSpan.Zero;
		}

		public enum ExpectedMemberMutation
		{
			Remove,
			Rename,
			Replace,
		}

		enum RuntimeTracePhase
		{
			Acquire,
			HashOpen,
			HashRead,
			HashEndOfStream,
			StreamDispose,
			VerifyUnchanged,
			LeaseDispose,
			RootDispose,
		}

		sealed class TracingRuntimeRoot : IIosRuntimeResourceRoot
		{
			readonly IIosRuntimeResourceRoot inner;

			public List<string> Events { get; } = new();
			public List<string> AcquiredPaths { get; } = new();
			public List<string> HashedPaths { get; } = new();
			public List<string> VerifiedPaths { get; } = new();
			public List<string> DisposedPaths { get; } = new();
			public List<string> StreamDisposeAttemptPaths { get; } = new();
			public int DisposeCalls { get; private set; }
			public Action<int, string> StreamDisposed { get; set; }

			public TracingRuntimeRoot(IIosRuntimeResourceRoot inner)
			{
				this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
			}

			public RetailOperationResult<IIosRuntimeResourceLease> AcquireRegularFile(
				string canonicalRelativePath)
			{
				var ordinal = AcquiredPaths.Count;
				AcquiredPaths.Add(canonicalRelativePath);
				Events.Add(TraceEvent(RuntimeTracePhase.Acquire, canonicalRelativePath));
				var acquired = inner.AcquireRegularFile(canonicalRelativePath);
				if (!acquired.Succeeded)
					return acquired;

				return RetailOperationResult<IIosRuntimeResourceLease>.Success(
					new TracingRuntimeLease(this, acquired.Value, ordinal));
			}

			public void Dispose()
			{
				DisposeCalls++;
				Events.Add(TraceEvent(RuntimeTracePhase.RootDispose, "<root>"));
				inner.Dispose();
			}

			public void RecordHashOpened(string path)
			{
				HashedPaths.Add(path);
				Events.Add(TraceEvent(RuntimeTracePhase.HashOpen, path));
			}

			public void RecordHashRead(string path) =>
				Events.Add(TraceEvent(RuntimeTracePhase.HashRead, path));

			public void RecordHashEndOfStream(string path) =>
				Events.Add(TraceEvent(RuntimeTracePhase.HashEndOfStream, path));

			public void RecordStreamDisposeAttempt(string path)
			{
				StreamDisposeAttemptPaths.Add(path);
				Events.Add(TraceEvent(RuntimeTracePhase.StreamDispose, path));
			}

			public void RecordStreamDisposed(int ordinal, string path) =>
				StreamDisposed?.Invoke(ordinal, path);

			public void RecordVerified(string path)
			{
				VerifiedPaths.Add(path);
				Events.Add(TraceEvent(RuntimeTracePhase.VerifyUnchanged, path));
			}

			public void RecordDisposed(string path)
			{
				DisposedPaths.Add(path);
				Events.Add(TraceEvent(RuntimeTracePhase.LeaseDispose, path));
			}
		}

		sealed class TracingRuntimeLease : IIosRuntimeResourceLease
		{
			readonly TracingRuntimeRoot root;
			readonly IIosRuntimeResourceLease inner;
			readonly int ordinal;
			readonly string tracePath;
			bool disposed;

			public string CanonicalRelativePath => inner.CanonicalRelativePath;
			public long Length => inner.Length;

			public TracingRuntimeLease(
				TracingRuntimeRoot root,
				IIosRuntimeResourceLease inner,
				int ordinal)
			{
				this.root = root ?? throw new ArgumentNullException(nameof(root));
				this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
				this.ordinal = ordinal;
				tracePath = inner.CanonicalRelativePath;
			}

			public Stream OpenVerifiedRead()
			{
				var canonicalPath = CanonicalRelativePath;
				var stream = inner.OpenVerifiedRead();
				root.RecordHashOpened(canonicalPath);
				return new TracingRuntimeStream(stream, root, ordinal, canonicalPath);
			}

			public void VerifyUnchanged()
			{
				root.RecordVerified(CanonicalRelativePath);
				inner.VerifyUnchanged();
			}

			public void Dispose()
			{
				root.RecordDisposed(tracePath);
				if (disposed)
					throw new AssertionException(
						$"Lease '{tracePath}' was disposed more than once.");

				disposed = true;
				inner.Dispose();
			}
		}

		sealed class TracingRuntimeStream : Stream
		{
			readonly Stream inner;
			readonly TracingRuntimeRoot root;
			readonly int ordinal;
			readonly string path;
			bool readStarted;
			bool disposed;

			public override bool CanRead => inner.CanRead;
			public override bool CanSeek => inner.CanSeek;
			public override bool CanWrite => inner.CanWrite;
			public override long Length => inner.Length;
			public override long Position
			{
				get => inner.Position;
				set => inner.Position = value;
			}

			public TracingRuntimeStream(
				Stream inner,
				TracingRuntimeRoot root,
				int ordinal,
				string path)
			{
				this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
				this.root = root ?? throw new ArgumentNullException(nameof(root));
				this.ordinal = ordinal;
				this.path = path ?? throw new ArgumentNullException(nameof(path));
			}

			public override void Flush() => inner.Flush();
			public override int Read(byte[] buffer, int offset, int count)
			{
				if (!readStarted)
				{
					readStarted = true;
					root.RecordHashRead(path);
				}

				var read = inner.Read(buffer, offset, count);
				if (read == 0)
					root.RecordHashEndOfStream(path);

				return read;
			}

			public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
			public override void SetLength(long value) => inner.SetLength(value);
			public override void Write(byte[] buffer, int offset, int count) =>
				inner.Write(buffer, offset, count);

			protected override void Dispose(bool disposing)
			{
				if (!disposing)
				{
					base.Dispose(disposing);
					return;
				}

				root.RecordStreamDisposeAttempt(path);
				if (disposed)
					throw new AssertionException(
						$"Stream for '{path}' was disposed more than once.");

				disposed = true;
				try
				{
					inner.Dispose();
				}
				finally
				{
					try
					{
						root.RecordStreamDisposed(ordinal, path);
					}
					finally
					{
						base.Dispose(disposing);
					}
				}
			}
		}

		sealed class RecordingRuntimeRoot : IIosRuntimeResourceRoot
		{
			readonly IReadOnlyDictionary<string, byte[]> payloads;

			public List<string> AcquiredPaths { get; } = new();
			public List<string> VerifiedPaths { get; } = new();
			public List<string> DisposedPaths { get; } = new();
			public Func<int, byte[], byte[]> PayloadTransform { get; set; }
			public Func<int, string, string> LeasePathTransform { get; set; }
			public Action<int> StreamFirstRead { get; set; }
			public Action<int> StreamDisposed { get; set; }
			public Action<int> LeaseVerified { get; set; }

			public RecordingRuntimeRoot(IReadOnlyDictionary<string, byte[]> payloads)
			{
				this.payloads = payloads;
			}

			public RetailOperationResult<IIosRuntimeResourceLease> AcquireRegularFile(
				string canonicalRelativePath)
			{
				if (!payloads.TryGetValue(canonicalRelativePath, out var payload))
					return RetailOperationResult<IIosRuntimeResourceLease>.Failed(
						RuntimeResourcesMissing());

				var ordinal = AcquiredPaths.Count;
				AcquiredPaths.Add(canonicalRelativePath);
				var transformed = PayloadTransform?.Invoke(ordinal, payload) ?? payload;
				var leasePath = LeasePathTransform?.Invoke(ordinal, canonicalRelativePath) ??
					canonicalRelativePath;
				return RetailOperationResult<IIosRuntimeResourceLease>.Success(
					new RecordingRuntimeLease(this, ordinal, leasePath, transformed));
			}

			public void Dispose() { }

			public void RecordVerified(int ordinal, string path)
			{
				VerifiedPaths.Add(path);
				LeaseVerified?.Invoke(ordinal);
			}

			public void RecordDisposed(string path) => DisposedPaths.Add(path);

			static RetailImportFailure RuntimeResourcesMissing() => new(
				RetailImportErrorCode.RuntimeResourcesMissing,
				null,
				null,
				RetailImportDiagnosticCode.InvalidMapPackage);
		}

		sealed class RecordingRuntimeLease : IIosRuntimeResourceLease
		{
			readonly RecordingRuntimeRoot root;
			readonly int ordinal;
			readonly byte[] payload;
			bool opened;
			bool disposed;

			public string CanonicalRelativePath { get; }
			public long Length => payload.LongLength;

			public RecordingRuntimeLease(
				RecordingRuntimeRoot root,
				int ordinal,
				string canonicalRelativePath,
				byte[] payload)
			{
				this.root = root;
				this.ordinal = ordinal;
				CanonicalRelativePath = canonicalRelativePath;
				this.payload = payload;
			}

			public Stream OpenVerifiedRead()
			{
				if (disposed)
					throw new ObjectDisposedException(nameof(RecordingRuntimeLease));
				if (opened)
					throw new InvalidOperationException("The test lease is one-shot.");

				opened = true;
				return new CallbackMemoryStream(
					payload,
					() => root.StreamFirstRead?.Invoke(ordinal),
					() => root.StreamDisposed?.Invoke(ordinal));
			}

			public void VerifyUnchanged()
			{
				if (disposed)
					throw new ObjectDisposedException(nameof(RecordingRuntimeLease));

				root.RecordVerified(ordinal, CanonicalRelativePath);
			}

			public void Dispose()
			{
				if (disposed)
					return;

				disposed = true;
				root.RecordDisposed(CanonicalRelativePath);
			}
		}

		sealed class CallbackMemoryStream : MemoryStream
		{
			readonly Action firstRead;
			readonly Action disposedCallback;
			bool readStarted;

			public CallbackMemoryStream(byte[] payload, Action firstRead, Action disposedCallback)
				: base(payload, writable: false)
			{
				this.firstRead = firstRead;
				this.disposedCallback = disposedCallback;
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				if (!readStarted)
				{
					readStarted = true;
					firstRead?.Invoke();
				}

				return base.Read(buffer, offset, count);
			}

			protected override void Dispose(bool disposing)
			{
				if (disposing)
					disposedCallback?.Invoke();

				base.Dispose(disposing);
			}
		}
	}

	[TestFixture]
	public sealed class RetailCandidateProfileValidationTestRa2MapImportContext
	{
		const string MinimalManifest =
			"Metadata:\n" +
			"\tTitle: Test\n" +
			"\tVersion: test\n" +
			"\tWebsite: https://example.invalid\n" +
			"\tWebIcon32: icon.png\n" +
			"\tWindowTitle: Test\n" +
			"FileSystem: DefaultFileSystem\n" +
			"LoadScreen: BlankLoadScreen\n" +
			"Assemblies: OpenRA.Mods.Common.dll\n" +
			"TerrainFormat: DefaultTerrain\n" +
			"SpriteSequenceFormat: DefaultSpriteSequence\n";

		[Test]
		public void FailedProfileMatchConsumesAndDisposesThePassedScopeExactlyOnce()
		{
			using var fixture = new ProfileScopeFixture(Ra2RuntimeProfileId.Base);
			var scope = fixture.TakeScope();
			var result = Ra2MapImportContext.Create(
				scope,
				Ra2RuntimeProfileId.Yuri,
				_ => null);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.False);
				Assert.That(result.Failure.Code,
					Is.EqualTo(RetailImportErrorCode.ProfileContextFailure));
				Assert.That(result.Failure.Diagnostic,
					Is.EqualTo(RetailImportDiagnosticCode.InvalidMapPackage));
				Assert.That(scope.ModData, Is.Null);
				Assert.That(fixture.Service.DisposeCalls, Is.EqualTo(1));
			});

			scope.Dispose();
			Assert.That(fixture.Service.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void ImportContextTakesScopeBeforeReturnAndCallerDisposalIsInert()
		{
			using var fixture = new ProfileScopeFixture(Ra2RuntimeProfileId.Base);
			var scope = fixture.TakeScope();
			var expectedModData = scope.ModData;
			Func<RetailStagingTarget, IReadWritePackage> outputFactory = _ => null;
			var result = Ra2MapImportContext.Create(
				scope,
				Ra2RuntimeProfileId.Base,
				outputFactory);

			Assert.That(result.Succeeded, Is.True);
			var context = result.Value;
			Assert.Multiple(() =>
			{
				Assert.That(context.Profile, Is.EqualTo(Ra2RuntimeProfileId.Base));
				Assert.That(context.ModData, Is.SameAs(expectedModData));
				Assert.That(context.CreateOutputPackage, Is.SameAs(outputFactory));
				Assert.That(scope.ModData, Is.Null);
				Assert.That(fixture.Service.DisposeCalls, Is.Zero);
			});

			scope.Dispose();
			Assert.Multiple(() =>
			{
				Assert.That(context.ModData, Is.SameAs(expectedModData));
				Assert.That(context.CreateOutputPackage, Is.SameAs(outputFactory));
				Assert.That(fixture.Service.DisposeCalls, Is.Zero);
			});

			context.Dispose();
			context.Dispose();
			Assert.Multiple(() =>
			{
				Assert.That(fixture.Service.DisposeCalls, Is.EqualTo(1));
				Assert.Throws<ObjectDisposedException>(() => _ = context.ModData);
				Assert.Throws<ObjectDisposedException>(() => _ = context.CreateOutputPackage);
			});
		}

		[Test]
		public void ValidationContextTakesScopeBeforeReturnAndCallerDisposalIsInert()
		{
			using var fixture = new ProfileScopeFixture(Ra2RuntimeProfileId.Base);
			var scope = fixture.TakeScope();
			var expectedModData = scope.ModData;
			var result = Ra2MapValidationContext.Create(
				scope,
				Ra2RuntimeProfileId.Base);

			Assert.That(result.Succeeded, Is.True);
			var context = result.Value;
			Assert.Multiple(() =>
			{
				Assert.That(context.Profile, Is.EqualTo(Ra2RuntimeProfileId.Base));
				Assert.That(context.ModData, Is.SameAs(expectedModData));
				Assert.That(scope.ModData, Is.Null);
				Assert.That(fixture.Service.DisposeCalls, Is.Zero);
			});

			scope.Dispose();
			Assert.Multiple(() =>
			{
				Assert.That(context.ModData, Is.SameAs(expectedModData));
				Assert.That(fixture.Service.DisposeCalls, Is.Zero);
			});

			context.Dispose();
			context.Dispose();
			Assert.Multiple(() =>
			{
				Assert.That(fixture.Service.DisposeCalls, Is.EqualTo(1));
				Assert.Throws<ObjectDisposedException>(() => _ = context.ModData);
			});
		}

		[Test]
		public void ImportContextRejectsUseAfterTransferWithoutDisturbingTheOwner()
		{
			using var fixture = new ProfileScopeFixture(Ra2RuntimeProfileId.Base);
			var scope = fixture.TakeScope();
			var expectedModData = scope.ModData;
			var first = Ra2MapImportContext.Create(
				scope,
				Ra2RuntimeProfileId.Base,
				_ => null);

			Assert.That(first.Succeeded, Is.True);
			var second = Ra2MapImportContext.Create(
				scope,
				Ra2RuntimeProfileId.Base,
				_ => null);

			Assert.That(second.Succeeded, Is.False);
			Assert.Multiple(() =>
			{
				AssertClosedContextFailure(second.Failure);
				Assert.That(first.Value.ModData, Is.SameAs(expectedModData));
				Assert.That(fixture.Service.DisposeCalls, Is.Zero);
			});

			first.Value.Dispose();
			Assert.That(fixture.Service.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void ValidationContextRejectsUseAfterTransferWithoutDisturbingTheOwner()
		{
			using var fixture = new ProfileScopeFixture(Ra2RuntimeProfileId.Base);
			var scope = fixture.TakeScope();
			var expectedModData = scope.ModData;
			var first = Ra2MapValidationContext.Create(
				scope,
				Ra2RuntimeProfileId.Base);

			Assert.That(first.Succeeded, Is.True);
			var second = Ra2MapValidationContext.Create(
				scope,
				Ra2RuntimeProfileId.Base);

			Assert.That(second.Succeeded, Is.False);
			Assert.Multiple(() =>
			{
				AssertClosedContextFailure(second.Failure);
				Assert.That(first.Value.ModData, Is.SameAs(expectedModData));
				Assert.That(fixture.Service.DisposeCalls, Is.Zero);
			});

			first.Value.Dispose();
			Assert.That(fixture.Service.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void ConcurrentImportFactoriesReturnOneOwnerAndClosedFailures()
		{
			using var fixture = new ProfileScopeFixture(Ra2RuntimeProfileId.Base);
			AssertConcurrentFactoryOwnership(fixture, CreateImportContext);
		}

		[Test]
		public void ConcurrentValidationFactoriesReturnOneOwnerAndClosedFailures()
		{
			using var fixture = new ProfileScopeFixture(Ra2RuntimeProfileId.Base);
			AssertConcurrentFactoryOwnership(fixture, CreateValidationContext);
		}

		[Test]
		public void ConcurrentCallerDisposalAndBothFactoriesReturnOnlyClosedOutcomes()
		{
			const int WorkerCount = 16;
			using var fixture = new ProfileScopeFixture(Ra2RuntimeProfileId.Base);
			var scope = fixture.TakeScope();
			var results = new OwnedContextFactoryResult[WorkerCount];
			using var start = new Barrier(WorkerCount + 2);
			var workers = Enumerable.Range(0, WorkerCount).Select(index =>
				Task.Factory.StartNew(() =>
				{
					start.SignalAndWait();
					results[index] = index % 2 == 0
						? CreateImportContext(scope)
						: CreateValidationContext(scope);
				}, CancellationToken.None, TaskCreationOptions.LongRunning,
					TaskScheduler.Default)).ToArray();
			var disposer = Task.Factory.StartNew(() =>
			{
				start.SignalAndWait();
				scope.Dispose();
			}, CancellationToken.None, TaskCreationOptions.LongRunning,
				TaskScheduler.Default);

			start.SignalAndWait();
			Task.WaitAll(workers.Append(disposer).ToArray());
			var owners = results.Where(result => result.Owner != null).ToArray();
			var failures = results.Where(result => result.Failure != null).ToArray();
			foreach (var owner in owners)
				owner.Owner.Dispose();

			Assert.Multiple(() =>
			{
				Assert.That(owners.Length, Is.LessThanOrEqualTo(1));
				Assert.That(owners.Length + failures.Length, Is.EqualTo(WorkerCount));
				foreach (var failure in failures)
					AssertClosedContextFailure(failure.Failure);
				Assert.That(scope.ModData, Is.Null);
				Assert.That(fixture.Service.DisposeCalls, Is.EqualTo(1));
			});
		}

		[Test]
		public void BoundValidationContextReleasesOnlyItsPinAndKeepsBorrowedModDataAlive()
		{
			using var fixture = new ProfileScopeFixture(Ra2RuntimeProfileId.Base);
			var scope = fixture.Scope;
			var borrowed = scope.ModData;
			var pin = new CountingPin();
			var result = Ra2MapValidationContext.CreateBound(
				borrowed,
				pin,
				Ra2RuntimeProfileId.Base);

			Assert.That(result.Succeeded, Is.True);
			var context = result.Value;
			context.Dispose();
			context.Dispose();

			Assert.Multiple(() =>
			{
				Assert.That(pin.DisposeCalls, Is.EqualTo(1));
				Assert.That(fixture.Service.DisposeCalls, Is.Zero);
				Assert.That(scope.ModData, Is.SameAs(borrowed));
				Assert.Throws<ObjectDisposedException>(() => _ = context.ModData);
			});

			fixture.Dispose();
			Assert.That(fixture.Service.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void InvalidBoundContextDisposesThePassedPinExactlyOnce()
		{
			using var fixture = new ProfileScopeFixture(Ra2RuntimeProfileId.Base);
			var pin = new CountingPin();
			var result = Ra2MapValidationContext.CreateBound(
				fixture.Scope.ModData,
				pin,
				Ra2RuntimeProfileId.Yuri);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.False);
				Assert.That(pin.DisposeCalls, Is.EqualTo(1));
				Assert.That(fixture.Service.DisposeCalls, Is.Zero);
				Assert.That(fixture.Scope.ModData, Is.Not.Null);
			});
		}

		[Test]
		public void ConcurrentBoundContextDisposeReleasesEachPinExactlyOnce()
		{
			const int WorkerCount = 16;
			const int Rounds = 128;
			using var fixture = new ProfileScopeFixture(Ra2RuntimeProfileId.Base);
			using var barrier = new Barrier(WorkerCount + 1);
			Ra2MapValidationContext current = null;
			var pins = new StrictPin[Rounds];
			var workers = Enumerable.Range(0, WorkerCount).Select(_ =>
				Task.Factory.StartNew(() =>
				{
					for (var round = 0; round < Rounds; round++)
					{
						barrier.SignalAndWait();
						Volatile.Read(ref current).Dispose();
						barrier.SignalAndWait();
					}
				}, CancellationToken.None, TaskCreationOptions.LongRunning,
					TaskScheduler.Default)).ToArray();

			for (var round = 0; round < Rounds; round++)
			{
				var pin = new StrictPin();
				pins[round] = pin;
				var created = Ra2MapValidationContext.CreateBound(
					fixture.Scope.ModData,
					pin,
					Ra2RuntimeProfileId.Base);
				Assert.That(created.Succeeded, Is.True);
				Volatile.Write(ref current, created.Value);
				barrier.SignalAndWait();
				barrier.SignalAndWait();
			}

			Task.WaitAll(workers);
			Assert.That(pins.Select(pin => pin.DisposeCalls), Is.All.EqualTo(1));
		}

		[Test]
		public void MetadataSortsOwnedRelationshipsAndPlayersButPreservesSpawnOrder()
		{
			var zulu = Player(
				"Zulu",
				ImmutableArray.Create("Middle", "Alpha"),
				ImmutableArray<string>.Empty,
				new CPos(3, 3));
			var alpha = Player(
				"Alpha",
				ImmutableArray<string>.Empty,
				ImmutableArray.Create("Zulu"),
				CPos.Zero);
			var middle = Player(
				"Middle",
				ImmutableArray<string>.Empty,
				ImmutableArray<string>.Empty,
				new CPos(4, 4));
			var spawns = ImmutableArray.Create(new CPos(6, 6), new CPos(2, 2));

			var metadata = ValidatedMapMetadata.CreateValidated(
				"Title",
				"Author",
				ImmutableArray.Create("Skirmish", "Standard"),
				"TEMPERATE",
				MapGridType.Rectangular,
				new int2(64, 64),
				new Rectangle(0, 0, 64, 64),
				ImmutableArray.Create(zulu, middle, alpha),
				spawns);

			Assert.Multiple(() =>
			{
				Assert.That(metadata.Players.Select(player => player.Name),
					Is.EqualTo(new[] { "Alpha", "Middle", "Zulu" }));
				Assert.That(metadata.Players[2].Allies,
					Is.EqualTo(new[] { "Alpha", "Middle" }));
				Assert.That(metadata.Categories,
					Is.EqualTo(new[] { "Skirmish", "Standard" }));
				Assert.That(metadata.SpawnPoints, Is.EqualTo(spawns));
				Assert.That(metadata.PlayablePlayers, Is.EqualTo(3));
			});
		}

		[Test]
		public void MetadataRejectsCaseAliasesDanglingReferencesAndInvalidGridCells()
		{
			var alpha = Player(
				"Alpha", ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, CPos.Zero);
			var alias = Player(
				"alpha", ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, CPos.Zero);
			var dangling = Player(
				"Solo", ImmutableArray.Create("Missing"), ImmutableArray<string>.Empty, CPos.Zero);

			Assert.Multiple(() =>
			{
				Assert.Throws<ArgumentException>(() => Metadata(
					MapGridType.Rectangular,
					ImmutableArray.Create(alpha, alias),
					ImmutableArray<CPos>.Empty));
				Assert.Throws<ArgumentException>(() => Metadata(
					MapGridType.Rectangular,
					ImmutableArray.Create(dangling),
					ImmutableArray<CPos>.Empty));
				Assert.Throws<ArgumentException>(() => Metadata(
					MapGridType.RectangularIsometric,
					ImmutableArray.Create(alpha),
					ImmutableArray.Create(new CPos(1, 2))));
				Assert.Throws<ArgumentException>(() => Metadata(
					MapGridType.Rectangular,
					ImmutableArray.Create(alpha),
					ImmutableArray.Create(new CPos(2, 2, 1))));
			});
		}

		static ValidatedMapMetadata Metadata(
			MapGridType gridType,
			ImmutableArray<ValidatedMapPlayerMetadata> players,
			ImmutableArray<CPos> spawns) =>
			ValidatedMapMetadata.CreateValidated(
				"Title",
				"Author",
				ImmutableArray<string>.Empty,
				"TEMPERATE",
				gridType,
				new int2(64, 64),
				new Rectangle(0, 0, 64, 64),
				players,
				spawns);

		static void AssertConcurrentFactoryOwnership(
			ProfileScopeFixture fixture,
			Func<PreGameModDataScope, OwnedContextFactoryResult> factory)
		{
			const int WorkerCount = 16;
			var scope = fixture.TakeScope();
			var results = new OwnedContextFactoryResult[WorkerCount];
			using var start = new Barrier(WorkerCount + 1);
			var workers = Enumerable.Range(0, WorkerCount).Select(index =>
				Task.Factory.StartNew(() =>
				{
					start.SignalAndWait();
					results[index] = factory(scope);
				}, CancellationToken.None, TaskCreationOptions.LongRunning,
					TaskScheduler.Default)).ToArray();

			start.SignalAndWait();
			Task.WaitAll(workers);
			var owners = results.Where(result => result.Owner != null).ToArray();
			var failures = results.Where(result => result.Failure != null).ToArray();
			scope.Dispose();
			foreach (var owner in owners)
				owner.Owner.Dispose();

			Assert.Multiple(() =>
			{
				Assert.That(owners.Length, Is.EqualTo(1));
				Assert.That(failures.Length, Is.EqualTo(WorkerCount - 1));
				foreach (var failure in failures)
					AssertClosedContextFailure(failure.Failure);
				Assert.That(scope.ModData, Is.Null);
				Assert.That(fixture.Service.DisposeCalls, Is.EqualTo(1));
			});
		}

		static OwnedContextFactoryResult CreateImportContext(PreGameModDataScope scope)
		{
			var result = Ra2MapImportContext.Create(
				scope,
				Ra2RuntimeProfileId.Base,
				_ => null);
			return result.Succeeded
				? OwnedContextFactoryResult.Succeeded(result.Value)
				: OwnedContextFactoryResult.Failed(result.Failure);
		}

		static OwnedContextFactoryResult CreateValidationContext(PreGameModDataScope scope)
		{
			var result = Ra2MapValidationContext.Create(
				scope,
				Ra2RuntimeProfileId.Base);
			return result.Succeeded
				? OwnedContextFactoryResult.Succeeded(result.Value)
				: OwnedContextFactoryResult.Failed(result.Failure);
		}

		static void AssertClosedContextFailure(RetailImportFailure failure)
		{
			Assert.Multiple(() =>
			{
				Assert.That(failure.Code,
					Is.EqualTo(RetailImportErrorCode.ProfileContextFailure));
				Assert.That(failure.Group, Is.Null);
				Assert.That(failure.SafeBasename, Is.Null);
				Assert.That(failure.Diagnostic,
					Is.EqualTo(RetailImportDiagnosticCode.InvalidMapPackage));
			});
		}

		static ValidatedMapPlayerMetadata Player(
			string name,
			ImmutableArray<string> allies,
			ImmutableArray<string> enemies,
			CPos home) =>
			ValidatedMapPlayerMetadata.CreateValidated(
				name,
				null,
				null,
				null,
				allowBots: true,
				playable: true,
				required: false,
				ownsWorld: false,
				spectating: false,
				nonCombatant: false,
				lockFaction: false,
				faction: null,
				lockColor: false,
				color: default,
				homeLocation: home,
				lockSpawn: false,
				spawn: 0,
				lockTeam: false,
				team: 0,
				lockHandicap: false,
				handicap: 0,
				allies,
				enemies);

		interface IMapContextOwnedService { }

		sealed class CountingOwnedService : IMapContextOwnedService, IDisposable
		{
			public int DisposeCalls { get; private set; }
			public void Dispose() { DisposeCalls++; }
		}

		sealed class CountingPin : IDisposable
		{
			public int DisposeCalls { get; private set; }
			public void Dispose() { DisposeCalls++; }
		}

		sealed class StrictPin : IDisposable
		{
			int disposeCalls;
			public int DisposeCalls => Volatile.Read(ref disposeCalls);
			public void Dispose() => Interlocked.Increment(ref disposeCalls);
		}

		sealed class OwnedContextFactoryResult
		{
			public IDisposable Owner { get; }
			public RetailImportFailure Failure { get; }

			OwnedContextFactoryResult(IDisposable owner, RetailImportFailure failure)
			{
				Owner = owner;
				Failure = failure;
			}

			public static OwnedContextFactoryResult Succeeded(IDisposable owner) =>
				new(owner, null);

			public static OwnedContextFactoryResult Failed(RetailImportFailure failure) =>
				new(null, failure);
		}

		sealed class ProfileScopeFixture : IDisposable
		{
			readonly TemporaryDirectory directory;
			readonly PreGameEngineContext context;

			public CountingOwnedService Service { get; } = new();
			public PreGameModDataScope Scope { get; private set; }

			public ProfileScopeFixture(Ra2RuntimeProfileId profile)
			{
				directory = new TemporaryDirectory("openra-map-context");
				var modsRoot = Path.Combine(directory.Path, "mods");
				var modRoot = Path.Combine(modsRoot, "ra2");
				Directory.CreateDirectory(modRoot);
				File.WriteAllText(Path.Combine(modRoot, "mod.yaml"), MinimalManifest);
				File.WriteAllText(Path.Combine(modRoot, "mod.ios-base.yaml"), MinimalManifest);
				File.WriteAllText(Path.Combine(modRoot, "mod.ios-yuri.yaml"), MinimalManifest);
				var configuration = new EngineBootstrapConfiguration(
					Platform.EngineDir,
					Platform.SupportDir,
					ImmutableArray.Create(modsRoot),
					ImmutableArray<string>.Empty,
					new Arguments());
				context = PreGameEngineContext.Open(configuration);
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<IMapContextOwnedService>(Service))), Is.True);
				using var options = collection.Seal(
					loader => loader,
					context.Settings,
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				Scope = context.OpenModData(profile.ManifestSelection(), options);
			}

			public PreGameModDataScope TakeScope()
			{
				var current = Scope;
				Scope = null;
				return current;
			}

			public void Dispose()
			{
				var current = Scope;
				Scope = null;
				try
				{
					current?.Dispose();
				}
				finally
				{
					context?.Dispose();
					directory?.Dispose();
				}
			}
		}
	}

	sealed class TemporaryDirectory : IDisposable
	{
		public string Path { get; }

		public TemporaryDirectory(string prefix)
		{
			Path = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
			Directory.CreateDirectory(Path);
		}

		public void Dispose()
		{
			if (Directory.Exists(Path))
				Directory.Delete(Path, recursive: true);
		}
	}
}
