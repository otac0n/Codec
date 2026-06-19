// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.IO.Abstractions;
    using System.Runtime.InteropServices;
    using Codec;
    using Codec.Archives;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    internal class TriFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".tri", StringComparison.OrdinalIgnoreCase))
                {
                    return (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        return new TriFileFileSystem(parentRelativePath, parent);
                    };
                }

                return null;
            });

            services.AddSingleton<FileHandlerResolver<Bitmap>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (parent is TriFileFileSystem)
                {
                    return (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var file = parent.File.OpenRead(parentRelativePath);
                        var id = Convert.ToUInt32(parent.Path.GetFileNameWithoutExtension(parentRelativePath), 16);
                        return Load(file, id);
                    };
                }

                return null;
            });
        }

        private class TriFileFileSystem : IndexedFileSystem<uint>
        {
            private readonly IFileSystem parent;
            private readonly string path;

            public TriFileFileSystem(string path, IFileSystem parent)
            {
                this.parent = parent ?? new FileSystem();
                this.path = path;
            }

            protected override IEnumerable<uint> ReadIndex()
            {
                using var stream = this.parent.File.OpenRead(this.path);
                var bytes = new byte[stream.Length];
                var span = bytes.AsSpan();
                stream.ReadExactly(span);

                var header = MemoryMarshal.Cast<byte, Header>(span)[0];
                var textureDefinitions = MemoryMarshal.Cast<byte, TextureInfo>(span[32..])[0..header.TextureCount];
                var ids = new uint[header.TextureCount];
                for (var t = 0; t < header.TextureCount; t++)
                {
                    var info = textureDefinitions[t];
                    ids[t] = info.Code;
                }

                return ids;
            }

            protected override string GetEntryName(uint entry) =>
                entry.ToString("x4") + ".tm2";

            protected override Stream Open(uint entry, FileStreamOptions parentOptions)
            {
                // HACK: Each sub-file loads the whole file using the filename to locate the entry.
                FileBase.EnsureReadOnly(parentOptions, "Writing to sub images in .tri files is not supported.");
                return this.parent.File.Open(this.path, parentOptions);
            }
        }

        private static int[] block32 =
        [
            00, 01, 04, 05, 16, 17, 20, 21,
            02, 03, 06, 07, 18, 19, 22, 23,
            08, 09, 12, 13, 24, 25, 28, 29,
            10, 11, 14, 15, 26, 27, 30, 31,
        ];

        private static int[] columnWord32 =
        [
            00, 01, 04, 05, 08, 09, 12, 13,
            02, 03, 06, 07, 10, 11, 14, 15,
        ];

        private static int[] block8 =
        [
            00, 01, 04, 05, 16, 17, 20, 21,
            02, 03, 06, 07, 18, 19, 22, 23,
            08, 09, 12, 13, 24, 25, 28, 29,
            10, 11, 14, 15, 26, 27, 30, 31,
        ];

        private static int[] block4 =
        [
            00, 02, 08, 10,
            01, 03, 09, 11,
            04, 06, 12, 14,
            05, 07, 13, 15,
            16, 18, 24, 26,
            17, 19, 25, 27,
            20, 22, 28, 30,
            21, 23, 29, 31,
        ];

        private static int[][] columnWord8 =
        [
            [
                00, 01, 04, 05, 08, 09, 12, 13, 00, 01, 04, 05, 08, 09, 12, 13,
                02, 03, 06, 07, 10, 11, 14, 15, 02, 03, 06, 07, 10, 11, 14, 15,

                08, 09, 12, 13, 00, 01, 04, 05,  08, 09, 12, 13, 00, 01, 04, 05,
                10, 11, 14, 15, 02, 03, 06, 07,  10, 11, 14, 15, 02, 03, 06, 07,
            ],
            [
                08, 09, 12, 13, 00, 01, 04, 05,  08, 09, 12, 13, 00, 01, 04, 05,
                10, 11, 14, 15, 02, 03, 06, 07,  10, 11, 14, 15, 02, 03, 06, 07,

                00, 01, 04, 05, 08, 09, 12, 13, 00, 01, 04, 05, 08, 09, 12, 13,
                02, 03, 06, 07, 10, 11, 14, 15, 02, 03, 06, 07, 10, 11, 14, 15,
            ],
        ];

        private static int[] columnByte8 =
        [
            0, 0, 0, 0, 0, 0, 0, 0,  2, 2, 2, 2, 2, 2, 2, 2,
            0, 0, 0, 0, 0, 0, 0, 0,  2, 2, 2, 2, 2, 2, 2, 2,

            1, 1, 1, 1, 1, 1, 1, 1,  3, 3, 3, 3, 3, 3, 3, 3,
            1, 1, 1, 1, 1, 1, 1, 1,  3, 3, 3, 3, 3, 3, 3, 3,
        ];

        private static int[][] columnWord4 =
        [
            [
                 0,  1,  4,  5,  8,  9, 12, 13,   0,  1,  4,  5,  8,  9, 12, 13,   0,  1,  4,  5,  8,  9, 12, 13,   0,  1,  4,  5,  8,  9, 12, 13,
                 2,  3,  6,  7, 10, 11, 14, 15,   2,  3,  6,  7, 10, 11, 14, 15,   2,  3,  6,  7, 10, 11, 14, 15,   2,  3,  6,  7, 10, 11, 14, 15,

                 8,  9, 12, 13,  0,  1,  4,  5,   8,  9, 12, 13,  0,  1,  4,  5,   8,  9, 12, 13,  0,  1,  4,  5,   8,  9, 12, 13,  0,  1,  4,  5,
                10, 11, 14, 15,  2,  3,  6,  7,  10, 11, 14, 15,  2,  3,  6,  7,  10, 11, 14, 15,  2,  3,  6,  7,  10, 11, 14, 15,  2,  3,  6,  7
            ],
            [
                 8,  9, 12, 13,  0,  1,  4,  5,   8,  9, 12, 13,  0,  1,  4,  5,   8,  9, 12, 13,  0,  1,  4,  5,   8,  9, 12, 13,  0,  1,  4,  5,
                10, 11, 14, 15,  2,  3,  6,  7,  10, 11, 14, 15,  2,  3,  6,  7,  10, 11, 14, 15,  2,  3,  6,  7,  10, 11, 14, 15,  2,  3,  6,  7,

                 0,  1,  4,  5,  8,  9, 12, 13,   0,  1,  4,  5,  8,  9, 12, 13,   0,  1,  4,  5,  8,  9, 12, 13,   0,  1,  4,  5,  8,  9, 12, 13,
                 2,  3,  6,  7, 10, 11, 14, 15,   2,  3,  6,  7, 10, 11, 14, 15,   2,  3,  6,  7, 10, 11, 14, 15,   2,  3,  6,  7, 10, 11, 14, 15
            ]
        ];

        private static int[] columnByte4 =
        [
            0, 0, 0, 0, 0, 0, 0, 0,  2, 2, 2, 2, 2, 2, 2, 2,  4, 4, 4, 4, 4, 4, 4, 4,  6, 6, 6, 6, 6, 6, 6, 6,
            0, 0, 0, 0, 0, 0, 0, 0,  2, 2, 2, 2, 2, 2, 2, 2,  4, 4, 4, 4, 4, 4, 4, 4,  6, 6, 6, 6, 6, 6, 6, 6,

            1, 1, 1, 1, 1, 1, 1, 1,  3, 3, 3, 3, 3, 3, 3, 3,  5, 5, 5, 5, 5, 5, 5, 5,  7, 7, 7, 7, 7, 7, 7, 7,
            1, 1, 1, 1, 1, 1, 1, 1,  3, 3, 3, 3, 3, 3, 3, 3,  5, 5, 5, 5, 5, 5, 5, 5,  7, 7, 7, 7, 7, 7, 7, 7
        ];

        private static void writeTexPSMCT32(int dbp, int dbw, int dsax, int dsay, int rrw, int rrh, Span<byte> data, uint[] gsmem)
        {
            var src = MemoryMarshal.Cast<byte, uint>(data);
            var startBlockPos = dbp * 64;

            var i = 0;
            for (var y = dsay; y < dsay + rrh; y++)
            {
                for (var x = dsax; x < dsax + rrw; x++)
                {
                    var pageX = x / 64;
                    var pageY = y / 32;
                    var page = pageX + pageY * dbw;

                    var px = x - (pageX * 64);
                    var py = y - (pageY * 32);

                    var blockX = px / 8;
                    var blockY = py / 8;
                    var block = block32[blockX + blockY * 8];

                    var bx = px - blockX * 8;
                    var by = py - blockY * 8;

                    var column = by / 2;

                    var cx = bx;
                    var cy = by - column * 2;
                    var cw = columnWord32[cx + cy * 8];

                    gsmem[startBlockPos + page * 2048 + block * 64 + column * 16 + cw] = src[i];
                    i++;
                }
            }
        }

        private static void readTexPSMCT32(int dbp, int dbw, int dsax, int dsay, int rrw, int rrh, Span<byte> data, uint[] gsmem)
        {
            var src = MemoryMarshal.Cast<byte, uint>(data);
            var startBlockPos = dbp * 64;

            var i = 0;
            for (var y = dsay; y < dsay + rrh; y++)
            {
                for (var x = dsax; x < dsax + rrw; x++)
                {
                    var pageX = x / 64;
                    var pageY = y / 32;
                    var page = pageX + pageY * dbw;

                    var px = x - (pageX * 64);
                    var py = y - (pageY * 32);

                    var blockX = px / 8;
                    var blockY = py / 8;
                    var block = block32[blockX + blockY * 8];

                    var bx = px - blockX * 8;
                    var by = py - blockY * 8;

                    var column = by / 2;

                    var cx = bx;
                    var cy = by - column * 2;
                    var cw = columnWord32[cx + cy * 8];

                    src[i] = gsmem[startBlockPos + page * 2048 + block * 64 + column * 16 + cw];
                    i++;
                }
            }
        }

        private static void readTexPSMT8(int dbp, int dbw, int dsax, int dsay, int rrw, int rrh, Span<byte> data, uint[] gsmem)
        {
            dbw >>= 1;
            var src = data;
            var startBlockPos = dbp * 64;

            var i = 0;
            for (var y = dsay; y < dsay + rrh; y++)
            {
                for (var x = dsax; x < dsax + rrw; x++)
                {
                    var pageX = x / 128;
                    var pageY = y / 64;
                    var page = pageX + pageY * dbw;

                    var px = x - (pageX * 128);
                    var py = y - (pageY * 64);

                    var blockX = px / 16;
                    var blockY = py / 16;
                    var block = block8[blockX + blockY * 8];

                    var bx = px - blockX * 16;
                    var by = py - blockY * 16;

                    var column = by / 4;

                    var cx = bx;
                    var cy = by - column * 4;
                    var cw = columnWord8[column & 1][cx + cy * 16];
                    var cb = columnByte8[cx + cy * 16];

                    var dst = MemoryMarshal.Cast<uint, byte>(gsmem.AsSpan()[(startBlockPos + page * 2048 + block * 64 + column * 16 + cw)..]);
                    src[i] = dst[cb];
                    i++;
                }
            }
        }

        private static void readTexPSMT4(int dbp, int dbw, int dsax, int dsay, int rrw, int rrh, Span<byte> data, uint[] gsmem)
        {
            dbw >>= 1;
            var src = data;
            var startBlockPos = dbp * 64;

            var odd = false;

            var i = 0;
            for (var y = dsay; y < dsay + rrh; y++)
            {
                for (var x = dsax; x < dsax + rrw; x++)
                {
                    var pageX = x / 128;
                    var pageY = y / 128;
                    var page = pageX + pageY * dbw;

                    var px = x - (pageX * 128);
                    var py = y - (pageY * 128);

                    var blockX = px / 32;
                    var blockY = py / 16;
                    var block = block4[blockX + blockY * 4];

                    var bx = px - blockX * 32;
                    var by = py - blockY * 16;

                    var column = by / 4;

                    var cx = bx;
                    var cy = by - column * 4;
                    var cw = columnWord4[column & 1][cx + cy * 32];
                    var cb = columnByte4[cx + cy * 32];

                    var dst = MemoryMarshal.Cast<uint, byte>(gsmem.AsSpan()[(startBlockPos + page * 2048 + block * 64 + column * 16 + cw)..]);

                    if ((cb & 1) != 0)
                    {
                        if (!odd)
                        {
                            src[i] = (byte)((src[i] & 0x0f) | (dst[cb >> 1] & 0xf0));
                        }
                        else
                        {
                            src[i] = (byte)((src[i] & 0xf0) | ((dst[cb >> 1] >> 4) & 0x0f));
                        }
                    }
                    else
                    {
                        if (!odd)
                        {
                            src[i] = (byte)((src[i] & 0x0f) | ((dst[cb >> 1] << 4) & 0xf0));
                        }
                        else
                        {
                            src[i] = (byte)((src[i] & 0xf0) | (dst[cb >> 1] & 0x0f));
                        }
                    }

                    if (odd)
                    {
                        i++;
                    }

                    odd = !odd;
                }
            }
        }

        private static void unswizzleClut(Span<byte> clutBuffer)
        {
            var temp = new byte[32];
            for (var i = 1; i <= 29; i += 4)
            {
                clutBuffer[(i * 32)..((i + 1) * 32)].CopyTo(temp);
                clutBuffer[((i + 1) * 32)..((i + 2) * 32)].CopyTo(clutBuffer[(i * 32)..]);
                temp.CopyTo(clutBuffer[((i + 1) * 32)..]);
            }
        }

        public static Bitmap Load(Stream stream, uint textureId, ILogger<TriFile>? logger = null)
        {
            logger ??= NullLogger<TriFile>.Instance;

            var bytes = new byte[stream.Length];
            var span = bytes.AsSpan();
            stream.ReadExactly(span);

            var header = MemoryMarshal.Cast<byte, Header>(span)[0];
            var textureDefinitions = MemoryMarshal.Cast<byte, TextureInfo>(span[32..])[0..header.TextureCount];

            var gsmemTexture = new uint[1024 * 1024];
            var gsmemPalette = new uint[1024 * 1024];
            writeTexPSMCT32(0, 1, 0, 0, 64, header.Height, span[header.ImageOffset..], gsmemTexture);
            writeTexPSMCT32(0, 1, 0, 0, 64, header.ClutHeight, span[header.ClutOffset..], gsmemPalette);

            for (var t = 0; t < header.TextureCount; t++)
            {
                var info = textureDefinitions[t];
                if (info.Code != textureId)
                {
                    continue;
                }

                var targetFormat = info.RegisterInfo2.PSM switch
                {
                    PixelStorageMode.PSMT4 => PixelFormat.Format4bppIndexed,
                    PixelStorageMode.PSMT8 => PixelFormat.Format8bppIndexed,
                    _ => throw new NotImplementedException(),
                };

                var imageWidth = 1 << (int)info.RegisterInfo2.TW;
                var imageHeight = 1 << (int)info.RegisterInfo2.TH;

                var u = (int)(info.UOffset * imageWidth);
                var v = (int)(info.VOffset * imageHeight);
                var rawW = info.UScale * imageWidth;
                var rawH = info.VScale * imageHeight;

                var texelU = info.UOffset * imageWidth;
                var texelV = info.VOffset * imageHeight;
                var centerAddressed = (texelU - MathF.Floor(texelU) > 0.25f) || (texelV - MathF.Floor(texelV) > 0.25f);

                var width = (int)rawW + (centerAddressed ? 1 : 0);
                var height = (int)rawH + (centerAddressed ? 1 : 0);

                var bmp = new Bitmap(width, height, targetFormat);

                if ((targetFormat & PixelFormat.Indexed) != 0)
                {
                    if (info.RegisterInfo2.CPSM != PixelStorageMode.PSMCT32)
                    {
                        throw new NotImplementedException();
                    }

                    var (w, h) = info.RegisterInfo2.PSM switch
                    {
                        PixelStorageMode.PSMT8 => (16, 16),
                        PixelStorageMode.PSMT4 => (8, 2),
                        _ => throw new NotImplementedException(),
                    };

                    var clutBuffer = new byte[1024 * 1024 * 4];
                    readTexPSMCT32(info.RegisterInfo2.CBP, 1, (int)(info.RegisterInfo2.CSAX * 8), (int)(info.RegisterInfo2.CSAY * 2), w, h, clutBuffer.AsSpan(), gsmemPalette);
                    var paletteData = MemoryMarshal.Cast<byte, int>(clutBuffer);
                    if (info.RegisterInfo2.PSM == PixelStorageMode.PSMT8 && !info.RegisterInfo2.CSM)
                    {
                        unswizzleClut(clutBuffer);
                    }

                    var p = bmp.Palette;
                    for (var i = 0; i < p.Entries.Length; i++)
                    {
                        var rgba = (uint)paletteData[i];
                        var r = (int)(0xFF & rgba);
                        var g = (int)((0xFF00 & rgba) >> 8);
                        var b = (int)((0xFF0000 & rgba) >> 16);
                        var a = (int)((0xFF000000 & rgba) >> 24);
                        a = (byte)(int)Math.Round(a / 128f * 255);
                        p.Entries[i] = Color.FromArgb(a, r, g, b);
                    }

                    bmp.Palette = p;
                }

                BitmapData? bmpData = null;
                try
                {
                    int chunkSize;
                    var texBuffer = new byte[1024 * 1024 * 4];
                    if (info.RegisterInfo2.PSM == PixelStorageMode.PSMT8)
                    {
                        readTexPSMT8(info.RegisterInfo2.TBP0, info.RegisterInfo2.TBW, u, v, width, height, texBuffer.AsSpan(), gsmemTexture);
                        chunkSize = width;
                    }
                    else
                    {
                        readTexPSMT4(info.RegisterInfo2.TBP0, info.RegisterInfo2.TBW, u, v, width, height, texBuffer.AsSpan(), gsmemTexture);
                        chunkSize = width / 2;
                    }

                    bmpData = bmp.LockBits(new Rectangle(Point.Empty, bmp.Size), ImageLockMode.WriteOnly, targetFormat);

                    Marshal.Copy(texBuffer, 0, bmpData.Scan0, bmpData.Stride * bmpData.Height);

                    var scan = bmpData.Scan0;
                    var buffer = new byte[bmpData.Stride];
                    for (var y = 0; y < bmpData.Height; y++, scan += bmpData.Stride)
                    {
                        Marshal.Copy(texBuffer, y * chunkSize, scan, chunkSize);
                    }
                }
                finally
                {
                    if (bmpData is not null)
                    {
                        bmp.UnlockBits(bmpData);
                    }
                }

                return bmp;
            }

            return null;
        }

        private record class BlockFormat(Size PageSize)
        {
            public static readonly BlockFormat PSMT8 = new(
                PageSize: new(128, 64));

            public static readonly BlockFormat PSMT4 = new(
                PageSize: new(128, 128));
        }

        private enum TextureFunction : uint
        {
            Modulate = 0,
            Decal = 1,
            Hilight = 2,
            Hilight2 = 3,
        }

        private enum PixelStorageMode : uint
        {
            /// <summary>
            /// R8G8B8A8.
            /// </summary>
            PSMCT32 = 0,

            /// <summary>
            /// R8G8B8, Unused 8-bits.
            /// </summary>
            PSMCT24 = 1,

            /// <summary>
            /// R5G5B5A1.
            /// </summary>
            PSMCT16 = 2,

            /// <summary>
            /// R5G5B5A1 (signed).
            /// </summary>
            PSMCT16S = 10,

            /// <summary>
            /// 8BPP Indexed.
            /// </summary>
            PSMT8 = 19,

            /// <summary>
            /// 4BPP Indexed.
            /// </summary>
            PSMT4 = 20,

            /// <summary>
            /// 8BPP Indexed, Unused 24-bits.
            /// </summary>
            PSMT8H = 27,

            /// <summary>
            /// 4BPP Indexed, Unused 24-bits.
            /// </summary>
            PSMT4HL = 26,

            /// <summary>
            /// 4BPP Indexed, Unused bits 0-3 and 8-31.
            /// </summary>
            PSMT4HH = 44,

            /// <summary>
            /// 32-bit Z-buffer (Grayscale).
            /// </summary>
            PZM32 = 48,

            /// <summary>
            /// 24-bit Z-buffer (Grayscale, unused 8-bits).
            /// </summary>
            PZM24 = 49,

            /// <summary>
            /// 16-bit Z-buffer (Grayscale).
            /// </summary>
            PZM16 = 50,

            /// <summary>
            /// 16-bit Z-buffer (Grayscale, signed).
            /// </summary>
            PZM16S = 58,
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private struct Header
        {
            public uint Pad0;
            public int Width;
            public int Height;
            public int ClutHeight;
            public int TextureCount;
            public uint Pad1;
            public int ImageOffset;
            public int ClutOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private struct TextureInfo
        {
            public float UOffset;
            public float VOffset;
            public float UScale;
            public float VScale;
            public uint Code;
            public uint Pad0;
            public uint Pad1;
            public uint Pad2;
            public uint Pad3;
            public uint Pad4;
            public uint Pad5;
            public uint Pad6;
            public uint Pad7;
            public uint Pad8;
            public uint Pad9;
            public uint Pad10;
            public uint UnknownA;
            public uint UnknownB;
            public uint UnknownC;
            public uint Pad11;
            public uint UnknownD;
            public uint UnknownE;
            public uint UnknownF;
            public uint Pad12;
            public GsTex0 RegisterInfo;
            public uint UnknownG;
            public uint UnknownH;
            public GsTex0 RegisterInfo2;
            public uint UnknownI;
            public uint UnknownJ;
            public uint UnknownK;
            public uint UnknownL;
            public uint UnknownM;
            public uint UnknownN;
            public uint UnknownO;
            public uint UnknownP;
            public uint UnknownQ;
            public uint UnknownR;
            public float U1;
            public float V1;
            public float U2;
            public float V2;
            public float U3;
            public float V3;
            public uint Pad13;
            public uint Pad14;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private struct GsTex0
        {
            public ulong Bitfield;

            private const int TBP0Shift = 0;
            private const ulong TBP0Mask = 0x3FFFUL; // bits  0–13

            private const int TBWShift = 14;
            private const ulong TBWMask = 0x3FUL;    // bits 14–19

            private const int PSMShift = 20;
            private const ulong PSMMask = 0x3FUL;    // bits 20–25

            private const int TWShift = 26;
            private const ulong TWMask = 0xFUL;      // bits 26–29

            private const int THShift = 30;
            private const ulong THMask = 0xFUL;      // bits 30–33

            private const int TCCShift = 34;
            private const ulong TCCMask = 0x1UL;     // bit  34

            private const int TFXShift = 35;
            private const ulong TFXMask = 0x3UL;     // bits 35–36

            private const int CBPShift = 37;
            private const ulong CBPMask = 0x3FFFUL;  // bits 37–50

            private const int CPSMShift = 51;
            private const ulong CPSMMask = 0xFUL;    // bits 51–54

            private const int CSMShift = 55;
            private const ulong CSMMask = 0x1UL;     // bit  55

            private const int CSAXShift = 56;
            private const ulong CSAXMask = 0x1UL;    // bit 56

            private const int CSAYShift = 57;
            private const ulong CSAYMask = 0xFUL;    // bits 57–60

            private const int CLDShift = 61;
            private const ulong CLDMask = 0x7UL;     // bits 61–63

            /// <summary>
            /// Texture buffer location (Address/256).
            /// </summary>
            public int TBP0
            {
                readonly get => (int)((this.Bitfield >> TBP0Shift) & TBP0Mask);
                set => this.Bitfield = (this.Bitfield & ~(TBP0Mask << TBP0Shift)) | (((uint)value & TBP0Mask) << TBP0Shift);
            }

            /// <summary>
            /// Texture buffer width (Texels/64).
            /// </summary>
            public int TBW
            {
                readonly get => (int)((this.Bitfield >> TBWShift) & TBWMask);
                set => this.Bitfield = (this.Bitfield & ~(TBWMask << TBWShift)) | (((uint)value & TBWMask) << TBWShift);
            }

            /// <summary>
            /// Texture pixel storage format.
            /// </summary>
            public PixelStorageMode PSM
            {
                readonly get => (PixelStorageMode)((this.Bitfield >> PSMShift) & PSMMask);
                set => this.Bitfield = (this.Bitfield & ~(PSMMask << PSMShift)) | (((uint)value & PSMMask) << PSMShift);
            }

            /// <summary>
            /// Texture Width (log2).
            /// </summary>
            public uint TW
            {
                readonly get => (uint)((this.Bitfield >> TWShift) & TWMask);
                set => this.Bitfield = (this.Bitfield & ~(TWMask << TWShift)) | ((value & TWMask) << TWShift);
            }

            /// <summary>
            /// Texture Height (log2).
            /// </summary>
            public uint TH
            {
                readonly get => (uint)((this.Bitfield >> THShift) & THMask);
                set => this.Bitfield = (this.Bitfield & ~(THMask << THShift)) | ((value & THMask) << THShift);
            }

            /// <summary>
            /// Texture color component.
            /// </summary>
            /// <remarks>
            /// <c>true</c> indicates the presence of an alpha channel.
            /// </remarks>
            public bool TCC
            {
                readonly get => ((this.Bitfield >> TCCShift) & TCCMask) != 0;
                set => this.Bitfield = (this.Bitfield & ~(TCCMask << TCCShift)) | ((value ? 1UL : 0UL) << TCCShift);
            }

            /// <summary>
            /// Texture function.
            /// </summary>
            public TextureFunction TFX
            {
                readonly get => (TextureFunction)((this.Bitfield >> TFXShift) & TFXMask);
                set => this.Bitfield = (this.Bitfield & ~(TFXMask << TFXShift)) | (((uint)value & TFXMask) << TFXShift);
            }

            /// <summary>
            /// CLUT buffer location (Address/256).
            /// </summary>
            public int CBP
            {
                readonly get => (int)((this.Bitfield >> CBPShift) & CBPMask);
                set => this.Bitfield = (this.Bitfield & ~(CBPMask << CBPShift)) | (((uint)value & CBPMask) << CBPShift);
            }

            /// <summary>
            /// CLUT pixel storage format.
            /// </summary>
            public PixelStorageMode CPSM
            {
                readonly get => (PixelStorageMode)((this.Bitfield >> CPSMShift) & CPSMMask);
                set => this.Bitfield = (this.Bitfield & ~(CPSMMask << CPSMShift)) | (((uint)value & CPSMMask) << CPSMShift);
            }

            /// <summary>
            /// CLUT storage mode.
            /// </summary>
            /// <remarks>
            /// <c>false</c> indicates swizzling is performed every 32 bytes.
            /// </remarks>
            public bool CSM
            {
                readonly get => ((this.Bitfield >> CSMShift) & CSMMask) != 0;
                set => this.Bitfield = (this.Bitfield & ~(CSMMask << CSMShift)) | ((value ? 1UL : 0UL) << CSMShift);
            }

            /// <summary>
            /// CLUT entry offset.
            /// </summary>
            public uint CSAX
            {
                readonly get => (uint)((this.Bitfield >> CSAXShift) & CSAXMask);
                set => this.Bitfield = (this.Bitfield & ~(CSAXMask << CSAXShift)) | ((value & CSAXMask) << CSAXShift);
            }

            /// <summary>
            /// CLUT entry offset.
            /// </summary>
            public uint CSAY
            {
                readonly get => (uint)((this.Bitfield >> CSAYShift) & CSAYMask);
                set => this.Bitfield = (this.Bitfield & ~(CSAYMask << CSAYShift)) | ((value & CSAYMask) << CSAYShift);
            }

            /// <summary>
            /// CLUT load control.
            /// </summary>
            public uint CLD
            {
                readonly get => (uint)((this.Bitfield >> CLDShift) & CLDMask);
                set => this.Bitfield = (this.Bitfield & ~(CLDMask << CLDShift)) | ((value & CLDMask) << CLDShift);
            }
        }
    }
}
