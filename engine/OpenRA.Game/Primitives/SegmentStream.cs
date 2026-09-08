#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;

namespace OpenRA.Primitives
{
	public class SegmentStream : Stream
	{
		public readonly Stream BaseStream;
		public readonly long BaseOffset;
		public readonly long BaseCount;

		/// <summary>
		/// Creates a new <see cref="SegmentStream"/> that wraps a subset of the source stream. This takes ownership of
		/// the source stream. The <see cref="SegmentStream"/> is dependent on the source and changes its underlying
		/// position.
		/// </summary>
		/// <param name="stream">The source stream, of which only a segment should be exposed. Ownership is transferred
		/// to the <see cref="SegmentStream"/>.</param>
		/// <param name="offset">The offset at which the segment starts.</param>
		/// <param name="count">The length of the segment.</param>
		public SegmentStream(Stream stream, long offset, long count)
		{
			if (stream == null)
				throw new ArgumentNullException(nameof(stream));
			if (!stream.CanSeek)
				throw new ArgumentException("stream must be seekable.", nameof(stream));
			if (offset < 0)
				throw new ArgumentOutOfRangeException(nameof(offset), "offset must be non-negative.");
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count), "count must be non-negative.");
			if (checked(offset + count) > stream.Length)
				throw new ArgumentException("The segment exceeds the source stream.", nameof(count));

			BaseStream = stream;
			BaseOffset = offset;
			BaseCount = count;

