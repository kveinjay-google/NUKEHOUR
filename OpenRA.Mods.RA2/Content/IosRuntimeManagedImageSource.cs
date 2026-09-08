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
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenRA.Mods.RA2.Content
{
	sealed class IosRuntimeManagedImageSource : IRuntimeManagedImageSource
	{
		static readonly UTF8Encoding StrictUtf8 = new(false, true);
		readonly object sync = new();
		IosRuntimeRootDirectoryOwner owner;

		internal IosRuntimeManagedImageSource(IosRuntimeResourceRoot sharedRoot)
		{
			if (sharedRoot == null)
				throw new ArgumentNullException(nameof(sharedRoot));
			if (!sharedRoot.TryRetainOwner(out owner))
				throw new ObjectDisposedException(nameof(sharedRoot));
		}

		public ImmutableArray<string> EnumerateAppLocalManagedImages()
		{
			IosRuntimeRootDirectoryOwner current;
			lock (sync)
			{
				current = owner;
				if (current == null || !current.TryRetain())
					throw new ObjectDisposedException(nameof(IosRuntimeManagedImageSource));
			}

			try
			{
				return EnumerateImmediateDllLeaves(current);
			}
			finally
			{
				current.Release();
			}
		}

		public IRuntimeManagedImageLease AcquirePinned(string canonicalBundleFilename)
		{
			RuntimeContractValidation.ManagedImageFilename(
				canonicalBundleFilename, nameof(canonicalBundleFilename));
			if (!IosRuntimeResourcePath.TrySplit(canonicalBundleFilename, out var components) ||
				components.Length != 1)
				throw new ArgumentException("The managed-image name must be one canonical bundle leaf.",
					nameof(canonicalBundleFilename));

			IosRuntimeRootDirectoryOwner retained;
			lock (sync)
			{
				retained = owner;
				if (retained == null || !retained.TryRetain())
					throw new ObjectDisposedException(nameof(IosRuntimeManagedImageSource));
			}

			var acquired = IosRuntimeResourceRoot.AcquireRegularFileRetained(
				retained, canonicalBundleFilename, components);
			if (!acquired.Succeeded)
				throw new IOException("The app-local managed image is not a regular pinned file.");

			var resourceLease = acquired.Value;
			try
			{
				var stream = resourceLease.OpenVerifiedRead();
				try
				{
					var mvid = ReadModuleMvid(stream);
					stream.Position = 0;
					return new IosRuntimeManagedImageLease(
						resourceLease,
						stream,
						canonicalBundleFilename,
						mvid,
						LoadedModuleMvid(canonicalBundleFilename));
				}
				catch
				{
					stream.Dispose();
					throw;
				}
			}
			catch
			{
				resourceLease.Dispose();
				throw;
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

		static ImmutableArray<string> EnumerateImmediateDllLeaves(
			IosRuntimeRootDirectoryOwner owner)
		{
			if (!owner.TryOpenVerifiedRoot(out var duplicate, out _))
				throw new IOException("The runtime bundle root identity changed.");

			var directory = IntPtr.Zero;
			try
			{
				directory = FdOpenDir(duplicate);
				if (directory == IntPtr.Zero)
					throw new IOException("The runtime bundle root could not be enumerated.");
				duplicate = -1;

				var exact = new HashSet<string>(StringComparer.Ordinal);
				var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				var names = ImmutableArray.CreateBuilder<string>();
				while (true)
				{
					Marshal.SetLastPInvokeError(0);
					var entry = ReadDir(directory);
					if (entry == IntPtr.Zero)
					{
						if (Marshal.GetLastPInvokeError() != 0)
							throw new IOException("The runtime bundle directory enumeration failed.");
						break;
					}

					var name = ReadDirectoryEntryName(entry);
					if (name is "." or ".." ||
						!name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
						continue;
					if (!name.EndsWith(".dll", StringComparison.Ordinal) ||
						!RuntimeContractValidation.TryCanonicalLeaf(name) ||
						!exact.Add(name) || !aliases.Add(name))
						throw new IOException("The app-local managed-image namespace is not canonical.");

					names.Add(name);
				}

				names.Sort(RuntimeContractDescriptorFraming.CompareUtf8);
				return names.ToImmutable();
			}
			finally
			{
				if (directory != IntPtr.Zero)
					CloseDir(directory);
				else if (duplicate >= 0)
					IosRuntimeUnixNative.Close(duplicate);
			}
		}

		static string ReadDirectoryEntryName(IntPtr entry)
		{
			int nameOffset;
			int nameLength;
			if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() ||
				OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst())
			{
				nameOffset = 21;
				nameLength = unchecked((ushort)Marshal.ReadInt16(entry, 18));
			}
			else if (OperatingSystem.IsLinux())
			{
				nameOffset = 19;
				var recordLength = unchecked((ushort)Marshal.ReadInt16(entry, 16));
				nameLength = 0;
				while (nameOffset + nameLength < recordLength &&
					Marshal.ReadByte(entry, nameOffset + nameLength) != 0)
					nameLength++;
			}
			else
				throw new PlatformNotSupportedException();

			if (nameLength <= 0 || nameLength > 1024)
				throw new IOException("The runtime bundle contains an invalid directory entry.");
			var bytes = new byte[nameLength];
			Marshal.Copy(IntPtr.Add(entry, nameOffset), bytes, 0, bytes.Length);
			return StrictUtf8.GetString(bytes);
		}

		static Guid ReadModuleMvid(Stream stream)
		{
			if (stream == null || !stream.CanRead || !stream.CanSeek)
				throw new InvalidDataException("The managed image stream must be readable and seekable.");

			using var portableExecutable = new PEReader(stream, PEStreamOptions.LeaveOpen);
			if (!portableExecutable.HasMetadata)
				throw new InvalidDataException("The managed image does not contain CLI metadata.");
			var metadata = portableExecutable.GetMetadataReader();
			var mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
			if (mvid == Guid.Empty)
				throw new InvalidDataException("The managed image has an empty MVID.");

			return mvid;
		}

		static Guid? LoadedModuleMvid(string canonicalBundleFilename)
		{
			Guid? loaded = null;
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				string filename;
				Guid mvid;
				try
				{
					filename = assembly.GetName().Name + ".dll";
					mvid = assembly.ManifestModule.ModuleVersionId;
				}
				catch
				{
					continue;
				}

				if (!StringComparer.Ordinal.Equals(filename, canonicalBundleFilename))
					continue;
				if (loaded.HasValue && loaded.Value != mvid)
					throw new InvalidDataException("Loaded assemblies disagree on a managed-image MVID.");

				loaded = mvid;
			}

			return loaded;
		}

		[DllImport("libc", EntryPoint = "fdopendir", SetLastError = true)]
		static extern IntPtr FdOpenDir(int descriptor);

		[DllImport("libc", EntryPoint = "readdir", SetLastError = true)]
		static extern IntPtr ReadDir(IntPtr directory);

		[DllImport("libc", EntryPoint = "closedir", SetLastError = true)]
		static extern int CloseDir(IntPtr directory);
	}

	sealed class IosRuntimeManagedImageLease : IRuntimeManagedImageLease
	{
		readonly object sync = new();
		IIosRuntimeResourceLease resourceLease;
		Stream stream;
		bool streamOpened;

		public string CanonicalBundleFilename { get; }
		public long Length { get; }
		public Guid ModuleMvid { get; }
		public Guid? LoadedModuleMvid { get; }

		internal IosRuntimeManagedImageLease(
			IIosRuntimeResourceLease resourceLease,
			Stream stream,
			string canonicalBundleFilename,
			Guid moduleMvid,
			Guid? loadedModuleMvid)
		{
			this.resourceLease = resourceLease ?? throw new ArgumentNullException(nameof(resourceLease));
			this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
			CanonicalBundleFilename = RuntimeContractValidation.ManagedImageFilename(
				canonicalBundleFilename, nameof(canonicalBundleFilename));
			if (!StringComparer.Ordinal.Equals(resourceLease.CanonicalRelativePath, canonicalBundleFilename))
				throw new ArgumentException("The resource lease does not match the managed-image name.",
					nameof(resourceLease));
			if (moduleMvid == Guid.Empty)
				throw new ArgumentException("The managed-image MVID must not be empty.", nameof(moduleMvid));

			Length = resourceLease.Length;
			ModuleMvid = moduleMvid;
			LoadedModuleMvid = loadedModuleMvid;
		}

		public Stream OpenRead()
		{
			lock (sync)
			{
				if (resourceLease == null || stream == null)
					throw new ObjectDisposedException(nameof(IosRuntimeManagedImageLease));
				if (streamOpened)
					throw new InvalidOperationException("The managed-image stream is one-shot.");

				streamOpened = true;
				var transfer = stream;
				stream = null;
				return transfer;
			}
		}

		public void VerifyUnchanged()
		{
			IIosRuntimeResourceLease current;
			lock (sync)
				current = resourceLease ??
					throw new ObjectDisposedException(nameof(IosRuntimeManagedImageLease));

			current.VerifyUnchanged();
		}

		public void Dispose()
		{
			IIosRuntimeResourceLease releaseLease;
			Stream releaseStream;
			lock (sync)
			{
				releaseLease = resourceLease;
				resourceLease = null;
				releaseStream = stream;
				stream = null;
			}

			try
			{
				releaseStream?.Dispose();
			}
			finally
			{
				releaseLease?.Dispose();
			}
		}
	}
}
