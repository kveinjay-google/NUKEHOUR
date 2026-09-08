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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.FileFormats;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Network;
using GameServer = OpenRA.Server.Server;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RuntimeContractPersistenceTest
	{
		const string ModId = "ra2";
		const string ModVersion = "release-20250330";
		const string DigestA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		const string DigestB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

		static readonly string SupportDir = Path.Combine("test-root", "support");
		static readonly RuntimeContractIdentity BaseIdentity = new("ra2-base", DigestA);
		static readonly RuntimeContractIdentity BaseOtherDigest = new("ra2-base", DigestB);
		static readonly RuntimeContractIdentity YuriIdentity = new("ra2-yuri", DigestA);

		string temporaryDirectory;

		[SetUp]
		public void SetUp()
		{
			temporaryDirectory = Path.Combine(
				Path.GetTempPath(), $"openra-runtime-contract-persistence-{Guid.NewGuid():N}");
			Directory.CreateDirectory(temporaryDirectory);
		}

		[TearDown]
		public void TearDown()
		{
			if (Directory.Exists(temporaryDirectory))
				Directory.Delete(temporaryDirectory, true);
		}

		[Test]
		[Timeout(10000)]
		public void LegacyPathsRemainTheExistingDesktopPaths()
		{
			Assert.Multiple(() =>
			{
				Assert.That(RuntimeContractStorage.SavesPath(
					SupportDir, ModId, ModVersion, RuntimeContractIdentity.Legacy),
					Is.EqualTo(Path.Combine(SupportDir, "Saves", ModId, ModVersion)));
				Assert.That(RuntimeContractStorage.ReplaysPath(
					SupportDir, ModId, ModVersion, RuntimeContractIdentity.Legacy),
					Is.EqualTo(Path.Combine(SupportDir, "Replays", ModId, ModVersion)));
				Assert.That(RuntimeContractStorage.ServerSettingsPath(
					SupportDir, ModId, RuntimeContractIdentity.Legacy),
					Is.EqualTo(Path.Combine(SupportDir, "settings.yaml")));
				Assert.That(RuntimeContractStorage.SkirmishPath(
					SupportDir, ModId, RuntimeContractIdentity.Legacy),
					Is.EqualTo(Path.Combine(SupportDir, $"skirmish.{ModId}.yaml")));
			});
		}

		[Test]
		[Timeout(10000)]
		public void BaseProfilePathsIncludeTheCanonicalContractIdentity()
		{
			AssertProfiledPaths(BaseIdentity, ModVersion);
		}

		[Test]
		[Timeout(10000)]
		public void YuriAndDifferentDigestUseDistinctCanonicalRoots()
		{
			AssertProfiledPaths(YuriIdentity, ModVersion);
			AssertProfiledPaths(BaseOtherDigest, ModVersion);

			Assert.That(
				RuntimeContractStorage.ServerSettingsPath(SupportDir, ModId, YuriIdentity),
				Is.Not.EqualTo(RuntimeContractStorage.ServerSettingsPath(
					SupportDir, ModId, BaseOtherDigest)));
		}

		[Test]
		[Timeout(10000)]
		public void DevelopmentVersionIsOneCanonicalCasePreservingSegment()
		{
			AssertProfiledPaths(BaseIdentity, "{DEV_VERSION}");
			Assert.That(RuntimeContractStorage.SavesPath(
				SupportDir, ModId, "{DEV_VERSION}", RuntimeContractIdentity.Legacy),
				Is.EqualTo(Path.Combine(SupportDir, "Saves", ModId, "{DEV_VERSION}")));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("RA2")]
		[TestCase(".")]
		[TestCase("..")]
		[TestCase("../ra2")]
		[TestCase("ra2/escape")]
		[TestCase("ra2\\escape")]
		[TestCase("ra2:escape")]
		[Timeout(10000)]
		public void StorageRejectsNonCanonicalModIds(string modId)
		{
			Assert.Multiple(() =>
			{
				Assert.That(() => RuntimeContractStorage.SavesPath(
					SupportDir, modId, ModVersion, BaseIdentity),
					Throws.InstanceOf<ArgumentException>());
				Assert.That(() => RuntimeContractStorage.ReplaysPath(
					SupportDir, modId, ModVersion, BaseIdentity),
					Throws.InstanceOf<ArgumentException>());
				Assert.That(() => RuntimeContractStorage.ServerSettingsPath(
					SupportDir, modId, BaseIdentity),
					Throws.InstanceOf<ArgumentException>());
				Assert.That(() => RuntimeContractStorage.SkirmishPath(
					SupportDir, modId, BaseIdentity),
					Throws.InstanceOf<ArgumentException>());
			});
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase(".")]
		[TestCase("..")]
		[TestCase("../release")]
		[TestCase("release/escape")]
		[TestCase("release\\escape")]
		[TestCase("/absolute")]
		[TestCase("release:stream")]
		[TestCase("e\u0301")]
		[Timeout(10000)]
		public void SaveAndReplayPathsRejectNonCanonicalVersions(string modVersion)
		{
			Assert.Multiple(() =>
			{
				Assert.That(() => RuntimeContractStorage.SavesPath(
					SupportDir, ModId, modVersion, BaseIdentity),
					Throws.InstanceOf<ArgumentException>());
				Assert.That(() => RuntimeContractStorage.ReplaysPath(
					SupportDir, ModId, modVersion, BaseIdentity),
					Throws.InstanceOf<ArgumentException>());
			});
		}

		[Test]
		[Timeout(10000)]
		public void StorageRejectsTheDefaultUninitializedIdentity()
		{
			Assert.Multiple(() =>
			{
				Assert.That(() => RuntimeContractStorage.SavesPath(
					SupportDir, ModId, ModVersion, default), Throws.ArgumentException);
				Assert.That(() => RuntimeContractStorage.ReplaysPath(
					SupportDir, ModId, ModVersion, default), Throws.ArgumentException);
				Assert.That(() => RuntimeContractStorage.ServerSettingsPath(
					SupportDir, ModId, default), Throws.ArgumentException);
				Assert.That(() => RuntimeContractStorage.SkirmishPath(
					SupportDir, ModId, default), Throws.ArgumentException);
			});
		}

		[Test]
		[Timeout(10000)]
		public void StoredIdentityCompatibilityUsesTheClosedFiveStateTable()
		{
			Assert.Multiple(() =>
			{
				Assert.That(RuntimeContractStorage.Compatibility(
					RuntimeContractIdentity.Legacy, null, null),
					Is.EqualTo(RuntimeContractCompatibilityReason.Compatible));
				Assert.That(RuntimeContractStorage.Compatibility(BaseIdentity, null, null),
					Is.EqualTo(RuntimeContractCompatibilityReason.MissingIdentity));
				Assert.That(RuntimeContractStorage.Compatibility(
					BaseIdentity, BaseIdentity.ProfileId, null),
					Is.EqualTo(RuntimeContractCompatibilityReason.MalformedIdentity));
				Assert.That(RuntimeContractStorage.Compatibility(
					BaseIdentity, null, BaseIdentity.ContractDigest),
					Is.EqualTo(RuntimeContractCompatibilityReason.MalformedIdentity));
				Assert.That(RuntimeContractStorage.Compatibility(
					BaseIdentity, "BASE", BaseIdentity.ContractDigest),
					Is.EqualTo(RuntimeContractCompatibilityReason.MalformedIdentity));
				Assert.That(RuntimeContractStorage.Compatibility(
					BaseIdentity, YuriIdentity.ProfileId, YuriIdentity.ContractDigest),
					Is.EqualTo(RuntimeContractCompatibilityReason.ProfileMismatch));
				Assert.That(RuntimeContractStorage.Compatibility(
					BaseIdentity, BaseOtherDigest.ProfileId, BaseOtherDigest.ContractDigest),
					Is.EqualTo(RuntimeContractCompatibilityReason.ContractMismatch));
				Assert.That(RuntimeContractStorage.Compatibility(
					BaseIdentity, BaseIdentity.ProfileId, BaseIdentity.ContractDigest),
					Is.EqualTo(RuntimeContractCompatibilityReason.Compatible));
				Assert.That(RuntimeContractStorage.Compatibility(
					default, BaseIdentity.ProfileId, BaseIdentity.ContractDigest),
					Is.EqualTo(RuntimeContractCompatibilityReason.MalformedIdentity));
			});
		}

		[Test]
		[NonParallelizable]
		[Timeout(10000)]
		public void NewGameInformationCapturesAndRoundTripsTheActiveRuntimeIdentity()
		{
			var originalModData = Game.ModData;
			try
			{
				Game.ModData = UninitializedModData(BaseIdentity);
				var captured = new GameInformation
				{
					Mod = ModId,
					Version = ModVersion,
				};

				Assert.Multiple(() =>
				{
					Assert.That(captured.RuntimeProfile, Is.EqualTo(BaseIdentity.ProfileId));
					Assert.That(captured.RuntimeContract, Is.EqualTo(BaseIdentity.ContractDigest));
				});

				var roundTripped = GameInformation.Deserialize(
					captured.Serialize(), "profiled-game-information");
				Assert.Multiple(() =>
				{
					Assert.That(roundTripped.RuntimeProfile, Is.EqualTo(BaseIdentity.ProfileId));
					Assert.That(roundTripped.RuntimeContract, Is.EqualTo(BaseIdentity.ContractDigest));
					Assert.That(RuntimeContractStorage.Compatibility(
						BaseIdentity, roundTripped.RuntimeProfile, roundTripped.RuntimeContract),
						Is.EqualTo(RuntimeContractCompatibilityReason.Compatible));
				});
			}
			finally
			{
				Game.ModData = originalModData;
			}
		}

		[Test]
		[NonParallelizable]
		[Timeout(10000)]
		public void LegacyGameInformationOmitsRuntimeFieldsWhenSerialized()
		{
			var originalModData = Game.ModData;
			try
			{
				Game.ModData = UninitializedModData(RuntimeContractIdentity.Legacy);
				var info = new GameInformation
				{
					Mod = ModId,
					Version = ModVersion,
				};
				var serialized = info.Serialize();

				Assert.Multiple(() =>
				{
					Assert.That(info.RuntimeProfile, Is.Null);
					Assert.That(info.RuntimeContract, Is.Null);
					Assert.That(serialized, Does.Not.Contain(nameof(GameInformation.RuntimeProfile)));
					Assert.That(serialized, Does.Not.Contain(nameof(GameInformation.RuntimeContract)));
				});
			}
			finally
			{
				Game.ModData = originalModData;
			}
		}

		[Test]
		[NonParallelizable]
		[Timeout(10000)]
		public void DeserializingLegacyGameInformationDoesNotCaptureTheCurrentProfile()
		{
			var originalModData = Game.ModData;
			try
			{
				Game.ModData = UninitializedModData(BaseIdentity);
				var legacy = GameInformation.Deserialize(
					"Root:\n\tMod: ra2\n\tVersion: release-20250330\n",
					"legacy-game-information");

				Assert.Multiple(() =>
				{
					Assert.That(legacy.RuntimeProfile, Is.Null);
					Assert.That(legacy.RuntimeContract, Is.Null);
					Assert.That(RuntimeContractStorage.Compatibility(
						BaseIdentity, legacy.RuntimeProfile, legacy.RuntimeContract),
						Is.EqualTo(RuntimeContractCompatibilityReason.MissingIdentity));
					Assert.That(typeof(GameInformation).GetConstructors(
						BindingFlags.Instance | BindingFlags.NonPublic),
						Has.Some.Matches<ConstructorInfo>(constructor => constructor.IsPrivate),
						"Deserialize must use a private non-capturing constructor.");
				});
			}
			finally
			{
				Game.ModData = originalModData;
			}
		}

		[Test]
		[Timeout(10000)]
		public void GameSaveCompatibilityUsesItsEmbeddedGlobalRuntimeIdentity()
		{
			Assert.Multiple(() =>
			{
				Assert.That(GameSaveWithIdentity(null, null)
					.RuntimeContractCompatibilityWith(RuntimeContractIdentity.Legacy),
					Is.EqualTo(RuntimeContractCompatibilityReason.Compatible));
				Assert.That(GameSaveWithIdentity(null, null)
					.RuntimeContractCompatibilityWith(BaseIdentity),
					Is.EqualTo(RuntimeContractCompatibilityReason.MissingIdentity));
				Assert.That(GameSaveWithIdentity(BaseIdentity.ProfileId, null)
					.RuntimeContractCompatibilityWith(BaseIdentity),
					Is.EqualTo(RuntimeContractCompatibilityReason.MalformedIdentity));
				Assert.That(GameSaveWithIdentity("BASE", BaseIdentity.ContractDigest)
					.RuntimeContractCompatibilityWith(BaseIdentity),
					Is.EqualTo(RuntimeContractCompatibilityReason.MalformedIdentity));
				Assert.That(GameSaveWithIdentity(
					YuriIdentity.ProfileId, YuriIdentity.ContractDigest)
					.RuntimeContractCompatibilityWith(BaseIdentity),
					Is.EqualTo(RuntimeContractCompatibilityReason.ProfileMismatch));
				Assert.That(GameSaveWithIdentity(
					BaseOtherDigest.ProfileId, BaseOtherDigest.ContractDigest)
					.RuntimeContractCompatibilityWith(BaseIdentity),
					Is.EqualTo(RuntimeContractCompatibilityReason.ContractMismatch));
				Assert.That(GameSaveWithIdentity(
					BaseIdentity.ProfileId, BaseIdentity.ContractDigest)
					.RuntimeContractCompatibilityWith(BaseIdentity),
					Is.EqualTo(RuntimeContractCompatibilityReason.Compatible));
				Assert.That(GameSaveWithIdentity(
					BaseIdentity.ProfileId, BaseIdentity.ContractDigest)
					.RuntimeContractCompatibilityWith(default),
					Is.EqualTo(RuntimeContractCompatibilityReason.MalformedIdentity));
			});
		}

		[Test]
		[Timeout(10000)]
		public void JoinReplayKeepsItsVoidApiAndRejectsBothIdentitiesBeforePublishingOrderManager()
		{
			var method = typeof(Game).GetMethod(nameof(Game.JoinReplay),
				BindingFlags.Public | BindingFlags.Static);
			Assert.That(method, Is.Not.Null);
			Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));

			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Game.cs"));
			var body = MethodBody(source, "public static void JoinReplay(string replayFile)");
			var legacyGuard = body.IndexOf(".IsLegacy", StringComparison.Ordinal);
			var legacyConnection = body.IndexOf(
				"new ReplayConnection(replayFile)", Math.Max(legacyGuard, 0), StringComparison.Ordinal);
			var snapshot = body.IndexOf(
				"File.ReadAllBytes(replayFile)", Math.Max(legacyConnection, 0), StringComparison.Ordinal);
			var replayConnection = body.IndexOf("CreateCompatibleReplayConnection(",
				Math.Max(snapshot, 0), StringComparison.Ordinal);
			var orderManager = body.IndexOf("new OrderManager(", Math.Max(replayConnection, 0),
				StringComparison.Ordinal);
			var publish = body.IndexOf("JoinInner(", Math.Max(orderManager, 0), StringComparison.Ordinal);

			Assert.Multiple(() =>
			{
				Assert.That(legacyGuard, Is.GreaterThanOrEqualTo(0));
				Assert.That(legacyConnection, Is.GreaterThan(legacyGuard),
					"Legacy replay admission must keep using the caller path directly.");
				Assert.That(snapshot, Is.GreaterThan(legacyConnection),
					"Only the profiled branch may capture bytes for a temporary snapshot.");
				Assert.That(CountOccurrences(body, "File.ReadAllBytes(replayFile)"), Is.EqualTo(1));
				Assert.That(replayConnection, Is.GreaterThan(snapshot));
				Assert.That(orderManager, Is.GreaterThan(replayConnection));
				Assert.That(publish, Is.GreaterThan(orderManager));
			});
		}

		[Test]
		[NonParallelizable]
		[Timeout(10000)]
		public void LegacyJoinReplayKeepsTheOriginalFilenameAndBypassesSnapshots()
		{
			var originalModData = Game.ModData;
			var originalOrderManager = Game.OrderManager;
			var path = Path.Combine(temporaryDirectory, "legacy-source.orarep");
			File.WriteAllBytes(path, ReplayBytes(
				null, null, null, null, includeLobby: true));

			ReplayConnection replay = null;
			try
			{
				Game.ModData = UninitializedReplayModData(RuntimeContractIdentity.Legacy);
				Game.OrderManager = null;
				Game.JoinReplay(path);
				replay = Game.OrderManager?.Connection as ReplayConnection;
				Assert.Multiple(() =>
				{
					Assert.That(replay, Is.Not.Null);
					Assert.That(replay?.Filename, Is.EqualTo(path),
						"Legacy desktop replay connections must retain the caller-visible source path.");
					Assert.That(File.Exists(path), Is.True,
						"Legacy admission must never transfer ownership of the caller's replay file.");
				});
			}
			finally
			{
				if (!ReferenceEquals(Game.OrderManager, originalOrderManager))
					Game.OrderManager?.Dispose();

				Game.OrderManager = originalOrderManager;
				Game.ModData = originalModData;
			}
		}

		[TestCase(null, null, RuntimeContractCompatibilityReason.MissingIdentity)]
		[TestCase("ra2-base", null, RuntimeContractCompatibilityReason.MalformedIdentity)]
		[TestCase("ra2-yuri", DigestA, RuntimeContractCompatibilityReason.ProfileMismatch)]
		[TestCase("ra2-base", DigestB, RuntimeContractCompatibilityReason.ContractMismatch)]
		[NonParallelizable]
		[Timeout(10000)]
		public void JoinReplayMetadataRejectionsCarryTheirExactTypedReason(
			string storedProfile,
			string storedContract,
			RuntimeContractCompatibilityReason expectedReason)
		{
			var originalModData = Game.ModData;
			var originalOrderManager = Game.OrderManager;
			try
			{
				Game.ModData = UninitializedModData(BaseIdentity);
				var path = Path.Combine(temporaryDirectory, "metadata-rejection.orarep");
				File.WriteAllBytes(path, ReplayBytes(
					storedProfile, storedContract, null, null, includeLobby: false));

				var thrown = Assert.Catch(() => Game.JoinReplay(path));
				AssertRuntimeContractCompatibilityException(thrown, expectedReason);
				Assert.That(Game.OrderManager, Is.SameAs(originalOrderManager));
			}
			finally
			{
				Game.ModData = originalModData;
				Game.OrderManager = originalOrderManager;
			}
		}

		[TestCase(null, null, RuntimeContractCompatibilityReason.MissingIdentity)]
		[TestCase("ra2-base", null, RuntimeContractCompatibilityReason.MalformedIdentity)]
		[TestCase("ra2-yuri", DigestA, RuntimeContractCompatibilityReason.ProfileMismatch)]
		[TestCase("ra2-base", DigestB, RuntimeContractCompatibilityReason.ContractMismatch)]
		[NonParallelizable]
		[Timeout(10000)]
		public void JoinReplayLobbyRejectionsCarryTheirExactTypedReason(
			string storedProfile,
			string storedContract,
			RuntimeContractCompatibilityReason expectedReason)
		{
			var originalModData = Game.ModData;
			var originalOrderManager = Game.OrderManager;
			try
			{
				Game.ModData = UninitializedReplayModData(BaseIdentity);
				var path = Path.Combine(temporaryDirectory, "lobby-rejection.orarep");
				File.WriteAllBytes(path, ReplayBytes(
					BaseIdentity.ProfileId, BaseIdentity.ContractDigest,
					storedProfile, storedContract, includeLobby: true));

				var thrown = Assert.Catch(() => Game.JoinReplay(path));
				AssertRuntimeContractCompatibilityException(thrown, expectedReason);
				Assert.That(Game.OrderManager, Is.SameAs(originalOrderManager));
			}
			finally
			{
				Game.ModData = originalModData;
				Game.OrderManager = originalOrderManager;
			}
		}

		[Test]
		[NonParallelizable]
		[Timeout(10000)]
		public void JoinReplayUsesOneImmutableSourceSnapshotForMetadataLobbyAndOrders()
		{
			var snapshotMethod = typeof(Game).GetMethod(
				"CreateCompatibleReplayConnection",
				BindingFlags.Static | BindingFlags.NonPublic,
				null,
				new[] { typeof(byte[]), typeof(RuntimeContractIdentity) },
				null);
			Assert.That(snapshotMethod, Is.Not.Null,
				"Game needs one internal byte-snapshot staging seam because ReplayConnection is outside Task 5.");

			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Game.cs"));
			var join = MethodBody(source, "public static void JoinReplay(string replayFile)");
			var profiledCapture = join.IndexOf(
				"File.ReadAllBytes(replayFile)", StringComparison.Ordinal);
			var profiledAdmission = profiledCapture < 0 ? join : join.Substring(profiledCapture);
			Assert.Multiple(() =>
			{
				Assert.That(CountOccurrences(join, "File.ReadAllBytes(replayFile)"), Is.EqualTo(1),
					"The caller-controlled replay path must be captured exactly once.");
				Assert.That(join, Does.Contain("CreateCompatibleReplayConnection("));
				Assert.That(profiledAdmission, Does.Not.Contain("ReplayMetadata.Read(replayFile)"));
				Assert.That(profiledAdmission, Does.Not.Contain("new ReplayConnection(replayFile)"),
					"The profiled branch must use only its captured immutable snapshot.");
			});

			var originalModData = Game.ModData;
			var originalOrderManager = Game.OrderManager;
			try
			{
				Game.ModData = UninitializedReplayModData(BaseIdentity);
				var capturedBytes = ReplayBytes(
					BaseIdentity.ProfileId, BaseIdentity.ContractDigest,
					YuriIdentity.ProfileId, YuriIdentity.ContractDigest, includeLobby: true);
				var replacedPath = Path.Combine(temporaryDirectory, "replaced-after-capture.orarep");
				File.WriteAllBytes(replacedPath, ReplayBytes(
					BaseIdentity.ProfileId, BaseIdentity.ContractDigest,
					BaseIdentity.ProfileId, BaseIdentity.ContractDigest, includeLobby: true));

				var invocation = Assert.Throws<TargetInvocationException>(() =>
					snapshotMethod.Invoke(null, new object[] { capturedBytes, BaseIdentity }));
				AssertRuntimeContractCompatibilityException(
					invocation.InnerException, RuntimeContractCompatibilityReason.ProfileMismatch);
				Assert.That(Game.OrderManager, Is.SameAs(originalOrderManager));
			}
			finally
			{
				Game.ModData = originalModData;
				Game.OrderManager = originalOrderManager;
			}
		}

		[Test]
		[NonParallelizable]
		[Timeout(10000)]
		public void CompatibleReplaySnapshotRemainsReadableUntilExplicitIdempotentRelease()
		{
			var snapshotMethod = typeof(Game).GetMethod(
				"CreateCompatibleReplayConnection",
				BindingFlags.Static | BindingFlags.NonPublic,
				null,
				new[] { typeof(byte[]), typeof(RuntimeContractIdentity) },
				null);
			Assert.That(snapshotMethod, Is.Not.Null);
			var releaseMethod = typeof(Game).GetMethod(
				"ReleaseReplaySnapshot",
				BindingFlags.Static | BindingFlags.NonPublic,
				null,
				new[] { typeof(ReplayConnection) },
				null);

			var originalModData = Game.ModData;
			ReplayConnection connection = null;
			try
			{
				Game.ModData = UninitializedReplayModData(BaseIdentity);
				connection = (ReplayConnection)snapshotMethod.Invoke(null, new object[]
				{
					ReplayBytes(
						BaseIdentity.ProfileId, BaseIdentity.ContractDigest,
						BaseIdentity.ProfileId, BaseIdentity.ContractDigest, includeLobby: true),
					BaseIdentity,
				});

				Assert.Multiple(() =>
				{
					Assert.That(connection.LobbyInfo?.GlobalSettings, Is.Not.Null);
					Assert.That(connection.FinalGameTick, Is.Zero);
					Assert.That(File.Exists(connection.Filename), Is.True,
						"ReplayConnection.Filename must remain a usable path for its published lifetime.");
					Assert.That(releaseMethod, Is.Not.Null,
						"Profiled replay snapshots need one explicit idempotent ownership-release API.");
				});

				Assert.DoesNotThrow(() => releaseMethod.Invoke(null, new object[] { connection }));
				Assert.That(File.Exists(connection.Filename), Is.False,
					"Explicit release must delete the owned profiled snapshot.");
				Assert.DoesNotThrow(() => releaseMethod.Invoke(null, new object[] { connection }),
					"Repeated snapshot release must be inert.");
			}
			finally
			{
				if (connection != null && File.Exists(connection.Filename))
					File.Delete(connection.Filename);

				(connection as IDisposable)?.Dispose();
				Game.ModData = originalModData;
			}
		}

		[Test]
		[NonParallelizable]
		[Timeout(10000)]
		public void FailedReplaySnapshotDeletionRetainsOwnershipForAnIdempotentRetry()
		{
			var snapshotMethod = typeof(Game).GetMethod(
				"CreateCompatibleReplayConnection",
				BindingFlags.Static | BindingFlags.NonPublic,
				null,
				new[] { typeof(byte[]), typeof(RuntimeContractIdentity) },
				null);
			var releaseMethod = typeof(Game).GetMethod(
				"ReleaseReplaySnapshot",
				BindingFlags.Static | BindingFlags.NonPublic,
				null,
				new[] { typeof(ReplayConnection) },
				null);
			Assert.Multiple(() =>
			{
				Assert.That(snapshotMethod, Is.Not.Null);
				Assert.That(releaseMethod, Is.Not.Null);
			});

			var originalModData = Game.ModData;
			ReplayConnection connection = null;
			FieldInfo deleteOverride = null;
			object originalDeleteOverride = null;
			try
			{
				Game.ModData = UninitializedReplayModData(BaseIdentity);
				connection = (ReplayConnection)snapshotMethod.Invoke(null, new object[]
				{
					ReplayBytes(
						BaseIdentity.ProfileId, BaseIdentity.ContractDigest,
						BaseIdentity.ProfileId, BaseIdentity.ContractDigest, includeLobby: true),
					BaseIdentity,
				});
				Assert.That(File.Exists(connection.Filename), Is.True);

				deleteOverride = typeof(Game).GetField(
					"ReplaySnapshotDeleteOverride", BindingFlags.Static | BindingFlags.NonPublic);
				Assert.Multiple(() =>
				{
					Assert.That(deleteOverride, Is.Not.Null,
						"Replay snapshot deletion needs a deterministic internal failure seam.");
					Assert.That(deleteOverride?.FieldType, Is.EqualTo(typeof(Func<string, bool>)));
					Assert.That(deleteOverride?.IsAssembly, Is.True,
						"The deletion seam must remain internal rather than public API.");
				});

				originalDeleteOverride = deleteOverride.GetValue(null);
				var deletionAttempts = 0;
				var allowDelete = false;
				deleteOverride.SetValue(null, (Func<string, bool>)(snapshotPath =>
				{
					deletionAttempts++;
					if (!allowDelete)
						return false;

					File.Delete(snapshotPath);
					return true;
				}));

				releaseMethod.Invoke(null, new object[] { connection });
				Assert.Multiple(() =>
				{
					Assert.That(deletionAttempts, Is.EqualTo(1));
					Assert.That(File.Exists(connection.Filename), Is.True,
						"A transient deletion failure must retain both the file and its ownership record.");
				});

				allowDelete = true;
				releaseMethod.Invoke(null, new object[] { connection });
				Assert.Multiple(() =>
				{
					Assert.That(deletionAttempts, Is.EqualTo(2),
						"A later release must retry a previously failed deletion.");
					Assert.That(File.Exists(connection.Filename), Is.False);
				});

				releaseMethod.Invoke(null, new object[] { connection });
				Assert.That(deletionAttempts, Is.EqualTo(2),
					"Release must become inert only after deletion succeeds.");
			}
			finally
			{
				if (deleteOverride != null)
					deleteOverride.SetValue(null, originalDeleteOverride);

				if (connection != null && File.Exists(connection.Filename))
					File.Delete(connection.Filename);

				(connection as IDisposable)?.Dispose();
				Game.ModData = originalModData;
			}
		}

		[Test]
		[Timeout(10000)]
		public void ReplayRestartCapturesTheOriginalCallerPathBeforeReleasingItsSnapshot()
		{
			var remember = typeof(Game).GetMethod(
				"RememberReplaySourcePath", BindingFlags.Static | BindingFlags.NonPublic,
				null, new[] { typeof(ReplayConnection), typeof(string) }, null);
			var lookup = typeof(Game).GetMethod(
				"ReplaySourcePath", BindingFlags.Static | BindingFlags.NonPublic,
				null, new[] { typeof(ReplayConnection) }, null);
			Assert.Multiple(() =>
			{
				Assert.That(remember, Is.Not.Null,
					"JoinReplay must retain the caller-visible source path independently of the temporary snapshot.");
				Assert.That(lookup, Is.Not.Null);
			});

#pragma warning disable SYSLIB0050
			var replay = (ReplayConnection)FormatterServices.GetUninitializedObject(typeof(ReplayConnection));
#pragma warning restore SYSLIB0050
			var originalPath = Path.Combine(temporaryDirectory, "original.orarep");
			remember.Invoke(null, new object[] { replay, originalPath });
			Assert.That(lookup.Invoke(null, new object[] { replay }), Is.EqualTo(originalPath));

			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Game.cs"));
			var join = MethodBody(source, "public static void JoinReplay(string replayFile)");
			var restart = MethodBody(source, "public static void RestartGame()");
			var rememberSource = join.IndexOf("RememberReplaySourcePath(", StringComparison.Ordinal);
			var publish = join.IndexOf("JoinInner(", StringComparison.Ordinal);
			var lookupSource = restart.IndexOf("ReplaySourcePath(replay)", StringComparison.Ordinal);
			var releaseSnapshot = restart.IndexOf(
				"ReleaseReplaySnapshot(replay)", Math.Max(lookupSource, 0), StringComparison.Ordinal);
			var disconnect = restart.IndexOf("Disconnect()", Math.Max(releaseSnapshot, 0),
				StringComparison.Ordinal);
			Assert.Multiple(() =>
			{
				Assert.That(source, Does.Contain("ConditionalWeakTable<ReplayConnection"));
				Assert.That(rememberSource, Is.GreaterThanOrEqualTo(0));
				Assert.That(rememberSource, Is.LessThan(publish));
				Assert.That(lookupSource, Is.GreaterThanOrEqualTo(0));
				Assert.That(releaseSnapshot, Is.GreaterThan(lookupSource),
					"Restart must capture the original replay path before releasing its snapshot.");
				Assert.That(disconnect, Is.GreaterThan(releaseSnapshot));
			});
		}

		[Test]
		[Timeout(10000)]
		public void ReplaySnapshotOwnershipPathsReleaseBeforeDisposingConnections()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Game.cs"));
			var joinReplay = MethodBody(source, "public static void JoinReplay(string replayFile)");
			var joinInner = MethodBody(source, "static void JoinInner(OrderManager om)");
			var disconnect = MethodBody(source, "public static void Disconnect()");
			var run = MethodBody(source, "static RunStatus Run()");
			var teardown = MethodBody(source,
				"static ExceptionDispatchInfo TeardownCurrentModEpoch()");

			AssertReplayReleasePrecedesDispose(
				joinReplay, "((IDisposable)replayConnection).Dispose()", "JoinReplay failure cleanup");
			AssertReplayReleasePrecedesDispose(
				joinInner, "OrderManager?.Dispose()", "JoinInner replacement");
			AssertReplayReleasePrecedesDispose(
				disconnect, "OrderManager.Dispose()", "Disconnect");
			AssertReplayReleasePrecedesDispose(
				run, "OrderManager?.Dispose()", "legacy Run teardown");
			AssertReplayReleasePrecedesDispose(
				teardown, "Cleanup(ownedOrderManager.Dispose)", "profiled epoch teardown");
		}

		[Test]
		[Timeout(10000)]
		public void ReplaySnapshotExceptionalCleanupUsesFailClosedOrderingAndAggregation()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Game.cs"));
			var disconnect = MethodBody(source, "public static void Disconnect()");
			var release = disconnect.IndexOf("ReleaseReplaySnapshot(", StringComparison.Ordinal);
			var report = disconnect.IndexOf(
				"TraitDict.PrintReport()", Math.Max(release, 0), StringComparison.Ordinal);
			var dispose = disconnect.IndexOf(
				"OrderManager.Dispose()", Math.Max(report, 0), StringComparison.Ordinal);

			var teardown = MethodBody(source,
				"static ExceptionDispatchInfo TeardownCurrentModEpoch()");
			var orderManagerBranch = BracedBlockAt(teardown,
				teardown.IndexOf("if (ownedOrderManager != null)", StringComparison.Ordinal));
			var aggregatedRelease = orderManagerBranch.IndexOf(
				"Cleanup(() => ReleaseReplaySnapshot(", StringComparison.Ordinal);
			var aggregatedDispose = orderManagerBranch.IndexOf(
				"Cleanup(ownedOrderManager.Dispose)", Math.Max(aggregatedRelease, 0),
				StringComparison.Ordinal);

			var delete = MethodBody(source,
				"static bool TryDeleteReplaySnapshot(string snapshotPath)");
			Assert.Multiple(() =>
			{
				Assert.That(release, Is.GreaterThanOrEqualTo(0));
				Assert.That(report, Is.GreaterThan(release),
					"Disconnect must release its replay snapshot before fallible reporting.");
				Assert.That(dispose, Is.GreaterThan(report));
				Assert.That(aggregatedRelease, Is.GreaterThanOrEqualTo(0),
					"Replay snapshot release must participate in teardown's first-failure Cleanup aggregation.");
				Assert.That(aggregatedDispose, Is.GreaterThan(aggregatedRelease));
				Assert.That(delete, Does.Not.Contain("File.Exists("),
					"Deletion success must come from the delete operation, without a racy existence recheck.");
			});
		}

		[Test]
		[NonParallelizable]
		[Timeout(10000)]
		public void CorruptReplayFailuresAreNeverReportedAsRuntimeContractMismatches()
		{
			var originalModData = Game.ModData;
			var originalOrderManager = Game.OrderManager;
			try
			{
				Game.ModData = UninitializedModData(BaseIdentity);
				var path = Path.Combine(temporaryDirectory, "corrupt.orarep");
				File.WriteAllBytes(path, new byte[] { 0x52, 0x41, 0x32 });

				var thrown = Assert.Catch(() => Game.JoinReplay(path));
				Assert.That(IsRuntimeContractCompatibilityException(thrown), Is.False,
					"Generic replay IO/format failures must stay generic.");
				Assert.That(Game.OrderManager, Is.SameAs(originalOrderManager));
			}
			finally
			{
				Game.ModData = originalModData;
				Game.OrderManager = originalOrderManager;
			}
		}

		[Test]
		[Timeout(10000)]
		public void ReplayUiOnlyMapsTypedFinalGateFailuresToRuntimeContractCopy()
		{
			var root = RepositoryRoot();
			var replaySource = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "ReplayBrowserLogic.cs"));
			var menuSource = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "MainMenuLogic.cs"));
			var watch = MethodBody(replaySource, "void WatchReplay()");
			var launch = MethodBody(menuSource, "void LaunchIntoRequestedPanel()");

			AssertTypedReplayCatch(watch, "Game.JoinReplay(selectedReplay.FilePath)");
			AssertTypedReplayCatch(launch, "Game.JoinReplay(argument)");
		}

		[Test]
		[Timeout(10000)]
		public void DisposedReplayBrowserQueuedActionsAreInert()
		{
			var gate = typeof(ReplayBrowserLogic).GetMethod(
				"TryRunPendingReplayAction",
				BindingFlags.Static | BindingFlags.NonPublic,
				null,
				new[] { typeof(bool), typeof(bool), typeof(Action) },
				null);
			Assert.That(gate, Is.Not.Null,
				"Queued replay worker callbacks need a testable disposed/cancelled lifecycle gate.");

			var notifications = 0;
			var listMutations = 0;
			Assert.Multiple(() =>
			{
				Assert.That((bool)gate.Invoke(null, new object[]
				{
					true, false, (Action)(() => notifications++),
				}), Is.False);
				Assert.That((bool)gate.Invoke(null, new object[]
				{
					true, false, (Action)(() => listMutations++),
				}), Is.False);
				Assert.That((bool)gate.Invoke(null, new object[]
				{
					false, true, (Action)(() => listMutations++),
				}), Is.False);
			});
			Assert.Multiple(() =>
			{
				Assert.That(notifications, Is.Zero);
				Assert.That(listMutations, Is.Zero);
				Assert.That((bool)gate.Invoke(null, new object[]
				{
					false, false, (Action)(() => listMutations++),
				}), Is.True);
				Assert.That(listMutations, Is.EqualTo(1));
			});

			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Widgets", "Logic",
				"ReplayBrowserLogic.cs"));
			var constructor = MethodBody(source, "public ReplayBrowserLogic(");
			var dispose = MethodBody(source, "protected override void Dispose(bool disposing)");
			Assert.Multiple(() =>
			{
				Assert.That(CountOccurrences(constructor, "TryRunPendingReplayAction("),
					Is.GreaterThanOrEqualTo(2),
					"Both hidden-notification and list-update callbacks must recheck lifecycle state.");
				Assert.That(dispose, Does.Contain("cancelLoadingReplays = true"));
			});
		}

		[Test]
		[Timeout(10000)]
		public void ServerTryReadCompatibleGameSaveFailsClosedForFilesAndTheFiveIdentityStates()
		{
			var saveDirectory = Path.Combine(temporaryDirectory, "Saves");
			Directory.CreateDirectory(saveDirectory);
			File.WriteAllText(Path.Combine(saveDirectory, "corrupt.orasav"), "not an orasav");

			WriteGameSave(saveDirectory, "legacy.orasav", null, null);
			WriteGameSave(saveDirectory, "malformed.orasav", BaseIdentity.ProfileId, null);
			WriteGameSave(saveDirectory, "yuri.orasav",
				YuriIdentity.ProfileId, YuriIdentity.ContractDigest);
			WriteGameSave(saveDirectory, "other-digest.orasav",
				BaseOtherDigest.ProfileId, BaseOtherDigest.ContractDigest);
			WriteGameSave(saveDirectory, "compatible.orasav",
				BaseIdentity.ProfileId, BaseIdentity.ContractDigest);

			AssertReadRejected(saveDirectory, null, BaseIdentity,
				RuntimeContractCompatibilityReason.MalformedIdentity);
			AssertReadRejected(saveDirectory, "../escape.orasav", BaseIdentity,
				RuntimeContractCompatibilityReason.MalformedIdentity);
			AssertReadRejected(saveDirectory, "missing.orasav", BaseIdentity,
				RuntimeContractCompatibilityReason.MalformedIdentity);
			AssertReadRejected(saveDirectory, "corrupt.orasav", BaseIdentity,
				RuntimeContractCompatibilityReason.MalformedIdentity);
			AssertReadRejected(saveDirectory, "legacy.orasav", BaseIdentity,
				RuntimeContractCompatibilityReason.MissingIdentity);
			AssertReadRejected(saveDirectory, "malformed.orasav", BaseIdentity,
				RuntimeContractCompatibilityReason.MalformedIdentity);
			AssertReadRejected(saveDirectory, "yuri.orasav", BaseIdentity,
				RuntimeContractCompatibilityReason.ProfileMismatch);
			AssertReadRejected(saveDirectory, "other-digest.orasav", BaseIdentity,
				RuntimeContractCompatibilityReason.ContractMismatch);

			var accepted = false;
			GameSave loaded = null;
			var reason = RuntimeContractCompatibilityReason.MalformedIdentity;
			Assert.DoesNotThrow(() => accepted = GameServer.TryReadCompatibleGameSave(
				saveDirectory, "compatible.orasav", BaseIdentity, out loaded, out reason));
			Assert.Multiple(() =>
			{
				Assert.That(accepted, Is.True);
				Assert.That(loaded, Is.Not.Null);
				Assert.That(reason, Is.EqualTo(RuntimeContractCompatibilityReason.Compatible));
				Assert.That(Enum.GetValues(typeof(RuntimeContractCompatibilityReason)), Has.Length.EqualTo(5),
					"File admission reasons must stay in the closed compatibility enum.");
			});
		}

		[Test]
		[Timeout(10000)]
		public void ServerGameSaveClientStagingFailuresNeverMutatePublishedState()
		{
			var existingSave = GameSaveWithIdentity(
				BaseIdentity.ProfileId, BaseIdentity.ContractDigest);
			var global = new Session.Global { Map = "current-map" };
			var slots = new Dictionary<string, Session.Slot>
			{
				["current-slot"] = new Session.Slot { PlayerReference = "current-slot" },
			};
			var human = CurrentHuman(isAdmin: true);
			var oldBot = CurrentBot();
			var server = UninitializedServer(existingSave, global, slots, human, oldBot);
			var currentClients = server.LobbyInfo.Clients;
			var snapshots = currentClients.Select(ClientSnapshot).ToArray();

			var insufficientHumans = StagingSave(
				SavedHuman("human-slot-a"), SavedHuman("human-slot-b"));
			AssertStageRejected(insufficientHumans, currentClients, 10);
			AssertPublishedStateUnchanged(server, existingSave, global, slots,
				currentClients, human, oldBot, snapshots);

			var noAdminClients = new List<Session.Client>
			{
				CurrentHuman(isAdmin: false), CurrentBot(),
			};
			var noAdminSnapshots = noAdminClients.Select(ClientSnapshot).ToArray();
			AssertStageRejected(StagingSave(SavedHuman("human-slot")), noAdminClients, 10);
			AssertClientsUnchanged(noAdminClients, noAdminSnapshots);

			var badSlot = StagingSave();
			SetGameSaveSlotClients(badSlot, new Dictionary<string, SlotClient>
			{
				["bad-key"] = new SlotClient(SavedHuman(null)),
			});
			AssertStageRejected(badSlot, currentClients, 10);
			AssertPublishedStateUnchanged(server, existingSave, global, slots,
				currentClients, human, oldBot, snapshots);
		}

		[Test]
		[Timeout(10000)]
		public void ServerGameSaveClientStagingDeepClonesHumansAndAllocatesBotsFromTheGivenIndex()
		{
			var human = CurrentHuman(isAdmin: true);
			var oldBot = CurrentBot();
			var currentClients = new List<Session.Client> { human, oldBot };
			var snapshots = currentClients.Select(ClientSnapshot).ToArray();
			var save = StagingSave(
				SavedHuman("human-slot"), SavedBot("bot-slot", "easy", "Saved Bot"));

			var accepted = GameServer.TryStageGameSaveClients(
				save, currentClients, 10, out var stagedClients, out var stagedNextPlayerIndex);

			var stagedHuman = stagedClients?.SingleOrDefault(client => client.Bot == null);
			var stagedBot = stagedClients?.SingleOrDefault(client => client.Bot != null);
			Assert.Multiple(() =>
			{
				Assert.That(accepted, Is.True);
				Assert.That(stagedClients, Is.Not.Null.And.Not.SameAs(currentClients));
				Assert.That(stagedClients, Has.Count.EqualTo(2));
				Assert.That(stagedHuman, Is.Not.Null.And.Not.SameAs(human));
				Assert.That(stagedHuman.Index, Is.EqualTo(human.Index));
				Assert.That(stagedHuman.Name, Is.EqualTo(human.Name));
				Assert.That(stagedHuman.IPAddress, Is.EqualTo(human.IPAddress),
					"Deep cloning must preserve fields ignored by lobby serialization.");
				Assert.That(stagedHuman.Slot, Is.EqualTo("human-slot"));
				Assert.That(stagedHuman.Faction, Is.EqualTo("Allies"));
				Assert.That(stagedBot, Is.Not.Null.And.Not.SameAs(oldBot));
				Assert.That(stagedBot.Index, Is.EqualTo(10));
				Assert.That(stagedBot.Bot, Is.EqualTo("easy"));
				Assert.That(stagedBot.Name, Is.EqualTo("Saved Bot"));
				Assert.That(stagedBot.Slot, Is.EqualTo("bot-slot"));
				Assert.That(stagedBot.BotControllerClientIndex, Is.EqualTo(human.Index));
				Assert.That(stagedNextPlayerIndex, Is.EqualTo(11));
			});

			AssertClientsUnchanged(currentClients, snapshots);
			Assert.That(currentClients[0], Is.SameAs(human));
			Assert.That(currentClients[1], Is.SameAs(oldBot));
		}

		[Test]
		[Timeout(10000)]
		public void ServerGameSaveClientStagingRejectsMissingOrInconsistentSlots()
		{
			var currentClients = new List<Session.Client> { CurrentHuman(isAdmin: true) };

			var nullSlots = StagingSave(SavedHuman("human-slot"));
			SetGameSaveSlots(nullSlots, null);
			AssertStageRejected(nullSlots, currentClients, 10);

			var emptySlots = StagingSave(SavedHuman("human-slot"));
			emptySlots.Slots.Clear();
			AssertStageRejected(emptySlots, currentClients, 10);

			var nullSlot = StagingSave(SavedHuman("human-slot"));
			nullSlot.Slots["human-slot"] = null;
			AssertStageRejected(nullSlot, currentClients, 10);

			var ghostMembership = StagingSave(SavedHuman("human-slot"));
			ghostMembership.Slots.Clear();
			ghostMembership.Slots["different-slot"] = new Session.Slot
			{
				PlayerReference = "different-slot",
			};
			AssertStageRejected(ghostMembership, currentClients, 10);
		}

		[Test]
		[Timeout(10000)]
		public void ServerGameSaveClientStagingPropagatesUnexpectedCollectionFailures()
		{
			var save = StagingSave(SavedHuman("human-slot"));
			foreach (var failure in new Exception[]
			{
				new InvalidOperationException("unexpected collection failure"),
				new OutOfMemoryException("fatal collection failure"),
			})
			{
				var currentClients = new ThrowingReadOnlyList<Session.Client>(failure);
				List<Session.Client> stagedClients = new() { CurrentHuman(isAdmin: true) };
				var stagedNextPlayerIndex = -1;

				var thrown = Assert.Throws(failure.GetType(), () =>
					GameServer.TryStageGameSaveClients(
						save, currentClients, 10,
						out stagedClients, out stagedNextPlayerIndex));

				Assert.Multiple(() =>
				{
					Assert.That(thrown, Is.SameAs(failure));
					Assert.That(stagedClients, Is.Null);
					Assert.That(stagedNextPlayerIndex, Is.EqualTo(10));
				});
			}
		}

		[Test]
		[Timeout(10000)]
		public void ServerGameSaveReadClosesExpectedFileFailuresWithoutCatchingFatalExceptions()
		{
			var saveDirectory = Path.Combine(temporaryDirectory, "ReadFailures");
			Directory.CreateDirectory(saveDirectory);
			File.WriteAllText(Path.Combine(saveDirectory, "corrupt.orasav"), "not an orasav");

			AssertReadRejected(saveDirectory, "missing.orasav", BaseIdentity,
				RuntimeContractCompatibilityReason.MalformedIdentity);
			AssertReadRejected(saveDirectory, "corrupt.orasav", BaseIdentity,
				RuntimeContractCompatibilityReason.MalformedIdentity);

			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Server", "Server.cs"));
			var read = MethodBody(source, "internal static bool TryReadCompatibleGameSave(");
			Assert.That(HasUnfilteredCatchAll(read), Is.False,
				"Expected file/format failures may close admission, but fatal exceptions must propagate.");
		}

		[Test]
		[Timeout(10000)]
		public void ServerGameSaveReadRejectsAnOversizedLengthPrefixWithoutThrowing()
		{
			var saveDirectory = Path.Combine(temporaryDirectory, "OversizedRead");
			Directory.CreateDirectory(saveDirectory);
			const string Filename = "oversized.orasav";
			var path = Path.Combine(saveDirectory, Filename);
			WriteGameSave(saveDirectory, Filename,
				BaseIdentity.ProfileId, BaseIdentity.ContractDigest);

			using (var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
			{
				stream.Seek(-12, SeekOrigin.End);
				var metadataOffset = stream.ReadInt32();
				var traitDataOffset = stream.ReadInt32();
				var endMarker = stream.ReadInt32();
				Assert.Multiple(() =>
				{
					Assert.That(metadataOffset, Is.GreaterThanOrEqualTo(0));
					Assert.That(traitDataOffset, Is.GreaterThan(metadataOffset));
					Assert.That(traitDataOffset, Is.LessThan(stream.Length - 12));
					Assert.That(endMarker, Is.EqualTo(GameSave.EOFMarker));
				});

				var firstLengthPrefix = metadataOffset + 3 * sizeof(int) + Order.SyncHashOrderLength;
				stream.Position = firstLengthPrefix;
				using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
				writer.Write(OpenRA.Server.Connection.MaxOrderLength + 1);
				writer.Flush();
			}

			var accepted = true;
			GameSave loaded = new();
			var compatibility = RuntimeContractCompatibilityReason.Compatible;
			Assert.DoesNotThrow(() => accepted = GameServer.TryReadCompatibleGameSave(
				saveDirectory, Filename, BaseIdentity, out loaded, out compatibility));
			Assert.Multiple(() =>
			{
				Assert.That(accepted, Is.False);
				Assert.That(loaded, Is.Null);
				Assert.That(compatibility,
					Is.EqualTo(RuntimeContractCompatibilityReason.MalformedIdentity));
			});
		}

		[Test]
		[Timeout(10000)]
		public void ServerGameSaveReadRejectsAnEmptyGlobalYamlWithoutThrowing()
		{
			var saveDirectory = Path.Combine(temporaryDirectory, "EmptyGlobalYamlRead");
			Directory.CreateDirectory(saveDirectory);
			const string Filename = "empty-global-yaml.orasav";
			var path = Path.Combine(saveDirectory, Filename);
			WriteGameSave(saveDirectory, Filename,
				BaseIdentity.ProfileId, BaseIdentity.ContractDigest);

			using (var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
			{
				stream.Seek(-12, SeekOrigin.End);
				var metadataOffset = stream.ReadInt32();
				var traitDataOffset = stream.ReadInt32();
				var endMarker = stream.ReadInt32();
				Assert.Multiple(() =>
				{
					Assert.That(metadataOffset, Is.GreaterThanOrEqualTo(0));
					Assert.That(traitDataOffset, Is.GreaterThan(metadataOffset));
					Assert.That(traitDataOffset, Is.LessThan(stream.Length - 12));
					Assert.That(endMarker, Is.EqualTo(GameSave.EOFMarker));
				});

				var globalYamlLengthPrefix = metadataOffset +
					3 * sizeof(int) + Order.SyncHashOrderLength;
				stream.Position = globalYamlLengthPrefix;
				using var writer = new BinaryWriter(
					stream, System.Text.Encoding.UTF8, leaveOpen: true);
				writer.Write(0);
				writer.Flush();
			}

			var parserFailure = Assert.Catch(() => _ = new GameSave(path));
			AssertReadRejected(saveDirectory, Filename, BaseIdentity,
				RuntimeContractCompatibilityReason.MalformedIdentity);
			Assert.That(parserFailure, Is.TypeOf<InvalidDataException>(),
				"The parser must classify a missing GlobalSettings node as malformed data, " +
				"instead of leaking a collection indexing exception to its caller.");
		}

		[Test]
		[Timeout(10000)]
		public void ServerGameSaveWriteRejectsInvalidFilenamesWithoutThrowing()
		{
			var accepted = true;
			Assert.DoesNotThrow(() => accepted = InvokeTryWriteGameSave(
				GameSaveWithIdentity(BaseIdentity.ProfileId, BaseIdentity.ContractDigest),
				Path.Combine(temporaryDirectory, "InvalidWrite"), "../escape.orasav"));
			Assert.That(accepted, Is.False);
		}

		[Test]
		[Timeout(10000)]
		public void ServerGameSaveWriteFailurePreservesTheDestinationAndCleansItsTemporaryFile()
		{
			var saveDirectory = Path.Combine(temporaryDirectory, "AtomicWrite");
			Directory.CreateDirectory(saveDirectory);
			var destination = Path.Combine(saveDirectory, "existing.orasav");
			var originalBytes = new byte[] { 0x52, 0x41, 0x32, 0x2d, 0x53, 0x41, 0x56, 0x45 };
			File.WriteAllBytes(destination, originalBytes);

			var failingSave = GameSaveWithIdentity(
				BaseIdentity.ProfileId, BaseIdentity.ContractDigest);
			SetGameSaveSlotClients(failingSave, new Dictionary<string, SlotClient>());
			SetGameSaveLastSyncPacket(failingSave, Array.Empty<byte>());

			var accepted = true;
			Assert.DoesNotThrow(() => accepted = InvokeTryWriteGameSave(
				failingSave, saveDirectory, Path.GetFileName(destination)));

			Assert.Multiple(() =>
			{
				Assert.That(accepted, Is.False);
				Assert.That(File.ReadAllBytes(destination), Is.EqualTo(originalBytes),
					"A failed serialization must not truncate the last known-good save.");
				Assert.That(Directory.GetFiles(saveDirectory, "*", SearchOption.TopDirectoryOnly),
					Is.EquivalentTo(new[] { destination }),
					"Failed writes must not leave a sibling temporary file behind.");
			});
		}

		[Test]
		[Timeout(10000)]
		public void ServerGameSaveWriteCreatesAndAtomicallyOverwritesValidSaves()
		{
			var saveDirectory = Path.Combine(temporaryDirectory, "SuccessfulAtomicWrite");
			Directory.CreateDirectory(saveDirectory);
			var save = GameSaveWithIdentity(
				BaseIdentity.ProfileId, BaseIdentity.ContractDigest);
			SetGameSaveSlotClients(save, new Dictionary<string, SlotClient>());
			SetGameSaveLastSyncPacket(save);

			const string NewFilename = "new.orasav";
			Assert.That(InvokeTryWriteGameSave(save, saveDirectory, NewFilename), Is.True);
			var newPath = Path.Combine(saveDirectory, NewFilename);
			Assert.That(new GameSave(newPath).RuntimeContractCompatibilityWith(BaseIdentity),
				Is.EqualTo(RuntimeContractCompatibilityReason.Compatible));

			const string ExistingFilename = "existing.orasav";
			var existingPath = Path.Combine(saveDirectory, ExistingFilename);
			var oldBytes = new byte[] { 0x52, 0x41, 0x32, 0x2d, 0x4f, 0x4c, 0x44 };
			File.WriteAllBytes(existingPath, oldBytes);
			Assert.That(InvokeTryWriteGameSave(save, saveDirectory, ExistingFilename), Is.True);

			Assert.Multiple(() =>
			{
				Assert.That(File.ReadAllBytes(existingPath), Is.Not.EqualTo(oldBytes));
				Assert.That(new GameSave(existingPath)
					.RuntimeContractCompatibilityWith(BaseIdentity),
					Is.EqualTo(RuntimeContractCompatibilityReason.Compatible));
				Assert.That(Directory.GetFiles(
					saveDirectory, ".openra-gamesave-*", SearchOption.TopDirectoryOnly), Is.Empty,
					"Successful publication must not leave a sibling staging file.");
			});
		}

		[Test]
		[Timeout(10000)]
		public void GameSaveRestoresTheOrdersStreamPositionWhenCopyingOrdersFails()
		{
			var failure = new IOException("injected orders copy failure");
			var orders = new ThrowingCopyMemoryStream(
				new byte[] { 0x52, 0x41, 0x32, 0x2d }, failure)
			{
				Position = 3,
			};
			var save = new GameSave();
			SetInstanceField(save, "ordersStream", orders);
			var destination = Path.Combine(temporaryDirectory, "copy-failure.orasav");

			var thrown = Assert.Throws<IOException>(() => save.Save(destination));
			Assert.Multiple(() =>
			{
				Assert.That(thrown, Is.SameAs(failure));
				Assert.That(File.ReadAllBytes(destination),
					Is.EqualTo(new byte[] { 0x52 }),
					"The injected stream must fail only after a real partial CopyTo write.");
				Assert.That(orders.Position, Is.EqualTo(3),
					"Save must restore the caller's orders stream position after CopyTo fails.");
			});
		}

		[Test]
		[Timeout(10000)]
		public void ServerGameSaveWriteStagesBeforeAtomicPublication()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Server", "Server.cs"));
			var write = MethodBody(source, "static bool TryWriteGameSave(");
			var stagedSave = write.IndexOf("gameSave.Save(", StringComparison.Ordinal);
			var move = write.IndexOf("File.Move(", Math.Max(stagedSave, 0), StringComparison.Ordinal);
			var replace = write.IndexOf("File.Replace(", Math.Max(stagedSave, 0), StringComparison.Ordinal);
			var atomicPublication = Math.Max(move, replace);

			Assert.Multiple(() =>
			{
				Assert.That(write, Does.Not.Contain(
					"gameSave.Save(Path.Combine(directory, canonicalFilename))"),
					"Serialization must never open the published destination directly.");
				Assert.That(stagedSave, Is.GreaterThanOrEqualTo(0));
				Assert.That(atomicPublication, Is.GreaterThan(stagedSave),
					"A completed sibling temporary file must be atomically published.");
			});
		}

		[Test]
		[Timeout(10000)]
		public void ServerGameSaveHandlersFailClosedAndCommitOnlyAfterReadAndStagingSucceed()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Server", "Server.cs"));
			var create = MethodBody(source, "case \"CreateGameSave\":");
			var load = MethodBody(source, "case \"LoadGameSave\":");
			var staging = MethodBody(source, "internal static bool TryStageGameSaveClients(");

			var read = load.IndexOf("TryReadCompatibleGameSave(", StringComparison.Ordinal);
			var requester = load.IndexOf("GetClient(conn)", StringComparison.Ordinal);
			var adminGate = load.IndexOf("IsAdmin", Math.Max(requester, 0), StringComparison.Ordinal);
			var stage = load.IndexOf("TryStageGameSaveClients(", Math.Max(read, 0),
				StringComparison.Ordinal);
			var publishSave = load.IndexOf("GameSave = loadedGameSave", StringComparison.Ordinal);
			var publishGlobal = load.IndexOf(
				"LobbyInfo.GlobalSettings = loadedGameSave.GlobalSettings", StringComparison.Ordinal);
			var publishSlots = load.IndexOf(
				"LobbyInfo.Slots = loadedGameSave.Slots", StringComparison.Ordinal);
			var publishClients = load.IndexOf(
				"LobbyInfo.Clients = stagedClients", StringComparison.Ordinal);
			var publishNextPlayerIndex = load.IndexOf(
				"nextPlayerIndex = stagedNextPlayerIndex", StringComparison.Ordinal);
			var syncLobby = load.IndexOf("SyncLobbyInfo()", StringComparison.Ordinal);
			var syncClients = load.IndexOf("SyncLobbyClients()", StringComparison.Ordinal);

			Assert.Multiple(() =>
			{
				Assert.That(create, Does.Not.Contain("throw new "));
				Assert.That(load, Does.Not.Contain("throw new "));
				Assert.That(requester, Is.GreaterThanOrEqualTo(0));
				Assert.That(adminGate, Is.GreaterThan(requester));
				Assert.That(read, Is.GreaterThan(adminGate),
					"Only the requesting admin may touch a local save file.");
				Assert.That(stage, Is.GreaterThan(read));
				Assert.That(publishSave, Is.GreaterThan(stage));
				Assert.That(publishGlobal, Is.GreaterThan(publishSave));
				Assert.That(publishSlots, Is.GreaterThan(publishGlobal));
				Assert.That(publishClients, Is.GreaterThan(publishSlots));
				Assert.That(publishNextPlayerIndex, Is.GreaterThan(publishClients));
				Assert.That(syncLobby, Is.GreaterThan(publishNextPlayerIndex));
				Assert.That(syncClients, Is.GreaterThan(publishNextPlayerIndex));
				Assert.That(load, Does.Not.Contain("new GameSave("));
				Assert.That(load, Does.Not.Contain("LobbyInfo.Clients.Remove"));
				Assert.That(load, Does.Not.Contain("ChooseFreePlayerIndex()"));
				Assert.That(staging, Does.Not.Contain("LobbyInfo"),
					"Staging must remain pure and unable to publish partial server state.");
				Assert.That(staging, Does.Not.Contain(".Remove(c)"),
					"A staged list must never be mutated by removing inside its foreach.");
			});
		}

		[Test]
		[Timeout(10000)]
		public void LegacyTwoArgumentSettingsKeepAllSectionsInTheGlobalFile()
		{
			var globalFile = Path.Combine(temporaryDirectory, "settings.yaml");
			File.WriteAllText(globalFile,
				"Player:\n" +
				"\tName: Legacy Player\n" +
				"\tFuturePlayerSetting: keep-player\n" +
				"Server:\n" +
				"\tName: Legacy Server\n" +
				"\tFutureServerSetting: keep-server\n" +
				"FutureGlobalSection:\n" +
				"\tValue: keep-global\n");

			var settings = new Settings(globalFile, new Arguments());
			Assert.Multiple(() =>
			{
				Assert.That(settings.Player.Name, Is.EqualTo("Legacy Player"));
				Assert.That(settings.Server.Name, Is.EqualTo("Legacy Server"));
			});

			settings.Player.Name = "Saved Player";
			settings.Server.Name = "Saved Server";
			settings.Save();

			Assert.Multiple(() =>
			{
				Assert.That(YamlValue(globalFile, "Player", "Name"), Is.EqualTo("Saved Player"));
				Assert.That(YamlValue(globalFile, "Server", "Name"), Is.EqualTo("Saved Server"));
				Assert.That(YamlValue(globalFile, "Player", "FuturePlayerSetting"),
					Is.EqualTo("keep-player"));
				Assert.That(YamlValue(globalFile, "Server", "FutureServerSetting"),
					Is.EqualTo("keep-server"));
				Assert.That(YamlValue(globalFile, "FutureGlobalSection", "Value"),
					Is.EqualTo("keep-global"));
			});
		}

		[Test]
		[Timeout(10000)]
		public void ProfiledSettingsKeepGlobalStateAndLegacyServerUntouchedWhileSavingServerSeparately()
		{
			var globalFile = Path.Combine(temporaryDirectory, "settings.yaml");
			var serverFile = RuntimeContractStorage.ServerSettingsPath(
				temporaryDirectory, ModId, BaseIdentity);
			Directory.CreateDirectory(Path.GetDirectoryName(serverFile));
			File.WriteAllText(globalFile,
				"Player:\n" +
				"\tName: Shared Player\n" +
				"\tFuturePlayerSetting: keep-player\n" +
				"Server:\n" +
				"\tName: Legacy Server\n" +
				"\tFutureLegacyServerSetting: keep-legacy\n" +
				"FutureGlobalSection:\n" +
				"\tValue: keep-global\n");
			File.WriteAllText(serverFile,
				"Server:\n" +
				"\tName: Base Server\n" +
				"\tFutureServerSetting: keep-profile\n" +
				"FutureServerSection:\n" +
				"\tValue: keep-server-file\n");

			var settings = new Settings(globalFile, serverFile, new Arguments());
			Assert.Multiple(() =>
			{
				Assert.That(settings.Player.Name, Is.EqualTo("Shared Player"));
				Assert.That(settings.Server.Name, Is.EqualTo("Base Server"));
			});

			settings.Player.Name = "Shared After Save";
			settings.Server.Name = "Base After Save";
			settings.Save();

			Assert.Multiple(() =>
			{
				Assert.That(YamlValue(globalFile, "Player", "Name"),
					Is.EqualTo("Shared After Save"));
				Assert.That(YamlValue(globalFile, "Server", "Name"),
					Is.EqualTo("Legacy Server"));
				Assert.That(YamlValue(globalFile, "Server", "FutureLegacyServerSetting"),
					Is.EqualTo("keep-legacy"));
				Assert.That(YamlValue(globalFile, "FutureGlobalSection", "Value"),
					Is.EqualTo("keep-global"));
				Assert.That(YamlValue(serverFile, "Server", "Name"),
					Is.EqualTo("Base After Save"));
				Assert.That(YamlValue(serverFile, "Server", "FutureServerSetting"),
					Is.EqualTo("keep-profile"));
				Assert.That(YamlValue(serverFile, "FutureServerSection", "Value"),
					Is.EqualTo("keep-server-file"));
				Assert.That(MiniYaml.FromFile(serverFile, false).Select(node => node.Key),
					Is.EquivalentTo(new[] { "Server", "FutureServerSection" }));
			});
		}

		[Test]
		[Timeout(10000)]
		public void ProfiledSettingsApplyCommandLineOverridesToTheirOwningFiles()
		{
			var globalFile = Path.Combine(temporaryDirectory, "settings.yaml");
			var serverFile = RuntimeContractStorage.ServerSettingsPath(
				temporaryDirectory, ModId, BaseIdentity);
			Directory.CreateDirectory(Path.GetDirectoryName(serverFile));
			File.WriteAllText(globalFile,
				"Player:\n\tName: Stored Player\n" +
				"Server:\n\tName: Legacy Server\n");
			File.WriteAllText(serverFile, "Server:\n\tName: Stored Base Server\n");

			var settings = new Settings(globalFile, serverFile, new Arguments(
				"Player.Name=CLI Player",
				"Server.Name=CLI Base Server"));

			Assert.Multiple(() =>
			{
				Assert.That(settings.Player.Name, Is.EqualTo("CLI Player"));
				Assert.That(settings.Server.Name, Is.EqualTo("CLI Base Server"));
			});

			settings.Save();
			Assert.Multiple(() =>
			{
				Assert.That(YamlValue(globalFile, "Player", "Name"), Is.EqualTo("CLI Player"));
				Assert.That(YamlValue(globalFile, "Server", "Name"), Is.EqualTo("Legacy Server"));
				Assert.That(YamlValue(serverFile, "Server", "Name"),
					Is.EqualTo("CLI Base Server"));
			});
		}

		[Test]
		[Timeout(10000)]
		public void ProfiledSettingsCreateTheServerParentDirectoryOnSave()
		{
			var globalFile = Path.Combine(temporaryDirectory, "settings.yaml");
			var serverFile = RuntimeContractStorage.ServerSettingsPath(
				temporaryDirectory, ModId, BaseIdentity);
			var serverParent = Path.GetDirectoryName(serverFile);
			Assert.That(Directory.Exists(serverParent), Is.False);

			var settings = new Settings(globalFile, serverFile, new Arguments());
			settings.Server.Name = "Created Parent Server";
			settings.Save();

			Assert.Multiple(() =>
			{
				Assert.That(Directory.Exists(serverParent), Is.True);
				Assert.That(File.Exists(serverFile), Is.True);
				Assert.That(YamlValue(serverFile, "Server", "Name"),
					Is.EqualTo("Created Parent Server"));
			});
		}

		[Test]
		[Timeout(10000)]
		public void BaseAndYuriServerSettingsAreIsolatedWhileGlobalSettingsRemainShared()
		{
			var globalFile = Path.Combine(temporaryDirectory, "settings.yaml");
			var baseFile = RuntimeContractStorage.ServerSettingsPath(
				temporaryDirectory, ModId, BaseIdentity);
			var yuriFile = RuntimeContractStorage.ServerSettingsPath(
				temporaryDirectory, ModId, YuriIdentity);

			var baseSettings = new Settings(globalFile, baseFile, new Arguments());
			baseSettings.Player.Name = "Shared Commander";
			baseSettings.Server.Name = "Base Server";
			baseSettings.Save();

			var yuriSettings = new Settings(globalFile, yuriFile, new Arguments());
			Assert.Multiple(() =>
			{
				Assert.That(yuriSettings.Player.Name, Is.EqualTo("Shared Commander"));
				Assert.That(yuriSettings.Server.Name, Is.EqualTo(new ServerSettings().Name));
			});
			yuriSettings.Server.Name = "Yuri Server";
			yuriSettings.Save();

			var reloadedBase = new Settings(globalFile, baseFile, new Arguments());
			var reloadedYuri = new Settings(globalFile, yuriFile, new Arguments());
			Assert.Multiple(() =>
			{
				Assert.That(reloadedBase.Player.Name, Is.EqualTo("Shared Commander"));
				Assert.That(reloadedYuri.Player.Name, Is.EqualTo("Shared Commander"));
				Assert.That(reloadedBase.Server.Name, Is.EqualTo("Base Server"));
				Assert.That(reloadedYuri.Server.Name, Is.EqualTo("Yuri Server"));
				Assert.That(baseFile, Is.Not.EqualTo(yuriFile));
			});
		}

		[TestCase(null)]
		[TestCase("")]
		[Timeout(10000)]
		public void ProfiledSettingsRejectMissingServerFile(string serverFile)
		{
			var globalFile = Path.Combine(temporaryDirectory, "settings.yaml");
			Assert.That(() => new Settings(globalFile, serverFile, new Arguments()),
				Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		[Timeout(10000)]
		public void ProfiledSettingsRejectUsingTheGlobalFileAsTheServerFile()
		{
			var globalFile = Path.Combine(temporaryDirectory, "settings.yaml");
			Assert.That(() => new Settings(globalFile, globalFile, new Arguments()),
				Throws.ArgumentException);
		}

		[Test]
		[Timeout(10000)]
		public void GameSaveBrowserUsesTheCurrentContractRootAndRevalidatesBeforeListingOrLoading()
		{
			var root = RepositoryRoot();
			var source = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "GameSaveBrowserLogic.cs"));
			var constructor = MethodBody(source, "public GameSaveBrowserLogic(");
			var loadGames = MethodBody(source, "void LoadGames(");
			var load = MethodBody(source, "void Load()");
			var save = MethodBody(source, "void Save(World world)");
			var loadPanelEnabled = MethodBody(source,
				"public static bool IsLoadPanelEnabled(Manifest mod)");

			var constructorRoot = constructor.IndexOf(
				"RuntimeContractStorage.SavesPath(", StringComparison.Ordinal);
			var constructorIdentity = constructor.IndexOf(
				"modData.RuntimeContract", Math.Max(constructorRoot, 0), StringComparison.Ordinal);

			var listParse = loadGames.IndexOf("new GameSave(savePath)", StringComparison.Ordinal);
			var listCompatibility = CompatibilityIndex(loadGames, listParse);
			var listAdmit = loadGames.IndexOf(
				"RuntimeContractCompatibilityReason.Compatible",
				Math.Max(listCompatibility, 0), StringComparison.Ordinal);
			var listAdd = loadGames.IndexOf("games.Add(savePath)", StringComparison.Ordinal);

			var loadParse = load.IndexOf("new GameSave(selectedSave)", StringComparison.Ordinal);
			var loadCompatibility = CompatibilityIndex(load, loadParse);
			var loadAdmit = load.IndexOf(
				"RuntimeContractCompatibilityReason.Compatible",
				Math.Max(loadCompatibility, 0), StringComparison.Ordinal);
			var loadMap = load.IndexOf("modData.MapCache", Math.Max(loadAdmit, 0),
				StringComparison.Ordinal);
			var loadServer = load.IndexOf("Game.CreateAndStartLocalServer(",
				Math.Max(loadMap, 0), StringComparison.Ordinal);

			var panelRoot = loadPanelEnabled.IndexOf(
				"RuntimeContractStorage.SavesPath(", StringComparison.Ordinal);
			var panelParse = loadPanelEnabled.IndexOf("new GameSave(",
				Math.Max(panelRoot, 0), StringComparison.Ordinal);
			var panelCompatibility = CompatibilityIndex(loadPanelEnabled, panelParse);
			var panelAdmit = loadPanelEnabled.IndexOf(
				"RuntimeContractCompatibilityReason.Compatible",
				Math.Max(panelCompatibility, 0), StringComparison.Ordinal);
			var panelReturn = loadPanelEnabled.IndexOf("return true", Math.Max(panelAdmit, 0),
				StringComparison.Ordinal);

			var preservedSignature = typeof(GameSaveBrowserLogic).GetMethod(
				nameof(GameSaveBrowserLogic.IsLoadPanelEnabled),
				BindingFlags.Public | BindingFlags.Static,
				null, new[] { typeof(Manifest) }, null);

			Assert.Multiple(() =>
			{
				Assert.That(constructorRoot, Is.GreaterThanOrEqualTo(0));
				Assert.That(constructorIdentity, Is.GreaterThan(constructorRoot));
				Assert.That(constructor, Does.Not.Contain("\"Saves\""),
					"The browser must not rebuild the legacy save root beside the central API.");
				Assert.That(listParse, Is.GreaterThanOrEqualTo(0));
				Assert.That(listCompatibility, Is.GreaterThan(listParse));
				Assert.That(listAdmit, Is.GreaterThan(listCompatibility));
				Assert.That(listAdd, Is.GreaterThan(listAdmit));
				Assert.That(loadParse, Is.GreaterThanOrEqualTo(0));
				Assert.That(loadCompatibility, Is.GreaterThan(loadParse));
				Assert.That(loadAdmit, Is.GreaterThan(loadCompatibility));
				Assert.That(loadMap, Is.GreaterThan(loadAdmit));
				Assert.That(loadServer, Is.GreaterThan(loadMap));
				Assert.That(save, Does.Contain("baseSavePath"));
				Assert.That(save, Does.Not.Contain("\"Saves\""));
				Assert.That(panelRoot, Is.GreaterThanOrEqualTo(0));
				Assert.That(panelParse, Is.GreaterThan(panelRoot));
				Assert.That(panelCompatibility, Is.GreaterThan(panelParse));
				Assert.That(panelAdmit, Is.GreaterThan(panelCompatibility));
				Assert.That(panelReturn, Is.GreaterThan(panelAdmit));
				Assert.That(preservedSignature, Is.Not.Null,
					"Boundary-excluded callers require IsLoadPanelEnabled(Manifest).");
				Assert.That(preservedSignature?.ReturnType, Is.EqualTo(typeof(bool)));
			});
		}

		[Test]
		[Timeout(10000)]
		public void PersistenceBrowsersStayAtTheExactCurrentRootAndCloseFilesystemFailures()
		{
			var root = RepositoryRoot();
			var saveSource = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "GameSaveBrowserLogic.cs"));
			var replaySource = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "ReplayBrowserLogic.cs"));
			var loadGames = MethodBody(saveSource, "void LoadGames(");
			var loadPanelEnabled = MethodBody(saveSource,
				"public static bool IsLoadPanelEnabled(Manifest mod)");
			var loadReplays = MethodBody(replaySource, "void LoadReplays(");

			var saveEnumeration = loadGames.IndexOf("Directory.GetFiles(", StringComparison.Ordinal);
			var saveTopOnly = loadGames.IndexOf("SearchOption.TopDirectoryOnly",
				Math.Max(saveEnumeration, 0), StringComparison.Ordinal);
			var saveTry = loadGames.LastIndexOf("try", Math.Max(saveEnumeration, 0),
				StringComparison.Ordinal);
			var saveLastWrite = loadGames.LastIndexOf("File.GetLastWriteTime(",
				StringComparison.Ordinal);
			var saveCatch = loadGames.IndexOf("catch",
				Math.Max(Math.Max(saveEnumeration, saveLastWrite), 0), StringComparison.Ordinal);

			var panelEnumeration = loadPanelEnabled.IndexOf(
				"Directory.GetFiles(", StringComparison.Ordinal);
			var panelTopOnly = loadPanelEnabled.IndexOf("SearchOption.TopDirectoryOnly",
				Math.Max(panelEnumeration, 0), StringComparison.Ordinal);
			var panelTry = loadPanelEnabled.LastIndexOf("try", Math.Max(panelEnumeration, 0),
				StringComparison.Ordinal);
			var panelCatch = loadPanelEnabled.IndexOf("catch",
				Math.Max(panelEnumeration, 0), StringComparison.Ordinal);

			var replayEnumeration = loadReplays.IndexOf(
				"Directory.GetFiles(", StringComparison.Ordinal);
			var replayTopOnly = loadReplays.IndexOf("SearchOption.TopDirectoryOnly",
				Math.Max(replayEnumeration, 0), StringComparison.Ordinal);
			var replayTry = loadReplays.LastIndexOf("try", Math.Max(replayEnumeration, 0),
				StringComparison.Ordinal);
			var replayCatch = loadReplays.IndexOf("catch",
				Math.Max(replayEnumeration, 0), StringComparison.Ordinal);

			Assert.Multiple(() =>
			{
				Assert.That(saveEnumeration, Is.GreaterThanOrEqualTo(0));
				Assert.That(saveTopOnly, Is.GreaterThan(saveEnumeration),
					"Save enumeration must not recurse below the exact current contract root.");
				Assert.That(loadGames, Does.Not.Contain("SearchOption.AllDirectories"));
				Assert.That(saveTry, Is.GreaterThanOrEqualTo(0),
					"Save enumeration and timestamp reads must be inside a no-throw boundary.");
				Assert.That(saveTry, Is.LessThan(saveEnumeration));
				Assert.That(saveCatch, Is.GreaterThan(Math.Max(saveEnumeration, saveLastWrite)),
					"Directory.GetFiles and File.GetLastWriteTime failures must not escape the UI.");

				Assert.That(panelEnumeration, Is.GreaterThanOrEqualTo(0));
				Assert.That(panelTopOnly, Is.GreaterThan(panelEnumeration));
				Assert.That(loadPanelEnabled, Does.Not.Contain("SearchOption.AllDirectories"));
				Assert.That(panelTry, Is.GreaterThanOrEqualTo(0),
					"IsLoadPanelEnabled must return false instead of throwing on enumeration failure.");
				Assert.That(panelTry, Is.LessThan(panelEnumeration));
				Assert.That(panelCatch, Is.GreaterThan(panelEnumeration));

				Assert.That(replayEnumeration, Is.GreaterThanOrEqualTo(0));
				Assert.That(replayTopOnly, Is.GreaterThan(replayEnumeration),
					"Replay enumeration must not recurse below the exact current contract root.");
				Assert.That(loadReplays, Does.Not.Contain("SearchOption.AllDirectories"));
				Assert.That(replayTry, Is.GreaterThanOrEqualTo(0),
					"Replay directory and metadata IO failures must not escape the worker.");
				Assert.That(replayTry, Is.LessThan(replayEnumeration));
				Assert.That(replayCatch, Is.GreaterThan(replayEnumeration));
			});
		}

		[Test]
		[Timeout(10000)]
		public void HiddenRuntimeReasonsReachTypedNotificationsBeforeBrowserAdmission()
		{
			var root = RepositoryRoot();
			var saveSource = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "GameSaveBrowserLogic.cs"));
			var replaySource = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "ReplayBrowserLogic.cs"));
			var loadGames = MethodBody(saveSource, "void LoadGames(");
			var loadReplays = MethodBody(replaySource, "void LoadReplays(");

			var saveCompatibility = CompatibilityIndex(loadGames, 0);
			var saveReject = loadGames.IndexOf(
				"compatibility != RuntimeContractCompatibilityReason.Compatible",
				Math.Max(saveCompatibility, 0), StringComparison.Ordinal);
			var saveNotify = loadGames.IndexOf(
				"NotifyRuntimePersistenceFailure(compatibility,",
				Math.Max(saveReject, 0), StringComparison.Ordinal);
			var saveContinue = loadGames.IndexOf("continue", Math.Max(saveReject, 0),
				StringComparison.Ordinal);

			var replayCompatibility = CompatibilityIndex(loadReplays, 0);
			var replayReject = loadReplays.IndexOf(
				"compatibility != RuntimeContractCompatibilityReason.Compatible",
				Math.Max(replayCompatibility, 0), StringComparison.Ordinal);
			var replayNotify = loadReplays.IndexOf(
				"NotifyRuntimePersistenceFailure(compatibility,",
				Math.Max(replayReject, 0), StringComparison.Ordinal);
			var replayReturn = loadReplays.IndexOf("return", Math.Max(replayReject, 0),
				StringComparison.Ordinal);

			Assert.Multiple(() =>
			{
				Assert.That(saveReject, Is.GreaterThan(saveCompatibility));
				Assert.That(saveNotify, Is.GreaterThan(saveReject),
					"A hidden save must surface its typed Missing/Malformed/Profile/Contract reason.");
				Assert.That(saveNotify, Is.LessThan(saveContinue),
					"The notification must be reachable before the rejected save is skipped.");
				Assert.That(replayReject, Is.GreaterThan(replayCompatibility));
				Assert.That(replayNotify, Is.GreaterThan(replayReject),
					"A hidden replay must surface its typed Missing/Malformed/Profile/Contract reason.");
				Assert.That(replayNotify, Is.LessThan(replayReturn),
					"The notification must be reachable before the rejected replay is skipped.");
			});
		}

		[Test]
		[Timeout(10000)]
		public void ReplayBrowserUsesTheCurrentContractRootAndKeepsTypedVisibilityClosed()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "ReplayBrowserLogic.cs"));
			var constructor = MethodBody(source, "public ReplayBrowserLogic(");
			var loadReplays = MethodBody(source, "void LoadReplays(");
			var visibility = MethodBody(source, "static bool EvaluateReplayVisibility(");
			var watch = MethodBody(source, "void WatchReplay()");

			var constructorRoot = constructor.IndexOf(
				"RuntimeContractStorage.ReplaysPath(", StringComparison.Ordinal);
			var constructorIdentity = constructor.IndexOf(
				"modData.RuntimeContract", Math.Max(constructorRoot, 0), StringComparison.Ordinal);

			var metadataRead = loadReplays.IndexOf("ReplayMetadata.Read(fileName)",
				StringComparison.Ordinal);
			var compatibility = CompatibilityIndex(loadReplays, metadataRead);
			var admission = loadReplays.IndexOf(
				"RuntimeContractCompatibilityReason.Compatible", Math.Max(compatibility, 0),
				StringComparison.Ordinal);
			var add = loadReplays.IndexOf("loadedReplays.Add(replay)", StringComparison.Ordinal);

			var visibilityCompatibility = CompatibilityIndex(visibility, 0);
			var visibilityAdmission = visibility.IndexOf(
				"RuntimeContractCompatibilityReason.Compatible",
				Math.Max(visibilityCompatibility, 0), StringComparison.Ordinal);
			var firstLegacyFilter = visibility.IndexOf("filter.Type", StringComparison.Ordinal);
			var joinReplay = watch.IndexOf("Game.JoinReplay(selectedReplay.FilePath)",
				StringComparison.Ordinal);

			Assert.Multiple(() =>
			{
				Assert.That(constructorRoot, Is.GreaterThanOrEqualTo(0));
				Assert.That(constructorIdentity, Is.GreaterThan(constructorRoot));
				Assert.That(constructor, Does.Not.Contain("\"Replays\""),
					"The browser must not rebuild the legacy replay root beside the central API.");
				Assert.That(metadataRead, Is.GreaterThanOrEqualTo(0));
				Assert.That(compatibility, Is.GreaterThan(metadataRead));
				Assert.That(admission, Is.GreaterThan(compatibility));
				Assert.That(add, Is.GreaterThan(admission));
				Assert.That(visibilityCompatibility, Is.GreaterThanOrEqualTo(0));
				Assert.That(visibilityAdmission, Is.GreaterThan(visibilityCompatibility));
				Assert.That(firstLegacyFilter, Is.GreaterThan(visibilityAdmission),
					"Runtime identity must fail closed before user-selected replay filters run.");
				Assert.That(joinReplay, Is.GreaterThanOrEqualTo(0),
					"WatchReplay must retain Game.JoinReplay as the non-bypassable final gate.");
			});
		}

		[Test]
		[Timeout(10000)]
		public void ReplayUiEntrypointsCloseAndReportTypedFinalGateFailures()
		{
			var root = RepositoryRoot();
			var replaySource = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "ReplayBrowserLogic.cs"));
			var mainMenuSource = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "MainMenuLogic.cs"));
			var watch = MethodBody(replaySource, "void WatchReplay()");
			var launch = MethodBody(mainMenuSource, "void LaunchIntoRequestedPanel()");

			var watchCompatibility = CompatibilityIndex(watch, 0);
			var watchJoin = watch.IndexOf("Game.JoinReplay(selectedReplay.FilePath)",
				StringComparison.Ordinal);
			var watchTry = watch.LastIndexOf("try", Math.Max(watchJoin, 0),
				StringComparison.Ordinal);
			var watchCatch = watch.IndexOf("catch", Math.Max(watchJoin, 0),
				StringComparison.Ordinal);
			var watchNotify = watch.IndexOf("NotifyRuntimePersistenceFailure(",
				Math.Max(watchCatch, 0), StringComparison.Ordinal);
			var watchCatchText = watchCatch >= 0 && watchNotify > watchCatch ?
				watch.Substring(watchCatch, watchNotify - watchCatch) : string.Empty;

			var replayCase = launch.IndexOf("case \"replay\"", StringComparison.Ordinal);
			var deepLinkJoin = launch.IndexOf("Game.JoinReplay(argument)",
				Math.Max(replayCase, 0), StringComparison.Ordinal);
			var loadSaveCase = launch.IndexOf("case \"loadsave\"",
				Math.Max(deepLinkJoin, 0), StringComparison.Ordinal);
			var deepLinkTry = launch.LastIndexOf("try", Math.Max(deepLinkJoin, 0),
				StringComparison.Ordinal);
			var deepLinkCatch = launch.IndexOf("catch", Math.Max(deepLinkJoin, 0),
				StringComparison.Ordinal);
			var deepLinkNotify = launch.IndexOf("NotifyRuntimePersistenceFailure(",
				Math.Max(deepLinkCatch, 0), StringComparison.Ordinal);
			var deepLinkCatchText = deepLinkCatch >= 0 && loadSaveCase > deepLinkCatch ?
				launch.Substring(deepLinkCatch, loadSaveCase - deepLinkCatch) : string.Empty;

			Assert.Multiple(() =>
			{
				Assert.That(watchJoin, Is.GreaterThan(watchCompatibility));
				Assert.That(watchTry, Is.GreaterThan(watchCompatibility));
				Assert.That(watchTry, Is.LessThan(watchJoin));
				Assert.That(watchCatch, Is.GreaterThan(watchJoin),
					"Watch must contain the final replay gate instead of throwing through the UI.");
				Assert.That(watchNotify, Is.GreaterThan(watchCatch));
				Assert.That(ContainsTypedRuntimeReason(watchCatchText), Is.True,
					"The final-gate exception must preserve a typed compatibility reason.");

				Assert.That(replayCase, Is.GreaterThanOrEqualTo(0));
				Assert.That(deepLinkJoin, Is.GreaterThan(replayCase));
				Assert.That(loadSaveCase, Is.GreaterThan(deepLinkJoin));
				Assert.That(deepLinkTry, Is.GreaterThan(replayCase));
				Assert.That(deepLinkTry, Is.LessThan(deepLinkJoin));
				Assert.That(deepLinkCatch, Is.InRange(deepLinkJoin + 1, loadSaveCase - 1),
					"Replay deep links must contain final-gate failures before dispatch continues.");
				Assert.That(deepLinkNotify, Is.InRange(deepLinkCatch + 1, loadSaveCase - 1));
				Assert.That(ContainsTypedRuntimeReason(deepLinkCatchText), Is.True);
			});
		}

		[Test]
		[Timeout(10000)]
		public void LoadSaveDeepLinkRejectsNonSegmentsAndValidatesBeforeMapOrServerMutation()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "MainMenuLogic.cs"));
			var launch = MethodBody(source, "void LaunchIntoRequestedPanel()");
			var load = MethodBody(source, "void LoadSavedGame(string saveFilename)");

			var segment = load.IndexOf("Path.GetFileName(saveFilename)", StringComparison.Ordinal);
			var ordinal = load.IndexOf("StringComparison.Ordinal", Math.Max(segment, 0),
				StringComparison.Ordinal);
			var centralRoot = load.IndexOf("RuntimeContractStorage.SavesPath(",
				Math.Max(ordinal, 0), StringComparison.Ordinal);
			var parse = load.IndexOf("new GameSave(savePath)", Math.Max(centralRoot, 0),
				StringComparison.Ordinal);
			var compatibility = CompatibilityIndex(load, parse);
			var admission = load.IndexOf("RuntimeContractCompatibilityReason.Compatible",
				Math.Max(compatibility, 0), StringComparison.Ordinal);
			var map = load.IndexOf("modData.MapCache", Math.Max(admission, 0),
				StringComparison.Ordinal);
			var server = load.IndexOf("Game.CreateAndStartLocalServer(", Math.Max(map, 0),
				StringComparison.Ordinal);

			Assert.Multiple(() =>
			{
				Assert.That(segment, Is.GreaterThanOrEqualTo(0),
					"loadsave input must be one filename, never a caller-supplied path.");
				Assert.That(ordinal, Is.GreaterThan(segment));
				Assert.That(centralRoot, Is.GreaterThan(ordinal));
				Assert.That(parse, Is.GreaterThan(centralRoot));
				Assert.That(compatibility, Is.GreaterThan(parse));
				Assert.That(admission, Is.GreaterThan(compatibility));
				Assert.That(map, Is.GreaterThan(admission));
				Assert.That(server, Is.GreaterThan(map));
				Assert.That(load, Does.Not.Contain("\"Saves\""));
				Assert.That(launch, Does.Contain("Game.JoinReplay(argument)"),
					"Replay deep links must still pass through Game.JoinReplay.");
			});
		}

		[Test]
		[Timeout(10000)]
		public void SaveNamesAndEmbeddedMapUidsAreValidatedBeforeFilesystemOrMapLookup()
		{
			var root = RepositoryRoot();
			var saveSource = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "GameSaveBrowserLogic.cs"));
			var mainMenuSource = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "MainMenuLogic.cs"));
			var saveMethod = MethodBody(saveSource, "void Save(World world)");
			var loadMethod = MethodBody(saveSource, "void Load()");
			var deepLinkLoad = MethodBody(mainMenuSource, "void LoadSavedGame(string saveFilename)");

			var canonicalMethod = typeof(GameSaveBrowserLogic).GetMethod(
				"IsCanonicalFilename", BindingFlags.Static | BindingFlags.Public |
				BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
			var canonicalCall = saveMethod.IndexOf("IsCanonicalFilename(", StringComparison.Ordinal);
			var savePathCombine = saveMethod.IndexOf("Path.Combine(baseSavePath,",
				StringComparison.Ordinal);

			var loadAdmission = loadMethod.IndexOf(
				"RuntimeContractCompatibilityReason.Compatible", StringComparison.Ordinal);
			var loadMap = loadMethod.IndexOf("modData.MapCache", Math.Max(loadAdmission, 0),
				StringComparison.Ordinal);
			var loadMapPrefix = loadAdmission >= 0 && loadMap > loadAdmission ?
				loadMethod.Substring(loadAdmission, loadMap - loadAdmission) : string.Empty;

			var deepLinkAdmission = deepLinkLoad.IndexOf(
				"RuntimeContractCompatibilityReason.Compatible", StringComparison.Ordinal);
			var deepLinkMap = deepLinkLoad.IndexOf("modData.MapCache",
				Math.Max(deepLinkAdmission, 0), StringComparison.Ordinal);
			var deepLinkMapPrefix = deepLinkAdmission >= 0 && deepLinkMap > deepLinkAdmission ?
				deepLinkLoad.Substring(deepLinkAdmission, deepLinkMap - deepLinkAdmission) : string.Empty;

			Assert.Multiple(() =>
			{
				Assert.That(canonicalMethod, Is.Not.Null,
					"The save UI needs one pure canonical filename policy shared by validation and use.");
				Assert.That(canonicalMethod?.ReturnType, Is.EqualTo(typeof(bool)));
				Assert.That(canonicalCall, Is.GreaterThanOrEqualTo(0));
				Assert.That(canonicalCall, Is.LessThan(savePathCombine),
					"User-controlled save text must be canonical before Path.Combine or overwrite probing.");
				Assert.That(HasSafeMapUidGuard(loadMapPrefix), Is.True,
					"Save-browser load must reject absent/empty map metadata before MapCache indexing.");
				Assert.That(HasSafeMapUidGuard(deepLinkMapPrefix), Is.True,
					"Deep-link load must reject absent/empty map metadata before MapCache indexing.");
			});

			if (canonicalMethod == null)
				return;

			Assert.Multiple(() =>
			{
				Assert.That(InvokeFilenamePolicy(canonicalMethod, "Commander.orasav"), Is.True);
				Assert.That(InvokeFilenamePolicy(canonicalMethod, "存档.orasav"), Is.True);
				foreach (var invalid in new[]
				{
					null, string.Empty, ".", "..", "../escape.orasav", "nested/escape.orasav",
					"nested\\escape.orasav", "/absolute.orasav", "stream:escape.orasav",
					"nul\0escape.orasav", "e\u0301.orasav", "line\u2028break.orasav",
				})
					Assert.That(InvokeFilenamePolicy(canonicalMethod, invalid), Is.False,
						$"Expected filename token to be rejected: {invalid ?? "<null>"}");
			});
		}

		[Test]
		[Timeout(10000)]
		public void SkirmishStateUsesOnlyTheCurrentContractPathForBothReadAndWrite()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine", "OpenRA.Mods.Common", "ServerTraits", "SkirmishLogic.cs"));
			var write = MethodBody(source, "void INotifySyncLobbyInfo.LobbyInfoSynced(");
			var read = MethodBody(source, "void IClientJoined.ClientJoined(");

			var writePath = write.IndexOf("RuntimeContractStorage.SkirmishPath(",
				StringComparison.Ordinal);
			var writeIdentity = write.IndexOf("server.ModData.RuntimeContract",
				Math.Max(writePath, 0), StringComparison.Ordinal);
			var createDirectory = write.IndexOf("Directory.CreateDirectory(",
				Math.Max(writeIdentity, 0), StringComparison.Ordinal);
			var writeFile = write.IndexOf(".WriteToFile(path)", Math.Max(createDirectory, 0),
				StringComparison.Ordinal);

			var readPath = read.IndexOf("RuntimeContractStorage.SkirmishPath(",
				StringComparison.Ordinal);
			var readIdentity = read.IndexOf("server.ModData.RuntimeContract",
				Math.Max(readPath, 0), StringComparison.Ordinal);
			var initialize = read.IndexOf("TryInitializeFromFile(server, skirmishFile, conn)",
				Math.Max(readIdentity, 0), StringComparison.Ordinal);

			Assert.Multiple(() =>
			{
				Assert.That(writePath, Is.GreaterThanOrEqualTo(0));
				Assert.That(writeIdentity, Is.GreaterThan(writePath));
				Assert.That(createDirectory, Is.GreaterThan(writeIdentity));
				Assert.That(writeFile, Is.GreaterThan(createDirectory));
				Assert.That(readPath, Is.GreaterThanOrEqualTo(0));
				Assert.That(readIdentity, Is.GreaterThan(readPath));
				Assert.That(initialize, Is.GreaterThan(readIdentity));
				Assert.That(source, Does.Not.Contain("RuntimeContractIdentity.Legacy"),
					"Profiled skirmish startup must not fall back to legacy state.");
				Assert.That(source, Does.Not.Contain("$\"skirmish."),
					"Legacy filenames belong only in RuntimeContractStorage.");
			});
		}

		[Test]
		[Timeout(10000)]
		public void SkirmishPersistenceClosesReadParseAndWriteFailures()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine", "OpenRA.Mods.Common", "ServerTraits", "SkirmishLogic.cs"));
			var initialize = MethodBody(source, "static bool TryInitializeFromFile(");
			var write = MethodBody(source, "void INotifySyncLobbyInfo.LobbyInfoSynced(");

			var exists = initialize.IndexOf("File.Exists(path)", StringComparison.Ordinal);
			var read = initialize.IndexOf("MiniYaml.FromFile(path)", StringComparison.Ordinal);
			var readTry = initialize.LastIndexOf("try", Math.Max(exists, 0),
				StringComparison.Ordinal);
			var readCatch = initialize.LastIndexOf("catch", StringComparison.Ordinal);
			var readFailureReturn = readCatch < 0 ? -1 : initialize.IndexOf(
				"return false", readCatch, StringComparison.Ordinal);

			var createDirectory = write.IndexOf("Directory.CreateDirectory(",
				StringComparison.Ordinal);
			var writeFile = write.IndexOf(".WriteToFile(path)",
				Math.Max(createDirectory, 0), StringComparison.Ordinal);
			var writeTry = write.LastIndexOf("try", Math.Max(createDirectory, 0),
				StringComparison.Ordinal);
			var writeCatch = write.IndexOf("catch", Math.Max(writeFile, 0),
				StringComparison.Ordinal);

			Assert.Multiple(() =>
			{
				Assert.That(exists, Is.GreaterThanOrEqualTo(0));
				Assert.That(read, Is.GreaterThan(exists));
				Assert.That(readTry, Is.GreaterThanOrEqualTo(0),
					"Skirmish read and YAML parse failures must be treated as unavailable state.");
				Assert.That(readTry, Is.LessThanOrEqualTo(exists));
				Assert.That(readCatch, Is.GreaterThan(read));
				Assert.That(readFailureReturn, Is.GreaterThan(readCatch));
				Assert.That(createDirectory, Is.GreaterThanOrEqualTo(0));
				Assert.That(writeFile, Is.GreaterThan(createDirectory));
				Assert.That(writeTry, Is.GreaterThanOrEqualTo(0),
					"Directory creation and skirmish writes must share a no-throw boundary.");
				Assert.That(writeTry, Is.LessThan(createDirectory));
				Assert.That(writeCatch, Is.GreaterThan(writeFile));
			});
		}

		[TestCase("late ghost bot slot")]
		[TestCase("late malformed bot field")]
		[Timeout(10000)]
		public void FailedSkirmishRestoreStagesEveryMutationBeforeOneCommit(string failure)
		{
			var type = typeof(OpenRA.Mods.Common.Server.SkirmishLogic);
			var stage = type.GetMethod(
				"TryStageSkirmishState", BindingFlags.Static | BindingFlags.NonPublic);
			var commit = type.GetMethod(
				"CommitSkirmishState", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.Multiple(() =>
			{
				Assert.That(stage, Is.Not.Null,
					$"{failure} must return false without publishing map/options/player/bot/index state.");
				Assert.That(commit, Is.Not.Null,
					"A successful parse needs one explicit publication point after all validation.");
			});

			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine", "OpenRA.Mods.Common", "ServerTraits", "SkirmishLogic.cs"));
			var initialize = MethodBody(source, "static bool TryInitializeFromFile(");
			var joined = MethodBody(source, "void IClientJoined.ClientJoined(");
			var stageCall = initialize.IndexOf("TryStageSkirmishState(", StringComparison.Ordinal);
			var commitCall = initialize.IndexOf("CommitSkirmishState(", Math.Max(stageCall, 0),
				StringComparison.Ordinal);
			var fallbackGate = joined.IndexOf(
				"TryInitializeFromFile(server, skirmishFile, conn)", StringComparison.Ordinal);
			var fallback = joined.IndexOf("slot_bot ", Math.Max(fallbackGate, 0),
				StringComparison.Ordinal);

			Assert.Multiple(() =>
			{
				Assert.That(stageCall, Is.GreaterThanOrEqualTo(0));
				Assert.That(commitCall, Is.GreaterThan(stageCall));
				Assert.That(fallbackGate, Is.GreaterThanOrEqualTo(0));
				Assert.That(fallback, Is.GreaterThan(fallbackGate),
					"Fallback may run only after a failed transaction, so it cannot stack on partial bots.");
				foreach (var mutation in new[]
				{
					"InterpretCommand(", "LobbyOptions[", "DeserializeToClient(",
					"ChooseFreePlayerIndex(", "Clients.Add(", "SyncClientToPlayerReference(",
				})
					Assert.That(initialize, Does.Not.Contain(mutation),
						$"{failure}: {mutation} must be confined to stage/commit so false preserves object identity and sync counts.");
			});
		}

		[Test]
		[Timeout(10000)]
		public void RuntimePersistenceDoesNotCreateAContractPartitionForSharedMaps()
		{
			var pathMethods = typeof(RuntimeContractStorage).GetMethods(
				BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
				.Where(method => method.ReturnType == typeof(string))
				.Select(method => method.Name)
				.ToArray();

			Assert.Multiple(() =>
			{
				Assert.That(pathMethods, Is.EquivalentTo(new[]
				{
					nameof(RuntimeContractStorage.SavesPath),
					nameof(RuntimeContractStorage.ReplaysPath),
					nameof(RuntimeContractStorage.ServerSettingsPath),
					nameof(RuntimeContractStorage.SkirmishPath),
				}));
				Assert.That(pathMethods, Has.None.Contains("Map"),
					"Base and Yuri must continue to use the shared manifest map folders.");
			});
		}

		[Test]
		[Timeout(10000)]
		public void RuntimePersistenceCopyIsExactInEnglishAndChineseAndUsedAtAllUiEntrypoints()
		{
			var root = RepositoryRoot();
			var english = File.ReadAllText(Path.Combine(root,
				"engine", "mods", "common", "fluent", "chrome.ftl"));
			var chinese = File.ReadAllText(Path.Combine(root,
				"engine", "mods", "common", "fluent", "zh-CN", "chrome.ftl"));
			var saveBrowser = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "GameSaveBrowserLogic.cs"));
			var replayBrowser = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "ReplayBrowserLogic.cs"));
			var mainMenu = File.ReadAllText(Path.Combine(root,
				"engine", "OpenRA.Mods.Common", "Widgets", "Logic", "MainMenuLogic.cs"));

			const string MismatchKey = "notification-runtime-persistence-mismatch";
			const string LegacyHiddenKey = "notification-runtime-persistence-legacy-hidden";
			const string SavedGameKindKey = "notification-runtime-persistence-kind-saved-game";
			const string ReplayKindKey = "notification-runtime-persistence-kind-replay";
			const string EnglishMismatch =
				"notification-runtime-persistence-mismatch = The selected { $kind } belongs to a different content profile or build and cannot be loaded.";
			const string EnglishLegacy =
				"notification-runtime-persistence-legacy-hidden = The legacy { $kind } has no verified content identity and is hidden.";
			const string EnglishSavedGameKind =
				"notification-runtime-persistence-kind-saved-game = saved game";
			const string EnglishReplayKind =
				"notification-runtime-persistence-kind-replay = replay";
			const string ChineseMismatch =
				"notification-runtime-persistence-mismatch = 所选{ $kind }属于不同的内容配置或版本，无法载入。";
			const string ChineseLegacy =
				"notification-runtime-persistence-legacy-hidden = 旧版{ $kind }缺少经验证的内容标识，已隐藏。";
			const string ChineseSavedGameKind =
				"notification-runtime-persistence-kind-saved-game = 存档";
			const string ChineseReplayKind =
				"notification-runtime-persistence-kind-replay = 回放";

			var saveNotification = MethodBody(saveBrowser,
				"static void NotifyRuntimePersistenceFailure(");
			var replayNotification = MethodBody(replayBrowser,
				"static void NotifyRuntimePersistenceFailure(");
			var mainMenuNotification = MethodBody(mainMenu,
				"static void NotifyRuntimePersistenceFailure(");

			Assert.Multiple(() =>
			{
				Assert.That(english, Does.Contain(EnglishMismatch));
				Assert.That(english, Does.Contain(EnglishLegacy));
				Assert.That(english, Does.Contain(EnglishSavedGameKind));
				Assert.That(english, Does.Contain(EnglishReplayKind));
				Assert.That(chinese, Does.Contain(ChineseMismatch));
				Assert.That(chinese, Does.Contain(ChineseLegacy));
				Assert.That(chinese, Does.Contain(ChineseSavedGameKind));
				Assert.That(chinese, Does.Contain(ChineseReplayKind));
				Assert.That(saveBrowser, Does.Contain(MismatchKey));
				Assert.That(saveBrowser, Does.Contain(LegacyHiddenKey));
				Assert.That(saveBrowser, Does.Contain(SavedGameKindKey));
				Assert.That(replayBrowser, Does.Contain(MismatchKey));
				Assert.That(replayBrowser, Does.Contain(LegacyHiddenKey));
				Assert.That(replayBrowser, Does.Contain(ReplayKindKey));
				Assert.That(mainMenu, Does.Contain(MismatchKey));
				Assert.That(mainMenu, Does.Contain(LegacyHiddenKey));
				Assert.That(mainMenu, Does.Contain(SavedGameKindKey));
				Assert.That(saveBrowser, Does.Contain("\"kind\""));
				Assert.That(replayBrowser, Does.Contain("\"kind\""));
				Assert.That(mainMenu, Does.Contain("\"kind\""));
				Assert.That(saveBrowser, Does.Not.Contain(
					"NotifyRuntimePersistenceFailure(compatibility, \"saved game\")"));
				Assert.That(replayBrowser, Does.Not.Contain(
					"NotifyRuntimePersistenceFailure(compatibility, \"replay\")"));
				Assert.That(mainMenu, Does.Not.Contain(
					"NotifyRuntimePersistenceFailure(compatibility, \"saved game\")"));
				Assert.That(saveNotification, Does.Contain("FluentProvider.GetMessage(kind)"),
					"The kind argument must be localized before interpolation.");
				Assert.That(replayNotification, Does.Contain("FluentProvider.GetMessage(kind)"));
				Assert.That(mainMenuNotification, Does.Contain("FluentProvider.GetMessage(kind)"));
			});
		}

		static bool ContainsTypedRuntimeReason(string source) =>
			source.Contains("RuntimeContractCompatibility", StringComparison.Ordinal) ||
			source.Contains(".Reason", StringComparison.Ordinal);

		static bool HasSafeMapUidGuard(string source)
		{
			var nullSafe = source.Contains("GlobalSettings?.Map", StringComparison.Ordinal) ||
				source.Contains("GlobalSettings == null", StringComparison.Ordinal) ||
				source.Contains("GlobalSettings is null", StringComparison.Ordinal);
			var emptySafe = source.Contains("string.IsNullOrEmpty(", StringComparison.Ordinal) ||
				source.Contains("string.IsNullOrWhiteSpace(", StringComparison.Ordinal);
			return nullSafe && emptySafe;
		}

		static bool HasUnfilteredCatchAll(string source) =>
			System.Text.RegularExpressions.Regex.IsMatch(source,
				@"catch\s*(?:\(\s*(?:System\.)?Exception(?:\s+\w+)?\s*\))?\s*\{");

		static bool IsRuntimeContractCompatibilityException(Exception exception) =>
			exception?.GetType().FullName == "OpenRA.RuntimeContractCompatibilityException";

		static void AssertRuntimeContractCompatibilityException(
			Exception exception, RuntimeContractCompatibilityReason expectedReason)
		{
			Assert.That(exception, Is.Not.Null);
			Assert.That(exception.GetType().FullName,
				Is.EqualTo("OpenRA.RuntimeContractCompatibilityException"),
				"Compatibility rejection must use the dedicated typed exception. " +
				$"Actual: {exception.GetType().FullName}: {exception.Message}");
			var reason = exception.GetType().GetProperty(
				"Reason", BindingFlags.Instance | BindingFlags.Public);
			Assert.That(reason, Is.Not.Null,
				"The typed replay rejection must expose its closed enum reason.");
			Assert.That(reason.GetValue(exception), Is.EqualTo(expectedReason));
		}

		static void AssertTypedReplayCatch(string source, string joinCall)
		{
			var join = source.IndexOf(joinCall, StringComparison.Ordinal);
			var typedCatch = source.IndexOf(
				"catch (RuntimeContractCompatibilityException", Math.Max(join, 0),
				StringComparison.Ordinal);
			Assert.That(join, Is.GreaterThanOrEqualTo(0));
			Assert.That(typedCatch, Is.GreaterThan(join),
				"The UI may show runtime mismatch copy only for the typed final-gate exception.");

			var typedBlock = BracedBlockAt(source, typedCatch);
			Assert.Multiple(() =>
			{
				Assert.That(typedBlock, Does.Contain(".Reason"));
				Assert.That(typedBlock, Does.Contain("NotifyRuntimePersistenceFailure("));
			});

			var genericCatch = source.IndexOf(
				"catch (Exception", typedCatch + 1, StringComparison.Ordinal);
			Assert.That(genericCatch, Is.GreaterThan(typedCatch),
				"Generic corrupt/IO failures still need their own no-throw logging boundary.");
			var genericBlock = BracedBlockAt(source, genericCatch);
			Assert.That(genericBlock, Does.Not.Contain("NotifyRuntimePersistenceFailure("),
				"Generic failures must never be relabeled as a runtime contract mismatch.");
		}

		static int CountOccurrences(string source, string value)
		{
			var count = 0;
			var index = 0;
			while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
			{
				count++;
				index += value.Length;
			}

			return count;
		}

		static string BracedBlockAt(string source, int start)
		{
			Assert.That(start, Is.GreaterThanOrEqualTo(0));
			var openBrace = source.IndexOf('{', start);
			Assert.That(openBrace, Is.GreaterThan(start));
			var depth = 0;
			for (var i = openBrace; i < source.Length; i++)
			{
				if (source[i] == '{')
					depth++;
				else if (source[i] == '}' && --depth == 0)
					return source.Substring(openBrace, i - openBrace + 1);
			}

			Assert.Fail("Unterminated source block.");
			return null;
		}

		static void AssertReplayReleasePrecedesDispose(
			string body, string disposeToken, string ownershipPath)
		{
			var release = body.IndexOf("ReleaseReplaySnapshot(", StringComparison.Ordinal);
			var dispose = body.IndexOf(disposeToken, Math.Max(release, 0), StringComparison.Ordinal);
			Assert.Multiple(() =>
			{
				Assert.That(release, Is.GreaterThanOrEqualTo(0),
					$"{ownershipPath} must release any owned replay snapshot.");
				Assert.That(dispose, Is.GreaterThan(release),
					$"{ownershipPath} must release its replay snapshot before disposing the connection owner.");
			});
		}

		static byte[] ReplayBytes(
			string footerProfile,
			string footerContract,
			string lobbyProfile,
			string lobbyContract,
			bool includeLobby)
		{
			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream);
			if (includeLobby)
			{
				var lobby = new Session();
				lobby.GlobalSettings.RuntimeProfile = lobbyProfile;
				lobby.GlobalSettings.RuntimeContract = lobbyContract;
				var packet = new OrderPacket(new[]
				{
					Order.FromTargetString("SyncInfo", lobby.Serialize(), true),
					Order.FromTargetString("StartGame", string.Empty, true),
				}).Serialize(0);
				writer.Write(0);
				writer.Write(packet.Length);
				writer.Write(packet);
			}

			var info = new GameInformation
			{
				Mod = ModId,
				Version = ModVersion,
				RuntimeProfile = footerProfile,
				RuntimeContract = footerContract,
				FinalGameTick = 0,
			};
			new ReplayMetadata(info).Write(writer);
			writer.Flush();
			return stream.ToArray();
		}

		static bool InvokeFilenamePolicy(MethodInfo method, string filename) =>
			(bool)method.Invoke(null, new object[] { filename });

		static bool InvokeTryWriteGameSave(GameSave save, string directory, string filename)
		{
			var method = typeof(GameServer).GetMethod(
				"TryWriteGameSave", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null);
			return (bool)method.Invoke(null, new object[] { save, directory, filename });
		}

		static void AssertProfiledPaths(RuntimeContractIdentity identity, string modVersion)
		{
			var profileRoot = Path.Combine(
				SupportDir, "Profiles", ModId, identity.ProfileId, identity.ContractDigest);

			Assert.Multiple(() =>
			{
				Assert.That(RuntimeContractStorage.SavesPath(
					SupportDir, ModId, modVersion, identity),
					Is.EqualTo(Path.Combine(profileRoot, "Saves", modVersion)));
				Assert.That(RuntimeContractStorage.ReplaysPath(
					SupportDir, ModId, modVersion, identity),
					Is.EqualTo(Path.Combine(profileRoot, "Replays", modVersion)));
				Assert.That(RuntimeContractStorage.ServerSettingsPath(
					SupportDir, ModId, identity),
					Is.EqualTo(Path.Combine(profileRoot, "settings.server.yaml")));
				Assert.That(RuntimeContractStorage.SkirmishPath(
					SupportDir, ModId, identity),
					Is.EqualTo(Path.Combine(profileRoot, "skirmish.yaml")));
			});
		}

		static string YamlValue(string path, string section, string field) =>
			MiniYaml.FromFile(path, false)
				.Single(node => node.Key == section).Value.Nodes
				.Single(node => node.Key == field).Value.Value;

