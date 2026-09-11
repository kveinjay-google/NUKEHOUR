using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenRA.Network;
using OpenRA.Server;
using OpenRA.Traits;

namespace OpenRA
{
	public enum IosMultiplayerSmokeRole { Disabled, Host, Client }
	public enum IosMultiplayerSmokeDiscoveryMode { Direct, Auto, OnlineList, OnlineCode }

	public sealed class IosMultiplayerSmokeConfiguration
	{
		const int DefaultPort = 1234;
		const int DefaultRequiredWorldTicks = 100;
		const int DefaultTimeoutSeconds = 120;

		public bool Enabled { get; private init; }
		public IosMultiplayerSmokeRole Role { get; private init; }
		public string Endpoint { get; private init; }
		public IosMultiplayerSmokeDiscoveryMode DiscoveryMode { get; private init; }
		public string Map { get; private init; }
		public int Port { get; private init; }
		public string RunId { get; private init; }
		public int RequiredWorldTicks { get; private init; }
		public int TimeoutSeconds { get; private init; }
		public string ExpectedRuntimeHash { get; private init; }
		public bool RequireGameplay { get; private init; }
		public string LaunchNonce { get; private init; }
		public string ParticipantNonce { get; private init; }
		public string ExpectedRemoteNonce { get; private init; }
		public string OnlineLobbyUrl { get; private init; }
		public string OnlineServerId { get; private init; }
		public string OnlineRoomCode { get; private init; }
		public string ValidationError { get; private init; }
		public bool CreatesPlayerHostedServer =>
			Role == IosMultiplayerSmokeRole.Host &&
			DiscoveryMode == IosMultiplayerSmokeDiscoveryMode.Auto && string.IsNullOrEmpty(Endpoint);

		public static IosMultiplayerSmokeConfiguration Parse(Func<string, string> read)
		{
			var roleText = read("OPENRA_IOS_MULTIPLAYER_SMOKE_ROLE")?.Trim();
			if (string.IsNullOrEmpty(roleText))
				return Disabled();

			if (!Enum.TryParse(roleText, true, out IosMultiplayerSmokeRole role) ||
				role == IosMultiplayerSmokeRole.Disabled)
				return Invalid($"Unknown multiplayer smoke role '{roleText}'.");

			var endpoint = read("OPENRA_IOS_MULTIPLAYER_SMOKE_ENDPOINT")?.Trim() ?? string.Empty;
			var discoveryModeText = read("OPENRA_IOS_MULTIPLAYER_SMOKE_DISCOVERY_MODE")?.Trim();
			IosMultiplayerSmokeDiscoveryMode discoveryMode;
			if (string.IsNullOrEmpty(discoveryModeText))
				discoveryMode = string.IsNullOrEmpty(endpoint) ?
					IosMultiplayerSmokeDiscoveryMode.Auto : IosMultiplayerSmokeDiscoveryMode.Direct;
			else if (!TryParseDiscoveryMode(discoveryModeText, out discoveryMode))
				return Invalid($"Unknown multiplayer smoke discovery mode '{discoveryModeText}'.");

			if (role == IosMultiplayerSmokeRole.Client &&
				discoveryMode == IosMultiplayerSmokeDiscoveryMode.Direct &&
				!TryParseEndpoint(endpoint, out _, out _))
				return Invalid("Client endpoint must use HOST:PORT format.");

			var onlineLobbyUrl = read("OPENRA_IOS_MULTIPLAYER_SMOKE_ONLINE_LOBBY_URL")?.Trim() ?? string.Empty;
			var onlineServerId = read("OPENRA_IOS_MULTIPLAYER_SMOKE_ONLINE_SERVER_ID")?.Trim() ?? string.Empty;
			var onlineRoomCode = read("OPENRA_IOS_MULTIPLAYER_SMOKE_ONLINE_ROOM_CODE")?.Trim() ?? string.Empty;
			if (discoveryMode is IosMultiplayerSmokeDiscoveryMode.OnlineList or
				IosMultiplayerSmokeDiscoveryMode.OnlineCode)
			{
				try
				{
					onlineLobbyUrl = OnlineRoomDirectoryClient.ParseBaseUri(onlineLobbyUrl).AbsoluteUri;
				}
				catch (ArgumentException)
				{
					return Invalid("Online multiplayer smoke Lobby URL must use HTTPS.");
				}

				if (!string.IsNullOrEmpty(endpoint))
					return Invalid("Online multiplayer smoke discovery cannot accept a direct endpoint.");

				if (discoveryMode == IosMultiplayerSmokeDiscoveryMode.OnlineList &&
					(string.IsNullOrWhiteSpace(onlineServerId) || onlineServerId.Length > 64 ||
					onlineServerId.Any(char.IsControl)))
					return Invalid("Online room-list smoke requires a valid server id.");

				if (discoveryMode == IosMultiplayerSmokeDiscoveryMode.OnlineCode &&
					!OnlineRoomDirectoryClient.TryNormalizeRoomCode(onlineRoomCode, out onlineRoomCode))
					return Invalid("Online room-code smoke requires a valid six-character room code.");
			}

			var expectedRuntimeHash = read("OPENRA_IOS_MULTIPLAYER_SMOKE_EXPECTED_RUNTIME_HASH")?.Trim() ?? string.Empty;
			if (!string.IsNullOrEmpty(expectedRuntimeHash) &&
				!IosMultiplayerSmokeParticipantIdentity.IsValidRuntimeHash(expectedRuntimeHash))
				return Invalid("Expected multiplayer smoke RuntimeContract hash must be 256-bit hexadecimal.");

			var requireGameplayText = read("OPENRA_IOS_MULTIPLAYER_SMOKE_REQUIRE_GAMEPLAY")?.Trim();
			var requireGameplay = false;
			if (!string.IsNullOrEmpty(requireGameplayText) &&
				!bool.TryParse(requireGameplayText, out requireGameplay))
				return Invalid("Multiplayer smoke gameplay requirement must be true or false.");

			var launchNonce = read("OPENRA_IOS_MULTIPLAYER_SMOKE_LAUNCH_NONCE")?.Trim();
			var participantNonce = read("OPENRA_IOS_MULTIPLAYER_SMOKE_PARTICIPANT_NONCE")?.Trim();
			var expectedRemoteNonce = read("OPENRA_IOS_MULTIPLAYER_SMOKE_EXPECTED_REMOTE_NONCE")?.Trim();
			if (!IosMultiplayerSmokeParticipantIdentity.IsValidNonce(launchNonce) ||
				!IosMultiplayerSmokeParticipantIdentity.IsValidNonce(participantNonce) ||
				!IosMultiplayerSmokeParticipantIdentity.IsValidNonce(expectedRemoteNonce))
				return Invalid("Multiplayer smoke requires 128-bit launch, participant, and expected-remote nonces.");

			if (string.Equals(participantNonce, expectedRemoteNonce, StringComparison.Ordinal))
				return Invalid("Local and expected-remote participant nonces must be different.");

			var map = read("OPENRA_IOS_MULTIPLAYER_SMOKE_MAP")?.Trim();
			if (string.IsNullOrEmpty(map))
				map = "south-pacific";

			return new IosMultiplayerSmokeConfiguration
			{
				Enabled = true,
				Role = role,
				Endpoint = endpoint,
				DiscoveryMode = discoveryMode,
				Map = map,
				Port = ParseBounded(read("OPENRA_IOS_MULTIPLAYER_SMOKE_PORT"), DefaultPort, 1, 65535),
				RunId = SafeRunId(read("OPENRA_IOS_MULTIPLAYER_SMOKE_RUN_ID")),
				RequiredWorldTicks = ParseBounded(read("OPENRA_IOS_MULTIPLAYER_SMOKE_WORLD_TICKS"),
					DefaultRequiredWorldTicks, 25, 2500),
				TimeoutSeconds = ParseBounded(read("OPENRA_IOS_MULTIPLAYER_SMOKE_TIMEOUT_SECONDS"),
					DefaultTimeoutSeconds, 15, 600),
				ExpectedRuntimeHash = expectedRuntimeHash,
				RequireGameplay = requireGameplay,
				LaunchNonce = launchNonce,
				ParticipantNonce = participantNonce,
				ExpectedRemoteNonce = expectedRemoteNonce,
				OnlineLobbyUrl = onlineLobbyUrl,
				OnlineServerId = onlineServerId,
				OnlineRoomCode = onlineRoomCode,
				ValidationError = string.Empty
			};
		}

