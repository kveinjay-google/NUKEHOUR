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
using System.Runtime.InteropServices;

namespace OpenRA.Mods.RA2.Content
{
	enum RetailImportProcessLockAcquireDisposition
	{
		Acquired,
		Busy,
		Failed,
	}

	sealed class RetailImportProcessLockAcquireResult : IDisposable
	{
		RetailImportProcessLockLease lease;

		internal RetailImportProcessLockAcquireResult(
			RetailImportProcessLockAcquireDisposition disposition,
			RetailImportFailure failure,
			RetailImportProcessLockLease lease)
		{
			Disposition = disposition;
			Failure = failure;
			this.lease = lease;
		}

		internal RetailImportProcessLockAcquireDisposition Disposition { get; }
		internal RetailImportFailure Failure { get; }
		internal bool HasLease => lease != null;

		internal RetailImportProcessLockLease TakeLease()
		{
			if (lease == null)
				throw new InvalidOperationException("The acquisition did not retain a lease.");

			var taken = lease;
			lease = null;
			return taken;
		}

		public void Dispose()
		{
			lease?.Dispose();
			lease = null;
		}
	}

	sealed class RetailImportOperationIdentity
	{
		private RetailImportOperationIdentity()
		{
		}

		internal static RetailImportOperationIdentity IssueForAcquisition() => new();
	}

	sealed class RetailImportProcessLock : IDisposable
	{
		private sealed class AcquisitionToken
		{
			readonly RetailImportDurableFileSystem storage;
			readonly long epoch;
			readonly RetailImportOperationIdentity operationIdentity;

			private AcquisitionToken(
				RetailImportDurableFileSystem storage,
				long epoch,
				RetailImportOperationIdentity operationIdentity)
			{
				this.storage = storage;
				this.epoch = epoch;
				this.operationIdentity = operationIdentity;
			}

			internal static RetailImportProcessLockLease IssueLease(
				RetailImportProcessLock owner,
				RetailImportDurableFileSystem storage,
				long epoch)
			{
				var identity = RetailImportOperationIdentity.IssueForAcquisition();
				var token = new AcquisitionToken(storage, epoch, identity);
				return RetailImportProcessLockLease.IssueForAcquisition(owner, token, identity);
			}
		}

		readonly object sync = new();
		readonly RetailImportDurableFileSystem storage;
		readonly FileStream stream;
		bool disposed;
		AcquisitionToken acquisition;
		long lastAcquisitionEpoch;

		RetailImportProcessLock(RetailImportDurableFileSystem storage, FileStream stream)
		{
			this.storage = storage;
			this.stream = stream;
		}

		internal static RetailOperationResult<RetailImportProcessLock> Open(
			RetailImportDurableFileSystem storage)
		{
			if (storage == null)
				throw new ArgumentNullException(nameof(storage));

			var opened = storage.OpenProcessLockFile(RetailImportPaths.ProcessLockPath());
			return opened.Succeeded ?
				RetailOperationResult<RetailImportProcessLock>.Success(
					new RetailImportProcessLock(storage, opened.Value)) :
				RetailOperationResult<RetailImportProcessLock>.Failed(opened.Failure);
		}

		internal RetailImportProcessLockAcquireResult TryAcquire()
		{
			lock (sync)
			{
				if (disposed || acquisition != null)
					return new RetailImportProcessLockAcquireResult(
						RetailImportProcessLockAcquireDisposition.Failed, IoFailure(), null);

				try
				{
					if (RetailImportProcessLockNative.TryAcquire(stream))
					{
						var epoch = checked(lastAcquisitionEpoch + 1);
						var lease = AcquisitionToken.IssueLease(this, storage, epoch);
						lastAcquisitionEpoch = epoch;
						acquisition = (AcquisitionToken)lease.AcquisitionToken;
						return new RetailImportProcessLockAcquireResult(
							RetailImportProcessLockAcquireDisposition.Acquired, null, lease);
					}

					return new RetailImportProcessLockAcquireResult(
						RetailImportProcessLockAcquireDisposition.Busy, null, null);
				}
				catch (Exception exception) when (exception is IOException ||
					exception is UnauthorizedAccessException ||
					exception is ObjectDisposedException ||
					exception is PlatformNotSupportedException ||
					exception is OverflowException)
				{
					return new RetailImportProcessLockAcquireResult(
						RetailImportProcessLockAcquireDisposition.Failed, IoFailure(), null);
				}
			}
		}

