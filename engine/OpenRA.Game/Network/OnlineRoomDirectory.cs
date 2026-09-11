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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenRA.Network
{
	public enum OnlineRoomCompatibilityReason
	{
		Compatible,
		HandshakeSchemaMismatch,
		OrdersVersionMismatch,
		EngineMismatch,
		ModMismatch,
		ModVersionMismatch,
		RuntimeCapabilityMismatch,
	}

	public enum OnlineRoomDirectoryError
	{
		ServiceUnavailable,
		RoomDisappeared,
		RateLimited,
		InvalidResponse,
	}

	public sealed class OnlineRoomDirectoryException : Exception
	{
		public OnlineRoomDirectoryError Error { get; }

		public OnlineRoomDirectoryException(OnlineRoomDirectoryError error, string message, Exception inner = null)
			: base(message, inner)
		{
			Error = error;
		}
	}

	public sealed class OnlineRoomCompatibilityIdentity
	{
		public string EngineCompatibility { get; }
		public string Mod { get; }
		public string ModVersion { get; }
		public string RuntimeCapability { get; }
		public int HandshakeSchemaVersion { get; }
		public int OrdersVersion { get; }

		public OnlineRoomCompatibilityIdentity(
			string engineCompatibility, string mod, string modVersion,
			string runtimeCapability, int handshakeSchemaVersion, int ordersVersion)
		{
			EngineCompatibility = engineCompatibility;
			Mod = mod;
			ModVersion = modVersion;
			RuntimeCapability = runtimeCapability;
			HandshakeSchemaVersion = handshakeSchemaVersion;
			OrdersVersion = ordersVersion;
		}
	}

	public static class OnlineRoomCompatibility
	{
		public static string MessageKey(OnlineRoomCompatibilityReason reason) => reason switch
		{
			OnlineRoomCompatibilityReason.HandshakeSchemaMismatch => "label-online-room-error-handshake",
			OnlineRoomCompatibilityReason.OrdersVersionMismatch => "label-online-room-error-orders",
			OnlineRoomCompatibilityReason.EngineMismatch => "label-online-room-error-engine",
			OnlineRoomCompatibilityReason.ModMismatch => "label-online-room-error-mod",
			OnlineRoomCompatibilityReason.ModVersionMismatch => "label-online-room-error-version",
			OnlineRoomCompatibilityReason.RuntimeCapabilityMismatch => "label-online-room-error-resources",
			_ => null,
		};

		public static OnlineRoomCompatibilityReason Precheck(
			OnlineRoom room, OnlineRoomCompatibilityIdentity local)
		{
			ArgumentNullException.ThrowIfNull(room);
			ArgumentNullException.ThrowIfNull(local);
			if (room.HandshakeSchemaVersion != local.HandshakeSchemaVersion)
				return OnlineRoomCompatibilityReason.HandshakeSchemaMismatch;
			if (room.OrdersVersion != local.OrdersVersion)
				return OnlineRoomCompatibilityReason.OrdersVersionMismatch;
			if (!StringComparer.Ordinal.Equals(room.EngineCompatibility, local.EngineCompatibility))
				return OnlineRoomCompatibilityReason.EngineMismatch;
			if (!StringComparer.Ordinal.Equals(room.Mod, local.Mod))
				return OnlineRoomCompatibilityReason.ModMismatch;
			if (!StringComparer.Ordinal.Equals(room.ModVersion, local.ModVersion))
				return OnlineRoomCompatibilityReason.ModVersionMismatch;
			if (!StringComparer.Ordinal.Equals(room.RuntimeCapability, local.RuntimeCapability))
				return OnlineRoomCompatibilityReason.RuntimeCapabilityMismatch;
			return OnlineRoomCompatibilityReason.Compatible;
		}
	}

	public sealed class OnlineRoom
	{
		static readonly Regex RoomCodePattern = new("^[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{6}$", RegexOptions.CultureInvariant);
		static readonly Regex RegionPattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

		public string RoomId { get; set; }
		public string RoomCode { get; set; }
		public string ServerId { get; set; }
		public string ServerEndpoint { get; set; }
		public int ServerPort { get; set; }
		public string ServerName { get; set; }
		public string Region { get; set; }
		public string Status { get; set; }
		public string Map { get; set; }
		public string Mod { get; set; }
		public string ModVersion { get; set; }
		public int Players { get; set; }
		public int MaxPlayers { get; set; }
		public bool HasPassword { get; set; }
		public bool Ready { get; set; }
		public string EngineCompatibility { get; set; }
		public int HandshakeSchemaVersion { get; set; }
		public int OrdersVersion { get; set; }
		public string RuntimeCapability { get; set; }
		public string Trust { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime LastHeartbeatAt { get; set; }

		public string Endpoint => IPAddress.TryParse(ServerEndpoint, out var address) &&
			address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
			? $"[{ServerEndpoint}]:{ServerPort}" : $"{ServerEndpoint}:{ServerPort}";
		public bool IsJoinable => Ready && Status == "WAITING" && Players < MaxPlayers;
		public string DisplaySummary => $"{ServerName} · {Map} · {Players}/{MaxPlayers} · {Region} · {Status}";

		public bool TryGetConnectionTarget(out ConnectionTarget target) =>
			ConnectionTarget.TryParseGameAddress(Endpoint, out target);

		static string VisibleText(string value, string field, int maxLength)
		{
			if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength || value.Any(char.IsControl))
				throw new InvalidDataException($"Invalid Online room {field}.");
			return value;
		}

		void Validate()
		{
			if (!Guid.TryParse(RoomId, out _))
				throw new InvalidDataException("Invalid Online room id.");
			if (!RoomCodePattern.IsMatch(RoomCode ?? ""))
				throw new InvalidDataException("Invalid Online room code.");
			VisibleText(ServerId, nameof(ServerId), 64);
			if (!IPAddress.TryParse(ServerEndpoint, out _))
				throw new InvalidDataException("Invalid Online room endpoint.");
			if (ServerPort < 1 || ServerPort > IPEndPoint.MaxPort)
				throw new InvalidDataException("Invalid Online room port.");
			VisibleText(ServerName, nameof(ServerName), 64);
			if (!RegionPattern.IsMatch(Region ?? "") || Region.Length > 32)
				throw new InvalidDataException("Invalid Online room region.");
			if (Status is not ("WAITING" or "IN_GAME"))
				throw new InvalidDataException("Invalid public Online room status.");
			VisibleText(Map, nameof(Map), 128);
			VisibleText(Mod, nameof(Mod), 32);
			VisibleText(ModVersion, nameof(ModVersion), 128);
			VisibleText(EngineCompatibility, nameof(EngineCompatibility), 128);
			VisibleText(RuntimeCapability, nameof(RuntimeCapability), 128);
			if (Players < 0 || MaxPlayers < 1 || MaxPlayers > 64 || Players > MaxPlayers)
				throw new InvalidDataException("Invalid Online room player capacity.");
			if (HandshakeSchemaVersion < 1 || OrdersVersion < 1)
				throw new InvalidDataException("Invalid Online room protocol metadata.");
			if (!StringComparer.Ordinal.Equals(Trust, "official"))
				throw new InvalidDataException("Untrusted Online room source.");
			if (CreatedAt.Kind == DateTimeKind.Unspecified || LastHeartbeatAt.Kind == DateTimeKind.Unspecified)
				throw new InvalidDataException("Invalid Online room timestamps.");
		}

		public static OnlineRoom Parse(string json)
		{
			try
			{
				var room = JsonSerializer.Deserialize<OnlineRoom>(json, OnlineRoomDirectoryClient.JsonOptions);
				if (room == null)
					throw new InvalidDataException("Online room response was empty.");
				room.Validate();
				return room;
			}
			catch (JsonException ex)
			{
				throw new InvalidDataException("Online room response was malformed.", ex);
			}
		}
	}

	public sealed class OnlineRoomDirectoryClient : IDisposable
	{
		internal static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		};

		const int MaxResponseBytes = 1024 * 1024;
		static readonly Regex RoomCodePattern = new("^[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{6}$", RegexOptions.CultureInvariant);
		readonly HttpClient httpClient;
		readonly Uri baseUri;

		public OnlineRoomDirectoryClient(HttpClient httpClient, Uri baseUri)
		{
			this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			this.baseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
		}

		public static Uri ParseBaseUri(string value)
		{
			if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
				(uri.Scheme != Uri.UriSchemeHttps && (uri.Scheme != Uri.UriSchemeHttp || !uri.IsLoopback)) ||
				!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
				throw new ArgumentException("Online Lobby must use HTTPS, except loopback development URLs.", nameof(value));
			return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(uri.AbsoluteUri + "/");
		}

		public static bool TryNormalizeRoomCode(string value, out string normalized)
		{
			normalized = (value ?? "").Trim().ToUpperInvariant();
			return RoomCodePattern.IsMatch(normalized);
		}

		static OnlineRoomDirectoryError MapStatus(HttpStatusCode status) => status switch
		{
			HttpStatusCode.NotFound => OnlineRoomDirectoryError.RoomDisappeared,
			(HttpStatusCode)429 => OnlineRoomDirectoryError.RateLimited,
			_ => OnlineRoomDirectoryError.ServiceUnavailable,
		};

		async Task<string> GetAsync(string path, CancellationToken cancellationToken)
		{
			try
			{
				using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, path));
				using var response = await httpClient.SendAsync(
					request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
				if (!response.IsSuccessStatusCode)
					throw new OnlineRoomDirectoryException(
						MapStatus(response.StatusCode), $"Online Lobby returned HTTP {(int)response.StatusCode}.");
				if (response.Content.Headers.ContentLength > MaxResponseBytes)
					throw new OnlineRoomDirectoryException(
						OnlineRoomDirectoryError.InvalidResponse, "Online Lobby response exceeded its limit.");

				using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
				using var memory = new MemoryStream();
				var buffer = new byte[8192];
				while (true)
				{
					var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
					if (read == 0)
						break;
					if (memory.Length + read > MaxResponseBytes)
						throw new OnlineRoomDirectoryException(
							OnlineRoomDirectoryError.InvalidResponse, "Online Lobby response exceeded its limit.");
					memory.Write(buffer, 0, read);
				}

				return Encoding.UTF8.GetString(memory.ToArray());
			}
			catch (OnlineRoomDirectoryException)
			{
				throw;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new OnlineRoomDirectoryException(
					OnlineRoomDirectoryError.ServiceUnavailable, "Online Lobby is unavailable.", ex);
			}
		}

		public async Task<IReadOnlyList<OnlineRoom>> GetRoomsAsync(CancellationToken cancellationToken)
		{
			var json = await GetAsync("v1/rooms", cancellationToken).ConfigureAwait(false);
			try
			{
				using var document = JsonDocument.Parse(json);
				if (!document.RootElement.TryGetProperty("rooms", out var rooms) || rooms.ValueKind != JsonValueKind.Array)
					throw new InvalidDataException("Online Lobby response omitted rooms.");
				return rooms.EnumerateArray().Select(room => OnlineRoom.Parse(room.GetRawText())).ToArray();
			}
			catch (Exception ex) when (ex is JsonException || ex is InvalidDataException)
			{
				throw new OnlineRoomDirectoryException(
					OnlineRoomDirectoryError.InvalidResponse, "Online Lobby returned invalid room metadata.", ex);
			}
		}

		public async Task<OnlineRoom> ResolveCodeAsync(string roomCode, CancellationToken cancellationToken)
		{
			if (!TryNormalizeRoomCode(roomCode, out var normalized))
				throw new ArgumentException("Room code must contain six unambiguous characters.", nameof(roomCode));
			var json = await GetAsync($"v1/rooms/code/{normalized}", cancellationToken).ConfigureAwait(false);
			try
			{
				return OnlineRoom.Parse(json);
			}
			catch (InvalidDataException ex)
			{
				throw new OnlineRoomDirectoryException(
					OnlineRoomDirectoryError.InvalidResponse, "Online Lobby returned invalid room metadata.", ex);
			}
		}

		public void Dispose() => httpClient.Dispose();
	}
}
