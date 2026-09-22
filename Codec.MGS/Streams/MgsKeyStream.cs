// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Streams
{
    using System.Buffers.Binary;
    using Codec.Streams;

    /// <summary>
    /// Generates the 32-bit-accumulator key stream used by the MGS codec's
    /// <c>DecodingStream</c>: each 4-byte block is the current accumulator value
    /// (little-endian), after which the accumulator advances by
    /// <c>accumulator = accumulator * Multiplier + Salt</c>.
    /// </summary>
    public sealed class MgsKeyStream : KeyStream
    {
        private static readonly uint Multiplier = 0x02E90EDD;

        public MgsKeyStream(uint iv, uint salt)
            : base(sizeof(uint))
        {
            this.KeyAccumulator = iv;
            this.Salt = salt;
        }

        public uint KeyAccumulator { get; private set; }

        public uint Salt { get; }

        public static uint MakeKey(uint iv) =>
            ((iv ^ 0x00006576) << 0x10) | iv;

        public static uint MakeKey(uint key, uint iv) =>
            key * iv;

        public static (uint IV, uint Salt) MakeKey(uint materialA, uint materialB, uint materialC)
        {
            var pageKey = materialA ^ materialB;
            var iv = MakeKey(pageKey);
            var salt = MakeKey(pageKey, materialC);
            return (iv, salt);
        }

        protected override void GenerateBlock(byte[] block)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(block, this.KeyAccumulator);
            this.KeyAccumulator = unchecked((this.KeyAccumulator * Multiplier) + this.Salt);
        }
    }
}
