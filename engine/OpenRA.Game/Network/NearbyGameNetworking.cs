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
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace OpenRA.Network
{
	public sealed class NearbyGameInfo
	{
		public readonly string ServiceId;
		public readonly string Payload;

		public NearbyGameInfo(string serviceId, string payload)
		{
			ServiceId = serviceId;
			Payload = payload;
		}
	}

	public interface INearbyGameService : IDisposable
	{
		event Action GamesChanged;
		IReadOnlyList<NearbyGameInfo> Games { get; }
		void StartBrowsing();
		void StopBrowsing();
		void StartAdvertising(string payload, int localServerPort);
		void UpdateAdvertising(string payload);
		void StopAdvertising();
		bool TryCreateConnectionTarget(string serviceId, out ConnectionTarget target);
		void Suspend();
		void Resume();
	}

	public static class NearbyGameNetworking
	{
		const string AddressPrefix = "nearby://";
		static readonly object Sync = new();
		static readonly IReadOnlyList<NearbyGameInfo> NoGames = Array.Empty<NearbyGameInfo>();
		static INearbyGameService service;

		public static event Action GamesChanged = () => { };

		public static IReadOnlyList<NearbyGameInfo> Games
		{
			get
			{
				lock (Sync)
					return service?.Games ?? NoGames;
			}
		}

		public static string ServiceBuildIdentity
		{
			get
			{
				var current = CurrentService();
				if (current == null)
					return "none";

				var module = current.GetType().Assembly.ManifestModule;
				return module.Assembly.GetName().Name + ":" + module.ModuleVersionId.ToString("N");
			}
		}

		public static void InstallService(INearbyGameService newService)
		{
			INearbyGameService oldService;
			lock (Sync)
			{
				oldService = service;
				service = newService;
				if (service != null)
					service.GamesChanged += OnGamesChanged;
			}

			if (oldService != null)
			{
				oldService.GamesChanged -= OnGamesChanged;
				oldService.Dispose();
			}
		}

		static void OnGamesChanged()
		{
			GamesChanged();
		}

		public static void StartBrowsing()
		{
			CurrentService()?.StartBrowsing();
		}

		public static void StopBrowsing()
		{
			CurrentService()?.StopBrowsing();
		}

		public static void StartAdvertising(string payload, int localServerPort)
		{
			CurrentService()?.StartAdvertising(payload, localServerPort);
		}

		public static void UpdateAdvertising(string payload)
		{
			CurrentService()?.UpdateAdvertising(payload);
		}

		public static void StopAdvertising()
		{
			CurrentService()?.StopAdvertising();
		}

		public static void Suspend()
		{
			CurrentService()?.Suspend();
		}

		public static void Resume()
		{
			CurrentService()?.Resume();
		}

		public static void Shutdown()
		{
			InstallService(null);
		}

		public static string FormatAddress(string serviceId)
		{
			if (string.IsNullOrWhiteSpace(serviceId))
				throw new ArgumentException("A nearby service id is required.", nameof(serviceId));

			return AddressPrefix + Uri.EscapeDataString(serviceId);
		}

		public static bool TryGetServiceId(string address, out string serviceId)
		{
			serviceId = null;
			if (address == null || !address.StartsWith(AddressPrefix, StringComparison.OrdinalIgnoreCase))
				return false;

			try
			{
				serviceId = Uri.UnescapeDataString(address[AddressPrefix.Length..]);
				return !string.IsNullOrWhiteSpace(serviceId);
			}
			catch (UriFormatException)
			{
				return false;
			}
		}

		public static bool TryCreateConnectionTarget(string serviceId, out ConnectionTarget target)
		{
			target = null;
			var current = CurrentService();
			return current != null && current.TryCreateConnectionTarget(serviceId, out target);
		}

		static INearbyGameService CurrentService()
		{
			lock (Sync)
				return service;
		}
	}

	public static class NearbyLanDiscoveryProtocol
	{
		public const string BeaconType = "OpenRALANGame";
		public const int DiscoveryPort = 35891;
		const string ServiceIdPrefix = "lan.";
		static readonly UTF8Encoding StrictUtf8 = new(false, true);

		public static byte[] EncodeQuery() => EncodeString(BeaconType);

		public static byte[] EncodeResponse(ushort port, string payload)
		{
			if (port == 0)
				throw new ArgumentOutOfRangeException(nameof(port));

			ArgumentNullException.ThrowIfNull(payload);
			var prefix = EncodeString(BeaconType);
			var encodedPayload = EncodeString(payload);
			var encodedPort = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)port));
			return prefix.Concat(encodedPort).Concat(encodedPayload).ToArray();
		}

		public static bool TryDecodeResponse(
			byte[] packet,
			out ushort beaconInstancePort,
			out string payload)
		{
			beaconInstancePort = 0;
			payload = null;
			if (packet == null)
				return false;

			var typePrefix = EncodeQuery();
			if (packet.Length < typePrefix.Length + 4 ||
				!packet.Take(typePrefix.Length).SequenceEqual(typePrefix))
				return false;

			var offset = typePrefix.Length;
			var port = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packet, offset));
			offset += 2;
			var payloadLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packet, offset));
			offset += 2;
			if (port == 0 || payloadLength < 0 || packet.Length != offset + payloadLength)
				return false;

			try
			{
				payload = StrictUtf8.GetString(packet, offset, payloadLength);
				beaconInstancePort = port;
				return true;
			}
			catch (DecoderFallbackException)
			{
				return false;
			}
		}

		public static bool TryDecodeGameAdvertisement(
			byte[] packet,
			IPAddress sourceAddress,
			out IPEndPoint endpoint,
			out string payload)
		{
			endpoint = null;
			payload = null;
			if (sourceAddress == null ||
				!TryDecodeResponse(packet, out _, out payload) ||
				!TryReadAdvertisedGamePort(payload, out var gamePort))
				return false;

			endpoint = new IPEndPoint(sourceAddress, gamePort);
			return true;
		}

		public static string FormatServiceId(IPEndPoint endpoint)
		{
			ArgumentNullException.ThrowIfNull(endpoint);
			if (endpoint.Port < 1 || endpoint.Port > ushort.MaxValue)
				throw new ArgumentOutOfRangeException(nameof(endpoint));

			var bytes = Encoding.UTF8.GetBytes(endpoint.ToString());
			return ServiceIdPrefix + Convert.ToBase64String(bytes)
				.TrimEnd('=')
				.Replace('+', '-')
				.Replace('/', '_');
		}

		public static bool IsServiceId(string serviceId) =>
			serviceId != null && serviceId.StartsWith(ServiceIdPrefix, StringComparison.Ordinal);

		public static bool TryParseServiceId(string serviceId, out IPEndPoint endpoint)
		{
			endpoint = null;
			if (!IsServiceId(serviceId))
				return false;

			var encoded = serviceId[ServiceIdPrefix.Length..]
				.Replace('-', '+')
				.Replace('_', '/');
			if (encoded.Length == 0 || encoded.Length % 4 == 1)
				return false;

			encoded = encoded.PadRight(encoded.Length + (4 - encoded.Length % 4) % 4, '=');
			try
			{
				var value = StrictUtf8.GetString(Convert.FromBase64String(encoded));
				return IPEndPoint.TryParse(value, out endpoint) && endpoint.Port != 0;
			}
			catch (FormatException)
			{
				return false;
			}
			catch (DecoderFallbackException)
			{
				return false;
			}
		}

		public static IReadOnlyList<IPAddress> BuildUnicastTargets(
			IPAddress address,
			IPAddress mask,
			int maximumHostCount = 1022)
		{
			ArgumentNullException.ThrowIfNull(address);
			ArgumentNullException.ThrowIfNull(mask);
			if (maximumHostCount < 1)
				throw new ArgumentOutOfRangeException(nameof(maximumHostCount));

			var addressBytes = address.GetAddressBytes();
			var maskBytes = mask.GetAddressBytes();
			if (addressBytes.Length != 4 || maskBytes.Length != 4)
				throw new ArgumentException("LAN discovery requires IPv4 addresses and masks.");

			var addressValue = ToUInt32(addressBytes);
			var maskValue = ToUInt32(maskBytes);
			var inverseMask = ~maskValue;
			if ((inverseMask & (inverseMask + 1)) != 0)
				throw new ArgumentException("LAN discovery requires a contiguous IPv4 mask.", nameof(mask));

			var network = addressValue & maskValue;
			var broadcast = network | inverseMask;
			var hostCount = (ulong)broadcast > (ulong)network + 1 ?
				(ulong)broadcast - network - 1 : 0;
			if (hostCount > (ulong)maximumHostCount)
			{
				// Very large corporate/VPN subnets are not safe to sweep. Restrict
				// the entitlement-free fallback to the device's local /24; Bonjour
				// remains the primary discovery path outside this bounded range.
				network = addressValue & 0xFFFFFF00u;
				broadcast = network | 0xFFu;
			}

			var result = new List<IPAddress>();
			for (var candidate = (ulong)network + 1; candidate < broadcast; candidate++)
			{
				var candidateValue = (uint)candidate;
				if (candidateValue != addressValue)
					result.Add(FromUInt32(candidateValue));
			}

			return result.AsReadOnly();
		}

		static bool TryReadAdvertisedGamePort(string payload, out int port)
		{
			port = 0;
			try
			{
				var game = MiniYaml.FromString(payload, "NearbyLanAdvertisement")
					.FirstOrDefault(node => node.Key == "Game");
				var address = game?.Value.NodeWithKeyOrDefault("Address")?.Value.Value;
				if (string.IsNullOrWhiteSpace(address))
					return false;

				var separator = address.LastIndexOf(':');
				return separator >= 0 && separator != address.Length - 1 &&
					int.TryParse(address[(separator + 1)..], out port) &&
					port >= 1 && port <= ushort.MaxValue;
			}
			catch
			{
				return false;
			}
		}

		static byte[] EncodeString(string value)
		{
			var bytes = Encoding.UTF8.GetBytes(value);
			if (bytes.Length > short.MaxValue)
				throw new ArgumentException("LAN discovery values must fit in a signed 16-bit length.", nameof(value));

			var length = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)bytes.Length));
			return length.Concat(bytes).ToArray();
		}

		static uint ToUInt32(byte[] bytes) =>
			((uint)bytes[0] << 24) |
			((uint)bytes[1] << 16) |
			((uint)bytes[2] << 8) |
			bytes[3];

		static IPAddress FromUInt32(uint value) => new(new[]
		{
			(byte)(value >> 24),
			(byte)(value >> 16),
			(byte)(value >> 8),
			(byte)value
		});
	}

	public static class NearbyGamePayloadCodec
	{
		const int FormatVersion = 1;
		const int DefaultChunkLength = 180;
		const int MaximumChunks = 64;
		const int MaximumDecodedBytes = 64 * 1024;

		public static IReadOnlyDictionary<string, string> Encode(string payload, int chunkLength = DefaultChunkLength)
		{
			if (payload == null)
				throw new ArgumentNullException(nameof(payload));

			if (chunkLength < 32 || chunkLength > 220)
				throw new ArgumentOutOfRangeException(nameof(chunkLength));

			byte[] compressed;
			using (var output = new MemoryStream())
			{
				using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, true))
				{
					var bytes = Encoding.UTF8.GetBytes(payload);
					deflate.Write(bytes, 0, bytes.Length);
				}

				compressed = output.ToArray();
			}

			var base64 = Convert.ToBase64String(compressed);
			var chunks = (base64.Length + chunkLength - 1) / chunkLength;
			if (chunks == 0 || chunks > MaximumChunks)
				throw new InvalidOperationException("Nearby game metadata is too large to advertise safely.");

			var result = new Dictionary<string, string>
			{
				{ "v", FormatVersion.ToString(CultureInfo.InvariantCulture) },
				{ "n", chunks.ToString(CultureInfo.InvariantCulture) },
				{ "h", Checksum(compressed) }
			};

			for (var i = 0; i < chunks; i++)
			{
				var offset = i * chunkLength;
				result.Add("p" + i.ToString(CultureInfo.InvariantCulture),
					base64.Substring(offset, Math.Min(chunkLength, base64.Length - offset)));
			}

			return result;
		}

		public static bool TryDecode(IReadOnlyDictionary<string, string> fields, out string payload, out string error)
		{
			payload = null;
			error = null;
			if (fields == null || !fields.TryGetValue("v", out var version) || version != FormatVersion.ToString(CultureInfo.InvariantCulture))
			{
				error = "unsupported nearby metadata version";
				return false;
			}

			if (!fields.TryGetValue("n", out var countText) ||
				!int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ||
				count < 1 || count > MaximumChunks)
			{
				error = "invalid nearby metadata chunk count";
				return false;
			}

			var encoded = new StringBuilder(count * DefaultChunkLength);
			for (var i = 0; i < count; i++)
			{
				if (!fields.TryGetValue("p" + i.ToString(CultureInfo.InvariantCulture), out var chunk))
				{
					error = "missing nearby metadata chunk";
					return false;
				}

				encoded.Append(chunk);
			}

			byte[] compressed;
			try
			{
				compressed = Convert.FromBase64String(encoded.ToString());
			}
			catch (FormatException)
			{
				error = "invalid nearby metadata encoding";
				return false;
			}

			if (!fields.TryGetValue("h", out var expectedChecksum) ||
				!string.Equals(expectedChecksum, Checksum(compressed), StringComparison.OrdinalIgnoreCase))
			{
				error = "nearby metadata checksum mismatch";
				return false;
			}

			try
			{
				using var input = new MemoryStream(compressed, false);
				using var deflate = new DeflateStream(input, CompressionMode.Decompress);
				using var output = new MemoryStream();
				var buffer = new byte[4096];
				while (true)
				{
					var read = deflate.Read(buffer, 0, buffer.Length);
					if (read == 0)
						break;

					if (output.Length + read > MaximumDecodedBytes)
					{
						error = "nearby metadata exceeds the decoded size limit";
						return false;
					}

					output.Write(buffer, 0, read);
				}

				payload = Encoding.UTF8.GetString(output.ToArray());
				return true;
			}
			catch (InvalidDataException)
			{
				error = "invalid nearby metadata compression";
				return false;
			}
		}

		static string Checksum(byte[] data)
		{
			return Convert.ToHexString(SHA256.HashData(data).Take(8).ToArray()).ToLowerInvariant();
		}
	}
}
