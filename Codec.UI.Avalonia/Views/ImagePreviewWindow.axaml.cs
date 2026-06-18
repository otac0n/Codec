namespace Codec.UI.Avalonia.Views
{
    using global::Avalonia.Controls;
    using global::Avalonia.Media.Imaging;

    public partial class ImagePreviewWindow : Window
    {
        public ImagePreviewWindow(Bitmap bitmap)
        {
            this.InitializeComponent();
            this.FindControl<Image>("PreviewImage")!.Source = bitmap;
        }
    }
}
