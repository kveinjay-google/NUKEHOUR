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
using System.Linq;
using System.Threading;

namespace OpenRA.Mods.RA2.Content
{
	public sealed class RetailMapProfileEvidence
	{
		public string CanonicalMapUid { get; }
		public Ra2MapCompatibleProfiles CompatibleProfiles { get; }

		RetailMapProfileEvidence(
			string canonicalMapUid,
			Ra2MapCompatibleProfiles compatibleProfiles)
		{
			CanonicalMapUid = ValidateUid(canonicalMapUid);
			if (compatibleProfiles == Ra2MapCompatibleProfiles.None ||
				(compatibleProfiles & ~(Ra2MapCompatibleProfiles.Base | Ra2MapCompatibleProfiles.Yuri)) != 0)
				throw new ArgumentOutOfRangeException(nameof(compatibleProfiles));

			CompatibleProfiles = compatibleProfiles;
		}

		internal static RetailMapProfileEvidence FromValidatedMap(
			string canonicalMapUid,
			Ra2MapCompatibleProfiles compatibleProfiles) =>
			new(canonicalMapUid, compatibleProfiles);

		static string ValidateUid(string value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));
			if (value.Length != 64 || value.Any(character =>
				!(character is >= '0' and <= '9') && !(character is >= 'a' and <= 'f')))
				throw new ArgumentException("The map UID must be 64 lowercase hexadecimal characters.", nameof(value));

