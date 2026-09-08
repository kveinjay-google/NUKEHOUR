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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("OpenRA.Test")]

namespace OpenRA.Mods.RA2.Content
{
	public enum RetailSourceKind
	{
		DirectFile,
		DirectoryDiscoveredFile,
	}

	public enum RetailSourceReadKind
	{
		Entry,
		Complete,
	}

	static class RetailCanonicalPath
	{
		public static string ValidateRelative(string value, string parameterName)
		{
			if (string.IsNullOrEmpty(value) || value[0] == '/' || value.IndexOf('\\') >= 0 || value.IndexOf('\0') >= 0)
				throw new ArgumentException("The path must be a canonical relative path.", parameterName);

			var components = value.Split('/');
			foreach (var component in components)
				if (component.Length == 0 || component == "." || component == "..")
					throw new ArgumentException("The path must be a canonical relative path.", parameterName);

			return value;
		}

		public static string Basename(string path)
		{
			var separator = path.LastIndexOf('/');
			return separator < 0 ? path : path.Substring(separator + 1);
		}
	}

	public readonly struct RetailSourceId : IEquatable<RetailSourceId>, IComparable<RetailSourceId>
	{
		public int RootIndex { get; }
		public string CanonicalRelativePath { get; }

		public RetailSourceId(int rootIndex, string canonicalRelativePath)
		{
			if (rootIndex < 0)
				throw new ArgumentOutOfRangeException(nameof(rootIndex));

			RootIndex = rootIndex;
			CanonicalRelativePath = RetailCanonicalPath.ValidateRelative(canonicalRelativePath, nameof(canonicalRelativePath));
		}

		public int CompareTo(RetailSourceId other)
		{
			var rootComparison = RootIndex.CompareTo(other.RootIndex);
			return rootComparison != 0 ? rootComparison : StringComparer.Ordinal.Compare(CanonicalRelativePath, other.CanonicalRelativePath);
		}

		public bool Equals(RetailSourceId other) => RootIndex == other.RootIndex &&
			StringComparer.Ordinal.Equals(CanonicalRelativePath, other.CanonicalRelativePath);
		public override bool Equals(object obj) => obj is RetailSourceId other && Equals(other);
		public override int GetHashCode() => HashCode.Combine(RootIndex, StringComparer.Ordinal.GetHashCode(CanonicalRelativePath ?? string.Empty));
		public static bool operator ==(RetailSourceId left, RetailSourceId right) => left.Equals(right);
		public static bool operator !=(RetailSourceId left, RetailSourceId right) => !left.Equals(right);
	}

	public readonly struct RetailStagingTarget : IEquatable<RetailStagingTarget>
	{
		public string CanonicalRelativePath { get; }

		public RetailStagingTarget(string canonicalRelativePath)
		{
			CanonicalRelativePath = RetailCanonicalPath.ValidateRelative(canonicalRelativePath, nameof(canonicalRelativePath));
		}

		public bool Equals(RetailStagingTarget other) => StringComparer.Ordinal.Equals(CanonicalRelativePath, other.CanonicalRelativePath);
		public override bool Equals(object obj) => obj is RetailStagingTarget other && Equals(other);
		public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(CanonicalRelativePath ?? string.Empty);
		public static bool operator ==(RetailStagingTarget left, RetailStagingTarget right) => left.Equals(right);
		public static bool operator !=(RetailStagingTarget left, RetailStagingTarget right) => !left.Equals(right);
	}

	public sealed record RetailSourceEntry
	{
		public RetailSourceId Id { get; }
		public string Basename { get; }
		public RetailSourceKind Kind { get; }
		public long? Length { get; }
		public bool IsSymbolicLink { get; }

		public RetailSourceEntry(RetailSourceId id, string basename, RetailSourceKind kind, long? length, bool isSymbolicLink)
		{
			if (id.CanonicalRelativePath == null)
				throw new ArgumentException("A default source ID is not valid.", nameof(id));
			if (!Enum.IsDefined(typeof(RetailSourceKind), kind))
				throw new ArgumentOutOfRangeException(nameof(kind));
			RetailSafeBasename.Validate(basename, nameof(basename));
			if (!StringComparer.Ordinal.Equals(RetailCanonicalPath.Basename(id.CanonicalRelativePath), basename))
				throw new ArgumentException("The basename must exactly match the stable relative path.", nameof(basename));
			if (length < 0)
				throw new ArgumentOutOfRangeException(nameof(length));

			Id = id;
			Basename = basename;
			Kind = kind;
			Length = length;
			IsSymbolicLink = isSymbolicLink;
		}
	}

	public sealed record RetailSourceRead
	{
		readonly RetailSourceEntry entry;
		public RetailSourceReadKind Kind { get; }
		public RetailSourceEntry Entry => Kind == RetailSourceReadKind.Entry ? entry :
			throw new InvalidOperationException("A completed read has no entry.");

