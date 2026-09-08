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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Support;

namespace OpenRA
{
	internal interface IMapPreviewWorkerSynchronization : IDisposable
	{
		void WaitForWake();
		void Wake();
	}

	sealed class MapPreviewWorkerSynchronization : IMapPreviewWorkerSynchronization
	{
		readonly AutoResetEvent wake = new(false);

		public void WaitForWake() => wake.WaitOne();
		public void Wake() => wake.Set();
		public void Dispose() => wake.Dispose();
	}

	public sealed class MapCache : IEnumerable<MapPreview>, IDisposable
	{
		public static readonly MapPreview UnknownMap = new(null, null, MapGridType.Rectangular, null);
		public IReadOnlyDictionary<IReadOnlyPackage, MapClassification> MapLocations => mapLocations;
		readonly Dictionary<IReadOnlyPackage, MapClassification> mapLocations = new();
		public bool LoadPreviewImages = true;

		readonly Cache<string, MapPreview> previews;
		readonly ModData modData;
		SheetBuilder sheetBuilder;
		readonly ModDataRenderingMode renderingMode;
		readonly IGameLifecycleDispatchLane lifecycleLane;
		IMapPreviewWorkerSynchronization previewWorkerWake;
		Thread previewLoaderThread;
		bool previewLoaderThreadShutDown = true;
		bool previewDrainRequested;
		bool previewResourcesDisposed;
		int pendingLifecycleActions;
		Exception previewDrainFailure;
		TaskCompletionSource<object> previewDrain = CompletedDrain();
		readonly object syncRoot = new();
		readonly Queue<MapPreview> generateMinimap = new();

		public HashSet<string> StringPool { get; } = new();

		readonly List<MapDirectoryTracker> mapDirectoryTrackers = new();

		/// <summary>
		/// The most recently modified or loaded map at runtime.
		/// </summary>
		public string LastModifiedMap { get; private set; } = null;
		readonly Dictionary<string, string> mapUpdates = new();

		string lastLoadedLastModifiedMap;

		/// <summary>
		/// If LastModifiedMap was picked already, returns a null.
		/// </summary>
		public string PickLastModifiedMap(MapVisibility visibility)
		{
			UpdateMaps();
			var map = string.IsNullOrEmpty(LastModifiedMap) ? null : this[LastModifiedMap];
			if (map != null && map.Status == MapStatus.Available && map.Visibility.HasFlag(visibility) && lastLoadedLastModifiedMap != LastModifiedMap)
			{
				lastLoadedLastModifiedMap = LastModifiedMap;
				return lastLoadedLastModifiedMap;
			}

			return null;
		}

		public MapCache(ModData modData)
			: this(
				modData,
				ModDataRenderingMode.Game,
				new LegacyGameLifecycleDispatchLane(),
				Game.Settings.Graphics.SheetSize)
		{ }

		internal MapCache(
			ModData modData,
			ModDataRenderingMode renderingMode,
			IGameLifecycleDispatchLane lifecycleLane,
			int graphicsSheetSize)
			: this(modData, renderingMode, lifecycleLane, graphicsSheetSize, null)
		{ }

		internal MapCache(
			ModData modData,
			ModDataRenderingMode renderingMode,
			IGameLifecycleDispatchLane lifecycleLane,
			int graphicsSheetSize,
			IMapPreviewWorkerSynchronization previewWorkerSynchronization)
		{
			this.modData = modData ?? throw new ArgumentNullException(nameof(modData));
			if (!Enum.IsDefined(typeof(ModDataRenderingMode), renderingMode))
				throw new ArgumentOutOfRangeException(nameof(renderingMode));
			if (renderingMode != ModDataRenderingMode.Game && previewWorkerSynchronization != null)
				throw new ArgumentException(
					"Preview worker synchronization is only valid in game rendering mode.",
					nameof(previewWorkerSynchronization));

			this.renderingMode = renderingMode;
			this.lifecycleLane = lifecycleLane ?? RejectingGameLifecycleDispatchLane.Instance;
			LoadPreviewImages = renderingMode == ModDataRenderingMode.Game;

			var gridType = Exts.Lazy(() => modData.Manifest.Get<MapGrid>(modData.ObjectCreator).Type);
			previews = new Cache<string, MapPreview>(uid => new MapPreview(modData, uid, gridType.Value, this));
			if (renderingMode == ModDataRenderingMode.Game)
			{
				if (graphicsSheetSize <= 0)
					throw new ArgumentOutOfRangeException(nameof(graphicsSheetSize));
				sheetBuilder = new SheetBuilder(SheetType.BGRA, graphicsSheetSize);
				previewWorkerWake = previewWorkerSynchronization ?? new MapPreviewWorkerSynchronization();
			}
		}

