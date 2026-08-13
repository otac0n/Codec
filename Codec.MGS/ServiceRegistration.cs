// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS
{
    using System;
    using Codec.Archives;
    using Codec.MGS.Archives;
    using Codec.MGS.Files;
    using DiscUtils.Iso9660;
    using Microsoft.Extensions.DependencyInjection;

    public static class ServiceRegistration
    {
        public static void Register(IServiceCollection services)
        {
            CtxrFile.Register(services);
            GlyFile.Register(services);
            MdxFile.Register(services);
            TriFile.Register(services);
            TplFile.Register(services);
            TxnFile.Register(services);
            TxpFile.Register(services);
            PllFile.Register(services);
            RpkFile.Register(services);
            WvxFile.Register(services);

            KmdFile.Register(services);
            KmsFile.Register(services);
            KmyFile.Register(services);
            MdnFile.Register(services);
            ZmdFile.Register(services);

            MgzArchive.Register(services);

            BrfDatArchive.Register(services);
            DarArchive.Register(services);
            DldArchive.Register(services);
            DemoDatArchive.Register(services);
            DlzArchive.Register(services);
            FaceDatArchive.Register(services);
            RadioDatArchive.Register(services);
            StageDatArchive.Register(services);
            StageDirArchive.Register(services);
            SlotArchive.Register(services);
            SdtArchive.Register(services);
            SdxArchive.Register(services);
            VoxDatArchive.Register(services);
            ZarArchive.Register(services);

            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (parent.Path.GetFileName(parentRelativePath).EndsWith("-washed.bin", StringComparison.OrdinalIgnoreCase) &&
                    parent.Path.GetFileName(parent.Path.GetDirectoryName(parentRelativePath)) == "roms")
                {
                    return static (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        var file = parent.File.OpenRead(parentRelativePath);
                        var cdSector = new CDSectorStream(file, CDSectorStream.XAForm1);
                        var cdReader = new CDReader(cdSector, joliet: false);
                        return new DiscUtilsFileSystemAdapter(cdReader);
                    };
                }

                return null;
            });
        }
    }
}
