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
using System.Collections.Immutable;
using System.Linq;

namespace OpenRA.Mods.RA2.Content
{
	public sealed class Ra2RetailMountRequirement
	{
		public RetailContentGroup Group { get; }
		public string SafeBasename { get; }
		public string MountName { get; }
		public bool Required { get; }

		internal Ra2RetailMountRequirement(
			RetailContentGroup group,
			string safeBasename,
			string mountName,
			bool required)
		{
			if (group is not (RetailContentGroup.Base or RetailContentGroup.Yuri))
				throw new ArgumentOutOfRangeException(nameof(group));
			SafeBasename = RetailSafeBasename.Validate(safeBasename, nameof(safeBasename));
			MountName = RetailSafeBasename.Validate(mountName, nameof(mountName));
			if (!StringComparer.Ordinal.Equals(SafeBasename, MountName))
				throw new ArgumentException("The V1 mount name must equal its authenticated basename.", nameof(mountName));
			if (!required)
				throw new ArgumentException("Task 3 exposes required retail mounts only.", nameof(required));

			Group = group;
			Required = required;
		}
	}

	public sealed class Ra2RetailMountContract
	{
		public Ra2RuntimeProfileId Profile { get; }
		public ImmutableArray<Ra2RetailMountRequirement> Mounts { get; }

		Ra2RetailMountContract(
			Ra2RuntimeProfileId profile,
			ImmutableArray<Ra2RetailMountRequirement> mounts)
		{
			if (!Enum.IsDefined(typeof(Ra2RuntimeProfileId), profile))
				throw new ArgumentOutOfRangeException(nameof(profile));
			if (mounts.IsDefault || mounts.Any(mount => mount == null))
				throw new ArgumentException("The mount requirement array is invalid.", nameof(mounts));

			var expected = Requirements(profile);
			if (mounts.Length != expected.Length)
				throw new ArgumentException("The mount requirement count is invalid.", nameof(mounts));
			for (var i = 0; i < expected.Length; i++)
				if (mounts[i].Group != expected[i].Group ||
					!StringComparer.Ordinal.Equals(mounts[i].SafeBasename, expected[i].Name) ||
					!StringComparer.Ordinal.Equals(mounts[i].MountName, expected[i].Name) ||
					!mounts[i].Required)
					throw new ArgumentException("The mount requirements are not in the fixed V1 order.", nameof(mounts));

			Profile = profile;
			Mounts = ImmutableArray.CreateRange(mounts);
		}

		public static RetailOperationResult<Ra2RetailMountContract> Create(
			Ra2RuntimeProfileId profile,
			RetailArchiveGraphResult archives) =>
			CreateCore(profile, archives, null, 0);

		internal static RetailOperationResult<Ra2RetailMountContract> CreateCheckedBase(
			RetailArchiveGraphResult archives,
			Func<RetailImportFailure> checkpoint) =>
			CreateCore(Ra2RuntimeProfileId.Base, archives, checkpoint, 0);

		internal static RetailOperationResult<Ra2RetailMountContract> CreateCheckedYuriAfterBase(
			RetailArchiveGraphResult archives,
			Func<RetailImportFailure> checkpoint) =>
			CreateCore(Ra2RuntimeProfileId.Yuri, archives, checkpoint, 2);

		static RetailOperationResult<Ra2RetailMountContract> CreateCore(
			Ra2RuntimeProfileId profile,
			RetailArchiveGraphResult archives,
			Func<RetailImportFailure> checkpoint,
			int firstRequirement)
		{
			if (!Enum.IsDefined(typeof(Ra2RuntimeProfileId), profile))
				throw new ArgumentOutOfRangeException(nameof(profile));
			if (archives == null)
				throw new ArgumentNullException(nameof(archives));
			if (firstRequirement < 0)
				throw new ArgumentOutOfRangeException(nameof(firstRequirement));

			var requirements = Requirements(profile);
			for (var index = firstRequirement; index < requirements.Length; index++)
			{
				var checkpointFailure = checkpoint?.Invoke();
				if (checkpointFailure != null)
					return RetailOperationResult<Ra2RetailMountContract>.Failed(checkpointFailure);

				var requirement = requirements[index];
				var status = archives.Groups[requirement.Group];
				if (status.Recognition != RetailEditionRecognition.NotApplicable)
					return Failure(
						requirement.Group, requirement.Name,
						RetailImportDiagnosticCode.ProfileMetadataMismatch);
				if (status.Validation == RetailGroupValidation.Ready)
					continue;

				if (status.Validation == RetailGroupValidation.Invalid &&
					archives.GroupFailures.TryGetValue(requirement.Group, out var groupFailure))
				{
					if (!StringComparer.Ordinal.Equals(groupFailure.SafeBasename, requirement.Name))
						continue;

					return Failure(
						requirement.Group, groupFailure.SafeBasename, groupFailure.Diagnostic);
				}

				return Failure(
					requirement.Group,
					requirement.Name,
					RetailImportDiagnosticCode.MissingRequiredArchive);
			}

			var mounts = requirements.Select(requirement =>
				new Ra2RetailMountRequirement(
					requirement.Group, requirement.Name, requirement.Name, true)).ToImmutableArray();
			return RetailOperationResult<Ra2RetailMountContract>.Success(
				new Ra2RetailMountContract(profile, mounts));
		}

		static RetailOperationResult<Ra2RetailMountContract> Failure(
			RetailContentGroup group,
			string safeBasename,
			RetailImportDiagnosticCode diagnostic) =>
			RetailOperationResult<Ra2RetailMountContract>.Failed(new RetailImportFailure(
				RetailImportErrorCode.RequiredMountFailure,
				group,
				safeBasename,
				diagnostic));

		static ImmutableArray<(RetailContentGroup Group, string Name)> Requirements(
			Ra2RuntimeProfileId profile)
		{
			var builder = ImmutableArray.CreateBuilder<(RetailContentGroup, string)>();
			builder.Add((RetailContentGroup.Base, "ra2.mix"));
			builder.Add((RetailContentGroup.Base, "language.mix"));
			if (profile == Ra2RuntimeProfileId.Yuri)
			{
				builder.Add((RetailContentGroup.Yuri, "ra2md.mix"));
				builder.Add((RetailContentGroup.Yuri, "langmd.mix"));
			}

			return builder.ToImmutable();
		}
	}
}