		static TaskCompletionSource<object> CompletedDrain()
		{
			var result = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
			result.SetResult(null);
			return result;
		}

		public void UpdateMaps()
		{
			foreach (var tracker in mapDirectoryTrackers)
				tracker.UpdateMaps(this);
		}

		public void LoadMaps()
		{
			// Utility mod that does not support maps
			if (!modData.Manifest.Contains<MapGrid>())
				return;

			var mapGrid = modData.Manifest.Get<MapGrid>(modData.ObjectCreator);

			// Enumerate map directories
			foreach (var kv in modData.Manifest.MapFolders)
			{
				var name = kv.Key;
				var classification = string.IsNullOrEmpty(kv.Value)
					? MapClassification.Unknown : Enum<MapClassification>.Parse(kv.Value);

				IReadOnlyPackage package;
				var optional = name.StartsWith('~');
				if (optional)
					name = name[1..];

				try
				{
					// HACK: If the path is inside the support directory then we may need to create it
					// Assume that the path is a directory if there is not an existing file with the same name
					var resolved = Platform.ResolvePath(name);
					if (resolved.StartsWith(Platform.SupportDir, StringComparison.Ordinal) && !File.Exists(resolved))
						Directory.CreateDirectory(resolved);

					package = modData.ModFiles.OpenPackage(name);
				}
				catch
				{
					if (optional)
						continue;

					throw;
				}

				mapLocations.Add(package, classification);

				// iOS does not implement FileSystemWatcher. Personal iPad installs do
				// not support live map-directory edits, so retain the initial scan and
				// skip only the desktop change tracker.
				if (!OperatingSystem.IsIOS())
					mapDirectoryTrackers.Add(new MapDirectoryTracker(mapGrid, package, classification));
			}

			// PERF: Load the mod YAML once outside the loop, and reuse it when resolving each maps custom YAML.
			var modDataRules = modData.GetRulesYaml();
			foreach (var kv in MapLocations)
			{
				foreach (var map in kv.Key.Contents)
					LoadMapInternal(map, kv.Key, kv.Value, mapGrid, null, modDataRules);
			}

			// We only want to track maps in runtime, not at loadtime
			LastModifiedMap = null;
		}

		public void LoadMap(string map, IReadOnlyPackage package, MapClassification classification, MapGrid mapGrid, string oldMap)
		{
			LoadMapInternal(map, package, classification, mapGrid, oldMap, null);
		}

		void LoadMapInternal(string map, IReadOnlyPackage package, MapClassification classification, MapGrid mapGrid, string oldMap,
			IEnumerable<List<MiniYamlNode>> modDataRules)
		{
			IReadOnlyPackage mapPackage = null;
			try
			{
				using (new PerfTimer(map))
				{
					mapPackage = package.OpenPackage(map, modData.ModFiles);
					if (mapPackage != null)
					{
						var uid = Map.ComputeUID(mapPackage);
						previews[uid].UpdateFromMap(mapPackage, package, classification, modData.Manifest.MapCompatibility, mapGrid.Type, modDataRules);

						// Freeing the package to save memory if there is a lot of Maps
						previews[uid].DisposePackage();

						if (oldMap != uid)
						{
							LastModifiedMap = uid;
							if (oldMap != null)
								mapUpdates[oldMap] = uid;
						}
					}
				}
			}
			catch (Exception e)
			{
				mapPackage?.Dispose();
				Console.WriteLine($"Failed to load map: {map}");
				Console.WriteLine("Details:");
				Console.WriteLine(e);
				Log.Write("debug", $"Failed to load map: {map}");
				Log.Write("debug", "Details:");
				Log.Write("debug", e);
			}
		}

