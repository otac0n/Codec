// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec.UI.WinForms
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Windows.Forms;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

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

            static void InstallSharedConfiguration(InvocationContext context, IServiceCollection services)
            {
                M2.ArchiveOptions.Bind(context, services);
                EnvironmentOptions.Bind(context, services);
                ServiceRegistration.Register(services);
            }

            void Browse(InvocationContext context)
            {
                var builder = Host.CreateDefaultBuilder(args);
                builder.ConfigureServices(services => InstallSharedConfiguration(context, services));

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
