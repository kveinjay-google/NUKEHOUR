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
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace OpenRA.Mods.RA2.Content
{
	public interface IRetailStoragePolicy
	{
		RetailOperationResult ApplyAndVerify(string path, bool recursive);
	}

	public interface IRetailDirectoryDurability
	{
		RetailOperationResult Sync(string directory);
	}

	enum RetailDurableWriteCheckpoint
	{
		AfterTemporaryCreated,
		AfterPayloadFlushed,
		AfterTemporaryPolicyApplied,
		BeforePublish,
		AfterPublish,
		AfterPublishedPolicyApplied,
		BeforeDirectorySync,
		AfterDirectorySync,
	}

	enum RetailPayloadInspection
	{
		Expected,
		Modified,
		Unsafe,
	}

	readonly struct RetailManagedFileIdentity
	{
		public ulong Volume { get; }
		public ulong Node { get; }
		public bool IsDirectory { get; }
		public long Length { get; }

		public RetailManagedFileIdentity(ulong volume, ulong node, bool isDirectory, long length)
		{
			Volume = volume;
			Node = node;
			IsDirectory = isDirectory;
			Length = length;
		}

		public bool SameNodeAndType(RetailManagedFileIdentity other) =>
			Volume == other.Volume && Node == other.Node && IsDirectory == other.IsDirectory;
	}

	sealed class RetailImportDurableFileSystem : IDisposable
	{
		const int UnixOwnerFileMode = 0x180;
		const int UnixOwnerDirectoryMode = 0x1C0;
		const int UnixMissingEntry = 2;
		const int MaximumCreateAttempts = 8;
		readonly object sync = new();
		readonly string displayRoot;
		readonly string nativeRoot;
		readonly IRetailStoragePolicy storagePolicy;
		readonly IRetailDirectoryDurability directoryDurability;
		readonly Action<RetailDurableWriteCheckpoint> checkpoint;
		readonly bool useUnixDescriptors;
		readonly IosUnixFileLayout unixLayout;
		readonly IosUnixFileIdentity rootIdentity;
		readonly int outerParentDescriptor;
		readonly IosUnixFileIdentity outerParentIdentity;
		readonly string rootLeafName;
		readonly RetailImportPaths.DurableAdapterResolver pathResolver;
		int rootDescriptor;
		bool disposed;

		RetailImportDurableFileSystem(
			string displayRoot,
			string nativeRoot,
			IRetailStoragePolicy storagePolicy,
			IRetailDirectoryDurability directoryDurability,
			Action<RetailDurableWriteCheckpoint> checkpoint,
			int rootDescriptor,
			IosUnixFileLayout unixLayout,
			IosUnixFileIdentity rootIdentity,
			int outerParentDescriptor,
			IosUnixFileIdentity outerParentIdentity,
			string rootLeafName,
			bool useUnixDescriptors,
			RetailImportPaths.DurableAdapterResolver pathResolver)
		{
			this.displayRoot = displayRoot;
			this.nativeRoot = nativeRoot;
			this.storagePolicy = storagePolicy;
			this.directoryDurability = directoryDurability;
			this.checkpoint = checkpoint;
			this.rootDescriptor = rootDescriptor;
			this.unixLayout = unixLayout;
			this.rootIdentity = rootIdentity;
			this.outerParentDescriptor = outerParentDescriptor;
			this.outerParentIdentity = outerParentIdentity;
			this.rootLeafName = rootLeafName;
			this.useUnixDescriptors = useUnixDescriptors;
			this.pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
		}

		internal static RetailOperationResult<RetailImportDurableFileSystem> Open(
			string fixedRoot,
			IRetailStoragePolicy storagePolicy,
			IRetailDirectoryDurability directoryDurability,
			Action<RetailDurableWriteCheckpoint> checkpoint = null)
		{
			if (storagePolicy == null)
				throw new ArgumentNullException(nameof(storagePolicy));
			if (directoryDurability == null)
				throw new ArgumentNullException(nameof(directoryDurability));

			var descriptor = -1;
			var outerParent = -1;
			try
			{
				if (!TryNormalizeRoot(fixedRoot, out var displayRoot, out var nativeRoot))
					return FailedFileSystem();

				if (IosRuntimeUnixNative.TryGetCurrentLayout(out var layout))
				{
					var parentPath = Path.GetDirectoryName(nativeRoot);
					var leafName = Path.GetFileName(nativeRoot);
					if (String.IsNullOrEmpty(parentPath) || String.IsNullOrEmpty(leafName) ||
						!TryOpenUnixDirectory(parentPath, layout, out outerParent, out var parentIdentity))
						return FailedFileSystem();
					// A reset may have stopped after its durable marker reached disk while the
					// original root was still present.  Always serialize and converge that
					// marker before exposing the root; only checking after a failed open
					// leaves a state-1 marker to poison the next confirmed reset.
					if (!TryConvergeOuterResetAtOpen(outerParent, parentIdentity, layout, leafName) ||
						!TryOpenUnixDirectory(nativeRoot, layout, out descriptor, out var identity) || !identity.IsDirectory)
						return FailedFileSystem();
					var verifiedLeaf = IosRuntimeUnixNative.OpenAt(outerParent, leafName, layout.DirectoryFlags, 0);
					if (verifiedLeaf < 0) return FailedFileSystem();
					try
					{
						if (!IosRuntimeUnixNative.TryReadIdentity(verifiedLeaf, layout, out var verifiedIdentity) ||
							!verifiedIdentity.SameNodeAndType(identity)) return FailedFileSystem();
					}
					finally { IosRuntimeUnixNative.Close(verifiedLeaf); }

					var fileSystem = new RetailImportDurableFileSystem(
						displayRoot,
						nativeRoot,
						storagePolicy,
						directoryDurability,
						checkpoint,
						descriptor,
						layout,
						identity,
						outerParent,
						parentIdentity,
						leafName,
					useUnixDescriptors: true,
					RetailImportPaths.CreateDurableAdapterResolver());
					descriptor = -1;
					outerParent = -1;
					return RetailOperationResult<RetailImportDurableFileSystem>.Success(fileSystem);
				}

				if (IsUnixPlatform())
					return FailedFileSystem();

				if (!TryVerifyManagedDirectory(displayRoot))
					return FailedFileSystem();

				return RetailOperationResult<RetailImportDurableFileSystem>.Success(
					new RetailImportDurableFileSystem(
						displayRoot,
						nativeRoot,
						storagePolicy,
						directoryDurability,
						checkpoint,
						-1,
						default,
						default,
						-1,
						default,
						null,
						useUnixDescriptors: false,
						RetailImportPaths.CreateDurableAdapterResolver()));
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return FailedFileSystem();
			}
			finally
			{
				if (descriptor >= 0)
					IosRuntimeUnixNative.Close(descriptor);
				if (outerParent >= 0)
					IosRuntimeUnixNative.Close(outerParent);
			}
		}

		RetailOperationResult EnsureDirectoryCore(
			RetailImportPaths.V1PathToken directory,
			CancellationToken cancellationToken,
			bool artifactParent = false)
		{
			lock (sync)
			{
				var published = false;
				try
				{
					ThrowIfDisposed();
					if (!IsDirectoryToken(directory) && !(artifactParent && directory?.Kind is
						RetailImportV1PathKind.GenerationArtifact or RetailImportV1PathKind.PendingStagingArtifact))
						return FailedIo();
					var relativePath = pathResolver.Resolve(directory);
					if (artifactParent)
					{
						var separator = relativePath.LastIndexOf('/');
						if (separator <= 0)
							return FailedIo();
						relativePath = relativePath[..separator];
					}
					cancellationToken.ThrowIfCancellationRequested();

					return useUnixDescriptors ?
						EnsureUnixDirectory(relativePath, cancellationToken, ref published) :
						EnsureManagedDirectory(relativePath, cancellationToken, ref published);
				}
				catch (OperationCanceledException) when (!published && cancellationToken.IsCancellationRequested)
				{
					return Cancelled();
				}
				catch (Exception exception) when (IsBoundaryException(exception))
				{
					return FailedIo();
				}
			}
		}

		internal RetailOperationResult<FileStream> OpenProcessLockFile(
			RetailImportPaths.V1PathToken path)
		{
			lock (sync)
			{
				try
				{
					ThrowIfDisposed();
					if (path == null || !path.Matches(RetailImportPaths.ProcessLockPath()))
						return RetailOperationResult<FileStream>.Failed(IoFailure());

					if (useUnixDescriptors)
						return OpenUnixProcessLockFile();

					return OpenManagedProcessLockFile();
				}
				catch (Exception exception) when (IsBoundaryException(exception))
				{
					return RetailOperationResult<FileStream>.Failed(IoFailure());
				}
			}
		}

		RetailOperationResult WriteAtomicCore(
			RetailImportPaths.V1PathToken document,
			byte[] payload,
			CancellationToken cancellationToken)
		{
			if (payload == null)
				throw new ArgumentNullException(nameof(payload));

			lock (sync)
			{
				var published = false;
				try
				{
					ThrowIfDisposed();
					if (!IsWritableDocumentToken(document))
						return FailedIo();
					var relativePath = pathResolver.Resolve(document);
					cancellationToken.ThrowIfCancellationRequested();

					return useUnixDescriptors ?
						WriteAtomicUnix(relativePath, payload, cancellationToken, ref published) :
						WriteAtomicManaged(relativePath, payload, cancellationToken, ref published);
				}
				catch (OperationCanceledException) when (!published && cancellationToken.IsCancellationRequested)
				{
					return Cancelled();
				}
				catch (Exception exception) when (IsBoundaryException(exception))
				{
					return FailedIo();
				}
			}
		}

		internal RetailImportInspectionResult Inspect(RetailImportProcessLockLease liveLease,
			RetailImportPaths.V1PathToken path)
		{
			if (!HasLiveTypedLease(liveLease) || path == null)
				return new RetailImportInspectionResult(RetailImportInspectionDisposition.Failed, null, TypedIoFailure());
			try
			{
				if (!useUnixDescriptors)
					return new RetailImportInspectionResult(RetailImportInspectionDisposition.Failed, null, TypedIoFailure());
				var components = pathResolver.Resolve(path).Split('/');
				if (!TryOpenPinnedUnixDirectory(components.Take(components.Length - 1).ToArray(), out var parent, out _))
					return new RetailImportInspectionResult(RetailImportInspectionDisposition.Failed, null, TypedIoFailure());
				try
				{
					var descriptor = IosRuntimeUnixNative.OpenAt(parent, components[^1], unixLayout.FileFlags, 0);
					if (descriptor < 0)
						return Marshal.GetLastWin32Error() == UnixMissingEntry ?
							new RetailImportInspectionResult(RetailImportInspectionDisposition.Missing, null, null) :
							new RetailImportInspectionResult(RetailImportInspectionDisposition.Failed, null, TypedIoFailure());
					try
					{
						if (!IosRuntimeUnixNative.TryReadIdentity(descriptor, unixLayout, out var identity))
							return new RetailImportInspectionResult(RetailImportInspectionDisposition.Invalid, null, TypedIoFailure());
						var kind = identity.IsDirectory ? RetailImportDirectoryEntryKind.Directory :
							identity.IsRegular ? RetailImportDirectoryEntryKind.RegularFile : default;
						if (!identity.IsDirectory && !IsSingleLinkedRegular(identity))
							return new RetailImportInspectionResult(RetailImportInspectionDisposition.Invalid, null, TypedIoFailure());
						return new RetailImportInspectionResult(kind == RetailImportDirectoryEntryKind.Directory ?
							RetailImportInspectionDisposition.Directory : RetailImportInspectionDisposition.RegularFile,
							new RetailImportDirectoryEntry(path, kind), null);
					}
					finally { IosRuntimeUnixNative.Close(descriptor); }
				}
				finally { IosRuntimeUnixNative.Close(parent); }
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return new RetailImportInspectionResult(RetailImportInspectionDisposition.Failed, null, TypedIoFailure());
			}
		}

		internal RetailOperationResult<ImmutableArray<RetailImportDirectoryEntry>> Enumerate(
			RetailImportProcessLockLease liveLease, RetailImportPaths.V1PathToken directory)
		{
			if (!HasLiveTypedLease(liveLease) || directory == null || directory.Kind is not
				(RetailImportV1PathKind.GenerationsDirectory or RetailImportV1PathKind.PendingItemsDirectory or RetailImportV1PathKind.QuarantineDirectory))
				return RetailOperationResult<ImmutableArray<RetailImportDirectoryEntry>>.Failed(TypedIoFailure());
			try
			{
				if (!useUnixDescriptors)
					return RetailOperationResult<ImmutableArray<RetailImportDirectoryEntry>>.Failed(TypedIoFailure());
				var components = pathResolver.Resolve(directory).Split('/');
				if (!TryOpenPinnedUnixDirectory(components, out var parent, out _))
					return RetailOperationResult<ImmutableArray<RetailImportDirectoryEntry>>.Failed(TypedIoFailure());
				var entries = ImmutableArray.CreateBuilder<RetailImportDirectoryEntry>();
				try
				{
					var names = RetailDurableUnixNative.EnumerateDirectoryNames(parent).OrderBy(name => name, StringComparer.Ordinal).ToArray();
					var aliases = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
					foreach (var name in names)
					{
						if (!aliases.Add(name) || HasCaseAlias(parent, name))
							throw new IOException("A case alias is unsafe.");
						var childDescriptor = IosRuntimeUnixNative.OpenAt(parent, name, unixLayout.DirectoryFlags, 0);
						if (childDescriptor < 0) throw new IOException("Could not pin directory entry.");
						try
						{
							if (!IosRuntimeUnixNative.TryReadIdentity(childDescriptor, unixLayout, out var childIdentity) || !childIdentity.IsDirectory)
								throw new IOException("Directory entry is not a directory.");
					RetailImportPaths.V1PathToken childToken;
					if (directory.Kind == RetailImportV1PathKind.GenerationsDirectory)
					{
						if (name.Length != 55 || !long.TryParse(name.AsSpan(2, 20), out var epoch)) throw new IOException("Invalid generation identity.");
						childToken = RetailImportPaths.GenerationDirectoryPath(name, epoch);
					}
					else if (directory.Kind == RetailImportV1PathKind.PendingItemsDirectory)
					{
						if (name.Length != 34 || !Guid.TryParseExact(name[2..], "N", out var nonce) || nonce == Guid.Empty) throw new IOException("Invalid pending identity.");
						childToken = name[0] switch { 's' => RetailImportPaths.PendingStagingDirectoryPath(nonce), 'p' => RetailImportPaths.PendingSealedDirectoryPath(nonce), 't' => RetailImportPaths.PendingTombstonePath(nonce), _ => throw new IOException("Invalid pending identity.") };
					}
					else
					{
						if (name.Length != 34 || !Guid.TryParseExact(name[2..], "N", out var nonce) || nonce == Guid.Empty || name[0] != 'q')
							throw new IOException("Invalid quarantine identity.");
						childToken = RetailImportPaths.QuarantineEntryPath(nonce);
					}
						// Retain descriptors rather than returning a re-openable name.  The
						// caller can only pass this one-shot capability back to a typed
						// mutation operation below.
						var retainedParent = IosRuntimeUnixNative.Dup(parent);
						if (retainedParent < 0) throw new IOException("Could not pin entry parent.");
						entries.Add(new RetailImportDirectoryEntry(childToken,
							RetailImportDirectoryEntryKind.Directory, retainedParent, childDescriptor));
						childDescriptor = -1;
					}
						finally { IosRuntimeUnixNative.Close(childDescriptor); }
					}
				}
				finally { IosRuntimeUnixNative.Close(parent); }
				return RetailOperationResult<ImmutableArray<RetailImportDirectoryEntry>>.Success(entries.ToImmutable());
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
			{
				return RetailOperationResult<ImmutableArray<RetailImportDirectoryEntry>>.Failed(TypedIoFailure());
			}
		}

		internal RetailOperationResult<RetailImportPinnedFile> OpenPinnedRead(RetailImportProcessLockLease liveLease,
			RetailImportPaths.V1PathToken file)
		{
			if (!HasLiveTypedLease(liveLease) || !IsReadableFileToken(file)) return RetailOperationResult<RetailImportPinnedFile>.Failed(TypedIoFailure());
			try
			{
				if (!useUnixDescriptors)
					return RetailOperationResult<RetailImportPinnedFile>.Failed(TypedIoFailure());
				var components = pathResolver.Resolve(file).Split('/');
				if (!TryOpenPinnedUnixDirectory(components.Take(components.Length - 1).ToArray(), out var parent, out _))
					return RetailOperationResult<RetailImportPinnedFile>.Failed(TypedIoFailure());
				try
				{
					var descriptor = IosRuntimeUnixNative.OpenAt(parent, components[^1], unixLayout.FileFlags, 0);
					if (descriptor < 0 || !IosRuntimeUnixNative.TryReadIdentity(descriptor, unixLayout, out var identity) || !IsSingleLinkedRegular(identity))
					{
						if (descriptor >= 0) IosRuntimeUnixNative.Close(descriptor);
						return RetailOperationResult<RetailImportPinnedFile>.Failed(TypedIoFailure());
					}
					var handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
					return RetailOperationResult<RetailImportPinnedFile>.Success(new RetailImportPinnedFile(
						new FileStream(handle, FileAccess.Read, 81920, isAsync: false)));
				}
				finally { IosRuntimeUnixNative.Close(parent); }
			}
			catch (Exception exception) when (IsBoundaryException(exception)) { return RetailOperationResult<RetailImportPinnedFile>.Failed(TypedIoFailure()); }
		}

		internal RetailOperationResult<RetailGenerationPin> PinGeneration(
			RetailImportProcessLockLease liveLease, string generationId, long epoch)
		{
			if (!HasLiveTypedLease(liveLease))
				return RetailOperationResult<RetailGenerationPin>.Failed(TypedIoFailure());
			try
			{
				var token = RetailImportPaths.GenerationDirectoryPath(generationId, epoch);
				using var inspected = Inspect(liveLease, token);
				if (inspected.Disposition != RetailImportInspectionDisposition.Directory)
					return RetailOperationResult<RetailGenerationPin>.Failed(
						inspected.Failure ?? TypedIoFailure());
				return RetailOperationResult<RetailGenerationPin>.Success(
					new RetailGenerationPin(this, liveLease, generationId, epoch));
			}
			catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
			{
				return RetailOperationResult<RetailGenerationPin>.Failed(TypedIoFailure());
			}
		}

		// Reset can cross this boundary only with the one-shot session minted by the
		// owning store.  The adapter receives neither a root string nor a caller
		// supplied lease, so the reset authority cannot be redirected to another
		// importer root between confirmation and the durable operation.
		internal RetailOperationResult<RetailQuarantinedRoot> QuarantineRootForConfirmedReset(
			RetailGenerationStore.ConfirmedResetSession ownedSession,
			CancellationToken cancellationToken)
		{
			if (ownedSession == null || cancellationToken.IsCancellationRequested)
				return RetailOperationResult<RetailQuarantinedRoot>.Failed(TypedIoFailure());

			var lease = ownedSession.TakeFor(this, out var retainedFaultInjection);
			if (!lease.Succeeded || !HasLiveTypedLease(lease.Value))
				return RetailOperationResult<RetailQuarantinedRoot>.Failed(
					lease.Succeeded ? TypedIoFailure() : lease.Failure);

			if (!useUnixDescriptors || retainedFaultInjection == null ||
				!TryAcquireOuterResetLock(out var outerLock))
				return RetailOperationResult<RetailQuarantinedRoot>.Failed(TypedIoFailure());

			try
			{
				if (!IosRuntimeUnixNative.TryReadIdentity(outerParentDescriptor, unixLayout, out var parent) ||
					!parent.IsDirectory || !parent.SameNodeAndType(outerParentIdentity) ||
					!TryVerifyResetSource())
					return RetailOperationResult<RetailQuarantinedRoot>.Failed(TypedIoFailure());

				var nonce = Guid.NewGuid();
				var sibling = ResetSiblingName(nonce);
				if (!WriteResetMarker(nonce, state: 1, create: true))
					return RetailOperationResult<RetailQuarantinedRoot>.Failed(TypedIoFailure());
				retainedFaultInjection.Hit(RetailImportFailpoint.ResetPreparedMarkerFlushed);
				if (RetailDurableUnixNative.RenameAt(outerParentDescriptor, rootLeafName,
					outerParentDescriptor, sibling) != 0 || RetailDurableUnixNative.Sync(outerParentDescriptor) != 0)
					return RetailOperationResult<RetailQuarantinedRoot>.Failed(TypedIoFailure());
				retainedFaultInjection.Hit(RetailImportFailpoint.ResetRootRenameFlushed);
				if (!WriteResetMarker(nonce, state: 2, create: false))
					return RetailOperationResult<RetailQuarantinedRoot>.Failed(TypedIoFailure());
				retainedFaultInjection.Hit(RetailImportFailpoint.ResetRenamedMarkerFlushed);
				var result = new RetailQuarantinedRoot(this, lease.Value, outerLock, nonce,
					retainedFaultInjection);
				outerLock = null;
				return RetailOperationResult<RetailQuarantinedRoot>.Success(result);
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return RetailOperationResult<RetailQuarantinedRoot>.Failed(TypedIoFailure());
			}
			finally { outerLock?.Dispose(); }
		}

		internal RetailOperationResult PurgeQuarantinedRoot(RetailQuarantinedRoot quarantined,
			CancellationToken cancellationToken)
		{
			if (quarantined == null || cancellationToken.IsCancellationRequested || !useUnixDescriptors ||
				!quarantined.TryConsumeFor(this, out var innerLease, out var outerLease, out var nonce,
					out var resetFaults))
				return RetailOperationResult.Failed(TypedIoFailure());
			using (innerLease)
			using (outerLease)
			{
				try
				{
					if (!IosRuntimeUnixNative.TryReadIdentity(outerParentDescriptor, unixLayout, out var parent) ||
						!parent.IsDirectory || !parent.SameNodeAndType(outerParentIdentity) ||
						!ConvergeOuterResetTransaction(nonce, cancellationToken, resetFaults))
						return RetailOperationResult.Failed(TypedIoFailure());
					return RetailOperationResult.Success();
				}
				catch (Exception exception) when (IsBoundaryException(exception))
				{
					return RetailOperationResult.Failed(TypedIoFailure());
				}
			}
		}

		bool ConvergeOuterResetTransaction(Guid nonce, CancellationToken cancellationToken,
			RetailImportFaultInjection resetFaults) =>
			PurgeResetSiblingAndRecreateRoot(nonce, cancellationToken, resetFaults);

		bool TryVerifyResetSource()
		{
			if (rootDescriptor < 0 || !IosRuntimeUnixNative.TryReadIdentity(rootDescriptor, unixLayout, out var pinned) ||
				!pinned.IsDirectory || !pinned.SameNodeAndType(rootIdentity)) return false;
			var source = IosRuntimeUnixNative.OpenAt(outerParentDescriptor, rootLeafName, unixLayout.DirectoryFlags, 0);
			if (source < 0) return false;
			try
			{
				return IosRuntimeUnixNative.TryReadIdentity(source, unixLayout, out var current) &&
					current.IsDirectory && current.SameNodeAndType(rootIdentity);
			}
			finally { IosRuntimeUnixNative.Close(source); }
		}

		bool TryAcquireOuterResetLock(out FileStream stream)
		{
			stream = null;
			if (outerParentDescriptor < 0 || HasCaseAlias(outerParentDescriptor, ".retail-import-reset.lock")) return false;
			var descriptor = IosRuntimeUnixNative.OpenAt(outerParentDescriptor, ".retail-import-reset.lock",
				RetailDurableUnixNative.OpenReadWriteFlags, 0);
			if (descriptor < 0 && Marshal.GetLastWin32Error() == UnixMissingEntry)
			{
				descriptor = IosRuntimeUnixNative.OpenAt(outerParentDescriptor, ".retail-import-reset.lock",
					RetailDurableUnixNative.CreateNewReadWriteFlags, UnixOwnerFileMode);
				if (descriptor >= 0 && (RetailDurableUnixNative.ChangeMode(descriptor, UnixOwnerFileMode) != 0 ||
					RetailDurableUnixNative.Sync(outerParentDescriptor) != 0)) { IosRuntimeUnixNative.Close(descriptor); return false; }
			}
			if (descriptor < 0 || !IosRuntimeUnixNative.TryReadIdentity(descriptor, unixLayout, out var identity) || !IsSingleLinkedRegular(identity)) { if (descriptor >= 0) IosRuntimeUnixNative.Close(descriptor); return false; }
			try
			{
				stream = new FileStream(new SafeFileHandle((IntPtr)descriptor, ownsHandle: true), FileAccess.ReadWrite, 81920, false);
				descriptor = -1;
				if (RetailImportProcessLockNative.TryAcquire(stream))
					return true;
				stream.Dispose();
				stream = null;
				return false;
			}
			catch { stream?.Dispose(); stream = null; return false; }
			finally { if (descriptor >= 0) IosRuntimeUnixNative.Close(descriptor); }
		}

		bool WriteResetMarker(Guid nonce, byte state, bool create)
		{
			var flags = create ? RetailDurableUnixNative.CreateNewReadWriteFlags : RetailDurableUnixNative.OpenReadWriteFlags;
			var descriptor = IosRuntimeUnixNative.OpenAt(outerParentDescriptor, ".retail-import-reset.transaction", flags, UnixOwnerFileMode);
			if (descriptor < 0) return false;
			try
			{
				if (create && RetailDurableUnixNative.ChangeMode(descriptor, UnixOwnerFileMode) != 0)
					return false;
				var marker = new byte[86];
				System.Text.Encoding.ASCII.GetBytes("NHRT").CopyTo(marker, 0); marker[4] = 1; marker[5] = state;
				System.Text.Encoding.ASCII.GetBytes(nonce.ToString("N")).CopyTo(marker, 6);
				BitConverter.TryWriteBytes(marker.AsSpan(38, 8), rootIdentity.Device);
				BitConverter.TryWriteBytes(marker.AsSpan(46, 8), rootIdentity.Inode);
				SHA256.HashData(marker.AsSpan(0, 54)).CopyTo(marker, 54);
				using var output = new FileStream(new SafeFileHandle((IntPtr)IosRuntimeUnixNative.Dup(descriptor), true), FileAccess.Write, 81920, false);
				output.SetLength(0); output.Write(marker); output.Flush(flushToDisk: true);
				return RetailDurableUnixNative.Sync(outerParentDescriptor) == 0;
			}
			catch (Exception exception) when (IsBoundaryException(exception)) { return false; }
			finally { IosRuntimeUnixNative.Close(descriptor); }
		}

		static string ResetSiblingName(Guid nonce) => ".retail-import-reset-q-" + nonce.ToString("N");

		bool PurgeResetSiblingAndRecreateRoot(Guid nonce, CancellationToken cancellationToken,
			RetailImportFaultInjection resetFaults)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!TryReadResetMarker(outerParentDescriptor, unixLayout, out var markerNonce, out var state,
				out var markerDevice, out var markerInode) || markerNonce != nonce ||
				state != 2 || HasCaseAlias(outerParentDescriptor, ResetSiblingName(nonce)) ||
				HasCaseAlias(outerParentDescriptor, rootLeafName))
				return false;

			var sibling = ResetSiblingName(nonce);
			var siblingDescriptor = IosRuntimeUnixNative.OpenAt(outerParentDescriptor, sibling,
				unixLayout.DirectoryFlags, 0);
			if (siblingDescriptor < 0)
				return false;
			try
			{
				if (!IosRuntimeUnixNative.TryReadIdentity(siblingDescriptor, unixLayout, out var siblingIdentity) ||
					!siblingIdentity.IsDirectory || siblingIdentity.Device != markerDevice ||
					siblingIdentity.Inode != markerInode)
					return false;

				resetFaults.Hit(RetailImportFailpoint.ResetQuarantinePurgeStarted);
				// The process lock lives below the moved root.  Removing it first makes
				// it impossible for an old descriptor to be confused with the lock in
				// the freshly created root; all remaining children are descriptor-walked.
				if (RetailDurableUnixNative.UnlinkAt(siblingDescriptor,
					"import.lock", 0) == 0)
					resetFaults.Hit(RetailImportFailpoint.ResetInnerLockUnlinked);
				else if (Marshal.GetLastWin32Error() != UnixMissingEntry)
					return false;
				if (!PurgePinnedDirectory(siblingDescriptor) ||
					RetailDurableUnixNative.UnlinkAt(outerParentDescriptor, sibling,
						RetailDurableUnixNative.RemoveDirectoryFlag) != 0 ||
					RetailDurableUnixNative.Sync(outerParentDescriptor) != 0)
					return false;
			}
			finally { IosRuntimeUnixNative.Close(siblingDescriptor); }

			var existingRoot = IosRuntimeUnixNative.OpenAt(outerParentDescriptor, rootLeafName, unixLayout.DirectoryFlags, 0);
			if (existingRoot >= 0)
			{
				IosRuntimeUnixNative.Close(existingRoot);
				return false;
			}
			if (Marshal.GetLastWin32Error() != UnixMissingEntry ||
				RetailDurableUnixNative.MkdirAt(outerParentDescriptor, rootLeafName, UnixOwnerDirectoryMode) != 0 ||
				RetailDurableUnixNative.Sync(outerParentDescriptor) != 0 ||
				RetailDurableUnixNative.UnlinkAt(outerParentDescriptor, ".retail-import-reset.transaction", 0) != 0 ||
				RetailDurableUnixNative.Sync(outerParentDescriptor) != 0)
				return false;
			resetFaults.Hit(RetailImportFailpoint.ResetTransactionMarkerCleared);
			return true;
		}

		static bool TryReadResetMarker(int parentDescriptor, IosUnixFileLayout layout, out Guid nonce, out byte state,
			out ulong device, out ulong inode)
		{
			nonce = Guid.Empty;
			state = 0;
			device = 0;
			inode = 0;
			var descriptor = IosRuntimeUnixNative.OpenAt(parentDescriptor,
				".retail-import-reset.transaction", RetailDurableUnixNative.OpenReadWriteFlags, 0);
			if (descriptor < 0) return false;
			try
			{
				if (!IosRuntimeUnixNative.TryReadIdentity(descriptor, layout, out var identity) ||
					!IsSingleLinkedRegular(identity)) return false;
				var bytes = new byte[86];
				using var stream = new FileStream(new SafeFileHandle((IntPtr)IosRuntimeUnixNative.Dup(descriptor), true), FileAccess.Read, 81920, false);
				if (stream.Read(bytes, 0, bytes.Length) != bytes.Length || stream.ReadByte() != -1 ||
					System.Text.Encoding.ASCII.GetString(bytes, 0, 4) != "NHRT" || bytes[4] != 1 ||
					bytes[5] is not (1 or 2) || !Guid.TryParseExact(System.Text.Encoding.ASCII.GetString(bytes, 6, 32), "N", out nonce) || nonce == Guid.Empty ||
					!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes.AsSpan(0, 54)), bytes.AsSpan(54, 32))) return false;
				state = bytes[5];
				device = BitConverter.ToUInt64(bytes, 38);
				inode = BitConverter.ToUInt64(bytes, 46);
				return true;
			}
			catch (Exception exception) when (IsBoundaryException(exception)) { return false; }
			finally { IosRuntimeUnixNative.Close(descriptor); }
		}

		// Open is the only recovery entry point that is permitted before an inner
		// import.lock exists.  It serializes on the fixed outer lock and accepts
		// only the two durable marker states written by reset.
		static bool TryConvergeOuterResetAtOpen(int parentDescriptor,
			IosUnixFileIdentity expectedParent, IosUnixFileLayout layout, string leafName)
		{
			if (!IosRuntimeUnixNative.TryReadIdentity(parentDescriptor, layout, out var actualParent) ||
				!actualParent.IsDirectory || !actualParent.SameNodeAndType(expectedParent) ||
				!TryAcquireOuterResetLockAt(parentDescriptor, layout, out var outerLock))
				return false;
			using (outerLock)
			{
				var markerDescriptor = IosRuntimeUnixNative.OpenAt(parentDescriptor,
					".retail-import-reset.transaction", RetailDurableUnixNative.OpenReadWriteFlags, 0);
				if (markerDescriptor < 0)
					return Marshal.GetLastWin32Error() == UnixMissingEntry;
				IosRuntimeUnixNative.Close(markerDescriptor);
				if (!TryReadResetMarker(parentDescriptor, layout, out var nonce, out var state, out var device, out var inode) ||
					HasCaseAlias(parentDescriptor, ".retail-import-reset.transaction") ||
					HasCaseAlias(parentDescriptor, leafName)) return false;
				var root = IosRuntimeUnixNative.OpenAt(parentDescriptor, leafName, layout.DirectoryFlags, 0);
				if (root >= 0)
				{
					try
					{
						if (state != 1 || !IosRuntimeUnixNative.TryReadIdentity(root, layout, out var identity) ||
							!identity.IsDirectory || identity.Device != device || identity.Inode != inode)
							return false;
					}
					finally { IosRuntimeUnixNative.Close(root); }
					return RetailDurableUnixNative.UnlinkAt(parentDescriptor,
						".retail-import-reset.transaction", 0) == 0 && RetailDurableUnixNative.Sync(parentDescriptor) == 0;
				}
				if (Marshal.GetLastWin32Error() != UnixMissingEntry) return false;
				var sibling = ResetSiblingName(nonce);
				if (HasCaseAlias(parentDescriptor, sibling)) return false;
				var quarantined = IosRuntimeUnixNative.OpenAt(parentDescriptor, sibling, layout.DirectoryFlags, 0);
				if (quarantined < 0) return false;
				try
				{
					if (!IosRuntimeUnixNative.TryReadIdentity(quarantined, layout, out var identity) ||
						!identity.IsDirectory || identity.Device != device || identity.Inode != inode ||
						!PurgePinnedDirectoryAt(quarantined, layout) ||
						RetailDurableUnixNative.UnlinkAt(parentDescriptor, sibling,
							RetailDurableUnixNative.RemoveDirectoryFlag) != 0)
						return false;
				}
				finally { IosRuntimeUnixNative.Close(quarantined); }
				return RetailDurableUnixNative.MkdirAt(parentDescriptor, leafName, UnixOwnerDirectoryMode) == 0 &&
					RetailDurableUnixNative.Sync(parentDescriptor) == 0 &&
					RetailDurableUnixNative.UnlinkAt(parentDescriptor, ".retail-import-reset.transaction", 0) == 0 &&
					RetailDurableUnixNative.Sync(parentDescriptor) == 0;
			}
		}

		static bool TryAcquireOuterResetLockAt(int parentDescriptor, IosUnixFileLayout layout, out FileStream stream)
		{
			stream = null;
			if (HasCaseAlias(parentDescriptor, ".retail-import-reset.lock")) return false;
			var descriptor = IosRuntimeUnixNative.OpenAt(parentDescriptor, ".retail-import-reset.lock",
				RetailDurableUnixNative.OpenReadWriteFlags, 0);
			if (descriptor < 0 && Marshal.GetLastWin32Error() == UnixMissingEntry)
			{
				descriptor = IosRuntimeUnixNative.OpenAt(parentDescriptor, ".retail-import-reset.lock",
					RetailDurableUnixNative.CreateNewReadWriteFlags, UnixOwnerFileMode);
				if (descriptor >= 0 && (RetailDurableUnixNative.ChangeMode(descriptor, UnixOwnerFileMode) != 0 ||
					RetailDurableUnixNative.Sync(parentDescriptor) != 0)) { IosRuntimeUnixNative.Close(descriptor); return false; }
			}
			if (descriptor < 0 || !IosRuntimeUnixNative.TryReadIdentity(descriptor, layout, out var identity) || !IsSingleLinkedRegular(identity))
			{
				if (descriptor >= 0) IosRuntimeUnixNative.Close(descriptor);
				return false;
			}
			try
			{
				stream = new FileStream(new SafeFileHandle((IntPtr)descriptor, true), FileAccess.ReadWrite, 81920, false);
				descriptor = -1;
				if (RetailImportProcessLockNative.TryAcquire(stream))
					return true;
				stream.Dispose();
				stream = null;
				return false;
			}
			catch { stream?.Dispose(); stream = null; return false; }
			finally { if (descriptor >= 0) IosRuntimeUnixNative.Close(descriptor); }
		}

		static bool PurgePinnedDirectoryAt(int directory, IosUnixFileLayout layout)
		{
			foreach (var name in RetailDurableUnixNative.EnumerateDirectoryNames(directory))
			{
				if (name is "." or ".." || HasCaseAlias(directory, name)) return false;
				var child = IosRuntimeUnixNative.OpenAt(directory, name, layout.FileFlags, 0);
				if (child < 0)
				{
					// FileFlags refuses symlinks.  Unlink the directory entry itself so
					// recovery never follows a hostile target outside the subtree.
					if (RetailDurableUnixNative.UnlinkAt(directory, name, 0) != 0) return false;
					continue;
				}
				try
				{
					if (!IosRuntimeUnixNative.TryReadIdentity(child, layout, out var identity)) return false;
					if (identity.IsDirectory)
					{
						if (!PurgePinnedDirectoryAt(child, layout) || RetailDurableUnixNative.UnlinkAt(directory,
							name, RetailDurableUnixNative.RemoveDirectoryFlag) != 0) return false;
					}
					else if (RetailDurableUnixNative.UnlinkAt(directory, name, 0) != 0) return false;
				}
				finally { IosRuntimeUnixNative.Close(child); }
			}
			return RetailDurableUnixNative.Sync(directory) == 0;
		}

		internal RetailOperationResult EnsureDirectory(RetailImportProcessLockLease liveLease,
			RetailImportPaths.V1PathToken directory, CancellationToken cancellationToken)
		{
			if (!HasLiveTypedLease(liveLease) || !IsDirectoryToken(directory)) return RetailOperationResult.Failed(TypedIoFailure());
			return EnsureDirectoryCore(directory, cancellationToken);
		}

		// Artifact tokens carry the only trusted projection of their nested parent.
		// The transaction never derives a directory name from a relative path.
		internal RetailOperationResult EnsureArtifactParentDirectory(RetailImportProcessLockLease liveLease,
			RetailImportPaths.V1PathToken artifact, CancellationToken cancellationToken)
		{
			if (!HasLiveTypedLease(liveLease) || artifact == null || artifact.Kind is not
				(RetailImportV1PathKind.GenerationArtifact or RetailImportV1PathKind.PendingStagingArtifact))
				return RetailOperationResult.Failed(TypedIoFailure());
			return EnsureDirectoryCore(artifact, cancellationToken, artifactParent: true);
		}

		internal RetailOperationResult WriteAtomic(RetailImportProcessLockLease liveLease,
			RetailImportPaths.V1PathToken document, byte[] payload, CancellationToken cancellationToken)
		{
			if (!HasLiveTypedLease(liveLease) || !IsWritableDocumentToken(document)) return RetailOperationResult.Failed(TypedIoFailure());
			return WriteAtomicCore(document, payload, cancellationToken);
		}

		// A quarantined directory is moved with the descriptor pair retained by
		// Enumerate.  In particular, this does not reopen the enumerated source by
		// pathname after an attacker has had an opportunity to replace it.
		internal RetailOperationResult<RetailImportDirectoryEntry> Quarantine(
			RetailImportProcessLockLease liveLease, RetailImportDirectoryEntry entry, Guid nonce,
			RetailImportFaultInjection faults)
		{
			if (!HasLiveTypedLease(liveLease) || entry == null || nonce == Guid.Empty || faults == null || !useUnixDescriptors)
				return RetailOperationResult<RetailImportDirectoryEntry>.Failed(TypedIoFailure());
			if (!entry.TryTakePinnedDirectory(out var sourceToken, out var sourceParent, out var child))
				return RetailOperationResult<RetailImportDirectoryEntry>.Failed(TypedIoFailure());
			var destinationParent = -1;
			try
			{
				if (!IosRuntimeUnixNative.TryReadIdentity(child, unixLayout, out var childIdentity) ||
					!childIdentity.IsDirectory)
					return RetailOperationResult<RetailImportDirectoryEntry>.Failed(TypedIoFailure());
				var sourceName = pathResolver.Resolve(sourceToken).Split('/').Last();
				var destination = RetailImportPaths.QuarantineEntryPath(nonce);
				var destinationParts = pathResolver.Resolve(destination).Split('/');
				if (!TryOpenPinnedUnixDirectory(destinationParts.Take(destinationParts.Length - 1).ToArray(),
					out destinationParent, out _) || HasCaseAlias(destinationParent, destinationParts[^1]) ||
					RetailDurableUnixNative.RenameAt(sourceParent, sourceName, destinationParent,
						destinationParts[^1]) != 0 ||
					RetailDurableUnixNative.Sync(sourceParent) != 0 ||
					RetailDurableUnixNative.Sync(destinationParent) != 0)
					return RetailOperationResult<RetailImportDirectoryEntry>.Failed(TypedIoFailure());

				var retainedDestinationParent = IosRuntimeUnixNative.Dup(destinationParent);
				if (retainedDestinationParent < 0)
					return RetailOperationResult<RetailImportDirectoryEntry>.Failed(TypedIoFailure());
				destinationParent = -1;
				var result = new RetailImportDirectoryEntry(destination,
					RetailImportDirectoryEntryKind.Directory, retainedDestinationParent, child);
				child = -1;
				faults.Hit(RetailImportFailpoint.QuarantineEntryRenamed);
				return RetailOperationResult<RetailImportDirectoryEntry>.Success(result);
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return RetailOperationResult<RetailImportDirectoryEntry>.Failed(TypedIoFailure());
			}
			finally
			{
				if (child >= 0) IosRuntimeUnixNative.Close(child);
				if (sourceParent >= 0) IosRuntimeUnixNative.Close(sourceParent);
				if (destinationParent >= 0) IosRuntimeUnixNative.Close(destinationParent);
			}
		}

		// Purge is intentionally available only for an already quarantined, pinned
		// entry.  It walks descriptor-relative children and never follows a link or
		// converts a child name into an absolute path.
		internal RetailOperationResult Purge(RetailImportProcessLockLease liveLease,
			RetailImportDirectoryEntry entry, RetailImportFaultInjection faults)
		{
			if (!HasLiveTypedLease(liveLease) || entry == null || faults == null || !useUnixDescriptors ||
				!entry.TryTakePinnedDirectory(out var token, out var parent, out var directory))
				return RetailOperationResult.Failed(TypedIoFailure());
			try
			{
				faults.Hit(RetailImportFailpoint.QuarantinePurgeStarted);
				if (token.Kind != RetailImportV1PathKind.QuarantineEntry ||
					!PurgePinnedDirectory(directory))
					return RetailOperationResult.Failed(TypedIoFailure());
				// Darwin does not permit rmdir through the parent while this directory
				// remains open.  It has already been recursively authenticated and purged.
				IosRuntimeUnixNative.Close(directory);
				directory = -1;
				var name = pathResolver.Resolve(token).Split('/').Last();
				if (RetailDurableUnixNative.UnlinkAt(parent, name,
					RetailDurableUnixNative.RemoveDirectoryFlag) != 0 ||
					RetailDurableUnixNative.Sync(parent) != 0)
					return RetailOperationResult.Failed(TypedIoFailure());
				return RetailOperationResult.Success();
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return RetailOperationResult.Failed(TypedIoFailure());
			}
			finally
			{
				if (directory >= 0) IosRuntimeUnixNative.Close(directory);
				if (parent >= 0) IosRuntimeUnixNative.Close(parent);
			}
		}

		bool PurgePinnedDirectory(int directory)
		{
			foreach (var name in RetailDurableUnixNative.EnumerateDirectoryNames(directory))
			{
				if (name is "." or ".." || HasCaseAlias(directory, name)) return false;
				var child = IosRuntimeUnixNative.OpenAt(directory, name, unixLayout.FileFlags, 0);
				if (child < 0) return false;
				try
				{
					if (!IosRuntimeUnixNative.TryReadIdentity(child, unixLayout, out var identity)) return false;
					if (identity.IsDirectory)
					{
						if (!PurgePinnedDirectory(child) || RetailDurableUnixNative.UnlinkAt(directory,
							name, RetailDurableUnixNative.RemoveDirectoryFlag) != 0) return false;
					}
					else if (!IsSingleLinkedRegular(identity) || RetailDurableUnixNative.UnlinkAt(directory,
						name, 0) != 0) return false;
				}
				finally { IosRuntimeUnixNative.Close(child); }
			}
			return RetailDurableUnixNative.Sync(directory) == 0;
		}

		internal async Task<RetailOperationResult<RetailImportDirectoryEntry>> MaterializeCreateNewAsync(
			RetailImportProcessLockLease liveLease, RetailImportPaths.V1PathToken destination,
			RetailPendingFaultInjection pendingFaultInjection, Stream ownedVerifiedSource, long expectedLength,
			string expectedSha256, RetailValidationDeadline deadline, IRetailMonotonicClock clock,
			CancellationToken cancellationToken, IProgress<RetailImportProgress> progress)
		{
			if (!HasLiveTypedLease(liveLease) || destination == null || destination.Kind is not
				(RetailImportV1PathKind.GenerationArtifact or RetailImportV1PathKind.PendingStagingArtifact) ||
				ownedVerifiedSource == null || expectedLength < 0 || clock == null || !deadline.MatchesClock(clock))
				return RetailOperationResult<RetailImportDirectoryEntry>.Failed(TypedIoFailure());
			try
			{
				RetailImportPaths.ValidateSha256(expectedSha256, nameof(expectedSha256));
				deadline.ThrowIfExpired(clock);
				var relative = pathResolver.Resolve(destination);
				if (!useUnixDescriptors)
					return RetailOperationResult<RetailImportDirectoryEntry>.Failed(TypedIoFailure());
				var components = relative.Split('/');
				if (!TryOpenPinnedUnixDirectory(components.Take(components.Length - 1).ToArray(), out var parent, out _))
					return RetailOperationResult<RetailImportDirectoryEntry>.Failed(TypedIoFailure());
				var descriptor = -1;
				var created = false;
				try
				{
					descriptor = RetailDurableUnixNative.OpenAt(parent, components[^1],
						RetailDurableUnixNative.CreateNewWriteFlags, UnixOwnerFileMode);
					if (descriptor < 0)
						return RetailOperationResult<RetailImportDirectoryEntry>.Failed(TypedIoFailure());
					created = true;
					if (RetailDurableUnixNative.ChangeMode(descriptor, UnixOwnerFileMode) != 0)
						throw new IOException("The materialized artifact permissions could not be sealed.");
					deadline.ThrowIfExpired(clock);
					var duplicate = IosRuntimeUnixNative.Dup(descriptor);
					if (duplicate < 0) throw new IOException("Could not duplicate materialization descriptor.");
					using var handle = new SafeFileHandle((IntPtr)duplicate, ownsHandle: true);
					// openat descriptors are synchronous file descriptors; marking this
					// wrapper asynchronous makes FileStream reject the SafeFileHandle.
					using var output = new FileStream(handle, FileAccess.Write, 81920, isAsync: false);
				using var hash = SHA256.Create();
				var buffer = new byte[81920]; long total = 0; int count;
				while (true)
				{
					deadline.ThrowIfExpired(clock);
					count = await ownedVerifiedSource.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
					deadline.ThrowIfExpired(clock);
					if (count == 0) break;
					if (checked(total + count) > expectedLength) throw new InvalidDataException("Artifact length exceeds its receipt.");
					deadline.ThrowIfExpired(clock);
					output.Write(buffer, 0, count);
					deadline.ThrowIfExpired(clock);
					deadline.ThrowIfExpired(clock);
					hash.TransformBlock(buffer, 0, count, null, 0); total += count;
					deadline.ThrowIfExpired(clock);
					progress?.Report(new RetailImportProgress(RetailImportPhase.Copying, null, 0, null,
						total, expectedLength, null));
				}
				deadline.ThrowIfExpired(clock);
				hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
				deadline.ThrowIfExpired(clock);
				deadline.ThrowIfExpired(clock);
					output.Flush();
				deadline.ThrowIfExpired(clock);
				output.Flush(flushToDisk: true);
				deadline.ThrowIfExpired(clock);
				if (total != expectedLength || !StringComparer.Ordinal.Equals(Convert.ToHexString(hash.Hash).ToLowerInvariant(), expectedSha256)) throw new InvalidDataException("Artifact receipt mismatch.");
				if (destination.Kind == RetailImportV1PathKind.PendingStagingArtifact)
					pendingFaultInjection?.Hit(RetailPendingFailpoint.StagingArtifactFlushed);
				deadline.ThrowIfExpired(clock);
				if (RetailDurableUnixNative.Sync(parent) != 0) throw new IOException("Could not sync materialization parent.");
				deadline.ThrowIfExpired(clock);
				return RetailOperationResult<RetailImportDirectoryEntry>.Success(new RetailImportDirectoryEntry(destination, RetailImportDirectoryEntryKind.RegularFile));
				}
				catch
				{
					if (created && (RetailDurableUnixNative.UnlinkAt(parent, components[^1], 0) != 0 ||
						RetailDurableUnixNative.Sync(parent) != 0))
						throw new IOException("Materialization cleanup could not be made durable.");
					throw;
				}
				finally
				{
					if (descriptor >= 0) IosRuntimeUnixNative.Close(descriptor);
					IosRuntimeUnixNative.Close(parent);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return RetailOperationResult<RetailImportDirectoryEntry>.Failed(Cancelled().Failure); }
			catch (TimeoutException) { return RetailOperationResult<RetailImportDirectoryEntry>.Failed(new RetailImportFailure(RetailImportErrorCode.ValidationTimedOut, null, null, RetailImportDiagnosticCode.DeadlineExceeded)); }
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
				InvalidDataException or OverflowException or ArgumentException or NotSupportedException) { return RetailOperationResult<RetailImportDirectoryEntry>.Failed(TypedIoFailure()); }
		}

		bool HasLiveTypedLease(RetailImportProcessLockLease lease) => !disposed && lease != null && lease.IsBoundTo(this);
		static bool IsDirectoryToken(RetailImportPaths.V1PathToken token) => token != null && token.Kind is RetailImportV1PathKind.GenerationsDirectory or RetailImportV1PathKind.QuarantineDirectory or RetailImportV1PathKind.PendingDirectory or RetailImportV1PathKind.PendingItemsDirectory or RetailImportV1PathKind.GenerationDirectory or RetailImportV1PathKind.GenerationContentDirectory or RetailImportV1PathKind.PendingStagingDirectory or RetailImportV1PathKind.PendingStagingContentDirectory or RetailImportV1PathKind.PendingSealedDirectory or RetailImportV1PathKind.PendingSealedContentDirectory or RetailImportV1PathKind.PendingTombstone;
		static bool IsSingleLinkedRegular(IosUnixFileIdentity identity) => identity.IsRegular && identity.LinkCount == 1;
		static bool IsReadableFileToken(RetailImportPaths.V1PathToken token) => token != null && token.Kind is RetailImportV1PathKind.ActivePointer or RetailImportV1PathKind.PublishJournal or RetailImportV1PathKind.GenerationCatalog or RetailImportV1PathKind.GenerationReceipts or RetailImportV1PathKind.GenerationArtifact or RetailImportV1PathKind.PendingStagingArtifact or RetailImportV1PathKind.PendingSealedArtifact or RetailImportV1PathKind.PendingStagingSeal or RetailImportV1PathKind.PendingSeal or RetailImportV1PathKind.PendingConsumeMarker or RetailImportV1PathKind.PendingLegacyMigrationMarker;
		static bool IsWritableDocumentToken(RetailImportPaths.V1PathToken token) => token != null && token.Kind is RetailImportV1PathKind.ActivePointer or RetailImportV1PathKind.PublishJournal or RetailImportV1PathKind.GenerationCatalog or RetailImportV1PathKind.GenerationReceipts or RetailImportV1PathKind.PendingStagingSeal or RetailImportV1PathKind.PendingLegacyMigrationMarker;
		internal static RetailImportFailure TypedIoFailure() => new(RetailImportErrorCode.SanitizedIoFailure, null, null, RetailImportDiagnosticCode.None);

		public void Dispose()
		{
			var descriptor = -1;
			var parentDescriptor = -1;
			lock (sync)
			{
				if (disposed)
					return;

				disposed = true;
				descriptor = rootDescriptor;
				rootDescriptor = -1;
				parentDescriptor = outerParentDescriptor;
			}

			if (descriptor >= 0)
				IosRuntimeUnixNative.Close(descriptor);
			if (parentDescriptor >= 0)
				IosRuntimeUnixNative.Close(parentDescriptor);
		}

		RetailOperationResult<FileStream> OpenUnixProcessLockFile()
		{
			if (rootDescriptor < 0 ||
				!IosRuntimeUnixNative.TryReadIdentity(rootDescriptor, unixLayout, out var currentRoot) ||
				!currentRoot.IsDirectory || !currentRoot.SameNodeAndType(rootIdentity))
				return RetailOperationResult<FileStream>.Failed(IoFailure());

			if (HasCaseAlias(rootDescriptor, "import.lock"))
				return RetailOperationResult<FileStream>.Failed(IoFailure());

			var descriptor = IosRuntimeUnixNative.OpenAt(
				rootDescriptor,
				"import.lock",
				RetailDurableUnixNative.OpenReadWriteFlags,
				0);
			var created = false;
			if (descriptor < 0 && Marshal.GetLastWin32Error() == UnixMissingEntry)
			{
				descriptor = IosRuntimeUnixNative.OpenAt(
					rootDescriptor,
					"import.lock",
					RetailDurableUnixNative.CreateNewReadWriteFlags,
					UnixOwnerFileMode);
				created = descriptor >= 0;
			}
			if (created && RetailDurableUnixNative.ChangeMode(descriptor, UnixOwnerFileMode) != 0)
			{
				IosRuntimeUnixNative.Close(descriptor);
				return RetailOperationResult<FileStream>.Failed(IoFailure());
			}
			if (descriptor < 0 ||
				!IosRuntimeUnixNative.TryReadIdentity(descriptor, unixLayout, out var identity) ||
				!IsSingleLinkedRegular(identity))
			{
				if (descriptor >= 0)
					IosRuntimeUnixNative.Close(descriptor);
				return RetailOperationResult<FileStream>.Failed(IoFailure());
			}

			var handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
			try
			{
				return RetailOperationResult<FileStream>.Success(
					new FileStream(handle, FileAccess.ReadWrite, 81920, isAsync: false));
			}
			catch
			{
				handle.Dispose();
				throw;
			}
		}

		RetailOperationResult<FileStream> OpenManagedProcessLockFile()
		{
			return RetailOperationResult<FileStream>.Failed(IoFailure());
		}

		RetailOperationResult EnsureUnixDirectory(
			string relativePath,
			CancellationToken cancellationToken,
			ref bool published)
		{
			var components = relativePath.Split('/');
			var parentComponents = components.Take(components.Length - 1).ToArray();
			var parentDescriptor = -1;
			var directoryDescriptor = -1;
			try
			{
				if (!TryOpenPinnedUnixDirectory(parentComponents, out parentDescriptor, out var parentIdentity))
					return FailedIo();

				var directoryName = components[^1];
				directoryDescriptor = IosRuntimeUnixNative.OpenAt(
					parentDescriptor, directoryName, unixLayout.DirectoryFlags, 0);
				if (directoryDescriptor >= 0)
				{
					if (!IosRuntimeUnixNative.TryReadIdentity(directoryDescriptor, unixLayout, out var existing) ||
						!existing.IsDirectory)
						return FailedIo();

					var existingPolicy = ApplyStoragePolicy(
						RetailImportPaths.ResolveUnderRoot(displayRoot, relativePath),
						recursive: true,
						observeCancellation: true,
						cancellationToken);
					if (!VerifyUnixParentBinding(parentComponents, parentIdentity) ||
						!IsExpectedUnixDirectory(parentDescriptor, directoryName, existing))
						return existingPolicy.Succeeded ? FailedIo() : existingPolicy;

					return existingPolicy;
				}

				if (Marshal.GetLastWin32Error() != UnixMissingEntry)
					return FailedIo();

				cancellationToken.ThrowIfCancellationRequested();
				if (RetailDurableUnixNative.MkdirAt(
					parentDescriptor, directoryName, UnixOwnerDirectoryMode) != 0)
					return FailedIo();

				published = true;
				RetailOperationResult firstFailure = null;
				var created = default(IosUnixFileIdentity);
				var createdIsSafe = false;
				directoryDescriptor = IosRuntimeUnixNative.OpenAt(
					parentDescriptor, directoryName, unixLayout.DirectoryFlags, 0);
				if (directoryDescriptor < 0)
					PreserveFirstFailure(ref firstFailure, FailedIo());
				else
				{
					if (RetailDurableUnixNative.ChangeMode(
						directoryDescriptor, UnixOwnerDirectoryMode) != 0)
						PreserveFirstFailure(ref firstFailure, FailedIo());
					if (!IosRuntimeUnixNative.TryReadIdentity(
						directoryDescriptor, unixLayout, out created) || !created.IsDirectory)
						PreserveFirstFailure(ref firstFailure, FailedIo());
					else
						createdIsSafe = IsExpectedUnixDirectory(
							parentDescriptor, directoryName, created);
				}

				if (!createdIsSafe)
					PreserveFirstFailure(ref firstFailure, FailedIo());
				var parentPathIsBound = VerifyUnixParentBinding(parentComponents, parentIdentity);
				if (!parentPathIsBound)
					PreserveFirstFailure(ref firstFailure, FailedIo());

				var directoryPath = RetailImportPaths.ResolveUnderRoot(displayRoot, relativePath);
				if (createdIsSafe && parentPathIsBound)
					PreserveFirstFailure(ref firstFailure, ApplyStoragePolicy(
						directoryPath, recursive: true, observeCancellation: false, cancellationToken));

				parentPathIsBound = VerifyUnixParentBinding(parentComponents, parentIdentity);
				if (!parentPathIsBound)
					PreserveFirstFailure(ref firstFailure, FailedIo());
				if (createdIsSafe &&
					!IsExpectedUnixDirectory(parentDescriptor, directoryName, created))
					PreserveFirstFailure(ref firstFailure, FailedIo());

				var parentPath = Path.GetDirectoryName(directoryPath);
				var pinnedDurable = SyncPinnedDirectory(parentDescriptor);
				PreserveFirstFailure(ref firstFailure, pinnedDurable);
				var pathDurable = RetailOperationResult.Success();
				parentPathIsBound = VerifyUnixParentBinding(parentComponents, parentIdentity);
				if (!parentPathIsBound)
					PreserveFirstFailure(ref firstFailure, FailedIo());
				if (parentPathIsBound)
				{
					pathDurable = SyncDirectory(
						parentPath, observeCancellation: false, cancellationToken);
					PreserveFirstFailure(ref firstFailure, pathDurable);
				}

				if (pinnedDurable.Succeeded && pathDurable.Succeeded)
					PreserveFirstFailure(ref firstFailure, InvokeCheckpoint(
						RetailDurableWriteCheckpoint.AfterDirectorySync,
						observeCancellation: false,
						cancellationToken));
				if (!VerifyUnixParentBinding(parentComponents, parentIdentity) ||
					(createdIsSafe &&
						!IsExpectedUnixDirectory(parentDescriptor, directoryName, created)))
					PreserveFirstFailure(ref firstFailure, FailedIo());

				return firstFailure ?? RetailOperationResult.Success();
			}
			finally
			{
				if (directoryDescriptor >= 0)
					IosRuntimeUnixNative.Close(directoryDescriptor);
				if (parentDescriptor >= 0)
					IosRuntimeUnixNative.Close(parentDescriptor);
			}
		}

		RetailOperationResult EnsureManagedDirectory(
			string relativePath,
			CancellationToken cancellationToken,
			ref bool published)
		{
			var directoryPath = RetailImportPaths.ResolveUnderRoot(displayRoot, relativePath);
			var parent = Path.GetDirectoryName(directoryPath);
			SafeFileHandle parentHandle = null;
			try
			{
				if (!TryVerifyManagedDirectory(parent))
					return FailedIo();
				parentHandle = OpenManagedEntry(parent, directory: true);
				if (parentHandle == null || parentHandle.IsInvalid ||
					!TryReadManagedIdentity(parentHandle, out var parentIdentity) ||
					!parentIdentity.IsDirectory)
					return FailedIo();

				if (Directory.Exists(directoryPath))
				{
					if (!TryVerifyManagedDirectory(directoryPath) ||
						!TryCaptureManagedIdentity(directoryPath, directory: true, out var existingIdentity))
						return FailedIo();

					var existingPolicy = ApplyStoragePolicy(
						directoryPath, recursive: true, observeCancellation: true, cancellationToken);
					if (!VerifyManagedBinding(parent, directory: true, parentIdentity) ||
						!VerifyManagedBinding(directoryPath, directory: true, existingIdentity))
						return existingPolicy.Succeeded ? FailedIo() : existingPolicy;

					return existingPolicy;
				}

				cancellationToken.ThrowIfCancellationRequested();
				Directory.CreateDirectory(directoryPath);
				published = true;
				RetailOperationResult firstFailure = null;
				var createdIdentity = default(RetailManagedFileIdentity);
				var createdIsSafe = TryVerifyManagedDirectory(directoryPath) &&
					TryCaptureManagedIdentity(directoryPath, directory: true, out createdIdentity);
				if (!createdIsSafe)
					PreserveFirstFailure(ref firstFailure, FailedIo());
				var parentPathIsBound = VerifyManagedBinding(parent, directory: true, parentIdentity);
				if (!parentPathIsBound)
					PreserveFirstFailure(ref firstFailure, FailedIo());

				if (createdIsSafe && parentPathIsBound)
					PreserveFirstFailure(ref firstFailure, ApplyStoragePolicy(
						directoryPath, recursive: true, observeCancellation: false, cancellationToken));

				parentPathIsBound = VerifyManagedBinding(parent, directory: true, parentIdentity);
				if (!parentPathIsBound)
					PreserveFirstFailure(ref firstFailure, FailedIo());
				if (createdIsSafe &&
					!VerifyManagedBinding(directoryPath, directory: true, createdIdentity))
					PreserveFirstFailure(ref firstFailure, FailedIo());

				var pinnedDurable = SyncPinnedDirectory(parentHandle);
				PreserveFirstFailure(ref firstFailure, pinnedDurable);
				var pathDurable = RetailOperationResult.Success();
				parentPathIsBound = VerifyManagedBinding(parent, directory: true, parentIdentity);
				if (!parentPathIsBound)
					PreserveFirstFailure(ref firstFailure, FailedIo());
				if (parentPathIsBound)
				{
					pathDurable = SyncDirectory(
						parent, observeCancellation: false, cancellationToken);
					PreserveFirstFailure(ref firstFailure, pathDurable);
				}

				if (pinnedDurable.Succeeded && pathDurable.Succeeded)
					PreserveFirstFailure(ref firstFailure, InvokeCheckpoint(
						RetailDurableWriteCheckpoint.AfterDirectorySync,
						observeCancellation: false,
						cancellationToken));
				if (!VerifyManagedBinding(parent, directory: true, parentIdentity) ||
					(createdIsSafe &&
						!VerifyManagedBinding(directoryPath, directory: true, createdIdentity)))
					PreserveFirstFailure(ref firstFailure, FailedIo());

				return firstFailure ?? RetailOperationResult.Success();
			}
			finally
			{
				parentHandle?.Dispose();
			}
		}

		RetailOperationResult WriteAtomicUnix(
			string relativePath,
			byte[] payload,
			CancellationToken cancellationToken,
			ref bool published)
		{
			var components = relativePath.Split('/');
			var parentComponents = components.Take(components.Length - 1).ToArray();
			var parentDescriptor = -1;
			var temporaryDescriptor = -1;
			var temporaryName = default(string);
			var temporaryIdentity = default(IosUnixFileIdentity);
			var temporaryCreated = false;
			try
			{
				if (!TryOpenPinnedUnixDirectory(parentComponents, out parentDescriptor, out var parentIdentity) ||
					!IsSafeUnixTarget(parentDescriptor, components[^1]))
					return FailedIo();

				var parentRelative = string.Join('/', parentComponents);
				var parentPath = parentComponents.Length == 0 ?
					displayRoot : RetailImportPaths.ResolveUnderRoot(displayRoot, parentRelative);
				for (var attempt = 0; attempt < MaximumCreateAttempts; attempt++)
				{
					temporaryName = ".nhri-" + Guid.NewGuid().ToString("N") + ".tmp";
					temporaryDescriptor = RetailDurableUnixNative.OpenAt(
						parentDescriptor,
						temporaryName,
						RetailDurableUnixNative.CreateNewWriteFlags,
						UnixOwnerFileMode);
					if (temporaryDescriptor < 0)
					{
						if (Marshal.GetLastWin32Error() == RetailDurableUnixNative.EntryExists)
							continue;

						return FailedIo();
					}

					temporaryCreated = true;
					if (RetailDurableUnixNative.ChangeMode(
						temporaryDescriptor, UnixOwnerFileMode) != 0)
						return FailedIo();

					var streamDescriptor = IosRuntimeUnixNative.Dup(temporaryDescriptor);
					if (streamDescriptor < 0)
						return FailedIo();

					using var handle = new SafeFileHandle((IntPtr)streamDescriptor, ownsHandle: true);
					using var stream = new FileStream(handle, FileAccess.Write, 81920, isAsync: false);
					checkpoint?.Invoke(RetailDurableWriteCheckpoint.AfterTemporaryCreated);
					cancellationToken.ThrowIfCancellationRequested();
					stream.Write(payload, 0, payload.Length);
					stream.Flush(flushToDisk: true);
					if (!IosRuntimeUnixNative.TryReadIdentity(
						temporaryDescriptor, unixLayout, out temporaryIdentity) ||
						!temporaryIdentity.IsRegular)
						return FailedIo();

					checkpoint?.Invoke(RetailDurableWriteCheckpoint.AfterPayloadFlushed);
					cancellationToken.ThrowIfCancellationRequested();
					if (!IsExpectedUnixPayload(
						parentDescriptor, temporaryName, temporaryIdentity, payload))
						return FailedIo();

					break;
				}

				if (!temporaryCreated)
					return FailedIo();

				cancellationToken.ThrowIfCancellationRequested();
				if (!VerifyUnixParentBinding(parentComponents, parentIdentity) ||
					!IsExpectedUnixPayload(
						parentDescriptor, temporaryName, temporaryIdentity, payload))
					return FailedIo();

				var temporaryPath = Path.Combine(parentPath, temporaryName);
				var temporaryPolicy = ApplyStoragePolicy(
					temporaryPath, recursive: false, observeCancellation: true, cancellationToken);
				if (!temporaryPolicy.Succeeded)
					return temporaryPolicy;
				if (!VerifyUnixParentBinding(parentComponents, parentIdentity) ||
					!IsExpectedUnixPayload(
						parentDescriptor, temporaryName, temporaryIdentity, payload))
					return FailedIo();

				checkpoint?.Invoke(RetailDurableWriteCheckpoint.AfterTemporaryPolicyApplied);
				cancellationToken.ThrowIfCancellationRequested();
				if (!VerifyUnixParentBinding(parentComponents, parentIdentity) ||
					!IsExpectedUnixPayload(
						parentDescriptor, temporaryName, temporaryIdentity, payload))
					return FailedIo();

				checkpoint?.Invoke(RetailDurableWriteCheckpoint.BeforePublish);
				cancellationToken.ThrowIfCancellationRequested();
				if (!VerifyUnixParentBinding(parentComponents, parentIdentity) ||
					!IsExpectedUnixPayload(
						parentDescriptor, temporaryName, temporaryIdentity, payload) ||
					!IsSafeUnixTarget(parentDescriptor, components[^1]) ||
					RetailDurableUnixNative.RenameAt(
						parentDescriptor,
						temporaryName,
						parentDescriptor,
						components[^1]) != 0)
					return FailedIo();

				temporaryCreated = false;
				published = true;
				RetailOperationResult firstFailure = null;
				PreserveFirstFailure(ref firstFailure, InvokeCheckpoint(
					RetailDurableWriteCheckpoint.AfterPublish,
					observeCancellation: false,
					cancellationToken));
				var parentPathIsBound = VerifyUnixParentBinding(parentComponents, parentIdentity);
				if (!parentPathIsBound)
					PreserveFirstFailure(ref firstFailure, FailedIo());

				var targetInspection = InspectUnixPayload(
					parentDescriptor, components[^1], temporaryIdentity, payload);
				if (targetInspection != RetailPayloadInspection.Expected)
					PreserveFirstFailure(ref firstFailure, FailedIo());

				var targetPath = RetailImportPaths.ResolveUnderRoot(displayRoot, relativePath);
				if (parentPathIsBound && targetInspection != RetailPayloadInspection.Unsafe)
				{
					var targetPolicy = ApplyStoragePolicy(
						targetPath, recursive: false, observeCancellation: false, cancellationToken);
					PreserveFirstFailure(ref firstFailure, targetPolicy);
					if (targetPolicy.Succeeded)
						PreserveFirstFailure(ref firstFailure, InvokeCheckpoint(
							RetailDurableWriteCheckpoint.AfterPublishedPolicyApplied,
							observeCancellation: false,
							cancellationToken));
				}

				parentPathIsBound = VerifyUnixParentBinding(parentComponents, parentIdentity);
				if (!parentPathIsBound)
					PreserveFirstFailure(ref firstFailure, FailedIo());
				if (InspectUnixPayload(
					parentDescriptor, components[^1], temporaryIdentity, payload) !=
					RetailPayloadInspection.Expected)
					PreserveFirstFailure(ref firstFailure, FailedIo());

				PreserveFirstFailure(ref firstFailure, InvokeCheckpoint(
					RetailDurableWriteCheckpoint.BeforeDirectorySync,
					observeCancellation: false,
					cancellationToken));
				parentPathIsBound = VerifyUnixParentBinding(parentComponents, parentIdentity);
				if (!parentPathIsBound)
					PreserveFirstFailure(ref firstFailure, FailedIo());
				if (InspectUnixPayload(
					parentDescriptor, components[^1], temporaryIdentity, payload) !=
					RetailPayloadInspection.Expected)
					PreserveFirstFailure(ref firstFailure, FailedIo());

				var pinnedDurable = SyncPinnedDirectory(parentDescriptor);
				PreserveFirstFailure(ref firstFailure, pinnedDurable);
				var pathDurable = RetailOperationResult.Success();
				parentPathIsBound = VerifyUnixParentBinding(parentComponents, parentIdentity);
				if (!parentPathIsBound)
					PreserveFirstFailure(ref firstFailure, FailedIo());
				if (parentPathIsBound)
				{
					pathDurable = SyncDirectory(
						parentPath, observeCancellation: false, cancellationToken);
					PreserveFirstFailure(ref firstFailure, pathDurable);
				}

				if (!VerifyUnixParentBinding(parentComponents, parentIdentity) ||
					InspectUnixPayload(
						parentDescriptor, components[^1], temporaryIdentity, payload) !=
					RetailPayloadInspection.Expected)
					PreserveFirstFailure(ref firstFailure, FailedIo());
				if (pinnedDurable.Succeeded && pathDurable.Succeeded)
					PreserveFirstFailure(ref firstFailure, InvokeCheckpoint(
						RetailDurableWriteCheckpoint.AfterDirectorySync,
						observeCancellation: false,
						cancellationToken));
				if (!VerifyUnixParentBinding(parentComponents, parentIdentity) ||
					InspectUnixPayload(
						parentDescriptor, components[^1], temporaryIdentity, payload) !=
					RetailPayloadInspection.Expected)
					PreserveFirstFailure(ref firstFailure, FailedIo());

				return firstFailure ?? RetailOperationResult.Success();
			}
			finally
			{
				if (temporaryCreated && parentDescriptor >= 0 && temporaryName != null)
					RetailDurableUnixNative.UnlinkAt(parentDescriptor, temporaryName, 0);
				if (temporaryDescriptor >= 0)
					IosRuntimeUnixNative.Close(temporaryDescriptor);
				if (parentDescriptor >= 0)
					IosRuntimeUnixNative.Close(parentDescriptor);
			}
		}

		RetailOperationResult WriteAtomicManaged(
			string relativePath,
			byte[] payload,
			CancellationToken cancellationToken,
			ref bool published)
		{
			var targetPath = RetailImportPaths.ResolveUnderRoot(displayRoot, relativePath);
			var parentPath = Path.GetDirectoryName(targetPath);
			var temporaryPath = default(string);
			var temporaryIdentity = default(RetailManagedFileIdentity);
			SafeFileHandle parentHandle = null;
			FileStream temporaryStream = null;
			var temporaryCreated = false;
			try
			{
				if (!TryVerifyManagedDirectory(parentPath) || !IsSafeManagedTarget(targetPath))
					return FailedIo();
				parentHandle = OpenManagedEntry(parentPath, directory: true);
				if (parentHandle == null || parentHandle.IsInvalid ||
					!TryReadManagedIdentity(parentHandle, out var parentIdentity) ||
					!parentIdentity.IsDirectory)
					return FailedIo();

				for (var attempt = 0; attempt < MaximumCreateAttempts; attempt++)
				{
					temporaryPath = Path.Combine(
						parentPath, ".nhri-" + Guid.NewGuid().ToString("N") + ".tmp");
					try
					{
						temporaryStream = new FileStream(
							temporaryPath,
							FileMode.CreateNew,
							FileAccess.ReadWrite,
							FileShare.ReadWrite | FileShare.Delete,
							81920,
							FileOptions.WriteThrough);
					}
					catch (IOException) when (File.Exists(temporaryPath))
					{
						continue;
					}

					temporaryCreated = true;
					checkpoint?.Invoke(RetailDurableWriteCheckpoint.AfterTemporaryCreated);
					cancellationToken.ThrowIfCancellationRequested();
					temporaryStream.Write(payload, 0, payload.Length);
					temporaryStream.Flush(flushToDisk: true);
					if (!TryReadManagedIdentity(temporaryStream.SafeFileHandle, out temporaryIdentity) ||
						temporaryIdentity.IsDirectory)
						return FailedIo();

					checkpoint?.Invoke(RetailDurableWriteCheckpoint.AfterPayloadFlushed);
					cancellationToken.ThrowIfCancellationRequested();
					if (!VerifyManagedBinding(parentPath, directory: true, parentIdentity) ||
						InspectManagedPayload(temporaryPath, temporaryIdentity, payload) !=
						RetailPayloadInspection.Expected)
						return FailedIo();

					break;
				}

				if (!temporaryCreated)
					return FailedIo();

				cancellationToken.ThrowIfCancellationRequested();
				if (!VerifyManagedBinding(parentPath, directory: true, parentIdentity) ||
					InspectManagedPayload(temporaryPath, temporaryIdentity, payload) !=
					RetailPayloadInspection.Expected)
					return FailedIo();

				var temporaryPolicy = ApplyStoragePolicy(
					temporaryPath, recursive: false, observeCancellation: true, cancellationToken);
				if (!temporaryPolicy.Succeeded)
					return temporaryPolicy;
				if (!VerifyManagedBinding(parentPath, directory: true, parentIdentity) ||
					InspectManagedPayload(temporaryPath, temporaryIdentity, payload) !=
					RetailPayloadInspection.Expected)
					return FailedIo();

				checkpoint?.Invoke(RetailDurableWriteCheckpoint.AfterTemporaryPolicyApplied);
				cancellationToken.ThrowIfCancellationRequested();
				if (!VerifyManagedBinding(parentPath, directory: true, parentIdentity) ||
					InspectManagedPayload(temporaryPath, temporaryIdentity, payload) !=
					RetailPayloadInspection.Expected)
					return FailedIo();

				checkpoint?.Invoke(RetailDurableWriteCheckpoint.BeforePublish);
				cancellationToken.ThrowIfCancellationRequested();
				if (!VerifyManagedBinding(parentPath, directory: true, parentIdentity) ||
					InspectManagedPayload(temporaryPath, temporaryIdentity, payload) !=
					RetailPayloadInspection.Expected ||
					!IsSafeManagedTarget(targetPath))
					return FailedIo();

				File.Move(temporaryPath, targetPath, overwrite: true);
				temporaryCreated = false;
				published = true;
				RetailOperationResult firstFailure = null;
				PreserveFirstFailure(ref firstFailure, InvokeCheckpoint(
					RetailDurableWriteCheckpoint.AfterPublish,
					observeCancellation: false,
					cancellationToken));
				var parentPathIsBound = VerifyManagedBinding(
					parentPath, directory: true, parentIdentity);
				if (!parentPathIsBound)
					PreserveFirstFailure(ref firstFailure, FailedIo());

				var targetInspection = InspectManagedPayload(
					targetPath, temporaryIdentity, payload);
				if (targetInspection != RetailPayloadInspection.Expected)
					PreserveFirstFailure(ref firstFailure, FailedIo());

				if (parentPathIsBound && targetInspection != RetailPayloadInspection.Unsafe)
				{
					var targetPolicy = ApplyStoragePolicy(
						targetPath, recursive: false, observeCancellation: false, cancellationToken);
					PreserveFirstFailure(ref firstFailure, targetPolicy);
					if (targetPolicy.Succeeded)
						PreserveFirstFailure(ref firstFailure, InvokeCheckpoint(
							RetailDurableWriteCheckpoint.AfterPublishedPolicyApplied,
							observeCancellation: false,
							cancellationToken));
				}

				parentPathIsBound = VerifyManagedBinding(parentPath, directory: true, parentIdentity);
				if (!parentPathIsBound)
					PreserveFirstFailure(ref firstFailure, FailedIo());
				if (InspectManagedPayload(targetPath, temporaryIdentity, payload) !=
					RetailPayloadInspection.Expected)
					PreserveFirstFailure(ref firstFailure, FailedIo());

				PreserveFirstFailure(ref firstFailure, InvokeCheckpoint(
					RetailDurableWriteCheckpoint.BeforeDirectorySync,
					observeCancellation: false,
					cancellationToken));
				parentPathIsBound = VerifyManagedBinding(parentPath, directory: true, parentIdentity);
				if (!parentPathIsBound)
					PreserveFirstFailure(ref firstFailure, FailedIo());
				if (InspectManagedPayload(targetPath, temporaryIdentity, payload) !=
					RetailPayloadInspection.Expected)
					PreserveFirstFailure(ref firstFailure, FailedIo());

				var pinnedDurable = SyncPinnedDirectory(parentHandle);
				PreserveFirstFailure(ref firstFailure, pinnedDurable);
				var pathDurable = RetailOperationResult.Success();
				parentPathIsBound = VerifyManagedBinding(parentPath, directory: true, parentIdentity);
				if (!parentPathIsBound)
					PreserveFirstFailure(ref firstFailure, FailedIo());
				if (parentPathIsBound)
				{
					pathDurable = SyncDirectory(
						parentPath, observeCancellation: false, cancellationToken);
					PreserveFirstFailure(ref firstFailure, pathDurable);
				}

				if (!VerifyManagedBinding(parentPath, directory: true, parentIdentity) ||
					InspectManagedPayload(targetPath, temporaryIdentity, payload) !=
					RetailPayloadInspection.Expected)
					PreserveFirstFailure(ref firstFailure, FailedIo());
				if (pinnedDurable.Succeeded && pathDurable.Succeeded)
					PreserveFirstFailure(ref firstFailure, InvokeCheckpoint(
						RetailDurableWriteCheckpoint.AfterDirectorySync,
						observeCancellation: false,
						cancellationToken));
				if (!VerifyManagedBinding(parentPath, directory: true, parentIdentity) ||
					InspectManagedPayload(targetPath, temporaryIdentity, payload) !=
					RetailPayloadInspection.Expected)
					PreserveFirstFailure(ref firstFailure, FailedIo());

				return firstFailure ?? RetailOperationResult.Success();
			}
			finally
			{
				temporaryStream?.Dispose();
				parentHandle?.Dispose();
				if (temporaryCreated && temporaryPath != null)
				{
					try
					{
						File.Delete(temporaryPath);
					}
					catch (Exception exception) when (IsBoundaryException(exception))
					{
						// The primary typed result takes precedence over best-effort temp cleanup.
					}
				}
			}
		}

		RetailOperationResult ApplyStoragePolicy(
			string path,
			bool recursive,
			bool observeCancellation,
			CancellationToken cancellationToken)
		{
			try
			{
				if (observeCancellation)
					cancellationToken.ThrowIfCancellationRequested();

				var result = storagePolicy.ApplyAndVerify(path, recursive);
				if (result == null || !result.Succeeded)
					return FailedStoragePolicy();

				if (observeCancellation)
					cancellationToken.ThrowIfCancellationRequested();

				return RetailOperationResult.Success();
			}
			catch (OperationCanceledException) when (observeCancellation && cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return FailedStoragePolicy();
			}
		}

		RetailOperationResult InvokeCheckpoint(
			RetailDurableWriteCheckpoint point,
			bool observeCancellation,
			CancellationToken cancellationToken)
		{
			try
			{
				if (observeCancellation)
					cancellationToken.ThrowIfCancellationRequested();

				checkpoint?.Invoke(point);

				if (observeCancellation)
					cancellationToken.ThrowIfCancellationRequested();

				return RetailOperationResult.Success();
			}
			catch (OperationCanceledException) when (observeCancellation && cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return FailedIo();
			}
		}

		static void PreserveFirstFailure(
			ref RetailOperationResult firstFailure,
			RetailOperationResult result)
		{
			if (firstFailure == null && result != null && !result.Succeeded)
				firstFailure = result;
		}

		RetailOperationResult SyncDirectory(
			string directory,
			bool observeCancellation,
			CancellationToken cancellationToken)
		{
			try
			{
				if (observeCancellation)
					cancellationToken.ThrowIfCancellationRequested();

				var result = directoryDurability.Sync(directory);
				if (result == null || !result.Succeeded)
					return FailedIo();

				if (observeCancellation)
					cancellationToken.ThrowIfCancellationRequested();

				return RetailOperationResult.Success();
			}
			catch (OperationCanceledException) when (observeCancellation && cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return FailedIo();
			}
		}

		static RetailOperationResult SyncPinnedDirectory(int descriptor)
		{
			try
			{
				return descriptor >= 0 && RetailDurableUnixNative.Sync(descriptor) == 0 ?
					RetailOperationResult.Success() : FailedIo();
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return FailedIo();
			}
		}

		static RetailOperationResult SyncPinnedDirectory(SafeFileHandle handle)
		{
			try
			{
				if (handle == null || handle.IsInvalid)
					return FailedIo();

				if (IosRuntimeUnixNative.TryGetCurrentLayout(out _))
				{
					var raw = handle.DangerousGetHandle().ToInt64();
					return raw >= 0 && raw <= int.MaxValue &&
						RetailDurableUnixNative.Sync((int)raw) == 0 ?
						RetailOperationResult.Success() : FailedIo();
				}

				return OperatingSystem.IsWindows() && RetailDurableWindowsNative.Sync(handle) ?
					RetailOperationResult.Success() : FailedIo();
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return FailedIo();
			}
		}

		bool TryOpenPinnedUnixDirectory(
			string[] components,
			out int descriptor,
			out IosUnixFileIdentity identity)
		{
			descriptor = -1;
			identity = default;
			if (disposed || rootDescriptor < 0 ||
				!IosRuntimeUnixNative.TryReadIdentity(rootDescriptor, unixLayout, out var currentRoot) ||
				!currentRoot.IsDirectory || !currentRoot.SameNodeAndType(rootIdentity))
				return false;

			var current = IosRuntimeUnixNative.OpenAt(
				rootDescriptor, ".", unixLayout.DirectoryFlags, 0);
			if (current < 0)
				return false;

			try
			{
				foreach (var component in components)
				{
					if (HasCaseAlias(current, component))
						return false;
					var next = IosRuntimeUnixNative.OpenAt(
						current, component, unixLayout.DirectoryFlags, 0);
					if (next < 0 ||
						!IosRuntimeUnixNative.TryReadIdentity(next, unixLayout, out var parent) ||
						!parent.IsDirectory)
					{
						if (next >= 0)
							IosRuntimeUnixNative.Close(next);
						return false;
					}

					IosRuntimeUnixNative.Close(current);
					current = next;
				}

				if (!IosRuntimeUnixNative.TryReadIdentity(current, unixLayout, out identity) ||
					!identity.IsDirectory)
					return false;

				descriptor = current;
				current = -1;
				return true;
			}
			finally
			{
				if (current >= 0)
					IosRuntimeUnixNative.Close(current);
			}
		}

		bool VerifyUnixParentBinding(string[] components, IosUnixFileIdentity expected)
		{
			var current = -1;
			try
			{
				if (!TryOpenUnixDirectory(nativeRoot, unixLayout, out current, out var currentRoot) ||
					!currentRoot.SameNodeAndType(rootIdentity))
					return false;

				foreach (var component in components)
				{
					var next = IosRuntimeUnixNative.OpenAt(
						current, component, unixLayout.DirectoryFlags, 0);
					if (next < 0 ||
						!IosRuntimeUnixNative.TryReadIdentity(next, unixLayout, out var parent) ||
						!parent.IsDirectory)
					{
						if (next >= 0)
							IosRuntimeUnixNative.Close(next);
						return false;
					}

					IosRuntimeUnixNative.Close(current);
					current = next;
				}

				return IosRuntimeUnixNative.TryReadIdentity(current, unixLayout, out var actual) &&
					actual.IsDirectory && actual.SameNodeAndType(expected);
			}
			finally
			{
				if (current >= 0)
					IosRuntimeUnixNative.Close(current);
			}
		}

		static bool HasCaseAlias(int parentDescriptor, string expectedName)
		{
			try
			{
				foreach (var existing in RetailDurableUnixNative.EnumerateDirectoryNames(parentDescriptor))
					if (!StringComparer.Ordinal.Equals(existing, expectedName) &&
						StringComparer.OrdinalIgnoreCase.Equals(existing, expectedName))
						return true;
				return false;
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return true;
			}
		}

		bool IsSafeUnixTarget(int parentDescriptor, string name)
		{
			var descriptor = IosRuntimeUnixNative.OpenAt(
				parentDescriptor, name, unixLayout.FileFlags, 0);
			if (descriptor < 0)
				return Marshal.GetLastWin32Error() == UnixMissingEntry;

			try
			{
				return IosRuntimeUnixNative.TryReadIdentity(descriptor, unixLayout, out var identity) &&
					IsSingleLinkedRegular(identity);
			}
			finally
			{
				IosRuntimeUnixNative.Close(descriptor);
			}
		}

		bool IsExpectedUnixDirectory(
			int parentDescriptor,
			string name,
			IosUnixFileIdentity expectedIdentity)
		{
			var descriptor = IosRuntimeUnixNative.OpenAt(
				parentDescriptor, name, unixLayout.DirectoryFlags, 0);
			if (descriptor < 0)
				return false;

			try
			{
				return IosRuntimeUnixNative.TryReadIdentity(
					descriptor, unixLayout, out var identity) &&
					identity.IsDirectory && identity.SameNodeAndType(expectedIdentity);
			}
			finally
			{
				IosRuntimeUnixNative.Close(descriptor);
			}
		}

		bool IsExpectedUnixPayload(
			int parentDescriptor,
			string name,
			IosUnixFileIdentity expectedIdentity,
			byte[] expectedPayload) =>
			InspectUnixPayload(parentDescriptor, name, expectedIdentity, expectedPayload) ==
				RetailPayloadInspection.Expected;

		RetailPayloadInspection InspectUnixPayload(
			int parentDescriptor,
			string name,
			IosUnixFileIdentity expectedIdentity,
			byte[] expectedPayload)
		{
			var descriptor = IosRuntimeUnixNative.OpenAt(
				parentDescriptor, name, unixLayout.FileFlags, 0);
			if (descriptor < 0)
				return RetailPayloadInspection.Unsafe;

			try
			{
				if (!IosRuntimeUnixNative.TryReadIdentity(descriptor, unixLayout, out var identity) ||
					!IsSingleLinkedRegular(identity) || !identity.SameNodeAndType(expectedIdentity))
					return RetailPayloadInspection.Unsafe;
				if (identity.Length != expectedPayload.LongLength)
					return RetailPayloadInspection.Modified;

				using var handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
				descriptor = -1;
				using var stream = new FileStream(handle, FileAccess.Read, 81920, isAsync: false);
				return PayloadMatches(stream, expectedPayload) ?
					RetailPayloadInspection.Expected : RetailPayloadInspection.Modified;
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return RetailPayloadInspection.Unsafe;
			}
			finally
			{
				IosRuntimeUnixNative.Close(descriptor);
			}
		}

		static bool TryCaptureManagedIdentity(
			string path,
			bool directory,
			out RetailManagedFileIdentity identity)
		{
			identity = default;
			SafeFileHandle handle = null;
			try
			{
				handle = OpenManagedEntry(path, directory);
				return handle != null && !handle.IsInvalid &&
					TryReadManagedIdentity(handle, out identity) &&
					identity.IsDirectory == directory;
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return false;
			}
			finally
			{
				handle?.Dispose();
			}
		}

		static bool VerifyManagedBinding(
			string path,
			bool directory,
			RetailManagedFileIdentity expected) =>
			TryCaptureManagedIdentity(path, directory, out var actual) &&
				actual.SameNodeAndType(expected);

		static RetailPayloadInspection InspectManagedPayload(
			string path,
			RetailManagedFileIdentity expectedIdentity,
			byte[] expectedPayload)
		{
			SafeFileHandle handle = null;
			try
			{
				handle = OpenManagedEntry(path, directory: false);
				if (handle == null || handle.IsInvalid ||
					!TryReadManagedIdentity(handle, out var identity) || identity.IsDirectory ||
					!identity.SameNodeAndType(expectedIdentity))
					return RetailPayloadInspection.Unsafe;
				if (identity.Length != expectedPayload.LongLength)
					return RetailPayloadInspection.Modified;

				using var stream = new FileStream(handle, FileAccess.Read, 81920, isAsync: false);
				handle = null;
				return PayloadMatches(stream, expectedPayload) ?
					RetailPayloadInspection.Expected : RetailPayloadInspection.Modified;
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return RetailPayloadInspection.Unsafe;
			}
			finally
			{
				handle?.Dispose();
			}
		}

		static SafeFileHandle OpenManagedEntry(string path, bool directory)
		{
			if (IosRuntimeUnixNative.TryGetCurrentLayout(out var layout))
			{
				var descriptor = IosRuntimeUnixNative.Open(
					path, directory ? layout.DirectoryFlags : layout.FileFlags, 0);
				return descriptor < 0 ? null :
					new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
			}

			return OperatingSystem.IsWindows() ?
				RetailDurableWindowsNative.Open(path, directory) : null;
		}

		static bool TryReadManagedIdentity(
			SafeFileHandle handle,
			out RetailManagedFileIdentity identity)
		{
			identity = default;
			if (IosRuntimeUnixNative.TryGetCurrentLayout(out var layout))
			{
				var raw = handle.DangerousGetHandle().ToInt64();
				if (raw < 0 || raw > int.MaxValue ||
					!IosRuntimeUnixNative.TryReadIdentity((int)raw, layout, out var unixIdentity))
					return false;

				identity = new RetailManagedFileIdentity(
					unixIdentity.Device,
					unixIdentity.Inode,
					unixIdentity.IsDirectory,
					unixIdentity.Length);
				return unixIdentity.IsDirectory || unixIdentity.IsRegular;
			}

			return OperatingSystem.IsWindows() &&
				RetailDurableWindowsNative.TryReadIdentity(handle, out identity);
		}

		static bool PayloadMatches(Stream stream, byte[] expectedPayload)
		{
			var buffer = new byte[Math.Min(81920, Math.Max(1, expectedPayload.Length))];
			var offset = 0;
			while (offset < expectedPayload.Length)
			{
				var read = stream.Read(
					buffer, 0, Math.Min(buffer.Length, expectedPayload.Length - offset));
				if (read == 0)
					return false;

				for (var index = 0; index < read; index++)
					if (buffer[index] != expectedPayload[offset + index])
						return false;

				offset += read;
			}

			return stream.ReadByte() == -1;
		}

		static bool TryOpenUnixDirectory(
			string path,
			IosUnixFileLayout layout,
			out int descriptor,
			out IosUnixFileIdentity identity)
		{
			descriptor = -1;
			identity = default;
			if (string.IsNullOrEmpty(path) || path[0] != '/')
				return false;

			var current = IosRuntimeUnixNative.Open("/", layout.DirectoryFlags, 0);
			if (current < 0)
				return false;

			try
			{
				var components = path == "/" ? Array.Empty<string>() : path[1..].Split('/');
				foreach (var component in components)
				{
					if (string.IsNullOrEmpty(component) || component == "." || component == "..")
						return false;

					var next = IosRuntimeUnixNative.OpenAt(
						current, component, layout.DirectoryFlags, 0);
					if (next < 0 ||
						!IosRuntimeUnixNative.TryReadIdentity(next, layout, out var parent) ||
						!parent.IsDirectory)
					{
						if (next >= 0)
							IosRuntimeUnixNative.Close(next);
						return false;
					}

					IosRuntimeUnixNative.Close(current);
					current = next;
				}

				if (!IosRuntimeUnixNative.TryReadIdentity(current, layout, out identity) ||
					!identity.IsDirectory)
					return false;

				descriptor = current;
				current = -1;
				return true;
			}
			finally
			{
				if (current >= 0)
					IosRuntimeUnixNative.Close(current);
			}
		}

		static bool TryNormalizeRoot(string value, out string display, out string native)
		{
			display = null;
			native = null;
			if (string.IsNullOrEmpty(value) || !Path.IsPathFullyQualified(value) ||
				value.Contains('\0') ||
				value.Any(character => char.IsControl(character)))
				return false;

			display = Path.GetFullPath(value);
			native = NormalizeDarwinSystemAlias(display);
			return true;
		}

		static bool IsUnixPlatform() =>
			OperatingSystem.IsMacOS() || OperatingSystem.IsLinux() ||
			OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() ||
			OperatingSystem.IsMacCatalyst();

		static string NormalizeDarwinSystemAlias(string path)
		{
			if (!(OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() ||
				OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst()))
				return path;

			foreach (var alias in new[] { "/var", "/tmp", "/etc" })
				if (path == alias || path.StartsWith(alias + "/", StringComparison.Ordinal))
					return "/private" + path;

			return path;
		}

		static bool TryVerifyManagedDirectory(string path)
		{
			if (string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path))
				return false;

			var root = Path.GetPathRoot(path);
			if (string.IsNullOrEmpty(root))
				return false;

			var current = root;
			var relative = path[root.Length..];
			foreach (var component in relative.Split(
				new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
				StringSplitOptions.RemoveEmptyEntries))
			{
				current = Path.Combine(current, component);
				var directory = new DirectoryInfo(current);
				if (!directory.Exists || directory.LinkTarget != null)
					return false;
			}

			return true;
		}

		static bool IsSafeManagedTarget(string path)
		{
			var file = new FileInfo(path);
			if (file.LinkTarget != null)
				return false;

			return !Directory.Exists(path);
		}

		void ThrowIfDisposed()
		{
			if (disposed)
				throw new ObjectDisposedException(nameof(RetailImportDurableFileSystem));
		}

		static bool IsBoundaryException(Exception exception) =>
			exception is IOException || exception is UnauthorizedAccessException ||
			exception is ArgumentException || exception is NotSupportedException ||
			exception is ObjectDisposedException || exception is OverflowException ||
			exception is PlatformNotSupportedException || exception is SecurityException ||
			exception is OperationCanceledException;

		static RetailOperationResult<RetailImportDurableFileSystem> FailedFileSystem() =>
			RetailOperationResult<RetailImportDurableFileSystem>.Failed(IoFailure());

		static RetailOperationResult FailedIo() =>
			RetailOperationResult.Failed(IoFailure());

		static RetailOperationResult FailedStoragePolicy() =>
			RetailOperationResult.Failed(new RetailImportFailure(
				RetailImportErrorCode.StoragePolicyFailure,
				null,
				null,
				RetailImportDiagnosticCode.None));

		static RetailOperationResult Cancelled() =>
			RetailOperationResult.Failed(new RetailImportFailure(
				RetailImportErrorCode.Cancelled,
				null,
				null,
				RetailImportDiagnosticCode.None));

		static RetailImportFailure IoFailure() => new(
			RetailImportErrorCode.SanitizedIoFailure,
			null,
			null,
			RetailImportDiagnosticCode.None);
	}

	static class RetailDurableWindowsNative
	{
		const uint GenericRead = 0x80000000;
		const uint GenericWrite = 0x40000000;
		const uint ReadAttributes = 0x80;
		const uint ShareAll = 0x7;
		const uint OpenExisting = 3;
		const uint DirectoryAttribute = 0x10;
		const uint ReparsePointAttribute = 0x400;
		const uint BackupSemantics = 0x02000000;
		const uint OpenReparsePoint = 0x00200000;

		internal static SafeFileHandle Open(string path, bool directory) => CreateFile(
			path,
			directory ? GenericWrite | ReadAttributes : GenericRead | ReadAttributes,
			ShareAll,
			IntPtr.Zero,
			OpenExisting,
			OpenReparsePoint | (directory ? BackupSemantics : 0),
			IntPtr.Zero);

		internal static bool TryReadIdentity(
			SafeFileHandle handle,
			out RetailManagedFileIdentity identity)
		{
			identity = default;
			if (handle == null || handle.IsInvalid ||
				!GetFileInformationByHandle(handle, out var information) ||
				(information.FileAttributes & ReparsePointAttribute) != 0)
				return false;

			var isDirectory = (information.FileAttributes & DirectoryAttribute) != 0;
			identity = new RetailManagedFileIdentity(
				information.VolumeSerialNumber,
				((ulong)information.FileIndexHigh << 32) | information.FileIndexLow,
				isDirectory,
				isDirectory ? 0 : checked((long)(
					((ulong)information.FileSizeHigh << 32) | information.FileSizeLow)));
			return true;
		}

		internal static bool Sync(SafeFileHandle handle) => FlushFileBuffers(handle);

		[DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode,
			ExactSpelling = true, SetLastError = true)]
		static extern SafeFileHandle CreateFile(
			string name,
			uint access,
			uint share,
			IntPtr securityAttributes,
			uint creationDisposition,
			uint flagsAndAttributes,
			IntPtr templateFile);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool GetFileInformationByHandle(
			SafeFileHandle handle,
			out ByHandleFileInformation information);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool FlushFileBuffers(SafeFileHandle handle);

		[StructLayout(LayoutKind.Sequential)]
		struct ByHandleFileInformation
		{
			public uint FileAttributes;
			public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
			public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
			public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
			public uint VolumeSerialNumber;
			public uint FileSizeHigh;
			public uint FileSizeLow;
			public uint NumberOfLinks;
			public uint FileIndexHigh;
			public uint FileIndexLow;
		}
	}

	static class RetailDurableUnixNative
	{
		internal const int EntryExists = 17;
		internal static int RemoveDirectoryFlag =>
			OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() ||
			OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst() ? 0x80 : 0x200;
		const int DarwinCreateNewWriteFlags = 0x1000B01;
		const int LinuxCreateNewWriteFlags = 0xA00C1;

		internal static int CreateNewWriteFlags =>
			OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() ||
			OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst() ?
				DarwinCreateNewWriteFlags : LinuxCreateNewWriteFlags;

		internal static int OpenReadWriteFlags =>
			OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() ||
			OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst() ?
				0x1000106 : 0xA0802;

		internal static int CreateNewReadWriteFlags =>
			OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() ||
			OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst() ?
				0x1000B06 : 0xA08C2;

		internal static System.Collections.Generic.IEnumerable<string> EnumerateDirectoryNames(int descriptor)
		{
			var duplicate = IosRuntimeUnixNative.Dup(descriptor);
			if (duplicate < 0)
				throw new IOException("Could not duplicate directory descriptor.");
			var directory = FdOpenDir(duplicate);
			if (directory == IntPtr.Zero)
			{
				IosRuntimeUnixNative.Close(duplicate);
				throw new IOException("Could not open pinned directory.");
			}
			try
			{
				IntPtr entry;
				while ((entry = ReadDir(directory)) != IntPtr.Zero)
				{
					var offset = OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() ||
						OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst() ? 21 : 19;
					var bytes = new System.Collections.Generic.List<byte>();
					for (var index = 0; index < 256; index++)
					{
						var value = Marshal.ReadByte(entry, offset + index);
						if (value == 0) break;
						bytes.Add(value);
					}
					var name = System.Text.Encoding.UTF8.GetString(bytes.ToArray());
					if (name is not "." and not "..")
						yield return name;
				}
			}
			finally { CloseDir(directory); }
		}

		[DllImport("libc", EntryPoint = "openat", SetLastError = true)]
		internal static extern int OpenAt(int directory, string path, int flags, int mode);

		[DllImport("libc", EntryPoint = "mkdirat", SetLastError = true)]
		internal static extern int MkdirAt(int directory, string path, int mode);

		[DllImport("libc", EntryPoint = "fchmod", SetLastError = true)]
		internal static extern int ChangeMode(int descriptor, int mode);

		[DllImport("libc", EntryPoint = "renameat", SetLastError = true)]
		internal static extern int RenameAt(
			int oldDirectory,
			string oldPath,
			int newDirectory,
			string newPath);

		[DllImport("libc", EntryPoint = "unlinkat", SetLastError = true)]
		internal static extern int UnlinkAt(int directory, string path, int flags);

		[DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
		internal static extern int Sync(int descriptor);

		[DllImport("libc", EntryPoint = "fdopendir", SetLastError = true)]
		static extern IntPtr FdOpenDir(int descriptor);

		[DllImport("libc", EntryPoint = "readdir", SetLastError = true)]
		static extern IntPtr ReadDir(IntPtr directory);

		[DllImport("libc", EntryPoint = "closedir", SetLastError = true)]
		static extern int CloseDir(IntPtr directory);
	}
}
