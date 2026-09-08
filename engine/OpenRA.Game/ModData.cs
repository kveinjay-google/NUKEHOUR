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
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Video;
using OpenRA.Widgets;
using FS = OpenRA.FileSystem.FileSystem;

namespace OpenRA
{
	public sealed class ModData : IDisposable
	{
		public TrustedManifestSelection Selection { get; }
		public RuntimeContractIdentity RuntimeContract { get; }
		internal IGameLifecycleDispatchLane LifecycleDispatchLane { get; }

		public readonly Manifest Manifest;
		public readonly ObjectCreator ObjectCreator;
		public readonly WidgetLoader WidgetLoader;
		public readonly MapCache MapCache;
		public readonly IPackageLoader[] PackageLoaders;
		public readonly ISoundLoader[] SoundLoaders;
		public readonly ISpriteLoader[] SpriteLoaders;
		public readonly ITerrainLoader TerrainLoader;
		public readonly ISpriteSequenceLoader SpriteSequenceLoader;
		public readonly IVideoLoader[] VideoLoaders;
		public readonly HotkeyManager Hotkeys;
		public readonly IFileSystemLoader FileSystemLoader;

		public ILoadScreen LoadScreen { get; }
		public CursorProvider CursorProvider { get; private set; }
		public FS ModFiles;
		public IReadOnlyFileSystem DefaultFileSystem => ModFiles;

		readonly Lazy<Ruleset> defaultRules;
		public Ruleset DefaultRules => defaultRules.Value;

		readonly Lazy<IReadOnlyDictionary<string, ITerrainInfo>> defaultTerrainInfo;
		public IReadOnlyDictionary<string, ITerrainInfo> DefaultTerrainInfo => defaultTerrainInfo.Value;

		IModDataServiceProvider serviceProvider;
		readonly List<IDisposable> bindTokens = new();
		readonly List<IModDataActivationToken> activationTokens = new();
		readonly bool ownsManifestPackage;
		readonly bool ownsServiceProvider;
		bool bindingsLive;
		int disposeState;

