namespace Codec.Rendering
{
    using System;
    using System.Linq;
    using System.Numerics;
    using Silk.NET.OpenGL;

    public sealed class ShaderHandle<TVertex> : IDisposable
    {
        private readonly GL gl;
        private readonly (int count, VertexAttribPointerType type, uint stride, uint offset)[] attributes;
        private readonly uint handle;

        public ShaderHandle(GL gl, (int count, VertexAttribPointerType type, int size)[] attributes, Func<string> getVertexShader, Func<string> getFragmentShader)
        {
            this.gl = gl;

            var offset = 0u;
            var stride = attributes.Sum(a => a.count * a.size);
            this.attributes = new (int count, VertexAttribPointerType type, uint stride, uint offset)[attributes.Length];
            for (var i = 0u; i < this.attributes.Length; i++)
            {
                var (count, type, size) = attributes[i];
                var width = (uint)(count * size);
                this.attributes[i] = (count, type, (uint)stride, offset);
                offset += width;
            }

            this.handle =
                WithShader(this.gl, ShaderType.VertexShader, getVertexShader, vertex =>
                    WithShader(this.gl, ShaderType.FragmentShader, getFragmentShader, fragment =>
                        LinkShaders(this.gl, vertex, fragment)));
        }

        public static uint LinkShaders(GL gl, uint vertex, uint fragment)
        {
            var handle = gl.CreateProgram();
            gl.AttachShader(handle, vertex);
            gl.AttachShader(handle, fragment);
            try
            {
                gl.LinkProgram(handle);
                gl.GetProgram(handle, GLEnum.LinkStatus, out var status);
                if (status == 0)
                {
                    throw new InvalidProgramException($"Program failed to link with error:{Environment.NewLine}{gl.GetProgramInfoLog(handle)}");
                }

                return handle;
            }
            finally
            {
                gl.DetachShader(handle, vertex);
                gl.DetachShader(handle, fragment);
            }
        }

        public static T WithShader<T>(GL gl, ShaderType type, Func<string> getShader, Func<uint, T> action)
        {
            var src = getShader();
            var handle = gl.CreateShader(type);
            try
            {
                gl.ShaderSource(handle, src);
                gl.CompileShader(handle);
                var infoLog = gl.GetShaderInfoLog(handle);
                if (!string.IsNullOrWhiteSpace(infoLog))
                {
                    throw new InvalidProgramException($"Error compiling '{type}', failed with error:{Environment.NewLine}{infoLog}");
                }

                return action(handle);
            }
            finally
            {
                gl.DeleteShader(handle);
            }
        }

        public void Use()
        {
            this.gl.UseProgram(this.handle);
        }

        public unsafe void Bind()
        {
            this.Use();

            for (var i = 0u; i < this.attributes.Length; i++)
            {
                var (count, type, stride, offset) = this.attributes[i];
                this.gl.VertexAttribPointer(i, count, type, false, stride, (void*)offset);
                this.gl.EnableVertexAttribArray(i);
            }
        }

        public void SetUniform(string name, Vector2 value) =>
            this.gl.Uniform2(this.GetUniformLocation(name), new[] { value.X, value.Y }.AsSpan());

        public void SetUniform(string name, Vector3 value) =>
            this.gl.Uniform3(this.GetUniformLocation(name), new[] { value.X, value.Y, value.Z }.AsSpan());

        public void SetUniform(string name, Vector4 value) =>
            this.gl.Uniform4(this.GetUniformLocation(name), new[] { value.X, value.Y, value.Z, value.W }.AsSpan());

        public void SetUniform(string name, int value) =>
            this.gl.Uniform1(this.GetUniformLocation(name), value);

        public unsafe void SetUniform(string name, Matrix4x4 value) =>
            this.gl.UniformMatrix4(this.GetUniformLocation(name), 1, false, (float*)&value);

        public void SetUniform(string name, float value) =>
            this.gl.Uniform1(this.GetUniformLocation(name), value);

        public void Dispose()
        {
            this.gl.DeleteProgram(this.handle);
            GC.SuppressFinalize(this);
        }

        private int GetUniformLocation(string name) =>
            this.gl.GetUniformLocation(this.handle, name) switch
            {
                -1 => throw new ArgumentException($"Uniform '{name}' not found on shader.", nameof(name)),
                var value => value,
            };
    }
}
