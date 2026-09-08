#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

[assembly: InternalsVisibleTo("OpenRA.Test")]

namespace OpenRA
{
	public sealed class EngineBootstrapConfiguration
	{
		readonly Arguments arguments;

		public string EngineDir { get; }
		public string SupportDir { get; }
		public ImmutableArray<string> ModSearchPaths { get; }
		public ImmutableArray<string> ExplicitModPaths { get; }
		public Arguments Arguments => arguments.Clone();

		public EngineBootstrapConfiguration(
			string engineDir,
			string supportDir,
			ImmutableArray<string> modSearchPaths,
			ImmutableArray<string> explicitModPaths,
			Arguments arguments)
		{
			EngineDir = ValidateDirectoryValue(engineDir, nameof(engineDir));
			SupportDir = ValidateDirectoryValue(supportDir, nameof(supportDir));
			ModSearchPaths = CopyPaths(modSearchPaths, nameof(modSearchPaths));
			ExplicitModPaths = CopyPaths(explicitModPaths, nameof(explicitModPaths));
			this.arguments = (arguments ?? throw new ArgumentNullException(nameof(arguments))).Clone();
		}

		static string ValidateDirectoryValue(string value, string parameter)
		{
			if (value == null)
				throw new ArgumentNullException(parameter);
			if (string.IsNullOrWhiteSpace(value) || value.Contains('\0'))
				throw new ArgumentException("The directory is not valid.", parameter);

			return value;
		}

		static ImmutableArray<string> CopyPaths(ImmutableArray<string> paths, string parameter)
		{
			if (paths.IsDefault)
				throw new ArgumentException("The path list must be initialized.", parameter);
			if (paths.Any(path => string.IsNullOrWhiteSpace(path) || path.Contains('\0')))
				throw new ArgumentException("The path list contains an invalid path.", parameter);

			return ImmutableArray.CreateRange(paths);
		}
	}

	public sealed class PreGameModDataScope : IDisposable
	{
		ModData modData;
		public ModData ModData => Volatile.Read(ref modData);

		internal PreGameModDataScope(ModData modData)
		{
			this.modData = modData ?? throw new ArgumentNullException(nameof(modData));
		}

		PreGameModDataScope() { }

		public PreGameModDataScope TakeOwnership()
		{
			var result = new PreGameModDataScope();
			var ownedModData = Interlocked.Exchange(ref modData, null);
			if (ownedModData == null)
				throw new InvalidOperationException("The pre-game scope was already transferred or disposed.");

			result.modData = ownedModData;
			return result;
		}

		public void Dispose()
		{
			Interlocked.Exchange(ref modData, null)?.Dispose();
		}
	}

	internal sealed class EngineStartupState : IDisposable
	{
		readonly object ownershipLock = new();
		EngineBootstrapConfiguration configuration;
		TrustedManifestSelection selection;
		RuntimeContractIdentity runtimeContract;
		OwnedModDataConstructionState construction;
		bool consumed;

		public EngineBootstrapConfiguration Configuration => configuration;
		public TrustedManifestSelection Selection => selection;
		public RuntimeContractIdentity RuntimeContract => runtimeContract;

		internal EngineStartupState(
			EngineBootstrapConfiguration configuration,
			TrustedManifestSelection selection,
			RuntimeContractIdentity runtimeContract,
			OwnedModDataConstructionState construction)
		{
			this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
			if (!selection.IsValid)
				throw new ArgumentException("The manifest selection is not initialized.", nameof(selection));
			if (!runtimeContract.IsValid)
				throw new ArgumentException("The runtime contract is not initialized.", nameof(runtimeContract));

			this.selection = selection;
			this.runtimeContract = runtimeContract;
			this.construction = construction ?? throw new ArgumentNullException(nameof(construction));
		}

		internal OwnedEngineStartupState TakeOwnership()
		{
			lock (ownershipLock)
			{
				if (consumed || construction == null)
					throw new InvalidOperationException("The startup state was already consumed or disposed.");

				consumed = true;
				var result = new OwnedEngineStartupState(
					configuration, selection, runtimeContract, construction);
				configuration = null;
				selection = default;
				runtimeContract = default;
				construction = null;
				return result;
			}
		}

		public void Dispose()
		{
			OwnedModDataConstructionState constructionToDispose;
			lock (ownershipLock)
			{
				if (consumed)
					return;

				consumed = true;
				constructionToDispose = construction;
				configuration = null;
				selection = default;
				runtimeContract = default;
				construction = null;
			}

			constructionToDispose?.Dispose();
		}
	}

