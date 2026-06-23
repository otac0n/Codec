namespace Codec.UI.Avalonia.Views
{
    using global::Avalonia.Controls;
    using global::Avalonia.Input;
    using Codec.UI.Avalonia.ViewModels;

    public partial class EntryListView : UserControl
    {
        public EntryListView() =>
            this.InitializeComponent();

        private void ListBox_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (this.DataContext is EntryListViewModel vm)
            {
                vm.Preview();
            }
        }
    }
}
