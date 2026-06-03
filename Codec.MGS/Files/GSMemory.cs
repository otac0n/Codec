// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec.Files
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    internal sealed class GsSwizzle
    {
        public static readonly int BitsPerPage = 65536;
        public static readonly int BitsPerBlock = BitsPerPage / 32;
        public static readonly int BitsPerColumn = BitsPerBlock / 4;

        private static class PageSwizzles
        {
            public static readonly int[] PSMCT32 =
            [
                 0,  1,  4,  5, 16, 17, 20, 21,
                 2,  3,  6,  7, 18, 19, 22, 23,
                 8,  9, 12, 13, 24, 25, 28, 29,
                10, 11, 14, 15, 26, 27, 30, 31,
            ];

            public static readonly int[] PSMZ32 =
            [
                24, 25, 28, 29,  8,  9, 12, 13,
                26, 27, 30, 31, 10, 11, 14, 15,
                16, 17, 20, 21,  0,  1,  4,  5,
                18, 19, 22, 23,  2,  3,  6,  7,
            ];

            public static readonly int[] PSMCT16 =
            [
                 0,  2,  8, 10,
                 1,  3,  9, 11,
                 4,  6, 12, 14,
                 5,  7, 13, 15,
                16, 18, 24, 26,
                17, 19, 25, 27,
                20, 22, 28, 30,
                21, 23, 29, 31,
            ];

            public static readonly int[] PSMCT16S =
            [
                 0,  2, 16, 18,
                 1,  3, 17, 19,
                 8, 10, 24, 26,
                 9, 11, 25, 27,
                 4,  6, 20, 22,
                 5,  7, 21, 23,
                12, 14, 28, 30,
                13, 15, 29, 31,
            ];

            public static readonly int[] PSMZ16 =
            [
                24, 26, 16, 18,
                25, 27, 17, 19,
                28, 30, 20, 22,
                29, 31, 21, 23,
                 8, 10,  0,  2,
                 9, 11,  1,  3,
                12, 14,  4,  6,
                13, 15,  5,  7,
            ];

            public static readonly int[] PSMZ16S =
            [
                24, 26,  8, 10,
                25, 27,  9, 11,
                16, 18,  0,  2,
                17, 19,  1,  3,
                28, 30, 12, 14,
                29, 31, 13, 15,
                20, 22,  4,  6,
                21, 23,  5,  7,
            ];
        }

        private static class ColumnSwizzles
        {
            public static readonly int[] PSMCT32 =
            [
                 0,  1,  4,  5,  8,  9, 12, 13,
                 2,  3,  6,  7, 10, 11, 14, 15,
            ];

            public static readonly int[] PSMCT16 =
            [
                 0,  2,  8, 10, 16, 18, 24, 26,  1,  3,  9, 11, 17, 19, 25, 27,
                 4,  6, 12, 14, 20, 22, 28, 30,  5,  7, 13, 15, 21, 23, 29, 31,
            ];

            public static readonly int[] PSMT8 =
            [
                 0,  4, 16, 20, 32, 36, 48, 52,    2,  6, 18, 22, 34, 38, 50, 54,
                 8, 12, 24, 28, 40, 44, 56, 60,   10, 14, 26, 30, 42, 46, 58, 62,
                33, 37, 49, 53,  1,  5, 17, 21,   35, 39, 51, 55,  3,  7, 19, 23,
                41, 45, 57, 61,  9, 13, 25, 29,   43, 47, 59, 63, 11, 15, 27, 31,

                32, 36, 48, 52,  0,  4, 16, 20,   34, 38, 50, 54,  2,  6, 18, 22,
                40, 44, 56, 60,  8, 12, 24, 28,   42, 46, 58, 62, 10, 14, 26, 30,
                 1,  5, 17, 21, 33, 37, 49, 53,    3,  7, 19, 23, 35, 39, 51, 55,
                 9, 13, 25, 29, 41, 45, 57, 61,   11, 15, 27, 31, 43, 47, 59, 63,
            ];

            public static readonly int[] PSMT4 =
            [
                  0,   8,  32,  40,    64,  72,  96, 104,     2,  10,  34,  42,    66,  74,  98, 106,     4,  12,  36,  44,    68,  76, 100, 108,     6,  14,  38,  46,    70,  78, 102, 110,
                 16,  24,  48,  56,    80,  88, 112, 120,    18,  26,  50,  58,    82,  90, 114, 122,    20,  28,  52,  60,    84,  92, 116, 124,    22,  30,  54,  62,    86,  94, 118, 126,
                 65,  73,  97, 105,     1,   9,  33,  41,    67,  75,  99, 107,     3,  11,  35,  43,    69,  77, 101, 109,     5,  13,  37,  45,    71,  79, 103, 111,     7,  15,  39,  47,
                 81,  89, 113, 121,    17,  25,  49,  57,    83,  91, 115, 123,    19,  27,  51,  59,    85,  93, 117, 125,    21,  29,  53,  61,    87,  95, 119, 127,    23,  31,  55,  63,

                 64,  72,  96, 104,     0,   8,  32,  40,    66,  74,  98, 106,     2,  10,  34,  42,    68,  76, 100, 108,     4,  12,  36,  44,    70,  78, 102, 110,     6,  14,  38,  46,
                 80,  88, 112, 120,    16,  24,  48,  56,    82,  90, 114, 122,    18,  26,  50,  58,    84,  92, 116, 124,    20,  28,  52,  60,    86,  94, 118, 126,    22,  30,  54,  62,
                  1,   9,  33,  41,    65,  73,  97, 105,     3,  11,  35,  43,    67,  75,  99, 107,     5,  13,  37,  45,    69,  77, 101, 109,     7,  15,  39,  47,    71,  79, 103, 111,
                 17,  25,  49,  57,    81,  89, 113, 121,    19,  27,  51,  59,    83,  91, 115, 123,    21,  29,  53,  61,    85,  93, 117, 125,    23,  31,  55,  63,    87,  95, 119, 127,
            ];
        }

        public static readonly GsSwizzle PSMCT32 = new([8, 4], [8, 2], 32, PageSwizzles.PSMCT32, ColumnSwizzles.PSMCT32);

        public static readonly GsSwizzle PSMCT24 = new([8, 4], [8, 2], 32, PageSwizzles.PSMCT32, ColumnSwizzles.PSMCT32);

        public static readonly GsSwizzle PSMCT16 = new([4, 8], [16, 2], 16, PageSwizzles.PSMCT16, ColumnSwizzles.PSMCT16);

        public static readonly GsSwizzle PSMCT16S = new([4, 8], [16, 2], 16, PageSwizzles.PSMCT16S, ColumnSwizzles.PSMCT16);

        public static readonly GsSwizzle PSMT8 = new([8, 4], [16, 4], 8, PageSwizzles.PSMCT32, ColumnSwizzles.PSMT8);

        public static readonly GsSwizzle PSMT4 = new([4, 8], [32, 4], 4, PageSwizzles.PSMCT16, ColumnSwizzles.PSMT4);

        public static readonly GsSwizzle PSMT8H = new([8, 4], [8, 2], 32, PageSwizzles.PSMCT32, ColumnSwizzles.PSMCT32);

        public static readonly GsSwizzle PSMT4HL = new([8, 4], [8, 2], 32, PageSwizzles.PSMCT32, ColumnSwizzles.PSMCT32);

        public static readonly GsSwizzle PSMT4HH = new([8, 4], [8, 2], 32, PageSwizzles.PSMCT32, ColumnSwizzles.PSMCT32);

        public static readonly GsSwizzle PSMZ32 = new([8, 4], [8, 2], 32, PageSwizzles.PSMZ32, ColumnSwizzles.PSMCT32);

        public static readonly GsSwizzle PSMZ24 = new([8, 4], [8, 2], 32, PageSwizzles.PSMZ32, ColumnSwizzles.PSMCT32);

        public static readonly GsSwizzle PSMZ16 = new([4, 8], [16, 2], 16, PageSwizzles.PSMZ16, ColumnSwizzles.PSMCT16);

        public static readonly GsSwizzle PSMZ16S = new([4, 8], [16, 2], 16, PageSwizzles.PSMZ16S, ColumnSwizzles.PSMCT16);

        public int BlocksWide { get; }

        public int BlocksTall { get; }

        public int ColumnPixelsWide { get; }

        public int ColumnPixelsTall { get; }

        public int BitsPerPixel { get; }

        public int PageBitsWide { get; }

        public int PagePixelsWide => PageBitsWide / BitsPerPixel;

        public int PagePixelsTall { get; }

        private readonly int[] swizzleLookup;

        private GsSwizzle(
            int[] blocksInPage,
            int[] pixelsInColumn,
            int bitsPerPixel,
            int[] pageSwizzle,
            int[] columnSwizzle)
        {
            this.BlocksWide = blocksInPage[0];
            this.BlocksTall = blocksInPage[1];
            this.ColumnPixelsWide = pixelsInColumn[0];
            this.ColumnPixelsTall = pixelsInColumn[1];
            this.BitsPerPixel = bitsPerPixel;

            var blockBitWidth = pixelsInColumn[0] * bitsPerPixel;
            this.PageBitsWide = blocksInPage[0] * blockBitWidth;
            this.PagePixelsTall = blocksInPage[1] * pixelsInColumn[1] * 4;

            this.swizzleLookup = new int[BitsPerPage];

            // Port of PSMEntry constructor inner loops — do not reorder.
            for (var block = 0; block < 32; block++)
            {
                var basePos = pageSwizzle[block] * BitsPerBlock;

                var blockX = (block % blocksInPage[0]) * blockBitWidth;
                var blockY = (block / blocksInPage[0]) * (pixelsInColumn[1] * 4);

                for (var i = 0; i < BitsPerBlock; i++)
                {
                    var px = i / bitsPerPixel;
                    var off = columnSwizzle[px % columnSwizzle.Length];
                    var blockOff = (i / BitsPerColumn) * BitsPerColumn;
                    var bitOff = off * bitsPerPixel + blockOff + i % bitsPerPixel;
                    var pos = basePos + bitOff;

                    var x = blockX + (i % blockBitWidth);
                    var y = blockY + (i / blockBitWidth);

                    this.swizzleLookup[y * this.PageBitsWide + x] = pos;
                }
            }
        }

        /// <summary>
        /// Returns the bit index within a GS page for pixel (x, y).
        ///
        /// To get the uint32 word index: <c>bitIndex / BitsPerPixel</c>.
        /// To get the byte offset within that word: <c>(bitIndex % 32) / 8</c>.
        /// To get the nibble offset for 4bpp: <c>(bitIndex % 32) / 4</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBitIndex(int x, int y)
        {
            // x here is the pixel column; we address in bits.
            return this.swizzleLookup[y * this.PageBitsWide + x * this.BitsPerPixel];
        }

        /// <summary>
        /// Returns the absolute bit index in a dbw-page-wide GS memory buffer
        /// for pixel (x, y), taking into account the base page pointer and buffer width.
        /// Mirrors the dbp/dbw logic in the original read/write methods.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBitIndex(int dbp, int dbw, int x, int y)
        {
            var (pageX, px) = Math.DivRem(x, this.PagePixelsWide);
            var (pageY, py) = Math.DivRem(y, this.PagePixelsTall);
            var page = pageX + pageY * dbw;
            return dbp * BitsPerBlock + page * BitsPerPage + this.swizzleLookup[py * this.PageBitsWide + px * this.BitsPerPixel];
        }

        /// <summary>
        /// Convenience: returns the uint32 word index and the bit offset within
        /// that word for pixel (x, y) within the absolute GS memory array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetWordAndBit(int dbp, int dbw, int x, int y, out int wordIndex, out int bitWithinWord)
        {
            var bitIndex = this.GetBitIndex(dbp, dbw, x, y);
            wordIndex = bitIndex / 32;
            bitWithinWord = bitIndex % 32;
        }
    }

    internal sealed class GsViewNibble
    {
        private readonly GsSwizzle swizzle;
        private readonly Memory<uint> underlying;

        public GsViewNibble(GsSwizzle swizzle, Memory<uint> backing)
        {
            this.swizzle = swizzle;
            this.underlying = backing;
        }

        public byte Read(int dbp, int dbw, int x, int y)
        {
            this.swizzle.GetWordAndBit(dbp, dbw, x, y, out var wi, out var bit);
            var bytes = MemoryMarshal.Cast<uint, byte>(this.underlying.Span.Slice(wi, 1));
            var b = bytes[bit / 8];
            return (bit & 4) != 0
                ? (byte)(b >> 4)
                : (byte)(b & 0x0F);
        }

        public void Write(int dbp, int dbw, int x, int y, byte value)
        {
            this.swizzle.GetWordAndBit(dbp, dbw, x, y, out var wi, out var bit);
            var bytes = MemoryMarshal.Cast<uint, byte>(this.underlying.Span.Slice(wi, 1));
            if ((bit & 4) != 0)
            {
                bytes[bit / 8] = (byte)((bytes[bit / 8] & 0x0F) | ((value & 0x0F) << 4));
            }
            else
            {
                bytes[bit / 8] = (byte)((bytes[bit / 8] & 0xF0) | (value & 0x0F));
            }
        }

        public void BulkWrite(int dbp, int dbw, int dsax, int dsay, int rrw, int rrh, ReadOnlySpan<byte> src)
        {
            var nibble = 0;
            for (var y = dsay; y < dsay + rrh; y++)
            {
                for (var x = dsax; x < dsax + rrw; x++, nibble++)
                {
                    var byteIndex = nibble >> 1;
                    var value = (nibble & 1) != 0
                        ? (byte)(src[byteIndex] & 0x0F)
                        : (byte)((src[byteIndex] >> 4) & 0x0F);
                    this.Write(dbp, dbw, x, y, value);
                }
            }
        }

        public void BulkRead(int dbp, int dbw, int dsax, int dsay, int rrw, int rrh, Span<byte> dst)
        {
            var nibble = 0;
            for (var y = dsay; y < dsay + rrh; y++)
            {
                for (var x = dsax; x < dsax + rrw; x++, nibble++)
                {
                    var value = this.Read(dbp, dbw, x, y);
                    var byteIndex = nibble >> 1;
                    dst[byteIndex] = (nibble & 1) != 0
                        ? (byte)((dst[byteIndex] & 0xF0) | value)
                        : (byte)((dst[byteIndex] & 0x0F) | (value << 4));
                }
            }
        }
    }

    internal sealed class GsViewByte
    {
        private readonly GsSwizzle swizzle;
        private readonly Memory<uint> underlying;

        public GsViewByte(GsSwizzle swizzle, Memory<uint> backing)
        {
            this.swizzle = swizzle;
            this.underlying = backing;
        }

        public byte Read(int dbp, int dbw, int x, int y)
        {
            this.swizzle.GetWordAndBit(dbp, dbw, x, y, out var wi, out var bit);
            var bytes = MemoryMarshal.Cast<uint, byte>(this.underlying.Span.Slice(wi, 1));
            return bytes[bit / 8];
        }

        public void Write(int dbp, int dbw, int x, int y, byte value)
        {
            this.swizzle.GetWordAndBit(dbp, dbw, x, y, out var wi, out var bit);
            var bytes = MemoryMarshal.Cast<uint, byte>(this.underlying.Span.Slice(wi, 1));
            bytes[bit / 8] = value;
        }

        public void BulkWrite(int dbp, int dbw, int dsax, int dsay, int rrw, int rrh, ReadOnlySpan<byte> src)
        {
            var i = 0;
            for (var y = dsay; y < dsay + rrh; y++)
            {
                for (var x = dsax; x < dsax + rrw; x++, i++)
                {
                    this.Write(dbp, dbw, x, y, src[i]);
                }
            }
        }

        public void BulkRead(int dbp, int dbw, int dsax, int dsay, int rrw, int rrh, Span<byte> dst)
        {
            var i = 0;
            for (var y = dsay; y < dsay + rrh; y++)
            {
                for (var x = dsax; x < dsax + rrw; x++, i++)
                {
                    dst[i] = this.Read(dbp, dbw, x, y);
                }
            }
        }
    }

    internal sealed class GsViewUInt16
    {
        private readonly GsSwizzle swizzle;
        private readonly Memory<uint> underlying;

        public GsViewUInt16(GsSwizzle swizzle, Memory<uint> backing)
        {
            this.swizzle = swizzle;
            this.underlying = backing;
        }

        public ushort Read(int dbp, int dbw, int x, int y)
        {
            this.swizzle.GetWordAndBit(dbp, dbw, x, y, out var wi, out var bit);
            var bytes = MemoryMarshal.Cast<uint, ushort>(this.underlying.Span.Slice(wi, 1));
            return bytes[bit / 16];
        }

        public void Write(int dbp, int dbw, int x, int y, ushort value)
        {
            this.swizzle.GetWordAndBit(dbp, dbw, x, y, out var wi, out var bit);
            var bytes = MemoryMarshal.Cast<uint, ushort>(this.underlying.Span.Slice(wi, 1));
            bytes[bit / 16] = value;
        }

        public void BulkWrite(int dbp, int dbw, int dsax, int dsay, int rrw, int rrh, ReadOnlySpan<ushort> src)
        {
            var i = 0;
            for (var y = dsay; y < dsay + rrh; y++)
            {
                for (var x = dsax; x < dsax + rrw; x++, i++)
                {
                    this.Write(dbp, dbw, x, y, src[i]);
                }
            }
        }

        public void BulkRead(int dbp, int dbw, int dsax, int dsay, int rrw, int rrh, Span<ushort> dst)
        {
            var i = 0;
            for (var y = dsay; y < dsay + rrh; y++)
            {
                for (var x = dsax; x < dsax + rrw; x++, i++)
                {
                    dst[i] = this.Read(dbp, dbw, x, y);
                }
            }
        }
    }

    internal sealed class GsViewUInt32
    {
        private readonly GsSwizzle swizzle;
        private readonly Memory<uint> underlying;

        public GsViewUInt32(GsSwizzle swizzle, Memory<uint> backing)
        {
            this.swizzle = swizzle;
            this.underlying = backing;
        }

        public uint Read(int dbp, int dbw, int x, int y)
        {
            this.swizzle.GetWordAndBit(dbp, dbw, x, y, out var wi, out _);
            return this.underlying.Span[wi];
        }

        public void Write(int dbp, int dbw, int x, int y, uint value)
        {
            this.swizzle.GetWordAndBit(dbp, dbw, x, y, out var wi, out _);
            this.underlying.Span[wi] = value;
        }

        public void BulkWrite(int dbp, int dbw, int dsax, int dsay, int rrw, int rrh, ReadOnlySpan<uint> src)
        {
            var i = 0;
            for (var y = dsay; y < dsay + rrh; y++)
            {
                for (var x = dsax; x < dsax + rrw; x++, i++)
                {
                    this.Write(dbp, dbw, x, y, src[i]);
                }
            }
        }

        public void BulkRead(int dbp, int dbw, int dsax, int dsay, int rrw, int rrh, Span<uint> dst)
        {
            var i = 0;
            for (var y = dsay; y < dsay + rrh; y++)
            {
                for (var x = dsax; x < dsax + rrw; x++, i++)
                {
                    dst[i] = this.Read(dbp, dbw, x, y);
                }
            }
        }
    }
}
