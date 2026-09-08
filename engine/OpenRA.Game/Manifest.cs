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
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using OpenRA.FileSystem;
using OpenRA.Primitives;

namespace OpenRA
{
	public interface IGlobalModData { }

	public sealed class TerrainFormat : IGlobalModData
	{
		public readonly string Type;
		public readonly IReadOnlyDictionary<string, MiniYaml> Metadata;
		public TerrainFormat(MiniYaml yaml)
		{
			Type = yaml.Value;
			Metadata = new ReadOnlyDictionary<string, MiniYaml>(yaml.ToDictionary());
		}
	}

	public sealed class SpriteSequenceFormat : IGlobalModData
	{
		public readonly string Type;
		public readonly IReadOnlyDictionary<string, MiniYaml> Metadata;
		public SpriteSequenceFormat(MiniYaml yaml)
		{
			Type = yaml.Value;
			Metadata = new ReadOnlyDictionary<string, MiniYaml>(yaml.ToDictionary());
		}
	}

	public class ModMetadata
	{
		// FieldLoader used here, must matching naming in YAML.
#pragma warning disable IDE1006 // Naming Styles
		[FluentReference]
		readonly string Title;
		public readonly string Version;
		public readonly string Website;
		public readonly string WebIcon32;
		[FluentReference]
		readonly string WindowTitle;
		public readonly bool Hidden;
#pragma warning restore IDE1006 // Naming Styles

		public string TitleTranslated => FluentProvider.GetMessage(Title);
		public string WindowTitleTranslated => WindowTitle != null ? FluentProvider.GetMessage(WindowTitle) : null;
	}

	/// <summary>Describes what is to be loaded in order to run a mod.</summary>
	public sealed class Manifest : IDisposable
	{
		public readonly string Id;
		public readonly IReadOnlyPackage Package;
		public string EntryName { get; }
		public ImmutableArray<string> IncludedFiles { get; }
		public ImmutableArray<string> RuntimeContractCultures { get; }
		public ImmutableArray<string> RuntimeContractFiles { get; }
		public readonly ModMetadata Metadata;
		public readonly string[]
			Rules, ServerTraits,
			Sequences, ModelSequences, Cursors, Chrome, ChromeLayout,
			Weapons, Voices, Notifications, Music, FluentMessages, TileSets,
			ChromeMetrics, MapCompatibility, Missions, Hotkeys;

		public readonly IReadOnlyDictionary<string, string> MapFolders;
		public readonly MiniYaml FileSystem;
		public readonly MiniYaml LoadScreen;
		public readonly string DefaultOrderGenerator;

		public readonly string[] Assemblies = Array.Empty<string>();
		public readonly string[] SoundFormats = Array.Empty<string>();
		public readonly string[] SpriteFormats = Array.Empty<string>();
		public readonly string[] PackageFormats = Array.Empty<string>();
		public readonly string[] VideoFormats = Array.Empty<string>();
		public readonly int FontSheetSize = 512;
		public readonly int CursorSheetSize = 512;

		// TODO: This should be controlled by a user-selected translation bundle!
		public readonly string FluentCulture = "en";
		public readonly bool AllowUnusedFluentMessagesInExternalPackages = true;

		readonly string[] reservedModuleNames =
		{
			"Include", "Metadata", "FileSystem", "MapFolders", "Rules",
			"Sequences", "ModelSequences", "Cursors", "Chrome", "Assemblies", "ChromeLayout", "Weapons",
			"Voices", "Notifications", "Music", "FluentMessages", "TileSets", "ChromeMetrics", "Missions", "Hotkeys",
			"ServerTraits", "LoadScreen", "DefaultOrderGenerator", "SupportsMapsFrom", "SoundFormats", "SpriteFormats", "VideoFormats",
			"RequiresMods", "PackageFormats", "AllowUnusedFluentMessagesInExternalPackages", "FontSheetSize", "CursorSheetSize",
			"RuntimeContractCultures"
		};

		static readonly HashSet<string> RuntimeFileSectionNames = new(StringComparer.Ordinal)
		{
			"Rules", "Sequences", "ModelSequences", "Cursors", "Chrome", "ChromeLayout",
			"Weapons", "Voices", "Notifications", "Music", "FluentMessages", "TileSets",
			"ChromeMetrics", "Missions", "Hotkeys"
		};

		readonly TypeDictionary modules = new();
		readonly Dictionary<string, MiniYaml> yaml;

		bool customDataLoaded;
		int disposeState;

