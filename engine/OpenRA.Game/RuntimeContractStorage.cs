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
using System.Text;
using OpenRA.Network;

namespace OpenRA
{
	public sealed class RuntimeContractCompatibilityException : IOException
	{
		public RuntimeContractCompatibilityReason Reason { get; }

		internal RuntimeContractCompatibilityException(
			RuntimeContractCompatibilityReason reason, string message)
			: base(message)
		{
			if (!Enum.IsDefined(typeof(RuntimeContractCompatibilityReason), reason) ||
				reason == RuntimeContractCompatibilityReason.Compatible)
				throw new ArgumentOutOfRangeException(nameof(reason));

			Reason = reason;
		}
	}

	public static class RuntimeContractStorage
	{
		static readonly UTF8Encoding StrictUtf8 = new(false, true);

		public static string SavesPath(
			string supportDir, string modId, string modVersion,
			RuntimeContractIdentity identity)
		{
			var version = ValidateVersion(modVersion);
			var root = ProfileRoot(supportDir, modId, identity);
			return identity.IsLegacy
				? Path.Combine(root, "Saves", modId, version)
				: Path.Combine(root, "Saves", version);
		}

		public static string ReplaysPath(
			string supportDir, string modId, string modVersion,
			RuntimeContractIdentity identity)
		{
			var version = ValidateVersion(modVersion);
			var root = ProfileRoot(supportDir, modId, identity);
			return identity.IsLegacy
				? Path.Combine(root, "Replays", modId, version)
				: Path.Combine(root, "Replays", version);
		}

		public static string ServerSettingsPath(
			string supportDir, string modId,
			RuntimeContractIdentity identity)
		{
			var root = ProfileRoot(supportDir, modId, identity);
			return Path.Combine(root, identity.IsLegacy ? "settings.yaml" : "settings.server.yaml");
		}

		public static string SkirmishPath(
			string supportDir, string modId,
			RuntimeContractIdentity identity)
		{
			var root = ProfileRoot(supportDir, modId, identity);
			return Path.Combine(root, identity.IsLegacy ? $"skirmish.{modId}.yaml" : "skirmish.yaml");
		}

		public static RuntimeContractCompatibilityReason Compatibility(
			RuntimeContractIdentity local,
			string storedProfile,
			string storedContract) =>
			RuntimeContractHandshake.Compare(local, storedProfile, storedContract);

		static string ProfileRoot(
			string supportDir, string modId,
			RuntimeContractIdentity identity)
		{
			var root = ValidateSupportDirectory(supportDir);
			var canonicalModId = ValidateModId(modId);
			if (!identity.IsValid)
				throw new ArgumentException("The runtime contract identity is not initialized.", nameof(identity));

			return identity.IsLegacy
				? root
				: Path.Combine(root, "Profiles", canonicalModId, identity.ProfileId, identity.ContractDigest);
		}

		static string ValidateSupportDirectory(string value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));
			if (string.IsNullOrWhiteSpace(value) || value.Contains('\0'))
				throw new ArgumentException("The support directory is not valid.", nameof(value));

			return value;
		}

		static string ValidateModId(string value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));
			if (value.Length is < 1 or > 32)
				throw new ArgumentException("The mod id length is invalid.", nameof(value));
			foreach (var character in value)
				if (!(character is >= 'a' and <= 'z') &&
					!(character is >= '0' and <= '9') && character != '-')
					throw new ArgumentException("The mod id is not canonical.", nameof(value));

			return value;
		}

		static string ValidateVersion(string value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			var canonical = value.Length > 0 && value is not "." and not ".." &&
				value.IndexOfAny(new[] { '/', '\\', '\0', ':' }) < 0 &&
				!Path.IsPathFullyQualified(value);
			if (canonical)
			{
				try
				{
					canonical = value.IsNormalized(NormalizationForm.FormC) &&
						StrictUtf8.GetByteCount(value) <= 255;
				}
				catch (ArgumentException)
				{
					canonical = false;
				}
			}

			if (canonical)
				foreach (var character in value)
					if (char.IsControl(character) || character is '\u2028' or '\u2029')
					{
						canonical = false;
						break;
					}

			if (!canonical)
				throw new ArgumentException("The mod version must be one canonical path segment.", nameof(value));

			return value;
		}
	}
}
