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
using System.Threading;
using System.Threading.Tasks;

namespace OpenRA.Mods.RA2.Content
{
	public static class RetailSourceDiscovery
	{
		public static async Task<RetailOperationResult<RetailCandidatePlan>> DiscoverAsync(
			IRetailImportSourceSession sources, IRetailMonotonicClock clock,
			RetailValidationDeadline deadline, CancellationToken cancellationToken)
		{
			if (sources == null)
				throw new ArgumentNullException(nameof(sources));
			if (clock == null)
				throw new ArgumentNullException(nameof(clock));

			cancellationToken.ThrowIfCancellationRequested();
			var budget = new RetailDiscoveryBudget(
				IosImportEnvelopePolicyV1.DiscoveryDepth,
				IosImportEnvelopePolicyV1.DiscoveryEntries,
				IosImportEnvelopePolicyV1.DiscoveryBytes,
				clock,
				deadline);

			try
			{
				var initialCheckpoint = budget.Checkpoint();
				if (!initialCheckpoint.Succeeded)
					return RetailOperationResult<RetailCandidatePlan>.Failed(initialCheckpoint.Failure);

				var opened = await sources.OpenEnumerationAsync(budget, cancellationToken);
				if (!opened.Succeeded)
					return RetailOperationResult<RetailCandidatePlan>.Failed(opened.Failure);

				var entries = new List<RetailSourceEntry>();
				long observedKnownBytes = 0;
				await using (var enumeration = opened.Value)
				{
					while (true)
					{
						cancellationToken.ThrowIfCancellationRequested();
						var checkpoint = budget.Checkpoint();
						if (!checkpoint.Succeeded)
							return RetailOperationResult<RetailCandidatePlan>.Failed(checkpoint.Failure);

						var next = await enumeration.ReadNextAsync(cancellationToken);
						if (!next.Succeeded)
							return RetailOperationResult<RetailCandidatePlan>.Failed(next.Failure);

						var read = next.Value;
						if (read.Kind == RetailSourceReadKind.Complete)
						{
							var completed = budget.VerifyComplete();
							if (!completed.Succeeded)
								return RetailOperationResult<RetailCandidatePlan>.Failed(completed.Failure);
							if (entries.Count != budget.AcceptedEntries || observedKnownBytes != budget.AcceptedKnownBytes)
								return Failed(RetailImportErrorCode.SourceChanged, RetailImportDiagnosticCode.LengthMismatch);

							break;
						}

						if (read.Kind != RetailSourceReadKind.Entry)
							return Failed(RetailImportErrorCode.SourceChanged, RetailImportDiagnosticCode.MalformedEnvelope);

						var entry = read.Entry;
						var verified = budget.VerifyAcceptedEntry(DirectoryDepth(entry.Id.CanonicalRelativePath), entry.Length);
						if (!verified.Succeeded)
							return RetailOperationResult<RetailCandidatePlan>.Failed(verified.Failure);
						entries.Add(entry);
						if (entry.Length.HasValue)
						{
							try
							{
								observedKnownBytes = checked(observedKnownBytes + entry.Length.Value);
							}
							catch (OverflowException)
							{
								return Failed(RetailImportErrorCode.SourceLimitExceeded, RetailImportDiagnosticCode.ArithmeticOverflow);
							}
						}

						if (entries.Count != budget.AcceptedEntries || observedKnownBytes != budget.AcceptedKnownBytes)
							return Failed(RetailImportErrorCode.SourceChanged, RetailImportDiagnosticCode.LengthMismatch);
					}
				}

				return RetailCandidatePlan.Create(ImmutableArray.CreateRange(entries));
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (OperationCanceledException)
			{
				return Failed(RetailImportErrorCode.SanitizedIoFailure, RetailImportDiagnosticCode.None);
			}
			catch (Exception)
			{
				return Failed(RetailImportErrorCode.SanitizedIoFailure, RetailImportDiagnosticCode.None);
			}
		}

		static int DirectoryDepth(string canonicalRelativePath)
		{
			var depth = 0;
			foreach (var character in canonicalRelativePath)
				if (character == '/')
					depth++;
			return depth;
		}

		static RetailOperationResult<RetailCandidatePlan> Failed(RetailImportErrorCode code, RetailImportDiagnosticCode diagnostic) =>
			RetailOperationResult<RetailCandidatePlan>.Failed(new RetailImportFailure(code, null, null, diagnostic));
	}
}
