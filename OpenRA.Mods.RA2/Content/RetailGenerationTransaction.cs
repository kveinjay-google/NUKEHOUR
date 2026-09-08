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
using System.Threading;
using System.Threading.Tasks;

namespace OpenRA.Mods.RA2.Content
{
	public sealed class RetailRecoveryResult
	{
		internal RetailRecoveryResult(RetailRecoveryDisposition disposition, RetailImportFailure failure)
		{
			Disposition = disposition;
			Failure = failure;
		}
		public RetailRecoveryDisposition Disposition { get; }
		public RetailImportFailure Failure { get; }
		internal static RetailRecoveryResult Result(RetailRecoveryDisposition disposition, RetailImportFailure failure = null) => new(disposition, failure);
	}

	public sealed partial class RetailGenerationStore
	{
		// The prepared object is an unforgeable, single-use capability.  Its durable
		// counterpart is the journal; this in-memory binding prevents a caller from
		// publishing somebody else's generation after losing the lock acquisition.
		internal sealed class PreparedGenerationData
		{
			internal PreparedGenerationData(ActiveGenerationPointer previous,
				ActiveGenerationPointer proposed, RetailGenerationCatalog catalog,
				RetailGenerationReceipt receipt, RetailPublishJournal journal)
			{
				Previous = previous;
				Proposed = proposed;
				Catalog = catalog;
				Receipt = receipt;
				Journal = journal;
			}
			internal ActiveGenerationPointer Previous { get; }
			internal ActiveGenerationPointer Proposed { get; }
			internal RetailGenerationCatalog Catalog { get; }
			internal RetailGenerationReceipt Receipt { get; }
			internal RetailPublishJournal Journal { get; }
		}

