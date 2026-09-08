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
using System.Threading;
using System.Threading.Tasks;

#if IOS
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using CoreFoundation;
using Network;
using OpenRA.Network;
#endif

namespace OpenRA.Platforms.Default
{
	internal static class NearbyRetryPolicy
	{
		const int MaximumAttempts = 5;
		const int InitialDelayMilliseconds = 250;

		internal static TimeSpan? DelayForAttempt(int attempt)
		{
			if (attempt < 1 || attempt > MaximumAttempts)
				return null;

			return TimeSpan.FromMilliseconds(InitialDelayMilliseconds * (1 << (attempt - 1)));
		}

		internal static bool ShouldExecute(
			long scheduledGeneration,
			long currentGeneration,
			bool requested,
			bool suspended,
			bool disposed)
		{
			return scheduledGeneration == currentGeneration && requested && !suspended && !disposed;
		}
	}

	internal static class NearbyDiscoveryLivenessPolicy
	{
		const int MaximumRefreshAttempts = 5;
		static readonly int[] RefreshDelaysMilliseconds = { 3000, 6000, 12000, 15000 };

		internal static TimeSpan AdvertiserRegistrationTimeout => TimeSpan.FromSeconds(3);

		internal static TimeSpan? DelayForAttempt(int attempt)
		{
			if (attempt < 1)
				throw new ArgumentOutOfRangeException(nameof(attempt));
			if (attempt > MaximumRefreshAttempts)
				return null;

			var index = Math.Min(attempt, RefreshDelaysMilliseconds.Length) - 1;
			return TimeSpan.FromMilliseconds(RefreshDelaysMilliseconds[index]);
		}

		internal static bool ShouldRefresh(
			long scheduledGeneration,
			long currentGeneration,
			bool requested,
			bool suspended,
			bool disposed,
			bool ready,
			bool hasGames)
		{
			return NearbyRetryPolicy.ShouldExecute(
				scheduledGeneration, currentGeneration, requested, suspended, disposed) &&
				ready && !hasGames;
		}
	}

	internal static class NearbyLanDiscoveryPolicy
	{
		const int SweepEveryCycles = 3;

		internal const int MaximumEntries = 64;
		internal const int MaximumEntriesPerSource = 4;
		internal static TimeSpan SweepInterval => TimeSpan.FromSeconds(SweepEveryCycles * 2);
		internal static TimeSpan AdvertisementTimeout => TimeSpan.FromSeconds(15);
		internal static TimeSpan MinimumUpdatePublishInterval => TimeSpan.FromSeconds(1);

		internal static bool ShouldSweep(int cycle) => cycle >= 0 && cycle % SweepEveryCycles == 0;

		internal static bool CanAddEntry(int entryCount, int entriesFromSource) =>
			entryCount >= 0 && entriesFromSource >= 0 &&
			entryCount < MaximumEntries && entriesFromSource < MaximumEntriesPerSource;

		internal static bool ShouldPublishUpdate(TimeSpan sinceLastPublished) =>
			sinceLastPublished >= MinimumUpdatePublishInterval;
	}

