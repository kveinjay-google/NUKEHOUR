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
using System.Text;
using OpenRA.Primitives;

namespace OpenRA.Mods.RA2.Content
{
	public sealed class ValidatedMapPlayerMetadata
	{
		public string Name { get; }
		public string Palette { get; }
		public string Bot { get; }
		public string StartingUnitsClass { get; }
		public bool AllowBots { get; }
		public bool Playable { get; }
		public bool Required { get; }
		public bool OwnsWorld { get; }
		public bool Spectating { get; }
		public bool NonCombatant { get; }
		public bool LockFaction { get; }
		public string Faction { get; }
		public bool LockColor { get; }
		public Color Color { get; }
		public CPos HomeLocation { get; }
		public bool LockSpawn { get; }
		public int Spawn { get; }
		public bool LockTeam { get; }
		public int Team { get; }
		public bool LockHandicap { get; }
		public int Handicap { get; }
		public ImmutableArray<string> Allies { get; }
		public ImmutableArray<string> Enemies { get; }

		ValidatedMapPlayerMetadata(
			string name, string palette, string bot, string startingUnitsClass,
			bool allowBots, bool playable, bool required, bool ownsWorld,
			bool spectating, bool nonCombatant, bool lockFaction, string faction,
			bool lockColor, Color color, CPos homeLocation, bool lockSpawn,
			int spawn, bool lockTeam, int team, bool lockHandicap, int handicap,
			ImmutableArray<string> allies, ImmutableArray<string> enemies)
		{
			Name = name;
			Palette = palette;
			Bot = bot;
			StartingUnitsClass = startingUnitsClass;
			AllowBots = allowBots;
			Playable = playable;
			Required = required;
			OwnsWorld = ownsWorld;
			Spectating = spectating;
			NonCombatant = nonCombatant;
			LockFaction = lockFaction;
			Faction = faction;
			LockColor = lockColor;
			Color = color;
			HomeLocation = homeLocation;
			LockSpawn = lockSpawn;
			Spawn = spawn;
			LockTeam = lockTeam;
			Team = team;
			LockHandicap = lockHandicap;
			Handicap = handicap;
			Allies = allies;
			Enemies = enemies;
		}

		internal static ValidatedMapPlayerMetadata CreateValidated(
			string name, string palette, string bot, string startingUnitsClass,
			bool allowBots, bool playable, bool required, bool ownsWorld,
			bool spectating, bool nonCombatant, bool lockFaction, string faction,
			bool lockColor, Color color, CPos homeLocation, bool lockSpawn,
			int spawn, bool lockTeam, int team, bool lockHandicap, int handicap,
			ImmutableArray<string> allies, ImmutableArray<string> enemies)
		{
			name = Ra2ValidatedMapText.Required(name, nameof(name));
			palette = Ra2ValidatedMapText.Optional(palette, nameof(palette));
			bot = Ra2ValidatedMapText.Optional(bot, nameof(bot));
			startingUnitsClass = Ra2ValidatedMapText.Optional(
				startingUnitsClass, nameof(startingUnitsClass));
			faction = Ra2ValidatedMapText.Optional(faction, nameof(faction));
			var copiedAllies = Ra2ValidatedMapText.StringArray(
				allies, IosImportEnvelopePolicyV1.MapPlayerDefinitions, nameof(allies), sort: true);
			var copiedEnemies = Ra2ValidatedMapText.StringArray(
				enemies, IosImportEnvelopePolicyV1.MapPlayerDefinitions, nameof(enemies), sort: true);

			return new ValidatedMapPlayerMetadata(
				name, palette, bot, startingUnitsClass,
				allowBots, playable, required, ownsWorld,
				spectating, nonCombatant, lockFaction, faction,
				lockColor, color, homeLocation, lockSpawn,
				spawn, lockTeam, team, lockHandicap, handicap,
				copiedAllies, copiedEnemies);
		}

		internal IEnumerable<string> ProjectStrings()
		{
			yield return Name;
			if (Palette != null)
				yield return Palette;
			if (Bot != null)
				yield return Bot;
			if (StartingUnitsClass != null)
				yield return StartingUnitsClass;
			if (Faction != null)
				yield return Faction;
			foreach (var ally in Allies)
				yield return ally;
			foreach (var enemy in Enemies)
				yield return enemy;
		}
	}

	public sealed class ValidatedMapMetadata
	{
		public string Title { get; }
		public string Author { get; }
		public ImmutableArray<string> Categories { get; }
		public string Tileset { get; }
		public MapGridType GridType { get; }
		public int2 MapSize { get; }
		public Rectangle Bounds { get; }
		public ImmutableArray<ValidatedMapPlayerMetadata> Players { get; }
		public ImmutableArray<CPos> SpawnPoints { get; }
		public int PlayablePlayers { get; }

