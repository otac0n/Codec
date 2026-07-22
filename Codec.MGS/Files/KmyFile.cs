namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Assimp;
    using Codec.Archives;
    using Codec.Files;
    using Codec.Services;
    using Microsoft.Extensions.DependencyInjection;

    public class KmyFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton(new EntryTypeMatcher(EntryType.Model, "*.kmy"));

            services.AddSingleton<FileHandlerResolver<RenderableScene>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".kmy", StringComparison.OrdinalIgnoreCase))
                {
                    return new((fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var file = parent.File.OpenRead(parentRelativePath);
                        return (RenderableScene)FromStream(serviceProvider.GetRequiredService<NestedFileSystemManager>(), fullPath, file);
                    });
                }

                return null;
            });
        }

        public static Scene FromStream(NestedFileSystemManager fsm, string fullPath, Stream stream)
        {
            var bytes = new byte[stream.Length];
            var fileSpan = bytes.AsSpan();
            stream.ReadExactly(fileSpan);

            var header = fileSpan.CastWithEndianness<Header>(1, Endianness.BigEndian)[0];
            var meshDefs = fileSpan[(int)header.BaseOffset..].CastWithEndianness<MeshDef>(header.MeshCount, Endianness.BigEndian);
            var boneDefs = fileSpan[Marshal.SizeOf<Header>()..].CastWithEndianness<BoneDef>(((int)header.BaseOffset - Marshal.SizeOf<Header>()) / Marshal.SizeOf<BoneDef>(), Endianness.BigEndian);

            var vertexData = fileSpan[(int)(header.BaseOffset + header.VertexOffset)..].CastWithEndianness<Short3>(header.VertexCount, Endianness.BigEndian);
            var normalData = fileSpan[(int)(header.BaseOffset + header.NormalOffset)..].CastWithEndianness<SByte3>(header.NormalCount, Endianness.BigEndian);
            var textureCoordData1 = fileSpan[(int)(header.BaseOffset + header.TexCoordOffset)..].CastWithEndianness<Short2>(header.TexCoordCount, Endianness.BigEndian);
            var weightData = fileSpan[(int)(header.BaseOffset + header.SkinOffset)..].CastWithEndianness<BoneWeight>(header.SkinCount, Endianness.BigEndian);

            var scene = new Scene();
            var rootNode = new Node("root");
            scene.RootNode = rootNode;

            var bones = new Node[boneDefs.Length];
            if (boneDefs.Length > 0)
            {
                var skeletonNode = new Node("skeleton");
                rootNode.Children.Add(skeletonNode);
                for (var b = 0; b < boneDefs.Length; b++)
                {
                    var boneDef = boneDefs[b];
                    var relativeNode = boneDef.ParentIndex != -1 && !(boneDef.ParentIndex == 0 && b == 0) ? bones[boneDef.ParentIndex] : skeletonNode;
                    relativeNode.Children.Add(bones[b] = new Node($"bone{b}")
                    {
                        // Hypothesis: bones are translation-only joints (no rotation data found in BoneDef).
                        Transform = Matrix4x4.Transpose(Matrix4x4.CreateTranslation(boneDef.LocalOffset)),
                    });
                }
            }

            var materialCache = new Dictionary<(uint NormalTextureId, uint DiffuseTextureId, uint SpecularTextureId), int>();
            for (var m = 0; m < header.MeshCount; m++)
            {
                var meshDef = meshDefs[m];
                var meshNode = new Node($"mesh{m}");
                rootNode.Children.Add(meshNode);
                var nodes = new List<Node>(meshDef.ObjectCount);
                var objectDefs = fileSpan[(int)(header.BaseOffset + meshDef.ObjectTableOffset)..].CastWithEndianness<ObjectDef>(meshDef.ObjectCount, Endianness.BigEndian);
                for (var p = 0; p < objectDefs.Length; p++)
                {
                    var objectDef = objectDefs[p];
                    var submeshes = fileSpan[(int)(header.BaseOffset + objectDef.SubmeshTableOffset)..].CastWithEndianness<SubmeshDef>(objectDef.SubmeshCount, Endianness.BigEndian);
                    var meshIndices = new List<int>();
                    foreach (var submesh in submeshes)
                    {
                        var faceDefs = fileSpan[(int)(header.BaseOffset + submesh.FaceTableOffset)..].CastWithEndianness<FaceDef>(submesh.FaceCount, Endianness.BigEndian);
                        var totalVertices = 0u;
                        for (var d = 0; d < faceDefs.Length; d++)
                        {
                            totalVertices += faceDefs[d].VertexCount;
                        }

                        var vertices = new Vector3[totalVertices];
                        var normals = new Vector3[totalVertices];
                        var textureCoords = new Vector2[totalVertices];
                        var vertexBoneWeights = new List<(int BoneIndex, float Weight)>?[totalVertices];
                        var trisByMaterial = new Dictionary<(uint NormalTextureId, uint DiffuseTextureId, uint SpecularTextureId), List<(int A, int B, int C)>>();
                        var vertexOutputOffset = 0u;
                        for (var d = 0; d < faceDefs.Length; d++)
                        {
                            var faceDef = faceDefs[d];
                            var vertexCount = faceDef.VertexCount;
                            var vertexIndexData = fileSpan[(int)(header.BaseOffset + faceDef.VertexIndexBufferOffset)..].CastWithEndianness<ushort>(vertexCount, Endianness.BigEndian);
                            for (var v = 0; v < vertexCount; v++)
                            {
                                vertices[v + vertexOutputOffset] = new Vector3(
                                    vertexData[vertexIndexData[v]].X,
                                    vertexData[vertexIndexData[v]].Y,
                                    vertexData[vertexIndexData[v]].Z);
                            }

                            if (faceDef.NormalIndexBufferOffset != 0)
                            {
                                var normalIndexData = fileSpan[(int)(header.BaseOffset + faceDef.NormalIndexBufferOffset)..].CastWithEndianness<ushort>(vertexCount, Endianness.BigEndian);
                                for (var v = 0; v < vertexCount; v++)
                                {
                                    normals[v + vertexOutputOffset] = new(
                                        normalData[normalIndexData[v]].X / 64f,
                                        normalData[normalIndexData[v]].Y / 64f,
                                        normalData[normalIndexData[v]].Z / 64f);
                                }
                            }

                            if (faceDef.TexCoordBufferOffset != 0 && submesh.TexCoordOffset1 != 0)
                            {
                                var textureCoordIndexData = fileSpan[(int)(header.BaseOffset + faceDef.TexCoordBufferOffset)..].CastWithEndianness<ushort>(vertexCount, Endianness.BigEndian);
                                for (var v = 0; v < vertexCount; v++)
                                {
                                    var textureCoordData2 = fileSpan[(int)(header.BaseOffset + submesh.TexCoordOffset1 + (uint)textureCoordIndexData[v] * sizeof(ushort))..].CastWithEndianness<ushort>(1, Endianness.BigEndian)[0];
                                    textureCoords[vertexOutputOffset + v] = new(
                                        textureCoordData1[textureCoordData2].U / 4096f,
                                        1 - textureCoordData1[textureCoordData2].V / 4096f);
                                }
                            }

                            if (faceDef.BonePaletteIndexOffset != 0 && faceDef.SkinRecordIndexOffset != 0 && weightData.Length > 0)
                            {
                                // BonePaletteIndices: per-vertex, LOCAL index into this face's own list of distinct
                                // weight records (BoneAttachmentCount long) - not a direct global weight index.
                                var bonePaletteIndexData = fileSpan[(int)(header.BaseOffset + faceDef.BonePaletteIndexOffset)..].CastWithEndianness<ushort>(vertexCount, Endianness.BigEndian);

                                // SkinRecordIndexOffset: the face's local list of distinct weight records, each
                                // entry a global index into the header-level weightData ("Thing"/BoneWeight) table.
                                var thingIndexData = fileSpan[(int)(header.BaseOffset + faceDef.SkinRecordIndexOffset)..].CastWithEndianness<ushort>(faceDef.BoneAttachmentCount, Endianness.BigEndian);

                                for (var v = 0; v < vertexCount; v++)
                                {
                                    var localThingIndex = bonePaletteIndexData[v];
                                    if (localThingIndex >= thingIndexData.Length)
                                    {
                                        continue;
                                    }

                                    var globalWeightIndex = thingIndexData[localThingIndex];
                                    if (globalWeightIndex >= weightData.Length)
                                    {
                                        continue;
                                    }

                                    var record = weightData[globalWeightIndex];
                                    List<(int BoneIndex, float Weight)>? weights = null;
                                    for (var k = 0; k < 4; k++)
                                    {
                                        var boneIndex = record.Indices[k];
                                        var weight = record.Weights[k];
                                        if (boneIndex < 0 || weight == 0 || boneIndex >= boneDefs.Length)
                                        {
                                            continue;
                                        }

                                        weights ??= [];
                                        weights.Add((boneIndex, weight / 128f));
                                    }

                                    vertexBoneWeights[v + vertexOutputOffset] = weights;
                                }
                            }

                            var matKey = (submesh.NormalTextureId, submesh.DiffuseTextureId, submesh.SpecularTextureId);
                            if (!trisByMaterial.TryGetValue(matKey, out var tris))
                            {
                                trisByMaterial[matKey] = tris = [];
                            }

                            ExpandStrip(tris, (int)vertexOutputOffset, vertexCount);

                            vertexOutputOffset += faceDef.VertexCount;
                        }

                        foreach (var (matKey, tris) in trisByMaterial)
                        {
                            if (tris.Count == 0)
                            {
                                continue;
                            }

                            var assimpMesh = new Mesh($"{header.ModelId:x}_node{p}_{matKey.NormalTextureId:x6}_{matKey.DiffuseTextureId:x6}_{matKey.SpecularTextureId:x6}", PrimitiveType.Triangle)
                            {
                                MaterialIndex = EnsureMaterial(scene, header.TexturePackageId, matKey, materialCache),
                            };
                            assimpMesh.UVComponentCount[0] = 2;

                            var indexRemap = new Dictionary<int, int>();
                            var boneMap = new Dictionary<int, Bone>();
                            var fallbackBoneIndex = objectDef.ParentIndex >= 0 && objectDef.ParentIndex < boneDefs.Length ? objectDef.ParentIndex : -1;
                            foreach (var (i0, i1, i2) in tris)
                            {
                                assimpMesh.Faces.Add(new Face([
                                    RemapVertex(indexRemap, i0, vertices, normals, textureCoords, vertexBoneWeights, boneMap, boneDefs, bones, fallbackBoneIndex, assimpMesh),
                                RemapVertex(indexRemap, i1, vertices, normals, textureCoords, vertexBoneWeights, boneMap, boneDefs, bones, fallbackBoneIndex, assimpMesh),
                                RemapVertex(indexRemap, i2, vertices, normals, textureCoords, vertexBoneWeights, boneMap, boneDefs, bones, fallbackBoneIndex, assimpMesh),
                            ]));
                            }

                            assimpMesh.Bones.AddRange(boneMap.Values);

                            scene.Meshes.Add(assimpMesh);
                            meshIndices.Add(scene.Meshes.Count - 1);
                        }
                    }

                    // Skinning now carries all positional data (vertices are stored in raw world/file space).
                    // Keep object nodes flat with identity transforms so their global transform contributes
                    // nothing extra to the glTF skin math (see inverse(meshNodeGlobalTransform) in the spec).
                    var node = new Node($"node{p}")
                    {
                        Transform = Matrix4x4.Identity,
                    };
                    node.MeshIndices.AddRange(meshIndices);
                    nodes.Add(node);
                    meshNode.Children.Add(node);
                }
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

        private static int RemapVertex(
            Dictionary<int, int> map,
            int globalIdx,
            Vector3[] verts,
            Vector3[] norms,
            Vector2[] uvs,
            List<(int BoneIndex, float Weight)>?[] vertexBoneWeights,
            Dictionary<int, Bone> boneMap,
            ReadOnlySpan<BoneDef> boneDefs,
            Node[] boneNodes,
            int fallbackBoneIndex,
            Mesh mesh)
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

            // Vertices with no resolved weight would otherwise get a zero skin matrix (collapse to origin) -
            // fall back to full rigid weight on the object's own parent bone so they render in place at bind pose.
            var weights = vertexBoneWeights[globalIdx];
            if (weights == null && fallbackBoneIndex >= 0)
            {
                weights = [(fallbackBoneIndex, 1f)];
            }

            if (weights != null)
            {
                foreach (var (boneIndex, weight) in weights)
                {
                    if (!boneMap.TryGetValue(boneIndex, out var bone))
                    {
                        // Bones are translation-only joints and mesh nodes are now identity-transformed
                        // (vertices stored in world/file space), so mesh-space -> bone-space reduces to
                        // a plain inverse translation: OffsetMatrix = Translate(-boneDef.GlobalOffset).
                        var boneDef = boneDefs[boneIndex];
                        boneMap[boneIndex] = bone = new Bone
                        {
                            Name = boneNodes[boneIndex].Name,
                            OffsetMatrix = Matrix4x4.Transpose(Matrix4x4.CreateTranslation(-boneDef.GlobalOffset)),
                        };
                    }

                    bone.VertexWeights.Add(new VertexWeight(local, weight));
                }
            }

            return local;
        }

        private static int EnsureMaterial(
            Scene scene,
            uint texturePackageId,
            (uint NormalTextureId, uint DiffuseTextureId, uint SpecularTextureId) key,
            Dictionary<(uint NormalTextureId, uint DiffuseTextureId, uint SpecularTextureId), int> materialCache)
        {
            if (materialCache.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var mat = new Material
            {
                Name = $"{key.NormalTextureId:x6}_{key.DiffuseTextureId:x6}_{key.SpecularTextureId:x6}",
                TextureDiffuse = MakeTextureSlot(GetTexturePath(texturePackageId, key.DiffuseTextureId)!, TextureType.Diffuse),
            };

            if (key.NormalTextureId != 0)
            {
                mat.TextureNormal = MakeTextureSlot(GetTexturePath(texturePackageId, key.NormalTextureId)!, TextureType.Normals);
            }

            if (key.SpecularTextureId != 0)
            {
                mat.TextureSpecular = MakeTextureSlot(GetTexturePath(texturePackageId, key.SpecularTextureId)!, TextureType.Specular);
            }

            scene.Materials.Add(mat);
            var index = scene.Materials.Count - 1;
            materialCache[key] = index;
            return index;
        }

        private static string? GetTexturePath(uint texturePackageId, uint textureId) =>
            $"{texturePackageId:x6}.tpl/{textureId:x6}.tplx";

        private static TextureSlot MakeTextureSlot(string path, TextureType type) =>
            new(
                filePath: path,
                typeSemantic: type,
                texIndex: 0,
                mapping: TextureMapping.FromUV,
                uvIndex: 0,
                blendFactor: 1f,
                texOp: TextureOperation.Add,
                wrapModeU: TextureWrapMode.Wrap,
                wrapModeV: TextureWrapMode.Wrap,
                flags: type == TextureType.Diffuse ? (int)TextureFlags.UseAlpha : 0);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Short3
        {
            public short X;
            public short Y;
            public short Z;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Short2
        {
            public short U;
            public short V;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SByte3
        {
            public sbyte X;
            public sbyte Y;
            public sbyte Z;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public uint Sentinel1;
            public uint UnknownCount1;
            public uint FirstMeshObjectCount;
            public uint TexturePackageId;
            public uint ModelId;
            public uint UnknownCount2;
            public Vector3 Min;
            public Vector3 Max;
            public Vector3 Origin;
            public ushort SkinCount;
            public ushort UnknownCount3;
            public ushort MeshCount;
            public ushort VertexCount;
            public ushort NormalCount;
            public ushort TexCoordCount;
            public uint Pad;
            public uint SkinOffset;
            public uint VertexOffset;
            public uint NormalOffset;
            public uint TexCoordOffset;
            public uint BaseOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BoneDef
        {
            public uint Flags;
            public int ParentIndex;
            public Vector3 LocalOffset;
            public Vector3 GlobalOffset;
            public Vector3 Min;
            public float Padding1;
            public Vector3 Max;
            public float Padding2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MeshDef
        {
            public uint Unknown1;
            public uint Unknown2;
            public ushort ObjectCount;
            public ushort TotalSubmeshCount;
            public uint UnknownCount1;
            public uint ObjectTableOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ObjectDef
        {
            public uint Flags;
            public uint Padding1;
            public Vector3 Min;
            public Vector3 Max;
            public Vector3 LocalOffset;
            public Vector3 GlobalOffset;
            public ushort SubmeshCount;
            public short ParentIndex;
            public uint Sentinel1;
            public ushort NodeCount;
            public short OverrideIndex;
            public uint SubmeshTableOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SubmeshDef
        {
            public uint Unknown1;
            public uint NormalTextureId;
            public uint DiffuseTextureId;
            public uint SpecularTextureId;
            public ushort FaceCount;
            public ushort UnknownCount1;
            public uint TexCoordOffset1;
            public uint TexCoordOffset2;
            public uint UnknownCount2;
            public uint FaceTableOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FaceDef
        {
            public uint VertexIndexBufferOffset;
            public uint TexCoordBufferOffset;
            public uint NormalIndexBufferOffset;
            public uint TangentIndexOffset;
            public uint BinormalIndexOffset;
            public uint SkinRecordIndexOffset;
            public uint BonePaletteIndexOffset;
            public ushort VertexCount;
            public ushort BoneAttachmentCount;
        }

        [InlineArray(4)]
        private struct Weight4
        {
            public byte Weight0;
        }

        [InlineArray(4)]
        private struct Index4
        {
            public sbyte ID0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BoneWeight
        {
            public Weight4 Weights;
            public Index4 Indices;
        }
    }
}
