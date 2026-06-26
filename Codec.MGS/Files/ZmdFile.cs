namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using Assimp;
    using Codec.Files;
    using Codec.Services;
    using Microsoft.Extensions.DependencyInjection;

    public class ZmdFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton(new EntryTypeMatcher(EntryType.Model, "*.zmd"));

            services.AddSingleton<FileHandlerResolver<RenderableScene>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".zmd", StringComparison.OrdinalIgnoreCase))
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
            if (header.Signature != 0x61444d4b) // KMDa
            {
                return null;
            }

            var scene = new Scene();
            var rootNode = new Node("root");
            scene.RootNode = rootNode;

            var offset = Marshal.SizeOf<Header>();
            var dataOffset = (int)header.DataOffset + offset;
            var vertexOffset = 0;
            var normalOffset = 0;
            var texCoordOffset = 0;
            for (var i = 0; i < header.ModelCount; i++)
            {
                var id = BitConverter.ToUInt32(buffer, offset);
                var idRoot = new Node($"model{id}");
                rootNode.Children.Add(idRoot);
                var count = KmdFile.LoadKmdModel(fileSpan, offset + sizeof(uint), dataOffset, scene, idRoot, ref vertexOffset, ref normalOffset, ref texCoordOffset);
                offset += count + sizeof(uint);
            }

            return scene;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public uint Signature;
            public uint ModelCount;
            public uint DataOffset;
            public uint BodyChunkLength;
        }
    }
}