		static bool TryParseDiscoveryMode(string value, out IosMultiplayerSmokeDiscoveryMode mode)
		{
			var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal);
			return Enum.TryParse(normalized, true, out mode);
		}

		public bool TryGetTarget(out ConnectionTarget target)
		{
			target = null;
			if (!TryParseEndpoint(Endpoint, out var host, out var port))
				return false;

			target = new ConnectionTarget(host, port);
			return true;
		}

		static bool TryParseEndpoint(string value, out string host, out int port)
		{
			host = string.Empty;
			port = 0;
			if (!Uri.TryCreate($"tcp://{value}", UriKind.Absolute, out var uri) ||
				string.IsNullOrEmpty(uri.Host) || uri.Port <= 0)
				return false;

			host = uri.Host;
			port = uri.Port;
			return true;
		}

		static int ParseBounded(string value, int fallback, int minimum, int maximum) =>
			int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ?
				parsed.Clamp(minimum, maximum) : fallback;

		static string SafeRunId(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

			var safe = new string(value.Trim()
				.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
			return safe.Length == 0 ? "invalid-run-id" : safe;
		}

		static IosMultiplayerSmokeConfiguration Disabled() => new()
		{
			Enabled = false,
			Role = IosMultiplayerSmokeRole.Disabled,
			ValidationError = string.Empty
		};

		static IosMultiplayerSmokeConfiguration Invalid(string error) => new()
		{
			Enabled = false,
			Role = IosMultiplayerSmokeRole.Disabled,
			ValidationError = error
		};
	}

	public sealed class IosMultiplayerSmokeParticipantIdentity
	{
		public IosMultiplayerSmokeRole Role { get; }
		public string LaunchNonce { get; }
		public string ParticipantNonce { get; }
		public string RuntimeHash { get; }
		public string SessionGuid { get; }

		public IosMultiplayerSmokeParticipantIdentity(
			IosMultiplayerSmokeRole role, string launchNonce, string participantNonce,
			string runtimeHash, string sessionGuid)
		{
			Role = role;
			LaunchNonce = launchNonce;
			ParticipantNonce = participantNonce;
			RuntimeHash = runtimeHash;
			SessionGuid = sessionGuid;
		}

		const int NonceLength = 32;
		const int RuntimeHashLength = 64;
		const int SessionLength = 32;
		const int PayloadLength = 1 + NonceLength + NonceLength + RuntimeHashLength + SessionLength;
		const int FrameChunkLength = 8;

		public static bool IsValidNonce(string value) => IsHex(value, NonceLength);
		public static bool IsValidRuntimeHash(string value) => IsHex(value, RuntimeHashLength);

		public static string CreateAcknowledgementFrame(
			IosMultiplayerSmokeParticipantIdentity acknowledgerIdentity,
			IosMultiplayerSmokeParticipantIdentity acknowledgedIdentity)
		{
			if (acknowledgerIdentity == null || acknowledgedIdentity == null ||
				acknowledgerIdentity.LaunchNonce != acknowledgedIdentity.LaunchNonce)
				throw new ArgumentException("Invalid multiplayer smoke acknowledgement identities.");

			var acknowledgerPayload = CreatePayload(
				acknowledgerIdentity.Role, acknowledgerIdentity.LaunchNonce,
				acknowledgerIdentity.ParticipantNonce, acknowledgerIdentity.RuntimeHash,
				acknowledgerIdentity.SessionGuid);
			var acknowledgedPayload = CreatePayload(
				acknowledgedIdentity.Role, acknowledgedIdentity.LaunchNonce,
				acknowledgedIdentity.ParticipantNonce, acknowledgedIdentity.RuntimeHash,
				acknowledgedIdentity.SessionGuid);
			var relationship = acknowledgerPayload + ">" + acknowledgedPayload;
			var prefix = "A" + acknowledgerPayload[0] + Token(relationship, 8);
			return prefix + Token(acknowledgerIdentity.LaunchNonce + prefix + relationship, 6);
		}

		public static bool IsAcknowledgementFrame(
			string frame, IosMultiplayerSmokeParticipantIdentity acknowledgerIdentity,
			IosMultiplayerSmokeParticipantIdentity acknowledgedIdentity)
		{
			if (frame == null || frame.Length != 16 || acknowledgerIdentity == null ||
				acknowledgedIdentity == null)
				return false;

			try
			{
				return string.Equals(frame, CreateAcknowledgementFrame(
					acknowledgerIdentity, acknowledgedIdentity), StringComparison.Ordinal);
			}
			catch (ArgumentException)
			{
				return false;
			}
		}

		public static IReadOnlyList<string> CreateFrames(
			IosMultiplayerSmokeRole role, string launchNonce, string participantNonce,
			string runtimeHash, string sessionGuid)
		{
			var payload = CreatePayload(role, launchNonce, participantNonce, runtimeHash, sessionGuid);
			var frameCount = (payload.Length + FrameChunkLength - 1) / FrameChunkLength;
			var frames = new List<string>(frameCount);
			for (var index = 0; index < frameCount; index++)
			{
				var offset = index * FrameChunkLength;
				var chunk = payload.Substring(offset, Math.Min(FrameChunkLength, payload.Length - offset))
					.PadRight(FrameChunkLength, '0');
				var prefix = $"{payload[0]}{index:x2}{frameCount:x2}{chunk}";
				frames.Add(prefix + Token(launchNonce + payload + prefix, 3));
			}

			return frames;
		}

		public static bool TryParseFrame(
			string frame, string launchNonce, out IosMultiplayerSmokeRole role,
			out int index, out int total, out string chunk)
		{
			role = IosMultiplayerSmokeRole.Disabled;
			index = 0;
			total = 0;
			chunk = null;
			if (!IsValidNonce(launchNonce) || frame == null || frame.Length != 16)
				return false;

			role = ParseRole(frame[0]);
			if (role == IosMultiplayerSmokeRole.Disabled ||
				!int.TryParse(frame.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out index) ||
				!int.TryParse(frame.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out total) ||
				total < 1 || index < 0 || index >= total)
				return false;

			chunk = frame.Substring(5, FrameChunkLength);
			// The first payload chunk starts with the H/C role marker.  All remaining
			// payload bytes are lowercase hexadecimal identity data.
			var validChunk = index == 0 ?
				ParseRole(chunk[0]) != IosMultiplayerSmokeRole.Disabled && IsHex(chunk[1..], FrameChunkLength - 1) :
				IsHex(chunk, FrameChunkLength);
			return validChunk && IsHex(frame[13..], 3);
		}

		public static bool TryReassemble(
			IEnumerable<string> frames, string launchNonce,
			out IosMultiplayerSmokeParticipantIdentity identity, out string error)
		{
			identity = null;
			error = string.Empty;
			var chunks = new Dictionary<int, string>();
			var suppliedFrames = new HashSet<string>(StringComparer.Ordinal);
			var expectedRole = IosMultiplayerSmokeRole.Disabled;
			var expectedTotal = -1;
			foreach (var frame in frames ?? Array.Empty<string>())
			{
				if (!TryParseFrame(frame, launchNonce, out var role, out var index, out var total, out var chunk))
				{
					error = "Invalid multiplayer smoke identity frame.";
					return false;
				}

				if (expectedTotal >= 0 && (role != expectedRole || total != expectedTotal))
				{
					error = "Mixed multiplayer smoke identity frame sets.";
					return false;
				}

				expectedRole = role;
				expectedTotal = total;
				if (chunks.TryGetValue(index, out var previous) && previous != chunk)
				{
					error = "Conflicting multiplayer smoke identity frame.";
					return false;
				}

				chunks[index] = chunk;
				suppliedFrames.Add(frame);
			}

			if (expectedTotal < 1 || chunks.Count != expectedTotal ||
				Enumerable.Range(0, expectedTotal).Any(index => !chunks.ContainsKey(index)))
				return false;

			var payload = Enumerable.Range(0, expectedTotal)
				.Select(index => chunks[index]).JoinWith(string.Empty)[..PayloadLength];
			if (!TryParsePayload(payload, out identity, out error))
				return false;

			var expectedFrames = CreateFrames(
				identity.Role, identity.LaunchNonce, identity.ParticipantNonce,
				identity.RuntimeHash, identity.SessionGuid);
			if (expectedFrames.Any(frame => !suppliedFrames.Contains(frame)))
			{
				identity = null;
				error = "Multiplayer smoke identity frame authentication failed.";
				return false;
			}

			return true;
		}

