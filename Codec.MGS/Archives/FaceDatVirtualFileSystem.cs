// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Archives
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Codec;
    using Codec.Archives;
    using Codec.Imaging;
    using Codec.Services;
    using DiscUtils.Streams;
    using ImageMagick;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (int Group, ushort Id, bool IsAnimation, long Offset, long Size);
    using ImageEntry = (int Index, int PaletteIndex, long Offset, long Length, bool IsPalette);

    public class FaceDatVirtualFileSystem(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        private static readonly int PaletteCount = 1 << 8;
        private static readonly int PaletteSize = sizeof(ushort) * PaletteCount;

        public static void Register(IServiceCollection services)
        {
            services.AddSingleton(new EntryTypeMatcher(EntryType.Image, "*.img"));

            services.AddSingleton<FileSystemResolver>((servicProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetFileName(parentRelativePath), "FACE.DAT", StringComparison.OrdinalIgnoreCase))
                {
                    return static (fullPath, parentRelativePath, parent, parentPath) =>
                        new FaceDatVirtualFileSystem(parentRelativePath, parent);
                }

                if (parent is FaceDatVirtualFileSystem faceDatVFS)
                {
                    if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".face", StringComparison.OrdinalIgnoreCase))
                    {
                        return static (fullPath, parentRelativePath, parent, parentPath) =>
                            new FaceFileSystem(parentRelativePath, parent);
                    }
                    else if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".anim", StringComparison.OrdinalIgnoreCase))
                    {
                        return static (fullPath, parentRelativePath, parent, parentPath) =>
                            new AnimFileSystem(parentRelativePath, parent);
                    }
                }

                return null;
            });

            services.AddSingleton<FileHandlerResolver<MagickImage>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (parent is FaceFileSystem or AnimFileSystem &&
                    string.Equals(parent.Path.GetExtension(parentRelativePath), ".img", StringComparison.OrdinalIgnoreCase))
                {
                    return new(
                        read: (fullPath, parentRelativePath, parent, parentPath) =>
                        {
                            using var file = parent.File.OpenRead(parentRelativePath);
                            using var palette = parent.File.OpenRead(GetPaletteFileName(parent, parentRelativePath));
                            return Load(palette, file);
                        },
                        write: (image, fullPath, parentRelativePath, parent, parentPath) =>
                        {
                            using var file = parent.File.Open(parentRelativePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                            using var palette = parent.File.OpenRead(GetPaletteFileName(parent, parentRelativePath));
                            Write(palette, image, file);
                        });
                }

                return null;
            });
        }

        protected override IEnumerable<Entry> ReadIndex()
        {
            var index = new List<Entry>();

            using var source = parent.File.OpenRead(parentRelativePath);
            for (var group = 0; source.Position < source.Length; group++)
            {
                var total = source.ReadInt32LittleEndian();
                if (total < 0 || total > (source.Length - source.Position) / Marshal.SizeOf<Header>())
                {
                    // This is not a FACE.DAT file we recognize.
                    return [];
                }

                var position = source.Position;
                var headers = source.ReadArrayLittleEndian<Header>(total);
                for (var h = 0; h < total; h++)
                {
                    var header = headers[h];
                    index.Add((group, header.Id, header.Animation > 0, position + header.Offset, header.Size));
                }

                position += headers.Max(h => h.Offset + h.Size);
                position = StreamExtensions.Align(position, 2048);
                source.Position = position;
            }

            return index;
        }

        protected override string GetEntryName(Entry entry) =>
            this.Path.Combine($"{entry.Group}", $"{entry.Id:x4}.{(entry.IsAnimation ? "anim" : "face")}");

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions) =>
            new OffsetStreamSpan(parent.File.Open(parentRelativePath, parentOptions), entry.Offset, entry.Size, Ownership.Dispose);

        private static string GetPaletteFileName(IFileSystem parent, string parentRelativePath) =>
            parent is FaceFileSystem
                ? "palette.pal"
                : $"{parent.Path.GetFileNameWithoutExtension(parentRelativePath).Split('_', 2)[1]}.pal";

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public readonly ushort Animation;
            public readonly ushort Id;
            public readonly uint Size;
            public readonly uint Offset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FrameHeader
        {
            public readonly uint PaletteOffset;
            public readonly uint FrameOffset;
            public readonly uint Unknown;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ImageDimensions
        {
            public readonly sbyte U;
            public readonly sbyte V;
            public readonly sbyte W;
            public readonly sbyte H;
        }

        private abstract class ImageFileSystem(string path, IFileSystem parent) : IndexedFileSystem<ImageEntry>
        {
            protected override Stream Open(ImageEntry entry, FileStreamOptions parentOptions) =>
                new OffsetStreamSpan(parent.File.Open(path, parentOptions), entry.Offset, entry.Length, Ownership.Dispose);
        }

        private class FaceFileSystem(string path, IFileSystem parent) : ImageFileSystem(path, parent)
        {
            private static readonly string[] ImageKeys = ["base", "eyes-droop", "eyes-blink", "unknown", "mouth-e", "mouth-a"];

            protected override IEnumerable<ImageEntry> ReadIndex()
            {
                using var source = parent.File.OpenRead(path);
                var paletteOffset = source.ReadUInt32LittleEndian();
                var imageOffsets = source.ReadArrayLittleEndian<uint>(ImageKeys.Length);

                var index = new List<ImageEntry>
                {
                    (-1, -1, paletteOffset, PaletteSize, true),
                };

                for (var i = 0; i < imageOffsets.Length; i++)
                {
                    if (imageOffsets[i] == 0)
                    {
                        continue;
                    }

                    source.Position = imageOffsets[i];
                    var dim = source.ReadLittleEndian<ImageDimensions>();
                    var length = Marshal.SizeOf<ImageDimensions>() + (dim.W * dim.H);

                    index.Add((i, -1, imageOffsets[i], length, false));
                }

                return index;
            }

            protected override string GetEntryName(ImageEntry entry) =>
                entry.IsPalette ? "palette.pal" : $"{ImageKeys[entry.Index]}.img";
        }

        private class AnimFileSystem(string path, IFileSystem parent) : ImageFileSystem(path, parent)
        {
            protected override IEnumerable<ImageEntry> ReadIndex()
            {
                using var source = parent.File.OpenRead(path);
                var frameCount = source.ReadUInt32LittleEndian();
                var frameHeaders = source.ReadArrayLittleEndian<FrameHeader>((int)frameCount);

                var paletteIndices = new Dictionary<long, int>();
                var index = new List<ImageEntry>();

                for (var i = 0; i < frameHeaders.Length; i++)
                {
                    var header = frameHeaders[i];
                    if (header.PaletteOffset == 0 || header.FrameOffset == 0)
                    {
                        continue;
                    }

                    if (!paletteIndices.TryGetValue(header.PaletteOffset, out var paletteIndex))
                    {
                        paletteIndex = paletteIndices.Count;
                        paletteIndices.Add(header.PaletteOffset, paletteIndex);
                        index.Add((paletteIndex, paletteIndex, header.PaletteOffset, PaletteSize, true));
                    }

                    source.Position = header.FrameOffset;
                    var dim = source.ReadLittleEndian<ImageDimensions>();
                    var length = Marshal.SizeOf<ImageDimensions>() + (dim.W * dim.H);

                    index.Add((i, paletteIndex, header.FrameOffset, length, false));
                }

                return index;
            }

            protected override string GetEntryName(ImageEntry entry) =>
                entry.IsPalette ? $"{entry.Index}.pal" : $"{entry.Index}_{entry.PaletteIndex}.img";
        }

        private static int Intensity(int c) =>
            ((c & 0b00010000) >> 4) * 80 +
            ((c & 0b00001000) >> 3) * 40 +
            ((c & 0b00000100) >> 2) * 20 +
            ((c & 0b00000010) >> 1) * 10 +
            ((c & 0b00000001) >> 0) * 8 +
            16;

        public static MagickImage Load(Stream paletteStream, Stream imageStream)
        {
            var dim = imageStream.ReadLittleEndian<ImageDimensions>();
            var writer = new TgaWriter<int, byte>((ushort)dim.W, (ushort)dim.H, (ushort)PaletteCount, dim.U, dim.V);

            for (var i = 0; i < PaletteCount; i++)
            {
                var color = paletteStream.ReadUInt16LittleEndian();
                writer.WriteColor(
                    ((color & 0x8000) != 0 ? 0xFF : 0x00) << 24 |
                    Intensity((color >> 0) & 0x001F) << 16 |
                    Intensity((color >> 5) & 0x001F) << 8 |
                    Intensity((color >> 10) & 0x001F) << 0);
            }

            var count = dim.W * dim.H;
            for (var i = 0; i < count; i++)
            {
                writer.WriteIndex((byte)imageStream.ReadByte());
            }

            return writer.ToMagickImage();
        }

        private static void Write(Stream paletteStream, MagickImage image, Stream outputStream)
        {
            var palette = new MagickColor[PaletteCount];
            for (var i = 0; i < PaletteCount; i++)
            {
                var color = paletteStream.ReadUInt16LittleEndian();
                var a = (byte)((color & 0x8000) != 0 ? 0xFF : 0x00);
                var r = (byte)Intensity((color >> 0) & 0x001F);
                var g = (byte)Intensity((color >> 5) & 0x001F);
                var b = (byte)Intensity((color >> 10) & 0x001F);
                palette[i] = new MagickColor(r, g, b, a);
            }

            var dim = outputStream.ReadLittleEndian<ImageDimensions>();
            if (image.Width != dim.W || image.Height != dim.H)
            {
                throw new NotImplementedException($"Replacement image must be exactly {dim.W}x{dim.H} to avoid index rewrite. Found {image.Width}x{image.Height}.");
            }

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

            outputStream.Write(indices, 0, indices.Length);
        }
    }
}
