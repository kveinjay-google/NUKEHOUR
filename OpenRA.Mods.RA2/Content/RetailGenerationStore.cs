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
	public enum RetailPublishDisposition
	{
		Published,
		RolledBack,
		Failed,
	}

	public enum RetailGenerationValidationDisposition
	{
		Ready,
		RepairRequired,
		Invalid,
		Inconclusive,
	}

	public enum RetailActiveOpenDisposition
	{
		Missing,
		Ready,
		RepairRequired,
		Invalid,
		Inconclusive,
	}

	internal sealed class RetailGenerationValidationIdentity
	{
	}

	internal sealed class RetailGenerationRootCapability
	{
		IRetailCatalogArtifactSource source;

		internal RetailGenerationRootCapability(IRetailCatalogArtifactSource source) =>
			this.source = source ?? throw new ArgumentNullException(nameof(source));

		internal IRetailCatalogArtifactSource TakeSource()
		{
			var taken = Interlocked.Exchange(ref source, null);
			return taken ?? throw new InvalidOperationException("The generation root capability was already consumed.");
		}
	}

	public sealed class RetailActiveGenerationSnapshot : IRetailCatalogArtifactSource, IDisposable
	{
		IRetailCatalogArtifactSource source;

		public ActiveGenerationPointer Pointer { get; }
		public RetailGenerationReceipt Receipt { get; }
		public RetailGenerationCatalog Catalog { get; }
		internal RetailGenerationValidationIdentity ValidationIdentity { get; }

		RetailActiveGenerationSnapshot(ActiveGenerationPointer pointer,
			RetailGenerationReceipt receipt, RetailGenerationCatalog catalog,
			RetailGenerationRootCapability rootCapability,
			RetailGenerationValidationIdentity validationIdentity)
		{
			Pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
			Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
			Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
			if (Pointer.Epoch != Receipt.Epoch ||
				!StringComparer.Ordinal.Equals(Pointer.CommitId, Receipt.CommitId))
				throw new ArgumentException("The active pointer and receipt do not describe the same generation.");

			source = (rootCapability ?? throw new ArgumentNullException(nameof(rootCapability))).TakeSource();
			ValidationIdentity = validationIdentity ?? throw new ArgumentNullException(nameof(validationIdentity));
		}

		public Stream OpenVerifiedRead(RetailCatalogArtifact artifact)
		{
			var current = Volatile.Read(ref source);
			if (current == null)
				throw new ObjectDisposedException(nameof(RetailActiveGenerationSnapshot));
			if (artifact == null || !Catalog.Artifacts.Any(item => ReferenceEquals(item, artifact)))
				throw new ArgumentException("The artifact is not a member of this immutable generation.", nameof(artifact));
			return current.OpenVerifiedRead(artifact);
		}

		internal static RetailActiveGenerationSnapshot CreateForStore(
			RetailGenerationStore owner, ActiveGenerationPointer pointer,
			RetailGenerationReceipt receipt, RetailGenerationCatalog catalog,
			RetailGenerationRootCapability rootCapability,
			RetailGenerationValidationIdentity validationIdentity)
		{
			if (owner == null)
				throw new ArgumentNullException(nameof(owner));
			owner.RequireLiveStore();
			return new RetailActiveGenerationSnapshot(pointer, receipt, catalog, rootCapability,
				validationIdentity);
		}

		public void Dispose() => Interlocked.Exchange(ref source, null)?.Dispose();
	}

	public sealed class RetailReadyGenerationCapability
	{
		internal ActiveGenerationPointer Pointer { get; }
		internal RetailGenerationReceipt Receipt { get; }
		internal RetailGenerationValidationIdentity ValidationIdentity { get; }

		internal RetailReadyGenerationCapability(ActiveGenerationPointer pointer,
			RetailGenerationReceipt receipt, RetailGenerationValidationIdentity validationIdentity)
		{
			Pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
			Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
			ValidationIdentity = validationIdentity ?? throw new ArgumentNullException(nameof(validationIdentity));
			if (Pointer.Epoch != Receipt.Epoch ||
				!StringComparer.Ordinal.Equals(Pointer.CommitId, Receipt.CommitId))
				throw new ArgumentException("A Ready capability must bind one active generation.");
		}

		internal bool Matches(RetailActiveGenerationSnapshot snapshot) => snapshot != null &&
			ReferenceEquals(ValidationIdentity, snapshot.ValidationIdentity) &&
			StringComparer.Ordinal.Equals(Pointer.GenerationId, snapshot.Pointer.GenerationId) &&
			Pointer.Epoch == snapshot.Pointer.Epoch &&
			StringComparer.Ordinal.Equals(Pointer.CommitId, snapshot.Pointer.CommitId);
	}

	public sealed class RetailGenerationValidationResult
	{
		readonly RetailActiveGenerationSnapshot candidate;
		public RetailGenerationValidationDisposition Disposition { get; }
		public RetailReadinessResult Readiness { get; }
		public RetailCatalogRepairPlan Repair { get; }
		public RetailImportFailure Failure { get; }
		internal RetailGenerationValidationIdentity Identity { get; }

		RetailGenerationValidationResult(RetailGenerationValidationDisposition disposition,
			RetailActiveGenerationSnapshot candidate, RetailReadinessResult readiness,
			RetailCatalogRepairPlan repair, RetailImportFailure failure)
		{
			if (!Enum.IsDefined(typeof(RetailGenerationValidationDisposition), disposition))
				throw new ArgumentOutOfRangeException(nameof(disposition));
			this.candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
			Identity = candidate.ValidationIdentity;
			Disposition = disposition;
			Readiness = readiness;
			Repair = repair;
			Failure = failure;
			if (disposition == RetailGenerationValidationDisposition.Ready && (readiness == null || !readiness.BaseReady || repair != null || failure != null))
				throw new ArgumentException("Ready validation requires Base-ready readiness only.");
			if (disposition == RetailGenerationValidationDisposition.RepairRequired && (readiness == null || repair == null || failure != null))
				throw new ArgumentException("Repair-required validation requires readiness and a repair plan.");
			if (disposition is RetailGenerationValidationDisposition.Invalid or RetailGenerationValidationDisposition.Inconclusive && failure == null)
				throw new ArgumentException("Non-ready validation requires a typed failure.");
		}

		internal bool MatchesCandidate(RetailActiveGenerationSnapshot value) =>
			ReferenceEquals(candidate, value) && ReferenceEquals(Identity, value?.ValidationIdentity);

		internal static RetailGenerationValidationResult Ready(RetailActiveGenerationSnapshot snapshot,
			RetailReadinessResult readiness) => new(RetailGenerationValidationDisposition.Ready, snapshot, readiness, null, null);

		internal static RetailGenerationValidationResult RepairRequired(RetailActiveGenerationSnapshot snapshot,
			RetailReadinessResult readiness, RetailCatalogRepairPlan repair) =>
			new(RetailGenerationValidationDisposition.RepairRequired, snapshot, readiness, repair, null);

		internal static RetailGenerationValidationResult Invalid(RetailActiveGenerationSnapshot snapshot,
			RetailReadinessResult readiness, RetailImportFailure failure) =>
			new(RetailGenerationValidationDisposition.Invalid, snapshot, readiness, null, failure);

		internal static RetailGenerationValidationResult Inconclusive(RetailActiveGenerationSnapshot snapshot,
			RetailImportFailure failure) => new(RetailGenerationValidationDisposition.Inconclusive, snapshot, null, null, failure);
	}

	public sealed class PreparedRetailGeneration : IDisposable
	{
		RetailGenerationStore.PreparedMintToken token;

		internal PreparedRetailGeneration(RetailGenerationStore.PreparedMintToken token) =>
			this.token = token ?? throw new ArgumentNullException(nameof(token));

		internal RetailOperationResult<RetailGenerationStore.PreparedPublicationSession> TryConsumeFor(
			RetailGenerationStore store, RetailImportDurableFileSystem root)
		{
			var current = Volatile.Read(ref token);
			if (current == null)
				return RetailOperationResult<RetailGenerationStore.PreparedPublicationSession>.Failed(
					RetailGenerationStore.CapabilityFailure());

			var result = current.TryConsumeFor(store, root);
			if (result.Succeeded)
				Interlocked.CompareExchange(ref token, null, current);
			return result;
		}

		public void Dispose() => Interlocked.Exchange(ref token, null)?.Dispose();
		internal RetailGenerationStore.PreparedGenerationData Data =>
			Volatile.Read(ref token)?.Data ?? throw new ObjectDisposedException(nameof(PreparedRetailGeneration));
		public string GenerationId => Data.Proposed.GenerationId;
		public long Epoch => Data.Proposed.Epoch;
		public string CommitId => Data.Proposed.CommitId;
		public string SourcePendingId => Data.Receipt.SourcePendingId;
		public RetailGenerationCatalog Catalog => Data.Catalog;
		public RetailGenerationReceipt Receipt => Data.Receipt;
		public ActiveGenerationPointer PreviousPointer => Data.Previous;
	}

	public sealed class RetailPublishResult : IDisposable
	{
		RetailActiveGenerationSnapshot snapshot;
		public RetailPublishDisposition Disposition { get; }
		public RetailReadyGenerationCapability Ready { get; }
		public RetailReadinessResult Readiness { get; }
		public RetailImportFailure Failure { get; }
		public bool HasActiveSnapshot => Volatile.Read(ref snapshot) != null;

		RetailPublishResult(RetailPublishDisposition disposition, RetailReadyGenerationCapability ready,
			RetailReadinessResult readiness, RetailImportFailure failure, RetailActiveGenerationSnapshot snapshot)
		{
			Disposition = disposition;
			Ready = ready;
			Readiness = readiness;
			Failure = failure;
			this.snapshot = snapshot;
			if (disposition == RetailPublishDisposition.Published && (ready == null || readiness == null || failure != null || snapshot == null || !ready.Matches(snapshot)))
				throw new ArgumentException("A published result must own the validated active snapshot.");
			if (disposition != RetailPublishDisposition.Published && (ready != null || snapshot != null || failure == null))
				throw new ArgumentException("A non-published result must carry only its typed failure.");
		}

		public RetailActiveGenerationSnapshot TakeActiveSnapshot() =>
			Interlocked.Exchange(ref snapshot, null) ?? throw new InvalidOperationException("The result has no active snapshot.");
		public void Dispose() => Interlocked.Exchange(ref snapshot, null)?.Dispose();
		internal static RetailPublishResult Published(RetailReadyGenerationCapability ready, RetailReadinessResult readiness, RetailActiveGenerationSnapshot snapshot) => new(RetailPublishDisposition.Published, ready, readiness, null, snapshot);
		internal static RetailPublishResult RolledBack(RetailImportFailure failure) => new(RetailPublishDisposition.RolledBack, null, null, failure, null);
		internal static RetailPublishResult Failed(RetailImportFailure failure) => new(RetailPublishDisposition.Failed, null, null, failure, null);
	}

	public sealed class RetailActiveOpenResult : IDisposable
	{
		RetailActiveGenerationSnapshot snapshot;
		public RetailActiveOpenDisposition Disposition { get; }
		public RetailReadyGenerationCapability Ready { get; }
		public RetailReadinessResult Readiness { get; }
		public RetailCatalogRepairPlan Repair { get; }
		public RetailImportFailure Failure { get; }
		public bool HasActiveSnapshot => Volatile.Read(ref snapshot) != null;

		RetailActiveOpenResult(RetailActiveOpenDisposition disposition, RetailReadyGenerationCapability ready,
			RetailReadinessResult readiness, RetailCatalogRepairPlan repair, RetailImportFailure failure,
			RetailActiveGenerationSnapshot snapshot)
		{
			Disposition = disposition;
			Ready = ready;
			Readiness = readiness;
			Repair = repair;
			Failure = failure;
			this.snapshot = snapshot;
			if (disposition == RetailActiveOpenDisposition.Ready && (ready == null || readiness == null || snapshot == null || !ready.Matches(snapshot) || repair != null || failure != null))
				throw new ArgumentException("A ready open result must own its exact Ready snapshot.");
			if (disposition == RetailActiveOpenDisposition.RepairRequired && (snapshot == null || readiness == null || repair == null || ready != null || failure != null))
				throw new ArgumentException("A repair result must own the candidate snapshot and repair plan.");
			if (disposition is RetailActiveOpenDisposition.Invalid or RetailActiveOpenDisposition.Inconclusive && (snapshot == null || failure == null || ready != null || repair != null))
				throw new ArgumentException("A failed validation result must retain its candidate and typed failure.");
			if (disposition == RetailActiveOpenDisposition.Missing && (ready != null || readiness != null || repair != null || failure != null || snapshot != null))
				throw new ArgumentException("A missing result must not expose generation state.");
		}

		public RetailActiveGenerationSnapshot TakeActiveSnapshot() =>
			Interlocked.Exchange(ref snapshot, null) ?? throw new InvalidOperationException("The result has no active snapshot.");
		public void Dispose() => Interlocked.Exchange(ref snapshot, null)?.Dispose();
		internal static RetailActiveOpenResult Missing() => new(RetailActiveOpenDisposition.Missing, null, null, null, null, null);
		internal static RetailActiveOpenResult ReadyResult(RetailReadyGenerationCapability ready, RetailReadinessResult readiness, RetailActiveGenerationSnapshot snapshot) => new(RetailActiveOpenDisposition.Ready, ready, readiness, null, null, snapshot);
		internal static RetailActiveOpenResult RepairRequired(RetailReadinessResult readiness, RetailCatalogRepairPlan repair, RetailActiveGenerationSnapshot snapshot) => new(RetailActiveOpenDisposition.RepairRequired, null, readiness, repair, null, snapshot);
		internal static RetailActiveOpenResult Invalid(RetailImportFailure failure, RetailActiveGenerationSnapshot snapshot) => new(RetailActiveOpenDisposition.Invalid, null, null, null, failure, snapshot);
		internal static RetailActiveOpenResult Inconclusive(RetailImportFailure failure, RetailActiveGenerationSnapshot snapshot) => new(RetailActiveOpenDisposition.Inconclusive, null, null, null, failure, snapshot);
	}

	// A destructive reset is deliberately not a boolean supplied by a coordinator.
	// This one-shot authorization binds the store's constructor-owned root and the
	// exact current process-lock acquisition before the durable adapter may act.
	public sealed class RetailConfirmedResetAuthorization : IDisposable
	{
		RetailGenerationStore.ConfirmedResetMintToken token;

		internal RetailConfirmedResetAuthorization(RetailGenerationStore.ConfirmedResetMintToken token) =>
			this.token = token ?? throw new ArgumentNullException(nameof(token));

		internal RetailOperationResult<RetailGenerationStore.ConfirmedResetSession> TryConsumeFor(
			RetailGenerationStore expectedStore, RetailImportDurableFileSystem expectedRoot,
			RetailImportProcessLockLease expectedLease)
		{
			var current = Volatile.Read(ref token);
			if (current == null)
				return RetailOperationResult<RetailGenerationStore.ConfirmedResetSession>.Failed(
					RetailGenerationStore.CapabilityFailure());
			var result = current.TryConsumeFor(expectedStore, expectedRoot, expectedLease);
			if (result.Succeeded)
				Interlocked.CompareExchange(ref token, null, current);
			return result;
		}

		public void Dispose() => Interlocked.Exchange(ref token, null)?.Dispose();
	}

	// This is an opaque transfer capability.  It owns the exact inner acquisition
	// until the adapter consumes it, so no caller can replace it with a path or a
	// different lease between confirmation and quarantine.
	internal sealed class RetailQuarantinedRoot : IDisposable
	{
		RetailImportProcessLockLease innerLease;
		FileStream outerLease;
		Guid nonce;
		RetailImportDurableFileSystem owner;
		RetailImportFaultInjection faults;

		// The outer lease deliberately outlives the original root.  The inner lock
		// lives inside the quarantined subtree and is therefore only useful as a
		// proof that the rename began under the confirmed reset acquisition.
		internal RetailQuarantinedRoot(RetailImportDurableFileSystem owner,
			RetailImportProcessLockLease innerLease, FileStream outerLease, Guid nonce,
			RetailImportFaultInjection faults)
		{
			this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
			this.innerLease = innerLease ?? throw new ArgumentNullException(nameof(innerLease));
			this.outerLease = outerLease ?? throw new ArgumentNullException(nameof(outerLease));
			this.nonce = nonce == Guid.Empty ? throw new ArgumentOutOfRangeException(nameof(nonce)) : nonce;
			this.faults = faults ?? throw new ArgumentNullException(nameof(faults));
		}

		internal bool TryConsumeFor(RetailImportDurableFileSystem expectedOwner,
			out RetailImportProcessLockLease retainedInnerLease, out FileStream retainedOuterLease,
			out Guid retainedNonce, out RetailImportFaultInjection retainedFaults)
		{
			retainedInnerLease = null;
			retainedOuterLease = null;
			retainedNonce = Guid.Empty;
			retainedFaults = null;
			if (!ReferenceEquals(owner, expectedOwner))
				return false;
			var outer = Interlocked.Exchange(ref outerLease, null);
			var inner = Interlocked.Exchange(ref innerLease, null);
			if (outer == null || inner == null)
			{
				outer?.Dispose();
				inner?.Dispose();
				return false;
			}
			Interlocked.Exchange(ref owner, null);
			retainedOuterLease = outer;
			retainedInnerLease = inner;
			retainedNonce = nonce;
			retainedFaults = Interlocked.Exchange(ref faults, null);
			nonce = Guid.Empty;
			return retainedFaults != null;
		}

		public void Dispose()
		{
			Interlocked.Exchange(ref owner, null);
			Interlocked.Exchange(ref innerLease, null)?.Dispose();
			Interlocked.Exchange(ref outerLease, null)?.Dispose();
			Interlocked.Exchange(ref faults, null);
			nonce = Guid.Empty;
		}
	}

	public sealed class RetailRecoverySession : IDisposable
	{
		RetailGenerationStore store;
		RetailImportProcessLockLease lease;
		RetailImportOperationIdentity operationIdentity;

		internal RetailRecoverySession(RetailGenerationStore store,
			RetailImportProcessLockLease lease)
		{
			this.store = store ?? throw new ArgumentNullException(nameof(store));
			this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
			operationIdentity = lease.BorrowOperationIdentity();
		}

		internal RetailGenerationStore TakeStore() => Interlocked.Exchange(ref store, null) ?? throw new InvalidOperationException("The recovery session was already completed.");

		internal RetailImportProcessLockLease TakeLease()
		{
			var currentStore = Volatile.Read(ref store);
			var currentLease = Interlocked.Exchange(ref lease, null);
			if (currentStore == null || currentLease == null ||
				!currentLease.OwnsOperationIdentity(operationIdentity))
				throw new ObjectDisposedException(nameof(RetailRecoverySession));
			Interlocked.Exchange(ref operationIdentity, null);
			return currentLease;
		}

		// The durable adapter accepts the lease only after this recovery capability
		// proves it still owns the exact acquisition that created it.
		internal RetailImportProcessLockLease BorrowLease()
		{
			var currentStore = Volatile.Read(ref store);
			var currentLease = Volatile.Read(ref lease);
			if (currentStore == null || currentLease == null ||
				!currentLease.OwnsOperationIdentity(operationIdentity))
				throw new ObjectDisposedException(nameof(RetailRecoverySession));
			return currentLease;
		}

		public void Dispose()
		{
			Interlocked.Exchange(ref store, null);
			Interlocked.Exchange(ref operationIdentity, null);
			Interlocked.Exchange(ref lease, null)?.Dispose();
		}
	}

	public sealed partial class RetailGenerationStore : IDisposable
	{
		internal sealed class ConfirmedResetMintToken : IDisposable
		{
			readonly RetailGenerationStore store;
			readonly RetailImportDurableFileSystem root;
			readonly RetailImportProcessLockLease lease;
			readonly RetailImportOperationIdentity operationIdentity;
			readonly RetailImportFaultInjection faultInjection;
			int consumed;

			private ConfirmedResetMintToken(RetailGenerationStore store,
				RetailImportDurableFileSystem root, RetailImportProcessLockLease lease)
			{
				this.store = store;
				this.root = root;
				this.lease = lease;
				operationIdentity = lease.BorrowOperationIdentity();
				faultInjection = store.faultInjection;
			}

			internal static ConfirmedResetMintToken Issue(RetailGenerationStore store,
				RetailImportDurableFileSystem root, RetailImportProcessLockLease lease) =>
				new(store, root, lease);

			internal RetailOperationResult<ConfirmedResetSession> TryConsumeFor(
				RetailGenerationStore expectedStore, RetailImportDurableFileSystem expectedRoot,
				RetailImportProcessLockLease expectedLease)
			{
				if (!ReferenceEquals(store, expectedStore) || !ReferenceEquals(root, expectedRoot) ||
					!ReferenceEquals(lease, expectedLease) || store.disposed ||
					!lease.OwnsOperationIdentity(operationIdentity) ||
					Interlocked.CompareExchange(ref consumed, 1, 0) != 0)
					return RetailOperationResult<ConfirmedResetSession>.Failed(CapabilityFailure());
				return RetailOperationResult<ConfirmedResetSession>.Success(
					new ConfirmedResetSession(this));
			}

			internal RetailOperationResult<RetailImportProcessLockLease> TakeFor(
				RetailImportDurableFileSystem expectedRoot, out RetailImportFaultInjection retainedFaultInjection)
			{
				retainedFaultInjection = null;
				if (!ReferenceEquals(root, expectedRoot) || store.disposed ||
					!lease.OwnsOperationIdentity(operationIdentity))
					return RetailOperationResult<RetailImportProcessLockLease>.Failed(CapabilityFailure());
				retainedFaultInjection = faultInjection;
				return RetailOperationResult<RetailImportProcessLockLease>.Success(lease);
			}

			public void Dispose() => Interlocked.Exchange(ref consumed, 1);
		}

		internal sealed class ConfirmedResetSession : IDisposable
		{
			ConfirmedResetMintToken token;
			internal ConfirmedResetSession(ConfirmedResetMintToken token) => this.token = token;
			internal RetailOperationResult<RetailImportProcessLockLease> TakeFor(
				RetailImportDurableFileSystem expectedRoot, out RetailImportFaultInjection retainedFaultInjection)
			{
				retainedFaultInjection = null;
				var current = Interlocked.Exchange(ref token, null);
				return current == null ? RetailOperationResult<RetailImportProcessLockLease>.Failed(CapabilityFailure()) :
					current.TakeFor(expectedRoot, out retainedFaultInjection);
			}
			public void Dispose() => Interlocked.Exchange(ref token, null)?.Dispose();
		}
		internal sealed class StoreIdentity
		{
			private StoreIdentity() { }
			internal static StoreIdentity Issue() => new();
		}
		internal sealed class RootIdentity
		{
			private RootIdentity() { }
			internal static RootIdentity Issue() => new();
		}

		internal sealed class PreparedMintToken : IDisposable
		{
			readonly RetailGenerationStore store;
			readonly RetailImportDurableFileSystem root;
			int consumed;
			internal PreparedGenerationData Data { get; }
			internal PreparedMintToken(RetailGenerationStore store, RetailImportDurableFileSystem root,
				PreparedGenerationData data)
			{
				this.store = store ?? throw new ArgumentNullException(nameof(store));
				this.root = root ?? throw new ArgumentNullException(nameof(root));
				Data = data ?? throw new ArgumentNullException(nameof(data));
			}
			internal RetailOperationResult<PreparedPublicationSession> TryConsumeFor(RetailGenerationStore expectedStore, RetailImportDurableFileSystem expectedRoot)
			{
				if (!ReferenceEquals(store, expectedStore) || !ReferenceEquals(root, expectedRoot) ||
					store.disposed || Interlocked.CompareExchange(ref consumed, 1, 0) != 0)
					return RetailOperationResult<PreparedPublicationSession>.Failed(CapabilityFailure());
				return RetailOperationResult<PreparedPublicationSession>.Success(new PreparedPublicationSession(expectedStore));
			}
			public void Dispose() => Interlocked.Exchange(ref consumed, 1);
		}

		internal sealed class PreparedPublicationSession : IDisposable
		{
			RetailGenerationStore store;
			internal PreparedPublicationSession(RetailGenerationStore store) => this.store = store;
			public void Dispose() => Interlocked.Exchange(ref store, null);
		}

		readonly RetailImportDurableFileSystem storage;
		readonly RetailImportProcessLock processLock;
		readonly IRetailMonotonicClock clock;
		readonly RetailImportFaultInjection faultInjection;
		readonly StoreIdentity identity = StoreIdentity.Issue();
		readonly RootIdentity rootIdentity = RootIdentity.Issue();
		bool disposed;

		internal RetailGenerationStore(RetailImportDurableFileSystem ownedStorage,
			RetailImportProcessLock ownedProcessLock, IRetailMonotonicClock clock,
			RetailImportFaultInjection faultInjection, RetailPendingFaultInjection pendingFaultInjection)
		{
			storage = ownedStorage ?? throw new ArgumentNullException(nameof(ownedStorage));
			processLock = ownedProcessLock ?? throw new ArgumentNullException(nameof(ownedProcessLock));
			this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
			this.faultInjection = faultInjection ?? throw new ArgumentNullException(nameof(faultInjection));
			_ = pendingFaultInjection ?? throw new ArgumentNullException(nameof(pendingFaultInjection));
			if (!processLock.IsBoundTo(storage))
				throw new ArgumentException("The process lock must be bound to the owned storage root.", nameof(ownedProcessLock));
		}

		internal StoreIdentity Identity => !disposed ? identity : throw new ObjectDisposedException(nameof(RetailGenerationStore));
		internal RootIdentity Root => !disposed ? rootIdentity : throw new ObjectDisposedException(nameof(RetailGenerationStore));
		internal void RequireLiveStore()
		{
			if (disposed || !processLock.IsBoundTo(storage))
				throw new ObjectDisposedException(nameof(RetailGenerationStore));
		}

		internal RetailOperationResult<RetailConfirmedResetAuthorization>
			CreateConfirmedResetAuthorization(RetailImportProcessLockLease liveLease)
		{
			try
			{
				RequireLiveStore();
				if (liveLease == null || !liveLease.IsBoundTo(storage))
					return RetailOperationResult<RetailConfirmedResetAuthorization>.Failed(CapabilityFailure());
				return RetailOperationResult<RetailConfirmedResetAuthorization>.Success(
					new RetailConfirmedResetAuthorization(ConfirmedResetMintToken.Issue(this, storage, liveLease)));
			}
			catch (ObjectDisposedException)
			{
				return RetailOperationResult<RetailConfirmedResetAuthorization>.Failed(CapabilityFailure());
			}
		}

		public void Dispose()
		{
			if (disposed) return;
			disposed = true;
			processLock.Dispose();
			storage.Dispose();
		}
	}
}
