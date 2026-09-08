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
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OpenRA.Mods.Cnc.FileSystem;

namespace OpenRA.Mods.RA2.Content
{
	public sealed class RetailFilenameCatalog : IMixFilenameResolver
	{
		const int V1Version = 1;
		const string V1DigestSha256 = "1dec73ccb18c0518a48b8e0c3a96ac914508338bae2335bad432bea12d04c440";

		static readonly ImmutableArray<string> V1Rows = ImmutableArray.Create(
			"leaf\taudio.mix\taudio.bag",
			"leaf\taudio.mix\taudio.idx",
			"leaf\taudiomd.mix\taudio.bag",
			"leaf\taudiomd.mix\taudio.idx",
			"map-suffix\tlocal-db\t.map",
			"map-suffix\tlocal-db\t.mpr",
			"map-suffix\tlocal-db\t.oramap",
			"map-suffix\tlocal-db\t.yrm",
			"package\tbase-root\tcache.mix",
			"package\tbase-root\tcameo.mix",
			"package\tbase-root\tconquer.mix",
			"package\tbase-root\tgeneric.mix",
			"package\tbase-root\tisogen.mix",
			"package\tbase-root\tisosnow.mix",
			"package\tbase-root\tisotemp.mix",
			"package\tbase-root\tisourb.mix",
			"package\tbase-root\tload.mix",
			"package\tbase-root\tlocal.mix",
			"package\tbase-root\tneutral.mix",
			"package\tbase-root\tsidec01.mix",
			"package\tbase-root\tsidec02.mix",
			"package\tbase-root\tsno.mix",
			"package\tbase-root\tsnow.mix",
			"package\tbase-root\ttem.mix",
			"package\tbase-root\ttemperat.mix",
			"package\tbase-root\turb.mix",
			"package\tbase-root\turban.mix",
			"package\tlangmd.mix\taudiomd.mix",
			"package\tlangmd.mix\tcameomd.mix",
			"package\tlanguage.mix\taudio.mix",
			"package\tra2md.mix\tcachemd.mix",
			"package\tra2md.mix\tconqmd.mix",
			"package\tra2md.mix\tdes.mix",
			"package\tra2md.mix\tdesert.mix",
			"package\tra2md.mix\tgenermd.mix",
			"package\tra2md.mix\tisodes.mix",
			"package\tra2md.mix\tisodesmd.mix",
			"package\tra2md.mix\tisogenmd.mix",
			"package\tra2md.mix\tisolun.mix",
			"package\tra2md.mix\tisolunmd.mix",
			"package\tra2md.mix\tisosnomd.mix",
			"package\tra2md.mix\tisotemmd.mix",
			"package\tra2md.mix\tisoubn.mix",
			"package\tra2md.mix\tisoubnmd.mix",
			"package\tra2md.mix\tisourbmd.mix",
			"package\tra2md.mix\tloadmd.mix",
			"package\tra2md.mix\tlocalmd.mix",
			"package\tra2md.mix\tlun.mix",
			"package\tra2md.mix\tlunar.mix",
			"package\tra2md.mix\tntrlmd.mix",
			"package\tra2md.mix\tsnowmd.mix",
			"package\tra2md.mix\tubn.mix",
			"package\tra2md.mix\turbann.mix",
			"resolver\tany-mix\tlocal mix database.dat",
			"root\tbase\tlanguage.mix",
			"root\tbase\tra2.mix",
			"root\tra2-music\ttheme.mix",
			"root\tyuri\tlangmd.mix",
			"root\tyuri\tra2md.mix",
			"root\tyuri-music\tthememd.mix");

		readonly ImmutableDictionary<uint, string> classicNames;
		readonly ImmutableDictionary<uint, string> crc32Names;

		public static RetailFilenameCatalog V1 { get; } = new(V1Version, V1Rows);

		public int Version { get; }
		public string DigestSha256 { get; }

		RetailFilenameCatalog(int version, ImmutableArray<string> rows)
		{
			ValidateRows(rows);
			var names = rows.Select(CanonicalName)
				.Where(name => !name.StartsWith(".", StringComparison.Ordinal))
				.Distinct(StringComparer.Ordinal)
				.ToImmutableArray();
			if (names.Length != 54)
				throw new InvalidOperationException("The retail filename catalog must contain exactly 54 names.");

			classicNames = BuildIndex(names, PackageHashType.Classic);
			crc32Names = BuildIndex(names, PackageHashType.CRC32);
			Version = version;
			DigestSha256 = ComputeDigest(version, rows);
			if (!StringComparer.Ordinal.Equals(DigestSha256, V1DigestSha256))
				throw new InvalidOperationException("The retail filename catalog digest does not match V1.");
		}

		internal bool TryResolve(uint hash, PackageHashType hashType, out string canonicalName)
		{
			switch (hashType)
			{
				case PackageHashType.Classic:
					return classicNames.TryGetValue(hash, out canonicalName);
				case PackageHashType.CRC32:
					return crc32Names.TryGetValue(hash, out canonicalName);
				default:
					canonicalName = null;
					return false;
			}
		}

		bool IMixFilenameResolver.TryResolve(uint hash, PackageHashType hashType, out string canonicalName) =>
			TryResolve(hash, hashType, out canonicalName);

		static ImmutableDictionary<uint, string> BuildIndex(
			ImmutableArray<string> names, PackageHashType hashType)
		{
			var index = ImmutableDictionary.CreateBuilder<uint, string>();
			foreach (var name in names)
			{
				var hash = PackageEntry.HashFilename(name, hashType);
				if (index.ContainsKey(hash))
					throw new InvalidOperationException("The retail filename catalog contains a hash collision.");
				index.Add(hash, name);
			}

			return index.ToImmutable();
		}

		static string CanonicalName(string row)
		{
			var separator = row.LastIndexOf('\t');
			if (separator < 0 || separator == row.Length - 1)
				throw new InvalidOperationException("The retail filename catalog contains an invalid row.");
			return row[(separator + 1)..];
		}

		static string ComputeDigest(int version, ImmutableArray<string> rows)
		{
			var serialized = "ra2-retail-filename-catalog\0" +
				version.ToString(CultureInfo.InvariantCulture) + "\n" +
				string.Concat(rows.Select(row => row + "\n"));
			using var hash = SHA256.Create();
			return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(serialized)))
				.Replace("-", string.Empty)
				.ToLowerInvariant();
		}

		static void ValidateRows(ImmutableArray<string> rows)
		{
			if (rows.IsDefault || rows.Length != 60)
				throw new InvalidOperationException("The retail filename catalog must contain exactly 60 rows.");

			for (var i = 0; i < rows.Length; i++)
			{
				var row = rows[i];
				if (row == null || row.Any(character => character > 0x7F) ||
					!StringComparer.Ordinal.Equals(row, row.ToLowerInvariant()) || row.Count(character => character == '\t') != 2)
					throw new InvalidOperationException("The retail filename catalog contains a non-canonical row.");
				if (i > 0 && StringComparer.Ordinal.Compare(rows[i - 1], row) >= 0)
					throw new InvalidOperationException("The retail filename catalog rows must be unique and ordinal-sorted.");
			}
		}
	}
}
