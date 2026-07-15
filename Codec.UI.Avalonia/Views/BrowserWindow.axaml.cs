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

        public BrowserWindow(BrowserViewModel viewModel, NotifyingLoggerProvider provider)
        {
            this.InitializeComponent();
            viewModel.AudioPreviewRequested += this.OnAudioPreviewRequested;
            viewModel.ImagePreviewRequested += this.OnImagePreviewRequested;
            viewModel.ModelPreviewRequested += this.OnModelPreviewRequested;
            provider.EntryLogged += this.Provider_EntryLogged;
            this.viewModel = viewModel;
            this.provider = provider;
            this.DataContext = viewModel;
        }

        private void Provider_EntryLogged(object? sender, NotifyingLoggerProvider.LogEntry e)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
            });
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
                this.viewModel.StatusMessage = $"Failed to play audio: {ex.Message}";
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