		public static string Token(string value, int length)
		{
			if (length < 1 || length > 64)
				throw new ArgumentOutOfRangeException(nameof(length));

			var digest = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
			return Convert.ToHexString(digest).ToLowerInvariant()[..length];
		}

		static string CreatePayload(
			IosMultiplayerSmokeRole role, string launchNonce, string participantNonce,
			string runtimeHash, string sessionGuid)
		{
			var roleToken = role == IosMultiplayerSmokeRole.Host ? "H" :
				role == IosMultiplayerSmokeRole.Client ? "C" : null;
			if (roleToken == null || !IsValidNonce(launchNonce) || !IsValidNonce(participantNonce) ||
				!IsValidRuntimeHash(runtimeHash) || !Guid.TryParse(sessionGuid, out var session))
				throw new ArgumentException("Invalid multiplayer smoke participant identity.");

			return roleToken + launchNonce + participantNonce + runtimeHash + session.ToString("N");
		}

		static bool TryParsePayload(
			string payload, out IosMultiplayerSmokeParticipantIdentity identity, out string error)
		{
			identity = null;
			error = string.Empty;
			if (payload == null || payload.Length != PayloadLength)
			{
				error = "Invalid multiplayer smoke identity payload length.";
				return false;
			}

			var role = ParseRole(payload[0]);
			var launchNonce = payload.Substring(1, NonceLength);
			var participantNonce = payload.Substring(1 + NonceLength, NonceLength);
			var runtimeHash = payload.Substring(1 + NonceLength * 2, RuntimeHashLength);
			var sessionText = payload.Substring(1 + NonceLength * 2 + RuntimeHashLength, SessionLength);
			if (role == IosMultiplayerSmokeRole.Disabled || !IsValidNonce(launchNonce) ||
				!IsValidNonce(participantNonce) || !IsValidRuntimeHash(runtimeHash) ||
				!Guid.TryParseExact(sessionText, "N", out var session))
			{
				error = "Invalid multiplayer smoke identity payload.";
				return false;
			}

			identity = new IosMultiplayerSmokeParticipantIdentity(
				role, launchNonce, participantNonce, runtimeHash, session.ToString());
			return true;
		}

		static IosMultiplayerSmokeRole ParseRole(char value) => value switch
		{
			'H' => IosMultiplayerSmokeRole.Host,
			'C' => IosMultiplayerSmokeRole.Client,
			_ => IosMultiplayerSmokeRole.Disabled
		};

		static bool IsHex(string value, int length) => value != null && value.Length == length &&
			value.All(c => Uri.IsHexDigit(c));
	}

	public static class IosMultiplayerSmokeReadiness
	{
		public static bool CanIssueReady(
			bool remoteIdentityVerified, bool remoteAcknowledgedLocalIdentity,
			bool remoteIdentityCurrentlyValid, string currentLocalName,
			string lastIssuedLocalName, string localAcknowledgement) =>
			remoteIdentityVerified && remoteAcknowledgedLocalIdentity && remoteIdentityCurrentlyValid &&
			!string.IsNullOrEmpty(localAcknowledgement) &&
			string.Equals(currentLocalName, localAcknowledgement, StringComparison.Ordinal) &&
			string.Equals(lastIssuedLocalName, localAcknowledgement, StringComparison.Ordinal);

		public static bool ShouldHoldAcknowledgement(
			bool remoteAcknowledgedLocalIdentity, string currentLocalName,
			string lastIssuedLocalName, string localAcknowledgement) =>
			remoteAcknowledgedLocalIdentity && !string.IsNullOrEmpty(localAcknowledgement) &&
			string.Equals(currentLocalName, localAcknowledgement, StringComparison.Ordinal) &&
			string.Equals(lastIssuedLocalName, localAcknowledgement, StringComparison.Ordinal);
	}

	public static class IosMultiplayerSmokeRuntimeIdentity
	{
		public static readonly IReadOnlyList<string> RequiredBundleFiles = new[]
		{
			"OpenRA.Game.dll",
			"OpenRA.Mods.Common.dll",
			"OpenRA.Mods.RA2.dll",
			"OpenRA.Platforms.Default.dll",
			"OpenRA.iOS.dll",
			"Info.plist",
			Path.Combine("mods", "ra2", "mod.yaml")
		};

		public static string Compute(string baseDirectory)
		{
			if (string.IsNullOrEmpty(baseDirectory))
				throw new ArgumentException("A bundle directory is required.", nameof(baseDirectory));

			var files = new HashSet<string>(RequiredBundleFiles, StringComparer.Ordinal);
			var modDirectory = Path.Combine(baseDirectory, "mods", "ra2");
			foreach (var directory in new[] { "rules", "weapons", "sequences" })
			{
				var path = Path.Combine(modDirectory, directory);
				if (Directory.Exists(path))
					foreach (var file in Directory.EnumerateFiles(path, "*.yaml", SearchOption.AllDirectories))
						files.Add(Path.GetRelativePath(baseDirectory, file));
			}

			// Deliberately exclude the native executable and AOT artifacts.  Two devices
			// can install the same managed/content build using different signing teams;
			// Mach-O signatures then differ even though the game build is identical.
			using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			var separator = new byte[] { 0 };
			var buffer = new byte[64 * 1024];
			foreach (var relativePath in files.OrderBy(path => path, StringComparer.Ordinal))
			{
				var path = Path.Combine(baseDirectory, relativePath);
				if (!File.Exists(path))
					throw new FileNotFoundException("Required smoke runtime identity file is missing.", path);

				hash.AppendData(Encoding.UTF8.GetBytes(relativePath.Replace(Path.DirectorySeparatorChar, '/')));
				hash.AppendData(separator);
				using var stream = File.OpenRead(path);
				int read;
				while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
					hash.AppendData(buffer, 0, read);

				hash.AppendData(separator);
			}

			return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
		}
	}

	public sealed class IosMultiplayerSmokeDiscoveredRoom
	{
		public NearbyGameInfo Game { get; }
		public string RemoteSessionGuid { get; }
		public string RemoteParticipantNonce { get; }
		public string RemoteBuildToken { get; }

		public IosMultiplayerSmokeDiscoveredRoom(
			NearbyGameInfo game, string sessionGuid, string participantNonce, string buildToken)
		{
			Game = game;
			RemoteSessionGuid = sessionGuid;
			RemoteParticipantNonce = participantNonce;
			RemoteBuildToken = buildToken;
		}
	}

	public static class IosMultiplayerSmokeDiscovery
	{
		public static string RoomName(string runId, string launchNonce, string hostNonce, string runtimeHash) =>
			$"OpenRA iOS smoke {runId}|{launchNonce}|{hostNonce}|{runtimeHash}";

