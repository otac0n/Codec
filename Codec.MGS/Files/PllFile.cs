namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using Codec.Imaging;
    using Codec.Services;
    using ImageMagick;
    using Microsoft.Extensions.DependencyInjection;

    internal class PllFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton(new EntryTypeMatcher(EntryType.Image, "*.pll"));

            services.AddSingleton<FileHandlerResolver<MagickImage>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".pll", StringComparison.OrdinalIgnoreCase))
                {
                    return new((fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var file = parent.File.OpenRead(parentRelativePath);
                        return Load(file);
                    });
                }

                return null;
            });
        }

        public static MagickImage Load(Stream stream)
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

            var tgaWriter = new TgaWriter<int, byte>(header.width, header.height, (ushort)palette.Length);

            for (var i = 0; i < palette.Length; i++)
            {
                var v = palette[i];
                static int Expand5(int x) => (x << 3) | (x >> 2); // x * 255 / 31
                tgaWriter.WriteColor(
                    ((v & 0x8000) != 0 ? 0xFF : 0x00) << 24 |
                    Expand5((v >> 0) & 0x1F) << 16 |
                    Expand5((v >> 5) & 0x1F) << 8 |
                    Expand5((v >> 10) & 0x1F) << 0);
            }

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
                        tgaWriter.WriteIndex(index);
                    }
                }
            }

            return tgaWriter.ToMagickImage();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
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
