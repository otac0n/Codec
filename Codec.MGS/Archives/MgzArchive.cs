namespace Codec.MGS.Archives
{
    using Codec.Archives;
    using Microsoft.Extensions.DependencyInjection;

    internal static class MgzArchive
    {
        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("*.mgz", static (fullPath, parentRelativePath, parent, parentPath) => new ZipArchiveFileSystem(parentRelativePath, parent));
        }
    }
}
