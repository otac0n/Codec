namespace Codec.MGS.Archives
{
    using System.Diagnostics;
    using System.IO;
    using System.IO.Abstractions;
    using System.IO.Compression;
    using Codec.Archives;
    using Codec.Streams;
    using Microsoft.Extensions.DependencyInjection;

    public class ZarArchive(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<string>
    {
        private readonly string fileName = parent.Path.GetFileName(parentRelativePath) + ".dar";

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("_zar", static (fullPath, parentRelativePath, parent, parentPath) => new ZarArchive(parentRelativePath, parent));
        }

        protected override string GetEntryName(string entry) => entry;

        protected override Stream Open(string entry, FileStreamOptions parentOptions)
        {
            Debug.Assert(entry == this.fileName, "Entry does not match the file name.");
            return CreateStreamWrapper(
                parentOptions,
                options =>
                {
                    Stream source = parent.File.Open(parentRelativePath, options);
                    var knownLength = source.ReadUInt32LittleEndian();
                    source = new ZLibStream(source, CompressionMode.Decompress);
                    source = new CachingSeekableStream(source, knownLength);
                    return source;
                },
                updated =>
                {
                    using var dest = parent.File.OpenWrite(parentRelativePath);
                    dest.WriteLittleEndian((uint)updated.Length);
                    using var compressed = new ZLibStream(dest, CompressionMode.Compress);
                    updated.Position = 0;
                    updated.CopyTo(compressed);
                });
        }

        protected override string[] ReadIndex() => [this.fileName];
    }
}
