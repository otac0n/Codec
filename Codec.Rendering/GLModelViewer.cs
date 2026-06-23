namespace Codec.Rendering
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using Assimp;
    using Codec.Archives;
    using Codec.Files;
    using ImageMagick;
    using Silk.NET.OpenGL;

    public class GLModelViewer : IDisposable
    {
        private readonly Scene scene;
        private readonly string path;
        private readonly NestedFileSystemManager fsm;
        private readonly Dictionary<(string, bool), TextureHandle?> textures = [];
        private readonly List<MeshGpuData> gpuMeshes = [];
        private GL gl;
        private Stopwatch T;
        private Vector3 center;
        private float size;
        private ShaderHandle<Vector3> shader;

        private sealed record MeshGpuData(uint Vao, uint Vbo, uint Ebo, int IndexCount, Material Material);

        public GLModelViewer(string path, NestedFileSystemManager fsm, RenderableScene? scene = null)
        {
            this.path = path;
            this.fsm = fsm;
            this.scene = scene ?? this.fsm.Resolve<RenderableScene>(this.path)!;
        }

        protected Camera Camera { get; } = new();

        public void Initialize(GL gl)
        {
            this.gl = gl;
            this.gl.Enable(EnableCap.DepthTest);
            this.gl.ClearColor(0.5f, 0.5f, 0.5f, 1);
            this.gl.Enable(EnableCap.Blend);
            this.gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            this.T = Stopwatch.StartNew();
            this.Camera.Up = new Vector3(0, 1, 0);
            this.shader = new(
                gl,
                [
                    (3, VertexAttribPointerType.Float, sizeof(float)),
                    (3, VertexAttribPointerType.Float, sizeof(float)),
                    (2, VertexAttribPointerType.Float, sizeof(float)),
                ],
                () => """
                        #version 330 core
                        layout (location = 0) in vec3 vertex_position;
                        layout (location = 1) in vec3 vertex_normal;
                        layout (location = 2) in vec2 vertex_textureCoords;
                        uniform mat4 uniform_cameraMatrix;
                        out vec2 fragment_textureCoords;
                        void main()
                        {
                            gl_Position = uniform_cameraMatrix * vec4(vertex_position, 1.0);
                            fragment_textureCoords = vertex_textureCoords;
                        }
                    """,
                () => """
                        #version 330 core
                        uniform sampler2D uniform_textureDiffuse;
                        uniform vec4 uniform_colorTransparent;
                        uniform float uniform_transparencyFactor;
                        uniform float uniform_opactiy;
                        in vec2 fragment_textureCoords;
                        out vec4 color;
                        void main()
                        {
                            color = texture(uniform_textureDiffuse, fragment_textureCoords);

                            if (color == uniform_colorTransparent)
                            {
                                color.a = 1 - uniform_transparencyFactor;
                            }

                            color.a *= uniform_opactiy;

                            if (color.a <= 0.01)
                            {
                                discard;
                            }
                        }
                    """);
            this.UpdateModel();
        }

        public void Dispose()
        {
            foreach (var m in this.gpuMeshes)
            {
                this.gl.DeleteVertexArray(m.Vao);
                this.gl.DeleteBuffer(m.Vbo);
                this.gl.DeleteBuffer(m.Ebo);
            }

            this.gpuMeshes.Clear();
        }

        public unsafe void Render(int width, int height)
        {
            var a = Math.Tau * this.T.Elapsed.TotalSeconds / 5;
            var (x, z) = Math.SinCos(a);
            var t = Math.Sin(a / 3);
            var p = new Vector3((float)(this.size * x), (float)(this.size / 10 * t), (float)(this.size * z));
            this.Camera.Position = this.center + p;
            this.Camera.Direction = -p;

            this.Camera.Width = width;
            this.Camera.Height = height;
            this.gl.Viewport(0, 0, (uint)width, (uint)height);
            this.gl.Enable(EnableCap.DepthTest);
            this.gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

            this.shader.Use();
            this.shader.SetUniform("uniform_cameraMatrix", this.Camera.Matrix);

            void DrawMesh(MeshGpuData gpuMesh)
            {
                if (gpuMesh.Material.IsTwoSided)
                {
                    this.gl.Disable(EnableCap.CullFace);
                }
                else
                {
                    this.gl.Enable(EnableCap.CullFace);
                }

                if (gpuMesh.Material.IsWireFrameEnabled)
                {
                    this.gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Line);
                }
                else
                {
                    this.gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);
                }

                var pixelArt = gpuMesh.Material
                    .GetAllProperties()
                    .SingleOrDefault(p => p.Name == $"$tex.pixelArt,{(int)gpuMesh.Material.TextureDiffuse.TextureType},{gpuMesh.Material.TextureDiffuse.TextureIndex}")
                    ?.GetBooleanValue()
                    ?? false;
                if (this.GetTexture(gpuMesh.Material.TextureDiffuse.FilePath, pixelArt) is TextureHandle texture)
                {
                    texture.Activate();
                    this.shader.SetUniform("uniform_textureDiffuse", 0);
                }

                this.shader.SetUniform("uniform_colorTransparent", gpuMesh.Material.ColorTransparent);
                this.shader.SetUniform("uniform_transparencyFactor", gpuMesh.Material.TransparencyFactor);
                this.shader.SetUniform("uniform_opactiy", gpuMesh.Material.Opacity);

                this.gl.BindVertexArray(gpuMesh.Vao);
                this.gl.DrawElements(Silk.NET.OpenGL.PrimitiveType.Triangles, (uint)gpuMesh.IndexCount, DrawElementsType.UnsignedInt, null);
            }

            var anyTransparent = false;
            foreach (var gpuMesh in this.gpuMeshes)
            {
                if (gpuMesh.Material.HasOpacity && gpuMesh.Material.Opacity < 1)
                {
                    anyTransparent = true;
                    continue;
                }

                DrawMesh(gpuMesh);
            }

            if (anyTransparent)
            {
                foreach (var gpuMesh in this.gpuMeshes)
                {
                    if (gpuMesh.Material.HasOpacity && gpuMesh.Material.Opacity < 1)
                    {
                        DrawMesh(gpuMesh);
                    }
                }
            }

            this.gl.BindVertexArray(0);
        }

        private unsafe void UpdateModel()
        {
            var min = new Vector3(float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity);

            foreach (var mesh in this.scene.Meshes)
            {
                foreach (var v in mesh.Vertices)
                {
                    min = Vector3.Min(min, v);
                    max = Vector3.Max(max, v);
                }
            }

            var size = max - min;
            this.center = min + size / 2;
            this.size = Math.Max(size.X, Math.Max(size.Y, size.Z));
            this.Camera.NearPlane = Math.Max(this.size / 1000f, 0.0001f);
            this.Camera.FarPlane = Math.Max(2 * this.size, 0.2f);

            var nodes = new Queue<(Matrix4x4, Node)>();
            nodes.Enqueue((Matrix4x4.Identity, this.scene.RootNode));
            while (nodes.Count > 0)
            {
                var (transform, node) = nodes.Dequeue();
                transform *= Matrix4x4.Transpose(node.Transform);
                foreach (var mesh in node.MeshIndices.Select(i => this.scene.Meshes[i]))
                {
                    var hasUV = mesh.HasTextureCoords(0);

                    var verts = new float[mesh.VertexCount * (3 + 3 + 2)];
                    for (var i = 0; i < mesh.VertexCount; i++)
                    {
                        var pos = Vector3.Transform(mesh.Vertices[i], transform);
                        var nor = mesh.Normals.Count > i ? mesh.Normals[i] : new(0, 1, 0);
                        var uv = hasUV ? mesh.TextureCoordinateChannels[0][i] : new(0, 0, 0);
                        var o = i * 8;
                        (verts[o], verts[o + 1], verts[o + 2]) = (pos.X, pos.Y, pos.Z);
                        (verts[o + 3], verts[o + 4], verts[o + 5]) = (nor.X, nor.Y, nor.Z);
                        (verts[o + 6], verts[o + 7]) = (uv.X, 1 - uv.Y);
                    }

                    var indices = mesh.Faces
                        .SelectMany(f => f.Indices)
                        .Select(i => (uint)i)
                        .ToArray();

                    var vao = this.gl.GenVertexArray();
                    var vbo = this.gl.GenBuffer();
                    var ebo = this.gl.GenBuffer();

                    this.gl.BindVertexArray(vao);

                    this.gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
                    this.gl.BufferData(BufferTargetARB.ArrayBuffer, new ReadOnlySpan<float>(verts), BufferUsageARB.StaticDraw);

                    this.gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
                    this.gl.BufferData(BufferTargetARB.ElementArrayBuffer, new ReadOnlySpan<uint>(indices), BufferUsageARB.StaticDraw);

                    const uint stride = 8 * sizeof(float);
                    this.gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)(0 * sizeof(float)));
                    this.gl.EnableVertexAttribArray(0);
                    this.gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
                    this.gl.EnableVertexAttribArray(1);
                    this.gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)(6 * sizeof(float)));
                    this.gl.EnableVertexAttribArray(2);
                    this.gl.BindVertexArray(0);

                    var mat = this.scene.Materials[mesh.MaterialIndex];
                    this.gpuMeshes.Add(new(vao, vbo, ebo, indices.Length, mat));
                }

                foreach (var child in node.Children)
                {
                    nodes.Enqueue((transform, child));
                }
            }
        }

        protected TextureHandle? GetTexture(string texturePath, bool pixelArt)
        {
            if (texturePath == null)
            {
                return null;
            }

            if (this.textures.TryGetValue((texturePath, pixelArt), out var texture))
            {
                return texture;
            }

            var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(this.path), texturePath));
            using var img = this.fsm.Resolve<MagickImage>(path);
            return this.textures[(texturePath, pixelArt)] = img == null ? null : new TextureHandle(this.gl, img, pixelArt ? TextureMagFilter.Nearest : TextureMagFilter.Linear, pixelArt ? TextureMinFilter.Nearest : TextureMinFilter.Linear);
        }
    }
}
