// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec
{
    using System;
    using System.Buffers.Binary;
    using System.IO;
    using System.Numerics;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    internal static class StreamExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetPadding<T>(T offset, T alignment)
            where T : IModulusOperators<T, T, T>, INumberBase<T>
        {
            return alignment == T.Zero ? T.Zero : (alignment - offset % alignment) % alignment;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Align<T>(T offset, T alignment)
            where T : IModulusOperators<T, T, T>, INumberBase<T>
        {
            return offset + GetPadding(offset, alignment);
        }

        public static void Align(this Stream stream, long alignment)
        {
            var offset = GetPadding(stream.Position, alignment);
            if (offset > 0)
            {
                stream.Seek(offset, SeekOrigin.Current);
            }
        }

        public static bool TryAlign(this Stream stream, long alignment)
        {
            var offset = GetPadding(stream.Position, alignment);
            if (offset + stream.Position > stream.Length)
            {
                return false;
            }

            if (offset > 0)
            {
                stream.Seek(offset, SeekOrigin.Current);
            }

            return true;
        }

        public static short ReadInt16BigEndian(this Stream s)
        {
            Span<byte> b = stackalloc byte[sizeof(short)];
            s.ReadExactly(b);
            return BinaryPrimitives.ReadInt16BigEndian(b);
        }

        public static ushort ReadUInt16BigEndian(this Stream s)
        {
            Span<byte> b = stackalloc byte[sizeof(ushort)];
            s.ReadExactly(b);
            return BinaryPrimitives.ReadUInt16BigEndian(b);
        }

        public static int ReadInt32BigEndian(this Stream s)
        {
            Span<byte> b = stackalloc byte[sizeof(int)];
            s.ReadExactly(b);
            return BinaryPrimitives.ReadInt32BigEndian(b);
        }

        public static uint ReadUInt32BigEndian(this Stream s)
        {
            Span<byte> b = stackalloc byte[sizeof(uint)];
            s.ReadExactly(b);
            return BinaryPrimitives.ReadUInt32BigEndian(b);
        }

        public static long ReadInt64BigEndian(this Stream s)
        {
            Span<byte> b = stackalloc byte[sizeof(long)];
            s.ReadExactly(b);
            return BinaryPrimitives.ReadInt64BigEndian(b);
        }

        public static ulong ReadUInt64BigEndian(this Stream s)
        {
            Span<byte> b = stackalloc byte[sizeof(ulong)];
            s.ReadExactly(b);
            return BinaryPrimitives.ReadUInt64BigEndian(b);
        }

        public static float ReadSingleBigEndian(this Stream s)
        {
            Span<byte> b = stackalloc byte[sizeof(float)];
            s.ReadExactly(b);
            return BinaryPrimitives.ReadSingleBigEndian(b);
        }

        public static double ReadDoubleBigEndian(this Stream s)
        {
            Span<byte> b = stackalloc byte[sizeof(double)];
            s.ReadExactly(b);
            return BinaryPrimitives.ReadDoubleBigEndian(b);
        }

        public static short ReadInt16LittleEndian(this Stream s)
        {
            Span<byte> b = stackalloc byte[sizeof(short)];
            s.ReadExactly(b);
            return BinaryPrimitives.ReadInt16LittleEndian(b);
        }

        public static ushort ReadUInt16LittleEndian(this Stream s)
        {
            Span<byte> b = stackalloc byte[sizeof(ushort)];
            s.ReadExactly(b);
            return BinaryPrimitives.ReadUInt16LittleEndian(b);
        }

        public static int ReadInt32LittleEndian(this Stream s)
        {
            Span<byte> b = stackalloc byte[sizeof(int)];
            s.ReadExactly(b);
            return BinaryPrimitives.ReadInt32LittleEndian(b);
        }

        public static uint ReadUInt32LittleEndian(this Stream s)
        {
            Span<byte> b = stackalloc byte[sizeof(uint)];
            s.ReadExactly(b);
            return BinaryPrimitives.ReadUInt32LittleEndian(b);
        }

        public static long ReadInt64LittleEndian(this Stream s)
        {
            Span<byte> b = stackalloc byte[sizeof(long)];
            s.ReadExactly(b);
            return BinaryPrimitives.ReadInt64LittleEndian(b);
        }

        public static ulong ReadUInt64LittleEndian(this Stream s)
        {
            Span<byte> b = stackalloc byte[sizeof(ulong)];
            s.ReadExactly(b);
            return BinaryPrimitives.ReadUInt64LittleEndian(b);
        }

        public static float ReadSingleLittleEndian(this Stream s)
        {
            Span<byte> b = stackalloc byte[sizeof(float)];
            s.ReadExactly(b);
            return BinaryPrimitives.ReadSingleLittleEndian(b);
        }

        public static double ReadDoubleLittleEndian(this Stream s)
        {
            Span<byte> b = stackalloc byte[sizeof(double)];
            s.ReadExactly(b);
            return BinaryPrimitives.ReadDoubleLittleEndian(b);
        }

        public static void ReadExactly(this Stream source, byte[] buffer, int count) => source.ReadExactly(buffer, 0, count);

        public static void CopyTo(this Stream source, Stream destination, long offset, SeekOrigin origin, long count)
        {
            source.Seek(offset, origin);
            int read;
            var buffer = new byte[81920];
            while (count > 0 && (read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, count))) > 0)
            {
                destination.Write(buffer, 0, read);
                count -= read;
            }
        }

        public static bool Contains(this Stream source, byte[] pattern)
        {
            using var memory = new MemoryStream();
            source.CopyTo(memory);
            var subject = memory.ToArray();
            var l = subject.LongLength;
            for (var i = 0L; i < l; i++)
            {
                var found = true;
                for (var j = 0; found && j < pattern.Length && (i + j) < l; j++)
                {
                    if (subject[i + j] != pattern[j])
                    {
                        found = false;
                    }
                }

                if (found)
                {
                    return true;
                }
            }

            return false;
        }

        public static T ReadBigEndian<T>(this Stream stream)
            where T : struct =>
            ReadWithEndianness<T>(stream, swapEndianness: BitConverter.IsLittleEndian);

        public static T ReadLittleEndian<T>(this Stream stream)
            where T : struct =>
            ReadWithEndianness<T>(stream, swapEndianness: !BitConverter.IsLittleEndian);

        public static T ReadSytemEndianness<T>(this Stream stream)
            where T : struct =>
            ReadWithEndianness<T>(stream, false);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T ReadWithEndianness<T>(this Stream stream, bool swapEndianness)
            where T : struct
        {
            var size = Marshal.SizeOf<T>();
            var buffer = size < 64 ? stackalloc byte[size] : new byte[size].AsSpan();
            stream.ReadExactly(buffer);
            if (swapEndianness)
            {
                SwapFields(buffer, typeof(T));
            }

            return MemoryMarshal.Cast<byte, T>(buffer)[0];
        }

        public static T[] ReadArrayBigEndian<T>(this Stream stream, int count)
            where T : struct =>
            ReadArrayWithEndianness<T>(stream, count, swapEndianness: BitConverter.IsLittleEndian);

        public static T[] ReadArrayLittleEndian<T>(this Stream stream, int count)
            where T : struct =>
            ReadArrayWithEndianness<T>(stream, count, swapEndianness: !BitConverter.IsLittleEndian);

        public static T[] ReadArraySystemEndianness<T>(this Stream stream, int count)
            where T : struct =>
            ReadArrayWithEndianness<T>(stream, count, swapEndianness: false);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T[] ReadArrayWithEndianness<T>(this Stream stream, int count, bool swapEndianness)
            where T : struct
        {
            var elementSize = Marshal.SizeOf<T>();
            var totalSize = checked(elementSize * count);
            var buffer = totalSize < 64 ? stackalloc byte[totalSize] : new byte[totalSize].AsSpan();
            stream.ReadExactly(buffer);

            if (swapEndianness)
            {
                for (var offset = 0; offset < totalSize; offset += elementSize)
                {
                    SwapFields(buffer[offset..], typeof(T));
                }
            }

            return MemoryMarshal.Cast<byte, T>(buffer).ToArray();
        }

        public static void WriteBigEndian<T>(this Stream stream, T value)
            where T : struct =>
            stream.WriteWithEndianness(value, swapEndianness: BitConverter.IsLittleEndian);

        public static void WriteLittleEndian<T>(this Stream stream, T value)
            where T : struct =>
            stream.WriteWithEndianness(value, swapEndianness: !BitConverter.IsLittleEndian);

        public static void WriteSystemEndianness<T>(this Stream stream, T value)
            where T : struct =>
            stream.WriteWithEndianness(value, swapEndianness: false);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteWithEndianness<T>(this Stream stream, T value, bool swapEndianness)
            where T : struct
        {
            var size = Marshal.SizeOf<T>();
            var buffer = size < 64 ? stackalloc byte[size] : new byte[size].AsSpan();
            MemoryMarshal.Write(buffer, in value);
            if (swapEndianness)
            {
                SwapFields(buffer, typeof(T));
            }

            stream.Write(buffer);
        }

        public static void WriteArrayBigEndian<T>(this Stream stream, T[] values)
            where T : struct =>
            stream.WriteArrayWithEndianness(values, swapEndianness: BitConverter.IsLittleEndian);

        public static void WriteArrayLittleEndian<T>(this Stream stream, T[] values)
            where T : struct =>
            stream.WriteArrayWithEndianness(values, swapEndianness: !BitConverter.IsLittleEndian);

        public static void WriteArraySystemEndianness<T>(this Stream stream, T[] values)
            where T : struct =>
            stream.WriteArrayWithEndianness(values, swapEndianness: false);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteArrayWithEndianness<T>(this Stream stream, T[] values, bool swapEndianness)
            where T : struct
        {
            var elementSize = Marshal.SizeOf<T>();
            var totalSize = checked(elementSize * values.Length);
            var buffer = totalSize < 64 ? stackalloc byte[totalSize] : new byte[totalSize].AsSpan();
            values.AsSpan().CopyTo(MemoryMarshal.Cast<byte, T>(buffer));

            if (swapEndianness)
            {
                for (var offset = 0; offset < totalSize; offset += elementSize)
                {
                    SwapFields(buffer[offset..], typeof(T));
                }
            }

            stream.Write(buffer);
        }

        private static void SwapFields(Span<byte> buffer, Type type)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var offset = (int)Marshal.OffsetOf(type, field.Name);
                var fieldType = field.FieldType;

                if (fieldType.IsValueType && !fieldType.IsPrimitive && !fieldType.IsEnum)
                {
                    SwapFields(buffer.Slice(offset, Marshal.SizeOf(fieldType)), fieldType);
                }
                else
                {
                    var fieldSize = Marshal.SizeOf(fieldType);
                    if (fieldSize > 1)
                    {
                        buffer.Slice(offset, fieldSize).Reverse();
                    }
                }
            }
        }
    }
}
