namespace Codec.Files
{
    using System;
    using System.IO.Abstractions;

    public delegate T FileHandler<T>(string fullPath, string fileSystemRelativePath, IFileSystem fileSystem, string fileSystemPath);

    public delegate FileHandler<T>? FileHandlerResolver<T>(IServiceProvider serviceProvider, string fullPath, string fileSystemRelativePath, IFileSystem fileSystem, string fileSystemPath);
}
