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
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using OpenRA.Mods.Cnc.FileSystem;

namespace OpenRA.Mods.RA2.Content
{
	public sealed class RetailContentImporter
	{
		public const string IosRuntimeLayoutMarker = ".ios-runtime-layout-v1";

		static readonly string[] BaseRuntimePackages =
		{
			"cache.mix", "cameo.mix", "conquer.mix", "generic.mix", "isogen.mix", "isosnow.mix",
			"isotemp.mix", "isourb.mix", "load.mix", "local.mix", "neutral.mix", "sidec01.mix",
			"sidec02.mix", "sno.mix", "snow.mix", "tem.mix", "temperat.mix", "urb.mix", "urban.mix",
		};
		static readonly string[] RuntimePackages = BaseRuntimePackages.Concat(new[] { "audio.mix" }).ToArray();
		static readonly string[] RuntimeArchiveFilenames = RuntimePackages
			.Concat(new[] { "audio.idx", "audio.bag", "local mix database.dat" }).ToArray();
		static readonly string[] RetailRootPackages =
		{
			"ra2.mix", "language.mix", "theme.mix", "ra2md.mix", "langmd.mix", "thememd.mix",
			"maps01.mix", "maps02.mix", "mapsmd03.mix", "multi.mix", "multimd.mix",
		};

		static readonly HashSet<string> UnsupportedMapExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".map", ".mpr", ".yrm", ".oramap",
		};

		readonly struct RetainedContent
		{
			public readonly string Source;
			public readonly string RelativePath;

			public RetainedContent(string source, string relativePath)
			{
				Source = source;
				RelativePath = relativePath;
			}
		}

		readonly RetailContentCatalog catalog;
		readonly bool requireStructuralValidation;
		readonly Action<string> failureCleanup;

		public RetailContentImporter()
			: this(new RetailContentCatalog()) { }

		public RetailContentImporter(bool requireStructuralValidation)
			: this(new RetailContentCatalog(), requireStructuralValidation) { }

		public RetailContentImporter(RetailContentCatalog catalog, bool requireStructuralValidation = true)
			: this(catalog, requireStructuralValidation, DeleteDirectory) { }

		internal RetailContentImporter(
			RetailContentCatalog catalog, bool requireStructuralValidation, Action<string> failureCleanup)
		{
			this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
			this.requireStructuralValidation = requireStructuralValidation;
			this.failureCleanup = failureCleanup ?? throw new ArgumentNullException(nameof(failureCleanup));
		}

