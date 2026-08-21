// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Codec.Archives;
    using Codec.Audio;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using VgmSharp;
    using Buffer = System.Buffer;
    using Entry = (int Index, long Offset, long Length);

    internal class WvxFile
    {
        public static readonly uint SampleRate = 11025;

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("*.wvx", static (fullPath, parentRelativePath, parent, parentPath) => new WvxFileFileSystem(parentRelativePath, parent));
        }

        public static Range PopulateWaveTable(Stream input, WaveTableEntry?[] headers, out Entry[] entries)
        {
            entries = ReadHeaders(input, out var tableHeader, out var waveTable);
            var baseIndex = (int)(tableHeader.BaseAddress / Marshal.SizeOf<WaveTableEntry>());

            foreach (var entry in entries)
            {
                headers[baseIndex + entry.Index] = waveTable[entry.Index];
            }

            return baseIndex..(baseIndex + entries.Length);
        }

        public static Range PopulateWaveTable(Stream input, WaveTableEntry?[] headers, short[]?[] samples, Range?[] loopPoints)
        {
            var range = PopulateWaveTable(input, headers, out var entries);

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var ix = range.Start.Value + i;

                using var vag = PrependVagHeader(input, entry, Ownership.None);
                using var vgm = VgmStreamReader.Open(vag, $"{entry.Index}.vag", config: VgmStreamConfig.PlayOnceNoLoop());
                using var mem = new MemoryStream();
                foreach (var item in vgm.RenderBlocks())
                {
                    mem.Write(item);
                }

                mem.Position = 0;
                samples[ix] = ReadPcm16(mem);
                if (vgm.Format.LoopStart != vgm.Format.LoopEnd)
                {
                    loopPoints[ix] = (int)vgm.Format.LoopStart..(int)vgm.Format.LoopEnd;
                }
            }

            return range;
        }

        private static short[] ReadPcm16(Stream stream)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();

            var samples = new short[bytes.Length / 2];
            Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
            return samples;
        }

        private static Entry[] ReadHeaders(Stream stream, out ChunkHeader tableHeader, out WaveTableEntry[] waveTable)
        {
            tableHeader = stream.ReadBigEndian<ChunkHeader>();
            var count = (int)(tableHeader.DataSize / Marshal.SizeOf<WaveTableEntry>());
            waveTable = stream.ReadArrayLittleEndian<WaveTableEntry>(count);
            var dataHeader = stream.ReadBigEndian<ChunkHeader>();
            var baseOffset = 2 * Marshal.SizeOf<ChunkHeader>() + tableHeader.DataSize;

            var entries = new Entry[waveTable.Length];
            for (var i = 0; i < waveTable.Length; i++)
            {
                var offset = waveTable[i].Offset;
                var nextOffset = waveTable.Select(e => e.Offset).Where(o => o > offset).DefaultIfEmpty(dataHeader.BaseAddress + dataHeader.DataSize).Min();
                entries[i] = (i, baseOffset + offset - dataHeader.BaseAddress, nextOffset - offset);
            }

            return entries;
        }

        private static Stream PrependVagHeader(Stream source, Entry entry, Ownership ownership)
        {
            var headerStream = new MemoryStream();
            var vag = new VagHeader
            {
                Version = 0,
                DataSize = (uint)entry.Length,
                SamplingFreq = SampleRate,
            };

            headerStream.WriteBigEndian(vag);

            return new ConcatStream(
                Ownership.Dispose,
                MappedStream.FromStream(headerStream, Ownership.Dispose),
                new OffsetStreamSpan(source, entry.Offset, entry.Length, ownership));
        }

        private class WvxFileFileSystem(string path, IFileSystem parent) : IndexedFileSystem<Entry>
        {
            private readonly IFileSystem parent = parent ?? new FileSystem();
            private readonly string path = path;

            protected override IEnumerable<Entry> ReadIndex()
            {
                using var stream = this.parent.File.OpenRead(this.path);
                return ReadHeaders(stream, out var _, out var _);
            }

            protected override string GetEntryName(Entry entry) =>
                $"{entry.Index}.vag";

            protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
            {
                FileBase.EnsureReadOnly(parentOptions, "Writing to sub patches in .wvx files is not supported.");
                var source = this.parent.File.Open(this.path, parentOptions);
                return PrependVagHeader(source, entry, Ownership.Dispose);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ChunkHeader
        {
            public uint BaseAddress;
            public uint DataSize;
            public ulong Padding;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct WaveTableEntry
        {
            public uint Offset;
            public sbyte SampleNote;
            public sbyte SampleTune;
            public byte AttackMode;
            public byte AttackRate;
            public byte DecayRate;
            public byte SustainMode;
            public byte SustainRate;
            public byte SustainLevel;
            public byte ReleaseMode;
            public byte ReleaseRate;
            public byte Pan;
            public byte Volume;
        }
    }
}