		public Manifest(string modId, IReadOnlyPackage package, string entryName = "mod.yaml")
		{
			var selection = new TrustedManifestSelection(modId, entryName);
			Id = selection.ModId;
			EntryName = selection.EntryName;
			Package = package ?? throw new ArgumentNullException(nameof(package));

			var stringPool = new HashSet<string>(); // Reuse common strings in YAML
			var root = package.GetStream(EntryName);
			if (root == null)
				throw new YamlException($"{package.Name}:{EntryName}: Manifest entry not found.");

			var rootNodes = MiniYaml.FromStream(root, $"{package.Name}:{EntryName}", stringPool: stringPool);
			var resolvedNodes = new List<MiniYamlNode>();
			var includedFiles = ImmutableArray.CreateBuilder<string>();
			var exactIncludes = new HashSet<string>(StringComparer.Ordinal);
			var caseIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var node in rootNodes)
			{
				if (node.Key != "Include")
				{
					resolvedNodes.Add(node);
					continue;
				}

				if (node.Value.Nodes.Any())
					throw new YamlException($"{node.Location}: Include must be one scalar path.");
				var filename = ValidateIncludePath(node.Value.Value, node.Location);
				if (!exactIncludes.Add(filename) || !caseIncludes.Add(filename))
					throw new YamlException($"{node.Location}: Duplicate manifest Include `{filename}`.");
				var contents = package.GetStream(filename);
				if (contents == null)
					throw new YamlException($"{node.Location}: File `{filename}` not found.");

				var fragment = MiniYaml.FromStream(contents, $"{package.Name}:{filename}", stringPool: stringPool);
				if (fragment.Any(fragmentNode => fragmentNode.Key == "Include"))
					throw new YamlException($"{package.Name}:{filename}: Manifest fragments cannot contain Include.");

				includedFiles.Add(filename);
				resolvedNodes.AddRange(fragment);
			}

			IncludedFiles = includedFiles.ToImmutable();

			// Merge inherited overrides
			yaml = new MiniYaml(null, MiniYaml.Merge(new[] { resolvedNodes })).ToDictionary();

			Metadata = FieldLoader.Load<ModMetadata>(yaml["Metadata"]);

			// TODO: Use fieldloader
			MapFolders = YamlDictionary(yaml, "MapFolders");

			if (!yaml.TryGetValue("FileSystem", out FileSystem))
				throw new InvalidDataException("`FileSystem` section is not defined.");

			Rules = YamlList(yaml, "Rules");
			Sequences = YamlList(yaml, "Sequences");
			ModelSequences = YamlList(yaml, "ModelSequences");
			Cursors = YamlList(yaml, "Cursors");
			Chrome = YamlList(yaml, "Chrome");
			ChromeLayout = YamlList(yaml, "ChromeLayout");
			Weapons = YamlList(yaml, "Weapons");
			Voices = YamlList(yaml, "Voices");
			Notifications = YamlList(yaml, "Notifications");
			Music = YamlList(yaml, "Music");
			FluentMessages = YamlList(yaml, "FluentMessages");
			TileSets = YamlList(yaml, "TileSets");
			ChromeMetrics = YamlList(yaml, "ChromeMetrics");
			Missions = YamlList(yaml, "Missions");
			Hotkeys = YamlList(yaml, "Hotkeys");
			RuntimeContractCultures = RuntimeCultures(yaml);
			if (!RuntimeContractCultures.IsDefaultOrEmpty)
				ValidateRuntimeFileSourceRows(resolvedNodes);
			RuntimeContractFiles = RuntimeFiles();

			ServerTraits = YamlList(yaml, "ServerTraits");

			if (!yaml.TryGetValue("LoadScreen", out LoadScreen))
				throw new InvalidDataException("`LoadScreen` section is not defined.");

			// Allow inherited mods to import parent maps.
			var compat = new List<string> { Id };

			if (yaml.TryGetValue("SupportsMapsFrom", out var entry))
				compat.AddRange(entry.Value.Split(',').Select(c => c.Trim()));

			MapCompatibility = RuntimeContractCultures.IsDefaultOrEmpty ?
				compat.Distinct(StringComparer.Ordinal).ToArray() : CanonicalMapCompatibility(compat);

			if (yaml.TryGetValue("DefaultOrderGenerator", out entry))
				DefaultOrderGenerator = entry.Value;

			if (yaml.TryGetValue("Assemblies", out entry))
				Assemblies = FieldLoader.GetValue<string[]>("Assemblies", entry.Value);

			if (yaml.TryGetValue("PackageFormats", out entry))
				PackageFormats = FieldLoader.GetValue<string[]>("PackageFormats", entry.Value);

