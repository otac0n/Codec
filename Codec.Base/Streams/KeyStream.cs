// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Streams
{
    using System;
    using System.IO;

    /// <summary>
    /// Base class for a forward-only, unbounded stream of generated key bytes.
    /// Derived classes fill one block at a time via <see cref="GenerateBlock"/>.
    /// </summary>
    public abstract class KeyStream : Stream
    {
        private readonly byte[] currentBlock;
        private long position;

        protected KeyStream(int blockSize)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blockSize);
            this.currentBlock = new byte[blockSize];
        }

        protected int BlockSize => this.currentBlock.Length;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException("KeyStream has no defined length.");

        public override long Position
        {
            get => this.position;
            set
            {
                if (value != this.position)
                {
                    throw new NotSupportedException("KeyStream is forward-only.");
                }
            }
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
                var ix = (int)(this.position % this.currentBlock.Length);
                if (ix == 0)
                {
                    this.GenerateBlock(this.currentBlock);
                }

                buffer[0] = this.currentBlock[ix];
                this.position++;
                total++;
                buffer = buffer[1..];
            }

            return total;
        }

        /// <summary>
        /// Fills <paramref name="block"/> (length == <see cref="BlockSize"/>) with the next
        /// block of key material, advancing any internal state.
        /// </summary>
        protected abstract void GenerateBlock(byte[] block);

        public override long Seek(long offset, SeekOrigin origin) =>
            this.Position = offset + origin switch
            {
                SeekOrigin.Begin => 0,
                SeekOrigin.Current => this.Position,
                SeekOrigin.End => this.Length,
            };

        public override void SetLength(long value) =>
            throw new NotSupportedException("KeyStream is read-only.");

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException("KeyStream is read-only.");
    }
}
