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
using System.IO;

namespace OpenRA.Mods.RA2.Content
{
	public enum ContentSafetyViolation
	{
		None,
		UnsupportedFile,
		ExecutableContent,
		MapOverride,
	}

	public static class PublicContentSafetyPolicy
	{
		static readonly HashSet<string> AcceptedDataExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".aud", ".bag", ".idx", ".map", ".mix", ".mpr", ".oramap", ".wav", ".yrm",
		};

		static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".dll", ".dylib", ".exe", ".so",
		};

		static readonly HashSet<string> MapOverrideDirectories = new(StringComparer.OrdinalIgnoreCase)
		{
			"assemblies", "mods", "rules", "scripts", "weapons",
		};

		public static ContentSafetyViolation ValidateImportFile(string path)
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				return ContentSafetyViolation.UnsupportedFile;

			var extension = Path.GetExtension(path);
			if (ExecutableExtensions.Contains(extension) || HasExecutableMagic(path))
				return ContentSafetyViolation.ExecutableContent;

			return AcceptedDataExtensions.Contains(extension)
				? ContentSafetyViolation.None
				: ContentSafetyViolation.UnsupportedFile;
		}

		public static ContentSafetyViolation ValidateMapPackageEntries(IEnumerable<string> entries)
		{
			if (entries == null)
				throw new ArgumentNullException(nameof(entries));

			foreach (var unnormalizedEntry in entries)
			{
				var entry = (unnormalizedEntry ?? string.Empty).Replace('\\', '/').TrimStart('/');
				var segments = entry.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
				if (segments.Length == 0)
					continue;

				if (Array.Exists(segments, segment =>
					segment.EndsWith(".app", StringComparison.OrdinalIgnoreCase) ||
					segment.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase) ||
					segment.EndsWith(".framework", StringComparison.OrdinalIgnoreCase)))
					return ContentSafetyViolation.ExecutableContent;

				if (MapOverrideDirectories.Contains(segments[0]) ||
					entry.EndsWith("mod.yaml", StringComparison.OrdinalIgnoreCase) ||
					entry.EndsWith("rules.yaml", StringComparison.OrdinalIgnoreCase) ||
					entry.EndsWith("weapons.yaml", StringComparison.OrdinalIgnoreCase) ||
					entry.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
					return ContentSafetyViolation.MapOverride;

				if (ExecutableExtensions.Contains(Path.GetExtension(entry)))
					return ContentSafetyViolation.ExecutableContent;
			}

			return ContentSafetyViolation.None;
		}

		static bool HasExecutableMagic(string path)
		{
			var header = new byte[4];
			using var stream = File.OpenRead(path);
			if (stream.Read(header, 0, header.Length) < header.Length)
				return false;

			var magic = ((uint)header[0] << 24) | ((uint)header[1] << 16) | ((uint)header[2] << 8) | header[3];
			return magic == 0xCAFEBABE || magic == 0xBEBAFECA ||
				magic == 0xFEEDFACE || magic == 0xCEFAEDFE ||
				magic == 0xFEEDFACF || magic == 0xCFFAEDFE ||
				magic == 0x7F454C46 ||
				(header[0] == 'M' && header[1] == 'Z');
		}
	}
}
