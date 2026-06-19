namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Runtime.InteropServices;
    using Microsoft.Extensions.DependencyInjection;

    internal class PllFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileHandlerResolver<Bitmap>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".pll", StringComparison.OrdinalIgnoreCase))
                {
                    return (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var file = parent.File.OpenRead(parentRelativePath);
                        return Load(file);
                    };
                }

                return null;
            });
        }

        public static Bitmap Load(Stream stream)
        {
            var header = stream.ReadLittleEndian<Header>();
            var divisor = header.flag0 >> 12;
            var condit = (uint)((1 << divisor) - 1);
            var times = (header.flag0 >> 8) switch
            {
                0x82 => 4,
                0x72 => 4,
                0x62 => 5,
                0x52 => 6,
                0x42 => 8,
                0x32 => 10,
                0x22 => 8,
                0x12 => 32,
            };

            var palette = stream.ReadArrayLittleEndian<ushort>(header.nColors);

            stream.Align(4);

            var jump = 0;
            var bitmap = new List<byte>();
            var readed = 0;

            while (stream.ReadByte() is >= 0 and var read)
            {
                var low = (byte)(read & 0x0F);
                var high = (byte)((read & 0xF0) >> 4);

                var (b, a) = Math.DivRem(low, (byte)4);
                var (d, c) = Math.DivRem(high, (byte)4);

                readed += a + b + c + d + 4;
                bitmap.AddRange([(byte)(a + 1), (byte)(b + 1), (byte)(c + 1), (byte)(d + 1)]);
                jump += 1;
                if (readed >= header.width * header.height)
                {
                    break;
                }
            }

            stream.Align(4);

            var data = new List<byte>();
            for (var loop = 0; loop <= bitmap.Count / times; loop++)
            {
                if (stream.Position + 4 >= stream.Length)
                {
                    break;
                }

                var colors = stream.ReadUInt32LittleEndian();
                for (var set = 0; set < times; set++)
                {
                    var index = (byte)((colors >> (divisor * set)) & 0xFF & condit);

                    if (bitmap.Count < loop * times + set + 1)
                    {
                        break;
                    }

                    for (var repeat = 0; repeat < bitmap[loop * times + set]; repeat++)
                    {
                        data.Add(index);
                    }
                }
            }

            var readIndex = 0;
            var bmp = new Bitmap(header.width, header.height, PixelFormat.Format8bppIndexed);
            var bmpData = bmp.LockBits(new Rectangle(0, 0, header.width, header.height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);

            var p = bmp.Palette;
            for (var i = 0; i < palette.Length; i++)
            {
                var v = palette[i];

                static int Expand5(int x) => (x << 3) | (x >> 2);
                p.Entries[i] = Color.FromArgb(
                    (v & 0x8000) != 0 ? 255 : 0,
                    Expand5((v >> 0) & 0x1F),
                    Expand5((v >> 5) & 0x1F),
                    Expand5((v >> 10) & 0x1F));
            }

            bmp.Palette = p;

            var indexData = new byte[header.width];
            var scan = bmpData.Scan0;
            for (var y = 0; y < header.height; y++, scan += bmpData.Stride)
            {
                for (var x = 0; x < header.width; x++)
                {
                    indexData[x] = (byte)(readIndex < data.Count ? data[readIndex++] : 0);
                }

                Marshal.Copy(indexData, 0, scan, header.width);
            }

            bmp.UnlockBits(bmpData);
            return bmp;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct Header
        {
            public ushort flag0;
            public ushort nColors;
            public ushort width;
            public ushort height;
            public ushort unknownA;
            public ushort unknownB;
            public ushort unknownC;
            public ushort unknownD;
            public ushort unknownE;
            public ushort unknownF;
        }
    }
}
