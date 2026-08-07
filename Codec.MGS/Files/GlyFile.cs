namespace Codec.MGS.Files
{
    using System;
    using System.IO;
    using Codec.Services;
    using ImageMagick;
    using Microsoft.Extensions.DependencyInjection;

    internal class GlyFile
    {
        public static readonly int ChunkSize = 36;
        private static readonly uint Width = 12;
        private static readonly uint Height = 12;

        public static void Register(IServiceCollection services)
        {
            services.AddSingleton(new EntryTypeMatcher(EntryType.Image, "*.gly"));

            services.AddSingleton<FileHandlerResolver<MagickImage>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".gly", StringComparison.OrdinalIgnoreCase))
                {
                    // TODO: Check the file-length beforehand.
                    return new((fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var file = parent.File.OpenRead(parentRelativePath);
                        return Load(file);
                    });
                }

                return null;
            });
        }

        public static MagickImage Load(Stream stream)
        {
            var data = new byte[ChunkSize];
            var read = 0;
            while (read < data.Length)
            {
                var n = stream.Read(data, read, data.Length - read);
                if (n == 0)
                {
                    throw new EndOfStreamException($"Expected {ChunkSize} bytes for a glyph, got {read}.");
                }

                read += n;
            }

            var pixels = new byte[Width * Height * 4];
            var bitIndex = 0;

            for (var i = 0; i < Width * Height; i++)
            {
                var high = ReadBit(data, bitIndex++);
                var low = ReadBit(data, bitIndex++);
                var value = (high << 1) | low;

                var gray = (byte)(value * 0x55);

                var offset = i * 4;
                pixels[offset + 0] = gray;
                pixels[offset + 1] = gray;
                pixels[offset + 2] = gray;
                pixels[offset + 3] = 0xFF;
            }

            var settings = new PixelReadSettings(Width, Height, StorageType.Char, PixelMapping.RGBA);
            return new MagickImage(pixels, settings);
        }

        private static int ReadBit(byte[] data, int bitIndex)
        {
            var byteIndex = bitIndex / 8;
            var bitOffset = 7 - (bitIndex % 8);
            return (data[byteIndex] >> bitOffset) & 1;
        }
    }
}
