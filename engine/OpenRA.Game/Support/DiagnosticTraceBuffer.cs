using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace OpenRA
{
	public sealed class DiagnosticTraceBuffer
	{
		sealed class ScopeState
		{
			public long SpanId;
			public long ParentSpanId;
			public long StartedMilliseconds;
			public int ThreadId;
			public DiagnosticSubsystem Subsystem;
			public string Name;
		}

		readonly object sync = new();
		readonly DiagnosticTraceEvent[] events;
		readonly Func<long> clock;
		readonly Dictionary<int, List<ScopeState>> scopes = new();
		readonly Dictionary<DiagnosticSubsystem, DiagnosticHeartbeat> heartbeats = new();
		readonly ConcurrentQueue<long> pendingEnds = new();
		int start;
		int count;
		long sequence;
		long nextSpanId;
		long droppedEvents;

		public DiagnosticTraceBuffer(int capacity = 4096, Func<long> clock = null)
		{
			if (capacity < 1)
				throw new ArgumentOutOfRangeException(nameof(capacity));

			events = new DiagnosticTraceEvent[capacity];
			this.clock = clock ?? (() => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency);
		}

		public DiagnosticTraceScope Scope(DiagnosticSubsystem subsystem, string name)
		{
			if (!Monitor.TryEnter(sync))
			{
				Interlocked.Increment(ref droppedEvents);
				return new DiagnosticTraceScope(this, 0);
			}

			try
			{
				DrainPendingEnds();
				var threadId = Environment.CurrentManagedThreadId;
				if (!scopes.TryGetValue(threadId, out var stack))
					scopes.Add(threadId, stack = new List<ScopeState>(16));

				var spanId = ++nextSpanId;
				var parentSpanId = stack.Count == 0 ? 0 : stack[^1].SpanId;
				var now = clock();
				stack.Add(new ScopeState
				{
					SpanId = spanId,
					ParentSpanId = parentSpanId,
					StartedMilliseconds = now,
					ThreadId = threadId,
					Subsystem = subsystem,
					Name = name
				});
				Append(now, threadId, subsystem, DiagnosticTracePhase.Begin, spanId, parentSpanId, name, 0, 0);
				return new DiagnosticTraceScope(this, spanId);
			}
			finally
			{
				Monitor.Exit(sync);
			}
		}

		public void Instant(DiagnosticSubsystem subsystem, string name, long arg0 = 0, long arg1 = 0)
		{
			if (!Monitor.TryEnter(sync))
			{
				Interlocked.Increment(ref droppedEvents);
				return;
			}

			try
			{
				DrainPendingEnds();
				var threadId = Environment.CurrentManagedThreadId;
				var parent = scopes.TryGetValue(threadId, out var stack) && stack.Count > 0 ? stack[^1].SpanId : 0;
				var now = clock();
				Append(now, threadId, subsystem, DiagnosticTracePhase.Instant, 0, parent, name, arg0, arg1);
			}
			finally
			{
				Monitor.Exit(sync);
			}
		}

		internal void Progress(long spanId, long current, long total)
		{
			if (spanId == 0 || !Monitor.TryEnter(sync))
			{
				if (spanId != 0)
					Interlocked.Increment(ref droppedEvents);

				return;
			}

			try
			{
				DrainPendingEnds();
				if (!FindScope(spanId, out var state, out _))
					return;

				var now = clock();
				Append(now, state.ThreadId, state.Subsystem, DiagnosticTracePhase.Progress,
					state.SpanId, state.ParentSpanId, state.Name, current, total);
			}
			finally
			{
				Monitor.Exit(sync);
			}
		}

		internal void End(long spanId)
		{
			if (spanId == 0 || !Monitor.TryEnter(sync))
			{
				if (spanId != 0)
				{
					Interlocked.Increment(ref droppedEvents);
					pendingEnds.Enqueue(spanId);
				}

				return;
			}

			try
			{
				DrainPendingEnds();
				if (!FindScope(spanId, out var state, out var stack))
					return;

				Append(clock(), state.ThreadId, state.Subsystem, DiagnosticTracePhase.End,
					state.SpanId, state.ParentSpanId, state.Name, 0, 0);
				stack.Remove(state);
				if (stack.Count == 0)
					scopes.Remove(state.ThreadId);
			}
			finally
			{
				Monitor.Exit(sync);
			}
		}

		public DiagnosticTraceSnapshot Snapshot()
		{
			if (!Monitor.TryEnter(sync))
				return new DiagnosticTraceSnapshot
				{
					Available = false,
					DroppedEvents = Interlocked.Read(ref droppedEvents),
					Events = Array.Empty<DiagnosticTraceEvent>(),
					ActiveScopes = Array.Empty<DiagnosticActiveScope>(),
					Heartbeats = new Dictionary<DiagnosticSubsystem, DiagnosticHeartbeat>()
				};

			try
			{
				DrainPendingEnds();
				var result = new DiagnosticTraceEvent[count];
				for (var i = 0; i < count; i++)
					result[i] = events[(start + i) % events.Length];

				var active = scopes.Values.Where(s => s.Count > 0).Select(stack => new DiagnosticActiveScope
				{
					ThreadId = stack[0].ThreadId,
					SpanId = stack[^1].SpanId,
					StartedMilliseconds = stack[^1].StartedMilliseconds,
					Path = string.Join(" > ", stack.Select(s => s.Name))
				}).ToArray();

				return new DiagnosticTraceSnapshot
				{
					Available = true,
					DroppedEvents = Interlocked.Read(ref droppedEvents),
					Events = result,
					ActiveScopes = active,
					Heartbeats = new Dictionary<DiagnosticSubsystem, DiagnosticHeartbeat>(heartbeats)
				};
			}
			finally
			{
				Monitor.Exit(sync);
			}
		}

		public bool TryIsScopeActive(string scopeName, out bool active)
		{
			active = false;
			if (!Monitor.TryEnter(sync))
				return false;

			try
			{
				DrainPendingEnds();
				foreach (var stack in scopes.Values)
					for (var i = 0; i < stack.Count; i++)
						if (string.Equals(stack[i].Name, scopeName, StringComparison.Ordinal))
						{
							active = true;
							return true;
						}

				return true;
			}
			finally
			{
				Monitor.Exit(sync);
			}
		}

		void DrainPendingEnds()
		{
			while (pendingEnds.TryDequeue(out var spanId))
			{
				if (!FindScope(spanId, out var state, out var stack))
					continue;

				Append(clock(), state.ThreadId, state.Subsystem, DiagnosticTracePhase.End,
					state.SpanId, state.ParentSpanId, state.Name, 0, 0);
				stack.Remove(state);
				if (stack.Count == 0)
					scopes.Remove(state.ThreadId);
			}
		}

		bool FindScope(long spanId, out ScopeState state, out List<ScopeState> stack)
		{
			foreach (var candidate in scopes.Values)
			{
				state = candidate.FirstOrDefault(s => s.SpanId == spanId);
				if (state != null)
				{
					stack = candidate;
					return true;
				}
			}

			state = null;
			stack = null;
			return false;
		}

		void Append(long now, int threadId, DiagnosticSubsystem subsystem, DiagnosticTracePhase phase,
			long spanId, long parentSpanId, string name, long arg0, long arg1)
		{
			var item = new DiagnosticTraceEvent(++sequence, now, threadId, subsystem, phase,
				spanId, parentSpanId, name, arg0, arg1);
			var index = (start + count) % events.Length;
			events[index] = item;
			if (count < events.Length)
				count++;
			else
				start = (start + 1) % events.Length;

			heartbeats[subsystem] = new DiagnosticHeartbeat(now, name, arg0, arg1);
		}
	}
}
