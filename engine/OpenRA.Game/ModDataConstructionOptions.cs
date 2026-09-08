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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace OpenRA
{
	internal interface IGameLifecycleDispatchLane
	{
		bool IsCurrentThread { get; }
		void Post(Action action);
		int PumpAvailable(int maxActions);
	}

	internal interface IGameLifecycleDispatchWaiter
	{
		bool IsQuiescent { get; }
		bool IsClosed { get; }
		IGameLifecycleDispatchWaitRegistration RegisterWait(Task completion);
	}

	internal interface IGameLifecycleDispatchWaitRegistration : IDisposable
	{
		void Wait();
	}

	internal sealed class LegacyGameLifecycleDispatchLane : IGameLifecycleDispatchLane
	{
		readonly int ownerThread = Environment.CurrentManagedThreadId;
		public bool IsCurrentThread => Environment.CurrentManagedThreadId == ownerThread;
		public void Post(Action action)
		{
			if (action == null)
				throw new ArgumentNullException(nameof(action));

			Game.RunAfterTick(action);
		}

		public int PumpAvailable(int maxActions) => 0;
	}

	internal sealed class GameLifecycleDispatchLane :
		IGameLifecycleDispatchLane, IGameLifecycleDispatchWaiter, IDisposable
	{
		readonly int ownerThread = Environment.CurrentManagedThreadId;
		readonly object syncRoot = new();
		readonly Queue<Action> actions = new();
		readonly AutoResetEvent workAvailable = new(false);
		readonly ManualResetEvent closeAvailable = new(false);
		bool disposed;
		bool waitHandlesDisposed;
		int waitRegistrationCount;

		public bool IsCurrentThread => Environment.CurrentManagedThreadId == ownerThread;
		public bool IsQuiescent
		{
			get
			{
				lock (syncRoot)
					return actions.Count == 0;
			}
		}

		public bool IsClosed
		{
			get
			{
				lock (syncRoot)
					return disposed;
			}
		}

		public void Post(Action action)
		{
			if (action == null)
				throw new ArgumentNullException(nameof(action));

			lock (syncRoot)
			{
				if (disposed)
					throw new ObjectDisposedException(nameof(GameLifecycleDispatchLane));

				actions.Enqueue(action);
				workAvailable.Set();
			}
		}

		public int PumpAvailable(int maxActions)
		{
			if (maxActions <= 0)
				throw new ArgumentOutOfRangeException(nameof(maxActions));
			if (!IsCurrentThread)
				throw new InvalidOperationException("Lifecycle actions must run on their owning game thread.");

			var processed = 0;
			while (processed < maxActions)
			{
				Action action;
				lock (syncRoot)
				{
					if (disposed)
						throw new ObjectDisposedException(nameof(GameLifecycleDispatchLane));
					if (actions.Count == 0)
						break;
					action = actions.Dequeue();
				}

				action();
				processed++;
			}

			lock (syncRoot)
			{
				if (disposed)
					throw new ObjectDisposedException(nameof(GameLifecycleDispatchLane));
				if (actions.Count != 0)
					workAvailable.Set();
			}

			return processed;
		}

		public IGameLifecycleDispatchWaitRegistration RegisterWait(Task completion)
		{
			if (completion == null)
				throw new ArgumentNullException(nameof(completion));
			if (!IsCurrentThread)
				throw new InvalidOperationException("Lifecycle work must register waits on its owning game thread.");

			var registration = new WaitRegistration(this, completion);
			lock (syncRoot)
			{
				if (disposed)
					throw new ObjectDisposedException(nameof(GameLifecycleDispatchLane));
				waitRegistrationCount++;
			}

			return registration;
		}

		void WaitForWorkOrCompletion(Task completion)
		{
			if (!IsCurrentThread)
				throw new InvalidOperationException("Lifecycle work must wait on its owning game thread.");
			WaitHandle.WaitAny(new[]
			{
				workAvailable,
				closeAvailable,
				((IAsyncResult)completion).AsyncWaitHandle,
			});
		}

		void ReleaseWaitRegistration()
		{
			var disposeWaitHandles = false;
			lock (syncRoot)
			{
				waitRegistrationCount--;
				if (waitRegistrationCount < 0)
					throw new InvalidOperationException("Lifecycle wait registration underflow.");
				if (disposed && waitRegistrationCount == 0 && !waitHandlesDisposed)
				{
					waitHandlesDisposed = true;
					disposeWaitHandles = true;
				}
			}

			if (disposeWaitHandles)
				DisposeWaitHandles();
		}

		public void Dispose()
		{
			var disposeWaitHandles = false;
			lock (syncRoot)
			{
				if (disposed)
					return;
				if (actions.Count != 0)
					throw new InvalidOperationException("Lifecycle actions remain queued.");

				disposed = true;
				closeAvailable.Set();
				if (waitRegistrationCount == 0)
				{
					waitHandlesDisposed = true;
					disposeWaitHandles = true;
				}
			}

			if (disposeWaitHandles)
				DisposeWaitHandles();
		}

		void DisposeWaitHandles()
		{
			workAvailable.Dispose();
			closeAvailable.Dispose();
		}

		sealed class WaitRegistration : IGameLifecycleDispatchWaitRegistration
		{
			GameLifecycleDispatchLane owner;
			readonly Task completion;

			public WaitRegistration(GameLifecycleDispatchLane owner, Task completion)
			{
				this.owner = owner;
				this.completion = completion;
			}

			public void Wait()
			{
				var currentOwner = Volatile.Read(ref owner);
				if (currentOwner == null)
					throw new ObjectDisposedException(nameof(WaitRegistration));

				currentOwner.WaitForWorkOrCompletion(completion);
			}

			public void Dispose()
			{
				var currentOwner = Interlocked.Exchange(ref owner, null);
				currentOwner?.ReleaseWaitRegistration();
			}
		}
	}

	internal interface IModDataServiceProvider : IDisposable
	{
		bool TryGet<T>(out T service) where T : class;
		bool TryGet(Type serviceType, out object service);
		ImmutableArray<ModDataServiceRegistration> Registrations { get; }
	}

	public interface IModDataBoundService
	{
		IDisposable Bind(ModData modData);
	}

	public interface IModDataActivationToken : IDisposable
	{
		Task BeginDrainAsync();
	}

	public interface IModDataActivatedService : IModDataBoundService
	{
		IModDataActivationToken Activate();
	}

	public readonly struct ModDataServiceRegistration : IEquatable<ModDataServiceRegistration>
	{
		public Type ServiceType { get; }
		public object Service { get; }

		ModDataServiceRegistration(Type serviceType, object service)
		{
			ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
			Service = service ?? throw new ArgumentNullException(nameof(service));
			if (!serviceType.IsInstanceOfType(service))
				throw new ArgumentException("The service does not implement its registration type.", nameof(service));
		}

		public static ModDataServiceRegistration Owned<T>(T service) where T : class =>
			new(typeof(T), service);

		internal bool IsValid => ServiceType != null && Service != null && ServiceType.IsInstanceOfType(Service);

		public bool Equals(ModDataServiceRegistration other) =>
			ReferenceEquals(ServiceType, other.ServiceType) && ReferenceEquals(Service, other.Service);
		public override bool Equals(object obj) => obj is ModDataServiceRegistration other && Equals(other);
		public override int GetHashCode()
		{
			unchecked
			{
				return (ServiceType == null ? 0 : RuntimeHelpers.GetHashCode(ServiceType)) * 397 ^
					(Service == null ? 0 : RuntimeHelpers.GetHashCode(Service));
			}
		}

		public static bool operator ==(ModDataServiceRegistration left, ModDataServiceRegistration right) =>
			left.Equals(right);
		public static bool operator !=(ModDataServiceRegistration left, ModDataServiceRegistration right) =>
			!left.Equals(right);
	}

	public delegate IFileSystemLoader FileSystemLoaderDecorator(IFileSystemLoader configuredLoader);

	public enum FluentInitializationMode
	{
		Isolated,
		PublishGlobal,
	}

	public enum ModDataRenderingMode
	{
		NoRender,
		Game,
	}

	public sealed class ModDataSettingsSnapshot
	{
		readonly Dictionary<string, Hotkey> hotkeys;
		Dictionary<string, Hotkey> publishedHotkeys;

		public string Language { get; }
		public string SystemLanguageTag { get; }
		internal int GraphicsSheetSize { get; }

		ModDataSettingsSnapshot(
			string language,
			string systemLanguageTag,
			Dictionary<string, Hotkey> hotkeys,
			int graphicsSheetSize)
		{
			Language = language ?? throw new ArgumentNullException(nameof(language));
			SystemLanguageTag = systemLanguageTag ?? throw new ArgumentNullException(nameof(systemLanguageTag));
			this.hotkeys = hotkeys ?? throw new ArgumentNullException(nameof(hotkeys));
			if (graphicsSheetSize <= 0)
				throw new ArgumentOutOfRangeException(nameof(graphicsSheetSize));
			GraphicsSheetSize = graphicsSheetSize;
		}

		internal static ModDataSettingsSnapshot Capture(Settings settings)
		{
			if (settings == null)
				throw new ArgumentNullException(nameof(settings));

			return new ModDataSettingsSnapshot(
				settings.Game.Language,
				settings.SystemLanguageTag,
				new Dictionary<string, Hotkey>(settings.Keys, StringComparer.Ordinal),
				settings.Graphics.SheetSize);
		}

		internal Dictionary<string, Hotkey> CreateIsolatedHotkeys() =>
			new(hotkeys, StringComparer.Ordinal);

		internal Dictionary<string, Hotkey> TakePublishGlobalHotkeys()
		{
			publishedHotkeys ??= new Dictionary<string, Hotkey>(hotkeys, StringComparer.Ordinal);
			return publishedHotkeys;
		}
	}

	public sealed class ModDataServiceCollection : IDisposable
	{
		readonly List<ModDataServiceRegistration> registrations = new();
		readonly HashSet<Type> serviceTypes = new();
		readonly HashSet<object> serviceObjects = new(ReferenceIdentityComparer.Instance);
		bool sealedCollection;
		bool disposed;

		public bool TryAddOwnedBatch(ImmutableArray<ModDataServiceRegistration> batch)
		{
			if (sealedCollection || disposed || batch.IsDefault)
				return false;

			var batchTypes = new HashSet<Type>();
			var batchObjects = new HashSet<object>(ReferenceIdentityComparer.Instance);
			foreach (var registration in batch)
				if (!registration.IsValid || serviceTypes.Contains(registration.ServiceType) ||
					serviceObjects.Contains(registration.Service) ||
					!batchTypes.Add(registration.ServiceType) || !batchObjects.Add(registration.Service))
					return false;

			foreach (var registration in batch)
			{
				registrations.Add(registration);
				serviceTypes.Add(registration.ServiceType);
				serviceObjects.Add(registration.Service);
			}

			return true;
		}

		public ModDataConstructionOptions Seal(
			FileSystemLoaderDecorator decorateFileSystemLoader,
			ModDataSettingsSnapshot settings,
			FluentInitializationMode fluentMode,
			ModDataRenderingMode renderingMode)
		{
			if (disposed || sealedCollection)
				throw new InvalidOperationException("The service collection is not available.");
			if (decorateFileSystemLoader == null)
				throw new ArgumentNullException(nameof(decorateFileSystemLoader));
			if (settings == null)
				throw new ArgumentNullException(nameof(settings));
			if (!Enum.IsDefined(typeof(FluentInitializationMode), fluentMode))
				throw new ArgumentOutOfRangeException(nameof(fluentMode));
			if (!Enum.IsDefined(typeof(ModDataRenderingMode), renderingMode))
				throw new ArgumentOutOfRangeException(nameof(renderingMode));

			var provider = new ModDataServiceProvider(ImmutableArray.CreateRange(registrations));
			var result = ModDataConstructionOptions.Create(
				provider, decorateFileSystemLoader, settings, fluentMode, renderingMode);
			sealedCollection = true;
			registrations.Clear();
			serviceTypes.Clear();
			serviceObjects.Clear();
			return result;
		}

		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;
			if (sealedCollection)
				return;

			Exception firstFailure = null;
			for (var i = registrations.Count - 1; i >= 0; i--)
				try
				{
					if (registrations[i].Service is IDisposable disposable)
						disposable.Dispose();
				}
				catch (Exception exception)
				{
					firstFailure ??= exception;
				}

			registrations.Clear();
			serviceTypes.Clear();
			serviceObjects.Clear();
			if (firstFailure != null)
				throw firstFailure;
		}

		sealed class ReferenceIdentityComparer : IEqualityComparer<object>
		{
			public static ReferenceIdentityComparer Instance { get; } = new();
			public new bool Equals(object x, object y) => ReferenceEquals(x, y);
			public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
		}
	}

	public sealed class ModDataConstructionOptions : IDisposable
	{
		readonly object ownershipLock = new();
		IModDataServiceProvider provider;
		FileSystemLoaderDecorator decorator;
		ModDataSettingsSnapshot settings;
		readonly FluentInitializationMode fluentMode;
		readonly ModDataRenderingMode renderingMode;
		bool consumed;
		bool disposed;

		ModDataConstructionOptions(
			IModDataServiceProvider provider,
			FileSystemLoaderDecorator decorator,
			ModDataSettingsSnapshot settings,
			FluentInitializationMode fluentMode,
			ModDataRenderingMode renderingMode)
		{
			this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
			this.decorator = decorator ?? throw new ArgumentNullException(nameof(decorator));
			this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
			this.fluentMode = fluentMode;
			this.renderingMode = renderingMode;
		}

		internal static ModDataConstructionOptions Create(
			IModDataServiceProvider provider,
			FileSystemLoaderDecorator decorator,
			ModDataSettingsSnapshot settings,
			FluentInitializationMode fluentMode,
			ModDataRenderingMode renderingMode) =>
			new(provider, decorator, settings, fluentMode, renderingMode);

		internal static ModDataConstructionOptions CreateTrustedDesktopDefault(ModDataSettingsSnapshot settings)
		{
			var provider = new ModDataServiceProvider(ImmutableArray<ModDataServiceRegistration>.Empty);
			return new ModDataConstructionOptions(
				provider, configured => configured, settings,
				FluentInitializationMode.PublishGlobal, ModDataRenderingMode.Game);
		}

		internal bool MatchesModes(
			FluentInitializationMode expectedFluentMode,
			ModDataRenderingMode expectedRenderingMode)
		{
			lock (ownershipLock)
				return !disposed && !consumed && provider != null &&
					fluentMode == expectedFluentMode && renderingMode == expectedRenderingMode;
		}

		internal OwnedModDataConstructionState TakeOwnership()
		{
			lock (ownershipLock)
			{
				if (disposed || consumed || provider == null)
					throw new InvalidOperationException("The construction options were already consumed or disposed.");

				consumed = true;
				var result = new OwnedModDataConstructionState(
					provider, decorator, settings, fluentMode, renderingMode);
				provider = null;
				decorator = null;
				settings = null;
				return result;
			}
		}

		public void Dispose()
		{
			IModDataServiceProvider providerToDispose;
			lock (ownershipLock)
			{
				if (disposed)
					return;

				disposed = true;
				providerToDispose = consumed ? null : provider;
				provider = null;
				decorator = null;
				settings = null;
			}

			providerToDispose?.Dispose();
		}
	}

	internal sealed class OwnedModDataConstructionState : IDisposable
	{
		readonly object providerLock = new();
		IModDataServiceProvider provider;
		int activeBorrows;
		bool ownershipLive = true;
		public IModDataServiceProvider Provider
		{
			get
			{
				lock (providerLock)
					return ownershipLive ? provider : null;
			}
		}

		public FileSystemLoaderDecorator Decorator { get; }
		public ModDataSettingsSnapshot Settings { get; }
		public FluentInitializationMode FluentMode { get; }
		public ModDataRenderingMode RenderingMode { get; }

		public OwnedModDataConstructionState(
			IModDataServiceProvider provider,
			FileSystemLoaderDecorator decorator,
			ModDataSettingsSnapshot settings,
			FluentInitializationMode fluentMode,
			ModDataRenderingMode renderingMode)
		{
			this.provider = provider;
			Decorator = decorator;
			Settings = settings;
			FluentMode = fluentMode;
			RenderingMode = renderingMode;
		}

		public IModDataServiceProvider TakeProvider()
		{
			lock (providerLock)
			{
				if (!ownershipLive || provider == null)
					throw new InvalidOperationException("The provider was already transferred.");
				if (activeBorrows != 0)
					throw new InvalidOperationException("The provider has active borrows.");

				ownershipLive = false;
				var result = provider;
				provider = null;
				return result;
			}
		}

		internal IModDataServiceProvider BorrowProvider()
		{
			lock (providerLock)
			{
				if (!ownershipLive || provider == null)
					throw new InvalidOperationException("The provider is no longer available.");

				activeBorrows = checked(activeBorrows + 1);
				return new BorrowedProvider(this, provider);
			}
		}

		public void Dispose()
		{
			IModDataServiceProvider providerToDispose = null;
			lock (providerLock)
			{
				if (!ownershipLive)
					return;

				ownershipLive = false;
				if (activeBorrows == 0)
				{
					providerToDispose = provider;
					provider = null;
				}
			}

			providerToDispose?.Dispose();
		}

		void ReleaseBorrow()
		{
			IModDataServiceProvider providerToDispose = null;
			lock (providerLock)
			{
				if (activeBorrows <= 0)
					return;

				activeBorrows--;
				if (activeBorrows == 0 && !ownershipLive)
				{
					providerToDispose = provider;
					provider = null;
				}
			}

			providerToDispose?.Dispose();
		}

		sealed class BorrowedProvider : IModDataServiceProvider
		{
			readonly object useLock = new();
			OwnedModDataConstructionState owner;
			IModDataServiceProvider provider;
			int activeUses;
			bool disposed;

			public ImmutableArray<ModDataServiceRegistration> Registrations
			{
				get
				{
					var current = AcquireProvider();
					if (current == null)
						return ImmutableArray<ModDataServiceRegistration>.Empty;

					try { return current.Registrations; }
					finally { ReleaseProvider(); }
				}
			}

			public BorrowedProvider(
				OwnedModDataConstructionState owner,
				IModDataServiceProvider provider)
			{
				this.owner = owner;
				this.provider = provider;
			}

			public bool TryGet<T>(out T service) where T : class
			{
				var current = AcquireProvider();
				if (current != null)
					try { return current.TryGet(out service); }
					finally { ReleaseProvider(); }

				service = null;
				return false;
			}

			public bool TryGet(Type serviceType, out object service)
			{
				var current = AcquireProvider();
				if (current != null)
					try { return current.TryGet(serviceType, out service); }
					finally { ReleaseProvider(); }

				service = null;
				return false;
			}

			IModDataServiceProvider AcquireProvider()
			{
				lock (useLock)
				{
					if (disposed || provider == null)
						return null;

					activeUses = checked(activeUses + 1);
					return provider;
				}
			}

			void ReleaseProvider()
			{
				OwnedModDataConstructionState ownerToRelease = null;
				lock (useLock)
				{
					if (activeUses <= 0)
						return;

					activeUses--;
					if (disposed && activeUses == 0)
					{
						ownerToRelease = owner;
						owner = null;
						provider = null;
					}
				}

				ownerToRelease?.ReleaseBorrow();
			}

			public void Dispose()
			{
				OwnedModDataConstructionState ownerToRelease = null;
				lock (useLock)
				{
					if (disposed)
						return;

					disposed = true;
					if (activeUses == 0)
					{
						ownerToRelease = owner;
						owner = null;
						provider = null;
					}
				}

				ownerToRelease?.ReleaseBorrow();
			}
		}
	}

	internal sealed class ModDataServiceProvider : IModDataServiceProvider
	{
		ImmutableArray<ModDataServiceRegistration> registrations;
		bool disposed;

		public ImmutableArray<ModDataServiceRegistration> Registrations => disposed
			? ImmutableArray<ModDataServiceRegistration>.Empty : registrations;

		public ModDataServiceProvider(ImmutableArray<ModDataServiceRegistration> registrations)
		{
			this.registrations = registrations;
		}

		public bool TryGet<T>(out T service) where T : class
		{
			if (TryGet(typeof(T), out var value))
			{
				service = (T)value;
				return true;
			}

			service = null;
			return false;
		}

		public bool TryGet(Type serviceType, out object service)
		{
			if (serviceType == null)
				throw new ArgumentNullException(nameof(serviceType));
			if (!disposed)
				foreach (var registration in registrations)
					if (registration.ServiceType == serviceType)
					{
						service = registration.Service;
						return true;
					}

			service = null;
			return false;
		}

		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;
			var close = registrations;
			registrations = ImmutableArray<ModDataServiceRegistration>.Empty;
			Exception firstFailure = null;
			for (var i = close.Length - 1; i >= 0; i--)
				try
				{
					if (close[i].Service is IDisposable disposable)
						disposable.Dispose();
				}
				catch (Exception exception)
				{
					firstFailure ??= exception;
				}

			if (firstFailure != null)
				throw firstFailure;
		}
	}
}
