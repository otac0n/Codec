namespace Codec.Imaging
{
    using System.Runtime.InteropServices;

    public static class DdsConstants
    {
        public static readonly uint DDSD_CAPS = 0x1;
        public static readonly uint DDSD_HEIGHT = 0x2;
        public static readonly uint DDSD_WIDTH = 0x4;
        public static readonly uint DDSD_PIXELFORMAT = 0x1000;
        public static readonly uint DDSD_LINEARSIZE = 0x80000;
        public static readonly uint DDSD_MIPMAPCOUNT = 0x20000;
        public static readonly uint DDSCAPS_COMPLEX = 0x8;
        public static readonly uint DDSCAPS_TEXTURE = 0x1000;
        public static readonly uint DDSCAPS_MIPMAP = 0x400000;
        public static readonly uint DDPF_ALPHAPIXELS = 0x1;
        public static readonly uint DDPF_ALPHA = 0x2;
        public static readonly uint DDPF_FOURCC = 0x4;
        public static readonly uint DDPF_RGB = 0x40;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DDS_HEADER
    {
        public uint Signature;
        public uint Size;
        public uint Flags;
        public uint Height;
        public uint Width;
        public uint PitchOrLinearSize;
        public uint Depth;
        public uint MipMapCount;
        public ulong ReservedA;
        public ulong ReservedB;
        public ulong ReservedC;
        public ulong ReservedD;
        public ulong ReservedE;
        public uint ReservedF;
        public DDS_PIXELFORMAT PixelFormat;
        public uint Caps1;
        public uint Caps2;
        public uint Caps3;
        public uint Caps4;
        public uint Reserved2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DDS_PIXELFORMAT
    {
        public uint Size;
        public uint Flags;
        public uint FourCC;
        public uint RGBBitCount;
        public uint RBitMask;
        public uint GBitMask;
        public uint BBitMask;
        public uint ABitMask;
    }
}