		ValidatedMapMetadata(
			string title, string author, ImmutableArray<string> categories,
			string tileset, MapGridType gridType, int2 mapSize, Rectangle bounds,
			ImmutableArray<ValidatedMapPlayerMetadata> players,
			ImmutableArray<CPos> spawnPoints)
		{
			Title = title;
			Author = author;
			Categories = categories;
			Tileset = tileset;
			GridType = gridType;
			MapSize = mapSize;
			Bounds = bounds;
			Players = players;
			SpawnPoints = spawnPoints;
			PlayablePlayers = players.Count(player => player.Playable);
		}

		internal static ValidatedMapMetadata CreateValidated(
			string title, string author, ImmutableArray<string> categories,
			string tileset, MapGridType gridType, int2 mapSize, Rectangle bounds,
			ImmutableArray<ValidatedMapPlayerMetadata> players,
			ImmutableArray<CPos> spawnPoints)
		{
			title = Ra2ValidatedMapText.Required(title, nameof(title));
			author = Ra2ValidatedMapText.Required(author, nameof(author));
			tileset = Ra2ValidatedMapText.Required(tileset, nameof(tileset));
			var copiedCategories = Ra2ValidatedMapText.StringArray(
				categories, IosImportEnvelopePolicyV1.RemoteCategories, nameof(categories), sort: false);
			var copiedPlayers = ValidatePlayers(players);
			var copiedSpawns = ValidateMapShape(gridType, mapSize, bounds, copiedPlayers, spawnPoints);
			ValidatePlayerReferences(copiedPlayers);
			ValidateStringBudget(title, author, tileset, copiedCategories, copiedPlayers);

			return new ValidatedMapMetadata(
				title, author, copiedCategories, tileset, gridType, mapSize, bounds,
				copiedPlayers, copiedSpawns);
		}

		static ImmutableArray<ValidatedMapPlayerMetadata> ValidatePlayers(
			ImmutableArray<ValidatedMapPlayerMetadata> players)
		{
			if (players.IsDefault)
				throw new ArgumentException("The player collection must be initialized.", nameof(players));
			if (players.Length > IosImportEnvelopePolicyV1.MapPlayerDefinitions)
				throw new ArgumentOutOfRangeException(nameof(players));
			if (players.Any(player => player == null))
				throw new ArgumentException("The player collection contains a null value.", nameof(players));

			var exact = new HashSet<string>(StringComparer.Ordinal);
			var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var player in players)
				if (!exact.Add(player.Name) || !aliases.Add(player.Name))
					throw new ArgumentException("Player names must be ordinally unique without case aliases.", nameof(players));

			return players.OrderBy(player => player.Name, StringComparer.Ordinal).ToImmutableArray();
		}

		static ImmutableArray<CPos> ValidateMapShape(
			MapGridType gridType, int2 mapSize, Rectangle bounds,
			ImmutableArray<ValidatedMapPlayerMetadata> players,
			ImmutableArray<CPos> spawnPoints)
		{
			if (gridType != MapGridType.Rectangular && gridType != MapGridType.RectangularIsometric)
				throw new ArgumentOutOfRangeException(nameof(gridType));
			if (mapSize.X <= 0 || mapSize.X > IosImportEnvelopePolicyV1.TraditionalMapWidth ||
				mapSize.Y <= 0 || mapSize.Y > IosImportEnvelopePolicyV1.TraditionalMapHeight ||
				(long)mapSize.X * mapSize.Y > IosImportEnvelopePolicyV1.TraditionalMapCells)
				throw new ArgumentOutOfRangeException(nameof(mapSize));

			var right = (long)bounds.X + bounds.Width;
			var bottom = (long)bounds.Y + bounds.Height;
			if (bounds.X < 0 || bounds.Y < 0 || bounds.Width <= 0 || bounds.Height <= 0 ||
				right > mapSize.X || bottom > mapSize.Y ||
				(long)bounds.Width * bounds.Height > IosImportEnvelopePolicyV1.TraditionalMapCells)
				throw new ArgumentOutOfRangeException(nameof(bounds));

			if (spawnPoints.IsDefault)
				throw new ArgumentException("The spawn collection must be initialized.", nameof(spawnPoints));
			if (spawnPoints.Length > IosImportEnvelopePolicyV1.MapSpawns)
				throw new ArgumentOutOfRangeException(nameof(spawnPoints));

			var uniqueSpawns = new HashSet<CPos>();
			foreach (var spawn in spawnPoints)
			{
				ValidateCell(spawn, gridType, bounds, nameof(spawnPoints));
				if (!uniqueSpawns.Add(spawn))
					throw new ArgumentException("Spawn points must be unique.", nameof(spawnPoints));
			}

			foreach (var player in players)
				if (player.HomeLocation != CPos.Zero)
					ValidateCell(player.HomeLocation, gridType, bounds, nameof(players));

			return ImmutableArray.CreateRange(spawnPoints);
		}

