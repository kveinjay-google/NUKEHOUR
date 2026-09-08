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
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace OpenRA.Mods.RA2.Content
{
	public sealed partial class RetailGenerationStore
	{
		// The q capability is minted only from the live recovery acquisition.  The
		// directory entry is descriptor-pinned by Enumerate and is consumed by the
		// adapter, so neither a pathname nor a different lock acquisition can be
		// substituted between reauthorization and rename.
		internal RetailOperationResult<RetailImportDirectoryEntry> QuarantineForRecovery(
			RetailRecoverySession recovery, RetailImportDirectoryEntry entry)
		{
			if (recovery == null || entry == null)
				return RetailOperationResult<RetailImportDirectoryEntry>.Failed(CapabilityFailure());
			try
			{
				var lease = recovery.BorrowLease();
				faultInjection.Hit(RetailImportFailpoint.QuarantineRecoveryAuthenticated);
				return storage.Quarantine(lease, entry, Guid.NewGuid(), faultInjection);
			}
			catch (ObjectDisposedException)
			{
				return RetailOperationResult<RetailImportDirectoryEntry>.Failed(CapabilityFailure());
			}
		}

		internal RetailOperationResult PurgeRecoveredQuarantine(
			RetailRecoverySession recovery, RetailImportDirectoryEntry quarantined)
		{
			if (recovery == null || quarantined == null)
				return RetailOperationResult.Failed(CapabilityFailure());
			try { return storage.Purge(recovery.BorrowLease(), quarantined, faultInjection); }
			catch (ObjectDisposedException) { return RetailOperationResult.Failed(CapabilityFailure()); }
		}

		internal RetailOperationResult RetireLostAdmissionForRecovery(
			RetailRecoverySession recovery, RetailImportDirectoryEntry nonactiveGeneration)
		{
			var quarantined = QuarantineForRecovery(recovery, nonactiveGeneration);
			if (!quarantined.Succeeded)
				return RetailOperationResult.Failed(quarantined.Failure);
			var purged = PurgeRecoveredQuarantine(recovery, quarantined.Value);
			if (purged.Succeeded)
				faultInjection.Hit(RetailImportFailpoint.LostAdmissionRetirementCommitted);
			return purged;
		}

		internal RetailOperationResult CollectUnpinnedGenerationForRecovery(
			RetailRecoverySession recovery, RetailImportDirectoryEntry unpinnedGeneration)
		{
			var quarantined = QuarantineForRecovery(recovery, unpinnedGeneration);
			if (!quarantined.Succeeded)
				return RetailOperationResult.Failed(quarantined.Failure);
			// The q rename is durable before collection is observed; a crash here is
			// recovered through the same reauthorization/purge path.
			faultInjection.Hit(RetailImportFailpoint.GarbageCollectionStarted);
			return PurgeRecoveredQuarantine(recovery, quarantined.Value);
		}
		// Recovery is deliberately the only way to obtain an outer mutation lease.
		// Callers receive neither a root nor a raw path, and disposal returns the
		// process lock even when journal decoding subsequently fails.
		internal RetailOperationResult<RetailRecoverySession> BeginRecovery()
		{
			try
			{
				RequireLiveStore();
				using var acquired = processLock.TryAcquire();
				if (acquired.Disposition != RetailImportProcessLockAcquireDisposition.Acquired)
					return RetailOperationResult<RetailRecoverySession>.Failed(acquired.Failure ?? CapabilityFailure());
				return RetailOperationResult<RetailRecoverySession>.Success(new RetailRecoverySession(this, acquired.TakeLease()));
			}
			catch (ObjectDisposedException) { return RetailOperationResult<RetailRecoverySession>.Failed(CapabilityFailure()); }
		}

		internal RetailOperationResult<ActiveGenerationPointer> ReadActivePointer(RetailRecoverySession recovery)
		{
			if (recovery == null) throw new ArgumentNullException(nameof(recovery));
			return ReadDocument(recovery.BorrowLease(), RetailImportPaths.ActivePointerPath(), RetailImportMetadataCodec.DecodeActiveGenerationPointer);
		}

		internal RetailOperationResult<RetailPublishJournal> ReadPublishJournal(RetailRecoverySession recovery)
		{
			if (recovery == null) throw new ArgumentNullException(nameof(recovery));
			return ReadDocument(recovery.BorrowLease(), RetailImportPaths.PublishJournalPath(), RetailImportMetadataCodec.DecodeRetailPublishJournal);
		}

		public async Task<RetailActiveOpenResult> OpenActiveAsync(Func<RetailActiveGenerationSnapshot, IProgress<RetailImportProgress>, CancellationToken, Task<RetailGenerationValidationResult>> validator, IProgress<RetailImportProgress> progress, CancellationToken cancellationToken)
		{
			if (validator == null) throw new ArgumentNullException(nameof(validator));
			if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
			var begun = BeginRecovery();
			if (!begun.Succeeded) return RetailActiveOpenResult.Inconclusive(begun.Failure, null);
			using var recovery = begun.Value;
			var pointer = ReadActivePointer(recovery);
			if (!pointer.Succeeded) return RetailActiveOpenResult.Inconclusive(pointer.Failure, null);
			var catalog = ReadGenerationDocument(recovery, pointer.Value, RetailImportPaths.GenerationCatalogPath(pointer.Value.GenerationId, pointer.Value.Epoch), pointer.Value.CatalogSha256, RetailImportMetadataCodec.DecodeRetailGenerationCatalog);
			if (!catalog.Succeeded) return RetailActiveOpenResult.Inconclusive(catalog.Failure, null);
			var receipt = ReadGenerationDocument(recovery, pointer.Value, RetailImportPaths.GenerationReceiptsPath(pointer.Value.GenerationId, pointer.Value.Epoch), pointer.Value.ReceiptsSha256, RetailImportMetadataCodec.DecodeRetailGenerationReceipt);
			if (!receipt.Succeeded) return RetailActiveOpenResult.Inconclusive(receipt.Failure, null);
			var pointerReceipt = RetailMetadataValidation.ValidatePointerReceipt(pointer.Value, receipt.Value);
			var catalogReceipt = RetailMetadataValidation.ValidateCatalogReceipt(catalog.Value, receipt.Value);
			if (!pointerReceipt.Succeeded || !catalogReceipt.Succeeded) return RetailActiveOpenResult.Inconclusive(pointerReceipt.Succeeded ? catalogReceipt.Failure : pointerReceipt.Failure, null);
			var lease = recovery.TakeLease();
			var pin = storage.PinGeneration(lease, pointer.Value.GenerationId, pointer.Value.Epoch);
			if (!pin.Succeeded) { lease.Dispose(); return RetailActiveOpenResult.Inconclusive(pin.Failure, null); }
			pin.Value.TakeLeaseOwnership();
			var snapshot = RetailActiveGenerationSnapshot.CreateForStore(this, pointer.Value, receipt.Value, catalog.Value, new RetailGenerationRootCapability(pin.Value), new RetailGenerationValidationIdentity());
			try
			{
				var validation = await validator(snapshot, progress, cancellationToken).ConfigureAwait(false);
				if (validation == null || !validation.MatchesCandidate(snapshot)) { snapshot.Dispose(); return RetailActiveOpenResult.Inconclusive(CapabilityFailure(), null); }
				return validation.Disposition switch
				{
					RetailGenerationValidationDisposition.Ready => RetailActiveOpenResult.ReadyResult(new RetailReadyGenerationCapability(pointer.Value, receipt.Value, snapshot.ValidationIdentity), validation.Readiness, snapshot),
					RetailGenerationValidationDisposition.RepairRequired => RetailActiveOpenResult.RepairRequired(validation.Readiness, validation.Repair, snapshot),
					RetailGenerationValidationDisposition.Invalid => RetailActiveOpenResult.Invalid(validation.Failure, snapshot),
					_ => RetailActiveOpenResult.Inconclusive(validation.Failure, snapshot),
				};
			}
			catch { snapshot.Dispose(); throw; }
		}

		RetailOperationResult<T> ReadGenerationDocument<T>(RetailRecoverySession recovery, ActiveGenerationPointer pointer, RetailImportPaths.V1PathToken path, string expectedSha256, Func<byte[], RetailOperationResult<T>> decode) where T : class
		{
			var opened = storage.OpenPinnedRead(recovery.BorrowLease(), path);
			if (!opened.Succeeded) return RetailOperationResult<T>.Failed(opened.Failure);
			using var pinned = opened.Value;
			try
			{
				using var bytes = new MemoryStream();
				using var stream = pinned.OpenRead(); stream.CopyTo(bytes);
				if (!pinned.VerifyUnchanged().Succeeded || !StringComparer.Ordinal.Equals(Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant(), expectedSha256)) return RetailOperationResult<T>.Failed(CapabilityFailure());
				return decode(bytes.ToArray());
			}
			catch (IOException) { return RetailOperationResult<T>.Failed(RetailImportDurableFileSystem.TypedIoFailure()); }
			catch (UnauthorizedAccessException) { return RetailOperationResult<T>.Failed(RetailImportDurableFileSystem.TypedIoFailure()); }
		}

		RetailOperationResult<T> ReadDocument<T>(RetailImportProcessLockLease lease, RetailImportPaths.V1PathToken path, Func<byte[], RetailOperationResult<T>> decode) where T : class
		{
			var opened = storage.OpenPinnedRead(lease, path);
			if (!opened.Succeeded) return RetailOperationResult<T>.Failed(opened.Failure);
			using var pinned = opened.Value;
			try
			{
				using var bytes = new MemoryStream();
				using var stream = pinned.OpenRead(); stream.CopyTo(bytes);
				var unchanged = pinned.VerifyUnchanged();
				if (!unchanged.Succeeded) return RetailOperationResult<T>.Failed(unchanged.Failure);
				return decode(bytes.ToArray());
			}
			catch (IOException) { return RetailOperationResult<T>.Failed(RetailImportDurableFileSystem.TypedIoFailure()); }
			catch (UnauthorizedAccessException) { return RetailOperationResult<T>.Failed(RetailImportDurableFileSystem.TypedIoFailure()); }
		}

		internal static RetailImportFailure CapabilityFailure() => new(RetailImportErrorCode.RecoveryFailed, null, null, RetailImportDiagnosticCode.GenerationInvalid);
	}
}
