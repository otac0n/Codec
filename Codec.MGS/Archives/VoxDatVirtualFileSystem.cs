namespace Codec.MGS.Archives
{
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Codec.Archives;
    using Codec.Audio;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (int Bank, VoxDatVirtualFileSystem.SlotCode Slot, (long Offset, long Size)[] Chunks);

    internal class VoxDatVirtualFileSystem(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("VOX.DAT", static (fullPath, parentRelativePath, parent, parentPath) => new VoxDatVirtualFileSystem(parentRelativePath, parent));
        }

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var file = parent.File.OpenRead(parentRelativePath);
            return ReadIndex(file);
        }

        protected override string GetEntryName(Entry entry) =>
            $"{entry.Bank}{GetSuffix(entry.Slot)}";

        private static string GetSuffix(SlotCode slot) => slot switch
        {
            SlotCode.Audio => ".vag",
            SlotCode.Caption => ".cap",
            SlotCode.Demo => ".demo",
            SlotCode.CaptionJP => "_jp.cap",
            _ => $"_{(byte)slot}.bin",
        };

        private static SlotCode MergeHeaderGroups(SlotCode slot) => slot switch
        {
            SlotCode.AudioHeader or SlotCode.CaptionHeader or SlotCode.CaptionJPHeader => slot - 1,
            _ => slot,
        };

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
        {
            if (entry.Slot == SlotCode.Audio)
            {
                var baseStream = parent.File.Open(parentRelativePath, parentOptions);
                var dataEntries = entry.Chunks.Skip(1);
                var headerStream = CreateVAGHeader(baseStream, entry.Chunks);
                return new ConcatStream(
                    Ownership.Dispose,
                    [MappedStream.FromStream(headerStream, Ownership.Dispose), .. dataEntries.Select(c => new OffsetStreamSpan(baseStream, c.Offset, c.Size, Ownership.Dispose))]);
            }
            else
            {
                var baseStream = parent.File.Open(parentRelativePath, parentOptions);
                return new ConcatStream(
                    Ownership.Dispose,
                    [.. entry.Chunks.Select(c => new OffsetStreamSpan(baseStream, c.Offset, c.Size, Ownership.Dispose))]);
            }
        }

        public static Stream CreateVAGHeader(Stream baseStream, (long Offset, long Size)[] chunks)
        {
            baseStream.Position = chunks[0].Offset;
            var header = new byte[chunks[0].Size];
            baseStream.ReadExactly(header, 0, header.Length);
            var samples = header[6];
            var channels = header[8];
            var vag = new VagHeader
            {
                Version = 0,
                DataSize = (uint)chunks.Skip(1).Sum(e => e.Size),
                SamplingFreq = samples switch
                {
                    8 => 22050,
                    12 => 33075,
                    16 => 44100,
                },
            };

            var headerStream = new MemoryStream();

            if (channels > 1)
            {
                // see: https://github.com/vgmstream/vgmstream/blob/master/src/meta/vag.c#L86
                vag.Signature = 0x56414769; // VAGi
                vag.Reserved1 = BinaryPrimitives.ReverseEndianness(0x1000u);
                vag.DataSize /= channels;
                headerStream.SetLength(0x800);
            }

            headerStream.WriteBigEndian(vag);
            return headerStream;
        }

        private static List<Entry> ReadIndex(Stream stream)
        {
            var index = new List<Entry>();
            var chunks = new List<VoxEntry>();

            var bank = 0;
            var position = 0L;
            while (true)
            {
                if (position >= stream.Length)
                {
                    break;
                }

                stream.Position = position;
                var entry = stream.ReadLittleEndian<RawEntry>();
                if (entry.Size == 0 || (position + entry.Size) > stream.Length)
                {
                    // Parse failed, not a valid VOX.DAT
                    return [];
                }

                var fullEntry = new VoxEntry(MergeHeaderGroups(entry.Code), position + 4, entry.Size - 4);
                position += entry.Size;

                switch (entry.Code)
                {
                    case SlotCode.Index:
                        break;

                    case SlotCode.End:
                        foreach (var slot in chunks.GroupBy(c => c.Code, c => (c.Offset, c.Size)))
                        {
                            index.Add(new(bank, slot.Key, [.. slot]));
                        }

                        bank++;
                        position = StreamExtensions.Align(position, 2048);
                        chunks.Clear();
                        break;

                    default:
                        chunks.Add(fullEntry);
                        break;
                }
            }

            return index;
        }

        public record class VoxEntry(SlotCode Code, long Offset, long Size);

        public enum SlotCode : byte
        {
            Unknown = 0,
            Audio,
            AudioHeader,
            Caption,
            CaptionHeader,
            Demo,
            CaptionJP,
            CaptionJPHeader,
            Index = 0x10,
            End = 0xF0,
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct RawEntry
        {
            public SlotCode Code;
            public ushort Size;
        }
    }
}
