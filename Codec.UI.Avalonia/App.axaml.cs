namespace Codec.UI.Avalonia
{
    using System;
    using global::Avalonia;
    using global::Avalonia.Controls.ApplicationLifetimes;
    using global::Avalonia.Markup.Xaml;
    using Codec.UI.Avalonia.Views;
    using Microsoft.Extensions.DependencyInjection;

    public partial class App(IServiceProvider services) : Application
    {
        public override void Initialize() =>
            AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = services.GetRequiredService<BrowserWindow>();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
