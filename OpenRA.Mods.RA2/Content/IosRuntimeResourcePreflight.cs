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
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace OpenRA.Mods.RA2.Content
{
	sealed class IosRuntimeResourcePreflightResult
	{
		public bool StaticResourcesPresent { get; }
		public RetailImportFailure Failure { get; }

		IosRuntimeResourcePreflightResult(
			bool staticResourcesPresent, RetailImportFailure failure)
		{
			if (staticResourcesPresent == (failure != null))
				throw new ArgumentException("A preflight result must contain exactly one state.", nameof(failure));

			if (failure != null && !IsClosedFailure(failure))
				throw new ArgumentException("The preflight failure is not a closed static-resource outcome.", nameof(failure));

			StaticResourcesPresent = staticResourcesPresent;
			Failure = failure;
		}

		internal static IosRuntimeResourcePreflightResult Present() =>
			new(true, null);

		internal static IosRuntimeResourcePreflightResult Missing(
			RetailImportFailure failure) => new(false,
				failure ?? throw new ArgumentNullException(nameof(failure)));

		static bool IsClosedFailure(RetailImportFailure failure)
		{
			if (failure.Group != null || failure.SafeBasename != null)
				return false;

			return failure.Code switch
			{
				RetailImportErrorCode.Cancelled =>
					failure.Diagnostic == RetailImportDiagnosticCode.None,
				RetailImportErrorCode.ValidationTimedOut =>
					failure.Diagnostic == RetailImportDiagnosticCode.DeadlineExceeded,
				RetailImportErrorCode.ProfileContextFailure =>
					failure.Diagnostic == RetailImportDiagnosticCode.ProfileMetadataMismatch ||
					failure.Diagnostic == RetailImportDiagnosticCode.InvalidMapPackage,
				RetailImportErrorCode.RuntimeResourcesMissing =>
					failure.Diagnostic is RetailImportDiagnosticCode.InvalidMapPackage or
						RetailImportDiagnosticCode.ArithmeticOverflow,
				_ => false,
			};
		}
	}

	public enum RuntimeContractResourcePurpose
	{
		Contract = 1,
		PreflightOnly = 2,
	}

	public sealed class RuntimeContractResourceDescriptor
	{
		public string LogicalName { get; }
		public string CanonicalBundleRelativePath { get; }
		public long Length { get; }
		public string Sha256 { get; }
		public ImmutableArray<Ra2RuntimeProfileId> Profiles { get; }
		public RuntimeContractResourcePurpose Purpose { get; }

		internal RuntimeContractResourceDescriptor(
			string logicalName,
			string canonicalBundleRelativePath,
			long length,
			string sha256,
			ImmutableArray<Ra2RuntimeProfileId> profiles,
			RuntimeContractResourcePurpose purpose)
		{
			if (!IosRuntimeResourcePath.TrySplit(logicalName, out _) ||
				!IosRuntimeResourcePath.TrySplit(canonicalBundleRelativePath, out _))
				throw new ArgumentException("Runtime descriptor paths must be canonical package-relative paths.");
			if (length < 0)
				throw new ArgumentOutOfRangeException(nameof(length));
			if (!RuntimeContractDescriptorFraming.IsLowerHexSha256(sha256))
				throw new ArgumentException("The runtime descriptor hash is not canonical SHA-256.", nameof(sha256));
			if (profiles.IsDefault || profiles.Length == 0 || profiles.Length > 2 ||
				profiles.Any(profile => profile != Ra2RuntimeProfileId.Base && profile != Ra2RuntimeProfileId.Yuri) ||
				!profiles.SequenceEqual(profiles.OrderBy(profile => profile)) ||
				profiles.Distinct().Count() != profiles.Length)
				throw new ArgumentException("The runtime descriptor profiles are not a closed canonical set.", nameof(profiles));
			if (purpose != RuntimeContractResourcePurpose.Contract &&
				purpose != RuntimeContractResourcePurpose.PreflightOnly)
				throw new ArgumentOutOfRangeException(nameof(purpose));

			LogicalName = logicalName;
			CanonicalBundleRelativePath = canonicalBundleRelativePath;
			Length = length;
			Sha256 = sha256;
			var profileCopy = ImmutableArray.CreateBuilder<Ra2RuntimeProfileId>(profiles.Length);
			profileCopy.AddRange(profiles);
			Profiles = profileCopy.MoveToImmutable();
			Purpose = purpose;
		}
	}

	public sealed class RuntimeContractDescriptorInventory
	{
		internal const int AuthenticatedSchemaVersion = 1;
		internal const int AuthenticatedDescriptorCount = 296;
		internal const string AuthenticatedShapeDigest =
			"4fe820475ee4d6ef843320f406b5db3fd414fd106c953b4a7b4a05020fb3614d";

		public int SchemaVersion { get; }
		public int DescriptorCount { get; }
		public string ShapeDigest { get; }
		public ImmutableArray<RuntimeContractResourceDescriptor> Descriptors { get; }

		internal RuntimeContractDescriptorInventory(
			int schemaVersion,
			int descriptorCount,
			string shapeDigest,
			ImmutableArray<RuntimeContractResourceDescriptor> descriptors)
		{
			if (schemaVersion != AuthenticatedSchemaVersion)
				throw new ArgumentOutOfRangeException(nameof(schemaVersion));
			if (descriptorCount != AuthenticatedDescriptorCount)
				throw new ArgumentOutOfRangeException(nameof(descriptorCount));
			if (!StringComparer.Ordinal.Equals(shapeDigest, AuthenticatedShapeDigest) ||
				!RuntimeContractDescriptorFraming.IsLowerHexSha256(shapeDigest))
				throw new ArgumentException("The runtime descriptor shape digest is not authenticated.", nameof(shapeDigest));
			if (descriptors.IsDefault || descriptors.Length != descriptorCount ||
				descriptors.Any(descriptor => descriptor == null))
				throw new ArgumentException("The runtime descriptor collection is incomplete.", nameof(descriptors));

			var logicalNames = new HashSet<string>(StringComparer.Ordinal);
			var logicalAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var paths = new HashSet<string>(StringComparer.Ordinal);
			var pathAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for (var index = 0; index < descriptors.Length; index++)
			{
				var descriptor = descriptors[index];
				if (!logicalNames.Add(descriptor.LogicalName) ||
					!logicalAliases.Add(descriptor.LogicalName) ||
					!paths.Add(descriptor.CanonicalBundleRelativePath) ||
					!pathAliases.Add(descriptor.CanonicalBundleRelativePath))
					throw new ArgumentException("Runtime descriptor names must be unique without case aliases.", nameof(descriptors));

				if (index > 0 && RuntimeContractDescriptorFraming.CompareUtf8(
					descriptors[index - 1].LogicalName, descriptor.LogicalName) >= 0)
					throw new ArgumentException("Runtime descriptors must use canonical UTF-8 byte order.", nameof(descriptors));
			}

			var computedDigest = RuntimeContractDescriptorFraming.ShapeDigest(
				schemaVersion, descriptors);
			if (!StringComparer.Ordinal.Equals(computedDigest, shapeDigest))
				throw new ArgumentException("The runtime descriptor projection does not match its authenticated shape.", nameof(descriptors));

			SchemaVersion = schemaVersion;
			DescriptorCount = descriptorCount;
			ShapeDigest = shapeDigest;
			var descriptorCopy = ImmutableArray.CreateBuilder<RuntimeContractResourceDescriptor>(
				descriptors.Length);
			descriptorCopy.AddRange(descriptors);
			Descriptors = descriptorCopy.MoveToImmutable();
		}
	}

	interface IIosRuntimeResourcePreflight
	{
		IosRuntimeResourcePreflightResult Check(
			Manifest resolvedManifest,
			IIosRuntimeResourceRoot distributionRoot,
			Ra2RuntimeProfileId profile,
			RetailValidationDeadline alreadyStartedDeadline,
			IRetailMonotonicClock clock,
			CancellationToken cancellationToken);
	}

	sealed class IosRuntimeResourcePreflight : IIosRuntimeResourcePreflight
	{
		static readonly ImmutableArray<string> BaseFragments = ImmutableArray.Create(
			"manifest/common.yaml",
			"manifest/gameplay-base.yaml",
			"manifest/filesystem-ios-base.yaml");
		static readonly ImmutableArray<string> YuriFragments = ImmutableArray.Create(
			"manifest/common.yaml",
			"manifest/gameplay-yuri.yaml",
			"manifest/filesystem-ios-yuri.yaml");
		static readonly ImmutableArray<string> ContractCultures = ImmutableArray.Create("zh-CN");

		readonly RuntimeContractDescriptorInventory inventory;
		readonly RuntimeContractInputs inputs;

		internal IosRuntimeResourcePreflight(
			RuntimeContractDescriptorInventory inventory)
		{
			if (inventory == null)
				throw new ArgumentNullException(nameof(inventory));

			this.inventory = inventory;
		}

		internal IosRuntimeResourcePreflight(RuntimeContractInputs inputs)
		{
			this.inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
			inventory = inputs.Inventory;
		}

		internal RetailOperationResult<VerifiedRa2RuntimeContract> Check(
			Ra2RuntimeContractExpectation expectation,
			Manifest resolvedManifest,
			IIosRuntimeResourceRoot distributionRoot,
			RetailValidationDeadline alreadyStartedDeadline,
			IRetailMonotonicClock clock,
			CancellationToken cancellationToken)
		{
			if (clock == null || !alreadyStartedDeadline.MatchesClock(clock))
				return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(
					RetailRuntimeContractFailures.ProfileMismatch());
			var checkpoint = RetailRuntimeContractFailures.Checkpoint(
				alreadyStartedDeadline, clock, cancellationToken);
			if (checkpoint != null)
				return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(checkpoint);
			if (inputs == null || expectation == null || resolvedManifest == null ||
				distributionRoot == null || !ReferenceEquals(expectation.Inputs, inputs) ||
				!ReferenceEquals(expectation.Inputs.Inventory, inventory) ||
				!ExpectationMatchesManifest(expectation, resolvedManifest) ||
				!TrySnapshotMapCompatibility(expectation, resolvedManifest, out var mapCompatibility))
				return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(
					RetailRuntimeContractFailures.ProfileMismatch());

			return CheckVerifiedWithRetainedLeases(
				expectation,
				resolvedManifest,
				distributionRoot,
				mapCompatibility,
				alreadyStartedDeadline,
				clock,
				cancellationToken);
		}

		RetailOperationResult<VerifiedRa2RuntimeContract> CheckVerifiedWithRetainedLeases(
			Ra2RuntimeContractExpectation expectation,
			Manifest resolvedManifest,
			IIosRuntimeResourceRoot distributionRoot,
			ImmutableArray<string> initialMapCompatibility,
			RetailValidationDeadline alreadyStartedDeadline,
			IRetailMonotonicClock clock,
			CancellationToken cancellationToken)
		{
			if (!TryResolveApplicable(
				resolvedManifest, expectation.Contract.ProfileId, out var firstCollection))
				return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(
					RetailRuntimeContractFailures.ProfileMismatch());

			var leases = new List<IIosRuntimeResourceLease>(firstCollection.Length);
			try
			{
				foreach (var descriptor in firstCollection)
				{
					var checkpoint = Checkpoint(
						alreadyStartedDeadline, clock, cancellationToken);
					if (checkpoint != null)
						return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(checkpoint);

					RetailOperationResult<IIosRuntimeResourceLease> acquired;
					try
					{
						acquired = distributionRoot.AcquireRegularFile(
							descriptor.CanonicalBundleRelativePath);
					}
					catch (Exception)
					{
						return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(
							Checkpoint(alreadyStartedDeadline, clock, cancellationToken) ??
							RuntimeResourcesMissing());
					}

					IIosRuntimeResourceLease lease = null;
					if (acquired?.Succeeded == true)
					{
						try
						{
							lease = acquired.Value;
						}
						catch (Exception)
						{
							return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(
								Checkpoint(alreadyStartedDeadline, clock, cancellationToken) ??
								RuntimeResourcesMissing());
						}

						if (lease != null)
							leases.Add(lease);
					}

					checkpoint = Checkpoint(alreadyStartedDeadline, clock, cancellationToken);
					if (checkpoint != null)
						return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(checkpoint);
					if (acquired?.Succeeded != true || lease == null)
						return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(
							RuntimeResourcesMissing());

					string actualPath;
					long actualLength;
					try
					{
						actualPath = lease.CanonicalRelativePath;
						actualLength = lease.Length;
					}
					catch (Exception)
					{
						return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(
							Checkpoint(alreadyStartedDeadline, clock, cancellationToken) ??
							RuntimeResourcesMissing());
					}

					checkpoint = Checkpoint(alreadyStartedDeadline, clock, cancellationToken);
					if (checkpoint != null)
						return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(checkpoint);
					if (!StringComparer.Ordinal.Equals(
						actualPath, descriptor.CanonicalBundleRelativePath) ||
						actualLength != descriptor.Length)
						return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(
							RuntimeResourcesMissing());

					var hashFailure = HashAndValidate(
						lease, descriptor, alreadyStartedDeadline, clock, cancellationToken);
					if (hashFailure != null)
						return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(hashFailure);
				}

				var finalCheckpoint = Checkpoint(
					alreadyStartedDeadline, clock, cancellationToken);
				if (finalCheckpoint != null)
					return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(finalCheckpoint);
				if (!TryResolveApplicable(
					resolvedManifest, expectation.Contract.ProfileId, out var secondCollection) ||
					!CollectionsEqual(firstCollection, secondCollection))
					return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(
						RuntimeResourcesMissing());

				foreach (var lease in leases)
				{
					finalCheckpoint = Checkpoint(
						alreadyStartedDeadline, clock, cancellationToken);
					if (finalCheckpoint != null)
						return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(finalCheckpoint);
					try
					{
						lease.VerifyUnchanged();
					}
					catch (Exception)
					{
						return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(
							Checkpoint(alreadyStartedDeadline, clock, cancellationToken) ??
							RuntimeResourcesMissing());
					}

					finalCheckpoint = Checkpoint(
						alreadyStartedDeadline, clock, cancellationToken);
					if (finalCheckpoint != null)
						return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(finalCheckpoint);
				}

				finalCheckpoint = Checkpoint(alreadyStartedDeadline, clock, cancellationToken);
				if (finalCheckpoint != null)
					return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(finalCheckpoint);
				if (!TrySnapshotMapCompatibility(
					expectation, resolvedManifest, out var finalMapCompatibility) ||
					!initialMapCompatibility.SequenceEqual(finalMapCompatibility))
					return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(
						RuntimeResourcesMissing());
				try
				{
					return RetailOperationResult<VerifiedRa2RuntimeContract>.Success(
						new VerifiedRa2RuntimeContract(
							expectation, initialMapCompatibility));
				}
				catch (Exception)
				{
					return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(
						RuntimeResourcesMissing());
				}
			}
			catch (Exception)
			{
				return RetailOperationResult<VerifiedRa2RuntimeContract>.Failed(
					Checkpoint(alreadyStartedDeadline, clock, cancellationToken) ??
					RuntimeResourcesMissing());
			}
			finally
			{
				for (var index = leases.Count - 1; index >= 0; index--)
				{
					try
					{
						leases[index]?.Dispose();
					}
					catch (Exception)
					{
						// Preflight owns every acquired lease and never exposes cleanup exceptions.
					}
				}
			}
		}

		public IosRuntimeResourcePreflightResult Check(
			Manifest resolvedManifest,
			IIosRuntimeResourceRoot distributionRoot,
			Ra2RuntimeProfileId profile,
			RetailValidationDeadline alreadyStartedDeadline,
			IRetailMonotonicClock clock,
			CancellationToken cancellationToken)
		{
			if (clock == null || !alreadyStartedDeadline.MatchesClock(clock))
				return Missing(ProfileFailure(RetailImportDiagnosticCode.ProfileMetadataMismatch));
			if (resolvedManifest == null || distributionRoot == null ||
				(profile != Ra2RuntimeProfileId.Base && profile != Ra2RuntimeProfileId.Yuri))
				return Missing(ProfileFailure(RetailImportDiagnosticCode.InvalidMapPackage));

			var checkpoint = Checkpoint(alreadyStartedDeadline, clock, cancellationToken);
			if (checkpoint != null)
				return Missing(checkpoint);

			if (!TryResolveApplicable(resolvedManifest, profile, out var firstCollection))
				return Missing(ProfileFailure(RetailImportDiagnosticCode.ProfileMetadataMismatch));

			var leases = new List<IIosRuntimeResourceLease>(firstCollection.Length);
			try
			{
				foreach (var descriptor in firstCollection)
				{
					checkpoint = Checkpoint(alreadyStartedDeadline, clock, cancellationToken);
					if (checkpoint != null)
						return Missing(checkpoint);

					RetailOperationResult<IIosRuntimeResourceLease> acquired;
					try
					{
						acquired = distributionRoot.AcquireRegularFile(
							descriptor.CanonicalBundleRelativePath);
					}
					catch
					{
						return Missing(Checkpoint(alreadyStartedDeadline, clock, cancellationToken) ??
							RuntimeResourcesMissing());
					}

					if (!acquired.Succeeded)
						return Missing(RuntimeResourcesMissing());

					var lease = acquired.Value;
					leases.Add(lease);
					if (lease == null ||
						!StringComparer.Ordinal.Equals(
							lease.CanonicalRelativePath, descriptor.CanonicalBundleRelativePath) ||
						lease.Length != descriptor.Length)
						return Missing(RuntimeResourcesMissing());

					var hashFailure = HashAndValidate(
						lease, descriptor, alreadyStartedDeadline, clock, cancellationToken);
					if (hashFailure != null)
						return Missing(hashFailure);
				}

				checkpoint = Checkpoint(alreadyStartedDeadline, clock, cancellationToken);
				if (checkpoint != null)
					return Missing(checkpoint);

				if (!TryResolveApplicable(resolvedManifest, profile, out var secondCollection) ||
					!CollectionsEqual(firstCollection, secondCollection))
					return Missing(RuntimeResourcesMissing());

				foreach (var lease in leases)
				{
					checkpoint = Checkpoint(alreadyStartedDeadline, clock, cancellationToken);
					if (checkpoint != null)
						return Missing(checkpoint);

					try
					{
						lease.VerifyUnchanged();
					}
					catch
					{
						return Missing(Checkpoint(alreadyStartedDeadline, clock, cancellationToken) ??
							RuntimeResourcesMissing());
					}
				}

				return IosRuntimeResourcePreflightResult.Present();
			}
			finally
			{
				for (var index = leases.Count - 1; index >= 0; index--)
				{
					try
					{
						leases[index]?.Dispose();
					}
					catch
					{
						// Preflight owns every acquired lease and does not expose cleanup exceptions.
					}
				}
			}
		}

		static RetailImportFailure HashAndValidate(
			IIosRuntimeResourceLease lease,
			RuntimeContractResourceDescriptor descriptor,
			RetailValidationDeadline deadline,
			IRetailMonotonicClock clock,
			CancellationToken cancellationToken)
		{
			try
			{
				using var stream = lease.OpenVerifiedRead();
				using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
				var buffer = new byte[81920];
				long total = 0;
				while (true)
				{
					var checkpoint = Checkpoint(deadline, clock, cancellationToken);
					if (checkpoint != null)
						return checkpoint;

					var read = stream.Read(buffer, 0, buffer.Length);
					checkpoint = Checkpoint(deadline, clock, cancellationToken);
					if (checkpoint != null)
						return checkpoint;
					if (read < 0 || read > buffer.Length)
						return RuntimeResourcesMissing();
					if (read == 0)
						break;

					if (!RetailRuntimeContractFailures.TryAccumulateReadLength(
						total, read, out total))
						return RetailRuntimeContractFailures.ArithmeticOverflow();
					if (total > descriptor.Length)
						return RuntimeResourcesMissing();

					hash.AppendData(buffer, 0, read);
					checkpoint = Checkpoint(deadline, clock, cancellationToken);
					if (checkpoint != null)
						return checkpoint;
				}

				var beforeDigest = Checkpoint(deadline, clock, cancellationToken);
				if (beforeDigest != null)
					return beforeDigest;
				var digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
				var afterDigest = Checkpoint(deadline, clock, cancellationToken);
				if (afterDigest != null)
					return afterDigest;

				return total == descriptor.Length &&
					StringComparer.Ordinal.Equals(digest, descriptor.Sha256)
					? null : RuntimeResourcesMissing();
			}
			catch
			{
				return Checkpoint(deadline, clock, cancellationToken) ?? RuntimeResourcesMissing();
			}
		}

		bool TryResolveApplicable(
			Manifest manifest,
			Ra2RuntimeProfileId profile,
			out ImmutableArray<RuntimeContractResourceDescriptor> applicable)
		{
			applicable = default;
			var selection = profile.ManifestSelection();
			var expectedFragments = profile == Ra2RuntimeProfileId.Base ? BaseFragments : YuriFragments;
			if (!StringComparer.Ordinal.Equals(manifest.Id, selection.ModId) ||
				!StringComparer.Ordinal.Equals(manifest.EntryName, selection.EntryName) ||
				!manifest.IncludedFiles.SequenceEqual(expectedFragments) ||
				!manifest.RuntimeContractCultures.SequenceEqual(ContractCultures))
				return false;

			var resolved = inventory.Descriptors
				.Where(descriptor => descriptor.Profiles.Contains(profile))
				.ToImmutableArray();
			var expectedCount = profile == Ra2RuntimeProfileId.Base ? 277 : 284;
			if (resolved.Length != expectedCount)
				return false;

			var requiredManifestFiles = new HashSet<string>(StringComparer.Ordinal)
			{
				"mods/ra2/" + selection.EntryName,
			};
			foreach (var fragment in expectedFragments)
				requiredManifestFiles.Add("mods/ra2/" + fragment);

			if (!requiredManifestFiles.All(required =>
				resolved.Any(descriptor => StringComparer.Ordinal.Equals(
					descriptor.LogicalName, required))))
				return false;

			applicable = resolved;
			return true;
		}

		static bool ExpectationMatchesManifest(
			Ra2RuntimeContractExpectation expectation,
			Manifest manifest)
		{
			if (!RuntimeContractValidation.IsKnownProfile(expectation.Contract.ProfileId) ||
				expectation.Selection != expectation.Contract.ProfileId.ManifestSelection() ||
				!StringComparer.Ordinal.Equals(manifest.Id, expectation.Selection.ModId) ||
				!StringComparer.Ordinal.Equals(manifest.EntryName, expectation.Selection.EntryName) ||
				!expectation.IncludedFiles.SequenceEqual(manifest.IncludedFiles) ||
				!expectation.Cultures.SequenceEqual(manifest.RuntimeContractCultures) ||
				!expectation.RuntimeContractFiles.SequenceEqual(manifest.RuntimeContractFiles))
				return false;

			return true;
		}

		static bool TrySnapshotMapCompatibility(
			Ra2RuntimeContractExpectation expectation,
			Manifest manifest,
			out ImmutableArray<string> snapshot)
		{
			snapshot = default;
			try
			{
				var first = manifest.MapCompatibility.ToImmutableArray();
				var second = manifest.MapCompatibility.ToImmutableArray();
				if (!first.SequenceEqual(second))
					return false;

				VerifiedRa2RuntimeContract.ValidateMapCompatibility(
					first, expectation.Selection.ModId);
				snapshot = first;
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		static bool CollectionsEqual(
			ImmutableArray<RuntimeContractResourceDescriptor> left,
			ImmutableArray<RuntimeContractResourceDescriptor> right)
		{
			if (left.IsDefault || right.IsDefault || left.Length != right.Length)
				return false;

			for (var index = 0; index < left.Length; index++)
			{
				var a = left[index];
				var b = right[index];
				if (!StringComparer.Ordinal.Equals(a.LogicalName, b.LogicalName) ||
					!StringComparer.Ordinal.Equals(a.CanonicalBundleRelativePath, b.CanonicalBundleRelativePath) ||
					a.Length != b.Length ||
					!StringComparer.Ordinal.Equals(a.Sha256, b.Sha256) ||
					!a.Profiles.SequenceEqual(b.Profiles) || a.Purpose != b.Purpose)
					return false;
			}

			return true;
		}

		static RetailImportFailure Checkpoint(
			RetailValidationDeadline deadline,
			IRetailMonotonicClock clock,
			CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
				return new RetailImportFailure(
					RetailImportErrorCode.Cancelled,
					null,
					null,
					RetailImportDiagnosticCode.None);

			try
			{
				deadline.ThrowIfExpired(clock);
				return null;
			}
			catch (TimeoutException)
			{
				return new RetailImportFailure(
					RetailImportErrorCode.ValidationTimedOut,
					null,
					null,
					RetailImportDiagnosticCode.DeadlineExceeded);
			}
			catch
			{
				return ProfileFailure(RetailImportDiagnosticCode.ProfileMetadataMismatch);
			}
		}

		static IosRuntimeResourcePreflightResult Missing(RetailImportFailure failure) =>
			IosRuntimeResourcePreflightResult.Missing(failure);

		static RetailImportFailure ProfileFailure(RetailImportDiagnosticCode diagnostic) => new(
			RetailImportErrorCode.ProfileContextFailure,
			null,
			null,
			diagnostic);

		static RetailImportFailure RuntimeResourcesMissing() => new(
			RetailImportErrorCode.RuntimeResourcesMissing,
			null,
			null,
			RetailImportDiagnosticCode.InvalidMapPackage);
	}

	static class RuntimeContractDescriptorFraming
	{
		const string ShapeMagic = "OpenRA.RuntimeContractDescriptorShape";
		static readonly UTF8Encoding StrictUtf8 = new(false, true);

		public static bool IsLowerHexSha256(string value) =>
			value != null && value.Length == 64 && value.All(character =>
				character is >= '0' and <= '9' || character is >= 'a' and <= 'f');

		public static int CompareUtf8(string left, string right)
		{
			var leftBytes = StrictUtf8.GetBytes(left);
			var rightBytes = StrictUtf8.GetBytes(right);
			var count = Math.Min(leftBytes.Length, rightBytes.Length);
			for (var index = 0; index < count; index++)
			{
				var comparison = leftBytes[index].CompareTo(rightBytes[index]);
				if (comparison != 0)
					return comparison;
			}

			return leftBytes.Length.CompareTo(rightBytes.Length);
		}

		public static string ShapeDigest(
			int schemaVersion,
			ImmutableArray<RuntimeContractResourceDescriptor> descriptors)
		{
			using var output = new MemoryStream();
			WriteLengthPrefixed(output, ShapeMagic);
			WriteUInt32(output, checked((uint)schemaVersion));
			WriteUInt32(output, checked((uint)descriptors.Length));
			for (var index = 0; index < descriptors.Length; index++)
			{
				using var payload = new MemoryStream();
				var descriptor = descriptors[index];
				WriteUInt32(payload, checked((uint)index));
				WriteLengthPrefixed(payload, descriptor.LogicalName);
				WriteLengthPrefixed(payload, descriptor.CanonicalBundleRelativePath);
				WriteUInt32(payload, checked((uint)descriptor.Profiles.Length));
				foreach (var profile in descriptor.Profiles)
					WriteLengthPrefixed(payload, profile.ToString());
				WriteLengthPrefixed(payload, descriptor.Purpose.ToString());
				WriteUInt32(output, checked((uint)payload.Length));
				payload.WriteTo(output);
			}

			return Convert.ToHexString(SHA256.HashData(output.ToArray())).ToLowerInvariant();
		}

		static void WriteLengthPrefixed(Stream output, string value)
		{
			var bytes = StrictUtf8.GetBytes(value);
			WriteUInt32(output, checked((uint)bytes.Length));
			output.Write(bytes, 0, bytes.Length);
		}

		static void WriteUInt32(Stream output, uint value)
		{
			output.WriteByte((byte)value);
			output.WriteByte((byte)(value >> 8));
			output.WriteByte((byte)(value >> 16));
			output.WriteByte((byte)(value >> 24));
		}
	}
}
