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
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.FileSystem;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class PreGameEngineContextTest
	{
		const string MinimalManifest =
			"Metadata:\n" +
			"\tTitle: Test\n" +
			"\tVersion: test\n" +
			"\tWebsite: https://example.invalid\n" +
			"\tWebIcon32: icon.png\n" +
			"\tWindowTitle: Test\n" +
			"FileSystem: DefaultFileSystem\n" +
			"LoadScreen: BlankLoadScreen\n" +
			"Assemblies: OpenRA.Mods.Common.dll\n" +
			"TerrainFormat: DefaultTerrain\n" +
			"SpriteSequenceFormat: DefaultSpriteSequence\n";

		[Test]
		public void RuntimeContractIdentityHasClosedLegacyAndProfileStates()
		{
			var digest = new string('a', 64);
			var first = new RuntimeContractIdentity("base", digest);
			var same = new RuntimeContractIdentity("base", digest);
			var yuri = new RuntimeContractIdentity("yuri", digest);

			Assert.Multiple(() =>
			{
				Assert.That(RuntimeContractIdentity.Legacy.IsLegacy, Is.True);
				Assert.That(RuntimeContractIdentity.Legacy.ProfileId, Is.Empty);
				Assert.That(RuntimeContractIdentity.Legacy.ContractDigest, Is.Empty);
				Assert.That(first.IsLegacy, Is.False);
				Assert.That(first.ProfileId, Is.EqualTo("base"));
				Assert.That(first.ContractDigest, Is.EqualTo(digest));
				Assert.That(first == same, Is.True);
				Assert.That(first != same, Is.False);
				Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
				Assert.That(first, Is.Not.EqualTo(yuri));
				Assert.That(default(RuntimeContractIdentity).GetHashCode(), Is.EqualTo(0));
			});
		}

		[TestCase("")]
		[TestCase("Base")]
		[TestCase("base_profile")]
		[TestCase("base/profile")]
		[TestCase("profile-name-that-is-longer-than-thirty-two-characters")]
		public void RuntimeContractIdentityRejectsInvalidProfiles(string profile)
		{
			Assert.Throws<ArgumentException>(() =>
				new RuntimeContractIdentity(profile, new string('a', 64)));
		}

		[TestCase("")]
		[TestCase("0")]
		[TestCase("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
		[TestCase("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
		public void RuntimeContractIdentityRejectsInvalidDigests(string digest)
		{
			Assert.Throws<ArgumentException>(() => new RuntimeContractIdentity("base", digest));
		}

		[Test]
		[Timeout(10000)]
		public void ConcurrentDirectoryConfigurationPublishesOneCompletePairAndRejectsTheLoser()
		{
			var originalSynchronization = Platform.DirectoryConfigurationSynchronization;
			var originalEngineAccessed = ReadStaticField(typeof(Platform), "engineDirAccessed");
			var originalEngine = ReadStaticField(typeof(Platform), "engineDir");
			var originalSupportInitialized = ReadStaticField(typeof(Platform), "supportDirInitialized");
			var originalSystemSupport = ReadStaticField(typeof(Platform), "systemSupportPath");
			var originalLegacySupport = ReadStaticField(typeof(Platform), "legacyUserSupportPath");
			var originalModernSupport = ReadStaticField(typeof(Platform), "modernUserSupportPath");
			var originalUserSupport = ReadStaticField(typeof(Platform), "userSupportPath");
			var root = Path.Combine(Path.GetTempPath(), "openra-platform-pair-" + Guid.NewGuid());
			var engineA = Path.Combine(root, "engine-a");
			var supportA = Path.Combine(root, "support-a");
			var engineB = Path.Combine(root, "engine-b");
			var supportB = Path.Combine(root, "support-b");
			System.Threading.Tasks.Task first = null;
			System.Threading.Tasks.Task second = null;
			var synchronization = new ControlledDirectoryConfigurationSynchronization(
				ReadStaticField(typeof(Platform), "DirectorySync"));
			Exception firstFailure = null;
			Exception secondFailure = null;
			try
			{
				Directory.CreateDirectory(engineA);
				Directory.CreateDirectory(supportA);
				Directory.CreateDirectory(engineB);
				Directory.CreateDirectory(supportB);
				SetStaticField(typeof(Platform), "engineDirAccessed", false);
				SetStaticField(typeof(Platform), "engineDir", null);
				SetStaticField(typeof(Platform), "supportDirInitialized", false);
				SetStaticField(typeof(Platform), "systemSupportPath", null);
				SetStaticField(typeof(Platform), "legacyUserSupportPath", null);
				SetStaticField(typeof(Platform), "modernUserSupportPath", null);
				SetStaticField(typeof(Platform), "userSupportPath", null);
				Platform.DirectoryConfigurationSynchronization = synchronization;

				first = System.Threading.Tasks.Task.Factory.StartNew(() =>
				{
					try { Platform.ConfigureDirectories(engineA, supportA); }
					catch (Exception exception) { firstFailure = exception; }
				}, System.Threading.CancellationToken.None,
					System.Threading.Tasks.TaskCreationOptions.LongRunning,
					System.Threading.Tasks.TaskScheduler.Default);
				second = System.Threading.Tasks.Task.Factory.StartNew(() =>
				{
					try { Platform.ConfigureDirectories(engineB, supportB); }
					catch (Exception exception) { secondFailure = exception; }
				}, System.Threading.CancellationToken.None,
					System.Threading.Tasks.TaskCreationOptions.LongRunning,
					System.Threading.Tasks.TaskScheduler.Default);
				System.Threading.Tasks.Task.WaitAll(first, second);

				string Normalized(string path) =>
					Path.GetFullPath(path) + Path.DirectorySeparatorChar;
				var publishedEngine = Platform.EngineDir;
				var publishedSupport = Platform.SupportDir;
				var publishedA = publishedEngine == Normalized(engineA) &&
					publishedSupport == Normalized(supportA);
				var publishedB = publishedEngine == Normalized(engineB) &&
					publishedSupport == Normalized(supportB);
				Assert.Multiple(() =>
				{
					Assert.That(new[] { firstFailure, secondFailure }.Count(failure => failure == null),
						Is.EqualTo(1), "Exactly one different directory pair may win publication.");
					Assert.That(new[] { firstFailure, secondFailure }.Count(
						failure => failure is InvalidOperationException), Is.EqualTo(1));
					Assert.That(publishedA || publishedB, Is.True,
						"Readers must never observe an engine/support pair from different contenders.");
				});

				var winnerEngine = publishedA ? engineA : engineB;
				var winnerSupport = publishedA ? supportA : supportB;
				var loserEngine = publishedA ? engineB : engineA;
				var loserSupport = publishedA ? supportB : supportA;
				Assert.DoesNotThrow(() => Platform.ConfigureDirectories(winnerEngine, winnerSupport),
					"Reconfiguring the exact normalized winning pair must be idempotent.");
				Assert.Throws<InvalidOperationException>(() =>
					Platform.ConfigureDirectories(loserEngine, loserSupport));
				Assert.Multiple(() =>
				{
					Assert.That(Platform.EngineDir, Is.EqualTo(publishedEngine));
					Assert.That(Platform.SupportDir, Is.EqualTo(publishedSupport));
				});

				synchronization.ThrowOnNotification = true;
				Assert.DoesNotThrow(() => Platform.ConfigureDirectories(winnerEngine, winnerSupport),
					"A test observer must not be able to interrupt atomic pair publication.");
				Assert.Multiple(() =>
				{
					Assert.That(Platform.EngineDir, Is.EqualTo(publishedEngine));
					Assert.That(Platform.SupportDir, Is.EqualTo(publishedSupport));
				});
			}
			finally
			{
				synchronization.Release();
				if (first != null)
					try { first.GetAwaiter().GetResult(); }
					catch { }
				if (second != null)
					try { second.GetAwaiter().GetResult(); }
					catch { }
				Platform.DirectoryConfigurationSynchronization = originalSynchronization;
				SetStaticField(typeof(Platform), "engineDir", originalEngine);
				SetStaticField(typeof(Platform), "engineDirAccessed", originalEngineAccessed);
				SetStaticField(typeof(Platform), "systemSupportPath", originalSystemSupport);
				SetStaticField(typeof(Platform), "legacyUserSupportPath", originalLegacySupport);
				SetStaticField(typeof(Platform), "modernUserSupportPath", originalModernSupport);
				SetStaticField(typeof(Platform), "userSupportPath", originalUserSupport);
				SetStaticField(typeof(Platform), "supportDirInitialized", originalSupportInitialized);
				synchronization.Dispose();
				if (Directory.Exists(root))
					Directory.Delete(root, true);
			}
		}

		[Test]
		public void DirectorySynchronizationNotificationCannotInterruptPairPublication()
		{
			var originalSynchronization = Platform.DirectoryConfigurationSynchronization;
			var originalEngineAccessed = ReadStaticField(typeof(Platform), "engineDirAccessed");
			var originalEngine = ReadStaticField(typeof(Platform), "engineDir");
			var originalSupportInitialized = ReadStaticField(typeof(Platform), "supportDirInitialized");
			var originalSystemSupport = ReadStaticField(typeof(Platform), "systemSupportPath");
			var originalLegacySupport = ReadStaticField(typeof(Platform), "legacyUserSupportPath");
			var originalModernSupport = ReadStaticField(typeof(Platform), "modernUserSupportPath");
			var originalUserSupport = ReadStaticField(typeof(Platform), "userSupportPath");
			var root = Path.Combine(Path.GetTempPath(), "openra-platform-notify-" + Guid.NewGuid());
			var engine = Path.Combine(root, "engine");
			var support = Path.Combine(root, "support");
			try
			{
				Directory.CreateDirectory(engine);
				Directory.CreateDirectory(support);
				SetStaticField(typeof(Platform), "engineDirAccessed", false);
				SetStaticField(typeof(Platform), "engineDir", null);
				SetStaticField(typeof(Platform), "supportDirInitialized", false);
				SetStaticField(typeof(Platform), "systemSupportPath", null);
				SetStaticField(typeof(Platform), "legacyUserSupportPath", null);
				SetStaticField(typeof(Platform), "modernUserSupportPath", null);
				SetStaticField(typeof(Platform), "userSupportPath", null);
				Platform.DirectoryConfigurationSynchronization =
					new ThrowingDirectoryConfigurationSynchronization();

				Assert.DoesNotThrow(() => Platform.ConfigureDirectories(engine, support));
				var normalizedEngine = Path.GetFullPath(engine) + Path.DirectorySeparatorChar;
				var normalizedSupport = Path.GetFullPath(support) + Path.DirectorySeparatorChar;
				Assert.Multiple(() =>
				{
					Assert.That(Platform.EngineDir, Is.EqualTo(normalizedEngine));
					Assert.That(Platform.SupportDir, Is.EqualTo(normalizedSupport));
				});
			}
			finally
			{
				Platform.DirectoryConfigurationSynchronization = originalSynchronization;
				SetStaticField(typeof(Platform), "engineDir", originalEngine);
				SetStaticField(typeof(Platform), "engineDirAccessed", originalEngineAccessed);
				SetStaticField(typeof(Platform), "systemSupportPath", originalSystemSupport);
				SetStaticField(typeof(Platform), "legacyUserSupportPath", originalLegacySupport);
				SetStaticField(typeof(Platform), "modernUserSupportPath", originalModernSupport);
				SetStaticField(typeof(Platform), "userSupportPath", originalUserSupport);
				SetStaticField(typeof(Platform), "supportDirInitialized", originalSupportInitialized);
				if (Directory.Exists(root))
					Directory.Delete(root, true);
			}
		}

		[Test]
		public void OwnedBatchRegistrationIsAtomicAndDisposesInReverseSubmissionOrder()
		{
			var events = new List<string>();
			var first = new FirstService(events);
			var second = new SecondService(events);
			var collection = new ModDataServiceCollection();

			Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
				ModDataServiceRegistration.Owned<IFirstService>(first),
				ModDataServiceRegistration.Owned<ISecondService>(second))), Is.True);
			Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
				ModDataServiceRegistration.Owned<IFirstService>(new FirstService(events)))), Is.False);
			collection.Dispose();

			Assert.That(events, Is.EqualTo(new[] { "second", "first" }));
			collection.Dispose();
			Assert.That(events, Is.EqualTo(new[] { "second", "first" }));
		}

		[Test]
		public void RejectedBatchRetainsCallerOwnershipAndDoesNotPartiallyRegister()
		{
			var events = new List<string>();
			var duplicate = new FirstService(events);
			using var collection = new ModDataServiceCollection();
			var registrations = ImmutableArray.Create(
				ModDataServiceRegistration.Owned<IFirstService>(duplicate),
				ModDataServiceRegistration.Owned<IFirstService>(duplicate));

			Assert.That(collection.TryAddOwnedBatch(registrations), Is.False);
			Assert.That(events, Is.Empty);
			duplicate.Dispose();
			Assert.That(events, Is.EqualTo(new[] { "first" }));
		}

		[Test]
		public void ServiceRegistrationUsesTypeAndReferenceIdentity()
		{
			var service = new FirstService(new List<string>());
			var first = ModDataServiceRegistration.Owned<IFirstService>(service);
			var same = ModDataServiceRegistration.Owned<IFirstService>(service);
			var other = ModDataServiceRegistration.Owned<IFirstService>(new FirstService(new List<string>()));

			Assert.Multiple(() =>
			{
				Assert.That(first.ServiceType, Is.SameAs(typeof(IFirstService)));
				Assert.That(first.Service, Is.SameAs(service));
				Assert.That(first, Is.EqualTo(same));
				Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
				Assert.That(first, Is.Not.EqualTo(other));
				Assert.That(default(ModDataServiceRegistration).GetHashCode(), Is.EqualTo(0));
			});
		}

		[Test]
		public void TrustedOwnershipShellsHaveNoPublicConstructionOrRecordCopySurface()
		{
			foreach (var type in new[]
			{
				typeof(ModDataConstructionOptions),
				typeof(EngineStartupState),
			})
			{
				Assert.Multiple(() =>
				{
					Assert.That(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance), Is.Empty,
						type.FullName);
					Assert.That(type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
						.Any(method => method.Name == "Clone"), Is.False, type.FullName);
					Assert.That(type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
						.Any(property => property.SetMethod != null), Is.False, type.FullName);
				});
			}
		}

		[Test]
		public void OwnershipShellAliasesBecomeInertAfterTheirSingleTransfer()
		{
			var service = new CountingOwnedService();
			using var collection = new ModDataServiceCollection();
			Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
				ModDataServiceRegistration.Owned<ICountingOwnedService>(service))), Is.True);
			var settings = ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments()));
			var options = collection.Seal(
				loader => loader,
				settings,
				FluentInitializationMode.PublishGlobal,
				ModDataRenderingMode.Game);
			var construction = options.TakeOwnership();
			options.Dispose();

			Assert.Multiple(() =>
			{
				Assert.That(service.DisposeCalls, Is.Zero);
				Assert.Throws<InvalidOperationException>(() => options.TakeOwnership());
			});

			var configuration = new EngineBootstrapConfiguration(
				Platform.EngineDir,
				Platform.SupportDir,
				ImmutableArray<string>.Empty,
				ImmutableArray<string>.Empty,
				new Arguments());
			var startup = new EngineStartupState(
				configuration,
				new TrustedManifestSelection("ra2", "mod.ios-base.yaml"),
				new RuntimeContractIdentity("base", new string('a', 64)),
				construction);
			var ownedStartup = startup.TakeOwnership();
			startup.Dispose();

			Assert.Multiple(() =>
			{
				Assert.That(service.DisposeCalls, Is.Zero);
				Assert.Throws<InvalidOperationException>(() => startup.TakeOwnership());
			});

			ownedStartup.Dispose();
			Assert.That(service.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void OwnershipShellsPermitExactlyOneConcurrentTransfer()
		{
			var optionsService = new CountingOwnedService();
			using (var collection = new ModDataServiceCollection())
			{
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<ICountingOwnedService>(optionsService))), Is.True);
				var options = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
					FluentInitializationMode.PublishGlobal,
					ModDataRenderingMode.Game);
				var owner = RaceSingleOwner(options.TakeOwnership);
				options.Dispose();
				Assert.That(optionsService.DisposeCalls, Is.Zero);
				owner.Dispose();
				Assert.That(optionsService.DisposeCalls, Is.EqualTo(1));
			}

			var startupService = new CountingOwnedService();
			using (var collection = new ModDataServiceCollection())
			{
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<ICountingOwnedService>(startupService))), Is.True);
				var options = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
					FluentInitializationMode.PublishGlobal,
					ModDataRenderingMode.Game);
				var startup = new EngineStartupState(
					new EngineBootstrapConfiguration(
						Platform.EngineDir,
						Platform.SupportDir,
						ImmutableArray<string>.Empty,
						ImmutableArray<string>.Empty,
						new Arguments()),
					new TrustedManifestSelection("ra2", "mod.ios-base.yaml"),
					new RuntimeContractIdentity("base", new string('b', 64)),
					options.TakeOwnership());
				var owner = RaceSingleOwner(startup.TakeOwnership);
				startup.Dispose();
				options.Dispose();
				Assert.That(startupService.DisposeCalls, Is.Zero);
				owner.Dispose();
				Assert.That(startupService.DisposeCalls, Is.EqualTo(1));
			}
		}

		[Test]
		public void OwnershipShellsSerializeConcurrentTransferAgainstAliasDisposal()
		{
			var optionsService = new CountingOwnedService();
			using (var collection = new ModDataServiceCollection())
			{
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<ICountingOwnedService>(optionsService))), Is.True);
				var options = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
					FluentInitializationMode.PublishGlobal,
					ModDataRenderingMode.Game);
				var owner = RaceOwnerAgainstDisposal(options.TakeOwnership, options.Dispose);
				Assert.That(optionsService.DisposeCalls, Is.EqualTo(owner == null ? 1 : 0));
				owner?.Dispose();
				options.Dispose();
				Assert.That(optionsService.DisposeCalls, Is.EqualTo(1));
			}

			var startupService = new CountingOwnedService();
			using (var collection = new ModDataServiceCollection())
			{
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<ICountingOwnedService>(startupService))), Is.True);
				var options = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
					FluentInitializationMode.PublishGlobal,
					ModDataRenderingMode.Game);
				var startup = new EngineStartupState(
					new EngineBootstrapConfiguration(
						Platform.EngineDir,
						Platform.SupportDir,
						ImmutableArray<string>.Empty,
						ImmutableArray<string>.Empty,
						new Arguments()),
					new TrustedManifestSelection("ra2", "mod.ios-base.yaml"),
					new RuntimeContractIdentity("base", new string('c', 64)),
					options.TakeOwnership());
				var owner = RaceOwnerAgainstDisposal(startup.TakeOwnership, startup.Dispose);
				Assert.That(startupService.DisposeCalls, Is.EqualTo(owner == null ? 1 : 0));
				owner?.Dispose();
				startup.Dispose();
				options.Dispose();
				Assert.That(startupService.DisposeCalls, Is.EqualTo(1));
			}
		}

		[Test]
		public void EngineStartupStateSerializesRepeatedConcurrentTransferAgainstDisposal()
		{
			const int TakeWorkerCount = 8;
			const int DisposeWorkerCount = 8;
			const int RoundCount = 256;
			var configuration = new EngineBootstrapConfiguration(
				Platform.EngineDir,
				Platform.SupportDir,
				ImmutableArray<string>.Empty,
				ImmutableArray<string>.Empty,
				new Arguments());
			var settings = ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments()));
			var states = new EngineStartupState[RoundCount];
			var services = new CountingOwnedService[RoundCount];
			var owners = Enumerable.Range(0, RoundCount)
				.Select(_ => new List<OwnedEngineStartupState>()).ToArray();
			var rejected = new int[RoundCount];
			var failures = new List<Exception>();
			for (var round = 0; round < RoundCount; round++)
			{
				services[round] = new CountingOwnedService();
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<ICountingOwnedService>(services[round]))), Is.True);
				using var options = collection.Seal(
					loader => loader,
					settings,
					FluentInitializationMode.PublishGlobal,
					ModDataRenderingMode.Game);
				states[round] = new EngineStartupState(
					configuration,
					new TrustedManifestSelection("ra2", "mod.ios-base.yaml"),
					new RuntimeContractIdentity("base", new string('d', 64)),
					options.TakeOwnership());
			}

			using var start = new System.Threading.Barrier(TakeWorkerCount + DisposeWorkerCount + 1);
			using var finish = new System.Threading.Barrier(TakeWorkerCount + DisposeWorkerCount + 1);
			System.Threading.Tasks.Task TakeWorker() => System.Threading.Tasks.Task.Factory.StartNew(() =>
			{
				for (var round = 0; round < RoundCount; round++)
				{
					start.SignalAndWait();
					try
					{
						var owner = states[round].TakeOwnership();
						lock (owners[round])
							owners[round].Add(owner);
					}
					catch (InvalidOperationException)
					{
						System.Threading.Interlocked.Increment(ref rejected[round]);
					}
					catch (Exception exception)
					{
						lock (failures)
							failures.Add(exception);
					}

					finish.SignalAndWait();
				}
			}, System.Threading.CancellationToken.None,
				System.Threading.Tasks.TaskCreationOptions.LongRunning,
				System.Threading.Tasks.TaskScheduler.Default);
			System.Threading.Tasks.Task DisposeWorker() => System.Threading.Tasks.Task.Factory.StartNew(() =>
			{
				for (var round = 0; round < RoundCount; round++)
				{
					start.SignalAndWait();
					try { states[round].Dispose(); }
					catch (Exception exception)
					{
						lock (failures)
							failures.Add(exception);
					}

					finish.SignalAndWait();
				}
			}, System.Threading.CancellationToken.None,
				System.Threading.Tasks.TaskCreationOptions.LongRunning,
				System.Threading.Tasks.TaskScheduler.Default);

			var workers = Enumerable.Range(0, TakeWorkerCount).Select(_ => TakeWorker())
				.Concat(Enumerable.Range(0, DisposeWorkerCount).Select(_ => DisposeWorker()))
				.ToArray();
			for (var round = 0; round < RoundCount; round++)
			{
				start.SignalAndWait();
				finish.SignalAndWait();
			}

			System.Threading.Tasks.Task.WaitAll(workers);
			var violations = Enumerable.Range(0, RoundCount).Where(round =>
				owners[round].Count > 1 ||
				rejected[round] != TakeWorkerCount - owners[round].Count ||
				services[round].DisposeCalls != (owners[round].Count == 0 ? 1 : 0)).ToArray();
			foreach (var roundOwners in owners)
				foreach (var owner in roundOwners)
					owner.Dispose();
			foreach (var state in states)
				state.Dispose();

			Assert.Multiple(() =>
			{
				Assert.That(failures, Is.Empty);
				Assert.That(violations, Is.Empty);
				Assert.That(services.Select(service => service.DisposeCalls), Is.All.EqualTo(1));
			});
		}

		[Test]
		public void PreGameCapturesSettingsBeforeOpeningInstalledMods()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "PreGameEngineContext.cs"));
			var capture = source.IndexOf("var settings = ModDataSettingsSnapshot.Capture(localSettings);", StringComparison.Ordinal);
			var installedMods = source.IndexOf("new InstalledMods(", StringComparison.Ordinal);

			Assert.Multiple(() =>
			{
				Assert.That(capture, Is.GreaterThanOrEqualTo(0));
				Assert.That(installedMods, Is.GreaterThan(capture));
			});
		}

		[Test]
		[Timeout(10000)]
		public void ConcurrentOwnedEngineStartupDisposeReleasesStrictProviderExactlyOnce()
		{
			var provider = new BlockingDisposeProvider();
			var construction = new OwnedModDataConstructionState(
				provider,
				loader => loader,
				ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
				FluentInitializationMode.PublishGlobal,
				ModDataRenderingMode.Game);
			var owned = new OwnedEngineStartupState(
				new EngineBootstrapConfiguration(
					Platform.EngineDir,
					Platform.SupportDir,
					ImmutableArray<string>.Empty,
					ImmutableArray<string>.Empty,
					new Arguments()),
				new TrustedManifestSelection("ra2", "mod.ios-base.yaml"),
				new RuntimeContractIdentity("base", new string('f', 64)),
				construction);
			System.Threading.Tasks.Task first = null;
			System.Threading.Tasks.Task second = null;
			try
			{
				first = System.Threading.Tasks.Task.Factory.StartNew(
					owned.Dispose,
					System.Threading.CancellationToken.None,
					System.Threading.Tasks.TaskCreationOptions.LongRunning,
					System.Threading.Tasks.TaskScheduler.Default);
				provider.FirstEntered.Wait();
				second = System.Threading.Tasks.Task.Factory.StartNew(
					owned.Dispose,
					System.Threading.CancellationToken.None,
					System.Threading.Tasks.TaskCreationOptions.LongRunning,
					System.Threading.Tasks.TaskScheduler.Default);

				var outcome = System.Threading.WaitHandle.WaitAny(new[]
				{
					((IAsyncResult)second).AsyncWaitHandle,
					provider.SecondEntered.WaitHandle,
				});
				provider.Release.Set();
				System.Threading.Tasks.Task.WaitAll(first, second);

				Assert.Multiple(() =>
				{
					Assert.That(outcome, Is.Zero,
						"A second owner disposal must return without entering the detached provider.");
					Assert.That(provider.DisposeCalls, Is.EqualTo(1));
					Assert.That(owned.Construction, Is.Null);
				});
			}
			finally
			{
				provider.Release.Set();
				if (first != null && second != null)
					System.Threading.Tasks.Task.WaitAll(first, second);
				owned.Dispose();
				provider.DisposeSignals();
			}
		}

		[Test]
		public void BorrowWinsFinalDisposeKeepsProviderAliveUntilLeaseRelease()
		{
			var provider = new LeaseLifetimeProvider();
			var construction = new OwnedModDataConstructionState(
				provider,
				loader => loader,
				ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
				FluentInitializationMode.PublishGlobal,
				ModDataRenderingMode.Game);
			var borrow = construction.BorrowProvider();
			try
			{
				construction.Dispose();

				var found = borrow.TryGet<ILeaseLifetimeService>(out var resolved);
				Assert.Multiple(() =>
				{
					Assert.That(provider.DisposeCalls, Is.Zero,
						"Final ownership release must defer provider disposal while a borrow is live.");
					Assert.That(found, Is.True);
					Assert.That(resolved, Is.SameAs(provider.Service));
				});

				borrow.Dispose();
				Assert.That(provider.DisposeCalls, Is.EqualTo(1));
			}
			finally
			{
				borrow.Dispose();
				construction.Dispose();
			}

			Assert.That(provider.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		[Timeout(10000)]
		public void ConcurrentAndRepeatedBorrowLeaseDisposeReleasesProviderExactlyOnce()
		{
			var provider = new LeaseLifetimeProvider();
			var construction = new OwnedModDataConstructionState(
				provider,
				loader => loader,
				ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
				FluentInitializationMode.PublishGlobal,
				ModDataRenderingMode.Game);
			var borrow = construction.BorrowProvider();
			using var start = new System.Threading.Barrier(3);
			var disposals = Enumerable.Range(0, 2)
				.Select(_ => System.Threading.Tasks.Task.Factory.StartNew(
					() =>
					{
						start.SignalAndWait();
						borrow.Dispose();
					},
					System.Threading.CancellationToken.None,
					System.Threading.Tasks.TaskCreationOptions.LongRunning,
					System.Threading.Tasks.TaskScheduler.Default))
				.ToArray();

			construction.Dispose();
			start.SignalAndWait();
			System.Threading.Tasks.Task.WaitAll(disposals);
			borrow.Dispose();
			construction.Dispose();

			Assert.That(provider.DisposeCalls, Is.EqualTo(1));
		}

		[Test]
		public void BorrowLeaseDisposeDefersProviderReleaseUntilInFlightUseReturns()
		{
			var provider = new LeaseLifetimeProvider();
			var construction = new OwnedModDataConstructionState(
				provider,
				loader => loader,
				ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
				FluentInitializationMode.PublishGlobal,
				ModDataRenderingMode.Game);
			var borrow = construction.BorrowProvider();
			provider.DuringTryGet = borrow.Dispose;
			construction.Dispose();

			var found = borrow.TryGet<ILeaseLifetimeService>(out var resolved);

			Assert.Multiple(() =>
			{
				Assert.That(found, Is.True);
				Assert.That(resolved, Is.SameAs(provider.Service));
				Assert.That(provider.DisposedDuringUse, Is.False,
					"Disposing a lease from an in-flight call must defer final provider release.");
				Assert.That(provider.DisposeCalls, Is.EqualTo(1));
			});
		}

		[Test]
		public void BorrowLeaseDisposeDefersProviderReleaseUntilRegistrationsReturns()
		{
			var provider = new LeaseLifetimeProvider();
			var construction = new OwnedModDataConstructionState(
				provider,
				loader => loader,
				ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
				FluentInitializationMode.PublishGlobal,
				ModDataRenderingMode.Game);
			var borrow = construction.BorrowProvider();
			provider.DuringRegistrations = borrow.Dispose;
			construction.Dispose();

			var registrations = borrow.Registrations;

			Assert.Multiple(() =>
			{
				Assert.That(registrations, Is.Empty);
				Assert.That(provider.DisposedDuringUse, Is.False,
					"Disposing a lease from Registrations must defer final provider release.");
				Assert.That(provider.DisposeCalls, Is.EqualTo(1));
			});
		}

		[Test]
		public void ActiveBorrowPreventsProviderTransferUntilLeaseRelease()
		{
			var provider = new LeaseLifetimeProvider();
			var construction = new OwnedModDataConstructionState(
				provider,
				loader => loader,
				ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
				FluentInitializationMode.PublishGlobal,
				ModDataRenderingMode.Game);
			var borrow = construction.BorrowProvider();
			try
			{
				Assert.Throws<InvalidOperationException>(() => construction.TakeProvider());
				Assert.That(borrow.TryGet<ILeaseLifetimeService>(out var resolved), Is.True);
				Assert.That(resolved, Is.SameAs(provider.Service));

				borrow.Dispose();
				var transferred = construction.TakeProvider();
				transferred.Dispose();
				Assert.That(provider.DisposeCalls, Is.EqualTo(1));
			}
			finally
			{
				borrow.Dispose();
				construction.Dispose();
				if (provider.DisposeCalls == 0)
					provider.Dispose();
			}
		}

		[Test]
		[Timeout(10000)]
		public void BorrowCannotObserveProviderAfterFinalOwnedDisposeStarts()
		{
			var provider = new BlockingDisposeProvider();
			var construction = new OwnedModDataConstructionState(
				provider,
				loader => loader,
				ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
				FluentInitializationMode.PublishGlobal,
				ModDataRenderingMode.Game);
			var owned = new OwnedEngineStartupState(
				new EngineBootstrapConfiguration(
					Platform.EngineDir,
					Platform.SupportDir,
					ImmutableArray<string>.Empty,
					ImmutableArray<string>.Empty,
					new Arguments()),
				new TrustedManifestSelection("ra2", "mod.ios-base.yaml"),
				new RuntimeContractIdentity("base", new string('f', 64)),
				construction);
			System.Threading.Tasks.Task dispose = null;
			try
			{
				dispose = System.Threading.Tasks.Task.Factory.StartNew(
					owned.Dispose,
					System.Threading.CancellationToken.None,
					System.Threading.Tasks.TaskCreationOptions.LongRunning,
					System.Threading.Tasks.TaskScheduler.Default);
				provider.FirstEntered.Wait();

				var ownerDetached = owned.Construction == null;
				Exception borrowFailure = null;
				try { construction.BorrowProvider(); }
				catch (Exception exception) { borrowFailure = exception; }
				provider.Release.Set();
				dispose.Wait();

				Assert.Multiple(() =>
				{
					Assert.That(ownerDetached, Is.True,
						"Final disposal must detach ownership before entering throwable provider cleanup.");
					Assert.That(borrowFailure, Is.TypeOf<InvalidOperationException>(),
						"A borrow that loses to final disposal must not observe the provider being disposed.");
					Assert.That(provider.DisposeCalls, Is.EqualTo(1));
				});
			}
			finally
			{
				provider.Release.Set();
				dispose?.Wait();
				owned.Dispose();
				provider.DisposeSignals();
			}
		}

		[Test]
		public void FailedPreGameConstructionDisposesTransferredManifestPackageExactlyOnce()
		{
			var fixture = CreateTestMod();
			var service = new PackageReplacingThrowingService();
			try
			{
				var configuration = new EngineBootstrapConfiguration(
					Platform.EngineDir,
					Platform.SupportDir,
					ImmutableArray.Create(fixture.ModsRoot),
					ImmutableArray<string>.Empty,
					new Arguments());
				using var context = PreGameEngineContext.Open(configuration);
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<IPackageReplacingService>(service))), Is.True);
				using var options = collection.Seal(
					loader => loader,
					context.Settings,
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);

				var exception = Assert.Throws<SentinelConstructionException>(() => context.OpenModData(
					new TrustedManifestSelection("test", "mod.ios-base.yaml"), options));

				Assert.Multiple(() =>
				{
					Assert.That(exception.Message, Is.EqualTo("bind failed"));
					Assert.That(service.Package, Is.Not.Null);
					Assert.That(service.Package.DisposeCalls, Is.EqualTo(1));
				});
			}
			finally
			{
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		public void ConcurrentPreGameScopeDisposalClosesTransferredManifestPackageExactlyOnce()
		{
			const int WorkerCount = 16;
			const int RoundCount = 128;
			var fixture = CreateTestMod();
			try
			{
				var configuration = new EngineBootstrapConfiguration(
					Platform.EngineDir,
					Platform.SupportDir,
					ImmutableArray.Create(fixture.ModsRoot),
					ImmutableArray<string>.Empty,
					new Arguments());
				using var context = PreGameEngineContext.Open(configuration);
				var scopes = new PreGameModDataScope[RoundCount];
				var packages = new StrictCountingPackage[RoundCount];
				for (var round = 0; round < RoundCount; round++)
				{
					var service = new PackageReplacingService();
					using var collection = new ModDataServiceCollection();
					Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
						ModDataServiceRegistration.Owned<IPackageReplacingService>(service))), Is.True);
					using var options = collection.Seal(
						loader => loader,
						context.Settings,
						FluentInitializationMode.Isolated,
						ModDataRenderingMode.NoRender);
					scopes[round] = context.OpenModData(
						new TrustedManifestSelection("test", "mod.ios-base.yaml"), options);
					packages[round] = service.Package;
				}

				using var start = new System.Threading.Barrier(WorkerCount + 1);
				using var finish = new System.Threading.Barrier(WorkerCount + 1);
				var workers = Enumerable.Range(0, WorkerCount).Select(_ =>
					System.Threading.Tasks.Task.Factory.StartNew(() =>
					{
						for (var round = 0; round < RoundCount; round++)
						{
							start.SignalAndWait();
							scopes[round].Dispose();
							finish.SignalAndWait();
						}
					}, System.Threading.CancellationToken.None,
						System.Threading.Tasks.TaskCreationOptions.LongRunning,
						System.Threading.Tasks.TaskScheduler.Default)).ToArray();

				for (var round = 0; round < RoundCount; round++)
				{
					start.SignalAndWait();
					finish.SignalAndWait();
				}

				System.Threading.Tasks.Task.WaitAll(workers);
				foreach (var scope in scopes)
					scope.Dispose();
				Assert.That(packages.Select(package => package.DisposeCalls), Is.All.EqualTo(1));
			}
			finally
			{
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		public void PreGameScopeTakeOwnershipInvalidatesCallerAliasWithoutDisposingResource()
		{
			var fixture = CreateTestMod();
			try
			{
				var configuration = new EngineBootstrapConfiguration(
					Platform.EngineDir,
					Platform.SupportDir,
					ImmutableArray.Create(fixture.ModsRoot),
					ImmutableArray<string>.Empty,
					new Arguments());
				using var context = PreGameEngineContext.Open(configuration);
				var service = new PackageReplacingService();
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<IPackageReplacingService>(service))), Is.True);
				using var options = collection.Seal(
					loader => loader,
					context.Settings,
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				using var callerScope = context.OpenModData(
					new TrustedManifestSelection("test", "mod.ios-base.yaml"), options);
				var modData = callerScope.ModData;
				using var ownedScope = callerScope.TakeOwnership();

				Assert.Multiple(() =>
				{
					Assert.That(callerScope.ModData, Is.Null);
					Assert.That(ownedScope.ModData, Is.SameAs(modData));
					Assert.That(service.Package.DisposeCalls, Is.Zero);
					Assert.Throws<InvalidOperationException>(() => callerScope.TakeOwnership());
				});

				callerScope.Dispose();
				Assert.That(service.Package.DisposeCalls, Is.Zero);
				ownedScope.Dispose();
				Assert.That(service.Package.DisposeCalls, Is.EqualTo(1));
			}
			finally
			{
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		public void PreGameScopeSerializesTakeOwnershipAgainstDispose()
		{
			var fixture = CreateTestMod();
			try
			{
				var configuration = new EngineBootstrapConfiguration(
					Platform.EngineDir,
					Platform.SupportDir,
					ImmutableArray.Create(fixture.ModsRoot),
					ImmutableArray<string>.Empty,
					new Arguments());
				using var context = PreGameEngineContext.Open(configuration);
				var service = new PackageReplacingService();
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<IPackageReplacingService>(service))), Is.True);
				using var options = collection.Seal(
					loader => loader,
					context.Settings,
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				using var callerScope = context.OpenModData(
					new TrustedManifestSelection("test", "mod.ios-base.yaml"), options);

				var owner = RaceOwnerAgainstDisposal(
					callerScope.TakeOwnership, callerScope.Dispose);
				Assert.That(service.Package.DisposeCalls, Is.EqualTo(owner == null ? 1 : 0));

				owner?.Dispose();
				callerScope.Dispose();
				Assert.That(service.Package.DisposeCalls, Is.EqualTo(1));
			}
			finally
			{
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		public void ConcurrentPreGameScopeTransfersPermitExactlyOneOwner()
		{
			const int WorkerCount = 16;
			const int RoundCount = 128;
			var fixture = CreateTestMod();
			try
			{
				var configuration = new EngineBootstrapConfiguration(
					Platform.EngineDir,
					Platform.SupportDir,
					ImmutableArray.Create(fixture.ModsRoot),
					ImmutableArray<string>.Empty,
					new Arguments());
				using var context = PreGameEngineContext.Open(configuration);
				var scopes = new PreGameModDataScope[RoundCount];
				var packages = new StrictCountingPackage[RoundCount];
				var owners = Enumerable.Range(0, RoundCount)
					.Select(_ => new List<PreGameModDataScope>()).ToArray();
				var rejected = new int[RoundCount];
				var failures = new List<Exception>();
				for (var round = 0; round < RoundCount; round++)
				{
					var service = new PackageReplacingService();
					using var collection = new ModDataServiceCollection();
					Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
						ModDataServiceRegistration.Owned<IPackageReplacingService>(service))), Is.True);
					using var options = collection.Seal(
						loader => loader,
						context.Settings,
						FluentInitializationMode.Isolated,
						ModDataRenderingMode.NoRender);
					scopes[round] = context.OpenModData(
						new TrustedManifestSelection("test", "mod.ios-base.yaml"), options);
					packages[round] = service.Package;
				}

				using var start = new System.Threading.Barrier(WorkerCount + 1);
				using var finish = new System.Threading.Barrier(WorkerCount + 1);
				var workers = Enumerable.Range(0, WorkerCount).Select(_ =>
					System.Threading.Tasks.Task.Factory.StartNew(() =>
					{
						for (var round = 0; round < RoundCount; round++)
						{
							start.SignalAndWait();
							try
							{
								var owner = scopes[round].TakeOwnership();
								lock (owners[round])
									owners[round].Add(owner);
							}
							catch (InvalidOperationException)
							{
								System.Threading.Interlocked.Increment(ref rejected[round]);
							}
							catch (Exception exception)
							{
								lock (failures)
									failures.Add(exception);
							}

							finish.SignalAndWait();
						}
					}, System.Threading.CancellationToken.None,
						System.Threading.Tasks.TaskCreationOptions.LongRunning,
						System.Threading.Tasks.TaskScheduler.Default)).ToArray();

				for (var round = 0; round < RoundCount; round++)
				{
					start.SignalAndWait();
					finish.SignalAndWait();
				}

				System.Threading.Tasks.Task.WaitAll(workers);
				var violations = Enumerable.Range(0, RoundCount).Where(round =>
					owners[round].Count != 1 || rejected[round] != WorkerCount - 1 ||
					scopes[round].ModData != null).ToArray();
				foreach (var scope in scopes)
					scope.Dispose();
				var disposeCallsBeforeOwnerRelease = packages.Select(package => package.DisposeCalls).ToArray();
				foreach (var roundOwners in owners)
					foreach (var owner in roundOwners)
						owner.Dispose();

				Assert.Multiple(() =>
				{
					Assert.That(failures, Is.Empty);
					Assert.That(violations, Is.Empty);
					Assert.That(disposeCallsBeforeOwnerRelease, Is.All.Zero);
					Assert.That(packages.Select(package => package.DisposeCalls), Is.All.EqualTo(1));
				});
			}
			finally
			{
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		public void ConcurrentModDataDisposalClosesTransferredManifestPackageExactlyOnce()
		{
			const int WorkerCount = 16;
			const int RoundCount = 128;
			var fixture = CreateTestMod();
			try
			{
				var configuration = new EngineBootstrapConfiguration(
					Platform.EngineDir,
					Platform.SupportDir,
					ImmutableArray.Create(fixture.ModsRoot),
					ImmutableArray<string>.Empty,
					new Arguments());
				using var context = PreGameEngineContext.Open(configuration);
				var scopes = new PreGameModDataScope[RoundCount];
				var modData = new ModData[RoundCount];
				var packages = new StrictCountingPackage[RoundCount];
				for (var round = 0; round < RoundCount; round++)
				{
					var service = new PackageReplacingService();
					using var collection = new ModDataServiceCollection();
					Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
						ModDataServiceRegistration.Owned<IPackageReplacingService>(service))), Is.True);
					using var options = collection.Seal(
						loader => loader,
						context.Settings,
						FluentInitializationMode.Isolated,
						ModDataRenderingMode.NoRender);
					scopes[round] = context.OpenModData(
						new TrustedManifestSelection("test", "mod.ios-base.yaml"), options);
					modData[round] = scopes[round].ModData;
					packages[round] = service.Package;
				}

				using var start = new System.Threading.Barrier(WorkerCount + 1);
				using var finish = new System.Threading.Barrier(WorkerCount + 1);
				var workers = Enumerable.Range(0, WorkerCount).Select(_ =>
					System.Threading.Tasks.Task.Factory.StartNew(() =>
					{
						for (var round = 0; round < RoundCount; round++)
						{
							start.SignalAndWait();
							modData[round].Dispose();
							finish.SignalAndWait();
						}
					}, System.Threading.CancellationToken.None,
						System.Threading.Tasks.TaskCreationOptions.LongRunning,
						System.Threading.Tasks.TaskScheduler.Default)).ToArray();

				for (var round = 0; round < RoundCount; round++)
				{
					start.SignalAndWait();
					finish.SignalAndWait();
				}

				System.Threading.Tasks.Task.WaitAll(workers);
				foreach (var scope in scopes)
					scope.Dispose();
				Assert.That(packages.Select(package => package.DisposeCalls), Is.All.EqualTo(1));
			}
			finally
			{
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		public void ProfiledStartupAuthorityIsInternalAndPresent()
		{
			var publicSeal = typeof(PreGameEngineContext).GetMethod(
				"SealForGame", BindingFlags.Public | BindingFlags.Instance);
			var publicStateOverload = typeof(Game).GetMethods(BindingFlags.Public | BindingFlags.Static)
				.SingleOrDefault(method => method.Name == nameof(Game.InitializeAndRun) &&
					method.GetParameters().Length == 1 &&
					method.GetParameters()[0].ParameterType == typeof(EngineStartupState));
			var internalSeal = typeof(PreGameEngineContext).GetMethod(
				"SealForGame", BindingFlags.NonPublic | BindingFlags.Instance);
			var internalStateOverload = typeof(Game).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
				.SingleOrDefault(method => method.Name == nameof(Game.InitializeAndRun) &&
					method.GetParameters().Length == 1 &&
					method.GetParameters()[0].ParameterType == typeof(EngineStartupState));
			var lifecycleBatch = typeof(Game).GetField(
				"LifecycleDispatchBatch", BindingFlags.NonPublic | BindingFlags.Static);
			var pump = typeof(Game).GetMethod(
				"PumpLifecycleDispatch", BindingFlags.NonPublic | BindingFlags.Static);
			var drain = typeof(Game).GetMethod(
				"DrainLifecycleDispatchUntil", BindingFlags.NonPublic | BindingFlags.Static);

			Assert.Multiple(() =>
			{
				Assert.That(publicSeal, Is.Null);
				Assert.That(publicStateOverload, Is.Null);
				Assert.That(internalSeal, Is.Not.Null);
				Assert.That(internalSeal?.ReturnType, Is.EqualTo(typeof(EngineStartupState)));
				Assert.That(internalStateOverload, Is.Not.Null);
				Assert.That(internalStateOverload?.ReturnType, Is.EqualTo(typeof(RunStatus)));
				Assert.That(lifecycleBatch, Is.Not.Null);
				Assert.That(lifecycleBatch?.GetRawConstantValue(), Is.EqualTo(64));
				Assert.That(pump, Is.Not.Null);
				Assert.That(pump?.ReturnType, Is.EqualTo(typeof(void)));
				Assert.That(pump?.GetParameters().Select(parameter => parameter.ParameterType),
					Is.EqualTo(new[] { typeof(IGameLifecycleDispatchLane) }));
				Assert.That(drain, Is.Not.Null);
				Assert.That(drain?.ReturnType, Is.EqualTo(typeof(void)));
				Assert.That(drain?.GetParameters().Select(parameter => parameter.ParameterType),
					Is.EqualTo(new[] { typeof(IGameLifecycleDispatchLane), typeof(System.Threading.Tasks.Task) }));
			});
		}

		[Test]
		public void GameOwnedConstructionBorrowsProviderAcrossTwoModEpochs()
		{
			var fixture = CreateTestMod();
			var events = new List<string>();
			var service = new EpochService(events);
			try
			{
				var selection = new TrustedManifestSelection("test", "mod.ios-base.yaml");
				using var mods = new InstalledMods(
					new[] { fixture.ModsRoot }, Array.Empty<string>());
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<IEpochService>(service))), Is.True);
				using var options = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				using var construction = options.TakeOwnership();

				for (var epoch = 1; epoch <= 2; epoch++)
				{
					var manifest = mods.OpenManifest(selection);
					using var modData = new ModData(
						manifest, mods, selection, RuntimeContractIdentity.Legacy,
						construction, RejectingGameLifecycleDispatchLane.Instance,
						useLoadScreen: false, ownsManifestPackage: true, ownsProvider: false);
					Assert.That(modData.TryGetService<IEpochService>(out var resolved), Is.True);
					Assert.That(resolved, Is.SameAs(service));
				}

				Assert.That(events, Is.EqualTo(new[]
				{
					"bind", "unbind", "bind", "unbind",
				}));
				Assert.That(service.DisposeCalls, Is.Zero);
				construction.Dispose();
				Assert.That(service.DisposeCalls, Is.EqualTo(1));
			}
			finally
			{
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		public void BorrowedConstructorFailureReleasesLeaseBeforeFinalOwnerDispose()
		{
			var fixture = CreateTestMod();
			var events = new List<string>();
			OwnedModDataConstructionState construction = null;
			try
			{
				var selection = new TrustedManifestSelection("test", "mod.ios-base.yaml");
				using var mods = new InstalledMods(
					new[] { fixture.ModsRoot }, Array.Empty<string>());
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<IThrowingActivationService>(
						new ThrowingActivationService(events)))), Is.True);
				using var options = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				construction = options.TakeOwnership();
				var manifest = mods.OpenManifest(selection);

				var exception = Assert.Throws<SentinelConstructionException>(() => new ModData(
					manifest, mods, selection, RuntimeContractIdentity.Legacy,
					construction, RejectingGameLifecycleDispatchLane.Instance,
					useLoadScreen: false, ownsManifestPackage: true, ownsProvider: false));
				construction.Dispose();

				Assert.Multiple(() =>
				{
					Assert.That(exception.Message, Is.EqualTo("activation failed"));
					Assert.That(events, Is.EqualTo(new[]
					{
						"throw-bind", "throw-activate", "throw-unbind", "throw-service-dispose",
					}));
				});
			}
			finally
			{
				construction?.Dispose();
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		[Timeout(10000)]
		public void ConstructorRollbackPumpsActivationFenceBeforeDisposeAndUnbind()
		{
			var fixture = CreateTestMod();
			var events = new List<string>();
			var lane = new GameLifecycleDispatchLane();
			try
			{
				var selection = new TrustedManifestSelection("test", "mod.ios-base.yaml");
				using var mods = new InstalledMods(
					new[] { fixture.ModsRoot }, Array.Empty<string>());
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<IRollbackFenceService>(
						new RollbackFenceService(events, lane)),
					ModDataServiceRegistration.Owned<IThrowingActivationService>(
						new ThrowingActivationService(events)))), Is.True);
				using var options = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				using var construction = options.TakeOwnership();
				var manifest = mods.OpenManifest(selection);

				var exception = Assert.Throws<SentinelConstructionException>(() => new ModData(
					manifest, mods, selection, RuntimeContractIdentity.Legacy,
					construction, lane, useLoadScreen: false,
					ownsManifestPackage: true, ownsProvider: true));

				Assert.Multiple(() =>
				{
					Assert.That(exception.Message, Is.EqualTo("activation failed"));
					Assert.That(events, Is.EqualTo(new[]
					{
						"fence-bind", "throw-bind", "fence-activate", "throw-activate",
						"fence-drain", "fence-ticket", "fence-dispose",
						"throw-unbind", "fence-unbind", "throw-service-dispose",
						"fence-service-dispose",
					}));
				});
			}
			finally
			{
				lane.Dispose();
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		[Timeout(10000)]
		public void ModDataDrainPumpsTrailingTicketAfterCompletedActivationFence()
		{
			var fixture = CreateTestMod();
			var events = new List<string>();
			var lane = new GameLifecycleDispatchLane();
			ModData modData = null;
			OwnedModDataConstructionState construction = null;
			try
			{
				var selection = new TrustedManifestSelection("test", "mod.ios-base.yaml");
				using var mods = new InstalledMods(
					new[] { fixture.ModsRoot }, Array.Empty<string>());
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<ICompletedFenceService>(
						new CompletedFenceService(events, lane)))), Is.True);
				using var options = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				construction = options.TakeOwnership();
				var manifest = mods.OpenManifest(selection);
				modData = new ModData(
					manifest, mods, selection, RuntimeContractIdentity.Legacy,
					construction, lane, useLoadScreen: false,
					ownsManifestPackage: true, ownsProvider: false);

				modData.Dispose();
				construction.Dispose();

				Assert.That(events, Is.EqualTo(new[]
				{
					"completed-bind", "completed-activate", "completed-drain",
					"completed-trailing", "completed-dispose-after-trailing",
					"completed-unbind", "completed-service-dispose",
				}));
			}
			finally
			{
				modData?.Dispose();
				construction?.Dispose();
				lane.PumpAvailable(int.MaxValue);
				lane.Dispose();
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		public void LegacyConstructorRollbackUnmountsAcquiredPackageExactlyOnce()
		{
			var fixture = CreateTestMod();
			var events = new List<string>();
			var package = new TrackingPackage("mounted", events, throwOnDispose: false);
			var failure = new SentinelConstructionException("mount failed");
			var originalLoaderFactory = ObjectCreator.LoaderFactoryOverride;
			try
			{
				using var mods = new InstalledMods(
					new[] { fixture.ModsRoot }, Array.Empty<string>());
				ObjectCreator.LoaderFactoryOverride = format => format == "DefaultFileSystem"
					? new MountThenThrowFileSystemLoader(package, failure)
					: null;

				var exception = Assert.Throws<SentinelConstructionException>(() =>
					new ModData(mods["test"], mods));

				Assert.Multiple(() =>
				{
					Assert.That(exception, Is.SameAs(failure));
					Assert.That(package.DisposeCalls, Is.EqualTo(1));
					Assert.That(events, Is.EqualTo(new[] { "mounted" }));
				});
			}
			finally
			{
				ObjectCreator.LoaderFactoryOverride = originalLoaderFactory;
				if (package.DisposeCalls == 0)
					package.Dispose();
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		[Timeout(10000)]
		public void LegacyConstructorRollbackPreservesPrimaryAndTotallyUnmountsHostilePackages()
		{
			var fixture = CreateTestMod();
			var events = new List<string>();
			var firstFailure = new InvalidOperationException("first package cleanup failed");
			var middleFailure = new InvalidOperationException("middle package cleanup failed");
			var first = new TrackingPackage("first", events, firstFailure);
			var middle = new TrackingPackage("middle", events, middleFailure);
			var last = new TrackingPackage("last", events, disposeFailure: null);
			var primary = new SentinelConstructionException("loader failed");
			var loader = new MountPackagesThenThrowFileSystemLoader(
				new IReadOnlyPackage[] { first, middle, last }, primary);
			var originalLoaderFactory = ObjectCreator.LoaderFactoryOverride;
			try
			{
				using var mods = new InstalledMods(
					new[] { fixture.ModsRoot }, Array.Empty<string>());
				ObjectCreator.LoaderFactoryOverride = format => format == "DefaultFileSystem"
					? loader
					: null;

				var observed = Assert.Throws<SentinelConstructionException>(() =>
					new ModData(mods["test"], mods));
				Exception repeatFailure = null;
				try { loader.MountedFileSystem.UnmountAll(); }
				catch (Exception exception) { repeatFailure = exception; }

				Assert.Multiple(() =>
				{
					Assert.That(observed, Is.SameAs(primary));
					Assert.That(events, Is.EqualTo(new[] { "first", "middle", "last" }));
					Assert.That(first.DisposeCalls, Is.EqualTo(1));
					Assert.That(middle.DisposeCalls, Is.EqualTo(1));
					Assert.That(last.DisposeCalls, Is.EqualTo(1));
					Assert.That(loader.MountedFileSystem.MountedPackages, Is.Empty);
					Assert.That(repeatFailure, Is.Null);
				});
			}
			finally
			{
				ObjectCreator.LoaderFactoryOverride = originalLoaderFactory;
				foreach (var package in new[] { first, middle, last })
					if (package.DisposeCalls == 0)
						try { package.Dispose(); }
						catch { }
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		[Timeout(10000)]
		public void ManifestDisposeContinuesAfterFirstAndMiddleFaultAndRepeatIsInert()
		{
			var events = new List<string>();
			var package = new ConcurrentTrackingPackage("manifest-root");
			var manifest = new Manifest("test", package);
			var firstFailure = new InvalidOperationException("first module cleanup failed");
			var first = new TrackingManifestModule("first", events, firstFailure);
			var middle = new TrackingManifestModule(
				"middle", events, new InvalidOperationException("middle module cleanup failed"));
			var last = new TrackingManifestModule("last", events, disposeFailure: null);
			var modules = (OpenRA.Primitives.TypeDictionary)ReadPrivateField(manifest, "modules");
			modules.Add(first);
			modules.Add(middle);
			modules.Add(last);
			try
			{
				Exception observed = null;
				try { manifest.Dispose(); }
				catch (Exception exception) { observed = exception; }
				Exception repeatFailure = null;
				try { manifest.Dispose(); }
				catch (Exception exception) { repeatFailure = exception; }

				Assert.Multiple(() =>
				{
					Assert.That(observed, Is.SameAs(firstFailure));
					Assert.That(events, Is.EqualTo(new[] { "first", "middle", "last" }));
					Assert.That(first.DisposeCalls, Is.EqualTo(1));
					Assert.That(middle.DisposeCalls, Is.EqualTo(1));
					Assert.That(last.DisposeCalls, Is.EqualTo(1));
					Assert.That(repeatFailure, Is.Null);
				});
			}
			finally
			{
				foreach (var module in new[] { first, middle, last })
					if (module.DisposeCalls == 0)
						try { module.Dispose(); }
						catch { }
				package.Dispose();
			}
		}

		[Test]
		[Timeout(10000)]
		public void NormalModDataDisposeReportsFirstCleanupFaultAfterTotalCleanupAndRepeatIsInert()
		{
			var fixture = CreateTestMod();
			var events = new List<string>();
			var firstPackageFailure = new InvalidOperationException("first package cleanup failed");
			var firstPackage = new TrackingPackage("package-first", events, firstPackageFailure);
			var middlePackage = new TrackingPackage("package-middle", events,
				new InvalidOperationException("middle package cleanup failed"));
			var lastPackage = new TrackingPackage("package-last", events, disposeFailure: null);
			var firstModule = new TrackingManifestModule("module-first", events,
				new InvalidOperationException("first module cleanup failed"));
			var middleModule = new TrackingManifestModule("module-middle", events,
				new InvalidOperationException("middle module cleanup failed"));
			var lastModule = new TrackingManifestModule("module-last", events, disposeFailure: null);
			var packageService = new PackageReplacingService();
			var ownedService = new CountingOwnedService();
			PreGameModDataScope scope = null;
			try
			{
				var configuration = new EngineBootstrapConfiguration(
					Platform.EngineDir,
					Platform.SupportDir,
					ImmutableArray.Create(fixture.ModsRoot),
					ImmutableArray<string>.Empty,
					new Arguments());
				using var context = PreGameEngineContext.Open(configuration);
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<IPackageReplacingService>(packageService),
					ModDataServiceRegistration.Owned<ICountingOwnedService>(ownedService))), Is.True);
				using var options = collection.Seal(
					loader => loader,
					context.Settings,
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				scope = context.OpenModData(
					new TrustedManifestSelection("test", "mod.ios-base.yaml"), options);
				var modData = scope.ModData;
				modData.ModFiles.Mount(firstPackage);
				modData.ModFiles.Mount(middlePackage);
				modData.ModFiles.Mount(lastPackage);
				var modules = (OpenRA.Primitives.TypeDictionary)ReadPrivateField(
					modData.Manifest, "modules");
				modules.Add(firstModule);
				modules.Add(middleModule);
				modules.Add(lastModule);

				Exception observed = null;
				try { modData.Dispose(); }
				catch (Exception exception) { observed = exception; }
				Exception repeatFailure = null;
				try { modData.Dispose(); }
				catch (Exception exception) { repeatFailure = exception; }
				Exception scopeFailure = null;
				try { scope.Dispose(); }
				catch (Exception exception) { scopeFailure = exception; }

				Assert.Multiple(() =>
				{
					Assert.That(observed, Is.SameAs(firstPackageFailure));
					Assert.That(events, Is.EqualTo(new[]
					{
						"package-first", "package-middle", "package-last",
						"module-first", "module-middle", "module-last",
					}));
					Assert.That(new[]
					{
						firstPackage.DisposeCalls, middlePackage.DisposeCalls, lastPackage.DisposeCalls,
					}, Is.All.EqualTo(1));
					Assert.That(new[]
					{
						firstModule.DisposeCalls, middleModule.DisposeCalls, lastModule.DisposeCalls,
					}, Is.All.EqualTo(1));
					Assert.That(modData.ModFiles.MountedPackages, Is.Empty);
					Assert.That(packageService.Package.DisposeCalls, Is.EqualTo(1));
					Assert.That(ownedService.DisposeCalls, Is.EqualTo(1));
					Assert.That(repeatFailure, Is.Null);
					Assert.That(scopeFailure, Is.Null);
					Assert.That(scope.ModData, Is.Null);
				});
			}
			finally
			{
				try { scope?.Dispose(); }
				catch { }
				foreach (var package in new[] { firstPackage, middlePackage, lastPackage })
					if (package.DisposeCalls == 0)
						try { package.Dispose(); }
						catch { }
				foreach (var module in new[] { firstModule, middleModule, lastModule })
					if (module.DisposeCalls == 0)
						try { module.Dispose(); }
						catch { }
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		public void LifecyclePumpProcessesOneSixtyFourAndSixtyFiveInExactFifoBatches()
		{
			var originalLegacyQueue = ReadStaticField(typeof(Game), "delayedActions");
			var isolatedLegacyQueue = new OpenRA.Primitives.ActionQueue();
			SetStaticField(typeof(Game), "delayedActions", isolatedLegacyQueue);
			try
			{
				var legacyExecuted = false;
				Game.RunAfterTick(() => legacyExecuted = true);
				foreach (var ticketCount in new[] { 1, 64, 65 })
				{
					var lane = CreateGameLifecycleLane();
					try
					{
						var events = new List<int>();
						for (var ticket = 0; ticket < ticketCount; ticket++)
						{
							var captured = ticket;
							lane.Post(() => events.Add(captured));
						}

						InvokeGameLifecycleMethod("PumpLifecycleDispatch", lane);
						Assert.That(events, Is.EqualTo(Enumerable.Range(
							0, Math.Min(ticketCount, 64))), $"first pump for {ticketCount}");
						Assert.That(legacyExecuted, Is.False);

						InvokeGameLifecycleMethod("PumpLifecycleDispatch", lane);
						Assert.That(events, Is.EqualTo(Enumerable.Range(0, ticketCount)),
							$"second pump for {ticketCount}");
						Assert.That(legacyExecuted, Is.False);
					}
					finally
					{
						(lane as IDisposable)?.Dispose();
					}
				}
			}
			finally
			{
				SetStaticField(typeof(Game), "delayedActions", originalLegacyQueue);
			}
		}

		[Test]
		[Timeout(10000)]
		public void DrainPumpWakesForLateTicketAndFaultsOnlyAfterQuiescence()
		{
			var drain = typeof(Game).GetMethod(
				"DrainLifecycleDispatchUntil", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.That(drain, Is.Not.Null, "Game.DrainLifecycleDispatchUntil is missing.");

			var lane = new ControlledLifecycleDispatchLane();
			var events = new List<string>();
			var failure = new SentinelConstructionException("fence failed");
			var fence = new System.Threading.Tasks.TaskCompletionSource<object>(
				System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
			System.Threading.Tasks.Task latePoster = null;
			try
			{
				latePoster = System.Threading.Tasks.Task.Factory.StartNew(() =>
				{
					lane.WaitForWaitRegistration();
					lane.Post(() =>
					{
						events.Add("late");
						lane.Post(() => events.Add("trailing"));
						fence.SetException(failure);
					});
				}, System.Threading.CancellationToken.None,
					System.Threading.Tasks.TaskCreationOptions.LongRunning,
					System.Threading.Tasks.TaskScheduler.Default);

				var exception = Assert.Throws<SentinelConstructionException>(() =>
					InvokeGameLifecycleMethod("DrainLifecycleDispatchUntil", lane, fence.Task));
				latePoster.Wait();
				Assert.Multiple(() =>
				{
					Assert.That(exception, Is.SameAs(failure));
					Assert.That(events, Is.EqualTo(new[] { "late", "trailing" }));
				});
			}
			finally
			{
				lane.ReleaseWaitRegistrationForCleanup();
				latePoster?.Wait();
				while (lane.PumpAvailable(Game.LifecycleDispatchBatch) != 0) { }
				lane.Dispose();
			}
		}

		[Test]
		[Timeout(10000)]
		public void DrainPumpRethrowsFirstInnerFenceFailureAfterQuiescence()
		{
			var lane = CreateGameLifecycleLane();
			var events = new List<string>();
			var firstFailure = new SentinelConstructionException("first fence failure");
			var secondFailure = new SentinelConstructionException("second fence failure");
			var fence = new System.Threading.Tasks.TaskCompletionSource<object>(
				System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
			try
			{
				lane.Post(() => events.Add("before"));
				lane.Post(() =>
				{
					events.Add("fence");
					lane.Post(() => events.Add("after"));
					fence.SetException(new Exception[] { firstFailure, secondFailure });
				});

				var exception = Assert.Throws<SentinelConstructionException>(() =>
					InvokeGameLifecycleMethod("DrainLifecycleDispatchUntil", lane, fence.Task));
				Assert.Multiple(() =>
				{
					Assert.That(exception, Is.SameAs(firstFailure));
					Assert.That(events, Is.EqualTo(new[] { "before", "fence", "after" }));
				});
			}
			finally
			{
				while (lane.PumpAvailable(Game.LifecycleDispatchBatch) != 0) { }
				(lane as IDisposable)?.Dispose();
			}
		}

		[Test]
		[Timeout(10000)]
		public void DrainPumpWakesForFenceOnlySuccessFaultAndCancellation()
		{
			foreach (var outcome in new[] { "success", "fault", "cancel" })
			{
				using var lane = new ControlledLifecycleDispatchLane();
				using var cancellation = new System.Threading.CancellationTokenSource();
				var failure = new SentinelConstructionException("fence-only failure");
				var fence = new System.Threading.Tasks.TaskCompletionSource<object>(
					System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
				var completer = System.Threading.Tasks.Task.Factory.StartNew(() =>
				{
					lane.WaitForWaitRegistration();
					switch (outcome)
					{
						case "success":
							fence.SetResult(null);
							break;
						case "fault":
							fence.SetException(failure);
							break;
						default:
							cancellation.Cancel();
							fence.SetCanceled(cancellation.Token);
							break;
					}
				}, System.Threading.CancellationToken.None,
					System.Threading.Tasks.TaskCreationOptions.LongRunning,
					System.Threading.Tasks.TaskScheduler.Default);

				try
				{
					switch (outcome)
					{
						case "success":
							Assert.DoesNotThrow(() => InvokeGameLifecycleMethod(
								"DrainLifecycleDispatchUntil", lane, fence.Task));
							break;
						case "fault":
							Assert.That(Assert.Throws<SentinelConstructionException>(() =>
								InvokeGameLifecycleMethod("DrainLifecycleDispatchUntil", lane, fence.Task)),
								Is.SameAs(failure));
							break;
						default:
							var exception = Assert.Throws<System.Threading.Tasks.TaskCanceledException>(() =>
								InvokeGameLifecycleMethod("DrainLifecycleDispatchUntil", lane, fence.Task));
							Assert.Multiple(() =>
							{
								Assert.That(exception?.Task, Is.SameAs(fence.Task));
								Assert.That(exception?.CancellationToken, Is.EqualTo(cancellation.Token));
							});
							break;
					}
				}
				finally
				{
					lane.ReleaseWaitRegistrationForCleanup();
					completer.Wait();
				}
			}
		}

		[Test]
		[Timeout(10000)]
		public void LifecycleWaitRegistrationKeepsPostBetweenEmptyCheckAndWait()
		{
			var lane = CreateGameLifecycleLane();
			var waiter = (IGameLifecycleDispatchWaiter)lane;
			var neverCompletes = new System.Threading.Tasks.TaskCompletionSource<object>(
				System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
			using var posted = new System.Threading.ManualResetEventSlim(false);
			IGameLifecycleDispatchWaitRegistration registration = null;
			try
			{
				Assert.That(waiter.IsQuiescent, Is.True);
				registration = waiter.RegisterWait(neverCompletes.Task);
				var worker = System.Threading.Tasks.Task.Factory.StartNew(() =>
				{
					lane.Post(() => { });
					posted.Set();
				}, System.Threading.CancellationToken.None,
					System.Threading.Tasks.TaskCreationOptions.LongRunning,
					System.Threading.Tasks.TaskScheduler.Default);

				posted.Wait();
				registration.Wait();
				worker.Wait();
				Assert.That(lane.PumpAvailable(1), Is.EqualTo(1));
			}
			finally
			{
				registration?.Dispose();
				while (lane.PumpAvailable(Game.LifecycleDispatchBatch) != 0) { }
				(lane as IDisposable)?.Dispose();
			}
		}

		[Test]
		[Timeout(10000)]
		public void ClosedLifecycleLaneRejectsPostPumpAndDrainAndRepeatDisposeIsInert()
		{
			var lane = CreateGameLifecycleLane();
			var disposable = (IDisposable)lane;
			disposable.Dispose();

			Assert.Multiple(() =>
			{
				Assert.Throws<ObjectDisposedException>(() => lane.Post(() => { }));
				Assert.Throws<ObjectDisposedException>(() => lane.PumpAvailable(1));
				Assert.Throws<ObjectDisposedException>(() =>
					((IGameLifecycleDispatchWaiter)lane).RegisterWait(
						System.Threading.Tasks.Task.CompletedTask));
				Assert.Throws<ObjectDisposedException>(() =>
					InvokeGameLifecycleMethod("PumpLifecycleDispatch", lane));
				Assert.Throws<ObjectDisposedException>(() => InvokeGameLifecycleMethod(
					"DrainLifecycleDispatchUntil", lane, System.Threading.Tasks.Task.CompletedTask));
				Assert.DoesNotThrow(disposable.Dispose);
			});
		}

		[Test]
		[Timeout(10000)]
		public void DisposeAfterWaitRegistrationWakesWithoutHandleRaceAndClosesLane()
		{
			var register = typeof(IGameLifecycleDispatchWaiter).GetMethod(
				"RegisterWait", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Assert.That(register, Is.Not.Null,
				"The lifecycle wait capability must expose an owned registration.");
			var wait = register.ReturnType.GetMethod(
				"Wait", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Assert.That(wait, Is.Not.Null,
				"The lifecycle wait registration must expose its untimed wait.");

			using var registered = new System.Threading.ManualResetEventSlim(false);
			var neverCompletes = new System.Threading.Tasks.TaskCompletionSource<object>(
				System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
			IGameLifecycleDispatchLane lane = null;
			object registration = null;
			Exception waitFailure = null;
			Exception pumpFailure = null;
			Exception drainFailure = null;
			Exception repeatDisposeFailure = null;
			var worker = System.Threading.Tasks.Task.Factory.StartNew(() =>
			{
				try
				{
					lane = CreateGameLifecycleLane();
					registration = InvokeInstanceMethod(
						register, (IGameLifecycleDispatchWaiter)lane, neverCompletes.Task);
					registered.Set();
					try { InvokeInstanceMethod(wait, registration); }
					catch (Exception exception) { waitFailure = exception; }
					((IDisposable)registration).Dispose();
					registration = null;

					try { lane.PumpAvailable(1); }
					catch (Exception exception) { pumpFailure = exception; }
					try
					{
						InvokeGameLifecycleMethod(
							"DrainLifecycleDispatchUntil", lane, neverCompletes.Task);
					}
					catch (Exception exception) { drainFailure = exception; }
					try { ((IDisposable)lane).Dispose(); }
					catch (Exception exception) { repeatDisposeFailure = exception; }
				}
				finally
				{
					registered.Set();
					(registration as IDisposable)?.Dispose();
				}
			}, System.Threading.CancellationToken.None,
				System.Threading.Tasks.TaskCreationOptions.LongRunning,
				System.Threading.Tasks.TaskScheduler.Default);

			registered.Wait();
			Assert.That(lane, Is.Not.Null);
			((IDisposable)lane).Dispose();
			worker.Wait();

			Assert.Multiple(() =>
			{
				Assert.That(waitFailure, Is.Null);
				Assert.That(pumpFailure, Is.TypeOf<ObjectDisposedException>());
				Assert.That(drainFailure, Is.TypeOf<ObjectDisposedException>());
				Assert.That(repeatDisposeFailure, Is.Null);
				Assert.Throws<ObjectDisposedException>(() => lane.Post(() => { }));
			});
		}

		[Test]
		[Timeout(10000)]
		public void DrainPumpPreservesActionFailureWhenWaiterCloses()
		{
			var failure = new SentinelConstructionException("action failure");
			var lane = new FaultThenClosedLifecycleLane(failure);
			var neverCompletes = new System.Threading.Tasks.TaskCompletionSource<object>(
				System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

			var exception = Assert.Throws<SentinelConstructionException>(() =>
				InvokeGameLifecycleMethod("DrainLifecycleDispatchUntil", lane, neverCompletes.Task));
			Assert.That(exception, Is.SameAs(failure));
		}

		[Test]
		[Timeout(10000)]
		public void DrainPumpEmptiesAcceptedActionsAndPreservesFirstActionFailure()
		{
			foreach (var outcome in new[] { "fault", "cancel" })
			{
				var lane = CreateGameLifecycleLane();
				var events = new List<string>();
				var firstFailure = new SentinelConstructionException("first action failure");
				var secondFailure = new SentinelConstructionException("second action failure");
				var fenceFailure = new SentinelConstructionException("fence failure");
				var fence = new System.Threading.Tasks.TaskCompletionSource<object>(
					System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
				try
				{
					lane.Post(() =>
					{
						events.Add("first");
						throw firstFailure;
					});
					lane.Post(() =>
					{
						events.Add("second");
						throw secondFailure;
					});
					lane.Post(() =>
					{
						events.Add("terminal");
						if (outcome == "fault")
							fence.SetException(new Exception[] { fenceFailure, secondFailure });
						else
							fence.SetCanceled();
					});
					lane.Post(() => events.Add("trailing"));

					var exception = Assert.Throws<SentinelConstructionException>(() =>
						InvokeGameLifecycleMethod("DrainLifecycleDispatchUntil", lane, fence.Task));
					Assert.Multiple(() =>
					{
						Assert.That(exception, Is.SameAs(firstFailure), outcome);
						Assert.That(events, Is.EqualTo(
							new[] { "first", "second", "terminal", "trailing" }), outcome);
					});
				}
				finally
				{
					while (lane.PumpAvailable(Game.LifecycleDispatchBatch) != 0) { }
					(lane as IDisposable)?.Dispose();
				}
			}
		}

		[Test]
		[Timeout(10000)]
		public void LifecyclePumpAndDrainRejectWrongThreadWithoutConsuming()
		{
			var lane = CreateGameLifecycleLane();
			var consumed = false;
			Exception pumpFailure = null;
			Exception drainFailure = null;
			try
			{
				lane.Post(() => consumed = true);
				var worker = System.Threading.Tasks.Task.Factory.StartNew(() =>
				{
					try { InvokeGameLifecycleMethod("PumpLifecycleDispatch", lane); }
					catch (Exception exception) { pumpFailure = exception; }
					try
					{
						InvokeGameLifecycleMethod("DrainLifecycleDispatchUntil", lane,
							System.Threading.Tasks.Task.CompletedTask);
					}
					catch (Exception exception) { drainFailure = exception; }
				}, System.Threading.CancellationToken.None,
					System.Threading.Tasks.TaskCreationOptions.LongRunning,
					System.Threading.Tasks.TaskScheduler.Default);

				worker.Wait();
				Assert.Multiple(() =>
				{
					Assert.That(pumpFailure, Is.TypeOf<InvalidOperationException>());
					Assert.That(drainFailure, Is.TypeOf<InvalidOperationException>());
					Assert.That(consumed, Is.False);
				});

				Assert.That(lane.PumpAvailable(1), Is.EqualTo(1));
				Assert.That(consumed, Is.True);
			}
			finally
			{
				while (lane.PumpAvailable(Game.LifecycleDispatchBatch) != 0) { }
				(lane as IDisposable)?.Dispose();
			}
		}

		[Test]
		[Timeout(10000)]
		public void DrainPumpEmptiesMoreThanTwoBatchesWithoutExecutingLegacyQueue()
		{
			var originalLegacyQueue = ReadStaticField(typeof(Game), "delayedActions");
			var isolatedLegacyQueue = new OpenRA.Primitives.ActionQueue();
			SetStaticField(typeof(Game), "delayedActions", isolatedLegacyQueue);
			var lane = CreateGameLifecycleLane();
			try
			{
				var legacyExecuted = false;
				Game.RunAfterTick(() => legacyExecuted = true);
				var events = new List<int>();
				for (var ticket = 0; ticket < 129; ticket++)
				{
					var captured = ticket;
					lane.Post(() => events.Add(captured));
				}

				InvokeGameLifecycleMethod("DrainLifecycleDispatchUntil", lane,
					System.Threading.Tasks.Task.CompletedTask);
				Assert.Throws<InvalidOperationException>(() => InvokeGameLifecycleMethod(
					"DrainLifecycleDispatchUntil", new LegacyGameLifecycleDispatchLane(),
					System.Threading.Tasks.Task.CompletedTask));
				Assert.Multiple(() =>
				{
					Assert.That(events, Is.EqualTo(Enumerable.Range(0, 129)));
					Assert.That(legacyExecuted, Is.False);
				});
			}
			finally
			{
				while (lane.PumpAvailable(Game.LifecycleDispatchBatch) != 0) { }
				(lane as IDisposable)?.Dispose();
				SetStaticField(typeof(Game), "delayedActions", originalLegacyQueue);
			}
		}

		[Test]
		[Timeout(10000)]
		public void LifecycleDrainSourceUsesUntimedSignalWithoutPollingOrLegacyTicks()
		{
			var gameSource = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Game.cs"));
			var laneSource = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "ModDataConstructionOptions.cs"));
			var drain = ExtractMethod(gameSource,
				"internal static void DrainLifecycleDispatchUntil(");
			var wait = ExtractMethod(laneSource,
				"void WaitForWorkOrCompletion(Task completion)");

			foreach (var token in new[]
			{
				"Thread.Sleep", "Task.Delay", "SpinWait", "Thread.Yield",
				"fence.Wait(", "Task.Wait(", ".Result", "GetAwaiter().GetResult()",
				"PerformDelayedActions", "LogicTick(", "InnerLogicTick(",
				"RunAfterTick", "OrderManager", "World",
			})
				StringAssert.DoesNotContain(token, drain, token);

			foreach (var token in new[]
			{
				"Thread.Sleep", "Task.Delay", "SpinWait", "Thread.Yield",
				"WaitOne(", ".Wait(", ".Result", "GetAwaiter().GetResult()",
				"Timeout", "TickCount", "Stopwatch",
			})
				StringAssert.DoesNotContain(token, wait, token);

			StringAssert.Contains("WaitHandle.WaitAny", wait);
			Assert.That(System.Text.RegularExpressions.Regex.IsMatch(wait,
				@"WaitAny\s*\([^;]*,\s*(?:-?\d+|TimeSpan)"), Is.False,
				"Lifecycle waits must not pass a fixed timeout to WaitAny.");
		}

		[Test]
		[Timeout(10000)]
		public void ModDataDrainOrdersStorePreviewDispatcherAndContinuesAfterFault()
		{
			var fixture = CreateTestMod();
			var events = new List<string>();
			var failure = new SentinelConstructionException("store dispose failed");
			var dispatcher = new OrderedActivationService(
				events, "dispatcher", faultOnDispose: false, failure);
			var store = new OrderedActivationService(
				events, "store", faultOnDispose: true, failure);
			var lifecycleLane = new QueuedLifecycleLane();
			ModData modData = null;
			OwnedModDataConstructionState construction = null;
			try
			{
				var selection = new TrustedManifestSelection("test", "mod.ios-base.yaml");
				using var mods = new InstalledMods(
					new[] { fixture.ModsRoot }, Array.Empty<string>());
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<IOrderedDispatcherService>(dispatcher),
					ModDataServiceRegistration.Owned<IOrderedStoreService>(store))), Is.True);
				using var options = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				construction = options.TakeOwnership();
				var manifest = mods.OpenManifest(selection);
				modData = new ModData(
					manifest, mods, selection, RuntimeContractIdentity.Legacy,
					construction, lifecycleLane, useLoadScreen: false,
					ownsManifestPackage: true, ownsProvider: false);

				var exception = Assert.Throws<SentinelConstructionException>(() =>
					modData.DrainActivationsAndUnbind(task =>
						Assert.That(task.IsCompleted, Is.True)));
				modData.Dispose();
				construction.Dispose();

				Assert.Multiple(() =>
				{
					Assert.That(exception, Is.SameAs(failure));
					Assert.That(events, Is.EqualTo(new[]
					{
						"dispatcher-bind", "store-bind",
						"dispatcher-activate", "store-activate",
						"store-drain-before-preview", "store-dispose",
						"dispatcher-drain-after-preview", "dispatcher-dispose",
						"store-unbind", "dispatcher-unbind",
						"store-service-dispose", "dispatcher-service-dispose",
					}));
				});
			}
			finally
			{
				modData?.Dispose();
				construction?.Dispose();
				lifecycleLane.Dispose();
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[TestCase("success")]
		[TestCase("fault")]
		[TestCase("cancel")]
		[TestCase("incomplete")]
		[Timeout(10000)]
		public void ModDataDisposeAppliesProductionNonWaiterFenceMatrixAndContinuesCleanup(
			string outcome)
		{
			var fixture = CreateTestMod();
			var events = new List<string>();
			var failure = new SentinelConstructionException("non-waiter fence failed");
			var service = new NonWaiterFenceService(events, outcome, failure);
			var lifecycleLane = new QueuedLifecycleLane();
			ModData modData = null;
			OwnedModDataConstructionState construction = null;
			try
			{
				var selection = new TrustedManifestSelection("test", "mod.ios-base.yaml");
				using var mods = new InstalledMods(
					new[] { fixture.ModsRoot }, Array.Empty<string>());
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<INonWaiterFenceService>(service))), Is.True);
				using var options = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				construction = options.TakeOwnership();
				modData = new ModData(
					mods.OpenManifest(selection), mods, selection, RuntimeContractIdentity.Legacy,
					construction, lifecycleLane, useLoadScreen: false,
					ownsManifestPackage: true, ownsProvider: false);

				Exception exception = null;
				try { modData.Dispose(); }
				catch (Exception caught) { exception = caught; }
				modData.Dispose();
				construction.Dispose();

				Assert.Multiple(() =>
				{
					if (outcome == "success")
						Assert.That(exception, Is.Null);
					else if (outcome == "fault")
						Assert.That(exception, Is.SameAs(failure));
					else if (outcome == "cancel")
						Assert.That(exception, Is.TypeOf<System.Threading.Tasks.TaskCanceledException>());
					else
					{
						Assert.That(exception, Is.TypeOf<InvalidOperationException>());
						Assert.That(exception?.Message, Is.EqualTo(
							"The lifecycle lane cannot drain an incomplete fence."));
					}

					Assert.That(events, Is.EqualTo(new[]
					{
						"bind", "activate", "drain", "activation-dispose",
						"unbind", "service-dispose",
					}), outcome);
				});
			}
			finally
			{
				modData?.Dispose();
				construction?.Dispose();
				lifecycleLane.Dispose();
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		[Timeout(10000)]
		public void ActivationFirstFaultSurvivesLaterDispatcherAndUnbindFaultsWithTotalCleanup()
		{
			var fixture = CreateTestMod();
			var events = new List<string>();
			var storeFailure = new SentinelConstructionException("store activation failed");
			var dispatcherFailure = new SentinelConstructionException("dispatcher activation failed");
			var unbindFailure = new SentinelConstructionException("store unbind failed");
			var dispatcher = new OrderedActivationService(
				events, "dispatcher", faultOnDispose: true, dispatcherFailure);
			var store = new OrderedActivationService(
				events, "store", faultOnDispose: true, storeFailure, unbindFailure);
			var lifecycleLane = new QueuedLifecycleLane();
			ModData modData = null;
			OwnedModDataConstructionState construction = null;
			try
			{
				var selection = new TrustedManifestSelection("test", "mod.ios-base.yaml");
				using var mods = new InstalledMods(
					new[] { fixture.ModsRoot }, Array.Empty<string>());
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<IOrderedDispatcherService>(dispatcher),
					ModDataServiceRegistration.Owned<IOrderedStoreService>(store))), Is.True);
				using var options = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				construction = options.TakeOwnership();
				modData = new ModData(
					mods.OpenManifest(selection), mods, selection, RuntimeContractIdentity.Legacy,
					construction, lifecycleLane, useLoadScreen: false,
					ownsManifestPackage: true, ownsProvider: false);

				var exception = Assert.Throws<SentinelConstructionException>(() =>
					modData.DrainActivationsAndUnbind(task =>
						Assert.That(task.IsCompleted, Is.True)));
				modData.Dispose();
				construction.Dispose();

				Assert.Multiple(() =>
				{
					Assert.That(exception, Is.SameAs(storeFailure));
					Assert.That(events, Is.EqualTo(new[]
					{
						"dispatcher-bind", "store-bind",
						"dispatcher-activate", "store-activate",
						"store-drain-before-preview", "store-dispose",
						"dispatcher-drain-after-preview", "dispatcher-dispose",
						"store-unbind", "dispatcher-unbind",
						"store-service-dispose", "dispatcher-service-dispose",
					}));
				});
			}
			finally
			{
				modData?.Dispose();
				construction?.Dispose();
				lifecycleLane.Dispose();
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		[Timeout(10000)]
		public void SameModInitializeInsideLegacyDelayedActionDoesNotReenterLegacyQueue()
		{
			var fixture = CreateTestMod();
			var originalQueue = ReadStaticField(typeof(Game), "delayedActions");
			var originalMods = typeof(Game).GetProperty(nameof(Game.Mods))?.GetValue(null);
			var originalModData = Game.ModData;
			var originalCursor = Game.Cursor;
			var originalLocalPlayerProfile = Game.LocalPlayerProfile;
			var originalLobbyInfoChanged = ReadStaticField(typeof(Game), nameof(Game.LobbyInfoChanged));
			var originalConnectionStateChanged = ReadStaticField(typeof(Game), nameof(Game.ConnectionStateChanged));
			var originalBeforeGameStart = ReadStaticField(typeof(Game), nameof(Game.BeforeGameStart));
			var originalRemoteDirectConnect = ReadStaticField(typeof(Game), nameof(Game.OnRemoteDirectConnect));
			var isolatedQueue = new OpenRA.Primitives.ActionQueue();
			var events = new List<string>();
			var lane = new GameLifecycleDispatchLane();
			OpenRA.Graphics.CursorManager retiredCursor = null;
			OpenRA.Graphics.SheetBuilder retiredCursorSheets = null;
			OwnedModDataConstructionState construction = null;
			InstalledMods mods = null;
			try
			{
				Assert.That(originalModData, Is.Null,
					"This behavior test requires an idle Game.ModData slot.");
				Assert.That(originalCursor, Is.Null,
					"This behavior test requires an idle Game.Cursor slot.");
				SetStaticField(typeof(Game), "delayedActions", isolatedQueue);
				retiredCursor = CreateDisposableCursor(out retiredCursorSheets);
				Game.Cursor = retiredCursor;
				Game.LocalPlayerProfile = new LocalPlayerProfile(
					Path.Combine(fixture.Root, "retired-profile"), null);
				mods = new InstalledMods(new[] { fixture.ModsRoot }, Array.Empty<string>());
				typeof(Game).GetProperty(nameof(Game.Mods))?.SetValue(null, mods);

				var service = new ReconstructionActivationService(
					events, lane, isolatedQueue);
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<IReconstructionActivationService>(service))),
					Is.True);
				using var options = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				construction = options.TakeOwnership();
				var selection = new TrustedManifestSelection("test", "mod.ios-base.yaml");
				Game.ModData = new ModData(
					mods.OpenManifest(selection), mods, selection, RuntimeContractIdentity.Legacy,
					construction, lane, useLoadScreen: false,
					ownsManifestPackage: true, ownsProvider: false);

				Exception initializeFailure = null;
				var insideInitialize = false;
				Game.RunAfterTick(() =>
				{
					insideInitialize = true;
					events.Add("initialize-enter");
					try { Game.InitializeMod("test", new Arguments()); }
					catch (Exception exception)
					{
						initializeFailure = exception;
						events.Add("initialize-failed-after-teardown");
					}
					finally
					{
						insideInitialize = false;
						events.Add("initialize-exit");
					}
				});

				Game.PerformDelayedActions();
				Assert.Multiple(() =>
				{
					Assert.That(initializeFailure, Is.Not.Null,
						"The lightweight fixture intentionally stops after the old epoch teardown.");
					Assert.That(insideInitialize, Is.False);
					Assert.That(events, Does.Not.Contain("legacy-reentered"));
					Assert.That(events, Does.Contain("drain-on-original-queue"));
					Assert.That(events, Does.Contain("lifecycle-ticket"));
					Assert.That(events, Does.Contain("activation-dispose-after-fence"));
					Assert.That(events, Does.Contain("unbind-on-original-queue"));
					Assert.That(lane.IsClosed, Is.True,
						"The retired epoch lane must close before the replacement epoch starts.");
					Assert.That(ReadStaticField(typeof(Game), "delayedActions"),
						Is.Not.SameAs(isolatedQueue),
						"A successful teardown must publish a fresh legacy queue before rebuilding.");
					Assert.That(Game.Cursor, Is.Null);
					Assert.That(Game.LocalPlayerProfile, Is.Null);
					Assert.That(retiredCursorSheets.AllSheets, Is.Empty,
						"The retired cursor sheets must be disposed before reconstruction.");
				});
			}
			finally
			{
				if (!lane.IsClosed)
					while (lane.PumpAvailable(Game.LifecycleDispatchBatch) != 0) { }
				if (Game.ModData != null && !ReferenceEquals(Game.ModData, originalModData))
					Game.ModData.Dispose();
				Game.ModData = originalModData;
				if (!ReferenceEquals(Game.Cursor, originalCursor))
					Game.Cursor?.Dispose();
				Game.Cursor = originalCursor;
				Game.LocalPlayerProfile = originalLocalPlayerProfile;
				retiredCursor?.Dispose();
				construction?.Dispose();
				lane.Dispose();
				typeof(Game).GetProperty(nameof(Game.Mods))?.SetValue(null, originalMods);
				mods?.Dispose();
				SetStaticField(typeof(Game), "delayedActions", originalQueue);
				SetStaticField(typeof(Game), nameof(Game.LobbyInfoChanged), originalLobbyInfoChanged);
				SetStaticField(typeof(Game), nameof(Game.ConnectionStateChanged), originalConnectionStateChanged);
				SetStaticField(typeof(Game), nameof(Game.BeforeGameStart), originalBeforeGameStart);
				SetStaticField(typeof(Game), nameof(Game.OnRemoteDirectConnect), originalRemoteDirectConnect);
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[TestCase("drain")]
		[TestCase("close")]
		[Timeout(10000)]
		public void FailedSameModTeardownRetainsLegacyQueueAfterClosingAndDetachingOldEpoch(
			string faultStage)
		{
			var fixture = CreateTestMod();
			var originalQueue = ReadStaticField(typeof(Game), "delayedActions");
			var originalMods = typeof(Game).GetProperty(nameof(Game.Mods))?.GetValue(null);
			var originalModData = Game.ModData;
			var originalLobbyInfoChanged = ReadStaticField(typeof(Game), nameof(Game.LobbyInfoChanged));
			var originalConnectionStateChanged = ReadStaticField(typeof(Game), nameof(Game.ConnectionStateChanged));
			var originalBeforeGameStart = ReadStaticField(typeof(Game), nameof(Game.BeforeGameStart));
			var originalRemoteDirectConnect = ReadStaticField(typeof(Game), nameof(Game.OnRemoteDirectConnect));
			var isolatedQueue = new OpenRA.Primitives.ActionQueue();
			var events = new List<string>();
			var failure = new SentinelConstructionException(faultStage + " failed");
			var lane = new ObservingLifecycleLane(
				events, faultStage == "close" ? failure : null);
			OwnedModDataConstructionState construction = null;
			InstalledMods mods = null;
			try
			{
				Assert.That(originalModData, Is.Null,
					"This behavior test requires an idle Game.ModData slot.");
				SetStaticField(typeof(Game), "delayedActions", isolatedQueue);
				mods = new InstalledMods(new[] { fixture.ModsRoot }, Array.Empty<string>());
				typeof(Game).GetProperty(nameof(Game.Mods))?.SetValue(null, mods);

				var service = new ReconstructionActivationService(
					events, lane, isolatedQueue, faultStage == "drain" ? failure : null);
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<IReconstructionActivationService>(service))),
					Is.True);
				using var options = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				construction = options.TakeOwnership();
				var selection = new TrustedManifestSelection("test", "mod.ios-base.yaml");
				Game.ModData = new ModData(
					mods.OpenManifest(selection), mods, selection, RuntimeContractIdentity.Legacy,
					construction, lane, useLoadScreen: false,
					ownsManifestPackage: true, ownsProvider: false);
				lane.ExpectedModData = Game.ModData;

				var exception = Assert.Throws<SentinelConstructionException>(() =>
					Game.InitializeMod("test", new Arguments()));

				Assert.Multiple(() =>
				{
					Assert.That(exception, Is.SameAs(failure));
					Assert.That(ReadStaticField(typeof(Game), "delayedActions"),
						Is.SameAs(isolatedQueue));
					Assert.That(events, Does.Contain("unbind-before-moddata-detach"));
					Assert.That(events, Does.Contain("lane-close-before-moddata-detach"));
					Assert.That(Game.ModData, Is.Null);
					Assert.That(lane.IsClosed, Is.True);
					Assert.That(lane.DisposeCalls, Is.EqualTo(1));
				});
			}
			finally
			{
				if (Game.ModData != null && !ReferenceEquals(Game.ModData, originalModData))
					Game.ModData.Dispose();
				Game.ModData = originalModData;
				construction?.Dispose();
				lane.Dispose();
				typeof(Game).GetProperty(nameof(Game.Mods))?.SetValue(null, originalMods);
				mods?.Dispose();
				SetStaticField(typeof(Game), "delayedActions", originalQueue);
				SetStaticField(typeof(Game), nameof(Game.LobbyInfoChanged), originalLobbyInfoChanged);
				SetStaticField(typeof(Game), nameof(Game.ConnectionStateChanged), originalConnectionStateChanged);
				SetStaticField(typeof(Game), nameof(Game.BeforeGameStart), originalBeforeGameStart);
				SetStaticField(typeof(Game), nameof(Game.OnRemoteDirectConnect), originalRemoteDirectConnect);
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		public void OuterStartupFailureReleasesEveryAcquiredStageExactlyOnce()
		{
			var events = new List<string>();
			var first = new StrictStartupStage(events, "first");
			var second = new StrictStartupStage(events, "second");
			var third = new StrictStartupStage(events, "third");
			var originalPlatformFactory = Game.PlatformFactoryOverride;
			var originalSettings = Game.Settings;
			EngineStartupState startup = null;
			try
			{
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<IStartupStageOne>(first),
					ModDataServiceRegistration.Owned<IStartupStageTwo>(second),
					ModDataServiceRegistration.Owned<IStartupStageThree>(third))), Is.True);
				using var options = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, new Arguments())),
					FluentInitializationMode.PublishGlobal,
					ModDataRenderingMode.Game);
				startup = new EngineStartupState(
					new EngineBootstrapConfiguration(
						Platform.EngineDir,
						Platform.SupportDir,
						ImmutableArray<string>.Empty,
						ImmutableArray<string>.Empty,
						new Arguments(new[] { "Game.Mod=missing" })),
					new TrustedManifestSelection("missing", "mod.ios-base.yaml"),
					new RuntimeContractIdentity("base", new string('e', 64)),
					options.TakeOwnership());

				var initialize = typeof(Game).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
					.SingleOrDefault(method => method.Name == nameof(Game.InitializeAndRun) &&
						method.GetParameters().Length == 1 &&
						method.GetParameters()[0].ParameterType == typeof(EngineStartupState));
				Assert.That(initialize, Is.Not.Null,
					"The owned startup entry point is missing.");

				Game.PlatformFactoryOverride = _ =>
					throw new SentinelConstructionException("platform failed");
				Exception startupFailure = null;
				try { initialize.Invoke(null, new object[] { startup }); }
				catch (TargetInvocationException exception)
				{
					startupFailure = exception.InnerException;
				}

				startup.Dispose();
				Assert.Multiple(() =>
				{
					Assert.That(startupFailure, Is.Not.Null);
					Assert.That(first.DisposeCalls, Is.EqualTo(1));
					Assert.That(second.DisposeCalls, Is.EqualTo(1));
					Assert.That(third.DisposeCalls, Is.EqualTo(1));
					Assert.That(events, Is.EqualTo(new[] { "third", "second", "first" }));
				});
			}
			finally
			{
				startup?.Dispose();
				Game.PlatformFactoryOverride = originalPlatformFactory;
				Game.Settings = originalSettings;
			}
		}

		[Test]
		[Timeout(10000)]
		public void ProfiledStartupPublishesExactSealedSettingsBeforePlatformConstruction()
		{
			var originalPlatformFactory = Game.PlatformFactoryOverride;
			var originalSettings = Game.Settings;
			var ownedService = new CountingOwnedService();
			var arguments = new Arguments(new[]
			{
				"Game.Mod=missing",
				"Game.Language=zh-CN",
				"Engine.SystemLanguage=zh-Hans-CN",
				"Graphics.SheetSize=1024",
			});
			var sealedSettings = new Settings(null, arguments);
			sealedSettings.Keys["sealed-only"] = new Hotkey(Keycode.A, Modifiers.Ctrl);
			var snapshot = ModDataSettingsSnapshot.Capture(sealedSettings);
			var expectedKeys = snapshot.TakePublishGlobalHotkeys();
			EngineStartupState startup = null;
			try
			{
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<ICountingOwnedService>(ownedService))),
					Is.True);
				using var options = collection.Seal(
					loader => loader, snapshot,
					FluentInitializationMode.PublishGlobal,
					ModDataRenderingMode.Game);
				startup = new EngineStartupState(
					new EngineBootstrapConfiguration(
						Platform.EngineDir,
						Platform.SupportDir,
						ImmutableArray<string>.Empty,
						ImmutableArray<string>.Empty,
						arguments),
					new TrustedManifestSelection("missing", "mod.ios-base.yaml"),
					new RuntimeContractIdentity("base", new string('d', 64)),
					options.TakeOwnership());

				var platformCalls = 0;
				var exactKeys = false;
				var exactLanguage = false;
				var exactSystemLanguage = false;
				var exactSheetSize = false;
				Game.PlatformFactoryOverride = _ =>
				{
					platformCalls++;
					exactKeys = ReferenceEquals(Game.Settings.Keys, expectedKeys);
					exactLanguage = Game.Settings.Game.Language == snapshot.Language;
					exactSystemLanguage = Game.Settings.SystemLanguageTag == snapshot.SystemLanguageTag;
					exactSheetSize = Game.Settings.Graphics.SheetSize == snapshot.GraphicsSheetSize;
					throw new SentinelConstructionException("platform failed");
				};

				Assert.Throws<InvalidOperationException>(() => Game.InitializeAndRun(startup));
				startup.Dispose();
				Assert.Multiple(() =>
				{
					Assert.That(platformCalls, Is.GreaterThan(0));
					Assert.That(exactKeys, Is.True,
						"Profiled startup must publish the sealed hotkey dictionary by identity.");
					Assert.That(exactLanguage, Is.True);
					Assert.That(exactSystemLanguage, Is.True);
					Assert.That(exactSheetSize, Is.True);
					Assert.That(ownedService.DisposeCalls, Is.EqualTo(1));
				});
			}
			finally
			{
				startup?.Dispose();
				Game.PlatformFactoryOverride = originalPlatformFactory;
				Game.Settings = originalSettings;
			}
		}

		[Test]
		[Timeout(10000)]
		public void PublicLegacyStartupOwnsEquivalentStateBeforePlatformAndCleansItOnFailure()
		{
			var originalPlatformFactory = Game.PlatformFactoryOverride;
			var originalSettings = Game.Settings;
			var originalStartup = ReadStaticField(typeof(Game), "activeStartup");
			OwnedEngineStartupState observedStartup = null;
			OwnedModDataConstructionState observedConstruction = null;
			var exactIdentity = false;
			var exactSettings = false;
			var platformCalls = 0;
			try
			{
				Assert.That(originalStartup, Is.Null,
					"This behavior test requires an idle startup owner slot.");
				Game.PlatformFactoryOverride = _ =>
				{
					platformCalls++;
					var current = ReadStaticField(typeof(Game), "activeStartup") as
						OwnedEngineStartupState;
					observedStartup ??= current;
					observedConstruction ??= current?.Construction;
					exactIdentity = current != null &&
						current.Selection == TrustedManifestSelection.Default("missing") &&
						current.RuntimeContract == RuntimeContractIdentity.Legacy &&
						current.Configuration.Arguments.GetValue("Game.Mod", null) == "missing";
					exactSettings = observedConstruction != null &&
						observedConstruction.FluentMode == FluentInitializationMode.PublishGlobal &&
						observedConstruction.RenderingMode == ModDataRenderingMode.Game &&
						ReferenceEquals(Game.Settings.Keys,
							observedConstruction.Settings.TakePublishGlobalHotkeys());
					throw new SentinelConstructionException("platform failed");
				};

				Assert.Throws<InvalidOperationException>(() => Game.InitializeAndRun(new[]
				{
					"Game.Mod=missing",
					"Engine.SystemLanguage=en",
				}));

				Assert.Multiple(() =>
				{
					Assert.That(platformCalls, Is.GreaterThan(0));
					Assert.That(observedStartup, Is.Not.Null);
					Assert.That(exactIdentity, Is.True);
					Assert.That(exactSettings, Is.True);
					Assert.That(ReadStaticField(typeof(Game), "activeStartup"), Is.Null);
					Assert.That(observedStartup?.Construction, Is.Null,
						"The retained owner alias must be inert after outer cleanup.");
				});
			}
			finally
			{
				SetStaticField(typeof(Game), "activeStartup", originalStartup);
				Game.PlatformFactoryOverride = originalPlatformFactory;
				Game.Settings = originalSettings;
			}
		}

		[Test]
		[Timeout(10000)]
		public void StartupOwnerCollisionPreservesWinnerAndDisposesRejectedOwner()
		{
			var originalPlatformFactory = Game.PlatformFactoryOverride;
			var originalSettings = Game.Settings;
			var originalStartup = ReadStaticField(typeof(Game), "activeStartup");
			var winnerService = new CountingOwnedService();
			var rejectedService = new CountingOwnedService();
			OwnedEngineStartupState winner = null;
			EngineStartupState candidate = null;
			var platformCalls = 0;
			try
			{
				Assert.That(originalStartup, Is.Null,
					"This behavior test requires an idle startup owner slot.");
				OwnedEngineStartupState CreateOwner(CountingOwnedService service, char digest)
				{
					var args = new Arguments(new[]
					{
						"Game.Mod=missing",
						"Engine.SystemLanguage=en",
					});
					using var collection = new ModDataServiceCollection();
					Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
						ModDataServiceRegistration.Owned<ICountingOwnedService>(service))),
						Is.True);
					using var options = collection.Seal(
						loader => loader,
						ModDataSettingsSnapshot.Capture(new Settings(null, args)),
						FluentInitializationMode.PublishGlobal,
						ModDataRenderingMode.Game);
					var state = new EngineStartupState(
						new EngineBootstrapConfiguration(
							Platform.EngineDir,
							Platform.SupportDir,
							ImmutableArray<string>.Empty,
							ImmutableArray<string>.Empty,
							args),
						new TrustedManifestSelection("missing", "mod.ios-base.yaml"),
						new RuntimeContractIdentity("base", new string(digest, 64)),
						options.TakeOwnership());
					return state.TakeOwnership();
				}

				winner = CreateOwner(winnerService, 'a');
				SetStaticField(typeof(Game), "activeStartup", winner);

				var candidateArgs = new Arguments(new[]
				{
					"Game.Mod=missing",
					"Engine.SystemLanguage=en",
				});
				using var candidateCollection = new ModDataServiceCollection();
				Assert.That(candidateCollection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<ICountingOwnedService>(rejectedService))),
					Is.True);
				using var candidateOptions = candidateCollection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, candidateArgs)),
					FluentInitializationMode.PublishGlobal,
					ModDataRenderingMode.Game);
				candidate = new EngineStartupState(
					new EngineBootstrapConfiguration(
						Platform.EngineDir,
						Platform.SupportDir,
						ImmutableArray<string>.Empty,
						ImmutableArray<string>.Empty,
						candidateArgs),
					new TrustedManifestSelection("missing", "mod.ios-base.yaml"),
					new RuntimeContractIdentity("base", new string('b', 64)),
					candidateOptions.TakeOwnership());
				Game.PlatformFactoryOverride = _ =>
				{
					platformCalls++;
					throw new SentinelConstructionException("platform must not run");
				};

				Assert.Throws<InvalidOperationException>(() => Game.InitializeAndRun(candidate));
				candidate.Dispose();
				Assert.Multiple(() =>
				{
					Assert.That(ReadStaticField(typeof(Game), "activeStartup"), Is.SameAs(winner));
					Assert.That(winnerService.DisposeCalls, Is.Zero);
					Assert.That(rejectedService.DisposeCalls, Is.EqualTo(1));
					Assert.That(platformCalls, Is.Zero);
				});
			}
			finally
			{
				SetStaticField(typeof(Game), "activeStartup", originalStartup);
				candidate?.Dispose();
				winner?.Dispose();
				Game.PlatformFactoryOverride = originalPlatformFactory;
				Game.Settings = originalSettings;
			}
		}

		[Test]
		[Timeout(10000)]
		public void PublicStartupCollisionDoesNotMutateWinnerSettingsOrEnterPlatform()
		{
			var originalPlatformFactory = Game.PlatformFactoryOverride;
			var originalSettings = Game.Settings;
			var originalStartup = ReadStaticField(typeof(Game), "activeStartup");
			var winnerService = new CountingOwnedService();
			OwnedEngineStartupState winner = null;
			var platformCalls = 0;
			try
			{
				Assert.That(originalStartup, Is.Null,
					"This behavior test requires an idle startup owner slot.");
				var winnerArguments = new Arguments(new[]
				{
					"Game.Mod=winner",
					"Engine.SystemLanguage=en",
				});
				var winnerSettings = new Settings(null, winnerArguments);
				Game.Settings = winnerSettings;
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<ICountingOwnedService>(winnerService))),
					Is.True);
				using var options = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(winnerSettings),
					FluentInitializationMode.PublishGlobal,
					ModDataRenderingMode.Game);
				var state = new EngineStartupState(
					new EngineBootstrapConfiguration(
						Platform.EngineDir,
						Platform.SupportDir,
						ImmutableArray<string>.Empty,
						ImmutableArray<string>.Empty,
						winnerArguments),
					TrustedManifestSelection.Default("winner"),
					RuntimeContractIdentity.Legacy,
					options.TakeOwnership());
				winner = state.TakeOwnership();
				SetStaticField(typeof(Game), "activeStartup", winner);
				Game.PlatformFactoryOverride = _ =>
				{
					platformCalls++;
					throw new SentinelConstructionException("platform must not run");
				};

				Assert.Throws<InvalidOperationException>(() => Game.InitializeAndRun(new[]
				{
					"Game.Mod=rejected",
					"Engine.SystemLanguage=fr",
				}));

				Assert.Multiple(() =>
				{
					Assert.That(Game.Settings, Is.SameAs(winnerSettings),
						"A rejected public startup must not replace the winner's settings.");
					Assert.That(platformCalls, Is.Zero);
					Assert.That(winnerService.DisposeCalls, Is.Zero);
					Assert.That(ReadStaticField(typeof(Game), "activeStartup"), Is.SameAs(winner));
				});
			}
			finally
			{
				SetStaticField(typeof(Game), "activeStartup", originalStartup);
				winner?.Dispose();
				Game.PlatformFactoryOverride = originalPlatformFactory;
				Game.Settings = originalSettings;
			}
		}

		[Test]
		[Timeout(10000)]
		public void StartupAdmissionRemainsClosedUntilWinnerProviderCleanupCompletes()
		{
			var originalPlatformFactory = Game.PlatformFactoryOverride;
			var originalSettings = Game.Settings;
			var originalStartup = ReadStaticField(typeof(Game), "activeStartup");
			var blockingProvider = new BlockingDisposeProvider();
			var rejectedService = new CountingOwnedService();
			EngineStartupState winner = null;
			EngineStartupState rejected = null;
			System.Threading.Tasks.Task winnerRun = null;
			Exception winnerFailure = null;
			var platformCalls = 0;
			try
			{
				Assert.That(originalStartup, Is.Null,
					"This behavior test requires an idle startup owner slot.");
				var winnerArguments = new Arguments(new[]
				{
					"Game.Mod=winner",
					"Engine.SystemLanguage=en",
				});
				using var winnerOptions = ModDataConstructionOptions.Create(
					blockingProvider,
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, winnerArguments)),
					FluentInitializationMode.PublishGlobal,
					ModDataRenderingMode.Game);
				winner = new EngineStartupState(
					new EngineBootstrapConfiguration(
						Platform.EngineDir,
						Platform.SupportDir,
						ImmutableArray<string>.Empty,
						ImmutableArray<string>.Empty,
						winnerArguments),
					TrustedManifestSelection.Default("winner"),
					RuntimeContractIdentity.Legacy,
					winnerOptions.TakeOwnership());

				var rejectedArguments = new Arguments(new[]
				{
					"Game.Mod=rejected",
					"Engine.SystemLanguage=en",
				});
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<ICountingOwnedService>(rejectedService))),
					Is.True);
				using var rejectedOptions = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, rejectedArguments)),
					FluentInitializationMode.PublishGlobal,
					ModDataRenderingMode.Game);
				rejected = new EngineStartupState(
					new EngineBootstrapConfiguration(
						Platform.EngineDir,
						Platform.SupportDir,
						ImmutableArray<string>.Empty,
						ImmutableArray<string>.Empty,
						rejectedArguments),
					TrustedManifestSelection.Default("rejected"),
					RuntimeContractIdentity.Legacy,
					rejectedOptions.TakeOwnership());
				Game.PlatformFactoryOverride = _ =>
				{
					System.Threading.Interlocked.Increment(ref platformCalls);
					throw new SentinelConstructionException("platform failed");
				};

				winnerRun = System.Threading.Tasks.Task.Factory.StartNew(() =>
				{
					try { Game.InitializeAndRun(winner); }
					catch (Exception exception) { winnerFailure = exception; }
				}, System.Threading.CancellationToken.None,
					System.Threading.Tasks.TaskCreationOptions.LongRunning,
					System.Threading.Tasks.TaskScheduler.Default);
				blockingProvider.FirstEntered.Wait();
				var winnerOwner = ReadStaticField(typeof(Game), "activeStartup");
				var callsBeforeCollision = System.Threading.Volatile.Read(ref platformCalls);

				Assert.Throws<InvalidOperationException>(() => Game.InitializeAndRun(rejected));
				rejected.Dispose();
				Assert.Multiple(() =>
				{
					Assert.That(winnerOwner, Is.Not.Null,
						"The winner must remain published until its provider cleanup finishes.");
					Assert.That(ReadStaticField(typeof(Game), "activeStartup"), Is.SameAs(winnerOwner));
					Assert.That(System.Threading.Volatile.Read(ref platformCalls),
						Is.EqualTo(callsBeforeCollision),
						"A cleanup collision must be rejected before entering platform initialization.");
					Assert.That(rejectedService.DisposeCalls, Is.EqualTo(1));
				});

				blockingProvider.Release.Set();
				winnerRun.GetAwaiter().GetResult();
				Assert.Multiple(() =>
				{
					Assert.That(winnerFailure, Is.TypeOf<InvalidOperationException>());
					Assert.That(ReadStaticField(typeof(Game), "activeStartup"), Is.Null);
					Assert.That(blockingProvider.DisposeCalls, Is.EqualTo(1));
				});
			}
			finally
			{
				blockingProvider.Release.Set();
				if (winnerRun != null)
					try { winnerRun.GetAwaiter().GetResult(); }
					catch { }
				SetStaticField(typeof(Game), "activeStartup", originalStartup);
				rejected?.Dispose();
				winner?.Dispose();
				blockingProvider.DisposeSignals();
				Game.PlatformFactoryOverride = originalPlatformFactory;
				Game.Settings = originalSettings;
			}
		}

		[Test]
		public void ProfiledEpochArgumentsIgnoreCallerAndReturnFreshSealedClone()
		{
			var originalStartup = ReadStaticField(typeof(Game), "activeStartup");
			OwnedEngineStartupState ownedStartup = null;
			try
			{
				var sealedArguments = new Arguments(new[]
				{
					"Game.Mod=test",
					"Launch.Map=sealed-map",
				});
				using var collection = new ModDataServiceCollection();
				using var options = collection.Seal(
					loader => loader,
					ModDataSettingsSnapshot.Capture(new Settings(null, sealedArguments)),
					FluentInitializationMode.PublishGlobal,
					ModDataRenderingMode.Game);
				var startup = new EngineStartupState(
					new EngineBootstrapConfiguration(
						Platform.EngineDir,
						Platform.SupportDir,
						ImmutableArray<string>.Empty,
						ImmutableArray<string>.Empty,
						sealedArguments),
					new TrustedManifestSelection("test", "mod.ios-base.yaml"),
					new RuntimeContractIdentity("base", new string('c', 64)),
					options.TakeOwnership());
				ownedStartup = startup.TakeOwnership();
				SetStaticField(typeof(Game), "activeStartup", ownedStartup);

				var selectArguments = typeof(Game).GetMethod(
					"ModEpochArguments", BindingFlags.Static | BindingFlags.NonPublic);
				Assert.That(selectArguments, Is.Not.Null,
					"The epoch argument authority must be shared by initial load and reconstruction.");
				var caller = new Arguments("Game.Mod=other", "Launch.Map=caller-map");
				var first = (Arguments)InvokeInstanceMethod(selectArguments, null, caller);
				caller.ReplaceValue("Launch.Map", "caller-mutated");
				first.ReplaceValue("Launch.Map", "returned-mutated");
				var second = (Arguments)InvokeInstanceMethod(selectArguments, null, caller);

				Assert.Multiple(() =>
				{
					Assert.That(first, Is.Not.SameAs(second));
					Assert.That(second.GetValue("Game.Mod", null), Is.EqualTo("test"));
					Assert.That(second.GetValue("Launch.Map", null), Is.EqualTo("sealed-map"));
					Assert.That(caller.GetValue("Launch.Map", null), Is.EqualTo("caller-mutated"));
				});
			}
			finally
			{
				SetStaticField(typeof(Game), "activeStartup", originalStartup);
				ownedStartup?.Dispose();
			}
		}

		[Test]
		public void FreshProcessNoRenderScopesDoNotReadGameSettingsOrAllocatePreviewResources()
		{
			var fixture = CreateTestMod();

			var originalSettings = Game.Settings;
			Game.Settings = null;
			try
			{
				var configuration = new EngineBootstrapConfiguration(
					Platform.EngineDir,
					Platform.SupportDir,
					ImmutableArray.Create(fixture.ModsRoot),
					ImmutableArray<string>.Empty,
					new Arguments());
				using var context = PreGameEngineContext.Open(configuration);
				Assert.That(context.Settings.GraphicsSheetSize, Is.GreaterThan(0));

				foreach (var entryName in new[] { "mod.ios-base.yaml", "mod.ios-yuri.yaml" })
				{
					using var collection = new ModDataServiceCollection();
					using var options = collection.Seal(
						loader => loader,
						context.Settings,
						FluentInitializationMode.Isolated,
						ModDataRenderingMode.NoRender);
					using var scope = context.OpenModData(
						new TrustedManifestSelection("test", entryName), options);

					Assert.Multiple(() =>
					{
						Assert.That(scope.ModData, Is.Not.Null);
						Assert.That(scope.ModData.MapCache, Is.Not.Null);
						Assert.That(scope.ModData.MapCache.LoadPreviewImages, Is.False);
						Assert.That(ReadPrivateField(scope.ModData.MapCache, "sheetBuilder"), Is.Null);
						Assert.That(ReadPrivateField(scope.ModData.MapCache, "previewLoaderThread"), Is.Null);
						Assert.That(Game.Settings, Is.Null);
					});
				}
			}
			finally
			{
				Game.Settings = originalSettings;
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		public void ServicesAreVisibleBeforeActivationAndReleaseInExactReverseOrder()
		{
			var fixture = CreateTestMod();
			var events = new List<string>();
			var service = new ActivatedService(events);
			try
			{
				var configuration = new EngineBootstrapConfiguration(
					Platform.EngineDir,
					Platform.SupportDir,
					ImmutableArray.Create(fixture.ModsRoot),
					ImmutableArray<string>.Empty,
					new Arguments());
				using var context = PreGameEngineContext.Open(configuration);
				using var collection = new ModDataServiceCollection();
				Assert.That(collection.TryAddOwnedBatch(ImmutableArray.Create(
					ModDataServiceRegistration.Owned<IActivatedService>(service))), Is.True);
				using var options = collection.Seal(
					loader => loader,
					context.Settings,
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);

				using (var scope = context.OpenModData(
					new TrustedManifestSelection("test", "mod.ios-base.yaml"), options))
				{
					Assert.That(scope.ModData.TryGetService<IActivatedService>(out var resolved), Is.True);
					Assert.That(resolved, Is.SameAs(service));
					Assert.That(events, Is.EqualTo(new[] { "bind", "activate" }));
				}

				Assert.That(events, Is.EqualTo(new[]
				{
					"bind", "activate", "drain", "activation-dispose", "unbind", "service-dispose",
				}));
			}
			finally
			{
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		public void InstalledModsDisposesEveryManifestPackageAfterAnEarlierFailure()
		{
			var events = new List<string>();
			var first = new TrackingPackage("first", events, throwOnDispose: true);
			var second = new TrackingPackage("second", events, throwOnDispose: false);
			var installed = new InstalledMods(Array.Empty<string>(), Array.Empty<string>());
			var mods = (Dictionary<string, Manifest>)typeof(InstalledMods)
				.GetField("mods", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(installed);
			mods.Add("first", new Manifest("first", first));
			mods.Add("second", new Manifest("second", second));

			Assert.Throws<InvalidOperationException>(() => installed.Dispose());
			Assert.That(events, Is.EqualTo(new[] { "first", "second" }));
			installed.Dispose();
			Assert.Multiple(() =>
			{
				Assert.That(events, Is.EqualTo(new[] { "first", "second" }));
				Assert.That(first.DisposeCalls, Is.EqualTo(1));
				Assert.That(second.DisposeCalls, Is.EqualTo(1));
			});
		}

		[Test]
		public void ConcurrentInstalledModsDisposalClosesEachManifestPackageExactlyOnce()
		{
			const int WorkerCount = 16;
			const int RoundCount = 256;
			var installed = new InstalledMods[RoundCount];
			var packages = new ConcurrentTrackingPackage[RoundCount];
			for (var round = 0; round < RoundCount; round++)
			{
				packages[round] = new ConcurrentTrackingPackage($"round-{round}");
				installed[round] = InstalledWithManifest($"mod-{round}", packages[round]);
			}

			using var start = new System.Threading.Barrier(WorkerCount + 1);
			using var finish = new System.Threading.Barrier(WorkerCount + 1);
			var workers = Enumerable.Range(0, WorkerCount).Select(_ =>
				System.Threading.Tasks.Task.Factory.StartNew(() =>
				{
					for (var round = 0; round < RoundCount; round++)
					{
						start.SignalAndWait();
						installed[round].Dispose();
						finish.SignalAndWait();
					}
				}, System.Threading.CancellationToken.None,
					System.Threading.Tasks.TaskCreationOptions.LongRunning,
					System.Threading.Tasks.TaskScheduler.Default)).ToArray();

			for (var round = 0; round < RoundCount; round++)
			{
				start.SignalAndWait();
				finish.SignalAndWait();
			}

			System.Threading.Tasks.Task.WaitAll(workers);
			Assert.That(packages.Select(package => package.DisposeCalls), Is.All.EqualTo(1));
		}

		[Test]
		public void MapCacheDrainFaultsOnlyAfterRejectedLifecyclePostReachesQuiescence()
		{
			Log.AddChannel("debug", null);
			var fixture = CreateTestMod();
			MapCache cache = null;
			try
			{
				var configuration = new EngineBootstrapConfiguration(
					Platform.EngineDir,
					Platform.SupportDir,
					ImmutableArray.Create(fixture.ModsRoot),
					ImmutableArray<string>.Empty,
					new Arguments());
				using var context = PreGameEngineContext.Open(configuration);
				using var collection = new ModDataServiceCollection();
				using var options = collection.Seal(
					loader => loader,
					context.Settings,
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				using var scope = context.OpenModData(
					new TrustedManifestSelection("test", "mod.ios-base.yaml"), options);
				var lane = new RejectingLifecycleLane();
				cache = new MapCache(scope.ModData, ModDataRenderingMode.Game, lane, 4);
				var drain = new System.Threading.Tasks.TaskCompletionSource<object>(
					System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
				SetPrivateField(cache, "previewDrain", drain);
				SetPrivateField(cache, "previewLoaderThreadShutDown", false);
				SetPrivateField(cache, "previewDrainRequested", true);

				var load = typeof(MapCache).GetMethod(
					"LoadAsyncInternal", BindingFlags.Instance | BindingFlags.NonPublic);
				Assert.DoesNotThrow(() => load.Invoke(cache, null));

				Assert.Multiple(() =>
				{
					Assert.That(lane.PostCalls, Is.EqualTo(1));
					Assert.That(cache.BeginPreviewDrainAsync().IsCompleted, Is.True);
					Assert.That(cache.BeginPreviewDrainAsync().IsFaulted, Is.True);
					Assert.That(ReadPrivateField(cache, "previewLoaderThreadShutDown"), Is.True);
				});
			}
			finally
			{
				if (cache != null)
				{
					SetPrivateField(cache, "previewLoaderThreadShutDown", true);
					var drain = (System.Threading.Tasks.TaskCompletionSource<object>)ReadPrivateField(
						cache, "previewDrain");
					drain.TrySetResult(null);
					cache.DisposePreviewResourcesOnGameThread();
				}

				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		public void AcceptedPreviewLifecycleCallbackFaultsFenceOnlyAfterTicketQuiesces()
		{
			var fixture = CreateTestMod();
			MapCache cache = null;
			var lane = new QueuedLifecycleLane();
			try
			{
				var configuration = new EngineBootstrapConfiguration(
					Platform.EngineDir,
					Platform.SupportDir,
					ImmutableArray.Create(fixture.ModsRoot),
					ImmutableArray<string>.Empty,
					new Arguments());
				using var context = PreGameEngineContext.Open(configuration);
				using var collection = new ModDataServiceCollection();
				using var options = collection.Seal(
					loader => loader,
					context.Settings,
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				using var scope = context.OpenModData(
					new TrustedManifestSelection("test", "mod.ios-base.yaml"), options);
				cache = new MapCache(scope.ModData, ModDataRenderingMode.Game, lane, 4);
				var drainSource = new System.Threading.Tasks.TaskCompletionSource<object>(
					System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
				SetPrivateField(cache, "previewDrain", drainSource);
				SetPrivateField(cache, "previewLoaderThreadShutDown", true);

				var failure = new SentinelConstructionException("preview callback failed");
				var post = typeof(MapCache).GetMethod(
					"PostLifecycleAction", BindingFlags.Instance | BindingFlags.NonPublic);
				Assert.That(post, Is.Not.Null);
				post.Invoke(cache, new object[] { new Action(() => throw failure) });

				var drain = cache.BeginPreviewDrainAsync();
				Assert.Multiple(() =>
				{
					Assert.That(drain.IsCompleted, Is.False,
						"An accepted lifecycle ticket must keep the preview fence open.");
					Assert.That(ReadPrivateField(cache, "pendingLifecycleActions"), Is.EqualTo(1));
				});

				var callbackFailure = Assert.Throws<SentinelConstructionException>(() =>
					lane.PumpAvailable(1));
				Assert.Multiple(() =>
				{
					Assert.That(callbackFailure, Is.SameAs(failure));
					Assert.That(ReadPrivateField(cache, "pendingLifecycleActions"), Is.Zero);
					Assert.That(lane.PumpAvailable(1), Is.Zero);
					Assert.That(drain.IsCompleted, Is.True);
					Assert.That(drain.IsFaulted, Is.True);
					Assert.That(drain.Exception?.InnerException, Is.SameAs(failure));
				});
			}
			finally
			{
				if (cache != null)
				{
					SetPrivateField(cache, "previewLoaderThreadShutDown", true);
					var drain = (System.Threading.Tasks.TaskCompletionSource<object>)ReadPrivateField(
						cache, "previewDrain");
					drain.TrySetResult(null);
					cache.DisposePreviewResourcesOnGameThread();
				}

				lane.Dispose();
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		public void PreviewResourcesDetachBeforeTotalCleanupAndPreserveTheFirstFailure()
		{
			var fixture = CreateTestMod();
			MapCache cache = null;
			TrackingPackage later = null;
			OpenRA.Graphics.SheetBuilder sheetBuilder = null;
			try
			{
				var configuration = new EngineBootstrapConfiguration(
					Platform.EngineDir,
					Platform.SupportDir,
					ImmutableArray.Create(fixture.ModsRoot),
					ImmutableArray<string>.Empty,
					new Arguments());
				using var context = PreGameEngineContext.Open(configuration);
				using var collection = new ModDataServiceCollection();
				using var options = collection.Seal(
					loader => loader,
					context.Settings,
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				using var scope = context.OpenModData(
					new TrustedManifestSelection("test", "mod.ios-base.yaml"), options);
				cache = new MapCache(
					scope.ModData, ModDataRenderingMode.Game, new QueuedLifecycleLane(), 4);

				var events = new List<string>();
				var hostile = new TrackingPackage("hostile-preview", events, throwOnDispose: true);
				later = new TrackingPackage("later-preview", events, throwOnDispose: false);
				var firstPreview = new MapPreview(
					scope.ModData, "first-preview", MapGridType.Rectangular, cache);
				var laterPreview = new MapPreview(
					scope.ModData, "later-preview", MapGridType.Rectangular, cache);
				SetPrivateField(firstPreview, "package", hostile);
				SetPrivateField(laterPreview, "package", later);

				var previewStore = ReadPrivateField(cache, "previews");
				var previewDictionary = (IDictionary<string, MapPreview>)previewStore.GetType()
					.GetField("cache", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(previewStore);
				previewDictionary.Add(firstPreview.Uid, firstPreview);
				previewDictionary.Add(laterPreview.Uid, laterPreview);
				sheetBuilder = (OpenRA.Graphics.SheetBuilder)ReadPrivateField(cache, "sheetBuilder");

				var failure = Assert.Throws<InvalidOperationException>(() =>
					cache.DisposePreviewResourcesOnGameThread());
				Assert.Multiple(() =>
				{
					Assert.That(failure.Message, Is.EqualTo("hostile-preview"));
					Assert.That(hostile.DisposeCalls, Is.EqualTo(1));
					Assert.That(later.DisposeCalls, Is.EqualTo(1));
					Assert.That(events, Is.EqualTo(new[] { "hostile-preview", "later-preview" }));
					Assert.That(previewDictionary, Is.Empty,
						"Preview ownership must be detached before any hostile Dispose call.");
					Assert.That(ReadPrivateField(cache, "sheetBuilder"), Is.Null,
						"Sheet ownership must be detached before cleanup begins.");
					Assert.That(((System.Collections.ICollection)ReadPrivateField(
						sheetBuilder, "sheets")).Count, Is.Zero,
						"The sheet must still be released after an earlier preview fault.");
				});

				Assert.DoesNotThrow(() => cache.DisposePreviewResourcesOnGameThread());
				Assert.That(later.DisposeCalls, Is.EqualTo(1));
			}
			finally
			{
				if (later != null && later.DisposeCalls == 0)
					later.Dispose();
				if (sheetBuilder != null &&
					((System.Collections.ICollection)ReadPrivateField(sheetBuilder, "sheets")).Count != 0)
					sheetBuilder.Dispose();

				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		[Timeout(10000)]
		public void BeginPreviewDrainWakesAnIdleWorkerAndCompletesItsFence()
		{
			Log.AddChannel("debug", null);
			var fixture = CreateTestMod();
			MapCache cache = null;
			MapPreview preview = null;
			var lane = new ObservableLifecycleLane();
			var synchronization = new ControlledPreviewWorkerSynchronization();
			var workerStarted = false;
			var releasePostObserved = false;
			try
			{
				var configuration = new EngineBootstrapConfiguration(
					Platform.EngineDir,
					Platform.SupportDir,
					ImmutableArray.Create(fixture.ModsRoot),
					ImmutableArray<string>.Empty,
					new Arguments());
				using var context = PreGameEngineContext.Open(configuration);
				using var collection = new ModDataServiceCollection();
				using var options = collection.Seal(
					loader => loader,
					context.Settings,
					FluentInitializationMode.Isolated,
					ModDataRenderingMode.NoRender);
				using var scope = context.OpenModData(
					new TrustedManifestSelection("test", "mod.ios-base.yaml"), options);

				cache = new MapCache(
					scope.ModData,
					ModDataRenderingMode.Game,
					lane,
					4,
					synchronization);
				preview = new MapPreview(
					scope.ModData, "idle-worker-preview", MapGridType.Rectangular, cache);
				cache.CacheMinimap(preview);
				workerStarted = true;

				// The admission wake is consumed by the first wait. The second entry proves that
				// the persistent worker has reached its untimed idle wait before drain begins.
				synchronization.WaitForIdleEntry();
				synchronization.WaitForIdleEntry();
				Assert.That(synchronization.WakeCalls, Is.EqualTo(1));

				var drain = cache.BeginPreviewDrainAsync();
				Assert.That(synchronization.WakeCalls, Is.EqualTo(2),
					"BeginPreviewDrainAsync must explicitly wake an already-idle worker.");

				lane.WaitForPost();
				releasePostObserved = true;
				Assert.Multiple(() =>
				{
					Assert.That(drain.IsCompleted, Is.False,
						"The release callback is an accepted lifecycle ticket in the preview fence.");
					Assert.That(ReadPrivateField(cache, "pendingLifecycleActions"), Is.EqualTo(1));
				});

				Assert.That(lane.PumpAvailable(1), Is.EqualTo(1));
				drain.GetAwaiter().GetResult();
				Assert.Multiple(() =>
				{
					Assert.That(drain.IsFaulted, Is.False);
					Assert.That(drain.IsCanceled, Is.False);
					Assert.That(ReadPrivateField(cache, "previewLoaderThreadShutDown"), Is.True);
					Assert.That(ReadPrivateField(cache, "previewLoaderThread"), Is.Null);
					Assert.That(ReadPrivateField(cache, "pendingLifecycleActions"), Is.Zero);
				});
			}
			finally
			{
				if (cache != null)
				{
					var cleanupDrain = cache.BeginPreviewDrainAsync();
					if (workerStarted && !releasePostObserved)
					{
						synchronization.ReleaseForCleanup();
						lane.WaitForPost();
					}

					lane.PumpAvailable(int.MaxValue);
					cleanupDrain.GetAwaiter().GetResult();
					cache.DisposePreviewResourcesOnGameThread();
				}

				preview?.Dispose();
				lane.Dispose();
				if (Directory.Exists(fixture.Root))
					Directory.Delete(fixture.Root, true);
			}
		}

		[Test]
		public void PreviewWorkerSourceUsesExplicitUntimedWakeAndHasNoIdleExitWindow()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Map", "MapCache.cs"));
			var worker = ExtractMethod(source, "void LoadAsyncInternal()");
			var admission = ExtractMethod(source, "public void CacheMinimap(MapPreview preview)");
			var drain = ExtractMethod(source, "internal Task BeginPreviewDrainAsync()");

			Assert.Multiple(() =>
			{
				StringAssert.DoesNotContain("Thread.Sleep", worker);
				StringAssert.DoesNotContain("MaxKeepAlive", worker);
				StringAssert.Contains("previewWorkerWake.WaitForWake()", worker);
				StringAssert.Contains("previewWorkerWake.Wake()", admission);
				StringAssert.Contains("previewWorkerWake?.Wake()", drain);
				StringAssert.DoesNotContain("keepAlive == 0", worker);
			});
		}

		[Test]
		public void PreviewLifecycleMethodsDoNotUseLegacyOrBlockingDispatch()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Map", "MapCache.cs"));
			var mapCacheSource = source[source.IndexOf(
				"public sealed class MapCache", StringComparison.Ordinal)..];
			var forbidden = new[]
			{
				"Game.RunAfterTick", "ThreadPool.QueueUserWorkItem", ".Join(",
				".Wait(", ".Result", "GetAwaiter().GetResult()",
			};

			foreach (var signature in new[]
			{
				"void LoadAsyncInternal()",
				"public void CacheMinimap(MapPreview preview)",
				"public void Dispose()",
			})
			{
				var method = ExtractMethod(mapCacheSource, signature);
				foreach (var token in forbidden)
					StringAssert.DoesNotContain(token, method, $"{signature} must not use {token}.");
			}
		}

		static object ReadPrivateField(object instance, string name) =>
			instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance);

		static object ReadStaticField(Type type, string name) =>
			type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);

		static void SetStaticField(Type type, string name, object value) =>
			type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, value);

		static OpenRA.Graphics.CursorManager CreateDisposableCursor(
			out OpenRA.Graphics.SheetBuilder sheets)
		{
#pragma warning disable SYSLIB0050
			var cursor = (OpenRA.Graphics.CursorManager)System.Runtime.Serialization.FormatterServices
				.GetUninitializedObject(typeof(OpenRA.Graphics.CursorManager));
#pragma warning restore SYSLIB0050
			var cursors = typeof(OpenRA.Graphics.CursorManager).GetField(
				"cursors", BindingFlags.Instance | BindingFlags.NonPublic);
			var sheetBuilder = typeof(OpenRA.Graphics.CursorManager).GetField(
				"sheetBuilder", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(cursors, Is.Not.Null);
			Assert.That(sheetBuilder, Is.Not.Null);
			cursors.SetValue(cursor, Activator.CreateInstance(cursors.FieldType));
			sheets = new OpenRA.Graphics.SheetBuilder(OpenRA.Graphics.SheetType.BGRA, 8);
			sheetBuilder.SetValue(cursor, sheets);
			return cursor;
		}

		static IGameLifecycleDispatchLane CreateGameLifecycleLane()
		{
			var type = typeof(Game).Assembly.GetType("OpenRA.GameLifecycleDispatchLane");
			Assert.That(type, Is.Not.Null, "The game-owned lifecycle lane is missing.");
			return (IGameLifecycleDispatchLane)Activator.CreateInstance(type, nonPublic: true);
		}

		static void InvokeGameLifecycleMethod(string name, params object[] arguments)
		{
			var method = typeof(Game).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null, $"Game.{name} is missing.");
			try { method.Invoke(null, arguments); }
			catch (TargetInvocationException exception) when (exception.InnerException != null)
			{
				System.Runtime.ExceptionServices.ExceptionDispatchInfo
					.Capture(exception.InnerException).Throw();
			}
		}

		static object InvokeInstanceMethod(MethodInfo method, object instance, params object[] arguments)
		{
			try { return method.Invoke(instance, arguments); }
			catch (TargetInvocationException exception) when (exception.InnerException != null)
			{
				System.Runtime.ExceptionServices.ExceptionDispatchInfo
					.Capture(exception.InnerException).Throw();
				return null;
			}
		}

		static T RaceSingleOwner<T>(Func<T> take) where T : IDisposable
		{
			const int WorkerCount = 16;
			using var start = new System.Threading.ManualResetEventSlim(false);
			using var ready = new System.Threading.CountdownEvent(WorkerCount);
			var owners = new List<T>();
			var rejected = 0;
			System.Threading.Tasks.Task Worker() => System.Threading.Tasks.Task.Factory.StartNew(() =>
			{
				ready.Signal();
				start.Wait();
				try
				{
					var owner = take();
					lock (owners)
						owners.Add(owner);
				}
				catch (InvalidOperationException)
				{
					System.Threading.Interlocked.Increment(ref rejected);
				}
			}, System.Threading.CancellationToken.None,
				System.Threading.Tasks.TaskCreationOptions.LongRunning,
				System.Threading.Tasks.TaskScheduler.Default);

			var workers = Enumerable.Range(0, WorkerCount).Select(_ => Worker()).ToArray();
			ready.Wait();
			start.Set();
			System.Threading.Tasks.Task.WaitAll(workers);
			Assert.Multiple(() =>
			{
				Assert.That(owners, Has.Count.EqualTo(1));
				Assert.That(rejected, Is.EqualTo(WorkerCount - 1));
			});
			return owners[0];
		}

		static T RaceOwnerAgainstDisposal<T>(Func<T> take, Action dispose) where T : class, IDisposable
		{
			const int TakeWorkerCount = 8;
			const int DisposeWorkerCount = 8;
			using var start = new System.Threading.ManualResetEventSlim(false);
			using var ready = new System.Threading.CountdownEvent(TakeWorkerCount + DisposeWorkerCount);
			var owners = new List<T>();
			var rejected = 0;
			var failures = new List<Exception>();

			System.Threading.Tasks.Task TakeWorker() => System.Threading.Tasks.Task.Factory.StartNew(() =>
			{
				ready.Signal();
				start.Wait();
				try
				{
					var owner = take();
					lock (owners)
						owners.Add(owner);
				}
				catch (InvalidOperationException)
				{
					System.Threading.Interlocked.Increment(ref rejected);
				}
				catch (Exception exception)
				{
					lock (failures)
						failures.Add(exception);
				}
			}, System.Threading.CancellationToken.None,
				System.Threading.Tasks.TaskCreationOptions.LongRunning,
				System.Threading.Tasks.TaskScheduler.Default);

			System.Threading.Tasks.Task DisposeWorker() => System.Threading.Tasks.Task.Factory.StartNew(() =>
			{
				ready.Signal();
				start.Wait();
				try { dispose(); }
				catch (Exception exception)
				{
					lock (failures)
						failures.Add(exception);
				}
			}, System.Threading.CancellationToken.None,
				System.Threading.Tasks.TaskCreationOptions.LongRunning,
				System.Threading.Tasks.TaskScheduler.Default);

			var workers = Enumerable.Range(0, TakeWorkerCount).Select(_ => TakeWorker())
				.Concat(Enumerable.Range(0, DisposeWorkerCount).Select(_ => DisposeWorker()))
				.ToArray();
			ready.Wait();
			start.Set();
			System.Threading.Tasks.Task.WaitAll(workers);
			Assert.Multiple(() =>
			{
				Assert.That(failures, Is.Empty);
				Assert.That(owners.Count, Is.LessThanOrEqualTo(1));
				Assert.That(rejected, Is.EqualTo(TakeWorkerCount - owners.Count));
			});
			return owners.SingleOrDefault();
		}

		static InstalledMods InstalledWithManifest(string id, IReadOnlyPackage package)
		{
			var installed = new InstalledMods(Array.Empty<string>(), Array.Empty<string>());
			var mods = (Dictionary<string, Manifest>)typeof(InstalledMods)
				.GetField("mods", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(installed);
			mods.Add(id, new Manifest(id, package));
			return installed;
		}

		static void SetPrivateField(object instance, string name, object value) =>
			instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(instance, value);

		static string ExtractMethod(string source, string signature)
		{
			var start = source.IndexOf(signature, StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
			var openBrace = source.IndexOf('{', start);
			Assert.That(openBrace, Is.GreaterThanOrEqualTo(0), signature);
			var depth = 0;
			for (var index = openBrace; index < source.Length; index++)
			{
				if (source[index] == '{')
					depth++;
				else if (source[index] == '}' && --depth == 0)
					return source.Substring(start, index - start + 1);
			}

			Assert.Fail($"Unable to find the end of {signature}.");
			return null;
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null &&
				!File.Exists(Path.Combine(directory.FullName, "engine", "OpenRA.Game", "ModDataConstructionOptions.cs")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory.FullName;
		}

		static (string Root, string ModsRoot) CreateTestMod()
		{
			var root = Path.Combine(Path.GetTempPath(), $"openra-pregame-{Guid.NewGuid():N}");
			var modsRoot = Path.Combine(root, "mods");
			var modRoot = Path.Combine(modsRoot, "test");
			Directory.CreateDirectory(modRoot);
			File.WriteAllText(Path.Combine(modRoot, "mod.yaml"), MinimalManifest);
			File.WriteAllText(Path.Combine(modRoot, "mod.ios-base.yaml"), MinimalManifest);
			File.WriteAllText(Path.Combine(modRoot, "mod.ios-yuri.yaml"), MinimalManifest);
			return (root, modsRoot);
		}

		interface IFirstService { }
		interface ISecondService { }
		interface IActivatedService { }
		interface IEpochService { }
		interface IRollbackFenceService { }
		interface ICompletedFenceService { }
		interface IThrowingActivationService { }
		interface IOrderedDispatcherService { }
		interface IOrderedStoreService { }
		interface INonWaiterFenceService { }
		interface IReconstructionActivationService { }
		interface IStartupStageOne { }
		interface IStartupStageTwo { }
		interface IStartupStageThree { }
		interface IPackageReplacingService { }
		interface ICountingOwnedService { }
		interface ILeaseLifetimeService { }

		sealed class ActivatedService : IActivatedService, IModDataActivatedService, IDisposable
		{
			readonly List<string> events;
			ModData modData;
			bool disposed;

			public ActivatedService(List<string> events) { this.events = events; }

			public IDisposable Bind(ModData value)
			{
				modData = value;
				events.Add("bind");
				return new CallbackToken(() => events.Add("unbind"));
			}

			public IModDataActivationToken Activate()
			{
				if (!modData.TryGetService<IActivatedService>(out var resolved) || !ReferenceEquals(resolved, this))
					throw new InvalidOperationException("The registered service was not visible before activation.");

				events.Add("activate");
				return new ActivationToken(events);
			}

			public void Dispose()
			{
				if (disposed)
					return;
				disposed = true;
				events.Add("service-dispose");
			}
		}

		sealed class EpochService : IEpochService, IModDataBoundService, IDisposable
		{
			readonly List<string> events;
			int disposeCalls;
			public int DisposeCalls => System.Threading.Volatile.Read(ref disposeCalls);

			public EpochService(List<string> events) { this.events = events; }

			public IDisposable Bind(ModData modData)
			{
				events.Add("bind");
				return new CallbackToken(() => events.Add("unbind"));
			}

			public void Dispose()
			{
				System.Threading.Interlocked.Increment(ref disposeCalls);
			}
		}

		sealed class OrderedActivationService :
			IOrderedDispatcherService, IOrderedStoreService, IModDataActivatedService, IDisposable
		{
			readonly List<string> events;
			readonly string role;
			readonly bool faultOnDispose;
			readonly Exception failure;
			readonly Exception unbindFailure;
			ModData modData;

			public OrderedActivationService(
				List<string> events, string role, bool faultOnDispose, Exception failure,
				Exception unbindFailure = null)
			{
				this.events = events;
				this.role = role;
				this.faultOnDispose = faultOnDispose;
				this.failure = failure;
				this.unbindFailure = unbindFailure;
			}

			public IDisposable Bind(ModData value)
			{
				modData = value;
				events.Add(role + "-bind");
				return new CallbackToken(() =>
				{
					events.Add(role + "-unbind");
					if (unbindFailure != null)
						throw unbindFailure;
				});
			}

			public IModDataActivationToken Activate()
			{
				events.Add(role + "-activate");
				return new OrderedActivationToken(
					events, role, modData, faultOnDispose, failure);
			}

			public void Dispose() { events.Add(role + "-service-dispose"); }
		}

		sealed class OrderedActivationToken : IModDataActivationToken
		{
			readonly List<string> events;
			readonly string role;
			readonly ModData modData;
			readonly bool faultOnDispose;
			readonly Exception failure;
			System.Threading.Tasks.Task drain;

			public OrderedActivationToken(
				List<string> events, string role, ModData modData,
				bool faultOnDispose, Exception failure)
			{
				this.events = events;
				this.role = role;
				this.modData = modData;
				this.faultOnDispose = faultOnDispose;
				this.failure = failure;
			}

			public System.Threading.Tasks.Task BeginDrainAsync()
			{
				if (drain != null)
					return drain;

				if (role == "dispatcher")
				{
					var previewDisposed = (bool)ReadPrivateField(
						modData.MapCache, "previewResourcesDisposed");
					events.Add(previewDisposed
						? "dispatcher-drain-after-preview"
						: "dispatcher-drain-before-preview");
				}
				else
				{
					var previewDisposed = (bool)ReadPrivateField(
						modData.MapCache, "previewResourcesDisposed");
					events.Add(previewDisposed
						? role + "-drain-after-preview"
						: role + "-drain-before-preview");
				}

				drain = System.Threading.Tasks.Task.CompletedTask;
				return drain;
			}

			public void Dispose()
			{
				events.Add(role + "-dispose");
				if (faultOnDispose)
					throw failure;
			}
		}

		sealed class NonWaiterFenceService :
			INonWaiterFenceService, IModDataActivatedService, IDisposable
		{
			readonly List<string> events;
			readonly string outcome;
			readonly Exception failure;

			public NonWaiterFenceService(
				List<string> events, string outcome, Exception failure)
			{
				this.events = events;
				this.outcome = outcome;
				this.failure = failure;
			}

			public IDisposable Bind(ModData modData)
			{
				events.Add("bind");
				return new CallbackToken(() => events.Add("unbind"));
			}

			public IModDataActivationToken Activate()
			{
				events.Add("activate");
				return new NonWaiterFenceToken(events, outcome, failure);
			}

			public void Dispose() { events.Add("service-dispose"); }
		}

		sealed class NonWaiterFenceToken : IModDataActivationToken
		{
			readonly List<string> events;
			readonly System.Threading.Tasks.Task fence;

			public NonWaiterFenceToken(
				List<string> events, string outcome, Exception failure)
			{
				this.events = events;
				fence = outcome switch
				{
					"success" => System.Threading.Tasks.Task.CompletedTask,
					"fault" => System.Threading.Tasks.Task.FromException(failure),
					"cancel" => System.Threading.Tasks.Task.FromCanceled(
						new System.Threading.CancellationToken(canceled: true)),
					"incomplete" => new System.Threading.Tasks.TaskCompletionSource<object>(
						System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously).Task,
					_ => throw new ArgumentOutOfRangeException(nameof(outcome)),
				};
			}

			public System.Threading.Tasks.Task BeginDrainAsync()
			{
				events.Add("drain");
				return fence;
			}

			public void Dispose() { events.Add("activation-dispose"); }
		}

		sealed class ReconstructionActivationService :
			IReconstructionActivationService, IModDataActivatedService, IDisposable
		{
			readonly List<string> events;
			readonly IGameLifecycleDispatchLane lane;
			readonly object expectedQueue;
			readonly Exception drainFailure;

			public ReconstructionActivationService(
				List<string> events, IGameLifecycleDispatchLane lane, object expectedQueue,
				Exception drainFailure = null)
			{
				this.events = events;
				this.lane = lane;
				this.expectedQueue = expectedQueue;
				this.drainFailure = drainFailure;
			}

			public IDisposable Bind(ModData modData) => new CallbackToken(() =>
			{
				events.Add(ReferenceEquals(Game.ModData, modData)
					? "unbind-before-moddata-detach"
					: "unbind-after-moddata-detach");
				events.Add(ReferenceEquals(
					ReadStaticField(typeof(Game), "delayedActions"), expectedQueue)
					? "unbind-on-original-queue"
					: "unbind-after-queue-replaced");
			});

			public IModDataActivationToken Activate() =>
				new ReconstructionActivationToken(events, lane, expectedQueue, drainFailure);

			public void Dispose() { }
		}

		sealed class ReconstructionActivationToken : IModDataActivationToken
		{
			readonly List<string> events;
			readonly IGameLifecycleDispatchLane lane;
			readonly object expectedQueue;
			readonly Exception drainFailure;
			readonly System.Threading.Tasks.TaskCompletionSource<object> fence = new(
				System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
			bool draining;

			public ReconstructionActivationToken(
				List<string> events, IGameLifecycleDispatchLane lane, object expectedQueue,
				Exception drainFailure)
			{
				this.events = events;
				this.lane = lane;
				this.expectedQueue = expectedQueue;
				this.drainFailure = drainFailure;
			}

			public System.Threading.Tasks.Task BeginDrainAsync()
			{
				if (draining)
					return fence.Task;

				draining = true;
				events.Add(ReferenceEquals(
					ReadStaticField(typeof(Game), "delayedActions"), expectedQueue)
					? "drain-on-original-queue"
					: "drain-after-queue-replaced");
				Game.RunAfterTick(() => events.Add("legacy-reentered"));
				lane.Post(() =>
				{
					events.Add("lifecycle-ticket");
					if (drainFailure != null)
						fence.TrySetException(drainFailure);
					else
						fence.TrySetResult(null);
				});
				return fence.Task;
			}

			public void Dispose()
			{
				events.Add(fence.Task.IsCompleted
					? "activation-dispose-after-fence"
					: "activation-dispose-before-fence");
			}
		}

		sealed class RollbackFenceService :
			IRollbackFenceService, IModDataActivatedService, IDisposable
		{
			readonly List<string> events;
			readonly IGameLifecycleDispatchLane lane;

			public RollbackFenceService(List<string> events, IGameLifecycleDispatchLane lane)
			{
				this.events = events;
				this.lane = lane;
			}

			public IDisposable Bind(ModData modData)
			{
				events.Add("fence-bind");
				return new CallbackToken(() => events.Add("fence-unbind"));
			}

			public IModDataActivationToken Activate()
			{
				events.Add("fence-activate");
				return new PumpedActivationToken(events, lane);
			}

			public void Dispose() { events.Add("fence-service-dispose"); }
		}

		sealed class ThrowingActivationService :
			IThrowingActivationService, IModDataActivatedService, IDisposable
		{
			readonly List<string> events;

			public ThrowingActivationService(List<string> events) { this.events = events; }

			public IDisposable Bind(ModData modData)
			{
				events.Add("throw-bind");
				return new CallbackToken(() => events.Add("throw-unbind"));
			}

			public IModDataActivationToken Activate()
			{
				events.Add("throw-activate");
				throw new SentinelConstructionException("activation failed");
			}

			public void Dispose() { events.Add("throw-service-dispose"); }
		}

		sealed class PumpedActivationToken : IModDataActivationToken
		{
			readonly List<string> events;
			readonly IGameLifecycleDispatchLane lane;
			readonly System.Threading.Tasks.TaskCompletionSource<object> fence = new(
				System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

			public PumpedActivationToken(List<string> events, IGameLifecycleDispatchLane lane)
			{
				this.events = events;
				this.lane = lane;
			}

			public System.Threading.Tasks.Task BeginDrainAsync()
			{
				events.Add("fence-drain");
				lane.Post(() =>
				{
					events.Add("fence-ticket");
					fence.TrySetResult(null);
				});
				return fence.Task;
			}

			public void Dispose()
			{
				events.Add(fence.Task.IsCompleted ? "fence-dispose" : "fence-dispose-before-ticket");
			}
		}

		sealed class CompletedFenceService :
			ICompletedFenceService, IModDataActivatedService, IDisposable
		{
			readonly List<string> events;
			readonly IGameLifecycleDispatchLane lane;

			public CompletedFenceService(List<string> events, IGameLifecycleDispatchLane lane)
			{
				this.events = events;
				this.lane = lane;
			}

			public IDisposable Bind(ModData modData)
			{
				events.Add("completed-bind");
				return new CallbackToken(() => events.Add("completed-unbind"));
			}

			public IModDataActivationToken Activate()
			{
				events.Add("completed-activate");
				return new CompletedFenceActivationToken(events, lane);
			}

			public void Dispose() { events.Add("completed-service-dispose"); }
		}

		sealed class CompletedFenceActivationToken : IModDataActivationToken
		{
			readonly List<string> events;
			readonly IGameLifecycleDispatchLane lane;

			public CompletedFenceActivationToken(
				List<string> events, IGameLifecycleDispatchLane lane)
			{
				this.events = events;
				this.lane = lane;
			}

			public System.Threading.Tasks.Task BeginDrainAsync()
			{
				events.Add("completed-drain");
				lane.Post(() => events.Add("completed-trailing"));
				return System.Threading.Tasks.Task.CompletedTask;
			}

			public void Dispose()
			{
				events.Add(events.Contains("completed-trailing")
					? "completed-dispose-after-trailing"
					: "completed-dispose-before-trailing");
			}
		}

		sealed class PackageReplacingThrowingService : IPackageReplacingService, IModDataBoundService, IDisposable
		{
			public StrictCountingPackage Package { get; private set; }

			public IDisposable Bind(ModData modData)
			{
				Package = new StrictCountingPackage(modData.Manifest.Package);
				typeof(Manifest).GetField(nameof(Manifest.Package)).SetValue(modData.Manifest, Package);
				throw new SentinelConstructionException("bind failed");
			}

			public void Dispose() { }
		}

		sealed class PackageReplacingService : IPackageReplacingService, IModDataBoundService, IDisposable
		{
			public StrictCountingPackage Package { get; private set; }

			public IDisposable Bind(ModData modData)
			{
				Package = new StrictCountingPackage(modData.Manifest.Package);
				typeof(Manifest).GetField(nameof(Manifest.Package)).SetValue(modData.Manifest, Package);
				return new CallbackToken(() => { });
			}

			public void Dispose() { }
		}

		sealed class CountingOwnedService : ICountingOwnedService, IDisposable
		{
			int disposeCalls;
			public int DisposeCalls => System.Threading.Volatile.Read(ref disposeCalls);
			public void Dispose() { System.Threading.Interlocked.Increment(ref disposeCalls); }
		}

		sealed class ControlledDirectoryConfigurationSynchronization :
			IPlatformDirectoryConfigurationSynchronization, IDisposable
		{
			readonly object directorySync;
			readonly System.Threading.CountdownEvent initialValidation = new(2);
			readonly System.Threading.ManualResetEventSlim laterPairPublished = new(false);
			int validationCalls;
			int blockedEnginePublisher;
			public bool ThrowOnNotification { get; set; }

			public ControlledDirectoryConfigurationSynchronization(object directorySync)
			{
				this.directorySync = directorySync ??
					throw new ArgumentNullException(nameof(directorySync));
			}

			public void AfterValidation(string engineDirectory, string supportDirectory)
			{
				ThrowIfRequested();
				if (System.Threading.Interlocked.Increment(ref validationCalls) > 2)
					return;

				initialValidation.Signal();
				initialValidation.Wait();
			}

			public void AfterEnginePublish(string engineDirectory, string supportDirectory)
			{
				ThrowIfRequested();
				if (!System.Threading.Monitor.IsEntered(directorySync) &&
					System.Threading.Interlocked.CompareExchange(
						ref blockedEnginePublisher, 1, 0) == 0)
					laterPairPublished.Wait();
			}

			public void AfterPairPublish(string engineDirectory, string supportDirectory)
			{
				ThrowIfRequested();
				laterPairPublished.Set();
			}

			void ThrowIfRequested()
			{
				if (ThrowOnNotification)
					throw new SentinelConstructionException("directory observer failed");
			}

			public void Release()
			{
				laterPairPublished.Set();
			}

			public void Dispose()
			{
				initialValidation.Dispose();
				laterPairPublished.Dispose();
			}
		}

		sealed class ThrowingDirectoryConfigurationSynchronization :
			IPlatformDirectoryConfigurationSynchronization
		{
			public void AfterValidation(string engineDirectory, string supportDirectory) { }
			public void AfterEnginePublish(string engineDirectory, string supportDirectory) =>
				throw new SentinelConstructionException("directory notification failed");
			public void AfterPairPublish(string engineDirectory, string supportDirectory) { }
		}

		sealed class LeaseLifetimeProvider : IModDataServiceProvider
		{
			int disposeCalls;
			int insideUse;
			public int DisposeCalls => System.Threading.Volatile.Read(ref disposeCalls);
			public bool DisposedDuringUse { get; private set; }
			public Action DuringTryGet { get; set; }
			public Action DuringRegistrations { get; set; }
			public ILeaseLifetimeService Service { get; } = new LeaseLifetimeService();
			public ImmutableArray<ModDataServiceRegistration> Registrations
			{
				get
				{
					System.Threading.Volatile.Write(ref insideUse, 1);
					try
					{
						DuringRegistrations?.Invoke();
						return ImmutableArray<ModDataServiceRegistration>.Empty;
					}
					finally
					{
						System.Threading.Volatile.Write(ref insideUse, 0);
					}
				}
			}

			public bool TryGet<T>(out T service) where T : class
			{
				if (System.Threading.Volatile.Read(ref disposeCalls) == 0 && Service is T resolved)
				{
					System.Threading.Volatile.Write(ref insideUse, 1);
					try
					{
						DuringTryGet?.Invoke();
						service = resolved;
						return true;
					}
					finally
					{
						System.Threading.Volatile.Write(ref insideUse, 0);
					}
				}

				service = null;
				return false;
			}

			public bool TryGet(Type serviceType, out object service)
			{
				if (System.Threading.Volatile.Read(ref disposeCalls) == 0 &&
					serviceType != null && serviceType.IsInstanceOfType(Service))
				{
					service = Service;
					return true;
				}

				service = null;
				return false;
			}

			public void Dispose()
			{
				if (System.Threading.Volatile.Read(ref insideUse) != 0)
					DisposedDuringUse = true;
				System.Threading.Interlocked.Increment(ref disposeCalls);
			}
		}

		sealed class LeaseLifetimeService : ILeaseLifetimeService { }

		sealed class StrictStartupStage :
			IStartupStageOne, IStartupStageTwo, IStartupStageThree, IDisposable
		{
			readonly List<string> events;
			readonly string name;
			int disposeCalls;
			public int DisposeCalls => System.Threading.Volatile.Read(ref disposeCalls);

			public StrictStartupStage(List<string> events, string name)
			{
				this.events = events;
				this.name = name;
			}

			public void Dispose()
			{
				if (System.Threading.Interlocked.Increment(ref disposeCalls) != 1)
					throw new InvalidOperationException(name + " disposed more than once.");

				events.Add(name);
			}
		}

		sealed class BlockingDisposeProvider : IModDataServiceProvider
		{
			int disposeCalls;
			public int DisposeCalls => System.Threading.Volatile.Read(ref disposeCalls);
			public System.Threading.ManualResetEventSlim FirstEntered { get; } = new(false);
			public System.Threading.ManualResetEventSlim SecondEntered { get; } = new(false);
			public System.Threading.ManualResetEventSlim Release { get; } = new(false);
			public ImmutableArray<ModDataServiceRegistration> Registrations =>
				ImmutableArray<ModDataServiceRegistration>.Empty;

			public bool TryGet<T>(out T service) where T : class
			{
				service = null;
				return false;
			}

			public bool TryGet(Type serviceType, out object service)
			{
				service = null;
				return false;
			}

			public void Dispose()
			{
				var call = System.Threading.Interlocked.Increment(ref disposeCalls);
				if (call == 1)
					FirstEntered.Set();
				else if (call == 2)
					SecondEntered.Set();

				Release.Wait();
			}

			public void DisposeSignals()
			{
				FirstEntered.Dispose();
				SecondEntered.Dispose();
				Release.Dispose();
			}
		}

		sealed class ActivationToken : IModDataActivationToken
		{
			readonly List<string> events;
			bool draining;
			bool disposed;

			public ActivationToken(List<string> events) { this.events = events; }
			public System.Threading.Tasks.Task BeginDrainAsync()
			{
				if (!draining)
				{
					draining = true;
					events.Add("drain");
				}

				return System.Threading.Tasks.Task.CompletedTask;
			}

			public void Dispose()
			{
				if (disposed)
					return;
				disposed = true;
				events.Add("activation-dispose");
			}
		}

		sealed class CallbackToken : IDisposable
		{
			Action callback;
			public CallbackToken(Action callback) { this.callback = callback; }
			public void Dispose()
			{
				var action = callback;
				callback = null;
				action?.Invoke();
			}
		}

		sealed class MountThenThrowFileSystemLoader : IFileSystemLoader
		{
			readonly IReadOnlyPackage package;
			readonly Exception failure;

			public MountThenThrowFileSystemLoader(IReadOnlyPackage package, Exception failure)
			{
				this.package = package;
				this.failure = failure;
			}

			public void Mount(OpenRA.FileSystem.FileSystem fileSystem, ObjectCreator objectCreator)
			{
				fileSystem.Mount(package);
				throw failure;
			}
		}

		sealed class MountPackagesThenThrowFileSystemLoader : IFileSystemLoader
		{
			readonly IReadOnlyPackage[] packages;
			readonly Exception failure;

			public OpenRA.FileSystem.FileSystem MountedFileSystem { get; private set; }

			public MountPackagesThenThrowFileSystemLoader(
				IReadOnlyPackage[] packages, Exception failure)
			{
				this.packages = packages;
				this.failure = failure;
			}

			public void Mount(OpenRA.FileSystem.FileSystem fileSystem, ObjectCreator objectCreator)
			{
				MountedFileSystem = fileSystem;
				foreach (var package in packages)
					fileSystem.Mount(package);

				throw failure;
			}
		}

		sealed class TrackingPackage : IReadOnlyPackage
		{
			readonly List<string> events;
			readonly Exception disposeFailure;
			bool disposed;
			public int DisposeCalls { get; private set; }

			public string Name { get; }
			public IEnumerable<string> Contents => new[] { "mod.yaml" };

			public TrackingPackage(string name, List<string> events, bool throwOnDispose)
				: this(name, events, throwOnDispose ? new InvalidOperationException(name) : null)
			{ }

			public TrackingPackage(string name, List<string> events, Exception disposeFailure)
			{
				Name = name;
				this.events = events;
				this.disposeFailure = disposeFailure;
			}

			public Stream GetStream(string filename) => filename == "mod.yaml"
				? new MemoryStream(System.Text.Encoding.UTF8.GetBytes(MinimalManifest), writable: false)
				: null;
			public bool Contains(string filename) => filename == "mod.yaml";
			public IReadOnlyPackage OpenPackage(string filename, OpenRA.FileSystem.FileSystem context) => null;

			public void Dispose()
			{
				DisposeCalls++;
				if (disposed)
					throw new InvalidOperationException($"{Name} was disposed more than once.");

				disposed = true;
				events.Add(Name);
				if (disposeFailure != null)
					throw disposeFailure;
			}
		}

		sealed class TrackingManifestModule : IGlobalModData, IDisposable
		{
			readonly string name;
			readonly List<string> events;
			readonly Exception disposeFailure;
			bool disposed;

			public int DisposeCalls { get; private set; }

			public TrackingManifestModule(
				string name, List<string> events, Exception disposeFailure)
			{
				this.name = name;
				this.events = events;
				this.disposeFailure = disposeFailure;
			}

			public void Dispose()
			{
				DisposeCalls++;
				if (disposed)
					throw new InvalidOperationException($"{name} was disposed more than once.");

				disposed = true;
				events.Add(name);
				if (disposeFailure != null)
					throw disposeFailure;
			}
		}

		sealed class ConcurrentTrackingPackage : IReadOnlyPackage
		{
			int disposeCalls;
			public int DisposeCalls => System.Threading.Volatile.Read(ref disposeCalls);
			public string Name { get; }
			public IEnumerable<string> Contents => new[] { "mod.yaml" };

			public ConcurrentTrackingPackage(string name)
			{
				Name = name;
			}

			public Stream GetStream(string filename) => filename == "mod.yaml"
				? new MemoryStream(System.Text.Encoding.UTF8.GetBytes(MinimalManifest), writable: false)
				: null;
			public bool Contains(string filename) => filename == "mod.yaml";
			public IReadOnlyPackage OpenPackage(string filename, OpenRA.FileSystem.FileSystem context) => null;
			public void Dispose() { System.Threading.Interlocked.Increment(ref disposeCalls); }
		}

		sealed class StrictCountingPackage : IReadOnlyPackage
		{
			readonly IReadOnlyPackage inner;
			int disposeCalls;
			public int DisposeCalls => System.Threading.Volatile.Read(ref disposeCalls);
			public string Name => inner.Name;
			public IEnumerable<string> Contents => inner.Contents;

			public StrictCountingPackage(IReadOnlyPackage inner)
			{
				this.inner = inner;
			}

			public Stream GetStream(string filename) => inner.GetStream(filename);
			public bool Contains(string filename) => inner.Contains(filename);
			public IReadOnlyPackage OpenPackage(string filename, OpenRA.FileSystem.FileSystem context) =>
				inner.OpenPackage(filename, context);

			public void Dispose()
			{
				if (System.Threading.Interlocked.Increment(ref disposeCalls) != 1)
					throw new InvalidOperationException("Manifest package was disposed more than once.");

				inner.Dispose();
			}
		}

		public sealed class SentinelConstructionException : Exception
		{
			public SentinelConstructionException(string message)
				: base(message)
			{ }
		}

		sealed class RejectingLifecycleLane : IGameLifecycleDispatchLane
		{
			public bool IsCurrentThread => true;
			public int PostCalls { get; private set; }

			public void Post(Action action)
			{
				PostCalls++;
				throw new InvalidOperationException("rejected");
			}

			public int PumpAvailable(int maxActions) => 0;
		}

		sealed class QueuedLifecycleLane : IGameLifecycleDispatchLane, IDisposable
		{
			readonly int ownerThread = Environment.CurrentManagedThreadId;
			readonly Queue<Action> actions = new();
			bool disposed;

			public bool IsCurrentThread => Environment.CurrentManagedThreadId == ownerThread;

			public void Post(Action action)
			{
				if (action == null)
					throw new ArgumentNullException(nameof(action));
				lock (actions)
				{
					if (disposed)
						throw new ObjectDisposedException(nameof(QueuedLifecycleLane));
					actions.Enqueue(action);
				}
			}

			public int PumpAvailable(int maxActions)
			{
				if (!IsCurrentThread)
					throw new InvalidOperationException("Wrong lifecycle thread.");

				var processed = 0;
				while (processed < maxActions)
				{
					Action action;
					lock (actions)
					{
						if (actions.Count == 0)
							break;
						action = actions.Dequeue();
					}

					action();
					processed++;
				}

				return processed;
			}

			public void Dispose()
			{
				lock (actions)
					disposed = true;
			}
		}

		sealed class ObservingLifecycleLane :
			IGameLifecycleDispatchLane, IGameLifecycleDispatchWaiter, IDisposable
		{
			readonly GameLifecycleDispatchLane inner = new();
			readonly List<string> events;
			readonly Exception disposeFailure;
			int disposeCalls;

			public ModData ExpectedModData { get; set; }
			public int DisposeCalls => System.Threading.Volatile.Read(ref disposeCalls);
			public bool IsCurrentThread => inner.IsCurrentThread;
			public bool IsQuiescent => inner.IsQuiescent;
			public bool IsClosed => inner.IsClosed;

			public ObservingLifecycleLane(List<string> events, Exception disposeFailure)
			{
				this.events = events;
				this.disposeFailure = disposeFailure;
			}

			public void Post(Action action) => inner.Post(action);
			public int PumpAvailable(int maxActions) => inner.PumpAvailable(maxActions);
			public IGameLifecycleDispatchWaitRegistration RegisterWait(
				System.Threading.Tasks.Task completion) =>
				inner.RegisterWait(completion);

			public void Dispose()
			{
				if (System.Threading.Interlocked.Increment(ref disposeCalls) != 1)
					return;

				events.Add(ReferenceEquals(Game.ModData, ExpectedModData)
					? "lane-close-before-moddata-detach"
					: "lane-close-after-moddata-detach");
				inner.Dispose();
				if (disposeFailure != null)
					throw disposeFailure;
			}
		}

		sealed class ControlledLifecycleDispatchLane :
			IGameLifecycleDispatchLane, IGameLifecycleDispatchWaiter, IDisposable
		{
			readonly int ownerThread = Environment.CurrentManagedThreadId;
			readonly object syncRoot = new();
			readonly Queue<Action> actions = new();
			readonly System.Threading.AutoResetEvent workAvailable = new(false);
			readonly System.Threading.SemaphoreSlim waitRegistrations = new(0);
			bool disposed;

			public bool IsCurrentThread => Environment.CurrentManagedThreadId == ownerThread;
			public bool IsQuiescent
			{
				get
				{
					lock (syncRoot)
						return actions.Count == 0;
				}
			}

			public bool IsClosed => disposed;

			public void Post(Action action)
			{
				if (action == null)
					throw new ArgumentNullException(nameof(action));

				lock (syncRoot)
				{
					if (disposed)
						throw new ObjectDisposedException(nameof(ControlledLifecycleDispatchLane));
					actions.Enqueue(action);
					workAvailable.Set();
				}
			}

			public int PumpAvailable(int maxActions)
			{
				if (!IsCurrentThread)
					throw new InvalidOperationException("Wrong lifecycle thread.");

				var processed = 0;
				while (processed < maxActions)
				{
					Action action;
					lock (syncRoot)
					{
						if (actions.Count == 0)
							break;
						action = actions.Dequeue();
					}

					action();
					processed++;
				}

				return processed;
			}

			public IGameLifecycleDispatchWaitRegistration RegisterWait(
				System.Threading.Tasks.Task completion)
			{
				waitRegistrations.Release();
				return new ControlledWaitRegistration(this, completion);
			}

			void WaitForWorkOrCompletion(System.Threading.Tasks.Task completion)
			{
				System.Threading.WaitHandle.WaitAny(new[]
				{
					workAvailable,
					((IAsyncResult)completion).AsyncWaitHandle,
				});
			}

			public void WaitForWaitRegistration() => waitRegistrations.Wait();
			public void ReleaseWaitRegistrationForCleanup() => waitRegistrations.Release();

			public void Dispose()
			{
				lock (syncRoot)
					disposed = true;
				workAvailable.Set();
				workAvailable.Dispose();
				waitRegistrations.Dispose();
			}

			sealed class ControlledWaitRegistration : IGameLifecycleDispatchWaitRegistration
			{
				readonly ControlledLifecycleDispatchLane owner;
				readonly System.Threading.Tasks.Task completion;

				public ControlledWaitRegistration(
					ControlledLifecycleDispatchLane owner,
					System.Threading.Tasks.Task completion)
				{
					this.owner = owner;
					this.completion = completion;
				}

				public void Wait() => owner.WaitForWorkOrCompletion(completion);
				public void Dispose() { }
			}
		}

		sealed class FaultThenClosedLifecycleLane :
			IGameLifecycleDispatchLane, IGameLifecycleDispatchWaiter
		{
			readonly Exception failure;
			bool faulted;
			bool closed;

			public FaultThenClosedLifecycleLane(Exception failure)
			{
				this.failure = failure;
			}

			public bool IsCurrentThread => true;
			public bool IsQuiescent => true;
			public bool IsClosed => closed;
			public void Post(Action action) => throw new InvalidOperationException();
			public int PumpAvailable(int maxActions)
			{
				if (!faulted)
				{
					faulted = true;
					throw failure;
				}

				return 0;
			}

			public IGameLifecycleDispatchWaitRegistration RegisterWait(
				System.Threading.Tasks.Task completion) => new FaultingWaitRegistration(this);

			sealed class FaultingWaitRegistration : IGameLifecycleDispatchWaitRegistration
			{
				readonly FaultThenClosedLifecycleLane owner;

				public FaultingWaitRegistration(FaultThenClosedLifecycleLane owner)
				{
					this.owner = owner;
				}

				public void Wait()
				{
					owner.closed = true;
					throw new ObjectDisposedException(nameof(FaultThenClosedLifecycleLane));
				}

				public void Dispose() { }
			}
		}

		sealed class ObservableLifecycleLane : IGameLifecycleDispatchLane, IDisposable
		{
			readonly int ownerThread = Environment.CurrentManagedThreadId;
			readonly Queue<Action> actions = new();
			readonly System.Threading.SemaphoreSlim posts = new(0);
			bool disposed;

			public bool IsCurrentThread => Environment.CurrentManagedThreadId == ownerThread;

			public void Post(Action action)
			{
				if (action == null)
					throw new ArgumentNullException(nameof(action));

				lock (actions)
				{
					if (disposed)
						throw new ObjectDisposedException(nameof(ObservableLifecycleLane));
					actions.Enqueue(action);
				}

				posts.Release();
			}

			public void WaitForPost() => posts.Wait();

			public int PumpAvailable(int maxActions)
			{
				if (!IsCurrentThread)
					throw new InvalidOperationException("Wrong lifecycle thread.");

				var processed = 0;
				while (processed < maxActions)
				{
					Action action;
					lock (actions)
					{
						if (actions.Count == 0)
							break;
						action = actions.Dequeue();
					}

					action();
					processed++;
				}

				return processed;
			}

			public void Dispose()
			{
				lock (actions)
					disposed = true;
				posts.Dispose();
			}
		}

		sealed class ControlledPreviewWorkerSynchronization : IMapPreviewWorkerSynchronization
		{
			readonly System.Threading.SemaphoreSlim wakes = new(0);
			readonly System.Threading.SemaphoreSlim waitEntries = new(0);
			int wakeCalls;

			public int WakeCalls => System.Threading.Volatile.Read(ref wakeCalls);

			public void WaitForWake()
			{
				waitEntries.Release();
				wakes.Wait();
			}

			public void Wake()
			{
				System.Threading.Interlocked.Increment(ref wakeCalls);
				wakes.Release();
			}

			public void WaitForIdleEntry() => waitEntries.Wait();

			public void ReleaseForCleanup() => wakes.Release();

			public void Dispose()
			{
				waitEntries.Dispose();
				wakes.Dispose();
			}
		}

		sealed class FirstService : IFirstService, IDisposable
		{
			readonly List<string> events;
			bool disposed;

			public FirstService(List<string> events) { this.events = events; }
			public void Dispose()
			{
				if (disposed)
					return;

				disposed = true;
				events.Add("first");
			}
		}

		sealed class SecondService : ISecondService, IDisposable
		{
			readonly List<string> events;
			bool disposed;

			public SecondService(List<string> events) { this.events = events; }
			public void Dispose()
			{
				if (disposed)
					return;

				disposed = true;
				events.Add("second");
			}
		}
	}
}
