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

    public static class ServiceRegistration
    {
        public static void Register(IServiceCollection services)
        {
            CueSheetVirtualFileSystem.Register(services);
            ZipArchiveFileSystem.Register(services);

            services.AddSingleton<EntryTypeDetector>();
            services.AddSingleton<RootEnumerableFileSystem>();

            services.AddSingleton(s =>
            {
                var handlers = s.GetServices<FileSystemResolver>().Select(r => new FileSystemHandler((a, b, c, d) => r(s, a, b, c, d))).ToArray();
                return new NestedFileSystemManager(s, s.GetRequiredService<RootEnumerableFileSystem>(), handlers);
            });

            SetupHelper.SetupComplete();
        }

        public static T? Resolve<T>(this IServiceProvider services, string path, string subPath, IFileSystem fs, string fsPath) =>
            services.Resolve(services.GetServices<FileHandlerResolver<T>>(), path, subPath, fs, fsPath);

        public static T? Resolve<T>(this IServiceProvider services, IEnumerable<FileHandlerResolver<T>> resolvers, string path, string subPath, IFileSystem fs, string fsPath) =>
            !fs.File.Exists(subPath)
            ? default
            : (from filter in resolvers
               where filter != null
               let resolver = filter(services, path, subPath, fs, fsPath)
               where resolver is not null
               let resolved = resolver(path, subPath, fs, fsPath)
               where resolved is not null
               select resolved).FirstOrDefault();
    }
}
