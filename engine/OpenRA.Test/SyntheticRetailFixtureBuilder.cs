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
using System.IO;
using System.Linq;
using System.Text;
using OpenRA.Mods.Cnc.FileSystem;

namespace OpenRA.Test
{
	/// <summary>Writes only project-generated sentinel fixtures. It never reads retail content.</summary>
	public sealed class SyntheticRetailFixtureBuilder : IDisposable
	{
		public const string Sentinel = "SYNTHETIC-NO-EA-DATA";
		public string Root { get; }

		public SyntheticRetailFixtureBuilder()
		{
			Root = Path.Combine(Path.GetTempPath(), "openra-synthetic-retail-" + Guid.NewGuid());
			Directory.CreateDirectory(Root);
		}

		public string WriteMinimalMix(string relativePath)
		{
			var data = Encoding.ASCII.GetBytes(Sentinel);
			return WriteMix(relativePath, 0, (uint)data.Length,
				new[] { (Hash: 0x534E544CU, Offset: 0U, Length: (uint)data.Length) }, data);
		}

		public byte[] BuildNamedMix(PackageHashType hashType,
			params (string Name, byte[] Data)[] entries)
		{
			if (entries == null)
				throw new ArgumentNullException(nameof(entries));

			var offset = 0U;
			var index = entries.Select(entry =>
			{
				if (string.IsNullOrEmpty(entry.Name) || entry.Data == null)
					throw new ArgumentException("Synthetic entries require a name and data.", nameof(entries));

				var item = (Hash: PackageEntry.HashFilename(entry.Name, hashType), Offset: offset,
					Length: checked((uint)entry.Data.Length));
				offset = checked(offset + item.Length);
				return item;
			}).ToArray();

			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
			{
				writer.Write((ushort)0);
				writer.Write((ushort)0);
				writer.Write(checked((ushort)index.Length));
				writer.Write(offset);
				foreach (var item in index)
				{
					writer.Write(item.Hash);
					writer.Write(item.Offset);
					writer.Write(item.Length);
				}

				foreach (var entry in entries)
					writer.Write(entry.Data);
			}

			return stream.ToArray();
		}

		public string WriteNamedMix(string relativePath, PackageHashType hashType,
			params (string Name, byte[] Data)[] entries) =>
			WriteBytes(relativePath, BuildNamedMix(hashType, entries));

		public (string Ra2, string Language) WriteValidBaseArchives(
			string ra2RelativePath = "ra2.mix", string languageRelativePath = "language.mix")
		{
			var basePackageNames = new[]
			{
				"cache.mix", "conquer.mix", "generic.mix", "isogen.mix", "isosnow.mix",
				"isotemp.mix", "isourb.mix", "load.mix", "local.mix", "neutral.mix", "sidec01.mix",
				"sidec02.mix", "sno.mix", "snow.mix", "tem.mix", "temperat.mix", "urb.mix", "urban.mix",
			};
			var bag = new byte[] { 1, 2, 3, 4 };
			var idx = BuildPcmIdx("sound", checked((uint)bag.Length));
			var audio = BuildNamedMix(PackageHashType.CRC32,
				("audio.idx", idx), ("audio.bag", bag));
			var cameo = BuildNamedMix(PackageHashType.CRC32,
				("synthetic.shp", Encoding.ASCII.GetBytes(Sentinel + ":cameo.mix")));
			var language = WriteNamedMix(languageRelativePath, PackageHashType.CRC32,
				("audio.mix", audio), ("cameo.mix", cameo));
			var packages = basePackageNames.Select(name => (Name: name,
				Data: BuildNamedMix(PackageHashType.CRC32,
					("synthetic.bin", Encoding.ASCII.GetBytes(Sentinel + ":" + name))))).ToArray();
			var ra2 = WriteNamedMix(ra2RelativePath, PackageHashType.CRC32, packages);
			return (ra2, language);
		}

		public static byte[] BuildPcmIdx(string name, uint length)
		{
			if (string.IsNullOrEmpty(name) || name.Length > 15 ||
				name.Any(character => !((character >= 'A' && character <= 'Z') ||
					(character >= 'a' && character <= 'z') || (character >= '0' && character <= '9')) &&
					character != '_' && character != '-'))
				throw new ArgumentException("Synthetic IDX names must fit the validated ASCII envelope.", nameof(name));

			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
			{
				writer.Write(Encoding.ASCII.GetBytes("GABA"));
				writer.Write(2);
				writer.Write(1);
				var nameBytes = new byte[16];
				Encoding.ASCII.GetBytes(name).CopyTo(nameBytes, 0);
				writer.Write(nameBytes);
				writer.Write(0U);
				writer.Write(length);
				writer.Write(22_050U);
				writer.Write(6U);
				writer.Write(0U);
			}

			return stream.ToArray();
		}

		public static byte[] BuildLocalMixDatabase(params string[] names)
		{
			if (names == null)
				throw new ArgumentNullException(nameof(names));

			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
			{
				writer.Write(new byte[48]);
				writer.Write(names.Length);
				foreach (var name in names)
				{
					writer.Write(Encoding.ASCII.GetBytes(name));
					writer.Write((byte)0);
				}
			}

			return stream.ToArray();
		}

		public string WriteMix(string relativePath, ushort flags, uint dataSize,
			(uint Hash, uint Offset, uint Length)[] entries, byte[] data)
		{
			var path = Path.Combine(Root, relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			using var stream = File.Create(path);
			using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
			writer.Write((ushort)0);
			writer.Write(flags);
			writer.Write((ushort)entries.Length);
			writer.Write(dataSize);
			foreach (var entry in entries)
			{
				writer.Write(entry.Hash);
				writer.Write(entry.Offset);
				writer.Write(entry.Length);
			}

			writer.Write(data);
			return path;
		}

		public string WriteBytes(string relativePath, byte[] data)
		{
			var path = Path.Combine(Root, relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllBytes(path, data);
			return path;
		}

		public void Dispose()
		{
			if (Directory.Exists(Root))
				Directory.Delete(Root, true);
		}
	}
}
