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
using System.Text;
using System.Threading;

namespace OpenRA.Mods.RA2.Content
{
	public sealed class ManagedImageIdentity
	{
		public string BundleFilename { get; }
		public string Sha256 { get; }
		public Guid ModuleMvid { get; }

		internal ManagedImageIdentity(
			string bundleFilename,
			string sha256,
			Guid moduleMvid)
		{
			BundleFilename = RuntimeContractValidation.ManagedImageFilename(
				bundleFilename, nameof(bundleFilename));
			if (!RuntimeContractDescriptorFraming.IsLowerHexSha256(sha256))
				throw new ArgumentException("The managed-image hash is not canonical SHA-256.", nameof(sha256));
			if (moduleMvid == Guid.Empty)
				throw new ArgumentException("The managed-image MVID must not be empty.", nameof(moduleMvid));

			Sha256 = sha256;
			ModuleMvid = moduleMvid;
		}
	}

	public sealed class RuntimeBuildIdentity
	{
		static readonly ImmutableArray<string> RequiredProjectImages = ImmutableArray.Create(
			"OpenRA.Game.dll",
			"OpenRA.Mods.Cnc.dll",
			"OpenRA.Mods.Common.dll",
			"OpenRA.Mods.RA2.dll",
			"OpenRA.Platforms.Default.dll",
			"OpenRA.iOS.dll");

		public ImmutableArray<ManagedImageIdentity> ManagedImages { get; }
		public string AotRegistryIdentity { get; }

		internal RuntimeBuildIdentity(
			ImmutableArray<ManagedImageIdentity> managedImages,
			string aotRegistryIdentity)
		{
			if (managedImages.IsDefault || managedImages.Length < RequiredProjectImages.Length ||
				managedImages.Any(image => image == null))
				throw new ArgumentException("The managed-image identity set is incomplete.", nameof(managedImages));
			if (!RuntimeContractDescriptorFraming.IsLowerHexSha256(aotRegistryIdentity))
				throw new ArgumentException("The AOT registry identity is not canonical SHA-256.",
					nameof(aotRegistryIdentity));

			var exact = new HashSet<string>(StringComparer.Ordinal);
			var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for (var index = 0; index < managedImages.Length; index++)
			{
				var image = managedImages[index];
				RuntimeContractValidation.ManagedImageFilename(
					image.BundleFilename, nameof(managedImages));
				if (!exact.Add(image.BundleFilename) || !aliases.Add(image.BundleFilename) ||
					(index > 0 && RuntimeContractDescriptorFraming.CompareUtf8(
						managedImages[index - 1].BundleFilename, image.BundleFilename) >= 0))
					throw new ArgumentException(
						"Managed images must be unique and use canonical UTF-8 byte order.", nameof(managedImages));
			}

			if (RequiredProjectImages.Any(required => !exact.Contains(required)))
				throw new ArgumentException("The managed-image identity set omits a required project image.",
					nameof(managedImages));

			var copy = ImmutableArray.CreateBuilder<ManagedImageIdentity>(managedImages.Length);
			copy.AddRange(managedImages);
			ManagedImages = copy.MoveToImmutable();
			AotRegistryIdentity = aotRegistryIdentity;
		}
	}

	interface IRuntimeBuildIdentityProvider
	{
		RetailOperationResult<RuntimeBuildIdentity> Capture(
			RetailValidationDeadline alreadyStartedDeadline,
			IRetailMonotonicClock clock,
			CancellationToken cancellationToken);
	}

	interface IRuntimeManagedImageSource : IDisposable
	{
		ImmutableArray<string> EnumerateAppLocalManagedImages();
		IRuntimeManagedImageLease AcquirePinned(string canonicalBundleFilename);
	}

	interface IRuntimeManagedImageLease : IDisposable
	{
		string CanonicalBundleFilename { get; }
		long Length { get; }
		Guid ModuleMvid { get; }
		Guid? LoadedModuleMvid { get; }
		Stream OpenRead();
		void VerifyUnchanged();
	}

	sealed class RuntimeContractInputs
	{
		internal RuntimeContractDescriptorInventory Inventory { get; }
		internal RuntimeBuildIdentity BuildIdentity { get; }

		internal RuntimeContractInputs(
			RuntimeContractDescriptorInventory inventory,
			RuntimeBuildIdentity buildIdentity)
		{
			Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
			BuildIdentity = buildIdentity ?? throw new ArgumentNullException(nameof(buildIdentity));
		}
	}

	static class RuntimeContractValidation
	{
		static readonly UTF8Encoding StrictUtf8 = new(false, true);

		public static bool IsKnownProfile(Ra2RuntimeProfileId profile) =>
			profile is Ra2RuntimeProfileId.Base or Ra2RuntimeProfileId.Yuri;

		public static string ManagedImageFilename(string value, string parameterName)
		{
			if (!TryCanonicalLeaf(value) || !value.EndsWith(".dll", StringComparison.Ordinal))
				throw new ArgumentException("The managed-image filename is not canonical.", parameterName);

			return value;
		}

		public static bool TryCanonicalLeaf(string value)
		{
			try
			{
				return !string.IsNullOrEmpty(value) && value is not "." and not ".." &&
					value.IndexOfAny(new[] { '/', '\\', '\0', ':' }) < 0 &&
					value.IsNormalized(NormalizationForm.FormC) &&
					!value.Any(character => char.IsControl(character) ||
						character == '\u2028' || character == '\u2029') &&
					StrictUtf8.GetByteCount(value) <= 255;
			}
			catch (ArgumentException)
			{
				return false;
			}
		}
	}
}
