// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Streams
{
    using System;
    using System.Buffers.Binary;
    using System.IO;
    using DiscUtils.Streams;

    public sealed class DecodingStream : Stream
    {
        private static readonly uint Key = 0x02E90EDD;

        private readonly Stream source;
        private readonly Ownership ownership;
        private readonly byte[] currentWord = new byte[4];
        private long position;
        private int? validBytes;

        public uint KeyAccumulator { get; private set; }

        public uint Salt { get; }

        public DecodingStream(uint iv, uint salt, Stream source, Ownership ownership = Ownership.None)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.ownership = ownership;
            this.KeyAccumulator = iv;
            this.Salt = salt;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => this.source.Length;

        public override long Position
        {
            get => this.position;
            set => throw new NotSupportedException("DecodingStream is forward-only.");
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            return this.Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            var total = 0;

            while (buffer.Length > 0)
            {
                if (this.validBytes is null)
                {
                    this.ReadNextWord();
                }

                var ix = (int)(this.position % 4);
                if (ix >= this.validBytes)
                {
                    break;
                }

                buffer[0] = this.currentWord[ix];
                this.position++;
                total++;
                buffer = buffer[1..];
                if (this.position % 4 == 0)
                {
                    this.validBytes = null;
                }
            }

            return total;
        }

        private static int ReadAll(Stream source, Span<byte> buffer)
        {
            var total = 0;
            while (buffer.Length > 0)
            {
                var read = source.Read(buffer);
                if (read == 0)
                {
                    break;
                }

                buffer = buffer[read..];
                total += read;
            }

            return total;
        }

        private void ReadNextWord()
        {
            this.validBytes = ReadAll(this.source, this.currentWord);
            var value = BinaryPrimitives.ReadUInt32LittleEndian(this.currentWord);
            value ^= this.KeyAccumulator;
            BinaryPrimitives.WriteUInt32LittleEndian(this.currentWord, value);
            this.KeyAccumulator = unchecked((this.KeyAccumulator * Key) + this.Salt);
        }

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException("DecodingStream is forward-only.");

        public override void SetLength(long value)
            => throw new NotSupportedException("DecodingStream is read-only.");

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException("DecodingStream is read-only.");

        protected override void Dispose(bool disposing)
        {
            if (disposing && this.ownership == Ownership.Dispose)
            {
                this.source.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
