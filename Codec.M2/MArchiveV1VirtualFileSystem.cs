// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec.M2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using Codec.Archives;
    using DiscUtils.Streams;
    using GMWare.M2.Models;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (string Path, long Offset, long Length);

    public sealed class MArchiveV1VirtualFileSystem : IndexedFileSystem<Entry>
    {
        private readonly string binPath;
        private readonly IFileSystem fileSystem;
        private readonly string seed;
        private readonly int keyLength;

        public MArchiveV1VirtualFileSystem(string binPath, string key, IFileSystem? fileSystem = null)
        {
            fileSystem ??= new FileSystem();
            this.binPath = binPath;
            this.fileSystem = fileSystem;
            this.seed = key;
            this.keyLength = 64;
        }

        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".bin", StringComparison.OrdinalIgnoreCase) &&
                    parent.File.Exists(parent.Path.ChangeExtension(parentRelativePath, ".psb.m")))
                {
                    var key = serviceProvider.GetRequiredService<ArchiveOptions>().Key;
                    return (fullPath, parentRelativePath, parent, parentPath) => new MArchiveV1VirtualFileSystem(parentRelativePath, key, parent);
                }

                return null;
            });
        }

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var fs = this.fileSystem.File.OpenRead(this.Path.ChangeExtension(this.binPath, ".psb.m"));
            using var decompStream = MVirtualFileSystem.ReadMArchive(fs, this.seed, this.keyLength, out var length);
            var info = PsbVirtualFileSystem.PsbDecode(decompStream).Root.ToObject<ArchiveV1>().FileInfo;
            return info.Keys.Select(k => (k, info[k][0], info[k][1]));
        }

        protected override string GetEntryName(Entry entry) =>
            entry.Path;

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions) =>
            new OffsetStreamSpan(this.fileSystem.File.Open(this.binPath, parentOptions), entry.Offset, entry.Length, Ownership.Dispose);
    }
}
