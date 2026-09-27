// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Streams
{
    using System;

    /// <summary>
    /// A key stream that repeats a fixed byte pattern indefinitely.
    /// </summary>
    public sealed class RepeatingKeyStream : KeyStream
    {
        private readonly byte[] pattern;

        public RepeatingKeyStream(ReadOnlySpan<byte> pattern)
            : base(pattern.Length)
        {
            this.pattern = pattern.ToArray();
        }

        protected override void GenerateBlock(byte[] block) =>
            this.pattern.CopyTo(block, 0);
    }
}
