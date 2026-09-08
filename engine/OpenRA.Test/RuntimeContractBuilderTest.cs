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
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using OpenRA.FileSystem;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RuntimeContractBuilderTest
	{
		[Test]
		public void LegacyManifestHasNoRuntimeContractFiles()
		{
			using var package = new TestPackage(("mod.yaml", Encoding.UTF8.GetBytes(MinimalManifest())));
			using var manifest = new Manifest("ra2", package);

			Assert.That(manifest.RuntimeContractFiles, Is.Empty);
		}

		[TestCase(Ra2RuntimeProfileId.Base, "mod.ios-base.yaml", 146)]
		[TestCase(Ra2RuntimeProfileId.Yuri, "mod.ios-yuri.yaml", 153)]
		public void ProfiledManifestFilesMatchTheAuthenticatedContractProjection(
			Ra2RuntimeProfileId profile, string entryName, int expectedCount)
		{
			var repository = RepositoryRoot();
			using var package = new Folder(Path.Combine(repository, "mods", "ra2"));
			using var manifest = new Manifest("ra2", package, entryName);
			var expected = GoldenContractLogicalNames(repository, profile);

			Assert.Multiple(() =>
			{
				Assert.That(manifest.RuntimeContractFiles, Has.Length.EqualTo(expectedCount));
				Assert.That(manifest.RuntimeContractFiles, Is.EqualTo(expected));
				Assert.That(manifest.RuntimeContractFiles,
					Is.Ordered.Using<string>(StringComparer.Ordinal));
			});
		}

		[TestCase("mod.ios-base.yaml")]
		[TestCase("mod.ios-yuri.yaml")]
		public void ProfiledMapCompatibilityIsCanonicalAndContainsTheModExactlyOnce(string entryName)
		{
			using var package = new Folder(Path.Combine(RepositoryRoot(), "mods", "ra2"));
			using var manifest = new Manifest("ra2", package, entryName);

			Assert.That(manifest.MapCompatibility, Is.EqualTo(new[] { "ra2" }));
		}

		[Test]
		public void ProfiledProjectionCanonicalizesDeclaredAliasesAndFluentCultures()
		{
			var yaml = MinimalManifest(
				"\tSystemPackages:\n" +
				"\t\t$ra2: ra2\n" +
				"\t\t^EngineDir|mods/common: common\n",
				"Rules:\n" +
				"\trules/local.yaml:\n" +
				"\tra2|rules/profile.yaml:\n" +
				"\tcommon|rules/shared.yaml:\n" +
				"FluentMessages:\n" +
				"\tra2|fluent/mod.ftl:\n" +
				"RuntimeContractCultures:\n" +
				"\tzh-CN:\n");
			using var package = new TestPackage(("mod.ios-base.yaml", Encoding.UTF8.GetBytes(yaml)));
			using var manifest = new Manifest("ra2", package, "mod.ios-base.yaml");

			Assert.That(manifest.RuntimeContractFiles, Is.EqualTo(new[]
			{
				"mods/common/rules/shared.yaml",
				"mods/ra2/fluent/mod.ftl",
				"mods/ra2/fluent/zh-CN/mod.ftl",
				"mods/ra2/mod.ios-base.yaml",
				"mods/ra2/rules/local.yaml",
				"mods/ra2/rules/profile.yaml",
			}));
		}

		[Test]
		public void RuntimeContractCulturesAndFluentSiblingsUseCanonicalUtf8Order()
		{
			var yaml = MinimalManifest(
				"\tSystemPackages:\n\t\t$ra2: ra2\n",
				"FluentMessages:\n" +
				"\tra2|fluent/mod.ftl:\n" +
				"RuntimeContractCultures:\n" +
				"\tzh-CN:\n" +
				"\ten-US:\n");
			using var package = new TestPackage(("mod.ios-base.yaml", Encoding.UTF8.GetBytes(yaml)));
			using var manifest = new Manifest("ra2", package, "mod.ios-base.yaml");

			Assert.Multiple(() =>
			{
				Assert.That(manifest.RuntimeContractCultures.ToArray(),
					Is.EqualTo(new[] { "en-US", "zh-CN" }));
				Assert.That(manifest.RuntimeContractFiles, Is.EqualTo(new[]
				{
					"mods/ra2/fluent/en-US/mod.ftl",
					"mods/ra2/fluent/mod.ftl",
					"mods/ra2/fluent/zh-CN/mod.ftl",
					"mods/ra2/mod.ios-base.yaml",
				}));
			});
		}

		[TestCase("Rules:\n\tunknown|rules/base.yaml:\n")]
		[TestCase("Rules:\n\tra2|../escape.yaml:\n")]
		[TestCase("Rules:\n\t~ra2|rules/base.yaml:\n")]
		[TestCase("Rules:\n\trules/base.yaml:\n\trules/base.yaml:\n")]
		[TestCase("Rules:\n\trules/base.yaml:\n\trules/Base.yaml:\n")]
		public void ProfiledProjectionRejectsAliasEscapeOptionalAndDuplicatePaths(string rows)
		{
			var yaml = MinimalManifest(
				"\tSystemPackages:\n\t\t$ra2: ra2\n",
				rows + "RuntimeContractCultures:\n\tzh-CN:\n");
			using var package = new TestPackage(("mod.ios-base.yaml", Encoding.UTF8.GetBytes(yaml)));

			Assert.Throws<YamlException>(() => new Manifest("ra2", package, "mod.ios-base.yaml"));
		}

		[Test]
		public void ProfiledProjectionRejectsADeclaredRetailContentAlias()
		{
			var yaml = MinimalManifest(
				"\tSystemPackages:\n\t\t$ra2: ra2\n\t\tcontent: content\n",
				"Rules:\n\tcontent|rules/base.yaml:\n" +
				"RuntimeContractCultures:\n\tzh-CN:\n");
			using var package = new TestPackage(("mod.ios-base.yaml", Encoding.UTF8.GetBytes(yaml)));

			Assert.Throws<YamlException>(() => new Manifest("ra2", package, "mod.ios-base.yaml"));
		}

		[Test]
		public void DescriptorKeepsLogicalIdentitySeparateFromBundleOpenPath()
		{
			var descriptor = new RuntimeContractResourceDescriptor(
				"mods/ra2/rules/world.yaml",
				"bundle/runtime/world.yaml",
				1,
				new string('a', 64),
				ImmutableArray.Create(Ra2RuntimeProfileId.Base),
				RuntimeContractResourcePurpose.Contract);

			Assert.Multiple(() =>
			{
				Assert.That(descriptor.LogicalName, Is.EqualTo("mods/ra2/rules/world.yaml"));
				Assert.That(descriptor.CanonicalBundleRelativePath,
					Is.EqualTo("bundle/runtime/world.yaml"));
			});
		}

		[Test]
		public void ManagedImageReaderUsesOnePinnedLeasePerOrdinalImageAndReleasesInReverse()
		{
			var bytes = File.ReadAllBytes(typeof(Manifest).Assembly.Location);
			var moduleMvid = ReadMvid(bytes);
			var source = new RecordingManagedImageSource(ProjectImageNames.Reverse(), bytes, moduleMvid);
			var clock = new TestClock();
			var reader = new RuntimeBuildIdentityReader(source, new string('b', 64));
			var result = reader.Capture(
				RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
				clock,
				CancellationToken.None);

			Assert.That(result.Succeeded, Is.True);
			Assert.Multiple(() =>
			{
				Assert.That(result.Value.ManagedImages.Select(image => image.BundleFilename),
					Is.EqualTo(ProjectImageNames));
				Assert.That(result.Value.ManagedImages.All(image =>
					image.ModuleMvid == moduleMvid), Is.True);
				Assert.That(source.Acquired, Is.EqualTo(ProjectImageNames));
				Assert.That(source.Opened, Is.EqualTo(ProjectImageNames));
				Assert.That(source.Verified, Is.EqualTo(ProjectImageNames));
				Assert.That(source.Disposed, Is.EqualTo(ProjectImageNames.Reverse()));
				Assert.That(source.EnumerationCount, Is.EqualTo(2));
			});
		}

		[Test]
		public void ManagedImageReaderMapsSecondEnumerationDriftToClosedResourceFailure()
		{
			var bytes = File.ReadAllBytes(typeof(Manifest).Assembly.Location);
			var source = new RecordingManagedImageSource(
				ProjectImageNames, bytes, ReadMvid(bytes))
			{
				SecondEnumeration = ProjectImageNames.Take(ProjectImageNames.Length - 1).ToImmutableArray(),
			};
			var clock = new TestClock();
			var result = new RuntimeBuildIdentityReader(source, new string('b', 64)).Capture(
				RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
				clock,
				CancellationToken.None);

			AssertClosedFailure(result, RetailImportErrorCode.RuntimeResourcesMissing,
				RetailImportDiagnosticCode.InvalidMapPackage);
			Assert.That(source.Disposed, Is.EqualTo(ProjectImageNames.Reverse()));
		}

		[Test]
		public void ManagedImageReaderPrefersCancellationAfterSecondEnumerationAndCleansInReverse()
		{
			var bytes = File.ReadAllBytes(typeof(Manifest).Assembly.Location);
			using var cancellation = new CancellationTokenSource();
			var source = new RecordingManagedImageSource(
				ProjectImageNames, bytes, ReadMvid(bytes))
			{
				SecondEnumeration = ImmutableArray<string>.Empty,
				AfterEnumeration = count =>
				{
					if (count == 2)
						cancellation.Cancel();
				},
			};
			var clock = new TestClock();
			var result = new RuntimeBuildIdentityReader(source, new string('b', 64)).Capture(
				RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
				clock,
				cancellation.Token);

			AssertClosedFailure(result, RetailImportErrorCode.Cancelled,
				RetailImportDiagnosticCode.None);
			Assert.That(source.Disposed, Is.EqualTo(ProjectImageNames.Reverse()));
		}

		[Test]
		public void ManagedImageReaderPrefersExpiryAfterNullAcquisitionAndCleansEarlierLease()
		{
			var bytes = File.ReadAllBytes(typeof(Manifest).Assembly.Location);
			var clock = new TestClock();
			var source = new RecordingManagedImageSource(
				ProjectImageNames, bytes, ReadMvid(bytes))
			{
				NullAcquireNumber = 2,
				AfterAcquire = count =>
				{
					if (count == 2)
						clock.Expired = true;
				},
			};
			var result = new RuntimeBuildIdentityReader(source, new string('b', 64)).Capture(
				RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
				clock,
				CancellationToken.None);

			AssertClosedFailure(result, RetailImportErrorCode.ValidationTimedOut,
				RetailImportDiagnosticCode.DeadlineExceeded);
			Assert.That(source.Disposed, Is.EqualTo(new[] { ProjectImageNames[0] }));
		}

		[Test]
		public void ManagedImageReaderRetainsAnAcquiredLeaseBeforePostAcquireCancellation()
		{
			var bytes = File.ReadAllBytes(typeof(Manifest).Assembly.Location);
			using var cancellation = new CancellationTokenSource();
			var source = new RecordingManagedImageSource(
				ProjectImageNames, bytes, ReadMvid(bytes))
			{
				AfterAcquire = count =>
				{
					if (count == 2)
						cancellation.Cancel();
				},
			};
			var clock = new TestClock();
			var result = new RuntimeBuildIdentityReader(source, new string('b', 64)).Capture(
				RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
				clock,
				cancellation.Token);

			AssertClosedFailure(result, RetailImportErrorCode.Cancelled,
				RetailImportDiagnosticCode.None);
			Assert.That(source.Disposed,
				Is.EqualTo(ProjectImageNames.Take(2).Reverse()));
		}

		[TestCase(ManagedImageFault.AcquireNull)]
		[TestCase(ManagedImageFault.AcquireThrow)]
		[TestCase(ManagedImageFault.AcquireOverflow)]
		[TestCase(ManagedImageFault.LeaseGetterThrow)]
		[TestCase(ManagedImageFault.LeaseGetterOverflow)]
		[TestCase(ManagedImageFault.FilenameMismatch)]
		[TestCase(ManagedImageFault.NegativeLength)]
		[TestCase(ManagedImageFault.EmptyLeaseMvid)]
		[TestCase(ManagedImageFault.LoadedMvidMismatch)]
		[TestCase(ManagedImageFault.OpenNull)]
		[TestCase(ManagedImageFault.OpenUnreadable)]
		[TestCase(ManagedImageFault.OpenNonSeekable)]
		[TestCase(ManagedImageFault.OpenThrow)]
		[TestCase(ManagedImageFault.OpenOverflow)]
		[TestCase(ManagedImageFault.ReadThrow)]
		[TestCase(ManagedImageFault.ReadOverflow)]
		[TestCase(ManagedImageFault.ShortStream)]
		[TestCase(ManagedImageFault.LongStream)]
		[TestCase(ManagedImageFault.NoPeMetadata)]
		[TestCase(ManagedImageFault.EmptyPeMvid)]
		[TestCase(ManagedImageFault.StreamMvidMismatch)]
		[TestCase(ManagedImageFault.PePositionOverflow)]
		[TestCase(ManagedImageFault.VerifyThrow)]
		[TestCase(ManagedImageFault.VerifyOverflow)]
		public void ManagedImageReaderClosesEveryNthLeaseAndStreamFault(
			ManagedImageFault fault)
		{
			var bytes = SmallManagedImageBytes();
			for (var faultNumber = 1; faultNumber <= ProjectImageNames.Length; faultNumber++)
			{
				var source = new RecordingManagedImageSource(
					ProjectImageNames, bytes, ReadMvid(bytes))
				{
					Fault = fault,
					FaultNumber = faultNumber,
				};
				var clock = new TestClock();
				RetailOperationResult<RuntimeBuildIdentity> result = null;

				Assert.DoesNotThrow(() => result = new RuntimeBuildIdentityReader(
					source, new string('b', 64)).Capture(
						RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
						clock,
						CancellationToken.None), $"{fault} at image {faultNumber}");

				AssertClosedFailure(result, RetailImportErrorCode.RuntimeResourcesMissing,
					RetailImportDiagnosticCode.InvalidMapPackage);
				Assert.That(source.Disposed, Is.EqualTo(source.Leased.AsEnumerable().Reverse()),
					$"{fault} at image {faultNumber}");
			}
		}

		[Test]
		public void ManagedImageReaderHonorsCancellationAndExpiryAtEveryCheckpoint()
		{
			var bytes = SmallManagedImageBytes();
			var mvid = ReadMvid(bytes);
			RetailOperationResult<RuntimeBuildIdentity> Capture(
				CheckpointProbeClock clock,
				CancellationToken token,
				out RecordingManagedImageSource source)
			{
				source = new RecordingManagedImageSource(ProjectImageNames, bytes, mvid);
				return new RuntimeBuildIdentityReader(source, new string('b', 64)).Capture(
					RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
					clock,
					token);
			}

			var probeClock = new CheckpointProbeClock();
			var probe = Capture(probeClock, CancellationToken.None, out var probeSource);
			Assert.That(probe.Succeeded, Is.True);
			Assert.That(probeSource.Disposed, Is.EqualTo(ProjectImageNames.Reverse()));
			var checkpointCount = probeClock.ElapsedCallCount;
			Assert.That(checkpointCount, Is.GreaterThan(ProjectImageNames.Length));

			for (var checkpoint = 1; checkpoint <= checkpointCount; checkpoint++)
			{
				var expiryClock = new CheckpointProbeClock(expireAtCheckpoint: checkpoint);
				var expired = Capture(
					expiryClock, CancellationToken.None, out var expirySource);
				AssertClosedFailure(expired, RetailImportErrorCode.ValidationTimedOut,
					RetailImportDiagnosticCode.DeadlineExceeded);
				Assert.That(expirySource.Disposed,
					Is.EqualTo(expirySource.Leased.AsEnumerable().Reverse()),
					$"expiry checkpoint {checkpoint}/{checkpointCount}");

				using var cancellation = new CancellationTokenSource();
				if (checkpoint == 1)
					cancellation.Cancel();
				var cancellationClock = new CheckpointProbeClock(
					cancelAfterCheckpoint: checkpoint - 1,
					cancellation: cancellation);
				var cancelled = Capture(
					cancellationClock, cancellation.Token, out var cancellationSource);
				AssertClosedFailure(cancelled, RetailImportErrorCode.Cancelled,
					RetailImportDiagnosticCode.None);
				Assert.That(cancellationSource.Disposed,
					Is.EqualTo(cancellationSource.Leased.AsEnumerable().Reverse()),
					$"cancellation checkpoint {checkpoint}/{checkpointCount}");
			}
		}

		[Test]
		public void ManagedImageReaderSuppressesHostileLeaseDisposalAndContinuesInReverse()
		{
			var bytes = SmallManagedImageBytes();
			var source = new RecordingManagedImageSource(
				ProjectImageNames, bytes, ReadMvid(bytes))
			{
				Fault = ManagedImageFault.DisposeThrow,
				FaultNumber = 5,
			};
			var clock = new TestClock();
			var result = new RuntimeBuildIdentityReader(source, new string('b', 64)).Capture(
				RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
				clock,
				CancellationToken.None);

			Assert.That(result.Succeeded, Is.True);
			Assert.That(source.Disposed, Is.EqualTo(ProjectImageNames.Reverse()));
		}

		[TestCase(SecondInventoryMutation.Add)]
		[TestCase(SecondInventoryMutation.Remove)]
		[TestCase(SecondInventoryMutation.Rename)]
		[TestCase(SecondInventoryMutation.CaseAlias)]
		[TestCase(SecondInventoryMutation.Empty)]
		[TestCase(SecondInventoryMutation.Throw)]
		public void ManagedImageReaderRejectsEverySecondEnumerationDrift(
			SecondInventoryMutation mutation)
		{
			var bytes = SmallManagedImageBytes();
			var source = new RecordingManagedImageSource(
				ProjectImageNames, bytes, ReadMvid(bytes));
			switch (mutation)
			{
				case SecondInventoryMutation.Add:
					source.SecondEnumeration = ProjectImageNames.Add("Unexpected.Framework.dll");
					break;
				case SecondInventoryMutation.Remove:
					source.SecondEnumeration = ProjectImageNames.RemoveAt(0);
					break;
				case SecondInventoryMutation.Rename:
					source.SecondEnumeration = ProjectImageNames.SetItem(0, "Renamed.Framework.dll");
					break;
				case SecondInventoryMutation.CaseAlias:
					source.SecondEnumeration = ProjectImageNames.Add("openra.game.dll");
					break;
				case SecondInventoryMutation.Empty:
					source.SecondEnumeration = ImmutableArray<string>.Empty;
					break;
				case SecondInventoryMutation.Throw:
					source.ThrowEnumerationNumber = 2;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(mutation));
			}

			var clock = new TestClock();
			var result = new RuntimeBuildIdentityReader(source, new string('b', 64)).Capture(
				RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
				clock,
				CancellationToken.None);

			AssertClosedFailure(result, RetailImportErrorCode.RuntimeResourcesMissing,
				RetailImportDiagnosticCode.InvalidMapPackage);
			Assert.That(source.Disposed, Is.EqualTo(ProjectImageNames.Reverse()));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void ManagedImageReaderRejectsEmptyOrThrowingFirstEnumeration(bool throws)
		{
			var bytes = SmallManagedImageBytes();
			var source = new RecordingManagedImageSource(
				throws ? ProjectImageNames : ImmutableArray<string>.Empty,
				bytes,
				ReadMvid(bytes))
			{
				ThrowEnumerationNumber = throws ? 1 : 0,
			};
			var clock = new TestClock();
			var result = new RuntimeBuildIdentityReader(source, new string('b', 64)).Capture(
				RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
				clock,
				CancellationToken.None);

			AssertClosedFailure(result, RetailImportErrorCode.RuntimeResourcesMissing,
				RetailImportDiagnosticCode.InvalidMapPackage);
			Assert.That(source.Acquired, Is.Empty);
		}

		[Test]
		public void ManagedImageReaderPrefersCancellationTriggeredByFailedPeStreamDisposal()
		{
			var bytes = SmallManagedImageBytes();
			using var cancellation = new CancellationTokenSource();
			var source = new RecordingManagedImageSource(
				ProjectImageNames, bytes, ReadMvid(bytes))
			{
				Fault = ManagedImageFault.NoPeMetadata,
				FaultNumber = 2,
				AfterStreamDisposed = number =>
				{
					if (number == 2)
						cancellation.Cancel();
				},
			};
			var clock = new TestClock();
			var result = new RuntimeBuildIdentityReader(source, new string('b', 64)).Capture(
				RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
				clock,
				cancellation.Token);

			AssertClosedFailure(result, RetailImportErrorCode.Cancelled,
				RetailImportDiagnosticCode.None);
			Assert.That(source.Disposed,
				Is.EqualTo(ProjectImageNames.Take(2).Reverse()));
		}

		[Test]
		public void ManagedImageReaderRejectsWrongClockBeforeEnumerating()
		{
			var bytes = File.ReadAllBytes(typeof(Manifest).Assembly.Location);
			var source = new RecordingManagedImageSource(ProjectImageNames, bytes, ReadMvid(bytes));
			var owningClock = new TestClock();
			var otherClock = new TestClock();
			var result = new RuntimeBuildIdentityReader(source, new string('b', 64)).Capture(
				RetailValidationDeadline.Start(owningClock, TimeSpan.FromMinutes(1)),
				otherClock,
				CancellationToken.None);

			AssertClosedFailure(result, RetailImportErrorCode.ProfileContextFailure,
				RetailImportDiagnosticCode.ProfileMetadataMismatch);
			Assert.That(source.EnumerationCount, Is.Zero);
		}

		[Test]
		public void ManagedImageSourceUsesTheLockedSharedRootConstructor()
		{
			var constructor = typeof(IosRuntimeManagedImageSource).GetConstructor(
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
				binder: null,
				new[] { typeof(IosRuntimeResourceRoot) },
				modifiers: null);

			Assert.That(constructor, Is.Not.Null,
				"the source must borrow the concrete shared root and retain its private owner itself");
		}

		[Test]
		public void ManagedImageSourceRetainsTheOneRootAcrossOuterDisposal()
		{
			if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
				Assert.Ignore("The production descriptor-relative adapter supports Darwin and Linux.");

			using var fixture = new TemporaryDirectory("openra-managed-image-root");
			var bytes = File.ReadAllBytes(typeof(Manifest).Assembly.Location);
			foreach (var name in ProjectImageNames)
				File.WriteAllBytes(Path.Combine(fixture.Path, name), bytes);

			var opened = IosRuntimeResourceRoot.OpenBundleRoot(fixture.Path);
			Assert.That(opened.Succeeded, Is.True);
			var root = opened.Value;
			var childResult = root.CreateManagedImageSource();
			Assert.That(childResult.Succeeded, Is.True);
			var child = childResult.Value;
			root.Dispose();
			root.Dispose();

			Assert.That(child.EnumerateAppLocalManagedImages(), Is.EqualTo(ProjectImageNames));
			var lease = child.AcquirePinned(ProjectImageNames[0]);
			Assert.DoesNotThrow(lease.VerifyUnchanged);
			var stream = lease.OpenRead();
			lease.Dispose();
			lease.Dispose();
			child.Dispose();
			child.Dispose();
			Assert.That(stream.ReadByte(), Is.GreaterThanOrEqualTo(0));
			stream.Dispose();
			stream.Dispose();
			Assert.That(root.CreateManagedImageSource().Succeeded, Is.False);
		}

		[Test]
		public void ManagedImageSourceRejectsUppercaseSuffixCaseAliasesAndNestedImages()
		{
			if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
				Assert.Ignore("The production descriptor-relative adapter supports Darwin and Linux.");

			using var fixture = new TemporaryDirectory("openra-managed-image-namespace");
			var bytes = File.ReadAllBytes(typeof(Manifest).Assembly.Location);
			foreach (var name in ProjectImageNames)
				File.WriteAllBytes(Path.Combine(fixture.Path, name), bytes);
			File.WriteAllBytes(Path.Combine(fixture.Path, "Upper.DLL"), bytes);
			Directory.CreateDirectory(Path.Combine(fixture.Path, "nested"));
			File.WriteAllBytes(Path.Combine(fixture.Path, "nested", "Nested.dll"), bytes);

			var opened = IosRuntimeResourceRoot.OpenBundleRoot(fixture.Path);
			Assert.That(opened.Succeeded, Is.True);
			using var root = opened.Value;
			var childResult = root.CreateManagedImageSource();
			Assert.That(childResult.Succeeded, Is.True);
			using var child = childResult.Value;

			Assert.Throws<IOException>(() => child.EnumerateAppLocalManagedImages());
			File.Delete(Path.Combine(fixture.Path, "Upper.DLL"));
			Assert.That(child.EnumerateAppLocalManagedImages(), Is.EqualTo(ProjectImageNames),
				"nested images are outside this non-recursive namespace and rejected by the bundle audit");
			var beforeAliasCount = Directory.GetFiles(fixture.Path).Length;
			File.WriteAllBytes(Path.Combine(fixture.Path, "openra.game.dll"), bytes);
			if (Directory.GetFiles(fixture.Path).Length > beforeAliasCount)
				Assert.Throws<IOException>(() => child.EnumerateAppLocalManagedImages());
		}

		[Test]
		public void ManagedImageReaderRejectsCaseAliasesBeforeAcquisition()
		{
			var bytes = File.ReadAllBytes(typeof(Manifest).Assembly.Location);
			var source = new RecordingManagedImageSource(
				ProjectImageNames.Add("openra.game.dll"), bytes, ReadMvid(bytes));
			var clock = new TestClock();
			var result = new RuntimeBuildIdentityReader(source, new string('b', 64)).Capture(
				RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
				clock,
				CancellationToken.None);

			AssertClosedFailure(result, RetailImportErrorCode.RuntimeResourcesMissing,
				RetailImportDiagnosticCode.InvalidMapPackage);
			Assert.That(source.Acquired, Is.Empty);
		}

		[Test]
		public void RealManagedImageAdapterCapturesTheSameClosedInventoryTwice()
		{
			if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
				Assert.Ignore("The production descriptor-relative adapter supports Darwin and Linux.");

			using var fixture = new TemporaryDirectory("openra-managed-image-real-capture");
			var fallback = File.ReadAllBytes(typeof(Manifest).Assembly.Location);
			foreach (var name in ProjectImageNames)
			{
				var built = Path.Combine(TestContext.CurrentContext.TestDirectory, name);
				File.WriteAllBytes(
					Path.Combine(fixture.Path, name),
					File.Exists(built) ? File.ReadAllBytes(built) : fallback);
			}

			var opened = IosRuntimeResourceRoot.OpenBundleRoot(fixture.Path);
			Assert.That(opened.Succeeded, Is.True);
			var root = opened.Value;
			var childResult = root.CreateManagedImageSource();
			Assert.That(childResult.Succeeded, Is.True);
			using var child = childResult.Value;
			root.Dispose();
			var clock = new TestClock();
			var result = new RuntimeBuildIdentityReader(child, new string('b', 64)).Capture(
				RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
				clock,
				CancellationToken.None);

			Assert.That(result.Succeeded, Is.True);
			Assert.That(result.Value.ManagedImages.Select(image => image.BundleFilename),
				Is.EqualTo(ProjectImageNames));
		}

		[TestCase(RealManagedImageMutation.SymlinkLeaf)]
		[TestCase(RealManagedImageMutation.DirectoryLeaf)]
		[TestCase(RealManagedImageMutation.FifoLeaf)]
		[TestCase(RealManagedImageMutation.UnparseableLeaf)]
		[TestCase(RealManagedImageMutation.AddBetweenEnumerations)]
		[TestCase(RealManagedImageMutation.RemoveBetweenEnumerations)]
		[TestCase(RealManagedImageMutation.RenameBetweenEnumerations)]
		[TestCase(RealManagedImageMutation.RenameBeforeAcquisition)]
		[TestCase(RealManagedImageMutation.ReplaceAfterAcquisition)]
		[TestCase(RealManagedImageMutation.ReplaceDuringHash)]
		[TestCase(RealManagedImageMutation.ReplaceBetweenHashAndMvid)]
		[TestCase(RealManagedImageMutation.ReplaceAfterFirstRead)]
		[TestCase(RealManagedImageMutation.ReplaceAfterSecondEnumeration)]
		[TestCase(RealManagedImageMutation.ReplaceDuringFinalVerification)]
		[Timeout(10000)]
		public void RealManagedImageAdapterRejectsNonregularUnparseableAndRacedLeaves(
			RealManagedImageMutation mutation)
		{
			if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
				Assert.Ignore("The production descriptor-relative adapter supports Darwin and Linux.");

			using var fixture = new TemporaryDirectory("openra-managed-image-real-negative");
			var fallback = File.ReadAllBytes(typeof(Manifest).Assembly.Location);
			foreach (var name in ProjectImageNames)
			{
				var built = Path.Combine(TestContext.CurrentContext.TestDirectory, name);
				File.WriteAllBytes(
					Path.Combine(fixture.Path, name),
					File.Exists(built) ? File.ReadAllBytes(built) : fallback);
			}

			var firstPath = Path.Combine(fixture.Path, ProjectImageNames[0]);
			switch (mutation)
			{
				case RealManagedImageMutation.SymlinkLeaf:
					File.Delete(firstPath);
					File.CreateSymbolicLink(firstPath, ProjectImageNames[1]);
					break;
				case RealManagedImageMutation.DirectoryLeaf:
					File.Delete(firstPath);
					Directory.CreateDirectory(firstPath);
					break;
				case RealManagedImageMutation.FifoLeaf:
					File.Delete(firstPath);
					Assert.That(MakeFifo(firstPath, 0x180), Is.Zero);
					break;
				case RealManagedImageMutation.UnparseableLeaf:
					File.WriteAllBytes(firstPath, Encoding.UTF8.GetBytes("not a managed image"));
					break;
			}

			var opened = IosRuntimeResourceRoot.OpenBundleRoot(fixture.Path);
			Assert.That(opened.Succeeded, Is.True);
			using var root = opened.Value;
			var childResult = root.CreateManagedImageSource();
			Assert.That(childResult.Succeeded, Is.True);
			using var child = childResult.Value;
			IRuntimeManagedImageSource source = child;
			var replacement = firstPath + ".replacement";
			if (mutation is RealManagedImageMutation.ReplaceAfterAcquisition or
				RealManagedImageMutation.ReplaceDuringHash or
				RealManagedImageMutation.ReplaceBetweenHashAndMvid or
				RealManagedImageMutation.ReplaceAfterFirstRead or
				RealManagedImageMutation.ReplaceAfterSecondEnumeration or
				RealManagedImageMutation.ReplaceDuringFinalVerification)
				File.WriteAllBytes(replacement, File.ReadAllBytes(firstPath));
			void ReplaceFirstName()
			{
				if (File.Exists(replacement))
					File.Move(replacement, firstPath, overwrite: true);
			}

			if (mutation == RealManagedImageMutation.AddBetweenEnumerations)
			{
				source = new CallbackManagedImageSource(child)
				{
					AfterEnumeration = count =>
					{
						if (count == 1)
							File.WriteAllBytes(
								Path.Combine(fixture.Path, "Unexpected.Framework.dll"), fallback);
					},
				};
			}
			else if (mutation == RealManagedImageMutation.RemoveBetweenEnumerations)
			{
				source = new CallbackManagedImageSource(child)
				{
					AfterStreamDisposed = number =>
					{
						if (number == ProjectImageNames.Length)
							File.Delete(Path.Combine(fixture.Path, ProjectImageNames[^1]));
					},
				};
			}
			else if (mutation == RealManagedImageMutation.RenameBetweenEnumerations)
			{
				source = new CallbackManagedImageSource(child)
				{
					AfterStreamDisposed = number =>
					{
						if (number == ProjectImageNames.Length)
							File.Move(
								Path.Combine(fixture.Path, ProjectImageNames[^1]),
								Path.Combine(fixture.Path, "Renamed.ThirdParty.dll"));
					},
				};
			}
			else if (mutation == RealManagedImageMutation.RenameBeforeAcquisition)
			{
				source = new CallbackManagedImageSource(child)
				{
					BeforeAcquire = number =>
					{
						if (number == 1)
							File.Move(firstPath, firstPath + ".renamed");
					},
				};
			}
			else if (mutation == RealManagedImageMutation.ReplaceAfterAcquisition)
			{
				source = new CallbackManagedImageSource(child)
				{
					AfterAcquire = number =>
					{
						if (number == 1)
							ReplaceFirstName();
					},
				};
			}
			else if (mutation == RealManagedImageMutation.ReplaceDuringHash)
			{
				source = new CallbackManagedImageSource(child)
				{
					AfterFirstRead = number =>
					{
						if (number == 1)
							ReplaceFirstName();
					},
				};
			}
			else if (mutation == RealManagedImageMutation.ReplaceBetweenHashAndMvid)
			{
				source = new CallbackManagedImageSource(child)
				{
					AfterPositionReset = number =>
					{
						if (number == 1)
							ReplaceFirstName();
					},
				};
			}
			else if (mutation == RealManagedImageMutation.ReplaceAfterFirstRead)
			{
				source = new CallbackManagedImageSource(child)
				{
					AfterStreamDisposed = number =>
					{
						if (number == 1)
							ReplaceFirstName();
					},
				};
			}
			else if (mutation == RealManagedImageMutation.ReplaceAfterSecondEnumeration)
			{
				source = new CallbackManagedImageSource(child)
				{
					AfterEnumeration = number =>
					{
						if (number == 2)
							ReplaceFirstName();
					},
				};
			}
			else if (mutation == RealManagedImageMutation.ReplaceDuringFinalVerification)
			{
				source = new CallbackManagedImageSource(child)
				{
					BeforeVerify = number =>
					{
						if (number == 1)
							ReplaceFirstName();
					},
				};
			}

			var clock = new TestClock();
			RetailOperationResult<RuntimeBuildIdentity> result = null;
			Assert.DoesNotThrow(() => result = new RuntimeBuildIdentityReader(
				source, new string('b', 64)).Capture(
					RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
					clock,
					CancellationToken.None));

			AssertClosedFailure(result, RetailImportErrorCode.RuntimeResourcesMissing,
				RetailImportDiagnosticCode.InvalidMapPackage);
		}

		[Test]
		public void SharedRuntimeRootClosesExactlyOnceAfterTheFinalManagedStreamReference()
		{
			if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
				Assert.Ignore("The production descriptor-relative adapter supports Darwin and Linux.");

			using var fixture = new TemporaryDirectory("openra-managed-image-reference-closure");
			var bytes = SmallManagedImageBytes();
			File.WriteAllBytes(Path.Combine(fixture.Path, ProjectImageNames[0]), bytes);
			var opened = IosRuntimeResourceRoot.OpenBundleRoot(fixture.Path);
			Assert.That(opened.Succeeded, Is.True);
			var root = opened.Value;
			var owner = PrivateField<IosRuntimeRootDirectoryOwner>(root, "owner");
			void AssertOwner(int references, bool descriptorOpen)
			{
				Assert.Multiple(() =>
				{
					Assert.That(PrivateField<int>(owner, "references"), Is.EqualTo(references));
					Assert.That(PrivateField<int>(owner, "rootDescriptor") >= 0,
						Is.EqualTo(descriptorOpen));
				});
			}

			AssertOwner(1, descriptorOpen: true);
			var childResult = root.CreateManagedImageSource();
			Assert.That(childResult.Succeeded, Is.True);
			var child = childResult.Value;
			AssertOwner(2, descriptorOpen: true);
			var imageLease = child.AcquirePinned(ProjectImageNames[0]);
			AssertOwner(4, descriptorOpen: true);
			var stream = imageLease.OpenRead();
			AssertOwner(4, descriptorOpen: true);

			root.Dispose();
			root.Dispose();
			AssertOwner(3, descriptorOpen: true);
			child.Dispose();
			child.Dispose();
			AssertOwner(2, descriptorOpen: true);
			imageLease.Dispose();
			imageLease.Dispose();
			AssertOwner(1, descriptorOpen: true);
			Assert.That(stream.ReadByte(), Is.GreaterThanOrEqualTo(0));
			stream.Dispose();
			stream.Dispose();
			AssertOwner(0, descriptorOpen: false);
		}

		[Test]
		public void RuntimeRootRejectsARealDeviceNodeAsNonregular()
		{
			if ((!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) ||
				!File.Exists("/dev/null"))
				Assert.Ignore("This proof requires a Unix device node.");

			var opened = IosRuntimeResourceRoot.OpenBundleRoot("/dev");
			Assert.That(opened.Succeeded, Is.True);
			using var root = opened.Value;
			var acquired = root.AcquireRegularFile("null");
			AssertClosedFailure(acquired, RetailImportErrorCode.RuntimeResourcesMissing,
				RetailImportDiagnosticCode.InvalidMapPackage);
		}

		[Test]
		public void DisposingManagedImageChildDoesNotConsumeTheOuterRootReference()
		{
			if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
				Assert.Ignore("The production descriptor-relative adapter supports Darwin and Linux.");

			using var fixture = new TemporaryDirectory("openra-managed-image-child-first");
			var bytes = File.ReadAllBytes(typeof(Manifest).Assembly.Location);
			File.WriteAllBytes(Path.Combine(fixture.Path, "OpenRA.Game.dll"), bytes);
			var opened = IosRuntimeResourceRoot.OpenBundleRoot(fixture.Path);
			Assert.That(opened.Succeeded, Is.True);
			using var root = opened.Value;
			var childResult = root.CreateManagedImageSource();
			Assert.That(childResult.Succeeded, Is.True);
			childResult.Value.Dispose();
			childResult.Value.Dispose();

			var acquired = root.AcquireRegularFile("OpenRA.Game.dll");
			Assert.That(acquired.Succeeded, Is.True);
			using var lease = acquired.Value;
			using var stream = lease.OpenVerifiedRead();
			Assert.That(stream.ReadByte(), Is.GreaterThanOrEqualTo(0));
		}

		[Test]
		public void PinnedResourceRejectsSameInodeSameLengthInPlaceMutation()
		{
			if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
				Assert.Ignore("The production descriptor-relative adapter supports Darwin and Linux.");

			using var fixture = new TemporaryDirectory("openra-runtime-in-place-drift");
			var path = Path.Combine(fixture.Path, "payload.bin");
			File.WriteAllBytes(path, Encoding.UTF8.GetBytes("original"));
			var opened = IosRuntimeResourceRoot.OpenBundleRoot(fixture.Path);
			Assert.That(opened.Succeeded, Is.True);
			using var root = opened.Value;
			var acquired = root.AcquireRegularFile("payload.bin");
			Assert.That(acquired.Succeeded, Is.True);
			using var lease = acquired.Value;
			using var stream = lease.OpenVerifiedRead();
			File.WriteAllBytes(path, Encoding.UTF8.GetBytes("mutated!"));

			Assert.Throws<IOException>(lease.VerifyUnchanged);
		}

		[Test]
		public void RuntimeStatLayoutsIncludeStableLeafChangeTimestamps()
		{
			Assert.That(IosRuntimeUnixNative.TryGetLayout(
				OSPlatform.OSX, Architecture.Arm64, out var darwin), Is.True);
			Assert.That(IosRuntimeUnixNative.TryGetLayout(
				OSPlatform.Linux, Architecture.X64, out var linux), Is.True);

			Assert.Multiple(() =>
			{
				Assert.That((darwin.ModificationSecondsOffset, darwin.ModificationNanosecondsOffset,
					darwin.ChangeSecondsOffset, darwin.ChangeNanosecondsOffset),
					Is.EqualTo((48, 56, 64, 72)));
				Assert.That((linux.ModificationSecondsOffset, linux.ModificationNanosecondsOffset,
					linux.ChangeSecondsOffset, linux.ChangeNanosecondsOffset),
					Is.EqualTo((88, 96, 104, 112)));
			});
		}

		[Test]
		public void ContractBuilderIsPureDeterministicAndPreservesExactInputInstances()
		{
			const string ExpectedBaseContractDigest =
				"415974360046ac3075e1b284c59e39affa4eab3c30aee6254c98cb0d2e6bfc7b";
			var fixture = RuntimeFixture.Load();
			using var package = new Folder(Path.Combine(RepositoryRoot(), "mods", "ra2"));
			using var manifest = new Manifest("ra2", package, "mod.ios-base.yaml");
			var buildIdentity = FixedBuildIdentity();
			var inputs = new RuntimeContractInputs(fixture.Inventory, buildIdentity);
			var clock = new TestClock();
			var builder = new Ra2RuntimeContractBuilder(inputs);
			var first = builder.BuildExpected(
				Ra2RuntimeProfileId.Base,
				Ra2RuntimeProfileId.Base.ManifestSelection(),
				manifest,
				RetailFilenameCatalog.V1.DigestSha256,
				Map.CurrentMapFormat,
				1,
				1,
				RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
				clock,
				CancellationToken.None);
			var second = builder.BuildExpected(
				Ra2RuntimeProfileId.Base,
				Ra2RuntimeProfileId.Base.ManifestSelection(),
				manifest,
				RetailFilenameCatalog.V1.DigestSha256,
				Map.CurrentMapFormat,
				1,
				1,
				RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
				clock,
				CancellationToken.None);

			Assert.That(first.Succeeded, Is.True);
			Assert.That(second.Succeeded, Is.True);
			Assert.Multiple(() =>
			{
				Assert.That(first.Value.Contract.ProfileId, Is.EqualTo(Ra2RuntimeProfileId.Base));
				Assert.That(first.Value.Contract.ContractDigest,
					Is.EqualTo(second.Value.Contract.ContractDigest));
				Assert.That(first.Value.Contract.ContractDigest,
					Is.EqualTo(ExpectedBaseContractDigest), "checked independent framing vector");
				Assert.That(first.Value.Contract.FilenameCatalogDigest,
					Is.EqualTo(RetailFilenameCatalog.V1.DigestSha256));
				Assert.That(first.Value.Contract.MapFormatVersion, Is.EqualTo(Map.CurrentMapFormat));
				Assert.That(ReferenceEquals(first.Value.Inputs, inputs), Is.True);
				Assert.That(ReferenceEquals(first.Value.Inputs.Inventory, fixture.Inventory), Is.True);
				Assert.That(ReferenceEquals(first.Value.Inputs.BuildIdentity, buildIdentity), Is.True);
			});
		}

		[TestCase(
			Ra2RuntimeProfileId.Base,
			"mod.ios-base.yaml",
			"415974360046ac3075e1b284c59e39affa4eab3c30aee6254c98cb0d2e6bfc7b")]
		[TestCase(
			Ra2RuntimeProfileId.Yuri,
			"mod.ios-yuri.yaml",
			"28d13a345c0d155aaedfdfed55a95ca2ec80305111758eeba7f29835d9792f3c")]
		public void ContractFramingMatchesAnIndependentCheckedVectorForBothProfiles(
			Ra2RuntimeProfileId profile,
			string entryName,
			string checkedDigest)
		{
			var fixture = RuntimeFixture.Load();
			using var package = new Folder(Path.Combine(RepositoryRoot(), "mods", "ra2"));
			using var manifest = new Manifest("ra2", package, entryName);
			var buildIdentity = FixedBuildIdentity();
			var inputs = new RuntimeContractInputs(fixture.Inventory, buildIdentity);
			var clock = new TestClock();
			var result = new Ra2RuntimeContractBuilder(inputs).BuildExpected(
				profile,
				profile.ManifestSelection(),
				manifest,
				RetailFilenameCatalog.V1.DigestSha256,
				Map.CurrentMapFormat,
				1,
				1,
				RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
				clock,
				CancellationToken.None);
			Assert.That(result.Succeeded, Is.True);
			var independentFrame = WriteIndependentContractFrame(
				profile,
				profile.ManifestSelection(),
				manifest,
				fixture.Inventory,
				buildIdentity,
				RetailFilenameCatalog.V1.DigestSha256,
				Map.CurrentMapFormat,
				1,
				1,
				ContractFramePerturbation.None);
			var independentDigest = Convert.ToHexString(SHA256.HashData(independentFrame))
				.ToLowerInvariant();

			Assert.Multiple(() =>
			{
				Assert.That(independentDigest, Is.EqualTo(checkedDigest));
				Assert.That(result.Value.Contract.ContractDigest, Is.EqualTo(checkedDigest));
				Assert.That(result.Value.Contract.ContractDigest, Is.EqualTo(independentDigest));
			});
		}

		[Test]
		public void EveryContractFieldOrdinalCountTagAndTrailingBytePerturbsTheIndependentDigest()
		{
			var fixture = RuntimeFixture.Load();
			using var package = new Folder(Path.Combine(RepositoryRoot(), "mods", "ra2"));
			using var manifest = new Manifest("ra2", package, "mod.ios-base.yaml");
			var buildIdentity = FixedBuildIdentity();
			var baseline = WriteIndependentContractFrame(
				Ra2RuntimeProfileId.Base,
				Ra2RuntimeProfileId.Base.ManifestSelection(),
				manifest,
				fixture.Inventory,
				buildIdentity,
				RetailFilenameCatalog.V1.DigestSha256,
				Map.CurrentMapFormat,
				1,
				1,
				ContractFramePerturbation.None);
			var baselineDigest = SHA256.HashData(baseline);

			foreach (var perturbation in Enum.GetValues<ContractFramePerturbation>().Skip(1))
			{
				var changed = WriteIndependentContractFrame(
					Ra2RuntimeProfileId.Base,
					Ra2RuntimeProfileId.Base.ManifestSelection(),
					manifest,
					fixture.Inventory,
					buildIdentity,
					RetailFilenameCatalog.V1.DigestSha256,
					Map.CurrentMapFormat,
					1,
					1,
					perturbation);
				Assert.That(SHA256.HashData(changed), Is.Not.EqualTo(baselineDigest),
					perturbation.ToString());
			}
		}

		[TestCase(BuilderContextScenario.DefaultDeadline)]
		[TestCase(BuilderContextScenario.DifferentClock)]
		[TestCase(BuilderContextScenario.NullClock)]
		[TestCase(BuilderContextScenario.MalformedCatalog)]
		[TestCase(BuilderContextScenario.ZeroMapFormatVersion)]
		[TestCase(BuilderContextScenario.ZeroArchiveValidatorVersion)]
		[TestCase(BuilderContextScenario.ZeroMapValidatorVersion)]
		public void ContractBuilderRejectsEveryInvalidClockCatalogAndVersionDomain(
			BuilderContextScenario scenario)
		{
			var fixture = RuntimeFixture.Load();
			using var package = new Folder(Path.Combine(RepositoryRoot(), "mods", "ra2"));
			using var manifest = new Manifest("ra2", package, "mod.ios-base.yaml");
			var builder = new Ra2RuntimeContractBuilder(
				new RuntimeContractInputs(fixture.Inventory, FixedBuildIdentity()));
			var clock = new TestClock();
			var deadline = RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1));
			IRetailMonotonicClock suppliedClock = clock;
			var catalog = RetailFilenameCatalog.V1.DigestSha256;
			var mapFormatVersion = Map.CurrentMapFormat;
			var archiveValidatorVersion = 1;
			var mapValidatorVersion = 1;
			switch (scenario)
			{
				case BuilderContextScenario.DefaultDeadline:
					deadline = default;
					break;
				case BuilderContextScenario.DifferentClock:
					suppliedClock = new TestClock();
					break;
				case BuilderContextScenario.NullClock:
					suppliedClock = null;
					break;
				case BuilderContextScenario.MalformedCatalog:
					catalog = new string('A', 64);
					break;
				case BuilderContextScenario.ZeroMapFormatVersion:
					mapFormatVersion = 0;
					break;
				case BuilderContextScenario.ZeroArchiveValidatorVersion:
					archiveValidatorVersion = 0;
					break;
				case BuilderContextScenario.ZeroMapValidatorVersion:
					mapValidatorVersion = 0;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(scenario));
			}

			var result = builder.BuildExpected(
				Ra2RuntimeProfileId.Base,
				Ra2RuntimeProfileId.Base.ManifestSelection(),
				manifest,
				catalog,
				mapFormatVersion,
				archiveValidatorVersion,
				mapValidatorVersion,
				deadline,
				suppliedClock,
				CancellationToken.None);

			AssertClosedFailure(result, RetailImportErrorCode.ProfileContextFailure,
				RetailImportDiagnosticCode.ProfileMetadataMismatch);
		}

		[Test]
		public void ContractBuilderHonorsCancellationAndExpiryAtEveryFramingCheckpoint()
		{
			var fixture = RuntimeFixture.Load();
			using var package = new Folder(Path.Combine(RepositoryRoot(), "mods", "ra2"));
			using var manifest = new Manifest("ra2", package, "mod.ios-base.yaml");
			var builder = new Ra2RuntimeContractBuilder(
				new RuntimeContractInputs(fixture.Inventory, FixedBuildIdentity()));
			RetailOperationResult<Ra2RuntimeContractExpectation> Build(
				CheckpointProbeClock clock,
				CancellationToken token) => builder.BuildExpected(
					Ra2RuntimeProfileId.Base,
					Ra2RuntimeProfileId.Base.ManifestSelection(),
					manifest,
					RetailFilenameCatalog.V1.DigestSha256,
					Map.CurrentMapFormat,
					1,
					1,
					RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
					clock,
					token);

			var probeClock = new CheckpointProbeClock();
			var probe = Build(probeClock, CancellationToken.None);
			Assert.That(probe.Succeeded, Is.True);
			var checkpointCount = probeClock.ElapsedCallCount;
			Assert.That(checkpointCount, Is.EqualTo(1 + 277 + ProjectImageNames.Length + 1));
			for (var checkpoint = 1; checkpoint <= checkpointCount; checkpoint++)
			{
				var expiryClock = new CheckpointProbeClock(expireAtCheckpoint: checkpoint);
				AssertClosedFailure(Build(expiryClock, CancellationToken.None),
					RetailImportErrorCode.ValidationTimedOut,
					RetailImportDiagnosticCode.DeadlineExceeded);

				using var cancellation = new CancellationTokenSource();
				if (checkpoint == 1)
					cancellation.Cancel();
				var cancellationClock = new CheckpointProbeClock(
					cancelAfterCheckpoint: checkpoint - 1,
					cancellation: cancellation);
				AssertClosedFailure(Build(cancellationClock, cancellation.Token),
					RetailImportErrorCode.Cancelled,
					RetailImportDiagnosticCode.None);
			}
		}

		[Test]
		public void ContractBuilderClosedInputDomainsCannotReachArithmeticOverflow()
		{
			var fixture = RuntimeFixture.Load();
			using var package = new Folder(Path.Combine(RepositoryRoot(), "mods", "ra2"));
			using var manifest = new Manifest("ra2", package, "mod.ios-base.yaml");
			var identity = FixedBuildIdentity();
			var clock = new TestClock();
			var result = new Ra2RuntimeContractBuilder(
				new RuntimeContractInputs(fixture.Inventory, identity)).BuildExpected(
					Ra2RuntimeProfileId.Base,
					Ra2RuntimeProfileId.Base.ManifestSelection(),
					manifest,
					RetailFilenameCatalog.V1.DigestSha256,
					int.MaxValue,
					int.MaxValue,
					int.MaxValue,
					RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
					clock,
					CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(result.Succeeded, Is.True,
					"the legal version maximum must remain representable as U32LE");
				Assert.That(fixture.Inventory.DescriptorCount,
					Is.EqualTo(RuntimeContractDescriptorInventory.AuthenticatedDescriptorCount));
				Assert.That(fixture.Inventory.Descriptors.All(descriptor =>
					descriptor.Length >= 0 && descriptor.Profiles.Length is >= 1 and <= 2 &&
					(int)descriptor.Purpose is >= 1 and <= 2), Is.True);
				Assert.That(identity.ManagedImages.Length, Is.LessThanOrEqualTo(int.MaxValue));
			});
		}

		[Test]
		public void OwnedReadLengthArithmeticHasOnePreciselyCheckedBoundary()
		{
			Assert.Multiple(() =>
			{
				Assert.That(RetailRuntimeContractFailures.TryAccumulateReadLength(
					long.MaxValue - 1, 1, out var maximum), Is.True);
				Assert.That(maximum, Is.EqualTo(long.MaxValue));
				Assert.That(RetailRuntimeContractFailures.TryAccumulateReadLength(
					long.MaxValue, 1, out _), Is.False);
			});
		}

		[Test]
		public void VerifiedPreflightReturnsTheExactExpectedContractInstance()
		{
			var fixture = RuntimeFixture.Load();
			using var package = new Folder(Path.Combine(RepositoryRoot(), "mods", "ra2"));
			using var manifest = new Manifest("ra2", package, "mod.ios-base.yaml");
			var inputs = new RuntimeContractInputs(fixture.Inventory, FixedBuildIdentity());
			var clock = new TestClock();
			var deadline = RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1));
			var expected = new Ra2RuntimeContractBuilder(inputs).BuildExpected(
				Ra2RuntimeProfileId.Base,
				Ra2RuntimeProfileId.Base.ManifestSelection(),
				manifest,
				RetailFilenameCatalog.V1.DigestSha256,
				Map.CurrentMapFormat,
				1,
				1,
				deadline,
				clock,
				CancellationToken.None);
			Assert.That(expected.Succeeded, Is.True);
			using var root = new DictionaryRuntimeRoot(fixture.Payloads);
			var result = new IosRuntimeResourcePreflight(inputs).Check(
				expected.Value,
				manifest,
				root,
				deadline,
				clock,
				CancellationToken.None);

			Assert.That(result.Succeeded, Is.True);
			Assert.Multiple(() =>
			{
				Assert.That(ReferenceEquals(result.Value.Contract, expected.Value.Contract), Is.True);
				Assert.That(result.Value.Selection, Is.EqualTo(expected.Value.Selection));
				Assert.That(result.Value.MapCompatibility, Is.EqualTo(manifest.MapCompatibility));
			});
		}

		[TestCase(false)]
		[TestCase(true)]
		public void VerifiedPreflightRejectsMapCompatibilityMutationAndCleansInReverse(
			bool mutateDuringVerification)
		{
			var fixture = RuntimeFixture.Load();
			using var package = new Folder(Path.Combine(RepositoryRoot(), "mods", "ra2"));
			using var manifest = new Manifest("ra2", package, "mod.ios-base.yaml");
			var inputs = new RuntimeContractInputs(fixture.Inventory, FixedBuildIdentity());
			var clock = new TestClock();
			var deadline = RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1));
			var expected = new Ra2RuntimeContractBuilder(inputs).BuildExpected(
				Ra2RuntimeProfileId.Base,
				Ra2RuntimeProfileId.Base.ManifestSelection(),
				manifest,
				RetailFilenameCatalog.V1.DigestSha256,
				Map.CurrentMapFormat,
				1,
				1,
				deadline,
				clock,
				CancellationToken.None);
			Assert.That(expected.Succeeded, Is.True);
			using var root = new DictionaryRuntimeRoot(fixture.Payloads);
			void Mutate(int number)
			{
				if (number == 1)
					manifest.MapCompatibility[0] = "cnc";
			}

			if (mutateDuringVerification)
				root.LeaseVerified = Mutate;
			else
				root.FileAcquired = Mutate;
			var result = new IosRuntimeResourcePreflight(inputs).Check(
				expected.Value,
				manifest,
				root,
				deadline,
				clock,
				CancellationToken.None);

			AssertClosedFailure(result, RetailImportErrorCode.RuntimeResourcesMissing,
				RetailImportDiagnosticCode.InvalidMapPackage);
			Assert.That(root.Disposed, Is.EqualTo(root.Acquired.AsEnumerable().Reverse()));
		}

		[Test]
		public void VerifiedContractRequiresTheSelectedModExactlyOnce()
		{
			var fixture = RuntimeFixture.Load();
			using var package = new Folder(Path.Combine(RepositoryRoot(), "mods", "ra2"));
			using var manifest = new Manifest("ra2", package, "mod.ios-base.yaml");
			var inputs = new RuntimeContractInputs(fixture.Inventory, FixedBuildIdentity());
			var clock = new TestClock();
			var expected = new Ra2RuntimeContractBuilder(inputs).BuildExpected(
				Ra2RuntimeProfileId.Base,
				Ra2RuntimeProfileId.Base.ManifestSelection(),
				manifest,
				RetailFilenameCatalog.V1.DigestSha256,
				Map.CurrentMapFormat,
				1,
				1,
				RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
				clock,
				CancellationToken.None);
			Assert.That(expected.Succeeded, Is.True);

			Assert.Throws<ArgumentException>(() => new VerifiedRa2RuntimeContract(
				expected.Value, ImmutableArray.Create("cnc")));
		}

		[TestCase(
			VerifiedPreflightScenario.DefaultDeadline,
			RetailImportErrorCode.ProfileContextFailure,
			RetailImportDiagnosticCode.ProfileMetadataMismatch)]
		[TestCase(
			VerifiedPreflightScenario.DifferentClock,
			RetailImportErrorCode.ProfileContextFailure,
			RetailImportDiagnosticCode.ProfileMetadataMismatch)]
		[TestCase(
			VerifiedPreflightScenario.CancelledAtEntry,
			RetailImportErrorCode.Cancelled,
			RetailImportDiagnosticCode.None)]
		[TestCase(
			VerifiedPreflightScenario.ExpiredAtEntry,
			RetailImportErrorCode.ValidationTimedOut,
			RetailImportDiagnosticCode.DeadlineExceeded)]
		[TestCase(
			VerifiedPreflightScenario.AcquireReturnsNull,
			RetailImportErrorCode.RuntimeResourcesMissing,
			RetailImportDiagnosticCode.InvalidMapPackage)]
		[TestCase(
			VerifiedPreflightScenario.AcquireThrows,
			RetailImportErrorCode.RuntimeResourcesMissing,
			RetailImportDiagnosticCode.InvalidMapPackage)]
		[TestCase(
			VerifiedPreflightScenario.AcquireOverflow,
			RetailImportErrorCode.RuntimeResourcesMissing,
			RetailImportDiagnosticCode.InvalidMapPackage)]
		[TestCase(
			VerifiedPreflightScenario.CancelAfterFailedAcquire,
			RetailImportErrorCode.Cancelled,
			RetailImportDiagnosticCode.None)]
		[TestCase(
			VerifiedPreflightScenario.LeaseGetterThrows,
			RetailImportErrorCode.RuntimeResourcesMissing,
			RetailImportDiagnosticCode.InvalidMapPackage)]
		[TestCase(
			VerifiedPreflightScenario.LeaseGetterOverflow,
			RetailImportErrorCode.RuntimeResourcesMissing,
			RetailImportDiagnosticCode.InvalidMapPackage)]
		[TestCase(
			VerifiedPreflightScenario.OpenThrows,
			RetailImportErrorCode.RuntimeResourcesMissing,
			RetailImportDiagnosticCode.InvalidMapPackage)]
		[TestCase(
			VerifiedPreflightScenario.OpenOverflow,
			RetailImportErrorCode.RuntimeResourcesMissing,
			RetailImportDiagnosticCode.InvalidMapPackage)]
		[TestCase(
			VerifiedPreflightScenario.ReadThrows,
			RetailImportErrorCode.RuntimeResourcesMissing,
			RetailImportDiagnosticCode.InvalidMapPackage)]
		[TestCase(
			VerifiedPreflightScenario.HashMismatch,
			RetailImportErrorCode.RuntimeResourcesMissing,
			RetailImportDiagnosticCode.InvalidMapPackage)]
		[TestCase(
			VerifiedPreflightScenario.CancelDuringRead,
			RetailImportErrorCode.Cancelled,
			RetailImportDiagnosticCode.None)]
		[TestCase(
			VerifiedPreflightScenario.VerifyThrows,
			RetailImportErrorCode.RuntimeResourcesMissing,
			RetailImportDiagnosticCode.InvalidMapPackage)]
		[TestCase(
			VerifiedPreflightScenario.VerifyOverflow,
			RetailImportErrorCode.RuntimeResourcesMissing,
			RetailImportDiagnosticCode.InvalidMapPackage)]
		[TestCase(
			VerifiedPreflightScenario.ExpireDuringVerify,
			RetailImportErrorCode.ValidationTimedOut,
			RetailImportDiagnosticCode.DeadlineExceeded)]
		[TestCase(
			VerifiedPreflightScenario.ReadOverflow,
			RetailImportErrorCode.RuntimeResourcesMissing,
			RetailImportDiagnosticCode.InvalidMapPackage)]
		public void VerifiedPreflightMapsEveryBoundaryAndCleansOwnedLeasesInReverse(
			VerifiedPreflightScenario scenario,
			RetailImportErrorCode expectedCode,
			RetailImportDiagnosticCode expectedDiagnostic)
		{
			var fixture = RuntimeFixture.Load();
			using var package = new Folder(Path.Combine(RepositoryRoot(), "mods", "ra2"));
			using var manifest = new Manifest("ra2", package, "mod.ios-base.yaml");
			var inputs = new RuntimeContractInputs(fixture.Inventory, FixedBuildIdentity());
			var clock = new TestClock();
			var deadline = RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1));
			var expected = new Ra2RuntimeContractBuilder(inputs).BuildExpected(
				Ra2RuntimeProfileId.Base,
				Ra2RuntimeProfileId.Base.ManifestSelection(),
				manifest,
				RetailFilenameCatalog.V1.DigestSha256,
				Map.CurrentMapFormat,
				1,
				1,
				deadline,
				clock,
				CancellationToken.None);
			Assert.That(expected.Succeeded, Is.True);
			using var cancellation = new CancellationTokenSource();
			using var root = new DictionaryRuntimeRoot(fixture.Payloads)
			{
				FaultNumber = 3,
			};
			var suppliedDeadline = deadline;
			IRetailMonotonicClock suppliedClock = clock;
			switch (scenario)
			{
				case VerifiedPreflightScenario.DefaultDeadline:
					suppliedDeadline = default;
					break;
				case VerifiedPreflightScenario.DifferentClock:
					suppliedClock = new TestClock();
					break;
				case VerifiedPreflightScenario.CancelledAtEntry:
					cancellation.Cancel();
					break;
				case VerifiedPreflightScenario.ExpiredAtEntry:
					clock.Expired = true;
					break;
				case VerifiedPreflightScenario.AcquireReturnsNull:
					root.Fault = VerifiedPreflightResourceFault.AcquireReturnsNull;
					break;
				case VerifiedPreflightScenario.AcquireThrows:
					root.Fault = VerifiedPreflightResourceFault.AcquireThrows;
					break;
				case VerifiedPreflightScenario.AcquireOverflow:
					root.Fault = VerifiedPreflightResourceFault.AcquireOverflow;
					break;
				case VerifiedPreflightScenario.CancelAfterFailedAcquire:
					root.Fault = VerifiedPreflightResourceFault.AcquireFails;
					root.FileAcquired = number =>
					{
						if (number == root.FaultNumber)
							cancellation.Cancel();
					};
					break;
				case VerifiedPreflightScenario.LeaseGetterThrows:
					root.Fault = VerifiedPreflightResourceFault.LeaseGetterThrows;
					break;
				case VerifiedPreflightScenario.LeaseGetterOverflow:
					root.Fault = VerifiedPreflightResourceFault.LeaseGetterOverflow;
					break;
				case VerifiedPreflightScenario.OpenThrows:
					root.Fault = VerifiedPreflightResourceFault.OpenThrows;
					break;
				case VerifiedPreflightScenario.OpenOverflow:
					root.Fault = VerifiedPreflightResourceFault.OpenOverflow;
					break;
				case VerifiedPreflightScenario.ReadThrows:
					root.Fault = VerifiedPreflightResourceFault.ReadThrows;
					break;
				case VerifiedPreflightScenario.HashMismatch:
					root.Fault = VerifiedPreflightResourceFault.HashMismatch;
					break;
				case VerifiedPreflightScenario.CancelDuringRead:
					root.StreamRead = number =>
					{
						if (number == root.FaultNumber)
							cancellation.Cancel();
					};
					break;
				case VerifiedPreflightScenario.VerifyThrows:
					root.Fault = VerifiedPreflightResourceFault.VerifyThrows;
					break;
				case VerifiedPreflightScenario.VerifyOverflow:
					root.Fault = VerifiedPreflightResourceFault.VerifyOverflow;
					break;
				case VerifiedPreflightScenario.ExpireDuringVerify:
					root.LeaseVerified = number =>
					{
						if (number == 1)
							clock.Expired = true;
					};
					break;
				case VerifiedPreflightScenario.ReadOverflow:
					root.Fault = VerifiedPreflightResourceFault.ReadOverflow;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(scenario));
			}

			RetailOperationResult<VerifiedRa2RuntimeContract> result = null;
			Assert.DoesNotThrow(() => result = new IosRuntimeResourcePreflight(inputs).Check(
				expected.Value,
				manifest,
				root,
				suppliedDeadline,
				suppliedClock,
				cancellation.Token));

			AssertClosedFailure(result, expectedCode, expectedDiagnostic);
			Assert.That(root.Disposed, Is.EqualTo(root.Leased.AsEnumerable().Reverse()));
			if (scenario is VerifiedPreflightScenario.DefaultDeadline or
				VerifiedPreflightScenario.DifferentClock or
				VerifiedPreflightScenario.CancelledAtEntry or
				VerifiedPreflightScenario.ExpiredAtEntry)
				Assert.That(root.Acquired, Is.Empty);
		}

		[Test]
		public void VerifiedPreflightHonorsCancellationAndExpiryAtEveryCheckpoint()
		{
			var fixture = RuntimeFixture.Load();
			using var package = new Folder(Path.Combine(RepositoryRoot(), "mods", "ra2"));
			using var manifest = new Manifest("ra2", package, "mod.ios-base.yaml");
			var inputs = new RuntimeContractInputs(fixture.Inventory, FixedBuildIdentity());
			var expectationClock = new TestClock();
			var expected = new Ra2RuntimeContractBuilder(inputs).BuildExpected(
				Ra2RuntimeProfileId.Base,
				Ra2RuntimeProfileId.Base.ManifestSelection(),
				manifest,
				RetailFilenameCatalog.V1.DigestSha256,
				Map.CurrentMapFormat,
				1,
				1,
				RetailValidationDeadline.Start(expectationClock, TimeSpan.FromMinutes(1)),
				expectationClock,
				CancellationToken.None);
			Assert.That(expected.Succeeded, Is.True);

			RetailOperationResult<VerifiedRa2RuntimeContract> Check(
				CheckpointProbeClock clock,
				CancellationToken token,
				out DictionaryRuntimeRoot root)
			{
				root = new DictionaryRuntimeRoot(fixture.Payloads);
				return new IosRuntimeResourcePreflight(inputs).Check(
					expected.Value,
					manifest,
					root,
					RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1)),
					clock,
					token);
			}

			var probeClock = new CheckpointProbeClock();
			var probe = Check(probeClock, CancellationToken.None, out var probeRoot);
			Assert.That(probe.Succeeded, Is.True);
			Assert.That(probeRoot.Disposed, Is.EqualTo(probeRoot.Leased.AsEnumerable().Reverse()));
			probeRoot.Dispose();
			var checkpointCount = probeClock.ElapsedCallCount;
			Assert.That(checkpointCount, Is.EqualTo(1 + 277 * 10 + 1 + 277 * 2 + 1));

			for (var checkpoint = 1; checkpoint <= checkpointCount; checkpoint++)
			{
				var expiryClock = new CheckpointProbeClock(expireAtCheckpoint: checkpoint);
				var expired = Check(expiryClock, CancellationToken.None, out var expiryRoot);
				AssertClosedFailure(expired, RetailImportErrorCode.ValidationTimedOut,
					RetailImportDiagnosticCode.DeadlineExceeded);
				Assert.That(expiryRoot.Disposed,
					Is.EqualTo(expiryRoot.Leased.AsEnumerable().Reverse()),
					$"expiry checkpoint {checkpoint}/{checkpointCount}");
				expiryRoot.Dispose();

				using var cancellation = new CancellationTokenSource();
				if (checkpoint == 1)
					cancellation.Cancel();
				var cancellationClock = new CheckpointProbeClock(
					cancelAfterCheckpoint: checkpoint - 1,
					cancellation: cancellation);
				var cancelled = Check(
					cancellationClock, cancellation.Token, out var cancellationRoot);
				AssertClosedFailure(cancelled, RetailImportErrorCode.Cancelled,
					RetailImportDiagnosticCode.None);
				Assert.That(cancellationRoot.Disposed,
					Is.EqualTo(cancellationRoot.Leased.AsEnumerable().Reverse()),
					$"cancellation checkpoint {checkpoint}/{checkpointCount}");
				cancellationRoot.Dispose();
			}
		}

		[Test]
		public void VerifiedPreflightSuppressesHostileCleanupAndDefensivelyCopiesCompatibility()
		{
			var fixture = RuntimeFixture.Load();
			using var package = new Folder(Path.Combine(RepositoryRoot(), "mods", "ra2"));
			using var manifest = new Manifest("ra2", package, "mod.ios-base.yaml");
			var inputs = new RuntimeContractInputs(fixture.Inventory, FixedBuildIdentity());
			var clock = new TestClock();
			var deadline = RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1));
			var expected = new Ra2RuntimeContractBuilder(inputs).BuildExpected(
				Ra2RuntimeProfileId.Base,
				Ra2RuntimeProfileId.Base.ManifestSelection(),
				manifest,
				RetailFilenameCatalog.V1.DigestSha256,
				Map.CurrentMapFormat,
				1,
				1,
				deadline,
				clock,
				CancellationToken.None);
			Assert.That(expected.Succeeded, Is.True);
			using var root = new DictionaryRuntimeRoot(fixture.Payloads)
			{
				Fault = VerifiedPreflightResourceFault.DisposeThrows,
				FaultNumber = 3,
			};
			var result = new IosRuntimeResourcePreflight(inputs).Check(
				expected.Value,
				manifest,
				root,
				deadline,
				clock,
				CancellationToken.None);

			Assert.That(result.Succeeded, Is.True);
			manifest.MapCompatibility[0] = "cnc";
			Assert.Multiple(() =>
			{
				Assert.That(result.Value.MapCompatibility, Is.EqualTo(new[] { "ra2" }));
				Assert.That(ReferenceEquals(result.Value.Contract, expected.Value.Contract), Is.True);
				Assert.That(root.Disposed, Is.EqualTo(root.Leased.AsEnumerable().Reverse()));
			});
		}

		[Test]
		public void BuilderAndPreflightRejectContextMismatchBeforeOpeningResources()
		{
			var fixture = RuntimeFixture.Load();
			using var package = new Folder(Path.Combine(RepositoryRoot(), "mods", "ra2"));
			using var manifest = new Manifest("ra2", package, "mod.ios-base.yaml");
			var firstInputs = new RuntimeContractInputs(fixture.Inventory, FixedBuildIdentity());
			var secondInputs = new RuntimeContractInputs(fixture.Inventory, FixedBuildIdentity());
			var clock = new TestClock();
			var deadline = RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1));
			var invalidProfile = new Ra2RuntimeContractBuilder(firstInputs).BuildExpected(
				default,
				default,
				manifest,
				RetailFilenameCatalog.V1.DigestSha256,
				Map.CurrentMapFormat,
				1,
				1,
				deadline,
				clock,
				CancellationToken.None);
			AssertClosedFailure(invalidProfile, RetailImportErrorCode.ProfileContextFailure,
				RetailImportDiagnosticCode.ProfileMetadataMismatch);

			var expectation = new Ra2RuntimeContractBuilder(firstInputs).BuildExpected(
				Ra2RuntimeProfileId.Base,
				Ra2RuntimeProfileId.Base.ManifestSelection(),
				manifest,
				RetailFilenameCatalog.V1.DigestSha256,
				Map.CurrentMapFormat,
				1,
				1,
				deadline,
				clock,
				CancellationToken.None);
			Assert.That(expectation.Succeeded, Is.True);
			using var root = new DictionaryRuntimeRoot(fixture.Payloads);
			var verified = new IosRuntimeResourcePreflight(secondInputs).Check(
				expectation.Value,
				manifest,
				root,
				deadline,
				clock,
				CancellationToken.None);

			AssertClosedFailure(verified, RetailImportErrorCode.ProfileContextFailure,
				RetailImportDiagnosticCode.ProfileMetadataMismatch);
			Assert.That(root.Acquired, Is.Empty);
		}

		[Test]
		public void OneAuthenticatedResourceByteChangesOnlyTheExpectedContractContentIdentity()
		{
			var original = RuntimeFixture.Load();
			var changed = RuntimeFixture.Load(mutateFirstPayload: true);
			using var package = new Folder(Path.Combine(RepositoryRoot(), "mods", "ra2"));
			using var manifest = new Manifest("ra2", package, "mod.ios-base.yaml");
			var buildIdentity = FixedBuildIdentity();
			var clock = new TestClock();
			var deadline = RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(1));
			var first = new Ra2RuntimeContractBuilder(
				new RuntimeContractInputs(original.Inventory, buildIdentity)).BuildExpected(
					Ra2RuntimeProfileId.Base,
					Ra2RuntimeProfileId.Base.ManifestSelection(),
					manifest,
					RetailFilenameCatalog.V1.DigestSha256,
					Map.CurrentMapFormat,
					1,
					1,
					deadline,
					clock,
					CancellationToken.None);
			var second = new Ra2RuntimeContractBuilder(
				new RuntimeContractInputs(changed.Inventory, buildIdentity)).BuildExpected(
					Ra2RuntimeProfileId.Base,
					Ra2RuntimeProfileId.Base.ManifestSelection(),
					manifest,
					RetailFilenameCatalog.V1.DigestSha256,
					Map.CurrentMapFormat,
					1,
					1,
					deadline,
					clock,
					CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(first.Succeeded, Is.True);
				Assert.That(second.Succeeded, Is.True);
				Assert.That(second.Value.Contract.ContractDigest,
					Is.Not.EqualTo(first.Value.Contract.ContractDigest));
				Assert.That(changed.Inventory.ShapeDigest, Is.EqualTo(original.Inventory.ShapeDigest));
				Assert.That(ReferenceEquals(second.Value.Inputs.BuildIdentity, buildIdentity), Is.True);
			});
		}

		[Test]
		public void ContractAuthorityTypesExposeNoPublicConstructionOrMutationSurface()
		{
			var types = new[]
			{
				typeof(ManagedImageIdentity),
				typeof(RuntimeBuildIdentity),
				typeof(Ra2RuntimeContract),
				typeof(Ra2RuntimeContractExpectation),
				typeof(VerifiedRa2RuntimeContract),
			};

			foreach (var type in types)
			{
				Assert.Multiple(() =>
				{
					Assert.That(type.IsSealed, Is.True, type.FullName);
					Assert.That(type.GetConstructors(
						System.Reflection.BindingFlags.Instance |
						System.Reflection.BindingFlags.Public |
						System.Reflection.BindingFlags.NonPublic).Any(constructor => constructor.IsPublic),
						Is.False, type.FullName);
					Assert.That(type.GetProperties(
						System.Reflection.BindingFlags.Instance |
						System.Reflection.BindingFlags.Public |
						System.Reflection.BindingFlags.NonPublic)
						.Any(property => property.GetSetMethod(nonPublic: true) != null),
						Is.False, type.FullName);
					Assert.That(type.GetMethods(
						System.Reflection.BindingFlags.Static |
						System.Reflection.BindingFlags.Public)
						.Any(method => method.ReturnType == type), Is.False, type.FullName);
					Assert.That(type.GetMethod(
						"<Clone>$",
						System.Reflection.BindingFlags.Instance |
						System.Reflection.BindingFlags.Public |
						System.Reflection.BindingFlags.NonPublic), Is.Null, type.FullName);
					Assert.That(type.GetProperty(
						"EqualityContract",
						System.Reflection.BindingFlags.Instance |
						System.Reflection.BindingFlags.Public |
						System.Reflection.BindingFlags.NonPublic), Is.Null, type.FullName);
				});
			}
		}

		[Test]
		public void RuntimeContractSeamsAreInternalAndEveryConcreteAuthorityIsSealed()
		{
			var interfaces = new[]
			{
				typeof(IRuntimeBuildIdentityProvider),
				typeof(IRuntimeManagedImageSource),
				typeof(IRuntimeManagedImageLease),
				typeof(IIosRuntimeResourceRoot),
				typeof(IIosRuntimeResourceLease),
			};
			var concreteTypes = new[]
			{
				typeof(RuntimeBuildIdentityReader),
				typeof(IosRuntimeManagedImageSource),
				typeof(IosRuntimeManagedImageLease),
				typeof(IosRuntimeResourceRoot),
				typeof(RuntimeContractInputs),
				typeof(Ra2RuntimeContractBuilder),
				typeof(IosRuntimeResourcePreflight),
			};

			foreach (var type in interfaces)
			{
				Assert.Multiple(() =>
				{
					Assert.That(type.IsInterface, Is.True, type.FullName);
					Assert.That(type.IsNotPublic, Is.True, type.FullName);
				});
			}

			foreach (var type in concreteTypes)
			{
				Assert.Multiple(() =>
				{
					Assert.That(type.IsNotPublic, Is.True, type.FullName);
					Assert.That(type.IsSealed, Is.True, type.FullName);
				});
			}
		}

		[Test]
		public void RuntimeContractAssembliesExposeOnlyTheAuditedProductionAndTestFriends()
		{
			var assemblies = new[]
			{
				typeof(Manifest).Assembly,
				typeof(RuntimeBuildIdentity).Assembly,
			};

			foreach (var assembly in assemblies)
			{
				var friends = assembly.GetCustomAttributes(
					typeof(System.Runtime.CompilerServices.InternalsVisibleToAttribute), inherit: false)
					.Cast<System.Runtime.CompilerServices.InternalsVisibleToAttribute>()
					.Select(attribute => attribute.AssemblyName.Split(',')[0])
					.OrderBy(name => name, StringComparer.Ordinal)
					.ToArray();
				Assert.That(friends, Is.EqualTo(new[] { "OpenRA.Test", "OpenRA.iOS" }),
					assembly.GetName().Name);
			}
		}

		[Test]
		[Timeout(120000)]
		public void DifferentlyNamedOrdinaryConsumerCannotCompileAgainstInternalAuthority()
		{
			var result = CompileOrdinaryRuntimeContractConsumer();
			Assert.That(result.ExitCode, Is.Not.Zero,
				"the ordinary consumer unexpectedly compiled" + Environment.NewLine + result.Diagnostics);
			Assert.That(result.Diagnostics, Does.Contain("CS0122"));
			foreach (var typeName in new[]
			{
				nameof(IRuntimeBuildIdentityProvider),
				nameof(IRuntimeManagedImageSource),
				nameof(IRuntimeManagedImageLease),
				nameof(IIosRuntimeResourceRoot),
				nameof(IIosRuntimeResourceLease),
				nameof(RuntimeBuildIdentityReader),
				nameof(IosRuntimeManagedImageSource),
				nameof(IosRuntimeResourceRoot),
				nameof(RuntimeContractInputs),
				nameof(Ra2RuntimeContractBuilder),
				nameof(IosRuntimeResourcePreflight),
			})
				Assert.That(result.Diagnostics, Does.Contain(typeName), typeName);
		}

		[Test]
		public void ProvisionalTask4AuthorityHasNoProductionConsumerCallEdge()
		{
			const System.Reflection.BindingFlags NonPublicInstance =
				System.Reflection.BindingFlags.Instance |
				System.Reflection.BindingFlags.NonPublic;
			var constructors = new[]
			{
				typeof(RuntimeContractInputs).GetConstructors(NonPublicInstance).Single(),
				typeof(Ra2RuntimeContractBuilder).GetConstructors(NonPublicInstance).Single(),
				typeof(IosRuntimeResourcePreflight).GetConstructors(NonPublicInstance)
					.Single(constructor => constructor.GetParameters()
						.Select(parameter => parameter.ParameterType)
						.SequenceEqual(new[] { typeof(RuntimeContractInputs) })),
			};
			foreach (var constructor in constructors)
				AssertNoProductionConstructorCallers(constructor);

			var wrapper = typeof(VerifiedRa2RuntimeContract);
			var unexpectedSignatureConsumers = wrapper.Assembly.GetTypes()
				.SelectMany(type => type.GetMethods(
					System.Reflection.BindingFlags.Instance |
					System.Reflection.BindingFlags.Static |
					System.Reflection.BindingFlags.Public |
					System.Reflection.BindingFlags.NonPublic))
				.Where(method => method.DeclaringType != typeof(IosRuntimeResourcePreflight) &&
					(TypeContains(method.ReturnType, wrapper) || method.GetParameters()
						.Any(parameter => TypeContains(parameter.ParameterType, wrapper))))
				.Select(method => method.DeclaringType.FullName + "." + method.Name)
				.ToArray();
			Assert.That(unexpectedSignatureConsumers, Is.Empty,
				"Task 14 is the first task permitted to add a production verified-wrapper consumer");
		}

		[Test]
		[Timeout(120000)]
		public void IosTrustedFriendUsesTheExactEvaluatedTask4CompileGraph()
		{
			var compileItems = EvaluateIosCompileItems();
			Assert.That(compileItems, Is.EqualTo(new[]
			{
				"AotPreserve.cs",
				"AppDelegate.cs",
				"GeneratedAotObjectRegistry.g.cs",
				"IosContentFileSystemLoader.cs",
				"IosContentImportCoordinator.cs",
				"IosMetricKitDiagnostics.cs",
				"IosNativeLanguage.cs",
				"IosRetailContentPicker.cs",
				"IosRuntimeBuildIdentityProvider.cs",
				"Program.cs",
				"ViewController.cs",
			}));
		}

		[Test]
		public void Task4AuthorityProducerEdgesAreExactlyAllowlistedAcrossProductionAssemblies()
		{
			var ra2Assembly = typeof(RuntimeBuildIdentity).Assembly.Location;
			var releaseRa2Assembly = IosReleaseAssemblyPath("OpenRA.Mods.RA2.dll");
			var iosAssembly = IosReleaseAssemblyPath("OpenRA.iOS.dll");
			const string Preflight = "OpenRA.Mods.RA2.Content.IosRuntimeResourcePreflight";
			const string Result = "OpenRA.Mods.RA2.Content.RetailOperationResult`1";
			const string Wrapper = "OpenRA.Mods.RA2.Content.VerifiedRa2RuntimeContract";
			const string Expectation = "OpenRA.Mods.RA2.Content.Ra2RuntimeContractExpectation";
			const string Manifest = "OpenRA.Manifest";
			const string Root = "OpenRA.Mods.RA2.Content.IIosRuntimeResourceRoot";
			const string ImmutableStrings = "System.Collections.Immutable.ImmutableArray`1<String>";
			const string Deadline = "OpenRA.Mods.RA2.Content.RetailValidationDeadline";
			const string Clock = "OpenRA.Mods.RA2.Content.IRetailMonotonicClock";
			const string Token = "System.Threading.CancellationToken";
			const string ContractResult = Result + "<" + Wrapper + ">";
			const string Check = "instance " + Preflight + ".Check<arity=0>(" +
				Expectation + "," + Manifest + "," + Root + "," + Deadline + "," +
				Clock + "," + Token + "):" + ContractResult;
			const string CheckRetained = "instance " + Preflight +
				".CheckVerifiedWithRetainedLeases<arity=0>(" + Expectation + "," +
				Manifest + "," + Root + "," + ImmutableStrings + "," + Deadline + "," +
				Clock + "," + Token + "):" + ContractResult;
			const string Failed = "static " + ContractResult + ".Failed<arity=0>(" +
				"OpenRA.Mods.RA2.Content.RetailImportFailure):" + Result + "<!0>";
			const string Success = "static " + ContractResult + ".Success<arity=0>(!0):" +
				Result + "<!0>";
			const string VerifiedConstructor = "instance " + Wrapper + "..ctor<arity=0>(" +
				Expectation + "," + ImmutableStrings + "):Void";
			const string SnapshotCompatibility = "static " + Preflight +
				".TrySnapshotMapCompatibility<arity=0>(" + Expectation + "," + Manifest +
				",out " + ImmutableStrings + "):Boolean";
			const string ValidateCompatibility = "static " + Wrapper +
				".ValidateMapCompatibility<arity=0>(" + ImmutableStrings + ",String):Void";
			var expectedRa2Edges = new[]
			{
				"instance OpenRA.Mods.RA2.Content.IosRuntimeManagedImageSource." +
					"AcquirePinned<arity=0>(String):OpenRA.Mods.RA2.Content.IRuntimeManagedImageLease" +
					" -> newobj instance OpenRA.Mods.RA2.Content.IosRuntimeManagedImageLease." +
					".ctor<arity=0>(OpenRA.Mods.RA2.Content.IIosRuntimeResourceLease," +
					"System.IO.Stream,String,System.Guid,System.Nullable`1<System.Guid>):Void",
				CheckRetained + " -> newobj " + VerifiedConstructor,
				"instance OpenRA.Mods.RA2.Content.IosRuntimeResourceRoot." +
					"CreateManagedImageSource<arity=0>():" + Result +
					"<OpenRA.Mods.RA2.Content.IosRuntimeManagedImageSource> -> newobj instance " +
					"OpenRA.Mods.RA2.Content.IosRuntimeManagedImageSource..ctor<arity=0>(" +
					"OpenRA.Mods.RA2.Content.IosRuntimeResourceRoot):Void",
				"instance OpenRA.Mods.RA2.Content.Ra2RuntimeContractBuilder." +
					"BuildExpected<arity=0>(OpenRA.Mods.RA2.Content.Ra2RuntimeProfileId," +
					"OpenRA.TrustedManifestSelection," + Manifest + ",String,Int32,Int32,Int32," +
					Deadline + "," + Clock + "," + Token + "):" + Result + "<" + Expectation +
					"> -> newobj instance OpenRA.Mods.RA2.Content.Ra2RuntimeContract." +
					".ctor<arity=0>(OpenRA.Mods.RA2.Content.Ra2RuntimeProfileId," +
					"String,String,Int32):Void",
				"instance OpenRA.Mods.RA2.Content.Ra2RuntimeContractBuilder." +
					"BuildExpected<arity=0>(OpenRA.Mods.RA2.Content.Ra2RuntimeProfileId," +
					"OpenRA.TrustedManifestSelection," + Manifest + ",String,Int32,Int32,Int32," +
					Deadline + "," + Clock + "," + Token + "):" + Result + "<" + Expectation +
					"> -> newobj instance " + Expectation + "..ctor<arity=0>(" +
					"OpenRA.Mods.RA2.Content.RuntimeContractInputs," +
					"OpenRA.Mods.RA2.Content.Ra2RuntimeContract,OpenRA.TrustedManifestSelection," +
					ImmutableStrings + "," + ImmutableStrings + "," + ImmutableStrings + "):Void",
				"instance OpenRA.Mods.RA2.Content.RuntimeBuildIdentityReader.Capture<arity=0>(" +
					Deadline + "," + Clock + "," + Token + "):" + Result +
					"<OpenRA.Mods.RA2.Content.RuntimeBuildIdentity> -> newobj instance " +
					"OpenRA.Mods.RA2.Content.ManagedImageIdentity..ctor<arity=0>(" +
					"String,String,System.Guid):Void",
				"instance OpenRA.Mods.RA2.Content.RuntimeBuildIdentityReader.Capture<arity=0>(" +
					Deadline + "," + Clock + "," + Token + "):" + Result +
					"<OpenRA.Mods.RA2.Content.RuntimeBuildIdentity> -> newobj instance " +
					"OpenRA.Mods.RA2.Content.RuntimeBuildIdentity..ctor<arity=0>(" +
					"System.Collections.Immutable.ImmutableArray`1<" +
					"OpenRA.Mods.RA2.Content.ManagedImageIdentity>,String):Void",
			};
			var expectedIosEdges = new[]
			{
				"instance OpenRA.iOS.IosRuntimeBuildIdentityProvider..ctor<arity=0>(" +
					"OpenRA.Mods.RA2.Content.IosRuntimeManagedImageSource,String):Void -> newobj " +
					"instance OpenRA.Mods.RA2.Content.RuntimeBuildIdentityReader..ctor<arity=0>(" +
					"OpenRA.Mods.RA2.Content.IRuntimeManagedImageSource,String):Void",
				"instance OpenRA.iOS.IosRuntimeBuildIdentityProvider.Capture<arity=0>(" +
					Deadline + "," + Clock + "," + Token + "):" + Result +
					"<OpenRA.Mods.RA2.Content.RuntimeBuildIdentity> -> callvirt instance " +
					"OpenRA.Mods.RA2.Content.RuntimeBuildIdentityReader.Capture<arity=0>(" +
					Deadline + "," + Clock + "," + Token + "):" + Result +
					"<OpenRA.Mods.RA2.Content.RuntimeBuildIdentity>",
			};
			var expectedDebugWrapperUseGroups = new[]
			{
				"1|il " + Check + " -> call " + CheckRetained,
				"3|il " + Check + " -> call " + Failed,
				"19|il " + CheckRetained + " -> call " + Failed,
				"1|il " + CheckRetained + " -> call " + Success,
				"1|il " + CheckRetained + " -> newobj " + VerifiedConstructor,
				"1|il " + SnapshotCompatibility + " -> call " + ValidateCompatibility,
				"1|local " + Check + "[3]",
				"1|local " + CheckRetained + "[3]",
				"1|method-return " + Check,
				"1|method-return " + CheckRetained,
			};
			var expectedReleaseWrapperUseGroups = new[]
			{
				"1|il " + Check + " -> call " + CheckRetained,
				"3|il " + Check + " -> call " + Failed,
				"19|il " + CheckRetained + " -> call " + Failed,
				"1|il " + CheckRetained + " -> call " + Success,
				"1|il " + CheckRetained + " -> newobj " + VerifiedConstructor,
				"1|il " + SnapshotCompatibility + " -> call " + ValidateCompatibility,
				"1|local " + CheckRetained + "[13]",
				"1|method-return " + Check,
				"1|method-return " + CheckRetained,
			};

			Assert.Multiple(() =>
			{
				Assert.That(ReadAuthorityIlEdges(ra2Assembly), Is.EqualTo(expectedRa2Edges));
				Assert.That(ReadAuthorityIlEdges(releaseRa2Assembly), Is.EqualTo(expectedRa2Edges));
				Assert.That(ReadAuthorityIlEdges(iosAssembly), Is.EqualTo(expectedIosEdges));
				Assert.That(GroupAuthorityUses(ReadProvisionalWrapperUses(ra2Assembly)),
					Is.EqualTo(expectedDebugWrapperUseGroups));
				Assert.That(GroupAuthorityUses(ReadProvisionalWrapperUses(releaseRa2Assembly)),
					Is.EqualTo(expectedReleaseWrapperUseGroups));
				Assert.That(ReadProvisionalWrapperUses(iosAssembly), Is.Empty, "iOS wrapper uses");
				Assert.That(ReadReferencedProvisionalContractTypes(iosAssembly), Is.Empty,
					"Task 14 is the first task permitted to add an iOS production contract consumer");
			});
		}

		[Test]
		[Timeout(120000)]
		public void AuthorityIlScannerDetectsSameAssemblyProviderConsumersAndFunctionPointers()
		{
			const string Source =
				"using System;\n" +
				"namespace OpenRA.iOS\n" +
				"{\n" +
				"  internal class IosRuntimeBuildIdentityProvider\n" +
				"  {\n" +
				"    internal IosRuntimeBuildIdentityProvider() { }\n" +
				"    internal virtual void Capture() { }\n" +
				"    internal static void Capture(int value) { }\n" +
				"  }\n" +
				"  internal static class StartupConsumer\n" +
				"  {\n" +
				"    internal static object Construct() => new IosRuntimeBuildIdentityProvider();\n" +
				"    internal static void Invoke(IosRuntimeBuildIdentityProvider provider) => provider.Capture();\n" +
				"    internal static Action InstanceDelegate(IosRuntimeBuildIdentityProvider provider) => provider.Capture;\n" +
				"    internal static Action<int> StaticDelegate() => IosRuntimeBuildIdentityProvider.Capture;\n" +
				"  }\n" +
				"}\n";
			var edges = CompileAuthorityTamperFixture(
				"OpenRA.iOS", Source, ReadAuthorityIlEdges);

			Assert.That(edges, Is.EqualTo(new[]
			{
				"static OpenRA.iOS.StartupConsumer.Construct<arity=0>():Object -> newobj " +
					"instance OpenRA.iOS.IosRuntimeBuildIdentityProvider..ctor<arity=0>():Void",
				"static OpenRA.iOS.StartupConsumer.InstanceDelegate<arity=0>(" +
					"OpenRA.iOS.IosRuntimeBuildIdentityProvider):System.Action -> ldvirtftn " +
					"instance OpenRA.iOS.IosRuntimeBuildIdentityProvider.Capture<arity=0>():Void",
				"static OpenRA.iOS.StartupConsumer.Invoke<arity=0>(" +
					"OpenRA.iOS.IosRuntimeBuildIdentityProvider):Void -> callvirt instance " +
					"OpenRA.iOS.IosRuntimeBuildIdentityProvider.Capture<arity=0>():Void",
				"static OpenRA.iOS.StartupConsumer.StaticDelegate<arity=0>():" +
					"System.Action`1<Int32> -> ldftn static OpenRA.iOS.IosRuntimeBuildIdentityProvider." +
					"Capture<arity=0>(Int32):Void",
			}));
		}

		[Test]
		[Timeout(120000)]
		public void AuthorityMetadataScannerDetectsEveryProvisionalWrapperConsumerShape()
		{
			const string Source =
				"using System;\n" +
				"namespace OpenRA.Mods.RA2.Content\n" +
				"{\n" +
				"  internal sealed class VerifiedRa2RuntimeContract { }\n" +
				"  internal sealed class ForbiddenConsumer\n" +
				"  {\n" +
				"    internal VerifiedRa2RuntimeContract Field;\n" +
				"    internal VerifiedRa2RuntimeContract Property => Field;\n" +
				"    internal VerifiedRa2RuntimeContract ReadProperty() => Property;\n" +
				"    internal VerifiedRa2RuntimeContract Cast(object value) => (VerifiedRa2RuntimeContract)value;\n" +
				"    internal void Local(object value)\n" +
				"    {\n" +
				"      var local = (VerifiedRa2RuntimeContract)value;\n" +
				"      GC.KeepAlive(local);\n" +
				"    }\n" +
				"    internal void Parameter(VerifiedRa2RuntimeContract value) { }\n" +
				"  }\n" +
				"}\n";
			var uses = CompileAuthorityTamperFixture(
				"OpenRA.Mods.RA2", Source, ReadProvisionalWrapperUses);
			const string ExternalSource =
				"using OpenRA.Mods.RA2.Content;\n" +
				"namespace OpenRA.iOS\n" +
				"{\n" +
				"  internal sealed class ForbiddenExternalConsumer\n" +
				"  {\n" +
				"    internal VerifiedRa2RuntimeContract Field;\n" +
				"    internal object Cast(object value) => (VerifiedRa2RuntimeContract)value;\n" +
				"  }\n" +
				"}\n";
			var externalUses = CompileAuthorityTamperFixture(
				"OpenRA.iOS",
				ExternalSource,
				ReadProvisionalWrapperUses,
				typeof(RuntimeBuildIdentity).Assembly.Location);

			Assert.Multiple(() =>
			{
				Assert.That(uses.Any(use => use.StartsWith("field ", StringComparison.Ordinal)), Is.True);
				Assert.That(uses.Any(use => use.StartsWith("property ", StringComparison.Ordinal)), Is.True);
				Assert.That(uses.Any(use => use.StartsWith("method-return ", StringComparison.Ordinal)), Is.True);
				Assert.That(uses.Any(use => use.StartsWith("method-parameter ", StringComparison.Ordinal)), Is.True);
				Assert.That(uses.Any(use => use.StartsWith("local ", StringComparison.Ordinal)), Is.True);
				Assert.That(uses.Any(use => use.Contains("castclass", StringComparison.Ordinal)), Is.True);
				Assert.That(uses.Any(use =>
					use.StartsWith(
						"il instance OpenRA.Mods.RA2.Content.ForbiddenConsumer." +
						"get_Property<arity=0>(",
						StringComparison.Ordinal) &&
					use.Contains(
						" -> ldfld OpenRA.Mods.RA2.Content.ForbiddenConsumer.Field:",
						StringComparison.Ordinal)), Is.True);
				Assert.That(uses.Any(use =>
					use.StartsWith(
						"il instance OpenRA.Mods.RA2.Content.ForbiddenConsumer." +
						"ReadProperty<arity=0>(",
						StringComparison.Ordinal) &&
					use.Contains(
						" -> call instance OpenRA.Mods.RA2.Content.ForbiddenConsumer." +
						"get_Property<arity=0>(",
						StringComparison.Ordinal)), Is.True);
				Assert.That(externalUses.Any(use => use.StartsWith("field ", StringComparison.Ordinal)), Is.True);
				Assert.That(externalUses.Any(use => use.Contains("castclass", StringComparison.Ordinal)), Is.True);
			});
		}

		[Test]
		[Timeout(120000)]
		public void AuthorityScannersDistinguishSameNameCallerAndTargetOverloads()
		{
			const string ProviderSource =
				"namespace OpenRA.iOS\n" +
				"{\n" +
				"  internal class IosRuntimeBuildIdentityProvider\n" +
				"  {\n" +
				"    internal IosRuntimeBuildIdentityProvider() { }\n" +
				"    internal IosRuntimeBuildIdentityProvider(int value) { }\n" +
				"    internal virtual void Capture() { }\n" +
				"    internal virtual void Capture(int value) { }\n" +
				"  }\n" +
				"  internal static class StartupConsumer\n" +
				"  {\n" +
				"    internal static object Construct() => new IosRuntimeBuildIdentityProvider();\n" +
				"    internal static object Construct(int value) => new IosRuntimeBuildIdentityProvider(value);\n" +
				"    internal static void Invoke(IosRuntimeBuildIdentityProvider provider) => provider.Capture();\n" +
				"    internal static void Invoke(IosRuntimeBuildIdentityProvider provider, int value) => provider.Capture(value);\n" +
				"  }\n" +
				"}\n";
			var providerEdges = CompileAuthorityTamperFixture(
				"OpenRA.iOS", ProviderSource, ReadAuthorityIlEdges);
			const string WrapperSource =
				"namespace OpenRA.Mods.RA2.Content\n" +
				"{\n" +
				"  internal sealed class VerifiedRa2RuntimeContract { }\n" +
				"  internal static class Producer\n" +
				"  {\n" +
				"    internal static VerifiedRa2RuntimeContract Check() => new VerifiedRa2RuntimeContract();\n" +
				"    internal static VerifiedRa2RuntimeContract Check(int value) => new VerifiedRa2RuntimeContract();\n" +
				"  }\n" +
				"}\n";
			var wrapperUses = CompileAuthorityTamperFixture(
				"OpenRA.Mods.RA2", WrapperSource, ReadProvisionalWrapperUses);

			Assert.Multiple(() =>
			{
				Assert.That(providerEdges.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(4));
				Assert.That(wrapperUses.Count(use => use.StartsWith(
					"method-return static OpenRA.Mods.RA2.Content.Producer.Check<arity=0>(",
					StringComparison.Ordinal)),
					Is.EqualTo(2));
				Assert.That(wrapperUses.Count(use => use.StartsWith(
					"il static OpenRA.Mods.RA2.Content.Producer.Check<arity=0>(",
					StringComparison.Ordinal) && use.Contains(" -> newobj ", StringComparison.Ordinal)),
					Is.EqualTo(2));
			});
		}

		[Test]
		public void IosBuildIdentityProviderOwnsOneConcreteChildAndHasNoAlternateRootPath()
		{
			var path = Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "IosRuntimeBuildIdentityProvider.cs");
			Assert.That(File.Exists(path), Is.True,
				"The iOS build-identity provider has not been implemented.");
			var source = File.ReadAllText(path);
			var capture = source.IndexOf(
				"public RetailOperationResult<RuntimeBuildIdentity> Capture(", StringComparison.Ordinal);
			var clockValidation = source.IndexOf("MatchesClock(clock)", capture, StringComparison.Ordinal);
			var checkpoint = source.IndexOf(
				"RetailRuntimeContractFailures.Checkpoint(", capture, StringComparison.Ordinal);
			var stateLock = source.IndexOf("lock (sync)", capture, StringComparison.Ordinal);

			Assert.Multiple(() =>
			{
				Assert.That(source, Does.Match(
					@"internal\s+sealed\s+class\s+IosRuntimeBuildIdentityProvider\s*:\s*IRuntimeBuildIdentityProvider\s*,\s*IDisposable"));
				Assert.That(source, Does.Match(
					@"IosRuntimeBuildIdentityProvider\s*\(\s*IosRuntimeManagedImageSource\s+ownedBundleImages\s*,\s*string\s+canonicalAotRegistryIdentity\s*\)"));
				Assert.That(Regex.Matches(source, @"new\s+RuntimeBuildIdentityReader\s*\(").Count,
					Is.EqualTo(1));
				Assert.That(source, Does.Contain("activeCaptures"));
				Assert.That(source, Does.Contain("Monitor.PulseAll"));
				Assert.That(clockValidation, Is.GreaterThan(capture));
				Assert.That(checkpoint, Is.GreaterThan(clockValidation));
				Assert.That(stateLock, Is.GreaterThan(checkpoint),
					"clock/token/deadline precedence must be resolved before the disposed shortcut");
				Assert.That(source, Does.Not.Contain("RetailValidationDeadline.Start"));
				Assert.That(source, Does.Not.Contain("NSBundle"));
				Assert.That(source, Does.Not.Contain("File.Open"));
				Assert.That(source, Does.Not.Contain("OpenBundleRoot"));
			});
		}

		static readonly IReadOnlyDictionary<ushort, System.Reflection.Emit.OpCode> IlOpCodes =
			typeof(System.Reflection.Emit.OpCodes).GetFields(
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
			.Where(field => field.FieldType == typeof(System.Reflection.Emit.OpCode))
			.Select(field => (System.Reflection.Emit.OpCode)field.GetValue(null))
			.GroupBy(operation => unchecked((ushort)operation.Value))
			.ToDictionary(group => group.Key, group => group.First());

		static readonly ImmutableArray<string> ProjectImageNames = ImmutableArray.Create(
			"Framework.Dependency.dll",
			"OpenRA.Game.dll",
			"OpenRA.Mods.Cnc.dll",
			"OpenRA.Mods.Common.dll",
			"OpenRA.Mods.RA2.dll",
			"OpenRA.Platforms.Default.dll",
			"OpenRA.iOS.dll",
			"ThirdParty.Parser.dll");

		[DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
		static extern int MakeFifo(string path, uint mode);

		static string MinimalManifest(string fileSystemChildren = "", string extra = "") =>
			"Metadata:\n" +
			"\tTitle: Test\n" +
			"\tVersion: test\n" +
			"\tWebsite: https://example.invalid\n" +
			"\tWebIcon32: icon.png\n" +
			"\tWindowTitle: Test\n" +
			"FileSystem: TestFileSystem\n" + fileSystemChildren +
			extra +
			"LoadScreen: TestLoadScreen\n";

		static ImmutableArray<string> GoldenContractLogicalNames(
			string repository, Ra2RuntimeProfileId profile)
		{
			using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
				repository, "packaging", "ios-runtime-contract-descriptor-shape-v1.json")));
			return document.RootElement.GetProperty("descriptors").EnumerateArray()
				.Where(item => item.GetProperty("purpose").GetString() == "Contract" &&
					item.GetProperty("profiles").EnumerateArray()
						.Any(value => value.GetString() == profile.ToString()))
				.Select(item => item.GetProperty("logicalName").GetString())
				.ToImmutableArray();
		}

		static RuntimeBuildIdentity FixedBuildIdentity()
		{
			var images = ImmutableArray.CreateBuilder<ManagedImageIdentity>(ProjectImageNames.Length);
			for (var index = 0; index < ProjectImageNames.Length; index++)
			{
				var digest = Convert.ToHexString(SHA256.HashData(
					Encoding.UTF8.GetBytes("managed-image:" + ProjectImageNames[index])))
					.ToLowerInvariant();
				var mvid = new Guid(index + 1, 2, 3,
					4, 5, 6, 7, 8, 9, 10, checked((byte)(11 + index)));
				images.Add(new ManagedImageIdentity(ProjectImageNames[index], digest, mvid));
			}

			return new RuntimeBuildIdentity(images.ToImmutable(), new string('b', 64));
		}

		static Guid ReadMvid(byte[] bytes)
		{
			using var stream = new MemoryStream(bytes, writable: false);
			using var portableExecutable = new PEReader(stream, PEStreamOptions.LeaveOpen);
			var metadata = portableExecutable.GetMetadataReader();
			return metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
		}

		static byte[] SmallManagedImageBytes()
		{
			var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "OpenRA.Utility.dll");
			return File.ReadAllBytes(File.Exists(path) ? path : typeof(Manifest).Assembly.Location);
		}

		static byte[] RewriteMvid(byte[] bytes, Guid current, Guid replacement)
		{
			var copy = bytes.ToArray();
			var currentBytes = current.ToByteArray();
			var replacementBytes = replacement.ToByteArray();
			for (var offset = 0; offset <= copy.Length - currentBytes.Length; offset++)
			{
				if (!copy.AsSpan(offset, currentBytes.Length).SequenceEqual(currentBytes))
					continue;

				Buffer.BlockCopy(replacementBytes, 0, copy, offset, replacementBytes.Length);
				return copy;
			}

			throw new InvalidDataException("The controlled PE fixture does not expose its MVID bytes.");
		}

		static byte[] WriteIndependentContractFrame(
			Ra2RuntimeProfileId profile,
			TrustedManifestSelection selection,
			Manifest manifest,
			RuntimeContractDescriptorInventory inventory,
			RuntimeBuildIdentity buildIdentity,
			string filenameCatalogDigest,
			int mapFormatVersion,
			int archiveValidatorVersion,
			int mapValidatorVersion,
			ContractFramePerturbation perturbation)
		{
			using var output = new MemoryStream();
			IndependentWriteString(output, perturbation == ContractFramePerturbation.Magic
				? "OpenRA.RA2RuntimeContract.changed" : "OpenRA.RA2RuntimeContract");
			IndependentWriteUInt32(output, perturbation == ContractFramePerturbation.SchemaVersion ? 2U : 1U);
			var profileTag = profile == Ra2RuntimeProfileId.Base ? 1U : 2U;
			IndependentWriteUInt32(output,
				perturbation == ContractFramePerturbation.ProfileTag ? profileTag + 7U : profileTag);
			IndependentWriteString(output, perturbation == ContractFramePerturbation.SelectionModId
				? selection.ModId + "-changed" : selection.ModId);
			IndependentWriteString(output, perturbation == ContractFramePerturbation.SelectionEntryName
				? selection.EntryName + ".changed" : selection.EntryName);

			IndependentWriteUInt32(output, checked((uint)(manifest.IncludedFiles.Length +
				(perturbation == ContractFramePerturbation.IncludedFileCount ? 1 : 0))));
			for (var index = 0; index < manifest.IncludedFiles.Length; index++)
				IndependentWriteString(output,
					perturbation == ContractFramePerturbation.IncludedFile && index == 0
						? manifest.IncludedFiles[index] + ".changed" : manifest.IncludedFiles[index]);

			IndependentWriteUInt32(output, checked((uint)(manifest.RuntimeContractCultures.Length +
				(perturbation == ContractFramePerturbation.CultureCount ? 1 : 0))));
			for (var index = 0; index < manifest.RuntimeContractCultures.Length; index++)
				IndependentWriteString(output,
					perturbation == ContractFramePerturbation.Culture && index == 0
						? manifest.RuntimeContractCultures[index] + "-changed"
						: manifest.RuntimeContractCultures[index]);

			IndependentWriteUInt32(output, checked((uint)(inventory.SchemaVersion +
				(perturbation == ContractFramePerturbation.InventorySchema ? 1 : 0))));
			IndependentWriteUInt32(output, checked((uint)(inventory.DescriptorCount +
				(perturbation == ContractFramePerturbation.InventoryDescriptorCount ? 1 : 0))));
			IndependentWriteHex(output, inventory.ShapeDigest,
				perturbation == ContractFramePerturbation.ShapeDigest);

			var applicable = inventory.Descriptors
				.Select((descriptor, ordinal) => (Descriptor: descriptor, Ordinal: ordinal))
				.Where(row => row.Descriptor.Profiles.Contains(profile))
				.ToImmutableArray();
			IndependentWriteUInt32(output, checked((uint)(applicable.Length +
				(perturbation == ContractFramePerturbation.ApplicableDescriptorCount ? 1 : 0))));
			for (var index = 0; index < applicable.Length; index++)
			{
				var (descriptor, ordinal) = applicable[index];
				var first = index == 0;
				IndependentWriteUInt32(output, checked((uint)(ordinal +
					(first && perturbation == ContractFramePerturbation.GlobalOrdinal ? 1 : 0))));
				IndependentWriteUInt32(output, checked((uint)((int)descriptor.Purpose +
					(first && perturbation == ContractFramePerturbation.PurposeTag ? 7 : 0))));
				IndependentWriteUInt32(output, checked((uint)(descriptor.Profiles.Length +
					(first && perturbation == ContractFramePerturbation.DescriptorProfileCount ? 1 : 0))));
				for (var profileIndex = 0; profileIndex < descriptor.Profiles.Length; profileIndex++)
				{
					var descriptorProfile = descriptor.Profiles[profileIndex];
					var descriptorProfileTag = descriptorProfile == Ra2RuntimeProfileId.Base ? 1U : 2U;
					IndependentWriteUInt32(output,
						first && profileIndex == 0 &&
						perturbation == ContractFramePerturbation.DescriptorProfileTag
							? descriptorProfileTag + 7U : descriptorProfileTag);
				}

				IndependentWriteString(output,
					first && perturbation == ContractFramePerturbation.LogicalName
						? descriptor.LogicalName + ".changed" : descriptor.LogicalName);
				IndependentWriteString(output,
					first && perturbation == ContractFramePerturbation.BundleRelativePath
						? descriptor.CanonicalBundleRelativePath + ".changed"
						: descriptor.CanonicalBundleRelativePath);
				IndependentWriteUInt64(output, checked((ulong)(descriptor.Length +
					(first && perturbation == ContractFramePerturbation.DescriptorLength ? 1 : 0))));
				IndependentWriteHex(output, descriptor.Sha256,
					first && perturbation == ContractFramePerturbation.DescriptorContentDigest);
			}

			IndependentWriteHex(output, filenameCatalogDigest,
				perturbation == ContractFramePerturbation.FilenameCatalogDigest);
			IndependentWriteUInt32(output, checked((uint)(mapFormatVersion +
				(perturbation == ContractFramePerturbation.MapFormatVersion ? 1 : 0))));
			IndependentWriteUInt32(output, checked((uint)(archiveValidatorVersion +
				(perturbation == ContractFramePerturbation.ArchiveValidatorVersion ? 1 : 0))));
			IndependentWriteUInt32(output, checked((uint)(mapValidatorVersion +
				(perturbation == ContractFramePerturbation.MapValidatorVersion ? 1 : 0))));

			IndependentWriteUInt32(output, checked((uint)(buildIdentity.ManagedImages.Length +
				(perturbation == ContractFramePerturbation.ManagedImageCount ? 1 : 0))));
			for (var index = 0; index < buildIdentity.ManagedImages.Length; index++)
			{
				var image = buildIdentity.ManagedImages[index];
				var first = index == 0;
				IndependentWriteString(output,
					first && perturbation == ContractFramePerturbation.ManagedImageFilename
						? "Changed." + image.BundleFilename : image.BundleFilename);
				IndependentWriteHex(output, image.Sha256,
					first && perturbation == ContractFramePerturbation.ManagedImageDigest);
				var mvid = first && perturbation == ContractFramePerturbation.ManagedImageMvid
					? new Guid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") : image.ModuleMvid;
				var mvidBytes = Convert.FromHexString(mvid.ToString("N"));
				output.Write(mvidBytes, 0, mvidBytes.Length);
			}

			IndependentWriteHex(output, buildIdentity.AotRegistryIdentity,
				perturbation == ContractFramePerturbation.AotRegistryDigest);
			if (perturbation == ContractFramePerturbation.TrailingByte)
				output.WriteByte(0);
			return output.ToArray();
		}

		static void IndependentWriteString(Stream output, string value)
		{
			if (!value.IsNormalized(NormalizationForm.FormC))
				throw new InvalidDataException("The independent framing vector requires NFC strings.");
			var bytes = new UTF8Encoding(false, true).GetBytes(value);
			IndependentWriteUInt32(output, checked((uint)bytes.Length));
			output.Write(bytes, 0, bytes.Length);
		}

		static void IndependentWriteHex(Stream output, string value, bool perturb)
		{
			var bytes = Convert.FromHexString(value);
			if (bytes.Length != 32)
				throw new InvalidDataException("The independent framing vector requires SHA-256 values.");
			if (perturb)
				bytes[0] ^= 1;
			output.Write(bytes, 0, bytes.Length);
		}

		static void IndependentWriteUInt32(Stream output, uint value)
		{
			for (var shift = 0; shift < 32; shift += 8)
				output.WriteByte((byte)(value >> shift));
		}

		static void IndependentWriteUInt64(Stream output, ulong value)
		{
			for (var shift = 0; shift < 64; shift += 8)
				output.WriteByte((byte)(value >> shift));
		}

		static void AssertClosedFailure<T>(
			RetailOperationResult<T> result,
			RetailImportErrorCode code,
			RetailImportDiagnosticCode diagnostic)
		{
			Assert.That(result.Succeeded, Is.False);
			Assert.Multiple(() =>
			{
				Assert.That(result.Failure.Code, Is.EqualTo(code));
				Assert.That(result.Failure.Group, Is.Null);
				Assert.That(result.Failure.SafeBasename, Is.Null);
				Assert.That(result.Failure.Diagnostic, Is.EqualTo(diagnostic));
			});
		}

		static T PrivateField<T>(object target, string name)
		{
			var field = target.GetType().GetField(
				name,
				System.Reflection.BindingFlags.Instance |
				System.Reflection.BindingFlags.NonPublic);
			Assert.That(field, Is.Not.Null, target.GetType().FullName + "." + name);
			return (T)field.GetValue(target);
		}

		static ProcessBuildResult CompileOrdinaryRuntimeContractConsumer()
		{
			using var temporary = new TemporaryDirectory("openra-ordinary-contract-consumer");
			var ra2Assembly = System.Security.SecurityElement.Escape(
				typeof(RuntimeBuildIdentity).Assembly.Location);
			var gameAssembly = System.Security.SecurityElement.Escape(typeof(Manifest).Assembly.Location);
			var project =
				"<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
				"  <PropertyGroup>\n" +
				"    <TargetFramework>net6.0</TargetFramework>\n" +
				"    <AssemblyName>Ordinary.Runtime.Contract.Consumer</AssemblyName>\n" +
				"    <NuGetAudit>false</NuGetAudit>\n" +
				"    <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>\n" +
				"  </PropertyGroup>\n" +
				"  <ItemGroup>\n" +
				"    <Reference Include=\"OpenRA.Mods.RA2\"><HintPath>" + ra2Assembly +
				"</HintPath></Reference>\n" +
				"    <Reference Include=\"OpenRA.Game\"><HintPath>" + gameAssembly +
				"</HintPath></Reference>\n" +
				"  </ItemGroup>\n" +
				"</Project>\n";
			const string Source =
				"using System;\n" +
				"using OpenRA.Mods.RA2.Content;\n" +
				"public static class OrdinaryConsumer\n" +
				"{\n" +
				"  public static Type PublicControl => typeof(RuntimeBuildIdentity);\n" +
				"  public static readonly Type[] Forbidden = new[]\n" +
				"  {\n" +
				"    typeof(IRuntimeBuildIdentityProvider),\n" +
				"    typeof(IRuntimeManagedImageSource),\n" +
				"    typeof(IRuntimeManagedImageLease),\n" +
				"    typeof(IIosRuntimeResourceRoot),\n" +
				"    typeof(IIosRuntimeResourceLease),\n" +
				"    typeof(RuntimeBuildIdentityReader),\n" +
				"    typeof(IosRuntimeManagedImageSource),\n" +
				"    typeof(IosRuntimeResourceRoot),\n" +
				"    typeof(RuntimeContractInputs),\n" +
				"    typeof(Ra2RuntimeContractBuilder),\n" +
				"    typeof(IosRuntimeResourcePreflight),\n" +
				"  };\n" +
				"}\n";
			var projectPath = Path.Combine(temporary.Path, "OrdinaryConsumer.csproj");
			File.WriteAllText(projectPath, project, new UTF8Encoding(false));
			File.WriteAllText(
				Path.Combine(temporary.Path, "OrdinaryConsumer.cs"), Source, new UTF8Encoding(false));

			var start = new System.Diagnostics.ProcessStartInfo
			{
				FileName = DotnetHost(),
				WorkingDirectory = temporary.Path,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			};
			start.ArgumentList.Add("build");
			start.ArgumentList.Add(projectPath);
			start.ArgumentList.Add("--configuration");
			start.ArgumentList.Add("Release");
			start.ArgumentList.Add("--nologo");
			start.ArgumentList.Add("-p:NuGetAudit=false");
			start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
			using var process = System.Diagnostics.Process.Start(start) ??
				throw new InvalidOperationException("Could not start the ordinary-consumer compiler.");
			var stdout = process.StandardOutput.ReadToEndAsync();
			var stderr = process.StandardError.ReadToEndAsync();
			if (!process.WaitForExit(90000))
			{
				process.Kill(true);
				Assert.Fail("Timed out while compiling the ordinary runtime-contract consumer.");
			}

			return new ProcessBuildResult(
				process.ExitCode,
				stdout.GetAwaiter().GetResult() + Environment.NewLine + stderr.GetAwaiter().GetResult());
		}

		static string[] CompileAuthorityTamperFixture(
			string assemblyName,
			string source,
			Func<string, string[]> inspect,
			string referencePath = null)
		{
			using var temporary = new TemporaryDirectory("openra-authority-tamper");
			var reference = referencePath == null ? string.Empty :
				"  <ItemGroup>\n" +
				"    <Reference Include=\"OpenRA.Mods.RA2\"><HintPath>" + referencePath +
				"</HintPath></Reference>\n" +
				"  </ItemGroup>\n";
			var project =
				"<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
				"  <PropertyGroup>\n" +
				"    <TargetFramework>net6.0</TargetFramework>\n" +
				"    <AssemblyName>" + assemblyName + "</AssemblyName>\n" +
				"    <Optimize>false</Optimize>\n" +
				"    <NuGetAudit>false</NuGetAudit>\n" +
				"    <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>\n" +
				"  </PropertyGroup>\n" +
				reference +
				"</Project>\n";
			var projectPath = Path.Combine(temporary.Path, "AuthorityTamper.csproj");
			File.WriteAllText(projectPath, project, new UTF8Encoding(false));
			File.WriteAllText(
				Path.Combine(temporary.Path, "AuthorityTamper.cs"), source, new UTF8Encoding(false));

			var start = new System.Diagnostics.ProcessStartInfo
			{
				FileName = DotnetHost(),
				WorkingDirectory = temporary.Path,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			};
			start.ArgumentList.Add("build");
			start.ArgumentList.Add(projectPath);
			start.ArgumentList.Add("--configuration");
			start.ArgumentList.Add("Release");
			start.ArgumentList.Add("--nologo");
			start.ArgumentList.Add("-p:NuGetAudit=false");
			start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
			using var process = System.Diagnostics.Process.Start(start) ??
				throw new InvalidOperationException("Could not compile the authority tamper fixture.");
			try
			{
				var stdout = process.StandardOutput.ReadToEndAsync();
				var stderr = process.StandardError.ReadToEndAsync();
				if (!process.WaitForExit(90000))
				{
					process.Kill(true);
					process.WaitForExit(5000);
					Assert.Fail("Timed out while compiling the authority tamper fixture.");
				}

				var diagnostics = stdout.GetAwaiter().GetResult() + Environment.NewLine +
					stderr.GetAwaiter().GetResult();
				Assert.That(process.ExitCode, Is.Zero, diagnostics);
				return inspect(Path.Combine(
					temporary.Path, "bin", "Release", "net6.0", assemblyName + ".dll"));
			}
			finally
			{
				if (!process.HasExited)
				{
					process.Kill(true);
					process.WaitForExit(5000);
				}
			}
		}

		static string[] GroupAuthorityUses(IEnumerable<string> uses) =>
			uses.GroupBy(use => use, StringComparer.Ordinal)
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.Select(group => group.Count() + "|" + group.Key)
				.ToArray();

		static string[] ReadProvisionalWrapperUses(string assemblyPath)
		{
			using var stream = File.OpenRead(assemblyPath);
			using var pe = new PEReader(stream);
			var reader = pe.GetMetadataReader();
			var sourceAssembly = reader.GetString(reader.GetAssemblyDefinition().Name);
			var provider = new ProvisionalWrapperSignatureTypeProvider(sourceAssembly);
			var uses = new List<string>();
			foreach (var typeHandle in reader.TypeDefinitions)
			{
				var definition = reader.GetTypeDefinition(typeHandle);
				var owner = MetadataTypeDefinitionName(reader, typeHandle);
				var isWrapperImplementation = IsProvisionalWrapperImplementation(owner);
				RecordMetadataTypeUse(
					uses, "base-type " + owner,
					ResolveMetadataTypeShape(reader, provider, definition.BaseType));

				foreach (var interfaceHandle in definition.GetInterfaceImplementations())
				{
					var implementation = reader.GetInterfaceImplementation(interfaceHandle);
					RecordMetadataTypeUse(
						uses, "interface " + owner,
						ResolveMetadataTypeShape(reader, provider, implementation.Interface));
				}

				foreach (var parameterHandle in definition.GetGenericParameters())
				{
					var parameter = reader.GetGenericParameter(parameterHandle);
					foreach (var constraintHandle in parameter.GetConstraints())
					{
						var constraint = reader.GetGenericParameterConstraint(constraintHandle);
						RecordMetadataTypeUse(
							uses,
							"type-constraint " + owner + "[" + parameter.Index + "]",
							ResolveMetadataTypeShape(reader, provider, constraint.Type));
					}
				}

				foreach (var fieldHandle in definition.GetFields())
				{
					var field = reader.GetFieldDefinition(fieldHandle);
					var fieldType = field.DecodeSignature(provider, null);
					RecordMetadataTypeUse(
						uses,
						"field " + owner + "." + reader.GetString(field.Name) + ":" + fieldType.Display,
						fieldType);
				}

				foreach (var propertyHandle in definition.GetProperties())
				{
					var property = reader.GetPropertyDefinition(propertyHandle);
					var signature = property.DecodeSignature(provider, null);
					if (MetadataSignatureContainsProvisionalWrapper(signature))
						uses.Add("property " + FormatMetadataCallableIdentity(
							owner,
							reader.GetString(property.Name),
							signature,
							signature.Header.IsInstance,
							signature.ParameterTypes.Select(FormatMetadataReferenceParameter)));
				}

				foreach (var eventHandle in definition.GetEvents())
				{
					var @event = reader.GetEventDefinition(eventHandle);
					var eventType = ResolveMetadataTypeShape(reader, provider, @event.Type);
					RecordMetadataTypeUse(
						uses,
						"event " + owner + "." + reader.GetString(@event.Name) + ":" + eventType.Display,
						eventType);
				}

				foreach (var methodHandle in definition.GetMethods())
				{
					var method = reader.GetMethodDefinition(methodHandle);
					var signature = method.DecodeSignature(provider, null);
					var caller = FormatMetadataMethodDefinition(
						reader, provider, method, signature);
					RecordMetadataTypeUse(uses, "method-return " + caller, signature.ReturnType);
					for (var i = 0; i < signature.ParameterTypes.Length; i++)
						RecordMetadataTypeUse(
							uses, "method-parameter " + caller + "[" + i + "]",
							signature.ParameterTypes[i]);

					foreach (var parameterHandle in method.GetGenericParameters())
					{
						var parameter = reader.GetGenericParameter(parameterHandle);
						foreach (var constraintHandle in parameter.GetConstraints())
						{
							var constraint = reader.GetGenericParameterConstraint(constraintHandle);
							RecordMetadataTypeUse(
								uses,
								"method-constraint " + caller + "[" + parameter.Index + "]",
								ResolveMetadataTypeShape(reader, provider, constraint.Type));
						}
					}

					if (method.RelativeVirtualAddress == 0)
						continue;

					var body = pe.GetMethodBody(method.RelativeVirtualAddress);
					if (!body.LocalSignature.IsNil)
					{
						var locals = reader.GetStandaloneSignature(body.LocalSignature)
							.DecodeLocalSignature(provider, null);
						for (var i = 0; i < locals.Length; i++)
							RecordMetadataTypeUse(uses, "local " + caller + "[" + i + "]", locals[i]);
					}

					foreach (var region in body.ExceptionRegions)
						if (region.Kind == ExceptionRegionKind.Catch && !region.CatchType.IsNil)
							RecordMetadataTypeUse(
								uses, "exception-catch " + caller,
								ResolveMetadataTypeShape(reader, provider, region.CatchType));

					var il = body.GetILBytes().ToArray();
					var position = 0;
					while (position < il.Length)
					{
						var value = (ushort)il[position++];
						if (value == 0xFE)
							value = checked((ushort)(0xFE00 | il[position++]));
						if (!IlOpCodes.TryGetValue(value, out var operation))
							throw new InvalidDataException("The final production IL contains an unknown opcode.");

						if (MetadataOperandCanContainType(operation.OperandType))
						{
							var token = BitConverter.ToInt32(il, position);
							var target = ResolveMetadataOperandShape(
								reader,
								provider,
								operation.OperandType,
								System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(token));
							if (!isWrapperImplementation ||
								operation.Value == System.Reflection.Emit.OpCodes.Newobj.Value)
								RecordMetadataTypeUse(
									uses,
									"il " + caller + " -> " + operation.Name + " " + target.Display,
									target);
						}

						position += OperandSize(operation.OperandType, il, position);
					}
				}
			}

			return uses.OrderBy(use => use, StringComparer.Ordinal).ToArray();
		}

		static bool IsProvisionalWrapperImplementation(string type) =>
			type == "OpenRA.Mods.RA2.Content.VerifiedRa2RuntimeContract" ||
			type.StartsWith(
				"OpenRA.Mods.RA2.Content.VerifiedRa2RuntimeContract+", StringComparison.Ordinal);

		static bool MetadataOperandCanContainType(System.Reflection.Emit.OperandType operand) =>
			operand == System.Reflection.Emit.OperandType.InlineField ||
			operand == System.Reflection.Emit.OperandType.InlineMethod ||
			operand == System.Reflection.Emit.OperandType.InlineSig ||
			operand == System.Reflection.Emit.OperandType.InlineTok ||
			operand == System.Reflection.Emit.OperandType.InlineType;

		static void RecordMetadataTypeUse(
			ICollection<string> uses,
			string description,
			ProvisionalWrapperMetadataType type)
		{
			if (type != null && type.ContainsProvisionalWrapper)
				uses.Add(description);
		}

		static bool MetadataSignatureContainsProvisionalWrapper(
			MethodSignature<ProvisionalWrapperMetadataType> signature) =>
			signature.ReturnType.ContainsProvisionalWrapper ||
			signature.ParameterTypes.Any(type => type.ContainsProvisionalWrapper);

		static string FormatMetadataMethodDefinition(
			MetadataReader reader,
			ProvisionalWrapperSignatureTypeProvider provider,
			MethodDefinition definition,
			MethodSignature<ProvisionalWrapperMetadataType> signature)
		{
			var parameterAttributes = new Dictionary<int, System.Reflection.ParameterAttributes>();
			foreach (var handle in definition.GetParameters())
			{
				var parameter = reader.GetParameter(handle);
				if (parameter.SequenceNumber > 0)
					parameterAttributes.Add(parameter.SequenceNumber - 1, parameter.Attributes);
			}

			var parameters = signature.ParameterTypes.Select((type, index) =>
				FormatMetadataDefinitionParameter(
					type,
					parameterAttributes.TryGetValue(index, out var attributes)
						? attributes : 0));
			return FormatMetadataCallableIdentity(
				provider.GetTypeFromDefinition(reader, definition.GetDeclaringType(), 0).Display,
				reader.GetString(definition.Name),
				signature,
				(definition.Attributes & System.Reflection.MethodAttributes.Static) == 0,
				parameters);
		}

		static string FormatMetadataCallableIdentity(
			string owner,
			string name,
			MethodSignature<ProvisionalWrapperMetadataType> signature,
			bool isInstance,
			IEnumerable<string> parameters) =>
			(isInstance ? "instance " : "static ") + owner + "." + name +
			"<arity=" + signature.GenericParameterCount + ">(" +
			string.Join(",", parameters) + "):" + signature.ReturnType.Display;

		static string FormatMetadataDefinitionParameter(
			ProvisionalWrapperMetadataType type,
			System.Reflection.ParameterAttributes attributes)
		{
			var byReference = type.Display.EndsWith("&", StringComparison.Ordinal);
			var display = byReference
				? type.Display.Substring(0, type.Display.Length - 1)
				: type.Display;
			var input = (attributes & System.Reflection.ParameterAttributes.In) != 0;
			var output = (attributes & System.Reflection.ParameterAttributes.Out) != 0;
			var optional = (attributes & System.Reflection.ParameterAttributes.Optional) != 0;
			var direction = byReference
				? output && !input ? "out " : input && !output ? "in " : "ref "
				: output ? "[out] " : input ? "[in] " : string.Empty;
			return direction + display + (optional ? " optional" : string.Empty);
		}

		static string FormatMetadataReferenceParameter(ProvisionalWrapperMetadataType type) =>
			FormatMetadataDefinitionParameter(type, 0);

		static ProvisionalWrapperMetadataType ResolveMetadataOperandShape(
			MetadataReader reader,
			ProvisionalWrapperSignatureTypeProvider provider,
			System.Reflection.Emit.OperandType operand,
			EntityHandle handle)
		{
			return operand switch
			{
				System.Reflection.Emit.OperandType.InlineType =>
					ResolveMetadataTypeShape(reader, provider, handle),
				System.Reflection.Emit.OperandType.InlineMethod =>
					ResolveMetadataMethodShape(reader, provider, handle),
				System.Reflection.Emit.OperandType.InlineField =>
					ResolveMetadataFieldShape(reader, provider, handle),
				System.Reflection.Emit.OperandType.InlineSig =>
					ResolveStandaloneSignatureShape(reader, provider, handle),
				System.Reflection.Emit.OperandType.InlineTok =>
					ResolveMetadataTokenShape(reader, provider, handle),
				_ => throw new InvalidDataException("The IL operand is not a metadata type carrier."),
			};
		}

		static ProvisionalWrapperMetadataType ResolveMetadataTokenShape(
			MetadataReader reader,
			ProvisionalWrapperSignatureTypeProvider provider,
			EntityHandle handle)
		{
			return handle.Kind switch
			{
				HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification =>
					ResolveMetadataTypeShape(reader, provider, handle),
				HandleKind.MethodDefinition or HandleKind.MethodSpecification =>
					ResolveMetadataMethodShape(reader, provider, handle),
				HandleKind.FieldDefinition => ResolveMetadataFieldShape(reader, provider, handle),
				HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)handle).GetKind() switch
				{
					MemberReferenceKind.Method => ResolveMetadataMethodShape(reader, provider, handle),
					MemberReferenceKind.Field => ResolveMetadataFieldShape(reader, provider, handle),
					_ => throw new InvalidDataException("The final production IL contains an unknown member reference kind."),
				},
				_ => throw new InvalidDataException(
					"The final production IL contains an unsupported metadata token kind: " + handle.Kind),
			};
		}

		static ProvisionalWrapperMetadataType ResolveMetadataTypeShape(
			MetadataReader reader,
			ProvisionalWrapperSignatureTypeProvider provider,
			EntityHandle handle)
		{
			if (handle.IsNil)
				return null;

			return handle.Kind switch
			{
				HandleKind.TypeDefinition => provider.GetTypeFromDefinition(
					reader, (TypeDefinitionHandle)handle, 0),
				HandleKind.TypeReference => provider.GetTypeFromReference(
					reader, (TypeReferenceHandle)handle, 0),
				HandleKind.TypeSpecification => provider.GetTypeFromSpecification(
					reader, null, (TypeSpecificationHandle)handle, 0),
				_ => throw new InvalidDataException(
					"The final production metadata contains an unsupported type handle: " + handle.Kind),
			};
		}

		static ProvisionalWrapperMetadataType ResolveMetadataMethodShape(
			MetadataReader reader,
			ProvisionalWrapperSignatureTypeProvider provider,
			EntityHandle handle)
		{
			switch (handle.Kind)
			{
				case HandleKind.MethodDefinition:
				{
					var definition = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
					var owner = provider.GetTypeFromDefinition(reader, definition.GetDeclaringType(), 0);
					var signature = definition.DecodeSignature(provider, null);
					return ProvisionalWrapperMetadataType.Composite(
						FormatMetadataMethodDefinition(
							reader,
							provider,
							definition,
							signature),
						new[] { owner, signature.ReturnType }.Concat(signature.ParameterTypes));
				}

				case HandleKind.MemberReference:
				{
					var reference = reader.GetMemberReference((MemberReferenceHandle)handle);
					if (reference.GetKind() != MemberReferenceKind.Method)
						throw new InvalidDataException("A method operand resolved to a field member reference.");

					var owner = ResolveMetadataMemberParentShape(reader, provider, reference.Parent);
					var signature = reference.DecodeMethodSignature(provider, null);
					return ProvisionalWrapperMetadataType.Composite(
						FormatMetadataCallableIdentity(
							owner.Display,
							reader.GetString(reference.Name),
							signature,
							signature.Header.IsInstance,
							signature.ParameterTypes.Select(FormatMetadataReferenceParameter)),
						new[] { owner, signature.ReturnType }.Concat(signature.ParameterTypes));
				}

				case HandleKind.MethodSpecification:
				{
					var specification = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
					var method = ResolveMetadataMethodShape(reader, provider, specification.Method);
					var arguments = specification.DecodeSignature(provider, null);
					return ProvisionalWrapperMetadataType.Composite(
						method.Display + "[method-args=" +
						string.Join(",", arguments.Select(argument => argument.Display)) + "]",
						new[] { method }.Concat(arguments));
				}

				default:
					throw new InvalidDataException(
						"The final production metadata contains an unsupported method handle: " + handle.Kind);
			}
		}

		static ProvisionalWrapperMetadataType ResolveMetadataFieldShape(
			MetadataReader reader,
			ProvisionalWrapperSignatureTypeProvider provider,
			EntityHandle handle)
		{
			switch (handle.Kind)
			{
				case HandleKind.FieldDefinition:
				{
					var definition = reader.GetFieldDefinition((FieldDefinitionHandle)handle);
					var owner = provider.GetTypeFromDefinition(reader, definition.GetDeclaringType(), 0);
					var fieldType = definition.DecodeSignature(provider, null);
					return ProvisionalWrapperMetadataType.Composite(
						owner.Display + "." + reader.GetString(definition.Name) + ":" + fieldType.Display,
						new[] { owner, fieldType });
				}

				case HandleKind.MemberReference:
				{
					var reference = reader.GetMemberReference((MemberReferenceHandle)handle);
					if (reference.GetKind() != MemberReferenceKind.Field)
						throw new InvalidDataException("A field operand resolved to a method member reference.");

					var owner = ResolveMetadataMemberParentShape(reader, provider, reference.Parent);
					var fieldType = reference.DecodeFieldSignature(provider, null);
					return ProvisionalWrapperMetadataType.Composite(
						owner.Display + "." + reader.GetString(reference.Name) + ":" + fieldType.Display,
						new[] { owner, fieldType });
				}

				default:
					throw new InvalidDataException(
						"The final production metadata contains an unsupported field handle: " + handle.Kind);
			}
		}

		static ProvisionalWrapperMetadataType ResolveMetadataMemberParentShape(
			MetadataReader reader,
			ProvisionalWrapperSignatureTypeProvider provider,
			EntityHandle handle)
		{
			return handle.Kind switch
			{
				HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification =>
					ResolveMetadataTypeShape(reader, provider, handle),
				HandleKind.MethodDefinition => provider.GetTypeFromDefinition(
					reader,
					reader.GetMethodDefinition((MethodDefinitionHandle)handle).GetDeclaringType(),
					0),
				HandleKind.ModuleDefinition => ProvisionalWrapperMetadataType.Unmonitored("<module>"),
				HandleKind.ModuleReference => ProvisionalWrapperMetadataType.Unmonitored(
					reader.GetString(reader.GetModuleReference((ModuleReferenceHandle)handle).Name)),
				_ => throw new InvalidDataException(
					"The final production metadata contains an unsupported member parent: " + handle.Kind),
			};
		}

		static ProvisionalWrapperMetadataType ResolveStandaloneSignatureShape(
			MetadataReader reader,
			ProvisionalWrapperSignatureTypeProvider provider,
			EntityHandle handle)
		{
			if (handle.Kind != HandleKind.StandaloneSignature)
				throw new InvalidDataException("An InlineSig operand did not resolve to a standalone signature.");

			var signature = reader.GetStandaloneSignature((StandaloneSignatureHandle)handle);
			return signature.GetKind() switch
			{
				StandaloneSignatureKind.Method => ProvisionalWrapperMetadataType.FromMethodSignature(
					"standalone-method-signature", signature.DecodeMethodSignature(provider, null)),
				StandaloneSignatureKind.LocalVariables => ProvisionalWrapperMetadataType.Composite(
					"standalone-local-signature", signature.DecodeLocalSignature(provider, null)),
				_ => throw new InvalidDataException(
					"The final production IL contains an unsupported standalone signature kind."),
			};
		}

		static string DotnetHost()
		{
			var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
			if (!string.IsNullOrEmpty(host) && File.Exists(host))
				return host;

			var root = Environment.GetEnvironmentVariable("DOTNET_ROOT");
			if (!string.IsNullOrEmpty(root))
			{
				var candidate = Path.Combine(root, "dotnet");
				if (File.Exists(candidate))
					return candidate;
			}

			const string PinnedHost = "/Users/kevin/.dotnet/dotnet";
			return File.Exists(PinnedHost) ? PinnedHost : "dotnet";
		}

		static string[] EvaluateIosCompileItems()
		{
			var project = Path.Combine(RepositoryRoot(), "ios", "OpenRA.iOS", "OpenRA.iOS.csproj");
			var start = new System.Diagnostics.ProcessStartInfo
			{
				FileName = DotnetHost(),
				WorkingDirectory = RepositoryRoot(),
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			};
			start.ArgumentList.Add("msbuild");
			start.ArgumentList.Add(project);
			start.ArgumentList.Add("-nologo");
			start.ArgumentList.Add("-p:RuntimeIdentifier=iossimulator-arm64");
			start.ArgumentList.Add("-p:NuGetAudit=false");
			start.ArgumentList.Add("-getItem:Compile");
			using var process = System.Diagnostics.Process.Start(start) ??
				throw new InvalidOperationException("Could not evaluate the iOS Compile graph.");
			var stdout = process.StandardOutput.ReadToEndAsync();
			var stderr = process.StandardError.ReadToEndAsync();
			if (!process.WaitForExit(90000))
			{
				process.Kill(true);
				Assert.Fail("Timed out while evaluating the iOS Compile graph.");
			}

			var output = stdout.GetAwaiter().GetResult();
			var diagnostics = stderr.GetAwaiter().GetResult();
			Assert.That(process.ExitCode, Is.Zero, output + Environment.NewLine + diagnostics);
			using var document = JsonDocument.Parse(output);
			return document.RootElement.GetProperty("Items").GetProperty("Compile")
				.EnumerateArray()
				.Select(item => item.GetProperty("Identity").GetString())
				.ToArray();
		}

		static string IosReleaseAssemblyPath(string basename)
		{
			var path = Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "bin", "Release", "net8.0-ios",
				"iossimulator-arm64", basename);
			Assert.That(File.Exists(path), Is.True,
				"Build the Release iOS simulator app before running the final Task 4 authority gate: " + path);
			return path;
		}

		static string[] ReadAuthorityIlEdges(string assemblyPath)
		{
			var monitoredTargets = new HashSet<string>(StringComparer.Ordinal)
			{
				"OpenRA.Mods.RA2::OpenRA.Mods.RA2.Content.IosRuntimeManagedImageLease::.ctor",
				"OpenRA.Mods.RA2::OpenRA.Mods.RA2.Content.IosRuntimeManagedImageSource::.ctor",
				"OpenRA.Mods.RA2::OpenRA.Mods.RA2.Content.IosRuntimeResourcePreflight::.ctor",
				"OpenRA.Mods.RA2::OpenRA.Mods.RA2.Content.IosRuntimeResourcePreflight::Check",
				"OpenRA.Mods.RA2::OpenRA.Mods.RA2.Content.ManagedImageIdentity::.ctor",
				"OpenRA.Mods.RA2::OpenRA.Mods.RA2.Content.Ra2RuntimeContract::.ctor",
				"OpenRA.Mods.RA2::OpenRA.Mods.RA2.Content.Ra2RuntimeContractBuilder::.ctor",
				"OpenRA.Mods.RA2::OpenRA.Mods.RA2.Content.Ra2RuntimeContractBuilder::BuildExpected",
				"OpenRA.Mods.RA2::OpenRA.Mods.RA2.Content.Ra2RuntimeContractExpectation::.ctor",
				"OpenRA.Mods.RA2::OpenRA.Mods.RA2.Content.RuntimeBuildIdentity::.ctor",
				"OpenRA.Mods.RA2::OpenRA.Mods.RA2.Content.RuntimeBuildIdentityReader::.ctor",
				"OpenRA.Mods.RA2::OpenRA.Mods.RA2.Content.RuntimeBuildIdentityReader::Capture",
				"OpenRA.Mods.RA2::OpenRA.Mods.RA2.Content.RuntimeContractInputs::.ctor",
				"OpenRA.Mods.RA2::OpenRA.Mods.RA2.Content.VerifiedRa2RuntimeContract::.ctor",
				"OpenRA.iOS::OpenRA.iOS.IosRuntimeBuildIdentityProvider::.ctor",
				"OpenRA.iOS::OpenRA.iOS.IosRuntimeBuildIdentityProvider::Capture",
			};
			using var stream = File.OpenRead(assemblyPath);
			using var pe = new PEReader(stream);
			var reader = pe.GetMetadataReader();
			var sourceAssembly = reader.GetString(reader.GetAssemblyDefinition().Name);
			var signatureProvider = new ProvisionalWrapperSignatureTypeProvider(sourceAssembly);
			var edges = new List<string>();
			foreach (var handle in reader.MethodDefinitions)
			{
				var definition = reader.GetMethodDefinition(handle);
				if (definition.RelativeVirtualAddress == 0)
					continue;

				var caller = FormatMetadataMethodDefinition(
					reader,
					signatureProvider,
					definition,
					definition.DecodeSignature(signatureProvider, null));
				var il = pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes().ToArray();
				var position = 0;
				while (position < il.Length)
				{
					var value = (ushort)il[position++];
					if (value == 0xFE)
						value = checked((ushort)(0xFE00 | il[position++]));
					if (!IlOpCodes.TryGetValue(value, out var operation))
						throw new InvalidDataException("The final production IL contains an unknown opcode.");

					if (operation.OperandType == System.Reflection.Emit.OperandType.InlineMethod &&
						(operation.Value == System.Reflection.Emit.OpCodes.Call.Value ||
						operation.Value == System.Reflection.Emit.OpCodes.Callvirt.Value ||
						operation.Value == System.Reflection.Emit.OpCodes.Newobj.Value ||
						operation.Value == System.Reflection.Emit.OpCodes.Ldftn.Value ||
						operation.Value == System.Reflection.Emit.OpCodes.Ldvirtftn.Value))
					{
						var token = BitConverter.ToInt32(il, position);
						var target = ResolveMetadataMethod(
							reader,
							sourceAssembly,
							System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(token));
						if (target != null && monitoredTargets.Contains(
							target.Assembly + "::" + target.Type + "::" + target.Method))
						{
							var targetIdentity = ResolveMetadataMethodShape(
								reader,
								signatureProvider,
								System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(token)).Display;
							var operationName = operation.Value switch
							{
								short opcodeValue when opcodeValue == System.Reflection.Emit.OpCodes.Newobj.Value => "newobj",
								short opcodeValue when opcodeValue == System.Reflection.Emit.OpCodes.Callvirt.Value => "callvirt",
								short opcodeValue when opcodeValue == System.Reflection.Emit.OpCodes.Ldftn.Value => "ldftn",
								short opcodeValue when opcodeValue == System.Reflection.Emit.OpCodes.Ldvirtftn.Value => "ldvirtftn",
								_ => "call",
							};
							edges.Add(caller + " -> " + operationName + " " + targetIdentity);
						}
					}

					position += OperandSize(operation.OperandType, il, position);
				}
			}

			return edges.OrderBy(edge => edge, StringComparer.Ordinal).ToArray();
		}

		static string[] ReadReferencedProvisionalContractTypes(string assemblyPath)
		{
			var provisionalTypes = new HashSet<string>(StringComparer.Ordinal)
			{
				"OpenRA.Mods.RA2.Content.IosRuntimeResourcePreflight",
				"OpenRA.Mods.RA2.Content.Ra2RuntimeContract",
				"OpenRA.Mods.RA2.Content.Ra2RuntimeContractBuilder",
				"OpenRA.Mods.RA2.Content.Ra2RuntimeContractExpectation",
				"OpenRA.Mods.RA2.Content.RuntimeContractInputs",
				"OpenRA.Mods.RA2.Content.VerifiedRa2RuntimeContract",
			};
			using var stream = File.OpenRead(assemblyPath);
			using var pe = new PEReader(stream);
			var reader = pe.GetMetadataReader();
			var sourceAssembly = reader.GetString(reader.GetAssemblyDefinition().Name);
			return reader.TypeReferences
				.Select(handle => ResolveMetadataTypeReference(reader, sourceAssembly, handle))
				.Where(type => type.Assembly == "OpenRA.Mods.RA2" &&
					provisionalTypes.Contains(type.Type))
				.Select(type => type.Type)
				.Distinct(StringComparer.Ordinal)
				.OrderBy(type => type, StringComparer.Ordinal)
				.ToArray();
		}

		static MetadataMethodReference ResolveMetadataMethod(
			MetadataReader reader,
			string sourceAssembly,
			EntityHandle handle)
		{
			switch (handle.Kind)
			{
				case HandleKind.MethodDefinition:
				{
					var definition = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
					return new MetadataMethodReference(
						sourceAssembly,
						MetadataTypeDefinitionName(reader, definition.GetDeclaringType()),
						reader.GetString(definition.Name));
				}

				case HandleKind.MemberReference:
				{
					var reference = reader.GetMemberReference((MemberReferenceHandle)handle);
					var type = ResolveMetadataType(reader, sourceAssembly, reference.Parent);
					return type == null ? null : new MetadataMethodReference(
						type.Assembly, type.Type, reader.GetString(reference.Name));
				}

				case HandleKind.MethodSpecification:
					return ResolveMetadataMethod(
						reader,
						sourceAssembly,
						reader.GetMethodSpecification((MethodSpecificationHandle)handle).Method);
				default:
					return null;
			}
		}

		static MetadataTypeReference ResolveMetadataType(
			MetadataReader reader,
			string sourceAssembly,
			EntityHandle handle)
		{
			return handle.Kind switch
			{
				HandleKind.TypeDefinition => new MetadataTypeReference(
					sourceAssembly,
					MetadataTypeDefinitionName(reader, (TypeDefinitionHandle)handle)),
				HandleKind.TypeReference => ResolveMetadataTypeReference(
					reader, sourceAssembly, (TypeReferenceHandle)handle),
				_ => null,
			};
		}

		static MetadataTypeReference ResolveMetadataTypeReference(
			MetadataReader reader,
			string sourceAssembly,
			TypeReferenceHandle handle)
		{
			var reference = reader.GetTypeReference(handle);
			var name = reader.GetString(reference.Name);
			if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
			{
				var parent = ResolveMetadataTypeReference(
					reader, sourceAssembly, (TypeReferenceHandle)reference.ResolutionScope);
				return new MetadataTypeReference(parent.Assembly, parent.Type + "+" + name);
			}

			var @namespace = reader.GetString(reference.Namespace);
			var assembly = reference.ResolutionScope.Kind == HandleKind.AssemblyReference
				? reader.GetString(reader.GetAssemblyReference(
					(AssemblyReferenceHandle)reference.ResolutionScope).Name)
				: sourceAssembly;
			return new MetadataTypeReference(
				assembly,
				string.IsNullOrEmpty(@namespace) ? name : @namespace + "." + name);
		}

		static string MetadataTypeDefinitionName(
			MetadataReader reader,
			TypeDefinitionHandle handle)
		{
			var definition = reader.GetTypeDefinition(handle);
			var name = reader.GetString(definition.Name);
			var parent = definition.GetDeclaringType();
			if (!parent.IsNil)
				return MetadataTypeDefinitionName(reader, parent) + "+" + name;

			var @namespace = reader.GetString(definition.Namespace);
			return string.IsNullOrEmpty(@namespace) ? name : @namespace + "." + name;
		}

		static void AssertNoProductionConstructorCallers(
			System.Reflection.ConstructorInfo target)
		{
			const System.Reflection.BindingFlags Flags =
				System.Reflection.BindingFlags.Instance |
				System.Reflection.BindingFlags.Static |
				System.Reflection.BindingFlags.Public |
				System.Reflection.BindingFlags.NonPublic |
				System.Reflection.BindingFlags.DeclaredOnly;
			var callers = target.Module.Assembly.GetTypes()
				.SelectMany(type => type.GetMethods(Flags)
					.Cast<System.Reflection.MethodBase>()
					.Concat(type.GetConstructors(Flags))
					.Concat(type.TypeInitializer == null
						? Enumerable.Empty<System.Reflection.MethodBase>()
						: new[] { type.TypeInitializer }))
				.Where(caller => CallsConstructor(caller, target))
				.Select(caller => caller.DeclaringType.FullName + "." + caller.Name)
				.ToArray();
			Assert.That(callers, Is.Empty, target.DeclaringType.FullName);
		}

		static bool CallsConstructor(
			System.Reflection.MethodBase caller,
			System.Reflection.ConstructorInfo target)
		{
			var il = caller.GetMethodBody()?.GetILAsByteArray();
			if (il == null)
				return false;

			var position = 0;
			while (position < il.Length)
			{
				var value = (ushort)il[position++];
				if (value == 0xFE)
					value = checked((ushort)(0xFE00 | il[position++]));
				if (!IlOpCodes.TryGetValue(value, out var operation))
					throw new InvalidDataException("The production IL contains an unknown opcode.");

				if (operation.OperandType == System.Reflection.Emit.OperandType.InlineMethod &&
					operation.Value == System.Reflection.Emit.OpCodes.Newobj.Value)
				{
					var token = BitConverter.ToInt32(il, position);
					try
					{
						var referenced = caller.Module.ResolveMethod(token,
							caller.DeclaringType?.GetGenericArguments(),
							caller is System.Reflection.MethodInfo method
								? method.GetGenericArguments() : Type.EmptyTypes);
						if (referenced.Module == target.Module &&
							referenced.MetadataToken == target.MetadataToken)
							return true;
					}
					catch (ArgumentException)
					{
						// The target constructors are non-generic same-module MethodDefs.
					}
				}

				position += OperandSize(operation.OperandType, il, position);
			}

			return false;
		}

		static int OperandSize(
			System.Reflection.Emit.OperandType operand,
			byte[] il,
			int position)
		{
			return operand switch
			{
				System.Reflection.Emit.OperandType.InlineNone => 0,
				System.Reflection.Emit.OperandType.ShortInlineBrTarget or
					System.Reflection.Emit.OperandType.ShortInlineI or
					System.Reflection.Emit.OperandType.ShortInlineVar => 1,
				System.Reflection.Emit.OperandType.InlineVar => 2,
				System.Reflection.Emit.OperandType.InlineBrTarget or
					System.Reflection.Emit.OperandType.InlineField or
					System.Reflection.Emit.OperandType.InlineI or
					System.Reflection.Emit.OperandType.InlineMethod or
					System.Reflection.Emit.OperandType.InlineSig or
					System.Reflection.Emit.OperandType.InlineString or
					System.Reflection.Emit.OperandType.InlineTok or
					System.Reflection.Emit.OperandType.InlineType or
					System.Reflection.Emit.OperandType.ShortInlineR => 4,
				System.Reflection.Emit.OperandType.InlineI8 or
					System.Reflection.Emit.OperandType.InlineR => 8,
				System.Reflection.Emit.OperandType.InlineSwitch =>
					checked(4 + 4 * BitConverter.ToInt32(il, position)),
				_ => throw new ArgumentOutOfRangeException(nameof(operand)),
			};
		}

		static bool TypeContains(Type candidate, Type target)
		{
			if (candidate == target)
				return true;
			if (candidate.HasElementType)
				return TypeContains(candidate.GetElementType(), target);
			return candidate.IsGenericType && candidate.GetGenericArguments()
				.Any(argument => TypeContains(argument, target));
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !File.Exists(Path.Combine(
				directory.FullName, "packaging", "ios-runtime-contract-descriptor-shape-v1.json")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory.FullName;
		}

		sealed class RuntimeFixture
		{
			public RuntimeContractDescriptorInventory Inventory { get; }
			public IReadOnlyDictionary<string, byte[]> Payloads { get; }

			RuntimeFixture(
				RuntimeContractDescriptorInventory inventory,
				IReadOnlyDictionary<string, byte[]> payloads)
			{
				Inventory = inventory;
				Payloads = payloads;
			}

			public static RuntimeFixture Load(bool mutateFirstPayload = false)
			{
				using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
					RepositoryRoot(), "packaging", "ios-runtime-contract-descriptor-shape-v1.json")));
				var root = document.RootElement;
				var descriptors = ImmutableArray.CreateBuilder<RuntimeContractResourceDescriptor>();
				var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);
				var ordinal = 0;
				foreach (var item in root.GetProperty("descriptors").EnumerateArray())
				{
					var logicalName = item.GetProperty("logicalName").GetString();
					var bundlePath = item.GetProperty("bundleRelativePath").GetString();
					var profiles = item.GetProperty("profiles").EnumerateArray()
						.Select(value => Enum.Parse<Ra2RuntimeProfileId>(value.GetString()))
						.ToImmutableArray();
					var purpose = Enum.Parse<RuntimeContractResourcePurpose>(
						item.GetProperty("purpose").GetString());
					var payload = Encoding.UTF8.GetBytes(
						"runtime-fixture:" + logicalName +
						(mutateFirstPayload && ordinal == 0 ? ":changed" : string.Empty));
					var digest = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
					descriptors.Add(new RuntimeContractResourceDescriptor(
						logicalName, bundlePath, payload.LongLength, digest, profiles, purpose));
					payloads.Add(bundlePath, payload);
					ordinal++;
				}

				var inventory = new RuntimeContractDescriptorInventory(
					root.GetProperty("schemaVersion").GetInt32(),
					root.GetProperty("descriptorCount").GetInt32(),
					root.GetProperty("shapeDigest").GetString(),
					descriptors.ToImmutable());
				return new RuntimeFixture(inventory, payloads);
			}
		}

		sealed class ProvisionalWrapperMetadataType
		{
			public string Display { get; }
			public bool ContainsProvisionalWrapper { get; }

			ProvisionalWrapperMetadataType(string display, bool containsProvisionalWrapper)
			{
				Display = display;
				ContainsProvisionalWrapper = containsProvisionalWrapper;
			}

			public static ProvisionalWrapperMetadataType Monitored(string display) =>
				new ProvisionalWrapperMetadataType(display, true);

			public static ProvisionalWrapperMetadataType Unmonitored(string display) =>
				new ProvisionalWrapperMetadataType(display, false);

			public static ProvisionalWrapperMetadataType Composite(
				string display,
				IEnumerable<ProvisionalWrapperMetadataType> components) =>
				new ProvisionalWrapperMetadataType(
					display,
					components.Any(component =>
						component != null && component.ContainsProvisionalWrapper));

			public static ProvisionalWrapperMetadataType FromMethodSignature(
				string display,
				MethodSignature<ProvisionalWrapperMetadataType> signature) =>
				Composite(display, new[] { signature.ReturnType }.Concat(signature.ParameterTypes));
		}

		sealed class ProvisionalWrapperSignatureTypeProvider :
			ISignatureTypeProvider<ProvisionalWrapperMetadataType, object>
		{
			const string WrapperAssembly = "OpenRA.Mods.RA2";
			const string WrapperType = "OpenRA.Mods.RA2.Content.VerifiedRa2RuntimeContract";
			readonly string sourceAssembly;

			public ProvisionalWrapperSignatureTypeProvider(string sourceAssembly)
			{
				this.sourceAssembly = sourceAssembly;
			}

			public ProvisionalWrapperMetadataType GetArrayType(
				ProvisionalWrapperMetadataType elementType,
				ArrayShape shape) =>
				ProvisionalWrapperMetadataType.Composite(
					elementType.Display + "[rank=" + shape.Rank + "]", new[] { elementType });

			public ProvisionalWrapperMetadataType GetByReferenceType(
				ProvisionalWrapperMetadataType elementType) =>
				ProvisionalWrapperMetadataType.Composite(elementType.Display + "&", new[] { elementType });

			public ProvisionalWrapperMetadataType GetFunctionPointerType(
				MethodSignature<ProvisionalWrapperMetadataType> signature) =>
				ProvisionalWrapperMetadataType.FromMethodSignature("method-pointer", signature);

			public ProvisionalWrapperMetadataType GetGenericInstantiation(
				ProvisionalWrapperMetadataType genericType,
				ImmutableArray<ProvisionalWrapperMetadataType> typeArguments) =>
				ProvisionalWrapperMetadataType.Composite(
					genericType.Display + "<" +
					string.Join(",", typeArguments.Select(argument => argument.Display)) + ">",
					new[] { genericType }.Concat(typeArguments));

			public ProvisionalWrapperMetadataType GetGenericMethodParameter(
				object genericContext,
				int index) =>
				ProvisionalWrapperMetadataType.Unmonitored("!!" + index);

			public ProvisionalWrapperMetadataType GetGenericTypeParameter(
				object genericContext,
				int index) =>
				ProvisionalWrapperMetadataType.Unmonitored("!" + index);

			public ProvisionalWrapperMetadataType GetModifiedType(
				ProvisionalWrapperMetadataType modifier,
				ProvisionalWrapperMetadataType unmodifiedType,
				bool isRequired) =>
				ProvisionalWrapperMetadataType.Composite(
					unmodifiedType.Display + (isRequired ? " modreq(" : " modopt(") +
					modifier.Display + ")",
					new[] { modifier, unmodifiedType });

			public ProvisionalWrapperMetadataType GetPinnedType(
				ProvisionalWrapperMetadataType elementType) =>
				ProvisionalWrapperMetadataType.Composite(
					elementType.Display + " pinned", new[] { elementType });

			public ProvisionalWrapperMetadataType GetPointerType(
				ProvisionalWrapperMetadataType elementType) =>
				ProvisionalWrapperMetadataType.Composite(elementType.Display + "*", new[] { elementType });

			public ProvisionalWrapperMetadataType GetPrimitiveType(PrimitiveTypeCode typeCode) =>
				ProvisionalWrapperMetadataType.Unmonitored(typeCode.ToString());

			public ProvisionalWrapperMetadataType GetSZArrayType(
				ProvisionalWrapperMetadataType elementType) =>
				ProvisionalWrapperMetadataType.Composite(elementType.Display + "[]", new[] { elementType });

			public ProvisionalWrapperMetadataType GetTypeFromDefinition(
				MetadataReader reader,
				TypeDefinitionHandle handle,
				byte rawTypeKind) =>
				Named(sourceAssembly, MetadataTypeDefinitionName(reader, handle));

			public ProvisionalWrapperMetadataType GetTypeFromReference(
				MetadataReader reader,
				TypeReferenceHandle handle,
				byte rawTypeKind)
			{
				var type = ResolveMetadataTypeReference(reader, sourceAssembly, handle);
				return Named(type.Assembly, type.Type);
			}

			public ProvisionalWrapperMetadataType GetTypeFromSpecification(
				MetadataReader reader,
				object genericContext,
				TypeSpecificationHandle handle,
				byte rawTypeKind) =>
				reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

			static ProvisionalWrapperMetadataType Named(string assembly, string type) =>
				assembly == WrapperAssembly && type == WrapperType
					? ProvisionalWrapperMetadataType.Monitored(type)
					: ProvisionalWrapperMetadataType.Unmonitored(type);
		}

		sealed class MetadataMethodReference
		{
			public string Assembly { get; }
			public string Type { get; }
			public string Method { get; }

			public MetadataMethodReference(string assembly, string type, string method)
			{
				Assembly = assembly;
				Type = type;
				Method = method;
			}
		}

		sealed class MetadataTypeReference
		{
			public string Assembly { get; }
			public string Type { get; }

			public MetadataTypeReference(string assembly, string type)
			{
				Assembly = assembly;
				Type = type;
			}
		}

		sealed class ProcessBuildResult
		{
			public int ExitCode { get; }
			public string Diagnostics { get; }

			public ProcessBuildResult(int exitCode, string diagnostics)
			{
				ExitCode = exitCode;
				Diagnostics = diagnostics;
			}
		}

		sealed class TestClock : IRetailMonotonicClock
		{
			public bool Expired { get; set; }
			public long GetTimestamp() => 0;
			public TimeSpan GetElapsedTime(long start, long end) =>
				Expired ? TimeSpan.MaxValue : TimeSpan.Zero;
		}

		sealed class CheckpointProbeClock : IRetailMonotonicClock
		{
			readonly int expireAtCheckpoint;
			readonly int cancelAfterCheckpoint;
			readonly CancellationTokenSource cancellation;
			public int ElapsedCallCount { get; private set; }

			public CheckpointProbeClock(
				int expireAtCheckpoint = 0,
				int cancelAfterCheckpoint = 0,
				CancellationTokenSource cancellation = null)
			{
				this.expireAtCheckpoint = expireAtCheckpoint;
				this.cancelAfterCheckpoint = cancelAfterCheckpoint;
				this.cancellation = cancellation;
			}

			public long GetTimestamp() => 0;

			public TimeSpan GetElapsedTime(long start, long end)
			{
				ElapsedCallCount++;
				if (cancelAfterCheckpoint > 0 && ElapsedCallCount == cancelAfterCheckpoint)
					cancellation?.Cancel();

				return expireAtCheckpoint > 0 && ElapsedCallCount == expireAtCheckpoint
					? TimeSpan.MaxValue : TimeSpan.Zero;
			}
		}

		public enum ManagedImageFault
		{
			None,
			AcquireNull,
			AcquireThrow,
			AcquireOverflow,
			LeaseGetterThrow,
			LeaseGetterOverflow,
			FilenameMismatch,
			NegativeLength,
			EmptyLeaseMvid,
			LoadedMvidMismatch,
			OpenNull,
			OpenUnreadable,
			OpenNonSeekable,
			OpenThrow,
			OpenOverflow,
			ReadThrow,
			ReadOverflow,
			ShortStream,
			LongStream,
			NoPeMetadata,
			EmptyPeMvid,
			StreamMvidMismatch,
			PePositionOverflow,
			VerifyThrow,
			VerifyOverflow,
			DisposeThrow,
		}

		public enum ContractFramePerturbation
		{
			None,
			Magic,
			SchemaVersion,
			ProfileTag,
			SelectionModId,
			SelectionEntryName,
			IncludedFileCount,
			IncludedFile,
			CultureCount,
			Culture,
			InventorySchema,
			InventoryDescriptorCount,
			ShapeDigest,
			ApplicableDescriptorCount,
			GlobalOrdinal,
			PurposeTag,
			DescriptorProfileCount,
			DescriptorProfileTag,
			LogicalName,
			BundleRelativePath,
			DescriptorLength,
			DescriptorContentDigest,
			FilenameCatalogDigest,
			MapFormatVersion,
			ArchiveValidatorVersion,
			MapValidatorVersion,
			ManagedImageCount,
			ManagedImageFilename,
			ManagedImageDigest,
			ManagedImageMvid,
			AotRegistryDigest,
			TrailingByte,
		}

		public enum SecondInventoryMutation
		{
			Add,
			Remove,
			Rename,
			CaseAlias,
			Empty,
			Throw,
		}

		public enum RealManagedImageMutation
		{
			SymlinkLeaf,
			DirectoryLeaf,
			FifoLeaf,
			UnparseableLeaf,
			AddBetweenEnumerations,
			RemoveBetweenEnumerations,
			RenameBetweenEnumerations,
			RenameBeforeAcquisition,
			ReplaceAfterAcquisition,
			ReplaceDuringHash,
			ReplaceBetweenHashAndMvid,
			ReplaceAfterFirstRead,
			ReplaceAfterSecondEnumeration,
			ReplaceDuringFinalVerification,
		}

		public enum BuilderContextScenario
		{
			DefaultDeadline,
			DifferentClock,
			NullClock,
			MalformedCatalog,
			ZeroMapFormatVersion,
			ZeroArchiveValidatorVersion,
			ZeroMapValidatorVersion,
		}

		public enum VerifiedPreflightScenario
		{
			DefaultDeadline,
			DifferentClock,
			CancelledAtEntry,
			ExpiredAtEntry,
			AcquireReturnsNull,
			AcquireThrows,
			AcquireOverflow,
			CancelAfterFailedAcquire,
			LeaseGetterThrows,
			LeaseGetterOverflow,
			OpenThrows,
			OpenOverflow,
			ReadThrows,
			HashMismatch,
			CancelDuringRead,
			VerifyThrows,
			VerifyOverflow,
			ExpireDuringVerify,
			ReadOverflow,
		}

		public enum VerifiedPreflightResourceFault
		{
			None,
			AcquireReturnsNull,
			AcquireThrows,
			AcquireOverflow,
			AcquireFails,
			LeaseGetterThrows,
			LeaseGetterOverflow,
			OpenThrows,
			OpenOverflow,
			ReadThrows,
			ReadOverflow,
			HashMismatch,
			VerifyThrows,
			VerifyOverflow,
			DisposeThrows,
		}

		sealed class CallbackManagedImageSource : IRuntimeManagedImageSource
		{
			readonly IRuntimeManagedImageSource inner;
			int enumerationCount;
			int acquisitionCount;
			public Action<int> AfterEnumeration { get; set; }
			public Action<int> BeforeAcquire { get; set; }
			public Action<int> AfterAcquire { get; set; }
			public Action<int> AfterFirstRead { get; set; }
			public Action<int> AfterPositionReset { get; set; }
			public Action<int> AfterStreamDisposed { get; set; }
			public Action<int> BeforeVerify { get; set; }

			public CallbackManagedImageSource(IRuntimeManagedImageSource inner) =>
				this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

			public ImmutableArray<string> EnumerateAppLocalManagedImages()
			{
				var result = inner.EnumerateAppLocalManagedImages();
				enumerationCount++;
				AfterEnumeration?.Invoke(enumerationCount);
				return result;
			}

			public IRuntimeManagedImageLease AcquirePinned(string canonicalBundleFilename)
			{
				acquisitionCount++;
				BeforeAcquire?.Invoke(acquisitionCount);
				var lease = inner.AcquirePinned(canonicalBundleFilename);
				AfterAcquire?.Invoke(acquisitionCount);
				return new CallbackManagedImageLease(
					lease,
					acquisitionCount,
					AfterFirstRead,
					AfterPositionReset,
					AfterStreamDisposed,
					BeforeVerify);
			}

			public void Dispose() { }

			sealed class CallbackManagedImageLease : IRuntimeManagedImageLease
			{
				readonly IRuntimeManagedImageLease inner;
				readonly int acquisitionNumber;
				readonly Action<int> afterFirstRead;
				readonly Action<int> afterPositionReset;
				readonly Action<int> afterStreamDisposed;
				readonly Action<int> beforeVerify;
				public string CanonicalBundleFilename => inner.CanonicalBundleFilename;
				public long Length => inner.Length;
				public Guid ModuleMvid => inner.ModuleMvid;
				public Guid? LoadedModuleMvid => inner.LoadedModuleMvid;

				public CallbackManagedImageLease(
					IRuntimeManagedImageLease inner,
					int acquisitionNumber,
					Action<int> afterFirstRead,
					Action<int> afterPositionReset,
					Action<int> afterStreamDisposed,
					Action<int> beforeVerify)
				{
					this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
					this.acquisitionNumber = acquisitionNumber;
					this.afterFirstRead = afterFirstRead;
					this.afterPositionReset = afterPositionReset;
					this.afterStreamDisposed = afterStreamDisposed;
					this.beforeVerify = beforeVerify;
				}

				public Stream OpenRead() => new CallbackOnDisposeStream(
					inner.OpenRead(),
					() => afterFirstRead?.Invoke(acquisitionNumber),
					() => afterPositionReset?.Invoke(acquisitionNumber),
					() => afterStreamDisposed?.Invoke(acquisitionNumber));
				public void VerifyUnchanged()
				{
					beforeVerify?.Invoke(acquisitionNumber);
					inner.VerifyUnchanged();
				}

				public void Dispose() => inner.Dispose();
			}
		}

		sealed class CallbackOnDisposeStream : Stream
		{
			Stream inner;
			Action afterFirstRead;
			Action afterPositionReset;
			Action afterDispose;
			bool firstRead;
			bool positionReset;
			Stream Current => inner ?? throw new ObjectDisposedException(nameof(CallbackOnDisposeStream));
			public override bool CanRead => inner?.CanRead == true;
			public override bool CanSeek => inner?.CanSeek == true;
			public override bool CanWrite => false;
			public override long Length => Current.Length;
			public override long Position
			{
				get => Current.Position;
				set
				{
					Current.Position = value;
					if (value == 0 && !positionReset)
					{
						positionReset = true;
						afterPositionReset?.Invoke();
					}
				}
			}

			public CallbackOnDisposeStream(
				Stream inner,
				Action afterFirstRead,
				Action afterPositionReset,
				Action afterDispose)
			{
				this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
				this.afterFirstRead = afterFirstRead;
				this.afterPositionReset = afterPositionReset;
				this.afterDispose = afterDispose;
			}

			public override void Flush() => Current.Flush();
			public override int Read(byte[] buffer, int offset, int count)
			{
				var read = Current.Read(buffer, offset, count);
				if (!firstRead)
				{
					firstRead = true;
					afterFirstRead?.Invoke();
				}

				return read;
			}

			public override long Seek(long offset, SeekOrigin origin) => Current.Seek(offset, origin);
			public override void SetLength(long value) => throw new NotSupportedException();
			public override void Write(byte[] buffer, int offset, int count) =>
				throw new NotSupportedException();

			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					var releaseStream = inner;
					inner = null;
					afterFirstRead = null;
					afterPositionReset = null;
					var callback = afterDispose;
					afterDispose = null;
					try
					{
						releaseStream?.Dispose();
					}
					finally
					{
						callback?.Invoke();
					}
				}

				base.Dispose(disposing);
			}
		}

		sealed class RecordingManagedImageSource : IRuntimeManagedImageSource
		{
			readonly ImmutableArray<string> firstEnumeration;
			readonly byte[] bytes;
			readonly Guid moduleMvid;
			public ImmutableArray<string> SecondEnumeration { get; set; }
			public Action<int> AfterEnumeration { get; set; }
			public Action<int> AfterAcquire { get; set; }
			public Action<int> AfterOpen { get; set; }
			public Action<int> AfterStreamDisposed { get; set; }
			public int NullAcquireNumber { get; set; }
			public int ThrowEnumerationNumber { get; set; }
			public ManagedImageFault Fault { get; set; }
			public int FaultNumber { get; set; }
			public List<string> Acquired { get; } = new();
			public List<string> Leased { get; } = new();
			public List<string> Opened { get; } = new();
			public List<string> Verified { get; } = new();
			public List<string> Disposed { get; } = new();
			public int EnumerationCount { get; private set; }

			public RecordingManagedImageSource(
				IEnumerable<string> firstEnumeration,
				byte[] bytes,
				Guid moduleMvid)
			{
				this.firstEnumeration = firstEnumeration.ToImmutableArray();
				this.bytes = bytes;
				this.moduleMvid = moduleMvid;
				SecondEnumeration = this.firstEnumeration;
			}

			public ImmutableArray<string> EnumerateAppLocalManagedImages()
			{
				EnumerationCount++;
				var result = EnumerationCount == 1 ? firstEnumeration : SecondEnumeration;
				AfterEnumeration?.Invoke(EnumerationCount);
				if (EnumerationCount == ThrowEnumerationNumber)
					throw new IOException("controlled enumeration failure");
				return result;
			}

			public IRuntimeManagedImageLease AcquirePinned(string canonicalBundleFilename)
			{
				Acquired.Add(canonicalBundleFilename);
				AfterAcquire?.Invoke(Acquired.Count);
				var fault = Acquired.Count == FaultNumber ? Fault : ManagedImageFault.None;
				if (Acquired.Count == NullAcquireNumber || fault == ManagedImageFault.AcquireNull)
					return null;
				if (fault == ManagedImageFault.AcquireThrow)
					throw new IOException("controlled acquisition failure");
				if (fault == ManagedImageFault.AcquireOverflow)
					throw new OverflowException("controlled external acquisition overflow");

				Leased.Add(canonicalBundleFilename);
				return new RecordingManagedImageLease(
					this, canonicalBundleFilename, bytes, moduleMvid, Acquired.Count, fault);
			}

			public void Dispose() { }

			sealed class RecordingManagedImageLease : IRuntimeManagedImageLease
			{
				readonly RecordingManagedImageSource source;
				readonly string filename;
				readonly byte[] bytes;
				readonly Guid moduleMvid;
				readonly int acquisitionNumber;
				readonly ManagedImageFault fault;
				bool opened;
				bool disposed;
				public string CanonicalBundleFilename => fault switch
				{
					ManagedImageFault.LeaseGetterThrow => throw new IOException("controlled getter failure"),
					ManagedImageFault.LeaseGetterOverflow =>
						throw new OverflowException("controlled external getter overflow"),
					ManagedImageFault.FilenameMismatch => "Wrong.Managed.Image.dll",
					_ => filename,
				};
				public long Length => fault switch
				{
					ManagedImageFault.NegativeLength => -1,
					ManagedImageFault.ShortStream => checked(bytes.LongLength + 1),
					ManagedImageFault.LongStream => checked(bytes.LongLength - 1),
					_ => bytes.LongLength,
				};
				public Guid ModuleMvid => fault == ManagedImageFault.EmptyLeaseMvid ? Guid.Empty : moduleMvid;
				public Guid? LoadedModuleMvid => fault == ManagedImageFault.LoadedMvidMismatch
					? new Guid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") : null;

				public RecordingManagedImageLease(
					RecordingManagedImageSource source,
					string filename,
					byte[] bytes,
					Guid moduleMvid,
					int acquisitionNumber,
					ManagedImageFault fault)
				{
					this.source = source;
					this.filename = filename;
					this.moduleMvid = moduleMvid;
					this.acquisitionNumber = acquisitionNumber;
					this.fault = fault;
					this.bytes = fault switch
					{
						ManagedImageFault.NoPeMetadata => new byte[bytes.Length],
						ManagedImageFault.EmptyPeMvid => RewriteMvid(bytes, moduleMvid, Guid.Empty),
						ManagedImageFault.StreamMvidMismatch => RewriteMvid(
							bytes, moduleMvid, new Guid("11111111-2222-3333-4444-555555555555")),
						_ => bytes,
					};
				}

				public Stream OpenRead()
				{
					if (disposed)
						throw new ObjectDisposedException(nameof(RecordingManagedImageLease));
					if (opened)
						throw new InvalidOperationException("The image stream is one-shot.");

					opened = true;
					source.Opened.Add(filename);
					if (fault == ManagedImageFault.OpenThrow)
						throw new IOException("controlled open failure");
					if (fault == ManagedImageFault.OpenOverflow)
						throw new OverflowException("controlled external open overflow");
					if (fault == ManagedImageFault.OpenNull)
						return null;

					var stream = new ControlledReadStream(
						bytes,
						canRead: fault != ManagedImageFault.OpenUnreadable,
						canSeek: fault != ManagedImageFault.OpenNonSeekable,
						throwOnRead: fault == ManagedImageFault.ReadThrow,
						overflowOnRead: fault == ManagedImageFault.ReadOverflow,
						overflowOnPositionReset: fault == ManagedImageFault.PePositionOverflow,
						() => source.AfterStreamDisposed?.Invoke(acquisitionNumber));
					source.AfterOpen?.Invoke(acquisitionNumber);
					return stream;
				}

				public void VerifyUnchanged()
				{
					if (disposed)
						throw new ObjectDisposedException(nameof(RecordingManagedImageLease));
					source.Verified.Add(filename);
					if (fault == ManagedImageFault.VerifyThrow)
						throw new IOException("controlled verification failure");
					if (fault == ManagedImageFault.VerifyOverflow)
						throw new OverflowException("controlled external verification overflow");
				}

				public void Dispose()
				{
					if (disposed)
						return;

					disposed = true;
					source.Disposed.Add(filename);
					if (fault == ManagedImageFault.DisposeThrow)
						throw new IOException("controlled disposal failure");
				}
			}
		}

		sealed class ControlledReadStream : Stream
		{
			readonly MemoryStream inner;
			readonly bool canRead;
			readonly bool canSeek;
			readonly bool throwOnRead;
			readonly bool overflowOnRead;
			readonly bool overflowOnPositionReset;
			readonly Action disposed;
			bool isDisposed;

			public override bool CanRead => !isDisposed && canRead;
			public override bool CanSeek => !isDisposed && canSeek;
			public override bool CanWrite => false;
			public override long Length => inner.Length;
			public override long Position
			{
				get => inner.Position;
				set
				{
					if (!canSeek)
						throw new NotSupportedException();
					if (overflowOnPositionReset && value == 0)
						throw new OverflowException("controlled external PE seek overflow");
					inner.Position = value;
				}
			}

			public ControlledReadStream(
				byte[] bytes,
				bool canRead,
				bool canSeek,
				bool throwOnRead,
				bool overflowOnRead,
				bool overflowOnPositionReset,
				Action disposed)
			{
				inner = new MemoryStream(bytes, writable: false);
				this.canRead = canRead;
				this.canSeek = canSeek;
				this.throwOnRead = throwOnRead;
				this.overflowOnRead = overflowOnRead;
				this.overflowOnPositionReset = overflowOnPositionReset;
				this.disposed = disposed;
			}

			public override void Flush() { }

			public override int Read(byte[] buffer, int offset, int count)
			{
				if (!canRead)
					throw new NotSupportedException();
				if (throwOnRead)
					throw new IOException("controlled read/hash failure");
				if (overflowOnRead)
					throw new OverflowException("controlled external read overflow");
				return inner.Read(buffer, offset, count);
			}

			public override long Seek(long offset, SeekOrigin origin)
			{
				if (!canSeek)
					throw new NotSupportedException();
				return inner.Seek(offset, origin);
			}

			public override void SetLength(long value) => throw new NotSupportedException();
			public override void Write(byte[] buffer, int offset, int count) =>
				throw new NotSupportedException();

			protected override void Dispose(bool disposing)
			{
				if (disposing && !isDisposed)
				{
					isDisposed = true;
					try
					{
						inner.Dispose();
					}
					finally
					{
						disposed?.Invoke();
					}
				}

				base.Dispose(disposing);
			}
		}

		sealed class DictionaryRuntimeRoot : IIosRuntimeResourceRoot
		{
			readonly IReadOnlyDictionary<string, byte[]> payloads;
			public List<string> Acquired { get; } = new();
			public List<string> Leased { get; } = new();
			public List<string> Disposed { get; } = new();
			public Action<int> FileAcquired { get; set; }
			public Action<int> StreamRead { get; set; }
			public Action<int> LeaseVerified { get; set; }
			public VerifiedPreflightResourceFault Fault { get; set; }
			public int FaultNumber { get; set; }
			public DictionaryRuntimeRoot(IReadOnlyDictionary<string, byte[]> payloads) =>
				this.payloads = payloads;

			public RetailOperationResult<IIosRuntimeResourceLease> AcquireRegularFile(
				string canonicalRelativePath)
			{
				Acquired.Add(canonicalRelativePath);
				FileAcquired?.Invoke(Acquired.Count);
				var fault = Acquired.Count == FaultNumber ? Fault : VerifiedPreflightResourceFault.None;
				if (fault == VerifiedPreflightResourceFault.AcquireReturnsNull)
					return null;
				if (fault == VerifiedPreflightResourceFault.AcquireThrows)
					throw new IOException("controlled acquisition failure");
				if (fault == VerifiedPreflightResourceFault.AcquireOverflow)
					throw new OverflowException("controlled external acquisition overflow");
				if (fault == VerifiedPreflightResourceFault.AcquireFails ||
					!payloads.TryGetValue(canonicalRelativePath, out var payload))
					return RetailOperationResult<IIosRuntimeResourceLease>.Failed(new RetailImportFailure(
						RetailImportErrorCode.RuntimeResourcesMissing,
						null,
						null,
						RetailImportDiagnosticCode.InvalidMapPackage));

				Leased.Add(canonicalRelativePath);
				return RetailOperationResult<IIosRuntimeResourceLease>.Success(
					new DictionaryRuntimeLease(
						this, canonicalRelativePath, payload, Acquired.Count, fault));
			}

			public void Dispose() { }

			sealed class DictionaryRuntimeLease : IIosRuntimeResourceLease
			{
				readonly DictionaryRuntimeRoot root;
				readonly byte[] payload;
				readonly int ordinal;
				readonly string path;
				readonly VerifiedPreflightResourceFault fault;
				bool opened;
				bool disposed;
				public string CanonicalRelativePath =>
					fault switch
					{
						VerifiedPreflightResourceFault.LeaseGetterThrows =>
							throw new IOException("controlled lease getter failure"),
						VerifiedPreflightResourceFault.LeaseGetterOverflow =>
							throw new OverflowException("controlled external getter overflow"),
						_ => path,
					};
				public long Length => payload.LongLength;

				public DictionaryRuntimeLease(
					DictionaryRuntimeRoot root,
					string path,
					byte[] payload,
					int ordinal,
					VerifiedPreflightResourceFault fault)
				{
					this.root = root;
					this.path = path;
					this.payload = payload;
					this.ordinal = ordinal;
					this.fault = fault;
				}

				public Stream OpenVerifiedRead()
				{
					if (opened)
						throw new InvalidOperationException();
					opened = true;
					if (fault == VerifiedPreflightResourceFault.OpenThrows)
						throw new IOException("controlled open failure");
					if (fault == VerifiedPreflightResourceFault.OpenOverflow)
						throw new OverflowException("controlled external open overflow");

					var bytes = payload;
					if (fault == VerifiedPreflightResourceFault.HashMismatch)
					{
						bytes = payload.ToArray();
						bytes[0] ^= 0xFF;
					}

					return new DictionaryRuntimeStream(
						bytes,
						() => root.StreamRead?.Invoke(ordinal),
						fault == VerifiedPreflightResourceFault.ReadThrows,
						fault == VerifiedPreflightResourceFault.ReadOverflow);
				}

				public void VerifyUnchanged()
				{
					root.LeaseVerified?.Invoke(ordinal);
					if (fault == VerifiedPreflightResourceFault.VerifyThrows)
						throw new IOException("controlled verify failure");
					if (fault == VerifiedPreflightResourceFault.VerifyOverflow)
						throw new OverflowException("controlled external verification overflow");
				}

				public void Dispose()
				{
					if (disposed)
						return;

					disposed = true;
					root.Disposed.Add(path);
					if (fault == VerifiedPreflightResourceFault.DisposeThrows)
						throw new IOException("controlled dispose failure");
				}
			}

			sealed class DictionaryRuntimeStream : MemoryStream
			{
				readonly Action firstRead;
				readonly bool throwOnRead;
				readonly bool overflowOnRead;
				bool readStarted;

				public DictionaryRuntimeStream(
					byte[] bytes,
					Action firstRead,
					bool throwOnRead,
					bool overflowOnRead)
					: base(bytes, writable: false)
				{
					this.firstRead = firstRead;
					this.throwOnRead = throwOnRead;
					this.overflowOnRead = overflowOnRead;
				}

				public override int Read(byte[] buffer, int offset, int count)
				{
					if (!readStarted)
					{
						readStarted = true;
						firstRead?.Invoke();
					}

					if (throwOnRead)
						throw new IOException("controlled read failure");
					if (overflowOnRead)
						throw new OverflowException("controlled external read overflow");
					return base.Read(buffer, offset, count);
				}
			}
		}

		sealed class TestPackage : IReadOnlyPackage
		{
			readonly IReadOnlyDictionary<string, byte[]> files;
			public string Name => "runtime-contract-builder-test";
			public IEnumerable<string> Contents => files.Keys;

			public TestPackage(params (string Name, byte[] Data)[] files)
			{
				this.files = files.ToDictionary(file => file.Name, file => file.Data, StringComparer.Ordinal);
			}

			public Stream GetStream(string filename) => files.TryGetValue(filename, out var data)
				? new MemoryStream(data, writable: false) : null;
			public bool Contains(string filename) => files.ContainsKey(filename);
			public IReadOnlyPackage OpenPackage(string filename, OpenRA.FileSystem.FileSystem context) => null;
			public void Dispose() { }
		}
	}
}
