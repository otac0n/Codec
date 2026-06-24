namespace Codec.Geometry
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vec2<T>
    {
        public T U;
        public T V;

        [UnscopedRef]
        public ref T this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return ref this.U;
                    case 1:
                        return ref this.V;
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public Span<T> AsSpan() => MemoryMarshal.CreateSpan(ref this.U, 2);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vec3<T>
    {
        public T X;
        public T Y;
        public T Z;

        [UnscopedRef]
        public ref T this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return ref this.X;
                    case 1:
                        return ref this.Y;
                    case 2:
                        return ref this.Z;
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public Span<T> AsSpan() => MemoryMarshal.CreateSpan(ref this.X, 3);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vec4<T>
    {
        public T X;
        public T Y;
        public T Z;
        public T W;

        [UnscopedRef]
        public ref T this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return ref this.X;
                    case 1:
                        return ref this.Y;
                    case 2:
                        return ref this.Z;
                    case 3:
                        return ref this.W;
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public Span<T> AsSpan() => MemoryMarshal.CreateSpan(ref this.X, 4);
    }
}
