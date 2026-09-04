// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.
namespace Codec.Streams
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class SpoolingStream : Stream
    {
        public const long DefaultThreshold = 16 * 1024 * 1024;
        private readonly long spillThreshold;
        private readonly string? tempDirectory;
        private readonly int fileBufferSize;

        private Stream inner;
        private bool disposed;

        public SpoolingStream(long? expectedSize = null, long thresholdBytes = DefaultThreshold, string? tempDirectory = null, int fileBufferSize = 1 << 16)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(thresholdBytes);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fileBufferSize);

            this.spillThreshold = thresholdBytes;
            this.tempDirectory = tempDirectory;
            this.fileBufferSize = fileBufferSize;

            if (expectedSize > thresholdBytes)
            {
                this.inner = this.CreateFileStream();
            }
            else
            {
                var initialCapacity = expectedSize >= 0 ? (int)expectedSize : 0;
                this.inner = new MemoryStream(initialCapacity);
            }
        }

        public bool IsSpooledToDisk => this.inner is FileStream;

        public string? TempFilePath { get; private set; }

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => true;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override long Length => this.inner.Length;

        /// <inheritdoc/>
        public override long Position
        {
            get => this.inner.Position;
            set => this.inner.Position = value;
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => this.inner.Read(buffer, offset, count);

        /// <inheritdoc/>
        public override int Read(Span<byte> buffer) => this.inner.Read(buffer);

        /// <inheritdoc/>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            this.inner.ReadAsync(buffer, offset, count, cancellationToken);

        /// <inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            this.inner.ReadAsync(buffer, cancellationToken);

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => this.inner.Seek(offset, origin);

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            this.EnsureCapacity(value);
            this.inner.SetLength(value);
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            this.EnsureCapacity(this.inner.Position + count);
            this.inner.Write(buffer, offset, count);
        }

        /// <inheritdoc/>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            this.EnsureCapacity(this.inner.Position + buffer.Length);
            this.inner.Write(buffer);
        }

        /// <inheritdoc/>
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            this.EnsureCapacity(this.inner.Position + count);
            await this.inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            this.EnsureCapacity(this.inner.Position + buffer.Length);
            await this.inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override void Flush() => this.inner.Flush();

        /// <inheritdoc/>
        public override Task FlushAsync(CancellationToken cancellationToken) => this.inner.FlushAsync(cancellationToken);

        /// <inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            if (!this.disposed)
            {
                await this.inner.DisposeAsync().ConfigureAwait(false);
                this.disposed = true;
            }

            await base.DisposeAsync().ConfigureAwait(false);

            GC.SuppressFinalize(this);
        }

        public void SpoolToDisk()
        {
            if (this.IsSpooledToDisk)
            {
                return;
            }

            var memory = (MemoryStream)this.inner;
            var file = this.CreateFileStream();

            try
            {
                var position = memory.Position;
                memory.Position = 0;
                memory.CopyTo(file);
                file.Position = position;
            }
            catch
            {
                file.Dispose();
                this.TempFilePath = null;
                throw;
            }

            memory.Dispose();
            this.inner = file;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.inner.Dispose();
                }

                this.disposed = true;
            }

            base.Dispose(disposing);
        }

        private void EnsureCapacity(long requiredLength)
        {
            if (requiredLength > this.spillThreshold)
            {
                this.SpoolToDisk();
            }
        }

        private FileStream CreateFileStream()
        {
            this.TempFilePath = Path.Combine(this.tempDirectory ?? Path.GetTempPath(), Path.GetRandomFileName());
            return new FileStream(
                this.TempFilePath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                this.fileBufferSize,
                FileOptions.DeleteOnClose | FileOptions.SequentialScan);
        }
    }
}