#pragma warning disable SYSLIB0050
		static ModData UninitializedModData(RuntimeContractIdentity identity)
		{
			var modData = (ModData)FormatterServices.GetUninitializedObject(typeof(ModData));
			var runtimeContract = typeof(ModData).GetField(
				"<RuntimeContract>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(runtimeContract, Is.Not.Null);
			runtimeContract.SetValue(modData, identity);
			return modData;
		}

		static ModData UninitializedReplayModData(RuntimeContractIdentity identity)
		{
			var modData = UninitializedModData(identity);
			var speed = (GameSpeed)FormatterServices.GetUninitializedObject(typeof(GameSpeed));
			SetInstanceField(speed, nameof(GameSpeed.Name), "default");
			SetInstanceField(speed, nameof(GameSpeed.Timestep), 40);
			SetInstanceField(speed, nameof(GameSpeed.OrderLatency), 0);

			var gameSpeeds = (GameSpeeds)FormatterServices.GetUninitializedObject(typeof(GameSpeeds));
			SetInstanceField(gameSpeeds, nameof(GameSpeeds.DefaultSpeed), "default");
			SetInstanceField(gameSpeeds, nameof(GameSpeeds.Speeds),
				new Dictionary<string, GameSpeed>(StringComparer.Ordinal)
				{
					["default"] = speed,
				});

			var modules = new OpenRA.Primitives.TypeDictionary();
			modules.Add(gameSpeeds);
			var manifest = (Manifest)FormatterServices.GetUninitializedObject(typeof(Manifest));
			SetInstanceField(manifest, "modules", modules);
			SetInstanceField(manifest, "customDataLoaded", true);
			SetInstanceField(modData, nameof(ModData.Manifest), manifest);
			return modData;
		}

		static void SetInstanceField(object target, string name, object value)
		{
			var field = target.GetType().GetField(
				name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().FullName}.{name}.");
			field.SetValue(target, value);
		}

		static GameServer UninitializedServer(
			GameSave gameSave,
			Session.Global globalSettings,
			Dictionary<string, Session.Slot> slots,
			params Session.Client[] clients)
		{
			var server = (GameServer)FormatterServices.GetUninitializedObject(typeof(GameServer));
			server.GameSave = gameSave;
			server.LobbyInfo = new Session
			{
				GlobalSettings = globalSettings,
				Slots = slots,
				Clients = clients.ToList(),
			};
			return server;
		}
#pragma warning restore SYSLIB0050

		static GameSave GameSaveWithIdentity(string profile, string contract)
		{
			var save = new GameSave();
			var globalSettings = typeof(GameSave).GetProperty(
				nameof(GameSave.GlobalSettings), BindingFlags.Instance | BindingFlags.Public);
			Assert.That(globalSettings, Is.Not.Null);
			globalSettings.SetValue(save, new Session.Global
			{
				RuntimeProfile = profile,
				RuntimeContract = contract,
			});
			return save;
		}

		static void WriteGameSave(
			string directory, string filename, string profile, string contract)
		{
			var save = GameSaveWithIdentity(profile, contract);
			SetGameSaveSlotClients(save, new Dictionary<string, SlotClient>());
			SetGameSaveLastSyncPacket(save);
			save.Save(Path.Combine(directory, filename));
		}

		static void AssertReadRejected(
			string directory,
			string filename,
			RuntimeContractIdentity activeIdentity,
			RuntimeContractCompatibilityReason expectedReason)
		{
			var accepted = true;
			GameSave loaded = new();
			var reason = RuntimeContractCompatibilityReason.Compatible;
			Assert.DoesNotThrow(() => accepted = GameServer.TryReadCompatibleGameSave(
				directory, filename, activeIdentity, out loaded, out reason));
			Assert.Multiple(() =>
			{
				Assert.That(accepted, Is.False);
				Assert.That(loaded, Is.Null);
				Assert.That(reason, Is.EqualTo(expectedReason));
			});
		}

		static void AssertStageRejected(
			GameSave save, IReadOnlyList<Session.Client> currentClients, int nextPlayerIndex)
		{
			var accepted = true;
			List<Session.Client> stagedClients = new();
			var stagedNextPlayerIndex = -1;
			Assert.DoesNotThrow(() => accepted = GameServer.TryStageGameSaveClients(
				save, currentClients, nextPlayerIndex,
				out stagedClients, out stagedNextPlayerIndex));
			Assert.Multiple(() =>
			{
				Assert.That(accepted, Is.False);
				Assert.That(stagedClients, Is.Null);
				Assert.That(stagedNextPlayerIndex, Is.EqualTo(nextPlayerIndex));
			});
		}

		static GameSave StagingSave(params Session.Client[] savedClients)
		{
			var save = GameSaveWithIdentity(
				BaseIdentity.ProfileId, BaseIdentity.ContractDigest);
			foreach (var client in savedClients)
				save.Slots.Add(client.Slot, new Session.Slot { PlayerReference = client.Slot });

			SetGameSaveSlotClients(save, savedClients.ToDictionary(
				client => client.Slot, client => new SlotClient(client), StringComparer.Ordinal));
			return save;
		}

		static void SetGameSaveSlots(GameSave save, Dictionary<string, Session.Slot> slots)
		{
			var property = typeof(GameSave).GetProperty(
				nameof(GameSave.Slots), BindingFlags.Instance | BindingFlags.Public);
			Assert.That(property, Is.Not.Null);
			property.SetValue(save, slots);
		}

		static void SetGameSaveSlotClients(
			GameSave save, Dictionary<string, SlotClient> slotClients)
		{
			var property = typeof(GameSave).GetProperty(
				nameof(GameSave.SlotClients), BindingFlags.Instance | BindingFlags.Public);
			Assert.That(property, Is.Not.Null);
			property.SetValue(save, slotClients);
		}

		static void SetGameSaveLastSyncPacket(GameSave save, byte[] packet = null)
		{
			var lastSyncPacket = typeof(GameSave).GetField(
				"lastSyncPacket", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(lastSyncPacket, Is.Not.Null);
			lastSyncPacket.SetValue(save,
				packet ?? new byte[Order.SyncHashOrderLength]);
		}

		static Session.Client CurrentHuman(bool isAdmin) => new()
		{
			Index = 1,
			Name = "Current Human",
			IPAddress = "192.0.2.25",
			AnonymizedIPAddress = "192.0.2.*",
			Location = "Test Region",
			ConnectionQuality = Session.ConnectionQuality.Poor,
			State = Session.ClientState.Ready,
			MapUid = "current-map",
			MapPhase = Session.ClientMapPhase.Ready,
			MapProgress = 100,
			Faction = "Soviets",
			SpawnPoint = 1,
			Team = 1,
			Handicap = 0,
			Slot = "current-human-slot",
			IsAdmin = isAdmin,
			Fingerprint = "current-human-fingerprint",
		};

		static Session.Client CurrentBot() => new()
		{
			Index = 2,
			Name = "Old Bot",
			State = Session.ClientState.NotReady,
			Faction = "Soviets",
			Slot = "current-bot-slot",
			Bot = "normal",
			BotControllerClientIndex = 1,
		};

		static Session.Client SavedHuman(string slot) => new()
		{
			Index = 80,
			Name = "Ignored Saved Human Name",
			Faction = "Allies",
			SpawnPoint = 3,
			Team = 2,
			Handicap = 5,
			Slot = slot,
			IsAdmin = true,
		};

		static Session.Client SavedBot(string slot, string botType, string name) => new()
		{
			Index = 81,
			Name = name,
			Faction = "Allies",
			SpawnPoint = 4,
			Team = 2,
			Handicap = 10,
			Slot = slot,
			Bot = botType,
		};

		static string ClientSnapshot(Session.Client client) =>
			$"{client.IPAddress}\n{client.Serialize().Value.Nodes.WriteToString()}";

		static void AssertClientsUnchanged(
			IReadOnlyList<Session.Client> clients, IReadOnlyList<string> snapshots)
		{
			Assert.That(clients.Select(ClientSnapshot), Is.EqualTo(snapshots));
		}

		static void AssertPublishedStateUnchanged(
			GameServer server,
			GameSave gameSave,
			Session.Global globalSettings,
			Dictionary<string, Session.Slot> slots,
			List<Session.Client> clients,
			Session.Client human,
			Session.Client bot,
			IReadOnlyList<string> snapshots)
		{
			Assert.Multiple(() =>
			{
				Assert.That(server.GameSave, Is.SameAs(gameSave));
				Assert.That(server.LobbyInfo.GlobalSettings, Is.SameAs(globalSettings));
				Assert.That(server.LobbyInfo.Slots, Is.SameAs(slots));
				Assert.That(server.LobbyInfo.Clients, Is.SameAs(clients));
				Assert.That(server.LobbyInfo.Clients[0], Is.SameAs(human));
				Assert.That(server.LobbyInfo.Clients[1], Is.SameAs(bot));
			});
			AssertClientsUnchanged(server.LobbyInfo.Clients, snapshots);
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !File.Exists(Path.Combine(
				directory.FullName, "engine", "OpenRA.Game", "Game.cs")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		static int CompatibilityIndex(string source, int startIndex)
		{
			var start = Math.Max(startIndex, 0);
			var storage = source.IndexOf(
				"RuntimeContractStorage.Compatibility(", start, StringComparison.Ordinal);
			var metadata = source.IndexOf(
				".RuntimeContractCompatibilityWith(", start, StringComparison.Ordinal);
			if (storage < 0)
				return metadata;
			if (metadata < 0)
				return storage;

			return Math.Min(storage, metadata);
		}

		static string MethodBody(string source, string signature)
		{
			var start = source.IndexOf(signature, StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing block {signature}.");
			var openBrace = source.IndexOf('{', start);
			Assert.That(openBrace, Is.GreaterThan(start), $"Missing body for {signature}.");

			var depth = 0;
			for (var i = openBrace; i < source.Length; i++)
			{
				if (source[i] == '{')
					depth++;
				else if (source[i] == '}' && --depth == 0)
					return source.Substring(openBrace, i - openBrace + 1);
			}

			Assert.Fail($"Unterminated body for {signature}.");
			return null;
		}

		sealed class ThrowingCopyMemoryStream : MemoryStream
		{
			readonly IOException failure;

			public ThrowingCopyMemoryStream(byte[] bytes, IOException failure)
				: base(bytes)
			{
				this.failure = failure;
			}

			public override void CopyTo(Stream destination, int bufferSize)
			{
				var value = ReadByte();
				if (value >= 0)
					destination.WriteByte((byte)value);

				throw failure;
			}
		}

		sealed class ThrowingReadOnlyList<T> : IReadOnlyList<T>
		{
			readonly Exception failure;

			public ThrowingReadOnlyList(Exception failure)
			{
				this.failure = failure;
			}

			public int Count => throw failure;
			public T this[int index] => throw failure;

			public IEnumerator<T> GetEnumerator() => throw failure;

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
				GetEnumerator();
		}
	}
}
