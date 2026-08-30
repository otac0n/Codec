namespace Codec.UI.Avalonia
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using global::Avalonia;
    using Codec.Services;
    using Codec.UI.Avalonia.Services;
    using Codec.UI.Avalonia.ViewModels;
    using Codec.UI.Avalonia.Views;
    using Microsoft.Extensions.DependencyInjection;

    sealed class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            var rootCommand = new RootCommand();

            M2.ArchiveOptions.Attach(rootCommand);

            var browseCommand = new Command("browse", "Browse Files");
            browseCommand.AddAlias("browser");
            rootCommand.Add(browseCommand);

            void Browse(InvocationContext context)
            {
                var services = new ServiceCollection();
                Codec.UI.ServiceRegistration.Register(context, services);
                services.AddTransient<FileExportService>();

                services.AddSingleton<ImageLoader>();
                services.AddSingleton<FileSaveService>();
                services.AddTransient<FileTreeViewModel>();
                services.AddTransient<EntryListViewModel>();
                services.AddTransient<BrowserViewModel>();
                services.AddTransient<BrowserWindow>();

                using var serviceProvider = services.BuildServiceProvider();
                BuildAvaloniaApp(serviceProvider).StartWithClassicDesktopLifetime(args);
            }

            browseCommand.SetHandler(Browse);
            rootCommand.SetHandler(Browse);

            return rootCommand.Invoke(args);
        }

        public static AppBuilder BuildAvaloniaApp(IServiceProvider services)
            => AppBuilder.Configure(() => new App(services))
                .UsePlatformDetect()
                .With(new Win32PlatformOptions
                {
                    RenderingMode = [Win32RenderingMode.Wgl]
                })
                .With(new X11PlatformOptions
                {
                    RenderingMode = [X11RenderingMode.Glx]
                })
                .WithInterFont()
                .LogToTrace();
    }
}
