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
using System.Threading;

namespace OpenRA.Mods.RA2.Content
{
	internal enum RetailImportDirectoryEntryKind { RegularFile, Directory }
	internal enum RetailImportInspectionDisposition { Missing, RegularFile, Directory, Invalid, Failed }

	internal sealed class RetailImportDirectoryEntry : IDisposable
	{
		RetailImportPaths.V1PathToken token;
		int parentDescriptor;
		int descriptor;
		internal RetailImportDirectoryEntryKind Kind { get; }

		internal RetailImportDirectoryEntry(RetailImportPaths.V1PathToken token,
			RetailImportDirectoryEntryKind kind)
		{
			this.token = token ?? throw new ArgumentNullException(nameof(token));
			Kind = kind;
			parentDescriptor = -1;
			descriptor = -1;
		}

		// Enumeration retains both the parent and child descriptors.  Mutations must
		// consume this object; they never reconstruct an entry from its name.
		internal RetailImportDirectoryEntry(RetailImportPaths.V1PathToken token,
			RetailImportDirectoryEntryKind kind, int parentDescriptor, int descriptor)
		{
			this.token = token ?? throw new ArgumentNullException(nameof(token));
			Kind = kind;
			this.parentDescriptor = parentDescriptor;
			this.descriptor = descriptor;
		}

		internal bool Matches(RetailImportPaths.V1PathToken expected) =>
			token != null && token.Matches(expected);

		internal bool TryGetGenerationIdentity(out string generationId, out long epoch)
		{
			generationId = null;
			epoch = 0;
			if (token == null || token.Kind != RetailImportV1PathKind.GenerationDirectory)
				return false;
			if (!token.TryGetGenerationIdentity(out var value, out epoch))
				return false;
			generationId = value;
			return true;
		}

		internal bool TryGetPendingItemIdentity(out RetailImportV1PathKind kind, out Guid nonce)
		{
			kind = default;
			nonce = Guid.Empty;
			if (token == null || token.Kind is not (RetailImportV1PathKind.PendingStagingDirectory or
				RetailImportV1PathKind.PendingSealedDirectory or RetailImportV1PathKind.PendingTombstone))
				return false;
			if (!token.TryGetPendingIdentity(out nonce))
				return false;
			kind = token.Kind;
			return true;
		}

		internal bool TryTakePinnedDirectory(out RetailImportPaths.V1PathToken value,
			out int parent, out int child)
		{
			value = Interlocked.Exchange(ref token, null);
			parent = Interlocked.Exchange(ref parentDescriptor, -1);
			child = Interlocked.Exchange(ref descriptor, -1);
			return value != null && parent >= 0 && child >= 0 &&
				Kind == RetailImportDirectoryEntryKind.Directory;
		}

		public void Dispose()
		{
			Interlocked.Exchange(ref token, null);
			var child = Interlocked.Exchange(ref descriptor, -1);
			var parent = Interlocked.Exchange(ref parentDescriptor, -1);
			if (child >= 0) IosRuntimeUnixNative.Close(child);
			if (parent >= 0) IosRuntimeUnixNative.Close(parent);
		}
	}

	internal sealed class RetailImportInspectionResult : IDisposable
	{
		RetailImportDirectoryEntry entry;
		internal RetailImportInspectionDisposition Disposition { get; }
		internal RetailImportFailure Failure { get; }
		internal bool HasEntry => entry != null;

		internal RetailImportInspectionResult(RetailImportInspectionDisposition disposition,
			RetailImportDirectoryEntry entry, RetailImportFailure failure)
		{
			Disposition = disposition;
			this.entry = entry;
			Failure = failure;
			if (disposition is RetailImportInspectionDisposition.RegularFile or RetailImportInspectionDisposition.Directory)
			{
				if (entry == null || failure != null) throw new ArgumentException("A safe inspection must own one entry.");
			}
			else if (entry != null || (disposition is RetailImportInspectionDisposition.Invalid or RetailImportInspectionDisposition.Failed) != (failure != null))
				throw new ArgumentException("The inspection tuple is invalid.");
		}

		internal RetailImportDirectoryEntry TakeEntry() =>
			Interlocked.Exchange(ref entry, null) ?? throw new InvalidOperationException("The inspection has no entry.");
		public void Dispose() => Interlocked.Exchange(ref entry, null)?.Dispose();
	}

	internal sealed class RetailImportPinnedFile : IDisposable
	{
		FileStream stream;
		readonly long length;

		internal RetailImportPinnedFile(FileStream stream)
		{
			this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
			length = stream.Length;
		}

