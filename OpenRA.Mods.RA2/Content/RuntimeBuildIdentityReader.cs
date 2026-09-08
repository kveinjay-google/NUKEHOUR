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
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Threading;

namespace OpenRA.Mods.RA2.Content
{
	sealed class RuntimeBuildIdentityReader
	{
		readonly IRuntimeManagedImageSource images;
		readonly string canonicalAotRegistryIdentity;

		internal RuntimeBuildIdentityReader(
			IRuntimeManagedImageSource images,
			string canonicalAotRegistryIdentity)
		{
			this.images = images ?? throw new ArgumentNullException(nameof(images));
			if (!RuntimeContractDescriptorFraming.IsLowerHexSha256(canonicalAotRegistryIdentity))
				throw new ArgumentException("The AOT registry identity is not canonical SHA-256.",
					nameof(canonicalAotRegistryIdentity));

			this.canonicalAotRegistryIdentity = canonicalAotRegistryIdentity;
		}

		internal RetailOperationResult<RuntimeBuildIdentity> Capture(
			RetailValidationDeadline alreadyStartedDeadline,
			IRetailMonotonicClock clock,
			CancellationToken cancellationToken)
		{
			if (clock == null || !alreadyStartedDeadline.MatchesClock(clock))
				return RetailOperationResult<RuntimeBuildIdentity>.Failed(
					RetailRuntimeContractFailures.ProfileMismatch());

			var checkpoint = RetailRuntimeContractFailures.Checkpoint(
				alreadyStartedDeadline, clock, cancellationToken);
			if (checkpoint != null)
				return RetailOperationResult<RuntimeBuildIdentity>.Failed(checkpoint);

			var leases = new List<IRuntimeManagedImageLease>();
			try
			{
				var firstEnumerationSucceeded = TryEnumerateCanonical(out var firstNames);
				checkpoint = RetailRuntimeContractFailures.Checkpoint(
					alreadyStartedDeadline, clock, cancellationToken);
				if (checkpoint != null)
					return RetailOperationResult<RuntimeBuildIdentity>.Failed(checkpoint);
				if (!firstEnumerationSucceeded)
					return Missing();

				var identities = ImmutableArray.CreateBuilder<ManagedImageIdentity>(firstNames.Length);
				foreach (var filename in firstNames)
				{
					checkpoint = RetailRuntimeContractFailures.Checkpoint(
						alreadyStartedDeadline, clock, cancellationToken);
					if (checkpoint != null)
						return RetailOperationResult<RuntimeBuildIdentity>.Failed(checkpoint);

					IRuntimeManagedImageLease lease;
					try
					{
						lease = images.AcquirePinned(filename);
					}
					catch (Exception)
					{
						return CheckpointOrMissing(alreadyStartedDeadline, clock, cancellationToken);
					}

					if (lease != null)
						leases.Add(lease);
					checkpoint = RetailRuntimeContractFailures.Checkpoint(
						alreadyStartedDeadline, clock, cancellationToken);
					if (checkpoint != null)
						return RetailOperationResult<RuntimeBuildIdentity>.Failed(checkpoint);
					if (lease == null)
						return Missing();

					string leasedFilename;
					long leasedLength;
					Guid leasedMvid;
					Guid? loadedMvid;
					try
					{
						leasedFilename = lease.CanonicalBundleFilename;
						leasedLength = lease.Length;
						leasedMvid = lease.ModuleMvid;
						loadedMvid = lease.LoadedModuleMvid;
					}
					catch (Exception)
					{
						return CheckpointOrMissing(alreadyStartedDeadline, clock, cancellationToken);
					}

					checkpoint = RetailRuntimeContractFailures.Checkpoint(
						alreadyStartedDeadline, clock, cancellationToken);
					if (checkpoint != null)
						return RetailOperationResult<RuntimeBuildIdentity>.Failed(checkpoint);
					if (!StringComparer.Ordinal.Equals(leasedFilename, filename) ||
						leasedLength < 0 || leasedMvid == Guid.Empty ||
						(loadedMvid.HasValue && loadedMvid.Value != leasedMvid))
						return Missing();

					var (sha256, moduleMvid, readFailure) = ReadIdentity(
						lease,
						leasedLength,
						alreadyStartedDeadline,
						clock,
						cancellationToken);
					checkpoint = RetailRuntimeContractFailures.Checkpoint(
						alreadyStartedDeadline, clock, cancellationToken);
					if (checkpoint != null)
						return RetailOperationResult<RuntimeBuildIdentity>.Failed(checkpoint);
					if (readFailure != null)
						return RetailOperationResult<RuntimeBuildIdentity>.Failed(readFailure);
					if (moduleMvid != leasedMvid)
						return Missing();

					identities.Add(new ManagedImageIdentity(filename, sha256, moduleMvid));
				}

				checkpoint = RetailRuntimeContractFailures.Checkpoint(
					alreadyStartedDeadline, clock, cancellationToken);
				if (checkpoint != null)
					return RetailOperationResult<RuntimeBuildIdentity>.Failed(checkpoint);
				var secondEnumerationSucceeded = TryEnumerateCanonical(out var secondNames);
				checkpoint = RetailRuntimeContractFailures.Checkpoint(
					alreadyStartedDeadline, clock, cancellationToken);
				if (checkpoint != null)
					return RetailOperationResult<RuntimeBuildIdentity>.Failed(checkpoint);
				if (!secondEnumerationSucceeded || !firstNames.SequenceEqual(secondNames))
					return Missing();

				foreach (var lease in leases)
				{
					checkpoint = RetailRuntimeContractFailures.Checkpoint(
						alreadyStartedDeadline, clock, cancellationToken);
					if (checkpoint != null)
						return RetailOperationResult<RuntimeBuildIdentity>.Failed(checkpoint);

					try
					{
						lease.VerifyUnchanged();
					}
					catch (Exception)
					{
						return CheckpointOrMissing(alreadyStartedDeadline, clock, cancellationToken);
					}

					checkpoint = RetailRuntimeContractFailures.Checkpoint(
						alreadyStartedDeadline, clock, cancellationToken);
					if (checkpoint != null)
						return RetailOperationResult<RuntimeBuildIdentity>.Failed(checkpoint);
				}

				checkpoint = RetailRuntimeContractFailures.Checkpoint(
					alreadyStartedDeadline, clock, cancellationToken);
				if (checkpoint != null)
					return RetailOperationResult<RuntimeBuildIdentity>.Failed(checkpoint);

				try
				{
					return RetailOperationResult<RuntimeBuildIdentity>.Success(
						new RuntimeBuildIdentity(identities.MoveToImmutable(), canonicalAotRegistryIdentity));
				}
				catch (Exception)
				{
					return Missing();
				}
			}
			catch (Exception)
			{
				return CheckpointOrMissing(alreadyStartedDeadline, clock, cancellationToken);
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
						// Capture owns every acquired lease and never exposes cleanup exceptions.
					}
				}
			}
		}

		bool TryEnumerateCanonical(out ImmutableArray<string> canonical)
		{
			canonical = default;
			try
			{
				var enumerated = images.EnumerateAppLocalManagedImages();
				if (enumerated.IsDefault || enumerated.Length == 0)
					return false;

				var exact = new HashSet<string>(StringComparer.Ordinal);
				var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				var builder = ImmutableArray.CreateBuilder<string>(enumerated.Length);
				foreach (var filename in enumerated)
				{
					RuntimeContractValidation.ManagedImageFilename(filename, nameof(enumerated));
					if (!exact.Add(filename) || !aliases.Add(filename))
						return false;
					builder.Add(filename);
				}

				builder.Sort(RuntimeContractDescriptorFraming.CompareUtf8);
				canonical = builder.MoveToImmutable();
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		static (string Sha256, Guid ModuleMvid, RetailImportFailure Failure) ReadIdentity(
			IRuntimeManagedImageLease lease,
			long expectedLength,
			RetailValidationDeadline deadline,
			IRetailMonotonicClock clock,
			CancellationToken cancellationToken)
		{
			try
			{
				using var stream = lease.OpenRead();
				var checkpoint = RetailRuntimeContractFailures.Checkpoint(
					deadline, clock, cancellationToken);
				if (checkpoint != null)
					return (null, default, checkpoint);
				var usable = stream != null && stream.CanRead && stream.CanSeek;
				checkpoint = RetailRuntimeContractFailures.Checkpoint(
					deadline, clock, cancellationToken);
				if (checkpoint != null)
					return (null, default, checkpoint);
				if (!usable)
					return (null, default, RetailRuntimeContractFailures.RuntimeMissing());

				using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
				var buffer = new byte[81920];
				long total = 0;
				while (true)
				{
					checkpoint = RetailRuntimeContractFailures.Checkpoint(
						deadline, clock, cancellationToken);
					if (checkpoint != null)
						return (null, default, checkpoint);

					var count = stream.Read(buffer, 0, buffer.Length);
					checkpoint = RetailRuntimeContractFailures.Checkpoint(
						deadline, clock, cancellationToken);
					if (checkpoint != null)
						return (null, default, checkpoint);
					if (count < 0 || count > buffer.Length)
						return (null, default, RetailRuntimeContractFailures.RuntimeMissing());
					if (count == 0)
						break;

					if (!RetailRuntimeContractFailures.TryAccumulateReadLength(
						total, count, out total))
						return (null, default, RetailRuntimeContractFailures.ArithmeticOverflow());
					if (total > expectedLength)
						return (null, default, RetailRuntimeContractFailures.RuntimeMissing());
					hash.AppendData(buffer, 0, count);
					checkpoint = RetailRuntimeContractFailures.Checkpoint(
						deadline, clock, cancellationToken);
					if (checkpoint != null)
						return (null, default, checkpoint);
				}

				if (total != expectedLength)
					return (null, default, RetailRuntimeContractFailures.RuntimeMissing());
				var beforeMetadata = RetailRuntimeContractFailures.Checkpoint(
					deadline, clock, cancellationToken);
				if (beforeMetadata != null)
					return (null, default, beforeMetadata);

				stream.Position = 0;
				checkpoint = RetailRuntimeContractFailures.Checkpoint(
					deadline, clock, cancellationToken);
				if (checkpoint != null)
					return (null, default, checkpoint);
				using var portableExecutable = new PEReader(stream, PEStreamOptions.LeaveOpen);
				var hasMetadata = portableExecutable.HasMetadata;
				checkpoint = RetailRuntimeContractFailures.Checkpoint(
					deadline, clock, cancellationToken);
				if (checkpoint != null)
					return (null, default, checkpoint);
				if (!hasMetadata)
					return (null, default, RetailRuntimeContractFailures.RuntimeMissing());
				var metadata = portableExecutable.GetMetadataReader();
				var mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
				var afterMetadata = RetailRuntimeContractFailures.Checkpoint(
					deadline, clock, cancellationToken);
				if (afterMetadata != null)
					return (null, default, afterMetadata);
				if (mvid == Guid.Empty)
					return (null, default, RetailRuntimeContractFailures.RuntimeMissing());

				return (
					Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
					mvid,
					null);
			}
			catch (Exception)
			{
				return (null, default,
					RetailRuntimeContractFailures.Checkpoint(deadline, clock, cancellationToken) ??
					RetailRuntimeContractFailures.RuntimeMissing());
			}
		}

		static RetailOperationResult<RuntimeBuildIdentity> Missing() =>
			RetailOperationResult<RuntimeBuildIdentity>.Failed(
				RetailRuntimeContractFailures.RuntimeMissing());

		static RetailOperationResult<RuntimeBuildIdentity> CheckpointOrMissing(
			RetailValidationDeadline deadline,
			IRetailMonotonicClock clock,
			CancellationToken cancellationToken) =>
			RetailOperationResult<RuntimeBuildIdentity>.Failed(
				RetailRuntimeContractFailures.Checkpoint(deadline, clock, cancellationToken) ??
				RetailRuntimeContractFailures.RuntimeMissing());
	}

	static class RetailRuntimeContractFailures
	{
		public static RetailImportFailure Checkpoint(
			RetailValidationDeadline deadline,
			IRetailMonotonicClock clock,
			CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
				return new RetailImportFailure(
					RetailImportErrorCode.Cancelled, null, null, RetailImportDiagnosticCode.None);

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
			catch (Exception)
			{
				return ProfileMismatch();
			}
		}

		public static RetailImportFailure ProfileMismatch() => new(
			RetailImportErrorCode.ProfileContextFailure,
			null,
			null,
			RetailImportDiagnosticCode.ProfileMetadataMismatch);

		public static RetailImportFailure RuntimeMissing() => new(
			RetailImportErrorCode.RuntimeResourcesMissing,
			null,
			null,
			RetailImportDiagnosticCode.InvalidMapPackage);

		public static RetailImportFailure ArithmeticOverflow() => new(
			RetailImportErrorCode.RuntimeResourcesMissing,
			null,
			null,
			RetailImportDiagnosticCode.ArithmeticOverflow);

		public static bool TryAccumulateReadLength(
			long current,
			int read,
			out long accumulated)
		{
			try
			{
				accumulated = checked(current + read);
				return true;
			}
			catch (OverflowException)
			{
				accumulated = default;
				return false;
			}
		}
	}
}
