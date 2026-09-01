namespace Codec.UI.Avalonia.Views
{
    using global::Avalonia.Controls;
    using Codec.UI.Avalonia.ViewModels;

    public partial class ProgressWindow : Window
    {
        public ProgressWindow()
        {
            this.InitializeComponent();
            this.Closing += this.ProgressWindow_Closing;
        }

        private void ProgressWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (this.DataContext is ProgressViewModel viewModel)
            {
                viewModel.CancelExportCommand.Execute(null);
            }
        }
    }
}
