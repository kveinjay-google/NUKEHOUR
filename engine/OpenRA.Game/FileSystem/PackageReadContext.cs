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
using System.Threading;

namespace OpenRA.FileSystem
{
	public interface IPackageReadDeadline
	{
		void ThrowIfExpired();
	}

	public sealed class PackageReadContext
	{
		sealed class SharedLedger
		{
			public readonly object Sync = new();
			public readonly int MaximumDepth;
			public int RemainingEntries;
			public long RemainingAddressedBytes;

			public SharedLedger(int maximumDepth, int maximumEntries, long maximumAddressedBytes)
			{
				MaximumDepth = maximumDepth;
				RemainingEntries = maximumEntries;
				RemainingAddressedBytes = maximumAddressedBytes;
			}
		}

		readonly SharedLedger ledger;
		public CancellationToken CancellationToken { get; }
		public IPackageReadDeadline Deadline { get; }
		public PackageEnvelopePolicy EnvelopePolicy { get; }
		public int Depth { get; }

		public int RemainingEntries
		{
			get
			{
				lock (ledger.Sync)
					return ledger.RemainingEntries;
			}
		}

		public long RemainingAddressedBytes
		{
			get
			{
				lock (ledger.Sync)
					return ledger.RemainingAddressedBytes;
			}
		}

		public PackageReadContext(CancellationToken cancellationToken, IPackageReadDeadline deadline,
			int maximumDepth, int maximumEntries, long maximumAddressedBytes)
			: this(cancellationToken, deadline, maximumDepth, maximumEntries, maximumAddressedBytes, null)
		{
		}

		public PackageReadContext(CancellationToken cancellationToken, IPackageReadDeadline deadline,
			int maximumDepth, int maximumEntries, long maximumAddressedBytes, PackageEnvelopePolicy envelopePolicy)
		{
			if (deadline == null)
				throw new ArgumentNullException(nameof(deadline));
			if (maximumDepth < 0)
				throw new ArgumentOutOfRangeException(nameof(maximumDepth));
			if (maximumEntries < 0)
				throw new ArgumentOutOfRangeException(nameof(maximumEntries));
			if (maximumAddressedBytes < 0)
				throw new ArgumentOutOfRangeException(nameof(maximumAddressedBytes));

			CancellationToken = cancellationToken;
			Deadline = deadline;
			EnvelopePolicy = envelopePolicy;
			Depth = 0;
			ledger = new SharedLedger(maximumDepth, maximumEntries, maximumAddressedBytes);
		}

		PackageReadContext(PackageReadContext parent, int depth)
		{
			CancellationToken = parent.CancellationToken;
			Deadline = parent.Deadline;
			EnvelopePolicy = parent.EnvelopePolicy;
			Depth = depth;
			ledger = parent.ledger;
		}

		public void ThrowIfCancelledOrExpired()
		{
			CancellationToken.ThrowIfCancellationRequested();
			Deadline.ThrowIfExpired();
		}

		public PackageReadContext Descend(int childEntries, long childAddressedBytes)
		{
			if (childEntries < 0)
				throw new ArgumentOutOfRangeException(nameof(childEntries));
			if (childAddressedBytes < 0)
				throw new ArgumentOutOfRangeException(nameof(childAddressedBytes));

			ThrowIfCancelledOrExpired();
			int childDepth;
			try
			{
				childDepth = checked(Depth + 1);
			}
			catch (OverflowException ex)
			{
				throw new PackageEnvelopeException(PackageEnvelopeFailure.ArithmeticOverflow,
					"Package nesting depth overflowed.", ex);
			}

			lock (ledger.Sync)
			{
				if (childDepth > ledger.MaximumDepth)
					throw new PackageEnvelopeException(PackageEnvelopeFailure.LimitExceeded,
						"The package nesting-depth limit was exceeded.");
				if (childEntries > ledger.RemainingEntries ||
					childAddressedBytes > ledger.RemainingAddressedBytes)
					throw new PackageEnvelopeException(PackageEnvelopeFailure.AggregateLimitExceeded,
						"The package aggregate envelope budget was exceeded.");

				ledger.RemainingEntries -= childEntries;
				ledger.RemainingAddressedBytes -= childAddressedBytes;
			}

			return new PackageReadContext(this, childDepth);
		}

		public bool SharesLedger(PackageReadContext other) => other != null && ReferenceEquals(ledger, other.ledger);
	}
}
