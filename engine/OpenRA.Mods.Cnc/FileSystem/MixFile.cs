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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using OpenRA.FileSystem;
using OpenRA.Mods.Cnc.FileFormats;
using OpenRA.Primitives;
using FS = OpenRA.FileSystem.FileSystem;

namespace OpenRA.Mods.Cnc.FileSystem
{
	public class MixLoader : IBoundedPackageLoader
	{
		const long MaximumOrdinarySegmentBytes = 512L << 20;
		readonly bool boundedConfigured;
		readonly PackageHashType boundedHashType;
		readonly IMixFilenameResolver boundedResolver;

		public MixLoader() { }

		public MixLoader(PackageHashType hashType, IMixFilenameResolver resolver)
		{
			if (!Enum.IsDefined(typeof(PackageHashType), hashType))
				throw new ArgumentOutOfRangeException(nameof(hashType));
			boundedResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
			boundedHashType = hashType;
			boundedConfigured = true;
		}

		public sealed class MixFile : IReadOnlyPackage
		{
			public string Name { get; }
			public IEnumerable<string> Contents => index.Keys;

			readonly Dictionary<string, PackageEntry> index;
			readonly long dataStart;
			readonly Stream s;

			public MixFile(Stream s, string filename, string[] globalFilenames)
			{
				Name = filename;
				this.s = s;

				try
				{
					// Detect format type
					var isCncMix = s.ReadUInt16() != 0;

					// The C&C mix format doesn't contain any flags or encryption
					var isEncrypted = false;
					if (!isCncMix)
						isEncrypted = (s.ReadUInt16() & 0x2) != 0;

					List<PackageEntry> entries;
					if (isEncrypted)
						entries = ParseHeader(DecryptHeader(s, 4, out dataStart), 0, out _);
					else
						entries = ParseHeader(s, isCncMix ? 0 : 4, out dataStart);

					index = ParseIndex(entries.ToDictionaryWithConflictLog(x => x.Hash,
						$"{filename} ({(isCncMix ? "C&C" : "RA/TS/RA2")} format, Encrypted: {isEncrypted}, DataStart: {dataStart})",
						null, x => $"(offs={x.Offset}, len={x.Length})"), globalFilenames);
				}
				catch (Exception)
				{
					Dispose();
					throw;
				}
			}

			Dictionary<string, PackageEntry> ParseIndex(Dictionary<uint, PackageEntry> entries, string[] globalFilenames)
			{
				var allPossibleFilenames = new HashSet<string>(globalFilenames);

				// Try and find a local mix database
				var dbNameClassic = PackageEntry.HashFilename("local mix database.dat", PackageHashType.Classic);
				var dbNameCRC = PackageEntry.HashFilename("local mix database.dat", PackageHashType.CRC32);
				foreach (var kv in entries)
				{
					if (kv.Key == dbNameClassic || kv.Key == dbNameCRC)
					{
						using (var content = GetContent(kv.Value))
						{
							var db = new XccLocalDatabase(content);
							allPossibleFilenames.EnsureCapacity(allPossibleFilenames.Count + db.Entries.Length);
							allPossibleFilenames.UnionWith(db.Entries);
						}

						break;
					}
				}

				var classicIndex = new Dictionary<string, PackageEntry>(entries.Count);
				var crcIndex = new Dictionary<string, PackageEntry>(entries.Count);

				foreach (var filename in allPossibleFilenames)
				{
					var classicHash = PackageEntry.HashFilename(filename, PackageHashType.Classic);
					var crcHash = PackageEntry.HashFilename(filename, PackageHashType.CRC32);

					if (entries.TryGetValue(classicHash, out var e))
						classicIndex.Add(filename, e);

					if (entries.TryGetValue(crcHash, out e))
						crcIndex.Add(filename, e);
				}

				var bestIndex = crcIndex.Count > classicIndex.Count ? crcIndex : classicIndex;

				var unknown = entries.Count - bestIndex.Count;
				if (unknown > 0)
					Log.Write("debug", $"{Name}: failed to resolve filenames for {unknown} unknown hashes");

				bestIndex.TrimExcess();
				return bestIndex;
			}

