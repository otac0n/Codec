namespace Codec.Rendering
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Numerics;
    using Codec.Archives;
    using Codec.Files;
    using Silk.NET.OpenGL;

    public class GLModelViewer
    {
        private readonly Model model;
        private readonly string path;
        private readonly NestedFileSystemManager fsm;
        private GL gl;
        private MaterialCache materialCache;
        private Stopwatch T;
        private Vector3 center;
        private float size;

        public GLModelViewer(string path, NestedFileSystemManager fsm, Model model = null)
        {
            this.path = path;
            this.fsm = fsm;
            this.model = model ?? this.fsm.Resolve<Model>(this.path)!;
        }

        public void Initialize(GL gl)
        {
            this.gl = gl;
            this.materialCache = MaterialCache.Create(this.gl, this.model, this.path, this.fsm);
            this.gl.Enable(EnableCap.DepthTest);
            this.gl.ClearColor(0.5f, 0.5f, 0.5f, 1);
            this.T = Stopwatch.StartNew();
            this.Camera.Up = new Vector3(0, 1, 0);
            this.UpdateModel();
        }

        public void Render(int width, int height)
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

            this.gl.Disable(EnableCap.CullFace);
            this.gl.Enable(EnableCap.DepthTest);
            this.gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

            foreach (var mesh in this.model.Meshes)
            {
                foreach (var face in mesh.Faces)
                {
                    var vertices = face.VertexIndices.Select((i, j) => (position: mesh.Vertices[i], uv: (face.TextureIndices is uint[] uv ? mesh.TextureCoords?[uv[j]] : null) ?? new(0, 0))).ToArray();
                    var shader = this.materialCache.Resolve(face);
                    shader?.SetUniform("uniform_cameraMatrix", this.Camera.Matrix);
                    this.gl.DrawStrip(vertices, shader);
                }
            }
        }

        protected Camera Camera { get; } = new();

        private void UpdateModel()
        {
            var min = new Vector3(float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity);

            foreach (var mesh in this.model.Meshes)
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
            this.Camera.FarPlane = Math.Max(2 * this.size, 0.2f);
        }
    }
}
