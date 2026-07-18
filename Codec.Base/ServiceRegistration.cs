// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec
{
    using System;
    using System.Collections.Generic;
    using System.IO.Abstractions;
    using System.Linq;
    using Codec.Archives;
    using Codec.Services;
    using DiscUtils.Complete;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public static class ServiceRegistration
    {
        public static void Register(IServiceCollection services)
        {
            CueSheetVirtualFileSystem.Register(services);
            CisoSparseStreamVFS.Register(services);
            GameCubeIsoVFS.Register(services);
            ZipArchiveFileSystem.Register(services);

            services.AddSingleton<EntryTypeDetector>();
            services.AddSingleton<RootEnumerableFileSystem>();
            services.AddSingleton<NestedFileSystemManager>();

            SetupHelper.SetupComplete();
        }

        public static T? Resolve<T>(this IServiceProvider services, string path, string subPath, IFileSystem fs, string fsPath) =>
            services.Resolve(services.GetServices<FileHandlerResolver<T>>(), path, subPath, fs, fsPath);

        public static Action<T>? ResolveWriter<T>(this IServiceProvider services, string path, string subPath, IFileSystem fs, string fsPath) =>
            services.ResolveWriter(services.GetServices<FileHandlerResolver<T>>(), path, subPath, fs, fsPath);

        public static T? Resolve<T>(this IServiceProvider services, IEnumerable<FileHandlerResolver<T>> resolvers, string path, string subPath, IFileSystem fs, string fsPath) =>
            !fs.File.Exists(subPath)
            ? default
            : (from filter in resolvers
               where filter != null
               let resolver = filter(services, path, subPath, fs, fsPath)
               where resolver is not null
               let resolved = resolver.Read(path, subPath, fs, fsPath)
               where resolved is not null
               select resolved).FirstOrDefault();

        public static Action<T>? ResolveWriter<T>(this IServiceProvider services, IEnumerable<FileHandlerResolver<T>> resolvers, string path, string subPath, IFileSystem fs, string fsPath) =>
            !fs.File.Exists(subPath)
            ? default
            : (from filter in resolvers
               where filter != null
               let resolver = filter(services, path, subPath, fs, fsPath)
               where resolver is not null && resolver.CanWrite
               let write = resolver.Write
               select new Action<T>(image => write(image, path, subPath, fs, fsPath))).FirstOrDefault();

        public static FileSystemFactory? GetDeferredFactory(this IServiceProvider services, string path, string subPath, IFileSystem fs, string fsPath, ILogger? logger = null)
        {
            var factories = services.GetServices<FileSystemResolver>().Select(r =>
            {
                try
                {
                    return r(services, path, subPath, fs, fsPath);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Skipping file system resolver due to execption.");
                    return null;
                }
            }).ToList();
            factories.RemoveAll(f => f == null);

            return factories switch
            {
                [] => null,
                [var factory] => factory,
                _ => (fullPath, parentRelativePath, parent, parentPath) =>
                {
                    foreach (var f in factories)
                    {
                        try
                        {
                            return f!(fullPath, parentRelativePath, parent, parentPath);
                        }
                        catch (Exception ex)
                        {
                            logger?.LogWarning(ex, "Skipping file system resolver due to execption.");
                        }
                    }

                    return null;
                },
            };
        }
    }
}
