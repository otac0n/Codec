namespace Codec.Rendering
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Numerics;
    using System.Threading.Tasks;
    using Codec.Archives;
    using Codec.Files;
    using Silk.NET.OpenGL;

    public class MaterialCache1 : MaterialCache
    {
        private readonly ShaderHandle<(Vector3 Position, Vector2 UV)> shader;
        private readonly Dictionary<ushort, Task<TextureHandle>> textures = [];
        private readonly GL gl;
        private readonly string path;
        private readonly NestedFileSystemManager fsm;

        public MaterialCache1(GL gl, KmdFile.Model1 model, string path, NestedFileSystemManager fsm)
        {
            this.gl = gl;
            this.path = path;
            this.fsm = fsm;
            this.shader = new(
                gl,
                [
                    (3, VertexAttribPointerType.Float, sizeof(float)),
                    (2, VertexAttribPointerType.Float, sizeof(float)),
                ],
                () => """
                        #version 330 core
                        layout (location = 0) in vec3 vertex_position;
                        layout (location = 1) in vec2 vertex_textureCoords;
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
                        uniform sampler2D uniform_texture;
                        uniform int uniform_textureAvailable;
                        uniform float uniform_opactiy;
                        in vec2 fragment_textureCoords;
                        out vec4 color;
                        void main()
                        {
                            color = uniform_textureAvailable > 0.5
                                ? texture(uniform_texture, fragment_textureCoords)
                                : vec4(fragment_textureCoords.x, fragment_textureCoords.y, 0.0, 1.0);
                            if (color.r == 0 && color.g == 0 && color.b == 0)
                            {
                                discard;
                            }
                            color.a = uniform_opactiy;
                        }
                    """);
        }

        public override ShaderHandle<(Vector3 Position, Vector2 UV)>? Resolve(Model.Mesh.Face face)
        {
            if (this.GetTexture(face) is TextureHandle texture)
            {
                texture.Activate();
                this.shader.SetUniform("uniform_texture", 0);
                this.shader.SetUniform("uniform_textureAvailable", 1);
            }
            else
            {
                this.shader.SetUniform("uniform_textureAvailable", 0);
            }

            return this.shader;
        }

        protected override TextureHandle? GetTexture(Model.Mesh.Face face)
        {
            var textureId = ((KmdFile.Model1.Mesh1.Face1)face).TextureId;
            if (this.textures.TryGetValue(textureId, out var task))
            {
                return task.IsCompletedSuccessfully ? task.Result : null;
            }

            var rootFolder = Path.GetDirectoryName(Path.GetDirectoryName(this.path));
            var path = Path.Combine(rootFolder, "texture", textureId.ToString("x4") + ".pcx");
            using var bmp = this.fsm.Resolve<Bitmap>(path);
            this.textures[textureId] = Task.FromResult(bmp == null ? null : new TextureHandle(this.gl, bmp, TextureMagFilter.Nearest, TextureMinFilter.Nearest));

            return null;
        }
    }
}