		public async Task<RetailOperationResult<PreparedRetailGeneration>> PrepareAsync(
			RetailCandidateCatalog candidate, RetailCandidateArtifactSource artifactSource,
			RetailActiveGenerationSnapshot active,
			Func<RetailActiveGenerationSnapshot, Task<RetailGenerationValidationResult>> preJournalValidator,
			CancellationToken cancellationToken,
			IProgress<RetailImportProgress> progress)
		{
			if (candidate == null || artifactSource == null || preJournalValidator == null)
				throw new ArgumentNullException(candidate == null ? nameof(candidate) : artifactSource == null ? nameof(artifactSource) : nameof(preJournalValidator));
			if (cancellationToken.IsCancellationRequested)
				throw new OperationCanceledException(cancellationToken);

			using var acquired = processLock.TryAcquire();
			if (acquired.Disposition != RetailImportProcessLockAcquireDisposition.Acquired)
				return RetailOperationResult<PreparedRetailGeneration>.Failed(acquired.Failure ?? CapabilityFailure());
			using var lease = acquired.TakeLease();
			try
			{
				var previous = active?.Pointer;
				var epoch = previous == null ? 0 : checked(previous.Epoch + 1);
				var generationId = RetailImportPaths.NewGenerationId(epoch);
				var commitId = "c-" + Guid.NewGuid().ToString("N");
				var catalog = BuildCatalog(candidate);
				var receipt = BuildReceipt(candidate, epoch, commitId, artifactSource.SourcePendingId);
				var generationsDirectory = storage.EnsureDirectory(lease,
					RetailImportPaths.GenerationsDirectoryPath(), cancellationToken);
				if (!generationsDirectory.Succeeded) return RetailOperationResult<PreparedRetailGeneration>.Failed(generationsDirectory.Failure);
				var generationDirectory = storage.EnsureDirectory(lease,
					RetailImportPaths.GenerationDirectoryPath(generationId, epoch), cancellationToken);
				if (!generationDirectory.Succeeded) return RetailOperationResult<PreparedRetailGeneration>.Failed(generationDirectory.Failure);
				faultInjection.Hit(RetailImportFailpoint.GenerationDirectoryCreated);
				var contentDirectory = storage.EnsureDirectory(lease,
					RetailImportPaths.GenerationContentDirectoryPath(generationId, epoch), cancellationToken);
				if (!contentDirectory.Succeeded) return RetailOperationResult<PreparedRetailGeneration>.Failed(contentDirectory.Failure);

				var deadline = RetailValidationDeadline.Start(clock, TimeSpan.FromMinutes(90));
				var preserved = active == null ? ImmutableArray<RetailCatalogArtifact>.Empty :
					active.Catalog.Artifacts.Where(existing => catalog.Artifacts.Any(next =>
						next.Group == existing.Group &&
						StringComparer.Ordinal.Equals(next.LogicalName, existing.LogicalName) &&
						next.Length == existing.Length &&
						StringComparer.Ordinal.Equals(next.Sha256, existing.Sha256))).ToImmutableArray();
				foreach (var existing in preserved)
				{
					using var stream = active.OpenVerifiedRead(existing);
					var destination = RetailImportPaths.GenerationArtifactPath(generationId, epoch,
						existing.CanonicalGenerationRelativePath);
					var parent = storage.EnsureArtifactParentDirectory(lease, destination, cancellationToken);
					if (!parent.Succeeded) return RetailOperationResult<PreparedRetailGeneration>.Failed(parent.Failure);
					var materialized = await storage.MaterializeCreateNewAsync(lease,
						destination, null, stream, existing.Length,
						existing.Sha256, deadline, clock, cancellationToken, progress).ConfigureAwait(false);
					if (!materialized.Succeeded) return RetailOperationResult<PreparedRetailGeneration>.Failed(materialized.Failure);
				}
				faultInjection.Hit(RetailImportFailpoint.PreservedFilesCopied);
				foreach (var artifact in catalog.Artifacts)
				{
					if (preserved.Any(existing => existing.Group == artifact.Group &&
						StringComparer.Ordinal.Equals(existing.LogicalName, artifact.LogicalName) &&
						existing.Length == artifact.Length && StringComparer.Ordinal.Equals(existing.Sha256, artifact.Sha256)))
						continue;
					var source = candidate.Artifacts.Single(item => item.Group == artifact.Group &&
						item.LogicalName == artifact.LogicalName && item.Length == artifact.Length &&
						StringComparer.Ordinal.Equals(item.Sha256, artifact.Sha256));
					using var stream = artifactSource.OpenVerifiedRead(source);
					var destination = RetailImportPaths.GenerationArtifactPath(generationId, epoch,
						artifact.CanonicalGenerationRelativePath);
					var parent = storage.EnsureArtifactParentDirectory(lease, destination, cancellationToken);
					if (!parent.Succeeded) return RetailOperationResult<PreparedRetailGeneration>.Failed(parent.Failure);
					var materialized = await storage.MaterializeCreateNewAsync(lease,
						destination, null, stream, artifact.Length,
						artifact.Sha256, deadline, clock, cancellationToken, progress).ConfigureAwait(false);
					if (!materialized.Succeeded) return RetailOperationResult<PreparedRetailGeneration>.Failed(materialized.Failure);
				}
				faultInjection.Hit(RetailImportFailpoint.ReplacementFilesMoved);

				var receiptBytes = RetailImportMetadataCodec.Encode(receipt);
				var catalogBytes = RetailImportMetadataCodec.Encode(catalog);
				var writeReceipt = storage.WriteAtomic(lease, RetailImportPaths.GenerationReceiptsPath(generationId, epoch), receiptBytes, cancellationToken);
				if (!writeReceipt.Succeeded) return RetailOperationResult<PreparedRetailGeneration>.Failed(writeReceipt.Failure);
				faultInjection.Hit(RetailImportFailpoint.ReceiptsFlushed);
				var writeCatalog = storage.WriteAtomic(lease, RetailImportPaths.GenerationCatalogPath(generationId, epoch), catalogBytes, cancellationToken);
				if (!writeCatalog.Succeeded) return RetailOperationResult<PreparedRetailGeneration>.Failed(writeCatalog.Failure);
				faultInjection.Hit(RetailImportFailpoint.CatalogFlushed);

				var proposed = new ActiveGenerationPointer(RetailImportMetadataCodec.SchemaVersion, generationId, epoch, commitId,
					Digest(catalogBytes), Digest(receiptBytes));
				var verified = await ReopenAndValidatePreparedGenerationAsync(lease, proposed,
					catalog, receipt, preJournalValidator).ConfigureAwait(false);
				if (!verified.Succeeded)
					return RetailOperationResult<PreparedRetailGeneration>.Failed(verified.Failure);
				var journal = new RetailPublishJournal(RetailImportMetadataCodec.SchemaVersion, RetailPublishState.Prepared,
					epoch, previous != null, previous?.GenerationId, generationId, commitId,
					artifactSource.SourcePendingId, catalog.Groups.Select(group => group.Group).ToImmutableArray(),
					catalog.Artifacts.Select(item => item.CanonicalGenerationRelativePath).ToImmutableArray());
				var journalWrite = storage.WriteAtomic(lease, RetailImportPaths.PublishJournalPath(),
					RetailImportMetadataCodec.Encode(journal), cancellationToken);
				if (!journalWrite.Succeeded) return RetailOperationResult<PreparedRetailGeneration>.Failed(journalWrite.Failure);
				faultInjection.Hit(RetailImportFailpoint.PreparedJournalFlushed);
				return RetailOperationResult<PreparedRetailGeneration>.Success(new PreparedRetailGeneration(
					new PreparedMintToken(this, storage, new PreparedGenerationData(previous, proposed, catalog, receipt, journal))));
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or OverflowException)
			{
				return RetailOperationResult<PreparedRetailGeneration>.Failed(RetailImportDurableFileSystem.TypedIoFailure());
			}
		}

