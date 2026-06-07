namespace Codec.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using Microsoft.Extensions.DependencyInjection;
    using Bounds = (System.Numerics.Vector3 Start, System.Numerics.Vector3 End);

    public class KmsFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileHandlerResolver<Model>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".kms", StringComparison.OrdinalIgnoreCase))
                {
                    return (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var file = parent.File.OpenRead(parentRelativePath);
                        return FromStream(file);
                    };
                }

                return null;
            });
        }

        public static Model2 FromStream(Stream stream)
        {
            var bytes = new byte[stream.Length];
            var fileSpan = bytes.AsSpan();
            stream.ReadExactly(fileSpan);
            var header = MemoryMarshal.Cast<byte, Header>(fileSpan)[0];
            var meshDefinitions = MemoryMarshal.Cast<byte, MeshDefinition>(fileSpan[64..])[0..(int)header.PartCount];
            var meshes = new List<Model2.Mesh2>();
            for (var p = 0; p < meshDefinitions.Length; p++)
            {
                var relativeMesh = meshDefinitions[p].ParentIndex != -1 && !(meshDefinitions[p].ParentIndex == 0 && p == 0) ? meshes[meshDefinitions[p].ParentIndex] : null;

                var stripDefinitions = MemoryMarshal.Cast<byte, StripDefinition>(fileSpan[(int)meshDefinitions[p].DefinitionOffset..])[0..(int)meshDefinitions[p].MeshCount];
                var totalVertices = 0u;
                for (var d = 0; d < stripDefinitions.Length; d++)
                {
                    totalVertices += stripDefinitions[d].VertexCount;
                }

                var vertices = new Vector3[totalVertices];
                var normals = new Vector3[totalVertices];
                var textureCoords = new List<Vector2>();
                var faces = new List<Model2.Mesh2.Face2>((int)meshDefinitions[p].MeshCount);
                var vertexOutputOffset = 0u;
                for (var d = 0; d < stripDefinitions.Length; vertexOutputOffset += stripDefinitions[d++].VertexCount)
                {
                    var vertexCount = stripDefinitions[d].VertexCount;
                    var vertexData = MemoryMarshal.Cast<byte, (short X, short Y, short Z, short W)>(fileSpan[(int)stripDefinitions[d].VertexOffset..])[0..(int)vertexCount];
                    var normalData = MemoryMarshal.Cast<byte, (short X, short Y, short Z, short W)>(fileSpan[(int)stripDefinitions[d].NormalOffset..])[0..(int)vertexCount];
                    for (var v = 0; v < vertexCount; v++)
                    {
                        vertices[v + vertexOutputOffset] = new(
                            vertexData[v].X / 4096f,
                            vertexData[v].Y / 4096f,
                            vertexData[v].Z / 4096f);
                        normals[v + vertexOutputOffset] = new(
                            normalData[v].X / 4096f,
                            normalData[v].Y / 4096f,
                            normalData[v].Z / 4096f);
                    }

                    var textureCoords1 = false;
                    if (stripDefinitions[d].UV1Offset != 0)
                    {
                        textureCoords1 = true;
                        var textureCoordData1 = MemoryMarshal.Cast<byte, (short U, short V)>(fileSpan[(int)stripDefinitions[d].UV1Offset..])[0..(int)vertexCount];
                        for (var v = 0; v < vertexCount; v++)
                        {
                            textureCoords.Add(new(
                                textureCoordData1[v].U / 4096f,
                                textureCoordData1[v].V / 4096f));
                        }
                    }

                    var strips = new List<(int Start, int Length)>();

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
                                strips.Add(new(startIndex, v - startIndex));
                            }
                        }

                        lastInclude = include;
                    }

                    if (lastInclude)
                    {
                        strips.Add(new(startIndex, (int)(vertexCount - startIndex)));
                    }

                    foreach (var (stripBegin, stripLength) in strips)
                    {
                        var indices = Enumerable.Range((int)(vertexOutputOffset + stripBegin), stripLength).Select(i => (uint)i).ToArray();
                        var txIndices = textureCoords1 ? Enumerable.Range((int)(textureCoords.Count - vertexCount + stripBegin), stripLength).Select(i => (uint)i).ToArray() : null;
                        faces.Add(new Model2.Mesh2.Face2(
                            (header.Id, stripDefinitions[d].TextureId1),
                            indices,
                            indices,
                            txIndices));
                    }
                }

                meshes.Add(
                    new Model2.Mesh2(
                        meshDefinitions[p].Flags,
                        (meshDefinitions[p].Min, meshDefinitions[p].Max),
                        meshDefinitions[p].RelativeOrigin,
                        relativeMesh,
                        vertices,
                        normals,
                        [.. textureCoords],
                        [.. faces]));
            }

            return new Model2((header.Min, header.Max), [.. meshes]);
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

        public class Model2(Bounds bounds, Model2.Mesh2[] meshes)
            : Model(meshes)
        {
            public new Mesh2[] Meshes { get; } = meshes;

            public class Mesh2(uint flags, Bounds bounds, Vector3 relativeOrigin, Mesh? relativeMesh, Vector3[] relativeVertices, Vector3[] normals, Vector2[] textureCoords, Mesh2.Face2[] faces)
                : Mesh(relativeOrigin, relativeVertices, textureCoords, normals, faces, relativeMesh)
            {
                public uint Flags { get; } = flags;

                public new Face2[] Faces { get; } = faces;

                public class Face2((ulong modelId, ulong textureId) textureId, uint[] vertexIndices, uint[] normalIndices, uint[] textureIndices)
                    : Face(vertexIndices, normalIndices, textureIndices)
                {
                    public (ulong modelCode, ulong textureCode) TextureId { get; set; } = textureId;
                }
            }
        }
    }
}
