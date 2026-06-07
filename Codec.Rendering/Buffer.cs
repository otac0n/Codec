namespace Codec.Rendering
{
    using System;
    using Silk.NET.OpenGL;

    public sealed class Buffer<T> : IDisposable
        where T : unmanaged
    {
        private readonly GL gl;

        public unsafe Buffer(GL gl, T[] source, BufferTargetARB bufferTarget, BufferUsageARB bufferUsage)
        {
            this.gl = gl;
            this.Length = (uint)source.Length;

            this.Handle = gl.GenBuffer();
            gl.BindBuffer(bufferTarget, this.Handle);

            fixed (T* bufferData = source)
            {
                gl.BufferData(bufferTarget, (nuint)(source.Length * sizeof(T)), bufferData, bufferUsage);
            }
        }

        public uint Length { get; }

        public uint Handle { get; private set; }

        public void Dispose()
        {
            if (this.Handle != 0)
            {
                this.gl.DeleteBuffer(this.Handle);
                this.Handle = 0;
            }
        }
    }
}
