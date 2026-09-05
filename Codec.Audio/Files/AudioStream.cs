namespace Codec.Files
{
    using System.IO;

    public record class AudioStream(Stream Stream, string FileName)
    {
        public static implicit operator Stream?(AudioStream audioStream) => audioStream?.Stream;
    }
}
