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
	static class RetailSafeBasename
	{
		public static string Validate(string value, string parameterName, bool optional = false)
		{
			if (value == null)
			{
				if (optional)
					return null;

				throw new ArgumentNullException(parameterName);
			}

			if (value.Length == 0 || value == "." || value == ".." ||
				value.IndexOfAny(new[] { '/', '\\', '\0', ':' }) >= 0 ||
				value.Any(character => char.IsControl(character) || character == '\u2028' || character == '\u2029'))
				throw new ArgumentException("A safe basename must not contain path or control syntax.", parameterName);

			return value;
		}
	}

	public enum RetailContentGroup
	{
		Base,
		Ra2Music,
		Yuri,
		YuriMusic,
		Maps,
	}

	public enum RetailGroupValidation
	{
		Missing,
		Invalid,
		Unavailable,
		Ready,
	}

	public enum RetailEditionRecognition
	{
		NotApplicable,
		Recognized,
		CompatibleUnrecognized,
	}

	public enum RetailImportPhase
	{
		PreparingSources,
		Discovering,
		Planning,
		Copying,
		VerifyingActiveHashes,
		ValidatingEnvelopes,
		ValidatingArchives,
		ValidatingAudio,
		ConvertingMaps,
		LoadingMaps,
		DownloadingMap,
		ValidatingDownloadedMap,
		SelectingProfile,
		ValidatingMounts,
		CheckingRuntimeResources,
		Committing,
		Recovering,
	}

	public enum RetailImportErrorCode
	{
		Cancelled,
		EmptySelection,
		ExtractInstallationFirst,
		ExecutableSelection,
		ProviderUnavailable,
		ProviderMaterializationRequired,
		SourcePermissionDenied,
		SourceAccessExpired,
		SourceChanged,
		SourceLimitExceeded,
		ValidationTimedOut,
		DuplicateConflict,
		InsufficientSpace,
		CrossVolume,
		CorruptArchive,
		MissingNestedBasePackage,
		InvalidAudio,
		IncompleteBase,
		PartialYuri,
		NoPlayableMap,
		MapConversionFailed,
		UnsafeMap,
		DownloadedMapInvalid,
		DownloadedMapUidMismatch,
		ProfileUnavailable,
		ProfileContextFailure,
		RequiredMountFailure,
		RuntimeResourcesMissing,
		ReceiptMismatch,
		ColdHashMismatch,
		CorruptJournal,
		RecoveryFailed,
		PublishFailed,
		RollbackFailed,
		PendingExpired,
		UpdateRequired,
		ResetRequired,
		LegacyMigrationFailed,
		StoragePolicyFailure,
		SanitizedIoFailure,
	}

	public enum RetailImportDiagnosticCode
	{
		None,
		ProviderNeedsDownload,
		ProviderOffline,
		SecurityScopeDenied,
		DeadlineExceeded,
		LengthMismatch,
		HashMismatch,
		MalformedEnvelope,
		CorruptMapSource,
		UnsupportedMapFeature,
		InvalidSpawnLayout,
		ConversionOutputFailure,
		ArithmeticOverflow,
		MissingRequiredArchive,
		InvalidMapPackage,
		ProfileMetadataMismatch,
		PointerMismatch,
		JournalInvalid,
		GenerationInvalid,
		InsufficientCapacity,
	}

	public sealed record RetailImportFailure
	{
		public RetailImportErrorCode Code { get; }
		public RetailContentGroup? Group { get; }
		public string SafeBasename { get; }
		public RetailImportDiagnosticCode Diagnostic { get; }

		public RetailImportFailure(RetailImportErrorCode code, RetailContentGroup? group, string safeBasename, RetailImportDiagnosticCode diagnostic)
		{
			if (!Enum.IsDefined(typeof(RetailImportErrorCode), code))
				throw new ArgumentOutOfRangeException(nameof(code));
			if (group.HasValue && !Enum.IsDefined(typeof(RetailContentGroup), group.Value))
				throw new ArgumentOutOfRangeException(nameof(group));
			if (!Enum.IsDefined(typeof(RetailImportDiagnosticCode), diagnostic))
				throw new ArgumentOutOfRangeException(nameof(diagnostic));
			RetailSafeBasename.Validate(safeBasename, nameof(safeBasename), optional: true);

			Code = code;
			Group = group;
			SafeBasename = safeBasename;
			Diagnostic = diagnostic;
		}
	}

	public sealed record RetailGroupStatus
	{
		public RetailGroupValidation Validation { get; }
		public RetailEditionRecognition Recognition { get; }
		public RetailImportDiagnosticCode Diagnostic { get; }

		public RetailGroupStatus(RetailGroupValidation validation, RetailEditionRecognition recognition, RetailImportDiagnosticCode diagnostic)
		{
			if (!Enum.IsDefined(typeof(RetailGroupValidation), validation))
				throw new ArgumentOutOfRangeException(nameof(validation));
			if (!Enum.IsDefined(typeof(RetailEditionRecognition), recognition))
				throw new ArgumentOutOfRangeException(nameof(recognition));
			if (!Enum.IsDefined(typeof(RetailImportDiagnosticCode), diagnostic))
				throw new ArgumentOutOfRangeException(nameof(diagnostic));

			Validation = validation;
			Recognition = recognition;
			Diagnostic = diagnostic;
		}
	}

	public sealed class RetailOperationResult<T>
	{
		readonly T value;
		readonly RetailImportFailure failure;
		public bool Succeeded { get; }
		public T Value => Succeeded ? value : throw new InvalidOperationException("A failed operation has no value.");
		public RetailImportFailure Failure => !Succeeded ? failure : throw new InvalidOperationException("A successful operation has no failure.");

		RetailOperationResult(bool succeeded, T value, RetailImportFailure failure)
		{
			Succeeded = succeeded;
			this.value = value;
			this.failure = failure;
		}

		public static RetailOperationResult<T> Success(T value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			return new RetailOperationResult<T>(true, value, null);
		}

		public static RetailOperationResult<T> Failed(RetailImportFailure failure)
		{
			return new RetailOperationResult<T>(false, default, failure ?? throw new ArgumentNullException(nameof(failure)));
		}
	}

	public sealed class RetailOperationResult
	{
		readonly RetailImportFailure failure;
		public bool Succeeded { get; }
		public RetailImportFailure Failure => !Succeeded ? failure : throw new InvalidOperationException("A successful operation has no failure.");

		RetailOperationResult(bool succeeded, RetailImportFailure failure)
		{
			Succeeded = succeeded;
			this.failure = failure;
		}

		public static RetailOperationResult Success() => new(true, null);

		public static RetailOperationResult Failed(RetailImportFailure failure)
		{
			return new RetailOperationResult(false, failure ?? throw new ArgumentNullException(nameof(failure)));
		}
	}

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
		public string SourceRoot { get; }

		public RetailImportRequest(
			IEnumerable<string> sourceFiles,
			string contentRoot,
			long? availableBytes = null,
			string sourceRoot = null)
		{
			if (sourceFiles == null)
				throw new ArgumentNullException(nameof(sourceFiles));

			SourceFiles = Array.AsReadOnly(sourceFiles.ToArray());
			ContentRoot = contentRoot ?? throw new ArgumentNullException(nameof(contentRoot));
			AvailableBytes = availableBytes;
			SourceRoot = sourceRoot;
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
