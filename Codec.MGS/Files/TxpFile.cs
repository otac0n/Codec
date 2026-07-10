// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Abstractions;
    using System.IO.Compression;
    using System.Runtime.InteropServices;
    using Codec;
    using Codec.Archives;
    using Codec.Imaging;
    using Codec.Services;
    using ImageMagick;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    internal class TxpFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton(new EntryTypeMatcher(EntryType.Image, "*.txpx"));

            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".txp", StringComparison.OrdinalIgnoreCase))
                {
                    using (var stream = parent.File.OpenRead(parentRelativePath))
                    {
                        var header = stream.ReadLittleEndian<Header>();
                        if (header.Flags > 0xFFF || header.TextureCount > 0x400)
                        {
                            serviceProvider.GetService<ILogger<TxpFileFileSystem>>()?
                                .LogInformation("Unknown TXP. Flags: '{Flags:x8}', Texture Count: '{TextureCount}', Path: '{FullPath}'", header.Flags, header.TextureCount, fullPath);
                            return null;
                        }
                    }

                    return (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        return new TxpFileFileSystem(parentRelativePath, parent);
                    };
                }

                return null;
            });

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

        private class TxpFileFileSystem : IndexedFileSystem<uint>
        {
            private readonly IFileSystem parent;
            private readonly string path;

            public TxpFileFileSystem(string path, IFileSystem parent)
            {
                this.parent = parent ?? new FileSystem();
                this.path = path;
            }

            protected override IEnumerable<uint> ReadIndex()
            {
                using var stream = this.parent.File.OpenRead(this.path);
                var header = stream.ReadLittleEndian<Header>();
                stream.Position = header.InfoOffset;
                var textureDefinitions = stream.ReadArrayLittleEndian<Descriptor>((int)header.TextureCount);
                var ids = new uint[header.TextureCount];
                for (var t = 0; t < header.TextureCount; t++)
                {
                    var info = textureDefinitions[t];
                    ids[t] = info.Id;
                }

                return ids;
            }

            protected override string GetEntryName(uint entry) =>
                $"{entry:x4}.txpx";

            protected override Stream Open(uint entry, FileStreamOptions parentOptions)
            {
                // HACK: Each sub-file loads the whole file using the filename to locate the entry.
                FileBase.EnsureReadOnly(parentOptions, "Writing to sub images in .txp files is not supported.");
                return this.parent.File.Open(this.path, parentOptions);
            }
        }

        public static MagickImage? Load(Stream stream, uint textureId)
        {
            var bytes = new byte[stream.Length];
            var span = bytes.AsSpan();
            stream.ReadExactly(span);

            var header = MemoryMarshal.Cast<byte, Header>(span)[0];
            var textureDefinitions = MemoryMarshal.Cast<byte, Descriptor>(span[(int)header.InfoOffset..])[0..(int)header.TextureCount];
            for (var i = 0; i < header.TextureCount; i++)
            {
                var info = textureDefinitions[i];
                if (info.Id != textureId)
                {
                    continue;
                }

                var entry = MemoryMarshal.Cast<byte, Entry>(span[(int)info.ImageOffset..])[0];

                var width = entry.Width & 0xFFF;
                var height = entry.Height & 0xFFF;
                var size = width * height;
                var compressed = (entry.Flags & 0xF0) != 0;

                int bpp;
                switch (entry.Flags & 0x0F)
                {
                    case 4:
                        bpp = 4;
                        break;

                    case 5:
                        bpp = 8;
                        break;

                    case 6:
                        // TODO: Is it possible to predetermine the length? Could be compressed DDS.
                        return new MagickImage(span[(int)info.ColorDataOffset..], MagickFormat.Dds);

                    default:
                        throw new NotImplementedException($"Unknown flags 0x{entry.Flags:x4}");
                }

                var colorTable = MemoryMarshal.Cast<byte, Color>(span[(int)info.ColorDataOffset..]);
                Span<byte> pixelData;
                if (compressed)
                {
                    pixelData = Decompress(bytes, (int)entry.ZOffset, size);
                }
                else
                {
                    if (entry.PixelOffset == 0)
                    {
                        return null;
                    }

                    pixelData = span[(int)entry.PixelOffset..];
                }

                pixelData = Unswizzle(pixelData, width, height, bpp);

                var writer = new TgaWriter<Color, byte>(info.Width, info.Height, 256, info.XOffset, info.YOffset);
                for (var j = 0; j < 256; j++)
                {
                    writer.WriteColor(colorTable[j]);
                }

                for (var j = 0; j < pixelData.Length; j++)
                {
                    writer.WriteIndex(pixelData[j]);
                }

                return writer.ToMagickImage();
            }

            return null;
        }

        private static Span<byte> Unswizzle(Span<byte> pixelData, int width, int height, int bpp)
        {
            var size = width * height;
            var output = new byte[size];
            var ix = 0;
            if (bpp == 4)
            {
                for (var y = 0; y < height; y++)
                {
                    var yc0 = y * 16;
                    var yc1 = y / 8 * (width * 4 - 128);

                    for (var x = 0; x < width / 2; x++)
                    {
                        var xc0 = x / 16 * 16;
                        var xc1 = x / 16 * 128;
                        var pixelPos = x - xc0 + xc1 + yc0 + yc1;

                        output[ix] = (byte)(pixelData[pixelPos] & 0xF);
                        ix++;
                        pixelPos = x - xc0 + xc1 + yc0 + yc1;
                        output[ix] = (byte)(pixelData[pixelPos] >> 4);
                        ix++;
                    }

                }
            }
            else
            {
                for (var y = 0; y < height; y++)
                {
                    var yc0 = y * 16;
                    var yc1 = y / 8 * (width * 8 - 128);

                    for (var x = 0; x < width; x++)
                    {
                        var xc0 = x / 16 * 16;
                        var xc1 = x / 16 * 128;

                        output[ix] = pixelData[x - xc0 + xc1 + yc0 + yc1];
                        ix++;
                    }
                }
            }

            return output.AsSpan();
        }

        private static Span<byte> Decompress(byte[] source, int offset, int size)
        {
            var count = BitConverter.ToInt32(source, offset);
            var mem = new MemoryStream(source, offset + sizeof(int), count);
            var stream = new ZLibStream(mem, CompressionMode.Decompress);
            var output = new byte[size];
            _ = stream.Read(output, 0, size);
            return output.AsSpan();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Header
        {
            public uint Flags;
            public uint Id;
            public uint TextureCount;
            public uint NumInfo;
            public uint NumColour;
            public uint ImageOffset;
            public uint InfoOffset;
            public uint ClutOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Entry
        {
            public ushort Flags;
            public ushort Width;
            public ushort Height;
            public ushort Pad;
            public uint Pad0;
            public uint PixelOffset;
            public uint ZOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Descriptor
        {
            public uint Flags;
            public uint Id;
            public uint ImageOffset;
            public uint ColorDataOffset;
            public float UScale;
            public float VScale;
            public float UOffset;
            public float VOffset;
            public ushort Width;
            public ushort Height;
            public short XOffset;
            public short YOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Color
        {
            public byte R;
            public byte G;
            public byte B;
            public byte A;
        }
    }
}