		async Task<RetailOperationResult> ReopenAndValidatePreparedGenerationAsync(
			RetailImportProcessLockLease lease, ActiveGenerationPointer proposed,
			RetailGenerationCatalog expectedCatalog, RetailGenerationReceipt expectedReceipt,
			Func<RetailActiveGenerationSnapshot, Task<RetailGenerationValidationResult>> validator)
		{
			var catalog = ReadAndHashDocument(lease,
				RetailImportPaths.GenerationCatalogPath(proposed.GenerationId, proposed.Epoch),
				proposed.CatalogSha256, RetailImportMetadataCodec.DecodeRetailGenerationCatalog);
			var receipt = ReadAndHashDocument(lease,
				RetailImportPaths.GenerationReceiptsPath(proposed.GenerationId, proposed.Epoch),
				proposed.ReceiptsSha256, RetailImportMetadataCodec.DecodeRetailGenerationReceipt);
			if (!catalog.Succeeded || !receipt.Succeeded)
				return RetailOperationResult.Failed(catalog.Succeeded ? receipt.Failure : catalog.Failure);
			if (!RetailMetadataValidation.ValidatePointerReceipt(proposed, receipt.Value).Succeeded ||
				!RetailMetadataValidation.ValidateCatalogReceipt(catalog.Value, receipt.Value).Succeeded ||
				!StringComparer.Ordinal.Equals(Digest(RetailImportMetadataCodec.Encode(catalog.Value)), Digest(RetailImportMetadataCodec.Encode(expectedCatalog))) ||
				!StringComparer.Ordinal.Equals(Digest(RetailImportMetadataCodec.Encode(receipt.Value)), Digest(RetailImportMetadataCodec.Encode(expectedReceipt))))
				return RetailOperationResult.Failed(CapabilityFailure());

			foreach (var artifact in catalog.Value.Artifacts)
			{
				var hash = ReadAndHashArtifact(lease, RetailImportPaths.GenerationArtifactPath(
					proposed.GenerationId, proposed.Epoch, artifact.CanonicalGenerationRelativePath));
				if (!hash.Succeeded || !StringComparer.Ordinal.Equals(hash.Value, artifact.Sha256))
					return RetailOperationResult.Failed(hash.Succeeded ? CapabilityFailure() : hash.Failure);
			}

			var pin = storage.PinGeneration(lease, proposed.GenerationId, proposed.Epoch);
			if (!pin.Succeeded) return RetailOperationResult.Failed(pin.Failure);
			var snapshot = RetailActiveGenerationSnapshot.CreateForStore(this, proposed, receipt.Value,
				catalog.Value, new RetailGenerationRootCapability(pin.Value), new RetailGenerationValidationIdentity());
			try
			{
				var validation = await validator(snapshot).ConfigureAwait(false);
				if (validation == null || !validation.MatchesCandidate(snapshot) ||
					validation.Disposition != RetailGenerationValidationDisposition.Ready)
					return RetailOperationResult.Failed(validation?.Failure ?? CapabilityFailure());
				RetailMetadataValidation.ValidateReadinessMatchesCatalog(catalog.Value.Profile,
					catalog.Value.Groups, catalog.Value.Maps, validation.Readiness);
				return RetailOperationResult.Success();
			}
			catch (ArgumentException) { return RetailOperationResult.Failed(CapabilityFailure()); }
			catch (IOException) { return RetailOperationResult.Failed(RetailImportDurableFileSystem.TypedIoFailure()); }
			catch (UnauthorizedAccessException) { return RetailOperationResult.Failed(RetailImportDurableFileSystem.TypedIoFailure()); }
			finally { snapshot.Dispose(); }
		}

