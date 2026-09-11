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
using System.Linq;

namespace OpenRA.Mods.RA2.Content
{
	public sealed class RetailContentCatalog
	{
		static readonly string[] BaseFiles = { "ra2.mix", "language.mix" };
		static readonly string[] YuriFiles = { "ra2md.mix", "langmd.mix" };
		// The retail audio.bag/audio.idx files are nested inside audio.mix, which is
		// itself nested inside language.mix (and langmd.mix for the expansion).
		// They are therefore not expected to exist as loose imported files.
		static readonly string[] AudioFiles = { "language.mix" };
		static readonly HashSet<string> MapExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			// Legacy Westwood maps must first be converted into a loadable package.
			// Counting them here caused the native wizard to report maps as ready even
			// though MapCache could not discover them.
			".oramap",
		};

		public RetailContentStatus Inspect(string directory)
		{
			if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
				return Inspect(Array.Empty<string>());

			return Inspect(Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories));
		}

		public RetailContentStatus Inspect(IEnumerable<string> paths)
		{
			if (paths == null)
				throw new ArgumentNullException(nameof(paths));

			var materializedPaths = paths.Where(path => !string.IsNullOrEmpty(path)).ToArray();
			var names = new HashSet<string>(
				materializedPaths.Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);

			var missingBase = Missing(names, BaseFiles);
			var missingYuri = Missing(names, YuriFiles);
			var missingAudio = Missing(names, AudioFiles);
			var hasMap = materializedPaths.Any(path => MapExtensions.Contains(Path.GetExtension(path)));

			return new RetailContentStatus(
				State(BaseFiles.Length, missingBase.Length),
				State(YuriFiles.Length, missingYuri.Length),
				State(AudioFiles.Length, missingAudio.Length),
				hasMap ? ContentState.Ready : ContentState.Missing,
				missingBase,
				missingYuri,
				missingAudio);
		}

		static string[] Missing(HashSet<string> names, IEnumerable<string> required)
		{
			return required.Where(file => !names.Contains(file)).ToArray();
		}

		static ContentState State(int requiredCount, int missingCount)
		{
			if (missingCount == requiredCount)
				return ContentState.Missing;

			return missingCount == 0 ? ContentState.Ready : ContentState.Incomplete;
		}
	}
}