		internal long Length => length;
		internal Stream OpenRead()
		{
			var current = Volatile.Read(ref stream) ?? throw new ObjectDisposedException(nameof(RetailImportPinnedFile));
			current.Position = 0;
			return current;
		}
		internal RetailOperationResult VerifyUnchanged()
		{
			try
			{
				var current = Volatile.Read(ref stream);
				// The stream is the descriptor opened beneath the pinned root.  Do not
				// re-open its pathname here: that would turn verification into a TOCTOU.
				if (current == null || current.Length != length)
					return RetailOperationResult.Failed(RetailImportDurableFileSystem.TypedIoFailure());
				return RetailOperationResult.Success();
			}
			catch (IOException) { return RetailOperationResult.Failed(RetailImportDurableFileSystem.TypedIoFailure()); }
			catch (UnauthorizedAccessException) { return RetailOperationResult.Failed(RetailImportDurableFileSystem.TypedIoFailure()); }
		}
		public void Dispose() => Interlocked.Exchange(ref stream, null)?.Dispose();
	}

	// This is deliberately a capability, not a generation path.  Holding a pin
	// holds the exact process-lock acquisition which authenticated the directory;
	// consumers can consequently open only receipt-derived artifact tokens.
	internal sealed class RetailGenerationPin : IRetailCatalogArtifactSource
	{
		RetailImportDurableFileSystem storage;
		RetailImportProcessLockLease lease;
		int ownsLease;
		readonly string generationId;
		readonly long epoch;

		internal RetailGenerationPin(RetailImportDurableFileSystem storage,
			RetailImportProcessLockLease lease, string generationId, long epoch)
		{
			this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
			this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
			this.generationId = RetailImportPaths.ValidateGenerationId(generationId, epoch,
				nameof(generationId));
			this.epoch = epoch;
		}

		public Stream OpenVerifiedRead(RetailCatalogArtifact artifact)
		{
			var currentStorage = Volatile.Read(ref storage);
			var currentLease = Volatile.Read(ref lease);
			if (currentStorage == null || currentLease == null || artifact == null)
				throw new ObjectDisposedException(nameof(RetailGenerationPin));
			var opened = currentStorage.OpenPinnedRead(currentLease,
				RetailImportPaths.GenerationArtifactPath(generationId, epoch,
					artifact.CanonicalGenerationRelativePath));
			if (!opened.Succeeded)
				throw new IOException("The pinned generation artifact could not be opened safely.");
			return new RetailPinnedReadStream(opened.Value, artifact.Length, artifact.Sha256);
		}

		// Pins begin as a borrow of their operation's lease.  A lifecycle may transfer
		// that lease only when it returns the snapshot to its caller; validation
		// snapshots must never release the outer Prepare/Recover acquisition.
		internal void TakeLeaseOwnership()
		{
			if (Volatile.Read(ref lease) == null)
				throw new ObjectDisposedException(nameof(RetailGenerationPin));
			Interlocked.Exchange(ref ownsLease, 1);
		}

		public void Dispose()
		{
			Interlocked.Exchange(ref storage, null);
			var currentLease = Interlocked.Exchange(ref lease, null);
			if (Interlocked.Exchange(ref ownsLease, 0) != 0)
				currentLease?.Dispose();
		}

		sealed class RetailPinnedReadStream : Stream
		{
			RetailImportPinnedFile pinned;
			readonly Stream stream;
			readonly System.Security.Cryptography.IncrementalHash hash =
				System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
			readonly long expectedLength;
			readonly string expectedSha256;
			long total;
			bool verified;
			internal RetailPinnedReadStream(RetailImportPinnedFile pinned, long expectedLength,
				string expectedSha256)
			{
				this.pinned = pinned ?? throw new ArgumentNullException(nameof(pinned));
				stream = pinned.OpenRead();
				this.expectedLength = expectedLength;
				this.expectedSha256 = expectedSha256;
			}
			public override bool CanRead => stream.CanRead;
			public override bool CanSeek => stream.CanSeek;
			public override bool CanWrite => false;
			public override long Length => stream.Length;
			public override long Position { get => stream.Position; set => stream.Position = value; }
			public override void Flush() => stream.Flush();
			public override int Read(byte[] buffer, int offset, int count)
			{
				var read = stream.Read(buffer, offset, count);
				if (read != 0)
				{
					total = checked(total + read);
					hash.AppendData(buffer, offset, read);
				}
				else if (!verified)
				{
					verified = true;
					if (total != expectedLength || !StringComparer.Ordinal.Equals(
						Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), expectedSha256) ||
						!pinned.VerifyUnchanged().Succeeded)
						throw new InvalidDataException("The pinned generation artifact no longer matches its receipt.");
				}
				return read;
			}
			public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);
			public override void SetLength(long value) => throw new NotSupportedException();
			public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					hash.Dispose();
					Interlocked.Exchange(ref pinned, null)?.Dispose();
				}
				base.Dispose(disposing);
			}
		}
	}
}
