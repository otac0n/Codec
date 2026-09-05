// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Archives
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Intervals;

    public record class ByteRange(long Offset, long Length) : IInterval<long>
    {
        /// <inheritdoc/>
        public long End => this.Offset + this.Length;

        bool IInterval<long>.EndInclusive => false;

        long IInterval<long>.Start => this.Offset;

        bool IInterval<long>.StartInclusive => true;

        IInterval<long> IInterval<long>.Clone(long start, bool startInclusive, long end, bool endInclusive)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(startInclusive, false);
            ArgumentOutOfRangeException.ThrowIfEqual(endInclusive, true);
            return new ByteRange(start, end - start);
        }

        public static ByteRange FindFreeSpace(long neededLength, IList<ByteRange> occupied, out long fileSize)
        {
            fileSize = occupied.Max(e => e.Offset + e.Length);
            var whole = new ByteRange(0, fileSize);
            var free = IntervalExtensions.DifferenceWith(whole, occupied) ?? [];
            var found = free
                .Where(gap => gap.End - gap.Start >= neededLength)
                .OrderBy(gap => gap.End - gap.Start)
                .Select(gap => new ByteRange(gap.Start, neededLength))
                .FirstOrDefault();

            if (found == null)
            {
                found = new ByteRange(fileSize, neededLength);
                fileSize += neededLength;
            }

            return found;
        }
    }
}
