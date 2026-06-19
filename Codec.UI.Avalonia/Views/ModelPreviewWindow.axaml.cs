namespace Codec.UI.Avalonia.Views
{
    using Codec.Archives;
    using Codec.Files;
    using global::Avalonia.Controls;

    public partial class ModelPreviewWindow : Window
    {
        public ModelPreviewWindow(string path, NestedFileSystemManager fsm, RenderableScene? scene = null)
        {
            this.InitializeComponent();
            this.Content = new ModelRendererControl(path, fsm, scene);
        }
    }
}
