namespace Codec.Rendering
{
    using Silk.NET.OpenGL;

    public static class DynamicDraw
    {
        public static void DrawTriangles<TVertex>(this GL gl, TVertex[] vertices, ShaderHandle<TVertex> shader)
            where TVertex : unmanaged
        {
            gl.DrawPrimitives(vertices, PrimitiveType.Triangles, shader);
        }

        public static void DrawStrip<TVertex>(this GL gl, TVertex[] vertices, ShaderHandle<TVertex> shader)
            where TVertex : unmanaged
        {
            gl.DrawPrimitives(vertices, PrimitiveType.TriangleStrip, shader);
        }

        private static unsafe void DrawPrimitives<TVertex>(this GL gl, TVertex[] vertices, PrimitiveType primitiveType, ShaderHandle<TVertex> shader)
            where TVertex : unmanaged
        {
            var vao = gl.GenVertexArray();
            try
            {
                gl.BindVertexArray(vao);

                using var vbo = new Buffer<TVertex>(gl, vertices, BufferTargetARB.ArrayBuffer, BufferUsageARB.DynamicDraw);
                shader.Bind();

                gl.DrawArrays(primitiveType, 0, (uint)vertices.Length);
            }
            finally
            {
                gl.DeleteVertexArray(vao);
            }
        }
    }
}
