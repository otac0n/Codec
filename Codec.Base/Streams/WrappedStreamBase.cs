namespace Codec.Streams
{
    using System;
    using System.IO;

    public abstract class WrappedStreamBase : Stream
    {
        protected readonly Stream inner;

        public WrappedStreamBase(Stream inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <inheritdoc/>
        public override bool CanRead => this.inner.CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => this.inner.CanSeek;

        /// <inheritdoc/>
        public override bool CanWrite => this.inner.CanWrite;

        /// <inheritdoc/>
        public override long Length => this.inner.Length;

        /// <inheritdoc/>
        public override long Position
        {
            get => this.inner.Position;
            set => this.inner.Position = value;
        }

        /// <inheritdoc/>
        public override void Flush() => this.inner.Flush();

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => this.inner.Read(buffer, offset, count);

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => this.inner.Seek(offset, origin);

        /// <inheritdoc/>
        public override void SetLength(long value) => this.inner.SetLength(value);

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => this.inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            this.inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
