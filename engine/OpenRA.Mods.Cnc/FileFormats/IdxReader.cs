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
	public class IdxReader
	{
		const long MaximumLegacyIdxBytes = 4L << 20;
		const int MaximumLegacySoundRecords = 65_536;
		public readonly int SoundCount;
		public List<IdxEntry> Entries;

		public IdxReader(Stream s)
		{
			if (s == null)
				throw new ArgumentNullException(nameof(s));
			if (!s.CanRead || !s.CanSeek || s.Length < 12 || s.Length > MaximumLegacyIdxBytes)
				throw new InvalidDataException("IDX stream length is invalid.");
			s.Seek(0, SeekOrigin.Begin);

			var id = s.ReadASCII(4);

			if (id != "GABA")
				throw new InvalidDataException($"Unable to load Idx file, did not find magic id, found {id} instead");

			var two = s.ReadInt32();

			if (two != 2)
				throw new InvalidDataException($"Unable to load Idx file, did not find magic number 2, found {two} instead");

			SoundCount = s.ReadInt32();
			if (SoundCount <= 0 || SoundCount > MaximumLegacySoundRecords ||
				checked(12L + SoundCount * 36L) != s.Length)
				throw new InvalidDataException("IDX record count does not match its bounded stream length.");

			Entries = new List<IdxEntry>();

			for (var i = 0; i < SoundCount; i++)
				Entries.Add(new IdxEntry(s));
		}
	}
}
