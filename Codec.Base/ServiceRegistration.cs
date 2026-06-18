// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec
{
    using System;
    using System.Collections.Generic;
    using System.IO.Abstractions;
    using System.Linq;
    using Codec.Archives;
    using Codec.Files;
    using Codec.Services;
    using DiscUtils.Complete;
    using DiscUtils.Iso9660;
    using Microsoft.Extensions.DependencyInjection;

    public static class ServiceRegistration
    {
        public static void Register(IServiceCollection services)
        {
            CdaFile.Register(services);
            CtxrFile.Register(services);
            TriFile.Register(services);
            PllFile.Register(services);
            KmdFile.Register(services);
            KmsFile.Register(services);
            ImageMagickBitmapResolver.Register(services);

            CueSheetVirtualFileSystem.Register(services);
            BrfDatVirtualFileSystem.Register(services);
            FaceDatVirtualFileSystem.Register(services);
            StageDirVirtualFileSystem.Register(services);
            PsbVirtualFileSystem.Register(services);
            MVirtualFileSystem.Register(services);
            MArchiveV1VirtualFileSystem.Register(services);
            SlotVirtualFileSystem.Register(services);
            SdtVirtualFileSystem.Register(services);
            SdxVirtualFileSystem.Register(services);

            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (parent is MArchiveV1VirtualFileSystem &&
                    string.Equals(parent.Path.GetExtension(parentRelativePath), ".bin", StringComparison.OrdinalIgnoreCase) &&
                    parent.Path.GetFileName(parent.Path.GetDirectoryName(parentRelativePath)) == "roms")
                {
                    return static (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        var file = parent.File.OpenRead(parentRelativePath);
                        var cdSector = new CDSectorStream(file, CDSectorStream.XAForm1);
                        var cdReader = new CDReader(cdSector, joliet: false);
                        return new DiscUtilsVFSAdapter(cdReader);
                    };
                }

                return null;
            });
            services.AddSingleton<EntryTypeDetector>();

            services.AddSingleton(s =>
            {
                var handlers = s.GetServices<FileSystemResolver>().Select(r => new FileSystemHandler((a, b, c, d) => r(s, a, b, c, d))).ToArray();
                return new NestedFileSystemManager(s, new RootEnumerableFileSystem(), handlers);
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
