namespace Codec.MGS.Files
{
    using System;
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
            var meshDef = fileSpan[(int)header.BaseOffset..].CastWithEndianness<MeshDef>(1, Endianness.BigEndian)[0];

            var tableHeaders = fileSpan[(int)(header.BaseOffset + meshDef.MeshTableOffset)..].CastWithEndianness<TableHeader>((int)header.PartCount, Endianness.BigEndian);
            var vertices = fileSpan[(int)(header.BaseOffset + header.VertexOffset)..].CastWithEndianness<Short3>(header.VertexCount, Endianness.BigEndian);
            var normals = fileSpan[(int)(header.BaseOffset + header.NormalOffset)..].CastWithEndianness<SByte3>(header.NormalCount, Endianness.BigEndian);
            var texCoords = fileSpan[(int)(header.BaseOffset + header.TexCoordOffset)..].CastWithEndianness<Short2>(header.TexCoordCount, Endianness.BigEndian);

            var scene = new Scene();
            var rootNode = new Node("root");
            scene.RootNode = rootNode;
            for (var t = 0; t < header.PartCount; t++)
            {
                var tableHeader = tableHeaders[t];
                var entries = fileSpan[(int)(header.BaseOffset + tableHeader.EntryOffset)..].CastWithEndianness<TableEntry>(tableHeader.EntryCount, Endianness.BigEndian);
                for (var i = 0; i < tableHeader.EntryCount; i++)
                {
                    var mesh = entries[i];
                    var faces = fileSpan[(int)(header.BaseOffset + mesh.FacesTableOffset)..].CastWithEndianness<FaceEntry>(mesh.FaceCount, Endianness.BigEndian);
                    // TODO: Populate the scene.
                }
            }

            return scene;
        }

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
            public uint Unknown4;
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
            public uint Unknown1;
            public uint Unknown2;
            public uint Unknown3;
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