			return value;
		}
	}

	public sealed class RetailProfileValidation
	{
		public Ra2RuntimeProfileId SelectedProfile { get; }
		public Ra2RetailMountContract MountContract { get; }
		public ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> Groups { get; }
		public ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure> GroupFailures { get; }
		public ImmutableArray<RetailMapProfileEvidence> Maps { get; }

		internal RetailProfileValidation(
			Ra2RuntimeProfileId selectedProfile,
			Ra2RetailMountContract mountContract,
			ImmutableSortedDictionary<RetailContentGroup, RetailGroupStatus> groups,
			ImmutableSortedDictionary<RetailContentGroup, RetailImportFailure> groupFailures,
			ImmutableArray<RetailMapProfileEvidence> maps)
		{
			if (!Enum.IsDefined(typeof(Ra2RuntimeProfileId), selectedProfile))
				throw new ArgumentOutOfRangeException(nameof(selectedProfile));
			if (mountContract == null || mountContract.Profile != selectedProfile)
				throw new ArgumentException("The mount contract does not match the selected profile.", nameof(mountContract));
			var expectedGroups = Enum.GetValues<RetailContentGroup>();
			if (groups == null || groups.Count != expectedGroups.Length ||
				!groups.Keys.SequenceEqual(expectedGroups.OrderBy(group => group)) ||
				groups.Any(pair => pair.Value == null ||
					!IsAllowedSuccessfulStatus(pair.Key, pair.Value, selectedProfile)))
				throw new ArgumentException("The profile result must contain exactly five groups.", nameof(groups));
			if (groupFailures == null)
				throw new ArgumentNullException(nameof(groupFailures));
			var invalidOptional = groups.Where(pair => pair.Value.Validation == RetailGroupValidation.Invalid)
				.Select(pair => pair.Key).ToArray();
			if (!groupFailures.Keys.SequenceEqual(invalidOptional))
				throw new ArgumentException("Invalid optional groups and failures must be one-to-one.", nameof(groupFailures));
			if (groupFailures.Any(pair =>
				pair.Value == null || pair.Value.Group != pair.Key ||
				groups[pair.Key].Diagnostic != pair.Value.Diagnostic ||
				!IsAllowedOptionalFailure(pair.Key, pair.Value)))
				throw new ArgumentException("The optional group failure map is inconsistent.", nameof(groupFailures));
			if (maps.IsDefault || maps.Length > IosImportEnvelopePolicyV1.ImportedMaps)
				throw new ArgumentException("The map evidence array is invalid.", nameof(maps));
			var hasBaseMap = false;
			var hasYuriMap = false;
			RetailMapProfileEvidence previousMap = null;
			foreach (var map in maps)
			{
				if (map == null || previousMap != null &&
					StringComparer.Ordinal.Compare(previousMap.CanonicalMapUid, map.CanonicalMapUid) >= 0)
					throw new ArgumentException("Map evidence must be canonical, unique, and ordered.", nameof(maps));

				hasBaseMap |= map.CompatibleProfiles.HasFlag(Ra2MapCompatibleProfiles.Base);
				hasYuriMap |= map.CompatibleProfiles.HasFlag(Ra2MapCompatibleProfiles.Yuri);
				previousMap = map;
			}
			if (!hasBaseMap ||
				(selectedProfile == Ra2RuntimeProfileId.Yuri && !hasYuriMap) ||
				(selectedProfile == Ra2RuntimeProfileId.Base && hasYuriMap &&
					groups[RetailContentGroup.Yuri].Validation == RetailGroupValidation.Unavailable))
				throw new ArgumentException("Map evidence does not match the selected profile.", nameof(maps));

			SelectedProfile = selectedProfile;
			MountContract = mountContract;
			Groups = groups;
			GroupFailures = groupFailures;
			Maps = ImmutableArray.CreateRange(maps);
		}

		static bool IsAllowedSuccessfulStatus(
			RetailContentGroup group,
			RetailGroupStatus status,
			Ra2RuntimeProfileId selectedProfile)
		{
			if (status.Recognition == RetailEditionRecognition.Recognized ||
				(status.Validation != RetailGroupValidation.Invalid &&
					status.Diagnostic != RetailImportDiagnosticCode.None))
				return false;

			if (group is RetailContentGroup.Base or RetailContentGroup.Maps)
				return status.Validation == RetailGroupValidation.Ready &&
					status.Recognition == RetailEditionRecognition.CompatibleUnrecognized &&
					status.Diagnostic == RetailImportDiagnosticCode.None;

			if (group is RetailContentGroup.Ra2Music or RetailContentGroup.YuriMusic)
				return status.Recognition == RetailEditionRecognition.NotApplicable &&
					status.Validation is RetailGroupValidation.Missing or
						RetailGroupValidation.Invalid or RetailGroupValidation.Unavailable;

			if (group != RetailContentGroup.Yuri)
				return false;
			if (selectedProfile == Ra2RuntimeProfileId.Yuri)
				return status.Validation == RetailGroupValidation.Ready &&
					status.Recognition == RetailEditionRecognition.CompatibleUnrecognized &&
					status.Diagnostic == RetailImportDiagnosticCode.None;

			return status.Validation switch
			{
				RetailGroupValidation.Missing =>
					status.Recognition == RetailEditionRecognition.NotApplicable &&
					status.Diagnostic == RetailImportDiagnosticCode.None,
				RetailGroupValidation.Invalid =>
					status.Recognition == RetailEditionRecognition.NotApplicable,
				RetailGroupValidation.Unavailable =>
					status.Recognition == RetailEditionRecognition.CompatibleUnrecognized &&
					status.Diagnostic == RetailImportDiagnosticCode.None,
				_ => false,
			};
		}

		static bool IsAllowedOptionalFailure(
			RetailContentGroup group,
			RetailImportFailure failure)
		{
			if (failure.Code == RetailImportErrorCode.CorruptArchive)
				return (failure.Diagnostic is RetailImportDiagnosticCode.MalformedEnvelope or
					RetailImportDiagnosticCode.ArithmeticOverflow) &&
					(group switch
					{
						RetailContentGroup.Ra2Music => failure.SafeBasename == "theme.mix",
						RetailContentGroup.Yuri => failure.SafeBasename is "ra2md.mix" or "langmd.mix",
						RetailContentGroup.YuriMusic => failure.SafeBasename == "thememd.mix",
						_ => false,
					});
			if (group == RetailContentGroup.Yuri &&
				failure.Code == RetailImportErrorCode.PartialYuri)
				return failure.Diagnostic == RetailImportDiagnosticCode.MissingRequiredArchive &&
					failure.SafeBasename is "ra2md.mix" or "langmd.mix";
			if (group == RetailContentGroup.Yuri &&
				failure.Code == RetailImportErrorCode.InvalidAudio)
				return failure.SafeBasename == "langmd.mix" &&
					failure.Diagnostic is RetailImportDiagnosticCode.MissingRequiredArchive or
						RetailImportDiagnosticCode.MalformedEnvelope or
						RetailImportDiagnosticCode.ArithmeticOverflow;

			return false;
		}
	}

	public sealed class RetailProfileValidator
	{
		readonly IRetailMonotonicClock clock;

		public RetailProfileValidator(IRetailMonotonicClock clock)
		{
			this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
		}

		public RetailOperationResult<RetailProfileValidation> Validate(
			RetailArchiveGraphResult archives,
			ImmutableArray<RetailMapProfileEvidence> maps,
			RetailValidationDeadline alreadyStartedWholeValidationDeadline,
			CancellationToken cancellationToken)
		{
			if (archives == null)
				throw new ArgumentNullException(nameof(archives));
			if (maps.IsDefault || maps.Length > IosImportEnvelopePolicyV1.ImportedMaps)
				return Failed(
					RetailImportErrorCode.ProfileContextFailure,
					null,
					null,
					RetailImportDiagnosticCode.InvalidMapPackage);
			if (!alreadyStartedWholeValidationDeadline.MatchesClock(clock))
				return Failed(
					RetailImportErrorCode.ProfileContextFailure,
					null,
					null,
					RetailImportDiagnosticCode.ProfileMetadataMismatch);

			var checkpoint = Checkpoint(alreadyStartedWholeValidationDeadline, cancellationToken);
			if (checkpoint != null)
				return RetailOperationResult<RetailProfileValidation>.Failed(checkpoint);

			foreach (var group in Enum.GetValues<RetailContentGroup>())
			{
				checkpoint = Checkpoint(alreadyStartedWholeValidationDeadline, cancellationToken);
				if (checkpoint != null)
					return RetailOperationResult<RetailProfileValidation>.Failed(checkpoint);
				if (!archives.Groups.ContainsKey(group))
					return Failed(
						RetailImportErrorCode.ProfileContextFailure,
						null,
						null,
						RetailImportDiagnosticCode.ProfileMetadataMismatch);
			}

			var baseContract = Ra2RetailMountContract.CreateCheckedBase(
				archives,
				() => Checkpoint(alreadyStartedWholeValidationDeadline, cancellationToken));
			if (!baseContract.Succeeded)
				return RetailOperationResult<RetailProfileValidation>.Failed(baseContract.Failure);

			var canonicalMaps = ImmutableArray.CreateBuilder<RetailMapProfileEvidence>(maps.Length);
			var canonicalMapUids = new HashSet<string>(StringComparer.Ordinal);
			var hasBaseMap = false;
			var hasYuriMap = false;
			foreach (var map in maps)
			{
				checkpoint = Checkpoint(alreadyStartedWholeValidationDeadline, cancellationToken);
				if (checkpoint != null)
					return RetailOperationResult<RetailProfileValidation>.Failed(checkpoint);
				if (map == null || !canonicalMapUids.Add(map.CanonicalMapUid))
					return Failed(
						RetailImportErrorCode.ProfileContextFailure,
						null,
						null,
						RetailImportDiagnosticCode.InvalidMapPackage);

				canonicalMaps.Add(map);
				hasBaseMap |= map.CompatibleProfiles.HasFlag(Ra2MapCompatibleProfiles.Base);
				hasYuriMap |= map.CompatibleProfiles.HasFlag(Ra2MapCompatibleProfiles.Yuri);
			}

			var orderedMaps = canonicalMaps.ToImmutable()
				.OrderBy(map => map.CanonicalMapUid, StringComparer.Ordinal).ToImmutableArray();
			checkpoint = Checkpoint(alreadyStartedWholeValidationDeadline, cancellationToken);
			if (checkpoint != null)
				return RetailOperationResult<RetailProfileValidation>.Failed(checkpoint);

			if (!hasBaseMap)
				return Failed(
					RetailImportErrorCode.ProfileUnavailable,
					RetailContentGroup.Maps,
					null,
					RetailImportDiagnosticCode.InvalidSpawnLayout);

			var selectedProfile = archives.Groups[RetailContentGroup.Yuri].Validation == RetailGroupValidation.Ready &&
				hasYuriMap ? Ra2RuntimeProfileId.Yuri : Ra2RuntimeProfileId.Base;
			var mountContract = selectedProfile == Ra2RuntimeProfileId.Base
				? baseContract : Ra2RetailMountContract.CreateCheckedYuriAfterBase(
					archives,
					() => Checkpoint(alreadyStartedWholeValidationDeadline, cancellationToken));
			if (!mountContract.Succeeded)
				return RetailOperationResult<RetailProfileValidation>.Failed(mountContract.Failure);

			var groups = archives.Groups.ToBuilder();
			groups[RetailContentGroup.Base] = ReadyStatus();
			groups[RetailContentGroup.Maps] = ReadyStatus();
			if (archives.Groups[RetailContentGroup.Yuri].Validation == RetailGroupValidation.Ready)
				groups[RetailContentGroup.Yuri] = selectedProfile == Ra2RuntimeProfileId.Yuri
					? ReadyStatus()
					: new RetailGroupStatus(
						RetailGroupValidation.Unavailable,
						RetailEditionRecognition.CompatibleUnrecognized,
						RetailImportDiagnosticCode.None);

			var failures = ImmutableSortedDictionary.CreateBuilder<RetailContentGroup, RetailImportFailure>();
			foreach (var pair in archives.GroupFailures)
			{
				if (pair.Key == RetailContentGroup.Base)
					continue;
				failures.Add(pair.Key, pair.Value);
			}

			return RetailOperationResult<RetailProfileValidation>.Success(new RetailProfileValidation(
				selectedProfile,
				mountContract.Value,
				groups.ToImmutable(),
				failures.ToImmutable(),
				orderedMaps));
		}

		RetailImportFailure Checkpoint(
			RetailValidationDeadline deadline,
			CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
				return new RetailImportFailure(
					RetailImportErrorCode.Cancelled, null, null, RetailImportDiagnosticCode.None);
			try
			{
				deadline.ThrowIfExpired(clock);
				return null;
			}
			catch (TimeoutException)
			{
				return new RetailImportFailure(
					RetailImportErrorCode.ValidationTimedOut,
					null,
					null,
					RetailImportDiagnosticCode.DeadlineExceeded);
			}
		}

		static RetailGroupStatus ReadyStatus() => new(
			RetailGroupValidation.Ready,
			RetailEditionRecognition.CompatibleUnrecognized,
			RetailImportDiagnosticCode.None);

		static RetailOperationResult<RetailProfileValidation> Failed(
			RetailImportErrorCode code,
			RetailContentGroup? group,
			string safeBasename,
			RetailImportDiagnosticCode diagnostic) =>
			RetailOperationResult<RetailProfileValidation>.Failed(
				new RetailImportFailure(code, group, safeBasename, diagnostic));
	}
}
