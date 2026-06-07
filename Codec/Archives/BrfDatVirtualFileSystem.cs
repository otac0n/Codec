namespace Codec.Archives
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Abstractions;
    using System.Text;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (string FolderName, string FileName, long Offset, long Length);

    internal class BrfDatVirtualFileSystem(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileSystemResolver>((servicProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetFileName(parentRelativePath), "BRF.DAT", StringComparison.OrdinalIgnoreCase))
                {
                    return static (fullPath, parentRelativePath, parent, parentPath) =>
                        new BrfDatVirtualFileSystem(parentRelativePath, parent);
                }

                return null;
            });
        }

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var file = parent.File.OpenRead(parentRelativePath);
            return ReadIndex(file);
        }

        protected override string GetEntryName(Entry entry) =>
            this.Path.Combine(entry.FolderName, entry.FileName);

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions) =>
            new OffsetStreamSpan(parent.File.Open(parentRelativePath, parentOptions), entry.Offset, entry.Length, Ownership.Dispose);

        private static List<Entry> ReadIndex(Stream stream)
        {
            var result = new List<Entry>();
            using var reader = new BinaryReader(stream);
            while (true)
            {
                var fileCount = reader.ReadUInt32();
                if (fileCount == 0 || IsPcx(fileCount))
                {
                    break;
                }

                foreach (var entry in IndexFolder(fileCount, reader))
                {
                    result.Add(entry);
                }

                stream.Align(0x800);
            }

            stream.Seek(-4, SeekOrigin.Current);
            foreach (var entry in IndexPcx(reader))
            {
                result.Add(entry);
            }

            stream.Seek(0, SeekOrigin.Begin);
            return result;
        }

        private static bool IsPcx(uint signature) => (signature & 0xFFFFFF) == 0x01050a;

        private static IEnumerable<Entry> IndexPcx(BinaryReader reader)
        {
            var stream = reader.BaseStream;
            var end = false;
            while (!end)
            {
                var pcxId = (uint)stream.Position;
                if (stream.Position < stream.Length && IsPcx(reader.ReadUInt32()))
                {
                    stream.Seek(-4, SeekOrigin.Current);
                    SeekPastPCX(reader);
                    yield return new("pcx", pcxId.ToString("x8", CultureInfo.InvariantCulture) + ".pcx", pcxId, stream.Position - pcxId);
                    if (!stream.TryAlign(0x800))
                    {
                        end = true;
                    }
                }
                else
                {
                    end = true;
                }
            }
        }

        private static void SeekPastPCX(BinaryReader br)
        {
            var s = br.BaseStream;

            var start = s.Position;

            var manufacturer = br.ReadByte(); // must be 0x0A
            var version = br.ReadByte();
            var encoding = br.ReadByte(); // must be 1 (RLE)
            var bitsPerPixel = br.ReadByte();

            if (manufacturer != 0x0A)
            {
                throw new InvalidDataException("Not a PCX file.");
            }

            if (encoding != 1)
            {
                throw new InvalidDataException("Unsupported PCX encoding.");
            }

            var xmin = br.ReadUInt16();
            var ymin = br.ReadUInt16();
            var xmax = br.ReadUInt16();
            var ymax = br.ReadUInt16();
            var width = xmax - xmin + 1;
            var height = ymax - ymin + 1;

            s.Seek(start + 0x041, SeekOrigin.Begin);
            var colorPlanes = br.ReadByte();

            s.Seek(start + 0x042, SeekOrigin.Begin);
            var bytesPerLine = br.ReadUInt16();

            var decodedBytesRequired =
                (long)height * colorPlanes * bytesPerLine;

            var bitmapPos = start + 0x80;
            s.Seek(bitmapPos, SeekOrigin.Begin);

            long decoded = 0;
            while (decoded < decodedBytesRequired)
            {
                var b = s.ReadByte();
                if (b < 0)
                {
                    throw new EndOfStreamException();
                }

                if ((b & 0xC0) == 0xC0)
                {
                    var runLength = b & 0x3F;
                    if (s.ReadByte() < 0)
                    {
                        throw new EndOfStreamException();
                    }

                    decoded += runLength;
                }
                else
                {
                    decoded += 1;
                }
            }

            if (bitsPerPixel == 8 && colorPlanes == 1)
            {
                var marker = s.ReadByte();

                if (marker == 0x0C)
                {
                    s.Seek(0x300, SeekOrigin.Current);
                }
                else
                {
                    s.Seek(-1, SeekOrigin.Current);
                }
            }
        }

        private static IEnumerable<Entry> IndexFolder(uint fileCount, BinaryReader reader)
        {
            var stream = reader.BaseStream;
            var folderId = (uint)stream.Position;
            for (var i = 0; i < fileCount; i++)
            {
                var fileName = ReadString(stream);
                stream.Align(0x004);
                var fileSize = reader.ReadUInt32();
                yield return new(folderId.ToString("x8", CultureInfo.InvariantCulture), fileName, stream.Position, fileSize);
                stream.Seek(fileSize + 1, SeekOrigin.Current);
            }
        }

        private static string ReadString(Stream stream)
        {
            var value = new StringBuilder();
            int c;
            while ((c = stream.ReadByte()) != -1)
            {
                if (c == 0)
                {
                    break;
                }

                value.Append((char)c);
            }

            return value.ToString();
        }
    }
}