			stream.Seek(BaseOffset, SeekOrigin.Begin);
		}

		public override bool CanSeek => true;
		public override bool CanRead => BaseStream.CanRead;
		public override bool CanWrite => BaseStream.CanWrite;

		public override long Length => BaseCount;

		public override long Position
		{
			get => BaseStream.Position - BaseOffset;
			set
			{
				if (value < 0 || value > Length)
					throw new ArgumentOutOfRangeException(nameof(value));
				BaseStream.Position = checked(BaseOffset + value);
			}
		}

		public override int ReadByte()
		{
			if (Position < Length)
				return BaseStream.ReadByte();
			return -1;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return Read(buffer.AsSpan(offset, count));
		}

		public override int Read(Span<byte> buffer)
		{
			var remaining = Length - Position;
			if (remaining <= 0)
				return 0;
			return BaseStream.Read(buffer[..(int)Math.Min(remaining, buffer.Length)]);
		}

		public override void WriteByte(byte value)
		{
			if (Position < Length)
			{
				BaseStream.WriteByte(value);
				return;
			}
			throw new IOException("Attempted to write past the end of the stream.");
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			Write(buffer.AsSpan(offset, count));
		}

		public override void Write(ReadOnlySpan<byte> buffer)
		{
			if (buffer.Length > Length - Position)
				throw new IOException("Attempted to write past the end of the stream.");
			BaseStream.Write(buffer);
		}

		public override void Flush() { BaseStream.Flush(); }
		public override long Seek(long offset, SeekOrigin origin)
		{
			long target;
			switch (origin)
			{
				default: throw new InvalidEnumArgumentException(nameof(origin), (int)origin, typeof(SeekOrigin));
				case SeekOrigin.Begin:
					target = offset;
					break;
				case SeekOrigin.Current:
					target = checked(Position + offset);
					break;
				case SeekOrigin.End:
					target = checked(Length + offset);
					break;
			}

			Position = target;
			return target;
		}

		public override void SetLength(long value) { throw new NotSupportedException(); }

		public override bool CanTimeout => BaseStream.CanTimeout;

		public override int ReadTimeout
		{
			get => BaseStream.ReadTimeout;
			set => BaseStream.ReadTimeout = value;
		}

		public override int WriteTimeout
		{
			get => BaseStream.WriteTimeout;
			set => BaseStream.WriteTimeout = value;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				BaseStream.Dispose();
			base.Dispose(disposing);
		}

		static long GetOverallNestedOffset(Stream stream, out Stream overallBaseStream)
		{
			var offset = 0L;
			overallBaseStream = stream;
			if (stream is SegmentStream segmentStream)
				offset += segmentStream.BaseOffset + GetOverallNestedOffset(segmentStream.BaseStream, out overallBaseStream);
			return offset;
		}

		/// <summary>
		/// Creates a new <see cref="Stream"/> that wraps a subset of the source stream without taking ownership of it,
		/// allowing it to be reused by the caller. The <see cref="Stream"/> is independent of the source stream and
		/// won't affect its position.
		/// </summary>
		/// <param name="stream">The source stream, of which only a segment should be exposed. Ownership is retained by
		/// the caller.</param>
		/// <param name="offset">The offset at which the segment starts.</param>
		/// <param name="count">The length of the segment.</param>
		public static Stream CreateWithoutOwningStream(Stream stream, long offset, int count)
		{
			var nestedOffset = offset + GetOverallNestedOffset(stream, out var parentStream);

			// Special case FileStream - instead of creating an in-memory copy,
			// just reference the portion of the on-disk file that we need to save memory.
			// We use GetType instead of 'is' here since we can't handle any derived classes of FileStream.
			if (parentStream.GetType() == typeof(FileStream))
			{
				var path = ((FileStream)parentStream).Name;
				return new SegmentStream(File.OpenRead(path), nestedOffset, count);
			}

			// For all other streams, create a copy in memory.
			// This uses more memory but is the only way in general to ensure the returned streams won't clash.
			stream.Seek(offset, SeekOrigin.Begin);
			return new MemoryStream(stream.ReadBytes(count));
		}
	}

	/// <summary>
	/// A seekable forwarding stream that borrows its backing stream until ownership is explicitly committed.
	/// This lets bounded package loaders validate with an ownership-taking parser while preserving the legacy
	/// loader rule that a failed parse retains caller ownership.
	/// </summary>
	public sealed class DeferredOwnershipStream : Stream
	{
		readonly object sync = new();
		Stream stream;
		bool committed;

		DeferredOwnershipStream(Stream borrowedStream)
		{
			stream = borrowedStream;
		}

		public static DeferredOwnershipStream Borrow(Stream borrowedStream)
		{
			if (borrowedStream == null)
				throw new ArgumentNullException(nameof(borrowedStream));
			if (!borrowedStream.CanRead || !borrowedStream.CanSeek)
				throw new ArgumentException("The borrowed stream must be readable and seekable.", nameof(borrowedStream));
			return new DeferredOwnershipStream(borrowedStream);
		}

		public void CommitOwnership()
		{
			lock (sync)
			{
				if (stream == null)
					throw new ObjectDisposedException(nameof(DeferredOwnershipStream));
				committed = true;
			}
		}

		Stream Current
		{
			get
			{
				lock (sync)
					return stream ?? throw new ObjectDisposedException(nameof(DeferredOwnershipStream));
			}
		}

		public override bool CanRead => Current.CanRead;
		public override bool CanSeek => Current.CanSeek;
		public override bool CanWrite => Current.CanWrite;
		public override long Length => Current.Length;
		public override long Position { get => Current.Position; set => Current.Position = value; }
		public override void Flush() => Current.Flush();
		public override int Read(byte[] buffer, int offset, int count) => Current.Read(buffer, offset, count);
		public override int Read(Span<byte> buffer) => Current.Read(buffer);
		public override int ReadByte() => Current.ReadByte();
		public override long Seek(long offset, SeekOrigin origin) => Current.Seek(offset, origin);
		public override void SetLength(long value) => Current.SetLength(value);
		public override void Write(byte[] buffer, int offset, int count) => Current.Write(buffer, offset, count);
		public override void Write(ReadOnlySpan<byte> buffer) => Current.Write(buffer);

		protected override void Dispose(bool disposing)
		{
			Stream close = null;
			if (disposing)
				lock (sync)
				{
					if (stream != null)
					{
						if (committed)
							close = stream;
						stream = null;
					}
				}

			close?.Dispose();
			base.Dispose(disposing);
		}
	}

	/// <summary>
	/// An independently disposable reference to one seekable stream identity. Segment reads use synchronized absolute
	/// offsets and never reopen a path or copy a complete segment.
	/// </summary>
	public sealed class RefCountedStreamLease : IDisposable
	{
		sealed class SharedState
		{
			readonly object sync = new();
			Stream stream;
			int references = 1;

			public long Length { get; }

			public SharedState(Stream ownedStream)
			{
				stream = ownedStream;
				Length = ownedStream.Length;
			}

			public void Acquire()
			{
				lock (sync)
				{
					if (stream == null)
						throw new ObjectDisposedException(nameof(RefCountedStreamLease));
					references = checked(references + 1);
				}
			}

			public int Read(long absoluteOffset, Span<byte> destination)
			{
				lock (sync)
				{
					if (stream == null)
						throw new ObjectDisposedException(nameof(RefCountedStreamLease));
					stream.Position = absoluteOffset;
					return stream.Read(destination);
				}
			}

			public void Release()
			{
				Stream close = null;
				lock (sync)
				{
					if (references <= 0)
						return;
					if (--references == 0)
					{
						close = stream;
						stream = null;
					}
				}

				close?.Dispose();
			}
		}

		sealed class PinnedSegmentStream : Stream
		{
			SharedState state;
			readonly long baseOffset;
			readonly long count;
			long position;

			public PinnedSegmentStream(SharedState state, long baseOffset, long count)
			{
				this.state = state;
				this.baseOffset = baseOffset;
				this.count = count;
			}

			public override bool CanRead => state != null;
			public override bool CanSeek => state != null;
			public override bool CanWrite => false;
			public override long Length => count;
			public override long Position
			{
				get => position;
				set
				{
					if (value < 0 || value > count)
						throw new ArgumentOutOfRangeException(nameof(value));
					position = value;
				}
			}

			public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

			public override int Read(Span<byte> buffer)
			{
				if (state == null)
					throw new ObjectDisposedException(nameof(PinnedSegmentStream));
				var remaining = this.count - position;
				if (remaining <= 0)
					return 0;
				var length = (int)Math.Min(remaining, buffer.Length);
				var read = state.Read(checked(baseOffset + position), buffer[..length]);
				position += read;
				return read;
			}

			public override int ReadByte()
			{
				Span<byte> value = stackalloc byte[1];
				return Read(value) == 0 ? -1 : value[0];
			}

			public override long Seek(long offset, SeekOrigin origin)
			{
				var target = origin switch
				{
					SeekOrigin.Begin => offset,
					SeekOrigin.Current => checked(position + offset),
					SeekOrigin.End => checked(count + offset),
					_ => throw new InvalidEnumArgumentException(nameof(origin), (int)origin, typeof(SeekOrigin)),
				};
				Position = target;
				return target;
			}

			public override void Flush() { }
			public override void SetLength(long value) => throw new NotSupportedException();
			public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
			protected override void Dispose(bool disposing)
			{
				if (disposing && state != null)
				{
					state.Release();
					state = null;
				}
				base.Dispose(disposing);
			}
		}

		SharedState state;

		RefCountedStreamLease(SharedState state)
		{
			this.state = state;
		}

		public long Length => (Volatile.Read(ref state) ??
			throw new ObjectDisposedException(nameof(RefCountedStreamLease))).Length;

		public static RefCountedStreamLease TakeOwnership(Stream ownedPinnedStream)
		{
			if (ownedPinnedStream == null)
				throw new ArgumentNullException(nameof(ownedPinnedStream));

			try
			{
				if (!ownedPinnedStream.CanRead || !ownedPinnedStream.CanSeek)
					throw new ArgumentException("The owned stream must be readable and seekable.", nameof(ownedPinnedStream));
				_ = ownedPinnedStream.Length;
				return new RefCountedStreamLease(new SharedState(ownedPinnedStream));
			}
			catch
			{
				ownedPinnedStream.Dispose();
				throw;
			}
		}

		public RefCountedStreamLease Acquire()
		{
			var current = Volatile.Read(ref state) ?? throw new ObjectDisposedException(nameof(RefCountedStreamLease));
			current.Acquire();
			return new RefCountedStreamLease(current);
		}

		public bool SharesIdentity(RefCountedStreamLease other) =>
			other != null && Volatile.Read(ref state) is { } current &&
			ReferenceEquals(current, Volatile.Read(ref other.state));

		public Stream OpenSegment(long offset, long count)
		{
			var current = Volatile.Read(ref state) ?? throw new ObjectDisposedException(nameof(RefCountedStreamLease));
			if (offset < 0)
				throw new ArgumentOutOfRangeException(nameof(offset));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if (checked(offset + count) > current.Length)
				throw new ArgumentException("The segment exceeds the pinned stream.", nameof(count));

			current.Acquire();
			return new PinnedSegmentStream(current, offset, count);
		}

		public void Dispose()
		{
			Interlocked.Exchange(ref state, null)?.Release();
			GC.SuppressFinalize(this);
		}
	}
}
