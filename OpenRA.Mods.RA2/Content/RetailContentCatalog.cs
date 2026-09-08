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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace OpenRA.Mods.RA2.Content
{
	interface IRetailMixFileOpener
	{
		public Stream OpenReadOnlyRegular(string path);
	}

	readonly struct RetailUnixFileLayout
	{
		public int DirectoryFlags { get; }
		public int FileFlags { get; }
		public int ModeOffset { get; }
		public int ModeWidth { get; }

		public RetailUnixFileLayout(int directoryFlags, int fileFlags, int modeOffset, int modeWidth)
		{
			DirectoryFlags = directoryFlags;
			FileFlags = fileFlags;
			ModeOffset = modeOffset;
			ModeWidth = modeWidth;
		}
	}

	sealed class RetailStableFileOpener : IRetailMixFileOpener
	{
		const int DarwinDirectoryFlags = 0x1100100;
		const int DarwinFileFlags = 0x1000100;
		const int LinuxDirectoryFlags = 0xB0000;
		const int LinuxFileFlags = 0xA0000;
		const uint UnixFileTypeMask = 0xF000;
		const uint UnixRegularFile = 0x8000;
		const uint WindowsReadData = 0x1;
		const uint WindowsReadAttributes = 0x80;
		const uint WindowsSynchronize = 0x100000;
		const uint WindowsShareAll = 0x7;
		const uint WindowsOpenExisting = 3;
		const uint WindowsBackupSemantics = 0x02000000;
		const uint WindowsOpenReparsePoint = 0x00200000;
		const uint WindowsObjectCaseInsensitive = 0x40;
		const uint WindowsFileOpen = 1;
		const uint WindowsDirectoryFile = 0x1;
		const uint WindowsSynchronousIoNonAlert = 0x20;
		const uint WindowsNonDirectoryFile = 0x40;
		const uint WindowsFileOpenReparsePoint = 0x200000;
		const uint WindowsDirectoryAttribute = 0x10;
		const uint WindowsReparsePointAttribute = 0x400;
		const int WindowsFileAttributeTagInfo = 9;

		readonly Action<int, string> beforeOpenComponent;

		public RetailStableFileOpener(Action<int, string> beforeOpenComponent = null)
		{
			this.beforeOpenComponent = beforeOpenComponent;
		}

		public Stream OpenReadOnlyRegular(string path)
		{
			if (string.IsNullOrEmpty(path))
				return null;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return OpenWindows(path);

			// Import validation runs only after the picker selection has been copied into
			// the app-owned staging directory. iOS rejects the POSIX O_DIRECTORY flag
			// used by the hardened Darwin walker, so use Foundation's sandboxed normal
			// file access for this already-owned path.
			if (OperatingSystem.IsIOS())
				return OpenIosSandboxRegular(path);

			if (!TryGetCurrentUnixLayout(out var layout))
				return null;

			var current = -1;
			var final = -1;
			try
			{
				var absolute = NormalizeSystemAliases(Path.GetFullPath(path));
				if (!absolute.StartsWith("/", StringComparison.Ordinal))
					return null;

				var components = absolute[1..].Split('/');
				if (components.Length == 0 || components.Any(string.IsNullOrEmpty))
					return null;

				current = Open("/", layout.DirectoryFlags, 0);
				if (current < 0)
					return null;

				for (var index = 0; index < components.Length - 1; index++)
				{
					beforeOpenComponent?.Invoke(index, components[index]);
					var next = OpenAt(current, components[index], layout.DirectoryFlags, 0);
					if (next < 0)
						return null;

					Close(current);
					current = next;
				}

				beforeOpenComponent?.Invoke(components.Length - 1, components[^1]);
				final = OpenAt(current, components[^1], layout.FileFlags, 0);
				if (final < 0 || !IsRegularUnixFile(final, layout))
					return null;

				var handle = new SafeFileHandle((IntPtr)final, ownsHandle: true);
				final = -1;
				FileStream stream;
				try
				{
					stream = new FileStream(handle, FileAccess.Read, 4096, isAsync: false);
				}
				catch
				{
					handle.Dispose();
					throw;
				}

				if (!stream.CanSeek)
				{
					stream.Dispose();
					return null;
				}

				return stream;
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
				ex is ArgumentException || ex is NotSupportedException)
			{
				return null;
			}
			finally
			{
				if (final >= 0)
					Close(final);
				if (current >= 0)
					Close(current);
			}
		}

		static Stream OpenIosSandboxRegular(string path)
		{
			try
			{
				var attributes = File.GetAttributes(path);
				if ((attributes & FileAttributes.Directory) != 0 ||
					(attributes & FileAttributes.ReparsePoint) != 0)
					return null;

				return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, false);
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
			{
				return null;
			}
		}

		Stream OpenWindows(string path)
		{
			SafeFileHandle current = null;
			SafeFileHandle final = null;
			try
			{
				var absolute = Path.GetFullPath(path);
				var root = Path.GetPathRoot(absolute);
				if (string.IsNullOrEmpty(root) || root.StartsWith(@"\\", StringComparison.Ordinal))
					return null;

				var relative = absolute[root.Length..];
				var components = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
					StringSplitOptions.RemoveEmptyEntries);
				if (components.Length == 0 || components.Any(component => component == "." || component == ".."))
					return null;

				current = CreateFile(root, WindowsReadData | WindowsReadAttributes, WindowsShareAll, IntPtr.Zero,
					WindowsOpenExisting, WindowsBackupSemantics | WindowsOpenReparsePoint, IntPtr.Zero);
				if (current.IsInvalid || !IsSafeWindowsHandle(current, directory: true))
					return null;

				for (var index = 0; index < components.Length - 1; index++)
				{
					beforeOpenComponent?.Invoke(index, components[index]);
					var next = OpenWindowsRelative(current, components[index], directory: true);
					if (next == null)
						return null;

					current.Dispose();
					current = next;
				}

				beforeOpenComponent?.Invoke(components.Length - 1, components[^1]);
				final = OpenWindowsRelative(current, components[^1], directory: false);
				if (final == null)
					return null;

				var stream = new FileStream(final, FileAccess.Read, 4096, isAsync: false);
				final = null;
				if (!stream.CanSeek)
				{
					stream.Dispose();
					return null;
				}

				return stream;
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
				ex is ArgumentException || ex is NotSupportedException)
			{
				return null;
			}
			finally
			{
				final?.Dispose();
				current?.Dispose();
			}
		}

		static SafeFileHandle OpenWindowsRelative(SafeFileHandle parent, string name, bool directory)
		{
			var buffer = Marshal.StringToHGlobalUni(name);
			var unicode = Marshal.AllocHGlobal(Marshal.SizeOf<WindowsUnicodeString>());
			try
			{
				var value = new WindowsUnicodeString
				{
					Length = checked((ushort)(name.Length * 2)),
					MaximumLength = checked((ushort)((name.Length + 1) * 2)),
					Buffer = buffer,
				};
				Marshal.StructureToPtr(value, unicode, fDeleteOld: false);
				var attributes = new WindowsObjectAttributes
				{
					Length = Marshal.SizeOf<WindowsObjectAttributes>(),
					RootDirectory = parent.DangerousGetHandle(),
					ObjectName = unicode,
					Attributes = WindowsObjectCaseInsensitive,
				};
				var options = WindowsSynchronousIoNonAlert | WindowsFileOpenReparsePoint |
					(directory ? WindowsDirectoryFile : WindowsNonDirectoryFile);
				var status = NtCreateFile(out var handle, WindowsReadData | WindowsReadAttributes | WindowsSynchronize,
					ref attributes, out _, IntPtr.Zero, 0, WindowsShareAll, WindowsFileOpen, options, IntPtr.Zero, 0);
				if (status < 0 || handle.IsInvalid || !IsSafeWindowsHandle(handle, directory))
				{
					handle?.Dispose();
					return null;
				}

				return handle;
			}
			finally
			{
				Marshal.FreeHGlobal(unicode);
				Marshal.FreeHGlobal(buffer);
			}
		}

		static bool IsSafeWindowsHandle(SafeFileHandle handle, bool directory)
		{
			if (!GetFileInformationByHandleEx(handle, WindowsFileAttributeTagInfo, out var information,
				(uint)Marshal.SizeOf<WindowsFileAttributeTagInformation>()))
				return false;

			var attributes = information.FileAttributes;
			return (attributes & WindowsReparsePointAttribute) == 0 &&
				(attributes & WindowsDirectoryAttribute) != 0 == directory;
		}

		static bool IsDarwin => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
			OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst();

		static bool TryGetCurrentUnixLayout(out RetailUnixFileLayout layout)
		{
			var platform = IsDarwin ? OSPlatform.OSX :
				RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OSPlatform.Linux : default;
			return TryGetUnixLayout(platform, RuntimeInformation.ProcessArchitecture, out layout);
		}

		internal static bool TryGetUnixLayout(OSPlatform platform, Architecture architecture,
			out RetailUnixFileLayout layout)
		{
			// Darwin's plain fstat struct stat ABI is process-architecture specific: st_mode is a
			// 16-bit field at byte 4 on arm64, but at byte 8 in the legacy x86_64 ABI.
			if (platform == OSPlatform.OSX && architecture == Architecture.Arm64)
			{
				layout = new RetailUnixFileLayout(DarwinDirectoryFlags, DarwinFileFlags, 4, 2);
				return true;
			}

			if (platform == OSPlatform.OSX && architecture == Architecture.X64)
			{
				layout = new RetailUnixFileLayout(DarwinDirectoryFlags, DarwinFileFlags, 8, 2);
				return true;
			}

			if (platform == OSPlatform.Linux && architecture == Architecture.Arm64)
			{
				layout = new RetailUnixFileLayout(LinuxDirectoryFlags, LinuxFileFlags, 16, 4);
				return true;
			}

			if (platform == OSPlatform.Linux && architecture == Architecture.X64)
			{
				layout = new RetailUnixFileLayout(LinuxDirectoryFlags, LinuxFileFlags, 24, 4);
				return true;
			}

			layout = default;
			return false;
		}

		static string NormalizeSystemAliases(string path)
		{
			if (!IsDarwin)
				return path;

			foreach (var alias in new[] { "/var", "/tmp", "/etc" })
				if (path == alias || path.StartsWith(alias + "/", StringComparison.Ordinal))
					return "/private" + path;

			return path;
		}

		static bool IsRegularUnixFile(int descriptor, RetailUnixFileLayout layout)
		{
			var metadata = Marshal.AllocHGlobal(256);
			try
			{
				if (FStat(descriptor, metadata) != 0)
					return false;

				var mode = layout.ModeWidth == 2 ? (ushort)Marshal.ReadInt16(metadata, layout.ModeOffset) :
					(uint)Marshal.ReadInt32(metadata, layout.ModeOffset);

				return (mode & UnixFileTypeMask) == UnixRegularFile;
			}
			finally
			{
				Marshal.FreeHGlobal(metadata);
			}
		}

		[DllImport("libc", EntryPoint = "open", SetLastError = true)]
		static extern int Open(string path, int flags, int mode);

		[DllImport("libc", EntryPoint = "openat", SetLastError = true)]
		static extern int OpenAt(int directory, string path, int flags, int mode);

		[DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
		static extern int FStat(int descriptor, IntPtr metadata);

		[DllImport("libc", EntryPoint = "close", SetLastError = true)]
		static extern int Close(int descriptor);

		[DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode,
			ExactSpelling = true, SetLastError = true)]
		static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr securityAttributes,
			uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int informationClass,
			out WindowsFileAttributeTagInformation information, uint bufferSize);

		[DllImport("ntdll.dll")]
		static extern int NtCreateFile(out SafeFileHandle handle, uint desiredAccess,
			ref WindowsObjectAttributes objectAttributes, out WindowsIoStatusBlock ioStatusBlock,
			IntPtr allocationSize, uint fileAttributes, uint shareAccess, uint createDisposition,
			uint createOptions, IntPtr extendedAttributes, uint extendedAttributesLength);

		[StructLayout(LayoutKind.Sequential)]
		struct WindowsUnicodeString
		{
			public ushort Length;
			public ushort MaximumLength;
			public IntPtr Buffer;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct WindowsObjectAttributes
		{
			public int Length;
			public IntPtr RootDirectory;
			public IntPtr ObjectName;
			public uint Attributes;
			public IntPtr SecurityDescriptor;
			public IntPtr SecurityQualityOfService;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct WindowsIoStatusBlock
		{
			public IntPtr Status;
			public IntPtr Information;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct WindowsFileAttributeTagInformation
		{
			public uint FileAttributes;
			public uint ReparseTag;
		}
	}

	public sealed class RetailContentCatalog
	{
		static readonly string[] BaseFiles = { "ra2.mix", "language.mix" };
		static readonly string[] YuriFiles = { "ra2md.mix", "langmd.mix" };
		static readonly string[] AudioFiles = { "audio.bag", "audio.idx" };
		static readonly ImmutableDictionary<string, RetailContentGroup> RootGroups =
			new Dictionary<string, RetailContentGroup>(StringComparer.OrdinalIgnoreCase)
			{
				{ "ra2.mix", RetailContentGroup.Base },
				{ "language.mix", RetailContentGroup.Base },
				{ "theme.mix", RetailContentGroup.Ra2Music },
				{ "ra2md.mix", RetailContentGroup.Yuri },
				{ "langmd.mix", RetailContentGroup.Yuri },
				{ "thememd.mix", RetailContentGroup.YuriMusic },
			}.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
		static readonly HashSet<string> MapExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".map", ".mpr", ".oramap", ".yrm",
		};
		readonly IRetailMixFileOpener fileOpener;

		public RetailContentCatalog()
			: this(new RetailStableFileOpener())
		{
		}

		internal RetailContentCatalog(IRetailMixFileOpener fileOpener)
		{
			this.fileOpener = fileOpener ?? throw new ArgumentNullException(nameof(fileOpener));
		}

		public RetailContentStatus Inspect(string directory)
		{
			if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
				return Inspect(Array.Empty<string>());

			return Inspect(Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories));
		}

		// iOS imports owned files into the app sandbox before any engine loading.  The
		// import gate deliberately checks only the documented archive names; format
		// compatibility belongs to the real runtime loader, which can report the
		// exact asset it cannot consume.
		public RetailContentStatus InspectRequiredFilesOnly(string directory)
		{
			var names = Directory.Exists(directory)
				? Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
					.Where(path => new FileInfo(path).Length > 0)
					.Select(Path.GetFileName)
					.ToHashSet(StringComparer.OrdinalIgnoreCase)
				: new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			var baseReady = BaseFiles.All(names.Contains);
			var yuriReady = YuriFiles.All(names.Contains);
			return new RetailContentStatus(
				baseReady ? ContentState.Ready : ContentState.Missing,
				yuriReady ? ContentState.Ready : ContentState.Missing,
				baseReady ? ContentState.Ready : ContentState.Missing,
				ContentState.Missing,
				BaseFiles.Where(name => !names.Contains(name)).ToArray(),
				YuriFiles.Where(name => !names.Contains(name)).ToArray(),
				Array.Empty<string>());
		}

		public RetailOperationResult<RetailArchiveGraphResult> OpenValidatedArchiveGraph(
			string directory,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
				return RetailOperationResult<RetailArchiveGraphResult>.Failed(new RetailImportFailure(
					RetailImportErrorCode.IncompleteBase, RetailContentGroup.Base, "ra2.mix",
					RetailImportDiagnosticCode.MissingRequiredArchive));

			var owned = ImmutableArray.CreateBuilder<RetailOwnedArchiveInput>();
			var clock = SystemMonotonicClock.Instance;
			var wholeDeadline = RetailValidationDeadline.Start(clock,
				IosImportEnvelopePolicyV1.WholeValidationTimeout);
			try
			{
				foreach (var pair in RootGroups.OrderBy(pair => pair.Key, StringComparer.Ordinal))
				{
					cancellationToken.ThrowIfCancellationRequested();
					var path = Path.Combine(directory, pair.Key);
					var stream = fileOpener.OpenReadOnlyRegular(path);
					if (stream == null)
						continue;

					try
					{
						if (stream.Length <= 0 || stream.Length > IosImportEnvelopePolicyV1.SourceBytes)
							throw new InvalidDataException("A retail archive is outside the supported envelope.");
						var digest = Hash(stream, clock, wholeDeadline);
						stream.Position = 0;
						var descriptor = new RetailArchiveInputDescriptor(
							pair.Key, pair.Value, stream.Length, digest);
						owned.Add(RetailOwnedArchiveInput.TakeOwnership(descriptor, stream));
						stream = null;
					}
					finally
					{
						stream?.Dispose();
					}
				}

				var inputs = owned.ToImmutable();
				owned.Clear();
				var batch = RetailArchiveInputBatch.TakeOwnership(inputs);
				var validator = new RetailArchiveGraphValidator(
					RetailArchiveGraphValidator.IosV1Policy, clock, RetailFilenameCatalog.V1);
				return validator.ValidateOwned(batch, wholeDeadline, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				return RetailOperationResult<RetailArchiveGraphResult>.Failed(new RetailImportFailure(
					RetailImportErrorCode.Cancelled, null, null, RetailImportDiagnosticCode.None));
			}
			catch (TimeoutException)
			{
				return RetailOperationResult<RetailArchiveGraphResult>.Failed(new RetailImportFailure(
					RetailImportErrorCode.ValidationTimedOut, null, null,
					RetailImportDiagnosticCode.DeadlineExceeded));
			}
			catch (Exception exception) when (exception is IOException ||
				exception is UnauthorizedAccessException || exception is InvalidDataException ||
				exception is ArgumentException || exception is OverflowException)
			{
				return RetailOperationResult<RetailArchiveGraphResult>.Failed(new RetailImportFailure(
					RetailImportErrorCode.CorruptArchive, null, null,
					RetailImportDiagnosticCode.MalformedEnvelope));
			}
			finally
			{
				foreach (var input in owned)
					input.Dispose();
			}
		}

		public RetailContentStatus Inspect(IEnumerable<string> paths)
		{
			if (paths == null)
				throw new ArgumentNullException(nameof(paths));

			var materializedPaths = paths.Where(path => !string.IsNullOrEmpty(path)).ToArray();
			var hasLooseMap = materializedPaths.Any(path => MapExtensions.Contains(Path.GetExtension(path)));
			var acceptedNames = new HashSet<string>(StringComparer.Ordinal);
			var invalidGroups = new HashSet<RetailContentGroup>();
			var owned = ImmutableArray.CreateBuilder<RetailOwnedArchiveInput>();
			var clock = SystemMonotonicClock.Instance;
			var wholeDeadline = RetailValidationDeadline.Start(clock,
				IosImportEnvelopePolicyV1.WholeValidationTimeout);

			try
			{
				foreach (var path in materializedPaths)
				{
					var suppliedName = Path.GetFileName(path);
					if (!RootGroups.TryGetValue(suppliedName, out var group))
						continue;

					var canonicalName = RootGroups.Keys.Single(name =>
						StringComparer.OrdinalIgnoreCase.Equals(name, suppliedName));
					if (acceptedNames.Contains(canonicalName))
					{
						invalidGroups.Add(group);
						continue;
					}

					Stream stream = null;
					try
					{
						stream = fileOpener.OpenReadOnlyRegular(path);
						if (stream == null)
						{
							if (File.Exists(path))
							{
								Console.Error.WriteLine($"RA2 content validation could not open {canonicalName}.");
								invalidGroups.Add(group);
							}
							continue;
						}

						if (stream.Length < 0 || stream.Length > IosImportEnvelopePolicyV1.SourceBytes)
						{
							Console.Error.WriteLine($"RA2 content validation rejected {canonicalName} by size.");
							invalidGroups.Add(group);
							continue;
						}

						var digest = Hash(stream, clock, wholeDeadline);
						stream.Position = 0;
						var descriptor = new RetailArchiveInputDescriptor(canonicalName, group,
							stream.Length, digest);
						owned.Add(RetailOwnedArchiveInput.TakeOwnership(descriptor, stream));
						stream = null;
						acceptedNames.Add(canonicalName);
					}
					catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
						ex is ArgumentException || ex is OverflowException || ex is TimeoutException)
					{
						Console.Error.WriteLine($"RA2 content validation could not read {canonicalName} ({ex.GetType().Name}).");
						invalidGroups.Add(group);
					}
					finally
					{
						stream?.Dispose();
					}
				}

				var inputs = owned.ToImmutable();
				owned.Clear();
				var batch = RetailArchiveInputBatch.TakeOwnership(inputs);
				var validator = new RetailArchiveGraphValidator(
					RetailArchiveGraphValidator.IosV1Policy, clock, RetailFilenameCatalog.V1);
				var operation = validator.ValidateOwned(batch, wholeDeadline, CancellationToken.None);
				if (!operation.Succeeded)
				{
					var failure = operation.Failure;
					Console.Error.WriteLine($"RA2 content validation rejected {failure.Group}:{failure.SafeBasename} ({failure.Code}/{failure.Diagnostic}).");
					return FailedStatus(acceptedNames, invalidGroups, hasLooseMap);
				}

				using var graph = operation.Value;
				foreach (var pair in graph.GroupFailures)
				{
					var failure = pair.Value;
					Console.Error.WriteLine($"RA2 content validation rejected {failure.Group}:{failure.SafeBasename} ({failure.Code}/{failure.Diagnostic}).");
				}
				var baseState = GraphState(graph.Groups[RetailContentGroup.Base]);
				var yuriState = GraphState(graph.Groups[RetailContentGroup.Yuri]);
				if (invalidGroups.Contains(RetailContentGroup.Base))
					baseState = ContentState.Incomplete;
				if (invalidGroups.Contains(RetailContentGroup.Yuri))
					yuriState = ContentState.Incomplete;

				var audioState = baseState switch
				{
					ContentState.Ready => ContentState.Ready,
					ContentState.Missing => ContentState.Missing,
					_ => ContentState.Incomplete,
				};
				var mapsState = hasLooseMap ? ContentState.Ready : GraphState(graph.Groups[RetailContentGroup.Maps]);
				return Status(baseState, yuriState, audioState, mapsState, acceptedNames);
			}
			finally
			{
				foreach (var input in owned)
					input.Dispose();
			}
		}

		static string[] Missing(HashSet<string> names, IEnumerable<string> required)
		{
			return required.Where(file => !names.Contains(file)).ToArray();
		}

		static ContentState GraphState(RetailGroupStatus status)
		{
			return status.Validation switch
			{
				RetailGroupValidation.Missing => ContentState.Missing,
				RetailGroupValidation.Ready => ContentState.Ready,
				_ => ContentState.Incomplete,
			};
		}

		static RetailContentStatus FailedStatus(HashSet<string> acceptedNames,
			HashSet<RetailContentGroup> invalidGroups, bool hasLooseMap)
		{
			var baseState = acceptedNames.Overlaps(BaseFiles) || invalidGroups.Contains(RetailContentGroup.Base) ?
				ContentState.Incomplete : ContentState.Missing;
			var yuriState = acceptedNames.Overlaps(YuriFiles) || invalidGroups.Contains(RetailContentGroup.Yuri) ?
				ContentState.Incomplete : ContentState.Missing;
			var audioState = baseState == ContentState.Missing ? ContentState.Missing : ContentState.Incomplete;
			return Status(baseState, yuriState, audioState,
				hasLooseMap ? ContentState.Ready : ContentState.Missing, acceptedNames);
		}

		static RetailContentStatus Status(ContentState baseState, ContentState yuriState,
			ContentState audioState, ContentState mapsState, HashSet<string> acceptedNames)
		{
			return new RetailContentStatus(baseState, yuriState, audioState, mapsState,
				Missing(acceptedNames, BaseFiles), Missing(acceptedNames, YuriFiles),
				audioState == ContentState.Ready ? Array.Empty<string>() : (string[])AudioFiles.Clone());
		}

		static string Hash(Stream stream, IRetailMonotonicClock clock, RetailValidationDeadline deadline)
		{
			using var sha = SHA256.Create();
			var buffer = new byte[64 << 10];
			while (true)
			{
				deadline.ThrowIfExpired(clock);
				var read = stream.Read(buffer, 0, buffer.Length);
				if (read == 0)
					break;
				sha.TransformBlock(buffer, 0, read, null, 0);
			}

			sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
			return BitConverter.ToString(sha.Hash).Replace("-", string.Empty).ToLowerInvariant();
		}

		sealed class SystemMonotonicClock : IRetailMonotonicClock
		{
			public static SystemMonotonicClock Instance { get; } = new();
			public long GetTimestamp() => Stopwatch.GetTimestamp();
			public TimeSpan GetElapsedTime(long start, long end) =>
				TimeSpan.FromSeconds((end - start) / (double)Stopwatch.Frequency);
		}
	}
}
