namespace Codec.MGS.Archives
{
    using System;
    using Codec.Archives;
    using Microsoft.Extensions.DependencyInjection;

    internal static class MgzFileSystemHandler
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileSystemResolver>((servicProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".mgz", StringComparison.OrdinalIgnoreCase))
                {
                    return static (fullPath, parentRelativePath, parent, parentPath) =>
                        new ZipArchiveFileSystem(parentRelativePath, parent);
                }

                return null;
            });
        }
    }
}
