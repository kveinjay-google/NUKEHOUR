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

namespace OpenRA.Mods.RA2.Content
{
	public sealed class RetailContentImporter
	{
		readonly RetailContentCatalog catalog;

		public RetailContentImporter()
			: this(new RetailContentCatalog()) { }

		public RetailContentImporter(RetailContentCatalog catalog)
		{
			this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
		}

		public async Task<RetailImportResult> ImportAsync(
			RetailImportRequest request, CancellationToken cancellationToken)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));

			if (cancellationToken.IsCancellationRequested)
				return Failure(RetailImportError.Cancelled, "Import cancelled.");

			var validation = Validate(request);
			if (validation != null)
				return validation;

			var contentRoot = Path.GetFullPath(request.ContentRoot);
			var stagingRoot = Path.Combine(contentRoot, ".staging");
			var staging = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
			var current = Path.Combine(contentRoot, "ra2");
			var previous = Path.Combine(contentRoot, ".previous");
			var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			try
			{
				Directory.CreateDirectory(contentRoot);
				CleanStaging(stagingRoot);
				Directory.CreateDirectory(staging);

				foreach (var source in request.SourceFiles)
				{
					cancellationToken.ThrowIfCancellationRequested();
					var name = Path.GetFileName(source);
					var destination = Path.Combine(staging, name);
					hashes[name] = await CopyAndHashAsync(source, destination, cancellationToken);
				}

				var status = catalog.Inspect(staging);
				if (!status.CanLaunchBaseGame)
				{
					DeleteDirectory(staging);
					return new RetailImportResult(false, RetailImportError.IncompleteBaseContent,
						"The selected files do not contain a complete RA2 base installation.", status, hashes);
				}

				Publish(staging, current, previous);
				return new RetailImportResult(true, RetailImportError.None, string.Empty, status, hashes);
			}
			catch (OperationCanceledException)
			{
				DeleteDirectory(staging);
				return Failure(RetailImportError.Cancelled, "Import cancelled.", hashes);
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
			{
				DeleteDirectory(staging);
				return Failure(RetailImportError.IoFailure, e.Message, hashes);
			}
		}

		static RetailImportResult Validate(RetailImportRequest request)
		{
			var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			long requiredBytes = 0;
			foreach (var source in request.SourceFiles)
			{
				if (string.IsNullOrEmpty(source) || !File.Exists(source))
					return Failure(RetailImportError.IoFailure, $"Source file is missing: {source}");

				var name = Path.GetFileName(source);
				if (!names.Add(name))
					return Failure(RetailImportError.DuplicateFile, $"Duplicate file name: {name}");

				var safety = PublicContentSafetyPolicy.ValidateImportFile(source);
				if (safety == ContentSafetyViolation.ExecutableContent)
					return Failure(RetailImportError.ExecutableContent, $"Executable content is not accepted: {name}");
				if (safety != ContentSafetyViolation.None)
					return Failure(RetailImportError.UnsupportedFile, $"Unsupported retail content file: {name}");

				try
				{
					requiredBytes = checked(requiredBytes + new FileInfo(source).Length);
				}
				catch (OverflowException)
				{
					return Failure(RetailImportError.InsufficientSpace, "Selected content size is invalid.");
				}
			}

			var availableBytes = request.AvailableBytes ?? AvailableBytes(request.ContentRoot);
			return availableBytes < requiredBytes
				? Failure(RetailImportError.InsufficientSpace, "Not enough free space to stage the selected content.")
				: null;
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
			else
				Directory.CreateDirectory(stagingRoot);
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