		RetailOperationResult<T> ReadAndHashDocument<T>(RetailImportProcessLockLease lease,
			RetailImportPaths.V1PathToken path, string expectedHash,
			Func<byte[], RetailOperationResult<T>> decode) where T : class
		{
			var opened = storage.OpenPinnedRead(lease, path);
			if (!opened.Succeeded) return RetailOperationResult<T>.Failed(opened.Failure);
			using var pinned = opened.Value;
			try
			{
				using var bytes = new MemoryStream();
				using var stream = pinned.OpenRead();
				stream.CopyTo(bytes);
				if (!pinned.VerifyUnchanged().Succeeded || !StringComparer.Ordinal.Equals(Digest(bytes.ToArray()), expectedHash))
					return RetailOperationResult<T>.Failed(CapabilityFailure());
				return decode(bytes.ToArray());
			}
			catch (IOException) { return RetailOperationResult<T>.Failed(RetailImportDurableFileSystem.TypedIoFailure()); }
			catch (UnauthorizedAccessException) { return RetailOperationResult<T>.Failed(RetailImportDurableFileSystem.TypedIoFailure()); }
		}

		RetailOperationResult<string> ReadAndHashArtifact(RetailImportProcessLockLease lease,
			RetailImportPaths.V1PathToken path)
		{
			var opened = storage.OpenPinnedRead(lease, path);
			if (!opened.Succeeded) return RetailOperationResult<string>.Failed(opened.Failure);
			using var pinned = opened.Value;
			try
			{
				using var stream = pinned.OpenRead();
				using var hash = SHA256.Create();
				var digest = Convert.ToHexString(hash.ComputeHash(stream)).ToLowerInvariant();
				return pinned.VerifyUnchanged().Succeeded ? RetailOperationResult<string>.Success(digest) : RetailOperationResult<string>.Failed(CapabilityFailure());
			}
			catch (IOException) { return RetailOperationResult<string>.Failed(RetailImportDurableFileSystem.TypedIoFailure()); }
			catch (UnauthorizedAccessException) { return RetailOperationResult<string>.Failed(RetailImportDurableFileSystem.TypedIoFailure()); }
		}

