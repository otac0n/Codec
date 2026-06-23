// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.UI
{
    using System.CommandLine.Invocation;
    using Codec.Services;
    using Microsoft.Extensions.DependencyInjection;

    public class ServiceRegistration
    {
        public static void Register(InvocationContext context, IServiceCollection services)
        {
            Codec.ServiceRegistration.Register(services);
            Audio.ServiceRegistration.Register(services);
            Imaging.ServiceRegistration.Register(services);
            Geometry.ServiceRegistration.Register(services);
            M2.ServiceRegistration.Register(services);
            MGS.ServiceRegistration.Register(services);
            EnvironmentOptions.Bind(context, services);
            M2.ArchiveOptions.Bind(context, services);
            services.AddTransient<FileExportService>();
        }
    }
}
