namespace Codec.Archives
{
    using System;
    using System.IO;

    public sealed class DisposingStream(Stream inner, IDisposable disposable) : Stream
    {
        private bool disposed;

        /// <inheritdoc/>
        public override bool CanRead => inner.CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => inner.CanSeek;

        /// <inheritdoc/>
        public override bool CanWrite => inner.CanWrite;

        /// <inheritdoc/>
        public override long Length => inner.Length;

        /// <inheritdoc/>
        public override long Position { get => inner.Position; set => inner.Position = value; }

        /// <inheritdoc/>
        public override void Flush()
        {
            inner.Flush();
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            return inner.Read(buffer, offset, count);
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            return inner.Seek(offset, origin);
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            inner.SetLength(value);
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            inner.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                inner.Dispose();
                disposable?.Dispose();
            }

            this.disposed = true;

            base.Dispose(disposing);
        }
    }
}
