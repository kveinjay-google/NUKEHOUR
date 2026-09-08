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
using System.Diagnostics;
using System.Linq;

namespace OpenRA.Support
{
	public sealed class PerformanceCategoryProfiler
	{
		readonly Func<long> clock;
		readonly double ticksPerMillisecond;
		readonly Dictionary<ProfileKey, ProfileAccumulator> entries = new();

		public static PerformanceCategoryProfiler Global { get; } =
			new(() => Stopwatch.GetTimestamp(), Stopwatch.Frequency);

		public bool Enabled { get; set; }

		public PerformanceCategoryProfiler(Func<long> clock, long ticksPerSecond)
		{
			this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
			ticksPerMillisecond = Math.Max(1, ticksPerSecond) / 1000d;
		}

		public PerformanceCategoryScope Measure(string category, Type detailType = null)
		{
			if (!Enabled)
				return default;

			return new PerformanceCategoryScope(this, category, detailType, clock());
		}

		internal void Complete(string category, Type detailType, long started)
		{
			var elapsed = Math.Max(0, clock() - started);
			var key = new ProfileKey(category, detailType);
			if (!entries.TryGetValue(key, out var value))
				value = default;

			value.CallCount++;
			value.TotalTicks += elapsed;
			value.MaxTicks = Math.Max(value.MaxTicks, elapsed);
			entries[key] = value;
		}

		public IReadOnlyList<PerformanceCategoryEntry> SnapshotTop(int limit)
		{
			return entries
				.Select(e => new PerformanceCategoryEntry(
					e.Key.Category,
					e.Key.DetailType?.Name ?? "",
					e.Value.CallCount,
					e.Value.TotalTicks / ticksPerMillisecond,
					e.Value.MaxTicks / ticksPerMillisecond))
				.OrderByDescending(e => e.TotalMilliseconds)
				.ThenBy(e => e.Category, StringComparer.Ordinal)
				.ThenBy(e => e.Detail, StringComparer.Ordinal)
				.Take(Math.Max(0, limit))
				.ToArray();
		}

		public void Reset() => entries.Clear();

		readonly struct ProfileKey : IEquatable<ProfileKey>
		{
			public readonly string Category;
			public readonly Type DetailType;

			public ProfileKey(string category, Type detailType)
			{
				Category = category ?? "";
				DetailType = detailType;
			}

			public bool Equals(ProfileKey other) =>
				Category == other.Category && DetailType == other.DetailType;

			public static bool operator ==(ProfileKey left, ProfileKey right) => left.Equals(right);
			public static bool operator !=(ProfileKey left, ProfileKey right) => !left.Equals(right);
			public override bool Equals(object obj) => obj is ProfileKey other && Equals(other);
			public override int GetHashCode() => HashCode.Combine(Category, DetailType);
		}

		struct ProfileAccumulator
		{
			public long CallCount;
			public long TotalTicks;
			public long MaxTicks;
		}
	}

	public readonly struct PerformanceCategoryScope : IDisposable
	{
		readonly PerformanceCategoryProfiler owner;
		readonly string category;
		readonly Type detailType;
		readonly long started;

		internal PerformanceCategoryScope(PerformanceCategoryProfiler owner, string category, Type detailType, long started)
		{
			this.owner = owner;
			this.category = category;
			this.detailType = detailType;
			this.started = started;
		}

		public void Dispose() => owner?.Complete(category, detailType, started);
	}

	public readonly struct PerformanceCategoryEntry
	{
		public string Category { get; }
		public string Detail { get; }
		public long CallCount { get; }
		public double TotalMilliseconds { get; }
		public double MaxMilliseconds { get; }

		public PerformanceCategoryEntry(string category, string detail, long callCount,
			double totalMilliseconds, double maxMilliseconds)
		{
			Category = category;
			Detail = detail;
			CallCount = callCount;
			TotalMilliseconds = totalMilliseconds;
			MaxMilliseconds = maxMilliseconds;
		}
	}
}
