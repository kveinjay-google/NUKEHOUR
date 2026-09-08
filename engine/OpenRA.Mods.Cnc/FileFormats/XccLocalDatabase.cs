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
using System.Text;
using OpenRA.FileSystem;

namespace OpenRA.Mods.Cnc.FileFormats
{
	public class XccLocalDatabase
	{
		const long MaximumTrustedDatabaseBytes = 64L << 20;
		const int MaximumTrustedDatabaseNames = 1_000_000;
		const int MaximumTrustedNameBytes = 4096;
		public readonly string[] Entries;
		public XccLocalDatabase(Stream s)
		{
			if (s == null)
				throw new ArgumentNullException(nameof(s));
			if (!s.CanRead || !s.CanSeek || s.Length < 52 || s.Length > MaximumTrustedDatabaseBytes)
				throw new InvalidDataException("The local MIX database envelope is invalid.");

			try
			{
				// Skip unnecessary header data, but validate the scalar count before allocation.
				s.Seek(48, SeekOrigin.Begin);
				using var reader = new BinaryReader(s, Encoding.ASCII, true);
				var count = reader.ReadInt32();
				if (count < 0 || count > MaximumTrustedDatabaseNames || count > s.Length - s.Position)
					throw new InvalidDataException("The local MIX database name count is invalid.");

				Entries = new string[count];
				var bytes = new byte[32];
				for (var i = 0; i < count; i++)
				{
					var length = 0;
					while (true)
					{
						if (s.Position >= s.Length)
							throw new InvalidDataException("The local MIX database name is truncated.");
						var value = reader.ReadByte();
						if (value == 0)
							break;
						if (value > 0x7F || length >= MaximumTrustedNameBytes)
							throw new InvalidDataException("The local MIX database name is invalid.");
						if (length == bytes.Length)
							Array.Resize(ref bytes, bytes.Length * 2);
						bytes[length++] = value;
					}

					Entries[i] = Encoding.ASCII.GetString(bytes, 0, length);
				}
			}
			catch (InvalidDataException)
			{
				throw;
			}
			catch (Exception ex) when (ex is IOException || ex is EndOfStreamException || ex is OverflowException)
			{
				throw new InvalidDataException("The local MIX database envelope is invalid.", ex);
			}
		}

		public XccLocalDatabase(IEnumerable<string> filenames)
		{
			Entries = filenames.ToArray();
		}

		public static ImmutableArray<string> ReadValidated(Stream stream, long maximumBytes, int maximumNames)
			=> ReadValidated(stream, maximumBytes, maximumNames, null);

		public static ImmutableArray<string> ReadValidated(Stream stream, long maximumBytes, int maximumNames,
			PackageReadContext context)
		{
			if (stream == null)
				throw new ArgumentNullException(nameof(stream));
			if (!stream.CanRead || !stream.CanSeek)
				throw new ArgumentException("The local MIX database must be readable and seekable.", nameof(stream));
			if (maximumBytes <= 0)
				throw new ArgumentOutOfRangeException(nameof(maximumBytes));
			if (maximumNames <= 0)
				throw new ArgumentOutOfRangeException(nameof(maximumNames));
			if (stream.Length < 52 || stream.Length > maximumBytes)
				throw new InvalidDataException("The local MIX database length is invalid.");

			stream.Position = 48;
			using var reader = new BinaryReader(stream, Encoding.ASCII, true);
			context?.ThrowIfCancelledOrExpired();
			var count = reader.ReadInt32();
			if (count <= 0 || count > maximumNames || count > (stream.Length - stream.Position) / 2)
				throw new InvalidDataException("The local MIX database name count is invalid.");

			var result = ImmutableArray.CreateBuilder<string>(count);
			for (var i = 0; i < count; i++)
			{
				context?.ThrowIfCancelledOrExpired();
				var bytes = new List<byte>();
				while (true)
				{
					context?.ThrowIfCancelledOrExpired();
					if (stream.Position >= stream.Length)
						throw new EndOfStreamException();
					var value = reader.ReadByte();
					if (value == 0)
						break;
					if (value > 0x7F)
						throw new InvalidDataException("The local MIX database contains a non-ASCII name.");
					bytes.Add(value);
				}

				if (bytes.Count == 0)
					throw new InvalidDataException("The local MIX database contains an empty name.");
				result.Add(Encoding.ASCII.GetString(bytes.ToArray()));
			}

			if (stream.Position != stream.Length)
				throw new InvalidDataException("The local MIX database contains trailing bytes.");
			return result.MoveToImmutable();
		}

		public byte[] Data()
		{
			var data = new MemoryStream();
			using (var writer = new BinaryWriter(data))
			{
				writer.Write(Encoding.ASCII.GetBytes("XCC by Olaf van der Spek"));
				writer.Write(new byte[] { 0x1A, 0x04, 0x17, 0x27, 0x10, 0x19, 0x80, 0x00 });

				writer.Write(Entries.Sum(e => e.Length) + Entries.Length + 52); // Size
				writer.Write(0); // Type
				writer.Write(0); // Version
				writer.Write(0); // Game/Format (0 == TD)
				writer.Write(Entries.Length); // Entries
				foreach (var e in Entries)
				{
					writer.Write(Encoding.ASCII.GetBytes(e));
					writer.Write((byte)0);
				}
			}

			return data.ToArray();
		}
	}
}
