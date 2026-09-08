using System;
using System.Collections.Generic;

namespace OpenRA
{
	public static class DiagnosticTrace
	{
		public static readonly DiagnosticTraceBuffer Buffer = new(8192);
		public static readonly bool DetailedSimulationEnabled =
			bool.TryParse(Environment.GetEnvironmentVariable("OPENRA_IOS_DESTRUCTION_AUDIT"), out var enabled) && enabled;

		public static DiagnosticTraceScope Scope(DiagnosticSubsystem subsystem, string name) => Buffer.Scope(subsystem, name);
		public static void Instant(DiagnosticSubsystem subsystem, string name, long arg0 = 0, long arg1 = 0) =>
			Buffer.Instant(subsystem, name, arg0, arg1);
		public static bool TryIsScopeActive(string scopeName, out bool active) =>
			Buffer.TryIsScopeActive(scopeName, out active);
		public static DiagnosticTraceSnapshot Snapshot() => Buffer.Snapshot();
	}

	public enum DiagnosticSubsystem
	{
		Lifecycle,
		Logic,
		Loading,
		Network,
		Server,
		Simulation,
		Render,
		Input,
		Audio,
		Storage
	}

	public enum DiagnosticTracePhase { Begin, Progress, End, Instant }

	public readonly struct DiagnosticTraceEvent
	{
		public readonly long Sequence;
		public readonly long TimestampMilliseconds;
		public readonly int ThreadId;
		public readonly DiagnosticSubsystem Subsystem;
		public readonly DiagnosticTracePhase Phase;
		public readonly long SpanId;
		public readonly long ParentSpanId;
		public readonly string Name;
		public readonly long Arg0;
		public readonly long Arg1;

		public DiagnosticTraceEvent(long sequence, long timestampMilliseconds, int threadId,
			DiagnosticSubsystem subsystem, DiagnosticTracePhase phase, long spanId, long parentSpanId,
			string name, long arg0, long arg1)
		{
			Sequence = sequence;
			TimestampMilliseconds = timestampMilliseconds;
			ThreadId = threadId;
			Subsystem = subsystem;
			Phase = phase;
			SpanId = spanId;
			ParentSpanId = parentSpanId;
			Name = name;
			Arg0 = arg0;
			Arg1 = arg1;
		}
	}

	public readonly struct DiagnosticHeartbeat
	{
		public readonly long TimestampMilliseconds;
		public readonly string Name;
		public readonly long Arg0;
		public readonly long Arg1;

		public DiagnosticHeartbeat(long timestampMilliseconds, string name, long arg0, long arg1)
		{
			TimestampMilliseconds = timestampMilliseconds;
			Name = name;
			Arg0 = arg0;
			Arg1 = arg1;
		}
	}

	public sealed class DiagnosticActiveScope
	{
		public int ThreadId { get; init; }
		public long SpanId { get; init; }
		public long StartedMilliseconds { get; init; }
		public string Path { get; init; }
	}

	public sealed class DiagnosticTraceSnapshot
	{
		public bool Available { get; init; }
		public long DroppedEvents { get; init; }
		public DiagnosticTraceEvent[] Events { get; init; }
		public DiagnosticActiveScope[] ActiveScopes { get; init; }
		public IReadOnlyDictionary<DiagnosticSubsystem, DiagnosticHeartbeat> Heartbeats { get; init; }
	}

	public sealed class DiagnosticTraceScope : IDisposable
	{
		readonly DiagnosticTraceBuffer owner;
		bool disposed;

		internal readonly long SpanId;
		internal DiagnosticTraceScope(DiagnosticTraceBuffer owner, long spanId)
		{
			this.owner = owner;
			SpanId = spanId;
		}

		public void Progress(long current, long total) => owner.Progress(SpanId, current, total);

		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;
			owner.End(SpanId);
		}
	}
}
