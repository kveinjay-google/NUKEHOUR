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

using System.Threading.Tasks;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class ServerListDirectoryTest
	{
		[Test]
		public void NearbySnapshotIsPublishedWhileInternetQueryIsPending()
		{
			var directory = new ServerListDirectory<string>(room => room);
			var internetResponse = new TaskCompletionSource<string[]>();
			var internetUpdate = UpdateInternetAfterResponse(directory, internetResponse.Task);

			var visibleRooms = directory.UpdateNearby(new[] { "nearby-room" });

			Assert.That(internetUpdate.IsCompleted, Is.False);
			Assert.That(visibleRooms, Is.EqualTo(new[] { "nearby-room" }));
		}

		[Test]
		public async Task InternetResponseMergesWithAlreadyPublishedNearbySnapshot()
		{
			var directory = new ServerListDirectory<string>(room => room);
			var internetResponse = new TaskCompletionSource<string[]>();
			var internetUpdate = UpdateInternetAfterResponse(directory, internetResponse.Task);
			directory.UpdateNearby(new[] { "nearby-room" });

			internetResponse.SetResult(new[] { "internet-room" });
			var visibleRooms = await internetUpdate;

			Assert.That(visibleRooms, Is.EqualTo(new[] { "internet-room", "nearby-room" }));
		}

		[Test]
		public void NearbyRoomReplacesDuplicateInternetEntry()
		{
			var directory = new ServerListDirectory<Room>(room => room.Address);
			var internetRoom = new Room("192.168.1.10:1234", "internet");
			var nearbyRoom = new Room("192.168.1.10:1234", "nearby");
			directory.UpdateInternet(new[] { internetRoom });

			var visibleRooms = directory.UpdateNearby(new[] { nearbyRoom });

			Assert.That(visibleRooms, Is.EqualTo(new[] { nearbyRoom }));
		}

		[Test]
		public void DisposedCallbackGenerationRejectsQueuedCallbacks()
		{
			var generation = ServerListCallbackGeneration.Create();
			var capturedGeneration = generation.Capture();
			var callbacks = 0;

			generation.Dispose();
			if (generation.IsCurrent(capturedGeneration))
				callbacks++;

			Assert.That(callbacks, Is.Zero);
			Assert.That(generation.IsCurrent(generation.Capture()), Is.False);
		}

		[Test]
		public void NearbyMergeDeduplicatesEachProviderAndPrefersLan()
		{
			var lan = new[]
			{
				new NearbyRoom("session-a", "192.168.1.10:1234", "lan-first"),
				new NearbyRoom("session-a", "192.168.1.11:1234", "lan-duplicate"),
				new NearbyRoom(null, "192.168.1.20:1234", "lan-address")
			};
			var peer = new[]
			{
				new NearbyRoom("session-a", "nearby://session-a", "peer-shadowed"),
				new NearbyRoom("session-b", "nearby://session-b", "peer-first"),
				new NearbyRoom("session-b", "nearby://session-b-copy", "peer-duplicate"),
				new NearbyRoom(null, "192.168.1.20:1234", "peer-address-duplicate")
			};

			var merged = ServerListNearbyMerger.Merge(
				lan, peer, room => room.SessionId ?? room.Address);

			Assert.That(merged, Is.EqualTo(new[] { lan[0], lan[2], peer[1] }));
		}

		static async Task<System.Collections.Generic.IReadOnlyList<T>> UpdateInternetAfterResponse<T>(
			ServerListDirectory<T> directory, Task<T[]> response)
		{
			return directory.UpdateInternet(await response);
		}

		sealed record Room(string Address, string Source);
		sealed record NearbyRoom(string SessionId, string Address, string Source);
	}
}