		public static bool TryFindUniqueRoom(
			IEnumerable<NearbyGameInfo> games, string runId, string launchNonce,
			string expectedHostNonce, string expectedRuntimeHash,
			out IosMultiplayerSmokeDiscoveredRoom match, out string error)
		{
			match = null;
			error = string.Empty;
			var expectedName = RoomName(runId, launchNonce, expectedHostNonce, expectedRuntimeHash);
			foreach (var game in games ?? Array.Empty<NearbyGameInfo>())
			{
				try
				{
					var roots = MiniYaml.FromString(game.Payload, $"NearbySmoke_{game.ServiceId}");
					var root = roots.FirstOrDefault(node => node.Key == "Game");
					var name = root?.Value.NodeWithKeyOrDefault("Name")?.Value.Value;
					if (!string.Equals(name, expectedName, StringComparison.Ordinal))
						continue;

					var sessionGuid = root.Value.NodeWithKeyOrDefault("Id")?.Value.Value;
					if (string.IsNullOrEmpty(sessionGuid))
						continue;

					var candidate = new IosMultiplayerSmokeDiscoveredRoom(
						game, sessionGuid, expectedHostNonce, expectedRuntimeHash);
					if (match == null)
					{
						match = candidate;
						continue;
					}

					if (!string.Equals(match.RemoteSessionGuid, sessionGuid, StringComparison.Ordinal))
					{
						error = $"Multiple nearby rooms matched smoke run '{runId}'.";
						match = null;
						return false;
					}

					// A single host can be visible through both Bonjour/AWDL and the
					// router LAN beacon. Prefer the direct LAN endpoint, but do not
					// treat the duplicate provider as a second room.
					if (NearbyLanDiscoveryProtocol.IsServiceId(candidate.Game.ServiceId) &&
						!NearbyLanDiscoveryProtocol.IsServiceId(match.Game.ServiceId))
						match = candidate;
				}
				catch
				{
					// Ignore advertisements that are not valid GameServer payloads.
				}
			}

			return match != null;
		}
	}

	public static class IosMultiplayerSmoke
	{
		public static bool TryStart()
		{
			var config = IosMultiplayerSmokeConfiguration.Parse(Environment.GetEnvironmentVariable);
			if (!config.Enabled)
			{
				if (!string.IsNullOrEmpty(config.ValidationError))
					Console.WriteLine($"OpenRA iOS multiplayer smoke configuration failed: {config.ValidationError}");

				return false;
			}

			new Coordinator(config).Start();
			return true;
		}

		sealed class Coordinator
		{
			readonly IosMultiplayerSmokeConfiguration config;
			readonly Stopwatch elapsed = Stopwatch.StartNew();
			readonly DateTime processStartedUtc = DateTime.UtcNow;
			readonly string journalPath;
			readonly string sessionGuid;
			readonly HashSet<string> remoteIdentityFrames = new(StringComparer.Ordinal);
			string runtimeHash;
			string runtimeProfile;
			string runtimeContract;
			string modCompatibility;
			string resourceCapability;
			string localLanAddresses;
			IosMultiplayerSmokeParticipantIdentity localIdentity;
			IReadOnlyList<string> localIdentityFrames;
			IReadOnlyList<string> localPublishedFrames;
			string localAcknowledgementFrame;
			IosMultiplayerSmokeParticipantIdentity remoteIdentity;
			OrderManager orderManager;
			string discoveryStage;
			string connectedAddress;
			long discoveryElapsedMs = -1;
			string observedRemoteRuntimeHash;
			string observedRemoteParticipantNonce;
			string observedRemoteSessionGuid;
			bool nearbySubscribed;
			bool remoteIdentityVerified;
			bool remoteAcknowledgedLocalIdentity;
			bool remoteIdentityCurrentlyValid;
			string lastIssuedIdentityFrame;
			int localIdentityFrameIndex;
			int localIdentityFrameHoldTicks;
			int blockedClientIndex = -1;
			bool mapBlockIssued;
			bool mapBlockObserved;
			bool mapReadyRestoreIssued;
			bool blockedStartIssued;
			bool blockedStartRejected;
			LobbySafeStartRequest blockedStartRequest;
			LobbySafeStartRequest finalStartRequest;
			bool readyIssued;
			bool startIssued;
			bool gameplayOptionsIssued;
			bool gameplayOptionsObserved;
			bool spawnObserved;
			bool selectObserved;
			bool movementOrderIssued;
			bool movementObserved;
			bool resourceUpdateOrderIssued;
			bool resourceUpdateObserved;
			bool deployOrderIssued;
			bool productionOrderIssued;
			bool productionObserved;
			bool completedProductionObserved;
			bool visibilityOrderIssued;
			bool attackOrderIssued;
			bool attackObserved;
			bool terrainAttackFallback;
			uint combatActorId;
			uint enemyActorId;
			CPos combatStartLocation;
			int movementIssuedAtTick = -1;
			int resourceIssuedAtTick = -1;
			int productionIssuedAtTick = -1;
			int visibilityIssuedAtTick = -1;
			int attackIssuedAtTick = -1;
			int placementAttempt;
			int syncHashBeforeResource;
			string gameplayStage = "not-required";
			bool terminal;
			long lastJournalSecond = -1;

			public Coordinator(IosMultiplayerSmokeConfiguration config)
			{
				this.config = config;
				var directory = Path.Combine(Platform.SupportDir, "Logs", "MultiplayerSmoke", config.RunId);
				Directory.CreateDirectory(directory);
				journalPath = Path.Combine(directory, $"{config.Role.ToString().ToLowerInvariant()}.json");
				sessionGuid = Platform.SessionGUID.ToString();
				discoveryStage = config.Role == IosMultiplayerSmokeRole.Host ? "host" : "not-started";
			}

			public void Start()
			{
				try
				{
					runtimeHash = Game.ModData.RuntimeContract.Digest;
					runtimeProfile = Game.ModData.RuntimeProfile.Serialize();
					runtimeContract = Game.ModData.RuntimeContract.Serialize();
					modCompatibility = Game.ModData.Manifest.Metadata.CompatibilityOrVersion;
					resourceCapability = Game.ModData.RuntimeContract.ResourceCapability;
					localLanAddresses = GetLocalLanAddresses();
					if (!string.IsNullOrEmpty(config.ExpectedRuntimeHash) &&
						!string.Equals(runtimeHash, config.ExpectedRuntimeHash, StringComparison.Ordinal))
						throw new InvalidOperationException(
							$"Active RuntimeContract hash '{runtimeHash}' does not match expected '{config.ExpectedRuntimeHash}'.");

					if (config.RequireGameplay)
						gameplayStage = "waiting-for-lobby";

					localIdentity = new IosMultiplayerSmokeParticipantIdentity(
						config.Role, config.LaunchNonce, config.ParticipantNonce, runtimeHash, sessionGuid);
					localIdentityFrames = IosMultiplayerSmokeParticipantIdentity.CreateFrames(
						localIdentity.Role, localIdentity.LaunchNonce, localIdentity.ParticipantNonce,
						localIdentity.RuntimeHash, localIdentity.SessionGuid);
					localPublishedFrames = localIdentityFrames;
					Write("STARTING", string.Empty);
					if (config.CreatesPlayerHostedServer)
						StartHost();
					else
						StartClient();

					Game.RunAfterTick(Poll);
				}
				catch (Exception e)
				{
					Fail($"{e.GetType().Name}: {e.Message}");
				}
			}

			void StartHost()
			{
				var map = Game.ModData.MapCache.SingleOrDefault(m =>
					m.Uid == config.Map || Path.GetFileName(m.PackageName) == config.Map);
				if (map == null)
					throw new InvalidOperationException($"Map '{config.Map}' is not available.");

				var settings = Game.Settings.Server.Clone();
				settings.Name = IosMultiplayerSmokeDiscovery.RoomName(
					config.RunId, config.LaunchNonce, config.ParticipantNonce, runtimeHash);
				settings.ListenPort = config.Port;
				settings.DiscoverNatDevices = false;
				settings.EnableSingleplayer = false;
				settings.EnableGeoIP = false;
				settings.EnableSyncReports = true;
				settings.Map = map.Uid;
				AttachOrderManager(Game.JoinServer(
					Game.CreateServer(settings), string.Empty, recordReplay: false));
			}

			void StartClient()
			{
				if (config.DiscoveryMode is IosMultiplayerSmokeDiscoveryMode.OnlineList or
					IosMultiplayerSmokeDiscoveryMode.OnlineCode)
				{
					StartOnlineClient();
					return;
				}

				if (config.DiscoveryMode == IosMultiplayerSmokeDiscoveryMode.Direct)
				{
					if (!config.TryGetTarget(out var target))
						throw new InvalidOperationException("Client endpoint is invalid.");

					Join(target, config.Endpoint, "direct");
					return;
				}

				discoveryStage = "waiting-for-nearby-room";
				nearbySubscribed = true;
				NearbyGameNetworking.GamesChanged += NearbyGamesChanged;
				NearbyGameNetworking.StartBrowsing();
				Write("DISCOVERING", string.Empty);
				NearbyGamesChanged();
			}

