namespace Codec.Files
{
    using System.IO;
    using System.IO.Abstractions;

    public record class AudioStream(Stream Stream, string FileName)
    {
        public static explicit operator AudioStream(Stream stream) => stream is null ? null : new(stream, stream is FileSystemStream file ? file.Name : null);

        public static implicit operator Stream(AudioStream audioStream) => audioStream?.Stream;
    }
}
