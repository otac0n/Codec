// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.UI
{
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Diagnostics;
    using Codec.Services;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;

    public class ServiceRegistration
    {
        public static ServiceProvider RegisterHeadless()
        {
            var rootCommand = new RootCommand();
            M2.ArchiveOptions.Attach(rootCommand);
            EnvironmentOptions.Attach(rootCommand);
            var emptyContext = new InvocationContext(rootCommand.Parse([]));
            var services = new ServiceCollection();
            Register(emptyContext, services);
            return services.BuildServiceProvider();
        }

        public static void Register(InvocationContext context, IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.AddConsole();

                var notifier = new NotifyingLoggerProvider();
                builder.Services.AddSingleton(notifier);
                builder.AddProvider(notifier);

                if (Debugger.IsAttached)
                {
                    builder.AddDebug();
                }
            });
            Codec.ServiceRegistration.Register(services);
            Audio.ServiceRegistration.Register(services);
            Imaging.ServiceRegistration.Register(services);
            Geometry.ServiceRegistration.Register(services);
            Rendering.ServiceRegistration.Register(services);
            M2.ServiceRegistration.Register(services);
            MGS.ServiceRegistration.Register(services);
            EnvironmentOptions.Bind(context, services);
            M2.ArchiveOptions.Bind(context, services);
            services.AddTransient<FileExportService>();
        }
    }
}