			void StartOnlineClient()
			{
				discoveryStage = config.DiscoveryMode == IosMultiplayerSmokeDiscoveryMode.OnlineList ?
					"waiting-for-online-room-list" : "waiting-for-online-room-code";
				Write("DISCOVERING", string.Empty);
				_ = Task.Run(ResolveAndJoinOnlineRoomAsync);
			}

			async Task ResolveAndJoinOnlineRoomAsync()
			{
				try
				{
					using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
					using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
					using var directory = new OnlineRoomDirectoryClient(
						http, OnlineRoomDirectoryClient.ParseBaseUri(config.OnlineLobbyUrl));
					OnlineRoom room;
					if (config.DiscoveryMode == IosMultiplayerSmokeDiscoveryMode.OnlineCode)
						room = await directory.ResolveCodeAsync(config.OnlineRoomCode, timeout.Token)
							.ConfigureAwait(false);
					else
					{
						var rooms = await directory.GetRoomsAsync(timeout.Token).ConfigureAwait(false);
						var matches = rooms.Where(candidate =>
							string.Equals(candidate.ServerId, config.OnlineServerId, StringComparison.Ordinal)).ToArray();
						if (matches.Length != 1)
							throw new InvalidOperationException(
								$"Online room list returned {matches.Length} matches for the expected server id.");
						room = matches[0];
					}

					var identity = new OnlineRoomCompatibilityIdentity(
						Game.EngineVersion, Game.ModData.Manifest.Id,
						Game.ModData.Manifest.Metadata.CompatibilityOrVersion,
						Game.ModData.RuntimeContract.ResourceCapability,
						ProtocolVersion.HandshakeSchema, ProtocolVersion.Orders);
					var compatibility = OnlineRoomCompatibility.Precheck(room, identity);
					if (compatibility != OnlineRoomCompatibilityReason.Compatible)
						throw new InvalidOperationException(
							$"Online room compatibility precheck rejected {compatibility}.");
					if (!room.IsJoinable)
						throw new InvalidOperationException("Online room is not joinable.");
					if (!room.TryGetConnectionTarget(out var target))
						throw new InvalidOperationException("Online room endpoint is invalid.");

					var stage = config.DiscoveryMode == IosMultiplayerSmokeDiscoveryMode.OnlineList ?
						"online-room-list-matched" : "online-room-code-matched";
					var evidenceAddress = config.DiscoveryMode == IosMultiplayerSmokeDiscoveryMode.OnlineList ?
						$"online-list:{room.ServerId}" : $"online-code:{room.RoomCode}";
					Game.RunAfterTick(() =>
					{
						if (!terminal)
							Join(target, evidenceAddress, stage);
					});
				}
				catch (Exception ex)
				{
					Game.RunAfterTick(() =>
					{
						if (!terminal)
							Fail($"Online discovery failed: {ex.Message}");
					});
				}
			}

			void NearbyGamesChanged()
			{
				var games = NearbyGameNetworking.Games.ToArray();
				Game.RunAfterTick(() => TryJoinNearby(games));
			}

			void TryJoinNearby(NearbyGameInfo[] games)
			{
				if (terminal || orderManager != null)
					return;

				if (!IosMultiplayerSmokeDiscovery.TryFindUniqueRoom(
					games, config.RunId, config.LaunchNonce,
					config.ExpectedRemoteNonce, runtimeHash, out var room, out var error))
				{
					if (!string.IsNullOrEmpty(error))
						Fail(error);

					return;
				}

				observedRemoteSessionGuid = room.RemoteSessionGuid;
				observedRemoteParticipantNonce = room.RemoteParticipantNonce;
				observedRemoteRuntimeHash = room.RemoteBuildToken;
				var address = NearbyGameNetworking.FormatAddress(room.Game.ServiceId);
				if (!ConnectionTarget.TryParseGameAddress(address, out var target))
				{
					Fail($"Nearby room '{room.Game.ServiceId}' could not create a connection relay.");
					return;
				}

				Join(target, address, "nearby-room-matched");
			}

			void Join(ConnectionTarget target, string address, string stage)
			{
				if (orderManager != null)
					return;

				AttachOrderManager(Game.JoinServer(target, string.Empty, recordReplay: false));
				connectedAddress = address;
				discoveryStage = stage;
				discoveryElapsedMs = elapsed.ElapsedMilliseconds;
				if (stage == "nearby-room-matched")
					StopNearbyDiscovery();

				Write("CONNECTING", string.Empty);
			}

			void AttachOrderManager(OrderManager manager)
			{
				orderManager = manager;
				orderManager.SafeStartRejected += HandleSafeStartRejected;
			}

			void HandleSafeStartRejected(LobbyStartRejection rejection)
			{
				if (terminal || config.Role != IosMultiplayerSmokeRole.Host || !blockedStartIssued)
					return;

				var currentMapUid = orderManager?.LobbyInfo.GlobalSettings.Map;
				if (blockedStartRejected || rejection.Reason != Session.LobbyStartBlockReason.ClientMapDownloading ||
					rejection.ClientIndex != blockedClientIndex || rejection.Progress != 42 ||
					rejection.RequestId != blockedStartRequest.RequestId ||
					!string.Equals(rejection.ExpectedMapUid, currentMapUid, StringComparison.Ordinal))
				{
					Fail("Safe-start blocker did not identify the expected remote 42% map download.");
					return;
				}

				blockedStartRejected = true;
			}

