namespace Codec.Rendering
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Numerics;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Codec.Archives;
    using Codec.Files;
    using Silk.NET.OpenGL;

    public partial class MaterialCache2 : MaterialCache
    {
        private readonly ShaderHandle<(Vector3 Position, Vector2 UV)> shader;
        private readonly Dictionary<(ulong ModelId, ulong TextureId), Task<TextureHandle>> textures = [];
        private readonly GL gl;
        private readonly string path;
        private readonly NestedFileSystemManager fsm;

        public MaterialCache2(GL gl, KmsFile.Model2 model, string path, NestedFileSystemManager fsm)
        {
            this.gl = gl;
            this.path = path;
            this.fsm = fsm;
            this.shader = new ShaderHandle<(Vector3 Position, Vector2 UV)>(
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
                        in vec2 fragment_textureCoords;
                        out vec4 color;
                        void main()
                        {
                            color = uniform_textureAvailable > 0.5
                                ? texture(uniform_texture, fragment_textureCoords)
                                : vec4(fragment_textureCoords.x, fragment_textureCoords.y, 0.0, 1.0);
                            if (color.a < 0.01)
                            {
                                discard;
                            }
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

        [GeneratedRegex(@"^[a-f0-9]{8}(?=_|\.|$)")]
        private static partial Regex HexPrefixRegex();

        ulong GetStrCode(string filename)
        {
            ulong Hash(string s)
            {
                var h = 0UL;

                for (var c = 0; c < s.Length; c++)
                {
                    h = ((h << 0x05) | (h >> 0x13)) + s[c];
                    h &= 0xffffff;
                }

                return h;
            }

            filename = Path.GetFileNameWithoutExtension(filename);
            if (HexPrefixRegex().Match(filename) is { Success: true } match)
            {
                return ulong.Parse(match.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return Hash(filename);
        }

        protected override TextureHandle? GetTexture(Model.Mesh.Face face)
        {
            var textureId = ((KmsFile.Model2.Mesh2.Face2)face).TextureId;
            if (this.textures.TryGetValue(textureId, out var task))
            {
                return task.IsCompletedSuccessfully ? task.Result : null;
            }

            var parentFolder = Path.GetDirectoryName(this.path);
            var rootFolder = Path.GetDirectoryName(Path.GetDirectoryName(parentFolder));
            var searchPath = Path.Combine(rootFolder, "tri", Path.GetFileName(parentFolder));
            foreach (var tri in this.fsm.EnumerateFiles(searchPath, "*.tri"))
            {
                var hash = GetStrCode(tri.Path);
                if (hash == textureId.modelCode)
                {
                    var path = Path.Combine(tri.Path, textureId.textureCode.ToString("x6") + ".tm2");
                    if (this.fsm.FileExists(path))
                    {
                        using var bmp = this.fsm.Resolve<Bitmap>(path);
                        this.textures[textureId] = Task.FromResult(bmp == null ? null : new TextureHandle(this.gl, bmp));
                        return null;
                    }
                }
            }

            this.textures[textureId] = Task.FromResult(default(TextureHandle));
            return null;
        }
    }
}
