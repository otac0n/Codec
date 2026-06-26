namespace Codec.Files
{
    using System.IO;

    public record class AudioStream(Stream Stream)
    {
        public static explicit operator AudioStream(Stream stream) => stream is null ? null : new(stream);

        public static implicit operator Stream(AudioStream audioStream) => audioStream?.Stream;
    }
}
