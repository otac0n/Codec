namespace Codec.MGS.Archives
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Abstractions;
    using Codec.Archives;
    using Codec.Imaging;
    using Codec.Streams;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (string FolderName, string FileName, long Offset, long Length);

    internal class BrfDatArchive(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("BRF.DAT", static (fullPath, parentRelativePath, parent, parentPath) => new BrfDatArchive(parentRelativePath, parent));
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
            while (true)
            {
                var fileCount = stream.ReadUInt32LittleEndian();
                if (fileCount == 0 || IsPcx(fileCount))
                {
                    break;
                }

                foreach (var entry in IndexFolder(fileCount, stream))
                {
                    result.Add(entry);
                }

                stream.Align(0x800);
            }

            stream.Seek(-4, SeekOrigin.Current);
            foreach (var entry in IndexPcx(stream))
            {
                result.Add(entry);
            }

            stream.Seek(0, SeekOrigin.Begin);
            return result;
        }

        private static bool IsPcx(uint signature) => (signature & 0xFFFFFF) == 0x01050a;

        private static IEnumerable<Entry> IndexPcx(Stream stream)
        {
            var end = false;
            while (!end)
            {
                var pcxId = (uint)stream.Position;
                if (stream.Position < stream.Length && IsPcx(stream.ReadUInt32LittleEndian()))
                {
                    stream.Seek(-4, SeekOrigin.Current);
                    SeekPastPCX(stream);
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

        private static void SeekPastPCX(Stream s)
        {
            var start = s.Position;
            var header = s.ReadLittleEndian<PcxHeader>();

            if (header.Manufacturer != 0x0A)
            {
                throw new InvalidDataException("Not a PCX file.");
            }
            else if (header.Encoding != 1)
            {
                throw new InvalidDataException("Unsupported PCX encoding.");
            }

            var width = header.XMax - header.XMin + 1;
            var height = header.YMax - header.YMin + 1;

            var decodedBytesRequired =
                (long)height * header.NumBitPlanes * header.BytesPerLine;

            var bitmapPos = start + 0x80;
            s.Position = bitmapPos;

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

            if (header.BitsPerPixel == 8 && header.NumBitPlanes == 1)
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

        private static IEnumerable<Entry> IndexFolder(uint fileCount, Stream stream)
        {
            var folderId = (uint)stream.Position;
            for (var i = 0; i < fileCount; i++)
            {
                var fileName = stream.ReadNullString();
                stream.Align(0x004);
                var fileSize = stream.ReadUInt32LittleEndian() + 1;
                yield return new(folderId.ToString("x8", CultureInfo.InvariantCulture), fileName, stream.Position, fileSize);
                stream.Seek(fileSize, SeekOrigin.Current);
            }
        }
    }
}
