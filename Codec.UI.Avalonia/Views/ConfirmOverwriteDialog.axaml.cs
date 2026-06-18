namespace Codec.UI.Avalonia.Views
{
    using global::Avalonia.Controls;
    using global::Avalonia.Interactivity;

    public partial class ConfirmOverwriteDialog : Window
    {
        public ConfirmOverwriteDialog()
        {
            this.InitializeComponent();
        }

        private void OnYesClick(object sender, RoutedEventArgs e)
        {
            this.Close(true);
        }

        private void OnNoClick(object sender, RoutedEventArgs e)
        {
            this.Close(false);
        }
    }
}