		public async Task<RetailImportResult> ImportAsync(
			RetailImportRequest request, CancellationToken cancellationToken)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));

			if (cancellationToken.IsCancellationRequested)
				return Failure(RetailImportError.Cancelled, "Import cancelled.");

			var contentRoot = Path.GetFullPath(request.ContentRoot);
			var stagingRoot = Path.Combine(contentRoot, ".staging");
			var staging = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
			var current = Path.Combine(contentRoot, "ra2");
			var previous = Path.Combine(contentRoot, ".previous");
			var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			try
			{
				RecoverInterruptedPublish(current, previous);
				CleanStaging(stagingRoot);
				var selectedNames = request.SourceFiles
					.Select(source => Path.GetFileName(source))
					.ToHashSet(StringComparer.OrdinalIgnoreCase);
				var selectedRelativePaths = request.SourceFiles
					.Select(DestinationRelativePath)
					.ToHashSet(StringComparer.OrdinalIgnoreCase);
				var selectionSuppliesBase = selectedNames.Contains("ra2.mix") && selectedNames.Contains("language.mix");
				var selectionContainsOnlyPickerFiles = request.SourceFiles.Count > 0 &&
					request.SourceFiles.All(IsSupportedPickerFile);
				var retainedSources = selectionSuppliesBase || selectionContainsOnlyPickerFiles ?
					FindRetainedContent(current, selectedNames, selectedRelativePaths).ToArray() :
					Array.Empty<RetainedContent>();
				var validation = Validate(request, retainedSources);
				if (validation != null)
					return validation;

				Directory.CreateDirectory(contentRoot);
				Directory.CreateDirectory(staging);

				// A picker selection is an incremental update. Preserve all previously imported,
				// safe user content (including nested maps), then let newly selected files replace
				// matching roots. Derived runtime packages are regenerated from refreshed roots.
				foreach (var retained in retainedSources)
				{
					cancellationToken.ThrowIfCancellationRequested();
					var destination = Path.Combine(staging, retained.RelativePath);
					Directory.CreateDirectory(Path.GetDirectoryName(destination));
					await CopyAndHashAsync(retained.Source, destination, cancellationToken);
				}

				foreach (var source in request.SourceFiles)
				{
					cancellationToken.ThrowIfCancellationRequested();
					var relativePath = DestinationRelativePath(source);
					var destination = Path.Combine(staging, relativePath);
					Directory.CreateDirectory(Path.GetDirectoryName(destination));
					hashes[relativePath] = await CopyAndHashAsync(source, destination, cancellationToken);
				}

				ExtractEmbeddedRuntimePackages(staging);
				string invalidRuntimePackage = null;
				foreach (var package in RuntimePackages)
					try
					{
						if (!IsReadableMixFile(Path.Combine(staging, package), package))
						{
							invalidRuntimePackage = package;
							break;
						}
					}
					catch (Exception e) when (IsMalformedArchiveException(e))
					{
						invalidRuntimePackage = package;
						break;
					}

				if (invalidRuntimePackage != null)
				{
					var incompleteStatus = catalog.InspectRequiredFilesOnly(staging);
					TryCleanupStaging(staging);
					return new RetailImportResult(false, RetailImportError.IncompleteBaseContent,
						$"The selected files do not contain a readable {invalidRuntimePackage} runtime package.",
						incompleteStatus, hashes);
				}

				File.WriteAllText(Path.Combine(staging, IosRuntimeLayoutMarker), string.Empty);

				var status = requireStructuralValidation ? catalog.Inspect(staging) : catalog.InspectRequiredFilesOnly(staging);
				if (!status.CanLaunchBaseGame)
				{
					TryCleanupStaging(staging);
					return new RetailImportResult(false, RetailImportError.IncompleteBaseContent,
						"The selected files do not contain a complete RA2 base installation.", status, hashes);
				}

				Publish(staging, current, previous);
				return new RetailImportResult(true, RetailImportError.None, string.Empty, status, hashes);
			}
			catch (OperationCanceledException)
			{
				TryCleanupStaging(staging);
				return Failure(RetailImportError.Cancelled, "Import cancelled.", hashes);
			}
			catch (Exception e) when (IsMalformedArchiveException(e))
			{
				TryCleanupStaging(staging);
				return Failure(RetailImportError.IncompleteBaseContent, e.Message, hashes);
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
			{
				TryCleanupStaging(staging);
				return Failure(RetailImportError.IoFailure, e.Message, hashes);
			}
		}

		public static bool IsSupportedPickerFile(string path)
		{
			if (string.IsNullOrEmpty(path))
				return false;

			var name = Path.GetFileName(path);
			return RetailRootPackages.Contains(name, StringComparer.OrdinalIgnoreCase);
		}

		public static bool PrepareCurrentRuntimeLayout(string contentRoot)
		{
			if (string.IsNullOrEmpty(contentRoot))
				return false;

			try
			{
				var root = Path.GetFullPath(contentRoot);
				var current = Path.Combine(root, "ra2");
				RecoverInterruptedPublish(current, Path.Combine(root, ".previous"));
				return HasCurrentRuntimeLayout(current);
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException ||
				e is System.Security.SecurityException || e is ArgumentException ||
				e is NotSupportedException)
			{
				return false;
			}
		}

		public static bool HasCurrentRuntimeLayout(string directory)
		{
			if (string.IsNullOrEmpty(directory) ||
				!File.Exists(Path.Combine(directory, IosRuntimeLayoutMarker)))
				return false;

			try
			{
				foreach (var rootName in new[] { "ra2.mix", "language.mix" })
					if (!IsReadableMixFile(Path.Combine(directory, rootName), rootName))
						return false;

				foreach (var package in RuntimePackages)
				{
					var packagePath = Path.Combine(directory, package);
					if (IsNonEmptyFile(packagePath))
					{
						if (!IsReadableMixFile(packagePath, package))
							return false;
					}
					else if (!HasReadableEmbeddedRuntimePackage(directory, package))
						return false;
				}

				return true;
			}
			catch (Exception e) when (IsMalformedArchiveException(e) ||
				e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException)
			{
				return false;
			}
		}

		static IEnumerable<RetainedContent> FindRetainedContent(
			string current, HashSet<string> selectedNames, HashSet<string> selectedRelativePaths)
		{
			if (!Directory.Exists(current))
				yield break;

			var refreshesBase = selectedNames.Contains("ra2.mix");
			var refreshesLanguage = selectedNames.Contains("language.mix");
			var options = new EnumerationOptions
			{
				RecurseSubdirectories = true,
				IgnoreInaccessible = false,
				AttributesToSkip = FileAttributes.ReparsePoint,
			};
			var expandedMapRoots = FindExpandedMapRoots(current).ToArray();

			foreach (var path in Directory.EnumerateFiles(current, "*", options))
			{
				var relativePath = Path.GetRelativePath(current, path);
				if (Path.IsPathRooted(relativePath) || relativePath == ".." ||
					relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
					relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
					continue;
				var topLevel = IsTopLevelPath(relativePath);
				var name = Path.GetFileName(path);
				var expandedMapContent = expandedMapRoots.Any(root => IsDescendantOf(path, root));
				var destinationRelativePath = topLevel ? CanonicalRetailRootName(name) : relativePath;

				if (selectedRelativePaths.Contains(destinationRelativePath))
					continue;

				if (topLevel && (selectedNames.Contains(name) ||
					((refreshesBase || refreshesLanguage) &&
						RuntimePackages.Contains(name, StringComparer.OrdinalIgnoreCase))))
					continue;

				var safety = PublicContentSafetyPolicy.ValidateImportFile(path);
				if (expandedMapContent)
				{
					if (safety == ContentSafetyViolation.ExecutableContent)
						throw new InvalidDataException("An existing expanded map contains executable content.");
				}
				else if (new FileInfo(path).Length == 0 || safety != ContentSafetyViolation.None)
					continue;

				yield return new RetainedContent(path, destinationRelativePath);
			}
		}

		static IEnumerable<string> FindExpandedMapRoots(string current)
		{
			var maps = Path.Combine(current, "maps");
			if (!Directory.Exists(maps))
				yield break;

			foreach (var directory in Directory.EnumerateDirectories(maps, "*", SearchOption.TopDirectoryOnly))
			{
				if ((new DirectoryInfo(directory).Attributes & FileAttributes.ReparsePoint) != 0)
					continue;

				if (File.Exists(Path.Combine(directory, "map.yaml")) &&
					File.Exists(Path.Combine(directory, "map.bin")))
					yield return directory;
			}
		}

		static bool IsDescendantOf(string path, string directory)
		{
			var relativePath = Path.GetRelativePath(directory, path);
			return IsSafeRelativePath(relativePath) && relativePath != ".";
		}

		static bool IsTopLevelPath(string relativePath)
		{
			return relativePath.IndexOf(Path.DirectorySeparatorChar) < 0 &&
				relativePath.IndexOf(Path.AltDirectorySeparatorChar) < 0;
		}

		static string CanonicalRetailRootName(string name)
		{
			return RetailRootPackages.FirstOrDefault(
				candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) ?? name;
		}

		static string DestinationRelativePath(string source)
		{
			var name = Path.GetFileName(source);
			return CanonicalRetailRootName(name);
		}

		static bool IsSafeRelativePath(string relativePath)
		{
			return !string.IsNullOrEmpty(relativePath) &&
				!Path.IsPathRooted(relativePath) && relativePath != ".." &&
				!relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
				!relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
		}

		static bool IsNonEmptyFile(string path)
		{
			return File.Exists(path) && new FileInfo(path).Length > 0;
		}

		static bool IsReadableMixFile(string path, string name)
		{
			if (!IsNonEmptyFile(path))
				return false;

			using var input = File.OpenRead(path);
			using var archive = new MixLoader.MixFile(input, name, RuntimeArchiveFilenames);
			return true;
		}

		static bool HasReadableEmbeddedRuntimePackage(string directory, string package)
		{
			foreach (var rootName in new[] { "ra2.mix", "language.mix" })
			{
				using var input = File.OpenRead(Path.Combine(directory, rootName));
				using var archive = new MixLoader.MixFile(input, rootName, RuntimeArchiveFilenames);
				using var source = archive.GetStream(package);
				if (source == null)
					continue;

				if (source.Length == 0)
					return false;

				using var embedded = new MixLoader.MixFile(source, package, RuntimeArchiveFilenames);
				return true;
			}

			return false;
		}

		static bool IsMalformedArchiveException(Exception exception)
		{
			return exception is InvalidDataException || exception is EndOfStreamException ||
				exception is ArgumentException || exception is OverflowException ||
				exception is CryptographicException || exception is FormatException ||
				exception is NotSupportedException;
		}

		static void ExtractEmbeddedRuntimePackages(string staging)
		{
			ExtractPackages(Path.Combine(staging, "ra2.mix"), staging, RuntimePackages);
			ExtractPackages(Path.Combine(staging, "language.mix"), staging, RuntimePackages);
		}

		static void ExtractPackages(string archivePath, string destinationDirectory, IEnumerable<string> names)
		{
			if (!File.Exists(archivePath))
				return;

			using var input = File.OpenRead(archivePath);
			using var archive = new MixLoader.MixFile(input, Path.GetFileName(archivePath),
				BaseRuntimePackages.Concat(new[] { "audio.mix", "local mix database.dat" }).ToArray());
			foreach (var name in names)
			{
				var destination = Path.Combine(destinationDirectory, name);
				if (File.Exists(destination))
					continue;

				using var source = archive.GetStream(name);
				if (source == null)
					continue;
				using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
				source.CopyTo(output);
			}
		}

		static RetailImportResult Validate(RetailImportRequest request, IEnumerable<RetainedContent> retainedSources)
		{
			var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			long requiredBytes = 0;
			foreach (var source in request.SourceFiles)
			{
				if (string.IsNullOrEmpty(source) || !File.Exists(source))
					return Failure(RetailImportError.IoFailure, $"Source file is missing: {source}");

				var name = Path.GetFileName(source);
				var relativePath = DestinationRelativePath(source);
				if (!names.Add(relativePath))
					return Failure(RetailImportError.DuplicateFile, $"Duplicate destination path: {relativePath}");

				var safety = PublicContentSafetyPolicy.ValidateImportFile(source);
				if (safety == ContentSafetyViolation.ExecutableContent)
					return Failure(RetailImportError.ExecutableContent, $"Executable content is not accepted: {name}");
				if (safety != ContentSafetyViolation.None)
					return Failure(RetailImportError.UnsupportedFile, $"Unsupported retail content file: {name}");
				if (UnsupportedMapExtensions.Contains(Path.GetExtension(name)))
					return Failure(RetailImportError.UnsupportedFile,
						$"Mod-aware map validation is not available for this import: {name}");

				try
				{
					requiredBytes = checked(requiredBytes + new FileInfo(source).Length);
				}
				catch (OverflowException)
				{
					return Failure(RetailImportError.InsufficientSpace, "Selected content size is invalid.");
				}
			}

			foreach (var retained in retainedSources)
				try
				{
					requiredBytes = checked(requiredBytes + new FileInfo(retained.Source).Length);
				}
				catch (OverflowException)
				{
					return Failure(RetailImportError.InsufficientSpace, "Selected content size is invalid.");
				}

			try
			{
				requiredBytes = checked(requiredBytes +
					EstimateEmbeddedRuntimeBytes(request.SourceFiles, retainedSources));
			}
			catch (OverflowException)
			{
				return Failure(RetailImportError.InsufficientSpace, "Selected content size is invalid.");
			}

			var availableBytes = request.AvailableBytes ?? AvailableBytes(request.ContentRoot);
			return availableBytes < requiredBytes
				? Failure(RetailImportError.InsufficientSpace, "Not enough free space to stage the selected content.")
				: null;
		}

		static long EstimateEmbeddedRuntimeBytes(
			IEnumerable<string> selectedSources, IEnumerable<RetainedContent> retainedSources)
		{
			var stagedSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var retained in retainedSources)
				if (IsTopLevelPath(retained.RelativePath))
					stagedSources[Path.GetFileName(retained.RelativePath)] = retained.Source;

			foreach (var source in selectedSources)
				stagedSources[CanonicalRetailRootName(Path.GetFileName(source))] = source;

			var stagedNames = stagedSources.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
			long extractedBytes = 0;
			foreach (var rootName in new[] { "ra2.mix", "language.mix" })
				if (stagedSources.TryGetValue(rootName, out var archive))
					extractedBytes = checked(extractedBytes + EstimatePackageBytes(archive,
						RuntimePackages.Where(package => !stagedNames.Contains(package)).ToArray(), stagedNames));

			return extractedBytes;
		}

		static long EstimatePackageBytes(string archivePath, IEnumerable<string> names, ISet<string> stagedNames)
		{
			using var input = File.OpenRead(archivePath);
			using var archive = new MixLoader.MixFile(input, Path.GetFileName(archivePath),
				BaseRuntimePackages.Concat(new[] { "audio.mix", "local mix database.dat" }).ToArray());
			long total = 0;
			foreach (var name in names)
			{
				using var source = archive.GetStream(name);
				if (source != null)
				{
					total = checked(total + source.Length);
					stagedNames.Add(name);
				}
			}

			return total;
		}

		static long AvailableBytes(string path)
		{
			var root = Path.GetPathRoot(Path.GetFullPath(path));
			return new DriveInfo(root).AvailableFreeSpace;
		}

		static async Task<string> CopyAndHashAsync(
			string source, string destination, CancellationToken cancellationToken)
		{
			using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true);
			using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, true);
			var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
			try
			{
				int read;
				while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
				{
					hash.AppendData(buffer, 0, read);
					await output.WriteAsync(buffer, 0, read, cancellationToken);
				}
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}

			return BitConverter.ToString(hash.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
		}

		static void Publish(string staging, string current, string previous)
		{
			DeleteDirectory(previous);
			var movedCurrent = false;
			try
			{
				if (Directory.Exists(current))
				{
					Directory.Move(current, previous);
					movedCurrent = true;
				}

				Directory.Move(staging, current);
				DeleteDirectory(previous);
			}
			catch
			{
				if (movedCurrent && !Directory.Exists(current) && Directory.Exists(previous))
					Directory.Move(previous, current);

				throw;
			}
		}

		static void RecoverInterruptedPublish(string current, string previous)
		{
			if (!Directory.Exists(current) && Directory.Exists(previous))
				Directory.Move(previous, current);
		}

		static void CleanStaging(string stagingRoot)
		{
			if (Directory.Exists(stagingRoot))
				foreach (var path in Directory.EnumerateFileSystemEntries(stagingRoot).ToArray())
				{
					if (Directory.Exists(path))
						Directory.Delete(path, true);
					else
						File.Delete(path);
				}
		}

		void TryCleanupStaging(string staging)
		{
			try
			{
				failureCleanup(staging);
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException ||
				e is System.Security.SecurityException)
			{
				// Preserve the primary import result. A later stale-staging cleanup can retry this deletion.
			}
		}

		static void DeleteDirectory(string path)
		{
			if (Directory.Exists(path))
				Directory.Delete(path, true);
		}

		static RetailImportResult Failure(
			RetailImportError error, string message, IDictionary<string, string> hashes = null)
		{
			return new RetailImportResult(false, error, message, null, hashes);
		}
	}
}
