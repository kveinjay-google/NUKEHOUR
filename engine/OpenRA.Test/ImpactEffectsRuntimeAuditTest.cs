// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available to you under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Mods.RA2.Traits;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class ImpactEffectsRuntimeAuditTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2"))))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
		}

		[Test]
		public void RuntimeAuditIsDisabledUnlessExplicitlyEnabled()
		{
			var config = ImpactEffectsRuntimeAuditConfiguration.Parse(_ => null);

			Assert.That(config.Enabled, Is.False);
		}

		[Test]
		public void RuntimeAuditBoundsTimeoutAndSanitizesRunId()
		{
			var values = new Dictionary<string, string>
			{
				["OPENRA_IMPACT_EFFECT_AUDIT"] = "true",
				["OPENRA_IMPACT_EFFECT_AUDIT_TIMEOUT_TICKS"] = "99999",
				["OPENRA_IMPACT_EFFECT_AUDIT_RUN_ID"] = "../../impact run?42"
			};
			var config = ImpactEffectsRuntimeAuditConfiguration.Parse(
				name => values.TryGetValue(name, out var value) ? value : null);

			Assert.Multiple(() =>
			{
				Assert.That(config.Enabled, Is.True);
				Assert.That(config.TimeoutTicks, Is.EqualTo(1200));
				Assert.That(config.RunId, Is.EqualTo("impactrun42"));
				Assert.That(config.BurstImpactCount, Is.EqualTo(16));
				Assert.That(config.BurstIntervalTicks, Is.EqualTo(2));
			});
		}

		[Test]
		public void RuntimeCasesCoverTheExactSixteenCaseMatrix()
		{
			var cases = ImpactEffectsRuntimeAuditConfiguration.CreateForTests().BuildCases().ToArray();

			Assert.Multiple(() =>
			{
				Assert.That(cases, Has.Length.EqualTo(16));
				Assert.That(cases.Select(c => c.Id), Is.Unique);
				Assert.That(cases.Select(c => c.WeaponName).Distinct(), Is.EquivalentTo(
					new[] { "V3Weapon", "V3WeaponE", "BlimpBomb", "BlimpBombE" }));
				foreach (var weapon in cases.GroupBy(c => c.WeaponName))
				{
					Assert.That(weapon.Count(), Is.EqualTo(4), weapon.Key);
					Assert.That(weapon.Count(c => c.Terrain == ImpactEffectsRuntimeTerrain.Land), Is.EqualTo(2), weapon.Key);
					Assert.That(weapon.Count(c => c.Terrain == ImpactEffectsRuntimeTerrain.Water), Is.EqualTo(2), weapon.Key);
					Assert.That(weapon.Count(c => c.HasActor), Is.EqualTo(2), weapon.Key);
					Assert.That(weapon.Count(c => !c.HasActor), Is.EqualTo(2), weapon.Key);
				}
			});
		}

		[TestCase("V3Weapon", "nc_core_small", "large_clsn", "nc_ring_small", "nc_debris_small", "large_watersplash", "gexp14a.wav", 5, 1)]
		[TestCase("V3WeaponE", "nc_core_large", "terrorist_explosion", "nc_ring_large", "nc_debris_large", "huge_watersplash", "gexpapoa.wav", 8, 2)]
		[TestCase("BlimpBomb", "nc_core_large", "verylarge_clsn", "nc_ring_large", "nc_debris_large", "huge_watersplash", "gexp14a.wav", 6, 2)]
		[TestCase("BlimpBombE", "nc_core_tesla", "kirovtesla", "nc_ring_tesla", "nc_debris_large", "huge_watersplash", "gexp14a.wav", 8, 3)]
		public void ExpectationsMatchTheConfirmedLandAndWaterMatrix(string weaponName,
			string core, string main, string ring, string plume, string splash,
			string landSound, int shakeDuration, int shakeIntensity)
		{
			var expectation = ImpactEffectsRuntimeExpectation.ForWeapon(weaponName);
			var land = expectation.LayersFor(ImpactEffectsRuntimeTerrain.Land).ToArray();
			var water = expectation.LayersFor(ImpactEffectsRuntimeTerrain.Water).ToArray();

			Assert.Multiple(() =>
			{
				Assert.That(land.Select(l => l.Sequence), Is.EqualTo(new[] { core, main, ring, plume }));
				Assert.That(land.Select(l => l.Delay), Is.EqualTo(new[] { 0, 0, 1, 3 }));
				Assert.That(land.SelectMany(l => l.ImpactSounds), Is.EqualTo(new[] { landSound }));
				Assert.That(water.Select(l => l.Sequence), Is.EqualTo(new[] { core, main, ring, splash }));
				Assert.That(water.Select(l => l.Delay), Is.EqualTo(new[] { 0, 0, 1, 1 }));
				Assert.That(water.SelectMany(l => l.ImpactSounds), Is.EqualTo(new[] { "gexpwasa.wav" }));
				Assert.That(expectation.ShakeDuration, Is.EqualTo(shakeDuration));
				Assert.That(expectation.ShakeIntensity, Is.EqualTo(shakeIntensity));
			});
		}

		[Test]
		public void RuntimeStateRequiresImpactEvidenceScreenshotAndCleanupBeforePassing()
		{
			var state = new ImpactEffectsRuntimeAuditStateMachine(timeoutTicks: 20);

			Assert.That(state.Phase, Is.EqualTo(ImpactEffectsRuntimeAuditPhase.Prepare));
			state.ScenarioReady();
			Assert.That(state.Phase, Is.EqualTo(ImpactEffectsRuntimeAuditPhase.WaitForBaseline));
			state.BaselineReady();
			Assert.That(state.Phase, Is.EqualTo(ImpactEffectsRuntimeAuditPhase.Impact));
			state.Impacted();
			Assert.That(state.Phase, Is.EqualTo(ImpactEffectsRuntimeAuditPhase.Observe));
			state.EvidenceReady();
			Assert.That(state.Phase, Is.EqualTo(ImpactEffectsRuntimeAuditPhase.WaitForScreenshot));
			state.ScreenshotReady();
			Assert.That(state.Phase, Is.EqualTo(ImpactEffectsRuntimeAuditPhase.Cleanup));
			state.Cleaned();
			Assert.That(state.Phase, Is.EqualTo(ImpactEffectsRuntimeAuditPhase.Passed));
		}

		[Test]
		public void RuntimeStateTimesOutWithTheActiveBoundary()
		{
			var state = new ImpactEffectsRuntimeAuditStateMachine(timeoutTicks: 2);
			state.ScenarioReady();
			state.BaselineReady();
			state.Impacted();

			state.Tick();
			state.Tick();
			state.Tick();

			Assert.That(state.Phase, Is.EqualTo(ImpactEffectsRuntimeAuditPhase.Failed));
			StringAssert.Contains("Observe", state.Detail);
		}

		[Test]
		public void BurstHasAnIndependentHardDeadline()
		{
			var config = ImpactEffectsRuntimeAuditConfiguration.CreateForTests(timeoutTicks: 60);

			Assert.Multiple(() =>
			{
				Assert.That(config.IsBurstTimedOut(60), Is.False);
				Assert.That(config.IsBurstTimedOut(61), Is.True);
			});
		}

		[Test]
		public void TerminalCompletionIsCommittedOnlyAfterWritersCloseAndSummarySucceeds()
		{
			var terminal = new ImpactEffectsRuntimeTerminalState();

			Assert.That(terminal.TryBeginFinishing(), Is.True);
			Assert.That(terminal.Completed, Is.False);
			Assert.Throws<InvalidOperationException>(() => terminal.MarkTerminalSummaryCommitted());
			terminal.MarkWritersClosed();
			Assert.That(terminal.Completed, Is.False);
			terminal.MarkTerminalSummaryCommitted();

			Assert.Multiple(() =>
			{
				Assert.That(terminal.Completed, Is.True);
				Assert.That(terminal.TryBeginFinishing(), Is.False);
			});
		}

		[Test]
		public void AssetFailureClassifierBlocksOnlyMissingRetailFiles()
		{
			Assert.Multiple(() =>
			{
				Assert.That(ImpactEffectsRuntimeAssetPolicy.ClassifySequenceFailure(
					"explosion", sequenceDeclared: true, new FileNotFoundException()),
					Is.EqualTo(ImpactEffectsRuntimeAssetFailure.BlockedRetailAssets));
				Assert.That(ImpactEffectsRuntimeAssetPolicy.ClassifySequenceFailure(
					"explosion", sequenceDeclared: false, new FileNotFoundException()),
					Is.EqualTo(ImpactEffectsRuntimeAssetFailure.Failed));
				Assert.That(ImpactEffectsRuntimeAssetPolicy.ClassifySequenceFailure(
					"explosion", sequenceDeclared: true, new InvalidDataException()),
					Is.EqualTo(ImpactEffectsRuntimeAssetFailure.Failed));
				Assert.That(ImpactEffectsRuntimeAssetPolicy.ClassifySequenceFailure(
					"nc-impact", sequenceDeclared: true, new FileNotFoundException()),
					Is.EqualTo(ImpactEffectsRuntimeAssetFailure.Failed));
				Assert.That(ImpactEffectsRuntimeAssetPolicy.ClassifySoundFilePresence(fileExists: false),
					Is.EqualTo(ImpactEffectsRuntimeAssetFailure.BlockedRetailAssets));
			});
		}

		[Test]
		public void OccupancyEvidenceRequiresZeroForEmptyAndOnlyTheTargetForActorCases()
		{
			Assert.Multiple(() =>
			{
				Assert.That(ImpactEffectsRuntimeOccupancyEvidence.Validate(false, 0, Array.Empty<uint>(), out _), Is.True);
				Assert.That(ImpactEffectsRuntimeOccupancyEvidence.Validate(false, 0, new uint[] { 7 }, out _), Is.False);
				Assert.That(ImpactEffectsRuntimeOccupancyEvidence.Validate(true, 7, new uint[] { 7 }, out _), Is.True);
				Assert.That(ImpactEffectsRuntimeOccupancyEvidence.Validate(true, 7, new uint[] { 8 }, out _), Is.False);
				Assert.That(ImpactEffectsRuntimeOccupancyEvidence.Validate(true, 7, new uint[] { 7, 8 }, out _), Is.False);
			});
		}

		[Test]
		public void ScreenshotDecoderRejectsTruncationWrongSizeAndIdenticalCrop()
		{
			var baselineBytes = RgbaPng(4, 4, _ => 0);
			var truncated = baselineBytes.Take(baselineBytes.Length / 2).ToArray();

			Assert.Multiple(() =>
			{
				Assert.Throws<InvalidDataException>(() =>
					ImpactEffectsRuntimeScreenshotEvidence.Decode(truncated, 4, 4, new int2(2, 2), 2));
				Assert.Throws<InvalidDataException>(() =>
					ImpactEffectsRuntimeScreenshotEvidence.Decode(baselineBytes, 5, 4, new int2(2, 2), 2));
			});

			var baseline = ImpactEffectsRuntimeScreenshotEvidence.Decode(
				baselineBytes, 4, 4, new int2(2, 2), 2);
			var identical = ImpactEffectsRuntimeScreenshotEvidence.Decode(
				baselineBytes, 4, 4, new int2(2, 2), 2);
			Assert.That(ImpactEffectsRuntimeScreenshotEvidence.CountChangedPixels(baseline, identical), Is.Zero);
		}

		[Test]
		public void ScreenshotCropReportsChangedPixelsAroundTheTarget()
		{
			var baseline = ImpactEffectsRuntimeScreenshotEvidence.Decode(
				RgbaPng(4, 4, _ => 0), 4, 4, new int2(2, 2), 2);
			var post = ImpactEffectsRuntimeScreenshotEvidence.Decode(
				RgbaPng(4, 4, index => index == 10 ? (byte)255 : (byte)0),
				4, 4, new int2(2, 2), 2);

			Assert.That(ImpactEffectsRuntimeScreenshotEvidence.CountChangedPixels(baseline, post), Is.EqualTo(1));
		}

		[Test]
		public void ScreenshotReadinessRequiresStableLengthAcrossTwoLaterRenderFrames()
		{
			var readiness = new ImpactEffectsRuntimeScreenshotReadiness(requestFrame: 10);

			Assert.Multiple(() =>
			{
				Assert.That(readiness.Observe(10, 100, decoded: true), Is.False);
				Assert.That(readiness.Observe(11, 100, decoded: true), Is.False);
				Assert.That(readiness.Observe(11, 100, decoded: true), Is.False);
				Assert.That(readiness.Observe(12, 101, decoded: true), Is.False);
				Assert.That(readiness.Observe(13, 101, decoded: true), Is.True);
			});
		}

		[Test]
		public void CorrelationEvidenceRejectsScheduledSpritesThatWereNeverActuallyAdded()
		{
			var records = new[]
			{
				Diagnostic(ImpactEffectDiagnosticKind.SpriteScheduled, 1, "nc-impact", "nc_core_small"),
				Diagnostic(ImpactEffectDiagnosticKind.SpriteAdded, 1, "nc-impact", "nc_core_small"),
				Diagnostic(ImpactEffectDiagnosticKind.SpriteScheduled, 2, "explosion", "large_clsn")
			};

			Assert.That(ImpactEffectsRuntimeCorrelationEvidence.Validate(records, 2, out var detail), Is.False);
			StringAssert.Contains("added", detail.ToLowerInvariant());
		}

		[Test]
		public void SpriteLifecycleRequiresEveryAddedSpriteToRenderAndComplete()
		{
			var lifecycle = new ImpactEffectsRuntimeSpriteLifecycleEvidence(expectedCount: 2);
			lifecycle.ObserveAdded(new long[] { 1, 2 });
			lifecycle.ObserveRendered(new long[] { 1 });
			lifecycle.ObserveCompleted(new long[] { 1, 2 });

			Assert.That(lifecycle.Validate(out var missingDetail), Is.False);
			StringAssert.Contains("rendered", missingDetail.ToLowerInvariant());
			lifecycle.ObserveRendered(new long[] { 2 });

			Assert.Multiple(() =>
			{
				Assert.That(lifecycle.Validate(out _), Is.True);
				Assert.That(lifecycle.Added, Is.EqualTo(2));
				Assert.That(lifecycle.Rendered, Is.EqualTo(2));
				Assert.That(lifecycle.Completed, Is.EqualTo(2));
			});
		}

		[Test]
		public void SoundDecoderRequiresValidFormatFieldsAndNonemptyPcm()
		{
			using var source = new MemoryStream(new byte[] { 9 });
			var valid = new TestSoundFormat(2, 16, 22050, 1f, new byte[] { 1, 2 });
			Assert.DoesNotThrow(() => ImpactEffectsRuntimeSoundDecoder.Validate(
				source, new ISoundLoader[] { new TestSoundLoader(valid) }, "ok.wav"));

			using var invalidSource = new MemoryStream(new byte[] { 9 });
			var emptyPcm = new TestSoundFormat(2, 16, 22050, 1f, Array.Empty<byte>());
			Assert.Throws<InvalidDataException>(() => ImpactEffectsRuntimeSoundDecoder.Validate(
				invalidSource, new ISoundLoader[] { new TestSoundLoader(emptyPcm) }, "empty.wav"));
		}

		[Test]
		public void PerformanceWindowExcludesFirstImpactAndCleanupTail()
		{
			Assert.Multiple(() =>
			{
				Assert.That(ImpactEffectsRuntimePerformanceWindow.ShouldSample(
					10, 10, 10, 1, 16, evidenceTicks: 5), Is.False);
				Assert.That(ImpactEffectsRuntimePerformanceWindow.ShouldSample(
					11, 10, 10, 1, 16, evidenceTicks: 5), Is.True);
				Assert.That(ImpactEffectsRuntimePerformanceWindow.ShouldSample(
					45, 10, 40, 16, 16, evidenceTicks: 5), Is.True);
				Assert.That(ImpactEffectsRuntimePerformanceWindow.ShouldSample(
					46, 10, 40, 16, 16, evidenceTicks: 5), Is.False);
			});
		}

		[Test]
		public void JsonSerializationEscapesAllControlCharacters()
		{
			var value = "line1\nline2\t\u0001\\\"";
			var json = ImpactEffectsRuntimeJson.Serialize(value);

			Assert.That(JsonSerializer.Deserialize<string>(json), Is.EqualTo(value));
		}

		[Test]
		public void TraceWatermarkMustBeAvailableAndDroppedEventsAreComparedToBaseline()
		{
			Assert.Throws<InvalidDataException>(() => ImpactEffectsRuntimeEvidence.CaptureWatermark(new DiagnosticTraceSnapshot
			{
				Available = false,
				DroppedEvents = 3,
				Events = Array.Empty<DiagnosticTraceEvent>()
			}));

			var watermark = ImpactEffectsRuntimeEvidence.CaptureWatermark(new DiagnosticTraceSnapshot
			{
				Available = true,
				DroppedEvents = 7,
				Events = Array.Empty<DiagnosticTraceEvent>()
			});

			Assert.Multiple(() =>
			{
				Assert.That(ImpactEffectsRuntimeEvidence.HasNoNewDroppedEvents(Snapshot(Array.Empty<DiagnosticTraceEvent>(), 7), watermark), Is.True);
				Assert.That(ImpactEffectsRuntimeEvidence.HasNoNewDroppedEvents(Snapshot(Array.Empty<DiagnosticTraceEvent>(), 8), watermark), Is.False);
			});
		}

		[Test]
		public void AudioEvidenceDistinguishesDummySchedulingFromObservedDeviceProgress()
		{
			Assert.Multiple(() =>
			{
				Assert.That(ImpactEffectsRuntimeAudioEvidence.Observe(new TestSound(float.NaN, complete: false), dummy: true).Status,
					Is.EqualTo(ImpactEffectsRuntimeAudioStatus.DummyScheduled));
				Assert.That(ImpactEffectsRuntimeAudioEvidence.Observe(new TestSound(0.25f, complete: false), dummy: false).Status,
					Is.EqualTo(ImpactEffectsRuntimeAudioStatus.DeviceStarted));
				Assert.That(ImpactEffectsRuntimeAudioEvidence.Observe(new TestSound(float.NaN, complete: true), dummy: false).Status,
					Is.EqualTo(ImpactEffectsRuntimeAudioStatus.Failed));
				Assert.That(ImpactEffectsRuntimeAudioEvidence.Observe(
					new TestSound(float.NaN, complete: true), dummy: false, alreadyObserved: true).Status,
					Is.EqualTo(ImpactEffectsRuntimeAudioStatus.DeviceStarted),
					"A handle that already demonstrated progress may later complete normally.");
			});
		}

		[Test]
		public void DiagnosticEvidenceRequiresExactLayerDelaysAndOneTerrainSound()
		{
			var target = new WPos(1234, -5678, 90);
			var packed = ImpactEffectsRuntimeEvidence.PackHorizontalPosition(target);
			var impactTick = 300;
			var events = new[]
			{
				Trace(11, "ImpactEffectAudit.SpriteScheduled|101|nc-impact|nc_core_small", impactTick, packed),
				Trace(12, "ImpactEffectAudit.SpriteAdded|101|nc-impact|nc_core_small", impactTick, packed),
				Trace(13, "ImpactEffectAudit.SpriteScheduled|102|explosion|large_clsn", impactTick, packed),
				Trace(14, "ImpactEffectAudit.SpriteAdded|102|explosion|large_clsn", impactTick, packed),
				Trace(15, "ImpactEffectAudit.Sound|105|gexp14a.wav|played|dummy", impactTick, packed, DiagnosticSubsystem.Audio),
				Trace(16, "ImpactEffectAudit.SpriteScheduled|103|nc-impact|nc_ring_small", impactTick + 1, packed),
				Trace(17, "ImpactEffectAudit.SpriteAdded|103|nc-impact|nc_ring_small", impactTick + 1, packed),
				Trace(18, "ImpactEffectAudit.SpriteScheduled|104|nc-impact|nc_debris_small", impactTick + 3, packed),
				Trace(19, "ImpactEffectAudit.SpriteAdded|104|nc-impact|nc_debris_small", impactTick + 3, packed),
				Trace(20, "ImpactEffectAudit.SpriteScheduled|106|explosion|huge_watersplash", impactTick + 1,
					ImpactEffectsRuntimeEvidence.PackHorizontalPosition(new WPos(9, 9, 0)))
			};
			var snapshot = Snapshot(events);
			var expectation = ImpactEffectsRuntimeExpectation.ForWeapon("V3Weapon");

			var evidence = ImpactEffectsRuntimeEvidence.Collect(
				snapshot, afterSequence: 10, target, impactTick, expectation, ImpactEffectsRuntimeTerrain.Land);

			Assert.Multiple(() =>
			{
				Assert.That(evidence.Valid, Is.True, evidence.Detail);
				Assert.That(evidence.SpriteRecords, Has.Count.EqualTo(4));
				Assert.That(evidence.SpriteAddedRecords, Has.Count.EqualTo(4));
				Assert.That(evidence.SoundRecords, Has.Count.EqualTo(1));
				Assert.That(evidence.SoundRecords.Single().PlaybackReturned, Is.True);
				Assert.That(evidence.SoundRecords.Single().DummyEngine, Is.True);
			});
		}

		[Test]
		public void DiagnosticEvidenceRejectsWrongDelayOrDuplicateSound()
		{
			var target = new WPos(1234, 5678, 0);
			var packed = ImpactEffectsRuntimeEvidence.PackHorizontalPosition(target);
			var impactTick = 50;
			var events = new[]
			{
				Trace(1, "ImpactEffectAudit.SpriteScheduled|1|nc-impact|nc_core_small", impactTick, packed),
				Trace(2, "ImpactEffectAudit.SpriteAdded|1|nc-impact|nc_core_small", impactTick, packed),
				Trace(3, "ImpactEffectAudit.SpriteScheduled|2|explosion|large_clsn", impactTick, packed),
				Trace(4, "ImpactEffectAudit.SpriteAdded|2|explosion|large_clsn", impactTick, packed),
				Trace(5, "ImpactEffectAudit.Sound|5|gexp14a.wav|played|device", impactTick, packed, DiagnosticSubsystem.Audio),
				Trace(6, "ImpactEffectAudit.Sound|6|gexp14a.wav|played|device", impactTick, packed, DiagnosticSubsystem.Audio),
				Trace(7, "ImpactEffectAudit.SpriteScheduled|3|nc-impact|nc_ring_small", impactTick + 2, packed),
				Trace(8, "ImpactEffectAudit.SpriteAdded|3|nc-impact|nc_ring_small", impactTick + 2, packed),
				Trace(9, "ImpactEffectAudit.SpriteScheduled|4|nc-impact|nc_debris_small", impactTick + 3, packed),
				Trace(10, "ImpactEffectAudit.SpriteAdded|4|nc-impact|nc_debris_small", impactTick + 3, packed)
			};

			var evidence = ImpactEffectsRuntimeEvidence.Collect(Snapshot(events), 0, target, impactTick,
				ImpactEffectsRuntimeExpectation.ForWeapon("V3Weapon"), ImpactEffectsRuntimeTerrain.Land);

			Assert.That(evidence.Valid, Is.False);
			StringAssert.Contains("sound", evidence.Detail.ToLowerInvariant());
		}

		[Test]
		public void P95UsesNearestRankAndHandlesEmptyInput()
		{
			Assert.Multiple(() =>
			{
				Assert.That(ImpactEffectsRuntimeEvidence.Percentile95(Array.Empty<double>()), Is.Zero);
				Assert.That(ImpactEffectsRuntimeEvidence.Percentile95(Enumerable.Range(1, 20).Select(i => (double)i)),
					Is.EqualTo(19));
			});
		}

		[Test]
		public void BurstSchedulesEveryImpactAtAnExactTwoTickInterval()
		{
			var config = ImpactEffectsRuntimeAuditConfiguration.CreateForTests();
			var impactTicks = new List<int>();
			var completedImpacts = 0;
			for (var relativeTick = 0; relativeTick <= 40; relativeTick++)
				if (config.IsBurstImpactDue(relativeTick, completedImpacts))
				{
					impactTicks.Add(relativeTick);
					completedImpacts++;
				}

			Assert.That(impactTicks, Is.EqualTo(
				Enumerable.Range(1, config.BurstImpactCount).Select(index => index * config.BurstIntervalTicks)));
		}

		[Test]
		public void RuntimeHarnessUsesRealResolvedImpactsRenderingScreenshotsAndBurstEvidence()
		{
			var root = RepositoryRoot();
			var audit = File.ReadAllText(Path.Combine(
				root, "OpenRA.Mods.RA2", "Traits", "ImpactEffectsRuntimeAudit.cs"));
			var benchmark = File.ReadAllText(Path.Combine(
				root, "OpenRA.Mods.RA2", "Traits", "IosHighUnitBenchmark.cs"));
			var effect = File.ReadAllText(Path.Combine(
				root, "engine", "OpenRA.Mods.Common", "Warheads", "CreateEffectWarhead.cs"));

			StringAssert.Contains("world.Map.Rules.Weapons", audit);
			StringAssert.Contains(".Impact(Target.FromPos", audit);
			StringAssert.Contains("Target.FromActor", audit);
			StringAssert.Contains("world.Effects.OfType<SpriteEffect>()", audit);
			StringAssert.Contains("registration.Effect.Render(worldRenderer)", audit);
			StringAssert.Contains("world.FogObscures", audit);
			StringAssert.Contains("world.ShroudObscures", audit);
			StringAssert.Contains("Game.Renderer.SaveScreenshot", audit);
			StringAssert.Contains("Game.RenderFrame", audit);
			StringAssert.Contains("BLOCKED_RETAIL_ASSETS", audit);
			StringAssert.Contains("render_prepare", audit);
			StringAssert.Contains("BurstIntervalTicks", audit);
			StringAssert.Contains("ImpactEffectsRuntimeScreenshotEvidence.Decode", audit);
			StringAssert.Contains("ImpactEffectsRuntimeOccupancyEvidence.Validate", audit);
			StringAssert.Contains("ImpactEffectAuditRegistry.Snapshot", audit);
			StringAssert.Contains("ImpactEffectsRuntimeSoundDecoder.Validate", audit);
			StringAssert.Contains("ImpactEffectsRuntimePerformanceWindow.ShouldSample", audit);
			StringAssert.Contains("terminal.MarkWritersClosed", audit);
			StringAssert.Contains("ImpactEffectsRuntimeAuditConfiguration.Parse", benchmark);
			StringAssert.Contains("ImpactEffectsRuntimeAuditSession.TryCreate", benchmark);
			StringAssert.Contains("impactEffectsRuntimeAudit.Tick()", benchmark);
			StringAssert.Contains("OPENRA_IMPACT_EFFECT_AUDIT", effect);
			StringAssert.Contains("ImpactEffectAudit.SpriteScheduled|", effect);
			StringAssert.Contains("ImpactEffectAudit.SpriteAdded|", effect);
			StringAssert.Contains("ImpactEffectAudit.Sound|", effect);
			StringAssert.Contains("ImpactEffectAuditRegistry.ScheduleSprite", effect);
			StringAssert.Contains("ImpactEffectAuditRegistry.MarkSpriteAdded", effect);
			StringAssert.Contains("ImpactEffectAuditRegistry.RegisterSound", effect);
			StringAssert.Contains("INotifyActorDisposing", benchmark);
			StringAssert.Contains("AbortForWorldDisposal", benchmark);
		}

		[Test]
		public void DesktopRunnerRequiresStrictProtocolIsolationAndEvidence()
		{
			var scriptPath = Path.Combine(
				RepositoryRoot(), "packaging", "run_impact_effects_runtime_audit.sh");
			Assert.That(File.Exists(scriptPath), Is.True, "The impact runtime audit runner is missing.");
			var script = File.ReadAllText(scriptPath);

			StringAssert.Contains("OPENRA_IMPACT_EFFECT_AUDIT=true", script);
			StringAssert.Contains("OPENRA_V3_RUNTIME_AUDIT=false", script);
			StringAssert.Contains("OPENRA_IOS_DESTRUCTION_AUDIT=false", script);
			StringAssert.Contains("OPENRA_IOS_PERF_COUNT=", script);
			StringAssert.Contains("game_pid=$!", script);
			StringAssert.Contains("ps -p \"$game_pid\" -o ppid= -o lstart= -o command=", script);
			StringAssert.Contains("launch_identity_pending=true", script);
			StringAssert.Contains("signal_owned_game TERM", script);
			StringAssert.Contains("signal_owned_game KILL", script);
			StringAssert.Contains("runtime-audit.lock", script);
			StringAssert.Contains("dotnet_bin\" build", script);
			StringAssert.Contains("evidence.sha256", script);
			StringAssert.Contains("if status == \"PASSED\":", script);
			StringAssert.Contains("\"functional_status\": \"PASSED\"", script);
			StringAssert.Contains("\"performance_status\": \"RECORDED_FOR_REVIEW\"", script);
			StringAssert.Contains("\"total\": 16", script);
			StringAssert.Contains("\"passed\": 16", script);
			StringAssert.Contains("\"screenshots\": 16", script);
			StringAssert.Contains("BLOCKED_RETAIL_ASSETS", script);
			StringAssert.DoesNotContain("pkill", script);
			StringAssert.DoesNotContain("killall", script);
			StringAssert.DoesNotContain("xcrun", script);
		}

		static DiagnosticTraceEvent Trace(long sequence, string name, long tick, long packed,
			DiagnosticSubsystem subsystem = DiagnosticSubsystem.Render) =>
			new(sequence, 0, 1, subsystem, DiagnosticTracePhase.Instant, 0, 0, name, tick, packed);

		static DiagnosticTraceSnapshot Snapshot(DiagnosticTraceEvent[] events) => new()
		{
			Available = true,
			DroppedEvents = 0,
			Events = events,
			ActiveScopes = Array.Empty<DiagnosticActiveScope>(),
			Heartbeats = new Dictionary<DiagnosticSubsystem, DiagnosticHeartbeat>()
		};

		static DiagnosticTraceSnapshot Snapshot(DiagnosticTraceEvent[] events, long droppedEvents) => new()
		{
			Available = true,
			DroppedEvents = droppedEvents,
			Events = events,
			ActiveScopes = Array.Empty<DiagnosticActiveScope>(),
			Heartbeats = new Dictionary<DiagnosticSubsystem, DiagnosticHeartbeat>()
		};

		static ImpactEffectDiagnosticRecord Diagnostic(ImpactEffectDiagnosticKind kind, long id,
			string image, string asset) => new(kind, id, image, asset, 0, false, false);

		static byte[] RgbaPng(int width, int height, Func<int, byte> red)
		{
			var data = new byte[width * height * 4];
			for (var i = 0; i < width * height; i++)
			{
				data[4 * i] = red(i);
				data[4 * i + 3] = 255;
			}

			return new Png(data, SpriteFrameType.Rgba32, width, height).Save();
		}

		sealed class TestSound : ISound
		{
			public float Volume { get; set; }
			public float SeekPosition { get; }
			public bool Complete { get; }

			public TestSound(float seekPosition, bool complete)
			{
				SeekPosition = seekPosition;
				Complete = complete;
			}

			public void SetPosition(WPos pos) { }
		}

		sealed class TestSoundLoader : ISoundLoader
		{
			readonly ISoundFormat format;

			public TestSoundLoader(ISoundFormat format)
			{
				this.format = format;
			}

			public bool TryParseSound(Stream stream, out ISoundFormat sound)
			{
				sound = format;
				return true;
			}
		}

		sealed class TestSoundFormat : ISoundFormat
		{
			readonly byte[] pcm;

			public int Channels { get; }
			public int SampleBits { get; }
			public int SampleRate { get; }
			public float LengthInSeconds { get; }

			public TestSoundFormat(int channels, int sampleBits, int sampleRate,
				float lengthInSeconds, byte[] pcm)
			{
				Channels = channels;
				SampleBits = sampleBits;
				SampleRate = sampleRate;
				LengthInSeconds = lengthInSeconds;
				this.pcm = pcm;
			}

			public Stream GetPCMInputStream() => new MemoryStream(pcm, writable: false);
			public void Dispose() { }
		}
	}
}