			if (yaml.TryGetValue("SoundFormats", out entry))
				SoundFormats = FieldLoader.GetValue<string[]>("SoundFormats", entry.Value);

			if (yaml.TryGetValue("SpriteFormats", out entry))
				SpriteFormats = FieldLoader.GetValue<string[]>("SpriteFormats", entry.Value);

			if (yaml.TryGetValue("VideoFormats", out entry))
				VideoFormats = FieldLoader.GetValue<string[]>("VideoFormats", entry.Value);

			if (yaml.TryGetValue("AllowUnusedFluentMessagesInExternalPackages", out entry))
				AllowUnusedFluentMessagesInExternalPackages =
					FieldLoader.GetValue<bool>("AllowUnusedFluentMessagesInExternalPackages", entry.Value);

			if (yaml.TryGetValue("FontSheetSize", out entry))
				FontSheetSize = FieldLoader.GetValue<int>("FontSheetSize", entry.Value);

			if (yaml.TryGetValue("CursorSheetSize", out entry))
				CursorSheetSize = FieldLoader.GetValue<int>("CursorSheetSize", entry.Value);
		}

		static string ValidateIncludePath(string value, MiniYamlNode.SourceLocation location)
		{
			if (string.IsNullOrEmpty(value) || value[0] == '/' || value.IndexOfAny(new[] { '\\', '\0', '|' }) >= 0)
				throw new YamlException($"{location}: Include path is not canonical.");
			foreach (var character in value)
				if (char.IsControl(character))
					throw new YamlException($"{location}: Include path contains a control character.");

			var segments = value.Split('/');
			if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
				throw new YamlException($"{location}: Include path is not canonical.");
			if (segments[0].Contains(':'))
				throw new YamlException($"{location}: Include path cannot be absolute or package-prefixed.");

			return value;
		}

		static ImmutableArray<string> RuntimeCultures(Dictionary<string, MiniYaml> yaml)
		{
			if (!yaml.TryGetValue("RuntimeContractCultures", out var value))
				return ImmutableArray<string>.Empty;

			var builder = ImmutableArray.CreateBuilder<string>();
			var exact = new HashSet<string>(StringComparer.Ordinal);
			var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var node in value.Nodes)
			{
				var culture = node.Key;
				if (!IsCanonicalCulture(culture) || !exact.Add(culture) || !aliases.Add(culture))
					throw new YamlException($"{node.Location}: Runtime contract culture is not canonical or unique.");

				builder.Add(culture);
			}

