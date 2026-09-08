using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenRA.Platforms.Default;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosNearbyReliabilityPolicyTest
	{
		[Test]
		public void NearbyRetriesUseBoundedExponentialBackoff()
		{
			var expected = new[] { 250, 500, 1000, 2000, 4000 };
			for (var attempt = 1; attempt <= expected.Length; attempt++)
				Assert.That(NearbyRetryPolicy.DelayForAttempt(attempt),
					Is.EqualTo(TimeSpan.FromMilliseconds(expected[attempt - 1])));

			Assert.That(NearbyRetryPolicy.DelayForAttempt(expected.Length + 1), Is.Null);
		}

		[Test]
		public void StaleOrInactiveRetryTicketsCannotRun()
		{
			Assert.That(NearbyRetryPolicy.ShouldExecute(7, 7, true, false, false), Is.True);
			Assert.That(NearbyRetryPolicy.ShouldExecute(6, 7, true, false, false), Is.False);
			Assert.That(NearbyRetryPolicy.ShouldExecute(7, 7, false, false, false), Is.False);
			Assert.That(NearbyRetryPolicy.ShouldExecute(7, 7, true, true, false), Is.False);
			Assert.That(NearbyRetryPolicy.ShouldExecute(7, 7, true, false, true), Is.False);
		}

		[Test]
		public void EmptyDiscoveryRefreshUsesAThrottledBoundedSchedule()
		{
			var expected = new[] { 3000, 6000, 12000, 15000, 15000 };
			for (var attempt = 1; attempt <= expected.Length; attempt++)
				Assert.That(NearbyDiscoveryLivenessPolicy.DelayForAttempt(attempt),
					Is.EqualTo(TimeSpan.FromMilliseconds(expected[attempt - 1])));

			Assert.That(NearbyDiscoveryLivenessPolicy.DelayForAttempt(expected.Length + 1), Is.Null);
		}

		[Test]
		public void LanFallbackCadenceKeepsRoomsAliveAcrossOneMissedSweep()
		{
			Assert.That(NearbyLanDiscoveryPolicy.ShouldSweep(0), Is.True);
			Assert.That(NearbyLanDiscoveryPolicy.ShouldSweep(1), Is.False);
			Assert.That(NearbyLanDiscoveryPolicy.ShouldSweep(2), Is.False);
			Assert.That(NearbyLanDiscoveryPolicy.ShouldSweep(3), Is.True);
			Assert.That(NearbyLanDiscoveryPolicy.AdvertisementTimeout,
				Is.GreaterThan(NearbyLanDiscoveryPolicy.SweepInterval * 2));
		}

		[Test]
		public void LanFallbackCapsUntrustedDirectoryGrowth()
		{
			Assert.That(NearbyLanDiscoveryPolicy.CanAddEntry(
				NearbyLanDiscoveryPolicy.MaximumEntries - 1,
				NearbyLanDiscoveryPolicy.MaximumEntriesPerSource - 1), Is.True);
			Assert.That(NearbyLanDiscoveryPolicy.CanAddEntry(
				NearbyLanDiscoveryPolicy.MaximumEntries,
				0), Is.False);
			Assert.That(NearbyLanDiscoveryPolicy.CanAddEntry(
				0,
				NearbyLanDiscoveryPolicy.MaximumEntriesPerSource), Is.False);
		}

		[Test]
		public void LanFallbackThrottlesRepeatedPayloadChangesFromOneEndpoint()
		{
			var interval = NearbyLanDiscoveryPolicy.MinimumUpdatePublishInterval;
			Assert.That(interval, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(500)));
			Assert.That(NearbyLanDiscoveryPolicy.ShouldPublishUpdate(interval - TimeSpan.FromMilliseconds(1)), Is.False);
			Assert.That(NearbyLanDiscoveryPolicy.ShouldPublishUpdate(interval), Is.True);
		}

		[Test]
		public void EmptyDiscoveryRefreshRequiresTheCurrentReadyEmptyBrowser()
		{
			Assert.That(NearbyDiscoveryLivenessPolicy.ShouldRefresh(
				9, 9, true, false, false, true, false), Is.True);
			Assert.That(NearbyDiscoveryLivenessPolicy.ShouldRefresh(
				8, 9, true, false, false, true, false), Is.False);
			Assert.That(NearbyDiscoveryLivenessPolicy.ShouldRefresh(
				9, 9, true, false, false, false, false), Is.False);
			Assert.That(NearbyDiscoveryLivenessPolicy.ShouldRefresh(
				9, 9, true, false, false, true, true), Is.False);
			Assert.That(NearbyDiscoveryLivenessPolicy.ShouldRefresh(
				9, 9, false, false, false, true, false), Is.False);
			Assert.That(NearbyDiscoveryLivenessPolicy.ShouldRefresh(
				9, 9, true, true, false, true, false), Is.False);
			Assert.That(NearbyDiscoveryLivenessPolicy.ShouldRefresh(
				9, 9, true, false, true, true, false), Is.False);
		}

		[Test]
		public void RelayDeadlineTimesOutTheWholeOperation()
		{
			var operation = new Func<CancellationToken, Task>(token =>
				Task.Delay(TimeSpan.FromMilliseconds(150), token));
			var task = NearbyRelayDeadlinePolicy.RunAsync(
				operation, TimeSpan.FromMilliseconds(20), CancellationToken.None);

			Assert.ThrowsAsync<TimeoutException>(async () => await task);
		}

		[Test]
		public void RelayDeadlinePreservesExternalCancellation()
		{
			using var cancellation = new CancellationTokenSource();
			cancellation.Cancel();
			var operation = new Func<CancellationToken, Task>(token =>
				Task.Delay(TimeSpan.FromMilliseconds(150), token));
			var task = NearbyRelayDeadlinePolicy.RunAsync(
				operation, TimeSpan.FromSeconds(1), cancellation.Token);

			Assert.CatchAsync<OperationCanceledException>(async () => await task);
		}
	}
}