	internal static class NearbyRelayDeadlinePolicy
	{
		internal static async Task RunAsync(
			Func<CancellationToken, Task> operation,
			TimeSpan timeout,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(operation);
			ValidateTimeout(timeout);

			using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			deadline.CancelAfter(timeout);
			try
			{
				await operation(deadline.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (
				!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
			{
				throw new TimeoutException($"Nearby relay establishment exceeded {timeout.TotalSeconds:0.###} seconds.");
			}
		}

		internal static async Task<T> RunWithResultAsync<T>(
			Func<CancellationToken, Task<T>> operation,
			TimeSpan timeout,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(operation);
			ValidateTimeout(timeout);

			using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			deadline.CancelAfter(timeout);
			try
			{
				return await operation(deadline.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (
				!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
			{
				throw new TimeoutException($"Nearby relay establishment exceeded {timeout.TotalSeconds:0.###} seconds.");
			}
		}

		static void ValidateTimeout(TimeSpan timeout)
		{
			if (timeout <= TimeSpan.Zero || timeout == Timeout.InfiniteTimeSpan)
				throw new ArgumentOutOfRangeException(nameof(timeout));
		}
	}

#if IOS
	public sealed class IosNearbyGameService : INearbyGameService
	{
		const string BonjourServiceType = "_openra-ra2._tcp";
		const string BonjourDomain = "local.";
		const string LogChannel = "client";
		// Microsoft.iOS 17.0.8478 omits the native nw_browser_state_waiting = 4 enum member.
		const NWBrowserState BrowserWaitingState = (NWBrowserState)4;

		static readonly DispatchQueue NetworkQueue = new("org.openra.nearby-networking");

		readonly object sync = new();
		readonly Dictionary<string, NearbyGameInfo> games = new(StringComparer.Ordinal);
		readonly HashSet<string> advertisedEndpoints = new(StringComparer.Ordinal);
		readonly HashSet<ConnectionRelay> relays = new();
		readonly string serviceName = "OpenRA-" + Guid.NewGuid().ToString("N")[..12];

		IReadOnlyList<NearbyGameInfo> gameSnapshot = Array.Empty<NearbyGameInfo>();
		NWBrowser browser;
		NearbyLanBrowser lanBrowser;
		NWListener advertiser;
		string advertisedPayload;
		int advertisedPort;
		bool browseRequested;
		bool advertiseRequested;
		bool suspended;
		bool disposed;
		long browserGeneration;
		long advertiserGeneration;
		int browserRetryAttempt;
		int advertiserRetryAttempt;
		int browserLivenessAttempt;
		bool browserRetryPending;
		bool advertiserRetryPending;
		long advertiserRegistrationCheckTicket;
		NWBrowserState browserState;

		public event Action GamesChanged = () => { };

		public IReadOnlyList<NearbyGameInfo> Games
		{
			get
			{
				lock (sync)
					return gameSnapshot;
			}
		}

		public void StartBrowsing()
		{
			lock (sync)
			{
				ThrowIfDisposed();
				browseRequested = true;
				if (browser == null && !suspended)
				{
					BeginBrowserGenerationLocked();
					StartBrowserLocked();
				}

				if (lanBrowser == null && !suspended)
					StartLanBrowserLocked();
			}
		}

		public void StopBrowsing()
		{
			NWBrowser oldBrowser;
			NearbyLanBrowser oldLanBrowser;
			bool changed;
			lock (sync)
			{
				if (disposed)
					return;

				browseRequested = false;
				InvalidateBrowserRetriesLocked();
				oldBrowser = DetachBrowserLocked();
				oldLanBrowser = DetachLanBrowserLocked();
				changed = ClearGamesLocked();
			}

			DisposeBrowser(oldBrowser);
			oldLanBrowser?.Dispose();
			if (changed)
				NotifyGamesChanged();
		}

		public void StartAdvertising(string payload, int localServerPort)
		{
			ArgumentNullException.ThrowIfNull(payload);
			if (localServerPort < 1 || localServerPort > ushort.MaxValue)
				throw new ArgumentOutOfRangeException(nameof(localServerPort));

			NWListener oldAdvertiser;
			lock (sync)
			{
				ThrowIfDisposed();
				advertiseRequested = true;
				advertisedPayload = payload;
				advertisedPort = localServerPort;
				BeginAdvertiserGenerationLocked();
				oldAdvertiser = DetachAdvertiserLocked();
			}

			DisposeAdvertiser(oldAdvertiser);

			lock (sync)
			{
				if (!disposed && !suspended && advertiseRequested && advertiser == null)
					StartAdvertiserLocked();
			}
		}

		public void UpdateAdvertising(string payload)
		{
			ArgumentNullException.ThrowIfNull(payload);

			lock (sync)
			{
				if (disposed || !advertiseRequested)
					return;

				advertisedPayload = payload;
				if (suspended)
					return;

				if (advertiser == null)
				{
					if (!advertiserRetryPending &&
						NearbyRetryPolicy.DelayForAttempt(advertiserRetryAttempt + 1) != null)
						StartAdvertiserLocked();

					return;
				}

				try
				{
					ApplyAdvertisement(advertiser, advertisedPayload, serviceName);
				}
				catch (Exception e)
				{
					Log.Write(LogChannel, $"Unable to update nearby-game advertisement: {e}");
				}
			}
		}

		public void StopAdvertising()
		{
			NWListener oldAdvertiser;
			lock (sync)
			{
				if (disposed)
					return;

				advertiseRequested = false;
				InvalidateAdvertiserRetriesLocked();
				advertisedPayload = null;
				advertisedPort = 0;
				oldAdvertiser = DetachAdvertiserLocked();
			}

			DisposeAdvertiser(oldAdvertiser);
		}

		public bool TryCreateConnectionTarget(string serviceId, out ConnectionTarget target)
		{
			target = null;
			if (NearbyLanDiscoveryProtocol.TryParseServiceId(serviceId, out var lanEndpoint))
			{
				target = new ConnectionTarget(lanEndpoint.Address.ToString(), lanEndpoint.Port);
				return true;
			}

			if (!TryDecodeServiceId(serviceId, out var name, out var type, out var domain) ||
				!string.Equals(type, BonjourServiceType, StringComparison.Ordinal))
				return false;

			using var endpoint = NWEndpoint.CreateBonjourService(name, type, domain);
			if (endpoint == null)
				return false;

			ConnectionRelay relay = null;
			try
			{
				relay = ConnectionRelay.CreateOutgoing(endpoint, OnRelayFinished, out target);
				var registered = false;
				lock (sync)
				{
					if (!disposed && !suspended)
					{
						relays.Add(relay);
						registered = true;
					}
				}

				if (!registered)
				{
					relay.Dispose();
					target = null;
					return false;
				}

				relay.Start();
				return true;
			}
			catch (Exception e)
			{
				relay?.Dispose();
				target = null;
				Log.Write(LogChannel, $"Unable to create nearby-game relay: {e}");
				return false;
			}
		}

		public void Suspend()
		{
			NWBrowser oldBrowser;
			NearbyLanBrowser oldLanBrowser;
			NWListener oldAdvertiser;
			ConnectionRelay[] oldRelays;
			bool changed;
			lock (sync)
			{
				if (disposed || suspended)
					return;

				suspended = true;
				InvalidateBrowserRetriesLocked();
				InvalidateAdvertiserRetriesLocked();
				oldBrowser = DetachBrowserLocked();
				oldLanBrowser = DetachLanBrowserLocked();
				oldAdvertiser = DetachAdvertiserLocked();
				oldRelays = relays.ToArray();
				relays.Clear();
				changed = ClearGamesLocked();
			}

			DisposeBrowser(oldBrowser);
			oldLanBrowser?.Dispose();
			DisposeAdvertiser(oldAdvertiser);
			foreach (var relay in oldRelays)
				relay.Dispose();

			if (changed)
				NotifyGamesChanged();
		}

		public void Resume()
		{
			lock (sync)
			{
				if (disposed || !suspended)
					return;

				suspended = false;
				if (browseRequested && browser == null)
				{
					BeginBrowserGenerationLocked();
					StartBrowserLocked();
				}

				if (browseRequested && lanBrowser == null)
					StartLanBrowserLocked();

				if (advertiseRequested && advertiser == null)
				{
					BeginAdvertiserGenerationLocked();
					StartAdvertiserLocked();
				}
			}
		}

		public void Dispose()
		{
			NWBrowser oldBrowser;
			NearbyLanBrowser oldLanBrowser;
			NWListener oldAdvertiser;
			ConnectionRelay[] oldRelays;
			bool changed;
			lock (sync)
			{
				if (disposed)
					return;

				disposed = true;
				browseRequested = false;
				advertiseRequested = false;
				InvalidateBrowserRetriesLocked();
				InvalidateAdvertiserRetriesLocked();
				oldBrowser = DetachBrowserLocked();
				oldLanBrowser = DetachLanBrowserLocked();
				oldAdvertiser = DetachAdvertiserLocked();
				oldRelays = relays.ToArray();
				relays.Clear();
				changed = ClearGamesLocked();
			}

			DisposeBrowser(oldBrowser);
			oldLanBrowser?.Dispose();
			DisposeAdvertiser(oldAdvertiser);
			foreach (var relay in oldRelays)
				relay.Dispose();

			if (changed)
				NotifyGamesChanged();
		}

		void StartBrowserLocked()
		{
			var generation = browserGeneration;
			NWBrowser newBrowser = null;
			try
			{
				using var descriptor = NWBrowserDescriptor.CreateBonjourService(BonjourServiceType);
				descriptor.IncludeTxtRecord = true;
				using var parameters = CreateTcpParameters();
				newBrowser = new NWBrowser(descriptor, parameters);
				newBrowser.SetDispatchQueue(NetworkQueue);
				newBrowser.SetStateChangesHandler((state, error) =>
					OnBrowserStateChanged(newBrowser, generation, state, error));
				newBrowser.IndividualChangesDelegate = (oldResult, newResult) =>
					OnBrowserChangeSafely(newBrowser, oldResult, newResult);
				browser = newBrowser;
				browserState = default;
				newBrowser.Start();
			}
			catch (Exception e)
			{
				browser = null;
				if (newBrowser != null)
					NetworkQueue.DispatchAsync(() => DisposeBrowser(newBrowser));

				Log.Write(LogChannel, $"Unable to start nearby-game browsing: {e}");
				ScheduleBrowserRetryLocked(generation);
			}
		}

		void StartLanBrowserLocked()
		{
			NearbyLanBrowser newBrowser = null;
			try
			{
				newBrowser = new NearbyLanBrowser(OnLanGamesChanged);
				lanBrowser = newBrowser;
				newBrowser.Start();
			}
			catch (Exception e)
			{
				if (ReferenceEquals(lanBrowser, newBrowser))
					lanBrowser = null;

				newBrowser?.Dispose();
				Log.Write(LogChannel, $"Unable to start nearby-game LAN discovery: {e}");
			}
		}

		void OnLanGamesChanged(NearbyLanBrowser owner, IReadOnlyList<NearbyGameInfo> snapshot)
		{
			var changed = false;
			lock (sync)
			{
				if (!ReferenceEquals(lanBrowser, owner) || suspended || disposed)
					return;

				var incoming = snapshot.ToDictionary(game => game.ServiceId, StringComparer.Ordinal);
				foreach (var serviceId in games.Keys
					.Where(NearbyLanDiscoveryProtocol.IsServiceId)
					.Where(serviceId => !incoming.ContainsKey(serviceId))
					.ToArray())
					changed |= games.Remove(serviceId);

				foreach (var pair in incoming)
				{
					if (!games.TryGetValue(pair.Key, out var previous) ||
						!string.Equals(previous.Payload, pair.Value.Payload, StringComparison.Ordinal))
					{
						games[pair.Key] = pair.Value;
						changed = true;
					}
				}

				if (changed)
				{
					RefreshSnapshotLocked();
					if (!HasBonjourGamesLocked() && browserState == NWBrowserState.Ready && browser != null)
						ScheduleBrowserLivenessCheckLocked(browser, browserGeneration);
				}
			}

			if (changed)
				NotifyGamesChanged();
		}

		void OnBrowserStateChanged(
			NWBrowser owner,
			long generation,
			NWBrowserState state,
			NWError error)
		{
			var current = false;
			lock (sync)
			{
				if (ReferenceEquals(browser, owner) && generation == browserGeneration)
				{
					browserState = state;
					current = true;
					if (state == NWBrowserState.Ready)
					{
						browserRetryAttempt = 0;
						browserRetryPending = false;
						ScheduleBrowserLivenessCheckLocked(owner, generation);
					}
				}
			}

			if (!current)
				return;

			Log.Write(LogChannel,
				$"Nearby-game Bonjour browser state {state} " +
				$"(generation {generation}, error {Describe(error)}).");

			if (state == NWBrowserState.Ready || state == BrowserWaitingState)
				return;

			if (state != NWBrowserState.Failed)
				return;

			NWBrowser oldBrowser = null;
			bool changed = false;
			var handled = false;
			lock (sync)
			{
				if (ReferenceEquals(browser, owner) && generation == browserGeneration)
				{
					handled = true;
					oldBrowser = DetachBrowserLocked();
					changed = ClearBonjourGamesLocked();
					ScheduleBrowserRetryLocked(generation);
				}
			}

			if (!handled)
				return;

			Log.Write(LogChannel, $"Nearby-game browser failed: {Describe(error)}");
			DisposeBrowser(oldBrowser);
			if (changed)
				NotifyGamesChanged();
		}

		void OnBrowserChangeSafely(
			NWBrowser owner,
			NWBrowseResult oldResult,
			NWBrowseResult newResult)
		{
			try
			{
				OnBrowserChange(owner, oldResult, newResult);
			}
			catch (Exception e)
			{
				Log.Write(LogChannel, $"Unable to process nearby-game browser result: {e}");
			}
		}

		void OnBrowserChange(
			NWBrowser owner,
			NWBrowseResult oldResult,
			NWBrowseResult newResult)
		{
			var changed = false;
			var diagnostics = new List<string>();
			lock (sync)
			{
				if (!ReferenceEquals(browser, owner) || suspended || disposed)
					return;

				if (oldResult != null)
				{
					using var oldEndpoint = oldResult.EndPoint;
					if (TryEncodeServiceId(oldEndpoint, out var oldServiceId))
						changed |= games.Remove(oldServiceId);
					else
						diagnostics.Add("ignored removed result with a non-Bonjour endpoint");
				}

				if (newResult != null)
				{
					using var newEndpoint = newResult.EndPoint;
					using var txtRecord = newResult.TxtRecord;
					if (!TryEncodeServiceId(newEndpoint, out var serviceId))
						diagnostics.Add("ignored added result with a non-Bonjour endpoint");
					else if (!TryReadPayload(txtRecord, out var payload, out var error))
						diagnostics.Add($"ignored invalid metadata for {serviceId}: {error}");
					else
					{
						games[serviceId] = new NearbyGameInfo(serviceId, payload);
						changed = true;
						browserLivenessAttempt = 0;
						diagnostics.Add($"accepted service {serviceId}");
					}
				}

				if (changed)
				{
					RefreshSnapshotLocked();
					if (!HasBonjourGamesLocked() && browserState == NWBrowserState.Ready)
						ScheduleBrowserLivenessCheckLocked(owner, browserGeneration);
				}
			}

			foreach (var diagnostic in diagnostics)
				Log.Write(LogChannel, $"Nearby-game browser result: {diagnostic}.");

			if (changed)
				NotifyGamesChanged();
		}

		void StartAdvertiserLocked()
		{
			var generation = advertiserGeneration;
			NWListener newAdvertiser = null;
			try
			{
				using var parameters = CreateTcpParameters();
				newAdvertiser = NWListener.Create(parameters) ??
					throw new InvalidOperationException("Network.framework did not create a listener.");

				newAdvertiser.SetQueue(NetworkQueue);
				newAdvertiser.SetStateChangedHandler((state, error) =>
					OnAdvertiserStateChanged(newAdvertiser, generation, state, error));
				newAdvertiser.SetNewConnectionHandler(connection =>
					OnIncomingConnection(newAdvertiser, connection));
				newAdvertiser.SetAdvertisedEndpointChangedHandler((endpoint, added) =>
					OnAdvertisedEndpointChanged(newAdvertiser, generation, endpoint, added));
				ApplyAdvertisement(newAdvertiser, advertisedPayload, serviceName);
				advertiser = newAdvertiser;
				newAdvertiser.Start();
			}
			catch (Exception e)
			{
				advertiser = null;
				if (newAdvertiser != null)
					NetworkQueue.DispatchAsync(() => DisposeAdvertiser(newAdvertiser));

				Log.Write(LogChannel, $"Unable to advertise nearby game: {e}");
				ScheduleAdvertiserRetryLocked(generation);
			}
		}

		void OnAdvertiserStateChanged(
			NWListener owner,
			long generation,
			NWListenerState state,
			NWError error)
		{
			lock (sync)
				if (!ReferenceEquals(advertiser, owner) || generation != advertiserGeneration)
					return;

			Log.Write(LogChannel,
				$"Nearby-game Bonjour listener state {state} " +
				$"(generation {generation}, error {Describe(error)}).");

			if (state == NWListenerState.Ready)
			{
				var current = false;
				ushort port = 0;
				var registeredEndpointCount = 0;
				lock (sync)
				{
					if (ReferenceEquals(advertiser, owner) && generation == advertiserGeneration)
					{
						port = owner.Port;
						registeredEndpointCount = advertisedEndpoints.Count;
						if (registeredEndpointCount == 0)
							ScheduleAdvertiserRegistrationCheckLocked(owner, generation);
						current = true;
					}
				}

				if (current)
					Log.Write(LogChannel,
						$"Nearby-game Bonjour listener is ready on Network.framework port {port}; " +
						$"registered endpoints: {registeredEndpointCount}.");

				return;
			}

			if (state != NWListenerState.Failed)
				return;

			NWListener oldAdvertiser = null;
			var handled = false;
			lock (sync)
			{
				if (ReferenceEquals(advertiser, owner) && generation == advertiserGeneration)
				{
					handled = true;
					oldAdvertiser = DetachAdvertiserLocked();
					ScheduleAdvertiserRetryLocked(generation);
				}
			}

			if (!handled)
				return;

			Log.Write(LogChannel, $"Nearby-game advertiser failed: {Describe(error)}");
			DisposeAdvertiser(oldAdvertiser);
		}

		void OnAdvertisedEndpointChanged(
			NWListener owner,
			long generation,
			NWEndpoint endpoint,
			bool added)
		{
			var endpointDescription = DescribeEndpoint(endpoint);
			var current = false;
			lock (sync)
			{
				if (ReferenceEquals(advertiser, owner) && generation == advertiserGeneration)
				{
					current = true;
					if (added)
					{
						advertisedEndpoints.Add(endpointDescription);
						advertiserRetryAttempt = 0;
						advertiserRetryPending = false;
						advertiserRegistrationCheckTicket++;
					}
					else
					{
						advertisedEndpoints.Remove(endpointDescription);
						if (advertisedEndpoints.Count == 0)
							ScheduleAdvertiserRegistrationCheckLocked(owner, generation);
					}
				}
			}

			if (current)
				Log.Write(LogChannel,
					$"Nearby-game Bonjour endpoint {(added ? "registered" : "removed")}: " +
					$"{endpointDescription} (generation {generation}).");
		}

		void OnIncomingConnection(NWListener owner, NWConnection connection)
		{
			ConnectionRelay relay = null;
			try
			{
				lock (sync)
				{
					if (!disposed && !suspended && ReferenceEquals(advertiser, owner))
					{
						relay = ConnectionRelay.CreateIncoming(
							connection, advertisedPort, OnRelayFinished);
						relays.Add(relay);
					}
				}
			}
			catch (Exception e)
			{
				Log.Write(LogChannel, $"Unable to prepare nearby-game connection: {e}");
			}

			if (relay == null)
			{
				connection.Cancel();
				connection.Dispose();
				return;
			}

			try
			{
				relay.Start();
			}
			catch (Exception e)
			{
				Log.Write(LogChannel, $"Unable to accept nearby-game connection: {e}");
				relay.Dispose();
			}
		}

		void OnRelayFinished(ConnectionRelay relay)
		{
			lock (sync)
				relays.Remove(relay);
		}

		void BeginBrowserGenerationLocked()
		{
			browserGeneration = unchecked(browserGeneration + 1);
			browserRetryAttempt = 0;
			browserLivenessAttempt = 0;
			browserRetryPending = false;
			browserState = default;
		}

		void InvalidateBrowserRetriesLocked()
		{
			BeginBrowserGenerationLocked();
		}

		void BeginAdvertiserGenerationLocked()
		{
			advertiserGeneration = unchecked(advertiserGeneration + 1);
			advertiserRetryAttempt = 0;
			advertiserRetryPending = false;
			advertiserRegistrationCheckTicket++;
			advertisedEndpoints.Clear();
		}

		void InvalidateAdvertiserRetriesLocked()
		{
			BeginAdvertiserGenerationLocked();
		}

		void ScheduleBrowserLivenessCheckLocked(NWBrowser owner, long generation)
		{
			if (!NearbyDiscoveryLivenessPolicy.ShouldRefresh(
				generation, browserGeneration, browseRequested, suspended, disposed,
				browserState == NWBrowserState.Ready, HasBonjourGamesLocked()))
				return;

			var attempt = ++browserLivenessAttempt;
			var delay = NearbyDiscoveryLivenessPolicy.DelayForAttempt(attempt);
			if (delay == null)
			{
				Log.Write(LogChannel,
					$"Empty nearby-game browser refresh budget exhausted " +
					$"(generation {generation}).");
				return;
			}

			Log.Write(LogChannel,
				$"Checking empty nearby-game browser in {delay.Value.TotalMilliseconds:0} ms " +
				$"(generation {generation}, attempt {attempt}).");
			NetworkQueue.DispatchAfter(new DispatchTime(DispatchTime.Now, delay.Value), () =>
				OnBrowserLivenessCheck(owner, generation, attempt));
		}

		void OnBrowserLivenessCheck(NWBrowser owner, long generation, int attempt)
		{
			NWBrowser oldBrowser = null;
			lock (sync)
			{
				if (attempt != browserLivenessAttempt || !ReferenceEquals(browser, owner) ||
					!NearbyDiscoveryLivenessPolicy.ShouldRefresh(
						generation, browserGeneration, browseRequested, suspended, disposed,
						browserState == NWBrowserState.Ready, HasBonjourGamesLocked()))
					return;

				oldBrowser = DetachBrowserLocked();
			}

			Log.Write(LogChannel,
				$"Restarting empty nearby-game browser (generation {generation}, attempt {attempt}).");
			DisposeBrowser(oldBrowser);
			lock (sync)
			{
				if (NearbyRetryPolicy.ShouldExecute(
					generation, browserGeneration, browseRequested, suspended, disposed) &&
					browser == null)
					StartBrowserLocked();
			}
		}

		void ScheduleAdvertiserRegistrationCheckLocked(NWListener owner, long generation)
		{
			if (!NearbyRetryPolicy.ShouldExecute(
				generation, advertiserGeneration, advertiseRequested, suspended, disposed) ||
				advertisedEndpoints.Count != 0)
				return;

			var ticket = ++advertiserRegistrationCheckTicket;
			var delay = NearbyDiscoveryLivenessPolicy.AdvertiserRegistrationTimeout;
			Log.Write(LogChannel,
				$"Waiting {delay.TotalMilliseconds:0} ms for nearby-game Bonjour registration " +
				$"(generation {generation}, ticket {ticket}).");
			NetworkQueue.DispatchAfter(new DispatchTime(DispatchTime.Now, delay), () =>
				OnAdvertiserRegistrationCheck(owner, generation, ticket));
		}

		void OnAdvertiserRegistrationCheck(NWListener owner, long generation, long ticket)
		{
			NWListener oldAdvertiser = null;
			lock (sync)
			{
				if (ticket != advertiserRegistrationCheckTicket ||
					!ReferenceEquals(advertiser, owner) || advertisedEndpoints.Count != 0 ||
					!NearbyRetryPolicy.ShouldExecute(
						generation, advertiserGeneration, advertiseRequested, suspended, disposed))
					return;

				oldAdvertiser = DetachAdvertiserLocked();
				ScheduleAdvertiserRetryLocked(generation);
			}

			Log.Write(LogChannel,
				$"Nearby-game listener did not register a Bonjour endpoint " +
				$"(generation {generation}); restarting it.");
			DisposeAdvertiser(oldAdvertiser);
		}

		void ScheduleBrowserRetryLocked(long generation)
		{
			if (!NearbyRetryPolicy.ShouldExecute(
				generation, browserGeneration, browseRequested, suspended, disposed))
				return;

			var attempt = browserRetryAttempt + 1;
			var delay = NearbyRetryPolicy.DelayForAttempt(attempt);
			if (delay == null)
			{
				browserRetryPending = false;
				Log.Write(LogChannel, "Nearby-game browser retry budget exhausted.");
				return;
			}

			browserRetryAttempt = attempt;
			browserRetryPending = true;
			Log.Write(LogChannel,
				$"Retrying nearby-game browser in {delay.Value.TotalMilliseconds:0} ms " +
				$"(attempt {attempt}).");
			NetworkQueue.DispatchAfter(new DispatchTime(DispatchTime.Now, delay.Value), () =>
			{
				lock (sync)
				{
					if (!browserRetryPending || browserRetryAttempt != attempt || browser != null ||
						!NearbyRetryPolicy.ShouldExecute(
							generation, browserGeneration, browseRequested, suspended, disposed))
						return;

					browserRetryPending = false;
					StartBrowserLocked();
				}
			});
		}

		void ScheduleAdvertiserRetryLocked(long generation)
		{
			if (!NearbyRetryPolicy.ShouldExecute(
				generation, advertiserGeneration, advertiseRequested, suspended, disposed))
				return;

			var attempt = advertiserRetryAttempt + 1;
			var delay = NearbyRetryPolicy.DelayForAttempt(attempt);
			if (delay == null)
			{
				advertiserRetryPending = false;
				Log.Write(LogChannel, "Nearby-game advertiser retry budget exhausted.");
				return;
			}

			advertiserRetryAttempt = attempt;
			advertiserRetryPending = true;
			Log.Write(LogChannel,
				$"Retrying nearby-game advertiser in {delay.Value.TotalMilliseconds:0} ms " +
				$"(attempt {attempt}).");
			NetworkQueue.DispatchAfter(new DispatchTime(DispatchTime.Now, delay.Value), () =>
			{
				lock (sync)
				{
					if (!advertiserRetryPending || advertiserRetryAttempt != attempt || advertiser != null ||
						!NearbyRetryPolicy.ShouldExecute(
							generation, advertiserGeneration, advertiseRequested, suspended, disposed))
						return;

					advertiserRetryPending = false;
					StartAdvertiserLocked();
				}
			});
		}

		NWBrowser DetachBrowserLocked()
		{
			var oldBrowser = browser;
			browser = null;
			browserState = default;
			return oldBrowser;
		}

		NearbyLanBrowser DetachLanBrowserLocked()
		{
			var oldBrowser = lanBrowser;
			lanBrowser = null;
			return oldBrowser;
		}

		NWListener DetachAdvertiserLocked()
		{
			var oldAdvertiser = advertiser;
			advertiser = null;
			advertisedEndpoints.Clear();
			advertiserRegistrationCheckTicket++;
			return oldAdvertiser;
		}

		bool ClearGamesLocked()
		{
			if (games.Count == 0)
				return false;

			games.Clear();
			RefreshSnapshotLocked();
			return true;
		}

		bool ClearBonjourGamesLocked()
		{
			var removed = false;
			foreach (var serviceId in games.Keys
				.Where(serviceId => !NearbyLanDiscoveryProtocol.IsServiceId(serviceId))
				.ToArray())
				removed |= games.Remove(serviceId);

			if (removed)
				RefreshSnapshotLocked();

			return removed;
		}

		bool HasBonjourGamesLocked() => games.Keys.Any(serviceId =>
			!NearbyLanDiscoveryProtocol.IsServiceId(serviceId));

		void RefreshSnapshotLocked()
		{
			gameSnapshot = Array.AsReadOnly(games.Values
				.OrderBy(game => NearbyLanDiscoveryProtocol.IsServiceId(game.ServiceId) ? 0 : 1)
				.ThenBy(game => game.ServiceId, StringComparer.Ordinal)
				.ToArray());
		}

		void NotifyGamesChanged()
		{
			try
			{
				GamesChanged();
			}
			catch (Exception e)
			{
				Log.Write(LogChannel, $"Nearby-game change subscriber failed: {e}");
			}
		}

		void ThrowIfDisposed()
		{
			if (disposed)
				throw new ObjectDisposedException(nameof(IosNearbyGameService));
		}

		static NWParameters CreateTcpParameters()
		{
			var parameters = NWParameters.CreateTcp(options =>
			{
				if (options is NWProtocolTcpOptions tcp)
				{
					tcp.SetNoDelay(true);
					tcp.SetEnableKeepAlive(true);
					tcp.SetConnectionTimeout(TimeSpan.FromSeconds(10));
				}
			});
			parameters.IncludePeerToPeer = true;
			parameters.ReuseLocalAddress = true;
			return parameters;
		}

		static void ApplyAdvertisement(NWListener listener, string payload, string name)
		{
			using var descriptor = NWAdvertiseDescriptor.CreateBonjourService(
				name, BonjourServiceType) ??
				throw new InvalidOperationException("Network.framework did not create a Bonjour descriptor.");
			using var record = NWTxtRecord.CreateDictionary() ??
				throw new InvalidOperationException("Network.framework did not create a TXT record.");

			foreach (var field in NearbyGamePayloadCodec.Encode(payload))
				if (!record.Add(field.Key, field.Value))
					throw new InvalidOperationException($"Unable to add Bonjour TXT field `{field.Key}`.");

			descriptor.TxtRecord = record;
			listener.SetAdvertiseDescriptor(descriptor);
		}

		static bool TryReadPayload(NWTxtRecord record, out string payload, out string error)
		{
			payload = null;
			error = null;
			if (record == null)
			{
				error = "missing Bonjour TXT record";
				return false;
			}

			var fields = new Dictionary<string, string>(StringComparer.Ordinal);
			try
			{
				record.Apply((key, result, value) =>
				{
					if (key != null && (result == NWTxtRecordFindKey.NonEmptyValue ||
						result == NWTxtRecordFindKey.EmptyValue))
						fields[key] = Encoding.UTF8.GetString(value);

					return true;
				});
			}
			catch (Exception e)
			{
				error = "unable to read Bonjour TXT record: " + e.Message;
				return false;
			}

			return NearbyGamePayloadCodec.TryDecode(fields, out payload, out error);
		}

		static bool TryEncodeServiceId(NWEndpoint endpoint, out string serviceId)
		{
			serviceId = null;
			if (endpoint.Type != NWEndpointType.BonjourService ||
				string.IsNullOrEmpty(endpoint.BonjourServiceName) ||
				string.IsNullOrEmpty(endpoint.BonjourServiceType))
				return false;

			var domain = string.IsNullOrEmpty(endpoint.BonjourServiceDomain) ?
				BonjourDomain : endpoint.BonjourServiceDomain;
			serviceId = string.Join(".",
				EncodeServiceIdPart(endpoint.BonjourServiceName),
				EncodeServiceIdPart(endpoint.BonjourServiceType),
				EncodeServiceIdPart(domain));
			return true;
		}

		static bool TryDecodeServiceId(
			string serviceId,
			out string name,
			out string type,
			out string domain)
		{
			name = null;
			type = null;
			domain = null;
			if (string.IsNullOrEmpty(serviceId))
				return false;

			var parts = serviceId.Split('.');
			return parts.Length == 3 &&
				TryDecodeServiceIdPart(parts[0], out name) &&
				TryDecodeServiceIdPart(parts[1], out type) &&
				TryDecodeServiceIdPart(parts[2], out domain) &&
				!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(type) && !string.IsNullOrEmpty(domain);
		}

		static string EncodeServiceIdPart(string value)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
				.TrimEnd('=')
				.Replace('+', '-')
				.Replace('/', '_');
		}

		static bool TryDecodeServiceIdPart(string value, out string decoded)
		{
			decoded = null;
			try
			{
				var base64 = value.Replace('-', '+').Replace('_', '/');
				base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
				decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
				return true;
			}
			catch (FormatException)
			{
				return false;
			}
		}

		static string Describe(NWError error)
		{
			return error == null ? "unknown Network.framework error" :
				$"{error.ErrorDomain}/{error.ErrorCode}";
		}

		static string DescribeEndpoint(NWEndpoint endpoint)
		{
			if (endpoint == null)
				return "<null>";

			if (TryEncodeServiceId(endpoint, out var serviceId))
				return serviceId;

			return $"{endpoint.Type}:{endpoint}";
		}

		static void DisposeBrowser(NWBrowser oldBrowser)
		{
			if (oldBrowser == null)
				return;

			try
			{
				oldBrowser.CompleteChangesDelegate = null;
				oldBrowser.IndividualChangesDelegate = null;
				oldBrowser.Cancel();
				oldBrowser.Dispose();
			}
			catch (Exception e)
			{
				Log.Write(LogChannel, $"Unable to stop nearby-game browser cleanly: {e}");
			}
		}

		static void DisposeAdvertiser(NWListener oldAdvertiser)
		{
			if (oldAdvertiser == null)
				return;

			try
			{
				oldAdvertiser.Cancel();
				oldAdvertiser.Dispose();
			}
			catch (Exception e)
			{
				Log.Write(LogChannel, $"Unable to stop nearby-game advertiser cleanly: {e}");
			}
		}

		sealed class NearbyLanBrowser : IDisposable
		{
			sealed class Entry
			{
				public NearbyGameInfo Game { get; set; }
				public IPAddress SourceAddress { get; set; }
				public DateTime LastSeenUtc { get; set; }
				public DateTime LastPublishedUtc { get; set; }
			}

			readonly Action<NearbyLanBrowser, IReadOnlyList<NearbyGameInfo>> changed;
			readonly UdpClient udp = new();
			readonly Thread thread;
			readonly EventWaitHandle wake = new(false, EventResetMode.AutoReset);
			readonly object entriesSync = new();
			readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
			IPEndPoint[] broadcastTargets;
			IPEndPoint[] unicastTargets;
			volatile bool running;
			bool started;
			int scanCycle;
			int receivePending;
			DateTime lastFailureLoggedUtc = DateTime.MinValue;
			DateTime lastInvalidResponseLoggedUtc = DateTime.MinValue;
			DateTime lastCapacityLoggedUtc = DateTime.MinValue;

			public NearbyLanBrowser(Action<NearbyLanBrowser, IReadOnlyList<NearbyGameInfo>> changed)
			{
				this.changed = changed ?? throw new ArgumentNullException(nameof(changed));
				udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				udp.EnableBroadcast = true;
				udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
				BuildDiscoveryTargets(out broadcastTargets, out unicastTargets);
				thread = new Thread(BackgroundLoop)
				{
					IsBackground = true,
					Name = "OpenRA iOS nearby LAN discovery"
				};
			}

			public void Start()
			{
				if (started)
					return;

				started = true;
				running = true;
				EnsureReceive();
				thread.Start();
				Log.Write(LogChannel,
					$"Nearby-game LAN discovery started with {broadcastTargets.Length} broadcast " +
					$"and {unicastTargets.Length} unicast targets.");
			}

			void EnsureReceive()
			{
				if (!running || Interlocked.CompareExchange(ref receivePending, 1, 0) != 0)
					return;

				try
				{
					udp.BeginReceive(ResponseReceived, null);
				}
				catch (ObjectDisposedException)
				{
					Interlocked.Exchange(ref receivePending, 0);
				}
				catch (Exception e)
				{
					Interlocked.Exchange(ref receivePending, 0);
					Log.Write(LogChannel, $"Nearby-game LAN receive failed: {e}");
				}
			}

			void ResponseReceived(IAsyncResult result)
			{
				try
				{
					var remote = new IPEndPoint(IPAddress.Any, 0);
					var packet = udp.EndReceive(result, ref remote);
					if (NearbyLanDiscoveryProtocol.TryDecodeGameAdvertisement(
						packet, remote.Address, out var gameEndpoint, out var payload))
						Update(gameEndpoint, payload);
					else if (DateTime.UtcNow - lastInvalidResponseLoggedUtc > TimeSpan.FromSeconds(30))
					{
						lastInvalidResponseLoggedUtc = DateTime.UtcNow;
						Log.Write(LogChannel,
							$"Ignored invalid nearby-game LAN response from {remote}.");
					}
				}
				catch (ObjectDisposedException) { }
				catch (Exception e)
				{
					Log.Write(LogChannel, $"Nearby-game LAN response failed: {e}");
				}
				finally
				{
					Interlocked.Exchange(ref receivePending, 0);
					if (running)
						EnsureReceive();
				}
			}

			void BackgroundLoop()
			{
				var query = NearbyLanDiscoveryProtocol.EncodeQuery();
				while (running)
				{
					var sweep = NearbyLanDiscoveryPolicy.ShouldSweep(scanCycle++);
					if (sweep)
						BuildDiscoveryTargets(out broadcastTargets, out unicastTargets);

					var sent = 0;
					Exception lastError = null;
					foreach (var target in broadcastTargets)
					{
						if (!running)
							break;

						try
						{
							udp.Send(query, query.Length, target);
							sent++;
						}
						catch (Exception e)
						{
							lastError = e;
						}
					}

					// iOS broadcast delivery requires a separately approved multicast
					// entitlement.  Run an immediate bounded subnet sweep, then repeat at
					// a lower cadence as the entitlement-free same-Wi-Fi fallback.
					if (sweep)
						foreach (var target in unicastTargets)
						{
							if (!running)
								break;

							try
							{
								udp.Send(query, query.Length, target);
								sent++;
							}
							catch (Exception e)
							{
								lastError = e;
							}
						}

					if (sent == 0 && DateTime.UtcNow - lastFailureLoggedUtc > TimeSpan.FromSeconds(30))
					{
						lastFailureLoggedUtc = DateTime.UtcNow;
						Log.Write(LogChannel,
							$"Nearby-game LAN discovery could not route a target: {lastError?.Message}");
					}

					wake.WaitOne(2000);
					Prune();
					EnsureReceive();
				}
			}

			void Update(IPEndPoint endpoint, string payload)
			{
				var serviceId = NearbyLanDiscoveryProtocol.FormatServiceId(endpoint);
				IReadOnlyList<NearbyGameInfo> snapshot = null;
				var capacityRejected = false;
				var discovered = false;
				var now = DateTime.UtcNow;
				lock (entriesSync)
				{
					var game = new NearbyGameInfo(serviceId, payload);
					if (!entries.TryGetValue(serviceId, out var entry))
					{
						var entriesFromSource = entries.Values.Count(candidate =>
							candidate.SourceAddress.Equals(endpoint.Address));
						if (!NearbyLanDiscoveryPolicy.CanAddEntry(entries.Count, entriesFromSource))
							capacityRejected = true;
						else
						{
							entries.Add(serviceId, new Entry
							{
								Game = game,
								SourceAddress = endpoint.Address,
								LastSeenUtc = now,
								LastPublishedUtc = now
							});
							discovered = true;
							snapshot = SnapshotLocked();
						}
					}
					else
					{
						entry.LastSeenUtc = now;
						if (!string.Equals(entry.Game.Payload, payload, StringComparison.Ordinal))
						{
							if (NearbyLanDiscoveryPolicy.ShouldPublishUpdate(now - entry.LastPublishedUtc))
							{
								entry.Game = game;
								entry.LastPublishedUtc = now;
								snapshot = SnapshotLocked();
							}
						}
					}
				}

				if (capacityRejected)
				{
					if (DateTime.UtcNow - lastCapacityLoggedUtc > TimeSpan.FromSeconds(30))
					{
						lastCapacityLoggedUtc = DateTime.UtcNow;
						Log.Write(LogChannel,
							$"Ignored nearby-game LAN service {serviceId}: directory capacity reached.");
					}

					return;
				}

				if (snapshot == null)
					return;

				if (discovered)
					Log.Write(LogChannel,
						$"Nearby-game LAN service discovered at {endpoint} ({serviceId}).");
				changed(this, snapshot);
			}

			void Prune()
			{
				IReadOnlyList<NearbyGameInfo> snapshot = null;
				lock (entriesSync)
				{
					var cutoff = DateTime.UtcNow - NearbyLanDiscoveryPolicy.AdvertisementTimeout;
					var expired = entries
						.Where(pair => pair.Value.LastSeenUtc < cutoff)
						.Select(pair => pair.Key)
						.ToArray();
					if (expired.Length == 0)
						return;

					foreach (var serviceId in expired)
						entries.Remove(serviceId);

					snapshot = SnapshotLocked();
				}

				changed(this, snapshot);
			}

			IReadOnlyList<NearbyGameInfo> SnapshotLocked() => Array.AsReadOnly(entries.Values
				.Select(entry => entry.Game)
				.OrderBy(game => game.ServiceId, StringComparer.Ordinal)
				.ToArray());

			static void BuildDiscoveryTargets(
				out IPEndPoint[] broadcasts,
				out IPEndPoint[] unicasts)
			{
				var broadcastAddresses = new HashSet<IPAddress>();
				var unicastAddresses = new HashSet<IPAddress>();
				try
				{
					foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
					{
						if (network.OperationalStatus != OperationalStatus.Up ||
							network.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
							IsExcludedInterface(network.Name))
							continue;

						foreach (var unicast in network.GetIPProperties().UnicastAddresses)
						{
							var address = unicast.Address;
							if (address.AddressFamily != AddressFamily.InterNetwork ||
								IPAddress.IsLoopback(address))
								continue;

							if (!IsPrivate(address))
								continue;

							var mask = unicast.IPv4Mask ?? IPAddress.Parse("255.255.255.0");
							broadcastAddresses.Add(GetDirectedBroadcast(address, mask));
							foreach (var target in NearbyLanDiscoveryProtocol.BuildUnicastTargets(address, mask))
								unicastAddresses.Add(target);
						}
					}
				}
				catch (Exception e)
				{
					Log.Write(LogChannel,
						$"Nearby-game LAN interface enumeration failed: {e.Message}");
				}

				broadcastAddresses.Add(IPAddress.Broadcast);
				broadcasts = broadcastAddresses
					.Select(address => new IPEndPoint(address, NearbyLanDiscoveryProtocol.DiscoveryPort))
					.ToArray();
				unicasts = unicastAddresses
					.Select(address => new IPEndPoint(address, NearbyLanDiscoveryProtocol.DiscoveryPort))
					.ToArray();
			}

			static bool IsExcludedInterface(string name) => name != null &&
				(name.StartsWith("pdp_", StringComparison.OrdinalIgnoreCase) ||
				name.StartsWith("utun", StringComparison.OrdinalIgnoreCase) ||
				name.StartsWith("ipsec", StringComparison.OrdinalIgnoreCase));

			static bool IsPrivate(IPAddress address)
			{
				var bytes = address.GetAddressBytes();
				return bytes[0] == 10 ||
					(bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
					(bytes[0] == 192 && bytes[1] == 168);
			}

			static IPAddress GetDirectedBroadcast(IPAddress address, IPAddress mask)
			{
				var addressBytes = address.GetAddressBytes();
				var maskBytes = mask.GetAddressBytes();
				if (addressBytes.Length != 4 || maskBytes.Length != 4)
					throw new ArgumentException("LAN discovery requires IPv4 addresses and masks.");

				var result = new byte[4];
				for (var i = 0; i < result.Length; i++)
					result[i] = (byte)(addressBytes[i] | ~maskBytes[i]);

				return new IPAddress(result);
			}

			public void Dispose()
			{
				running = false;
				wake.Set();
				udp.Dispose();
				if (started && thread.IsAlive && !ReferenceEquals(Thread.CurrentThread, thread))
					thread.Join();

				wake.Dispose();
			}
		}

		sealed class ConnectionRelay : IDisposable
		{
			const int BufferSize = 64 * 1024;
			static readonly TimeSpan EstablishmentTimeout = TimeSpan.FromSeconds(12);
			static readonly TimeSpan HalfCloseDrainTimeout = TimeSpan.FromSeconds(2);

			readonly object resourceSync = new();
			readonly NWConnection connection;
			readonly TcpListener loopbackListener;
			readonly int localServerPort;
			readonly Action<ConnectionRelay> finished;
			readonly CancellationTokenSource cancellation = new();
			readonly bool outgoing;

			TcpClient localClient;
			int disposed;

			ConnectionRelay(
				NWConnection connection,
				TcpListener loopbackListener,
				TcpClient localClient,
				int localServerPort,
				bool outgoing,
				Action<ConnectionRelay> finished)
			{
				this.connection = connection;
				this.loopbackListener = loopbackListener;
				this.localClient = localClient;
				this.localServerPort = localServerPort;
				this.outgoing = outgoing;
				this.finished = finished;
			}

			public static ConnectionRelay CreateOutgoing(
				NWEndpoint endpoint,
				Action<ConnectionRelay> finished,
				out ConnectionTarget target)
			{
				var listener = new TcpListener(IPAddress.Loopback, 0);
				listener.Start(1);
				try
				{
					using var parameters = CreateTcpParameters();
					var connection = new NWConnection(endpoint, parameters);
					var port = ((IPEndPoint)listener.LocalEndpoint).Port;
					target = new ConnectionTarget(IPAddress.Loopback.ToString(), port);
					return new ConnectionRelay(
						connection, listener, null, 0, true, finished);
				}
				catch
				{
					listener.Stop();
					throw;
				}
			}

			public static ConnectionRelay CreateIncoming(
				NWConnection connection,
				int localServerPort,
				Action<ConnectionRelay> finished)
			{
				var localClient = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
				return new ConnectionRelay(
					connection, null, localClient, localServerPort, false, finished);
			}

			public void Start()
			{
				connection.SetQueue(NetworkQueue);
				var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
				connection.SetStateChangeHandler((state, error) => OnConnectionStateChanged(ready, state, error));
				var establishment = NearbyRelayDeadlinePolicy.RunWithResultAsync(
					token => outgoing ? EstablishOutgoingAsync(ready.Task, token) :
					EstablishIncomingAsync(ready.Task, token),
					EstablishmentTimeout,
					cancellation.Token);
				_ = Task.Run(() => RunAsync(establishment));
				connection.Start();
			}

			async Task RunAsync(Task<NetworkStream> establishment)
			{
				try
				{
					var stream = await establishment.ConfigureAwait(false);
					await PumpAsync(stream).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
				catch (TimeoutException e)
				{
					Log.Write(LogChannel, $"Nearby-game relay establishment timed out: {e.Message}");
				}
				catch (Exception e)
				{
					var side = outgoing ? "client" : "host";
					Log.Write(LogChannel, $"Nearby-game {side} relay failed: {e}");
				}
				finally
				{
					Dispose();
				}
			}

			async Task<NetworkStream> EstablishOutgoingAsync(Task ready, CancellationToken deadline)
			{
				var accepted = await loopbackListener.AcceptTcpClientAsync(deadline).ConfigureAwait(false);
				accepted.NoDelay = true;
				lock (resourceSync)
				{
					if (disposed != 0)
					{
						accepted.Dispose();
						throw new OperationCanceledException(cancellation.Token);
					}

					localClient = accepted;
				}

				loopbackListener.Stop();
				await ready.WaitAsync(deadline).ConfigureAwait(false);
				return accepted.GetStream();
			}

			async Task<NetworkStream> EstablishIncomingAsync(Task ready, CancellationToken deadline)
			{
				await localClient.ConnectAsync(IPAddress.Loopback, localServerPort, deadline).ConfigureAwait(false);
				await ready.WaitAsync(deadline).ConfigureAwait(false);
				return localClient.GetStream();
			}

			void OnConnectionStateChanged(
				TaskCompletionSource<bool> ready,
				NWConnectionState state,
				NWError error)
			{
				switch (state)
				{
					case NWConnectionState.Ready:
						ready.TrySetResult(true);
						break;
					case NWConnectionState.Failed:
						var message = $"Network.framework connection failed: {Describe(error)}";
						Log.Write(LogChannel, message);
						ready.TrySetException(new IOException(message));
						cancellation.Cancel();
						break;
					case NWConnectionState.Cancelled:
						cancellation.Cancel();
						ready.TrySetCanceled(cancellation.Token);
						break;
				}
			}

			async Task PumpAsync(NetworkStream stream)
			{
				var toNetwork = CopyToNetworkAsync(stream);
				var fromNetwork = CopyFromNetworkAsync(stream);
				var first = await Task.WhenAny(toNetwork, fromNetwork);
				var remaining = ReferenceEquals(first, toNetwork) ? fromNetwork : toNetwork;
				try
				{
					await first;
				}
				catch
				{
					cancellation.Cancel();
					_ = ObserveCompletionAsync(remaining);
					throw;
				}

				try
				{
					await remaining.WaitAsync(HalfCloseDrainTimeout, cancellation.Token);
				}
				catch (TimeoutException)
				{
					Log.Write(LogChannel,
						$"Nearby-game relay half-close drain exceeded {HalfCloseDrainTimeout.TotalSeconds:0.#} seconds.");
					cancellation.Cancel();
					_ = ObserveCompletionAsync(remaining);
				}
				catch
				{
					cancellation.Cancel();
					throw;
				}
			}

			static async Task ObserveCompletionAsync(Task task)
			{
				try
				{
					await task;
				}
				catch { }
			}

			async Task CopyToNetworkAsync(NetworkStream stream)
			{
				var buffer = new byte[BufferSize];
				while (!cancellation.IsCancellationRequested)
				{
					var count = await stream.ReadAsync(buffer, cancellation.Token);
					if (count == 0)
					{
						await SendCompleteAsync();
						return;
					}

					await SendAsync(buffer, count);
				}
			}

			async Task CopyFromNetworkAsync(NetworkStream stream)
			{
				while (!cancellation.IsCancellationRequested)
				{
					var (data, complete) = await ReceiveAsync();
					if (data.Length != 0)
						await stream.WriteAsync(data, cancellation.Token);

					if (complete)
					{
						SignalLocalReadCompletion();
						return;
					}
				}
			}

			Task SendCompleteAsync()
			{
				cancellation.Token.ThrowIfCancellationRequested();
				var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
				var registration = cancellation.Token.Register(() =>
					completion.TrySetCanceled(cancellation.Token));
				try
				{
					// Network.framework represents a TCP FIN as a final stream context
					// with no DispatchData.  The byte[] slice overload rejects a
					// zero-length range before the FIN reaches the native framework.
					connection.Send((DispatchData)null, NWContentContext.DefaultStream, true, error =>
					{
						registration.Dispose();
						if (error == null)
							completion.TrySetResult(true);
						else
							completion.TrySetException(new IOException(
								$"Network.framework FIN send failed: {Describe(error)}"));
					});
				}
				catch
				{
					registration.Dispose();
					throw;
				}

				return completion.Task;
			}

			Task SendAsync(byte[] buffer, int count, bool isComplete = false)
			{
				cancellation.Token.ThrowIfCancellationRequested();
				var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
				var registration = cancellation.Token.Register(() =>
					completion.TrySetCanceled(cancellation.Token));
				try
				{
					connection.Send(buffer, 0, count, NWContentContext.DefaultStream, isComplete, error =>
					{
						registration.Dispose();
						if (error == null)
							completion.TrySetResult(true);
						else
							completion.TrySetException(new IOException(
								$"Network.framework send failed: {Describe(error)}"));
					});
				}
				catch
				{
					registration.Dispose();
					throw;
				}

				return completion.Task;
			}

			void SignalLocalReadCompletion()
			{
				try
				{
					localClient?.Client.Shutdown(SocketShutdown.Send);
				}
				catch (SocketException) { }
				catch (ObjectDisposedException) { }
			}

			Task<(byte[] Data, bool Complete)> ReceiveAsync()
			{
				cancellation.Token.ThrowIfCancellationRequested();
				var completion = new TaskCompletionSource<(byte[], bool)>(
					TaskCreationOptions.RunContinuationsAsynchronously);
				var registration = cancellation.Token.Register(() =>
					completion.TrySetCanceled(cancellation.Token));
				try
				{
					connection.ReceiveData(1, BufferSize, (data, _, complete, error) =>
					{
						registration.Dispose();
						if (error != null)
						{
							completion.TrySetException(new IOException(
								$"Network.framework receive failed: {Describe(error)}"));
							return;
						}

						completion.TrySetResult((data?.ToArray() ?? Array.Empty<byte>(), complete));
					});
				}
				catch
				{
					registration.Dispose();
					throw;
				}

				return completion.Task;
			}

			public void Dispose()
			{
				if (Interlocked.Exchange(ref disposed, 1) != 0)
					return;

				cancellation.Cancel();
				try
				{
					loopbackListener?.Stop();
				}
				catch (SocketException) { }

				TcpClient client;
				lock (resourceSync)
				{
					client = localClient;
					localClient = null;
				}

				client?.Dispose();
				connection.Cancel();
				connection.Dispose();
				finished(this);
			}
		}
	}
#endif
}
