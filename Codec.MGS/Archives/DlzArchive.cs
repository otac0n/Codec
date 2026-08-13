namespace Codec.MGS.Archives
{
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Abstractions;
    using System.IO.Compression;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using Codec.Archives;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;

    internal class DlzArchive : IndexedFileSystem<string>
    {
        private readonly string filePath;
        private readonly IFileSystem fileSystem;
        private readonly string fileName;
        private Stream cachedStream;

        public DlzArchive(string filePath, IFileSystem? fileSystem = null)
        {
            fileSystem ??= new FileSystem();
            this.filePath = filePath;
            this.fileSystem = fileSystem;
            this.fileName = fileSystem.Path.GetFileNameWithoutExtension(filePath) + ".dld";
        }

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem(
                 "*.dlz",
                 static (services, fullPath, parentRelativePath, parent, parentPath) =>
                 {
                     using var file = parent.File.OpenRead(parentRelativePath);
                     var signature = file.ReadLittleEndian<Name4>();
                     return Encoding.ASCII.GetString(signature) == "segs";
                 },
                 static (fullPath, parentRelativePath, parent, parentPath) => new DlzArchive(parentRelativePath, parent));
        }

        internal static Stream ReadDlzArchive(Stream stream)
        {
            // Demux the DLZ segments into a single file.
            var streams = new List<SparseStream>();
            while (stream.TryAlign(0x20000) && stream.Position < stream.Length)
            {
                var baseAddress = stream.Position;
                var header = stream.ReadBigEndian<SegHeader>();
                header.CompressedSize = BinaryPrimitives.ReverseEndianness(header.CompressedSize);
                var segments = stream.ReadArrayBigEndian<Segment>(header.ChunkCount);
                streams.AddRange(segments.Select(s =>
                {
                    var segment = new OffsetStreamSpan(stream, baseAddress + s.Offset - 1, s.CompressedSize, Ownership.Dispose);
                    var decompressed = new DeflateStream(segment, CompressionMode.Decompress);
                    var seekable = new CachingSeekableStream(decompressed, s.DecompressedSize);
                    return seekable;
                }));
            }

            return new ConcatStream(Ownership.Dispose, [.. streams]);
        }

        protected override string[] ReadIndex() => [this.fileName];

        protected override string GetEntryName(string entry) => entry;

        protected override Stream Open(string entry, FileStreamOptions parentOptions)
        {
            Debug.Assert(entry == this.fileName, "Entry does not match the file name.");
            FileBase.EnsureReadOnly(parentOptions, "Recompressing .dlz streams is not currently supported.");
            var inner = this.cachedStream ??= ReadDlzArchive(this.fileSystem.File.Open(this.filePath, parentOptions));
            var disposable = new OffsetStreamSpan(inner, 0, inner.Length, Ownership.None);
            return disposable;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.cachedStream?.Dispose();
            }

            base.Dispose(disposing);
        }

        [InlineArray(4)]
        private struct Name4
        {
            public byte Char0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SegHeader
        {
            public Name4 Signature;
            public ushort Flags;
            public ushort ChunkCount;
            public uint DecompressedSize;
            public uint CompressedSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Segment
        {
            public ushort CompressedSize;
            public ushort DecompressedSize;
            public uint Offset;
        }
    }
}
