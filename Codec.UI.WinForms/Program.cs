// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec.UI.WinForms
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Diagnostics;
    using System.Windows.Forms;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    internal static partial class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static int Main(string[] args)
        {
            var rootCommand = new RootCommand();

            M2.ArchiveOptions.Attach(rootCommand);
            EnvironmentOptions.Attach(rootCommand);

            var browseCommand = new Command("browse", "Browse Files");
            browseCommand.AddAlias("browser");
            rootCommand.Add(browseCommand);

            void Browse(InvocationContext context)
            {
                var builder = Host.CreateDefaultBuilder(args);
                builder.ConfigureServices(services =>
                {
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        if (Debugger.IsAttached)
                        {
                            builder.AddDebug();
                        }
                    });

                    Codec.UI.ServiceRegistration.Register(context, services);
                    services.AddTransient<Browser>();
                });

                using var host = builder.Build();
                ApplicationConfiguration.Initialize();
                Application.Run(host.Services.GetRequiredService<Browser>());
            }

            browseCommand.SetHandler(Browse);
            rootCommand.SetHandler(Browse);

            return rootCommand.Invoke(args);
        }
    }
}
