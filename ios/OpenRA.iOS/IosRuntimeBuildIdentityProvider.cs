using System;
using System.Threading;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.iOS;

internal sealed class IosRuntimeBuildIdentityProvider : IRuntimeBuildIdentityProvider, IDisposable
{
	readonly object sync = new();
	readonly RuntimeBuildIdentityReader reader;
	IosRuntimeManagedImageSource? ownedBundleImages;
	int activeCaptures;
	bool disposing;
	bool disposed;

	internal IosRuntimeBuildIdentityProvider(
		IosRuntimeManagedImageSource ownedBundleImages,
		string canonicalAotRegistryIdentity)
	{
		this.ownedBundleImages = ownedBundleImages ??
			throw new ArgumentNullException(nameof(ownedBundleImages));
		try
		{
			reader = new RuntimeBuildIdentityReader(
				ownedBundleImages, canonicalAotRegistryIdentity);
		}
		catch
		{
			this.ownedBundleImages = null;
			ownedBundleImages.Dispose();
			throw;
		}
	}

	public RetailOperationResult<RuntimeBuildIdentity> Capture(
		RetailValidationDeadline alreadyStartedDeadline,
		IRetailMonotonicClock clock,
		CancellationToken cancellationToken)
	{
		if (clock == null || !alreadyStartedDeadline.MatchesClock(clock))
			return RetailOperationResult<RuntimeBuildIdentity>.Failed(
				RetailRuntimeContractFailures.ProfileMismatch());

		var checkpoint = RetailRuntimeContractFailures.Checkpoint(
			alreadyStartedDeadline, clock, cancellationToken);
		if (checkpoint != null)
			return RetailOperationResult<RuntimeBuildIdentity>.Failed(checkpoint);

		lock (sync)
		{
			if (disposing || disposed || ownedBundleImages == null)
				return RetailOperationResult<RuntimeBuildIdentity>.Failed(
					RetailRuntimeContractFailures.RuntimeMissing());

			activeCaptures++;
		}

		try
		{
			return reader.Capture(alreadyStartedDeadline, clock, cancellationToken);
		}
		catch
		{
			if (clock == null || !alreadyStartedDeadline.MatchesClock(clock))
				return RetailOperationResult<RuntimeBuildIdentity>.Failed(
					RetailRuntimeContractFailures.ProfileMismatch());

			return RetailOperationResult<RuntimeBuildIdentity>.Failed(
				RetailRuntimeContractFailures.Checkpoint(
					alreadyStartedDeadline, clock, cancellationToken) ??
				RetailRuntimeContractFailures.RuntimeMissing());
		}
		finally
		{
			lock (sync)
			{
				activeCaptures--;
				if (activeCaptures == 0)
					Monitor.PulseAll(sync);
			}
		}
	}

	public void Dispose()
	{
		IosRuntimeManagedImageSource? release;
		lock (sync)
		{
			if (disposed)
				return;
			if (disposing)
			{
				while (!disposed)
					Monitor.Wait(sync);
				return;
			}

			disposing = true;
			while (activeCaptures != 0)
				Monitor.Wait(sync);

			release = ownedBundleImages;
			ownedBundleImages = null;
		}

		try
		{
			release?.Dispose();
		}
		finally
		{
			lock (sync)
			{
				disposed = true;
				disposing = false;
				Monitor.PulseAll(sync);
			}
		}
	}
}