		public async Task<RetailPublishResult> PublishAsync(PreparedRetailGeneration prepared,
			Func<RetailActiveGenerationSnapshot, Task<RetailGenerationValidationResult>> postSwitchValidator)
		{
			if (prepared == null || postSwitchValidator == null) throw new ArgumentNullException(prepared == null ? nameof(prepared) : nameof(postSwitchValidator));
			var data = prepared.Data;
			var consumed = prepared.TryConsumeFor(this, storage);
			if (!consumed.Succeeded) return RetailPublishResult.Failed(consumed.Failure);
			using var session = consumed.Value;
			using var acquired = processLock.TryAcquire();
			if (acquired.Disposition != RetailImportProcessLockAcquireDisposition.Acquired) return RetailPublishResult.Failed(acquired.Failure ?? CapabilityFailure());
			var lease = acquired.TakeLease();
			var leaseTransferred = false;
			try
			{
			var publishing = new RetailPublishJournal(RetailImportMetadataCodec.SchemaVersion, RetailPublishState.Publishing,
				data.Journal.Epoch, data.Journal.HasPreviousGeneration, data.Journal.PreviousGenerationId,
				data.Journal.ProposedGenerationId, data.Journal.CommitId, data.Journal.SourcePendingId,
				data.Journal.AffectedGroups, data.Journal.AffectedRelativePaths);
			var writeJournal = storage.WriteAtomic(lease, RetailImportPaths.PublishJournalPath(), RetailImportMetadataCodec.Encode(publishing), CancellationToken.None);
			if (!writeJournal.Succeeded) return RetailPublishResult.Failed(writeJournal.Failure);
			faultInjection.Hit(RetailImportFailpoint.PublishingJournalFlushed);
			var switchPointer = storage.WriteAtomic(lease, RetailImportPaths.ActivePointerPath(), RetailImportMetadataCodec.Encode(data.Proposed), CancellationToken.None);
			if (!switchPointer.Succeeded) return RetailPublishResult.Failed(switchPointer.Failure);
			faultInjection.Hit(RetailImportFailpoint.ActivePointerReplaced);
			var pin = storage.PinGeneration(lease, data.Proposed.GenerationId, data.Proposed.Epoch);
			if (!pin.Succeeded) return RollBack(data, lease, pin.Failure);
			var snapshot = RetailActiveGenerationSnapshot.CreateForStore(this, data.Proposed, data.Receipt, data.Catalog,
				new RetailGenerationRootCapability(pin.Value), new RetailGenerationValidationIdentity());
			try
			{
				faultInjection.Hit(RetailImportFailpoint.PostCommitValidationStarted);
				var validation = await postSwitchValidator(snapshot).ConfigureAwait(false);
				if (validation == null || !validation.MatchesCandidate(snapshot) || validation.Disposition != RetailGenerationValidationDisposition.Ready)
				{
					snapshot.Dispose(); return RollBack(data, lease, validation?.Failure ?? CapabilityFailure());
				}
				var committed = new RetailPublishJournal(RetailImportMetadataCodec.SchemaVersion, RetailPublishState.Committed,
					publishing.Epoch, publishing.HasPreviousGeneration, publishing.PreviousGenerationId, publishing.ProposedGenerationId,
					publishing.CommitId, publishing.SourcePendingId, publishing.AffectedGroups, publishing.AffectedRelativePaths);
				var writeCommitted = storage.WriteAtomic(lease, RetailImportPaths.PublishJournalPath(), RetailImportMetadataCodec.Encode(committed), CancellationToken.None);
				if (!writeCommitted.Succeeded) { snapshot.Dispose(); return RetailPublishResult.Failed(writeCommitted.Failure); }
				faultInjection.Hit(RetailImportFailpoint.CommittedJournalFlushed);
				pin.Value.TakeLeaseOwnership();
				leaseTransferred = true;
				return RetailPublishResult.Published(new RetailReadyGenerationCapability(data.Proposed, data.Receipt, snapshot.ValidationIdentity), validation.Readiness, snapshot);
			}
			catch { snapshot.Dispose(); throw; }
			}
			finally
			{
				if (!leaseTransferred)
					lease.Dispose();
			}
		}

		RetailPublishResult RollBack(PreparedGenerationData data, RetailImportProcessLockLease lease, RetailImportFailure failure)
		{
			if (data.Previous == null) return RetailPublishResult.RolledBack(failure);
			var restore = storage.WriteAtomic(lease, RetailImportPaths.ActivePointerPath(), RetailImportMetadataCodec.Encode(data.Previous), CancellationToken.None);
			return restore.Succeeded ? RetailPublishResult.RolledBack(failure) : RetailPublishResult.Failed(restore.Failure);
		}

