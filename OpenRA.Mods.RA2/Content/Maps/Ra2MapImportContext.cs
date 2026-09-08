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
using System.Threading;
using OpenRA.FileSystem;

namespace OpenRA.Mods.RA2.Content
{
	public sealed class Ra2MapImportContext : IDisposable
	{
		PreGameModDataScope ownedScope;
		Func<RetailStagingTarget, IReadWritePackage> outputFactory;

		public Ra2RuntimeProfileId Profile { get; }
		public ModData ModData => ownedScope?.ModData ??
			throw new ObjectDisposedException(nameof(Ra2MapImportContext));
		public Func<RetailStagingTarget, IReadWritePackage> CreateOutputPackage => outputFactory ??
			throw new ObjectDisposedException(nameof(Ra2MapImportContext));

		Ra2MapImportContext(
			PreGameModDataScope ownedScope,
			Ra2RuntimeProfileId profile,
			Func<RetailStagingTarget, IReadWritePackage> outputFactory)
		{
			this.ownedScope = ownedScope;
			Profile = profile;
			this.outputFactory = outputFactory;
		}

		public static RetailOperationResult<Ra2MapImportContext> Create(
			PreGameModDataScope ownedScope,
			Ra2RuntimeProfileId profile,
			Func<RetailStagingTarget, IReadWritePackage> outputFactory)
		{
			if (!Ra2MapContextValidation.IsValid(ownedScope?.ModData, profile) || outputFactory == null)
			{
				Ra2MapContextValidation.DisposeFailedOwner(ownedScope);
				return RetailOperationResult<Ra2MapImportContext>.Failed(
					Ra2MapContextValidation.InvalidContextFailure());
			}

			if (!Ra2MapContextValidation.TryTakeOwnership(ownedScope, out var transferredScope))
				return RetailOperationResult<Ra2MapImportContext>.Failed(
					Ra2MapContextValidation.InvalidContextFailure());

			try
			{
				return RetailOperationResult<Ra2MapImportContext>.Success(
					new Ra2MapImportContext(transferredScope, profile, outputFactory));
			}
			catch
			{
				Ra2MapContextValidation.DisposeFailedOwner(transferredScope);
				throw;
			}
		}

		public void Dispose()
		{
			var scope = Interlocked.Exchange(ref ownedScope, null);
			Interlocked.Exchange(ref outputFactory, null);
			scope?.Dispose();
		}
	}

	public sealed class Ra2MapValidationContext : IDisposable
	{
		PreGameModDataScope ownedScope;
		IDisposable ownedBindingPin;
		ModData modData;

		public Ra2RuntimeProfileId Profile { get; }
		public ModData ModData => modData ??
			throw new ObjectDisposedException(nameof(Ra2MapValidationContext));

		Ra2MapValidationContext(
			PreGameModDataScope ownedScope,
			Ra2RuntimeProfileId profile)
		{
			this.ownedScope = ownedScope;
			modData = ownedScope.ModData;
			Profile = profile;
		}

		Ra2MapValidationContext(
			ModData borrowedModData,
			IDisposable ownedBindingPin,
			Ra2RuntimeProfileId profile)
		{
			modData = borrowedModData;
			this.ownedBindingPin = ownedBindingPin;
			Profile = profile;
		}

		public static RetailOperationResult<Ra2MapValidationContext> Create(
			PreGameModDataScope ownedScope,
			Ra2RuntimeProfileId profile)
		{
			if (!Ra2MapContextValidation.IsValid(ownedScope?.ModData, profile))
			{
				Ra2MapContextValidation.DisposeFailedOwner(ownedScope);
				return RetailOperationResult<Ra2MapValidationContext>.Failed(
					Ra2MapContextValidation.InvalidContextFailure());
			}

			if (!Ra2MapContextValidation.TryTakeOwnership(ownedScope, out var transferredScope))
				return RetailOperationResult<Ra2MapValidationContext>.Failed(
					Ra2MapContextValidation.InvalidContextFailure());

			try
			{
				return RetailOperationResult<Ra2MapValidationContext>.Success(
					new Ra2MapValidationContext(transferredScope, profile));
			}
			catch
			{
				Ra2MapContextValidation.DisposeFailedOwner(transferredScope);
				throw;
			}
		}

		internal static RetailOperationResult<Ra2MapValidationContext> CreateBound(
			ModData borrowedModData,
			IDisposable ownedBindingPin,
			Ra2RuntimeProfileId profile)
		{
			if (!Ra2MapContextValidation.IsValid(borrowedModData, profile) || ownedBindingPin == null)
			{
				Ra2MapContextValidation.DisposeFailedOwner(ownedBindingPin);
				return RetailOperationResult<Ra2MapValidationContext>.Failed(
					Ra2MapContextValidation.InvalidContextFailure());
			}

			return RetailOperationResult<Ra2MapValidationContext>.Success(
				new Ra2MapValidationContext(borrowedModData, ownedBindingPin, profile));
		}

		public void Dispose()
		{
			var scope = Interlocked.Exchange(ref ownedScope, null);
			var pin = Interlocked.Exchange(ref ownedBindingPin, null);
			Interlocked.Exchange(ref modData, null);

			try
			{
				scope?.Dispose();
			}
			finally
			{
				pin?.Dispose();
			}
		}
	}

	static class Ra2MapContextValidation
	{
		public static bool IsValid(ModData modData, Ra2RuntimeProfileId profile)
		{
			if (modData == null ||
				(profile != Ra2RuntimeProfileId.Base && profile != Ra2RuntimeProfileId.Yuri))
				return false;

			return modData.Selection == profile.ManifestSelection();
		}

		public static RetailImportFailure InvalidContextFailure() => new(
			RetailImportErrorCode.ProfileContextFailure,
			null,
			null,
			RetailImportDiagnosticCode.InvalidMapPackage);

		public static bool TryTakeOwnership(
			PreGameModDataScope source,
			out PreGameModDataScope owner)
		{
			try
			{
				owner = source.TakeOwnership();
				return true;
			}
			catch (InvalidOperationException)
			{
				owner = null;
				return false;
			}
		}

		public static void DisposeFailedOwner(IDisposable owner)
		{
			try
			{
				owner?.Dispose();
			}
			catch
			{
				// A failed factory never returns ownership, and cleanup failures must not
				// expose an implementation exception through the typed import boundary.
			}
		}
	}
}
