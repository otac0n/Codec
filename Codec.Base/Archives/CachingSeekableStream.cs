namespace Codec.Archives
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using DiscUtils.Streams;

    public sealed class CachingSeekableStream(Stream inner, long? knownLength = null) : SparseStream
    {
        private const int CopyBufferSize = 81920;

        private readonly Stream inner = inner ?? throw new ArgumentNullException(nameof(inner));
        private readonly MemoryStream cache = new();
        private byte[]? copyBuffer;
        private long position;
        private bool innerExhausted;
        private bool disposed;

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => true;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length
        {
            get
            {
                ObjectDisposedException.ThrowIf(this.disposed, this);
                if (knownLength is long value)
                {
                    return value;
                }

                this.EnsureFullyCached();
                return this.cache.Length;
            }
        }

        /// <inheritdoc/>
        public override long Position
        {
            get
            {
                ObjectDisposedException.ThrowIf(this.disposed, this);
                return this.position;
            }
            set => this.Seek(value, SeekOrigin.Begin);
        }

        /// <inheritdoc/>
        public override IEnumerable<StreamExtent> Extents => [new StreamExtent(0, this.Length)];

        /// <inheritdoc/>
        public override void Flush()
        {
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            ArgumentNullException.ThrowIfNull(buffer);

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (count == 0)
            {
                return 0;
            }

            this.EnsureCachedTo(this.position + count);

            var available = this.cache.Length - this.position;
            if (available <= 0)
            {
                return 0;
            }

            var toCopy = (int)Math.Min(count, available);
            this.cache.Position = this.position;
            var read = this.cache.Read(buffer, offset, toCopy);
            this.position += read;
            return read;
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);

            long target;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    target = offset;
                    break;
                case SeekOrigin.Current:
                    target = this.position + offset;
                    break;
                case SeekOrigin.End:
                    this.EnsureFullyCached();
                    target = this.cache.Length + offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin));
            }

            if (target < 0)
            {
                throw new IOException("An attempt was made to move the position before the beginning of the stream.");
            }

            this.EnsureCachedTo(target);
            if (this.innerExhausted && target > this.cache.Length)
            {
                target = this.cache.Length;
            }

            this.position = target;
            return this.position;
        }

        /// <inheritdoc/>
        public override void SetLength(long value) =>
            throw new NotSupportedException($"{nameof(CachingSeekableStream)} is read-only.");

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException($"{nameof(CachingSeekableStream)} is read-only.");

        /// <summary>
        /// Reads from the inner stream, appending to the cache, until the cache holds at least
        /// <paramref name="targetLength"/> bytes or the inner stream is exhausted.
        /// </summary>
        private void EnsureCachedTo(long targetLength)
        {
            if (this.innerExhausted || this.cache.Length >= targetLength)
            {
                return;
            }

            this.copyBuffer ??= new byte[CopyBufferSize];
            this.cache.Position = this.cache.Length; // Make sure we append to the end of the cache.

            while (this.cache.Length < targetLength)
            {
                var read = this.inner.Read(this.copyBuffer, 0, this.copyBuffer.Length);
                if (read == 0)
                {
                    this.innerExhausted = true;
                    this.copyBuffer = null;
                    break;
                }

                this.cache.Write(this.copyBuffer, 0, read);
            }
        }

        private void EnsureFullyCached() => this.EnsureCachedTo(long.MaxValue);

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!this.disposed && disposing)
            {
                this.inner.Dispose();
                this.cache.Dispose();
                this.disposed = true;
                this.copyBuffer = null;
            }

            base.Dispose(disposing);
        }
    }
}
