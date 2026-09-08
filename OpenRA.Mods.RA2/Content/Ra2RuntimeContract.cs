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

namespace OpenRA.Mods.RA2.Content
{
	public sealed class Ra2RuntimeContract
	{
		public Ra2RuntimeProfileId ProfileId { get; }
		public string ContractDigest { get; }
		public string FilenameCatalogDigest { get; }
		public int MapFormatVersion { get; }

		internal Ra2RuntimeContract(
			Ra2RuntimeProfileId profileId,
			string contractDigest,
			string filenameCatalogDigest,
			int mapFormatVersion)
		{
			if (!RuntimeContractValidation.IsKnownProfile(profileId))
				throw new ArgumentOutOfRangeException(nameof(profileId));
			if (!RuntimeContractDescriptorFraming.IsLowerHexSha256(contractDigest))
				throw new ArgumentException("The runtime contract digest is not canonical SHA-256.",
					nameof(contractDigest));
			if (!RuntimeContractDescriptorFraming.IsLowerHexSha256(filenameCatalogDigest))
				throw new ArgumentException("The filename-catalog digest is not canonical SHA-256.",
					nameof(filenameCatalogDigest));
			if (mapFormatVersion <= 0)
				throw new ArgumentOutOfRangeException(nameof(mapFormatVersion));

			ProfileId = profileId;
			ContractDigest = contractDigest;
			FilenameCatalogDigest = filenameCatalogDigest;
			MapFormatVersion = mapFormatVersion;
		}
	}

	public sealed class Ra2RuntimeContractExpectation
	{
		public Ra2RuntimeContract Contract { get; }
		internal RuntimeContractInputs Inputs { get; }
		internal TrustedManifestSelection Selection { get; }
		internal ImmutableArray<string> IncludedFiles { get; }
		internal ImmutableArray<string> Cultures { get; }
		internal ImmutableArray<string> RuntimeContractFiles { get; }

		internal Ra2RuntimeContractExpectation(
			RuntimeContractInputs inputs,
			Ra2RuntimeContract contract,
			TrustedManifestSelection selection,
			ImmutableArray<string> includedFiles,
			ImmutableArray<string> cultures,
			ImmutableArray<string> runtimeContractFiles)
		{
			Inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
			Contract = contract ?? throw new ArgumentNullException(nameof(contract));
			if (!RuntimeContractValidation.IsKnownProfile(contract.ProfileId) ||
				selection != contract.ProfileId.ManifestSelection())
				throw new ArgumentException("The manifest selection does not match the runtime profile.",
					nameof(selection));
			var expectedIncludes = contract.ProfileId == Ra2RuntimeProfileId.Base
				? new[]
				{
					"manifest/common.yaml",
					"manifest/gameplay-base.yaml",
					"manifest/filesystem-ios-base.yaml",
				}
				: new[]
				{
					"manifest/common.yaml",
					"manifest/gameplay-yuri.yaml",
					"manifest/filesystem-ios-yuri.yaml",
				};
			ValidateProjection(includedFiles, nameof(includedFiles), requireNonempty: true,
				requireSorted: false);
			if (!includedFiles.SequenceEqual(expectedIncludes) ||
				includedFiles.Any(path => !IosRuntimeResourcePath.TrySplit(path, out _)))
				throw new ArgumentException("The manifest fragment projection does not match the profile.",
					nameof(includedFiles));
			ValidateProjection(cultures, nameof(cultures), requireNonempty: true,
				requireSorted: true);
			if (!cultures.SequenceEqual(new[] { "zh-CN" }))
				throw new ArgumentException("The runtime contract culture projection is not authenticated.",
					nameof(cultures));
			ValidateProjection(runtimeContractFiles, nameof(runtimeContractFiles), requireNonempty: true,
				requireSorted: true);
			if (runtimeContractFiles.Any(path => !IosRuntimeResourcePath.TrySplit(path, out _)))
				throw new ArgumentException("The runtime contract file projection is not canonical.",
					nameof(runtimeContractFiles));

			Selection = selection;
			IncludedFiles = Copy(includedFiles);
			Cultures = Copy(cultures);
			RuntimeContractFiles = Copy(runtimeContractFiles);
		}

		internal static ImmutableArray<string> Copy(ImmutableArray<string> values)
		{
			var copy = ImmutableArray.CreateBuilder<string>(values.Length);
			copy.AddRange(values);
			return copy.MoveToImmutable();
		}

		internal static void ValidateProjection(
			ImmutableArray<string> values,
			string parameterName,
			bool requireNonempty,
			bool requireSorted)
		{
			if (values.IsDefault || (requireNonempty && values.Length == 0) ||
				values.Any(string.IsNullOrEmpty))
				throw new ArgumentException("The runtime contract projection is incomplete.", parameterName);

			var exact = new HashSet<string>(StringComparer.Ordinal);
			var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for (var index = 0; index < values.Length; index++)
			{
				if (!exact.Add(values[index]) || !aliases.Add(values[index]) ||
					(requireSorted && index > 0 && RuntimeContractDescriptorFraming.CompareUtf8(
						values[index - 1], values[index]) >= 0))
					throw new ArgumentException(
						"The runtime contract projection is not canonical and unique.", parameterName);
			}
		}
	}

	public sealed class VerifiedRa2RuntimeContract
	{
		public Ra2RuntimeContract Contract { get; }
		internal TrustedManifestSelection Selection { get; }
		internal ImmutableArray<string> MapCompatibility { get; }

		internal VerifiedRa2RuntimeContract(
			Ra2RuntimeContractExpectation expectation,
			ImmutableArray<string> mapCompatibility)
		{
			if (expectation == null)
				throw new ArgumentNullException(nameof(expectation));
			ValidateMapCompatibility(mapCompatibility, expectation.Selection.ModId);

			Contract = expectation.Contract;
			Selection = expectation.Selection;
			MapCompatibility = Ra2RuntimeContractExpectation.Copy(mapCompatibility);
		}

		internal static void ValidateMapCompatibility(
			ImmutableArray<string> mapCompatibility,
			string selectedModId)
		{
			Ra2RuntimeContractExpectation.ValidateProjection(
				mapCompatibility, nameof(mapCompatibility), requireNonempty: true, requireSorted: true);
			if (!IsCanonicalModId(selectedModId) ||
				mapCompatibility.Any(value => !IsCanonicalModId(value)) ||
				mapCompatibility.Count(value => StringComparer.Ordinal.Equals(
					value, selectedModId)) != 1)
				throw new ArgumentException("The map-compatibility projection is not canonical.",
					nameof(mapCompatibility));
		}

		static bool IsCanonicalModId(string value)
		{
			if (value == null || value.Length is < 1 or > 32)
				return false;
			return value.All(character => character is >= 'a' and <= 'z' ||
				character is >= '0' and <= '9' || character == '-');
		}
	}
}
