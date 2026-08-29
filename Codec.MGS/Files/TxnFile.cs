// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Codec;
    using Codec.Archives;
    using Codec.Imaging;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using static Codec.Imaging.DdsConstants;

    internal class TxnFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".txn", StringComparison.OrdinalIgnoreCase))
                {
                    return (fullPath, parentRelativePath, parent, parentPath) => new TxnFileFileSystem(fullPath, serviceProvider.GetRequiredService<NestedFileSystemManager>());
                }

                return null;
            });
        }

        private class TxnFileFileSystem(string fullPath, NestedFileSystemManager fsm) : IndexedFileSystem<uint>
        {
            protected override IEnumerable<uint> ReadIndex()
            {
                using var stream = fsm.OpenRead(fullPath);
                var header = stream.ReadBigEndian<TxnHeader>();
                stream.Position = header.TextureOffset;
                var textureDefinitions = stream.ReadArrayBigEndian<TxnTexture>((int)header.TextureCount);
                var ids = new List<uint>((int)header.TextureCount);
                for (var t = 0; t < header.TextureCount; t++)
                {
                    var info = textureDefinitions[t];
                    stream.Position = info.ImageOffset;
                    var image = stream.ReadBigEndian<TxnImage>();
                    if (image.PixelFormat is 0x08 or 0x09 or 0x0B)
                    {
                        ids.Add(info.Id);
                    }
                }

                return ids;
            }

            protected override string GetEntryName(uint entry) =>
                $"{entry:x6}.dds";

            protected override Stream Open(uint entry, FileStreamOptions parentOptions)
            {
                FileBase.EnsureReadOnly(parentOptions, "Writing to sub images in .txn files is not supported.");

                var stream = fsm.Open(fullPath, parentOptions);
                var header = stream.ReadBigEndian<TxnHeader>();
                stream.Position = header.TextureOffset;
                var textureDefinitions = stream.ReadArrayBigEndian<TxnTexture>((int)header.TextureCount);
                var ix = Array.FindIndex(textureDefinitions, t => t.Id == entry);
                if (ix == -1)
                {
                    throw new FileNotFoundException($"Texture with ID '{entry:x8}' not found.");
                }

                var texture = textureDefinitions[ix];
                stream.Position = texture.ImageOffset;
                var image = stream.ReadBigEndian<TxnImage>();

                Stream dataStream;
                var external = (image.Flags & 0xF0) == 0xF0;
                if (external)
                {
                    stream.Dispose();
                    var folderName = PathExtensions.GetDirectoryName(fullPath) ?? string.Empty;
                    var searchPaths = new List<string>
                    {
                        folderName,
                    };

                    if (!string.IsNullOrEmpty(folderName))
                    {
                        folderName = PathExtensions.GetDirectoryName(folderName) ?? string.Empty;
                        searchPaths.Add(folderName);
                    }

                    var targetName = $"{texture.FileId:x8}_{ix}.data";

                    var found = searchPaths.SelectMany(searchPath => fsm.EnumerateFiles(searchPath, targetName, recursive: true)).FirstOrDefault(f => f is not null);
                    if (found is not { CanOpen: true })
                    {
                        throw new FileNotFoundException($"External texture file '{targetName}' not found.");
                    }

                    dataStream = fsm.OpenRead(found.Path);
                }
                else
                {
                    var dataOffset = image.Offset;
                    var dataLength = stream.Length - dataOffset; // TODO: Not the whole rest of the file.
                    dataStream = new OffsetStreamSpan(stream, dataOffset, dataLength, Ownership.Dispose);
                }

                return BuildDdsStream(image, dataStream);
            }

            private static Stream BuildDdsStream(TxnImage image, Stream dataStream)
            {
                var sizeOfHeader = Marshal.SizeOf<DDS_HEADER>();
                var sizeOfFormat = (uint)Marshal.SizeOf<DDS_PIXELFORMAT>();
                var blockSize = image.PixelFormat == 0x09 ? 8u : 16u;
                var pixelFormat = image.PixelFormat switch
                {
                    // TODO: 0x03 => ???
                    0x08 => new DDS_PIXELFORMAT
                    {
                        Size = sizeOfFormat,
                        Flags = DDPF_RGB | DDPF_ALPHA,
                        RBitMask = 0x00FF0000,
                        GBitMask = 0x0000FF00,
                        BBitMask = 0x000000FF,
                        ABitMask = 0xFF000000,
                    },
                    0x09 => new DDS_PIXELFORMAT
                    {
                        Size = sizeOfFormat,
                        Flags = DDPF_FOURCC,
                        FourCC = 0x31545844, // 'DXT1'
                    },
                    0x0B => new DDS_PIXELFORMAT
                    {
                        Size = sizeOfFormat,
                        Flags = DDPF_FOURCC,
                        FourCC = 0x35545844, // 'DXT5'
                    },
                };
                var dds = new DDS_HEADER
                {
                    Signature = 0x20534444u,
                    Size = (uint)sizeOfHeader - 4, // Signature not included in size.
                    Flags = DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT | DDSD_LINEARSIZE,
                    Caps1 = DDSCAPS_TEXTURE,
                    Width = image.Width,
                    Height = image.Height,
                    PitchOrLinearSize = Math.Max(1u, ((uint)image.Width + 3) / 4) * Math.Max(1u, ((uint)image.Height + 3) / 4) * blockSize,
                    Depth = 0u,
                    PixelFormat = pixelFormat,
                };

                var headerStream = new MemoryStream(sizeOfHeader);
                headerStream.WriteLittleEndian(dds);
                return new ConcatStream(
                    Ownership.Dispose,
                    MappedStream.FromStream(headerStream, Ownership.Dispose),
                    MappedStream.FromStream(dataStream, Ownership.Dispose));
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TxnImage
        {
            public ushort Width;
            public ushort Height;
            public ushort PixelFormat;
            public ushort Flags;
            public uint Offset;
            public uint MipMapOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TxnTexture
        {
            public uint Flags;
            public uint Id;
            public uint FileId;
            public ushort Width;
            public ushort Height;
            public ushort XOffset;
            public ushort YOffset;
            public uint ImageOffset;
            public uint Pad;
            public float UScale;
            public float VScale;
            public float UOffset;
            public float VOffset;
            public uint Pad2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TxnHeader
        {
            public uint Pad;
            public uint Flags;
            public uint ImageCount;
            public uint ImageOffset;
            public uint TextureCount;
            public uint TextureOffset;
            public uint Pad1;
            public uint Pad2;
        }
    }
}
