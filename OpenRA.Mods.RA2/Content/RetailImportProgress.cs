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

namespace OpenRA.Mods.RA2.Content
{
	public readonly struct RetailImportProgress : IEquatable<RetailImportProgress>
	{
		public RetailImportPhase Phase { get; }
		public RetailContentGroup? Group { get; }
		public int CompletedItems { get; }
		public int? TotalItems { get; }
		public long CompletedBytes { get; }
		public long? TotalBytes { get; }
		public string SafeBasename { get; }
		public int? Percentage
		{
			get
			{
				if (TotalBytes > 0)
					return (int)Math.Min(100m, 100m * CompletedBytes / TotalBytes.Value);

				if (TotalItems > 0)
					return (int)Math.Min(100m, 100m * CompletedItems / TotalItems.Value);

				return null;
			}
		}

		public RetailImportProgress(RetailImportPhase phase, RetailContentGroup? group, int completedItems,
			int? totalItems, long completedBytes, long? totalBytes, string safeBasename)
		{
			if (!Enum.IsDefined(typeof(RetailImportPhase), phase))
				throw new ArgumentOutOfRangeException(nameof(phase));
			if (group.HasValue && !Enum.IsDefined(typeof(RetailContentGroup), group.Value))
				throw new ArgumentOutOfRangeException(nameof(group));
			if (completedItems < 0)
				throw new ArgumentOutOfRangeException(nameof(completedItems));
			if (totalItems < 0)
				throw new ArgumentOutOfRangeException(nameof(totalItems));
			if (completedBytes < 0)
				throw new ArgumentOutOfRangeException(nameof(completedBytes));
			if (totalBytes < 0)
				throw new ArgumentOutOfRangeException(nameof(totalBytes));
			RetailSafeBasename.Validate(safeBasename, nameof(safeBasename), optional: true);

			Phase = phase;
			Group = group;
			CompletedItems = completedItems;
			TotalItems = totalItems;
			CompletedBytes = completedBytes;
			TotalBytes = totalBytes;
			SafeBasename = safeBasename;
		}

		public bool Equals(RetailImportProgress other) => Phase == other.Phase && Group == other.Group &&
			CompletedItems == other.CompletedItems && TotalItems == other.TotalItems &&
			CompletedBytes == other.CompletedBytes && TotalBytes == other.TotalBytes &&
			StringComparer.Ordinal.Equals(SafeBasename, other.SafeBasename);

		public override bool Equals(object obj) => obj is RetailImportProgress other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(Phase, Group, CompletedItems, TotalItems,
			CompletedBytes, TotalBytes, SafeBasename);

		public static bool operator ==(RetailImportProgress left, RetailImportProgress right) => left.Equals(right);
		public static bool operator !=(RetailImportProgress left, RetailImportProgress right) => !left.Equals(right);
	}
}