		// Recovery never accepts a path from the journal.  The only mutations are
		// pointer documents minted from already decoded, authenticated identities.
		public async Task<RetailRecoveryResult> RecoverAsync(
			Func<RetailActiveGenerationSnapshot, Task<RetailGenerationValidationResult>> validator)
		{
			if (validator == null) throw new ArgumentNullException(nameof(validator));
			var begun = BeginRecovery();
			if (!begun.Succeeded) return RetailRecoveryResult.Result(RetailRecoveryDisposition.RecoveryFailed, begun.Failure);
			using var recovery = begun.Value;
			var journal = ReadPublishJournal(recovery);
			if (!journal.Succeeded)
				return RetailRecoveryResult.Result(journal.Failure.Code == RetailImportErrorCode.UpdateRequired ? RetailRecoveryDisposition.UpdateRequired : RetailRecoveryDisposition.ResetRequired, journal.Failure);
			var pointer = ReadActivePointer(recovery);
			if (!pointer.Succeeded)
				return RetailRecoveryResult.Result(RetailRecoveryDisposition.ResetRequired, pointer.Failure);
			var current = pointer.Value;
			var isProposed = SamePointer(current, journal.Value.ProposedGenerationId, journal.Value.Epoch, journal.Value.CommitId);
			var isPrevious = journal.Value.HasPreviousGeneration && StringComparer.Ordinal.Equals(current.GenerationId, journal.Value.PreviousGenerationId);
			if (journal.Value.State == RetailPublishState.Prepared)
				return isPrevious ? RetailRecoveryResult.Result(RetailRecoveryDisposition.KeptPrevious) : RetailRecoveryResult.Result(RetailRecoveryDisposition.ResetRequired, CapabilityFailure());
			if (isProposed)
				return await ValidateAndConvergeProposedAsync(recovery, journal.Value, current, true, validator).ConfigureAwait(false);
			if (journal.Value.State == RetailPublishState.Publishing && isPrevious)
				return RetailRecoveryResult.Result(RetailRecoveryDisposition.KeptPrevious);
			if (journal.Value.State == RetailPublishState.Committed && isPrevious)
			{
				var proposed = ReadPointerForJournal(recovery, journal.Value);
				if (!proposed.Succeeded) return RetailRecoveryResult.Result(RetailRecoveryDisposition.ResetRequired, proposed.Failure);
				return await ValidateAndConvergeProposedAsync(recovery, journal.Value, proposed.Value, false, validator).ConfigureAwait(false);
			}
			return RetailRecoveryResult.Result(RetailRecoveryDisposition.ResetRequired, CapabilityFailure());
		}

		async Task<RetailRecoveryResult> ValidateAndConvergeProposedAsync(RetailRecoverySession recovery,
			RetailPublishJournal journal, ActiveGenerationPointer proposed, bool proposedIsActive,
			Func<RetailActiveGenerationSnapshot, Task<RetailGenerationValidationResult>> validator)
		{
			var catalog = ReadGenerationDocument(recovery, proposed,
				RetailImportPaths.GenerationCatalogPath(proposed.GenerationId, proposed.Epoch),
				proposed.CatalogSha256, RetailImportMetadataCodec.DecodeRetailGenerationCatalog);
			var receipt = ReadGenerationDocument(recovery, proposed,
				RetailImportPaths.GenerationReceiptsPath(proposed.GenerationId, proposed.Epoch),
				proposed.ReceiptsSha256, RetailImportMetadataCodec.DecodeRetailGenerationReceipt);
			if (!catalog.Succeeded || !receipt.Succeeded)
				return RetailRecoveryResult.Result(RetailRecoveryDisposition.RecoveryFailed,
					catalog.Succeeded ? receipt.Failure : catalog.Failure);
			var pointerReceipt = RetailMetadataValidation.ValidatePointerReceipt(proposed, receipt.Value);
			var catalogReceipt = RetailMetadataValidation.ValidateCatalogReceipt(catalog.Value, receipt.Value);
			var journalReceipt = RetailMetadataValidation.ValidateJournalReceipt(journal, receipt.Value);
			if (!pointerReceipt.Succeeded || !catalogReceipt.Succeeded || !journalReceipt.Succeeded)
				return RetailRecoveryResult.Result(RetailRecoveryDisposition.RecoveryFailed,
					!pointerReceipt.Succeeded ? pointerReceipt.Failure : !catalogReceipt.Succeeded ? catalogReceipt.Failure : journalReceipt.Failure);

			var lease = recovery.BorrowLease();
			var pin = storage.PinGeneration(lease, proposed.GenerationId, proposed.Epoch);
			if (!pin.Succeeded)
				return RetailRecoveryResult.Result(RetailRecoveryDisposition.RecoveryFailed, pin.Failure);
			var snapshot = RetailActiveGenerationSnapshot.CreateForStore(this, proposed, receipt.Value,
				catalog.Value, new RetailGenerationRootCapability(pin.Value), new RetailGenerationValidationIdentity());
			RetailGenerationValidationResult validation;
			try { validation = await validator(snapshot).ConfigureAwait(false); }
			catch
			{
				snapshot.Dispose();
				return RetailRecoveryResult.Result(RetailRecoveryDisposition.RecoveryFailed, CapabilityFailure());
			}
			if (validation == null || !validation.MatchesCandidate(snapshot))
			{
				snapshot.Dispose();
				return RetailRecoveryResult.Result(RetailRecoveryDisposition.RecoveryFailed, CapabilityFailure());
			}
			if (validation.Disposition == RetailGenerationValidationDisposition.Ready)
			{
				snapshot.Dispose();
				if (!proposedIsActive)
				{
					var written = storage.WriteAtomic(lease, RetailImportPaths.ActivePointerPath(), RetailImportMetadataCodec.Encode(proposed), CancellationToken.None);
					if (!written.Succeeded) return RetailRecoveryResult.Result(RetailRecoveryDisposition.RecoveryFailed, written.Failure);
				}
				return RetailRecoveryResult.Result(RetailRecoveryDisposition.ConfirmedProposed);
			}

			snapshot.Dispose();
			// A non-Ready proposal is never left active.  A transient Inconclusive
			// result intentionally does not mutate durable state; an explicit invalid
			// or repair result rolls back through the typed active-pointer operation.
			if (validation.Disposition == RetailGenerationValidationDisposition.Inconclusive)
				return RetailRecoveryResult.Result(RetailRecoveryDisposition.RecoveryFailed, validation.Failure);
			if (!proposedIsActive)
				return RetailRecoveryResult.Result(RetailRecoveryDisposition.KeptPrevious, validation.Failure);
			return RestorePreviousAfterRejectedProposal(recovery, journal, validation.Failure);
		}

