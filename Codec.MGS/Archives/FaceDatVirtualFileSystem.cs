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
    using ImageEntry = (int Index, long PaletteOffset, long ImageOffset);

    public class FaceDatVirtualFileSystem : IndexedFileSystem<Entry>
    {

        private readonly string parentRelativePath;
        private readonly IFileSystem parent;

        public FaceDatVirtualFileSystem(string parentRelativePath, IFileSystem parent)
        {
            this.parentRelativePath = parentRelativePath;
            this.parent = parent;
        }

        public static void Register(IServiceCollection services)
        {
            services.AddSingleton(new EntryTypeMatcher(EntryTypeDetector.EntryType.Image, "*.img"));

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
        }

        protected override IEnumerable<Entry> ReadIndex()
        {
            var index = new List<Entry>();

            using var source = this.parent.File.OpenRead(this.parentRelativePath);
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
            new OffsetStreamSpan(this.parent.File.Open(this.parentRelativePath, parentOptions), entry.Offset, entry.Size, Ownership.Dispose);

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private struct Header
        {
            public readonly ushort Animation;
            public readonly ushort Id;
            public readonly uint Size;
            public readonly uint Offset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private struct FrameHeader
        {
            public readonly uint PaletteOffset;
            public readonly uint FrameOffset;
            public readonly uint Unknown;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private struct ImageDimensions
        {
            public readonly sbyte U;
            public readonly sbyte V;
            public readonly sbyte W;
            public readonly sbyte H;
        }

        private abstract class ImageFileSystem : IndexedFileSystem<ImageEntry>
        {
            protected readonly string parentRelativePath;
            protected readonly IFileSystem parent;

            public ImageFileSystem(string parentRelativePath, IFileSystem parent)
            {
                this.parentRelativePath = parentRelativePath;
                this.parent = parent;
            }

            protected override Stream Open(ImageEntry entry, FileStreamOptions parentOptions)
            {
                FileBase.EnsureReadOnly(parentOptions, "Writing to sub images in FACE.DAT is not supported.");

                using var source = this.parent.File.Open(this.parentRelativePath, parentOptions);
                var dest = new MemoryStream();
                var (_, _, img) = GetImage(source, entry.PaletteOffset, entry.ImageOffset);
                img.Write(dest, MagickFormat.Png);
                dest.Position = 0;
                return dest;
            }
        }

        private class FaceFileSystem : ImageFileSystem
        {
            private static readonly string[] ImageKeys = ["base", "eyes-droop", "eyes-blink", "unknown", "mouth-e", "mouth-a"];

            public FaceFileSystem(string parentRelativePath, IFileSystem parent)
                : base(parentRelativePath, parent)
            {
            }

            protected override IEnumerable<ImageEntry> ReadIndex()
            {
                var index = new List<ImageEntry>();

                using var source = this.parent.File.OpenRead(this.parentRelativePath);
                var paletteOffset = source.ReadUInt32LittleEndian();
                var imageOffsets = source.ReadArrayLittleEndian<uint>(ImageKeys.Length);
                for (var i = 0; i < imageOffsets.Length; i++)
                {
                    if (imageOffsets[i] != 0)
                    {
                        index.Add((i, paletteOffset, imageOffsets[i]));
                    }
                }

                return index;
            }

            protected override string GetEntryName(ImageEntry entry) =>
                ImageKeys[entry.Index] + ".img";
        }

        private class AnimFileSystem : ImageFileSystem
        {
            public AnimFileSystem(string parentRelativePath, IFileSystem parent)
                : base(parentRelativePath, parent)
            {
            }

            protected override IEnumerable<ImageEntry> ReadIndex()
            {
                var index = new List<ImageEntry>();

                using var source = this.parent.File.OpenRead(this.parentRelativePath);
                var frames = source.ReadUInt32LittleEndian();
                var frameHeaders = source.ReadArrayLittleEndian<FrameHeader>((int)frames);
                for (var i = 0; i < frameHeaders.Length; i++)
                {
                    var header = frameHeaders[i];
                    if (header.PaletteOffset != 0 && header.FrameOffset != 0)
                    {
                        index.Add((i, header.PaletteOffset, header.FrameOffset));
                    }
                }

                return index;
            }

            protected override string GetEntryName(ImageEntry entry) =>
                entry.Index + ".img";
        }

        static (int X, int Y, MagickImage Image) GetImage(Stream source, long paletteOffset, long imageOffset)
        {
            const int PaletteCount = 1 << 8; // 8BPP
            const int PaletteSize = sizeof(ushort) * PaletteCount;

            source.Seek(imageOffset, SeekOrigin.Begin);
            var dim = source.ReadLittleEndian<ImageDimensions>();
            var tgaWriter = new TgaWriter<int, byte>((ushort)dim.W, (ushort)dim.H, PaletteCount);

            var imgPosition = source.Position;

            static int Intensity(int c) =>
                ((c & 0b00010000) >> 4) * 80 +
                ((c & 0b00001000) >> 3) * 40 +
                ((c & 0b00000100) >> 2) * 20 +
                ((c & 0b00000010) >> 1) * 10 +
                ((c & 0b00000001) >> 0) * 8 +
                16;

            source.Seek(paletteOffset, SeekOrigin.Begin);
            for (var i = 0; i < PaletteCount; i++)
            {
                var color = source.ReadUInt16LittleEndian();
                tgaWriter.WriteColor(
                    ((color & 0x8000) != 0 ? 0xFF : 0x00) << 24 |
                    Intensity((color >> 0) & 0x001F) << 16 |
                    Intensity((color >> 5) & 0x001F) << 8 |
                    Intensity((color >> 10) & 0x001F) << 0);
            }

            source.CopyTo(tgaWriter.TgaStream, imgPosition, SeekOrigin.Begin, dim.W * dim.H);

            return (dim.U, dim.V, tgaWriter.ToMagickImage());
        }
    }
}