			builder.Sort(CompareUtf8Ordinal);
			return builder.ToImmutable();
		}

		static string[] CanonicalMapCompatibility(IEnumerable<string> values)
		{
			var result = new List<string>();
			var exact = new HashSet<string>(StringComparer.Ordinal);
			var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var value in values)
			{
				if (!IsCanonicalRuntimeSegment(value))
					throw new YamlException($"Runtime map compatibility `{value}` is not canonical.");

				try
				{
					if (!value.IsNormalized(NormalizationForm.FormC))
						throw new YamlException($"Runtime map compatibility `{value}` is not NFC.");
				}
				catch (ArgumentException)
				{
					throw new YamlException("Runtime map compatibility contains invalid Unicode.");
				}

				if (!exact.Add(value))
					continue;
				if (!aliases.Add(value))
					throw new YamlException(
						$"Runtime map compatibility `{value}` is case-aliased.");

				result.Add(value);
			}

			result.Sort(CompareUtf8Ordinal);
			return result.ToArray();
		}

		static void ValidateRuntimeFileSourceRows(IEnumerable<MiniYamlNode> sourceNodes)
		{
			var exact = new HashSet<string>(StringComparer.Ordinal);
			var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var section in sourceNodes)
			{
				if (!RuntimeFileSectionNames.Contains(section.Key))
					continue;

				foreach (var row in section.Value.Nodes)
					if (!exact.Add(row.Key) || !aliases.Add(row.Key))
						throw new YamlException(
							$"{row.Location}: Runtime contract file row `{row.Key}` is duplicated or case-aliased.");
			}
		}

		static bool IsCanonicalCulture(string culture)
		{
			if (string.IsNullOrEmpty(culture) || culture.Contains('\0'))
				return false;

			try
			{
				return CultureInfo.GetCultureInfo(culture).Name == culture;
			}
			catch (CultureNotFoundException)
			{
				return false;
			}
		}

		ImmutableArray<string> RuntimeFiles()
		{
			// Existing desktop manifests do not opt into the runtime contract and must
			// retain their historical parsing behavior.
			if (RuntimeContractCultures.IsDefaultOrEmpty)
				return ImmutableArray<string>.Empty;

			var packageAliases = RuntimePackageAliases(FileSystem);
			var files = new List<string>();
			var exact = new HashSet<string>(StringComparer.Ordinal);
			var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			void AddCanonical(string path)
			{
				ValidateRuntimeRelativePath(path);
				if (!exact.Add(path) || !aliases.Add(path))
					throw new YamlException($"Runtime contract path `{path}` is duplicated or case-aliased.");

				files.Add(path);
			}

			void AddManifestPath(string path) =>
				AddCanonical(CanonicalRuntimePath(path, Id, packageAliases));

			AddCanonical($"mods/{Id}/{EntryName}");
			foreach (var includedFile in IncludedFiles)
				AddCanonical($"mods/{Id}/{includedFile}");

			foreach (var path in Rules
				.Concat(Sequences)
				.Concat(ModelSequences)
				.Concat(Cursors)
				.Concat(Chrome)
				.Concat(ChromeLayout)
				.Concat(Weapons)
				.Concat(Voices)
				.Concat(Notifications)
				.Concat(Music)
				.Concat(TileSets)
				.Concat(ChromeMetrics)
				.Concat(Missions)
				.Concat(Hotkeys))
				AddManifestPath(path);

			foreach (var fluentPath in FluentMessages)
			{
				var canonical = CanonicalRuntimePath(fluentPath, Id, packageAliases);
				AddCanonical(canonical);
				var filenameOffset = canonical.LastIndexOf('/') + 1;
				foreach (var culture in RuntimeContractCultures)
					AddCanonical(canonical.Insert(filenameOffset, culture + "/"));
			}

			files.Sort(CompareUtf8Ordinal);
			return files.ToImmutableArray();
		}

		static Dictionary<string, string> RuntimePackageAliases(MiniYaml fileSystem)
		{
			var result = new Dictionary<string, string>(StringComparer.Ordinal);
			var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var sections = fileSystem.Nodes.Where(node => node.Key == "SystemPackages").ToArray();
			if (sections.Length > 1)
				throw new YamlException("Runtime contract SystemPackages must be unique.");
			if (sections.Length == 0)
				return result;

			foreach (var node in sections[0].Value.Nodes)
			{
				var alias = node.Value.Value;
				if (string.IsNullOrEmpty(alias))
					continue;
				if (!IsCanonicalRuntimeSegment(alias) || !aliases.Add(alias))
					throw new YamlException($"Runtime contract package alias `{alias}` is not canonical or unique.");

				string root = null;
				if (node.Key.Length > 1 && node.Key[0] == '$' &&
					IsCanonicalRuntimeSegment(node.Key[1..]))
					root = "mods/" + node.Key[1..];
				else
				{
					const string EngineModPrefix = "^EngineDir|mods/";
					if (node.Key.StartsWith(EngineModPrefix, StringComparison.Ordinal) &&
						IsCanonicalRuntimeSegment(node.Key[EngineModPrefix.Length..]))
						root = "mods/" + node.Key[EngineModPrefix.Length..];
				}

				result.Add(alias, root);
			}

			return result;
		}

		static string CanonicalRuntimePath(
			string value, string modId, IReadOnlyDictionary<string, string> packageAliases)
		{
			if (string.IsNullOrEmpty(value) || value[0] == '~')
				throw new YamlException("Runtime contract file path is not canonical.");

			var separator = value.IndexOf('|');
			if (separator < 0)
			{
				ValidateRuntimeRelativePath(value);
				return $"mods/{modId}/{value}";
			}

			if (separator == 0 || separator != value.LastIndexOf('|'))
				throw new YamlException($"Runtime contract file path `{value}` is not canonical.");

			var alias = value[..separator];
			var relativePath = value[(separator + 1)..];
			if (!IsCanonicalRuntimeSegment(alias) ||
				!packageAliases.TryGetValue(alias, out var root) || root == null)
				throw new YamlException($"Runtime contract package alias `{alias}` is not trusted.");

			ValidateRuntimeRelativePath(relativePath);
			return root + "/" + relativePath;
		}

		static void ValidateRuntimeRelativePath(string value)
		{
			if (string.IsNullOrEmpty(value) || value[0] == '/' ||
				value.IndexOfAny(new[] { '\\', '\0', '|' }) >= 0)
				throw new YamlException($"Runtime contract path `{value}` is not canonical.");

			try
			{
				if (!value.IsNormalized(NormalizationForm.FormC))
					throw new YamlException($"Runtime contract path `{value}` is not NFC.");
			}
			catch (ArgumentException)
			{
				throw new YamlException("Runtime contract path contains invalid Unicode.");
			}

			if (value.Any(char.IsControl))
				throw new YamlException($"Runtime contract path `{value}` contains a control character.");

			var segments = value.Split('/');
			if (segments.Any(segment => !IsCanonicalRuntimeSegment(segment)))
				throw new YamlException($"Runtime contract path `{value}` is not canonical.");
		}

		static bool IsCanonicalRuntimeSegment(string value) =>
			!string.IsNullOrEmpty(value) && value is not "." and not ".." &&
			value.IndexOfAny(new[] { '/', '\\', '\0', '|', ':' }) < 0 &&
			!value.Any(char.IsControl);

		static int CompareUtf8Ordinal(string left, string right)
		{
			var leftBytes = Encoding.UTF8.GetBytes(left);
			var rightBytes = Encoding.UTF8.GetBytes(right);
			var shared = Math.Min(leftBytes.Length, rightBytes.Length);
			for (var i = 0; i < shared; i++)
			{
				var comparison = leftBytes[i].CompareTo(rightBytes[i]);
				if (comparison != 0)
					return comparison;
			}

			return leftBytes.Length.CompareTo(rightBytes.Length);
		}

		public void LoadCustomData(ObjectCreator oc)
		{
			foreach (var kv in yaml)
			{
				if (reservedModuleNames.Contains(kv.Key))
					continue;

				var t = oc.FindType(kv.Key);
				if (t == null || !typeof(IGlobalModData).IsAssignableFrom(t))
					throw new InvalidDataException($"`{kv.Key}` is not a valid mod manifest entry.");

				IGlobalModData module;
				var ctor = t.GetConstructor(new[] { typeof(MiniYaml) });
				if (ctor != null)
				{
					// Class has opted-in to DIY initialization
					module = (IGlobalModData)ctor.Invoke(new object[] { kv.Value });
				}
				else
				{
					// Automatically load the child nodes using FieldLoader
					module = oc.CreateObject<IGlobalModData>(kv.Key);
					FieldLoader.Load(module, kv.Value);
				}

				modules.Add(module);
			}

			customDataLoaded = true;
		}

		static string[] YamlList(Dictionary<string, MiniYaml> yaml, string key)
		{
			if (!yaml.TryGetValue(key, out var value))
				return Array.Empty<string>();

			return value.Nodes.Select(n => n.Key).ToArray();
		}

		static IReadOnlyDictionary<string, string> YamlDictionary(Dictionary<string, MiniYaml> yaml, string key)
		{
			if (!yaml.TryGetValue(key, out var value))
				return new Dictionary<string, string>();

			return value.ToDictionary(my => my.Value);
		}

		public bool Contains<T>() where T : IGlobalModData
		{
			return modules.Contains<T>();
		}

		/// <summary>Load a cached IGlobalModData instance.</summary>
		public T Get<T>() where T : IGlobalModData
		{
			if (!customDataLoaded)
				throw new InvalidOperationException("Attempted to call Manifest.Get() before loading custom data!");

			var module = modules.GetOrDefault<T>();

			// Lazily create the default values if not explicitly defined.
			if (module == null)
			{
				module = (T)Game.ModData.ObjectCreator.CreateBasic(typeof(T));
				modules.Add(module);
			}

			return module;
		}

		/// <summary>
		/// Load an uncached IGlobalModData instance directly from the manifest yaml.
		/// This should only be used by external mods that want to query data from this mod.
		/// </summary>
		public T Get<T>(ObjectCreator oc) where T : IGlobalModData
		{
			var t = typeof(T);
			if (!yaml.TryGetValue(t.Name, out var data))
			{
				// Lazily create the default values if not explicitly defined.
				return (T)oc.CreateBasic(t);
			}

			IGlobalModData module;
			var ctor = t.GetConstructor(new[] { typeof(MiniYaml) });
			if (ctor != null)
			{
				// Class has opted-in to DIY initialization
				module = (IGlobalModData)ctor.Invoke(new object[] { data });
			}
			else
			{
				// Automatically load the child nodes using FieldLoader
				module = oc.CreateObject<IGlobalModData>(t.Name);
				FieldLoader.Load(module, data);
			}

			return (T)module;
		}

		public void Dispose()
		{
			if (Interlocked.Exchange(ref disposeState, 1) != 0)
				return;

			var disposableModules = modules.OfType<IDisposable>().ToArray();
			ExceptionDispatchInfo firstFailure = null;
			foreach (var module in disposableModules)
				try { module.Dispose(); }
				catch (Exception exception)
				{
					firstFailure ??= ExceptionDispatchInfo.Capture(exception);
				}

			firstFailure?.Throw();
		}
	}
}