		public ModData(Manifest mod, InstalledMods mods, bool useLoadScreen = false)
		{
			if (mod == null)
				throw new ArgumentNullException(nameof(mod));
			if (mods == null)
				throw new ArgumentNullException(nameof(mods));

			try
			{
				Selection = TrustedManifestSelection.Default(mod.Id);
				RuntimeContract = RuntimeContractIdentity.Legacy;
				LifecycleDispatchLane = new LegacyGameLifecycleDispatchLane();
				serviceProvider = new ModDataServiceProvider(ImmutableArray<ModDataServiceRegistration>.Empty);
				ownsServiceProvider = true;
				Languages = Array.Empty<string>();

				// Take a local copy of the manifest
				Manifest = new Manifest(mod.Id, mod.Package);
				ObjectCreator = new ObjectCreator(Manifest, mods);
				PackageLoaders = ObjectCreator.GetLoaders<IPackageLoader>(Manifest.PackageFormats, "package");
				ModFiles = new FS(mod.Id, mods, PackageLoaders);

				FileSystemLoader = ObjectCreator.GetLoader<IFileSystemLoader>(Manifest.FileSystem.Value, "filesystem");
				FieldLoader.Load(FileSystemLoader, Manifest.FileSystem);
				FileSystemLoader.Mount(ModFiles, ObjectCreator);
				ModFiles.TrimExcess();

				Manifest.LoadCustomData(ObjectCreator);

				FluentProvider.Initialize(this, DefaultFileSystem);

				if (useLoadScreen)
				{
					LoadScreen = ObjectCreator.CreateObject<ILoadScreen>(Manifest.LoadScreen.Value);
					LoadScreen.Init(this, Manifest.LoadScreen.ToDictionary(my => my.Value));
					LoadScreen.Display();
				}

				WidgetLoader = new WidgetLoader(this);
				MapCache = new MapCache(
					this, ModDataRenderingMode.Game, LifecycleDispatchLane,
					Game.Settings.Graphics.SheetSize);

				SoundLoaders = ObjectCreator.GetLoaders<ISoundLoader>(Manifest.SoundFormats, "sound");
				SpriteLoaders = ObjectCreator.GetLoaders<ISpriteLoader>(Manifest.SpriteFormats, "sprite");
				VideoLoaders = ObjectCreator.GetLoaders<IVideoLoader>(Manifest.VideoFormats, "video");

				var terrainFormat = Manifest.Get<TerrainFormat>();
				var terrainLoader = ObjectCreator.FindType(terrainFormat.Type + "Loader");
				var terrainCtor = terrainLoader?.GetConstructor(new[] { typeof(ModData) });
				if (terrainLoader == null || !terrainLoader.GetInterfaces().Contains(typeof(ITerrainLoader)) || terrainCtor == null)
					throw new InvalidOperationException($"Unable to find a terrain loader for type '{terrainFormat.Type}'.");

				TerrainLoader = (ITerrainLoader)terrainCtor.Invoke(new[] { this });

				var sequenceFormat = Manifest.Get<SpriteSequenceFormat>();
				var sequenceLoader = ObjectCreator.FindType(sequenceFormat.Type + "Loader");
				var sequenceCtor = sequenceLoader?.GetConstructor(new[] { typeof(ModData) });
				if (sequenceLoader == null || !sequenceLoader.GetInterfaces().Contains(typeof(ISpriteSequenceLoader)) || sequenceCtor == null)
					throw new InvalidOperationException($"Unable to find a sequence loader for type '{sequenceFormat.Type}'.");

				SpriteSequenceLoader = (ISpriteSequenceLoader)sequenceCtor.Invoke(new[] { this });

				Hotkeys = new HotkeyManager(ModFiles, Game.Settings.Keys, Manifest);

				defaultRules = Exts.Lazy(() => Ruleset.LoadDefaults(this));
				defaultTerrainInfo = Exts.Lazy(() =>
				{
					var items = new Dictionary<string, ITerrainInfo>();

					foreach (var file in Manifest.TileSets)
					{
						var t = TerrainLoader.ParseTerrain(DefaultFileSystem, file);
						items.Add(t.Id, t);
					}

					return (IReadOnlyDictionary<string, ITerrainInfo>)new ReadOnlyDictionary<string, ITerrainInfo>(items);
				});

				initialThreadId = Environment.CurrentManagedThreadId;
				bindingsLive = true;
			}
			catch
			{
				RollbackConstruction();
				throw;
			}
		}

