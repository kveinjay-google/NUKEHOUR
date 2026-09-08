#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software under the GNU General Public License.
 */
#endregion

using System;
using NUnit.Framework;
using OpenRA.Support;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RuntimeResourceSamplerTest
	{
		[TestCase(500, 1000, 2, 25)]
		[TestCase(3000, 1000, 2, 100)]
		[TestCase(-10, 1000, 2, 0)]
		public void CpuPercentIsNormalizedAndClamped(double cpuMilliseconds, double elapsedMilliseconds,
			int processorCount, double expected)
		{
			Assert.That(RuntimeResourceSampler.CalculateCpuPercent(
				TimeSpan.FromMilliseconds(cpuMilliseconds), elapsedMilliseconds, processorCount), Is.EqualTo(expected));
		}

		[Test]
		public void SamplesAreCachedUntilTheIntervalExpires()
		{
			var elapsed = 0L;
			var cpu = TimeSpan.Zero;
			var reads = 0;
			var sampler = new RuntimeResourceSampler(
				() => elapsed,
				() => { reads++; return cpu; },
				() => 256 * 1024 * 1024,
				() => 64 * 1024 * 1024,
				2,
				500);

			elapsed = 100;
			cpu = TimeSpan.FromMilliseconds(50);
			sampler.Sample();
			Assert.That(reads, Is.EqualTo(1));

			elapsed = 499;
			cpu = TimeSpan.FromMilliseconds(200);
			sampler.Sample();
			Assert.That(reads, Is.EqualTo(1));

			elapsed = 600;
			var sample = sampler.Sample();
			Assert.That(reads, Is.EqualTo(2));
			Assert.That(sample.CpuPercent, Is.EqualTo(15).Within(0.001));
			Assert.That(sample.ResidentMemoryBytes, Is.EqualTo(256 * 1024 * 1024));
			Assert.That(sample.ManagedMemoryBytes, Is.EqualTo(64 * 1024 * 1024));
		}

		[Test]
		public void ProviderFailuresReturnUnavailableValues()
		{
			var sampler = new RuntimeResourceSampler(
				() => 0,
				() => throw new InvalidOperationException(),
				() => throw new InvalidOperationException(),
				() => throw new InvalidOperationException(),
				1,
				500);

			var sample = sampler.Sample();
			Assert.That(sample.CpuPercent, Is.Null);
			Assert.That(sample.ResidentMemoryBytes, Is.Null);
			Assert.That(sample.ManagedMemoryBytes, Is.Null);
		}

		[Test]
		public void ExtendedSampleIncludesAllocationsCollectionsAndThermalState()
		{
			var sampler = new RuntimeResourceSampler(
				() => 1000,
				() => TimeSpan.FromMilliseconds(10),
				() => 256 * 1024 * 1024,
				() => 64 * 1024 * 1024,
				() => 123456789,
				() => 4,
				() => 3,
				() => 2,
				() => "Serious",
				2,
				500);

			var sample = sampler.Sample();

			Assert.That(sample.TotalAllocatedBytes, Is.EqualTo(123456789));
			Assert.That(sample.Generation0Collections, Is.EqualTo(4));
			Assert.That(sample.Generation1Collections, Is.EqualTo(3));
			Assert.That(sample.Generation2Collections, Is.EqualTo(2));
			Assert.That(sample.ThermalState, Is.EqualTo("Serious"));
		}

		[Test]
		public void ExtendedProviderFailuresRemainUnavailable()
		{
			var sampler = new RuntimeResourceSampler(
				() => 1000,
				() => TimeSpan.Zero,
				() => 1,
				() => 1,
				() => throw new InvalidOperationException(),
				() => throw new InvalidOperationException(),
				() => throw new InvalidOperationException(),
				() => throw new InvalidOperationException(),
				() => throw new InvalidOperationException(),
				1,
				500);

			var sample = sampler.Sample();
			Assert.That(sample.TotalAllocatedBytes, Is.Null);
			Assert.That(sample.Generation0Collections, Is.Null);
			Assert.That(sample.Generation1Collections, Is.Null);
			Assert.That(sample.Generation2Collections, Is.Null);
			Assert.That(sample.ThermalState, Is.Null);
		}

		[TestCase(0, "0 MB")]
		[TestCase(1572864, "1.5 MB")]
		[TestCase(null, "N/A")]
		public void MemoryValuesHaveCompactFormatting(long? bytes, string expected)
		{
			Assert.That(RuntimeResourceSampler.FormatMegabytes(bytes), Is.EqualTo(expected));
		}

		[Test]
		public void IosUsesNativeResourceProviderInsteadOfUnsupportedProcessApis()
		{
			var elapsed = 0L;
			var cpu = TimeSpan.Zero;
			var nativeReads = 0;
			var sampler = RuntimeResourceSampler.CreateForPlatform(
				true,
				() => elapsed,
				() => throw new PlatformNotSupportedException(),
				() => throw new PlatformNotSupportedException(),
				() => 32 * 1024 * 1024,
				() =>
				{
					nativeReads++;
					return new RuntimeNativeResourceSample(cpu, 384 * 1024 * 1024);
				},
				4,
				500);

			elapsed = 100;
			cpu = TimeSpan.FromMilliseconds(40);
			sampler.Sample();
			elapsed = 600;
			cpu = TimeSpan.FromMilliseconds(240);
			var sample = sampler.Sample();

			Assert.That(nativeReads, Is.EqualTo(2));
			Assert.That(sample.CpuPercent, Is.EqualTo(10).Within(0.001));
			Assert.That(sample.ResidentMemoryBytes, Is.EqualTo(384 * 1024 * 1024));
			Assert.That(sample.ManagedMemoryBytes, Is.EqualTo(32 * 1024 * 1024));
		}
	}
}
