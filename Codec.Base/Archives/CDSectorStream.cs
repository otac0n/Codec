// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec.Archives
{
    using System;
    using System.IO;

    public class CDSectorStream : Stream
    {
        public static readonly Mode Mode1 = new Mode(
            sectorIn: 12 + 3 + 1,
            chunkSize: 2048,
            sectorOut: 4 + 8 + 172 + 104);

        public static readonly Mode Mode2 = new Mode(
            sectorIn: 12 + 3 + 1,
            chunkSize: 2336,
            sectorOut: 0);

        public static readonly Mode XAForm1 = new Mode(
            sectorIn: 12 + 3 + 1 + 8,
            chunkSize: 2048,
            sectorOut: 4 + 276);

        public static readonly Mode XAForm2 = new Mode(
            sectorIn: 12 + 3 + 1 + 8,
            chunkSize: 2324,
            sectorOut: 4);

        private readonly Stream underlying;
        private readonly Mode mode;
        private long position;

        public CDSectorStream(Stream underlying, Mode mode)
        {
            this.underlying = underlying;
            this.mode = mode;
            this.position = 0;
        }

        public override bool CanRead => this.underlying.CanRead;

        public override bool CanSeek => this.underlying.CanSeek;

        public override bool CanWrite => false; // Can't support checksums yet.

        public override long Length => this.underlying.Length / this.mode.SectorSize * this.mode.ChunkSize;

        public override long Position
        {
            get => this.position;
            set
            {
                var chunk = value / this.mode.ChunkSize;
                var chunkIndex = value - (chunk * this.mode.ChunkSize);

                var target = chunk * this.mode.SectorSize + this.mode.SectorIn + chunkIndex;
                if (target > this.underlying.Length)
                {
                    this.underlying.Position = this.underlying.Length;
                }
                else
                {
                    this.underlying.Position = target;
                }

                this.position = value;
            }
        }

        public override void Flush() => this.underlying.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var chunk = this.position / this.mode.ChunkSize;
            var chunkIndex = this.position - (chunk * this.mode.ChunkSize);
            count = (int)Math.Min(count, this.mode.ChunkSize - chunkIndex);

            var read = this.underlying.Read(buffer, offset, count);
            this.Position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Current:
                    return this.Position = this.position + offset;

                case SeekOrigin.Begin:
                    return this.Position = offset;

                default:
                    throw new NotSupportedException();
            }
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public record class Mode
        {
            public Mode(uint sectorIn, uint chunkSize, uint sectorOut)
            {
                this.SectorIn = sectorIn;
                this.ChunkSize = chunkSize;
                this.SectorOut = sectorOut;
            }

            public uint SectorIn { get; }

            public uint ChunkSize { get; }

            public uint SectorOut { get; }

            public uint SectorSize => this.SectorIn + this.ChunkSize + this.SectorOut;
        }
    }
}
