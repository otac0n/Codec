// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Imaging
{
    using System.IO;
    using System.Runtime.InteropServices;
    using ImageMagick;

    public class TgaWriter<TColor, TIndex>
        where TColor : struct
        where TIndex : struct
    {
        private static readonly int ColorSize = Marshal.SizeOf<TColor>();
        private static readonly int IndexSize = Marshal.SizeOf<TIndex>();
        private readonly short xOffset;
        private readonly short yOffset;

        public TgaWriter(ushort width, ushort height, ushort paletteLength, short xOffset = 0, short yOffset = 0)
            : this(new(width * height + paletteLength * ColorSize + Marshal.SizeOf<TgaHeader>()), width, height, paletteLength, xOffset, yOffset)
        {
        }

        public TgaWriter(MemoryStream tgaStream, ushort width, ushort height, ushort paletteLength, short xOffset = 0, short yOffset = 0)
        {
            tgaStream.SetLength(tgaStream.Position + width * height + paletteLength * ColorSize + Marshal.SizeOf<TgaHeader>());
            tgaStream.WriteLittleEndian(new TgaHeader
            {
                ColorMapType = 1,
                ImageType = 1,
                CMapLength = paletteLength,
                CMapDepth = (byte)(ColorSize * 8),
                XOffset = xOffset,
                YOffset = yOffset,
                Width = width,
                Height = height,
                PixelDepth = (byte)(IndexSize * 8),
                ImageDescriptor = 0x28,
            });
            this.TgaStream = tgaStream;
            this.xOffset = xOffset;
            this.yOffset = yOffset;
        }

        public MemoryStream TgaStream { get; }

        public void WriteColor(TColor value)
        {
            this.TgaStream.WriteLittleEndian(value);
        }

        public void WriteIndex(TIndex index)
        {
            this.TgaStream.WriteLittleEndian(index);
        }

        public MagickImage ToMagickImage()
        {
            this.TgaStream.Position = 0;
            var image = new MagickImage(this.TgaStream, MagickFormat.Tga);
            var page = image.Page;
            page.X = this.xOffset;
            page.Y = this.yOffset;
            image.Page = page;
            return image;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TgaHeader
    {
        public byte IDLength;
        public byte ColorMapType;
        public byte ImageType;
        public ushort CMapStart;
        public ushort CMapLength;
        public byte CMapDepth;
        public short XOffset;
        public short YOffset;
        public ushort Width;
        public ushort Height;
        public byte PixelDepth;
        public byte ImageDescriptor;
    }
}
