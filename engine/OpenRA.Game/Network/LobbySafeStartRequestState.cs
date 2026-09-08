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

namespace OpenRA.Network
{
	public sealed class LobbySafeStartRequestState
	{
		long lastIssuedRequestId;
		string pendingMapUid;

		public bool IsPending { get; private set; }
		public long PendingRequestId { get; private set; }
		public LobbyStartRejection Rejection { get; private set; }

		public LobbySafeStartRequestState(long lastIssuedRequestId)
		{
			if (lastIssuedRequestId < 0)
				throw new ArgumentOutOfRangeException(nameof(lastIssuedRequestId));

			this.lastIssuedRequestId = lastIssuedRequestId;
		}

		public LobbySafeStartRequest Begin(string mapUid)
		{
			if (lastIssuedRequestId == long.MaxValue)
				throw new InvalidOperationException("Safe-start request IDs are exhausted.");

			var request = new LobbySafeStartRequest(mapUid, lastIssuedRequestId + 1);
			lastIssuedRequestId = request.RequestId;
			pendingMapUid = request.ExpectedMapUid;
			PendingRequestId = request.RequestId;
			IsPending = true;
			Rejection = null;
			return request;
		}

		public bool TryAcceptRejection(LobbyStartRejection rejection, string currentMapUid)
		{
			if (!IsPending || rejection == null || rejection.RequestId != PendingRequestId ||
				!string.Equals(rejection.ExpectedMapUid, pendingMapUid, StringComparison.Ordinal) ||
				!string.Equals(rejection.ExpectedMapUid, currentMapUid, StringComparison.Ordinal))
				return false;

			ClearPending();
			Rejection = rejection;
			return true;
		}

		public bool TryTimeout(long requestId)
		{
			if (!IsPending || PendingRequestId != requestId)
				return false;

			ClearPending();
			return true;
		}

		public bool ObserveReadinessChange(int clientIndex)
		{
			if (Rejection == null || Rejection.ClientIndex != clientIndex)
				return false;

			Rejection = null;
			return true;
		}

		public bool ObserveLobbySync(string currentMapUid)
		{
			var changed = Rejection != null;
			Rejection = null;

			if (IsPending && !string.Equals(pendingMapUid, currentMapUid, StringComparison.Ordinal))
			{
				ClearPending();
				changed = true;
			}

			return changed;
		}

		public bool Reset()
		{
			if (!IsPending && Rejection == null)
				return false;

			ClearPending();
			Rejection = null;
			return true;
		}

		void ClearPending()
		{
			IsPending = false;
			PendingRequestId = 0;
			pendingMapUid = null;
		}
	}
}
