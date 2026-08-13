// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Archives
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Abstractions;
    using System.Runtime.InteropServices;
    using Codec;
    using Codec.Archives;
    using Codec.MGS.Files;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (int Group, int Index, string Name, long Offset, long Size);

    public class RadioDatArchive(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        private static readonly int Alignment = 0x800;
        private static readonly int HeaderSize = Marshal.SizeOf<Header>();

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("*RADIO*.DAT", static (fullPath, parentRelativePath, parent, parentPath) => new RadioDatArchive(parentRelativePath, parent));
        }

        protected override string GetEntryName(Entry entry) =>
            $"{entry.Group / 100f:0.00}/{entry.Index}/{entry.Name}";

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions) =>
            CreateStreamWrapper(
                parentOptions,
                options => new OffsetStreamSpan(parent.File.Open(parentRelativePath, options), entry.Offset, entry.Size, Ownership.Dispose),
                updated => throw new NotSupportedException());

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var source = parent.File.OpenRead(parentRelativePath);

            var indices = new Dictionary<ushort, int>();
            var entries = new List<Entry>();

            bool? aligned = null;
            while (source.Position + HeaderSize <= source.Length)
            {
                var position = source.Position;
                var header = source.ReadBigEndian<Header>();
                var size = header.Size + HeaderSize - sizeof(ushort);

                var group = header.Frequency;
                var index = indices[group] = (indices.TryGetValue(group, out var existing) ? existing : 0) + 1;

                entries.Add((group, index, "captions.rad", position, size));

                source.Position = position + size;

                var glyphIndex = 0;
                while (IsGlyph(source, aligned))
                {
                    entries.Add((group, index, $"{glyphIndex:D4}.gly", source.Position, GlyFile.ChunkSize));
                    source.Position += GlyFile.ChunkSize;
                    glyphIndex++;
                }

                ApplyAlignment(source, ref aligned);
            }

            return entries;
        }

        private static bool IsGlyph(Stream source, bool? aligned)
        {
            if (source.Position + GlyFile.ChunkSize > source.Length)
            {
                return false;
            }

            if (aligned == true)
            {
                var padding = StreamExtensions.GetPadding(source.Position, Alignment);
                if (padding == 0)
                {
                    return !LooksLikeHeader(source);
                }

                return !source.PeekAllZeros(GlyFile.ChunkSize);
            }
            else if (aligned == false)
            {
                return !LooksLikeHeader(source);
            }
            else
            {
                var padding = StreamExtensions.GetPadding(source.Position, Alignment);
                if (padding == 0)
                {
                    return !LooksLikeHeader(source);
                }

                if (padding < GlyFile.ChunkSize && source.PeekAllZeros(padding))
                {
                    source.Position += padding;
                    var headerAfterPad = LooksLikeHeader(source);
                    source.Position -= padding;
                    if (headerAfterPad)
                    {
                        return false;
                    }
                }

                return !LooksLikeHeader(source) && !source.PeekAllZeros(GlyFile.ChunkSize);
            }
        }

        private static void ApplyAlignment(Stream source, ref bool? aligned)
        {
            if (aligned is bool value)
            {
                if (value)
                {
                    source.Align(Alignment);
                }

                return;
            }

            var padding = StreamExtensions.GetPadding(source.Position, Alignment);
            if (padding > 0)
            {
                if (LooksLikeHeader(source))
                {
                    aligned = false;
                }
                else
                {
                    if (source.PeekAllZeros(padding))
                    {
                        aligned = true;
                        source.Align(Alignment);
                    }
                    else
                    {
                        throw new InvalidDataException($"Unexpected data at 0x{source.Position:x8}-0x{source.Position + padding:x8}.");
                    }
                }

                Debug.WriteLine($"Alignment set to: {aligned}");
            }
        }

        private static bool LooksLikeHeader(Stream source)
        {
            if (source.Position + HeaderSize > source.Length)
            {
                return false;
            }

            var start = source.Position;
            var header = source.ReadBigEndian<Header>();
            source.Position = start;

            return header.Frequency is >= 14000 and < 14300 && header.Pad == 0 && header.Flags == 0x80;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public ushort Frequency;
            public ushort UnknownA;
            public ushort UnknownB;
            public ushort Pad;
            public byte Flags;
            public ushort Size;
        }
    }
}
