#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using OpenRA.FileFormats;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	public enum ImpactEffectsRuntimeTerrain
	{
		Land,
		Water
	}

	public sealed class ImpactEffectsRuntimeLayerExpectation
	{
		public string Image { get; }
		public string Sequence { get; }
		public int Delay { get; }
		public IReadOnlyList<string> ImpactSounds { get; }

		public ImpactEffectsRuntimeLayerExpectation(string image, string sequence, int delay,
			params string[] impactSounds)
		{
			Image = image;
			Sequence = sequence;
			Delay = delay;
			ImpactSounds = impactSounds ?? Array.Empty<string>();
		}
	}

	public sealed class ImpactEffectsRuntimeExpectation
	{
		static readonly IReadOnlyDictionary<string, ImpactEffectsRuntimeExpectation> Expectations =
			new Dictionary<string, ImpactEffectsRuntimeExpectation>(StringComparer.Ordinal)
			{
				["V3Weapon"] = new("V3Weapon", "nc_core_small", "large_clsn", "nc_ring_small",
					"nc_debris_small", "large_watersplash", "gexp14a.wav", 5, 1),
				["V3WeaponE"] = new("V3WeaponE", "nc_core_large", "terrorist_explosion", "nc_ring_large",
					"nc_debris_large", "huge_watersplash", "gexpapoa.wav", 8, 2),
				["BlimpBomb"] = new("BlimpBomb", "nc_core_large", "verylarge_clsn", "nc_ring_large",
					"nc_debris_large", "huge_watersplash", "gexp14a.wav", 6, 2),
				["BlimpBombE"] = new("BlimpBombE", "nc_core_tesla", "kirovtesla", "nc_ring_tesla",
					"nc_debris_large", "huge_watersplash", "gexp14a.wav", 8, 3)
			};

		readonly ImpactEffectsRuntimeLayerExpectation[] landLayers;
		readonly ImpactEffectsRuntimeLayerExpectation[] waterLayers;

		public string WeaponName { get; }
		public int ShakeDuration { get; }
		public int ShakeIntensity { get; }

		ImpactEffectsRuntimeExpectation(string weaponName, string core, string main, string ring,
			string plume, string splash, string landSound, int shakeDuration, int shakeIntensity)
		{
			WeaponName = weaponName;
			ShakeDuration = shakeDuration;
			ShakeIntensity = shakeIntensity;
			landLayers = new[]
			{
				new ImpactEffectsRuntimeLayerExpectation("nc-impact", core, 0),
				new ImpactEffectsRuntimeLayerExpectation("explosion", main, 0, landSound),
				new ImpactEffectsRuntimeLayerExpectation("nc-impact", ring, 1),
				new ImpactEffectsRuntimeLayerExpectation("nc-impact", plume, 3)
			};
			waterLayers = new[]
			{
				new ImpactEffectsRuntimeLayerExpectation("nc-impact", core, 0),
				new ImpactEffectsRuntimeLayerExpectation("explosion", main, 0),
				new ImpactEffectsRuntimeLayerExpectation("nc-impact", ring, 1),
				new ImpactEffectsRuntimeLayerExpectation("explosion", splash, 1, "gexpwasa.wav")
			};
		}

		public IReadOnlyList<ImpactEffectsRuntimeLayerExpectation> LayersFor(ImpactEffectsRuntimeTerrain terrain) =>
			terrain == ImpactEffectsRuntimeTerrain.Land ? landLayers : waterLayers;

		public static ImpactEffectsRuntimeExpectation ForWeapon(string weaponName)
		{
			if (!Expectations.TryGetValue(weaponName, out var expectation))
				throw new ArgumentException($"Unknown impact audit weapon `{weaponName}`.", nameof(weaponName));

			return expectation;
		}

		public static IEnumerable<ImpactEffectsRuntimeExpectation> All()
		{
			foreach (var weaponName in new[] { "V3Weapon", "V3WeaponE", "BlimpBomb", "BlimpBombE" })
				yield return Expectations[weaponName];
		}
	}

	public sealed class ImpactEffectsRuntimeAuditCase
	{
		public string Id { get; }
		public string WeaponName { get; }
		public ImpactEffectsRuntimeTerrain Terrain { get; }
		public bool HasActor { get; }
		public string TargetActorName => Terrain == ImpactEffectsRuntimeTerrain.Land ? "apoc" : "dred";
		public ImpactEffectsRuntimeExpectation Expectation => ImpactEffectsRuntimeExpectation.ForWeapon(WeaponName);

		public ImpactEffectsRuntimeAuditCase(string id, string weaponName,
			ImpactEffectsRuntimeTerrain terrain, bool hasActor)
		{
			Id = id;
			WeaponName = weaponName;
			Terrain = terrain;
			HasActor = hasActor;
		}
	}

	public sealed class ImpactEffectsRuntimeAuditConfiguration
	{
		const int DefaultTimeoutTicks = 300;
		const int MinimumTimeoutTicks = 60;
		const int MaximumTimeoutTicks = 1200;

		public bool Enabled { get; private init; }
		public int TimeoutTicks { get; private init; }
		public string RunId { get; private init; }
		public int BurstImpactCount => 16;
		public int BurstIntervalTicks => 2;
		public bool IsBurstImpactDue(int relativeTick, int completedImpacts) =>
			completedImpacts >= 0 && completedImpacts < BurstImpactCount &&
			relativeTick >= (completedImpacts + 1) * BurstIntervalTicks;
		public bool IsBurstTimedOut(int relativeTick) => relativeTick > TimeoutTicks;

		public static ImpactEffectsRuntimeAuditConfiguration Parse(Func<string, string> read)
		{
			if (!bool.TryParse(read("OPENRA_IMPACT_EFFECT_AUDIT"), out var enabled) || !enabled)
				return new ImpactEffectsRuntimeAuditConfiguration
				{
					Enabled = false,
					TimeoutTicks = DefaultTimeoutTicks,
					RunId = string.Empty
				};

			var timeout = int.TryParse(read("OPENRA_IMPACT_EFFECT_AUDIT_TIMEOUT_TICKS"),
				NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTimeout)
				? parsedTimeout.Clamp(MinimumTimeoutTicks, MaximumTimeoutTicks)
				: DefaultTimeoutTicks;
			var runId = SafeRunId(read("OPENRA_IMPACT_EFFECT_AUDIT_RUN_ID"));
			if (runId.Length == 0)
				runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

			return new ImpactEffectsRuntimeAuditConfiguration
			{
				Enabled = true,
				TimeoutTicks = timeout,
				RunId = runId
			};
		}

		public static ImpactEffectsRuntimeAuditConfiguration CreateForTests(int timeoutTicks = DefaultTimeoutTicks) =>
			new() { Enabled = true, TimeoutTicks = timeoutTicks, RunId = "test" };

		public IEnumerable<ImpactEffectsRuntimeAuditCase> BuildCases()
		{
			var labels = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["V3Weapon"] = "v3-normal",
				["V3WeaponE"] = "v3-elite",
				["BlimpBomb"] = "kirov-normal",
				["BlimpBombE"] = "kirov-elite"
			};

			foreach (var expectation in ImpactEffectsRuntimeExpectation.All())
				foreach (var terrain in new[] { ImpactEffectsRuntimeTerrain.Land, ImpactEffectsRuntimeTerrain.Water })
					foreach (var hasActor in new[] { false, true })
					{
						var terrainLabel = terrain.ToString().ToLowerInvariant();
						var targetLabel = hasActor ? "actor" : "empty";
						yield return new ImpactEffectsRuntimeAuditCase(
							$"{labels[expectation.WeaponName]}-{terrainLabel}-{targetLabel}",
							expectation.WeaponName, terrain, hasActor);
					}
		}

		public static bool ShouldStartInWorld(WorldType worldType) => worldType == WorldType.Regular;

		static string SafeRunId(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return string.Empty;

			return new string(value.Trim()
				.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
				.Take(80)
				.ToArray());
		}
	}

	public sealed class ImpactEffectsRuntimeTerminalState
	{
		public bool Finishing { get; private set; }
		public bool WritersClosed { get; private set; }
		public bool Completed { get; private set; }

		public bool TryBeginFinishing()
		{
			if (Finishing || Completed)
				return false;

			Finishing = true;
			return true;
		}

		public void MarkWritersClosed()
		{
			if (!Finishing)
				throw new InvalidOperationException("Terminal finishing has not begun.");

			WritersClosed = true;
		}

		public void MarkTerminalSummaryCommitted()
		{
			if (!WritersClosed)
				throw new InvalidOperationException("Evidence writers must close before committing the terminal summary.");

			Completed = true;
		}
	}

	public enum ImpactEffectsRuntimeAssetFailure
	{
		BlockedRetailAssets,
		Failed
	}

	public static class ImpactEffectsRuntimeAssetPolicy
	{
		public static ImpactEffectsRuntimeAssetFailure ClassifySequenceFailure(
			string image, bool sequenceDeclared, Exception exception) =>
			sequenceDeclared && string.Equals(image, "explosion", StringComparison.Ordinal) &&
			exception is FileNotFoundException
				? ImpactEffectsRuntimeAssetFailure.BlockedRetailAssets
				: ImpactEffectsRuntimeAssetFailure.Failed;

		public static ImpactEffectsRuntimeAssetFailure ClassifySoundFilePresence(bool fileExists) =>
			fileExists ? ImpactEffectsRuntimeAssetFailure.Failed : ImpactEffectsRuntimeAssetFailure.BlockedRetailAssets;
	}

	public static class ImpactEffectsRuntimeOccupancyEvidence
	{
		public static bool Validate(bool hasActor, uint targetActorId,
			IEnumerable<uint> overlappingActorIds, out string detail)
		{
			var ids = overlappingActorIds?.Distinct().OrderBy(id => id).ToArray() ?? Array.Empty<uint>();
			var valid = hasActor
				? ids.Length == 1 && ids[0] == targetActorId
				: ids.Length == 0;
			detail = valid
				? string.Empty
				: $"Impact occupancy mismatch: target={(hasActor ? targetActorId : 0)} " +
					$"count={ids.Length} actors=[{string.Join(',', ids)}].";
			return valid;
		}
	}

	public sealed class ImpactEffectsRuntimeDecodedScreenshot
	{
		public int Width { get; }
		public int Height { get; }
		public int PixelStride { get; }
		public Rectangle CropBounds { get; }
		public byte[] CropData { get; }

		public ImpactEffectsRuntimeDecodedScreenshot(int width, int height, int pixelStride,
			Rectangle cropBounds, byte[] cropData)
		{
			Width = width;
			Height = height;
			PixelStride = pixelStride;
			CropBounds = cropBounds;
			CropData = cropData;
		}
	}

	public static class ImpactEffectsRuntimeScreenshotEvidence
	{
		public static ImpactEffectsRuntimeDecodedScreenshot Decode(byte[] bytes,
			int expectedWidth, int expectedHeight, int2 center, int cropSize)
		{
			if (bytes == null || bytes.Length == 0)
				throw new InvalidDataException("Screenshot PNG is empty.");

			try
			{
				using var stream = new MemoryStream(bytes, writable: false);
				var png = new Png(stream);
				if (png.Width != expectedWidth || png.Height != expectedHeight)
					throw new InvalidDataException(
						$"Screenshot dimensions {png.Width}x{png.Height} do not match " +
						$"renderer {expectedWidth}x{expectedHeight}.");

				if (cropSize <= 0 || png.PixelStride <= 0 ||
					png.Data.Length != png.Width * png.Height * png.PixelStride)
					throw new InvalidDataException("Screenshot pixel data is invalid.");

				var half = cropSize / 2;
				var left = Math.Max(0, center.X - half);
				var top = Math.Max(0, center.Y - half);
				var right = Math.Min(png.Width, left + cropSize);
				var bottom = Math.Min(png.Height, top + cropSize);
				left = Math.Max(0, right - cropSize);
				top = Math.Max(0, bottom - cropSize);
				var crop = Rectangle.FromLTRB(left, top, right, bottom);
				if (crop.Width <= 0 || crop.Height <= 0)
					throw new InvalidDataException("Screenshot target crop is outside the renderer bounds.");

				var cropData = new byte[crop.Width * crop.Height * png.PixelStride];
				for (var y = 0; y < crop.Height; y++)
					Array.Copy(png.Data, ((crop.Y + y) * png.Width + crop.X) * png.PixelStride,
						cropData, y * crop.Width * png.PixelStride, crop.Width * png.PixelStride);

				return new ImpactEffectsRuntimeDecodedScreenshot(
					png.Width, png.Height, png.PixelStride, crop, cropData);
			}
			catch (InvalidDataException)
			{
				throw;
			}
			catch (Exception e) when (e is not OutOfMemoryException)
			{
				throw new InvalidDataException("Screenshot PNG could not be fully decoded.", e);
			}
		}

		public static int CountChangedPixels(ImpactEffectsRuntimeDecodedScreenshot baseline,
			ImpactEffectsRuntimeDecodedScreenshot post)
		{
			if (baseline == null || post == null || baseline.PixelStride != post.PixelStride ||
				baseline.CropBounds.Size != post.CropBounds.Size ||
				baseline.CropData.Length != post.CropData.Length)
				throw new InvalidDataException("Screenshot crops are not comparable.");

			var changed = 0;
			for (var offset = 0; offset < baseline.CropData.Length; offset += baseline.PixelStride)
			{
				var pixelChanged = false;
				for (var channel = 0; channel < baseline.PixelStride; channel++)
					pixelChanged |= baseline.CropData[offset + channel] != post.CropData[offset + channel];

				if (pixelChanged)
					changed++;
			}

			return changed;
		}
	}

	public sealed class ImpactEffectsRuntimeScreenshotReadiness
	{
		readonly int requestFrame;
		int lastFrame = -1;
		long lastLength = -1;
		int stableFrames;

		public ImpactEffectsRuntimeScreenshotReadiness(int requestFrame)
		{
			this.requestFrame = requestFrame;
		}

		public bool Observe(int renderFrame, long length, bool decoded)
		{
			if (renderFrame <= requestFrame || renderFrame == lastFrame)
				return false;

			lastFrame = renderFrame;
			if (!decoded || length <= 0)
			{
				lastLength = -1;
				stableFrames = 0;
				return false;
			}

			stableFrames = length == lastLength ? stableFrames + 1 : 1;
			lastLength = length;
			return stableFrames >= 2;
		}
	}

	public static class ImpactEffectsRuntimePerformanceWindow
	{
		public static bool ShouldSample(int tick, int firstImpactTick, int lastImpactTick,
			int completedImpacts, int expectedImpacts, int evidenceTicks) =>
			firstImpactTick >= 0 && tick > firstImpactTick &&
			(completedImpacts < expectedImpacts || tick <= lastImpactTick + evidenceTicks);
	}

	public static class ImpactEffectsRuntimeJson
	{
		public static string Serialize<T>(T value) => JsonSerializer.Serialize(value);
		public static string Serialize<T>(T value, JsonSerializerOptions options) =>
			JsonSerializer.Serialize(value, options);
	}

	public enum ImpactEffectsRuntimeAudioStatus
	{
		Pending,
		DummyScheduled,
		DeviceStarted,
		Failed
	}

	public readonly struct ImpactEffectsRuntimeAudioObservation
	{
		public ImpactEffectsRuntimeAudioStatus Status { get; }
		public string Detail { get; }

		public ImpactEffectsRuntimeAudioObservation(ImpactEffectsRuntimeAudioStatus status, string detail)
		{
			Status = status;
			Detail = detail;
		}
	}

	public static class ImpactEffectsRuntimeAudioEvidence
	{
		public static ImpactEffectsRuntimeAudioObservation Observe(
			ISound playback, bool dummy, bool alreadyObserved)
		{
			if (alreadyObserved)
				return new ImpactEffectsRuntimeAudioObservation(
					dummy ? ImpactEffectsRuntimeAudioStatus.DummyScheduled :
						ImpactEffectsRuntimeAudioStatus.DeviceStarted,
					"Playback evidence was already observed before normal completion.");

			return Observe(playback, dummy);
		}

		public static ImpactEffectsRuntimeAudioObservation Observe(ISound playback, bool dummy)
		{
			if (playback == null)
				return new ImpactEffectsRuntimeAudioObservation(
					ImpactEffectsRuntimeAudioStatus.Failed, "Playback returned null.");

			if (dummy)
				return new ImpactEffectsRuntimeAudioObservation(
					ImpactEffectsRuntimeAudioStatus.DummyScheduled, "Dummy backend is schedule-only.");

			try
			{
				var seek = playback.SeekPosition;
				if (float.IsNaN(seek) || float.IsInfinity(seek) || seek < 0)
					return new ImpactEffectsRuntimeAudioObservation(
						ImpactEffectsRuntimeAudioStatus.Failed, $"Playback progress is not finite: {seek}.");

				if (seek > 0)
					return new ImpactEffectsRuntimeAudioObservation(
						ImpactEffectsRuntimeAudioStatus.DeviceStarted, $"Playback progressed to {seek} seconds.");

				if (playback.Complete)
					return new ImpactEffectsRuntimeAudioObservation(
						ImpactEffectsRuntimeAudioStatus.Failed, "Playback completed before observable progress.");

				return new ImpactEffectsRuntimeAudioObservation(ImpactEffectsRuntimeAudioStatus.Pending, string.Empty);
			}
			catch (Exception e)
			{
				return new ImpactEffectsRuntimeAudioObservation(
					ImpactEffectsRuntimeAudioStatus.Failed, $"Playback observation failed: {e.GetType().Name}: {e.Message}");
			}
		}
	}

	public static class ImpactEffectsRuntimeSoundDecoder
	{
		public static void Validate(Stream stream, IEnumerable<ISoundLoader> loaders, string asset)
		{
			if (stream == null)
				throw new InvalidDataException($"Sound `{asset}` stream is null.");

			foreach (var loader in loaders ?? Array.Empty<ISoundLoader>())
			{
				try
				{
					if (stream.CanSeek)
						stream.Position = 0;

					if (!loader.TryParseSound(stream, out var format))
						continue;

					if (format == null)
						throw new InvalidDataException($"Sound `{asset}` loader returned a null format.");

					using (format)
					{
						if (format.Channels <= 0 || format.SampleBits <= 0 || format.SampleRate <= 0 ||
							!float.IsFinite(format.LengthInSeconds) || format.LengthInSeconds <= 0)
							throw new InvalidDataException($"Sound `{asset}` format fields are invalid.");

						using var pcm = format.GetPCMInputStream();
						if (pcm == null)
							throw new InvalidDataException($"Sound `{asset}` PCM stream is null.");

						var buffer = new byte[8192];
						long decodedBytes = 0;
						int read;
						while ((read = pcm.Read(buffer, 0, buffer.Length)) > 0)
							decodedBytes += read;

						if (decodedBytes == 0)
							throw new InvalidDataException($"Sound `{asset}` decoded to empty PCM.");
					}

					return;
				}
				catch (InvalidDataException)
				{
					throw;
				}
				catch (Exception e)
				{
					throw new InvalidDataException($"Sound `{asset}` failed to decode.", e);
				}
			}

			throw new InvalidDataException($"Sound `{asset}` has no compatible decoder.");
		}
	}

	public enum ImpactEffectsRuntimeAuditPhase
	{
		Prepare,
		WaitForBaseline,
		Impact,
		Observe,
		WaitForScreenshot,
		Cleanup,
		Passed,
		Failed
	}

	public sealed class ImpactEffectsRuntimeAuditStateMachine
	{
		readonly int timeoutTicks;

		public ImpactEffectsRuntimeAuditPhase Phase { get; private set; } = ImpactEffectsRuntimeAuditPhase.Prepare;
		public string Detail { get; private set; } = string.Empty;
		public int Ticks { get; private set; }
		public bool IsTerminal => Phase is ImpactEffectsRuntimeAuditPhase.Passed or ImpactEffectsRuntimeAuditPhase.Failed;

		public ImpactEffectsRuntimeAuditStateMachine(int timeoutTicks)
		{
			this.timeoutTicks = timeoutTicks;
		}

		public void ScenarioReady() => Transition(ImpactEffectsRuntimeAuditPhase.Prepare,
			ImpactEffectsRuntimeAuditPhase.WaitForBaseline);
		public void BaselineReady() => Transition(ImpactEffectsRuntimeAuditPhase.WaitForBaseline,
			ImpactEffectsRuntimeAuditPhase.Impact);
		public void Impacted() => Transition(ImpactEffectsRuntimeAuditPhase.Impact,
			ImpactEffectsRuntimeAuditPhase.Observe);
		public void EvidenceReady() => Transition(ImpactEffectsRuntimeAuditPhase.Observe,
			ImpactEffectsRuntimeAuditPhase.WaitForScreenshot);
		public void ScreenshotReady() => Transition(ImpactEffectsRuntimeAuditPhase.WaitForScreenshot,
			ImpactEffectsRuntimeAuditPhase.Cleanup);
		public void Cleaned() => Transition(ImpactEffectsRuntimeAuditPhase.Cleanup,
			ImpactEffectsRuntimeAuditPhase.Passed);

		public void Fail(string detail)
		{
			Detail = detail;
			Transition(ImpactEffectsRuntimeAuditPhase.Failed);
		}

		public void Tick()
		{
			if (IsTerminal)
				return;

			if (++Ticks > timeoutTicks)
				Fail($"Timed out in {Phase} after {Ticks} ticks");
		}

		void Transition(ImpactEffectsRuntimeAuditPhase expected, ImpactEffectsRuntimeAuditPhase phase)
		{
			if (Phase != expected)
				throw new InvalidOperationException($"Cannot transition from {Phase}; expected {expected}.");

			Phase = phase;
		}

		void Transition(ImpactEffectsRuntimeAuditPhase phase)
		{
			Phase = phase;
		}
	}

	public enum ImpactEffectDiagnosticKind
	{
		SpriteScheduled,
		SpriteAdded,
		Sound
	}

	public sealed class ImpactEffectDiagnosticRecord
	{
		public ImpactEffectDiagnosticKind Kind { get; }
		public long CorrelationId { get; }
		public string Image { get; }
		public string Asset { get; }
		public int TickOffset { get; }
		public bool PlaybackReturned { get; }
		public bool DummyEngine { get; }

		public ImpactEffectDiagnosticRecord(ImpactEffectDiagnosticKind kind, long correlationId, string image,
			string asset, int tickOffset, bool playbackReturned, bool dummyEngine)
		{
			Kind = kind;
			CorrelationId = correlationId;
			Image = image;
			Asset = asset;
			TickOffset = tickOffset;
			PlaybackReturned = playbackReturned;
			DummyEngine = dummyEngine;
		}
	}

	public static class ImpactEffectsRuntimeCorrelationEvidence
	{
		public static bool Validate(IEnumerable<ImpactEffectDiagnosticRecord> records,
			int expectedCount, out string detail)
		{
			var all = records?.ToArray() ?? Array.Empty<ImpactEffectDiagnosticRecord>();
			var scheduled = all.Where(record => record.Kind == ImpactEffectDiagnosticKind.SpriteScheduled).ToArray();
			var added = all.Where(record => record.Kind == ImpactEffectDiagnosticKind.SpriteAdded).ToArray();
			if (scheduled.Length != expectedCount || scheduled.Any(record => record.CorrelationId <= 0) ||
				scheduled.Select(record => record.CorrelationId).Distinct().Count() != expectedCount)
			{
				detail = $"Expected {expectedCount} uniquely correlated scheduled sprites; observed {scheduled.Length}.";
				return false;
			}

			if (added.Length != expectedCount || added.Select(record => record.CorrelationId).Distinct().Count() != expectedCount)
			{
				detail = $"Expected {expectedCount} actually added sprites; observed {added.Length}.";
				return false;
			}

			foreach (var schedule in scheduled)
				if (!added.Any(actual => actual.CorrelationId == schedule.CorrelationId &&
					actual.Image == schedule.Image && actual.Asset == schedule.Asset &&
					actual.TickOffset == schedule.TickOffset))
				{
					detail = $"Scheduled sprite {schedule.CorrelationId} was not actually added with matching evidence.";
					return false;
				}

			detail = string.Empty;
			return true;
		}
	}

	public sealed class ImpactEffectsRuntimeSpriteLifecycleEvidence
	{
		readonly int expectedCount;
		readonly HashSet<long> added = new();
		readonly HashSet<long> rendered = new();
		readonly HashSet<long> completed = new();

		public int Added => added.Count;
		public int Rendered => rendered.Count;
		public int Completed => completed.Count;

		public ImpactEffectsRuntimeSpriteLifecycleEvidence(int expectedCount)
		{
			if (expectedCount < 0)
				throw new ArgumentOutOfRangeException(nameof(expectedCount));

			this.expectedCount = expectedCount;
		}

		public void ObserveAdded(IEnumerable<long> ids) => Add(added, ids);
		public void ObserveRendered(IEnumerable<long> ids) => Add(rendered, ids);
		public void ObserveCompleted(IEnumerable<long> ids) => Add(completed, ids);
		public void ObserveAdded(long id) => Add(added, id);
		public void ObserveRendered(long id) => Add(rendered, id);
		public void ObserveCompleted(long id) => Add(completed, id);

		public bool Validate(out string detail)
		{
			if (added.Count != expectedCount)
			{
				detail = $"Expected {expectedCount} added sprites; observed {added.Count}.";
				return false;
			}

			if (rendered.Count != expectedCount || !rendered.SetEquals(added))
			{
				detail = $"Expected {expectedCount} rendered sprites matching added IDs; observed {rendered.Count}.";
				return false;
			}

			if (completed.Count != expectedCount || !completed.SetEquals(added))
			{
				detail = $"Expected {expectedCount} completed sprites matching added IDs; observed {completed.Count}.";
				return false;
			}

			detail = string.Empty;
			return true;
		}

		static void Add(HashSet<long> destination, IEnumerable<long> ids)
		{
			if (ids == null)
				return;

			foreach (var id in ids)
				if (id > 0)
					destination.Add(id);
		}

		static void Add(HashSet<long> destination, long id)
		{
			if (id > 0)
				destination.Add(id);
		}
	}

	public sealed class ImpactEffectTraceEvidence
	{
		public bool Valid { get; }
		public string Detail { get; }
		public IReadOnlyList<ImpactEffectDiagnosticRecord> SpriteRecords { get; }
		public IReadOnlyList<ImpactEffectDiagnosticRecord> SpriteAddedRecords { get; }
		public IReadOnlyList<ImpactEffectDiagnosticRecord> SoundRecords { get; }

		public ImpactEffectTraceEvidence(bool valid, string detail,
			IReadOnlyList<ImpactEffectDiagnosticRecord> spriteRecords,
			IReadOnlyList<ImpactEffectDiagnosticRecord> spriteAddedRecords,
			IReadOnlyList<ImpactEffectDiagnosticRecord> soundRecords)
		{
			Valid = valid;
			Detail = detail;
			SpriteRecords = spriteRecords;
			SpriteAddedRecords = spriteAddedRecords;
			SoundRecords = soundRecords;
		}
	}

	public readonly struct ImpactEffectsRuntimeTraceWatermark
	{
		public long Sequence { get; }
		public long DroppedEvents { get; }

		public ImpactEffectsRuntimeTraceWatermark(long sequence, long droppedEvents)
		{
			Sequence = sequence;
			DroppedEvents = droppedEvents;
		}
	}

	public static class ImpactEffectsRuntimeEvidence
	{
		const string SpriteScheduledPrefix = "ImpactEffectAudit.SpriteScheduled|";
		const string SpriteAddedPrefix = "ImpactEffectAudit.SpriteAdded|";
		const string SoundPrefix = "ImpactEffectAudit.Sound|";

		public static long PackHorizontalPosition(WPos position) =>
			unchecked((long)(((ulong)(uint)position.X << 32) | (uint)position.Y));

		public static ImpactEffectsRuntimeTraceWatermark CaptureWatermark(DiagnosticTraceSnapshot snapshot)
		{
			if (snapshot == null || !snapshot.Available || snapshot.Events == null)
				throw new InvalidDataException("Diagnostic trace watermark is unavailable.");

			var sequence = snapshot.Events.Length == 0 ? 0 : snapshot.Events[^1].Sequence;
			return new ImpactEffectsRuntimeTraceWatermark(sequence, snapshot.DroppedEvents);
		}

		public static bool HasNoNewDroppedEvents(DiagnosticTraceSnapshot snapshot,
			ImpactEffectsRuntimeTraceWatermark watermark) =>
			snapshot != null && snapshot.Available && snapshot.Events != null &&
			snapshot.DroppedEvents == watermark.DroppedEvents;

		public static ImpactEffectTraceEvidence Collect(DiagnosticTraceSnapshot snapshot,
			long afterSequence, WPos target, int impactTick,
			ImpactEffectsRuntimeExpectation expectation, ImpactEffectsRuntimeTerrain terrain) =>
			Collect(snapshot, new ImpactEffectsRuntimeTraceWatermark(afterSequence, 0),
				target, impactTick, expectation, terrain);

		public static ImpactEffectTraceEvidence Collect(DiagnosticTraceSnapshot snapshot,
			ImpactEffectsRuntimeTraceWatermark watermark, WPos target, int impactTick,
			ImpactEffectsRuntimeExpectation expectation, ImpactEffectsRuntimeTerrain terrain)
		{
			var sprites = new List<ImpactEffectDiagnosticRecord>();
			var spriteAdded = new List<ImpactEffectDiagnosticRecord>();
			var sounds = new List<ImpactEffectDiagnosticRecord>();
			if (snapshot == null || !snapshot.Available || snapshot.Events == null)
				return new ImpactEffectTraceEvidence(false, "Diagnostic trace snapshot is unavailable.",
					sprites, spriteAdded, sounds);

			if (!HasNoNewDroppedEvents(snapshot, watermark))
				return new ImpactEffectTraceEvidence(false,
					$"Diagnostic trace dropped-event counter changed from {watermark.DroppedEvents} " +
					$"to {snapshot.DroppedEvents}.", sprites, spriteAdded, sounds);

			var packed = PackHorizontalPosition(target);
			foreach (var trace in snapshot.Events.Where(e => e.Sequence > watermark.Sequence &&
				e.Phase == DiagnosticTracePhase.Instant && e.Arg1 == packed))
			{
				var tickOffset = checked((int)(trace.Arg0 - impactTick));
				if (trace.Name.StartsWith(SpriteScheduledPrefix, StringComparison.Ordinal))
				{
					var fields = trace.Name[SpriteScheduledPrefix.Length..].Split('|');
					if (fields.Length == 3 && long.TryParse(fields[0], NumberStyles.None,
						CultureInfo.InvariantCulture, out var correlationId))
						sprites.Add(new ImpactEffectDiagnosticRecord(ImpactEffectDiagnosticKind.SpriteScheduled,
							correlationId, fields[1], fields[2], tickOffset, false, false));
				}
				else if (trace.Name.StartsWith(SpriteAddedPrefix, StringComparison.Ordinal))
				{
					var fields = trace.Name[SpriteAddedPrefix.Length..].Split('|');
					if (fields.Length == 3 && long.TryParse(fields[0], NumberStyles.None,
						CultureInfo.InvariantCulture, out var correlationId))
						spriteAdded.Add(new ImpactEffectDiagnosticRecord(ImpactEffectDiagnosticKind.SpriteAdded,
							correlationId, fields[1], fields[2], tickOffset, false, false));
				}
				else if (trace.Name.StartsWith(SoundPrefix, StringComparison.Ordinal))
				{
					var fields = trace.Name[SoundPrefix.Length..].Split('|');
					if (fields.Length == 4 && long.TryParse(fields[0], NumberStyles.None,
						CultureInfo.InvariantCulture, out var correlationId))
						sounds.Add(new ImpactEffectDiagnosticRecord(ImpactEffectDiagnosticKind.Sound,
							correlationId, string.Empty, fields[1], tickOffset,
							string.Equals(fields[2], "played", StringComparison.Ordinal),
							string.Equals(fields[3], "dummy", StringComparison.Ordinal)));
				}
			}

			var expectedLayers = expectation.LayersFor(terrain);
			var expectedSounds = expectedLayers
				.SelectMany(layer => layer.ImpactSounds.Select(sound => (Sound: sound, layer.Delay)))
				.ToArray();
			if (sounds.Count != expectedSounds.Length)
				return new ImpactEffectTraceEvidence(false,
					$"Expected {expectedSounds.Length} terrain sound trace but observed {sounds.Count}.",
					sprites, spriteAdded, sounds);

			for (var i = 0; i < expectedSounds.Length; i++)
			{
				var expected = expectedSounds[i];
				var actual = sounds[i];
				if (actual.Asset != expected.Sound || actual.TickOffset != expected.Delay || !actual.PlaybackReturned)
					return new ImpactEffectTraceEvidence(false,
						$"Terrain sound mismatch: expected {expected.Sound}@{expected.Delay} with a non-null playback result, " +
						$"observed {actual.Asset}@{actual.TickOffset} playback={actual.PlaybackReturned}.",
						sprites, spriteAdded, sounds);
			}

			if (sprites.Count != expectedLayers.Count)
				return new ImpactEffectTraceEvidence(false,
					$"Expected {expectedLayers.Count} sprite traces but observed {sprites.Count}.",
					sprites, spriteAdded, sounds);

			var unmatched = sprites.ToList();
			foreach (var expected in expectedLayers)
			{
				var index = unmatched.FindIndex(actual => actual.Image == expected.Image &&
					actual.Asset == expected.Sequence && actual.TickOffset == expected.Delay);
				if (index < 0)
					return new ImpactEffectTraceEvidence(false,
						$"Missing sprite trace {expected.Image}|{expected.Sequence}@{expected.Delay}.",
						sprites, spriteAdded, sounds);

				unmatched.RemoveAt(index);
			}

			if (!ImpactEffectsRuntimeCorrelationEvidence.Validate(
				sprites.Concat(spriteAdded), expectedLayers.Count, out var correlationDetail))
				return new ImpactEffectTraceEvidence(false, correlationDetail, sprites, spriteAdded, sounds);

			return new ImpactEffectTraceEvidence(true, string.Empty, sprites, spriteAdded, sounds);
		}

		public static double Percentile95(IEnumerable<double> values)
		{
			var ordered = values.OrderBy(value => value).ToArray();
			if (ordered.Length == 0)
				return 0;

			var index = Math.Max(0, (int)Math.Ceiling(0.95 * ordered.Length) - 1);
			return ordered[index];
		}
	}

	public readonly struct ImpactEffectsRuntimePerformanceSample
	{
		public int Tick { get; }
		public double Render { get; }
		public double WorldTick { get; }
		public double RenderPrepare { get; }
		public double RenderFlip { get; }
		public double Batches { get; }

		public ImpactEffectsRuntimePerformanceSample(int tick, double render, double worldTick,
			double renderPrepare, double renderFlip, double batches)
		{
			Tick = tick;
			Render = render;
			WorldTick = worldTick;
			RenderPrepare = renderPrepare;
			RenderFlip = renderFlip;
			Batches = batches;
		}
	}

	sealed class ImpactEffectsRuntimeAuditSession : IDisposable
	{
		const int EvidenceTick = 5;
		const int ScreenshotCropSize = 128;
		static readonly string[] PerformanceMetrics =
		{
			"render", "world_tick", "render_prepare", "render_flip", "batches"
		};
		static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly ImpactEffectsRuntimeAuditConfiguration config;
		readonly ImpactEffectsRuntimeAuditCase[] cases;
		readonly string outputDirectory;
		readonly string screenshotDirectory;
		readonly string baselineDirectory;
		readonly FileStream progressStream;
		readonly StreamWriter progressWriter;
		readonly FileStream casesStream;
		readonly StreamWriter casesWriter;
		readonly FileStream effectsStream;
		readonly StreamWriter effectsWriter;
		readonly FileStream performanceStream;
		readonly StreamWriter performanceWriter;
		readonly Dictionary<string, string> emptyTargetSignatures = new(StringComparer.Ordinal);
		readonly List<ImpactEffectsRuntimePerformanceSample> performanceSamples = new();
		readonly List<BurstProgress> bufferedBurstProgress = new();
		readonly ImpactEffectsRuntimeTerminalState terminal = new();

		ImpactEffectsRuntimeAuditCase currentCase;
		ImpactEffectsRuntimeAuditStateMachine state;
		ImpactEffectTraceEvidence currentTraceEvidence;
		WeaponInfo currentWeapon;
		Actor sourceActor;
		Actor targetActor;
		Target currentTarget;
		CPos landCell;
		CPos waterCell;
		WPos expectedGroundPosition;
		int impactTick;
		int impactRenderFrame;
		int screenshotRequestFrame;
		int baselineRequestFrame;
		int2 viewportCenterAtImpact;
		int2 baselineCropCenter;
		int2 screenshotCropCenter;
		ImpactEffectsRuntimeTraceWatermark traceWatermark;
		long registryWatermark;
		string screenshotPath;
		string baselinePath;
		string pendingFailure;
		string screenshotDecodeDetail;
		ImpactEffectsRuntimeScreenshotReadiness baselineReadiness;
		ImpactEffectsRuntimeScreenshotReadiness screenshotReadiness;
		ImpactEffectsRuntimeDecodedScreenshot baselineScreenshot;
		ImpactEffectAuditRegistrySnapshot currentRegistrySnapshot;
		ImpactEffectsRuntimeSpriteLifecycleEvidence caseSpriteLifecycle;
		readonly HashSet<long> caseAudioObserved = new();
		string caseAudioEvidence = string.Empty;
		uint[] impactOccupancyActorIds = Array.Empty<uint>();
		int screenshotChangedPixels;
		bool preflightComplete;
		bool scenarioReady;
		bool shakeObserved;
		bool renderObserved;
		bool positionExact;
		bool visibilityObserved;
		bool actorAliveObserved;
		bool screenshotObserved;
		bool writersDisposed;
		int currentIndex;
		int passed;
		int failed;
		int screenshots;
		int caseSpritePeak;

		bool burstStarted;
		bool burstComplete;
		int burstStartTick;
		int burstFirstImpactTick = -1;
		int burstLastImpactTick;
		int burstImpacts;
		int burstSpriteSchedules;
		int burstSpriteAdded;
		int burstSpriteRendered;
		int burstSpriteCompleted;
		int burstTargetPeak;
		int burstGlobalPeak;
		int burstCleanupTicks;
		ImpactEffectsRuntimeTraceWatermark burstTraceWatermark;
		long burstRegistryWatermark;
		WPos burstPosition;
		readonly List<int> burstImpactTicks = new();
		ImpactEffectAuditRegistrySnapshot burstRegistrySnapshot;
		readonly ImpactEffectsRuntimeSpriteLifecycleEvidence burstSpriteLifecycle = new(64);
		readonly HashSet<long> burstAudioObserved = new();
		bool burstEvidenceValidated;
		bool performanceWritten;

		readonly struct BurstProgress
		{
			public readonly int Index;
			public readonly int Tick;
			public readonly bool Elite;

			public BurstProgress(int index, int tick, bool elite)
			{
				Index = index;
				Tick = tick;
				Elite = elite;
			}
		}

		ImpactEffectsRuntimeAuditSession(World world, WorldRenderer worldRenderer,
			ImpactEffectsRuntimeAuditConfiguration config)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			this.config = config;
			cases = config.BuildCases().ToArray();
			outputDirectory = Path.Combine(Platform.SupportDir, "Logs", "ImpactEffectsRuntimeAudit", config.RunId);
			screenshotDirectory = Path.Combine(outputDirectory, "screenshots");
			baselineDirectory = Path.Combine(outputDirectory, "baselines");
			Directory.CreateDirectory(screenshotDirectory);
			Directory.CreateDirectory(baselineDirectory);

			progressStream = OpenNew(Path.Combine(outputDirectory, "progress.log"));
			progressWriter = new StreamWriter(progressStream, new UTF8Encoding(false)) { AutoFlush = true };
			casesStream = OpenNew(Path.Combine(outputDirectory, "cases.csv"));
			casesWriter = new StreamWriter(casesStream, new UTF8Encoding(false)) { AutoFlush = true };
			effectsStream = OpenNew(Path.Combine(outputDirectory, "effects.csv"));
			effectsWriter = new StreamWriter(effectsStream, new UTF8Encoding(false)) { AutoFlush = true };
			performanceStream = OpenNew(Path.Combine(outputDirectory, "performance.csv"));
			performanceWriter = new StreamWriter(performanceStream, new UTF8Encoding(false)) { AutoFlush = true };
			WriteDurable(casesWriter, casesStream,
				"case,weapon,terrain,target,result,ticks,sprites,sprites_added,sprites_rendered," +
				"sprites_completed,peak_sprites,sounds,audio_evidence,shake,render_nonempty," +
				"position_exact,visible,actor_alive,occupancy_count,occupancy_actor_ids," +
				"screenshot,screenshot_changed_pixels,detail");
			WriteDurable(effectsWriter, effectsStream,
				"scope,case,weapon,kind,correlation_id,tick_offset,image,asset," +
				"playback_returned,dummy_audio");
			WriteDurable(performanceWriter, performanceStream,
				"kind,tick,render,world_tick,render_prepare,render_flip,batches");
			RecordProgress("audit-start", $"cases={cases.Length} burst={config.BurstImpactCount}");
			WriteSummary(false, "RUNNING", string.Empty);
		}

		public static ImpactEffectsRuntimeAuditSession TryCreate(World world, WorldRenderer worldRenderer,
			ImpactEffectsRuntimeAuditConfiguration config)
		{
			if (!config.Enabled || !ImpactEffectsRuntimeAuditConfiguration.ShouldStartInWorld(world.Type))
				return null;

			return new ImpactEffectsRuntimeAuditSession(world, worldRenderer, config);
		}

		public void Tick()
		{
			if (terminal.Completed || terminal.Finishing)
				return;

			try
			{
				if (!preflightComplete)
				{
					RunPreflight();
					return;
				}

				if (currentIndex < cases.Length)
					TickCase();
				else
					TickBurst();
			}
			catch (Exception e)
			{
				CompleteFailed($"{e.GetType().Name}: {e.Message}");
			}
		}

		public void AbortForWorldDisposal()
		{
			if (terminal.Completed || terminal.Finishing)
				return;

			CompleteFailed("World actor disposed before the impact runtime audit reached a terminal summary.");
		}

		void RunPreflight()
		{
			if (world.RenderPlayer == null)
				throw new InvalidOperationException("A non-null RenderPlayer is required for visibility evidence.");

			var localPlayer = world.LocalPlayer ?? world.Players.FirstOrDefault(player => player.Playable && !player.NonCombatant);
			if (localPlayer == null)
				throw new InvalidOperationException("A playable local player is required.");
			if (!ImpactEffectAuditRegistry.IsEnabled)
				throw new InvalidOperationException("Impact effect correlation registry is not enabled.");

			sourceActor = localPlayer.PlayerActor;
			landCell = FindAuditCell(ImpactEffectsRuntimeTerrain.Land, "apoc");
			waterCell = FindAuditCell(ImpactEffectsRuntimeTerrain.Water, "dred");
			var missingRetailAssets = new List<string>();
			var decodedSounds = new HashSet<string>(StringComparer.Ordinal);
			foreach (var expectation in ImpactEffectsRuntimeExpectation.All())
			{
				if (!world.Map.Rules.Weapons.TryGetValue(expectation.WeaponName.ToLowerInvariant(), out var weapon))
					throw new InvalidOperationException($"Resolved weapon `{expectation.WeaponName}` is missing.");

				ValidateResolvedWeapon(expectation, weapon);
				foreach (var layer in new[] { ImpactEffectsRuntimeTerrain.Land, ImpactEffectsRuntimeTerrain.Water }
					.SelectMany(expectation.LayersFor).DistinctBy(item => (item.Image, item.Sequence)))
				{
					bool sequenceDeclared;
					try
					{
						sequenceDeclared = world.Map.Sequences.HasSequence(layer.Image, layer.Sequence);
					}
					catch (Exception e)
					{
						throw new InvalidDataException(
							$"Sequence declaration lookup failed for `{layer.Image}:{layer.Sequence}`.", e);
					}

					if (!sequenceDeclared)
						throw new InvalidDataException(
							$"Sequence `{layer.Image}:{layer.Sequence}` is not declared.");

					try
					{
						if (world.Map.Sequences.GetSequence(layer.Image, layer.Sequence).GetSprite(0) == null)
							throw new InvalidDataException(
								$"Sequence `{layer.Image}:{layer.Sequence}` resolved a null sprite.");
					}
					catch (FileNotFoundException e) when (
						ImpactEffectsRuntimeAssetPolicy.ClassifySequenceFailure(
							layer.Image, sequenceDeclared, e) == ImpactEffectsRuntimeAssetFailure.BlockedRetailAssets)
					{
						missingRetailAssets.Add($"sequence:{layer.Image}:{layer.Sequence}:{e.Message}");
					}
					catch (Exception e)
					{
						throw new InvalidDataException(
							$"Sequence `{layer.Image}:{layer.Sequence}` failed to resolve.", e);
					}
				}

				foreach (var sound in new[] { ImpactEffectsRuntimeTerrain.Land, ImpactEffectsRuntimeTerrain.Water }
					.SelectMany(expectation.LayersFor).SelectMany(layer => layer.ImpactSounds).Distinct())
				{
					if (!decodedSounds.Add(sound))
						continue;

					var soundExists = Game.ModData.DefaultFileSystem.Exists(sound);
					if (ImpactEffectsRuntimeAssetPolicy.ClassifySoundFilePresence(soundExists) ==
						ImpactEffectsRuntimeAssetFailure.BlockedRetailAssets)
					{
						missingRetailAssets.Add("sound:" + sound);
						continue;
					}

					using var stream = Game.ModData.DefaultFileSystem.Open(sound);
					ImpactEffectsRuntimeSoundDecoder.Validate(stream, Game.ModData.SoundLoaders, sound);
				}
			}

			if (missingRetailAssets.Count > 0)
			{
				var blockedPath = Path.Combine(outputDirectory, "blocked-retail-assets.txt");
				WriteAtomic(blockedPath, string.Join("\n", missingRetailAssets.Distinct()) + "\n");
				CompleteBlocked("Player-imported retail sequences or sounds are unavailable.");
				return;
			}

			WriteResolvedWeapons();
			preflightComplete = true;
			RecordProgress("preflight-passed", $"land={landCell} water={waterCell}");
		}

		void ValidateResolvedWeapon(ImpactEffectsRuntimeExpectation expectation, WeaponInfo weapon)
		{
			var effects = weapon.Warheads.OfType<CreateEffectWarhead>().ToArray();
			if (effects.Length != 6 || effects.Any(effect => effect.Explosions.Length != 1 ||
				effect.ImpactActors || !effect.ForceDisplayAtGroundLevel || effect.ImpactSoundChance != 100))
				throw new InvalidDataException(
					$"{expectation.WeaponName} must resolve to six single-sequence, ground-forced, " +
					"ImpactActors:false, ImpactSoundChance:100 effects.");

			foreach (var terrain in new[] { ImpactEffectsRuntimeTerrain.Land, ImpactEffectsRuntimeTerrain.Water })
			{
				var terrainName = terrain == ImpactEffectsRuntimeTerrain.Land ? "Ground" : "Water";
				var resolved = effects.Where(effect => effect.ValidTargets.Contains(terrainName)).ToArray();
				var expected = expectation.LayersFor(terrain);
				if (resolved.Length != 4)
					throw new InvalidDataException(
						$"{expectation.WeaponName} {terrain} resolved {resolved.Length} sprite layers instead of four.");

				foreach (var layer in expected)
				{
					var matches = resolved.Count(effect => effect.Image == layer.Image &&
						effect.Explosions.SequenceEqual(new[] { layer.Sequence }) &&
						effect.Delay == layer.Delay && effect.ImpactSounds.SequenceEqual(layer.ImpactSounds));
					if (matches != 1)
						throw new InvalidDataException(
							$"{expectation.WeaponName} {terrain} did not uniquely resolve " +
							$"{layer.Image}:{layer.Sequence}@{layer.Delay}.");
				}

				if (resolved.Sum(effect => effect.ImpactSounds.Length) != 1)
					throw new InvalidDataException(
						$"{expectation.WeaponName} {terrain} must resolve exactly one terrain sound.");
			}

			var shakes = weapon.Warheads.OfType<ShakeScreenWarhead>().ToArray();
			if (shakes.Length != 1 || shakes[0].Duration != expectation.ShakeDuration ||
				shakes[0].Intensity != expectation.ShakeIntensity || shakes[0].Multiplier != new float2(1, 1))
				throw new InvalidDataException($"{expectation.WeaponName} resolved shake settings do not match the contract.");
		}

		CPos FindAuditCell(ImpactEffectsRuntimeTerrain terrain, string actorName)
		{
			if (!world.Map.Rules.Actors.TryGetValue(actorName, out var actorInfo))
				throw new InvalidOperationException($"Audit target actor `{actorName}` is unavailable.");

			var positionable = actorInfo.TraitInfo<IPositionableInfo>();
			var allCells = world.Map.AllCells.ToArray();
			var minX = allCells.Min(cell => cell.X);
			var maxX = allCells.Max(cell => cell.X);
			var minY = allCells.Min(cell => cell.Y);
			var maxY = allCells.Max(cell => cell.Y);
			var centerX = (minX + maxX) / 2;
			var centerY = (minY + maxY) / 2;
			foreach (var margin in new[] { 10, 6, 2 })
				foreach (var cell in allCells
					.Where(cell => cell.X >= minX + margin && cell.X <= maxX - margin &&
						cell.Y >= minY + margin && cell.Y <= maxY - margin)
					.OrderBy(cell => Math.Abs(cell.X - centerX) + Math.Abs(cell.Y - centerY))
					.ThenBy(cell => cell.Y).ThenBy(cell => cell.X))
				{
					var terrainName = terrain == ImpactEffectsRuntimeTerrain.Land ? "Ground" : "Water";
					if (!world.Map.GetTerrainInfo(cell).TargetTypes.Contains(terrainName))
						continue;

					var subCell = positionable.SharesCell ? world.ActorMap.FreeSubCell(cell) : SubCell.FullCell;
					if (subCell == SubCell.Invalid || !positionable.CanEnterCell(
						world, null, cell, subCell, check: BlockedByActor.All))
						continue;

					var position = world.Map.CenterOfCell(cell);
					if (world.FindActorsInCircle(position, WDist.FromCells(5))
						.Any(actor => actor.IsInWorld && actor.OccupiesSpace != null))
						continue;

					return cell;
				}

			throw new InvalidOperationException($"No isolated interior {terrain} cell supports `{actorName}`.");
		}

		void TickCase()
		{
			if (currentCase == null)
			{
				BeginCase(cases[currentIndex]);
				return;
			}

			state.Tick();
			if (pendingFailure != null)
			{
				state.Fail(pendingFailure);
				pendingFailure = null;
			}

			if (state.Phase == ImpactEffectsRuntimeAuditPhase.Failed)
			{
				if (!string.IsNullOrEmpty(screenshotDecodeDetail))
					state.Fail(state.Detail + " Last screenshot decode error: " + screenshotDecodeDetail);

				FinishFailedCase();
				return;
			}

			switch (state.Phase)
			{
				case ImpactEffectsRuntimeAuditPhase.Prepare:
					if (scenarioReady)
						state.ScenarioReady();
					break;
				case ImpactEffectsRuntimeAuditPhase.WaitForBaseline:
					WaitForBaseline();
					break;
				case ImpactEffectsRuntimeAuditPhase.Impact:
					ImpactCurrentCase();
					break;
				case ImpactEffectsRuntimeAuditPhase.Observe:
					ObserveCurrentCase();
					break;
				case ImpactEffectsRuntimeAuditPhase.WaitForScreenshot:
					WaitForScreenshot();
					break;
				case ImpactEffectsRuntimeAuditPhase.Cleanup:
					WaitForCaseCleanup();
					break;
				case ImpactEffectsRuntimeAuditPhase.Passed:
					FinishPassedCase();
					break;
			}
		}

		void BeginCase(ImpactEffectsRuntimeAuditCase auditCase)
		{
			currentCase = auditCase;
			state = new ImpactEffectsRuntimeAuditStateMachine(config.TimeoutTicks);
			currentTraceEvidence = null;
			currentWeapon = world.Map.Rules.Weapons[auditCase.WeaponName.ToLowerInvariant()];
			targetActor = null;
			pendingFailure = null;
			screenshotDecodeDetail = string.Empty;
			scenarioReady = false;
			shakeObserved = false;
			renderObserved = false;
			positionExact = false;
			visibilityObserved = false;
			actorAliveObserved = !auditCase.HasActor;
			screenshotObserved = false;
			screenshotChangedPixels = 0;
			impactOccupancyActorIds = Array.Empty<uint>();
			baselineReadiness = null;
			screenshotReadiness = null;
			baselineScreenshot = null;
			currentRegistrySnapshot = null;
			caseSpriteLifecycle = new ImpactEffectsRuntimeSpriteLifecycleEvidence(4);
			caseAudioObserved.Clear();
			caseAudioEvidence = string.Empty;
			caseSpritePeak = 0;
			RecordProgress("case-start", auditCase.Id);

			world.AddFrameEndTask(_ =>
			{
				try
				{
					var cell = auditCase.Terrain == ImpactEffectsRuntimeTerrain.Land ? landCell : waterCell;
					if (auditCase.HasActor)
					{
						targetActor = world.CreateActor(auditCase.TargetActorName,
							Initializers(auditCase.TargetActorName, sourceActor.Owner, cell));
						currentTarget = Target.FromActor(targetActor);
					}
					else
						currentTarget = Target.FromPos(world.Map.CenterOfCell(cell));

					expectedGroundPosition = GroundPosition(currentTarget.CenterPosition);
					worldRenderer.Viewport.Center(expectedGroundPosition);
					scenarioReady = true;
				}
				catch (Exception e)
				{
					pendingFailure = $"scenario exception: {e.GetType().Name}: {e.Message}";
				}
			});
		}

		void WaitForBaseline()
		{
			if (baselineReadiness == null)
			{
				worldRenderer.Viewport.Center(expectedGroundPosition);
				baselineCropCenter = worldRenderer.Viewport.WorldToViewPx(
					worldRenderer.ScreenPxPosition(expectedGroundPosition));
				baselinePath = Path.Combine(baselineDirectory, currentCase.Id + ".png");
				baselineRequestFrame = Game.RenderFrame;
				baselineReadiness = new ImpactEffectsRuntimeScreenshotReadiness(baselineRequestFrame);
				Game.Renderer.SaveScreenshot(baselinePath);
				RecordProgress("baseline-requested", currentCase.Id);
				return;
			}

			if (!TryReadStableScreenshot(
				baselinePath, baselineReadiness, baselineCropCenter, out var decoded))
				return;

			baselineScreenshot = decoded;
			state.BaselineReady();
			RecordProgress("baseline-ready", currentCase.Id);
		}

		void ImpactCurrentCase()
		{
			if (world.RenderPlayer == null || world.FogObscures(expectedGroundPosition) ||
				world.ShroudObscures(expectedGroundPosition))
			{
				state.Fail("Impact cell is not visible to the RenderPlayer.");
				return;
			}

			worldRenderer.Viewport.Center(expectedGroundPosition);
			viewportCenterAtImpact = worldRenderer.Viewport.CenterLocation;
			var occupancyPosition = currentCase.HasActor ? targetActor.CenterPosition : currentTarget.CenterPosition;
			impactOccupancyActorIds = OverlappingActorIds(occupancyPosition);
			if (!ImpactEffectsRuntimeOccupancyEvidence.Validate(currentCase.HasActor,
				targetActor?.ActorID ?? 0, impactOccupancyActorIds, out var occupancyDetail))
			{
				state.Fail(occupancyDetail);
				return;
			}

			traceWatermark = ImpactEffectsRuntimeEvidence.CaptureWatermark(DiagnosticTrace.Snapshot());
			registryWatermark = ImpactEffectAuditRegistry.LatestCorrelationId;
			impactTick = Game.LocalTick;
			impactRenderFrame = Game.RenderFrame;
			if (currentCase.HasActor)
				currentWeapon.Impact(Target.FromActor(targetActor), sourceActor);
			else
				currentWeapon.Impact(Target.FromPos(expectedGroundPosition), sourceActor);

			state.Impacted();
			RecordProgress("weapon-impact",
				$"{currentCase.Id} weapon={currentCase.WeaponName} tick={impactTick} " +
				$"pos={Format(expectedGroundPosition)} occupancy={impactOccupancyActorIds.Length} " +
				$"actors={string.Join(';', impactOccupancyActorIds)}");
		}

		void ObserveCurrentCase()
		{
			if (targetActor != null && (!targetActor.IsInWorld || targetActor.Disposed || targetActor.WillDispose))
			{
				state.Fail("Actor target did not survive the single audit impact.");
				return;
			}

			actorAliveObserved = targetActor == null || targetActor.IsInWorld;
			shakeObserved |= worldRenderer.Viewport.CenterLocation != viewportCenterAtImpact;
			currentRegistrySnapshot = ImpactEffectAuditRegistry.Snapshot(
				registryWatermark, expectedGroundPosition);
			var activeSprites = 0;
			foreach (var registration in currentRegistrySnapshot.Sprites)
			{
				if (registration.Effect == null || registration.AddedTick < 0)
					continue;

				caseSpriteLifecycle.ObserveAdded(registration.CorrelationId);
				if (!world.Effects.Contains(registration.Effect))
					continue;

				activeSprites++;
				var renderables = registration.Effect.Render(worldRenderer).ToArray();
				if (renderables.Length == 0)
					continue;

				if (renderables.Any(renderable => renderable.Pos != expectedGroundPosition))
				{
					state.Fail($"Correlated sprite {registration.CorrelationId} rendered away from the exact impact position.");
					return;
				}

				caseSpriteLifecycle.ObserveRendered(registration.CorrelationId);
			}

			caseSpritePeak = Math.Max(caseSpritePeak, activeSprites);
			renderObserved = caseSpriteLifecycle.Rendered == 4;
			positionExact = renderObserved;
			if (!ObserveAudioRegistrations(currentRegistrySnapshot.Sounds, caseAudioObserved,
				value => caseAudioEvidence = value, out var audioFailure))
			{
				state.Fail(audioFailure);
				return;
			}

			visibilityObserved = world.RenderPlayer != null && !world.FogObscures(expectedGroundPosition) &&
				!world.ShroudObscures(expectedGroundPosition);
			if (Game.LocalTick - impactTick < EvidenceTick)
				return;

			currentTraceEvidence = ImpactEffectsRuntimeEvidence.Collect(DiagnosticTrace.Snapshot(),
				traceWatermark, expectedGroundPosition, impactTick, currentCase.Expectation, currentCase.Terrain);
			if (!currentTraceEvidence.Valid)
			{
				state.Fail(currentTraceEvidence.Detail);
				return;
			}

			if (currentRegistrySnapshot.Sprites.Count != 4 || caseSpriteLifecycle.Added != 4 ||
				caseSpriteLifecycle.Rendered != 4)
			{
				state.Fail($"Expected four correlated sprites to be actually added and rendered; " +
					$"registry={currentRegistrySnapshot.Sprites.Count} added={caseSpriteLifecycle.Added} " +
					$"rendered={caseSpriteLifecycle.Rendered} peak={caseSpritePeak}.");
				return;
			}

			var registrySpriteIds = currentRegistrySnapshot.Sprites
				.Select(registration => registration.CorrelationId).OrderBy(id => id).ToArray();
			var traceSpriteIds = currentTraceEvidence.SpriteAddedRecords
				.Select(record => record.CorrelationId).OrderBy(id => id).ToArray();
			if (!registrySpriteIds.SequenceEqual(traceSpriteIds))
			{
				state.Fail("Trace and actual-added sprite correlation IDs do not match.");
				return;
			}

			if (currentRegistrySnapshot.Sounds.Count != 1 || caseAudioObserved.Count != 1 ||
				currentTraceEvidence.SoundRecords.Count != 1 ||
				currentRegistrySnapshot.Sounds[0].CorrelationId !=
				currentTraceEvidence.SoundRecords[0].CorrelationId)
			{
				state.Fail($"Expected one correlated terrain sound with observable backend evidence; " +
					$"registry={currentRegistrySnapshot.Sounds.Count} observed={caseAudioObserved.Count} " +
					$"trace={currentTraceEvidence.SoundRecords.Count}.");
				return;
			}

			if (!renderObserved || !positionExact || !visibilityObserved || !shakeObserved || !actorAliveObserved)
			{
				state.Fail($"Runtime evidence incomplete: render={renderObserved} position={positionExact} " +
					$"visible={visibilityObserved} shake={shakeObserved} actor={actorAliveObserved}.");
				return;
			}

			if (Game.RenderFrame <= impactRenderFrame)
				return;

			WriteEffectEvidence("case", currentCase.Id, currentCase.WeaponName, currentTraceEvidence);
			screenshotPath = Path.Combine(screenshotDirectory, currentCase.Id + ".png");
			screenshotCropCenter = worldRenderer.Viewport.WorldToViewPx(
				worldRenderer.ScreenPxPosition(expectedGroundPosition));
			screenshotRequestFrame = Game.RenderFrame;
			screenshotReadiness = new ImpactEffectsRuntimeScreenshotReadiness(screenshotRequestFrame);
			Game.Renderer.SaveScreenshot(screenshotPath);
			state.EvidenceReady();
			RecordProgress("screenshot-requested", currentCase.Id);
		}

		void WaitForScreenshot()
		{
			shakeObserved |= worldRenderer.Viewport.CenterLocation != viewportCenterAtImpact;
			if (!TryReadStableScreenshot(
				screenshotPath, screenshotReadiness, screenshotCropCenter, out var decoded))
				return;

			screenshotChangedPixels = ImpactEffectsRuntimeScreenshotEvidence.CountChangedPixels(
				baselineScreenshot, decoded);
			if (screenshotChangedPixels <= 0)
			{
				state.Fail("Post-impact target crop is pixel-identical to its pre-impact baseline.");
				return;
			}

			screenshotObserved = true;
			screenshots++;
			state.ScreenshotReady();
			RecordProgress("screenshot-ready",
				$"{currentCase.Id} changed_pixels={screenshotChangedPixels}");
		}

		void WaitForCaseCleanup()
		{
			currentRegistrySnapshot = ImpactEffectAuditRegistry.Snapshot(
				registryWatermark, expectedGroundPosition);
			foreach (var registration in currentRegistrySnapshot.Sprites)
			{
				if (registration.Effect == null || world.Effects.Contains(registration.Effect))
					return;

				caseSpriteLifecycle.ObserveCompleted(registration.CorrelationId);
			}

			if (!caseSpriteLifecycle.Validate(out var lifecycleDetail))
			{
				state.Fail(lifecycleDetail);
				FinishFailedCase();
				return;
			}

			state.Cleaned();
			FinishPassedCase();
		}

		void FinishPassedCase()
		{
			var signature = string.Join(";", currentTraceEvidence.SpriteRecords
				.Select(record => $"{record.Image}|{record.Asset}|{record.TickOffset}")
				.OrderBy(value => value, StringComparer.Ordinal));
			var parityKey = $"{currentCase.WeaponName}:{currentCase.Terrain}";
			if (!currentCase.HasActor)
				emptyTargetSignatures[parityKey] = signature;
			else if (!emptyTargetSignatures.TryGetValue(parityKey, out var emptySignature) || emptySignature != signature)
			{
				state.Fail("Actor and empty-target layer signatures differ; ImpactActors:false behavior was not preserved.");
				FinishFailedCase();
				return;
			}

			passed++;
			WriteCaseResult("PASSED", string.Empty);
			RecordProgress("case-finished", $"{currentCase.Id} result=PASSED");
			DisposeActor(targetActor);
			currentIndex++;
			WriteSummary(false, "RUNNING", string.Empty);
			ResetCase();
		}

		void FinishFailedCase()
		{
			failed++;
			WriteCaseResult("FAILED", state.Detail);
			RecordProgress("case-finished", $"{currentCase.Id} result=FAILED detail={state.Detail}");
			DisposeActor(targetActor);
			currentIndex++;
			var detail = state.Detail;
			ResetCase();
			CompleteFailed(detail);
		}

		void WriteCaseResult(string result, string detail)
		{
			var soundRecords = currentTraceEvidence?.SoundRecords ?? Array.Empty<ImpactEffectDiagnosticRecord>();
			var spriteCount = currentTraceEvidence?.SpriteRecords.Count ?? 0;
			WriteDurable(casesWriter, casesStream,
				$"{Csv(currentCase.Id)},{Csv(currentCase.WeaponName)},{currentCase.Terrain}," +
				$"{(currentCase.HasActor ? "actor" : "empty")},{result},{state.Ticks},{spriteCount}," +
				$"{caseSpriteLifecycle?.Added ?? 0},{caseSpriteLifecycle?.Rendered ?? 0}," +
				$"{caseSpriteLifecycle?.Completed ?? 0},{caseSpritePeak},{soundRecords.Count}," +
				$"{Csv(caseAudioEvidence)},{shakeObserved.ToString().ToLowerInvariant()}," +
				$"{renderObserved.ToString().ToLowerInvariant()},{positionExact.ToString().ToLowerInvariant()}," +
				$"{visibilityObserved.ToString().ToLowerInvariant()},{actorAliveObserved.ToString().ToLowerInvariant()}," +
				$"{impactOccupancyActorIds.Length},{Csv(string.Join(';', impactOccupancyActorIds))}," +
				$"{screenshotObserved.ToString().ToLowerInvariant()},{screenshotChangedPixels},{Csv(detail)}");
		}

		void ResetCase()
		{
			currentCase = null;
			state = null;
			currentTraceEvidence = null;
			currentWeapon = null;
			targetActor = null;
			currentRegistrySnapshot = null;
		}

		void TickBurst()
		{
			if (!burstStarted)
			{
				StartBurst();
				return;
			}

			var relativeTick = Game.LocalTick - burstStartTick;
			if (config.IsBurstTimedOut(relativeTick))
			{
				CompleteFailed($"Burst timed out after {relativeTick} ticks.");
				return;
			}

			if (config.IsBurstImpactDue(relativeTick, burstImpacts))
			{
				var elite = (burstImpacts & 1) != 0;
				var weaponName = elite ? "BlimpBombE" : "BlimpBomb";
				world.Map.Rules.Weapons[weaponName.ToLowerInvariant()]
					.Impact(Target.FromPos(burstPosition), sourceActor);
				if (burstFirstImpactTick < 0)
					burstFirstImpactTick = Game.LocalTick;

				burstImpactTicks.Add(Game.LocalTick);
				burstLastImpactTick = Game.LocalTick;
				burstImpacts++;
				bufferedBurstProgress.Add(new BurstProgress(burstImpacts, Game.LocalTick, elite));
			}

			burstRegistrySnapshot = ImpactEffectAuditRegistry.Snapshot(
				burstRegistryWatermark, burstPosition);
			var activeSprites = 0;
			foreach (var registration in burstRegistrySnapshot.Sprites)
			{
				if (registration.Effect == null || registration.AddedTick < 0)
					continue;

				burstSpriteLifecycle.ObserveAdded(registration.CorrelationId);
				if (!world.Effects.Contains(registration.Effect))
					continue;

				activeSprites++;
				var renderables = registration.Effect.Render(worldRenderer).ToArray();
				if (renderables.Length == 0)
					continue;

				if (renderables.Any(renderable => renderable.Pos != burstPosition))
				{
					CompleteFailed($"Burst sprite {registration.CorrelationId} rendered away from the target.");
					return;
				}

				burstSpriteLifecycle.ObserveRendered(registration.CorrelationId);
			}

			if (!ObserveAudioRegistrations(burstRegistrySnapshot.Sounds, burstAudioObserved,
				_ => { }, out var audioFailure))
			{
				CompleteFailed(audioFailure);
				return;
			}

			burstTargetPeak = Math.Max(burstTargetPeak, activeSprites);
			burstGlobalPeak = Math.Max(burstGlobalPeak, world.Effects.OfType<SpriteEffect>().Count());
			if (ImpactEffectsRuntimePerformanceWindow.ShouldSample(Game.LocalTick,
				burstFirstImpactTick, burstLastImpactTick, burstImpacts, config.BurstImpactCount, EvidenceTick))
				CapturePerformanceSample(Game.LocalTick);

			if (burstImpacts < config.BurstImpactCount || Game.LocalTick - burstLastImpactTick < EvidenceTick)
				return;

			if (!burstEvidenceValidated)
			{
				if (!ValidateBurstTrace(out var detail, out var records))
				{
					CompleteFailed(detail);
					return;
				}

				burstSpriteSchedules = records.Count(
					record => record.Kind == ImpactEffectDiagnosticKind.SpriteScheduled);
				burstSpriteAdded = burstSpriteLifecycle.Added;
				burstSpriteRendered = burstSpriteLifecycle.Rendered;
				if (burstRegistrySnapshot.Sprites.Count != config.BurstImpactCount * 4 ||
					burstSpriteAdded != config.BurstImpactCount * 4 ||
					burstSpriteRendered != config.BurstImpactCount * 4 ||
					burstRegistrySnapshot.Sounds.Count != config.BurstImpactCount ||
					burstAudioObserved.Count != config.BurstImpactCount)
				{
					CompleteFailed($"Burst actual evidence mismatch: registry={burstRegistrySnapshot.Sprites.Count} " +
						$"added={burstSpriteAdded} rendered={burstSpriteRendered} " +
						$"sounds={burstRegistrySnapshot.Sounds.Count} audio={burstAudioObserved.Count}.");
					return;
				}

				var registrySpriteIds = burstRegistrySnapshot.Sprites
					.Select(registration => registration.CorrelationId).OrderBy(id => id).ToArray();
				var traceSpriteIds = records.Where(record => record.Kind == ImpactEffectDiagnosticKind.SpriteAdded)
					.Select(record => record.CorrelationId).OrderBy(id => id).ToArray();
				var registrySoundIds = burstRegistrySnapshot.Sounds
					.Select(registration => registration.CorrelationId).OrderBy(id => id).ToArray();
				var traceSoundIds = records.Where(record => record.Kind == ImpactEffectDiagnosticKind.Sound)
					.Select(record => record.CorrelationId).OrderBy(id => id).ToArray();
				if (!registrySpriteIds.SequenceEqual(traceSpriteIds) || !registrySoundIds.SequenceEqual(traceSoundIds))
				{
					CompleteFailed("Burst registry and diagnostic correlation IDs do not match.");
					return;
				}

				WriteEffectRecords("burst", "kirov-burst", "BlimpBomb/BlimpBombE", records);
				WriteBufferedPerformanceEvidence();
				burstEvidenceValidated = true;
			}

			burstRegistrySnapshot = ImpactEffectAuditRegistry.Snapshot(
				burstRegistryWatermark, burstPosition);
			foreach (var registration in burstRegistrySnapshot.Sprites)
			{
				if (registration.Effect == null || world.Effects.Contains(registration.Effect))
					return;

				burstSpriteLifecycle.ObserveCompleted(registration.CorrelationId);
			}

			if (!burstSpriteLifecycle.Validate(out var lifecycleDetail))
			{
				CompleteFailed(lifecycleDetail);
				return;
			}

			burstCleanupTicks = Game.LocalTick - burstLastImpactTick;
			burstSpriteCompleted = burstSpriteLifecycle.Completed;
			burstComplete = true;
			CompletePassed();
		}

		void StartBurst()
		{
			burstStarted = true;
			burstPosition = GroundPosition(world.Map.CenterOfCell(landCell));
			worldRenderer.Viewport.Center(burstPosition);
			burstTraceWatermark = ImpactEffectsRuntimeEvidence.CaptureWatermark(DiagnosticTrace.Snapshot());
			burstRegistryWatermark = ImpactEffectAuditRegistry.LatestCorrelationId;
			burstStartTick = Game.LocalTick;
			burstLastImpactTick = Game.LocalTick;
			burstGlobalPeak = world.Effects.OfType<SpriteEffect>().Count();
			RecordProgress("burst-start",
				$"impacts={config.BurstImpactCount} interval={config.BurstIntervalTicks} position={Format(burstPosition)}");
		}

		bool ValidateBurstTrace(out string detail, out IReadOnlyList<ImpactEffectDiagnosticRecord> records)
		{
			var snapshot = DiagnosticTrace.Snapshot();
			var parsed = ParseAllDiagnostics(snapshot, burstTraceWatermark, burstPosition, burstStartTick);
			records = parsed;
			if (!ImpactEffectsRuntimeEvidence.HasNoNewDroppedEvents(snapshot, burstTraceWatermark))
			{
				detail = "Burst diagnostic trace is unavailable or its dropped-event counter changed.";
				return false;
			}

			var sprites = parsed.Where(
				record => record.Kind == ImpactEffectDiagnosticKind.SpriteScheduled).ToList();
			var sounds = parsed.Where(record => record.Kind == ImpactEffectDiagnosticKind.Sound).ToList();
			var expectedSprites = new List<(string Image, string Asset, int Tick)>();
			var expectedSounds = new List<(string Asset, int Tick)>();
			for (var i = 0; i < config.BurstImpactCount; i++)
			{
				var weaponName = (i & 1) == 0 ? "BlimpBomb" : "BlimpBombE";
				var tickOffset = burstImpactTicks[i] - burstStartTick;
				foreach (var layer in ImpactEffectsRuntimeExpectation.ForWeapon(weaponName)
					.LayersFor(ImpactEffectsRuntimeTerrain.Land))
				{
					expectedSprites.Add((layer.Image, layer.Sequence, tickOffset + layer.Delay));
					foreach (var sound in layer.ImpactSounds)
						expectedSounds.Add((sound, tickOffset + layer.Delay));
				}
			}

			if (sprites.Count != expectedSprites.Count || expectedSprites.Count != config.BurstImpactCount * 4)
			{
				detail = $"Burst sprite schedule mismatch: expected={expectedSprites.Count} observed={sprites.Count}.";
				return false;
			}

			foreach (var expected in expectedSprites)
			{
				var index = sprites.FindIndex(record => record.Image == expected.Image &&
					record.Asset == expected.Asset && record.TickOffset == expected.Tick);
				if (index < 0)
				{
					detail = $"Missing burst sprite {expected.Image}|{expected.Asset}@{expected.Tick}.";
					return false;
				}

				sprites.RemoveAt(index);
			}

			if (!ImpactEffectsRuntimeCorrelationEvidence.Validate(
				parsed, config.BurstImpactCount * 4, out detail))
				return false;

			if (sounds.Count != expectedSounds.Count || sounds.Any(record => !record.PlaybackReturned))
			{
				detail = $"Burst sound schedule mismatch: expected={expectedSounds.Count} observed={sounds.Count}.";
				return false;
			}

			foreach (var expected in expectedSounds)
			{
				var index = sounds.FindIndex(record => record.Asset == expected.Asset && record.TickOffset == expected.Tick);
				if (index < 0)
				{
					detail = $"Missing burst sound {expected.Asset}@{expected.Tick}.";
					return false;
				}

				sounds.RemoveAt(index);
			}

			detail = string.Empty;
			return true;
		}

		static IReadOnlyList<ImpactEffectDiagnosticRecord> ParseAllDiagnostics(
			DiagnosticTraceSnapshot snapshot, ImpactEffectsRuntimeTraceWatermark watermark,
			WPos target, int originTick)
		{
			var records = new List<ImpactEffectDiagnosticRecord>();
			if (snapshot == null || !snapshot.Available || snapshot.Events == null)
				return records;

			var packed = ImpactEffectsRuntimeEvidence.PackHorizontalPosition(target);
			foreach (var trace in snapshot.Events.Where(e => e.Sequence > watermark.Sequence &&
				e.Phase == DiagnosticTracePhase.Instant && e.Arg1 == packed))
			{
				var tickOffset = checked((int)(trace.Arg0 - originTick));
				const string spriteScheduledPrefix = "ImpactEffectAudit.SpriteScheduled|";
				const string spriteAddedPrefix = "ImpactEffectAudit.SpriteAdded|";
				const string soundPrefix = "ImpactEffectAudit.Sound|";
				if (trace.Name.StartsWith(spriteScheduledPrefix, StringComparison.Ordinal))
				{
					var fields = trace.Name[spriteScheduledPrefix.Length..].Split('|');
					if (fields.Length == 3 && long.TryParse(fields[0], NumberStyles.None,
						CultureInfo.InvariantCulture, out var correlationId))
						records.Add(new ImpactEffectDiagnosticRecord(ImpactEffectDiagnosticKind.SpriteScheduled,
							correlationId, fields[1], fields[2], tickOffset, false, false));
				}
				else if (trace.Name.StartsWith(spriteAddedPrefix, StringComparison.Ordinal))
				{
					var fields = trace.Name[spriteAddedPrefix.Length..].Split('|');
					if (fields.Length == 3 && long.TryParse(fields[0], NumberStyles.None,
						CultureInfo.InvariantCulture, out var correlationId))
						records.Add(new ImpactEffectDiagnosticRecord(ImpactEffectDiagnosticKind.SpriteAdded,
							correlationId, fields[1], fields[2], tickOffset, false, false));
				}
				else if (trace.Name.StartsWith(soundPrefix, StringComparison.Ordinal))
				{
					var fields = trace.Name[soundPrefix.Length..].Split('|');
					if (fields.Length == 4 && long.TryParse(fields[0], NumberStyles.None,
						CultureInfo.InvariantCulture, out var correlationId))
						records.Add(new ImpactEffectDiagnosticRecord(ImpactEffectDiagnosticKind.Sound,
							correlationId, string.Empty, fields[1], tickOffset,
							fields[2] == "played", fields[3] == "dummy"));
				}
			}

			return records;
		}

		bool TryReadStableScreenshot(string path, ImpactEffectsRuntimeScreenshotReadiness readiness,
			int2 cropCenter, out ImpactEffectsRuntimeDecodedScreenshot decoded)
		{
			decoded = null;
			if (readiness == null || !File.Exists(path))
				return false;

			long length;
			ImpactEffectsRuntimeDecodedScreenshot candidate;
			try
			{
				length = new FileInfo(path).Length;
				var bytes = File.ReadAllBytes(path);
				var resolution = Game.Renderer.Resolution;
				candidate = ImpactEffectsRuntimeScreenshotEvidence.Decode(bytes,
					resolution.Width, resolution.Height, cropCenter, ScreenshotCropSize);
				screenshotDecodeDetail = string.Empty;
			}
			catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
			{
				length = File.Exists(path) ? new FileInfo(path).Length : 0;
				screenshotDecodeDetail = $"{e.GetType().Name}: {e.Message}";
				readiness.Observe(Game.RenderFrame, length, decoded: false);
				return false;
			}

			if (!readiness.Observe(Game.RenderFrame, length, decoded: true))
				return false;

			decoded = candidate;
			return true;
		}

		uint[] OverlappingActorIds(WPos position) => world.FindActorsOnCircle(position, WDist.Zero)
			.Where(actor => actor.IsInWorld && !actor.Disposed && !actor.WillDispose)
			.Where(actor => actor.TraitsImplementing<HitShape>()
				.Where(shape => !shape.IsTraitDisabled)
				.Any(shape => shape.DistanceFromEdge(actor, position).Length <= 0))
			.Select(actor => actor.ActorID)
			.Distinct()
			.OrderBy(id => id)
			.ToArray();

		static bool ObserveAudioRegistrations(
			IReadOnlyList<ImpactEffectAuditSoundRegistration> registrations,
			HashSet<long> observed, Action<string> setEvidence, out string detail)
		{
			foreach (var registration in registrations)
			{
				var observation = ImpactEffectsRuntimeAudioEvidence.Observe(
					registration.Playback, registration.DummyEngine,
					observed.Contains(registration.CorrelationId));
				if (observation.Status == ImpactEffectsRuntimeAudioStatus.Failed)
				{
					detail = $"Sound `{registration.Asset}` correlation {registration.CorrelationId} failed: " +
						observation.Detail;
					return false;
				}

				if (observation.Status == ImpactEffectsRuntimeAudioStatus.DummyScheduled)
				{
					observed.Add(registration.CorrelationId);
					setEvidence("dummy_schedule_only");
				}
				else if (observation.Status == ImpactEffectsRuntimeAudioStatus.DeviceStarted)
				{
					observed.Add(registration.CorrelationId);
					setEvidence("asset_decoded_and_device_handle_progress_observed");
				}
			}

			detail = string.Empty;
			return true;
		}

		void WriteEffectEvidence(string scope, string caseId, string weaponName,
			ImpactEffectTraceEvidence evidence) =>
			WriteEffectRecords(scope, caseId, weaponName,
				evidence.SpriteRecords.Concat(evidence.SpriteAddedRecords)
					.Concat(evidence.SoundRecords).ToArray());

		void WriteEffectRecords(string scope, string caseId, string weaponName,
			IEnumerable<ImpactEffectDiagnosticRecord> records)
		{
			foreach (var record in records)
				WriteDurable(effectsWriter, effectsStream,
					$"{Csv(scope)},{Csv(caseId)},{Csv(weaponName)},{record.Kind},{record.CorrelationId}," +
					$"{record.TickOffset}," +
					$"{Csv(record.Image)},{Csv(record.Asset)},{record.PlaybackReturned.ToString().ToLowerInvariant()}," +
					$"{record.DummyEngine.ToString().ToLowerInvariant()}");
		}

		void CapturePerformanceSample(int tick)
		{
			performanceSamples.Add(new ImpactEffectsRuntimePerformanceSample(tick,
				PerformanceValue("render"), PerformanceValue("world_tick"),
				PerformanceValue("render_prepare"), PerformanceValue("render_flip"),
				PerformanceValue("batches")));
		}

		void WriteBufferedPerformanceEvidence()
		{
			if (performanceWritten)
				return;

			foreach (var progress in bufferedBurstProgress)
				RecordProgress("burst-impact",
					$"index={progress.Index} tick={progress.Tick} weapon={(progress.Elite ? "BlimpBombE" : "BlimpBomb")}");

			foreach (var sample in performanceSamples)
				WriteDurable(performanceWriter, performanceStream,
					$"sample,{sample.Tick},{FormatNumber(sample.Render)},{FormatNumber(sample.WorldTick)}," +
					$"{FormatNumber(sample.RenderPrepare)},{FormatNumber(sample.RenderFlip)}," +
					FormatNumber(sample.Batches));

			var p95 = PerformanceMetrics.Select(metric =>
				ImpactEffectsRuntimeEvidence.Percentile95(PerformanceValues(metric))).ToArray();
			var max = PerformanceMetrics.Select(metric =>
				performanceSamples.Count == 0 ? 0 : PerformanceValues(metric).Max()).ToArray();
			WriteDurable(performanceWriter, performanceStream,
				$"p95,-1,{string.Join(',', p95.Select(FormatNumber))}");
			WriteDurable(performanceWriter, performanceStream,
				$"max,-1,{string.Join(',', max.Select(FormatNumber))}");
			performanceWritten = true;
		}

		IEnumerable<double> PerformanceValues(string metric)
		{
			foreach (var sample in performanceSamples)
				yield return metric switch
				{
					"render" => sample.Render,
					"world_tick" => sample.WorldTick,
					"render_prepare" => sample.RenderPrepare,
					"render_flip" => sample.RenderFlip,
					"batches" => sample.Batches,
					_ => 0
				};
		}

		Dictionary<string, double> PerformanceSummary(Func<IEnumerable<double>, double> summarize) =>
			PerformanceMetrics.ToDictionary(metric => metric,
				metric => summarize(PerformanceValues(metric)), StringComparer.Ordinal);

		static string FormatNumber(double value) => value.ToString(CultureInfo.InvariantCulture);

		static double PerformanceValue(string metric) =>
			PerfHistory.Items.TryGetValue(metric, out var item) ? item.LastValue : 0;

		void CompletePassed()
		{
			if (passed != cases.Length || failed != 0 || screenshots != cases.Length ||
				!burstComplete || burstImpacts != config.BurstImpactCount ||
				burstSpriteSchedules != config.BurstImpactCount * 4 ||
				burstSpriteAdded != config.BurstImpactCount * 4 ||
				burstSpriteRendered != config.BurstImpactCount * 4 ||
				burstSpriteCompleted != config.BurstImpactCount * 4 ||
				!performanceWritten || performanceSamples.Count == 0)
			{
				CompleteFailed("Completion invariants were not satisfied.");
				return;
			}

			FinishTerminal("PASSED", string.Empty);
		}

		void CompleteFailed(string detail)
		{
			if (terminal.Completed || terminal.Finishing)
				return;

			if (failed == 0)
				failed = 1;

			FinishTerminal("FAILED", detail);
		}

		void CompleteBlocked(string detail) => FinishTerminal("BLOCKED_RETAIL_ASSETS", detail);

		void FinishTerminal(string status, string detail)
		{
			if (!terminal.TryBeginFinishing())
				return;

			var finalStatus = status;
			var finalDetail = detail ?? string.Empty;
			try
			{
				var phase = status == "PASSED" ? "audit-complete" :
					status == "BLOCKED_RETAIL_ASSETS" ? "audit-blocked" : "audit-failed";
				RecordProgress(phase,
					$"status={status} passed={passed} failed={failed} screenshots={screenshots} detail={detail}");
			}
			catch (Exception e)
			{
				finalStatus = "FAILED";
				if (failed == 0)
					failed = 1;
				finalDetail = AppendDetail(finalDetail,
					$"Terminal progress write failed: {e.GetType().Name}: {e.Message}");
			}

			try
			{
				DisposeActor(targetActor);
			}
			catch (Exception e)
			{
				finalStatus = "FAILED";
				if (failed == 0)
					failed = 1;
				finalDetail = AppendDetail(finalDetail,
					$"Target cleanup failed: {e.GetType().Name}: {e.Message}");
			}

			var closeDetail = DisposeEvidenceWriters();
			if (!string.IsNullOrEmpty(closeDetail))
			{
				finalStatus = "FAILED";
				if (failed == 0)
					failed = 1;
				finalDetail = AppendDetail(finalDetail, closeDetail);
			}

			terminal.MarkWritersClosed();
			try
			{
				WriteSummary(true, finalStatus, finalDetail);
				terminal.MarkTerminalSummaryCommitted();
			}
			catch (Exception e)
			{
				Log.Write("debug", "Impact runtime audit terminal summary failed: " + e);
			}
			finally
			{
				Game.Exit();
			}
		}

		void WriteResolvedWeapons()
		{
			object Layers(ImpactEffectsRuntimeExpectation expectation, ImpactEffectsRuntimeTerrain terrain) =>
				expectation.LayersFor(terrain).Select(layer => new
				{
					image = layer.Image,
					sequence = layer.Sequence,
					delay = layer.Delay,
					sounds = layer.ImpactSounds
				}).ToArray();

			var document = new
			{
				weapons = ImpactEffectsRuntimeExpectation.All().Select(expectation => new
				{
					weapon = expectation.WeaponName,
					land = Layers(expectation, ImpactEffectsRuntimeTerrain.Land),
					water = Layers(expectation, ImpactEffectsRuntimeTerrain.Water),
					shake_duration = expectation.ShakeDuration,
					shake_intensity = expectation.ShakeIntensity
				}).ToArray()
			};
			WriteAtomic(Path.Combine(outputDirectory, "resolved-weapons.json"),
				ImpactEffectsRuntimeJson.Serialize(document, JsonOptions) + "\n");
		}

		void RecordProgress(string phase, string detail)
		{
			DiagnosticTrace.Instant(DiagnosticSubsystem.Simulation,
				"ImpactEffectsRuntimeAudit." + phase, currentIndex, cases.Length);
			WriteDurable(progressWriter, progressStream, $"{DateTime.UtcNow:O}\t{phase}\t{detail}");
		}

		void WriteSummary(bool complete, string status, string detail)
		{
			var functionalStatus = status == "PASSED" ? "PASSED" : status;
			var performanceStatus = status == "PASSED" ? "RECORDED_FOR_REVIEW" : "NOT_COMPLETED";
			var p95 = PerformanceSummary(ImpactEffectsRuntimeEvidence.Percentile95);
			var max = PerformanceSummary(values => values.Any() ? values.Max() : 0);
			var document = new
			{
				complete,
				status,
				functional_status = functionalStatus,
				performance_status = performanceStatus,
				total = cases.Length,
				completed = currentIndex,
				passed,
				failed,
				screenshots,
				burst_complete = burstComplete,
				burst_impacts = burstImpacts,
				burst_sprite_schedules = burstSpriteSchedules,
				burst_sprite_added = burstSpriteAdded,
				burst_sprite_rendered = burstSpriteRendered,
				burst_sprite_completed = burstSpriteCompleted,
				burst_target_sprite_peak = burstTargetPeak,
				burst_global_sprite_peak = burstGlobalPeak,
				burst_cleanup_ticks = burstCleanupTicks,
				sample_count = performanceSamples.Count,
				performance_sample_count = performanceSamples.Count,
				performance_p95 = p95,
				performance_max = max,
				audio_backend_evidence = Game.Sound.DummyEngine
					? "dummy_schedule_only"
					: "decoded_asset_plus_matching_ISound_SeekPosition_progress",
				audio_backend_limitation = "ISound exposes no per-handle backend error API",
				detail
			};
			WriteAtomic(Path.Combine(outputDirectory, "summary.json"),
				ImpactEffectsRuntimeJson.Serialize(document, JsonOptions) + "\n");
		}

		TypeDictionary Initializers(string actorName, Player owner, CPos cell)
		{
			var positionable = world.Map.Rules.Actors[actorName].TraitInfo<IPositionableInfo>();
			var subCell = positionable.SharesCell ? world.ActorMap.FreeSubCell(cell) : SubCell.FullCell;
			return new TypeDictionary
			{
				new OwnerInit(owner),
				new LocationInit(cell),
				new SubCellInit(subCell),
				new FacingInit(WAngle.Zero),
				new FactionInit(owner.Faction.InternalName),
				new SpawnedByMapInit()
			};
		}

		WPos GroundPosition(WPos position)
		{
			var distance = world.Map.DistanceAboveTerrain(position);
			return position - new WVec(0, 0, distance.Length);
		}

		static void DisposeActor(Actor actor)
		{
			if (actor != null && !actor.Disposed && !actor.WillDispose)
				actor.Dispose();
		}

		static FileStream OpenNew(string path) =>
			new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);

		static void WriteDurable(StreamWriter writer, FileStream stream, string line)
		{
			writer.WriteLine(line);
			writer.Flush();
			stream.Flush(true);
		}

		static void WriteAtomic(string path, string content)
		{
			var temporary = path + ".tmp";
			using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.Read))
			using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
			{
				writer.Write(content);
				writer.Flush();
				stream.Flush(true);
			}

			File.Move(temporary, path, true);
		}

		static string Format(WPos position) => $"{position.X}:{position.Y}:{position.Z}";
		static string Csv(string value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
		static string AppendDetail(string detail, string addition) =>
			string.IsNullOrEmpty(detail) ? addition : detail + " " + addition;

		string DisposeEvidenceWriters()
		{
			if (writersDisposed)
				return string.Empty;

			writersDisposed = true;
			var errors = new List<string>();
			Close("progress", progressWriter, progressStream, errors);
			Close("cases", casesWriter, casesStream, errors);
			Close("effects", effectsWriter, effectsStream, errors);
			Close("performance", performanceWriter, performanceStream, errors);
			return string.Join(" ", errors);
		}

		static void Close(string name, StreamWriter writer, FileStream stream, List<string> errors)
		{
			try
			{
				writer?.Flush();
			}
			catch (Exception e)
			{
				errors.Add($"{name} flush failed: {e.GetType().Name}: {e.Message}.");
			}

			try
			{
				writer?.Dispose();
			}
			catch (Exception e)
			{
				errors.Add($"{name} writer close failed: {e.GetType().Name}: {e.Message}.");
			}

			try
			{
				stream?.Dispose();
			}
			catch (Exception e)
			{
				errors.Add($"{name} stream close failed: {e.GetType().Name}: {e.Message}.");
			}
		}

		public void Dispose() => DisposeEvidenceWriters();
	}
}
