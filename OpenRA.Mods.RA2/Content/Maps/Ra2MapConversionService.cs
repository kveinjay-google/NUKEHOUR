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
using System.IO;
using OpenRA.FileSystem;
using OpenRA.Mods.RA2.UtilityCommands;

namespace OpenRA.Mods.RA2.Content
{
	public sealed class Ra2MapConversionService
	{
		public int Convert(
			ModData modData,
			IRa2MapSourceHandle source,
			string destinationPath)
		{
			if (modData == null)
				throw new ArgumentNullException(nameof(modData));
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (source.Length <= 0 || source.Length > IosImportEnvelopePolicyV1.TraditionalMapSourceBytes)
				throw new InvalidDataException("The traditional map source exceeds the supported envelope.");
			if (string.IsNullOrEmpty(destinationPath))
				throw new ArgumentException("The destination path is required.", nameof(destinationPath));

			var extension = Path.GetExtension(source.SafeName);
			if (!extension.Equals(".map", StringComparison.OrdinalIgnoreCase) &&
				!extension.Equals(".mpr", StringComparison.OrdinalIgnoreCase) &&
				!extension.Equals(".yrm", StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException("The embedded source is not a supported traditional map.");

			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath)));
			using var input = source.OpenVerifiedRead();
			if (!input.CanRead || !input.CanSeek || input.Position != 0 || input.Length != source.Length)
				throw new InvalidDataException("The verified map source changed before conversion.");

			using var output = ZipFileLoader.Create(destinationPath);
			return ImportRA2MapCommand.ConvertMap(modData, input, source.SafeName, output);
		}
	}
}
