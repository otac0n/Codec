namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using Assimp;
    using Codec.Files;
    using Codec.Geometry;
    using Microsoft.Extensions.DependencyInjection;

    public class KmdFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileHandlerResolver<RenderableScene>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".kmd", StringComparison.OrdinalIgnoreCase))
                {
                    return (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var file = parent.File.OpenRead(parentRelativePath);
                        return (RenderableScene)FromStream(file);
                    };
                }

                return null;
            });
        }

        public static Scene FromStream(Stream stream)
        {
            var buffer = new byte[stream.Length];
            var fileSpan = buffer.AsSpan();
            stream.ReadExactly(fileSpan);
            var header = MemoryMarshal.Cast<byte, Header>(fileSpan)[0];
            var meshDefinitions = MemoryMarshal.Cast<byte, MeshDefinition>(fileSpan[32..])[0..(int)header.MeshCount];

            var scene = new Scene();
            var rootNode = new Node("root");
            scene.RootNode = rootNode;
            var nodes = new List<Node>((int)header.MeshCount);
            for (var m = 0; m < header.MeshCount; m++)
            {
                var mesh = meshDefinitions[m];
                var relativeNode = mesh.RelativeIndex != -1 && !(mesh.RelativeIndex == 0 && m == 0) ? nodes[mesh.RelativeIndex] : rootNode;

                var vertexCount = (int)mesh.VertexCount;
                var normalCount = (int)mesh.NormalCount;
                var faceCount = (int)mesh.FaceCount;
                var texCoordCount = (int)((mesh.TextureOffset - mesh.TextureCoordOffset) / 2);

                var vertices = MemoryMarshal.Cast<byte, Vec4<short>>(fileSpan[(int)mesh.VertexOffset..])[..vertexCount];
                var normals = MemoryMarshal.Cast<byte, Vec4<short>>(fileSpan[(int)mesh.NormalOffset..])[..normalCount];
                var textureCoords = MemoryMarshal.Cast<byte, Vec2<byte>>(fileSpan[(int)mesh.TextureCoordOffset..])[..texCoordCount];
                var textureIds = MemoryMarshal.Cast<byte, ushort>(fileSpan[(int)mesh.TextureOffset..])[..faceCount];
                var vertexIndices = MemoryMarshal.Cast<byte, Vec4<byte>>(fileSpan[(int)mesh.VertexIndexOffset..])[..faceCount];
                var normalIndices = MemoryMarshal.Cast<byte, Vec4<byte>>(fileSpan[(int)mesh.NormalIndexOffset..])[..faceCount];

                var facesByTex = new Dictionary<ushort, List<int>>();
                for (var f = 0; f < faceCount; f++)
                {
                    if (!facesByTex.TryGetValue(textureIds[f], out var list))
                    {
                        facesByTex[textureIds[f]] = list = [];
                    }

                    list.Add(f);
                }

                var flags = (DrawingFlags)mesh.Flags;
                var meshIndices = new List<int>();
                foreach (var (texId, faceGroup) in facesByTex)
                {
                    var assimpMesh = new Mesh($"mesh{m}_tex{texId}", PrimitiveType.Triangle)
                    {
                        MaterialIndex = EnsureMaterial(scene, texId, flags),
                    };
                    assimpMesh.UVComponentCount[0] = 2;

                    foreach (var fi in faceGroup)
                    {
                        var vi = vertexIndices[fi];
                        var ni = normalIndices[fi];
                        var baseVert = assimpMesh.Vertices.Count;
                        for (var c = 0; c < 4; c++)
                        {
                            var v = vertices[vi[c]];
                            assimpMesh.Vertices.Add(new(v.X, v.Y, v.Z));

                            var nix = ni[c];
                            var n = nix < normalCount ? normals[nix] : default;
                            assimpMesh.Normals.Add(new(n.X / 4096f, n.Y / 4096f, n.Z / 4096f));

                            var uv = textureCoords[4 * fi + c];
                            assimpMesh.TextureCoordinateChannels[0].Add(new(uv.U / 255f, uv.V / 255f, 0f));
                        }

                        assimpMesh.Faces.Add(new Face([baseVert + 1, baseVert, baseVert + 2]));
                        assimpMesh.Faces.Add(new Face([baseVert + 2, baseVert, baseVert + 3]));
                    }

                    scene.Meshes.Add(assimpMesh);
                    meshIndices.Add(scene.Meshes.Count - 1);
                }

                var origin = mesh.RelativeOrigin;
                var node = new Node($"node{m}")
                {
                    Transform = Matrix4x4.CreateTranslation(origin.X, origin.Y, origin.Z),
                };

                node.Metadata["DrawingFlags"] = new Metadata.Entry(MetaDataType.Int32, (int)flags);
                node.MeshIndices.AddRange(meshIndices);
                nodes.Add(node);
                relativeNode.Children.Add(node);
            }

            return scene;
        }

        private static int EnsureMaterial(Scene scene, ushort texId, DrawingFlags flags)
        {
            flags &= DrawingFlags.TwoSided | DrawingFlags.Transparent;
            var name = $"tex{texId}:{(uint)flags:x8}";
            for (var i = 0; i < scene.Materials.Count; i++)
            {
                if (scene.Materials[i].Name == name)
                {
                    return i;
                }
            }

            var mat = new Material
            {
                Name = name,
                TextureDiffuse = new TextureSlot(
                    filePath: $"../texture/{texId:x4}.pcx",
                    typeSemantic: TextureType.Diffuse,
                    texIndex: 0,
                    mapping: TextureMapping.FromUV,
                    uvIndex: 0,
                    blendFactor: 1f,
                    texOp: TextureOperation.Add,
                    wrapModeU: TextureWrapMode.Wrap,
                    wrapModeV: TextureWrapMode.Wrap,
                    flags: (int)TextureFlags.IgnoreAlpha),
                ColorTransparent = new(0, 0, 0, 1),
                TransparencyFactor = 1f,
                IsTwoSided = flags.HasFlag(DrawingFlags.TwoSided),
                Opacity = flags.HasFlag(DrawingFlags.Transparent) ? 0.5f : 1f,
            };

            mat.AddProperty(new MaterialProperty($"$tex.pixelArt,{(int)mat.TextureDiffuse.TextureType},{mat.TextureDiffuse.TextureIndex}", true));

            scene.Materials.Add(mat);
            return scene.Materials.Count - 1;
        }

        [Flags]
        public enum DrawingFlags : uint
        {
            Visible = 0b00000000000000000001,
            Transparent = 0b00000000000000000010,
            NoLight = 0b00000000000000000100,
            TwoSided = 0b00000000010000000000,
            Indirect = 0b00010000000000000000,
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private struct Header
        {
            public uint FaceCount;
            public uint MeshCount;
            public Vec3<int> Min;
            public Vec3<int> Max;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private struct MeshDefinition
        {
            public uint Flags;
            public uint FaceCount;
            public Vec3<int> Min;
            public Vec3<int> Max;
            public Vec3<int> RelativeOrigin;
            public int RelativeIndex;
            public uint UnkownA;
            public uint VertexCount;
            public uint VertexOffset;
            public uint VertexIndexOffset;
            public uint NormalCount;
            public uint NormalOffset;
            public uint NormalIndexOffset;
            public uint TextureCoordOffset;
            public uint TextureOffset;
            public uint UnkownB;
        }
    }
}
