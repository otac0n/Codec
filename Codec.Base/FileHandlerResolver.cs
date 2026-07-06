namespace Codec
{
    using System;
    using System.IO.Abstractions;

    /// <summary>
    /// Reads a value of type <typeparamref name="T"/> from the file at the given path.
    /// </summary>
    public delegate T FileReader<T>(string fullPath, string fileSystemRelativePath, IFileSystem fileSystem, string fileSystemPath);

    /// <summary>
    /// Writes <paramref name="value"/> back to the file at the given path.
    /// </summary>
    /// <remarks>
    /// Implementations should treat this as a merge, not a replacement: anything the format
    /// tracks that <paramref name="value"/> doesn't specify (or can't represent) should be left
    /// exactly as it was found, rather than reset to a default.
    /// </remarks>
    public delegate void FileWriter<T>(T value, string fullPath, string fileSystemRelativePath, IFileSystem fileSystem, string fileSystemPath);

    /// <summary>
    /// Bundles the ability to read, and optionally write, a particular file format.
    /// </summary>
    /// <remarks>
    /// A format that is read-only simply omits <see cref="Write"/>. This used to be a single
    /// delegate (<c>FileHandler&lt;T&gt;</c> was itself the reader function); it is now a small
    /// class so that a resolver can hand back both directions for a format without having to
    /// invent a second, parallel resolver type.
    /// </remarks>
    public class FileHandler<T>
    {
        public FileHandler(FileReader<T> read, FileWriter<T>? write = null)
        {
            this.Read = read ?? throw new ArgumentNullException(nameof(read));
            this.Write = write;
        }

        public FileReader<T> Read { get; }

        public FileWriter<T>? Write { get; }

        public bool CanWrite => this.Write is not null;
    }

    /// <summary>
    /// Attempts to resolve a <see cref="FileHandler{T}"/> capable of handling the file at the
    /// given path, or <see langword="null"/> if this resolver doesn't recognize it.
    /// </summary>
    public delegate FileHandler<T>? FileHandlerResolver<T>(IServiceProvider serviceProvider, string fullPath, string fileSystemRelativePath, IFileSystem fileSystem, string fileSystemPath);
}
