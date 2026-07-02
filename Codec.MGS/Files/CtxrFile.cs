// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Files
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using Codec.Archives;
    using Codec.Imaging;
    using Codec.Services;
    using DiscUtils.Streams;
    using ImageMagick;
    using Microsoft.Extensions.DependencyInjection;
    using static Codec.Imaging.DdsConstants;

    internal class CtxrFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton(new EntryTypeMatcher(EntryType.Image, "*.ctxr"));

            services.AddSingleton<FileHandlerResolver<MagickImage>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
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

        public static MagickImage Load(string path)
        {
            using var stream = File.OpenRead(path);
            return Load(stream);
        }

        public static MagickImage Load(Stream stream)
        {
            var header = stream.ReadBigEndian<Header>();
            if (header.Signature != 0x54585452 || header.Version != 7)
            {
                throw new FormatException();
            }
            else if ((PixelFormat)header.PixelFormat != PixelFormat.A8R8G8B8)
            {
                return ConvertWithImageMagick(header, stream);
            }

            stream.Align(0x80);
            var size = (int)stream.ReadUInt32BigEndian();

            var rowBytes = header.Width * 4;
            var pixelData = new byte[header.Width * header.Height * 4];
            var offset = 0;

            for (var y = 0; y < header.Height; y++)
            {
                var read = stream.Read(pixelData, offset, Math.Min(rowBytes, size));
                size -= read;
                for (var x = 0; x < read / 4; x++)
                {
                    var ix = offset + x * 4;
                    static int ScaleAlpha(int v) => v * 2;
                    pixelData[ix + 3] = (byte)Math.Clamp(ScaleAlpha(pixelData[ix + 3]), 0, 255);
                }

                offset += rowBytes;
                if (read < rowBytes)
                {
                    break;
                }
            }

            return new MagickImage(pixelData, new PixelReadSettings(header.Width, header.Height, StorageType.Char, "BGRA"));
        }

        private static MagickImage ConvertWithImageMagick(Header header, Stream stream)
        {
            var pixelFormat = (PixelFormat)header.PixelFormat;
            switch (pixelFormat)
            {
                case PixelFormat.DXT1:
                case PixelFormat.DXT3:
                case PixelFormat.DXT5:
                    {
                        using var ddsStream = BuildDdsStream(header, pixelFormat, stream);
                        return new MagickImage(ddsStream);
                    }

                default:
                    throw new NotImplementedException();
            }
        }

        private static Stream BuildDdsStream(Header header, PixelFormat pixelFormat, Stream stream)
        {
            uint fourCC = pixelFormat switch
            {
                PixelFormat.DXT1 => 0x31545844, // 'DXT1'
                PixelFormat.DXT3 => 0x33545844, // 'DXT3'
                PixelFormat.DXT5 => 0x35545844, // 'DXT5'
                _ => throw new ArgumentException("Not a DXT format"),
            };

            var sizeOfHeader = Marshal.SizeOf<DDS_HEADER>();
            var blockSize = pixelFormat == PixelFormat.DXT1 ? 8u : 16u;
            var mipCount = Math.Max((byte)1, header.MipMapsCount);
            var dds = new DDS_HEADER
            {
                Signature = 0x20534444u,
                Size = (uint)sizeOfHeader - 4, // Signature not included in size.
                Flags = DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT | DDSD_LINEARSIZE | (mipCount > 1 ? DDSD_MIPMAPCOUNT : 0),
                Caps1 = DDSCAPS_TEXTURE | (mipCount > 1 ? DDSCAPS_MIPMAP | DDSCAPS_COMPLEX : 0),
                Width = header.Width,
                Height = header.Height,
                PitchOrLinearSize = Math.Max(1u, ((uint)header.Width + 3) / 4) * Math.Max(1u, ((uint)header.Height + 3) / 4) * blockSize,
                Depth = (uint)header.Depth > 1 ? header.Depth : 0u,
                MipMapCount = mipCount,
                PixelFormat = new DDS_PIXELFORMAT
                {
                    Size = (uint)Marshal.SizeOf<DDS_PIXELFORMAT>(),
                    Flags = DDPF_FOURCC,
                    FourCC = fourCC,
                },
            };

            var headerStream = new MemoryStream(sizeOfHeader);
            headerStream.WriteLittleEndian(dds);
            return new ConcatStream(
                Ownership.Dispose,
                MappedStream.FromStream(headerStream, Ownership.Dispose),
                new OffsetStreamSpan(stream, 0x84, stream.Length - 0x84, Ownership.Dispose));
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

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
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