		static void ValidateCell(CPos cell, MapGridType gridType, Rectangle bounds, string parameterName)
		{
			if (cell.Layer != 0 ||
				(gridType == MapGridType.RectangularIsometric && cell.X < cell.Y))
				throw new ArgumentException("The map cell is outside the supported grid projection.", parameterName);

			var projected = cell.ToMPos(gridType);
			if (!bounds.Contains(projected.U, projected.V))
				throw new ArgumentException("The map cell is outside the playable bounds.", parameterName);
		}

		static void ValidatePlayerReferences(ImmutableArray<ValidatedMapPlayerMetadata> players)
		{
			var names = new HashSet<string>(players.Select(player => player.Name), StringComparer.Ordinal);
			foreach (var player in players)
			{
				if (player.Allies.Any(ally => !names.Contains(ally)) ||
					player.Enemies.Any(enemy => !names.Contains(enemy)))
					throw new ArgumentException("Player relationship sets contain a dangling reference.", nameof(players));
			}
		}

		static void ValidateStringBudget(
			string title, string author, string tileset,
			ImmutableArray<string> categories,
			ImmutableArray<ValidatedMapPlayerMetadata> players)
		{
			long count = 0;
			long bytes = 0;
			void Add(string value)
			{
				count = checked(count + 1);
				bytes = checked(bytes + Ra2ValidatedMapText.Utf8Length(value));
				if (count > IosImportEnvelopePolicyV1.ResidentStringEntries ||
					bytes > IosImportEnvelopePolicyV1.PerMapStringBytes)
					throw new ArgumentException("The map metadata string projection exceeds its bounded envelope.");
			}

			Add(title);
			Add(author);
			Add(tileset);
			foreach (var category in categories)
				Add(category);
			foreach (var player in players)
				foreach (var value in player.ProjectStrings())
					Add(value);
		}
	}

	public interface IRa2MapSourceHandle : IDisposable
	{
		string SafeName { get; }
		long Length { get; }
		string SourceSha256 { get; }
		Stream OpenVerifiedRead();
	}

	static class Ra2ValidatedMapText
	{
		static readonly UTF8Encoding StrictUtf8 = new(false, true);

		public static string Required(string value, string parameterName) =>
			Validate(value, parameterName, optional: false);

		public static string Optional(string value, string parameterName) =>
			Validate(value, parameterName, optional: true);

		static string Validate(string value, string parameterName, bool optional)
		{
			if (value == null)
			{
				if (optional)
					return null;

				throw new ArgumentNullException(parameterName);
			}

			if (value.Length == 0 || !value.IsNormalized(NormalizationForm.FormC) ||
				value.Any(character => char.IsControl(character) || character == '\u2028' || character == '\u2029'))
				throw new ArgumentException("The map metadata scalar is not canonical.", parameterName);

			int byteLength;
			try
			{
				byteLength = StrictUtf8.GetByteCount(value);
			}
			catch (EncoderFallbackException exception)
			{
				throw new ArgumentException("The map metadata scalar is not valid UTF-8.", parameterName, exception);
			}

			if (byteLength == 0 || byteLength > IosImportEnvelopePolicyV1.MapYamlScalarBytes)
				throw new ArgumentOutOfRangeException(parameterName);

			return value;
		}

		public static ImmutableArray<string> StringArray(
			ImmutableArray<string> values, int maximum, string parameterName, bool sort)
		{
			if (values.IsDefault)
				throw new ArgumentException("The string collection must be initialized.", parameterName);
			if (values.Length > maximum)
				throw new ArgumentOutOfRangeException(parameterName);

			var exact = new HashSet<string>(StringComparer.Ordinal);
			var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var copy = new string[values.Length];
			for (var i = 0; i < values.Length; i++)
			{
				copy[i] = Required(values[i], parameterName);
				if (!exact.Add(copy[i]) || !aliases.Add(copy[i]))
					throw new ArgumentException("The string collection contains a duplicate or case alias.", parameterName);
			}

			if (sort)
				Array.Sort(copy, StringComparer.Ordinal);

			return ImmutableArray.Create(copy);
		}

		public static int Utf8Length(string value) => StrictUtf8.GetByteCount(value);
	}
}
