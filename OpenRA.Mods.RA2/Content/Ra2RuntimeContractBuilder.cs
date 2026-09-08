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
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace OpenRA.Mods.RA2.Content
{
	sealed class Ra2RuntimeContractBuilder
	{
		const string ContractMagic = "OpenRA.RA2RuntimeContract";
		const int ContractSchemaVersion = 1;
		static readonly UTF8Encoding StrictUtf8 = new(false, true);
		static readonly ImmutableArray<string> BaseFragments = ImmutableArray.Create(
			"manifest/common.yaml",
			"manifest/gameplay-base.yaml",
			"manifest/filesystem-ios-base.yaml");
		static readonly ImmutableArray<string> YuriFragments = ImmutableArray.Create(
			"manifest/common.yaml",
			"manifest/gameplay-yuri.yaml",
			"manifest/filesystem-ios-yuri.yaml");
		static readonly ImmutableArray<string> ContractCultures = ImmutableArray.Create("zh-CN");

		readonly RuntimeContractInputs inputs;

		internal Ra2RuntimeContractBuilder(RuntimeContractInputs inputs)
		{
			this.inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
		}

		internal RetailOperationResult<Ra2RuntimeContractExpectation> BuildExpected(
			Ra2RuntimeProfileId profile,
			TrustedManifestSelection selection,
			Manifest resolvedManifest,
			string filenameCatalogDigest,
			int mapFormatVersion,
			int archiveValidatorVersion,
			int mapValidatorVersion,
			RetailValidationDeadline alreadyStartedDeadline,
			IRetailMonotonicClock clock,
			CancellationToken cancellationToken)
		{
			if (clock == null || !alreadyStartedDeadline.MatchesClock(clock))
				return ProfileFailure();
			var checkpoint = RetailRuntimeContractFailures.Checkpoint(
				alreadyStartedDeadline, clock, cancellationToken);
			if (checkpoint != null)
				return RetailOperationResult<Ra2RuntimeContractExpectation>.Failed(checkpoint);

			if (!TryValidateContext(
				profile,
				selection,
				resolvedManifest,
				filenameCatalogDigest,
				mapFormatVersion,
				archiveValidatorVersion,
				mapValidatorVersion,
				out var applicable))
				return ProfileFailure();

			try
			{
				using var framed = new MemoryStream();
				WriteLengthPrefixed(framed, ContractMagic);
				WriteUInt32(framed, ContractSchemaVersion);
				WriteUInt32(framed, ProfileTag(profile));
				WriteLengthPrefixed(framed, selection.ModId);
				WriteLengthPrefixed(framed, selection.EntryName);
				WriteStrings(framed, resolvedManifest.IncludedFiles);
				WriteStrings(framed, resolvedManifest.RuntimeContractCultures);
				WriteUInt32(framed, inputs.Inventory.SchemaVersion);
				WriteUInt32(framed, inputs.Inventory.DescriptorCount);
				WriteHex(framed, inputs.Inventory.ShapeDigest);
				WriteUInt32(framed, applicable.Length);

				foreach (var row in applicable)
				{
					checkpoint = RetailRuntimeContractFailures.Checkpoint(
						alreadyStartedDeadline, clock, cancellationToken);
					if (checkpoint != null)
						return RetailOperationResult<Ra2RuntimeContractExpectation>.Failed(checkpoint);

					WriteUInt32(framed, row.Ordinal);
					WriteUInt32(framed, checked((int)row.Descriptor.Purpose));
					WriteUInt32(framed, row.Descriptor.Profiles.Length);
					foreach (var descriptorProfile in row.Descriptor.Profiles)
						WriteUInt32(framed, ProfileTag(descriptorProfile));
					WriteLengthPrefixed(framed, row.Descriptor.LogicalName);
					WriteLengthPrefixed(framed, row.Descriptor.CanonicalBundleRelativePath);
					WriteUInt64(framed, row.Descriptor.Length);
					WriteHex(framed, row.Descriptor.Sha256);
				}

				WriteHex(framed, filenameCatalogDigest);
				WriteUInt32(framed, mapFormatVersion);
				WriteUInt32(framed, archiveValidatorVersion);
				WriteUInt32(framed, mapValidatorVersion);
				WriteUInt32(framed, inputs.BuildIdentity.ManagedImages.Length);
				foreach (var image in inputs.BuildIdentity.ManagedImages)
				{
					checkpoint = RetailRuntimeContractFailures.Checkpoint(
						alreadyStartedDeadline, clock, cancellationToken);
					if (checkpoint != null)
						return RetailOperationResult<Ra2RuntimeContractExpectation>.Failed(checkpoint);

					WriteLengthPrefixed(framed, image.BundleFilename);
					WriteHex(framed, image.Sha256);
					WriteGuidDisplayBytes(framed, image.ModuleMvid);
				}

				WriteHex(framed, inputs.BuildIdentity.AotRegistryIdentity);
				checkpoint = RetailRuntimeContractFailures.Checkpoint(
					alreadyStartedDeadline, clock, cancellationToken);
				if (checkpoint != null)
					return RetailOperationResult<Ra2RuntimeContractExpectation>.Failed(checkpoint);

				var digest = Convert.ToHexString(SHA256.HashData(framed.ToArray())).ToLowerInvariant();
				var contract = new Ra2RuntimeContract(
					profile, digest, filenameCatalogDigest, mapFormatVersion);
				return RetailOperationResult<Ra2RuntimeContractExpectation>.Success(
					new Ra2RuntimeContractExpectation(
						inputs,
						contract,
						selection,
						resolvedManifest.IncludedFiles,
						resolvedManifest.RuntimeContractCultures,
						resolvedManifest.RuntimeContractFiles));
			}
			catch (OverflowException)
			{
				return RetailOperationResult<Ra2RuntimeContractExpectation>.Failed(
					RetailRuntimeContractFailures.ArithmeticOverflow());
			}
			catch (Exception)
			{
				return RetailOperationResult<Ra2RuntimeContractExpectation>.Failed(
					RetailRuntimeContractFailures.Checkpoint(
						alreadyStartedDeadline, clock, cancellationToken) ??
					RetailRuntimeContractFailures.ProfileMismatch());
			}
		}

		bool TryValidateContext(
			Ra2RuntimeProfileId profile,
			TrustedManifestSelection selection,
			Manifest manifest,
			string filenameCatalogDigest,
			int mapFormatVersion,
			int archiveValidatorVersion,
			int mapValidatorVersion,
			out ImmutableArray<ApplicableDescriptor> applicable)
		{
			applicable = default;
			if (!RuntimeContractValidation.IsKnownProfile(profile) || selection != profile.ManifestSelection() ||
				manifest == null ||
				!StringComparer.Ordinal.Equals(manifest.Id, selection.ModId) ||
				!StringComparer.Ordinal.Equals(manifest.EntryName, selection.EntryName) ||
				!RuntimeContractDescriptorFraming.IsLowerHexSha256(filenameCatalogDigest) ||
				mapFormatVersion <= 0 || archiveValidatorVersion <= 0 || mapValidatorVersion <= 0)
				return false;

			var expectedFragments = profile == Ra2RuntimeProfileId.Base ? BaseFragments : YuriFragments;
			if (!manifest.IncludedFiles.SequenceEqual(expectedFragments) ||
				!manifest.RuntimeContractCultures.SequenceEqual(ContractCultures) ||
				manifest.RuntimeContractFiles.IsDefaultOrEmpty)
				return false;

			var rows = ImmutableArray.CreateBuilder<ApplicableDescriptor>();
			for (var index = 0; index < inputs.Inventory.Descriptors.Length; index++)
			{
				var descriptor = inputs.Inventory.Descriptors[index];
				if (descriptor.Profiles.Contains(profile))
					rows.Add(new ApplicableDescriptor(index, descriptor));
			}

			var expectedCount = profile == Ra2RuntimeProfileId.Base ? 277 : 284;
			if (rows.Count != expectedCount)
				return false;

			var contractFiles = rows
				.Where(row => row.Descriptor.Purpose == RuntimeContractResourcePurpose.Contract)
				.Select(row => row.Descriptor.LogicalName)
				.ToImmutableArray();
			var expectedContractCount = profile == Ra2RuntimeProfileId.Base ? 146 : 153;
			if (contractFiles.Length != expectedContractCount ||
				!contractFiles.SequenceEqual(manifest.RuntimeContractFiles))
				return false;

			applicable = rows.ToImmutable();
			return true;
		}

		static void WriteStrings(Stream output, ImmutableArray<string> values)
		{
			WriteUInt32(output, values.Length);
			foreach (var value in values)
				WriteLengthPrefixed(output, value);
		}

		static void WriteLengthPrefixed(Stream output, string value)
		{
			if (value == null || !value.IsNormalized(NormalizationForm.FormC))
				throw new ArgumentException("Runtime contract strings must be strict NFC text.", nameof(value));
			var bytes = StrictUtf8.GetBytes(value);
			WriteUInt32(output, bytes.Length);
			output.Write(bytes, 0, bytes.Length);
		}

		static void WriteHex(Stream output, string value)
		{
			if (!RuntimeContractDescriptorFraming.IsLowerHexSha256(value))
				throw new ArgumentException("A runtime contract digest is not canonical SHA-256.", nameof(value));
			var bytes = Convert.FromHexString(value);
			output.Write(bytes, 0, bytes.Length);
		}

		static void WriteGuidDisplayBytes(Stream output, Guid value)
		{
			if (value == Guid.Empty)
				throw new ArgumentException("A runtime contract MVID must not be empty.", nameof(value));
			var bytes = Convert.FromHexString(value.ToString("N"));
			output.Write(bytes, 0, bytes.Length);
		}

		static void WriteUInt32(Stream output, int value)
		{
			if (value < 0)
				throw new OverflowException();
			WriteUInt32(output, checked((uint)value));
		}

		static void WriteUInt32(Stream output, uint value)
		{
			output.WriteByte((byte)value);
			output.WriteByte((byte)(value >> 8));
			output.WriteByte((byte)(value >> 16));
			output.WriteByte((byte)(value >> 24));
		}

		static void WriteUInt64(Stream output, long value)
		{
			if (value < 0)
				throw new OverflowException();
			var unsigned = checked((ulong)value);
			for (var shift = 0; shift < 64; shift += 8)
				output.WriteByte((byte)(unsigned >> shift));
		}

		static uint ProfileTag(Ra2RuntimeProfileId profile) => profile switch
		{
			Ra2RuntimeProfileId.Base => 1,
			Ra2RuntimeProfileId.Yuri => 2,
			_ => throw new ArgumentOutOfRangeException(nameof(profile)),
		};

		static RetailOperationResult<Ra2RuntimeContractExpectation> ProfileFailure() =>
			RetailOperationResult<Ra2RuntimeContractExpectation>.Failed(
				RetailRuntimeContractFailures.ProfileMismatch());

		readonly struct ApplicableDescriptor
		{
			public int Ordinal { get; }
			public RuntimeContractResourceDescriptor Descriptor { get; }

			public ApplicableDescriptor(int ordinal, RuntimeContractResourceDescriptor descriptor)
			{
				Ordinal = ordinal;
				Descriptor = descriptor;
			}
		}
	}
}
