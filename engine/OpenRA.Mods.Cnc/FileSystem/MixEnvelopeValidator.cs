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
using System.Linq;
using OpenRA.FileSystem;
using OpenRA.Mods.Cnc.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Mods.Cnc.FileSystem
{
	public interface IMixFilenameResolver
	{
		bool TryResolve(uint hash, PackageHashType hashType, out string canonicalName);
	}

	public interface IValidatedMixPackage : IReadOnlyPackage
	{
		ImmutableArray<ValidatedMixEntry> Entries { get; }
		ValidatedMixEntrySource OpenEntrySource(uint hash);
	}

	public sealed record ValidatedMixEntry(uint Hash, string CanonicalName, long DataOffset, long Length);

	public static class MixEnvelopeValidator
	{
		public static ValidatedMixIndex Validate(Stream ownedPinnedStream, MixEnvelopeLimits limits,
			PackageHashType hashType, IMixFilenameResolver resolver, PackageReadContext context)
		{
			if (ownedPinnedStream == null)
				throw new ArgumentNullException(nameof(ownedPinnedStream));

			RefCountedStreamLease owner = null;
			try
			{
				if (limits == null)
					throw new ArgumentNullException(nameof(limits));
				if (context == null)
					throw new ArgumentNullException(nameof(context));
				if (resolver == null)
					throw new ArgumentNullException(nameof(resolver));
				if (context.EnvelopePolicy != null && !ReferenceEquals(context.EnvelopePolicy.Mix, limits))
					throw new InvalidOperationException("The MIX limits do not match the bound package policy.");
				if (!Enum.IsDefined(typeof(PackageHashType), hashType))
					throw new ArgumentOutOfRangeException(nameof(hashType));
				if (ownedPinnedStream.Position != 0)
					throw new ArgumentException("The owned MIX stream must be positioned at zero.", nameof(ownedPinnedStream));

				owner = RefCountedStreamLease.TakeOwnership(ownedPinnedStream);
				ownedPinnedStream = null;
				context.ThrowIfCancelledOrExpired();
				var entries = Parse(owner, limits, context, hashType, resolver, out var childContext);
				var result = new ValidatedMixIndex(entries, owner, childContext);
				owner = null;
				return result;
			}
			catch (PackageEnvelopeException)
			{
				throw;
			}
			catch (OverflowException ex)
			{
				throw new PackageEnvelopeException(PackageEnvelopeFailure.ArithmeticOverflow,
					"MIX envelope arithmetic overflowed.", ex);
			}
			catch (Exception ex) when (ex is IOException || ex is EndOfStreamException)
			{
				throw new PackageEnvelopeException(PackageEnvelopeFailure.Malformed, "The MIX envelope is malformed.", ex);
			}
			finally
			{
				owner?.Dispose();
				ownedPinnedStream?.Dispose();
			}
		}

		static ImmutableArray<ValidatedMixEntry> Parse(RefCountedStreamLease owner, MixEnvelopeLimits limits,
			PackageReadContext context, PackageHashType hashType, IMixFilenameResolver resolver,
			out PackageReadContext childContext)
		{
			using var stream = owner.OpenSegment(0, owner.Length);
			using var reader = new BinaryReader(stream);
			if (owner.Length < 10 || reader.ReadUInt16() != 0 || reader.ReadUInt16() != 0)
				throw Malformed("Only unencrypted RA2 MIX envelopes are accepted.");

			var count = reader.ReadUInt16();
			var dataSize = reader.ReadUInt32();
			if (count == 0)
				throw Malformed("The MIX index contains no entries.");
			if (count > limits.MaximumEntriesPerArchive)
				throw Limit("The MIX entry-count limit was exceeded.");
			var dataOffset = checked(10L + count * 12L);
			if (dataOffset > limits.MaximumIndexBytes)
				throw Limit("The MIX index-byte limit was exceeded.");
			if (checked(dataOffset + dataSize) != owner.Length)
				throw Malformed("The MIX declared data length is invalid.");

			// First pass performs only scalar checks and charges the shared ledger before any table-sized allocation.
			long addressedBytes = 0;
			for (var i = 0; i < count; i++)
			{
				context.ThrowIfCancelledOrExpired();
				var hash = reader.ReadUInt32();
				var offset = reader.ReadUInt32();
				var length = reader.ReadUInt32();
				if (hash == 0 || length == 0)
					throw Malformed("The MIX index contains a zero hash or empty segment.");
				if (length > limits.MaximumSegmentBytes)
					throw Limit("A MIX segment exceeds its configured size.");
				if (checked(offset + length) > dataSize)
					throw Malformed("A MIX segment exceeds its declared data range.");
				addressedBytes = checked(addressedBytes + length);
				if (addressedBytes > limits.MaximumAddressedBytes)
					throw Limit("The MIX addressed-byte limit was exceeded.");
			}

			childContext = context.Descend(count, addressedBytes);
			stream.Position = 10;
			var raw = new List<(uint Hash, long Offset, long Length)>(count);
			var hashes = new HashSet<uint>();
			long materializedAddressedBytes = 0;
			for (var i = 0; i < count; i++)
			{
				childContext.ThrowIfCancelledOrExpired();
				var hash = reader.ReadUInt32();
				var offset = reader.ReadUInt32();
				var length = reader.ReadUInt32();
				if (hash == 0 || length == 0)
					throw Malformed("The MIX index changed to a zero hash or empty segment during validation.");
				if (length > limits.MaximumSegmentBytes)
					throw Limit("A MIX segment changed beyond its configured size during validation.");
				if (checked(offset + length) > dataSize)
					throw Malformed("A MIX segment changed beyond its declared data range during validation.");
				materializedAddressedBytes = checked(materializedAddressedBytes + length);
				if (materializedAddressedBytes > limits.MaximumAddressedBytes)
					throw Limit("The changed MIX index exceeds its addressed-byte limit.");
				if (!hashes.Add(hash))
					throw Malformed("The MIX index contains a duplicate hash.");
				raw.Add((hash, offset, length));
			}
			if (materializedAddressedBytes != addressedBytes)
				throw Malformed("The MIX index changed after its aggregate budget was charged.");

			var orderedRanges = raw.OrderBy(entry => entry.Offset).ToArray();
			for (var i = 1; i < orderedRanges.Length; i++)
				if (orderedRanges[i].Offset < checked(orderedRanges[i - 1].Offset + orderedRanges[i - 1].Length))
					throw Malformed("The MIX index contains overlapping segments.");

			var resolved = new Dictionary<uint, string>();
			var resolvedNames = new HashSet<string>(StringComparer.Ordinal);
			foreach (var entry in raw)
			{
				childContext.ThrowIfCancelledOrExpired();
				if (resolver.TryResolve(entry.Hash, hashType, out var name))
				{
					name = ValidateResolvedName(name, entry.Hash, hashType);
					if (!resolvedNames.Add(name))
						throw Malformed("The MIX resolver returned a duplicate canonical name.");
					resolved.Add(entry.Hash, name);
				}
			}

			return raw.Select(entry => new ValidatedMixEntry(entry.Hash,
				resolved.TryGetValue(entry.Hash, out var name) ? name : null,
				checked(dataOffset + entry.Offset), entry.Length)).ToImmutableArray();
		}

		static string ValidateResolvedName(string name, uint hash, PackageHashType hashType)
		{
			name = ValidateCanonicalBasename(name);
			if (PackageEntry.HashFilename(name, hashType) != hash)
				throw Malformed("The MIX resolver returned a name that does not match the requested hash.");
			return name;
		}

		static string ValidateCanonicalBasename(string name)
		{
			if (string.IsNullOrEmpty(name) || name == "." || name == ".." ||
				!StringComparer.Ordinal.Equals(name, name.ToLowerInvariant()) ||
				name.Any(character => character > 0x7F || char.IsControl(character) ||
					character == '/' || character == '\\' || character == ':'))
				throw Malformed("The MIX resolver returned a non-canonical basename.");
			return name;
		}

		static PackageEnvelopeException Malformed(string message) =>
			new(PackageEnvelopeFailure.Malformed, message);

		static PackageEnvelopeException Limit(string message) =>
			new(PackageEnvelopeFailure.LimitExceeded, message);
	}

	public sealed class ValidatedMixIndex : IDisposable
	{
		readonly Dictionary<uint, ValidatedMixEntry> byHash;
		RefCountedStreamLease owner;
		public ImmutableArray<ValidatedMixEntry> Entries { get; }
		internal PackageReadContext ReadContext { get; }

		internal ValidatedMixIndex(ImmutableArray<ValidatedMixEntry> entries,
			RefCountedStreamLease ownedLease, PackageReadContext context)
		{
			Entries = entries;
			byHash = entries.ToDictionary(entry => entry.Hash);
			owner = ownedLease;
			ReadContext = context;
		}

		public Stream OpenEntry(uint hash, PackageReadContext context)
		{
			var current = EnsureOpen(context);
			return byHash.TryGetValue(hash, out var entry) ? current.OpenSegment(entry.DataOffset, entry.Length) : null;
		}

		public ValidatedMixEntrySource OpenEntrySource(uint hash, PackageReadContext context)
		{
			var current = EnsureOpen(context);
			return byHash.TryGetValue(hash, out var entry) ?
				new ValidatedMixEntrySource(entry.CanonicalName, entry.Length, entry.DataOffset,
					current.Acquire(), ReadContext) : null;
		}

		RefCountedStreamLease EnsureOpen(PackageReadContext context)
		{
			var current = System.Threading.Volatile.Read(ref owner);
			if (current == null)
				throw new ObjectDisposedException(nameof(ValidatedMixIndex));
			if (!ReadContext.SharesLedger(context) ||
				!ReferenceEquals(ReadContext.EnvelopePolicy, context?.EnvelopePolicy))
				throw new InvalidOperationException("The MIX entry context belongs to another aggregate ledger.");
			context.ThrowIfCancelledOrExpired();
			return current;
		}

		public void Dispose()
		{
			System.Threading.Interlocked.Exchange(ref owner, null)?.Dispose();
			GC.SuppressFinalize(this);
		}
	}

	public sealed class ValidatedMixEntrySource : IDisposable
	{
		RefCountedStreamLease owner;
		readonly long offset;
		internal string CanonicalName { get; }
		internal PackageReadContext ReadContext { get; }
		public long Length { get; }

		internal ValidatedMixEntrySource(string canonicalName, long length, long offset,
			RefCountedStreamLease ownedLease, PackageReadContext context)
		{
			CanonicalName = canonicalName;
			Length = length;
			this.offset = offset;
			owner = ownedLease;
			ReadContext = context;
		}

		public Stream OpenRead()
		{
			var current = System.Threading.Volatile.Read(ref owner);
			if (current == null)
				throw new ObjectDisposedException(nameof(ValidatedMixEntrySource));
			ReadContext.ThrowIfCancelledOrExpired();
			return current.OpenSegment(offset, Length);
		}

		internal bool SharesBacking(ValidatedMixEntrySource other) =>
			other != null && System.Threading.Volatile.Read(ref owner) is { } current &&
			System.Threading.Volatile.Read(ref other.owner) is { } otherCurrent && current.SharesIdentity(otherCurrent);

		internal bool SharesLedger(PackageReadContext context) => ReadContext.SharesLedger(context);
		internal bool MatchesPolicy(PackageReadContext context) =>
			context != null && ReferenceEquals(ReadContext.EnvelopePolicy, context.EnvelopePolicy);

		public void Dispose()
		{
			System.Threading.Interlocked.Exchange(ref owner, null)?.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