		public IEnumerable<IReadWritePackage> EnumerateMapDirPackages(MapClassification classification = MapClassification.System)
		{
			// Utility mod that does not support maps
			if (!modData.Manifest.Contains<MapGrid>())
				yield break;

			// Enumerate map directories
			foreach (var kv in modData.Manifest.MapFolders)
			{
				if (!Enum.TryParse(kv.Value, out MapClassification packageClassification))
					continue;

				if (!classification.HasFlag(packageClassification))
					continue;

				var name = kv.Key;
				var optional = name.StartsWith('~');
				if (optional)
					name = name[1..];

				// Don't try to open the map directory in the support directory if it doesn't exist
				var resolved = Platform.ResolvePath(name);
				if (resolved.StartsWith(Platform.SupportDir, StringComparison.Ordinal) && (!Directory.Exists(resolved) || !File.Exists(resolved)))
					continue;

				using (var package = (IReadWritePackage)modData.ModFiles.OpenPackage(name))
					yield return package;
			}
		}

		public IEnumerable<(IReadWritePackage Package, string Map)> EnumerateMapDirPackagesAndNames(MapClassification classification = MapClassification.System)
		{
			var mapDirPackages = EnumerateMapDirPackages(classification);

			foreach (var mapDirPackage in mapDirPackages)
				foreach (var map in mapDirPackage.Contents)
					yield return (mapDirPackage, map);
		}

		public IEnumerable<IReadWritePackage> EnumerateMapPackagesWithoutCaching(MapClassification classification = MapClassification.System)
		{
			var mapDirPackages = EnumerateMapDirPackages(classification);

			foreach (var mapDirPackage in mapDirPackages)
				foreach (var map in mapDirPackage.Contents)
					if (mapDirPackage.OpenPackage(map, modData.ModFiles) is IReadWritePackage mapPackage)
						yield return mapPackage;
		}

		public void QueryRemoteMapDetails(string repositoryUrl, IEnumerable<string> uids,
			Action<MapPreview> mapDetailsReceived = null, Action<MapPreview> mapQueryFailed = null)
		{
			var queryUids = uids.Distinct()
				.Where(uid => uid != null)
				.Select(uid => previews[uid])
				.Where(p => p.Status == MapStatus.Unavailable)
				.Select(p => p.Uid)
				.ToList();

			foreach (var uid in queryUids)
				previews[uid].UpdateRemoteSearch(MapStatus.Searching, null, null);

			Task.Run(async () =>
			{
				var client = HttpClientFactory.Create();
				var stringPool = new HashSet<string>(); // Reuse common strings in YAML
				var buffer = new StringBuilder();

				// Limit each query to 50 maps at a time to avoid request size limits
				for (var i = 0; i < queryUids.Count; i += 50)
				{
					var batchUids = queryUids.Skip(i).Take(50).ToList();
					var url = repositoryUrl + "hash/" + string.Join(",", batchUids) + "/yaml";

					using (new PerfTimer("RemoteMapDetails"))
					{
						try
						{
							await using (var resultStream = await client.GetStreamAsync(url))
							{
								using (var resultReader = new StreamReader(resultStream))
								{
									while (true)
									{
										var line = await resultReader.ReadLineAsync();
										if (line == null || !line.StartsWith('\t'))
										{
											var yaml = MiniYaml.FromString(buffer.ToString(), url, stringPool: stringPool);
											buffer.Clear();
											foreach (var kv in yaml)
												previews[kv.Key].UpdateRemoteSearch(MapStatus.DownloadAvailable, kv.Value, modData.Manifest.MapCompatibility, mapDetailsReceived);

											if (line == null)
												break;
										}

										buffer.Append(line);
										buffer.Append('\n');
									}
								}
							}

							foreach (var uid in batchUids)
							{
								var p = previews[uid];
								if (p.Status != MapStatus.DownloadAvailable)
									p.UpdateRemoteSearch(MapStatus.Unavailable, null, null);
							}
						}
						catch (Exception e)
						{
							Log.Write("debug", "Remote map query failed with error:");
							Log.Write("debug", e);
							Log.Write("debug", $"URL was: {url}");

							foreach (var uid in batchUids)
							{
								var p = previews[uid];
								p.UpdateRemoteSearch(MapStatus.Unavailable, null, null);
								mapQueryFailed?.Invoke(p);
							}
						}
					}
				}
			});
		}

