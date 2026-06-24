// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec.M2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using Codec.Archives;
    using GMWare.M2.MArchive;
    using Microsoft.Extensions.DependencyInjection;

    public sealed class MVirtualFileSystem : IndexedFileSystem<string>
    {
        private static readonly Dictionary<uint, IMArchiveCodec> CodecLookup = new IMArchiveCodec[] { new ZStandardCodec(), new ZlibCodec(), new FastLzCodec(), }.ToDictionary(c => c.Magic);
        private readonly string filePath;
        private readonly string seed;
        private readonly int keyLength;
        private readonly IFileSystem fileSystem;
        private readonly string fileName;

        public MVirtualFileSystem(string filePath, string seed, int keyLength, IFileSystem? fileSystem = null)
        {
            fileSystem ??= new FileSystem();
            this.filePath = filePath;
            this.seed = seed;
            this.keyLength = keyLength;
            this.fileSystem = fileSystem;
            this.fileName = fileSystem.Path.GetFileNameWithoutExtension(filePath);
        }

        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".m", StringComparison.OrdinalIgnoreCase))
                {
                    var seed = serviceProvider.GetRequiredService<ArchiveOptions>().Key;
                    return (fullPath, parentRelativePath, parent, parentPath) => new MVirtualFileSystem(parentRelativePath, seed, 64, parent);
                }

                return null;
            });
        }

        internal static Stream? ReadMArchive(FileSystemStream fs, string seed, int keyLength, out int decompressedLength)
        {
            var magic = fs.ReadUInt32LittleEndian();
            if (!CodecLookup.TryGetValue(magic, out var codec))
            {
                decompressedLength = 0;
                return null;
            }

            decompressedLength = fs.ReadInt32LittleEndian();
            var cs = new MArchiveCryptoStream(fs, fs.Name, seed, keyLength);
            return codec.GetDecompressionStream(cs, decompressedLength);
        }

        protected override string[] ReadIndex() => [this.fileName];

        protected override string GetEntryName(string entry) => entry;

        protected override Stream Open(string entry, FileStreamOptions parentOptions)
        {
            Debug.Assert(entry == this.fileName, "Entry does not match the file name.");
            FileBase.EnsureReadOnly(parentOptions, "Recompressing .m streams is not supported.");
            return ReadMArchive(this.fileSystem.File.Open(this.filePath, parentOptions), this.seed, this.keyLength, out _) ?? throw new InvalidDataException("Not a valid M archive.");
        }
    }
}