		internal ModData(
			Manifest manifest,
			InstalledMods mods,
			TrustedManifestSelection selection,
			RuntimeContractIdentity runtimeContract,
			OwnedModDataConstructionState construction,
			IGameLifecycleDispatchLane lifecycleDispatchLane,
			bool useLoadScreen,
			bool ownsManifestPackage,
			bool ownsProvider)
		{
			if (manifest == null)
				throw new ArgumentNullException(nameof(manifest));
			if (mods == null)
				throw new ArgumentNullException(nameof(mods));
			if (!selection.IsValid || manifest.Id != selection.ModId || manifest.EntryName != selection.EntryName)
				throw new ArgumentException("The manifest does not match the trusted selection.", nameof(selection));
			if (!runtimeContract.IsValid)
				throw new ArgumentException("The runtime contract is not initialized.", nameof(runtimeContract));
			if (construction == null)
				throw new ArgumentNullException(nameof(construction));
			if (!Enum.IsDefined(typeof(FluentInitializationMode), construction.FluentMode))
				throw new ArgumentOutOfRangeException(nameof(construction));
			if (!Enum.IsDefined(typeof(ModDataRenderingMode), construction.RenderingMode))
				throw new ArgumentOutOfRangeException(nameof(construction));

			Selection = selection;
			RuntimeContract = runtimeContract;
			LifecycleDispatchLane = lifecycleDispatchLane ?? throw new ArgumentNullException(nameof(lifecycleDispatchLane));
			this.ownsManifestPackage = ownsManifestPackage;
			ownsServiceProvider = true;
			Languages = Array.Empty<string>();

			try
			{
				Manifest = manifest;
				ObjectCreator = new ObjectCreator(Manifest, mods);
				PackageLoaders = ObjectCreator.GetLoaders<IPackageLoader>(Manifest.PackageFormats, "package");
				ModFiles = new FS(manifest.Id, mods, PackageLoaders);

				var configuredLoader = ObjectCreator.GetLoader<IFileSystemLoader>(Manifest.FileSystem.Value, "filesystem");
				FieldLoader.Load(configuredLoader, Manifest.FileSystem);
				FileSystemLoader = construction.Decorator(configuredLoader) ??
					throw new InvalidOperationException("The file-system loader decorator returned null.");
				FileSystemLoader.Mount(ModFiles, ObjectCreator);
				ModFiles.TrimExcess();

				Manifest.LoadCustomData(ObjectCreator);
				if (construction.FluentMode == FluentInitializationMode.PublishGlobal)
					FluentProvider.Initialize(this, DefaultFileSystem);

				if (useLoadScreen)
				{
					LoadScreen = ObjectCreator.CreateObject<ILoadScreen>(Manifest.LoadScreen.Value);
					LoadScreen.Init(this, Manifest.LoadScreen.ToDictionary(my => my.Value));
					LoadScreen.Display();
				}

				WidgetLoader = new WidgetLoader(this);
				MapCache = new MapCache(
					this, construction.RenderingMode, LifecycleDispatchLane,
					construction.Settings.GraphicsSheetSize);

				SoundLoaders = ObjectCreator.GetLoaders<ISoundLoader>(Manifest.SoundFormats, "sound");
				SpriteLoaders = ObjectCreator.GetLoaders<ISpriteLoader>(Manifest.SpriteFormats, "sprite");
				VideoLoaders = ObjectCreator.GetLoaders<IVideoLoader>(Manifest.VideoFormats, "video");

				var terrainFormat = Manifest.Get<TerrainFormat>(ObjectCreator);
				var terrainLoader = ObjectCreator.FindType(terrainFormat.Type + "Loader");
				var terrainCtor = terrainLoader?.GetConstructor(new[] { typeof(ModData) });
				if (terrainLoader == null || !terrainLoader.GetInterfaces().Contains(typeof(ITerrainLoader)) || terrainCtor == null)
					throw new InvalidOperationException($"Unable to find a terrain loader for type '{terrainFormat.Type}'.");
				TerrainLoader = (ITerrainLoader)terrainCtor.Invoke(new[] { this });

				var sequenceFormat = Manifest.Get<SpriteSequenceFormat>(ObjectCreator);
				var sequenceLoader = ObjectCreator.FindType(sequenceFormat.Type + "Loader");
				var sequenceCtor = sequenceLoader?.GetConstructor(new[] { typeof(ModData) });
				if (sequenceLoader == null || !sequenceLoader.GetInterfaces().Contains(typeof(ISpriteSequenceLoader)) || sequenceCtor == null)
					throw new InvalidOperationException($"Unable to find a sequence loader for type '{sequenceFormat.Type}'.");
				SpriteSequenceLoader = (ISpriteSequenceLoader)sequenceCtor.Invoke(new[] { this });

				var hotkeys = construction.FluentMode == FluentInitializationMode.Isolated
					? construction.Settings.CreateIsolatedHotkeys()
					: construction.Settings.TakePublishGlobalHotkeys();
				Hotkeys = new HotkeyManager(ModFiles, hotkeys, Manifest);

				defaultRules = Exts.Lazy(() => Ruleset.LoadDefaults(this));
				defaultTerrainInfo = Exts.Lazy(() =>
				{
					var items = new Dictionary<string, ITerrainInfo>();
					foreach (var file in Manifest.TileSets)
					{
						var terrain = TerrainLoader.ParseTerrain(DefaultFileSystem, file);
						items.Add(terrain.Id, terrain);
					}

					return (IReadOnlyDictionary<string, ITerrainInfo>)new ReadOnlyDictionary<string, ITerrainInfo>(items);
				});

				initialThreadId = Environment.CurrentManagedThreadId;
				serviceProvider = ownsProvider
					? construction.TakeProvider()
					: construction.BorrowProvider();
				BindAndActivateServices();
				bindingsLive = true;
			}
			catch
			{
				RollbackConstruction();
				throw;
			}
		}