		internal bool IsBoundTo(RetailImportDurableFileSystem expectedStorage) =>
			ReferenceEquals(storage, expectedStorage) && !disposed;

		internal bool IsCurrentLease(RetailImportProcessLockLease lease) =>
			!disposed && acquisition != null && lease != null &&
			lease.IsLiveAndOwnedBy(this, acquisition);

		internal void Release(RetailImportProcessLockLease lease)
		{
			lock (sync)
			{
				if (disposed || acquisition == null ||
					!lease.IsLiveAndOwnedBy(this, acquisition))
					throw new InvalidOperationException("The process-lock lease does not own this acquisition.");

				try
				{
					RetailImportProcessLockNative.Release(stream);
				}
				finally
				{
					acquisition = null;
				}
			}
		}

		public void Dispose()
		{
			lock (sync)
			{
				if (disposed)
					return;

				try
				{
					if (acquisition != null)
						RetailImportProcessLockNative.Release(stream);
				}
				finally
				{
					acquisition = null;
					disposed = true;
					stream.Dispose();
				}
			}
		}

		static RetailImportFailure IoFailure() => new(
			RetailImportErrorCode.SanitizedIoFailure,
			null,
			null,
			RetailImportDiagnosticCode.None);
	}

	public sealed class RetailImportProcessLockLease : IDisposable
	{
		readonly RetailImportProcessLock owner;
		readonly object acquisition;
		readonly RetailImportOperationIdentity operationIdentity;
		bool disposed;

		private RetailImportProcessLockLease(
			RetailImportProcessLock owner,
			object acquisition,
			RetailImportOperationIdentity operationIdentity)
		{
			this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
			this.acquisition = acquisition ?? throw new ArgumentNullException(nameof(acquisition));
			this.operationIdentity = operationIdentity ??
				throw new ArgumentNullException(nameof(operationIdentity));
		}

		internal object AcquisitionToken => acquisition;

		internal static RetailImportProcessLockLease IssueForAcquisition(
			RetailImportProcessLock owner,
			object acquisition,
			RetailImportOperationIdentity operationIdentity) =>
			new(owner, acquisition, operationIdentity);

		internal RetailImportOperationIdentity BorrowOperationIdentity()
		{
			if (disposed)
				throw new ObjectDisposedException(nameof(RetailImportProcessLockLease));

			return operationIdentity;
		}

		internal bool OwnsOperationIdentity(RetailImportOperationIdentity identity) =>
			!disposed && ReferenceEquals(operationIdentity, identity);

		internal bool IsLiveAndOwnedBy(
			RetailImportProcessLock expectedLock,
			object expectedAcquisition) =>
			!disposed && ReferenceEquals(owner, expectedLock) &&
			ReferenceEquals(acquisition, expectedAcquisition);

		internal bool IsBoundTo(RetailImportDurableFileSystem expectedStorage) =>
			!disposed && owner.IsBoundTo(expectedStorage) && owner.IsCurrentLease(this);

		public void Dispose()
		{
			if (disposed)
				return;

			owner.Release(this);
			disposed = true;
		}
	}

	static class RetailImportProcessLockNative
	{
		const int LockExclusive = 2;
		const int LockNonBlocking = 4;
		const int LockUnlock = 8;
		const int UnixWouldBlock = 11;
		const int MacOsWouldBlock = 35;

		internal static bool TryAcquire(FileStream stream)
		{
			if (OperatingSystem.IsWindows())
			{
				stream.Lock(0, 1);
				return true;
			}

			if (Flock(ToDescriptor(stream.SafeFileHandle), LockExclusive | LockNonBlocking) == 0)
				return true;

			var error = Marshal.GetLastWin32Error();
			if (error is UnixWouldBlock or MacOsWouldBlock)
				return false;

			throw new IOException("The process lock could not be acquired.");
		}

		internal static void Release(FileStream stream)
		{
			if (OperatingSystem.IsWindows())
				stream.Unlock(0, 1);
			else if (Flock(ToDescriptor(stream.SafeFileHandle), LockUnlock) != 0)
				throw new IOException("The process lock could not be released.");
		}

		static int ToDescriptor(Microsoft.Win32.SafeHandles.SafeFileHandle handle)
		{
			var descriptor = handle.DangerousGetHandle().ToInt64();
			if (descriptor < 0 || descriptor > int.MaxValue)
				throw new IOException("The process lock descriptor is invalid.");

			return (int)descriptor;
		}

		[DllImport("libc", EntryPoint = "flock", SetLastError = true)]
		static extern int Flock(int descriptor, int operation);
	}
}
