// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Files
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Codec;
    using Codec.Archives;
    using Codec.Services;
    using ImageMagick;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (uint TextureId, uint Index);
    using PaletteEntry = (byte R, byte G, byte B, byte A);

    internal class TplFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton(new EntryTypeMatcher(EntryType.Image, "*.tplx"));

            services.AddFileSystem(
                "*.tpl",
                static (serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
                {
                    using var stream = parent.File.OpenRead(parentRelativePath);
                    var header = stream.ReadBigEndian<Header>();
                    if (header.Padding1 != 0 || header.Padding2 != 0 || header.Padding3 != 0)
                    {
                        return false;
                    }

                    stream.Position = (long)header.TplOffset;
                    var signature = stream.ReadUInt32BigEndian();
                    return signature == 0x0020af30;
                },
                static (fullPath, parentRelativePath, parent, parentPath) => new TxpFileFileSystem(parentRelativePath, parent));

            services.AddSingleton<FileHandlerResolver<MagickImage>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (parent is TxpFileFileSystem)
                {
                    return new((fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var file = parent.File.OpenRead(parentRelativePath);
                        var id = Convert.ToUInt32(parent.Path.GetFileNameWithoutExtension(parentRelativePath), 16);
                        return Load(file, id);
                    });
                }

                return null;
            });
        }

        private class TxpFileFileSystem(string path, IFileSystem parent) : IndexedFileSystem<Entry>
        {
            private readonly IFileSystem parent = parent ?? new FileSystem();
            private readonly string path = path;

            protected override IEnumerable<Entry> ReadIndex()
            {
                using var stream = this.parent.File.OpenRead(this.path);
                var header = stream.ReadBigEndian<Header>();
                var textures = stream.ReadArrayBigEndian<TextureDef>(header.TextureCount);
                return Array.ConvertAll(textures, t => (t.TextureId, (uint)t.TextureIndex));
            }

            protected override string GetEntryName(Entry entry) =>
                $"{entry.TextureId:x6}.tplx";

            protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
            {
                // HACK: Each sub-file loads the whole file using the filename to locate the entry.
                FileBase.EnsureReadOnly(parentOptions, "Writing to sub images in .txp files is not supported.");
                return this.parent.File.Open(this.path, parentOptions);
            }
        }

        public static MagickImage? Load(Stream stream, uint textureId)
        {
            var header = stream.ReadBigEndian<Header>();
            var textures = stream.ReadArrayBigEndian<TextureDef>(header.TextureCount);
            stream.Position = (long)header.TplOffset;
            var tplHeader = stream.ReadBigEndian<TplHeader>();
            var tplOffsets = stream.ReadArrayBigEndian<Offsets>(tplHeader.ImageCount);
            for (var t = 0; t < header.TextureCount; t++)
            {
                var texture = textures[t];
                if (texture.TextureId != textureId)
                {
                    continue;
                }

                var offsets = tplOffsets[texture.TextureIndex];
                stream.Position = (long)(header.TplOffset + offsets.ImageOffset);
                var imageHeader = stream.ReadBigEndian<ImageHeader>();

                // NOTE: Height comes first in the on-disk image header. Transposing width/height
                // here shears every tiled decode into diagonal noise.
                var width = (int)imageHeader.Width;
                var height = (int)imageHeader.Height;
                var imageDataOffset = (long)header.TplOffset + imageHeader.DataOffset;

                PaletteEntry[]? palette = null;
                if (offsets.PaletteOffset != 0)
                {
                    stream.Position = (long)(header.TplOffset + offsets.PaletteOffset);
                    var paletteHeader = stream.ReadBigEndian<PaletteHeader>();
                    var paletteDataOffset = (long)header.TplOffset + paletteHeader.DataOffset;
                    var rawPalette = ReadPaletteEntries(stream, paletteDataOffset, paletteHeader.EntryCount);

                    palette = paletteHeader.Format switch
                    {
                        0 => DecodePaletteIA8(rawPalette),
                        1 => DecodePaletteRgb565(rawPalette),
                        2 => DecodePaletteRgb5A3(rawPalette),
                        _ => throw new NotImplementedException($"Unsupported TPL Palette Format '{paletteHeader.Format:x2}'."),
                    };
                }

                var pixels = imageHeader.Format switch
                {
                    0x00 => DecodeI4(stream, imageDataOffset, width, height),
                    0x01 => DecodeI8(stream, imageDataOffset, width, height),
                    0x02 => DecodeIA4(stream, imageDataOffset, width, height),
                    0x03 => DecodeIA8(stream, imageDataOffset, width, height),
                    0x04 => DecodeRgb565(stream, imageDataOffset, width, height),
                    0x05 => DecodeRgb5A3(stream, imageDataOffset, width, height),
                    0x06 => DecodeRgba8(stream, imageDataOffset, width, height),
                    0x08 => DecodeColorIndexed(stream, imageDataOffset, width, height, tileW: 8, tileH: 8, bpp: 4, palette),
                    0x09 => DecodeColorIndexed(stream, imageDataOffset, width, height, tileW: 8, tileH: 4, bpp: 8, palette),
                    0x0A => DecodeColorIndexed14X2(stream, imageDataOffset, width, height, palette),
                    0x0E => DecodeCmpr(stream, imageDataOffset, width, height),
                    _ => throw new NotImplementedException($"Unsupported TPL Image Format '{imageHeader.Format:x2}'."),
                };

                var settings = new PixelReadSettings((uint)width, (uint)height, StorageType.Char, PixelMapping.RGBA);
                return new MagickImage(pixels, settings);
            }

            return null;
        }

        private static byte[] ReadBytes(Stream stream, long offset, int count)
        {
            stream.Position = offset;
            var buffer = new byte[count];
            stream.ReadExactly(buffer);
            return buffer;
        }

        private static int TiledSize(int width, int height, int tileW, int tileH, int bytesPerTile)
        {
            var tilesX = (width + tileW - 1) / tileW;
            var tilesY = (height + tileH - 1) / tileH;
            return tilesX * tilesY * bytesPerTile;
        }

        private static void SetPixel(byte[] output, int width, int x, int y, byte r, byte g, byte b, byte a)
        {
            var i = ((y * width) + x) * 4;
            output[i] = r;
            output[i + 1] = g;
            output[i + 2] = b;
            output[i + 3] = a;
        }

        private static ushort[] ReadPaletteEntries(Stream stream, long offset, int count)
        {
            var bytes = ReadBytes(stream, offset, count * 2);
            var entries = new ushort[count];
            for (var i = 0; i < count; i++)
            {
                entries[i] = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(i * 2, 2));
            }

            return entries;
        }

        private static PaletteEntry[] DecodePaletteIA8(ushort[] raw)
        {
            var result = new PaletteEntry[raw.Length];
            for (var i = 0; i < raw.Length; i++)
            {
                var v = raw[i];
                var intensity = (byte)(v >> 8);
                var alpha = (byte)(v & 0xFF);
                result[i] = (intensity, intensity, intensity, alpha);
            }

            return result;
        }

        private static PaletteEntry[] DecodePaletteRgb565(ushort[] raw)
        {
            var result = new PaletteEntry[raw.Length];
            for (var i = 0; i < raw.Length; i++)
            {
                result[i] = DecodeRgb565Pixel(raw[i]);
            }

            return result;
        }

        private static PaletteEntry[] DecodePaletteRgb5A3(ushort[] raw)
        {
            var result = new PaletteEntry[raw.Length];
            for (var i = 0; i < raw.Length; i++)
            {
                result[i] = DecodeRgb5A3Pixel(raw[i]);
            }

            return result;
        }

        private static PaletteEntry DecodeRgb565Pixel(ushort v)
        {
            var r = (byte)(((v >> 11) & 0x1F) * 255 / 31);
            var g = (byte)(((v >> 5) & 0x3F) * 255 / 63);
            var b = (byte)((v & 0x1F) * 255 / 31);
            return (r, g, b, 255);
        }

        private static PaletteEntry DecodeRgb5A3Pixel(ushort v)
        {
            if ((v & 0x8000) != 0)
            {
                var r = (byte)(((v >> 10) & 0x1F) * 255 / 31);
                var g = (byte)(((v >> 5) & 0x1F) * 255 / 31);
                var b = (byte)((v & 0x1F) * 255 / 31);
                return (r, g, b, 255);
            }
            else
            {
                var a = (byte)(((v >> 12) & 0x7) * 255 / 7);
                var r = (byte)(((v >> 8) & 0xF) * 17);
                var g = (byte)(((v >> 4) & 0xF) * 17);
                var b = (byte)((v & 0xF) * 17);
                return (r, g, b, a);
            }
        }

        private static byte[] DecodeI4(Stream stream, long offset, int width, int height)
        {
            const int tileW = 8, tileH = 8;
            var size = TiledSize(width, height, tileW, tileH, tileW * tileH / 2);
            var data = ReadBytes(stream, offset, size);
            var output = new byte[width * height * 4];
            var p = 0;
            for (var ty = 0; ty < height; ty += tileH)
            {
                for (var tx = 0; tx < width; tx += tileW)
                {
                    for (var py = 0; py < tileH; py++)
                    {
                        for (var px = 0; px < tileW; px += 2)
                        {
                            var v = data[p++];
                            var hi = (byte)((v >> 4) * 17);
                            var lo = (byte)((v & 0xF) * 17);
                            var x0 = tx + px;
                            var y0 = ty + py;
                            if (x0 < width && y0 < height)
                            {
                                SetPixel(output, width, x0, y0, hi, hi, hi, 255);
                            }

                            var x1 = tx + px + 1;
                            if (x1 < width && y0 < height)
                            {
                                SetPixel(output, width, x1, y0, lo, lo, lo, 255);
                            }
                        }
                    }
                }
            }

            return output;
        }

        private static byte[] DecodeI8(Stream stream, long offset, int width, int height)
        {
            const int tileW = 8, tileH = 4;
            var size = TiledSize(width, height, tileW, tileH, tileW * tileH);
            var data = ReadBytes(stream, offset, size);
            var output = new byte[width * height * 4];
            var p = 0;
            for (var ty = 0; ty < height; ty += tileH)
            {
                for (var tx = 0; tx < width; tx += tileW)
                {
                    for (var py = 0; py < tileH; py++)
                    {
                        for (var px = 0; px < tileW; px++)
                        {
                            var v = data[p++];
                            var x = tx + px;
                            var y = ty + py;
                            if (x < width && y < height)
                            {
                                SetPixel(output, width, x, y, v, v, v, 255);
                            }
                        }
                    }
                }
            }

            return output;
        }

        private static byte[] DecodeIA4(Stream stream, long offset, int width, int height)
        {
            const int tileW = 8, tileH = 4;
            var size = TiledSize(width, height, tileW, tileH, tileW * tileH);
            var data = ReadBytes(stream, offset, size);
            var output = new byte[width * height * 4];
            var p = 0;
            for (var ty = 0; ty < height; ty += tileH)
            {
                for (var tx = 0; tx < width; tx += tileW)
                {
                    for (var py = 0; py < tileH; py++)
                    {
                        for (var px = 0; px < tileW; px++)
                        {
                            var v = data[p++];
                            var i = (byte)((v & 0xF) * 17);
                            var a = (byte)((v >> 4) * 17);
                            var x = tx + px;
                            var y = ty + py;
                            if (x < width && y < height)
                            {
                                SetPixel(output, width, x, y, i, i, i, a);
                            }
                        }
                    }
                }
            }

            return output;
        }

        private static byte[] DecodeIA8(Stream stream, long offset, int width, int height)
        {
            const int tileW = 4, tileH = 4;
            var size = TiledSize(width, height, tileW, tileH, tileW * tileH * 2);
            var data = ReadBytes(stream, offset, size);
            var output = new byte[width * height * 4];
            var p = 0;
            for (var ty = 0; ty < height; ty += tileH)
            {
                for (var tx = 0; tx < width; tx += tileW)
                {
                    for (var py = 0; py < tileH; py++)
                    {
                        for (var px = 0; px < tileW; px++)
                        {
                            var v = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p, 2));
                            p += 2;
                            var i = (byte)(v >> 8);
                            var a = (byte)(v & 0xFF);
                            var x = tx + px;
                            var y = ty + py;
                            if (x < width && y < height)
                            {
                                SetPixel(output, width, x, y, i, i, i, a);
                            }
                        }
                    }
                }
            }

            return output;
        }

        private static byte[] DecodeRgb565(Stream stream, long offset, int width, int height)
        {
            const int tileW = 4, tileH = 4;
            var size = TiledSize(width, height, tileW, tileH, tileW * tileH * 2);
            var data = ReadBytes(stream, offset, size);
            var output = new byte[width * height * 4];
            var p = 0;
            for (var ty = 0; ty < height; ty += tileH)
            {
                for (var tx = 0; tx < width; tx += tileW)
                {
                    for (var py = 0; py < tileH; py++)
                    {
                        for (var px = 0; px < tileW; px++)
                        {
                            var v = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p, 2));
                            p += 2;
                            var c = DecodeRgb565Pixel(v);
                            var x = tx + px;
                            var y = ty + py;
                            if (x < width && y < height)
                            {
                                SetPixel(output, width, x, y, c.R, c.G, c.B, c.A);
                            }
                        }
                    }
                }
            }

            return output;
        }

        private static byte[] DecodeRgb5A3(Stream stream, long offset, int width, int height)
        {
            const int tileW = 4, tileH = 4;
            var size = TiledSize(width, height, tileW, tileH, tileW * tileH * 2);
            var data = ReadBytes(stream, offset, size);
            var output = new byte[width * height * 4];
            var p = 0;
            for (var ty = 0; ty < height; ty += tileH)
            {
                for (var tx = 0; tx < width; tx += tileW)
                {
                    for (var py = 0; py < tileH; py++)
                    {
                        for (var px = 0; px < tileW; px++)
                        {
                            var v = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p, 2));
                            p += 2;
                            var c = DecodeRgb5A3Pixel(v);
                            var x = tx + px;
                            var y = ty + py;
                            if (x < width && y < height)
                            {
                                SetPixel(output, width, x, y, c.R, c.G, c.B, c.A);
                            }
                        }
                    }
                }
            }

            return output;
        }

        private static byte[] DecodeRgba8(Stream stream, long offset, int width, int height)
        {
            const int tileW = 4, tileH = 4;
            var size = TiledSize(width, height, tileW, tileH, 64);
            var data = ReadBytes(stream, offset, size);
            var output = new byte[width * height * 4];
            var p = 0;
            for (var ty = 0; ty < height; ty += tileH)
            {
                for (var tx = 0; tx < width; tx += tileW)
                {
                    var arOffset = p;
                    var gbOffset = p + 32;
                    var idx = 0;
                    for (var py = 0; py < tileH; py++)
                    {
                        for (var px = 0; px < tileW; px++)
                        {
                            var a = data[arOffset + (idx * 2)];
                            var r = data[arOffset + (idx * 2) + 1];
                            var g = data[gbOffset + (idx * 2)];
                            var b = data[gbOffset + (idx * 2) + 1];
                            var x = tx + px;
                            var y = ty + py;
                            if (x < width && y < height)
                            {
                                SetPixel(output, width, x, y, r, g, b, a);
                            }

                            idx++;
                        }
                    }

                    p += 64;
                }
            }

            return output;
        }

        private static void WriteIndexed(byte[] output, int width, int height, int x, int y, int index, PaletteEntry[] palette)
        {
            if (x >= width || y >= height)
            {
                return;
            }

            var color = (uint)index < (uint)palette.Length ? palette[index] : default;
            SetPixel(output, width, x, y, color.R, color.G, color.B, color.A);
        }

        private static byte[] DecodeColorIndexed(Stream stream, long offset, int width, int height, int tileW, int tileH, int bpp, PaletteEntry[]? palette)
        {
            if (palette == null)
            {
                throw new InvalidDataException("Color-indexed TPL image has no associated palette.");
            }

            var bytesPerTile = tileW * tileH * bpp / 8;
            var size = TiledSize(width, height, tileW, tileH, bytesPerTile);
            var data = ReadBytes(stream, offset, size);
            var output = new byte[width * height * 4];
            var p = 0;
            for (var ty = 0; ty < height; ty += tileH)
            {
                for (var tx = 0; tx < width; tx += tileW)
                {
                    if (bpp == 4)
                    {
                        for (var py = 0; py < tileH; py++)
                        {
                            for (var px = 0; px < tileW; px += 2)
                            {
                                var v = data[p++];
                                WriteIndexed(output, width, height, tx + px, ty + py, v >> 4, palette);
                                WriteIndexed(output, width, height, tx + px + 1, ty + py, v & 0xF, palette);
                            }
                        }
                    }
                    else
                    {
                        for (var py = 0; py < tileH; py++)
                        {
                            for (var px = 0; px < tileW; px++)
                            {
                                var v = data[p++];
                                WriteIndexed(output, width, height, tx + px, ty + py, v, palette);
                            }
                        }
                    }
                }
            }

            return output;
        }

        private static byte[] DecodeColorIndexed14X2(Stream stream, long offset, int width, int height, PaletteEntry[]? palette)
        {
            if (palette == null)
            {
                throw new InvalidDataException("Color-indexed TPL image has no associated palette.");
            }

            const int tileW = 4, tileH = 4;
            var size = TiledSize(width, height, tileW, tileH, tileW * tileH * 2);
            var data = ReadBytes(stream, offset, size);
            var output = new byte[width * height * 4];
            var p = 0;
            for (var ty = 0; ty < height; ty += tileH)
            {
                for (var tx = 0; tx < width; tx += tileW)
                {
                    for (var py = 0; py < tileH; py++)
                    {
                        for (var px = 0; px < tileW; px++)
                        {
                            var v = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p, 2));
                            p += 2;
                            var index = v & 0x3FFF;
                            WriteIndexed(output, width, height, tx + px, ty + py, index, palette);
                        }
                    }
                }
            }

            return output;
        }

        private static byte[] DecodeCmpr(Stream stream, long offset, int width, int height)
        {
            const int tileW = 8, tileH = 8;
            var size = TiledSize(width, height, tileW, tileH, 32);
            var data = ReadBytes(stream, offset, size);
            var output = new byte[width * height * 4];
            var p = 0;
            for (var ty = 0; ty < height; ty += tileH)
            {
                for (var tx = 0; tx < width; tx += tileW)
                {
                    for (var sy = 0; sy < tileH; sy += 4)
                    {
                        for (var sx = 0; sx < tileW; sx += 4)
                        {
                            var c0 = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p, 2));
                            var c1 = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p + 2, 2));
                            var bits = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(p + 4, 4));
                            p += 8;

                            var color0 = DecodeRgb565Pixel(c0);
                            var color1 = DecodeRgb565Pixel(c1);

                            PaletteEntry[] colors;
                            if (c0 > c1)
                            {
                                colors =
                                [
                                    color0,
                                    color1,
                                    ((byte)(((2 * color0.R) + color1.R) / 3), (byte)(((2 * color0.G) + color1.G) / 3), (byte)(((2 * color0.B) + color1.B) / 3), 255),
                                    ((byte)((color0.R + (2 * color1.R)) / 3), (byte)((color0.G + (2 * color1.G)) / 3), (byte)((color0.B + (2 * color1.B)) / 3), 255),
                                ];
                            }
                            else
                            {
                                // 3-color + transparent mode.
                                colors =
                                [
                                    color0,
                                    color1,
                                    ((byte)((color0.R + color1.R) / 2), (byte)((color0.G + color1.G) / 2), (byte)((color0.B + color1.B) / 2), 255),
                                    (0, 0, 0, 0),
                                ];
                            }

                            for (var py = 0; py < 4; py++)
                            {
                                for (var px = 0; px < 4; px++)
                                {
                                    var index = (int)((bits >> (30 - (2 * ((py * 4) + px)))) & 3);
                                    var x = tx + sx + px;
                                    var y = ty + sy + py;
                                    if (x < width && y < height)
                                    {
                                        var c = colors[index];
                                        SetPixel(output, width, x, y, c.R, c.G, c.B, c.A);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return output;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public ulong Padding1;
            public ulong Padding2;
            public uint TextureCount;
            public ulong TplOffset;
            public uint Padding3;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TextureDef
        {
            public ulong Padding1;
            public float UnknownA;
            public float UnknownB;
            public uint TextureId;
            public uint Padding2;
            public uint UnknownC;
            public uint TextureIndex;
            public ulong Padding3;
            public ulong UnknownD;
            public uint UnknownE;
            public uint UnknownF;
            public ulong UnknownG;
            public uint UnknownH;
            public uint UnknownI;
            public ulong UnknownJ;
            public uint UnknownK;
            public uint UnknownL;
            public ulong UnknownM;
            public float UnknownN;
            public float UnknownO;
            public float UnknownP;
            public float UnknownQ;
            public float UnknownR;
            public float UnknownS;
            public float UnknownT;
            public float UnknownU;
        }

        [InlineArray(4)]
        private struct Name4
        {
            public byte Char0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TplHeader
        {
            public Name4 Signature;
            public uint ImageCount;
            public uint OffetTableOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Offsets
        {
            public uint ImageOffset;
            public uint PaletteOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct PaletteHeader
        {
            public ushort EntryCount;
            public byte Unpacked;
            public byte Padding;
            public uint Format;
            public uint DataOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ImageHeader
        {
            public ushort Height;
            public ushort Width;
            public uint Format;
            public uint DataOffset;
        }
    }
}
