// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.UI
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Diagnostics;
    using System.IO;
    using Codec.Services;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Serilog;

    public class ServiceRegistration
    {
        public static ServiceProvider RegisterHeadless()
        {
            var rootCommand = new RootCommand();
            M2.ArchiveOptions.Attach(rootCommand);
            var emptyContext = new InvocationContext(rootCommand.Parse([]));
            var services = new ServiceCollection();
            Register(emptyContext, services);
            return services.BuildServiceProvider();
        }

        public static void Register(InvocationContext context, IServiceCollection services)
        {
            Log.Logger = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Debug()
#else
                .MinimumLevel.Warning()
#endif
                .WriteTo.Console()
                .WriteTo.File(
                    Path.ChangeExtension(Environment.ProcessPath!, ".log"),
                    fileSizeLimitBytes: 5 * 1024 * 1024, // 5 MB
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: 5,
                    shared: true)
                .CreateLogger();

            services.AddLogging(builder =>
            {
                builder.AddSerilog();

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
            M2.ArchiveOptions.Bind(context, services);
            services.AddTransient<FileExportService>();
        }
    }
}
