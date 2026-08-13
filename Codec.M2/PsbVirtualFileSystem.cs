// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec.M2
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Codec.Archives;
    using GMWare.M2.Psb;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;

    public sealed partial class PsbVirtualFileSystem : IndexedFileSystem<string>
    {
        private readonly string filePath;
        private readonly IFileSystem fileSystem;
        private readonly string indexFileName;

        public PsbVirtualFileSystem(string filePath, IFileSystem? fileSystem = null)
        {
            fileSystem ??= new FileSystem();
            this.filePath = filePath;
            this.fileSystem = fileSystem;
            this.indexFileName = fileSystem.Path.ChangeExtension(fileSystem.Path.GetFileName(filePath), ".json")!;
        }

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("*.psb", static (fullPath, parentRelativePath, parent, parentPath) => new PsbVirtualFileSystem(parentRelativePath, parent));
        }

        internal static PsbReader PsbDecode(Stream decompStream)
        {
            var memoryStream = new MemoryStream();
            decompStream.CopyTo(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            return new PsbReader(memoryStream, filter: null);
        }

        protected override IEnumerable<string> ReadIndex()
        {
            using var fs = this.fileSystem.File.OpenRead(this.filePath);
            var psbReader = PsbDecode(fs);
            var root = psbReader.Root;
            psbReader.LoadAllStreamData();
            return [this.indexFileName, .. psbReader.StreamCache.Keys.Select(k => $"_stream.{k}")];
        }

        protected override string GetEntryName(string entry) =>
            entry;

        protected override Stream Open(string path, FileStreamOptions parentOptions)
        {
            FileBase.EnsureReadOnly(parentOptions, "Writing to sub streams .psb files is not supported.");

            using var stream = this.fileSystem.File.OpenRead(this.filePath);
            var psb = PsbDecode(stream);
            var root = psb.Root;
            MemoryStream memoryStream;
            if (path == this.indexFileName)
            {
                memoryStream = new MemoryStream();
                using var sw = new StreamWriter(memoryStream, leaveOpen: true) { AutoFlush = true };
                using var jw = new JsonTextWriter(sw) { CloseOutput = false, Formatting = Formatting.Indented };
                root.WriteTo(jw);
            }
            else
            {
                var id = uint.Parse(StreamIndexExtractor().Match(path).Groups[1].Value, CultureInfo.InvariantCulture);
                memoryStream = new MemoryStream(psb.StreamCache[id].BinaryData);
            }

            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }

        [GeneratedRegex("^_stream\\.(\\d+)$")]
        private static partial Regex StreamIndexExtractor();
    }
}
