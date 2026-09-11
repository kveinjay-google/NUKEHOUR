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
using System.IO.Compression;
using System.Text;

namespace OpenRA.Mods.RA2.Content
{
	/// <summary>
	/// Creates the empty engine world that hosts the main menu in resource-free builds.
	/// This is generated in writable user storage and is not a bundled retail or gameplay map.
	/// </summary>
	public static class RuntimeShellmapInstaller
	{
		const int Width = 130;
		const int Height = 146;
		const int CellCount = Width * Height;
		const int TilesOffset = 17;
		const int HeightsOffset = 3 * CellCount + TilesOffset;
		const int ResourcesOffset = 4 * CellCount + TilesOffset;
		const int BinaryLength = 6 * CellCount + TilesOffset;
		const string DirectoryName = "runtime-shellmap-v1";
		const string Marker = "Title: NUKE HOUR Runtime Shell";
		const string PlayablePackageName = "nukehour-starter-v1.oramap";
		const string PlayableMarker = "Title: NUKE HOUR Starter Battlefield";

		static readonly string MapYaml = string.Join(Environment.NewLine, new[]
		{
			"MapFormat: 11",
			string.Empty,
			"RequiresMod: ra2",
			string.Empty,
			Marker,
			string.Empty,
			"Author: NUKE HOUR",
			string.Empty,
			"Tileset: TEMPERATE",
			string.Empty,
			$"MapSize: {Width},{Height}",
			string.Empty,
			"Bounds: 1,1,128,144",
			string.Empty,
			"Visibility: Shellmap",
			string.Empty,
			"Categories: Shellmap",
			string.Empty,
			"Players:",
			"\tPlayerReference@Neutral:",
			"\t\tName: Neutral",
			"\t\tOwnsWorld: True",
			"\t\tNonCombatant: True",
			"\t\tFaction: Random",
			"\tPlayerReference@Creeps:",
			"\t\tName: Creeps",
			"\t\tNonCombatant: True",
			"\t\tFaction: Random",
			string.Empty,
			"Actors:",
			string.Empty,
		});

		static readonly string PlayableMapYaml = string.Join(Environment.NewLine, new[]
		{
			"MapFormat: 11",
			string.Empty,
			"RequiresMod: ra2",
			string.Empty,
			PlayableMarker,
			string.Empty,
			"Author: NUKE HOUR",
			string.Empty,
			"Tileset: TEMPERATE",
			string.Empty,
			$"MapSize: {Width},{Height}",
			string.Empty,
			"Bounds: 1,1,128,144",
			string.Empty,
			"Visibility: Lobby",
			string.Empty,
			"Categories: Conquest",
			string.Empty,
			"Players:",
			"\tPlayerReference@Neutral:",
			"\t\tName: Neutral",
			"\t\tOwnsWorld: True",
			"\t\tNonCombatant: True",
			"\t\tFaction: Random",
			"\tPlayerReference@Creeps:",
			"\t\tName: Creeps",
			"\t\tNonCombatant: True",
			"\t\tFaction: Random",
			"\t\tEnemies: Multi0, Multi1, Multi2, Multi3",
			"\tPlayerReference@Multi0:",
			"\t\tName: Multi0",
			"\t\tPlayable: True",
			"\t\tFaction: Random",
			"\t\tEnemies: Creeps",
			"\tPlayerReference@Multi1:",
			"\t\tName: Multi1",
			"\t\tPlayable: True",
			"\t\tFaction: Random",
			"\t\tEnemies: Creeps",
			"\tPlayerReference@Multi2:",
			"\t\tName: Multi2",
			"\t\tPlayable: True",
			"\t\tFaction: Random",
			"\t\tEnemies: Creeps",
			"\tPlayerReference@Multi3:",
			"\t\tName: Multi3",
			"\t\tPlayable: True",
			"\t\tFaction: Random",
			"\t\tEnemies: Creeps",
			string.Empty,
			"Actors:",
			"\tActor@mpspawn0: mpspawn",
			"\t\tLocation: 46,-10",
			"\t\tOwner: Neutral",
			"\tActor@mpspawn1: mpspawn",
			"\t\tLocation: 118,-82",
			"\t\tOwner: Neutral",
			"\tActor@mpspawn2: mpspawn",
			"\t\tLocation: 82,26",
			"\t\tOwner: Neutral",
			"\tActor@mpspawn3: mpspawn",
			"\t\tLocation: 154,-46",
			"\t\tOwner: Neutral",
			string.Empty,
		});

		public static string InstallationPath(string supportPath)
		{
			if (string.IsNullOrWhiteSpace(supportPath))
				throw new ArgumentException("The support path is unavailable.", nameof(supportPath));

			return Path.Combine(supportPath, "maps", "ra2", "nukehour-storage-v1", DirectoryName);
		}

		public static string EnsureInstalled(string supportPath)
		{
			var destination = InstallationPath(supportPath);
			if (IsValid(destination))
				return destination;

			var parent = Path.GetDirectoryName(destination) ??
				throw new InvalidOperationException("The runtime shellmap parent path is unavailable.");
			Directory.CreateDirectory(parent);
			var staging = Path.Combine(parent, "." + DirectoryName + "-" + Guid.NewGuid().ToString("N"));

			try
			{
				Directory.CreateDirectory(staging);
				File.WriteAllText(Path.Combine(staging, "map.yaml"), MapYaml, new UTF8Encoding(false));
				WriteBinary(Path.Combine(staging, "map.bin"));
				if (!IsValid(staging))
					throw new InvalidDataException("The generated runtime shellmap failed validation.");

				if (Directory.Exists(destination))
					Directory.Delete(destination, true);

				Directory.Move(staging, destination);
				return destination;
			}
			finally
			{
				if (Directory.Exists(staging))
					Directory.Delete(staging, true);
			}
		}