		RetailRecoveryResult RestorePreviousAfterRejectedProposal(RetailRecoverySession recovery,
			RetailPublishJournal journal, RetailImportFailure failure)
		{
			if (!journal.HasPreviousGeneration || journal.Epoch == 0)
				return RetailRecoveryResult.Result(RetailRecoveryDisposition.ResetRequired, failure ?? CapabilityFailure());
			var previous = ReadPointerForGeneration(recovery, journal.PreviousGenerationId, journal.Epoch - 1);
			if (!previous.Succeeded)
				return RetailRecoveryResult.Result(RetailRecoveryDisposition.RecoveryFailed, previous.Failure);
			var written = storage.WriteAtomic(recovery.BorrowLease(), RetailImportPaths.ActivePointerPath(),
				RetailImportMetadataCodec.Encode(previous.Value), CancellationToken.None);
			return written.Succeeded ? RetailRecoveryResult.Result(RetailRecoveryDisposition.RestoredPrevious, failure) :
				RetailRecoveryResult.Result(RetailRecoveryDisposition.RecoveryFailed, written.Failure);
		}

		RetailOperationResult<ActiveGenerationPointer> ReadPointerForGeneration(RetailRecoverySession recovery,
			string generationId, long epoch)
		{
			var catalogPath = RetailImportPaths.GenerationCatalogPath(generationId, epoch);
			var receiptPath = RetailImportPaths.GenerationReceiptsPath(generationId, epoch);
			var openedCatalog = storage.OpenPinnedRead(recovery.BorrowLease(), catalogPath);
			var openedReceipt = storage.OpenPinnedRead(recovery.BorrowLease(), receiptPath);
			if (!openedCatalog.Succeeded || !openedReceipt.Succeeded)
				return RetailOperationResult<ActiveGenerationPointer>.Failed(!openedCatalog.Succeeded ? openedCatalog.Failure : openedReceipt.Failure);
			using var catalogFile = openedCatalog.Value;
			using var receiptFile = openedReceipt.Value;
			using var catalogBytes = new MemoryStream();
			using var receiptBytes = new MemoryStream();
			catalogFile.OpenRead().CopyTo(catalogBytes);
			receiptFile.OpenRead().CopyTo(receiptBytes);
			if (!catalogFile.VerifyUnchanged().Succeeded || !receiptFile.VerifyUnchanged().Succeeded)
				return RetailOperationResult<ActiveGenerationPointer>.Failed(CapabilityFailure());
			var catalog = RetailImportMetadataCodec.DecodeRetailGenerationCatalog(catalogBytes.ToArray());
			var receipt = RetailImportMetadataCodec.DecodeRetailGenerationReceipt(receiptBytes.ToArray());
			if (!catalog.Succeeded || !receipt.Succeeded || receipt.Value.Epoch != epoch ||
				!RetailMetadataValidation.ValidateCatalogReceipt(catalog.Value, receipt.Value).Succeeded)
				return RetailOperationResult<ActiveGenerationPointer>.Failed(CapabilityFailure());
			return RetailOperationResult<ActiveGenerationPointer>.Success(new ActiveGenerationPointer(
				RetailImportMetadataCodec.SchemaVersion, generationId, epoch, receipt.Value.CommitId,
				Digest(catalogBytes.ToArray()), Digest(receiptBytes.ToArray())));
		}

