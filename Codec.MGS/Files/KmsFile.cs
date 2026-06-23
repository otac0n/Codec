namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using Assimp;
    using Codec.Archives;
    using Codec.Files;
    using Codec.Geometry;
    using Codec.Services;
    using Microsoft.Extensions.DependencyInjection;

    public class KmsFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton(new EntryTypeMatcher(EntryType.Model, "*.kms"));

            services.AddSingleton<FileHandlerResolver<RenderableScene>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".kms", StringComparison.OrdinalIgnoreCase))
                {
                    return (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var file = parent.File.OpenRead(parentRelativePath);
                        return (RenderableScene)FromStream(serviceProvider.GetRequiredService<NestedFileSystemManager>(), fullPath, file);
                    };
                }

                return null;
            });
        }

        public static Scene FromStream(NestedFileSystemManager fsm, string fullPath, Stream stream)
        {
            var bytes = new byte[stream.Length];
            var fileSpan = bytes.AsSpan();
            stream.ReadExactly(fileSpan);
            var header = MemoryMarshal.Cast<byte, Header>(fileSpan)[0];
            var meshDefinitions = MemoryMarshal.Cast<byte, MeshDefinition>(fileSpan[64..])[..(int)header.PartCount];

            var scene = new Scene();
            var rootNode = new Node("root");
            scene.RootNode = rootNode;
            var textureCache = new Dictionary<ulong, string?>();
            var nodes = new List<Node>((int)header.PartCount);
            for (var p = 0; p < meshDefinitions.Length; p++)
            {
                var relativeNode = meshDefinitions[p].ParentIndex != -1 && !(meshDefinitions[p].ParentIndex == 0 && p == 0) ? nodes[meshDefinitions[p].ParentIndex] : rootNode;

                var stripDefinitions = MemoryMarshal.Cast<byte, StripDefinition>(fileSpan[(int)meshDefinitions[p].DefinitionOffset..])[0..(int)meshDefinitions[p].MeshCount];
                var totalVertices = 0u;
                for (var d = 0; d < stripDefinitions.Length; d++)
                {
                    totalVertices += stripDefinitions[d].VertexCount;
                }

                var vertices = new Vector3[totalVertices];
                var normals = new Vector3[totalVertices];
                var textureCoords = new Vector2[totalVertices];
                var trisByTex = new Dictionary<ulong, List<(int A, int B, int C)>>();
                var vertexOutputOffset = 0u;
                for (var d = 0; d < stripDefinitions.Length; vertexOutputOffset += stripDefinitions[d++].VertexCount)
                {
                    var vertexCount = stripDefinitions[d].VertexCount;
                    var vertexData = MemoryMarshal.Cast<byte, Vec4<short>>(fileSpan[(int)stripDefinitions[d].VertexOffset..])[..(int)vertexCount];
                    var normalData = MemoryMarshal.Cast<byte, Vec4<short>>(fileSpan[(int)stripDefinitions[d].NormalOffset..])[..(int)vertexCount];
                    for (var v = 0; v < vertexCount; v++)
                    {
                        vertices[v + vertexOutputOffset] = new(
                            vertexData[v].X,
                            vertexData[v].Y,
                            vertexData[v].Z);
                        normals[v + vertexOutputOffset] = new(
                            normalData[v].X / 4096f,
                            normalData[v].Y / 4096f,
                            normalData[v].Z / 4096f);
                    }

                    if (stripDefinitions[d].UV1Offset != 0)
                    {
                        var textureCoordData1 = MemoryMarshal.Cast<byte, Vec2<short>>(fileSpan[(int)stripDefinitions[d].UV1Offset..])[..(int)vertexCount];
                        for (var v = 0; v < vertexCount; v++)
                        {
                            textureCoords[vertexOutputOffset + v] = new(
                                textureCoordData1[v].U / 4096f,
                                1 - textureCoordData1[v].V / 4096f);
                        }
                    }

                    var texKey = stripDefinitions[d].TextureId1;
                    if (!trisByTex.TryGetValue(texKey, out var tris))
                    {
                        trisByTex[texKey] = tris = [];
                    }

                    var startIndex = -1;
                    var lastInclude = false;
                    for (var v = 0; v < vertexCount; v++)
                    {
                        var include = (normalData[v].W & 0x8000) == 0;
                        if (include)
                        {
                            if (!lastInclude)
                            {
                                startIndex = v - 2;
                            }
                        }
                        else
                        {
                            if (lastInclude)
                            {
                                ExpandStrip(tris, (int)vertexOutputOffset + startIndex, v - startIndex);
                            }
                        }

                        lastInclude = include;
                    }

                    if (lastInclude)
                    {
                        ExpandStrip(tris, (int)vertexOutputOffset + startIndex, (int)(vertexCount - startIndex));
                    }
                }

                var flags = meshDefinitions[p].Flags;
                var meshIndices = new List<int>();

                foreach (var (texId, tris) in trisByTex)
                {
                    if (tris.Count == 0)
                    {
                        continue;
                    }

                    var texturePath = GetTexturePath(fsm, fullPath, header.Id, textureCache, texId);
                    var assimpMesh = new Mesh($"{header.Id:x}_node{p}_tex{texId:x6}", PrimitiveType.Triangle)
                    {
                        MaterialIndex = EnsureMaterial(scene, texturePath, header.Id, texId, flags),
                    };
                    assimpMesh.UVComponentCount[0] = 2;

                    var indexRemap = new Dictionary<int, int>();
                    foreach (var (i0, i1, i2) in tris)
                    {
                        assimpMesh.Faces.Add(new Face([
                            RemapVertex(indexRemap, i0, vertices, normals, textureCoords, assimpMesh),
                            RemapVertex(indexRemap, i1, vertices, normals, textureCoords, assimpMesh),
                            RemapVertex(indexRemap, i2, vertices, normals, textureCoords, assimpMesh),
                        ]));
                    }

                    scene.Meshes.Add(assimpMesh);
                    meshIndices.Add(scene.Meshes.Count - 1);
                }

                var node = new Node($"node{p}")
                {
                    Transform = Matrix4x4.Transpose(Matrix4x4.CreateTranslation(meshDefinitions[p].RelativeOrigin)),
                };
                node.Metadata["DrawingFlags"] = new Metadata.Entry(MetaDataType.Int32, (int)flags);
                node.MeshIndices.AddRange(meshIndices);
                nodes.Add(node);
                relativeNode.Children.Add(node);
            }

            return scene;
        }

        private static void ExpandStrip(List<(int A, int B, int C)> triangles, int offset, int length)
        {
            for (var i = 0; i < length - 2; i++)
            {
                var a = offset + i;
                var b = offset + i + 1;
                var c = offset + i + 2;

                if (i % 2 != 0)
                {
                    triangles.Add((a, b, c));
                }
                else
                {
                    triangles.Add((b, a, c));
                }
            }
        }

        private static int RemapVertex(Dictionary<int, int> map, int globalIdx, Vector3[] verts, Vector3[] norms, Vector2[] uvs, Mesh mesh)
        {
            if (map.TryGetValue(globalIdx, out var local))
            {
                return local;
            }

            local = mesh.Vertices.Count;
            mesh.Vertices.Add(verts[globalIdx]);
            mesh.Normals.Add(norms[globalIdx]);
            mesh.TextureCoordinateChannels[0].Add(new Vector3(uvs[globalIdx], 0));
            map[globalIdx] = local;
            return local;
        }

        private static string? GetTexturePath(NestedFileSystemManager fsm, string modelPath, ulong modelId, Dictionary<ulong, string?> textures, ulong textureId)
        {
            if (textures.TryGetValue(textureId, out var path))
            {
                return path;
            }

            var parentFolder = Path.GetDirectoryName(modelPath);
            var rootFolder = Path.GetDirectoryName(Path.GetDirectoryName(parentFolder));
            var searchPath = Path.Combine(rootFolder, "tri", Path.GetFileName(parentFolder));
            foreach (var tri in fsm.EnumerateFiles(searchPath, "*.tri"))
            {
                var hash = StringCode.GetStrCode(tri.Path);
                if (hash == modelId)
                {
                    path = Path.Combine(tri.Path, textureId.ToString("x6", CultureInfo.InvariantCulture) + ".tm2");
                    if (fsm.FileExists(path))
                    {
                        path = Path.GetRelativePath(parentFolder, path);
                        break;
                    }

                    path = null;
                }
            }

            return textures[textureId] = path;
        }

        private static int EnsureMaterial(Scene scene, string texturePath, ulong modelId, ulong textureId, uint flags)
        {
            var name = $"{modelId:x6}_{textureId:x6}_{flags:x6}";
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
                    filePath: texturePath,
                    typeSemantic: TextureType.Diffuse,
                    texIndex: 0,
                    mapping: TextureMapping.FromUV,
                    uvIndex: 0,
                    blendFactor: 1f,
                    texOp: TextureOperation.Add,
                    wrapModeU: TextureWrapMode.Wrap,
                    wrapModeV: TextureWrapMode.Wrap,
                    flags: (int)TextureFlags.UseAlpha),
                IsTwoSided = true,
            };

            scene.Materials.Add(mat);
            return scene.Materials.Count - 1;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private struct StripDefinition
        {
            public uint Flags;
            public uint VertexCount;
            public ulong TextureId1;
            public ulong TextureId2;
            public ulong TextureId3;
            public ulong VertexOffset;
            public ulong NormalOffset;
            public ulong UV1Offset;
            public ulong UV2Offset;
            public ulong UV3Offset;
            public uint Pad1;
            public uint Pad2;
            public uint Pad3;
            public uint Pad4;
            public uint Pad5;
            public uint Pad6;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private struct Header
        {
            public uint Version;
            public uint PartCount;
            public uint BoneCount;
            public ulong Id;
            public uint UnknownB;
            public uint UnknownC;
            public Vector3 Min;
            public Vector3 Max;
            public Vector3 RelativeOrigin;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private struct MeshDefinition
        {
            public uint Flags;
            public uint MeshCount;
            public Vector3 Min;
            public Vector3 Max;
            public Vector3 RelativeOrigin;
            public int ParentIndex;
            public ulong DefinitionOffset;
            public uint Pad1;
            public uint Pad2;
            public uint Pad3;
            public uint Pad4;
            public uint Pad5;
        }
    }
}
