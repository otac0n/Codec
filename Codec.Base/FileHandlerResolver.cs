namespace Codec
{
    using System;
    using System.IO.Abstractions;
    using static Codec.Services.EntryTypeDetector;

    public delegate T FileHandler<T>(string fullPath, string fileSystemRelativePath, IFileSystem fileSystem, string fileSystemPath);

    public delegate FileHandler<T>? FileHandlerResolver<T>(IServiceProvider serviceProvider, string fullPath, string fileSystemRelativePath, IFileSystem fileSystem, string fileSystemPath);

    public record EntryTypeMatcher(EntryType EntryType, string GlobPattern);
}