		void BindAndActivateServices()
		{
			foreach (var registration in serviceProvider.Registrations)
				if (registration.Service is IModDataBoundService bound)
				{
					var token = bound.Bind(this) ??
						throw new InvalidOperationException("A ModData service returned a null bind token.");
					bindTokens.Add(token);
				}

			bindingsLive = true;
			foreach (var registration in serviceProvider.Registrations)
			{
				if (!TryGetServiceByRegistration(registration))
					throw new InvalidOperationException("A registered ModData service changed identity.");
			}

			foreach (var registration in serviceProvider.Registrations)
				if (registration.Service is IModDataActivatedService activated)
				{
					var token = activated.Activate() ??
						throw new InvalidOperationException("A ModData service returned a null activation token.");
					activationTokens.Add(token);
				}
		}

		bool TryGetServiceByRegistration(ModDataServiceRegistration registration)
		{
			return serviceProvider.TryGet(registration.ServiceType, out var service) &&
				ReferenceEquals(service, registration.Service);
		}

		public bool TryGetService<T>(out T service) where T : class
		{
			var provider = serviceProvider;
			if (bindingsLive && Volatile.Read(ref disposeState) == 0 && provider != null)
				return provider.TryGet(out service);

			service = null;
			return false;
		}

		// HACK: Only update the loading screen if we're in the main thread.
		readonly int initialThreadId;
		internal void HandleLoadingProgress()
		{
#if IOS
			// UIKit owns the main thread during startup. Rendering an intermediate
			// load screen synchronously here can deadlock it against the SDL render
			// thread; the completed shell will render after initialization instead.
			return;
#endif
			if (LoadScreen != null && IsOnMainThread)
				LoadScreen.Display();
		}

		internal bool IsOnMainThread => Environment.CurrentManagedThreadId == initialThreadId;

		public void InitializeLoaders(IReadOnlyFileSystem fileSystem)
		{
			// all this manipulation of static crap here is nasty and breaks
			// horribly when you use ModData in unexpected ways.
			ChromeMetrics.Initialize(this);
			ChromeProvider.Initialize(this);
			FluentProvider.Initialize(this, fileSystem);

			Game.Sound.Initialize(SoundLoaders, fileSystem);

			CursorProvider = new CursorProvider(this);
		}

		public IEnumerable<string> Languages { get; }

		public Map PrepareMap(string uid)
		{
			LoadScreen?.Display();

			if (MapCache[uid].Status != MapStatus.Available)
				throw new InvalidDataException($"Invalid map uid: {uid}");

			Map map;
			using (new Support.PerfTimer("Map"))
				map = new Map(this, MapCache[uid].Package);

			// Reinitialize all our assets
			InitializeLoaders(map);
			map.Sequences.LoadSprites();

			// Load music with map assets mounted
			using (new Support.PerfTimer("Map.Music"))
				foreach (var entry in map.Rules.Music)
					entry.Value.Load(map);

			return map;
		}

		public List<MiniYamlNode>[] GetRulesYaml()
		{
			var stringPool = new HashSet<string>(); // Reuse common strings in YAML
			return Manifest.Rules.Select(s => MiniYaml.FromStream(DefaultFileSystem.Open(s), s, stringPool: stringPool)).ToArray();
		}

		public void Dispose()
		{
			if (Interlocked.Exchange(ref disposeState, 1) != 0)
				return;

			var failure = RollbackConstruction();
			failure?.Throw();
		}

		internal void DrainActivationsAndUnbind(Action<System.Threading.Tasks.Task> drainFence)
		{
			if (drainFence == null)
				throw new ArgumentNullException(nameof(drainFence));

			var failure = DrainActivationsAndUnbindCore(drainFence);
			failure?.Throw();
		}

