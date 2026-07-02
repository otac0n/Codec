// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.IO.Abstractions;
    using System.Runtime.InteropServices;
    using Codec;
    using Codec.Archives;
    using Codec.Imaging;
    using ImageMagick;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (int ImageIndex, int PaletteIndex);

    internal class RpkFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".rpk", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(parent.Path.GetExtension(parentRelativePath), ".res", StringComparison.OrdinalIgnoreCase))
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
                    {
                        return new RpkFileFileSystem(parentRelativePath, parent);
                    };
                }

                return null;
            });

            services.AddSingleton<FileHandlerResolver<MagickImage>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (parent is RpkFileFileSystem)
                {
                    return (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var file = parent.File.OpenRead(parentRelativePath);
                        var name = parent.Path.GetFileNameWithoutExtension(parentRelativePath).Split('_', 2);
                        var entry = (int.Parse(name[0]), int.Parse(name[1]));
                        return Load(file, entry);
                    };
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
                var paletteIndex = -1;
                for (var i = 0; i < entries; i++)
                {
                    stream.Position = offsets[i] + startOffset;
                    var desc = stream.ReadLittleEndian<ItemDescriptor>();
                    if (desc.Y == 0 && desc.W == 0 && desc.H == 0)
                    {
                        paletteIndex = i;
                    }
                    else
                    {
                        if (paletteIndex != -1)
                        {
                            yield return (i, i switch
                            {
                                22 => 46,
                                27 => 47,
                                48 => 39,
                                _ => paletteIndex,
                            });
                        }
                    }
                }
            }

            protected override string GetEntryName(Entry entry) =>
                $"{entry.ImageIndex}_{entry.PaletteIndex}.img";

            protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
            {
                // HACK: Each sub-file loads the whole file using the filename to locate the entry.
                FileBase.EnsureReadOnly(parentOptions, "Writing to sub images in .txp files is not supported.");
                return parent.File.Open(path, parentOptions);
            }
        }

        public static MagickImage? Load(Stream stream, Entry entry)
        {
            var header = stream.ReadLittleEndian<Header>();
            var entries = header.PaletteCount + header.ImageCount;
            var offsets = stream.ReadArrayLittleEndian<int>(entries);
            var startOffset = Marshal.SizeOf<Header>() + (sizeof(int) * entries);

            stream.Position = offsets[entry.ImageIndex] + startOffset;
            var desc = stream.ReadLittleEndian<ItemDescriptor>();
            var imageOffset = stream.Position;

            stream.Position = offsets[entry.PaletteIndex] + startOffset;
            var paletteDesc = stream.ReadLittleEndian<ItemDescriptor>();

            var writer = new TgaWriter<int, byte>((ushort)(desc.W * 4), desc.H, paletteDesc.X);
            for (var x = 0; x < paletteDesc.X; x++)
            {
                var v = stream.ReadUInt16LittleEndian();
                static int Expand5(int x) => (x << 3) | (x >> 2);
                var c = Color.FromArgb((v & 0x8000) != 0 ? 0xFF : 0x00, Expand5((v >> 10) & 0x1F), Expand5((v >> 5) & 0x1F), Expand5((v >> 0) & 0x1F));
                writer.WriteColor(
                    ((v & 0x8000) != 0 ? 0xFF : 0x00) << 24 |
                    Expand5((v >> 0) & 0x1F) << 16 |
                    Expand5((v >> 5) & 0x1F) << 8 |
                    Expand5((v >> 10) & 0x1F) << 0);
            }

            stream.Position = imageOffset;
            var count = desc.W * desc.H * 2;
            for (var x = 0; x < count; x++)
            {
                var data = stream.ReadByte();
                writer.WriteIndex((byte)(data & 0x0F));
                writer.WriteIndex((byte)(data >> 4));
            }

            return writer.ToMagickImage();
        }

        struct Header
        {
            public byte PaletteCount;
            public byte ImageCount;
            public ushort Pad;
        }

        struct ItemDescriptor
        {
            public byte X;
            public byte Y;
            public byte W;
            public byte H;
        }
    }
}