		void LoadAsyncInternal()
		{
			Log.Write("debug", "MapCache.LoadAsyncInternal started");
			Exception failure = null;

			try
			{
				while (true)
				{
					List<MapPreview> todo = null;
					lock (syncRoot)
					{
						if (generateMinimap.Count == 0)
						{
							if (previewDrainRequested)
								break;
						}
						else
						{
							todo = generateMinimap.Where(p => p.GetMinimap() == null).ToList();
							generateMinimap.Clear();
						}
					}

					if (todo == null)
					{
						previewWorkerWake.WaitForWake();
						continue;
					}

					// Render the minimap into the shared sheet through the ModData-owned lane.
					foreach (var p in todo)
					{
						if (p.Preview != null)
						{
							PostLifecycleAction(() =>
							{
								try
								{
									p.SetMinimap(sheetBuilder.Add(p.Preview));
								}
								catch (Exception e)
								{
									Log.Write("debug", "Failed to load minimap with exception:");
									Log.Write("debug", e);
									throw;
								}
							});
						}
					}
				}
			}
			catch (Exception exception)
			{
				failure = exception;
				lock (syncRoot)
				{
					previewDrainRequested = true;
					generateMinimap.Clear();
				}

				Log.Write("debug", "Map preview worker failed while dispatching lifecycle work:");
				Log.Write("debug", exception);
			}
			finally
			{
				// Release the buffer on the same lifecycle lane as every upload.
				try { PostLifecycleAction(sheetBuilder.Current.ReleaseBuffer); }
				catch (Exception exception)
				{
					failure ??= exception;
					Log.Write("debug", "Map preview buffer release was rejected by the lifecycle lane:");
					Log.Write("debug", exception);
				}

				lock (syncRoot)
				{
					previewDrainFailure ??= failure;
					previewLoaderThread = null;
					previewLoaderThreadShutDown = true;
					TryCompletePreviewDrain();
				}

				Log.Write("debug", "MapCache.LoadAsyncInternal ended");
			}
		}

		void PostLifecycleAction(Action action)
		{
			lock (syncRoot)
				pendingLifecycleActions++;

			try
			{
				lifecycleLane.Post(() =>
				{
					Exception failure = null;
					try
					{
						action();
					}
					catch (Exception exception)
					{
						failure = exception;
						throw;
					}
					finally
					{
						lock (syncRoot)
						{
							previewDrainFailure ??= failure;
							pendingLifecycleActions--;
							TryCompletePreviewDrain();
						}
					}
				});
			}
			catch (Exception exception)
			{
				lock (syncRoot)
				{
					previewDrainFailure ??= exception;
					pendingLifecycleActions--;
					TryCompletePreviewDrain();
				}

				throw;
			}
		}

		void TryCompletePreviewDrain()
		{
			if (previewLoaderThreadShutDown && pendingLifecycleActions == 0 && generateMinimap.Count == 0)
			{
				if (previewDrainFailure != null)
					previewDrain.TrySetException(previewDrainFailure);
				else
					previewDrain.TrySetResult(null);
			}
		}

		public string GetUpdatedMap(string uid)
		{
			if (uid == null)
				return null;

			while (this[uid].Status != MapStatus.Available)
			{
				if (mapUpdates.TryGetValue(uid, out var newUid))
					uid = newUid;
				else
					return null;
			}

			return uid;
		}

		public void CacheMinimap(MapPreview preview)
		{
			if (renderingMode != ModDataRenderingMode.Game)
				throw new InvalidOperationException("Map previews are unavailable in no-render mode.");

			lock (syncRoot)
			{
				if (previewDrainRequested || previewResourcesDisposed || sheetBuilder == null)
					throw new ObjectDisposedException(nameof(MapCache));

				if (previewLoaderThreadShutDown)
				{
					previewLoaderThreadShutDown = false;
					previewDrainFailure = null;
					previewDrain = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
					var worker = new Thread(LoadAsyncInternal)
					{
						Name = "Map Preview Loader",
						IsBackground = true
					};
					previewLoaderThread = worker;
					try { worker.Start(); }
					catch (Exception exception)
					{
						previewLoaderThread = null;
						previewLoaderThreadShutDown = true;
						previewDrainFailure = exception;
						TryCompletePreviewDrain();

						throw;
					}
				}

				generateMinimap.Enqueue(preview);
				previewWorkerWake.Wake();
			}
		}

