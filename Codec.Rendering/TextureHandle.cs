// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Rendering
{
    using System;
    using ImageMagick;
    using Silk.NET.OpenGL;

    public sealed class TextureHandle : IDisposable
    {
        private readonly GL gl;

        public unsafe TextureHandle(GL gl, MagickImage source, TextureMagFilter magnify = TextureMagFilter.Linear, TextureMinFilter minify = TextureMinFilter.LinearMipmapLinear)
        {
            this.gl = gl;

            this.Handle = gl.GenTexture();
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, this.Handle);

            using var pixels = source.GetPixelsUnsafe();
            var bytes = pixels.ToByteArray("BGRA") ?? throw new InvalidOperationException("Failed to get BGRA pixel data from MagickImage.");

            fixed (byte* ptr = bytes)
            {
                gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, source.Width, source.Height, 0, Silk.NET.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, ptr);
            }

            gl.TextureParameter(this.Handle, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TextureParameter(this.Handle, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            gl.TextureParameter(this.Handle, TextureParameterName.TextureMinFilter, (int)minify);
            gl.TextureParameter(this.Handle, TextureParameterName.TextureMagFilter, (int)magnify);
            gl.GenerateMipmap(TextureTarget.Texture2D);
            gl.BindTexture(TextureTarget.Texture2D, 0);
        }

        public uint Handle { get; private set; }

        public void Activate(TextureUnit textureUnit = TextureUnit.Texture0)
        {
            this.gl.ActiveTexture(textureUnit);
            this.gl.BindTexture(TextureTarget.Texture2D, this.Handle);
        }

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
