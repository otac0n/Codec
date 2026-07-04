namespace Codec.UI.Avalonia.Views
{
    using global::Avalonia.Controls;
    using global::Avalonia.Input;
    using global::Avalonia.Interactivity;
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

        private void OnThumbnailContextRequested(object? sender, ContextRequestedEventArgs e)
        {
            if (this.DataContext is EntryListViewModel vm)
            {
                if (e.Source is Control { DataContext: ThumbnailItemViewModel thumbnail })
                {
                    vm.ContextEntry = thumbnail.Item;
                }
            }
        }

        private void OnEntryContextMenuClosed(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is EntryListViewModel vm)
            {
                vm.ContextEntry = null;
            }
        }
    }
}
