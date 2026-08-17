namespace Codec.UI.Avalonia.Views
{
    using global::Avalonia.Controls;
    using global::Avalonia.Interactivity;
    using Codec.UI.Avalonia.ViewModels;

    public partial class ExportDialog : Window
    {
        public ExportDialog(ExportViewModel exportViewModel)
        {
            this.InitializeComponent();
            this.DataContext = exportViewModel;
        }

        private void OnAccept(object sender, RoutedEventArgs e)
        {
            this.Close(true);
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            this.Close(false);
        }
    }
}
