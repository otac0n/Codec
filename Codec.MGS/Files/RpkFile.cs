// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Runtime.InteropServices;
    using Codec;
    using Codec.Archives;
    using Codec.Imaging;
    using DiscUtils.Streams;
    using ImageMagick;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
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
                            serviceProvider.GetService<ILogger<RpkFileFileSystem>>()?
                                .LogInformation("Unknown RPK. PaletteCount: '{PaletteCount}', ImageCount: '{ImageCount}', Pad: '{Pad:x4}'", header.PaletteCount, header.ImageCount, header.Pad);
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
                            using var file = parent.File.OpenRead(parentRelativePath);
                            using var palette = parent.File.OpenRead($"{entry.PaletteIndex}.pal");
                            return Load(palette, file);
                        },
                        write: (image, fullPath, parentRelativePath, parent, parentPath) =>
                        {
                            var name = parent.Path.GetFileNameWithoutExtension(parentRelativePath).Split('_', 2);
                            var entry = (ImageIndex: int.Parse(name[0]), PaletteIndex: int.Parse(name[1]));
                            using var file = parent.File.Open(parentRelativePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite); // Share read/write with the palette file stream.
                            using var palette = parent.File.OpenRead($"{entry.PaletteIndex}.pal");
                            Write(palette, image, file);
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
                return CreateStreamWrapper(
                    parentOptions,
                    options => new OffsetStreamSpan(parent.File.Open(path, options), entry.Offset, entry.Length, Ownership.Dispose),
                    updated =>
                    {
                        using var parentStream = parent.File.Open(path, FileMode.Open, FileAccess.ReadWrite);

                        var delta = checked((int)(updated.Length - entry.Length));
                        if (delta == 0)
                        {
                            // Simple overwrite.
                            parentStream.Position = entry.Offset;
                            updated.Position = 0;
                            updated.CopyTo(parentStream);

                            this.index = null;
                            return;
                        }

                        var header = parentStream.ReadLittleEndian<Header>();
                        var entries = header.PaletteCount + header.ImageCount;
                        var offsets = parentStream.ReadArrayLittleEndian<int>(entries);

                        // Read everything after the original entry.
                        parentStream.Position = entry.Offset + entry.Length;
                        updated.Position = updated.Length;
                        parentStream.CopyTo(updated);

                        // Update every later offset.
                        var startOffset = Marshal.SizeOf<Header>() + sizeof(int) * entries;
                        var relativeOffset = (int)(entry.Offset - startOffset);
                        for (var i = 0; i < offsets.Length; i++)
                        {
                            if (offsets[i] > relativeOffset)
                            {
                                offsets[i] += delta;
                            }
                        }

                        // Rewrite offset table.
                        parentStream.Position = Marshal.SizeOf<Header>();
                        parentStream.WriteArrayLittleEndian(offsets);

                        // Rewrite modified entry + tail.
                        parentStream.Position = entry.Offset;
                        updated.Position = 0;
                        updated.CopyTo(parentStream);

                        parentStream.SetLength(parentStream.Position);

                        this.index = null;
                    });
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
                writer.WriteColor(
                    ((v & 0x8000) != 0 ? 0xFF : 0x00) << 24 |
                    ColorUtils.Expand5To8((v >> 0) & 0x1F) << 16 |
                    ColorUtils.Expand5To8((v >> 5) & 0x1F) << 8 |
                    ColorUtils.Expand5To8((v >> 10) & 0x1F) << 0);
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

        private static void Write(Stream paletteToMatch, MagickImage image, Stream outputStream)
        {
            var paletteDesc = paletteToMatch.ReadLittleEndian<ItemDescriptor>();
            var palette = new MagickColor[paletteDesc.X];
            for (var i = 0; i < paletteDesc.X; i++)
            {
                var v = paletteToMatch.ReadUInt16LittleEndian();
                var a = (ushort)((v & 0x8000) != 0 ? Quantum.Max : 0x00);
                var r = ColorUtils.Expand5To16((v >> 0) & 0x1F);
                var g = ColorUtils.Expand5To16((v >> 5) & 0x1F);
                var b = ColorUtils.Expand5To16((v >> 10) & 0x1F);
                palette[i] = new MagickColor(r, g, b, a);
            }

            if (image.Width == 0 || image.Height == 0 || image.Width % 4 != 0)
            {
                throw new ArgumentException($"Image width must be a non-zero multiple of 4. Found {image.Width}x{image.Height}.", nameof(image));
            }

            var desc = outputStream.ReadLittleEndian<ItemDescriptor>();
            if (image.Page.X == 0 && image.Page.Y == 0)
            {
                var oldWidth = desc.W * 4;
                var oldHeight = desc.H;
                desc.X = (byte)(desc.X + (oldWidth - image.Width) / 2);
                desc.Y = (byte)(desc.Y + (oldHeight - image.Height) / 2);
            }
            else
            {
                desc.X = (byte)image.Page.X;
                desc.Y = (byte)image.Page.Y;
            }

            desc.W = (byte)(image.Width / 4);
            desc.H = (byte)image.Height;

            var byteCount = desc.W * desc.H * 2;

            outputStream.Position = 0;
            outputStream.WriteLittleEndian(desc);

            var indices = new byte[image.Width * image.Height];
            using (var pixels = image.GetPixels())
            {
                var i = 0;
                for (var y = 0; y < image.Height; y++)
                {
                    for (var x = 0; x < image.Width; x++)
                    {
                        var color = pixels.GetPixel(x, y).ToColor() ?? MagickColors.Transparent;
                        indices[i++] = ColorUtils.FindClosestPaletteIndex(palette, color);
                    }
                }
            }

            for (var i = 0; i < byteCount; i++)
            {
                var low = indices[i * 2];
                var high = indices[(i * 2) + 1];
                outputStream.WriteByte((byte)((low & 0x0F) | (high << 4)));
            }

            outputStream.SetLength(outputStream.Position);
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