		RetailSourceRead(RetailSourceReadKind kind, RetailSourceEntry entry)
		{
			Kind = kind;
			this.entry = entry;
		}

		public static RetailSourceRead Item(RetailSourceEntry entry) =>
			new(RetailSourceReadKind.Entry, entry ?? throw new ArgumentNullException(nameof(entry)));
		public static RetailSourceRead Complete() => new(RetailSourceReadKind.Complete, null);
	}

	public interface IRetailSourceEnumeration : IAsyncDisposable
	{
		ValueTask<RetailOperationResult<RetailSourceRead>> ReadNextAsync(CancellationToken cancellationToken);
	}

	public interface IRetailImportSourceSession : IAsyncDisposable
	{
		Task<RetailOperationResult<IRetailSourceEnumeration>> OpenEnumerationAsync(RetailDiscoveryBudget budget, CancellationToken cancellationToken);
		Task<RetailOperationResult<StagedRetailSource>> CopyToAsync(RetailSourceId id, RetailStagingTarget target,
			IProgress<RetailImportProgress> progress, CancellationToken cancellationToken);
	}

	public interface IRetailMonotonicClock
	{
		long GetTimestamp();
		TimeSpan GetElapsedTime(long start, long end);
	}

	public interface IRetailPersistentTimeProvider
	{
		DateTimeOffset GetUtcNow();
	}

