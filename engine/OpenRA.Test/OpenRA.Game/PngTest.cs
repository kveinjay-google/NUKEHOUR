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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using NUnit.Framework;
using OpenRA.FileFormats;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public class PngTests
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2")))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
		}

		[Test]
		public void TrustedBuiltInRa2ShellmapPreviewLoads()
		{
			var path = Path.Combine(RepositoryRoot(), "mods", "ra2", "maps", "blank-shellmap", "map.png");

			Assert.DoesNotThrow(() => new Png(File.OpenRead(path), PngReadLimits.TrustedSystemMapDefault));
		}

		[Test]
		public void BundledRa2ChromeSheetsLoad()
		{
			var directory = Path.Combine(RepositoryRoot(), "mods", "ra2", "uibits");
			foreach (var path in Directory.EnumerateFiles(directory, "*.png").OrderBy(path => path))
				Assert.DoesNotThrow(() => new Png(File.OpenRead(path), PngReadLimits.TrustedChromeDefault),
					$"Could not decode bundled chrome sheet '{Path.GetFileName(path)}'.");
		}

		[Test]
		public void Save_ShouldProduceValidPngFile()
		{
			// Arrange
			var colors = Enumerable.Range(0, 256).Select(i => Color.FromArgb(i, i, i)).ToArray();
			var png = new Png(new byte[10 * 20], SpriteFrameType.Indexed8, 10, 20, colors);

			// Act
			var result = png.Save();

			// Assert
			Assert.IsTrue(Png.Verify(new MemoryStream(result)));
		}

		[Test]
		public void Save_Method_Should_Write_Indexed8_Palette_If_256_Colors_Or_Less()
		{
			// Arrange
			var colors = Enumerable.Range(0, 256).Select(i => Color.FromArgb(i, i, i)).ToArray();
			var png = new Png(new byte[10 * 20], SpriteFrameType.Indexed8, 10, 20, colors);

			// Act
			var result = png.Save();

			// Assert
			// Byte at index 25 contains color type information
			// 0x03 represents Indexed8 with a palette
			var colorTypeByte = result[25];
			Assert.That(colorTypeByte, Is.EqualTo(0x03));
		}

		[Test]
		public void Save_Method_Should_Write_Rgba32_If_Alpha_Channel_Required()
		{
			// Arrange
			var png = new Png(new byte[10 * 20 * 4], SpriteFrameType.Rgba32, 10, 20);

			// Act
			var result = png.Save();

			// Assert
			// Byte at index 25 contains color type information
			// 0x06 represents RGBA32 with alpha
			var colorTypeByte = result[25];
			Assert.That(colorTypeByte, Is.EqualTo(0x06));
		}

		[Test]
		public void Save_ShouldThrowException_WhenDataLenghtNotEqualExpectedLenght()
		{
			// Arrange
			var colors = Enumerable.Range(0, 256).Select(i => Color.FromArgb(i, i, i)).ToArray();

			// Act
			void TestDelegate() => new Png(new byte[10 * 20], SpriteFrameType.Indexed8, 100, 20, colors);

			// Assert
			var ex = Assert.Throws<InvalidDataException>(TestDelegate);
			Assert.That(ex.Message, Is.EqualTo("Input data does not match expected length"));
		}

		[Test]
		public void PngConstructor_InvalidSignature_ThrowsInvalidDataException()
		{
			// Arrange
			byte[] invalidSignature = { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };

			// Act & Assert
			var exception = Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(invalidSignature)));
			StringAssert.Contains("PNG Signature is bogus", exception.Message);
		}

		[Test]
		public void PngConstructor_HeaderNotFirst_ThrowsInvalidDataException()
		{
			// Arrange
			var invalidPngData = new byte[]
			{
				0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
				0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4e, 0x44,
				0x00, 0x00, 0x00, 0x00,
			};

			// Act & Assert
			var exception = Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(invalidPngData)));
			StringAssert.Contains("Invalid PNG file - header does not appear first.", exception.Message);
		}

		[Test]
		public void PngConstructor_DuplicateIhdrHeader_ThrowsInvalidDataException()
		{
			var header = BuildHeader(1, 1);
			using var output = new MemoryStream();
			output.Write(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });
			WriteChunk(output, "IHDR", header);
			WriteChunk(output, "IHDR", header);

			Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(output.ToArray())));
		}

		[Test]
		public void PngConstructor_CompressionMethodNotSupported_ThrowsInvalidDataException()
		{
			var invalidPngData = BuildHeaderOnlyPng(1, 1, includeEnd: false, compression: 1);

			var exception = Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(invalidPngData)));
			Assert.That(exception.Message, Is.EqualTo("Compression method not supported"));
		}

		[Test]
		public void Constructor_ThrowsExceptionForNullPaletteIfTypeIsIndexed8()
		{
			// Arrange
			const int Width = 100;
			const int Height = 100;
			const SpriteFrameType Type = SpriteFrameType.Indexed8;

			// Act and Assert
			Assert.Throws<InvalidDataException>(() => new Png(new byte[Width * Height], Type, Width, Height, null));
		}

		[Test]
		public void TrustedDefaultIsFiniteAndExactlyPinned()
		{
			Assert.That(PngReadLimits.TrustedEngineDefault, Is.EqualTo(new PngReadLimits(
				4_096, 16_777_216, 64L << 20, 4_096, 32L << 20, 100)));
			Assert.That(PngReadLimits.TrustedSystemMapDefault, Is.EqualTo(new PngReadLimits(
				4_096, 16_777_216, 64L << 20, 4_096, 32L << 20, 512)));
			Assert.That(PngReadLimits.TrustedChromeDefault, Is.EqualTo(new PngReadLimits(
				4_096, 16_777_216, 64L << 20, 4_096, 32L << 20, 512)));
		}

		[Test]
		public void TrustedChromeDefaultAllowsHighlyCompressibleBundledUi()
		{
			const int Edge = 32;
			var bytes = new Png(new byte[Edge * Edge * 4], SpriteFrameType.Rgba32, Edge, Edge).Save();
			var idatBytes = FindChunkLength(bytes, "IDAT");
			var inflatedBytes = (Edge * 4 + 1) * Edge;
			var ratio = (inflatedBytes + idatBytes - 1) / idatBytes;

			Assert.That(ratio, Is.GreaterThan(PngReadLimits.TrustedEngineDefault.MaximumInflateRatio));
			Assert.That(ratio, Is.LessThanOrEqualTo(PngReadLimits.TrustedChromeDefault.MaximumInflateRatio));
			Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(bytes), PngReadLimits.TrustedEngineDefault));
			Assert.DoesNotThrow(() => new Png(new MemoryStream(bytes), PngReadLimits.TrustedChromeDefault));
		}


		[Test]
		public void ExplicitLimitsCoverEdgePixelsDecodedChunksIdatAndRatio()
		{
			var bytes = new Png(new byte[2 * 2 * 4], SpriteFrameType.Rgba32, 2, 2).Save();
			var generous = new PngReadLimits(2, 4, 16, 4, bytes.Length, 100);
			Assert.That(new Png(new MemoryStream(bytes), generous).Data, Has.Length.EqualTo(16));

			Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(bytes),
				new PngReadLimits(1, 4, 16, 4, bytes.Length, 100)));
			Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(bytes),
				new PngReadLimits(2, 3, 16, 4, bytes.Length, 100)));
			Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(bytes),
				new PngReadLimits(2, 4, 15, 4, bytes.Length, 100)));
			Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(bytes),
				new PngReadLimits(2, 4, 16, 2, bytes.Length, 100)));
			Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(bytes),
				new PngReadLimits(2, 4, 16, 4, 1, 100)));
			Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(bytes),
				new PngReadLimits(2, 4, 16, 4, bytes.Length, 1)));
		}

		[Test]
		public void LegacyAndMatchingExplicitDefaultHaveEqualBehavior()
		{
			var bytes = new Png(new byte[4], SpriteFrameType.Rgba32, 1, 1).Save();
			var legacy = new Png(new MemoryStream(bytes));
			var explicitPolicy = new Png(new MemoryStream(bytes), PngReadLimits.TrustedEngineDefault);

			Assert.That((legacy.Width, legacy.Height, legacy.Type, legacy.Data),
				Is.EqualTo((explicitPolicy.Width, explicitPolicy.Height, explicitPolicy.Type, explicitPolicy.Data)));
		}

		[Test]
		public void HugeIhdrAndContradictoryOrTruncatedChunksFailClosed()
		{
			var huge = BuildHeaderOnlyPng(int.MaxValue, int.MaxValue, includeEnd: true);
			Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(huge), PngReadLimits.TrustedEngineDefault));

			var truncated = BuildHeaderOnlyPng(1, 1, includeEnd: false);
			Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(truncated), PngReadLimits.TrustedEngineDefault));
		}

		[Test]
		public void OutputConstructorUsesCheckedDimensionArithmetic()
		{
			Assert.Throws<InvalidDataException>(() => new Png(Array.Empty<byte>(), SpriteFrameType.Rgba32,
				int.MaxValue, int.MaxValue));
		}

		[Test]
		public void IndexedFiltersAreAppliedToPackedBytesBeforeUnpacking()
		{
			using var output = new MemoryStream();
			output.Write(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });
			var header = BuildHeader(2, 2);
			header[8] = 1;
			header[9] = 3;
			WriteChunk(output, "IHDR", header);
			WriteChunk(output, "PLTE", new byte[] { 0, 0, 0, 255, 255, 255 });
			WriteChunk(output, "IDAT", Compress(new byte[] { 0, 0x40, 2, 0x40 }));
			WriteChunk(output, "IEND", Array.Empty<byte>());

			var png = new Png(new MemoryStream(output.ToArray()));
			Assert.That(png.Data, Is.EqualTo(new byte[] { 0, 1, 1, 0 }));
		}

		[TestCase(1, 0x80)]
		[TestCase(2, 0x40)]
		[TestCase(4, 0x10)]
		[TestCase(8, 0x01)]
		public void IndexedPixelsMustFitTheDeclaredPalette(byte bitDepth, byte packedPixel)
		{
			using var output = new MemoryStream();
			output.Write(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });
			var header = BuildHeader(1, 1);
			header[8] = bitDepth;
			header[9] = 3;
			WriteChunk(output, "IHDR", header);
			WriteChunk(output, "PLTE", new byte[] { 0, 0, 0 });
			WriteChunk(output, "IDAT", Compress(new byte[] { 0, packedPixel }));
			WriteChunk(output, "IEND", Array.Empty<byte>());

			Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(output.ToArray())));
		}

		[Test]
		public void RetainedTextUsesOneAggregateBudget()
		{
			var basePng = new Png(new byte[4], SpriteFrameType.Rgba32, 1, 1).Save();
			var exact = InsertAfterHeader(basePng, ("tEXt", new byte[] { (byte)'a', 0 }),
				("tEXt", new byte[] { (byte)'b', 0 }));
			var above = InsertAfterHeader(basePng, ("tEXt", new byte[] { (byte)'a', 0 }),
				("tEXt", new byte[] { (byte)'b', 0 }), ("tEXt", new byte[] { (byte)'c', 0 }));
			var limits = new PngReadLimits(1, 1, 4, 8, 128, 100);

			Assert.That(new Png(new MemoryStream(exact), limits).EmbeddedData, Has.Count.EqualTo(2));
			Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(above), limits));
		}

		[Test]
		public void EveryExplicitLimitCoversBelowExactAndAbove()
		{
			const int Edge = 64;
			var bytes = new Png(new byte[Edge * Edge * 4], SpriteFrameType.Rgba32, Edge, Edge).Save();
			var idatBytes = FindChunkLength(bytes, "IDAT");
			var decodedBytes = Edge * Edge * 4;
			var inflatedBytes = (Edge * 4 + 1) * Edge;
			var ratio = (inflatedBytes + idatBytes - 1) / idatBytes;
			Assert.That(ratio, Is.GreaterThan(1));

			Assert.Throws<InvalidDataException>(() => Read(bytes, Edge - 1, Edge * Edge,
				decodedBytes, 3, idatBytes, ratio));
			Assert.DoesNotThrow(() => Read(bytes, Edge, Edge * Edge, decodedBytes, 3, idatBytes, ratio));
			Assert.DoesNotThrow(() => Read(bytes, Edge + 1, Edge * Edge, decodedBytes, 3, idatBytes, ratio));

			Assert.Throws<InvalidDataException>(() => Read(bytes, Edge, Edge * Edge - 1,
				decodedBytes, 3, idatBytes, ratio));
			Assert.DoesNotThrow(() => Read(bytes, Edge, Edge * Edge, decodedBytes, 3, idatBytes, ratio));
			Assert.DoesNotThrow(() => Read(bytes, Edge, Edge * Edge + 1L, decodedBytes, 3, idatBytes, ratio));

			Assert.Throws<InvalidDataException>(() => Read(bytes, Edge, Edge * Edge,
				decodedBytes - 1, 3, idatBytes, ratio));
			Assert.DoesNotThrow(() => Read(bytes, Edge, Edge * Edge, decodedBytes, 3, idatBytes, ratio));
			Assert.DoesNotThrow(() => Read(bytes, Edge, Edge * Edge, decodedBytes + 1L, 3, idatBytes, ratio));

			Assert.Throws<InvalidDataException>(() => Read(bytes, Edge, Edge * Edge,
				decodedBytes, 2, idatBytes, ratio));
			Assert.DoesNotThrow(() => Read(bytes, Edge, Edge * Edge, decodedBytes, 3, idatBytes, ratio));
			Assert.DoesNotThrow(() => Read(bytes, Edge, Edge * Edge, decodedBytes, 4, idatBytes, ratio));

			Assert.Throws<InvalidDataException>(() => Read(bytes, Edge, Edge * Edge,
				decodedBytes, 3, idatBytes - 1, ratio));
			Assert.DoesNotThrow(() => Read(bytes, Edge, Edge * Edge, decodedBytes, 3, idatBytes, ratio));
			Assert.DoesNotThrow(() => Read(bytes, Edge, Edge * Edge, decodedBytes, 3, idatBytes + 1L, ratio));

			Assert.Throws<InvalidDataException>(() => Read(bytes, Edge, Edge * Edge,
				decodedBytes, 3, idatBytes, ratio - 1));
			Assert.DoesNotThrow(() => Read(bytes, Edge, Edge * Edge, decodedBytes, 3, idatBytes, ratio));
			Assert.DoesNotThrow(() => Read(bytes, Edge, Edge * Edge, decodedBytes, 3, idatBytes, ratio + 1));
		}

		[Test]
		public void CrcTrailingBytesAndDiscontinuousIdatFailClosed()
		{
			var valid = new Png(new byte[4], SpriteFrameType.Rgba32, 1, 1).Save();
			var corrupt = valid.ToArray();
			corrupt[29] ^= 1;
			Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(corrupt)));
			Assert.Throws<InvalidDataException>(() => new Png(
				new MemoryStream(valid.Concat(new byte[] { 0 }).ToArray())));

			var compressed = Compress(new byte[] { 0, 0, 0, 0, 0 });
			using var output = new MemoryStream();
			output.Write(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });
			WriteChunk(output, "IHDR", BuildHeader(1, 1));
			WriteChunk(output, "IDAT", compressed[..(compressed.Length / 2)]);
			WriteChunk(output, "tEXt", new byte[] { (byte)'a', 0 });
			WriteChunk(output, "IDAT", compressed[(compressed.Length / 2)..]);
			WriteChunk(output, "IEND", Array.Empty<byte>());
			Assert.Throws<InvalidDataException>(() => new Png(new MemoryStream(output.ToArray())));
		}

		static byte[] BuildHeaderOnlyPng(int width, int height, bool includeEnd, byte compression = 0)
		{
			using var output = new MemoryStream();
			output.Write(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });
			var header = BuildHeader(width, height);
			header[10] = compression;
			WriteChunk(output, "IHDR", header);
			if (includeEnd)
				WriteChunk(output, "IEND", Array.Empty<byte>());
			return output.ToArray();
		}

		static byte[] BuildHeader(int width, int height)
		{
			using var header = new MemoryStream();
			header.Write(IPAddress.HostToNetworkOrder(width));
			header.Write(IPAddress.HostToNetworkOrder(height));
			header.WriteByte(8);
			header.WriteByte(6);
			header.WriteByte(0);
			header.WriteByte(0);
			header.WriteByte(0);
			return header.ToArray();
		}

		static byte[] Compress(byte[] bytes)
		{
			using var output = new MemoryStream();
			using (var compressed = new DeflaterOutputStream(output, new Deflater(Deflater.BEST_COMPRESSION)))
			{
				compressed.Write(bytes, 0, bytes.Length);
				compressed.Finish();
			}

			return output.ToArray();
		}

		static byte[] InsertAfterHeader(byte[] png, params (string Type, byte[] Data)[] chunks)
		{
			const int HeaderEnd = 8 + 12 + 13;
			using var output = new MemoryStream();
			output.Write(png, 0, HeaderEnd);
			foreach (var chunk in chunks)
				WriteChunk(output, chunk.Type, chunk.Data);
			output.Write(png, HeaderEnd, png.Length - HeaderEnd);
			return output.ToArray();
		}

		static Png Read(byte[] bytes, int edge, long pixels, long decoded, int chunks,
			long idat, int ratio) => new(new MemoryStream(bytes),
				new PngReadLimits(edge, pixels, decoded, chunks, idat, ratio));

		static int FindChunkLength(byte[] png, string target)
		{
			var offset = 8;
			while (offset < png.Length)
			{
				var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(png, offset));
				var type = Encoding.ASCII.GetString(png, offset + 4, 4);
				if (type == target)
					return length;
				offset = checked(offset + 12 + length);
			}

			throw new InvalidDataException("Synthetic PNG chunk was not found.");
		}

		static void WriteChunk(Stream output, string type, byte[] content)
		{
			var typeBytes = Encoding.ASCII.GetBytes(type);
			output.Write(IPAddress.HostToNetworkOrder(content.Length));
			output.Write(typeBytes);
			output.Write(content);
			var crc = new Crc32();
			crc.Update(typeBytes);
			crc.Update(content);
			output.Write(IPAddress.HostToNetworkOrder((int)crc.Value));
		}
	}
}
