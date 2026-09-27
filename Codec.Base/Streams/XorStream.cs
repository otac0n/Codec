// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Streams
{
    using System;
    using System.IO;
    using DiscUtils.Streams;

    /// <summary>
    /// A read-only, forward-only stream that XORs bytes from <paramref name="first"/> with
    /// bytes from <paramref name="second"/> as they are read. Reading stops as soon as
    /// either underlying stream is exhausted.
    /// </summary>
    public sealed class XorStream : Stream
    {
        private readonly Stream first;
        private readonly Stream second;
        private readonly Ownership firstOwnership;
        private readonly Ownership secondOwnership;
        private byte[] secondScratch = Array.Empty<byte>();
        private long position;

        /// <param name="first">The primary data stream. Its length, if any, determines <see cref="Length"/>.</param>
        /// <param name="second">The stream XORed against <paramref name="first"/>, e.g. a <see cref="KeyStream"/>.</param>
        /// <param name="firstOwnership">Whether to dispose <paramref name="first"/> when this stream is disposed.</param>
        /// <param name="secondOwnership">Whether to dispose <paramref name="second"/> when this stream is disposed.</param>
        public XorStream(Stream first, Stream second, Ownership firstOwnership, Ownership secondOwnership)
        {
            this.first = first ?? throw new ArgumentNullException(nameof(first));
            this.second = second ?? throw new ArgumentNullException(nameof(second));
            this.firstOwnership = firstOwnership;
            this.secondOwnership = secondOwnership;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                // Read never produces more bytes than the shorter side has to offer (see
                // Read below), so that's the stream's true length. Either side may also be
                // unbounded (e.g. a KeyStream, which never supports Length) -- fall back to
                // whichever side actually knows its length when only one does.
                var firstHasLength = this.first.CanSeek;
                var secondHasLength = this.second.CanSeek;

                if (firstHasLength && secondHasLength)
                {
                    return Math.Min(this.first.Length, this.second.Length);
                }

                if (firstHasLength)
                {
                    return this.first.Length;
                }

                if (secondHasLength)
                {
                    return this.second.Length;
                }

                throw new NotSupportedException("Neither underlying stream supports Length.");
            }
        }

        public override long Position
        {
            get => this.position;
            set
            {
                if (value != this.position)
                {
                    throw new NotSupportedException("XorStream is forward-only.");
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
            var read = this.first.Read(buffer);
            if (read == 0)
            {
                return 0;
            }

            if (this.secondScratch.Length < read)
            {
                this.secondScratch = new byte[read];
            }

            var secondSpan = this.secondScratch.AsSpan(0, read);
            var secondRead = ReadAll(this.second, secondSpan);

            for (var i = 0; i < secondRead; i++)
            {
                buffer[i] ^= secondSpan[i];
            }

            this.position += secondRead;
            return secondRead;
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

        public override long Seek(long offset, SeekOrigin origin) =>
            this.Position = offset + origin switch
            {
                SeekOrigin.Begin => 0,
                SeekOrigin.Current => this.Position,
                SeekOrigin.End => this.Length,
            };

        public override void SetLength(long value) =>
            throw new NotSupportedException("XorStream is read-only.");

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException("XorStream is read-only.");

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.firstOwnership == Ownership.Dispose)
                {
                    this.first.Dispose();
                }

                if (this.secondOwnership == Ownership.Dispose)
                {
                    this.second.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