			static List<PackageEntry> ParseHeader(Stream s, long offset, out long headerEnd)
			{
				s.Seek(offset, SeekOrigin.Begin);
				var numFiles = s.ReadUInt16();
				s.ReadUInt32(); // dataSize

				var items = new List<PackageEntry>();
				for (var i = 0; i < numFiles; i++)
					items.Add(new PackageEntry(s));

				headerEnd = offset + 6 + numFiles * PackageEntry.Size;
				return items;
			}

			static MemoryStream DecryptHeader(Stream s, long offset, out long headerEnd)
			{
				s.Seek(offset, SeekOrigin.Begin);

				// Decrypt blowfish key
				var keyblock = s.ReadBytes(80);
				var blowfishKey = new BlowfishKeyProvider().DecryptKey(keyblock);
				var fish = new Blowfish(blowfishKey);

				// Decrypt first block to work out the header length
				var ms = Decrypt(ReadBlocks(s, offset + 80, 1), fish);
				var numFiles = ms.ReadUInt16();

				// Decrypt the full header - round bytes up to a full block
				var blockCount = (13 + numFiles * PackageEntry.Size) / 8;
				headerEnd = offset + 80 + blockCount * 8;

				return Decrypt(ReadBlocks(s, offset + 80, blockCount), fish);
			}

			static MemoryStream Decrypt(uint[] h, Blowfish fish)
			{
				var decrypted = fish.Decrypt(h);

				var ms = new MemoryStream(decrypted.Length * 4);
				var writer = new BinaryWriter(ms);
				foreach (var t in decrypted)
					writer.Write(t);
				writer.Flush();

				ms.Seek(0, SeekOrigin.Begin);
				return ms;
			}

			static uint[] ReadBlocks(Stream s, long offset, int count)
			{
				if (offset < 0)
					throw new ArgumentOutOfRangeException(nameof(offset), "Non-negative number required.");

				if (count < 0)
					throw new ArgumentOutOfRangeException(nameof(count), "Non-negative number required.");

				if (offset + count * 2 > s.Length)
					throw new ArgumentException($"Bytes to read {count * 2} and offset {offset} greater than stream length {s.Length}.");

				s.Seek(offset, SeekOrigin.Begin);

				// A block is a single encryption unit (represented as two 32-bit integers)
				var ret = new uint[2 * count];
				for (var i = 0; i < ret.Length; i++)
					ret[i] = s.ReadUInt32();

				return ret;
			}

			public Stream GetContent(PackageEntry entry)
			{
				if (entry.Length > MaximumOrdinarySegmentBytes || entry.Length > int.MaxValue)
					throw new InvalidDataException("MIX segment exceeds the ordinary reader limit.");
				return SegmentStream.CreateWithoutOwningStream(s, checked(dataStart + entry.Offset), (int)entry.Length);
			}

			public Stream GetStream(string filename)
			{
				if (!index.TryGetValue(filename, out var e))
					return null;

				return GetContent(e);
			}

			public IReadOnlyDictionary<string, PackageEntry> Index
			{
				get
				{
					var absoluteIndex = index.ToDictionary(e => e.Key, e => new PackageEntry(e.Value.Hash, (uint)(e.Value.Offset + dataStart), e.Value.Length));
					return new ReadOnlyDictionary<string, PackageEntry>(absoluteIndex);
				}
			}

			public bool Contains(string filename)
			{
				return index.ContainsKey(filename);
			}

			public IReadOnlyPackage OpenPackage(string filename, FS context)
			{
				var childStream = GetStream(filename);
				if (childStream == null)
					return null;

				if (context.TryParsePackage(childStream, filename, out var package))
					return package;

				childStream.Dispose();
				return null;
			}

			public void Dispose()
			{
				s.Dispose();
			}
		}

		string[] globalFilenames;

		bool IPackageLoader.TryParsePackage(Stream s, string filename, FS context, out IReadOnlyPackage package)
		{
			if (!filename.EndsWith(".mix", StringComparison.InvariantCultureIgnoreCase))
			{
				package = null;
				return false;
			}

			// Load the global mix database
			if (globalFilenames == null && context.TryOpen("global mix database.dat", out var mixDatabase))
				using (var db = new XccGlobalDatabase(mixDatabase))
					globalFilenames = db.Entries.ToHashSet().ToArray();

			package = new MixFile(s, filename, globalFilenames ?? Array.Empty<string>());
			return true;
		}

