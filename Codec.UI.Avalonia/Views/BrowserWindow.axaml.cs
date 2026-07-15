namespace Codec.UI.Avalonia.Views
{
    using System;
    using global::Avalonia.Controls;
    using global::Avalonia.Media.Imaging;
    using global::Avalonia.Threading;
    using Codec.Files;
    using Codec.UI.Avalonia.ViewModels;
    using Microsoft.Extensions.Logging;

    public partial class BrowserWindow : Window
    {
        private readonly BrowserViewModel viewModel;
        private readonly NotifyingLoggerProvider provider;
        private readonly ILogger<BrowserWindow> logger;

        public BrowserWindow(BrowserViewModel viewModel, NotifyingLoggerProvider provider, ILogger<BrowserWindow> logger)
        {
            this.InitializeComponent();
            viewModel.AudioPreviewRequested += this.OnAudioPreviewRequested;
            viewModel.ImagePreviewRequested += this.OnImagePreviewRequested;
            viewModel.ModelPreviewRequested += this.OnModelPreviewRequested;
            provider.EntryLogged += this.Provider_EntryLogged;
            this.viewModel = viewModel;
            this.provider = provider;
            this.logger = logger;
            this.DataContext = viewModel;
        }

        private void Provider_EntryLogged(object? sender, NotifyingLoggerProvider.LogEntry e)
        {
            Dispatcher.UIThread.Invoke(() => this.viewModel.Errors.Add(e));
        }

        private void OnAudioPreviewRequested(object? sender, BrowserViewModel.PreviewRequestedEventArgs<AudioStream> args)
        {
            try
            {
                var preview = new AudioPreviewWindow(args.Item)
                {
                    Title = args.Parent.GetFileName(args.Path),
                };
                preview.Show(this);
            }
            catch (Exception ex)
            {
                this.logger.FailedToLoad(ex, args.Path);
            }
        }

        private void OnImagePreviewRequested(object? sender, BrowserViewModel.PreviewRequestedEventArgs<Bitmap> args)
        {
            var preview = new ImagePreviewWindow(args.Item)
            {
                Title = args.Parent.GetFileName(args.Path),
            };
            preview.Show(this);
        }

        private void OnModelPreviewRequested(object? sender, BrowserViewModel.PreviewRequestedEventArgs<RenderableScene> args)
        {
            var preview = new ModelPreviewWindow(args.Path, args.Parent, args.Item)
            {
                Title = args.Parent.GetFileName(args.Path),
            };
            preview.Show(this);
        }
    }
}
