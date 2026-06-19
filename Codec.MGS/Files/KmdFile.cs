namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using Codec.Files;
    using Microsoft.Extensions.DependencyInjection;
    using Bounds = (System.Numerics.Vector3 Start, System.Numerics.Vector3 End);

    public class KmdFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileHandlerResolver<Model>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".kmd", StringComparison.OrdinalIgnoreCase))
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

        public static Model1 FromStream(Stream stream)
        {
            static Vector3 V(Point p) => new(p.X, p.Y, p.Z);

            var buffer = new byte[stream.Length];
            var fileSpan = buffer.AsSpan();
            stream.ReadExactly(fileSpan);
            var header = MemoryMarshal.Cast<byte, Header>(fileSpan)[0];
            var meshDefinitions = MemoryMarshal.Cast<byte, MeshDefinition>(fileSpan[32..])[0..(int)header.MeshCount];

            var meshes = new List<Model1.Mesh1>((int)header.MeshCount);
            for (var m = 0; m < header.MeshCount; m++)
            {
                var relativeMesh = meshDefinitions[m].RelativeIndex != -1 && !(meshDefinitions[m].RelativeIndex == 0 && m == 0) ? meshes[meshDefinitions[m].RelativeIndex] : null;

                var vertexCount = meshDefinitions[m].VertexCount;
                var vertices = new Vector3[vertexCount];
                var vertexData = MemoryMarshal.Cast<byte, (short X, short Y, short Z, short W)>(fileSpan[(int)meshDefinitions[m].VertexOffset..])[0..(int)vertexCount];
                for (var v = 0; v < vertexCount; v++)
                {
                    // Not using W.
                    vertices[v] = new(
                        vertexData[v].X,
                        vertexData[v].Y,
                        vertexData[v].Z);
                }

                var normalCount = meshDefinitions[m].NormalCount;
                var normalData = MemoryMarshal.Cast<byte, (short X, short Y, short Z, short W)>(fileSpan[(int)meshDefinitions[m].NormalOffset..])[0..(int)normalCount];
                var normals = new Vector3[normalCount];
                for (var n = 0; n < normalCount; n++)
                {
                    normals[n] = new Vector3(
                        normalData[n].X / 4096f,
                        normalData[n].Y / 4096f,
                        normalData[n].Z / 4096f);
                }

                var faceCount = meshDefinitions[m].FaceCount;
                var faces = new Model1.Mesh1.Face1[faceCount];
                var vertexIndexData = MemoryMarshal.Cast<byte, (byte A, byte B, byte C, byte D)>(fileSpan[(int)meshDefinitions[m].VertexIndexOffset..])[0..(int)faceCount];
                var normalIndexData = MemoryMarshal.Cast<byte, (byte A, byte B, byte C, byte D)>(fileSpan[(int)meshDefinitions[m].NormalIndexOffset..])[0..(int)faceCount];
                var texCoordCount = (meshDefinitions[m].TextureOffset - meshDefinitions[m].TextureCoordOffset) / 2;
                var textureCoordData = MemoryMarshal.Cast<byte, (byte U, byte V)>(fileSpan[(int)meshDefinitions[m].TextureCoordOffset..])[0..(int)texCoordCount];
                var textureData = MemoryMarshal.Cast<byte, ushort>(fileSpan[(int)meshDefinitions[m].TextureOffset..])[0..(int)faceCount];

                var texCoords = new Vector2[texCoordCount];
                for (var t = 0; t < texCoords.Length; t++)
                {
                    texCoords[t] = new Vector2(
                        textureCoordData[t].U / 255f,
                        textureCoordData[t].V / 255f);
                }

                for (var v = 0; v < faceCount; v++)
                {
                    var (vA, vB, vC, vD) = vertexIndexData[v];
                    var (nA, nB, nC, nD) = normalIndexData[v];
                    faces[v] = new Model1.Mesh1.Face1(
                        textureData[v],
                        [vB, vA, vC, vD],
                        [nB, nA, nC, nD],
                        [(uint)(4 * v + 1), (uint)(4 * v + 0), (uint)(4 * v + 2), (uint)(4 * v + 3)]);
                }

                meshes.Add(
                    new Model1.Mesh1(
                        (DrawingFlags)meshDefinitions[m].Flags,
                        (V(meshDefinitions[m].Min), V(meshDefinitions[m].Max)),
                        V(meshDefinitions[m].RelativeOrigin),
                        relativeMesh,
                        vertices,
                        normals,
                        texCoords,
                        faces));
            }

            return new Model1((V(header.Min), V(header.Max)), [.. meshes]);
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
        private struct Point
        {
            public int X;
            public int Y;
            public int Z;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private struct Header
        {
            public uint FaceCount;
            public uint MeshCount;
            public Point Min;
            public Point Max;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private struct MeshDefinition
        {
            public uint Flags;
            public uint FaceCount;
            public Point Min;
            public Point Max;
            public Point RelativeOrigin;
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

        public class Model1(Bounds bounds, Model1.Mesh1[] meshes)
            : Model(meshes)
        {
            public new Mesh1[] Meshes { get; } = meshes;

            public class Mesh1(DrawingFlags flags, Bounds bounds, Vector3 relativeOrigin, Mesh? relativeMesh, Vector3[] relativeVertices, Vector3[] normals, Vector2[] textureCoords, Mesh1.Face1[] faces)
                : Mesh(relativeOrigin, relativeVertices, textureCoords, normals, faces, relativeMesh)
            {
                public DrawingFlags Flags { get; } = flags;

                public new Face1[] Faces => faces;

                public class Face1(ushort textureId, uint[] vertexIndices, uint[] normalIndices, uint[] textureIndices)
                    : Face(vertexIndices, normalIndices, textureIndices)
                {
                    public ushort TextureId { get; set; } = textureId;
                }
            }
        }
    }
}
