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
        private static readonly ushort PaletteCount = 1 << 8;
        private static readonly int PaletteSize = sizeof(ushort) * PaletteCount;
        private static readonly int Alignment = 0x800;

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

                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".face", StringComparison.OrdinalIgnoreCase))
                {
                    return static (fullPath, parentRelativePath, parent, parentPath) =>
                        new FaceFileSystem(parentRelativePath, parent);
                }

                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".anim", StringComparison.OrdinalIgnoreCase))
                {
                    return static (fullPath, parentRelativePath, parent, parentPath) =>
                        new AnimFileSystem(parentRelativePath, parent);
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

                if (parent is FaceFileSystem or AnimFileSystem &&
                    string.Equals(parent.Path.GetExtension(parentRelativePath), ".pal", StringComparison.OrdinalIgnoreCase))
                {
                    return new(
                        read: (fullPath, parentRelativePath, parent, parentPath) =>
                        {
                            using var palette = parent.File.OpenRead(GetPaletteFileName(parent, parentRelativePath));
                            return LoadPaletteImage(palette);
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
                position = StreamExtensions.Align(position, Alignment);
                source.Position = position;
            }

            return index;
        }

        protected override string GetEntryName(Entry entry) =>
            this.Path.Combine($"{entry.Group}", $"{entry.Id:x4}.{(entry.IsAnimation ? "anim" : "face")}");

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions) =>
            CreateStreamWrapper(
                parentOptions,
                options => new OffsetStreamSpan(parent.File.Open(parentRelativePath, options), entry.Offset, entry.Size, Ownership.Dispose),
                updated => this.WriteEntry(entry, updated));

        private void WriteEntry(Entry entry, Stream updated)
        {
            using var stream = parent.File.Open(parentRelativePath, FileMode.Open, FileAccess.ReadWrite);

            var delta = updated.Length - entry.Size;
            if (delta == 0)
            {
                stream.Position = entry.Offset;
                updated.Position = 0;
                updated.CopyTo(stream);
                this.index = null;
                return;
            }

            var (_, entriesBase, headers) = FindGroup(stream, entry.Group);
            var headerIndex = Array.FindIndex(headers, h => entriesBase + h.Offset == entry.Offset);
            if (headerIndex < 0)
            {
                throw new InvalidOperationException("Could not locate the entry to update; the archive may have changed since it was last indexed.");
            }

            var oldDataEnd = entriesBase + headers.Max(h => h.Offset + h.Size);
            var oldAlignedEnd = StreamExtensions.Align(oldDataEnd, Alignment);

            var oldHeader = headers[headerIndex];
            var oldEntryAbsOffset = entriesBase + oldHeader.Offset;
            var oldEntryAbsEnd = oldEntryAbsOffset + oldHeader.Size;

            for (var i = 0; i < headers.Length; i++)
            {
                if (headers[i].Offset > oldHeader.Offset)
                {
                    headers[i].Offset = checked((uint)(headers[i].Offset + delta));
                }
            }

            headers[headerIndex].Size = checked((uint)updated.Length);

            var newDataEnd = oldDataEnd + delta;
            var newAlignedEnd = StreamExtensions.Align(newDataEnd, Alignment);
            var alignedDelta = newAlignedEnd - oldAlignedEnd;

            if (alignedDelta != 0)
            {
                var originalLength = stream.Length;
                if (alignedDelta > 0)
                {
                    stream.SetLength(originalLength + alignedDelta);
                    ShiftRegion(stream, oldAlignedEnd, oldAlignedEnd + alignedDelta, originalLength - oldAlignedEnd);
                }
                else
                {
                    ShiftRegion(stream, oldAlignedEnd, oldAlignedEnd + alignedDelta, originalLength - oldAlignedEnd);
                    stream.SetLength(originalLength + alignedDelta);
                }
            }

            ShiftRegion(stream, oldEntryAbsEnd, oldEntryAbsOffset + updated.Length, oldDataEnd - oldEntryAbsEnd);

            stream.Position = oldEntryAbsOffset;
            updated.Position = 0;
            updated.CopyTo(stream);

            stream.Position = entriesBase;
            stream.WriteArrayLittleEndian(headers);

            this.index = null;
        }

        private static (long GroupStart, long EntriesBase, Header[] Headers) FindGroup(Stream source, int targetGroup)
        {
            for (var group = 0; source.Position < source.Length; group++)
            {
                var groupStart = source.Position;
                var total = source.ReadInt32LittleEndian();
                var entriesBase = source.Position;
                var headers = source.ReadArrayLittleEndian<Header>(total);

                if (group == targetGroup)
                {
                    return (groupStart, entriesBase, headers);
                }

                var dataEnd = entriesBase + headers.Max(h => h.Offset + h.Size);
                source.Position = StreamExtensions.Align(dataEnd, Alignment);
            }

            throw new InvalidOperationException($"Group {targetGroup} was not found.");
        }

        private static void ShiftRegion(Stream stream, long readPosition, long writePosition, long length)
        {
            if (readPosition == writePosition || length <= 0)
            {
                return;
            }

            var buffer = new byte[Math.Min(8 * 1024, length)];
            if (writePosition > readPosition)
            {
                var remaining = length;
                while (remaining > 0)
                {
                    var chunk = (int)Math.Min(buffer.Length, remaining);
                    stream.Position = readPosition + remaining - chunk;
                    stream.ReadExactly(buffer, 0, chunk);
                    stream.Position = writePosition + remaining - chunk;
                    stream.Write(buffer, 0, chunk);
                    remaining -= chunk;
                }
            }
            else
            {
                var offset = 0L;
                while (offset < length)
                {
                    var chunk = (int)Math.Min(buffer.Length, length - offset);
                    stream.Position = readPosition + offset;
                    stream.ReadExactly(buffer, 0, chunk);
                    stream.Position = writePosition + offset;
                    stream.Write(buffer, 0, chunk);
                    offset += chunk;
                }
            }
        }

        private static string GetPaletteFileName(IFileSystem parent, string parentRelativePath) =>
            parent is FaceFileSystem
                ? "palette.pal"
                : $"{parent.Path.GetFileNameWithoutExtension(parentRelativePath).Split('_', 2)[1]}.pal";

        private abstract class ImageFileSystem(string path, IFileSystem parent) : IndexedFileSystem<ImageEntry>
        {
            protected override Stream Open(ImageEntry entry, FileStreamOptions parentOptions) =>
                CreateStreamWrapper(
                    parentOptions,
                    options => new OffsetStreamSpan(parent.File.Open(path, options), entry.Offset, entry.Length, Ownership.Dispose),
                    updated => this.WriteEntry(entry, updated));

            protected abstract void WriteEntry(ImageEntry entry, Stream updated);
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

            protected override void WriteEntry(ImageEntry entry, Stream updated)
            {
                if (entry.IsPalette && updated.Length != PaletteSize)
                {
                    throw new ArgumentException($"A palette must be exactly {PaletteSize} bytes.", nameof(updated));
                }

                using var stream = parent.File.Open(path, FileMode.Open, FileAccess.ReadWrite);

                var delta = updated.Length - entry.Length;
                if (delta != 0)
                {
                    var originalLength = stream.Length;
                    var oldEntryEnd = entry.Offset + entry.Length;
                    ShiftRegion(stream, oldEntryEnd, oldEntryEnd + delta, originalLength - oldEntryEnd);
                    stream.SetLength(originalLength + delta);

                    stream.Position = 0;
                    var paletteOffset = stream.ReadUInt32LittleEndian();
                    var imageOffsets = stream.ReadArrayLittleEndian<uint>(ImageKeys.Length);

                    if (paletteOffset > entry.Offset)
                    {
                        paletteOffset = checked((uint)(paletteOffset + delta));
                    }

                    for (var i = 0; i < imageOffsets.Length; i++)
                    {
                        if (imageOffsets[i] > entry.Offset)
                        {
                            imageOffsets[i] = checked((uint)(imageOffsets[i] + delta));
                        }
                    }

                    stream.Position = 0;
                    stream.WriteLittleEndian(paletteOffset);
                    stream.WriteArrayLittleEndian(imageOffsets);
                }

                stream.Position = entry.Offset;
                updated.Position = 0;
                updated.CopyTo(stream);

                this.index = null;
            }
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

            protected override void WriteEntry(ImageEntry entry, Stream updated)
            {
                if (entry.IsPalette && updated.Length != PaletteSize)
                {
                    throw new ArgumentException($"A palette must be exactly {PaletteSize} bytes.", nameof(updated));
                }

                using var stream = parent.File.Open(path, FileMode.Open, FileAccess.ReadWrite);

                var delta = updated.Length - entry.Length;
                if (delta != 0)
                {
                    var originalLength = stream.Length;
                    var oldEntryEnd = entry.Offset + entry.Length;
                    ShiftRegion(stream, oldEntryEnd, oldEntryEnd + delta, originalLength - oldEntryEnd);
                    stream.SetLength(originalLength + delta);

                    stream.Position = 0;
                    var frameCount = stream.ReadUInt32LittleEndian();
                    var frameHeaders = stream.ReadArrayLittleEndian<FrameHeader>((int)frameCount);

                    for (var i = 0; i < frameHeaders.Length; i++)
                    {
                        var header = frameHeaders[i];
                        if (header.PaletteOffset > entry.Offset)
                        {
                            header.PaletteOffset = checked((uint)(header.PaletteOffset + delta));
                        }

                        if (header.FrameOffset > entry.Offset)
                        {
                            header.FrameOffset = checked((uint)(header.FrameOffset + delta));
                        }

                        frameHeaders[i] = header;
                    }

                    stream.Position = sizeof(uint);
                    stream.WriteArrayLittleEndian(frameHeaders);
                }

                stream.Position = entry.Offset;
                updated.Position = 0;
                updated.CopyTo(stream);

                this.index = null;
            }
        }

        public static MagickImage Load(Stream paletteStream, Stream imageStream)
        {
            var dim = imageStream.ReadLittleEndian<ImageDimensions>();
            var writer = new TgaWriter<int, byte>((ushort)dim.W, (ushort)dim.H, PaletteCount, dim.U, dim.V);

            ReadPalette(paletteStream, writer);

            var count = dim.W * dim.H;
            for (var i = 0; i < count; i++)
            {
                writer.WriteIndex((byte)imageStream.ReadByte());
            }

            return writer.ToMagickImage();
        }

        public static MagickImage LoadPaletteImage(Stream paletteStream)
        {
            var w = PaletteCount;
            ushort h = 1;
            var writer = new TgaWriter<int, byte>(w, h, PaletteCount);

            ReadPalette(paletteStream, writer);

            var count = w * h;
            for (var i = 0; i < count; i++)
            {
                writer.WriteIndex((byte)i);
            }

            return writer.ToMagickImage();
        }

        private static void ReadPalette(Stream paletteStream, TgaWriter<int, byte> writer)
        {
            for (var i = 0; i < PaletteCount; i++)
            {
                var color = paletteStream.ReadUInt16LittleEndian();
                var a = (color & 0x8000) != 0 ? 0xFF : 0;
                var r = ColorUtils.Expand5To8((color >> 0) & 0x001F);
                var g = ColorUtils.Expand5To8((color >> 5) & 0x001F);
                var b = ColorUtils.Expand5To8((color >> 10) & 0x001F);
                writer.WriteColor(
                    a << 24 |
                    r << 16 |
                    g << 8 |
                    b << 0);
            }
        }

        private static void Write(Stream paletteStream, MagickImage image, Stream outputStream)
        {
            var palette = new MagickColor[PaletteCount];
            for (var i = 0; i < PaletteCount; i++)
            {
                var color = paletteStream.ReadUInt16LittleEndian();
                var a = (color & 0x8000) != 0 ? Quantum.Max : (ushort)0;
                var r = ColorUtils.Expand5To16((color >> 0) & 0x001F);
                var g = ColorUtils.Expand5To16((color >> 5) & 0x001F);
                var b = ColorUtils.Expand5To16((color >> 10) & 0x001F);
                palette[i] = new MagickColor(r, g, b, a);
            }

            var dim = outputStream.ReadLittleEndian<ImageDimensions>();

            if (image.Page.X != 0 || image.Page.Y != 0)
            {
                dim.U = (sbyte)image.Page.X;
                dim.V = (sbyte)image.Page.Y;
            }

            dim.W = (sbyte)image.Width;
            dim.H = (sbyte)image.Height;

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

            outputStream.Position = 0;
            outputStream.WriteLittleEndian(dim);
            outputStream.Write(indices, 0, indices.Length);
            outputStream.SetLength(outputStream.Position);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public ushort Animation;
            public ushort Id;
            public uint Size;
            public uint Offset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FrameHeader
        {
            public uint PaletteOffset;
            public uint FrameOffset;
            public uint Unknown;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ImageDimensions
        {
            public sbyte U;
            public sbyte V;
            public sbyte W;
            public sbyte H;
        }
    }
}
