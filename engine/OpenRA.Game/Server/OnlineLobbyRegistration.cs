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
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenRA.Server
{
	public static class OnlineLobbyRegistrationPolicy
	{
		public const string CredentialEnvironmentVariable = "NUKEHOUR_LOBBY_REGISTRATION_CREDENTIAL";
		static readonly Regex SafeIdentifier = new("^[A-Za-z0-9._-]{1,64}$", RegexOptions.CultureInvariant);
		static readonly Regex SafeRegion = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

		public static Uri ParseBaseUri(string value)
		{
			if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
				(uri.Scheme != Uri.UriSchemeHttps && (uri.Scheme != Uri.UriSchemeHttp || !uri.IsLoopback)) ||
				!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
				throw new ArgumentException("Online Lobby must use HTTPS, except loopback development URLs.", nameof(value));

			return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(uri.AbsoluteUri + "/");
		}

		public static void Validate(ServerSettings settings, string registrationCredential)
		{
			ArgumentNullException.ThrowIfNull(settings);
			if (string.IsNullOrWhiteSpace(settings.OnlineLobbyUrl))
				return;

			ParseBaseUri(settings.OnlineLobbyUrl);
			if (!SafeIdentifier.IsMatch(settings.OnlineLobbyServerId ?? ""))
				throw new ArgumentException("OnlineLobbyServerId must use 1-64 safe identifier characters.");
			if (string.IsNullOrEmpty(registrationCredential) || registrationCredential.Length < 32)
				throw new ArgumentException(
					$"{CredentialEnvironmentVariable} must contain at least 32 characters.");
			if (!IPAddress.TryParse(settings.OnlineLobbyPublicEndpoint, out _))
				throw new ArgumentException("OnlineLobbyPublicEndpoint must be a numeric IPv4 or IPv6 address.");
			var port = settings.OnlineLobbyPublicPort == 0 ? settings.ListenPort : settings.OnlineLobbyPublicPort;
			if (port < 1 || port > IPEndPoint.MaxPort)
				throw new ArgumentException("OnlineLobbyPublicPort must be a valid TCP port.", nameof(settings));
			if (!SafeRegion.IsMatch(settings.OnlineLobbyRegion ?? "") || settings.OnlineLobbyRegion.Length > 32)
				throw new ArgumentException("OnlineLobbyRegion must be a lower-case region identifier.");
			if (settings.OnlineLobbyHeartbeatSeconds < 10 || settings.OnlineLobbyHeartbeatSeconds > 300)
				throw new ArgumentException("OnlineLobbyHeartbeatSeconds must be between 10 and 300.", nameof(settings));
		}

		public static bool ShouldPublish(DedicatedServerOperationalState state) =>
			state == DedicatedServerOperationalState.WaitingForPlayers ||
			state == DedicatedServerOperationalState.InLobby ||
			state == DedicatedServerOperationalState.InGame;

		public static string ToRoomStatus(DedicatedServerOperationalState state) => state switch
		{
			DedicatedServerOperationalState.InGame => "IN_GAME",
			DedicatedServerOperationalState.GameFinished => "FINISHED",
			DedicatedServerOperationalState.Stopping or DedicatedServerOperationalState.Stopped => "OFFLINE",
			DedicatedServerOperationalState.WaitingForPlayers or DedicatedServerOperationalState.InLobby => "WAITING",
			_ => "STARTING",
		};
	}

	public sealed class OnlineLobbyRoomSnapshot
	{
		public string ServerEndpoint { get; }
		public int ServerPort { get; }
		public string ServerName { get; }
		public string Region { get; }
		[JsonIgnore]
		public DedicatedServerOperationalState OperationalState { get; }
		public string Status => OnlineLobbyRegistrationPolicy.ToRoomStatus(OperationalState);
		public string Map { get; }
		public string Mod { get; }
		public string ModVersion { get; }
		public int Players { get; }
		public int MaxPlayers { get; }
		public bool HasPassword { get; }
		public bool Ready { get; }
		public string EngineCompatibility { get; }
		public int HandshakeSchemaVersion { get; }
		public int OrdersVersion { get; }
		public string RuntimeCapability { get; }

		public OnlineLobbyRoomSnapshot(
			string serverEndpoint, int serverPort, string serverName, string region,
			DedicatedServerOperationalState operationalState, string map, string mod,
			string modVersion, int players, int maxPlayers, bool hasPassword, bool ready,
			string engineCompatibility, int handshakeSchemaVersion, int ordersVersion,
			string runtimeCapability)
		{
			ServerEndpoint = serverEndpoint;
			ServerPort = serverPort;
			ServerName = serverName;
			Region = region;
			OperationalState = operationalState;
			Map = map;
			Mod = mod;
			ModVersion = modVersion;
			Players = players;
			MaxPlayers = maxPlayers;
			HasPassword = hasPassword;
			Ready = ready;
			EngineCompatibility = engineCompatibility;
			HandshakeSchemaVersion = handshakeSchemaVersion;
			OrdersVersion = ordersVersion;
			RuntimeCapability = runtimeCapability;
		}
	}

	public sealed class OnlineLobbyRegistrationClient : IDisposable
	{
		sealed class RegistrationRequest
		{
			public string ServerId { get; init; }
			public string ServerEndpoint { get; init; }
			public int ServerPort { get; init; }
			public string ServerName { get; init; }
			public string Region { get; init; }
			public string Status { get; init; }
			public string Map { get; init; }
			public string Mod { get; init; }
			public string ModVersion { get; init; }
			public int Players { get; init; }
			public int MaxPlayers { get; init; }
			public bool HasPassword { get; init; }
			public bool Ready { get; init; }
			public string EngineCompatibility { get; init; }
			public int HandshakeSchemaVersion { get; init; }
			public int OrdersVersion { get; init; }
			public string RuntimeCapability { get; init; }
		}

		sealed class HeartbeatRequest
		{
			public string Status { get; init; }
			public string Map { get; init; }
			public int Players { get; init; }
			public int MaxPlayers { get; init; }
			public bool HasPassword { get; init; }
			public bool Ready { get; init; }
		}

		sealed class RegistrationResponse
		{
			public string RegistrationToken { get; init; }
		}

		public sealed class LobbyRequestException : Exception
		{
			public HttpStatusCode StatusCode { get; }
			public LobbyRequestException(HttpStatusCode statusCode)
				: base($"Lobby returned HTTP {(int)statusCode}.")
			{
				StatusCode = statusCode;
			}
		}

		static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			PropertyNameCaseInsensitive = true,
		};

		readonly HttpClient httpClient;
		readonly Uri baseUri;
		readonly string registrationCredential;
		readonly string serverId;
		string registrationToken;

		public string LastErrorCode { get; private set; } = "";

		public OnlineLobbyRegistrationClient(
			HttpClient httpClient, Uri baseUri, string registrationCredential, string serverId)
		{
			this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			this.baseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
			this.registrationCredential = !string.IsNullOrEmpty(registrationCredential)
				? registrationCredential : throw new ArgumentException("Registration credential is required.", nameof(registrationCredential));
			this.serverId = !string.IsNullOrEmpty(serverId)
				? serverId : throw new ArgumentException("Server id is required.", nameof(serverId));
		}

		HttpRequestMessage Request(HttpMethod method, string relativePath, string token, object payload = null)
		{
			var request = new HttpRequestMessage(method, new Uri(baseUri, relativePath));
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			if (payload != null)
				request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
			return request;
		}

		async Task RegisterAsync(OnlineLobbyRoomSnapshot snapshot, CancellationToken cancellationToken)
		{
			var payload = new RegistrationRequest
			{
				ServerId = serverId,
				ServerEndpoint = snapshot.ServerEndpoint,
				ServerPort = snapshot.ServerPort,
				ServerName = snapshot.ServerName,
				Region = snapshot.Region,
				Status = snapshot.Status,
				Map = snapshot.Map,
				Mod = snapshot.Mod,
				ModVersion = snapshot.ModVersion,
				Players = snapshot.Players,
				MaxPlayers = snapshot.MaxPlayers,
				HasPassword = snapshot.HasPassword,
				Ready = snapshot.Ready,
				EngineCompatibility = snapshot.EngineCompatibility,
				HandshakeSchemaVersion = snapshot.HandshakeSchemaVersion,
				OrdersVersion = snapshot.OrdersVersion,
				RuntimeCapability = snapshot.RuntimeCapability,
			};
			using var request = Request(HttpMethod.Post, "v1/servers/register", registrationCredential, payload);
			using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
				throw new LobbyRequestException(response.StatusCode);
			var document = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			var parsed = JsonSerializer.Deserialize<RegistrationResponse>(document, JsonOptions);
			if (string.IsNullOrEmpty(parsed?.RegistrationToken) || parsed.RegistrationToken.Length < 32)
				throw new InvalidOperationException("Lobby registration response omitted its private session token.");
			registrationToken = parsed.RegistrationToken;
		}

		async Task HeartbeatAsync(OnlineLobbyRoomSnapshot snapshot, CancellationToken cancellationToken)
		{
			var payload = new HeartbeatRequest
			{
				Status = snapshot.Status,
				Map = snapshot.Map,
				Players = snapshot.Players,
				MaxPlayers = snapshot.MaxPlayers,
				HasPassword = snapshot.HasPassword,
				Ready = snapshot.Ready,
			};
			using var request = Request(
				HttpMethod.Post, $"v1/servers/{Uri.EscapeDataString(serverId)}/heartbeat",
				registrationToken, payload);
			using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
			if (response.StatusCode == HttpStatusCode.NotFound)
			{
				registrationToken = null;
				await RegisterAsync(snapshot, cancellationToken).ConfigureAwait(false);
				return;
			}

			if (!response.IsSuccessStatusCode)
				throw new LobbyRequestException(response.StatusCode);
		}

		public async Task UpdateAsync(OnlineLobbyRoomSnapshot snapshot, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(snapshot);
			if (!OnlineLobbyRegistrationPolicy.ShouldPublish(snapshot.OperationalState))
			{
				await UnregisterAsync(cancellationToken).ConfigureAwait(false);
				return;
			}

			if (registrationToken == null)
				await RegisterAsync(snapshot, cancellationToken).ConfigureAwait(false);
			else
				await HeartbeatAsync(snapshot, cancellationToken).ConfigureAwait(false);
			LastErrorCode = "";
		}

		public async Task<bool> TryUpdateAsync(OnlineLobbyRoomSnapshot snapshot, CancellationToken cancellationToken)
		{
			try
			{
				await UpdateAsync(snapshot, cancellationToken).ConfigureAwait(false);
				return true;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (LobbyRequestException ex)
			{
				LastErrorCode = $"LOBBY_HTTP_{(int)ex.StatusCode}";
				return false;
			}
			catch
			{
				LastErrorCode = "LOBBY_UNAVAILABLE";
				return false;
			}
		}

		public async Task UnregisterAsync(CancellationToken cancellationToken)
		{
			if (registrationToken == null)
				return;
			using var request = Request(
				HttpMethod.Post, $"v1/servers/{Uri.EscapeDataString(serverId)}/unregister", registrationToken);
			using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
			if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
				throw new LobbyRequestException(response.StatusCode);
			registrationToken = null;
		}

		public void Dispose() => httpClient.Dispose();
	}
}
