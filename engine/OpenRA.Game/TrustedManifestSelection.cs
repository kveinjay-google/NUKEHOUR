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
	public readonly struct TrustedManifestSelection : IEquatable<TrustedManifestSelection>
	{
		public string ModId { get; }
		public string EntryName { get; }

		public TrustedManifestSelection(string modId, string entryName)
		{
			ModId = ValidateModId(modId);
			EntryName = ValidateEntryName(entryName);
		}

		public static TrustedManifestSelection Default(string modId) => new(modId, "mod.yaml");

		internal bool IsValid => ModId != null && EntryName != null;

		static string ValidateModId(string value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));
			if (value.Length is < 1 or > 32)
				throw new ArgumentException("The mod id length is invalid.", nameof(value));
			foreach (var character in value)
				if (!(character is >= 'a' and <= 'z') && !(character is >= '0' and <= '9') && character != '-')
					throw new ArgumentException("The mod id is not canonical.", nameof(value));

			return value;
		}

		static string ValidateEntryName(string value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));
			if (value.Length == 0 || value is "." or ".." ||
				value.IndexOfAny(new[] { '/', '\\', '\0', '|', ':' }) >= 0 ||
				value[0] == '~')
				throw new ArgumentException("The manifest entry must be one canonical root filename.", nameof(value));
			foreach (var character in value)
				if (char.IsControl(character))
					throw new ArgumentException("The manifest entry contains a control character.", nameof(value));

			return value;
		}

		public bool Equals(TrustedManifestSelection other) =>
			StringComparer.Ordinal.Equals(ModId, other.ModId) &&
			StringComparer.Ordinal.Equals(EntryName, other.EntryName);

		public override bool Equals(object obj) => obj is TrustedManifestSelection other && Equals(other);

		public override int GetHashCode()
		{
			unchecked
			{
				return (ModId == null ? 0 : StringComparer.Ordinal.GetHashCode(ModId)) * 397 ^
					(EntryName == null ? 0 : StringComparer.Ordinal.GetHashCode(EntryName));
			}
		}

		public static bool operator ==(TrustedManifestSelection left, TrustedManifestSelection right) =>
			left.Equals(right);
		public static bool operator !=(TrustedManifestSelection left, TrustedManifestSelection right) =>
			!left.Equals(right);
	}
}
