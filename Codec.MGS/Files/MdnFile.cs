namespace Codec.MGS.Files
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.IO;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using Assimp;
    using Codec.Archives;
    using Codec.Files;
    using Codec.Services;
    using Microsoft.Extensions.DependencyInjection;

    public class MdnFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton(new EntryTypeMatcher(EntryType.Model, "*.mdn"));

            services.AddSingleton<FileHandlerResolver<RenderableScene>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".mdn", StringComparison.OrdinalIgnoreCase))
                {
                    using var file = parent.File.OpenRead(parentRelativePath);
                    var header = file.ReadBigEndian<MdnHeader>();
                    if (Encoding.ASCII.GetString(header.Signature) != "MDN ")
                    {
                        return null;
                    }

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
            var scene = new Scene();
            var rootNode = new Node("root");
            scene.RootNode = rootNode;

            var header = stream.ReadBigEndian<MdnHeader>();
            stream.Position = header.BoneOffset;
            var bones = stream.ReadArrayBigEndian<MdnBone>(header.BoneCount);
            stream.Position = header.GroupOffset;
            var groups = stream.ReadArrayBigEndian<MdnGroup>(header.GroupCount);
            stream.Position = header.MeshOffset;
            var meshes = stream.ReadArrayBigEndian<MdnMesh>(header.MeshCount);
            stream.Position = header.FaceOffset;
            var faces = stream.ReadArrayBigEndian<MdnFace>(header.FaceCount);
            stream.Position = header.VertexDefinitionOffset;
            var vertexDefinitions = stream.ReadArrayBigEndian<MdnVertexDefinition>(header.VertexDefinitionCount);
            stream.Position = header.MaterialOffset;
            var materials = stream.ReadArrayBigEndian<MdnMaterial>(header.MaterialCount);
            stream.Position = header.TextureOffset;
            var textures = stream.ReadArrayBigEndian<MdnTexture>(header.TextureCount);
            stream.Position = header.SkinOffset;
            var skins = stream.ReadArrayBigEndian<MdnSkin>(header.SkinCount);
            stream.Position = header.VertexBufferOffset;
            var vertexBytes = new byte[header.VertexBufferSize];
            stream.ReadExactly(vertexBytes);
            stream.Position = header.FaceBufferOffset;
            var faceIndices = stream.ReadArrayBigEndian<ushort>(header.FaceBufferSize / sizeof(ushort));
            var vertexSpan = vertexBytes.AsSpan();

            var materialCache = new Dictionary<uint, int>();
            int EnsureMaterial(uint matGroup)
            {
                if (materialCache.TryGetValue(matGroup, out var existing))
                {
                    return existing;
                }

                var material = materials[(int)matGroup];

                var assimpMaterial = new Material
                {
                    Name = $"{material.Id:x6}_{material.Flags:x4}_{material.Shader:x4}",
                };

                for (var tx = 0; tx < material.TextureCount; tx++)
                {
                    var textureType = tx switch
                    {
                        0 => TextureType.Diffuse,
                        1 => TextureType.Normals,
                        2 => TextureType.Specular,
                        3 => TextureType.Metalness,
                        4 => TextureType.Reflection,
                        5 => TextureType.Emissive,
                    };

                    if (material.Texture[tx] < textures.Length)
                    {
                        var texturePath = $"{header.Id:x6}.txn/{textures[(int)material.Texture[tx]].Id:x6}.dds";
                        assimpMaterial.AddMaterialTexture(new TextureSlot(
                            filePath: texturePath,
                            typeSemantic: textureType,
                            texIndex: 0,
                            mapping: TextureMapping.FromUV,
                            uvIndex: tx,
                            blendFactor: 1f,
                            texOp: TextureOperation.Add,
                            wrapModeU: TextureWrapMode.Wrap,
                            wrapModeV: TextureWrapMode.Wrap,
                            flags: textureType == TextureType.Diffuse ? (int)TextureFlags.UseAlpha : 0));
                    }
                }

                scene.Materials.Add(assimpMaterial);
                var index = scene.Materials.Count - 1;
                materialCache[matGroup] = index;
                return index;
            }

            var boneNodes = Array.ConvertAll(bones, bone =>
            {
                var origin = bone.ParentPos;
                var boneNode = new Node($"bone_{bone.Id:x6}")
                {
                    Transform = Matrix4x4.Transpose(Matrix4x4.CreateTranslation(origin.X, origin.Y, origin.Z)),
                };
                boneNode.Metadata["BoneFlags"] = new Metadata.Entry(MetaDataType.Int32, (int)bone.Flags);
                return boneNode;
            });
            for (var b = 0; b < bones.Length; b++)
            {
                var parent = bones[b].Parent >= 0 && bones[b].Parent < boneNodes.Length ? boneNodes[bones[b].Parent] : rootNode;
                parent.Children.Add(boneNodes[b]);
            }

            var groupNodes = Array.ConvertAll(groups, group =>
            {
                var groupNode = new Node($"group_{group.Id:x6}");
                groupNode.Metadata["GroupFlags"] = new Metadata.Entry(MetaDataType.Int32, (int)group.Flags);
                return groupNode;
            });
            for (var g = 0; g < groups.Length; g++)
            {
                var parent = groups[g].Parent >= 0 && groups[g].Parent < groupNodes.Length ? groupNodes[groups[g].Parent] : rootNode;
                parent.Children.Add(groupNodes[g]);
            }

            for (var m = 0; m < meshes.Length; m++)
            {
                var mesh = meshes[m];
                var group = groupNodes[(int)mesh.GroupIdx];
                var vertexDefinition = vertexDefinitions[(int)mesh.VertexDefIdx];
                var skin = header.SkinCount == 0 ? default : skins[(int)mesh.SkinIdx];
                var numVertex = (int)mesh.NumVertex;

                var vertexStart = (int)vertexDefinition.Offset;
                var stride = (int)vertexDefinition.Stride;
                var numDefinitions = (int)vertexDefinition.DefinitionCount;

                var positions = new Vector3[numVertex];
                Vector2[]?[]? textureCoords = null;
                Vector3[]? normals = null;
                Vector4[]? weights = null;
                (byte A, byte B, byte C, byte D)[]? boneIndices = null;

                for (var v = 0; v < numVertex; v++)
                {
                    var vertexOffset = vertexStart + (v * stride);
                    for (var d = 0; d < numDefinitions; d++)
                    {
                        var definitionAndType = vertexDefinition.Definition[d];
                        var definition = (Definition)(definitionAndType & 0x0F);
                        var fieldOffset = vertexOffset + vertexDefinition.Position[d];
                        var span = vertexSpan[fieldOffset..];

                        switch (definition)
                        {
                            case Definition.Position:
                                positions[v] = new Vector3(
                                    BinaryPrimitives.ReadSingleBigEndian(span),
                                    BinaryPrimitives.ReadSingleBigEndian(span[4..]),
                                    BinaryPrimitives.ReadSingleBigEndian(span[8..]));
                                break;

                            case Definition.Weight:
                                (weights ??= new Vector4[numVertex])[v] = new Vector4(
                                    span[0] / 255f,
                                    span[1] / 255f,
                                    span[2] / 255f,
                                    span[3] / 255f);
                                break;

                            case Definition.Normal:
                                static Vector3 ReadDirection(ReadOnlySpan<byte> span, byte definition)
                                {
                                    var type = definition >> 4;
                                    if (type == 0x01)
                                    {
                                        return new Vector3(
                                            BinaryPrimitives.ReadSingleBigEndian(span),
                                            BinaryPrimitives.ReadSingleBigEndian(span[4..]),
                                            BinaryPrimitives.ReadSingleBigEndian(span[8..]));
                                    }
                                    else if (type == 0x05)
                                    {
                                        return new Vector3(
                                            BinaryPrimitives.ReadInt16BigEndian(span) / 4096f,
                                            BinaryPrimitives.ReadInt16BigEndian(span[2..]) / 4096f,
                                            BinaryPrimitives.ReadInt16BigEndian(span[4..]) / 4096f);
                                    }
                                    else if (type == 0x0A)
                                    {
                                        static int SignExtend(int value, int bits)
                                        {
                                            var signBit = 1 << (bits - 1);
                                            return (value & signBit) != 0 ? value - (1 << bits) : value;
                                        }

                                        var bits = BinaryPrimitives.ReadUInt32BigEndian(span);
                                        var ax = SignExtend((int)(bits & 0x7FF), 11);
                                        var ay = SignExtend((int)((bits >> 11) & 0x7FF), 11);
                                        var az = SignExtend((int)((bits >> 22) & 0x3FF), 10);
                                        return new Vector3(ax / 1023f, ay / 1023f, az / 511f);
                                    }
                                    else
                                    {
                                        return new Vector3(
                                            (sbyte)span[0] / 127f,
                                            (sbyte)span[1] / 127f,
                                            (sbyte)span[2] / 127f);
                                    }
                                }

                                (normals ??= new Vector3[numVertex])[v] = ReadDirection(span, definitionAndType);
                                break;

                            case Definition.Colour:
                                break;

                            case Definition.Texture3Ds:
                                break;

                            case Definition.BoneIndex:
                                (boneIndices ??= new (byte A, byte B, byte C, byte D)[numVertex])[v] = (span[0], span[1], span[2], span[3]);
                                break;

                            case Definition.Texture00:
                            case Definition.Texture01:
                            case Definition.Texture02:
                            case Definition.Texture03:
                            case Definition.Texture04:
                            case Definition.Texture05:
                                var channel = definition - Definition.Texture00;
                                if (channel != 0)
                                {
                                    break;
                                }

                                ((textureCoords ??= new Vector2[]?[6])[channel] ??= new Vector2[numVertex])[v] = new Vector2(
                                    (float)BinaryPrimitives.ReadHalfBigEndian(span),
                                    1 - (float)BinaryPrimitives.ReadHalfBigEndian(span[2..]));
                                break;

                            case Definition.Tangent:
                                break;

                            default:
                                // TODO: Logging.
                                break;
                        }
                    }
                }

                var trisByMaterial = new Dictionary<int, List<(int A, int B, int C)>>();
                var faceEnd = (int)(mesh.FaceIdx + mesh.NumFaceIdx);
                for (var f = (int)mesh.FaceIdx; f < faceEnd; f++)
                {
                    var face = faces[f];
                    var indexOffset = (int)face.Offset / sizeof(ushort);
                    var materialIndex = EnsureMaterial(face.MatGroup);
                    if (!trisByMaterial.TryGetValue(materialIndex, out var tris))
                    {
                        trisByMaterial[materialIndex] = tris = [];
                    }

                    for (var i = 0; i < face.Count; i += 3)
                    {
                        var i0 = faceIndices[indexOffset + i + 0];
                        var i1 = faceIndices[indexOffset + i + 1];
                        var i2 = faceIndices[indexOffset + i + 2];
                        tris.Add((i0, i2, i1));
                    }
                }

                var meshIndices = new List<int>();
                foreach (var (materialIndex, tris) in trisByMaterial)
                {
                    if (tris.Count == 0)
                    {
                        continue;
                    }

                    var assimpMesh = new Mesh($"{group.Name}_node{m}_mat{materialIndex}", PrimitiveType.Triangle)
                    {
                        MaterialIndex = materialIndex,
                    };
                    assimpMesh.UVComponentCount[0] = 2;

                    var indexRemap = new Dictionary<int, int>();
                    var boneLookup = new Dictionary<int, Bone>();
                    void AddBoneWeight(byte localBoneIdx, float weight, int vertexIndex)
                    {
                        if (weight <= 0f || localBoneIdx >= skin.Count)
                        {
                            return;
                        }

                        var globalBoneIdx = skin.BoneId[localBoneIdx];
                        if (globalBoneIdx >= boneNodes.Length)
                        {
                            return;
                        }

                        if (!boneLookup.TryGetValue(globalBoneIdx, out var bone))
                        {
                            var worldPos = bones[globalBoneIdx].WorldPos;
                            bone = new Bone
                            {
                                Name = $"bone_{bones[globalBoneIdx].Id:x6}",
                                OffsetMatrix = Matrix4x4.Transpose(Matrix4x4.CreateTranslation(-worldPos.X, -worldPos.Y, -worldPos.Z)),
                            };
                            assimpMesh.Bones.Add(bone);
                            boneLookup[globalBoneIdx] = bone;
                        }

                        bone.VertexWeights.Add(new VertexWeight(vertexIndex, weight));
                    }

                    int RemapVertex(int globalIndex)
                    {
                        if (indexRemap.TryGetValue(globalIndex, out var localIndex))
                        {
                            return localIndex;
                        }

                        localIndex = assimpMesh.Vertices.Count;
                        assimpMesh.Vertices.Add(positions[globalIndex]);

                        if (normals != null)
                        {
                            assimpMesh.Normals.Add(normals[globalIndex]);
                        }

                        if (textureCoords != null)
                        {
                            for (var i = 0; i < 6; i++)
                            {
                                if (textureCoords[i] is Vector2[] channel)
                                {
                                    assimpMesh.TextureCoordinateChannels[i].Add(new Vector3(channel[globalIndex], 0));
                                }
                            }
                        }

                        if (weights != null && boneIndices != null)
                        {
                            var (a, b, c, d) = boneIndices[globalIndex];
                            var w = weights[globalIndex];
                            AddBoneWeight(a, w.X, localIndex);
                            AddBoneWeight(b, w.Y, localIndex);
                            AddBoneWeight(c, w.Z, localIndex);
                            AddBoneWeight(d, w.W, localIndex);
                        }

                        indexRemap[globalIndex] = localIndex;
                        return localIndex;
                    }

                    foreach (var (i0, i1, i2) in tris)
                    {
                        assimpMesh.Faces.Add(new Face(
                        [
                            RemapVertex(i0),
                            RemapVertex(i1),
                            RemapVertex(i2),
                        ]));
                    }

                    scene.Meshes.Add(assimpMesh);
                    meshIndices.Add(scene.Meshes.Count - 1);
                }

                var node = new Node($"{group.Name}_node{m}");
                node.Metadata["MeshFlags"] = new Metadata.Entry(MetaDataType.Int32, (int)mesh.Flags);
                node.MeshIndices.AddRange(meshIndices);
                group.Children.Add(node);
            }

            return scene;
        }

        [InlineArray(4)]
        private struct Name4
        {
            public byte Char0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MdnHeader
        {
            public Name4 Signature;
            public uint Id;
            public uint BoneCount;
            public uint GroupCount;
            public uint MeshCount;
            public uint FaceCount;
            public uint VertexDefinitionCount;
            public uint MaterialCount;
            public uint TextureCount;
            public uint SkinCount;
            public uint BoneOffset;
            public uint GroupOffset;
            public uint MeshOffset;
            public uint FaceOffset;
            public uint VertexDefinitionOffset;
            public uint MaterialOffset;
            public uint TextureOffset;
            public uint SkinOffset;
            public uint VertexBufferOffset;
            public uint VertexBufferSize;
            public uint FaceBufferOffset;
            public uint FaceBufferSize;
            public uint Pad;
            public uint FileSize;
            public Vector4 Max;
            public Vector4 Min;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MdnBone
        {
            public uint Id;
            public uint Flags;
            public int Parent;
            public uint Unknown;
            public Vector4 ParentPos;
            public Vector4 WorldPos;
            public Vector4 Max;
            public Vector4 Min;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MdnGroup
        {
            public uint Id;
            public uint Flags;
            public uint Parent;
            public uint Pad;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MdnMesh
        {
            public uint GroupIdx;
            public uint Flags;
            public uint NumFaceIdx;
            public uint FaceIdx;
            public uint VertexDefIdx;
            public uint SkinIdx;
            public uint NumVertex;
            public uint Pad;
            public Vector4 Max;
            public Vector4 Min;
            public Vector4 Pos;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MdnFace
        {
            public ushort Type;
            public ushort Count;
            public uint Offset;
            public uint MatGroup;
            public ushort Start;
            public ushort Size;
        }

        private enum Definition : byte
        {
            Position,
            Weight,
            Normal,
            Colour,
            Texture3Ds = 5,
            BoneIndex = 7,
            Texture00,
            Texture01,
            Texture02,
            Texture03,
            Texture04,
            Texture05,
            Tangent,
        }

        [InlineArray(16)]
        private struct Index16
        {
            public byte Index0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MdnVertexDefinition
        {
            public uint Pad;
            public uint DefinitionCount;
            public uint Stride;
            public uint Offset;
            public Index16 Definition;
            public Index16 Position;
        }

        [InlineArray(8)]
        private struct Id8
        {
            public uint Id0;
        }

        [InlineArray(4)]
        private struct Short4
        {
            public short X;
        }

        [InlineArray(8)]
        private struct Params8
        {
            public Short4 Param0;
        }

        private enum MaterialFlags : ushort
        {
            UseUV1 = 0x1,
            NoSpecular = 0x2,
            HasEnv = 0x4,
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MdnMaterial
        {
            public ushort Flags;
            public ushort Shader;
            public uint Id;
            public uint TextureCount;
            public uint NumParams;
            public Id8 Texture;
            public Params8 Params;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MdnTexture
        {
            public uint Id;
            public uint Flags;
            public float ScaleU;
            public float ScaleV;
            public float PosU;
            public float PosV;
            public ulong Pad;
        }

        [InlineArray(32)]
        private struct Index32
        {
            public byte Index0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MdnSkin
        {
            public uint Unknown;
            public ushort Count;
            public ushort Pad;
            public Index32 BoneId;
        }
    }
}