		ExceptionDispatchInfo DrainActivationsAndUnbindCore(
			Action<System.Threading.Tasks.Task> drainFence)
		{
			bindingsLive = false;
			var ownedActivationTokens = activationTokens.ToArray();
			activationTokens.Clear();
			var ownedBindTokens = bindTokens.ToArray();
			bindTokens.Clear();

			ExceptionDispatchInfo firstFailure = null;
			void CaptureFirst(Exception exception)
			{
				firstFailure ??= ExceptionDispatchInfo.Capture(exception);
			}

			void DrainAndDispose(IModDataActivationToken token)
			{
				System.Threading.Tasks.Task fence = null;
				try
				{
					fence = token.BeginDrainAsync() ??
						throw new InvalidOperationException("An activation returned a null drain fence.");
				}
				catch (Exception exception)
				{
					CaptureFirst(exception);
				}

				if (fence != null)
					try { drainFence(fence); }
					catch (Exception exception) { CaptureFirst(exception); }

				try { token.Dispose(); }
				catch (Exception exception) { CaptureFirst(exception); }
			}

			// The dispatcher activates first and must remain live while every other
			// activation and the map-preview pipeline drain.
			for (var i = ownedActivationTokens.Length - 1; i > 0; i--)
				DrainAndDispose(ownedActivationTokens[i]);

			System.Threading.Tasks.Task previewFence = null;
			try { previewFence = MapCache?.BeginPreviewDrainAsync(); }
			catch (Exception exception) { CaptureFirst(exception); }

			if (previewFence != null)
				try { drainFence(previewFence); }
				catch (Exception exception) { CaptureFirst(exception); }

			try { MapCache?.DisposePreviewResourcesOnGameThread(); }
			catch (Exception exception) { CaptureFirst(exception); }

			if (ownedActivationTokens.Length != 0)
				DrainAndDispose(ownedActivationTokens[0]);

			for (var i = ownedBindTokens.Length - 1; i >= 0; i--)
				try { ownedBindTokens[i].Dispose(); }
				catch (Exception exception)
				{
					CaptureFirst(exception);
				}

			return firstFailure;
		}

		void DrainLifecycleFence(System.Threading.Tasks.Task fence)
		{
			if (LifecycleDispatchLane is IGameLifecycleDispatchWaiter)
			{
				Game.DrainLifecycleDispatchUntil(LifecycleDispatchLane, fence);
				return;
			}

			if (!fence.IsCompleted)
				throw new InvalidOperationException(
					"The lifecycle lane cannot drain an incomplete fence.");

			if (fence.IsFaulted)
				ExceptionDispatchInfo.Capture(fence.Exception.InnerExceptions[0]).Throw();
			else if (fence.IsCanceled)
				throw new System.Threading.Tasks.TaskCanceledException(fence);
		}

		ExceptionDispatchInfo RollbackConstruction()
		{
			bindingsLive = false;
			var provider = Interlocked.Exchange(ref serviceProvider, null);
			var firstFailure = DrainActivationsAndUnbindCore(DrainLifecycleFence);
			void CaptureFirst(Exception exception)
			{
				firstFailure ??= ExceptionDispatchInfo.Capture(exception);
			}

			try { MapCache?.Dispose(); }
			catch (Exception exception)
			{
				CaptureFirst(exception);
			}

			try { LoadScreen?.Dispose(); }
			catch (Exception exception)
			{
				CaptureFirst(exception);
			}

			try { ModFiles?.UnmountAll(); }
			catch (Exception exception)
			{
				CaptureFirst(exception);
			}

			try { ObjectCreator?.Dispose(); }
			catch (Exception exception)
			{
				CaptureFirst(exception);
			}

			try { Manifest?.Dispose(); }
			catch (Exception exception)
			{
				CaptureFirst(exception);
			}

			if (ownsManifestPackage)
				try { Manifest?.Package.Dispose(); }
				catch (Exception exception)
				{
					CaptureFirst(exception);
				}

			if (ownsServiceProvider)
				try { provider?.Dispose(); }
				catch (Exception exception)
				{
					CaptureFirst(exception);
				}

			return firstFailure;
		}
	}

	public interface ILoadScreen : IDisposable
	{
		/// <summary>Initializes the loadscreen with yaml data from the LoadScreen block in mod.yaml.</summary>
		void Init(ModData m, Dictionary<string, string> info);

		/// <summary>Called at arbitrary times during mod load to rerender the loadscreen.</summary>
		void Display();

		/// <summary>
		/// Called before loading the mod assets.
		/// Returns false if mod loading should be aborted (e.g. switching to another mod instead).
		/// </summary>
		bool BeforeLoad();

		/// <summary>Called when the engine expects to connect to a server/replay or load the shellmap.</summary>
		void StartGame(Arguments args);
	}

	public interface IFileSystemLoader
	{
		void Mount(FS fileSystem, ObjectCreator objectCreator);
	}
}
