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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.FileFormats
{
	public class Png
	{
		static readonly byte[] Signature = { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };

		public int Width { get; }
		public int Height { get; }
		public Color[] Palette { get; }
		public byte[] Data { get; }
		public SpriteFrameType Type { get; }
		public Dictionary<string, string> EmbeddedData = new();

		public int PixelStride => Type == SpriteFrameType.Indexed8 ? 1 : Type == SpriteFrameType.Rgb24 ? 3 : 4;

		public Png(Stream s)
			: this(s, PngReadLimits.TrustedEngineDefault)
		{
		}

		public Png(Stream s, PngReadLimits limits)
		{
			if (s == null)
				throw new ArgumentNullException(nameof(s));
			if (limits == null)
				throw new ArgumentNullException(nameof(limits));
			if (!s.CanRead || !s.CanSeek)
				throw new ArgumentException("PNG input must be readable and seekable.", nameof(s));
			try
			{
				if (!Verify(s))
					throw new InvalidDataException("PNG Signature is bogus");

				s.Position += Signature.Length;
				var chunks = 0;
				var headerParsed = false;
				var paletteParsed = false;
				var transparencyParsed = false;
				var imageDataStarted = false;
				var imageDataEnded = false;
				var finished = false;
				using var compressedData = new MemoryStream();
				long idatBytes = 0;
				long retainedTextBytes = 0;
				byte bitDepth = 8;
				long decodedBytes = 0;
				Type = SpriteFrameType.Rgba32;

				while (!finished)
				{
					if (s.Length - s.Position < 12)
						throw new InvalidDataException("Invalid PNG file - truncated chunk header.");
					var length = ReadNetworkUInt32(s);
					if (checked((long)length + 8) > s.Length - s.Position)
						throw new InvalidDataException("Invalid PNG file - contradictory chunk length.");
					if (++chunks > limits.MaximumChunks)
						throw new InvalidDataException("PNG chunk limit exceeded.");

					var typeBytes = ReadExactBytes(s, 4);
					if (typeBytes.Any(value => !((value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z'))))
						throw new InvalidDataException("Invalid PNG chunk type.");
					var type = Encoding.ASCII.GetString(typeBytes);
					if (!headerParsed && type != "IHDR")
						throw new InvalidDataException("Invalid PNG file - header does not appear first.");
					if (imageDataStarted && type != "IDAT" && type != "IEND")
						imageDataEnded = true;
					if (imageDataEnded && type == "IDAT")
						throw new InvalidDataException("PNG IDAT chunks must be consecutive.");

					var crc = new Crc32();
					crc.Update(typeBytes);
					byte[] content = null;
					if (type == "IDAT")
					{
						if (!headerParsed || (Type == SpriteFrameType.Indexed8 && !paletteParsed))
							throw new InvalidDataException("PNG image data appears before required metadata.");
						var aggregate = checked(idatBytes + length);
						if (aggregate > limits.MaximumIdatBytes || aggregate > int.MaxValue)
							throw new InvalidDataException("PNG IDAT-byte limit exceeded.");
						CopyChunk(s, compressedData, length, crc);
						idatBytes = aggregate;
						imageDataStarted = true;
					}
					else
					{
						var allocationLimit = type switch
						{
							"IHDR" => 13L,
							"PLTE" => 768L,
							"tRNS" => 256L,
							"IEND" => 0L,
							"tEXt" => Math.Min(limits.MaximumIdatBytes, limits.MaximumDecodedBytes),
							_ => 0L,
						};
						if (type is "IHDR" or "PLTE" or "tRNS" or "IEND" or "tEXt")
						{
							if (type == "tEXt")
							{
								retainedTextBytes = checked(retainedTextBytes + length);
								if (retainedTextBytes > allocationLimit)
									throw new InvalidDataException("PNG retained-text aggregate limit exceeded.");
							}

							if (length > allocationLimit || length > int.MaxValue)
								throw new InvalidDataException("PNG chunk exceeds its allocation limit.");
							content = ReadExactBytes(s, (int)length);
							crc.Update(content);
						}
						else
						{
							if ((typeBytes[0] & 0x20) == 0)
								throw new InvalidDataException("Unsupported critical PNG chunk.");
							CopyChunk(s, null, length, crc);
						}
					}

					var expectedCrc = ReadNetworkUInt32(s);
					if ((uint)crc.Value != expectedCrc)
						throw new InvalidDataException("PNG chunk CRC does not match its contents.");

					using var ms = content == null ? null : new MemoryStream(content, writable: false);
					switch (type)
					{
						case "IHDR":
							if (headerParsed || length != 13)
								throw new InvalidDataException("Invalid PNG file - duplicate or malformed header.");
							Width = IPAddress.NetworkToHostOrder(ms.ReadInt32());
							Height = IPAddress.NetworkToHostOrder(ms.ReadInt32());
							bitDepth = ms.ReadUInt8();
							var colorType = (PngColorType)ms.ReadUInt8();
							if (IsPaletted(bitDepth, colorType))
								Type = SpriteFrameType.Indexed8;
							else if (colorType == PngColorType.Color)
								Type = SpriteFrameType.Rgb24;
							if (ms.ReadUInt8() != 0)
								throw new InvalidDataException("Compression method not supported");
							if (ms.ReadUInt8() != 0)
								throw new InvalidDataException("Filter method not supported");
							if (ms.ReadUInt8() != 0)
								throw new InvalidDataException("Interlacing not supported");
							if (Width <= 0 || Height <= 0 || Width > limits.MaximumEdge || Height > limits.MaximumEdge)
								throw new InvalidDataException("PNG dimensions exceed the configured edge limit.");
							var pixels = checked((long)Width * Height);
							if (pixels > limits.MaximumPixels)
								throw new InvalidDataException("PNG pixel limit exceeded.");
							decodedBytes = checked(pixels * PixelStride);
							if (decodedBytes > limits.MaximumDecodedBytes || decodedBytes > int.MaxValue)
								throw new InvalidDataException("PNG decoded-byte limit exceeded.");
							headerParsed = true;
							break;

						case "PLTE":
							if (paletteParsed || imageDataStarted || length == 0 || length % 3 != 0 || length > 768)
								throw new InvalidDataException("Invalid PNG palette.");
							Palette = new Color[length / 3];
							for (var i = 0; i < Palette.Length; i++)
								Palette[i] = Color.FromArgb(ms.ReadUInt8(), ms.ReadUInt8(), ms.ReadUInt8());
							paletteParsed = true;
							break;

						case "tRNS":
							if (transparencyParsed || imageDataStarted || Palette == null || length > Palette.Length)
								throw new InvalidDataException("Non-Palette indexed PNG are not supported.");
							for (var i = 0; i < length; i++)
								Palette[i] = Color.FromArgb(ms.ReadUInt8(), Palette[i]);
							transparencyParsed = true;
							break;

						case "tEXt":
							var separator = Array.IndexOf(content, (byte)0);
							if (separator <= 0 || separator > 79 ||
								content.Any(value => value > 0x7F))
								throw new InvalidDataException("Invalid PNG text chunk.");
							var key = Encoding.ASCII.GetString(content, 0, separator);
							if (EmbeddedData.ContainsKey(key))
								throw new InvalidDataException("Duplicate PNG text key.");
							EmbeddedData.Add(key, Encoding.ASCII.GetString(content, separator + 1,
								content.Length - separator - 1));
							break;

						case "IEND":
							if (length != 0 || !imageDataStarted || idatBytes == 0)
								throw new InvalidDataException("Invalid PNG end or missing image data.");
							finished = true;
							break;
					}
				}

				if (s.Position != s.Length)
					throw new InvalidDataException("PNG contains trailing bytes after IEND.");
				if (Type == SpriteFrameType.Indexed8 && Palette == null)
					throw new InvalidDataException("Non-Palette indexed PNG are not supported.");
				if (Type == SpriteFrameType.Indexed8 && Palette.Length > 1 << bitDepth)
					throw new InvalidDataException("PNG palette has more entries than its indexed bit depth can address.");

				var rowStride = checked(Width * PixelStride);
				var pixelsPerByte = 8 / bitDepth;
				var sourceRowStride = Type == SpriteFrameType.Indexed8 ?
					checked((Width * bitDepth + 7) / 8) : rowStride;
				var expectedInflated = checked((long)(sourceRowStride + 1) * Height);
				var maximumInflated = checked(limits.MaximumDecodedBytes + Height);
				if (expectedInflated > maximumInflated)
					throw new InvalidDataException("PNG inflated row data exceeds the configured limit.");
				if (expectedInflated > checked(idatBytes * limits.MaximumInflateRatio))
					throw new InvalidDataException("PNG inflate-ratio limit exceeded.");

				Data = new byte[(int)decodedBytes];
				compressedData.Position = 0;
				using var decoded = new InflaterInputStream(compressedData);
				var previous = new byte[sourceRowStride];
				var packed = bitDepth < 8 ? new byte[sourceRowStride] : null;
				for (var y = 0; y < Height; y++)
				{
					var filterValue = decoded.ReadByte();
					if (filterValue < 0)
						throw new InvalidDataException("PNG image data is truncated.");
					var line = Data.AsSpan(y * rowStride, rowStride);
					if (bitDepth < 8)
					{
						ReadExact(decoded, packed, 0, sourceRowStride);
						ApplyFilter((PngFilter)filterValue, packed, previous, 1);
						packed.AsSpan().CopyTo(line);
						Unpack(line, sourceRowStride, bitDepth, pixelsPerByte);
						packed.AsSpan().CopyTo(previous);
					}
					else
					{
						ReadExact(decoded, Data, checked(y * rowStride), sourceRowStride);
						ApplyFilter((PngFilter)filterValue, line, previous, PixelStride);
						line.CopyTo(previous);
					}
				}

				if (decoded.ReadByte() != -1)
					throw new InvalidDataException("PNG decoded data exceeds the expected image size.");
				if (Type == SpriteFrameType.Indexed8 && Data.Any(index => index >= Palette.Length))
					throw new InvalidDataException("PNG pixel index exceeds the declared palette.");
			}
			catch (InvalidDataException)
			{
				throw;
			}
			catch (OverflowException ex)
			{
				throw new InvalidDataException("PNG envelope arithmetic overflowed.", ex);
			}
			catch (Exception ex) when (ex is EndOfStreamException || ex is IOException ||
				ex is SharpZipBaseException || ex is ArgumentException)
			{
				throw new InvalidDataException("PNG data is malformed or truncated.", ex);
			}
		}

		public Png(byte[] data, SpriteFrameType type, int width, int height, Color[] palette = null,
			Dictionary<string, string> embeddedData = null)
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));
			if (width <= 0 || height <= 0)
				throw new InvalidDataException("Image dimensions must be positive.");
			int stride;
			switch (type)
			{
				case SpriteFrameType.Indexed8: stride = 1; break;
				case SpriteFrameType.Rgb24:
				case SpriteFrameType.Bgr24: stride = 3; break;
				case SpriteFrameType.Rgba32:
				case SpriteFrameType.Bgra32: stride = 4; break;
				default: throw new InvalidDataException($"Unhandled SpriteFrameType {type}");
			}

			int expectLength;
			try
			{
				expectLength = checked(width * height * stride);
			}
			catch (OverflowException ex)
			{
				throw new InvalidDataException("Image dimensions overflow the decoded allocation.", ex);
			}

			if (data.Length != expectLength)
				throw new InvalidDataException("Input data does not match expected length");
			if (type == SpriteFrameType.Indexed8 && (palette == null || palette.Length == 0 || palette.Length > 256))
				throw new InvalidDataException("Indexed images require a palette of 1 through 256 colors.");

			Type = type;
			Width = width;
			Height = height;

			switch (type)
			{
				case SpriteFrameType.Indexed8:
				case SpriteFrameType.Rgba32:
				case SpriteFrameType.Rgb24:
				{
					// Data is already in a compatible format
					Data = data;
					if (type == SpriteFrameType.Indexed8)
						Palette = palette;

					break;
				}

				case SpriteFrameType.Bgra32:
				case SpriteFrameType.Bgr24:
				{
					// Convert to big endian
					Data = new byte[data.Length];
					var conversionStride = PixelStride;
					for (var i = 0; i < width * height; i++)
					{
						Data[conversionStride * i] = data[conversionStride * i + 2];
						Data[conversionStride * i + 1] = data[conversionStride * i + 1];
						Data[conversionStride * i + 2] = data[conversionStride * i + 0];

						if (type == SpriteFrameType.Bgra32)
							Data[conversionStride * i + 3] = data[conversionStride * i + 3];
					}

					break;
				}

				default:
					throw new InvalidDataException($"Unhandled SpriteFrameType {type}");
			}

			if (embeddedData != null)
				EmbeddedData = embeddedData;
		}

		public static bool Verify(Stream s)
		{
			if (s == null)
				throw new ArgumentNullException(nameof(s));
			if (!s.CanRead || !s.CanSeek)
				return false;

			var pos = s.Position;
			try
			{
				for (var i = 0; i < Signature.Length; i++)
					if (s.ReadByte() != Signature[i])
						return false;
				return true;
			}
			finally
			{
				s.Position = pos;
			}
		}

		[Flags]
		enum PngColorType : byte { Indexed = 1, Color = 2, Alpha = 4 }
		enum PngFilter : byte { None, Sub, Up, Average, Paeth }

		static bool IsPaletted(byte bitDepth, PngColorType colorType)
		{
			if ((bitDepth == 1 || bitDepth == 2 || bitDepth == 4 || bitDepth == 8) &&
				colorType == (PngColorType.Indexed | PngColorType.Color))
				return true;

			if (bitDepth == 8 && colorType == (PngColorType.Color | PngColorType.Alpha))
				return false;

			if (bitDepth == 8 && colorType == PngColorType.Color)
				return false;

			throw new InvalidDataException("Unknown pixel format");
		}

		static uint ReadNetworkUInt32(Stream stream)
		{
			var bytes = ReadExactBytes(stream, 4);
			return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
		}

		static byte[] ReadExactBytes(Stream stream, int count)
		{
			var result = new byte[count];
			ReadExact(stream, result, 0, count);
			return result;
		}

		static void ReadExact(Stream stream, byte[] buffer, int offset, int count)
		{
			while (count > 0)
			{
				var read = stream.Read(buffer, offset, count);
				if (read == 0)
					throw new InvalidDataException("PNG data is truncated.");
				offset += read;
				count -= read;
			}
		}

		static void CopyChunk(Stream input, Stream output, uint length, Crc32 crc)
		{
			var buffer = new byte[64 << 10];
			long remaining = length;
			while (remaining > 0)
			{
				var requested = (int)Math.Min(buffer.Length, remaining);
				var read = input.Read(buffer, 0, requested);
				if (read == 0)
					throw new InvalidDataException("PNG chunk data is truncated.");
				crc.Update(new ArraySegment<byte>(buffer, 0, read));
				output?.Write(buffer, 0, read);
				remaining -= read;
			}
		}

		static void Unpack(Span<byte> line, int sourceRowStride, byte bitDepth, int pixelsPerByte)
		{
			var mask = 0xFF >> (8 - bitDepth);
			for (var i = sourceRowStride - 1; i >= 0; i--)
			{
				var packed = line[i];
				for (var j = 0; j < pixelsPerByte; j++)
				{
					var destination = i * pixelsPerByte + j;
					if (destination < line.Length)
						line[destination] = (byte)(packed >> (8 - (j + 1) * bitDepth) & mask);
				}
			}
		}

		static void ApplyFilter(PngFilter filter, Span<byte> line, ReadOnlySpan<byte> previous, int pixelStride)
		{
			switch (filter)
			{
				case PngFilter.None:
					return;
				case PngFilter.Sub:
					for (var i = pixelStride; i < line.Length; i++)
						line[i] += line[i - pixelStride];
					return;
				case PngFilter.Up:
					for (var i = 0; i < line.Length; i++)
						line[i] += previous[i];
					return;
				case PngFilter.Average:
					for (var i = 0; i < pixelStride; i++)
						line[i] += (byte)(previous[i] / 2);
					for (var i = pixelStride; i < line.Length; i++)
						line[i] += (byte)((line[i - pixelStride] + previous[i]) / 2);
					return;
				case PngFilter.Paeth:
					for (var i = 0; i < pixelStride; i++)
						line[i] += Paeth(0, previous[i], 0);
					for (var i = pixelStride; i < line.Length; i++)
						line[i] += Paeth(line[i - pixelStride], previous[i], previous[i - pixelStride]);
					return;
				default:
					throw new InvalidDataException("Unsupported PNG filter.");
			}
		}

		static byte Paeth(byte a, byte b, byte c)
		{
			var p = a + b - c;
			var pa = Math.Abs(p - a);
			var pb = Math.Abs(p - b);
			var pc = Math.Abs(p - c);
			return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
		}

		static void WritePngChunk(Stream output, string type, Stream input)
		{
			input.Position = 0;

			var typeBytes = Encoding.ASCII.GetBytes(type);
			output.Write(IPAddress.HostToNetworkOrder((int)input.Length));
			output.Write(typeBytes);

			var data = input.ReadAllBytes();
			output.Write(data);

			var crc32 = new Crc32();
			crc32.Update(typeBytes);
			crc32.Update(data);
			output.Write(IPAddress.NetworkToHostOrder((int)crc32.Value));
		}

		public byte[] Save()
		{
			using (var output = new MemoryStream())
			{
				output.Write(Signature);
				using (var header = new MemoryStream())
				{
					header.Write(IPAddress.HostToNetworkOrder(Width));
					header.Write(IPAddress.HostToNetworkOrder(Height));
					header.WriteByte(8); // Bit depth

					var colorType = Type == SpriteFrameType.Indexed8 ? PngColorType.Indexed | PngColorType.Color :
						Type == SpriteFrameType.Rgb24 ? PngColorType.Color : PngColorType.Color | PngColorType.Alpha;
					header.WriteByte((byte)colorType);

					header.WriteByte(0); // Compression
					header.WriteByte(0); // Filter
					header.WriteByte(0); // Interlacing

					WritePngChunk(output, "IHDR", header);
				}

				var alphaPalette = false;
				if (Palette != null)
				{
					using (var palette = new MemoryStream())
					{
						foreach (var c in Palette)
						{
							palette.WriteByte(c.R);
							palette.WriteByte(c.G);
							palette.WriteByte(c.B);
							alphaPalette |= c.A > 0;
						}

						WritePngChunk(output, "PLTE", palette);
					}
				}

				if (alphaPalette)
				{
					using (var alpha = new MemoryStream())
					{
						foreach (var c in Palette)
							alpha.WriteByte(c.A);

						WritePngChunk(output, "tRNS", alpha);
					}
				}

				using (var data = new MemoryStream())
				{
					using (var compressed = new DeflaterOutputStream(data, new Deflater(Deflater.BEST_COMPRESSION)))
					{
						var rowStride = Width * PixelStride;
						for (var y = 0; y < Height; y++)
						{
							// Assuming no filtering for simplicity
							const byte FilterType = 0;
							compressed.WriteByte(FilterType);
							compressed.Write(Data, y * rowStride, rowStride);
						}

						compressed.Flush();
						compressed.Finish();

						WritePngChunk(output, "IDAT", data);
					}
				}

				foreach (var kv in EmbeddedData)
				{
					using (var text = new MemoryStream())
					{
						text.Write(Encoding.ASCII.GetBytes(kv.Key + (char)0 + kv.Value));
						WritePngChunk(output, "tEXt", text);
					}
				}

				WritePngChunk(output, "IEND", new MemoryStream());
				return output.ToArray();
			}
		}

		public void Save(string path)
		{
			File.WriteAllBytes(path, Save());
		}
	}
}
