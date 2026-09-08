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

namespace OpenRA.Mods.RA2.Content
{
	public enum RetailImportFailpoint
	{
		GenerationDirectoryCreated,
		PreservedFilesCopied,
		ReplacementFilesMoved,
		ReceiptsFlushed,
		CatalogFlushed,
		PreparedJournalFlushed,
		PublishingJournalFlushed,
		ActivePointerReplaced,
		CommittedJournalFlushed,
		PostCommitValidationStarted,
		QuarantineEntryRenamed,
		QuarantineRecoveryAuthenticated,
		QuarantinePurgeStarted,
		LostAdmissionRetirementCommitted,
		GarbageCollectionStarted,
		ResetPreparedMarkerFlushed,
		ResetRootRenameFlushed,
		ResetRenamedMarkerFlushed,
		ResetQuarantinePurgeStarted,
		ResetInnerLockUnlinked,
		ResetTransactionMarkerCleared,
	}

	public enum RetailPendingFailpoint
	{
		StagingArtifactFlushed,
		StagingSealFlushed,
		CompleteStagingAuthenticated,
		StagingRenamedToSealed,
		StagingParentDirectoryFlushed,
		ReadyCapabilityCreated,
		PreparedMarkerFlushed,
		PendingRenamedToTombstone,
		PendingTombstoneParentDirectoryFlushed,
		TombstoneDeleted,
		ConsumedMarkerFlushed,
		ConsumptionMarkerAuthenticated,
		ConsumptionMarkerCleared,
		CleanupAuthorizationAuthenticated,
		CleanupPendingRenamedToTombstone,
		CleanupParentDirectoryFlushed,
		CleanupTombstoneRecoveryAuthenticated,
		CleanupTombstoneDeleted,
	}

	sealed class RetailImportFaultInjection
	{
		readonly Action<RetailImportFailpoint> callback;

		internal static RetailImportFaultInjection Disabled { get; } =
			new RetailImportFaultInjection(null);

		internal RetailImportFaultInjection(Action<RetailImportFailpoint> callback) =>
			this.callback = callback;

		internal void Hit(RetailImportFailpoint failpoint)
		{
			if (!Enum.IsDefined(typeof(RetailImportFailpoint), failpoint))
				throw new ArgumentOutOfRangeException(nameof(failpoint));

			callback?.Invoke(failpoint);
		}
	}

	sealed class RetailPendingFaultInjection
	{
		readonly Action<RetailPendingFailpoint> callback;

		internal static RetailPendingFaultInjection Disabled { get; } =
			new RetailPendingFaultInjection(null);

		internal RetailPendingFaultInjection(Action<RetailPendingFailpoint> callback) =>
			this.callback = callback;

		internal void Hit(RetailPendingFailpoint failpoint)
		{
			if (!Enum.IsDefined(typeof(RetailPendingFailpoint), failpoint))
				throw new ArgumentOutOfRangeException(nameof(failpoint));

			callback?.Invoke(failpoint);
		}
	}
}