	public readonly struct RetailValidationDeadline : IEquatable<RetailValidationDeadline>
	{
		readonly IRetailMonotonicClock owningClock;
		public long StartTimestamp { get; }
		public TimeSpan Budget { get; }

		RetailValidationDeadline(
			IRetailMonotonicClock owningClock,
			long startTimestamp,
			TimeSpan budget)
		{
			if (owningClock == null)
				throw new ArgumentNullException(nameof(owningClock));
			if (startTimestamp < 0)
				throw new ArgumentOutOfRangeException(nameof(startTimestamp));
			if (budget <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException(nameof(budget));

			this.owningClock = owningClock;
			StartTimestamp = startTimestamp;
			Budget = budget;
		}

		public static RetailValidationDeadline Start(IRetailMonotonicClock clock, TimeSpan budget)
		{
			if (clock == null)
				throw new ArgumentNullException(nameof(clock));

			return new RetailValidationDeadline(clock, clock.GetTimestamp(), budget);
		}

		public bool MatchesClock(IRetailMonotonicClock clock) =>
			owningClock != null && ReferenceEquals(owningClock, clock);

		public TimeSpan Remaining(IRetailMonotonicClock clock)
		{
			if (clock == null)
				throw new ArgumentNullException(nameof(clock));
			if (!MatchesClock(clock))
				throw new ArgumentException("The validation deadline belongs to a different clock.", nameof(clock));

			var elapsed = clock.GetElapsedTime(StartTimestamp, clock.GetTimestamp());
			if (elapsed < TimeSpan.Zero)
				throw new InvalidOperationException("A monotonic clock cannot move backwards.");

			if (elapsed >= Budget)
				return TimeSpan.Zero;

			return Budget - elapsed;
		}

		public void ThrowIfExpired(IRetailMonotonicClock clock)
		{
			if (Remaining(clock) == TimeSpan.Zero)
				throw new TimeoutException("The validation deadline expired.");
		}

		public bool Equals(RetailValidationDeadline other) =>
			ReferenceEquals(owningClock, other.owningClock) &&
			StartTimestamp == other.StartTimestamp && Budget == other.Budget;
		public override bool Equals(object obj) => obj is RetailValidationDeadline other && Equals(other);
		public override int GetHashCode() => HashCode.Combine(
			owningClock == null ? 0 : RuntimeHelpers.GetHashCode(owningClock),
			StartTimestamp,
			Budget);
		public static bool operator ==(RetailValidationDeadline left, RetailValidationDeadline right) => left.Equals(right);
		public static bool operator !=(RetailValidationDeadline left, RetailValidationDeadline right) => !left.Equals(right);
	}

	public sealed class RetailDiscoveryBudget
	{
		readonly int maximumDepth;
		readonly int maximumEntries;
		readonly long maximumKnownBytes;
		readonly IRetailMonotonicClock clock;
		readonly RetailValidationDeadline deadline;
		RetailImportFailure terminalFailure;
		int verifiedEntries;
		bool hasPendingAcceptance;
		int pendingDepth;
		long? pendingApparentBytes;

		public int AcceptedEntries { get; private set; }
		public long AcceptedKnownBytes { get; private set; }

		internal RetailDiscoveryBudget(int maximumDepth, int maximumEntries, long maximumKnownBytes,
			IRetailMonotonicClock clock, RetailValidationDeadline deadline)
		{
			if (maximumDepth < 0)
				throw new ArgumentOutOfRangeException(nameof(maximumDepth));
			if (maximumEntries < 0)
				throw new ArgumentOutOfRangeException(nameof(maximumEntries));
			if (maximumKnownBytes < 0)
				throw new ArgumentOutOfRangeException(nameof(maximumKnownBytes));

			this.maximumDepth = maximumDepth;
			this.maximumEntries = maximumEntries;
			this.maximumKnownBytes = maximumKnownBytes;
			this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
			this.deadline = deadline;
			if (!deadline.MatchesClock(this.clock))
				terminalFailure = new RetailImportFailure(
					RetailImportErrorCode.ProfileContextFailure,
					null,
					null,
					RetailImportDiagnosticCode.ProfileMetadataMismatch);
		}

		public RetailOperationResult Checkpoint()
		{
			if (terminalFailure != null)
				return RetailOperationResult.Failed(terminalFailure);

			try
			{
				deadline.ThrowIfExpired(clock);
				return RetailOperationResult.Success();
			}
			catch (TimeoutException)
			{
				return Fail(RetailImportErrorCode.ValidationTimedOut, RetailImportDiagnosticCode.DeadlineExceeded);
			}
			catch (OverflowException)
			{
				return Fail(RetailImportErrorCode.SourceLimitExceeded, RetailImportDiagnosticCode.ArithmeticOverflow);
			}
		}

		public RetailOperationResult TryEnterDirectory(int depth)
		{
			if (terminalFailure != null)
				return RetailOperationResult.Failed(terminalFailure);
			if (depth < 0)
				throw new ArgumentOutOfRangeException(nameof(depth));

			var checkpoint = Checkpoint();
			if (!checkpoint.Succeeded)
				return checkpoint;
			if (depth > maximumDepth)
				return Fail(RetailImportErrorCode.SourceLimitExceeded, RetailImportDiagnosticCode.None);

			return RetailOperationResult.Success();
		}

		public RetailOperationResult TryAcceptEntry(int depth, long? apparentBytes)
		{
			if (terminalFailure != null)
				return RetailOperationResult.Failed(terminalFailure);

			var checkpoint = Checkpoint();
			if (!checkpoint.Succeeded)
				return checkpoint;
			if (hasPendingAcceptance)
				return Fail(RetailImportErrorCode.SourceChanged, RetailImportDiagnosticCode.LengthMismatch);
			if (depth < 0 || depth > maximumDepth || apparentBytes < 0)
				return Fail(RetailImportErrorCode.SourceLimitExceeded, RetailImportDiagnosticCode.None);
			if (AcceptedEntries >= maximumEntries)
				return Fail(RetailImportErrorCode.SourceLimitExceeded, RetailImportDiagnosticCode.None);

			long nextBytes;
			try
			{
				nextBytes = apparentBytes.HasValue ? checked(AcceptedKnownBytes + apparentBytes.Value) : AcceptedKnownBytes;
			}
			catch (OverflowException)
			{
				return Fail(RetailImportErrorCode.SourceLimitExceeded, RetailImportDiagnosticCode.ArithmeticOverflow);
			}

			if (nextBytes > maximumKnownBytes)
				return Fail(RetailImportErrorCode.SourceLimitExceeded, RetailImportDiagnosticCode.None);

			AcceptedEntries++;
			AcceptedKnownBytes = nextBytes;
			hasPendingAcceptance = true;
			pendingDepth = depth;
			pendingApparentBytes = apparentBytes;
			return RetailOperationResult.Success();
		}

		internal RetailOperationResult VerifyAcceptedEntry(int depth, long? apparentBytes)
		{
			if (terminalFailure != null)
				return RetailOperationResult.Failed(terminalFailure);

			var checkpoint = Checkpoint();
			if (!checkpoint.Succeeded)
				return checkpoint;
			if (!hasPendingAcceptance || AcceptedEntries != verifiedEntries + 1 ||
				depth != pendingDepth || apparentBytes != pendingApparentBytes)
				return Fail(RetailImportErrorCode.SourceChanged, RetailImportDiagnosticCode.LengthMismatch);

			verifiedEntries++;
			hasPendingAcceptance = false;
			pendingApparentBytes = null;
			return RetailOperationResult.Success();
		}

		internal RetailOperationResult VerifyComplete()
		{
			if (terminalFailure != null)
				return RetailOperationResult.Failed(terminalFailure);

			var checkpoint = Checkpoint();
			if (!checkpoint.Succeeded)
				return checkpoint;
			if (hasPendingAcceptance || AcceptedEntries != verifiedEntries)
				return Fail(RetailImportErrorCode.SourceChanged, RetailImportDiagnosticCode.LengthMismatch);

			return RetailOperationResult.Success();
		}

		RetailOperationResult Fail(RetailImportErrorCode code, RetailImportDiagnosticCode diagnostic)
		{
			terminalFailure ??= new RetailImportFailure(code, null, null, diagnostic);
			return RetailOperationResult.Failed(terminalFailure);
		}
	}
}
