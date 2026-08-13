namespace Codec.MGS.Archives
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Codec.Archives;
    using Microsoft.Extensions.DependencyInjection;
    using NAudio.Utils;
    using NAudio.Wave;
    using Entry = (string FileName, SdxVirtualFileSystem.NoteParameters Data, uint SpuID);

    public sealed partial class SdxVirtualFileSystem : IndexedFileSystem<Entry>
    {
        private readonly string parentRelativePath;
        private readonly IFileSystem parent;

        List<SpuData> soundDatas = new List<SpuData>();

        public SdxVirtualFileSystem(string parentRelativePath, IFileSystem parent)
        {
            this.parentRelativePath = parentRelativePath;
            this.parent = parent;
        }

        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".sdx", StringComparison.OrdinalIgnoreCase))
                {
                    return static (fullPath, parentRelativePath, parent, parentPath) =>
                        new SdxVirtualFileSystem(parentRelativePath, parent);
                }

                return null;
            });
        }

        protected override IEnumerable<Entry> ReadIndex()
        {
            var result = new List<Entry>();
            using var stream = this.parent.File.OpenRead(this.parentRelativePath);

            stream.Seek(0, SeekOrigin.Begin);

            var firstTableOffset = stream.ReadUInt32LittleEndian() * 0x800;
            stream.ReadUInt32LittleEndian(); // se1: code
            var secondTableOffset = stream.ReadUInt32LittleEndian() * 0x800;
            stream.ReadUInt32LittleEndian(); // se2: code

            stream.Seek(firstTableOffset, SeekOrigin.Begin);

            var bound = stream.ReadUInt32BigEndian();
            var size = stream.ReadUInt32BigEndian();
            stream.Position += 8;

            // SE 1
            for (uint i = 0; i < (size / 0x10); i++)
            {
                result.Add(("0" + this.Path.DirectorySeparatorChar + i.ToString() + ".wav", stream.ReadLittleEndian<NoteParameters>(), 0));
            }

            soundDatas.Add(new SpuData(stream));

            stream.Seek(secondTableOffset, SeekOrigin.Begin);

            bound = stream.ReadUInt32BigEndian();
            size = stream.ReadUInt32BigEndian();
            stream.Position += 8;

            if (size % 10 != 0 && size < 1000000)
            {
                for (uint i = 0; i < (size / 0x10); i++)
                {
                    result.Add(("1" + this.Path.DirectorySeparatorChar + i.ToString() + ".wav", stream.ReadLittleEndian<NoteParameters>(), 1));
                }

                soundDatas.Add(new SpuData(stream));
            }

            return result;
        }

        protected override string GetEntryName(Entry entry) =>
            entry.FileName;

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
        {
            FileBase.EnsureReadOnly(parentOptions, "Writing to sub patches in .sdx files is not supported.");

            using var stream = this.parent.File.OpenRead(this.parentRelativePath);
            stream.Seek(0, 0);

            var spu = soundDatas[(int)entry.SpuID];

            var adpcm = spu.GetAudioData(stream, entry.Data.addrLe);

            var pcmSamples = DecodeSpuAdpcm(adpcm);

            var ms = new MemoryStream();
            var format = new WaveFormat(22050, 16, 1); // 16-bit mono
            using (var writer = new WaveFileWriter(new IgnoreDisposeStream(ms), format))
            {
                writer.WriteSamples(pcmSamples, 0, pcmSamples.Length);
                writer.Flush();
            }

            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        #region from MGS2 SDX Tool by Gaming With Portals: originally from someone else but i forgot
        static readonly double[] PosTable = [0.0, 60.0 / 64, 115.0 / 64, 98.0 / 64, 122.0 / 64];
        static readonly double[] NegTable = [0.0, 0.0, -52.0 / 64, -55.0 / 64, -60.0 / 64];

        public static short[] DecodeSpuAdpcm(byte[] data)
        {
            var samples = new List<short>();
            double hist1 = 0.0, hist2 = 0.0;

            var numBlocks = data.Length / 16;

            for (var blockIdx = 0; blockIdx < numBlocks; blockIdx++)
            {
                var blockOffset = blockIdx * 16;
                var shiftFilter = data[blockOffset];
                var flags = data[blockOffset + 1];

                var shift = shiftFilter & 0x0F;
                var filterIdx = (shiftFilter >> 4) & 0x0F;
                if (filterIdx > 4)
                {
                    filterIdx = 0;
                }

                var pos = PosTable[filterIdx];
                var neg = NegTable[filterIdx];

                for (var i = blockOffset + 2; i < blockOffset + 16; i++)
                {
                    var b = data[i];
                    foreach (var nibbleShift in new[] { 0, 4 })
                    {
                        var nibble = (b >> nibbleShift) & 0x0F;
                        if (nibble >= 8)
                        {
                            nibble -= 16;
                        }

                        double raw = nibble * (1 << (12 - shift));
                        var sample = raw + pos * hist1 + neg * hist2;
                        hist2 = hist1;
                        hist1 = sample;

                        samples.Add((short)Math.Clamp((int)sample, -32768, 32767));
                    }
                }

                if ((flags & 0x01) != 0)
                {
                    break;
                }
            }

            return samples.ToArray();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct NoteParameters
        {
            public uint addrLe;
            public byte sampleNote;
            public byte sampleTune;
            public byte attackMode;
            public byte attackRate;
            public byte decayRate;
            public byte sustainMode;
            public byte sustainRate;
            public byte sustainLevel;
            public byte releaseMode;
            public byte releaseRate;
            public byte pan;
            public byte decVolume;
        }

        public struct SpuData
        {
            public uint spuOffset;
            public uint spuSize;
            public uint dataStart;

            public SpuData(Stream stream)
            {
                spuOffset = stream.ReadUInt32BigEndian();
                spuSize = stream.ReadUInt32BigEndian();
                stream.Position += 8;
                dataStart = (uint)stream.Position;
            }

            public byte[] GetAudioData(Stream stream, uint offset)
            {
                stream.Seek(dataStart + (offset - spuOffset) + 0x10, SeekOrigin.Begin);
                MemoryStream memstream = new MemoryStream();

                var frame = new byte[0x10];
                while (true)
                {
                    var read = stream.Read(frame, 0, 0x10);
                    if (read < 0x10 || frame.All(b => b == 0))
                    {
                        break;
                    }

                    memstream.Write(frame, 0, read);
                }

                return memstream.ToArray();
            }
        }
        #endregion
    }
}
