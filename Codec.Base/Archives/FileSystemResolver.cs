namespace Codec.Archives
{
    using System;
    using System.IO.Abstractions;

    public delegate IFileSystem? FileSystemFactory(string fullPath, string parentRelativePath, IFileSystem parent, string parentPath);

    public delegate FileSystemFactory? FileSystemHandler(string fullPath, string fileSystemRelativePath, IFileSystem parent, string parentPath);

    public delegate FileSystemFactory? FileSystemResolver(IServiceProvider serviceProvider, string fullPath, string parentRelativePath, IFileSystem parent, string parentPath);
}
