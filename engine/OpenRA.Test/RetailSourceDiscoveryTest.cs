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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailSourceDiscoveryTest
	{
		static int DirectoryDepth(RetailSourceEntry entry) =>
			entry.Id.CanonicalRelativePath.Count(character => character == '/');

		static RetailSourceEntry Entry(int root, string path, long? length = 1, RetailSourceKind kind = RetailSourceKind.DirectoryDiscoveredFile)
		{
			return new RetailSourceEntry(new RetailSourceId(root, path), path.Split('/').Last(), kind, length, false);
		}

		static RetailOperationResult Accept(RetailDiscoveryBudget budget, int depth, long? apparentBytes)
		{
			var method = typeof(RetailDiscoveryBudget).GetMethod(nameof(RetailDiscoveryBudget.TryAcceptEntry),
				BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int), typeof(long?) }, null);
			Assert.That(method, Is.Not.Null, "TryAcceptEntry must expose the exact (int, long?) API.");
			return (RetailOperationResult)method.Invoke(budget, new object[] { depth, apparentBytes });
		}

		static RetailOperationResult Verify(RetailDiscoveryBudget budget, int depth, long? apparentBytes)
		{
			var method = typeof(RetailDiscoveryBudget).GetMethod("VerifyAcceptedEntry", BindingFlags.Instance | BindingFlags.NonPublic,
				null, new[] { typeof(int), typeof(long?) }, null);
			Assert.That(method, Is.Not.Null, "Neutral discovery needs an internal exact acceptance verifier.");
			return (RetailOperationResult)method.Invoke(budget, new object[] { depth, apparentBytes });
		}

		static RetailValidationDeadline Deadline(FakeClock clock, TimeSpan? budget = null) =>
			RetailValidationDeadline.Start(clock, budget ?? IosImportEnvelopePolicyV1.DiscoveryTimeout);

		[Test]
		public async Task DiscoveryIsPullBasedAndReturnsOrdinalOrderIndependentOfProviderOrder()
		{
			var clock = new FakeClock();
			var entries = new[] { Entry(1, "Z/map02.map"), Entry(0, "b/RA2.MIX"), Entry(0, "a/language.mix") };
			var session = FakeSession.FromEntries(entries);
			var result = await RetailSourceDiscovery.DiscoverAsync(session, clock, Deadline(clock), CancellationToken.None);

			Assert.That(result.Succeeded, Is.True);
			Assert.That(result.Value.Candidates.Select(c => (c.SourceId.RootIndex, c.SourceId.CanonicalRelativePath)),
				Is.EqualTo(new[] { (0, "a/language.mix"), (0, "b/RA2.MIX"), (1, "Z/map02.map") }));
			Assert.That(session.OpenCalls, Is.EqualTo(1));
			Assert.That(session.Enumeration.ReadCalls, Is.EqualTo(4));
			Assert.That(session.Enumeration.Disposed, Is.True);
			Assert.That(session.ReceivedBudget.AcceptedEntries, Is.EqualTo(3));
		}

		[Test]
		public async Task DiscoveryNeverTreatsSelectedZipMediaAsAnImportableRetailEnvelope()
		{
			var clock = new FakeClock();
			var session = FakeSession.FromEntries(new[]
			{
				Entry(0, "retail-install.zip", kind: RetailSourceKind.DirectFile),
			});
			var discovered = await RetailSourceDiscovery.DiscoverAsync(
				session, clock, Deadline(clock), CancellationToken.None);

			Assert.That(discovered.Succeeded, Is.False);
			Assert.That(discovered.Failure.Code, Is.EqualTo(RetailImportErrorCode.ExtractInstallationFirst));
			Assert.That(session.Enumeration.ProviderFetches, Is.EqualTo(1));
		}

		[Test]
		public async Task ExpiredDeadlineFailsBeforeOpeningProvider()
		{
			var clock = new FakeClock();
			var deadline = Deadline(clock, TimeSpan.FromSeconds(1));
			clock.Timestamp = 1;
			var session = FakeSession.FromEntries(new[] { Entry(0, "ra2.mix") });
			var result = await RetailSourceDiscovery.DiscoverAsync(session, clock, deadline, CancellationToken.None);

			Assert.That(result.Failure.Code, Is.EqualTo(RetailImportErrorCode.ValidationTimedOut));
			Assert.That(result.Failure.Diagnostic, Is.EqualTo(RetailImportDiagnosticCode.DeadlineExceeded));
			Assert.That(session.OpenCalls, Is.Zero);
		}

		[Test]
		public async Task ProviderFailureRemainsClosedAndDisposesCursor()
		{
			var failure = new RetailImportFailure(RetailImportErrorCode.ProviderMaterializationRequired, null,
				"ra2.mix", RetailImportDiagnosticCode.ProviderNeedsDownload);
			var session = FakeSession.FromEntries(new[] { Entry(0, "ra2.mix") });
			session.FailureAtRead = 0;
			session.ReadFailure = failure;
			var clock = new FakeClock();
			var result = await RetailSourceDiscovery.DiscoverAsync(session, clock, Deadline(clock), CancellationToken.None);

			Assert.That(result.Succeeded, Is.False);
			Assert.That(result.Failure, Is.SameAs(failure));
			Assert.That(session.Enumeration.Disposed, Is.True);
		}

		[Test]
		public async Task NoncooperativeProviderCancellationIsSanitizedAndCursorIsDisposed()
		{
			var clock = new FakeClock();
			var cursor = FakeSession.FromEntries(new[] { Entry(0, "ra2.mix") });
			cursor.ThrowCancellationAtRead = 1;
			var result = await RetailSourceDiscovery.DiscoverAsync(cursor, clock, Deadline(clock), CancellationToken.None);

			Assert.That(result.Succeeded, Is.False);
			Assert.That(result.Failure.Code, Is.EqualTo(RetailImportErrorCode.SanitizedIoFailure));
			Assert.That(cursor.Enumeration.Disposed, Is.True);

			var opening = FakeSession.FromEntries(new[] { Entry(0, "ra2.mix") });
			opening.ThrowCancellationOnOpen = true;
			var openResult = await RetailSourceDiscovery.DiscoverAsync(opening, clock, Deadline(clock), CancellationToken.None);
			Assert.That(openResult.Failure.Code, Is.EqualTo(RetailImportErrorCode.SanitizedIoFailure));
		}

		[Test]
		public void CooperativeCancellationPropagatesBeforeOpenAndMidstreamWithDisposal()
		{
			var clock = new FakeClock();
			using var before = new CancellationTokenSource();
			before.Cancel();
			var unopened = FakeSession.FromEntries(new[] { Entry(0, "ra2.mix") });
			Assert.ThrowsAsync<OperationCanceledException>(async () => await RetailSourceDiscovery.DiscoverAsync(
				unopened, clock, Deadline(clock), before.Token));
			Assert.That(unopened.OpenCalls, Is.Zero);

			using var middle = new CancellationTokenSource();
			var opened = FakeSession.PotentiallyMillions(10_000_000);
			opened.CancelAfterFetch = 1;
			opened.CancellationSource = middle;
			Assert.ThrowsAsync<OperationCanceledException>(async () => await RetailSourceDiscovery.DiscoverAsync(
				opened, clock, Deadline(clock), middle.Token));
			Assert.That(opened.Enumeration.ProviderFetches, Is.EqualTo(1));
			Assert.That(opened.Enumeration.Disposed, Is.True);
		}

		[Test]
		public async Task MidstreamTimeoutStopsBeforeAnotherProviderFetchAndDisposes()
		{
			var clock = new FakeClock();
			var session = FakeSession.PotentiallyMillions(10_000_000);
			session.Clock = clock;
			session.AdvanceClockAfterFetch = 1;
			var result = await RetailSourceDiscovery.DiscoverAsync(session, clock,
				Deadline(clock, TimeSpan.FromSeconds(1)), CancellationToken.None);

			Assert.That(result.Failure.Code, Is.EqualTo(RetailImportErrorCode.ValidationTimedOut));
			Assert.That(session.Enumeration.ProviderFetches, Is.EqualTo(1));
			Assert.That(session.Enumeration.Disposed, Is.True);
		}

		[Test]
		public async Task PerEntryAccountingMismatchFailsImmediatelyBeforeCompensation()
		{
			var clock = new FakeClock();
			var session = FakeSession.FromEntries(new[] { Entry(0, "one.map", 1), Entry(0, "two.map", 1) });
			session.ApparentLength = index => index == 0 ? 2 : 0;
			var result = await RetailSourceDiscovery.DiscoverAsync(session, clock, Deadline(clock), CancellationToken.None);

			Assert.That(result.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceChanged));
			Assert.That(result.Failure.Diagnostic, Is.EqualTo(RetailImportDiagnosticCode.LengthMismatch));
			Assert.That(session.Enumeration.ReadCalls, Is.EqualTo(1));
			Assert.That(session.Enumeration.ProviderFetches, Is.EqualTo(1));
			Assert.That(session.Enumeration.Disposed, Is.True);
		}

		[Test]
		public async Task CompleteAccountingMismatchFailsClosed()
		{
			var clock = new FakeClock();
			var session = FakeSession.FromEntries(new[] { Entry(0, "one.map", 1) });
			session.AcceptExtraBeforeComplete = true;
			var result = await RetailSourceDiscovery.DiscoverAsync(session, clock, Deadline(clock), CancellationToken.None);

			Assert.That(result.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceChanged));
			Assert.That(result.Failure.Diagnostic, Is.EqualTo(RetailImportDiagnosticCode.LengthMismatch));
			Assert.That(session.Enumeration.Disposed, Is.True);
		}

		[Test]
		public void SourceReadEnforcesExactlyOneState()
		{
			var entry = Entry(0, "ra2.mix");
			var item = RetailSourceRead.Item(entry);
			var complete = RetailSourceRead.Complete();
			Assert.That(item.Kind, Is.EqualTo(RetailSourceReadKind.Entry));
			Assert.That(item.Entry, Is.SameAs(entry));
			Assert.That(complete.Kind, Is.EqualTo(RetailSourceReadKind.Complete));
			Assert.Throws<InvalidOperationException>(() => _ = complete.Entry);
			Assert.Throws<ArgumentNullException>(() => RetailSourceRead.Item(null));
		}

		[Test]
		public void SourceReadIsARecordValueWhileFactoriesKeepStatesClosed()
		{
			Assert.That(typeof(RetailSourceRead).GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public),
				Is.Not.Null);
			var left = RetailSourceRead.Item(Entry(0, "ra2.mix", 1));
			var right = RetailSourceRead.Item(Entry(0, "ra2.mix", 1));
			Assert.That(right, Is.EqualTo(left));
			Assert.That(right.GetHashCode(), Is.EqualTo(left.GetHashCode()));
			Assert.That(new HashSet<RetailSourceRead> { left }.Contains(right), Is.True);
			Assert.That(RetailSourceRead.Complete(), Is.EqualTo(RetailSourceRead.Complete()));
			Assert.That(RetailSourceRead.Item(Entry(0, "ra2.mix", 2)), Is.Not.EqualTo(left));
			Assert.That(RetailSourceRead.Complete(), Is.Not.EqualTo(left));
		}

		[Test]
		public async Task AcceptanceReceiptRejectsDepthMismatchAtFirstYieldAndBecomesInert()
		{
			var clock = new FakeClock();
			var path = string.Join("/", Enumerable.Repeat("d", IosImportEnvelopePolicyV1.DiscoveryDepth + 1)) + "/map.map";
			var session = FakeSession.FromEntries(new[] { Entry(0, path, 1) });
			session.Depth = _ => 0;
			var result = await RetailSourceDiscovery.DiscoverAsync(session, clock, Deadline(clock), CancellationToken.None);

			Assert.That(result.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceChanged));
			Assert.That(result.Failure.Diagnostic, Is.EqualTo(RetailImportDiagnosticCode.LengthMismatch));
			Assert.That(session.Enumeration.ProviderFetches, Is.EqualTo(1));
			Assert.That(session.Enumeration.ReadCalls, Is.EqualTo(1));
			Assert.That(session.Enumeration.Disposed, Is.True);
			var repeated = Accept(session.ReceivedBudget, 0, 1);
			Assert.That(repeated.Failure, Is.SameAs(result.Failure));
		}

		[TestCase(null, 0)]
		[TestCase(0, null)]
		public async Task AcceptanceReceiptDistinguishesUnknownAndZeroLength(long? apparent, long? returned)
		{
			var clock = new FakeClock();
			var session = FakeSession.FromEntries(new[] { Entry(0, "map.map", returned) });
			session.ApparentLength = _ => apparent;
			var result = await RetailSourceDiscovery.DiscoverAsync(session, clock, Deadline(clock), CancellationToken.None);

			Assert.That(result.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceChanged));
			Assert.That(session.Enumeration.ProviderFetches, Is.EqualTo(1));
			Assert.That(session.Enumeration.ReadCalls, Is.EqualTo(1));
			Assert.That(session.Enumeration.Disposed, Is.True);
		}

		[Test]
		public async Task AcceptanceReceiptRejectsMissingAndMultiplePreacceptsWithoutExtraFetch()
		{
			var clock = new FakeClock();
			var missing = FakeSession.FromEntries(new[] { Entry(0, "map.map", 1) });
			missing.SkipAcceptanceAtIndex = 0;
			var missingResult = await RetailSourceDiscovery.DiscoverAsync(missing, clock, Deadline(clock), CancellationToken.None);
			Assert.That(missingResult.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceChanged));
			Assert.That(missing.Enumeration.ProviderFetches, Is.EqualTo(1));
			Assert.That(missing.Enumeration.Disposed, Is.True);

			var multiple = FakeSession.FromEntries(new[] { Entry(0, "map.map", 1) });
			multiple.AcceptTwiceAtIndex = 0;
			var multipleResult = await RetailSourceDiscovery.DiscoverAsync(multiple, clock, Deadline(clock), CancellationToken.None);
			Assert.That(multipleResult.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceChanged));
			Assert.That(multiple.Enumeration.ProviderFetches, Is.Zero);
			Assert.That(multiple.Enumeration.Disposed, Is.True);
		}

		[Test]
		public async Task AcceptanceReceiptAcceptsExactMaximumCanonicalDirectoryDepth()
		{
			var clock = new FakeClock();
			var path = string.Join("/", Enumerable.Repeat("d", IosImportEnvelopePolicyV1.DiscoveryDepth)) + "/map.map";
			var session = FakeSession.FromEntries(new[] { Entry(0, path, 1) });
			var result = await RetailSourceDiscovery.DiscoverAsync(session, clock, Deadline(clock), CancellationToken.None);

			Assert.That(result.Succeeded, Is.True);
			Assert.That(session.Enumeration.ProviderFetches, Is.EqualTo(1));
			Assert.That(session.Enumeration.Disposed, Is.True);
		}

		[Test]
		public void BudgetHasExactAtomicApiAndDepthBoundaries()
		{
			var signature = typeof(RetailDiscoveryBudget).GetMethod(nameof(RetailDiscoveryBudget.TryAcceptEntry),
				BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int), typeof(long?) }, null);
			Assert.That(signature, Is.Not.Null);
			var clock = new FakeClock();
			foreach (var depth in new[] { IosImportEnvelopePolicyV1.DiscoveryDepth - 1, IosImportEnvelopePolicyV1.DiscoveryDepth })
			{
				var accepted = new RetailDiscoveryBudget(IosImportEnvelopePolicyV1.DiscoveryDepth, 1, 1, clock, Deadline(clock));
				Assert.That(Accept(accepted, depth, 1).Succeeded, Is.True);
			}

			var exceeded = new RetailDiscoveryBudget(IosImportEnvelopePolicyV1.DiscoveryDepth, 1, 1, clock, Deadline(clock));
			var failure = Accept(exceeded, IosImportEnvelopePolicyV1.DiscoveryDepth + 1, 0);
			Assert.That(failure.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceLimitExceeded));
			Assert.That(exceeded.AcceptedEntries, Is.Zero);

			var negative = new RetailDiscoveryBudget(1, 1, 1, clock, Deadline(clock));
			var negativeFailure = Accept(negative, -1, 0);
			Assert.That(negativeFailure.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceLimitExceeded));
			Assert.That(Accept(negative, 0, -1).Failure, Is.SameAs(negativeFailure.Failure));
		}

		[Test]
		public void BudgetEntryByteOverflowAndInertBoundariesAreAtomic()
		{
			var clock = new FakeClock();
			var count = new RetailDiscoveryBudget(1, 2, 10, clock, Deadline(clock));
			Assert.That(Accept(count, 0, 4).Succeeded, Is.True);
			Assert.That(Verify(count, 0, 4).Succeeded, Is.True);
			Assert.That(Accept(count, 1, 6).Succeeded, Is.True);
			Assert.That(Verify(count, 1, 6).Succeeded, Is.True);
			var countFailure = Accept(count, 0, 0);
			Assert.That(countFailure.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceLimitExceeded));
			Assert.That(count.AcceptedEntries, Is.EqualTo(2));
			Assert.That(count.AcceptedKnownBytes, Is.EqualTo(10));
			Assert.That(Accept(count, -1, -1).Failure, Is.SameAs(countFailure.Failure));

			foreach (var bytes in new[] { IosImportEnvelopePolicyV1.DiscoveryBytes - 1, IosImportEnvelopePolicyV1.DiscoveryBytes })
			{
				var accepted = new RetailDiscoveryBudget(1, 1, IosImportEnvelopePolicyV1.DiscoveryBytes, clock, Deadline(clock));
				Assert.That(Accept(accepted, 0, bytes).Succeeded, Is.True);
			}

			var bytesExceeded = new RetailDiscoveryBudget(1, 1, IosImportEnvelopePolicyV1.DiscoveryBytes, clock, Deadline(clock));
			Assert.That(Accept(bytesExceeded, 0, IosImportEnvelopePolicyV1.DiscoveryBytes + 1).Failure.Code,
				Is.EqualTo(RetailImportErrorCode.SourceLimitExceeded));

			var overflow = new RetailDiscoveryBudget(1, 2, long.MaxValue, clock, Deadline(clock));
			Assert.That(Accept(overflow, 0, long.MaxValue).Succeeded, Is.True);
			Assert.That(Verify(overflow, 0, long.MaxValue).Succeeded, Is.True);
			var overflowFailure = Accept(overflow, 0, 1);
			Assert.That(overflowFailure.Failure.Diagnostic, Is.EqualTo(RetailImportDiagnosticCode.ArithmeticOverflow));
			Assert.That(Accept(overflow, int.MinValue, long.MinValue).Failure, Is.SameAs(overflowFailure.Failure));
		}

		[Test]
		public void DeadlineFailureIsRepeatedByIdentity()
		{
			var clock = new FakeClock();
			var budget = new RetailDiscoveryBudget(1, 1, 1, clock, Deadline(clock, TimeSpan.FromSeconds(1)));
			clock.Timestamp = 1;
			var first = budget.Checkpoint();
			var second = Accept(budget, -1, -1);
			Assert.That(second.Failure, Is.SameAs(first.Failure));
		}

		[Test]
		public async Task PotentialMillionsProviderNeverFetchesEntryLimitPlusOne()
		{
			var clock = new FakeClock();
			var session = FakeSession.PotentiallyMillions(10_000_000);
			var result = await RetailSourceDiscovery.DiscoverAsync(session, clock, Deadline(clock), CancellationToken.None);

			Assert.That(result.Failure.Code, Is.EqualTo(RetailImportErrorCode.SourceLimitExceeded));
			Assert.That(session.Enumeration.ReadCalls, Is.EqualTo(IosImportEnvelopePolicyV1.DiscoveryEntries + 1));
			Assert.That(session.Enumeration.ProviderFetches, Is.EqualTo(IosImportEnvelopePolicyV1.DiscoveryEntries));
			Assert.That(session.GeneratedEntries, Is.EqualTo(IosImportEnvelopePolicyV1.DiscoveryEntries));
			Assert.That(session.Enumeration.Disposed, Is.True);
		}

		sealed class FakeClock : IRetailMonotonicClock
		{
			public long Timestamp { get; set; }
			public long GetTimestamp() => Timestamp;
			public TimeSpan GetElapsedTime(long start, long end) => TimeSpan.FromSeconds(checked(end - start));
		}

		sealed class FakeSession : IRetailImportSourceSession
		{
			readonly Func<int, RetailSourceEntry> entryFactory;
			readonly int potentialCount;
			public int PotentialCount => potentialCount;
			public int OpenCalls { get; private set; }
			public int GeneratedEntries { get; private set; }
			public int FailureAtRead { get; set; } = -1;
			public int ThrowCancellationAtRead { get; set; } = -1;
			public bool ThrowCancellationOnOpen { get; set; }
			public bool AcceptExtraBeforeComplete { get; set; }
			public int SkipAcceptanceAtIndex { get; set; } = -1;
			public int AcceptTwiceAtIndex { get; set; } = -1;
			public int CancelAfterFetch { get; set; } = -1;
			public int AdvanceClockAfterFetch { get; set; } = -1;
			public CancellationTokenSource CancellationSource { get; set; }
			public FakeClock Clock { get; set; }
			public Func<int, long?> ApparentLength { get; set; }
			public Func<int, int> Depth { get; set; }
			public RetailImportFailure ReadFailure { get; set; }
			public FakeEnumeration Enumeration { get; private set; }
			public RetailDiscoveryBudget ReceivedBudget { get; private set; }

			FakeSession(Func<int, RetailSourceEntry> entryFactory, int potentialCount)
			{
				this.entryFactory = entryFactory;
				this.potentialCount = potentialCount;
			}

			public static FakeSession FromEntries(IReadOnlyList<RetailSourceEntry> entries) =>
				new(index => entries[index], entries.Count)
				{
					ApparentLength = index => entries[index].Length,
					Depth = index => DirectoryDepth(entries[index]),
				};

			public static FakeSession PotentiallyMillions(int potentialCount) =>
				new(index => Entry(0, "ignored/" + index + ".txt", 0), potentialCount)
				{
					ApparentLength = _ => 0,
					Depth = _ => 1,
				};

			public Task<RetailOperationResult<IRetailSourceEnumeration>> OpenEnumerationAsync(RetailDiscoveryBudget budget,
				CancellationToken cancellationToken)
			{
				OpenCalls++;
				if (ThrowCancellationOnOpen)
					throw new OperationCanceledException();

				ReceivedBudget = budget;
				Enumeration = new FakeEnumeration(budget, this);
				return Task.FromResult(RetailOperationResult<IRetailSourceEnumeration>.Success(Enumeration));
			}

			public Task<RetailOperationResult<StagedRetailSource>> CopyToAsync(RetailSourceId id, RetailStagingTarget target,
				IProgress<RetailImportProgress> progress, CancellationToken cancellationToken) =>
				throw new AssertionException("Discovery must not copy.");

			public ValueTask DisposeAsync() => default;

			public RetailSourceEntry Generate(int index)
			{
				GeneratedEntries++;
				return entryFactory(index);
			}
		}

		sealed class FakeEnumeration : IRetailSourceEnumeration
		{
			readonly RetailDiscoveryBudget budget;
			readonly FakeSession owner;
			int index;
			bool acceptedExtra;
			public int ReadCalls { get; private set; }
			public int ProviderFetches { get; private set; }
			public bool Disposed { get; private set; }

			public FakeEnumeration(RetailDiscoveryBudget budget, FakeSession owner)
			{
				this.budget = budget;
				this.owner = owner;
			}

			public ValueTask<RetailOperationResult<RetailSourceRead>> ReadNextAsync(CancellationToken cancellationToken)
			{
				ReadCalls++;
				if (ReadCalls == owner.ThrowCancellationAtRead)
					throw new OperationCanceledException();
				if (ReadCalls - 1 == owner.FailureAtRead)
					return new ValueTask<RetailOperationResult<RetailSourceRead>>(
						RetailOperationResult<RetailSourceRead>.Failed(owner.ReadFailure));
				if (index == owner.PotentialCount)
				{
					if (owner.AcceptExtraBeforeComplete && !acceptedExtra)
					{
						var extra = Accept(budget, 0, 0);
						if (!extra.Succeeded)
							return new ValueTask<RetailOperationResult<RetailSourceRead>>(
								RetailOperationResult<RetailSourceRead>.Failed(extra.Failure));
						acceptedExtra = true;
					}

					return new ValueTask<RetailOperationResult<RetailSourceRead>>(
						RetailOperationResult<RetailSourceRead>.Success(RetailSourceRead.Complete()));
				}

				var apparent = owner.ApparentLength(index);
				if (index != owner.SkipAcceptanceAtIndex)
				{
					var accepted = Accept(budget, owner.Depth(index), apparent);
					if (!accepted.Succeeded)
						return new ValueTask<RetailOperationResult<RetailSourceRead>>(
							RetailOperationResult<RetailSourceRead>.Failed(accepted.Failure));
					if (index == owner.AcceptTwiceAtIndex)
					{
						var duplicate = Accept(budget, owner.Depth(index), apparent);
						if (!duplicate.Succeeded)
							return new ValueTask<RetailOperationResult<RetailSourceRead>>(
								RetailOperationResult<RetailSourceRead>.Failed(duplicate.Failure));
					}
				}

				ProviderFetches++;
				var entry = owner.Generate(index++);
				if (ProviderFetches == owner.CancelAfterFetch)
					owner.CancellationSource.Cancel();
				if (ProviderFetches == owner.AdvanceClockAfterFetch)
					owner.Clock.Timestamp++;

				return new ValueTask<RetailOperationResult<RetailSourceRead>>(
					RetailOperationResult<RetailSourceRead>.Success(RetailSourceRead.Item(entry)));
			}

			public ValueTask DisposeAsync()
			{
				Disposed = true;
				return default;
			}
		}
	}
}