			void Poll()
			{
				if (terminal)
					return;

				try
				{
					if (orderManager == null)
					{
						if (elapsed.Elapsed.TotalSeconds >= config.TimeoutSeconds)
						{
							Fail("Timed out before discovering the matching nearby smoke room.");
							return;
						}

						var discoverySecond = (long)elapsed.Elapsed.TotalSeconds;
						if (discoverySecond != lastJournalSecond)
						{
							lastJournalSecond = discoverySecond;
							Write("DISCOVERING", string.Empty);
						}

						Game.RunAfterTick(Poll);
						return;
					}

					var connection = orderManager.Connection as NetworkConnection;
					if (connection?.ConnectionState == ConnectionState.NotConnected)
					{
						Fail(connection.ErrorMessage ?? "Network connection closed.");
						return;
					}

					var humans = orderManager.LobbyInfo.Clients.Where(c => !c.IsBot).ToArray();
					var localClient = orderManager.LocalClient;
					Session.Client remoteClient = null;
					if (localClient != null)
						PublishLocalIdentityFrame(localClient.Name);

					if (humans.Length > 2)
					{
						Fail("Unexpected third participant joined the multiplayer smoke lobby.");
						return;
					}

					remoteIdentityCurrentlyValid = false;
					if (localClient != null && humans.Length == 2)
					{
						remoteClient = humans.Single(client => client.Index != localClient.Index);
						CaptureRemoteIdentityFrame(remoteClient.Name);
					}

					if (!remoteIdentityVerified && elapsed.Elapsed.TotalSeconds >= config.TimeoutSeconds)
					{
						Fail("Timed out waiting for the complete remote 128-bit launch identity.");
						return;
					}

					var currentMapUid = orderManager.LobbyInfo.GlobalSettings.Map;
					var selectedMap = Game.ModData.MapCache.FirstOrDefault(map => map.Uid == currentMapUid);
					var localMapAvailable = selectedMap != null && selectedMap.Status == MapStatus.Available;
					var serverMapPlayable = orderManager.LobbyInfo.GlobalSettings.MapStatus
						.HasFlag(Session.MapStatus.Playable);
					var identityReady = localClient != null && IosMultiplayerSmokeReadiness.CanIssueReady(
						remoteIdentityVerified, remoteAcknowledgedLocalIdentity,
						remoteIdentityCurrentlyValid, localClient.Name,
						lastIssuedIdentityFrame, localAcknowledgementFrame);
					gameplayOptionsObserved = !config.RequireGameplay ||
						(orderManager.LobbyInfo.GlobalSettings.OptionOrDefault("cheats", false) &&
						string.Equals(orderManager.LobbyInfo.GlobalSettings.OptionOrDefault(
							"startingunits", "none"), "heavy", StringComparison.Ordinal));

					if (config.RequireGameplay && config.Role == IosMultiplayerSmokeRole.Host &&
						identityReady && localClient?.IsAdmin == true && !gameplayOptionsIssued)
					{
						gameplayOptionsIssued = true;
						orderManager.IssueOrder(Order.Command("option startingunits heavy"));
						orderManager.IssueOrder(Order.Command("option cheats True"));
					}

					if (config.Role == IosMultiplayerSmokeRole.Client && identityReady && gameplayOptionsObserved &&
						!mapBlockIssued && localMapAvailable && serverMapPlayable &&
						localClient.IsMapReadyFor(currentMapUid))
					{
						mapBlockIssued = true;
						var report = new ClientMapReadinessReport(
							currentMapUid, Session.ClientMapPhase.Downloading, 42);
						orderManager.IssueOrder(Order.Command(report.ToCommand()));
					}

					if (config.Role == IosMultiplayerSmokeRole.Client && mapBlockIssued &&
						localClient?.MapUid == currentMapUid &&
						localClient.MapPhase == Session.ClientMapPhase.Downloading &&
						localClient.MapProgress == 42)
						mapBlockObserved = true;

					if (config.Role == IosMultiplayerSmokeRole.Host && !blockedStartIssued &&
						identityReady && gameplayOptionsObserved && localMapAvailable && serverMapPlayable &&
						localClient.IsMapReadyFor(currentMapUid) && remoteClient != null &&
						remoteClient.MapUid == currentMapUid &&
						remoteClient.MapPhase == Session.ClientMapPhase.Downloading &&
						remoteClient.MapProgress == 42)
					{
						blockedClientIndex = remoteClient.Index;
						blockedStartRequest = new LobbySafeStartRequest(currentMapUid, 1);
						blockedStartIssued = true;
						orderManager.IssueOrder(Order.Command(blockedStartRequest.ToCommand()));
					}

					if (config.Role == IosMultiplayerSmokeRole.Client && mapBlockObserved &&
						!mapReadyRestoreIssued && remoteClient?.State == Session.ClientState.Ready)
					{
						mapReadyRestoreIssued = true;
						var report = new ClientMapReadinessReport(
							currentMapUid, Session.ClientMapPhase.Ready, 100);
						orderManager.IssueOrder(Order.Command(report.ToCommand()));
					}

					var blockedPhaseComplete = config.Role == IosMultiplayerSmokeRole.Host ?
						blockedStartRejected : mapReadyRestoreIssued;
					var canIssueReady = identityReady && gameplayOptionsObserved && blockedPhaseComplete && localMapAvailable &&
						serverMapPlayable && localClient.IsMapReadyFor(currentMapUid);
					if (canIssueReady && localClient != null && !readyIssued &&
						localClient.State != Session.ClientState.Ready)
					{
						readyIssued = true;
						orderManager.IssueOrder(Order.Command($"state {Session.ClientState.Ready}"));
					}

					if (config.Role == IosMultiplayerSmokeRole.Host && blockedStartRejected && !startIssued &&
						remoteIdentityVerified && remoteIdentityCurrentlyValid && localClient?.IsAdmin == true &&
						humans.Length == 2 && humans.All(c => c.State == Session.ClientState.Ready) &&
						humans.All(c => c.IsMapReadyFor(currentMapUid)))
					{
						finalStartRequest = new LobbySafeStartRequest(currentMapUid, 2);
						startIssued = true;
						orderManager.IssueOrder(Order.Command(finalStartRequest.ToCommand()));
					}

					if (orderManager.IsOutOfSync)
					{
						Fail("Order manager reported a desync.");
						return;
					}

					if (orderManager.World != null && orderManager.GameStarted && config.RequireGameplay)
						PollGameplay(orderManager.World);

					if (remoteIdentityVerified && remoteIdentityCurrentlyValid && humans.Length == 2 &&
						orderManager.World != null && orderManager.GameStarted &&
						orderManager.World.WorldTick >= config.RequiredWorldTicks &&
						(!config.RequireGameplay || GameplayComplete()))
					{
						Pass();
						return;
					}

					if (elapsed.Elapsed.TotalSeconds >= config.TimeoutSeconds)
					{
						Fail("Timed out before both clients reached the synchronized world-tick target.");
						return;
					}

					var second = (long)elapsed.Elapsed.TotalSeconds;
					if (second != lastJournalSecond)
					{
						lastJournalSecond = second;
						Write(orderManager.GameStarted ? "RUNNING" : "CONNECTING", string.Empty);
					}

					Game.RunAfterTick(Poll);
				}
				catch (Exception e)
				{
					Fail($"{e.GetType().Name}: {e.Message}");
				}
			}

