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
using System.IO;

namespace OpenRA.Mods.Cnc.FileFormats
{
	public sealed class XccGlobalDatabase : IDisposable
	{
		const long MaximumTrustedDatabaseBytes = 64L << 20;
		const int MaximumTrustedDatabaseNames = 1_000_000;
		public readonly string[] Entries;
		readonly Stream s;

		public XccGlobalDatabase(Stream stream)
			: this(stream, MaximumTrustedDatabaseBytes, MaximumTrustedDatabaseNames)
		{
		}

		public XccGlobalDatabase(Stream stream, long maximumBytes, int maximumNames)
		{
			s = stream ?? throw new ArgumentNullException(nameof(stream));
			if (!s.CanRead || !s.CanSeek || maximumBytes <= 0 || maximumNames <= 0 || s.Length > maximumBytes)
			{
				s.Dispose();
				throw new InvalidDataException("The global MIX database envelope is invalid.");
			}

			try
			{
				var entries = new List<string>();
				var chars = new char[32];
				while (s.Peek() > -1)
				{
					var count = s.ReadInt32();
					if (count < 0 || count > maximumNames - entries.Count)
						throw new InvalidDataException("The global MIX database name count is invalid.");
					entries.Capacity += count;
					for (var i = 0; i < count; i++)
					{
						byte c;
						var charsIndex = 0;
						while ((c = s.ReadUInt8()) != 0)
						{
							if (c > 0x7F || charsIndex >= 4096)
								throw new InvalidDataException("The global MIX database name is invalid.");
							if (charsIndex >= chars.Length)
								Array.Resize(ref chars, chars.Length * 2);
							chars[charsIndex++] = (char)c;
						}

						entries.Add(new string(chars, 0, charsIndex));

						var commentBytes = 0;
						while (s.ReadUInt8() != 0)
							if (++commentBytes > 4096)
								throw new InvalidDataException("The global MIX database comment is invalid.");
					}
				}

				Entries = entries.ToArray();
			}
			catch
			{
				s.Dispose();
				throw;
			}
		}

		public void Dispose()
		{
			s.Dispose();
		}
	}
}
