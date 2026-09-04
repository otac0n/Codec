// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Codec;
    using Codec.Archives;
    using Codec.Imaging;
    using Codec.Streams;
    using DiscUtils.Streams;
    using ImageMagick;
    using Microsoft.Extensions.DependencyInjection;
    using static Codec.Imaging.DdsConstants;

    public class TxnFile
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

        public static (TxnHeader Header, TxnImage[] Images, TxnTexture[] Textures) ReadHeaders(Stream stream)
        {
            var header = stream.ReadBigEndian<TxnHeader>();
            stream.Position = header.ImageOffset;
            var images = stream.ReadArrayBigEndian<TxnImage>((int)header.ImageCount);
            stream.Position = header.TextureOffset;
            var textureDefinitions = stream.ReadArrayBigEndian<TxnTexture>((int)header.TextureCount);
            return (header, images, textureDefinitions);
        }

        private static int GetImageIndex(TxnHeader header, TxnTexture tx)
        {
            return (int)((tx.ImageOffset - header.ImageOffset) / Marshal.SizeOf<TxnImage>());
        }

        public static IEnumerable<Entry> FindRelatedFiles(string fullPath, NestedFileSystemManager fsm, uint strCode, int ix)
        {
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

            var targetName = $"{strCode:x8}_{ix}.data";
            return searchPaths.SelectMany(searchPath => fsm.EnumerateFiles(searchPath, targetName, recursive: true));
        }

        private static string? FindTextureDataPath(string fullPath, NestedFileSystemManager fsm)
        {
            var searchPath = fullPath;

            while (true)
            {
                var name = PathExtensions.GetFileName(searchPath);
                var folder = PathExtensions.GetDirectoryName(searchPath);
                if ((name == WellKnownPaths.PCTextures && !folder.Contains(WellKnownPaths.PCTextures)) || string.IsNullOrEmpty(folder))
                {
                    break;
                }

                var testPath = fsm.CombinePaths(folder, WellKnownPaths.PackedTextures);
                if (fsm.FileExists(testPath))
                {
                    return testPath;
                }

                searchPath = folder;
            }

            return null;
        }

        private class TxnFileFileSystem(string fullPath, NestedFileSystemManager fsm) : IndexedFileSystem<int>
        {
            protected override IEnumerable<int> ReadIndex()
            {
                using var stream = fsm.OpenRead(fullPath);
                var (header, images, textures) = ReadHeaders(stream);

                var entries = new List<int>();
                for (var i = 0; i < images.Length; i++)
                {
                    entries.Add(~i);
                }

                foreach (var tx in textures)
                {
                    entries.Add((int)tx.Id);
                    if (tx.UOffset == 0 && tx.VOffset == 0 && tx.UScale == 1 && tx.VScale == 1)
                    {
                        entries.Remove(~GetImageIndex(header, tx));
                    }
                }

                return entries;
            }

            protected override string GetEntryName(int entry) =>
                entry < 0 ? $"{~entry}.dds" : $"{entry:x6}.dds";

            protected override Stream Open(int entry, FileStreamOptions parentOptions)
            {
                FileBase.EnsureReadOnly(parentOptions, "Writing to sub images in .txn files is not supported.");

                var stream = fsm.Open(fullPath, parentOptions);
                var (header, images, textures) = ReadHeaders(stream);

                int ix;
                TxnTexture? texture = null;
                if (entry < 0)
                {
                    ix = ~entry;
                }
                else
                {
                    ix = Array.FindIndex(textures, t => t.Id == entry);
                    if (ix < 0)
                    {
                        throw new FileNotFoundException($"Texture with ID '{entry:x8}' not found.");
                    }

                    texture = textures[ix];
                    ix = GetImageIndex(header, textures[ix]);
                }

                if (ix < 0 || ix >= images.Length)
                {
                    throw new FileNotFoundException($"Image with Index '{ix}' not found.");
                }

                var image = images[ix];

                Stream dataStream;
                if ((header.Flags & 0x400) == 0x400)
                {
                    stream.Position += ix * 0x100;
                    var path = stream.ReadNullString();
                    var textureData = FindTextureDataPath(fullPath, fsm);

                    dataStream = fsm.OpenRead(fsm.CombinePaths(textureData, path));
                }
                else
                {
                    var external = (image.Flags & 0xF0) == 0xF0;
                    if (external)
                    {
                        stream.Dispose();

                        var found = FindRelatedFiles(fullPath, fsm, (texture ?? textures[0]).FileId, ix).FirstOrDefault(f => f is not null);
                        if (found is not { CanOpen: true })
                        {
                            throw new FileNotFoundException($"External texture file not found.");
                        }

                        dataStream = fsm.OpenRead(found.Path);
                    }
                    else
                    {
                        var dataOffset = image.Offset;
                        var dataLength = stream.Length - dataOffset; // TODO: Not the whole rest of the file.
                        dataStream = new OffsetStreamSpan(stream, dataOffset, dataLength, Ownership.Dispose);
                    }
                }

                var dds = BuildDdsStream(image, dataStream);
                if (texture is TxnTexture tx && !(tx.UOffset == 0 && tx.VOffset == 0 && tx.UScale == 1 && tx.VScale == 1))
                {
                    var ms = new MemoryStream();
                    using (dds)
                    using (var collection = new MagickImageCollection())
                    {
                        collection.Read(dds, MagickFormat.Dds);

                        var x = (int)Math.Round(tx.UOffset * image.Width);
                        var y = (int)Math.Round(tx.VOffset * image.Height);
                        for (var i = 0; i < collection.Count; i++)
                        {
                            var mip = collection[i];
                            var mipWidth = (int)mip.Width;
                            var mipHeight = (int)mip.Height;

                            var scaleX = (double)mipWidth / image.Width;
                            var scaleY = (double)mipHeight / image.Height;

                            var mipX = (int)Math.Round(x * scaleX);
                            var mipY = (int)Math.Round(y * scaleY);
                            var mipCropWidth = Math.Max(1, (int)Math.Round(tx.Width * scaleX));
                            var mipCropHeight = Math.Max(1, (int)Math.Round(tx.Height * scaleY));

                            mipCropWidth = Math.Min(mipCropWidth, mipWidth - mipX);
                            mipCropHeight = Math.Min(mipCropHeight, mipHeight - mipY);

                            mip.Crop(new MagickGeometry(mipX, mipY, (uint)mipCropWidth, (uint)mipCropHeight));
                            mip.ResetPage();
                        }

                        collection.Write(ms, MagickFormat.Dds);
                        ms.Position = 0;
                        return ms;
                    }
                }
                else
                {
                    return dds;
                }
            }

            private static Stream BuildDdsStream(TxnImage image, Stream dataStream)
            {
                var sizeOfHeader = Marshal.SizeOf<DDS_HEADER>();
                var sizeOfFormat = (uint)Marshal.SizeOf<DDS_PIXELFORMAT>();
                var (blockSize, pixelFormat) = image.PixelFormat switch
                {
                    0x03 or 0x08 => (0, new DDS_PIXELFORMAT
                    {
                        Size = sizeOfFormat,
                        Flags = DDPF_RGB | DDPF_ALPHA,
                        ABitMask = 0xFF000000,
                        RBitMask = 0x00FF0000,
                        GBitMask = 0x0000FF00,
                        BBitMask = 0x000000FF,
                    }),
                    0x09 => (8, new DDS_PIXELFORMAT
                    {
                        Size = sizeOfFormat,
                        Flags = DDPF_FOURCC,
                        FourCC = 0x31545844, // 'DXT1'
                    }),
                    0x0A => (16, new DDS_PIXELFORMAT
                    {
                        Size = sizeOfFormat,
                        Flags = DDPF_FOURCC,
                        FourCC = 0x33545844, // 'DXT3'
                    }),
                    0x0B => (16, new DDS_PIXELFORMAT
                    {
                        Size = sizeOfFormat,
                        Flags = DDPF_FOURCC,
                        FourCC = 0x35545844, // 'DXT5'
                    }),
                };

                var width = image.Width;
                var height = image.Height;

                var dds = new DDS_HEADER
                {
                    Signature = 0x20534444u,
                    Size = (uint)sizeOfHeader - 4, // Signature not included in size.
                    Flags = DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT | DDSD_LINEARSIZE,
                    Caps1 = DDSCAPS_TEXTURE,
                    Width = width,
                    Height = height,
                    PitchOrLinearSize = blockSize == 0
                        ? (uint)dataStream.Length
                        : (uint)(Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * blockSize),
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
