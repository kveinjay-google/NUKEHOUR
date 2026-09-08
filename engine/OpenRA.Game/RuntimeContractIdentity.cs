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

namespace OpenRA
{
	public readonly struct RuntimeContractIdentity : IEquatable<RuntimeContractIdentity>
	{
		public static RuntimeContractIdentity Legacy { get; } = new(true);

		public string ProfileId { get; }
		public string ContractDigest { get; }
		public bool IsLegacy { get; }

		RuntimeContractIdentity(bool legacy)
		{
			ProfileId = string.Empty;
			ContractDigest = string.Empty;
			IsLegacy = legacy;
		}

		public RuntimeContractIdentity(string profileId, string contractDigest)
		{
			ProfileId = ValidateProfile(profileId);
			ContractDigest = ValidateDigest(contractDigest);
			IsLegacy = false;
		}

		internal bool IsValid => IsLegacy
			? ProfileId == string.Empty && ContractDigest == string.Empty
			: ProfileId != null && ContractDigest != null;

		static string ValidateProfile(string value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));
			if (value.Length is < 1 or > 32)
				throw new ArgumentException("The profile id length is invalid.", nameof(value));
			foreach (var character in value)
				if (!(character is >= 'a' and <= 'z') && !(character is >= '0' and <= '9') && character != '-')
					throw new ArgumentException("The profile id is not canonical.", nameof(value));

			return value;
		}

		static string ValidateDigest(string value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));
			if (value.Length != 64)
				throw new ArgumentException("The runtime contract digest must contain 64 lowercase hex characters.",
					nameof(value));
			foreach (var character in value)
				if (!(character is >= '0' and <= '9') && !(character is >= 'a' and <= 'f'))
					throw new ArgumentException("The runtime contract digest is not lowercase hexadecimal.", nameof(value));

			return value;
		}

		public bool Equals(RuntimeContractIdentity other) =>
			IsLegacy == other.IsLegacy &&
			StringComparer.Ordinal.Equals(ProfileId, other.ProfileId) &&
			StringComparer.Ordinal.Equals(ContractDigest, other.ContractDigest);

		public override bool Equals(object obj) => obj is RuntimeContractIdentity other && Equals(other);

		public override int GetHashCode()
		{
			unchecked
			{
				var hash = ProfileId == null ? 0 : StringComparer.Ordinal.GetHashCode(ProfileId);
				hash = hash * 397 ^ (ContractDigest == null ? 0 : StringComparer.Ordinal.GetHashCode(ContractDigest));
				return hash * 397 ^ (IsLegacy ? 1 : 0);
			}
		}

		public static bool operator ==(RuntimeContractIdentity left, RuntimeContractIdentity right) => left.Equals(right);
		public static bool operator !=(RuntimeContractIdentity left, RuntimeContractIdentity right) => !left.Equals(right);
	}
}
