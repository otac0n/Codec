namespace Codec.UI.Avalonia.Views
{
    using System;
    using global::Avalonia.Controls;
    using global::Avalonia.Media.Imaging;
    using Codec.Files;
    using Codec.UI.Avalonia.ViewModels;

    public partial class BrowserWindow : Window
    {
        private readonly BrowserViewModel viewModel;

        public BrowserWindow(BrowserViewModel viewModel)
        {
            this.InitializeComponent();
            viewModel.AudioPreviewRequested += this.OnAudioPreviewRequested;
            viewModel.ImagePreviewRequested += this.OnImagePreviewRequested;
            viewModel.ModelPreviewRequested += this.OnModelPreviewRequested;
            this.viewModel = viewModel;
            this.DataContext = viewModel;
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