		RetailOperationResult<ActiveGenerationPointer> ReadPointerForJournal(RetailRecoverySession recovery, RetailPublishJournal journal)
		{
			var catalogPath = RetailImportPaths.GenerationCatalogPath(journal.ProposedGenerationId, journal.Epoch);
			var receiptPath = RetailImportPaths.GenerationReceiptsPath(journal.ProposedGenerationId, journal.Epoch);
			var openedCatalog = storage.OpenPinnedRead(recovery.BorrowLease(), catalogPath);
			var openedReceipt = storage.OpenPinnedRead(recovery.BorrowLease(), receiptPath);
			if (!openedCatalog.Succeeded || !openedReceipt.Succeeded) return RetailOperationResult<ActiveGenerationPointer>.Failed((!openedCatalog.Succeeded ? openedCatalog.Failure : openedReceipt.Failure));
			using var catalogFile = openedCatalog.Value;
			using var receiptFile = openedReceipt.Value;
			using var catalogBytes = new MemoryStream(); using var receiptBytes = new MemoryStream();
			catalogFile.OpenRead().CopyTo(catalogBytes); receiptFile.OpenRead().CopyTo(receiptBytes);
			var catalog = RetailImportMetadataCodec.DecodeRetailGenerationCatalog(catalogBytes.ToArray());
			var receipt = RetailImportMetadataCodec.DecodeRetailGenerationReceipt(receiptBytes.ToArray());
			if (!catalog.Succeeded || !receipt.Succeeded || !RetailMetadataValidation.ValidateJournalReceipt(journal, receipt.Value).Succeeded || !RetailMetadataValidation.ValidateCatalogReceipt(catalog.Value, receipt.Value).Succeeded)
				return RetailOperationResult<ActiveGenerationPointer>.Failed(CapabilityFailure());
			return RetailOperationResult<ActiveGenerationPointer>.Success(new ActiveGenerationPointer(RetailImportMetadataCodec.SchemaVersion, journal.ProposedGenerationId, journal.Epoch, journal.CommitId, Digest(catalogBytes.ToArray()), Digest(receiptBytes.ToArray())));
		}

		static bool SamePointer(ActiveGenerationPointer pointer, string generation, long epoch, string commit) =>
			pointer != null && pointer.Epoch == epoch && StringComparer.Ordinal.Equals(pointer.GenerationId, generation) && StringComparer.Ordinal.Equals(pointer.CommitId, commit);

		static RetailGenerationCatalog BuildCatalog(RetailCandidateCatalog candidate)
		{
			var artifacts = candidate.Artifacts.Select(item => new RetailCatalogArtifact(item.LogicalName,
				candidate.Groups.Single(group => group.Group == item.Group).Entries.Single(entry => entry.Length == item.Length && StringComparer.Ordinal.Equals(entry.Sha256, item.Sha256)).CanonicalRelativePath,
				item.Group, item.Length, item.Sha256)).ToImmutableArray();
			return new RetailGenerationCatalog(candidate.Profile, candidate.Contract, artifacts, candidate.Groups, candidate.Maps, candidate.MapResidentUsage);
		}

		static RetailGenerationReceipt BuildReceipt(RetailCandidateCatalog candidate, long epoch, string commitId, string pendingId)
		{
			var archiveVersion = candidate.Groups.First(group => group.Group != RetailContentGroup.Maps).ValidatorVersion;
			var mapVersion = candidate.Groups.Single(group => group.Group == RetailContentGroup.Maps).ValidatorVersion;
			return new RetailGenerationReceipt(epoch, commitId, pendingId, candidate.Profile, candidate.Contract,
				candidate.Contract.FilenameCatalogDigest, archiveVersion, mapVersion, candidate.Groups, candidate.Maps, candidate.MapResidentUsage);
		}
		static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
	}
}
