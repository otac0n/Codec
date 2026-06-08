// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec.Files
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Runtime.InteropServices;
    using Microsoft.Extensions.DependencyInjection;

    internal class CtxrFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileHandlerResolver<Bitmap>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".ctxr", StringComparison.OrdinalIgnoreCase))
                {
                    return (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var input = parent.File.OpenRead(parentRelativePath);
                        return Load(input);
                    };
                }

                return null;
            });
        }

        public static Bitmap Load(string path)
        {
            using var stream = File.OpenRead(path);
            return Load(stream);
        }

        public static Bitmap Load(Stream stream)
        {
            var header = stream.ReadBigEndian<Header>();
            if (header.Signature != 0x54585452 || header.Version != 7)
            {
                throw new FormatException();
            }
            else if ((PixelFormat)header.PixelFormat != PixelFormat.A8R8G8B8)
            {
                throw new NotImplementedException($"The pixel format {header.PixelFormat} is not currently supported.");
            }

            stream.Align(0x80);
            var size = (int)stream.ReadUInt32BigEndian();

            var bitmap = new Bitmap(header.Width, header.Height);

            BitmapData? bmpData = null;
            try
            {
                bmpData = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var buffer = new byte[bmpData.Width * 4];
                var scan = bmpData.Scan0;
                for (var y = 0; y < bmpData.Height; y++, scan += bmpData.Stride)
                {
                    var read = stream.Read(buffer, 0, Math.Min(buffer.Length, size));
                    size -= read;
                    for (var x = 0; x < read / 4; x++)
                    {
                        var ix = x * 4;
                        buffer[ix + 3] = (byte)Math.Clamp(buffer[ix + 3] * 2, 0, 255);
                    }

                    Marshal.Copy(buffer, 0, scan, read);
                    if (read < buffer.Length)
                    {
                        break;
                    }
                }
            }
            finally
            {
                if (bmpData is not null)
                {
                    bitmap.UnlockBits(bmpData);
                }
            }

            return bitmap;
        }

        private enum PixelFormat : ushort
        {
            A8R8G8B8 = 0x0,
            A16B16G16R16F = 0x2,
            R32F = 0x3,
            D24X8 = 0x4,
            DXT1 = 0x5,
            DXT3 = 0x6,
            DXT5 = 0x7,
            A32B32G32R32F = 0x8,
            Luminance8 = 0x9,
            D24FS8 = 0xA,
            Count = 0xB,
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private struct Header
        {
            public uint Signature;
            public uint Version;
            public ushort Width;
            public ushort Height;
            public ushort Depth;
            public ushort UnknownA;
            public ushort PixelFormat;
            public ushort UnknownC;
            public byte UnknownD;
            public byte UnknownE;
            public byte UnknownF;
            public byte UnknownG;
            public byte UnknownH;
            public byte UnknownI;
            public byte UnknownJ;
            public byte UnknownK;
            public byte UnknownL;
            public byte UnknownM;
            public byte UnknownN;
            public byte UnknownO;
            public byte UnknownP;
            public byte UnknownQ;
            public byte UnknownR;
            public byte UnknownS;
            public byte UnknownT;
            public byte UnknownU;
            public byte MipMapsCount;
            public byte UnknownV;
        }
    }
}
