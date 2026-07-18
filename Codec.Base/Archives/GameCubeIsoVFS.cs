namespace Codec.Archives
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;

    public class GameCubeIsoVFS(string parentRelativePath, IFileSystem parent)
        : IndexedFileSystem<GameCubeIsoVFS.FstFile>
    {
        private const uint GameCubeMagic = 0xC2339F3D;

        private const int FstEntrySize = 12;

        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                var extension = parent.Path.GetExtension(parentRelativePath);
                if (!string.Equals(extension, ".iso", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".gcm", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                using (var file = parent.File.OpenRead(parentRelativePath))
                {
                    if (file.Length < Marshal.SizeOf<GcDiscHeader>())
                    {
                        return null;
                    }

                    var header = file.ReadBigEndian<GcDiscHeader>();
                    if (header.GcMagic != GameCubeMagic)
                    {
                        return null;
                    }
                }

                return static (fullPath, parentRelativePath, parent, parentPath) =>
                    new GameCubeIsoVFS(parentRelativePath, parent);
            });
        }

        protected override string GetEntryName(FstFile entry) =>
            entry.Path;

        protected override IEnumerable<FstFile> ReadIndex()
        {
            using var file = parent.File.OpenRead(parentRelativePath);
            var header = file.ReadBigEndian<GcDiscHeader>();

            file.Position = header.FstOffset;
            var fst = new byte[header.FstSize];
            file.ReadExactly(fst, fst.Length);

            return ParseFst(fst);
        }

        protected override Stream Open(FstFile entry, FileStreamOptions parentOptions)
        {
            FileBase.EnsureReadOnly(parentOptions, "Rebuilding GameCube ISOs is not supported.");
            var file = parent.File.OpenRead(parentRelativePath);
            return new OffsetStreamSpan(file, entry.Offset, entry.Length, Ownership.Dispose);
        }

        private static IEnumerable<FstFile> ParseFst(byte[] fst)
        {
            var numEntries = checked((int)BinaryPrimitives.ReadUInt32BigEndian(fst.AsSpan(8, 4)));

            using var fstStream = new MemoryStream(fst);
            var entries = fstStream.ReadArrayBigEndian<FstEntryRaw>(numEntries);
            var stringTable = fst.AsSpan(numEntries * FstEntrySize);

            var results = new List<FstFile>();

            var dirStack = new Stack<(int EndIndex, string Path)>();
            dirStack.Push((numEntries, string.Empty));

            for (var i = 1; i < numEntries; i++)
            {
                while (dirStack.Peek().EndIndex == i)
                {
                    dirStack.Pop();
                }

                var entry = entries[i];
                var nameBytes = (Span<byte>)entry.NameOffset;
                var nameOffset = (nameBytes[0] << 16) | (nameBytes[1] << 8) | nameBytes[2];

                var name = ReadNullTerminatedString(stringTable, nameOffset);
                var parentPath = dirStack.Peek().Path;
                var path = parentPath.Length == 0 ? name : $"{parentPath}/{name}";

                if (entry.Type != 0)
                {
                    dirStack.Push((checked((int)entry.FieldB), path));
                }
                else
                {
                    results.Add(new FstFile(path, entry.FieldA, entry.FieldB));
                }
            }

            return results;
        }

        private static string ReadNullTerminatedString(Span<byte> buffer, int offset)
        {
            var end = offset;
            while (end < buffer.Length && buffer[end] != 0)
            {
                end++;
            }

            return Encoding.ASCII.GetString(buffer[offset..end]);
        }

        public readonly record struct FstFile(string Path, long Offset, long Length);

        [InlineArray(3)]
        internal struct UInt24BE
        {
            public byte Byte0;
        }

        [InlineArray(6)]
        internal struct GameCode
        {
            public byte Char0;
        }

        [InlineArray(14)]
        internal struct HeaderPadding1
        {
            public byte Byte0;
        }

        [InlineArray(0x3E0)]
        internal struct GameTitle
        {
            public byte Char0;
        }

        [InlineArray(0x18)]
        internal struct HeaderPadding2
        {
            public byte Byte0;
        }

        [InlineArray(0x10)]
        internal struct HeaderPadding3
        {
            public byte Byte0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct GcDiscHeader
        {
            public GameCode GameCode;
            public byte DiscId;
            public byte Version;
            public byte AudioStreaming;
            public byte StreamBufferSize;
            public HeaderPadding1 Unused1;
            public uint WiiMagic;
            public uint GcMagic;
            public GameTitle GameTitle;
            public uint DebugMonitorOffset;
            public uint DebugMonitorLoadAddress;
            public HeaderPadding2 Unused2;
            public uint DolOffset;
            public uint FstOffset;
            public uint FstSize;
            public uint FstMaxSize;
            public HeaderPadding3 Unused3;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct FstEntryRaw
        {
            public byte Type;
            public UInt24BE NameOffset;
            public uint FieldA;
            public uint FieldB;
        }
    }
}