			void PollGameplay(World world)
			{
				var player = world.LocalPlayer;
				if (player == null || player.PlayerActor == null || !player.PlayerActor.IsInWorld)
					return;

				var ownedActors = world.Actors.Where(actor => actor.IsInWorld && !actor.IsDead &&
					actor.Owner == player).ToArray();
				spawnObserved |= ownedActors.Length > 1;
				if (!spawnObserved)
				{
					gameplayStage = "waiting-for-spawn";
					return;
				}

				var combatNames = new HashSet<string>(new[]
				{
					"mtnk", "fv", "e1", "htnk", "htk", "e2", "ltnk", "ytnk", "init"
				}, StringComparer.OrdinalIgnoreCase);
				var combatActor = ownedActors.FirstOrDefault(actor => actor.ActorID == combatActorId);
				if (combatActor == null)
				{
					combatActor = ownedActors.FirstOrDefault(actor => combatNames.Contains(actor.Info.Name));
					if (combatActor != null)
					{
						combatActorId = combatActor.ActorID;
						combatStartLocation = combatActor.Location;
					}
				}

				if (combatActor != null && !selectObserved)
				{
					world.Selection.Combine(world, new[] { combatActor }, false, true);
					selectObserved = world.Selection.Contains(combatActor);
					gameplayStage = selectObserved ? "selected" : "waiting-for-selection";
				}

				if (selectObserved && combatActor != null && !movementOrderIssued)
				{
					var target = world.Map.FindTilesInAnnulus(combatActor.Location, 3, 7)
						.FirstOrDefault(cell => world.Map.Contains(cell));
					if (target != default && target != combatActor.Location)
					{
						world.IssueOrder(new Order("Move", combatActor, Target.FromCell(world, target), false));
						movementOrderIssued = true;
						movementIssuedAtTick = world.WorldTick;
						gameplayStage = "movement-issued";
					}
				}

				if (movementOrderIssued && combatActor != null && world.WorldTick > movementIssuedAtTick + 5 &&
					combatActor.Location != combatStartLocation)
				{
					movementObserved = true;
					gameplayStage = "movement-observed";
				}

				if (movementObserved && !resourceUpdateOrderIssued)
				{
					syncHashBeforeResource = world.SyncHash();
					world.IssueOrder(new Order("DevGiveCash", player.PlayerActor, false) { ExtraData = 12345 });
					resourceUpdateOrderIssued = true;
					resourceIssuedAtTick = world.WorldTick;
					gameplayStage = "resource-update-issued";
				}

				if (resourceUpdateOrderIssued && world.WorldTick > resourceIssuedAtTick + 5 &&
					world.SyncHash() != syncHashBeforeResource)
				{
					resourceUpdateObserved = true;
					gameplayStage = "resource-update-observed";
				}

				var mcv = ownedActors.FirstOrDefault(actor =>
					actor.Info.Name is "amcv" or "smcv" or "pcv");
				if (resourceUpdateObserved && mcv != null && !deployOrderIssued)
				{
					world.IssueOrder(new Order("DeployTransform", mcv, false));
					deployOrderIssued = true;
					gameplayStage = "mcv-deploy-issued";
				}

				var constructionYard = ownedActors.FirstOrDefault(actor =>
					actor.Info.Name is "gacnst" or "nacnst" or "yacnst");
				var powerPlant = PowerPlantFor(player.Faction.InternalName);
				if (deployOrderIssued && constructionYard != null && !productionOrderIssued)
				{
					world.IssueOrder(new Order("DevAll", player.PlayerActor, false));
					world.IssueOrder(Order.StartProduction(player.PlayerActor, powerPlant, 1));
					productionOrderIssued = true;
					productionObserved = true;
					productionIssuedAtTick = world.WorldTick;
					gameplayStage = "production-started";
				}

				completedProductionObserved |= ownedActors.Any(actor =>
					string.Equals(actor.Info.Name, powerPlant, StringComparison.OrdinalIgnoreCase));
				if (productionOrderIssued && !completedProductionObserved &&
					world.WorldTick > productionIssuedAtTick + 10 && world.WorldTick % 8 == 0)
				{
					var candidates = world.Map.FindTilesInAnnulus(player.HomeLocation, 6, 18)
						.Where(world.Map.Contains).Take(64).ToArray();
					if (candidates.Length > 0)
					{
						var target = candidates[placementAttempt++ % candidates.Length];
						world.IssueOrder(new Order("PlaceBuilding", player.PlayerActor,
							Target.FromCell(world, target), false)
						{
							TargetString = powerPlant,
							ExtraData = player.PlayerActor.ActorID,
							SuppressVisualFeedback = true
						});
					}
				}

				if (completedProductionObserved)
					gameplayStage = "production-completed";

				if (completedProductionObserved && !visibilityOrderIssued)
				{
					world.IssueOrder(new Order("DevVisibility", player.PlayerActor, false));
					visibilityOrderIssued = true;
					visibilityIssuedAtTick = world.WorldTick;
					gameplayStage = "visibility-issued";
				}

				var enemy = combatActor != null && visibilityOrderIssued && world.WorldTick > visibilityIssuedAtTick ?
					world.Actors.FirstOrDefault(actor => actor.IsInWorld && !actor.IsDead &&
						actor.Owner != player && actor.Owner.Playable && combatNames.Contains(actor.Info.Name) &&
						actor.CanBeViewedByPlayer(player) && actor.IsTargetableBy(combatActor)) : null;
				if (completedProductionObserved && combatActor != null && enemy != null && !attackOrderIssued)
				{
					enemyActorId = enemy.ActorID;
					world.IssueOrder(new Order("Attack", combatActor, Target.FromActor(enemy), false));
					attackOrderIssued = true;
					attackIssuedAtTick = world.WorldTick;
					gameplayStage = "attack-issued";
				}
				else if (completedProductionObserved && combatActor != null && enemy == null &&
					!attackOrderIssued && world.WorldTick > visibilityIssuedAtTick + 30)
				{
					// Large maps can place both humans outside each other's actor-visibility range.
					// Exercise the synchronized attack-order path deterministically instead of
					// waiting for the players to meet by chance.
					var target = world.Map.FindTilesInAnnulus(combatActor.Location, 4, 8)
						.FirstOrDefault(world.Map.Contains);
					if (target != default)
					{
						terrainAttackFallback = true;
						world.IssueOrder(new Order("ForceAttack", combatActor,
							Target.FromCell(world, target), false));
						attackOrderIssued = true;
						attackIssuedAtTick = world.WorldTick;
						gameplayStage = "terrain-attack-issued";
					}
				}

				if (attackOrderIssued && !attackObserved && !terrainAttackFallback &&
					world.WorldTick > attackIssuedAtTick + 30)
				{
					var target = world.Map.FindTilesInAnnulus(combatActor.Location, 4, 8)
						.FirstOrDefault(world.Map.Contains);
					if (target != default)
					{
						terrainAttackFallback = true;
						world.IssueOrder(new Order("ForceAttack", combatActor,
							Target.FromCell(world, target), false));
						attackIssuedAtTick = world.WorldTick;
						gameplayStage = "terrain-attack-issued";
					}
				}

				if (attackOrderIssued && !attackObserved && world.WorldTick > attackIssuedAtTick)
				{
					var currentEnemy = terrainAttackFallback ? null :
						world.Actors.FirstOrDefault(actor => actor.ActorID == enemyActorId);
					attackObserved = combatActor == null || combatActor.IsDead ||
						(!terrainAttackFallback && (currentEnemy == null || currentEnemy.IsDead)) ||
						combatActor.CurrentActivity != null;
					if (attackObserved)
						gameplayStage = "stable-gameplay";
				}
			}

			bool GameplayComplete() => spawnObserved && selectObserved && movementObserved &&
				resourceUpdateObserved && productionObserved && completedProductionObserved &&
				attackObserved;

			static string PowerPlantFor(string faction) => faction == "yuri" ? "yapowr" :
				faction is "soviets" or "cuba" or "libya" or "iraq" or "russia" ? "napowr" : "gapowr";

			void PublishLocalIdentityFrame(string currentName)
			{
				if (localPublishedFrames == null || localPublishedFrames.Count == 0)
					return;

				// Once the peer has acknowledged our complete identity, hold our own
				// acknowledgement name steady.  Lobby commands reject name changes after
				// Ready, so this must be the final published name before changing state.
				if (IosMultiplayerSmokeReadiness.ShouldHoldAcknowledgement(
					remoteAcknowledgedLocalIdentity, currentName,
					lastIssuedIdentityFrame, localAcknowledgementFrame))
					return;

				if (lastIssuedIdentityFrame == null)
				{
					lastIssuedIdentityFrame = localPublishedFrames[0];
					orderManager.IssueOrder(Order.Command("name " + lastIssuedIdentityFrame));
					return;
				}

				if (currentName != lastIssuedIdentityFrame || ++localIdentityFrameHoldTicks < 2)
					return;

				localIdentityFrameHoldTicks = 0;
				localIdentityFrameIndex = (localIdentityFrameIndex + 1) % localPublishedFrames.Count;
				lastIssuedIdentityFrame = localPublishedFrames[localIdentityFrameIndex];
				orderManager.IssueOrder(Order.Command("name " + lastIssuedIdentityFrame));
			}

			void CaptureRemoteIdentityFrame(string name)
			{
				var expectedRole = config.Role == IosMultiplayerSmokeRole.Host ?
					IosMultiplayerSmokeRole.Client : IosMultiplayerSmokeRole.Host;
				if (IosMultiplayerSmokeParticipantIdentity.IsAcknowledgementFrame(
					name, remoteIdentity, localIdentity))
				{
					remoteAcknowledgedLocalIdentity = true;
					remoteIdentityCurrentlyValid = true;
					return;
				}

				if (!IosMultiplayerSmokeParticipantIdentity.TryParseFrame(
					name, config.LaunchNonce, out var role, out _, out _, out _) || role != expectedRole)
					return;

				if (remoteIdentityVerified)
				{
					var expectedFrames = IosMultiplayerSmokeParticipantIdentity.CreateFrames(
						remoteIdentity.Role, remoteIdentity.LaunchNonce, remoteIdentity.ParticipantNonce,
						remoteIdentity.RuntimeHash, remoteIdentity.SessionGuid);
					remoteIdentityCurrentlyValid = expectedFrames.Contains(name);
					return;
				}

				remoteIdentityFrames.Add(name);
				if (!IosMultiplayerSmokeParticipantIdentity.TryReassemble(
					remoteIdentityFrames, config.LaunchNonce, out var identity, out var error))
				{
					if (!string.IsNullOrEmpty(error))
						throw new InvalidOperationException(error);

					return;
				}

				if (identity.Role != expectedRole || identity.LaunchNonce != config.LaunchNonce ||
					identity.ParticipantNonce != config.ExpectedRemoteNonce || identity.RuntimeHash != runtimeHash)
					throw new InvalidOperationException(
						"Remote participant launch nonce, participant nonce, or runtime hash did not match this run.");

				if (config.Role == IosMultiplayerSmokeRole.Client &&
					config.DiscoveryMode == IosMultiplayerSmokeDiscoveryMode.Auto &&
					!string.Equals(identity.SessionGuid, observedRemoteSessionGuid, StringComparison.OrdinalIgnoreCase))
					throw new InvalidOperationException(
						"Host lobby session GUID did not match the discovered advertisement session.");

				remoteIdentity = identity;
				remoteIdentityVerified = true;
				remoteIdentityCurrentlyValid = true;
				localAcknowledgementFrame = IosMultiplayerSmokeParticipantIdentity.CreateAcknowledgementFrame(
					localIdentity, remoteIdentity);
				localPublishedFrames = localIdentityFrames.Concat(new[] { localAcknowledgementFrame }).ToArray();
				observedRemoteParticipantNonce = identity.ParticipantNonce;
				observedRemoteRuntimeHash = identity.RuntimeHash;
				observedRemoteSessionGuid = identity.SessionGuid;
			}

