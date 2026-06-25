namespace Codec.Audio
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    [InlineArray(16)]
    public struct Name16
    {
        public byte Char0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VagHeader
    {
        public uint Signature;
        public uint Version;
        public uint Reserved1;
        public uint DataSize;
        public uint SamplingFreq;
        public uint Reserved2;
        public uint Reserved3;
        public uint Reserved4;
        public Name16 Name;

        public VagHeader()
        {
            this.Signature = 0x56414770;
        }
    }
}
