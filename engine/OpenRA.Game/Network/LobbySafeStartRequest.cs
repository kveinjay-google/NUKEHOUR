#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Globalization;
using System.Linq;

namespace OpenRA.Network
{
	public readonly struct LobbySafeStartRequest : IEquatable<LobbySafeStartRequest>
	{
		public string ExpectedMapUid { get; }
		public long RequestId { get; }

		public LobbySafeStartRequest(string expectedMapUid, long requestId)
		{
			if (string.IsNullOrWhiteSpace(expectedMapUid) || expectedMapUid.Any(char.IsWhiteSpace))
				throw new ArgumentException("Map UID must be a nonempty single token.", nameof(expectedMapUid));
			if (requestId <= 0)
				throw new ArgumentOutOfRangeException(nameof(requestId), "Request ID must be positive.");

			ExpectedMapUid = expectedMapUid;
			RequestId = requestId;
		}

		public string ToCommand()
		{
			return $"startgame_safe {ExpectedMapUid} {RequestId.ToString(CultureInfo.InvariantCulture)}";
		}

		public static bool TryParse(string value, out LobbySafeStartRequest request)
		{
			request = default;
			if (value == null)
				return false;

			var fields = value.Split(' ');
			if (fields.Length != 2 || fields.Any(string.IsNullOrEmpty) || fields[0].Any(char.IsWhiteSpace) ||
				!long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var requestId) ||
				requestId <= 0 || requestId.ToString(CultureInfo.InvariantCulture) != fields[1])
				return false;

			try
			{
				request = new LobbySafeStartRequest(fields[0], requestId);
				return true;
			}
			catch (ArgumentException)
			{
				return false;
			}
		}

		public bool Equals(LobbySafeStartRequest other)
		{
			return ExpectedMapUid == other.ExpectedMapUid && RequestId == other.RequestId;
		}

		public override bool Equals(object obj)
		{
			return obj is LobbySafeStartRequest other && Equals(other);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(ExpectedMapUid, RequestId);
		}

		public static bool operator ==(LobbySafeStartRequest left, LobbySafeStartRequest right) => left.Equals(right);
		public static bool operator !=(LobbySafeStartRequest left, LobbySafeStartRequest right) => !left.Equals(right);
	}
}