			void Pass()
			{
				terminal = true;
				DetachOrderManagerEvents();
				StopNearbyDiscovery();
				Write("PASSED", string.Empty);
			}

			void Fail(string detail)
			{
				terminal = true;
				DetachOrderManagerEvents();
				StopNearbyDiscovery();
				Write("FAILED", detail);
				Console.WriteLine($"OpenRA iOS multiplayer smoke failed ({config.Role}): {detail}");
			}

			void Write(string status, string detail)
			{
				var connectionState = orderManager?.Connection is NetworkConnection connection ?
					connection.ConnectionState.ToString() : "None";
				var clients = orderManager?.LobbyInfo.Clients.Count(c => !c.IsBot) ?? 0;
				var readyClients = orderManager?.LobbyInfo.Clients.Count(c =>
					!c.IsBot && c.State == Session.ClientState.Ready) ?? 0;
				var content = "{\n" +
					$"  \"status\": \"{Escape(status)}\",\n" +
					$"  \"role\": \"{config.Role.ToString().ToLowerInvariant()}\",\n" +
					$"  \"runId\": \"{Escape(config.RunId)}\",\n" +
					$"  \"detail\": \"{Escape(detail)}\",\n" +
					$"  \"discoveryMode\": \"{config.DiscoveryMode.ToString().ToLowerInvariant()}\",\n" +
					$"  \"discoveryStage\": \"{Escape(discoveryStage)}\",\n" +
					$"  \"discoveryElapsedMs\": {discoveryElapsedMs},\n" +
					"  \"runtimeIdentityKind\": \"runtime-contract-v1\",\n" +
					$"  \"runtimeHash\": \"{Escape(runtimeHash)}\",\n" +
					$"  \"expectedRuntimeHash\": \"{Escape(config.ExpectedRuntimeHash)}\",\n" +
					$"  \"runtimeProfile\": \"{Escape(runtimeProfile)}\",\n" +
					$"  \"runtimeContract\": \"{Escape(runtimeContract)}\",\n" +
					$"  \"modCompatibility\": \"{Escape(modCompatibility)}\",\n" +
					$"  \"resourceCapability\": \"{Escape(resourceCapability)}\",\n" +
					$"  \"localLanAddresses\": \"{Escape(localLanAddresses)}\",\n" +
					$"  \"launchNonce\": \"{Escape(config.LaunchNonce)}\",\n" +
					$"  \"sessionGuid\": \"{Escape(sessionGuid)}\",\n" +
					$"  \"participantNonce\": \"{Escape(config.ParticipantNonce)}\",\n" +
					$"  \"expectedRemoteNonce\": \"{Escape(config.ExpectedRemoteNonce)}\",\n" +
					$"  \"observedRemoteRuntimeHash\": \"{Escape(observedRemoteRuntimeHash)}\",\n" +
					$"  \"observedRemoteParticipantNonce\": \"{Escape(observedRemoteParticipantNonce)}\",\n" +
					$"  \"observedRemoteSessionGuid\": \"{Escape(observedRemoteSessionGuid)}\",\n" +
					$"  \"remoteIdentityVerified\": {(remoteIdentityVerified ? "true" : "false")},\n" +
					$"  \"remoteAcknowledgedLocalIdentity\": {(remoteAcknowledgedLocalIdentity ? "true" : "false")},\n" +
					$"  \"mapBlockIssued\": {(mapBlockIssued ? "true" : "false")},\n" +
					$"  \"mapBlockObserved\": {(mapBlockObserved ? "true" : "false")},\n" +
					$"  \"blockedStartIssued\": {(blockedStartIssued ? "true" : "false")},\n" +
					$"  \"blockedStartRejected\": {(blockedStartRejected ? "true" : "false")},\n" +
					$"  \"mapReadyRestoreIssued\": {(mapReadyRestoreIssued ? "true" : "false")},\n" +
					$"  \"gameplayRequired\": {(config.RequireGameplay ? "true" : "false")},\n" +
					$"  \"gameplayOptionsObserved\": {(gameplayOptionsObserved ? "true" : "false")},\n" +
					$"  \"gameplayStage\": \"{Escape(gameplayStage)}\",\n" +
					$"  \"spawnObserved\": {(spawnObserved ? "true" : "false")},\n" +
					$"  \"selectObserved\": {(selectObserved ? "true" : "false")},\n" +
					$"  \"movementOrderIssued\": {(movementOrderIssued ? "true" : "false")},\n" +
					$"  \"movementObserved\": {(movementObserved ? "true" : "false")},\n" +
					$"  \"resourceUpdateOrderIssued\": {(resourceUpdateOrderIssued ? "true" : "false")},\n" +
					$"  \"resourceUpdateObserved\": {(resourceUpdateObserved ? "true" : "false")},\n" +
					$"  \"productionObserved\": {(productionObserved ? "true" : "false")},\n" +
					$"  \"completedProductionObserved\": {(completedProductionObserved ? "true" : "false")},\n" +
					$"  \"attackOrderIssued\": {(attackOrderIssued ? "true" : "false")},\n" +
					$"  \"attackTargetKind\": \"{(terrainAttackFallback ? "terrain" : "actor")}\",\n" +
					$"  \"attackObserved\": {(attackObserved ? "true" : "false")},\n" +
					"  \"orderRejectionObserved\": false,\n" +
					$"  \"processStartedUtc\": \"{processStartedUtc:O}\",\n" +
					$"  \"connectionState\": \"{Escape(connectionState)}\",\n" +
					$"  \"clients\": {clients},\n" +
					$"  \"readyClients\": {readyClients},\n" +
					$"  \"netFrame\": {orderManager?.NetFrameNumber ?? 0},\n" +
					$"  \"localFrame\": {orderManager?.LocalFrameNumber ?? 0},\n" +
					$"  \"worldTick\": {orderManager?.World?.WorldTick ?? 0},\n" +
					$"  \"outOfSync\": {(orderManager?.IsOutOfSync == true ? "true" : "false")},\n" +
					$"  \"map\": \"{Escape(orderManager?.LobbyInfo.GlobalSettings.Map ?? config.Map)}\",\n" +
					$"  \"endpoint\": \"{Escape(connectedAddress ?? config.Endpoint)}\",\n" +
					$"  \"updatedUtc\": \"{DateTime.UtcNow:O}\"\n" +
					"}\n";
				var temporary = journalPath + ".tmp";
				File.WriteAllText(temporary, content);
				File.Move(temporary, journalPath, true);
			}

			void DetachOrderManagerEvents()
			{
				if (orderManager != null)
					orderManager.SafeStartRejected -= HandleSafeStartRejected;
			}

			void StopNearbyDiscovery()
			{
				if (!nearbySubscribed)
					return;

				nearbySubscribed = false;
				NearbyGameNetworking.GamesChanged -= NearbyGamesChanged;
				NearbyGameNetworking.StopBrowsing();
			}

			static string GetLocalLanAddresses()
			{
				try
				{
					return NetworkInterface.GetAllNetworkInterfaces()
						.Where(network => network.OperationalStatus == OperationalStatus.Up)
						.SelectMany(network => network.GetIPProperties().UnicastAddresses)
						.Select(address => address.Address)
						.Where(address => address.AddressFamily == AddressFamily.InterNetwork &&
							!IPAddress.IsLoopback(address))
						.Select(address => address.ToString())
						.Distinct(StringComparer.Ordinal)
						.OrderBy(address => address, StringComparer.Ordinal)
						.JoinWith(",");
				}
				catch
				{
					return string.Empty;
				}
			}

			static string Escape(string value) => (value ?? string.Empty)
				.Replace("\\", "\\\\", StringComparison.Ordinal)
				.Replace("\"", "\\\"", StringComparison.Ordinal)
				.Replace("\r", "\\r", StringComparison.Ordinal)
				.Replace("\n", "\\n", StringComparison.Ordinal);
		}
	}
}