		internal Task BeginPreviewDrainAsync()
		{
			lock (syncRoot)
			{
				previewDrainRequested = true;
				previewWorkerWake?.Wake();
				TryCompletePreviewDrain();
				return previewDrain.Task;
			}
		}

		static void DisposeOwned(IDisposable owned, ref Exception failure)
		{
			if (owned == null)
				return;

			try { owned.Dispose(); }
			catch (Exception exception) { failure ??= exception; }
		}

		internal void DisposePreviewResourcesOnGameThread()
		{
			if (previewResourcesDisposed)
				return;
			if (!lifecycleLane.IsCurrentThread)
				throw new InvalidOperationException("Map preview resources must be disposed on their lifecycle thread.");
			if (!BeginPreviewDrainAsync().IsCompleted)
				throw new InvalidOperationException("Map preview resources cannot be disposed before their drain completes.");

			MapPreview[] ownedPreviews;
			MapDirectoryTracker[] ownedTrackers;
			SheetBuilder ownedSheetBuilder;
			IMapPreviewWorkerSynchronization ownedWorkerWake;
			lock (syncRoot)
			{
				if (previewResourcesDisposed)
					return;

				previewResourcesDisposed = true;
				ownedPreviews = previews.Values.ToArray();
				previews.Clear();
				ownedTrackers = mapDirectoryTrackers.ToArray();
				mapDirectoryTrackers.Clear();
				ownedSheetBuilder = sheetBuilder;
				sheetBuilder = null;
				ownedWorkerWake = previewWorkerWake;
				previewWorkerWake = null;
			}

			Exception failure = null;
			foreach (var preview in ownedPreviews)
				DisposeOwned(preview, ref failure);
			foreach (var tracker in ownedTrackers)
				DisposeOwned(tracker, ref failure);
			DisposeOwned(ownedSheetBuilder, ref failure);
			DisposeOwned(ownedWorkerWake, ref failure);

			if (failure != null)
				ExceptionDispatchInfo.Capture(failure).Throw();
		}

		bool IsSuitableInitialMap(MapPreview map)
		{
			if (map.Status != MapStatus.Available || !map.Visibility.HasFlag(MapVisibility.Lobby))
				return false;

			// Other map types may have confusing settings or gameplay
			if (!map.Categories.Contains("Conquest"))
				return false;

			// Maps with bots disabled confuse new players
			if (map.Players.Players.Any(x => !x.Value.AllowBots))
				return false;

			// Large maps expose unfortunate performance problems
			if (map.Bounds.Width > 128 || map.Bounds.Height > 128)
				return false;

			return true;
		}

		public string ChooseInitialMap(string initialUid, MersenneTwister random)
		{
			UpdateMaps();
			var map = string.IsNullOrEmpty(initialUid) ? null : previews[initialUid];
			if (map == null ||
				map.Status != MapStatus.Available ||
				!map.Visibility.HasFlag(MapVisibility.Lobby) ||
				(map.Class != MapClassification.System && map.Class != MapClassification.User))
			{
				var selected = previews.Values.Where(IsSuitableInitialMap).RandomOrDefault(random) ??
					previews.Values.FirstOrDefault(m =>
					m.Status == MapStatus.Available &&
					m.Visibility.HasFlag(MapVisibility.Lobby) &&
					(m.Class == MapClassification.System || m.Class == MapClassification.User));
				return selected == null ? string.Empty : selected.Uid;
			}

			return initialUid;
		}

		public MapPreview this[string key]
		{
			get
			{
				UpdateMaps();
				return previews[key];
			}
		}

		public IEnumerator<MapPreview> GetEnumerator()
		{
			UpdateMaps();
			return previews.Values.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public void Dispose()
		{
			DisposePreviewResourcesOnGameThread();
		}
	}
}