		public static string PlayableMapInstallationPath(string supportPath)
		{
			if (string.IsNullOrWhiteSpace(supportPath))
				throw new ArgumentException("The support path is unavailable.", nameof(supportPath));

			return Path.Combine(supportPath, "maps", "ra2", "nukehour-storage-v1", PlayablePackageName);
		}

		/// <summary>
		/// Creates a small NUKE HOUR-owned skirmish battlefield after the user has
		/// imported their retail data. The package contains layout metadata only;
		/// terrain artwork and gameplay assets continue to come from those imports.
		/// </summary>
		public static string EnsurePlayableMapInstalled(string supportPath)
		{
			var destination = PlayableMapInstallationPath(supportPath);
			if (IsValidPlayablePackage(destination))
				return destination;

			var parent = Path.GetDirectoryName(destination) ??
				throw new InvalidOperationException("The playable map parent path is unavailable.");
			Directory.CreateDirectory(parent);
			var staging = Path.Combine(parent, "." + PlayablePackageName + "-" + Guid.NewGuid().ToString("N"));

			try
			{
				using (var archive = ZipFile.Open(staging, ZipArchiveMode.Create))
				{
					var yaml = archive.CreateEntry("map.yaml", CompressionLevel.Optimal);
					using (var writer = new StreamWriter(yaml.Open(), new UTF8Encoding(false)))
						writer.Write(PlayableMapYaml);

					var binary = archive.CreateEntry("map.bin", CompressionLevel.Optimal);
					using var stream = binary.Open();
					WriteBinary(stream, includeResources: true);
				}

				if (!IsValidPlayablePackage(staging))
					throw new InvalidDataException("The generated playable map failed validation.");

				File.Move(staging, destination, true);
				return destination;
			}
			finally
			{
				if (File.Exists(staging))
					File.Delete(staging);
			}
		}

		static bool IsValid(string directory)
		{
			var yamlPath = Path.Combine(directory, "map.yaml");
			var binaryPath = Path.Combine(directory, "map.bin");
			if (!File.Exists(yamlPath) || !File.Exists(binaryPath) || new FileInfo(binaryPath).Length != BinaryLength)
				return false;

			try
			{
				if (!File.ReadAllText(yamlPath).Contains(Marker, StringComparison.Ordinal))
					return false;

				using var stream = File.OpenRead(binaryPath);
				using var reader = new BinaryReader(stream);
				return reader.ReadByte() == 2 && reader.ReadUInt16() == Width && reader.ReadUInt16() == Height &&
					reader.ReadUInt32() == TilesOffset && reader.ReadUInt32() == HeightsOffset &&
					reader.ReadUInt32() == ResourcesOffset;
			}
			catch (IOException)
			{
				return false;
			}
		}

		static bool IsValidPlayablePackage(string path)
		{
			if (!File.Exists(path))
				return false;

			try
			{
				using var archive = ZipFile.OpenRead(path);
				var yaml = archive.GetEntry("map.yaml");
				var binary = archive.GetEntry("map.bin");
				if (yaml == null || binary == null || binary.Length != BinaryLength)
					return false;

				using var reader = new StreamReader(yaml.Open());
				return reader.ReadToEnd().Contains(PlayableMarker, StringComparison.Ordinal);
			}
			catch (InvalidDataException)
			{
				return false;
			}
			catch (IOException)
			{
				return false;
			}
		}

		static void WriteBinary(string path)
		{
			using var stream = File.Create(path);
			WriteBinary(stream, includeResources: false);
		}

		static void WriteBinary(Stream stream, bool includeResources)
		{
			using var writer = new BinaryWriter(stream);
			writer.Write((byte)2);
			writer.Write((ushort)Width);
			writer.Write((ushort)Height);
			writer.Write((uint)TilesOffset);
			writer.Write((uint)HeightsOffset);
			writer.Write((uint)ResourcesOffset);
			var body = new byte[BinaryLength - TilesOffset];
			if (includeResources)
				AddOreFields(body);

			writer.Write(body);
		}

		static void AddOreFields(byte[] body)
		{
			var centers = new[] { (42, 48), (87, 48), (42, 96), (87, 96), (65, 72) };
			foreach (var (centerU, centerV) in centers)
				for (var u = centerU - 5; u <= centerU + 5; u++)
					for (var v = centerV - 5; v <= centerV + 5; v++)
					{
						var distance = Math.Abs(u - centerU) + Math.Abs(v - centerV);
						if (distance > 7 || u < 1 || u >= Width - 1 || v < 1 || v >= Height - 1)
							continue;

						var absoluteOffset = ResourcesOffset + 2 * (u * Height + v);
						var bodyOffset = absoluteOffset - TilesOffset;
						body[bodyOffset] = 1;
						body[bodyOffset + 1] = (byte)Math.Max(4, 12 - distance);
					}
		}
	}
}
