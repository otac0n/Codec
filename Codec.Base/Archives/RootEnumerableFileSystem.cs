namespace Codec.Archives
{
    using System.Collections.Generic;
    using System.IO.Abstractions;
    using System.Runtime.InteropServices;

    public class RootEnumerableFileSystem : FileSystem
    {
        private readonly RootEnumerableDirectoryWrapper directory;

        public RootEnumerableFileSystem()
        {
            this.directory = new RootEnumerableDirectoryWrapper(this);
        }

        public override IDirectory Directory => this.directory;

        public class RootEnumerableDirectoryWrapper : DirectoryWrapper
        {
            public RootEnumerableDirectoryWrapper(RootEnumerableFileSystem fileSystem)
                : base(fileSystem)
            {
            }

            public override IEnumerable<string> EnumerateFiles(string path)
            {
                if (path == string.Empty)
                {
                    return [];
                }
                else
                {
                    return base.EnumerateFiles(path);
                }
            }

            public override IEnumerable<string> EnumerateDirectories(string path)
            {
                if (path == string.Empty)
                {
                    IEnumerable<string> Enumerate()
                    {
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        {
                            yield return "/";
                            yield break;
                        }

                        foreach (var drive in this.FileSystem.DriveInfo.GetDrives())
                        {
                            yield return drive.RootDirectory.Name;
                        }
                    }

                    return Enumerate();
                }
                else
                {
                    return base.EnumerateDirectories(path);
                }
            }
        }
    }
}