	internal sealed class OwnedEngineStartupState : IDisposable
	{
		OwnedModDataConstructionState construction;

		public EngineBootstrapConfiguration Configuration { get; }
		public TrustedManifestSelection Selection { get; }
		public RuntimeContractIdentity RuntimeContract { get; }
		public OwnedModDataConstructionState Construction => Volatile.Read(ref construction);

		internal OwnedEngineStartupState(
			EngineBootstrapConfiguration configuration,
			TrustedManifestSelection selection,
			RuntimeContractIdentity runtimeContract,
			OwnedModDataConstructionState construction)
		{
			Configuration = configuration;
			Selection = selection;
			RuntimeContract = runtimeContract;
			this.construction = construction;
		}

		public void Dispose()
		{
			Interlocked.Exchange(ref construction, null)?.Dispose();
		}
	}

	public sealed class PreGameEngineContext : IDisposable
	{
		readonly EngineBootstrapConfiguration configuration;
		readonly InstalledMods mods;
		bool disposed;

		public ModDataSettingsSnapshot Settings { get; }

		PreGameEngineContext(
			EngineBootstrapConfiguration configuration,
			InstalledMods mods,
			ModDataSettingsSnapshot settings)
		{
			this.configuration = configuration;
			this.mods = mods;
			Settings = settings;
		}

		public static PreGameEngineContext Open(EngineBootstrapConfiguration configuration)
		{
			if (configuration == null)
				throw new ArgumentNullException(nameof(configuration));

			Platform.ConfigureDirectories(configuration.EngineDir, configuration.SupportDir);
			var arguments = configuration.Arguments;
			var localSettings = new Settings(
				Path.Combine(Platform.SupportDir, "settings.yaml"), arguments);
			var settings = ModDataSettingsSnapshot.Capture(localSettings);
			var mods = new InstalledMods(configuration.ModSearchPaths, configuration.ExplicitModPaths);
			return new PreGameEngineContext(configuration, mods, settings);
		}

		public PreGameModDataScope OpenModData(
			TrustedManifestSelection selection,
			ModDataConstructionOptions options)
		{
			ThrowIfDisposed();
			if (!selection.IsValid)
				throw new ArgumentException("The manifest selection is not initialized.", nameof(selection));
			if (options == null)
				throw new ArgumentNullException(nameof(options));
			if (!options.MatchesModes(FluentInitializationMode.Isolated, ModDataRenderingMode.NoRender))
				throw new InvalidOperationException("Pre-game scopes require isolated, no-render options.");

			var construction = options.TakeOwnership();
			Manifest manifest = null;
			try
			{
				manifest = mods.OpenManifest(selection);
				var ownedManifest = manifest;
				manifest = null;
				var modData = new ModData(
					ownedManifest, mods, selection, RuntimeContractIdentity.Legacy,
					construction, RejectingGameLifecycleDispatchLane.Instance,
					useLoadScreen: false, ownsManifestPackage: true, ownsProvider: true);
				construction = null;
				return new PreGameModDataScope(modData);
			}
			finally
			{
				manifest?.Package.Dispose();
				construction?.Dispose();
			}
		}

		internal EngineStartupState SealForGame(
			TrustedManifestSelection selection,
			RuntimeContractIdentity runtimeContract,
			ModDataConstructionOptions options)
		{
			ThrowIfDisposed();
			if (!selection.IsValid)
				throw new ArgumentException("The manifest selection is not initialized.", nameof(selection));
			if (!runtimeContract.IsValid || runtimeContract.IsLegacy)
				throw new ArgumentException("A profiled runtime contract is required.", nameof(runtimeContract));
			if (options == null)
				throw new ArgumentNullException(nameof(options));
			if (!options.MatchesModes(FluentInitializationMode.PublishGlobal, ModDataRenderingMode.Game))
				throw new InvalidOperationException("Game startup requires publish-global, rendered options.");

			return new EngineStartupState(
				configuration, selection, runtimeContract, options.TakeOwnership());
		}

		void ThrowIfDisposed()
		{
			if (disposed)
				throw new ObjectDisposedException(nameof(PreGameEngineContext));
		}

		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;
			mods.Dispose();
		}
	}

	internal sealed class RejectingGameLifecycleDispatchLane : IGameLifecycleDispatchLane
	{
		public static RejectingGameLifecycleDispatchLane Instance { get; } = new();
		public bool IsCurrentThread => true;
		public void Post(Action action) =>
			throw new InvalidOperationException("The pre-game lifecycle lane does not accept work.");
		public int PumpAvailable(int maxActions) => 0;
	}
}
