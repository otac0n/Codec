namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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
        private const float PositionScale = 1f / 16f;
        private const float NormalScale = 1f / 127f;
        private const float TexCoordScale = 1f / 4096f;

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
            var meshDef = fileSpan[(int)header.BaseOffset..].CastWithEndianness<MeshDef>(1, Endianness.BigEndian)[0];

            var tableHeaders = fileSpan[(int)(header.BaseOffset + meshDef.MeshTableOffset)..].CastWithEndianness<TableHeader>(meshDef.MeshTable1Count, Endianness.BigEndian);
            var vertices = fileSpan[(int)(header.BaseOffset + header.VertexOffset)..].CastWithEndianness<Short3>(header.VertexCount, Endianness.BigEndian);
            var normals = fileSpan[(int)(header.BaseOffset + header.NormalOffset)..].CastWithEndianness<SByte3>(header.NormalCount, Endianness.BigEndian);
            var texCoords = fileSpan[(int)(header.BaseOffset + header.TexCoordOffset)..].CastWithEndianness<Short2>(header.TexCoordCount, Endianness.BigEndian);

            var scene = new Scene();
            var rootNode = new Node("root");
            scene.RootNode = rootNode;
            for (var t = 0; t < meshDef.MeshTable1Count; t++)
            {
                var tableHeader = tableHeaders[t];
                var entries = fileSpan[(int)(header.BaseOffset + tableHeader.EntryOffset)..].CastWithEndianness<TableEntry>(tableHeader.EntryCount, Endianness.BigEndian);
                var meshesByTexture = new Dictionary<uint, (Mesh Mesh, Dictionary<(ushort Position, ushort Normal, ushort TexCoord), int> Remap)>();
                for (var i = 0; i < tableHeader.EntryCount; i++)
                {
                    var mesh = entries[i];
                    if (!meshesByTexture.TryGetValue(mesh.TextureId, out var entry))
                    {
                        var assimpMesh = new Mesh($"part{t}_tex{mesh.TextureId:x6}", PrimitiveType.Triangle)
                        {
                            MaterialIndex = EnsureMaterial(scene, mesh.TextureId),
                        };
                        assimpMesh.UVComponentCount[0] = 2;
                        entry = (assimpMesh, new Dictionary<(ushort, ushort, ushort), int>());
                        meshesByTexture[mesh.TextureId] = entry;
                    }

                    var faces = fileSpan[(int)(header.BaseOffset + mesh.FacesTableOffset)..].CastWithEndianness<FaceEntry>(mesh.FaceCount, Endianness.BigEndian);
                    for (var j = 0; j < mesh.FaceCount; j++)
                    {
                        AddFace(fileSpan, bytes.Length, header.BaseOffset, mesh, faces[j], vertices, normals, texCoords, entry.Remap, entry.Mesh);
                    }
                }

                var node = new Node($"part{t}");
                foreach (var entry in meshesByTexture.Values)
                {
                    if (entry.Mesh.Faces.Count == 0)
                    {
                        continue;
                    }

                    scene.Meshes.Add(entry.Mesh);
                    node.MeshIndices.Add(scene.Meshes.Count - 1);
                }

                if (node.MeshIndices.Count > 0)
                {
                    rootNode.Children.Add(node);
                }
            }

            return scene;
        }

        private static void AddFace(
            Span<byte> fileSpan,
            int fileLength,
            uint baseOffset,
            TableEntry mesh,
            FaceEntry face,
            Span<Short3> vertices,
            Span<SByte3> normals,
            Span<Short2> texCoords,
            Dictionary<(ushort Position, ushort Normal, ushort TexCoord), int> vertexRemap,
            Mesh assimpMesh)
        {
            if (face.VertexCount < 3)
            {
                return;
            }

            var posIndices = ExpandStripIndices(fileSpan, baseOffset + face.IndexBufferOffset, face.VertexCount);
            var normIndices = ExpandStripIndices(fileSpan, baseOffset + face.NormalIndexBufferOffset, face.VertexCount);
            var uvIndices = GetTexCoordIndices(fileSpan, fileLength, baseOffset, mesh, face);

            var triangleCount = Math.Min(posIndices.Count, Math.Min(normIndices.Count, uvIndices.Count));
            for (var k = 0; k + 2 < triangleCount; k += 3)
            {
                if (!TryRemapVertex(vertexRemap, posIndices[k], normIndices[k], uvIndices[k], vertices, normals, texCoords, assimpMesh, out var a) ||
                    !TryRemapVertex(vertexRemap, posIndices[k + 1], normIndices[k + 1], uvIndices[k + 1], vertices, normals, texCoords, assimpMesh, out var b) ||
                    !TryRemapVertex(vertexRemap, posIndices[k + 2], normIndices[k + 2], uvIndices[k + 2], vertices, normals, texCoords, assimpMesh, out var c))
                {
                    continue;
                }

                assimpMesh.Faces.Add(new Face([a, b, c]));
            }
        }

        private static List<ushort> ExpandStripIndices(Span<byte> fileSpan, uint offset, ushort count)
        {
            var raw = fileSpan[(int)offset..].CastWithEndianness<ushort>(count, Endianness.BigEndian);
            var output = new List<ushort>(count < 3 ? count : 3 + ((count - 3) * 3));
            for (var i = 0; i < count; i++)
            {
                if (i < 3)
                {
                    output.Add(raw[i]);
                }
                else
                {
                    var last = output.Count - 1;
                    output.Add(output[last - 1]);
                    output.Add(output[last]);
                    output.Add(raw[i]);
                }
            }

            return output;
        }

        private static List<ushort> GetTexCoordIndices(Span<byte> fileSpan, int fileLength, uint baseOffset, TableEntry mesh, FaceEntry face)
        {
            var output = new List<ushort>();

            if (face.TexCoordBufferOffset != 0)
            {
                var nodeIndices = ExpandStripIndices(fileSpan, baseOffset + face.TexCoordBufferOffset, face.VertexCount);
                foreach (var nodeIndex in nodeIndices)
                {
                    var nodeOffset = baseOffset + mesh.TexCoordOffset1 + ((uint)nodeIndex * sizeof(ushort));
                    output.Add(fileSpan[(int)nodeOffset..].CastWithEndianness<ushort>(1, Endianness.BigEndian)[0]);
                }
            }
            else
            {
                var nodeOffset = baseOffset + mesh.TexCoordOffset1;
                var uvIndex = fileSpan[(int)nodeOffset..].CastWithEndianness<ushort>(1, Endianness.BigEndian)[0];
                var repeatCount = Math.Max(0, (face.VertexCount - 2) * 3);
                for (var k = 0; k < repeatCount; k++)
                {
                    output.Add(uvIndex);
                }
            }

            return output;
        }

        private static bool TryRemapVertex(
            Dictionary<(ushort Position, ushort Normal, ushort TexCoord), int> map,
            ushort positionIndex,
            ushort normalIndex,
            ushort texCoordIndex,
            Span<Short3> vertices,
            Span<SByte3> normals,
            Span<Short2> texCoords,
            Mesh assimpMesh,
            out int index)
        {
            if (positionIndex >= vertices.Length || normalIndex >= normals.Length || texCoordIndex >= texCoords.Length)
            {
                index = -1;
                return false;
            }

            var key = (positionIndex, normalIndex, texCoordIndex);
            if (map.TryGetValue(key, out index))
            {
                return true;
            }

            index = assimpMesh.Vertices.Count;

            var pos = vertices[positionIndex];
            assimpMesh.Vertices.Add(new Vector3(pos[0], pos[1], pos[2]) * PositionScale);

            var norm = normals[normalIndex];
            assimpMesh.Normals.Add(new Vector3(norm[0], -norm[1], norm[2]) * NormalScale);

            var uv = texCoords[texCoordIndex];
            assimpMesh.TextureCoordinateChannels[0].Add(new Vector3(uv[0] * TexCoordScale, 1f - (uv[1] * TexCoordScale), 0));

            map[key] = index;
            return true;
        }

        private static int EnsureMaterial(Scene scene, uint textureId)
        {
            var name = $"{textureId:x6}";
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
                IsTwoSided = true,
            };

            var texturePath = $"{textureId:x6}.tpl";
            if (texturePath is not null)
            {
                mat.TextureDiffuse = new TextureSlot(
                    filePath: texturePath,
                    typeSemantic: TextureType.Diffuse,
                    texIndex: 0,
                    mapping: TextureMapping.FromUV,
                    uvIndex: 0,
                    blendFactor: 1f,
                    texOp: TextureOperation.Add,
                    wrapModeU: TextureWrapMode.Wrap,
                    wrapModeV: TextureWrapMode.Wrap,
                    flags: (int)TextureFlags.UseAlpha);
            }

            scene.Materials.Add(mat);
            return scene.Materials.Count - 1;
        }

        private static bool InBounds(int fileLength, long offset, long size) =>
            offset >= 0 && size >= 0 && offset + size <= fileLength;

        [InlineArray(3)]
        private struct Short3
        {
            public short Axis0;
        }

        [InlineArray(2)]
        private struct Short2
        {
            public short Axis0;
        }

        [InlineArray(3)]
        private struct SByte3
        {
            public sbyte Axis0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public uint Unknown1;
            public uint PartCount;
            public uint BoneCount;
            public uint Unknown2;
            public uint Id;
            public uint Unknown3;
            public Vector3 Min;
            public Vector3 Max;
            public Vector3 Origin;
            public ushort PartCountAlmost;
            public ushort BoneCountAlmost;
            public ushort UnknownCount;
            public ushort VertexCount;
            public ushort NormalCount;
            public ushort TexCoordCount;
            public uint Pad;
            public uint UnknownOffset;
            public uint VertexOffset;
            public uint NormalOffset;
            public uint TexCoordOffset;
            public uint BaseOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MeshDef
        {
            public uint Unknown1;
            public uint Unknown2;
            public ushort MeshTable1Count;
            public ushort MeshTable2Count;
            public uint UnknownCount;
            public uint MeshTableOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TableHeader
        {
            public uint Unknown1;
            public uint Unknown2;
            public uint Unknown3;
            public uint Unknown4;
            public uint Unknown5;
            public uint Unknown6;
            public uint Unknown7;
            public uint Unknown8;
            public uint Unknown9;
            public uint Unknown10;
            public uint Unknown11;
            public uint Unknown12;
            public uint Unknown13;
            public uint Unknown14;
            public ushort EntryCount;
            public ushort Unknown15;
            public uint Unknown16;
            public ushort UnknownCount;
            public ushort Unknown18;
            public uint EntryOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TableEntry
        {
            public uint UnknownOffset;
            public uint TextureId;
            public uint Unknown1;
            public uint Unknown2;
            public ushort FaceCount;
            public ushort NodeCount;
            public uint TexCoordOffset1;
            public uint TexCoordOffset2;
            public uint UnknownCount;
            public uint FacesTableOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FaceEntry
        {
            public uint IndexBufferOffset;
            public uint TexCoordBufferOffset;
            public uint NormalIndexBufferOffset;
            public uint Unknown1;
            public uint Unknown2;
            public uint Unknown3;
            public uint Unknown4;
            public ushort VertexCount;
            public ushort Unknown5;
        }
    }
}
