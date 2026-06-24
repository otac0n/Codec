namespace Codec.MGS.Archives
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using Codec.Archives;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (int Index, SdtVirtualFileSystem.SDTStream Stream, SdtVirtualFileSystem.SDTChunk[] Chunks);

    public sealed partial class SdtVirtualFileSystem(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".sdt", StringComparison.OrdinalIgnoreCase))
                {
                    return static (fullPath, parentRelativePath, parent, parentPath) =>
                        new SdtVirtualFileSystem(parentRelativePath, parent);
                }

                return null;
            });
        }

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var source = parent.File.OpenRead(parentRelativePath);

            var result = new List<(int Index, SDTStream Stream)>();
            var chunks = new Dictionary<uint, List<SDTChunk>>();
            var fileSize = source.Length;

            var index = 0;
            while (source.Position < fileSize)
            {
                var start = source.Position;
                var stream = source.ReadLittleEndian<SDTStream>();

                switch (stream.ResourceId)
                {
                    case 0x10:
                        // Indicate New Resource
                        result.Add(new(index++, stream));
                        chunks[stream.StreamId] = [];
                        break;
                    case 0xF0:
                        // NO-OP Chunk
                        continue;
                    default:
                        var readSize = stream.Size - 0x10;
                        var dataSize = readSize;

                        if (stream.ResourceId == 0x00040001)
                        {
                            if (stream.StreamId != 0)
                            {
                                dataSize = stream.StreamId;
                            }
                        }

                        chunks[stream.ResourceId].Add(new(start + 0x10, dataSize));
                        break;
                }

                source.Seek(start + stream.Size, SeekOrigin.Begin);
            }

            return result.Select(r => (r.Index, r.Stream, chunks[r.Stream.StreamId].ToArray()));
        }

        protected override string GetEntryName(Entry entry) =>
            entry.Stream.StreamId.ToString("x8", CultureInfo.InvariantCulture) + (SDTExtensionMap.TryGetValue(entry.Stream.StreamId, out var extension) ? extension : ".bin");

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
        {
            if (entry.Stream.StreamId == 0x00040001)
            {
                FileBase.EnsureReadOnly(parentOptions, "Header reformatting for .xwma files is not supported.");
            }

            // Demux
            var baseStream = parent.File.Open(parentRelativePath, parentOptions);
            Stream stream = new ConcatStream(
                Ownership.Dispose,
                [.. entry.Chunks.Select(c => new OffsetStreamSpan(baseStream, c.Position, c.Size, Ownership.Dispose))]);

            if (entry.Stream.StreamId == 0x00040001)
            {
                stream = PrependXWMAHeader(entry.Stream, stream);
            }

            return stream;
        }

        #region GamingWithPortals "MGS2AudioTool"
        public static Dictionary<uint, string> SDTExtensionMap = new Dictionary<uint, string>()
        {
            { 0x00000001, ".genh" },
            { 0x00000002, ".dmx" },
            { 0x00000003, ".nrm" },
            { 0x00000004, ".pacb" },
            { 0x00000005, ".dmx" },
            { 0x00000006, ".bpx" },
            { 0x0000000c, ".pac" },
            { 0x0000000d, ".pac" },
            { 0x0000000e, ".pss" },
            { 0x0000000f, ".ipu" },
            { 0x00000020, ".m2v" },
            { 0x00010001, ".sdx_1" },
            { 0x00010004, ".sub_en" },
            { 0x00020001, ".sdx_2" },
            { 0x00020004, ".sub_fr" },
            { 0x00030001, ".msf" },
            { 0x00030004, ".sub_de" },
            { 0x00040001, ".xwma" },
            { 0x00040004, ".sub_it" },
            { 0x00050001, ".9tav" },
            { 0x00050004, ".sub_es" },
            { 0x00060004, ".sub_jp" },
            { 0x00070004, ".sub_jp" },
            { 0x00100001, ".vag" },
            { 0x00110001, ".mtaf" },
        };

        public record struct SDTChunk(long Position, long Size);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SDTStream
        {
            public uint ResourceId;
            public uint Size;
            public uint U_8;
            public uint StreamId;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Header
        {
            public uint Magic;
            public int Codec;
            public int Channels;
            public int SampleRate;
            public uint DataSize;
            public int AvgBps;
            public int BlockSize;
        }

        public static Stream PrependXWMAHeader(SDTStream headerChunk, Stream stream)
        {
            var inputHeader = stream.ReadLittleEndian<Header>();
            stream.Seek(0x32, SeekOrigin.Begin);
            int seekEntryCount = stream.ReadInt16LittleEndian();
            var pos = StreamExtensions.Align(stream.Position + 4 * seekEntryCount, 0x10);
            var audioData = new OffsetStreamSpan(stream, pos, stream.Length - pos, Ownership.Dispose);

            const int headerSize = 0x2E;
            var headerStream = new MemoryStream();
            using var writer = new BinaryWriter(headerStream, Encoding.UTF8, leaveOpen: true);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(headerSize + inputHeader.DataSize - 8);
            writer.Write(Encoding.ASCII.GetBytes("XWMAfmt "));
            writer.Write(0x12U);
            writer.Write((short)inputHeader.Codec);
            writer.Write((short)inputHeader.Channels);
            writer.Write((uint)inputHeader.SampleRate);
            writer.Write((uint)inputHeader.AvgBps);
            writer.Write((short)inputHeader.BlockSize);
            writer.Write((short)16);
            writer.Write((short)0);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(inputHeader.DataSize);

            return new ConcatStream(Ownership.Dispose, MappedStream.FromStream(headerStream, Ownership.Dispose), audioData);
        }
        #endregion
    }
}
