// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Abstractions;
    using System.Runtime.InteropServices;
    using Codec;
    using Codec.Archives;
    using Codec.Imaging;
    using DiscUtils.Streams;
    using ImageMagick;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (int ImageIndex, int PaletteIndex, long Offset, long Length);

    internal class RpkFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".rpk", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(parent.Path.GetExtension(parentRelativePath), ".res", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(parent.Path.GetExtension(parentRelativePath), ".r", StringComparison.OrdinalIgnoreCase))
                {
                    using (var stream = parent.File.OpenRead(parentRelativePath))
                    {
                        var header = stream.ReadLittleEndian<Header>();
                        if (header.PaletteCount > header.ImageCount || header.Pad != 0)
                        {
                            Debug.WriteLine($"Unknown RPK. PaletteCount: {header.PaletteCount}, ImageCount: {header.ImageCount}, Pad: {header.Pad:x4}");
                            return null;
                        }
                    }

                    return (fullPath, parentRelativePath, parent, parentPath) =>
                        new RpkFileFileSystem(parentRelativePath, parent);
                }

                return null;
            });

            services.AddSingleton<FileHandlerResolver<MagickImage>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (parent is RpkFileFileSystem &&
                    string.Equals(parent.Path.GetExtension(parentRelativePath), ".img", StringComparison.OrdinalIgnoreCase))
                {
                    return new(
                        read: (fullPath, parentRelativePath, parent, parentPath) =>
                        {
                            var name = parent.Path.GetFileNameWithoutExtension(parentRelativePath).Split('_', 2);
                            var entry = (ImageIndex: int.Parse(name[0]), PaletteIndex: int.Parse(name[1]));
                            using var palette = parent.File.OpenRead($"{entry.PaletteIndex}.pal");
                            using var file = parent.File.OpenRead(parentRelativePath);
                            return Load(palette, file);
                        });
                }

                return null;
            });
        }

        private class RpkFileFileSystem(string path, IFileSystem parent) : IndexedFileSystem<Entry>
        {
            protected override IEnumerable<Entry> ReadIndex()
            {
                using var stream = parent.File.OpenRead(path);
                var header = stream.ReadLittleEndian<Header>();
                var entries = header.PaletteCount + header.ImageCount;
                var offsets = stream.ReadArrayLittleEndian<int>(entries);
                var startOffset = Marshal.SizeOf<Header>() + (sizeof(int) * entries);
                var descriptorSize = Marshal.SizeOf<ItemDescriptor>();
                var paletteIndex = -1;
                for (var i = 0; i < entries; i++)
                {
                    var offset = offsets[i] + startOffset;
                    stream.Position = offset;
                    var desc = stream.ReadLittleEndian<ItemDescriptor>();
                    if (desc.Y == 0 && desc.W == 0 && desc.H == 0)
                    {
                        paletteIndex = i;
                        yield return (i, paletteIndex, offset, desc.X * sizeof(ushort) + descriptorSize);
                    }
                    else
                    {
                        if (paletteIndex != -1)
                        {
                            var palette = i switch
                            {
                                22 => 46,
                                27 => 47,
                                48 => 39,
                                _ => paletteIndex,
                            };
                            yield return (i, palette, offset, desc.W * desc.H * sizeof(byte) * 2 + descriptorSize);
                        }
                    }
                }
            }

            protected override string GetEntryName(Entry entry) =>
                entry.ImageIndex == entry.PaletteIndex
                    ? $"{entry.ImageIndex}.pal"
                    : $"{entry.ImageIndex}_{entry.PaletteIndex}.img";

            protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
            {
                FileBase.EnsureReadOnly(parentOptions, "Writing to sub images in .txp files is not supported.");
                var file = parent.File.Open(path, parentOptions);
                return new OffsetStreamSpan(file, entry.Offset, entry.Length, Ownership.Dispose);
            }
        }

        public static MagickImage? Load(Stream paletteStream, Stream imageStream)
        {
            var desc = imageStream.ReadLittleEndian<ItemDescriptor>();
            var paletteDesc = paletteStream.ReadLittleEndian<ItemDescriptor>();

            var writer = new TgaWriter<int, byte>((ushort)(desc.W * 4), desc.H, paletteDesc.X, desc.X, desc.Y);
            for (var x = 0; x < paletteDesc.X; x++)
            {
                var v = paletteStream.ReadUInt16LittleEndian();
                static int Expand5(int x) => (x << 3) | (x >> 2); // x * 255 / 31
                writer.WriteColor(
                    ((v & 0x8000) != 0 ? 0xFF : 0x00) << 24 |
                    Expand5((v >> 0) & 0x1F) << 16 |
                    Expand5((v >> 5) & 0x1F) << 8 |
                    Expand5((v >> 10) & 0x1F) << 0);
            }

            var count = desc.W * desc.H * 2;
            for (var x = 0; x < count; x++)
            {
                var data = imageStream.ReadByte();
                writer.WriteIndex((byte)(data & 0x0F));
                writer.WriteIndex((byte)(data >> 4));
            }

            return writer.ToMagickImage();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Header
        {
            public byte PaletteCount;
            public byte ImageCount;
            public ushort Pad;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct ItemDescriptor
        {
            public byte X;
            public byte Y;
            public byte W;
            public byte H;
        }
    }
}
