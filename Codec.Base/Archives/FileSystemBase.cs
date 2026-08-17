namespace Codec.Archives
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Win32.SafeHandles;

    public abstract class FileSystemBase : IFileSystem, IDisposable
    {
        protected FileSystemBase()
        {
            this.Directory = new DirectoryBase(this);
            this.DirectoryInfo = new DirectoryInfoFactoryBase(this);
            this.DriveInfo = new DriveInfoFactoryBase(this);
            this.File = new FileBase(this);
            this.FileInfo = new FileInfoFactoryBase(this);
            this.FileStream = new FileStreamFactoryBase(this);
            this.FileSystemWatcher = new FileSystemWatcherFactoryBase(this);
            this.Path = new PathBase(this);
        }

        /// <inheritdoc/>
        public virtual IDirectory Directory { get; protected init; }

        /// <inheritdoc/>
        public virtual IDirectoryInfoFactory DirectoryInfo { get; protected init; }

        /// <inheritdoc/>
        public virtual IDriveInfoFactory DriveInfo { get; protected init; }

        /// <inheritdoc/>
        public virtual IFile File { get; protected init; }

        /// <inheritdoc/>
        public virtual IFileInfoFactory FileInfo { get; protected init; }

        /// <inheritdoc/>
        public virtual IFileStreamFactory FileStream { get; protected init; }

        /// <inheritdoc/>
        public virtual IFileSystemWatcherFactory FileSystemWatcher { get; protected init; }

        /// <inheritdoc/>
        public virtual IPath Path { get; protected init; }

        protected virtual void Dispose(bool disposing)
        {
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public class DirectoryBase : IDirectory
        {
            private readonly FileSystemBase parent;

            public DirectoryBase(FileSystemBase parent)
            {
                this.parent = parent;
            }

            /// <inheritdoc/>
            public virtual IFileSystem FileSystem => this.parent;

            /// <inheritdoc/>
            public virtual IDirectoryInfo CreateDirectory(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual IDirectoryInfo CreateDirectory(string path, UnixFileMode unixCreateMode) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual IFileSystemInfo CreateSymbolicLink(string path, string pathToTarget) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual IDirectoryInfo CreateTempSubdirectory(string? prefix = null) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void Delete(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void Delete(string path, bool recursive) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual IEnumerable<string> EnumerateDirectories(string path) => this.EnumerateDirectories(path, "*");

            /// <inheritdoc/>
            public virtual IEnumerable<string> EnumerateDirectories(string path, string searchPattern) => this.EnumerateDirectories(path, searchPattern, SearchOption.TopDirectoryOnly);

            /// <inheritdoc/>
            public virtual IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) => this.EnumerateFileSystemEntries(path, searchPattern, searchOption, directories: true);

            /// <inheritdoc/>
            public virtual IEnumerable<string> EnumerateDirectories(string path, string searchPattern, EnumerationOptions enumerationOptions) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual IEnumerable<string> EnumerateFiles(string path) => this.EnumerateFiles(path, "*");

            /// <inheritdoc/>
            public virtual IEnumerable<string> EnumerateFiles(string path, string searchPattern) => this.EnumerateFiles(path, searchPattern, SearchOption.TopDirectoryOnly);

            /// <inheritdoc/>
            public virtual IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) => this.EnumerateFileSystemEntries(path, searchPattern, searchOption, files: true);

            /// <inheritdoc/>
            public virtual IEnumerable<string> EnumerateFiles(string path, string searchPattern, EnumerationOptions enumerationOptions) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual IEnumerable<string> EnumerateFileSystemEntries(string path) => this.EnumerateFileSystemEntries(path, "*");

            /// <inheritdoc/>
            public virtual IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern) => this.EnumerateFileSystemEntries(path, searchPattern, SearchOption.TopDirectoryOnly);

            /// <inheritdoc/>
            public virtual IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption) => this.EnumerateFileSystemEntries(path, searchPattern, searchOption, files: true, directories: true);

            /// <inheritdoc/>
            public virtual IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, EnumerationOptions enumerationOptions) => this.EnumerateDirectories(path, searchPattern, enumerationOptions).Concat(this.EnumerateFiles(path, searchPattern, enumerationOptions));

            protected virtual IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption, bool files = false, bool directories = false) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual bool Exists([NotNullWhen(true)] string? path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetCreationTime(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetCreationTimeUtc(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual string GetCurrentDirectory() => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual string[] GetDirectories(string path) => [.. this.EnumerateDirectories(path)];

            /// <inheritdoc/>
            public virtual string[] GetDirectories(string path, string searchPattern) => [.. this.EnumerateDirectories(path, searchPattern)];

            /// <inheritdoc/>
            public virtual string[] GetDirectories(string path, string searchPattern, SearchOption searchOption) => [.. this.EnumerateDirectories(path, searchPattern, searchOption)];

            /// <inheritdoc/>
            public virtual string[] GetDirectories(string path, string searchPattern, EnumerationOptions enumerationOptions) => [.. this.EnumerateDirectories(path, searchPattern, enumerationOptions)];

            /// <inheritdoc/>
            public virtual string GetDirectoryRoot(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual string[] GetFiles(string path) => [.. this.EnumerateFiles(path)];

            /// <inheritdoc/>
            public virtual string[] GetFiles(string path, string searchPattern) => [.. this.EnumerateFiles(path, searchPattern)];

            /// <inheritdoc/>
            public virtual string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => [.. this.EnumerateFiles(path, searchPattern, searchOption)];

            /// <inheritdoc/>
            public virtual string[] GetFiles(string path, string searchPattern, EnumerationOptions enumerationOptions) => [.. this.EnumerateFiles(path, searchPattern, enumerationOptions)];

            /// <inheritdoc/>
            public virtual string[] GetFileSystemEntries(string path) => [.. this.EnumerateFileSystemEntries(path)];

            /// <inheritdoc/>
            public virtual string[] GetFileSystemEntries(string path, string searchPattern) => [.. this.EnumerateFileSystemEntries(path, searchPattern)];

            /// <inheritdoc/>
            public virtual string[] GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption) => [.. this.EnumerateFileSystemEntries(path, searchPattern, searchOption)];

            /// <inheritdoc/>
            public virtual string[] GetFileSystemEntries(string path, string searchPattern, EnumerationOptions enumerationOptions) => [.. this.EnumerateFileSystemEntries(path, searchPattern, enumerationOptions)];

            /// <inheritdoc/>
            public virtual DateTime GetLastAccessTime(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetLastAccessTimeUtc(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetLastWriteTime(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetLastWriteTimeUtc(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual string[] GetLogicalDrives() => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual IDirectoryInfo? GetParent(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void Move(string sourceDirName, string destDirName) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual IFileSystemInfo? ResolveLinkTarget(string linkPath, bool returnFinalTarget) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetCreationTime(string path, DateTime creationTime) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetCreationTimeUtc(string path, DateTime creationTimeUtc) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetCurrentDirectory(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetLastAccessTime(string path, DateTime lastAccessTime) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetLastWriteTime(string path, DateTime lastWriteTime) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc) => throw new NotImplementedException();
        }

        public class DirectoryInfoFactoryBase : IDirectoryInfoFactory
        {
            private readonly FileSystemBase parent;

            public DirectoryInfoFactoryBase(FileSystemBase parent)
            {
                this.parent = parent;
            }

            /// <inheritdoc/>
            public IFileSystem FileSystem => this.parent;

            /// <inheritdoc/>
            public IDirectoryInfo FromDirectoryName(string directoryName) => throw new NotImplementedException();

            /// <inheritdoc/>
            public IDirectoryInfo New(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            [return: NotNullIfNotNull(nameof(directoryInfo))]
            public IDirectoryInfo? Wrap(DirectoryInfo? directoryInfo) => throw new NotImplementedException();
        }

        public class DriveInfoFactoryBase : IDriveInfoFactory
        {
            private readonly FileSystemBase parent;

            public DriveInfoFactoryBase(FileSystemBase parent)
            {
                this.parent = parent;
            }

            /// <inheritdoc/>
            public IFileSystem FileSystem => this.parent;

            /// <inheritdoc/>
            public IDriveInfo FromDriveName(string driveName) => throw new NotImplementedException();

            /// <inheritdoc/>
            public IDriveInfo[] GetDrives() => throw new NotImplementedException();

            /// <inheritdoc/>
            public IDriveInfo New(string driveName) => throw new NotImplementedException();

            /// <inheritdoc/>
            [return: NotNullIfNotNull(nameof(driveInfo))]
            public IDriveInfo? Wrap(DriveInfo? driveInfo) => throw new NotImplementedException();
        }

        public class FileBase : IFile
        {
            private readonly FileSystemBase parent;

            public FileBase(FileSystemBase parent)
            {
                this.parent = parent;
            }

            public static void EnsureReadOnly(FileStreamOptions options, string message)
            {
                if ((options.Access & FileAccess.Write) == FileAccess.Write)
                {
                    throw new IOException(message);
                }
            }

            public static FileStreamOptions GetParentOptions(FileStreamOptions options)
            {
                return new FileStreamOptions()
                {
                    Access = options.Access,
                    Mode = FileMode.Open,
                    Share = FileShare.ReadWrite,
                    BufferSize = options.BufferSize,
                    PreallocationSize = options.PreallocationSize,
                    Options = options.Options & (FileOptions.Asynchronous | FileOptions.WriteThrough),
                };
            }

            /// <inheritdoc/>
            public virtual IFileSystem FileSystem => this.parent;

            /// <inheritdoc/>
            public virtual void AppendAllLines(string path, IEnumerable<string> contents) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void AppendAllLines(string path, IEnumerable<string> contents, Encoding encoding) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual Task AppendAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual Task AppendAllLinesAsync(string path, IEnumerable<string> contents, Encoding encoding, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void AppendAllText(string path, string? contents) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void AppendAllText(string path, string? contents, Encoding encoding) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual Task AppendAllTextAsync(string path, string? contents, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual Task AppendAllTextAsync(string path, string? contents, Encoding encoding, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual StreamWriter AppendText(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void Copy(string sourceFileName, string destFileName) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void Copy(string sourceFileName, string destFileName, bool overwrite) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual FileSystemStream Create(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual FileSystemStream Create(string path, int bufferSize) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual FileSystemStream Create(string path, int bufferSize, FileOptions options) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual IFileSystemInfo CreateSymbolicLink(string path, string pathToTarget) => throw new NotSupportedException();

            /// <inheritdoc/>
            public virtual StreamWriter CreateText(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void Decrypt(string path) => throw new NotSupportedException();

            /// <inheritdoc/>
            public virtual void Delete(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void Encrypt(string path) => throw new NotSupportedException();

            /// <inheritdoc/>
            public virtual bool Exists([NotNullWhen(true)] string? path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual FileAttributes GetAttributes(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual FileAttributes GetAttributes(SafeFileHandle fileHandle) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetCreationTime(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetCreationTime(SafeFileHandle fileHandle) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetCreationTimeUtc(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetCreationTimeUtc(SafeFileHandle fileHandle) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetLastAccessTime(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetLastAccessTime(SafeFileHandle fileHandle) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetLastAccessTimeUtc(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetLastAccessTimeUtc(SafeFileHandle fileHandle) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetLastWriteTime(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetLastWriteTime(SafeFileHandle fileHandle) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetLastWriteTimeUtc(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual DateTime GetLastWriteTimeUtc(SafeFileHandle fileHandle) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual UnixFileMode GetUnixFileMode(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual UnixFileMode GetUnixFileMode(SafeFileHandle fileHandle) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void Move(string sourceFileName, string destFileName) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void Move(string sourceFileName, string destFileName, bool overwrite) => throw new NotImplementedException();

            /// <inheritdoc/>
            public FileSystemStream Open(string path, FileMode mode) => this.Open(path, mode, FileAccess.ReadWrite);

            /// <inheritdoc/>
            public FileSystemStream Open(string path, FileMode mode, FileAccess access) => this.Open(path, mode, access, FileShare.None);

            /// <inheritdoc/>
            public FileSystemStream Open(string path, FileMode mode, FileAccess access, FileShare share) => this.Open(path, new FileStreamOptions { Mode = mode, Access = access, Share = share });

            /// <inheritdoc/>
            public virtual FileSystemStream Open(string path, FileStreamOptions options) => throw new NotImplementedException();

            /// <inheritdoc/>
            public FileSystemStream OpenRead(string path) => this.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            /// <inheritdoc/>
            public StreamReader OpenText(string path) => new StreamReader(this.OpenRead(path));

            /// <inheritdoc/>
            public FileSystemStream OpenWrite(string path) => this.Open(path, FileMode.OpenOrCreate, FileAccess.Write);

            /// <inheritdoc/>
            public virtual byte[] ReadAllBytes(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual string[] ReadAllLines(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual string[] ReadAllLines(string path, Encoding encoding) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual Task<string[]> ReadAllLinesAsync(string path, Encoding encoding, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual string ReadAllText(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual string ReadAllText(string path, Encoding encoding) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual Task<string> ReadAllTextAsync(string path, Encoding encoding, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual IEnumerable<string> ReadLines(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual IEnumerable<string> ReadLines(string path, Encoding encoding) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual IAsyncEnumerable<string> ReadLinesAsync(string path, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual IAsyncEnumerable<string> ReadLinesAsync(string path, Encoding encoding, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void Replace(string sourceFileName, string destinationFileName, string? destinationBackupFileName) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void Replace(string sourceFileName, string destinationFileName, string? destinationBackupFileName, bool ignoreMetadataErrors) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual IFileSystemInfo? ResolveLinkTarget(string linkPath, bool returnFinalTarget) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetAttributes(string path, FileAttributes fileAttributes) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetAttributes(SafeFileHandle fileHandle, FileAttributes fileAttributes) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetCreationTime(string path, DateTime creationTime) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetCreationTime(SafeFileHandle fileHandle, DateTime creationTime) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetCreationTimeUtc(string path, DateTime creationTimeUtc) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetCreationTimeUtc(SafeFileHandle fileHandle, DateTime creationTimeUtc) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetLastAccessTime(string path, DateTime lastAccessTime) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetLastAccessTime(SafeFileHandle fileHandle, DateTime lastAccessTime) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetLastAccessTimeUtc(SafeFileHandle fileHandle, DateTime lastAccessTimeUtc) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetLastWriteTime(string path, DateTime lastWriteTime) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetLastWriteTime(SafeFileHandle fileHandle, DateTime lastWriteTime) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetLastWriteTimeUtc(SafeFileHandle fileHandle, DateTime lastWriteTimeUtc) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetUnixFileMode(string path, UnixFileMode mode) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void SetUnixFileMode(SafeFileHandle fileHandle, UnixFileMode mode) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void WriteAllBytes(string path, byte[] bytes) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void WriteAllLines(string path, string[] contents) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void WriteAllLines(string path, IEnumerable<string> contents) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void WriteAllLines(string path, string[] contents, Encoding encoding) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual Task WriteAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual Task WriteAllLinesAsync(string path, IEnumerable<string> contents, Encoding encoding, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void WriteAllText(string path, string? contents) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual void WriteAllText(string path, string? contents, Encoding encoding) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual Task WriteAllTextAsync(string path, string? contents, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            /// <inheritdoc/>
            public virtual Task WriteAllTextAsync(string path, string? contents, Encoding encoding, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        public class FileInfoFactoryBase : IFileInfoFactory
        {
            private readonly FileSystemBase parent;

            public FileInfoFactoryBase(FileSystemBase parent)
            {
                this.parent = parent;
            }

            /// <inheritdoc/>
            public IFileSystem FileSystem => this.parent;

            /// <inheritdoc/>
            public IFileInfo FromFileName(string fileName) => throw new NotImplementedException();

            /// <inheritdoc/>
            public IFileInfo New(string fileName) => throw new NotImplementedException();

            /// <inheritdoc/>
            [return: NotNullIfNotNull(nameof(fileInfo))]
            public IFileInfo? Wrap(FileInfo? fileInfo) => throw new NotImplementedException();
        }

        public class FileStreamFactoryBase : IFileStreamFactory
        {
            private readonly FileSystemBase parent;

            public FileStreamFactoryBase(FileSystemBase parent)
            {
                this.parent = parent;
            }

            /// <inheritdoc/>
            public IFileSystem FileSystem => this.parent;

            /// <inheritdoc/>
            public Stream Create(string path, FileMode mode) => throw new NotImplementedException();

            /// <inheritdoc/>
            public Stream Create(string path, FileMode mode, FileAccess access) => throw new NotImplementedException();

            /// <inheritdoc/>
            public Stream Create(string path, FileMode mode, FileAccess access, FileShare share) => throw new NotImplementedException();

            /// <inheritdoc/>
            public Stream Create(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize) => throw new NotImplementedException();

            /// <inheritdoc/>
            public Stream Create(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options) => throw new NotImplementedException();

            /// <inheritdoc/>
            public Stream Create(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, bool useAsync) => throw new NotImplementedException();

            /// <inheritdoc/>
            public Stream Create(SafeFileHandle handle, FileAccess access) => throw new NotImplementedException();

            /// <inheritdoc/>
            public Stream Create(SafeFileHandle handle, FileAccess access, int bufferSize) => throw new NotImplementedException();

            /// <inheritdoc/>
            public Stream Create(SafeFileHandle handle, FileAccess access, int bufferSize, bool isAsync) => throw new NotImplementedException();

            /// <inheritdoc/>
            public Stream Create(nint handle, FileAccess access) => throw new NotImplementedException();

            /// <inheritdoc/>
            public Stream Create(nint handle, FileAccess access, bool ownsHandle) => throw new NotImplementedException();

            /// <inheritdoc/>
            public Stream Create(nint handle, FileAccess access, bool ownsHandle, int bufferSize) => throw new NotImplementedException();

            /// <inheritdoc/>
            public Stream Create(nint handle, FileAccess access, bool ownsHandle, int bufferSize, bool isAsync) => throw new NotImplementedException();

            /// <inheritdoc/>
            public FileSystemStream New(SafeFileHandle handle, FileAccess access) => throw new NotImplementedException();

            /// <inheritdoc/>
            public FileSystemStream New(SafeFileHandle handle, FileAccess access, int bufferSize) => throw new NotImplementedException();

            /// <inheritdoc/>
            public FileSystemStream New(SafeFileHandle handle, FileAccess access, int bufferSize, bool isAsync) => throw new NotImplementedException();

            /// <inheritdoc/>
            public FileSystemStream New(string path, FileMode mode) => throw new NotImplementedException();

            /// <inheritdoc/>
            public FileSystemStream New(string path, FileMode mode, FileAccess access) => throw new NotImplementedException();

            /// <inheritdoc/>
            public FileSystemStream New(string path, FileMode mode, FileAccess access, FileShare share) => throw new NotImplementedException();

            /// <inheritdoc/>
            public FileSystemStream New(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize) => throw new NotImplementedException();

            /// <inheritdoc/>
            public FileSystemStream New(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, bool useAsync) => throw new NotImplementedException();

            /// <inheritdoc/>
            public FileSystemStream New(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options) => throw new NotImplementedException();

            /// <inheritdoc/>
            public FileSystemStream New(string path, FileStreamOptions options) => throw new NotImplementedException();

            /// <inheritdoc/>
            public FileSystemStream Wrap(FileStream fileStream) => throw new NotImplementedException();
        }

        public class FileSystemWatcherFactoryBase : IFileSystemWatcherFactory
        {
            private readonly FileSystemBase parent;

            public FileSystemWatcherFactoryBase(FileSystemBase parent)
            {
                this.parent = parent;
            }

            /// <inheritdoc/>
            public IFileSystem FileSystem => this.parent;

            /// <inheritdoc/>
            public IFileSystemWatcher CreateNew() => throw new NotImplementedException();

            /// <inheritdoc/>
            public IFileSystemWatcher CreateNew(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public IFileSystemWatcher CreateNew(string path, string filter) => throw new NotImplementedException();

            /// <inheritdoc/>
            public IFileSystemWatcher New() => throw new NotImplementedException();

            /// <inheritdoc/>
            public IFileSystemWatcher New(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public IFileSystemWatcher New(string path, string filter) => throw new NotImplementedException();

            /// <inheritdoc/>
            [return: NotNullIfNotNull(nameof(fileSystemWatcher))]
            public IFileSystemWatcher? Wrap(FileSystemWatcher? fileSystemWatcher) => throw new NotImplementedException();
        }

        public class PathBase : IPath
        {
            private readonly FileSystemBase parent;

            public PathBase(FileSystemBase parent)
            {
                this.parent = parent;
            }

            /// <inheritdoc/>
            public virtual char AltDirectorySeparatorChar => this.DirectorySeparatorChar == '/' ? '\\' : '/';

            /// <inheritdoc/>
            public virtual char DirectorySeparatorChar => '/';

            /// <inheritdoc/>
            public virtual char PathSeparator => ';';

            /// <inheritdoc/>
            public virtual char VolumeSeparatorChar => ':';

            /// <inheritdoc/>
            public IFileSystem FileSystem => this.parent;

            /// <inheritdoc/>
            [return: NotNullIfNotNull(nameof(path))]
            public string? ChangeExtension(string? path, string? extension) => PathExtensions.ChangeExtension(path, extension);

            /// <inheritdoc/>
            public string Combine(string path1, string path2) => this.CombineWithSeparator(this.DirectorySeparatorChar, path1, path2);

            /// <inheritdoc/>
            public string Combine(string path1, string path2, string path3) => throw new NotImplementedException();

            /// <inheritdoc/>
            public string Combine(string path1, string path2, string path3, string path4) => throw new NotImplementedException();

            /// <inheritdoc/>
            public string Combine(params string[] paths) => throw new NotImplementedException();

            /// <inheritdoc/>
            public bool EndsInDirectorySeparator(ReadOnlySpan<char> path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public bool EndsInDirectorySeparator(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public bool Exists([NotNullWhen(true)] string? path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public ReadOnlySpan<char> GetDirectoryName(ReadOnlySpan<char> path) => PathExtensions.GetDirectoryName(path);

            /// <inheritdoc/>
            public string? GetDirectoryName(string? path) => PathExtensions.GetDirectoryName(path);

            /// <inheritdoc/>
            public ReadOnlySpan<char> GetExtension(ReadOnlySpan<char> path) => PathExtensions.GetExtension(path);

            /// <inheritdoc/>
            [return: NotNullIfNotNull(nameof(path))]
            public string? GetExtension(string? path) => PathExtensions.GetExtension(path);

            /// <inheritdoc/>
            public ReadOnlySpan<char> GetFileName(ReadOnlySpan<char> path) => PathExtensions.GetFileName(path);

            /// <inheritdoc/>
            [return: NotNullIfNotNull(nameof(path))]
            public string? GetFileName(string? path) => PathExtensions.GetFileName(path);

            /// <inheritdoc/>
            public ReadOnlySpan<char> GetFileNameWithoutExtension(ReadOnlySpan<char> path) => PathExtensions.GetFileNameWithoutExtension(path);

            /// <inheritdoc/>
            [return: NotNullIfNotNull(nameof(path))]
            public string? GetFileNameWithoutExtension(string? path) => PathExtensions.GetFileNameWithoutExtension(path);

            /// <inheritdoc/>
            public string GetFullPath(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public string GetFullPath(string path, string basePath) => throw new NotImplementedException();

            /// <inheritdoc/>
            public char[] GetInvalidFileNameChars() => throw new NotImplementedException();

            /// <inheritdoc/>
            public char[] GetInvalidPathChars() => throw new NotImplementedException();

            /// <inheritdoc/>
            public ReadOnlySpan<char> GetPathRoot(ReadOnlySpan<char> path) => PathExtensions.GetPathRoot(path);

            /// <inheritdoc/>
            public string? GetPathRoot(string? path) => PathExtensions.GetPathRoot(path);

            /// <inheritdoc/>
            public string GetRandomFileName() => throw new NotImplementedException();

            /// <inheritdoc/>
            public string GetRelativePath(string relativeTo, string path) => PathExtensions.GetRelativePath(this, relativeTo, path);

            /// <inheritdoc/>
            public string GetTempFileName() => throw new NotImplementedException();

            /// <inheritdoc/>
            public string GetTempPath() => throw new NotImplementedException();

            /// <inheritdoc/>
            public bool HasExtension(ReadOnlySpan<char> path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public bool HasExtension([NotNullWhen(true)] string? path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public bool IsPathFullyQualified(ReadOnlySpan<char> path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public bool IsPathFullyQualified(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public bool IsPathRooted(ReadOnlySpan<char> path) => PathExtensions.IsPathRooted(path);

            /// <inheritdoc/>
            public bool IsPathRooted([NotNullWhen(true)] string? path) => PathExtensions.IsPathRooted(path);

            /// <inheritdoc/>
            public string Join(ReadOnlySpan<char> path1, ReadOnlySpan<char> path2) => throw new NotImplementedException();

            /// <inheritdoc/>
            public string Join(ReadOnlySpan<char> path1, ReadOnlySpan<char> path2, ReadOnlySpan<char> path3) => throw new NotImplementedException();

            /// <inheritdoc/>
            public string Join(string? path1, string? path2) => throw new NotImplementedException();

            /// <inheritdoc/>
            public string Join(string? path1, string? path2, string? path3) => throw new NotImplementedException();

            /// <inheritdoc/>
            public string Join(params string?[] paths) => throw new NotImplementedException();

            /// <inheritdoc/>
            public string Join(ReadOnlySpan<char> path1, ReadOnlySpan<char> path2, ReadOnlySpan<char> path3, ReadOnlySpan<char> path4) => throw new NotImplementedException();

            /// <inheritdoc/>
            public string Join(string? path1, string? path2, string? path3, string? path4) => throw new NotImplementedException();

            /// <inheritdoc/>
            public ReadOnlySpan<char> TrimEndingDirectorySeparator(ReadOnlySpan<char> path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public string TrimEndingDirectorySeparator(string path) => throw new NotImplementedException();

            /// <inheritdoc/>
            public bool TryJoin(ReadOnlySpan<char> path1, ReadOnlySpan<char> path2, Span<char> destination, out int charsWritten) => throw new NotImplementedException();

            /// <inheritdoc/>
            public bool TryJoin(ReadOnlySpan<char> path1, ReadOnlySpan<char> path2, ReadOnlySpan<char> path3, Span<char> destination, out int charsWritten) => throw new NotImplementedException();
        }
    }
}