		bool IBoundedPackageLoader.TryParsePackage(Stream stream, string filename, FS context,
			PackageReadContext readContext, out IReadOnlyPackage package)
		{
			if (readContext == null)
				throw new ArgumentNullException(nameof(readContext));
			var policy = readContext.EnvelopePolicy ?? throw new InvalidOperationException(
				"A bounded MIX loader requires an explicit package envelope policy.");
			if (!boundedConfigured)
				throw new InvalidOperationException("The parameterless MIX loader is for trusted legacy parsing only.");
			if (boundedResolver == null || !Enum.IsDefined(typeof(PackageHashType), boundedHashType))
				throw new InvalidOperationException("The bounded MIX loader configuration is invalid.");
			if (!filename.EndsWith(".mix", StringComparison.OrdinalIgnoreCase))
			{
				package = null;
				return false;
			}

			var initialPosition = stream.Position;
			if (initialPosition != 0)
				throw new InvalidDataException("A bounded MIX stream must start at zero.");
			var gate = DeferredOwnershipStream.Borrow(stream);
			ValidatedMixIndex index = null;
			try
			{
				index = MixEnvelopeValidator.Validate(
					gate, policy.Mix, boundedHashType, boundedResolver, readContext);
				package = new ValidatedMixPackage(filename, index, index.ReadContext, context);
				index = null;
				gate.CommitOwnership();
				return true;
			}
			catch
			{
				index?.Dispose();
				gate.Dispose();
				stream.Position = initialPosition;
				package = null;
				throw;
			}
		}

		sealed class ValidatedMixPackage : IValidatedMixPackage
		{
			ValidatedMixIndex index;
			readonly Dictionary<string, uint> names;
			readonly PackageReadContext readContext;
			readonly FS creatingFileSystem;
			public string Name { get; }
			public IEnumerable<string> Contents => names.Keys;
			public System.Collections.Immutable.ImmutableArray<ValidatedMixEntry> Entries =>
				(System.Threading.Volatile.Read(ref index) ??
					throw new ObjectDisposedException(nameof(ValidatedMixPackage))).Entries;

			public ValidatedMixPackage(string name, ValidatedMixIndex ownedIndex,
				PackageReadContext readContext, FS creatingFileSystem)
			{
				Name = name;
				index = ownedIndex;
				this.readContext = readContext;
				this.creatingFileSystem = creatingFileSystem;
				names = ownedIndex.Entries.Where(entry => entry.CanonicalName != null)
					.ToDictionary(entry => entry.CanonicalName, entry => entry.Hash, StringComparer.Ordinal);
			}

			public Stream GetStream(string filename)
			{
				var current = System.Threading.Volatile.Read(ref index);
				if (current == null)
					throw new ObjectDisposedException(nameof(ValidatedMixPackage));
				return filename != null && names.TryGetValue(filename, out var hash) ?
					current.OpenEntry(hash, readContext) : null;
			}

			public ValidatedMixEntrySource OpenEntrySource(uint hash)
			{
				var current = System.Threading.Volatile.Read(ref index) ??
					throw new ObjectDisposedException(nameof(ValidatedMixPackage));
				return current.OpenEntrySource(hash, readContext);
			}

			public bool Contains(string filename) => filename != null && names.ContainsKey(filename);

			public IReadOnlyPackage OpenPackage(string filename, FS context)
			{
				if (!ReferenceEquals(creatingFileSystem, context))
					throw new InvalidOperationException("A bounded MIX package rejects a replacement file system.");
				var stream = GetStream(filename);
				if (stream == null)
					return null;
				try
				{
					if (creatingFileSystem.TryParsePackage(stream, filename, readContext, out var package))
						return package;
					stream.Dispose();
					return null;
				}
				catch
				{
					stream.Dispose();
					throw;
				}
			}

			public void Dispose()
			{
				System.Threading.Interlocked.Exchange(ref index, null)?.Dispose();
			}
		}
	}
}
