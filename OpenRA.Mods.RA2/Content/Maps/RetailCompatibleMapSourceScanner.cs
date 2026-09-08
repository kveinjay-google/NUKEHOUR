#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using OpenRA.FileSystem;
using OpenRA.Mods.Cnc.FileFormats;
using OpenRA.Mods.Cnc.FileSystem;

namespace OpenRA.Mods.RA2.Content
{
	public sealed class RetailCompatibleMapSourceScanner
	{
		static readonly string[] RetailRoots =
		{
			"multimd.mix", "multi.mix", "mapsmd03.mix", "maps01.mix", "maps02.mix",
			"ra2md.mix", "ra2.mix", "langmd.mix", "language.mix", "thememd.mix", "theme.mix",
		};

		static readonly HashSet<string> MapExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".map", ".mpr", ".yrm",
		};

		public ImmutableArray<IRa2MapSourceHandle> Scan(
			string importedRoot,
			string globalDatabasePath,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(importedRoot))
				throw new ArgumentException("The imported content root is required.", nameof(importedRoot));

			var globalNames = LoadGlobalNames(globalDatabasePath);
			var maps = ImmutableArray.CreateBuilder<IRa2MapSourceHandle>();
			var digests = new HashSet<string>(StringComparer.Ordinal);
			long aggregateBytes = 0;
			foreach (var rootName in RetailRoots)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var rootPath = Path.Combine(importedRoot, rootName);
				if (!File.Exists(rootPath))
					continue;

				try
				{
					var stream = new FileStream(rootPath, FileMode.Open, FileAccess.Read, FileShare.Read,
						64 << 10, FileOptions.SequentialScan);
					MixLoader.MixFile package = null;
					try
					{
						package = new MixLoader.MixFile(stream, rootName, globalNames);
						stream = null;
						ScanPackage(package, globalNames, 0, maps, digests,
							ref aggregateBytes, cancellationToken);
					}
					finally
					{
						package?.Dispose();
						stream?.Dispose();
					}
				}
				catch (Exception exception) when (exception is IOException ||
					exception is InvalidDataException || exception is ArgumentException ||
					exception is EndOfStreamException || exception is OverflowException ||
					exception is IndexOutOfRangeException)
				{
					Console.Error.WriteLine($"Could not scan {rootName} for maps: {exception.GetType().Name}");
				}
			}

			return maps.ToImmutable();
		}

		static void ScanPackage(
			IReadOnlyPackage package,
			string[] globalNames,
			int depth,
			ImmutableArray<IRa2MapSourceHandle>.Builder maps,
			HashSet<string> digests,
			ref long aggregateBytes,
			CancellationToken cancellationToken)
		{
			if (depth > IosImportEnvelopePolicyV1.MixDepth ||
				maps.Count >= IosImportEnvelopePolicyV1.ImportedMaps)
				return;

			foreach (var name in package.Contents.OrderBy(name => name, StringComparer.Ordinal))
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (maps.Count >= IosImportEnvelopePolicyV1.ImportedMaps)
					return;

				var extension = Path.GetExtension(name);
				if (MapExtensions.Contains(extension))
				{
					using var map = package.GetStream(name);
					if (map == null || !map.CanRead || !map.CanSeek || map.Length <= 0 ||
						map.Length > IosImportEnvelopePolicyV1.TraditionalMapSourceBytes)
						continue;

					var nextAggregate = checked(aggregateBytes + map.Length);
					if (nextAggregate > IosImportEnvelopePolicyV1.ImportedMapStorageBytes)
						return;

					var bytes = ReadExactly(map, checked((int)map.Length));
					var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
					if (digests.Add(digest))
					{
						maps.Add(new MemoryMapSource(name, bytes, digest));
						aggregateBytes = nextAggregate;
					}

					continue;
				}

				if (!extension.Equals(".mix", StringComparison.OrdinalIgnoreCase))
					continue;

				using var nestedStream = package.GetStream(name);
				if (nestedStream == null || !nestedStream.CanRead || !nestedStream.CanSeek)
					continue;

				using var nested = new MixLoader.MixFile(nestedStream, name, globalNames);
				ScanPackage(nested, globalNames, depth + 1, maps, digests,
					ref aggregateBytes, cancellationToken);
			}
		}

		static byte[] ReadExactly(Stream stream, int length)
		{
			var bytes = new byte[length];
			var offset = 0;
			while (offset < bytes.Length)
			{
				var read = stream.Read(bytes, offset, bytes.Length - offset);
				if (read == 0)
					throw new EndOfStreamException();
				offset += read;
			}

			return bytes;
		}

		static string[] LoadGlobalNames(string path)
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				return Array.Empty<string>();

			using var database = new XccGlobalDatabase(File.OpenRead(path));
			return database.Entries;
		}

		sealed class MemoryMapSource : IRa2MapSourceHandle
		{
			byte[] bytes;
			public string SafeName { get; }
			public long Length => bytes?.LongLength ?? 0;
			public string SourceSha256 { get; }

			public MemoryMapSource(string safeName, byte[] bytes, string sha256)
			{
				RetailSafeBasename.Validate(safeName, nameof(safeName));
				SafeName = safeName;
				this.bytes = bytes;
				SourceSha256 = sha256;
			}

			public Stream OpenVerifiedRead()
			{
				var current = Volatile.Read(ref bytes) ??
					throw new ObjectDisposedException(nameof(MemoryMapSource));
				return new MemoryStream(current, writable: false);
			}

			public void Dispose() => Interlocked.Exchange(ref bytes, null);
		}
	}
}
