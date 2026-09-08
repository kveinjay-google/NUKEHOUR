#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software under the GNU General Public License.
 */
#endregion

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace OpenRA.Support
{
	public readonly struct RuntimeResourceSample
	{
		public readonly double? CpuPercent;
		public readonly long? ResidentMemoryBytes;
		public readonly long? ManagedMemoryBytes;
		public readonly long? TotalAllocatedBytes;
		public readonly int? Generation0Collections;
		public readonly int? Generation1Collections;
		public readonly int? Generation2Collections;
		public readonly string ThermalState;

		public RuntimeResourceSample(double? cpuPercent, long? residentMemoryBytes, long? managedMemoryBytes,
			long? totalAllocatedBytes = null, int? generation0Collections = null,
			int? generation1Collections = null, int? generation2Collections = null,
			string thermalState = null)
		{
			CpuPercent = cpuPercent;
			ResidentMemoryBytes = residentMemoryBytes;
			ManagedMemoryBytes = managedMemoryBytes;
			TotalAllocatedBytes = totalAllocatedBytes;
			Generation0Collections = generation0Collections;
			Generation1Collections = generation1Collections;
			Generation2Collections = generation2Collections;
			ThermalState = thermalState;
		}
	}

	public readonly struct RuntimeNativeResourceSample
	{
		public readonly TimeSpan CpuTime;
		public readonly long ResidentMemoryBytes;

		public RuntimeNativeResourceSample(TimeSpan cpuTime, long residentMemoryBytes)
		{
			CpuTime = cpuTime;
			ResidentMemoryBytes = residentMemoryBytes;
		}
	}

	public sealed class RuntimeResourceSampler
	{
		const int TaskBasicInfo64Flavor = 5;
		const int TaskThreadTimesInfoFlavor = 3;

		public static Func<string> ThermalStateProvider { get; set; }

		readonly Func<long> elapsedMilliseconds;
		readonly Func<TimeSpan> processCpuTime;
		readonly Func<long> residentMemoryBytes;
		readonly Func<long> managedMemoryBytes;
		readonly Func<long> totalAllocatedBytes;
		readonly Func<int> generation0Collections;
		readonly Func<int> generation1Collections;
		readonly Func<int> generation2Collections;
		readonly Func<string> thermalState;
		readonly int processorCount;
		readonly int sampleIntervalMilliseconds;

		long? previousSampleTime;
		TimeSpan? previousCpuTime;
		RuntimeResourceSample cachedSample;

		public RuntimeResourceSampler(int sampleIntervalMilliseconds = 500)
			: this(CreateForPlatform(
				Platform.IsIOS,
				() => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency,
				ReadProcessCpuTime,
				ReadResidentMemory,
				() => GC.GetTotalMemory(false),
				ReadMachResources,
				Environment.ProcessorCount,
				sampleIntervalMilliseconds))
		{
		}

		RuntimeResourceSampler(RuntimeResourceSampler configured)
			: this(configured.elapsedMilliseconds, configured.processCpuTime, configured.residentMemoryBytes,
				configured.managedMemoryBytes, configured.totalAllocatedBytes,
				configured.generation0Collections, configured.generation1Collections,
				configured.generation2Collections, configured.thermalState,
				configured.processorCount, configured.sampleIntervalMilliseconds)
		{
		}

		public static RuntimeResourceSampler CreateForPlatform(bool isIos, Func<long> elapsedMilliseconds,
			Func<TimeSpan> processCpuTime, Func<long> residentMemoryBytes, Func<long> managedMemoryBytes,
			Func<RuntimeNativeResourceSample> nativeResources, int processorCount, int sampleIntervalMilliseconds)
		{
			if (!isIos)
				return new RuntimeResourceSampler(elapsedMilliseconds, processCpuTime, residentMemoryBytes,
					managedMemoryBytes, processorCount, sampleIntervalMilliseconds);

			RuntimeNativeResourceSample latest = default;
			var hasLatest = false;
			TimeSpan ReadCpu()
			{
				latest = nativeResources();
				hasLatest = true;
				return latest.CpuTime;
			}

			long ReadResident()
			{
				if (!hasLatest)
					latest = nativeResources();

				hasLatest = false;
				return latest.ResidentMemoryBytes;
			}

			return new RuntimeResourceSampler(elapsedMilliseconds, ReadCpu, ReadResident,
				managedMemoryBytes, processorCount, sampleIntervalMilliseconds);
		}

		public RuntimeResourceSampler(Func<long> elapsedMilliseconds, Func<TimeSpan> processCpuTime,
			Func<long> residentMemoryBytes, Func<long> managedMemoryBytes, int processorCount,
			int sampleIntervalMilliseconds)
			: this(elapsedMilliseconds, processCpuTime, residentMemoryBytes, managedMemoryBytes,
				() => GC.GetTotalAllocatedBytes(false),
				() => GC.CollectionCount(0), () => GC.CollectionCount(1), () => GC.CollectionCount(2),
				() => ThermalStateProvider?.Invoke(), processorCount, sampleIntervalMilliseconds)
		{
		}

		public RuntimeResourceSampler(Func<long> elapsedMilliseconds, Func<TimeSpan> processCpuTime,
			Func<long> residentMemoryBytes, Func<long> managedMemoryBytes, Func<long> totalAllocatedBytes,
			Func<int> generation0Collections, Func<int> generation1Collections, Func<int> generation2Collections,
			Func<string> thermalState, int processorCount, int sampleIntervalMilliseconds)
		{
			this.elapsedMilliseconds = elapsedMilliseconds;
			this.processCpuTime = processCpuTime;
			this.residentMemoryBytes = residentMemoryBytes;
			this.managedMemoryBytes = managedMemoryBytes;
			this.totalAllocatedBytes = totalAllocatedBytes;
			this.generation0Collections = generation0Collections;
			this.generation1Collections = generation1Collections;
			this.generation2Collections = generation2Collections;
			this.thermalState = thermalState;
			this.processorCount = Math.Max(1, processorCount);
			this.sampleIntervalMilliseconds = Math.Max(1, sampleIntervalMilliseconds);
		}

		public RuntimeResourceSample Sample()
		{
			var now = elapsedMilliseconds();
			if (previousSampleTime.HasValue && now - previousSampleTime.Value < sampleIntervalMilliseconds)
				return cachedSample;

			var cpuTime = TryRead(processCpuTime);
			double? cpuPercent = null;
			if (cpuTime.HasValue && previousCpuTime.HasValue && previousSampleTime.HasValue)
				cpuPercent = CalculateCpuPercent(cpuTime.Value - previousCpuTime.Value,
					now - previousSampleTime.Value, processorCount);

			previousSampleTime = now;
			previousCpuTime = cpuTime;
			cachedSample = new RuntimeResourceSample(
				cpuPercent,
				TryRead(residentMemoryBytes),
				TryRead(managedMemoryBytes),
				TryRead(totalAllocatedBytes),
				TryRead(generation0Collections),
				TryRead(generation1Collections),
				TryRead(generation2Collections),
				TryReadReference(thermalState));
			return cachedSample;
		}

		public static double CalculateCpuPercent(TimeSpan cpuTime, double elapsedMilliseconds, int processorCount)
		{
			if (elapsedMilliseconds <= 0)
				return 0;

			var percent = cpuTime.TotalMilliseconds / elapsedMilliseconds / Math.Max(1, processorCount) * 100;
			return Math.Clamp(percent, 0, 100);
		}

		public static string FormatMegabytes(long? bytes)
		{
			if (!bytes.HasValue)
				return "N/A";

			return (bytes.Value / 1048576d).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
		}

		static TimeSpan ReadProcessCpuTime()
		{
			using var process = Process.GetCurrentProcess();
			return process.TotalProcessorTime;
		}

		static long ReadResidentMemory()
		{
			using var process = Process.GetCurrentProcess();
			return process.WorkingSet64;
		}

		static RuntimeNativeResourceSample ReadMachResources()
		{
			var task = MachTaskSelf();
			var timeInfo = default(TaskThreadTimesInfo);
			var timeCount = (uint)(Marshal.SizeOf<TaskThreadTimesInfo>() / sizeof(int));
			if (TaskInfoTimes(task, TaskThreadTimesInfoFlavor, ref timeInfo, ref timeCount) != 0)
				throw new InvalidOperationException("task_info(TASK_THREAD_TIMES_INFO) failed.");

			var basicInfo = default(TaskBasicInfo64);
			var basicCount = (uint)(Marshal.SizeOf<TaskBasicInfo64>() / sizeof(int));
			if (TaskInfoBasic(task, TaskBasicInfo64Flavor, ref basicInfo, ref basicCount) != 0)
				throw new InvalidOperationException("task_info(TASK_BASIC_INFO_64) failed.");

			var userTicks = (long)timeInfo.UserTime.Seconds * TimeSpan.TicksPerSecond +
				(long)timeInfo.UserTime.Microseconds * 10;
			var systemTicks = (long)timeInfo.SystemTime.Seconds * TimeSpan.TicksPerSecond +
				(long)timeInfo.SystemTime.Microseconds * 10;
			return new RuntimeNativeResourceSample(TimeSpan.FromTicks(userTicks + systemTicks),
				checked((long)basicInfo.ResidentSize));
		}

		[StructLayout(LayoutKind.Sequential)]
		struct TimeValue
		{
			public int Seconds;
			public int Microseconds;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct TaskThreadTimesInfo
		{
			public TimeValue UserTime;
			public TimeValue SystemTime;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct TaskBasicInfo64
		{
			public ulong VirtualSize;
			public ulong ResidentSize;
			public TimeValue UserTime;
			public TimeValue SystemTime;
			public int Policy;
			public int SuspendCount;
		}

		[DllImport("__Internal", EntryPoint = "mach_task_self")]
		static extern uint MachTaskSelf();

		[DllImport("__Internal", EntryPoint = "task_info")]
		static extern int TaskInfoTimes(uint task, int flavor, ref TaskThreadTimesInfo info, ref uint count);

		[DllImport("__Internal", EntryPoint = "task_info")]
		static extern int TaskInfoBasic(uint task, int flavor, ref TaskBasicInfo64 info, ref uint count);

		static T? TryRead<T>(Func<T> read) where T : struct
		{
			try
			{
				return read();
			}
			catch
			{
				return null;
			}
		}

		static string TryReadReference(Func<string> read)
		{
			try
			{
				return read?.Invoke();
			}
			catch
			{
				return null;
			}
		}
	}
}
