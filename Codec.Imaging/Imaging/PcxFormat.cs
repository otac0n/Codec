namespace Codec.Imaging
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct R8G8B8
    {
        public byte R;
        public byte G;
        public byte B;
    }

    [InlineArray(16)]
    public struct PcxPalette
    {
        public R8G8B8 Color;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PcxHeader
    {
        public byte Manufacturer;
        public byte Version;
        public byte Encoding;
        public byte BitsPerPixel;
        public ushort XMin;
        public ushort YMin;
        public ushort XMax;
        public ushort YMax;
        public ushort HorzRes;
        public ushort VertRes;
        public PcxPalette Palette;
        public byte Reserved1;
        public byte NumBitPlanes;
        public ushort BytesPerLine;
        public ushort PaletteType;
        public ushort HorzScreenSize;
        public ushort VertScreenSize;
    }
}
