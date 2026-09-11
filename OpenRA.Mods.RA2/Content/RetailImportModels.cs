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
using System.Collections.ObjectModel;
using System.Linq;

namespace OpenRA.Mods.RA2.Content
{
	public enum ContentState
	{
		Missing,
		Incomplete,
		Ready,
		Unsupported,
	}

	public sealed class RetailContentStatus
	{
		public ContentState Base { get; }
		public ContentState Yuri { get; }
		public ContentState Audio { get; }
		public ContentState Maps { get; }
		public IReadOnlyList<string> MissingBaseFiles { get; }
		public IReadOnlyList<string> MissingYuriFiles { get; }
		public IReadOnlyList<string> MissingAudioFiles { get; }

		public bool CanLaunchBaseGame => Base == ContentState.Ready;
		public bool CanLaunchYuriGame => CanLaunchBaseGame && Yuri == ContentState.Ready;

		public RetailContentStatus(
			ContentState baseState,
			ContentState yuriState,
			ContentState audioState,
			ContentState mapsState,
			string[] missingBaseFiles,
			string[] missingYuriFiles,
			string[] missingAudioFiles)
		{
			Base = baseState;
			Yuri = yuriState;
			Audio = audioState;
			Maps = mapsState;
			MissingBaseFiles = Array.AsReadOnly((string[])missingBaseFiles.Clone());
			MissingYuriFiles = Array.AsReadOnly((string[])missingYuriFiles.Clone());
			MissingAudioFiles = Array.AsReadOnly((string[])missingAudioFiles.Clone());
		}
	}

	public enum RetailImportError
	{
		None,
		Cancelled,
		UnsupportedFile,
		ExecutableContent,
		InsufficientSpace,
		IncompleteBaseContent,
		DuplicateFile,
		IoFailure,
	}

	public sealed class RetailImportRequest
	{
		public IReadOnlyList<string> SourceFiles { get; }
		public string ContentRoot { get; }
		public long? AvailableBytes { get; }

		public RetailImportRequest(IEnumerable<string> sourceFiles, string contentRoot, long? availableBytes = null)
		{
			if (sourceFiles == null)
				throw new ArgumentNullException(nameof(sourceFiles));

			SourceFiles = Array.AsReadOnly(sourceFiles.ToArray());
			ContentRoot = contentRoot ?? throw new ArgumentNullException(nameof(contentRoot));
			AvailableBytes = availableBytes;
		}
	}

	public sealed class RetailImportResult
	{
		public bool Published { get; }
		public RetailImportError Error { get; }
		public string Message { get; }
		public RetailContentStatus Status { get; }
		public IReadOnlyDictionary<string, string> Hashes { get; }

		public RetailImportResult(
			bool published,
			RetailImportError error,
			string message,
			RetailContentStatus status,
			IDictionary<string, string> hashes)
		{
			Published = published;
			Error = error;
			Message = message ?? string.Empty;
			Status = status;
			Hashes = new ReadOnlyDictionary<string, string>(
				new Dictionary<string, string>(hashes ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase));
		}
	}
}
