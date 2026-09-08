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
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace OpenRA.Mods.RA2.Content
{
	interface IIosRuntimeResourceLease : IDisposable
	{
		string CanonicalRelativePath { get; }
		long Length { get; }
		Stream OpenVerifiedRead();
		void VerifyUnchanged();
	}

	interface IIosRuntimeResourceRoot : IDisposable
	{
		RetailOperationResult<IIosRuntimeResourceLease> AcquireRegularFile(
			string canonicalRelativePath);
	}

	sealed class IosRuntimeResourceRoot : IIosRuntimeResourceRoot
	{
		readonly object sync = new();
		IosRuntimeRootDirectoryOwner owner;

		IosRuntimeResourceRoot(IosRuntimeRootDirectoryOwner owner)
		{
			this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
		}

		internal static RetailOperationResult<IosRuntimeResourceRoot> OpenBundleRoot(
			string trustedBundleResourceRoot)
		{
			var descriptor = -1;
			try
			{
				if (!TryValidateTrustedRoot(trustedBundleResourceRoot, out var canonicalRoot) ||
					!IosRuntimeUnixNative.TryGetCurrentLayout(out var layout))
					return FailedRoot();

				descriptor = IosRuntimeUnixNative.Open(canonicalRoot, layout.DirectoryFlags, 0);
				if (descriptor < 0 ||
					!IosRuntimeUnixNative.TryReadIdentity(descriptor, layout, out var identity) ||
					!identity.IsDirectory)
					return FailedRoot();

				var root = new IosRuntimeResourceRoot(
					new IosRuntimeRootDirectoryOwner(descriptor, layout, identity));
				descriptor = -1;
				return RetailOperationResult<IosRuntimeResourceRoot>.Success(root);
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return FailedRoot();
			}
			finally
			{
				if (descriptor >= 0)
					IosRuntimeUnixNative.Close(descriptor);
			}
		}

		public RetailOperationResult<IIosRuntimeResourceLease> AcquireRegularFile(
			string canonicalRelativePath)
		{
			if (!IosRuntimeResourcePath.TrySplit(canonicalRelativePath, out var components))
				return FailedLease();

			IosRuntimeRootDirectoryOwner retained;
			lock (sync)
			{
				retained = owner;
				if (retained == null || !retained.TryRetain())
					return FailedLease();
			}

			return AcquireRegularFileRetained(
				retained, canonicalRelativePath, components);
		}

		internal RetailOperationResult<IosRuntimeManagedImageSource> CreateManagedImageSource()
		{
			try
			{
				return RetailOperationResult<IosRuntimeManagedImageSource>.Success(
					new IosRuntimeManagedImageSource(this));
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return RetailOperationResult<IosRuntimeManagedImageSource>.Failed(
					RuntimeResourceFailure());
			}
		}

		internal bool TryRetainOwner(out IosRuntimeRootDirectoryOwner retained)
		{
			lock (sync)
			{
				retained = owner;
				if (retained == null || !retained.TryRetain())
				{
					retained = null;
					return false;
				}

				return true;
			}
		}

		public void Dispose()
		{
			IosRuntimeRootDirectoryOwner release;
			lock (sync)
			{
				release = owner;
				owner = null;
			}

			release?.Release();
		}

		internal static RetailOperationResult<IIosRuntimeResourceLease> AcquireRegularFileRetained(
			IosRuntimeRootDirectoryOwner retained,
			string canonicalRelativePath,
			ImmutableArray<string> components)
		{
			var current = -1;
			var leaf = -1;
			var transferred = false;
			try
			{
				if (retained == null || components.IsDefault || components.Length == 0 ||
					!retained.TryOpenVerifiedRoot(out current, out var currentRoot))
					return FailedLease();

				var layout = retained.Layout;
				var parentIdentities = ImmutableArray.CreateBuilder<IosUnixFileIdentity>();
				parentIdentities.Add(currentRoot);
				for (var index = 0; index < components.Length - 1; index++)
				{
					var next = IosRuntimeUnixNative.OpenAt(
						current, components[index], layout.DirectoryFlags, 0);
					if (next < 0 ||
						!IosRuntimeUnixNative.TryReadIdentity(next, layout, out var parentIdentity) ||
						!parentIdentity.IsDirectory)
					{
						if (next >= 0)
							IosRuntimeUnixNative.Close(next);
						return FailedLease();
					}

					IosRuntimeUnixNative.Close(current);
					current = next;
					parentIdentities.Add(parentIdentity);
				}

				leaf = IosRuntimeUnixNative.OpenAt(
					current, components[^1], layout.FileFlags, 0);
				if (leaf < 0 ||
					!IosRuntimeUnixNative.TryReadIdentity(leaf, layout, out var leafIdentity) ||
					!leafIdentity.IsRegular || leafIdentity.Length < 0)
					return FailedLease();

				var lease = new IosRuntimeResourceLease(
					retained,
					canonicalRelativePath,
					components,
					parentIdentities.ToImmutable(),
					leafIdentity,
					leaf,
					layout);
				leaf = -1;
				transferred = true;
				return RetailOperationResult<IIosRuntimeResourceLease>.Success(lease);
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return FailedLease();
			}
			finally
			{
				if (leaf >= 0)
					IosRuntimeUnixNative.Close(leaf);
				if (current >= 0)
					IosRuntimeUnixNative.Close(current);
				if (!transferred)
					retained?.Release();
			}
		}

		static bool TryValidateTrustedRoot(string value, out string canonical)
		{
			canonical = null;
			if (string.IsNullOrEmpty(value) || value.Contains('\0') ||
				value.Any(character => char.IsControl(character)))
				return false;

			try
			{
				canonical = Path.GetFullPath(value);
				return Path.IsPathFullyQualified(canonical);
			}
			catch (Exception exception) when (IsBoundaryException(exception))
			{
				return false;
			}
		}

		internal static bool IsBoundaryException(Exception exception) =>
			exception is IOException || exception is UnauthorizedAccessException ||
			exception is ArgumentException || exception is NotSupportedException ||
			exception is ObjectDisposedException || exception is OverflowException;

		static RetailOperationResult<IosRuntimeResourceRoot> FailedRoot() =>
			RetailOperationResult<IosRuntimeResourceRoot>.Failed(RuntimeResourceFailure());

		static RetailOperationResult<IIosRuntimeResourceLease> FailedLease() =>
			RetailOperationResult<IIosRuntimeResourceLease>.Failed(RuntimeResourceFailure());

		internal static RetailImportFailure RuntimeResourceFailure() => new(
			RetailImportErrorCode.RuntimeResourcesMissing,
			null,
			null,
			RetailImportDiagnosticCode.InvalidMapPackage);
	}

	sealed class IosRuntimeResourceLease : IIosRuntimeResourceLease
	{
		readonly object sync = new();
		IosRuntimeRootDirectoryOwner owner;
		readonly ImmutableArray<string> components;
		readonly ImmutableArray<IosUnixFileIdentity> parentIdentities;
		readonly IosUnixFileIdentity leafIdentity;
		readonly IosUnixFileLayout layout;
		int leafDescriptor;
		bool streamOpened;

		public string CanonicalRelativePath { get; }
		public long Length => leafIdentity.Length;

		internal IosRuntimeResourceLease(
			IosRuntimeRootDirectoryOwner owner,
			string canonicalRelativePath,
			ImmutableArray<string> components,
			ImmutableArray<IosUnixFileIdentity> parentIdentities,
			IosUnixFileIdentity leafIdentity,
			int leafDescriptor,
			IosUnixFileLayout layout)
		{
			this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
			CanonicalRelativePath = canonicalRelativePath ??
				throw new ArgumentNullException(nameof(canonicalRelativePath));
			this.components = ImmutableArray.CreateRange(components);
			this.parentIdentities = ImmutableArray.CreateRange(parentIdentities);
			this.leafIdentity = leafIdentity;
			this.leafDescriptor = leafDescriptor >= 0 ? leafDescriptor :
				throw new ArgumentOutOfRangeException(nameof(leafDescriptor));
			this.layout = layout;
		}

		public Stream OpenVerifiedRead()
		{
			lock (sync)
			{
				if (leafDescriptor < 0)
					throw new ObjectDisposedException(nameof(IosRuntimeResourceLease));
				if (streamOpened)
					throw new InvalidOperationException("The verified resource stream is one-shot.");

				streamOpened = true;
				if (!IosRuntimeUnixNative.TryReadIdentity(leafDescriptor, layout, out var current) ||
					!current.IsRegular || !current.SameFile(leafIdentity))
					throw new IOException("The runtime resource identity changed.");

				var duplicate = IosRuntimeUnixNative.Dup(leafDescriptor);
				if (duplicate < 0)
					throw new IOException("The runtime resource descriptor could not be retained.");
				if (owner == null || !owner.TryRetain())
				{
					IosRuntimeUnixNative.Close(duplicate);
					throw new ObjectDisposedException(nameof(IosRuntimeResourceLease));
				}

				var handle = new SafeFileHandle((IntPtr)duplicate, ownsHandle: true);
				try
				{
					return new IosRuntimeOwnedStream(
						new FileStream(handle, FileAccess.Read, 81920, isAsync: false), owner);
				}
				catch
				{
					handle.Dispose();
					owner.Release();
					throw;
				}
			}
		}

		public void VerifyUnchanged()
		{
			IosRuntimeRootDirectoryOwner currentOwner;
			lock (sync)
			{
				if (leafDescriptor < 0 || owner == null)
					throw new ObjectDisposedException(nameof(IosRuntimeResourceLease));
				if (!IosRuntimeUnixNative.TryReadIdentity(leafDescriptor, layout, out var current) ||
					!current.IsRegular || !current.SameFile(leafIdentity))
					throw new IOException("The runtime resource identity changed.");

				currentOwner = owner;
			}

			if (!currentOwner.VerifyCurrent(components, parentIdentities, leafIdentity))
				throw new IOException("The runtime resource identity changed.");
		}

		public void Dispose()
		{
			int descriptor;
			IosRuntimeRootDirectoryOwner release;
			lock (sync)
			{
				descriptor = leafDescriptor;
				leafDescriptor = -1;
				release = owner;
				owner = null;
			}

			if (descriptor >= 0)
				IosRuntimeUnixNative.Close(descriptor);
			release?.Release();
		}
	}

	sealed class IosRuntimeOwnedStream : Stream
	{
		readonly object sync = new();
		Stream inner;
		IosRuntimeRootDirectoryOwner owner;

		public override bool CanRead => inner?.CanRead == true;
		public override bool CanSeek => inner?.CanSeek == true;
		public override bool CanWrite => false;
		public override long Length => Current.Length;
		public override long Position
		{
			get => Current.Position;
			set => Current.Position = value;
		}

		Stream Current => inner ?? throw new ObjectDisposedException(nameof(IosRuntimeOwnedStream));

		internal IosRuntimeOwnedStream(Stream inner, IosRuntimeRootDirectoryOwner owner)
		{
			this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
			this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
		}

		public override void Flush() => Current.Flush();
		public override int Read(byte[] buffer, int offset, int count) =>
			Current.Read(buffer, offset, count);
		public override long Seek(long offset, SeekOrigin origin) => Current.Seek(offset, origin);
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		protected override void Dispose(bool disposing)
		{
			if (!disposing)
			{
				base.Dispose(disposing);
				return;
			}

			Stream releaseStream;
			IosRuntimeRootDirectoryOwner releaseOwner;
			lock (sync)
			{
				releaseStream = inner;
				inner = null;
				releaseOwner = owner;
				owner = null;
			}

			try
			{
				releaseStream?.Dispose();
			}
			finally
			{
				releaseOwner?.Release();
				base.Dispose(disposing);
			}
		}
	}

	sealed class IosRuntimeRootDirectoryOwner
	{
		readonly object sync = new();
		readonly IosUnixFileIdentity rootIdentity;
		int rootDescriptor;
		int references;

		internal IosUnixFileLayout Layout { get; }

		internal IosRuntimeRootDirectoryOwner(
			int rootDescriptor,
			IosUnixFileLayout layout,
			IosUnixFileIdentity rootIdentity)
		{
			this.rootDescriptor = rootDescriptor >= 0 ? rootDescriptor :
				throw new ArgumentOutOfRangeException(nameof(rootDescriptor));
			if (!rootIdentity.IsDirectory)
				throw new ArgumentException("The runtime root identity is not a directory.", nameof(rootIdentity));

			Layout = layout;
			this.rootIdentity = rootIdentity;
			references = 1;
		}

		internal bool TryRetain()
		{
			lock (sync)
			{
				if (rootDescriptor < 0 || references <= 0)
					return false;

				references = checked(references + 1);
				return true;
			}
		}

		internal void Release()
		{
			var close = -1;
			lock (sync)
			{
				if (references <= 0)
					return;

				references--;
				if (references == 0)
				{
					close = rootDescriptor;
					rootDescriptor = -1;
				}
			}

			if (close >= 0)
				IosRuntimeUnixNative.Close(close);
		}

		internal bool TryOpenVerifiedRoot(
			out int duplicate,
			out IosUnixFileIdentity currentIdentity)
		{
			lock (sync)
			{
				duplicate = -1;
				currentIdentity = default;
				if (rootDescriptor < 0 || references <= 0 ||
					!IosRuntimeUnixNative.TryReadIdentity(rootDescriptor, Layout, out currentIdentity) ||
					!currentIdentity.IsDirectory || !currentIdentity.SameNodeAndType(rootIdentity))
					return false;

				duplicate = IosRuntimeUnixNative.OpenAt(
					rootDescriptor, ".", Layout.DirectoryFlags, 0);
				return duplicate >= 0;
			}
		}

		internal bool VerifyCurrent(
			ImmutableArray<string> components,
			ImmutableArray<IosUnixFileIdentity> expectedParents,
			IosUnixFileIdentity expectedLeaf)
		{
			if (components.IsDefault || expectedParents.IsDefault ||
				expectedParents.Length != components.Length || components.Length == 0)
				return false;

			var current = -1;
			var leaf = -1;
			try
			{
				if (!TryOpenVerifiedRoot(out current, out var currentRoot) ||
					!currentRoot.SameNodeAndType(expectedParents[0]))
					return false;

				for (var index = 0; index < components.Length - 1; index++)
				{
					var next = IosRuntimeUnixNative.OpenAt(
						current, components[index], Layout.DirectoryFlags, 0);
					if (next < 0 ||
						!IosRuntimeUnixNative.TryReadIdentity(next, Layout, out var parentIdentity) ||
						!parentIdentity.IsDirectory ||
						!parentIdentity.SameNodeAndType(expectedParents[index + 1]))
					{
						if (next >= 0)
							IosRuntimeUnixNative.Close(next);
						return false;
					}

					IosRuntimeUnixNative.Close(current);
					current = next;
				}

				leaf = IosRuntimeUnixNative.OpenAt(
					current, components[^1], Layout.FileFlags, 0);
				return leaf >= 0 &&
					IosRuntimeUnixNative.TryReadIdentity(leaf, Layout, out var leafIdentity) &&
					leafIdentity.IsRegular && leafIdentity.SameFile(expectedLeaf);
			}
			catch (Exception exception) when (IosRuntimeResourceRoot.IsBoundaryException(exception))
			{
				return false;
			}
			finally
			{
				if (leaf >= 0)
					IosRuntimeUnixNative.Close(leaf);
				if (current >= 0)
					IosRuntimeUnixNative.Close(current);
			}
		}
	}

	readonly struct IosUnixFileLayout
	{
		public int DirectoryFlags { get; }
		public int FileFlags { get; }
		public int DeviceOffset { get; }
		public int DeviceWidth { get; }
		public int InodeOffset { get; }
		public int InodeWidth { get; }
		public int ModeOffset { get; }
		public int ModeWidth { get; }
		public int LinkCountOffset { get; }
		public int LinkCountWidth { get; }
		public int SizeOffset { get; }
		public int ModificationSecondsOffset { get; }
		public int ModificationNanosecondsOffset { get; }
		public int ChangeSecondsOffset { get; }
		public int ChangeNanosecondsOffset { get; }

		public IosUnixFileLayout(
			int directoryFlags, int fileFlags,
			int deviceOffset, int deviceWidth,
			int inodeOffset, int inodeWidth,
			int modeOffset, int modeWidth, int linkCountOffset, int linkCountWidth,
			int sizeOffset,
			int modificationSecondsOffset,
			int modificationNanosecondsOffset,
			int changeSecondsOffset,
			int changeNanosecondsOffset)
		{
			DirectoryFlags = directoryFlags;
			FileFlags = fileFlags;
			DeviceOffset = deviceOffset;
			DeviceWidth = deviceWidth;
			InodeOffset = inodeOffset;
			InodeWidth = inodeWidth;
			ModeOffset = modeOffset;
			ModeWidth = modeWidth;
			LinkCountOffset = linkCountOffset;
			LinkCountWidth = linkCountWidth;
			SizeOffset = sizeOffset;
			ModificationSecondsOffset = modificationSecondsOffset;
			ModificationNanosecondsOffset = modificationNanosecondsOffset;
			ChangeSecondsOffset = changeSecondsOffset;
			ChangeNanosecondsOffset = changeNanosecondsOffset;
		}
	}

	readonly struct IosUnixFileIdentity
	{
		const uint FileTypeMask = 0xF000;
		const uint RegularFile = 0x8000;
		const uint Directory = 0x4000;

		public ulong Device { get; }
		public ulong Inode { get; }
		public uint Mode { get; }
		public ulong LinkCount { get; }
		public long Length { get; }
		public long ModificationSeconds { get; }
		public long ModificationNanoseconds { get; }
		public long ChangeSeconds { get; }
		public long ChangeNanoseconds { get; }
		public bool IsRegular => (Mode & FileTypeMask) == RegularFile;
		public bool IsDirectory => (Mode & FileTypeMask) == Directory;

		public IosUnixFileIdentity(
			ulong device,
			ulong inode,
			uint mode, ulong linkCount,
			long length,
			long modificationSeconds,
			long modificationNanoseconds,
			long changeSeconds,
			long changeNanoseconds)
		{
			Device = device;
			Inode = inode;
			Mode = mode;
			LinkCount = linkCount;
			Length = length;
			ModificationSeconds = modificationSeconds;
			ModificationNanoseconds = modificationNanoseconds;
			ChangeSeconds = changeSeconds;
			ChangeNanoseconds = changeNanoseconds;
		}

		public bool SameNodeAndType(IosUnixFileIdentity other) =>
			Device == other.Device && Inode == other.Inode &&
			(Mode & FileTypeMask) == (other.Mode & FileTypeMask);

		public bool SameFile(IosUnixFileIdentity other) =>
			SameNodeAndType(other) && Length == other.Length &&
			ModificationSeconds == other.ModificationSeconds &&
			ModificationNanoseconds == other.ModificationNanoseconds &&
			ChangeSeconds == other.ChangeSeconds &&
			ChangeNanoseconds == other.ChangeNanoseconds;
	}

	static class IosRuntimeResourcePath
	{
		static readonly UTF8Encoding StrictUtf8 = new(false, true);

		public static bool TrySplit(string value, out ImmutableArray<string> components)
		{
			components = default;
			try
			{
				if (string.IsNullOrEmpty(value) || value[0] == '/' || value[^1] == '/' ||
					value.IndexOfAny(new[] { '\\', '\0', ':' }) >= 0 ||
					!value.IsNormalized(NormalizationForm.FormC) ||
					value.Any(character => char.IsControl(character) ||
						character == '\u2028' || character == '\u2029'))
					return false;

				var split = value.Split('/');
				if (split.Length == 0 || split.Any(component =>
					component.Length == 0 || component == "." || component == ".."))
					return false;

				if (StrictUtf8.GetByteCount(value) > 4096 ||
					split.Any(component => StrictUtf8.GetByteCount(component) > 255))
					return false;

				components = ImmutableArray.Create(split);
				return true;
			}
			catch (ArgumentException)
			{
				return false;
			}
		}
	}

	static class IosRuntimeUnixNative
	{
		const int DarwinDirectoryFlags = 0x1100100;
		const int DarwinFileFlags = 0x1000104;
		const int LinuxDirectoryFlags = 0xB0000;
		const int LinuxFileFlags = 0xA0800;

		static bool IsDarwin => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
			OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst();

		public static bool TryGetCurrentLayout(out IosUnixFileLayout layout)
		{
			var platform = IsDarwin ? OSPlatform.OSX :
				RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OSPlatform.Linux : default;
			return TryGetLayout(platform, RuntimeInformation.ProcessArchitecture, out layout);
		}

		internal static bool TryGetLayout(
			OSPlatform platform,
			Architecture architecture,
			out IosUnixFileLayout layout)
		{
			// Xcode's Darwin arm64 and x86_64 struct stat ABIs both use the same
			// relevant offsets in the 144-byte structure.
			if (platform == OSPlatform.OSX &&
				architecture is Architecture.Arm64 or Architecture.X64)
			{
				layout = new IosUnixFileLayout(
					DarwinDirectoryFlags, DarwinFileFlags,
					0, 4, 8, 8, 4, 2, 6, 2, 96,
					48, 56, 64, 72);
				return true;
			}

			if (platform == OSPlatform.Linux && architecture == Architecture.Arm64)
			{
				layout = new IosUnixFileLayout(
					LinuxDirectoryFlags, LinuxFileFlags,
					0, 8, 8, 8, 16, 4, 16, 8, 48,
					88, 96, 104, 112);
				return true;
			}

			if (platform == OSPlatform.Linux && architecture == Architecture.X64)
			{
				layout = new IosUnixFileLayout(
					LinuxDirectoryFlags, LinuxFileFlags,
					0, 8, 8, 8, 24, 4, 16, 8, 48,
					88, 96, 104, 112);
				return true;
			}

			layout = default;
			return false;
		}

		public static bool TryReadIdentity(
			int descriptor, IosUnixFileLayout layout, out IosUnixFileIdentity identity)
		{
			identity = default;
			var metadata = Marshal.AllocHGlobal(256);
			try
			{
				for (var offset = 0; offset < 256; offset += sizeof(long))
					Marshal.WriteInt64(metadata, offset, 0);

				if (FStat(descriptor, metadata) != 0)
					return false;

				var device = ReadUnsigned(metadata, layout.DeviceOffset, layout.DeviceWidth);
				var inode = ReadUnsigned(metadata, layout.InodeOffset, layout.InodeWidth);
				var mode = checked((uint)ReadUnsigned(metadata, layout.ModeOffset, layout.ModeWidth));
				var length = Marshal.ReadInt64(metadata, layout.SizeOffset);
				identity = new IosUnixFileIdentity(
					device,
					inode,
					mode,
					ReadUnsigned(metadata, layout.LinkCountOffset, layout.LinkCountWidth),
					length,
					Marshal.ReadInt64(metadata, layout.ModificationSecondsOffset),
					Marshal.ReadInt64(metadata, layout.ModificationNanosecondsOffset),
					Marshal.ReadInt64(metadata, layout.ChangeSecondsOffset),
					Marshal.ReadInt64(metadata, layout.ChangeNanosecondsOffset));
				return true;
			}
			finally
			{
				Marshal.FreeHGlobal(metadata);
			}
		}

		static ulong ReadUnsigned(IntPtr buffer, int offset, int width) => width switch
		{
			2 => unchecked((ushort)Marshal.ReadInt16(buffer, offset)),
			4 => unchecked((uint)Marshal.ReadInt32(buffer, offset)),
			8 => unchecked((ulong)Marshal.ReadInt64(buffer, offset)),
			_ => throw new ArgumentOutOfRangeException(nameof(width)),
		};

		[DllImport("libc", EntryPoint = "open", SetLastError = true)]
		internal static extern int Open(string path, int flags, int mode);

		[DllImport("libc", EntryPoint = "openat", SetLastError = true)]
		internal static extern int OpenAt(int directory, string path, int flags, int mode);

		static int FStat(int descriptor, IntPtr metadata) =>
			UseDarwinInode64FStatSymbol(
				OperatingSystem.IsMacOS(), RuntimeInformation.ProcessArchitecture)
				? FStatDarwinInode64(descriptor, metadata)
				: FStatBare(descriptor, metadata);

		internal static bool UseDarwinInode64FStatSymbol(
			bool isMacOS,
			Architecture architecture) =>
			isMacOS && architecture == Architecture.X64;

		[DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
		static extern int FStatBare(int descriptor, IntPtr metadata);

		[DllImport("libc", EntryPoint = "fstat$INODE64", SetLastError = true)]
		static extern int FStatDarwinInode64(int descriptor, IntPtr metadata);

		[DllImport("libc", EntryPoint = "dup", SetLastError = true)]
		internal static extern int Dup(int descriptor);

		[DllImport("libc", EntryPoint = "close", SetLastError = true)]
		internal static extern int Close(int descriptor);
	}
}
